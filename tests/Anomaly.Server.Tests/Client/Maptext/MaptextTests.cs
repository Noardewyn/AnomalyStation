using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Maptext;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Maptext;

/// <summary>HUD maptext (protocol version 18): tg's maptext markup subset, its layout in the box and the collection of a tree's texts.</summary>
public class MaptextTests
{
    // 5 pixels a character at 8 px (scaled by the size), a line 12 pixels tall at 8 px with a baseline at 10.
    private sealed class FixedMetrics : IMaptextMetrics
    {
        public float Width(in MaptextStyle style, string text) => text.Length * 5f * style.SizePx / 8f;

        public float LineHeight(in MaptextStyle style) => 12f * style.SizePx / 8f;

        public float Ascent(in MaptextStyle style) => 10f * style.SizePx / 8f;
    }

    private static string Text(MaptextDocument doc) => string.Concat(doc.Paragraphs.SelectMany(p => p.Runs).Select(r => r.Text));

    [Fact]
    public void TheMaptextMacroIsGrand9KSixPointWithAnOutlineAndWhite()
    {
        MaptextDocument doc = MaptextMarkup.Parse("<span class='maptext'>50</span>");
        MaptextRun run = Assert.Single(Assert.Single(doc.Paragraphs).Runs);
        Assert.Equal("50", run.Text);
        Assert.Equal(MaptextFont.Grand9K, run.Style.Font);
        Assert.Equal(8f, run.Style.SizePx); // 6pt is 8 pixels
        Assert.Equal(1, run.Style.OutlinePx);
        Assert.Equal(0x000000FFu, run.Style.OutlineRgba);
        Assert.Equal(0xFFFFFFFFu, run.Style.Rgba);
    }

    [Fact]
    public void TheTinyUnicodeMacroReadsItsStyleAttribute()
    {
        MaptextDocument doc = MaptextMarkup.Parse("<span style='font-family: \"TinyUnicode\"; font-size: 12pt; line-height: 0.75; -dm-text-outline: 1px black'>7</span>");
        MaptextStyle st = doc.Paragraphs[0].Runs[0].Style;
        Assert.Equal((MaptextFont.TinyUnicode, 16f, 0.75f, 1), (st.Font, st.SizePx, st.LineHeight, st.OutlinePx));
    }

    [Fact]
    public void TheSpaceSuitDisplaysDivIsRightAligned()
    {
        MaptextDocument doc = MaptextMarkup.Parse("<span class='maptext'><div align='right'>85.5%</div></span>");
        Assert.Equal("85.5%", Text(doc));
        Assert.Equal(MaptextAlign.Right, Assert.Single(doc.Paragraphs).Align);
    }

    [Fact]
    public void TgsOtherHudFormsParse()
    {
        // stack.dm:119 and storage_interface.dm:113: a font colour
        MaptextDocument stack = MaptextMarkup.Parse("<span class='maptext'><font color='white'>50</font></span>");
        Assert.Equal(0xFFFFFFFFu, stack.Paragraphs[0].Runs[0].Style.Rgba);
        // the keybind: span text-align right
        Assert.Equal(MaptextAlign.Right, MaptextMarkup.Parse("<span class='maptext'><span style='text-align: right'>Q</span></span>").Paragraphs[0].Align);
        // the blood level: center, valign middle, position relative left 6px, a colour
        MaptextDocument blood = MaptextMarkup.Parse("<span class='maptext'><div align='center' valign='middle' style='position:relative; top:0px; left:6px'><font color='#FFDDDD'>560</font></div></span>");
        Assert.Equal((MaptextAlign.Center, MaptextVAlign.Middle, 6f, 0xFFDDDDFFu), (blood.Paragraphs[0].Align, blood.VAlign, blood.OffsetX, blood.Paragraphs[0].Runs[0].Style.Rgba));
        // the alert's counters
        Assert.Equal(0xFFFF00FFu, MaptextMarkup.Parse("<span class='maptext'><span style='color: yellow'>3x</span></span>").Paragraphs[0].Runs[0].Style.Rgba);
        Assert.True(MaptextMarkup.Parse("<span class='maptext'><b>12</b></span>").Paragraphs[0].Runs[0].Style.Bold);
    }

