using System.Globalization;
using System.Linq;
using System.Text;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// tg's chat HTML as plain lines for the console and the analyzer panel (the client shows no HTML in M2; windows are native
/// UI, D44). Block tags and line breaks become line breaks, the rest of the tags are dropped, the common entities are
/// decoded; the result is bounded.
/// </summary>
/// <summary>Protocol version 24 (D154): a link of a chat line, the plain text's <see cref="Length"/> characters from <see cref="Start"/>, and its href.</summary>
public readonly record struct ChatLink(int Start, int Length, string Href);

/// <summary>A run of the plain text, <see cref="Length"/> characters from <see cref="Start"/>, in tg's chat style <see cref="Style"/> (<see cref="ChatStyles"/>).</summary>
public readonly record struct ChatSpan(int Start, int Length, ChatStyle Style);

public static class HtmlText
{
    /// <summary>The most characters of a result.</summary>
    public const int MaxChars = 8192;

    public static string ToPlain(string html) => ToPlain(html, null);

    /// <summary>
    /// Protocol version 24 (D154): <see cref="ToPlain(string)"/>, and the BYOND links of the HTML (<c>&lt;a href='byond://?...'&gt;</c> whose
    /// href fits <see cref="Limits.MaxTopicHrefBytes"/>) as spans of the plain text, in order, into <paramref name="links"/> (cleared first).
    /// </summary>
    public static string ToPlain(string html, List<ChatLink> links) => ToPlain(html, links, null);

    /// <summary>
    /// <see cref="ToPlain(string, List{ChatLink})"/>, and the text's runs in tg's chat styles into <paramref name="spans"/> (cleared first;
    /// together they cover the text): an element's <c>class</c> by tg's dark chat sheet (<see cref="ChatStyles"/>), its inline <c>color</c>,
    /// <c>font-weight</c> and <c>font-style</c>, <c>&lt;font color&gt;</c>, and b, strong, i and em (tg's sheet: em is bold, not italic).
    /// </summary>
    public static string ToPlain(string html, List<ChatLink> links, List<ChatSpan> spans)
    {
        links?.Clear();
        spans?.Clear();
        if (string.IsNullOrEmpty(html))
            return string.Empty;
        var hrefs = links == null && spans == null ? null : new List<string>();
        var styles = spans == null ? null : new List<ChatStyle> { ChatStyles.Base };
        var open = spans == null ? null : new List<(string Name, int Style)>();
        bool inLink = false;
        var sb = new StringBuilder(Math.Min(html.Length, MaxChars));
        int i = 0;
        while (i < html.Length && sb.Length < MaxChars)
        {
            char ch = html[i];
            if (ch == '<')
            {
                int close = html.IndexOf('>', i + 1);
                if (close < 0)
                    break; // an unfinished tag at the end: drop the rest
                string raw = html.Substring(i + 1, close - i - 1).Trim();
                string tag = raw.ToLowerInvariant();
                if (IsBreak(tag))
                    NewLine(sb);
                else if ((tag == "td" || tag.StartsWith("td ", StringComparison.Ordinal)) && sb.Length > 0 && !char.IsWhiteSpace(sb[^1]))
                    sb.Append(' '); // a table's cells (a health scan's limb and organ tables) apart
                if (styles != null)
                    StyleTag(sb, raw, tag, styles, open);
                if (hrefs != null)
                {
                    // a link's text is marked with two private-use characters, taken out again below with its span
                    string href = tag.StartsWith("a ", StringComparison.Ordinal) ? HrefOf(raw) : null;
                    if (href != null && !inLink)
                    {
                        hrefs.Add(href);
                        sb.Append(LinkOpen);
                        inLink = true;
                    }
                    else if (inLink && (tag == "/a" || tag.StartsWith("/a ", StringComparison.Ordinal)))
                    {
                        sb.Append(LinkClose);
                        inLink = false;
                    }
                }
                i = close + 1;
            }
            else if (ch == '&')
            {
                int semi = html.IndexOf(';', i + 1, Math.Min(10, html.Length - i - 1));
                if (semi > 0 && TryDecodeEntity(html.AsSpan(i + 1, semi - i - 1), out string decoded))
                {
                    sb.Append(decoded);
                    i = semi + 1;
                }
                else
                {
                    sb.Append(ch);
                    i++;
                }
            }
            else
            {
                sb.Append(ch == '\r' || ch == '\t' ? ' ' : ch);
                i++;
            }
        }
        // Lines are trimmed, runs of blank lines collapse to one (the marks stay: a dropped line's go to the next line).
        var lines = new List<string>();
        string carried = string.Empty;
        foreach (string line in sb.ToString().Split('\n'))
        {
            string trimmed = carried + TrimMarked(line);
            carried = string.Empty;
            if (IsBlank(trimmed) && (lines.Count == 0 || IsBlank(lines[^1])))
            {
                carried = Marks(trimmed);
                continue;
            }
            lines.Add(trimmed);
        }
        while (lines.Count > 0 && IsBlank(lines[^1]))
            lines.RemoveAt(lines.Count - 1);
        string plain = string.Join('\n', lines);
        return hrefs == null ? plain : TakeLinks(plain, hrefs, links, styles, spans);
    }

