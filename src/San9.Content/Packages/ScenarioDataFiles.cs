using System.Text.Json;
using San9.Content.Scenarios;

namespace San9.Content.Packages;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record OfficerDefaultsCatalog(int SchemaVersion, OfficerDefaults[] Officers)
{
    public const int CurrentSchemaVersion = 1;
}

/// <summary>Explicitly owned reusable inputs; no automatic discovery or content-based relocation.</summary>
public sealed record SharedScenarioData(string DefinitionsPath, string OfficersPath, string GeographyPath,
    ScenarioDefinitions Definitions, OfficerDefaultsCatalog Officers, ScenarioGeography Geography)
{
    public static SharedScenarioData From(ScenarioInitialState scenario, string definitionsPath, string officersPath, string geographyPath) =>
        new(definitionsPath, officersPath, geographyPath, ScenarioDefinitions.From(scenario),
            new(OfficerDefaultsCatalog.CurrentSchemaVersion, scenario.Officers.Select(OfficerDefaults.From).ToArray()),
            ScenarioGeography.From(scenario));

    public ScenarioDocument Describe(ScenarioInitialState scenario)
    {
        Validate();
        ContentStructure.Validate(scenario);
        if (scenario.SchemaVersion != ScenarioInitialState.CurrentSchemaVersion)
            throw new InvalidDataException("Unsupported compiled scenario state version.");
        if (!ContentComparison.Equal(Geography, ScenarioGeography.From(scenario)))
            throw new InvalidDataException("Scenario geography differs from its explicitly referenced shared geography.");
        var document = ScenarioDocument.From(scenario, Definitions, Officers.Officers);
        if (!ContentComparison.Equal(scenario, document.Compile(Definitions, Officers.Officers, Geography)))
            throw new InvalidDataException("Authored scenario composition changed the compiled initial state.");
        return document;
    }

    public void Validate()
    {
        ContentStructure.Validate(this);
        if (Definitions.SchemaVersion != ScenarioDefinitions.CurrentSchemaVersion ||
            Officers.SchemaVersion != OfficerDefaultsCatalog.CurrentSchemaVersion ||
            Geography.SchemaVersion != ScenarioGeography.CurrentSchemaVersion)
            throw new InvalidDataException("Unsupported shared scenario data version; reconvert the package.");
        OfficerComposition.Compile([], Officers.Officers);
    }

    internal string[] ResolvePaths(string resourceRoot)
    {
        var paths = new[] { DefinitionsPath, OfficersPath, GeographyPath }.Select(p => PackageFiles.Resolve(resourceRoot, p)).ToArray();
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length)
            throw new InvalidDataException("Shared scenario inputs must use distinct paths.");
        return paths;
    }
}

public static class ScenarioDataFiles
{
    public static async Task<SharedScenarioData> LoadAsync(string resourceRoot, ScenarioContentManifest manifest, CancellationToken token = default)
    {
        var definitions = await ReadAsync<ScenarioDefinitions>(resourceRoot, manifest.Definitions, token);
        var officers = await ReadAsync<OfficerDefaultsCatalog>(resourceRoot, manifest.OfficerDefaults, token);
        var geography = await ReadAsync<ScenarioGeography>(resourceRoot, manifest.Geography, token);
        var result = new SharedScenarioData(manifest.Definitions, manifest.OfficerDefaults, manifest.Geography, definitions, officers, geography);
        result.Validate();
        return result;
    }

    public static async Task<ScenarioInitialState> LoadScenarioAsync(string resourceRoot, ScenarioContentManifest manifest, CancellationToken token = default)
    {
        var document = await ReadAsync<ScenarioDocument>(resourceRoot, manifest.Scenario, token);
        var shared = await LoadAsync(resourceRoot, manifest, token);
        var scenario = document.Compile(shared.Definitions, shared.Officers.Officers, shared.Geography);
        ContentStructure.Validate(scenario);
        return scenario;
    }

