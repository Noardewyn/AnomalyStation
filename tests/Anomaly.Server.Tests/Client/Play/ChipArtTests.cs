using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client.Render;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 11: a chip shows the entry's visible art (every opaque pixel of its icon state's frames with its overlays, tg's outline at
/// its full reach), scaled uniformly so that its larger side fills the chip less 2 on each side, centred at whole pixels, never outside the chip;
/// tg's whole square without art.
/// </summary>
public class ChipArtTests
{
    private readonly ITestOutputHelper _output;

    public ChipArtTests(ITestOutputHelper output) => _output = output;

    private static bool Whole(HudRect r) => r.Left == MathF.Floor(r.Left) && r.Top == MathF.Floor(r.Top) && r.Width == MathF.Floor(r.Width) && r.Height == MathF.Floor(r.Height);

    private static bool Inside(HudRect inner, HudRect outer) => inner.Left >= outer.Left && inner.Top >= outer.Top && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

    private static float Room(HudRect chip) => chip.Width - 2 * MathF.Round(2f * chip.Width / 40f);

    // centred both ways within one pixel
    private static void Centred(HudRect picture, HudRect chip, string what)
    {
        Assert.True(MathF.Abs(picture.Left + picture.Width / 2f - (chip.Left + chip.Width / 2f)) <= 1f, $"{what}: {picture} not centred across {chip}");
        Assert.True(MathF.Abs(picture.Top + picture.Height / 2f - (chip.Top + chip.Height / 2f)) <= 1f, $"{what}: {picture} not centred down {chip}");
    }

    // the larger side fills the room exactly, the smaller keeps the art's aspect within a pixel, at whole pixels, centred, inside the chip
    private static void Fills(HudRect picture, HudRect art, HudRect chip, string what)
    {
        float room = Room(chip);
        Assert.True(Whole(picture), $"{what}: {picture} not at whole pixels");
        Assert.Equal(room, MathF.Max(picture.Width, picture.Height));
        float k = room / MathF.Max(art.Width, art.Height);
        Assert.True(MathF.Abs(picture.Width - art.Width * k) <= 1f && MathF.Abs(picture.Height - art.Height * k) <= 1f, $"{what}: {picture} not {art} scaled by {k}");
        Centred(picture, chip, what);
        Assert.True(Inside(picture, chip), $"{what}: {picture} outside {chip}");
    }

    [Fact]
    public void TheArtIsScaledSoItsLargerSideFillsTheChipAndCentred()
    {
        var chip = new HudRect(100, 200, 40, 40); // full size at 100%: the room is 36
        (float scale, HudRect hunger) = LiveHud.ChipArt(chip, new HudRect(2, 5, 23, 22)); // tg's hunger bar: the burger, its outline and the bar
        Assert.Equal(36f / 23f, scale, 4);
        Assert.Equal(new HudRect(102, 203, 36, 34), hunger); // 22 x 36 / 23 = 34.4
        Assert.Equal(new HudRect(102, 207, 36, 25), LiveHud.ChipArt(chip, new HudRect(7, 3, 17, 12)).Picture); // 12 x 36 / 17 = 25.4; the odd pixel below
        Assert.Equal(new HudRect(110, 202, 20, 36), LiveHud.ChipArt(chip, new HudRect(11, 8, 9, 16)).Picture); // 9 x 36 / 16 = 20.25
        Assert.Equal(new HudRect(102, 202, 36, 36), LiveHud.ChipArt(chip, new HudRect(0, 0, 32, 32)).Picture);
        // art larger than the room is made smaller: 30 in a chip of 24 (room 22), inside it
        (float down, HudRect small) = LiveHud.ChipArt(new HudRect(0, 0, 24, 24), new HudRect(1, 1, 30, 30));
        Assert.True(down < 1f);
        Assert.Equal(new HudRect(1, 1, 22, 22), small);
        Assert.Equal(new HudRect(1, 2, 22, 20), LiveHud.ChipArt(new HudRect(0, 0, 24, 24), new HudRect(-2, 0, 34, 31)).Picture);
    }

