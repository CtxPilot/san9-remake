using San9.Importer.San9PkTc101.Packages;

namespace San9.Importer.San9PkTc101.Tests;

public sealed class MediaConversionTests
{
    private sealed class Encoder : IMediaEncoder
    {
        internal int Calls;
        internal bool Fail;
        public async Task EncodeAsync(string source, string output, IReadOnlyList<string> options, CancellationToken token)
        {
            Calls++;
            await File.WriteAllTextAsync(output, await File.ReadAllTextAsync(source, token) + string.Join(',', options), token);
            if (Fail) throw new InvalidDataException("Broken input");
        }
    }

    [Fact]
    public async Task ReusesOnlyMatchingSourceRecipeAndOutputAndPreservesPublishedFileOnFailure()
    {
        string root = Path.Combine(Path.GetTempPath(), "san9-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source.wma"), destination = Path.Combine(root, "shared/music/title.ogg");
            await File.WriteAllTextAsync(source, "first");
            var encoder = new Encoder();
            var conversion = new MediaConversion(root, encoder);
            Task Run(string recipe = "q5") => conversion.ConvertAsync(source, "shared/music/title.ogg", [recipe], null, default);
            await Run(); await Run();
            Assert.Equal(1, encoder.Calls);
            await File.WriteAllTextAsync(source, "second");
            await Run();
            Assert.Equal(2, encoder.Calls);
            await Run("q4");
            Assert.Equal(3, encoder.Calls);
            await File.WriteAllTextAsync(destination, "corrupted");
            await Run("q4");
            Assert.Equal("secondq4", await File.ReadAllTextAsync(destination));
            Assert.Equal(4, encoder.Calls);
            encoder.Fail = true;
            await File.WriteAllTextAsync(source, "third");
            await Assert.ThrowsAsync<InvalidDataException>(() => Run());
            Assert.Equal("secondq4", await File.ReadAllTextAsync(destination));
            Assert.DoesNotContain(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories), path => path.Contains(".tmp", StringComparison.Ordinal));
            Assert.Null(FfmpegEncoder.Find(Path.Combine(root, "no-tools")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(AudioLibraryGroup.Effects, "SE/SE_M000.WAV", "shared/sound-effects/construction.wav")]
    [InlineData(AudioLibraryGroup.Bgm, "BGM/BGM_01.WAV", "shared/music/title.wav")]
    public async Task ExplicitAudioGroupReadsOnlyItsDirectoryAndCopiesPlayableFiles(
        AudioLibraryGroup group, string source, string destination)
    {
        string root = Path.Combine(Path.GetTempPath(), "san9-effects-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, source))!);
        try
        {
            byte[] bytes = "RIFF\x04\0\0\0WAVE"u8.ToArray();
            await File.WriteAllBytesAsync(Path.Combine(root, source), bytes);
            string output = Path.Combine(root, "content");
            await AudioLibraryImporter.ImportAsync(root, output, group);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(output, destination)));
            Assert.Contains(destination, San9.Content.Packages.AudioCatalogFiles.Load(output));
            Assert.Equal(2, Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).Count());
        }
        finally { Directory.Delete(root, true); }
    }
}
