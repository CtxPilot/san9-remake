using Godot;
using San9.Rendering;

namespace San9.Client.Godot;

internal enum MapFrameChange { Camera, Scene }

/// <summary>Owns one camera's pending/complete frames, prefetch and resource retirement.</summary>
internal sealed class MapFramePresenter : IDisposable
{
    private readonly TextureService Textures;
    private readonly TerrainChunkCache<ImageTexture> Terrain;
    private readonly MapFrameTransition<MapDisplayFrame> _transition;
    private TextureCache<ImageTexture>.Lease? _prefetchAssets;
    private TerrainChunkCache<ImageTexture>.Lease? _prefetchTerrain;
    private (MapScene Scene, MapViewport Camera, Vector2 Size, bool Boundaries)? _lastRequest;
    public MapDisplayFrame? Presented => _transition.Presented;
    public string? Error { get; private set; }
    public bool IsReady => Presented is not null && _transition.Pending is null && Error is null;
    public event Action<MapFrameChange>? Committed;
    public MapFramePresenter(TextureService textures, TerrainChunkCache<ImageTexture> terrain)
    {
        Textures = textures; Terrain = terrain;
        _transition = new(f => f.Ready, f => f.Error, f => Textures.Defer(f.Dispose));
    }
    internal void Request(MapScene scene, MapViewport camera, Vector2 size, bool boundaries, bool prefetch = false)
    {
        var request = (scene, camera, size, boundaries);
        if (_lastRequest == request) return;
        _lastRequest = request; Error = null;
        Vector2 half = size / (2 * (float)camera.Zoom);
        var rect = new Rect2(new Vector2((float)camera.X, (float)camera.Y) - half, half * 2);
        // Camera motion inside a prepared region does not change resource ownership.
        bool Covers(MapDisplayFrame frame) => ReferenceEquals(frame.Region.Scene, scene) && frame.Boundaries == boundaries &&
            frame.Region.Bounds.Encloses(rect);
        if (Presented is { } presented && Covers(presented) && presented.CanPresent(camera, size))
        {
            _transition.CancelPending();
            presented.Move(camera, size);
            Committed?.Invoke(MapFrameChange.Camera);
            return;
        }
        if (_transition.Pending is { } pending && Covers(pending))
        {
            pending.Move(camera, size); TryCommit(); return;
        }
        // Main-map coverage has slack; stationary previews retain their exact clipping rectangle.
        var coverage = prefetch ? rect.Grow(256) : rect;
        var region = scene.Select(coverage, boundaries);
        MapFrameResources resources;
        if (Presented is { } complete && complete.CanShareResources(scene, camera, size))
        {
            resources = complete.Resources.Share();
        }
        else
        {
            var assets = Textures.CreateScope(Textures.MapAssets);
            var visible = camera.Chunks(scene.Map.CanvasWidth, scene.Map.CanvasHeight, (int)Math.Ceiling(size.X), (int)Math.Ceiling(size.Y)).ToHashSet();
            var terrain = Terrain.Acquire(Keys(0).OrderBy(k => visible.Contains((k.X, k.Y)) ? 0 : 1));
            resources = new(assets, terrain);
        }
        MapDisplayFrame next;
        try { next = new MapDisplayFrame(region, camera, size, resources, boundaries); }
        catch { resources.Release(); Presented?.RetainVisibleResources(); throw; }
        _transition.Request(next);
        var oldAssets = _prefetchAssets; var oldTerrain = _prefetchTerrain;
        _prefetchAssets = null; _prefetchTerrain = null;
        if (prefetch)
        {
            // Prefetch owns decoded files, not a drawing binding for every atlas cell.
            _prefetchAssets = Textures.Cache.Acquire(scene.Select(coverage.Grow(512), boundaries).Assets
                .Select(id => Textures.MapAssets[id].File), prefetch: true);
            _prefetchTerrain = Terrain.Acquire(Keys(1).Except(resources.Terrain.Keys), true);
        }
        // Transfer shared requests before releasing the old leases, including in-flight prefetch.
        oldAssets?.Dispose(); oldTerrain?.Dispose();
        TryCommit();
        IEnumerable<TerrainChunkKey> Keys(int margin) =>
            new MapViewport(coverage.GetCenter().X, coverage.GetCenter().Y, 1)
                .Chunks(scene.Map.CanvasWidth, scene.Map.CanvasHeight,
                    (int)Math.Ceiling(coverage.Size.X), (int)Math.Ceiling(coverage.Size.Y), margin)
                .Select(k => new TerrainChunkKey(scene.Terrain, k.X, k.Y));
    }
    private void TryCommit()
    {
        if (!_transition.TryCommit())
        {
            if (_transition.Error is { } error && Error != error.Message)
            {
                Error = error.Message; GD.PushError(Error);
                _prefetchAssets?.Dispose(); _prefetchAssets = null;
                _prefetchTerrain?.Dispose(); _prefetchTerrain = null;
                Presented?.RetainVisibleResources();
            }
            return;
        }
        Presented!.RetainVisibleResources();
        Committed?.Invoke(MapFrameChange.Scene);
    }

    public void Retry() { if (Error is not null) _lastRequest = null; }
    public void Pump()
    {
        TryCommit();
        if (_prefetchAssets is { IsComplete: true }) { _prefetchAssets.Dispose(); _prefetchAssets = null; }
        if (_prefetchTerrain is { IsReady: true } || _prefetchTerrain?.Error is not null)
        { _prefetchTerrain.Dispose(); _prefetchTerrain = null; }
    }
    public void Dispose() { _transition.Dispose(); _prefetchAssets?.Dispose(); _prefetchTerrain?.Dispose(); }
}
