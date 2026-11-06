using System.Text;
using System.Text.Json;
using San9.Content.Images;
using San9.Content.Packages;

namespace San9.Content.Tests;

public sealed class PortraitViewTests
{
    [Theory]
    [InlineData(PortraitView.Face)]
    [InlineData(PortraitView.Bust)]
    public void LinkedMovementUsesSharedIntegerDeltaAndStopsAtEitherCropBoundary(PortraitView reference)
    {
        var portrait = new PortraitDefinition(9, new("portrait/9", "portrait.png", 512, 512, 3),
            new(100d / 512, 20d / 512), new(290d / 512, 100d / 512));
        var origin = portrait.Anchor(reference);
        var moved = portrait.MoveTogether(reference, new(origin.X + 10.4 / 512, origin.Y + 15.4 / 512));
        Assert.Equal(new PortraitAnchor(110d / 512, 35d / 512), moved.BustAnchor);
        Assert.Equal(new PortraitAnchor(300d / 512, 115d / 512), moved.FaceAnchor);
        var edge = portrait.MoveTogether(reference, new(10, 10));
        // Face reaches the right edge first; bust reaches the bottom edge first.
        Assert.Equal(new PortraitAnchor(117d / 512, 128d / 512), edge.BustAnchor);
        Assert.Equal(new PortraitAnchor(307d / 512, 208d / 512), edge.FaceAnchor);
        edge.Validate();
        var reverse = portrait.MoveTogether(reference, new(-10, -10));
        Assert.Equal(new PortraitAnchor(0, 0), reverse.BustAnchor);
        Assert.Equal(new PortraitAnchor(190d / 512, 80d / 512), reverse.FaceAnchor);
        reverse.Validate();
        Assert.Equal(portrait, moved.MoveTogether(reference, origin));
    }

    [Theory]
    [InlineData(240, 38, 0, 160, 180, 70, 30, 96, 120)]
    [InlineData(512, 81, 0, 341, 384, 149, 64, 205, 256)]
    [InlineData(960, 152, 0, 640, 720, 280, 120, 384, 480)]
    [InlineData(1024, 162, 0, 683, 768, 299, 128, 409, 512)]
    public void RelativeAnchorScalesBothCropsAndSharesTheMotherImage(int size,
        int bx, int by, int bw, int bh, int fx, int fy, int fw, int fh)
    {
        var image = new ImageAsset("portrait/9", "images/portraits/9.png", size, size, 3);
        var portrait = new PortraitDefinition(9, image, new(38d / 240, 0), new(70d / 240, 30d / 240));
        portrait.Validate();
        Assert.Same(image, portrait.GetView(PortraitView.Full));
        var bust = portrait.GetView(PortraitView.Bust);
        var face = portrait.GetView(PortraitView.Face);
        Assert.Equal((bx, by, bw, bh), (bust.X, bust.Y, bust.Width, bust.Height));
        Assert.Equal((fx, fy, fw, fh), (face.X, face.Y, face.Width, face.Height));
        bust.Validate(); face.Validate();
        Assert.Same(image.File, bust.File);
        Assert.Same(image.File, face.File);
    }

    [Theory]
    [InlineData(240)]
    [InlineData(512)]
    [InlineData(1024)]
    public void BoundaryAnchorsKeepRoundedViewsInsideTheMotherImage(int size)
    {
        var image = new ImageAsset("portrait/0", "portrait.png", size, size, 3);
        foreach (var (x, y) in new[] { (0d, 0d), (1d / 3, 0d), (0d, .25), (1d / 3, .25) })
        {
            var portrait = new PortraitDefinition(0, image, new(x, y), PortraitGeometry.FaceFromBust(new(x, y)));
            portrait.Validate();
            foreach (var view in Enum.GetValues<PortraitView>())
                portrait.GetView(view).Validate();
            var bust = portrait.GetView(PortraitView.Bust);
            Assert.Equal(x == 0 ? 0 : size, x == 0 ? bust.X : bust.X + bust.Width);
            Assert.Equal(y == 0 ? 0 : size, y == 0 ? bust.Y : bust.Y + bust.Height);
        }
    }

