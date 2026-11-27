using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 10, slice 3A (DV603) on the client: the hotbar's state from the server's message (<see cref="HotbarModel"/>), the ten hotbar
/// keys (1 to 9 and 0, the number row and the keypad) with tg's action buttons on Shift and a number, an old options file keeping its keys, and a
/// press sent as a HotbarPress of that cell alone.
/// </summary>
public class HotbarClientTests
{
    // section 16: a cell draws tg's slot picture with no rim of ours over it, but the red one of a refused press
    private const uint NoRim = 0u;

    private static HotbarMessage Message(byte refused = 0, string left = "wrench", bool belt = true)
    {
        var cells = Enumerable.Repeat(HotbarCell.Empty, HotbarMessage.CellCount).ToArray();
        cells[2] = new HotbarCell(HotbarCellState.Out, new TableRef(12, true));
        cells[4] = new HotbarCell(HotbarCellState.Holding, new TableRef(13, true));
        cells[9] = new HotbarCell(HotbarCellState.Holding, new TableRef(14, true));
        return new HotbarMessage { Bar = true, BeltWorn = belt, Cells = cells, LeftHand = left, RefusedCell = refused };
    }

    // ---- slice 3B: the bar ----

    [Fact]
    public void ACellShowsEmptyHoldingAndOutAndWithNoBeltTheBeltsCellsAreHalfSeenEmptyCells()
    {
        var model = new HotbarModel();
        model.Apply(Message(), refusals: 0, refusedCell: 0, now: 1.0);
        KeyBindings keys = KeyBindings.Defaults();
        uint rim = NoRim;
        // empty: a dim key cap, no picture, and no rim over tg's slot picture (section 16); holding: the item's picture; out (in a hand): the picture at about a third
        Assert.Equal(new HotbarCellLook(1f, TableRef.None, 0f, "1", UiTokens.HotbarEmptyCapAlpha, rim), LiveHud.CellLook(model, 1, keys, 1.0));
        Assert.Equal(new HotbarCellLook(1f, new TableRef(13, true), 1f, "5", 1f, rim), LiveHud.CellLook(model, 5, keys, 1.0));
        Assert.Equal(new HotbarCellLook(1f, new TableRef(12, true), UiTokens.HotbarOutAlpha, "3", 1f, rim), LiveHud.CellLook(model, 3, keys, 1.0));
        Assert.Equal(new HotbarCellLook(1f, new TableRef(14, true), 1f, "0", 1f, rim), LiveHud.CellLook(model, 10, keys, 1.0)); // the ID slot's key 0
        Assert.InRange(UiTokens.HotbarOutAlpha, 0.3f, 0.37f);
        Assert.InRange(UiTokens.HotbarEmptyCapAlpha, 0.2f, 0.6f);
        Assert.NotEqual(UiTokens.HotbarRefusedRim, rim);
        // no belt worn: the seven belt cells are empty cells at half opacity, whatever a stale cell says; the pockets and the ID slot as they are
        model.Apply(Message(belt: false), refusals: 0, refusedCell: 0, now: 2.0);
        for (int c = 1; c <= HotbarMessage.BeltCells; c++)
            Assert.Equal(new HotbarCellLook(UiTokens.HotbarNoBeltAlpha, TableRef.None, 0f, c.ToString(System.Globalization.CultureInfo.InvariantCulture), UiTokens.HotbarEmptyCapAlpha, rim),
                LiveHud.CellLook(model, c, keys, 2.0));
        Assert.Equal(0.5f, UiTokens.HotbarNoBeltAlpha);
        Assert.Equal(new HotbarCellLook(1f, new TableRef(14, true), 1f, "0", 1f, rim), LiveHud.CellLook(model, 10, keys, 2.0));
        Assert.Equal(1f, LiveHud.CellLook(model, 8, keys, 2.0).Alpha);
    }

