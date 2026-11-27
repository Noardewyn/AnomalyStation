using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 12, slice 4B-2: what the Tab inventory sends: the places a HUD pick names, every drag from every kind of place onto every
/// kind of target, a click on a panel, the quick transfer from each place, the refused move's blink and a drag's rim on each kind of place, a drag
/// ended by a message, the "inventory opened" notice, tg's storage window kept for other containers only, and the quick transfer's own key.
/// </summary>
public class InventoryRulesTests
{
    private const uint Gloves = 1u << 2, LeftPocket = 1u << 15, Back = 1u << 10;
    private const int Active = 1, Other = 2;

    // the places of the test: the active (left) hand holding item 11, the right one item 12; the belt (200) item 203 in bar cell 3; the left pocket
    // the box 300; worn gloves 900; the backpack (100) item 102 in its panel's cell 1 (wishing for place 2)
    private static readonly InvPlace HandActive = new(InvKind.Hand, 11, Active);
    private static readonly InvPlace HandOther = new(InvKind.Hand, 12, Other);
    private static readonly InvPlace BeltCell = new(InvKind.Belt, 203, 3, 200, 0);
    private static readonly InvPlace Pocket = new(InvKind.Slot, 300, (int)LeftPocket);
    private static readonly InvPlace Worn = new(InvKind.Slot, 900, (int)Gloves);
    private static readonly InvPlace PanelCell = new(InvKind.Cell, 102, 1, 100, 2);

    public static IEnumerable<object[]> Sources() => new[] { "hand", "other hand", "belt", "pocket", "worn", "panel" }.Select(s => new object[] { s });

    private static InvPlace SourceOf(string name) => name switch
    {
        "hand" => HandActive,
        "other hand" => HandOther,
        "belt" => BeltCell,
        "pocket" => Pocket,
        "worn" => Worn,
        _ => PanelCell,
    };

