using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 15 (DV698), engine-free: the actions menu's nine slots (a new action takes the first free one, a gone one leaves its slot
/// empty and undrawn, a drag assigns and swaps, a right click empties, Shift+N uses), the mouse in the menu (a click uses, a drag shows the empty slots),
/// the layout (the row at the top left, the window in the middle in rows of nine, scrolling when long), what a square shows from what the client gets
/// (a toggle's highlight, a cooldown's share, tg's count), and the keys (K the menu, Caps Lock the scoreboard, V and Shift+V free) with the upgrade of
/// an old settings file.
/// </summary>
public class ActionsMenuClientTests
{
    private static ActionSlots Synced(params long[] buttons)
    {
        var slots = new ActionSlots();
        slots.Sync(buttons);
        return slots;
    }

    private static long[] Row(ActionSlots slots) => Enumerable.Range(1, ActionSlots.Count).Select(slots.ButtonIn).ToArray();

    [Fact]
    public void ANewActionTakesTheFirstFreeSlotAndAGoneOneLeavesItsSlotEmptyAndUndrawn()
    {
        ActionSlots slots = Synced(10, 20, 30);
        Assert.Equal(new long[] { 10, 20, 30, 0, 0, 0, 0, 0, 0 }, Row(slots));
        Assert.Equal(2, slots.SlotOf(20));
        // 20 goes away (an item put down): its slot is empty and not drawn, the others keep theirs
        slots.Sync(new long[] { 10, 30 });
        Assert.Equal(new long[] { 10, 0, 30, 0, 0, 0, 0, 0, 0 }, Row(slots));
        Assert.False(slots.Drawn(2, dragging: false));
        Assert.True(slots.Drawn(1, dragging: false));
        // a new action fills the first free slot, the gap
        slots.Sync(new long[] { 10, 30, 40 });
        Assert.Equal(new long[] { 10, 40, 30, 0, 0, 0, 0, 0, 0 }, Row(slots));
        // syncing the same buttons again changes nothing
        slots.Sync(new long[] { 10, 30, 40 });
        Assert.Equal(new long[] { 10, 40, 30, 0, 0, 0, 0, 0, 0 }, Row(slots));
        // ten actions: the tenth has no slot (it is in the menu's list only)
        ActionSlots full = Synced(Enumerable.Range(1, 10).Select(i => (long)i).ToArray());
        Assert.Equal(Enumerable.Range(1, 9).Select(i => (long)i), Row(full));
        Assert.Equal(0, full.SlotOf(10));
        // nothing is kept across a new start (a reconnect, a new round: the buttons are new)
        full.Sync(Array.Empty<long>());
        Assert.All(Row(full), b => Assert.Equal(0, b));
    }

    [Fact]
    public void ADragAssignsAndSwapsARightClickEmptiesAndAnEmptiedActionIsNotPutBackByItself()
    {
        ActionSlots slots = Synced(10, 20, 30);
        // from the list onto slot 5: there now, and out of slot 1 (an action is in one slot at most)
        slots.Assign(10, 5);
        Assert.Equal(new long[] { 0, 20, 30, 0, 10, 0, 0, 0, 0 }, Row(slots));
        // slot 2 onto slot 5 (taken): a swap
        slots.Assign(20, 5);
        Assert.Equal(new long[] { 0, 10, 30, 0, 20, 0, 0, 0, 0 }, Row(slots));
        // a right click empties slot 3; the action stays the player's and is not put back into a slot by the next sync
        Assert.Equal(ActionsMenuOutcome.Cleared, ActionsMenuInput.RightClick(3, slots));
        Assert.Equal(ActionsMenuOutcome.None, ActionsMenuInput.RightClick(3, slots)); // already empty
        slots.Sync(new long[] { 10, 20, 30 });
        Assert.Equal(new long[] { 0, 10, 0, 0, 20, 0, 0, 0, 0 }, Row(slots));
        // an action in no slot dropped onto a taken slot replaces the one there, which leaves the row
        slots.Assign(30, 2);
        Assert.Equal(new long[] { 0, 30, 0, 0, 20, 0, 0, 0, 0 }, Row(slots));
        Assert.Equal(0, slots.SlotOf(10));
        // a button the player does not have, or a slot out of range, changes nothing
        slots.Assign(99, 1);
        slots.Assign(20, 0);
        slots.Assign(20, 10);
        Assert.Equal(new long[] { 0, 30, 0, 0, 20, 0, 0, 0, 0 }, Row(slots));
        // an action that went away and came back is new again: it takes the first free slot
        slots.Sync(new long[] { 20, 30 });
        slots.Sync(new long[] { 20, 30, 10 });
        Assert.Equal(1, slots.SlotOf(10));
    }

