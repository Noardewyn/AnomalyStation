using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Hud;

/// <summary>
/// M4a P7 (D128, interaction.md 9): the feedback until M9, one overlay: tg's last chat lines at the lower left (<see cref="ChatFeed"/>), a balloon alert under the crosshair
/// (<see cref="BalloonModel"/>), tg's progress bars at the top middle, labelled, or an item's under its action button (the owner, 2026-10-18: over its target or under the crosshair it covered the mobs and items in the middle of the view; <see cref="ProgressBarLayout"/>), and the hovered thing's
/// name with its prompts (a key chip and the server's hint each, <see cref="KeyChips.Prompts"/>) at the crosshair's lower right (ui-overhaul.md section 6; the top right
/// before). The scene fills the fields each frame.
/// </summary>
public partial class ChatLines : Control
{
    public List<ChatLine> Lines { get; set; } = new();

    public string Balloon { get; set; }

    public float BalloonAlpha { get; set; }

    /// <summary>The hovered thing's name over its prompts, or null when nothing is hovered.</summary>
    public string PromptName { get; set; }

    /// <summary>The hovered thing's prompt lines (<see cref="KeyChips.Prompts"/>).</summary>
    public List<PromptLine> Prompts { get; set; } = new();

    /// <summary>The screen the HUD is drawn on, with the options' HUD scale (the prompts' and the balloon's sizes, <see cref="UiTokens.Unit"/>), set by the scene every frame.</summary>
    public HudMetrics Metrics { get; set; } = new(1920, 1080, 1f);

    /// <summary>What the prompts stand beside, screen pixels: the screen's middle (the crosshair), or the free pointer in cursor mode.</summary>
    public Vector2 PromptAnchor { get; set; }

    /// <summary>How far right of <see cref="PromptAnchor"/> what is drawn there reaches (<see cref="Crosshair.Extent"/>, <see cref="KeyChips.PointerReach"/>): the prompts start past it.</summary>
    public float PromptReach { get; set; }

