using Godot;

namespace San9.Client.Godot;

/// <summary>Owns the opening layer and its presentation/completion tasks for one playback.</summary>
internal sealed class OpeningMoviePlayback : IDisposable
{
    private readonly CanvasLayer _layer;
    private readonly SceneMovieView _view;
    private readonly CancellationTokenSource _lifetime;
    public Task Presented { get; }
    public Task Finished { get; }
    private bool _disposed;

    public OpeningMoviePlayback(Node parent, string path, bool soundEnabled, CancellationToken token)
    {
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        _layer = new CanvasLayer { Name = "OpeningMovie", Layer = 101 };
        _view = new SceneMovieView { MoviePath = path, SoundEnabled = soundEnabled };
        parent.AddChild(_layer);
        _layer.AddChild(_view);
        _view.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Presented = WaitForPresentation(_lifetime.Token);
        Finished = WaitForCompletion(_lifetime.Token);
    }

    private async Task WaitForPresentation(CancellationToken token)
    {
        await Task.WhenAny(_view.FirstFrame, _view.Completion).WaitAsync(token);
        if (!_view.Completion.IsCompleted)
        {
            var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Frame() => rendered.TrySetResult();
            RenderingServer.FramePostDraw += Frame;
            using var cancellation = token.Register(() => rendered.TrySetCanceled(token));
            try { await rendered.Task; }
            finally { RenderingServer.FramePostDraw -= Frame; }
        }
        token.ThrowIfCancellationRequested();
    }

    private async Task WaitForCompletion(CancellationToken token)
    {
        await _view.Completion.WaitAsync(token);
        if (GodotObject.IsInstanceValid(_view)) _view.Hide();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        if (GodotObject.IsInstanceValid(_layer)) _layer.Free();
        _lifetime.Dispose();
    }
}
