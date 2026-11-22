using Godot;

namespace Anomaly.Client.Hud.Tgui;

// M5 slice 3 (D148): tg's dark Nanotrasen theme of tgui (tgui-core's default theme: its colour palette, the window, title bar,
// section, button, input, notice box and label colours), and tgui's icon font (Font Awesome names) mapped to Unicode symbols the
// client's font has. tgui-core's stylesheets are an npm dependency the tg clone does not carry; the values are tgui's well-known
// defaults.

public static class TguiTheme
{
    /// <summary>
    /// A window's colours. tgui-core's default is <see cref="Nanotrasen"/>; tgui's other window themes (the NtOS themes a PDA's
    /// Themeify picks, tg's <c>PDA_THEME_*</c>) are the client's own palettes in each theme's spirit (DV172; the owner, 2026-10-23: tgui-core's
    /// theme stylesheets are not in the tg clone).
    /// </summary>
    public sealed record Palette(Color WindowBg, Color WindowBgEnd, Color TitleBarBg, Color TitleText, Color SectionBg, Color SectionRule,
        Color Text, Color Label, Color InputBg, Color InputBorder, Color Divider, Color ButtonDefault, Color TransparentText);

    public static readonly Palette Nanotrasen = new(Hex("#252525"), Hex("#1d1d1d"), Hex("#363636"), new Color(1, 1, 1, 0.75f), new Color(0, 0, 0, 0.33f),
        Hex("#4972a1"), Hex("#ffffff"), Hex("#7e90a7"), Hex("#0a0a0a"), Hex("#88bfff"), new Color(1, 1, 1, 0.1f), Hex("#3e6189"), new Color(1, 1, 1, 0.5f));

    // the NtOS themes (__DEFINES/modular_computer.dm:97-113), by their tgui theme names
    private static readonly Dictionary<string, Palette> Themes = new(StringComparer.Ordinal)
    {
        ["ntos"] = Dark("#1f2b39", "#17202b", "#2a3a4d", "#577ba6", "#8ba5c4", "#6fa0d8", "#384e68", "#ffffff"),
        ["ntos_darkmode"] = Dark("#121212", "#0c0c0c", "#1e1e1e", "#4a4a4a", "#9a9a9a", "#777777", "#2f2f2f", "#e6e6e6"),
        ["ntos_lightmode"] = new(Hex("#e8e8e8"), Hex("#d9d9d9"), Hex("#c9c9c9"), new Color(0, 0, 0, 0.8f), new Color(1, 1, 1, 0.45f), Hex("#6f8fb0"),
            Hex("#1a1a1a"), Hex("#505a66"), Hex("#ffffff"), Hex("#6f8fb0"), new Color(0, 0, 0, 0.12f), Hex("#7f9bb8"), new Color(0, 0, 0, 0.5f)),
        ["ntOS95"] = new(Hex("#c3c3c3"), Hex("#b5b5b5"), Hex("#000a80"), new Color(1, 1, 1, 0.95f), new Color(1, 1, 1, 0.25f), Hex("#000a80"),
            Hex("#000000"), Hex("#303030"), Hex("#ffffff"), Hex("#000000"), new Color(0, 0, 0, 0.2f), Hex("#9a9a9a"), new Color(0, 0, 0, 0.5f)),
        ["ntos_synth"] = Dark("#2a1638", "#1d0f28", "#5b1a6e", "#ff4fd8", "#e39bff", "#ff6bdb", "#8a2a8a", "#ffe6ff"),
        ["ntos_terminal"] = Dark("#000000", "#000000", "#0b1a0b", "#33ff33", "#22aa22", "#33ff33", "#0f3d0f", "#33ff33"),
        ["syndicate"] = Dark("#2b0e0e", "#1f0909", "#4a1212", "#a63737", "#c98989", "#d65c5c", "#6b1d1d", "#ffffff"),
        ["ntos_cat"] = Dark("#3b2a33", "#2c1f26", "#6e3f57", "#ff9ccc", "#f0b6d2", "#ff9ccc", "#b05c86", "#ffffff"),
        ["ntos_spooky"] = Dark("#0f1a12", "#0a120c", "#1c3322", "#6fbf73", "#8fbf8f", "#6fbf73", "#2e5c3a", "#d8ffd8"),
        // tgui-core's malfunction theme (the malf AI's module picker): a deep blue-green base, red accents
        ["malfunction"] = Dark("#1b3443", "#13252f", "#235577", "#910101", "#7fb5d4", "#c42424", "#2c4c5c", "#ffffff"),
        ["hackerman"] = Dark("#000000", "#000000", "#001a00", "#00ff00", "#00cc00", "#00ff00", "#003300", "#00ff00"),
        ["cardtable"] = Dark("#0f4d24", "#0b3a1b", "#6b1f1f", "#d4af37", "#d8c58f", "#d4af37", "#8b5a2b", "#ffffff"),
        ["abductor"] = Dark("#2c1c3d", "#201429", "#4a2f68", "#b89cff", "#c9b3ff", "#b89cff", "#6a43a0", "#ffffff"),
        ["neutral"] = Dark("#3a3a3a", "#2f2f2f", "#4a4a4a", "#8a8a8a", "#b0b0b0", "#9a9a9a", "#5a5a5a", "#ffffff"),
        // tgui's operating_computer theme (gaps P1, styles/interfaces/OperatingComputer.scss): a deep green base, hsl(150, 100%, 18%), its
        // primary 5 % lighter; the DeForest logo behind the content is not drawn
        ["operating_computer"] = Dark("#005c2e", "#005c2e", "#002914", "#00753b", "#a8d8bc", "#00753b", "#00753b", "#ffffff"),
        // tgui's retro theme (the portable gravity unit's window, E16): beige panels, dark text
        ["retro"] = new(Hex("#e8e4c9"), Hex("#dcd6b4"), Hex("#585337"), new Color(1, 1, 1, 0.9f), new Color(0, 0, 0, 0.08f), Hex("#7b7550"),
            Hex("#000000"), Hex("#3e3b28"), Hex("#ffffff"), Hex("#585337"), new Color(0, 0, 0, 0.15f), Hex("#c9c2a0"), new Color(0, 0, 0, 0.5f)),
    };

