using System.Collections.Immutable;

namespace San9.Core.Domain;

public enum EquipmentSlot { MainHand, OffHand, Helmet, Armor, Mount, Treasure }
public enum EquipmentRarity { Ordinary, Precious, Rare }
public enum EquipmentUse { Attack, Defense }

/// <summary>Content-authored equipment type. Instances have independent identities and experience.</summary>
public sealed record EquipmentDefinition(string Id, EquipmentSlot Slot, EquipmentRarity Rarity,
    OfficerAbilities BaseBonuses, OfficerAbilities BonusPerLevel)
{
    public int? LegacyTreasureId { get; init; }
    public int? TacticId { get; init; }
    public bool EscapeMount { get; init; }
    public string Name { get; init; } = "";
    public string? ResearchProjectId { get; init; }
    public int ProductionCost { get; init; }
    public int ProductionDays { get; init; }
    // Empty means no restrictions. Values are definition IDs, not source-game numeric slots.
    public ImmutableArray<string> CompatibleWeapons { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore] public bool IsValid => !string.IsNullOrWhiteSpace(Id) && Enum.IsDefined(Slot) && Enum.IsDefined(Rarity) &&
        LegacyTreasureId is not < 0 && TacticId is not < 0 && (!EscapeMount || Slot == EquipmentSlot.Mount) &&
        Name is not null && ProductionCost >= 0 && ProductionDays >= 0 &&
        (ResearchProjectId is null ? ProductionCost == 0 && ProductionDays == 0 :
            !string.IsNullOrWhiteSpace(ResearchProjectId) && ProductionDays > 0 && LegacyTreasureId is null && Slot != EquipmentSlot.Treasure) &&
        BaseBonuses.IsNonnegative && BonusPerLevel.IsNonnegative && !CompatibleWeapons.IsDefault &&
        CompatibleWeapons.All(id => !string.IsNullOrWhiteSpace(id)) &&
        CompatibleWeapons.Distinct(StringComparer.Ordinal).Count() == CompatibleWeapons.Length &&
        (Slot is EquipmentSlot.MainHand or EquipmentSlot.OffHand || CompatibleWeapons.IsEmpty);
}

public sealed record EquipmentProductionProgress(string DefinitionId, int SettlementId, int DaysRemaining);

