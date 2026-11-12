using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Immutable;
using San9.Application.Persistence;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Content.Scenarios;

namespace San9.Application.Tests;

public sealed class EquipmentPersistenceTests
{
    [Fact]
    public void ExchangeHistoryRoundTripsAndLegacyItemWithoutHistoryLoads()
    {
        var item = new EquipmentInstance(8, "sword", null, 1, false, 2, 33)
        {
            LastExperienceExchange = "42:u1:u3",
            ExperienceExchanges = ImmutableSortedSet.Create(StringComparer.Ordinal, "42:u1:u2", "42:u1:u3")
        };
        var json = JsonNode.Parse(JsonSerializer.Serialize(item, StrictJson.Options))!.AsObject();
        var restored = StrictJson.Deserialize<EquipmentInstance>(Encoding.UTF8.GetBytes(json.ToJsonString()));
        Assert.True(item.ExperienceExchanges.SetEquals(restored.ExperienceExchanges!));
        json.Remove("experienceExchanges");
        var legacy = StrictJson.Deserialize<EquipmentInstance>(Encoding.UTF8.GetBytes(json.ToJsonString()));
        Assert.Null(legacy.ExperienceExchanges);
        Assert.Equal(item.LastExperienceExchange, legacy.LastExperienceExchange);
    }

    [Fact]
    public void MigratedNamedItemIsDeferredForNonServingHolder()
    {
        var person = new OfficerState(2, null, 1, new(50, 50, 50, 50), false) { Status = OfficerStatus.Free };
        var named = new EquipmentInstance(5, "legacy-treasure:5", 2, null, true) { LegacyTreasureId = 5 };
        var deferred = Assert.Single(EquipmentMigration.UnequipNonServing([named], [person]));
        Assert.False(deferred.Equipped);
        Assert.True(deferred.EquipOnService);
        Assert.Equal(5, deferred.LegacyTreasureId);
    }
    [Fact]
    public void InitialIssueMigratesOldSaveOnceAndDoesNotReissueAfterUnequip()
    {
        var scenario = G01ScenarioTests.Fixture();
        scenario = scenario with { Information = scenario.Information with { Equipment = EquipmentPreset.Basic() } };
        var session = GameSession.Start(scenario, 42);
        var original = session.Capture();
        var owned = original.World.Equipment.Where(e => e.OfficerId == 1 && e.DefinitionId.StartsWith("issue-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, owned.Length);
        var old = original with { InitialEquipmentIssued = null, World = original.World with
            { Equipment = original.World.Equipment.Where(e => !e.DefinitionId.StartsWith("issue-", StringComparison.Ordinal)).ToArray() } };
        var migrated = GameSession.Restore(scenario, old).Capture();
        Assert.True(migrated.InitialEquipmentIssued);
        Assert.Equal(3, migrated.World.Equipment.Count(e => e.OfficerId == 1 && e.DefinitionId.StartsWith("issue-", StringComparison.Ordinal)));
        var unequipped = migrated with { World = migrated.World with { Equipment = migrated.World.Equipment
            .Select(e => e.OfficerId == 1 && e.DefinitionId == "issue-short-sword" ? e with { Equipped = false } : e).ToArray() } };
        var saved = GameSession.Restore(scenario, unequipped);
        var restored = SaveService.Decode(SaveService.Encode(saved), scenario).Capture();
        Assert.False(restored.World.Equipment.Single(e => e.OfficerId == 1 && e.DefinitionId == "issue-short-sword").Equipped);
        Assert.Equal(unequipped.World.Equipment.Count, restored.World.Equipment.Count);
    }
    [Fact]
    public void LegacyEquipmentDefinitionsDefaultGradeAndRequirements()
    {
        var definition = new EquipmentDefinition("spear", EquipmentSlot.MainHand, EquipmentRarity.Ordinary, default, default);
        var json = JsonNode.Parse(JsonSerializer.Serialize(definition, StrictJson.Options))!.AsObject();
        json.Remove("grade");
        json.Remove("minimumAbilities");
        var restored = StrictJson.Deserialize<EquipmentDefinition>(Encoding.UTF8.GetBytes(json.ToJsonString()));
        Assert.Equal(1, restored.Grade);
        Assert.Equal(default, restored.MinimumAbilities);
    }

    [Fact]
    public void WorldSnapshotAcceptsLegacyMissingEquipmentAndRoundTripsInstances()
    {
        var world = WorldState.Create(new(190, 1, 1), 1, [new ForceState(1, 0)], [], [],
            corps: [new CorpsState(1, 1, null, new(0, 0)) { IsRoot = true }]);
        var source = new SimulationEngine(world).Snapshot;
        var json = JsonNode.Parse(JsonSerializer.Serialize(source, StrictJson.Options))!.AsObject();
        json.Remove("equipment");
        json.Remove("equipmentInitialized");
        var legacy = StrictJson.Deserialize<WorldSnapshot>(Encoding.UTF8.GetBytes(json.ToJsonString()));
        Assert.Empty(legacy.Equipment);
        Assert.False(legacy.EquipmentInitialized);
        var current = source with { Equipment = [new EquipmentInstance(8, "sword", null, 1, false, 2, 33)] };
        var restored = StrictJson.Deserialize<WorldSnapshot>(JsonSerializer.SerializeToUtf8Bytes(current, StrictJson.Options));
        Assert.Equal(current.Equipment, restored.Equipment);
        Assert.Single(WorldState.Restore(restored).BeginMutation().CapturePresentation().Equipment);
    }

    [Fact]
    public void LegacyInformationWithoutEquipmentRetainsAnEmptyCatalog()
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(InformationDefinitions.Empty, StrictJson.Options))!.AsObject();
        json.Remove("equipment");
        var restored = StrictJson.Deserialize<InformationDefinitions>(Encoding.UTF8.GetBytes(json.ToJsonString()));
        Assert.Empty(restored.Equipment);
        Assert.False(restored.Equipment.IsDefault);
    }

