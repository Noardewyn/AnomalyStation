using Anomaly.Client.Shared;
using Anomaly.Protocol;
using static Anomaly.Server.Tests.Client.Play.TabStripViewTests;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 12, slice 4D-1: what a click, a right click and a drag on the strip view send (InventoryRules with the strip window shown):
/// tg's <c>use</c> on a slot, tg's <c>alt</c> from the menu, tg's close from the X, the world's examine from the sprite, and nothing for every other case
/// (every obscured state, tg's window not interactive, another HUD).
/// </summary>
public class StripViewRulesTests
{
    private const int Active = 2;
    private const long Held = 555;

    private static int K(string key) => StripView.IndexOf(key);

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    private static InvPlace Resolve(long id, StripModel strip, bool humanHud = true)
        => InventoryRules.Resolve(Screen(id), new Dictionary<long, ScreenEntry>(), new List<StoragePanel>(), 0, new long[7], humanHud, null, strip);

    private static InvPlace Slot(string key, StripModel strip) => Resolve(StripView.SlotId(K(key)), strip);

    // every state tg's window can give a slot: an item, an empty slot, a hidden pocket, a blocked slot, an inaccessible item and an inaccessible empty slot
    private static StripModel States(int status = 2) => Model(new[]
    {
        ("shoes", Item("black shoes", alternates: new[] { "knot", "unknown_action" })),
        ("head", Bare()),
        ("left_pocket", Bare(2)),
        ("suit", Bare(1)),
        ("gloves", Item("insulated gloves", obscured: 3, alternates: "strip_accessory")),
        ("neck", Bare(3)),
        ("jumpsuit", Item("grey jumpsuit", alternates: new[] { "adjust_jumpsuit", "adjust_sensor" })),
    }, status);

    private static readonly InvPlace Hand = new(InvKind.Hand, Held, Active);
    private static readonly InvPlace OtherHand = new(InvKind.Hand, 777, 1);

    [Fact]
    public void ThePanelsIdsResolveToTheStripPlacesOnlyWhileTheStripViewIsShownOnTheHumanHud()
    {
        StripModel m = States();
        Assert.Equal(new InvPlace(InvKind.Strip, StripView.FullMark, K("shoes")), Slot("shoes", m));
        Assert.Equal(new InvPlace(InvKind.Strip, 0, K("head")), Slot("head", m));
        Assert.Equal(new InvPlace(InvKind.Strip, StripView.FullMark, K("left_pocket")), Slot("left_pocket", m)); // a hidden pocket is full
        Assert.Equal(new InvPlace(InvKind.Strip, 0, K("suit")), Slot("suit", m));
        Assert.Equal(new InvPlace(InvKind.Strip, Index: -1), Resolve(StripView.PanelId, m));
        Assert.Equal(InvKind.StripClose, Resolve(StripView.CloseId, m).Kind);
        Assert.Equal(InvKind.StripSprite, Resolve(StripView.SpriteId, m).Kind);
        // a slot tg does not show: nothing
        Assert.Equal(InvKind.None, Slot("mask", m).Kind);
        // no strip view, or another HUD: none of its ids is a place
        foreach (long id in new[] { StripView.PanelId, StripView.CloseId, StripView.SpriteId, StripView.SlotId(K("shoes")) })
        {
            Assert.Equal(InvKind.None, Resolve(id, null).Kind);
            Assert.Equal(InvKind.None, Resolve(id, m, humanHud: false).Kind);
        }
    }

