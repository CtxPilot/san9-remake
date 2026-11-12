using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Core.Tests;

public sealed class FormationTeachingTests
{
    private static MilitaryRulebook Rules() => new(new(1, 1, [0], [new(0, 1, null)],
        [new(7, 10, 0, 10000, 5, new(1, 1), TravelMode.Land)
        { Combat = new(10, 10, 10, 10, 50), Attack = new(1, true), Positions = San9.Tests.FormationTestData.Layout([BattleRank.Middle, BattleRank.Middle, BattleRank.Middle, BattleRank.Middle, BattleRank.Middle]) }], []));
    private static WorldMutation World(bool learning = true)
    {
        var skills = ImmutableSortedDictionary<int, int>.Empty;
        var world = WorldState.Create(new(190, 1, 1), 1, [new ForceState(1, 0) { LeaderId = 1 }],
            [new SettlementState(1, 1, 0, 100, 100, 0, 0, 0, 100, 0, 100)],
            [new OfficerState(1, 1, 1, new(50, 50, 50, 50), false) { Status = OfficerStatus.Ruler, FormationProficiencies = skills.Add(7, 500) },
             new OfficerState(2, 1, 1, new(50, 50, 50, 50), false) { FormationProficiencies = skills },
             new OfficerState(3, 1, 1, new(50, 50, 50, 50), false) { FormationProficiencies = skills }],
            corps: [new CorpsState(1, 1, 1, new(0, 0)) { IsRoot = true }], newGame: new() { FormationLearningEnabled = learning });
        return world.BeginMutation();
    }
    [Fact]
    public void DisabledFormationLearningRejectsTeaching()
    {
        var world = World(learning: false);
        var command = new StartFormationTeachingCommand(Guid.NewGuid(), 1, 0, 1, 1, 2, 7);
        Assert.Equal(CommandRejection.InvalidTask, FormationTeaching.Start(world, command, Rules(), []));
        Assert.Null(world.Officers[2].FormationTeaching);
    }
    [Fact]
    public void TeachingCompletesAfterThirtyDaysAndPreventsDuplicateAssignments()
    {
        var world = World(); var events = new List<DomainEvent>();
        var command = new StartFormationTeachingCommand(Guid.NewGuid(), 1, 0, 1, 1, 2, 7);
        Assert.Equal(CommandRejection.None, FormationTeaching.Start(world, command, Rules(), events));
        Assert.Equal(CommandRejection.InvalidTask, FormationTeaching.Start(world, command with { StudentId = 3 }, Rules(), events));
        for (int i = 0; i < 29; i++) FormationTeaching.AdvanceDay(world, events);
        Assert.False(FormationKnowledge.Knows(world.Officers[2], 7));
        Assert.Equal(1, world.Officers[2].FormationTeaching!.DaysRemaining);
        FormationTeaching.AdvanceDay(world, events);
        Assert.Equal(0, world.Officers[2].FormationProficiencies![7]);
        Assert.Null(world.Officers[2].FormationTeaching);
        Assert.Single(events.OfType<FormationTeachingCompletedEvent>());
    }
    [Fact]
    public void TeacherDepartureImmediatelyCancelsWithoutLearning()
    {
        var world = World(); var events = new List<DomainEvent>();
        var command = new StartFormationTeachingCommand(Guid.NewGuid(), 1, 0, 1, 1, 2, 7);
        Assert.Equal(CommandRejection.None, FormationTeaching.Start(world, command, Rules(), events));
        world.Replace(world.Officers[1] with { OwnerId = null, Status = OfficerStatus.Free });
        FormationTeaching.Reconcile(world, events);
        Assert.Null(world.Officers[2].FormationTeaching);
        Assert.False(FormationKnowledge.Knows(world.Officers[2], 7));
        Assert.Single(events.OfType<FormationTeachingCancelledEvent>());
    }
}