    [Theory]
    [MemberData(nameof(Sources))]
    public void ADragFromAnyPlaceSendsOneMoveForEveryInventoryTargetAndTheViewsAsTheDesignSays(string source)
    {
        InvPlace from = SourceOf(source);
        long item = from.Item;
        bool hand = from.Kind == InvKind.Hand, active = hand && from.Index == Active;
        // onto a hand box: to that hand (released on its own hand: nothing)
        Assert.Equal(from.SamePlace(HandOther) ? InvSend.Nothing : InvSend.Move(item, InventoryTarget.Hand, 0, Other), InventoryRules.Drop(from, HandOther with { Item = 0 }, Active));
        Assert.Equal(from.SamePlace(HandActive) ? InvSend.Nothing : InvSend.Move(item, InventoryTarget.Hand, 0, Active), InventoryRules.Drop(from, HandActive, Active));
        // onto a belt cell n (as Resolve names it: the wish is n): into the belt wishing for bar cell n, held or empty; with no belt worn nothing
        var beltWithItem = new InvPlace(InvKind.Belt, 205, 5, 200, 5);
        Assert.Equal(from.SamePlace(beltWithItem) ? InvSend.Nothing : InvSend.Move(item, InventoryTarget.Storage, 200, 5), InventoryRules.Drop(from, beltWithItem, Active));
        Assert.Equal(InvSend.Move(item, InventoryTarget.Storage, 200, 6), InventoryRules.Drop(from, new InvPlace(InvKind.Belt, 0, 6, 200, 6), Active));
        Assert.Equal(InvSend.Nothing, InventoryRules.Drop(from, new InvPlace(InvKind.Belt, 0, 6, 0, 0), Active));
        // onto a pocket, ID or worn slot's cell: onto that slot (its own: nothing)
        Assert.Equal(from.SamePlace(Worn) ? InvSend.Nothing : InvSend.Move(item, InventoryTarget.Slot, 0, (int)Gloves), InventoryRules.Drop(from, Worn with { Item = 0 }, Active));
        Assert.Equal(from.SamePlace(Pocket) ? InvSend.Nothing : InvSend.Move(item, InventoryTarget.Slot, 0, (int)LeftPocket), InventoryRules.Drop(from, Pocket, Active));
        // onto a panel's cell: into that storage at its item; an empty cell simply in; onto the header or backing simply in
        Assert.Equal(InvSend.Move(item, InventoryTarget.Storage, 300, 2), InventoryRules.Drop(from, new InvPlace(InvKind.Cell, 302, 1, 300, 2), Active));
        Assert.Equal(InvSend.Move(item, InventoryTarget.Storage, 300, 0), InventoryRules.Drop(from, new InvPlace(InvKind.Cell, 0, 5, 300, 0), Active));
        Assert.Equal(InvSend.Move(item, InventoryTarget.Storage, 300, 0), InventoryRules.Drop(from, new InvPlace(InvKind.Panel, Storage: 300), Active));
        // within its own storage onto another cell: the reorder tg's storage does
        Assert.Equal(InvSend.Move(item, InventoryTarget.Storage, 100, 4), InventoryRules.Drop(from, new InvPlace(InvKind.Cell, 104, 3, 100, 4), Active));
        // onto the own sprite: the active hand's item used on oneself there; anything else nothing
        Assert.Equal(active ? new InvSend(InvSendKind.SelfUse, item) : InvSend.Nothing, InventoryRules.Drop(from, new InvPlace(InvKind.Sprite), Active));
        // onto the floor in the view (a turf, or nothing at all): dropped
        Assert.Equal(InvSend.Move(item, InventoryTarget.Floor), InventoryRules.Drop(from, new InvPlace(InvKind.Floor), Active));
        // onto a thing in the view or a tg entry that is no inventory place (the doll, a crate's window): from tg's own entries (a hand, a worn slot, a
        // pocket or the ID slot) tg's drag, as before (D224, D225); from our cells (a panel's, a belt cell of the bar) nothing
        bool tgEntry = from.Kind is InvKind.Hand or InvKind.Slot;
        Assert.Equal(tgEntry ? new InvSend(InvSendKind.TgDrop, item) : InvSend.Nothing, InventoryRules.Drop(from, new InvPlace(InvKind.Thing, 77), Active));
        Assert.Equal(tgEntry ? new InvSend(InvSendKind.TgDrop, item) : InvSend.Nothing, InventoryRules.Drop(from, new InvPlace(InvKind.Hud), Active));
        // onto a part of ours that holds nothing: nothing; released where it was pressed: nothing
        Assert.Equal(InvSend.Nothing, InventoryRules.Drop(from, new InvPlace(InvKind.None), Active));
        Assert.Equal(InvSend.Nothing, InventoryRules.Drop(from, from, Active));
    }

    [Fact]
    public void ADragFromAPlaceWithNoItemOrFromNoInventoryPlaceSendsNothing()
    {
        foreach (InvPlace empty in new[] { HandActive with { Item = 0 }, BeltCell with { Item = 0 }, Worn with { Item = 0 }, PanelCell with { Item = 0 } })
            Assert.Equal(InvSend.Nothing, InventoryRules.Drop(empty, new InvPlace(InvKind.Floor), Active));
        foreach (InvPlace notOne in new[] { new InvPlace(InvKind.Panel, 0, 0, 100), new InvPlace(InvKind.Sprite), new InvPlace(InvKind.Hud, 5), new InvPlace(InvKind.Thing, 5) })
            Assert.Equal(InvSend.Nothing, InventoryRules.Drop(notOne, HandActive, Active));
    }

