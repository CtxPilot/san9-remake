using Godot;
using San9.Core.Domain;

namespace San9.Client.Godot;

/// <summary>Draws the authored positions directly; no original formation artwork is required.</summary>
public partial class FormationEditorPreview : Control
{
    private static readonly string[] RankLabels = ["前锋", "中锋", "后卫"];
    private readonly StyleBoxFlat _background = ContentEditorStyle.Surface("151c24", "394758", 8);
    private FormationRule? _rule;
    private int _dragged = -1;
    private FormationPosition _destination;
    public bool Editable { get; init; }
    public Action<int, FormationPosition>? PositionMoved { get; init; }
    public FormationRule? Rule
    {
        get => _rule;
        set
        {
            if (_rule?.Id != value?.Id) CancelDrag();
            _rule = value; QueueRedraw();
        }
    }
    private long Columns => Editable ? 3 : Math.Max(3, _rule?.Positions.Select(p => (long)p.Column + 1).DefaultIfEmpty(3).Max() ?? 3);
    private bool WithinEditingBounds => _rule is { } rule && rule.Positions.All(p => p.Column is >= 0 and < 3);
    private Rect2 Board => new(Editable ? new Vector2(54, 14) : new Vector2(10, 8),
        new Vector2(Math.Max(1, Size.X - (Editable ? 68 : 20)), Math.Max(1, Size.Y - (Editable ? 28 : 16))));
    private Vector2 Center(FormationPosition position) => Board.Position + new Vector2(
        (float)((position.Column + .5) * Board.Size.X / Columns), (position.Row + .5f) * Board.Size.Y / 3);
    private FormationPosition Target(Vector2 point) => new(
        (int)Math.Clamp(Math.Floor((point.X - Board.Position.X) / Board.Size.X * 3), 0, 2),
        (BattleRank)Math.Clamp((int)((point.Y - Board.Position.Y) / Board.Size.Y * 3), 0, 2));
    private float Radius => Math.Min(Editable ? 20 : 10, Math.Min(Board.Size.X / Columns, Board.Size.Y / 3) * .36f);

    public override void _Ready()
    {
        Resized += Redraw;
        VisibilityChanged += CancelDrag;
        GetWindow().FocusExited += CancelDrag;
        if (Editable) MouseDefaultCursorShape = CursorShape.Drag;
    }
    private void Redraw() => QueueRedraw();
    private void CancelDrag() { _dragged = -1; QueueRedraw(); }
    public override void _ExitTree() { Resized -= Redraw; VisibilityChanged -= CancelDrag; GetWindow().FocusExited -= CancelDrag; }
    public override void _Input(InputEvent input)
    {
        if (_dragged < 0) return;
        if (input is InputEventKey { Pressed: true, Keycode: Key.Escape } or
            InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        { CancelDrag(); GetViewport().SetInputAsHandled(); }
    }
    public override void _GuiInput(InputEvent input)
    {
        if (!Editable || !WithinEditingBounds || !CanProcess() || _rule is null) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } click)
        {
            if (click.Pressed)
            {
                for (int i = 0; i < _rule.Positions.Length; i++)
                    if (Center(_rule.Positions[i]).DistanceTo(click.Position) <= Radius + 6)
                    { _dragged = i; _destination = _rule.Positions[i]; AcceptEvent(); break; }
            }
            else if (_dragged >= 0)
            {
                int index = _dragged;
                bool inside = Board.HasPoint(click.Position);
                var destination = Target(click.Position);
                CancelDrag(); AcceptEvent();
                if (inside && destination != _rule.Positions[index]) PositionMoved?.Invoke(index, destination);
            }
            QueueRedraw();
        }
        else if (input is InputEventMouseMotion motion && _dragged >= 0)
        { _destination = Target(motion.Position); AcceptEvent(); QueueRedraw(); }
    }
    public override void _Draw()
    {
        DrawStyleBox(_background, new(Vector2.Zero, Size));
        if (_rule is not { } rule) return;
        if (Editable && !WithinEditingBounds)
        {
            DrawString(GetThemeDefaultFont(), new(16, 36), "此阵形超出 3×3 范围，暂不支持拖动编辑。", fontSize: 16, modulate: ContentEditorStyle.Muted);
            return;
        }
        var board = Board; long columns = Columns; float width = board.Size.X / columns, height = board.Size.Y / 3;
        long step = Math.Max(1, (long)Math.Ceiling(columns / Math.Max(1, board.Size.X / 12)));
        for (long x = 0; x <= columns; x += step) DrawLine(board.Position + new Vector2(x * width, 0), board.Position + new Vector2(x * width, board.Size.Y), new("344250"));
        for (int y = 0; y <= 3; y++) DrawLine(board.Position + new Vector2(0, y * height), board.Position + new Vector2(board.Size.X, y * height), new("344250"));
        if (Editable)
            for (int y = 0; y < 3; y++) DrawString(GetThemeDefaultFont(), new(12, board.Position.Y + (y + .5f) * height + 5), RankLabels[y], fontSize: 14, modulate: ContentEditorStyle.Muted);
        if (_dragged >= 0) DrawRect(new(Center(_destination) - new Vector2(width, height) / 2, new(width, height)), new Color("465c74"));
        for (int i = 0; i < rule.Positions.Length; i++)
            if (i != _dragged) DrawPosition(i, rule.Positions[i]);
        if (_dragged >= 0) DrawPosition(_dragged, _destination);
    }
    private void DrawPosition(int index, FormationPosition position)
    {
        if (!position.IsValid) return;
        var point = Center(position);
        DrawCircle(point, Radius, new(index == 0 ? "f0d3a3" : "85b5a9"));
        int fontSize = Editable ? 17 : 13;
        string text = (index + 1).ToString();
        float textWidth = GetThemeDefaultFont().GetStringSize(text, fontSize: fontSize).X;
        DrawString(GetThemeDefaultFont(), point + new Vector2(-textWidth / 2, fontSize * .35f), text, fontSize: fontSize, modulate: new("17212c"));
    }
}
