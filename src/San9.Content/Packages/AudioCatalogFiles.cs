using System.Text.Json;
using San9.Content.Interface;

namespace San9.Content.Packages;

/// <summary>Installed physical audio is independent of presentation cue references.</summary>
public sealed record AudioCatalog(int SchemaVersion, string[] Files)
{
    public const int CurrentSchemaVersion = 1;
    public const string MusicPath = "shared/music/catalog.json";
    public const string EffectsPath = "shared/sound-effects/catalog.json";
}

public static class AudioCatalogFiles
{
    public static HashSet<string> Load(string root, bool requireFiles = true)
    {
        HashSet<string> files = new(StringComparer.Ordinal);
        foreach (string path in new[] { AudioCatalog.MusicPath, AudioCatalog.EffectsPath })
            files.UnionWith(Read(root, path, requireFiles).Files);
        return files;
    }

    public static AudioCatalog Read(string root, string relativePath, bool requireFiles = true)
    {
        string path = PackageFiles.Resolve(root, relativePath);
        if (!File.Exists(path)) return new(AudioCatalog.CurrentSchemaVersion, []);
        var catalog = JsonSerializer.Deserialize<AudioCatalog>(File.ReadAllText(path, System.Text.Encoding.UTF8), PackageFiles.JsonOptions)
            ?? throw new InvalidDataException("Empty audio catalog.");
        Validate(root, catalog, requireFiles);
        return catalog;
    }

    public static void Validate(string root, AudioCatalog catalog, bool requireFiles = true)
    {
        if (catalog.SchemaVersion != AudioCatalog.CurrentSchemaVersion || catalog.Files is null ||
            catalog.Files.Distinct(StringComparer.Ordinal).Count() != catalog.Files.Length)
            throw new InvalidDataException("Invalid audio catalog.");
        Span<byte> header = stackalloc byte[12];
        foreach (string path in catalog.Files)
        {
            string file = PackageFiles.Resolve(root, path);
            if (AudioFileFormats.Identify(file) is not { } format)
                throw new InvalidDataException("Unsupported audio format: " + path);
            if (!requireFiles) continue;
            using var stream = File.OpenRead(file);
            stream.ReadExactly(header);
            bool valid = format switch
            {
                AudioFileFormat.Wave => header[..4].SequenceEqual("RIFF"u8) && header[8..].SequenceEqual("WAVE"u8),
                AudioFileFormat.OggVorbis => header[..4].SequenceEqual("OggS"u8),
                AudioFileFormat.Mp3 => header[..3].SequenceEqual("ID3"u8) || (header[0] == 0xff && (header[1] & 0xe0) == 0xe0),
                _ => false
            };
            if (!valid) throw new InvalidDataException("Invalid audio file: " + path);
        }
    }

    public static async Task WriteAsync(string root, string relativePath, AudioCatalog catalog, CancellationToken token = default)
    {
        Validate(root, catalog);
        string path = PackageFiles.Resolve(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string pending = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(pending, JsonSerializer.Serialize(catalog, PackageFiles.JsonOptions), System.Text.Encoding.UTF8, token);
            File.Move(pending, path, overwrite: true);
        }
        finally { File.Delete(pending); }
    }
}
