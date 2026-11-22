using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7 EMP slice (D162) through the real server on MetaStation: the dev console's <c>emp X Y 3 6</c> (tg's admin EM Pulse,
/// <c>empulse(turf, 3, 6)</c>) next to a player in the arrivals corridor. The client sees the pulse (tg's
/// <c>/obj/effect/temp_visual/emp/pulse</c>, icon state emppulse, at the epicenter) and the sparks on the machines (the
/// <c>/obj/effect/temp_visual/emp</c> of <c>machinery/emp_act</c>, empdisable); the player's worn suit zaps its cell
/// (<c>suit/space/emp_act</c>, the high-capacity cell's modifier 3), the lit jetpack on the back goes out and is disabled for
/// four seconds (<c>jetpack/emp_act</c>: the client sees its lit state vanish from the body), a held flashlight stays as it was
/// (tg gives a flashlight no <c>emp_act</c>), a power cell on the floor drains, an EMP-proof one does not, and the machines
/// around react (the light fixtures show sparks). No runtime.
/// </summary>
public class EmpGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int AroundX = 46, AroundY = 122; // the arrivals corridor: the fixtures nearest to it are the room
    private const string PulsePath = "/obj/effect/temp_visual/emp/pulse";
    private const string SparksPath = "/obj/effect/temp_visual/emp";

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

    private static string IconOf(ReplicaWorld replica, ReplicaEntity e)
        => replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;

    private static List<string> BodyStates(ReplicaWorld replica, ReplicaEntity e)
        => CrewOutfitTests.Icons(replica, e.State.Appearance).Select(i => i.State).ToList();

    [MetaServerFact]
    public void An_emp_next_to_a_suited_player_is_seen_and_hits_the_cell_the_jetpack_the_floor_cells_and_the_machines()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 73, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        ReplicaWorld replica = alice.Session.Replica;

        // the spot: the lit fixture nearest to the arrivals corridor, its machines around it
        obj_machinery_light anchor = In(w, () => Dm.each<obj_machinery_light>(Dm.world)
            .Where(l => Dm.eq(l.z, 1) && Dm.truthy(l.on) && l.loc is turf)
            .OrderBy(l => Math.Abs(Dm.num(l.x) - AroundX) + Math.Abs(Dm.num(l.y) - AroundY))
            .First());
        int ax = (int)In(w, () => Dm.num(anchor.x)), ay = (int)In(w, () => Dm.num(anchor.y));
        In(w, () => Waiting.NoSleep(me.forceMove(anchor.loc as turf)));
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        const int Heavy = 3, Light = 6;
        List<obj_machinery> machines = In(w, () => Dm.each<obj_machinery>(Dm.world)
            .Where(m => Dm.eq(m.z, 1) && Dm.truthy(m.use_power) && !Dm.truthy(m.machine_stat) && m.loc is turf && Builtins.get_dist(m, locate(ax, ay, 1)) <= Heavy)
            .ToList());
        List<obj_machinery_light> fixtures = machines.OfType<obj_machinery_light>().ToList();
        output.WriteLine($"{machines.Count} machines within {Heavy} tiles of {ax},{ay}: {string.Join(", ", machines.Select(m => m.GetType().Name).Distinct())}");
        Assert.True(fixtures.Count >= 1, "a light fixture is in the heavy range");

        // the player: an EVA suit on the body, a lit CO2 jetpack on the back, a lit flashlight in the hand
        var suit = In(w, () => Dm.@new<obj_item_clothing_suit_space_eva>(me.loc));
        Assert.True(In(w, () => Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(suit, ITEM_SLOT_OCLOTHING)))));
        if (In(w, () => me.back) != null)
        {
            In(w, () => Waiting.NoSleep(me.dropItemToGround(me.back)));
            w.Run(5);
        }
        var jetpack = In(w, () => Dm.@new<obj_item_tank_jetpack_carbondioxide>(me.loc));
        Assert.True(In(w, () => Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(jetpack, ITEM_SLOT_BACK)))));
        In(w, () => Waiting.NoSleep(jetpack.turn_on(me)));
        var flashlight = In(w, () => Dm.@new<obj_item_flashlight>(me.loc));
        Assert.True(In(w, () => Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(flashlight)))));
        In(w, () => Waiting.NoSleep(flashlight.attack_self(me)));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(flashlight.light_on)), "the flashlight is lit");
        Assert.True(In(w, () => Dm.truthy(jetpack.on)), "the jetpack is lit");
        var cell = In(w, () => suit.cell_instance);
        float cellBefore = In(w, () => cell.charge);
        Assert.True(BodyStates(replica, replica.Entities.Single(e => e.Id == me.dm_id)).Contains("jetpack-black-on"), "the client sees the lit jetpack on the back");

        // two cells on the floor of the pulse: a plain one and an EMP-proof one
        var plain = In(w, () => Dm.@new<obj_item_stock_parts_power_store_cell>(locate(ax, ay, 1)));
        In(w, () => { plain.maxcharge = 10 * STANDARD_CELL_CHARGE; plain.charge = plain.maxcharge; });
        var proof = In(w, () => Dm.@new<obj_item_stock_parts_power_store_cell_emproof>(locate(ax, ay, 1)));
        float proofBefore = In(w, () => proof.charge);
        w.Run(Second);

        // the pulse: heavy within 3 tiles, light out to 6 (the dev console's emp, tg's admin EM Pulse)
        w.Host.EnqueueConsoleLine($"emp {ax} {ay} {Heavy} {Light}");
        w.Run(3); // the effects live 8 and 10 deciseconds (16 and 20 ticks): look while they are there

        List<ReplicaEntity> pulses = replica.Entities.Where(e => e.TypePath == PulsePath).ToList();
        List<ReplicaEntity> sparks = replica.Entities.Where(e => e.TypePath == SparksPath).ToList();
        output.WriteLine($"the client sees {pulses.Count} pulse and {sparks.Count} spark entities; icons {string.Join(",", pulses.Concat(sparks).Select(e => IconOf(replica, e)).Distinct())}");
        ReplicaEntity pulse = Assert.Single(pulses);
        Assert.Equal("emppulse", IconOf(replica, pulse));
        Assert.Equal((ax, ay), ((int)pulse.State.X, (int)pulse.State.Y)); // the effect is at the epicenter
        Assert.NotEmpty(sparks);
        Assert.All(sparks, e => Assert.Equal("empdisable", IconOf(replica, e)));
        // the machines react: every machine of the heavy range shows its sparks to the client (machinery/emp_act, _machinery.dm:350): the fixtures, the air and fire alarms, the camera, the airlock
        var sparkTiles = sparks.Select(e => ((int)e.State.X, (int)e.State.Y)).ToHashSet();
        foreach (obj_machinery m in machines)
            Assert.Contains((((int)In(w, () => Dm.num(m.x))), (int)In(w, () => Dm.num(m.y))), sparkTiles);

        // the player: severity 1 (heavy) plus the EVA suit's emp_protection (none) reaches the suit's cell with its modifier 3
        float modifier = In(w, () => cell.emp_damage_modifier);
        Assert.Equal(cellBefore - modifier * STANDARD_CELL_CHARGE, In(w, () => cell.charge), 0.1f);
        // the jetpack went out and is disabled, and the client sees the lit state gone from the body
        Assert.False(In(w, () => Dm.truthy(jetpack.on)));
        Assert.True(In(w, () => Dm.truthy(jetpack.disabled)));
        Assert.DoesNotContain("jetpack-black-on", BodyStates(replica, replica.Entities.Single(e => e.Id == me.dm_id)));
        // a held flashlight is not affected by tg's emp_act (it has none)
        Assert.True(In(w, () => Dm.truthy(flashlight.light_on)));
        // the floor cells: the plain one drained (heavy or light, whatever distance 0 is: heavy), the EMP-proof one did not
        Assert.Equal(10 * STANDARD_CELL_CHARGE - STANDARD_CELL_CHARGE, In(w, () => plain.charge), 0.1f);
        Assert.Equal(proofBefore, In(w, () => proof.charge));

        // four seconds later the jetpack can be lit again
        w.Run(4 * Second);
        Assert.False(In(w, () => Dm.truthy(jetpack.disabled)));
        // and the effects are gone
        w.Run(Second);
        Assert.DoesNotContain(replica.Entities, e => e.TypePath == PulsePath);
        Assert.DoesNotContain(replica.Entities, e => e.TypePath == SparksPath);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the pulse ran into a runtime
    }
}
