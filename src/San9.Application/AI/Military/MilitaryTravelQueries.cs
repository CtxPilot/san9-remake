using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

public sealed record MilitaryOwnJourney(int PreparationDays, int MovementRemainder, WorldPosition? Waypoint);
public sealed record MilitaryTravelEstimate(ArrivalEstimateStatus Status, int? Days, int EarliestDays,
    ImmutableArray<MapCell> Cells, int Expanded, bool TargetIsMoving);
public sealed record MilitaryIncomingForce(int UnitId, int OwnerId, uint Soldiers, uint Wounded, MilitaryTravelEstimate Arrival);
public sealed record MilitaryPotentialThreat(int UnitId, int OwnerId, uint Soldiers, int EarliestDays, bool ExplicitTarget);
public sealed record MilitaryThreatQuery(ImmutableArray<MilitaryPotentialThreat> Candidates, MilitarySpatialQuery Spatial);
public sealed record MilitaryForceTimeline(int SettlementId, uint Garrison, uint Wounded, ImmutableArray<MilitaryIncomingForce> Incoming)
{
    public long SoldiersAt(int day) => Garrison + Incoming.Where(p => p.Arrival.Status == ArrivalEstimateStatus.Known && p.Arrival.Days <= day)
        .Sum(p => (long)p.Soldiers);
    public long ReceivingLoadAt(int day) => SoldiersAt(day) + Wounded + Incoming
        .Where(p => p.Arrival.Status == ArrivalEstimateStatus.Known && p.Arrival.Days <= day).Sum(p => (long)p.Wounded);
}

/// <summary>Shared batch transport, physical-neighbor and arrival queries. Enemy queries are built
/// exclusively from the authorized observation; a visible order is not a future movement promise.</summary>
public sealed class MilitaryTravelQueries
{
    private readonly MilitaryRulebook rules;
    private readonly Func<int, int?, bool> hostile;
    private readonly IReadOnlyDictionary<int, MilitaryOwnJourney> ownJourneys;
    public MilitaryObservation Observation { get; }
    private MilitaryTransportNetwork Network { get; }
    public MilitaryQueryBudget Budget { get; }
    private readonly ImmutableArray<UnitTravel> travelProfiles;
    private readonly int maximumRange;

    public MilitaryTravelQueries(MilitaryObservation observation, MilitaryRulebook rules, MilitaryTransportNetwork network,
        MilitaryQueryBudget budget, Func<int, int?, bool> hostile, IReadOnlyDictionary<int, MilitaryOwnJourney>? ownJourneys = null)
    {
        Observation = observation;
        this.rules = rules;
        Network = network;
        Budget = budget;
        this.hostile = hostile;
        this.ownJourneys = ownJourneys ?? ImmutableDictionary<int, MilitaryOwnJourney>.Empty;
        travelProfiles = observation.Forces.Objects.Where(p => p.Kind == MilitaryPresenceKind.Unit && p.FormationId is int id && rules.Purpose(id) == UnitPurpose.Combat)
            .Select(p => rules.Travel(p.FormationId!.Value, p.NavalFormationId, p.Soldiers)).Distinct().ToImmutableArray();
        maximumRange = rules.Formations.Values.Select(f => f.Attack is { } attack ? checked(attack.Range + attack.MaximumElevationBonus) : 1).DefaultIfEmpty(1).Max();
    }

    public MilitaryTransitQuery Transport(int sourceId, IEnumerable<int> targetIds, int formationId,
        int? navalFormationId, uint soldiers, int horizonDays = 60, MilitaryTransitPassage passage = MilitaryTransitPassage.Military)
    {
        lock (Network)
        {
            Network.Refresh(Observation.Forces);
            return Network.Query(sourceId, targetIds, formationId, navalFormationId, soldiers, Budget, horizonDays, hostile, passage: passage);
        }
    }

