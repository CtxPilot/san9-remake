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
    [Fact]
    public void MultipleSelectionIsTemporaryUntilConfirmedAndKeepsCandidateOrder()
    {
        MapObjectTarget a = new(1, true), b = new(2, true), c = new(3, true);
        IReadOnlyList<MapObjectTarget>? selected = null;
        var request = new MapObjectPickRequest([a, b, c], _ => true, () => true, [a], ids => selected = ids);
        Assert.True(request.Toggle(c)); Assert.True(request.Toggle(a)); Assert.True(request.Toggle(b));
        Assert.Null(selected); Assert.False(request.TrySelect(a));
        Assert.True(request.Confirm()); Assert.Equal(new[] { b, c }, selected);
        Assert.False(request.Confirm()); Assert.False(request.Toggle(a));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CancellationOrWorldChangeDiscardsTemporaryMultiSelection(bool cancel)
    {
        MapObjectTarget a = new(1, true); bool current = true, committed = false;
        var request = new MapObjectPickRequest([a], _ => true, () => current, [], _ => committed = true);
        request.Toggle(a);
        if (cancel) request.Cancel(); else current = false;
        Assert.False(request.Confirm()); Assert.False(committed);
    }
}
