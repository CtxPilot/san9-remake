using San9.Application.Authoring;
using San9.Content.Authoring;

namespace San9.Application.Tests;

public sealed class CreatedOfficerTests
{
    [Theory]
    [InlineData("司马", 66)]
    [InlineData("\U00020000甲", 77)]
    [InlineData("\U00020001", -1)]
    [InlineData("陌", -1)]
    [InlineData("", -1)]
    public void SurnameFlagsChangeOnlyOnExplicitEditorAssignment(string surname, int expected)
    {
        var person = Person() with { Surname = "新", FlagGlyph = 42 };
        var workspace = new CreatedOfficerWorkspace(CreatedOfficerLibrary.Empty with { Officers = [person] });
        workspace.UseFlagCatalog(new(OfficerFlagCatalog.CurrentSchemaVersion, new() { ["司"] = 66, ["𠀀"] = 77 }, [66, 77]));
        Assert.False(workspace.HasChanges); Assert.Equal(42, workspace.Library.Officers[0].FlagGlyph);
        workspace.Set(person with { Surname = surname });
        Assert.Equal(42, workspace.Library.Officers[0].FlagGlyph);
        workspace.AssignFlagFromSurname(person.Id);
        var automatic = workspace.Library.Officers[0];
        Assert.Equal(expected, automatic.FlagGlyph);
        workspace.Set(automatic with { FlagGlyph = -1 });
        workspace.Set(workspace.Library.Officers[0] with { Surname = "司徒" });
        Assert.Equal(-1, workspace.Library.Officers[0].FlagGlyph);
        workspace.AssignFlagFromSurname(person.Id);
        Assert.Equal(66, workspace.Library.Officers[0].FlagGlyph);
        var copy = workspace.Duplicate(person.Id);
        Assert.Equal(66, copy.FlagGlyph);
        Assert.Equal(66, CreatedOfficerLibrary.Decode(workspace.Library.Encode()).Officers[0].FlagGlyph);
    }

    [Fact]
    public void MissingFlagCatalogRejectsAssignmentButAllowsSurnameEditingWithoutChangingTheFlag()
    {
        var person = Person() with { Surname = "新" };
        var workspace = new CreatedOfficerWorkspace(CreatedOfficerLibrary.Empty with { Officers = [person] });
        Assert.Throws<InvalidOperationException>(() => workspace.AssignFlagFromSurname(person.Id));
        Assert.Same(person, workspace.Library.Officers[0]); Assert.False(workspace.HasChanges);
        workspace.Set(person with { Surname = "司马" });
        Assert.Equal(42, workspace.Library.Officers[0].FlagGlyph);
        Assert.False(workspace.FinishSurnameEdit(person.Id, automatic: true));
        Assert.Equal("司马", workspace.Library.Officers[0].Surname);
        Assert.Equal(42, CreatedOfficerLibrary.Decode(workspace.Library.Encode()).Officers[0].FlagGlyph);
        workspace.UseFlagCatalog(new(OfficerFlagCatalog.CurrentSchemaVersion, new() { ["司"] = 66 }, [66]));
        Assert.False(workspace.FinishSurnameEdits(automatic: true));
        Assert.Equal(42, workspace.Library.Officers[0].FlagGlyph);
        workspace.Set(workspace.Library.Officers[0] with { Surname = "司徒" });
        Assert.True(workspace.FinishSurnameEdit(person.Id, automatic: true));
        Assert.Equal(66, workspace.Library.Officers[0].FlagGlyph);
    }

    [Theory]
    [InlineData(true, 66)]
    [InlineData(false, 42)]
    public void FinishingSurnameEditsUsesTheCurrentPreferenceAndCannotOverrideALaterManualChoice(bool automatic, int expected)
    {
        var person = Person() with { Surname = "新" };
        var workspace = new CreatedOfficerWorkspace(CreatedOfficerLibrary.Empty with { Officers = [person] });
        workspace.UseFlagCatalog(new(OfficerFlagCatalog.CurrentSchemaVersion, new() { ["司"] = 66 }, [66]));
        workspace.Set(person with { Surname = "司" });
        workspace.Set(workspace.Library.Officers[0] with { Surname = "司马" });
        Assert.Equal(42, workspace.Library.Officers[0].FlagGlyph);
        workspace.FinishSurnameEdits(automatic);
        Assert.Equal(expected, workspace.Library.Officers[0].FlagGlyph);

        workspace.Set(workspace.Library.Officers[0] with { FlagGlyph = -1 });
        Assert.False(workspace.FinishSurnameEdit(person.Id, automatic: true));
        workspace.Set(workspace.Library.Officers[0] with { Surname = "刘" });
        workspace.Set(workspace.Library.Officers[0] with { Surname = "司马" });
        Assert.False(workspace.FinishSurnameEdits(automatic: true));
        Assert.Equal(-1, workspace.Duplicate(person.Id).FlagGlyph);
    }

    [Fact]
    public void ImportDiscardsSupersededSurnameEditsBeforeLateControlCallbacks()
    {
        var person = Person() with { Surname = "新" };
        var workspace = new CreatedOfficerWorkspace(CreatedOfficerLibrary.Empty with { Officers = [person] });
        workspace.UseFlagCatalog(new(OfficerFlagCatalog.CurrentSchemaVersion, new() { ["刘"] = 163 }, [163]));
        workspace.Set(person with { Surname = "司马" });
        workspace.Import(CreatedOfficerLibrary.Empty with { Officers = [person with { Surname = "刘", FlagGlyph = 77 }] }, createCopies: false);
        Assert.False(workspace.FinishSurnameEdit(person.Id, automatic: true));
        Assert.Equal(77, workspace.Library.Officers[0].FlagGlyph);
        workspace.Delete(person.Id);
        Assert.False(workspace.FinishSurnameEdit(person.Id, automatic: true));
    }

    private static CreatedOfficer Person(string name = "自创人物") => new(Guid.NewGuid(), name)
    {
        PortraitKey = "portrait-42", FlagGlyph = 42,
        Abilities = new(125, 76, 85, 94), BirthYear = 160, AppearanceYear = 181,
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
