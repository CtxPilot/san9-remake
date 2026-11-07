namespace San9.Content.Packages;

/// <summary>An independently installed, optional movie library. Only declared roles are available.</summary>
public sealed record MovieCatalog(int SchemaVersion, Dictionary<string, string> Movies)
{
    public const int CurrentSchemaVersion = 1;
    public const string RelativePath = "shared/movies/catalog.json";
    public const string StartupThemeRole = "startup-theme";
    public const string StartupOpeningRole = "startup-opening";
    public static MovieCatalog Empty => new(CurrentSchemaVersion, []);

    public static string ScenarioOpeningRole(string scenarioId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        return "scenario-opening-" + scenarioId;
    }

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion || Movies is null)
            throw new InvalidDataException("Unsupported movie catalog.");
        foreach (var (role, path) in Movies)
        {
            if (string.IsNullOrWhiteSpace(role) || !string.Equals(Path.GetExtension(path), ".ogv", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid movie role or format.");
            _ = PackageFiles.Resolve(".", path);
        }
    }
}

public static class MovieCatalogFiles
{
    public static async Task<MovieCatalog> LoadAsync(string root, CancellationToken token = default)
    {
        string path = PackageFiles.Resolve(root, MovieCatalog.RelativePath);
        if (!File.Exists(path)) return MovieCatalog.Empty;
        var catalog = await PackageFiles.ReadAsync<MovieCatalog>(path, token);
        await ValidateFilesAsync(root, catalog, token);
        return catalog;
    }

    public static async Task ValidateFilesAsync(string root, MovieCatalog catalog, CancellationToken token = default)
    {
        catalog.Validate();
        foreach (string path in catalog.Movies.Values.Distinct())
        {
            await using var file = File.OpenRead(PackageFiles.Resolve(root, path));
            byte[] header = new byte[27];
            await file.ReadExactlyAsync(header, token);
            if (!header.AsSpan(0, 4).SequenceEqual("OggS"u8) || header[4] != 0 || header[26] == 0)
                throw new InvalidDataException($"Invalid Ogg movie: {path}.");
            byte[] segments = new byte[header[26]];
            await file.ReadExactlyAsync(segments, token);
            byte[] signature = new byte[7];
            await file.ReadExactlyAsync(signature, token);
            if (segments[0] < 7 || signature[0] != 0x80 || !signature.AsSpan(1).SequenceEqual("theora"u8))
                throw new InvalidDataException($"Movie is not Ogg Theora: {path}.");
        }
    }

    public static async Task WriteAsync(string root, MovieCatalog catalog, CancellationToken token = default)
    {
        await ValidateFilesAsync(root, catalog, token);
        string path = PackageFiles.Resolve(root, MovieCatalog.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string pending = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(pending, System.Text.Json.JsonSerializer.Serialize(catalog, PackageFiles.JsonOptions), System.Text.Encoding.UTF8, token);
            File.Move(pending, path, overwrite: true);
        }
        finally { File.Delete(pending); }
    }
}
