using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

public enum MilitaryPresenceKind { Settlement, Unit, Personnel }

/// <summary>Physical facts only. No orders, tactics, routes, policies or predicted arrivals.</summary>
public sealed record MilitaryPresence(MapObjectReference Object, MilitaryPresenceKind Kind, int? OwnerId,
    int? CorpsId, MapOccupancy Area, uint Soldiers, uint Wounded)
{
    public int? FormationId { get; init; }
    public int? NavalFormationId { get; init; }
    public UnitCondition Condition { get; init; }
    public int Morale { get; init; }
    public int? FacilityTypeId { get; init; }
}

public sealed record MilitaryStrength(long GarrisonSoldiers, long MobileSoldiers, long GarrisonWounded, long MobileWounded);
public sealed record MilitaryIndexWork(int Objects, int Buckets, long BucketReferences)
{
    public int UpdatedObjects { get; init; }
}
public sealed record MilitarySpatialQuery(ImmutableArray<MilitaryPresence> Objects, int BucketsVisited,
    long BucketReferencesVisited, int ObjectsChecked, bool UsedFullScan);

/// <summary>An immutable batch index, shared across tasks. Spatial proximity is not reachability.</summary>
public sealed class MilitaryForceIndex
{
    private readonly int bucketSize;
    private readonly ImmutableDictionary<(long X, long Y), ImmutableArray<MilitaryPresence>> buckets;
    private readonly ImmutableDictionary<int, ImmutableArray<MilitaryPresence>> forces;
    private readonly ImmutableDictionary<int, ImmutableArray<MilitaryPresence>> corps;
    private readonly ImmutableDictionary<int, MilitaryStrength> strengths;
    private readonly ImmutableDictionary<MapObjectReference, MilitaryPresence> identities;
    public ImmutableArray<MilitaryPresence> Objects { get; }
    public MilitaryIndexWork Work { get; }

    private MilitaryForceIndex(ImmutableArray<MilitaryPresence> objects, int bucketSize)
    {
        this.bucketSize = bucketSize;
        Objects = objects;
        identities = objects.ToImmutableDictionary(p => p.Object);
        forces = objects.Where(p => p.OwnerId is not null).GroupBy(p => p.OwnerId!.Value)
            .ToImmutableDictionary(g => g.Key, g => g.ToImmutableArray());
        corps = objects.Where(p => p.CorpsId is not null).GroupBy(p => p.CorpsId!.Value)
            .ToImmutableDictionary(g => g.Key, g => g.ToImmutableArray());
        strengths = forces.ToImmutableDictionary(p => p.Key, p => new MilitaryStrength(
            p.Value.Where(IsGarrison).Sum(o => (long)o.Soldiers), p.Value.Where(IsMobile).Sum(o => (long)o.Soldiers),
            p.Value.Where(IsGarrison).Sum(o => (long)o.Wounded), p.Value.Where(IsMobile).Sum(o => (long)o.Wounded)));
        var builder = new Dictionary<(long, long), List<MilitaryPresence>>();
        long references = 0;
        foreach (var item in objects)
        {
            var range = BucketRange(item.Area, 0);
            for (long y = range.Top; y <= range.Bottom; y++)
            for (long x = range.Left; x <= range.Right; x++)
            {
                if (!builder.TryGetValue((x, y), out var entries)) builder.Add((x, y), entries = []);
                entries.Add(item);
                references++;
            }
        }
        buckets = builder.ToImmutableDictionary(p => p.Key, p => p.Value.ToImmutableArray());
        Work = new(objects.Length, buckets.Count, references);
    }

