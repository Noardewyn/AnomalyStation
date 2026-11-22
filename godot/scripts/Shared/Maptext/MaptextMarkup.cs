using System.Globalization;
using System.Text;

namespace Anomaly.Client.Shared.Maptext;

// HUD maptext (protocol version 18, D142; docs/ai/specs/protocol.md section 27). BYOND's maptext is a small HTML subset; tg writes it through
// the MAPTEXT* macros of code/__DEFINES/text.dm (a span with class 'maptext', or a style with a font family, a size in pt and a text outline)
// and adds its own tags: font color, div align and valign, span style color and text-align, b, br. This file parses that subset into styled
// paragraphs of runs; MaptextLayout places them in the text box; the Godot side (MaptextPainter) draws them. Engine-free.
//
// What it follows: the tags span, div, p, center, font (color, face), b, strong, i, em, u, s, strike, small, big, br and (as plain
// containers) everything else; the attributes class, style, align, valign, color, face; the style properties color, font-family, font-size,
// font-weight, font-style, text-align, line-height, text-decoration, -dm-text-outline, position:relative with top and left; the classes of
// tg's map skin style sheet (interface/skin.dmf: maptext, center, context, subcontext, small, big, ...); the entities &nbsp; &amp; &lt; &gt;
// &quot; &apos; and numeric ones; a newline in the text as a line break.
// Protocol version 19 reads tg's tooltip text with the same parser and the tooltip page's sheet (MaptextSheet.Tooltip): the body's style
// from the theme (bold 12px sans, the theme's colour), h1 (1.2em, line-height 1.4, margins -5px above and 2px below) and p (line-height
// 1.2); a block's vertical margins reach the layout (MaptextParagraph.MarginTop, MarginBottom).
// What it does not follow: images, tables, lists, links, filters, sub and superscripts, and any style it does not list (ignored).
// A text cut off by the wire's string cap may end inside a tag or with open tags: the open tags close at the end, a half tag is dropped.

/// <summary>The fonts tg's skin and macros name (interface/fonts: Grand9K Pixel, Pixellari, TinyUnicode, Spess Font, VCR OSD Mono).</summary>
public enum MaptextFont : byte
{
    /// <summary>"Grand9K Pixel": the map's default font (skin.dmf), 6pt = 8 pixels.</summary>
    Grand9K = 0,

    /// <summary>"Pixellari": 12pt = 16 pixels (the context tips).</summary>
    Pixellari = 1,

    /// <summary>"TinyUnicode": 12pt (the small sub-tips and the cooldown counters).</summary>
    TinyUnicode = 2,

    /// <summary>"Spess Font": 6pt (whispers).</summary>
    SpessFont = 3,

    /// <summary>"VCR OSD Mono": a variable-size font.</summary>
    VcrOsdMono = 4,

    /// <summary>
    /// Protocol version 19: the browser's sans-serif (tg's tooltip.html: <c>Arial, "Helvetica Neue", Helvetica, sans-serif</c>), the
    /// system's Arial or Helvetica where it has one.
    /// </summary>
    Sans = 5,
}

/// <summary>
/// The style sheet a markup is read with: the map control's (BYOND maptext: <c>interface/skin.dmf</c>'s map style) or, protocol version 19,
/// tg's tooltip page (<c>code/modules/tooltip/tooltip.html</c>: <c>h1 { margin: -5px 0 2px 0; font-size: 1.2em; line-height: 1.4 }</c>,
/// <c>p { margin: 0; line-height: 1.2 }</c>).
/// </summary>
public enum MaptextSheet : byte
{
    Map,
    Tooltip,
}

/// <summary>A paragraph's horizontal alignment.</summary>
public enum MaptextAlign : byte
{
    Left,
    Center,
    Right,
}

/// <summary>Where the text block sits in the box vertically (the <c>valign</c> attribute; BYOND's default is the bottom, a tooltip page's the top).</summary>
public enum MaptextVAlign : byte
{
    Top,
    Middle,
    Bottom,
}

