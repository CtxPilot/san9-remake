namespace San9.Rendering;

public readonly record struct UnitGroupMenuPlacement(OverlayBounds Menu, OverlayBounds List);

/// <summary>Share single-object command placement, then fit the adjacent roster independently.</summary>
public static class UnitGroupMenuLayout
{
    public static UnitGroupMenuPlacement Place(double x, double y, int commandCount,
        double listWidth, double listHeight, float viewportWidth, float viewportHeight, IReadOnlyList<OverlayBounds> occupied)
    {
        var viewport = new OverlayBounds(0, 0, viewportWidth, viewportHeight);
        double Area(OverlayBounds rect) => occupied.Sum(rect.Overlap);
        if (commandCount == 0)
        {
            var roster = OverlayLayout.Place(new(x - 13, y - 18, listWidth, listHeight), viewport, occupied);
            return new(new(roster.X, roster.Y, 0, 0), roster);
        }
        var menu = CommandMenuGeometry.PlacePrimary((float)x, (float)y, commandCount, viewportWidth, viewportHeight, occupied);
        var list = new[]
        {
            new OverlayBounds(menu.X + menu.Width + 8, menu.Y, listWidth, listHeight),
            new(menu.X - listWidth - 8, menu.Y, listWidth, listHeight),
            new(menu.X, menu.Y + menu.Height + 8, listWidth, listHeight),
            new(menu.X, menu.Y - listHeight - 8, listWidth, listHeight)
        }.Select(rect => rect.Fit(viewport))
            .OrderBy(rect => rect.Overlap(menu)).ThenBy(Area).First();
        return new(menu, list);
    }
}
