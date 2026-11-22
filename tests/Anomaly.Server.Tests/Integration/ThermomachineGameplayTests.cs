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
/// M7 slice 4 (thermomachine.dm, heat_exchanger.dm, meter.dm) on MetaStation through the real server and a client's own stack: an
/// engineer stands by a supermatter room freezer (the engine's cooling loop), opens its window with an empty hand (tg's ThermoMachine
/// interface, translated), turns it on and types a target. The gas of its pipe network (a canister's worth, put in as the engine's
/// players would connect one) cools over seconds, the client sees the unit lit (thermo_1) and coloured for its target, and its room's
/// APC reads a larger load than before. The loop's meters read the gas, and the engineer's alt-click cycles the target.
/// </summary>
public class ThermomachineGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

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

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf Beside(atom thing)
    {
        foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST })
        {
            turf t = get_step(thing, dir);
            if (t != null && Clear(t))
                return t;
        }
        return null;
    }

    private static AppearanceEntry Look(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? look : null;
    }

    private static string IconOf(ReplicaWorld replica, atom a)
    {
        AppearanceEntry look = Look(replica, a);
        return look == null ? null : replica.ResolveString(look.IconState);
    }

    private static string ResourceOf(ReplicaWorld replica, atom a)
    {
        AppearanceEntry look = Look(replica, a);
        return look == null ? null : replica.ResolveResource(look.Icon);
    }

    private static List<string> OverlaysOf(ReplicaWorld replica, atom a)
    {
        var shown = new List<string>();
        AppearanceEntry look = Look(replica, a);
        if (look == null)
            return shown;
        foreach (TableRef o in look.Overlays)
        {
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay))
                shown.Add(replica.ResolveString(overlay.IconState));
        }
        return shown;
    }

    [MetaServerFact]
    public void An_engineer_turns_on_a_supermatter_room_freezer_in_its_window_and_the_loops_gas_cools_with_the_rooms_apc_load_up()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 91, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // the engine's cooling loop: two freezers in the supermatter room, Meta's own (thermomachine/freezer, off, anchored on the loop)
        List<obj_machinery_atmospherics_components_unary_thermomachine_freezer> freezers = In(w, () =>
            Dm.each_cast<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_atmospherics_components_unary_thermomachine_freezer)))
                .Where(f => f.GetType() == typeof(obj_machinery_atmospherics_components_unary_thermomachine_freezer) && get_area(f) is area_station_engineering_supermatter_room)
                .OrderBy(f => f.dm_id).ToList());
        Assert.Equal(2, freezers.Count);
        obj_machinery_atmospherics_components_unary_thermomachine_freezer freezer = freezers[0];
        output.WriteLine($"the freezers at {In(w, () => freezers.Select(f => $"{f.x},{f.y}").ToList()).Aggregate((a, b) => a + " and " + b)}; the first on layer {In(w, () => freezer.piping_layer)}, anchored {In(w, () => freezer.anchored)}");
        In(w, () =>
        {
            Assert.True(Dm.truthy(freezer.anchored));
            Assert.Equal(0f, freezer.on);
            Assert.NotNull(freezer.nodes[1]);
            Assert.NotNull(freezer.parents[1]);
            Assert.Equal(5000f, freezer.heat_capacity);
            Assert.Equal(73.15f, freezer.min_temperature, 2);
            Assert.Equal(573.15f, freezer.max_temperature, 2);
        });
        w.Run(10 * Second);

        // the loop's gas: what a nitrogen canister put in the loop would be (the engine's players connect one)
        datum_pipeline net = In(w, () => (datum_pipeline)freezer.parents[1]);
        In(w, () =>
        {
            net.air.merge(SSair.parse_gas_string("n2=1500;TEMP=300") as datum_gas_mixture);
            net.update = TRUE;
        });
        w.Run(3 * Second);
        output.WriteLine($"the loop: {In(w, () => net.members.len)} pipes and {In(w, () => net.other_atmos_machines.len)} machines, {In(w, () => net.air.volume)} L, {In(w, () => net.air.temperature)} K, {In(w, () => net.air.return_pressure())} kPa");

        // an engineer beside the freezer opens its window with an empty hand
        turf stand = In(w, () => Beside(freezer));
        Assert.NotNull(stand);
        In(w, () => Waiting.NoSleep(me.forceMove(stand)));
        w.Run(Second);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, freezer.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("ThermoMachine", window.Interface);
        Assert.Equal(0, window.Merged.Num("on"));
        Assert.Equal(73.15, window.Merged.Num("min"), 2);
        Assert.Equal(573.15, window.Merged.Num("max"), 2);
        Assert.Equal(293.15, window.Merged.Num("target"), 2);
        Assert.Equal(293.15, window.Merged.Num("initial"), 2);
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Equal(300, windows.Get(window.Window).Context.WindowWidth);
        Assert.Contains("Temperature", content.AllText());
        Assert.Contains("Target Temperature", content.AllText());
        Assert.Equal("Off", ByKey(content, "power").Text);
        string stateOff = IconOf(replica, freezer);
        string resourceOff = ResourceOf(replica, freezer);
        List<string> overlaysOff = OverlaysOf(replica, freezer);
        output.WriteLine($"off: {stateOff} {resourceOff} overlays {string.Join(" ", overlaysOff)}");
        Assert.Equal("thermo_base", stateOff);
        Assert.Contains("pipe", overlaysOff); // the port's pipe (update_overlays, thermomachine.dm:128)

        // the room's power use before the unit works: what the APC's channel counts (area.energy_usage, the dynamic use_energy of tg's
        // machines) sampled twice a second over a SSmachines cycle, and the station grid's load behind the APC (add_load, power.dm:159)
        obj_machinery_power_apc apc = In(w, () => ((area)get_area(freezer)).apc);
        Assert.NotNull(apc);
        area room = In(w, () => (area)get_area(freezer));
        float Dynamic() => In(w, () => Dm.num(room.energy_usage[AREA_USAGE_ENVIRON]));
        float Sample(int halfSeconds)
        {
            float most = 0;
            for (int i = 0; i < halfSeconds; i++)
            {
                w.Run(Second / 2);
                most = Math.Max(most, Dynamic());
            }
            return most;
        }
        w.Run(4 * Second);
        float loadBefore = Sample(6);
        float coldBefore = In(w, () => net.air.temperature);
        output.WriteLine($"the room's dynamic environment use before: {loadBefore} J per cycle; the APC's own readout {In(w, () => apc.lastused_environ)}");
        Assert.Equal(0f, loadBefore);

        // power on, then the target typed in the field (tg's NumberInput commits on Enter)
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.Equal(1f, In(w, () => freezer.on));
        content = Shown(windows, replica, window.Window);
        Assert.Equal("On", ByKey(content, "power").Text);
        ByKey(content, "target").OnChange(150);
        float loadWorking = Sample(6);
        output.WriteLine($"the room's dynamic environment use working: {loadWorking} J per cycle");
        Assert.Equal(150f, In(w, () => Dm.num(freezer.target_temperature)));
        Assert.Equal(150, replica.UiWindows[window.Window].Merged.Num("target"));
        content = Shown(windows, replica, window.Window);
        Assert.Equal(150, ByKey(content, "target").Value);
        Assert.False(ByKey(content, "min").Disabled);
        Assert.False(ByKey(content, "initial").Disabled);

        // the client sees the unit lit and coloured for a cold target
        string stateOn = IconOf(replica, freezer);
        string resourceOn = ResourceOf(replica, freezer);
        output.WriteLine($"on: {stateOn} {resourceOn} overlays {string.Join(" ", OverlaysOf(replica, freezer))}");
        Assert.Equal("thermo_1", stateOn);
        Assert.NotEqual(resourceOff, resourceOn); // the greyscale colours follow the target: tg's blue for 150 K
        Assert.EndsWith("|#0000FF", resourceOn);
        Assert.EndsWith("|#00FF00", resourceOff); // room temperature: tg's lime
        // (tg's components_base update_overlays cuts the overlays of an update that changes nothing, so the pipe's picture is not kept
        // across the repeated updates of a running unit; the lit screen is the thermo_1 state itself, its emissive glow DEVIATION DV12)

        // the loop's gas cools over seconds, towards the target and not below it
        w.Run(10 * Second);
        float cold = In(w, () => net.air.temperature);
        output.WriteLine($"the loop: {coldBefore} K -> {cold} K after about 13 s");
        Assert.True(cold < coldBefore - 3, $"the loop's gas cools: {coldBefore} -> {cold}");
        Assert.True(cold > 150f);
        w.Run(20 * Second);
        float colder = In(w, () => net.air.temperature);
        Assert.True(colder < cold, $"and goes on cooling: {cold} -> {colder}");
        content = Shown(windows, replica, window.Window);
        Assert.Contains(TguiFormat.ToFixed(replica.UiWindows[window.Window].Merged.Num("temperature"), 2) + " K", ByKey(content, "temperature").Text);

        // its room's power use is larger: the unit draws power as it works (idle_power_usage, the heat curve, thermomachine.dm:197)
        Assert.True(loadWorking > loadBefore, $"the room's load rises: {loadBefore} -> {loadWorking}");
        // (the APC's own readout, lastused_environ, is read in late_process after early_process cleared the area's dynamic counters in the
        // same SSmachines cycle, so tg shows it only the static load and the machines that process early)

        // the preset buttons: minimum, room temperature
        ByKey(content, "min").OnClick();
        w.Run(Second);
        Assert.Equal(In(w, () => freezer.min_temperature), In(w, () => Dm.num(freezer.target_temperature)));
        content = Shown(windows, replica, window.Window);
        Assert.True(ByKey(content, "min").Disabled);
        ByKey(content, "initial").OnClick();
        w.Run(Second);
        Assert.Equal(293.15f, In(w, () => Dm.num(freezer.target_temperature)), 2);

        // off: the unit stops, the client's picture goes dark again
        content = Shown(windows, replica, window.Window);
        ByKey(content, "power").OnClick();
        w.Run(2 * Second);
        Assert.Equal(0f, In(w, () => freezer.on));
        Assert.Equal("thermo_base", IconOf(replica, freezer));

        // a meter of the loop reads the gas (meter.dm): the supermatter room's meters sit on its pipes
        List<obj_machinery_meter> meters = In(w, () => Dm.each_cast<obj_machinery_meter>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_meter)))
            .Where(m => get_area(m) is area_station_engineering_supermatter_room).ToList());
        Assert.NotEmpty(meters);
        Assert.All(meters, m => Assert.NotNull(In(w, () => m.target)));
        Assert.Contains(meters, m => In(w, () => Dm.@in(m.target, net.members)));
        output.WriteLine($"{meters.Count} meters in the room: {string.Join(", ", meters.Select(m => In(w, () => m.icon_state)))}");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
