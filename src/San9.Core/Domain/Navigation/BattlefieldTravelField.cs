using System.Collections.Immutable;

namespace San9.Core.Domain;

public enum TravelFieldStatus { Reachable, Unreachable, BeyondHorizon, Incomplete }
public sealed record TravelFieldEndpoint(int Id, MapOccupancy Area);
public sealed record TravelFieldRoute(int Id, TravelFieldStatus Status, long Cost, RouteTerrainCost TerrainCost,
    ImmutableArray<MapCell> Cells)
{
    public ImmutableArray<long> CumulativeCosts { get; init; } = [];
}

/// <summary>A single bounded terrain search for many fixed destinations. This is a coarse transport
/// estimate, not a promise of fine footprint clearance, public command eligibility or daily arrival.</summary>
public sealed class BattlefieldTravelField
{
    public ImmutableArray<TravelFieldRoute> Routes { get; }
    public int Expanded { get; }
    public int CellsInitialized { get; }
    public bool Complete { get; }

    private BattlefieldTravelField(ImmutableArray<TravelFieldRoute> routes, int expanded, int initialized, bool complete)
    { Routes = routes; Expanded = expanded; CellsInitialized = initialized; Complete = complete; }

    public static BattlefieldTravelField Build(BattlefieldMap map, IEnumerable<WorldPosition> starts,
        IEnumerable<TravelFieldEndpoint> destinations, TravelMode mode, long maximumCost, NavigationWorkBudget work,
        IReadOnlyList<MapOccupancy>? barriers = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCost);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var targets = destinations.OrderBy(t => t.Id).ToArray();
        if (targets.Any(t => t.Id < 0 || !t.Area.Footprint.IsValid) || targets.Select(t => t.Id).Distinct().Count() != targets.Length)
            throw new ArgumentException("Expected unique destinations with valid footprints.", nameof(destinations));
        int count = checked(map.Width * map.Height);
        var costs = new int[count];
        map.CopyPlanningCosts(mode, costs);
        // The ordinary strategic planner normalizes speed when choosing a corridor. A transport
        // time field must optimize the actual land/water ratio, not reweight one representative path.
        for (int cell = 0; cell < count; cell++)
            if (costs[cell] > 0) costs[cell] = checked((int)map.Reweight(map.PassageComponents(Center(cell), mode)));
        var blocked = new bool[count];
        var terminal = new bool[count];
        foreach (var area in barriers ?? [])
            Paint(area, cell => blocked[cell] = true);
        var goals = new Dictionary<int, List<int>>();
        for (int i = 0; i < targets.Length; i++)
        {
            int targetIndex = i;
            Paint(targets[i].Area, cell =>
            {
                if (!goals.TryGetValue(cell, out var list)) goals.Add(cell, list = []);
                list.Add(targetIndex);
                // Buildings are legal endpoints even when their authored center blocks through movement.
                if (costs[cell] == 0)
                {
                    costs[cell] = map.EndpointPassageCost(Center(cell), mode) ?? 0;
                    terminal[cell] = true;
                }
            });
        }
        var distance = new long[count];
        Array.Fill(distance, long.MaxValue);
        var previous = new int[count];
        Array.Fill(previous, -1);
        var components = new RouteTerrainCost[count];
        var queue = new PriorityQueue<int, (long Cost, int Cell)>();
        foreach (var start in starts.Distinct().OrderBy(p => p.Y).ThenBy(p => p.X))
        {
            if (!Inside(start)) continue;
            int cell = start.Y / 5 * map.Width + start.X / 5;
            if (costs[cell] == 0) costs[cell] = map.EndpointPassageCost(start, mode) ?? 0;
            if (costs[cell] == 0 || blocked[cell] || distance[cell] == 0) continue;
            distance[cell] = 0;
            queue.Enqueue(cell, (0, cell));
        }
        var selected = Enumerable.Repeat(-1, targets.Length).ToArray();
        int expandedBefore = work.Expanded, remaining = targets.Length;
        bool horizonReached = false;
        if (PathSearchDiagnostics.Current is { } diagnostics) diagnostics.Queries++;
        Span<int> neighbors = stackalloc int[4];
        while (remaining > 0 && queue.TryDequeue(out int at, out var priority))
        {
            if (priority.Cost != distance[at]) continue;
            if (priority.Cost > maximumCost) { horizonReached = true; break; }
            if (!work.TryExpand()) break;
            if (PathSearchDiagnostics.Current is { } d) d.Expanded++;
            if (goals.TryGetValue(at, out var atGoals))
                foreach (int target in atGoals)
                    if (selected[target] == -1) { selected[target] = at; remaining--; }
            if (blocked[at] || terminal[at] && distance[at] != 0) continue;
            int x = at % map.Width, y = at / map.Width, n = 0;
            if (y > 0) neighbors[n++] = at - map.Width;
            if (x > 0) neighbors[n++] = at - 1;
            if (y + 1 < map.Height) neighbors[n++] = at + map.Width;
            if (x + 1 < map.Width) neighbors[n++] = at + 1;
            for (int i = 0; i < n; i++)
            {
                int next = neighbors[i];
                if (costs[next] == 0 || blocked[next] && !goals.ContainsKey(next)) continue;
                long cost = checked(priority.Cost + 5L * costs[next]);
                if (cost >= distance[next]) continue;
                distance[next] = cost;
                previous[next] = at;
                components[next] = components[at] + map.PassageComponents(Center(next), mode) * 5;
                queue.Enqueue(next, (cost, next));
            }
        }
        int expanded = work.Expanded - expandedBefore;
        if (PathSearchDiagnostics.Current is { } finalDiagnostics)
            finalDiagnostics.MaximumExpanded = Math.Max(finalDiagnostics.MaximumExpanded, expanded);
        var routes = ImmutableArray.CreateBuilder<TravelFieldRoute>(targets.Length);
        for (int i = 0; i < targets.Length; i++)
        {
            int at = selected[i];
            if (at < 0)
            {
                routes.Add(new(targets[i].Id, work.Exhausted ? TravelFieldStatus.Incomplete :
                    horizonReached ? TravelFieldStatus.BeyondHorizon : TravelFieldStatus.Unreachable, 0, default, []));
                continue;
            }
            var cells = new List<MapCell>();
            for (int cell = at; cell >= 0; cell = previous[cell]) cells.Add(new(cell % map.Width, cell / map.Width));
            cells.Reverse();
            routes.Add(new(targets[i].Id, TravelFieldStatus.Reachable, distance[at], components[at], cells.ToImmutableArray())
                { CumulativeCosts = cells.Select(cell => distance[cell.Y * map.Width + cell.X]).ToImmutableArray() });
        }
        return new(routes.MoveToImmutable(), expanded, count, !work.Exhausted);

        bool Inside(WorldPosition p) => p.X >= 0 && p.Y >= 0 && p.X < map.FineWidth && p.Y < map.FineHeight;
        WorldPosition Center(int cell) => MapCoordinates.Center(new(cell % map.Width, cell / map.Width));
        void Paint(MapOccupancy area, Action<int> apply)
        {
            int left = (int)Math.Max(0, area.Left) / 5, right = (int)Math.Min(map.FineWidth - 1, area.Right - 1) / 5;
            int top = (int)Math.Max(0, area.Top) / 5, bottom = (int)Math.Min(map.FineHeight - 1, area.Bottom - 1) / 5;
            if (area.Right <= 0 || area.Bottom <= 0 || area.Left >= map.FineWidth || area.Top >= map.FineHeight) return;
            for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++) apply(y * map.Width + x);
        }
    }
}
