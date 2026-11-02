using System.Collections.Immutable;
using Godot;
using San9.Content.Authoring;
using San9.Content.Scenarios;
using San9.Core.Domain;

namespace San9.Client.Godot;

public partial class CreatedOfficerEditor
{
    private static readonly string[] Categories = ["步兵", "骑兵", "弓骑", "弩兵", "水军", "攻城", "智略", "谋略", "策略"];
    private VBoxContainer Page(string title)
    {
        var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _tabs.AddChild(scroll);
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (string edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 16);
        scroll.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 12); margin.AddChild(column); return column;
    }
    private static GridContainer Grid(Node parent, int columns = 2)
    { var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill }; grid.AddThemeConstantOverride("h_separation", 18); grid.AddThemeConstantOverride("v_separation", 12); parent.AddChild(grid); return grid; }
    private static void Caption(Node parent, string text) => parent.AddChild(new Label { Text = text,
        CustomMinimumSize = new(56, 0), VerticalAlignment = VerticalAlignment.Center });
    private static VBoxContainer Section(Node parent, string title, Action<HBoxContainer>? actions = null)
    {
        var section = new VBoxContainer(); section.AddThemeConstantOverride("separation", 6); parent.AddChild(section);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 12); section.AddChild(header);
        var heading = new Label { Text = title, CustomMinimumSize = new(56, 0), VerticalAlignment = VerticalAlignment.Center };
        heading.AddThemeFontSizeOverride("font_size", 16);
        heading.AddThemeColorOverride("font_color", CreatedOfficerEditorStyle.Accent); header.AddChild(heading);
        actions?.Invoke(header);
        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        return section;
    }
    private static GridContainer ProfileGrid(Node parent, string title, Action<HBoxContainer>? actions = null)
    {
        var grid = Grid(Section(parent, title, actions), 4);
        grid.AddThemeConstantOverride("h_separation", 12); grid.AddThemeConstantOverride("v_separation", 8); return grid;
    }
    private static LineEdit TextField(Node parent, string label, string value, Action<string> changed, string name = "")
    {
        Caption(parent, label);
        var field = new LineEdit { Text = value, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new(112, 34) };
        if (name.Length > 0) field.Name = name;
        field.TextChanged += value => changed(value); parent.AddChild(field); return field;
    }
    private static SpinBox Number(Node parent, string label, int value, int minimum, int maximum, Action<int> changed, string name = "")
    {
        Caption(parent, label);
        var field = NumericInput(parent, value, minimum, maximum, changed, name);
        field.SizeFlagsHorizontal = SizeFlags.ExpandFill; return field;
    }
    private static SpinBox NumericInput(Node parent, int value, int minimum, int maximum, Action<int> changed, string name = "")
    {
        var field = new SpinBox { MinValue = minimum, MaxValue = maximum, Value = value, Step = 1,
            Rounded = true, UpdateOnTextChanged = true, SizeFlagsHorizontal = SizeFlags.ShrinkBegin, CustomMinimumSize = new(112, 34) };
        if (name.Length > 0) field.Name = name;
        field.ValueChanged += next => changed((int)next); parent.AddChild(field); return field;
    }
    private static Button Toggle(Node parent, string label, bool value, Action<bool> changed, string name = "")
    {
        var field = new Button { Text = label, ToggleMode = true, ButtonPressed = value, CustomMinimumSize = new(64, 32) };
        CompanionPanelStyle.Toggle(field);
        if (name.Length > 0) field.Name = name;
        field.Toggled += value => changed(value); parent.AddChild(field); return field;
    }
    private static OptionButton Choices(Node parent, string label, string[] values, int selected, Action<int> changed, string name = "")
    {
        Caption(parent, label);
        var field = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (name.Length > 0) field.Name = name;
        foreach (string value in values) field.AddItem(value);
        field.Select(selected); field.ItemSelected += index => changed((int)index); parent.AddChild(field); return field;
    }
    private void BuildProfile(CreatedOfficer person)
    {
        var page = Page("基本资料");
        page.AddThemeConstantOverride("separation", 12);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 18); page.AddChild(row);
        var avatar = Section(row, "头像"); avatar.CustomMinimumSize = new(160, 0); avatar.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        var portraitFrame = new PanelContainer();
        portraitFrame.AddThemeStyleboxOverride("panel", CreatedOfficerEditorStyle.Surface("151c24", "465260", 6)); avatar.AddChild(portraitFrame);
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
        var surname = TextField(fields, "姓", person.Surname, value => Change(person.Id, p => p with { Surname = value }), "OfficerSurname");
        surname.Editable = !Preferences.Current.AutoAssignOfficerFlag || Workspace.FlagCatalog is not null;
        if (!surname.Editable) surname.TooltipText = "公共旗字目录载入后可编辑姓氏。";
        surname.TextSubmitted += _ => FinishSurnameEdit(person.Id);
        surname.FocusExited += () => FinishSurnameEdit(person.Id);
        TextField(fields, "名", person.Name, value => Change(person.Id, p => p with { Name = value }), "OfficerName");
        TextField(fields, "字", person.CourtesyName, value => Change(person.Id, p => p with { CourtesyName = value }), "CourtesyName");
        Choices(fields, "性别", ["男", "女"], person.IsFemale ? 1 : 0, value => Change(person.Id, p => p with { IsFemale = value == 1 }));
        BuildAttributes(details, person);
        BuildLife(ProfileGrid(details, "生平与关系"), person);
        var biographySection = Section(page, "列传");
        var biography = new TextEdit { Text = person.Biography, CustomMinimumSize = new(0, 96), WrapMode = TextEdit.LineWrappingMode.Boundary, Name = "OfficerBiography" };
        biography.TextChanged += () => Change(person.Id, p => p with { Biography = biography.Text }); biographySection.AddChild(biography);
    }
    private void BuildAttributes(Node parent, CreatedOfficer person)
    {
        var grid = ProfileGrid(parent, "能力", header =>
        {
            var maxAbilities = AddButton(header, "全满", () =>
            {
                int maximum = CompanionSettings.Default.OfficerAbilityCap;
                Change(person.Id, p => p with { Abilities = new(maximum, maximum, maximum, maximum) }, rebuild: true);
            }, "MaxAbilities");
            maxAbilities.TooltipText = "能力全满：统率、武力、智力、政治全部设为100。";
            CreatedOfficerEditorStyle.Compact(maxAbilities);
        });
        Number(grid, "统率", person.Abilities.Leadership, 1, int.MaxValue, value => Change(person.Id, p => p with { Abilities = p.Abilities with { Leadership = value } }), "OfficerLeadership");
        Number(grid, "武力", person.Abilities.War, 1, int.MaxValue, value => Change(person.Id, p => p with { Abilities = p.Abilities with { War = value } }), "OfficerWar");
        Number(grid, "智力", person.Abilities.Intelligence, 1, int.MaxValue, value => Change(person.Id, p => p with { Abilities = p.Abilities with { Intelligence = value } }), "OfficerIntelligence");
        Number(grid, "政治", person.Abilities.Politics, 1, int.MaxValue, value => Change(person.Id, p => p with { Abilities = p.Abilities with { Politics = value } }), "OfficerPolitics");
        grid = ProfileGrid(parent, "个性");
        var personality = Choices(grid, "性格", ["莽撞", "刚胆", "冷静", "慎重"], person.Personality, value => Change(person.Id, p => p with { Personality = value }));
        personality.CustomMinimumSize = new(112, 34);
        Number(grid, "义理", person.Righteousness, 0, 15, value => Change(person.Id, p => p with { Righteousness = value }));
        Number(grid, "野心", person.Ambition, 0, 15, value => Change(person.Id, p => p with { Ambition = value }));
        Number(grid, "相性", person.Affinity, 0, 149, value => Change(person.Id, p => p with { Affinity = value }));
    }
    private IEnumerable<(MilitaryCatalogDefinition Definition, TacticRule Rule)> EditableTactics()
    {
        if (_context?.Scenario.Military is not { } military) yield break;
        var rules = military.Tactics.ToDictionary(t => t.Id);
        foreach (var definition in _context.Scenario.Information.Tactics)
            if (rules.TryGetValue(definition.Id, out var rule) && rule.Category is >= 0 and <= 8 &&
                (rule.Selectable || rule.Effect is TacticEffect.FormationTechnology or TacticEffect.Construction or TacticEffect.StoneFormation))
                yield return (definition, rule);
    }
    private void BuildTactics(CreatedOfficer person)
    {
        var page = Page("兵法");
        page.AddThemeConstantOverride("separation", 8);
        var tactics = EditableTactics().ToArray();
        bool ProficienciesFull(CreatedOfficer p) => p.Proficiencies.All(value => value >= 1000);
        bool CategoriesFull(CreatedOfficer p) => Enumerable.Range(0, Categories.Length).All(p.MasteredCategories.Contains);
        bool TacticsFull(CreatedOfficer p) => tactics.Length > 0 && tactics.All(t => p.LearnedTactics.Contains(t.Definition.Id));
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 8); page.AddChild(actions);
        var maxProficiencies = AddButton(actions, "熟练度全满", () =>
        {
            Change(person.Id, p => p with { Proficiencies = Enumerable.Repeat(ProficienciesFull(p) ? 0 : 1000, Categories.Length).ToImmutableArray() }, rebuild: true);
        }, "MaxProficiencies");
        var masterAll = AddButton(actions, "全精通", () =>
        {
            Change(person.Id, p => p with { MasteredCategories = CategoriesFull(p) ? [] : Enumerable.Range(0, Categories.Length).ToImmutableArray() }, rebuild: true);
        }, "MasterAllCategories");
        var learnAll = AddButton(actions, "全兵法", () =>
        {
            Change(person.Id, p => p with { LearnedTactics = TacticsFull(p) ? [] : p.LearnedTactics.Union(tactics.Select(t => t.Definition.Id)) }, rebuild: true);
        }, "LearnAllTactics");
        learnAll.Disabled = tactics.Length == 0;
        void RefreshActions()
        {
            if (Current is not { } current) return;
            maxProficiencies.Text = ProficienciesFull(current) ? "清空熟练度" : "熟练度全满";
            maxProficiencies.TooltipText = ProficienciesFull(current) ? "九类熟练度全部清零。" : "九类熟练度全部设为1000。";
            masterAll.Text = CategoriesFull(current) ? "取消全精通" : "全精通";
            masterAll.TooltipText = CategoriesFull(current) ? "取消九类精通。" : "开启九类精通。";
            learnAll.Text = TacticsFull(current) ? "清空兵法" : "全兵法";
            learnAll.TooltipText = TacticsFull(current) ? "清空所有已学兵法。" : "学会当前参考剧本的全部可学习兵法。";
        }
        RefreshActions();
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 12); page.AddChild(header);
        foreach (var (title, width) in new[] { ("类别", 48), ("熟练度", 112), ("", 64), ("已学兵法", 80) })
        {
            var label = new Label { Text = title, CustomMinimumSize = new(width, 24) };
            label.AddThemeFontSizeOverride("font_size", 14); label.AddThemeColorOverride("font_color", CreatedOfficerEditorStyle.Muted);
            header.AddChild(label);
        }
        for (int i = 0; i < Categories.Length; i++)
        {
            int category = i;
            var row = new HBoxContainer { CustomMinimumSize = new(0, 36) }; row.AddThemeConstantOverride("separation", 12); page.AddChild(row);
            row.AddChild(new Label { Text = Categories[i], CustomMinimumSize = new(48, 0), VerticalAlignment = VerticalAlignment.Center });
            NumericInput(row, person.Proficiencies[i], 0, int.MaxValue,
                value => { Change(person.Id, p => p with { Proficiencies = p.Proficiencies.SetItem(category, value) }); RefreshActions(); });
            var mastery = Toggle(row, "精通", person.MasteredCategories.Contains(i), enabled =>
            {
                Change(person.Id, p => p with { MasteredCategories = enabled ? p.MasteredCategories.Add(category).Distinct().Order().ToImmutableArray() : p.MasteredCategories.Remove(category) });
                RefreshActions();
            });
            mastery.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            if (_context is null) continue;
            var choices = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            choices.AddThemeConstantOverride("h_separation", 6); choices.AddThemeConstantOverride("v_separation", 6); row.AddChild(choices);
            foreach (var (definition, _) in tactics.Where(t => t.Rule.Category == category))
            {
                int id = definition.Id;
                Toggle(choices, _context.Text.Resolve(definition.NameKey), person.LearnedTactics.Contains(id), enabled =>
                {
                    Change(person.Id, p => p with { LearnedTactics = enabled ? p.LearnedTactics.Add(id) : p.LearnedTactics.Remove(id) });
                    RefreshActions();
                });
            }
        }
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
