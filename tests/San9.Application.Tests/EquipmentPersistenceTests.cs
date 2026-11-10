using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using San9.Application.Persistence;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Content.Scenarios;

namespace San9.Application.Tests;

public sealed class EquipmentPersistenceTests
{
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
        Assert.Equal(production, StrictJson.Deserialize<GameCommand>(JsonSerializer.SerializeToUtf8Bytes(production, StrictJson.Options)));
        Assert.Equal(transfer, StrictJson.Deserialize<GameCommand>(JsonSerializer.SerializeToUtf8Bytes(transfer, StrictJson.Options)));
        Assert.Equal(completed, StrictJson.Deserialize<DomainEvent>(JsonSerializer.SerializeToUtf8Bytes(completed, StrictJson.Options)));
    }
}