    [Fact]
    public void EquipmentCommandAndEventRoundTripPolymorphically()
    {
        GameCommand command = new ConfigureEquipmentCommand(Guid.NewGuid(), 1, 3, 2, EquipmentSlot.MainHand, 5);
        DomainEvent changed = new EquipmentConfiguredEvent(new(190, 1, 1), 1, 2, EquipmentSlot.MainHand, 5);
        Assert.Equal(command, StrictJson.Deserialize<GameCommand>(JsonSerializer.SerializeToUtf8Bytes(command, StrictJson.Options)));
        Assert.Equal(changed, StrictJson.Deserialize<DomainEvent>(JsonSerializer.SerializeToUtf8Bytes(changed, StrictJson.Options)));
        GameCommand production = new StartEquipmentProductionCommand(Guid.NewGuid(), 1, 4, 2, "spear");
        GameCommand transfer = new TransferEquipmentCommand(Guid.NewGuid(), 1, 5, 7, 2);
        DomainEvent completed = new EquipmentProductionCompletedEvent(new(190, 1, 1), 1, 2, "spear", 7);
        GameCommand purchase = new PurchaseEquipmentCommand(Guid.NewGuid(), 1, 6, 2, "spear");
        DomainEvent purchased = new EquipmentPurchasedEvent(new(190, 1, 1), 1, 2, "spear", 8, 400);
        Assert.Equal(production, StrictJson.Deserialize<GameCommand>(JsonSerializer.SerializeToUtf8Bytes(production, StrictJson.Options)));
        Assert.Equal(transfer, StrictJson.Deserialize<GameCommand>(JsonSerializer.SerializeToUtf8Bytes(transfer, StrictJson.Options)));
        Assert.Equal(completed, StrictJson.Deserialize<DomainEvent>(JsonSerializer.SerializeToUtf8Bytes(completed, StrictJson.Options)));
        Assert.Equal(purchase, StrictJson.Deserialize<GameCommand>(JsonSerializer.SerializeToUtf8Bytes(purchase, StrictJson.Options)));
        Assert.Equal(purchased, StrictJson.Deserialize<DomainEvent>(JsonSerializer.SerializeToUtf8Bytes(purchased, StrictJson.Options)));
    }
}
