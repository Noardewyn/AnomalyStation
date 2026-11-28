using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ui-overhaul.md section 12, slice 4C-1 (DV696): the other container through a real server and two headless clients. Alice (a backpack and a toolbelt)
/// opens a box on her tile by tg's own way (a right click on it): her Storages message lists it last, with its contents, and Bob's does not; she moves
/// items out of it to her hand, her backpack and belt cell 3 and one from her backpack into it, and examines one of its items; Bob, beside her, opens
/// it too and each has it; Bob takes an item and her message changes; she walks away and her entry goes, his stays. The message is not rebuilt while
/// nothing changes and follows each change within a tick or two.
/// </summary>
public class StoragesOtherGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private sealed record Rig(RealHeadlessWorld W, RealHeadlessWorld.Player Alice, mob_living_carbon_human Body, RealHeadlessWorld.Player Bob,
        mob_living_carbon_human BobBody, obj_item Backpack, obj_item Belt, obj_item Box) : IDisposable
    {
        public StoragesModel Model => Alice.Session.Storages;

        public void Dispose() => W.Dispose();
    }

    // Alice at (12, 10) in a jumpsuit with a backpack (a wrench) and a toolbelt; a box on her tile holding a pen, a crayon and a screwdriver; Bob at
    // (13, 10).
    private static Rig Make(ulong seed)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var body = (mob_living_carbon_human)w.MobOf(alice);
        var bobBody = (mob_living_carbon_human)w.MobOf(bob);
        obj_item backpack = null, belt = null, box = null;
        In(w, () =>
        {
            foreach (mob_living_carbon_human who in new[] { body, bobBody })
            {
                foreach (obj_item item in new[] { who.back, who.belt, who.l_store, who.r_store, who.wear_id, who.s_store,
                    who.get_item_for_held_index(1) as obj_item, who.get_item_for_held_index(2) as obj_item })
                    if (item != null)
                        qdel(item);
                if (who.w_uniform == null)
                    Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(who), ITEM_SLOT_ICLOTHING))));
            }
            Waiting.NoSleep(body.forceMove(locate(12, 10, 1)));
            Waiting.NoSleep(bobBody.forceMove(locate(13, 10, 1)));
            backpack = Dm.@new<obj_item_storage_backpack>(get_turf(body));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(backpack, ITEM_SLOT_BACK))));
            Dm.@new<obj_item_wrench>(backpack);
            belt = Dm.@new<obj_item_storage_belt_utility>(get_turf(body));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(belt, ITEM_SLOT_BELT))));
            box = Dm.@new<obj_item_storage_box>(get_turf(body));
            Dm.@new<obj_item_pen>(box);
            Dm.@new<obj_item_toy_crayon_red>(box);
            Dm.@new<obj_item_screwdriver>(box);
            return 0;
        });
        w.Run(Second);
        return new Rig(w, alice, body, bob, bobBody, backpack, belt, box);
    }

    // tg's own opening of a box on the floor: a right click on it with the empty hand (attack_hand_secondary -> open_storage_on_signal)
    private static void Open(Rig g, RealHeadlessWorld.Player who)
    {
        Assert.True(who.Session.Click(new ClickTarget(TargetKind.Entity, g.Box.dm_id, 0, 0, 0, 16, 16), ClickButton.Right, ClickModifiers.None));
        g.W.Run(6);
    }

    private static CarriedStorage OtherOf(StoragesModel model) => model.Storages.Count > 0 && model.Storages[^1].Place == StoragePlace.Other ? model.Storages[^1] : null;

    private static List<long> Contents(Rig g, obj_item storage) => In(g.W, () => Dm.each<obj_item>(storage.atom_storage.real_location.contents).Select(i => i.dm_id).ToList());

    private static obj_item Find<T>(Rig g, obj_item storage) => In(g.W, () => Dm.each<obj_item>(storage.atom_storage.real_location.contents).First(i => i is T));

    private static void Move(Rig g, RealHeadlessWorld.Player who, obj_item item, InventoryTarget to, obj_item storage = null, int index = 0)
    {
        Assert.True(who.Session.InventoryMove(item.dm_id, to, storage?.dm_id ?? 0, index));
        g.W.Run(10);
    }

    [Fact]
    public void Alice_opens_a_box_on_the_floor_moves_in_and_out_of_it_and_it_goes_when_she_walks_away()
    {
        using Rig g = Make(7401);
        RealHeadlessWorld w = g.W;
        int runtimes = w.World.RuntimeErrorCount;
        Assert.Null(OtherOf(g.Model));
        Open(g, g.Alice);
        Assert.Same(g.Box.atom_storage, In(w, () => g.Body.active_storage));
        CarriedStorage other = OtherOf(g.Model);
        Assert.True(other != null, "her message lists the box last: " + string.Join("; ", g.Model.Storages) + "\n" + w.Err);
        Assert.Equal(g.Box.dm_id, other.Entity);
        Assert.Equal(new[] { StoragePlace.Back, StoragePlace.Belt, StoragePlace.Other }, g.Model.Storages.Select(s => s.Place));
        Assert.Equal(Contents(g, g.Box), other.Items.Select(i => i.Entity));
        Assert.Equal(In(w, () => (int)g.Box.atom_storage.max_slots), other.Slots);
        Assert.Equal(In(w, () => (int)Dm.num(g.Box.atom_storage.get_total_weight())), other.Used);
        Assert.False(other.Locked || other.Hidden);
        foreach (StorageItem item in other.Items)
            Assert.True(g.Alice.Session.Replica.TryGetAppearance(item.Appearance, out _), item.Name + "'s picture is in her client's tables");
        Assert.Null(OtherOf(g.Bob.Session.Storages)); // Bob has not opened it
        // the examine of an item in it: tg's examine box
        var chat = new List<ServerText>();
        g.Alice.Client.ServerTextReceived += chat.Add;
        obj_item pen = Find<obj_item_pen>(g, g.Box), crayon = Find<obj_item_toy_crayon>(g, g.Box), screwdriver = Find<obj_item_screwdriver>(g, g.Box);
        Assert.True(g.Alice.Session.Click(new ClickTarget(TargetKind.Screen, pen.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.Shift));
        w.Run(Second);
        Assert.Contains(chat, t => t.Html.Contains("boxed_message", StringComparison.Ordinal) && t.Html.Contains("pen", StringComparison.OrdinalIgnoreCase));
        Assert.True(In(w, () => pen.loc == g.Box), "the examine moved nothing");

        int active = In(w, () => (int)Dm.num(g.Body.active_hand_index));
        Move(g, g.Alice, pen, InventoryTarget.Hand, index: active); // box to hand
        Assert.True(In(w, () => g.Body.get_item_for_held_index(active) == pen));
        Assert.DoesNotContain(pen.dm_id, OtherOf(g.Model).Items.Select(i => i.Entity));
        Move(g, g.Alice, crayon, InventoryTarget.Storage, g.Backpack); // box to backpack
        Assert.True(In(w, () => crayon.loc == g.Backpack));
        Assert.Contains(crayon.dm_id, g.Model.At(StoragePlace.Back).Items.Select(i => i.Entity));
        Move(g, g.Alice, screwdriver, InventoryTarget.Storage, g.Belt, 3); // box to belt cell 3
        Assert.True(In(w, () => screwdriver.loc == g.Belt));
        Assert.Equal(screwdriver.dm_id, g.Alice.Session.Hotbar.Cells[2].Entity);
        obj_item wrench = Find<obj_item_wrench>(g, g.Backpack);
        Move(g, g.Alice, wrench, InventoryTarget.Storage, g.Box); // backpack to box
        Assert.True(In(w, () => wrench.loc == g.Box));
        Assert.Equal(new[] { wrench.dm_id }, OtherOf(g.Model).Items.Select(i => i.Entity));
        Assert.Equal(0L, g.Model.RefusedItem);

        // Bob opens it too: each has it
        Open(g, g.Bob);
        Assert.Equal(g.Box.dm_id, OtherOf(g.Bob.Session.Storages)?.Entity);
        Assert.Equal(g.Box.dm_id, OtherOf(g.Model)?.Entity);
        // Bob takes the wrench: Alice's message changes
        int bobActive = In(w, () => (int)Dm.num(g.BobBody.active_hand_index));
        Move(g, g.Bob, wrench, InventoryTarget.Hand, index: bobActive);
        Assert.True(In(w, () => g.BobBody.get_item_for_held_index(bobActive) == wrench));
        Assert.Empty(OtherOf(g.Model).Items);
        Assert.Empty(OtherOf(g.Bob.Session.Storages).Items);
        // Alice walks two tiles north: tg's Move closes it for her; Bob's stays
        // steered from her own predicted place, as a player would (BuckleGameplayTests' WalkTo)
        MotionState start = g.Alice.Session.Prediction.Predicted;
        double toX = start.X / 256.0, toY = start.Y / 256.0 + 2;
        for (int i = 0; i < 8 * Second; i++)
        {
            MotionState now = g.Alice.Session.Prediction.Predicted;
            double dx = toX - now.X / 256.0, dy = toY - now.Y / 256.0;
            if (Math.Sqrt(dx * dx + dy * dy) < 0.15)
                break;
            g.Alice.Keys = MoveKeys.Forward;
            g.Alice.Yaw = Math.Atan2(-dx, dy); // Godot's yaw: 0 faces north, +90 degrees west
            w.Run(1);
        }
        g.Alice.Keys = MoveKeys.None;
        w.Run(4);
        Assert.True(In(w, () => (int)g.Body.y) >= 12, "two tiles or more from the box");
        w.Run(4);
        Assert.Null(In(w, () => g.Body.active_storage));
        Assert.Null(OtherOf(g.Model));
        Assert.Equal(new[] { StoragePlace.Back, StoragePlace.Belt }, g.Model.Storages.Select(s => s.Place));
        Assert.Equal(g.Box.dm_id, OtherOf(g.Bob.Session.Storages)?.Entity);
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void The_message_is_not_rebuilt_while_nothing_changes_and_follows_each_change_within_two_ticks()
    {
        using Rig g = Make(7402);
        RealHeadlessWorld w = g.W;
        int session = w.Host.LiveSession("Alice").Ordinal;
        Open(g, g.Alice);
        Open(g, g.Bob);
        Assert.NotNull(OtherOf(g.Model));
        int builds = w.Replicator.StoragesBuilds(session);
        w.Run(2 * Second);
        Assert.Equal(builds, w.Replicator.StoragesBuilds(session)); // forty ticks with the box open and nothing happening: no build
        // another player takes an item out of the same box: her message has it gone within two ticks
        obj_item pen = Find<obj_item_pen>(g, g.Box);
        In(w, () =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Box.atom_storage.remove_single(g.BobBody, pen, g.BobBody))));
            Waiting.NoSleep(g.BobBody.put_in_hands(pen));
            return 0;
        });
        int ticks = 0;
        while (OtherOf(g.Model).Items.Any(i => i.Entity == pen.dm_id) && ticks < 10)
        {
            w.Run(1);
            ticks++;
        }
        Assert.True(ticks <= 2, "the pen's leaving took " + ticks + " ticks");
        // the box moved out of her reach (tg's close_distance): her entry goes within two ticks
        In(w, () =>
        {
            Waiting.NoSleep(g.Box.forceMove(locate(16, 10, 1)));
            return 0;
        });
        ticks = 0;
        while (OtherOf(g.Model) != null && ticks < 10)
        {
            w.Run(1);
            ticks++;
        }
        Assert.True(ticks <= 2, "the box's closing took " + ticks + " ticks");
        int after = w.Replicator.StoragesBuilds(session);
        w.Run(Second);
        Assert.Equal(after, w.Replicator.StoragesBuilds(session));
    }
}
