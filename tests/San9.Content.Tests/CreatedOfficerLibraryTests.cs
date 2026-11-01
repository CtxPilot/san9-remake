using System.Text.Json;
using System.Text.Json.Nodes;
using San9.Content.Authoring;
using San9.Content.Packages;

namespace San9.Content.Tests;

public sealed class CreatedOfficerLibraryTests
{
    [Fact]
    public void PortableLibraryPreservesLiteralNamesPortraitsAndIndependentTacticInputs()
    {
        var person = new CreatedOfficer(Guid.NewGuid(), "任意长度的自创人物姓名")
        {
            Surname = "诸葛",
            PortraitKey = "portrait-officer-42",
            Proficiencies = [1, 20, 300, 0, 0, 0, 0, 0, 0], MasteredCategories = [8], LearnedTactics = [1000],
            BirthYear = 160, AppearanceYear = 199, Abilities = new(125, 88, 73, 91), Biography = "自定列传"
        };
        var library = CreatedOfficerLibrary.Empty with { Officers = [person] };
        var decoded = Assert.Single(CreatedOfficerLibrary.Decode(library.Encode()).Officers);
        Assert.Equal(person.Name, decoded.Name); Assert.Equal(person.PortraitKey, decoded.PortraitKey);
        Assert.Equal(person.Surname + person.Name, decoded.FullName);
        Assert.False(JsonNode.Parse(library.Encode())!["officers"]![0]!.AsObject().ContainsKey("fullName"));
        Assert.Equal(person.Proficiencies.ToArray(), decoded.Proficiencies.ToArray()); Assert.Equal(new[] { 8 }, decoded.MasteredCategories.ToArray());
        Assert.Equal(new[] { 1000 }, decoded.LearnedTactics.ToArray()); Assert.Equal(199, decoded.AppearanceYear);
        Assert.Equal(person.Abilities, decoded.Abilities);
    }

    [Fact]
    public void ParentCyclesAndDuplicateIdentityAreRejectedWithoutRestrictingDuplicateNames()
    {
        var first = new CreatedOfficer(Guid.NewGuid(), "同名");
        var second = new CreatedOfficer(Guid.NewGuid(), "同名") { ParentId = first.Id };
        var library = CreatedOfficerLibrary.Empty with { Officers = [first, second] };
        library.Validate();
        Assert.Throws<InvalidDataException>(() => (library with { Officers = [first with { ParentId = second.Id }, second] }).Validate());
        Assert.Throws<InvalidDataException>(() => (library with { Officers = [first, first] }).Validate());
    }

    [Theory]
    [InlineData("birthYear")]
    [InlineData("appearanceYear")]
    [InlineData("portraitKey")]
    public void AuthoredFieldsAreRequiredRatherThanSilentlyRegenerated(string field)
    {
        var person = new CreatedOfficer(Guid.NewGuid(), "人物");
        var root = JsonNode.Parse(JsonSerializer.Serialize(person, PackageFiles.JsonOptions))!.AsObject(); root.Remove(field);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreatedOfficer>(root.ToJsonString(), PackageFiles.JsonOptions));
    }
}
