using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4c;

/// <summary>
/// M4c slice 5: tg's airlock cycling pump (atmospherics/machinery/components/unary_devices/airlock_pump.dm) in a booted toy
/// room. A one-tile chamber between two external airlocks, the pump in it facing the station (dir NORTH), its distro and
/// waste pipes under the inner airlock; outside the outer airlock a vacuum. The pump links the doors as tg's set_links does
/// (the outer bolted shut, the inner bolted open). Opening the bolted outer door (tg's hook, airlock.dm:1329) siphons the
/// chamber below allowed_pressure_error within cycle_timeout and opens the outer door; opening the bolted inner door fills
/// the chamber from the distro; a power loss mid-cycle unbolts every door and stops the pump, regained power bolts them again
/// (airlock_pump.dm:604-620). The pump's icon states, the bolts' sounds and the airflow reports follow.
/// </summary>
public class AirlockPumpTests
{
    private const int Second = 20;
    private const string BoltsUp = "sound/machines/airlock/boltsup.ogg";

    private sealed class Vents : IAirflowSink
    {
        public float In, Out;

        public void Flow(turf_open turf, float difference, int direction) { }

        public void CycleEnd() { }

        public void Vent(turf_open turf, float kpa)
        {
            if (kpa > 0) Out += kpa;
            else In -= kpa;
        }
    }

    private sealed record Chamber(AudioRoom R, obj_machinery_door_airlock_external Inner, obj_machinery_door_airlock_external Outer,
        obj_machinery_atmospherics_components_unary_airlock_pump Pump, mob_living_carbon_human Listener, Vents Airflow);

    // tg's cycle_timeout (10 SECONDS) in ticks.
    private const int Timeout = 10 * Second;

