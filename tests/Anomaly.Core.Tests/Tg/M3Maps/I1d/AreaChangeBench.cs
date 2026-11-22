using System.Diagnostics;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.Tg.M3Maps.I1d;

/// <summary>
/// M3 I1d (DmList.RemoveLast): every turf of Meta's station level is moved into one station area (space's too), then the world steps until
/// SSarea_contents has cut them all from their old areas' turfs_by_zlevel (one turf at a time, oldest first,
/// area_contents.dm:68-71). Prints the ticks it took and the step times. Skipped unless ANOMALY_SLOW=1 (one boot of Meta).
/// </summary>
[Trait("Category", "Slow")]
public class AreaChangeBench
{
    private readonly ITestOutputHelper _output;

    public AreaChangeBench(ITestOutputHelper output) => _output = output;

    [SlowMetaFact]
    public void A_whole_map_area_change_drains_in_few_cheap_ticks()
    {
        World world = MetaMaps.Boot(out _);
        int moved = 0;
        using (world.Enter())
        {
            area target = locate(128, 128, 1).loc as area; // a station area: space's ~47k turfs move out of /area/space too
            for (int y = 1; y <= Dm.world.maxy; y++)
            for (int x = 1; x <= Dm.world.maxx; x++)
            {
                turf t = locate(x, y, 1);
                if (t.loc is area old && old != target)
                {
                    t.change_area(old, target);
                    moved++;
                }
            }
        }
        var steps = new List<double>();
        int left = Left(world);
        while (left > 0 && steps.Count < 4000)
        {
            var clock = Stopwatch.StartNew();
            world.Step();
            steps.Add(clock.Elapsed.TotalMilliseconds);
            left = Left(world);
        }
        steps.Sort();
        _output.WriteLine($"{moved} turfs moved; drained in {steps.Count} ticks; step p50 {steps[steps.Count / 2]:0.0} ms, p99 {steps[(int)(steps.Count * 0.99)]:0.0} ms, max {steps[^1]:0.0} ms, total {steps.Sum():0} ms");
        Assert.Equal(0, left);
    }

    private const int AllowedLooseTurfs = 100; // tg: code/controllers/subsystem/area_contents.dm:1

    private static int Left(World world)
    {
        using (world.Enter())
        {
            int left = 0;
            foreach (area a in Dm.each_cast<area>(GLOB.areas))
                foreach (object level in Dm.each(a.turfs_to_uncontain_by_zlevel))
                    if (length(level) > AllowedLooseTurfs) left += (int)length(level); // tg leaves up to 100 loose turfs
            return left;
        }
    }
}
