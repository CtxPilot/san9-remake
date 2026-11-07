using Godot;
using San9.Application.Presentation;

namespace San9.Client.Godot;

public partial class StrategyHud
{
    private readonly UnitGroupSelection _unitGroup = new();
    private MapCommandList _unitGroupMenu = null!;
    private MapObjectList _unitGroupList = null!;
    private long _unitGroupRevision = -1;
    private Vector2 _unitGroupAnchor = new(280, 96);
    private bool _unitGroupRightPressed;

    private void BuildUnitGroup()
    {
        _unitGroupMenu = new MapCommandList { Textures = _textures, Texts = Texts, Visible = false,
            Activated = (entry, _) => BeginGroupCommand(entry.Id == "attack" ? UnitCommandKind.Attack : UnitCommandKind.March) };
        AddChild(_unitGroupMenu);
        _unitGroupList = new MapObjectList { Textures = _textures, Visible = false,
            Removed = target => _unitGroup.Toggle(Scenario, target.Id) };
        AddChild(_unitGroupList);
        _unitGroup.Changed += RefreshUnitGroup;
        World.UnitsBoxSelected += SelectUnitBox;
        World.UnitGroupPickRequested += PickUnitGroup;
        var overlay = new UnitSelectionOverlay { World = World, Selection = _unitGroup,
            IsSelectionVisible = () => Turn.IsStrategy && !Overview.IsOpen };
        AddChild(overlay); MoveChild(overlay, 0);
    }
    private void SelectUnitBox(IReadOnlyList<int> ids, bool append, Vector2 releasePosition)
    {
        if (!CanIssueOrders) return;
        if (ids.Count > 0) _unitGroupAnchor = GetGlobalTransformWithCanvas().AffineInverse() * releasePosition;
        _unitGroup.Select(Scenario, ids, append);
        Selection.Clear();
    }
    private void PickUnitGroup(IReadOnlyList<MapObjectTarget> hits, Vector2 point)
    {
        if (!CanIssueOrders) return;
        var candidates = hits.Where(t => t.IsUnit && UnitGroupSelection.CanSelect(Scenario, t.Id)).Distinct().ToArray();
        _unitGroupAnchor = GetGlobalTransformWithCanvas().AffineInverse() * point;
        if (candidates.Length == 1) { _unitGroup.Toggle(Scenario, candidates[0].Id); Selection.Clear(); return; }
        if (candidates.Length > 1) OpenGroupPicker(candidates, point);
    }
    private void OpenGroupPicker(IReadOnlyList<MapObjectTarget> candidates, Vector2 point)
    {
        long revision = Session.Snapshot.Revision;
        var request = new MapObjectPickRequest(candidates, t => t.IsUnit && UnitGroupSelection.CanSelect(Scenario, t.Id),
            () => Session.Snapshot.Revision == revision,
            _unitGroup.UnitIds.Select(id => new MapObjectTarget(id, true)), selected =>
            {
                var outside = _unitGroup.UnitIds.Except(candidates.Select(t => t.Id));
                _unitGroup.Select(Scenario, outside.Concat(selected.Select(t => t.Id)), false);
                Selection.Clear();
            });
        ShowObjectChoices(request, point);
    }
    private void BeginGroupCommand(UnitCommandKind kind)
    {
        if (!CanIssueOrders || !_unitGroup.Commands(Scenario).Contains(UnitCommandTargets.CommandId(kind))) return;
        if (CommandPresentation.Military.Count == 0)
        { _notice.SetNotice("内容包没有提供军事指令表现定义。"); _notice.Show(); return; }
        _flow.OpenUnits(_unitGroup.UnitIds, kind);
    }
    private void RefreshUnitGroup()
    {
        var entries = _unitGroup.Commands(Scenario).Select(id => new MapCommandEntry(id, MapCommandTone.Green, [])).ToArray();
        _unitGroupMenu.SetEntries(entries);
        _unitGroupList.SetChoices(ReadObjectChoices(_unitGroup.UnitIds.Select(id => new MapObjectTarget(id, true))));
    }
    private void UpdateUnitGroup()
    {
        if (_unitGroupRevision != Session.Snapshot.Revision)
        { _unitGroupRevision = Session.Snapshot.Revision; _unitGroup.Refresh(Scenario); RefreshUnitGroup(); }
        bool visible = CanIssueOrders && World.SelectionRectangle is null && !SelectingTargets && Turn.IsStrategy && !Overview.IsOpen &&
            !HasMapPopup && !OverlayHost.HasModal && _unitGroup.UnitIds.Count > 0;
        _unitGroupMenu.Visible = visible && _unitGroupMenu.Items.Count > 0;
        _unitGroupList.Visible = visible;
        if (!visible) return;
        float menuWidth = _unitGroupMenu.Visible ? _unitGroupMenu.Size.X : 0;
        _unitGroupList.Fit(Math.Max(120, Size.X - menuWidth - 8), Math.Min(320, Size.Y - 80));
        var placement = San9.Rendering.UnitGroupMenuLayout.Place(_unitGroupAnchor.X, _unitGroupAnchor.Y,
            _unitGroupMenu.Visible ? _unitGroupMenu.Items.Count : 0, _unitGroupList.Size.X, _unitGroupList.Size.Y,
            Size.X, Size.Y, MapObstacles(includeUnitGroup: false));
        _unitGroupMenu.Position = new((float)placement.Menu.X, (float)placement.Menu.Y);
        _unitGroupList.Position = new((float)placement.List.X, (float)placement.List.Y);
    }
    private bool HandleUnitGroupCancel(InputEvent input)
    {
        if (World.SelectionRectangle is not null || input is not InputEventMouseButton { ButtonIndex: MouseButton.Right } mouse) return false;
        if (!_unitGroupRightPressed && (_unitGroup.UnitIds.Count == 0 || SelectingTargets ||
            !CanIssueOrders || Overview.IsOpen || OverlayHost.HasModal)) return false;
        GetViewport().SetInputAsHandled();
        if (mouse.Pressed) _unitGroupRightPressed = true;
        else if (_unitGroupRightPressed) { _unitGroupRightPressed = false; _unitGroup.Clear(); }
        return true;
    }
}