    /// <summary>Even a distant/unknown public order does not erase nearby physical threats. Units
    /// are listed once; this is a candidate set, never an assumption that all threats act together.</summary>
    public MilitaryThreatQuery PotentialThreats(int settlementId, int responseDays, int? defendingForceId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(responseDays);
        var target = Observation.Forces.Find(new(settlementId, true)) ?? throw new ArgumentException("Unknown defended settlement.");
        long distance = travelProfiles.Select(t => NavigationArrivalForecast.MaximumDistance(t, responseDays) +
            t.Shape.Width + (long)t.Shape.Height + 2L * maximumRange).DefaultIfEmpty(0).Max();
        var nearby = Observation.Forces.Nearby(target.Area, (int)Math.Min(int.MaxValue, distance));
        var candidates = nearby.Objects.Where(p => p.Kind == MilitaryPresenceKind.Unit && p.FormationId is not null &&
                p.OwnerId is int owner && owner != (defendingForceId ?? Observation.ViewerForceId) && rules.Purpose(p.FormationId.Value) == UnitPurpose.Combat &&
                (hostile(defendingForceId ?? Observation.ViewerForceId, owner) || Observation.FindUnit(p.Object.Id)?.Order is
                    { IsKnown: true, Value.Kind: MilitaryOrderKind.AttackSettlement, Value.SettlementId: int attacked } && attacked == settlementId))
            .Select(p =>
            {
                var travel = rules.Travel(p.FormationId!.Value, p.NavalFormationId, p.Soldiers);
                int earliest = NavigationArrivalForecast.EarliestDays(travel, p.Area.Position, target.Area, maximumRange: maximumRange);
                return new MilitaryPotentialThreat(p.Object.Id, p.OwnerId!.Value, p.Soldiers, earliest,
                    Observation.FindUnit(p.Object.Id)?.Order is { IsKnown: true, Value.Kind: MilitaryOrderKind.AttackSettlement, Value.SettlementId: int place } && place == settlementId);
            }).Where(p => p.EarliestDays <= responseDays).OrderBy(p => p.EarliestDays).ThenBy(p => p.UnitId).ToImmutableArray();
        return new(candidates, nearby);
    }

    public MilitaryTravelEstimate FromUnit(int unitId, MapObjectReference targetIdentity, UnitPolicies? policies = null,
        NavigationArrival arrival = NavigationArrival.Range, bool verifyApproach = false, bool newOrder = false)
    {
        var source = Observation.Forces.Find(new(unitId, false));
        var target = Observation.Forces.Find(targetIdentity);
        if (source?.FormationId is not int formation || target is null || source.Kind == MilitaryPresenceKind.Personnel)
            return new(ArrivalEstimateStatus.NotApplicable, null, 0, [], 0, !targetIdentity.IsSettlement);
        var travel = rules.Travel(formation, source.NavalFormationId, source.Soldiers);
        var own = source.OwnerId == Observation.ViewerForceId ? ownJourneys.GetValueOrDefault(unitId) : null;
        if (newOrder && own is not null) own = own with { PreparationDays = 0, Waypoint = null };
        return Estimate(source, target.Area, target.Object, travel, unitId, own, policies ?? new(), arrival, verifyApproach);
    }

    public MilitaryTravelEstimate FromSettlement(int settlementId, MapObjectReference targetIdentity, int formationId,
        int? navalFormationId, uint soldiers, int preparationDays = 0, UnitPolicies? policies = null,
        NavigationArrival arrival = NavigationArrival.Range, bool verifyApproach = false)
    {
        if (preparationDays is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(preparationDays));
        var source = Observation.Forces.Find(new(settlementId, true));
        var target = Observation.Forces.Find(targetIdentity);
        if (source?.OwnerId is not int owner || target is null || !rules.ValidFormationPair(formationId, navalFormationId))
            return new(ArrivalEstimateStatus.NotApplicable, null, 0, [], 0, !targetIdentity.IsSettlement);
        policies ??= new();
        var travel = rules.Travel(formationId, navalFormationId, soldiers);
        var start = MilitaryRouteQuery.DeploymentPosition(travel, source.Area, Occupancy(null, policies, targetIdentity, owner));
        if (start is null) return new(ArrivalEstimateStatus.Unreachable, null, 0, [], 0, !targetIdentity.IsSettlement);
        var prospective = source with { Area = new(start.Value, travel.Shape), FormationId = formationId,
            NavalFormationId = navalFormationId, Soldiers = soldiers };
        return Estimate(prospective, target.Area, target.Object, travel, null, new(preparationDays, 0, null), policies, arrival, verifyApproach);
    }

