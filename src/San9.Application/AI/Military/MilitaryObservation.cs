using System.Collections.Immutable;
using System.Text.Json.Serialization;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

/// <summary>Unknown is distinct from a known empty value.</summary>
public readonly record struct MilitaryKnowledge<T>(bool IsKnown, T? Value)
{
    public static MilitaryKnowledge<T> Unknown => new(false, default);
    public static MilitaryKnowledge<T> Known(T? value) => new(true, value);
}

public sealed record MilitaryPublicOrder(MilitaryOrderKind Kind, int? SettlementId = null, int? UnitId = null,
    int? RegionId = null, WorldPosition? Position = null, int? FacilityTypeId = null)
{
    public bool IsRetreat { get; init; }
}
public sealed record MilitaryEquipment(int OfficerId, MilitaryKnowledge<int?> Tactic);
public sealed record MilitaryUnitObservation(int Id, int OwnerId, MilitaryKnowledge<MilitaryPublicOrder> Order,
    ImmutableArray<MilitaryEquipment> Equipment);
public sealed record MilitaryIntelligenceState([property: JsonRequired] GameClock PublishedAt,
    [property: JsonRequired] long Revision, [property: JsonRequired] ImmutableArray<MilitaryPresence> Physical,
    [property: JsonRequired] ImmutableArray<MilitaryUnitObservation> Units)
{
    public void Validate(WorldSnapshot world, MilitaryRulebook rules)
    {
        if (Revision < 0 || Revision > world.Revision || PublishedAt != world.Clock || Physical.IsDefault || Units.IsDefault ||
            Physical.Any(p => p is null || p.Object is null || p.Object.Id < 0 || !p.Area.Footprint.IsValid ||
                !Enum.IsDefined(p.Kind) || p.Object.IsSettlement != (p.Kind == MilitaryPresenceKind.Settlement) ||
                p.OwnerId is < 0 || p.CorpsId is < 0 || !Enum.IsDefined(p.Condition) || p.Morale is < 0 or > 100 ||
                p.FacilityTypeId is int facility && !rules.Facilities.ContainsKey(facility) ||
                p.Kind == MilitaryPresenceKind.Settlement && (p.SettlementKind is null || !Enum.IsDefined(p.SettlementKind.Value)) ||
                p.Area.Position.X < 0 || p.Area.Position.Y < 0 ||
                p.Area.Position.X >= rules.Map.FineWidth || p.Area.Position.Y >= rules.Map.FineHeight ||
                p.FormationId is int formation && !rules.Formations.ContainsKey(formation) ||
                p.NavalFormationId is int naval && !rules.Formations.ContainsKey(naval)) ||
            Physical.Select(p => p.Object).Distinct().Count() != Physical.Length)
            throw new ArgumentException("Invalid military intelligence publication.");
        var physicalUnits = Physical.Where(p => !p.Object.IsSettlement).ToDictionary(p => p.Object.Id);
        if (Units.Any(u => u is null || u.Id < 0 || u.OwnerId < 0 || !u.Order.IsKnown || u.Equipment.IsDefault ||
                !physicalUnits.TryGetValue(u.Id, out var body) || body.OwnerId != u.OwnerId ||
                u.Order.Value is { } order && (!Enum.IsDefined(order.Kind) || order.SettlementId is < 0 ||
                    order.UnitId is < 0 || order.RegionId is < 0 || order.FacilityTypeId is < 0) ||
                u.Equipment.Any(e => e is null || e.OfficerId < 0 || !e.Tactic.IsKnown ||
                    e.Tactic.Value is int tactic && !rules.Tactics.ContainsKey(tactic)) ||
                u.Equipment.Select(e => e.OfficerId).Distinct().Count() != u.Equipment.Length) ||
            Units.Select(u => u.Id).Distinct().Count() != Units.Length)
            throw new ArgumentException("Invalid military intelligence publication.");
    }
}
public sealed class MilitaryObservation
{
    public GameClock PublishedAt { get; }
    public long PublicationRevision { get; }
    public int ViewerForceId { get; }
    public MilitaryForceIndex Forces { get; }
    public ImmutableArray<MilitaryUnitObservation> Units { get; }
    private readonly ImmutableDictionary<MapObjectReference, ImmutableArray<int>> unitsByTarget;
    private readonly ImmutableDictionary<int, MilitaryUnitObservation> unitsById;

