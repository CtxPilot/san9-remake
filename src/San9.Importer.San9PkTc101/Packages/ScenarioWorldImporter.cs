using System.Globalization;
using San9.Content.Packages;
using San9.Content.Scenarios;
using San9.Importer.San9PkTc101.Scenarios;

namespace San9.Importer.San9PkTc101.Packages;

/// <summary>Recompiles source worlds while preserving separately owned presentation and shared resources.</summary>
public static class ScenarioWorldImporter
{
    public static async Task RebuildAsync(string gameDirectory, string output, Action<string>? progress = null,
        CancellationToken token = default)
    {
        var source = new San9SourceDirectory(gameDirectory);
        var identity = await San9PkTc101Detector.DetectAsync(source.Executable, token);
        if (!identity.IsMatch) throw new InvalidDataException(identity.Reason);
        output = Path.GetFullPath(output);
        var catalog = await ContentCatalogFiles.LoadAsync(output, token);
        byte[] exe = await File.ReadAllBytesAsync(source.Executable, token);
        byte[] messages = await File.ReadAllBytesAsync(source.File("M_MsgPK.s9"), token);
        byte[] biographies = await File.ReadAllBytesAsync(source.File("M_RtdnPK.s9"), token);
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(source.Root, "D_Sce*.S9"))
        {
            using var stream = File.OpenRead(path);
            string id = San9ScenarioArchive.ReadHeader(stream).ScenarioId.ToString(CultureInfo.InvariantCulture);
            if (!sources.TryAdd(id, path)) throw new InvalidDataException($"Duplicate source scenario identity: {id}.");
        }
        List<(ScenarioCatalogEntry Entry, ScenarioInitialState Scenario)> worlds = [];
        foreach (var entry in catalog.Scenarios)
        {
            token.ThrowIfCancellationRequested();
            if (!sources.TryGetValue(entry.Id, out var path)) throw new InvalidDataException($"Missing source scenario: {entry.Id}.");
            byte[] bytes = await File.ReadAllBytesAsync(path, token);
            var (scenario, _) = ScenarioContentCompiler.Compile(exe, bytes, messages, biographies);
            worlds.Add((entry, scenario));
        }
        var baseline = worlds.Single(w => w.Entry.Id == "0").Scenario;
        var shared = SharedScenarioData.From(baseline, "shared/definitions.json", "shared/officer-defaults.json", "shared/maps/china/geography.json");
        await ScenarioContentFiles.WriteWorldsAsync(output, shared, worlds.Select(w => (w.Entry.Package, w.Scenario)).ToArray(), token);
        foreach (var (entry, _) in worlds) progress?.Invoke($"Rebuilt scenario world: {entry.Id}.");
    }
}
