using San9.Core.Domain;

namespace San9.Core.Tests;

public sealed class NavigationParityTests
{
    [Fact]
    public void CoarseSquareEndpointUsesAssociatedBuildingWithoutAddingAvoidance()
    {
        var blocked = Enumerable.Range(9, 3).SelectMany(y => Enumerable.Range(9, 3).Select(x => new MapCell(x, y)));
        var map = new BattlefieldMap(20, 20, new int[400], [new(0, 10, null)], blocked);
        var city = new MapOccupancy(new(52, 52), new(25, 25)) { Hard = false };
        Assert.False(map.FindRoute(new(52, 52), new(82, 52), new(4, 4), TravelMode.Land, []).Reachable);
        Assert.True(map.FindRoute(new(52, 52), new(82, 52), new(4, 4), TravelMode.Land, [city]).Reachable);
        Assert.Null(map.PassageCost(new(52, 52), new(4, 4), TravelMode.Land, [city]));
    }

    [Fact]
    public void BlockedSourceFootprintCanLeaveButDoesNotOpenOtherTerrain()
    {
        var map = new BattlefieldMap(20, 20, new int[400], [new(0, 10, null)], [new(10, 10)]);
        var step = map.Navigate(new(52, 52), new(new(82, 52), new(1, 1)), NavigationArrival.Point, new(10, 10), TravelMode.Land, [], 100);
        Assert.NotEqual(new WorldPosition(52, 52), step.Position);
        Assert.Contains(step.Steps, p => p.X >= 55);
        Assert.Null(map.PassageCost(new(52, 52), new(10, 10), TravelMode.Land, []));
        var wall = new BattlefieldMap(20, 20, new int[400], [new(0, 10, null)], Enumerable.Range(0, 20).Select(y => new MapCell(10, y)));
        Assert.Equal(NavigationStatus.Unreachable,
            wall.Navigate(new(12, 52), new(new(82, 52), new(1, 1)), NavigationArrival.Point, new(4, 4), TravelMode.Land, [], 100).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EndpointRelaxationDoesNotOverrideObjectCollision(bool hard)
    {
        var map = new BattlefieldMap(20, 20, new int[400], [new(0, 10, null)], [new(10, 10)]);
        var target = new MapOccupancy(new(52, 52), new(15, 15)) { Hard = hard };
        var position = new WorldPosition(32, 52);
        NavigationLookahead? stage = null;
        for (int day = 0; day < 15; day++)
        {
            var step = map.Navigate(position, target, NavigationArrival.Point, new(4, 4), TravelMode.Land, [target], 100, lookahead: stage);
            if (hard) Assert.All(step.Steps, p => Assert.False(new MapOccupancy(p, new(4, 4)).Overlaps(target)));
            position = step.Position; stage = step.Lookahead;
        }
        Assert.Equal(!hard, position == target.Position);
    }

    [Fact]
    public void AttackGoalDoesNotTurnSoftObjectIntoHardCollision()
    {
        var map = new BattlefieldMap(20, 20, new int[400], [new(0, 10, null)]);
        var target = new MapOccupancy(new(52, 52), new(10, 10)) { Hard = false, Avoidance = NavigationAvoidance.Hostile };
        var step = map.Navigate(new(42, 52), target, NavigationArrival.Range, new(4, 4), TravelMode.Land, [target], 200, continueApproach: true);
        Assert.Contains(step.Steps, p => new MapOccupancy(p, new(4, 4)).Overlaps(target));
        var blocked = map.Navigate(new(42, 52), target, NavigationArrival.Range, new(4, 4), TravelMode.Land, [target with { Hard = true }], 200, continueApproach: true);
        Assert.All(blocked.Steps, p => Assert.False(new MapOccupancy(p, new(4, 4)).Overlaps(target)));
    }

    [Fact]
    public void LargerUnusedShipDoesNotChangeLandNavigation()
    {
        var map = new BattlefieldMap(30, 30, new int[900], [new(0, 10, null)]);
        var land = new FormationRule(0, 100, 0, 100000, 1, new(4, 4), TravelMode.Land);
        var ship = new FormationRule(1, 100, 0, 100000, 1, new(10, 10), TravelMode.Water);
        var combined = map.ForTravel(land, ship, 100);
        MapOccupancy[] objects = [new(new(70, 65), new(8, 8)) { Hard = false, Avoidance = NavigationAvoidance.Friendly }];
        for (ulong seed = 0; seed < 20; seed++)
        {
            var a = map.Navigate(new(52, 52), new(new(112, 102), new(1, 1)), NavigationArrival.Point, land.Footprint, TravelMode.Land, objects, 100, randomSeed: seed);
            var b = combined.Navigate(new(52, 52), new(new(112, 102), new(1, 1)), NavigationArrival.Point, ship.Footprint, TravelMode.Land, objects, 100, randomSeed: seed);
            Assert.Equal(a.Steps.ToArray(), b.Steps.ToArray());
            Assert.Equal(a.Lookahead, b.Lookahead);
        }
    }

    [Fact]
    public void RelayCoarseRouteAvoidsFinalBuilding()
    {
        var map = new BattlefieldMap(25, 25, new int[625], [new(0, 10, null)]);
        var building = new MapOccupancy(new(52, 52), new(15, 15));
        var result = map.EstimateTravelCost(new(12, 52), new(new(92, 52), new(1, 1)), new(4, 4), TravelMode.Land,
            [], new(10000), arrival: NavigationArrival.Point, includeCells: true, excluded: building);
        Assert.Equal(RouteReachability.Reachable, result.Status);
        Assert.DoesNotContain(result.Cells, c => new MapOccupancy(MapCoordinates.Center(c), new(1, 1)).Overlaps(building));
        var arrival = map.EstimateTravelCost(new(12, 52), building, new(4, 4), TravelMode.Land,
            [], new(10000), arrival: NavigationArrival.Point, includeCells: true, excluded: building);
        Assert.Equal(RouteReachability.Reachable, arrival.Status);
        Assert.Equal(MapCoordinates.Cell(building.Position), arrival.Cells[^1]);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 8)]
    [InlineData(8, 1)]
    [InlineData(4, 4)]
    [InlineData(5, 5)]
    [InlineData(4, 7)]
    [InlineData(7, 4)]
    public void GradientCoversHalfOpenFootprintWithoutDirectionalGaps(int width, int height)
    {
        var area = new MapOccupancy(new(12, 12), new(width, height));
        var cells = new Dictionary<(int, int), int>();
        BattlefieldPathSearch.RasterizeGradient(area, 25, 10,
            (x, y, cost) => Assert.True(cells.TryAdd((x, y), cost)));
        Assert.Equal(width * height, cells.Count);
        Assert.All(cells, cell =>
        {
            var (x, y) = cell.Key;
            Assert.True(area.Contains(new(x, y)));
            Assert.InRange(cell.Value, 25, 250);
            Assert.Equal(cell.Value, cells[((int)(area.Left + area.Right - 1 - x), y)]);
            Assert.Equal(cell.Value, cells[(x, (int)(area.Top + area.Bottom - 1 - y))]);
        });
    }

    [Fact]
    public void GradientIncreasesInwardAndSaturatesWithoutOpeningCornerChannels()
    {
        var cells = new Dictionary<(int, int), int>();
        BattlefieldPathSearch.RasterizeGradient(new(new(12, 12), new(5, 5)), 25, 10,
            (x, y, cost) => cells[(x, y)] = cost);
        Assert.Equal(25, cells[(14, 14)]);
        Assert.Equal(35, cells[(13, 13)]);
        Assert.Equal(45, cells[(12, 12)]);

        cells.Clear();
        BattlefieldPathSearch.RasterizeGradient(new(new(12, 12), new(5, 5)), 240, 10,
            (x, y, cost) => cells[(x, y)] = cost);
        Assert.Equal(240, cells[(14, 14)]);
        Assert.Equal(250, cells[(13, 13)]);
        Assert.Equal(250, cells[(12, 12)]);
    }

    [Theory]
    [InlineData(true, 115)]
    [InlineData(false, 95)]
    public void ObstacleGradientRetainsFullObjectDepthAtWindowBoundary(bool hard, int expectedEdgeCost)
    {
        var cells = new Dictionary<(int, int), int>();
        BattlefieldPathSearch.RasterizeObstacle(
            new(new(10, 10), new(20, 20)) { Hard = hard, Avoidance = NavigationAvoidance.Friendly },
            new(new(15, 15), new(10, 10)), new(4, 4), 20,
            (x, y, cost) => cells[(x, y)] = cost);
        Assert.Equal(expectedEdgeCost, cells[(10, 15)]);
        Assert.Equal(100, cells.Count);
        Assert.All(cells.Keys, p => Assert.InRange(p.Item1, 10, 19));
        Assert.All(cells.Keys, p => Assert.InRange(p.Item2, 10, 19));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ObstacleGradientCostsStayEqualAcrossWindows(bool hard)
    {
        var obstacle = new MapOccupancy(new(10, 10), new(21, 17))
            { Hard = hard, Avoidance = NavigationAvoidance.Friendly };
        var full = new Dictionary<(int, int), int>();
        BattlefieldPathSearch.RasterizeObstacle(obstacle, new(new(10, 10), new(40, 40)), new(4, 4), 20,
            (x, y, cost) => full.Add((x, y), cost));
        MapOccupancy[] windows = [new(new(15, 15), new(10, 10)), new(new(5, 5), new(10, 10))];
        foreach (var window in windows)
        {
            var clipped = new Dictionary<(int, int), int>();
            BattlefieldPathSearch.RasterizeObstacle(obstacle, window, new(4, 4), 20,
                (x, y, cost) => clipped.Add((x, y), cost));
            var expected = full.Where(p => window.Contains(new(p.Key.Item1, p.Key.Item2))).ToArray();
            Assert.Equal(expected.Length, clipped.Count);
            Assert.All(expected, p => Assert.Equal(p.Value, clipped[p.Key]));
        }
    }

    [Fact]
    public void SoftObstacleOutsideWindowDoesNotEnterThroughPadding()
    {
        BattlefieldPathSearch.RasterizeObstacle(
            new(new(5, 5), new(10, 10)) { Hard = false, Avoidance = NavigationAvoidance.Friendly },
            new(new(15, 5), new(10, 10)), new(4, 4), 20,
            (_, _, _) => Assert.Fail("The original footprint does not overlap the window."));
    }
}
