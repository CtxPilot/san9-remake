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

    [Fact]
    public void ZhaoYunReceivesOnlyTheFullySupportedYearsAndCorrectGrade()
    {
        var person = new OfficerInitialState("name", new OfficerState(376, 1, 1, new(50, 50, 50, 50), false)) { CourtesyNameKey = "courtesy" };
        var scenario = new ScenarioInitialState(ScenarioInitialState.CurrentSchemaVersion, "fixture", 225, 1, 1, null, [], [], [], []) { Officers = [person] };
        var texts = new TextCatalog(TextCatalog.CurrentSchemaVersion, new() { ["name"] = "赵云", ["courtesy"] = "子龙" });
        var evidence = LegacyHistoricalPeerageEvidence.Apply(scenario, texts, true).Officers[0].InitialPersonalPeerage!;
        Assert.Equal(new PersonalPeerage(PersonalPeerageGrade.VillageMarquis, "永昌亭侯"), evidence.Peerage);
        Assert.False(evidence.Applies(new(223, 12, 30)));
        Assert.True(evidence.Applies(new(224, 1, 1)));
        Assert.True(evidence.Applies(new(228, 12, 30)));
        Assert.False(evidence.Applies(new(229, 1, 1)));
        Assert.Null(LegacyHistoricalPeerageEvidence.Apply(scenario, texts, false).Officers[0].InitialPersonalPeerage);
    }
}
