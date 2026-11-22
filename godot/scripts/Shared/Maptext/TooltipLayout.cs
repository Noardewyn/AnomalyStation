using System.Linq;

namespace Anomaly.Client.Shared.Maptext;

// Protocol version 19 (tg's tooltips, DV47; docs/ai/specs/protocol.md section 28): the box of tg's tooltip, laid out as tg's page
// (code/modules/tooltip/tooltip.html) lays it out, engine-free. The page is
//   <div class="wrap"><div class="content">[title][content]</div></div>   in   <body class="[theme]">
// with .wrap { position: absolute; max-width: 298px; border: 2px solid } and .content { font: bold 12px Arial; padding: 8px;
// border: 2px solid; }, h1 { margin: -5px 0 2px 0; font-size: 1.2em; line-height: 1.4 } and p { margin: 0; line-height: 1.2 }, and the
// theme's colours (the wrap's border; the content's text, border and background). The absolutely placed wrap is as wide as its text
// without wrapping, at most 298 pixels inside its border; the page's script then makes the wrap 2 pixels wider ("Dumb hack to fix a
// bizarre sizing bug"), still at most 298, and the text reflows in it. Sizes are in CSS pixels at scale 1; the client scales them with
// the HUD.
//
// The page places the box under the hovered tile; the client places it under its pointer (TooltipLayout.Position, DV47), above the
// pointer when the bottom edge would leave the window (the page snaps it above the tile the same way).

/// <summary>A theme of tg's tooltip page: the colours of one <c>body</c> class (<c>0xRRGGBBAA</c>).</summary>
/// <param name="Name">The class (<c>"default"</c>, <c>"midnight"</c>, ...).</param>
/// <param name="WrapBorder">The outer border's colour (<c>.wrap</c>'s <c>border-color</c>).</param>
/// <param name="ContentBorder">The inner border's colour (<c>.content</c>'s <c>border-color</c>).</param>
/// <param name="Background">The box's background (<c>.content</c>'s <c>background</c>).</param>
/// <param name="Text">The text's colour (<c>.content</c>'s <c>color</c>).</param>
public sealed record TooltipTheme(string Name, uint WrapBorder, uint ContentBorder, uint Background, uint Text);

/// <summary>The themes of tg's <c>tooltip.html</c>.</summary>
public static class TooltipThemes
{
    // tg: code/modules/tooltip/tooltip.html (the style sheet). A theme that does not set a colour keeps the base one: .wrap's
    // border #1b2967, .content's color #ffffff, border #0033cc, background #005cb8.
    private const uint BaseWrap = 0x1B2967FF, BaseBorder = 0x0033CCFF, BaseBackground = 0x005CB8FF, BaseText = 0xFFFFFFFF;

    /// <summary>The page's base look (no theme class matches it: <c>"default"</c>).</summary>
    public static readonly TooltipTheme Default = new("default", BaseWrap, BaseBorder, BaseBackground, BaseText);

    private static readonly Dictionary<string, TooltipTheme> All = new[]
    {
        Default,
        // Custom Themes
        new TooltipTheme("blob", 0x2E2E2EFF, 0x4E4C4AFF, 0x191918FF, 0x82ED00FF),
        new TooltipTheme("parasite", 0x88868DFF, 0x35333AFF, 0x636169FF, 0xEFEEEFFF),
        new TooltipTheme("alien", 0x33165BFF, 0x5A3076FF, 0x6D3A8EFF, 0x25004AFF),
        new TooltipTheme("wraith", 0x492136FF, 0x331726FF, 0x471962FF, BaseText),
        new TooltipTheme("cult", 0x292222FF, 0x4C4343FF, 0x3C3434FF, 0xFF0000FF),
        new TooltipTheme("pod", 0x052401FF, 0x326D29FF, 0x569F4BFF, BaseText),
        new TooltipTheme("colo-pod", 0x256FB9FF, 0x000000FF, 0x000000FF, BaseText),
        new TooltipTheme("hisgrace", 0x7C1414FF, 0x9D1414FF, 0x861414FF, 0x15D512FF),
        // TG: Themes (ScreenUI: the HUD styles' themes, a player's ui_style lowercased)
        new TooltipTheme("midnight", 0x2B2B33FF, 0x2B2B33FF, 0x36363CFF, 0x6087A0FF),
        new TooltipTheme("plasmafire", 0x21213DFF, 0x21213DFF, 0x1D1D36FF, 0xFFA800FF),
        new TooltipTheme("retro", 0x005E00FF, 0x005E00FF, 0x00BD00FF, 0x003366FF),
        new TooltipTheme("slimecore", 0x18640EFF, 0x11450BFF, 0x354E35FF, 0x6EA161FF),
        new TooltipTheme("operative", 0x13121BFF, 0x13121BFF, 0x282831FF, 0xB01232FF),
        new TooltipTheme("clockwork", 0xCFBC47FF, 0xCFBC47FF, 0x523C15FF, 0xB18E25FF),
        new TooltipTheme("glass", 0x344B58FF, 0x344B58FF, 0x1F252BFF, 0x75A4C4FF),
        new TooltipTheme("trasen-knox", 0x998E81FF, 0x998E81FF, 0x1E1D21FF, 0x3CE375FF),
        new TooltipTheme("detective", 0x2C0F0CFF, 0x2C0F0CFF, 0x221C1AFF, 0xC7B08BFF),
    }.ToDictionary(t => t.Name, StringComparer.Ordinal);

