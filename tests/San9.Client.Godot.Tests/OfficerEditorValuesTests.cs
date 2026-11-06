using San9.Content.Scenarios;
using San9.Content.Text;
using San9.Core.Domain;

namespace San9.Client.Godot.Tests;

public sealed class OfficerEditorValuesTests
{
    private static OfficerDefaults Person() => OfficerDefaults.From(
        new("person", new(42, null, null, new(50, 60, 70, 80), false) { Surname = "姓" }),
        new(TextCatalog.CurrentSchemaVersion, new() { ["person"] = "独立显示名" }));

    [Fact]
    public void ChangingATraitPreservesUneditedTextAndDisplayIdentity()
    {
        var person = Person();
        var edited = (OfficerEditorValues.From(person) with { SuccessionPriority = 9 }).Apply(person);
        Assert.Equal(person with { SuccessionPriority = 9 }, edited);
    }

    [Fact]
    public void EditingNamesAndClearingTextChangesOnlyTheRequestedFields()
    {
        var person = Person() with { CourtesyName = "旧字", Biography = "旧列传" };
        var edited = (OfficerEditorValues.From(person) with
        { GivenName = "新名", CourtesyName = "", Biography = " " }).Apply(person);
        Assert.Equal(person with { Name = "姓新名", CourtesyName = null, Biography = null }, edited);
    }
}