    [Fact]
    public void ALeftClickOnASlotIsTgsUseEvenWhenItLooksEmptyExceptOnAnObscuredOrInaccessibleSlot()
    {
        StripModel m = States();
        InvSend? Click(string key, long held = 0, ClickModifiers mods = ClickModifiers.None, StripModel model = null)
            => InventoryRules.Click(Slot(key, model ?? m), mods, Active, held, model ?? m);
        Assert.Equal(InvSend.StripUse(K("shoes")), Click("shoes"));
        Assert.Equal(InvSend.StripUse(K("shoes")), Click("shoes", Held)); // tg decides (the held item is not put on a full slot)
        Assert.Equal(InvSend.StripUse(K("left_pocket")), Click("left_pocket")); // tg's pickpocket
        Assert.Equal(InvSend.StripUse(K("head")), Click("head", Held)); // put the held item on
        Assert.Equal(InvSend.StripUse(K("head")), Click("head")); // empty-looking, empty hand: still tg's use (tg's data hides a wig there); tg decides
        Assert.Equal(InvSend.Nothing, Click("suit", Held)); // completely obscured
        Assert.Equal(InvSend.Nothing, Click("gloves")); // inaccessible item
        Assert.Equal(InvSend.Nothing, Click("neck", Held)); // inaccessible empty slot
        // with a modifier: nothing (Shift is not the slot's examine, Ctrl not a transfer)
        Assert.Equal(InvSend.Nothing, Click("shoes", mods: ClickModifiers.Shift));
        Assert.Equal(InvSend.Nothing, Click("shoes", mods: ClickModifiers.Alt));
        Assert.Equal(InvSend.Nothing, InventoryRules.QuickTransfer(Slot("shoes", m), Active, 1, 2));
        // tg's window not interactive: no slot takes a click
        StripModel far = States(status: 1);
        foreach (string key in new[] { "shoes", "head", "left_pocket", "jumpsuit" })
            Assert.Equal(InvSend.Nothing, Click(key, Held, model: far));
        // the panel's backing: nothing
        Assert.Equal(InvSend.Nothing, InventoryRules.Click(Resolve(StripView.PanelId, m), ClickModifiers.None, Active, Held, m));
    }

    [Fact]
    public void TheXClosesTgsWindowAtAnyStatusAndShiftOnTheSpriteExaminesTheTarget()
    {
        foreach (StripModel m in new[] { States(2), States(1), States(0) })
        {
            Assert.Equal(new InvSend(InvSendKind.StripClose), InventoryRules.Click(Resolve(StripView.CloseId, m), ClickModifiers.None, Active, 0, m));
            Assert.Equal(InvSend.Nothing, InventoryRules.Click(Resolve(StripView.CloseId, m), ClickModifiers.Shift, Active, 0, m));
            Assert.Equal(new InvSend(InvSendKind.StripExamine), InventoryRules.Click(Resolve(StripView.SpriteId, m), ClickModifiers.Shift, Active, 0, m));
            Assert.Equal(InvSend.Nothing, InventoryRules.Click(Resolve(StripView.SpriteId, m), ClickModifiers.None, Active, Held, m));
        }
    }

    [Fact]
    public void ARightClickOffersTgsAlternateActionsWithTgsLabelsOnAnItemTgLetsBeUsed()
    {
        StripModel m = States();
        Assert.Equal(new[] { ("knot", "Knot") }, InventoryRules.StripAlternates(Slot("shoes", m), m)); // tg's window draws no "unknown_action"
        Assert.Equal(new[] { ("adjust_jumpsuit", "Adjust jumpsuit"), ("adjust_sensor", "Adjust sensors") }, InventoryRules.StripAlternates(Slot("jumpsuit", m), m));
        Assert.Empty(InventoryRules.StripAlternates(Slot("gloves", m), m)); // inaccessible: tg disables them
        Assert.Empty(InventoryRules.StripAlternates(Slot("left_pocket", m), m));
        Assert.Empty(InventoryRules.StripAlternates(Slot("head", m), m));
        Assert.Empty(InventoryRules.StripAlternates(Resolve(StripView.SpriteId, m), m));
        Assert.Empty(InventoryRules.StripAlternates(Resolve(StripView.PanelId, m), m));
        StripModel far = States(status: 1);
        Assert.Empty(InventoryRules.StripAlternates(Slot("jumpsuit", far), far));
        Assert.Empty(InventoryRules.StripAlternates(Slot("jumpsuit", m), null));
    }

    [Fact]
    public void ADragOfTheActiveHandsItemOntoAnEmptyUsableSlotIsTgsUseAndAnyOtherDragOntoThePanelIsNothing()
    {
        StripModel m = States();
        Assert.Equal(InvSend.StripUse(K("head")), InventoryRules.Drop(Hand, Slot("head", m), Active, m));
        // onto a full one, a blocked or an inaccessible one: nothing
        foreach (string key in new[] { "shoes", "left_pocket", "suit", "neck", "gloves" })
            Assert.Equal(InvSend.Nothing, InventoryRules.Drop(Hand, Slot(key, m), Active, m));
        // from the other hand, or from anything else of the player's: nothing
        Assert.Equal(InvSend.Nothing, InventoryRules.Drop(OtherHand, Slot("head", m), Active, m));
        foreach (InvPlace from in new[]
        {
            new InvPlace(InvKind.Cell, 9, 0, 100, 1), new InvPlace(InvKind.Belt, 9, 1, 101, 1), new InvPlace(InvKind.Slot, 9, 1 << 11),
            new InvPlace(InvKind.Nearby, 9, 0), new InvPlace(InvKind.Hand, 0, Active),
        })
            Assert.Equal(InvSend.Nothing, InventoryRules.Drop(from, Slot("head", m), Active, m));
        // onto the sprite, the X or the backing: nothing
        foreach (long id in new[] { StripView.SpriteId, StripView.CloseId, StripView.PanelId })
            Assert.Equal(InvSend.Nothing, InventoryRules.Drop(Hand, Resolve(id, m), Active, m));
        // tg's window not interactive: nothing
        StripModel far = States(status: 1);
        Assert.Equal(InvSend.Nothing, InventoryRules.Drop(Hand, Slot("head", far), Active, far));
    }

