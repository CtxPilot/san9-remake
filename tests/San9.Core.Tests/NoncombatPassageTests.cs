using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Core.Tests;

public sealed class NoncombatPassageTests
{
    [Theory]
    [InlineData(80, false, false, false)]
    [InlineData(40, false, false, false)]
    [InlineData(0, true, false, false)]
    [InlineData(39, false, false, true)]
    [InlineData(80, false, true, true)]
    public void PassageAndApproachShareOpponentRelationship(int relation, bool allied, bool explicitAttack, bool hard)
    {
        var rules = new MilitaryRulebook(new(20, 1, new int[20].ToImmutableArray(), [new(0, 1, null)],
            [new(1, 100, 0, 100000, 1, new(1, 5), TravelMode.Land)], []));
        var a = new UnitState(1, 1, new(47, 2), 5000)
        {
            FormationId = 1, Policies = new(AttackApproachingEnemies: true),
            Order = explicitAttack ? new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = 2 } : new(MilitaryOrderKind.Move, new(82, 2), null)
        };
        var b = new UnitState(2, 2, new(48, 2), 5000)
        { FormationId = 1, Policies = new(AttackApproachingEnemies: true), Order = new(MilitaryOrderKind.Move, new(17, 2), null) };
        var state = WorldState.Create(new(190, 1, 1), 42, [new(1, 100), new(2, 100), new(3, 100)], [], [], [a, b],
            corps: [new(1, 1, null, new(10000, 10000)) { IsRoot = true }, new(2, 2, null, new(10000, 10000)) { IsRoot = true },
                new(3, 3, null, new(10000, 10000)) { IsRoot = true }],
            relations: [new(1, 2, relation)], alliance: allied ? new(3, 1, [1, 2], 30) : null);
        var world = state.BeginMutation();
        Assert.Equal(hard, MilitaryDiplomacy.ApproachEnemy(world, a, b.OwnerId));
        Assert.Equal(hard, new MovementOccupancy(world, rules).Query(world, 1, a.Policies, 1).Single().Hard);
        Assert.Equal(hard, MilitaryRouteQuery.Occupancy(state.CreateSnapshot(), rules, 1, a.Policies, movingUnit: 1).Single().Hard);
        var query = new WorldQuery(state.CreateSnapshot());
        var attack = a with { Order = new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = 2 } };
        Assert.Equal(hard, query.MovementOccupancy(rules, 1, a.Policies, 1, null, a).Single().Hard);
        Assert.True(query.MovementOccupancy(rules, 1, a.Policies, 1, null, attack).Single().Hard);
        Assert.Equal(hard, query.MovementOccupancy(rules, 1, a.Policies, 1, null, a).Single().Hard);
        Assert.Equal(a, query.Units[1]);
        // Temporary combat eligibility is not a passage switch.
        world.Replace(a with { Condition = UnitCondition.Confused, ConditionDaysRemaining = 3 });
        Assert.Equal(hard, new MovementOccupancy(world, rules).Query(world, 1, a.Policies, 1).Single().Hard);
        world.Replace(a);
        if (hard)
        {
            var step = MilitaryNavigation.Advance(world, a, 1, rules, 100);
            Assert.True(step.Position.X < b.Position.X);
            Assert.NotNull(step.Navigation.Collision);
        }
        else
        {
            for (int day = 0; day < 8; day++)
            {
                foreach (int id in new[] { 1, 2 })
                {
                    var unit = world.Units[id];
                    world.AdvanceNavigation(id, MilitaryNavigation.Advance(world, unit, id, rules, 100));
                }
                world.SetClock(world.Clock.NextDay());
            }
            Assert.Equal(a.Order!.Position, world.Units[1].Position);
            Assert.Equal(b.Order!.Position, world.Units[2].Position);
        }
    }
}
