using San9.Content.Maps;
using San9.Core.Domain;

namespace San9.Rendering;

/// <summary>The domain footprint projected at the displayed anchor, independent of animation frames.</summary>
public readonly record struct UnitPickGeometry(double X, double Y, Footprint Footprint)
{
    public bool Contains(double x, double y)
    {
        double difference = (x - X) / MapDisplayMetrics.HalfCellWidth;
        double sum = (y - Y) / MapDisplayMetrics.HalfCellHeight;
        double dx = (sum + difference) * MapCoordinates.UnitsPerCell / 2;
        double dy = (sum - difference) * MapCoordinates.UnitsPerCell / 2;
        // Preserve MapOccupancy's integer centering and half-open edges, including odd widths.
        return dx >= -(Footprint.Width / 2) && dx < Footprint.Width - Footprint.Width / 2 &&
            dy >= -(Footprint.Height / 2) && dy < Footprint.Height - Footprint.Height / 2;
    }
}
