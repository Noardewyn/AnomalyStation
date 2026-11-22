using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 power monitor slice (modules/power/monitor.dm, game/machinery/computer/_computer.dm) in a booted toy room whose area needs
/// power: an APC at (3, 2) with its cable run along y 2 and a power source at the end of it, a power monitoring console on a cable
/// (it reads that cable's network) and another one off the cable (it reads the network of its area's APC through the terminal).
/// </summary>
public class PowerMonitorTests
{
    private const int Second = 20;
    private const int ApcX = 3, Y = 2, OnCableX = 6, SourceX = 7, OffCableX = 5, OffCableY = 4;

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_power_apc Apc, obj_machinery_computer_monitor OnCable,
        obj_machinery_computer_monitor OffCable, obj_machinery_power_test_m7apc_source Source);

    private static Rig Build(ulong seed)
    {
        obj_machinery_power_apc apc = null;
        obj_machinery_computer_monitor onCable = null, offCable = null;
        obj_machinery_power_test_m7apc_source source = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).requires_power = TRUE;
            for (int x = 2; x <= SourceX; x++)
                Dm.@new<obj_structure_cable>(locate(x, Y, 1));
            apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(ApcX, Y, 1));
            source = Dm.@new<obj_machinery_power_test_m7apc_source>(locate(SourceX, Y, 1));
            onCable = Dm.@new<obj_machinery_computer_monitor>(locate(OnCableX, Y, 1));
            offCable = Dm.@new<obj_machinery_computer_monitor>(locate(OffCableX, OffCableY, 1));
        });
        mob_living_carbon_human bob = r.Join(ApcX, Y + 1, "Bob");
        r.Step(Second);
        return new Rig(r, bob, apc, onCable, offCable, source);
    }

    private static void MachinesTick(AudioRoom r, int runs = 1) => r.Step(runs * r.In(() => (int)Dm.num(SSmachines.wait)) * 2);

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o switch
    {
        string t => t,
        mutable_appearance m => m.icon_state,
        DmAppearance d => d.icon_state,
        _ => null,
    }).Where(s => s != null).ToList());

    [Fact]
    public void AConsoleOnACableReadsThatCablesNetworkAndOneOffTheCableReadsItsAreasApc()
    {
        var g = Build(7801);
        g.R.In(() =>
        {
            // Initialize (monitor.dm:20): on the late process queue, the histories made
            Assert.True(Dm.@in(g.OnCable, SSmachines.processing_late));
            Assert.True(Dm.@in(g.OffCable, SSmachines.processing_late));
            Assert.NotNull(g.OnCable.history["supply"]);
            Assert.NotNull(g.OnCable.history["demand"]);
            // search (:40): the cable under the console, else the APC of its area (when the APC has its terminal)
            obj_structure_cable cable = Dm.locate<obj_structure_cable>(g.OnCable.loc);
            Assert.Same(cable, g.OnCable.attached_wire_ref.resolve());
            Assert.Null(g.OnCable.local_apc_ref);
            Assert.Same(cable.powernet, g.OnCable.get_powernet());
            Assert.Null(g.OffCable.attached_wire_ref);
            Assert.Same(g.Apc, g.OffCable.local_apc_ref.resolve());
            Assert.Same(g.Apc.terminal.powernet, g.OffCable.get_powernet());
            Assert.Same(g.OnCable.get_powernet(), g.OffCable.get_powernet()); // one run of cable
            // the base computer (_computer.dm): ACTIVE_POWER_USE from the map, the console lights its screen
            Assert.Equal(ACTIVE_POWER_USE, g.OnCable.use_power);
            Assert.Equal("power", g.OnCable.icon_screen);
            Assert.Equal("power_key", g.OnCable.icon_keyboard);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ThePowerMonitorRecordsSupplyAndDemandEveryFiveSecondsAndKeepsSixtyPoints()
    {
        var g = Build(7802);
        g.R.In(() => g.Source.output_energy = 100000);
        MachinesTick(g.R, 8);
        var supply = g.R.In(() => (DmList)g.OnCable.history["supply"]);
        var demand = g.R.In(() => (DmList)g.OnCable.history["demand"]);
        g.R.In(() =>
        {
            Assert.True(supply.len >= 1, "the console recorded");
            Assert.Equal(supply.len, demand.len);
            // energy_to_power(avail): the 100 kJ of one 2 second cycle is 50 kW
            Assert.Contains(Dm.each(supply).Select(o => Dm.num(o)), v => Math.Abs(v - 50000f) < 1);
            Assert.Equal(50f, g.OnCable.record_interval);
            Assert.Equal(60, g.OnCable.record_size);
        });
        // one point per 50 deciseconds (record_interval): ten seconds gives two more, from the first record on
        float before = g.R.In(() => supply.len);
        g.R.Step(10 * Second);
        float after = g.R.In(() => supply.len);
        Assert.InRange(after - before, 1, 3);
        // the history is cut to record_size: a short one keeps its newest points
        // (a record adds one point and cuts one when the list is longer than record_size)
        float size = g.R.In(() => g.OnCable.record_size = supply.len);
        g.R.Step(30 * Second);
        g.R.In(() =>
        {
            Assert.Equal(size, supply.len);
            Assert.Equal(size, demand.len);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheUiDataListsTheNetworksApcsWithTgsFields()
    {
        var g = Build(7803);
        g.R.In(() => g.Source.output_energy = 100000);
        MachinesTick(g.R, 3);
        g.R.In(() =>
        {
            var data = (DmList)Waiting.NoSleep(g.OffCable.ui_data(g.Bob));
            Assert.Equal(60f, Dm.num(data["stored"]));
            Assert.Equal(5f, Dm.num(data["interval"])); // 50 deciseconds
            Assert.Equal(1f, Dm.num(data["attached"]));
            Assert.Equal(Dm.str(display_power(g.Apc.terminal.powernet is datum_powernet pn ? pn.avail : 0)), Dm.str(data["supply"]));
            Assert.Equal(Dm.str(display_power(((datum_powernet)g.Apc.terminal.powernet).load)), Dm.str(data["demand"]));
            Assert.Same(g.OffCable.history, data["history"]);
            var areas = (DmList)data["areas"];
            Assert.Equal(1f, areas.len); // the test room's APC is the only terminal on the network
            var row = (DmList)areas[1];
            Assert.Equal(Dm.str(g.Apc.area.name), Dm.str(row["name"]));
            Assert.Equal(Dm.num(g.Apc.cell.percent()), Dm.num(row["charge"]), 3);
            Assert.Equal(Dm.str(display_power(g.Apc.lastused_total)), Dm.str(row["load"]));
            Assert.Equal(Dm.num(g.Apc.charging), Dm.num(row["charging"]));
            Assert.Equal(3f, Dm.num(row["eqp"]));
            Assert.Equal(3f, Dm.num(row["lgt"]));
            Assert.Equal(3f, Dm.num(row["env"]));

            // the breaker: the channels of the APC read as they are (3 is on and automatic, 0 off and manual)
            g.Apc.equipment = 0;
            g.Apc.lighting = 1;
            data = (DmList)Waiting.NoSleep(g.OffCable.ui_data(g.Bob));
            row = (DmList)((DmList)data["areas"])[1];
            Assert.Equal(0f, Dm.num(row["eqp"]));
            Assert.Equal(1f, Dm.num(row["lgt"]));
            // a cell-less APC reports 0 charge (monitor.dm:100)
            var cell = g.Apc.cell;
            g.Apc.cell = null;
            data = (DmList)Waiting.NoSleep(g.OffCable.ui_data(g.Bob));
            Assert.Equal(0f, Dm.num(((DmList)((DmList)data["areas"])[1])["charge"]));
            g.Apc.cell = cell;
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AConsoleWithNoNetworkIsDetachedIdlesAndSearchesAgain()
    {
        var g = Build(7804);
        // a console beside the run's end, on no cable, in an area that has no APC (the room's APC is forgotten by the area for the moment)
        g.R.In(() => g.Apc.area.apc = null);
        var lone = g.R.In(() => Dm.@new<obj_machinery_computer_monitor>(locate(SourceX + 1, Y, 1)));
        MachinesTick(g.R, 2);
        g.R.In(() =>
        {
            Assert.False(Dm.truthy(lone.get_powernet()));
            Assert.Equal(IDLE_POWER_USE, lone.use_power); // process_late (:32)
            var data = (DmList)Waiting.NoSleep(lone.ui_data(g.Bob));
            Assert.Equal(0f, Dm.num(data["attached"]));
            Assert.Equal(0f, ((DmList)data["areas"]).len);
            Assert.Null(data["supply"]);
            Assert.Equal(0f, ((DmList)lone.history["supply"]).len);
        });
        // the area's APC back: the next search finds it
        g.R.In(() => g.Apc.area.apc = g.Apc);
        MachinesTick(g.R, 2);
        g.R.In(() =>
        {
            Assert.Same(g.Apc.terminal.powernet, lone.get_powernet());
            lone.attached_wire_ref = null;
            lone.local_apc_ref = null;
            g.Apc.area.apc = null;
        });
        MachinesTick(g.R, 2);
        g.R.In(() => Assert.False(Dm.truthy(lone.get_powernet())));
        // a cable laid under it later is found by the next search
        g.R.In(() => Dm.@new<datum_powernet>().add_cable(Dm.@new<obj_structure_cable>(locate(SourceX + 1, Y, 1)))); // a cable of its own network
        MachinesTick(g.R, 2);
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(lone.get_powernet()));
            Assert.Equal(ACTIVE_POWER_USE, lone.use_power);
        });
        // Destroy (:28): it leaves the late process queue
        g.R.In(() => qdel(lone));
        g.R.Step(2);
        g.R.In(() => Assert.False(Dm.@in(lone, SSmachines.processing_late)));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheScreenAndKeyboardShowWhileTheConsoleHasPowerAndGoDarkWithout()
    {
        var g = Build(7805);
        g.R.Step(Second);
        var lit = Overlays(g.R, g.OnCable);
        Assert.Contains("power_key", lit);
        Assert.Contains("power", lit);
        Assert.True(g.R.In(() => Dm.truthy(g.OnCable.light_on))); // power_change (_computer.dm:87): brightness_on
        // no power (the area's equipment channel): the keyboard shows its off picture and there is no screen
        g.R.In(() => Waiting.NoSleep(g.OnCable.set_machine_stat(Dm.bitor(g.OnCable.machine_stat, NOPOWER))));
        g.R.In(() => g.OnCable.update_appearance());
        var dark = Overlays(g.R, g.OnCable);
        Assert.Contains("power_key_off", dark);
        Assert.DoesNotContain("power", dark);
        // broken: the broken picture over the keyboard
        g.R.In(() => Waiting.NoSleep(g.OnCable.set_machine_stat(Dm.bitor(Dm.bitand(g.OnCable.machine_stat, Dm.bitnot(NOPOWER)), BROKEN))));
        g.R.In(() => g.OnCable.update_appearance());
        Assert.Contains("computer_broken", Overlays(g.R, g.OnCable));
        g.R.AssertNoRuntimes();
    }
}
