using Godot;
using San9.Application.Presentation;

namespace San9.Client.Godot;

/// <summary>Viewport-space gestures and per-unit ground rings; never participates in picking.</summary>
public partial class UnitSelectionOverlay : Control
{
    public MapWorld World { get; init; } = null!;
    public UnitGroupSelection Selection { get; init; } = null!;
    public Func<bool> IsSelectionVisible { get; init; } = null!;
    private object? _selection;
    private (Vector2, float, Rect2?, bool, long) _drawn;
    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
    public override void _Process(double delta)
    {
        var state = (World.CameraPosition, World.ZoomFactor, World.SelectionRectangle, IsSelectionVisible(), World.Display?.Revision ?? -1);
        if (_drawn == state && ReferenceEquals(_selection, Selection.UnitIds)) return;
        _drawn = state; _selection = Selection.UnitIds; QueueRedraw();
    }
    public override void _Draw()
    {
        if (!IsSelectionVisible()) return;
        if (World.SelectionRectangle is { } box)
        {
            DrawRect(box, new Color(.65f, .75f, .8f, .18f));
            DrawRect(box, new Color(.65f, .75f, .8f, .85f), false, 1);
        }
        if (World.Display?.Presented is not { } frame) return;
        IReadOnlySet<int> selected = Selection.UnitIds;
        if (World.BoxSelectionPreview is { } preview)
            selected = Selection.Preview(frame.Region.Scene.State, preview, World.BoxSelectionAppends);
        foreach (var unit in frame.Region.Units.Where(u => selected.Contains(u.Id)))
        {
            var center = World.WorldToViewport(new(unit.AnchorX, unit.AnchorY));
            var points = Enumerable.Range(0, 33).Select(i => center + new Vector2(
                20 * World.ZoomFactor * MathF.Cos(i * MathF.Tau / 32),
                9 * World.ZoomFactor * MathF.Sin(i * MathF.Tau / 32))).ToArray();
            DrawPolyline(points, new Color(1f, .85f, .35f, .95f), 2, true);
        }
    }
}
