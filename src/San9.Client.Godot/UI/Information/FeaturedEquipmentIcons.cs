using Godot;
namespace San9.Client.Godot;

/// <summary>Project-owned atlas; legacy or mod items without a matching cell have no icon.</summary>
internal static class FeaturedEquipmentIcons
{
    private const string Path = "res://assets/equipment/featured-atlas.png";
    private static Dictionary<string, Texture2D>? _icons;
    // Atlas coordinates belong to the artwork, not to the order of content definitions.
    internal static readonly string[] CellIds =
    [
        "featured-xuanwen-ji", "featured-qinggang-jian", "featured-yanling-dao", "featured-cangyun-qiang",
        "featured-chifeng-mao", "featured-huyue-ji", "featured-ningyuan-jian", "featured-qingyu-dao",
        "featured-yueshan-yue", "featured-jingtao-qiang", "featured-shuofeng-ji", "featured-yunwen-jian",
        "featured-huanshou-kui", "featured-yunwen-kui", "featured-xuantie-jia", "featured-qinglin-jia",
        "featured-chijin-jia", "featured-wuzhui", "featured-huacong", "featured-zaoliu",
        "featured-qingzong", "featured-xuecong", "featured-heiliu", "featured-huwen-fu",
        "featured-yunlei-fu", "featured-shanwen-fu", "featured-shuanghu-fu", "featured-yanwei-biao",
        "featured-liuxing-biao", "featured-heiling-zhen", "featured-feishi-suo", "featured-xingjun-ji",
        "featured-shoubei-lue", "featured-junxu-ce", "featured-dili-yao", "featured-jianglue-pian",
        "featured-wenyu-pei", "featured-qingyu-yin", "featured-tongque-zun", "featured-guwen-jing"
    ];

    internal static Texture2D? Find(string definitionId, string name)
    {
        if (definitionId == "featured-liuxing-biao")
        {
            const string dart = "res://assets/equipment/liuxing-biao.png";
            return ResourceLoader.Exists(dart) ? GD.Load<Texture2D>(dart) : null;
        }
        if (_icons is null)
        {
            _icons = new(StringComparer.Ordinal);
            if (ResourceLoader.Exists(Path) && GD.Load<Texture2D>(Path) is { } atlas)
            for (int i = 0; i < CellIds.Length; i++)
            {
                int column = i % 8, row = i / 8;
                int x0 = atlas.GetWidth() * column / 8, x1 = atlas.GetWidth() * (column + 1) / 8;
                int y0 = atlas.GetHeight() * row / 5, y1 = atlas.GetHeight() * (row + 1) / 5;
                _icons.Add(CellIds[i], new AtlasTexture { Atlas = atlas, Region = new(x0 + 1, y0 + 1, x1 - x0 - 2, y1 - y0 - 2) });
            }
        }
        if (_icons.TryGetValue(definitionId, out var icon)) return icon;
        // The name guard prevents a mod that reuses a legacy ID for another object
        // from silently displaying a misleading image.
        string? path = (definitionId, name) switch
        {
            ("legacy-treasure:0", "赤兔马" or "赤兔馬") => "res://assets/equipment/red-hare.png",
            ("legacy-treasure:1", "的卢" or "的盧") => "res://assets/equipment/dilu.png",
            ("legacy-treasure:2", "爪黄飞电" or "爪黃飛電") => "res://assets/equipment/zhaohuang-feidian.png",
            ("legacy-treasure:3", "绝影" or "絕影") => "res://assets/equipment/jueying.png",
            ("legacy-treasure:9", "方天画戟" or "方天畫戟") => "res://assets/equipment/fangtian-ji.png",
            ("legacy-treasure:10", "青龙偃月刀" or "青龍偃月刀") => "res://assets/equipment/green-dragon-blade.png",
            ("legacy-treasure:11", "蛇矛") => "res://assets/equipment/serpent-spear.png",
            _ => null
        };
        return path is not null && ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }
}