    [Fact]
    public void AClickOnAPanelTakesItsItemToTheActiveHandOrPutsTheHeldOneIn()
    {
        // on an item: the move to the active hand (whatever the hand holds; the server refuses a full hand)
        Assert.Equal(InvSend.Move(102, InventoryTarget.Hand, 0, Active), InventoryRules.Click(PanelCell, ClickModifiers.None, Active, held: 0));
        Assert.Equal(InvSend.Move(102, InventoryTarget.Hand, 0, Other), InventoryRules.Click(PanelCell, ClickModifiers.None, Other, held: 11));
        // on an empty cell or the header with an item in the active hand: it goes in; with an empty hand nothing
        var emptyCell = new InvPlace(InvKind.Cell, 0, 7, 100, 0);
        Assert.Equal(InvSend.Move(11, InventoryTarget.Storage, 100, 0), InventoryRules.Click(emptyCell, ClickModifiers.None, Active, held: 11));
        Assert.Equal(InvSend.Move(11, InventoryTarget.Storage, 300, 0), InventoryRules.Click(new InvPlace(InvKind.Panel, Storage: 300), ClickModifiers.None, Active, held: 11));
        Assert.Equal(InvSend.Nothing, InventoryRules.Click(emptyCell, ClickModifiers.None, Active, held: 0));
        // Shift alone on a panel's item: tg's Shift+click on it (its examine); on an empty cell or a header nothing; any other modifier nothing
        Assert.Equal(new InvSend(InvSendKind.Examine, 102), InventoryRules.Click(PanelCell, ClickModifiers.Shift, Active, held: 0));
        Assert.Equal(new InvSend(InvSendKind.Examine, 102), InventoryRules.Click(PanelCell, ClickModifiers.Shift, Active, held: 11));
        Assert.Equal(InvSend.Nothing, InventoryRules.Click(emptyCell, ClickModifiers.Shift, Active, held: 11));
        Assert.Equal(InvSend.Nothing, InventoryRules.Click(new InvPlace(InvKind.Panel, Storage: 300), ClickModifiers.Shift, Active, held: 11));
        Assert.Equal(InvSend.Nothing, InventoryRules.Click(PanelCell, ClickModifiers.Shift | ClickModifiers.Ctrl, Active, held: 0));
        Assert.Equal(InvSend.Nothing, InventoryRules.Click(PanelCell, ClickModifiers.Alt, Active, held: 0));
        // a click anywhere else stays what it was (tg's click on a hand or a slot, a belt cell's press, the sprite)
        foreach (InvPlace other in new[] { HandActive, BeltCell, Pocket, Worn, new InvPlace(InvKind.Sprite), new InvPlace(InvKind.Hud) })
            Assert.Null(InventoryRules.Click(other, ClickModifiers.None, Active, held: 11));
    }

    [Fact]
    public void TheQuickTransferTakesAnItemToTheActiveHandAndAHeldOneIntoTheBackpack()
    {
        foreach (InvPlace from in new[] { BeltCell, Pocket, Worn, PanelCell })
            Assert.Equal(InvSend.Move(from.Item, InventoryTarget.Hand, 0, Active), InventoryRules.QuickTransfer(from, Active, back: 100));
        // a held item, either hand, into the backpack simply; with none worn refused (its cell blinks)
        Assert.Equal(InvSend.Move(11, InventoryTarget.Storage, 100, 0), InventoryRules.QuickTransfer(HandActive, Active, back: 100));
        Assert.Equal(InvSend.Move(12, InventoryTarget.Storage, 100, 0), InventoryRules.QuickTransfer(HandOther, Active, back: 100));
        Assert.Equal(new InvSend(InvSendKind.Refused, 11), InventoryRules.QuickTransfer(HandActive, Active, back: 0));
        // on a panel's header or empty cell nothing; on what is no item of the inventory tg's own Ctrl-click (null: as before)
        Assert.Equal(InvSend.Nothing, InventoryRules.QuickTransfer(new InvPlace(InvKind.Panel, Storage: 100), Active, 100));
        Assert.Equal(InvSend.Nothing, InventoryRules.QuickTransfer(PanelCell with { Item = 0 }, Active, 100));
        foreach (InvPlace other in new[] { new InvPlace(InvKind.Sprite), new InvPlace(InvKind.Hud), new InvPlace(InvKind.Thing, 4), HandActive with { Item = 0 }, Worn with { Item = 0 } })
            Assert.Null(InventoryRules.QuickTransfer(other, Active, 100));
    }