    [Fact]
    public void ANewlineAndABrBreakALineAndWhitespaceCollapses()
    {
        MaptextDocument doc = MaptextMarkup.Parse("<span class='maptext'>3\n  4/5<br>a   b</span>");
        Assert.Equal(new[] { "3", "\n", "4/5", "\n", "a b" }, doc.Paragraphs.Single().Runs.Select(r => r.Text).ToArray());
    }

    [Fact]
    public void EntitiesAndATextCutOffByTheWireAreHandled()
    {
        Assert.Equal("a&b<c> d", Text(MaptextMarkup.Parse("a&amp;b&lt;c&gt;&nbsp;d").Paragraphs.Count == 0 ? new MaptextDocument() : MaptextMarkup.Parse("a&amp;b&lt;c&gt;&nbsp;d")).Replace(' ', ' '));
        MaptextDocument cut = MaptextMarkup.Parse("<span class='maptext'><div align='right'>12%</div><spa");
        Assert.Equal("12%", Text(cut)); // the half tag is dropped and the open span closes at the end
        Assert.True(MaptextMarkup.Parse(null).IsEmpty);
        Assert.True(MaptextMarkup.Parse("<span class='maptext'></span>").IsEmpty);
        Assert.Equal("x", Text(MaptextMarkup.Parse("<unknown foo=bar>x</unknown>")));
    }

    [Fact]
    public void ColoursAndSizesParse()
    {
        Assert.True(MaptextMarkup.TryColor("#abc", out uint a));
        Assert.Equal(0xAABBCCFFu, a);
        Assert.True(MaptextMarkup.TryColor("Aqua", out uint aqua));
        Assert.Equal(0x00FFFFFFu, aqua);
        Assert.False(MaptextMarkup.TryColor("nonsense", out _));
        Assert.Equal(16f, MaptextMarkup.Parse("<span style='font-size: 16px'>x</span>").Paragraphs[0].Runs[0].Style.SizePx);
        Assert.Equal(16f, MaptextMarkup.Parse("<span style='font-size: 12pt'>x</span>").Paragraphs[0].Runs[0].Style.SizePx);
    }

    [Fact]
    public void ATextSitsAtTheBottomOfTheBoxUnlessItAsksForTheTopAndRightAlignmentSitsAtTheRightEdge()
    {
        var m = new FixedMetrics();
        MaptextLayoutResult left = MaptextLayout.Place(MaptextMarkup.Parse("<span class='maptext'>AB</span>"), m, 32, 32);
        PlacedText l = Assert.Single(left.Items);
        Assert.Equal((0f, 30f, 10f), (l.X, l.Baseline, l.Width)); // BYOND's default: the line's bottom on the box's (its top 32 - 12, the baseline 10 below)
        MaptextLayoutResult top = MaptextLayout.Place(MaptextMarkup.Parse("<div valign=\"top\">AB</div>"), m, 32, 32);
        Assert.Equal(10f, Assert.Single(top.Items).Baseline); // the status display's valign="top": the baseline at the ascent
        // tg's elevator indicator (elev_indicator.dm: maptext_y 20, an 8 x 16 box): a one-line floor number sits on the box's bottom
        PlacedText floor = Assert.Single(MaptextLayout.Place(MaptextMarkup.Parse("<div style='font-family: TinyUnicode; font-size: 12pt'>2</div>"), m, 8, 16).Items);
        Assert.Equal(16f, floor.Baseline - m.Ascent(floor.Style) + m.LineHeight(floor.Style));
        MaptextLayoutResult right = MaptextLayout.Place(MaptextMarkup.Parse("<span class='maptext'><div align='right'>AB</div></span>"), m, 32, 32);
        Assert.Equal(22f, Assert.Single(right.Items).X); // 32 - 10
        MaptextLayoutResult centre = MaptextLayout.Place(MaptextMarkup.Parse("<span class='maptext'><div align='center'>AB</div></span>"), m, 32, 32);
        Assert.Equal(11f, Assert.Single(centre.Items).X);
    }

