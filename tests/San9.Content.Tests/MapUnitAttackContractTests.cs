using System.Text.Json;
using System.Text.Json.Nodes;
using San9.Content.Maps;
using San9.Content.Packages;

namespace San9.Content.Tests;

public sealed class MapUnitAttackContractTests
{
    private static readonly Dictionary<string, MapAnimationClip> Clips = new() { ["body"] = new("body", ["frame"], 100, []) };
    private static MapUnitBody Body => new("custom", Enumerable.Repeat("body", 8).ToArray(), 5, 8)
    {
        Flags = Enumerable.Range(0, 8).Select(_ => Enumerable.Range(0, 3).Select(i => new MapUnitFlagPlacement(i, -20, i)).ToArray()).ToArray(),
        MeleeAttack = new([new(Enumerable.Repeat("body", 8).ToArray())], MapProjectileKind.Arrow, 7)
    };
    private static MapUnitCatalog Catalog(MapUnitBody body) => new([body], [], [], -1)
    { Projectiles = new() { [MapProjectileKind.Arrow] = new(["arrow"], 64, false, 0, 0) } };

    [Fact]
    public void FormationAppearanceResolvesReferencesAndPreservesTheBaseCatalog()
    {
        var clips = new Dictionary<string, MapAnimationClip>(Clips);
        foreach (int direction in Enumerable.Range(0, 8))
            foreach (string layer in new[] { "pole-back", "pole-front" })
            {
                string id = $"unit-flag-{direction}-{layer}";
                clips.Add(id, new(id, ["frame"], 100, []));
            }
        var escort = new MapUnitMember(Body.Id, 30, 10) { Group = 1 };
        var influence = new MapUnitEffect("body", -7, -9);
        var formation = new MapUnitFormation(900, [new(1, [new(Body.Id, 0, 0), escort], influence),
            new(100, [new(Body.Id, 1, 1), escort], influence)], -50);
        var central = new MapUnitFormation(901, [new(50, [new(Body.Id, 7, 9), new(Body.Id, -7, -9) { Index = 1 }], influence),
            new(1, [new(Body.Id, 2, 3)], influence)], -60);
        var preset = new MapUnitFormationAppearance(900, "alternate", 901);
        var catalog = Catalog(Body) with
        {
            Bodies = [Body, Body with { Id = "alternate" }],
            Formations = [formation, central], FormationAppearance = [preset]
        };
        catalog.Validate(clips);
        var selected = catalog.WithFormationAppearance();
        selected.Validate(clips);
        Assert.Equal(new[] { new MapUnitMember("alternate", 2, 3), escort }, selected.Formations[0].Layouts[0].Members);
        Assert.Equal(new[] { new MapUnitMember("alternate", 7, 9), new("alternate", -7, -9) { Index = 1 }, escort },
            selected.Formations[0].Layouts[1].Members);
        Assert.Equal(formation.StatusY, selected.Formations[0].StatusY);
        Assert.Equal(formation.Layouts.Select(l => l.MinimumSoldiers), selected.Formations[0].Layouts.Select(l => l.MinimumSoldiers));
        Assert.All(selected.Formations[0].Layouts, l => Assert.Same(influence, l.Influence));
        Assert.Same(central, selected.Formations[1]);
        Assert.Same(formation, catalog.Formations[0]);
        Assert.All(formation.Layouts.SelectMany(l => l.Members), m => Assert.Equal(Body.Id, m.BodyId));
        foreach (var bindings in new MapUnitFormationAppearance[][] {
            [preset with { FormationId = 902 }], [preset with { CentralBodyId = "absent" }],
            [preset with { CentralFormationId = 902 }], [preset, preset] })
            Assert.Throws<InvalidDataException>(() => (catalog with { FormationAppearance = bindings }).Validate(clips));
        Assert.Throws<InvalidDataException>(() => (catalog with
        {
            Formations = [formation, central with { Layouts = [new(1, [escort], influence)] }]
        }).Validate(clips));
        var node = JsonSerializer.SerializeToNode(catalog, PackageFiles.JsonOptions)!.AsObject();
        node.Remove("formationAppearance");
        Assert.Throws<JsonException>(() => node.Deserialize<MapUnitCatalog>(PackageFiles.JsonOptions));
    }

