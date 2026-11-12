using System.Collections.Immutable;
using San9.Application.Persistence;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed class FormationTeachingSessionTests
{
    [Fact]
    public void TeachingContinuesAcrossSaveAndCompletesAfterThirtyDays()
    {
        var known = ImmutableSortedDictionary<int, int>.Empty.Add(7, 500);
        var basis = G01ScenarioTests.Fixture();
        var scenario = basis with
        {
            Officers = basis.Officers.Select(p => p with { State = p.State with
            {
                FormationProficiencies = p.State.Id == 1 ? known : ImmutableSortedDictionary<int, int>.Empty
            } }).ToArray(),
            Military = new MilitaryDefinition(3, 3, Enumerable.Repeat(0, 9).ToImmutableArray(), [new(0, 1, null)],
                [new(7, 1, 0, 10000, 5, new(1, 1), TravelMode.Land)
                { Combat = new(10, 10, 10, 10, 50), Attack = new(1, true),
                    Positions = FormationTestData.Layout([BattleRank.Middle, BattleRank.Middle, BattleRank.Middle, BattleRank.Middle, BattleRank.Middle]) }],
                [new(1, new(7, 7), new(1, 1)), new(2, new(12, 12), new(1, 1))])
        };
        var session = GameSession.Start(scenario, 42, settings: new() { FormationLearningEnabled = true });
        var command = new StartFormationTeachingCommand(Guid.NewGuid(), 1, session.Snapshot.Simulation.NextSequence, 1, 1, 2, 7);
        Assert.True(session.Execute(new("player"), command).Submission.Accepted);
        session.AdvanceXun();
        var restored = SaveService.Decode(SaveService.Encode(session), scenario);
        Assert.Equal(20, restored.Snapshot.Officers.Single(p => p.Id == 2).FormationTeaching!.DaysRemaining);
        for (int i = 0; i < 2; i++)
        {
            session.AdvanceXun();
            restored.AdvanceXun();
        }
        var student = restored.Snapshot.Officers.Single(p => p.Id == 2);
        Assert.Null(student.FormationTeaching);
        Assert.Equal(0, student.FormationProficiencies![7]);
        StateAssert.Equal(session.Capture(), restored.Capture());
    }
}
