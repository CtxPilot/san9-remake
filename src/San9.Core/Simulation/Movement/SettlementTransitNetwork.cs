using System.Collections.Immutable;
using San9.Core.Domain;

namespace San9.Core.Simulation;

/// <summary>Static facts along the reference centerline. Travel cost starts after leaving the
/// source occupancy and stops before entering the target occupancy; it is not a clearance proof.</summary>
public sealed record TransitRouteSummary(WorldPosition Start, WorldPosition End, RouteTerrainCost TerrainCost,
    ImmutableArray<int> TerrainIds, ImmutableArray<int> RegionIds, ImmutableArray<int> ViaSettlementIds)
{
    public int EstimateDays(UnitTravel travel)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(travel.DailyBudget);
        long cost = travel.Map.Reweight(TerrainCost);
        return checked((int)Math.Min(int.MaxValue, (cost + travel.DailyBudget - 1) / travel.DailyBudget));
    }
}

public sealed record SettlementTransitRoute(int SourceId, TransitRoute Reference, TransitRouteSummary Summary);

/// <summary>Immutable session-wide reference graph, completed before the session is exposed.
/// Every physical source uses the same model-owned movement conditions, independently of armies,
/// officers, ownership or money. Incoming edges retain their original direction and cost.</summary>
public sealed class SettlementTransitNetwork
{
    public const int ReferenceDailyBudget = 50;
    // Candidate coverage only; callers own the actual travel limits of each work.
    public const int ReferenceHorizonDays = 60;
    public static Footprint ReferenceLandShape { get; } = new(5, 5);
    public static Footprint ReferenceWaterShape { get; } = new(4, 4);

    private readonly MilitaryRulebook rules;
    private readonly ImmutableDictionary<int, ImmutableArray<SettlementTransitRoute>> outgoing;
    private readonly ImmutableDictionary<int, ImmutableArray<SettlementTransitRoute>> incoming;
    private readonly ImmutableDictionary<(int Source, int Target), SettlementTransitRoute> connections;
    public ImmutableArray<TransitEndpoint> Nodes { get; }
    public int GeometryRevision { get; }
    public int BuildSearches { get; }
    public long BuildExpanded { get; }

    private SettlementTransitNetwork(MilitaryRulebook rules, ImmutableArray<TransitEndpoint> nodes, int revision)
    {
        this.rules = rules;
        Nodes = nodes;
        GeometryRevision = revision;
        var occupiedCells = nodes.SelectMany(n =>
        {
            var area = n.Area.CoveringCells();
            var cells = new List<(MapCell Cell, int Id)>();
            for (int y = (int)area.Top / MapCoordinates.UnitsPerCell; y < area.Bottom / MapCoordinates.UnitsPerCell; y++)
            for (int x = (int)area.Left / MapCoordinates.UnitsPerCell; x < area.Right / MapCoordinates.UnitsPerCell; x++)
                cells.Add((new(x, y), n.Id));
            return cells;
        }).GroupBy(p => p.Cell).ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToArray());
        var nodesById = nodes.ToDictionary(n => n.Id);
        // Model-owned reference geometry and equal land/water speed. These formations are only
        // topology descriptors, never selectable formations or content identities.
        var land = new FormationRule(0, ReferenceDailyBudget, 0, 1, 1, ReferenceLandShape, TravelMode.Land);
        var water = new FormationRule(1, ReferenceDailyBudget, 0, 1, 1, ReferenceWaterShape, TravelMode.Water);
        var travel = new UnitTravel(rules.Map.ForTravel(land, water, ReferenceDailyBudget),
            ReferenceLandShape, TravelMode.Land, ReferenceDailyBudget);
        var rows = ImmutableDictionary.CreateBuilder<int, ImmutableArray<SettlementTransitRoute>>();
        foreach (var source in nodes)
        {
            // A finite map and time bound define construction work. Do not publish a partly
            // built graph merely because an optional AI forecast budget was exhausted.
            var field = SettlementTransitField.Build(travel, source.Area.Position,
                nodes.Where(n => n.Id != source.Id), ReferenceHorizonDays, new(int.MaxValue));
            BuildSearches++;
            BuildExpanded += field.Expanded;
            rows.Add(source.Id, field.Routes.Where(r => r.Status == TransitReachability.Reachable).Select(route =>
                new SettlementTransitRoute(source.Id, route, Summarize(source, route))).ToImmutableArray());
        }
        outgoing = rows.ToImmutable();
        var edges = outgoing.Values.SelectMany(r => r).ToArray();
        incoming = edges.GroupBy(r => r.Reference.TargetId).ToImmutableDictionary(g => g.Key,
            g => g.OrderBy(r => r.SourceId).ToImmutableArray());
        connections = edges.ToImmutableDictionary(r => (r.SourceId, r.Reference.TargetId));

