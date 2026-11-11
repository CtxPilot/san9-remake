using San9.Content.Scenarios;
using San9.Content.Text;
using San9.Core.Domain;

namespace San9.Importer.San9PkTc101.Packages;

/// <summary>Evidence tied to an identified legacy source person; never infer identities in generic content.</summary>
public static class LegacyHistoricalPeerageEvidence
{
    public static ScenarioInitialState Apply(ScenarioInitialState scenario, TextCatalog text, bool historical)
    {
        if (!historical) return scenario;
        var people = scenario.Officers.Select(officer =>
        {
            // Source-format identity plus both independently stored text fields must match.
            if (officer.State.Id != 95 || officer.InitialPersonalPeerage is not null ||
                !text.Entries.TryGetValue(officer.NameKey, out var name) || name != "关羽" ||
                officer.CourtesyNameKey is not string courtesy || !text.Entries.TryGetValue(courtesy, out var courtesyName) || courtesyName != "云长")
                return officer;
            return officer with { InitialPersonalPeerage = new PersonalPeerageEvidence(
                new(PersonalPeerageGrade.VillageMarquis, "汉寿亭侯"), new(201, 1, 1), new(218, 12, 30),
                PersonalPeerageEvidenceKind.Historical,
                "《三国志·关羽传》记载受封：https://ctext.org/text.pl?if=gb&node=603537&show=parallel；" +
                "建安五年事件后的完整年度至死亡年前一完整年度为保守适用范围，并非确切封爵日期。") };
        }).ToArray();
        return scenario with { Officers = people };
    }
}
