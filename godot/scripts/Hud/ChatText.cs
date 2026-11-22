using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Hud;

/// <summary>
/// tg's HTML in tg's dark chat colours (<see cref="HtmlText"/>'s plain text, styles and links) drawn into a RichTextLabel: the chat
/// window's lines and, M10x slice G6 (DV216), tg's browse() pages. A link is the label's meta, its href.
/// </summary>
public static class ChatText
{
    private static Font _bold, _italic, _boldItalic;

    /// <summary>The label's look: the font sizes, tg's line height and its base colour.</summary>
    public static void Style(RichTextLabel text, int fontSize)
    {
        foreach (string name in new[] { "normal_font_size", "bold_font_size", "italics_font_size", "bold_italics_font_size" })
            text.AddThemeFontSizeOverride(name, fontSize);
        text.AddThemeConstantOverride("line_separation", 3); // tg's line height 1.2
        text.AddThemeColorOverride("default_color", ToColor(ChatStyles.Base.Rgba));
    }

    /// <summary>Adds <paramref name="plain"/> in its <paramref name="spans"/>' styles with its <paramref name="links"/> as metas.</summary>
    public static void Append(RichTextLabel text, string plain, IReadOnlyList<ChatSpan> spans, IReadOnlyList<ChatLink> links, int fontSize)
    {
        if (_bold == null)
        {
            Font font = ThemeDB.FallbackFont;
            var slant = new Transform2D(new Vector2(1, 0), new Vector2(0.2f, 1), Vector2.Zero);
            _bold = new FontVariation { BaseFont = font, VariationEmbolden = 0.6f };
            _italic = new FontVariation { BaseFont = font, VariationTransform = slant };
            _boldItalic = new FontVariation { BaseFont = font, VariationEmbolden = 0.6f, VariationTransform = slant };
        }
        spans ??= Array.Empty<ChatSpan>();
        links ??= Array.Empty<ChatLink>();
        int at = 0;
        while (at < plain.Length)
        {
            ChatSpan? span = null;
            ChatLink? link = null;
            int end = plain.Length;
            foreach (ChatSpan s in spans)
            {
                if (s.Start > at) end = Math.Min(end, s.Start);
                if (s.Start <= at && s.Start + s.Length > at) { end = Math.Min(end, s.Start + s.Length); span = s; }
            }
            foreach (ChatLink l in links)
            {
                if (l.Start > at) end = Math.Min(end, l.Start);
                if (l.Start <= at && l.Start + l.Length > at) { end = Math.Min(end, l.Start + l.Length); link = l; }
            }
            ChatStyle style = span?.Style ?? ChatStyles.Base;
            if (link is ChatLink l2)
            {
                text.PushMeta(l2.Href);
                text.PushUnderline();
            }
            Font face = style.Bold && style.Italic ? _boldItalic : style.Bold ? _bold : style.Italic ? _italic : null;
            int size = (int)MathF.Round(fontSize * style.Scale);
            if (face != null)
                text.PushFont(face, size);
            else if (style.Scale != 1f)
                text.PushFontSize(size);
            if (style.Bg != 0)
                text.PushBgcolor(ToColor(style.Bg)); // tg's announcement box (ChatStyles.Announcement)
            text.PushColor(link != null ? new Color(0.55f, 0.8f, 1f) : ToColor(style.Rgba));
            text.AddText(plain.Substring(at, end - at));
            text.Pop();
            if (style.Bg != 0)
                text.Pop();
            if (face != null || style.Scale != 1f)
                text.Pop();
            if (link != null)
            {
                text.Pop();
                text.Pop();
            }
            at = end;
        }
    }

    /// <summary>An RGBA colour (0xRRGGBBAA).</summary>
    public static Color ToColor(uint rgba) =>
        new(((rgba >> 24) & 0xFF) / 255f, ((rgba >> 16) & 0xFF) / 255f, ((rgba >> 8) & 0xFF) / 255f, (rgba & 0xFF) / 255f);
}
