using System.Text.Json;
using System.Text.Json.Nodes;
using San9.Content.Packages;
using San9.Content.Scenarios;

namespace San9.Content.Tests;

public sealed class OfficerAgeDisplayTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AgeVisibilitySurvivesCopyAndJsonAndIsRequired(bool showAges)
    {
        var information = InformationDefinitions.Empty with { ShowOfficerAges = showAges };
        Assert.Equal(showAges, information.Copy().ShowOfficerAges);
        var json = JsonSerializer.Serialize(information, ScenarioContentFiles.DataJsonOptions);
        var restored = JsonSerializer.Deserialize<InformationDefinitions>(json, ScenarioContentFiles.DataJsonOptions)!;
        Assert.Equal(showAges, restored.ShowOfficerAges);
        var node = JsonNode.Parse(json)!.AsObject();
        Assert.True(node.Remove("showOfficerAges"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InformationDefinitions>(node.ToJsonString(),
            ScenarioContentFiles.DataJsonOptions));
    }
}