    [Fact]
    public void AtEveryChipSizeTheArtFillsItsRoomAtWholePixelsInsideTheChip()
    {
        // chips of 24 (the least the stack makes, room 22), 40, 50 (125%), 60 (150%) and 80 (200%), at odd and even places
        foreach (HudRect chip in new[] { new HudRect(1385, 480, 24, 24), new HudRect(17, 3, 24, 24), new HudRect(1832, 711, 40, 40), new HudRect(1807, 500, 50, 50),
            new HudRect(1, 1, 50, 50), new HudRect(1793, 300, 60, 60), new HudRect(1770, 400, 80, 80), new HudRect(5, 9, 80, 80) })
        {
            foreach (HudRect art in new[] { new HudRect(7, 3, 17, 12), new HudRect(11, 8, 9, 16), new HudRect(2, 5, 23, 22), new HudRect(3, 5, 1, 1), new HudRect(4, 1, 24, 30),
                new HudRect(0, 0, 32, 32), new HudRect(-1, -1, 34, 34), new HudRect(0, 17, 32, 15) })
            {
                string at = $"{art} in {chip}";
                HudRect picture = LiveHud.ChipArt(chip, art).Picture;
                Fills(picture, art, chip, at);
                // the composed picture (here tg's 32 x 32 square, or a 40 x 36 one with its square's corner at 4, 2) is drawn so the art lands on it
                foreach ((int w, int h, int ox, int oy) in new[] { (32, 32, 0, 0), (40, 36, 4, 2) })
                {
                    HudRect drawn = LiveHud.ChipPicture(chip, art, w, h, ox, oy);
                    float kx = drawn.Width / w, ky = drawn.Height / h;
                    float artLeft = drawn.Left + (art.Left + ox) * kx, artTop = drawn.Top + (art.Top + h - oy - 32) * ky;
                    Assert.True(MathF.Abs(artLeft - picture.Left) < 1e-3f && MathF.Abs(artTop - picture.Top) < 1e-3f, $"{at}: art at {artLeft},{artTop}, not {picture}");
                    Assert.True(MathF.Abs(art.Width * kx - picture.Width) < 1e-3f && MathF.Abs(art.Height * ky - picture.Height) < 1e-3f, $"{at}: art {art.Width * kx}x{art.Height * ky}");
                }
            }
        }
    }

    [Fact]
    public void WithoutArtAChipKeepsTgsWholeSquare()
    {
        var chip = new HudRect(100, 200, 40, 40);
        Assert.Equal(ScreenLoc.FitIcon(LiveHud.AlertPicture(chip), 32, 32, 0, 0), LiveHud.ChipPicture(chip, null, 32, 32, 0, 0));
        Assert.Equal(ScreenLoc.FitIcon(LiveHud.AlertPicture(chip), 40, 36, 4, 2), LiveHud.ChipPicture(chip, new HudRect(0, 0, 0, 0), 40, 36, 4, 2));
        // nothing opaque: no art
        var library = new IconLibrary(new MemoryIconFiles());
        library.Register("icons/blank.dmi", new FakeIcon().State("none", 1, 1, new Raster(32, 32)));
        var tables = new TestTables();
        Assert.True(LiveHud.TryChipArtBounds(new AppearanceCompositor(library), tables, tables.Add("icons/blank.dmi", "none"), out HudRect? art));
        Assert.Null(art);
        // an icon that cannot be read: no art either
        Assert.True(LiveHud.TryChipArtBounds(new AppearanceCompositor(library), tables, tables.Add("icons/missing.dmi", "none"), out art));
        Assert.Null(art);
    }

