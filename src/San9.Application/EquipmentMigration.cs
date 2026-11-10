using San9.Content.Scenarios;
using San9.Core.Domain;

namespace San9.Application;

internal static class EquipmentMigration
{
    internal static SessionSnapshot Initialize(SessionSnapshot snapshot, ScenarioInitialState content)
    {
        if (snapshot.World.EquipmentInitialized) return snapshot;
        if (snapshot.World.Equipment.Count != 0)
            throw new InvalidDataException("Uninitialized equipment state contains existing instances.");
        var migrated = LegacyEquipmentMigration.Convert(content.Information.Treasures, snapshot.World.Treasures,
            content.Information.Equipment);
        return snapshot with { World = snapshot.World with
        {
            Equipment = UnequipNonServing(migrated.Instances, snapshot.World.Officers), EquipmentInitialized = true
        } };
    }

    internal static EquipmentInstance[] UnequipNonServing(IEnumerable<EquipmentInstance> items, IEnumerable<OfficerState> officers)
    {
        var byId = officers.ToDictionary(officer => officer.Id);
        return items.Select(item => item.Equipped && item.OfficerId is int holder &&
            byId.TryGetValue(holder, out var officer) && !officer.IsServing ? item with { Equipped = false } : item).ToArray();
    }
}
