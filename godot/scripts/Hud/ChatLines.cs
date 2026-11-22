using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Hud;

/// <summary>
/// M4a P7 (D128, interaction.md 9): the feedback until M9, one overlay: tg's last chat lines at the lower left (<see cref="ChatFeed"/>), a balloon alert under the crosshair
/// (<see cref="BalloonModel"/>), tg's progress bars at the top middle, labelled, or an item's under its action button (the owner, 2026-10-18: over its target or under the crosshair it covered the mobs and items in the middle of the view; <see cref="ProgressBarLayout"/>), and the screentip line of the hovered thing (its name and the left and right
/// labels the server sent) at the top right, out of the view's middle (the owner, 2026-10-16). The scene fills the fields each frame.
/// </summary>
public partial class ChatLines : Control
{
    public List<ChatLine> Lines { get; set; } = new();

    public string Balloon { get; set; }

    public float BalloonAlpha { get; set; }

    public string Screentip { get; set; }

    /// <summary>How many lines of the stats corner (<see cref="StatsCorner"/>) stand at the top right now (0: it is off): the screentip goes under them.</summary>
    public int StatsLines { get; set; }

    /// <summary>The progress bars placed this frame (<see cref="PlaceBars"/>).</summary>
    public List<PlacedBar> Bars { get; } = new();

    /// <summary>
    /// The size of the chat lines, the screentip, the balloon and the progress bars: twice P7's sizes times the options'
    /// HUD scale (the owner, 2026-10-09), set by the scene every frame.
    /// </summary>
    public float TextScale { get; set; } = 2f;

    /// <summary>The chat lines' own share of <see cref="TextScale"/> (the options' <see cref="PlayOptions.ChatLinesScale"/>), set by the scene every frame.</summary>
    public float ChatScale { get; set; } = 0.5f;

    private int Px(float px) => Math.Max(1, (int)Math.Round(px * TextScale));

