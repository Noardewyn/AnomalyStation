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
/// Package E11 on MetaStation through the real server and a client's own stack: an engineer opens the incinerator's gas turbine control
/// computer, whose mapping_id found the mapped core rotor and whose three parts linked at init; the TurbineComputer window's power button
/// starts the rotor, hot gas on the compressor's input turf spins it up, the window reads the RPM and the power and the rotor's on overlay
/// reaches the client. Then a thermoelectric generator built on a fresh cable between a hot and a cold circulator, each fed by a canister
/// on a port, makes power that its ThermoElectricGenerator window shows; and an RTG on a fresh cable supplies its kilowatt to the cable.
/// </summary>
public class TurbineTegRtgGameplayTests(ITestOutputHelper output)
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

    private static bool Clear(turf t) => t is turf_open && t is not turf_open_space && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

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

    /// <summary>The centre of the nearest clear 3 by 3 block of the station around <paramref name="near"/> with no machine or cable in it and no cable beside it.</summary>
    private static turf Block(atom near, ICollection<turf> taken)
    {
        bool Free(turf t) => t != null && Clear(t) && !taken.Contains(t)
            && !Dm.each_cast<atom>(t.contents).Any(a => a is obj_machinery_atmospherics || a is obj_structure_cable || a is obj_machinery);
        for (int r = 2; r <= 80; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    turf c = locate(near.x + dx, near.y + dy, near.z);
                    if (c == null || get_area(c) is not area_station || (Math.Abs(dx) != r && Math.Abs(dy) != r))
                        continue;
                    bool ok = true;
                    for (int i = -2; i <= 2 && ok; i++)
                        for (int j = -2; j <= 2 && ok; j++)
                            ok = (Math.Abs(i) == 2 || Math.Abs(j) == 2)
                                ? !Dm.each_cast<atom>(locate(c.x + i, c.y + j, c.z)?.contents ?? Dm.list()).Any(a => a is obj_structure_cable)
                                : Free(locate(c.x + i, c.y + j, c.z));
                    if (ok)
                        return c;
                }
            }
        }
        return null;
    }

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static AppearanceEntry Look(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? look : null;
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

    private static void SetAir(datum_gas_mixture air, string mix)
    {
        air.moles = Dm.list();
        datum_gas_mixture parsed = SSair.parse_gas_string(mix) as datum_gas_mixture;
        air.merge(parsed);
        air.temperature = parsed.temperature;
    }

    [MetaServerFact]
    public void An_engineer_runs_metas_turbine_from_its_computer_and_a_built_teg_and_an_rtg_power_their_cables()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        In(w, () => ADD_TRAIT(me, TRAIT_GODMODE, ADMIN_TRAIT)); // the hot gas and the blasts of the incinerator room

        // ---- the incinerator's turbine: its computer found the mapped rotor (mapping_id "main_turbine"), the three parts linked
        // Meta's: Lavaland's ruins hold more, as their seeding rolls
        var computer = In(w, () => Assert.Single(Dm.each<obj_machinery_computer_turbine_computer>(Dm.world), c => Dm.truthy(is_station_level(c.z))));
        var core = In(w, () => (obj_machinery_power_turbine_core_rotor)computer.turbine_core.resolve());
        In(w, () =>
        {
            Assert.Equal("main_turbine", Dm.str(core.mapping_id));
            Assert.Equal(TRUE, core.all_parts_connected);
            Assert.NotNull(core.compressor);
            Assert.NotNull(core.turbine);
            Assert.NotNull(core.powernet);
            Assert.Equal(0f, Dm.num(core.active));
        });
        turf intake = In(w, () => get_step(core.compressor, REVERSE_DIR(core.compressor.dir)));
        output.WriteLine($"the rotor at {In(w, () => $"{core.x},{core.y}")}, its intake {In(w, () => $"{intake.x},{intake.y} {intake.GetType().Name}")}, the exhaust {In(w, () => get_step(core.turbine, core.turbine.dir).GetType().Name)}");

        // the exhaust: Meta's incinerator vent blast door shuts the outlet's turf at round start (TURF_SHARES 0: no power, as in tg);
        // it is opened here as its button would (the incinerator's controls are package E8's)
        var vent = In(w, () => Dm.locate<obj_machinery_door_poddoor_incinerator_atmos_main>(get_step(core.turbine, core.turbine.dir)));
        Assert.NotNull(vent);
        Assert.Equal(0f, In(w, () => TURF_SHARES(get_step(core.turbine, core.turbine.dir))));
        In(w, () => Waiting.Detach(vent.open()));
        w.Run(2 * Second);
        Assert.True(In(w, () => TURF_SHARES(get_step(core.turbine, core.turbine.dir))) > 0, "the opened vent lets the exhaust out");

        In(w, () => Waiting.NoSleep(me.forceMove(Beside(computer))));
        w.Run(Second);
        Assert.True(p.Session.Click(On(computer), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("TurbineComputer", window.Interface);
        Assert.Equal(1, window.Merged.Num("connected"));
        Assert.Equal(0, window.Merged.Num("active"));
        Assert.Equal(0.5, window.Merged.Num("regulator"));
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Equal("Offline", ByKey(content, "toggle_power").Text);

        // the power button: the rotor runs; the regulator to 80%
        ByKey(content, "toggle_power").OnClick();
        ByKey(content, "regulator").OnChange(80);
        w.Run(Second);
        Assert.Equal(1f, In(w, () => Dm.num(core.active)));
        Assert.Equal(0.8f, In(w, () => core.compressor.intake_regulator), 4);
        Assert.Equal("Online", ByKey(Shown(windows, replica, window.Window), "toggle_power").Text);
        Assert.Contains("core_on", OverlaysOf(replica, core));

        // hot gas fed on the compressor's intake: the rotor spins, its energy reaches its cable
        float maxRpm = 0, maxShownRpm = 0, maxEnergy = 0;
        double maxShownPower = 0;
        for (int n = 0; n < 12; n++)
        {
            In(w, () =>
            {
                SetAir(((turf_open)intake).air, "n2=1000;TEMP=3000");
                SSair.add_to_active((turf_open)intake);
            });
            w.Run(Second);
            maxRpm = Math.Max(maxRpm, In(w, () => core.rpm));
            maxEnergy = Math.Max(maxEnergy, In(w, () => core.produced_energy));
            maxShownRpm = Math.Max(maxShownRpm, (float)replica.UiWindows[window.Window].Merged.Num("rpm"));
            maxShownPower = Math.Max(maxShownPower, replica.UiWindows[window.Window].Merged.Num("power"));
        }
        output.WriteLine($"the turbine: up to {maxRpm} rpm, {maxEnergy} J a cycle; the window showed {maxShownRpm} rpm, {maxShownPower} W");
        Assert.True(maxRpm > 0, "the rotor spins");
        Assert.True(maxEnergy > 0, "the rotor adds energy to its cable");
        Assert.True(maxShownRpm > 0 && maxShownPower > 0, "the window reads the rpm and the power");
        Assert.Equal(In(w, () => Dm.num(core.get_turbine_integrity())), replica.UiWindows[window.Window].Merged.Num("integrity"), 2);
        p.Session.UiClose(window.Window);
        w.Run(2);

        // ---- a thermoelectric generator built between a hot circulator (west: a hot nitrogen canister on its input port south, an empty
        // one on its output port north) and a cold one (east: a cold nitrogen canister north, an empty one south)
        var taken = new List<turf>();
        turf c = In(w, () => Block(me, taken));
        Assert.NotNull(c);
        In(w, () => { for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) taken.Add(locate(c.x + i, c.y + j, c.z)); });
        output.WriteLine($"the generator at {In(w, () => $"{c.x},{c.y}")}");
        In(w, () => Waiting.NoSleep(me.forceMove(locate(c.x, c.y - 1, c.z)))); // the engineer watches from the block's south side
        w.Run(Second);
        var (teg, hot, cold) = In(w, () =>
        {
            Dm.@new<obj_structure_cable>(c).propagate_if_no_network(); // a fresh cable's own powernet (cable.dm:434)
            var h = Dm.@new<obj_machinery_atmospherics_components_binary_circulator>(locate(c.x - 1, c.y, c.z), TRUE, EAST);
            var k = Dm.@new<obj_machinery_atmospherics_components_binary_circulator_cold>(locate(c.x + 1, c.y, c.z), TRUE, WEST);
            var g = Dm.@new<obj_machinery_power_thermoelectric_generator>(c);
            var hotPort = Dm.@new<obj_machinery_atmospherics_components_unary_portables_connector_visible>(locate(c.x - 1, c.y - 1, c.z), TRUE, NORTH);
            var coldPort = Dm.@new<obj_machinery_atmospherics_components_unary_portables_connector_visible>(locate(c.x + 1, c.y + 1, c.z), TRUE, SOUTH);
            // the outputs (north of the hot circulator, south of the cold one) to empty canisters
            var hotOut = Dm.@new<obj_machinery_atmospherics_components_unary_portables_connector_visible>(locate(c.x - 1, c.y + 1, c.z), TRUE, SOUTH);
            var coldOut = Dm.@new<obj_machinery_atmospherics_components_unary_portables_connector_visible>(locate(c.x + 1, c.y - 1, c.z), TRUE, NORTH);
            SSair.setup_template_machinery(Dm.list(h, k, hotPort, coldPort, hotOut, coldOut));
            foreach (var port in new[] { hotOut, coldOut })
                Assert.True(Dm.truthy(Waiting.NoSleep(Dm.@new<obj_machinery_portable_atmospherics_canister>(port.loc).connect(port))));
            var hotCan = Dm.@new<obj_machinery_portable_atmospherics_canister_nitrogen>(hotPort.loc);
            hotCan.air_contents.temperature = 2000;
            var coldCan = Dm.@new<obj_machinery_portable_atmospherics_canister_nitrogen>(coldPort.loc);
            coldCan.air_contents.temperature = 80;
            Assert.True(Dm.truthy(Waiting.NoSleep(hotCan.connect(hotPort))));
            Assert.True(Dm.truthy(Waiting.NoSleep(coldCan.connect(coldPort))));
            return (g, h, k);
        });
        In(w, () =>
        {
            Assert.Same(hot, teg.hot_circ);
            Assert.Same(cold, teg.cold_circ);
            Assert.NotNull(teg.powernet);
            Assert.NotNull(hot.nodes[2]); // the hot port on its input side
            Assert.NotNull(cold.nodes[2]);
        });
        float maxGen = 0;
        var seen = new HashSet<string>();
        for (int n = 0; n < 8; n++)
        {
            w.Run(Second);
            maxGen = Math.Max(maxGen, In(w, () => teg.lastgenlev));
            seen.UnionWith(OverlaysOf(replica, teg));
            seen.UnionWith(OverlaysOf(replica, hot).Select(o => "hot:" + o));
        }
        output.WriteLine($"overlays seen: {string.Join(", ", seen)}");
        output.WriteLine($"the generator: up to {maxGen} W a cycle, the hot loop's delta {In(w, () => hot.last_pressure_delta)} kPa");
        Assert.True(maxGen > 0, "the generator makes power");
        Assert.True(In(w, () => Dm.num(((datum_powernet)teg.powernet).avail)) > 0, "its cable carries it");

        Assert.Contains("teg-oc11", seen); // both loops flowing (thermoelectric_generator.dm:56)
        Assert.Contains(seen, o => o.StartsWith("teg-op", StringComparison.Ordinal)); // the output level (:53)
        Assert.Contains("hot:flow_on", seen);
        Assert.Contains("hot:fan_0_1", seen); // the hot fan spinning fast: more than one atmosphere of delta (circulator.dm:79)
        Assert.True(p.Session.Click(On(teg), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("ThermoElectricGenerator", window.Interface);
        Assert.False(window.Merged.Truthy("error_message"));
        content = Shown(windows, replica, window.Window);
        Assert.StartsWith("Last Output: ", ByKey(content, "output").Text);
        Assert.StartsWith("Temperature Inlet: ", ByKey(content, "hot0-temperature").Text);
        Assert.StartsWith("Pressure Inlet: ", ByKey(content, "cold0-pressure").Text);
        p.Session.UiClose(window.Window);
        w.Run(2);

        // ---- an RTG on a fresh cable: its kilowatt (2 kJ a 2-second machines cycle) on the cable
        turf rc = In(w, () => Block(me, taken));
        Assert.NotNull(rc);
        var rtg = In(w, () =>
        {
            Dm.@new<obj_structure_cable>(rc).propagate_if_no_network();
            return Dm.@new<obj_machinery_power_rtg>(rc);
        });
        Assert.Equal(1000f, In(w, () => rtg.power_gen));
        Assert.NotNull(In(w, () => rtg.powernet));
        w.Run(5 * Second); // two machines cycles: one adds the energy (newavail), the next powernet reset makes it the cable's avail
        Assert.Equal(In(w, () => Dm.num(power_to_energy(1000))), In(w, () => Dm.num(((datum_powernet)rtg.powernet).avail)));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n", runtimes));
    }
}
