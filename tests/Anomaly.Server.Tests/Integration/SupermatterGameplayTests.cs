using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The M7 supermatter slice (G5, D163, D167) through the real server on MetaStation's engine: an engineer puts nitrogen in the
/// cooling loop, turns its two freezers on in their windows (tg's ThermoMachine interface) and the loop's pumps in theirs (tg's
/// AtmosPump interface), then turns the engine's emitters on with his hand. Their beams energise the crystal (eat_bullets), which gains power, zaps the tesla coils (supermatter_zap) and the coils'
/// release raises the engine network's available power (measured); the crystal's waste heats the gas around it. The client sees
/// the crystal (tg's sprite, supermatter.dmi), its glow (its light and the rays filter, _sm_delam.dm:115-140) and the zaps' beams,
/// and hears its hum (the looping sound, machinery_sounds.dm:13). A second player who walks into the crystal is dusted. A crystal
/// past its danger point (the dev console's smcrystal) makes anomalies, and the client sees one. No new runtime.
/// </summary>
public class SupermatterGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const string BeamPath = "/obj/effect/ebeam";

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

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf Beside(atom thing)
    {
        foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST })
        {
            turf t = get_step(thing, dir);
            if (t != null && Clear(t))
                return t;
        }
        return null;
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    [MetaServerFact]
    public void Emitters_on_the_crystal_with_the_loop_running_power_it_zap_the_coils_and_a_player_who_walks_into_it_is_dusted()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 83, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        RealHeadlessWorld.Player q = w.Join("Bob");
        var me = (mob_living_carbon_human)w.MobOf(p);
        var bob = (mob_living_carbon_human)w.MobOf(q);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        // MetaStation's engine (MetaStation.dmm: /obj/machinery/power/supermatter_crystal/engine in the supermatter chamber)
        obj_machinery_power_supermatter_crystal sm = In(w, () => Assert.Single(Dm.each<obj_machinery_power_supermatter_crystal>(Dm.world).Where(c => Dm.truthy(is_station_level(c.z))).ToList()));
        Assert.IsType<obj_machinery_power_supermatter_crystal_engine>(sm);
        Assert.Same(sm, In(w, () => GLOB.main_supermatter_engine));
        int smx = (int)In(w, () => Dm.num(sm.x)), smy = (int)In(w, () => Dm.num(sm.y));
        output.WriteLine($"the crystal at {smx},{smy} in {In(w, () => Dm.str(((area)get_area(sm)).name))}, {In(w, () => sm.internal_energy)} MeV");
        Assert.Equal(0f, In(w, () => sm.internal_energy));

        // the cooling loop's two freezers, with a canister's worth of nitrogen in the loop (ThermomachineGameplayTests)
        List<obj_machinery_atmospherics_components_unary_thermomachine_freezer> freezers = In(w, () =>
            Dm.each_cast<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_atmospherics_components_unary_thermomachine_freezer)))
                .Where(f => f.GetType() == typeof(obj_machinery_atmospherics_components_unary_thermomachine_freezer) && get_area(f) is area_station_engineering_supermatter_room)
                .OrderBy(f => f.dm_id).ToList());
        Assert.Equal(2, freezers.Count);
        In(w, () =>
        {
            var loop = (datum_pipeline)freezers[0].parents[1];
            loop.air.merge(SSair.parse_gas_string("n2=1500;TEMP=300") as datum_gas_mixture);
            loop.update = TRUE;
        });
        foreach (var freezer in freezers)
        {
            In(w, () => Waiting.NoSleep(me.forceMove(Beside(freezer))));
            w.Run(Second);
            Assert.True(p.Session.Click(On(freezer), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
            UiWindowState window = replica.UiWindows.Values.Single(v => v.Interface == "ThermoMachine");
            Shown(windows, replica, window.Window).Walk().First(n => n.Key == "power").OnClick();
            w.Run(Second);
            Assert.Equal(1f, In(w, () => freezer.on));
            p.Session.UiClose(window.Window);
            w.Run(2);
        }

        // the loop's pumps (the pressure and volume pumps of the supermatter room): the engineer switches each on in its window
        // (tg's AtmosPump interface, translated), so the loop moves its gas through the chamber and the freezers as in tg
        List<obj_machinery_atmospherics_components_binary> pumps = In(w, () => Dm.each<obj_machinery_atmospherics_components_binary>(Dm.world)
            .Where(m => (m is obj_machinery_atmospherics_components_binary_pump || m is obj_machinery_atmospherics_components_binary_volume_pump)
                && (get_area(m) is area_station_engineering_supermatter_room || get_area(m) is area_station_engineering_supermatter))
            .OrderBy(m => m.dm_id).ToList());
        output.WriteLine($"{pumps.Count} loop pumps: {string.Join(", ", pumps.Select(m => In(w, () => $"{m.GetType().Name} at {m.x},{m.y} on {m.on}")))}");
        Assert.NotEmpty(pumps);
        foreach (var pump in pumps.Where(m => In(w, () => !Dm.truthy(m.on))))
        {
            turf by = In(w, () => Beside(pump));
            if (by == null)
                continue;
            In(w, () => Waiting.NoSleep(me.forceMove(by)));
            w.Run(Second);
            Assert.True(p.Session.Click(On(pump), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
            UiWindowState window = replica.UiWindows.Values.Single(v => v.Interface == "AtmosPump");
            UiNode content = Shown(windows, replica, window.Window);
            Assert.Equal("Off", content.Walk().First(n => n.Key == "power").Text);
            content.Walk().First(n => n.Key == "power").OnClick();
            w.Run(Second);
            Assert.Equal(1f, In(w, () => pump.on));
            Assert.Equal("On", Shown(windows, replica, window.Window).Walk().First(n => n.Key == "power").Text);
            p.Session.UiClose(window.Window);
            w.Run(2);
        }
        int pumpsOn = pumps.Count(m => In(w, () => Dm.truthy(m.on)));
        output.WriteLine($"{pumpsOn} of the loop's pumps on");

        // the engine's emitters: the welded ones on the grid aim at the crystal; the engineer's hand turns each on
        List<obj_machinery_power_emitter> emitters = In(w, () => Dm.each<obj_machinery_power_emitter>(Dm.world)
            .Where(e => Dm.truthy(e.welded) && e.powernet != null && get_dist(e, sm) <= 8).ToList());
        output.WriteLine($"{emitters.Count} welded emitters by the crystal: {string.Join(", ", emitters.Select(e => In(w, () => $"{e.x},{e.y} facing {e.dir}")))}");
        Assert.NotEmpty(emitters);
        foreach (var emitter in emitters)
        {
            In(w, () => Waiting.NoSleep(me.forceMove(Beside(emitter))));
            w.Run(Second);
            Assert.True(p.Session.Click(On(emitter), ClickButton.Left, ClickModifiers.None));
            w.Run(2);
            Assert.True(In(w, () => Dm.truthy(emitter.active)), "the emitter is on");
        }
        // the engineer watches from the chamber's door side, out of the bolts' way
        In(w, () => Waiting.NoSleep(me.forceMove(Dm.each<turf>(orange(5, sm)).First(t => Clear(t) && get_dist(t, sm) >= 3 && get_area(t) == get_area(sm)))));

        // the engine network: the coils' and rods' network
        List<obj_machinery_power_energy_accumulator_tesla_coil> coils = In(w, () => Dm.each<obj_machinery_power_energy_accumulator_tesla_coil>(Dm.world).Where(c => c.powernet != null).ToList());
        Assert.NotEmpty(coils);
        datum_powernet net = In(w, () => (datum_powernet)coils[0].powernet);
        float availBefore = In(w, () => net.avail);
        float tileTemp0 = In(w, () => ((datum_gas_mixture)Waiting.NoSleep(((turf)sm.loc).return_air())).temperature);
        output.WriteLine($"{coils.Count} coils on networks; the engine network {availBefore} J a cycle; the crystal's tile at {tileTemp0} K");

        // a minute of emitter fire
        var beamsSeen = new Dictionary<long, ReplicaEntity>();
        float maxEnergy = 0, maxAvail = availBefore, coilEnergy = 0;
        for (int t = 0; t < 60 * Second; t++)
        {
            w.Run(1);
            foreach (ReplicaEntity e in replica.Entities.Where(e => e.TypePath != null && e.TypePath.StartsWith(BeamPath, StringComparison.Ordinal)))
                beamsSeen[e.Id] = e;
            if (t % Second == 0)
            {
                maxEnergy = Math.Max(maxEnergy, In(w, () => sm.internal_energy));
                maxAvail = Math.Max(maxAvail, In(w, () => net.avail));
                coilEnergy = Math.Max(coilEnergy, In(w, () => coils.Sum(c => c.stored_energy + c.processed_energy)));
            }
        }
        float tileTemp1 = In(w, () => ((datum_gas_mixture)Waiting.NoSleep(((turf)sm.loc).return_air())).temperature);
        output.WriteLine($"after a minute: the crystal reached {maxEnergy} MeV (now {In(w, () => sm.internal_energy)}), integrity {In(w, () => Dm.num(sm.get_integrity_percent()))}%, the coils took {coilEnergy} J, the network peaked at {maxAvail} J a cycle; the tile at {tileTemp1} K; the client saw {beamsSeen.Count} beam segments");
        Assert.True(maxEnergy > 0, "the emitters' beams energise the crystal");
        Assert.True(coilEnergy > 0, "the crystal's bolts reach the coils");
        Assert.True(maxAvail > availBefore, "the coils' release raises the engine network's power");
        Assert.NotEmpty(beamsSeen);
        Assert.True(tileTemp1 > tileTemp0, "the crystal's waste heats the gas around it");

        // the client sees the crystal: tg's sprite, its light and the rays filter of its glow
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == sm.dm_id);
        Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
        Assert.Equal("icons/obj/machines/engine/supermatter.dmi", replica.ResolveResource(look.Icon));
        Assert.StartsWith("sm", replica.ResolveString(look.IconState));
        Assert.NotNull(look.Filters);
        Assert.Contains(look.Filters, f => f.Kind == FilterKind.Rays);
        Assert.True(seen.State.Light.Range >= 4 && seen.State.Light.On, $"the crystal glows: range {seen.State.Light.Range}");
        // and hears its hum
        output.WriteLine($"heard: {string.Join(", ", heard.Where(e => e.File != null).Select(e => e.File).Distinct().Take(20))}");
        Assert.Contains(heard, e => e.File != null && e.File.StartsWith("sound/machines/sm/loops/", StringComparison.Ordinal));

        // Bob walks into the crystal from its west and is dusted
        turf west = In(w, () => get_step(sm, WEST));
        int bobRow = smy;
        if (!In(w, () => Clear(west)))
        {
            west = In(w, () => get_step(sm, EAST));
        }
        bool fromWest = In(w, () => west == get_step(sm, WEST));
        In(w, () => Waiting.NoSleep(bob.forceMove(west)));
        w.Run(Second);
        q.Yaw = fromWest ? -Math.PI / 2 : Math.PI / 2; // Godot's yaw: 0 looks north, +90 degrees west
        q.Keys = MoveKeys.Forward;
        for (int t = 0; t < 2 * Second && !In(w, () => bob.stat == DEAD || QDELETED(bob)); t++)
            w.Run(1);
        q.Keys = MoveKeys.None;
        w.Run(2 * Second);
        output.WriteLine($"Bob: dead {In(w, () => bob.stat == DEAD)}, deleted {In(w, () => QDELETED(bob))}; the crystal's trickle {In(w, () => sm.external_power_trickle)}");
        Assert.True(In(w, () => bob.stat == DEAD || QDELETED(bob)), "Bob is dusted");
        Assert.True(In(w, () => Dm.locate<obj_effect_decal_remains_human>(west) != null), "his bones are left where he stood (spawn_dust)");
        Assert.Contains(heard, e => e.File == "sound/effects/supermatter.ogg");

        // a crystal past its danger point (the dev console's smcrystal: its power decays below 5000 MeV within a cycle, its damage
        // heals slowly) runs handle_high_power every cycle and makes anomalies (supermatter_extra_effects.dm:120-127); the client
        // sees them
        w.Host.EnqueueConsoleLine($"smcrystal {smx} {smy} 1 power 6000");
        w.Host.EnqueueConsoleLine($"smcrystal {smx} {smy} 1 damage 70");
        ReplicaEntity anomaly = null;
        for (int t = 0; t < 60 * Second && anomaly == null; t++)
        {
            w.Run(1);
            anomaly = replica.Entities.FirstOrDefault(e => e.TypePath != null && e.TypePath.StartsWith("/obj/effect/anomaly/", StringComparison.Ordinal));
        }
        Assert.NotNull(anomaly);
        Assert.True(replica.TryGetAppearance(anomaly.State.Appearance, out AppearanceEntry anomalyLook));
        output.WriteLine($"the client sees {anomaly.TypePath} at {anomaly.State.X},{anomaly.State.Y}: {replica.ResolveResource(anomalyLook.Icon)} {replica.ResolveString(anomalyLook.IconState)}");
        Assert.True(anomaly.State.Light.Range > 0, "an anomaly glows (light_range 3)");
        In(w, () => DevCommands.SmCrystal((turf)sm.loc, "damage", 0));

        w.World.RuntimeErrorSink = sink;
        output.WriteLine($"runtimes: {string.Join(" | ", errors.Select(e => e.ToString()[..Math.Min(e.ToString().Length, 2500)]).Distinct())}");
        Assert.Empty(errors);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
