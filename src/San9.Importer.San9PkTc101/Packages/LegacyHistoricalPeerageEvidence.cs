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
            if (officer.InitialPersonalPeerage is not null || officer.CourtesyNameKey is not string courtesy ||
                !text.Entries.TryGetValue(officer.NameKey, out var name) || !text.Entries.TryGetValue(courtesy, out var courtesyName))
                return officer;
            PersonalPeerageEvidence? evidence = (officer.State.Id, name, courtesyName) switch
            {
                (95, "关羽", "云长") => new(new(PersonalPeerageGrade.VillageMarquis, "汉寿亭侯"),
                    new(201, 1, 1), new(218, 12, 30), PersonalPeerageEvidenceKind.Historical,
                    "《三国志·关羽传》记载受封：https://ctext.org/text.pl?if=gb&node=603537&show=parallel；" +
                    "建安五年事件后的完整年度至死亡年前一完整年度为保守适用范围，并非确切封爵日期。"),
                (376, "赵云", "子龙") => new(new(PersonalPeerageGrade.VillageMarquis, "永昌亭侯"),
                    new(224, 1, 1), new(228, 12, 30), PersonalPeerageEvidenceKind.Historical,
                    "《三国志·赵云传》记载建兴元年受封、建兴七年去世：https://ctext.org/text.pl?if=gb&node=603557&remap=gb；" +
                    "只采用受封次年至去世前一完整年度，不假定具体月份。"),
                _ => null
            };
            return evidence is null ? officer : officer with { InitialPersonalPeerage = evidence };
        }).ToArray();
        return scenario with { Officers = people };
    }
}