    /// <summary>The theme of a class name; one the page has no rules for is the base look, as the page's CSS draws it.</summary>
    public static TooltipTheme Get(string name) => name != null && All.TryGetValue(name, out TooltipTheme theme) ? theme : Default;

    /// <summary>Every theme's name.</summary>
    public static IEnumerable<string> Names => All.Keys;
}

/// <summary>A laid out tooltip box (CSS pixels at scale 1, from the box's top left).</summary>
public sealed class TooltipBox
{
    /// <summary>The theme it is drawn in.</summary>
    public TooltipTheme Theme { get; init; }

    /// <summary>The text placed in the content's text area (positions from <see cref="TextX"/>, <see cref="TextY"/>).</summary>
    public MaptextLayoutResult Text { get; init; }

    /// <summary>The whole box's width, borders included.</summary>
    public float Width { get; init; }

    /// <summary>The whole box's height, borders included.</summary>
    public float Height { get; init; }

    /// <summary>The text area's left edge: the two borders and the padding.</summary>
    public float TextX { get; init; }

    /// <summary>The text area's top edge.</summary>
    public float TextY { get; init; }

    /// <summary>The text area's width (what the text wraps at).</summary>
    public float TextWidth { get; init; }
}

/// <summary>Lays tg's tooltip out and places it.</summary>
public static class TooltipLayout
{
    /// <summary><c>.wrap { max-width: 298px }</c>: the most the box is wide inside its outer border.</summary>
    public const float MaxWrapWidth = 298f;

    /// <summary><c>.wrap { border: 2px }</c>.</summary>
    public const float WrapBorder = 2f;

    /// <summary><c>.content { border: 2px }</c>.</summary>
    public const float ContentBorder = 2f;

    /// <summary><c>.content { padding: 8px }</c>.</summary>
    public const float Padding = 8f;

    /// <summary>The page's script: <c>$wrap.width($wrap.width() + 2)</c>.</summary>
    public const float WidthHack = 2f;

    /// <summary><c>.content { font: bold 12px Arial }</c>.</summary>
    public const float FontPx = 12f;

    /// <summary>The page's <c>tooltip.padding</c>: the gap it leaves between the hovered tile and the box.</summary>
    public const float Gap = 2f;

    /// <summary>The most the text area is wide: the wrap's 298 less the content's borders and padding.</summary>
    public const float MaxTextWidth = MaxWrapWidth - 2 * ContentBorder - 2 * Padding;

    /// <summary>The style of the text outside any tag: bold 12px sans in the theme's colour.</summary>
    public static MaptextStyle BodyStyle(TooltipTheme theme)
        => new(theme.Text, MaptextFont.Sans, FontPx, true, false, false, false, 0, 0x000000FF, 1f);

    /// <summary>Lays out tg's HTML <paramref name="text"/> (<c>"&lt;h1&gt;title&lt;/h1&gt;&lt;p&gt;content&lt;/p&gt;"</c>) in <paramref name="themeName"/>.</summary>
    public static TooltipBox Place(string text, string themeName, IMaptextMetrics metrics)
    {
        TooltipTheme theme = TooltipThemes.Get(themeName);
        MaptextDocument doc = MaptextMarkup.Parse(text, BodyStyle(theme), MaptextSheet.Tooltip);
        // The absolutely placed wrap shrinks to fit: as wide as the text unwrapped, at most the room max-width leaves.
        float natural = MaptextLayout.Place(doc, metrics, 0f, 0f).Width;
        float fit = Math.Min(natural, MaxTextWidth);
        // The script's two pixels, within max-width; the text reflows in the wider box.
        float textWidth = Math.Min(fit + WidthHack, MaxTextWidth);
        MaptextLayoutResult placed = MaptextLayout.Place(doc, metrics, textWidth, 0f);
        float inset = WrapBorder + ContentBorder + Padding;
        return new TooltipBox
        {
            Theme = theme,
            Text = placed,
            TextX = inset,
            TextY = inset,
            TextWidth = textWidth,
            Width = textWidth + 2 * inset,
            Height = Math.Max(0f, placed.Height) + 2 * inset,
        };
    }

    /// <summary>
    /// DV47: where the box's top left goes on a screen of <paramref name="screenWidth"/> x <paramref name="screenHeight"/> pixels for a pointer at
    /// <paramref name="pointerX"/>, <paramref name="pointerY"/>: <paramref name="below"/> pixels under the pointer (past its arrow), its left edge at the
    /// pointer's; above the pointer when the bottom edge would leave the screen (the page snaps it above the tile so); kept on the screen.
    /// <paramref name="width"/> and <paramref name="height"/> are the box's drawn size.
    /// </summary>
    public static (float X, float Y) Position(float pointerX, float pointerY, float width, float height, float screenWidth, float screenHeight, float below)
    {
        float x = pointerX;
        float y = pointerY + below;
        if (y + height > screenHeight)
            y = pointerY - height - Gap; //Is the bottom edge below the window? Snap it up if so
        if (x + width > screenWidth)
            x = screenWidth - width;
        x = Math.Max(0f, x);
        y = Math.Max(0f, y);
        return (MathF.Round(x), MathF.Round(y));
    }
}
