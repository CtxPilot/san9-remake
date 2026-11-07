using San9.Content.Maps;
using San9.Core.Domain;
using San9.Rendering;

namespace San9.Client.Godot.Tests;

public sealed class UnitPickGeometryTests
{
    [Theory]
    [InlineData(4, 4)]
    [InlineData(5, 3)]
    [InlineData(10, 6)]
    public void ProjectedPickingMatchesDomainOccupancyIncludingOddSizesAndHalfOpenEdges(int width, int height)
    {
        var footprint = new Footprint(width, height);
        var area = new MapOccupancy(new(30, 40), footprint);
        var shape = new UnitPickGeometry(100, 200, footprint);
        for (int dx = -12; dx <= 12; dx++)
        for (int dy = -12; dy <= 12; dy++)
        {
            double x = 100 + (dx - dy) * MapDisplayMetrics.HalfCellWidth / (double)MapCoordinates.UnitsPerCell;
            double y = 200 + (dx + dy) * MapDisplayMetrics.HalfCellHeight / (double)MapCoordinates.UnitsPerCell;
            bool edge = 30 + dx == area.Left || 30 + dx == area.Right ||
                40 + dy == area.Top || 40 + dy == area.Bottom;
            if (edge && (x != Math.Truncate(x) || y != Math.Truncate(y))) continue;
            Assert.Equal(area.Contains(new(30 + dx, 40 + dy)), shape.Contains(x, y));
        }
    }

    [Fact]
    public void PresentedAnchorMovesTheFootprintWithoutRetainingTheOldLocation()
    {
        var footprint = new Footprint(5, 5);
        var before = new UnitPickGeometry(100, 200, footprint);
        var after = new UnitPickGeometry(164, 232, footprint);
        Assert.True(before.Contains(100, 200));
        Assert.False(after.Contains(100, 200));
        Assert.True(after.Contains(164, 232));
    }
}
