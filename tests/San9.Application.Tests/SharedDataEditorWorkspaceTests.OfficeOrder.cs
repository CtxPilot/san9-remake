using San9.Application.Authoring;
using San9.Content.Packages;

namespace San9.Application.Tests;

public sealed partial class SharedDataEditorWorkspaceTests
{
    [Fact]
    public async Task RestoringADraggedOfficeRestoresEveryShiftedPriorityWithoutDiscardingOtherFields()
    {
        await Create();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var second = await workspace.CreateOfficeAsync();
        var third = await workspace.CreateOfficeAsync();
        await workspace.SaveAsync();
        workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var baseline = workspace.OrderedOffices.ToArray();
        workspace.MoveOfficeTo(third.Id, 0);
        workspace.RestoreOffice(third.Id);
        Assert.Equal(baseline, workspace.OrderedOffices);
        Assert.False(workspace.HasChanges);
        Assert.All(workspace.Offices, o => Assert.False(workspace.IsOfficeChanged(o.Id)));

        workspace.SetOffice(workspace.GetOffice(second.Id) with { Salary = 81, Name = "保留编辑" });
        workspace.MoveOfficeTo(third.Id, 0);
        workspace.RestoreOffice(third.Id);
        Assert.Equal(baseline.Select(o => o.Id), workspace.OrderedOffices.Select(o => o.Id));
        Assert.Equal(baseline.Select(o => o.GovernorPriority), workspace.OrderedOffices.Select(o => o.GovernorPriority));
        Assert.Equal(81, workspace.GetOffice(second.Id).Salary);
        Assert.Equal("保留编辑", workspace.GetOffice(second.Id).Name);
        Assert.Equal(new[] { second.Id }, workspace.Offices.Where(o => workspace.IsOfficeChanged(o.Id)).Select(o => o.Id));
    }

    [Fact]
    public async Task RestoredOrderExtendsOpeningOrderWithCopiesInsteadOfCapturingDraftDrags()
    {
        await Create();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var second = await workspace.CreateOfficeAsync();
        var third = await workspace.CreateOfficeAsync();
        await workspace.SaveAsync();
        workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        workspace.MoveOfficeTo(third.Id, 0);
        var copy = await workspace.CreateOfficeAsync(9000);
        workspace.MoveOfficeTo(copy.Id, 0);
        long watermark = workspace.Definitions.NextOfficeId;
        workspace.RestoreOffice(copy.Id);
        Assert.Equal(new[] { 9000, copy.Id, second.Id, third.Id }, workspace.OrderedOffices.Select(o => o.Id));
        Assert.Equal(watermark, workspace.Definitions.NextOfficeId);
        await workspace.DeleteOfficeAsync(second.Id);
        workspace.RestoreOffice(third.Id);
        Assert.Equal(new[] { 9000, copy.Id, third.Id }, workspace.OrderedOffices.Select(o => o.Id));
    }

    [Fact]
    public async Task MovingOfficesSwapsExistingPriorityValuesAndPreservesGaps()
    {
        await Create();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var second = await workspace.CreateOfficeAsync();
        var third = await workspace.CreateOfficeAsync();
        workspace.SetOffice(workspace.GetOffice(9000) with { GovernorPriority = 100 });
        workspace.SetOffice(second with { GovernorPriority = 50 });
        workspace.SetOffice(third with { GovernorPriority = 10 });
        Assert.True(workspace.MoveOfficeTo(second.Id, 0));
        Assert.Equal(new[] { second.Id, 9000, third.Id }, workspace.OrderedOffices.Select(o => o.Id));
        Assert.Equal(new[] { 100, 50, 10 }, workspace.OrderedOffices.Select(o => o.GovernorPriority));
        var draft = workspace.Definitions;
        Assert.False(workspace.MoveOfficeTo(second.Id, 0));
        Assert.Same(draft, workspace.Definitions);
        Assert.True(workspace.MoveOfficeTo(second.Id, 1));
        Assert.Equal(new[] { 9000, second.Id, third.Id }, workspace.OrderedOffices.Select(o => o.Id));
        await workspace.SaveAsync();
        var reopened = await SharedDataEditorWorkspace.OpenAsync(_root);
        Assert.Equal(new[] { 9000, second.Id, third.Id }, reopened.OrderedOffices.Select(o => o.Id));
        Assert.Equal(new[] { 100, 50, 10 }, reopened.OrderedOffices.Select(o => o.GovernorPriority));
    }

    [Fact]
    public async Task CopyInsertsAfterItsSourceAndNewOfficeAppendsWithoutRenumberingWhenSpaceExists()
    {
        await Create();
        var workspace = await SharedDataEditorWorkspace.OpenAsync(_root);
        var lower = await workspace.CreateOfficeAsync();
        workspace.SetOffice(workspace.GetOffice(9000) with { GovernorPriority = 100 });
        workspace.SetOffice(lower with { GovernorPriority = 50 });
        var copy = await workspace.CreateOfficeAsync(9000);
        var created = await workspace.CreateOfficeAsync();
        Assert.Equal(new[] { 9000, copy.Id, lower.Id, created.Id }, workspace.OrderedOffices.Select(o => o.Id));
        Assert.Equal(new[] { 100, 99, 50, 49 }, workspace.OrderedOffices.Select(o => o.GovernorPriority));
        workspace.SetOffice(copy with { Salary = 12 });
        workspace.RestoreOffice(copy.Id);
        Assert.Equal(new[] { 9000, copy.Id, lower.Id, created.Id }, workspace.OrderedOffices.Select(o => o.Id));
        Assert.Equal(copy.Salary, workspace.GetOffice(copy.Id).Salary);
    }

}
