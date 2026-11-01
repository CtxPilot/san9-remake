using Godot;
using San9.Core.Domain;

namespace San9.Client.Godot;

/// <summary>Edits a draft; closing saves and applies only an actual configuration change.</summary>
public partial class CompanionSettingsView : ModalView
{
    public CompanionSettings Initial { get; init; } = CompanionSettings.Default;
    public Action<CompanionSettings> SaveAndApply { get; init; } = null!;
    public Action Closed { get; init; } = null!;
    private CompanionSettings _settings = CompanionSettings.Default;
    private readonly List<(ToggleOption Option, Button Control)> _toggleEditors = [];
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
        foreach (var page in Pages)
        {
            var scroll = new ScrollContainer { Name = page.Title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            list.AddThemeConstantOverride("separation", 12); scroll.AddChild(list); tabs.AddChild(scroll);
            foreach (var section in page.Sections) AddSection(list, section);
            if (page.HasStrategyPermissions) StrategyPermissions(list);
        }
        _failure = Note(""); _failure.Visible = false;
        _failure.AddThemeColorOverride("font_color", new("ffb4ab")); column.AddChild(_failure);
        var divider = new ColorRect { Color = new("343e4b"), CustomMinimumSize = new(0, 1), MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(divider);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 12); column.AddChild(actions);
        var shortcut = Note("Esc 保存并关闭"); shortcut.SizeFlagsVertical = SizeFlags.ShrinkCenter; actions.AddChild(shortcut);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var preset = new Button { Text = "预设配置", CustomMinimumSize = new(104, 38),
            TooltipText = "开启普通功能开关；允许伪报、扰乱、激励、救援，禁止离间、流言、烧夷、夺取。\n资金与兵粮倍率200%，能力上限125，无退路俘获加成0%，其余数值保留当前输入。" };
        preset.Pressed += ApplyPreset; actions.AddChild(preset);
        var reset = new Button { Text = "原版配置", CustomMinimumSize = new(104, 38),
            TooltipText = "恢复原版对应配置。" };
        reset.Pressed += () => RefreshEditors(CompanionSettings.Default); actions.AddChild(reset);
        var close = new Button { Text = "关闭", CustomMinimumSize = new(96, 38) };
        CompanionPanelStyle.Primary(close);
        close.Pressed += Back; actions.AddChild(close);
        close.GrabFocus();
    }

