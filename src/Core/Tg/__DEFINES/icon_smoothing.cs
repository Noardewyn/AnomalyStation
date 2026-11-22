// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/icon_smoothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The function-like macros of icon_smoothing.dm: QUEUE_SMOOTH and QUEUE_SMOOTH_NEIGHBORS are members; SETUP_SMOOTHING,
// PARSE_SMOOTHING_GROUPS, PARSE_CAN_SMOOTH_WITH, UNWRAP_SMOOTHING_GROUPS and ASSERT_SORTED_SMOOTHING_GROUPS are expanded
// inline at their uses (rules 11.1 (a), (d): they read and name the caller's vars; /atom/Initialize, /turf/Initialize, the
// inert init, set_can_smooth_with, set_smoothing_groups); SET_SMOOTHING_GROUPS is __HELPERS/bitflag_lists.cs's (tg defines
// it there). The constants and S_TURF/S_OBJ groups are generated (Defines). M3 P4.
public static partial class GlobalProcs
{
    // tg: code/__DEFINES/icon_smoothing.dm:61
    public static void QUEUE_SMOOTH(object thing_to_queue)
    {
        if (Dm.truthy(Dm.bitand(((atom)thing_to_queue).smoothing_flags, USES_SMOOTHING)))
            SSicon_smooth.add_to_queue((atom)thing_to_queue);
    }

    // tg: code/__DEFINES/icon_smoothing.dm:63
    public static void QUEUE_SMOOTH_NEIGHBORS(object thing_to_queue)
    {
        foreach (atom atom_neighbor in Dm.each_cast<atom>(orange(1, thing_to_queue)))
            QUEUE_SMOOTH(atom_neighbor);
    }
}
