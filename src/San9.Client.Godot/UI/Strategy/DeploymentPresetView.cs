using Godot;
using San9.Application;
using San9.Application.Presentation;
using San9.Content.Text;
using static San9.Client.Godot.DeploymentPresetStyle;

namespace San9.Client.Godot;

/// <summary>Campaign-owned presets. Applying edits preparation only; management uses session operations.</summary>
public partial class DeploymentPresetView : FixedPage
{
    protected override Color PageBackground => new(0, 0, 0, .65f);
    public DeploymentDraft Draft { get; init; } = null!;
    public GameSession Session { get; init; } = null!;
    public TextCatalog Texts { get; init; } = null!;
    public TextureScope Textures { get; init; } = null!;
    public Action Closed { get; init; } = null!;
    private readonly GameTextPresenter _text = new();
    private CommandWindow _window = null!;
    private LineEdit _name = null!;
    private Button _maximum = null!, _fixed = null!;
    private VBoxContainer _rows = null!;
    private ScrollContainer _listScroll = null!;
    private Control _detailBody = null!;
    private Label _status = null!, _count = null!;
    private InformationMenuButton _overwrite = null!, _rename = null!, _delete = null!;
    private Guid? _selectedId;
    private GameDialog? _dialog;
    private DeploymentPreset? Selected => Session.DeploymentPresets.FirstOrDefault(p => p.Id == _selectedId && p.ForceId == Draft.Force.Id);

