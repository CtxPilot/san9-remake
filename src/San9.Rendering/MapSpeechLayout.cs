using San9.Content.Interface;

namespace San9.Rendering;

/// <summary>Four original anchors, fitted to the presented viewport. Ties retain the current direction.</summary>
public static class MapSpeechLayout
{
    public static MapSpeechPlacement FromUnitDirection(int direction) => direction is >= 0 and < 8
        ? (MapSpeechPlacement)(direction / 2) : throw new ArgumentOutOfRangeException(nameof(direction));
    public static OverlayBounds Bounds(double x, double y, double width, MapSpeechPlacement placement) => placement switch
    {
        MapSpeechPlacement.LeftBelow => new(x + 30 - width, y, width, 108),
        MapSpeechPlacement.LeftAbove => new(x + 30 - width, y - 100, width, 108),
        MapSpeechPlacement.RightAbove => new(x - 54, y - 100, width, 108),
        MapSpeechPlacement.RightBelow => new(x - 54, y, width, 108),
        _ => throw new ArgumentOutOfRangeException(nameof(placement))
    };

    public static OverlayBounds HitBounds(OverlayBounds bounds) => new(bounds.X + 38, bounds.Y + 20, bounds.Width - 54, 48);

    public static (MapSpeechPlacement Placement, OverlayBounds Bounds) Place(double x, double y, double width,
        MapSpeechPlacement preferred, OverlayBounds viewport, IReadOnlyList<OverlayBounds> occupied)
    {
        (OverlayBounds Bounds, (double Overlap, double Shift) Cost) Evaluate(MapSpeechPlacement placement)
        {
            var natural = Bounds(x, y, width, placement);
            var fitted = OverlayLayout.Place(natural, viewport, occupied);
            double overlap = 0;
            for (int i = 0; i < occupied.Count; i++) overlap += fitted.Overlap(occupied[i]);
            return (fitted, (overlap, Math.Abs(fitted.X - natural.X) + Math.Abs(fitted.Y - natural.Y)));
        }
        var best = preferred;
        var result = Evaluate(best);
        foreach (var placement in Placements)
        {
            if (placement == preferred) continue;
            var candidate = Evaluate(placement);
            if (candidate.Cost.CompareTo(result.Cost) >= 0) continue;
            result = candidate; best = placement;
        }
        return (best, result.Bounds);
    }

    private static readonly MapSpeechPlacement[] Placements = Enum.GetValues<MapSpeechPlacement>();
}

/// <summary>One speech owns its layout; every placement input participates in invalidation.</summary>
public sealed class MapSpeechLayoutCache
{
    private (double X, double Y, double Width, MapSpeechPlacement Preferred, OverlayBounds Viewport)? _inputs;
    private readonly List<OverlayBounds> _occupied = [];
    private (MapSpeechPlacement Placement, OverlayBounds Bounds) _result;

    public (MapSpeechPlacement Placement, OverlayBounds Bounds) Place(double x, double y, double width,
        MapSpeechPlacement preferred, OverlayBounds viewport, IReadOnlyList<OverlayBounds> occupied)
    {
        var inputs = (x, y, width, preferred, viewport);
        if (_inputs == inputs && SameObstacles(occupied)) return _result;
        _result = MapSpeechLayout.Place(x, y, width, preferred, viewport, occupied);
        _inputs = inputs;
        _occupied.Clear();
        for (int i = 0; i < occupied.Count; i++) _occupied.Add(occupied[i]);
        return _result;
    }

    private bool SameObstacles(IReadOnlyList<OverlayBounds> occupied)
    {
        if (_occupied.Count != occupied.Count) return false;
        for (int i = 0; i < occupied.Count; i++)
            if (_occupied[i] != occupied[i]) return false;
        return true;
    }
}