    // tg's screen as the client has it: the two hand boxes (the left holding item 11, the right empty), the left pocket holding the box 300, the
    // gloves slot holding 900, the health doll (no slot), an open crate's item (tg's storage group)
    private static Dictionary<long, ScreenEntry> Screen() => new()
    {
        [1] = Entry(1, hand: 1, child: 11),
        [2] = Entry(2, hand: 2),
        [3] = Entry(3, slot: LeftPocket, child: 300),
        [4] = Entry(4, slot: Gloves, child: 900),
        [5] = Entry(5),
        [6] = new ScreenEntry(6, new TableRef(1, true), "x") { Group = HudGroup.Storage },
    };

    private static ScreenEntry Entry(long id, byte hand = 0, uint slot = 0, long child = 0)
        => new(id, new TableRef(1, true), "x")
        {
            HandIndex = hand,
            SlotId = slot,
            VisChildren = child == 0 ? null : new ScreenChildren { Items = new[] { new ScreenChild(child, new TableRef(9, true)) { IsItem = true } } },
        };

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    [Fact]
    public void AHudPickNamesTheInventoryPlaceAndTheItemItShows()
    {
        (List<CarriedStorage> storages, HotbarModel bar) = StorageColumnTests.Carried(9, 9);
        long[] belt = StorageColumn.BeltOnBar(bar);
        List<StoragePanel> panels = StorageColumn.Panels(storages, belt);
        Dictionary<long, ScreenEntry> screen = Screen();
        InvPlace Of(ClickTarget t) => InventoryRules.Resolve(t, screen, panels, 200, belt);
        // a hand box, by its box or by the item drawn in it; a pocket and a worn slot the same
        Assert.Equal(new InvPlace(InvKind.Hand, 11, 1), Of(Screen(1)));
        Assert.Equal(new InvPlace(InvKind.Hand, 11, 1), Of(Screen(11)));
        Assert.Equal(new InvPlace(InvKind.Hand, 0, 2), Of(Screen(2)));
        Assert.Equal(new InvPlace(InvKind.Slot, 300, (int)LeftPocket), Of(Screen(300)));
        Assert.Equal(new InvPlace(InvKind.Slot, 900, (int)Gloves), Of(Screen(4)));
        // a belt cell: its item on the bar, the worn belt, and the bar cell as the wish (DV689, DV692); with no belt worn no wish
        Assert.Equal(new InvPlace(InvKind.Belt, 203, 3, 200, 3), Of(Screen(LiveHud.BeltCellId(3))));
        Assert.Equal(new InvPlace(InvKind.Belt, 0, 6, 0, 0), InventoryRules.Resolve(Screen(LiveHud.BeltCellId(6)), screen, panels, 0, new long[7]));
        // the panels: the backpack's second cell, the belt's panel's first (the eighth item, its place 8), a header, an empty cell, a cell past the panels
        Assert.Equal(new InvPlace(InvKind.Cell, 102, 1, 100, 2), Of(Screen(StorageColumn.CellId(0, 1))));
        Assert.Equal(new InvPlace(InvKind.Cell, 208, 0, 200, 0), Of(Screen(StorageColumn.CellId(1, 0))));
        // on any other HUD than the human one nothing is an inventory place: its hand boxes, slots and drags keep tg's paths
        foreach (long id in new long[] { 1, 11, 3, 300, 4, LiveHud.BeltCellId(3), StorageColumn.CellId(0, 1), StorageColumn.PanelId(2), TabInventory.SpriteId })
        {
            InvPlace none = InventoryRules.Resolve(Screen(id), screen, panels, 200, belt, humanHud: false);
            Assert.Equal(InvKind.None, none.Kind);
            Assert.Null(InventoryRules.Click(none, ClickModifiers.None, Active, held: 11));
            Assert.Null(InventoryRules.QuickTransfer(none, Active, 100));
            Assert.Equal(InvSend.Nothing, InventoryRules.Drop(none, new InvPlace(InvKind.Floor), Active));
        }
        Assert.Equal(new InvPlace(InvKind.Panel, Storage: 300), Of(Screen(StorageColumn.PanelId(2))));
        Assert.Equal(new InvPlace(InvKind.Cell, 0, 10, 100, 0), Of(Screen(StorageColumn.CellId(0, 10))));
        Assert.Equal(InvKind.None, Of(Screen(StorageColumn.CellId(7, 0))).Kind);
        Assert.Equal(InvKind.None, Of(Screen(StorageColumn.CellId(0, 30))).Kind);
        // the sprite; tg's other entries (the doll, a crate's window); the view
        Assert.Equal(InvKind.Sprite, Of(Screen(TabInventory.SpriteId)).Kind);
        Assert.Equal(InvKind.Hud, Of(Screen(5)).Kind);
        Assert.Equal(InvKind.Hud, Of(Screen(6)).Kind);
        Assert.Equal(InvKind.None, Of(Screen(-999)).Kind);
        Assert.Equal(new InvPlace(InvKind.Thing, 42), Of(new ClickTarget(TargetKind.Entity, 42, 0, 0, 0, 0, 0)));
        Assert.Equal(InvKind.Floor, Of(new ClickTarget(TargetKind.Turf, 0, 5, 5, 1, 0, 0)).Kind);
        Assert.Equal(InvKind.Floor, Of(ClickTarget.None).Kind);
    }

