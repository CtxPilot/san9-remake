using System.Collections.Immutable;
using System.Text.Json.Nodes;
using San9.Application.AI.Military;
using San9.Application.Persistence;
using San9.Application.Presentation;
using San9.Content.Scenarios;
using San9.Content.Text;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed class MilitaryPublicationSessionTests
{
    [Fact]
    public void UnpublishedEnemyDeploymentStaysUnknownThroughSaveAndPublishesWithExecution()
    {
        var content = Fixture();
        var session = GameSession.Start(content, 281);
        session.EndControl(new("player"));
        var deploy = new DeployUnitCommand(Guid.NewGuid(), 2, session.Snapshot.Simulation.NextSequence, 2, [2], 1000, 10,
            new(MilitaryOrderKind.Move, new(80, 80), null));
        var submitted = session.Execute(new("ai:2"), deploy);
        Assert.True(submitted.Submission.Accepted, submitted.Submission.Rejection.ToString());
        int unit = Assert.Single(submitted.Events.OfType<UnitDeployedEvent>()).UnitId;
        Assert.DoesNotContain(session.ObserveMilitary(1).Units, u => u.Id == unit);
        Assert.Contains(session.ObserveMilitary(2).Units, u => u.Id == unit && u.Order.IsKnown);
        var texts = new TextCatalog(1, new Dictionary<string, string> { ["person.2"] = "敌将", ["force.2"] = "敌军" });
        Assert.True(UnitCatalog.Read(session.View, new(content.Military!), texts, 4).Rows.Single().Cells[1].IsUnavailable);
        var restored = SaveService.Decode(SaveService.Encode(session), content);
        Assert.DoesNotContain(restored.ObserveMilitary(1).Units, u => u.Id == unit);
        Assert.DoesNotContain(restored.InitialView.ObserveMilitary(1)!.Units, u => u.Id == unit);
        StateAssert.Equal(session.Capture(), restored.Capture());
        EventAssert.Equal(session.AdvanceXun().Events, restored.AdvanceXun().Events);
        StateAssert.Equal(session.Capture(), restored.Capture());
        Assert.Contains(session.ObserveMilitary(1).Units, u => u.Id == unit && u.Order.IsKnown);
        Assert.Equal(session.Snapshot.Clock, session.Capture().MilitaryIntelligence!.PublishedAt);
        Assert.Same(session.ObserveMilitary(1), session.ObserveMilitary(1));
    }

    [Fact]
    public void StableVisibilityChangesInvalidateOnlyPermissionProjectionAndInvalidSavesAreRejected()
    {
        var content = Fixture();
        var session = GameSession.Start(content, 281);
        var before = session.ObserveMilitary(1);
        var changed = session.Apply(session.Request(new PkSettingsOperation(false, false, false)));
        Assert.Equal(SessionDisposition.Accepted, changed.Disposition);
        var hidden = session.ObserveMilitary(1);
        Assert.NotSame(before, hidden);
        Assert.Same(before.Forces, hidden.Forces);
        Assert.Equal(before.PublicationRevision, hidden.PublicationRevision);
        var snapshot = session.Capture();
        var invalid = snapshot with { MilitaryIntelligence = snapshot.MilitaryIntelligence! with
            { Revision = snapshot.World.Revision + 1 } };
        Assert.Throws<ArgumentException>(() => GameSession.Restore(content, invalid));
        foreach (var physical in new[]
        {
            snapshot.MilitaryIntelligence!.Physical[0] with { FacilityTypeId = int.MaxValue },
            snapshot.MilitaryIntelligence.Physical[0] with { Condition = (UnitCondition)int.MaxValue },
            snapshot.MilitaryIntelligence.Physical[0] with { Morale = -1 }
        })
        {
            var malformed = snapshot with { MilitaryIntelligence = snapshot.MilitaryIntelligence with
                { Physical = snapshot.MilitaryIntelligence.Physical.SetItem(0, physical) } };
            Assert.Throws<ArgumentException>(() => GameSession.Restore(content, malformed));
        }
        var saved = JsonNode.Parse(SaveService.Encode(session))!;
        saved["session"]!.AsObject().Remove("militaryIntelligence");
        Assert.Throws<SaveException>(() => SaveService.Decode(System.Text.Encoding.UTF8.GetBytes(saved.ToJsonString()), content));
    }

    [Fact]
    public void ExecutionEndingTheGamePublishesFinalFactsBeforeTerminalSave()
    {
        var content = Fixture() with { Year = 189, Month = 12, Day = 21, Terminal = new([], 190) };
        var session = GameSession.Start(content, 281);
        session.AdvanceXun();
        Assert.Equal(SimulationBoundary.Terminal, session.Snapshot.Simulation.Boundary);
        Assert.Equal(session.Snapshot.Clock, session.Capture().MilitaryIntelligence!.PublishedAt);
        var restored = SaveService.Decode(SaveService.Encode(session), content);
        Assert.Equal(session.ObserveMilitary(1).Forces.Objects.ToArray(), restored.ObserveMilitary(1).Forces.Objects.ToArray());
        Assert.Equal(restored.Snapshot.Clock, restored.InitialView.ObserveMilitary(1)!.PublishedAt);
        StateAssert.Equal(session.Capture(), restored.Capture());
    }

    private static ScenarioInitialState Fixture()
    {
        var basis = G01ScenarioTests.Fixture();
        return basis with
        {
            Settlements = basis.Settlements.Select(s => s with { Soldiers = 10000, Morale = 100, X = s.Id == 1 ? 1 : 3, Y = 1 }).ToArray(),
            Military = new(20, 20, Enumerable.Repeat(0, 400).ToImmutableArray(), [new(0, 1, null)],
                [new(10, 1, 0, 10000, 5, new(1, 1), TravelMode.Land)
                    { Positions = FormationTestData.Layout([BattleRank.Middle, BattleRank.Middle, BattleRank.Middle, BattleRank.Middle, BattleRank.Middle]) }],
                [new(1, new(7, 7), new(1, 1)), new(2, new(17, 7), new(1, 1))]),
            Officers = [new("person.1", new(1, 1, 1, new(50, 50, 50, 50), false) { Status = OfficerStatus.Ruler, BirthYear = 160 }),
                new("person.2", new(2, 2, 2, new(50, 50, 50, 50), false) { Status = OfficerStatus.Ruler, BirthYear = 160 })],
            Forces = [new(1, "force.1", 1, 100) { LeaderId = 1 }, new(2, "force.2", 2, 100) { LeaderId = 2 }],
            Information = basis.Information with { Corps = [new(1, 1, 1, new(10000, 100000)) { IsRoot = true }, new(2, 2, 2, new(10000, 100000)) { IsRoot = true }] },
            OriginalAi = OriginalAiDefinition.Default with { WorkRules = OriginalAiDefinition.Default.WorkRules.ToImmutableDictionary(p => p.Key,
                p => p.Value with { OpeningWaitDays = 10000 }) }
        };
    }
}
