using System.Collections.Immutable;
using San9.Application.AI.Military;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.Tests;

public sealed class MilitaryForceIndexTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(40)]
    public void SpatialQueriesMatchIndependentScanAcrossFootprintsAndTypedIdentities(int bucketSize)
    {
        MilitaryPresence[] observations = [
            new(new(1, true), MilitaryPresenceKind.Settlement, 7, 10, new(new(40, 40), new(15, 15)), 1000, 300),
            new(new(1, false), MilitaryPresenceKind.Unit, 7, 11, new(new(40, 40), new(1, 1)), 2000, 400),
            new(new(2, true), MilitaryPresenceKind.Settlement, null, null, new(new(0, 0), new(9, 9)), 0, 0),
            new(new(3, false), MilitaryPresenceKind.Personnel, 8, 12, new(new(120, 120), new(1, 1)), 1, 0)];
        var index = MilitaryForceIndex.Create(observations.Reverse(), bucketSize);
        for (int x = -10; x <= 130; x += 7)
        for (int y = -10; y <= 130; y += 11)
        {
            var area = new MapOccupancy(new(x, y), new(7, 5));
            var expected = observations.Where(o => o.Area.Overlaps(area)).Select(o => o.Object).ToHashSet();
            var actual = index.Nearby(area);
            Assert.Equal(expected.Count, actual.Objects.Length);
            Assert.True(expected.SetEquals(actual.Objects.Select(o => o.Object)));
            Assert.InRange(actual.ObjectsChecked, 0, observations.Length);
        }
        var nearEdge = index.Nearby(new(new(49, 40), new(1, 1)), 2);
        Assert.Contains(nearEdge.Objects, p => p.Object == new MapObjectReference(1, true));
        Assert.DoesNotContain(nearEdge.Objects, p => p.Object == new MapObjectReference(1, false));
        Assert.True(index.Nearby(new(new(0, 0), new(int.MaxValue, int.MaxValue))).UsedFullScan);
        Assert.Equal(new MilitaryStrength(1000, 2000, 300, 400), index.Strength(7));
        Assert.Equal(0, index.Strength(8).MobileSoldiers); // Personnel carrier is not an extra fighting soldier.
        Assert.Equal(2, index.ForForce(7).Length);
        Assert.Single(index.ForCorps(11));
        Assert.Equal(MilitaryPresenceKind.Unit, index.Find(new(1, false))!.Kind);
        Assert.Equal(MilitaryPresenceKind.Settlement, index.Find(new(1, true))!.Kind);
    }

    [Fact]
    public void BatchRebuildTracksMovementWoundsOwnershipAndRemovalWithoutReadingHiddenOrders()
    {
        var clock = new GameClock(190, 1, 1);
        var rules = new MilitaryRulebook(new(30, 30, Enumerable.Repeat(0, 900).ToImmutableArray(), [new(0, 1, null)],
            [new(10, 10, 0, 10000, 5, new(3, 3), TravelMode.Land)],
            [new(1, new(10, 10), new(3, 3)), new(2, new(100, 100), new(1, 1))]));
        var world = new WorldSnapshot(clock, new(clock, 281))
        {
            Settlements = [new(1, 7, 1000, 100, 100, 90, 100, 0, 0, 0, 0) { Wounded = 300 },
                new(2, 8, 9000, 100, 100, 90, 100, 0, 0, 0, 0) { IsDormant = true }],
            Units = [new(1, 7, new(10, 10), 1000) { IsEmbedded = true, FormationId = 10 },
                new(2, 7, new(50, 50), 2000) { FormationId = 10, Wounded = 400 },
                new(3, 7, new(55, 55), 1) { IsPersonnel = true }]
        };
        var before = MilitaryForceIndex.Capture(world, rules);
        Assert.Equal(3, before.Objects.Length);
        Assert.Equal(new MilitaryStrength(1000, 2000, 300, 400), before.Strength(7));
        var hiddenChanged = world with { Units = world.Units.Select(u => u with
        {
            Order = new(MilitaryOrderKind.Move, new(99, 99), null), Waypoint = new(60, 60),
            DepartureDaysRemaining = 9, EngagementTargetId = 99, Policies = new(true, true, true, true)
        }).ToArray() };
        Assert.Equal(before.Objects.ToArray(), MilitaryForceIndex.Capture(hiddenChanged, rules).Objects.ToArray());

        var changed = world with
        {
            Settlements = world.Settlements.Select(s => s with { IsDestroyed = true }).ToArray(),
            Units = [world.Units[1] with { Position = new(130, 130), OwnerId = 8, Soldiers = 1500, Wounded = 600 }]
        };
        var after = MilitaryForceIndex.Capture(changed, rules);
        Assert.Empty(after.ForForce(7));
        Assert.Equal(new MilitaryStrength(0, 1500, 0, 600), after.Strength(8));
        Assert.Empty(after.Nearby(new(new(50, 50), new(10, 10))).Objects);
        Assert.Single(after.Nearby(new(new(130, 130), new(1, 1))).Objects);
        Assert.Equal(before.Objects.ToArray(), MilitaryForceIndex.Capture(world, rules).Objects.ToArray());
    }

    [Fact]
    public void SparseQueryChecksLocalCandidatesWhileVeryWideQueryScansObjectsOnce()
    {
        var objects = Enumerable.Range(0, 1024).Select(id => new MilitaryPresence(new(id, false), MilitaryPresenceKind.Unit,
            7, 10, new(new(id % 32 * 100, id / 32 * 100), new(1, 1)), 1000, 0)).ToArray();
        var index = MilitaryForceIndex.Create(objects);
        var local = index.Nearby(new(new(1600, 1600), new(3, 3)));
        Assert.Single(local.Objects);
        Assert.Equal(1, local.ObjectsChecked);
        Assert.False(local.UsedFullScan);
        var wide = index.Nearby(new(new(0, 0), new(int.MaxValue, int.MaxValue)));
        Assert.Equal(objects.Length, wide.ObjectsChecked);
        Assert.Equal(objects.Length, wide.Objects.Length);
        Assert.True(wide.UsedFullScan);
    }
}
