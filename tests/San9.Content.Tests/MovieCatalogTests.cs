using San9.Content.Packages;

namespace San9.Content.Tests;

public sealed class MovieCatalogTests
{
    [Fact]
    public async Task UninstalledMoviesAreOptionalButDeclaredMissingFilesFail()
    {
        string root = Path.Combine(Path.GetTempPath(), "san9-movies-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Empty((await MovieCatalogFiles.LoadAsync(root)).Movies);
            await Assert.ThrowsAsync<FileNotFoundException>(() => MovieCatalogFiles.ValidateFilesAsync(root,
                new(1, new() { ["story"] = "missing.ogv" })));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("../outside.ogv")]
    [InlineData("clip.mp4")]
    public void CatalogRejectsUnsupportedFormatsAndEscapingPaths(string path)
        => Assert.Throws<InvalidDataException>(() => new MovieCatalog(1, new() { ["story"] = path }).Validate());

    [Fact]
    public async Task CatalogChecksTheCodecRatherThanTrustingTheExtension()
    {
        string root = Path.Combine(Path.GetTempPath(), "san9-movies-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            byte[] header = new byte[35];
            "OggS"u8.CopyTo(header);
            header[26] = 1; header[27] = 7; header[28] = 0x80;
            "theora"u8.CopyTo(header.AsSpan(29));
            await File.WriteAllBytesAsync(Path.Combine(root, "story.ogv"), header);
            var catalog = new MovieCatalog(1, new() { ["authored-story"] = "story.ogv" });
            await MovieCatalogFiles.WriteAsync(root, catalog);
            Assert.Equal("story.ogv", (await MovieCatalogFiles.LoadAsync(root)).Movies["authored-story"]);
            header[29] = (byte)'x';
            await File.WriteAllBytesAsync(Path.Combine(root, "story.ogv"), header);
            await Assert.ThrowsAsync<InvalidDataException>(() => MovieCatalogFiles.LoadAsync(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
