using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.T2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.MultiZ;

/// <summary>
/// DV34 on two levels: tg's get_dir ignores z, so a turf whose biggest share went through open space has pressure_direction 0;
/// the airflow seam reports it as DOWN (or UP), so the clients draw the air of the level above pouring down to a breach.
/// </summary>
public class AirflowVerticalTests
{
    private sealed class Recorder : IAirflowSink
    {
        private readonly Dictionary<(int, int, int), int> _cycle = new();
        public readonly HashSet<((int, int, int) Cell, int Dir)> Seen = new();

        public void Flow(turf_open t, float difference, int direction) =>
            _cycle[((int)Dm.num(t.x), (int)Dm.num(t.y), (int)Dm.num(t.z))] = direction;

        public void CycleEnd()
        {
            foreach (var (cell, dir) in _cycle) Seen.Add((cell, dir));
            _cycle.Clear();
        }
    }

    [Fact]
    public void TheOpenSpaceOverABreachedRoomFlowsDown()
    {
        World world = T2Maps.Boot("multiz", out TgTestWorld.Log log);
        TgTestWorld.RunLevel(world, RUNLEVEL_GAME);
        var sink = new Recorder();
        AirflowSeam.Install(world, sink);
        world.Step(40);
        using (world.Enter()) DevCommands.Breach(locate(1, 3, 1));
        world.Step(60);
        Assert.Contains(((3, 3, 2), (int)DOWN), sink.Seen);
        Assert.DoesNotContain(sink.Seen, s => s.Dir == 0);
        using (world.Enter()) Assert.Equal(0f, ((turf_open)locate(3, 3, 2)).pressure_direction); // tg's own, which pushes nothing
        Assert.Empty(log.Runtimes);
    }
}
