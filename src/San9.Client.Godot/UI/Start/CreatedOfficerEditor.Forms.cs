using Godot;
using San9.Content.Authoring;
using San9.Content.Scenarios;
using San9.Core.Domain;

namespace San9.Client.Godot;

public partial class CreatedOfficerEditor
{
    private VBoxContainer Page(string title)
    {
        var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _tabs.AddChild(scroll);
        return ContentEditorControls.PageBody(scroll);
    }
    private static void Caption(Node parent, string text) => ContentEditorControls.Caption(parent, text);
    private static VBoxContainer Section(Node parent, string title, Action<HBoxContainer>? actions = null) =>
        ContentEditorControls.Section(parent, title, actions);
    private static GridContainer ProfileGrid(Node parent, string title, Action<HBoxContainer>? actions = null) =>
        ContentEditorControls.ProfileGrid(parent, title, actions);
    private static SpinBox Number(Node parent, string label, int value, int minimum, int maximum, Action<int> changed, string name = "")
    {
        Caption(parent, label);
        return ContentEditorControls.NumericInput(parent, value, minimum, maximum, next => changed((int)next), name);
    }
    private static OptionButton Choices(Node parent, string label, string[] values, int selected, Action<int> changed, string name = "") =>
        ContentEditorControls.Choices(parent, label, values, selected, changed, name);
    private void BuildProfile(CreatedOfficer person)
    {
        var page = Page("基本资料");
        page.AddThemeConstantOverride("separation", 12);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 18); page.AddChild(row);
        var avatar = Section(row, "头像"); avatar.CustomMinimumSize = new(160, 0); avatar.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        var portraitFrame = new PanelContainer();
        portraitFrame.AddThemeStyleboxOverride("panel", ContentEditorStyle.Surface("151c24", "465260", 6)); avatar.AddChild(portraitFrame);
        portraitFrame.AddChild(PortraitPreview(person.PortraitKey, new(148, 148)));
        var portraitActions = new HBoxContainer(); portraitActions.AddThemeConstantOverride("separation", 6); avatar.AddChild(portraitActions);
        var choose = AddButton(portraitActions, "选择", ChoosePortrait, "ChoosePortrait");
        choose.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        choose.Disabled = _portraitService is null || _portraitService.Portraits.Count == 0;
        if (choose.Disabled) choose.TooltipText = "请先准备游戏的转换头像资源。";
        AddButton(portraitActions, "清除", () => { Change(person.Id, p => p with { PortraitKey = null }, rebuild: true); }).TooltipText = "清除头像";
        BuildFlags(avatar, person);
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkBegin };
        details.AddThemeConstantOverride("separation", 10); row.AddChild(details);
        var fields = ProfileGrid(details, "身份");
        var surname = OfficerEditorFields.Names(fields, person.Surname, person.Name, person.CourtesyName,
            value => Change(person.Id, p => p with { Surname = value }),
            value => Change(person.Id, p => p with { Name = value }),
            value => Change(person.Id, p => p with { CourtesyName = value }));
        surname.Editable = !Preferences.Current.AutoAssignOfficerFlag || Workspace.FlagCatalog is not null;
        if (!surname.Editable) surname.TooltipText = "公共旗字目录载入后可编辑姓氏。";
        surname.TextSubmitted += _ => FinishSurnameEdit(person.Id);
        surname.FocusExited += () => FinishSurnameEdit(person.Id);
        Choices(fields, "性别", ["男", "女"], person.IsFemale ? 1 : 0, value => Change(person.Id, p => p with { IsFemale = value == 1 }));
        BuildAttributes(details, person);
        BuildLife(ProfileGrid(details, "生平与关系"), person);
        ContentEditorControls.TextArea(page, "列传", person.Biography,
            value => Change(person.Id, p => p with { Biography = value }), "OfficerBiography");
    }
    private void BuildAttributes(Node parent, CreatedOfficer person)
    {
        OfficerEditorFields.Abilities(parent, person.Abilities,
            edit => Change(person.Id, p => p with { Abilities = edit(p.Abilities) }), 1, () =>
            {
                int maximum = CompanionSettings.Default.OfficerAbilityCap;
                Change(person.Id, p => p with { Abilities = new(maximum, maximum, maximum, maximum) }, rebuild: true);
            });
        OfficerEditorFields.Personality(parent, person.Personality, person.Righteousness, person.Ambition, person.Affinity,
            value => Change(person.Id, p => p with { Personality = value }),
            value => Change(person.Id, p => p with { Righteousness = value }),
            value => Change(person.Id, p => p with { Ambition = value }),
            value => Change(person.Id, p => p with { Affinity = value }));
    }
    private IEnumerable<(MilitaryCatalogDefinition Definition, TacticRule Rule)> EditableTactics()
    {
        if (_context?.Scenario.Military is not { } military) yield break;
        var rules = military.Tactics.ToDictionary(t => t.Id);
        foreach (var definition in _context.Scenario.Information.Tactics)
            if (rules.TryGetValue(definition.Id, out var rule) && rule.IsOfficerTactic)
                yield return (definition, rule);
    }
    private void BuildTactics(CreatedOfficer person)
    {
        var page = Page("兵法");
        page.AddThemeConstantOverride("separation", 8);
        var tactics = EditableTactics().Select(t => new OfficerTacticChoice(t.Definition.Id, t.Definition.Name, t.Rule.Category!.Value)).ToArray();
        OfficerTrainingFields Read()
        {
            var current = Library.Officers.Single(p => p.Id == person.Id);
            return new(current.Proficiencies, current.MasteredCategories, current.LearnedTactics);
        }
        OfficerEditorFields.Training(page, Read, value => Change(person.Id, p => p with
        { Proficiencies = value.Proficiencies, MasteredCategories = value.MasteredCategories, LearnedTactics = value.LearnedTactics }), tactics);
    }
    private void BuildLife(GridContainer grid, CreatedOfficer person)
    {
        Number(grid, "出生年", person.BirthYear, 1, int.MaxValue, value => Change(person.Id, p => p with { BirthYear = value }), "BirthYear");
        Number(grid, "登场年", person.AppearanceYear, 1, int.MaxValue, value => Change(person.Id, p => p with { AppearanceYear = value }), "AppearanceYear")
            .TooltipText = "独立填写，不随出生年自动改变。";
        Number(grid, "寿命档", person.LifespanTier, 0, 7, value => Change(person.Id, p => p with { LifespanTier = value }))
            .TooltipText = "自然死亡规则使用的档位，并非死亡年龄。";
        var references = new List<(string Label, Guid? Created, int? Historical)> { ("无亲代", null, null) };
        references.AddRange(Library.Officers.Where(o => o.Id != person.Id).Select(o => ("自创：" + o.FullName, (Guid?)o.Id, (int?)null)));
        if (_context is not null) references.AddRange(_context.Scenario.Officers.Select(o => ("剧本：" + _context.Text.Resolve(o.NameKey), (Guid?)null, (int?)o.State.Id)));
        if (person.ScenarioParentId is int existing && references.All(r => r.Historical != existing))
            references.Add(($"剧本人物 {existing}（当前参考中不可用）", null, existing));
        int selected = references.FindIndex(r => r.Created == person.ParentId && r.Historical == person.ScenarioParentId);
        Caption(grid, "亲代");
        var choose = AddButton(grid, references[Math.Max(0, selected)].Label,
            () => ChooseParent(references, person), "ChooseParent");
        choose.Alignment = HorizontalAlignment.Left; choose.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        choose.CustomMinimumSize = new(112, 34); choose.ClipText = true;
        choose.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        choose.TooltipText = references[Math.Max(0, selected)].Label + "\n按姓名搜索人物库或参考剧本中的人物。";
    }
}
