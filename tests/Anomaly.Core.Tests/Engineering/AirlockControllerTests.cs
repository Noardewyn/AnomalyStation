using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Package E8 (embedded_controller/airlock_controller.dm, airlock_control.dm's sensor, access_controller.dm) in a booted toy room. A
/// one-tile chamber walled east and west between the room and a pocket of space, the atmos incinerator's two doors north and south of it, its dual-port vent and airlock sensor
/// in it and its console beside it: the console links them by their tags, cycles out (closes the inner door, siphons the chamber to
/// 1% of an atmosphere, opens the outer door) and in (siphons, then releases to 95%, opens the inner door) on SSmachines, its ui_data
/// follows, abort with a sanitizing console siphons to the end first, the sensor's button cycles and its picture alerts under 80%.
/// The access console and its two buttons open, cycle and close a two-door airlock, and refuse those without the access.
/// </summary>
public class AirlockControllerTests
{
    private const int Second = 20;

    private static T New<T>(AudioRoom r, int x, int y, params object[] args) where T : class
        => r.In(() => Dm.@new<T>(new object[] { locate(x, y, 1) }.Concat(args).ToArray()));

    private static void Wall(AudioRoom r, int x, int y)
        => r.In(() => Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_closed_wall), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating)))));

    private static DmList Data(AudioRoom r, datum machine, mob user) => r.In(() => (DmList)Waiting.NoSleep(machine.ui_data(user)));

    private static object Act(AudioRoom r, datum machine, mob user, string iface, string action, params (string Key, object Value)[] args)
    {
        DmList p = Dm.list();
        foreach ((string key, object value) in args)
            p[key] = value;
        return r.In(() => Waiting.NoSleep(machine.ui_act(action, p, Dm.@new<datum_tgui>(user, machine, iface, "test"), null)));
    }

    /// <summary>An act whose procs sleep (the doors' close and open), as the server's tgui runs it: detached.</summary>
    private static void ActDetached(AudioRoom r, datum machine, mob user, string iface, string action, params (string Key, object Value)[] args)
    {
        DmList p = r.In(() => Dm.list());
        foreach ((string key, object value) in args)
            r.In(() => p[key] = value);
        r.Command(() => Waiting.Detach(machine.ui_act(action, p, Dm.@new<datum_tgui>(user, machine, iface, "test"), null)));
        r.Step();
    }

    private static float Pressure(AudioRoom r, int x, int y) => r.In(() => Dm.num(((turf_open)locate(x, y, 1)).air.return_pressure()));

    /// <summary>Steps the world until the console's state is <paramref name="state"/> (SSmachines runs it every 2 s).</summary>
    private static void Until(AudioRoom r, obj_machinery_airlock_controller console, string state, int seconds = 30)
    {
        for (int i = 0; i < seconds && r.In(() => console.state) != state; i++)
            r.Step(Second);
        Assert.Equal(state, r.In(() => console.state));
    }

    private sealed record Chamber(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_door_airlock Interior, obj_machinery_door_airlock Exterior,
        obj_machinery_atmospherics_components_binary_dp_vent_pump Pump, obj_machinery_airlock_sensor Sensor, obj_machinery_airlock_controller Console);

    private static Chamber Build(ulong seed)
    {
        var r = new AudioRoom(16, 16, seed: seed);
        var bob = r.Join(6, 6, "Bob");
        // the chamber walled east and west; the outer door opens onto a pocket of space (the burn chamber side), the inner onto the room
        Wall(r, 7, 8);
        Wall(r, 9, 8);
        Wall(r, 7, 9);
        Wall(r, 9, 9);
        Wall(r, 7, 10);
        Wall(r, 9, 10);
        Wall(r, 8, 11);
        r.In(() => Waiting.NoSleep(locate(8, 10, 1).ChangeTurf(typeof(turf_open_space))));
        var interior = New<obj_machinery_door_airlock_public_glass_incinerator_atmos_interior>(r, 8, 7);
        var exterior = New<obj_machinery_door_airlock_public_glass_incinerator_atmos_exterior>(r, 8, 9);
        var pump = New<obj_machinery_atmospherics_components_binary_dp_vent_pump_high_volume_incinerator_atmos>(r, 8, 8);
        r.In(() => SSair.setup_template_machinery(Dm.list(pump)));
        var sensor = New<obj_machinery_airlock_sensor_incinerator_atmos>(r, 8, 8);
        r.Step(Second);
        var console = New<obj_machinery_airlock_controller_incinerator_atmos>(r, 8, 6);
        r.Step(Second);
        return new Chamber(r, bob, interior, exterior, pump, sensor, console);
    }

    [Fact]
    public void TheConsoleCyclesTheChamberOutAndInThroughItsWindowsActs()
    {
        var c = Build(8801);
        var r = c.R;

        // post_machine_initialize: the four weakrefs by GLOB.objects_by_id_tag
        r.In(() =>
        {
            Assert.Same(c.Interior, c.Console.interior_door_ref.resolve());
            Assert.Same(c.Exterior, c.Console.exterior_door_ref.resolve());
            Assert.Same(c.Pump, c.Console.pump_ref.resolve());
            Assert.Same(c.Sensor, c.Console.sensor_ref.resolve());
            Assert.Equal("incinerator access console", Dm.str(c.Console.name));
        });
        DmList data = Data(r, c.Console, c.Bob);
        Assert.Equal(("closed", "closed", "closed", "release"), (Dm.str(data["airlockState"]), Dm.str(data["interiorStatus"]), Dm.str(data["exteriorStatus"]), Dm.str(data["pumpStatus"])));
        Assert.Equal(MathF.Round(Pressure(r, 8, 8) * 10) / 10, Dm.num(data["sensorPressure"]), 3);
        // closed with nothing to do: the pump is switched on (airlock_controller.dm:146-151)
        r.Step(3 * Second);
        Assert.Equal(1f, r.In(() => c.Pump.on));
        Assert.Equal(("airlock_control_standby", "airlock_sensor_standby"), r.In(() => (Dm.str(c.Console.icon_state), Dm.str(c.Sensor.icon_state))));

        // ---- out: the inner door shut, the chamber siphoned to 1% of an atmosphere (sanitize_external), the outer door opened and bolted
        Assert.Equal(1f, Dm.num(Act(r, c.Console, c.Bob, "AirlockController", "cycleExterior")));
        Assert.Equal("outopen", r.In(() => c.Console.target_state));
        Until(r, c.Console, "depressurize");
        Assert.Equal(1f, r.In(() => c.Console.processing));
        Assert.Equal("airlock_control_process", r.In(() => Dm.str(c.Console.icon_state)));
        r.Step(5 * Second);
        Assert.Equal((float)ATMOS_DIRECTION_SIPHONING, r.In(() => c.Pump.pump_direction));
        Assert.Equal(0f, r.In(() => Dm.bitand(c.Pump.pressure_checks, ATMOS_EXTERNAL_BOUND)));
        Assert.Equal("siphon", Dm.str(Data(r, c.Console, c.Bob)["pumpStatus"]));
        Until(r, c.Console, "outopen");
        Assert.True(Pressure(r, 8, 8) <= ONE_ATMOSPHERE * 0.01f || r.In(() => !Dm.truthy(c.Exterior.density)), "siphoned before the outer door opened");
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(c.Exterior.density));
            Assert.True(Dm.truthy(c.Exterior.locked)); // secure_open bolts it open
            Assert.True(Dm.truthy(c.Interior.density)); // already shut: the console went straight to depressurize (:122-131)
            Assert.Equal(0f, c.Pump.on); // outopen: the pump off
            Assert.Equal(0f, c.Console.processing);
            Assert.Equal("airlock_control_standby", Dm.str(c.Console.icon_state));
        });
        data = Data(r, c.Console, c.Bob);
        Assert.Equal(("outopen", "closed", "open"), (Dm.str(data["airlockState"]), Dm.str(data["interiorStatus"]), Dm.str(data["exteriorStatus"])));

        // ---- in: the outer door shut and the chamber siphoned again, then the pump turned round and the chamber filled from its input
        r.In(() => ((datum_gas_mixture)c.Pump.airs[1]).merge(SSair.parse_gas_string("o2=2000;n2=8000;TEMP=293.15") as datum_gas_mixture));
        Act(r, c.Console, c.Bob, "AirlockController", "cycleInterior");
        Until(r, c.Console, "pressurize");
        r.Step(Second);
        Until(r, c.Console, "inopen");
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(c.Exterior.density));
            Assert.True(Dm.truthy(c.Exterior.locked)); // secure_close bolts it shut
            Assert.False(Dm.truthy(c.Interior.density));
            Assert.Equal((float)ATMOS_DIRECTION_RELEASING, c.Pump.pump_direction);
            Assert.Equal((float)ATMOS_EXTERNAL_BOUND, Dm.bitand(c.Pump.pressure_checks, ATMOS_EXTERNAL_BOUND));
            Assert.Equal(0f, c.Pump.on); // inopen: the pump off
        });
        Assert.True(Pressure(r, 8, 8) >= ONE_ATMOSPHERE * 0.9f, "released to an atmosphere");

        // ---- close the inner door again (cycleClosed)
        Act(r, c.Console, c.Bob, "AirlockController", "cycleClosed");
        Until(r, c.Console, "closed");
        r.Step(3 * Second);
        Assert.True(r.In(() => Dm.truthy(c.Interior.density)));
        Assert.Equal("closed", Dm.str(Data(r, c.Console, c.Bob)["interiorStatus"]));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AbortWithASanitizingConsoleSiphonsTheChamberFirstAndTheSensorCyclesAndAlerts()
    {
        var c = Build(8802);
        var r = c.R;
        r.Step(3 * Second);

        // the sensor's button: from closed, a cycle to the inner door (cycle(): neither inopen nor pressurize)
        r.In(() => Waiting.NoSleep(c.Bob.forceMove(locate(8, 7, 1))));
        r.Step();
        r.In(() => Waiting.NoSleep(c.Sensor.attack_hand(c.Bob)));
        Assert.Equal("inopen", r.In(() => c.Console.target_state));
        r.In(() => Waiting.NoSleep(c.Bob.forceMove(locate(6, 6, 1))));
        Until(r, c.Console, "inopen");
        // from inopen the button cycles out
        r.In(() => Waiting.NoSleep(c.Sensor.attack_hand(c.Bob)));
        Assert.Equal("outopen", r.In(() => c.Console.target_state));
        Until(r, c.Console, "depressurize");

        // abort while siphoning: the incinerator's console sanitizes, so it siphons on to 1% before it rests closed (:174-180)
        Act(r, c.Console, c.Bob, "AirlockController", "abort");
        Assert.Equal("closed", r.In(() => c.Console.target_state));
        Until(r, c.Console, "closed");
        Assert.True(Pressure(r, 8, 8) <= ONE_ATMOSPHERE * 0.01f, "siphoned to the end before the abort took");
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(c.Interior.density));
            Assert.True(Dm.truthy(c.Exterior.density));
            Assert.Equal(1f, c.Pump.on); // closed: the pump on (:146-151)
        });

        // the sensor's alert under 80% of an atmosphere (airlock_control.dm:88-94), its picture, and its "off" picture
        Assert.Equal((1f, "airlock_sensor_alert"), r.In(() => (c.Sensor.alert, Dm.str(c.Sensor.icon_state))));
        r.In(() =>
        {
            c.Sensor.on = 0;
            c.Sensor.update_appearance();
        });
        Assert.Equal("airlock_sensor_off", r.In(() => Dm.str(c.Sensor.icon_state)));
        // a sensor that is off keeps the console's last reading (sensor_pressure, :272-279)
        float last = r.In(() => Dm.num(c.Console.last_pressure));
        r.In(() => ((turf_open)locate(8, 8, 1)).air.merge(SSair.parse_gas_string("n2=50;TEMP=293.15") as datum_gas_mixture));
        Assert.Equal(MathF.Round(last * 10) / 10, Dm.num(Data(r, c.Console, c.Bob)["sensorPressure"]), 3);
        r.AssertNoRuntimes();
    }

    private sealed record Lock(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_door_airlock Inner, obj_machinery_door_airlock Outer,
        obj_machinery_door_buttons_airlock_controller Console, obj_machinery_door_buttons_access_button InnerButton, obj_machinery_door_buttons_access_button OuterButton);

    private static Lock BuildLock(ulong seed)
    {
        var r = new AudioRoom(16, 16, seed: seed);
        var bob = r.Join(5, 5, "Bob");
        var inner = New<obj_machinery_door_airlock_virology>(r, 8, 6);
        var outer = New<obj_machinery_door_airlock_virology>(r, 8, 9);
        r.In(() =>
        {
            inner.id_tag = "virology_airlock_interior";
            outer.id_tag = "virology_airlock_exterior";
            inner.autoclose = FALSE;
            outer.autoclose = FALSE;
            inner.@lock();
            outer.@lock();
        });
        var console = r.In(() =>
        {
            var m = Dm.@new<obj_machinery_door_buttons_airlock_controller>(locate(9, 7, 1));
            m.idSelf = "virology_airlock_control";
            m.idInterior = "virology_airlock_interior";
            m.idExterior = "virology_airlock_exterior";
            m.req_access = Dm.list(ACCESS_VIROLOGY);
            return m;
        });
        obj_machinery_door_buttons_access_button Button(int x, int y, string door) => r.In(() =>
        {
            var b = Dm.@new<obj_machinery_door_buttons_access_button>(locate(x, y, 1));
            b.idSelf = "virology_airlock_control";
            b.idDoor = door;
            b.req_access = Dm.list(ACCESS_VIROLOGY);
            return b;
        });
        // the var edits a map gives (idSelf, idDoor) are in place before LateInitialize looks them up: run the lookups again, as tg's
        // post_machine_initialize does at map load
        var innerButton = Button(7, 5, "virology_airlock_interior");
        var outerButton = Button(7, 10, "virology_airlock_exterior");
        r.In(() =>
        {
            console.find_objects_by_tag();
            innerButton.find_objects_by_tag();
            outerButton.find_objects_by_tag();
        });
        r.Step(Second);
        return new Lock(r, bob, inner, outer, console, innerButton, outerButton);
    }

    private static void Press(Lock l, obj_machinery_door_buttons_access_button button)
    {
        l.R.In(() => Waiting.NoSleep(l.Bob.forceMove(get_step(button, SOUTH) ?? get_step(button, EAST))));
        l.R.Command(() => Waiting.Detach(button.interact(l.Bob)));
        l.R.Step();
    }

    private static void GiveAccess(Lock l) => l.R.In(() =>
    {
        // the Captain's spare (every station access) in the active hand: allowed() reads the held ID first (access.dm:12)
        var card = Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(l.Bob.loc);
        Assert.True(Dm.truthy(Waiting.NoSleep(l.Bob.put_in_active_hand(card))));
    });

    [Fact]
    public void TheAccessButtonsOpenCycleAndCloseTheirAirlockForThoseWithTheAccess()
    {
        var l = BuildLock(8803);
        var r = l.R;
        r.In(() =>
        {
            Assert.Same(l.Console, l.InnerButton.controller);
            Assert.Same(l.Inner, l.InnerButton.door);
            Assert.Same(l.Inner, l.Console.interior_airlock);
            Assert.Same(l.Outer, l.Console.exterior_airlock);
            Assert.Equal("access_button_standby", Dm.str(l.InnerButton.icon_state));
            Assert.Equal("access_control_standby", Dm.str(l.Console.icon_state));
        });

        // no access: "Access denied." and the door stays bolted shut
        Press(l, l.InnerButton);
        r.Step(3 * Second);
        Assert.True(r.In(() => Dm.truthy(l.Inner.density) && Dm.truthy(l.Inner.locked)));
        Assert.False(r.In(() => Dm.truthy(l.InnerButton.busy)));

        // with the access, both doors shut: only_open (unbolt, open, bolt) and the button busy for 2 seconds
        GiveAccess(l);
        Press(l, l.InnerButton);
        Assert.True(r.In(() => Dm.truthy(l.InnerButton.busy)));
        Assert.Equal("access_button_cycle", r.In(() => Dm.str(l.InnerButton.icon_state)));
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(l.Inner.density));
            Assert.True(Dm.truthy(l.Inner.locked));
            Assert.False(Dm.truthy(l.InnerButton.busy));
            Assert.False(Dm.truthy(l.Console.busy));
            Assert.Equal("access_button_standby", Dm.str(l.InnerButton.icon_state));
        });

        // the outer button with the inner door open: cycle_close shuts and bolts the inner door, cycle_open opens the outer 2 s later
        Press(l, l.OuterButton);
        r.Step(Second);
        Assert.True(r.In(() => Dm.truthy(l.Console.busy)));
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(l.Inner.density) && Dm.truthy(l.Inner.locked));
            Assert.False(Dm.truthy(l.Outer.density));
            Assert.True(Dm.truthy(l.Outer.locked));
            Assert.False(Dm.truthy(l.Console.busy));
        });

        // the console's window: the doors by REF, their state, and an act on the outer door closes it (only_close)
        DmList data = Data(r, l.Console, l.Bob);
        r.In(() =>
        {
            Assert.Equal(REF(l.Inner), data["interior_door"]);
            Assert.Equal(REF(l.Outer), data["exterior_door"]);
            Assert.Equal((1f, 0f, 0f), (Dm.num(data["interior_door_closed"]), Dm.num(data["exterior_door_closed"]), Dm.num(data["busy"])));
        });
        object outerRef = r.In(() => REF(l.Outer));
        ActDetached(r, l.Console, l.Bob, "AirlockButtonController", "close", ("requested_door", outerRef));
        r.Step(3 * Second);
        Assert.True(r.In(() => Dm.truthy(l.Outer.density) && Dm.truthy(l.Outer.locked)));
        // open the inner door from the window: both shut, so only_open
        object innerRef = r.In(() => REF(l.Inner));
        ActDetached(r, l.Console, l.Bob, "AirlockButtonController", "open", ("requested_door", innerRef));
        r.Step(3 * Second);
        Assert.False(r.In(() => Dm.truthy(l.Inner.density)));
        // the outer door from the window with the inner open: the cycle
        ActDetached(r, l.Console, l.Bob, "AirlockButtonController", "open", ("requested_door", outerRef));
        r.Step(5 * Second);
        Assert.True(r.In(() => Dm.truthy(l.Inner.density)));
        Assert.False(r.In(() => Dm.truthy(l.Outer.density)));
        // a request without a door does nothing (ui_act :252)
        Assert.Equal(1f, Dm.num(Act(r, l.Console, l.Bob, "AirlockButtonController", "close")));
        r.Step(3 * Second);
        Assert.False(r.In(() => Dm.truthy(l.Outer.density)));
        r.AssertNoRuntimes();
    }
}
