using System.Text;
using System.Text.Json;
using San9.Content.Packages;

namespace San9.Content.Tests;

public sealed class AudioCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "san9-audio-catalog-" + Guid.NewGuid().ToString("N"));
    public AudioCatalogTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task LibrariesAreOptionalAndFilesOnlyBecomeAvailableAfterDeclaration()
    {
        Assert.Empty(AudioCatalogFiles.Load(_root));
        const string music = "custom/title.wav", effect = "custom/click.wav";
        WriteWave(music); WriteWave(effect);
        Assert.Empty(AudioCatalogFiles.Load(_root));
        await AudioCatalogFiles.WriteAsync(_root, AudioCatalog.MusicPath, new(1, [music]));
        Assert.Equal([music], AudioCatalogFiles.Load(_root));
        await AudioCatalogFiles.WriteAsync(_root, AudioCatalog.EffectsPath, new(1, [effect]));
        Assert.True(AudioCatalogFiles.Load(_root).SetEquals([music, effect]));
        File.Delete(PackageFiles.Resolve(_root, AudioCatalog.MusicPath));
        Assert.Equal([effect], AudioCatalogFiles.Load(_root));
    }

    [Fact]
    public async Task DeclaredMissingAndCorruptFilesRemainErrors()
    {
        const string sound = "custom/effect.wav";
        WriteWave(sound);
        await AudioCatalogFiles.WriteAsync(_root, AudioCatalog.EffectsPath, new(1, [sound]));
        File.WriteAllBytes(PackageFiles.Resolve(_root, sound), new byte[12]);
        Assert.Throws<InvalidDataException>(() => AudioCatalogFiles.Load(_root));
        File.Delete(PackageFiles.Resolve(_root, sound));
        Assert.Throws<FileNotFoundException>(() => AudioCatalogFiles.Load(_root));
    }

    [Theory]
    [InlineData("../escape.wav")]
    [InlineData("sound.wma")]
    public void InvalidReferencesFailEvenWithoutPlayback(string path)
        => Assert.Throws<InvalidDataException>(() => AudioCatalogFiles.Validate(_root, new(1, [path]), requireFiles: false));

    [Fact]
    public void MalformedCatalogIsNotTreatedAsUninstalled()
    {
        string path = PackageFiles.Resolve(_root, AudioCatalog.MusicPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}", Encoding.UTF8);
        Assert.Throws<JsonException>(() => AudioCatalogFiles.Load(_root));
    }

    private void WriteWave(string path)
    {
        string file = PackageFiles.Resolve(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, "RIFF\x04\0\0\0WAVE"u8.ToArray());
    }
}
