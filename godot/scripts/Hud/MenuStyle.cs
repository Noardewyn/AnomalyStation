using Godot;
using Anomaly.Client.Hud.Maptext;
using Anomaly.Client.Hud.Tgui;
using Anomaly.Client.Shared.Maptext;

namespace Anomaly.Client.Hud;

/// <summary>
/// The Esc menu's look (D282), which Settings, the bug report and the changelog share (the owner, 2026-10-28): tg's Pixellari for
/// titles, tabs and buttons, Grand9K Pixel for text, the dark blue panel with the menu's gold hover. Each font falls back to the
/// default one, so icons (Font Awesome is its fallback) and symbols such as the close button's still draw.
/// </summary>
internal static class MenuStyle
{
    public static readonly Color Text = new(0.85f, 0.85f, 0.85f);
    public static readonly Color Accent = new(1f, 0.85f, 0.4f);
    public static readonly Color Dim = new(0.65f, 0.65f, 0.65f);
    public static readonly Color Disabled = new(0.4f, 0.4f, 0.4f);
    public static readonly Color TitleBar = new(0.11f, 0.13f, 0.18f);

    private static Font _title, _text;
    private static Theme _theme;

    public static Font TitleFont => _title ??= WithFallback(MaptextRenderer.Shared.Fonts.Get(MaptextFont.Pixellari, false, false));

    public static Font TextFont => _text ??= WithFallback(MaptextRenderer.Shared.Fonts.Get(MaptextFont.Grand9K, false, false));

    /// <summary>The Godot theme of a window in this look: set it on the window's root.</summary>
    public static Theme Theme => _theme ??= BuildTheme();

    /// <summary>The tgui window palette in this look (a window drawn by TguiHost, the changelog).</summary>
    public static readonly TguiTheme.Palette Palette = new(new Color(0.07f, 0.08f, 0.11f), new Color(0.07f, 0.08f, 0.11f), TitleBar, Accent,
        new Color(0, 0, 0, 0.25f), Accent, Text, Dim, new Color(0.04f, 0.04f, 0.05f), new Color(0.35f, 0.4f, 0.5f), new Color(1, 1, 1, 0.1f),
        new Color(0.16f, 0.19f, 0.26f), new Color(1, 1, 1, 0.5f));

    /// <summary>A <see cref="WindowChrome.TitleBar"/> in this look: the menu's title font and colours.</summary>
    public static Control Restyle(Control bar)
    {
        bar.AddThemeStyleboxOverride("panel", TguiRender.Flat(TitleBar, 0));
        foreach (Node node in bar.FindChildren("*", "Label", true, false))
        {
            var label = (Label)node;
            label.AddThemeFontOverride("font", TitleFont);
            label.AddThemeFontSizeOverride("font_size", 20);
            label.AddThemeColorOverride("font_color", Accent);
        }
        return bar;
    }

    /// <summary>
    /// The owner (2026-10-28): a button looks like one, apart from the text around it: a framed box, gold framed under the pointer,
    /// darker pressed, faint when off.
    /// </summary>
    public static void StyleButton(Button button)
    {
        foreach ((string state, StyleBox box) in ButtonBoxes())
            button.AddThemeStyleboxOverride(state, box);
    }

    private static (string, StyleBox)[] ButtonBoxes()
    {
        StyleBoxFlat Box(Color bg, Color border)
        {
            var box = new StyleBoxFlat { BgColor = bg, BorderColor = border, ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 3, ContentMarginBottom = 3 };
            box.SetBorderWidthAll(1);
            box.SetCornerRadiusAll(3);
            return box;
        }
        return new (string, StyleBox)[]
        {
            ("normal", Box(new Color(1, 1, 1, 0.06f), new Color(0.35f, 0.4f, 0.5f))),
            ("hover", Box(new Color(1f, 0.85f, 0.4f, 0.12f), Accent)),
            ("pressed", Box(new Color(0, 0, 0, 0.3f), Accent)),
            ("disabled", Box(new Color(1, 1, 1, 0.02f), new Color(0.22f, 0.24f, 0.28f))),
            ("focus", new StyleBoxEmpty()),
        };
    }

    private static Font WithFallback(Font face) => new FontVariation { BaseFont = face, Fallbacks = new Godot.Collections.Array<Font> { ThemeDB.FallbackFont } };

    private static Theme BuildTheme()
    {
        var theme = new Theme { DefaultFont = TextFont, DefaultFontSize = 16 };
        theme.SetColor("font_color", "Label", Text);
        foreach (string type in new[] { "Button", "TabContainer", "TabBar" })
        {
            theme.SetFont("font", type, TitleFont);
            theme.SetFontSize("font_size", type, 19);
        }
        theme.SetColor("font_color", "Button", Text);
        theme.SetColor("font_hover_color", "Button", Accent);
        theme.SetColor("font_pressed_color", "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", Disabled);
        foreach ((string state, StyleBox box) in ButtonBoxes())
            theme.SetStylebox(state, "Button", box);
        foreach (string type in new[] { "TabContainer", "TabBar" })
        {
            theme.SetColor("font_selected_color", type, Accent);
            theme.SetColor("font_unselected_color", type, Dim);
            theme.SetColor("font_hovered_color", type, Text);
            var selected = new StyleBoxFlat { BgColor = TitleBar, BorderColor = Accent, BorderWidthBottom = 2, ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 4, ContentMarginBottom = 4 };
            var idle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 4, ContentMarginBottom = 4 };
            theme.SetStylebox("tab_selected", type, selected);
            theme.SetStylebox("tab_unselected", type, idle);
            theme.SetStylebox("tab_hovered", type, idle);
        }
        theme.SetStylebox("panel", "TabContainer", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.2f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6 });
        return theme;
    }
}
