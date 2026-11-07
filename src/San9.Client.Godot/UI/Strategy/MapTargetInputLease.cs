using San9.Application.Presentation;
using San9.Core.Domain;

namespace San9.Client.Godot;

/// <summary>Owns the map target callbacks and eligibility as one input binding.</summary>
internal sealed class MapTargetInputLease : IDisposable
{
    private readonly MapWorld _world;
    private bool _disposed;
    public MapTargetInputLease(MapWorld world, Action<MapObjectTarget?> target, Action<MapCell>? waypoint = null,
        Action<MapCell>? site = null, Func<MapObjectTarget, bool>? allowed = null)
    {
        _world = world; world.TargetInputOwner = this;
        world.TargetPicked = target; world.TargetAllowed = allowed; world.WaypointPicked = waypoint; world.SitePicked = site;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!ReferenceEquals(_world.TargetInputOwner, this)) return;
        _world.TargetInputOwner = null;
        _world.TargetPicked = null; _world.TargetAllowed = null;
        _world.WaypointPicked = null; _world.SitePicked = null;
    }
}
