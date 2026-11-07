namespace San9.Client.Godot;

/// <summary>A map-owned press becomes either one click or one rectangle on release.</summary>
public sealed class MapSelectionGesture
{
    public CanvasPoint? Start { get; private set; }
    public CanvasPoint End { get; private set; }
    public bool Append { get; private set; }
    public bool IsDragging { get; private set; }
    public void Press(CanvasPoint point, bool append)
    { Start = point; End = point; Append = append; IsDragging = false; }
    public void Move(CanvasPoint point)
    {
        if (Start is not { } start) return;
        End = point;
        IsDragging |= (point.X - start.X) * (point.X - start.X) + (point.Y - start.Y) * (point.Y - start.Y) >= 36;
    }
    public void Cancel() { Start = null; IsDragging = false; }
}