    [Fact]
    public void ADragOfTheirWornItemOntoTheInventoryOrTheFloorIsTgsStripAndAnywhereElseNothing()
    {
        StripModel m = States();
        InvPlace shoes = Slot("shoes", m);
        foreach (InvPlace over in new[]
        {
            new InvPlace(InvKind.Hand, 0, 1), new InvPlace(InvKind.Hand, Held, Active), new InvPlace(InvKind.Belt, 0, 3, 101, 3), new InvPlace(InvKind.Belt, 0, 3),
            new InvPlace(InvKind.Slot, 0, 1 << 11), new InvPlace(InvKind.Cell, 0, 4, 100), new InvPlace(InvKind.Panel, Storage: 100),
            new InvPlace(InvKind.Nearby, 0, -1), new InvPlace(InvKind.Nearby, 44, 0), new InvPlace(InvKind.Floor),
        })
            Assert.Equal(InvSend.StripUse(K("shoes")), InventoryRules.Drop(shoes, over, Active, m));
        // onto another of their slots, the sprite, the X, a thing in the view, tg's HUD or nothing: nothing
        foreach (InvPlace over in new[]
        {
            Slot("head", m), Slot("jumpsuit", m), Resolve(StripView.SpriteId, m), Resolve(StripView.CloseId, m), Resolve(StripView.PanelId, m),
            new InvPlace(InvKind.Thing, 12), new InvPlace(InvKind.Hud), new InvPlace(InvKind.None), new InvPlace(InvKind.Sprite),
        })
            Assert.Equal(InvSend.Nothing, InventoryRules.Drop(shoes, over, Active, m));
        // a hidden pocket is stripped the same way (tg's pickpocket)
        Assert.Equal(InvSend.StripUse(K("left_pocket")), InventoryRules.Drop(Slot("left_pocket", m), new InvPlace(InvKind.Hand, 0, 1), Active, m));
        // an empty, blocked or inaccessible slot is no drag source
        foreach (string key in new[] { "head", "suit", "gloves", "neck" })
            Assert.Equal(InvSend.Nothing, InventoryRules.Drop(Slot(key, m), new InvPlace(InvKind.Floor), Active, m));
        // tg's window not interactive, or gone: nothing
        StripModel far = States(status: 1);
        Assert.Equal(InvSend.Nothing, InventoryRules.Drop(Slot("shoes", far), new InvPlace(InvKind.Floor), Active, far));
        Assert.Equal(InvSend.Nothing, InventoryRules.Drop(shoes, new InvPlace(InvKind.Floor), Active, null));
    }

    [Fact]
    public void ADragOverASlotRimsItAndTheOtherHudsKeepTheirOldDrags()
    {
        StripModel m = States();
        Assert.Equal(UiTokens.InventoryDragRim, InventoryRules.Rim(0, Slot("head", m), 0, Slot("head", m)));
        Assert.Equal(0u, InventoryRules.Rim(0, Slot("head", m), 0, Slot("shoes", m)));
        // another HUD: the panel's ids are no place, so a drag from a hand onto them is no strip use (and the strip view is not drawn there)
        InvPlace over = Resolve(StripView.SlotId(K("head")), m, humanHud: false);
        Assert.Equal(InvKind.None, over.Kind);
        Assert.NotEqual(InvSendKind.StripUse, InventoryRules.Drop(Hand, over, Active, m).Kind);
        // the old rules are untouched without a strip view: a hand onto the floor is still the move to the floor
        Assert.Equal(InvSend.Move(Held, InventoryTarget.Floor), InventoryRules.Drop(Hand, new InvPlace(InvKind.Floor), Active, null));
    }
}