    [Fact]
    public void AClickUsesADragShowsTheEmptySlotsAndADropOnASlotAssigns()
    {
        ActionSlots slots = Synced(10, 20);
        var mouse = new ActionsMenuInput();
        // a click without moving: tg's click on the button
        mouse.Press(20, 100, 100);
        Assert.False(mouse.Dragging);
        Assert.Equal((ActionsMenuOutcome.Use, 20L), mouse.Release(0, slots));
        // a small shake is still a click
        mouse.Press(20, 100, 100);
        mouse.Move(102, 103);
        Assert.Equal((ActionsMenuOutcome.Use, 20L), mouse.Release(0, slots));
        // the empty slots are drawn only while an action is dragged
        Assert.False(slots.Drawn(4, mouse.Dragging));
        mouse.Press(20, 100, 100);
        mouse.Move(140, 100);
        Assert.True(mouse.Dragging);
        Assert.Equal(20, mouse.Dragged);
        Assert.True(slots.Drawn(4, mouse.Dragging));
        Assert.Equal((ActionsMenuOutcome.Assigned, 20L), mouse.Release(4, slots));
        Assert.False(mouse.Dragging);
        Assert.False(slots.Drawn(2, mouse.Dragging)); // slot 2 emptied by the move, hidden again
        Assert.Equal(new long[] { 10, 0, 0, 20, 0, 0, 0, 0, 0 }, Row(slots));
        // a drag let go over no slot does nothing; a press on nothing does nothing
        mouse.Press(10, 0, 0);
        mouse.Move(50, 50);
        Assert.Equal((ActionsMenuOutcome.None, 0L), mouse.Release(0, slots));
        Assert.Equal(1, slots.SlotOf(10));
        mouse.Press(0, 0, 0);
        mouse.Move(50, 50);
        Assert.False(mouse.Dragging);
        Assert.Equal((ActionsMenuOutcome.None, 0L), mouse.Release(3, slots));
        // the menu closing ends a drag
        mouse.Press(10, 0, 0);
        mouse.Move(50, 50);
        mouse.Cancel();
        Assert.False(mouse.Dragging);
    }

    [Fact]
    public void ShiftAndADigitUseThatSlotWithTheMenuOpenOrClosed()
    {
        KeyBindings keys = KeyBindings.Defaults();
        ActionSlots slots = Synced(10, 20);
        slots.Assign(10, 2);
        // the key: Shift+2 is slot 2's (the plain digits are the belt bar's), whatever the menu does; the slot names the button tg's click goes to
        PlayAction? pressed = keys.ActionOfPress("Key2", shift: true);
        Assert.Equal(PlayAction.ActionButton2, pressed);
        Assert.Equal(10, slots.ButtonIn(pressed.Value - PlayAction.ActionButton1 + 1));
        Assert.Equal(PlayAction.Hotbar2, keys.ActionOfPress("Key2", shift: false));
        var mouse = new ActionsMenuInput();
        mouse.Press(20, 0, 0);
        mouse.Move(30, 30); // a drag in the open menu does not change what a slot's key uses until it lands
        Assert.Equal(10, slots.ButtonIn(2));
        Assert.Equal(0, slots.ButtonIn(5));
        Assert.Equal(0, slots.ButtonIn(0));
        Assert.Equal(0, slots.ButtonIn(10));
    }

    [Fact]
    public void TheRowIsAtTheTopLeftAndTheMenuIsCentredInRowsOfNineAndScrollsWhenLong()
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        float unit = UiTokens.Unit(metrics), cell = MathF.Round(UiTokens.HotbarSlot * unit), gap = MathF.Round(UiTokens.StorageCellGap * unit);
        HudRect[] row = ActionsMenu.Row(cell, unit);
        Assert.Equal(9, row.Length);
        Assert.Equal(MathF.Round(UiTokens.ScreenMargin * unit), row[0].Left);
        Assert.All(row, r => Assert.Equal(row[0].Top, r.Top));
        Assert.Equal(row[0].Right + gap, row[1].Left);
        Assert.Equal(3, ActionsMenu.SlotAt(row, row[2].Left + 1, row[2].Top + 1));
        Assert.Equal(0, ActionsMenu.SlotAt(row, row[8].Right + 5, row[8].Top + 1));

