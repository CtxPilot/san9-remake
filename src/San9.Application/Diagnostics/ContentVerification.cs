using San9.Application.Presentation;
using San9.Core.Domain;
using San9.Content.Images;
using San9.Content.Packages;

namespace San9.Application.Diagnostics;

public static class ContentVerification
{
    public static async Task VerifyAsync(string root, Action<string>? progress = null, CancellationToken token = default)
    {
        var catalog = await ContentCatalogFiles.LoadAsync(root, token);
        _ = await MovieCatalogFiles.LoadAsync(root, token);
        var textures = await TexturePackageFiles.LoadAsync(PackageFiles.Resolve(root, catalog.Textures), token);
        var flags = await ContentCatalogFiles.LoadOfficerFlagsAsync(root, catalog, token);
        flags.ValidateReferences(textures.Map.Clips.Select(c => c.Id).ToHashSet());
        var opening = await ContentCatalogFiles.LoadOpeningAsync(root, catalog, token);
        opening.Menu.Validate(textures.Assets); opening.Selection.Validate(textures.Assets);
        foreach (var entry in catalog.Scenarios)
        {
            token.ThrowIfCancellationRequested();
            var scenario = await ScenarioContentFiles.LoadAsync(PackageFiles.Resolve(root, entry.Package), token);
            if (scenario.Scenario.ScenarioId != entry.Id)
                throw new InvalidDataException($"Scenario identity differs from catalog: {entry.Id}.");
            _ = ClientContentFiles.Resolve(scenario, textures, PackageFiles.Resolve(root, catalog.Textures));
            var preview = opening.Selection.Entries.Single(p => p.Id == entry.Id);
            if (scenario.Scenario.Year != preview.Year || scenario.Scenario.Month != preview.Month)
                throw new InvalidDataException($"Scenario preview date differs from world: {entry.Id}");
            var forces = San9.Application.Presentation.ScenarioForceSelection.Read(
                scenario.Scenario, scenario.Text, scenario.IllustrationRoles);
            if (preview.CityColors.Any(p => !forces.CityColors.TryGetValue(p.Key, out var color) || color != p.Value))
                throw new InvalidDataException($"Scenario preview ownership differs from world: {entry.Id}; rebuild opening content.");
            foreach (string path in scenario.Presentation.Sounds.Values)
                if (!File.Exists(PackageFiles.Resolve(scenario.ResourceRoot, path))) throw new InvalidDataException($"Missing sound: {path}");
            var session = San9.Application.GameSession.Start(scenario.Scenario, 42);
            if (!session.Snapshot.Forces.Select(f => f.Id).Order().SequenceEqual(scenario.Scenario.Forces.Select(f => f.Id).Order()) ||
                !session.Snapshot.Settlements.Select(s => s.Id).Order().SequenceEqual(scenario.Scenario.Settlements.Select(s => s.Id).Order()) ||
                !session.Snapshot.Officers.Select(o => o.Id).Order().SequenceEqual(scenario.Scenario.Officers.Select(o => o.State.Id).Order()))
                throw new InvalidDataException($"Initial world lost scenario identities: {entry.Id}");
            VerifyNewGame(scenario, token);
            var query = new San9.Application.Presentation.InformationQuery(session, scenario.Text);
            foreach (var category in new[] { San9.Application.Presentation.InformationCategory.Forces, San9.Application.Presentation.InformationCategory.Cities,
                San9.Application.Presentation.InformationCategory.Regions, San9.Application.Presentation.InformationCategory.Officers, San9.Application.Presentation.InformationCategory.Treasures })
                _ = query.Read(category);
            progress?.Invoke($"Verified {entry.Id}: {scenario.Scenario.Forces.Length} forces; {scenario.Scenario.Settlements.Length} settlements; {scenario.Scenario.Officers.Length} officers; world and queries ready.");
        }
        await ImageAssetFiles.VerifyAsync(PackageFiles.Resolve(root, catalog.Textures), textures.Assets, token);
    }
    private static void VerifyNewGame(ScenarioContent content, CancellationToken token)
    {
        // Start each selectable force separately so relations, computer bonuses and reserved activation keep their normal scopes.
        var players = ScenarioForceSelection.Read(content.Scenario, content.Text, content.IllustrationRoles).Forces.Select(f => f.Id).ToArray();
        foreach (var mode in Enum.GetValues<GameMode>())
        foreach (int player in players)
        {
            token.ThrowIfCancellationRequested();
            var session = GameSession.Start(content.Scenario, 42, settings: new() { Mode = mode }, playerForceIds: [player]);
            var messages = NewGameIntroduction.Produce(session.View, session.Messages(content.Text), content.Text, content.Presentation.Progress!);
            if (messages.Count == 0 || messages.Any(m => string.IsNullOrWhiteSpace(m.Message.BbCode)))
                throw new InvalidDataException($"Missing new-game introduction: {content.Scenario.ScenarioId}/{player}/{mode}");
        }
    }
}
