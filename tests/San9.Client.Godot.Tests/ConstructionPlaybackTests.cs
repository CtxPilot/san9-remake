using San9.Content.Maps;
using San9.Core.Domain;
using San9.Rendering;

namespace San9.Client.Godot.Tests;

public sealed class ConstructionPlaybackTests
{
    private static readonly Dictionary<string, MapAnimationClip> Clips = UnitVisualFixture.Clips
        .Concat(Enumerable.Range(0, 8).Select(d => new KeyValuePair<string, MapAnimationClip>($"work-{d}",
            new($"work-{d}", Enumerable.Range(0, 8).Select(f => $"work-{d}-{f}").ToArray(), 100, []))))
        .ToDictionary();
    private static readonly MapUnitCatalog Catalog = UnitVisualFixture.Catalog with
    {
        Bodies = UnitVisualFixture.Catalog.Bodies.Select(b => b with
            { ConstructionClips = Enumerable.Range(0, 8).Select(d => $"work-{d}").ToArray() }).ToArray(),
        Formations = [new(10000, [new(1, Enumerable.Range(0, 5).Select(g => new MapUnitMember("body", g * 8, 0) { Group = g }).ToArray(),
            new("influence", 0, 0))], -70)]
    };
    private static UnitVisualState Working => UnitVisualFixture.State with { CellX = 0, CellY = 0, Morale = 100, ConstructionTarget = new(12, 2) };

    [Fact]
    public void EveryGroupLoopsConstructionThroughPauseAndDayPublicationWithoutRestarting()
    {
        var state = Working;
        var playback = new UnitMotionPlayback();
        playback.Begin(state, Catalog, 0, 0, false);
        var first = UnitVisuals.Resolve(state, Catalog, Clips, playback);
        Assert.Equal(UnitVisualAction.Construction, first.Action);
        Assert.All(first.Animations.Where(a => a.IsBody), a => { Assert.StartsWith("work-", a.ClipId); Assert.Equal(0, a.Frame); });
        playback.Advance(0, 300, false, _ => state, Catalog, Clips, _ => 0);
        Assert.All(UnitVisuals.Resolve(state, Catalog, Clips, playback).Animations.Where(a => a.IsBody), a => Assert.Equal(3, a.Frame));
        var next = playback.Copy();
        next.Begin(state, Catalog, 0, 300, false);
        next.Advance(0, 1100, false, _ => state, Catalog, Clips, _ => 0);
        Assert.All(UnitVisuals.Resolve(state, Catalog, Clips, next).Animations.Where(a => a.IsBody), a => Assert.Equal(3, a.Frame));
        Assert.Equal(first.Direction, UnitVisuals.Direction(new(2, 2), state.ConstructionTarget));
    }

    [Theory]
    [InlineData(true, false, UnitCondition.Normal, true)]
    [InlineData(false, true, UnitCondition.Normal, true)]
    [InlineData(false, false, UnitCondition.Confused, true)]
    [InlineData(false, false, UnitCondition.Normal, false)]
    public void MovementCombatAbnormalStateAndCompletionLeaveTheConstructionAction(bool moving, bool engaged, UnitCondition condition, bool target)
    {
        var state = Working with { IsMoving = moving, IsEngaged = engaged, Condition = condition,
            ConstructionTarget = target ? Working.ConstructionTarget : null };
        Assert.NotEqual(UnitVisualAction.Construction, UnitVisuals.Resolve(state, Catalog, Clips).Action);
    }

    [Fact]
    public void ABodyWithoutConstructionResourcesKeepsItsExistingBodyClip()
    {
        var visual = UnitVisuals.Resolve(Working, UnitVisualFixture.Catalog, UnitVisualFixture.Clips);
        Assert.All(visual.Animations.Where(a => a.IsBody), a => Assert.StartsWith("body-", a.ClipId));
    }
}
