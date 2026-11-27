using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 13: the round's chat window at its default place stands clear above the bottom row (the hands' row and the hint strip) at
/// every screen and HUD scale; it is see-through while the player is not typing; its action and input rows show only while the player types; and the
/// chat lines keep clear of the bottom row.
/// </summary>
public class ChatPlaceTests
{
    // the screens and scales of the HUD's overlap theory (LiveHudTests.HandScreens): 16:9, 16:10, 4:3, 5:4 and 21:9, 50 to 300 % in steps of 5
    private static readonly (int W, int H)[] Sizes =
    {
        (1280, 720), (1920, 1080), (2560, 1440), (1024, 600), (1024, 768), (1280, 1024), (1440, 900), (1920, 1200), (2560, 1080), (3440, 1440), (3840, 2160),
    };

    public static IEnumerable<object[]> Screens()
    {
        foreach ((int w, int h) in Sizes)
        {
            for (int percent = 50; percent <= 300; percent += 5)
                yield return new object[] { w, h, percent / 100f };
        }
    }

    private static float WindowScale(float height) => new PlayOptions().ChatWindowScaleFor(height);

    private static bool Over(HudRect a, HudRect b) => a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    private static bool Whole(HudRect r) => r.Left == MathF.Round(r.Left) && r.Top == MathF.Round(r.Top) && r.Width == MathF.Round(r.Width) && r.Height == MathF.Round(r.Height);

    [Theory]
    [MemberData(nameof(Screens))]
    public void TheWindowsDefaultPlaceIsOnTheScreenAndStandsAboveTheBottomRowClearOfIt(int width, int height, float scale)
    {
        var metrics = new HudMetrics(width, height, scale);
        HudRect window = ChatPlace.RoundDefault(metrics, WindowScale(height));
        Assert.True(Whole(window), $"{window} is at whole pixels");
        Assert.True(window.Left >= 0 && window.Top >= 0 && window.Right <= width && window.Bottom <= height, $"{window} is on the {width}x{height} screen");
        Assert.True(window.Height > 0 && window.Width > 0);
        float gap = MathF.Round(UiTokens.ChatRowGap * UiTokens.Unit(metrics));
        HandsLayout row = LiveHud.Hands(metrics, true, true);
        HudRect strip = LiveHud.HintSpan(metrics, row);
        bool underRow = false;
        foreach (HudRect part in new[] { row.Left, row.Hotbar, row.Right, strip })
        {
            if (window.Left >= part.Right || part.Left >= window.Right)
                continue;
            underRow = true;
            Assert.True(window.Bottom <= part.Top - gap, $"{window} ends {gap} above {part}");
        }
        // what really stands there, with either hand active and the strip of any width the row lets it have, under Tab or not
        LiveHudLayout column = LiveHud.Layout(metrics, human: true);
        foreach ((bool leftActive, bool rightActive) in new[] { (true, false), (false, true), (false, false) })
        foreach (bool tab in new[] { false, true })
        foreach (float hint in new[] { 0f, 100f, 300f, 600f, 2000f })
        {
            HandsLayout hands = LiveHud.Hands(metrics, leftActive, rightActive, column, hint * UiTokens.Unit(metrics), tab: tab);
            var drawn = new List<HudRect> { hands.Left, hands.Right, hands.Hotbar, hands.Hint };
            drawn.AddRange(Enumerable.Range(1, HotbarMessage.CellCount).Select(c => LiveHud.BarCell(hands, c)));
            foreach (HudRect part in drawn.Where(p => p.Width > 0))
                Assert.False(Over(window, part), $"{window} is clear of {part}");
        }
        if (underRow)
        {
            // just ChatRowGap above what stands under it, unless made lower for the screen's top
            float top = new[] { row.Left, row.Hotbar, row.Right, strip }.Where(p => window.Left < p.Right && p.Left < window.Right).Min(p => p.Top);
            Assert.Equal(top - gap, window.Bottom);
        }
    }

    [Theory]
    [InlineData(1920, 1080, 1f, 12, 562, 749, 384)] // the window's least height (240 at the chat window's scale 1.6) over its share's 345
    [InlineData(1920, 1080, 1.25f, 12, 528, 749, 384)]
    [InlineData(1280, 720, 1f, 8, 375, 499, 256)]
    public void TheWindowsDefaultPlaceAtCommonScreens(int width, int height, float scale, float left, float top, float w, float h)
        => Assert.Equal(new HudRect(left, top, w, h), ChatPlace.RoundDefault(new HudMetrics(width, height, scale), WindowScale(height)));

    [Fact]
    public void WhereNoPartOfTheBottomRowIsUnderItTheWindowKeepsTodaysPlace()
    {
        // 3440x1440 at 100 %: the window (at most 0.39 of the width, from the left) ends left of the hands' row
        var metrics = new HudMetrics(3440, 1440, 1f);
        HudRect window = ChatPlace.RoundDefault(metrics, WindowScale(1440));
        Assert.Null(ChatPlace.BottomClear(metrics, window.Left, window.Right));
        Assert.Equal(MathF.Round(ChatPlace.RoundWidth * 3440), window.Width);
        Assert.Equal(MathF.Round(ChatPlace.RoundTop * 1440), window.Top); // its share's top, as before
        Assert.Equal(MathF.Round(ChatPlace.MinHeight * WindowScale(1440)), window.Height); // its least height at the window's scale, over its share's
    }

