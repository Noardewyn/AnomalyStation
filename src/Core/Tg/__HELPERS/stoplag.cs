// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/stoplag.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//Key thing that stops lag. Cornerstone of performance in ss13, Just sitting here, in unsorted.dm. Now with dedicated file!

public static partial class GlobalProcs
{
    ///Increases delay as the server gets more overloaded, as sleeps aren't cheap and sleeping only to wake up and sleep again is wasteful
    // tg: code/__HELPERS/stoplag.dm:4  #define DELTA_CALC (file-local, #undef at :28; with the virtual tick usage of 0 it is 1, DV3)
    private static float DELTA_CALC() => max(((max(TICK_USAGE(), world.cpu) / 100) * max(Master.sleep_delta - 1, 1)), 1);

    ///returns the number of ticks slept
    // tg: code/__HELPERS/stoplag.dm:7
    public static async DmTask<object> stoplag(object initial_delay = null)
    {
        object dot = null;
        if (!Dm.truthy(Master) || Dm.lt(datum_controller_master.init_stage_completed, INITSTAGE_MAX))
        {
            await sleep(world.tick_lag);
            return 1;
        }
        if (!Dm.truthy(initial_delay))
            initial_delay = world.tick_lag;
        // Unit tests are not the normal environemnt. The mc can get absolutely thigh crushed, and sleeping procs running for ages is much more common
        // We don't want spurious hard deletes off this, so let's only sleep for the requested period of time here yeah?
        // tg: #ifdef UNIT_TESTS ... #else not translated (inactive in production)
        dot = 0;
        float i = DS2TICKS(Dm.num(initial_delay));
        do
        {
            Dm.append(ref dot, ceil(i * DELTA_CALC()));
            await sleep(i * world.tick_lag * DELTA_CALC());
            i *= 2;
        }
        while (TICK_USAGE() > min(TICK_LIMIT_TO_RUN, datum_controller_master.current_ticklimit));
        return dot;
    }
}
