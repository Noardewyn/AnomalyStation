using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 slice 2: the placeholder power source (modules/power/port_gen.dm, the PACMAN) in a booted toy room with a player: a
/// generator at (4, 4) on a run of cables (x 2-6) that is anchored with a wrench, fed plasma sheets by hand, started and tuned in
/// its window's acts, burns its fuel as tg computes it and adds power to the network at its level, heats up, hums while it runs
/// and, driven too hard, blows up.
/// </summary>
public class PortGenTests
{
    private const int Second = 20;
    private const int Y = 4;
    private const float Cycle = 20000; // power_to_energy(10 KILO JOULES): 10000 W over SSmachines' 2 seconds

    private static (AudioRoom r, mob_living_carbon_human bob) Room(Type generator = null, ulong seed = 7301)
    {
        generator ??= typeof(obj_machinery_power_port_gen_pacman_pre_loaded);
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            for (int x = 1; x <= 8; x++)
                Dm.@new<turf_open_floor_plating>(locate(x, Y, 1));
            Dm.@new<turf_open_floor_plating>(locate(4, Y + 1, 1));
            Dm.@new(generator, locate(4, Y, 1));
            for (int x = 2; x <= 6; x++)
                Dm.@new<obj_structure_cable>(locate(x, Y, 1));
        });
        mob_living_carbon_human bob = r.Join(4, Y + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static obj_machinery_power_port_gen_pacman Gen(AudioRoom r) => r.In(() => Dm.locate<obj_machinery_power_port_gen_pacman>(locate(4, Y, 1)));

    private static datum_powernet Net(AudioRoom r) => r.In(() => Dm.locate<obj_structure_cable>(locate(6, Y, 1)).powernet);

    private static void MachinesTick(AudioRoom r, int runs = 1) => r.Step(runs * r.In(() => (int)Dm.num(SSmachines.wait)) * 2);

    private static datum_tgui Window(AudioRoom r, mob who, obj_machinery_power_port_gen_pacman gen) => r.In(() => Dm.@new<datum_tgui>(who, gen, "PortableGenerator", "PACMAN"));

    private static bool Act(AudioRoom r, obj_machinery_power_port_gen_pacman gen, datum_tgui ui, string action)
        => r.In(() => Dm.truthy(Waiting.NoSleep(gen.ui_act(action, Dm.list(), ui, null))));

    // an examine's lines: a list, or the text the lines concatenate to when the parent's examine gave nothing (DM's null + text)
    private static string Said(object examined) => examined is DmList l ? string.Join(" ", Dm.each(l).Select(Dm.str)) : Dm.str(examined);

    private static void Anchor(AudioRoom r, mob_living_carbon_human bob)
    {
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, Gen(r), 0.5f);
    }

    private static List<HeardSound> Hum(AudioRoom r)
        => r.Sounds.Heard.Where(h => h.File != null && h.File.StartsWith("sound/machines/generator/", StringComparison.Ordinal)).ToList();

    [Fact]
    public void AWrenchAnchorsTheGeneratorOnTheCableAndItJoinsTheNetwork()
    {
        var (r, bob) = Room();
        r.In(() =>
        {
            obj_machinery_power_port_gen_pacman gen = Gen(r);
            Assert.False(Dm.truthy(gen.anchored)); // port_gen.dm:11
            Assert.Null(gen.powernet);
            Assert.Contains("P.A.C.M.A.N.-type portable generator", gen.name);
            Assert.Equal("plasma", gen.sheet_name.ToLowerInvariant().Replace("solid ", "")); // initial(sheet_path.name) = "solid plasma"
            Assert.Equal(15f, gen.sheets);
        });
        Anchor(r, bob);
        r.In(() =>
        {
            obj_machinery_power_port_gen_pacman gen = Gen(r);
            Assert.True(Dm.truthy(gen.anchored));
            Assert.Same(Net(r), gen.powernet); // set_anchored (port_gen.dm:177) -> connect_to_network
            Assert.Equal(1f, Dm.num(gen.should_have_node()));
        });
        // the wrench again takes it off the floor and off the network (port_gen.dm:207)
        WallTests.Click(r, bob, Gen(r), 0.5f);
        r.In(() =>
        {
            Assert.False(Dm.truthy(Gen(r).anchored));
            Assert.Null(Gen(r).powernet);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePacmanBurnsItsSheetsAndAddsPowerAtItsLevelWhileItHeatsUp()
    {
        var (r, bob) = Room();
        Anchor(r, bob);
        obj_machinery_power_port_gen_pacman gen = Gen(r);
        datum_tgui ui = Window(r, bob, gen);
        r.In(() =>
        {
            var data = (DmList)Waiting.NoSleep(gen.ui_data(bob));
            Assert.Equal(0f, Dm.num(data["active"]));
            Assert.Equal(1f, Dm.num(data["ready_to_boot"])); // anchored and fuel
            Assert.Equal(15f, Dm.num(data["sheets"]));
            Assert.Equal(1f, Dm.num(data["connected"]));
            Assert.Equal("10 kW", Dm.str(data["power_generated"]));
        });

        // toggle_power starts it (port_gen.dm:49): processing, the appearance, the hum
        Assert.True(Act(r, gen, ui, "toggle_power"));
        r.In(() =>
        {
            Assert.True(Dm.truthy(gen.active));
            Assert.Equal("portgen0_1", gen.icon_state);
            Assert.True(Dm.truthy(Dm.bitand(gen.datum_flags, DF_ISPROCESSING)));
        });
        MachinesTick(r, 4);
        r.In(() =>
        {
            // process (port_gen.dm:71): add_avail(power_to_energy(power_gen * power_output)) each cycle, the network's after the reset
            Assert.Equal(Cycle, Net(r).avail);
            // UseFuel (port_gen.dm:139): a sheet lasts time_per_sheet = 180 passes at level 1; the first pass opens a sheet
            float passes = gen.current_heat / 3; // while the heat is under its limit it rises 3 a pass (:157)
            Assert.InRange(passes, 3, 4);
            Assert.Equal(15 - passes / 180, Dm.num(gen.sheets) + gen.sheet_left, 4);
            Assert.Equal(14f, gen.sheets);
            var data = (DmList)Waiting.NoSleep(gen.ui_data(bob));
            Assert.Equal(1f, Dm.num(data["active"]));
            Assert.Equal(Dm.num(gen.current_heat), Dm.num(data["current_heat"]));
            Assert.Equal("10 kW", Dm.str(data["power_available"])); // avail() is 20 kJ a cycle: 10 kW over SSmachines'' 2 s
        });

        // the level: higher_power up to 4, then no more without the emag (port_gen.dm:262); the power follows the level
        Assert.True(Act(r, gen, ui, "higher_power"));
        Assert.True(Act(r, gen, ui, "higher_power"));
        Assert.True(Act(r, gen, ui, "higher_power"));
        Assert.False(Act(r, gen, ui, "higher_power"));
        r.In(() => Assert.Equal(4f, gen.power_output));
        MachinesTick(r, 2);
        r.In(() => Assert.Equal(4 * Cycle, Net(r).avail));
        Assert.True(Act(r, gen, ui, "lower_power"));
        Assert.True(Act(r, gen, ui, "lower_power"));
        Assert.True(Act(r, gen, ui, "lower_power"));
        Assert.False(Act(r, gen, ui, "lower_power"));
        r.In(() => Assert.Equal(1f, gen.power_output));

        // eject does nothing while it runs
        Assert.False(Act(r, gen, ui, "eject"));
        // and the hum started with it
        Assert.NotEmpty(Hum(r));
        // switching off stops processing's work: no more power after the reset, the icon back
        Assert.True(Act(r, gen, ui, "toggle_power"));
        MachinesTick(r, 2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(gen.active));
            Assert.Equal("portgen0_0", gen.icon_state);
            Assert.Equal(0f, Net(r).avail);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGeneratorHumsWhileItRunsAndTheHeatSettlesBackWhenItStops()
    {
        var (r, bob) = Room(seed: 7302);
        Anchor(r, bob);
        obj_machinery_power_port_gen_pacman gen = Gen(r);
        datum_tgui ui = Window(r, bob, gen);
        r.Sounds.Heard.Clear();
        Assert.True(Act(r, gen, ui, "toggle_power"));
        r.Step(3 * Second);
        List<HeardSound> hum = Hum(r).Where(h => ReferenceEquals(h.Target, bob)).ToList();
        Assert.Contains(hum, h => h.File == "sound/machines/generator/generator_start.ogg");
        Assert.Contains(hum, h => h.File.StartsWith("sound/machines/generator/generator_mid", StringComparison.Ordinal));
        r.Sounds.Heard.Clear();
        Assert.True(Act(r, gen, ui, "toggle_power"));
        r.Step(Second);
        Assert.Contains(Hum(r), h => h.File == "sound/machines/generator/generator_end.ogg");
        // handleInactive (port_gen.dm:169): 2 degrees a pass until it is cold, then it stops being processed
        float hot = r.In(() => gen.current_heat);
        Assert.True(hot > 0);
        MachinesTick(r, (int)Math.Ceiling(hot / 2) + 2);
        r.In(() =>
        {
            Assert.Equal(0f, gen.current_heat);
            Assert.False(Dm.truthy(Dm.bitand(gen.datum_flags, DF_ISPROCESSING)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void PlasmaSheetsGoInByHandUpToTheTankAndTheEjectGivesThemBack()
    {
        var (r, bob) = Room(typeof(obj_machinery_power_port_gen_pacman));
        Anchor(r, bob);
        obj_machinery_power_port_gen_pacman gen = Gen(r);
        datum_tgui ui = Window(r, bob, gen);
        r.In(() => Assert.False(Dm.truthy(gen.HasFuel()))); // nothing to burn: it cannot start (port_gen.dm:129)
        Assert.False(Act(r, gen, ui, "toggle_power") && r.In(() => Dm.truthy(gen.active)));
        r.In(() => Assert.False(Dm.truthy(gen.active)));

        // the sheets: item_interaction (port_gen.dm:186) takes what fits under max_sheets = 50
        obj_item_stack_sheet_mineral_plasma stack = WallTests.Hold<obj_item_stack_sheet_mineral_plasma>(r, bob, 30);
        WallTests.Click(r, bob, gen, 0.5f);
        r.In(() =>
        {
            Assert.Equal(30f, gen.sheets);
            Assert.True(QDELETED(stack));
            Assert.True(Dm.truthy(gen.HasFuel()));
        });
        stack = WallTests.Hold<obj_item_stack_sheet_mineral_plasma>(r, bob, 30);
        WallTests.Click(r, bob, gen, 0.5f);
        r.In(() =>
        {
            Assert.Equal(50f, gen.sheets);
            Assert.Equal(10f, Dm.num(stack.amount)); // the rest stays in the hand
        });
        WallTests.Click(r, bob, gen, 0.5f); // full: the stack is left alone
        r.In(() =>
        {
            Assert.Equal(50f, gen.sheets);
            Assert.Equal(10f, Dm.num(stack.amount));
        });
        r.In(() => Assert.Contains("has 50 units of", Said(Waiting.NoSleep(gen.examine(bob)))));

        // eject (port_gen.dm:262) drops them in one stack where the generator stands
        Assert.True(Act(r, gen, ui, "eject"));
        r.In(() =>
        {
            Assert.Equal(0f, gen.sheets);
            var dropped = Dm.locate<obj_item_stack_sheet_mineral_plasma>(locate(4, Y, 1));
            Assert.NotNull(dropped);
            Assert.Equal(50f, Dm.num(dropped.amount));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGeneratorThatRunsOutOfFuelSwitchesItselfOff()
    {
        var (r, bob) = Room(typeof(obj_machinery_power_port_gen_pacman), seed: 7303);
        Anchor(r, bob);
        r.In(() =>
        {
            obj_machinery_power_port_gen_pacman gen = Gen(r);
            // a sheet's last crumb, below what one pass burns (1 / 180): it is on, and the next pass finds no fuel
            gen.sheet_left = 0.005f;
            gen.active = TRUE;
            START_PROCESSING(SSmachines, gen);
            gen.update_appearance();
            gen.soundloop.start();
        });
        MachinesTick(r, 2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(Gen(r).active)); // process (port_gen.dm:71): !HasFuel() -> TogglePower()
            Assert.Equal("portgen0_0", Gen(r).icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADrivenTooHardGeneratorOverheatsAndExplodes()
    {
        var (r, bob) = Room(seed: 7304);
        Anchor(r, bob);
        obj_machinery_power_port_gen_pacman gen = Gen(r);
        datum_tgui ui = Window(r, bob, gen);
        r.In(() =>
        {
            gen.power_output = 5; // the emag's levels (port_gen.dm:225)
            Dm.combine(ref gen.obj_flags, EMAGGED);
            gen.current_heat = 399;
        });
        Assert.True(Act(r, gen, ui, "toggle_power"));
        MachinesTick(r, 2);
        r.In(() => Assert.True(QDELETED(gen))); // current_heat > 300 -> overheat() -> explosion, qdel (port_gen.dm:164)
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEmagUnlocksTheHigherLevelsAndTheBoardPicksUraniumOrPlasma()
    {
        var (r, bob) = Room(seed: 7305);
        obj_machinery_power_port_gen_pacman gen = Gen(r);
        datum_tgui ui = Window(r, bob, gen);
        r.In(() =>
        {
            gen.power_output = 4;
            Assert.False(Dm.truthy(Waiting.NoSleep(gen.ui_act("higher_power", Dm.list(), ui, null))));
            var emag = Dm.@new<obj_item_card_emag>(get_turf(bob));
            Assert.True(Dm.truthy(Waiting.NoSleep(gen.emag_act(bob, emag))));
            Assert.True(Dm.truthy(Dm.bitand(gen.obj_flags, EMAGGED)));
            Assert.False(Dm.truthy(Waiting.NoSleep(gen.emag_act(bob, emag)))); // once
            Assert.True(Dm.truthy(Waiting.NoSleep(gen.ui_act("higher_power", Dm.list(), ui, null))));
            Assert.Equal(5f, gen.power_output);

            // the board's mode (machine_circuitboards.dm:302-312) and what the finished machine takes from it (port_gen.dm:113)
            var board = (obj_item_circuitboard_machine_pacman)gen.circuit_value;
            Assert.NotNull(board);
            Assert.False(Dm.truthy(board.high_production_profile));
            Waiting.NoSleep(board.screwdriver_act(bob, Dm.@new<obj_item_screwdriver>(get_turf(bob))));
            Assert.True(Dm.truthy(board.high_production_profile));
            Assert.Contains("high-power uranium mode", Said(Waiting.NoSleep(board.examine(bob))));
            Waiting.NoSleep(gen.on_construction(bob));
            Assert.Equal("portgen1_0", gen.icon_state);
            Assert.Equal(20f, gen.max_sheets);
            Assert.Equal(60f, gen.time_per_sheet);
            Assert.Equal(30000f, gen.power_gen);
            Assert.Equal(typeof(obj_item_stack_sheet_mineral_uranium), gen.sheet_path);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheScrewdriverOpensThePanelAndTheCrowbarTakesTheStoppedGeneratorApartDroppingItsFuel()
    {
        var (r, bob) = Room(seed: 7306);
        obj_machinery_power_port_gen_pacman gen = Gen(r);
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        WallTests.Click(r, bob, gen, 0.5f);
        r.In(() => Assert.True(Dm.truthy(gen.panel_open)));
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, gen, 1);
        r.In(() =>
        {
            Assert.True(QDELETED(gen));
            var fuel = Dm.locate<obj_item_stack_sheet_mineral_plasma>(locate(4, Y, 1)); // on_deconstruction: DropFuel (port_gen.dm:109)
            Assert.NotNull(fuel);
            Assert.Equal(15f, Dm.num(fuel.amount));
            Assert.NotNull(Dm.locate<obj_item_circuitboard_machine_pacman>(locate(4, Y, 1)));
        });
        r.AssertNoRuntimes();
    }
}
