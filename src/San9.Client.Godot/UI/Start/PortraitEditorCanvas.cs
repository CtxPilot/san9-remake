using Godot;
using San9.Content.Images;
using San9.Application.Authoring;

namespace San9.Client.Godot;

internal sealed class PortraitEditorImage : IDisposable
{
    private readonly TextureService _service;
    private readonly TextureScope? _scope;
    private readonly ImageTexture? _texture;
    private readonly string _key;
    public PortraitEditorImage(TextureService service, PortraitDefinition portrait, byte[]? pending, string path)
    {
        _service = service; _key = portrait.Image.Id;
        if (pending is null && service.Portraits.TryGetValue(_key, out var published) && published.Image == portrait.Image)
        {
            _scope = service.CreatePortraitScope(_key, PortraitView.Full); _scope.Request([_key]);
        }
        else
        {
            using var image = new Image();
            var error = pending is null ? image.Load(path) : image.LoadPngFromBuffer(pending);
            if (error != global::Godot.Error.Ok || image.GetWidth() != portrait.Image.Width || image.GetHeight() != portrait.Image.Height)
                throw new InvalidDataException("头像预览解码失败或尺寸不符。");
            _texture = ImageTexture.CreateFromImage(image);
        }
    }
    public Texture2D? Get(CanvasItem consumer) => _texture ?? _scope?.Get(_key, consumer);
    public string? Error => _scope?.Error?.Message;
    public void Dispose() { _scope?.Dispose(); if (_texture is not null) _service.Defer(_texture.Dispose); }
}

public partial class PortraitEditorCanvas : Control
{
    internal PortraitEditorImage? Source { get; set; }
    public PortraitDefinition? Portrait { get; set; }
    public PortraitView EditingView { get; set; } = PortraitView.Face;
    public bool MoveBoth { get; set; }
    public Action<PortraitDefinition, bool> AnchorChanged { get; set; } = null!;
    public Action<PortraitView>? ViewChanged { get; set; }
    public Action? ManualEditStarted { get; set; }
    public PortraitFaceCandidate[] Candidates { get; set; } = [];
    public Action<int>? FaceSelected { get; set; }
    private float _zoom = 1;
    private Vector2 _pan, _press, _startPan;
    private PortraitDefinition? _startPortrait;
    private bool _drag, _panning;
    private float ImageScale => Math.Max(1, Math.Min(Size.X - 32, Size.Y - 32)) / (Portrait?.Image.Width ?? 240) * _zoom;
    private Rect2 ImageRect => new((Size - Vector2.One * (Portrait?.Image.Width ?? 240) * ImageScale) / 2 + _pan,
        Vector2.One * (Portrait?.Image.Width ?? 240) * ImageScale);

