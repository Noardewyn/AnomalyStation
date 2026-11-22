using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4b;

/// <summary>
/// M4b slice 2 (the pipenets): tg's pipelines in a booted 16 x 16 toy room. The machines are placed after the boot and joined
/// the way tg joins a map template's machines (SSair.setup_template_machinery: every machine's atmos_init, then the blocking
/// pipeline build). A line of pipes is one pipeline whose volume is the pipes' volume; an air vent fills a vacuum room from
/// its pipeline up to one atmosphere; a scrubber takes the room's carbon dioxide into its pipeline at tg's rate; a pipe cut in two splits
/// the pipeline through SSair's rebuild queue and keeps the gas; a pressure pump moves gas from one pipeline to another up to
/// its target; a closed manual valve keeps two pipelines apart and an open one mixes them.
/// </summary>
public class PipenetTests
{
    private const int Second = 20; // ticks per second

    // A machine at (x, y) facing dir, created the way tg's new /obj/machinery/atmospherics/x(loc, process, setdir) does.
    private static T Place<T>(T2Room r, int x, int y, float dir = SOUTH) where T : obj_machinery_atmospherics
        => r.In(() => Dm.@new<T>(locate(x, y, 1), TRUE, dir));

    private static void Join(T2Room r, params obj_machinery_atmospherics[] machines)
        => r.In(() => SSair.setup_template_machinery(Dm.list(machines)));

    private static datum_pipeline Net(T2Room r, obj_machinery_atmospherics m) => r.In(() => m switch
    {
        obj_machinery_atmospherics_pipe p => p.parent,
        obj_machinery_atmospherics_components c => c.parents[1] as datum_pipeline,
        _ => null,
    });

    private static float Pressure(T2Room r, datum_pipeline net) => r.In(() => Dm.num(net.air.return_pressure()));

    private static float RoomPressure(T2Room r, int x, int y) => r.In(() => Dm.num(((turf_open)locate(x, y, 1)).air.return_pressure()));

    private static float Moles(T2Room r, int x0, int x1, object gas = null) => r.In(() =>
    {
        float total = 0;
        for (int x = x0; x <= x1; x++)
            for (int y = 2; y < T2Room.Size; y++)
                if (locate(x, y, 1) is turf_open t)
                    total += gas == null ? Dm.num(t.air.total_moles()) : Dm.num(t.air.moles[gas]);
        return total;
    });

    private static void Fill(T2Room r, datum_pipeline net, string mix) => r.In(() =>
    {
        datum_gas_mixture gas = SSair.parse_gas_string(mix) as datum_gas_mixture;
        net.air.merge(gas);
    });

