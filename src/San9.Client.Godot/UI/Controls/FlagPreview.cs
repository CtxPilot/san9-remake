using Godot;
using San9.Rendering;

namespace San9.Client.Godot;

public partial class FlagPreview : Control
{
    public TextureService Service { get; init; } = null!;
    public int Glyph { get; init; } = -1;
    public int ColorCode { get; init; }
    public float PreviewScale { get; init; } = 1;
    private TextureScope? _textures;
    private string[] _frames = [];

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = Size = new Vector2(68, 52) * PreviewScale;
        _frames = FlagVisuals.Layers(5, ColorCode, Glyph).Select(layer => Service.AnimationClips[layer].Frames[^1]).ToArray();
        _textures = Service.CreateScope(Service.MapAssets); _textures.Request(_frames);
    }

    public override void _Draw()
    {
        if (_textures is null) return;
        DrawSetTransform(Vector2.Zero, 0, Vector2.One * PreviewScale);
        foreach (string frame in _frames) DrawTexture(_textures.Get(frame, this), Vector2.Zero);
        DrawSetTransform(Vector2.Zero);
    }

    public override void _ExitTree() => _textures?.Dispose();
}
