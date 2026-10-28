using San9.Content.Interface;
using San9.Importer.San9PkTc101.Interface;

namespace San9.Importer.San9PkTc101.Tests;

public sealed class BattlePresentationCompilerTests
{
    [Fact]
    public void FacilityBackdropsUseCatalogReferencesIncludingEveryGateAndWork()
    {
        var scene = BattleSceneCompiler.Compile((image, _) => image.ToString());
        Assert.Equal(Enumerable.Range(0, 10), scene.Facilities.Keys.Order());
        foreach (int type in new[] { 0, 2, 3, 4, 5, 9 })
        {
            Assert.Equal(1, BattleFacilityTypes.Groups[type]);
            Assert.Equal("50", Assert.Single(scene.Facilities[type].Target).Asset);
            Assert.Equal("51", Assert.Single(scene.Facilities[type].Source).Asset);
            Assert.True(scene.Facilities[type].ReplacesTerrain);
        }
        foreach (int type in new[] { 1, 6 })
        {
            Assert.Equal(0, BattleFacilityTypes.Groups[type]);
            Assert.Equal("54", Assert.Single(scene.Facilities[type].Target).Asset);
            Assert.False(scene.Facilities[type].ReplacesTerrain);
        }
        foreach (int type in new[] { 7, 8 })
        {
            Assert.Equal(2, BattleFacilityTypes.Groups[type]);
            Assert.Equal(new[] { "52", "56", "56" }, scene.Facilities[type].Target.Select(l => l.Asset));
            Assert.False(scene.Facilities[type].Target[0].Foreground);
            Assert.All(scene.Facilities[type].Target.Skip(1).Concat(scene.Facilities[type].Source.Skip(1)), layer => Assert.True(layer.Foreground));
        }
    }

    [Theory]
    [InlineData(23, 9, 6794, 10781, 10799, 10808, 10790, null)]
    [InlineData(25, 12, 6844, 10867, 10885, 10892, 10876, null)]
    [InlineData(26, 4, 6943, 11037, 11053, 11062, 11046, 17)]
    [InlineData(27, 11, 6816, 10817, 10839, 10848, 10830, null)]
    [InlineData(30, 5, 6965, 11071, 11087, 11094, 11080, 18)]
    [InlineData(35, 2, 6868, 10901, 10919, 10928, 10910, null)]
    [InlineData(36, 0, 6893, 10937, 10955, 10964, 10946, null)]
    [InlineData(37, 1, 6913, 10971, 10989, 10998, 10980, null)]
    [InlineData(38, 3, 6928, 11005, 11023, 11032, 11014, null)]
    public void IntelligencePresentationKeepsMessagesAndEffectsWithTheSourceTactic(
        int tacticId, int nativeType, int announcement, int action, int hit, int reduced, int failed, int? dynamicType)
    {
        var tactics = BattlePresentationCompiler.CompileIntelligenceTactics(
            type => new BattleSprite($"native/{type}", 8, 100),
            type => Enumerable.Range(0, 4).Select(frame => new BattleSprite($"dynamic/{type}/{frame}", 1, 100)).ToArray());
        var tactic = tactics[tacticId];
        Assert.Equal($"native/{nativeType}", tactic.MapEffect!.Asset);
        foreach (var (role, key) in new[] { ("announcement", announcement), ("action", action),
            ("hit", hit), ("reduced", reduced), ("failed", failed) })
            Assert.Equal($"messages.{key}", tactic.Messages[role].Key);
        if (dynamicType is int screen)
        {
            Assert.Equal(11, tactic.Prelude.Length);
            Assert.Equal($"dynamic/{screen}/0", tactic.Prelude[0].Asset);
            Assert.Equal(2875, tactic.Prelude.Sum(s => s.FrameMilliseconds));
            Assert.All(tactic.Prelude.Skip(4), s => Assert.Equal($"dynamic/{screen}/3", s.Asset));
            Assert.True(tactic.Prelude[4].Opacity < tactic.Prelude[3].Opacity);
            Assert.True(tactic.Prelude[^1].Opacity < tactic.Prelude[4].Opacity);
        }
        else Assert.Empty(tactic.Prelude);
        Assert.Equal(tacticId == 25 ? "messages.10736" : "messages.10729", tactic.Messages["defensive"].Key);
        Assert.Equal("target-force", tactics[25].Messages["announcement"].Bindings["force.0"]);
        Assert.Equal("messages.10026", tactic.Messages["failed-report"].Key);
        Assert.Equal(tacticId != 23, tactic.Messages.ContainsKey("reduced-report"));
        if (tacticId != 23) Assert.Equal("messages.10063", tactic.Messages["reduced-report"].Key);
        Assert.Equal(tacticId is 26 or 30 or >= 35, tactic.Messages.ContainsKey("hit-report"));
        if (tacticId is 25 or 26 or >= 35)
        {
            Assert.Equal(tacticId == 25 ? "messages.10101" : "messages.10100", tactic.Messages["secondary-countered"].Key);
            Assert.Equal(tacticId == 25 ? "messages.10736" : "messages.10729", tactic.Messages["secondary-defensive"].Key);
        }
    }
}