/// <summary>The style of a run of text.</summary>
/// <param name="Rgba">The colour, <c>0xRRGGBBAA</c> (default white).</param>
/// <param name="Font">The font.</param>
/// <param name="SizePx">The font size in pixels (pt / 0.75: 6pt is 8 pixels).</param>
/// <param name="Bold">Bold.</param>
/// <param name="Italic">Italic.</param>
/// <param name="Underline">Underlined.</param>
/// <param name="Strike">Struck through.</param>
/// <param name="OutlinePx">The text outline's width in pixels (<c>-dm-text-outline</c>), 0 for none.</param>
/// <param name="OutlineRgba">The outline's colour.</param>
/// <param name="LineHeight">The line height as a multiple of the font's natural one (<c>line-height</c>; 1 by default).</param>
public readonly record struct MaptextStyle(uint Rgba, MaptextFont Font, float SizePx, bool Bold, bool Italic, bool Underline, bool Strike, int OutlinePx, uint OutlineRgba, float LineHeight)
{
    /// <summary>The map's default: Grand9K Pixel 6pt, white, no outline (the style sheet's <c>.maptext</c> adds the outline).</summary>
    public static MaptextStyle Default => new(0xFFFFFFFF, MaptextFont.Grand9K, 8f, false, false, false, false, 0, 0x000000FF, 1f);
}

/// <summary>A run of text in one style (no newline inside; spaces are single).</summary>
public sealed record MaptextRun(string Text, MaptextStyle Style);

/// <summary>A paragraph: runs and hard line breaks (<see cref="MaptextRun.Text"/> of <c>"\n"</c> alone is a break) with one alignment.</summary>
public sealed class MaptextParagraph
{
    public MaptextAlign Align { get; set; }

    /// <summary>The space above the paragraph in pixels (its block's CSS <c>margin-top</c>; may be negative). Adjacent margins collapse.</summary>
    public float MarginTop { get; set; }

    /// <summary>The space below the paragraph in pixels (its block's CSS <c>margin-bottom</c>).</summary>
    public float MarginBottom { get; set; }

    public List<MaptextRun> Runs { get; } = new();

    public bool IsEmpty => Runs.Count == 0;
}

/// <summary>A parsed maptext.</summary>
public sealed class MaptextDocument
{
    public List<MaptextParagraph> Paragraphs { get; } = new();

    /// <summary>The block's vertical place in the box (the first <c>valign</c> met).</summary>
    public MaptextVAlign VAlign { get; set; }

    /// <summary>The offset of <c>position:relative; left:..; top:..</c> in pixels (the first block that has one).</summary>
    public float OffsetX { get; set; }

    public float OffsetY { get; set; }

    /// <summary>True when there is no text to draw.</summary>
    public bool IsEmpty
    {
        get
        {
            foreach (MaptextParagraph p in Paragraphs)
                foreach (MaptextRun r in p.Runs)
                    if (r.Text.Length > 0) return false;
            return true;
        }
    }
}

/// <summary>Parses BYOND maptext markup.</summary>
public static class MaptextMarkup
{
    private const int MaxDepth = 32;
    private const int MaxRuns = 256;

    /// <summary>Parses <paramref name="markup"/> (null and empty give an empty document).</summary>
    public static MaptextDocument Parse(string markup) => Parse(markup, MaptextStyle.Default, MaptextSheet.Map);

    /// <summary>
    /// Parses <paramref name="markup"/> with <paramref name="root"/> as the style of the text outside any tag (the page's body) and
    /// <paramref name="sheet"/>'s rules for the tags (protocol version 19: a tooltip's text).
    /// </summary>
    public static MaptextDocument Parse(string markup, MaptextStyle root, MaptextSheet sheet)
    {
        // BYOND sets a maptext at the bottom of its box unless a valign says otherwise (tg's stack amounts sit at an item's bottom left in a
        // 32 x 32 box, the elevator indicator's floor number in its screen; the status display asks for valign="top"); a tooltip is a page, from the top.
        var doc = new MaptextDocument { VAlign = sheet == MaptextSheet.Tooltip ? MaptextVAlign.Top : MaptextVAlign.Bottom };
        if (string.IsNullOrEmpty(markup)) return doc;
        var state = new ParseState(doc, root, sheet);
        state.Run(markup);
        return doc;
    }

    private sealed class Frame
    {
        public string Tag;
        public MaptextStyle Style;
        public MaptextAlign? Align;
        public bool Block;
        public float MarginTop, MarginBottom;
    }

    private sealed class ParseState
    {
        private readonly MaptextDocument _doc;
        private readonly List<Frame> _stack = new();
        private MaptextParagraph _paragraph;
        private readonly StringBuilder _text = new();
        private bool _pendingSpace;
        private bool _lineStart = true; // no text yet on the line: leading spaces are dropped
        private bool _sawVAlign, _sawOffset;
        private int _runs;
        private readonly MaptextSheet _sheet;

