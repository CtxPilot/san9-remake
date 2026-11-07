using Godot;

namespace San9.Client.Godot;

/// <summary>Selection is a separate indicator; the row only presses during the pointer gesture.</summary>
public partial class MapObjectRow : InformationMenuButton
{
    public static MapObjectRow Create(MapObjectChoice choice, TextureScope textures, float width,
        bool selectable, bool selected)
    {
        var row = new MapObjectRow { Textures = textures, Title = choice.Name, ClipTitle = true,
            SkinPrefix = "map-object-row", TextAlignment = HorizontalAlignment.Left,
            TextInsets = new(selectable ? 36 : 16, 0, 0, 0), CustomMinimumSize = new(width, 20),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, ShowSelection = selectable, Selected = selected };
        int markerX = selectable ? 22 : 2;
        var marker = new ColorRect { Color = new("080808"), Position = new(markerX, 4), Size = new(12, 12), MouseFilter = MouseFilterEnum.Ignore };
        marker.AddChild(new ColorRect { Color = choice.Color, Position = Vector2.One, Size = new(10, 10), MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(marker);
        return row;
    }

    public bool ShowSelection { get; init; }
    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; QueueRedraw(); }
    }
    public override void _Ready()
    {
        if (ShowSelection) Textures.Request(["catalog-checkbox", "catalog-checkbox-2"]);
        base._Ready();
    }
    public override void _Draw()
    {
        base._Draw();
        if (ShowSelection)
            DrawTexture(Textures.Get(Selected ? "catalog-checkbox-2" : "catalog-checkbox", this), new(2, 2));
    }
}
