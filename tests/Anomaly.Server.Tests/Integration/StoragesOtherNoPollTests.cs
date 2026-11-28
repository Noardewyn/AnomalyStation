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
/// ui-overhaul.md section 12, slice 4C-1 (DV696): nothing asks tg's reach for an open container while its player stands still, through a real server
/// (its command phase and its replicator run every tick). tg's reach test is not free of effects: <c>Adjacent</c> asks <c>CanPass</c> of what lies on
/// the turfs, and a prison gate answers by speaking and starting its cooldown; a reach over one tile walks a probe object a proximity sensor senses.
/// tg asks it only on events (a step, a click). With a box open for 200 still ticks the gate and the sensor do what they do with it closed (nothing);
/// a move, a request as tg's click is one, asks tg's reach and the gate answers it.
/// </summary>
public class StoragesOtherNoPollTests
{
    private const int Second = 20;

    private static mob_living_carbon_human Dress(IntegrationRig rig, string key, int x, int y)
    {
        using (rig.World.Enter())
        {
            var body = (mob_living_carbon_human)rig.MobOf(key);
            foreach (obj_item item in new[] { body.back, body.belt, body.get_item_for_held_index(1) as obj_item, body.get_item_for_held_index(2) as obj_item })
                if (item != null)
                    qdel(item);
            if (body.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(body), ITEM_SLOT_ICLOTHING))));
            Waiting.NoSleep(body.forceMove(locate(x, y, 1)));
            return body;
        }
    }

    private static T In<T>(IntegrationRig rig, Func<T> f)
    {
        using (rig.World.Enter())
            return f();
    }

    private static ActionMessage Move(long item, InventoryTarget to, long storage = 0, int index = 0)
        => new(0, ActionKind.InventoryMove, 0, ClickButton.None, ClickModifiers.None, null) { MoveItem = item, MoveTo = to, MoveStorage = storage, MoveIndex = index };

    // tg's Shift+click on an item, named as a screen target (as on tg's storage window), then the ticks for its examine; how many examine boxes naming
    // the item came
    private static int Examine(IntegrationRig rig, RawClient client, obj_item item, int times)
    {
        int before = client.Texts.Count(t => t.Contains("boxed_message", StringComparison.Ordinal) && t.Contains(item.name, StringComparison.OrdinalIgnoreCase));
        for (int i = 0; i < times; i++)
        {
            client.Transport.Send(Channel.Control, Codec.Encode(new ActionMessage(0, ActionKind.Click, item.dm_id, ClickButton.Left, ClickModifiers.Shift, null)
            {
                TargetKind = TargetKind.Screen, IconX = 16, IconY = 16,
            }));
            rig.Tick(25); // past tg's window for examining closer (EXAMINE_MORE_WINDOW)
        }
        return client.Texts.Count(t => t.Contains("boxed_message", StringComparison.Ordinal) && t.Contains(item.name, StringComparison.OrdinalIgnoreCase)) - before;
    }

    private static long ListedOther(RawClient client)
    {
        client.Transport.Poll();
        StoragesMessage last = null;
        foreach ((Channel _, byte[] message) in client.Received)
            if (message.Length > 0 && message[0] == (byte)MessageId.Storages && Codec.TryDecode(message, out StoragesMessage m, out _))
                last = m;
        return last?.Storages.LastOrDefault(s => s.Place == StoragePlace.Other)?.Entity ?? 0;
    }

    [Fact]
    public void A_prison_gate_under_an_open_box_says_nothing_while_its_opener_stands_still()
    {
        using var rig = new IntegrationRig(network: new LoopbackOptions { Seed = 44, TimeoutSteps = 4000 });
        var officer = new RawClient(rig, "Officer");
        rig.Tick(20);
        mob_living_carbon_human body = Dress(rig, "Officer", 8, 8);
        obj_machinery_prisongate gate;
        obj_item box, pen;
        using (rig.World.Enter())
        {
            gate = Dm.@new<obj_machinery_prisongate>(locate(9, 8, 1));
            gate.gate_active = TRUE; // as on a powered brig's
            box = Dm.@new<obj_item_storage_box>(locate(9, 8, 1));
            pen = Dm.@new<obj_item_pen>(box);
        }
        rig.Tick(4 * Second);
        int runtimes = rig.World.RuntimeErrorCount;
        (float Cooldown, int Lines) Still(int ticks)
        {
            float before = In(rig, () => gate.spam_cooldown_time);
            int lines = officer.Texts.Count;
            rig.Tick(ticks);
            Assert.Equal(before, In(rig, () => gate.spam_cooldown_time));
            return (before, officer.Texts.Count - lines);
        }
        (float _, int closedLines) = Still(200);
        // tg's opening asks the reach once (can_perform_action), so the gate may answer it; then 200 still ticks with the box open and listed
        using (rig.World.Enter())
            Waiting.NoSleep(box.atom_storage.open_storage(body));
        rig.Tick(4 * Second); // past the gate's 3 s cooldown
        Assert.Same(box.atom_storage, In(rig, () => body.active_storage));
        Assert.Equal(box.dm_id, ListedOther(officer));
        (float cooldown, int openLines) = Still(200);
        Assert.Equal(closedLines, openLines);
        // twenty examines of the pen in the open box: tg's Shift+click asks no reach, so the gate stays silent, and each prints the pen's examine
        int texts = officer.Texts.Count;
        Assert.Equal(20, Examine(rig, officer, pen, 20));
        Assert.Equal(cooldown, In(rig, () => gate.spam_cooldown_time));
        Assert.DoesNotContain(officer.Texts.Skip(texts), t => t.Contains("clearance", StringComparison.Ordinal) || t.Contains("hardlight", StringComparison.Ordinal));
        Assert.Equal(box.dm_id, ListedOther(officer));
        // a move is a request: tg's reach is asked as tg's click asks it, and the gate answers (its cooldown starts)
        int active = In(rig, () => (int)Dm.num(body.active_hand_index));
        officer.Transport.Send(Channel.Control, Codec.Encode(Move(pen.dm_id, InventoryTarget.Hand, index: active)));
        rig.Tick(4);
        Assert.True(In(rig, () => body.get_item_for_held_index(active) == pen), "the pen moved out of the box");
        Assert.NotEqual(cooldown, In(rig, () => gate.spam_cooldown_time));
        Assert.Equal(runtimes, rig.World.RuntimeErrorCount);
        Assert.True(string.IsNullOrEmpty(rig.Runtimes), rig.Runtimes);
    }

    [Fact]
    public void A_proximity_sensor_between_a_long_reach_player_and_her_open_box_senses_nothing_while_she_stands_still()
    {
        using var rig = new IntegrationRig(network: new LoopbackOptions { Seed = 45, TimeoutSteps = 4000 });
        var alice = new RawClient(rig, "Alice");
        rig.Tick(20);
        mob_living_carbon_human body = Dress(rig, "Alice", 8, 8);
        obj_item box, pen, ownPen;
        obj_item_assembly_prox_sensor sensor;
        using (rig.World.Enter())
        {
            var backpack = Dm.@new<obj_item_storage_backpack>(get_turf(body));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(backpack, ITEM_SLOT_BACK))));
            ownPen = Dm.@new<obj_item_pen>(backpack);
            Waiting.Detach(body.dna.add_mutation(typeof(datum_mutation_elastic_arms), MUTATION_SOURCE_ACTIVATED)); // reach 2 (reach.dm:46-52)
            box = Dm.@new<obj_item_storage_box>(locate(10, 8, 1));
            pen = Dm.@new<obj_item_pen>(box);
            sensor = Dm.@new<obj_item_assembly_prox_sensor>(locate(9, 8, 1));
            Waiting.NoSleep(sensor.toggle_scan(TRUE));
        }
        rig.Tick(4 * Second);
        Assert.Equal(2f, In(rig, () => body.reach_length));
        Assert.True(In(rig, () => Dm.truthy(sensor.scanning)));
        int runtimes = rig.World.RuntimeErrorCount;
        (int Beeps, long Ids) Still(int ticks)
        {
            int beeps = alice.Texts.Count(t => t.Contains("*beep*", StringComparison.Ordinal));
            long ids = rig.World.Objects.HighestId;
            rig.Tick(ticks);
            return (alice.Texts.Count(t => t.Contains("*beep*", StringComparison.Ordinal)) - beeps, rig.World.Objects.HighestId - ids);
        }
        (int Beeps, long Ids) closed = Still(200);
        using (rig.World.Enter())
            Waiting.NoSleep(box.atom_storage.open_storage(body)); // tg's opening asks the ranged reach once, probe and all
        rig.Tick(4 * Second); // past the sensor's 3 s cooldown
        Assert.Same(box.atom_storage, In(rig, () => body.active_storage));
        Assert.Equal(box.dm_id, ListedOther(alice));
        (int Beeps, long Ids) open = Still(200);
        Assert.Equal(closed.Beeps, open.Beeps);
        // no probe object made while she stands still: a probe a tick would be 200 new ids; the server makes a few of its own either way
        Assert.True(open.Ids <= closed.Ids + 5, $"new ids: {closed.Ids} with the box closed, {open.Ids} with it open");
        // twenty examines of the pen in her own backpack, then twenty of the pen two tiles away: tg's examine makes objects of its own, the same for
        // both; a probe per examine would be twenty more, and would walk past the sensor
        long idsBefore = rig.World.Objects.HighestId;
        Assert.Equal(20, Examine(rig, alice, ownPen, 20));
        long ownIds = rig.World.Objects.HighestId - idsBefore;
        int beepsBefore = alice.Texts.Count(t => t.Contains("*beep*", StringComparison.Ordinal));
        idsBefore = rig.World.Objects.HighestId;
        Assert.Equal(20, Examine(rig, alice, pen, 20));
        long otherIds = rig.World.Objects.HighestId - idsBefore;
        Assert.Equal(beepsBefore, alice.Texts.Count(t => t.Contains("*beep*", StringComparison.Ordinal)));
        Assert.True(Math.Abs(otherIds - ownIds) <= 5, $"new ids over twenty examines: {ownIds} of her own pen, {otherIds} of the pen in the box");
        Console.WriteLine($"twenty examines: {ownIds} new ids for her own pen, {otherIds} for the pen in the box; beeps {closed.Beeps} closed, {open.Beeps} open, 0 more over the examines");
        Assert.Equal(box.dm_id, ListedOther(alice));
        Assert.Equal(runtimes, rig.World.RuntimeErrorCount);
        Assert.True(string.IsNullOrEmpty(rig.Runtimes), rig.Runtimes);
    }
}