        public ParseState(MaptextDocument doc, MaptextStyle root, MaptextSheet sheet)
        {
            _doc = doc;
            _sheet = sheet;
            _stack.Add(new Frame { Tag = "", Style = root });
        }

        private Frame Top => _stack[^1];

        public void Run(string s)
        {
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '<')
                {
                    int end = s.IndexOf('>', i + 1);
                    if (end < 0) break; // a half tag (the wire cut the text): dropped
                    Tag(s.Substring(i + 1, end - i - 1));
                    i = end + 1;
                    continue;
                }

                if (c == '&')
                {
                    int semi = s.IndexOf(';', i + 1, Math.Min(10, s.Length - i - 1));
                    if (semi > i && TryEntity(s.AsSpan(i + 1, semi - i - 1), out string ent))
                    {
                        Text(ent, true);
                        i = semi + 1;
                        continue;
                    }
                }

                int next = i;
                while (next < s.Length && s[next] != '<' && s[next] != '&') next++;
                if (next == i) next = i + 1;
                Text(s.Substring(i, next - i), false);
                i = next;
            }

            FlushText();
            _doc.Paragraphs.RemoveAll(p => p.IsEmpty);
        }

        private void Text(string text, bool literal)
        {
            foreach (char ch in text)
            {
                if (ch == '\n')
                {
                    FlushText();
                    LineBreak();
                }
                else if (ch == '\r')
                {
                }
                else if (!literal && (ch == ' ' || ch == '\t'))
                {
                    if (!_lineStart) _pendingSpace = true;
                }
                else
                {
                    if (_pendingSpace)
                    {
                        _text.Append(' ');
                        _pendingSpace = false;
                    }

                    _text.Append(ch);
                    _lineStart = false;
                }
            }
        }

        private void FlushText()
        {
            if (_text.Length == 0) return;
            if (_runs >= MaxRuns)
            {
                _text.Clear();
                return;
            }

            Paragraph().Runs.Add(new MaptextRun(_text.ToString(), Top.Style));
            _runs++;
            _text.Clear();
        }

        private MaptextParagraph Paragraph()
        {
            if (_paragraph != null) return _paragraph;
            MaptextAlign align = MaptextAlign.Left;
            for (int i = _stack.Count - 1; i >= 0; i--)
                if (_stack[i].Align is MaptextAlign a)
                {
                    align = a;
                    break;
                }

            _paragraph = new MaptextParagraph { Align = align };
            for (int i = _stack.Count - 1; i >= 1; i--)
                if (_stack[i].Block)
                {
                    _paragraph.MarginTop = _stack[i].MarginTop;
                    _paragraph.MarginBottom = _stack[i].MarginBottom;
                    break;
                }

            _doc.Paragraphs.Add(_paragraph);
            return _paragraph;
        }

        private void LineBreak()
        {
            if (_runs >= MaxRuns) return;
            Paragraph().Runs.Add(new MaptextRun("\n", Top.Style));
            _runs++;
            _pendingSpace = false;
            _lineStart = true;
        }

        private void EndParagraph()
        {
            FlushText();
            _paragraph = null;
            _pendingSpace = false;
            _lineStart = true;
        }

        private void Tag(string body)
        {
            body = body.Trim();
            if (body.Length == 0) return;
            if (body[0] == '/')
            {
                Close(body.Substring(1).Trim().ToLowerInvariant());
                return;
            }

            bool selfClosing = body.EndsWith('/');
            if (selfClosing) body = body.Substring(0, body.Length - 1);
            int sp = 0;
            while (sp < body.Length && !char.IsWhiteSpace(body[sp])) sp++;
            string name = body.Substring(0, sp).ToLowerInvariant();
            Dictionary<string, string> attrs = Attributes(body.Substring(sp));
            if (name == "br")
            {
                FlushText();
                LineBreak();
                return;
            }

            bool block = name is "div" or "p" or "center" or "h1" or "h2" or "h3" or "h4" or "ul" or "ol" or "li" or "table" or "tr";
            if (block) EndParagraph();
            else FlushText();
            if (_stack.Count >= MaxDepth) return;

            var frame = new Frame { Tag = name, Style = Top.Style, Block = block };
            Apply(frame, name, attrs);
            _stack.Add(frame);
            if (selfClosing || name is "img" or "hr") Close(name);
        }

        private void Close(string name)
        {
            int at = -1;
            for (int i = _stack.Count - 1; i >= 1; i--)
                if (_stack[i].Tag == name)
                {
                    at = i;
                    break;
                }

            if (at < 0) return;
            if (_stack[at].Block) EndParagraph();
            else FlushText();
            _stack.RemoveRange(at, _stack.Count - at);
        }

        private void Apply(Frame frame, string tag, Dictionary<string, string> attrs)
        {
            MaptextStyle st = frame.Style;
            switch (tag)
            {
                case "b" or "strong":
                    st = st with { Bold = true };
                    break;
                case "i" or "em":
                    st = st with { Italic = true };
                    break;
                case "u":
                    st = st with { Underline = true };
                    break;
                case "s" or "strike":
                    st = st with { Strike = true };
                    break;
                case "center":
                    frame.Align = MaptextAlign.Center;
                    break;
                case "small":
                    st = st with { SizePx = Math.Max(4f, st.SizePx * 0.8f) };
                    break;
                case "big":
                    st = st with { SizePx = st.SizePx * 1.25f };
                    break;
            }

            if (_sheet == MaptextSheet.Tooltip)
            {
                // tg: code/modules/tooltip/tooltip.html (the page's style sheet)
                switch (tag)
                {
                    case "h1": // h1 { margin: -5px 0 2px 0; font-size: 1.2em; line-height: 1.4; }
                        st = st with { SizePx = st.SizePx * 1.2f, LineHeight = 1.4f };
                        frame.MarginTop = -5f;
                        frame.MarginBottom = 2f;
                        break;
                    case "p": // p { margin: 0; line-height: 1.2; }
                        st = st with { LineHeight = 1.2f };
                        break;
                }
            }

            if (attrs.TryGetValue("class", out string classes))
                foreach (string cls in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    ApplyClass(ref st, frame, cls);
            if (attrs.TryGetValue("color", out string color) && TryColor(color, out uint rgba))
                st = st with { Rgba = rgba };
            if (attrs.TryGetValue("face", out string face) && TryFont(face, out MaptextFont f))
                st = st with { Font = f };
            if (attrs.TryGetValue("align", out string align) && TryAlign(align, out MaptextAlign al))
                frame.Align = al;
            if (attrs.TryGetValue("valign", out string valign) && !_sawVAlign)
            {
                string v = valign.Trim().ToLowerInvariant();
                if (v == "middle" || v == "center") _doc.VAlign = MaptextVAlign.Middle;
                else if (v == "bottom") _doc.VAlign = MaptextVAlign.Bottom;
                else if (v == "top") _doc.VAlign = MaptextVAlign.Top;
                _sawVAlign = true;
            }

            if (attrs.TryGetValue("style", out string style))
                ApplyStyle(ref st, frame, style);
            frame.Style = st;
        }

        // The rules of the map control's style sheet (interface/skin.dmf, the map element's style).
        private static void ApplyClass(ref MaptextStyle st, Frame frame, string cls)
        {
            switch (cls)
            {
                case "maptext":
                    st = st with { Font = MaptextFont.Grand9K, SizePx = 8f, OutlinePx = 1, OutlineRgba = 0x000000FF, Rgba = 0xFFFFFFFF, LineHeight = 1f };
                    break;
                case "center":
                    frame.Align = MaptextAlign.Center;
                    break;
                case "context":
                    st = st with { Font = MaptextFont.Pixellari, SizePx = 16f, OutlinePx = 1, OutlineRgba = 0x000000FF };
                    break;
                case "subcontext":
                    st = st with { Font = MaptextFont.TinyUnicode, SizePx = 16f, LineHeight = 0.75f };
                    break;
                case "small":
                    st = st with { Font = MaptextFont.SpessFont, SizePx = 8f, LineHeight = 1.4f };
                    break;
                case "italics":
                    st = st with { Font = MaptextFont.SpessFont, SizePx = 8f, LineHeight = 1.4f };
                    break;
                case "big":
                    st = st with { Font = MaptextFont.Pixellari, SizePx = 16f };
                    break;
                case "reallybig" or "extremelybig":
                    st = st with { SizePx = 16f };
                    break;
                case "greentext":
                    st = st with { Rgba = 0x00FF00FF, SizePx = 8f };
                    break;
                case "redtext":
                    st = st with { Rgba = 0xFF0000FF, SizePx = 8f };
                    break;
                case "clown":
                    st = st with { Rgba = 0xFF69BFFF, Bold = true };
                    break;
                case "his_grace":
                    st = st with { Rgba = 0x15D512FF };
                    break;
                case "hypnophrase":
                    st = st with { Rgba = 0x0D0D0DFF, Bold = true };
                    break;
                case "yell" or "command_headset":
                    st = st with { Bold = true };
                    break;
            }
        }

        private void ApplyStyle(ref MaptextStyle st, Frame frame, string css)
        {
            float left = 0, top = 0;
            bool relative = false, hasOffset = false;
            foreach (string decl in css.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = decl.IndexOf(':');
                if (colon <= 0) continue;
                string prop = decl.Substring(0, colon).Trim().ToLowerInvariant();
                string value = decl.Substring(colon + 1).Trim();
                switch (prop)
                {
                    case "color":
                        if (TryColor(value, out uint rgba)) st = st with { Rgba = rgba };
                        break;
                    case "font-family":
                        if (TryFont(value, out MaptextFont f)) st = st with { Font = f };
                        break;
                    case "font-size":
                        if (TrySize(value, st.SizePx, out float px)) st = st with { SizePx = px };
                        break;
                    case "font-weight":
                        st = st with { Bold = value.Equals("bold", StringComparison.OrdinalIgnoreCase) || value.Equals("bolder", StringComparison.OrdinalIgnoreCase) || (int.TryParse(value, out int w) && w >= 600) };
                        break;
                    case "font-style":
                        st = st with { Italic = value.Equals("italic", StringComparison.OrdinalIgnoreCase) || value.Equals("oblique", StringComparison.OrdinalIgnoreCase) };
                        break;
                    case "text-align":
                        if (TryAlign(value, out MaptextAlign al)) frame.Align = al;
                        break;
                    case "line-height":
                        if (TryNumber(value, out float lh) && lh > 0f && lh < 10f) st = st with { LineHeight = lh };
                        break;
                    case "text-decoration":
                        string d = value.ToLowerInvariant();
                        st = st with { Underline = d.Contains("underline"), Strike = d.Contains("line-through") };
                        break;
                    case "-dm-text-outline":
                        ApplyOutline(ref st, value);
                        break;
                    case "position":
                        relative = value.Equals("relative", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "left":
                        if (TryPixels(value, out float l)) { left = l; hasOffset = true; }
                        break;
                    case "top":
                        if (TryPixels(value, out float t)) { top = t; hasOffset = true; }
                        break;
                }
            }

            if (relative && hasOffset && !_sawOffset)
            {
                _doc.OffsetX = left;
                _doc.OffsetY = top;
                _sawOffset = true;
            }
        }
    }

    private static void ApplyOutline(ref MaptextStyle st, string value)
    {
        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            st = st with { OutlinePx = 0 };
            return;
        }

        int px = 1;
        uint color = 0x000000FF;
        foreach (string p in parts)
        {
            if (TryPixels(p, out float width)) px = Math.Max(0, (int)MathF.Round(width));
            else if (TryColor(p, out uint rgba)) color = rgba;
        }

        st = st with { OutlinePx = px, OutlineRgba = color };
    }

    private static Dictionary<string, string> Attributes(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            int nameStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '=') i++;
            if (i == nameStart) break;
            string name = text.Substring(nameStart, i - nameStart).ToLowerInvariant();
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            string value = string.Empty;
            if (i < text.Length && text[i] == '=')
            {
                i++;
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                if (i < text.Length && (text[i] == '\'' || text[i] == '"'))
                {
                    char quote = text[i++];
                    int start = i;
                    while (i < text.Length && text[i] != quote) i++;
                    value = text.Substring(start, i - start);
                    if (i < text.Length) i++;
                }
                else
                {
                    int start = i;
                    while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                    value = text.Substring(start, i - start);
                }
            }

            map[name] = value;
        }

        return map;
    }

    private static readonly Dictionary<string, uint> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["white"] = 0xFFFFFFFF, ["black"] = 0x000000FF, ["red"] = 0xFF0000FF, ["green"] = 0x008000FF, ["blue"] = 0x0000FFFF,
        ["yellow"] = 0xFFFF00FF, ["aqua"] = 0x00FFFFFF, ["cyan"] = 0x00FFFFFF, ["fuchsia"] = 0xFF00FFFF, ["magenta"] = 0xFF00FFFF,
        ["lime"] = 0x00FF00FF, ["maroon"] = 0x800000FF, ["navy"] = 0x000080FF, ["olive"] = 0x808000FF, ["purple"] = 0x800080FF,
        ["silver"] = 0xC0C0C0FF, ["gray"] = 0x808080FF, ["grey"] = 0x808080FF, ["teal"] = 0x008080FF, ["orange"] = 0xFFA500FF,
        ["pink"] = 0xFFC0CBFF, ["brown"] = 0xA52A2AFF, ["gold"] = 0xFFD700FF,
    };

    /// <summary>A colour as <c>0xRRGGBBAA</c>: <c>#rgb</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c> or an HTML colour name.</summary>
    public static bool TryColor(string text, out uint rgba)
    {
        rgba = 0xFFFFFFFF;
        text = text?.Trim() ?? string.Empty;
        if (text.Length == 0) return false;
        if (text[0] == '#')
        {
            string hex = text.Substring(1);
            if (hex.Length == 3)
                hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
            if (hex.Length == 6) hex += "FF";
            if (hex.Length != 8 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgba)) return false;
            return true;
        }

        return Named.TryGetValue(text, out rgba);
    }

    private static bool TryFont(string text, out MaptextFont font)
    {
        font = MaptextFont.Grand9K;
        foreach (string part in (text ?? string.Empty).Split(','))
        {
            string name = part.Trim().Trim('\'', '"').Replace(" ", string.Empty).ToLowerInvariant();
            switch (name)
            {
                case "grand9kpixel" or "grand9k":
                    font = MaptextFont.Grand9K;
                    return true;
                case "pixellari":
                    font = MaptextFont.Pixellari;
                    return true;
                case "tinyunicode":
                    font = MaptextFont.TinyUnicode;
                    return true;
                case "spessfont":
                    font = MaptextFont.SpessFont;
                    return true;
                case "vcrosdmono":
                    font = MaptextFont.VcrOsdMono;
                    return true;
                case "arial" or "helveticaneue" or "helvetica" or "sans-serif" or "verdana":
                    font = MaptextFont.Sans;
                    return true;
            }
        }

        return false;
    }

    private static bool TryAlign(string text, out MaptextAlign align)
    {
        switch ((text ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "left":
                align = MaptextAlign.Left;
                return true;
            case "center" or "middle":
                align = MaptextAlign.Center;
                return true;
            case "right":
                align = MaptextAlign.Right;
                return true;
        }

        align = MaptextAlign.Left;
        return false;
    }

    private static bool TryNumber(string text, out float value)
        => float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);

    private static bool TryPixels(string text, out float value)
    {
        text = text.Trim();
        if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase)) text = text.Substring(0, text.Length - 2);
        return TryNumber(text, out value);
    }

    // font-size: pt (BYOND's unit; px = pt / 0.75), px, em and % of the parent's, a bare number as pt, and the CSS keywords.
    private static bool TrySize(string text, float parentPx, out float px)
    {
        px = parentPx;
        text = text.Trim().ToLowerInvariant();
        switch (text)
        {
            case "xx-small": px = 6f; return true;
            case "x-small": px = 7f; return true;
            case "small": px = 8f; return true;
            case "medium": px = 11f; return true;
            case "large": px = 16f; return true;
            case "x-large": px = 21f; return true;
            case "xx-large": px = 28f; return true;
        }

        float number;
        if (text.EndsWith("pt")) { if (!TryNumber(text[..^2], out number)) return false; px = number / 0.75f; }
        else if (text.EndsWith("px")) { if (!TryNumber(text[..^2], out number)) return false; px = number; }
        else if (text.EndsWith("em")) { if (!TryNumber(text[..^2], out number)) return false; px = parentPx * number; }
        else if (text.EndsWith('%')) { if (!TryNumber(text[..^1], out number)) return false; px = parentPx * number / 100f; }
        else if (TryNumber(text, out number)) px = number / 0.75f;
        else return false;
        px = Math.Clamp(px, 2f, 128f);
        return true;
    }

    private static bool TryEntity(ReadOnlySpan<char> name, out string text)
    {
        text = null;
        switch (name.ToString())
        {
            case "nbsp": text = "\u00A0"; return true;
            case "amp": text = "&"; return true;
            case "lt": text = "<"; return true;
            case "gt": text = ">"; return true;
            case "quot": text = "\""; return true;
            case "apos": text = "'"; return true;
        }

        if (name.Length > 1 && name[0] == '#')
        {
            bool hex = name[1] == 'x' || name[1] == 'X';
            ReadOnlySpan<char> digits = name.Slice(hex ? 2 : 1);
            if (digits.Length > 0 && int.TryParse(digits, hex ? NumberStyles.HexNumber : NumberStyles.None, CultureInfo.InvariantCulture, out int code)
                && code > 0 && code <= 0x10FFFF && (code < 0xD800 || code > 0xDFFF))
            {
                text = char.ConvertFromUtf32(code);
                return true;
            }
        }

        return false;
    }
}
