using System.Collections.Immutable;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

internal sealed partial class MilitaryCorpsPlanner(SimulationEngine engine, ScenarioSessionDefinition definition,
    CorpsState corps, CommandContext context, MilitaryTurnBatch batch, List<DomainEvent> events)
{
    private readonly MilitaryRulebook rules = definition.MilitaryRules!;
    private int attempts, submitted, rejected, empty, groups;
    private bool incomplete;
    private MilitaryDecisionReason reason = MilitaryDecisionReason.NoMap;
    private sealed record RouteObjective(MilitaryPresence Target, int CellIndex);
    private sealed record ActionOption(GameCommand Command, int SourceId, int Objective, int Target, long Score,
        MilitaryCampaignStage Stage = MilitaryCampaignStage.Marching, int? DeliveryOfficerId = null,
        int ArrivalDays = 0, ImmutableArray<int> Officers = default);

    internal OriginalAiWorkReport Run()
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp(), allocated = GC.GetAllocatedBytesForCurrentThread();
        using var paths = new PathSearchDiagnostics();
        long revision = engine.Snapshot.Revision;
        batch.State = batch.State.Reconcile(engine.Snapshot, rules);
        if (rules is not null)
        {
            reason = MilitaryDecisionReason.NoSource;
            var queries = batch.Queries(engine.Snapshot, rules, corps.OwnerId);
            ReviewCampaigns(queries);
            foreach (var withdrawing in batch.State.Campaigns.Where(c => c.ForceId == corps.OwnerId &&
                c.Stage is MilitaryCampaignStage.Withdrawing or MilitaryCampaignStage.Consolidating).ToArray()) Withdraw(withdrawing, queries);
            bool emergency = Rescue(queries);
            Evacuate(queries);
            var active = batch.State.Campaigns.FirstOrDefault(c => c.CorpsId == corps.Id && c.Purpose == MilitaryCampaignPurpose.Conquest &&
                c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled));
            if (active is not null && submitted == 0) reason = active.Stage switch
            {
                MilitaryCampaignStage.Assembling => MilitaryDecisionReason.AwaitingAssembly,
                MilitaryCampaignStage.Consolidating => MilitaryDecisionReason.Consolidating,
                MilitaryCampaignStage.Withdrawing => MilitaryDecisionReason.Withdrawing,
                _ => MilitaryDecisionReason.ExistingCommitment
            };
            if (!emergency)
            {
                queries = batch.Queries(engine.Snapshot, rules, corps.OwnerId);
                if (active is { Stage: MilitaryCampaignStage.Preparing } &&
                    engine.Snapshot.Units.FirstOrDefault(u => u.Id == active.UnitId && !u.IsEmbedded) is { } moving)
                    Continue(active, moving, queries);
                else if (!SupportCampaign(queries) && (active is null || active.Stage == MilitaryCampaignStage.Preparing)) Start(active, queries);
            }
            if (!emergency) RecoverStaff(batch.Queries(engine.Snapshot, rules, corps.OwnerId));
            incomplete |= queries.Budget.Exhausted;
        }
        return new(corps.Id, OriginalAiWork.Attack, incomplete ? OriginalAiWorkEnd.AnalysisIncomplete :
            OriginalAiWorkEnd.NoCandidates, attempts, submitted)
        {
            Executor = "military-campaign-v1", GroupEvaluations = groups,
            MilitaryReason = submitted > 0 ? MilitaryDecisionReason.Committed : incomplete ? MilitaryDecisionReason.AnalysisIncomplete :
                rejected > 0 ? MilitaryDecisionReason.PublicRejected : reason, InputRevision = revision, Rejected = rejected, EmptyPlans = empty,
            ElapsedTimestampTicks = System.Diagnostics.Stopwatch.GetTimestamp() - started,
            AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated,
            PathQueries = (int)paths.Queries, PathSearches = paths.Queries,
            PathExpanded = paths.Expanded, PathEndpointExpanded = paths.EndpointExpanded
        };

    }

    bool OwnSource(SettlementState s) => s.OwnerId == corps.OwnerId && (s.CorpsId == corps.Id || s.CorpsId is null && corps.IsRoot) &&
        !s.IsDormant && !s.IsDestroyed && rules.TryPlace(s, out _) && s.CanHostPersons && engine.CanStartSettlementWork(s.Id);
    bool Target(MilitaryPresence p) => p.Kind == MilitaryPresenceKind.Settlement &&
        MilitaryDiplomacy.AutomaticEnemy(engine.Snapshot, corps.OwnerId, p.OwnerId) && corps.Delegation switch
        {
            DomesticDelegation => false,
            DefeatForceDelegation directive => p.OwnerId == directive.ForceId,
            CaptureSettlementDelegation directive => p.Object.Id == directive.SettlementId,
            _ => true
        };
    void Replace(MilitaryCampaign old, MilitaryCampaign next) =>
        batch.State = batch.State with { Campaigns = batch.State.Campaigns.Replace(old, next) };
    bool Submit(GameCommand command)
    {
        attempts++;
        var result = engine.Execute(context, command);
        if (!result.Submission.Accepted) { rejected++; return false; }
        submitted++; events.AddRange(result.Events);
        AutomaticResponseRunner.Run(engine, events);
        return true;
    }
    (Guid Id, long Sequence) Identity()
    {
        long sequence = engine.Snapshot.Simulation.NextSequence;
        return (SimulationIdentity.Command(sequence, corps.OwnerId), sequence);
    }
    RouteObjective? FirstObjective(ImmutableArray<MapCell> cells, MilitaryObservation observation, int source, int final)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            var at = new MapOccupancy(MapCoordinates.Center(cell), new(5, 5));
            var obstacle = observation.Forces.Nearby(at).Objects.FirstOrDefault(p => p.Object.Id != source &&
                p.Kind == MilitaryPresenceKind.Settlement && p.OwnerId != corps.OwnerId &&
                MilitaryDiplomacy.AutomaticEnemy(engine.Snapshot, corps.OwnerId, p.OwnerId));
            if (obstacle is not null) return new(obstacle, i);
        }
        return observation.Forces.Find(new(final, true)) is { } destination ? new(destination, cells.Length - 1) : null;
    }
    void Start(MilitaryCampaign? continuation, MilitaryTravelQueries queries)
    {
        var world = engine.Snapshot;
        var force = world.Forces.Single(f => f.Id == corps.OwnerId);
        var busy = world.Simulation.Tasks.Select(t => t.OfficerId).ToHashSet();
        var evaluator = new MilitaryGroupEvaluator(rules!);
        int lastSource = batch.State.LastSources.GetValueOrDefault(corps.Id, -1);
        var eligible = world.Settlements.Where(s => OwnSource(s) && s.Soldiers > 0 &&
            (continuation is null || s.Id == continuation.TargetId)).OrderBy(s => s.Id).ToArray();
        var sources = eligible.Where(s => s.Id > lastSource).Concat(eligible.Where(s => s.Id <= lastSource)).ToArray();
        var targets = queries.Observation.Forces.Objects.Where(p => continuation is null ? Target(p) && !batch.State.Campaigns.Any(c =>
                c.ForceId == corps.OwnerId && c.Purpose == MilitaryCampaignPurpose.Conquest && c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled) &&
                (c.ObjectiveId == p.Object.Id || c.TargetId == p.Object.Id)) :
            p.Object == new MapObjectReference(continuation.ObjectiveId, true)).ToArray();
        if (sources.Length == 0) { reason = MilitaryDecisionReason.NoSource; return; }
        if (targets.Length == 0) { reason = MilitaryDecisionReason.NoTarget; return; }
        reason = MilitaryDecisionReason.NoGroup;
        var alternatives = rules!.Formations.Values.Where(f => f.TravelMode == TravelMode.Land &&
            rules.Purpose(f.Id) == UnitPurpose.Combat && f.Id != rules.UnformedFormationId)
            .OrderBy(f => f.Id).Select(f => new MilitaryFormationPair(f.Id, rules.BasicNavalFormationId)).ToArray();
        var options = new List<ActionOption>();
        foreach (var source in sources)
        {
            if (queries.Budget.RemainingSourceFields == 0 || batch.RemainingGroups(corps.OwnerId) == 0) { incomplete = true; break; }
            batch.State = batch.State with { LastSources = batch.State.LastSources.SetItem(corps.Id, source.Id) };
            var people = world.Officers.Where(p => p.OwnerId == force.Id && p.SettlementId == source.Id && !busy.Contains(p.Id) &&
                !MilitaryResourceLedger.OfficerReserved(batch.State, p.Id, continuation?.Id))
                .Select(p => MilitaryGroupEvaluator.ObserveOwnOfficer(p, force, definition.Rules.Abilities,
                    world.Treasures, definition.Rules.Treasures, rules)).ToArray();
            long reserve = queries.PotentialThreats(source.Id, 10).Candidates.Sum(p => (long)p.Soldiers);
            uint available = (uint)Math.Max(0, source.Soldiers - reserve - MilitaryResourceLedger.ReservedSoldiers(batch.State, source.Id, continuation?.Id));
            var group = evaluator.Search(force, people, alternatives, source.Id, available, Math.Max(0, engine.Snapshot.Corps.Single(c => c.Id == corps.Id).Treasury.Money - MilitaryResourceLedger.ReservedMoney(batch.State, corps.Id, continuation?.Id)),
                rules.SettlementArea(source).Position, MilitaryGroupRole.Siege,
                queries.Observation.Units.Where(u => u.OwnerId != corps.OwnerId).SelectMany(u => u.Equipment),
                maximumEvaluations: Math.Min(8, batch.RemainingGroups(corps.OwnerId)));
            groups += group.Evaluations;
            batch.SpendGroups(corps.OwnerId, group.Evaluations);
            if (group.Best is not { } best) { empty++; continue; }
            reason = MilitaryDecisionReason.Unreachable;
            var commander = world.Officers.Single(p => p.Id == best.CommanderId);
            uint capacity = CommanderCapacity.Read(commander.Id == force.LeaderId,
                definition.Content.Information.Ranks.FirstOrDefault(r => r.Id == force.RankId)?.CommandCapacity,
                definition.Content.Information.Offices.FirstOrDefault(o => o.Id == commander.OfficeId)?.CommandCapacity);
            best = best with { Soldiers = Math.Min(best.Soldiers, capacity) };
            var equipment = new Dictionary<int, MilitaryGroupOption>
            {
                [rules.Formation(best.FormationId, best.NavalFormationId, rules.SettlementArea(source).Position).Id] = best
            };
            var travel = rules.Travel(best.FormationId, best.NavalFormationId, best.Soldiers);
            var orderedTargets = targets.OrderBy(p => NavigationArrivalForecast.EarliestDays(travel,
                rules.SettlementArea(source).Position, p.Area)).ThenBy(p => p.Object.Id).Take(8).ToArray();
            var routes = queries.Transport(source.Id, orderedTargets.Select(p => p.Object.Id), best.FormationId,
                best.NavalFormationId, best.Soldiers, passage: MilitaryTransitPassage.Terrain);
            var routeOptions = routes.Routes;
            var beyond = routeOptions.Where(r => r.Status == TravelFieldStatus.BeyondHorizon).Select(r => r.TargetId).ToArray();
            if (beyond.Length > 0)
            {
                var extended = queries.Transport(source.Id, beyond, best.FormationId, best.NavalFormationId,
                    best.Soldiers, horizonDays: 256, passage: MilitaryTransitPassage.Terrain);
                routeOptions = routeOptions.Where(r => r.Status != TravelFieldStatus.BeyondHorizon).Concat(extended.Routes).ToImmutableArray();
            }
            foreach (var route in routeOptions.Where(r => r.Status == TravelFieldStatus.Reachable))
            {
                var objective = FirstObjective(route.Cells, queries.Observation, source.Id, route.TargetId);
                if (objective is null) continue;
                var target = objective.Target;
                int activeFormation = rules.Formation(best.FormationId, best.NavalFormationId, target.Area.Position).Id;
                if (!equipment.TryGetValue(activeFormation, out var battleGroup))
                {
                    if (batch.RemainingGroups(corps.OwnerId) == 0) { incomplete = true; continue; }
                    var adapted = evaluator.Search(force, people, [new(best.FormationId, best.NavalFormationId)], source.Id,
                        best.Soldiers, Math.Max(0, engine.Snapshot.Corps.Single(c => c.Id == corps.Id).Treasury.Money - MilitaryResourceLedger.ReservedMoney(batch.State, corps.Id, continuation?.Id)), target.Area.Position, MilitaryGroupRole.Siege,
                        queries.Observation.Units.Where(u => u.OwnerId != corps.OwnerId).SelectMany(u => u.Equipment), maximumEvaluations: 1);
                    groups += adapted.Evaluations; batch.SpendGroups(corps.OwnerId, adapted.Evaluations);
                    if (adapted.Best is null) continue;
                    var leader = world.Officers.Single(p => p.Id == adapted.Best.CommanderId);
                    uint limit = CommanderCapacity.Read(leader.Id == force.LeaderId,
                        definition.Content.Information.Ranks.FirstOrDefault(r => r.Id == force.RankId)?.CommandCapacity,
                        definition.Content.Information.Offices.FirstOrDefault(o => o.Id == leader.OfficeId)?.CommandCapacity);
                    equipment[activeFormation] = battleGroup = adapted.Best with { Soldiers = Math.Min(adapted.Best.Soldiers, limit) };
                }
                // Mobilize to the task, including zero-garrison defenses; this is a heuristic, not an eligibility gate.
                uint soldiers = (uint)Math.Min(battleGroup.Soldiers, Math.Max(1000L, 2L * target.Soldiers));
                if (soldiers == 0) continue;
                int days = (int)((route.CumulativeCosts[objective.CellIndex] + travel.DailyBudget - 1) / travel.DailyBudget);
                long nearby = queries.PotentialThreats(target.Object.Id, days).Candidates
                    .Sum(p => (long)p.Soldiers);
                long value = target.SettlementKind == SettlementKind.City ? 30000 : 12000;
                long score = value - days * (200L + soldiers / 100) - (target.Soldiers + nearby) / 2 - best.Money;
                if (score <= 0) { reason = MilitaryDecisionReason.NotWorthwhile; continue; }
                var id = Identity();
                var members = battleGroup.Members.OrderBy(p => p.OfficerId == battleGroup.CommanderId ? 0 : 1).ThenBy(p => p.OfficerId).ToArray();
                options.Add(new(new DeployUnitCommand(id.Id, corps.OwnerId, id.Sequence, source.Id,
                    members.Select(p => p.OfficerId).ToImmutableArray(), soldiers, best.FormationId,
                    new(MilitaryOrderKind.AttackSettlement, null, target.Object.Id))
                {
                    NavalFormationId = best.NavalFormationId,
                    Positions = members.ToImmutableDictionary(p => p.OfficerId, p => p.Position),
                    Tactics = members.ToImmutableDictionary(p => p.OfficerId, p => p.TacticId)
                }, source.Id, route.TargetId, target.Object.Id, score, ArrivalDays: days));
            }
            AssemblyOptions(source, commander, available, orderedTargets, queries, options);
        }
        // Alternatives reserve independently; only the selected public preparation can become a commitment.
        foreach (var option in options.OrderByDescending(p => p.Score).ThenBy(p => p.SourceId).ThenBy(p => p.Objective).Take(8))
        {
            reason = MilitaryDecisionReason.Unreachable;
            MilitaryBattlefield? battlefield = null;
            if (option.Command is DeployUnitCommand planned)
            {
                battlefield = batch.Battlefield(queries, rules, option.Target, option.ArrivalDays);
                long extraOpposition = Math.Max(0, battlefield.PossibleAt(option.ArrivalDays) - battlefield.PublishedAt(0));
                var source = engine.Snapshot.Settlements.Single(s => s.Id == planned.SettlementId);
                var leader = engine.GetAbilities(engine.Snapshot)[planned.OfficerIds[0]];
                int siege = Math.Max(1, queries.Observation.Forces.Find(new(option.Target, true))!.Durability /
                    Math.Max(1, CombatMath.DurabilityDamage(planned.Soldiers, leader.Leadership, rules.Formations[planned.FormationId].Combat?.AgainstWalls ?? 0, int.MaxValue)));
                var food = MilitaryResourceLedger.DeploymentFood(engine.Snapshot, corps.OwnerId, corps.Id, source,
                    planned.Soldiers, option.ArrivalDays, Math.Min(60, siege), engine.Companion);
                long failures = batch.State.Campaigns.Count(c => c.Stage == MilitaryCampaignStage.Cancelled && c.SourceId == option.SourceId && c.ObjectiveId == option.Objective);
                if (option.Score - extraOpposition / 2 - food.AdditionalFood / 20 - food.Shortfall / 5 - failures * 3000 <= 0)
                { reason = MilitaryDecisionReason.NotWorthwhile; continue; }
            }
            if (option.Command is DeployUnitCommand departure && queries.FromSettlement(departure.SettlementId,
                    new(option.Target, true), departure.FormationId, departure.NavalFormationId,
                    departure.Soldiers, verifyApproach: true).Status != ArrivalEstimateStatus.Known) continue;
            if (option.Command is TransportUnitCommand shipment)
            {
                var pair = rules.TransportFormations!;
                if (queries.FromSettlement(shipment.SettlementId, new(shipment.DestinationId, true), pair.LandFormationId,
                        pair.NavalFormationId, shipment.Soldiers, arrival: NavigationArrival.Region,
                        verifyApproach: true).Status != ArrivalEstimateStatus.Known) continue;
            }
            attempts++;
            CommandExecutionResult result;
            if (option.Command is DeployUnitCommand deploy)
            {
                var preparation = engine.PrepareDeployments(context);
                if (preparation.TryPrepare(deploy, out var prepared) != CommandRejection.None) { rejected++; continue; }
                result = engine.Execute(context, deploy, prepared!);
            }
            else result = engine.Execute(context, option.Command);
            if (!result.Submission.Accepted) { rejected++; continue; }
            submitted++; events.AddRange(result.Events);
            int? unitId = result.Events.OfType<UnitDeployedEvent>().Select(e => (int?)e.UnitId).SingleOrDefault();
            var position = unitId is int body ? engine.Snapshot.Units.Single(u => u.Id == body).Position : rules.SettlementArea(world.Settlements.Single(s => s.Id == option.SourceId)).Position;
            var campaign = new MilitaryCampaign(continuation?.Id ?? batch.State.NextId, corps.OwnerId, corps.Id,
                continuation?.SourceId ?? option.SourceId, option.Objective, option.Target, unitId,
                option.Stage, continuation?.Started ?? world.Clock, world.Clock, position,
                world.Settlements.Single(s => s.Id == option.Target).Durability)
            { DeliveryOfficerId = option.DeliveryOfficerId, PlannedArrivalDay = checked(world.Clock.DayNumber + option.ArrivalDays),
                ExpectedOpposition = battlefield?.PossibleAt(option.ArrivalDays) ?? 0,
                InitialSoldiers = unitId is int committed ? engine.Snapshot.Units.Single(u => u.Id == committed).Soldiers : 0,
                ReservedOfficers = option.Officers.IsDefault ? [] : option.Officers,
                Support = continuation?.Support ?? [], Reservations = continuation?.Reservations ?? [] };
            if (continuation is null) batch.State = batch.State with { NextId = checked(batch.State.NextId + 1),
                Campaigns = batch.State.Campaigns.Add(campaign) };
            else Replace(continuation, campaign);
            AutomaticResponseRunner.Run(engine, events);
            break;
        }
    }
    void AssemblyOptions(SettlementState source, OfficerState courier, uint available,
        MilitaryPresence[] targets, MilitaryTravelQueries queries, List<ActionOption> options)
    {
        var world = engine.Snapshot;
        var force = world.Forces.Single(f => f.Id == corps.OwnerId);
        foreach (var front in world.Settlements.Where(s => OwnSource(s) && s.Id != source.Id && targets.Any(t =>
                NavigationArrivalForecast.EarliestDays(rules.Travel(rules.Formations.Values.First(f => f.TravelMode == TravelMode.Land).Id, rules.BasicNavalFormationId, 1000), rules.SettlementArea(s).Position, t.Area) <
                NavigationArrivalForecast.EarliestDays(rules.Travel(rules.Formations.Values.First(f => f.TravelMode == TravelMode.Land).Id, rules.BasicNavalFormationId, 1000), rules.SettlementArea(source).Position, t.Area)))
            .OrderBy(s => s.Id).Take(3))
        {
            if (batch.RemainingGroups(corps.OwnerId) == 0 || queries.Budget.Exhausted) { incomplete = true; return; }
            var pending = world.Simulation.Tasks.Select(t => t.OfficerId).ToHashSet();
            var projected = world.Officers.Where(p => p.OwnerId == force.Id && p.SettlementId == front.Id && !pending.Contains(p.Id) && !MilitaryResourceLedger.OfficerReserved(batch.State, p.Id))
                .Append(courier with { SettlementId = front.Id, Acted = false, UnitId = null }).DistinctBy(p => p.Id)
                .Select(p => MilitaryGroupEvaluator.ObserveOwnOfficer(p, force, definition.Rules.Abilities,
                    world.Treasures, definition.Rules.Treasures, rules!)).ToArray();
            foreach (bool transport in new[] { false, true })
            {
                if (transport && (rules!.TransportFormations is null || available == 0)) continue;
                uint capacity = CommanderCapacity.Read(courier.Id == force.LeaderId,
                    definition.Content.Information.Ranks.FirstOrDefault(r => r.Id == force.RankId)?.CommandCapacity,
                    definition.Content.Information.Offices.FirstOrDefault(o => o.Id == courier.OfficeId)?.CommandCapacity);
                uint shipped = transport ? Math.Min(available, Math.Min(capacity, rules!.Formations[rules.TransportFormations!.LandFormationId].SoldierCapacity)) : 0;
                if (transport && rules.TransportFormations!.NavalFormationId is int naval)
                    shipped = Math.Min(shipped, rules.Formations[naval].SoldierCapacity);
                uint ready = (uint)Math.Min(front.SoldierCapacity, (long)front.Soldiers + shipped);
                if (ready == 0) continue;
                if (shipped > MilitaryResourceLedger.ReceivingSpace(world, front)) continue;
                var alternatives = rules!.Formations.Values.Where(f => f.TravelMode == TravelMode.Land &&
                    rules.Purpose(f.Id) == UnitPurpose.Combat && f.Id != rules.UnformedFormationId)
                    .OrderBy(f => f.Id).Select(f => new MilitaryFormationPair(f.Id, rules.BasicNavalFormationId));
                var group = new MilitaryGroupEvaluator(rules).Search(force, projected, alternatives, front.Id, ready,
                    corps.Treasury.Money, rules.SettlementArea(front).Position, MilitaryGroupRole.Siege,
                    maximumEvaluations: Math.Min(4, batch.RemainingGroups(corps.OwnerId)));
                groups += group.Evaluations;
                batch.SpendGroups(corps.OwnerId, group.Evaluations);
                if (group.Best is not { } best) continue;
                int deliveryDays;
                var id = Identity();
                GameCommand delivery;
                if (transport)
                {
                    var pair = rules.TransportFormations!;
                    var arrival = queries.Transport(source.Id, [front.Id], pair.LandFormationId,
                        pair.NavalFormationId, shipped).Routes[0];
                    if (arrival.Status != TravelFieldStatus.Reachable) continue;
                    deliveryDays = arrival.Days!.Value;
                    delivery = new TransportUnitCommand(id.Id, force.Id, id.Sequence, source.Id, [courier.Id], shipped, front.Id);
                }
                else
                {
                    var move = new MoveOfficersCommand(id.Id, force.Id, id.Sequence, [courier.Id], front.Id);
                    var mission = engine.Preview(context, move);
                    if (mission.Rejection != CommandRejection.None || mission.JourneyDays is not long duration) continue;
                    deliveryDays = (int)Math.Min(int.MaxValue, duration);
                    delivery = move;
                }
                // Delivery does not grant an extra order. Include arrival/action recovery and the next xun boundary.
                int readyDay = checked((deliveryDays / 10 + 1) * 10);
                var attacks = queries.Transport(front.Id, targets.Select(t => t.Object.Id), best.FormationId,
                    best.NavalFormationId, best.Soldiers, passage: MilitaryTransitPassage.Terrain);
                var attackTravel = rules.Travel(best.FormationId, best.NavalFormationId, best.Soldiers);
                foreach (var attack in attacks.Routes.Where(r => r.Status == TravelFieldStatus.Reachable))
                {
                    var objective = FirstObjective(attack.Cells, queries.Observation, front.Id, attack.TargetId);
                    if (objective is null) continue;
                    var target = objective.Target;
                    int days = (int)((attack.CumulativeCosts[objective.CellIndex] + attackTravel.DailyBudget - 1) / attackTravel.DailyBudget);
                    long value = target.SettlementKind == SettlementKind.City ? 30000 : 12000;
                    long score = value - (readyDay + days) * (200L + best.Soldiers / 100) - target.Soldiers / 2 - best.Money;
                    if (score > 0) options.Add(new(delivery, source.Id, attack.TargetId, front.Id, score,
                        MilitaryCampaignStage.Assembling, courier.Id, readyDay + days, best.Members.Select(m => m.OfficerId).ToImmutableArray()));
                }
            }
        }
    }
    void Continue(MilitaryCampaign campaign, UnitState unit, MilitaryTravelQueries queries)
    {
        var target = queries.Observation.Forces.Find(new(campaign.ObjectiveId, true));
        if (target is null || !Target(target)) { Replace(campaign, campaign with { Stage = MilitaryCampaignStage.Withdrawing }); return; }
        var travel = rules!.Travel(unit);
        if (!queries.Budget.BeginSourceField()) { incomplete = true; return; }
        var work = new NavigationWorkBudget(queries.Budget.RemainingExpanded, () => queries.Budget.Spend(1));
        var field = BattlefieldTravelField.Build(travel.Map, [unit.Position], [new(target.Object.Id, target.Area)],
            travel.Mode, 60L * travel.DailyBudget, work);
        if (!field.Complete) { incomplete = true; return; }
        if (field.Routes[0].Status != TravelFieldStatus.Reachable) return;
        var next = FirstObjective(field.Routes[0].Cells, queries.Observation, campaign.TargetId, campaign.ObjectiveId)?.Target;
        if (next is null || queries.FromUnit(unit.Id, next.Object, verifyApproach: true).Status != ArrivalEstimateStatus.Known) return;
        var id = Identity();
        if (Submit(new OrderUnitCommand(id.Id, corps.OwnerId, id.Sequence, unit.Id,
                new(MilitaryOrderKind.AttackSettlement, null, next.Object.Id))))
            Replace(campaign, campaign with { TargetId = next.Object.Id, Stage = MilitaryCampaignStage.Marching,
                ProgressAt = engine.Snapshot.Clock, TargetDurability = engine.Snapshot.Settlements.Single(s => s.Id == next.Object.Id).Durability });
    }
}