    [Fact]
    public void ABlinkingAlertHasOneRectangleForAllItsFrames()
    {
        // two frames: a block at the top left, then one at the bottom right; with an overlay further right still
        Raster first = new(32, 32), second = new(32, 32), dot = new(32, 32);
        for (int y = 4; y < 8; y++)
            for (int x = 6; x < 10; x++)
                first.Set(x, y, 0xFF0000FF);
        for (int y = 20; y < 26; y++)
            for (int x = 14; x < 18; x++)
                second.Set(x, y, 0x00FF00FF);
        dot.Set(25, 12, 0x0000FF01); // the faintest pixel counts
        var library = new IconLibrary(new MemoryIconFiles());
        library.Register("icons/alert.dmi", new FakeIcon().State("blink", 1, 2, first, second).State("dot", 1, 1, dot));
        var tables = new TestTables();
        TableRef overlay = tables.Add("icons/alert.dmi", "dot");
        TableRef root = tables.Add("icons/alert.dmi", "blink", e => e.Overlays.Add(overlay));
        var compositor = new AppearanceCompositor(library);
        Assert.True(LiveHud.TryChipArtBounds(compositor, tables, root, out HudRect? art));
        Assert.Equal(new HudRect(6, 4, 20, 22), art); // x 6..25, y 4..25
        // the same whatever frame the HUD drew last
        compositor.TryCompose(tables, root, ByondDir.South, 0.15, out _, rootOffset: true);
        Assert.True(LiveHud.TryChipArtBounds(compositor, tables, root, out HudRect? again));
        Assert.Equal(art, again);
        // the root's own pixel offset moves the art in tg's square, as the HUD draws it
        TableRef shifted = tables.Add("icons/alert.dmi", "blink", e => { e.Overlays.Add(overlay); e.PixelX = 3; e.PixelY = 2; });
        Assert.True(LiveHud.TryChipArtBounds(compositor, tables, shifted, out HudRect? moved));
        Assert.Equal(new HudRect(9, 2, 20, 22), moved);
        // a transparent root (a hunger bar fading in) counts whole
        TableRef faded = tables.Add("icons/alert.dmi", "blink", e => { e.Overlays.Add(overlay); e.Alpha = 0; });
        Assert.True(LiveHud.TryChipArtBounds(compositor, tables, faded, out HudRect? whole));
        Assert.Equal(art, whole);
    }

    [Fact]
    public void TgsPulsingOutlineCountsAtItsFullReach()
    {
        // the starving hunger bar: tg's outline of size 2 that pulses from alpha 0 (screen_objects.dm:1253); at alpha 0 it draws nothing, yet the
        // art is measured with it at full strength, 2 pixels round the block
        Raster block = new(32, 32);
        for (int y = 10; y < 20; y++)
            for (int x = 12; x < 18; x++)
                block.Set(x, y, 0xFFFFFFFF);
        var library = new IconLibrary(new MemoryIconFiles());
        library.Register("icons/bar.dmi", new FakeIcon().State("bar", 1, 1, block));
        var tables = new TestTables();
        var compositor = new AppearanceCompositor(library);
        Assert.True(LiveHud.TryChipArtBounds(compositor, tables, tables.Add("icons/bar.dmi", "bar"), out HudRect? plain));
        Assert.Equal(new HudRect(12, 10, 6, 10), plain);
        TableRef starving = tables.Add("icons/bar.dmi", "bar", e => e.Filters = new List<FilterEntry> { new() { Kind = FilterKind.Outline, Size = 2, Rgba = 0xFF0033FF, Alpha = 0 } });
        Assert.Equal(2f, LiveHud.OutlineReach(tables.Entry(starving)));
        Assert.Equal(0f, LiveHud.OutlineReach(tables.Entry(tables.Add("icons/bar.dmi", "bar"))));
        Assert.True(LiveHud.TryChipArtBounds(compositor, tables, starving, out HudRect? pulsing));
        Assert.Equal(new HudRect(10, 8, 10, 14), pulsing);
        // the outline the HUD draws at any moment of the pulse stands inside that art: the bounds of the strongest moment
        TableRef strongest = tables.Add("icons/bar.dmi", "bar", e => e.Filters = new List<FilterEntry> { new() { Kind = FilterKind.Outline, Size = 2, Rgba = 0xFF0033FF, Alpha = 200 } });
        Assert.True(LiveHud.TryChipArtBounds(compositor, tables, strongest, out HudRect? drawn));
        Assert.Equal(pulsing, drawn);
    }

