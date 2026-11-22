using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using T1 = Anomaly.Core.Tests.Tg.M3Doors.T1.T1;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 lights slice: the light fixture's emergency power (modules/power/lighting/light.dm:324-340, :516-540). An area whose
/// lighting channel goes off (tg's APC sets area.power_light and calls power_change, areas.dm:519) leaves its fixtures on the
/// emergency cell: tg's mock cell, the real miniature cell made on the first use, the red low-power light, the drain of
/// 0.0002 STANDARD_CELL_RATE per second, and the dark fixture once the cell is empty. The toy room's area never requires power,
/// so use_energy grants what charge_cell asks for (power.dm:166), which stands for the grid until the APC slice.
/// </summary>
public class EmergencyLightTests
{
    // light.dm:3 /obj/machinery/light/directional/south; the south wall of the room is its rim
    private static obj_machinery_light Lamp(TgToyRoom room, int x = 4, int y = 2)
    {
        var lamp = room.In(() => Dm.@new<obj_machinery_light_directional_south>(locate(x, y, 1)));
        room.In(() => lamp.allow_break_on_init = FALSE);
        room.In(() => { if (!Dm.eq(lamp.status, LIGHT_OK)) Waiting.NoSleep(lamp.fix()); });
        return lamp;
    }

    private static void Power(TgToyRoom room, obj_machinery_light lamp, bool on)
        => room.In(() =>
        {
            area a = (area)get_area(lamp);
            a.power_light = on ? TRUE : FALSE; // the APC's lighting channel (apc_main.dm: area.power_light = ...)
            a.power_change();
        });

    /// <summary>
    /// Steps to the end of the next <paramref name="runs"/> fires of SSmachines: each is exactly one run of every machine's process.
    /// The Master staggers the first fire of each subsystem by a random 0 to 2 s (master.dm:621), so it follows the fires,
    /// not a count of ticks; a fire is one period (its wait, 2 ticks per decisecond) after the last.
    /// </summary>
    private static void MachinesTick(TgToyRoom room, int runs = 1)
    {
        float target = room.In(() => SSmachines.times_fired) + runs;
        int limit = (runs + 1) * (room.In(() => (int)Dm.num(SSmachines.wait)) * T1.TicksPerDs + 2); // the stagger fits in the first period
        for (int ticks = 0; room.In(() => SSmachines.times_fired) < target; ticks++)
        {
            Assert.True(ticks < limit, $"SSmachines did not fire {runs} times in {limit} ticks");
            room.Step();
        }
    }

    private static float SecondsPerTick(TgToyRoom room) => room.In(() => Dm.num(SSmachines.wait) / 10);

