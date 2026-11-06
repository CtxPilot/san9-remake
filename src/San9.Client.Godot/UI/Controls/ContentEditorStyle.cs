using Godot;

namespace San9.Client.Godot;

internal static class ContentEditorStyle
{
    internal static readonly Color Muted = new("aab6c5"), Accent = new("f0d3a3");

    internal static Theme Create(Font font)
    {
        var theme = CompanionPanelStyle.Create(font);
        theme.SetStylebox("panel", "TabContainer", Surface("202833", "394758"));
        theme.SetStylebox("panel", "ItemList", Surface("151c24", "394758", 6));
        theme.SetStylebox("selected", "ItemList", Surface("294f4b", "60958b", 8));
        theme.SetStylebox("selected_focus", "ItemList", Surface("335f59", "85b5a9", 8));
        theme.SetStylebox("cursor", "ItemList", new StyleBoxEmpty());
        theme.SetStylebox("cursor_unfocused", "ItemList", new StyleBoxEmpty());
        theme.SetStylebox("hovered", "ItemList", Surface("263442", "3b495b", 8));
        theme.SetColor("font_color", "ItemList", new("e8edf3"));
        theme.SetColor("font_selected_color", "ItemList", new("e6fff4"));
        theme.SetConstant("v_separation", "ItemList", 8);
        theme.SetConstant("line_separation", "ItemList", 6);
        theme.SetStylebox("normal", "TextEdit", Surface("17212c", "526174", 10));
        theme.SetStylebox("focus", "TextEdit", Surface("17212c", "f0d3a3", 10));
        theme.SetColor("font_color", "TextEdit", new("e8edf3"));
        theme.SetColor("caret_color", "TextEdit", Accent);
        theme.SetColor("selection_color", "TextEdit", new("465c74"));
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            theme.SetStylebox(state, "OptionButton", theme.GetStylebox(state, "Button"));
        foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_disabled_color" })
            theme.SetColor(color, "OptionButton", theme.GetColor(color, "Button"));
        return theme;
    }

    internal static void Compact(Button button)
    {
        button.AddThemeFontSizeOverride("font_size", 14); button.CustomMinimumSize = new(52, 28);
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
        {
            var style = (StyleBoxFlat)button.GetThemeStylebox(state).Duplicate();
            style.ContentMarginLeft = style.ContentMarginRight = 8;
            style.ContentMarginTop = style.ContentMarginBottom = 3;
            button.AddThemeStyleboxOverride(state, style);
        }
    }

    internal static StyleBoxFlat Surface(string background, string border, int padding = 0) => new()
    {
        BgColor = new(background), BorderColor = new(border),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = padding, ContentMarginBottom = padding,
        ShadowSize = 0
    };
}
