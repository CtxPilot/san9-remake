namespace San9.Application.Authoring;

/// <summary>Assigns descending integer priorities while preserving existing gaps whenever possible.</summary>
internal static class CatalogPriorityOrder
{
    internal readonly record struct Entry(int Id, int Priority);
    private static IOrderedEnumerable<Entry> Ordered(IEnumerable<Entry> entries) => entries.OrderByDescending(e => e.Priority).ThenBy(e => e.Id);
    private static Entry[] Number(IReadOnlyList<Entry> entries) => entries.Select((e, i) => e with { Priority = entries.Count - 1 - i }).ToArray();

    internal static Entry[] Move(IEnumerable<Entry> entries, int id, int target)
    {
        var ordered = Ordered(entries).ToList();
        int index = ordered.FindIndex(e => e.Id == id);
        if (index < 0) throw new InvalidDataException("找不到排序项。");
        if ((uint)target >= (uint)ordered.Count) throw new ArgumentOutOfRangeException(nameof(target));
        int[] priorities = ordered.Select(e => e.Priority).ToArray();
        var moving = ordered[index]; ordered.RemoveAt(index); ordered.Insert(target, moving);
        var assigned = ordered.Select((e, i) => e with { Priority = priorities[i] }).ToArray();
        return Ordered(assigned).Select(e => e.Id).SequenceEqual(ordered.Select(e => e.Id)) ? assigned : Number(ordered);
    }

    internal static Entry[] Insert(IEnumerable<Entry> entries, int id, int position)
    {
        var ordered = Ordered(entries).ToList();
        if ((uint)position > (uint)ordered.Count) throw new ArgumentOutOfRangeException(nameof(position));
        ordered.Insert(position, new(id, 0));
        long above = position == 0 ? (long)int.MaxValue + 1 : ordered[position - 1].Priority;
        long below = position + 1 == ordered.Count ? -1 : ordered[position + 1].Priority;
        long value = position == 0 ? below + 1 : above - 1;
        if (above - below <= 1 || value > int.MaxValue) return Number(ordered);
        ordered[position] = new(id, (int)value);
        return ordered.ToArray();
    }
}
