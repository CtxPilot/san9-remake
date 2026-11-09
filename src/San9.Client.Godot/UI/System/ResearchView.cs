using Godot;
using San9.Application;
using San9.Content.Text;
using San9.Core.Simulation;

namespace San9.Client.Godot;

public partial class ResearchView : ModalView
{
    public GameSession Session { get; init; } = null!;
    public TextCatalog Texts { get; init; } = null!;
    public Action Closed { get; init; } = null!;
    public int? InitialSettlementId { get; init; }
    private VBoxContainer _projects = null!;
    private OptionButton _settlements = null!;
    private Label _status = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Theme = CompanionPanelStyle.Create(GetThemeFont("font"));
        var backdrop = new ColorRect { Color = new(0, 0, 0, .42f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 64, OffsetRight = -64, OffsetTop = 20, OffsetBottom = -20 };
        AddChild(panel);
        var margin = new MarginContainer();
        foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 16);
        panel.AddChild(margin);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 8); margin.AddChild(column);
        var title = new Label { Text = "兵种研究" }; title.AddThemeFontSizeOverride("font_size", 24); column.AddChild(title);
        _settlements = new OptionButton(); column.AddChild(_settlements);
        int? forceId = Session.View.PlayerForceId;
        var world = Session.Snapshot;
        var seat = world.Simulation.Controls.FirstOrDefault(c => c.ForceId == forceId && c.Kind == ControllerKind.Player);
        if (seat is not null)
        {
            var context = new CommandContext("player");
            var validPlaces = world.Settlements.Where(place => !place.IsDormant && !place.IsDestroyed && place.CanHostPersons &&
                CorpsControl.CanControlSettlement(world, seat, context, place.Id)).Select(place => place.Id).ToHashSet();
            foreach (var place in Session.View.Settlements.Where(s => validPlaces.Contains(s.Id)).OrderBy(s => s.Id))
                _settlements.AddItem(Texts.Resolve(place.NameKey), place.Id);
        }
        if (InitialSettlementId is int selected)
            for (int i = 0; i < _settlements.ItemCount; i++)
                if (_settlements.GetItemId(i) == selected) { _settlements.Select(i); break; }
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        _projects = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(_projects);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; column.AddChild(_status);
        var close = new Button { Text = "关闭", CustomMinimumSize = new(96, 34) }; close.Pressed += Closed; column.AddChild(close);
        Refresh();
    }

    private void Refresh()
    {
        foreach (Node child in _projects.GetChildren()) { _projects.RemoveChild(child); child.QueueFree(); }
        int? forceId = Session.View.PlayerForceId;
        var force = Session.Snapshot.Forces.FirstOrDefault(f => f.Id == forceId);
        if (force is null) { _status.Text = "当前没有可操作的势力。"; return; }
        _status.Text = force.ActiveResearch is { } active
            ? $"正在研究 {Session.ResearchProjectName(active.ProjectId)}，剩余 {active.DaysRemaining} 日。"
            : "选择据点和研究项目。费用从该据点所属军团支付。";
        foreach (var project in Session.ResearchProjects)
        {
            bool completed = force.CompletedResearch.Contains(project.Id);
            var missing = project.Prerequisites.Where(id => !force.CompletedResearch.Contains(id)).ToArray();
            string prerequisites = missing.Length == 0 ? "" : $"　需要 {string.Join("、", missing.Select(Session.ResearchProjectName))}";
            string state = completed ? "　已完成" : prerequisites;
            var row = new HBoxContainer(); _projects.AddChild(row);
            var label = new Label { Text = $"{Session.ResearchProjectName(project.Id)}　资金 {project.Cost}　{project.Days} 日{state}", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(label);
            var start = new Button { Text = "研究", Disabled = completed || missing.Length > 0 || force.ActiveResearch is not null || _settlements.ItemCount == 0 };
            row.AddChild(start); start.Pressed += () => Start(force.Id, project.Id);
        }
    }

    private void Start(int forceId, string projectId)
    {
        int settlementId = _settlements.GetSelectedId();
        var command = new StartResearchCommand(Guid.NewGuid(), forceId, Session.Snapshot.Simulation.NextSequence, settlementId, projectId);
        var result = Session.Execute(new("player"), command);
        Refresh();
        if (!result.Submission.Accepted)
            _status.Text = result.Submission.Rejection == CommandRejection.InsufficientFunds ? "研究失败，资金不足。" :
                $"研究失败，请检查当前回合、据点控制权和研究条件（{result.Submission.Rejection}）。";
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree()) return;
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } ||
            input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        {
            GetViewport().SetInputAsHandled(); Closed();
        }
    }
    public override void _GuiInput(InputEvent input) => AcceptEvent();
}
