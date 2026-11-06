using System.Collections.Immutable;
using System.Text.Json;
using Godot;
using San9.Core.Domain;

namespace San9.Client.Godot;

public partial class FormationEditorPage
{
    private VBoxContainer Page(string title)
    {
        var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; _tabs.AddChild(scroll);
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(margin);
        foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 14);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 12); margin.AddChild(box); return box;
    }

    private static GridContainer Section(Node parent, string title)
    {
        var caption = new Label { Text = title }; caption.AddThemeColorOverride("font_color", ContentEditorStyle.Accent); parent.AddChild(caption);
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12); grid.AddThemeConstantOverride("v_separation", 9); parent.AddChild(grid); return grid;
    }

    private void Number(Node parent, string title, double value, double min, double max, Action<double> changed, string name = "")
    {
        parent.AddChild(new Label { Text = title, VerticalAlignment = VerticalAlignment.Center });
        var input = new SpinBox { MinValue = min, MaxValue = max, Value = value, Step = 1, Rounded = true,
            UpdateOnTextChanged = true, CustomMinimumSize = new(110, 34), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (name.Length > 0) input.Name = name;
        var apply = Guard(changed); input.ValueChanged += next => apply(next); parent.AddChild(input);
    }

    private void Toggle(Node parent, string title, bool value, Action<bool> changed)
    {
        var check = new CheckBox { Text = title, ButtonPressed = value, CustomMinimumSize = new(0, 32) };
        var apply = Guard(changed); check.Toggled += next => apply(next); parent.AddChild(check);
    }

    private void BuildBasics(FormationRule f)
    {
        int id = f.Id;
        var page = Page("基础");
        var catalog = Workspace.GetFormationCatalog(id);
        ContentEditorControls.AddText(page, "名称", catalog.Name, Guard<string>(v => { Workspace.SetFormationCatalog(Workspace.GetFormationCatalog(id) with { Name = v }); RefreshList(); UpdateHeading(); }));
        ContentEditorControls.AddText(page, "描述", catalog.Description, Guard<string>(v => { Workspace.SetFormationCatalog(Workspace.GetFormationCatalog(id) with { Description = v }); RefreshList(); UpdateHeading(); }), true);
        var grid = Section(page, "行军与编制");
        Number(grid, "每日移动力", f.MovementBudget, 1, int.MaxValue, v => Edit(id, p => p with { MovementBudget = (int)v }), "FormationMovement");
        Number(grid, "出征费用", f.DeploymentCost, 0, int.MaxValue, v => Edit(id, p => p with { DeploymentCost = (int)v }), "FormationCost");
        Number(grid, "兵力上限", f.SoldierCapacity, 1, uint.MaxValue, v => Edit(id, p => p with { SoldierCapacity = (uint)v }), "FormationSoldiers");
        Number(grid, "武将上限", f.OfficerCapacity, 1, int.MaxValue, v => Edit(id, p => p with { OfficerCapacity = (int)v }), "FormationOfficers");
        Number(grid, "基础占地宽", f.Footprint.Width, 1, int.MaxValue, v => Edit(id, p => p with { Footprint = p.Footprint with { Width = (int)v } }));
        Number(grid, "基础占地高", f.Footprint.Height, 1, int.MaxValue, v => Edit(id, p => p with { Footprint = p.Footprint with { Height = (int)v } }));
        grid.AddChild(new Label { Text = "行军方式" });
        var travel = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; travel.AddItem("陆上"); travel.AddItem("水上"); travel.Select((int)f.TravelMode);
        var choose = Guard<long>(v => Edit(id, p => p with { TravelMode = (TravelMode)v })); travel.ItemSelected += v => choose(v); grid.AddChild(travel);
        Toggle(page, "通用阵形（仍需满足兵法等使用条件）", f.IsCommon, v => Edit(id, p => p with { IsCommon = v }));
        page.AddChild(new Label { Text = "距离与占地以细单位计，5单位为1地图格；普通陆地每格基础消耗50移动力。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        Toggle(page, "提供视野", f.Vision is not null, v => Edit(id, p => p with { Vision = v ? p.Vision ?? new(15, 180) : null }, true));
        if (f.Vision is { } vision)
        {
            var view = Section(page, "视野");
            Number(view, "视野半径", vision.Radius, 0, int.MaxValue, v => Edit(id, p => p with { Vision = p.Vision! with { Radius = (int)v } }));
            Number(view, "视野角度", vision.ArcDegrees, 0, 360, v => Edit(id, p => p with { Vision = p.Vision! with { ArcDegrees = (int)v } }));
        }
    }

    private void BuildCombat(FormationRule f)
    {
        int id = f.Id; var page = Page("战斗");
        Toggle(page, "启用战斗系数", f.Combat is not null, v => Edit(id, p => p with { Combat = v ? p.Combat ?? new(10, 10, 10, 10, 50) : null }, true));
        if (f.Combat is { } combat)
        {
            var grid = Section(page, "攻防系数");
            Number(grid, "对部队攻击", combat.AgainstUnits, 0, int.MaxValue, v => Edit(id, p => p with { Combat = p.Combat! with { AgainstUnits = (int)v } }), "FormationAttack");
            Number(grid, "对守兵攻击", combat.AgainstGarrison, 0, int.MaxValue, v => Edit(id, p => p with { Combat = p.Combat! with { AgainstGarrison = (int)v } }));
            Number(grid, "对城墙攻击", combat.AgainstWalls, 0, int.MaxValue, v => Edit(id, p => p with { Combat = p.Combat! with { AgainstWalls = (int)v } }));
            Number(grid, "防御系数", combat.Defense, 0, int.MaxValue, v => Edit(id, p => p with { Combat = p.Combat! with { Defense = (int)v } }), "FormationDefense");
            Number(grid, "伤兵比例 %", combat.WoundedPercent, 0, 100, v => Edit(id, p => p with { Combat = p.Combat! with { WoundedPercent = (int)v } }));
        }
        Toggle(page, "启用普通攻击", f.Attack is not null, v => Edit(id, p => p with { Attack = v ? p.Attack ?? new(10, false) : null }, true));
        if (f.Attack is { } attack)
        {
            var grid = Section(page, "普通攻击");
            Number(grid, "攻击距离", attack.Range, 1, int.MaxValue, v => Edit(id, p => p with { Attack = p.Attack! with { Range = (int)v } }));
            Number(grid, "每级高差加成", attack.ElevationBonusPerLevel, 0, int.MaxValue, v => Edit(id, p => p with { Attack = p.Attack! with { ElevationBonusPerLevel = (int)v } }));
            Number(grid, "高差加成上限", attack.MaximumElevationBonus, 0, int.MaxValue, v => Edit(id, p => p with { Attack = p.Attack! with { MaximumElevationBonus = (int)v } }));
            var checks = new VBoxContainer(); page.AddChild(checks);
            Toggle(checks, "可以削减城墙耐久", attack.DamagesWalls, v => Edit(id, p => p with { Attack = p.Attack! with { DamagesWalls = v } }));
            Toggle(checks, "攻击要求水域接触", attack.RequiresWaterContact, v => Edit(id, p => p with { Attack = p.Attack! with { RequiresWaterContact = v } }));
            Toggle(checks, "远距攻城", attack.RangedSiege, v => Edit(id, p => p with { Attack = p.Attack! with { RangedSiege = v } }));
        }
    }

    private void BuildTactics(FormationRule f)
    {
        int id = f.Id; var page = Page("兵法与特性");
        Toggle(page, "启用兵法发动配置", f.Tactics is not null, v => Edit(id, p => p with
        { Tactics = v ? p.Tactics ?? new(15, 2, [], []) : null }, true));
        if (f.Tactics is { } tactics)
        {
            var grid = Section(page, "兵法机会与联动");
            Number(grid, "个人发动机会 %", tactics.OpportunityChance, 0, 100, v => Edit(id, p => p with { Tactics = p.Tactics! with { OpportunityChance = (int)v } }));
            Number(grid, "联动倍率", tactics.LinkMultiplier, 1, int.MaxValue, v => Edit(id, p => p with { Tactics = p.Tactics! with { LinkMultiplier = (int)v } }));
            if (tactics.AutomaticOpportunityChance is int chance)
                Number(grid, "自动兵法机会 %", chance, 0, 100, v => Edit(id, p => p with { Tactics = p.Tactics! with { AutomaticOpportunityChance = (int)v } }));
        }
        var resistance = Section(page, "抗性与狙击");
        Number(resistance, "混乱抵抗 %", f.ConfusionResistanceChance, 0, 100, v => Edit(id, p => p with { ConfusionResistanceChance = (int)v }));
        Number(resistance, "狙击机会加成", f.SnipingChanceBonus, 0, 100, v => Edit(id, p => p with { SnipingChanceBonus = (int)v }));
        var flags = new VBoxContainer(); page.AddChild(flags);
        Toggle(flags, "忽略被动反制", f.BypassesPassiveCounters, v => Edit(id, p => p with { BypassesPassiveCounters = v }));
        Toggle(flags, "俘获效果翻倍", f.DoublesCapture, v => Edit(id, p => p with { DoublesCapture = v }));
        Toggle(flags, "阻止兵法救援", f.PreventsTacticRescue, v => Edit(id, p => p with { PreventsTacticRescue = v }));
        Toggle(flags, "近战阵亡按行位计算", f.MeleeDeathUsesRanks, v => Edit(id, p => p with { MeleeDeathUsesRanks = v }));
        Toggle(flags, "单挑机会翻倍", f.DoublesDuelChance, v => Edit(id, p => p with { DoublesDuelChance = v }));
        Toggle(flags, "每日获得战斗熟练度", f.GainsDailyCombatProficiency, v => Edit(id, p => p with { GainsDailyCombatProficiency = v }));
        Toggle(flags, "受到的敌方计略机会减半", f.HalvesHostileStrategyChance, v => Edit(id, p => p with { HalvesHostileStrategyChance = v }));
        Toggle(flags, "免疫扰乱", f.ImmuneToDisorder, v => Edit(id, p => p with { ImmuneToDisorder = v }));
    }

    private void BuildAdvanced(FormationRule f)
    {
        int id = f.Id; var page = Page("阵位与完整数据");
        page.AddChild(new Label { Text = "阵位顺序对应部队成员顺序。启用普通攻击时，阵位数量须等于武将上限。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var positions = new GridContainer { Columns = 4 }; positions.AddThemeConstantOverride("h_separation", 10); page.AddChild(positions);
        for (int i = 0; i < f.Positions.Length; i++)
        {
            int index = i;
            Number(positions, $"阵位 {i + 1} 列", f.Positions[i].Column, 0, int.MaxValue,
                v => Edit(id, p => p with { Positions = p.Positions.SetItem(index, p.Positions[index] with { Column = (int)v }) }));
            var rank = new OptionButton(); foreach (string name in new[] { "前列", "中列", "后列" }) rank.AddItem(name); rank.Select((int)f.Positions[i].Rank);
            var choose = Guard<long>(v => Edit(id, p => p with { Positions = p.Positions.SetItem(index, p.Positions[index] with { Rank = (BattleRank)v }) }));
            rank.ItemSelected += value => choose(value); positions.AddChild(rank);
            var remove = Guard<bool>(_ => Edit(id, p => p with { Positions = p.Positions.RemoveAt(index) }, true));
            Button(positions, "移除", () => remove(true));
        }
        var add = Guard<bool>(_ => Edit(id, p => p with { Positions = p.Positions.Add(new(p.Positions.Select(s => s.Column).DefaultIfEmpty(-1).Max() + 1, BattleRank.Middle)) }, true));
        Button(page, "添加阵位", () => add(true));
        page.AddChild(new Label { Text = "完整数据（JSON）" });
        _json = new TextEdit { Text = JsonSerializer.Serialize(f, EditorJson), CustomMinimumSize = new(0, 340), SizeFlagsHorizontal = SizeFlags.ExpandFill, Name = "FormationJson" };
        var field = _json; field.TooltipText = "阵形 ID 不可修改。";
        field.TextChanged += () => { if (!_building) { _rawDirty = true; UpdateHeading(); } }; page.AddChild(field);
        var apply = Guard<bool>(_ => { if (CommitJson()) { ShowCurrent(); Status("已应用，尚未保存。"); } });
        Button(page, "应用数据", () => apply(true));
    }
}
