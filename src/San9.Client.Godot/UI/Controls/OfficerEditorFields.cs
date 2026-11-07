using System.Collections.Immutable;
using Godot;
using San9.Core.Domain;

namespace San9.Client.Godot;

internal sealed record OfficerTrainingFields(ImmutableArray<int> Proficiencies,
    ImmutableArray<int> MasteredCategories, ImmutableSortedSet<int> LearnedTactics);
internal readonly record struct OfficerTacticChoice(int Id, string Name, int Category);

/// <summary>Person-specific forms shared by historical defaults and the created officer library.</summary>
internal static class OfficerEditorFields
{

    internal static LineEdit Names(Node parent, string surname, string givenName, string courtesy,
        Action<string> surnameChanged, Action<string> givenChanged, Action<string> courtesyChanged,
        string givenControl = "OfficerName", string courtesyControl = "CourtesyName")
    {
        var field = ContentEditorControls.TextField(parent, "姓", surname, surnameChanged, "OfficerSurname");
        ContentEditorControls.TextField(parent, "名", givenName, givenChanged, givenControl);
        ContentEditorControls.TextField(parent, "字", courtesy, courtesyChanged, courtesyControl); return field;
    }

    internal static void Abilities(Node parent, OfficerAbilities values, Action<Func<OfficerAbilities, OfficerAbilities>> edit,
        int minimum, Action? maximize = null)
    {
        var grid = ContentEditorControls.ProfileGrid(parent, "能力", header =>
        {
            if (maximize is null) return;
            var button = ContentEditorControls.ActionButton(header, "全满", maximize, "MaxAbilities");
            button.TooltipText = "能力全满：统率、武力、智力、政治全部设为100。";
            ContentEditorStyle.Compact(button);
        });
        Number(grid, "统率", values.Leadership, minimum, int.MaxValue, v => edit(a => a with { Leadership = v }), "OfficerLeadership");
        Number(grid, "武力", values.War, minimum, int.MaxValue, v => edit(a => a with { War = v }), "OfficerWar");
        Number(grid, "智力", values.Intelligence, minimum, int.MaxValue, v => edit(a => a with { Intelligence = v }), "OfficerIntelligence");
        Number(grid, "政治", values.Politics, minimum, int.MaxValue, v => edit(a => a with { Politics = v }), "OfficerPolitics");
    }

    internal static void Personality(Node parent, int personality, int righteousness, int ambition, int affinity,
        Action<int> personalityChanged, Action<int> righteousnessChanged, Action<int> ambitionChanged, Action<int> affinityChanged)
    {
        var grid = ContentEditorControls.ProfileGrid(parent, "个性");
        var choice = ContentEditorControls.Choices(grid, "性格", new[] { "莽撞", "刚胆", "冷静", "慎重" }, personality, personalityChanged);
        choice.CustomMinimumSize = new(112, 34);
        Number(grid, "义理", righteousness, 0, 15, righteousnessChanged);
        Number(grid, "野心", ambition, 0, 15, ambitionChanged);
        Number(grid, "相性", affinity, 0, 149, affinityChanged);
    }

    private static void Number(Node parent, string title, int value, int minimum, int maximum, Action<int> changed, string name = "")
    {
        ContentEditorControls.Caption(parent, title);
        ContentEditorControls.NumericInput(parent, value, minimum, maximum, next => changed((int)next), name);
    }

