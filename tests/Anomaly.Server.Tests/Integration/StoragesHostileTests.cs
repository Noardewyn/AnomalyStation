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
/// ui-overhaul.md section 12, slice 4B-1 (DV696), security: hostile raw clients (no game client, bytes as they like) send inventory moves for another
/// player's item, into another player's bag, for an item on the floor out of reach, for things that are no items, with every field at its extremes, a
/// thousand in one tick and while dead. Nothing changes, no Storages message names anything of another player's, nothing is refused aloud for a
/// request that is not the sender's to make, and the server does not throw.
/// </summary>
public class StoragesHostileTests
{
    private sealed record Kit(mob_living_carbon_human Body, obj_item Backpack, obj_item Tool);

    private static Kit Dress(IntegrationRig rig, string key)
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

    // Every entity any Storages message to this client named.
    private static HashSet<long> Named(RawClient client)
    {
        var named = new HashSet<long>();
        foreach ((Channel _, byte[] message) in client.Received)
        {
            if (message.Length == 0 || message[0] != (byte)MessageId.Storages)
                continue;
            Assert.True(Codec.TryDecode(message, out StoragesMessage storages, out ProtocolError error), error.ToString());
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

    [Fact]
    public void Moves_of_what_is_not_the_senders_change_nothing_and_reveal_nothing()
    {
        using var rig = new IntegrationRig(network: new LoopbackOptions { Seed = 3, TimeoutSteps = 4000 });
        var mallory = new RawClient(rig, "Mallory");
        var victim = new RawClient(rig, "Victim");
        rig.Tick(20);
        Assert.NotNull(mallory.Welcome);
        Assert.NotNull(victim.Welcome);
        Kit m = Dress(rig, "Mallory"), v = Dress(rig, "Victim");
        obj_item far;
        long turfId, victimMob;
        using (rig.World.Enter())
        {
            far = Dm.@new<obj_item_wrench>(locate(IntegrationRig.RoomSize - 2, IntegrationRig.RoomSize - 2, 1)); // a floor item out of reach
            turfId = ((turf)get_turf(m.Body)).dm_id;
            victimMob = v.Body.dm_id;
        }
        rig.Tick(10);
        string victimTool = Where(rig, v.Tool), victimPack = Where(rig, v.Backpack), farAt = Where(rig, far);
        int runtimes = rig.World.RuntimeErrorCount;

        Send(mallory, Move(v.Tool.dm_id, InventoryTarget.Hand, index: 1)); // the victim's item into her hand
        Send(mallory, Move(v.Tool.dm_id, InventoryTarget.Storage, m.Backpack.dm_id)); // into her bag
        Send(mallory, Move(v.Backpack.dm_id, InventoryTarget.Floor)); // the victim's worn bag onto the floor
        Send(mallory, Move(m.Tool.dm_id, InventoryTarget.Storage, v.Backpack.dm_id)); // her own item into the victim's bag
        Send(mallory, Move(far.dm_id, InventoryTarget.Hand, index: 1)); // a floor item out of reach
        Send(mallory, Move(far.dm_id, InventoryTarget.Storage, m.Backpack.dm_id));
        Send(mallory, Move(victimMob, InventoryTarget.Storage, m.Backpack.dm_id)); // a mob is no item
        Send(mallory, Move(m.Body.dm_id, InventoryTarget.Floor)); // nor is she
        Send(mallory, Move(turfId, InventoryTarget.Hand, index: 1)); // a turf
        Send(mallory, Move(m.Backpack.dm_id, InventoryTarget.Storage, m.Backpack.dm_id)); // the bag into itself
        // every field at its extremes, as the codec lets them through
        Send(mallory, Move(long.MaxValue, InventoryTarget.Storage, long.MaxValue, Limits.MaxStorageItems));
        Send(mallory, Move(m.Tool.dm_id, InventoryTarget.Storage, long.MaxValue, Limits.MaxStorageItems));
        Send(mallory, Move(m.Tool.dm_id, InventoryTarget.Hand, index: Limits.MaxStorageHands)); // a hand she does not have
        Send(mallory, Move(m.Tool.dm_id, InventoryTarget.Slot, index: 0x10000)); // valid: her right pocket
        Send(mallory, Move(1, InventoryTarget.Slot, index: 1));
        rig.Tick(10);
        mallory.Transport.Poll();

        Assert.Equal(victimTool, Where(rig, v.Tool));
        Assert.Equal(victimPack, Where(rig, v.Backpack));
        Assert.Equal(farAt, Where(rig, far));
        // her own item moved only by her own valid request (the pocket), never into the victim's bag
        Assert.NotEqual(v.Backpack.dm_id.ToString(System.Globalization.CultureInfo.InvariantCulture), Where(rig, m.Tool));
        HashSet<long> named = Named(mallory);
        Assert.DoesNotContain(v.Tool.dm_id, named);
        Assert.DoesNotContain(v.Backpack.dm_id, named);
        Assert.DoesNotContain(far.dm_id, named);
        Assert.DoesNotContain(victimMob, named);
        Assert.Contains(m.Backpack.dm_id, named); // her own message is there
        // the victim was told nothing of Mallory's either
        HashSet<long> victimNamed = Named(victim);
        Assert.DoesNotContain(m.Backpack.dm_id, victimNamed);
        Assert.DoesNotContain(m.Tool.dm_id, victimNamed);
        // requests that were not hers to make are not refused aloud (nothing tells her whether such an item exists); the bag into itself is hers
        Assert.Equal(1, Refusals(rig, m)); // the bag into itself: tg's can_insert refuses
        Assert.Equal(0, Refusals(rig, v));
        Assert.Equal(runtimes, rig.World.RuntimeErrorCount);
        Assert.True(string.IsNullOrEmpty(rig.Runtimes), rig.Runtimes);
    }

    // tg's varuint (7 bits a byte, the high bit set on all but the last), as the wire writes an entity id
    private static byte[] VarUInt(long value)
    {
        var bytes = new List<byte>();
        ulong u = (ulong)value;
        do
        {
            byte b = (byte)(u & 0x7F);
            u >>= 7;
            if (u != 0)
                b |= 0x80;
            bytes.Add(b);
        }
        while (u != 0);
        return bytes.ToArray();
    }

    [Fact]
    public void Malformed_moves_are_refused_by_the_codec_and_the_server_keeps_running()
    {
        using var rig = new IntegrationRig(network: new LoopbackOptions { Seed = 4, TimeoutSteps = 4000 });
        // each malformed shape from a client of its own, naming that client's real tool and backpack, so that only the codec stands between the
        // bytes and a move; the well-formed control moves the tool
        var shapes = new (string Why, Func<Kit, byte[]> Bytes)[]
        {
            ("a cell of 65", k => new byte[] { 10, 0, 29 }.Concat(VarUInt(k.Tool.dm_id)).Concat(new byte[] { 1 }).Concat(VarUInt(k.Backpack.dm_id)).Concat(new byte[] { 65 }).ToArray()),
            ("a hand of 9", k => new byte[] { 10, 0, 29 }.Concat(VarUInt(k.Tool.dm_id)).Concat(new byte[] { 3, 9 }).ToArray()),
            ("two slots at once", k => new byte[] { 10, 0, 29 }.Concat(VarUInt(k.Tool.dm_id)).Concat(new byte[] { 2, 3 }).ToArray()),
            ("an unknown target", k => new byte[] { 10, 0, 29 }.Concat(VarUInt(k.Tool.dm_id)).Concat(new byte[] { 9 }).ToArray()),
            ("a trailing byte", k => new byte[] { 10, 0, 29 }.Concat(VarUInt(k.Tool.dm_id)).Concat(new byte[] { 4, 0 }).ToArray()),
            ("an item of 0", k => new byte[] { 10, 0, 29, 0, 4 }),
            ("a Tab notice with a field", k => new byte[] { 10, 0, 30, 1 }),
        };
        var clients = new List<(RawClient Client, string Key)>();
        for (int i = 0; i <= shapes.Length; i++)
            clients.Add((new RawClient(rig, "M" + i), "M" + i));
        rig.Tick(20);
        var kits = clients.Select(c => Dress(rig, c.Key)).ToList();
        rig.Tick(4);
        for (int i = 0; i < shapes.Length; i++)
            clients[i].Client.Transport.Send(Channel.Control, shapes[i].Bytes(kits[i]));
        Kit control = kits[^1];
        clients[^1].Client.Transport.Send(Channel.Control, new byte[] { 10, 0, 29 }.Concat(VarUInt(control.Tool.dm_id)).Concat(new byte[] { 4 }).ToArray());
        rig.Tick(10);
        for (int i = 0; i < shapes.Length; i++)
        {
            Session s = rig.Host.Sessions.Single(x => x.Key == clients[i].Key);
            Assert.True(s.Strikes == 1, shapes[i].Why + ": one strike, not " + s.Strikes);
            using (rig.World.Enter())
                Assert.True(kits[i].Tool.loc == kits[i].Backpack, shapes[i].Why + ": nothing moved");
        }
        Assert.Equal(0, rig.Host.Sessions.Single(x => x.Key == clients[^1].Key).Strikes);
        using (rig.World.Enter())
            Assert.True(control.Tool.loc is turf, "the well-formed move went through");
        Assert.False(rig.Host.Finished);
        Assert.True(string.IsNullOrEmpty(rig.Runtimes), rig.Runtimes);
    }

    [Fact]
    public void A_thousand_moves_in_a_tick_duplicate_and_lose_nothing()
    {
        using var rig = new IntegrationRig(network: new LoopbackOptions { Seed = 5, TimeoutSteps = 4000 });
        var mallory = new RawClient(rig, "Mallory");
        rig.Tick(20);
        Kit m = Dress(rig, "Mallory");
        obj_item second;
        using (rig.World.Enter())
            second = Dm.@new<obj_item_wrench>(m.Backpack);
        rig.Tick(4);
        int runtimes = rig.World.RuntimeErrorCount;
        for (int i = 0; i < 1000; i++)
        {
            obj_item item = i % 2 == 0 ? m.Tool : second;
            Send(mallory, (i % 4) switch
            {
                0 => Move(item.dm_id, InventoryTarget.Hand, index: 1 + i % 2),
                1 => Move(item.dm_id, InventoryTarget.Storage, m.Backpack.dm_id, i % 3),
                2 => Move(item.dm_id, InventoryTarget.Slot, index: ITEM_SLOT_LPOCKET),
                _ => Move(item.dm_id, InventoryTarget.Floor),
            });
        }
        rig.Tick(40);
        using (rig.World.Enter())
        {
            foreach (obj_item item in new[] { m.Tool, second })
            {
                Assert.False(QDELETED(item));
                int places = 0;
                for (int h = 1; h <= 2; h++) places += m.Body.get_item_for_held_index(h) == item ? 1 : 0;
                places += m.Body.l_store == item ? 1 : 0;
                places += item.loc == m.Backpack ? 1 : 0;
                places += item.loc is turf ? 1 : 0;
                Assert.Equal(1, places);
            }
            Assert.Equal(2, Dm.each<obj_item>(Dm.list(m.Tool, second)).Count(i => !QDELETED(i)));
        }
        Assert.Equal(runtimes, rig.World.RuntimeErrorCount);
        Assert.False(rig.Host.Finished);
        Assert.True(string.IsNullOrEmpty(rig.Runtimes), rig.Runtimes);
    }

    [Fact]
    public void A_dead_body_moves_nothing()
    {
        using var rig = new IntegrationRig(network: new LoopbackOptions { Seed = 6, TimeoutSteps = 4000 });
        var mallory = new RawClient(rig, "Mallory");
        rig.Tick(20);
        Kit m = Dress(rig, "Mallory");
        obj_item second;
        using (rig.World.Enter())
        {
            second = Dm.@new<obj_item_wrench>(m.Backpack);
            Waiting.Detach(m.Body.death());
        }
        rig.Tick(4);
        int runtimes = rig.World.RuntimeErrorCount;
        int refusedBefore = Refusals(rig, m);
        Send(mallory, Move(m.Tool.dm_id, InventoryTarget.Hand, index: 1));
        Send(mallory, Move(second.dm_id, InventoryTarget.Floor));
        rig.Tick(10);
        using (rig.World.Enter())
            Assert.True(m.Tool.loc == m.Backpack && second.loc == m.Backpack);
        Assert.Equal(refusedBefore + 2, Refusals(rig, m));
        Assert.Equal(runtimes, rig.World.RuntimeErrorCount);
        Assert.True(string.IsNullOrEmpty(rig.Runtimes), rig.Runtimes);
    }
}
