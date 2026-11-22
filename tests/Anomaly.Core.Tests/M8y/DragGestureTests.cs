using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.M8y;

/// <summary>
/// M8y slice D (D224-D226, D230): the cursor mode's drag gestures in a booted toy room with a joined player, Bob. The decision (D225: a pair
/// with its own tg handler is tg's alone; the rest are gestures by receiver) and each gesture through the client seam's drop: an item from
/// the floor into a hand, onto Bob, onto an equipment slot, into a backpack, a box and an open storage's cells; a carried item onto a table
/// (the click), onto the floor; and each refusal with its line (hand full, hands full, anchored, slot taken, the equip refusal, too big,
/// too far away). The radial's Buckle, Unbuckle, Pull and Stop pulling (RadialExtras).
/// </summary>
public class DragGestureTests
{
    private const int Second = 20;
    private const string Drag = "left=1;button=left;drag=left";

    private sealed class Feed : IMessageSink
    {
        public List<string> Lines { get; } = new();

        void IMessageSink.Chat(object target, DmList message) => Lines.Add(Dm.str(message?["html"]));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) => Lines.Add("balloon: " + text);

        public bool Any(string text) => Lines.Any(l => l != null && l.Contains(text, StringComparison.Ordinal));
    }

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, Feed Feed);

    private static Rig Build(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        r.Step(Second);
        return new Rig(r, bob, feed);
    }

    private static T Put<T>(AudioRoom r, atom loc) where T : atom_movable => r.In(() => Dm.@new<T>(loc));

    private static turf T(AudioRoom r, int x, int y) => r.In(() => locate(x, y, 1));

    private static void Drop(Rig g, atom dragged, atom over)
    {
        g.R.Command(() => ClientSeam.Drop(g.Bob.client, dragged, over, Drag, Drag, 5));
        g.R.Step(2);
    }

    private static atom_movable_screen_inventory Slot(Rig g, string key) => g.R.In(() => g.Bob.hud_used.screen_objects[key] as atom_movable_screen_inventory);

    private static atom_movable_screen_inventory Hand(Rig g, int index) => Slot(g, Dm.text("hand_slot:[]", index));

    private static atom_movable_screen_inventory Back(Rig g) => Slot(g, Dm.text("item_slot:[]", ITEM_SLOT_BACK));

    private static void Hold(Rig g, obj_item item) => g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.put_in_active_hand(item)))));

    private static void Wear(Rig g, obj_item item, float slot) => g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.equip_to_slot_if_possible(item, slot)))));

    // ---- the decision (D225) ----

    [Fact]
    public void APairWithItsOwnTgHandlerIsTgsAndTheRestAreGesturesByReceiver()
    {
        var g = Build(9301);
        g.R.In(() =>
        {
            mob_living_carbon_human bob = g.Bob;
            var pen = Dm.@new<obj_item_pen>(locate(5, 4, 1));
            var table = Dm.@new<obj_structure_table>(locate(5, 5, 1));
            var chair = Dm.@new<obj_structure_chair>(locate(3, 4, 1));
            var closet = Dm.@new<obj_structure_closet>(locate(3, 5, 1));
            var box = Dm.@new<obj_item_storage_box>(locate(4, 5, 1));
            Assert.Equal(DragReceiver.Hand, DragGestures.Classify(pen, Hand(g, 1), bob));
            Assert.Equal(DragReceiver.Self, DragGestures.Classify(pen, bob, bob));
            Assert.Equal(DragReceiver.Slot, DragGestures.Classify(pen, Back(g), bob));
            Assert.Equal(DragReceiver.Storage, DragGestures.Classify(pen, box, bob));
            Assert.Equal(DragReceiver.Floor, DragGestures.Classify(pen, locate(4, 3, 1), bob));
            Assert.Equal(DragReceiver.Thing, DragGestures.Classify(pen, table, bob));
            Assert.True(DragGestures.Applies(pen, table, bob));
            Assert.True(DragGestures.Applies(pen, chair, bob)); // /atom/movable's buckling takes a dropped mob only
            Assert.False(DragGestures.Applies(pen, closet, bob)); // the closet's own mouse_drop_receive (closets.dm:987)
            Assert.False(DragGestures.Applies(bob, chair, bob)); // a mob dragged: tg's buckling
            Assert.False(DragGestures.Applies(table, pen, bob)); // not an item
            var other = Dm.@new<obj_item_pen>(box);
            Assert.False(DragGestures.Applies(other, table, bob)); // in a storage he has not opened
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnOpenStoragesCellTakesAnItemFromElsewhereAndItsOwnItemsReorderAsTgs()
    {
        var g = Build(9302);
        var pack = Put<obj_item_storage_backpack>(g.R, g.Bob.loc);
        Wear(g, pack, ITEM_SLOT_BACK);
        obj_item a = Put<obj_item_pen>(g.R, pack), b = Put<obj_item_pen>(g.R, pack);
        g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(pack.atom_storage.open_storage(g.Bob)))));
        var cell = g.R.In(() => Dm.each_cast<object>(g.Bob.client.screen).OfType<atom_movable_screen_storage_cell>().First());
        var pen = Put<obj_item_pen>(g.R, T(g.R, 5, 4));
        g.R.In(() =>
        {
            Assert.False(DragGestures.Applies(a, b, g.Bob)); // tg's reorder within the storage
            Assert.False(DragGestures.Applies(a, cell, g.Bob)); // the cell's own handler for its own item
            Assert.True(DragGestures.Applies(pen, cell, g.Bob));
            Assert.True(DragGestures.Applies(pen, a, g.Bob)); // an item shown in the open storage: into that storage
        });
        Drop(g, pen, cell);
        g.R.In(() => Assert.Same(pack, pen.loc));
        g.R.AssertNoRuntimes();
    }

    // ---- the gestures ----

    [Fact]
    public void AFloorItemGoesIntoTheHandItIsDroppedOnOrOntoBobAndAFullHandSaysSo()
    {
        var g = Build(9303);
        var pen = Put<obj_item_pen>(g.R, T(g.R, 5, 4));
        Drop(g, pen, Hand(g, 2));
        g.R.In(() => Assert.Same(pen, g.Bob.get_item_for_held_index(2)));

        var other = Put<obj_item_pen>(g.R, T(g.R, 5, 4));
        Drop(g, other, Hand(g, 2));
        g.R.In(() => Assert.IsAssignableFrom<turf>(other.loc));
        Assert.True(g.Feed.Any("balloon: hand full"));

        Drop(g, other, g.Bob); // onto himself: tg's put_in_hands
        g.R.In(() => Assert.Same(other, g.Bob.get_item_for_held_index(1)));

        var third = Put<obj_item_pen>(g.R, T(g.R, 5, 4));
        Drop(g, third, g.Bob);
        g.R.In(() => Assert.IsAssignableFrom<turf>(third.loc));
        Assert.True(g.Feed.Any("balloon: hands full"));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AFloorItemOntoAnEquipmentSlotIsWornOrRefusedWithTgsLineAndATakenSlotSaysSo()
    {
        var g = Build(9304);
        var pack = Put<obj_item_storage_backpack>(g.R, T(g.R, 5, 4));
        Drop(g, pack, Back(g));
        g.R.In(() => Assert.Same(pack, g.Bob.back));

        g.R.In(() => Waiting.NoSleep(g.Bob.dropItemToGround(pack)));
        var jet = Put<obj_item_tank_jetpack_carbondioxide>(g.R, g.Bob.loc);
        Wear(g, jet, ITEM_SLOT_BACK);
        g.R.Step(Second);
        var pen = Put<obj_item_pen>(g.R, T(g.R, 5, 4));
        Drop(g, pen, Back(g));
        Assert.True(g.Feed.Any("balloon: slot taken"));
        g.R.In(() => Assert.IsAssignableFrom<turf>(pen.loc));

        g.R.In(() => Waiting.NoSleep(g.Bob.dropItemToGround(jet)));
        g.R.Step(Second);
        Drop(g, pen, Back(g)); // a pen does not go on the back: picked up, then tg's refusal, and it stays in the hand
        Assert.True(g.Feed.Any("You are unable to equip that!"));
        g.R.In(() => Assert.Same(pen, g.Bob.get_active_held_item()));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ItemsGoIntoAWornBackpackAndABoxOnTheFloorAndATooBigOneIsTold()
    {
        var g = Build(9305);
        var pack = Put<obj_item_storage_backpack>(g.R, g.Bob.loc);
        Wear(g, pack, ITEM_SLOT_BACK);
        var pen = Put<obj_item_pen>(g.R, g.Bob.loc);
        Hold(g, pen);
        Drop(g, pen, pack); // the held pen onto the worn backpack: tg's own (the active held item, on_mousedropped_onto)
        g.R.In(() => Assert.Same(pack, pen.loc));

        var floorPen = Put<obj_item_pen>(g.R, T(g.R, 5, 4));
        Drop(g, floorPen, pack); // a floor item onto the backpack: the storage's attempt_insert
        g.R.In(() => Assert.Same(pack, floorPen.loc));

        var box = Put<obj_item_storage_box>(g.R, T(g.R, 4, 5));
        var other = Put<obj_item_pen>(g.R, T(g.R, 5, 4));
        Drop(g, other, box);
        g.R.In(() => Assert.Same(box, other.loc));

        var big = Put<obj_item_pen>(g.R, T(g.R, 5, 4));
        g.R.In(() => big.w_class = WEIGHT_CLASS_BULKY);
        Drop(g, big, box);
        Assert.True(g.Feed.Any("balloon: too big!"));
        g.R.In(() => Assert.IsAssignableFrom<turf>(big.loc));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ACarriedItemOntoATableIsPlacedThereAndOntoTheFloorIsDroppedThere()
    {
        var g = Build(9306);
        var table = Put<obj_structure_table>(g.R, T(g.R, 5, 4));
        var pen = Put<obj_item_pen>(g.R, g.Bob.loc);
        g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.put_in_hand(pen, 2)))));
        Drop(g, pen, table); // its hand made active, then the table clicked with it: tg's table_place
        g.R.In(() => Assert.Same(table.loc, pen.loc));
        g.R.Step(Second); // the click's cooldown

        var floorPen = Put<obj_item_pen>(g.R, T(g.R, 3, 4));
        Drop(g, floorPen, table); // a floor item onto the table: picked up into the empty active hand, then the click
        g.R.In(() => Assert.Same(table.loc, floorPen.loc));
        g.R.Step(Second);

        var held = Put<obj_item_pen>(g.R, g.Bob.loc);
        Hold(g, held);
        turf floor = T(g.R, 4, 3);
        Drop(g, held, floor);
        g.R.In(() => Assert.Same(floor, held.loc));

        var far = Put<obj_item_pen>(g.R, g.Bob.loc);
        Hold(g, far);
        Drop(g, far, T(g.R, 8, 4));
        Assert.True(g.Feed.Any("You are too far away!"));
        g.R.In(() => Assert.Same(g.Bob, far.loc));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnAnchoredFloorItemStaysAndSaysSo()
    {
        var g = Build(9307);
        var pen = Put<obj_item_pen>(g.R, T(g.R, 5, 4));
        g.R.In(() => Waiting.NoSleep(pen.set_anchored(Dm.TRUE)));
        Drop(g, pen, Hand(g, 1));
        Assert.True(g.Feed.Any("balloon: anchored"));
        g.R.In(() => Assert.IsAssignableFrom<turf>(pen.loc));
        g.R.AssertNoRuntimes();
    }

    // ---- the radial's entries (D230) ----

    [Fact]
    public void TheRadialOffersBuckleUnbuckleAndPullOnlyWhenPossible()
    {
        var g = Build(9308);
        var chair = Put<obj_structure_chair>(g.R, T(g.R, 5, 4));
        var crate = Put<obj_structure_closet_crate>(g.R, T(g.R, 4, 5));
        var far = Put<obj_structure_chair>(g.R, T(g.R, 8, 4));
        RadialExtra[] Of(atom target) => g.R.In(() => RadialExtras.Of(g.Bob, target).Select(e => e.Kind).ToArray());

        Assert.Equal(new[] { RadialExtra.Buckle }, Of(chair)); // a chair is anchored: not pulled
        Assert.Empty(Of(far));
        Assert.Equal(new[] { RadialExtra.Climb, RadialExtra.Pull }, Of(crate)); // M8z (D238): a closed crate is climbable

        Drop(g, g.Bob, chair); // the radial's Buckle: tg's drag of himself onto it
        g.R.In(() => Assert.Same(chair, g.Bob.buckled));
        Assert.Equal(new[] { RadialExtra.Unbuckle }, Of(chair));

        var pen = Put<obj_item_pen>(g.R, g.Bob.loc);
        Hold(g, pen);
        Assert.DoesNotContain(RadialExtra.Unbuckle, Of(chair)); // tg's hand unbuckle needs the empty hand
        g.R.In(() => Waiting.NoSleep(g.Bob.dropItemToGround(pen)));

        g.R.Command(() => ClientSeam.Click(g.Bob.client, chair, "left=1;button=left;icon-x=16;icon-y=16"));
        g.R.Step(2);
        g.R.In(() => Assert.Null(g.Bob.buckled));
        g.R.Step(Second);

        g.R.Command(() => ClientSeam.Click(g.Bob.client, crate, "left=1;ctrl=1;button=left;icon-x=16;icon-y=16"));
        g.R.Step(2);
        g.R.In(() => Assert.Same(crate, g.Bob.pulling));
        Assert.Equal(new[] { RadialExtra.Climb, RadialExtra.StopPulling }, Of(crate));
        g.R.AssertNoRuntimes();
    }
}
