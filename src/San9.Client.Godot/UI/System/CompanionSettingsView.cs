using Godot;
using San9.Core.Domain;

namespace San9.Client.Godot;

/// <summary>Edits a draft; closing saves and applies only an actual configuration change.</summary>
public partial class CompanionSettingsView : ModalView
{
    private sealed record ToggleOption(string Title, string Group, string Note,
        Func<CompanionSettings, bool> Read, Func<CompanionSettings, bool, CompanionSettings> Write, NumericOption? Parameter = null);
    private sealed record NumericOption(string Title, string Group, string Note, int Minimum, int Maximum,
        Func<CompanionSettings, int> Read, Func<CompanionSettings, int, CompanionSettings> Write, string Suffix = "");

    private static readonly ToggleOption[] Options =
    [
        new("玩家自动内政", "系统", "每旬结束操作时，使用直辖军团的闲置武将依次安排训练、修筑、商业、开垦和巡察。",
            settings => settings.PlayerAutoDomesticEnabled,
            (settings, value) => settings with { PlayerAutoDomesticEnabled = value }),
        new("自动探索所在地", "系统", "直辖军团可行动武将探索所在地，回报选择由玩家回答；同时开启自动内政时，先做内政再探索。",
            settings => settings.PlayerAutoSearchEnabled,
            (settings, value) => settings with { PlayerAutoSearchEnabled = value },
            new("最低政治", "系统", "按当前有效政治筛选，包含伤势、宝物和官职影响。默认45，0不限制政治。",
                0, int.MaxValue, settings => settings.PlayerAutoSearchMinimumPolitics,
                (settings, value) => settings with { PlayerAutoSearchMinimumPolitics = value })),
        new("过滤一般探索汇报", "系统", "隐藏探索获得金钱和无收获的汇报；宝物、人物及特殊事件提示保留，含手动探索和自动探索。",
            settings => settings.HideRoutineSearchReports,
            (settings, value) => settings with { HideRoutineSearchReports = value }),
        new("分档增加武将能力", "熟练成长", "基础能力越高，普通成长概率越低：低能力5%，每15点递减，最低1%；不属于玩家势力的人物再加1%。",
            settings => settings.DynamicOfficerAbilityGrowth,
            (settings, value) => settings with { DynamicOfficerAbilityGrowth = value }),
        new("港口、城塞连射，阵齐射", "设施舰船", "按配置预设：港口、城塞发动连射，阵发动齐射。",
            settings => settings.UseFacilityTacticPreset,
            (settings, value) => settings with { UseFacilityTacticPreset = value }),
        new("艨艟可以发动箭雨", "设施舰船", "近距离优先强袭，否则改用箭雨，包括攻击设施。",
            settings => settings.EnableNavalTacticFallback,
            (settings, value) => settings with { EnableNavalTacticFallback = value }),
        new("斗舰不能发动箭岚", "设施舰船", "从斗舰的自动船型兵法中移除箭岚，保留强攻。不改变电脑选船策略。",
            settings => settings.SuppressAdvancedNavalTactic,
            (settings, value) => settings with { SuppressAdvancedNavalTactic = value }),
        new("兵数与统率增加兵法伤害", "战斗", "兵力至少一万时，按发动武将超过60的统率和当前兵力提高武力兵法伤害；设施按守军兵力计算。",
            settings => settings.BoostMartialTacticDamage,
            (settings, value) => settings with { BoostMartialTacticDamage = value }),
        new("少兵部队减少兵法伤害", "战斗", "兵力不足一万时，武力兵法本体伤害乘以兵数平方根÷100；不改变普通攻击。",
            settings => settings.ReduceSmallUnitTacticDamage,
            (settings, value) => settings with { ReduceSmallUnitTacticDamage = value }),
        new("远程兵法攻击设施减伤", "战斗", "弓骑、弩兵系兵法攻击设施时，耐久提供额外保护，最多减半；井阑的弩兵兵法不受此项影响。",
            settings => settings.ReduceRangedTacticDamageAgainstFacilities,
            (settings, value) => settings with { ReduceRangedTacticDamageAgainstFacilities = value }),
        new("减少战斗伤兵率", "战斗", "战斗伤兵比例：部队乘10/13，设施乘10/12。",
            settings => settings.ReduceBattleWounded,
            (settings, value) => settings with { ReduceBattleWounded = value }),
        new("提高耐久普防系数", "战斗", "设施耐久计入普通防御的系数从0.4提高到0.6，不改变城墙自身受到的耐久伤害。",
            settings => settings.IncreaseDurabilityDefense,
            (settings, value) => settings with { IncreaseDurabilityDefense = value }),
        new("统率和智力影响兵法发动概率", "发动联动", "武力兵法按执行统率、其他兵法按执行智力调整成员发动分配。人数系数乘以min(150, 执行能力+60)÷100。",
            settings => settings.AbilitiesAffectTacticAllocation,
            (settings, value) => settings with { AbilitiesAffectTacticAllocation = value }),
        new("士气影响反击兵法发动率", "发动联动", "部队和设施反击兵法的入口概率加上当前士气−80",
            settings => settings.MoraleAffectsTacticCounters,
            (settings, value) => settings with { MoraleAffectsTacticCounters = value }),
        new("调整水军兵法发动率", "发动联动", "未习得当前船只对应技术的首发武将，有一半几率失去本次兵法机会；不影响智力兵法带动的后续兵法。",
            settings => settings.RequireNavalTacticTraining,
            (settings, value) => settings with { RequireNavalTacticTraining = value }),
        new("箕形利于联动、雁行附加阻击", "效果阻击", "按配置预设交换阵型的兵法联动倍率和阻击加成。原版为锥行的联动优势移给箕形、箕形的阻击优势移给雁行。",
            settings => settings.RedistributeFormationTacticBonuses,
            (settings, value) => settings with { RedistributeFormationTacticBonuses = value }),
        new("君主都督交战时随机鼓舞", "效果阻击", "主动交战时，主将为无伤的君主或都督且士气低于70，有4%几率自鼓舞。士气增加至少6，按当前四项能力之和÷24计算。",
            settings => settings.LeadersInspireInCombat,
            (settings, value) => settings with { LeadersInspireInCombat = value }),
        new("分档增加兵法熟练度", "熟练成长", "精通加成之后，熟练度增量乘以max(1, 4−当前熟练度÷250)÷3。越熟练增长越慢。",
            settings => settings.DynamicTacticProficiencyGrowth,
            (settings, value) => settings with { DynamicTacticProficiencyGrowth = value }),
        new("智力兵法成功率修正", "效果阻击", "成功率改为100+(6×(执行智力差−5)−识破修正)×7÷30。习得但未装备教谕、陷阱时，识破修正为86。",
            settings => settings.AdjustIntelligenceTacticSuccess,
            (settings, value) => settings with { AdjustIntelligenceTacticSuccess = value }),
        new("混乱威力影响持续时间", "效果阻击", "混乱的智力项除以max(16, 40−威力)；熟练档大于1且执行智力至少30时，档位减1。只影响混乱本体。",
            settings => settings.ConfusionPowerAffectsDuration,
            (settings, value) => settings with { ConfusionPowerAffectsDuration = value }),
        new("首发与联动武将额外成长", "熟练成长", "全员基础熟练度+3，电脑势力+4；首发者额外加一倍基础值，首发者或联动者额外再+2。",
            settings => settings.RewardTacticParticipants,
            (settings, value) => settings with { RewardTacticParticipants = value }),
        new("智力兵法按亲密概率带动武力兵法", "发动联动", "武力首发候选按与智力发动者的相性、亲缘及关系独立筛选，不影响后续武力兵法联动。",
            settings => settings.AffinityAffectsMartialChains,
            (settings, value) => settings with { AffinityAffectsMartialChains = value }),
        new("攻城熟练度影响器械攻城力", "战斗", "配置预设的攻城阵型，对守军和耐久的普通攻击系数乘以1+(部队最高攻城熟练度−600)÷2048；不缩放武力兵法伤害。",
            settings => settings.ProficiencyAffectsSiegePower,
            (settings, value) => settings with { ProficiencyAffectsSiegePower = value }),
        new("减半设施智力兵法间隔", "发动联动", "设施发动智力兵法、鼓舞或治疗后，发动者的兵法间隔减半并向下取整。",
            settings => settings.HalveFacilityIntelligenceCooldown,
            (settings, value) => settings with { HalveFacilityIntelligenceCooldown = value }),
        new("取消交战每日熟练度增长", "熟练成长", "取消陆战和水战交战中的每日自动熟练度增长。兵法发动、训练、攻城等其他成长来源继续按各自规则结算。",
            settings => settings.DisableDailyCombatProficiencyGain,
            (settings, value) => settings with { DisableDailyCombatProficiencyGain = value }),
        new("阻击概率修正", "效果阻击", "熟练度平方项改为熟练度÷(40−2×阻击系数)，最多50；有效阵型加成大于0时减5。系数范围0～10、默认5。",
            settings => settings.AdjustSnipingChance,
            (settings, value) => settings with { AdjustSnipingChance = value },
            new("系数", "效果阻击", "仅在阻击概率修正开启时参与计算。范围0～10，默认5；熟练度除数为40−2×此值，默认除以30。",
                0, 10, settings => settings.SnipingChanceCoefficient,
                (settings, value) => settings with { SnipingChanceCoefficient = value })),
    ];
    private static readonly NumericOption[] NumericOptions =
    [
        new("武将基础能力上限", "熟练成长", "默认100，125为原伴侣预设。普通成长、事件奖励和武将编辑统一使用此值，已有超限能力不倒扣",
            100, int.MaxValue, settings => settings.OfficerAbilityCap,
            (settings, value) => settings with { OfficerAbilityCap = value }),
        new("武力兵法伤害系数", "战斗", "武力兵法本体伤害乘以此数值÷10；范围5～10，10保持原值。",
            5, 10, settings => settings.MartialTacticDamageCoefficient,
            (settings, value) => settings with { MartialTacticDamageCoefficient = value }, "/10"),
    ];
    private static readonly NumericOption[] AllocationOptions =
    [
        new("1人", "发动联动", "一人部队发动系数，默认20。",
            0, 20, settings => settings.TacticAllocation.One,
            (settings, value) => settings with { TacticAllocation = settings.TacticAllocation with { One = value } }),
        new("2人", "发动联动", "二人部队发动系数，默认14。",
            0, 20, settings => settings.TacticAllocation.Two,
            (settings, value) => settings with { TacticAllocation = settings.TacticAllocation with { Two = value } }),
        new("3人", "发动联动", "三人部队发动系数，默认10。",
            0, 20, settings => settings.TacticAllocation.Three,
            (settings, value) => settings with { TacticAllocation = settings.TacticAllocation with { Three = value } }),
        new("4人", "发动联动", "四人部队发动系数，默认9。",
            0, 20, settings => settings.TacticAllocation.Four,
            (settings, value) => settings with { TacticAllocation = settings.TacticAllocation with { Four = value } }),
        new("5人及以上", "发动联动", "五人及以上部队发动系数，默认8。",
            0, 20, settings => settings.TacticAllocation.FiveOrMore,
            (settings, value) => settings with { TacticAllocation = settings.TacticAllocation with { FiveOrMore = value } }),
    ];

