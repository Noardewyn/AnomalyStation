namespace Anomaly.Client.Shared.Maptext;

// HUD maptext (protocol version 18, D142): the layout of a parsed maptext in its box. BYOND lays the text out in the box
// (maptext_height), wraps it at maptext_width, aligns each paragraph by its align/text-align and the block by valign (bottom by default). Engine-free: the
// font's measurements come from an IMaptextMetrics (the client reads them from the loaded fonts, the tests use a fixed-width one).
// Positions are in map pixels at scale 1, from the box's top left, y downwards; the painter scales them by the HUD's pixel size.

/// <summary>The measurements of a font at a size (the client's loaded font; a fixed-width stand-in in tests).</summary>
public interface IMaptextMetrics
{
    /// <summary>The width in pixels of <paramref name="text"/> (no newline) in <paramref name="style"/>'s font, size and weight.</summary>
    float Width(in MaptextStyle style, string text);

    /// <summary>The font's natural line height in pixels at the style's size.</summary>
    float LineHeight(in MaptextStyle style);

    /// <summary>The distance from the top of a natural line to the baseline.</summary>
    float Ascent(in MaptextStyle style);
}

/// <summary>A piece of text at a place.</summary>
/// <param name="Text">The text (one line, one style).</param>
/// <param name="X">The left edge in pixels from the box's left.</param>
/// <param name="Baseline">The baseline in pixels from the box's top.</param>
/// <param name="Width">The text's width.</param>
/// <param name="Style">Its style.</param>
public readonly record struct PlacedText(string Text, float X, float Baseline, float Width, MaptextStyle Style);

/// <summary>The placed text of one maptext.</summary>
public sealed class MaptextLayoutResult
{
    public List<PlacedText> Items { get; } = new();

    /// <summary>The number of lines.</summary>
    public int Lines { get; set; }

    /// <summary>The height of the laid out block in pixels.</summary>
    public float Height { get; set; }

    /// <summary>The widest line.</summary>
    public float Width { get; set; }
}

/// <summary>Lays maptext out.</summary>
public static class MaptextLayout
{
    private const int MaxLines = 200;

    /// <summary>Places <paramref name="doc"/> in a box of <paramref name="boxWidth"/> x <paramref name="boxHeight"/> pixels.</summary>
    public static MaptextLayoutResult Place(MaptextDocument doc, IMaptextMetrics metrics, float boxWidth, float boxHeight)
    {
        var result = new MaptextLayoutResult();
        var lines = new List<Line>();
        float after = 0f; // the bottom margin of the paragraph above (protocol version 19's tooltip sheet; 0 on the map)
        bool any = false;
        foreach (MaptextParagraph paragraph in doc.Paragraphs)
        {
            int first = lines.Count;
            LayParagraph(paragraph, metrics, boxWidth, lines);
            if (lines.Count == first) continue;
            lines[first].Before = any ? Collapse(after, paragraph.MarginTop) : paragraph.MarginTop;
            after = paragraph.MarginBottom;
            any = true;
        }

        float total = any ? after : 0f;
        foreach (Line line in lines) total += line.Before + line.Height;
        float top = doc.VAlign switch
        {
            MaptextVAlign.Middle => (boxHeight - total) / 2f,
            MaptextVAlign.Bottom => boxHeight - total,
            _ => 0f,
        };
        top += doc.OffsetY;
        float y = top;
        float widest = 0f;
        foreach (Line line in lines)
        {
            y += line.Before;
            float shift = line.Align switch
            {
                MaptextAlign.Center => (boxWidth - line.Width) / 2f,
                MaptextAlign.Right => boxWidth - line.Width,
                _ => 0f,
            };
            shift += doc.OffsetX;
            widest = Math.Max(widest, line.Width);
            foreach (Piece piece in line.Pieces)
            {
                float baseline = y + piece.Ascent + (line.Height - piece.Natural) / 2f;
                result.Items.Add(new PlacedText(piece.Text, shift + piece.X, baseline, piece.Width, piece.Style));
            }

            y += line.Height;
        }

        result.Lines = lines.Count;
        result.Height = total;
        result.Width = widest;
        return result;
    }

    private sealed class Piece
    {
        public string Text;
        public float X, Width, Ascent, Natural;
        public MaptextStyle Style;
    }

    private sealed class Line
    {
        public MaptextAlign Align;
        public readonly List<Piece> Pieces = new();
        public float Width, Height;
        public float Before; // the space above the line (a paragraph's margin, on its first line)
    }

    // CSS's collapsing of two adjacent vertical margins: the largest positive one plus the most negative one.
    private static float Collapse(float a, float b) => Math.Max(0f, Math.Max(a, b)) + Math.Min(0f, Math.Min(a, b));

