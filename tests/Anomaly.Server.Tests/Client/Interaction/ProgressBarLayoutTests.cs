using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Interaction;

/// <summary>
/// The progress bars as the owner asked (2026-10-18): each labelled with what it is for and a little larger, the percent never over the
/// label or another bar's text, and an item's bar (version 27's holder) under that item's action button at the top left.
/// </summary>
public class ProgressBarLayoutTests
{
    private const long Me = 50, Tank = 900, Airlock = 77;
    private const float K = 2f; // 1080p's text scale
    private const float Line = 30f;

    // A font of 7 pixels a character at the bar's size.
    private static float Measure(string text) => text.Length * 7f;

    private static (bool, HudRect) NoButton(long id) => (false, default);

    private static (bool, HudRect) TankButton(long id) => id == Tank ? (true, new HudRect(12, 12, 64, 64)) : (false, default);

    private static bool Overlap(HudRect a, HudRect b)
        => a.Width > 0 && b.Width > 0 && a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    [Theory]
    [InlineData(Airlock, "airlock", "Airlock")]
    [InlineData(Tank, "oxygen tank", "Oxygen tank")]
    [InlineData(Me, "Urist McTest", "Action")] // a bar on the player's own mob: getting up, a do_after on oneself
    [InlineData(0, null, "Action")]
    [InlineData(5, "  ", "Action")]
    public void A_bar_is_labelled_with_what_it_hangs_on(long holder, string name, string label)
        => Assert.Equal(label, ProgressBars.Label(new ProgressBarInfo(1, 0, 0.5, holder, name), Me));

    [Theory]
    [InlineData(0.0, "0%")]
    [InlineData(0.454, "45%")]
    [InlineData(1.0, "100%")]
    [InlineData(1.7, "100%")]
    public void The_percent_is_a_whole_number(double fraction, string text) => Assert.Equal(text, ProgressBars.Percent(fraction));

    [Fact]
    public void The_column_bars_are_larger_labelled_and_their_texts_never_touch()
    {
        var bars = new List<ProgressBarInfo>
        {
            new(1, Airlock, 0.45, Airlock, "airlock"),
            new(2, 0, 1.0, Me, "Urist McTest"),
            new(3, 0, 0.05, 4, "a very long name of a machine that does not fit over the bar at all"),
        };
        List<PlacedBar> placed = ProgressBarLayout.Place(bars, Me, K, 1920, Line, NoButton, Measure);
        Assert.Equal(3, placed.Count);
        foreach (PlacedBar bar in placed)
        {
            Assert.Equal(ProgressBarLayout.Width * K, bar.Bar.Width); // 200 px at 1080p, was 136
            Assert.Equal(ProgressBarLayout.Height * K, bar.Bar.Height); // 28 px, was 24
            Assert.Equal(960, bar.Bar.Left + bar.Bar.Width / 2, 3); // the top edge's middle
            Assert.False(string.IsNullOrEmpty(bar.Label));
            Assert.NotNull(bar.Percent);
            Assert.True(Measure(bar.Label) <= bar.LabelBox.Width, bar.Label);
            Assert.True(bar.LabelBox.Right < bar.PercentBox.Left, "the label stops short of the percent");
            Assert.Equal(bar.Bar.Right, bar.PercentBox.Right, 3);
            Assert.True(bar.PercentBox.Bottom <= bar.Bar.Top, "the texts stand over the bar");
        }
        Assert.Equal(new[] { "Airlock", "Action" }, placed.Take(2).Select(b => b.Label));
        Assert.EndsWith("…", placed[2].Label);
        Assert.Equal(new[] { "45%", "100%", "5%" }, placed.Select(b => b.Percent));
        // nothing of one bar touches anything of another (the overlapping percent digits of the play test)
        var boxes = placed.Select(b => new[] { b.Bar, b.LabelBox, b.PercentBox }).ToList();
        for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
                foreach (HudRect a in boxes[i])
                    foreach (HudRect b in boxes[j])
                        Assert.False(Overlap(a, b), $"bar {i} {a} and bar {j} {b}");
    }

    [Fact]
    public void An_items_bar_goes_under_its_action_button_and_the_rest_stay_in_the_column()
    {
        var bars = new List<ProgressBarInfo>
        {
            new(1, 0, 0.8, Tank, "oxygen tank"), // the internals' volume bar: on the tank on the back
            new(2, Airlock, 0.3, Airlock, "airlock"),
            new(3, 0, 0.6, Tank, "oxygen tank"), // a second bar on the same tank goes under the first
        };
        List<PlacedBar> placed = ProgressBarLayout.Place(bars, Me, K, 1920, Line, TankButton, Measure);
        PlacedBar first = placed[0], weld = placed[1], second = placed[2];
        Assert.Null(first.Label); // the button shows the item
        Assert.Null(first.Percent);
        Assert.True(first.Bar.Top > 12 + 64, "under the button");
        Assert.True(first.Bar.Left >= 12 && first.Bar.Right <= 12 + 64, "within the button's width");
        Assert.Equal(ProgressBarLayout.ItemHeight * K, first.Bar.Height);
        Assert.True(second.Bar.Top >= first.Bar.Bottom, "stacked under the first");
        Assert.Equal("Airlock", weld.Label);
        Assert.Equal(960, weld.Bar.Left + weld.Bar.Width / 2, 3);
        Assert.Equal(24 * K + Line + ProgressBarLayout.TextGap * K, weld.Bar.Top, 3); // the column's first row: the item bars take none of it
    }

    [Fact]
    public void A_bar_on_an_item_without_a_button_or_on_the_own_mob_stays_in_the_column()
    {
        var bars = new List<ProgressBarInfo> { new(1, 0, 0.5, 901, "emergency oxygen tank"), new(2, 0, 0.5, Me, "Urist McTest") };
        // a button that would name the own mob is never used: the bar on oneself is no item's
        List<PlacedBar> placed = ProgressBarLayout.Place(bars, Me, K, 1920, Line, id => id == Me ? (true, new HudRect(0, 0, 64, 64)) : (false, default), Measure);
        Assert.Equal("Emergency oxygen tank", placed[0].Label);
        Assert.Equal("Action", placed[1].Label);
        Assert.All(placed, b => Assert.NotNull(b.Percent));
    }
}
