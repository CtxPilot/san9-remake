namespace San9.Content.Images;

public enum PortraitView { Full, Bust, Face }

/// <summary>Normalized crop origin, independent of image resolution.</summary>
public readonly record struct PortraitAnchor(double X, double Y)
{
    public void Validate(PortraitView view)
    {
        var (width, height) = PortraitGeometry.Size(view);
        if (!double.IsFinite(X) || !double.IsFinite(Y) || X < 0 || Y < 0 ||
            X + width > 1 + 1e-12 || Y + height > 1 + 1e-12)
            throw new InvalidDataException("Invalid normalized portrait anchor.");
    }
    public PortraitAnchor Clamp(PortraitView view)
    {
        var (width, height) = PortraitGeometry.Size(view);
        return new(Math.Clamp(X, 0, 1 - width), Math.Clamp(Y, 0, 1 - height));
    }

    public PortraitAnchor SnapToPixels(PortraitView view, int imageWidth, int imageHeight)
    {
        Validate(view);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);
        var (width, height) = PortraitGeometry.Size(view);
        // Snap inward at fractional limits so the fixed-size crop stays inside the mother image.
        double maxX = Math.Floor((1 - width) * imageWidth + 1e-9);
        double maxY = Math.Floor((1 - height) * imageHeight + 1e-9);
        return new(Math.Clamp(Math.Round(X * imageWidth), 0, maxX) / imageWidth,
            Math.Clamp(Math.Round(Y * imageHeight), 0, maxY) / imageHeight);
    }
}

public static class PortraitGeometry
{
    public static (double Width, double Height) Size(PortraitView view) => view switch
    {
        PortraitView.Full => (1, 1), PortraitView.Bust => (160d / 240, 180d / 240),
        PortraitView.Face => (96d / 240, 120d / 240), _ => throw new ArgumentOutOfRangeException(nameof(view))
    };
    public static PortraitAnchor DefaultBust => new(1d / 6, 0);
    public static PortraitAnchor FaceFromBust(PortraitAnchor bust) => new(bust.X + 32d / 240, bust.Y + 30d / 240);
    public static PortraitAnchor BustFromFace(PortraitAnchor face) => new PortraitAnchor(face.X - 32d / 240, face.Y - 30d / 240).Clamp(PortraitView.Bust);
}

/// <summary>One normal-color mother image and independent normalized crop origins.</summary>
public sealed record PortraitDefinition(int Id, ImageAsset Image, PortraitAnchor BustAnchor, PortraitAnchor FaceAnchor)
{
    [System.Text.Json.Serialization.JsonIgnore] public string? DisplayName { get; init; }

    public void Validate()
    {
        Image.Validate(); BustAnchor.Validate(PortraitView.Bust); FaceAnchor.Validate(PortraitView.Face);
        if (Id < 0 || Image.Width != Image.Height) throw new InvalidDataException("Invalid portrait image.");
    }
    public PortraitAnchor Anchor(PortraitView view) => view switch
    {
        PortraitView.Bust => BustAnchor, PortraitView.Face => FaceAnchor,
        _ => throw new ArgumentOutOfRangeException(nameof(view))
    };
    public PortraitDefinition WithAnchor(PortraitView view, PortraitAnchor anchor) => view switch
    {
        PortraitView.Bust => this with { BustAnchor = anchor }, PortraitView.Face => this with { FaceAnchor = anchor },
        _ => throw new ArgumentOutOfRangeException(nameof(view))
    };
    /// <summary>Translate both crops by one integer-pixel delta, constrained by both image boundaries.</summary>
    public PortraitDefinition MoveTogether(PortraitView reference, PortraitAnchor target)
    {
        var bust = BustAnchor.SnapToPixels(PortraitView.Bust, Image.Width, Image.Height);
        var face = FaceAnchor.SnapToPixels(PortraitView.Face, Image.Width, Image.Height);
        var origin = reference switch
        {
            PortraitView.Bust => bust, PortraitView.Face => face,
            _ => throw new ArgumentOutOfRangeException(nameof(reference))
        };
        double bx = Math.Round(bust.X * Image.Width), by = Math.Round(bust.Y * Image.Height);
        double fx = Math.Round(face.X * Image.Width), fy = Math.Round(face.Y * Image.Height);
        var (bw, bh) = PortraitGeometry.Size(PortraitView.Bust);
        var (fw, fh) = PortraitGeometry.Size(PortraitView.Face);
        double dx = Math.Clamp(Math.Round(target.X * Image.Width) - Math.Round(origin.X * Image.Width),
            -Math.Min(bx, fx), Math.Min(Math.Floor((1 - bw) * Image.Width + 1e-9) - bx,
                Math.Floor((1 - fw) * Image.Width + 1e-9) - fx));
        double dy = Math.Clamp(Math.Round(target.Y * Image.Height) - Math.Round(origin.Y * Image.Height),
            -Math.Min(by, fy), Math.Min(Math.Floor((1 - bh) * Image.Height + 1e-9) - by,
                Math.Floor((1 - fh) * Image.Height + 1e-9) - fy));
        return this with { BustAnchor = new((bx + dx) / Image.Width, (by + dy) / Image.Height),
            FaceAnchor = new((fx + dx) / Image.Width, (fy + dy) / Image.Height) };
    }

    public ImageAsset GetView(PortraitView view)
    {
        if (view == PortraitView.Full) return Image;
        var anchor = Anchor(view); var (width, height) = PortraitGeometry.Size(view);
        int left = (int)Math.Round(anchor.X * Image.Width), top = (int)Math.Round(anchor.Y * Image.Height);
        int right = (int)Math.Round((anchor.X + width) * Image.Width), bottom = (int)Math.Round((anchor.Y + height) * Image.Height);
        return new($"{Image.Id}/{view.ToString().ToLowerInvariant()}", Image.File,
            Image.X + left, Image.Y + top, right - left, bottom - top);
    }
}
