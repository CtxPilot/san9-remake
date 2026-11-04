using San9.Application.Presentation;

namespace San9.Client.Godot.Settings;

/// <summary>One client lifetime. Navigation changes memory; the application saves once on normal shutdown.</summary>
public sealed class UiPreferencesSession
{
    private readonly Action<UiPreferences>? _save;
    private UiPreferences _saved;
    public UiPreferences Current { get; private set; }

    public UiPreferencesSession(UiPreferences? initial = null, Action<UiPreferences>? save = null)
    {
        Current = _saved = initial ?? UiPreferences.Default;
        Current.Validate();
        _save = save;
    }

    public bool IsMap(SelectionViewGroup group) => Current.SelectionViews.Get(group) == SelectionViewMode.Map;
    public bool IsMap(InformationCategory category) => IsMap(AdministrativeGroup(category));
    public void SetMap(InformationCategory category, bool map) => SetMap(AdministrativeGroup(category), map);
    public void SetMap(SelectionViewGroup group, bool map) => Update(p => p with
    { SelectionViews = p.SelectionViews.With(group, map ? SelectionViewMode.Map : SelectionViewMode.List) });

    public void Update(Func<UiPreferences, UiPreferences> change)
    {
        var next = change(Current);
        next.Validate();
        Current = next;
    }

    public void Save()
    {
        if (Current == _saved || _save is null) return;
        _save(Current);
        _saved = Current;
    }
    private static SelectionViewGroup AdministrativeGroup(InformationCategory category) => category switch
    {
        InformationCategory.Forces => SelectionViewGroup.Forces,
        InformationCategory.Cities => SelectionViewGroup.Cities,
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
}
