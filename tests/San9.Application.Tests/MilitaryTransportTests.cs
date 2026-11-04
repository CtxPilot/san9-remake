using System.Collections.Immutable;
using San9.Application.AI.Military;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.Tests;

public sealed class MilitaryTransportTests
{
    [Fact]
    public void RequestedSourceSharesSearchAndColdWarmBudgetsHaveTheSameMeaning()
    {
        var (world, rules) = Fixture();
        var index = MilitaryForceIndex.Capture(world, rules);
        var network = new MilitaryTransportNetwork(rules, 2);
        network.Refresh(index);
        var coldBudget = new MilitaryQueryBudget(10000);
        var cold = network.Query(1, [2, 3], 10, null, 1000, coldBudget);
        var warmBudget = new MilitaryQueryBudget(10000);
        var warm = network.Query(1, [3, 2, 2], 10, null, 1000, warmBudget);
        Assert.False(cold.CacheHit);
        Assert.True(warm.CacheHit);
        Assert.Equal(cold.Routes.ToArray(), warm.Routes.ToArray());
        Assert.Equal(coldBudget.Expanded, warmBudget.Expanded);
        Assert.All(cold.Routes, r => Assert.Equal(1, r.SourceId)); // No parent-city/administrative source gate.
        var lowWarmBudget = new MilitaryQueryBudget(1);
        var lowWarm = network.Query(1, [2, 3], 10, null, 1000, lowWarmBudget);
        var fresh = new MilitaryTransportNetwork(rules);
        fresh.Refresh(index);
        var lowColdBudget = new MilitaryQueryBudget(1);
        var lowCold = fresh.Query(1, [2, 3], 10, null, 1000, lowColdBudget);
        Assert.All(lowWarm.Routes.Concat(lowCold.Routes), r => Assert.Equal(TravelFieldStatus.Incomplete, r.Status));
        Assert.Equal(lowColdBudget.Expanded, lowWarmBudget.Expanded);
        Assert.True(lowColdBudget.Exhausted && lowWarmBudget.Exhausted);
        var beyond = fresh.Query(1, [2], 10, null, 1000, new(), horizonDays: 1);
        Assert.Equal(TravelFieldStatus.BeyondHorizon, beyond.Routes[0].Status);
        // The precomputation horizon is not military eligibility: a wider request still finds the same target.
        Assert.Equal(TravelFieldStatus.Reachable, fresh.Query(1, [2], 10, null, 1000, new(), horizonDays: 60).Routes[0].Status);
        network.Query(1, [2], 10, null, 1000, new());
        network.Query(1, [3], 10, null, 1000, new());
        Assert.Equal(2, network.CachedFields);
    }

    [Fact]
    public void DemolishingAnObstacleOutsideTheOldRouteInvalidatesAndFindsTheShortcut()
    {
        var (world, rules) = Fixture();
        var physical = MilitaryForceIndex.Capture(world, rules).Objects;
        var wall = new MilitaryPresence(new(100, true), MilitaryPresenceKind.Settlement, 8, null,
            new(new(47, 17), new(1, 30)), 0, 0);
        var network = new MilitaryTransportNetwork(rules);
        network.Refresh(MilitaryForceIndex.Create(physical.Add(wall)));
        var before = network.Query(1, [2], 10, null, 1000, new()).Routes[0];
        Assert.Equal(TravelFieldStatus.Reachable, before.Status);
        Assert.DoesNotContain(new MapCell(9, 1), before.Cells);
        network.Refresh(MilitaryForceIndex.Create(physical));
        var after = network.Query(1, [2], 10, null, 1000, new());
        Assert.False(after.CacheHit);
        Assert.Contains(new MapCell(9, 1), after.Routes[0].Cells);
        Assert.True(after.Routes[0].Cost < before.Cost);
    }

    [Fact]
    public void MovementUsesActualPositionAndIncomingCountsDoNotSpendWoundsAsCombatPower()
    {
        var (world, rules) = Fixture();
        var observation = MilitaryIntelligencePublication.Publish(world, rules).Observe(world, rules, 7);
        var queries = new MilitaryTravelQueries(observation, rules, new(rules), new(), (_, owner) => owner == 8);
        var moving = queries.FromUnit(11, new(2, true));
        Assert.Equal(ArrivalEstimateStatus.Known, moving.Status);
        var source = queries.Transport(1, [2], 10, null, 1000).Routes[0];
        Assert.True(moving.Days < source.Days); // Snapping the unit back to its home city would fail this.
        Assert.False(moving.TargetIsMoving);
        var timeline = queries.Incoming(1);
        Assert.Single(timeline.Incoming);
        Assert.Equal(1000, timeline.SoldiersAt(0));
        Assert.Equal(3000, timeline.SoldiersAt(100));
        Assert.Equal(3600, timeline.ReceivingLoadAt(100));
        var changedSecrets = world with { Units = world.Units.Select(u => u with
            { Waypoint = new(90, 35), DepartureDaysRemaining = 9, Policies = new(true, true, true, true) }).ToArray() };
        var same = MilitaryIntelligencePublication.Publish(changedSecrets, rules).Observe(changedSecrets, rules, 7);
        var generic = new MilitaryTravelQueries(same, rules, new(rules), new(), (_, owner) => owner == 8).FromUnit(11, new(2, true));
        Assert.Equal(moving.Days, generic.Days); // No private modifier is inferred from the raw world.
    }

