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
/// ui-overhaul.md section 12, slice 4C-1 (DV696), security: hostile raw clients name items of containers that are not their open other container: a box
/// beside the sender she has not opened, one another player has open, her own open box after she was moved out of reach in the same tick (still listed, as tg's window shows it, until her next step), a locked
/// box, a move into a box not opened, every field at its extremes, and a thousand moves in a tick on a box she has open. Nothing changes that was not
/// hers to change, no Storages message names an entity of a container its receiver has not open, nothing is refused aloud for a request that was not
/// hers to make, and the server does not throw.
/// </summary>
public class StoragesOtherHostileTests
{
    private sealed record Kit(mob_living_carbon_human Body, obj_item Backpack, obj_item Tool);

    private static Kit Dress(IntegrationRig rig, string key, int x, int y)
    {
        using (rig.World.Enter())
        {
            var body = (mob_living_carbon_human)rig.MobOf(key);
            foreach (obj_item item in new[] { body.back, body.belt, body.l_store, body.r_store, body.wear_id,
                body.get_item_for_held_index(1) as obj_item, body.get_item_for_held_index(2) as obj_item })
                if (item != null)
                    qdel(item);
            if (body.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(body), ITEM_SLOT_ICLOTHING))));
            Waiting.NoSleep(body.forceMove(locate(x, y, 1)));
            var backpack = Dm.@new<obj_item_storage_backpack>(get_turf(body));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(backpack, ITEM_SLOT_BACK))));
            var tool = Dm.@new<obj_item_screwdriver>(backpack);
            return new Kit(body, backpack, tool);
        }
    }

    private static void Send(RawClient client, ActionMessage action) => client.Transport.Send(Channel.Control, Codec.Encode(action));

    private static ActionMessage Move(long item, InventoryTarget to, long storage = 0, int index = 0)
        => new(0, ActionKind.InventoryMove, 0, ClickButton.None, ClickModifiers.None, null) { MoveItem = item, MoveTo = to, MoveStorage = storage, MoveIndex = index };

    private static string Where(IntegrationRig rig, obj_item item)
    {
        using (rig.World.Enter())
            return QDELETED(item) ? "deleted" : item.loc is datum d ? d.dm_id.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null";
    }

    // The Storages messages this client received, decoded.
    private static List<StoragesMessage> Received(RawClient client)
    {
        client.Transport.Poll();
        var all = new List<StoragesMessage>();
        foreach ((Channel _, byte[] message) in client.Received)
        {
            if (message.Length == 0 || message[0] != (byte)MessageId.Storages)
                continue;
            Assert.True(Codec.TryDecode(message, out StoragesMessage storages, out ProtocolError error), error.ToString());
            all.Add(storages);
        }
        return all;
    }

    // Every entity any Storages message to this client named.
    private static HashSet<long> Named(RawClient client)
    {
        var named = new HashSet<long>();
        foreach (StoragesMessage storages in Received(client))
        {
            foreach (CarriedStorage s in storages.Storages)
            {
                named.Add(s.Entity);
                foreach (StorageItem item in s.Items)
                    named.Add(item.Entity);
            }
            if (storages.RefusedItem != 0)
                named.Add(storages.RefusedItem);
        }
        return named;
    }

    private static int Refusals(IntegrationRig rig, Kit kit)
    {
        using (rig.World.Enter())
            return kit.Body.dm_carried?.Refusals ?? 0;
    }

    private static obj_item Box(IntegrationRig rig, int x, int y, out obj_item inside)
    {
        using (rig.World.Enter())
        {
            var box = Dm.@new<obj_item_storage_box>(locate(x, y, 1));
            inside = Dm.@new<obj_item_pen>(box);
            return box;
        }
    }

    // How many examine boxes naming a pen reach the client after a Shift+click on the item as a screen target (tg's window entry), then a second
    private static int Examined(IntegrationRig rig, RawClient client, obj_item item)
    {
        static bool Box(string t) => t.Contains("boxed_message", StringComparison.Ordinal) && t.Contains("pen", StringComparison.OrdinalIgnoreCase);
        int before = client.Texts.Count(Box);
        Send(client, new ActionMessage(0, ActionKind.Click, item.dm_id, ClickButton.Left, ClickModifiers.Shift, null) { TargetKind = TargetKind.Screen, IconX = 16, IconY = 16 });
        rig.Tick(25);
        return client.Texts.Count(Box) - before;
    }

    private static void Open(IntegrationRig rig, Kit who, obj_item storage)
    {
        using (rig.World.Enter())
            Waiting.NoSleep(storage.atom_storage.open_storage(who.Body));
    }

    [Fact]
    public void Moves_naming_a_container_the_sender_has_not_open_change_nothing_and_reveal_nothing()
    {
        using var rig = new IntegrationRig(network: new LoopbackOptions { Seed = 41, TimeoutSteps = 4000 });
        var mallory = new RawClient(rig, "Mallory");
        var victim = new RawClient(rig, "Victim");
        rig.Tick(20);
        Kit m = Dress(rig, "Mallory", 8, 8), v = Dress(rig, "Victim", 10, 8);
        // beside her and not opened; beside both and opened by the victim; a locked box beside her; her own box, opened, then out of reach
        obj_item closed = Box(rig, 8, 9, out obj_item closedPen), theirs = Box(rig, 9, 8, out obj_item theirPen), mine = Box(rig, 7, 8, out obj_item myPen);
        obj_item lockbox, lockedPen;
        using (rig.World.Enter())
        {
            lockbox = Dm.@new<obj_item_storage_lockbox>(locate(8, 7, 1));
            lockedPen = Dm.@new<obj_item_pen>(lockbox);
            Assert.True(Dm.truthy(lockbox.atom_storage.locked));
        }
        Open(rig, v, theirs);
        Open(rig, m, lockbox); // tg's "closed!": not opened
        rig.Tick(4);
        using (rig.World.Enter())
        {
            Assert.Same(theirs.atom_storage, v.Body.active_storage);
            Assert.Null(m.Body.active_storage);
        }
        int runtimes = rig.World.RuntimeErrorCount;
        var before = new[] { closedPen, theirPen, lockedPen, m.Tool }.ToDictionary(i => i, i => Where(rig, i));
        foreach ((obj_item pen, obj_item box) in new[] { (closedPen, closed), (theirPen, theirs), (lockedPen, lockbox) })
        {
            Send(mallory, Move(pen.dm_id, InventoryTarget.Hand, index: 1));
            Send(mallory, Move(pen.dm_id, InventoryTarget.Storage, m.Backpack.dm_id));
            Send(mallory, Move(pen.dm_id, InventoryTarget.Floor));
            Send(mallory, Move(m.Tool.dm_id, InventoryTarget.Storage, box.dm_id)); // her own item into it
            Send(mallory, Move(m.Tool.dm_id, InventoryTarget.Storage, box.dm_id, Limits.MaxStorageItems));
            Send(mallory, Move(pen.dm_id, InventoryTarget.Storage, box.dm_id, 1)); // a reorder within it
            Send(mallory, Move(box.dm_id, InventoryTarget.Hand, index: 1)); // the box itself, an item on the floor
        }
        rig.Tick(10);
        foreach ((obj_item item, string at) in before)
            Assert.True(at == Where(rig, item), item.name + " moved");
        Assert.Equal(0, Refusals(rig, m));
        // examining an item of a box she has not open by tg's state (closed beside her, the victim's open one, a locked one): refused as before
        foreach (obj_item pen in new[] { closedPen, theirPen, lockedPen })
            Assert.True(Examined(rig, mallory, pen) == 0, "examined " + pen.dm_id);

        // her own box, opened, then she is put out of its reach and names its pen in the same tick (tg's own state keeps it open: forceMove is no Move)
        Open(rig, m, mine);
        rig.Tick(4);
        Assert.Contains(myPen.dm_id, Named(mallory)); // hers while it was open
        Assert.Equal(1, Examined(rig, mallory, myPen)); // and hers to examine
        using (rig.World.Enter())
        {
            Waiting.NoSleep(m.Body.forceMove(locate(4, 8, 1)));
            Assert.Same(mine.atom_storage, m.Body.active_storage);
        }
        Send(mallory, Move(myPen.dm_id, InventoryTarget.Hand, index: 1));
        Send(mallory, Move(m.Tool.dm_id, InventoryTarget.Storage, mine.dm_id));
        rig.Tick(10);
        Assert.Equal(mine.dm_id.ToString(System.Globalization.CultureInfo.InvariantCulture), Where(rig, myPen));
        // out of it: tg's reach, refused; into it: tg's window click asks no reach, so her own screwdriver goes in
        Assert.Equal(mine.dm_id.ToString(System.Globalization.CultureInfo.InvariantCulture), Where(rig, m.Tool));
        Assert.Equal(0, Refusals(rig, m));
        // still listed, as tg's window still shows it (tg's state); her next step closes it (tg's living/Move)
        Assert.Equal(mine.dm_id, Received(mallory)[^1].Storages.LastOrDefault(s => s.Place == StoragePlace.Other)?.Entity);
        using (rig.World.Enter())
            Waiting.Detach(m.Body.Move(locate(3, 8, 1), WEST));
        rig.Tick(4);
        using (rig.World.Enter())
            Assert.Null(m.Body.active_storage);
        Assert.DoesNotContain(Received(mallory)[^1].Storages, s => s.Place == StoragePlace.Other);
        Assert.Equal(0, Examined(rig, mallory, myPen)); // tg closed it: its items are no longer hers to examine

        HashSet<long> named = Named(mallory);
        foreach (long never in new[] { closed.dm_id, closedPen.dm_id, theirs.dm_id, theirPen.dm_id, lockbox.dm_id, lockedPen.dm_id, v.Backpack.dm_id, v.Tool.dm_id })
            Assert.DoesNotContain(never, named);
        HashSet<long> victimNamed = Named(victim);
        Assert.Contains(theirPen.dm_id, victimNamed); // the victim's own open box is his
        foreach (long never in new[] { mine.dm_id, myPen.dm_id, closed.dm_id, m.Backpack.dm_id })
            Assert.DoesNotContain(never, victimNamed);
        Assert.Equal(runtimes, rig.World.RuntimeErrorCount);
        Assert.True(string.IsNullOrEmpty(rig.Runtimes), rig.Runtimes);
    }

    [Fact]
    public void A_thousand_moves_in_a_tick_on_her_open_box_duplicate_and_lose_nothing()
    {
        using var rig = new IntegrationRig(network: new LoopbackOptions { Seed = 42, TimeoutSteps = 4000 });
        var mallory = new RawClient(rig, "Mallory");
        var bob = new RawClient(rig, "Bob");
        rig.Tick(20);
        Kit m = Dress(rig, "Mallory", 8, 8), b = Dress(rig, "Bob", 9, 9);
        obj_item box = Box(rig, 9, 8, out obj_item pen);
        obj_item wrench;
        using (rig.World.Enter())
            wrench = Dm.@new<obj_item_wrench>(box);
        Open(rig, m, box);
        Open(rig, b, box); // Bob has it open too and floods the same items
        rig.Tick(4);
        int runtimes = rig.World.RuntimeErrorCount;
        obj_item[] items = { pen, wrench, m.Tool };
        for (int i = 0; i < 1000; i++)
        {
            obj_item item = items[i % 3];
            RawClient who = i % 5 == 4 ? bob : mallory;
            Kit kit = who == bob ? b : m;
            Send(who, (i % 4) switch
            {
                0 => Move(item.dm_id, InventoryTarget.Hand, index: 1 + i % 2),
                1 => Move(item.dm_id, InventoryTarget.Storage, box.dm_id, i % 3),
                2 => Move(item.dm_id, InventoryTarget.Storage, kit.Backpack.dm_id),
                _ => Move(item.dm_id, InventoryTarget.Floor),
            });
        }
        rig.Tick(40);
        using (rig.World.Enter())
        {
            foreach (obj_item item in items)
            {
                Assert.False(QDELETED(item));
                int places = 0;
                foreach (mob_living_carbon_human body in new[] { m.Body, b.Body })
                {
                    for (int h = 1; h <= 2; h++)
                        places += body.get_item_for_held_index(h) == item ? 1 : 0;
                    places += item.loc == body.back ? 1 : 0;
                }
                places += item.loc == box ? 1 : 0;
                places += item.loc is turf ? 1 : 0;
                Assert.True(places == 1, item.name + " is in " + places + " places");
            }
        }
        // nothing of Bob's reached Mallory, nor of hers Bob
        Assert.DoesNotContain(b.Backpack.dm_id, Named(mallory));
        Assert.DoesNotContain(m.Backpack.dm_id, Named(bob));
        Assert.Equal(runtimes, rig.World.RuntimeErrorCount);
        Assert.False(rig.Host.Finished);
        Assert.True(string.IsNullOrEmpty(rig.Runtimes), rig.Runtimes);
    }
}
