using System.Collections.Immutable;
using Godot;
using San9.Content.Scenarios;
using San9.Core.Domain;

namespace San9.Client.Godot;

public partial class OfficerEditorPage : SharedCatalogEditorPage
{
    private static readonly string[] Categories = ["步兵", "骑兵", "弓骑", "弩兵", "水军", "攻城", "智略", "谋略", "策略"];
    protected override string Subject => "武将";
    protected override IEnumerable<(int Id, string Name)> Entries => Workspace.Officers.Select(p => (p.Id, p.Name));
    protected override bool Changed(int id) => Workspace.IsOfficerChanged(id);
    protected override void Restore(int id) => Workspace.RestoreOfficer(id);
    private void Edit(int id, Func<OfficerDefaults, OfficerDefaults> change) => Workspace.SetOfficer(change(Workspace.GetOfficer(id)));
    protected override void BuildForm(int id)
    {
        var p = Workspace.GetOfficer(id);
        var identity = new GridContainer { Columns = 6, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identity.AddThemeConstantOverride("h_separation", 12); Form.AddChild(identity);
        TextField(identity, "姓", p.Surname, v => Edit(id, x => x with { Surname = v, Name = v + x.GivenName }), "OfficerSurname");
        TextField(identity, "名", p.GivenName, v => Edit(id, x => x with { Name = x.Surname + v }), "OfficerGivenName");
        TextField(identity, "字", p.CourtesyName ?? "", v => Edit(id, x => x with
        { CourtesyName = string.IsNullOrWhiteSpace(v) ? null : v }), "OfficerCourtesyName");
        TextField("列传", p.Biography ?? "", v => Edit(id, x => x with { Biography = string.IsNullOrWhiteSpace(v) ? null : v }), true);
        var ability = Section("基础能力");
        Number(ability, "统率", p.Leadership, 0, int.MaxValue, v => Edit(id, x => x with { Leadership = (int)v }));
        Number(ability, "武力", p.War, 0, int.MaxValue, v => Edit(id, x => x with { War = (int)v }));
        Number(ability, "智力", p.Intelligence, 0, int.MaxValue, v => Edit(id, x => x with { Intelligence = (int)v }));
        Number(ability, "政治", p.Politics, 0, int.MaxValue, v => Edit(id, x => x with { Politics = (int)v }));
        var profile = Section("人物属性");
        Choice(profile, "性别", ["男", "女"], p.IsFemale ? 1 : 0, v => Edit(id, x => x with { IsFemale = v == 1 }));
        Number(profile, "旗字", p.FlagGlyph, -1, 185, v => Edit(id, x => x with { FlagGlyph = (int)v }));
        Choice(profile, "性格", ["莽撞", "刚胆", "冷静", "慎重"], p.Personality, v => Edit(id, x => x with { Personality = v }));
        Number(profile, "义理", p.Righteousness, 0, 15, v => Edit(id, x => x with { Righteousness = (int)v }));
        Number(profile, "野心", p.Ambition, 0, 15, v => Edit(id, x => x with { Ambition = (int)v }));
        Number(profile, "相性", p.Affinity, 0, 149, v => Edit(id, x => x with { Affinity = (int)v }));
        Number(profile, "寿命档", p.LifespanTier, 0, int.MaxValue, v => Edit(id, x => x with { LifespanTier = (int)v }));
        Number(profile, "登场年份", p.AppearanceYear, 0, int.MaxValue, v => Edit(id, x => x with { AppearanceYear = (int)v }));
        Toggle(Form, "按开局年龄确定出生年", p.AgeAtScenarioStart is not null, v =>
        { Edit(id, x => x with { AgeAtScenarioStart = v ? 20 : null, BirthYear = v ? 0 : 150 }); Rebuild(); });
        if (p.AgeAtScenarioStart is int age) Number(profile, "开局年龄差", age, 0, int.MaxValue, v => Edit(id, x => x with { AgeAtScenarioStart = (int)v }));
        else Number(profile, "出生年份", p.BirthYear, 0, int.MaxValue, v => Edit(id, x => x with { BirthYear = (int)v }));
        var family = Section("亲缘与继承");
        var parents = Workspace.Officers.Where(x => x.Id != id).ToArray();
        Choice(family, "亲代", new[] { "无" }.Concat(parents.Select(x => x.Name)).ToArray(),
            p.ParentId is int parent ? Array.FindIndex(parents, x => x.Id == parent) + 1 : 0,
            v => Edit(id, x => x with { ParentId = v == 0 ? null : parents[v - 1].Id }));
        Number(family, "血缘", p.BloodlineId ?? -1, -1, int.MaxValue, v => Edit(id, x => x with { BloodlineId = v < 0 ? null : (int)v }));
        Number(family, "继承优先级", p.SuccessionPriority, 0, int.MaxValue, v => Edit(id, x => x with { SuccessionPriority = (int)v }));
        Number(family, "拥立优先级", p.ForceFounderPriority, 0, int.MaxValue, v => Edit(id, x => x with { ForceFounderPriority = (int)v }));
        var proficiency = Section("兵法熟练");
        for (int category = 0; category < Categories.Length; category++)
        {
            int c = category;
            Number(proficiency, Categories[c], p.TacticProficiencies[c], 0, int.MaxValue,
                v => Edit(id, x => x with { TacticProficiencies = x.TacticProficiencies.SetItem(c, (int)v) }));
            Toggle(proficiency, "精通", p.MasteredCategories.Contains(c), v => Edit(id, x => x with
            { MasteredCategories = v ? x.MasteredCategories.Append(c).Distinct().Order().ToImmutableArray() : x.MasteredCategories.Where(a => a != c).ToImmutableArray() }));
            proficiency.AddChild(new Control());
        }
        Number(proficiency, "连携加成", p.TacticLinkBonus, 0, 100, v => Edit(id, x => x with { TacticLinkBonus = (int)v }));
        var learned = Section("已学兵法");
        foreach (var tactic in Workspace.Tactics.Where(t => t.IsOfficerTactic))
        {
            int t = tactic.Id;
            Toggle(learned, Workspace.GetTacticCatalog(t).Name, p.LearnedTactics.Contains(t), v => Edit(id, x => x with
            { LearnedTactics = v ? x.LearnedTactics.Add(t) : x.LearnedTactics.Remove(t) }));
        }
        var flags = Section("人物特性");
        Toggle(flags, "优先成为合并势力君主", p.PrefersConsolidationLeadership, v => Edit(id, x => x with { PrefersConsolidationLeadership = v }));
        Toggle(flags, "允许自动投降", p.AutomaticSurrenderAllowed, v => Edit(id, x => x with { AutomaticSurrenderAllowed = v }));
        Toggle(flags, "登用政治加成", p.HasRecruitmentPoliticsBonus, v => Edit(id, x => x with { HasRecruitmentPoliticsBonus = v }));
        foreach (var flag in Enum.GetValues<DuelAdvantages>().Where(f => f != 0 && ((int)f & ((int)f - 1)) == 0))
            Toggle(flags, DuelLabel(flag), p.DuelAdvantages.HasFlag(flag), v => Edit(id, x => x with
            { DuelAdvantages = v ? x.DuelAdvantages | flag : x.DuelAdvantages & ~flag }));
    }
    private static string DuelLabel(DuelAdvantages flag) => flag switch
    { DuelAdvantages.DominantParry => "单挑格挡优势", DuelAdvantages.DominantAttack => "单挑攻击优势",
        DuelAdvantages.DominantDoubleStrike => "单挑双击优势", _ => throw new ArgumentOutOfRangeException(nameof(flag)) };
}
