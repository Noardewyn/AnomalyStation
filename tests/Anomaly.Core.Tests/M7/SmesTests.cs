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
/// M7 slice 2: the power storage (modules/power/smes.dm, battery.dm, powernet.dm:87) in a booted toy room with a player: a SMES
/// at (4, 4) with its terminal at (3, 4) facing it, the terminal's cable run (the input network, x 2-3) and the SMES's own run
/// (the output network, x 4-7). The SMES takes its megacells and its terminal, outputs to its network at its output level, charges
/// from the surplus behind its terminal, and gets back what the network did not use (restore, in the powernet's reset). The
/// window's acts, the tools, the terminal's construction and the EMP follow tg's code.
/// </summary>
public class SmesTests
{
    private const int Second = 20;
    private const int Y = 4;
    private const float Cycle = 100000; // power_to_energy(50 KILO WATTS): 50000 W over SSmachines' 2 seconds

    private static (AudioRoom r, mob_living_carbon_human bob) Room(Type smesType = null, ulong seed = 7201, bool withTerminal = true)
    {
        smesType ??= typeof(obj_machinery_power_smes_full);
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            for (int x = 1; x <= 8; x++)
                Dm.@new<turf_open_floor_plating>(locate(x, Y, 1));
            Dm.@new<turf_open_floor_plating>(locate(3, Y + 1, 1));
            Dm.@new<turf_open_floor_plating>(locate(4, Y + 1, 1));
            // the terminal and the SMES before the cables: a cable under them reads them when it links (cable.dm:88)
            if (withTerminal)
                Dm.@new<obj_machinery_power_terminal>(locate(3, Y, 1)).dir = EAST;
            Dm.@new(smesType, locate(4, Y, 1));
            for (int x = 2; x <= 7; x++)
                Dm.@new<obj_structure_cable>(locate(x, Y, 1));
        });
        mob_living_carbon_human bob = r.Join(4, Y + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        // The first cycles of a round: SSmachines resets the networks and then runs the machines, so a unit's output of one cycle is the
        // network's power of the next and the unit takes back its unused output one reset later. Two outputs are in flight for good, and
        // the unit's charge is then steady (full less two outputs, with no load): the tests start from there.
        MachinesTick(r, 4);
        return (r, bob);
    }

    private static obj_machinery_power_smes Smes(AudioRoom r) => r.In(() => Dm.locate<obj_machinery_power_smes>(locate(4, Y, 1)));

    private static obj_machinery_power_terminal Terminal(AudioRoom r) => r.In(() => Dm.locate<obj_machinery_power_terminal>(locate(3, Y, 1)));

    private static obj_structure_cable Cable(AudioRoom r, int x) => r.In(() => Dm.locate<obj_structure_cable>(locate(x, Y, 1)));

    private static datum_powernet Out(AudioRoom r) => r.In(() => Cable(r, 6).powernet);

    private static datum_powernet In(AudioRoom r) => r.In(() => Cable(r, 2).powernet);

    private static void MachinesTick(AudioRoom r, int runs = 1) => r.Step(runs * r.In(() => (int)Dm.num(SSmachines.wait)) * 2);

    private static float Charge(AudioRoom r) => r.In(() => Dm.num(Smes(r).total_charge()));

    private static List<string> Overlays(AudioRoom r, atom a)
        => r.In(() => Dm.each(a.managed_overlays is DmList l ? l : Dm.list(a.managed_overlays))
            .Select(o => o switch { string t => t, mutable_appearance m => (string)m.icon_state, DmAppearance d => (string)d.icon_state, _ => null }).Where(t => t != null).ToList());

    private static datum_tgui Window(AudioRoom r, mob who, obj_machinery_power_smes smes) => r.In(() => Dm.@new<datum_tgui>(who, smes, "Smes", "SMES"));

    [Fact]
    public void ASmesTakesItsMegacellsAndItsTerminalAndStartsCharged()
    {
        var (r, _) = Room();
        r.In(() =>
        {
            obj_machinery_power_smes smes = Smes(r);
            obj_machinery_power_terminal terminal = Terminal(r);
            // Initialize (smes.dm:47): the terminal in front of the unit that faces it, linked both ways
            Assert.Same(terminal, smes.terminal);
            Assert.Same(smes, terminal.master);
            Assert.False(Dm.truthy(Dm.bitand(smes.machine_stat, BROKEN)));
            // the board's default parts (circuitboard.dm:141): five high-capacity megacells and a capacitor, refreshed (smes.dm:192)
            var cells = Dm.each<obj_item_stock_parts_power_store_battery_high_empty>(smes.component_parts).ToList();
            Assert.Equal(5, cells.Count);
            var capacitor = Assert.Single(Dm.each<datum_stock_part_capacitor>(smes.component_parts));
            Assert.Equal(5 * 10 * STANDARD_BATTERY_CHARGE, smes.total_capacity);
            Assert.Equal(200000 * Dm.num(capacitor.tier), smes.input_level_max);
            Assert.Equal(200000 * Dm.num(capacitor.tier), smes.output_level_max);
            // "full" starts with 50 standard batteries, which the empty cells take in order (smes.dm:55) and then forgets
            Assert.Equal(0f, smes.charge);
            Assert.Equal(50 * STANDARD_BATTERY_CHARGE - 2 * Cycle, Dm.num(smes.total_charge())); // two outputs are in flight (see Room)
            Assert.Equal(50 * STANDARD_BATTERY_CHARGE - 2 * Cycle, cells.Sum(c => c.charge));
            Assert.Equal(4f, Dm.num(smes.chargedisplay())); // round(5 * 0.996) is DM's floor
            // each side on its own network: the unit's cable does not link to its terminal's (cable.dm:96)
            Assert.NotSame(In(r), Out(r));
            Assert.Same(Out(r), smes.powernet);
            Assert.Same(In(r), terminal.powernet);
            Assert.Equal(4f, Out(r).cables.len);
            Assert.Equal(2f, In(r).cables.len);
            // the display lights: output, input and the charge level (smes.dm:140)
            Assert.Equal(new[] { "smes-op1", "smes-oc0", "smes-og4" }, Overlays(r, smes));
            Assert.Equal("smes", smes.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEngineeringUnitStartsAtNinetyKilowattsAndAPlainUnitStartsEmpty()
    {
        var (r, _) = Room(typeof(obj_machinery_power_smes_engineering));
        r.In(() =>
        {
            Assert.Equal(90000f, Smes(r).output_level);
            Assert.Equal(50 * STANDARD_BATTERY_CHARGE - 2 * 180000, Dm.num(Smes(r).total_charge())); // two outputs of 90 kW over 2 s in flight
        });
        var (empty, _) = Room(typeof(obj_machinery_power_smes), seed: 7202);
        empty.In(() =>
        {
            Assert.Equal(0f, Dm.num(Smes(empty).total_charge()));
            Assert.Equal(0f, Dm.num(Smes(empty).chargedisplay()));
            Assert.Equal(new[] { "smes-op0", "smes-oc0" }, Overlays(empty, Smes(empty))); // nothing to output, nothing to take
        });
        r.AssertNoRuntimes();
        empty.AssertNoRuntimes();
    }

    [Fact]
    public void ASmesWithoutATerminalBreaksAtInitAndRunsNothing()
    {
        var (r, _) = Room(withTerminal: false);
        r.In(() =>
        {
            Assert.Null(Smes(r).terminal);
            Assert.True(Dm.truthy(Dm.bitand(Smes(r).machine_stat, BROKEN))); // atom_break (smes.dm:70)
        });
        MachinesTick(r, 3);
        r.In(() => Assert.Equal(0f, Out(r).avail));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AChargedSmesOutputsToItsNetworkAtItsOutputLevelFromItsCells()
    {
        var (r, _) = Room();
        float before = Charge(r);
        MachinesTick(r);
        r.In(() =>
        {
            obj_machinery_power_smes smes = Smes(r);
            // process (smes.dm:351): power_to_energy(output_level) leaves the cells and becomes the network's power at the next reset
            Assert.Equal(Cycle, Out(r).avail);
            Assert.Equal(Cycle, smes.output_used);
            Assert.Equal(1f, smes.outputting);
            Assert.Equal(Cycle, Dm.num(smes.avail()));
            // the input side has none: the terminal's network stays dark and the unit does not charge (nothing to take)
            Assert.Equal(0f, In(r).avail);
            Assert.Equal(0f, smes.inputting);
        });
        // nothing used the power: the reset gave the last output back (restore) as the new one left
        Assert.Equal(before, Charge(r), 8);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASmesChargesFromTheSurplusBehindItsTerminalAndAddsItsLoad()
    {
        var (r, _) = Room(typeof(obj_machinery_power_smes));
        r.In(() => Smes(r).output_attempt = FALSE); // an empty unit would put what it takes straight out again
        MachinesTick(r, 2);
        // 150 kJ of new power on the input network each cycle (a generator's): the unit takes its input level's worth (100 kJ) of
        // what the network has after the reset
        for (int i = 0; i < 4; i++)
        {
            r.In(() => Cable(r, 2).add_avail(150000));
            MachinesTick(r);
        }
        r.In(() =>
        {
            obj_machinery_power_smes smes = Smes(r);
            Assert.Equal(4 * 100000f, Dm.num(smes.total_charge()));
            Assert.Equal(1f, smes.inputting);
            Assert.Equal(150000f, smes.input_available);
            // terminal.add_load(load): the unit's draw is the network's load of the cycle, so the reset (powernet.dm:84) finds what
            // is left of the 150 kJ: 50 kJ
            Assert.Equal(150000f, In(r).avail);
            Assert.Equal(50000f, In(r).netexcess);
            Assert.Equal(new[] { "smes-op0", "smes-oc1" }, Overlays(r, smes)); // output off; 400 kJ of 50 MJ: no charge light yet
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheUnusedOutputGoesBackIntoTheCellsAtTheResetAsTgComputesIt()
    {
        var (r, _) = Room();
        float start = Charge(r);
        const float load = 30000;
        // a machine's 30 kJ drawn each cycle: the reset finds the last output as avail and the load against it, and the unit takes back
        // what was not used (restore: min(output_used, netexcess) with netexcess = avail - load, smes.dm:409, powernet.dm:84), so each
        // cycle costs the cells the load and no more
        for (int i = 0; i < 4; i++)
        {
            r.In(() => Cable(r, 6).add_load(load));
            MachinesTick(r);
            r.In(() =>
            {
                Assert.Equal(0f, Out(r).netexcess); // restore took the whole excess (smes.dm:424)
                Assert.Equal(Cycle, Out(r).avail);
            });
            Assert.Equal(start - (i + 1) * load, Charge(r), 8);
        }
        r.In(() => Assert.Equal(Cycle, Smes(r).output_used)); // this cycle's output, taken after the reset gave back the last one
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASmesWithNoLoadTakesAllItsOutputBackAndKeepsItsCharge()
    {
        var (r, _) = Room();
        float start = Charge(r);
        MachinesTick(r, 5);
        Assert.Equal(start, Charge(r), 8);
        r.In(() => Assert.Equal(Cycle, Out(r).avail));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWindowsActsSwitchAndSetTheInputAndOutputWithinTheirLimits()
    {
        var (r, bob) = Room();
        obj_machinery_power_smes smes = Smes(r);
        datum_tgui ui = Window(r, bob, smes);
        r.In(() =>
        {
            float outMax = smes.output_level_max, inMax = smes.input_level_max;
            // ui_static_data and ui_data (smes.dm:436, :443)
            var stat = (DmList)Waiting.NoSleep(smes.ui_static_data(bob));
            Assert.Equal(smes.total_capacity, Dm.num(stat["capacity"]));
            Assert.Equal(inMax, Dm.num(stat["inputLevelMax"]));
            Assert.Equal(outMax, Dm.num(stat["outputLevelMax"]));
            var data = (DmList)Waiting.NoSleep(smes.ui_data(bob));
            Assert.Equal(50 * STANDARD_BATTERY_CHARGE - 2 * Cycle, Dm.num(data["charge"]));
            Assert.Equal(50000f, Dm.num(data["inputLevel"]));
            Assert.Equal(50000f, Dm.num(data["outputLevel"]));
            Assert.Equal(1f, Dm.num(data["inputAttempt"]));
            Assert.Equal(1f, Dm.num(data["outputAttempt"]));

            // the targets: min, max, an adjustment, a number, and a number past the limit clamps
            Act(smes, ui, "output", Dm.list(Dm.kv("target", "min")));
            Assert.Equal(0f, smes.output_level);
            Act(smes, ui, "output", Dm.list(Dm.kv("target", "max")));
            Assert.Equal(outMax, smes.output_level);
            Act(smes, ui, "output", Dm.list(Dm.kv("target", "min")));
            Act(smes, ui, "output", Dm.list(Dm.kv("adjust", 10000)));
            Assert.Equal(10000f, smes.output_level);
            Act(smes, ui, "output", Dm.list(Dm.kv("adjust", -30000)));
            Assert.Equal(0f, smes.output_level);
            Act(smes, ui, "output", Dm.list(Dm.kv("target", 90000)));
            Assert.Equal(90000f, smes.output_level);
            Act(smes, ui, "output", Dm.list(Dm.kv("target", outMax * 10)));
            Assert.Equal(outMax, smes.output_level);
            Act(smes, ui, "input", Dm.list(Dm.kv("target", 120000)));
            Assert.Equal(120000f, smes.input_level);
            Act(smes, ui, "input", Dm.list(Dm.kv("target", "min")));
            Assert.Equal(0f, smes.input_level);
            Act(smes, ui, "input", Dm.list(Dm.kv("target", "max")));
            Assert.Equal(inMax, smes.input_level);
            // an act with neither target nor adjust changes nothing and is not handled
            Assert.False(Dm.truthy(Waiting.NoSleep(smes.ui_act("output", Dm.list(), ui, null))));

            // tryinput and tryoutput flip the attempts and redraw the lights
            Assert.True(Dm.truthy(Waiting.NoSleep(smes.ui_act("tryoutput", Dm.list(), ui, null))));
            Assert.Equal(0f, smes.output_attempt);
            Assert.True(Dm.truthy(Waiting.NoSleep(smes.ui_act("tryinput", Dm.list(), ui, null))));
            Assert.Equal(0f, smes.input_attempt);
        });
        r.AssertNoRuntimes();
    }

    private static void Act(obj_machinery_power_smes smes, datum_tgui ui, string action, DmList parameters)
        => Assert.True(Dm.truthy(Waiting.NoSleep(smes.ui_act(action, parameters, ui, null))), action);

    [Fact]
    public void TurningTheOutputOffStopsTheSmesFeedingItsNetwork()
    {
        var (r, bob) = Room();
        r.In(() => Assert.Equal(Cycle, Out(r).avail));
        datum_tgui ui = Window(r, bob, Smes(r));
        float before = Charge(r);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(Smes(r).ui_act("tryoutput", Dm.list(), ui, null)))));
        MachinesTick(r, 2);
        r.In(() =>
        {
            obj_machinery_power_smes smes = Smes(r);
            Assert.Equal(0f, Out(r).avail);
            Assert.Equal(0f, smes.outputting);
            Assert.Equal(new[] { "smes-op0", "smes-oc0", "smes-og4" }, Overlays(r, smes));
        });
        // the one output in flight at the switch came back at the next reset (the unit was still outputting then); no new one left
        Assert.Equal(before + Cycle, Charge(r), 8);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ScrewdriverWrenchWirecuttersCoilAndCrowbarWorkTheUnitAndItsTerminal()
    {
        var (r, bob) = Room();
        obj_machinery_power_smes smes = Smes(r);
        obj_machinery_power_terminal terminal = Terminal(r);

        // the panel: a screwdriver opens it, the icon changes and the lights go (smes.dm:140, :292)
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        WallTests.Click(r, bob, smes, 0.5f);
        r.In(() =>
        {
            Assert.True(Dm.truthy(smes.panel_open));
            Assert.Equal("smes-o", smes.icon_state);
            Assert.Empty(Overlays(r, smes));
            Assert.True(Dm.truthy(smes.can_terminal_dismantle()));
        });

        // the crowbar refuses while the terminal is there (smes.dm:306)
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, smes, 0.5f);
        r.In(() => Assert.False(QDELETED(smes)));

        // the wrench turns the unit and it finds the terminal again on the way round (smes.dm:320): panel open, one turn
        // from south to west, where the terminal stands
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, smes, 0.5f);
        r.In(() =>
        {
            Assert.Equal(WEST, smes.dir);
            Assert.Same(terminal, smes.terminal);
            Assert.Same(smes, terminal.master);
            Assert.False(Dm.truthy(Dm.bitand(smes.machine_stat, BROKEN)));
        });

        // wirecutters cut the terminal off (terminal.dm:49): the unit breaks and has none
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(3, Y + 1, 1))));
        WallTests.Hold<obj_item_wirecutters>(r, bob);
        WallTests.Click(r, bob, smes, 6);
        r.In(() =>
        {
            Assert.True(QDELETED(terminal));
            Assert.Null(smes.terminal);
            Assert.True(Dm.truthy(Dm.bitand(smes.machine_stat, BROKEN))); // disconnect_terminal (smes.dm:89)
            Assert.Equal(10f, Dm.num(Dm.locate<obj_item_stack_cable_coil>(locate(3, Y, 1)).amount));
        });

        // ten lengths of cable on the unit from the terminal's tile build a new terminal facing it (smes.dm:245)
        r.In(() =>
        {
            qdel(Dm.locate<obj_item_stack_cable_coil>(locate(3, Y, 1)));
            Waiting.NoSleep(bob.forceMove(locate(3, Y, 1)));
        });
        WallTests.Hold<obj_item_stack_cable_coil>(r, bob, 10);
        WallTests.Click(r, bob, smes, 4);
        r.In(() =>
        {
            obj_machinery_power_terminal built = Terminal(r);
            Assert.NotNull(built);
            Assert.Equal(EAST, built.dir); // get_dir(turf, src)
            Assert.Same(built, smes.terminal);
            Assert.Same(smes, built.master);
            Assert.False(Dm.truthy(Dm.bitand(smes.machine_stat, BROKEN)));
            Assert.NotNull(built.powernet);
            Assert.Same(In(r), built.powernet);
        });

        // the terminal gone again, the crowbar takes the open unit apart (smes.dm:305): its cells, board and capacitor come out
        r.In(() =>
        {
            qdel(Terminal(r));
            Waiting.NoSleep(bob.forceMove(locate(4, Y + 1, 1)));
        });
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, smes, 1);
        r.In(() =>
        {
            Assert.True(QDELETED(smes));
            Assert.Equal(5, Dm.each<obj_item_stock_parts_power_store_battery_high_empty>(locate(4, Y, 1).contents).Count());
            Assert.NotNull(Dm.locate<obj_item_circuitboard_machine_smes>(locate(4, Y, 1)));
            Assert.NotNull(Dm.locate<obj_structure_frame_machine>(locate(4, Y, 1)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmpRandomisesTheSmesAndDrainsItsCells()
    {
        var (r, _) = Room();
        float before = Charge(r);
        r.In(() => Waiting.NoSleep(Smes(r).emp_act(1)));
        r.In(() =>
        {
            obj_machinery_power_smes smes = Smes(r);
            Assert.Equal(before - STANDARD_BATTERY_CHARGE, Dm.num(smes.total_charge()), 8); // STANDARD_BATTERY_CHARGE / severity (smes.dm:530)
            Assert.InRange(smes.input_level, 0, smes.input_level_max);
            Assert.InRange(smes.output_level, 0, smes.output_level_max);
            Assert.Equal(smes.input_attempt, smes.inputting);
            Assert.Equal(smes.output_attempt, smes.outputting);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMagicalSmesGivesChargeWithoutConsumingAnything()
    {
        var (r, _) = Room(typeof(obj_machinery_power_smes_magical));
        r.In(() =>
        {
            obj_machinery_power_smes smes = Smes(r);
            Assert.Equal(5000f, Dm.num(Waiting.NoSleep(smes.adjust_charge(-5000)))); // smes.dm:556
            Assert.Equal(0f, Dm.num(Waiting.NoSleep(smes.adjust_charge(5000))));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRechargeDevCommandFillsTheUnitsAtAPlaceOrEverywhere()
    {
        var (r, _) = Room(typeof(obj_machinery_power_smes));
        r.In(() =>
        {
            Assert.Equal(0f, Dm.num(Smes(r).total_charge()));
            Assert.Equal("recharge: no power storage unit at (9,9,1)", DevCommands.Recharge(locate(9, 9, 1)));
            string said = DevCommands.Recharge(locate(4, Y, 1));
            Assert.Equal("recharge: 1 power storage unit at (4,4,1) full, 50 MJ stored", said);
            Assert.Equal(Smes(r).total_capacity, Dm.num(Smes(r).total_charge()));
            Assert.Equal(new[] { "smes-op0", "smes-oc0", "smes-og5" }, Overlays(r, Smes(r))); // the charge light follows, the rest waits for a cycle
            Waiting.NoSleep(Smes(r).adjust_charge(-30000000));
            Assert.Equal("recharge: 1 power storage unit everywhere full, 50 MJ stored", DevCommands.Recharge(null));
            Assert.Equal(Smes(r).total_capacity, Dm.num(Smes(r).total_charge()));
        });
        r.AssertNoRuntimes();
    }
}
