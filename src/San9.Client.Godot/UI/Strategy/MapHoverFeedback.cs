using San9.Core.Domain;

namespace San9.Client.Godot;

// A present preview owns feedback even when no row is currently hovered.
public readonly record struct MapHoverPreview(MapObjectTarget? Target);

public readonly record struct MapHoverFeedback(MapObjectTarget? Highlight, MapObjectTarget? Information, MapCell? Region)
{
    public static MapHoverFeedback Resolve(bool pointerInside, MapHoverPreview? picker,
        MapHoverPreview? command, MapHoverPreview? list, MapObjectTarget? mapTarget, MapCell? region)
    {
        if (!pointerInside) return default;
        if (picker is { } picking) return new(picking.Target, null, null);
        if (command is { } locked) return new(locked.Target, null, null);
        if (list is { } row) return new(row.Target, row.Target, null);
        return new(mapTarget, mapTarget, region);
    }
}
