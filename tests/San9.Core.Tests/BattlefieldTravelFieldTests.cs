using San9.Core.Domain;

namespace San9.Core.Tests;

public sealed class BattlefieldTravelFieldTests
{
    [Fact]
    public void OneExpansionServesAllDestinationsAndKeepsDirectedTerrainCosts()
    {
        var map = new BattlefieldMap(20, 1, Enumerable.Repeat(0, 20), [new(0, 2, null)]);
        var targets = Enumerable.Range(1, 19).Select(id => new TravelFieldEndpoint(id, new(new(5 * id + 2, 2), new(1, 1)))).ToArray();
        var field = BattlefieldTravelField.Build(map, [new(2, 2)], targets, TravelMode.Land, 1000, new(1000));
        Assert.True(field.Complete);
        Assert.Equal(20, field.Expanded);
        Assert.All(field.Routes, route =>
        {
            Assert.Equal(TravelFieldStatus.Reachable, route.Status);
            Assert.Equal(10L * route.Id, route.Cost);
            Assert.Equal(route.Cost, route.TerrainCost.Land);
            Assert.Equal(new MapCell(0, 0), route.Cells[0]);
            Assert.Equal(new MapCell(route.Id, 0), route.Cells[^1]);
        });
        int separateWork = targets.Sum(t => BattlefieldTravelField.Build(map, [new(2, 2)], [t], TravelMode.Land, 1000, new(1000)).Expanded);
        Assert.True(separateWork > 10 * field.Expanded);
        var directed = new BattlefieldMap(2, 1, [0, 1], [new(0, 1, null), new(1, 3, null)]);
        long Cost(WorldPosition start, WorldPosition end) => BattlefieldTravelField.Build(directed, [start],
            [new(0, new(end, new(1, 1)))], TravelMode.Land, 100, new(100)).Routes[0].Cost;
        Assert.Equal(15, Cost(new(2, 2), new(7, 2)));
        Assert.Equal(5, Cost(new(7, 2), new(2, 2)));
    }

    [Fact]
    public void HorizonDisconnectionAndInterruptedAnalysisAreDifferentResults()
    {
        var map = new BattlefieldMap(9, 1, new int[9], [new(0, 1, null)]);
        TravelFieldEndpoint[] targets = [new(1, new(new(22, 2), new(1, 1))), new(2, new(new(42, 2), new(1, 1)))];
        var bounded = BattlefieldTravelField.Build(map, [new(2, 2)], targets, TravelMode.Land, 20, new(100));
        Assert.Equal(TravelFieldStatus.Reachable, bounded.Routes[0].Status);
        Assert.Equal(TravelFieldStatus.BeyondHorizon, bounded.Routes[1].Status);
        var interrupted = BattlefieldTravelField.Build(map, [new(2, 2)], targets, TravelMode.Land, 100, new(1));
        Assert.False(interrupted.Complete);
        Assert.All(interrupted.Routes, r => Assert.Equal(TravelFieldStatus.Incomplete, r.Status));
        var wall = new BattlefieldMap(9, 1, new int[9], [new(0, 1, null)], [new(4, 0)]);
        var disconnected = BattlefieldTravelField.Build(wall, [new(2, 2)], [targets[1]], TravelMode.Land, 100, new(100));
        Assert.Equal(TravelFieldStatus.Unreachable, disconnected.Routes[0].Status);
    }

    [Fact]
    public void SpeedRatioCanChangeTheChosenWaterLandPath()
    {
        var map = new BattlefieldMap(9, 3, Enumerable.Range(0, 27).Select(i => i / 9 == 1 && i % 9 is >= 3 and <= 5 ? 1 : 0),
            [new(0, 1, null), new(1, null, 1) { IsWater = true }]);
        var land = new FormationRule(0, 20, 0, 10000, 1, new(1, 1), TravelMode.Land);
        var slowShip = land with { Id = 1, MovementBudget = 5, TravelMode = TravelMode.Water };
        var slow = map.ForTravel(land, slowShip, 20);
        var fast = map.ForTravel(land, slowShip with { MovementBudget = 20 }, 20);
        TravelFieldRoute Route(BattlefieldMap terrain) => BattlefieldTravelField.Build(terrain, [new(2, 7)],
            [new(1, new(new(42, 7), new(1, 1)))], TravelMode.Land, 1000, new(1000)).Routes[0];
        Assert.Equal(0, Route(slow).TerrainCost.Water);
        Assert.True(Route(fast).TerrainCost.Water > 0);
        Assert.Equal(Route(fast).Cost, fast.Reweight(Route(fast).TerrainCost));
    }

    [Fact]
    public void AddingAnEndpointDoesNotTurnABlockedBuildingIntoAThroughCorridor()
    {
        var map = new BattlefieldMap(9, 1, new int[9], [new(0, 1, null)], [new(4, 0)]);
        TravelFieldEndpoint[] targets = [new(1, new(new(22, 2), new(1, 1))), new(2, new(new(42, 2), new(1, 1)))];
        var result = BattlefieldTravelField.Build(map, [new(2, 2)], targets, TravelMode.Land, 1000, new(1000));
        Assert.Equal(TravelFieldStatus.Reachable, result.Routes[0].Status);
        Assert.Equal(TravelFieldStatus.Unreachable, result.Routes[1].Status);
    }
}
