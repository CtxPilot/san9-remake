using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using San9.Client.Godot.Settings;
using San9.Core.Domain;

namespace San9.Client.Godot.Tests;

public sealed class CompanionSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "san9-companion-tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "companion-settings.json");

    [Fact]
    public void StandaloneConfigurationRoundTripsAndInvalidDraftCannotReplaceIt()
    {
        var store = new CompanionSettingsStore(FilePath);
        Assert.Equal(CompanionSettings.Default, store.Load());
        Assert.False(Directory.Exists(_directory));
        var settings = new CompanionSettings
        {
            PlayerAutoDomesticEnabled = true, OfficerAbilityCap = 125,
            EnableNavalTacticFallback = true, TacticAllocation = new(0, 1, 2, 3, 4),
        };
        store.Save(settings);
        Assert.Equal(settings, new CompanionSettingsStore(FilePath).Load());
        byte[] saved = File.ReadAllBytes(FilePath);
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(settings with { OfficerAbilityCap = 99 }));
        Assert.Equal(saved, File.ReadAllBytes(FilePath));

        var document = JsonNode.Parse(saved)!;
        document["settings"]!.AsObject().Remove("officerAbilityCap");
        string incomplete = document.ToJsonString();
        File.WriteAllText(FilePath, incomplete, Encoding.UTF8);
        Assert.Throws<JsonException>(() => store.Load());
        Assert.Equal(incomplete, File.ReadAllText(FilePath, Encoding.UTF8));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
