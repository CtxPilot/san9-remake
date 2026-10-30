using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Core.Tests;

public sealed class DailyStrategyResolutionTests
{
    [Fact]
    public void FalseReportChangesTheNextMovementRatherThanTheMovementBeforeItsResolution()
    {
        var rules = UnitStrategyTests.Rules();
        var world = UnitStrategyTests.World(distance: 30);
        var setup = world.BeginMutation();
        setup.Replace(setup.Units[5] with
        {
            ServicePlaceId = 2, HasEnteredDailyCycle = true,
            Order = new(MilitaryOrderKind.Move, new(20, 2), null)
        });
        world.Commit(setup);
        UnitStrategyTests.Submit(world, rules, UnitStrategyKind.FalseReport, 0);
        var state = world.BeginMutation(rules.Abilities);

        var first = UnitStrategyTests.Day(state, rules);
        var movement = Assert.Single(first.OfType<UnitMovedEvent>());
        var result = Assert.Single(first.OfType<UnitStrategyResolvedEvent>());
        Assert.True(result.Success);
        Assert.True(movement.To.X < movement.From.X);
        Assert.Equal(world.Clock, movement.Clock);
        Assert.Equal(movement.Clock, result.Clock);
        Assert.True(first.IndexOf(movement) < first.IndexOf(result));
        Assert.Equal(movement.To, result.Target.Position);
        Assert.Equal(Assert.Single(first.OfType<BattleConditionChangedEvent>()).DaysRemaining,
            state.Units[5].ConditionDaysRemaining);

        var nextMovement = Assert.Single(UnitStrategyTests.Day(state, rules).OfType<UnitMovedEvent>());
        Assert.True(nextMovement.To.X > nextMovement.From.X);
        Assert.Equal(result.Clock.NextDay(), nextMovement.Clock);
    }

    [Theory]
    [InlineData(UnitStrategyKind.FalseReport, UnitCondition.Confused)]
    [InlineData(UnitStrategyKind.Disorder, UnitCondition.NoFormation)]
    [InlineData(UnitStrategyKind.Inspire, UnitCondition.HighSpirits)]
    public void TemporaryTargetStateDuringTravelDoesNotCancelAnOtherwiseValidMission(
        UnitStrategyKind strategy, UnitCondition temporary)
    {
        var rules = UnitStrategyTests.Rules();
        var world = UnitStrategyTests.World(friendly: strategy == UnitStrategyKind.Inspire, distance: 91);
        var task = UnitStrategyTests.Submit(world, rules, strategy, 0);
        var state = world.BeginMutation(rules.Abilities);
        state.Replace(state.Units[5] with
        {
            Condition = temporary, ConditionDaysRemaining = 2, HasEnteredDailyCycle = true
        });

        var first = UnitStrategyTests.Day(state, rules);
        Assert.Equal(temporary, state.Units[5].Condition);
        Assert.Contains(state.Simulation.Tasks, t => t.Id == task.Id);
        Assert.Empty(first.OfType<TaskCancelledEvent>());
        Assert.Empty(first.OfType<UnitStrategyResolvedEvent>());

        List<DomainEvent> following = [];
        while (state.Clock.DayNumber <= task.Due.DayNumber)
            following.AddRange(UnitStrategyTests.Day(state, rules));
        Assert.Empty(following.OfType<TaskCancelledEvent>());
        var resolved = Assert.Single(following.OfType<UnitStrategyResolvedEvent>());
        Assert.Equal(task.Due, resolved.Clock);
        Assert.Equal(UnitCondition.Normal, resolved.Target.Condition);
        Assert.True(resolved.Success);
    }

    [Theory]
    [InlineData(UnitStrategyKind.Inspire)]
    [InlineData(UnitStrategyKind.Reinforce)]
    public void ReturningTargetIsAbsorbedBeforeAnArrivingStrategyCanApplyToIt(UnitStrategyKind strategy)
    {
        var rules = UnitStrategyTests.Rules();
        var world = UnitStrategyTests.World(friendly: true, distance: 1);
        var setup = world.BeginMutation();
        setup.Replace(setup.Units[5] with
        {
            ServicePlaceId = 1, HasEnteredDailyCycle = true,
            Order = new(MilitaryOrderKind.Return, null, 1)
        });
        world.Commit(setup);
        var task = UnitStrategyTests.Submit(world, rules, strategy, 0);
        var state = world.BeginMutation(rules.Abilities);

        var facts = UnitStrategyTests.Day(state, rules);
        var absorbed = Assert.Single(facts.OfType<UnitAbsorbedEvent>());
        var cancelled = Assert.Single(facts.OfType<TaskCancelledEvent>(), e => e.TaskId == task.Id);
        Assert.True(facts.IndexOf(absorbed) < facts.IndexOf(cancelled));
        Assert.Equal(task.Due, absorbed.Clock);
        Assert.Empty(facts.OfType<UnitStrategyResolvedEvent>());
        Assert.Empty(facts.OfType<OfficerAbilityGrownEvent>());
        Assert.False(state.Units.ContainsKey(5));
    }
}
