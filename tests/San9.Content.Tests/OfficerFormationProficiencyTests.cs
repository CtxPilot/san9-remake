using System.Collections.Immutable;
using System.Text.Json;
using San9.Content.Packages;
using San9.Content.Scenarios;

namespace San9.Content.Tests;

public sealed class OfficerFormationProficiencyTests
{
    [Fact]
    public void FormationKnowledgeSurvivesProfileOverridesAndJson()
    {
        var legacy = OfficerDefaults.Create(1);
        var authored = legacy with { FormationProficiencies = ImmutableSortedDictionary<int, int>.Empty.Add(7, 0).Add(28, 750) };
        var overrides = OfficerDefaultsOverrides.Between(legacy, authored);
        var json = JsonSerializer.Serialize(overrides, PackageFiles.JsonOptions);
        var restored = JsonSerializer.Deserialize<OfficerDefaultsOverrides>(json, PackageFiles.JsonOptions)!.Apply(legacy);
        Assert.NotNull(restored.FormationProficiencies);
        Assert.Equal(0, restored.FormationProficiencies[7]);
        Assert.Equal(750, restored.FormationProficiencies[28]);
        Assert.Null(legacy.FormationProficiencies);
    }
}
