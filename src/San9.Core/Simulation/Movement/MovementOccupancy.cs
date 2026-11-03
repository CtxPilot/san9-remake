using System.Collections.Immutable;
using San9.Core.Domain;

namespace San9.Core.Simulation;

/// <summary>World-owned occupancy rectangles in stable order; passage is filtered per query.</summary>
internal sealed class MovementOccupancy
{
    internal MilitaryRulebook Rules { get; }
    private readonly SortedDictionary<(bool Unit, int Id), MapOccupancy> areas = [];

    private readonly Dictionary<MapCell, HashSet<int>> buildingCells = [];

    internal bool BlocksContact(WorldPosition position, int? target)
    {
        if (!buildingCells.TryGetValue(MapCoordinates.Cell(position), out var ids)) return false;
        foreach (int id in ids)
        {
            if (id == target) continue;
            var area = areas[(false, id)];
            if (area.Contains(position)) return true;
        }
        return false;
    }

    private IEnumerable<MapCell> BuildingCells(MapOccupancy area)
    {
        int scale = MapCoordinates.UnitsPerCell;
        long left = Math.Max(0, area.Left), top = Math.Max(0, area.Top);
        long right = Math.Min(Rules.Map.FineWidth, area.Right), bottom = Math.Min(Rules.Map.FineHeight, area.Bottom);
        if (left >= right || top >= bottom) yield break;
        for (int y = (int)(top / scale); y <= (bottom - 1) / scale; y++)
            for (int x = (int)(left / scale); x <= (right - 1) / scale; x++) yield return new(x, y);
    }

    internal MovementOccupancy(IWorldQuery world, MilitaryRulebook rules) : this(world.Settlements.Values, world.Units.Values, rules) { }
    private MovementOccupancy(IEnumerable<SettlementState> places, IEnumerable<UnitState> units, MilitaryRulebook rules)
    {
        Rules = rules;
        foreach (var place in places) Set(place);
        foreach (var unit in units) Set(unit);
    }
    internal static IReadOnlyList<MapOccupancy> Snapshot(WorldSnapshot world, MilitaryRulebook rules, int owner, UnitPolicies policies, bool includeUnits,
        int? movingUnit = null)
    {
        var query = new WorldQuery(world);
        return new MovementOccupancy(query.Settlements.Values, includeUnits ? world.Units : [], rules)
            .Query(query, owner, policies, movingUnit);
    }

    internal void Set(UnitState unit) => Set((true, unit.Id), unit.FormationId is int id
        ? new(unit.Position, Rules.UnitFootprint(unit)) : null);
    internal void Set(SettlementState place) => Set((false, place.Id), !place.IsDestroyed && Rules.TryPlace(place, out var p)
        ? Rules.BuildingArea(p) : null);
    internal void RemoveUnit(int id) => Set((true, id), null);
    private void Set((bool Unit, int Id) key, MapOccupancy? area)
    {
        if (area is { } next) area = next with { Object = new(key.Id, !key.Unit) };
        if (areas.TryGetValue(key, out var previous))
        {
            if (area == previous) return;
            if (!key.Unit)
                foreach (var cell in BuildingCells(previous))
                    if (buildingCells.TryGetValue(cell, out var ids) && ids.Remove(key.Id) && ids.Count == 0) buildingCells.Remove(cell);
        }
        if (area is not { } value) { areas.Remove(key); return; }
        areas[key] = value;
        if (!key.Unit)
            foreach (var cell in BuildingCells(value))
            {
                if (!buildingCells.TryGetValue(cell, out var ids)) buildingCells[cell] = ids = [];
                ids.Add(key.Id);
            }
    }

    internal IReadOnlyList<MapOccupancy> Query(IWorldQuery world, int owner, UnitPolicies policies, int? movingUnit = null, int? entry = null, UnitState? mover = null)
        => Query(world.Units, world.Settlements, policies, other => MilitaryDiplomacy.AutomaticEnemy(world, owner, other),
            other => (mover ?? (movingUnit is int id ? world.Units.GetValueOrDefault(id) : null)) is { } source
                ? MilitaryDiplomacy.ApproachEnemy(world, source, other) : MilitaryDiplomacy.AutomaticEnemy(world, owner, other),
            owner, movingUnit, entry);

    private IReadOnlyList<MapOccupancy> Query(IReadOnlyDictionary<int, UnitState> units, IReadOnlyDictionary<int, SettlementState> places,
        UnitPolicies policies, Func<int?, bool> enemyOf, Func<int, bool> unitEnemyOf, int owner, int? movingUnit = null, int? entry = null)
    {
        var result = new List<MapOccupancy>();
        foreach (var (key, area) in areas)
        {
            if (key.Unit)
            {
                if (key.Id == movingUnit) continue;
                var unit = units[key.Id];
                if (unit.IsEmbedded || unit.IsPersonnel) continue;
                bool enemy = unit.OwnerId != owner;
                result.Add(area with { Hard = unitEnemyOf(unit.OwnerId) && policies.AttackApproachingEnemies,
                    Avoidance = enemy ? NavigationAvoidance.Hostile : NavigationAvoidance.Friendly });
            }
            else
            {
                if (key.Id == entry) continue;
                var place = places[key.Id];
                if (Rules.Place(place).FacilityTypeId is int type && Rules.Facilities[type].IsWork) result.Add(area with { Avoidance = NavigationAvoidance.Facility });
                else if (place.OwnerId is not null && enemyOf(place.OwnerId))
                    result.Add(area with { Avoidance = NavigationAvoidance.Facility });
                else result.Add(area with { Hard = false, Avoidance = NavigationAvoidance.None });
            }
        }
        return result;
    }
}