    private MilitaryForceIndex(MilitaryForceIndex previous, int owner, ImmutableArray<MilitaryPresence> current)
    {
        bucketSize = previous.bucketSize;
        var replacement = current.ToDictionary(p => p.Object);
        var removed = previous.ForForce(owner).Where(p => !replacement.TryGetValue(p.Object, out var next) || p != next)
            .Concat(current.Select(p => previous.Find(p.Object)).OfType<MilitaryPresence>().Where(p => p.OwnerId != owner && p != replacement[p.Object]))
            .DistinctBy(p => p.Object).ToArray();
        var added = current.Where(p => previous.Find(p.Object) != p).ToArray();
        var removedIds = removed.Select(p => p.Object).ToHashSet();
        var identityBuilder = previous.identities.ToBuilder();
        foreach (var p in removed) identityBuilder.Remove(p.Object);
        foreach (var p in added) identityBuilder[p.Object] = p;
        identities = identityBuilder.ToImmutable();
        Objects = identities.Values.OrderByDescending(p => p.Object.IsSettlement).ThenBy(p => p.Object.Id).ToImmutableArray();
        var bucketBuilder = previous.buckets.ToBuilder();
        long references = previous.Work.BucketReferences;
        foreach (var p in removed) ChangeBuckets(p, false);
        foreach (var p in added) ChangeBuckets(p, true);
        buckets = bucketBuilder.ToImmutable();
        var forceBuilder = previous.forces.ToBuilder();
        var corpsBuilder = previous.corps.ToBuilder();
        var strengthBuilder = previous.strengths.ToBuilder();
        foreach (int id in removed.Concat(added).Select(p => p.OwnerId).OfType<int>().Distinct())
        {
            var entries = Replace(previous.ForForce(id), id, p => p.OwnerId);
            if (entries.IsEmpty) { forceBuilder.Remove(id); strengthBuilder.Remove(id); }
            else { forceBuilder[id] = entries; strengthBuilder[id] = Summarize(entries); }
        }
        foreach (int id in removed.Concat(added).Select(p => p.CorpsId).OfType<int>().Distinct())
        {
            var entries = Replace(previous.ForCorps(id), id, p => p.CorpsId);
            if (entries.IsEmpty) corpsBuilder.Remove(id); else corpsBuilder[id] = entries;
        }
        forces = forceBuilder.ToImmutable();
        corps = corpsBuilder.ToImmutable();
        strengths = strengthBuilder.ToImmutable();
        Work = new(Objects.Length, buckets.Count, references) { UpdatedObjects = removed.Select(p => p.Object).Union(added.Select(p => p.Object)).Count() };

        ImmutableArray<MilitaryPresence> Replace(ImmutableArray<MilitaryPresence> before, int id, Func<MilitaryPresence, int?> group) =>
            before.Where(p => !removedIds.Contains(p.Object)).Concat(added.Where(p => group(p) == id))
                .OrderByDescending(p => p.Object.IsSettlement).ThenBy(p => p.Object.Id).ToImmutableArray();

        void ChangeBuckets(MilitaryPresence item, bool add)
        {
            var range = BucketRange(item.Area, 0);
            for (long y = range.Top; y <= range.Bottom; y++)
            for (long x = range.Left; x <= range.Right; x++)
            {
                var key = (x, y);
                var entries = bucketBuilder.GetValueOrDefault(key, []);
                if (add) entries = entries.Add(item).OrderByDescending(p => p.Object.IsSettlement).ThenBy(p => p.Object.Id).ToImmutableArray();
                else entries = entries.Where(p => p.Object != item.Object).ToImmutableArray();
                references += add ? 1 : -1;
                if (entries.IsEmpty) bucketBuilder.Remove(key); else bucketBuilder[key] = entries;
            }
        }
    }

    public MilitaryForceIndex ReplaceForce(int forceId, IEnumerable<MilitaryPresence> current)
    {
        var values = current.OrderByDescending(p => p.Object.IsSettlement).ThenBy(p => p.Object.Id).ToImmutableArray();
        if (values.Any(p => p.OwnerId != forceId) || values.Select(p => p.Object).Distinct().Count() != values.Length)
            throw new ArgumentException("Friendly updates require unique observations owned by that force.", nameof(current));
        return ForForce(forceId).SequenceEqual(values) ? this : new(this, forceId, values);
    }

