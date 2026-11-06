using San9.Content.Interface;
using San9.Content.Maps;
using San9.Content.Scenarios;

namespace San9.Content.Tests;

public sealed class MilitaryAppearanceBindingTests
{
    [Fact]
    public void CustomFormationsReuseLayoutsAndOptionalAppearancePresetsWithoutChangingSources()
    {
        var layout = new MapUnitLayout(1, [new("body", 0, 0)], new("influence", 0, 0));
        var catalog = new MapUnitCatalog([], [new(20, [layout], -50)], [], -1)
            { FormationAppearance = [new(20, "ship", 20)] };
        var bound = catalog.BindFormations([new(9000, "自建船型") { MapAppearanceId = 20 }]);
        Assert.Same(layout, bound.Formations.Single(f => f.FormationId == 9000).Layouts[0]);
        var preset = bound.WithFormationAppearance().Formations.Single(f => f.FormationId == 9000);
        Assert.Equal("ship", Assert.Single(preset.Layouts[0].Members).BodyId);
        Assert.Single(catalog.Formations);
        Assert.Equal("body", Assert.Single(catalog.Formations[0].Layouts[0].Members).BodyId);
        Assert.Throws<InvalidDataException>(() => catalog.BindFormations([new(9001, "无效外观") { MapAppearanceId = 404 }]));
    }

    [Fact]
    public void RemappingAnOriginalDoesNotChangeResourceReferencesForOtherFormations()
    {
        var central = new MapUnitLayout(1, [new("ship", 0, 0)], new("influence", 0, 0));
        var escort = central with { Members = [new("escort", 1, 1)] };
        var catalog = new MapUnitCatalog([], [new(20, [central], -50), new(21, [escort], -60)], [], -1)
            { FormationAppearance = [new(21, "alternate", 20)] };
        var bound = catalog.BindFormations([new(20, "原船改用护卫") { MapAppearanceId = 21 },
            new(9000, "护卫副本") { MapAppearanceId = 21 }]);
        var appeared = bound.WithFormationAppearance();
        Assert.Equal(0, Assert.Single(appeared.Formations.Single(f => f.FormationId == 9000).Layouts[0].Members).X);
        Assert.Equal("alternate", Assert.Single(appeared.Formations.Single(f => f.FormationId == 9000).Layouts[0].Members).BodyId);
    }

    [Fact]
    public void TacticAndNavalZoomBindingsUseIndependentPresentationReferences()
    {
        var sprite = new BattleSprite("sprite", 1, 100);
        var land = new BattleZoomBody(sprite, null, null, null);
        var ship = land with { Idle = sprite with { Asset = "ship" } };
        var zoom = new BattleZoomDefinition(land, land, sprite, sprite, "source", "target", [])
            { SourceFormations = new() { [20] = ship }, TargetFormations = new() { [20] = ship } };
        var presentation = new BattlePresentationDefinition([], new() { [5] = new([], null, zoom) }, "hit", "block");
        var information = InformationDefinitions.Empty with
        {
            Tactics = [new(7000, "新兵法") { BattleAppearanceId = 5 }, new(7001, "无表现")],
            Formations = [new(9000, "新船型") { MapAppearanceId = 20 }]
        };
        var bound = presentation.BindCatalog(information);
        Assert.Same(ship, bound.Tactics[7000].Zoom!.SourceFormations[9000]);
        Assert.Same(ship, bound.Tactics[7000].Zoom!.TargetFormations[9000]);
        Assert.DoesNotContain(7001, bound.Tactics.Keys);
        Assert.Single(presentation.Tactics);
        Assert.DoesNotContain(9000, presentation.Tactics[5].Zoom!.SourceFormations.Keys);
        Assert.Throws<InvalidDataException>(() => presentation.BindCatalog(information with
            { Tactics = [new(7002, "未知表现") { BattleAppearanceId = 404 }] }));
    }
}
