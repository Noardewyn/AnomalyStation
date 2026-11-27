using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 12, slice 4A: the Tab inventory on the human HUD: the chat column, the character panel (the own sprite with tg's worn slots
/// round it), the sprite's clicks as the health doll's, the overlay closing on damage, and tg's inventory kept expanded.
/// </summary>
public class TabInventoryTests
{
    // tg's human HUD entries still drawn under Tab (hud/human.dm, __DEFINES/hud.dm): the combat toggle in the corner and the row left of it (the zone
    // selector, the memories button, the area creator); with the reduced vitals tg's right status column and sleep button
    private static readonly string[] TgBottomRight = { "EAST-1:28,SOUTH:5", "EAST-2:26,SOUTH:5", "EAST-3:24,SOUTH:5", "EAST-4:22,SOUTH:5" };

    private static readonly string[] TgStatus =
    {
        "EAST-1:28,CENTER-1:19", "EAST-1:28,CENTER:21", "EAST-1:2,CENTER:21", "EAST-1:28,CENTER-3:14", "EAST-1:28,CENTER+1:21", "EAST-1:28,CENTER-4:14",
        "EAST-1:28,SOUTH+1:41",
    };

    // tg's listed action buttons (action_group.dm ButtonNumberToScreenCoords: column c at WEST+c:(4 + 2 (c + 1)), row r at NORTH-r:-6, ten to a
    // row) and the palette under them (ui_action_palette_offset)
    private static IEnumerable<string> ActionButtons(int rows)
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < 10; c++)
                yield return $"WEST+{c}:{4 + 2 * (c + 1)},NORTH{(r == 0 ? "+0" : "-" + r)}:-6";
        yield return $"WEST+0:23,NORTH-{rows}:5";
    }

    private static ScreenLocSpec Parse(string loc) => ScreenLoc.TryParse(loc, out ScreenLocSpec spec) ? spec : throw new ArgumentException(loc);

    private static HudRect Place(string loc, HudMetrics metrics) => ScreenLoc.Place(Parse(loc), metrics, 32, 32, 0, 0, out _);

    private static List<(string Name, HudRect Rect)> TgDrawn(HudMetrics metrics, LiveHudLayout vitals, int actionRows)
        => TgBottomRight.Concat(vitals.Vitals ? Array.Empty<string>() : TgStatus).Concat(ActionButtons(actionRows)).Select(loc => (loc, Place(loc, metrics))).ToList();

    private static bool Overlaps(HudRect a, HudRect b) => a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    private static bool Inside(HudRect inner, HudRect outer) => inner.Left >= outer.Left && inner.Right <= outer.Right && inner.Top >= outer.Top && inner.Bottom <= outer.Bottom;

    private static bool Whole(HudRect r) => r.Left == MathF.Round(r.Left) && r.Top == MathF.Round(r.Top) && r.Width == MathF.Round(r.Width) && r.Height == MathF.Round(r.Height);

    private static TabInventoryLayout Reference(int actionRows = 0)
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        LiveHudLayout vitals = LiveHud.Layout(metrics, human: true);
        return TabInventory.Layout(metrics, LiveHud.Hands(metrics, true, false, tab: true), vitals, TgDrawn(metrics, vitals, actionRows).Select(t => t.Rect).ToList());
    }

    [Fact]
    public void AtTheReferenceSizeTheChatColumnTheSpriteAndTheTwelveCellsAreWhereTheDesignPutsThem()
    {
        TabInventoryLayout tab = Reference(actionRows: 1);
        // the chat column: the left 28 % (538 of 1920), from 8 under tg's action buttons (the first row and the palette button under it, its bottom at
        // 133) to 8 over the bottom row's top line (the active hand box's top, 992)
        Assert.Equal(new HudRect(0, 141, 538, 843), tab.Chat);
        // the panel centred in the middle column (538 to 1229) between the same lines: the sprite at 6 screen pixels per tg pixel (192 across) ...
        Assert.Equal(6, tab.SpriteScale);
        Assert.Equal(new HudRect(787, 518, 192, 192), tab.Sprite);
        Assert.Equal(1f, tab.CellUnit);
        // ... and the cells of the bar's size (48), 4 apart and 12 from the sprite: above it the ears, the head and the eyes, under them the mask and
        // the neck; left of it the suit, the uniform and the gloves; right of it the back, suit storage and the belt; under it the shoes
        HudRect[] expected =
        {
            new(807, 354, 48, 48), new(859, 354, 48, 48), new(911, 354, 48, 48), // ears, head, eyes
            new(859, 406, 48, 48), new(859, 458, 48, 48), // mask, neck
            new(727, 538, 48, 48), new(727, 590, 48, 48), new(727, 642, 48, 48), // suit, uniform, gloves
            new(991, 538, 48, 48), new(991, 590, 48, 48), new(991, 642, 48, 48), // back, suit storage, belt
            new(859, 722, 48, 48), // shoes
        };
        Assert.Equal(expected, tab.Cells);
        // with no action buttons the chat and the panel stand where they do with one row (under the palette button's place), so they do not jump when
        // the first button comes; a second row (the palette under it) moves the chat's top down
        TabInventoryLayout none = Reference(actionRows: 0), two = Reference(actionRows: 2);
        Assert.Equal((tab.Chat, tab.Sprite), (none.Chat, none.Sprite));
        Assert.Equal(tab.Cells, none.Cells);
        Assert.True(two.Chat.Top > tab.Chat.Top && two.Chat.Bottom == tab.Chat.Bottom, $"{two.Chat}");
    }

    [Fact]
    public void EachWornSlotHasItsCellAndThePocketsAndTheIdSlotStayBarCells()
    {
        // tg's ITEM_SLOT_* bits in the panel's order
        uint[] order = { 1u << 4, 1u << 6, 1u << 3, 1u << 5, 1u << 12, 1u << 0, 1u << 1, 1u << 2, 1u << 10, 1u << 14, 1u << 9, 1u << 7 };
        for (int i = 0; i < order.Length; i++)
        {
            Assert.Equal(i, TabInventory.CellOf(order[i]));
            Assert.Equal(0, LiveHud.BarCellOfSlot(order[i]));
        }
        Assert.Equal(TabInventory.CellCount, order.Length);
        // the pockets and the ID slot are the bar's cells 8, 9 and 10, never repeated in the panel
        foreach ((uint slot, int cell) in new[] { (1u << 15, 8), (1u << 16, 9), (1u << 8, 10) })
        {
            Assert.Equal(-1, TabInventory.CellOf(slot));
            Assert.Equal(cell, LiveHud.BarCellOfSlot(slot));
        }
        Assert.Equal(-1, TabInventory.CellOf(0));
        Assert.Equal(-1, TabInventory.CellOf(1u << 11)); // a slot the panel has no cell for (tg's ITEM_SLOT_DEX_STORAGE) is drawn nowhere
        // under Tab only, on the human HUD; on another HUD a slot keeps tg's place, always drawn
        Assert.False(LiveHud.Shown(LiveHud.Placed(LiveHudRole.WornSlot, humanHud: true), cursorMode: false));
        Assert.True(LiveHud.Shown(LiveHud.Placed(LiveHudRole.WornSlot, humanHud: true), cursorMode: true));
        Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(LiveHudRole.WornSlot, humanHud: false));
        Assert.True(LiveHud.Shown(LiveHud.Placed(LiveHudRole.WornSlot, humanHud: false), cursorMode: false));
        // a worn slot's cell: none without the panel (first person) or for a slot it has no cell for
        TabInventoryLayout tab = Reference();
        Assert.Equal(tab.Cells[11], tab.CellAt(TabInventory.CellOf(1u << 7)));
        Assert.Equal(default, tab.CellAt(-1));
        Assert.Equal(default, default(TabInventoryLayout).CellAt(0));
    }

    [Theory]
    [MemberData(nameof(LiveHudTests.HandScreens), MemberType = typeof(LiveHudTests))]
    public void UnderTabTheChatAndThePanelAreOnTheScreenAtWholePixelsAndClearOfEverythingElse(int width, int height, float scale)
    {
        var metrics = new HudMetrics(width, height, scale);
        var screen = new HudRect(0, 0, width, height);
        LiveHudLayout vitals = LiveHud.Layout(metrics, human: true);
        float u = UiTokens.Unit(metrics);
        List<HudRect> anyChip = Enumerable.Range(0, LiveHud.MaxChips + 1).SelectMany(n => LiveHud.AlertStack(metrics, n, vitals)).Distinct().ToList();
        float split = MathF.Round(UiTokens.OverlayLeftShare * width), end = MathF.Round(UiTokens.OverlayRightShare * width);
        foreach (int actionRows in new[] { 0, 1, 2 })
        {
            List<(string Name, HudRect Rect)> tg = TgDrawn(metrics, vitals, actionRows);
            TabInventoryLayout layout = default;
            foreach ((bool leftActive, bool rightActive) in new[] { (true, false), (false, true), (false, false) })
            {
                HandsLayout hands = LiveHud.Hands(metrics, leftActive, rightActive, vitals, 300f * u, tab: true);
                string at = $"{width}x{height} at {scale}, {actionRows} action rows, active {(leftActive ? "left" : rightActive ? "right" : "neither")}";
                TabInventoryLayout tab = TabInventory.Layout(metrics, hands, vitals, tg.Select(t => t.Rect).ToList());
                // nothing of it moves with a swap of hands
                if (layout.Cells != null)
                {
                    Assert.Equal(layout.Chat, tab.Chat);
                    Assert.Equal(layout.Sprite, tab.Sprite);
                    Assert.Equal(layout.Cells, tab.Cells);
                }
                layout = tab;
                Assert.True(tab.Shown, $"{at}: the panel left out");
                Assert.True(tab.Chat.Width > 0 && tab.Chat.Height > 0, $"{at}: no chat column");
                // the chat at the left edge, 28 % wide; the panel in the middle column
                Assert.Equal(0f, tab.Chat.Left);
                Assert.Equal(split, tab.Chat.Right);
                var ours = new List<(string Name, HudRect Rect)> { ("chat", tab.Chat), ("sprite", tab.Sprite) };
                ours.AddRange(tab.Cells.Select((c, i) => ($"cell {i}", c)));
                foreach ((string name, HudRect rect) in ours.Skip(1))
                    Assert.True(rect.Left >= split && rect.Right <= end, $"{at}: {name} {rect} out of the middle column {split}..{end}");
                // the sprite a whole number of screen pixels per tg pixel; the cells square, of the bar's size unless the sprite is at 1 already
                Assert.True(tab.SpriteScale >= 1 && tab.Sprite.Width == 32 * tab.SpriteScale && tab.Sprite.Height == tab.Sprite.Width, $"{at}: sprite {tab.Sprite} at {tab.SpriteScale}");
                float bar = LiveHud.BarCell(hands, 1).Width;
                foreach (HudRect cell in tab.Cells)
                {
                    // (where the bar's own cells are smaller than the least the panel shrinks to, a 4:3 screen at the largest scales, they are the bar's)
                    Assert.True(cell.Width == cell.Height && cell.Width >= Math.Min(bar, UiTokens.TabCellMinPixels) && cell.Width <= bar, $"{at}: cell {cell}");
                    Assert.True(cell.Width == bar || tab.SpriteScale == 1, $"{at}: cells made smaller with the sprite at {tab.SpriteScale}");
                }
                // the bar, the hands, the vitals and every chip's place, ours too
                ours.AddRange(new[] { ("left hand", hands.Left), ("right hand", hands.Right), ("hotbar", hands.Hotbar), ("doll", vitals.Doll) });
                if (vitals.Vitals)
                    ours.AddRange(new[] { ("health", vitals.Health), ("mood", vitals.Mood) });
                ours.AddRange(Enumerable.Range(1, HotbarMessage.CellCount).Select(c => ($"bar cell {c}", LiveHud.BarCell(hands, c))));
                foreach ((string name, HudRect rect) in ours.Take(2 + TabInventory.CellCount))
                {
                    Assert.True(Inside(rect, screen) && Whole(rect), $"{at}: {name} {rect} off the screen or not at whole pixels");
                    foreach ((string loc, HudRect tgRect) in tg)
                        Assert.False(Overlaps(rect, tgRect), $"{at}: {name} {rect} over tg's {loc} {tgRect}");
                    foreach (HudRect chip in anyChip)
                        Assert.False(Overlaps(rect, chip), $"{at}: {name} {rect} over chip {chip}");
                }
                for (int i = 0; i < 2 + TabInventory.CellCount; i++)
                    for (int j = i + 1; j < ours.Count; j++)
                        Assert.False(Overlaps(ours[i].Rect, ours[j].Rect), $"{at}: {ours[i].Name} {ours[i].Rect} over {ours[j].Name} {ours[j].Rect}");
                // first person: nothing of it (no docked chat; the slots are not drawn: LiveHud.Shown)
                Assert.Null(TabInventory.ChatDock(false, true, tab));
                Assert.Equal(tab.Chat, TabInventory.ChatDock(true, true, tab));
                Assert.Null(TabInventory.ChatDock(true, false, tab)); // another HUD: the chat stays where it is
            }
        }
    }

    public static IEnumerable<object[]> PortraitScreens()
    {
        foreach ((int w, int h) in new[] { (640, 990), (600, 1024), (720, 1280), (768, 1024), (800, 1280), (900, 1600), (1080, 1920) })
        {
            for (int percent = 50; percent <= 300; percent += 5)
                yield return new object[] { w, h, percent / 100f };
        }
    }

    // On a portrait screen the chips left of the vitals reach into the left columns: whatever of the Tab inventory is laid out there (the chat column,
    // or none where nothing is left for it; the panel, or none) is on the screen at whole pixels and clear of the vitals, every place a chip can take
    // (twelve chips), tg's entries still drawn, the bottom row and each other.
    [Theory]
    [MemberData(nameof(PortraitScreens))]
    public void OnAPortraitScreenTheChatAndThePanelKeepClearOfTheVitalsAndEveryChip(int width, int height, float scale)
    {
        var metrics = new HudMetrics(width, height, scale);
        var screen = new HudRect(0, 0, width, height);
        LiveHudLayout vitals = LiveHud.Layout(metrics, human: true);
        List<HudRect> anyChip = Enumerable.Range(0, LiveHud.MaxChips + 1).SelectMany(n => LiveHud.AlertStack(metrics, n, vitals)).Distinct().ToList();
        var others = anyChip.Select((c, i) => ($"chip place {i}", c)).ToList();
        others.Add(("doll", vitals.Doll));
        if (vitals.Vitals)
            others.AddRange(new[] { ("health", vitals.Health), ("mood", vitals.Mood) });
        foreach (int actionRows in new[] { 0, 1, 2 })
        {
            List<(string Name, HudRect Rect)> tg = TgDrawn(metrics, vitals, actionRows);
            HandsLayout hands = LiveHud.Hands(metrics, true, false, vitals, tab: true);
            TabInventoryLayout tab = TabInventory.Layout(metrics, hands, vitals, tg.Select(t => t.Rect).ToList());
            string at = $"{width}x{height} at {scale}, {actionRows} action rows";
            var ours = new List<(string Name, HudRect Rect)>();
            if (tab.Chat.Width > 0 || tab.Chat.Height > 0)
            {
                Assert.True(tab.Chat.Width > 0 && tab.Chat.Height > 0, $"{at}: chat {tab.Chat}");
                ours.Add(("chat", tab.Chat));
                Assert.Equal(tab.Chat, TabInventory.ChatDock(true, true, tab));
            }
            else
                Assert.Null(TabInventory.ChatDock(true, true, tab)); // no room: the chat stays at its own place
            if (tab.Shown)
                ours.AddRange(tab.Cells.Select((c, i) => ($"cell {i}", c)).Prepend(("sprite", tab.Sprite)));
            var row = new[] { ("left hand", hands.Left), ("right hand", hands.Right), ("hotbar", hands.Hotbar) };
            foreach ((string name, HudRect rect) in ours)
            {
                Assert.True(Inside(rect, screen) && Whole(rect), $"{at}: {name} {rect}");
                foreach ((string other, HudRect o) in others.Concat(tg).Concat(row))
                    Assert.False(Overlaps(rect, o), $"{at}: {name} {rect} over {other} {o}");
            }
            for (int i = 0; i < ours.Count; i++)
                for (int j = i + 1; j < ours.Count; j++)
                    Assert.False(Overlaps(ours[i].Rect, ours[j].Rect), $"{at}: {ours[i].Name} {ours[i].Rect} over {ours[j].Name} {ours[j].Rect}");
        }
    }

    [Fact]
    public void OnTheTestersPortraitScreenTheChatKeepsClearOfTheChipLeftOfTheVitals()
    {
        // 640 x 990 at 220 %, no action buttons: the twelfth chip's place (177, 262, 81 x 81) stands over the chat column's right edge (179)
        var metrics = new HudMetrics(640, 990, 2.2f);
        LiveHudLayout vitals = LiveHud.Layout(metrics, human: true);
        List<HudRect> chips = LiveHud.AlertStack(metrics, LiveHud.MaxChips, vitals);
        var chip = new HudRect(177, 262, 81, 81);
        Assert.Contains(chip, chips);
        TabInventoryLayout tab = TabInventory.Layout(metrics, LiveHud.Hands(metrics, true, false, vitals, tab: true), vitals, TgDrawn(metrics, vitals, 0).Select(t => t.Rect).ToList());
        Assert.False(Overlaps(tab.Chat, chip), $"chat {tab.Chat} over chip {chip}");
        foreach (HudRect c in chips)
            Assert.False(Overlaps(tab.Chat, c), $"chat {tab.Chat} over chip {c}");
        // the column still has room under the chip: docked under it, the full 28 % wide
        Assert.True(tab.Chat.Height > 0 && tab.Chat.Top >= chip.Bottom && tab.Chat.Right == 179, $"{tab.Chat}");
    }

    [Fact]
    public void TheInventoryKeyRunsTabsOwnAction()
    {
        // one handler: the inventory key's press is cursor mode's action, whatever state the game is in (the dev menu's focus included)
        Assert.Equal(PlayAction.CursorMode, TabInventory.KeyAction(PlayAction.Inventory));
        Assert.Equal(PlayAction.CursorMode, TabInventory.KeyAction(PlayAction.CursorMode));
        foreach (PlayAction other in Enum.GetValues<PlayAction>().Where(a => a is not PlayAction.Inventory))
            Assert.Equal(other, TabInventory.KeyAction(other));
        Assert.Null(TabInventory.KeyAction(null));
        // the default keys: I and Tab both come to that action
        KeyBindings keys = KeyBindings.Defaults();
        Assert.Equal(PlayAction.CursorMode, TabInventory.KeyAction(keys.ActionOfPress("I", false)));
        Assert.Equal(PlayAction.CursorMode, TabInventory.KeyAction(keys.ActionOfPress("Tab", false)));
    }

    [Fact]
    public void ThePanelStepsTheSpriteDownThenShrinksTheCellsWhereItDoesNotFitTheHeight()
    {
        // a low wide screen at a large scale: the sprite at less than its scale for the unit, the cells smaller only once the sprite is at 1
        var seen = new HashSet<string>();
        foreach (object[] row in LiveHudTests.HandScreens())
        {
            var metrics = new HudMetrics((int)row[0], (int)row[1], (float)row[2]);
            HandsLayout hands = LiveHud.Hands(metrics, true, false, tab: true);
            TabInventoryLayout tab = TabInventory.Layout(metrics, hands, LiveHud.Layout(metrics, human: true), Array.Empty<HudRect>());
            int natural = Math.Max(1, (int)Math.Round(UiTokens.TabSpritePixels * UiTokens.Unit(metrics), MidpointRounding.AwayFromZero));
            Assert.True(tab.SpriteScale <= natural);
            if (tab.SpriteScale < natural)
                seen.Add("stepped");
            if (tab.Cells[0].Width < LiveHud.BarCell(hands, 1).Width)
            {
                Assert.Equal(1, tab.SpriteScale);
                seen.Add("shrunk");
            }
        }
        Assert.Contains("stepped", seen);
    }

    // a body seen from the front on tg's 32 x 32 square: the head rows 23 to 29 (columns 12 to 20), the chest rows 10 to 22 (12 to 20), the right arm
    // (the viewer's left) columns 8 to 11 and the left arm 21 to 24 on rows 12 to 22, the right leg columns 12 to 15 and the left leg 17 to 20 on rows
    // 1 to 9; everything else transparent
    private static AimZone[] Body()
    {
        var onTop = new AimZone[32 * 32];
        void Fill(int x0, int x1, int y0, int y1, AimZone zone)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    onTop[x - 1 + (y - 1) * 32] = zone;
        }
        Fill(12, 20, 23, 29, AimZone.Head);
        Fill(12, 20, 10, 22, AimZone.Chest);
        Fill(8, 11, 12, 22, AimZone.RightArm);
        Fill(21, 24, 12, 22, AimZone.LeftArm);
        Fill(12, 15, 1, 9, AimZone.RightLeg);
        Fill(17, 20, 1, 9, AimZone.LeftLeg);
        return onTop;
    }

    private sealed class Surface(List<HudDrawn> drawn) : IHudSurface
    {
        public void Collect(List<HudDrawn> bottomToTop) => bottomToTop.AddRange(drawn);
    }

    [Fact]
    public void APointOnTheSpriteIsThePartUnderItAndATransparentPixelIsNoClickOnIt()
    {
        AimZone[] body = Body();
        BodyZoneMap map = BodyZoneMap.Build(body, ByondDir.South);
        var picture = new Raster(32, 32);
        for (int y = 1; y <= 32; y++)
            for (int x = 1; x <= 32; x++)
                picture.Pixels[((32 - y) * 32 + x - 1) * 4 + 3] = body[x - 1 + (y - 1) * 32] != AimZone.None ? (byte)255 : (byte)0;
        TabInventoryLayout tab = Reference();
        // as HudLayer reports it: the picture over the sprite's square (a 32 x 32 picture fills it)
        HudRect drawn = ScreenLoc.FitIcon(tab.Sprite, 32, 32, 0, 0);
        Assert.Equal(tab.Sprite, drawn);
        var picker = new HudPicker(new Surface(new List<HudDrawn> { new(TabInventory.SpriteId, drawn, picture, 0, 0) }));
        float k = tab.SpriteScale;
        PickHit At(int x, int y) => picker.PickScreen(drawn.Left + (x - 0.5f) * k, drawn.Top + (32 - y + 0.5f) * k);
        foreach ((int x, int y, AimZone zone) in new[]
        {
            (16, 28, AimZone.Head), (16, 26, AimZone.Eyes), (14, 26, AimZone.Eyes), (16, 24, AimZone.Mouth), (16, 18, AimZone.Chest), (16, 11, AimZone.Groin),
            (9, 15, AimZone.RightArm), (23, 15, AimZone.LeftArm), (13, 5, AimZone.RightLeg), (18, 5, AimZone.LeftLeg),
        })
        {
            PickHit hit = At(x, y);
            Assert.Equal(TabInventory.SpriteId, hit.Id);
            Assert.Equal(TargetKind.Screen, hit.Kind);
            Assert.Equal(((byte)x, (byte)y), (hit.IconX, hit.IconY));
            Assert.Equal(zone, map.At(hit.IconX, hit.IconY));
        }
        // between the legs, beside the head, over it: no click on the sprite
        foreach ((int x, int y) in new[] { (16, 5), (5, 28), (16, 31), (26, 15) })
            Assert.False(At(x, y).IsHit, $"{x},{y}");
        // the pointer's prompt names the part
        Assert.Equal("Eyes", TabInventory.PartName(map.At(16, 26)));
        Assert.Equal("Groin", TabInventory.PartName(map.At(16, 11)));
        Assert.Equal("Left arm", TabInventory.PartName(map.At(23, 15)));
        Assert.Equal("Right leg", TabInventory.PartName(map.At(13, 5)));
        Assert.Null(TabInventory.PartName(map.At(16, 5)));
    }

    [Fact]
    public void APictureLargerThanTheSquareIsPickedOnlyWhereItIsDrawnAtTheIconPixelShownThere()
    {
        // a 64 x 64 picture (a long held item, a big hat) with its icon square at (16, 16) from its lower left: the body in the square, and opaque
        // pixels all round it outside the square, which the panel cuts off when it draws the sprite
        AimZone[] body = Body();
        BodyZoneMap map = BodyZoneMap.Build(body, ByondDir.South);
        const int origin = 16, size = 64;
        var picture = new Raster(size, size);
        for (int fromBottom = 0; fromBottom < size; fromBottom++)
            for (int col = 0; col < size; col++)
            {
                int x = col - origin + 1, y = fromBottom - origin + 1; // tg's icon pixel
                bool inSquare = x is >= 1 and <= 32 && y is >= 1 and <= 32;
                bool opaque = inSquare ? body[x - 1 + (y - 1) * 32] != AimZone.None : true;
                picture.Pixels[((size - 1 - fromBottom) * size + col) * 4 + 3] = opaque ? (byte)255 : (byte)0;
            }
        Raster pick = TabInventory.SpritePick(picture, origin, origin);
        Assert.Equal((32, 32), (pick.Width, pick.Height));
        TabInventoryLayout tab = Reference();
        // reported as HudLayer reports it: the square alone, over the sprite's square
        var picker = new HudPicker(new Surface(new List<HudDrawn> { new(TabInventory.SpriteId, tab.Sprite, pick, 0, 0) }));
        float k = tab.SpriteScale;
        // the whole picture as drawn before the cut would stand at FitIcon's place, reaching 16 tg pixels past the square on each side
        HudRect whole = ScreenLoc.FitIcon(tab.Sprite, size, size, origin, origin);
        Assert.True(whole.Left < tab.Sprite.Left && whole.Right > tab.Sprite.Right);
        PickHit At(float x, float y) => picker.PickScreen(tab.Sprite.Left + (x - 0.5f) * k, tab.Sprite.Top + (32 - y + 0.5f) * k);
        // outside the square, on the picture's opaque pixels that are not drawn: no click on the sprite (left, right, above, below, a corner)
        foreach ((float x, float y) in new[] { (-3f, 15f), (36f, 15f), (16f, 35f), (16f, -2f), (-5f, -5f) })
            Assert.False(At(x, y).IsHit, $"{x},{y}");
        // inside it: the icon pixel drawn there and its zone, never one clamped to the square's edge
        foreach ((int x, int y, AimZone zone) in new[] { (16, 26, AimZone.Eyes), (16, 18, AimZone.Chest), (9, 15, AimZone.RightArm), (23, 15, AimZone.LeftArm), (18, 5, AimZone.LeftLeg) })
        {
            PickHit hit = At(x, y);
            Assert.Equal(TabInventory.SpriteId, hit.Id);
            Assert.Equal(((byte)x, (byte)y), (hit.IconX, hit.IconY));
            // (the pointer's prompt is named from this same hit's icon pixel: GameScene.HudTooltip reads the hover the picker gives)
            Assert.Equal(zone, map.At(hit.IconX, hit.IconY));
        }
        // a transparent pixel in the square stays no click, though the picture is opaque just outside it
        Assert.False(At(2, 30).IsHit);
        // a picture smaller than the square, or set off in it: the square's uncovered pixels are transparent
        var small = new Raster(8, 8);
        Array.Fill(small.Pixels, (byte)255);
        Raster smallPick = TabInventory.SpritePick(small, 0, 0);
        Assert.Equal(255, smallPick.Pixels[((32 - 1) * 32 + 0) * 4 + 3]); // icon pixel (1, 1)
        Assert.Equal(0, smallPick.Pixels[((32 - 9) * 32 + 0) * 4 + 3]); // icon pixel (1, 9): above the picture
        Assert.Null(TabInventory.SpritePick(null, 0, 0));
    }

    [Fact]
    public void AClickOnTheSpriteIsTheDollsAnItemUsedOnOneselfOrTheGraspOfTheLimb()
    {
        var sprite = new ClickTarget(TargetKind.Screen, TabInventory.SpriteId, 0, 0, 0, 16, 26);
        const long own = 99, doll = 40;
        // an item in the active hand: a click on the own mob aimed at the part clicked, the nine zones
        foreach (AimZone zone in new[] { AimZone.Head, AimZone.Eyes, AimZone.Mouth, AimZone.Chest, AimZone.Groin, AimZone.LeftArm, AimZone.RightArm, AimZone.LeftLeg, AimZone.RightLeg })
            Assert.Equal(new ClickTarget(TargetKind.Entity, own, 0, 0, 0, 0, 0, zone), TabInventory.SelfClick(sprite, ClickModifiers.None, true, own, doll, zone));
        // an empty hand: tg's self-check, the click on the health doll
        Assert.Equal(new ClickTarget(TargetKind.Screen, doll, 0, 0, 0, 16, 16), TabInventory.SelfClick(sprite, ClickModifiers.None, false, own, doll, AimZone.Chest));
        // Ctrl+click: the own mob's ctrl-click at the limb clicked (tg grasps it; the eyes and the mouth are the head's, the groin the chest's), item or not
        Assert.Equal(new ClickTarget(TargetKind.Entity, own, 0, 0, 0, 0, 0, AimZone.Head), TabInventory.SelfClick(sprite, ClickModifiers.Ctrl, false, own, doll, AimZone.Eyes));
        Assert.Equal(new ClickTarget(TargetKind.Entity, own, 0, 0, 0, 0, 0, AimZone.Chest), TabInventory.SelfClick(sprite, ClickModifiers.Ctrl, true, own, doll, AimZone.Groin));
        Assert.Equal(new ClickTarget(TargetKind.Entity, own, 0, 0, 0, 0, 0, AimZone.LeftLeg), TabInventory.SelfClick(sprite, ClickModifiers.Ctrl, true, own, doll, AimZone.LeftLeg));
        // any other modifier is the doll's click (Shift examines it, as on the doll)
        Assert.Equal(new ClickTarget(TargetKind.Screen, doll, 0, 0, 0, 16, 16), TabInventory.SelfClick(sprite, ClickModifiers.Shift, true, own, doll, AimZone.Chest));
        // with no doll and an empty hand nothing is sent; with no own mob yet nothing reaches it
        Assert.False(TabInventory.SelfClick(sprite, ClickModifiers.None, false, own, 0, AimZone.Chest).IsSet);
        Assert.Equal(new ClickTarget(TargetKind.Screen, doll, 0, 0, 0, 16, 16), TabInventory.SelfClick(sprite, ClickModifiers.None, true, 0, doll, AimZone.Chest));
        // any other target is kept
        var slot = new ClickTarget(TargetKind.Screen, 41, 0, 0, 0, 16, 16);
        Assert.Equal(slot, TabInventory.SelfClick(slot, ClickModifiers.None, true, own, doll, AimZone.Chest));
        // the sprite is ours: the server is never told of it
        Assert.True(LiveHud.IsOurs(sprite));
        Assert.False(LiveHud.IsOurs(slot));
    }

    [Fact]
    public void TheOverlayClosesOnDamageUnlessThePlayerTypesOrAWindowNeedsThePointer()
    {
        Assert.True(TabInventory.CloseOnDamage(damaged: true, cursorMode: true, typing: false, pointerWindow: false));
        Assert.False(TabInventory.CloseOnDamage(damaged: true, cursorMode: true, typing: true, pointerWindow: false));
        Assert.False(TabInventory.CloseOnDamage(damaged: true, cursorMode: true, typing: false, pointerWindow: true));
        Assert.False(TabInventory.CloseOnDamage(damaged: true, cursorMode: true, typing: true, pointerWindow: true));
        Assert.False(TabInventory.CloseOnDamage(damaged: true, cursorMode: false, typing: false, pointerWindow: false)); // first person: nothing to close
        Assert.False(TabInventory.CloseOnDamage(damaged: false, cursorMode: true, typing: false, pointerWindow: false)); // no damage, no close
    }

    [Fact]
    public void TheChatIsDockedUnderTabAndBackAtItsOwnPlaceAfter()
    {
        TabInventoryLayout tab = Reference();
        var own = new HudRect(12, 600, 700, 340); // its default place or the player's
        // under Tab on the human HUD: the left column, whatever its own place
        Assert.Equal(tab.Chat, ChatPlace.Docked(own, TabInventory.ChatDock(true, true, tab)));
        // Tab off: its own place again, unchanged
        Assert.Equal(own, ChatPlace.Docked(own, TabInventory.ChatDock(false, true, tab)));
        // another HUD, or a column with no room: its own place
        Assert.Equal(own, ChatPlace.Docked(own, TabInventory.ChatDock(true, false, tab)));
        Assert.Equal(own, ChatPlace.Docked(own, TabInventory.ChatDock(true, true, tab with { Chat = default })));
    }

    [Fact]
    public void TgsInventoryIsExpandedOnceWheneverItShowsCollapsed()
    {
        var expander = new InventoryExpander();
        Assert.Equal(0, expander.Next(0, 0)); // no toggle (another HUD)
        Assert.Equal(7, expander.Next(7, 0)); // collapsed: one click
        Assert.Equal(0, expander.Next(7, 0)); // still collapsed while the click is on its way: no second click
        Assert.Equal(0, expander.Next(0, 7)); // expanded
        Assert.Equal(7, expander.Next(7, 0)); // collapsed again by the server: one click again
        Assert.Equal(9, expander.Next(9, 0)); // a new HUD's toggle
        Assert.Equal(0, expander.Next(9, 0));
    }
}
