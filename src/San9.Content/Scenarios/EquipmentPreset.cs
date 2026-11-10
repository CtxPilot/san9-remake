using System.Collections.Immutable;
using San9.Core.Domain;

namespace San9.Content.Scenarios;

/// <summary>Original remake equipment and production choices; independent of imported artwork.</summary>
public static class EquipmentPreset
{
    public const string ResearchId = "equipment-forging";

    public static ResearchProjectDefinition Research() => new(ResearchId, 1000, 30, []) { Name = "装备锻造" };

    public static ImmutableArray<EquipmentDefinition> Basic() =>
    [
        New("basic-spear", "制式长枪", EquipmentSlot.MainHand, new(2, 0, 0, 0), new(1, 0, 0, 0), 200),
        New("basic-sword", "制式短剑", EquipmentSlot.OffHand, new(1, 0, 0, 0), new(1, 0, 0, 0), 180),
        New("basic-bow", "制式弓", EquipmentSlot.OffHand, new(1, 0, 0, 0), new(1, 0, 0, 0), 220),
        New("basic-shield", "制式盾", EquipmentSlot.OffHand, new(0, 0, 0, 1), new(0, 0, 0, 1), 180),
        New("basic-helmet", "制式头盔", EquipmentSlot.Helmet, new(0, 0, 0, 1), new(0, 0, 0, 1), 160),
        New("basic-armor", "制式铠甲", EquipmentSlot.Armor, new(0, 0, 0, 2), new(0, 0, 0, 1), 240),
        New("basic-mount", "战马", EquipmentSlot.Mount, new(1, 0, 0, 0), default, 250)
    ];

    private static EquipmentDefinition New(string id, string name, EquipmentSlot slot, OfficerAbilities bonus,
        OfficerAbilities growth, int cost) => new(id, slot, EquipmentRarity.Ordinary, bonus, growth)
        { Name = name, ResearchProjectId = ResearchId, ProductionCost = cost, ProductionDays = 30 };
}
