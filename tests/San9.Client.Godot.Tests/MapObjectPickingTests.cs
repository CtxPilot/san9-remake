namespace San9.Client.Godot.Tests;

public sealed class MapObjectPickingTests
{
    [Fact]
    public void OverlappingBodiesAndLabelsKeepAllEligibleObjectsAndSeparateUnitAndBuildingIdentity()
    {
        var unit = new MapObjectTarget(1, true);
        var building = new MapObjectTarget(1, false);
        var enemy = new MapObjectTarget(2, true);
        MapObjectTarget? picked = null;
        var hits = new[] { enemy, unit, building, unit, building }
            .Concat(Enumerable.Range(3, 12).Select(id => new MapObjectTarget(id, true)));
        var request = new MapObjectPickRequest(hits, target => target != enemy, () => true, target => picked = target);

        Assert.Equal(14, request.Candidates.Count);
        Assert.Equal(new[] { unit, building }, request.Candidates.Take(2));
        Assert.False(request.TrySelect(enemy));
        Assert.Null(picked);
        Assert.True(request.TrySelect(request.Candidates.Last()));
        Assert.Equal(new MapObjectTarget(14, true), picked);
        Assert.False(request.TrySelect(unit));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplacingWorldOrInputOwnerInvalidatesPendingClick(bool replaceWorld)
    {
        int revision = 1, owner = 1;
        int issued = 0;
        var target = new MapObjectTarget(1, true);
        var request = new MapObjectPickRequest([target], _ => true, () => revision == 1 && owner == 1, _ => issued++);
        if (replaceWorld) revision++; else owner++;
        Assert.False(request.IsCurrent);
        Assert.False(request.TrySelect(target));
        Assert.Equal(0, issued);
    }

    [Fact]
    public void DismissalAndLostEligibilityCannotSubmitACommand()
    {
        var target = new MapObjectTarget(1, false);
        bool eligible = true;
        int issued = 0;
        var request = new MapObjectPickRequest([target], _ => eligible, () => true, _ => issued++);
        eligible = false;
        Assert.False(request.TrySelect(target));
        eligible = true;
        request.Cancel();
        Assert.False(request.TrySelect(target));
        Assert.Equal(0, issued);
    }
}