    internal MilitaryObservation(GameClock publishedAt, long revision, int viewer, MilitaryForceIndex forces,
        ImmutableArray<MilitaryUnitObservation> units)
    {
        PublishedAt = publishedAt;
        PublicationRevision = revision;
        ViewerForceId = viewer;
        Forces = forces;
        Units = units;
        unitsById = units.ToImmutableDictionary(u => u.Id);
        unitsByTarget = units.Where(u => u.Order.IsKnown &&
                (u.Order.Value?.SettlementId is not null || u.Order.Value?.UnitId is not null))
            .GroupBy(u => u.Order.Value!.SettlementId is int place ? new MapObjectReference(place, true)
                : new(u.Order.Value.UnitId!.Value, false))
            .ToImmutableDictionary(g => g.Key, g => g.Select(u => u.Id).ToImmutableArray());
    }

    /// <summary>Known targets are evidence, not commitments or guaranteed future arrivals.</summary>
    public ImmutableArray<int> UnitsTargeting(MapObjectReference target) => unitsByTarget.GetValueOrDefault(target, []);
    public MilitaryUnitObservation? FindUnit(int unitId) => unitsById.GetValueOrDefault(unitId);
}

/// <summary>Minimal enemy publication, independent of the live world and control processing order.
/// Only State is authoritative; per-viewer observations and index updates are disposable caches.</summary>
public sealed class MilitaryIntelligencePublication
{
    private readonly ImmutableArray<MilitaryPresence> physical;
    private readonly ImmutableArray<MilitaryUnitObservation> units;
    public GameClock PublishedAt { get; }
    public long Revision { get; }
    public MilitaryIntelligenceState State { get; }
    private MilitaryForceIndex? publishedForces;
    private readonly Dictionary<int, ViewerCache> viewers = [];
    private sealed record ViewerCache(ImmutableArray<MilitaryPresence> Friendly,
        ImmutableArray<MilitaryUnitObservation> OwnUnits, bool Orders, bool Tactics, MilitaryObservation Observation);

    private MilitaryIntelligencePublication(WorldSnapshot world, MilitaryRulebook rules)
        : this(new(world.Clock, world.Revision, MilitaryForceIndex.ProjectPhysical(world, rules).ToImmutableArray(), ProjectUnits(world, rules))) { }

    public MilitaryIntelligencePublication(MilitaryIntelligenceState state)
    {
        State = state;
        PublishedAt = state.PublishedAt;
        Revision = state.Revision;
        physical = state.Physical;
        units = state.Units;
    }

    public static MilitaryIntelligencePublication Publish(WorldSnapshot world, MilitaryRulebook rules) => new(world, rules);

    /// <summary>Enemy facts stay published; friendly resources and equipment reflect actual submissions.
    /// The result contains no retained raw world or unit references that could bypass permissions.</summary>
    public MilitaryObservation Observe(WorldSnapshot current, MilitaryRulebook rules, int viewerForceId)
    {
        // Published state is immutable. Background branches may share these disposable caches.
        lock (viewers) return ObserveCore(current, rules, viewerForceId);
    }