    // The style marks: StyleMark + the style's index in the line's style list (a mark sets the style of what follows).
    private const char StyleMark = '\uE100';
    private const int MaxStyles = 0x1000;

    private static bool IsMark(char ch) => ch is LinkOpen or LinkClose || (ch >= StyleMark && ch < StyleMark + MaxStyles);

    private static bool IsBlank(string line) => line.All(ch => IsMark(ch) || char.IsWhiteSpace(ch));

    private static string Marks(string line) => new(line.Where(IsMark).ToArray());

    // Trim's whitespace at the ends, the marks there kept (a mark is no character of the text).
    private static string TrimMarked(string line)
    {
        int first = 0, last = line.Length - 1;
        while (first <= last && (IsMark(line[first]) || char.IsWhiteSpace(line[first]))) first++;
        while (last >= first && (IsMark(line[last]) || char.IsWhiteSpace(line[last]))) last--;
        if (first > last)
            return Marks(line);
        return Marks(line.Substring(0, first)) + line.Substring(first, last - first + 1) + Marks(line.Substring(last + 1));
    }

    private static readonly HashSet<string> VoidTags = new(StringComparer.Ordinal) { "br", "img", "hr", "input", "meta", "link", "wbr", "col", "area", "base", "source" };

    // One tag of the HTML for the styles: an opening one pushes its element's style, a closing one pops back to its element's parent.
    private static void StyleTag(StringBuilder sb, string raw, string tag, List<ChatStyle> styles, List<(string Name, int Style)> open)
    {
        bool closing = tag.StartsWith('/');
        string name = TagName(closing ? tag.Substring(1) : tag);
        if (name.Length == 0)
            return;
        int before = open.Count > 0 ? open[^1].Style : 0;
        if (closing)
        {
            int at = open.FindLastIndex(e => e.Name == name);
            if (at < 0)
                return;
            open.RemoveRange(at, open.Count - at);
        }
        else
        {
            if (VoidTags.Contains(name) || tag.EndsWith('/'))
                return;
            ChatStyle style = ElementStyle(styles[before], name, raw);
            int index = styles.IndexOf(style);
            if (index < 0 && styles.Count < MaxStyles)
            {
                styles.Add(style);
                index = styles.Count - 1;
            }
            open.Add((name, index < 0 ? before : index));
        }
        int now = open.Count > 0 ? open[^1].Style : 0;
        if (now != before)
            sb.Append((char)(StyleMark + now));
    }

    private static string TagName(string tag)
    {
        int end = 0;
        while (end < tag.Length && char.IsAsciiLetterOrDigit(tag[end]))
            end++;
        return tag.Substring(0, end);
    }

