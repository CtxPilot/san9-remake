using Godot;
using San9.Content.Authoring;

namespace San9.Client.Godot;

public partial class CreatedOfficerEditor
{
    private CenterContainer? _flagImage;
    private int? _shownFlagGlyph;
    private void FinishSurnameEdit(Guid? id = null)
    {
        if (_workspace is null) return;
        bool changed = id is Guid person ? Workspace.FinishSurnameEdit(person, Preferences.Current.AutoAssignOfficerFlag)
            : Workspace.FinishSurnameEdits(Preferences.Current.AutoAssignOfficerFlag);
        if (changed) Changed();
    }

    private void RefreshFlagValue()
    {
        if (_flagImage is null || !IsInstanceValid(_flagImage) || Current is not { } person ||
            _shownFlagGlyph == person.FlagGlyph) return;
        _shownFlagGlyph = person.FlagGlyph;
        OfficerFlagField.Refresh(_flagImage, _portraitService, Workspace.FlagCatalog, person.FlagGlyph);
    }

    private void BuildFlags(Node parent, CreatedOfficer person)
    {
        _flagImage = OfficerFlagField.Add(parent, _portraitService, Workspace.FlagCatalog, person.FlagGlyph, ChooseFlag, () =>
        { FinishSurnameEdit(); Workspace.AssignFlagFromSurname(person.Id); Changed(); });
        _shownFlagGlyph = person.FlagGlyph;
    }

    private void ChooseFlag()
    {
        FinishSurnameEdit();
        if (Workspace.FlagCatalog is not { } flags || _portraitService is not { } service || Current is not { } person) return;
        OfficerFlagField.Show(this, service, flags, person.FlagGlyph,
            glyph => Change(person.Id, p => p with { FlagGlyph = glyph }, rebuild: true));
    }
}
