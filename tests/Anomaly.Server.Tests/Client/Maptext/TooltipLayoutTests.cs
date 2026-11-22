using Anomaly.Client.Shared.Maptext;

namespace Anomaly.Server.Tests.Client.Maptext;

/// <summary>
/// Protocol version 19 (tg's tooltips, DV47): tg's tooltip text read with the tooltip page's sheet, its box laid out as tooltip.html lays it
/// out (the theme's colours, the shrink to fit, the 298 pixel cap, the script's two pixels, the paddings and borders), its place by the pointer;
/// and the clip of BYOND maptext to its box (OpenDream's rule) with the split of a word longer than the box.
/// </summary>
public class TooltipLayoutTests
{
    // 5 pixels a character at 8 px (scaled by the size), a line 12 pixels tall at 8 px with a baseline at 10.
    private sealed class FixedMetrics : IMaptextMetrics
    {
        public float Width(in MaptextStyle style, string text) => text.Length * 5f * style.SizePx / 8f;

        public float LineHeight(in MaptextStyle style) => 12f * style.SizePx / 8f;

        public float Ascent(in MaptextStyle style) => 10f * style.SizePx / 8f;
    }

    private static readonly FixedMetrics Metrics = new();

    [Fact]
    public void TheTooltipSheetReadsTgsTitleAndContent()
    {
        TooltipTheme theme = TooltipThemes.Get("default");
        MaptextDocument doc = MaptextMarkup.Parse("<h1>Choking (No O2)</h1><p>You're not getting enough oxygen.<br><b>Force:</b> low</p>", TooltipLayout.BodyStyle(theme), MaptextSheet.Tooltip);
        Assert.Equal(2, doc.Paragraphs.Count);
        MaptextParagraph title = doc.Paragraphs[0], content = doc.Paragraphs[1];

        // h1 { margin: -5px 0 2px 0; font-size: 1.2em; line-height: 1.4 } on the body's bold 12px sans in the theme's colour.
        MaptextStyle h1 = title.Runs[0].Style;
        Assert.Equal("Choking (No O2)", title.Runs[0].Text);
        Assert.Equal((MaptextFont.Sans, true, 1.4f, 0xFFFFFFFFu), (h1.Font, h1.Bold, h1.LineHeight, h1.Rgba));
        Assert.Equal(12f * 1.2f, h1.SizePx, 4);
        Assert.Equal((-5f, 2f), (title.MarginTop, title.MarginBottom));

        // p { margin: 0; line-height: 1.2 }, its <br> a line break and <b> bold.
        Assert.Equal((0f, 0f), (content.MarginTop, content.MarginBottom));
        Assert.Equal(new[] { "You're not getting enough oxygen.", "\n", "Force:", " low" }, content.Runs.Select(r => r.Text).ToArray());
        Assert.All(content.Runs, r => Assert.Equal((12f, 1.2f), (r.Style.SizePx, r.Style.LineHeight)));

        // The map's sheet keeps BYOND's maptext: no margins, h1 and p as plain blocks.
        MaptextDocument map = MaptextMarkup.Parse("<h1>A</h1><p>B</p>");
        Assert.All(map.Paragraphs, p => Assert.Equal((0f, 0f), (p.MarginTop, p.MarginBottom)));
        Assert.Equal((8f, 1f), (map.Paragraphs[0].Runs[0].Style.SizePx, map.Paragraphs[0].Runs[0].Style.LineHeight));
    }

