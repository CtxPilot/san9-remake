using Godot;
using San9.Application;
using San9.Application.Presentation;
using San9.Content.Text;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Client.Godot;

public partial class OfficerEquipmentView : ModalView
{
    public GameSession Session { get; init; } = null!;
    public TextCatalog Texts { get; init; } = null!;
    public int OfficerId { get; init; }
    public Action Closed { get; set; } = null!;
    private VBoxContainer _slots = null!;
    private Label _status = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop;
        Theme = CompanionPanelStyle.Create(GetThemeFont("font"));
        var backdrop = new ColorRect { Color = new(0, 0, 0, .65f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 55, OffsetRight = -55, OffsetTop = 32, OffsetBottom = -32 };
        AddChild(panel);
        var margin = new MarginContainer();
        foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 16);
        panel.AddChild(margin);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 8); margin.AddChild(column);
        var person = Session.View.Officers.Single(o => o.Id == OfficerId);
        var title = new Label { Text = $"装备　{Texts.Resolve(person.NameKey)}" }; title.AddThemeFontSizeOverride("font_size", 24); column.AddChild(title);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        _slots = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _slots.AddThemeConstantOverride("separation", 10); scroll.AddChild(_slots);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new(0, 32) }; column.AddChild(_status);
        var close = new Button { Text = "关闭", CustomMinimumSize = new(96, 34) }; close.Pressed += Closed; column.AddChild(close);
        Refresh();
    }

    private void Refresh()
    {
        foreach (Node child in _slots.GetChildren()) { _slots.RemoveChild(child); child.QueueFree(); }
        var data = OfficerEquipment.Read(Session, Texts, OfficerId);
        _status.Text = data.CanManage ? "选择持有或库存装备；宝物请通过赏赐和没收转移。" : "当前武将的装备仅供查看。";
        foreach (var slot in data.Slots)
        {
            var group = new VBoxContainer(); _slots.AddChild(group);
            var row = new HBoxContainer(); group.AddChild(row);
            row.AddChild(new Label { Text = slot.Name, CustomMinimumSize = new(90, 32), VerticalAlignment = VerticalAlignment.Center });
            var choices = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            choices.Name = $"EquipmentChoice_{slot.Slot}";
            row.AddChild(choices); choices.AddItem("— 选择装备 —");
            foreach (var item in slot.Items)
            {
                string rarity = item.Rarity switch { EquipmentRarity.Ordinary => "普通", EquipmentRarity.Precious => "珍贵", _ => "稀世" };
                string progress = item.Slot is EquipmentSlot.Mount or EquipmentSlot.Treasure ? "固定等级" :
                    item.Level == 5 ? "满级" : $"经验 {item.Experience}/100";
                choices.AddItem($"{item.Name}　{rarity}　{item.Level}级　{progress}" +
                    (item.Equipped ? "　已装备" : item.InInventory ? "　库存" : "　持有"));
            }
            int equipped = Array.FindIndex(slot.Items, item => item.Equipped);
            choices.Select(equipped < 0 ? 0 : equipped + 1);
            var equip = new Button { Name = $"EquipmentApply_{slot.Slot}", Text = "装备" }; row.AddChild(equip);
            var clear = new Button { Text = "卸下", Disabled = !data.CanManage || equipped < 0 }; row.AddChild(clear);
            var store = new Button { Text = "入库" }; row.AddChild(store);
            var detail = new Label { CustomMinimumSize = new(0, 24), AutowrapMode = TextServer.AutowrapMode.WordSmart }; group.AddChild(detail);
            OfficerEquipmentItem? Selected() => choices.Selected > 0 ? slot.Items[choices.Selected - 1] : null;
            void UpdateSelection()
            {
                var item = Selected();
                equip.Disabled = item is null || item.Equipped || !item.CanEquip;
                store.Disabled = !data.CanManage || item is null || item.InInventory || item.IsTreasure;
                detail.Text = item is null ? "未选择装备。" : item.Effect + (item.UnavailableReason is null || item.Equipped ? "" : "　" + item.UnavailableReason);
            }
            choices.ItemSelected += _ => UpdateSelection();
            equip.Pressed += () => { if (Selected() is { } item) Configure(slot.Slot, item.Id); };
            clear.Pressed += () => Configure(slot.Slot, null);
            store.Pressed += () => { if (Selected() is { } item) Store(item.Id); };
            UpdateSelection();
        }
    }

    private void Configure(EquipmentSlot slot, long? item)
    {
        int force = Session.View.PlayerForceId!.Value;
        var result = Session.Execute(new("player"), new ConfigureEquipmentCommand(Guid.NewGuid(), force,
            Session.Snapshot.Simulation.NextSequence, OfficerId, slot, item));
        Refresh();
        if (!result.Submission.Accepted) _status.Text = "操作失败，请检查回合、武将和装备条件。";
    }

    private void Store(long id)
    {
        int force = Session.View.PlayerForceId!.Value;
        var result = Session.Execute(new("player"), new TransferEquipmentCommand(Guid.NewGuid(), force,
            Session.Snapshot.Simulation.NextSequence, id, null));
        Refresh();
        if (!result.Submission.Accepted) _status.Text = "无法收回装备，请检查回合和武将状态。";
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
