using Godot;
using San9.Application;
using San9.Content.Text;
using San9.Core.Simulation;

namespace San9.Client.Godot;

public partial class EquipmentProductionView : ModalView
{
    public GameSession Session { get; init; } = null!;
    public TextCatalog Texts { get; init; } = null!;
    public Action Closed { get; init; } = null!;
    public int SettlementId { get; init; }
    private VBoxContainer _recipes = null!;
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
        var place = Session.View.Settlements.Single(s => s.Id == SettlementId);
        var title = new Label { Text = $"装备制造　{place.ResolveName(Texts)}" }; title.AddThemeFontSizeOverride("font_size", 24); column.AddChild(title);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        _recipes = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(_recipes);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; column.AddChild(_status);
        var close = new Button { Text = "关闭", CustomMinimumSize = new(96, 34) }; close.Pressed += Closed; column.AddChild(close);
        Refresh();
    }

    private void Refresh()
    {
        foreach (Node child in _recipes.GetChildren()) { _recipes.RemoveChild(child); child.QueueFree(); }
        var force = Session.Snapshot.Forces.FirstOrDefault(f => f.Id == Session.View.PlayerForceId);
        if (force is null) { _status.Text = "当前没有可操作的势力。"; return; }
        _status.Text = force.EquipmentProduction is { } active
            ? $"正在制造 {Session.EquipmentRecipes.FirstOrDefault(r => r.Id == active.DefinitionId)?.Name ?? active.DefinitionId}，剩余 {active.DaysRemaining} 日。"
            : "选择装备；完成后进入势力库存。资金由据点所属军团支付。";
        foreach (var recipe in Session.EquipmentRecipes)
        {
            bool unlocked = force.CompletedResearch.Contains(recipe.ResearchProjectId!);
            var row = new HBoxContainer(); _recipes.AddChild(row);
            var bonus = recipe.BaseBonuses;
            string effect = $"统率+{bonus.Leadership} 武力+{bonus.War} 智力+{bonus.Intelligence} 政治+{bonus.Politics}";
            var label = new Label { Text = $"{recipe.Name}　资金 {recipe.ProductionCost}　{recipe.ProductionDays} 日　{effect}" +
                (unlocked ? "" : $"　需要 {Session.ResearchProjectName(recipe.ResearchProjectId!)}"), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(label);
            var start = new Button { Text = "制造", Disabled = !unlocked || force.EquipmentProduction is not null };
            row.AddChild(start); start.Pressed += () => Start(force.Id, recipe.Id);
        }
    }

    private void Start(int forceId, string id)
    {
        var command = new StartEquipmentProductionCommand(Guid.NewGuid(), forceId, Session.Snapshot.Simulation.NextSequence, SettlementId, id);
        var result = Session.Execute(new("player"), command);
        Refresh();
        if (!result.Submission.Accepted)
            _status.Text = result.Submission.Rejection == CommandRejection.InsufficientFunds ? "制造失败，资金不足。" : "制造失败，请检查当前回合、据点和研究条件。";
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree()) return;
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } ||
            input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        { GetViewport().SetInputAsHandled(); Closed(); }
    }
    public override void _GuiInput(InputEvent input) => AcceptEvent();
}
