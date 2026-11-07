namespace San9.Client.Godot.Tests;

public sealed class MapSelectionGestureTests
{
    [Fact]
    public void SmallMotionRemainsClickButCrossingThresholdKeepsDragWhenReturningToOrigin()
    {
        var gesture = new MapSelectionGesture();
        gesture.Press(new(100, 100), true); gesture.Move(new(102, 102));
        Assert.False(gesture.IsDragging);
        gesture.Move(new(120, 130)); gesture.Move(new(100, 100));
        Assert.True(gesture.IsDragging); Assert.True(gesture.Append);
        gesture.Cancel(); Assert.Null(gesture.Start); Assert.False(gesture.IsDragging);
        gesture.Press(new(20, 20), false);
        Assert.False(gesture.Append); Assert.False(gesture.IsDragging);
    }
}
