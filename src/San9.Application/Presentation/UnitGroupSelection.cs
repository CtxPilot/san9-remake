using System.Collections.Immutable;

namespace San9.Application.Presentation;

/// <summary>Local command sources, independent of focus, hover and the world state.</summary>
public sealed class UnitGroupSelection
{
    public ImmutableSortedSet<int> UnitIds { get; private set; } = [];
    public event Action? Changed;
    private StrategySnapshot? _menuView;
    private readonly Dictionary<int, IReadOnlyList<MapCommandEntry>> _menus = [];
    private ImmutableSortedSet<int>? _commandSelection;
    private IReadOnlyList<string> _commands = [];

    private IReadOnlyList<MapCommandEntry> Menu(StrategySnapshot view, int id)
    {
        if (!ReferenceEquals(_menuView, view))
        { _menuView = view; _menus.Clear(); _commandSelection = null; }
        if (!_menus.TryGetValue(id, out var entries)) _menus.Add(id, entries = MapCommandMenu.Unit(view, id));
        return entries;
    }
    private bool Eligible(StrategySnapshot view, int id) => Menu(view, id).Any(entry => entry.Enabled);
    public static bool CanSelect(StrategySnapshot view, int id) =>
        MapCommandMenu.Unit(view, id).Any(e => e.Enabled);
    public void Toggle(StrategySnapshot view, int id)
    {
        if (!Eligible(view, id)) return;
        Set(UnitIds.Contains(id) ? UnitIds.Remove(id) : UnitIds.Add(id));
    }
    public ImmutableSortedSet<int> Preview(StrategySnapshot view, IEnumerable<int> ids, bool append) =>
        (append ? UnitIds : ImmutableSortedSet<int>.Empty).Union(ids.Where(id => Eligible(view, id)));
    public void Select(StrategySnapshot view, IEnumerable<int> ids, bool append) => Set(Preview(view, ids, append));
    public void Refresh(StrategySnapshot view) => Set(UnitIds.Where(id => Eligible(view, id)).ToImmutableSortedSet());
    public void Clear() => Set([]);
    public IReadOnlyList<string> Commands(StrategySnapshot view)
    {
        // Menus are snapshot-bound. Dragging and HUD repainting do not rebuild world-wide availability queries.
        if (ReferenceEquals(_menuView, view) && ReferenceEquals(_commandSelection, UnitIds)) return _commands;
        _commands = UnitIds.Count == 0 ? [] : new[] { "attack", "march" }.Where(command => UnitIds.All(id =>
            Menu(view, id).Any(entry => entry.Id == command && entry.Enabled))).ToArray();
        _commandSelection = UnitIds;
        return _commands;
    }
    private void Set(ImmutableSortedSet<int> ids)
    {
        if (UnitIds.SetEquals(ids)) return;
        UnitIds = ids; Changed?.Invoke();
    }
}
