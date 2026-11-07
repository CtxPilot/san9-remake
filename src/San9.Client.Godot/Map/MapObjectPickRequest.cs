namespace San9.Client.Godot;

/// <summary>Revision-bound eligible objects, with either immediate single selection or temporary multiple selection.</summary>
public sealed class MapObjectPickRequest
{
    private readonly Func<MapObjectTarget, bool> _allowed;
    private readonly Func<bool> _current;
    private readonly Action<MapObjectTarget>? _selected;
    private bool _closed;
    private readonly Action<IReadOnlyList<MapObjectTarget>>? _confirmed;
    private readonly HashSet<MapObjectTarget> _selection = [];
    public bool Multiple => _confirmed is not null;
    public bool IsSelected(MapObjectTarget target) => _selection.Contains(target);
    public MapObjectPickRequest(IEnumerable<MapObjectTarget> hits, Func<MapObjectTarget, bool> allowed,
        Func<bool> current, IEnumerable<MapObjectTarget> selected, Action<IReadOnlyList<MapObjectTarget>> confirmed)
        : this(hits, allowed, current)
    {
        _confirmed = confirmed;
        _selection.UnionWith(selected.Where(Candidates.Contains));
    }
    public bool Toggle(MapObjectTarget target)
    {
        if (!Multiple || !IsCurrent || !Candidates.Contains(target) || !_allowed(target)) return false;
        if (!_selection.Remove(target)) _selection.Add(target);
        return true;
    }
    public bool Confirm()
    {
        if (!Multiple || !IsCurrent || _selection.Any(t => !_allowed(t))) return false;
        _closed = true;
        _confirmed!(Candidates.Where(_selection.Contains).ToArray());
        return true;
    }
    public IReadOnlyList<MapObjectTarget> Candidates { get; }
    public bool IsCurrent => !_closed && _current();

    public MapObjectPickRequest(IEnumerable<MapObjectTarget> hits, Func<MapObjectTarget, bool> allowed,
        Func<bool> current, Action<MapObjectTarget> selected) : this(hits, allowed, current) => _selected = selected;

    private MapObjectPickRequest(IEnumerable<MapObjectTarget> hits, Func<MapObjectTarget, bool> allowed, Func<bool> current)
    {
        _allowed = allowed; _current = current;
        Candidates = Array.AsReadOnly(hits.Distinct().Where(allowed).ToArray());
    }

    public bool TrySelect(MapObjectTarget target)
    {
        if (Multiple || !IsCurrent || !Candidates.Contains(target) || !_allowed(target)) return false;
        _closed = true;
        _selected!(target);
        return true;
    }

    public void Cancel() => _closed = true;
}