    public MilitaryTravelEstimate FromUnitToPosition(int unitId, WorldPosition position, UnitPolicies? policies = null, bool verifyApproach = false)
    {
        var source = Observation.Forces.Find(new(unitId, false));
        if (source?.FormationId is not int formation || source.Kind != MilitaryPresenceKind.Unit || position.X < 0 || position.Y < 0 ||
            position.X >= rules.Map.FineWidth || position.Y >= rules.Map.FineHeight)
            return new(ArrivalEstimateStatus.NotApplicable, null, 0, [], 0, false);
        var own = source.OwnerId == Observation.ViewerForceId ? ownJourneys.GetValueOrDefault(unitId) : null;
        if (own is not null) own = own with { PreparationDays = 0, Waypoint = null };
        return Estimate(source, new(position, new(1, 1)), null, rules.Travel(formation, source.NavalFormationId, source.Soldiers),
            unitId, own, policies ?? new(), NavigationArrival.Point, verifyApproach);
    }

    private MilitaryTravelEstimate Estimate(MilitaryPresence source, MapOccupancy target, MapObjectReference? targetIdentity, UnitTravel travel,
        int? moving, MilitaryOwnJourney? own, UnitPolicies policies, NavigationArrival arrival, bool verifyApproach)
    {
        int formation = source.FormationId!.Value;
        int preparation = own?.PreparationDays ?? 0, remainder = own?.MovementRemainder ?? 0;
        int Range(int id) => rules.Formations[id].Attack is { } attack ? checked(attack.Range + attack.MaximumElevationBonus) : 1;
        int reach = arrival == NavigationArrival.Range ? Math.Max(Range(formation), source.NavalFormationId is int ship ? Range(ship) : 1) : 0;
        int earliest = NavigationArrivalForecast.EarliestDays(travel, source.Area.Position,
            arrival == NavigationArrival.Region ? target.CoveringCells() : target, preparation,
            own is not null ? remainder : null, maximumRange: reach);
        if (travel.DailyBudget <= 0 || source.Condition == UnitCondition.Confused)
            return new(ArrivalEstimateStatus.Waiting, null, earliest, [], 0, targetIdentity is { IsSettlement: false });
        if (!Budget.BeginDetailedRoute() || Budget.RemainingExpanded == 0)
        {
            if (Budget.RemainingExpanded == 0) Budget.Spend(1);
            return new(ArrivalEstimateStatus.Incomplete, null, earliest, [], 0, targetIdentity is { IsSettlement: false });
        }
        var occupancy = Occupancy(moving, policies, targetIdentity, source.OwnerId!.Value);
        var work = new NavigationWorkBudget(Budget.RemainingExpanded, () => Budget.Spend(1));
        if (verifyApproach)
        {
            var prediction = NavigationArrivalForecast.Evaluate(new(travel, source.Area.Position, target, occupancy,
                arrival, preparation, remainder, RectangularReach: true, Range: Math.Max(1, reach), Waypoint: own?.Waypoint), work, maximumDays: 256);
            if (work.Exhausted) Budget.Spend(1);
            return new(prediction.Status, prediction.Days, earliest, [], work.Expanded, targetIdentity is { IsSettlement: false });
        }
        var cells = ImmutableArray.CreateBuilder<MapCell>();
        long cost = 0;
        var start = source.Area.Position;
        if (own?.Waypoint is { } waypoint)
        {
            var first = travel.Map.EstimateTravelCost(start, new(waypoint, new(1, 1)), travel.Shape, travel.Mode,
                occupancy, work, arrival: NavigationArrival.Point, includeCells: true, excluded: targetIdentity is { IsSettlement: true } ? target : null);
            if (first.Status != RouteReachability.Reachable) { cells.AddRange(first.Cells); return Result(first.Status, null); }
            cost = first.Cost;
            cells.AddRange(first.Cells);
            start = waypoint;
        }
        var route = travel.Map.EstimateTravelCost(start, target, travel.Shape, travel.Mode, occupancy, work,
            arrival: arrival, range: Math.Max(1, reach), rectangularReach: true, includeCells: true);
        cells.AddRange(route.Cells.Where(p => cells.Count == 0 || cells[^1] != p));
        if (route.Status != RouteReachability.Reachable) return Result(route.Status, null);
        int days = (int)Math.Min(int.MaxValue, preparation + (Math.Max(0, cost + route.Cost - remainder) + travel.DailyBudget - 1) / travel.DailyBudget);
        return Result(route.Status, days);

        MilitaryTravelEstimate Result(RouteReachability status, int? days)
        {
            if (work.Exhausted) Budget.Spend(1);
            return new(status switch { RouteReachability.Reachable => ArrivalEstimateStatus.Known,
                RouteReachability.Unreachable => ArrivalEstimateStatus.Unreachable, _ => ArrivalEstimateStatus.Incomplete },
                days, earliest, cells.ToImmutable(), work.Expanded, targetIdentity is { IsSettlement: false });
        }
    }

