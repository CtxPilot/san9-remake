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
        var id = Identity();
        return new(id.Id, force.Id, id.Sequence, source.Id, members.Select(m => m.OfficerId).ToImmutableArray(),
            Math.Min(best.Soldiers, capacity), best.FormationId, target.IsSettlement
                ? new(MilitaryOrderKind.AttackSettlement, null, target.Id) : new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = target.Id })
        {
            NavalFormationId = best.NavalFormationId,
            Positions = members.ToImmutableDictionary(m => m.OfficerId, m => m.Position),
            Tactics = members.ToImmutableDictionary(m => m.OfficerId, m => m.TacticId),
            Policies = new(true, false, false, false)
        };
    }

    private int? ExecuteDeparture(GameCommand command)
    {
        attempts++;
        CommandExecutionResult result;
        if (command is DeployUnitCommand deployment)
        {
            if (engine.PrepareDeployments(context).TryPrepare(deployment, out var prepared) != CommandRejection.None)
            { rejected++; return null; }
            result = engine.Execute(context, deployment, prepared!);
        }
        else result = engine.Execute(context, command);
        if (!result.Submission.Accepted) { rejected++; return null; }
        submitted++; events.AddRange(result.Events);
        AutomaticResponseRunner.Run(engine, events);
        return result.Events.OfType<UnitDeployedEvent>().Select(e => (int?)e.UnitId).SingleOrDefault();
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
        foreach (var campaign in batch.State.Campaigns.Where(c => c.ForceId == corps.OwnerId && c.Purpose == MilitaryCampaignPurpose.Conquest &&
            c.Stage is MilitaryCampaignStage.Marching or MilitaryCampaignStage.Preparing).OrderBy(c => c.Started.DayNumber).ThenBy(c => c.Id).ToArray())
        {
            var target = queries.Observation.Forces.Find(new(campaign.TargetId, true));
            var primary = campaign.UnitId is int lead ? world.Units.FirstOrDefault(u => u.Id == lead) : null;
            if (target is null || primary is null || target.OwnerId == corps.OwnerId) continue;
            foreach (var guard in campaign.Support.Where(s => s.Role == MilitaryCommitmentRole.Field))
            {
                var escort = engine.Snapshot.Units.FirstOrDefault(u => u.Id == guard.UnitId);
                if (escort is null || !Controls(escort) || !escort.CanReceiveOrders || escort.CommandedThisXun ||
                    escort.Order?.UnitId is not int prior || queries.Observation.Forces.Find(new(prior, false)) is not null) continue;
                var identity = Identity();
                Submit(new OrderUnitCommand(identity.Id, corps.OwnerId, identity.Sequence, escort.Id,
                    new(MilitaryOrderKind.AttackSettlement, null, campaign.TargetId)));
            }
            int window = (int)Math.Clamp(campaign.PlannedArrivalDay - world.Clock.DayNumber, 1, 60);
            var battlefield = batch.Battlefield(queries, rules, target.Object.Id, window);
            var members = campaign.UnitIds.Select(id => world.Units.FirstOrDefault(u => u.Id == id)).OfType<UnitState>()
                .Where(u => u.OwnerId == corps.OwnerId && u.FormationId is not null && u.Order?.Kind is not (MilitaryOrderKind.Return or MilitaryOrderKind.Transport)).ToArray();
            long live = members.Where(u => NavigationArrivalForecast.EarliestDays(rules.Travel(u), u.Position, target.Area,
                u.DepartureDaysRemaining, u.MovementRemainder) <= window).Sum(u => (long)u.Soldiers);
            var field = battlefield.Contributions.FirstOrDefault(c => c.Kind == MilitaryContributionKind.FieldResponse && (c.ArrivalDay ?? c.EarliestDay) <= window);
            bool needsGuard = field is not null && !campaign.Support.Any(s => s.Role == MilitaryCommitmentRole.Field && world.Units.Any(u => u.Id == s.UnitId));
            long demand = Math.Max(0, 2 * battlefield.PossibleAt(window) - live);
            if (demand == 0 && !needsGuard)
            {
                if (!campaign.Reservations.IsEmpty) Replace(campaign, campaign with { Reservations = [] });
                continue;
            }
            foreach (var source in Sources(queries, campaign.Id).Take(4))
            {
                var goal = needsGuard ? field!.Source : target.Object;
                var role = needsGuard ? MilitaryGroupRole.Field : MilitaryGroupRole.Siege;
                var command = Organize(source, goal, (uint)Math.Min(Spare(source, queries, campaign.Id), Math.Max(1000, demand)), role, queries, campaign.Id);
                if (command is null) continue;
                var arrival = queries.FromSettlement(source.Id, goal, command.FormationId, command.NavalFormationId, command.Soldiers, verifyApproach: true);
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
                if (ExecuteDeparture(command) is not int unit) continue;
                AddSupport(campaign, unit, source.Id, needsGuard ? MilitaryCommitmentRole.Field : MilitaryCommitmentRole.Siege);
                return true;
            }
        }
        return false;
    }
}
