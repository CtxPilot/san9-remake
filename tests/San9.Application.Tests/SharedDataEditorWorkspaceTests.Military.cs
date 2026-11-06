using System.Text.Json;
using San9.Application.Authoring;
using San9.Content.Packages;
using San9.Content.Scenarios;
using San9.Core.Domain;

namespace San9.Application.Tests;

public sealed partial class SharedDataEditorWorkspaceTests
{
    private async Task CreateSharedMilitary()
    {
        await CreatePeopleAndTactics();
        // This fixture's worlds inherit the same rules. Explicit full replacements
        // are covered separately and intentionally retain their replacement meaning.
        await EditMilitaryWorld("private", d => d with { DefinitionOverrides = new() });
    }

    private async Task EditMilitaryWorld(string id, Func<ScenarioDocument, ScenarioDocument> edit)
    {
        string path = Path.Combine(_root, id, "scenario.json");
        var document = JsonSerializer.Deserialize<ScenarioDocument>(await File.ReadAllTextAsync(path), ScenarioContentFiles.DataJsonOptions)!;
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(edit(document), ScenarioContentFiles.DataJsonOptions));
    }

    [Fact]
    public async Task SharedMilitaryCreationCopiesRulesAndSurvivesSaveWithoutWorldEdits()
    {
        await CreateSharedMilitary();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var world = await File.ReadAllBytesAsync(Path.Combine(_root, "a/scenario.json"));
        var formation = await workspace.CreateFormationAsync();
        var formationCopy = await workspace.CreateFormationAsync(7000);
        var tactic = await workspace.CreateTacticAsync();
        var tacticCopy = await workspace.CreateTacticAsync(6000);
        Assert.True(formation.IsCommon);
        Assert.True(workspace.IsCreatedFormation(formation.Id));
        Assert.True(workspace.IsCreatedTactic(tactic.Id));
        Assert.Equal(workspace.GetFormation(7000) with { Id = formationCopy.Id }, formationCopy);
        Assert.Equal(workspace.GetTactic(6000) with { Id = tacticCopy.Id }, tacticCopy);
        Assert.Equal(7000, workspace.GetFormationCatalog(formationCopy.Id).MapAppearanceId);
        Assert.Equal(new[] { 7000 }, tacticCopy.CompatibleFormations);
        Assert.DoesNotContain(tactic.Id, workspace.GetOfficer(9001).LearnedTactics);
        await workspace.SaveAsync();
        Assert.Equal(world, await File.ReadAllBytesAsync(Path.Combine(_root, "a/scenario.json")));
        var reopened = await SharedDataEditorWorkspace.OpenAsync(_root);
        Assert.False(reopened.HasChanges);
        Assert.True(ContentComparison.Equal(formation, reopened.GetFormation(formation.Id)));
        Assert.True(ContentComparison.Equal(tactic, reopened.GetTactic(tactic.Id)));
        foreach (string id in new[] { "a", "b", "private" })
        {
            var content = await ScenarioContentFiles.LoadAsync(Path.Combine(_root, id));
            var rules = new MilitaryRulebook(content.Scenario.Military!);
            Assert.Contains(formation.Id, rules.Formations.Keys);
            Assert.Contains(tactic.Id, rules.Tactics.Keys);
        }
    }

    [Fact]
    public async Task MilitaryIdentitiesAreNotReusedAfterDeletionOrReopening()
    {
        await CreateSharedMilitary();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var formation = await workspace.CreateFormationAsync();
        var tactic = await workspace.CreateTacticAsync();
        await workspace.DeleteFormationAsync(formation.Id);
        await workspace.DeleteTacticAsync(tactic.Id);
        await workspace.SaveAsync();
        var reopened = await SharedDataEditorWorkspace.OpenAsync(_root);
        Assert.True((await reopened.CreateFormationAsync()).Id > formation.Id);
        Assert.True((await reopened.CreateTacticAsync()).Id > tactic.Id);
        await Assert.ThrowsAsync<InvalidDataException>(() => reopened.DeleteFormationAsync(7000));
        await Assert.ThrowsAsync<InvalidDataException>(() => reopened.DeleteTacticAsync(6000));
    }

    [Fact]
    public async Task MilitaryRestoreUsesCreationBaselineEvenAfterSave()
    {
        await CreateSharedMilitary();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var formation = await workspace.CreateFormationAsync();
        var tactic = await workspace.CreateTacticAsync();
        workspace.SetFormation(formation with { MovementBudget = 71, Positions = [new(0, BattleRank.Front)] });
        workspace.SetTactic(tactic with { Power = 42 });
        await workspace.SaveAsync();
        workspace.RestoreFormation(formation.Id); workspace.RestoreTactic(tactic.Id);
        Assert.Equal(formation, workspace.GetFormation(formation.Id));
        Assert.Equal(tactic, workspace.GetTactic(tactic.Id));
        Assert.Equal(new FormationSlot(1, 1), Assert.Single(workspace.GetFormationCatalog(formation.Id).Presentation!.Slots));
        Assert.True(workspace.IsFormationChanged(formation.Id));
        Assert.True(workspace.IsTacticChanged(tactic.Id));
    }

    [Fact]
    public async Task FormationDeletionRejectsTacticReferencesAndKeepsTheWholeDraft()
    {
        await CreateSharedMilitary();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var formation = await workspace.CreateFormationAsync();
        var tactic = await workspace.CreateTacticAsync();
        workspace.SetTactic(tactic with { CompatibleFormations = [formation.Id] });
        var draft = workspace.Definitions;
        await Assert.ThrowsAsync<InvalidDataException>(() => workspace.DeleteFormationAsync(formation.Id));
        Assert.Same(draft, workspace.Definitions);
        workspace.SetTactic(tactic);
        await workspace.DeleteFormationAsync(formation.Id);
        Assert.DoesNotContain(workspace.Formations, f => f.Id == formation.Id);
    }

    [Fact]
    public async Task TacticDeletionRejectsUndeployedPeopleAndWorldOverrides()
    {
        await CreateSharedMilitary();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var tactic = await workspace.CreateTacticAsync();
        var person = await workspace.CreateOfficerAsync();
        workspace.SetOfficer(person with { LearnedTactics = [tactic.Id] });
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => workspace.DeleteTacticAsync(tactic.Id));
        Assert.Contains(person.Id.ToString(), error.Message);
        workspace.SetOfficer(person);
        await EditMilitaryWorld("b", d => d with { Officers = d.Officers.Select(o => o with
            { Overrides = o.Overrides with { LearnedTactics = new([tactic.Id]) } }).ToArray() });
        error = await Assert.ThrowsAsync<InvalidDataException>(() => workspace.DeleteTacticAsync(tactic.Id));
        Assert.Contains("剧本 b", error.Message);
        Assert.Contains(workspace.Tactics, t => t.Id == tactic.Id);
    }

    [Fact]
    public async Task SaveRechecksReferencesAddedAfterMilitaryDeletion()
    {
        await CreateSharedMilitary();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var tactic = await workspace.CreateTacticAsync();
        await workspace.SaveAsync();
        await workspace.DeleteTacticAsync(tactic.Id);
        byte[] saved = await File.ReadAllBytesAsync(workspace.PlayerDefinitionPath);
        await EditMilitaryWorld("b", d => d with { Officers = d.Officers.Select(o => o with { EquippedTacticId = tactic.Id }).ToArray() });
        await Assert.ThrowsAsync<InvalidDataException>(() => workspace.SaveAsync());
        Assert.Equal(saved, await File.ReadAllBytesAsync(workspace.PlayerDefinitionPath));
    }

    [Fact]
    public async Task MilitaryAllocationReservesScenarioPrivateRulesAndInternalRules()
    {
        await CreatePeopleAndTactics();
        await EditMilitaryWorld("private", d => d with { DefinitionOverrides = d.DefinitionOverrides with
        {
            Military = new(d.DefinitionOverrides.Military!.Value! with
            {
                Formations = [.. d.DefinitionOverrides.Military.Value!.Formations,
                    new(10000, 50, 0, 10000, 1, new(1, 1), TravelMode.Land)],
                Tactics = [.. d.DefinitionOverrides.Military.Value!.Tactics,
                    new(20000, 0, TacticEffect.Martial, 10, 5, 0, true, false, [])]
            })
        } });
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        Assert.True((await workspace.CreateFormationAsync()).Id > 10000);
        Assert.True((await workspace.CreateTacticAsync()).Id > 20000);
    }
}
