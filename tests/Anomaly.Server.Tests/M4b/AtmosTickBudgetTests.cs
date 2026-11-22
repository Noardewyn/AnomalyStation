using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Server.Tests.Replication;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.M4b;

/// <summary>
/// DV33 (amends DV3): SSair charges a fixed virtual cost per unit of work to world.tick_usage, so tg's own MC_TICK_CHECKs
/// pause it past its share of the tick and the Master resumes it in the next ticks. A fire through a 48 x 48 room of lean
/// plasma (hundreds of hotspots, the room's turfs active) still burns the room, SSair's cycles spread over several ticks,
/// and no tick is charged much past the Master's running limit.
/// </summary>
public class AtmosTickBudgetTests
{
    private readonly ITestOutputHelper _out;

    public AtmosTickBudgetTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void ARoomWideFireIsSpreadOverTicksWithinTheBudget()
    {
        var w = ReplicationWorld.Boot(size: 48, movers: 0, containers: 0, notTranslatedThrows: false);
        w.ScenarioOn = false;
        using (w.World.Enter())
        {
            DevCommands.Gas("o2=22;n2=82;plasma=5;TEMP=293.15");
            DevCommands.Ignite(locate(24, 24, 1) as turf);
        }
        float maxUsage = 0;
        int pausedTicks = 0, peakHotspots = 0;
        double worstTickMs = 0;
        var state = DmWorldState.Of(w.World);
        for (int i = 0; i < 40 * 20; i++)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            w.World.Step();
            worstTickMs = Math.Max(worstTickMs, clock.Elapsed.TotalMilliseconds);
            maxUsage = Math.Max(maxUsage, state.TickUsage);
            using (w.World.Enter())
            {
                if (Dm.eq(SSair.state, SS_PAUSED) || Dm.eq(SSair.state, SS_PAUSING)) pausedTicks++;
                peakHotspots = Math.Max(peakHotspots, (int)SSair.hotspots.len);
            }
        }
        float burnt;
        using (w.World.Enter())
        {
            burnt = 0;
            for (int x = 2; x < 48; x++)
                for (int y = 2; y < 48; y++)
                    if (locate(x, y, 1) is turf_open t && Dm.num(t.air?.moles[typeof(datum_gas_carbon_dioxide)]) > 0.1f) burnt++;
        }
        _out.WriteLine($"peak hotspots {peakHotspots}, ticks with SSair paused {pausedTicks}, max charged usage {maxUsage:0.0} %, worst tick {worstTickMs:0.0} ms, turfs with fire products {burnt}");
        Assert.True(peakHotspots > 200, "the fire swept the room: " + peakHotspots);
        Assert.True(burnt > 900, "the room burnt: " + burnt); // 961, as without the budget
        Assert.True(pausedTicks > 0, "SSair yielded to the next tick at least once");
        // the Master stops handing out the tick at TICK_LIMIT_RUNNING; one unit of work may run past it
        Assert.True(maxUsage <= TICK_LIMIT_RUNNING() + 5 * AtmosTickCost.ActiveTurf, "charged " + maxUsage);
    }
}
