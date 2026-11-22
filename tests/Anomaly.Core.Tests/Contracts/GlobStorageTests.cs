using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>
/// Rules 10.6: the GLOBAL_* vars are DM <c>var/global</c> vars, so they exist before the GLOB datum. tg writes
/// <c>GLOB.config_error_log</c> in <c>/world/Genesis</c> (game/world.dm:91) and reads <c>GLOB.sortInstance</c> in
/// <c>Master/New</c> (controllers/master.dm:110) before <c>master.dm:128</c> creates GLOB.
/// </summary>
public class GlobStorageTests
{
    [Fact]
    public void GlobalVarsAreReachableBeforeTheGlobDatumAndMoveIntoIt()
    {
        var w = new World(seed: 1);
        using (w.Enter())
        {
            Assert.Null(GLOB__var);
            Assert.NotNull(GLOB);
            Assert.Null(GLOB.gvars_datum_init_order);
            DmList early = Dm.list("a");
            GLOB.gvars_datum_init_order = early;
            Assert.Null(GLOB__var);

            var glob = new datum_controller_global_vars() /* the C# constructor: GLOB's New queues a timer (globals.dm:24) */;
            GLOB__var = glob;
            Assert.Same(glob, GLOB);
            Assert.Same(early, glob.gvars_datum_init_order);
        }
    }

    [Fact]
    public void ThePreGlobStorageDoesNotChangeTheWorldHash()
    {
        var a = new World(seed: 1);
        var b = new World(seed: 1);
        using (a.Enter())
            GLOB.gvars_datum_init_order = null; // creates the pre-GLOB storage
        using (b.Enter())
            _ = TgState.Current;
        Assert.Equal(b.ComputeStateHash(), a.ComputeStateHash());
    }
}
