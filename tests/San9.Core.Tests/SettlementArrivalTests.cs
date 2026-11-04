using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Core.Tests;

public sealed class SettlementArrivalTests
{
    [Theory]
    [InlineData(25, 25, MilitaryOrderKind.Return)]
    [InlineData(15, 15, MilitaryOrderKind.Return)]
    [InlineData(15, 15, MilitaryOrderKind.Transport)]
    [InlineData(5, 15, MilitaryOrderKind.Return)]
    [InlineData(15, 5, MilitaryOrderKind.Return)]
    [InlineData(5, 25, MilitaryOrderKind.Return)]
    [InlineData(25, 5, MilitaryOrderKind.Return)]
    public void EntryUsesCenterCellAndTransfersTroopsOnlyAfterCrossingTheBoundary(int width, int height, MilitaryOrderKind kind)
    {
        var area = new MapOccupancy(new(52, 52), new(width, height));
        var region = area.CoveringCells();
        var start = new WorldPosition((int)region.Left - 3, 52);
        var rules = new MilitaryRulebook(new(20, 20, new int[400].ToImmutableArray(), [new(0, 1, null)],
            [new(0, 1, 0, 100000, 5, new(8, 8), TravelMode.Land)], [new(1, area.Position, area.Footprint)])
            { TransportFormations = kind == MilitaryOrderKind.Transport ? new(0, null) : null });
        var unit = new UnitState(0, 1, start, 30000) { FormationId = 0, Order = new(kind, null, 1), ServicePlaceId = 1 };
        var world = WorldState.Create(new(190, 1, 1), 42, [new(1, 100)],
            [new(1, 1, 100, 100, 100, 70, 0, 0, 0, 0, 0)], [], [unit],
            corps: [new(1, 1, null, new(0, 10000)) { IsRoot = true }]);
        var snapshot = world.CreateSnapshot();
        Assert.True(new MapOccupancy(start, new(8, 8)).Overlaps(area));
        Assert.Equal(new(ArrivalEstimateStatus.Known, 3), new MilitaryArrivalForecast(snapshot, rules).Estimate(0));
        Assert.Equal(new(ArrivalEstimateStatus.Known, 3), NavigationArrivalForecast.Evaluate(
            new(rules.Travel(unit), start, area, [], Arrival: NavigationArrival.Region), new(10000)));
        Assert.Equal(3, new MilitaryRouteQueryContext(snapshot, rules).Estimate(unit, 0).Days);
        Assert.Equal(0, new MilitaryRouteQueryContext(snapshot, rules).Estimate(unit with { Position = new((int)region.Left, 52) }, 0).Days);
        var mutation = world.BeginMutation();
        for (int day = 1; day <= 3; day++)
        {
            var events = new List<DomainEvent>();
            var arrivals = MilitaryMovement.AdvanceDay(mutation, rules, events);
            var moved = Assert.Single(events.OfType<UnitMovedEvent>());
            Assert.Equal(day == 3, region.Contains(moved.To));
            MilitaryMovement.ResolveArrivals(mutation, arrivals, rules, new([]), PersonnelRulebook.Empty, events);
            Assert.Equal(day == 3, events.OfType<UnitAbsorbedEvent>().Any());
            Assert.Equal(day == 3 ? 30100u : 100u, mutation.Settlements[1].Soldiers);
            mutation.SetClock(mutation.Clock.NextDay());
        }
        Assert.Empty(mutation.Units);
        Assert.Single(world.CreateSnapshot().Units);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(5, 1)]
    public void NarrowRegionsUseOutwardMapCellBoundsAndExcludeTheNextCell(int width, int height)
    {
        var target = new MapOccupancy(new(17, 17), new(width, height));
        var region = target.CoveringCells();
        var map = new BattlefieldMap(8, 8, new int[64], [new(0, 1, null)]);
        var outside = new WorldPosition((int)region.Left - 1, 17);
        var entrance = outside with { X = (int)region.Left };
        Assert.False(map.Navigate(outside, target, NavigationArrival.Region, new(8, 8), TravelMode.Land, [], 0).Arrived);
        Assert.True(map.Navigate(entrance, target, NavigationArrival.Region, new(8, 8), TravelMode.Land, [], 0).Arrived);
        Assert.False(map.Navigate(entrance with { X = (int)region.Right }, target, NavigationArrival.Region,
            new(8, 8), TravelMode.Land, [], 0).Arrived);
        Assert.False(map.Navigate(new(17, (int)region.Top - 1), target, NavigationArrival.Region,
            new(8, 8), TravelMode.Land, [], 0).Arrived);
        Assert.True(map.Navigate(new(17, (int)region.Top), target, NavigationArrival.Region,
            new(8, 8), TravelMode.Land, [], 0).Arrived);
        Assert.False(map.Navigate(new(17, (int)region.Bottom), target, NavigationArrival.Region,
            new(8, 8), TravelMode.Land, [], 0).Arrived);
    }

    [Fact]
    public void ForecastChargesOnlyThePathToEntryAndSearchesOnce()
    {
        var map = new BattlefieldMap(20, 3, Enumerable.Range(0, 60).Select(i => i % 20 >= 14 ? 1 : 0),
            [new(0, 2, null), new(1, 9, null)]);
        var target = new MapOccupancy(new(82, 7), new(25, 15));
        var start = new WorldPosition(2, 7);
        using var diagnostics = new PathSearchDiagnostics();
        var estimate = map.EstimateTravelCost(start, target, new(8, 8), TravelMode.Land, [], new(10000),
            arrival: NavigationArrival.Region, includeCells: true);
        Assert.Equal(RouteReachability.Reachable, estimate.Status);
        Assert.Equal(67 * 2 + 9, estimate.Cost); // Entry at x=70; high-cost cells inside do not add the remaining center path.
        Assert.Equal(1, diagnostics.Queries);
        Assert.Equal(new MapCell(14, 1), estimate.Cells[^1]);
        Assert.Equal(start, map.Navigate(start, target, NavigationArrival.Region, new(8, 8), TravelMode.Land, [], 0).Position);
    }
}
