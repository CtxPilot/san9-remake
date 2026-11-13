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
            if (officer.InitialPersonalPeerage is not null || officer.InitialPersonalPeerageHistory is not null || officer.CourtesyNameKey is not string courtesy ||
                !text.Entries.TryGetValue(officer.NameKey, out var name) || !text.Entries.TryGetValue(courtesy, out var courtesyName))
                return officer;
            if (officer.State.Id == 119 && name == "魏延" && courtesyName == "文长")
            {
                const string source = "《三国志·魏延传》：https://ctext.org/text.pl?if=gb&node=603669；";
                return officer with { InitialPersonalPeerageHistory = [
                    new(new(PersonalPeerageGrade.VillageMarquis, "都亭侯"), new(224, 1, 1), new(229, 12, 30),
                        PersonalPeerageEvidenceKind.Historical, source + "建兴元年封都亭侯，取次年起至进封年前一完整年度。"),
                    new(new(PersonalPeerageGrade.CountyMarquis, "南郑侯"), new(231, 1, 1), new(233, 12, 30),
                        PersonalPeerageEvidenceKind.Historical, source + "建兴八年进封南郑侯；南郑为县见 https://ctext.org/wiki.pl?chapter=4004493&if=gb，取次年起至去世年前一完整年度。")
                ] };
            }
            if (officer.State.Id == 129 && name == "姜维" && courtesyName == "伯约")
            {
                const string source = "《三国志·姜维传》：https://ctext.org/text.pl?if=gb&node=603816；";
                return officer with { InitialPersonalPeerageHistory = [
                    new(new(PersonalPeerageGrade.VillageMarquis, "当阳亭侯"), new(229, 1, 1), new(233, 12, 30),
                        PersonalPeerageEvidenceKind.Historical, source + "建兴六年归蜀后封当阳亭侯，取次年至进封年前一完整年度。"),
                    new(new(PersonalPeerageGrade.CountyMarquis, "平襄侯"), new(235, 1, 1), new(242, 12, 30),
                        PersonalPeerageEvidenceKind.Historical, source + "建兴十二年进封平襄侯；平襄为县见 https://ctext.org/wiki.pl?chapter=248522，取保守完整年度。")
                ] };
            }
            if (officer.State.Id == 326 && name == "曹操" && courtesyName == "孟德")
            {
                const string source = "《三国志·武帝纪》：https://ctext.org/text.pl?if=gb&node=601875；";
                return officer with { InitialPersonalPeerageHistory = [
                    new(new(PersonalPeerageGrade.CountyMarquis, "武平侯"), new(197, 1, 1), new(212, 12, 30),
                        PersonalPeerageEvidenceKind.Historical, source + "建安元年封武平侯；武平为县见 https://tgaz.fudan.edu.cn/tgaz/placename/hvd_82102，取次年至进公年前。"),
                    new(new(PersonalPeerageGrade.Duke, "魏公"), new(214, 1, 1), new(215, 12, 30),
                        PersonalPeerageEvidenceKind.Historical, source + "建安十八年进魏公，取次年至进王年前。"),
                    new(new(PersonalPeerageGrade.King, "魏王"), new(217, 1, 1), new(219, 12, 30),
                        PersonalPeerageEvidenceKind.Historical, source + "建安二十一年进魏王，取次年至去世年前。")
                ] };
            }
            if (officer.State.Id == 422 && name == "张辽" && courtesyName == "文远")
            {
                const string source = "《三国志·张辽传》：https://ctext.org/text.pl?if=gb&node=602710&remap=gb；";
                return officer with { InitialPersonalPeerageHistory = [
                    new(new(PersonalPeerageGrade.ImperialMarquis, "关内侯"), new(199, 1, 1), new(200, 12, 30),
                        PersonalPeerageEvidenceKind.Historical, source + "下邳归曹后赐关内侯；仅取其后有把握的完整年度。"),
                    new(new(PersonalPeerageGrade.VillageMarquis, "都亭侯"), new(211, 1, 1), new(219, 12, 30),
                        PersonalPeerageEvidenceKind.Historical, source + "记载征讨荆州后封都亭侯，曹丕即位后又进爵；保守取上述完整年度。"),
                    new(new(PersonalPeerageGrade.CountyMarquis, "晋阳侯"), new(221, 1, 1), new(221, 12, 30),
                        PersonalPeerageEvidenceKind.Historical, source + "曹丕称帝后封晋阳侯，晋阳为县（https://ctext.org/wiki.pl?chapter=291343&if=gb）；仅取进封次年至去世年前的完整年度。")
                ] };
            }
            PersonalPeerageEvidence? evidence = (officer.State.Id, name, courtesyName) switch
            {
                (279, "徐晃", "公明") => new(new(PersonalPeerageGrade.VillageMarquis, "都亭侯"),
                    new(201, 1, 1), new(219, 12, 30), PersonalPeerageEvidenceKind.Historical,
                    "《三国志·徐晃传》记载封都亭侯及曹丕即王位后进封：https://ctext.org/text.pl?if=gb&node=602739；" +
                    "故市之战后至进封前仅取完整年度，不假定具体封爵日期。"),
                (95, "关羽", "云长") => new(new(PersonalPeerageGrade.VillageMarquis, "汉寿亭侯"),
                    new(201, 1, 1), new(218, 12, 30), PersonalPeerageEvidenceKind.Historical,
                    "《三国志·关羽传》记载受封：https://ctext.org/text.pl?if=gb&node=603537&show=parallel；" +
                    "建安五年事件后的完整年度至死亡年前一完整年度为保守适用范围，并非确切封爵日期。"),
                (376, "赵云", "子龙") => new(new(PersonalPeerageGrade.VillageMarquis, "永昌亭侯"),
                    new(224, 1, 1), new(228, 12, 30), PersonalPeerageEvidenceKind.Historical,
                    "《三国志·赵云传》记载建兴元年受封、建兴七年去世：https://ctext.org/text.pl?if=gb&node=603557&remap=gb；" +
                    "只采用受封次年至去世前一完整年度，不假定具体月份。"),
                (591, "李严", "正方") => new(new(PersonalPeerageGrade.DistrictMarquis, "都乡侯"),
                    new(224, 1, 1), new(230, 12, 30), PersonalPeerageEvidenceKind.Historical,
                    "《三国志·李严传》记载建兴元年受封、九年削爵：https://ctext.org/text.pl?if=gb&node=603661&remap=gb；" +
                    "只采用受封次年至削爵前一完整年度，不假定具体月份。"),
                _ => null
            };
            return evidence is null ? officer : officer with { InitialPersonalPeerage = evidence };
        }).ToArray();
        return scenario with { Officers = people };
    }
}