    [Fact]
    public void AMessageThatTakesTheDraggedItemFromItsPlaceEndsTheDrag()
    {
        (List<CarriedStorage> storages, HotbarModel bar) = StorageColumnTests.Carried(7, 3);
        Dictionary<long, ScreenEntry> screen = Screen();
        InvPlace Resolve(ClickTarget t, List<CarriedStorage> now) => InventoryRules.Resolve(t, screen, StorageColumn.Panels(now, null), 200, null);
        ClickTarget pressedOn = Screen(StorageColumn.CellId(0, 1)); // the backpack's item 102
        InvPlace pressed = Resolve(pressedOn, storages);
        Assert.True(InventoryRules.DragHolds(pressed, Resolve(pressedOn, storages)));
        // the item gone from the bag (101 and 103 stay): its cell shows another item
        var gone = new List<CarriedStorage>(storages) { [0] = StorageColumnTests.Storage(100, StoragePlace.Back, "backpack", 21, 8, 21, new[] { StorageColumnTests.Item(101), StorageColumnTests.Item(103) }) };
        Assert.False(InventoryRules.DragHolds(pressed, Resolve(pressedOn, gone)));
        // moved within the bag to its end
        var moved = new List<CarriedStorage>(storages)
        {
            [0] = StorageColumnTests.Storage(100, StoragePlace.Back, "backpack", 21, 13, 21, new long[] { 101, 103, 104, 105, 102 }.Select(i => StorageColumnTests.Item(i))),
        };
        Assert.False(InventoryRules.DragHolds(pressed, Resolve(pressedOn, moved)));
        // the backpack itself gone: the first panel is now another storage's
        Assert.False(InventoryRules.DragHolds(pressed, Resolve(pressedOn, storages.Skip(1).ToList())));
        // every storage gone (a body that is no human any more): no panel at all
        Assert.False(InventoryRules.DragHolds(pressed, Resolve(pressedOn, new List<CarriedStorage>())));
        // a held item dragged and then put away by the server: the hand box shows none
        InvPlace held = InventoryRules.Resolve(Screen(11), screen, null, 0, null);
        screen[1] = Entry(1, hand: 1);
        Assert.False(InventoryRules.DragHolds(held, InventoryRules.Resolve(Screen(11), screen, null, 0, null)));
        Assert.False(InventoryRules.DragHolds(held, InventoryRules.Resolve(Screen(1), screen, null, 0, null)));
    }

