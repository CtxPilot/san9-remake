using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Core.Tests;

public sealed class SettlementTransitNetworkTests
{
    [Theory]
    [InlineData(7, 16, 17, 15)]
    [InlineData(6, 6, 7, 6)]
    public void SummaryDoesNotIncludeFacilitiesBeyondTheFirstTargetEntry(int source, int target, int beyond, int end)
    {
        var map = Map() with
        {
            Places = [new(17, new(source, 12), new(1, 1)), new(9001, new(target, 12), new(1, 1)),
                new(501, new(beyond, 12), new(1, 1))]
        };
        var summary = SettlementTransitNetwork.Build(new(map), Places()).Find(17, 9001)!.Summary;
        Assert.Equal(new WorldPosition(end, 12), summary.End);
        Assert.Empty(summary.ViaSettlementIds);
    }

    [Fact]
    public void SummaryDistinguishesARealCrossingFromANearbyFacilityInTheSameCoarseCell()
    {
        var map = Map() with
        {
            Places = [new(17, new(7, 12), new(1, 1)), new(501, new(27, 10), new(1, 1)),
                new(9001, new(62, 12), new(1, 1)), new(700, new(37, 12), new(1, 1))],
            RegionIds = Enumerable.Range(0, 80).Select(i => (int?)(i % 16 < 5 ? 1 : i % 16 < 9 ? 8 : 2)).ToImmutableArray()
        };
        var network = SettlementTransitNetwork.Build(new(map), [.. Places(), Place(700)]);
        var summary = network.Find(17, 9001)!.Summary;
        Assert.Equal(new[] { 700 }, summary.ViaSettlementIds);
        Assert.Equal(new[] { 1, 8, 2 }, summary.RegionIds);
        Assert.Equal(new[] { 0 }, summary.TerrainIds);
    }

    [Fact]
    public void SummaryCostsExcludeSourceInteriorAndStopBeforeTargetInterior()
    {
        var map = Map() with { Places = [new(17, new(12, 12), new(11, 3)), new(9001, new(62, 12), new(11, 3))] };
        var network = SettlementTransitNetwork.Build(new(map), [Place(17), Place(9001)]);
        var summary = network.Find(17, 9001)!.Summary;
        Assert.Equal(new WorldPosition(17, 12), summary.Start);
        Assert.Equal(new WorldPosition(56, 12), summary.End);
        Assert.Equal(new RouteTerrainCost(78, 0), summary.TerrainCost);
    }

    [Fact]
    public void ReferenceRouteCanBeRepricedForActualLandAndWaterSpeedsWithoutSearching()
    {
        var map = Map() with
        {
            TerrainIds = Enumerable.Range(0, 80).Select(i => i % 16 >= 8 ? 1 : 0).ToImmutableArray(),
            Terrains = [new(0, 2, null), new(1, null, 2) { IsWater = true }],
            Formations = [new(203, 10, 0, 100000, 5, new(1, 1), TravelMode.Land),
                new(204, 5, 0, 100000, 5, new(1, 1), TravelMode.Water),
                new(205, 20, 0, 100000, 5, new(1, 1), TravelMode.Land),
                new(206, 10, 0, 100000, 5, new(1, 1), TravelMode.Water)]
        };
        var rules = new MilitaryRulebook(map);
        var route = SettlementTransitNetwork.Build(rules, Places()).Find(17, 9001)!;
        using var diagnostics = new PathSearchDiagnostics();
        Assert.Equal(new RouteTerrainCost(54, 44), route.Summary.TerrainCost);
        Assert.Equal(15, route.Summary.EstimateDays(rules.Travel(203, 204)));
        Assert.Equal(8, route.Summary.EstimateDays(rules.Travel(205, 206)));
        Assert.Equal(0, diagnostics.Queries);
    }

    private static MilitaryDefinition Map() => new(16, 5, Enumerable.Repeat(0, 80).ToImmutableArray(),
        [new(0, 2, null)], [new(203, 10, 0, 100000, 5, new(1, 1), TravelMode.Land)],
        [new(17, new(12, 12), new(1, 1)), new(501, new(37, 12), new(1, 1)), new(9001, new(62, 12), new(1, 1))]);

