namespace San9.Rendering;

public readonly record struct OverlayBounds(double X, double Y, double Width, double Height)
{
    public double Overlap(OverlayBounds other) => Math.Max(0, Math.Min(X + Width, other.X + other.Width) - Math.Max(X, other.X)) *
        Math.Max(0, Math.Min(Y + Height, other.Y + other.Height) - Math.Max(Y, other.Y));

    public OverlayBounds Fit(OverlayBounds viewport) => this with
    {
        X = Math.Clamp(X, viewport.X, viewport.X + Math.Max(0, viewport.Width - Width)),
        Y = Math.Clamp(Y, viewport.Y, viewport.Y + Math.Max(0, viewport.Height - Height))
    };
}

/// <summary>Fits an overlay to the viewport, then minimizes overlap with visible UI without scaling it.</summary>
public static class OverlayLayout
{
    private readonly record struct Edge(double Y, int Obstacle, int Slope);

    public static OverlayBounds Place(OverlayBounds desired, OverlayBounds viewport, IReadOnlyList<OverlayBounds> occupied)
    {
        var initial = desired.Fit(viewport);
        var xs = new List<double>();
        var ys = new List<double>();
        static void Add(List<double> values, double value, double min, double max)
        {
            value = Math.Clamp(value, min, max);
            if (!values.Contains(value)) values.Add(value);
        }
        double maxX = viewport.X + Math.Max(0, viewport.Width - desired.Width);
        double maxY = viewport.Y + Math.Max(0, viewport.Height - desired.Height);
        void X(double value) => Add(xs, value, viewport.X, maxX);
        void Y(double value) => Add(ys, value, viewport.Y, maxY);
        X(initial.X); X(viewport.X); X(maxX);
        Y(initial.Y); Y(viewport.Y); Y(maxY);
        foreach (var obstacle in occupied)
        {
            X(obstacle.X - desired.Width); X(obstacle.X + obstacle.Width);
            Y(obstacle.Y - desired.Height); Y(obstacle.Y + obstacle.Height);
        }
        (double Overlap, double Distance) Cost(OverlayBounds rect)
        {
            double overlap = 0;
            for (int i = 0; i < occupied.Count; i++) overlap += rect.Overlap(occupied[i]);
            return (overlap, Math.Abs(rect.X - desired.X) + Math.Abs(rect.Y - desired.Y));
        }
        var best = initial;
        var cost = Cost(best);
        if (cost.Overlap == 0) return best;
        // For a fixed x, total overlapping area is a piecewise-linear function of y.
        // Sweep its slope changes once instead of rescanning every obstacle at every grid point.
        var edges = new Edge[occupied.Count * 4];
        for (int i = 0; i < occupied.Count; i++)
        {
            var obstacle = occupied[i];
            edges[i * 4] = new(obstacle.Y - desired.Height, i, 1);
            edges[i * 4 + 1] = new(obstacle.Y, i, -1);
            edges[i * 4 + 2] = new(obstacle.Y + obstacle.Height - desired.Height, i, -1);
            edges[i * 4 + 3] = new(obstacle.Y + obstacle.Height, i, 1);
        }
        Array.Sort(edges, static (a, b) => a.Y.CompareTo(b.Y));
        var rows = ys.Select((y, index) => (Y: y, Index: index)).OrderBy(p => p.Y).ToArray();
        var widths = new double[occupied.Count];
        int bestOrder = 0;
        for (int column = 0; column < xs.Count; column++)
        {
            double x = xs[column];
            for (int i = 0; i < occupied.Count; i++)
                widths[i] = Math.Max(0, Math.Min(x + desired.Width, occupied[i].X + occupied[i].Width) - Math.Max(x, occupied[i].X));
            double area = 0, slope = 0, at = edges[0].Y;
            int edge = 0;
            foreach (var row in rows)
            {
                while (edge < edges.Length && edges[edge].Y <= row.Y)
                {
                    var next = edges[edge++];
                    area += slope * (next.Y - at); at = next.Y;
                    slope += widths[next.Obstacle] * next.Slope;
                }
                area += slope * (row.Y - at); at = row.Y;
                double overlap = Math.Max(0, area);
                double distance = Math.Abs(x - desired.X) + Math.Abs(row.Y - desired.Y);
                int order = column * ys.Count + row.Index;
                // Integrating fractional pixel coordinates may leave subpixel round-off.
                const double areaEpsilon = 1e-7;
                bool sameOverlap = Math.Abs(overlap - cost.Overlap) <= areaEpsilon;
                if (overlap >= cost.Overlap - areaEpsilon &&
                    (!sameOverlap || distance > cost.Distance || distance == cost.Distance && order >= bestOrder)) continue;
                best = desired with { X = x, Y = row.Y };
                cost = (overlap, distance); bestOrder = order;
            }
        }
        return best;
    }
}
