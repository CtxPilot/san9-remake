using San9.Content.Authoring;

namespace San9.Client.Godot.Tests;

public sealed class OpeningOfficerCatalogTests
{
    [Fact]
    public void SummaryAndTacticsPagesKeepLearningMasteryAndProficiencyIndependent()
    {
        var person = new CreatedOfficer(Guid.NewGuid(), "甲")
        {
            Surname = "新", Abilities = new(81, 82, 83, 84), LearnedTactics = [23, 27],
            MasteredCategories = [7], Proficiencies = [10, 20, 30, 40, 50, 60, 70, 80, 90]
        };
        Assert.Equal("新甲", OpeningOfficerCatalog.Cell(person, "name").Text);
        Assert.Equal(2, OpeningOfficerCatalog.Cell(person, "learned").Number);
        Assert.Equal(80, OpeningOfficerCatalog.Cell(person, "proficiency.7").Number);
        Assert.Equal("☆", OpeningOfficerCatalog.Cell(person, "mastery.7").Text);
        Assert.Equal("○", OpeningOfficerCatalog.Cell(person, "tactic.23").Text);
        Assert.Equal("", OpeningOfficerCatalog.Cell(person, "tactic.24").Text);
        Assert.Equal("", OpeningOfficerCatalog.Cell(person, "mastery.8").Text);
        Assert.Equal("○", OpeningOfficerCatalog.Cell(person, "tactic.27").Text);
    }
}
