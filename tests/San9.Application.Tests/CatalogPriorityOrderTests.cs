using San9.Application.Authoring;
using Entry = San9.Application.Authoring.CatalogPriorityOrder.Entry;

namespace San9.Application.Tests;

public sealed class CatalogPriorityOrderTests
{
    [Theory]
    [InlineData(new[] { 100, 50, 10 }, 0, 2, new[] { 1, 2, 0 }, new[] { 100, 50, 10 })]
    [InlineData(new[] { 100, 50, 10 }, 1, 0, new[] { 1, 0, 2 }, new[] { 100, 50, 10 })]
    [InlineData(new[] { 100, 50, 10 }, 1, 1, new[] { 0, 1, 2 }, new[] { 100, 50, 10 })]
    [InlineData(new[] { int.MaxValue, int.MaxValue }, 1, 0, new[] { 1, 0 }, new[] { 1, 0 })]
    public void MovingPreservesExistingPriorityValuesUnlessTiesPreventTheRequestedOrder(
        int[] priorities, int id, int target, int[] expectedIds, int[] expectedPriorities)
    {
        var moved = CatalogPriorityOrder.Move(priorities.Select((p, i) => new Entry(i, p)), id, target);
        Assert.Equal(expectedIds, moved.Select(e => e.Id));
        Assert.Equal(expectedPriorities, moved.Select(e => e.Priority));
    }

    [Theory]
    [InlineData(new[] { 100, 50 }, 1, new[] { 0, 2, 1 }, new[] { 100, 99, 50 })]
    [InlineData(new[] { 100, 99 }, 1, new[] { 0, 2, 1 }, new[] { 2, 1, 0 })]
    [InlineData(new[] { 2, 1, 0 }, 3, new[] { 0, 1, 2, 3 }, new[] { 3, 2, 1, 0 })]
    [InlineData(new[] { 80, 40, 0 }, 2, new[] { 0, 1, 3, 2 }, new[] { 80, 40, 39, 0 })]
    [InlineData(new[] { int.MaxValue }, 0, new[] { 1, 0 }, new[] { 1, 0 })]
    [InlineData(new int[] { }, 0, new[] { 0 }, new[] { 0 })]
    public void InsertionUsesAGapOrRenumbersWithoutOverflow(int[] priorities, int position, int[] expectedIds, int[] expectedPriorities)
    {
        var inserted = CatalogPriorityOrder.Insert(priorities.Select((p, i) => new Entry(i, p)), priorities.Length, position);
        Assert.Equal(expectedIds, inserted.Select(e => e.Id));
        Assert.Equal(expectedPriorities, inserted.Select(e => e.Priority));
    }

    [Fact]
    public void InvalidPositionsCannotProduceAnOrder()
    {
        Entry[] entries = [new(0, 10), new(1, 5)];
        Assert.Throws<ArgumentOutOfRangeException>(() => CatalogPriorityOrder.Move(entries, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CatalogPriorityOrder.Insert(entries, 2, 3));
        Assert.Throws<InvalidDataException>(() => CatalogPriorityOrder.Move(entries, 2, 0));
    }
}