    [Fact]
    public void ARefusedCellsRimIsRedForAThirdOfASecondFromTheRefusalAndASecondRefusalStartsItAgain()
    {
        var model = new HotbarModel();
        model.Apply(Message(), refusals: 0, refusedCell: 0, now: 1.0);
        KeyBindings keys = KeyBindings.Defaults();
        uint Rim(int cell, double now) => LiveHud.CellLook(model, cell, keys, now).Rim;
        Assert.Equal(NoRim, Rim(9, 1.0)); // nothing refused yet
        model.Apply(Message(), refusals: 1, refusedCell: 9, now: 2.0);
        Assert.InRange(UiTokens.HotbarRefusedSeconds, 0.3, 0.37);
        Assert.Equal(NoRim, Rim(9, 1.99)); // not before it
        Assert.Equal(UiTokens.HotbarRefusedRim, Rim(9, 2.0));
        Assert.Equal(UiTokens.HotbarRefusedRim, Rim(9, 2.3));
        Assert.Equal(NoRim, Rim(9, 2.34)); // over about a third of a second later
        Assert.Equal(NoRim, Rim(8, 2.1)); // only the refused cell
        // the refusal's look keeps the cell's state (an empty cell stays empty)
        Assert.Equal(LiveHud.CellLook(model, 9, keys, 3.0) with { Rim = UiTokens.HotbarRefusedRim }, LiveHud.CellLook(model, 9, keys, 2.1));
        // a second refusal of the same cell starts the blink again
        model.Apply(Message(), refusals: 2, refusedCell: 9, now: 2.2);
        Assert.Equal(UiTokens.HotbarRefusedRim, Rim(9, 2.4)); // 0.4 after the first, 0.2 after the second
        Assert.Equal(NoRim, Rim(9, 2.54));
        // a refusal of another cell moves the blink there
        model.Apply(Message(), refusals: 3, refusedCell: 2, now: 3.0);
        Assert.Equal(UiTokens.HotbarRefusedRim, Rim(2, 3.1));
        Assert.Equal(NoRim, Rim(9, 3.1));
    }

    [Fact]
    public void ARefusalKeepsTheCellsOutPictureAndTheNoBeltCellsHalfOpacity()
    {
        KeyBindings keys = KeyBindings.Defaults();
        // cell 3 is out (its item in a hand): refused, its rim is red and its picture stays at a third
        var model = new HotbarModel();
        model.Apply(Message(), refusals: 0, refusedCell: 0, now: 1.0);
        model.Apply(Message(), refusals: 1, refusedCell: 3, now: 2.0);
        Assert.Equal(new HotbarCellLook(1f, new TableRef(12, true), UiTokens.HotbarOutAlpha, "3", 1f, UiTokens.HotbarRefusedRim), LiveHud.CellLook(model, 3, keys, 2.1));
        // a belt cell with no belt worn: refused, its rim is red at half opacity, an empty cell still
        var noBelt = new HotbarModel();
        noBelt.Apply(Message(belt: false), refusals: 0, refusedCell: 0, now: 1.0);
        noBelt.Apply(Message(belt: false), refusals: 1, refusedCell: 2, now: 2.0);
        Assert.Equal(new HotbarCellLook(UiTokens.HotbarNoBeltAlpha, TableRef.None, 0f, "2", UiTokens.HotbarEmptyCapAlpha, UiTokens.HotbarRefusedRim),
            LiveHud.CellLook(noBelt, 2, keys, 2.1));
        Assert.Equal(NoRim, LiveHud.CellLook(noBelt, 2, keys, 2.4).Rim);
    }

    [Fact]
    public void WhileTheInterfaceIsHiddenTheBeltCellsAreNotPicked()
    {
        HandsLayout hands = LiveHud.Hands(new HudMetrics(1920, 1080, 1f), true, false);
        HudRect third = LiveHud.BarCell(hands, 3);
        var shown = new List<HudDrawn>();
        LiveHud.BeltDrawn(hands, shown, hidden: false);
        Assert.Equal(3, LiveHud.BeltCellOf(ClickTarget.Of(new HudPicker(new Stub(shown)).PickScreen(third.Left + 2, third.Top + 2))));
        var hidden = new List<HudDrawn>();
        LiveHud.BeltDrawn(hands, hidden, hidden: true);
        Assert.Empty(hidden);
        Assert.False(new HudPicker(new Stub(hidden)).PickScreen(third.Left + 2, third.Top + 2).IsHit);
    }

