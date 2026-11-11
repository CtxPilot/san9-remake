using San9.Content.Scenarios;
using San9.Content.Text;
using San9.Core.Domain;
using San9.Importer.San9PkTc101.Packages;

namespace San9.Importer.San9PkTc101.Tests;

public sealed class LegacyHistoricalPeerageEvidenceTests
{
    [Fact]
    public void KnownLegacyIdentityGetsSourcedConservativeDatesOnlyInHistoricalScenarios()
    {
        var person = new OfficerInitialState("name", new OfficerState(95, 1, 1, new(50, 50, 50, 50), false)) { CourtesyNameKey = "courtesy" };
        var scenario = new ScenarioInitialState(ScenarioInitialState.CurrentSchemaVersion, "fixture", 211, 1, 1, null, [], [], [], []) { Officers = [person] };
        var texts = new TextCatalog(TextCatalog.CurrentSchemaVersion, new() { ["name"] = "关羽", ["courtesy"] = "云长" });
        var record = LegacyHistoricalPeerageEvidence.Apply(scenario, texts, true).Officers[0].InitialPersonalPeerage!;
        Assert.Equal("汉寿亭侯", record.Peerage.Title);
        Assert.Equal(PersonalPeerageEvidenceKind.Historical, record.Kind);
        Assert.Equal(201, record.From.Year);
        Assert.Equal(218, record.Until!.Value.Year);
        Assert.Contains("ctext.org", record.Source);
        Assert.Null(LegacyHistoricalPeerageEvidence.Apply(scenario, texts, false).Officers[0].InitialPersonalPeerage);
        var other = scenario with { Officers = [person with { State = person.State with { Id = 96 } }] };
        Assert.Null(LegacyHistoricalPeerageEvidence.Apply(other, texts, true).Officers[0].InitialPersonalPeerage);
        texts.Entries["courtesy"] = "他字";
        Assert.Null(LegacyHistoricalPeerageEvidence.Apply(scenario, texts, true).Officers[0].InitialPersonalPeerage);
    }
}