    internal static void Training(Node parent, Func<OfficerTrainingFields> read, Action<OfficerTrainingFields> write,
        IReadOnlyList<OfficerTacticChoice> tactics, int proficiencyCap)
    {
        var initial = read();
        var proficiencies = new SpinBox[TacticCategoryLabels.Names.Length];
        var mastery = new Button[TacticCategoryLabels.Names.Length];
        var learned = new Dictionary<int, Button>();
        bool building = true;
        bool ProficienciesFull(OfficerTrainingFields p) => p.Proficiencies.All(v => v >= proficiencyCap);
        bool MasteryFull(OfficerTrainingFields p) => Enumerable.Range(0, TacticCategoryLabels.Names.Length).All(p.MasteredCategories.Contains);
        bool TacticsFull(OfficerTrainingFields p) => tactics.Count > 0 && tactics.All(t => p.LearnedTactics.Contains(t.Id));
        var actions = ContentEditorControls.ButtonGroup(parent);
        // These callbacks run after the complete form has been built.
        Button max = null!, master = null!, learn = null!;
        void Refresh()
        {
            var p = read();
            max.Text = ProficienciesFull(p) ? "清空熟练度" : "熟练度全满";
            max.TooltipText = ProficienciesFull(p) ? "九类熟练度全部清零。" : $"九类熟练度全部设为{proficiencyCap}。";
            master.Text = MasteryFull(p) ? "取消全精通" : "全精通";
            master.TooltipText = MasteryFull(p) ? "取消九类精通。" : "开启九类精通。";
            learn.Text = TacticsFull(p) ? "清空兵法" : "全兵法";
            learn.TooltipText = TacticsFull(p) ? "清除所有列出的已学兵法。" : "学会所有列出的兵法。";
            for (int c = 0; c < TacticCategoryLabels.Names.Length; c++)
            {
                proficiencies[c].SetValueNoSignal(p.Proficiencies[c]);
                mastery[c].SetPressedNoSignal(p.MasteredCategories.Contains(c));
            }
            foreach (var (id, button) in learned) button.SetPressedNoSignal(p.LearnedTactics.Contains(id));
        }
        void Edit(Func<OfficerTrainingFields, OfficerTrainingFields> change)
        {
            if (building) return;
            write(change(read())); Refresh();
        }
        max = ContentEditorControls.ActionButton(actions, "熟练度全满", () => Edit(p => p with
            { Proficiencies = Enumerable.Repeat(ProficienciesFull(p) ? 0 : proficiencyCap, TacticCategoryLabels.Names.Length).ToImmutableArray() }), "MaxProficiencies");
        master = ContentEditorControls.ActionButton(actions, "全精通", () => Edit(p => p with
            { MasteredCategories = MasteryFull(p) ? [] : Enumerable.Range(0, TacticCategoryLabels.Names.Length).ToImmutableArray() }), "MasterAllCategories");
        learn = ContentEditorControls.ActionButton(actions, "全兵法", () => Edit(p => p with
            { LearnedTactics = TacticsFull(p) ? p.LearnedTactics.Except(tactics.Select(t => t.Id)) : p.LearnedTactics.Union(tactics.Select(t => t.Id)) }), "LearnAllTactics");
        learn.Disabled = tactics.Count == 0;
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 12); parent.AddChild(header);
        foreach (var (title, width) in new[] { ("类别", 48), ("熟练度", 112), ("", 64), ("已学兵法", 80) })
        {
            var label = new Label { Text = title, CustomMinimumSize = new(width, 24) };
            label.AddThemeFontSizeOverride("font_size", 14); label.AddThemeColorOverride("font_color", ContentEditorStyle.Muted); header.AddChild(label);
        }
        for (int c = 0; c < TacticCategoryLabels.Names.Length; c++)
        {
            int category = c;
            var row = new HBoxContainer { CustomMinimumSize = new(0, 36) }; row.AddThemeConstantOverride("separation", 12); parent.AddChild(row);
            row.AddChild(new Label { Text = TacticCategoryLabels.Names[c], CustomMinimumSize = new(48, 0), VerticalAlignment = VerticalAlignment.Center });
            proficiencies[c] = ContentEditorControls.NumericInput(row, initial.Proficiencies[c], 0, Math.Max(proficiencyCap, initial.Proficiencies[c]),
                v =>
                {
                    if (v > proficiencyCap) { Refresh(); return; }
                    Edit(p => p with { Proficiencies = p.Proficiencies.SetItem(category, (int)v) });
                }, tooltip: $"当前熟练度上限：{proficiencyCap}。已有超限值可保留。");
            proficiencies[c].SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            mastery[c] = ContentEditorControls.Toggle(row, "精通", initial.MasteredCategories.Contains(c), enabled => Edit(p => p with
                { MasteredCategories = enabled ? p.MasteredCategories.Add(category).Distinct().Order().ToImmutableArray() : p.MasteredCategories.Remove(category) }));
            var choices = ContentEditorControls.ButtonGroup(row); choices.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            foreach (var tactic in tactics.Where(t => t.Category == category))
            {
                int id = tactic.Id;
                learned[id] = ContentEditorControls.Toggle(choices, tactic.Name, initial.LearnedTactics.Contains(id), enabled => Edit(p => p with
                    { LearnedTactics = enabled ? p.LearnedTactics.Add(id) : p.LearnedTactics.Remove(id) }));
            }
        }
        building = false; Refresh();
    }
}