    private static void Wall(AudioRoom r, int x, int y) => r.In(() => Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_closed_indestructible))));

    private static float Pressure(AudioRoom r, int x, int y) => r.In(() => Dm.num(((turf_open)locate(x, y, 1)).air.return_pressure()));

    // y 6, 7 and 8 are walls but for column 8: the outer airlock (8,6), the chamber (8,7) with the pump, the inner airlock (8,8).
    // North of them the station (1 atm), south a vacuum (outside).
    private static Chamber Build(ulong seed, bool mapload = false)
    {
        obj_machinery_door_airlock_external inner = null, outer = null;
        obj_machinery_atmospherics_components_unary_airlock_pump pump = null;
        var pipes = new List<obj_machinery_atmospherics>();
        void Machines()
        {
            inner = Dm.@new<obj_machinery_door_airlock_external>(locate(8, 8, 1));
            outer = Dm.@new<obj_machinery_door_airlock_external>(locate(8, 6, 1));
            pipes.Add(Machine<obj_machinery_atmospherics_pipe_smart_manifold4w_cyan_hidden_layer4>(8, 8, SOUTH));
            pipes.Add(Machine<obj_machinery_atmospherics_pipe_smart_manifold4w_scrubbers_hidden_layer2>(8, 8, SOUTH));
            for (int y = 9; y <= 12; y++)
            {
                pipes.Add(Machine<obj_machinery_atmospherics_pipe_smart_simple_cyan_hidden_layer4>(8, y, NORTH));
                pipes.Add(Machine<obj_machinery_atmospherics_pipe_smart_simple_scrubbers_hidden_layer2>(8, y, NORTH));
            }
            // Made after the map load, the pump's LateInitialize links the doors at once (set_links); in the map, at the
            // late loaders of the atoms' init, as on Meta.
            pump = Machine<obj_machinery_atmospherics_components_unary_airlock_pump>(8, 7, NORTH);
            pipes.Add(pump);
        }
        // A map's machine has its dir set as a map var before its init (the loader's var edits); one made later gets it as
        // New's setdir (atmosmachinery.dm:81).
        T Machine<T>(int x, int y, float dir) where T : obj_machinery_atmospherics
        {
            if (!mapload)
                return Dm.@new<T>(locate(x, y, 1), TRUE, dir);
            T m = Dm.@new<T>(locate(x, y, 1));
            m.dir = dir;
            return m;
        }
        var r = new AudioRoom(16, 16, seed: seed, mapExtras: mapload ? _ => Machines() : null);
        for (int x = 2; x <= 15; x++)
            foreach (int y in new[] { 6, 7, 8 })
                if (x != 8)
                    Wall(r, x, y);
        if (!mapload)
        {
            r.In(Machines);
            r.In(() => SSair.setup_template_machinery(Dm.list(pipes.ToArray())));
        }
        // The distro holds air at about two atmospheres.
        r.In(() =>
        {
            var distro = (datum_pipeline)pump.parents[1];
            distro.air.merge(SSair.parse_gas_string("o2=40;n2=150;TEMP=293.15") as datum_gas_mixture);
        });
        // Outside: a vacuum (the outer airlock's own turf too).
        r.In(() =>
        {
            for (int x = 2; x <= 15; x++)
                for (int y = 2; y <= 6; y++)
                    if (locate(x, y, 1) is turf_open t)
                    {
                        t.air = SSair.parse_gas_string(AIRLESS_ATMOS) as datum_gas_mixture;
                        SSair.add_to_active(t);
                    }
        });
        mob_living_carbon_human listener = r.Join(8, 11, "Bob");
        var airflow = new Vents();
        AirflowSeam.Install(r.World, airflow);
        r.Step(2 * Second); // the doors finish the moves set_links started
        return new Chamber(r, inner, outer, pump, listener, airflow);
    }

    private static bool Dense(AudioRoom r, obj_machinery_door d) => r.In(() => Dm.truthy(d.density));

    private static bool Bolted(AudioRoom r, obj_machinery_door_airlock d) => r.In(() => Dm.truthy(d.locked));

    // Steps until the pump is off again; returns the ticks it ran.
    private static int RunCycle(Chamber c, Action<int> each = null)
    {
        int ticks = 0;
        while (ticks < 3 * Timeout && !c.R.In(() => Dm.truthy(c.Pump.on)))
        {
            c.R.Step();
            ticks++;
        }
        Assert.True(c.R.In(() => Dm.truthy(c.Pump.on)), "the pump started");
        int on = 0;
        while (on < 3 * Timeout && c.R.In(() => Dm.truthy(c.Pump.on)))
        {
            each?.Invoke(on);
            c.R.Step();
            on++;
        }
        return on;
    }

    [Fact]
    public void ThePumpLinksTheDoorsTheOuterBoltedShutTheInnerBoltedOpen()
    {
        Chamber c = Build(5101);
        AudioRoom r = c.R;
        Assert.True(r.In(() => Dm.truthy(c.Pump.cycling_set_up)));
        Assert.Same(c.Pump, r.In(() => c.Inner.cycle_pump));
        Assert.Same(c.Pump, r.In(() => c.Outer.cycle_pump));
        Assert.Equal(new object[] { c.Inner }, r.In(() => c.Pump.internal_airlocks.CopyItems().ToArray()));
        Assert.Equal(new object[] { c.Outer }, r.In(() => c.Pump.external_airlocks.CopyItems().ToArray()));
        Assert.True(Dense(r, c.Outer) && Bolted(r, c.Outer), "the outer door is bolted shut");
        Assert.True(!Dense(r, c.Inner) && Bolted(r, c.Inner), "the inner door is bolted open");
        Assert.NotNull(r.In(() => c.Pump.parents[1]));
        Assert.NotNull(r.In(() => c.Pump.parents[2]));
        Assert.Equal("vent_off", r.In(() => c.Pump.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMapLoadedPumpLinksTheDoorsAtTheLateLoaders()
    {
        Chamber c = Build(5106, mapload: true);
        AudioRoom r = c.R;
        Assert.True(r.In(() => Dm.truthy(c.Pump.cycling_set_up)));
        Assert.Equal(0f, r.In(() => c.Pump.can_unwrench)); // a map-loaded pump is secured (airlock_pump.dm:125)
        Assert.True(Dense(r, c.Outer) && Bolted(r, c.Outer), "the outer door is bolted shut");
        Assert.True(!Dense(r, c.Inner) && Bolted(r, c.Inner), "the inner door is bolted open");
        Assert.NotNull(r.In(() => c.Pump.parents[1]));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OpeningTheBoltedOuterDoorSiphonsTheChamberWithinTheTimeoutAndOpensIt()
    {
        Chamber c = Build(5102);
        AudioRoom r = c.R;
        float before = Pressure(r, 8, 7);
        Assert.InRange(before, 95f, 105f);
        float allowed = r.In(() => c.Pump.allowed_pressure_error);

        r.In(() => Waiting.Detach(c.Outer.open())); // the bump's and the hand's open (airlock.dm:1328): the hook starts the cycle
        string iconWhileOn = null;
        float atStop = 0;
        int ran = RunCycle(c, t =>
        {
            if (t == 1) iconWhileOn = r.In(() => c.Pump.icon_state);
            atStop = Pressure(r, 8, 7);
        });
        float after = Pressure(r, 8, 7);
        Assert.Equal("vent_in", iconWhileOn);
        Assert.True(atStop <= allowed + 0.001f || after <= allowed, $"the chamber fell below allowed_pressure_error ({allowed}): {atStop} / {after}");
        Assert.True(ran < Timeout, $"decompression completed before the emergency stop: {ran} ticks");
        r.Step(2 * Second); // the outer door's secure_open
        Assert.False(Dense(r, c.Outer), "the far door opened");
        Assert.True(Bolted(r, c.Outer), "and is bolted open again (secure_open)");
        Assert.True(Dense(r, c.Inner) && Bolted(r, c.Inner), "the inner door is bolted shut");
        Assert.Equal("vent_off", r.In(() => c.Pump.icon_state));
        Assert.True(c.Airflow.In > 0, "the siphoned gas went to the airflow seam");
        Assert.NotEmpty(r.Sounds.Of(c.Listener, BoltsUp)); // stop_cycle's unbolt
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OpeningTheBoltedInnerDoorFillsTheChamberFromTheDistro()
    {
        Chamber c = Build(5103);
        AudioRoom r = c.R;
        r.In(() => Waiting.Detach(c.Outer.open()));
        RunCycle(c);
        r.Step(2 * Second);
        Assert.False(Dense(r, c.Outer));
        float empty = Pressure(r, 8, 7);
        Assert.True(empty < 1.5f, "the chamber is open to the vacuum: " + empty);
        float distroBefore = r.In(() => Dm.num(((datum_pipeline)c.Pump.parents[1]).air.return_pressure()));

        r.In(() => Waiting.Detach(c.Inner.open()));
        string iconWhileOn = null;
        int ran = RunCycle(c, t =>
        {
            if (t == 1) iconWhileOn = r.In(() => c.Pump.icon_state);
        });
        r.Step(2 * Second);
        float full = Pressure(r, 8, 7);
        float target = r.In(() => c.Pump.internal_pressure_target);
        float allowed = r.In(() => c.Pump.allowed_pressure_error);
        Assert.Equal("vent_out", iconWhileOn);
        Assert.True(ran < Timeout, $"pressurization completed before the emergency stop: {ran} ticks");
        Assert.True(full >= target - 2 * allowed, $"the chamber is pressurized: {full} kPa");
        Assert.True(r.In(() => Dm.num(((datum_pipeline)c.Pump.parents[1]).air.return_pressure())) < distroBefore, "the gas came from the distro");
        Assert.True(Dense(r, c.Outer) && Bolted(r, c.Outer), "the outer door is bolted shut");
        Assert.False(Dense(r, c.Inner), "the inner door opened");
        Assert.True(c.Airflow.Out > 0, "the filled gas went to the airflow seam");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APowerLossUnboltsEveryDoorAndStopsThePumpRegainedPowerBoltsThemAgain()
    {
        Chamber c = Build(5104);
        AudioRoom r = c.R;
        r.In(() => Waiting.Detach(c.Outer.open()));
        int waited = 0;
        while (!r.In(() => Dm.truthy(c.Pump.on)) && waited++ < Timeout)
            r.Step();
        Assert.True(r.In(() => Dm.truthy(c.Pump.on)), "the siphon started");
        r.Step(5);

        // tg's power loss: the area's environment channel goes off (an APC's, areas.dm:519): the pump's power_change sets
        // NOPOWER and it is no longer operational (the doors on the same channel lose their power too).
        area room = r.In(() => get_area(c.Pump) as area);
        r.In(() =>
        {
            room.requires_power = TRUE;
            room.power_environ = FALSE;
            room.power_change();
        });
        Assert.False(r.In(() => Dm.truthy(c.Pump.on)), "the pump stopped");
        Assert.False(Bolted(r, c.Inner), "the inner door is unbolted");
        Assert.False(Bolted(r, c.Outer), "the outer door is unbolted");
        Assert.True(Dense(r, c.Inner) && Dense(r, c.Outer), "but not opened");
        Assert.Equal("vent_off", r.In(() => c.Pump.icon_state));
        r.Step(Timeout + Second);
        Assert.False(r.In(() => Dm.truthy(c.Pump.on)), "the emergency stop timer was deleted");

        r.In(() =>
        {
            room.power_environ = TRUE;
            room.power_change();
        });
        r.Step(2 * Second);
        Assert.True(Dense(r, c.Outer) && Bolted(r, c.Outer), "the outer door is bolted shut again");
        Assert.True(!Dense(r, c.Inner) && Bolted(r, c.Inner), "the inner door is bolted open again");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void IsSafeTurfWantsABreathableFloor()
    {
        var r = new AudioRoom(9, 9, seed: 5105);
        r.In(() => Waiting.NoSleep(locate(4, 4, 1).ChangeTurf(typeof(turf_open_floor_iron))));
        r.Step(Second);
        Assert.True(r.In(() => Dm.truthy(is_safe_turf(locate(4, 4, 1) as turf, TRUE, FALSE))), "an iron floor with station air");
        Assert.False(r.In(() => Dm.truthy(is_safe_turf(locate(5, 4, 1) as turf))), "not a floor (the toy room's indestructible ground)");
        r.In(() =>
        {
            var t = (turf_open)locate(4, 4, 1);
            t.air = SSair.parse_gas_string(AIRLESS_ATMOS) as datum_gas_mixture;
        });
        Assert.False(r.In(() => Dm.truthy(is_safe_turf(locate(4, 4, 1) as turf))), "a vacuum");
        r.AssertNoRuntimes();
    }
}
