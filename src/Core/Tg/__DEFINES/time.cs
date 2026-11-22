// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/time.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only STATION_TIME_PASSED, CURRENT_STATION_YEAR (M9 slice E), DS2TICKS, TICKS2DS and MS2DS translated (the MC, SStimer, SSgarbage and
// server_timestamp use them). The constants of time.dm are generated into Defines; TICKS (:78) is rewritten at each use as
// `* world.tick_lag` (rules 11.2); the rest of its function-like macros is completed by the package that translates it.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/time.dm:8  (its variadic ... is never read)
    public static float STATION_TIME_PASSED(params object[] __extra) => (world.time - SSticker.round_start_time);

    /// Define that just has the current in-universe year for use in whatever context you might want to display that in. (For example, 2022 -> 2562 given a 540 year offset)
    // tg: code/__DEFINES/time.dm:11  #define CURRENT_STATION_YEAR (M9 slice E: a PDA's date)
    public static float CURRENT_STATION_YEAR => Dm.num(GLOB.year_integer) + STATION_YEAR_OFFSET;

    // tg: code/__DEFINES/time.dm:84
    public static float MS2DS(float T) => (T) * MILLISECONDS;

    // tg: code/__DEFINES/time.dm:80
    public static float DS2TICKS(float DS) => (Dm.div((DS), world.tick_lag));

    // tg: code/__DEFINES/time.dm:82  ((T) TICKS, with TICKS rewritten as * world.tick_lag, rules 11.2)
    public static float TICKS2DS(float T) => ((T) * world.tick_lag);
}