    public static MilitaryForceIndex Create(IEnumerable<MilitaryPresence> observations, int bucketSize = 40)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bucketSize, 1);
        var objects = observations.OrderByDescending(p => p.Object.IsSettlement).ThenBy(p => p.Object.Id).ToImmutableArray();
        if (objects.Any(p => p.Object.Id < 0 || !p.Area.Footprint.IsValid ||
                !Enum.IsDefined(p.Kind) || p.Object.IsSettlement != (p.Kind == MilitaryPresenceKind.Settlement)) ||
            objects.Select(p => p.Object).Distinct().Count() != objects.Length)
            throw new ArgumentException("Expected unique typed identities and valid physical observations.", nameof(observations));
        return new(objects, bucketSize);
    }

    /// <summary>Projects public physical state. Call once per observation batch, never per target.</summary>
    public static MilitaryForceIndex Capture(WorldSnapshot world, MilitaryRulebook rules, int bucketSize = 40)
        => Create(ProjectPhysical(world, rules), bucketSize);

    internal static IEnumerable<MilitaryPresence> ProjectPhysical(WorldSnapshot world, MilitaryRulebook rules, int? owner = null)
    {
        var places = world.Settlements.Where(s => !s.IsDormant && !s.IsDestroyed && (owner is null || s.OwnerId == owner) && rules.TryPlace(s, out _))
            .Select(s => new MilitaryPresence(new(s.Id, true), MilitaryPresenceKind.Settlement, s.OwnerId,
                s.CorpsId, rules.SettlementArea(s), s.Soldiers, s.Wounded) { FacilityTypeId = rules.Place(s).FacilityTypeId });
        // An embedded guard is a representation of its settlement garrison, not another field army.
        var units = world.Units.Where(u => !u.IsEmbedded && (owner is null || u.OwnerId == owner)).Select(u => new MilitaryPresence(new(u.Id, false),
            u.IsPersonnel ? MilitaryPresenceKind.Personnel : MilitaryPresenceKind.Unit, u.OwnerId, u.CorpsId,
            u.FormationId is null ? new(u.Position, new(1, 1)) : rules.Travel(u).AreaAt(u.Position), u.Soldiers, u.Wounded)
            { FormationId = u.FormationId, NavalFormationId = u.NavalFormationId, Condition = u.Condition, Morale = u.Morale });
        return places.Concat(units);
    }

    public ImmutableArray<MilitaryPresence> ForForce(int forceId) => forces.GetValueOrDefault(forceId, []);
    public ImmutableArray<MilitaryPresence> ForCorps(int corpsId) => corps.GetValueOrDefault(corpsId, []);
    public MilitaryPresence? Find(MapObjectReference identity) => identities.GetValueOrDefault(identity);
    public MilitaryStrength Strength(int forceId) => strengths.GetValueOrDefault(forceId) ?? new(0, 0, 0, 0);

    /// <summary>Exact rectangle overlap after coarse buckets, with typed deduplication and stable order.</summary>
    public MilitarySpatialQuery Nearby(MapOccupancy area, int margin = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(margin);
        if (!area.Footprint.IsValid) throw new ArgumentException("Expected a nonempty query rectangle.", nameof(area));
        var range = BucketRange(area, margin);
        var found = new List<MilitaryPresence>();
        int checkedObjects = 0, visitedBuckets = 0;
        long visitedReferences = 0;
        // Large empty regions must not cause unbounded bucket enumeration. A broad query scans once.
        bool scan = (Int128)(range.Right - range.Left + 1) * (range.Bottom - range.Top + 1) > buckets.Count;
        if (scan)
        {
            foreach (var item in Objects) Check(item);
        }
        else
        {
            HashSet<MapObjectReference> seen = [];
            for (long y = range.Top; y <= range.Bottom; y++)
            for (long x = range.Left; x <= range.Right; x++)
            {
                visitedBuckets++;
                if (!buckets.TryGetValue((x, y), out var entries)) continue;
                foreach (var item in entries)
                {
                    visitedReferences++;
                    if (seen.Add(item.Object)) Check(item);
                }
            }
        }
        return new(found.OrderByDescending(p => p.Object.IsSettlement).ThenBy(p => p.Object.Id).ToImmutableArray(),
            visitedBuckets, visitedReferences, checkedObjects, scan);

        void Check(MilitaryPresence item)
        {
            checkedObjects++;
            if (area.Left - margin < item.Area.Right && item.Area.Left < area.Right + margin &&
                area.Top - margin < item.Area.Bottom && item.Area.Top < area.Bottom + margin) found.Add(item);
        }
    }

    private (long Left, long Top, long Right, long Bottom) BucketRange(MapOccupancy area, int margin) =>
        (Floor(area.Left - margin), Floor(area.Top - margin), Floor(area.Right + margin - 1), Floor(area.Bottom + margin - 1));
    private long Floor(long coordinate) => coordinate >= 0 ? coordinate / bucketSize : (coordinate + 1) / bucketSize - 1;
    private static bool IsGarrison(MilitaryPresence p) => p.Kind == MilitaryPresenceKind.Settlement;
    private static bool IsMobile(MilitaryPresence p) => p.Kind == MilitaryPresenceKind.Unit;
    private static MilitaryStrength Summarize(IEnumerable<MilitaryPresence> values)
    {
        long garrison = 0, mobile = 0, garrisonWounded = 0, mobileWounded = 0;
        foreach (var p in values)
        {
            if (IsGarrison(p)) { garrison += p.Soldiers; garrisonWounded += p.Wounded; }
            else if (IsMobile(p)) { mobile += p.Soldiers; mobileWounded += p.Wounded; }
        }
        return new(garrison, mobile, garrisonWounded, mobileWounded);
    }
}