    [Fact]
    public void TheThemesAreTooltipHtmlsColours()
    {
        Assert.Equal(new TooltipTheme("default", 0x1B2967FF, 0x0033CCFF, 0x005CB8FF, 0xFFFFFFFF), TooltipThemes.Get("default"));
        Assert.Equal(new TooltipTheme("midnight", 0x2B2B33FF, 0x2B2B33FF, 0x36363CFF, 0x6087A0FF), TooltipThemes.Get("midnight"));
        Assert.Equal(new TooltipTheme("alien", 0x33165BFF, 0x5A3076FF, 0x6D3A8EFF, 0x25004AFF), TooltipThemes.Get("alien"));
        Assert.Equal(0xFFFFFFFFu, TooltipThemes.Get("wraith").Text); // a theme without a colour keeps the base one
        Assert.Equal(0x15D512FFu, TooltipThemes.Get("hisgrace").Text);
        Assert.Same(TooltipThemes.Default, TooltipThemes.Get("nonsense")); // no class rules: the base look
        Assert.Same(TooltipThemes.Default, TooltipThemes.Get(null));
        Assert.Equal(18, TooltipThemes.Names.Count()); // default, the 8 custom themes and the 9 HUD styles' themes
        Assert.Contains("trasen-knox", TooltipThemes.Names);
    }

    [Fact]
    public void AShortTooltipShrinksToItsTextAndTakesTheScriptsTwoPixels()
    {
        TooltipBox box = TooltipLayout.Place("<h1>Title</h1><p>Body text</p>", "midnight", Metrics);
        // The text unwrapped: "Body text" at 12px is 9 x 7.5 = 67.5 pixels (the title, 5 x 9 = 45, is narrower); two more from the script.
        Assert.Equal(69.5f, box.TextWidth, 3);
        Assert.Equal(69.5f + 2 * (2 + 2 + 8), box.Width, 3);
        Assert.Equal((12f, 12f), (box.TextX, box.TextY));
        // Heights: the title's line 18 x 1.2 x 1.4 = 30.24 starting 5 above the text area, then 2, then the content's line 18 x 1.2 = 21.6.
        Assert.Equal(-5f + 30.24f + 2f + 21.6f, box.Text.Height, 3);
        Assert.Equal(box.Text.Height + 24f, box.Height, 3);
        Assert.Equal(2, box.Text.Lines);
        PlacedText title = box.Text.Items[0], body = box.Text.Items[1];
        Assert.Equal(("Title", 0f), (title.Text, title.X));
        Assert.True(title.Baseline < body.Baseline);
        // The title's baseline: 5 above, then its ascent (18 at 14.4px) and half its line's extra height ((30.24 - 21.6) / 2).
        Assert.Equal(-5f + 18f + (30.24f - 21.6f) / 2f, title.Baseline, 3);
        Assert.Equal("midnight", box.Theme.Name);
    }

    [Fact]
    public void ALongTooltipWrapsAt298PixelsWithinTheBorder()
    {
        string content = string.Join(' ', Enumerable.Repeat("word", 40));
        TooltipBox box = TooltipLayout.Place("<h1>Name</h1><p>" + content + "</p>", "default", Metrics);
        Assert.Equal(TooltipLayout.MaxTextWidth, box.TextWidth); // 298 - 2 x (2 + 8): the script's two pixels are within max-width
        Assert.Equal(278f, box.TextWidth);
        Assert.Equal(298f + 2 * 2, box.Width);
        Assert.True(box.Text.Lines > 3);
        Assert.All(box.Text.Items, item => Assert.True(item.X + item.Width <= 278f + 0.01f));
    }

    [Fact]
    public void ATitleAloneIsAParagraph()
    {
        // tg's show(): a title without a content is "<p>[title]</p>".
        TooltipBox box = TooltipLayout.Place("<p>Resist</p>", "default", Metrics);
        Assert.Equal(1, box.Text.Lines);
        Assert.Equal(21.6f, box.Text.Height, 3);
        Assert.Equal(6 * 7.5f + 2f, box.TextWidth, 3);
    }

    [Fact]
    public void TheBoxSitsUnderThePointerAndStaysOnTheScreen()
    {
        Assert.Equal((100f, 122f), TooltipLayout.Position(100, 100, 50, 30, 800, 600, 22));
        // The bottom edge would leave the screen: above the pointer, as the page snaps it above the tile.
        Assert.Equal((100f, 590f - 30f - 2f), TooltipLayout.Position(100, 590, 50, 30, 800, 600, 22));
        // The right edge would: moved in.
        Assert.Equal((750f, 122f), TooltipLayout.Position(790, 100, 50, 30, 800, 600, 22));
        // Never off the top left.
        Assert.Equal((0f, 0f), TooltipLayout.Position(5, 5, 900, 700, 800, 600, 22));
    }

