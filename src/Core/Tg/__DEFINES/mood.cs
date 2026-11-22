// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/mood.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps MS1): the function-like macros HAS_PERSONALITY and IS_DESENSITIZED (rules 11.1: GlobalProcs members); the
// constants are generated (Defines).

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/mood.dm:109  #define HAS_PERSONALITY(mob, personality) (LAZYACCESS(mob.personalities, personality))
    public static object HAS_PERSONALITY(object mob, object personality) => LAZYACCESS(((mob_living)mob).personalities, personality);

    // tg: code/__DEFINES/mood.dm:124  #define IS_DESENSITIZED(mob) ((mob.mind?.desensitized_level || 1) <= DESENSITIZED_THRESHOLD)
    public static bool IS_DESENSITIZED(object mob)
    {
        float? level = ((mob)mob).mind?.desensitized_level;
        return (Dm.truthy(level) ? Dm.num(level) : 1) <= DESENSITIZED_THRESHOLD;
    }
}
