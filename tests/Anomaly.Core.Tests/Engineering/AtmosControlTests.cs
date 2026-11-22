using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Package E5 (atmos_computers: _air_sensor.dm, _atmos_control.dm, meters.dm; the injector's, vent's and air alarm's multitool_act) in a
/// booted toy room. A tank's air sensor links the plain injector and vent beside it at its late init and sets them up as tg does (the
/// injector at the maximum rate, the vent siphoning to 4500 kPa, off its area's air alarm); the tank's console finds the closest sensor
/// of its chamber and shows its reading and the two devices in ui_data, and its acts switch and set them; the master console reads a
/// monitored meter; a multitool links a logged injector and resets the ports; the sensor follows its power and turns into its item.
/// </summary>
public class AtmosControlTests
{
    private const int Second = 20;

    private static T New<T>(T2Room r, int x, int y, params object[] args) where T : class
        => r.In(() => Dm.@new<T>(new object[] { locate(x, y, 1) }.Concat(args).ToArray()));

    /// <summary>A unary device at (x, y) on a pipe south of it, joined the way a map template's machines are.</summary>
    private static T OnPipe<T>(T2Room r, int x, int y) where T : obj_machinery_atmospherics_components_unary
    {
        T device = New<T>(r, x, y);
        var pipe = New<obj_machinery_atmospherics_pipe_smart>(r, x, y - 1);
        r.In(() => SSair.setup_template_machinery(Dm.list(device, pipe)));
        return device;
    }

    private static DmList Data(T2Room r, obj_machinery_computer_atmos_control console, mob user) => r.In(() => (DmList)Waiting.NoSleep(console.ui_data(user)));

    private static DmList Chamber(DmList data, int i) => (DmList)((DmList)data["chambers"])[i];

    private static object Act(T2Room r, obj_machinery_computer_atmos_control console, mob user, string action, params (string Key, object Value)[] args)
    {
        DmList p = Dm.list();
        foreach ((string key, object value) in args)
            p[key] = value;
        return r.In(() => Waiting.NoSleep(console.ui_act(action, p, Dm.@new<datum_tgui>(user, console, "AtmosControlConsole", console.name), null)));
    }

    [Fact]
    public void ATankSensorLinksTheDevicesBesideItAndItsConsoleReadsAndRunsThem()
    {
        var r = new T2Room(5501);
        var bob = r.Join(12, 12, "Bob");
        var injector = OnPipe<obj_machinery_atmospherics_components_unary_outlet_injector>(r, 4, 6);
        var vent = OnPipe<obj_machinery_atmospherics_components_unary_vent_pump>(r, 6, 6);
        r.Step(Second);
        var sensor = New<obj_machinery_air_sensor_oxygen_tank>(r, 5, 8);
        var far = New<obj_machinery_air_sensor_oxygen_tank>(r, 14, 2); // the same chamber, farther from the console
        r.Step(Second);

        // the late init links the closest injector and vent in oview(4) and configures the plain (non-mapped) types (_air_sensor.dm:40-88)
        r.In(() =>
        {
            Assert.NotNull(sensor.id_tag);
            Assert.Same(sensor, GLOB.objects_by_id_tag[sensor.id_tag]);
            Assert.Equal(injector.id_tag, sensor.inlet_id);
            Assert.Equal(vent.id_tag, sensor.outlet_id);
            Assert.Null(far.inlet_id);
            Assert.Equal(200f, injector.volume_rate);
            Assert.Equal((float)ATMOS_DIRECTION_SIPHONING, vent.pump_direction);
            Assert.Equal((float)ATMOS_INTERNAL_BOUND, vent.pressure_checks);
            Assert.Equal(4500f, vent.internal_pressure_bound);
            Assert.Equal(0f, vent.external_pressure_bound);
            Assert.DoesNotContain(vent, Dm.each<atom>(((area)get_area(vent)).air_vents).ToList());
            Assert.Equal("gsensor1", sensor.icon_state);
        });

        var console = New<obj_machinery_computer_atmos_control_oxygen_tank>(r, 8, 10);
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(sensor.id_tag, console.connected_sensors[ATMOS_GAS_MONITOR_O2]); // the closest of the chamber's two sensors
            var statics = (DmList)Waiting.NoSleep(console.ui_static_data(bob));
            Assert.Equal((200f, 4500f, 1f, 1f, "o2"), (Dm.num(statics["maxInput"]), Dm.num(statics["maxOutput"]), Dm.num(statics["control"]),
                Dm.num(statics["reconnecting"]), Dm.str(statics["defaultGas"])));
            Assert.NotNull(statics["gasInfo"]); // the handbooks
        });
        DmList chamber = Chamber(Data(r, console, bob), 1);
        Assert.Equal(("o2", "Oxygen Supply"), (Dm.str(chamber["id"]), Dm.str(chamber["name"])));
        Assert.True(Dm.num(((DmList)chamber["gasmix"])["total_moles"]) > 0); // the room's air at the sensor
        Assert.Equal((0f, 200f), (Dm.num(((DmList)chamber["input_info"])["active"]), Dm.num(((DmList)chamber["input_info"])["amount"])));
        Assert.Equal((0f, 4500f), (Dm.num(((DmList)chamber["output_info"])["active"]), Dm.num(((DmList)chamber["output_info"])["amount"])));

        // the acts (_atmos_control.dm:150-231): the switches, the rate clamped to the port's volume, the pressure clamped to 5066.25
        Assert.True(Dm.truthy(Act(r, console, bob, "toggle_input", ("chamber", "o2"))));
        Assert.True(Dm.truthy(Act(r, console, bob, "toggle_output", ("chamber", "o2"))));
        Act(r, console, bob, "adjust_input", ("chamber", "o2"), ("rate", 75));
        Act(r, console, bob, "adjust_output", ("chamber", "o2"), ("rate", 9000));
        r.In(() => Assert.Equal((1f, 75f, 1f, ATMOS_PUMP_MAX_PRESSURE), (injector.on, injector.volume_rate, vent.on, vent.internal_pressure_bound)));
        Act(r, console, bob, "adjust_input", ("chamber", "o2"), ("rate", "x")); // not a number: nothing
        Act(r, console, bob, "adjust_input", ("chamber", "plasma"), ("rate", 10)); // not this console's chamber: nothing
        r.In(() => Assert.Equal(75f, injector.volume_rate));
        r.Step(Second);
        r.In(() => Assert.Equal("inje_on", injector.icon_state));
        chamber = Chamber(Data(r, console, bob), 1);
        Assert.Equal((1f, 75f), (Dm.num(((DmList)chamber["input_info"])["active"]), Dm.num(((DmList)chamber["input_info"])["amount"])));

        // a fixed monitor (neither control nor reconnecting) takes no act
        r.In(() => { console.control = FALSE; console.reconnecting = FALSE; });
        Assert.False(Dm.truthy(Act(r, console, bob, "toggle_input", ("chamber", "o2"))));
        r.In(() => Assert.Equal(1f, injector.on));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMasterConsoleReadsTheMonitoredLoopMeters()
    {
        var r = new T2Room(5502);
        var bob = r.Join(12, 12, "Bob");
        var pipe = New<obj_machinery_atmospherics_pipe_smart>(r, 4, 4);
        r.In(() => SSair.setup_template_machinery(Dm.list(pipe)));
        var meter = New<obj_machinery_meter_monitored_distro_loop>(r, 4, 4);
        var master = New<obj_machinery_computer_atmos_control_nocontrol_master>(r, 8, 8);
        r.Step(Second);
        DmList data = Data(r, master, bob);
        Assert.Equal(2f, ((DmList)data["chambers"]).len);
        DmList distro = Chamber(data, 1);
        Assert.Equal(("distro", "Distribution Loop"), (Dm.str(distro["id"]), Dm.str(distro["name"])));
        Assert.NotNull(distro["gasmix"]); // the meter's pipe air
        Assert.Null(distro["input_info"]); // a meter has no devices
        Assert.Equal("waste", Dm.str(Chamber(data, 2)["id"]));
        Assert.Null(Chamber(data, 2)["gasmix"]); // no waste meter here
        r.In(() =>
        {
            Assert.Equal(meter.id_tag, master.connected_sensors[ATMOS_GAS_MONITOR_DISTRO]);
            Assert.Equal(0f, Dm.num(((DmList)Waiting.NoSleep(master.ui_static_data(bob)))["control"]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMultitoolLinksALoggedInjectorAndResetsThePortsAndTheSensorTurnsIntoItsItem()
    {
        var r = new T2Room(5503);
        var bob = r.Join(12, 12, "Bob");
        var sensor = New<obj_machinery_air_sensor_mix_tank>(r, 5, 8);
        r.Step(Second);
        var injector = OnPipe<obj_machinery_atmospherics_components_unary_outlet_injector_monitored_mix_input>(r, 12, 4); // out of oview(4)
        var tool = New<obj_item_multitool>(r, 12, 12);
        r.In(() => { Assert.Null(sensor.inlet_id); injector.volume_rate = 50; });

        // the injector logs itself, then the sensor links it as its input (outlet_injector.dm:46, _air_sensor.dm:145); a mapped type keeps its rate
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(injector.multitool_act(bob, tool))));
        r.In(() => Assert.Same(injector, tool.buffer));
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(sensor.multitool_act(bob, tool))));
        r.In(() =>
        {
            Assert.Equal(injector.id_tag, sensor.inlet_id);
            Assert.Same(sensor, tool.buffer);
            Assert.Equal(50f, injector.volume_rate);
        });
        // with the sensor in the buffer, the injector hands itself to the sensor
        r.In(() => sensor.inlet_id = null);
        r.In(() => Waiting.NoSleep(injector.multitool_act(bob, tool)));
        r.In(() => Assert.Equal(injector.id_tag, sensor.inlet_id));

        // the right click resets the ports (_air_sensor.dm:140)
        r.In(() => Waiting.NoSleep(sensor.multitool_act_secondary(bob, tool)));
        r.In(() => Assert.Null(sensor.inlet_id));

        // no power: off and its picture (process, update_icon_state)
        var stat = r.In(() => sensor.machine_stat);
        r.In(() => { sensor.machine_stat = Dm.bitor(stat, NOPOWER); sensor.process(); });
        r.In(() => Assert.Equal((0f, "gsensor0"), (sensor.on, sensor.icon_state)));
        r.In(() => Assert.Null(Waiting.NoSleep(sensor.return_air())));
        r.In(() => { sensor.machine_stat = stat; sensor.process(); });
        r.In(() => Assert.Equal((1f, "gsensor1"), (sensor.on, sensor.icon_state)));

        // a hand turns it off into its anchored item, which dismantles into an analyzer and iron (_air_sensor.dm:115, :258)
        turf at = r.In(() => get_turf(sensor));
        r.In(() => Waiting.NoSleep(sensor.attack_hand(bob)));
        r.Step(1);
        obj_item_air_sensor item = r.In(() => Dm.each<obj_item_air_sensor>(at).Single());
        r.In(() =>
        {
            Assert.True(QDELETED(sensor));
            Assert.Equal(1f, Dm.num(item.anchored));
            Assert.Null(GLOB.objects_by_id_tag[sensor.id_tag]);
            Waiting.NoSleep(item.deconstruct(TRUE));
        });
        r.Step(1);
        r.In(() =>
        {
            Assert.Single(Dm.each<obj_item_analyzer>(at));
            Assert.Single(Dm.each<obj_item_stack_sheet_iron>(at));
            Assert.Empty(Dm.each<obj_item_air_sensor>(at));
        });
        r.AssertNoRuntimes();
    }
}
