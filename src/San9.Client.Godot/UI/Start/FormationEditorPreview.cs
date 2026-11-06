using Godot;
using San9.Core.Domain;

namespace San9.Client.Godot;

/// <summary>Draws the authored positions directly; no original formation artwork is required.</summary>
public partial class FormationEditorPreview : Control
{
    private FormationRule? _rule;
    public FormationRule? Rule { get => _rule; set { _rule = value; QueueRedraw(); } }
    public override void _Ready() => Resized += Redraw;
    private void Redraw() => QueueRedraw();
    public override void _ExitTree() => Resized -= Redraw;
    public override void _Draw()
    {
        DrawStyleBox(ContentEditorStyle.Surface("151c24", "394758", 8), new(Vector2.Zero, Size));
        if (_rule is not { } rule) return;
        int columns = Math.Clamp(rule.Positions.Select(p => p.Column + 1).DefaultIfEmpty(3).Max(), 3, 12);
        var origin = new Vector2(10, 8); float width = (Size.X - 20) / columns, height = (Size.Y - 16) / 3;
        for (int x = 0; x <= columns; x++) DrawLine(origin + new Vector2(x * width, 0), origin + new Vector2(x * width, height * 3), new("344250"));
        for (int y = 0; y <= 3; y++) DrawLine(origin + new Vector2(0, y * height), origin + new Vector2(width * columns, y * height), new("344250"));
        for (int i = 0; i < rule.Positions.Length; i++)
        {
            var position = rule.Positions[i]; if (position.Column >= columns || !position.IsValid) continue;
            var point = origin + new Vector2((position.Column + .5f) * width, (position.Row + .5f) * height);
            DrawCircle(point, Math.Min(10, Math.Min(width, height) * .36f), new(i == 0 ? "f0d3a3" : "85b5a9"));
            DrawString(GetThemeDefaultFont(), point + new Vector2(-4, 5), (i + 1).ToString(), fontSize: 13, modulate: new("17212c"));
        }
    }
}
