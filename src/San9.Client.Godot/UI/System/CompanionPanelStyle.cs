using Godot;

namespace San9.Client.Godot;

/// <summary>Opaque surfaces and restrained focus borders; no inherited game skin or drop shadows.</summary>
internal static class CompanionPanelStyle
{
    internal static Theme Create(Font font)
    {
        var theme = new Theme { DefaultFont = font, DefaultFontSize = 16 };
        foreach (string type in new[] { "Label", "Button", "TabContainer", "LineEdit" })
        {
            theme.SetColor("font_color", type, new("e8edf3"));
            theme.SetColor("font_shadow_color", type, Colors.Transparent);
            theme.SetColor("font_outline_color", type, Colors.Transparent);
            theme.SetConstant("outline_size", type, 0);
        }
        theme.SetStylebox("panel", "PanelContainer", Surface("1b212a", "46515f", 10));
        theme.SetStylebox("normal", "Button", Surface("2a333f", "465260", 6, 14, 8));
        theme.SetStylebox("hover", "Button", Surface("354151", "66778a", 6, 14, 8));
        theme.SetStylebox("pressed", "Button", Surface("202a35", "8093a7", 6, 14, 8));
        theme.SetStylebox("hover_pressed", "Button", theme.GetStylebox("pressed", "Button"));
        theme.SetStylebox("disabled", "Button", Surface("222932", "343e4b", 6, 14, 8));
        theme.SetStylebox("focus", "Button", Focus());
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_pressed_color", "Button", Colors.White);
        theme.SetColor("font_hover_pressed_color", "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", new("738093"));
        theme.SetStylebox("normal", "LineEdit", Surface("202a35", "526174", 6, 10, 6));
        theme.SetStylebox("focus", "LineEdit", Focus());
        theme.SetStylebox("read_only", "LineEdit", Surface("222932", "343e4b", 6, 10, 6));
        theme.SetColor("caret_color", "LineEdit", new("f0d3a3"));
        theme.SetColor("selection_color", "LineEdit", new("465c74"));

        var tabBody = Surface("1b212a", "1b212a", 0);
        tabBody.ContentMarginTop = 8;
        theme.SetStylebox("panel", "TabContainer", tabBody);
        theme.SetStylebox("tab_selected", "TabContainer", Surface("344151", "536478", 6, 18, 6));
        theme.SetStylebox("tab_unselected", "TabContainer", Surface("232b36", "232b36", 6, 18, 6));
        theme.SetStylebox("tab_hovered", "TabContainer", Surface("2c3745", "465669", 6, 18, 6));
        theme.SetStylebox("tab_disabled", "TabContainer", Surface("202731", "202731", 6, 18, 6));
        theme.SetStylebox("tab_focus", "TabContainer", Focus());
        theme.SetColor("font_selected_color", "TabContainer", new("f0d3a3"));
        theme.SetColor("font_unselected_color", "TabContainer", new("aab6c5"));
        theme.SetColor("font_hovered_color", "TabContainer", new("e8edf3"));

        theme.SetStylebox("scroll", "VScrollBar", Surface("1b212a", "1b212a", 3, 4, 4));
        theme.SetStylebox("scroll_focus", "VScrollBar", new StyleBoxEmpty());
        theme.SetStylebox("grabber", "VScrollBar", Surface("526174", "526174", 3, 4, 4));
        theme.SetStylebox("grabber_highlight", "VScrollBar", Surface("6b7e95", "6b7e95", 3, 4, 4));
        theme.SetStylebox("grabber_pressed", "VScrollBar", Surface("8d9fb5", "8d9fb5", 3, 4, 4));
        return theme;
    }

    internal static StyleBoxFlat Card() => Surface("252e3a", "3b495b", 8, 14, 10);

    internal static PanelContainer SectionHeading(string title)
    {
        var style = Surface("263d49", "61c9da", 6, 14, 6);
        style.BorderWidthLeft = 4;
        style.BorderWidthRight = style.BorderWidthTop = style.BorderWidthBottom = 0;
        var banner = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        banner.AddThemeStyleboxOverride("panel", style);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 18); banner.AddChild(row);
        var heading = new Label { Text = title };
        heading.AddThemeFontSizeOverride("font_size", 18);
        heading.AddThemeColorOverride("font_color", new("a4e3ed")); row.AddChild(heading);
        row.AddChild(new ColorRect { Color = new("4e7e8d"), CustomMinimumSize = new(0, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore });
        return banner;
    }

    internal static void Toggle(Button button)
    {
        button.AddThemeStyleboxOverride("normal", Surface("303b49", "526174", 6, 12, 6));
        button.AddThemeStyleboxOverride("hover", Surface("3b4859", "74869c", 6, 12, 6));
        button.AddThemeStyleboxOverride("pressed", Surface("294f4b", "60958b", 6, 12, 6));
        button.AddThemeStyleboxOverride("hover_pressed", Surface("335f59", "85b5a9", 6, 12, 6));
        button.AddThemeColorOverride("font_pressed_color", new("d7f4e8"));
        button.AddThemeColorOverride("font_hover_pressed_color", new("e6fff4"));
    }

    internal static void Primary(Button button)
    {
        button.AddThemeStyleboxOverride("normal", Surface("d1b17c", "d1b17c", 6, 14, 8));
        button.AddThemeStyleboxOverride("hover", Surface("e4c797", "e4c797", 6, 14, 8));
        button.AddThemeStyleboxOverride("pressed", Surface("b59663", "b59663", 6, 14, 8));
        foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color" })
            button.AddThemeColorOverride(color, new("20242a"));
    }

    private static StyleBoxFlat Focus() => new()
    {
        BgColor = Colors.Transparent, BorderColor = new("f0d3a3"),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ShadowSize = 0,
    };

    private static StyleBoxFlat Surface(string background, string border, int radius, int horizontal = 0, int vertical = 0) => new()
    {
        BgColor = new(background), BorderColor = new(border),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = horizontal, ContentMarginRight = horizontal, ContentMarginTop = vertical, ContentMarginBottom = vertical,
        ShadowSize = 0,
    };
}
