using Godot;
using San9.Application.Authoring;
using San9.Application.Presentation;
using San9.Content.Interface;

namespace San9.Client.Godot;

/// <summary>A single city choice shared by its map and catalog; neither view changes the opening draft.</summary>
public partial class OpeningCityPicker : ModalView
{
    public string Title { get; init; } = "选择都市";
    public NewGameDraft Draft { get; init; } = null!;
    public OpeningMapDefinition Geography { get; init; } = null!;
    public TextureScope Textures { get; init; } = null!;
    public TextureService Service { get; init; } = null!;
    public int[] Candidates { get; init; } = [];
    public int? InitialCurrentId { get; init; }
    public Action<int> Confirmed { get; init; } = null!;
    public Action Closed { get; init; } = null!;
    private OpeningCityCatalog _catalog = null!;
    private TextureScope _mapTextures = null!;
    private CatalogView? _list;
    private readonly CatalogTableState _table = new();
    private readonly GameTextPresenter _text = new();
    private readonly List<Label> _values = [];
    private static readonly string[] Fields = ["name", "province", "trait", "force", "serving", "durability", "public-order", "commerce", "cultivation", "population", "draft", "money-income", "food-income"];
    private static readonly int[] FieldY = [132, 180, 212, 244, 276, 324, 356, 388, 420, 468, 500, 548, 580];
    private string Hint => CreateForceHint ? "选择新势力的所在都市。" : "选择新武将的部署都市。";
    public bool CreateForceHint { get; init; } = true;
    public override void _Ready()
    {
        Size = new(1024, 768); MouseFilter = MouseFilterEnum.Stop;
        _catalog = new(Draft);
        _mapTextures = Service.CreateScope(Service.LibraryAssets);
        string marker = Textures.Assets["opening-city-marker"].Id;
        _mapTextures.Request(Geography.Assets.Append(marker));
        var map = new OpeningMapView { Definition = Geography, Textures = _mapTextures, MarkerAsset = marker,
            CityColors = Draft.Selection().CityColors, Targets = Candidates.ToDictionary(id => id),
            Position = new(108, 112), Hovered = Summary, Activated = Confirmed };
        AddChild(map);
        Label(Title, new(84, 40), new(128, 32), 24);
        string[] labels = ["都市", "州", "特性", "势力", "在任", "耐久", "民心", "收益", "收获", "人口", "兵役人口", "资金收入", "兵粮收入"];
        for (int i = 0; i < labels.Length; i++)
        {
            Label(labels[i], new(710, FieldY[i]), new(98, 24));
            _values.Add(Label("", new(822, FieldY[i]), new(88, 24)));
        }
        ActionButton("一览", new(612, 674), new(112, 32), ShowList, map: true);
        ActionButton("中止", new(808, 690), new(80, 48), Closed);
        var close = new TextureButton { Position = new(928, 62), TextureNormal = Textures.Get("information-action-close", this),
            TextureHover = Textures.Get("information-action-close-1", this), TexturePressed = Textures.Get("information-action-close-2", this) };
        close.Pressed += Closed; AddChild(close);
        AddChild(new TextureRect { Position = new(896, 38), Texture = Textures.Get("catalog-help", this), MouseFilter = MouseFilterEnum.Ignore });
        Label(Hint, new(0, 736), new(1024, 24));
    }
    private void Summary(int? id)
    {
        for (int i = 0; i < _values.Count; i++) _values[i].Text = id is int city ? _catalog.Cell(city, Fields[i]).Text : "";
    }
    private Label Label(string title, Vector2 position, Vector2 size, int font = 16)
    {
        var label = new Label { Text = title, Position = position, Size = size, ClipText = true,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(label); _text.Apply(label, new(font, Colors.White, GameTextEffect.Outline, Colors.Black)); return label;
    }
    private void ActionButton(string title, Vector2 position, Vector2 size, Action action, bool map = false)
    {
        var button = new InformationActionButton { Title = title, Textures = Textures, Position = position, Size = size,
            MapToggle = map, TextRole = map ? GameButtonTextRole.MapMode : GameButtonTextRole.ConfirmNo };
        button.Pressed += action; AddChild(button);
    }
    private void ShowList()
    {
        foreach (var child in GetChildren().OfType<CanvasItem>()) child.Hide();
        _list = new CatalogView { Title = Title, HintText = Hint, CloseTitle = "中止", Service = Service, Textures = Textures,
            WindowPainter = (canvas, body) => OpeningWindowPainter.Draw(canvas, Textures, body, pairedActions: false),
            TitleWidth = 208, MinimumBodyWidth = 800, MapButtonWidth = 112, TabStripFollowsPage = true,
            Pages = OpeningCityCatalog.Pages, ReadRows = page => _catalog.Read(Candidates, page),
            TableState = _table, InitialCurrentId = _table.Current is null ? InitialCurrentId : null, Activated = Confirmed, Closed = Closed, ShowMap = CloseList };
        AddChild(_list); QueueRedraw();
    }
    private void CloseList()
    {
        RemoveChild(_list!); _list!.QueueFree(); _list = null;
        foreach (var child in GetChildren().OfType<CanvasItem>()) child.Show();
        QueueRedraw();
    }
    public override void _Input(InputEvent input)
    {
        if (_list is not null || !IsVisibleInTree()) return;
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } ||
            input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        { GetViewport().SetInputAsHandled(); Closed(); }
    }
    public override void _Draw()
    {
        if (_list is not null) return;
        DrawRect(new(0, 0, 1024, 768), new(0, 0, 0, .45f));
        if (!Textures.IsReady) { Textures.BindRedraw(this); return; }
        OpeningWindowPainter.Draw(this, Textures, new(56, 30, 912, 708), pairedActions: false);
        DrawTexture(Textures.Get("opening-panel", this), new(88, 92));
    }
    public override void _ExitTree() { _mapTextures.Dispose(); _text.Dispose(); }
}
