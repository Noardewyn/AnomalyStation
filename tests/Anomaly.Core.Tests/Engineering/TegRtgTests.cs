using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E11 (power/thermoelectric_generator.dm, binary_devices/circulator.dm, power/rtg.dm) in a booted toy room: a
/// thermoelectric generator on a cable between a hot circulator (west, facing east) and a cold one (east, facing west) finds both, turns
/// the heat their gas carries into tg's 65% of power, gives a tenth of it to the cable each machines cycle, shows its output and
/// pressure overlays and its window's loops; the multitool flips a circulator's mode; the RTGs add their power to the cable, their
/// capacitors' tier multiplies it, the panel shows the open sprite, and the abductors' void core blows up when blasted.
/// </summary>
public class TegRtgTests
{
    private const int Second = 20;
    private const int Y = 5;

    private static (AudioRoom r, mob_living_carbon_human bob) TegRoom(ulong seed = 11201, bool cable = true)
    {
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            for (int x = 3; x <= 9; x++)
                Dm.@new<turf_open_floor_plating>(locate(x, Y, 1));
            Dm.@new<obj_machinery_atmospherics_components_binary_circulator>(locate(5, Y, 1)).dir = EAST;
            Dm.@new<obj_machinery_atmospherics_components_binary_circulator_cold>(locate(7, Y, 1)).dir = WEST;
            Dm.@new<obj_machinery_power_thermoelectric_generator>(locate(6, Y, 1));
            if (cable)
                Dm.@new<obj_structure_cable>(locate(6, Y, 1));
        });
        mob_living_carbon_human bob = r.Join(6, Y + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static obj_machinery_power_thermoelectric_generator Teg(AudioRoom r) => r.In(() => Dm.locate<obj_machinery_power_thermoelectric_generator>(locate(6, Y, 1)));
    private static obj_machinery_atmospherics_components_binary_circulator Circ(AudioRoom r, int x) => r.In(() => Dm.locate<obj_machinery_atmospherics_components_binary_circulator>(locate(x, Y, 1)));

    private static DmList Data(AudioRoom r, mob who) => r.In(() => (DmList)Waiting.NoSleep(Teg(r).ui_data(who)));

    private static datum_gas_mixture Air(obj_machinery_atmospherics_components_binary_circulator c, int i) => (datum_gas_mixture)c.airs[i];

    private static void Fill(datum_gas_mixture air, string mix)
    {
        air.moles = Dm.list();
        air.temperature = TCMB;
        if (mix == null)
            return;
        datum_gas_mixture parsed = SSair.parse_gas_string(mix) as datum_gas_mixture;
        air.merge(parsed);
        air.temperature = parsed.temperature;
    }

    [Fact]
    public void TheGeneratorTurnsTheLoopsHeatIntoPowerOnTheCable()
    {
        var (r, bob) = TegRoom();
        var teg = Teg(r);
        var hot = Circ(r, 5);
        var cold = Circ(r, 7);
        r.In(() =>
        {
            // find_circulators at Initialize: the generator faces south, so its circulators are east and west, facing it
            Assert.Same(hot, teg.hot_circ);
            Assert.Same(cold, teg.cold_circ);
            Assert.Same(teg, hot.generator);
            Assert.Same(teg, cold.generator);
            Assert.Equal((0f, 1f), (Dm.num(hot.mode), Dm.num(cold.mode)));
            Assert.NotNull(teg.powernet);

            // the inputs (air2) full, the outputs (air1) empty: each circulator passes half the pressure difference
            Fill(Air(hot, 2), "n2=200;TEMP=5000");
            Fill(Air(hot, 1), null);
            Fill(Air(cold, 2), "n2=200;TEMP=100");
            Fill(Air(cold, 1), null);
            float hotPressure = Dm.num(Air(hot, 2).return_pressure());
            float coldPressure = Dm.num(Air(cold, 2).return_pressure());
            teg.process_atmos(null);
            Assert.Equal(hotPressure / 2, hot.last_pressure_delta, 0.01f);
            Assert.Equal(coldPressure / 2, cold.last_pressure_delta, 0.01f);
            Assert.Equal("11", teg.last_pressure_overlay);

            // the exchange (thermoelectric_generator.dm:111-118): the hot gas gives energy_transfer, 65% of it is power, 35% heats the cold gas
            datum_gas_mixture hotOut = Air(hot, 1), coldOut = Air(cold, 1);
            float energy = (5000 - hotOut.temperature) * Dm.num(hotOut.heat_capacity());
            Assert.True(energy > 0);
            Assert.Equal(obj_machinery_power_thermoelectric_generator.TEG_EFFICIENCY * energy, teg.lastgen, energy * 1e-4f);
            Assert.Equal((1 - obj_machinery_power_thermoelectric_generator.TEG_EFFICIENCY) * energy, (coldOut.temperature - 100) * Dm.num(coldOut.heat_capacity()), energy * 1e-4f);

            // process: a tenth of it to the cable a cycle
            float lastgen = teg.lastgen;
            float before = ((datum_powernet)teg.powernet).newavail;
            teg.process(null);
            Assert.Equal(round(lastgen / 10), teg.lastgenlev);
            Assert.Equal(lastgen - round(lastgen / 10), teg.lastgen);
            Assert.Equal(before + teg.lastgenlev, ((datum_powernet)teg.powernet).newavail);
            teg.update_appearance();
        });
        List<string> overlays = TurbineTests.Overlays(r, teg);
        Assert.Contains("teg-oc11", overlays);
        float level = r.In(() => min(round(teg.lastgenlev / 100000), 11));
        Assert.True(level >= 1, $"the output level {level}");
        Assert.Contains($"teg-op{level}", overlays);

        // the window: the last output and the loops (inlet = air2, outlet = air1)
        DmList data = Data(r, bob);
        Assert.Null(data["error_message"]);
        Assert.Equal(r.In(() => Dm.str(display_power(teg.lastgenlev))), Dm.str(data["last_power_output"]));
        DmList hotData = (DmList)((DmList)data["hot_data"])[1];
        Assert.Equal(r.In(() => round(Air(hot, 2).temperature, 0.1f)), Dm.num(hotData["temperature_inlet"]));
        Assert.Equal(r.In(() => round(Air(hot, 1).temperature, 0.1f)), Dm.num(hotData["temperature_outlet"]));
        DmList coldData = (DmList)((DmList)data["cold_data"])[1];
        Assert.Equal(r.In(() => round(Dm.num(Air(cold, 2).return_pressure()), 0.1f)), Dm.num(coldData["pressure_inlet"]));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCirculatorsOverlaysShowTheFlow()
    {
        var (r, _) = TegRoom(11202);
        var hot = Circ(r, 5);
        r.In(() =>
        {
            hot.last_pressure_delta = 50;
            hot.update_appearance();
        });
        List<string> overlays = TurbineTests.Overlays(r, hot);
        Assert.Contains("circ_anchor", overlays);
        Assert.Contains("panel_0", overlays);
        Assert.Contains("flow_on", overlays);
        Assert.Contains("display_0", overlays);
        Assert.Contains("fan_0_0", overlays);
        r.In(() =>
        {
            hot.last_pressure_delta = 200;
            hot.update_appearance();
        });
        Assert.Contains("fan_0_1", TurbineTests.Overlays(r, hot));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWindowTellsWhatIsMissingAndTheMultitoolFlipsACirculator()
    {
        var (r, bob) = TegRoom(11203, cable: false);
        Assert.Equal("Unable to connect to the power network!", Dm.str(Data(r, bob)["error_message"]));

        var (r2, bob2) = TegRoom(11204);
        var hot = Circ(r2, 5);
        // a multitool on the hot circulator: it leaves the generator and turns cold
        r2.In(() =>
        {
            Waiting.NoSleep(hot.multitool_act(bob2, Dm.@new<obj_item_multitool>(get_turf(bob2))));
            Assert.Equal(1f, Dm.num(hot.mode));
            Assert.Null(hot.generator);
            Assert.Null(Teg(r2).hot_circ);
        });
        Assert.Equal("Unable to locate hot circulator!", Dm.str(Data(r2, bob2)["error_message"]));
        // the generator's multitool finds the circulators again: two cold ones, the east one first (find_circulators' order), the other left out
        r2.In(() =>
        {
            Waiting.NoSleep(Teg(r2).multitool_act(bob2, Dm.@new<obj_item_multitool>(get_turf(bob2))));
            Assert.Same(Circ(r2, 7), Teg(r2).cold_circ);
            Assert.Null(Teg(r2).hot_circ);
            Assert.Null(hot.generator);
        });
        r.AssertNoRuntimes();
        r2.AssertNoRuntimes();
    }

    private static (AudioRoom r, mob_living_carbon_human bob) RtgRoom(Type rtg, ulong seed)
    {
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            for (int x = 3; x <= 6; x++)
                Dm.@new<turf_open_floor_plating>(locate(x, Y, 1));
            Dm.@new(rtg, locate(4, Y, 1));
            Dm.@new<obj_structure_cable>(locate(4, Y, 1));
        });
        mob_living_carbon_human bob = r.Join(5, Y, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    [Theory]
    [InlineData(typeof(obj_machinery_power_rtg), 1000f)]
    [InlineData(typeof(obj_machinery_power_rtg_advanced), 2500f)] // 1250 W times its board's capacitor and micro-laser, tier one each
    [InlineData(typeof(obj_machinery_power_rtg_debug), 20000f)]
    public void AnRtgAddsItsPowerToTheCable(Type type, float watts)
    {
        var (r, _) = RtgRoom(type, 11210);
        r.In(() =>
        {
            var rtg = Dm.locate<obj_machinery_power_rtg>(locate(4, Y, 1));
            Assert.NotNull(rtg.powernet);
            Assert.Equal(watts, rtg.power_gen); // times the parts' tiers (a tier one capacitor), the debug RTG ignores its parts
            float before = ((datum_powernet)rtg.powernet).newavail;
            rtg.process(null);
            Assert.Equal(before + Dm.num(power_to_energy(watts)), ((datum_powernet)rtg.powernet).newavail);

            // one tier four capacitor alone: four times the base
            if (Dm.truthy(rtg.affected_by_parts))
            {
                foreach (datum_stock_part part in Dm.each<datum_stock_part>(rtg.component_parts.Copy()))
                    Dm.remove(rtg.component_parts, part);
                Dm.append(rtg.component_parts, GLOB.stock_part_datums[typeof(datum_stock_part_capacitor_tier4)]);
                Waiting.NoSleep(rtg.RefreshParts());
                Assert.Equal(Dm.num(Dm.initial(rtg, (obj_machinery_power_rtg p) => p.power_gen)) * 4, rtg.power_gen);
            }
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRtgsPanelShowsItsOpenSprite()
    {
        var (r, bob) = RtgRoom(typeof(obj_machinery_power_rtg), 11211);
        var rtg = r.In(() => Dm.locate<obj_machinery_power_rtg>(locate(4, Y, 1)));
        Assert.Equal("rtg", r.In(() => rtg.icon_state));
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        WallTests.Click(r, bob, rtg, 1);
        Assert.True(r.In(() => Dm.truthy(rtg.panel_open)));
        Assert.Equal("rtg-open", r.In(() => rtg.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheVoidCoreOverloadsWhenBlastedAndExplodesTenSecondsLater()
    {
        var (r, _) = RtgRoom(typeof(obj_machinery_power_rtg_abductor), 11212);
        var core = r.In(() => Dm.locate<obj_machinery_power_rtg_abductor>(locate(4, Y, 1)));
        Assert.Equal(160000f, r.In(() => core.power_gen)); // 20 kW times its tier four capacitor and micro-laser (the board's def_components)
        float blasts = r.In(() => SSexplosions.explosion_index);
        r.In(() => core.ex_act(EXPLODE_LIGHT));
        Assert.Equal(1f, r.In(() => core.going_kaboom));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/magic/lightningshock.ogg");
        r.Step(9 * Second);
        Assert.Equal(blasts, r.In(() => SSexplosions.explosion_index));
        r.Step(2 * Second);
        Assert.Equal(blasts + 1, r.In(() => SSexplosions.explosion_index));
        Assert.True(r.In(() => QDELETED(core)), "the second blast deletes the overloading core");
    }
}