    public MilitaryForceTimeline Incoming(int settlementId)
    {
        var place = Observation.Forces.Find(new(settlementId, true)) ?? throw new ArgumentException("Unknown receiving settlement.");
        var target = new MapObjectReference(settlementId, true);
        var units = Observation.Units.ToDictionary(u => u.Id);
        var incoming = Observation.UnitsTargeting(target).Where(id => units[id].OwnerId == place.OwnerId &&
                units[id].Order.Value?.Kind is MilitaryOrderKind.Return or MilitaryOrderKind.Transport)
            .Select(id => (Id: id, Force: Observation.Forces.Find(new(id, false))))
            .Where(p => p.Force is not null).Select(p => new MilitaryIncomingForce(p.Id, p.Force!.OwnerId!.Value,
                p.Force.Soldiers, p.Force.Wounded, FromUnit(p.Id, target, arrival: NavigationArrival.Region))).ToImmutableArray();
        return new(settlementId, place.Soldiers, place.Wounded, incoming);
    }

    private ImmutableArray<MapOccupancy> Occupancy(int? moving, UnitPolicies policies, MapObjectReference? target, int owner)
    {
        var result = ImmutableArray.CreateBuilder<MapOccupancy>();
        int? targetOwner = target is not null ? Observation.Forces.Find(target)?.OwnerId : null;
        foreach (var p in Observation.Forces.Objects)
        {
            if (!p.Object.IsSettlement && (p.Object.Id == moving || p.Kind == MilitaryPresenceKind.Personnel)) continue;
            if (p.Object.IsSettlement)
            {
                bool work = p.FacilityTypeId is int type && rules.Facilities[type].IsWork;
                bool enemy = hostile(owner, p.OwnerId);
                result.Add(p.Area with { Object = p.Object, Hard = work || enemy,
                    Avoidance = work || enemy ? NavigationAvoidance.Facility : NavigationAvoidance.None });
            }
            else result.Add(p.Area with { Object = p.Object,
                Hard = policies.AttackApproachingEnemies && (hostile(owner, p.OwnerId) || p.OwnerId == targetOwner && p.OwnerId != owner),
                Avoidance = p.OwnerId == owner ? NavigationAvoidance.Friendly : NavigationAvoidance.Hostile });
        }
        return result.ToImmutable();
    }
}
