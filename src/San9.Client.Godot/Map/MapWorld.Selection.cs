using Godot;
using San9.Rendering;

namespace San9.Client.Godot;

public partial class MapWorld
{
    private readonly MapSelectionGesture _selectionGesture = new();
    private bool _selectionRightCancel;
    public Rect2? SelectionRectangle => _selectionGesture.IsDragging && _selectionGesture.Start is { } start
        ? new Rect2(new((float)start.X, (float)start.Y), new((float)(_selectionGesture.End.X-start.X),
            (float)(_selectionGesture.End.Y-start.Y))).Abs() : null;
    public IReadOnlyList<int>? BoxSelectionPreview => SelectionRectangle is { } box ? UnitsInSelectionBox(box) : null;
    public bool BoxSelectionAppends => _selectionGesture.Append;
    public event Action<IReadOnlyList<int>, bool, Vector2>? UnitsBoxSelected;

    private int[] UnitsInSelectionBox(Rect2 box) => _display?.Presented?.Region.Units.Where(unit =>
    {
        var point = WorldToViewport(new(unit.AnchorX, unit.AnchorY));
        return box.HasPoint(point) && IsInputBlocked?.Invoke(point) != true;
    }).Select(unit => unit.Id).ToArray() ?? [];
    public event Action<IReadOnlyList<MapObjectTarget>, Vector2>? UnitGroupPickRequested;

    private bool HandleSelectionGesture(InputEvent input)
    {
        if (_selectionRightCancel && input is InputEventMouseButton { ButtonIndex: MouseButton.Right } right)
        {
            _selectionRightCancel = right.Pressed;
            GetViewport().SetInputAsHandled(); return true;
        }
        if (_selectionGesture.Start is null) return false;
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } ||
            input is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            _selectionRightCancel = input is InputEventMouseButton;
            _selectionGesture.Cancel(); GetViewport().SetInputAsHandled(); return true;
        }
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Middle })
        { _selectionGesture.Cancel(); return false; }
        if (input is InputEventMouseMotion motion)
        {
            _selectionGesture.Move(new(motion.Position.X, motion.Position.Y));
            if (_selectionGesture.IsDragging) GetViewport().SetInputAsHandled();
            return true;
        }
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release) return false;
        _selectionGesture.Move(new(release.Position.X, release.Position.Y));
        bool append = _selectionGesture.Append;
        var rectangle = SelectionRectangle;
        _selectionGesture.Cancel();
        GetViewport().SetInputAsHandled();
        if (!NavigationAvailable || TargetPicked is not null || CanIssueOrders?.Invoke() == false ||
            !CanPickAt(release.Position) || IsInputBlocked?.Invoke(release.Position) == true) return true;
        if (rectangle is { } box)
        {
            var units = UnitsInSelectionBox(box);
            UnitsBoxSelected?.Invoke(units, append, release.Position);
        }
        else if (append) UnitGroupPickRequested?.Invoke(ObjectsAtViewport(release.Position), release.Position);
        else
        {
            var point = release.Position;
            UnitsBoxSelected?.Invoke([], false, release.Position);
            PickObject(point, ObjectsAtViewport(point), CommandSourceAllowed,
                () => TargetPicked is null, target => CommandMenuRequested?.Invoke(target, point));
        }
        return true;
    }
}