    [Fact]
    public void ACellsKeyCapIsItsBoundKey()
    {
        var model = new HotbarModel();
        KeyBindings keys = KeyBindings.Defaults();
        Assert.Equal(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" }, Enumerable.Range(1, 10).Select(c => LiveHud.CellLook(model, c, keys, 0).Cap));
        keys.Set(PlayAction.Hotbar3, "V", "Kp3"); // rebound: cap 3 follows, the others stay
        Assert.Equal("V", LiveHud.CellLook(model, 3, keys, 0).Cap);
        Assert.Equal("4", LiveHud.CellLook(model, 4, keys, 0).Cap);
        keys.Set(PlayAction.Hotbar3); // unbound: no cap
        Assert.Null(LiveHud.CellLook(model, 3, keys, 0).Cap);
        // the cap: the swap cap's height and inset in the cell's top left corner, wholly inside it, a long label cut to the cell
        var cell = new HudRect(100, 200, 48, 48);
        Assert.Equal(new HudRect(103, 203, 14, 18), LiveHud.CellCap(cell, 1f, 13.2f));
        Assert.Equal(new HudRect(103, 203, 42, 18), LiveHud.CellCap(cell, 1f, 500f));
        Assert.Equal(new HudRect(206, 406, 28, 36), LiveHud.CellCap(new HudRect(200, 400, 96, 96), 2f, 28f));
    }

    // a stand-in for the font: 0.6 of the size per character
    private static float Measure(string text, int size) => text.Length * size * 0.6f;

    // A cap as the HUD draws it in `box` at the unit `k`: its rectangle for the label's whole width at the cap's size, and the label fitted to it.
    private static (HudRect Cap, string Text, int Size) Cap(HudRect box, float k, string label)
    {
        int size = (int)Math.Round(UiTokens.SwapCapFont * k);
        HudRect cap = LiveHud.CellCap(box, k, Measure(label, size) + 2 * UiTokens.SwapCapInset * k);
        (string text, int fitted) = LiveHud.CapLabel(label, cap.Width - 2 * UiTokens.SwapCapInset * k, size, Measure);
        return (cap, text, fitted);
    }

    [Fact]
    public void ALongKeyLabelIsDrawnSmallerThenCutSoItStaysInsideItsCap()
    {
        var cell = new HudRect(100, 200, 48, 48);
        float inner(HudRect cap) => cap.Width - 2 * UiTokens.SwapCapInset;
        // one character: full size, whole
        (HudRect cap, string text, int size) = Cap(cell, 1f, "1");
        Assert.Equal(("1", 12), (text, size));
        Assert.True(Measure(text, size) <= inner(cap));
        // a little too wide for the cell's cap (42 wide, 36 inside): smaller, whole
        (cap, text, size) = Cap(cell, 1f, "Ctrl+Q");
        Assert.Equal(new HudRect(103, 203, 42, 18), cap);
        Assert.Equal(("Ctrl+Q", 10), (text, size));
        Assert.True(size < 12 && Measure(text, size) <= inner(cap));
        // far too wide: at 60 % of the size, cut with an ellipsis, inside the cap
        (cap, text, size) = Cap(cell, 1f, "Ctrl+Shift+F10");
        Assert.Equal(8, size); // 60 % of 12, rounded up
        Assert.Equal("Ctrl+S…", text);
        Assert.True(Measure(text, size) <= inner(cap) && Inside(cap, cell));
        // the least size is 60 % of whatever size the cap has; nothing at all fits an empty room
        Assert.Equal(("…", 15), LiveHud.CapLabel("Ctrl+Shift+F10", 9f, 24, Measure));
        Assert.Equal((string.Empty, 15), LiveHud.CapLabel("Ctrl+Shift+F10", 0f, 24, Measure));
        Assert.Equal(0.6f, UiTokens.CapLeastShare);
    }

    [Fact]
    public void AnActionButtonsKeyIsACapInItsTopLeftCornerFittedAsACellsCap()
    {
        // tg's action button at the HUD's size (a 64-pixel tile at 1080 lines and 100 %): "Shift+1" whole at the cap's size in its top left corner
        var button = new HudRect(10, 10, 64, 64);
        (HudRect cap, string text, int size) = Cap(button, 1f, "Shift+1");
        Assert.Equal(new HudRect(13, 13, 57, 18), cap);
        Assert.Equal(("Shift+1", 12), (text, size));
        Assert.True(Inside(cap, button));
        // a long rebinding is fitted, never drawn past the button
        (cap, text, size) = Cap(button, 1f, "Ctrl+Shift+F10");
        Assert.True(Inside(cap, button) && Measure(text, size) <= cap.Width - 2 * UiTokens.SwapCapInset && size >= 8);
        // at twice the unit the cap is twice the size
        Assert.Equal(new HudRect(16, 16, 113, 36), Cap(new HudRect(10, 10, 128, 128), 2f, "Shift+1").Cap);
    }

    private static bool Inside(HudRect inner, HudRect outer) => inner.Left >= outer.Left && inner.Top >= outer.Top && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

    private sealed class Stub(List<HudDrawn> drawn) : IHudSurface
    {
        public void Collect(List<HudDrawn> bottomToTop) => bottomToTop.AddRange(drawn);
    }

    [Fact]
    public void UnderTabAClickOnABeltCellIsItsPressAndOnAPocketOrTheIdCellTgsSlot()
    {
        (PlayRig rig, FakeServer server) = Kit();
        HotbarModel bar = rig.Session.Hotbar;
        ReplicaWorld world = rig.Feed.Replica;
        HandsLayout hands = LiveHud.Hands(new HudMetrics(1920, 1080, 1f), true, false);
        // what HudLayer.Collect reports: the belt's cells under their own ids, tg's pocket and ID slots in cells 8, 9 and 10
        var drawn = new List<HudDrawn>();
        LiveHud.BeltDrawn(hands, drawn);
        var slots = new Dictionary<int, long>();
        foreach (ScreenEntry entry in world.Screen.Values.Where(e => LiveHud.RoleOf(world, e) == LiveHudRole.BarSlot))
        {
            slots[LiveHud.BarCellOfSlot(entry.SlotId)] = entry.Id;
            LiveHud.BarSlotDrawn(hands, entry.Id, LiveHud.BarCellOfSlot(entry.SlotId), null, drawn);
        }
        Assert.Equal(new[] { 8, 9, 10 }, slots.Keys.Order());
        var picker = new HudPicker(new Stub(drawn));
        ClickTarget At(int cell) => ClickTarget.Of(picker.PickScreen(LiveHud.BarCell(hands, cell).Left + 2, LiveHud.BarCell(hands, cell).Top + 2));

        // a belt cell: its press, as its key; no click goes out
        ClickTarget third = At(3);
        Assert.Equal(3, LiveHud.BeltCellOf(third));
        Assert.Equal(Enumerable.Range(1, 7), Enumerable.Range(1, 7).Select(c => LiveHud.BeltCellOf(At(c))));
        long tool = server.HotbarItemId(3);
        Assert.False(rig.Session.Click(third, ClickButton.Left, ClickModifiers.None));
        rig.Run(0.4);
        Assert.Equal(tool, server.HeldId(server.ActiveHand));
        Assert.Equal(HotbarCellState.Out, bar.StateOf(3));
        // a right click or a shift-click on it does nothing; a hover over it hovers nothing; a drag to or from it is not sent
        Assert.False(rig.Session.Click(third, ClickButton.Right, ClickModifiers.None));
        Assert.False(rig.Session.Click(third, ClickButton.Left, ClickModifiers.Shift));
        rig.Run(0.4);
        Assert.Equal(tool, server.HeldId(server.ActiveHand));
        Assert.False(rig.Session.Hover(third, 10.0));
        Assert.False(rig.Session.Drop(new DragRelease(third, ClickTarget.Of(picker.PickScreen(LiveHud.BarCell(hands, 8).Left + 2, LiveHud.BarCell(hands, 8).Top + 2)), ClickButton.Left, ClickModifiers.None, 5)));
        Assert.False(rig.Session.Drop(new DragRelease(At(8), third, ClickButton.Left, ClickModifiers.None, 5)));
        Assert.False(rig.Session.Click(third, ClickButton.Left, ClickModifiers.None)); // pressed again: back into its cell
        rig.Run(0.4);
        Assert.Equal(0, server.HeldId(server.ActiveHand));
        Assert.Equal(HotbarCellState.Holding, bar.StateOf(3));

        // the left pocket's cell is tg's left pocket: a click there is tg's slot click, which takes the pen into the empty hand
        ClickTarget pocket = At(8);
        Assert.Equal(TargetKind.Screen, pocket.Kind);
        Assert.Equal(slots[8], pocket.Id);
        Assert.Equal(0, LiveHud.BeltCellOf(pocket));
        long pen = server.HotbarItemId(8);
        Assert.True(rig.Session.Click(pocket, ClickButton.Left, ClickModifiers.None));
        rig.Run(0.4);
        Assert.Equal(pen, server.HeldId(server.ActiveHand));
        Assert.Equal(slots[9], At(9).Id);
        Assert.Equal(slots[10], At(10).Id);
        // slice 4A: tg's belt, back and suit-storage slots and the nine of tg's toggled inventory: drawn and clicked under Tab only, each in its own
        // cell of the character panel
        List<ScreenEntry> worn = world.Screen.Values.Where(e => LiveHud.RoleOf(world, e) == LiveHudRole.WornSlot).ToList();
        foreach (ScreenEntry entry in worn)
        {
            Assert.False(LiveHud.Shown(LiveHud.Placed(LiveHud.RoleOf(world, entry), humanHud: true), cursorMode: false));
            Assert.True(LiveHud.Shown(LiveHud.Placed(LiveHud.RoleOf(world, entry), humanHud: true), cursorMode: true));
        }
        Assert.Equal(TabInventory.CellCount, worn.Count);
        Assert.Equal(Enumerable.Range(0, TabInventory.CellCount), worn.Select(e => TabInventory.CellOf(e.SlotId)).OrderBy(c => c));
    }

    [Fact]
    public void TheStateComesFromTheMessageAndARefusalIsDatedWhenItIsNew()
    {
        var model = new HotbarModel();
        Assert.False(model.Bar);
        Assert.All(Enumerable.Range(1, 10), c => Assert.Equal(HotbarCellState.Empty, model.StateOf(c)));
        Assert.Equal(0, model.RefusedCell);
        Assert.Equal(double.NegativeInfinity, model.RefusedAt);

        model.Apply(Message(), refusals: 0, refusedCell: 0, now: 1.0);
        Assert.True(model.Bar && model.BeltWorn);
        Assert.Equal(HotbarCellState.Out, model.StateOf(3));
        Assert.Equal(HotbarCellState.Holding, model.StateOf(5));
        Assert.Equal(HotbarCellState.Holding, model.StateOf(10));
        Assert.Equal(HotbarCellState.Empty, model.StateOf(1));
        Assert.Equal(HotbarCellState.Empty, model.StateOf(0)); // out of range reads as empty
        Assert.Equal(HotbarCellState.Empty, model.StateOf(11));
        Assert.Equal(new TableRef(12, true), model.Cells[2].Appearance);
        Assert.Equal("wrench", model.LeftHand);
        Assert.Null(model.RightHand);
        Assert.Equal(0, model.RefusedCell);

        model.Apply(Message(), refusals: 1, refusedCell: 7, now: 2.5); // the feed counted a refusal of cell 7
        Assert.Equal(7, model.RefusedCell);
        Assert.Equal(2.5, model.RefusedAt);
        model.Apply(Message(left: null), refusals: 1, refusedCell: 7, now: 3.0); // the same count: no new refusal
        Assert.Equal(2.5, model.RefusedAt);
        Assert.Null(model.LeftHand);
        model.Apply(Message(), refusals: 2, refusedCell: 7, now: 4.0); // the same cell refused again
        Assert.Equal(4.0, model.RefusedAt);
        model.Apply(HotbarMessage.None, refusals: 0, refusedCell: 0, now: 5.0); // a new join: no bar, the count starts again
        Assert.False(model.Bar);
        Assert.Equal(4.0, model.RefusedAt);
        model.Apply(null, refusals: 1, refusedCell: 9, now: 6.0);
        Assert.Equal(9, model.RefusedCell);
        Assert.Equal(6.0, model.RefusedAt);
    }

    // The fake server with its inventory and the hotbar's kit on (the dev command "toolbelt").
    private static (PlayRig Rig, FakeServer Server) Kit()
    {
        var feed = new FakeFeed("Tester", seed: 1, size: 32, bots: 0, inventory: true);
        var rig = new PlayRig(feed);
        rig.Run(0.5);
        rig.Session.DevCommand("toolbelt");
        rig.Run(0.5);
        return (rig, feed.Server);
    }

    private static void Press(PlayRig rig, int cell)
    {
        Assert.True(rig.Session.HotbarPress(cell));
        rig.Run(0.4);
    }

    [Fact]
    public void TheFakeServersToolbeltFillsTheCellsAndAPressTakesSwapsAndPutsBack()
    {
        (PlayRig rig, FakeServer server) = Kit();
        HotbarModel bar = rig.Session.Hotbar;
        Assert.True(bar.Bar && bar.BeltWorn);
        Assert.Equal(7, server.BeltContentIds.Count);
        for (int c = 1; c <= 7; c++)
        {
            Assert.Equal(HotbarCellState.Holding, bar.StateOf(c));
            Assert.Equal(server.BeltContentIds[c - 1], server.HotbarItemId(c));
            Assert.True(rig.Feed.Replica.TryGetAppearance(bar.Cells[c - 1].Appearance, out _), "cell " + c + "'s picture is in the client's tables");
        }
        Assert.Equal(HotbarCellState.Holding, bar.StateOf(8)); // the pen
        Assert.Equal(HotbarCellState.Empty, bar.StateOf(9));
        Assert.Equal(HotbarCellState.Holding, bar.StateOf(10)); // the ID card
        long third = server.HotbarItemId(3), fifth = server.HotbarItemId(5);

        Assert.True(rig.Session.HotbarPress(3));
        Assert.Equal(HotbarCellState.Holding, bar.StateOf(3)); // nothing is predicted: the cell waits for the server's word
        rig.Run(0.4);
        Assert.Equal(HotbarCellState.Out, bar.StateOf(3));
        Assert.Equal(third, server.HeldId(server.ActiveHand));
        Assert.Equal("welding tool", server.ActiveHand == 0 ? bar.LeftHand : bar.RightHand);
        Press(rig, 5); // rule 3: the swap
        Assert.Equal(HotbarCellState.Holding, bar.StateOf(3));
        Assert.Equal(HotbarCellState.Out, bar.StateOf(5));
        Assert.Equal(fifth, server.HeldId(server.ActiveHand));
        Press(rig, 5); // rule 1: back
        Assert.Equal(HotbarCellState.Holding, bar.StateOf(5));
        Assert.Equal(0, server.HeldId(server.ActiveHand));
        Assert.Equal(0, bar.RefusedCell);

        Press(rig, 9); // an empty hand on the empty right pocket: refused
        Assert.Equal(9, bar.RefusedCell);
        Assert.True(bar.RefusedAt > 0);
        Press(rig, 8); // the pen out of the left pocket, then into the right one (rule 4)
        Press(rig, 9);
        Assert.Equal(HotbarCellState.Holding, bar.StateOf(9));
        Assert.Equal(HotbarCellState.Empty, bar.StateOf(8));
    }

    [Fact]
    public void AHotbarKeyPressesItsCellAndACellOutOfRangeIsNotSent()
    {
        (PlayRig rig, FakeServer server) = Kit();
        for (PlayAction key = PlayAction.Hotbar1; key <= PlayAction.Hotbar10; key++)
            Assert.Equal(key - PlayAction.Hotbar1 + 1, HotbarModel.CellOf(key));
        Assert.Equal(0, HotbarModel.CellOf(PlayAction.ActionButton1));
        Assert.Equal(0, HotbarModel.CellOf(PlayAction.Inventory));
        Assert.False(rig.Session.HotbarPress(0));
        Assert.False(rig.Session.HotbarPress(11));
        rig.Run(0.4);
        Assert.Equal(0, server.HeldId(0) + server.HeldId(1));
        Assert.Equal(0, rig.Session.Hotbar.RefusedCell); // nothing went out, so nothing was refused
        Press(rig, HotbarModel.CellOf(PlayAction.Hotbar10)); // the key 0: the ID card
        Assert.Equal(server.HotbarItemId(10), server.HeldId(server.ActiveHand));
    }

    [Fact]
    public void TheDefaultsAreTheNumberRowAndTheKeypadWithTheActionButtonsOnShiftAndNoKeyServesTwoActions()
    {
        KeyBindings keys = KeyBindings.Defaults();
        for (int n = 1; n <= 10; n++)
        {
            PlayAction hotbar = PlayAction.Hotbar1 + (byte)(n - 1);
            string digit = (n % 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(new[] { "Key" + digit, "Kp" + digit }, keys.KeysOf(hotbar));
            Assert.Equal(hotbar, keys.ActionOfPress("Key" + digit, shift: false));
            Assert.Equal(hotbar, keys.ActionOfPress("Kp" + digit, shift: false));
        }
        for (int n = 1; n <= 9; n++)
        {
            PlayAction button = PlayAction.ActionButton1 + (byte)(n - 1);
            Assert.Equal(new[] { "Shift_Key" + n }, keys.KeysOf(button));
            Assert.Equal(button, keys.ActionOfPress("Key" + n, shift: true));
            Assert.Equal("Shift+" + n, KeyChips.KeyOf(keys, button)); // the number drawn on the action button follows the binding
        }
        var seen = new HashSet<string>();
        foreach (PlayAction action in Enum.GetValues<PlayAction>())
            foreach (string key in keys.KeysOf(action))
                Assert.True(seen.Add(key), key + " is bound twice");
        // the options rows, in the style of the others, and their file ids
        Assert.Equal("Hotbar 1 (belt)", KeyBindings.TitleOf(PlayAction.Hotbar1));
        Assert.Equal("Hotbar 7 (belt)", KeyBindings.TitleOf(PlayAction.Hotbar7));
        Assert.Equal("Hotbar 8 (left pocket)", KeyBindings.TitleOf(PlayAction.Hotbar8));
        Assert.Equal("Hotbar 9 (right pocket)", KeyBindings.TitleOf(PlayAction.Hotbar9));
        Assert.Equal("Hotbar 0 (ID slot)", KeyBindings.TitleOf(PlayAction.Hotbar10));
        Assert.Equal(("hotbar1", "hotbar10"), (KeyBindings.IdOf(PlayAction.Hotbar1), KeyBindings.IdOf(PlayAction.Hotbar10)));
        Assert.Equal(PlayAction.Hotbar10, KeyBindings.ActionOfId("hotbar10"));
    }

    [Fact]
    public void AnOldFileKeepsItsNumberKeysOnTheActionButtonsAndTheHotbarGetsOnlyWhatIsFree()
    {
        // an options file saved before section 10: the numbers click the action buttons, and so they still do
        string old = "options_version=5\n" + string.Concat(Enumerable.Range(1, 9).Select(n => $"bind.action_button{n}=Key{n},Kp{n}\n"));
        var errors = new List<string>();
        PlayOptions options = PlayOptions.Parse(old, errors);
        Assert.Empty(errors);
        for (int n = 1; n <= 9; n++)
        {
            Assert.Equal(new[] { "Key" + n, "Kp" + n }, options.Bindings.KeysOf(PlayAction.ActionButton1 + (byte)(n - 1)));
            Assert.Empty(options.Bindings.KeysOf(PlayAction.Hotbar1 + (byte)(n - 1)));
        }
        Assert.Equal(new[] { "Key0", "Kp0" }, options.Bindings.KeysOf(PlayAction.Hotbar10)); // 0 was free
        // a saved file round trips with the new keys
        PlayOptions fresh = new();
        PlayOptions back = PlayOptions.Parse(fresh.ToText(), errors);
        Assert.Empty(errors);
        Assert.Equal(new[] { "Key3", "Kp3" }, back.Bindings.KeysOf(PlayAction.Hotbar3));
        Assert.Equal(new[] { "Shift_Key3" }, back.Bindings.KeysOf(PlayAction.ActionButton3));
    }
}
