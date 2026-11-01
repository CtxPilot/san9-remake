using San9.Importer.San9PkTc101.Images;

namespace San9.Importer.San9PkTc101.Tests;

public sealed class OpeningTextureAlphaTests
{
    [Fact]
    public void NewForceArtworkRetainsPerColorCoverageWithoutFadingSolidPixels()
    {
        byte[] palette = new byte[1024];
        new byte[] { 0, 255, 0, 0, 148, 208, 224, 16, 32, 48, 96, 128, 64, 80, 112, 0 }.CopyTo(palette, 0);
        var source = new IndexedImage(4, 1, [0, 1, 2, 3]);
        foreach (int image in new[] { 4, 5, 6, 7 })
        {
            var decoded = TextureLibraryCompiler.Decode("G_Start2", image, source, palette, opaque: false);
            Assert.Equal(new byte[] { 0, 32, 255, 0 }, Enumerable.Range(0, 4).Select(i => decoded.Pixels[i * 4 + 3]).ToArray());
            Assert.Equal(new byte[] { 148, 208, 224 }, decoded.Pixels.AsSpan(4, 3).ToArray());
        }
        var keyed = TextureLibraryCompiler.Decode("G_Start2", 8, source, palette, opaque: false);
        Assert.Equal(255, keyed.Pixels[7]);
        Assert.Equal(255, keyed.Pixels[15]);
    }
}