    // An element's style inside its parent's: the tag's own (tg's sheet: em bold and upright), its classes, then its inline style.
    private static ChatStyle ElementStyle(ChatStyle parent, string name, string raw)
    {
        ChatStyle style = name switch
        {
            "b" or "strong" => parent with { Bold = true },
            "i" => parent with { Italic = true },
            "em" => parent with { Bold = true, Italic = false },
            _ => parent,
        };
        style = ChatStyles.Apply(style, AttrOf(raw, "class"));
        if (name == "font" && Maptext.MaptextMarkup.TryColor(AttrOf(raw, "color"), out uint fontColour))
            style = style with { Rgba = fontColour };
        string inline = AttrOf(raw, "style");
        if (inline != null)
        {
            foreach (string declaration in inline.Split(';'))
            {
                int colon = declaration.IndexOf(':');
                if (colon < 0)
                    continue;
                string property = declaration.Substring(0, colon).Trim().ToLowerInvariant();
                string value = declaration.Substring(colon + 1).Trim().ToLowerInvariant();
                if (property == "color" && Maptext.MaptextMarkup.TryColor(value, out uint colour))
                    style = style with { Rgba = colour };
                else if (property == "font-weight")
                    style = style with { Bold = value is "bold" or "bolder" or "700" or "800" or "900" };
                else if (property == "font-style")
                    style = style with { Italic = value is "italic" or "oblique" };
            }
        }
        return style;
    }

