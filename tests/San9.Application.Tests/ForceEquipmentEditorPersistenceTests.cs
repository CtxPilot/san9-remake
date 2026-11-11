using San9.Application.Persistence;
using San9.Content.Scenarios;
using San9.Core.Domain;

namespace San9.Application.Tests;

public sealed class ForceEquipmentEditorPersistenceTests
{
    [Fact]
    public void ForceEditorPreviewIsIsolatedAndChangesSurviveSave()
    {
        var scenario = G01ScenarioTests.Fixture(); var session = GameSession.Start(scenario, 42);
        var source = session.Snapshot.Forces.Single(f => f.Id == 1);
        var operation = new WorldEditOperation(WorldEditorKind.Forces, [new ForceEdit(1, 90, source.ColorCode, [new(1, 2, 75)])]);
        var preview = session.PreviewEditor(operation);
        Assert.Equal(90, preview.Forces.Single(f => f.Id == 1).BaseTrust);
        Assert.Equal(source.BaseTrust, session.Snapshot.Forces.Single(f => f.Id == 1).BaseTrust);
        Assert.Equal(SessionDisposition.Accepted, session.Apply(session.Request(operation)).Disposition);
        var loaded = SaveService.Decode(SaveService.Encode(session), scenario);
        Assert.Equal(90, loaded.Snapshot.Forces.Single(f => f.Id == 1).BaseTrust);
        Assert.Equal(75, loaded.Snapshot.Relations.Single(r => r.FirstId == 1 && r.SecondId == 2).Value);
    }

    [Fact]
    public void EquipmentEditorPreviewAndSavePreserveInstanceState()
    {
        var basis = G01ScenarioTests.Fixture();
        var scenario = basis with { Information = basis.Information with { Equipment = EquipmentPreset.Basic() } };
        var session = GameSession.Start(scenario, 42);
        var item = session.Snapshot.Equipment.First(e => e.OfficerId == 1 && e.Equipped);
        var operation = new WorldEditOperation(WorldEditorKind.Equipment, [])
        { EquipmentEdits = [new(item.Id, item.OfficerId, null, false, item.Level, item.Experience)] };
        Assert.False(session.PreviewEditor(operation).Equipment.Single(e => e.Id == item.Id).Equipped);
        Assert.True(session.Snapshot.Equipment.Single(e => e.Id == item.Id).Equipped);
        Assert.Equal(SessionDisposition.Accepted, session.Apply(session.Request(operation)).Disposition);
        Assert.False(SaveService.Decode(SaveService.Encode(session), scenario).Snapshot.Equipment.Single(e => e.Id == item.Id).Equipped);
    }
}