    private static SettlementState Place(int id) => new(id, 1, 10000, 100, 100, 100, 100, 0, 100, 0, 100);
    private static SettlementState[] Places() => [Place(17), Place(501), Place(9001)];

    [Fact]
    public void AllSourceRelationsExistBeforeAnyQueryAndBothDirectionsUseOnlyIndexes()
    {
        var network = SettlementTransitNetwork.Build(new(Map()), Places().Select(s => s.Id == 501
            ? s with { Kind = SettlementKind.Facility, CanHostPersons = false } : s));
        Assert.Equal(3, network.BuildSearches);
        Assert.Equal(3, network.Nodes.Length);
        using var diagnostics = new PathSearchDiagnostics();
        Assert.All(network.Nodes, n => Assert.Equal(2, network.From(n.Id).Length));
        var route = Assert.IsType<SettlementTransitRoute>(network.Find(17, 9001));
        Assert.Equal(2, route.Reference.Days);
        Assert.Equal(new RouteTerrainCost(100, 0), route.Reference.TerrainCost);
        Assert.Equal(new[] { 501 }, route.Summary.ViaSettlementIds);
        Assert.Same(route, network.To(9001).Single(r => r.SourceId == 17));
        Assert.Equal(0, diagnostics.Queries);
        Assert.Equal(0, diagnostics.Expanded);
    }

    [Fact]
    public void SixtyReferenceDaysMeanThreeThousandMovementPoints()
    {
        var map = new MilitaryDefinition(620, 1, Enumerable.Repeat(0, 620).ToImmutableArray(), [new(0, 1, null)], [],
            [new(17, new(2, 2), new(1, 1)), new(501, new(3002, 2), new(1, 1)), new(9001, new(3052, 2), new(1, 1))]);
        var network = SettlementTransitNetwork.Build(new(map), Places());
        Assert.Equal(50, SettlementTransitNetwork.ReferenceDailyBudget);
        Assert.Equal(60, SettlementTransitNetwork.HorizonDays);
        Assert.Equal(60, network.Find(17, 501)!.Reference.Days);
        Assert.Equal(new RouteTerrainCost(3000, 0), network.Find(17, 501)!.Reference.TerrainCost);
        Assert.Null(network.Find(17, 9001));
        Assert.Equal(60, network.To(501).Single(r => r.SourceId == 17).Reference.Days);
    }

    [Fact]
    public void ReferenceMovementDoesNotDependOnSelectableFormationsOrOwnership()
    {
        var map = Map() with
        {
            TerrainIds = Enumerable.Range(0, 80).Select(i => i % 16 >= 8 ? 1 : 0).ToImmutableArray(),
            Terrains = [new(0, 2, null), new(1, null, 2) { IsWater = true }],
            Formations = []
        };
        var rules = new MilitaryRulebook(map);
        var network = SettlementTransitNetwork.Build(rules, Places());
        var route = Assert.IsType<SettlementTransitRoute>(network.Find(17, 9001));
        Assert.True(route.Reference.TerrainCost.Land > 0);
        Assert.True(route.Reference.TerrainCost.Water > 0);
        Assert.Equal(2, route.Reference.Days);
        Assert.Same(network, network.Update(Places().Select(s => s with { OwnerId = 2, Soldiers = 1, Wounded = 100 })));
    }

    [Fact]
    public void IncomingIndexKeepsTheForwardCostWhenOppositeDirectionCostsDiffer()
    {
        var map = new MilitaryDefinition(8, 1,
            Enumerable.Range(0, 8).Select(x => x == 1 ? 1 : x == 6 ? 2 : 0).ToImmutableArray(),
            [new(0, 1, null), new(1, 2, null), new(2, 5, null)], [],
            [new(17, new(7, 2), new(1, 1)), new(501, new(32, 2), new(1, 1))]);
        var network = SettlementTransitNetwork.Build(new(map), [Place(17), Place(501)]);
        Assert.Equal(45, network.Find(17, 501)!.Reference.TerrainCost.Land);
        Assert.Equal(30, network.Find(501, 17)!.Reference.TerrainCost.Land);
        Assert.Equal(45, Assert.Single(network.To(501)).Reference.TerrainCost.Land);
    }

