namespace San9.Rendering;

/// <summary>A fixed-size directional glow around the unit's ground anchor, independent of detection radius.</summary>
public sealed record MapVisionOverlay(double X, double Y, double Depth, int ArcDegrees, int Direction)
{
    public double HalfWidth => 92;
    public double HalfHeight => 48;
}