    private static Palette Dark(string bg, string bgEnd, string title, string rule, string label, string inputBorder, string button, string text)
        => new(Hex(bg), Hex(bgEnd), Hex(title), new Color(1, 1, 1, 0.8f), new Color(0, 0, 0, 0.33f), Hex(rule), Hex(text), Hex(label),
            Hex("#0a0a0a"), Hex(inputBorder), new Color(1, 1, 1, 0.1f), Hex(button), new Color(1, 1, 1, 0.5f));

    /// <summary>The palette of a tgui window theme (null or unknown: the default).</summary>
    public static Palette Of(string theme) => theme != null && Themes.TryGetValue(theme, out Palette p) ? p : Nanotrasen;

    /// <summary>The palette the controls being built or drawn use (<see cref="Use"/>); the default outside a window.</summary>
    public static Palette Current { get; private set; } = Nanotrasen;

    /// <summary>Makes <paramref name="palette"/> current until the returned scope is disposed (a window's build, a control's draw).</summary>
    public static Scope Use(Palette palette)
    {
        var scope = new Scope(Current);
        Current = palette ?? Nanotrasen;
        return scope;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly Palette _previous;

        internal Scope(Palette previous) => _previous = previous;

        public void Dispose() => Current = _previous;
    }

    public static Color WindowBg => Current.WindowBg;
    public static Color WindowBgEnd => Current.WindowBgEnd;
    public static Color TitleBarBg => Current.TitleBarBg;
    public static Color TitleText => Current.TitleText;
    public static Color SectionBg => Current.SectionBg;
    public static Color SectionRule => Current.SectionRule;
    public static Color Text => Current.Text;
    public static Color Label => Current.Label;
    public static Color InputBg => Current.InputBg;
    public static Color InputBorder => Current.InputBorder;
    public static Color Divider => Current.Divider;
    public static readonly Color NoticeBg = Hex("#bb9b68");
    public static readonly Color Dimmer = new(0, 0, 0, 0.6f);