    private static void LayParagraph(MaptextParagraph paragraph, IMaptextMetrics metrics, float boxWidth, List<Line> lines)
    {
        Line line = NewLine(paragraph, lines);
        MaptextStyle last = MaptextStyle.Default;
        bool lineHasWord = false;
        float pendingSpace = 0f; // the width of a space seen since the last word, added when another word follows on the line
        MaptextStyle pendingStyle = last;
        foreach (MaptextRun run in paragraph.Runs)
        {
            last = run.Style;
            if (run.Text == "\n")
            {
                EnsureHeight(line, metrics, run.Style);
                line = NewLine(paragraph, lines);
                lineHasWord = false;
                pendingSpace = 0f;
                continue;
            }

            int i = 0;
            string text = run.Text;
            while (i < text.Length)
            {
                if (text[i] == ' ')
                {
                    if (lineHasWord)
                    {
                        pendingSpace += metrics.Width(run.Style, " ");
                        pendingStyle = run.Style;
                    }

                    i++;
                    continue;
                }

                int start = i;
                while (i < text.Length && text[i] != ' ') i++;
                string word = text.Substring(start, i - start);
                float w = metrics.Width(run.Style, word);
                if (boxWidth > 0 && w > boxWidth + 0.01f)
                {
                    // OpenDream's word wrap (MapTextRenderer.WordWrap): a word longer than the whole box is split where the line
                    // overflows, the rest going on to the next lines (a word that fits a line moves to the next line whole, below).
                    if (lineHasWord && pendingSpace > 0f)
                        Append(line, " ", pendingSpace, metrics, pendingStyle);
                    pendingSpace = 0f;
                    int k = 0;
                    while (k < word.Length)
                    {
                        int n = 0;
                        while (k + n < word.Length && line.Width + metrics.Width(run.Style, word.Substring(k, n + 1)) <= boxWidth + 0.01f) n++;
                        if (n == 0)
                        {
                            if (line.Pieces.Count > 0 && lines.Count < MaxLines)
                            {
                                line = NewLine(paragraph, lines);
                                lineHasWord = false;
                                continue;
                            }

                            n = 1; // not one character fits the box: it is drawn and the box's clip cuts it
                        }

                        string part = word.Substring(k, n);
                        Append(line, part, metrics.Width(run.Style, part), metrics, run.Style);
                        lineHasWord = true;
                        k += n;
                        if (k < word.Length)
                        {
                            if (lines.Count >= MaxLines) break;
                            line = NewLine(paragraph, lines);
                            lineHasWord = false;
                        }
                    }

                    continue;
                }
                if (lineHasWord && boxWidth > 0 && line.Width + pendingSpace + w > boxWidth + 0.01f && lines.Count < MaxLines)
                {
                    line = NewLine(paragraph, lines);
                    lineHasWord = false;
                    pendingSpace = 0f;
                }

                if (lineHasWord && pendingSpace > 0f)
                {
                    Append(line, " ", pendingSpace, metrics, pendingStyle);
                    pendingSpace = 0f;
                }

                Append(line, word, w, metrics, run.Style);
                lineHasWord = true;
            }
        }

        EnsureHeight(line, metrics, last);
    }

    private static Line NewLine(MaptextParagraph paragraph, List<Line> lines)
    {
        var line = new Line { Align = paragraph.Align };
        lines.Add(line);
        return line;
    }

    private static void EnsureHeight(Line line, IMaptextMetrics metrics, in MaptextStyle style)
    {
        if (line.Height <= 0f) line.Height = metrics.LineHeight(style) * style.LineHeight;
    }

    private static void Append(Line line, string text, float width, IMaptextMetrics metrics, in MaptextStyle style)
    {
        float natural = metrics.LineHeight(style);
        line.Height = Math.Max(line.Height, natural * style.LineHeight);
        if (line.Pieces.Count > 0)
        {
            Piece prev = line.Pieces[^1];
            if (prev.Style == style)
            {
                prev.Text += text;
                prev.Width += width;
                line.Width += width;
                return;
            }
        }

        line.Pieces.Add(new Piece { Text = text, X = line.Width, Width = width, Ascent = metrics.Ascent(style), Natural = natural, Style = style });
        line.Width += width;
    }
}

/// <summary>A placed text and whether the box's edge cuts it.</summary>
/// <param name="Text">The placed text.</param>
/// <param name="Cut">True when part of it lies outside the box (drawn clipped to the box).</param>
public readonly record struct ClippedText(PlacedText Text, bool Cut);

/// <summary>
/// BYOND clips maptext to its box: OpenDream (the reference for the exact rule) draws a maptext into a render target of exactly
/// maptext_width x maptext_height pixels (a 0 is BYOND's default, 32: <c>DreamViewOverlay</c>, the maptext of an icon) and draws that texture
/// as the icon, so text past the box's right or bottom edge, or above its top, is not shown; a word longer than the box is split
/// (<see cref="MaptextLayout"/>). This decides what is drawn; the client draws inside a clip of the box.
/// </summary>
public static class MaptextClip
{
    /// <summary>The box size BYOND draws for a width or height of 0 (world.icon_size; OpenDream's 32).</summary>
    public const int DefaultBoxSize = 32;

    /// <summary>The box's width in pixels: <paramref name="width"/>, or 32 for 0 and less.</summary>
    public static int BoxWidth(int width) => width > 0 ? width : DefaultBoxSize;

    /// <summary>The box's height in pixels: <paramref name="height"/>, or 32 for 0 and less.</summary>
    public static int BoxHeight(int height) => height > 0 ? height : DefaultBoxSize;

    /// <summary>
    /// The texts of <paramref name="layout"/> that show in a box of <paramref name="boxWidth"/> x <paramref name="boxHeight"/> pixels: a text wholly
    /// outside it is left out, one that crosses an edge is kept as cut. A text's extent is its width and its font's line from the top of the ascent.
    /// </summary>
    public static List<ClippedText> Visible(MaptextLayoutResult layout, IMaptextMetrics metrics, float boxWidth, float boxHeight)
    {
        var shown = new List<ClippedText>();
        foreach (PlacedText placed in layout.Items)
        {
            float top = placed.Baseline - metrics.Ascent(placed.Style);
            float bottom = top + metrics.LineHeight(placed.Style);
            float left = placed.X, right = placed.X + placed.Width;
            if (right <= 0f || left >= boxWidth || bottom <= 0f || top >= boxHeight)
                continue;
            bool cut = left < 0f || right > boxWidth + 0.01f || top < 0f || bottom > boxHeight + 0.01f;
            shown.Add(new ClippedText(placed, cut));
        }

        return shown;
    }
}
