using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M2 P8: tg's screen_loc on the client's screen, the doll's zones and their D70 cues, the text of the dev replies, the console model.</summary>
public class HudTests
{
    private static ScreenLocSpec Parse(string text)
    {
        Assert.True(ScreenLoc.TryParse(text, out ScreenLocSpec spec), text);
        return spec;
    }

    [Theory]
    [InlineData("EAST-1:28,CENTER-2:17")]
    [InlineData("WEST:6,SOUTH:5")]
    [InlineData("CENTER:-2,WEST")]
    [InlineData("EAST-2:26, SOUTH+1:24")]
    [InlineData("12,1")]
    [InlineData("WEST,SOUTH to EAST,NORTH")]
    [InlineData("BOTTOM+1:8,RIGHT-1:28")]
    [InlineData("CENTER-7,CENTER-7")]
    public void TgsScreenLocsParse(string text) => Parse(text);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("EAST")]
    [InlineData("EAST,WEST,NORTH")]
    [InlineData("MIDDLE,SOUTH")]
    [InlineData("EAST-x,SOUTH")]
    [InlineData("EAST:,SOUTH")]
    [InlineData("1e9,1")]
    public void BadScreenLocsDoNotParse(string text) => Assert.False(ScreenLoc.TryParse(text, out _));

    [Fact]
    public void APercentageOfTheScreenParses()
    {
        // tg's cinematic (syndicate N1a): "BOTTOM,LEFT+50%", the y axis named first and the x half the screen from the left
        ScreenLocSpec spec = Parse("BOTTOM,LEFT+50%");
        Assert.Equal(new ScreenAxis(ScreenAnchor.Low, 0, 0, 50), spec.X);
        Assert.Equal(new ScreenAxis(ScreenAnchor.Low, 0, 0, 0), spec.Y);
        var screen = new HudMetrics(1920, 1080, 1f);
        HudRect rect = ScreenLoc.Place(spec, screen, 32, 32, 0, 0, out bool fullscreen);
        Assert.False(fullscreen);
        Assert.Equal(960f, rect.Left);
    }

    [Fact]
    public void TheGridSitsOnTheScreenEdges()
    {
        var screen = new HudMetrics(1920, 1080, 1f);
        Assert.Equal(72f, screen.Tile);
        Assert.Equal(2.25f, screen.PixelScale);

        // The bottom left tile: its icon square starts at the corner, and the sprite is 32 x 32.
        HudRect west = ScreenLoc.Place(Parse("WEST,SOUTH"), screen, 32, 32, 0, 0, out bool full);
        Assert.False(full);
        Assert.Equal(0f, west.Left, 3);
        Assert.Equal(1080f - 72f, west.Top, 3);
        Assert.Equal(72f, west.Width, 3);
        Assert.Equal(72f, west.Height, 3);

        // The right column: EAST is the last column at the right edge, EAST-1 the one before it.
        HudRect east = ScreenLoc.Place(Parse("EAST,NORTH"), screen, 32, 32, 0, 0, out _);
        Assert.Equal(1920f - 72f, east.Left, 3);
        Assert.Equal(0f, east.Top, 3);
        HudRect eastMinus = ScreenLoc.Place(Parse("EAST-1,NORTH-1"), screen, 32, 32, 0, 0, out _);
        Assert.Equal(1920f - 144f, eastMinus.Left, 3);
        Assert.Equal(72f, eastMinus.Top, 3);

        // The middle: the centre tile is centred.
        HudRect center = ScreenLoc.Place(Parse("CENTER,CENTER"), screen, 32, 32, 0, 0, out _);
        Assert.Equal(960f - 36f, center.Left, 3);
        Assert.Equal(540f - 36f, center.Top, 3);
    }

    [Fact]
    public void TheAlertsStartInTheTopRightCornerAndNeverReachThePulse()
    {
        // tg's alert.dm get_ui_alert_placement: EAST-1:28,CENTER+(6 - i):(29 - 2i), five slots.
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, ScreenLoc.AlertSlot(Parse($"EAST-1:28,CENTER+{6 - i}:{29 - 2 * i}")));
        Assert.Equal(0, ScreenLoc.AlertSlot(Parse("EAST-1:28,CENTER+1:21"))); // ui_internal
        Assert.Equal(0, ScreenLoc.AlertSlot(Parse("EAST-1:28,CENTER-1:19"))); // ui_health, the pulse

        foreach (float scale in new[] { 1f, 1.5f, 2f, 3f })
        {
            var screen = new HudMetrics(1920, 1080, scale);
            float pulseTop = ScreenLoc.Place(Parse("EAST-1:28,CENTER-1:19"), screen, 32, 32, 0, 0, out _).Top;
            float floor = Math.Max(pulseTop, 0f);
            HudRect first = ScreenLoc.PlaceAlert(1, 5, screen, floor);
            // DV82: the first one as far from the top as from the right (tg's EAST-1:28 leaves 4 pixels), on the screen at any scale.
            Assert.Equal(first.Top, 1920f - first.Right, 2);
            Assert.True(first.Top >= 0f, $"scale {scale}");
            // Five of them end above the pulse (shrunk when the screen is too short for them), in tg's order down the column.
            HudRect last = ScreenLoc.PlaceAlert(5, 5, screen, floor);
            Assert.True(last.Bottom <= floor || floor < 2 * first.Top + 1f, $"scale {scale}: {last.Bottom} > {floor}");
            Assert.True(last.Top > first.Top);
        }
        // With room to spare (scale 1, one alert) it is tg's size and spacing: one tile and two pixels.
        var normal = new HudMetrics(1920, 1080, 1f);
        Assert.Equal(72f, ScreenLoc.PlaceAlert(1, 2, normal, 1080f).Width, 2);
        Assert.Equal(72f + 2 * 2.25f, ScreenLoc.PlaceAlert(2, 2, normal, 1080f).Top - ScreenLoc.PlaceAlert(1, 2, normal, 1080f).Top, 2);
    }

    [Fact]
    public void PixelOffsetsMoveUpAndRightInTgPixels()
    {
        var screen = new HudMetrics(1920, 1080, 1f);
        // ui_healthdoll: EAST-1:28,CENTER-2:17
        HudRect doll = ScreenLoc.Place(Parse("EAST-1:28,CENTER-2:17"), screen, 32, 32, 0, 0, out _);
        Assert.Equal(1920f - 144f + 28 * 2.25f, doll.Left, 2);
        // y up: the tile two below the centre, 17 pixels higher
        float bottom = 540f - 36f - 2 * 72f + 17 * 2.25f;
        Assert.Equal(1080f - (bottom + 72f), doll.Top, 2);
        // a negative pixel offset
        HudRect negative = ScreenLoc.Place(Parse("CENTER:-2,CENTER"), screen, 32, 32, 0, 0, out _);
        Assert.Equal(960f - 36f - 2 * 2.25f, negative.Left, 2);
    }

    [Fact]
    public void ASpriteThatReachesOutOfItsSquareIsOffsetByItsOrigin()
    {
        var screen = new HudMetrics(1280, 720, 1f);
        HudRect plain = ScreenLoc.Place(Parse("WEST+3,SOUTH+3"), screen, 32, 32, 0, 0, out _);
        HudRect wide = ScreenLoc.Place(Parse("WEST+3,SOUTH+3"), screen, 64, 64, 16, 8, out _);
        float s = screen.PixelScale;
        Assert.Equal(plain.Left - 16 * s, wide.Left, 3);
        Assert.Equal(64 * s, wide.Width, 3);
        Assert.Equal(plain.Top - 24 * s, wide.Top, 3); // 32 pixels taller, 8 of them below the icon square
    }

    [Fact]
    public void TheFullscreenOverlayCoversTheScreenAndARangeStretches()
    {
        var screen = new HudMetrics(1920, 1080, 1f);
        HudRect full = ScreenLoc.Place(Parse("CENTER-7,CENTER-7"), screen, 480, 480, 0, 0, out bool fullscreen);
        Assert.True(fullscreen);
        Assert.Equal(new HudRect(0, 0, 1920, 1080), full);
        HudRect range = ScreenLoc.Place(Parse("WEST,SOUTH to EAST,NORTH"), screen, 32, 32, 0, 0, out fullscreen);
        Assert.False(fullscreen);
        Assert.Equal(0f, range.Left, 3);
        Assert.Equal(0f, range.Top, 3);
        Assert.Equal(1920f, range.Width, 3);
        Assert.Equal(1080f, range.Height, 3);
    }

    [Fact]
    public void TheUiScaleScalesTheTileAndKeepsTheEdges()
    {
        var big = new HudMetrics(1920, 1080, 1.5f);
        Assert.Equal(108f, big.Tile, 3);
        HudRect east = ScreenLoc.Place(Parse("EAST,SOUTH"), big, 32, 32, 0, 0, out _);
        Assert.Equal(1920f - 108f, east.Left, 3);
        Assert.Equal(1080f - 108f, east.Top, 3);
    }

    [Fact]
    public void AnAxisPairGivenYFirstIsSwapped()
    {
        ScreenLocSpec spec = Parse("BOTTOM+1:8,RIGHT-1:28");
        Assert.Equal(ScreenAnchor.High, spec.X.Anchor);
        Assert.Equal(-1, spec.X.Tile);
        Assert.Equal(28, spec.X.Pixels);
        Assert.Equal(ScreenAnchor.Low, spec.Y.Anchor);
        Assert.Equal(1, spec.Y.Tile);
        Assert.Equal(8, spec.Y.Pixels);
    }

    [Fact]
    public void AnAbsoluteTileCountsFromOne()
    {
        ScreenLocSpec spec = Parse("12,1");
        Assert.Equal(ScreenAnchor.Absolute, spec.X.Anchor);
        Assert.Equal(11, spec.X.Tile);
        Assert.Equal(0, spec.Y.Tile);
    }

    [Theory]
    [InlineData("head0", "head", ZoneKind.Level, 0)]
    [InlineData("head3", "head", ZoneKind.Level, 3)]
    [InlineData("chest5", "chest", ZoneKind.Level, 5)]
    [InlineData("l_arm1", "l_arm", ZoneKind.Level, 1)]
    [InlineData("r_leg6", "r_leg", ZoneKind.Missing, 0)]
    [InlineData("l_leg7", "l_leg", ZoneKind.Disabled, 0)]
    [InlineData("headDEAD", "head", ZoneKind.Dead, 0)]
    [InlineData("r_armDEAD", "r_arm", ZoneKind.Dead, 0)]
    public void DollIconStatesAreRead(string state, string zone, ZoneKind kind, int level)
    {
        Assert.True(HealthZones.TryParseState(state, out ZoneState parsed));
        Assert.Equal(new ZoneState(zone, kind, level), parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("head")]
    [InlineData("head8")]
    [InlineData("head12")]
    [InlineData("health3")]
    [InlineData("tail1")]
    [InlineData("headfoo")]
    public void OtherIconStatesAreNotZones(string state) => Assert.False(HealthZones.TryParseState(state, out _));

    [Fact]
    public void TheHealthIndicatorIsRead()
    {
        Assert.Equal(0, HealthZones.TryParseIndicator("health0"));
        Assert.Equal(7, HealthZones.TryParseIndicator("health7"));
        Assert.Equal(-1, HealthZones.TryParseIndicator("health"));
        Assert.Equal(-1, HealthZones.TryParseIndicator("head3"));
        Assert.Equal(-1, HealthZones.TryParseIndicator(null));
    }

    [Fact]
    public void EveryZoneHasALabelWithItsNameAndLevel()
    {
        Assert.Equal("Head 2/5", HealthZones.Label(new ZoneState("head", ZoneKind.Level, 2)));
        Assert.Equal("L arm 0/5", HealthZones.Label(new ZoneState("l_arm", ZoneKind.Level, 0)));
        Assert.Equal("R leg missing", HealthZones.Label(new ZoneState("r_leg", ZoneKind.Missing, 0)));
        Assert.Equal("Chest disabled", HealthZones.Label(new ZoneState("chest", ZoneKind.Disabled, 0)));
        Assert.Equal("Head dead", HealthZones.Label(new ZoneState("head", ZoneKind.Dead, 0)));
        foreach (string zone in HealthZones.Zones)
            Assert.NotEqual(zone, HealthZones.ZoneName(zone));
    }

    [Fact]
    public void EachLevelHasItsOwnHatchingAndNoHatchingMeansNoDamage()
    {
        var styles = new HashSet<HatchStyle>();
        for (int level = 0; level <= HealthZones.MaxLevel; level++)
            Assert.True(styles.Add(HealthZones.Hatch(new ZoneState("head", ZoneKind.Level, level))));
        Assert.Equal(HatchStyle.None, HealthZones.Hatch(new ZoneState("head", ZoneKind.Level, 0)));
        Assert.True(styles.Add(HealthZones.Hatch(new ZoneState("head", ZoneKind.Missing, 0))));
        Assert.True(styles.Add(HealthZones.Hatch(new ZoneState("head", ZoneKind.Disabled, 0))));
        Assert.True(styles.Add(HealthZones.Hatch(new ZoneState("head", ZoneKind.Dead, 0))));
    }

    [Theory]
    [InlineData(ColorblindMode.RedGreen)]
    [InlineData(ColorblindMode.BlueYellow)]
    [InlineData(ColorblindMode.Mono)]
    public void AColourblindPaletteGivesEveryLevelItsOwnColourAndNothingIsRedOnGreen(ColorblindMode mode)
    {
        var colours = new List<Rgb>();
        for (int level = 0; level <= HealthZones.MaxLevel; level++)
        {
            Rgb? c = HealthZones.PaletteColor(mode, new ZoneState("chest", ZoneKind.Level, level));
            Assert.NotNull(c);
            Assert.DoesNotContain(c.Value, colours);
            colours.Add(c.Value);
        }
        // The first and the worst level differ in lightness as well as in hue.
        float Light(Rgb c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
        Assert.True(Math.Abs(Light(colours[0]) - Light(colours[5])) > 0.2f);
        Assert.NotNull(HealthZones.PaletteColor(mode, new ZoneState("chest", ZoneKind.Missing, 0)));
    }

    // ui-overhaul.md section 6: with no colourblind palette, one standard ramp replaces tg's own sprite colours
    [Theory]
    [InlineData(ZoneKind.Level, 0, 0x8FD694FF)]
    [InlineData(ZoneKind.Level, 1, 0xFFE156FF)]
    [InlineData(ZoneKind.Level, 2, 0xFFE156FF)]
    [InlineData(ZoneKind.Level, 3, 0xFFA62BFF)]
    [InlineData(ZoneKind.Level, 4, 0xFFA62BFF)]
    [InlineData(ZoneKind.Level, 5, 0xFF4D4DFF)]
    [InlineData(ZoneKind.Missing, 0, UiTokens.DollMissing)]
    [InlineData(ZoneKind.Dead, 0, UiTokens.DollDead)]
    public void WithoutAColourblindPaletteTheStandardRampColoursTheDoll(ZoneKind kind, int level, uint rgba)
    {
        var state = new ZoneState("chest", kind, level);
        Rgb? colour = HealthZones.PaletteColor(ColorblindMode.Off, state);
        Assert.Equal(UiTokens.ToRgb(rgba), colour);
        Assert.Equal(colour, HealthZones.StandardColor(state));
    }

    [Fact]
    public void TheStandardRampsMissingPartIsDarkGreyAndADeadMobNearBlack()
    {
        float Light(Rgb c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
        Rgb missing = HealthZones.StandardColor(new ZoneState("l_leg", ZoneKind.Missing, 0)).Value;
        Rgb dead = HealthZones.StandardColor(new ZoneState("l_leg", ZoneKind.Dead, 0)).Value;
        Assert.True(Math.Abs(missing.R - missing.G) < 0.05f && Math.Abs(missing.G - missing.B) < 0.05f, "grey");
        Assert.InRange(Light(missing), 0.2f, 0.4f);
        Assert.True(Light(dead) < 0.12f);
        // every level and state keeps its own hatching besides (D70), whatever the colour
        Assert.Equal(HatchStyle.DenseCross, HealthZones.Hatch(new ZoneState("chest", ZoneKind.Level, 4)));
        Assert.NotEqual(HealthZones.Hatch(new ZoneState("chest", ZoneKind.Level, 3)), HealthZones.Hatch(new ZoneState("chest", ZoneKind.Level, 4)));
    }

    // tg's disabled sprite (dark grey with bright red marks) is drawn untinted: tinted by its red marks it would read as a dark maroon
    [Fact]
    public void WithoutAColourblindPaletteADisabledPartIsTgsOwnPictureAndKeepsItsHatch()
    {
        foreach (string zone in HealthZones.Zones)
        {
            var disabled = new ZoneState(zone, ZoneKind.Disabled, 0);
            Assert.Null(HealthZones.PaletteColor(ColorblindMode.Off, disabled));
            Assert.Null(HealthZones.StandardColor(disabled));
            Assert.Equal(HatchStyle.Bars, HealthZones.Hatch(disabled));
            foreach (ColorblindMode mode in new[] { ColorblindMode.RedGreen, ColorblindMode.BlueYellow, ColorblindMode.Mono })
                Assert.NotNull(HealthZones.PaletteColor(mode, disabled));
        }
    }

    [Fact]
    public void TheColourblindPalettesAreUnchanged()
    {
        Assert.Equal(new Rgb(0.00f, 0.45f, 0.70f), HealthZones.PaletteColor(ColorblindMode.RedGreen, new ZoneState("chest", ZoneKind.Level, 0)));
        Assert.Equal(new Rgb(0.25f, 0.05f, 0.00f), HealthZones.PaletteColor(ColorblindMode.RedGreen, new ZoneState("chest", ZoneKind.Level, 5)));
        Assert.Equal(new Rgb(0.00f, 0.60f, 0.53f), HealthZones.PaletteColor(ColorblindMode.BlueYellow, new ZoneState("chest", ZoneKind.Level, 0)));
        Assert.Equal(new Rgb(0.51f, 0.00f, 0.24f), HealthZones.PaletteColor(ColorblindMode.BlueYellow, new ZoneState("chest", ZoneKind.Level, 5)));
        Assert.Equal(new Rgb(0.88f, 0.88f, 0.88f), HealthZones.PaletteColor(ColorblindMode.Mono, new ZoneState("chest", ZoneKind.Level, 0)));
        foreach (ColorblindMode mode in new[] { ColorblindMode.RedGreen, ColorblindMode.BlueYellow, ColorblindMode.Mono })
        {
            Assert.Equal(new Rgb(0.45f, 0.45f, 0.45f), HealthZones.PaletteColor(mode, new ZoneState("chest", ZoneKind.Missing, 0)));
            Assert.Equal(new Rgb(0.35f, 0.40f, 0.60f), HealthZones.PaletteColor(mode, new ZoneState("chest", ZoneKind.Disabled, 0)));
            Assert.Equal(new Rgb(0.12f, 0.12f, 0.12f), HealthZones.PaletteColor(mode, new ZoneState("chest", ZoneKind.Dead, 0)));
        }
    }

    [Fact]
    public void TheBrightestChannelOfASpritesOpaquePixelsIsItsMainTone()
    {
        // tg's chest3: main tone BD7E00, the darker one A87100, the edge 5E4000; a see-through pixel brighter than all does not count
        var image = new Anomaly.Client.Shared.Icons.Raster(4, 1, new byte[] { 0xBD, 0x7E, 0x00, 255, 0xA8, 0x71, 0x00, 255, 0x5E, 0x40, 0x00, 255, 0xFF, 0xFF, 0xFF, 0 });
        Assert.Equal(0xBD / 255f, HealthZones.Brightest(image), 4);
        Assert.Equal(0f, HealthZones.Brightest(new Anomaly.Client.Shared.Icons.Raster(1, 1, new byte[] { 255, 255, 255, 10 })));
        Assert.Equal(0f, HealthZones.Brightest(null));
    }

    private static readonly HatchStyle[] AllHatched =
        { HatchStyle.Light, HatchStyle.Medium, HatchStyle.Cross, HatchStyle.DenseCross, HatchStyle.Solid, HatchStyle.Dots, HatchStyle.Bars, HatchStyle.Stripes };

    // ui-overhaul.md 6: every hatching leaves most of the part in its colour, so levels 4 and 5 do not read near-black
    [Fact]
    public void EveryHatchingLeavesMostOfThePartInItsColourAndTheLevelsStayApart()
    {
        var seen = new HashSet<HatchPattern>();
        foreach (HatchStyle style in AllHatched)
        {
            HatchPattern pattern = HealthZones.Pattern(style);
            Assert.NotEqual(HatchShape.None, pattern.Shape);
            float coverage = HealthZones.Coverage(pattern);
            Assert.InRange(coverage, 0.05f, HealthZones.MaxHatchCoverage);
            Assert.True(seen.Add(pattern), $"{style} looks like another level");
        }
        Assert.Equal(HatchShape.None, HealthZones.Pattern(HatchStyle.None).Shape);
        // a worse level never covers less than a lighter one
        for (int level = 2; level <= HealthZones.MaxLevel; level++)
        {
            float before = HealthZones.Coverage(HealthZones.Pattern(HealthZones.Hatch(new ZoneState("chest", ZoneKind.Level, level - 1))));
            float now = HealthZones.Coverage(HealthZones.Pattern(HealthZones.Hatch(new ZoneState("chest", ZoneKind.Level, level))));
            Assert.True(now >= before, $"level {level}");
        }
    }

    [Theory]
    [InlineData(2f)]
    [InlineData(3f)]
    [InlineData(6f)]
    [InlineData(9f)]
    public void TheDrawnLinesCoverWhatThePatternSaysAtEveryDollScale(float cell)
    {
        // the shader's lines (HealthZones.Hatched) over an area of a few doll parts, at a doll scale of 1x (cell 2) to 9x
        foreach (HatchStyle style in AllHatched)
        {
            HatchPattern pattern = HealthZones.Pattern(style);
            int hit = 0, all = 0;
            for (int y = 0; y < 240; y++)
            {
                for (int x = 0; x < 240; x++, all++)
                {
                    if (HealthZones.Hatched(pattern, cell, x + 0.5f, y + 0.5f))
                        hit++;
                }
            }
            float share = hit / (float)all;
            Assert.True(share <= HealthZones.MaxHatchCoverage + 0.06f, $"{style} at {cell}: {share}");
            Assert.True(share > 0.03f, $"{style} at {cell} draws nothing");
        }
        Assert.False(HealthZones.Hatched(HealthZones.Pattern(HatchStyle.None), cell, 0.5f, 0.5f));
    }

    [Fact]
    public void ChatHtmlBecomesPlainLines()
    {
        Assert.Equal("Analyzing results for Bob\nOverall status: healthy\nBrute: 0",
            HtmlText.ToPlain("<div class='info'><b>Analyzing results for Bob</b><br>Overall status: <span class='good'>healthy</span><br />Brute:&nbsp;0</div>"));
        Assert.Equal("a < b & c", HtmlText.ToPlain("a &lt; b &amp; c"));
        Assert.Equal("A", HtmlText.ToPlain("&#65;"));
        Assert.Equal("&bogus; x", HtmlText.ToPlain("&bogus; x"));
        Assert.Equal("one\ntwo", HtmlText.ToPlain("one<br><br><br><br>two"));
        Assert.Equal("text", HtmlText.ToPlain("text<unfinished"));
        Assert.Equal(string.Empty, HtmlText.ToPlain(null));
        Assert.True(HtmlText.ToPlain(new string('x', 100000)).Length <= HtmlText.MaxChars);
    }

    [Fact]
    public void TheConsoleChecksALineLikeTheServerDoes()
    {
        Assert.True(DevConsoleModel.TryPrepare("  damage brute 20 head  ", out string command, out _));
        Assert.Equal("damage brute 20 head", command);
        Assert.False(DevConsoleModel.TryPrepare("   ", out _, out string problem));
        Assert.Equal("type a command", problem);
        Assert.False(DevConsoleModel.TryPrepare("a\u0001b", out _, out _));
        Assert.False(DevConsoleModel.TryPrepare(new string('x', Limits.MaxDevCommandBytes + 1), out _, out _));
        Assert.True(DevConsoleModel.TryPrepare(new string('x', Limits.MaxDevCommandBytes), out _, out _));
        // Bytes, not characters.
        Assert.False(DevConsoleModel.TryPrepare(new string('é', Limits.MaxDevCommandBytes / 2 + 1), out _, out _));
    }

    [Fact]
    public void TheHistoryIsWalkedWithUpAndDown()
    {
        var console = new DevConsoleModel();
        Assert.Null(console.HistoryPrevious());
        console.OnSent("heal", 1);
        console.OnSent("kill", 2);
        console.OnSent("kill", 3); // a repeat is one entry
        Assert.Equal(2, console.History.Count);
        Assert.Equal("kill", console.HistoryPrevious());
        Assert.Equal("heal", console.HistoryPrevious());
        Assert.Equal("heal", console.HistoryPrevious());
        Assert.Equal("kill", console.HistoryNext());
        Assert.Equal(string.Empty, console.HistoryNext());
    }

    [Fact]
    public void TheReplyOfAnAnalyzeGoesToThePanelAndTheRestToTheLog()
    {
        var console = new DevConsoleModel();
        console.OnSent("analyze Bob", 10.0);
        Assert.Equal(TextDestination.Analyzer, console.OnServerText(new ServerText(null, "<b>Bob</b><br>healthy"), 10.5, out string plain));
        Assert.Equal("Bob\nhealthy", plain);
        Assert.Equal("Bob\nhealthy", console.AnalyzerText);
        // The next text is the console's, and so is anything after the window.
        Assert.Equal(TextDestination.Console, console.OnServerText(new ServerText(null, "You feel better."), 10.6, out _));
        console.OnSent("analyze", 20.0);
        Assert.Equal(TextDestination.Console, console.OnServerText(new ServerText(null, "late"), 24.0, out _));
        Assert.Contains("late", console.Log);
        Assert.Contains("> [dev] analyze Bob", console.Log);
        Assert.Equal(TextDestination.Console, console.OnServerText(new ServerText(null, "<br>"), 1.0, out _));
    }

    [Fact]
    public void TheLogIsBounded()
    {
        var console = new DevConsoleModel();
        for (int i = 0; i < 500; i++)
            console.AddLog("line " + i);
        Assert.Equal(DevConsoleModel.MaxLogLines, console.Log.Count);
        Assert.Equal("line 499", console.Log[^1]);
        for (int i = 0; i < 200; i++)
            console.OnSent("cmd " + i, i);
        Assert.Equal(DevConsoleModel.MaxHistory, console.History.Count);
    }
}
