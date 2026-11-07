using San9.Content.Interface;
using San9.Rendering;

namespace San9.Client.Godot.Tests;

public sealed class OverlayLayoutTests
{
    [Fact]
    public void DenseLayoutMinimizesOverlapBeforeDistance()
    {
        var viewport = new OverlayBounds(0, 0, 1024, 768);
        OverlayBounds[] occupied = [new(0, 0, 1024, 90), new(700, 90, 324, 220),
            new(0, 650, 1024, 118), new(180, 220, 280, 108), new(450, 360, 320, 108),
            new(80, 480, 300, 108), new(650, 520, 280, 108)];
        foreach (double x in new[] { 100, 250.25, 480.5, 1000 })
        foreach (double y in new[] { 30, 230.75, 470.5, 740 })
        {
            var desired = new OverlayBounds(x, y, 280, 108);
            var actual = OverlayLayout.Place(desired, viewport, occupied);
            var xs = new[] { desired.Fit(viewport).X, 0, 744 }.Concat(occupied.SelectMany(o => new[] { o.X - 280, o.X + o.Width }));
            var ys = new[] { desired.Fit(viewport).Y, 0, 660 }.Concat(occupied.SelectMany(o => new[] { o.Y - 108, o.Y + o.Height }));
            (double Area, double Distance) Cost(OverlayBounds b) =>
                (occupied.Sum(b.Overlap), Math.Abs(b.X - x) + Math.Abs(b.Y - y));
            var expected = xs.SelectMany(cx => ys.Select(cy => new OverlayBounds(cx, cy, 280, 108).Fit(viewport)))
                .Select(Cost).Order().First();
            Assert.Equal(expected.Area, Cost(actual).Area, 6);
            Assert.Equal(expected.Distance, Cost(actual).Distance, 6);
        }
    }

    [Fact]
    public void RetainedSpeechReflowsWhenItsAnchorViewportOrExistingObstaclesChange()
    {
        var cache = new MapSpeechLayoutCache();
        var preferred = San9.Content.Interface.MapSpeechPlacement.RightBelow;
        var viewport = new OverlayBounds(0, 0, 1024, 768);
        var obstacles = new List<OverlayBounds> { new(200, 300, 280, 108) };
        var first = cache.Place(254, 300, 280, preferred, viewport, obstacles);
        Assert.Equal(0, first.Bounds.Overlap(obstacles[0]));
        Assert.Equal(first, cache.Place(254, 300, 280, preferred, viewport, obstacles));
        obstacles.Clear();
        var released = cache.Place(254, 300, 280, preferred, viewport, obstacles);
        Assert.Equal(MapSpeechLayout.Bounds(254, 300, 280, preferred), released.Bounds);
        var moved = cache.Place(500, 400, 280, preferred, viewport, obstacles);
        Assert.Equal(MapSpeechLayout.Bounds(500, 400, 280, preferred), moved.Bounds);
        var resized = cache.Place(500, 400, 350, preferred, new(0, 0, 400, 300), obstacles);
        Assert.InRange(resized.Bounds.X, 0, 50);
        Assert.InRange(resized.Bounds.Y, 0, 192);
        Assert.Equal(350, resized.Bounds.Width);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(1365.333)]
    [InlineData(1792)]
    public void OverlayClearsAnchoredSidebarAndBottomBand(double width)
    {
        var viewport = new OverlayBounds(0, 0, width, 768);
        OverlayBounds[] occupied = [new(width - 188, 167, 188, 570), new(0, 736, width, 32)];
        var placed = OverlayLayout.Place(new(width - 180, 650, 160, 316), viewport, occupied);
        Assert.All(occupied, obstacle => Assert.Equal(0, placed.Overlap(obstacle)));
        Assert.InRange(placed.X, 0, width - placed.Width);
        Assert.InRange(placed.Y, 0, 768 - placed.Height);
        Assert.Equal(placed, OverlayLayout.Place(placed, viewport, occupied));
    }

    [Fact]
    public void RecedingPanelReleasesSpaceWithoutChangingPreferredAnchor()
    {
        var viewport = new OverlayBounds(0, 0, 1365, 768);
        var desired = new OverlayBounds(50, 550, 280, 108);
        var expanded = OverlayLayout.Place(desired, viewport, [new(13, 564, 424, 208)]);
        Assert.NotEqual(desired, expanded);
        Assert.Equal(desired, OverlayLayout.Place(desired, viewport, [new(13, 778, 424, 208)]));
    }

    [Fact]
    public void SpeechCanMoveBeyondItsFourAnchorsToClearHud()
    {
        var viewport = new OverlayBounds(0, 0, 1365, 768);
        OverlayBounds[] occupied = [new(900, 0, 465, 210), new(1177, 167, 188, 570), new(0, 736, 1365, 32)];
        var speech = MapSpeechLayout.Place(1280, 160, 280, MapSpeechPlacement.RightBelow, viewport, occupied);
        Assert.All(occupied, obstacle => Assert.Equal(0, speech.Bounds.Overlap(obstacle)));
    }

    [Fact]
    public void FullyOccupiedViewportKeepsSizeAndDeterministicPlacement()
    {
        var viewport = new OverlayBounds(0, 0, 400, 300);
        var desired = new OverlayBounds(390, 290, 160, 100);
        var placed = OverlayLayout.Place(desired, viewport, [viewport]);
        Assert.Equal(new OverlayBounds(240, 200, 160, 100), placed);
        Assert.Equal(placed, OverlayLayout.Place(desired, viewport, [viewport]));
    }
}