    // ---- tg's real icons ----

    private static readonly uint Gold = 0xFFD700FF;

    private static FilterEntry Outline(uint rgba, float size, byte flags = 0, float alpha = 255f) => new() { Kind = FilterKind.Outline, Rgba = rgba, Size = size, Flags = flags, Alpha = alpha };


    // the art TryChipArtBounds finds for a real entry must be the expected literal; the chip pictures of that art fill their room
    private HudRect RealChip(string what, TestTables tables, TableRef root, List<string> rows, HudRect expected)
    {
        var compositor = new AppearanceCompositor(RealIcons.NewLibrary());
        Assert.True(LiveHud.TryChipArtBounds(compositor, tables, root, out HudRect? found), what);
        Assert.True(found is HudRect, $"{what}: no art found ({string.Join("; ", compositor.Library.Problems)})");
        HudRect art = found.Value;
        Assert.True(expected == art, $"{what}: art {art}, expected {expected}");
        var row = new System.Text.StringBuilder($"{what,-30} art {art.Width}x{art.Height} at ({art.Left},{art.Top})");
        foreach (float side in new[] { 24f, 40f, 50f, 60f, 80f })
        {
            var chip = new HudRect(1000, 300, side, side);
            HudRect picture = LiveHud.ChipArt(chip, art).Picture;
            Fills(picture, art, chip, $"{what} in {side}");
            if (side is 40f or 50f or 60f)
                row.Append($" | {side}: {picture.Width}x{picture.Height} at +({picture.Left - chip.Left},{picture.Top - chip.Top})");
        }
        rows.Add(row.ToString());
        return art;
    }

