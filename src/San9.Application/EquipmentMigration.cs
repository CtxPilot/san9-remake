using San9.Content.Scenarios;

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
        return snapshot with { World = snapshot.World with { Equipment = migrated.Instances, EquipmentInitialized = true } };
    }
}
