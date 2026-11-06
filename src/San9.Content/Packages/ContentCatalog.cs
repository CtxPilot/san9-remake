using San9.Content.Interface;

namespace San9.Content.Packages;

public sealed record ScenarioCatalogEntry(string Id, string Package);

/// <summary>Package discovery only; previews and world data belong to individual scenarios.</summary>
public sealed record ContentCatalog(int SchemaVersion, string Textures, string Opening,
    ScenarioCatalogEntry[] Scenarios)
{
    public const int CurrentSchemaVersion = 5;
    [System.Text.Json.Serialization.JsonRequired] public string OfficerFlags { get; init; } = null!;
    public ScenarioCatalogEntry RequireScenario(string id) => Scenarios.SingleOrDefault(s => s.Id == id)
        ?? throw new InvalidDataException($"Unknown scenario: {id}.");
}

public sealed record OpeningAudioDefinition(string Music, string GameStartSound, Dictionary<string, string> Sounds)
{
    public void Validate(string root)
    {
        foreach (string role in new[] { Music, GameStartSound })
            if (string.IsNullOrWhiteSpace(role) || !Sounds.TryGetValue(role, out string? path) ||
                AudioFileFormats.Identify(PackageFiles.Resolve(root, path)) is null || !File.Exists(PackageFiles.Resolve(root, path)))
                throw new InvalidDataException("Missing or invalid opening audio asset: " + role);
    }
}

[System.Text.Json.Serialization.JsonConverter(typeof(OpeningContentJsonConverter))]
public sealed record OpeningContent(StartMenuPresentation Menu, ScenarioSelectionPresentation Selection)
{
    public const int CurrentSchemaVersion = 1;
    [System.Text.Json.Serialization.JsonRequired] public OpeningAudioDefinition Audio { get; init; } = null!;
}

public static class ContentCatalogFiles
{
    public static async Task<ContentCatalog> LoadAsync(string root, CancellationToken token = default)
    {
        var catalog = await PackageFiles.ReadAsync<ContentCatalog>(Path.Combine(root, "catalog.json"), token);
        Validate(root, catalog);
        return catalog;
    }

    public static void Validate(string root, ContentCatalog catalog)
    {
        if (catalog.SchemaVersion != ContentCatalog.CurrentSchemaVersion || catalog.Scenarios.Length == 0 ||
            catalog.Scenarios.Select(s => s.Id).Distinct().Count() != catalog.Scenarios.Length ||
            catalog.Scenarios.Any(s => string.IsNullOrWhiteSpace(s.Id)))
            throw new InvalidDataException("Invalid content catalog.");
        _ = PackageFiles.Resolve(root, catalog.Textures);
        _ = PackageFiles.Resolve(root, catalog.Opening);
        _ = PackageFiles.Resolve(root, catalog.OfficerFlags);
        foreach (var scenario in catalog.Scenarios) _ = PackageFiles.Resolve(root, scenario.Package);
    }

    public static async Task<Authoring.OfficerFlagCatalog> LoadOfficerFlagsAsync(string root, ContentCatalog catalog, CancellationToken token = default)
    {
        var flags = await PackageFiles.ReadAsync<Authoring.OfficerFlagCatalog>(PackageFiles.Resolve(root, catalog.OfficerFlags), token);
        flags.Validate(); return flags;
    }

    public static async Task<ScenarioPreviewEntry[]> LoadPreviewsAsync(string root, ContentCatalog catalog, CancellationToken token = default)
    {
        var previews = await Task.WhenAll(catalog.Scenarios.Select(async entry =>
        {
            string package = PackageFiles.Resolve(root, entry.Package);
            var preview = await ScenarioContentFiles.LoadPreviewAsync(package, token);
            if (preview.Id != entry.Id) throw new InvalidDataException($"Scenario preview identity differs from catalog: {entry.Id}.");
            return preview;
        }));
        return previews;
    }

    public static async Task<OpeningContent> LoadOpeningAsync(string root, ContentCatalog catalog, CancellationToken token = default)
    {
        var opening = await PackageFiles.ReadAsync<OpeningContent>(PackageFiles.Resolve(root, catalog.Opening), token);
        if (opening.Audio is null) throw new InvalidDataException("Missing opening audio definition.");
        opening.Audio.Validate(root);
        return opening with { Selection = opening.Selection with { Entries = await LoadPreviewsAsync(root, catalog, token) } };
    }
}