    // the opaque bounds of every frame of a state, read straight from tg's sheet (not through the compositor), in tg pixels from the top left of
    // its 32 x 32 square, and their union
    private static HudRect FramesUnion(string icon, string state)
    {
        Assert.True(RealIcons.NewLibrary().TryGetIcon(icon, out IIcon sheet));
        Assert.True(sheet.TryGetState(state, out IIconState frames));
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        for (int f = 0; f < frames.Frames; f++)
        {
            Raster frame = frames.Get(0, f);
            for (int y = 0; y < frame.Height; y++)
                for (int x = 0; x < frame.Width; x++)
                    if ((frame.Get(x, y) & 0xFF) != 0)
                        (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x), Math.Max(bottom, y));
        }
        return new HudRect(left, top, right - left + 1, bottom - top + 1);
    }

    private static HudRect Grown(HudRect r, float by) => new(r.Left - by, r.Top - by, r.Width + 2 * by, r.Height + 2 * by);

    private static HudRect Union(HudRect a, HudRect b)
    {
        float left = MathF.Min(a.Left, b.Left), top = MathF.Min(a.Top, b.Top);
        return new HudRect(left, top, MathF.Max(a.Right, b.Right) - left, MathF.Max(a.Bottom, b.Bottom) - top);
    }

    // composes a real entry as the HUD does (facing south, its own offset kept, at time 0) and checks that the first and last opaque column and
    // row of that picture, drawn where ChipPicture puts it, land on the edges of ChipArt's picture within a pixel
    private static void DrawnOnItsPicture(string what, TestTables tables, TableRef root, HudRect art)
    {
        var compositor = new AppearanceCompositor(RealIcons.NewLibrary());
        Assert.True(compositor.TryCompose(tables, root, ByondDir.South, 0, out ComposedRaster composed, rootOffset: true), what);
        Raster image = composed.Image;
        int first = int.MaxValue, last = int.MinValue, topRow = int.MaxValue, bottomRow = int.MinValue;
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                if ((image.Get(x, y) & 0xFF) != 0)
                    (first, last, topRow, bottomRow) = (Math.Min(first, x), Math.Max(last, x), Math.Min(topRow, y), Math.Max(bottomRow, y));
        foreach (float side in new[] { 40f, 50f })
        {
            var chip = new HudRect(1807, 411, side, side);
            HudRect picture = LiveHud.ChipArt(chip, art).Picture;
            HudRect drawn = LiveHud.ChipPicture(chip, art, image.Width, image.Height, composed.OriginX, composed.OriginY);
            float kx = drawn.Width / image.Width, ky = drawn.Height / image.Height;
            string at = $"{what} in {side} ({image.Width}x{image.Height}, origin {composed.OriginX},{composed.OriginY}; columns {first}..{last}, rows {topRow}..{bottomRow})";
            Assert.True(MathF.Abs(drawn.Left + first * kx - picture.Left) <= 1f, $"{at}: left edge {drawn.Left + first * kx}, picture {picture}");
            Assert.True(MathF.Abs(drawn.Left + (last + 1) * kx - picture.Right) <= 1f, $"{at}: right edge {drawn.Left + (last + 1) * kx}, picture {picture}");
            Assert.True(MathF.Abs(drawn.Top + topRow * ky - picture.Top) <= 1f, $"{at}: top edge {drawn.Top + topRow * ky}, picture {picture}");
            Assert.True(MathF.Abs(drawn.Top + (bottomRow + 1) * ky - picture.Bottom) <= 1f, $"{at}: bottom edge {drawn.Top + (bottomRow + 1) * ky}, picture {picture}");
        }
    }

    [RealIconsFact]
    public void TgsStatusAndAlertPicturesAreCentredAndLargeInTheirChips()
    {
        var rows = new List<string>();
        var tables = new TestTables();
        const string gen = "icons/hud/screen_gen.dmi", alerts = "icons/hud/screen_alert.dmi", midnight = "icons/hud/screen_midnight.dmi";

        // the hunger bar as the server sends it (read from a real server's replica; screen_objects.dm /atom/movable/screen/hunger): "hungerbar",
        // the burger under it 5 pixels left with a 1-pixel black outline (Initialize), the bar (its vis_contents, flattened into an overlay) over it
        // with its alpha mask from tg's hungerbar_mask, slid 9 down at 55% fullness
        TableRef burger = tables.Add("icons/obj/food/burgerbread.dmi", "hburger", e =>
        {
            e.PixelX = -5;
            e.AppearanceFlags = AppearanceFlag.KeepApart;
            e.Filters = new List<FilterEntry> { Outline(0x000000FF, 1, 1) };
        });
        TableRef bar = tables.Add(gen, "hungerbar_bar", e =>
        {
            e.Rgba = 0x3CFB00FF;
            e.Filters = new List<FilterEntry> { new() { Kind = FilterKind.Alpha, Y = -9, Icon = tables.Res(gen), IconState = tables.Str("hungerbar_mask") } };
        });
        TableRef hunger = tables.Add(gen, "hungerbar", e =>
        {
            e.Underlays.Add(burger);
            e.Overlays.Add(bar);
        });
        // from tg's sheets: the bar's frame "hungerbar" is x 20..24, y 5..26 (5 x 22 at (20,5)); the burger "hburger" x 8..23, y 8..22 (16 x 15 at
        // (8,8)), moved 5 left and grown by its outline: x 2..19, y 7..23. Together x 2..24, y 5..26
        Assert.Equal(new HudRect(20, 5, 5, 22), FramesUnion(gen, "hungerbar"));
        Assert.Equal(new HudRect(8, 8, 16, 15), FramesUnion("icons/obj/food/burgerbread.dmi", "hburger"));
        Assert.Equal(new HudRect(2, 5, 23, 22), Union(FramesUnion(gen, "hungerbar"), Grown(FramesUnion("icons/obj/food/burgerbread.dmi", "hburger") with { Left = 3 }, 1)));
        HudRect hungerArt = RealChip("hunger bar with burger and bar", tables, hunger, rows, new HudRect(2, 5, 23, 22));
        // starving: tg's red outline of size 2, pulsing from alpha 0 (screen_objects.dm:1253), counted at its full reach: 2 more on every side
        TableRef starving = tables.Add(gen, "hungerbar", e =>
        {
            e.Underlays.Add(burger);
            e.Overlays.Add(bar);
            e.Filters = new List<FilterEntry> { Outline(0xFF0033FF, 2, alpha: 0) };
        });
        RealChip("hunger bar starving (outline)", tables, starving, rows, new HudRect(0, 3, 27, 26));

        // single states, each literal read from tg's sheet over all its frames (FramesUnion, checked here too)
        void Sheet(string icon, string state, HudRect literal)
        {
            Assert.True(literal == FramesUnion(icon, state), $"{state}: the sheet's pixels are {FramesUnion(icon, state)}, the literal {literal}");
            RealChip(state, tables, tables.Add(icon, state), rows, literal);
        }
        // stamina in every state that shows a chip (living.dm update_stamina; not stamina_full), animated: the bolt x 8..23, y 2..30
        foreach (string state in new[] { "stamina_1", "stamina_2", "stamina_3", "stamina_4", "stamina_5", "stamina_crit", "stamina_dead" })
            Sheet(gen, state, new HudRect(8, 2, 16, 29));
        // internals, the suit cell, the sleep button (the human HUD's style)
        Sheet(gen, "internal0", new HudRect(4, 3, 27, 27));
        Sheet(gen, "internal1", new HudRect(4, 3, 25, 26));
        Sheet(gen, "internal2", new HudRect(1, 0, 30, 31));
        foreach (string state in new[] { "spacesuit_high", "spacesuit_low", "spacesuit_very_low", "spacesuit_empty", "spacesuit_missing" })
            Sheet(gen, state, new HudRect(3, 5, 26, 22));
        Sheet(midnight, "act_sleep", new HudRect(0, 17, 32, 15));
        // tg's alerts: low pressure, no oxygen (blinking: its two frames' union), cold
        Sheet(alerts, "lowpressure2", new HudRect(4, 1, 24, 30));
        Sheet(alerts, "not_enough_oxy", new HudRect(5, 1, 21, 30));
        Sheet(alerts, "cold3", new HudRect(7, 1, 18, 30));
        // handcuffed: the style's whole "template" square (0,0 32 x 32 in the sheet), the cuffs inside it as its overlay, tg's gold glow of 1 round it
        Assert.Equal(new HudRect(0, 0, 32, 32), FramesUnion(midnight, "template"));
        TableRef cuffs = tables.Add("icons/obj/weapons/restraints.dmi", "handcuff", e => { e.Layer = -1; e.AppearanceFlags = AppearanceFlag.KeepTogether; });
        RealChip("handcuffed", tables, tables.Add(midnight, "template", e =>
        {
            e.Overlays.Add(cuffs);
            e.Filters = new List<FilterEntry> { Outline(Gold, 1) };
        }), rows, new HudRect(-1, -1, 34, 34));

        // the draw mapping on real pictures: the hunger bar (its burger 5 left of tg's square widens the composed picture) and an alert
        DrawnOnItsPicture("hunger bar", tables, hunger, hungerArt);
        TableRef pressure = tables.Add(alerts, "lowpressure1");
        Assert.Equal(1, Frames(alerts, "lowpressure1")); // a still alert: the picture at time 0 is all of its art
        DrawnOnItsPicture("lowpressure1", tables, pressure, RealChip("lowpressure1", tables, pressure, rows, FramesUnion(alerts, "lowpressure1")));
        foreach (string row in rows)
            _output.WriteLine(row);
    }

    private static int Frames(string icon, string state)
    {
        Assert.True(RealIcons.NewLibrary().TryGetIcon(icon, out IIcon sheet));
        Assert.True(sheet.TryGetState(state, out IIconState frames));
        return frames.Frames;
    }
}