    public CompanionSettings Initial { get; init; } = CompanionSettings.Default;
    public Action<CompanionSettings> SaveAndApply { get; init; } = null!;
    public Action Closed { get; init; } = null!;
    private CompanionSettings _settings = CompanionSettings.Default;
    private readonly List<(ToggleOption Option, Button Control)> _editors = [];
    private readonly List<(NumericOption Option, SpinBox Control)> _numericEditors = [];
    private Label _failure = null!;

    public override void _Ready()
    {
        _settings = Initial;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Theme = CompanionPanelStyle.Create(GetThemeFont("font"));
        var backdrop = new ColorRect { Color = new(0, 0, 0, .42f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer
        {
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = .5f, AnchorBottom = .5f,
            OffsetLeft = 120, OffsetRight = -120, OffsetTop = -330, OffsetBottom = 330,
        };
        AddChild(panel);
        var margin = new MarginContainer();
        foreach (string side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 24);
        panel.AddChild(margin);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 14); margin.AddChild(column);
        var heading = new VBoxContainer(); heading.AddThemeConstantOverride("separation", 4); column.AddChild(heading);
        var title = new Label { Text = "优化伴侣" };
        title.AddThemeFontSizeOverride("font_size", 24); heading.AddChild(title);
        heading.AddChild(Note("关闭面板时保存配置并生效，新游戏自动读取。"));
        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; column.AddChild(tabs);
        foreach (string group in Options.Select(option => option.Group).Concat(NumericOptions.Select(option => option.Group)).Distinct())
        {
            var scroll = new ScrollContainer { Name = group, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            list.AddThemeConstantOverride("separation", 12); scroll.AddChild(list); tabs.AddChild(scroll);
            foreach (var option in Options.Where(option => option.Group == group))
            {
                var row = OptionRow(list, option.Title, option.Note);
                bool enabled = option.Read(_settings);
                var toggle = new Button { Text = enabled ? "开启" : "关闭", ToggleMode = true,
                    ButtonPressed = enabled, CustomMinimumSize = new(96, 36) };
                CompanionPanelStyle.Toggle(toggle);
                toggle.Toggled += value => Edit(option.Write(_settings, value));
                if (option.Parameter is { } parameter)
                {
                    row.AddChild(new Label { Text = parameter.Title, TooltipText = parameter.Note,
                        MouseFilter = MouseFilterEnum.Pass, VerticalAlignment = VerticalAlignment.Center });
                    row.AddChild(NumberEditor(parameter, 88));
                }
                row.AddChild(toggle); _editors.Add((option, toggle));
            }
            foreach (var option in NumericOptions.Where(option => option.Group == group))
            {
                var row = OptionRow(list, option.Title, option.Note);
                row.AddChild(NumberEditor(option));
            }
            if (group == "发动联动")
            {
                var row = OptionRow(list, "人数发动系数",
                    "默认20／14／10／9／8，各项范围0～20，0关闭对应人数的个人兵法分配。先按人数选档，再计算能力修正、阵位和擅长系；自动水军与设施不增加个人分配判定。");
                var numbers = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                numbers.AddThemeConstantOverride("separation", 12); row.AddChild(numbers);
                foreach (var option in AllocationOptions)
                {
                    var field = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    numbers.AddChild(field);
                    var label = new Label { Text = option.Title, TooltipText = option.Note,
                        MouseFilter = MouseFilterEnum.Pass, HorizontalAlignment = HorizontalAlignment.Center };
                    label.AddThemeFontSizeOverride("font_size", 14); field.AddChild(label);
                    field.AddChild(NumberEditor(option, 80));
                }
            }
        }
        _failure = Note(""); _failure.Visible = false;
        _failure.AddThemeColorOverride("font_color", new("ffb4ab")); column.AddChild(_failure);
        var divider = new ColorRect { Color = new("343e4b"), CustomMinimumSize = new(0, 1), MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(divider);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 12); column.AddChild(actions);
        var shortcut = Note("F10 / Esc 保存并关闭"); shortcut.SizeFlagsVertical = SizeFlags.ShrinkCenter; actions.AddChild(shortcut);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var reset = new Button { Text = "恢复默认", CustomMinimumSize = new(104, 38) };
        reset.Pressed += () => Edit(CompanionSettings.Default); actions.AddChild(reset);
        var close = new Button { Text = "关闭", CustomMinimumSize = new(96, 38) };
        CompanionPanelStyle.Primary(close);
        close.Pressed += Back; actions.AddChild(close);
        close.GrabFocus();
    }

    private SpinBox NumberEditor(NumericOption option, int width = 112)
    {
        var number = new SpinBox { MinValue = option.Minimum, MaxValue = option.Maximum, Step = 1,
            Value = option.Read(_settings), CustomMinimumSize = new(width, 36), Suffix = option.Suffix,
            TooltipText = option.Note };
        number.GetLineEdit().TooltipText = option.Note;
        number.ValueChanged += value => Edit(option.Write(_settings, (int)value));
        _numericEditors.Add((option, number));
        return number;
    }

    private static HBoxContainer OptionRow(VBoxContainer list, string title, string description)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", CompanionPanelStyle.Card()); list.AddChild(card);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 10); card.AddChild(body);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); body.AddChild(row);
        var label = new Label { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 18); row.AddChild(label);
        body.AddChild(Note(description));
        return row;
    }

    private static Label Note(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new("aab6c5"));
        return label;
    }

    private void Edit(CompanionSettings settings)
    {
        _settings = settings;
        _failure.Hide();
        foreach (var (option, control) in _editors)
        {
            bool enabled = option.Read(_settings);
            control.SetPressedNoSignal(enabled);
            control.Text = enabled ? "开启" : "关闭";
        }
        foreach (var (option, control) in _numericEditors) control.SetValueNoSignal(option.Read(_settings));
    }

    public void Back()
    {
        try
        {
            foreach (var (_, control) in _numericEditors) control.Apply();
            if (_settings != Initial) SaveAndApply(_settings);
            Closed();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or
            InvalidDataException or InvalidOperationException)
        {
            _failure.Text = $"配置未能保存或应用：{error.Message}\n修改仍保留在面板中，请重试关闭。";
            _failure.Show();
        }
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree()) return;
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape or Key.F10 } ||
            input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        {
            GetViewport().SetInputAsHandled(); Back();
        }
    }

    public override void _GuiInput(InputEvent input) => AcceptEvent();
}