    [Fact]
    public void RemovalAndConstructionCreateNewCompleteGraphsWithoutMutatingThePreviousOne()
    {
        var network = SettlementTransitNetwork.Build(new(Map()), Places());
        var removed = network.Update(Places().Select(s => s.Id == 501 ? s with { IsDestroyed = true } : s));
        Assert.NotSame(network, removed);
        Assert.Equal(2, removed.GeometryRevision);
        Assert.Equal(2, removed.BuildSearches);
        Assert.Empty(removed.Find(17, 9001)!.Summary.ViaSettlementIds);
        Assert.Equal(new[] { 501 }, network.Find(17, 9001)!.Summary.ViaSettlementIds);
        var rebuilt = removed.Update(Places());
        Assert.Equal(3, rebuilt.GeometryRevision);
        Assert.Equal(3, rebuilt.BuildSearches);
        Assert.Equal(new[] { 501 }, rebuilt.Find(17, 9001)!.Summary.ViaSettlementIds);
    }

    [Fact]
    public void BoundedFieldStillDistinguishesInterruptionHorizonAndDisconnection()
    {
        var rules = new MilitaryRulebook(Map());
        var endpoints = rules.Places.Values.Where(p => p.Id != 17).Select(p => new TransitEndpoint(p.Id, rules.BuildingArea(p))).ToArray();
        var travel = rules.Travel(203, null);
        Assert.All(SettlementTransitField.Build(travel, new(12, 12), endpoints, 1, new(1000)).Routes,
            r => Assert.Equal(TransitReachability.BeyondHorizon, r.Status));
        Assert.All(SettlementTransitField.Build(travel, new(12, 12), endpoints, 60, new(1)).Routes,
            r => Assert.Equal(TransitReachability.Incomplete, r.Status));
        var map = Map() with { BlockedCells = Enumerable.Range(0, 5).Select(y => new MapCell(5, y)).ToImmutableArray() };
        var blocked = SettlementTransitNetwork.Build(new(map), Places());
        Assert.Empty(blocked.From(17));
    }

    [Fact]
    public void PublicEstimatesReuseEqualMovementGeometryAndRecomputeDaysForTheActualSpeed()
    {
        var map = Map() with { Formations = [Map().Formations[0] with { SoldierFootprints = [new(20000, new(3, 3))] },
            Map().Formations[0] with { Id = 204, MovementBudget = 5 }] };
        var world = WorldState.Create(new(190, 1, 1), 42, [new(1, 100)], Places(), [], [],
            corps: [new(1, 1, null, new(1000, 10000)) { IsRoot = true }]).CreateSnapshot();
        var context = new MilitaryRouteQueryContext(world, new(map));
        var unit = new UnitState(0, 1, new(12, 12), 1)
            { FormationId = 203, Order = new(MilitaryOrderKind.Move, new(62, 12), null) };
        using var diagnostics = new PathSearchDiagnostics();
        var first = context.Estimate(unit, null);
        Assert.Equal(RouteReachability.Reachable, first.Status);
        Assert.Equal(10, first.Days);
        Assert.Equal(first.Days, context.Estimate(unit with { Soldiers = 5000 }, null).Days);
        Assert.Equal(20, context.Estimate(unit with { FormationId = 204 }, null).Days);
        Assert.Equal(1, diagnostics.Queries);
        Assert.Equal(RouteReachability.Reachable, context.Estimate(unit with { Soldiers = 20000 }, null).Status);
        Assert.Equal(2, diagnostics.Queries);
    }

    [Fact]
    public void InterruptedPublicEstimateDoesNotPoisonACompleteRetry()
    {
        var world = WorldState.Create(new(190, 1, 1), 42, [new(1, 100)], Places(), [], [],
            corps: [new(1, 1, null, new(1000, 10000)) { IsRoot = true }]).CreateSnapshot();
        var context = new MilitaryRouteQueryContext(world, new(Map()));
        var unit = new UnitState(0, 1, new(12, 12), 1)
            { FormationId = 203, Order = new(MilitaryOrderKind.Move, new(62, 12), null) };
        Assert.Equal(RouteReachability.Incomplete, context.Estimate(unit, null, maximumExpanded: 1).Status);
        Assert.Equal(RouteReachability.Reachable, context.Estimate(unit, null).Status);
    }
}
