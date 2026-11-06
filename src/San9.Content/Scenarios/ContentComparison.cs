using System.Text.Json;
using San9.Content.Packages;

namespace San9.Content.Scenarios;

/// <summary>Structural comparison for authoring differences, independent of dictionary insertion order.</summary>
public static class ContentComparison
{
    public static bool Equal<T>(T first, T second) => JsonElement.DeepEquals(
        JsonSerializer.SerializeToElement(first, ScenarioContentFiles.DataJsonOptions),
        JsonSerializer.SerializeToElement(second, ScenarioContentFiles.DataJsonOptions));
}
