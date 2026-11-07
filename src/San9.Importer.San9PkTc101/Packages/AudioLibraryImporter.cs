using San9.Content.Packages;
using San9.Importer.San9PkTc101.Interface;

namespace San9.Importer.San9PkTc101.Packages;

public enum AudioLibraryGroup { Bgm, Effects, All }

public static class AudioLibraryImporter
{
    public static async Task ImportAsync(string gameDirectory, string output,
        AudioLibraryGroup group = AudioLibraryGroup.All, Action<string>? progress = null, CancellationToken token = default, string? ffmpegPath = null)
    {
        if (!Enum.IsDefined(group)) throw new ArgumentOutOfRangeException(nameof(group));
        string[] directories = group switch
        {
            AudioLibraryGroup.Bgm => ["BGM"],
            AudioLibraryGroup.Effects => ["SE"],
            _ => ["BGM", "SE"]
        };
        var sources = AudioAssetCompiler.Discover(gameDirectory, directories).Values;
        if (group is AudioLibraryGroup.Bgm or AudioLibraryGroup.All)
        {
            var encoder = ffmpegPath is null ? FfmpegEncoder.Find() : new FfmpegEncoder(Path.GetFullPath(ffmpegPath));
            MediaConversion? conversion = encoder is null ? null : new(output, encoder);
            foreach (string source in sources.Where(p => p.StartsWith("BGM/", StringComparison.OrdinalIgnoreCase)))
            {
                string path = AudioAssetCompiler.Path(source);
                if (Path.GetExtension(source).Equals(".wma", StringComparison.OrdinalIgnoreCase))
                {
                    if (conversion is null)
                        progress?.Invoke("Skipped BGM: ffmpeg was not found on PATH: " + source);
                    else
                        await conversion.ConvertAsync(Path.Combine(gameDirectory, source), path, FfmpegEncoder.AudioOptions, progress, token);
                }
                else
                    await CopyAsync(Path.Combine(gameDirectory, source), output, path, token);
            }
        }
        if (group is AudioLibraryGroup.Effects or AudioLibraryGroup.All)
            foreach (string source in sources.Where(p => p.StartsWith("SE/", StringComparison.OrdinalIgnoreCase)))
            {
                string path = AudioAssetCompiler.Path(source);
                await CopyAsync(Path.Combine(gameDirectory, source), output, path, token);
                progress?.Invoke("Copied: " + path);
            }
    }

    private static async Task CopyAsync(string source, string output, string path, CancellationToken token)
    {
        string destination = PackageFiles.Resolve(output, path);
        byte[] bytes = await File.ReadAllBytesAsync(source, token);
        if (File.Exists(destination) && (await File.ReadAllBytesAsync(destination, token)).AsSpan().SequenceEqual(bytes)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string pending = destination + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(pending, bytes, token);
            File.Move(pending, destination, true);
        }
        finally { File.Delete(pending); }
    }
}
