using System.Collections.Immutable;
using San9.Core.Domain;

namespace San9.Content.Scenarios;

public static class MixedTroopPreset
{
    public static MixedTroopCombatProfile Combat() => new(20, 15, 15, 10);

    public static ImmutableArray<SiegeEquipmentDefinition> SiegeEquipment(int tower, int ram, int stone, int elephant) =>
    [
        new(0, "井阑", "siege-tower", tower, 300),
        new(1, "冲车", "siege-ram", ram, 300),
        new(2, "发石", "siege-stone", stone, 500),
        new(3, "象兵", "siege-elephant", elephant, 500)
    ];

    public static ImmutableArray<NavalTrainingDefinition> NavalTraining(int arrowRain, int arrowStorm, int assault, int attack) =>
    [
        new(0, "水军一级", null, 0, [arrowRain, assault]),
        new(1, "水军二级", "naval-training", 500, [arrowRain, arrowStorm, assault, attack])
    ];

    public static string FormationDescription(int index) => index switch
    {
        0 => "枪盾列前掩护后方，可应对骑兵与远程攻击的守备用阵形。",
        1 => "枪兵列前掩护弓弩，利于弓弩兵法的阵形。",
        2 => "利于步兵系兵法，兼顾进攻与防守的阵形。",
        3 => "利于骑兵和弓骑兵法，机动力较高的阵形。",
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public static ImmutableArray<FormationPosition> FormationPositions(int index) => index switch
    {
        0 => [new(0, BattleRank.Front), new(2, BattleRank.Front), new(1, BattleRank.Middle),
            new(0, BattleRank.Rear), new(2, BattleRank.Rear)],
        1 => [new(1, BattleRank.Front), new(0, BattleRank.Middle), new(2, BattleRank.Middle),
            new(0, BattleRank.Rear), new(2, BattleRank.Rear)],
        2 => [new(0, BattleRank.Front), new(2, BattleRank.Front), new(1, BattleRank.Middle),
            new(0, BattleRank.Middle), new(2, BattleRank.Middle)],
        3 => [new(1, BattleRank.Front), new(0, BattleRank.Middle), new(2, BattleRank.Middle),
            new(1, BattleRank.Middle), new(1, BattleRank.Rear)],
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public static ImmutableArray<TroopTypeDefinition> TroopTypes(ImmutableArray<int> infantry, ImmutableArray<int> cavalry, ImmutableArray<int> mounted, ImmutableArray<int> archery) =>
    [
        new(0, "枪兵", 2, new Dictionary<int, int> { [infantry[0]] = 1, [infantry[1]] = 2, [infantry[2]] = 2 }.ToImmutableDictionary())
            { SupportsSpearDefense = true, EquipmentCostPerThousand = Costs(20, 40), LevelResearchProjects = new Dictionary<int, string> { [2] = "spear-2" }.ToImmutableDictionary() },
        new(1, "盾兵", 2, new Dictionary<int, int> { [infantry[0]] = 1, [infantry[1]] = 2, [infantry[2]] = 2 }.ToImmutableDictionary())
            { SupportsShieldCover = true, EquipmentCostPerThousand = Costs(20, 40), LevelResearchProjects = new Dictionary<int, string> { [2] = "shield-2" }.ToImmutableDictionary() },
        new(2, "弓兵", 2, new Dictionary<int, int> { [archery[0]] = 1, [archery[1]] = 2 }.ToImmutableDictionary())
            { IsRangedInfantry = true, IsRangedAttack = true, EquipmentCostPerThousand = Costs(25, 45), LevelResearchProjects = new Dictionary<int, string> { [2] = "bow-2" }.ToImmutableDictionary() },
        new(3, "弩兵", 2, new Dictionary<int, int> { [archery[0]] = 1, [archery[1]] = 1, [archery[2]] = 2 }.ToImmutableDictionary())
            { IsRangedInfantry = true, IsRangedAttack = true, EquipmentCostPerThousand = Costs(60, 100), LevelResearchProjects = new Dictionary<int, string> { [1] = "crossbow-1", [2] = "crossbow-2" }.ToImmutableDictionary() },
        new(4, "骑兵", 2, new Dictionary<int, int> { [cavalry[0]] = 1, [cavalry[1]] = 2, [cavalry[2]] = 2 }.ToImmutableDictionary())
            { IsMountedMelee = true, EquipmentCostPerThousand = Costs(60, 100), LevelResearchProjects = new Dictionary<int, string> { [2] = "cavalry-2" }.ToImmutableDictionary() },
        new(5, "弓骑", 2, new Dictionary<int, int> { [mounted[0]] = 1, [mounted[1]] = 2, [mounted[2]] = 2 }.ToImmutableDictionary())
            { IsRangedAttack = true, EquipmentCostPerThousand = Costs(80, 130), LevelResearchProjects = new Dictionary<int, string> { [1] = "mounted-archer-1", [2] = "mounted-archer-2" }.ToImmutableDictionary() }
    ];

    private static ImmutableDictionary<int, int> Costs(int basic, int advanced) =>
        new Dictionary<int, int> { [1] = basic, [2] = advanced }.ToImmutableDictionary();

    public static ImmutableArray<ResearchProjectDefinition> TroopResearchProjects() =>
    new ResearchProjectDefinition[]
    {
        new("spear-2", 1000, 30, []), new("shield-2", 1000, 30, []),
        new("bow-2", 1000, 30, []), new("crossbow-1", 1500, 30, ["bow-2"]),
        new("crossbow-2", 2000, 60, ["crossbow-1"]), new("cavalry-2", 1500, 30, []),
        new("mounted-archer-1", 2000, 30, ["cavalry-2"]),
        new("mounted-archer-2", 2500, 60, ["mounted-archer-1"]),
        new("formation-yanyue", 1500, 30, []), new("formation-henge", 1500, 30, []),
        new("formation-gouxing", 1500, 30, []), new("formation-chexuan", 1500, 30, []),
        new("naval-training", 1000, 30, []),
        new("naval-mengchong", 1500, 30, ["naval-training"]),
        new("naval-louchuan", 2000, 30, ["naval-mengchong"]),
        new("naval-doujian", 2500, 60, ["naval-louchuan"]),
        new("siege-tower", 1500, 30, []), new("siege-ram", 1500, 30, []),
        new("siege-stone", 2000, 60, []), new("siege-elephant", 2500, 60, [])
    }.Select(project => project with { Name = project.Id switch
    {
        "spear-2" => "枪兵二级", "shield-2" => "盾兵二级", "bow-2" => "弓兵二级",
        "crossbow-1" => "弩兵一级", "crossbow-2" => "弩兵二级", "cavalry-2" => "骑兵二级",
        "mounted-archer-1" => "弓骑一级", "mounted-archer-2" => "弓骑二级",
        "formation-yanyue" => "偃月", "formation-henge" => "衡轭", "formation-gouxing" => "钩行", "formation-chexuan" => "车悬",
        "naval-training" => "水军训练", "naval-mengchong" => "艨艟", "naval-louchuan" => "楼船", "naval-doujian" => "斗舰",
        "siege-tower" => "井阑", "siege-ram" => "冲车", "siege-stone" => "发石", "siege-elephant" => "象兵",
        _ => project.Id
    } }).ToImmutableArray();

}
