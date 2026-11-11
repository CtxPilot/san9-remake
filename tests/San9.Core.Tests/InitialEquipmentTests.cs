using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Core.Tests;

public sealed class InitialEquipmentTests
{
    private static EquipmentDefinition Basic(string id, EquipmentSlot slot) =>
        new(id, slot, EquipmentRarity.Ordinary, default, default) { InitialIssue = true };

    [Fact]
    public void PreciousEquipmentCannotBeOrdinarilyManufacturedOrIssued()
    {
        var named = new EquipmentDefinition("named", EquipmentSlot.MainHand, EquipmentRarity.Precious, default, default)
            { ResearchProjectId = "smithing", ProductionCost = 100, ProductionDays = 30 };
        Assert.False(named.IsValid);
        Assert.False((named with { ResearchProjectId = null, ProductionCost = 0, ProductionDays = 0, InitialIssue = true }).IsValid);
    }

    [Fact]
    public void IssuePreservesExistingPossessionsAndDefersNonServingEquipment()
    {
        var rules = new EquipmentRulebook([Basic("sword", EquipmentSlot.MainHand), Basic("cap", EquipmentSlot.Helmet),
            Basic("armor", EquipmentSlot.Armor), new("heirloom", EquipmentSlot.MainHand, EquipmentRarity.Rare, default, default)]);
        var serving = new OfficerState(1, 1, 1, new(50, 50, 50, 50), false);
        var free = new OfficerState(2, null, 1, new(50, 50, 50, 50), false) { Status = OfficerStatus.Free };
        var dead = new OfficerState(3, null, null, new(50, 50, 50, 50), false) { Status = OfficerStatus.Dead };
        var heirloom = new EquipmentInstance(5, "heirloom", 1, null, true);
        var items = InitialEquipment.Issue([heirloom], [dead, free, serving], rules);
        Assert.Equal(6, items.Length);
        Assert.Equal(heirloom, items.Single(i => i.Id == 5));
        Assert.Equal(new[] { "armor", "cap" }, items.Where(i => i.OfficerId == 1 && i.Id != 5).Select(i => i.DefinitionId).Order().ToArray());
        Assert.All(items.Where(i => i.OfficerId == 1), item => Assert.True(item.Equipped));
        Assert.All(items.Where(i => i.OfficerId == 2), item => { Assert.False(item.Equipped); Assert.True(item.EquipOnService); });
        Assert.DoesNotContain(items, item => item.OfficerId == 3);
        Assert.True(EquipmentRules.ValidInventory(items, rules.Items));
    }

    [Fact]
    public void FirstEntryIntoServiceEquipsIssuedItemsOnlyOnce()
    {
        var rules = new EquipmentRulebook([Basic("sword", EquipmentSlot.MainHand)]);
        var person = new OfficerState(1, null, 1, new(50, 50, 50, 50), false) { Status = OfficerStatus.Free };
        var issued = InitialEquipment.Issue([], [person], rules);
        var world = WorldState.Create(new(190, 1, 1), 1, [new ForceState(1, 0)],
            [new SettlementState(1, 1, 0, 100, 100, 0, 0, 0, 100, 0, 100)], [person],
            corps: [new CorpsState(1, 1, null, new(0, 0)) { IsRoot = true }], equipment: issued);
        var mutation = world.BeginMutation(equipment: rules);
        mutation.Replace(person with { OwnerId = 1, Status = OfficerStatus.Serving });
        var equipped = Assert.Single(mutation.AllEquipment.Values);
        Assert.True(equipped.Equipped);
        Assert.Null(equipped.EquipOnService);
        mutation.SetEquipment(equipped with { Equipped = false });
        mutation.Replace(person);
        mutation.Replace(person with { OwnerId = 1, Status = OfficerStatus.Serving });
        Assert.False(Assert.Single(mutation.AllEquipment.Values).Equipped);
    }

    [Fact]
    public void IncompatibleIssuedWeaponIsHeldWithoutDisplacingExistingOffHand()
    {
        var sword = Basic("sword", EquipmentSlot.MainHand) with { CompatibleWeapons = ["shield"] };
        var bow = new EquipmentDefinition("bow", EquipmentSlot.OffHand, EquipmentRarity.Ordinary, default, default);
        var shield = new EquipmentDefinition("shield", EquipmentSlot.OffHand, EquipmentRarity.Ordinary, default, default);
        var rules = new EquipmentRulebook([sword, bow, shield]);
        var person = new OfficerState(1, 1, 1, new(50, 50, 50, 50), false);
        var equippedBow = new EquipmentInstance(10, "bow", 1, null, true);
        var result = InitialEquipment.Issue([equippedBow], [person], rules);
        Assert.Equal(equippedBow, result.Single(item => item.Id == 10));
        Assert.False(result.Single(item => item.DefinitionId == "sword").Equipped);
        Assert.True(EquipmentRules.ValidInventory(result, rules.Items));
    }
}