        TransitRouteSummary Summarize(TransitEndpoint source, TransitRoute route)
        {
            var target = nodesById[route.TargetId];
            var terrainIds = new HashSet<int>();
            var regions = new List<int>();
            var regionSet = new HashSet<int>();
            var via = new List<int>();
            var viaSet = new HashSet<int>();
            void Region(int? region)
            {
                if (region is int id && regionSet.Add(id)) regions.Add(id);
            }
            Region(rules.RegionAt(source.Area.Position) ?? source.Id);
            var cursor = source.Area.Position;
            var start = cursor;
            var end = cursor;
            var cost = new RouteTerrainCost();
            bool departed = false, arrived = target.Area.Contains(cursor);
            Visit(cursor);
            foreach (var destination in route.Cells.Select(MapCoordinates.Center).Append(target.Area.Position))
            {
                if (arrived) break;
                while (cursor != destination)
                {
                    var previous = cursor;
                    cursor = cursor.X != destination.X ? cursor with { X = cursor.X + Math.Sign(destination.X - cursor.X) }
                        : cursor with { Y = cursor.Y + Math.Sign(destination.Y - cursor.Y) };
                    Visit(cursor);
                    if (!departed && !source.Area.Contains(cursor))
                    {
                        departed = true;
                        start = previous;
                        end = previous;
                    }
                    if (target.Area.Contains(cursor)) { arrived = true; break; }
                    if (!departed) continue;
                    if (travel.Map.TerrainPassageCost(cursor, travel.Mode) is not null)
                        cost += travel.Map.PassageComponents(cursor, travel.Mode);
                    end = cursor;
                }
            }
            Region(rules.RegionAt(target.Area.Position) ?? target.Id);
            return new(start, end, cost, terrainIds.Order().ToImmutableArray(), regions.ToImmutableArray(), via.ToImmutableArray());

            void Visit(WorldPosition point)
            {
                var cell = MapCoordinates.Cell(point);
                terrainIds.Add(rules.Map.TerrainIdAt(cell));
                Region(rules.RegionAt(point));
                foreach (int id in occupiedCells.GetValueOrDefault(cell, []))
                    if (id != source.Id && id != target.Id && nodesById[id].Area.Contains(point) && viaSet.Add(id)) via.Add(id);
            }
        }
    }

    public static SettlementTransitNetwork Build(MilitaryRulebook rules, IEnumerable<SettlementState> settlements) =>
        new(rules, Geometry(rules, settlements), 1);

    public bool UsesRules(MilitaryRulebook rulebook) => ReferenceEquals(rules, rulebook);

    /// <summary>Reuse the whole graph when physical geometry is unchanged. Rebuild into a new
    /// immutable graph when necessary, leaving the previous session/transaction untouched.</summary>
    public SettlementTransitNetwork Update(IEnumerable<SettlementState> settlements)
    {
        var geometry = Geometry(rules, settlements);
        return Nodes.SequenceEqual(geometry) ? this : new(rules, geometry, checked(GeometryRevision + 1));
    }

    public ImmutableArray<SettlementTransitRoute> From(int sourceId) => outgoing.TryGetValue(sourceId, out var routes)
        ? routes : throw new ArgumentException("Unknown physical source.", nameof(sourceId));

    public ImmutableArray<SettlementTransitRoute> To(int targetId) => Nodes.Any(n => n.Id == targetId)
        ? incoming.GetValueOrDefault(targetId, []) : throw new ArgumentException("Unknown physical target.", nameof(targetId));

    public SettlementTransitRoute? Find(int sourceId, int targetId) => connections.GetValueOrDefault((sourceId, targetId));

    private static ImmutableArray<TransitEndpoint> Geometry(MilitaryRulebook rules, IEnumerable<SettlementState> settlements) =>
        settlements.Where(s => !s.IsDormant && !s.IsDestroyed && rules.TryPlace(s, out _))
            .OrderBy(s => s.Id).Select(s => new TransitEndpoint(s.Id, rules.SettlementArea(s))).ToImmutableArray();
}