        // twelve actions: two rows under the nine slots, the window in the middle of the screen, as wide as nine cells
        ActionsMenuLayout menu = ActionsMenu.Layout(metrics, cell, unit, 12, 0f);
        Assert.Equal(metrics.Width / 2f, menu.Window.Left + menu.Window.Width / 2f, 0);
        Assert.Equal(metrics.Height / 2f, menu.Window.Top + menu.Window.Height / 2f, 0);
        Assert.Equal(9, menu.Slots.Length);
        Assert.Equal(menu.Slots[0].Left, menu.Items[0].Left);
        Assert.Equal(menu.Slots[8].Right, menu.Items[8].Right);
        Assert.Equal(menu.Items[0].Left, menu.Items[9].Left); // the tenth starts the second row
        Assert.True(menu.Items[9].Top > menu.Items[0].Bottom);
        Assert.True(menu.Items[0].Top > menu.Slots[0].Bottom); // the list under the slots
        Assert.Equal(0f, menu.MaxScroll);
        Assert.Equal(10, ActionsMenu.ItemAt(menu, menu.Items[10].Left + 1, menu.Items[10].Bottom + 1)); // its name under it is it too
        Assert.Equal(-1, ActionsMenu.ItemAt(menu, menu.Window.Left - 5, menu.Items[0].Top + 1));
        Assert.Equal(4, ActionsMenu.SlotAt(menu.Slots, menu.Slots[3].Left + 1, menu.Slots[3].Top + 1));

