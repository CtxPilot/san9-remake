using Godot;

namespace San9.Client.Godot;

/// <summary>Shared full-page opening heading, positioned by its nominal glyph cell.</summary>
internal static class OpeningPageHeading
{
    internal static void Add(Control parent, GameTextPresenter text, string title, GameTextStyle? style = null)
    {
        var label = new Label
        {
            Name = "PageHeading", Text = title, Size = new(224, 24),
            ClipText = true, MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(label);
        text.ApplyAtGlyphCell(label, style ?? new(24, Colors.White, GameTextEffect.Outline, Colors.Black), new(48, 16));
    }
}