    private MilitaryObservation ObserveCore(WorldSnapshot current, MilitaryRulebook rules, int viewerForceId)
    {
        if (current.Clock != PublishedAt || current.Revision < Revision)
            throw new InvalidOperationException("A new order boundary requires a new intelligence publication.");
        var friendly = MilitaryForceIndex.ProjectPhysical(current, rules, viewerForceId).ToImmutableArray();
        var ownUnits = ProjectUnits(current, rules, viewerForceId);
        var old = viewers.GetValueOrDefault(viewerForceId);
        bool sameFriendly = old is not null && old.Friendly.SequenceEqual(friendly);
        bool sameOwn = old is not null && old.OwnUnits.Length == ownUnits.Length && old.OwnUnits.Zip(ownUnits).All(p =>
            p.First.Id == p.Second.Id && p.First.OwnerId == p.Second.OwnerId && p.First.Order == p.Second.Order && p.First.Equipment.SequenceEqual(p.Second.Equipment));
        if (sameFriendly && sameOwn && old!.Orders == current.Simulation.OtherForceOrdersVisible && old.Tactics == current.Simulation.OtherForceTacticsVisible)
            return old.Observation;
        // Every changed overlay derives from the publication, so a discarded branch cannot
        // erase a foreign object from subsequent views of the same order boundary.
        var forces = sameFriendly ? old!.Observation.Forces
            : (publishedForces ??= MilitaryForceIndex.Create(physical)).ReplaceForce(viewerForceId, friendly);
        var ownIds = ownUnits.Select(u => u.Id).ToHashSet();
        var observed = units.Where(u => u.OwnerId != viewerForceId && !ownIds.Contains(u.Id)).Select(u => u with
        {
            Order = current.Simulation.OtherForceOrdersVisible ? u.Order : MilitaryKnowledge<MilitaryPublicOrder>.Unknown,
            Equipment = current.Simulation.OtherForceTacticsVisible ? u.Equipment :
                u.Equipment.Select(e => e with { Tactic = MilitaryKnowledge<int?>.Unknown }).ToImmutableArray()
        }).Concat(ownUnits).OrderBy(u => u.Id).ToImmutableArray();
        var result = new MilitaryObservation(PublishedAt, Revision, viewerForceId, forces, observed);
        viewers[viewerForceId] = new(friendly, ownUnits, current.Simulation.OtherForceOrdersVisible, current.Simulation.OtherForceTacticsVisible, result);
        return result;
    }

    private static ImmutableArray<MilitaryUnitObservation> ProjectUnits(WorldSnapshot world, MilitaryRulebook rules, int? owner = null)
    {
        var equipment = world.Officers.Where(p => p.IsServing && p.UnitId is not null && (owner is null || p.OwnerId == owner))
            .GroupBy(p => p.UnitId!.Value).ToDictionary(g => g.Key, g => g.OrderBy(p => p.Id)
                .Select(p => new MilitaryEquipment(p.Id, MilitaryKnowledge<int?>.Known(p.EquippedTacticId))).ToImmutableArray());
        return world.Units.Where(u => !u.IsEmbedded && (owner is null || u.OwnerId == owner)).OrderBy(u => u.Id)
            .Select(u => new MilitaryUnitObservation(u.Id, u.OwnerId, MilitaryKnowledge<MilitaryPublicOrder>.Known(PublicOrder(u, rules)),
                equipment.GetValueOrDefault(u.Id, []))).ToImmutableArray();
    }

    private static MilitaryPublicOrder? PublicOrder(UnitState unit, MilitaryRulebook rules)
    {
        // A temporary engagement, contact target, waypoint, delay and policy are not published orders.
        var order = unit.HasForcedReturn ? new MilitaryOrder(MilitaryOrderKind.Return, null, unit.ServicePlaceId) : unit.Order;
        if (order is null) return null;
        if (order.Kind == MilitaryOrderKind.Move && order.Position is { } position)
            return rules.RegionAt(position) is int region ? new(order.Kind, RegionId: region) : new(order.Kind, Position: position);
        if (order.Construction is { } construction)
            return new(order.Kind, RegionId: construction.RegionId, FacilityTypeId: construction.FacilityTypeId);
        return new(order.Kind, SettlementId: order.SettlementId, UnitId: order.UnitId)
            { IsRetreat = order.Kind == MilitaryOrderKind.Return && (unit.AutonomousReturn || unit.HasForcedReturn) };
    }
}