    [Fact]
    public void AWindowTooTallForTheRoomAboveTheRowIsMadeLowerNotPushedOffTheTop()
    {
        // a window scale so large that its least height is more than the screen above the row
        var metrics = new HudMetrics(1920, 1080, 1f);
        HudRect window = ChatPlace.RoundDefault(metrics, 10f);
        Assert.Equal(0f, window.Top);
        Assert.Equal(ChatPlace.BottomClear(metrics, window.Left, window.Right), window.Bottom);
    }

    [Fact]
    public void SavedGeometryWinsOverTheDefault()
    {
        HudRect fallback = ChatPlace.RoundDefault(new HudMetrics(1920, 1080, 1f), 1.6f);
        // none saved: the default
        Assert.Equal(fallback, ChatPlace.Placed(fallback, 1920, 1080, 1.6f, null, null));
        // a place and a size the player gave it: theirs (the size at scale 1, shown at the window's scale), even over the bar
        Assert.Equal(new HudRect(960, 540, 800, 480), ChatPlace.Placed(fallback, 1920, 1080, 1.6f, (0.5f, 0.5f), (500f, 300f)));
        Assert.Equal(new HudRect(480, 540, 800, 380), ChatPlace.Placed(fallback, 1920, 1080, 2f, (0.25f, 0.5f), (400f, 190f)));
        // one without the other: the saved one and the default's other
        Assert.Equal(new HudRect(192, 540, fallback.Width, fallback.Height), ChatPlace.Placed(fallback, 1920, 1080, 1.6f, (0.1f, 0.5f), null));
        Assert.Equal(new HudRect(fallback.Left, fallback.Top, 640, 400), ChatPlace.Placed(fallback, 1920, 1080, 1.6f, null, (400f, 250f)));
        // a saved place that would leave the screen is moved back onto it
        Assert.Equal(new HudRect(1120, 600, 800, 480), ChatPlace.Placed(fallback, 1920, 1080, 1.6f, (0.9f, 0.9f), (500f, 300f)));
    }

    [Theory]
    [InlineData(false, false, false, UiTokens.ChatRestAlpha)]
    [InlineData(false, false, true, UiTokens.ChatRestAlpha)] // the captured pointer is not hovering it
    [InlineData(false, true, false, UiTokens.ChatRestAlpha)] // under Tab, the pointer elsewhere
    [InlineData(false, true, true, 1f)] // under Tab, the pointer over it
    [InlineData(true, false, false, 1f)] // typing
    [InlineData(true, true, false, 1f)]
    [InlineData(true, true, true, 1f)]
    public void TheBackingIsSeeThroughUnlessTypingOrHoveredUnderTab(bool typing, bool cursorMode, bool over, float alpha)
        => Assert.Equal(alpha, ChatPlace.BackingAlpha(typing, cursorMode, over));

    [Fact]
    public void TheRestShareIsAboutAThird() => Assert.InRange(UiTokens.ChatRestAlpha, 0.3f, 0.4f);

    [Fact]
    public void TheRoundsRowsAreHiddenAtRestAndShownByAChatKeyUntilSentCancelledOrLeft()
    {
        var rows = new ChatInputRows { Round = true };
        Assert.False(rows.Shown);
        // a chat key (Say, Radio, Me, OOC, ...: each focuses the input through the same call)
        rows.Type();
        Assert.True(rows.Shown);
        Assert.True(rows.Sent()); // sent: the input lets the keyboard go
        Assert.False(rows.Shown);
        rows.Type();
        rows.Leave(); // Esc
        Assert.False(rows.Shown);
        rows.Type();
        rows.Leave(); // the input lost the keyboard
        Assert.False(rows.Shown);
    }

    [Fact]
    public void AClickOnTheWindowsOwnBodyShowsTheRowsUnderTabOnly()
    {
        var rows = new ChatInputRows { Round = true };
        Assert.False(rows.Click(cursorMode: false, onBody: true));
        Assert.False(rows.Shown);
        // a click on another window, a menu or a modal drawn over the chat, or on one of its buttons or scroll bars, is not a click on its body
        Assert.False(rows.Click(cursorMode: true, onBody: false));
        Assert.False(rows.Shown);
        Assert.True(rows.Click(cursorMode: true, onBody: true));
        Assert.True(rows.Shown);
        rows.Leave();
        Assert.False(rows.Shown);
    }