    public override void _Ready()
    {
        ClipContents = true; FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.Drag;
        FocusExited += CancelGesture;
        Resized += () => { CancelGesture(); QueueRedraw(); };
    }
    public void ResetView() { CancelGesture(); _zoom = 1; _pan = Vector2.Zero; QueueRedraw(); }
    public void CancelGesture()
    {
        if (_drag && Portrait is not null)
        {
            Portrait = _startPortrait!;
            AnchorChanged(Portrait, false);
        }
        _drag = _panning = false; QueueRedraw();
    }
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut) CancelGesture();
    }
    public override void _Draw()
    {
        DrawRect(new(Vector2.Zero, Size), new Color("121923"));
        if (Portrait is null || Source is null) return;
        var target = ImageRect;
        var texture = Source.Get(this);
        if (texture is not null) DrawTextureRect(texture, target, false);
        foreach (var (view, color) in new[] { (PortraitView.Bust, new Color("ffd280")), (PortraitView.Face, new Color("72d8e8")) })
        {
            var region = Portrait.GetView(view);
            var box = new Rect2(target.Position + new Vector2(region.X - Portrait.Image.X, region.Y - Portrait.Image.Y) * ImageScale,
                new Vector2(region.Width, region.Height) * ImageScale);
            DrawRect(box, color, false, MoveBoth || view == EditingView ? 3 : 1);
        }
        for (int i = 0; i < Candidates.Length; i++)
        {
            var face = Candidates[i];
            var box = new Rect2(target.Position + new Vector2((float)face.Left, (float)face.Top) * target.Size,
                new Vector2((float)face.Width, (float)face.Height) * target.Size);
            DrawRect(box, new Color("a8ef91"), false, 2);
            DrawString(GetThemeDefaultFont(), box.Position + new Vector2(2, 16), (i + 1).ToString(), fontSize: 14);
        }
        var active = Portrait.GetView(EditingView);
        var origin = target.Position + new Vector2(active.X - Portrait.Image.X, active.Y - Portrait.Image.Y) * ImageScale;
        DrawLine(origin - new Vector2(8, 0), origin + new Vector2(8, 0), Colors.White, 1);
        DrawLine(origin - new Vector2(0, 8), origin + new Vector2(0, 8), Colors.White, 1);
    }
    public override void _GuiInput(InputEvent input)
    {
        if (Portrait is null) return;
        if (input is InputEventMouseButton mouse)
        {
            if (mouse.Pressed && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                CancelGesture();
                float factor = mouse.ButtonIndex == MouseButton.WheelUp ? 1.2f : 1 / 1.2f;
                float next = Math.Clamp(_zoom * factor, .25f, 8);
                _pan = (mouse.Position - Size / 2) - (mouse.Position - Size / 2 - _pan) * (next / _zoom);
                _zoom = next; QueueRedraw(); AcceptEvent();
            }
            else if (mouse.ButtonIndex == MouseButton.Middle)
            { _panning = mouse.Pressed; _press = mouse.Position; _startPan = _pan; AcceptEvent(); }
            else if (mouse.ButtonIndex == MouseButton.Left)
            {
                if (mouse.Pressed)
                {
                    GrabFocus();
                    if (Candidates.Length > 0)
                    {
                        var point = (mouse.Position - ImageRect.Position) / ImageRect.Size;
                        int index = Array.FindIndex(Candidates, face => new Rect2((float)face.Left, (float)face.Top,
                            (float)face.Width, (float)face.Height).HasPoint(point));
                        if (index >= 0) FaceSelected?.Invoke(index);
                        AcceptEvent(); return;
                    }
                    Rect2 Frame(PortraitView view)
                    {
                        var crop = Portrait.GetView(view);
                        return new(ImageRect.Position + new Vector2(crop.X - Portrait.Image.X, crop.Y - Portrait.Image.Y) * ImageScale,
                            new Vector2(crop.Width, crop.Height) * ImageScale);
                    }
                    PortraitView? hit = null;
                    // Borders select a crop; dragging inside honors the explicit view selector.
                    foreach (var view in new[] { PortraitView.Face, PortraitView.Bust })
                    {
                        var rect = Frame(view);
                        if (rect.HasPoint(mouse.Position) && !rect.Grow(-6).HasPoint(mouse.Position)) { hit = view; break; }
                    }
                    if (hit is null)
                        foreach (var view in new[] { EditingView, EditingView == PortraitView.Face ? PortraitView.Bust : PortraitView.Face })
                            if (Frame(view).HasPoint(mouse.Position)) { hit = view; break; }
                    if (hit is { } selected)
                    {
                        ManualEditStarted?.Invoke(); EditingView = selected; ViewChanged?.Invoke(selected);
                        _drag = true; _press = mouse.Position; _startPortrait = Portrait;
                    }
                }
                else if (_drag)
                { _drag = false; AnchorChanged(Portrait, true); }

                AcceptEvent();
            }
            else if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed) { CancelGesture(); AcceptEvent(); }
        }
        else if (input is InputEventMouseMotion motion)
        {
            if (_panning) { _pan = _startPan + motion.Position - _press; QueueRedraw(); }
            else if (_drag)
            {
                var delta = (motion.Position - _press) / ImageRect.Size;
                var origin = _startPortrait!.Anchor(EditingView);
                MoveAnchor(origin.X + delta.X, origin.Y + delta.Y, false);
            }
        }
        else if (input is InputEventKey { Pressed: true } key)
        {
            if (key.Keycode == Key.Escape && (_drag || _panning)) { CancelGesture(); AcceptEvent(); return; }
            var delta = key.Keycode switch { Key.Left => Vector2.Left, Key.Right => Vector2.Right,
                Key.Up => Vector2.Up, Key.Down => Vector2.Down, _ => Vector2.Zero };
            if (delta != Vector2.Zero)
            {
                CancelGesture();
                ManualEditStarted?.Invoke();
                double step = (key.ShiftPressed ? 10d : 1d) / Portrait.Image.Width;
                var anchor = Portrait.Anchor(EditingView).SnapToPixels(EditingView, Portrait.Image.Width, Portrait.Image.Height);
                MoveAnchor(anchor.X + delta.X * step, anchor.Y + delta.Y * step, true); AcceptEvent();
            }
        }
    }
    private void MoveAnchor(double x, double y, bool commit)
    {
        if (Portrait is null) return;
        var basis = _drag ? _startPortrait! : Portrait;
        var target = new PortraitAnchor(x, y);
        Portrait = MoveBoth ? basis.MoveTogether(EditingView, target)
            : basis.WithAnchor(EditingView, target.Clamp(EditingView).SnapToPixels(EditingView, basis.Image.Width, basis.Image.Height));
        AnchorChanged(Portrait, commit); QueueRedraw();
    }
}

public partial class PortraitEditorPreviews : Control
{
    internal PortraitEditorImage? Source { get; set; }
    public PortraitDefinition? Portrait { get; set; }
    public override void _Draw()
    {
        if (Portrait is null || Source is null) return;
        var texture = Source.Get(this);
        var font = GetThemeDefaultFont();
        float y = 20;
        foreach (var (view, width, height, label) in new[] {
            (PortraitView.Bust, 160, 180, "半身 8:9"),
            (PortraitView.Face, 96, 120, "面部 4:5") })
        {
            DrawString(font, new(0, y), label, fontSize: 14); y += 8;
            DrawPreview(texture, new(0, y, width, height), view); y += height + 26;
        }
    }
    private void DrawPreview(Texture2D? texture, Rect2 target, PortraitView view)
    {
        DrawRect(target, new Color("303d50"));
        if (texture is null || Portrait is null) return;
        var crop = Portrait.GetView(view);
        DrawTextureRectRegion(texture, target, new(crop.X - Portrait.Image.X, crop.Y - Portrait.Image.Y, crop.Width, crop.Height));
    }
}