    public override void _Ready()
    {
        Name = "Feedback";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private bool _drewSomething;

    // Redraws while there is something to show (and once more when it is gone).
    public override void _Process(double delta)
    {
        bool something = Lines.Count > 0 || !string.IsNullOrEmpty(Balloon) || !string.IsNullOrEmpty(Screentip) || Bars.Count > 0;
        if (something || _drewSomething)
            QueueRedraw();
        _drewSomething = something;
    }

    public override void _Draw()
    {
        Vector2 size = GetViewportRect().Size;
        Vector2 centre = size / 2f;
        Font font = ThemeDB.FallbackFont;
        float k = TextScale, kc = k * ChatScale; // the chat block's own scale (kc); the screentip, the balloon and the bars below keep the HUD's
        // Chat: the newest at the bottom, above the hands' row.
        _links.Clear();
        float y = size.Y - Mathf.Round(size.Y * 0.2f);
        float chatWidth = Math.Max(200f, size.X * 0.55f - 16 * kc);
        int chatSize = Math.Max(1, (int)Math.Round(15 * kc));
        float lineHeight = font.GetHeight(chatSize);
        float ascent = font.GetAscent(chatSize);
        for (int i = Lines.Count - 1; i >= 0; i--)
        {
            ChatLine line = Lines[i];
            // A long line wraps (at the larger size it would run past the width); the block of the newest line ends at y.
            List<Run> runs = Layout(font, line, chatWidth, chatSize, lineHeight, out float height);
            float top = y - height + 20f * kc;
            var linkColour = new Color(0.55f, 0.8f, 1f, line.Alpha);
            var outline = new Color(0, 0, 0, 0.85f * line.Alpha);
            var origin = new Vector2(16 * kc, top);
            foreach (Run run in runs)
            {
                Vector2 at = origin + run.At + new Vector2(0, ascent);
                Font face = FaceOf(font, run.Style);
                Color colour = ToColor(run.Style.Rgba, line.Alpha); // M9: tg's chat colours (Shared/Play/ChatStyles)
                int runSize = SizeOf(chatSize, run.Style);
                DrawStringOutline(face, at, run.Text, HorizontalAlignment.Left, -1, runSize, Math.Max(1, (int)Math.Round(5 * kc)), outline);
                DrawString(face, at, run.Text, HorizontalAlignment.Left, -1, runSize, run.Href != null ? linkColour : colour);
                if (run.Href == null)
                    continue;
                // version 24 (D154): tg's link, underlined; a click on it in cursor mode sends its href (LinkAt)
                float inked = face.GetStringSize(run.Text.TrimEnd(), HorizontalAlignment.Left, -1, chatSize).X;
                DrawLine(at + new Vector2(0, 2 * kc), at + new Vector2(inked, 2 * kc), linkColour, Math.Max(1f, kc));
                _links.Add((new Rect2(origin + run.At, new Vector2(run.Width, lineHeight)), run.Href));
            }
            y = top - 20f * kc;
        }
        if (!string.IsNullOrEmpty(Screentip))
        {
            // The top right, right aligned where the stats corner's lines end (left of tg's alert column), under those lines when they are on.
            float right = size.X - 48 * k;
            int statsSize = Px(11);
            float top = StatsCorner.Top(k) + StatsLines * (font.GetHeight(statsSize) + 2 * k) + (StatsLines > 0 ? 6 * k : 0);
            float width = Math.Min(520 * k, right - 16);
            int tipSize = Px(12); // a little over the stats' 11: smaller than the chat now that it sits in the corner (the owner, 2026-10-16)
            Text(font, new Vector2(right - width, top + font.GetAscent(tipSize)), Screentip, tipSize, new Color(1f, 0.97f, 0.8f, StatsCorner.TextAlpha), width, StatsCorner.TextAlpha, align: HorizontalAlignment.Right);
        }
        if (!string.IsNullOrEmpty(Balloon))
        {
            float width = Math.Min(400 * k, size.X - 32);
            Text(font, new Vector2(centre.X - width / 2, centre.Y + 78 * k), Balloon, Px(17), new Color(1f, 1f, 1f, BalloonAlpha), width, BalloonAlpha, center: true);
        }
        // The bars (ProgressBarLayout, the owner, 2026-10-18): an item's under its action button at the top left, the others in a column at
        // the top edge's middle with a label and the percent above each, out of the view's middle.
        int barText = Px(ProgressBarLayout.FontSize);
        float barAscent = font.GetAscent(barText);
        var outlineColour = new Color(0, 0, 0, 0.85f);
        foreach (PlacedBar bar in Bars)
        {
            var back = new Rect2(bar.Bar.Left, bar.Bar.Top, bar.Bar.Width, bar.Bar.Height);
            float inset = Math.Max(1f, Mathf.Round(Math.Min(2 * k, back.Size.Y / 4)));
            DrawRect(back, new Color(0, 0, 0, 0.75f));
            var inner = back.Grow(-inset);
            DrawRect(new Rect2(inner.Position, new Vector2(inner.Size.X * (float)Math.Clamp(bar.Fraction, 0, 1), inner.Size.Y)), new Color(0.35f, 0.9f, 0.4f));
            DrawRect(back, Colors.White, false, Math.Max(1f, Math.Min(1.5f * k, inset)));
            if (!string.IsNullOrEmpty(bar.Label))
            {
                var at = new Vector2(bar.LabelBox.Left, bar.LabelBox.Top + barAscent);
                DrawStringOutline(font, at, bar.Label, HorizontalAlignment.Left, -1, barText, Px(4), outlineColour);
                DrawString(font, at, bar.Label, HorizontalAlignment.Left, -1, barText, Colors.White);
            }
            if (!string.IsNullOrEmpty(bar.Percent))
            {
                var at = new Vector2(bar.PercentBox.Left, bar.PercentBox.Top + barAscent);
                DrawStringOutline(font, at, bar.Percent, HorizontalAlignment.Left, -1, barText, Px(4), outlineColour);
                DrawString(font, at, bar.Percent, HorizontalAlignment.Left, -1, barText, new Color(0.75f, 1f, 0.8f));
            }
        }
    }

    /// <summary>Places this frame's bars (<see cref="ProgressBarLayout.Place"/>) with this overlay's font at its text scale.</summary>
    public void PlaceBars(IReadOnlyList<ProgressBarInfo> bars, long ownEntity, Func<long, (bool Found, HudRect Rect)> buttonOf)
    {
        Bars.Clear();
        if (bars.Count == 0)
            return;
        Font font = ThemeDB.FallbackFont;
        int size = Px(ProgressBarLayout.FontSize);
        Bars.AddRange(ProgressBarLayout.Place(bars, ownEntity, TextScale, GetViewportRect().Size.X, font.GetHeight(size), buttonOf,
            text => font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X));
    }

    // Version 24 (D154): where the links of the chat stand on the screen now, with their hrefs (the last _Draw's).
    private readonly List<(Rect2 Rect, string Href)> _links = new();

    /// <summary>Version 24: the href of the chat link drawn at <paramref name="point"/> (screen pixels), or null.</summary>
    public string LinkAt(Vector2 point)
    {
        foreach ((Rect2 rect, string href) in _links)
        {
            if (rect.HasPoint(point))
                return href;
        }
        return null;
    }

    private readonly record struct Run(string Text, Vector2 At, float Width, string Href, ChatStyle Style);

    private Font _bold, _italic, _boldItalic;

    // tg's bold and italic chat text: the chat font emboldened or slanted.
    private Font FaceOf(Font font, ChatStyle style)
    {
        if (!style.Bold && !style.Italic)
            return font;
        _bold ??= new FontVariation { BaseFont = font, VariationEmbolden = 0.6f };
        _italic ??= new FontVariation { BaseFont = font, VariationTransform = new Transform2D(new Vector2(1, 0), new Vector2(0.2f, 1), Vector2.Zero) };
        _boldItalic ??= new FontVariation { BaseFont = font, VariationEmbolden = 0.6f, VariationTransform = new Transform2D(new Vector2(1, 0), new Vector2(0.2f, 1), Vector2.Zero) };
        return style.Bold && style.Italic ? _boldItalic : style.Bold ? _bold : _italic;
    }

    private static Color ToColor(uint rgba, float alpha) =>
        new(((rgba >> 24) & 0xFF) / 255f, ((rgba >> 16) & 0xFF) / 255f, ((rgba >> 8) & 0xFF) / 255f, (rgba & 0xFF) / 255f * alpha);

    // A chat line laid out in words: runs of a word and the spaces after it (cut where a link or a style starts or ends and at a line
    // break), wrapped at the width; a run carries its link's href and its style.
    private List<Run> Layout(Font font, ChatLine line, float width, int fontSize, float lineHeight, out float height)
    {
        var runs = new List<Run>();
        string text = line.Text;
        float x = 0, yy = 0;
        int start = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            bool end = i == text.Length;
            bool cut = end || text[i] == '\n' || (i > start && char.IsWhiteSpace(text[i - 1]) && !char.IsWhiteSpace(text[i]))
                || (i > start && HrefAt(line, i) != HrefAt(line, i - 1))
                || (i > start && !end && line.StyleAt(i) != line.StyleAt(i - 1));
            if (!cut)
                continue;
            if (i > start)
            {
                string piece = text.Substring(start, i - start);
                ChatStyle style = line.StyleAt(start);
                Font face = FaceOf(font, style);
                int size = SizeOf(fontSize, style);
                float w = face.GetStringSize(piece, HorizontalAlignment.Left, -1, size).X;
                if (x > 0 && x + face.GetStringSize(piece.TrimEnd(), HorizontalAlignment.Left, -1, size).X > width)
                {
                    x = 0;
                    yy += lineHeight;
                }
                runs.Add(new Run(piece, new Vector2(x, yy), w, HrefAt(line, start), style));
                x += w;
            }
            if (!end && text[i] == '\n')
            {
                x = 0;
                yy += lineHeight;
                start = i + 1;
            }
            else
                start = i;
        }
        height = yy + lineHeight;
        return runs;
    }

    // D319: a smaller style (a translated line's original) is drawn smaller; a header's larger one is not, the lines being one height
    private static int SizeOf(int fontSize, ChatStyle style) => style.Scale < 1f ? Math.Max(1, (int)MathF.Round(fontSize * style.Scale)) : fontSize;

    private static string HrefAt(ChatLine line, int index)
    {
        if (line.Links == null)
            return null;
        foreach (ChatLink link in line.Links)
        {
            if (index >= link.Start && index < link.Start + link.Length)
                return link.Href;
        }
        return null;
    }

    private void Text(Font font, Vector2 position, string text, int fontSize, Color colour, float width, float alpha, bool center = false, HorizontalAlignment? align = null)
    {
        HorizontalAlignment alignment = align ?? (center ? HorizontalAlignment.Center : HorizontalAlignment.Left);
        DrawStringOutline(font, position, text, alignment, width, fontSize, Px(5), new Color(0, 0, 0, 0.85f * alpha));
        DrawString(font, position, text, alignment, width, fontSize, colour);
    }
}
