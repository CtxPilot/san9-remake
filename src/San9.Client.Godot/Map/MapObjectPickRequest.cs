namespace San9.Client.Godot;

/// <summary>A single click's eligible objects, bound to the world and input owner that produced it.</summary>
public sealed class MapObjectPickRequest
{
    private readonly Func<MapObjectTarget, bool> _allowed;
    private readonly Func<bool> _current;
    private readonly Action<MapObjectTarget> _selected;
    private bool _closed;
    public IReadOnlyList<MapObjectTarget> Candidates { get; }
    public bool IsCurrent => !_closed && _current();

    public MapObjectPickRequest(IEnumerable<MapObjectTarget> hits, Func<MapObjectTarget, bool> allowed,
        Func<bool> current, Action<MapObjectTarget> selected)
    {
        _allowed = allowed; _current = current; _selected = selected;
        Candidates = Array.AsReadOnly(hits.Distinct().Where(allowed).ToArray());
    }

    public bool TrySelect(MapObjectTarget target)
    {
        if (!IsCurrent || !Candidates.Contains(target) || !_allowed(target)) return false;
        _closed = true;
        _selected(target);
        return true;
    }

    public void Cancel() => _closed = true;
}