    public override void _Ready()
    {
        const int panelTop = 124, bodyTop = 168, footerTop = 504;
        int panelHeight = footerTop - 16 - panelTop;
        int bodyHeight = footerTop - 28 - bodyTop;
        _window = new CommandWindow
        {
            Texts = Texts, Textures = Textures, Position = new(136, 116), Size = new(752, footerTop + 52),
            Title = "常用编队", AcceptTitle = "套用", CancelTitle = "返回", Closed = Closed, Accepted = ApplySelected,
            Help = () => Notice("在出征页配置队伍后，可在此新增或覆盖编队。\n选中方案后点击套用，仍可修改并选择出征目标。\n缺员编队以橙色标记，套用时直接使用当前可出征成员；兵力超过上限时按上限调整。\n编队随本局存档保存，不占用武将、兵力或资金。\n“最大兵力”选项在新增或覆盖时保存；取消出征不会撤销已保存的方案。")
        };
        AddChild(_window);
        Panel(_window, new(0, 0, 752, 104), "142334", "70664e");
        Text(_window, "方案名称", new(16, 12, 80, 36), 16, Gold);
        _name = new LineEdit
        {
            Position = new(104, 12), Size = new(304, 36), MaxLength = DeploymentPreset.MaximumNameLength,
            PlaceholderText = "输入编队名称", Text = DefaultName(Draft, Texts)
        };
        _name.AddThemeFontSizeOverride("font_size", 18);
        _name.AddThemeColorOverride("font_color", Ink);
        _name.AddThemeColorOverride("caret_color", Gold);
        _name.AddThemeColorOverride("selection_color", new("405b75"));
        _name.AddThemeStyleboxOverride("normal", Surface("0c1725", "566273"));
        _name.AddThemeStyleboxOverride("focus", Surface("0c1725", "d0b675"));
        _window.AddChild(_name);
        Text(_window, "兵力偏好", new(432, 12, 80, 36), 16, Gold);
        var modes = new ButtonGroup();
        _fixed = ModeButton("固定人数", new(520, 12, 104, 36), modes);
        _maximum = ModeButton("最大兵力", new(624, 12, 112, 36), modes);
        _fixed.ButtonPressed = true;
        _window.AddChild(_fixed);
        _window.AddChild(_maximum);
        _window.ActionButton("新增", "edit", new(16, 60, 104, 40), () => Save(false)).Disabled = !Draft.CanProceed;
        _overwrite = _window.ActionButton("覆盖", "edit", new(136, 60, 104, 40), () => Save(true));
        _rename = _window.ActionButton("改名", "edit", new(256, 60, 104, 40), Rename);
        _delete = _window.ActionButton("删除", "edit", new(632, 60, 104, 40), Delete);
        Panel(_window, new(0, panelTop, 264, panelHeight), "111e2d", "566273");
        Panel(_window, new(284, panelTop, 468, panelHeight), "111e2d", "566273");
        Text(_window, "已保存编队", new(16, 132, 184, 24), 18, Gold);
        _count = Text(_window, "", new(208, 132, 40, 24), 14, Muted, HorizontalAlignment.Right);
        Text(_window, "编队详情", new(300, 132, 436, 24), 18, Gold);
        _listScroll = new ScrollContainer
        {
            Position = new(12, bodyTop), Size = new(240, bodyHeight), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _window.AddChild(_listScroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 8);
        _listScroll.AddChild(_rows);
        _detailBody = new Control { Position = new(300, bodyTop), Size = new(436, bodyHeight), MouseFilter = MouseFilterEnum.Ignore };
        _window.AddChild(_detailBody);
        Panel(_window, new(0, footerTop, 752, 52), "142334", "566273");
        _status = Text(_window, "", new(16, footerTop + 6, 720, 24), 16, Ink);
        Text(_window, "方案随本局存档保存 · 出征目标每次另选", new(16, footerTop + 30, 720, 18), 13, Muted);
        Refresh();
    }

    private Label Text(Control parent, string value, Rect2 bounds, int size, Color color,
        HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new Label
        {
            Text = value, Position = bounds.Position, Size = bounds.Size, MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = alignment, VerticalAlignment = VerticalAlignment.Center, ClipText = true
        };
        parent.AddChild(label); _text.Apply(label, new(size, color, GameTextEffect.None, Colors.Transparent));
        return label;
    }
    private static Button ModeButton(string title, Rect2 bounds, ButtonGroup group)
    {
        var button = new Button { Text = title, Position = bounds.Position, Size = bounds.Size, ToggleMode = true, ButtonGroup = group };
        StyleButton(button); return button;
    }
    private string Person(int id) => Draft.View.Officers.FirstOrDefault(o => o.Id == id) is { } person ? Texts.Resolve(person.NameKey) : "未知武将";
    private string Formation(int? id) => Draft.View.Information.Formations.FirstOrDefault(f => f.Id == id) is { } formation
        ? Texts.Resolve(formation.NameKey) : "无";

    private void Refresh()
    {
        int scrollPosition = _listScroll.ScrollVertical;
        foreach (var child in _rows.GetChildren()) { _rows.RemoveChild(child); child.QueueFree(); }
        var presets = Session.DeploymentPresets.Where(p => p.ForceId == Draft.Force.Id).ToArray();
        _count.Text = presets.Length.ToString();
        foreach (var preset in presets)
        {
            var check = Draft.CheckPreset(preset);
            int? commander = Draft.PresetCommanderId(preset);
            string commanderName = commander is int person ? Person(person) : "无可用主将";
            var row = new Button
            {
                ToggleMode = true, ButtonPressed = preset.Id == _selectedId,
                CustomMinimumSize = new(224, 76), TooltipText = $"主将：{commanderName}\n" + check.DescribeAvailability(preset, Draft.View, Texts)
            };
            row.Pressed += () => { _selectedId = preset.Id; SelectPreset(); Refresh(); };
            StyleButton(row, warning: check.HasMissingMembers);
            _rows.AddChild(row);
            Panel(row, new(8, 6, 52, 64), "0c1725", check.HasMissingMembers ? "956c42" : "70664e");
            if (commander is int commanderId)
                row.AddChild(new AsyncPortrait
                {
                    Service = Textures.Service, Role = $"catalog-person-{commanderId}",
                    View = San9.Content.Images.PortraitView.Face, Position = new(10, 8), Size = new(48, 60),
                    MouseFilter = MouseFilterEnum.Ignore
                });
            else Text(row, "—", new(10, 8, 48, 60), 20, Muted, HorizontalAlignment.Center);
            Text(row, preset.Name, new(70, 6, 144, 26), 18, check.HasMissingMembers ? Warning : preset.Id == _selectedId ? Gold : Ink);
            Text(row, check.HasMissingMembers ? $"缺{check.MissingMembers.Length}人" : check.CanApply ? "可用" : "需调整", new(166, 30, 48, 20), 13,
                check.CanApply && !check.HasMissingMembers ? new("9dc8b1") : Warning, HorizontalAlignment.Right);
            string soldiers = preset.SoldierMode == DeploymentSoldierMode.Maximum ? "最大兵力" : $"{preset.Soldiers}人";
            string memberCount = check.HasMissingMembers ? $"{preset.Members.Length - check.MissingMembers.Length}/{preset.Members.Length}将" : $"{preset.Members.Length}将";
            Text(row, $"{memberCount} · {Formation(preset.FormationId)}", new(70, 30, 94, 20), 13, check.HasMissingMembers ? Warning : Muted);
            Text(row, soldiers, new(70, 52, 144, 18), 12, Muted);
        }
        if (_rows.GetChildCount() == 0)
        {
            var empty = new Control { CustomMinimumSize = new(224, 160) };
            _rows.AddChild(empty);
            Text(empty, "尚未保存编队", new(8, 40, 208, 28), 18, Muted, HorizontalAlignment.Center);
            Text(empty, "配置队伍后，点击上方「新增」", new(0, 80, 224, 24), 13, Muted, HorizontalAlignment.Center);
        }
        CallDeferred(nameof(RestoreScroll), scrollPosition);
        var selected = Selected;
        _overwrite.Disabled = selected is null || !Draft.CanProceed;
        _rename.Disabled = _delete.Disabled = selected is null;
        foreach (var child in _detailBody.GetChildren()) { _detailBody.RemoveChild(child); child.QueueFree(); }
        if (selected is null)
        {
            Text(_detailBody, "选择一个常用编队", new(0, 64, 436, 36), 22, Gold, HorizontalAlignment.Center);
            Text(_detailBody, "在左侧预览成员、阵形与兵力配置", new(0, 112, 436, 28), 16, Muted, HorizontalAlignment.Center);
            Text(_detailBody, "套用后仍可调整，再选择出征目标", new(0, 148, 436, 28), 16, Muted, HorizontalAlignment.Center);
            SetStatus("配置队伍后点击「新增」，或选择左侧编队套用。", Ink);
            return;
        }
        Summary("陆上阵形", Formation(selected.FormationId), 0);
        Summary("船", Formation(selected.NavalFormationId), 150);
        Summary("出征兵力", selected.SoldierMode == DeploymentSoldierMode.Maximum ? "当前最大" : selected.Soldiers.ToString("N0"), 300);
        var availability = Draft.CheckPreset(selected);
        Text(_detailBody, "武将", new(12, 78, 148, 24), 14, Gold);
        Text(_detailBody, "阵位", new(168, 78, 64, 24), 14, Gold);
        Text(_detailBody, "兵法", new(248, 78, 176, 24), 14, Gold);
        var scroll = new ScrollContainer
        {
            Position = new(0, 108), Size = new(436, _detailBody.Size.Y - 108), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _detailBody.AddChild(scroll);
        var members = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        members.AddThemeConstantOverride("separation", 4); scroll.AddChild(members);
        foreach (var member in selected.Members)
        {
            var missing = availability.MissingMembers.FirstOrDefault(m => m.OfficerId == member.OfficerId);
            string tactic = member.TacticId is int id && Draft.View.Information.Tactics.FirstOrDefault(t => t.Id == id) is { } info
                ? Texts.Resolve(info.NameKey) : "无";
            var line = new Panel { CustomMinimumSize = new(416, 32), MouseFilter = MouseFilterEnum.Ignore };
            line.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new(missing is null ? "1b2b3b" : "352b25") }); members.AddChild(line);
            Text(line, Person(member.OfficerId), new(12, 0, 148, 32), 16, missing is null ? Ink : Warning);
            string rank = member.Position.Row switch { 0 => "前锋", 1 => "中锋", _ => "后卫" };
            Text(line, missing is null ? rank : "缺员", new(168, 0, 64, 32), 15, missing is null ? Muted : Warning);
            string reason = missing?.Reason switch
            {
                DeploymentPresetFailure.OfficerDead => "已死亡",
                DeploymentPresetFailure.OfficerCaptured => "被俘",
                DeploymentPresetFailure.OfficerTransferred => "已转属",
                DeploymentPresetFailure.OfficerElsewhere => "不在本据点",
                DeploymentPresetFailure.OfficerBusy => "暂不可出征",
                _ => tactic
            };
            Text(line, reason, new(248, 0, 156, 32), 16, missing is null ? Ink : Warning);
        }
        string status = !availability.CanApply ? availability.Describe(Draft.View, Texts)
            : availability.HasMissingMembers ? $"{selected.Members.Length - availability.MissingMembers.Length}/{selected.Members.Length}名武将可用 · 套用时直接略过缺员"
            : "可以套用 · 成员与配置均可用";
        SetStatus(status, availability.CanApply && !availability.HasMissingMembers ? new("a7d3b8") : Warning);
    }

