using Godot;
using San9.Rendering;

namespace San9.Client.Godot;

public sealed record MapObjectChoice(MapObjectTarget Target, string Name, uint Soldiers, Color Color);

/// <summary>Dismissal consumes input and leaves the underlying command selection active.</summary>
public partial class MapObjectPicker : ModalView
{
    public TextureScope Textures { get; init; } = null!;
    public Action<MapObjectTarget?>? Preview { get; init; }
    private MapObjectPickRequest? _request;
    private ScrollContainer? _scroll;
    private Rect2 _panel;
    private Vector2 _anchor;
    private int _count;
    private bool _rightPressed;
    private readonly GameTextPresenter _text = new();
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
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 0);
        _scroll.AddChild(rows); AddChild(_scroll);
        foreach (var choice in choices)
        {
            var row = new InformationMenuButton { Textures = Textures, Title = choice.Name, ClipTitle = true,
                TextInsets = new(24, 0, 88, 0), CustomMinimumSize = new(288, 24),
                SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.Pressed += () => { Close(cancel: false); request.TrySelect(choice.Target); };
            row.MouseEntered += () => SetPreview(choice.Target);
            row.MouseExited += () => { if (PreviewTarget == choice.Target) SetPreview(null); };
            rows.AddChild(row);
            row.AddChild(new ColorRect { Color = choice.Color, Position = new(6, 6), Size = new(12, 12),
                MouseFilter = MouseFilterEnum.Ignore });
            var soldiers = new Label { Text = choice.Soldiers.ToString(System.Globalization.CultureInfo.InvariantCulture),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore };
            row.AddChild(soldiers);
            soldiers.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            soldiers.AnchorLeft = 1;
            soldiers.OffsetLeft = -80; soldiers.OffsetRight = -8;
            _text.Apply(soldiers, GameTextStyles.Building(false));
        }
        Relayout(); Show();
    }

    private void SetPreview(MapObjectTarget? target)
    { PreviewTarget = target; Preview?.Invoke(target); }

    private void Relayout()
    {
        Size = GetViewportRect().Size;
        float height = Math.Min(_count * 24, Math.Max(24, Size.Y - 96));
        _panel = new(new(Math.Clamp(_anchor.X - 18, 0, Math.Max(0, Size.X - 352)),
            Math.Clamp(_anchor.Y - 18, 0, Math.Max(0, Size.Y - 32 - height - 32))), new(352, height + 32));
        if (_scroll is not null) { _scroll.Position = _panel.Position + new Vector2(16, 16); _scroll.Size = new(320, height); }
        QueueRedraw();
    }

    public void Close(bool cancel = true)
    {
        Hide();
        if (cancel) _request?.Cancel();
        _request = null; _rightPressed = false; SetPreview(null);
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

    public override void _Draw() => SkinPainter.InformationMenu(this, Textures, _panel);
    public override void _ExitTree()
    { GetViewport().SizeChanged -= Relayout; _request?.Cancel(); Preview?.Invoke(null); _text.Dispose(); }
}
