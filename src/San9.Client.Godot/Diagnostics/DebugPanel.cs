using Godot;
using San9.Application;
using San9.Core.Simulation;

namespace San9.Client.Godot.Diagnostics;

/// <summary>Session-host-owned tools that survive HUD replacement.</summary>
public partial class DebugPanel : PanelContainer
{
    private static readonly string?[] Seasons = [null, "spring", "summer", "autumn", "winter"];
    private static readonly Color Muted = new("aab6c5"), Accent = new("a4e3ed"), Error = new("ffb4a6");
    public MapWorld World { get; init; } = null!;
    public SessionUiHost Host { get; init; } = null!;
    private Label _mapStatus = null!, _debugStatus = null!, _controlStatus = null!, _feedback = null!;
    private OptionButton _season = null!;
    private MenuButton _forces = null!;
    private Button _stop = null!;
    private bool _detailsExpanded;
    private GameSession? _controlSession;
    private long _controlRevision = -1;
    private string? _lastError;
    private double _refreshRemaining;
    private bool _changingControl;

    public override void _Input(InputEvent input)
    {
        if (ToggleKey(input)) GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (Visible && input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        { Close(); GetViewport().SetInputAsHandled(); }
    }

    private bool ToggleKey(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F3 }) return false;
        if (Visible) Close();
        else { Show(); Refresh(); }
        return true;
    }

    private void Close()
    {
        _season.GetPopup().Hide(); _forces.GetPopup().Hide(); Hide();
    }

    public override void _Process(double delta)
    {
        string? error = World.LoadError;
        if (error is not null && error != _lastError) { Show(); _refreshRemaining = 0; }
        _lastError = error;
        if (!Visible) return;
        _refreshRemaining -= delta;
        if (_refreshRemaining > 0) return;
        Refresh();
    }

    private void Refresh()
    {
        _refreshRemaining = 0.2;
        _mapStatus.Text = World.LoadError ?? (World.IsLoading ? "正在准备地图…" :
            $"{SeasonName(World.Season)} · 缩放 {World.ZoomFactor:0.00}×");
        _mapStatus.Modulate = World.LoadError is null ? Colors.White : Error;
        _debugStatus.Text = World.Textures.Statistics +
            $"\n地形 GPU 估算 {World.CacheBytes / 1048576.0:F1} MiB\nCPU 合成源 {World.CpuSourceBytes / 1048576.0:F1} MiB";
        _season.Disabled = Host.IsSwitching || World.IsLoading || World.Package is null;
        int season = Array.IndexOf(Seasons, World.PreviewSeasonOverride);
        if (_season.Selected != season) _season.Select(season);
        RefreshControl();
    }

    public override void _Ready()
    {
        Theme = ContentEditorStyle.Create(GetThemeDefaultFont());
        AddThemeStyleboxOverride("panel", CompanionPanelStyle.Card());
        MouseFilter = MouseFilterEnum.Stop;
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 10); AddChild(box);
        var header = new HBoxContainer(); box.AddChild(header);
        var title = new Label { Text = "调试面板", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 20); title.Modulate = Accent; header.AddChild(title);
        var close = new Button { Text = "关闭 · F3", TooltipText = "F3 切换面板，Esc 关闭" };
        close.Pressed += Close; header.AddChild(close);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10); scroll.AddChild(body);
        Heading(body, "势力控制");
        _forces = new MenuButton { Name = "ForceControl", Text = "选择势力…", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(_forces);
        var forceMenu = _forces.GetPopup();
        forceMenu.HideOnCheckableItemSelection = false;
        forceMenu.AboutToPopup += RefreshControl;
        forceMenu.IdPressed += id => ChangeControl((int)id);
        forceMenu.WindowInput += input => { if (ToggleKey(input)) forceMenu.SetInputAsHandled(); };
        _controlStatus = Text(body, "", muted: true);
        _stop = Action(body, "停在下一旬", () => { Host.Hud.Turn.StopAtStrategyBoundary(); RefreshControl(); });
        _stop.TooltipText = "完成当前旬后停止自动推进，再存档或接管势力。";
        _feedback = Text(body, ""); _feedback.Hide();
        body.AddChild(new HSeparator()); Heading(body, "地图预览");
        _mapStatus = Text(body, "");
        var seasonRow = new HBoxContainer(); body.AddChild(seasonRow);
        seasonRow.AddChild(new Label { Text = "显示季节", SizeFlagsVertical = SizeFlags.ShrinkCenter });
        _season = Options(seasonRow);
        foreach (string name in new[] { "跟随游戏日期", "春季", "夏季", "秋季", "冬季" }) _season.AddItem(name);
        _season.ItemSelected += i => { World.PreviewSeason(Seasons[i]); Refresh(); };
        body.AddChild(new HSeparator());
        var details = new Button { Text = "展开运行诊断", ToggleMode = true, Alignment = HorizontalAlignment.Left };
        body.AddChild(details);
        _debugStatus = Text(body, "", muted: true); _debugStatus.Hide();
        details.Toggled += expanded =>
        {
            _detailsExpanded = expanded; _debugStatus.Visible = expanded;
            details.Text = expanded ? "收起运行诊断" : "展开运行诊断"; ResizePanel();
        };
        GetViewport().SizeChanged += ResizePanel;
        ResizePanel(); Hide();
    }

    private void ResizePanel()
    {
        var viewport = GetViewportRect().Size;
        Position = new(16, Math.Min(116, viewport.Y / 5));
        Size = new(Math.Min(440, Math.Max(0, viewport.X - 32)),
            Math.Min(_detailsExpanded ? 570 : _feedback.Visible ? 500 : 460,
                Math.Max(0, viewport.Y - Position.Y - 16)));
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= ResizePanel;

    private OptionButton Options(Node parent)
    {
        var options = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FitToLongestItem = false, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        parent.AddChild(options);
        var popup = options.GetPopup();
        popup.WindowInput += input => { if (ToggleKey(input)) popup.SetInputAsHandled(); };
        return options;
    }

    private static Button Action(Node parent, string title, System.Action activate)
    {
        var button = new Button { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        button.Pressed += activate; parent.AddChild(button); return button;
    }

    private static Label Text(Node parent, string text, bool muted = false)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (muted) label.Modulate = Muted;
        parent.AddChild(label); return label;
    }

    private static void Heading(Node parent, string title)
    {
        var label = Text(parent, title); label.Modulate = Accent;
        label.AddThemeFontSizeOverride("font_size", 18);
    }

    private static string SeasonName(string season) => season switch
    { "spring" => "春季", "summer" => "夏季", "autumn" => "秋季", "winter" => "冬季", _ => "地图尚未就绪" };

    private void RefreshControl()
    {
        var session = Host.Session;
        var menu = _forces.GetPopup();
        if (_controlSession != session || _controlRevision != session.Snapshot.Revision)
        {
            var forces = session.View.Forces.ToDictionary(f => f.Id);
            var choices = session.Snapshot.Forces.Where(f => !f.IsDormant && !f.IsEliminated && !f.IsBarbarian)
                .OrderBy(f => f.Id).Select(f => (f.Id, Text: Host.Content.Text.Resolve(forces[f.Id].NameKey))).ToArray();
            // Update checkmarks in place so repeated clicks can use the same open menu.
            if (menu.ItemCount != choices.Length || choices.Where((choice, i) =>
                menu.GetItemId(i) != choice.Id || menu.GetItemText(i) != choice.Text).Any())
            {
                menu.Clear();
                foreach (var choice in choices) menu.AddCheckItem(choice.Text, choice.Id);
            }
            _controlSession = session; _controlRevision = session.Snapshot.Revision;
        }
        string? blocked = _changingControl || Host.IsSwitching ? "正在切换控制权…" :
            Host.IsBlocked ? "请先关闭系统菜单。" :
            Host.Hud.Turn.IsComputing ? "正在计算，请稍候。" :
            !Host.Hud.CanQuickSave || !session.CanChangeForceControl ? "当前阶段不可切换；可先停在下一旬。" :
            menu.ItemCount == 0 ? "没有可切换的势力。" : null;
        int controlled = 0;
        for (int i = 0; i < menu.ItemCount; i++)
        {
            var seat = session.Snapshot.Simulation.Controls.FirstOrDefault(c => c.ForceId == menu.GetItemId(i));
            bool player = seat?.Kind == ControllerKind.Player;
            menu.SetItemChecked(i, player);
            menu.SetItemDisabled(i, blocked is not null || seat is null);
            if (player) controlled++;
        }
        _forces.Disabled = menu.ItemCount == 0;
        _forces.Text = $"已控制 {controlled} 个势力 ▾";
        _forces.TooltipText = blocked ?? "点击势力勾选或取消控制。";
        _controlStatus.Text = blocked ?? "";
        _controlStatus.Visible = blocked is not null;
        var turn = Host.Hud.Turn;
        _stop.Disabled = Host.IsSwitching || !turn.CanStopAtStrategyBoundary || turn.StopAtBoundaryRequested;
        _stop.Text = turn.StopAtBoundaryRequested ? "已请求 · 本旬结束后停止" :
            turn.CanStopAtStrategyBoundary ? "停在下一旬" : "当前未自动推进";
    }

    private void ChangeControl(int id)
    {
        RefreshControl();
        var menu = _forces.GetPopup();
        int index = menu.GetItemIndex(id);
        if (index < 0 || menu.IsItemDisabled(index)) return;
        var seat = Host.Session.Snapshot.Simulation.Controls.Single(c => c.ForceId == id);
        var kind = seat.Kind == ControllerKind.Player ? ControllerKind.Automatic : ControllerKind.Player;
        string name = Host.Content.Text.Resolve(Host.Session.View.Forces.Single(f => f.Id == id).NameKey);
        var result = Host.Session.Apply(Host.Session.Request(new ForceControlOperation(id, kind)));
        _feedback.Show(); ResizePanel();
        _feedback.Modulate = result.Disposition == SessionDisposition.Rejected ? Error : Accent;
        if (result.Disposition == SessionDisposition.Rejected)
        { _feedback.Text = $"切换失败：{result.Rejection}"; return; }
        _feedback.Text = $"{name}已{(kind == ControllerKind.Player ? "由玩家接管" : "交给电脑")}。";
        _changingControl = true; RefreshControl();
        // Keep these tools and feedback while replacing the session-dependent HUD.
        Callable.From(() =>
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            Host.Activate(Host.Session, SessionReplacementKind.ControlChange);
            _changingControl = false; RefreshControl();
        }).CallDeferred();
    }
}