    /// <summary>The prompts end before this, screen pixels from the left (left of the vitals at the right edge and their chips, in the captured view; infinity: no limit), their text cut with an ellipsis.</summary>
    public float PromptRightEdge { get; set; } = float.PositiveInfinity;

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
        bool something = Lines.Count > 0 || !string.IsNullOrEmpty(Balloon) || PromptName != null || Bars.Count > 0;
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
        float chatWidth = Math.Max(200f, size.X / 3f - 16 * kc); // at most a third of the screen: text and boxes wrap there
        int chatSize = Math.Max(1, (int)Math.Round(15 * kc));
        float lineHeight = font.GetHeight(chatSize);
        float ascent = font.GetAscent(chatSize);
        for (int i = Lines.Count - 1; i >= 0; i--)
        {
            ChatLine line = Lines[i];
            // D184: a boxed message in its frame (chat-dark.scss .boxed_message: a padded block, a thin border, a thick left one;
            // an announcement's .chat_alert_*: a thick border all round), as wide as its text
            bool boxed = line.Box != null;
            float padX = boxed ? 12f * kc : 0, padY = boxed ? 8f * kc : 0, gap = boxed ? 6f * kc : 0;
            // A long line wraps (at the larger size it would run past the width); the block of the newest line ends at y.
            List<Run> runs = Layout(font, line, chatWidth - 2 * padX, chatSize, lineHeight, ascent, out float height, out float inkWidth);
            float top = y + 20f * kc - gap - padY - height;
            var linkColour = new Color(0.55f, 0.8f, 1f, line.Alpha);
            var outline = new Color(0, 0, 0, 0.85f * line.Alpha);
            var origin = new Vector2(16 * kc + padX, top);
            if (line.Box is ChatBox box)
            {
                var frame = new Rect2(16 * kc, top - padY, inkWidth + 2 * padX, height + 2 * padY);
                Color border = ToColor(box.Border, line.Alpha);
                float thick = Math.Max(2f, 3f * kc);
                DrawRect(frame, ToColor(box.Bg, 0.9f * line.Alpha));
                if (box.Alert)
                    DrawRect(frame.Grow(-thick / 2), border, false, thick);
                else
                {
                    DrawRect(frame, border, false, Math.Max(1f, kc));
                    DrawRect(new Rect2(frame.Position, new Vector2(thick, frame.Size.Y)), border);
                }
            }
            foreach (Run run in runs)
            {
                Vector2 at = origin + new Vector2(run.At.X, run.Baseline);
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
            y = top - padY - gap - 20f * kc;
        }
        float u = UiTokens.Unit(Metrics);
        if (PromptName != null)
            DrawPrompts(font, u);
        if (!string.IsNullOrEmpty(Balloon))
        {
            HudRect row = KeyChips.BalloonRow(Metrics);
            Text(font, new Vector2(row.Left, centre.Y + UiTokens.BalloonBaseline * u), Balloon, FontPx(UiTokens.BalloonFont, u), new Color(1f, 1f, 1f, BalloonAlpha), row.Width, BalloonAlpha, center: true);
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

    private static int FontPx(float referencePixels, float u) => Math.Max(1, (int)Math.Round(referencePixels * u));

    // The hovered thing's name, then a key chip and the server's hint per line, at the crosshair's lower right (KeyChips.PromptBlock), with the
    // HUD text's opacity (the options' HudTextOpacity, as the screentip had in the corner).
    private void DrawPrompts(Font font, float u)
    {
        float alpha = StatsCorner.TextAlpha;
        int nameSize = FontPx(UiTokens.PromptNameFont, u), textSize = FontPx(UiTokens.PromptFont, u), chipSize = FontPx(UiTokens.ChipFont, u);
        float width = font.GetStringSize(PromptName, HorizontalAlignment.Left, -1, nameSize).X;
        foreach (PromptLine line in Prompts)
            width = Math.Max(width, ChipWidth(font, line.Key, chipSize, u) + UiTokens.PromptChipGap * u + font.GetStringSize(line.Text, HorizontalAlignment.Left, -1, textSize).X);
        HudRect block = KeyChips.PromptBlock(Metrics, Prompts.Count, width, PromptAnchor.X, PromptAnchor.Y, PromptReach, PromptRightEdge);
        float nameLine = UiTokens.PromptNameLine * u;
        string name = KeyChips.Ellipsize(PromptName, block.Width, s => font.GetStringSize(s, HorizontalAlignment.Left, -1, nameSize).X);
        Outlined(font, new Vector2(block.Left, block.Top + (nameLine + font.GetAscent(nameSize) - font.GetDescent(nameSize)) / 2f), name, nameSize, block.Width, alpha, u);
        float y = block.Top + nameLine;
        foreach (PromptLine line in Prompts)
        {
            float chipWidth = ChipWidth(font, line.Key, chipSize, u);
            DrawChip(this, font, new Rect2(block.Left, y, chipWidth, UiTokens.ChipHeight * u), line.Key, chipSize, u, alpha);
            float textLeft = block.Left + chipWidth + UiTokens.PromptChipGap * u;
            float baseline = y + (UiTokens.ChipHeight * u + font.GetAscent(textSize) - font.GetDescent(textSize)) / 2f;
            float room = Math.Max(1f, block.Right - textLeft);
            string text = KeyChips.Ellipsize(line.Text, room, s => font.GetStringSize(s, HorizontalAlignment.Left, -1, textSize).X);
            Outlined(font, new Vector2(textLeft, baseline), text, textSize, room, alpha, u);
            y += UiTokens.PromptLine * u;
        }
    }

    // The prompts' text: white at the HUD text's opacity over a dark outline (UiTokens) that keeps it readable over bright scenery; the outline
    // fades only below half that opacity, so the text stays dark-edged at the default 60 per cent.
    private void Outlined(Font font, Vector2 at, string text, int fontSize, float width, float alpha, float u)
    {
        int outline = Math.Max(1, (int)Math.Round(UiTokens.TextOutlineWidth * u));
        DrawStringOutline(font, at, text, HorizontalAlignment.Left, width, fontSize, outline, Rgba(UiTokens.TextOutline, Math.Min(1f, 2f * alpha)));
        DrawString(font, at, text, HorizontalAlignment.Left, width, fontSize, Rgba(UiTokens.LocalText, alpha));
    }

    private static float ChipWidth(Font font, string text, int fontSize, float u) =>
        font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X + 2 * UiTokens.ChipPadX * u;

    private static StyleBoxFlat _chipStyle;

    /// <summary>A chip (UiTokens): a small rounded box, the local pill's slate with a white rim, and its text centred in it, at <paramref name="alpha"/>.</summary>
    public static void DrawChip(CanvasItem canvas, Font font, Rect2 rect, string text, int fontSize, float u, float alpha)
    {
        _chipStyle ??= new StyleBoxFlat();
        _chipStyle.BgColor = Rgba(UiTokens.ChipBack, alpha);
        _chipStyle.BorderColor = Rgba(UiTokens.ChipRim, alpha);
        _chipStyle.SetBorderWidthAll(Math.Max(1, (int)Math.Round(UiTokens.ChipRimWidth * u)));
        _chipStyle.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.ChipRadius * u)));
        canvas.DrawStyleBox(_chipStyle, rect);
        float baseline = rect.Position.Y + (rect.Size.Y + font.GetAscent(fontSize) - font.GetDescent(fontSize)) / 2f;
        canvas.DrawString(font, new Vector2(rect.Position.X, baseline), text, HorizontalAlignment.Center, rect.Size.X, fontSize, Rgba(UiTokens.LocalText, alpha));
    }

    /// <summary>A token's colour (0xRRGGBBAA) with its alpha times <paramref name="alpha"/>.</summary>
    public static Color Rgba(uint rgba, float alpha = 1f) => ToColor(rgba, alpha);

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

    private readonly record struct Run(string Text, Vector2 At, float Width, string Href, ChatStyle Style, float Baseline = 0);

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
    // break), wrapped at the width; a run carries its link's href, its style and its row's baseline. A row is as tall as its largest
    // text (an OOC line, an announcement's title). inkWidth: the widest row's text.
    private List<Run> Layout(Font font, ChatLine line, float width, int fontSize, float lineHeight, float ascent, out float height, out float inkWidth)
    {
        var runs = new List<Run>();
        string text = line.Text;
        float x = 0, yy = 0, ink = 0;
        int start = 0, rowStart = 0;
        void EndRow()
        {
            float scale = 1f;
            for (int r = rowStart; r < runs.Count; r++)
                scale = Math.Max(scale, runs[r].Style.Scale);
            for (int r = rowStart; r < runs.Count; r++)
                runs[r] = runs[r] with { At = new Vector2(runs[r].At.X, yy), Baseline = yy + ascent * scale };
            yy += lineHeight * scale;
            rowStart = runs.Count;
        }
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
                float inked = face.GetStringSize(piece.TrimEnd(), HorizontalAlignment.Left, -1, size).X;
                if (x > 0 && x + inked > width)
                {
                    EndRow();
                    x = 0;
                }
                runs.Add(new Run(piece, new Vector2(x, yy), w, HrefAt(line, start), style));
                ink = Math.Max(ink, x + inked);
                x += w;
            }
            if (!end && text[i] == '\n')
            {
                EndRow();
                x = 0;
                start = i + 1;
            }
            else
                start = i;
        }
        EndRow();
        height = yy;
        inkWidth = ink;
        return runs;
    }

    // D319: a smaller style (a translated line's original) is drawn smaller; a larger one (an OOC line, an announcement's title) larger, its row taller
    private static int SizeOf(int fontSize, ChatStyle style) => style.Scale != 1f ? Math.Max(1, (int)MathF.Round(fontSize * style.Scale)) : fontSize;

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
