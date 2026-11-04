using System.Collections.Immutable;
using San9.Application.AI.Military;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.Tests;

public sealed class MilitaryObservationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PermissionsAreSymmetricAndDistinguishUnknownFromKnownEmpty(bool orders, bool tactics)
    {
        var (world, rules) = Fixture(orders, tactics);
        var publication = MilitaryIntelligencePublication.Publish(world, rules);
        var first = publication.Observe(world, rules, 7);
        var second = publication.Observe(world, rules, 8);
        var enemy = first.Units.Single(u => u.Id == 2);
        var own = second.Units.Single(u => u.Id == 2);
        Assert.Equal(orders, enemy.Order.IsKnown);
        Assert.Null(enemy.Order.Value); // No order when visible; unknown when hidden.
        Assert.Equal(tactics, enemy.Equipment[0].Tactic.IsKnown);
        Assert.Null(enemy.Equipment[0].Tactic.Value);
        Assert.True(own.Order.IsKnown);
        Assert.True(own.Equipment[0].Tactic.IsKnown);
        Assert.Equal(orders, second.Units.Single(u => u.Id == 1).Order.IsKnown);
        Assert.Equal(tactics, second.Units.Single(u => u.Id == 1).Equipment[0].Tactic.IsKnown);
        Assert.Equal(1000, first.Forces.Strength(7).MobileSoldiers);
    }

    [Fact]
    public void EnemyPublicationIsFrozenWhileOwnSubmissionsAreCurrentAndSecretsAreNotRetained()
    {
        var (world, rules) = Fixture(true, true);
        var publication = MilitaryIntelligencePublication.Publish(world, rules);
        var current = world with
        {
            Revision = world.Revision + 1,
            Units = world.Units.Select(u => u with { Soldiers = 500, Position = new(50, 50),
                Order = new(MilitaryOrderKind.AttackSettlement, null, 90), Waypoint = new(80, 80),
                EngagementTargetId = 99, DepartureDaysRemaining = 9 }).ToArray(),
            Officers = world.Officers.Select(p => p with { EquippedTacticId = 20 }).ToArray()
        };
        var observation = publication.Observe(current, rules, 7);
        Assert.Equal(500, observation.Forces.Strength(7).MobileSoldiers);
        Assert.Equal(1000, observation.Forces.Strength(8).MobileSoldiers);
        Assert.Equal(90, observation.Units.Single(u => u.Id == 1).Order.Value!.SettlementId);
        Assert.Equal(20, observation.Units.Single(u => u.Id == 1).Equipment[0].Tactic.Value);
        Assert.Null(observation.Units.Single(u => u.Id == 2).Order.Value);
        Assert.Null(observation.Units.Single(u => u.Id == 2).Equipment[0].Tactic.Value);
        var republished = MilitaryIntelligencePublication.Publish(current, rules).Observe(current, rules, 7);
        Assert.Equal(90, republished.Units.Single(u => u.Id == 2).Order.Value!.SettlementId);
        Assert.NotEqual(99, republished.Units.Single(u => u.Id == 2).Order.Value!.UnitId);
        Assert.Equal(new[] { 1, 2 }, republished.UnitsTargeting(new(90, true)).ToArray());
        Assert.Empty(republished.UnitsTargeting(new(99, false)));

        var hidden = current with { Simulation = current.Simulation with { OtherForceOrdersVisible = false, OtherForceTacticsVisible = false } };
        var masked = publication.Observe(hidden, rules, 7).Units.Single(u => u.Id == 2);
        Assert.False(masked.Order.IsKnown);
        Assert.Null(masked.Order.Value);
        Assert.False(masked.Equipment[0].Tactic.IsKnown);
        Assert.Null(masked.Equipment[0].Tactic.Value);
        var hiddenObservation = MilitaryIntelligencePublication.Publish(hidden, rules).Observe(hidden, rules, 7);
        Assert.Equal(new[] { 1 }, hiddenObservation.UnitsTargeting(new(90, true)).ToArray());
        Assert.Throws<InvalidOperationException>(() => publication.Observe(current with { Clock = current.Clock.Advance() }, rules, 7));
    }

    [Fact]
    public void HiddenEnemyOrdersAndEquipmentCannotChangeObservedFactsOrTargetPrecision()
    {
        var (world, rules) = Fixture(false, false);
        var changed = world with
        {
            Units = world.Units.Select(u => u.OwnerId == 8 ? u with
                { Order = new(MilitaryOrderKind.Move, new(40, 40), null), Waypoint = new(30, 30) } : u).ToArray(),
            Officers = world.Officers.Select(p => p.OwnerId == 8 ? p with { EquippedTacticId = 50 } : p).ToArray()
        };
        var original = MilitaryIntelligencePublication.Publish(world, rules).Observe(world, rules, 7);
        var masked = MilitaryIntelligencePublication.Publish(changed, rules).Observe(changed, rules, 7);
        Assert.Equal(original.Forces.Objects.ToArray(), masked.Forces.Objects.ToArray());
        Assert.Equal(original.Units.Select(u => u.Order), masked.Units.Select(u => u.Order));
        Assert.Equal(original.Units.SelectMany(u => u.Equipment), masked.Units.SelectMany(u => u.Equipment));

        var publicWorld = changed with { Simulation = changed.Simulation with { OtherForceOrdersVisible = true } };
        var publicOrder = MilitaryIntelligencePublication.Publish(publicWorld, rules).Observe(publicWorld, rules, 7)
            .Units.Single(u => u.Id == 2).Order.Value!;
        Assert.Equal(100, publicOrder.RegionId);
        Assert.Null(publicOrder.Position); // Public UI says the region's surroundings, not exact movement coordinates.
    }

    [Fact]
    public void OwnershipOverlayRollbackMatchesColdPublication()
    {
        var (world, rules) = Fixture(true, true);
        var publication = MilitaryIntelligencePublication.Publish(world, rules);
        publication.Observe(world, rules, 7);
        var acquired = world with
        {
            Revision = world.Revision + 1,
            Units = world.Units.Select(u => u.Id == 2 ? u with { OwnerId = 7 } : u).ToArray(),
            Officers = world.Officers.Select(p => p.Id == 2 ? p with { OwnerId = 7 } : p).ToArray()
        };
        Assert.Equal(2000, publication.Observe(acquired, rules, 7).Forces.Strength(7).MobileSoldiers);
        var restored = world with { Revision = world.Revision + 2 };
        var warm = publication.Observe(restored, rules, 7);
        var cold = new MilitaryIntelligencePublication(publication.State).Observe(restored, rules, 7);
        Assert.Equal(cold.Forces.Objects.ToArray(), warm.Forces.Objects.ToArray());
        Assert.Equal(1000, warm.Forces.Strength(8).MobileSoldiers);
        Assert.Equal(cold.Units.Select(u => (u.Id, u.OwnerId, u.Order)), warm.Units.Select(u => (u.Id, u.OwnerId, u.Order)));
        Assert.Equal(cold.Units.SelectMany(u => u.Equipment), warm.Units.SelectMany(u => u.Equipment));
    }

    private static (WorldSnapshot, MilitaryRulebook) Fixture(bool orders, bool tactics)
    {
        var clock = new GameClock(190, 1, 1);
        var rules = new MilitaryRulebook(new(20, 20, Enumerable.Repeat(0, 400).ToImmutableArray(), [new(0, 1, null)],
            [new(10, 10, 0, 10000, 5, new(1, 1), TravelMode.Land)], [])
            { RegionIds = Enumerable.Repeat<int?>(100, 400).ToImmutableArray() });
        var world = new WorldSnapshot(clock, new(clock, 281) { OtherForceOrdersVisible = orders, OtherForceTacticsVisible = tactics })
        {
            Units = [new(1, 7, new(10, 10), 1000) { FormationId = 10 }, new(2, 8, new(20, 20), 1000) { FormationId = 10 }],
            Officers = [new(1, 7, null, new(50, 50, 50, 50), false) { UnitId = 1, EquippedTacticId = 10 },
                new(2, 8, null, new(50, 50, 50, 50), false) { UnitId = 2 }]
        };
        return (world, rules);
    }
}