    // An attribute's value (quoted with ' or ", or bare), or null.
    private static string AttrOf(string raw, string attribute)
    {
        int at = 0;
        while ((at = raw.IndexOf(attribute, at, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            int end = at + attribute.Length;
            bool starts = at > 0 && char.IsWhiteSpace(raw[at - 1]);
            int eq = end;
            while (eq < raw.Length && raw[eq] == ' ') eq++;
            if (!starts || eq >= raw.Length || raw[eq] != '=')
            {
                at = end;
                continue;
            }
            int v = eq + 1;
            while (v < raw.Length && raw[v] == ' ') v++;
            if (v >= raw.Length)
                return null;
            char quote = raw[v];
            if (quote is '\'' or '"')
            {
                int close = raw.IndexOf(quote, v + 1);
                return close < 0 ? raw.Substring(v + 1) : raw.Substring(v + 1, close - v - 1);
            }
            int stop = v;
            while (stop < raw.Length && !char.IsWhiteSpace(raw[stop]) && raw[stop] != '/') stop++;
            return raw.Substring(v, stop - v);
        }
        return null;
    }

    private const char LinkOpen = '\uE000', LinkClose = '\uE001';

    // The marks out of the text, each marked run a link of the next href (an unclosed one runs to the end; an empty one is no link),
    // and the runs of the styles (each from its mark to the next).
    private static string TakeLinks(string marked, List<string> hrefs, List<ChatLink> links, List<ChatStyle> styles, List<ChatSpan> spans)
    {
        var sb = new StringBuilder(marked.Length);
        int next = 0, start = -1;
        int style = 0, styleStart = 0;
        foreach (char ch in marked)
        {
            if (ch >= StyleMark && ch < StyleMark + MaxStyles)
            {
                if (sb.Length > styleStart)
                    spans?.Add(new ChatSpan(styleStart, sb.Length - styleStart, styles[style]));
                style = ch - StyleMark;
                styleStart = sb.Length;
            }
            else if (ch == LinkOpen)
                start = sb.Length;
            else if (ch == LinkClose)
            {
                if (start >= 0 && next < hrefs.Count && sb.Length > start)
                    links?.Add(new ChatLink(start, sb.Length - start, hrefs[next]));
                next++;
                start = -1;
            }
            else
                sb.Append(ch);
        }
        if (start >= 0 && next < hrefs.Count && sb.Length > start)
            links?.Add(new ChatLink(start, sb.Length - start, hrefs[next]));
        if (spans != null && sb.Length > styleStart)
            spans.Add(new ChatSpan(styleStart, sb.Length - styleStart, styles[style]));
        return sb.ToString();
    }

    // The href of an <a> tag when it is a BYOND link the client may send back (protocol.md section 33): byond://? and within the cap.
    private static string HrefOf(string tag)
    {
        int at = tag.IndexOf("href=", StringComparison.OrdinalIgnoreCase);
        if (at < 0 || at + 5 >= tag.Length)
            return null;
        char quote = tag[at + 5];
        string href;
        if (quote != '\'' && quote != '"')
        {
            // M10x slice C: an unquoted value (tg's FOLLOW_LINK and TURF_LINK, __DEFINES/say.dm) runs to a space or the tag's end, as HTML's
            int stop = tag.IndexOfAny(UnquotedEnd, at + 5);
            href = tag.Substring(at + 5, (stop < 0 ? tag.Length : stop) - at - 5);
        }
        else
        {
            int end = tag.IndexOf(quote, at + 6);
            if (end < 0)
                return null;
            href = tag.Substring(at + 6, end - at - 6);
        }
        return IsLink(href) && Encoding.UTF8.GetByteCount(href) <= Limits.MaxTopicHrefBytes ? href : null;
    }

    private static readonly char[] UnquotedEnd = { ' ', '\t', '\n', '\r', '>' };

    /// <summary>The start of a BYOND link's href that the client offers as a link.</summary>
    public const string TopicPrefix = "byond://?";

    /// <summary>DV364: the start of BYOND's client command link (<c>byond://winset?command=vote</c>), which the server runs as the verb.</summary>
    public const string WinsetPrefix = "byond://winset?";

    /// <summary>tg's vote verb as a client command link (the Esc menu's and the lobby's Vote button, DV364).</summary>
    public const string VoteCommand = WinsetPrefix + "command=vote";

    /// <summary>Whether <paramref name="href"/> is a BYOND link the client sends back: a Topic link or a winset command link.</summary>
    public static bool IsLink(string href) => href != null && (href.StartsWith(TopicPrefix, StringComparison.Ordinal) || href.StartsWith(WinsetPrefix, StringComparison.Ordinal));

    private static void NewLine(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[^1] != '\n')
            sb.Append('\n');
    }

    // tg's announcement parts are blocks (chat-dark.scss: .announcement_header is a flex column, the texts have padding and width 100%).
    private static readonly string[] BlockClasses = { "major_announcement_title", "subheader_announcement_text", "major_announcement_text", "minor_announcement_title", "minor_announcement_text" };

    private static bool IsBreak(string tag)
    {
        if (!tag.StartsWith('/') && tag.StartsWith("span", StringComparison.Ordinal) && BlockClasses.Any(c => tag.Contains(c, StringComparison.Ordinal)))
            return true;
        if (tag.StartsWith('/'))
            tag = tag.Substring(1);
        int end = 0;
        while (end < tag.Length && char.IsAsciiLetterOrDigit(tag[end]))
            end++;
        switch (tag.Substring(0, end))
        {
            case "br" or "p" or "div" or "li" or "tr" or "hr" or "h1" or "h2" or "h3" or "h4" or "table" or "ul" or "ol":
                return true;
        }
        return false;
    }

    private static bool TryDecodeEntity(ReadOnlySpan<char> name, out string decoded)
    {
        decoded = null;
        if (name.Length == 0)
            return false;
        if (name[0] == '#')
        {
            bool hex = name.Length > 1 && (name[1] == 'x' || name[1] == 'X');
            ReadOnlySpan<char> digits = name.Slice(hex ? 2 : 1);
            if (digits.Length == 0 || !int.TryParse(digits, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out int code))
                return false;
            if (code <= 0 || code > 0x10FFFF || (code >= 0xD800 && code <= 0xDFFF))
                return false;
            decoded = char.ConvertFromUtf32(code);
            return true;
        }
        switch (name.ToString())
        {
            case "amp": decoded = "&"; return true;
            case "lt": decoded = "<"; return true;
            case "gt": decoded = ">"; return true;
            case "quot": decoded = "\""; return true;
            case "apos": decoded = "'"; return true;
            case "nbsp": decoded = " "; return true;
            case "bull": decoded = "*"; return true;
            case "rdsh": decoded = "\u21B3"; return true; // tg's self-check and analyzer rows
            case "deg": decoded = "\u00B0"; return true;
        }
        return false;
    }
}