    // Selection deliberately skips rule and map payloads, while sharing the exact person composition.
    public static async Task<ScenarioSelectionState> LoadSelectionAsync(string resourceRoot, ScenarioContentManifest manifest, CancellationToken token = default)
    {
        var document = await ReadAsync<SelectionDocument>(resourceRoot, manifest.Scenario, token);
        var definitions = await ReadAsync<SelectionDefinitions>(resourceRoot, manifest.Definitions, token);
        var officers = await ReadAsync<OfficerDefaultsCatalog>(resourceRoot, manifest.OfficerDefaults, token);
        if (document.SchemaVersion != ScenarioDocument.CurrentSchemaVersion || definitions.SchemaVersion != ScenarioDefinitions.CurrentSchemaVersion ||
            officers.SchemaVersion != OfficerDefaultsCatalog.CurrentSchemaVersion)
            throw new InvalidDataException("Unsupported authored scenario data version; reconvert the package.");
        var catalog = document.DefinitionOverrides.Information is { } change ? change.Value : definitions.Information;
        ContentStructure.Validate(catalog);
        return new(ScenarioInitialState.CurrentSchemaVersion, document.Year, document.Month, document.Day,
            document.Forces, document.Settlements, OfficerComposition.Compile(document.Officers, [.. officers.Officers, .. document.AdditionalOfficerDefaults]),
            document.Units, catalog.Compile(document.Information), document.Alliance);
    }

    public static async Task WriteSharedAsync(string resourceRoot, SharedScenarioData shared, CancellationToken token = default)
    {
        shared.Validate();
        List<(string Pending, string Target)> files = [];
        try
        {
            await StageSharedAsync(resourceRoot, shared, files, token);
            token.ThrowIfCancellationRequested();
            ScenarioContentFiles.Publish(files);
        }
        finally { foreach (var (pending, _) in files) File.Delete(pending); }
    }

    internal static async Task StageSharedAsync(string resourceRoot, SharedScenarioData shared,
        List<(string Pending, string Target)> files, CancellationToken token)
    {
        shared.ResolvePaths(resourceRoot);
        await StageAsync(resourceRoot, shared.DefinitionsPath, shared.Definitions, files, ScenarioContentFiles.DataJsonOptions, token);
        await StageAsync(resourceRoot, shared.OfficersPath, shared.Officers, files, ScenarioContentFiles.DataJsonOptions, token);
        await StageAsync(resourceRoot, shared.GeographyPath, shared.Geography, files, ScenarioContentFiles.DataJsonOptions, token);
    }

    internal static async Task StageAsync<T>(string resourceRoot, string relative, T value,
        List<(string Pending, string Target)> files, JsonSerializerOptions options, CancellationToken token)
    {
        string target = PackageFiles.Resolve(resourceRoot, relative), pending = target + ".pending";
        if (files.Any(f => f.Target.Equals(target, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException($"Duplicate scenario publication path: {relative}.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        files.Add((pending, target));
        await File.WriteAllTextAsync(pending, JsonSerializer.Serialize(value, options), System.Text.Encoding.UTF8, token);
    }

    private static Task<T> ReadAsync<T>(string root, string path, CancellationToken token) =>
        PackageFiles.ReadAsync<T>(PackageFiles.Resolve(root, path), token);

    private sealed record SelectionDefinitions(int SchemaVersion, InformationCatalog Information);
    private sealed record SelectionOverrides(ContentOverride<InformationCatalog>? Information = null);
    private sealed record SelectionDocument(int SchemaVersion, int Year, int Month, int Day,
        Scenarios.ForceInitialState[] Forces, Scenarios.SettlementInitialState[] Settlements, ScenarioOfficerState[] Officers,
        OfficerDefaults[] AdditionalOfficerDefaults, San9.Core.Domain.UnitState[] Units, ScenarioInformationState Information,
        San9.Core.Domain.AllianceState? Alliance, SelectionOverrides DefinitionOverrides);
}