    private static void Wall(T2Room r, int x, int y) => r.In(() => Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_closed_indestructible))));

    private static void Empty(T2Room r, int x0, int x1) => r.In(() =>
    {
        for (int x = x0; x <= x1; x++)
            for (int y = 2; y < T2Room.Size; y++)
                if (locate(x, y, 1) is turf_open t)
                {
                    t.air = SSair.parse_gas_string(AIRLESS_ATMOS) as datum_gas_mixture;
                    SSair.add_to_active(t);
                }
    });

    [Fact]
    public void ALineOfPipesIsOnePipelineWithThePipesVolume()
    {
        var r = new T2Room(4201);
        var pipes = new List<obj_machinery_atmospherics>();
        for (int x = 4; x <= 10; x++)
            pipes.Add(Place<obj_machinery_atmospherics_pipe_smart>(r, x, 5));
        Join(r, pipes.ToArray());
        datum_pipeline net = Net(r, pipes[0]);
        Assert.NotNull(net);
        Assert.All(pipes, p => Assert.Same(net, Net(r, p)));
        Assert.Equal(7, r.In(() => net.members.len));
        float volume = r.In(() => pipes.Sum(p => Dm.num(((obj_machinery_atmospherics_pipe)p).volume)));
        Assert.Equal(volume, r.In(() => Dm.num(net.air.volume)));
        Assert.True(r.In(() => Dm.@in(net, SSair.networks)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnAirVentFillsAVacuumRoomFromItsPipelineUpToOneAtmosphere()
    {
        var r = new T2Room(4202);
        for (int y = 2; y < T2Room.Size; y++)
            Wall(r, 8, y);
        var vent = Place<obj_machinery_atmospherics_components_unary_vent_pump_on>(r, 4, 5, EAST);
        var pipes = new List<obj_machinery_atmospherics> { vent };
        for (int x = 5; x <= 12; x++)
            pipes.Add(Place<obj_machinery_atmospherics_pipe_smart>(r, x, 5)); // the line runs under the wall to the east half
        Join(r, pipes.ToArray());
        datum_pipeline net = Net(r, vent);
        Assert.Same(net, Net(r, pipes[^1]));
        Assert.Contains(r.In(() => vent.airs[1]), r.In(() => net.other_airs.CopyItems()));
        Fill(r, net, "o2=2500;n2=9500;TEMP=293.15"); // about 1.4 times what the west half holds at one atmosphere
        r.Step(Second);
        Empty(r, 2, 7);
        float west0 = RoomPressure(r, 3, 10);
        Assert.True(west0 < 1f, "the west half is a vacuum: " + west0);
        float pipe0 = Pressure(r, net);

        r.Step(30 * Second);
        float west = RoomPressure(r, 3, 10);
        Assert.True(west > 20f, "the vent fills the room: " + west);
        Assert.True(Pressure(r, net) < pipe0, "the pipeline gave its gas: " + Pressure(r, net));

        r.Step(240 * Second);
        west = RoomPressure(r, 3, 10);
        Assert.InRange(west, 90f, 102f); // ATMOS_EXTERNAL_BOUND: the vent stops at external_pressure_bound (ONE_ATMOSPHERE)
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AScrubberTakesTheRoomsCarbonDioxideIntoItsPipeline()
    {
        var r = new T2Room(4203);
        var scrubber = Place<obj_machinery_atmospherics_components_unary_vent_scrubber_on>(r, 8, 8, EAST);
        var pipes = new List<obj_machinery_atmospherics> { scrubber };
        for (int x = 9; x <= 12; x++)
            pipes.Add(Place<obj_machinery_atmospherics_pipe_smart>(r, x, 8));
        Join(r, pipes.ToArray());
        // tg: a scrubber made outside the map load checks its turf at once (atmos_sensitive's Attach), finds no node yet and
        // turns itself off (should_atmos_process); an air alarm turns it on again.
        Assert.Equal(0f, r.In(() => scrubber.on));
        r.In(() => scrubber.set_on(TRUE));
        datum_pipeline net = Net(r, scrubber);
        r.Step(Second);
        r.In(() =>
        {
            var t = (turf_open)locate(8, 8, 1);
            t.air.merge(SSair.parse_gas_string("co2=20;TEMP=293.15") as datum_gas_mixture);
            SSair.add_to_active(t);
        });
        float co2Before = Moles(r, 2, 15, typeof(datum_gas_carbon_dioxide));
        Assert.True(co2Before > 19f);

        r.Step(60 * Second);
        float co2Room = Moles(r, 2, 15, typeof(datum_gas_carbon_dioxide));
        // the pipenet's gas: the pipes' air and the scrubber's own mixture, which reconcile_air shares by volume
        float co2Pipe = r.In(() => Dm.num(net.air.moles[typeof(datum_gas_carbon_dioxide)]) + Dm.num(((datum_gas_mixture)scrubber.airs[1]).moles[typeof(datum_gas_carbon_dioxide)]));
        // tg's rate: a scrubber takes volume_rate / the tile's volume (200 / 2500 L) of its own tile's carbon dioxide per
        // SSair fire, and the room refills that tile by sharing, so one scrubber clears this 14 x 14 room in about 20 minutes.
        Assert.True(co2Room < co2Before - 1f, $"the scrubber took carbon dioxide: room {co2Room}, before {co2Before}");
        Assert.InRange(co2Pipe, co2Before - co2Room - 0.01f, co2Before - co2Room + 0.01f); // all of it is in the scrubber's pipeline
        Assert.True(r.In(() => Dm.num(net.air.moles[typeof(datum_gas_oxygen)])) < 0.01f, "it filtered only carbon dioxide");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACutPipeSplitsThePipelineThroughTheRebuildQueueAndKeepsTheGas()
    {
        var r = new T2Room(4204);
        var pipes = new List<obj_machinery_atmospherics_pipe>();
        for (int x = 3; x <= 13; x++)
            pipes.Add(Place<obj_machinery_atmospherics_pipe_smart>(r, x, 6));
        Join(r, pipes.ToArray());
        datum_pipeline whole = Net(r, pipes[0]);
        Fill(r, whole, "n2=100;TEMP=293.15");
        float total = r.In(() => Dm.num(whole.air.total_moles()));
        float perPipe = total / pipes.Count;

        r.In(() => qdel(pipes[5])); // x = 8
        r.Step(2 * Second); // SSair's rebuild queue rebuilds both halves at its next fire
        datum_pipeline west = Net(r, pipes[0]), east = Net(r, pipes[^1]);
        Assert.NotNull(west);
        Assert.NotNull(east);
        Assert.NotSame(west, east);
        Assert.True(r.In(() => QDELETED(whole)));
        Assert.Equal(5, r.In(() => west.members.len));
        Assert.Equal(5, r.In(() => east.members.len));
        float kept = r.In(() => Dm.num(west.air.total_moles()) + Dm.num(east.air.total_moles()));
        Assert.InRange(kept, total - perPipe - 0.5f, total - perPipe + 0.5f); // the cut pipe's share is gone with it (tg: deconstruction would vent it)
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnAirTankBesideAPipeSharesItsAirAndIsDestroyedCleanly()
    {
        // the M7 visual check: a blast destroyed MetaStation's air tank, then inert (D112, DV31: its Initialize never made parents or
        // airs, and its Destroy's nullify_node read the parents list it never had). Live since syndicate N1b: mapped, it opens its port
        // toward its dir (tank.dm:111) and joins the pipe's pipenet, which takes its air; destroyed, it leaves the pipe's net whole
        var r = new T2Room(4205);
        var pipe = Place<obj_machinery_atmospherics_pipe_smart>(r, 6, 6);
        var tank = Place<obj_machinery_atmospherics_components_tank_air>(r, 7, 6, WEST);
        Assert.False(r.In(() => InertSeam.IsInert(tank)));
        r.In(() =>
        {
            tank.set_portdir_relative(tank.dir, TRUE); // what its Initialize does at mapload
            tank.set_init_directions();
        });
        Join(r, pipe, tank);
        r.Step(5 * Second);
        Assert.Equal((float)WEST, r.In(() => tank.initialize_directions));
        Assert.NotNull(Net(r, pipe));
        Assert.True(Pressure(r, Net(r, pipe)) > 50, "the pipe holds the tank's air");
        r.In(() => qdel(tank));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(tank)));
        Assert.NotNull(Net(r, pipe));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APressurePumpMovesGasFromOnePipelineToAnotherUpToItsTarget()
    {
        var r = new T2Room(4205);
        var input = new List<obj_machinery_atmospherics>();
        for (int x = 3; x <= 6; x++)
            input.Add(Place<obj_machinery_atmospherics_pipe_smart>(r, x, 7));
        var pump = Place<obj_machinery_atmospherics_components_binary_pump_on>(r, 7, 7, EAST); // node 1 west (input), node 2 east (output)
        var output = new List<obj_machinery_atmospherics>();
        for (int x = 8; x <= 11; x++)
            output.Add(Place<obj_machinery_atmospherics_pipe_smart>(r, x, 7));
        Join(r, input.Concat(new[] { pump }).Concat(output).ToArray());
        datum_pipeline netIn = Net(r, input[0]), netOut = Net(r, output[0]);
        Assert.NotSame(netIn, netOut);
        Assert.Same(netIn, r.In(() => pump.parents[1] as datum_pipeline));
        Assert.Same(netOut, r.In(() => pump.parents[2] as datum_pipeline));
        Fill(r, netIn, "n2=200;TEMP=293.15");

        r.Step(30 * Second);
        float outKpa = Pressure(r, netOut);
        Assert.InRange(outKpa, 95f, 103f); // target_pressure ONE_ATMOSPHERE
        Assert.True(Pressure(r, netIn) > outKpa, "the input keeps the rest: " + Pressure(r, netIn));

        r.In(() => pump.set_on(FALSE));
        r.In(() => netOut.air.remove(Dm.num(netOut.air.total_moles()) / 2));
        float halved = Pressure(r, netOut);
        r.Step(10 * Second);
        Assert.InRange(Pressure(r, netOut), halved - 0.5f, halved + 0.5f); // a pump that is off moves nothing
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AClosedValveKeepsTwoPipelinesApartAndAnOpenOneMixesThem()
    {
        var r = new T2Room(4206);
        var west = new List<obj_machinery_atmospherics>();
        for (int x = 3; x <= 6; x++)
            west.Add(Place<obj_machinery_atmospherics_pipe_smart>(r, x, 9));
        var valve = Place<obj_machinery_atmospherics_components_binary_valve>(r, 7, 9, EAST);
        var east = new List<obj_machinery_atmospherics>();
        for (int x = 8; x <= 11; x++)
            east.Add(Place<obj_machinery_atmospherics_pipe_smart>(r, x, 9));
        Join(r, west.Concat(new[] { valve }).Concat(east).ToArray());
        datum_pipeline netW = Net(r, west[0]), netE = Net(r, east[0]);
        Fill(r, netW, "o2=100;TEMP=293.15");
        r.Step(10 * Second);
        Assert.True(Pressure(r, netE) < 0.01f, "the closed valve holds: " + Pressure(r, netE));

        r.In(() => Waiting.NoSleep(valve.set_open(TRUE)));
        r.Step(5 * Second);
        float w = Pressure(r, netW), e = Pressure(r, netE);
        Assert.True(e > 1f, "the open valve lets the gas through: " + e);
        Assert.InRange(e, w * 0.95f, w * 1.05f); // tg's reconcile_air treats both sides as one network while it is open
        r.AssertNoRuntimes();
    }
}
