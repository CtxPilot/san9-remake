using San9.Core.Domain;

namespace San9.Client.Godot.Tests;

public sealed class MapHoverFeedbackTests
{
    private static readonly MapObjectTarget MapTarget = new(1, false);
    private static readonly MapObjectTarget MenuTarget = new(2, true);
    private static readonly MapObjectTarget RowTarget = new(3, true);
    private static readonly MapCell Region = new(10, 20);

    [Fact]
    public void LeavingWindowSuppressesAllSourcesAndReentryResolvesCurrentSource()
    {
        var preview = new MapHoverPreview(RowTarget);
        Assert.Equal(default, MapHoverFeedback.Resolve(false, preview, new(MenuTarget), preview, MapTarget, Region));
        Assert.Equal(new(RowTarget, null, null),
            MapHoverFeedback.Resolve(true, preview, new(MenuTarget), preview, MapTarget, Region));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PickerOwnsHighlightWithoutLeakingMenuOrMapInformation(bool hasRow)
    {
        MapObjectTarget? target = hasRow ? RowTarget : null;
        var feedback = MapHoverFeedback.Resolve(true, new(target), new(MenuTarget), new(RowTarget), MapTarget, Region);
        Assert.Equal(new(target, null, null), feedback);
    }

    [Fact]
    public void ClosingPickerRestoresMenuLockAndClosingMenuRestoresMapInformation()
    {
        Assert.Equal(new(MenuTarget, null, null),
            MapHoverFeedback.Resolve(true, null, new(MenuTarget), new(RowTarget), MapTarget, Region));
        Assert.Equal(new(MapTarget, MapTarget, Region),
            MapHoverFeedback.Resolve(true, null, null, null, MapTarget, Region));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GroupListOwnsInformationAndSuppressesUnderlyingRegion(bool hasRow)
    {
        MapObjectTarget? target = hasRow ? RowTarget : null;
        Assert.Equal(new(target, target, null),
            MapHoverFeedback.Resolve(true, null, null, new(target), MapTarget, Region));
    }

    [Fact]
    public void EmptyMapCellStillProvidesRegionWhileCoveredPointClearsFeedback()
    {
        Assert.Equal(new(null, null, Region),
            MapHoverFeedback.Resolve(true, null, null, null, null, Region));
        Assert.Equal(default,
            MapHoverFeedback.Resolve(true, null, null, null, null, null));
    }
}
