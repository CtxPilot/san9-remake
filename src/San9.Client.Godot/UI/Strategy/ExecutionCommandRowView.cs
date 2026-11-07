using Godot;
using San9.Application.Presentation;
using San9.Content.Interface;

namespace San9.Client.Godot;

/// <summary>A command row retains its controls while its text and countdown change.</summary>
internal partial class ExecutionCommandRowView : Control
{
    public TextureScope Textures { get; init; } = null!;
    public InterfaceSkin Skin { get; init; } = null!;
    private readonly GameTextPresenter _text = new();
    private readonly Label[] _labels = new Label[4];
    private ExecutionCommandRow? _entry;
    private Color _color;
    private Texture2D _frame = null!, _fill = null!;

    public override void _Ready()
    {
        Size = new(244, 16); MouseFilter = MouseFilterEnum.Ignore;
        int[] x = [14, 74, 110, 166], widths = [60, 36, 56, 74];
        for (int i = 0; i < _labels.Length; i++)
        {
            var label = new Label { Position = new(x[i], 0), Size = new(widths[i], 16),
                HorizontalAlignment = i == 3 ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                ClipText = true, MouseFilter = MouseFilterEnum.Ignore };
            _labels[i] = label; AddChild(label);
            _text.Apply(label, GameTextStyles.CatalogRow with { FontSize = 12 });
        }
        _frame = Textures.Get("turn-command-remaining-frame", this);
        _fill = Textures.Get("turn-command-remaining-fill", this);
    }

    public void Bind(ExecutionCommandRow entry, int colorCode)
    {
        var color = new Color(Skin.Colors[$"overview-force-{colorCode}"]);
        if (_entry == entry && _color == color) return;
        _entry = entry; _color = color;
        _labels[0].Text = entry.Officer;
        _labels[1].Text = entry.Command;
        _labels[2].Text = entry.Target;
        _labels[3].Text = entry.FinishesThisXun ? "" : "-------";
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_entry is not { } entry) return;
        DrawRect(new(2, 2, 12, 12), new Color("080808"));
        DrawRect(new(3, 3, 10, 10), _color);
        if (!entry.FinishesThisXun) return;
        DrawTexture(_frame, new(166, 3));
        int width = (int)(_fill.GetWidth() * Math.Min(10, entry.RemainingDays) / 10);
        if (width > 0) DrawTextureRectRegion(_fill, new(169, 6, width, 3), new(0, 0, width, 3));
    }

    public override void _ExitTree() => _text.Dispose();
}
