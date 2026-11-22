// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/string_lists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/string_lists.dm:1  GLOBAL_LIST_EMPTY(string_lists)
    public DmList string_lists;

    // tg: code/__HELPERS/string_lists.dm:1  (GLOBAL_MANAGED(string_lists, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstring_lists()
    {
        string_lists = Dm.list();
        Dm.append(ref gvars_datum_init_order, "string_lists");
    }
}

public static partial class GlobalProcs
{
    /**
     * Caches lists with non-numeric stringify-able values (text or typepath).
     */
    // tg: code/__HELPERS/string_lists.dm:6
    public static object string_list(DmList values = null)
    {
        object dot = null;
        string string_id = values.Join("-");

        dot = GLOB.string_lists[string_id];

        if (Dm.truthy(dot))
            return dot;

        return GLOB.string_lists[string_id] = values;
    }

    ///A wrapper for baseturf string lists, to offer support of non list values, and a stack_trace if we have major issues
    // tg: code/__HELPERS/string_lists.dm:17
    public static async DmTask<object> baseturfs_string_list(object values = null, turf baseturf_holder = null)
    {
        if (!islist(values))
            return values; //baseturf things
        // return values
        if (length(values) > 10)
        {
            stack_trace(Dm.text("The baseturfs list of [] at [], [], [] is [], it should never be this long, investigate. I've set baseturfs to a flashing wall as a visual queue", baseturf_holder, baseturf_holder.x, baseturf_holder.y, baseturf_holder.x, length(values)), "code/__HELPERS/string_lists.dm", 22);
            // tg :23  baseturf_holder.ChangeTurf(/turf/closed/indestructible/baseturfs_ded, list(/turf/closed/indestructible/baseturfs_ded), flags = CHANGETURF_FORCEOP)
            // GAP(M3): /turf/proc/ChangeTurf (game/turfs/change_turf.dm) is not declared yet (scope R13: ChangeTurf is M3).
            Dm.NotTranslated("code/__HELPERS/string_lists.dm:23", "baseturfs_string_list: ChangeTurf to the flashing baseturfs_ded wall (M3, game/turfs/change_turf.dm)");
            return string_list(Dm.list(typeof(turf_closed_indestructible_baseturfs_ded))); //I want this reported god damn it
        }

        return string_list((DmList)values);
    }
}

// tg: code/__HELPERS/string_lists.dm:28
[DmPath("/turf/closed/indestructible/baseturfs_ded")]
public partial class turf_closed_indestructible_baseturfs_ded : turf_closed_indestructible
{
    // tg: code/__HELPERS/string_lists.dm:29-32
    public turf_closed_indestructible_baseturfs_ded()
    {
        name = "Report this";
        desc = "It looks like base turfs went to the fucking moon, TELL YOUR LOCAL CODER TODAY";
        icon = Dm.resource("icons/turf/debug.dmi");
        icon_state = "fucked_baseturfs";
    }
}
