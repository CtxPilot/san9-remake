using San9.Content.Scenarios;
using San9.Content.Text;
using San9.Core.Domain;
using San9.Importer.San9PkTc101.Scenarios;

namespace San9.Importer.San9PkTc101.Tests;

public sealed class CuratedFormationEvidenceTests
{
    [Fact]
    public void RecordsRequireVerifiedIdentityDateAndDoNotOverwriteAuthoredValues()
    {
        var officer = new OfficerInitialState("name", new OfficerState(278, null, null, new(50, 50, 50, 50), false))
        { CourtesyNameKey = "courtesy" };
        var scenario = new ScenarioInitialState(ScenarioInitialState.CurrentSchemaVersion, "fixture", 214, 1, 1, null, [], [], [], [])
        {
            Officers = [officer], Military = new(1, 1, [0], [new(0, 1, null)],
                [new(28, 1, 0, 10000, 5, new(1, 1), TravelMode.Land)], [])
        };
        var texts = new TextCatalog(TextCatalog.CurrentSchemaVersion, new() { ["name"] = "诸葛亮", ["courtesy"] = "孔明" });
        var evidence = Assert.Single(CuratedFormationEvidence.Apply(scenario, texts).Officers[0].InitialFormationEvidence!);
        Assert.Equal(FormationEvidenceKind.ExperienceInference, evidence.Kind);
        Assert.Equal(28, evidence.FormationId);
        Assert.Contains("不能认定", evidence.Explanation);
        Assert.Null(CuratedFormationEvidence.Apply(scenario with { Year = 213 }, texts).Officers[0].InitialFormationEvidence);
        texts.Entries["courtesy"] = "他字";
        Assert.Null(CuratedFormationEvidence.Apply(scenario, texts).Officers[0].InitialFormationEvidence);
        texts.Entries["courtesy"] = "孔明";
        var authored = officer with { State = officer.State with { FormationProficiencies = System.Collections.Immutable.ImmutableSortedDictionary<int, int>.Empty } };
        Assert.Null(CuratedFormationEvidence.Apply(scenario with { Officers = [authored] }, texts).Officers[0].InitialFormationEvidence);
    }
}
