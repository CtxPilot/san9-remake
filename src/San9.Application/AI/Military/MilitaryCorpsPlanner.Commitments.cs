using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

internal sealed partial class MilitaryCorpsPlanner
{
    private sealed record CommittedForce(UnitState Unit, MilitaryCommitmentRole Role, int? Days, bool Uncertain);
    private sealed record CampaignForces(int Window, ImmutableArray<CommittedForce> Participants)
    {
        internal long TimelySoldiers => Participants.Where(p => p.Days <= Window).Sum(p => (long)p.Unit.Soldiers);
        internal bool Uncertain => Participants.Any(p => p.Uncertain);
        internal bool HasGuard => Participants.Any(p => p.Role == MilitaryCommitmentRole.Field && p.Days <= Window);
    }

    private CampaignForces CommittedForces(MilitaryCampaign campaign, MilitaryTravelQueries queries, int? deadline = null)
    {
        var world = engine.Snapshot;
        var target = queries.Observation.Forces.Find(new(campaign.TargetId, true));
        if (target is null) return new(1, []);
        var commitments = campaign.Support.Select(p => (p.UnitId, p.Role)).ToList();
        if (campaign.UnitId is int primary) commitments.Insert(0, (primary, campaign.Purpose == MilitaryCampaignPurpose.Relief
            ? world.Units.FirstOrDefault(u => u.Id == primary)?.Order?.Kind == MilitaryOrderKind.Transport ? MilitaryCommitmentRole.Transport : MilitaryCommitmentRole.Field
            : MilitaryCommitmentRole.Siege));
        var result = ImmutableArray.CreateBuilder<CommittedForce>();
        int window = deadline ?? (int)Math.Clamp(campaign.PlannedArrivalDay - world.Clock.DayNumber, 1, 60);
        foreach (var (unitId, role) in commitments)
        {
            var unit = world.Units.FirstOrDefault(u => u.Id == unitId && u.OwnerId == corps.OwnerId && !u.IsEmbedded && !u.IsPersonnel);
            if (unit?.FormationId is not int formation) continue;
            MapObjectReference? goal = unit.EffectiveOrder switch
            {
                { Kind: MilitaryOrderKind.AttackSettlement, SettlementId: int place } when place == campaign.TargetId => target.Object,
                { Kind: MilitaryOrderKind.Transport, SettlementId: int place } when campaign.Purpose == MilitaryCampaignPurpose.Relief && place == campaign.ObjectiveId => target.Object,
                { Kind: MilitaryOrderKind.AttackUnit, UnitId: int enemy } when (role == MilitaryCommitmentRole.Field || unitId == campaign.UnitId) &&
                    queries.Observation.Forces.Find(new(enemy, false)) is { } body &&
                    (campaign.Purpose == MilitaryCampaignPurpose.Conquest ? body.OwnerId == target.OwnerId && CanRespond(body, target.Area, window) :
                        queries.PotentialThreats(campaign.ObjectiveId, 10).Candidates.Any(t => t.UnitId == enemy)) => new(enemy, false),
                _ => null
            };
            if (goal is null) { result.Add(new(unit, role, null, false)); continue; }
            var area = queries.Observation.Forces.Find(goal)!.Area;
            var travel = rules.Travel(unit);
            int range = rules.Formation(formation, unit.NavalFormationId, area.Position).Attack?.Range ?? 1;
            int earliest = NavigationArrivalForecast.EarliestDays(travel, unit.Position, area, unit.DepartureDaysRemaining, unit.MovementRemainder, range);
            // A late support has no early contribution, regardless of its remaining troop count.
            if (unit.Condition == UnitCondition.Confused) earliest = (int)Math.Min(int.MaxValue, (long)earliest + unit.ConditionDaysRemaining);
            if (deadline is not null && earliest > window || unitId != campaign.UnitId && earliest > window) { result.Add(new(unit, role, null, false)); continue; }
            var arrival = batch.CommittedArrival(queries, unit, goal);
            if (unitId == campaign.UnitId && deadline is null && arrival.Status == ArrivalEstimateStatus.Known)
                window = Math.Clamp(arrival.Days!.Value, 1, 60);
            bool uncertain = arrival.Status is ArrivalEstimateStatus.Incomplete or ArrivalEstimateStatus.Waiting;
            result.Add(new(unit, goal.IsSettlement ? role : MilitaryCommitmentRole.Field, arrival.Status == ArrivalEstimateStatus.Known ? arrival.Days : null, uncertain));
        }
        return new(window, result.ToImmutable());
    }

    private bool CanRespond(MilitaryPresence unit, MapOccupancy battlefield, int days)
    {
        if (unit.FormationId is not int formation || rules.Purpose(formation) != UnitPurpose.Combat) return false;
        var attack = rules.Formation(formation, unit.NavalFormationId, battlefield.Position).Attack;
        return NavigationArrivalForecast.EarliestDays(rules.Travel(formation, unit.NavalFormationId, unit.Soldiers), unit.Area.Position,
            battlefield, maximumRange: attack is null ? 1 : attack.Range + attack.MaximumElevationBonus) <= days;
    }

    private void ReleaseReservation(long campaignId, int source)
    {
        var current = batch.State.Campaigns.Single(c => c.Id == campaignId);
        Replace(current, current with { Reservations = current.Reservations.Where(r => r.SourceId != source || r.CorpsId != corps.Id).ToImmutableArray() });
    }
}
