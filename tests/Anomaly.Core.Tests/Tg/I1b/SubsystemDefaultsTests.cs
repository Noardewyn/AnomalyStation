using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.I1b;

/// <summary>
/// M3 I1b: the object-creating defaults of SSjob's and SSair's var blocks (the types generator's GAP list: a hand-written
/// var block initializes them in its dm_init_vars, rules 10.2), so tg's code that appends to them finds a list.
/// </summary>
public class SubsystemDefaultsTests
{
    [Fact]
    public void SSjobAndSSairHaveTheirListDefaults()
    {
        var room = TgToyRoom.Boot(5, 5, game: false);
        using (room.World.Enter())
        {
            Assert.NotNull(SSjob.latejoin_trackers); // job.dm:32
            Assert.NotNull(SSjob.prioritized_jobs);
            Assert.Equal(3f, Dm.num(SSjob.level_order.len));
            Assert.Equal((float)JP_HIGH, Dm.num(SSjob.level_order[1]));
            Assert.Equal((float)JP_LOW, Dm.num(SSjob.level_order[3]));
            Assert.Equal(1f, Dm.num(SSjob.chain_of_command[JOB_CAPTAIN]));
            Assert.Equal(7f, Dm.num(SSjob.chain_of_command[JOB_QUARTERMASTER]));
            Assert.NotNull(SSair.atmos_machinery); // air.dm:35
            Assert.NotNull(SSair.rebuild_queue);
            Assert.True(Dm.eq(SSair.map_loading, 0)); // SSair initialized (air.dm:97)
        }
    }
}
