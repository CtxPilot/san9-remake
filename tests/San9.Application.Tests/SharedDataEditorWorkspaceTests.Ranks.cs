using System.Collections.Immutable;
using System.Text.Json;
using San9.Application.Authoring;
using San9.Content.Packages;
using San9.Content.Scenarios;
using San9.Core.Domain;

namespace San9.Application.Tests;

public sealed partial class SharedDataEditorWorkspaceTests
{
    private async Task CreateRanks()
    {
        await Create();
        var basis = await SharedDataEditorWorkspace.OpenAsync(_root);
        var definitions = basis.Definitions with { NextRankId = 8003, Information = basis.Definitions.Information with
        {
            Ranks = [new(8000, "无爵位", 10000, 0, 0, 0) { IsUnranked = true },
                new(8001, "王", 20000, 500, 5, 10), new(8002, "帝", 30000, 1000, 10, 20)]
        } };
        await File.WriteAllTextAsync(basis.DefinitionPath, JsonSerializer.Serialize(definitions, ScenarioContentFiles.DataJsonOptions));
    }

    [Fact]
    public async Task RankOrderingChangesSpecialRolesButKeepsTheInitialIdentityAndRoundTrips()
    {
        await CreateRanks();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        Assert.True(workspace.MoveRankTo(8001, 0));
        Assert.Equal(new[] { 8001, 8002, 8000 }, workspace.OrderedRanks.Select(r => r.Id));
        await workspace.SaveAsync();
        var loaded = await ScenarioContentFiles.LoadScenarioAsync(Path.Combine(_root, "b"));
        var rules = new PoliticalRulebook(null, loaded.Information.Ranks.Select(r => r.ToRule()), [], ImmutableDictionary<int, int>.Empty);
        Assert.Equal(8001, loaded.Information.ImperialRankId);
        Assert.Equal(8001, rules.ImperialRankId);
        Assert.Equal(8002, rules.ProclamationRankId);
        Assert.Equal(8000, rules.InitialRankId);
        Assert.Equal(8002, rules.NextRank(8000)!.Id);
        Assert.Equal(8001, rules.NextRank(8002)!.Id);
        Assert.Throws<InvalidDataException>(() => workspace.MoveRankTo(8000, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.MoveRankTo(8001, 2));
        Assert.Throws<InvalidDataException>(() => workspace.SetRank(workspace.GetRank(8001) with { IsUnranked = true }));
        var reopened = await SharedDataEditorWorkspace.OpenAsync(_root);
        Assert.False(reopened.HasChanges);
        Assert.Equal(new[] { 8001, 8002, 8000 }, reopened.OrderedRanks.Select(r => r.Id));
    }

    [Fact]
    public async Task RankCreationAndCopiesUseIndependentIdentitiesAndKeepUnrankedLast()
    {
        await CreateRanks();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var created = await workspace.CreateRankAsync();
        var copy = await workspace.CreateRankAsync(8002);
        var unrankedCopy = await workspace.CreateRankAsync(8000);
        Assert.Equal(new[] { 8002, copy.Id, 8001, created.Id, unrankedCopy.Id, 8000 }, workspace.OrderedRanks.Select(r => r.Id));
        Assert.False(unrankedCopy.IsUnranked);
        Assert.Single(workspace.Ranks, r => r.IsUnranked);
        Assert.True(workspace.IsCreatedRank(copy.Id));
        Assert.False(workspace.IsCreatedRank(8000));
        Assert.Equal(workspace.Ranks.Count, workspace.Ranks.Select(r => r.Precedence).Distinct().Count());
        await workspace.DeleteRankAsync(created.Id);
        await workspace.SaveAsync();
        var reopened = await SharedDataEditorWorkspace.OpenAsync(_root);
        Assert.True((await reopened.CreateRankAsync()).Id > unrankedCopy.Id);
        await Assert.ThrowsAsync<InvalidDataException>(() => reopened.DeleteRankAsync(8002));
        await Assert.ThrowsAsync<InvalidDataException>(() => reopened.DeleteRankAsync(8000));
    }

    [Fact]
    public async Task RankDeletionRejectsOfficeAndLateScenarioReferencesWithoutPublishing()
    {
        await CreateRanks();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var rank = await workspace.CreateRankAsync();
        var office = workspace.GetOffice(9000);
        workspace.SetOffice(office with { RequiredRankId = rank.Id });
        var draft = workspace.Definitions;
        await Assert.ThrowsAsync<InvalidDataException>(() => workspace.DeleteRankAsync(rank.Id));
        Assert.Same(draft, workspace.Definitions);
        workspace.SetOffice(office);
        await workspace.SaveAsync();
        await workspace.DeleteRankAsync(rank.Id);
        byte[] saved = await File.ReadAllBytesAsync(workspace.PlayerDefinitionPath);
        await EditMilitaryWorld("b", d => d with { Forces = [new(1, "force", 1, 100) { RankId = rank.Id }] });
        await Assert.ThrowsAsync<InvalidDataException>(() => workspace.SaveAsync());
        Assert.Equal(saved, await File.ReadAllBytesAsync(workspace.PlayerDefinitionPath));
    }

    [Fact]
    public async Task RankRestoreClearsShiftedMarkersAndPreservesIndependentAttributes()
    {
        await CreateRanks();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        workspace.MoveRankTo(8001, 0);
        workspace.RestoreRank(8001);
        Assert.False(workspace.HasChanges);
        workspace.SetRank(workspace.GetRank(8002) with { Name = "保留名称", CommandCapacity = 40000 });
        workspace.MoveRankTo(8001, 0);
        var copy = await workspace.CreateRankAsync(8002);
        workspace.RestoreRank(copy.Id);
        Assert.Equal(new[] { 8002, copy.Id, 8001, 8000 }, workspace.OrderedRanks.Select(r => r.Id));
        Assert.Equal("保留名称", workspace.GetRank(8002).Name);
        Assert.Equal(40000u, workspace.GetRank(8002).CommandCapacity);
    }

    [Fact]
    public async Task RankDeletionRetainsTwoFormalRanksEvenWhenThePlayerCatalogOmitsABaseItem()
    {
        await CreateRanks();
        var basis = await SharedDataEditorWorkspace.OpenAsync(_root);
        var definitions = basis.Definitions with { NextRankId = 9001, Information = basis.Definitions.Information with
            { Ranks = [basis.GetRank(8000), basis.GetRank(8001), new(9000, "自建帝", 30000, 1000, 5, 20)] } };
        await File.WriteAllTextAsync(basis.PlayerDefinitionPath, JsonSerializer.Serialize(definitions, ScenarioContentFiles.DataJsonOptions));
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => workspace.DeleteRankAsync(9000));
        Assert.Contains("至少两个", error.Message);
        Assert.False(workspace.HasChanges);
    }
}
