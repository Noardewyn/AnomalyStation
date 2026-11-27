using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ui-overhaul.md section 12, slice 4B-1 (DV692): the Tab inventory's server half through a real server and headless clients. Alice wears a backpack, a
/// toolbelt and a box in a pocket; her client receives the storages with their contents (protocol 99's Storages, StoragesModel), sends moves
/// (PlaySession.InventoryMove) that the server does with tg's procs, and the Tab notice (PlaySession.InventoryOpened) whose rustle Bob, next to her,
/// hears once a second. Bob never receives a Storages message about Alice; Alice's changes when an item leaves her bag or pocket another way.
/// </summary>
public class StoragesGameplayTests
{
    private const int Second = 20;

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public StoragesGameplayTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private sealed record Rig(RealHeadlessWorld W, RealHeadlessWorld.Player Alice, mob_living_carbon_human Body, RealHeadlessWorld.Player Bob,
        mob_living_carbon_human BobBody, obj_item Backpack, obj_item Belt, obj_item PocketBox) : IDisposable
    {
        public StoragesModel Model => Alice.Session.Storages;

        public void Dispose() => W.Dispose();
    }

    // Alice in a jumpsuit with a backpack (a screwdriver and a wrench in it), a toolbelt (a crowbar) and a box in her right pocket (a pen); Bob next
    // to her.
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
                foreach (obj_item item in new[] { who.back, who.belt, who.l_store, who.r_store, who.wear_id, who.s_store, who.shoes,
                    who.get_item_for_held_index(1) as obj_item, who.get_item_for_held_index(2) as obj_item })
                    if (item != null)
                        qdel(item);
                if (who.w_uniform == null)
                    Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(who), ITEM_SLOT_ICLOTHING))));
            }
            backpack = Dm.@new<obj_item_storage_backpack>(get_turf(body));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(backpack, ITEM_SLOT_BACK))));
            Dm.@new<obj_item_screwdriver>(backpack);
            Dm.@new<obj_item_wrench>(backpack);
            belt = Dm.@new<obj_item_storage_belt_utility>(get_turf(body));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(belt, ITEM_SLOT_BELT))));
            Dm.@new<obj_item_crowbar>(belt);
            // tg's box is normal-sized and does not fit a pocket by hand; it is put there as an outfit's equip_to_slot would
            box = Dm.@new<obj_item_storage_box>(get_turf(body));
            Waiting.NoSleep(body.equip_to_slot(box, ITEM_SLOT_RPOCKET));
            Dm.@new<obj_item_pen>(box);
            Waiting.NoSleep(bobBody.forceMove(get_step(body, EAST)));
        });
        w.Run(Second);
        return new Rig(w, alice, body, bob, bobBody, backpack, belt, box);
    }

    private static void Move(Rig g, obj_item item, InventoryTarget to, obj_item storage = null, int index = 0, int ticks = 10)
    {
        Assert.True(g.Alice.Session.InventoryMove(item.dm_id, to, storage?.dm_id ?? 0, index));
        g.W.Run(ticks);
    }

    private static List<long> ItemsIn(Rig g, StoragePlace place) => g.Model.At(place)?.Items.Select(i => i.Entity).ToList() ?? new List<long>();

    private static List<long> Contents(Rig g, obj_item storage) => In(g.W, () => Dm.each<obj_item>(storage.atom_storage.real_location.contents).Select(i => i.dm_id).ToList());

    [Fact]
    public void Alice_receives_her_storages_with_their_contents_and_Bob_none_of_hers()
    {
        using Rig g = Make(7301);
        RealHeadlessWorld w = g.W;
        int runtimes = w.World.RuntimeErrorCount;
        IReadOnlyList<CarriedStorage> seen = g.Model.Storages;
        Assert.Equal(new[] { StoragePlace.Back, StoragePlace.Belt, StoragePlace.RightPocket }, seen.Select(s => s.Place));
        Assert.Equal(new[] { g.Backpack.dm_id, g.Belt.dm_id, g.PocketBox.dm_id }, seen.Select(s => s.Entity));
        CarriedStorage bag = seen[0];
        Assert.Equal(In(w, () => Dm.jointext(Dm.list(g.Backpack), "")), bag.Name);
        Assert.Equal(In(w, () => (int)g.Backpack.atom_storage.max_slots), bag.Slots);
        Assert.Equal(In(w, () => (int)g.Backpack.atom_storage.max_total_storage), bag.Limit);
        Assert.Equal(In(w, () => (int)Dm.num(g.Backpack.atom_storage.get_total_weight())), bag.Used);
        Assert.True(bag.Used > 0);
        Assert.Equal(Contents(g, g.Backpack), bag.Items.Select(i => i.Entity));
        Assert.Equal(Contents(g, g.Belt), ItemsIn(g, StoragePlace.Belt));
        Assert.Equal(Contents(g, g.PocketBox), ItemsIn(g, StoragePlace.RightPocket));
        foreach (CarriedStorage s in seen)
            foreach (StorageItem item in s.Items)
            {
                Assert.False(string.IsNullOrEmpty(item.Name));
                Assert.False(item.Appearance.IsNone, item.Name + " has a picture");
                Assert.True(g.Alice.Session.Replica.TryGetAppearance(item.Appearance, out _), item.Name + "'s picture is in the client's tables");
            }
        // Bob's own message lists only his own (none: he carries no storage); nothing of Alice's reaches him
        Assert.Empty(g.Bob.Session.Storages.Storages);
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void Moves_between_the_bag_the_belt_the_pocket_box_the_hands_the_slots_and_the_floor()
    {
        using Rig g = Make(7302);
        RealHeadlessWorld w = g.W;
        int runtimes = w.World.RuntimeErrorCount;
        obj_item screwdriver = In(w, () => Dm.each<obj_item>(g.Backpack.atom_storage.real_location.contents).First(i => i is obj_item_screwdriver));
        obj_item wrench = In(w, () => Dm.each<obj_item>(g.Backpack.atom_storage.real_location.contents).First(i => i is obj_item_wrench));

        Move(g, screwdriver, InventoryTarget.Storage, g.Belt); // bag to belt
        Assert.True(In(w, () => screwdriver.loc == g.Belt));
        Assert.Contains(screwdriver.dm_id, ItemsIn(g, StoragePlace.Belt));
        Assert.DoesNotContain(screwdriver.dm_id, ItemsIn(g, StoragePlace.Back));

        Move(g, screwdriver, InventoryTarget.Storage, g.PocketBox); // belt to the pocket box
        Assert.True(In(w, () => screwdriver.loc == g.PocketBox));
        Assert.Contains(screwdriver.dm_id, ItemsIn(g, StoragePlace.RightPocket));

        Move(g, wrench, InventoryTarget.Hand, index: 2); // bag to hand
        Assert.True(In(w, () => g.Body.get_item_for_held_index(2) == wrench));
        Assert.Empty(ItemsIn(g, StoragePlace.Back));

        Move(g, wrench, InventoryTarget.Storage, g.Backpack, 1); // hand to bag, first cell
        Assert.True(In(w, () => wrench.loc == g.Backpack));
        Assert.Equal(new[] { wrench.dm_id }, ItemsIn(g, StoragePlace.Back));

        Move(g, wrench, InventoryTarget.Floor); // bag to the floor
        Assert.True(In(w, () => wrench.loc == get_turf(g.Body)));
        Assert.Empty(ItemsIn(g, StoragePlace.Back));

        obj_item shoes = In(w, () =>
        {
            var s = Dm.@new<obj_item_clothing_shoes_sneakers_black>(get_turf(g.Body));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Body.equip_to_slot_if_possible(s, ITEM_SLOT_FEET))));
            return (obj_item)s;
        });
        w.Run(2);
        Move(g, shoes, InventoryTarget.Storage, g.Backpack); // worn shoes into the bag
        Assert.True(In(w, () => shoes.loc == g.Backpack && g.Body.shoes == null));
        Assert.Contains(shoes.dm_id, ItemsIn(g, StoragePlace.Back));

        obj_item jumpsuit = In(w, () =>
        {
            obj_item old = g.Body.w_uniform;
            qdel(old); // the belt, the ID and the pockets come off with it, as in tg
            var s = Dm.@new<obj_item_clothing_under_color_grey>(get_turf(g.Body));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Body.put_in_hand(s, 1))));
            return (obj_item)s;
        });
        w.Run(2);
        Move(g, jumpsuit, InventoryTarget.Slot, index: ITEM_SLOT_ICLOTHING); // a held jumpsuit onto the uniform slot
        Assert.True(In(w, () => g.Body.w_uniform == jumpsuit && g.Body.get_item_for_held_index(1) == null));
        Assert.Equal(0L, g.Model.RefusedItem);
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void A_refused_move_leaves_everything_in_place_and_the_client_is_told()
    {
        using Rig g = Make(7303);
        RealHeadlessWorld w = g.W;
        obj_item toolbox = In(w, () =>
        {
            var t = Dm.@new<obj_item_storage_toolbox_mechanical>(get_turf(g.Body));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Body.put_in_hand(t, 1))));
            return (obj_item)t;
        });
        w.Run(2);
        List<long> before = Contents(g, g.Backpack);
        Move(g, toolbox, InventoryTarget.Storage, g.Backpack); // bulky into a backpack: tg's "too big!"
        Assert.True(In(w, () => g.Body.get_item_for_held_index(1) == toolbox));
        Assert.Equal(before, Contents(g, g.Backpack));
        Assert.Equal(toolbox.dm_id, g.Model.RefusedItem);
        Assert.True(g.Model.RefusedAt > double.NegativeInfinity);
        Assert.Equal(1, In(w, () => g.Body.dm_carried.Refusals));
        // a held toolbox is itself a storage: it is listed after the worn ones
        Assert.Equal(StoragePlace.Hand, g.Model.Storages[^1].Place);
        Assert.Equal(toolbox.dm_id, g.Model.Storages[^1].Entity);
    }

    [Fact]
    public void Bob_hears_the_rustle_of_Alices_Tab_once_a_second_and_never_receives_her_storages()
    {
        using Rig g = Make(7304);
        RealHeadlessWorld w = g.W;
        int rustles = 0, bobStorageMessages = 0;
        var heardPaths = new List<string>();
        ReplicaWorld bobReplica = g.Bob.Session.Replica;
        bobReplica.SoundsReceived += events =>
        {
            foreach (SoundEvent e in events)
            {
                string path = e.File ?? bobReplica.SoundPath(e.Path);
                heardPaths.Add(path);
                if (path != null && path.Contains("rustle", StringComparison.Ordinal))
                    rustles++;
            }
        };
        StoragesMessage bobLast = bobReplica.Storages;
        void CountBob()
        {
            if (!ReferenceEquals(bobLast, bobReplica.Storages))
            {
                bobStorageMessages++;
                bobLast = bobReplica.Storages;
                foreach (CarriedStorage s in bobLast.Storages)
                    Assert.NotEqual(g.Backpack.dm_id, s.Entity);
            }
        }
        for (int i = 0; i < 10; i++)
        {
            g.Alice.Session.InventoryOpened();
            w.Run(1, _ => CountBob());
        }
        w.Run(4, _ => CountBob());
        Assert.True(In(w, () => g.Body.dm_carried != null), "the notice reached the server");
        Assert.True(1 == rustles, "heard: " + string.Join(", ", heardPaths));
        w.Run(Second, _ => CountBob());
        g.Alice.Session.InventoryOpened();
        w.Run(6, _ => CountBob());
        Assert.Equal(2, rustles);
        // Alice's moves change her message, never Bob's
        obj_item screwdriver = In(w, () => Dm.each<obj_item>(g.Backpack.atom_storage.real_location.contents).First());
        Assert.True(g.Alice.Session.InventoryMove(screwdriver.dm_id, InventoryTarget.Storage, g.Belt.dm_id));
        w.Run(10, _ => CountBob());
        Assert.True(In(w, () => screwdriver.loc == g.Belt));
        Assert.Equal(0, bobStorageMessages);
        Assert.Empty(g.Bob.Session.Storages.Storages);
    }

    [Fact]
    public void Alices_message_changes_when_Bob_takes_an_item_out_of_her_bag_or_pocket()
    {
        using Rig g = Make(7305);
        RealHeadlessWorld w = g.W;
        obj_item pen = In(w, () => Dm.each<obj_item>(g.PocketBox.atom_storage.real_location.contents).First());
        obj_item wrench = In(w, () => Dm.each<obj_item>(g.Backpack.atom_storage.real_location.contents).First(i => i is obj_item_wrench));
        Assert.Contains(pen.dm_id, ItemsIn(g, StoragePlace.RightPocket));
        // Bob takes the pocket box itself (tg's strip of a pocket: strippable.dm finish_unequip puts it in the stripper's hands)
        In(w, () =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Body.temporarilyRemoveItemFromInventory(g.PocketBox, force: TRUE))));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.BobBody.put_in_hands(g.PocketBox))));
        });
        w.Run(4);
        Assert.Null(g.Model.At(StoragePlace.RightPocket));
        Assert.DoesNotContain(g.PocketBox.dm_id, g.Model.Storages.Select(s => s.Entity));
        // Bob reaches into her bag (the bag's storage handing the wrench to him)
        In(w, () =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Backpack.atom_storage.remove_single(g.BobBody, wrench, g.BobBody))));
            Waiting.NoSleep(g.BobBody.put_in_hands(wrench));
        });
        w.Run(4);
        Assert.DoesNotContain(wrench.dm_id, ItemsIn(g, StoragePlace.Back));
        Assert.Equal(Contents(g, g.Backpack), ItemsIn(g, StoragePlace.Back));
        // and Bob's own message now lists the box he holds
        Assert.Equal(g.PocketBox.dm_id, g.Bob.Session.Storages.At(StoragePlace.Hand, In(w, () => (int)Dm.num(g.BobBody.get_held_index_of_item(g.PocketBox))))?.Entity);
    }

    [Fact]
    public void The_message_is_not_rebuilt_while_nothing_changes_and_a_full_backpack_is_measured()
    {
        using Rig g = Make(7306);
        RealHeadlessWorld w = g.W;
        int session = g.W.Host.LiveSession("Alice").Ordinal;
        int builds = w.Replicator.StoragesBuilds(session);
        w.Run(2 * Second);
        Assert.Equal(builds, w.Replicator.StoragesBuilds(session)); // forty ticks of nothing: no build
        // fill the backpack to tg's limit with small items: one build per change, and the message's size
        In(w, () =>
        {
            for (int i = 0; i < 21 && Dm.truthy(g.Backpack.atom_storage.can_insert(Dm.@new<obj_item_pen>(get_turf(g.Body)), null, messages: FALSE)); i++)
            {
                obj_item pen = Dm.locate<obj_item_pen>(get_turf(g.Body));
                g.Backpack.atom_storage.attempt_insert(pen, null, messages: FALSE);
            }
        });
        w.Run(4);
        CarriedStorage bag = g.Model.At(StoragePlace.Back);
        Assert.Equal(Contents(g, g.Backpack), bag.Items.Select(i => i.Entity));
        int bytes = Codec.Encode(new StoragesMessage { Storages = g.Model.Storages }).Length;
        Assert.True(bytes < 4096, "a full backpack's message: " + bytes + " bytes");
        _output.WriteLine("A full backpack (" + bag.Items.Count + " items) with the belt and the pocket box: " + bytes + " bytes");
        int after = w.Replicator.StoragesBuilds(session);
        w.Run(Second);
        Assert.Equal(after, w.Replicator.StoragesBuilds(session));
    }
}
