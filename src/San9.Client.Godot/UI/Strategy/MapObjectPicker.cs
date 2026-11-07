using Godot;
using San9.Rendering;

namespace San9.Client.Godot;

public sealed record MapObjectChoice(MapObjectTarget Target, string Name, Color Color);

/// <summary>Dismissal consumes input and leaves the underlying command selection active.</summary>
public partial class MapObjectPicker : ModalView
{
    public TextureScope Textures { get; init; } = null!;
    private MapObjectPickRequest? _request;
    private ScrollContainer? _scroll;
    private Rect2 _panel;
    private Vector2 _anchor;
    private int _count;
    private float _rowWidth = 96;
    private bool _rightPressed;
    private InformationActionButton? _confirm;
    public MapObjectTarget? PreviewTarget { get; private set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Size = GetViewportRect().Size; Hide();
        GetViewport().SizeChanged += Relayout;
    }

    public void Open(MapObjectPickRequest request, IReadOnlyList<MapObjectChoice> choices, Vector2 point)
    {
        Close();
        _request = request; _anchor = point; _count = choices.Count;
        var font = GetThemeFont("font");
        _rowWidth = Math.Max(96, choices.Max(choice => font.GetStringSize(choice.Name, fontSize: 16).X + (request.Multiple ? 36 : 16)));
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 0);
        _scroll.AddChild(rows); AddChild(_scroll);
        foreach (var choice in choices)
        {
            var row = MapObjectRow.Create(choice, Textures, _rowWidth, request.Multiple, request.IsSelected(choice.Target));
            row.Pressed += () =>
            {
                if (request.Multiple) { request.Toggle(choice.Target); row.Selected = request.IsSelected(choice.Target); }
                else { Close(cancel: false); request.TrySelect(choice.Target); }
            };
            row.MouseEntered += () => SetPreview(choice.Target);
            row.MouseExited += () => { if (PreviewTarget == choice.Target) SetPreview(null); };
            rows.AddChild(row);

        }
        if (request.Multiple)
        {
            _confirm = new InformationActionButton { Title = "决定", SkinPrefix = "catalog-accept-", FontSize = 16, Textures = Textures, CustomMinimumSize = new(80, 28) };
            Textures.RequestPrefixes("catalog-accept-");
            _confirm.Pressed += () => { Close(cancel: false); request.Confirm(); };
            AddChild(_confirm);
        }
        Relayout(); Show();
    }

    private void SetPreview(MapObjectTarget? target)
    { PreviewTarget = target; }

    private void Relayout()
    {
        Size = GetViewportRect().Size;
        float height = Math.Min(_count * 20, Math.Max(20, Size.Y - (_confirm is null ? 56 : 88)));
        float scrollbar = _count * 20 > height ? _scroll!.GetVScrollBar().GetCombinedMinimumSize().X : 0;
        float width = Math.Min(_rowWidth + scrollbar + 24, Size.X);
        _panel = new(new(Math.Clamp(_anchor.X, 0, Math.Max(0, Size.X - width)),
            Math.Clamp(_anchor.Y, 0, Math.Max(0, Size.Y - 32 - height - 24))), new(width, height + (_confirm is null ? 24 : 56)));
        if (_confirm is not null) { _confirm.Position = _panel.Position + new Vector2(12, height + 18); _confirm.Size = new(width-24, 28); }
        if (_scroll is not null)
        {
            _scroll.Position = _panel.Position + new Vector2(12, 12); _scroll.Size = new(width - 24, height);
            foreach (var row in _scroll.GetChild<VBoxContainer>(0).GetChildren().OfType<Control>())
                row.CustomMinimumSize = new(Math.Max(0, width - 24 - scrollbar), 20);
        }
        QueueRedraw();
    }

    public void Close(bool cancel = true)
    {
        Hide();
        if (cancel) _request?.Cancel();
        if (_confirm is not null) { RemoveChild(_confirm); _confirm.QueueFree(); _confirm = null; }
        _request = null; _count = 0; _rightPressed = false; SetPreview(null);
        if (_scroll is not null) { RemoveChild(_scroll); _scroll.QueueFree(); _scroll = null; }
    }

    public override void _Process(double delta)
    { if (Visible && _request?.IsCurrent != true) Close(); }

    public override void _Input(InputEvent input)
    {
        if (!Visible) return;
        if (input is InputEventKey { Pressed: true, Keycode: Key.Escape })
        { Close(); GetViewport().SetInputAsHandled(); }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right } mouse)
        {
            if (mouse.Pressed) _rightPressed = true;
            else if (_rightPressed) Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } mouse &&
            !_panel.HasPoint(mouse.Position)) Close();
        AcceptEvent();
    }

    public override void _Draw() => SkinPainter.MapObjectPicker(this, Textures, _panel);
    public override void _ExitTree()
    { GetViewport().SizeChanged -= Relayout; _request?.Cancel(); }
}