    [Fact]
    public void IncrementalFriendlyUpdatesMatchRebuildAndReuseUnchangedBatches()
    {
        var (world, rules) = Fixture();
        var publication = MilitaryIntelligencePublication.Publish(world, rules);
        var first = publication.Observe(world, rules, 7);
        Assert.Same(first, publication.Observe(world with { Revision = 1 }, rules, 7));
        var changed = world with { Revision = 2, Units = world.Units.Select(u => u.Id == 11 ? u with
            { Position = new(60, 30), Soldiers = 1500, Wounded = 900, CorpsId = 30 } : u).ToArray() };
        var updated = publication.Observe(changed, rules, 7);
        var rebuilt = MilitaryForceIndex.Capture(changed, rules);
        Assert.Equal(rebuilt.Objects.ToArray(), updated.Forces.Objects.ToArray());
        Assert.Equal(rebuilt.Strength(7), updated.Forces.Strength(7));
        Assert.Equal(1, updated.Forces.Work.UpdatedObjects);
        for (int x = 0; x < 100; x += 9)
        {
            var area = new MapOccupancy(new(x, 20), new(40, 40));
            Assert.Equal(rebuilt.Nearby(area).Objects.ToArray(), updated.Forces.Nearby(area).Objects.ToArray());
        }
        Assert.Empty(updated.Forces.ForCorps(10).Where(p => p.Object == new MapObjectReference(11, false)));
        Assert.Single(updated.Forces.ForCorps(30));
    }