public sealed record MilitaryRoutePlan(NavigationStatus Status, ImmutableArray<WorldPosition> Steps)
{
    public int? Days { get; init; }
}

/// <summary>Read-only intention routes share the coarse planner with daily movement.</summary>
public static class MilitaryRouteQuery
{
    /// <summary>Plans coarse route segments through the player waypoint without simulating future days.</summary>
    public static MilitaryRoutePlan Plan(WorldSnapshot snapshot, MilitaryRulebook rules, UnitState unit, int? movingUnit,
        int maximumDays = 4096, int maximumExpanded = 100000)
        => new MilitaryRouteQueryContext(snapshot, rules).Plan(unit, movingUnit, maximumDays, maximumExpanded);

    internal static MilitaryRoutePlan Plan(IWorldQuery world, MilitaryRulebook rules, UnitState unit, int? movingUnit,
        int maximumDays, int maximumExpanded)
    {
        var route = Estimate(world, rules, unit, movingUnit, new(maximumExpanded));
        return new(route.Status switch
        {
            RouteReachability.Reachable => NavigationStatus.Arrived,
            RouteReachability.Incomplete => NavigationStatus.Planning,
            _ => NavigationStatus.Unreachable
        }, route.Cells.Select(MapCoordinates.Center).ToImmutableArray());
    }

    /// <summary>One search per active route segment; avoidance affects routing, never adds elapsed days.</summary>
    internal static RouteCostEstimate Estimate(IWorldQuery world, MilitaryRulebook rules, UnitState unit, int? movingUnit,
        NavigationWorkBudget work)
    {
        var travel = rules.Travel(unit);
        var order = unit.EffectiveOrder!;
        var target = order.IsAttack ? MilitaryCombat.TargetArea(world, order, rules) :
            order.Construction is { } site ? rules.FacilityArea(site.Position, site.FacilityTypeId) :
            order.SettlementId is int home ? rules.SettlementArea(world.Settlements[home]) : new MapOccupancy(order.Position!.Value, new(1, 1));
        var occupancy = world.MovementOccupancy(rules, unit.OwnerId, unit.Policies, movingUnit, order.Construction?.RegionId, unit);
        var cells = ImmutableArray.CreateBuilder<MapCell>();
        long cost = 0;
        WorldPosition start = unit.Position;
        RouteCostEstimate Segment(MapOccupancy end, bool intoRange, MapOccupancy? excluded = null) => travel.Map.EstimateTravelCost(start, end,
            travel.Shape, travel.Mode, occupancy, work, intoRange: intoRange, includeCells: true, excluded: excluded);
        var waypoint = unit.ActiveWaypoint;
        if (waypoint is { } via)
        {
            var first = Segment(new(via, new(1, 1)), false, order.SettlementId is not null ? target : null);
            if (first.Status != RouteReachability.Reachable)
                return first;
            cells.AddRange(first.Cells);
            cost = first.Cost;
            start = via;
        }
        var last = Segment(target, order.IsAttack || order.SettlementId is not null || order.Construction is not null,
            waypoint is not null && order.SettlementId is not null ? target : null);
        if (last.Status != RouteReachability.Reachable)
            return last with { Cells = cells.ToImmutable() };
        foreach (var cell in last.Cells)
        {
            if (cells.Count == 0 || cells[^1] != cell) cells.Add(cell);
        }
        return new(RouteReachability.Reachable, cost + last.Cost) { Cells = cells.ToImmutable() };
    }

    public static IReadOnlyList<MapOccupancy> Occupancy(WorldSnapshot snapshot, MilitaryRulebook rules, int owner, UnitPolicies policies,
        bool includeUnits = true, int? movingUnit = null) => MovementOccupancy.Snapshot(snapshot, rules, owner, policies, includeUnits, movingUnit);

    public static WorldPosition? DeploymentPosition(UnitTravel travel, MapOccupancy place, IReadOnlyList<MapOccupancy> occupancy)
    {
        var area = new MapOccupancy(place.Position, travel.Map.ShapeAt(place.Position, travel.Shape));
        if (travel.Map.EndpointPassageCost(place.Position, travel.Mode) is not null &&
            !occupancy.Any(o => o.Hard && area.Overlaps(o))) return place.Position;
        // Water-only formations can require an actual shoreline entrance instead of a land facility's center.
        return travel.Entrances(place).Cast<WorldPosition?>().FirstOrDefault(p =>
            travel.Map.PassageCost(p!.Value, travel.Shape, travel.Mode, occupancy) is not null);
    }
}
