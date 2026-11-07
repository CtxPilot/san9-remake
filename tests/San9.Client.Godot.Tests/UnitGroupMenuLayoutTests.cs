using San9.Rendering;

namespace San9.Client.Godot.Tests;

public sealed class UnitGroupMenuLayoutTests
{
    [Fact]
    public void RosterSizeCannotPushCommandsAwayFromTheReleasePoint()
    {
        var viewport = new OverlayBounds(0, 0, 1024, 768);
        OverlayBounds[] obstacles = [new(836, 167, 188, 569)];
        var small = UnitGroupMenuLayout.Place(760, 400, 2, 120, 84, 1024, 768, obstacles);
        var large = UnitGroupMenuLayout.Place(760, 400, 2, 260, 320, 1024, 768, obstacles);
        Assert.Equal(small.Menu, large.Menu);
        Assert.Equal(CommandMenuGeometry.PlacePrimary(760, 400, 2, 1024, 768, obstacles), small.Menu);
        Assert.InRange(Math.Abs(small.Menu.X - 760), 0, 106);
        Assert.InRange(Math.Abs(small.Menu.Y - 400), 0, 104);
        Assert.Equal(0, small.Menu.Overlap(obstacles[0]));
        Assert.Equal(0, small.Menu.Overlap(small.List));
    }

    [Theory]
    [InlineData(1, 50)]
    [InlineData(1020, 730)]
    public void ViewportEdgesKeepBothPanelsVisible(double x, double y)
    {
        var viewport = new OverlayBounds(0, 0, 1024, 768);
        var placed = UnitGroupMenuLayout.Place(x, y, 2, 120, 84, 1024, 768, []);
        Assert.Equal(placed.Menu, placed.Menu.Fit(viewport));
        Assert.Equal(placed.List, placed.List.Fit(viewport));
        Assert.Equal(0, placed.Menu.Overlap(placed.List));
    }
}
