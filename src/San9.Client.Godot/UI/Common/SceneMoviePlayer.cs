using Godot;
using San9.Application.Presentation;
using San9.Client.Godot.Settings;
using San9.Content.Packages;

namespace San9.Client.Godot;

/// <summary>Session-owned movie playback uses the same modal and cancellation lifecycle as narration.</summary>
public partial class SceneMoviePlayer : Node, ISceneMedia
{
    public string ContentRoot { get; init; } = "";
    public StrategyOverlayHost OverlayHost { get; init; } = null!;
    public SceneMusicPlayer Music { get; init; } = null!;
    public Func<UserSettings> ReadSettings { get; init; } = null!;
    private WorkflowWindows? _windows;
    private SceneMovieView? _active;
    private IDisposable? _background;
    private bool _exiting;
    private MovieCatalog? _catalog;

    public void RequestBackgroundMusic(BackgroundMusicRequest request)
    {
        if (request.Action == BackgroundMusicAction.Preserve) return;
        _background?.Dispose();
        _background = request.Action == BackgroundMusicAction.Stop ? Music.Suspend() :
            Music.Begin(request.Role ?? throw new ArgumentException("Music role is required."), loop: false);
    }

    public async Task PlayMovieAsync(MovieRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ReadSettings().MoviesEnabled) return;
        if (_active is not null) throw new InvalidOperationException("A movie is already playing.");
        _catalog ??= await MovieCatalogFiles.LoadAsync(ContentRoot, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_exiting || !ReadSettings().MoviesEnabled || !_catalog.Movies.TryGetValue(request.Role, out string? path)) return;
        if (_active is not null) throw new InvalidOperationException("A movie is already playing.");
        var view = new SceneMovieView
        {
            MoviePath = PackageFiles.Resolve(ContentRoot, path),
            SoundEnabled = ReadSettings().SoundEnabled, Backdrop = request.Backdrop
        };
        _active = view;
        using var silence = Music.Suspend();
        try
        {
            (_windows ??= new(OverlayHost)).Show(view, blocked: InteractionBlock.Orders | InteractionBlock.Continuation | InteractionBlock.Playback);
            view.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await view.Completion.WaitAsync(cancellationToken);
        }
        finally
        {
            _active = null;
            if (!_exiting && IsInstanceValid(view)) _windows?.Close(view);
        }
    }

    public override void _ExitTree()
    {
        _exiting = true;
        _windows?.Dispose();
        _background?.Dispose();
    }
}
