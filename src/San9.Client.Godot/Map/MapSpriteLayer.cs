using Godot;
using San9.Rendering;

namespace San9.Client.Godot;

internal partial class MapSpriteLayer : Node2D
{
    private sealed class Entry(Sprite2D sprite)
    {
        public Sprite2D Sprite = sprite;
        public Texture2D? Texture;
        public Vector2 Position;
        public Vector2 Scale = Vector2.One;
        public Material? Material;
        public Color Modulate = Colors.White;
        public bool Visible;
    }
    private readonly List<Entry> _sprites = [];
    private readonly Dictionary<(int Direction, int Arc), ShaderMaterial> _visionMaterials = [];
    private Shader _visionShader = null!;
    private ImageTexture _visionTexture = null!;
    private int _cursor, _visible;

    public override void _Ready()
    {
        _visionShader = GD.Load<Shader>("res://shaders/map_vision.gdshader");
        using var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        _visionTexture = ImageTexture.CreateFromImage(image);
    }

    public void Begin() => _cursor = 0;
    public void Add(Texture2D texture, Vector2 position, Material? material, Color? modulate = null)
        => Add(texture, position, Vector2.One, material, modulate ?? Colors.White);

    public void Add(MapVisionOverlay vision)
    {
        var item = Next();
        var parameters = (vision.Direction, vision.ArcDegrees);
        if (!_visionMaterials.TryGetValue(parameters, out var material))
        {
            material = new ShaderMaterial { Shader = _visionShader };
            material.SetShaderParameter("facing", vision.Direction * Mathf.Pi / 4);
            material.SetShaderParameter("vision_angle", Mathf.DegToRad(vision.ArcDegrees));
            _visionMaterials.Add(parameters, material);
        }
        Set(item, _visionTexture, new((float)(vision.X - vision.HalfWidth), (float)(vision.Y - vision.HalfHeight)),
            new((float)(2 * vision.HalfWidth), (float)(2 * vision.HalfHeight)), material, Colors.White);
    }

    private Entry Next()
    {
        if (_cursor == _sprites.Count)
        { var sprite = new Sprite2D { Centered = false, Visible = false }; AddChild(sprite); _sprites.Add(new(sprite)); }
        return _sprites[_cursor++];
    }

    private void Add(Texture2D texture, Vector2 position, Vector2 scale, Material? material, Color color)
        => Set(Next(), texture, position, scale, material, color);

    private static void Set(Entry item, Texture2D texture, Vector2 position, Vector2 scale, Material? material, Color color)
    {
        if (item.Texture != texture) { item.Sprite.Texture = texture; item.Texture = texture; }
        if (item.Position != position) { item.Sprite.Position = position; item.Position = position; }
        if (item.Scale != scale) { item.Sprite.Scale = scale; item.Scale = scale; }
        if (!item.Visible) { item.Sprite.Visible = true; item.Visible = true; }
        if (item.Material != material) { item.Sprite.Material = material; item.Material = material; }
        if (item.Modulate != color) { item.Sprite.Modulate = color; item.Modulate = color; }
    }
    public void End()
    {
        for (int i = _cursor; i < _visible; i++)
        {
            var item = _sprites[i]; item.Sprite.Texture = null; item.Texture = null;
            item.Sprite.Visible = false; item.Visible = false;
        }
        _visible = _cursor;
    }
    public void Clear() { _cursor = 0; End(); }

    public override void _ExitTree()
    {
        Clear();
        foreach (var item in _sprites) item.Sprite.Material = null;
        foreach (var material in _visionMaterials.Values) material.Dispose();
        _visionTexture.Dispose();
    }
}