    public static Color ButtonDefault => Current.ButtonDefault;
    public static readonly Color ButtonSelected = Hex("#1b9638");
    public static readonly Color ButtonCaution = Hex("#d9b804");
    public static readonly Color ButtonDanger = Hex("#bd2020");
    public static readonly Color ButtonDisabled = Hex("#999999");
    public static Color TransparentText => Current.TransparentText;

    /// <summary>tgui's colour names (tgui-core's palette: <c>good</c>, <c>average</c>, <c>bad</c>, <c>label</c>, the named colours), else a CSS colour, else null.</summary>
    public static Color? Named(string name) => name switch
    {
        null or "" => null,
        "black" => Hex("#000000"),
        "white" => Hex("#ffffff"),
        "red" => Hex("#db2828"),
        "orange" => Hex("#f2711c"),
        "yellow" => Hex("#fbd608"),
        "olive" => Hex("#b5cc18"),
        "green" => Hex("#20b142"),
        "teal" => Hex("#00b5ad"),
        "blue" => Hex("#2185d0"),
        "violet" => Hex("#6435c9"),
        "purple" => Hex("#a333c8"),
        "pink" => Hex("#e03997"),
        "brown" => Hex("#a5673f"),
        "grey" or "gray" => Hex("#767676"),
        "light-grey" or "light-gray" => Hex("#aaaaaa"),
        "good" => Hex("#5baa27"),
        "average" => Hex("#f08f11"),
        "bad" => Hex("#db2828"),
        "label" => Label,
        "caution" => ButtonCaution,
        "danger" => ButtonDanger,
        _ when name.StartsWith('#') && Color.HtmlIsValid(name) => Color.FromHtml(name),
        _ when name.StartsWith("rgba(", StringComparison.Ordinal) => Rgba(name),
        _ => CssNamed(name),
    };

    // A CSS colour name (tgui-core's ProgressBar sets a colour outside its palette as the fill's background-color: the gases' colours of
    // tgui's constants.ts, lightsteelblue and the like, E2); Godot's named colours are CSS's. Null for an unknown name.
    private static Color? CssNamed(string name)
    {
        var none = new Color(0, 0, 0, -1);
        Color color = Color.FromString(name, none);
        return color == none ? null : color;
    }

    /// <summary>
    /// A button's CSS background <c>linear-gradient(to right, c1, ..., cn)</c> of hex colours, spread evenly (gaps package G4: MemoryPanel's
    /// stars); null for anything else, as a browser drops an invalid value (MemoryPanel's "meh" star ends in a semicolon: a plain button).
    /// </summary>
    public static Color[] LinearGradient(string css)
    {
        const string Prefix = "linear-gradient(to right,";
        if (css == null || !css.StartsWith(Prefix, StringComparison.Ordinal) || !css.EndsWith(')'))
            return null;
        string[] parts = css[Prefix.Length..^1].Split(',');
        var colors = new Color[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            if (!part.StartsWith('#') || !Color.HtmlIsValid(part))
                return null;
            colors[i] = Color.FromHtml(part);
        }
        return colors.Length >= 2 ? colors : null;
    }

