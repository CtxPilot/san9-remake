using System.Collections.Immutable;

namespace San9.Core.Domain;

public enum TransitReachability { Reachable, BeyondHorizon, Unreachable, Incomplete }
public sealed record TransitEndpoint(int Id, MapOccupancy Area);
public sealed record TransitRoute(int TargetId, TransitReachability Status, int? Days,
    RouteTerrainCost TerrainCost, ImmutableArray<MapCell> Cells);

/// <summary>A bounded terrain reference for many settlement endpoints. This is neither a
/// public movement forecast nor proof of clearance for a particular army.</summary>
public sealed class SettlementTransitField
{
    public ImmutableArray<TransitRoute> Routes { get; }
    public int Expanded { get; }
    public bool Complete { get; }

    private SettlementTransitField(ImmutableArray<TransitRoute> routes, int expanded, bool complete)
        => (Routes, Expanded, Complete) = (routes, expanded, complete);

    public static SettlementTransitField Build(UnitTravel travel, WorldPosition start,
        IEnumerable<TransitEndpoint> endpoints, int horizonDays, NavigationWorkBudget work)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(horizonDays);
        var targets = endpoints.OrderBy(t => t.Id).ToArray();
        if (targets.Any(t => t.Id < 0 || !t.Area.Footprint.IsValid) || targets.DistinctBy(t => t.Id).Count() != targets.Length)
            throw new ArgumentException("Expected unique physical endpoints.", nameof(endpoints));
        var map = travel.Map;
        if (start.X < 0 || start.Y < 0 || start.X >= map.FineWidth || start.Y >= map.FineHeight || travel.DailyBudget <= 0)
            throw new ArgumentException("Expected a valid departure and movement profile.");
        int count = checked(map.Width * map.Height);
        var costs = new int[count];
        map.CopyPlanningCosts(travel.Mode, costs);
        for (int i = 0; i < count; i++)
            if (costs[i] > 0) costs[i] = checked((int)map.Reweight(map.PassageComponents(Center(i), travel.Mode)));
        var goals = new Dictionary<int, List<int>>();
        for (int i = 0; i < targets.Length; i++)
        {
            var area = targets[i].Area;
            int left = (int)Math.Max(0, area.Left) / MapCoordinates.UnitsPerCell;
            int right = (int)Math.Min(map.FineWidth - 1, area.Right - 1) / MapCoordinates.UnitsPerCell;
            int top = (int)Math.Max(0, area.Top) / MapCoordinates.UnitsPerCell;
            int bottom = (int)Math.Min(map.FineHeight - 1, area.Bottom - 1) / MapCoordinates.UnitsPerCell;
            for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
            {
                int cell = y * map.Width + x;
                if (!goals.TryGetValue(cell, out var list)) goals[cell] = list = [];
                list.Add(i);
            }
        }
        var distance = new long[count];
        Array.Fill(distance, long.MaxValue);
        var previous = new int[count];
        Array.Fill(previous, -1);
        var terrain = new RouteTerrainCost[count];
        int origin = start.Y / MapCoordinates.UnitsPerCell * map.Width + start.X / MapCoordinates.UnitsPerCell;
        // A settlement's authored center may be blocked. Only the departure cell is relaxed;
        // target-only terrain cells can be reached, but never used as through passages.
        if (costs[origin] == 0) costs[origin] = map.EndpointPassageCost(start, travel.Mode) ?? 1;
        distance[origin] = 0;
        var queue = new PriorityQueue<int, (long Cost, int Cell)>();
        queue.Enqueue(origin, (0, origin));
        var selected = Enumerable.Repeat(-1, targets.Length).ToArray();
        long maximum = checked((long)horizonDays * travel.DailyBudget);
        int remaining = targets.Length, before = work.Expanded;
        bool beyond = false;
        if (PathSearchDiagnostics.Current is { } diagnostics) diagnostics.Queries++;
        Span<int> adjacent = stackalloc int[4];
        while (remaining > 0 && queue.TryDequeue(out int cell, out var priority))
        {
            if (priority.Cost != distance[cell]) continue;
            if (priority.Cost > maximum) { beyond = true; break; }
            if (!work.TryExpand()) break;
            if (PathSearchDiagnostics.Current is { } d) d.Expanded++;
            if (goals.TryGetValue(cell, out var at))
                foreach (int target in at)
                    if (selected[target] < 0) { selected[target] = cell; remaining--; }
            if (costs[cell] == 0) continue;
            int x = cell % map.Width, y = cell / map.Width, n = 0;
            if (y > 0) adjacent[n++] = cell - map.Width;
            if (x > 0) adjacent[n++] = cell - 1;
            if (y + 1 < map.Height) adjacent[n++] = cell + map.Width;
            if (x + 1 < map.Width) adjacent[n++] = cell + 1;
            for (int i = 0; i < n; i++)
            {
                int next = adjacent[i];
                int cost = costs[next];
                if (cost == 0 && (!goals.ContainsKey(next) ||
                    (cost = map.EndpointPassageCost(Center(next), travel.Mode) ?? 0) == 0)) continue;
                long value = checked(priority.Cost + MapCoordinates.UnitsPerCell * (long)cost);
                if (value >= distance[next]) continue;
                distance[next] = value;
                previous[next] = cell;
                terrain[next] = terrain[cell] + map.PassageComponents(Center(next), travel.Mode) * MapCoordinates.UnitsPerCell;
                queue.Enqueue(next, (value, next));
            }
        }
        var output = ImmutableArray.CreateBuilder<TransitRoute>(targets.Length);
        for (int i = 0; i < targets.Length; i++)
        {
            int cell = selected[i];
            if (cell < 0)
            {
                output.Add(new(targets[i].Id, work.Exhausted ? TransitReachability.Incomplete :
                    beyond ? TransitReachability.BeyondHorizon : TransitReachability.Unreachable, null, default, []));
                continue;
            }
            var cells = new List<MapCell>();
            for (int cursor = cell; cursor >= 0; cursor = previous[cursor]) cells.Add(new(cursor % map.Width, cursor / map.Width));
            cells.Reverse();
            int days = (int)Math.Min(int.MaxValue, (distance[cell] + travel.DailyBudget - 1) / travel.DailyBudget);
            output.Add(new(targets[i].Id, TransitReachability.Reachable, days, terrain[cell], cells.ToImmutableArray()));
        }
        int expanded = work.Expanded - before;
        if (PathSearchDiagnostics.Current is { } final) final.MaximumExpanded = Math.Max(final.MaximumExpanded, expanded);
        return new(output.MoveToImmutable(), expanded, !work.Exhausted);

        WorldPosition Center(int cell) => MapCoordinates.Center(new(cell % map.Width, cell / map.Width));
    }
}