/// <summary>A unique object owned by either an officer or a force's inventory.</summary>
public sealed record EquipmentInstance(long Id, string DefinitionId, int? OfficerId, int? InventoryForceId,
    bool Equipped, int Level = 1, int Experience = 0)
{
    public int? LegacyTreasureId { get; init; }
    public bool IsLost { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? LastExperienceExchange { get; init; }
    [System.Text.Json.Serialization.JsonIgnore] public bool IsValid => Id >= 0 && !string.IsNullOrWhiteSpace(DefinitionId) &&
        LegacyTreasureId is not < 0 && (LastExperienceExchange is null || !string.IsNullOrWhiteSpace(LastExperienceExchange)) &&
        OfficerId is not < 0 && InventoryForceId is not < 0 &&
        !(OfficerId is not null && InventoryForceId is not null) &&
        (OfficerId is not null || InventoryForceId is not null || LegacyTreasureId is not null || IsLost) &&
        (!IsLost || OfficerId is null && InventoryForceId is null && LegacyTreasureId is null && !Equipped) &&
        (!Equipped || OfficerId is not null) && Level is >= 1 and <= 5 && Experience is >= 0 and < 100 &&
        (Level != 5 || Experience == 0);
}

public sealed class EquipmentRulebook
{
    public static EquipmentRulebook Empty { get; } = new([]);
    public ImmutableDictionary<string, EquipmentDefinition> Items { get; }

    public EquipmentRulebook(IEnumerable<EquipmentDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var items = definitions.ToArray();
        if (items.Any(item => item is null || !item.IsValid) ||
            items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != items.Length)
            throw new ArgumentException("Invalid equipment definitions.");
        Items = items.ToImmutableDictionary(item => item.Id, StringComparer.Ordinal);
        if (!EquipmentRules.ValidInventory([], Items) ||
            Items.Values.Where(item => item.LegacyTreasureId is not null).GroupBy(item => item.LegacyTreasureId).Any(g => g.Count() > 1))
            throw new ArgumentException("Invalid equipment definition references.");
    }
}

public static class EquipmentRules
{
    public static bool ValidInventory(IEnumerable<EquipmentInstance> items,
        IReadOnlyDictionary<string, EquipmentDefinition> definitions)
    {
        var all = items.ToArray();
        if (definitions.Any(pair => pair.Key != pair.Value.Id || !pair.Value.IsValid) ||
            definitions.Values.Any(definition => definition.CompatibleWeapons.Any(id =>
                !definitions.TryGetValue(id, out var other) ||
                other.Slot != (definition.Slot == EquipmentSlot.MainHand ? EquipmentSlot.OffHand : EquipmentSlot.MainHand))) ||
            all.Any(item => !item.IsValid || !definitions.ContainsKey(item.DefinitionId)) ||
            all.Any(item => item.LegacyTreasureId != definitions[item.DefinitionId].LegacyTreasureId) ||
            all.Select(item => item.Id).Distinct().Count() != all.Length ||
            all.Where(item => item.LegacyTreasureId is not null).GroupBy(item => item.LegacyTreasureId).Any(group => group.Count() > 1))
            return false;
        foreach (var loadout in all.Where(item => item.Equipped).GroupBy(item => item.OfficerId!.Value))
        {
            if (loadout.GroupBy(item => definitions[item.DefinitionId].Slot).Any(group => group.Count() > 1)) return false;
            var main = loadout.FirstOrDefault(item => definitions[item.DefinitionId].Slot == EquipmentSlot.MainHand);
            var off = loadout.FirstOrDefault(item => definitions[item.DefinitionId].Slot == EquipmentSlot.OffHand);
            if (main is not null && off is not null && !Compatible(definitions[main.DefinitionId], definitions[off.DefinitionId])) return false;
        }
        return true;
    }

    public static bool Compatible(EquipmentDefinition main, EquipmentDefinition off) =>
        main.Slot == EquipmentSlot.MainHand && off.Slot == EquipmentSlot.OffHand &&
        (main.CompatibleWeapons.IsEmpty || main.CompatibleWeapons.Contains(off.Id, StringComparer.Ordinal)) &&
        (off.CompatibleWeapons.IsEmpty || off.CompatibleWeapons.Contains(main.Id, StringComparer.Ordinal));

    /// <summary>Returns a replacement loadout, or null when the requested change is invalid.</summary>
    public static ImmutableArray<EquipmentInstance>? Equip(IEnumerable<EquipmentInstance> items,
        IReadOnlyDictionary<string, EquipmentDefinition> definitions, int officerId, long itemId)
    {
        var all = items.ToImmutableArray();
        if (officerId < 0 || !ValidInventory(all, definitions))
            return null;
        var target = all.FirstOrDefault(item => item.Id == itemId);
        if (target is null || target.OfficerId != officerId) return null;
        var definition = definitions[target.DefinitionId];
        var equipped = all.Where(item => item.OfficerId == officerId && item.Equipped && item.Id != target.Id &&
            definitions[item.DefinitionId].Slot != definition.Slot).ToArray();
        if (equipped.GroupBy(item => definitions[item.DefinitionId].Slot).Any(group => group.Count() > 1)) return null;
        var other = equipped.FirstOrDefault(item => definitions[item.DefinitionId].Slot ==
            (definition.Slot == EquipmentSlot.MainHand ? EquipmentSlot.OffHand : EquipmentSlot.MainHand));
        if (other is not null && (definition.Slot is EquipmentSlot.MainHand or EquipmentSlot.OffHand) &&
            !(definition.Slot == EquipmentSlot.MainHand ? Compatible(definition, definitions[other.DefinitionId]) : Compatible(definitions[other.DefinitionId], definition)))
            return null;
        return all.Select(item => item.Id == target.Id ? item with { Equipped = true } :
            item.OfficerId == officerId && item.Equipped && definitions[item.DefinitionId].Slot == definition.Slot ? item with { Equipped = false } : item).ToImmutableArray();
    }

    public static EquipmentInstance MoveToOfficer(EquipmentInstance item, int officerId)
    {
        if (!item.IsValid || officerId < 0) throw new ArgumentException("Invalid equipment transfer.");
        return item with { OfficerId = officerId, InventoryForceId = null, Equipped = false, IsLost = false };
    }

    public static EquipmentInstance MoveToInventory(EquipmentInstance item, int forceId)
    {
        if (!item.IsValid || forceId < 0) throw new ArgumentException("Invalid equipment transfer.");
        return item with { OfficerId = null, InventoryForceId = forceId, Equipped = false, IsLost = false };
    }

    public static OfficerAbilities Bonuses(IEnumerable<EquipmentInstance> items,
        IReadOnlyDictionary<string, EquipmentDefinition> definitions, int officerId)
    {
        var all = items.ToArray();
        if (officerId < 0 || !ValidInventory(all, definitions)) throw new ArgumentException("Invalid equipment inventory.");
        return SumBonuses(all.Where(item => item.Equipped && item.OfficerId == officerId), definitions);
    }

    public static ImmutableDictionary<int, OfficerAbilities> BonusesByOfficer(IEnumerable<EquipmentInstance> items,
        IReadOnlyDictionary<string, EquipmentDefinition> definitions)
    {
        var all = items.ToArray();
        if (!ValidInventory(all, definitions)) throw new ArgumentException("Invalid equipment inventory.");
        return all.Where(item => item.Equipped).GroupBy(item => item.OfficerId!.Value)
            .ToImmutableDictionary(group => group.Key, group => SumBonuses(group, definitions));
    }

    private static OfficerAbilities SumBonuses(IEnumerable<EquipmentInstance> equipped,
        IReadOnlyDictionary<string, EquipmentDefinition> definitions)
    {
        long war = 0, intelligence = 0, politics = 0, leadership = 0;
        foreach (var item in equipped)
        {
            var definition = definitions[item.DefinitionId];
            int growth = item.Level - 1;
            war += (long)definition.BaseBonuses.War + (long)definition.BonusPerLevel.War * growth;
            intelligence += (long)definition.BaseBonuses.Intelligence + (long)definition.BonusPerLevel.Intelligence * growth;
            politics += (long)definition.BaseBonuses.Politics + (long)definition.BonusPerLevel.Politics * growth;
            leadership += (long)definition.BaseBonuses.Leadership + (long)definition.BonusPerLevel.Leadership * growth;
        }
        return new((int)Math.Min(int.MaxValue, war), (int)Math.Min(int.MaxValue, intelligence),
            (int)Math.Min(int.MaxValue, politics), (int)Math.Min(int.MaxValue, leadership));
    }

    public static EquipmentInstance RecordUse(EquipmentInstance item, EquipmentDefinition definition, EquipmentUse use)
    {
        if (!item.IsValid || !definition.IsValid || item.DefinitionId != definition.Id || !Enum.IsDefined(use))
            throw new ArgumentException("Invalid equipment use.");
        bool earns = item.Equipped && item.Level < 5 && (use switch
        {
            EquipmentUse.Attack => definition.Slot is EquipmentSlot.MainHand or EquipmentSlot.OffHand,
            EquipmentUse.Defense => definition.Slot is EquipmentSlot.Helmet or EquipmentSlot.Armor,
            _ => false
        });
        if (!earns) return item;
        int experience = item.Experience + 1;
        return experience == 100 ? item with { Level = item.Level + 1, Experience = 0 } : item with { Experience = experience };
    }
}
