using Godot;

namespace San9.Client.Godot;

internal static class ContentEditorStyle
{
    internal static readonly Color Muted = new("aab6c5"), Accent = new("f0d3a3");

    internal static Theme Create(Font font)
    {
        var theme = CompanionPanelStyle.Create(font);
        Dialogs(theme);
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

    private static void Dialogs(Theme theme)
    {
        var body = Surface("1b222c", "1b222c", 16);
        body.SetBorderWidthAll(0);
        body.SetCornerRadiusAll(0);
        theme.SetStylebox("panel", "AcceptDialog", body);
        theme.SetConstant("buttons_separation", "AcceptDialog", 16);

        var frame = Surface("263240", "465260");
        frame.SetCornerRadiusAll(10);
        frame.ContentMarginLeft = frame.ContentMarginRight = 10;
        frame.ContentMarginTop = 36; frame.ContentMarginBottom = 8;
        frame.ExpandMarginLeft = frame.ExpandMarginRight = frame.ExpandMarginBottom = 8;
        frame.ExpandMarginTop = 40;
        theme.SetStylebox("embedded_border", "Window", frame);
        var inactive = (StyleBoxFlat)frame.Duplicate();
        inactive.BgColor = new("202a35"); inactive.BorderColor = new("394758");
        theme.SetStylebox("embedded_unfocused_border", "Window", inactive);
        theme.SetColor("title_color", "Window", new("e8edf3"));
        theme.SetColor("title_outline_modulate", "Window", Colors.Transparent);
        theme.SetFontSize("title_font_size", "Window", 18);
        theme.SetConstant("title_outline_size", "Window", 0);
        theme.SetConstant("title_height", "Window", 40);
        theme.SetConstant("close_h_offset", "Window", 20);
        theme.SetConstant("close_v_offset", "Window", 24);
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
