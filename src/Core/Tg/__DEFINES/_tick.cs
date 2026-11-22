// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/_tick.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The constants of _tick.dm (MAPTICK_MC_MIN_RESERVE, TICK_BYOND_RESERVE, TICK_LIMIT_TO_RUN, TICK_LIMIT_MC) are generated
// into Defines. The tick usage is virtual (DV3, DV33): TICK_USAGE reads world.tick_usage (SSair's charged work), TICK_CHECK is false and
// CHECK_TICK never reaches its stoplag() (rules 11.3).

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/_tick.dm:3  (world.map_cpu is always 0, DV3)
    public static float MAPTICK_LAST_INTERNAL_TICK_USAGE() => (world.map_cpu);

    /// Tick limit while running normally
    // tg: code/__DEFINES/_tick.dm:7  (world.map_cpu is always 0, DV3)
    public static float TICK_LIMIT_RUNNING() => (max(100 - TICK_BYOND_RESERVE - MAPTICK_LAST_INTERNAL_TICK_USAGE(), MAPTICK_MC_MIN_RESERVE));

    /// for general usage of tick_usage
    // tg: code/__DEFINES/_tick.dm:14
    public static float TICK_USAGE() => world.tick_usage;
    /// to be used where the result isn't checked
    // tg: code/__DEFINES/_tick.dm:16
    public static float TICK_USAGE_REAL() => world.tick_usage;

    /// Returns true if tick_usage is above the limit
    // tg: code/__DEFINES/_tick.dm:19  (rules 11.3: a bool member). DEVIATION(DV33): the generic checks keep DV3's view of a
    // tick with no usage, so CHECK_TICK in a synchronous proc never reaches stoplag() and a verb never yields; only the MC's
    // scheduling and MC_TICK_CHECK read the usage SSair charges.
    public static bool TICK_CHECK() => false;

    /// runs stoplag if tick_usage is above the limit
    // tg: code/__DEFINES/_tick.dm:21  (rules 11.3: a statement; it never reaches stoplag() while TICK_CHECK is false, DV3,
    // and a synchronous member calls the coloured stoplag through Waiting.NoSleep, waiting-model.md 4.4)
    public static void CHECK_TICK()
    {
        if (TICK_CHECK())
            Waiting.NoSleep(stoplag());
    }

    /// Checks if a sleeping proc is running before or after the master controller
    // tg: code/__DEFINES/_tick.dm:24
    public static bool RUNNING_BEFORE_MASTER() => (Master.last_run != null && !Dm.eq(Master.last_run, world.time));
    /// Returns true if a verb ought to yield to the MC (IE: queue up to be processed by a subsystem)
    // tg: code/__DEFINES/_tick.dm:26
    public static bool VERB_SHOULD_YIELD() => (TICK_CHECK() || RUNNING_BEFORE_MASTER());

    /// Returns true if tick usage is above 95, for high priority usage
    // tg: code/__DEFINES/_tick.dm:29
    public static bool TICK_CHECK_HIGH_PRIORITY() => false; // DEVIATION(DV33): as TICK_CHECK
    /// runs stoplag if tick_usage is above 95, for high priority usage
    // tg: code/__DEFINES/_tick.dm:31  (a statement, as CHECK_TICK)
    public static void CHECK_TICK_HIGH_PRIORITY()
    {
        if (TICK_CHECK_HIGH_PRIORITY())
            Waiting.NoSleep(stoplag());
    }
}
