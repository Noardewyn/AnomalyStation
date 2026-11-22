using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M4c slice 5 through the real server on MetaStation: the Auxiliary Airlock in arrivals, tg's cycling pump at (39,119)
/// facing the station, the inner doors (38-39,120) bolted open and the outer doors (38-39,118) bolted shut by the pump's
/// set_links. A headless Godot client (<see cref="PlaySession"/>) stands in the chamber and clicks the bolted outer door:
/// tg's hook (airlock.dm:1329) starts the siphon, the chamber's pressure in the dev gas probe falls below the pump's
/// allowed error, and the outer doors open; a click on the bolted inner door fills the chamber from the distro and opens the
/// inner doors. The client sees the pump's vent_in / vent_out / vent_off states and the doors' bolt lights, and hears the
/// bolts, and gets the pump's air streams (DV34).
/// </summary>
public class AirlockCycleGameplayTests
{
    private const int Second = 20; // ticks per second
    private const string BoltsUp = "sound/machines/airlock/boltsup.ogg";

    private readonly ITestOutputHelper _output;

    public AirlockCycleGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        using (w.World.Enter())
            Waiting.NoSleep(m.forceMove(Builtins.locate(x, y, m.z)));
        w.Run(Second);
    }

    private static T Find<T>(RealHeadlessWorld w, int x, int y, int z) where T : class
        => In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(x, y, z)).contents).OfType<T>().FirstOrDefault());

    private static List<string> States(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State).ToList();
    }

    private static string State(ReplicaWorld replica, atom a)
        => replica.Entities.Single(e => e.Id == a.dm_id).State.Appearance is { } ap ? CrewOutfitTests.Icons(replica, ap).FirstOrDefault().State : null;

    private static float ServerPressure(RealHeadlessWorld w, int x, int y, int z)
        => In(w, () => Dm.num(((turf_open)Builtins.locate(x, y, z)).air.return_pressure()));

    // The pump's air streams on the chamber's cells, as the client has them (DV34, the floor devices' vents).
    private static bool ChamberStreams(ReplicaWorld replica)
        => replica.AirflowVentCells.Any(c => (c.X == 38 || c.X == 39) && c.Y == 119 && c.Cell != Airflow.Clear);

    // The dev gas probe's answer for (x, y): the pressure the client is told.
    private static float Probe(RealHeadlessWorld w, RealHeadlessWorld.Player p, List<DevGasReport> reports, int x, int y, int z)
    {
        reports.Clear();
        p.Session.DevGasProbe(x, y, z);
        w.Run(3);
        DevGasReport report = Assert.Single(reports);
        DevGasCell cell = report.At(x, y);
        Assert.Equal(DevCellKind.Open, cell.Kind);
        return cell.Pressure;
    }

    [MetaServerFact]
    public void A_player_cycles_the_auxiliary_airlock_out_and_back_in()
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "metastation", MapsFolder = MetaServer.Folder(), DevCommands = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 4000 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var reports = new List<DevGasReport>();
        p.Client.DevGasReportReceived += r => reports.Add(r);
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // Every one of Meta's pumps linked its doors at the map load (a map-loaded pump that finds none CRASHes, set_links).
        (int pumps, int linked) = In(w, () =>
        {
            var all = Dm.each_cast<object>(SSair.atmos_machinery).OfType<obj_machinery_atmospherics_components_unary_airlock_pump>().ToList();
            return (all.Count, all.Count(a => Dm.truthy(a.cycling_set_up)));
        });
        _output.WriteLine($"Meta's airlock pumps: {pumps}, linked {linked}");
        Assert.Equal(29, pumps);
        Assert.Equal(pumps, linked);

        var pump = Find<obj_machinery_atmospherics_components_unary_airlock_pump>(w, 39, 119, z);
        Assert.NotNull(pump);
        var inner = new[] { Find<obj_machinery_door_airlock_external>(w, 38, 120, z), Find<obj_machinery_door_airlock_external>(w, 39, 120, z) };
        var outer = new[] { Find<obj_machinery_door_airlock_external>(w, 38, 118, z), Find<obj_machinery_door_airlock_external>(w, 39, 118, z) };
        Assert.All(inner.Concat(outer), d => Assert.NotNull(d));
        Assert.All(inner, d => Assert.Same(pump, In(w, () => d.cycle_pump)));
        Assert.All(outer, d => Assert.Same(pump, In(w, () => d.cycle_pump)));
        w.Run(2 * Second); // the doors finish the moves the pumps' set_links started at the map load
        Assert.All(inner, d => Assert.True(In(w, () => !Dm.truthy(d.density) && Dm.truthy(d.locked)), "the inner doors are bolted open"));
        Assert.All(outer, d => Assert.True(In(w, () => Dm.truthy(d.density) && Dm.truthy(d.locked)), "the outer doors are bolted shut"));

        // The distro the pump fills from starts empty (tg's pipes have no air of their own) and fills from the air mix.
        int waited = 0;
        while (waited < 120 * Second && In(w, () => Dm.num(((datum_gas_mixture)pump.airs[1]).return_pressure())) < 50f)
        {
            w.Run(Second);
            waited += Second;
        }
        float distro = In(w, () => Dm.num(((datum_gas_mixture)pump.airs[1]).return_pressure()));
        _output.WriteLine($"the pump's distro after {waited / Second} s: {distro} kPa");
        Assert.True(distro >= 50f, "the distro filled: " + distro);

        // Into the chamber. The client sees the outer doors' bolt lights and the pump off.
        Put(w, me, 38, 119);
        List<string> outerSeen = States(replica, outer[0]);
        _output.WriteLine("the bolted outer door as the client sees it: " + string.Join(", ", outerSeen));
        Assert.Contains("lights_bolts", outerSeen);
        Assert.Equal("vent_off", State(replica, pump));
        float full = Probe(w, p, reports, 38, 119, z);
        Assert.InRange(full, 95f, 106f);

        // A click on the bolted outer door: the pump siphons the chamber.
        heard.Clear();
        Assert.True(p.Session.Click(outer[0].dm_id, ClickButton.Left, ClickModifiers.None));
        var pumpStates = new HashSet<string>();
        float lowest = full;
        bool started = false;
        bool streams = false;
        int ran = 0;
        w.Run(15 * Second, _ =>
        {
            bool on = In(w, () => Dm.truthy(pump.on));
            started |= on;
            if (on) ran++;
            if (replica.Entities.Any(e => e.Id == pump.dm_id)) pumpStates.Add(State(replica, pump));
            lowest = Math.Min(lowest, ServerPressure(w, 38, 119, z));
            streams |= ChamberStreams(replica);
        });
        float allowed = In(w, () => pump.allowed_pressure_error);
        _output.WriteLine($"siphon: ran {ran} ticks, lowest chamber pressure {lowest} kPa (allowed error {allowed}); pump states seen {string.Join(", ", pumpStates)}");
        Assert.True(started, "the click started the cycle");
        Assert.False(In(w, () => Dm.truthy(pump.on)), "the cycle ended");
        Assert.True(ran < In(w, () => (int)pump.cycle_timeout) * Second / 10, "decompression completed before the emergency stop");
        Assert.True(lowest <= allowed + 0.01f, "the chamber fell below the allowed error: " + lowest);
        Assert.Contains("vent_in", pumpStates);
        Assert.True(streams, "the client got the pump's air streams (DV34)");
        Assert.All(outer, d => Assert.False(In(w, () => Dm.truthy(d.density)), "the outer doors opened"));
        Assert.All(inner, d => Assert.True(In(w, () => Dm.truthy(d.density) && Dm.truthy(d.locked)), "the inner doors are bolted shut"));
        Assert.Contains(heard, e => e.File == BoltsUp);
        List<string> innerSeen = States(replica, inner[0]);
        _output.WriteLine("the bolted inner door as the client sees it: " + string.Join(", ", innerSeen));
        Assert.Contains("lights_bolts", innerSeen);
        Assert.Contains("open", States(replica, outer[0]));
        Assert.Equal("vent_off", State(replica, pump));
        float empty = Probe(w, p, reports, 38, 119, z);
        _output.WriteLine($"the probe in the chamber after the siphon: {empty} kPa");
        Assert.True(empty < 2f, "the client's probe shows the chamber empty: " + empty);

        // Back in: a click on the bolted inner door fills the chamber from the distro.
        heard.Clear();
        pumpStates.Clear();
        started = false;
        streams = false;
        ran = 0;
        float distroBefore = In(w, () => Dm.num(((datum_gas_mixture)pump.airs[1]).return_pressure()));
        // The outer doors' own turfs kept their air through the siphon (the pump's adjacent turfs are the chamber's open
        // ones, check_turfs); opened, that air rushes out and its push (tg's space wind) can move the player a tile toward
        // space. Back to the chamber, beside the inner doors.
        (int ax, int ay) = In(w, () => ((int)me.x, (int)me.y));
        _output.WriteLine($"the player after the doors opened: {ax},{ay}");
        Put(w, me, 38, 119);
        Assert.True(p.Session.Click(inner[0].dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(15 * Second, _ =>
        {
            bool on = In(w, () => Dm.truthy(pump.on));
            started |= on;
            if (on) ran++;
            if (replica.Entities.Any(e => e.Id == pump.dm_id)) pumpStates.Add(State(replica, pump));
            streams |= ChamberStreams(replica);
        });
        _output.WriteLine($"fill: ran {ran} ticks; pump states seen {string.Join(", ", pumpStates)}; distro {distroBefore} -> {In(w, () => Dm.num(((datum_gas_mixture)pump.airs[1]).return_pressure()))} kPa");
        Assert.True(started, "the click started the cycle");
        Assert.False(In(w, () => Dm.truthy(pump.on)), "the cycle ended");
        Assert.True(ran < In(w, () => (int)pump.cycle_timeout) * Second / 10, "pressurization completed before the emergency stop");
        Assert.Contains("vent_out", pumpStates);
        Assert.True(streams, "the client got the pump's air streams (DV34)");
        Assert.All(inner, d => Assert.False(In(w, () => Dm.truthy(d.density)), "the inner doors opened"));
        Assert.All(outer, d => Assert.True(In(w, () => Dm.truthy(d.density) && Dm.truthy(d.locked)), "the outer doors are bolted shut"));
        Assert.Contains(heard, e => e.File == BoltsUp);
        Assert.Contains("lights_bolts", States(replica, outer[0]));
        float refilled = Probe(w, p, reports, 38, 119, z);
        _output.WriteLine($"the probe in the chamber after the fill: {refilled} kPa");
        Assert.True(refilled >= 95f, "the client's probe shows the chamber pressurized: " + refilled);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the player did ran into a runtime
    }
}