    [Fact]
    public void ARefusedMovesItemBlinksRedWhereverItIsDrawnAndADragRimsThePlaceUnderIt()
    {
        var model = new StoragesModel();
        model.Apply(new StoragesMessage(), 1, 102, now: 10.0);
        Assert.Equal(102, InventoryRules.Blinking(model, 10.0));
        Assert.Equal(102, InventoryRules.Blinking(model, 10.0 + UiTokens.HotbarRefusedSeconds - 0.01));
        Assert.Equal(0, InventoryRules.Blinking(model, 10.0 + UiTokens.HotbarRefusedSeconds));
        Assert.Equal(0, InventoryRules.Blinking(new StoragesModel(), 10.0));
        // the item's cell, whatever kind of place it is in: red; any other place's none
        foreach (InvPlace place in new[] { HandActive, HandOther, BeltCell, Pocket, Worn, PanelCell })
        {
            Assert.Equal(UiTokens.HotbarRefusedRim, InventoryRules.Rim(place.Item, place, place.Item, default));
            Assert.Equal(0u, InventoryRules.Rim(place.Item, place, 999, default));
            Assert.Equal(0u, InventoryRules.Rim(0, place with { Item = 0 }, 0, default)); // nothing blinks: no rim on an empty cell
            // a drag over the place (its item or none): the light rim; over another place none; the refusal's red wins
            Assert.Equal(UiTokens.InventoryDragRim, InventoryRules.Rim(place.Item, place, 0, place));
            Assert.Equal(UiTokens.InventoryDragRim, InventoryRules.Rim(0, place with { Item = 0 }, 0, place with { Item = 0 }));
            Assert.Equal(0u, InventoryRules.Rim(place.Item, place, 0, new InvPlace(InvKind.Cell, 1, 20, 100)));
            Assert.Equal(UiTokens.HotbarRefusedRim, InventoryRules.Rim(place.Item, place, place.Item, place));
        }
        // a drag over the floor, a header or the sprite rims no cell
        Assert.Equal(0u, InventoryRules.Rim(0, new InvPlace(InvKind.Panel, Storage: 100), 0, new InvPlace(InvKind.Panel, Storage: 100)));
        // a quick transfer with nothing to go into blinks as a refusal from the server does
        model.RefuseLocally(11, 20.0);
        Assert.Equal(11, InventoryRules.Blinking(model, 20.1));
    }

    [Fact]
    public void TheOpenedNoticeGoesOncePerTurningTheInventoryOnAtMostOnceASecond()
    {
        var notice = new InventoryNotice();
        Assert.True(notice.Opened(turnedOn: true, humanHud: true, now: 5.0)); // Tab (or the inventory key) turns it on
        Assert.False(notice.Opened(turnedOn: false, humanHud: true, now: 5.3)); // and off: no notice
        Assert.False(notice.Opened(turnedOn: true, humanHud: true, now: 5.6)); // on again within the second: the server would not open them again
        Assert.True(notice.Opened(turnedOn: true, humanHud: true, now: 6.0));
        Assert.False(notice.Opened(turnedOn: true, humanHud: false, now: 9.0)); // another HUD has no Tab inventory
        // five presses in two seconds (on, off, on, off, on): two notices, a second apart
        var quick = new InventoryNotice();
        int sent = new[] { (true, 0.0), (false, 0.4), (true, 0.8), (false, 1.2), (true, 1.6) }.Count(p => quick.Opened(p.Item1, true, p.Item2));
        Assert.Equal(2, sent);
        // the inventory key is cursor mode's key, so it goes the same way; nothing else turns the notice on (chat typing turns cursor mode on by
        // itself, MouseOwner.Windows, and presses no key)
        Assert.Equal(PlayAction.CursorMode, TabInventory.KeyAction(PlayAction.Inventory));
        var mouse = new MouseOwner();
        Assert.True(mouse.Windows(true) && mouse.CursorMode);
    }

