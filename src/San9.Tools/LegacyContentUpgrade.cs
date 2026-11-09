using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using San9.Application.Diagnostics;
using San9.Content.Interface;
using San9.Content.Packages;
using San9.Content.Scenarios;
using San9.Content.Text;
using San9.Importer.San9PkTc101.Packages;

namespace San9.Tools;

internal static class LegacyContentUpgrade
{
    public static async Task RunAsync(string sourcePath, string outputPath, bool showOfficerAges)
    {
        string source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourcePath));
        string output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputPath));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (source.Equals(output, comparison) || source.StartsWith(output + Path.DirectorySeparatorChar, comparison) ||
            output.StartsWith(source + Path.DirectorySeparatorChar, comparison))
            throw new ArgumentException("Source and output must be separate directories.");
        if (!Directory.Exists(source) || Directory.Exists(output) || File.Exists(output))
            throw new ArgumentException("Source must exist and output must not exist.");
        string parent = Path.GetDirectoryName(output) ?? throw new ArgumentException("Missing output parent.");
        Directory.CreateDirectory(parent);
        string stage = Path.Combine(parent, ".legacy-upgrade-" + Guid.NewGuid().ToString("N"));
        try
        {
            var catalog = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(source, "catalog.json"), Encoding.UTF8))!.AsObject();
            if (catalog["schemaVersion"]?.GetValue<int>() != 4) throw new InvalidDataException("Expected legacy catalog schema 4.");
            var entries = catalog["scenarios"]!.AsArray();
            var ages = entries.ToDictionary(e => e!["id"]!.GetValue<string>(), _ => showOfficerAges, StringComparer.Ordinal);
            string baseline = entries[0]!["id"]!.GetValue<string>();
            var prepared = await LegacyScenarioDataUpgrade.PrepareAsync(source, baseline, ages);
            string texturesPath = catalog["textures"]!.GetValue<string>();
            string oldTextures = PackageFiles.Resolve(source, texturesPath);
            CopyDirectory(source, stage, oldTextures);
            await LegacyTextureSkinUpgrade.UpgradeAsync(oldTextures, PackageFiles.Resolve(stage, texturesPath));
            var textures = await TexturePackageFiles.LoadAsync(PackageFiles.Resolve(stage, texturesPath));
            var previews = new List<ScenarioPreviewEntry>();
            string? messagesPath = null;
            foreach (var entry in entries)
            {
                string package = entry!["package"]!.GetValue<string>();
                var manifest = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(PackageFiles.Resolve(source, package), "content.json"), Encoding.UTF8))!.AsObject();
                previews.Add(manifest["preview"]!.Deserialize<ScenarioPreviewEntry>(PackageFiles.JsonOptions)!);
                messagesPath ??= manifest["sharedMessages"]!.GetValue<string>();
                string presentationPath = PackageFiles.Resolve(stage, manifest["presentation"]!.GetValue<string>());
                var presentation = await PackageFiles.ReadAsync<CommandPresentation>(presentationPath);
                await WriteAsync(presentationPath, LegacyPresentationUpgrade.Prepare(presentation));
            }
            var messages = await MessageLibraryFiles.LoadAsync(PackageFiles.Resolve(source, messagesPath!));
            string openingPath = catalog["opening"]!.GetValue<string>();
            var oldOpening = JsonNode.Parse(await File.ReadAllTextAsync(PackageFiles.Resolve(source, openingPath), Encoding.UTF8))!.AsObject();
            var opening = LegacyOpeningUpgrade.Prepare(oldOpening, prepared.Worlds.Single(w => w.Scenario.ScenarioId == baseline).Scenario,
                textures, messages, previews, stage);
            await WriteAsync(PackageFiles.Resolve(stage, openingPath), opening.Opening);
            await WriteAsync(PackageFiles.Resolve(stage, "shared/officer-flags.json"), opening.Flags);
            catalog["schemaVersion"] = ContentCatalog.CurrentSchemaVersion;
            catalog["officerFlags"] = "shared/officer-flags.json";
            catalog["definitions"] = "shared/definitions.json";
            catalog["officerDefaults"] = "shared/officer-defaults.json";
            await File.WriteAllTextAsync(Path.Combine(stage, "catalog.json"), catalog.ToJsonString(PackageFiles.JsonOptions), Encoding.UTF8);
            await LegacyScenarioDataUpgrade.PublishWorldsAsync(prepared, stage);
            await ContentVerification.VerifyAsync(stage, Console.WriteLine);
            foreach (var world in prepared.Worlds)
            {
                _ = San9.Application.GameSession.Start(world.Scenario, 42, settings: new() { MixedTroopsEnabled = true });
                Console.WriteLine($"Verified mixed troop startup: {world.Scenario.ScenarioId}.");
            }
            Directory.Move(stage, output);
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }

    private static async Task WriteAsync<T>(string path, T value) =>
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, PackageFiles.JsonOptions), Encoding.UTF8);

    private static void CopyDirectory(string source, string destination, string excluded)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Links are not supported in legacy content.");
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Links are not supported in legacy content.");
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
        foreach (string directory in Directory.EnumerateDirectories(source))
            if (!string.Equals(directory, excluded, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), excluded);
    }
}
