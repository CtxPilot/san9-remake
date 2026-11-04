using System.Collections.Immutable;
using System.Globalization;
using San9.Core.Domain;

namespace San9.Application.AI.Military;

public sealed record MilitaryTransitRoute(int SourceId, int TargetId, TravelFieldStatus Status, int? Days,
    long Cost, RouteTerrainCost TerrainCost, ImmutableArray<MapCell> Cells)
{
    public bool RequiresNavigationCheck => true;
}
public sealed record MilitaryTransitQuery(ImmutableArray<MilitaryTransitRoute> Routes, int Expanded,
    int CellsInitialized, bool CacheHit);

/// <summary>Bounded, requested-source transport fields. Administrative city affiliation and the
/// number/identity of authored slots never restrict a military connection.</summary>
public sealed class MilitaryTransportNetwork
{
    private readonly MilitaryRulebook rules;
    private readonly int capacity;
    private ImmutableArray<(int Id, MapOccupancy Area)> geometry = [];
    private ImmutableArray<MilitaryPresence> nodes = [];
    private readonly Dictionary<Key, BattlefieldTravelField> cache = [];
    private readonly Queue<Key> insertionOrder = [];
    private readonly record struct Key(int Source, UnitTravel Travel, string Targets, int Horizon, string Blocked);
    public int CachedFields => cache.Count;
    public int GeometryRevision { get; private set; }

    public MilitaryTransportNetwork(MilitaryRulebook rules, int maximumCachedFields = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumCachedFields, 1);
        this.rules = rules;
        capacity = maximumCachedFields;
    }

    /// <summary>All physical geometry is compared: removing an off-route obstacle can open a shortcut.</summary>
    public void Refresh(MilitaryForceIndex forces)
    {
        nodes = forces.Objects.Where(p => p.Kind == MilitaryPresenceKind.Settlement).ToImmutableArray();
        var current = nodes
            .Select(p => (p.Object.Id, p.Area)).ToImmutableArray();
        if (geometry.SequenceEqual(current)) return;
        geometry = current;
        cache.Clear();
        insertionOrder.Clear();
        GeometryRevision++;
    }

    public MilitaryTransitQuery Query(int sourceId, IEnumerable<int> targetIds, int formationId,
        int? navalFormationId, uint soldiers, MilitaryQueryBudget budget, int horizonDays = 60,
        Func<int, int?, bool>? isHostile = null, int? marchingForceId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(horizonDays);
        var targets = targetIds.Distinct().Order().ToArray();
        var places = geometry.ToDictionary(p => p.Id, p => p.Area);
        if (!places.TryGetValue(sourceId, out var source) || targets.Any(t => !places.ContainsKey(t)))
            throw new ArgumentException("Transport endpoints must be active physical settlements.");
        if (!rules.ValidFormationPair(formationId, navalFormationId)) throw new ArgumentException("Invalid movement conditions.");
        var travel = rules.Travel(formationId, navalFormationId, soldiers);
        int owner = marchingForceId ?? nodes.Single(p => p.Object.Id == sourceId).OwnerId ?? -1;
        var obstacles = nodes.Where(p => p.Object.Id != sourceId && (p.FacilityTypeId is int type && rules.Facilities[type].IsWork ||
            (isHostile?.Invoke(owner, p.OwnerId) ?? (p.OwnerId != owner)))).ToArray();
        var key = new Key(sourceId, travel, string.Join(',', targets.Select(t => t.ToString(CultureInfo.InvariantCulture))), horizonDays,
            string.Join(',', obstacles.Select(p => p.Object.Id.ToString(CultureInfo.InvariantCulture))));
        MilitaryTransitQuery Incomplete() => new(targets.Select(t => new MilitaryTransitRoute(sourceId, t,
            TravelFieldStatus.Incomplete, null, 0, default, [])).ToImmutableArray(), 0, 0, false);
        if (targets.Length == 0) return new([], 0, 0, false);
        if (!budget.BeginSourceField()) return Incomplete();
        if (cache.TryGetValue(key, out var cached))
        {
            int before = budget.Expanded;
            if (!budget.Spend(cached.Expanded)) return Incomplete() with { CacheHit = true, Expanded = budget.Expanded - before };
            return Convert(cached, true, 0);
        }
        if (budget.RemainingExpanded == 0) { budget.Spend(1); return Incomplete(); }
        var barriers = obstacles.Select(p => p.Area).ToArray();
        // A center that public deployment can use is preferable; otherwise use all legal shoreline entrances.
        var origin = San9.Core.Simulation.MilitaryRouteQuery.DeploymentPosition(travel, source, barriers);
        IEnumerable<WorldPosition> starts = origin is { } position ? [position] : [];
        var work = new NavigationWorkBudget(budget.RemainingExpanded, () => budget.Spend(1));
        var field = BattlefieldTravelField.Build(travel.Map, starts,
            targets.Select(t => new TravelFieldEndpoint(t, places[t])), travel.Mode,
            checked((long)horizonDays * travel.DailyBudget), work, barriers);
        if (!field.Complete)
        {
            budget.Spend(1);
            return Incomplete() with { Expanded = field.Expanded, CellsInitialized = field.CellsInitialized };
        }
        // Only this explicit source/target request is retained. Interrupted fields never poison the cache.
        if (cache.Count >= capacity) cache.Remove(insertionOrder.Dequeue());
        cache.Add(key, field);
        insertionOrder.Enqueue(key);
        return Convert(field, false, field.CellsInitialized);

        MilitaryTransitQuery Convert(BattlefieldTravelField field, bool hit, int initialized) => new(field.Routes.Select(r =>
            new MilitaryTransitRoute(sourceId, r.Id, r.Status, r.Status == TravelFieldStatus.Reachable
                ? (int)Math.Min(int.MaxValue, (r.Cost + travel.DailyBudget - 1) / travel.DailyBudget) : null,
                r.Cost, r.TerrainCost, r.Cells)).ToImmutableArray(), field.Expanded, initialized, hit);
    }
}