    [Fact]
    public void PublicNavigationIncludesDeploymentDelayAndUsesTheMovingTargetsCurrentPosition()
    {
        var (world, rules) = Fixture();
        world = world with { Units = [world.Units[0], world.Units[0] with { Id = 12, OwnerId = 8, Position = new(77, 27) }] };
        MilitaryTravelQueries Queries() => new(MilitaryIntelligencePublication.Publish(world, rules).Observe(world, rules, 7),
            rules, new(rules), new(), (source, owner) => source != owner);
        var direct = Queries().FromSettlement(1, new(2, true), 10, null, 1000);
        var delayed = Queries().FromSettlement(1, new(2, true), 10, null, 1000, preparationDays: 9);
        Assert.Equal(ArrivalEstimateStatus.Known, direct.Status);
        Assert.Equal(direct.Days + 9, delayed.Days);
        var verified = Queries().FromUnit(11, new(12, false), verifyApproach: true);
        Assert.True(verified.TargetIsMoving);
        Assert.Equal(ArrivalEstimateStatus.Known, verified.Status);
        Assert.True(verified.Days >= verified.EarliestDays);
        var waiting = world with { Units = world.Units.Select(u => u.Id == 11 ? u with { Condition = UnitCondition.Confused } : u).ToArray() };
        var waitQuery = new MilitaryTravelQueries(MilitaryIntelligencePublication.Publish(waiting, rules).Observe(waiting, rules, 7),
            rules, new(rules), new(), (source, owner) => source != owner);
        Assert.Equal(ArrivalEstimateStatus.Waiting, waitQuery.FromUnit(11, new(2, true)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenOrDistantOrdersDoNotEraseNearThreatsAndFastUnitsSurviveTheSpatialFilter(bool orders)
    {
        var (world, original) = Fixture();
        var rules = new MilitaryRulebook(new(20, 8, Enumerable.Repeat(0, 160).ToImmutableArray(), [new(0, 1, null)],
            [.. original.Formations.Values, original.Formations[10] with { Id = 20, MovementBudget = 100 }],
            original.Places.Values.ToImmutableArray()));
        world = world with
        {
            Simulation = world.Simulation with { OtherForceOrdersVisible = orders },
            Units = [world.Units[0],
                world.Units[0] with { Id = 12, OwnerId = 8, Position = new(17, 7), Order = new(MilitaryOrderKind.Move, new(97, 37), null) },
                world.Units[0] with { Id = 13, OwnerId = 8, FormationId = 20, Position = new(87, 7), Order = new(MilitaryOrderKind.AttackSettlement, null, 1) }]
        };
        var observation = MilitaryIntelligencePublication.Publish(world, rules).Observe(world, rules, 7);
        var queries = new MilitaryTravelQueries(observation, rules, new(rules), new(), (_, owner) => owner == 8);
        var threats = queries.PotentialThreats(1, 1);
        Assert.Equal(new[] { 12, 13 }, threats.Candidates.Select(p => p.UnitId).Order().ToArray());
        Assert.False(threats.Candidates.Single(p => p.UnitId == 12).ExplicitTarget);
        Assert.Equal(orders, threats.Candidates.Single(p => p.UnitId == 13).ExplicitTarget);
        Assert.True(threats.Candidates.Select(p => p.UnitId).Distinct().Count() == threats.Candidates.Length);
    }

    [Fact]
    public void OwnershipAndDiplomacyChangesCannotReuseTheWrongPassageConditions()
    {
        var (world, rules) = Fixture();
        var physical = MilitaryForceIndex.Capture(world, rules).Objects;
        var gate = new MilitaryPresence(new(100, true), MilitaryPresenceKind.Settlement, 8, null,
            new(new(47, 17), new(1, 30)), 0, 0);
        var network = new MilitaryTransportNetwork(rules);
        network.Refresh(MilitaryForceIndex.Create(physical.Add(gate)));
        var hostile = network.Query(1, [2], 10, null, 1000, new(), isHostile: (_, owner) => owner == 8);
        var friendly = network.Query(1, [2], 10, null, 1000, new(), isHostile: (_, _) => false);
        Assert.False(friendly.CacheHit);
        Assert.True(friendly.Routes[0].Cost < hostile.Routes[0].Cost);
        network.Refresh(MilitaryForceIndex.Create(physical.Add(gate with { OwnerId = 7 })));
        var captured = network.Query(1, [2], 10, null, 1000, new(), isHostile: (_, owner) => owner == 8);
        Assert.Equal(friendly.Routes[0].Cost, captured.Routes[0].Cost);
    }

    [Fact]
    public void IntermediateObjectiveKeepsTheOriginalSourceWithoutAdministrativeReselection()
    {
        var (world, original) = Fixture();
        var rules = new MilitaryRulebook(new(20, 8, Enumerable.Repeat(0, 160).ToImmutableArray(), [new(0, 1, null)],
            original.Formations.Values.ToImmutableArray(), original.Places.Values.Select(p => p with { Id = p.Id + 1000 }).ToImmutableArray()));
        world = world with { Units = [], Settlements = world.Settlements.Select(s => s with { Id = s.Id + 1000 }).ToArray() };
        var queries = new MilitaryTravelQueries(MilitaryIntelligencePublication.Publish(world, rules).Observe(world, rules, 7),
            rules, new(rules), new(), (_, owner) => owner == 8);
        // The caller retains source 1001 while turning the final objective 1002 into intermediate 1003.
        // No parent-city identity, source slot interval or map-specific place number enters the query.
        var final = queries.Transport(1001, [1002], 10, null, 1000).Routes[0];
        var intermediate = queries.Transport(final.SourceId, [1003], 10, null, 1000).Routes[0];
        Assert.Equal(1001, intermediate.SourceId);
        Assert.Equal(TravelFieldStatus.Reachable, intermediate.Status);
        Assert.Equal(TravelFieldStatus.Reachable, final.Status);
    }

    [Fact]
    public void FineNavigationChecksPhysicalBarriersInsteadOfTreatingProximityAsPassage()
    {
        var (world, original) = Fixture();
        var rules = new MilitaryRulebook(new(20, 8, Enumerable.Repeat(0, 160).ToImmutableArray(), [new(0, 1, null)],
            original.Formations.Values.ToImmutableArray(), original.Places.Values.ToImmutableArray())
            { BlockedCells = Enumerable.Range(0, 8).Select(y => new MapCell(10, y)).ToImmutableArray() });
        var observation = MilitaryIntelligencePublication.Publish(world, rules).Observe(world, rules, 7);
        var queries = new MilitaryTravelQueries(observation, rules, new(rules), new(), (_, owner) => owner == 8);
        var route = queries.FromUnit(11, new(2, true), verifyApproach: true);
        Assert.Equal(ArrivalEstimateStatus.Unreachable, route.Status);
        Assert.True(route.EarliestDays < int.MaxValue); // An optimistic bound alone cannot authorize the trip.
    }

    private static (WorldSnapshot, MilitaryRulebook) Fixture()
    {
        var clock = new GameClock(190, 1, 1);
        var rules = new MilitaryRulebook(new(20, 8, Enumerable.Repeat(0, 160).ToImmutableArray(), [new(0, 1, null)],
            [new(10, 10, 0, 10000, 5, new(1, 1), TravelMode.Land)],
            [new(1, new(7, 7), new(1, 1)), new(2, new(87, 7), new(1, 1)), new(3, new(47, 27), new(1, 1))]));
        var world = new WorldSnapshot(clock, new(clock, 281))
        {
            Forces = [new(7, 100), new(8, 100)],
            Settlements = [new(1, 7, 1000, 100, 100, 90, 100, 0, 0, 0, 0) { Wounded = 200, CorpsId = 10 },
                new(2, 8, 1000, 100, 100, 90, 100, 0, 0, 0, 0), new(3, null, 0, 100, 100, 90, 100, 0, 0, 0, 0)],
            Units = [new(11, 7, new(42, 7), 2000) { FormationId = 10, Wounded = 400, CorpsId = 10,
                ServicePlaceId = 1, Order = new(MilitaryOrderKind.Return, null, 1) }]
        };
        return (world, rules);
    }
}