    [Fact]
    public void TheLobbysWindowAlwaysShowsTheRows()
    {
        var rows = new ChatInputRows();
        Assert.True(rows.Shown);
        rows.Type();
        Assert.False(rows.Sent()); // the lobby's input keeps the keyboard after a send
        Assert.True(rows.Shown);
        rows.Leave();
        Assert.True(rows.Shown);
        rows.Round = true; // not typing: the round's rows hide
        Assert.False(rows.Shown);
        rows.Round = false;
        Assert.True(rows.Shown);
    }

    [Fact]
    public void TheRowsStayShownWhileTheInputHasTheKeyboardWhenTheLobbyBecomesTheRound()
    {
        // the player clicked into the lobby's input (focus gained), or sent a line there and kept typing; then the round starts
        var rows = new ChatInputRows();
        rows.Type();
        Assert.False(rows.Sent());
        rows.Round = true;
        Assert.True(rows.Shown); // never hidden under a focused input
        rows.Round = false;
        rows.Round = true;
        Assert.True(rows.Shown);
        rows.Leave(); // until the input loses the keyboard
        Assert.False(rows.Shown);
    }

    [Fact]
    public void ALineTypedWhenTheInterfaceWasHiddenTakesTheKeyboardBackOnlyIfNothingElseTookIt()
    {
        // taken back: hidden while typing, still pending while hidden, then visible again with nothing else focused
        var rows = new ChatInputRows { Round = true };
        rows.Type();
        rows.LostWhileHidden();
        Assert.True(rows.Shown);
        Assert.Equal(ChatResume.Waiting, rows.Resume(inputVisible: false, otherFocused: false));
        Assert.True(rows.Shown);
        Assert.Equal(ChatResume.TakeBack, rows.Resume(inputVisible: true, otherFocused: false));
        Assert.True(rows.Shown);
        Assert.Equal(ChatResume.None, rows.Resume(inputVisible: true, otherFocused: false)); // once

        // dropped: the player typed in the dev console or a tgui window's field while the interface was hidden
        rows.LostWhileHidden();
        Assert.Equal(ChatResume.Dropped, rows.Resume(inputVisible: false, otherFocused: true));
        Assert.False(rows.Shown);
        Assert.Equal(ChatResume.None, rows.Resume(inputVisible: true, otherFocused: false));

        // dropped too when the other field holds the keyboard as the interface shows again
        rows.Type();
        rows.LostWhileHidden();
        Assert.Equal(ChatResume.Dropped, rows.Resume(inputVisible: true, otherFocused: true));
        Assert.False(rows.Shown);

        // nothing pending when the input was not typing, or after the window closed
        rows.LostWhileHidden();
        Assert.Equal(ChatResume.None, rows.Resume(inputVisible: true, otherFocused: false));
        rows.Type();
        rows.LostWhileHidden();
        rows.Leave();
        Assert.Equal(ChatResume.None, rows.Resume(inputVisible: true, otherFocused: false));
    }

    [Fact]
    public void APlaceOrSizeIsSavedOnlyWhenAMoveOrAResizeChangedIt()
    {
        var at = new HudRect(12, 562, 468.125f, 240);
        Assert.False(ChatPlace.GeometryChanged(at, at)); // a click on the title bar or an edge without moving
        Assert.False(ChatPlace.GeometryChanged(at, new HudRect(12, 562, 468.125f, 240)));
        Assert.True(ChatPlace.GeometryChanged(at, at with { Left = 13 })); // moved
        Assert.True(ChatPlace.GeometryChanged(at, at with { Top = 500 }));
        Assert.True(ChatPlace.GeometryChanged(at, at with { Width = 500 })); // resized
        Assert.True(ChatPlace.GeometryChanged(at, at with { Height = 300 }));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(3f)]
    public void TheChatLinesKeepClearOfTheBottomRowAt1080(float scale)
    {
        var metrics = new HudMetrics(1920, 1080, scale);
        float lines = metrics.TextScale * new PlayOptions().ChatLinesScale;
        float bottom = ChatPlace.LinesBottom(metrics, lines, bottomRow: true);
        float before = 1080 - MathF.Round(1080 * 0.2f) + 20f * lines;
        float left = ChatPlace.LinesLeft(lines), right = left + ChatPlace.LinesWidth(1920, lines);
        HandsLayout row = LiveHud.Hands(metrics, true, true);
        HudRect strip = LiveHud.HintSpan(metrics, row);
        float gap = MathF.Round(UiTokens.ChatRowGap * UiTokens.Unit(metrics));
        Assert.True(right > row.Left.Left, "the lines' block reaches over the row at 1920x1080");
        Assert.True(bottom <= strip.Top - gap && bottom <= row.Left.Top - gap, $"the lines end at {bottom}, {gap} above the strip at {strip.Top}");
        // where they already stood clear they stay where they were; else they end just the gap above the strip
        Assert.Equal(Math.Min(before, strip.Top - gap), bottom);
        if (scale >= 2f)
            Assert.True(bottom < before, "at 200 % and more the lines are raised");
        else
            Assert.Equal(before, bottom);
        // the lobby has no bottom row: the lines stay where they were
        Assert.Equal(before, ChatPlace.LinesBottom(metrics, lines, bottomRow: false));
    }
}
