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
/// Engineering package E11 (power/turbine/turbine.dm, turbine_computer.dm, turbine_parts.dm): a mapped gas turbine in a booted toy room,
/// inlet compressor (4, 4), core rotor (5, 4) on a cable and turbine outlet (6, 4) all facing east, and its control computer sharing the
/// rotor's mapping_id. The parts link at init with their tier one parts, the computer finds its rotor, the window's toggle starts the
/// rotor, hot gas on the compressor's input turf spins it and its energy reaches the cable, the heat above the rating damages it, sounds
/// the alarm and at zero integrity blows it up; the tier parts' ratings and upgrades; the panel, the part swap and the alignment checks.
/// </summary>
public class TurbineTests
{
    private const int Second = 20;
    private const int Y = 4;

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed = 11101)
    {
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            for (int x = 2; x <= 10; x++)
                Dm.@new<turf_open_floor_plating>(locate(x, Y, 1));
            Dm.@new<obj_machinery_power_turbine_inlet_compressor>(locate(4, Y, 1)).dir = EAST;
            var core = Dm.@new<obj_machinery_power_turbine_core_rotor>(locate(5, Y, 1));
            core.dir = EAST;
            core.mapping_id = "main_turbine";
            Dm.@new<obj_machinery_power_turbine_turbine_outlet>(locate(6, Y, 1)).dir = EAST;
            Dm.@new<obj_structure_cable>(locate(5, Y, 1));
            Dm.@new<obj_machinery_computer_turbine_computer>(locate(8, Y + 2, 1)).mapping_id = "main_turbine";
        });
        mob_living_carbon_human bob = r.Join(8, Y + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static T At<T>(AudioRoom r, int x, int y = Y) where T : atom => r.In(() => Dm.locate<T>(locate(x, y, 1)));
    private static obj_machinery_power_turbine_core_rotor Core(AudioRoom r) => At<obj_machinery_power_turbine_core_rotor>(r, 5);
    private static obj_machinery_computer_turbine_computer Computer(AudioRoom r) => At<obj_machinery_computer_turbine_computer>(r, 8, Y + 2);

    private static DmList Data(AudioRoom r, mob who) => r.In(() => (DmList)Waiting.NoSleep(Computer(r).ui_data(who)));

    private static object Act(AudioRoom r, mob who, string action, DmList parameters = null) => r.In(() =>
        Waiting.NoSleep(Computer(r).ui_act(action, parameters ?? Dm.list(), Dm.@new<datum_tgui>(who, Computer(r), "TurbineComputer", "turbine"), null)));

    /// <summary>Sets the tile's gas to the mix (a gas string, as the maps' initial_gas_mix).</summary>
    private static void SetAir(AudioRoom r, int x, int y, string mix) => r.In(() =>
    {
        datum_gas_mixture air = ((turf_open)locate(x, y, 1)).air;
        air.moles = Dm.list();
        air.temperature = TCMB;
        datum_gas_mixture parsed = SSair.parse_gas_string(mix) as datum_gas_mixture;
        air.merge(parsed);
        air.temperature = parsed.temperature;
    });

    internal static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o switch
    {
        string t => t,
        mutable_appearance m => m.icon_state,
        DmAppearance d => d.icon_state,
        _ => null,
    }).Where(s => s != null).ToList());

    [Fact]
    public void TheMappedTurbineLinksItsPartsAndTheComputerFindsItsRotor()
    {
        var (r, bob) = Room();
        r.In(() =>
        {
            var core = Core(r);
            var inlet = Dm.locate<obj_machinery_power_turbine_inlet_compressor>(locate(4, Y, 1));
            var outlet = Dm.locate<obj_machinery_power_turbine_turbine_outlet>(locate(6, Y, 1));

            // Initialize (turbine.dm:23): the gas mixes of tg's volumes and the tier one parts of a mapped turbine
            Assert.Equal((1000f, 3000f, 6000f), (Dm.num(inlet.machine_gasmix.volume), Dm.num(core.machine_gasmix.volume), Dm.num(outlet.machine_gasmix.volume)));
            Assert.IsType<obj_item_turbine_parts_compressor>(inlet.installed_part);
            Assert.IsType<obj_item_turbine_parts_rotor>(core.installed_part);
            Assert.IsType<obj_item_turbine_parts_stator>(outlet.installed_part);
            Assert.Equal((0.25f, 0.25f, 0.85f), (inlet.efficiency, core.efficiency, outlet.efficiency));
            Assert.NotNull(Dm.locate<obj_item_paper_guides_jobs_atmos_turbine>(locate(5, Y, 1))); // the rotor's guide paper

            // post_machine_initialize: activate_parts links the three (the compressor behind the rotor, the outlet ahead)
            Assert.Equal(TRUE, core.all_parts_connected);
            Assert.Same(core, inlet.rotor);
            Assert.Same(core, outlet.rotor);
            Assert.Equal((float)TURBINE_MAX_BASE_RPM, core.max_allowed_rpm);
            Assert.Equal(50000f, core.max_allowed_temperature);
            Assert.NotNull(core.powernet);
            Assert.Equal(0f, Dm.num(Dm.bitand(core.machine_stat, MAINT)));
        });

        // the computer's mapping_id found the rotor (turbine_computer.dm:13); the window says connected and off
        DmList data = Data(r, bob);
        Assert.Equal((1f, 0f, 0f, 0.5f, 100f), (Dm.num(data["connected"]), Dm.num(data["active"]), Dm.num(data["rpm"]), Dm.num(data["regulator"]), Dm.num(data["integrity"])));
        Assert.Equal((50000f, 50000f), (Dm.num(data["max_rpm"]), Dm.num(data["max_temperature"])));
        Assert.Equal(0f, Dm.num(data["temp"])); // the compressor finds its input turf when it first runs
        Assert.Contains("inlet_off", Overlays(r, At<obj_machinery_power_turbine_inlet_compressor>(r, 4)));
        Assert.Contains("outlet_off", Overlays(r, At<obj_machinery_power_turbine_turbine_outlet>(r, 6)));
        Assert.DoesNotContain("core_on", Overlays(r, Core(r)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void HotGasSpinsTheRotorAndItsEnergyReachesTheCable()
    {
        var (r, bob) = Room(11102);
        SetAir(r, 3, Y, "n2=400;TEMP=1200");

        // toggle_power: on (all parts linked), the parts show their on overlays
        Assert.True(Dm.truthy(Act(r, bob, "toggle_power")));
        var core = Core(r);
        Assert.Equal(1f, Dm.num(r.In(() => core.active)));
        Assert.Contains("core_on", Overlays(r, core));
        Assert.Contains("inlet_on", Overlays(r, At<obj_machinery_power_turbine_inlet_compressor>(r, 4)));
        Assert.Contains("outlet_on", Overlays(r, At<obj_machinery_power_turbine_turbine_outlet>(r, 6)));

        // the regulator: clamped to 1% .. 100%, a non-number ignored
        Assert.True(Dm.truthy(Act(r, bob, "regulate", Dm.list(Dm.kv("regulate", 2)))));
        Assert.Equal(1f, r.In(() => core.compressor.intake_regulator));
        Assert.True(Dm.truthy(Act(r, bob, "regulate", Dm.list(Dm.kv("regulate", "0.001")))));
        Assert.Equal(0.01f, r.In(() => core.compressor.intake_regulator));
        Assert.False(Dm.truthy(Act(r, bob, "regulate", Dm.list(Dm.kv("regulate", "banana")))));
        Assert.Equal(0.01f, r.In(() => core.compressor.intake_regulator));
        Assert.True(Dm.truthy(Act(r, bob, "regulate", Dm.list(Dm.kv("regulate", 0.8f)))));
        Assert.Equal(0.8f, r.In(() => core.compressor.intake_regulator));

        float before = r.In(() => Dm.num(((turf_open)locate(7, Y, 1)).air.total_moles()));
        r.In(() =>
        {
            Assert.Null(core.process(2f)); // one machines cycle on the fresh hot air (the toy room's air does not flow while the test holds it)
            Assert.True(core.rpm > 0, $"the rotor spins: {core.rpm} rpm");
            Assert.Equal(MathF.Ceiling(core.rpm), core.rpm); // ROUND_UP
            Assert.True(core.rpm <= core.max_allowed_rpm);
            Assert.Equal(core.rpm * TURBINE_ENERGY_RECTIFICATION_MULTIPLIER * TURBINE_RPM_CONVERSION * 2f, core.produced_energy);
            Assert.True(((datum_powernet)core.powernet).newavail >= core.produced_energy, $"the cable gets the energy: {((datum_powernet)core.powernet).newavail}");
            Assert.Equal(0f, Dm.num(core.damage)); // well under the 50000 K rating: no damage
        });
        Assert.True(r.In(() => Dm.num(((turf_open)locate(7, Y, 1)).air.total_moles())) > before, "the outlet expels the gas east");

        DmList data = Data(r, bob);
        Assert.Equal(r.In(() => core.rpm), Dm.num(data["rpm"]));
        Assert.Equal(r.In(() => Dm.num(energy_to_power(core.produced_energy))), Dm.num(data["power"]));
        Assert.Equal(1200f, Dm.num(data["temp"]), 0.5f);

        // off is refused above 1000 rpm; at a stop it turns off and the rotor stops processing
        if (r.In(() => core.rpm) > 1000)
            Assert.False(Dm.truthy(Act(r, bob, "toggle_power")));
        r.In(() => core.rpm = 0);
        Assert.True(Dm.truthy(Act(r, bob, "toggle_power")));
        Assert.Equal(0f, Dm.num(r.In(() => core.active)));
        Assert.DoesNotContain("core_on", Overlays(r, core));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABlockedOutletMakesNoPower()
    {
        var (r, bob) = Room(11103);
        SetAir(r, 3, Y, "n2=400;TEMP=1200");
        r.In(() => Waiting.NoSleep(locate(7, Y, 1).ChangeTurf(typeof(turf_closed_wall))));
        Assert.True(Dm.truthy(Act(r, bob, "toggle_power")));
        r.In(() =>
        {
            var core = Core(r);
            Assert.Null(core.process(2f));
            Assert.Equal((0f, 0f), (core.rpm, core.produced_energy));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void HeatAboveTheRatingDamagesTheTurbineSoundsTheAlarmAndBlowsItUpAtZero()
    {
        var (r, bob) = Room(11104);
        SetAir(r, 3, Y, "n2=100;TEMP=1000000");
        Assert.True(Dm.truthy(Act(r, bob, "toggle_power")));
        var core = Core(r);

        // one process 950000 K over the rating: round(log(90, 950000), 0.5) = 3, half of it is damage; the alarm waits for 2 or more than 15
        r.In(() => core.process(2f));
        Assert.Equal(1.5f, r.In(() => Dm.num(core.damage)));
        Assert.DoesNotContain(r.Sounds.Heard, h => h.File == "sound/machines/engine_alert/engine_alert1.ogg");

        // above 15 damage: the alarm sounds and the cooldown holds it max(round(15 - 3), 5) = 12 seconds
        r.In(() => core.damage = 20);
        r.In(() => core.process(2f));
        Assert.Equal(21.5f, r.In(() => Dm.num(core.damage)));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/engine_alert/engine_alert1.ogg");
        Assert.Equal(r.In(() => world.time + 12 * SECONDS), r.In(() => core.turbine_damage_alert));
        Assert.Equal(95.7f, Dm.num(r.In(() => core.get_turbine_integrity())), 0.01f);

        // cooled under the rating it heals two a process
        SetAir(r, 3, Y, "n2=100;TEMP=300");
        r.In(() => core.process(2f));
        Assert.Equal(19.5f, r.In(() => Dm.num(core.damage)));

        // at zero integrity: the parts unlink and the rotor blows up (rpm is 0 after deactivate_parts: tg's smallest blast, 0/1/4)
        SetAir(r, 3, Y, "n2=100;TEMP=1000000");
        float blasts = r.In(() => SSexplosions.explosion_index);
        r.In(() =>
        {
            core.damage = 499;
            core.turbine_damage_alert = 0;
            Assert.Equal(PROCESS_KILL, Dm.num(core.process(2f)));
            Assert.Equal(0f, Dm.num(core.active));
            Assert.Equal(0f, core.all_parts_connected);
        });
        r.Step(Second);
        Assert.Equal(blasts + 1, r.In(() => SSexplosions.explosion_index));
    }

    [Fact]
    public void ThePartsTiersRaiseTheirRatingsAndSheetsUpgradeThem()
    {
        var (r, bob) = Room(11105);
        r.In(() =>
        {
            var rotor = Dm.@new<obj_item_turbine_parts_rotor>(locate(8, Y + 1, 1));
            var stator = Dm.@new<obj_item_turbine_parts_stator>(locate(8, Y + 1, 1));
            float[] rpm = { 50000, 125000, 312500, 781250 };
            float[] efficiency = { 0.25f, 0.45f, 0.65f, 0.85f };
            float[] statorEfficiency = { 0.85f, 0.865f, 0.88f, 0.895f };
            float temp = 50000;
            for (int tier = 1; tier <= 4; tier++)
            {
                rotor.current_tier = stator.current_tier = tier;
                Assert.Equal(rpm[tier - 1], Dm.num(rotor.get_tier_value(TURBINE_MAX_RPM)));
                Assert.Equal(temp, Dm.num(rotor.get_tier_value(TURBINE_MAX_TEMP)));
                Assert.Equal(efficiency[tier - 1], Dm.num(rotor.get_tier_value(TURBINE_MAX_EFFICIENCY)), 0.0001f);
                Assert.Equal(statorEfficiency[tier - 1], Dm.num(stator.get_tier_value(TURBINE_MAX_EFFICIENCY)), 0.0001f);
                Assert.Equal(rpm[tier - 1], Dm.num(stator.get_tier_value(TURBINE_MAX_RPM)));
                temp = Dm.pow(temp, 1.2f);
            }
            Assert.Null(rotor.get_tier_upgrades());
            rotor.current_tier = 1;
            DmList up = (DmList)rotor.get_tier_upgrades();
            Assert.Equal((typeof(obj_item_stack_sheet_plasteel), 10f), (up["part"] as Type, Dm.num(up["amount"])));
            stator.current_tier = 3;
            up = (DmList)stator.get_tier_upgrades();
            Assert.Equal((typeof(obj_item_stack_sheet_mineral_zaukerite), 10f), (up["part"] as Type, Dm.num(up["amount"])));
        });

        // ten plasteel sheets on a tier one rotor part: one second's work, tier two, the sheets used
        var part = r.In(() => Dm.locate<obj_item_turbine_parts_rotor>(locate(8, Y + 1, 1)));
        var plasteel = WallTests.Hold<obj_item_stack_sheet_plasteel>(r, bob, 12);
        WallTests.Click(r, bob, part, 2);
        Assert.Equal(2f, r.In(() => part.current_tier));
        Assert.Equal(2f, r.In(() => Dm.num(plasteel.amount)));
        // a wrong material does nothing
        WallTests.Hold<obj_item_stack_sheet_plasteel>(r, bob, 20);
        WallTests.Click(r, bob, part, 3);
        Assert.Equal(2f, r.In(() => part.current_tier));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePanelUnlinksTheTurbineAndAMisalignedPartFailsTheLink()
    {
        var (r, bob) = Room(11106);
        var inlet = At<obj_machinery_power_turbine_inlet_compressor>(r, 4);
        var core = Core(r);

        // a screwdriver on the inlet: its panel opens, the turbine unlinks (MAINT), the window is out of service
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(4, Y + 1, 1))));
        WallTests.Click(r, bob, inlet, 1);
        r.In(() =>
        {
            Assert.True(Dm.truthy(inlet.panel_open));
            Assert.NotEqual(0f, Dm.num(Dm.bitand(inlet.machine_stat, MAINT)));
            Assert.Equal(0f, core.all_parts_connected);
            Assert.Null(inlet.rotor);
        });
        Assert.Contains("inlet_open", Overlays(r, inlet));
        Assert.Equal(0f, Dm.num(Data(r, bob)["connected"]));

        // the part out with a crowbar's secondary use, the rotor cannot link a compressor without one
        r.In(() =>
        {
            obj_item_turbine_parts part = inlet.installed_part;
            Assert.Equal(ITEM_INTERACT_SUCCESS, Dm.num(Waiting.NoSleep(inlet.crowbar_act_secondary(bob, Dm.@new<obj_item_crowbar>(get_turf(bob))))));
            Assert.Null(inlet.installed_part);
            Assert.Same(bob, part.loc);
            inlet.toggle_panel_open();
            Waiting.NoSleep(inlet.activate_parts(bob));
            Assert.False(Dm.truthy(Waiting.NoSleep(core.activate_parts(bob))));
        });

        // a compressor turned across the rotor is not aligned
        r.In(() =>
        {
            inlet.installed_part = Dm.@new<obj_item_turbine_parts_compressor>(inlet);
            Waiting.NoSleep(inlet.setDir(NORTH));
            Assert.False(Dm.truthy(Waiting.NoSleep(core.activate_parts(bob))));
            Waiting.NoSleep(inlet.setDir(EAST));
            Assert.True(Dm.truthy(Waiting.NoSleep(core.activate_parts(bob))));
            Assert.Equal(TRUE, core.all_parts_connected);
        });
        r.AssertNoRuntimes();
    }
}
