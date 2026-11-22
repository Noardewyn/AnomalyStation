using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4b;

/// <summary>
/// The airflow seam (D141, DEVIATION DV34): SSair's pressure pass reports every turf it pushes atoms on with tg's
/// pressure_difference and pressure_direction, before the difference is zeroed, and the end of each cycle's pass; a room at
/// rest reports none; a breach reports the flow into the hole, strongest by it; the seam changes nothing of the air.
/// </summary>
public class AirflowSeamTests
{
    private const int Second = 20;

    private sealed class Recorder : IAirflowSink
    {
        public readonly Dictionary<(int, int), (float Kpa, int Dir)> Cycle = new();
        public readonly Dictionary<(int, int), float> Vents = new();
        public Dictionary<(int, int), (float Kpa, int Dir)> Last = new();
        public int Ends;

        public void Flow(turf_open turf, float difference, int direction) =>
            Cycle[((int)Dm.num(turf.x), (int)Dm.num(turf.y))] = (difference, direction);

        public void Vent(turf_open turf, float kpa)
        {
            var key = ((int)Dm.num(turf.x), (int)Dm.num(turf.y));
            Vents[key] = Vents.GetValueOrDefault(key) + kpa;
        }

        public void CycleEnd()
        {
            Ends++;
            Last = new Dictionary<(int, int), (float, int)>(Cycle);
            Cycle.Clear();
        }
    }

    [Fact]
    public void ABreachReportsTheFlowIntoTheHoleStrongestByIt()
    {
        var r = new T2Room(4105);
        var sink = new Recorder();
        AirflowSeam.Install(r.World, sink);
        r.Step(2 * Second);
        Assert.True(sink.Ends >= 3, "a cycle end every SSair fire: " + sink.Ends);
        Assert.Empty(sink.Last);

        r.In(() => DevCommands.Breach(locate(1, 8, 1)));
        r.Step(Second);
        Assert.True(sink.Last.TryGetValue((2, 8), out (float Kpa, int Dir) byHole), "the tile by the hole flows");
        Assert.Equal(8, byHole.Dir); // BYOND WEST
        Assert.True(byHole.Kpa > 16.7f, "strong enough to push a human: " + byHole.Kpa);
        foreach (((int x, int y), (float kpa, int _)) in sink.Last)
            Assert.True(kpa <= byHole.Kpa + 0.001f || Math.Abs(x - 2) + Math.Abs(y - 8) <= 1, $"({x},{y}) {kpa} kPa beats the tile by the hole ({byHole.Kpa})");
        Assert.True(sink.Last.Count >= 3, "the flow reaches past the first tile: " + sink.Last.Count);

        // the seam reads: the difference is zeroed after it as before
        Assert.Equal(0f, r.In(() => ((turf_open)locate(2, 8, 1)).pressure_difference));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSameRunWithAndWithoutASinkKeepsTheSameState()
    {
        var a = new T2Room(4105);
        var b = new T2Room(4105);
        AirflowSeam.Install(b.World, new Recorder());
        foreach (T2Room r in new[] { a, b })
        {
            r.Step(Second);
            r.In(() => DevCommands.Breach(locate(1, 8, 1)));
            r.Step(3 * Second);
        }
        Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
    }

    private static T Place<T>(T2Room r, int x, int y, float dir) where T : obj_machinery_atmospherics
        => r.In(() => Dm.@new<T>(locate(x, y, 1), TRUE, dir));

    [Fact]
    public void AVentFillingAVacuumReportsAirOutAndASiphoningScrubberAirIn()
    {
        // the airflow streams of the floor devices (D141, version 7): tg moves the gas, the seam reports what it moved
        var r = new T2Room(4202);
        var sink = new Recorder();
        AirflowSeam.Install(r.World, sink);
        var vent = Place<obj_machinery_atmospherics_components_unary_vent_pump_on>(r, 4, 5, EAST);
        var scrubber = Place<obj_machinery_atmospherics_components_unary_vent_scrubber_on>(r, 10, 10, EAST);
        var pipes = new List<obj_machinery_atmospherics> { vent };
        for (int x = 5; x <= 8; x++)
            pipes.Add(Place<obj_machinery_atmospherics_pipe_smart>(r, x, 5, SOUTH));
        var scrubberPipes = new List<obj_machinery_atmospherics> { scrubber };
        for (int x = 11; x <= 13; x++)
            scrubberPipes.Add(Place<obj_machinery_atmospherics_pipe_smart>(r, x, 10, SOUTH));
        r.In(() => SSair.setup_template_machinery(Dm.list(pipes.ToArray())));
        r.In(() => SSair.setup_template_machinery(Dm.list(scrubberPipes.ToArray())));
        r.In(() =>
        {
            ((datum_pipeline)vent.parents[1]).air.merge(SSair.parse_gas_string("o2=2500;n2=9500;TEMP=293.15") as datum_gas_mixture);
            for (int x = 2; x < T2Room.Size; x++)
                for (int y = 2; y < T2Room.Size; y++)
                    if (locate(x, y, 1) is turf_open t)
                    {
                        t.air = SSair.parse_gas_string("o2=5;n2=20;TEMP=293.15") as datum_gas_mixture;
                        SSair.add_to_active(t);
                    }
            scrubber.set_on(TRUE);
            scrubber.set_scrubbing(ATMOS_DIRECTION_SIPHONING);
        });
        r.Step(2 * Second);
        Assert.True(sink.Vents.TryGetValue((4, 5), out float outKpa) && outKpa > 0.5f, "the vent blows air out: " + outKpa);
        Assert.True(sink.Vents.TryGetValue((10, 10), out float inKpa) && inKpa < 0f, "the scrubber siphons air in: " + inKpa);
        r.AssertNoRuntimes();
    }
}
