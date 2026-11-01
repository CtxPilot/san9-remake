using Godot;
using San9.Application;
using San9.Application.Presentation;
using San9.Content.Text;

namespace San9.Client.Godot;

internal static class DeploymentPresetStyle
{
    internal static readonly Color Gold = new("e3cc91"), Muted = new("a4b5c5"), Ink = new("e9edf0"), Warning = new("e0b47a");

    internal static StyleBoxFlat Surface(string fill, string border, int width = 1) => new()
    {
        BgColor = new(fill), BorderColor = new(border),
        BorderWidthLeft = width, BorderWidthTop = width, BorderWidthRight = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
        ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 4, ContentMarginBottom = 4
    };

    internal static void Panel(Control parent, Rect2 bounds, string fill, string border)
    {
        var panel = new Panel { Position = bounds.Position, Size = bounds.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", Surface(fill, border)); parent.AddChild(panel);
    }

    internal static void StyleButton(Button button, bool warning = false, bool selected = false)
    {
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", Muted);
        foreach (string state in new[] { "font_hover_color", "font_pressed_color", "font_hover_pressed_color" })
            button.AddThemeColorOverride(state, Gold);
        button.AddThemeStyleboxOverride("normal", Surface(warning ? "352b25" : "182a3c",
            selected ? "e3cc91" : warning ? "956c42" : "34485d", selected ? 2 : 1));
        button.AddThemeStyleboxOverride("hover", Surface(warning ? "48392b" : "233c53",
            selected ? "e3cc91" : warning ? "c39b66" : "8192a1"));
        button.AddThemeStyleboxOverride("pressed", Surface(warning ? "443629" : "2b3b49", "c4aa6d"));
        button.AddThemeStyleboxOverride("hover_pressed", Surface(warning ? "51412e" : "344957", "e3cc91"));
        button.AddThemeStyleboxOverride("disabled", Surface("161e26", "3b4653"));
        button.AddThemeStyleboxOverride("focus", new StyleBoxFlat
        {
            BgColor = Colors.Transparent, BorderColor = new("8192a1"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ExpandMarginLeft = 2, ExpandMarginTop = 2, ExpandMarginRight = 2, ExpandMarginBottom = 2
        });
    }

    internal static string DefaultName(DeploymentDraft draft, TextCatalog texts)
    {
        string name = draft.CommanderId is int id ? texts.Resolve(draft.View.Officers.Single(o => o.Id == id).NameKey) + "队" : "新编队";
        return name.Length <= DeploymentPreset.MaximumNameLength ? name : name[..DeploymentPreset.MaximumNameLength];
    }
}