    [Fact]
    public void TgsStorageWindowIsNotShownForAListedStorageAndIsKeptForAnyOtherContainer()
    {
        (List<CarriedStorage> storages, _) = StorageColumnTests.Carried(7, 3);
        // the backpack's window: its items are the backpack's
        Assert.True(InventoryRules.ListedWindow(new long[] { 101, 102 }, storages, actedOn: 0));
        Assert.True(InventoryRules.ListedWindow(new long[] { 301 }, storages, actedOn: 0)); // the pocket box's
        // a crate's: items of no listed storage, whatever was clicked
        Assert.False(InventoryRules.ListedWindow(new long[] { 7001, 7002 }, storages, actedOn: 100));
        // an empty storage's window: listed when the player last acted on a listed storage holding none (a crate otherwise)
        var empty = new List<CarriedStorage>(storages) { [0] = StorageColumnTests.Storage(100, StoragePlace.Back, "backpack", 21, 0, 21) };
        Assert.True(InventoryRules.ListedWindow(Array.Empty<long>(), empty, actedOn: 100));
        Assert.False(InventoryRules.ListedWindow(Array.Empty<long>(), empty, actedOn: 8000));
        Assert.False(InventoryRules.ListedWindow(Array.Empty<long>(), empty, actedOn: 0));
        Assert.False(InventoryRules.ListedWindow(Array.Empty<long>(), storages, actedOn: 300)); // the box holds items: its window would show them
        // no storages listed (another mob): tg's window always
        Assert.False(InventoryRules.ListedWindow(new long[] { 101 }, StoragesMessage.None.Storages, actedOn: 100));
    }

    [Fact]
    public void TheQuickTransferIsItsOwnRebindableActionSharingPullsCtrlClick()
    {
        KeyBindings keys = KeyBindings.Defaults();
        Assert.Equal(new[] { "Ctrl_MouseLeft" }, keys.KeysOf(PlayAction.QuickTransfer));
        Assert.Equal(PlayAction.Pull, keys.ActionOfPress("MouseLeft", shift: false, ctrl: true)); // Pull everywhere but on an inventory item
        Assert.True(KeyBindings.SharesKeys(PlayAction.Pull, PlayAction.QuickTransfer));
        Assert.False(KeyBindings.SharesKeys(PlayAction.Pull, PlayAction.Examine));
        // binding either leaves the other its key; another action takes it from both
        Assert.Null(keys.Bind(PlayAction.Pull, "Ctrl_MouseLeft"));
        Assert.Equal(new[] { "Ctrl_MouseLeft" }, keys.KeysOf(PlayAction.QuickTransfer));
        Assert.Equal("Ctrl_Shift_MouseLeft", KeyBindings.MouseBindingName(PlayAction.QuickTransfer, "MouseLeft", true, true));
        keys.Bind(PlayAction.QuickTransfer, "Ctrl_Shift_MouseLeft");
        Assert.Equal(new[] { "Ctrl_MouseLeft" }, keys.KeysOf(PlayAction.Pull));
        keys.Bind(PlayAction.Examine, "Ctrl_MouseLeft");
        Assert.Empty(keys.KeysOf(PlayAction.Pull));
        // the options file keeps both on the same press, read in either order
        var options = new PlayOptions();
        Assert.True(options.TrySetBinding("quick_transfer", "Ctrl_MouseLeft", out _));
        Assert.True(options.TrySetBinding("pull", "Ctrl_MouseLeft", out _));
        Assert.Equal(new[] { "Ctrl_MouseLeft" }, options.Bindings.KeysOf(PlayAction.QuickTransfer));
        Assert.Equal(new[] { "Ctrl_MouseLeft" }, options.Bindings.KeysOf(PlayAction.Pull));
        Assert.Equal("quick_transfer", KeyBindings.IdOf(PlayAction.QuickTransfer));
        // the quick transfer is Ctrl+left click and no other modifier: with Alt held too, or without Ctrl, it goes the old way
        KeyBindings defaults = KeyBindings.Defaults();
        Assert.True(InventoryRules.IsQuickTransfer(defaults, KeyBindings.NameOf("MouseLeft", false, true), alt: false));
        Assert.False(InventoryRules.IsQuickTransfer(defaults, KeyBindings.NameOf("MouseLeft", false, true), alt: true));
        Assert.False(InventoryRules.IsQuickTransfer(defaults, KeyBindings.NameOf("MouseLeft", true, true), alt: false));
        Assert.False(InventoryRules.IsQuickTransfer(defaults, KeyBindings.NameOf("MouseLeft", false, false), alt: false));
        Assert.False(InventoryRules.IsQuickTransfer(defaults, KeyBindings.NameOf("MouseLeft", true, false), alt: false));
    }
}
