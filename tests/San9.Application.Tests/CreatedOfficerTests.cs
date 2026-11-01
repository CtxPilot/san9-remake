using San9.Application.Authoring;
using San9.Content.Authoring;

namespace San9.Application.Tests;

public sealed class CreatedOfficerTests
{
    private static CreatedOfficer Person(string name = "自创人物") => new(Guid.NewGuid(), name)
    {
        PortraitKey = "portrait-42", Abilities = new(125, 76, 85, 94), BirthYear = 160, AppearanceYear = 181,
        Proficiencies = [42, 0, 0, 0, 0, 0, 0, 0, 0], MasteredCategories = [3]
    };

    [Fact]
    public void WorkspaceCopiesAndImportsKeepRelationshipsAndRequireAnExplicitIdentityDecision()
    {
        var parent = Person("同名"); var child = Person("同名") with { ParentId = parent.Id };
        var library = CreatedOfficerLibrary.Empty with { Officers = [parent, child] };
        var workspace = new CreatedOfficerWorkspace(library);
        Assert.Throws<InvalidDataException>(() => workspace.Delete(parent.Id));
        workspace.Import(library, createCopies: true);
        var importedChild = Assert.Single(workspace.Library.Officers, p => p.Id != child.Id && p.ParentId is not null);
        Assert.NotEqual(parent.Id, importedChild.ParentId);
        Assert.Contains(workspace.Library.Officers, p => p.Id == importedChild.ParentId);
        workspace.Import(library with { Officers = [parent with { Name = "更新" }, child] }, createCopies: false);
        Assert.Equal("更新", workspace.Library.Officers.Single(p => p.Id == parent.Id).Name);
    }

    [Fact]
    public void DraftEditsAndCopiesPreserveExplicitInputsUntilTheHostSaves()
    {
        var workspace = new CreatedOfficerWorkspace(CreatedOfficerLibrary.Empty);
        var person = workspace.Add() with { PortraitKey = "portrait-42", BirthYear = 150, AppearanceYear = 187, LearnedTactics = [42] };
        workspace.Set(person);
        Assert.True(workspace.HasChanges);
        workspace.Saved();
        Assert.False(workspace.HasChanges);
        var copy = workspace.Duplicate(person.Id);
        Assert.NotEqual(person.Id, copy.Id);
        Assert.Equal(person, copy with { Id = person.Id });
        Assert.True(workspace.HasChanges);
        workspace.Delete(copy.Id);
        Assert.Equal(person, Assert.Single(workspace.Library.Officers));
    }

    [Fact]
    public void InvalidImportLeavesTheCurrentDraftAndSavedFlagIntact()
    {
        var person = Person();
        var library = CreatedOfficerLibrary.Empty with { Officers = [person] };
        var workspace = new CreatedOfficerWorkspace(library);
        var invalid = CreatedOfficerLibrary.Empty with { Officers = [Person() with { ParentId = Guid.NewGuid() }] };
        Assert.Throws<InvalidDataException>(() => workspace.Import(invalid, createCopies: false));
        Assert.Same(library, workspace.Library);
        Assert.False(workspace.HasChanges);
    }

    [Fact]
    public void ImportUpdatesInPlaceAndAppendsNewPeopleInAuthorOrder()
    {
        var first = Person("第一位") with { Id = Guid.Parse("ffffffff-0000-0000-0000-000000000000") };
        var second = Person("第二位") with { Id = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000000") };
        var incoming = Person("第三位") with { Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000") };
        var workspace = new CreatedOfficerWorkspace(CreatedOfficerLibrary.Empty with { Officers = [first, second] });
        workspace.Import(CreatedOfficerLibrary.Empty with { Officers = [incoming, first with { Name = "已更新" }] }, createCopies: false);
        Assert.Equal(new[] { first.Id, second.Id, incoming.Id }, workspace.Library.Officers.Select(p => p.Id).ToArray());
        Assert.Equal("已更新", workspace.Library.Officers[0].Name);
    }
}