        // two hundred actions: the window stops at its share of the screen and the list scrolls, kept within its overflow
        ActionsMenuLayout longList = ActionsMenu.Layout(metrics, cell, unit, 200, 0f);
        Assert.True(longList.Window.Height <= metrics.Height * ActionsMenu.MaxHeightShare);
        Assert.True(longList.MaxScroll > 0f);
        ActionsMenuLayout scrolled = ActionsMenu.Layout(metrics, cell, unit, 200, 1e6f);
        Assert.Equal(scrolled.MaxScroll, scrolled.Scroll);
        Assert.Equal(longList.Items[0].Top - scrolled.Scroll, scrolled.Items[0].Top);
        Assert.Equal(0f, ActionsMenu.Layout(metrics, cell, unit, 200, -50f).Scroll);
        Assert.Equal(-1, ActionsMenu.ItemAt(scrolled, scrolled.Items[0].Left + 1, scrolled.Items[0].Top + 1)); // scrolled out of sight
        // none: one row's room for the words
        Assert.Empty(ActionsMenu.Layout(metrics, cell, unit, 0, 0f).Items);
    }

    [Fact]
    public void ASquareShowsTheToggleTheCooldownShareAndTgsCountFromWhatTheClientGets()
    {
        var replica = new ReplicaWorld();
        // tg's count is the button's own maptext (a charge, a growth, a cooldown's seconds), shown as plain text in the corner
        TableRef counted = replica.LocalAppearance(new AppearanceEntry { IconState = replica.LocalString("cocoon"), Maptext = replica.LocalString("<span class='maptext'>75%</span>") });
        TableRef plain = replica.LocalAppearance(new AppearanceEntry { IconState = replica.LocalString("flashlight") });
        Assert.Equal("75%", ActionsMenu.CountOf(replica, counted));
        Assert.Null(ActionsMenu.CountOf(replica, plain));
        Assert.Null(ActionsMenu.CountOf(replica, TableRef.None));
        // the toggle and the cooldown come with the button in the server's Actions message
        var message = new ActionsMessage
        {
            Buttons = new[] { new ActionButtonState(5, "Set Internals", true), new ActionButtonState(6, "Fireball", false, 1200, 400) },
        };
        Assert.True(message.Find(5).Value.Active);
        Assert.False(message.Find(6).Value.Active);
        Assert.Equal(0.75f, message.Find(6).Value.CooldownLeft(900), 3); // three quarters still to run: the wheel covers that much
        Assert.Equal(0f, message.Find(6).Value.CooldownLeft(1200)); // over: no wheel
        Assert.Equal(0f, message.Find(5).Value.CooldownLeft(900)); // none: no wheel
        // only the buttons the client has a screen entry for are listed (and slotted)
        var screen = new Dictionary<long, ScreenEntry> { [6] = new ScreenEntry(6, plain, null) };
        Assert.Equal(new long[] { 6 }, ActionSlots.Drawable(message, screen));
    }

    [Fact]
    public void KOpensTheActionsMenuCapsLockHoldsTheScoreboardAndAnOldSettingsFileIsUpgraded()
    {
        KeyBindings keys = KeyBindings.Defaults();
        Assert.Equal(PlayAction.ActionsMenu, keys.ActionOfPress("K", shift: false));
        Assert.Equal(PlayAction.Scoreboard, keys.ActionOfPress("Capslock", shift: false));
        Assert.Null(keys.ActionOfPress("V", shift: false));
        Assert.Null(keys.ActionOfPress("V", shift: true));
        Assert.Equal("Caps Lock", KeyChips.Label("Capslock"));
        Assert.Equal("actions_menu", KeyBindings.IdOf(PlayAction.ActionsMenu));

        // a version 6 file as the game wrote it: every action's keys, the scoreboard on K, the open keys on V and Shift+V
        string old = PlayOptions.Parse(string.Empty).ToText().Replace("options_version=" + PlayOptions.FileVersion, "options_version=6")
            .Replace("bind.scoreboard=Capslock", "bind.scoreboard=K").Replace("bind.open_backpack=\n", "bind.open_backpack=V\n")
            .Replace("bind.open_belt=\n", "bind.open_belt=Shift_V\n").Replace("bind.actions_menu=K\n", string.Empty);
        Assert.Contains("bind.scoreboard=K\n", old);
        var errors = new List<string>();
        PlayOptions upgraded = PlayOptions.Parse(old, errors);
        Assert.Empty(errors);
        Assert.Equal(new[] { "Capslock" }, upgraded.Bindings.KeysOf(PlayAction.Scoreboard));
        Assert.Equal(new[] { "K" }, upgraded.Bindings.KeysOf(PlayAction.ActionsMenu));
        Assert.Empty(upgraded.Bindings.KeysOf(PlayAction.OpenBackpack));
        Assert.Empty(upgraded.Bindings.KeysOf(PlayAction.OpenBelt));
        Assert.Equal(PlayOptions.FileVersion, upgraded.ReadVersion);
        Assert.Contains("bind.actions_menu=K\n", upgraded.ToText());

        // the player's own keys stay: the scoreboard moved to J keeps J, the open keys moved elsewhere keep theirs, and K free goes to the menu
        PlayOptions own = PlayOptions.Parse("options_version=6\nbind.scoreboard=J\nbind.open_backpack=Shift_O\n", errors);
        Assert.Equal(new[] { "J" }, own.Bindings.KeysOf(PlayAction.Scoreboard));
        Assert.Equal(new[] { "K" }, own.Bindings.KeysOf(PlayAction.ActionsMenu));
        Assert.Equal(new[] { "Shift_O" }, own.Bindings.KeysOf(PlayAction.OpenBackpack));
        // K taken by another action of the player's: the menu is left unbound, the other action keeps K; Caps Lock taken: the scoreboard stays on K
        PlayOptions taken = PlayOptions.Parse("options_version=6\nbind.scoreboard=Capslock\nbind.say=K\n", errors);
        Assert.Equal(new[] { "K" }, taken.Bindings.KeysOf(PlayAction.Say));
        Assert.Empty(taken.Bindings.KeysOf(PlayAction.ActionsMenu));
        PlayOptions caps = PlayOptions.Parse("options_version=6\nbind.scoreboard=K\nbind.say=Capslock\n", errors);
        Assert.Equal(new[] { "K" }, caps.Bindings.KeysOf(PlayAction.Scoreboard));
        Assert.Empty(caps.Bindings.KeysOf(PlayAction.ActionsMenu));
        // a version 7 file is read as written: V bound to the backpack on purpose stays
        PlayOptions mine = PlayOptions.Parse("options_version=7\nbind.open_backpack=V\nbind.scoreboard=K\n", errors);
        Assert.Equal(new[] { "V" }, mine.Bindings.KeysOf(PlayAction.OpenBackpack));
        Assert.Equal(new[] { "K" }, mine.Bindings.KeysOf(PlayAction.Scoreboard));
        Assert.Empty(mine.Bindings.KeysOf(PlayAction.ActionsMenu));
    }
}
