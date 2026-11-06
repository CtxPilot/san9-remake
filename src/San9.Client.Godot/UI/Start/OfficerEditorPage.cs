using Godot;
using San9.Content.Scenarios;
using San9.Core.Domain;

namespace San9.Client.Godot;

public partial class OfficerEditorPage : SharedCatalogEditorPage
{
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
        OfficerEditorFields.Names(identity, p.Surname, p.GivenName, p.CourtesyName ?? "",
            Guard<string>(v => Edit(id, x => x with { Surname = v, Name = v + x.GivenName })),
            Guard<string>(v => Edit(id, x => x with { Name = x.Surname + v })),
            Guard<string>(v => Edit(id, x => x with { CourtesyName = string.IsNullOrWhiteSpace(v) ? null : v })),
            "OfficerGivenName", "OfficerCourtesyName");
        TextField("列传", p.Biography ?? "", v => Edit(id, x => x with { Biography = string.IsNullOrWhiteSpace(v) ? null : v }), true);
        OfficerEditorFields.Abilities(Form, new(p.War, p.Intelligence, p.Politics, p.Leadership),
            Guard<Func<OfficerAbilities, OfficerAbilities>>(change => Edit(id, x =>
            {
                var abilities = change(new(x.War, x.Intelligence, x.Politics, x.Leadership));
                return x with { War = abilities.War, Intelligence = abilities.Intelligence, Politics = abilities.Politics, Leadership = abilities.Leadership };
            })), 0);
        OfficerEditorFields.Personality(Form, p.Personality, p.Righteousness, p.Ambition, p.Affinity,
            Guard<int>(v => Edit(id, x => x with { Personality = v })),
            Guard<int>(v => Edit(id, x => x with { Righteousness = v })),
            Guard<int>(v => Edit(id, x => x with { Ambition = v })),
            Guard<int>(v => Edit(id, x => x with { Affinity = v })));
        var profile = Section("人物属性");
        Choice(profile, "性别", ["男", "女"], p.IsFemale ? 1 : 0, v => Edit(id, x => x with { IsFemale = v == 1 }));
        Number(profile, "旗字", p.FlagGlyph, -1, 185, v => Edit(id, x => x with { FlagGlyph = (int)v }));
        Number(profile, "寿命档", p.LifespanTier, 0, int.MaxValue, v => Edit(id, x => x with { LifespanTier = (int)v }));
        Number(profile, "登场年份", p.AppearanceYear, 0, int.MaxValue, v => Edit(id, x => x with { AppearanceYear = (int)v }));
        Toggle(Form, "按开局年龄确定出生年", p.AgeAtScenarioStart is not null, v =>
        { Edit(id, x => x with { AgeAtScenarioStart = v ? 20 : null, BirthYear = v ? 0 : 150 }); Rebuild(); });
        if (p.AgeAtScenarioStart is int age) Number(profile, "开局年龄差", age, 0, int.MaxValue, v => Edit(id, x => x with { AgeAtScenarioStart = (int)v }));
        else Number(profile, "出生年份", p.BirthYear, 0, int.MaxValue, v => Edit(id, x => x with { BirthYear = (int)v }));
        var family = Section("亲缘与继承");
        PersonReference(family, "亲代", p.ParentId, x => x.Id != id,
            value => Edit(id, x => x with { ParentId = value }), "ChooseParent");
        PersonReference(family, "血缘", p.BloodlineId, _ => true,
            value => Edit(id, x => x with { BloodlineId = value }), "ChooseBloodline");
        Number(family, "继承优先级", p.SuccessionPriority, 0, int.MaxValue, v => Edit(id, x => x with { SuccessionPriority = (int)v }));
        Number(family, "拥立优先级", p.ForceFounderPriority, 0, int.MaxValue, v => Edit(id, x => x with { ForceFounderPriority = (int)v }));
        var linked = Section("兵法加成");
        Number(linked, "连携加成", p.TacticLinkBonus, 0, 100, v => Edit(id, x => x with { TacticLinkBonus = (int)v }));
        var training = ContentEditorControls.Section(Form, "兵法");
        OfficerTrainingFields ReadTraining()
        {
            var current = Workspace.GetOfficer(id);
            return new(current.TacticProficiencies, current.MasteredCategories, current.LearnedTactics);
        }
        var tactics = Workspace.Tactics.Where(t => t.IsOfficerTactic)
            .Select(t => new OfficerTacticChoice(t.Id, Workspace.GetTacticCatalog(t.Id).Name, t.Category!.Value)).ToArray();
        OfficerEditorFields.Training(training, ReadTraining, Guard<OfficerTrainingFields>(value => Edit(id, x => x with
        { TacticProficiencies = value.Proficiencies, MasteredCategories = value.MasteredCategories, LearnedTactics = value.LearnedTactics })), tactics);
        var flags = Options("人物特性");
        Toggle(flags, "优先成为合并势力君主", p.PrefersConsolidationLeadership, v => Edit(id, x => x with { PrefersConsolidationLeadership = v }));
        Toggle(flags, "允许自动投降", p.AutomaticSurrenderAllowed, v => Edit(id, x => x with { AutomaticSurrenderAllowed = v }));
        Toggle(flags, "登用政治加成", p.HasRecruitmentPoliticsBonus, v => Edit(id, x => x with { HasRecruitmentPoliticsBonus = v }));
        foreach (var flag in Enum.GetValues<DuelAdvantages>().Where(f => f != 0 && ((int)f & ((int)f - 1)) == 0))
            Toggle(flags, DuelLabel(flag), p.DuelAdvantages.HasFlag(flag), v => Edit(id, x => x with
            { DuelAdvantages = v ? x.DuelAdvantages | flag : x.DuelAdvantages & ~flag }));
    }
    private void PersonReference(Node parent, string title, int? current, Func<OfficerDefaults, bool> eligible,
        Action<int?> changed, string name)
    {
        var candidates = new List<(string Label, int? Value)> { ("无" + title, null) };
        candidates.AddRange(Workspace.Officers.Where(eligible).Select(p => ($"{p.Name}（{p.Id}）", (int?)p.Id)));
        OfficerReferenceField.Add(parent, title, candidates, current, Guard(changed), name,
            title == "血缘" ? "选择家族血缘根人物；同族使用同一人物，可选本人。" : "按姓名搜索上一代人物。",
            $"人物 {current}（不在可选人物目录中）");
    }
    private static string DuelLabel(DuelAdvantages flag) => flag switch
    { DuelAdvantages.DominantParry => "单挑格挡优势", DuelAdvantages.DominantAttack => "单挑攻击优势",
        DuelAdvantages.DominantDoubleStrike => "单挑双击优势", _ => throw new ArgumentOutOfRangeException(nameof(flag)) };
}
