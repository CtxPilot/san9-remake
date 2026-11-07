using Godot;
using San9.Rendering;

namespace San9.Client.Godot;

// Frames may share a camera's leases while the next scene acquires only its added
// dependencies. The previous complete frame remains valid until atomic publication.
internal sealed class MapFrameResources(TextureScope assets, TerrainChunkCache<ImageTexture>.Lease terrain)
{
    public TextureScope Assets { get; } = assets;
    public TerrainChunkCache<ImageTexture>.Lease Terrain { get; } = terrain;
    private int _owners = 1;
    public MapFrameResources Share() { _owners++; return this; }
    public void Release() { if (--_owners == 0) { Assets.Dispose(); Terrain.Dispose(); } }
}

internal sealed class MapDisplayFrame : IDisposable
{
    private sealed record Target(MapViewport Camera, Vector2 Size, string[] Assets, TerrainChunkKey[] Terrain);
    public MapSceneRegion Region { get; }
    public MapViewport Camera => _target.Camera;
    public Vector2 Size => _target.Size;
    public Rect2 ViewportBounds => new(new Vector2((float)Camera.X, (float)Camera.Y) - Size / (2 * (float)Camera.Zoom), Size / (float)Camera.Zoom);
    public bool Boundaries { get; }
    public TextureScope Assets => Resources.Assets;
    public TerrainChunkCache<ImageTexture>.Lease Terrain => Resources.Terrain;
    public MapFrameResources Resources { get; }
    public HashSet<string> WaterClips { get; }
    public HashSet<string> ObjectClips { get; }
    private bool _disposed;
    private Target _target;
    private Target? _candidate;
    public MapDisplayFrame(MapSceneRegion region, MapViewport camera, Vector2 size, MapFrameResources resources, bool boundaries)
    {
        Region = region; Resources = resources; Boundaries = boundaries;
        WaterClips = region.Water.Select(s => s.ClipId).ToHashSet();
        ObjectClips = region.ObjectAnimations.Select(s => s.ClipId).ToHashSet();
        _target = Select(camera, size); Assets.Request(_target.Assets);
    }
    private Target Select(MapViewport camera, Vector2 size)
    {
        if (_candidate is { } existing && existing.Camera == camera && existing.Size == size) return existing;
        Vector2 half = size / (2 * (float)camera.Zoom);
        var bounds = new Rect2(new Vector2((float)camera.X, (float)camera.Y) - half, half * 2);
        return _candidate = new(camera, size, Region.Select(bounds).Assets.ToArray(),
            camera.Chunks(Region.Scene.Map.CanvasWidth, Region.Scene.Map.CanvasHeight, (int)Math.Ceiling(size.X), (int)Math.Ceiling(size.Y))
                .Select(k => new TerrainChunkKey(Region.Scene.Terrain, k.X, k.Y)).ToArray());
    }
    public void Move(MapViewport camera, Vector2 size)
    { _target = Select(camera, size); Assets.Request(_target.Assets); RetainVisibleResources(); }
    public bool CanShareResources(MapScene scene, MapViewport camera, Vector2 size)
    {
        return ReferenceEquals(Region.Scene.Map, scene.Map) && ReferenceEquals(Region.Scene.Terrain, scene.Terrain) &&
            camera.Chunks(scene.Map.CanvasWidth, scene.Map.CanvasHeight, (int)Math.Ceiling(size.X), (int)Math.Ceiling(size.Y))
                .All(k => Terrain.IsAvailable(new(scene.Terrain, k.X, k.Y)));
    }
    public void RetainVisibleResources() => Assets.Retain(_target.Assets.ToHashSet());
    private bool IsReady(Target target) => Assets.AreReady(target.Assets) && target.Terrain.All(Terrain.IsAvailable);
    public bool Ready => IsReady(_target);
    public bool CanPresent(MapViewport camera, Vector2 size)
    {
        var target = Select(camera, size);
        // A probe must not start demand loads for a navigation that has not been committed.
        Assets.RequestCached(target.Assets);
        return IsReady(target);
    }
    public Exception? Error => Assets.ErrorFor(_target.Assets) ?? Terrain.Error;
    public void Dispose() { if (!_disposed) { _disposed = true; Resources.Release(); } }
}