    [Fact]
    public void ACutLightingChannelPutsTheFixtureOnTgsEmergencyLight()
    {
        var room = T1.Room();
        var lamp = Lamp(room);
        room.In(() => { Assert.True(Dm.truthy(lamp.on)); Assert.True(Dm.eq(lamp.light_range, 8)); Assert.True(Dm.truthy(lamp.has_mock_cell)); Assert.Null(lamp.cell); });
        Power(room, lamp, false);
        room.In(() =>
        {
            // update() (light.dm:232): no power but a mock cell and the switch on -> low_power_mode, IDLE_POWER_USE, processing
            Assert.False(Dm.truthy(lamp.on));
            Assert.True(Dm.truthy(lamp.low_power_mode));
            Assert.True(Dm.eq(lamp.use_power, IDLE_POWER_USE));
            Assert.Equal("tube_emergency", lamp.icon_state);
            Assert.True(Dm.truthy(lamp.has_mock_cell)); // no real cell is made before the first process
        });
        MachinesTick(room);
        float spt = SecondsPerTick(room);
        room.In(() =>
        {
            // use_emergency_power (light.dm:526) made the real cell, used LIGHT_EMERGENCY_POWER_USE * seconds_per_tick and set the light
            Assert.False(Dm.truthy(lamp.has_mock_cell));
            Assert.NotNull(lamp.cell);
            Assert.Same(lamp, lamp.cell.loc);
            Assert.Contains(lamp.cell, Dm.each_cast<atom_movable>(lamp.contents));
            Assert.IsType<obj_item_stock_parts_power_store_cell_emergency_light>(lamp.cell);
            Assert.Equal(1200f, lamp.cell.maxcharge); // STANDARD_CELL_CHARGE * 0.12
            Assert.Equal(1200f - LIGHT_EMERGENCY_POWER_USE * spt, lamp.cell.charge, 3);
            Assert.True(Dm.truthy(lamp.light_on));
            Assert.Equal(8f * 0.25f, Dm.num(lamp.light_range), 3); // brightness * bulb_low_power_brightness_mul
            Assert.Equal(Math.Max(0.5f, 0.75f * lamp.cell.charge / lamp.cell.maxcharge), Dm.num(lamp.light_power), 3);
            Assert.Equal(COLOR_VIVID_RED, lamp.light_color); // bulb_low_power_colour
            Assert.True(Dm.truthy(lamp.low_power_mode));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void TheEmergencyCellDrainsAtTgsRateAndTheFixtureGoesDarkWhenItIsEmpty()
    {
        var room = T1.Room();
        var lamp = Lamp(room);
        Power(room, lamp, false);
        MachinesTick(room);
        float spt = SecondsPerTick(room);
        float before = room.In(() => lamp.cell.charge);
        MachinesTick(room, 5);
        float after = room.In(() => lamp.cell.charge);
        // 5 runs of the process step, each LIGHT_EMERGENCY_POWER_USE * seconds_per_tick
        Assert.Equal(5 * LIGHT_EMERGENCY_POWER_USE * spt, before - after, 3);
        // the whole cell lasts maxcharge / (LIGHT_EMERGENCY_POWER_USE * seconds_per_tick) runs: ~10 minutes at SSmachines' 2 s
        float runs = 1200f / (LIGHT_EMERGENCY_POWER_USE * spt);
        Assert.InRange(runs * spt, 590f, 610f);
        // close to empty: the next runs drain it, the first run that cannot pay darkens the fixture and ends its processing
        room.In(() => lamp.cell.charge = LIGHT_EMERGENCY_POWER_USE * spt + 1);
        MachinesTick(room);
        room.In(() =>
        {
            Assert.True(Dm.truthy(lamp.low_power_mode), "one use is still paid: 1 unit left");
            Assert.Equal(1f, lamp.cell.charge, 3);
            Assert.True(Dm.truthy(lamp.light_on));
        });
        MachinesTick(room);
        room.In(() =>
        {
            Assert.False(Dm.truthy(lamp.low_power_mode)); // update(FALSE): emergency mode off
            Assert.False(Dm.truthy(lamp.on));
            Assert.True(Dm.eq(lamp.light_range, 0), "the fixture is dark");
            Assert.Equal("tube", lamp.icon_state);
            Assert.Equal(1f, lamp.cell.charge, 3); // nothing was used
            Assert.False(Dm.truthy(lamp.has_emergency_power(LIGHT_EMERGENCY_POWER_USE * spt)));
        });
        float stays = room.In(() => lamp.cell.charge);
        MachinesTick(room, 3);
        room.In(() => { Assert.True(Dm.eq(lamp.light_range, 0)); Assert.Equal(stays, lamp.cell.charge); }); // PROCESS_KILL: it no longer runs
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ARealCellDrainsToEmptyAtTgsRate()
    {
        // the whole drain, run to the end: 1200 / (2 * 2) = 300 runs of 40 ticks (light.dm:540)
        var room = T1.Room();
        var lamp = Lamp(room);
        Power(room, lamp, false);
        int runs = 0;
        while (room.In(() => Dm.truthy(lamp.low_power_mode)) && runs < 400)
        {
            MachinesTick(room);
            runs++;
        }
        float spt = SecondsPerTick(room);
        Assert.InRange(runs, 299, 303);
        room.In(() =>
        {
            Assert.True(Dm.eq(lamp.light_range, 0));
            Assert.False(Dm.truthy(lamp.on));
            Assert.True(lamp.cell.charge < LIGHT_EMERGENCY_POWER_USE * spt);
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void WhenPowerReturnsTheFixtureIsNormalAgainAndTheCellRecharges()
    {
        var room = T1.Room();
        var lamp = Lamp(room);
        Power(room, lamp, false);
        MachinesTick(room, 3);
        float spt = SecondsPerTick(room);
        float drained = room.In(() => lamp.cell.charge);
        Assert.True(drained < 1200f);
        Power(room, lamp, true);
        room.In(() =>
        {
            Assert.True(Dm.truthy(lamp.on));
            Assert.False(Dm.truthy(lamp.low_power_mode));
            Assert.Equal("tube", lamp.icon_state);
            Assert.True(Dm.eq(lamp.light_range, 8));
            Assert.True(Dm.eq(lamp.light_power, 1));
            Assert.Equal(lamp.bulb_colour, lamp.light_color);
            Assert.False(Dm.truthy(lamp.is_full_charge())); // the cell is below its maximum: the fixture keeps processing
        });
        MachinesTick(room, 2);
        float charged = room.In(() => lamp.cell.charge);
        // process (light.dm:329): charge_cell(LIGHT_EMERGENCY_POWER_USE * seconds_per_tick), granted by the area's use_energy
        Assert.Equal(drained + 2 * LIGHT_EMERGENCY_POWER_USE * spt, charged, 3);
        MachinesTick(room, 4);
        room.In(() => Assert.Equal(1200f, lamp.cell.charge, 3));
        MachinesTick(room, 2);
        room.In(() =>
        {
            Assert.True(Dm.truthy(lamp.is_full_charge()));
            Assert.Equal(1200f, lamp.cell.charge, 3); // full: PROCESS_KILL, no more charging
            Assert.True(Dm.eq(lamp.light_range, 8));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void WithoutAGridThePowerCutCellDoesNotRecharge()
    {
        // power.dm:166-170: an area that requires power but has no running APC yields no energy, so charge_cell gives nothing
        var room = T1.Room();
        var lamp = Lamp(room);
        room.In(() => ((area)get_area(lamp)).requires_power = TRUE);
        Power(room, lamp, false);
        MachinesTick(room, 3);
        float low = room.In(() => lamp.cell.charge);
        Power(room, lamp, true);
        MachinesTick(room, 3);
        room.In(() => Assert.Equal(low, lamp.cell.charge, 3));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ALightSwitchedOffByHandDoesNotUseEmergencyPowerAndAFixtureWithoutACellIsDark()
    {
        var room = T1.Room();
        var lamp = Lamp(room);
        // turned_off (light.dm:504): the area has light power but its switch is off -> no emergency light
        room.In(() => { area a = (area)get_area(lamp); a.lightswitch = FALSE; a.power_change(); });
        room.In(() => { Assert.False(Dm.truthy(lamp.low_power_mode)); Assert.True(Dm.eq(lamp.light_range, 0)); });
        room.In(() => { area a = (area)get_area(lamp); a.lightswitch = TRUE; a.power_change(); });
        room.In(() => Assert.True(Dm.truthy(lamp.on)));
        // no cell at all (light.dm:517: start_with_cell FALSE leaves has_mock_cell FALSE)
        room.In(() => lamp.has_mock_cell = FALSE);
        Power(room, lamp, false);
        room.In(() =>
        {
            Assert.False(Dm.truthy(lamp.has_emergency_power()));
            Assert.False(Dm.truthy(lamp.low_power_mode));
            Assert.True(Dm.eq(lamp.light_range, 0));
        });
        Power(room, lamp, true);
        // no_low_power (an APC with emergency lighting off sets it) refuses the mock cell too
        var lamp2 = Lamp(room, 6, 2);
        room.In(() => lamp2.no_low_power = TRUE);
        Power(room, lamp2, false);
        room.In(() => { Assert.False(Dm.truthy(lamp2.low_power_mode)); Assert.True(Dm.eq(lamp2.light_range, 0)); });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void GetCellMakesTheMiniatureCellInTheFixtureAndANaturallyDarkAreaStartsItEmpty()
    {
        var room = T1.Room();
        var lamp = Lamp(room);
        room.In(() =>
        {
            Assert.True(Dm.truthy(lamp.has_mock_cell));
            var cell = (obj_item_stock_parts_power_store)lamp.get_cell();
            Assert.False(Dm.truthy(lamp.has_mock_cell));
            Assert.Same(cell, lamp.cell);
            Assert.Same(cell, lamp.get_cell()); // made once
            Assert.Same(lamp, cell.loc);
            Assert.Equal(cell.maxcharge, cell.charge); // cell.dm:241: the area has its switch on and light power
            Assert.Equal("miniature power cell", cell.name);
        });
        // cell.dm:241 Initialize: an area whose lightswitch is off starts the cell with no charge
        var lamp2 = Lamp(room, 7, 2);
        room.In(() => ((area)get_area(lamp2)).lightswitch = FALSE);
        room.In(() => Assert.Equal(0f, ((obj_item_stock_parts_power_store)lamp2.get_cell()).charge));
        room.In(() => ((area)get_area(lamp2)).lightswitch = TRUE);
        // a cell taken out of the fixture is forgotten (Exited, light.dm:154); Destroy qdels the cell it holds (:146)
        room.In(() =>
        {
            var cell = lamp.cell;
            Waiting.NoSleep(cell.forceMove(locate(5, 3, 1)));
            Assert.Null(lamp.cell);
            var cell2 = (obj_item_stock_parts_power_store)lamp2.get_cell();
            qdel(lamp2);
            Assert.True(QDELETED(cell2));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ACellOverchargedPastTgsLimitShortCircuitsTheFixture()
    {
        var room = T1.Room();
        var lamp = Lamp(room);
        Power(room, lamp, false);
        room.In(() =>
        {
            var cell = (obj_item_stock_parts_power_store)lamp.get_cell();
            cell.maxcharge = 10000;
            cell.charge = 3500; // above 2.5 * the emergency cell's 1200
            Assert.False(Dm.truthy(Waiting.NoSleep(lamp.use_emergency_power(4))));
            Assert.True(Dm.eq(lamp.status, LIGHT_BURNED)); // burn_out
            Assert.True(Dm.eq(lamp.light_range, 0));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AnEmergencyFixtureReachesTheReplicatedLight()
    {
        // the replicator reads the atom's light vars (ReplicatedFields.WireLight); the emergency colour, range and power are those vars
        var room = T1.Room();
        var sinks = RecordingSinks.Install(room.World);
        var lamp = Lamp(room);
        Power(room, lamp, false);
        int before = sinks.Lines.Count;
        MachinesTick(room);
        Assert.True(sinks.Lines.Count > before, "use_emergency_power's set_light must reach LightChanged");
        Assert.Contains(sinks.Lines.Skip(before), l => l.EndsWith(" light " + RecordingSinks.Describe(lamp), StringComparison.Ordinal));
        T1.NoRuntimes(room);
    }
}
