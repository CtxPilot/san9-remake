using San9.Core.Domain;

namespace San9.Core.Tests;

public sealed class EquipmentRulesTests
{
    private static EquipmentDefinition Definition(EquipmentSlot slot) => new("item", slot, EquipmentRarity.Ordinary, default, default);
    private static EquipmentInstance Instance => new(1, "item", 10, null, true);

    [Theory]
    [InlineData(EquipmentSlot.MainHand, EquipmentUse.Attack, 1)]
    [InlineData(EquipmentSlot.OffHand, EquipmentUse.Attack, 1)]
    [InlineData(EquipmentSlot.Helmet, EquipmentUse.Defense, 1)]
    [InlineData(EquipmentSlot.Armor, EquipmentUse.Defense, 1)]
    [InlineData(EquipmentSlot.Mount, EquipmentUse.Attack, 0)]
    [InlineData(EquipmentSlot.Treasure, EquipmentUse.Defense, 0)]
    [InlineData(EquipmentSlot.Helmet, EquipmentUse.Attack, 0)]
    public void UseOnlyTrainsApplicableEquippedItems(EquipmentSlot slot, EquipmentUse use, int experience)
    {
        var definition = Definition(slot);
        Assert.Equal(experience, EquipmentRules.RecordUse(Instance, definition, use).Experience);
        Assert.Equal(0, EquipmentRules.RecordUse(Instance with { Equipped = false }, definition, use).Experience);
    }

    [Fact]
    public void ExperienceLevelsUpAndStopsAtTheCap()
    {
        var definition = Definition(EquipmentSlot.MainHand);
        var upgraded = EquipmentRules.RecordUse(Instance with { Level = 4, Experience = 99 }, definition, EquipmentUse.Attack);
        Assert.Equal(5, upgraded.Level);
        Assert.Equal(0, upgraded.Experience);
        Assert.Same(upgraded, EquipmentRules.RecordUse(upgraded, definition, EquipmentUse.Attack));
    }

    [Fact]
    public void WeaponRestrictionsAreMutualAndUseDefinitionIdentity()
    {
        var main = Definition(EquipmentSlot.MainHand) with { CompatibleWeapons = ["shield"] };
        var shield = Definition(EquipmentSlot.OffHand) with { Id = "shield", CompatibleWeapons = ["item"] };
        Assert.True(EquipmentRules.Compatible(main, shield));
        Assert.False(EquipmentRules.Compatible(main, shield with { CompatibleWeapons = ["other"] }));
        Assert.False(EquipmentRules.Compatible(main, shield with { Id = "bow" }));
    }

    [Fact]
    public void EquippingReplacesOnlyTheSameSlotAndRejectsIncompatibleOrForeignItems()
    {
        var sword = Definition(EquipmentSlot.MainHand) with { CompatibleWeapons = ["shield"] };
        var axe = sword with { Id = "axe", CompatibleWeapons = ["bow"] };
        var shield = Definition(EquipmentSlot.OffHand) with { Id = "shield" };
        var bow = shield with { Id = "bow" };
        var rules = new[] { sword, axe, shield, bow }.ToDictionary(rule => rule.Id);
        EquipmentInstance[] items = [Instance, new(2, "axe", 10, null, false), new(3, "shield", 10, null, true),
            new(4, "item", 20, null, false)];
        Assert.Null(EquipmentRules.Equip(items, rules, 10, 2));
        Assert.Null(EquipmentRules.Equip(items, rules, 10, 4));
        rules["axe"] = axe with { CompatibleWeapons = ["shield"] };
        var changed = EquipmentRules.Equip(items, rules, 10, 2)!.Value;
        Assert.False(changed.Single(item => item.Id == 1).Equipped);
        Assert.True(changed.Single(item => item.Id == 2).Equipped);
        Assert.True(changed.Single(item => item.Id == 3).Equipped);
        Assert.True(items[0].Equipped);
    }

    [Fact]
    public void TransferUnequipsAndPreservesProgress()
    {
        var item = Instance with { Level = 3, Experience = 47 };
        var stored = EquipmentRules.MoveToInventory(item, 7);
        Assert.False(stored.Equipped);
        Assert.Null(stored.OfficerId);
        Assert.Equal(7, stored.InventoryForceId);
        var transferred = EquipmentRules.MoveToOfficer(stored, 12);
        Assert.Equal(12, transferred.OfficerId);
        Assert.Null(transferred.InventoryForceId);
        Assert.Equal(3, transferred.Level);
        Assert.Equal(47, transferred.Experience);
        Assert.False(transferred.Equipped);
    }

    [Fact]
    public void InventoryRejectsBadReferencesAndConflictingSlots()
    {
        var rules = new[] { Definition(EquipmentSlot.MainHand), Definition(EquipmentSlot.MainHand) with { Id = "other" } }
            .ToDictionary(rule => rule.Id);
        Assert.False(EquipmentRules.ValidInventory([Instance, Instance with { Id = 2, DefinitionId = "other" }], rules));
        Assert.False(EquipmentRules.ValidInventory([Instance with { DefinitionId = "unknown" }], rules));
        rules["other"] = rules["other"] with { CompatibleWeapons = ["unknown"] };
        Assert.False(EquipmentRules.ValidInventory([], rules));
    }

    [Fact]
    public void BonusesUseOnlyEquippedItemsAndTheirIndividualLevel()
    {
        var sword = Definition(EquipmentSlot.MainHand) with { BaseBonuses = new(2, 0, 0, 0), BonusPerLevel = new(1, 0, 0, 0) };
        var helmet = Definition(EquipmentSlot.Helmet) with { Id = "helmet", BaseBonuses = new(0, 0, 0, 3) };
        var rules = new[] { sword, helmet }.ToDictionary(rule => rule.Id);
        EquipmentInstance[] items = [Instance with { Level = 3 }, new(2, "helmet", 10, null, true),
            new(3, "item", 10, null, false), new(4, "helmet", 11, null, true)];
        Assert.Equal(new OfficerAbilities(4, 0, 0, 3), EquipmentRules.Bonuses(items, rules, 10));
        Assert.Equal(new OfficerAbilities(0, 0, 0, 3), EquipmentRules.Bonuses(items, rules, 11));
    }

    [Fact]
    public void OnlyEquippedItemsProvideTacticsWhenEquipmentIsInitialized()
    {
        var equipment = new EquipmentRulebook([Definition(EquipmentSlot.MainHand) with { TacticId = 8 }]);
        var treasures = new TreasureRulebook([], equipment);
        var person = new OfficerState(10, 1, 1, new(50, 50, 50, 50), false);
        Assert.True(TacticAvailability.ForOfficer(person, null, [], treasures, [Instance]).ContainsKey(8));
        Assert.False(TacticAvailability.ForOfficer(person, null, [], treasures, [Instance with { Equipped = false }]).ContainsKey(8));
    }

    [Fact]
    public void OwnershipAndExperienceMustBeValid()
    {
        Assert.False((Instance with { InventoryForceId = 2 }).IsValid);
        Assert.False((Instance with { OfficerId = null, InventoryForceId = 2 }).IsValid);
        Assert.False((Instance with { Level = 5, Experience = 1 }).IsValid);
        Assert.Throws<ArgumentException>(() => EquipmentRules.RecordUse(Instance with { DefinitionId = "unknown" }, Definition(EquipmentSlot.MainHand), EquipmentUse.Attack));
    }
}
