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
        Assert.Equal(45, store.Load().PlayerAutoSearchMinimumPolitics);
        Assert.False(store.Load().DisableComputerForceConsolidation);
        Assert.False(store.Load().DisableComputerPrisonerExecution);
        Assert.Equal(100, store.Load().CityBattleDamagePercent);
        Assert.Equal(100, store.Load().NoRetreatCaptureBonus);
        Assert.False(store.Load().UseHighestAbilityForCapture);
        Assert.False(store.Load().ConfigureTacticsOnFormationSelection);
        Assert.False(Directory.Exists(_directory));
        var settings = new CompanionSettings
        {
            PlayerAutoDomesticEnabled = true, OfficerAbilityCap = 125,
            DisableComputerForceConsolidation = true,
            DisableComputerPrisonerExecution = true,
            EnableNavalTacticFallback = true, TacticAllocation = new(0, 1, 2, 3, 4),
            PlayerAutoSearchEnabled = true, PlayerAutoSearchMinimumPolitics = 60,
            HideRoutineSearchReports = true,
            ConfigureTacticsOnFormationSelection = true,
            ComputerRevenuePercent = 125, ComputerHarvestPercent = 200,
            CityBattleDamagePercent = 50,
            NoRetreatCaptureBonus = 20,
            UseHighestAbilityForCapture = true,
            ComputerStrategyPermissions = new() { Alienate = false, Burn = false },
            PlayerDelegatedStrategyPermissions = new() { Seize = false, Gossip = false },
        };
        store.Save(settings);
        Assert.Equal(settings, new CompanionSettingsStore(FilePath).Load());
        byte[] saved = File.ReadAllBytes(FilePath);
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(settings with { OfficerAbilityCap = 99 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(settings with { PlayerAutoSearchMinimumPolitics = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(settings with { ComputerRevenuePercent = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(settings with { ComputerHarvestPercent = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(settings with { CityBattleDamagePercent = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(settings with { NoRetreatCaptureBonus = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(settings with { NoRetreatCaptureBonus = 101 }));
        Assert.Throws<ArgumentNullException>(() => store.Save(settings with { ComputerStrategyPermissions = null! }));
        Assert.Throws<ArgumentNullException>(() => store.Save(settings with { PlayerDelegatedStrategyPermissions = null! }));
        Assert.Equal(saved, File.ReadAllBytes(FilePath));

        var document = JsonNode.Parse(saved)!;
        document["settings"]!.AsObject().Remove("officerAbilityCap");
        string incomplete = document.ToJsonString();
        File.WriteAllText(FilePath, incomplete, Encoding.UTF8);
        Assert.Throws<JsonException>(() => store.Load());
        Assert.Equal(incomplete, File.ReadAllText(FilePath, Encoding.UTF8));

        document = JsonNode.Parse(saved)!;
        document["settings"]!["computerStrategyPermissions"]!.AsObject().Remove("burn");
        File.WriteAllText(FilePath, document.ToJsonString(), Encoding.UTF8);
        Assert.Throws<JsonException>(() => store.Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
