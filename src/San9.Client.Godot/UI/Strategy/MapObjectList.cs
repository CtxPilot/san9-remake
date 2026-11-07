using Godot;

namespace San9.Client.Godot;

/// <summary>A persistent map-object list using the same rows and frame as overlapping-object selection.</summary>
public partial class MapObjectList : Control
{
    public TextureScope Textures { get; init; } = null!;
    public Action<MapObjectTarget>? Removed { get; init; }
    public MapObjectTarget? PreviewTarget { get; private set; }
    private ScrollContainer _scroll = null!;
    private VBoxContainer _rows = null!;
    private int _count;
    private float _rowWidth = 96;
    private bool _needsLayout = true;
    private readonly Dictionary<MapObjectTarget, (MapObjectChoice Choice, MapObjectRow Row)> _items = [];

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
        _scroll.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 0);
        _scroll.AddChild(_rows); AddChild(_scroll);
        VisibilityChanged += () => { if (!IsVisibleInTree()) PreviewTarget = null; };
    }

    public void SetChoices(IReadOnlyList<MapObjectChoice> choices)
    {
        int offset = _scroll.ScrollVertical;
        var remaining = choices.Select(choice => choice.Target).ToHashSet();
        if (PreviewTarget is { } hovered && !remaining.Contains(hovered)) PreviewTarget = null;
        foreach (var target in _items.Keys.Where(target => !remaining.Contains(target)).ToArray())
        {
            var row = _items[target].Row;
            _items.Remove(target); _rows.RemoveChild(row); row.QueueFree();
        }
        _count = choices.Count;
        _needsLayout = true;
        var font = GetThemeFont("font");
        _rowWidth = Math.Max(96, choices.Select(choice => font.GetStringSize(choice.Name, fontSize: 16).X + 16).DefaultIfEmpty(96).Max());
        for (int i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            if (_items.TryGetValue(choice.Target, out var existing) && existing.Choice != choice)
            {
                _rows.RemoveChild(existing.Row); existing.Row.QueueFree(); _items.Remove(choice.Target);
            }
            if (!_items.TryGetValue(choice.Target, out var item))
            {
                var row = MapObjectRow.Create(choice, Textures, _rowWidth, selectable: false, selected: true);
                row.Pressed += () => Removed?.Invoke(choice.Target);
                row.MouseEntered += () => PreviewTarget = choice.Target;
                row.MouseExited += () => { if (PreviewTarget == choice.Target) PreviewTarget = null; };
                _items.Add(choice.Target, item = (choice, row)); _rows.AddChild(row);
            }
            _rows.MoveChild(item.Row, i);
        }
        _scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, offset);
        QueueRedraw();
    }

    public void Fit(float maximumWidth, float maximumHeight)
    {
        float height = Math.Min(_count * 20, Math.Max(20, maximumHeight - 24));
        float scrollbar = _count * 20 > height ? _scroll.GetVScrollBar().GetCombinedMinimumSize().X : 0;
        var size = new Vector2(Math.Min(_rowWidth + scrollbar + 24, maximumWidth), height + 24);
        if (!_needsLayout && Size == size) return;
        _needsLayout = false;
        float rowWidth = Math.Max(0, size.X - 24 - scrollbar);
        foreach (var item in _items.Values) item.Row.CustomMinimumSize = new(rowWidth, 20);
        Size = size;
        _scroll.Position = new(12, 12); _scroll.Size = size - new Vector2(24, 24);
        QueueRedraw();
    }
    public override void _Draw() => SkinPainter.MapObjectPicker(this, Textures, new(Vector2.Zero, Size));
}
