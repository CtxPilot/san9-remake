using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Core.Tests;

public sealed class CityBattleDamageTests
{
    private static WorldState World(bool playerAttacks = true)
    {
        int attacker = playerAttacks ? 1 : 2, defender = playerAttacks ? 2 : 1;
        SettlementState City(int id, int owner) => new(id, owner, 1000, 100, 100, 100, 1000, 1000, 1000, 1000, 1000)
            { Kind = SettlementKind.City, CorpsId = owner, Population = 1000000 };
        return WorldState.Create(new(190, 2, 1), 42, [new(1, 100), new(2, 100)],
            [City(10, attacker), City(20, defender)], [],
            units: [new(1, attacker, new(2, 2), 1000) { Morale = 100, CorpsId = attacker }],
            corps: [new(1, 1, null, new(1000, 1000)) { IsRoot = true },
                new(2, 2, null, new(1000, 1000)) { IsRoot = true }],
            controls: [new("player", 1, ControllerKind.Player), new("ai:2", 2, ControllerKind.Automatic)]);
    }

    [Theory]
    [InlineData(0, 1000, true)]
    [InlineData(25, 997, true)]
    [InlineData(50, 995, false)]
    [InlineData(100, 990, true)]
    [InlineData(200, 980, false)]
    [InlineData(int.MaxValue, 0, true)]
    public void RatesApplyToBothSidesAndAllThreeCurrentValuesWithoutRandomDraws(int percent, int remaining, bool playerAttacks)
    {
        var state = World(playerAttacks);
        var world = state.BeginMutation(companion: new() { CityBattleDamagePercent = percent });
        var before = state.CreateSnapshot();
        var city = world.Settlements[20];
        List<DomainEvent> events = [];
        CityBattleEffects.CompleteAttack(world, 1, 20, city.OwnerId, events);
        Assert.Equal(city with { PublicOrder = remaining, Commerce = remaining, Cultivation = remaining }, world.Settlements[20]);
        Assert.Equal(before.Simulation.RandomStreams, world.Simulation.RandomStreams);
        Assert.Equal(before.Corps, world.Corps.Values.OrderBy(c => c.Id));
        if (percent == 0)
        {
            Assert.Empty(events);
            state.Commit(world);
            StateAssert.Equal(before, state.CreateSnapshot());
        }
        else
        {
            var loss = Assert.Single(events.OfType<CityBattleDamageEvent>());
            Assert.Equal((1000 - remaining, 1000 - remaining, 1000 - remaining),
                (loss.PublicOrderLoss, loss.CommerceLoss, loss.CultivationLoss));
        }
    }

    [Fact]
    public void ScalingRoundsOnceAndDoesNotScaleAnAlreadyRoundedBaselineLoss()
    {
        var world = World().BeginMutation(companion: new() { CityBattleDamagePercent = 150 });
        world.Replace(world.Settlements[20] with { PublicOrder = 101, Commerce = 101, Cultivation = 101 });
        CityBattleEffects.CompleteAttack(world, 1, 20, 2, []);
        // 1.5% of 101 leaves 99.485 -> 99. Scaling the rounded original loss of 2 would incorrectly leave 98.
        Assert.Equal(99, world.Settlements[20].PublicOrder);
    }

    [Fact]
    public void EveryAttackUsesTheCurrentValuesWithoutDailyExemptionsAndRebuildsTrust()
    {
        var world = World().BeginMutation(companion: new() { CityBattleDamagePercent = 50 });
        world.RebuildForceTrust();
        int trust = world.Forces[2].Trust;
        List<DomainEvent> events = [];
        CityBattleEffects.CompleteAttack(world, 1, 20, 2, events);
        Assert.Equal(995, world.Settlements[20].PublicOrder);
        Assert.True(world.Forces[2].Trust < trust);
        CityBattleEffects.CompleteAttack(world, 1, 20, 2, events);
        Assert.Equal(990, world.Settlements[20].PublicOrder);
        world.SetClock(world.Clock.Advance());
        CityBattleEffects.CompleteAttack(world, 1, 20, 2, events);
        Assert.Equal(985, world.Settlements[20].PublicOrder);
        Assert.Equal(3, events.OfType<CityBattleDamageEvent>().Count());
        world.Replace(world.Settlements[20] with { OwnerId = 1, CorpsId = 1 });
        CityBattleEffects.CompleteAttack(world, 1, 20, 2, events);
        Assert.Equal(3, events.OfType<CityBattleDamageEvent>().Count());
    }
}
