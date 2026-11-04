using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

internal sealed partial class MilitaryCorpsPlanner
{
    private bool Controls(UnitState unit) => unit.OwnerId == corps.OwnerId && (unit.CorpsId == corps.Id || unit.CorpsId is null && corps.IsRoot);
    private bool Friendly(SettlementState place) => place.OwnerId == corps.OwnerId && !place.IsDormant && !place.IsDestroyed &&
        place.CanHostPersons && place.Durability > 0 && rules.TryPlace(place, out _);
    private IEnumerable<SettlementState> Sources(MilitaryTravelQueries queries, long? campaign = null) => engine.Snapshot.Settlements
        .Where(s => OwnSource(s) && s.Soldiers > 0).OrderBy(s => queries.PotentialThreats(s.Id, 10).Candidates.Sum(p => (long)p.Soldiers)).ThenBy(s => s.Id);
    private uint Spare(SettlementState source, MilitaryTravelQueries queries, long? except = null) => (uint)Math.Max(0,
        source.Soldiers - queries.PotentialThreats(source.Id, 10).Candidates.Sum(p => (long)p.Soldiers) -
        MilitaryResourceLedger.ReservedSoldiers(batch.State, source.Id, except));
    private DeployUnitCommand? Organize(SettlementState source, MapObjectReference target, uint wanted, MilitaryGroupRole role,
        MilitaryTravelQueries queries, long? campaign = null)
    {
        if (wanted == 0 || batch.RemainingGroups(corps.OwnerId) == 0) return null;
        var world = engine.Snapshot;
        var force = world.Forces.Single(f => f.Id == corps.OwnerId);
        var busy = world.Simulation.Tasks.Select(t => t.OfficerId).ToHashSet();
        var people = world.Officers.Where(p => p.OwnerId == corps.OwnerId && p.SettlementId == source.Id && !busy.Contains(p.Id) &&
            !MilitaryResourceLedger.OfficerReserved(batch.State, p.Id, campaign)).Select(p => MilitaryGroupEvaluator.ObserveOwnOfficer(p,
                force, definition.Rules.Abilities, world.Treasures, definition.Rules.Treasures, rules)).ToArray();
        var position = queries.Observation.Forces.Find(target)?.Area.Position;
        if (position is null) return null;
        var alternatives = rules.Formations.Values.Where(f => f.TravelMode == TravelMode.Land && rules.Purpose(f.Id) == UnitPurpose.Combat &&
                f.Id != rules.UnformedFormationId).OrderBy(f => f.Id).Select(f => new MilitaryFormationPair(f.Id, rules.BasicNavalFormationId));
        long money = Math.Max(0, world.Corps.Single(c => c.Id == corps.Id).Treasury.Money - MilitaryResourceLedger.ReservedMoney(batch.State, corps.Id, campaign));
        var group = new MilitaryGroupEvaluator(rules).Search(force, people, alternatives, source.Id, wanted, money,
            position.Value, role, queries.Observation.Units.Where(u => u.OwnerId != corps.OwnerId).SelectMany(u => u.Equipment),
            maximumEvaluations: Math.Min(4, batch.RemainingGroups(corps.OwnerId)));
        groups += group.Evaluations; batch.SpendGroups(corps.OwnerId, group.Evaluations);
        if (group.Best is not { } best) return null;
        var leader = world.Officers.Single(p => p.Id == best.CommanderId);
        uint capacity = CommanderCapacity.Read(leader.Id == force.LeaderId,
            definition.Content.Information.Ranks.FirstOrDefault(r => r.Id == force.RankId)?.CommandCapacity,
            definition.Content.Information.Offices.FirstOrDefault(o => o.Id == leader.OfficeId)?.CommandCapacity);
        var members = best.Members.OrderBy(m => m.OfficerId == best.CommanderId ? 0 : 1).ThenBy(m => m.OfficerId).ToArray();
        uint soldiers = DepartureLimit(source, Math.Min(best.Soldiers, capacity), members.Select(m => m.OfficerId), queries);
        if (soldiers == 0) { reason = MilitaryDecisionReason.SourceExposed; return null; }
        var id = Identity();
        return new(id.Id, force.Id, id.Sequence, source.Id, members.Select(m => m.OfficerId).ToImmutableArray(),
            soldiers, best.FormationId, target.IsSettlement
                ? new(MilitaryOrderKind.AttackSettlement, null, target.Id) : new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = target.Id })
        {
            NavalFormationId = best.NavalFormationId,
            Positions = members.ToImmutableDictionary(m => m.OfficerId, m => m.Position),
            Tactics = members.ToImmutableDictionary(m => m.OfficerId, m => m.TacticId),
            Policies = CombatPolicies, ConfirmAttack = true
        };
    }

    private int? ExecuteDeparture(GameCommand command, long? campaignId = null)
    {
        attempts++;
        CommandExecutionResult result;
        if (command is DeployUnitCommand deployment)
        {
            if (engine.PrepareDeployments(context).TryPrepare(deployment, out var prepared) != CommandRejection.None)
            { rejected++; Release(); return null; }
            result = engine.Execute(context, deployment, prepared!);
        }
        else result = engine.Execute(context, command);
        if (!result.Submission.Accepted) { rejected++; Release(); return null; }
        submitted++; events.AddRange(result.Events);
        AutomaticResponseRunner.Run(engine, events);
        return result.Events.OfType<UnitDeployedEvent>().Select(e => (int?)e.UnitId).SingleOrDefault();

        void Release()
        {
            int? source = command switch { DeployUnitCommand d => d.SettlementId, TransportUnitCommand t => t.SettlementId, _ => null };
            if (campaignId is long campaign && source is int place) ReleaseReservation(campaign, place);
        }
    }
    private void AddSupport(MilitaryCampaign campaign, int unitId, int source, MilitaryCommitmentRole role, int? destination = null)
    {
        var unit = engine.Snapshot.Units.Single(u => u.Id == unitId);
        var actual = batch.State.Campaigns.Single(c => c.Id == campaign.Id);
        Replace(actual, actual with { Support = actual.Support.Add(new(unitId, source, corps.Id, role, unit.Soldiers, destination)),
            InitialSoldiers = (uint)Math.Min(uint.MaxValue, (long)actual.InitialSoldiers + unit.Soldiers),
            Reservations = actual.Reservations.Where(r => r.CorpsId != corps.Id || r.SourceId != source).ToImmutableArray() });
    }

    private bool SupportCampaign(MilitaryTravelQueries queries)
    {
        var world = engine.Snapshot;
        int? requested = MilitaryCampaignPolicy.RequestedObjective(world, corps);
        foreach (var campaign in batch.State.Campaigns.Where(c => c.ForceId == corps.OwnerId && c.Purpose == MilitaryCampaignPurpose.Conquest &&
            (c.Stage is MilitaryCampaignStage.Marching or MilitaryCampaignStage.Preparing) &&
            MilitaryCampaignPolicy.AllowsObjective(world, corps, c.ObjectiveId))
            .OrderBy(c => c.ObjectiveId == requested ? 0 : 1)
            .ThenBy(c => c.Started.DayNumber).ThenBy(c => c.Id).ToArray())
        {
            var target = queries.Observation.Forces.Find(new(campaign.TargetId, true));
            var primary = campaign.UnitId is int lead ? world.Units.FirstOrDefault(u => u.Id == lead) : null;
            if (target is null || primary is null || target.OwnerId == corps.OwnerId) continue;
            var commitment = CommittedForces(campaign, queries);
            int window = commitment.Window;
            foreach (var guard in campaign.Support.Where(s => s.Role == MilitaryCommitmentRole.Field))
            {
                var escort = engine.Snapshot.Units.FirstOrDefault(u => u.Id == guard.UnitId);
                if (escort is null || !Controls(escort) || !escort.CanReceiveOrders || escort.CommandedThisXun) continue;
                bool fieldTargetAlive = escort.Order?.Kind == MilitaryOrderKind.AttackUnit && escort.Order.UnitId is int prior &&
                    queries.Observation.Forces.Find(new(prior, false)) is { } enemy && CanRespond(enemy, target.Area, window);
                if (fieldTargetAlive || escort.Order is { Kind: MilitaryOrderKind.AttackSettlement } existing && existing.SettlementId == campaign.TargetId) continue;
                if (queries.FromUnit(escort.Id, target.Object, policies: escort.Policies, verifyApproach: true, newOrder: true).Status != ArrivalEstimateStatus.Known) continue;
                var identity = Identity();
                if (Submit(new OrderUnitCommand(identity.Id, corps.OwnerId, identity.Sequence, escort.Id,
                    new(MilitaryOrderKind.AttackSettlement, null, campaign.TargetId)) { ConfirmAttack = true })) return true;
            }
            var battlefield = batch.Battlefield(queries, rules, target.Object.Id, window);
            long live = commitment.TimelySoldiers;
            var field = battlefield.Contributions.FirstOrDefault(c => c.Kind == MilitaryContributionKind.FieldResponse && (c.ArrivalDay ?? c.EarliestDay) <= window);
            bool needsGuard = field is not null && !commitment.HasGuard;
            long demand = Math.Max(0, 2 * battlefield.PossibleAt(window) - live);
            if (demand == 0 && !needsGuard)
            {
                if (!campaign.Reservations.IsEmpty)
                { var actual = batch.State.Campaigns.Single(c => c.Id == campaign.Id); Replace(actual, actual with { Reservations = [] }); }
                continue;
            }
            if (commitment.Uncertain) { incomplete = true; continue; }
            foreach (var pending in campaign.Reservations.Where(r => r.CorpsId == corps.Id && r.NotBeforeDay <= world.Clock.DayNumber))
                if (!world.Settlements.Any(s => s.Id == pending.SourceId && OwnSource(s))) ReleaseReservation(campaign.Id, pending.SourceId);
            foreach (var source in Sources(queries, campaign.Id).Take(4))
            {
                var goal = needsGuard ? field!.Source : target.Object;
                var role = needsGuard ? MilitaryGroupRole.Field : MilitaryGroupRole.Siege;
                var command = Organize(source, goal, (uint)Math.Min(Spare(source, queries, campaign.Id), Math.Max(1000, demand)), role, queries, campaign.Id);
                if (command is null) continue;
                var arrival = queries.FromSettlement(source.Id, goal, command.FormationId, command.NavalFormationId, command.Soldiers,
                    policies: command.Policies, verifyApproach: true);
                if (arrival.Status != ArrivalEstimateStatus.Known) continue;
                var food = MilitaryResourceLedger.DeploymentFood(world, corps.OwnerId, corps.Id, source, command.Soldiers,
                    arrival.Days!.Value, 10, engine.Companion);
                if (food.Shortfall > (target.SettlementKind == SettlementKind.City ? 30000 : 12000)) continue;
                int wait = Math.Max(0, window - arrival.Days.Value);
                if (wait > 9)
                {
                    var reservation = new MilitaryReservation(source.Id, corps.Id, command.OfficerIds, command.Soldiers,
                        rules.Formations[command.FormationId].DeploymentCost + (command.NavalFormationId is int ship ? rules.Formations[ship].DeploymentCost : 0),
                        world.Clock.DayNumber + wait - 9, world.Clock.DayNumber + window + 10,
                        needsGuard ? MilitaryCommitmentRole.Field : MilitaryCommitmentRole.Siege);
                    var actual = batch.State.Campaigns.Single(c => c.Id == campaign.Id);
                    Replace(actual, actual with { Reservations = actual.Reservations.Where(r => r.CorpsId != corps.Id || r.SourceId != source.Id)
                        .Append(reservation).ToImmutableArray() });
                    reason = MilitaryDecisionReason.AwaitingAssembly;
                    return true;
                }
                if (arrival.Days.Value > window + 10 && !needsGuard) continue;
                command = command with { DepartureDelayDays = wait };
                if (ExecuteDeparture(command, campaign.Id) is not int unit) continue;
                AddSupport(campaign, unit, source.Id, needsGuard ? MilitaryCommitmentRole.Field : MilitaryCommitmentRole.Siege);
                return true;
            }
        }
        return false;
    }
}