    [Fact]
    public void WrappingFollowsTheWidth()
    {
        var m = new FixedMetrics();
        // 5 pixels a character: "aaaa" is 20, "aaaa bbbb" is 45 pixels
        MaptextLayoutResult narrow = MaptextLayout.Place(MaptextMarkup.Parse("<span class='maptext'>aaaa bbbb</span>"), m, 32, 64);
        Assert.Equal(new[] { "aaaa", "bbbb" }, narrow.Items.Select(i => i.Text).ToArray());
        Assert.Equal(2, narrow.Lines);
        Assert.Equal(24f, narrow.Height); // two lines of 12
        Assert.Equal(62f, narrow.Items[1].Baseline); // at the box's bottom: the second line's top is 64 - 12, its baseline 10 below
        MaptextLayoutResult wide = MaptextLayout.Place(MaptextMarkup.Parse("<span class='maptext'>aaaa bbbb</span>"), m, 64, 64);
        Assert.Equal("aaaa bbbb", Assert.Single(wide.Items).Text);
        MaptextLayoutResult hard = MaptextLayout.Place(MaptextMarkup.Parse("<span class='maptext'>a\nb</span>"), m, 64, 64);
        Assert.Equal(2, hard.Lines);
    }

    [Fact]
    public void VerticalMiddleAndTheRelativeOffsetMoveTheBlock()
    {
        var m = new FixedMetrics();
        MaptextLayoutResult r = MaptextLayout.Place(
            MaptextMarkup.Parse("<div align='center' valign='middle' style='position:relative; top:0px; left:6px'>A</div>"), m, 32, 32);
        PlacedText p = Assert.Single(r.Items);
        Assert.Equal((32f - 5f) / 2f + 6f, p.X);
        Assert.Equal((32f - 12f) / 2f + 10f, p.Baseline);
    }

    // ---- the collection of a tree's texts ----

    private sealed class Tables : IAppearanceSource
    {
        public readonly List<AppearanceEntry> Entries = new();
        public readonly List<string> Strings = new();

        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry)
        {
            entry = reference.Index >= 1 && reference.Index <= Entries.Count ? Entries[(int)reference.Index - 1] : null;
            return entry != null;
        }

        public string ResolveString(TableRef reference) => reference.Index >= 1 && reference.Index <= Strings.Count ? Strings[(int)reference.Index - 1] : null;

        public string ResolveResource(TableRef reference) => null;

        public TableRef Add(AppearanceEntry e)
        {
            Entries.Add(e);
            return new TableRef((uint)Entries.Count, true);
        }

        public TableRef Text(string text)
        {
            Strings.Add(text);
            return new TableRef((uint)Strings.Count, true);
        }
    }

    [Fact]
    public void ATreesTextsAreCollectedWithTheirOffsets()
    {
        var t = new Tables();
        // the keybind overlay of an action button: its own maptext, shifted by a transform's translation (action_button.dm:178)
        TableRef key = t.Add(new AppearanceEntry { Maptext = t.Text("Q"), Transform = new[] { 1f, 0f, -4f, 0f, 1f, 2f } });
        // the cooldown counter on the button itself: a 32 x 16 box at 4, 2
        var button = new AppearanceEntry { Maptext = t.Text("12"), MaptextWidth = 32, MaptextHeight = 16, MaptextX = 4, MaptextY = 2, PixelX = 3 };
        button.Overlays.Add(key);
        TableRef root = t.Add(button);

        var collector = new MaptextCollector();
        MaptextItem[] items = collector.Collect(t, root);
        Assert.Equal(2, items.Length);
        // version 74: each names its entry and that entry's maptext_x (an animation track of the entry moves it)
        Assert.Equal(new MaptextItem("12", 32, 16, 7f, 2f, root, 4f), items[0]); // the own text first, then the overlay's
        Assert.Equal(new MaptextItem("Q", 32, 32, -1f, 2f, key, 0f), items[1]); // 3 (pixel_x of the root) - 4
        Assert.Same(items, collector.Collect(t, root)); // cached
        Assert.Empty(collector.Collect(t, t.Add(new AppearanceEntry())));
        Assert.Empty(collector.Collect(t, TableRef.None));
    }
}