    // ---- BYOND's maptext clip (protocol version 18) ----

    [Fact]
    public void AZeroBoxIsBYONDsThirtyTwoPixels()
    {
        Assert.Equal((32, 32), (MaptextClip.BoxWidth(0), MaptextClip.BoxHeight(0)));
        Assert.Equal((96, 8), (MaptextClip.BoxWidth(96), MaptextClip.BoxHeight(8)));
        Assert.Equal(32, MaptextClip.BoxWidth(-3));
    }

    [Fact]
    public void AWordLongerThanTheBoxIsSplitWhereTheLineOverflows()
    {
        // OpenDream's word wrap: "1234567890" is 50 pixels in a 32 pixel box: 6 characters (30 pixels) fit the line, the rest goes on.
        MaptextLayoutResult layout = MaptextLayout.Place(MaptextMarkup.Parse("1234567890"), Metrics, 32, 32);
        Assert.Equal(new[] { "123456", "7890" }, layout.Items.Select(i => i.Text).ToArray());
        Assert.Equal(2, layout.Lines);

        // After a word on the line, the long word starts there and breaks where the line is full.
        MaptextLayoutResult after = MaptextLayout.Place(MaptextMarkup.Parse("ab 1234567890"), Metrics, 32, 32);
        Assert.Equal(new[] { "ab 123", "456789", "0" }, after.Items.Select(i => i.Text).ToArray());

        // A word that fits a line still moves to the next line whole.
        MaptextLayoutResult whole = MaptextLayout.Place(MaptextMarkup.Parse("abcd efgh"), Metrics, 32, 32);
        Assert.Equal(new[] { "abcd", "efgh" }, whole.Items.Select(i => i.Text).ToArray());
    }

    [Fact]
    public void TextPastTheBoxIsClippedAsBYONDDrawsIt()
    {
        // Three lines of 12 pixels from the top of a box 20 tall: the first shows whole, the second is cut by the bottom edge, the third is not drawn.
        MaptextLayoutResult layout = MaptextLayout.Place(MaptextMarkup.Parse("<div valign='top'>one<br>two<br>three</div>"), Metrics, 32, 20);
        Assert.Equal(3, layout.Lines);
        List<ClippedText> shown = MaptextClip.Visible(layout, Metrics, 32, 20);
        Assert.Equal(new[] { ("one", false), ("two", true) }, shown.Select(c => (c.Text.Text, c.Cut)).ToArray());

        // Exactly one line tall: the second line starts at the edge and is not drawn.
        Assert.Single(MaptextClip.Visible(layout, Metrics, 32, 12));

        // Across the left and right edges, and wholly past them.
        var style = MaptextStyle.Default;
        var placed = new MaptextLayoutResult();
        placed.Items.Add(new PlacedText("inside", 0, 10, 30, style));
        placed.Items.Add(new PlacedText("right", 28, 10, 25, style));
        placed.Items.Add(new PlacedText("left", -10, 10, 20, style));
        placed.Items.Add(new PlacedText("past", 32, 10, 20, style));
        placed.Items.Add(new PlacedText("before", -30, 10, 30, style));
        placed.Items.Add(new PlacedText("above", 0, -2, 25, style));
        Assert.Equal(new[] { ("inside", false), ("right", true), ("left", true) }, MaptextClip.Visible(placed, Metrics, 32, 32).Select(c => (c.Text.Text, c.Cut)).ToArray());

        // A centred valign pushes the text up past the top when it is taller than the box: the top line is cut.
        MaptextLayoutResult middle = MaptextLayout.Place(MaptextMarkup.Parse("<span valign='middle'>a<br>b<br>c</span>"), Metrics, 32, 20);
        List<ClippedText> mid = MaptextClip.Visible(middle, Metrics, 32, 20);
        Assert.True(mid[0].Cut);
        Assert.Equal(3, mid.Count);
    }
}
