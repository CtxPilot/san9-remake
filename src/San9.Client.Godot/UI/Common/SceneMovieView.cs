using Godot;
using San9.Application.Presentation;

namespace San9.Client.Godot;

public partial class SceneMovieView : ModalView
{
    public string MoviePath { get; init; } = "";
    public bool SoundEnabled { get; init; }
    public MovieBackdrop Backdrop { get; init; } = MovieBackdrop.Black;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _firstFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private VideoStreamPlayer _player = null!;
    private Vector2 _nativeSize;
    public Task Completion => _completion.Task;
    public Task FirstFrame => _firstFrame.Task;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        if (Backdrop == MovieBackdrop.Black)
        {
            var background = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(background);
            background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        _player = new VideoStreamPlayer
        {
            Stream = new VideoStreamTheora { File = MoviePath }, Expand = true,
            Volume = SoundEnabled ? 1 : 0, MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.Linear
        };
        AddChild(_player);
        _player.Finished += Finish;
        Resized += LayoutVideo;
        LayoutVideo();
        _player.Play();
    }

    private void LayoutVideo()
    {
        Vector2 native = _player.GetVideoTexture()?.GetSize() ?? Vector2.Zero;
        if (native.X <= 0 || native.Y <= 0) return;
        _nativeSize = native;
        float scale = Math.Min(1, Math.Min(Size.X / native.X, Size.Y / native.Y));
        _player.Size = native * scale;
        _player.Position = (Size - _player.Size) / 2;
    }

    public override void _Process(double delta)
    {
        if (_player.GetVideoTexture()?.GetSize() != _nativeSize) LayoutVideo();
        if (_player.StreamPosition > 0 && _nativeSize.X > 0) _firstFrame.TrySetResult();
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree()) return;
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape or Key.Enter or Key.Space } ||
            input is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left or MouseButton.Right })
        {
            GetViewport().SetInputAsHandled();
            Finish();
        }
    }

    private void Finish()
    {
        _player.Stop();
        _completion.TrySetResult();
    }

    public override void _ExitTree()
    {
        _player.Stop();
        _player.Stream = null;
        _completion.TrySetCanceled();
        _firstFrame.TrySetCanceled();
    }
}
