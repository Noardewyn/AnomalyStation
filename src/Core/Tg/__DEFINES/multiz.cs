// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/multiz.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (W4.0 contracts: /turf/Initialize and Destroy and the multiz helpers call both, rules 11.1: GlobalProcs
// members, each argument evaluated once).

public static partial class GlobalProcs
{
    /// Attempt to get the turf below the provided one according to Z traits
    // tg: code/__DEFINES/multiz.dm:2
    public static object GET_TURF_BELOW(object turf)
    {
        return (Dm.truthy(Dm.bitand(((turf)turf).turf_flags, RESERVATION_TURF)) ? ((datum_turf_reservation)SSmapping.get_reservation_from_turf((turf)turf))?.get_turf_below((turf)turf) :
            (!Dm.truthy(turf) || !Dm.truthy(length(SSmapping.multiz_levels)) || !Dm.truthy(((DmList)SSmapping.multiz_levels[((atom)turf).z])[Z_LEVEL_DOWN])) ? null : (object)get_step(turf, DOWN));
    }

    /// Attempt to get the turf above the provided one according to Z traits
    // tg: code/__DEFINES/multiz.dm:6
    public static object GET_TURF_ABOVE(object turf)
    {
        return (Dm.truthy(Dm.bitand(((turf)turf).turf_flags, RESERVATION_TURF)) ? ((datum_turf_reservation)SSmapping.get_reservation_from_turf((turf)turf))?.get_turf_above((turf)turf) :
            (!Dm.truthy(turf) || !Dm.truthy(length(SSmapping.multiz_levels)) || !Dm.truthy(((DmList)SSmapping.multiz_levels[((atom)turf).z])[Z_LEVEL_UP])) ? null : (object)get_step(turf, UP));
    }
}
