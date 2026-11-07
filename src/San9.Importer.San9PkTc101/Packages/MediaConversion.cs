using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using San9.Content.Packages;

namespace San9.Importer.San9PkTc101.Packages;

/// <summary>Conversion provenance belongs to the final package, never to a staging library.</summary>
internal sealed class MediaConversion(string root, IMediaEncoder encoder)
{
    internal sealed record Entry(string SourceHash, string RecipeHash, string OutputHash);

    internal async Task ConvertAsync(string source, string relativePath, IReadOnlyList<string> options,
        Action<string>? progress, CancellationToken token)
    {
        string destination = PackageFiles.Resolve(root, relativePath);
        string manifest = PackageFiles.Resolve(root, "shared/media-conversion.json");
        var entries = File.Exists(manifest)
            ? await PackageFiles.ReadAsync<Dictionary<string, Entry>>(manifest, token) : [];
        string sourceHash = await HashAsync(source, token);
        string recipeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0', options))));
        if (entries.TryGetValue(relativePath, out var previous) && previous.SourceHash == sourceHash &&
            previous.RecipeHash == recipeHash && File.Exists(destination) && previous.OutputHash == await HashAsync(destination, token))
        {
            progress?.Invoke("Unchanged: " + relativePath);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string pending = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileNameWithoutExtension(destination)}-{Guid.NewGuid():N}.tmp{Path.GetExtension(destination)}");
        string pendingManifest = manifest + ".tmp";
        try
        {
            await encoder.EncodeAsync(source, pending, options, token);
            string outputHash = await HashAsync(pending, token);
            token.ThrowIfCancellationRequested();
            File.Move(pending, destination, true);
            entries[relativePath] = new(sourceHash, recipeHash, outputHash);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
            await File.WriteAllTextAsync(pendingManifest, JsonSerializer.Serialize(entries, PackageFiles.JsonOptions), Encoding.UTF8, token);
            File.Move(pendingManifest, manifest, true);
            progress?.Invoke("Converted: " + relativePath);
        }
        finally { File.Delete(pending); File.Delete(pendingManifest); }
    }

    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
    }
}