    private void AddSection(VBoxContainer list, SectionDefinition section)
    {
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12); list.AddChild(body);
        if (section.Title is { } title)
        {
            var heading = new Label { Text = title };
            heading.AddThemeFontSizeOverride("font_size", 16);
            heading.AddThemeColorOverride("font_color", new("aab6c5")); body.AddChild(heading);
        }
        foreach (var option in section.Options)
        {
            switch (option)
            {
                case ToggleOption toggleOption:
                {
                    var row = OptionRow(body, toggleOption.Title, toggleOption.Note);
                    if (toggleOption.Parameter is { } parameter)
                    {
                        row.AddChild(new Label { Text = parameter.Title, TooltipText = parameter.Note,
                            MouseFilter = MouseFilterEnum.Pass, VerticalAlignment = VerticalAlignment.Center });
                        row.AddChild(NumberEditor(parameter, 88));
                    }
                    row.AddChild(ToggleEditor(toggleOption));
                    break;
                }
                case NumericOption numericOption:
                    OptionRow(body, numericOption.Title, numericOption.Note).AddChild(NumberEditor(numericOption));
                    break;
                case NumberGroupOption numberGroup:
                    NumberGroup(body, numberGroup.Title, numberGroup.Note, numberGroup.Fields, numberGroup.Width);
                    break;
            }
        }
    }

    private void StrategyPermissions(VBoxContainer list)
    {
        var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        card.AddThemeStyleboxOverride("panel", CompanionPanelStyle.Card()); list.AddChild(card);
        var table = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        table.AddThemeConstantOverride("h_separation", 16);
        table.AddThemeConstantOverride("v_separation", 6); card.AddChild(table);
        table.AddChild(new Label { Text = "计略", CustomMinimumSize = new(64, 0) });
        foreach (string scope in new[] { "电脑势力", "玩家委任军团" })
        {
            var label = new Label { Text = scope, CustomMinimumSize = new(112, 0),
                HorizontalAlignment = HorizontalAlignment.Center };
            label.AddThemeFontSizeOverride("font_size", 14); table.AddChild(label);
        }
        foreach (var option in StrategyOptions)
        {
            table.AddChild(new Label { Text = option.Title, TooltipText = option.Note,
                CustomMinimumSize = new(64, 0), VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Pass });
            foreach (bool delegated in new[] { false, true })
            {
                var optionToggle = new ToggleOption(option.Title, option.Note,
                    settings => (delegated ? settings.PlayerDelegatedStrategyPermissions : settings.ComputerStrategyPermissions).Allows(option.Kind),
                    (settings, allowed) => delegated
                        ? settings with { PlayerDelegatedStrategyPermissions = settings.PlayerDelegatedStrategyPermissions.WithAllowed(option.Kind, allowed) }
                        : settings with { ComputerStrategyPermissions = settings.ComputerStrategyPermissions.WithAllowed(option.Kind, allowed) },
                    EnabledText: "允许", DisabledText: "禁止",
                    PresetValue: option.Kind is not (AutomaticStrategyKind.Alienate or AutomaticStrategyKind.Gossip or
                        AutomaticStrategyKind.Burn or AutomaticStrategyKind.Seize));
                var toggle = ToggleEditor(optionToggle, 112);
                toggle.TooltipText = $"{(delegated ? "玩家委任军团" : "电脑势力")}：{option.Note}";
                table.AddChild(toggle);
            }
        }
    }

    private Button ToggleEditor(ToggleOption option, int width = 96)
    {
        bool enabled = option.Read(_settings);
        var toggle = new Button { Text = enabled ? option.EnabledText : option.DisabledText, ToggleMode = true,
            ButtonPressed = enabled, CustomMinimumSize = new(width, 36) };
        CompanionPanelStyle.Toggle(toggle);
        toggle.Toggled += value =>
        {
            Edit(option.Write(_settings, value));
            toggle.Text = value ? option.EnabledText : option.DisabledText;
        };
        _toggleEditors.Add((option, toggle));
        return toggle;
    }

    private void NumberGroup(VBoxContainer list, string title, string note, NumericOption[] options, int width)
    {
        var row = OptionRow(list, title, note);
        var numbers = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        numbers.AddThemeConstantOverride("separation", 12); row.AddChild(numbers);
        foreach (var option in options)
        {
            var field = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            numbers.AddChild(field);
            var label = new Label { Text = option.Title, TooltipText = option.Note,
                MouseFilter = MouseFilterEnum.Pass, HorizontalAlignment = HorizontalAlignment.Center };
            label.AddThemeFontSizeOverride("font_size", 14); field.AddChild(label);
            field.AddChild(NumberEditor(option, width));
        }
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

    private void ApplyPreset()
    {
        foreach (var (_, control) in _numericEditors) control.Apply();
        var settings = _settings;
        foreach (var (option, _) in _toggleEditors) settings = option.Write(settings, option.PresetValue);
        foreach (var (option, _) in _numericEditors)
            if (option.PresetValue is { } value) settings = option.Write(settings, value);
        RefreshEditors(settings);
    }

    private void Edit(CompanionSettings settings)
    {
        _settings = settings;
        _failure.Hide();
    }

    private void RefreshEditors(CompanionSettings settings)
    {
        Edit(settings);
        foreach (var (option, control) in _toggleEditors)
        {
            bool enabled = option.Read(_settings);
            control.SetPressedNoSignal(enabled);
            control.Text = enabled ? option.EnabledText : option.DisabledText;
        }
        foreach (var (option, control) in _numericEditors)
        {
            // Hidden tabs do not redraw SpinBox text after a Range value change.
            // Apply the replacement text so a later close cannot restore the old input.
            control.GetLineEdit().Text = option.Read(settings).ToString(System.Globalization.CultureInfo.InvariantCulture);
            control.Apply();
        }
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
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } ||
            input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        {
            GetViewport().SetInputAsHandled(); Back();
        }
    }

    public override void _GuiInput(InputEvent input) => AcceptEvent();
}