    // CSS rgba(r, g, b, a) (SolarControl's chart colours), else null.
    private static Color? Rgba(string css)
    {
        string[] parts = css["rgba(".Length..].TrimEnd(')').Split(',');
        if (parts.Length != 4
            || !float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r)
            || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float g)
            || !float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float b)
            || !float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float a))
            return null;
        return new Color(r / 255f, g / 255f, b / 255f, a);
    }

    /// <summary>A button's background for its colour name (tgui-core's Button: the colour darkened as its background).</summary>
    public static Color ButtonColor(string name, bool selected)
    {
        if (selected)
            return ButtonSelected;
        return name switch
        {
            null or "" or "default" => ButtonDefault,
            "caution" or "yellow" => ButtonCaution,
            "danger" => ButtonDanger,
            "transparent" => new Color(0, 0, 0, 0),
            _ => Named(name)?.Darkened(0.15f) ?? ButtonDefault,
        };
    }

    /// <summary>Black text on the light buttons (tgui's low-contrast colours: yellow, caution, white).</summary>
    public static bool DarkText(string name) => name is "caution" or "yellow" or "white";

    public static Color Hex(string html) => Color.FromHtml(html);

    /// <summary>
    /// tgui's icon: Font Awesome's glyph of the name (<see cref="FontAwesomeFont"/>, drawn by the default font's fallback). tgui's outline
    /// names ("-o": Font Awesome's regular face, which the client does not load) keep their outline symbol where there is one (an empty
    /// checkbox must not look ticked), else take the solid glyph of the name without "-o" (NtOS's minimize and close). tg's own icon font
    /// ("tg-") and a missing Font Awesome fall back to a symbol.
    /// </summary>
    // shortcut: "-o" icons are drawn solid; load fa-regular-400.ttf as a face of its own when an outline look matters.
    public static string Glyph(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        if (name.StartsWith("tg-", StringComparison.Ordinal))
            return Symbol(name);
        if (name.EndsWith("-o", StringComparison.Ordinal))
        {
            string symbol = Symbol(name);
            return symbol != NoSymbol ? symbol : FontAwesomeFont.Glyph(name[..^2]) ?? symbol;
        }
        return FontAwesomeFont.Glyph(name) ?? Symbol(name);
    }

    private const string NoSymbol = "•";

    // The symbols that stood in for tgui's icons before Font Awesome.
    private static string Symbol(string name) => name switch
    {
        "power-off" => "⏻",
        "times" or "window-close" or "xmark" => "✕",
        "pencil-alt" or "pencil" => "✎",
        "palette" => "◑",
        "fast-forward" => "»",
        "undo" => "↺",
        "redo" => "↻",
        "plug" => "⏚",
        "eject" => "⏏",
        "snowflake" => "❄",
        "search" => "⌕",
        "check" => "✓",
        "check-circle" => "✔",
        "times-circle" => "⊗",
        "stop-circle" => "◼",
        "check-double" => "✓✓",
        "square-o" or "square" => "☐",
        "check-square-o" or "check-square" => "☑",
        "lock" => "⊠",
        "unlock" => "⊡",
        "angle-double-left" => "«",
        "angle-double-right" => "»",
        "angle-down" => "▾",
        "angle-up" => "▴",
        "angle-left" => "‹",
        "angle-right" => "›",
        "caret-right" or "chevron-right" => "▸",
        "caret-down" or "chevron-down" => "▾",
        "minus" => "−",
        "plus" => "+",
        "info" or "info-circle" => "ⓘ",
        "exclamation-triangle" => "⚠",
        "cog" => "⚙",
        "trash" => "⌫",
        "arrow-up" => "↑",
        "arrow-down" => "↓",
        "arrow-left" => "←",
        "arrow-right" => "→",
        "hammer" => "⚒",
        "repeat" => "⟳",
        "circle-notch" => "◌",
        "screwdriver-wrench" => "⚒",
        "circle-question-o" => "?",
        "exclamation" => "!",
        "sign-out-alt" => "⇥",
        "sign-in-alt" => "⇤",
        "filter" => "⏷",
        "chart-bar" => "▤",
        "link-slash" => "⛓",
        "gauge-high" => "◔",
        "expand" => "⤢",
        "compress" => "⤡",
        "sync" => "⟳",
        "clock-o" or "clock" => "◷",
        "battery-full" => "▓",
        "battery-half" => "▒",
        "battery-quarter" => "░",
        "bolt" => "↯",
        "toolbox" => "⚒",
        "shoe-prints" => "⁖",
        "handcuffs" => "⛓",
        "tg-air-tank" => "◍",
        "tg-air-tank-slash" => "⊘",
        "tshirt" => "Ⓣ",
        "microchip" => "▣",
        "ribbon" => "✿",
        "box-open" => "▭",
        "ban" => "⊘",
        "eye-slash" => "⊖",
        _ => NoSymbol,
    };
}
