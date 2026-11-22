using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M4c slice 1 through the real server on MetaStation: a headless Godot client (<see cref="PlaySession"/>) opens its
/// backpack, takes the survival box out, opens it with the other hand, takes the breath mask and puts it on (E), takes the
/// emergency oxygen tank and opens its valve with an alt-click on the held tank. A dev breach empties the room; the player
/// breathes from the tank and takes no oxygen damage. The client hears internals_on and sees the mask on the body.
/// </summary>
public class InternalsGameplayTests
{
    private const int Second = 20; // ticks per second
    private const string On = "sound/items/internals/internals_on.ogg";

    private readonly ITestOutputHelper _output;

    public InternalsGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void ClickScreen(RealHeadlessWorld.Player p, long id, ClickModifiers modifiers = ClickModifiers.None)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), ClickButton.Left, modifiers));

    private static List<string> States(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State).ToList();
    }

    [MetaServerFact]
    public void A_player_puts_on_the_survival_mask_opens_the_emergency_tank_and_breathes_through_a_breach()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 4000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        int z = In(w, () => (int)me.z);

        // The job outfit's survival box in the backpack (outfit.dm's box), with tg's breath mask and emergency oxygen tank.
        var backpack = In(w, () => me.back as obj_item);
        Assert.NotNull(backpack);
        var box = In(w, () => Dm.each_cast<atom>(backpack.contents).OfType<obj_item_storage_box_survival>().FirstOrDefault());
        Assert.NotNull(box);
        var mask = In(w, () => Dm.each_cast<atom>(box.contents).OfType<obj_item_clothing_mask_breath>().Single());
        var tank = In(w, () => Dm.each_cast<atom>(box.contents).OfType<obj_item_tank_internals_emergency_oxygen>().Single());
        Assert.True(In(w, () => Dm.num(tank.air_contents.total_moles())) > 1f, "the emergency tank is filled at Initialize");

        // Open the backpack: a click on it in the back slot with the empty hand; take the box out of the storage window.
        Assert.Null(In(w, () => me.get_active_held_item()));
        ClickScreen(p, backpack.dm_id);
        w.Run(Second);
        Assert.Contains(replica.Screen.Values, e => e.Id == box.dm_id);
        ClickScreen(p, box.dm_id);
        w.Run(Second);
        Assert.Same(box, In(w, () => me.get_active_held_item()));

        // Open the box with the other hand and take the mask; E puts it on.
        p.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(5);
        ClickScreen(p, box.dm_id);
        w.Run(Second);
        Assert.Contains(replica.Screen.Values, e => e.Id == mask.dm_id);
        ClickScreen(p, mask.dm_id);
        w.Run(Second);
        Assert.Same(mask, In(w, () => me.get_active_held_item()));
        p.Session.Keybinding(KeybindingCode.QuickEquip);
        w.Run(Second);
        Assert.Same(mask, In(w, () => me.wear_mask));
        List<string> body = States(replica, me);
        _output.WriteLine("the body as the client sees it: " + string.Join(", ", body));
        Assert.Contains("breath", body);

        // Take the tank into the emptied hand from the box's window (opened again by a click on the box if it closed).
        if (!replica.Screen.Values.Any(e => e.Id == tank.dm_id))
        {
            ClickScreen(p, box.dm_id);
            w.Run(Second);
        }
        Assert.Contains(replica.Screen.Values, e => e.Id == tank.dm_id);
        ClickScreen(p, tank.dm_id);
        w.Run(Second);
        Assert.Same(tank, In(w, () => me.get_active_held_item()));

        // Alt-click on the held tank: tank_types.dm:15, the valve opens with internals_on.
        heard.Clear();
        ClickScreen(p, tank.dm_id, ClickModifiers.Alt);
        w.Run(Second);
        Assert.Same(tank, In(w, () => me.@internal));
        Assert.Contains(heard, e => e.File == On);

        // The dev breach a tile away: the room empties into space; the tank keeps the player breathing.
        (int x, int y) = In(w, () => ((int)me.x, (int)me.y));
        float before = In(w, () => Dm.num(tank.air_contents.total_moles()));
        w.Host.EnqueueConsoleLine($"breach {x + 1} {y} {z}");
        // 25 seconds: long enough for several breaths in the vacuum, short of the crit that tg's low pressure and cold bring to
        // a human without a space suit (about 2 brute a second, M4c slice 2's suit stops it); in crit tg's breathe() loses
        // breaths (losebreath), with or without a tank.
        w.Run(25 * Second);
        float pressure = In(w, () => Dm.num(((datum_gas_mixture)Waiting.NoSleep(((turf)me.loc).return_air())).return_pressure()));
        _output.WriteLine($"pressure at the player after the breach: {pressure} kPa; tank moles {before} -> {In(w, () => Dm.num(tank.air_contents.total_moles()))}");
        Assert.True(pressure < 10f, "the room emptied: " + pressure);
        Assert.Equal(0f, In(w, () => Dm.num(me.get_oxy_loss())));
        Assert.Equal(0f, In(w, () => me.stat)); // CONSCIOUS
        Assert.Same(tank, In(w, () => me.@internal));
        Assert.True(In(w, () => Dm.num(tank.air_contents.total_moles())) < before, "the breaths came from the tank");
        Assert.Contains("breath", States(replica, me));
    }
}