    [Fact]
    public async Task MixedResolutionMetadataRoundTripsAndLoadsEachImagesActualPixels()
    {
        string root = Path.Combine(Path.GetTempPath(), "san9-portrait-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var portraits = new[] { 240, 512, 1024 }.Select((size, id) =>
                new PortraitDefinition(id, new($"portrait/{id}", $"{id}.png", size, size, 3), new(38d / 240, 0), new(70d / 240, 30d / 240))).ToArray();
            foreach (var portrait in portraits)
            {
                int size = portrait.Image.Width;
                byte[] pixels = new byte[size * size * 3];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    int offset = (y * size + x) * 3;
                    pixels[offset] = (byte)x; pixels[offset + 1] = (byte)y; pixels[offset + 2] = (byte)portrait.Id;
                }
                await File.WriteAllBytesAsync(Path.Combine(root, portrait.Image.Path), new PngImage(size, size, 3, pixels).Encode());
            }
            string manifest = Path.Combine(root, "portraits.json");
            await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(portraits, PackageFiles.JsonOptions), Encoding.UTF8);
            var restored = await PackageFiles.ReadAsync<PortraitDefinition[]>(manifest);
            Assert.Equal(portraits, restored);
            await ImageAssetFiles.VerifyAsync(root, restored.Select(p => p.Image));
            foreach (var portrait in restored)
            {
                portrait.Validate();
                var pixels = ImageAssetFiles.LoadFile(root, portrait.Image.File);
                foreach (var view in Enum.GetValues<PortraitView>())
                {
                    var asset = portrait.GetView(view);
                    var crop = ImageAssetFiles.Region(pixels, asset);
                    Assert.Equal(new byte[] { (byte)asset.X, (byte)asset.Y, (byte)portrait.Id }, crop.Pixels[..3]);
                    Assert.Equal(new byte[] { (byte)(asset.X + asset.Width - 1), (byte)(asset.Y + asset.Height - 1), (byte)portrait.Id }, crop.Pixels[^3..]);
                }
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void IndependentAnchorsCanUseTheEntireMotherImageWithoutMovingTheOtherView()
    {
        var portrait = new PortraitDefinition(0, new("portrait/0", "portrait.png", 960, 960, 3),
            new(0, .25), new(.6, .5));
        portrait.Validate();
        var face = portrait.GetView(PortraitView.Face);
        Assert.Equal((576, 480, 384, 480), (face.X, face.Y, face.Width, face.Height));
        var moved = portrait.WithAnchor(PortraitView.Bust, new(1d / 3, 0));
        Assert.Equal(face, moved.GetView(PortraitView.Face));
        Assert.Equal(portrait.BustAnchor, portrait.WithAnchor(PortraitView.Face, new(0, 0)).BustAnchor);
        Assert.Throws<InvalidDataException>(() => (portrait with { FaceAnchor = new(.61, 0) }).Validate());
    }

    [Fact]
    public void PixelSnappingRoundsOriginsAndKeepsFixedCropsInsideFractionalBounds()
    {
        var anchor = new PortraitAnchor(131.330524 / 600, 25.704769 / 600).SnapToPixels(PortraitView.Face, 600, 600);
        Assert.Equal(new PortraitAnchor(131d / 600, 26d / 600), anchor);
        Assert.Equal(anchor, anchor.SnapToPixels(PortraitView.Face, 600, 600));
        var boundary = new PortraitAnchor(1d / 3, .25).SnapToPixels(PortraitView.Bust, 512, 512);
        Assert.Equal(new PortraitAnchor(170d / 512, 128d / 512), boundary);
        boundary.Validate(PortraitView.Bust);
    }

    [Fact]
    public void InvalidAnchorsCannotProduceOutOfBoundsViews()
    {
        var image = new ImageAsset("portrait/0", "images/portraits/0.png", 240, 240, 3);
        foreach (double x in new[] { -0.1, 0.4, double.NaN, double.PositiveInfinity })
            Assert.Throws<InvalidDataException>(() => new PortraitDefinition(0, image, new(x, 0), new(.2, .1)).Validate());
        Assert.Throws<InvalidDataException>(() => new PortraitDefinition(0, image, new(0, .3), new(.2, .1)).Validate());
    }
}