    private void RestoreScroll(int position) => _listScroll.ScrollVertical = position;

    private void Summary(string title, string value, float x)
    {
        Panel(_detailBody, new(x, 0, 136, 64), "1b2b3b", "34485d");
        Text(_detailBody, title, new(x + 12, 6, 112, 20), 13, Muted);
        Text(_detailBody, value, new(x + 12, 28, 112, 28), 20, Ink);
    }
    private void SetStatus(string value, Color color)
    { _status.Text = value; _text.Apply(_status, new(16, color, GameTextEffect.None, Colors.Transparent)); }

    private void SelectPreset()
    {
        if (Selected is not { } preset) return;
        _name.Text = preset.Name;
        _maximum.ButtonPressed = preset.SoldierMode == DeploymentSoldierMode.Maximum;
        _fixed.ButtonPressed = preset.SoldierMode == DeploymentSoldierMode.Fixed;
    }

    private void Save(bool overwrite)
    {
        var selected = Selected;
        if (overwrite && selected is null) return;
        if (!DeploymentPreset.IsValidName(_name.Text.Trim())) { Notice("请输入1至32字的编队名称。 "); return; }
        DeploymentPreset preset;
        try
        {
            preset = Draft.CreatePreset(overwrite ? selected!.Id : Guid.NewGuid(), _name.Text,
                _maximum.ButtonPressed ? DeploymentSoldierMode.Maximum : DeploymentSoldierMode.Fixed);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        { Notice("请先在出征页完成武将、阵形、兵法和兵力配置。 "); return; }
        void Commit()
        {
            if (!Change(new SaveDeploymentPresetOperation(preset))) return;
            Draft.MarkPresetSaved(preset);
            _selectedId = preset.Id; SelectPreset(); Refresh();
        }
        if (overwrite) Confirm($"用当前出征配置覆盖“{selected!.Name}”？", Commit);
        else Commit();
    }

    private void Rename()
    {
        if (Selected is not { } selected) return;
        string name = _name.Text.Trim();
        if (!DeploymentPreset.IsValidName(name)) { Notice("请输入1至32字的编队名称。 "); return; }
        if (Change(new SaveDeploymentPresetOperation(selected with { Name = name }))) Refresh();
    }
    private void Delete()
    {
        if (Selected is not { } selected) return;
        Confirm($"删除编队“{selected.Name}”？", () =>
        {
            if (!Change(new DeleteDeploymentPresetOperation(selected.Id))) return;
            _selectedId = null; Refresh();
        });
    }
    private bool Change(SessionOperation operation)
    {
        if (Session.Apply(Session.Request(operation)).Disposition == SessionDisposition.Accepted) return true;
        Notice("当前无法修改编队，请返回后重试。 "); return false;
    }

    private void ApplySelected()
    {
        if (Selected is not { } selected) { Notice("请先选择一个已保存的编队。 "); return; }
        var check = Draft.ApplyPreset(selected);
        if (check.CanApply) Closed();
        else Notice(check.Describe(Draft.View, Texts) + "。\n当前出征配置保持原样，请返回手动调整。 ");
    }

    private void Notice(string message) => Dialog(message, null);
    private void Confirm(string message, Action confirmed) => Dialog(message, confirmed);
    private void Dialog(string message, Action? confirmed)
    {
        if (_dialog is not null) return;
        _dialog = new GameDialog { Textures = Textures, Size = new(1024, 768) };
        _dialog.Closed += CloseDialog;
        _dialog.Dismissed += CloseDialog;
        _dialog.Confirmed += () => { CloseDialog(); confirmed?.Invoke(); };
        AddChild(_dialog);
        if (confirmed is null) _dialog.SetNotice(message); else _dialog.SetMessage(message);
    }
    private void CloseDialog()
    {
        if (_dialog is not { } dialog) return;
        _dialog = null; RemoveChild(dialog); dialog.QueueFree();
    }
    public override void _Input(InputEvent input)
    {
        if (_dialog is not null || !IsVisibleInTree()) return;
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } ||
            input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        { GetViewport().SetInputAsHandled(); Closed(); }
    }
    public override void _ExitTree() => _text.Dispose();
}