    [Fact]
    public void AuthoredContentSupportsMultipleFlagsAndAnIndependentVolleyAndRejectsInvalidRecipes()
    {
        Catalog(Body).Validate(Clips);
        var roundTrip = JsonSerializer.Deserialize<MapUnitBody>(JsonSerializer.Serialize(Body, PackageFiles.JsonOptions), PackageFiles.JsonOptions)!;
        Assert.Equal(3, roundTrip.Flags[7].Length);
        Assert.Equal(7, roundTrip.MeleeAttack!.VolleySize);
        void Reject(MapUnitBody body) => Assert.Throws<InvalidDataException>(() => Catalog(body).Validate(Clips));
        Reject(Body with { Flags = Body.Flags[..7] });
        Reject(Body with { MeleeAttack = Body.MeleeAttack! with { Actions = [] } });
        Reject(Body with { MeleeAttack = Body.MeleeAttack! with { VolleySize = 0 } });
        Reject(Body with { MeleeAttack = Body.MeleeAttack! with { Projectile = MapProjectileKind.None } });
        Reject(Body with { MeleeAttack = Body.MeleeAttack! with { Projectile = MapProjectileKind.Stone } });
        Reject(Body with { MeleeAttack = Body.MeleeAttack! with { Timings = [] } });
        Reject(Body with { MeleeAttack = Body.MeleeAttack! with { Timings = [new(-1, 500)] } });
        Reject(Body with { MeleeAttack = Body.MeleeAttack! with { ShotSpacingMilliseconds = -1 } });
    }

    [Theory]
    [InlineData("timings")]
    [InlineData("shotSpacingMilliseconds")]
    [InlineData("blocksDayProgress")]
    public void TimingAndBlockingAreRequiredContentInsteadOfRuntimeBodyIdRules(string field)
    {
        var node = JsonSerializer.SerializeToNode(Body, PackageFiles.JsonOptions)!;
        var recipe = node["meleeAttack"]!.AsObject();
        if (field == "blocksDayProgress") recipe["actions"]![0]!.AsObject().Remove(field);
        else recipe.Remove(field);
        Assert.Throws<JsonException>(() => node.Deserialize<MapUnitBody>(PackageFiles.JsonOptions));
    }

    [Theory]
    [InlineData("flags")]
    [InlineData("meleeAttack")]
    [InlineData("rangedAttack")]
    [InlineData("retirement")]
    [InlineData("constructionClips")]
    public void MissingVisualDefinitionsAreRejectedInsteadOfReadingTheOldActionContract(string field)
    {
        var node = JsonSerializer.SerializeToNode(Body, PackageFiles.JsonOptions)!.AsObject();
        node.Remove(field);
        Assert.Throws<JsonException>(() => node.Deserialize<MapUnitBody>(PackageFiles.JsonOptions));
    }

    [Fact]
    public void RetirementDefinitionsValidateTheirOwnClipsEffectsAndLifetime()
    {
        var retirement = new MapUnitRetirement(Enumerable.Repeat("body", 8).ToArray(), new("body", -4, -5), 500, 1000, 100);
        var body = Body with { Retirement = retirement };
        Catalog(body).Validate(Clips);
        Assert.Throws<InvalidDataException>(() => Catalog(body with { Retirement = retirement with { DirectionClips = ["body"] } }).Validate(Clips));
        Assert.Throws<InvalidDataException>(() => Catalog(body with { Retirement = retirement with { Effect = new("absent", 0, 0) } }).Validate(Clips));
        Assert.Throws<InvalidDataException>(() => Catalog(body with { Retirement = retirement with { FadeMilliseconds = 0 } }).Validate(Clips));
    }

    [Fact]
    public void ConstructionClipsDeclareBodySupportAndValidateAllDirections()
    {
        Catalog(Body with { ConstructionClips = [] }).Validate(Clips);
        Catalog(Body with { ConstructionClips = Enumerable.Repeat("body", 8).ToArray() }).Validate(Clips);
        Assert.Throws<InvalidDataException>(() => Catalog(Body with { ConstructionClips = ["body"] }).Validate(Clips));
        Assert.Throws<InvalidDataException>(() => Catalog(Body with { ConstructionClips = Enumerable.Repeat("absent", 8).ToArray() }).Validate(Clips));
    }
}
