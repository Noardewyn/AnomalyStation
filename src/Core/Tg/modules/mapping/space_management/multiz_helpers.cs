// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/space_management/multiz_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    // tg: code/modules/mapping/space_management/multiz_helpers.dm:1
    public static object get_step_multiz(object @ref = null, object dir = null)
    {
        turf us = get_turf(@ref);
        if (Dm.truthy(Dm.bitand(dir, UP)))
        {
            Dm.mask(ref dir, Dm.bitnot(UP));
            return get_step(GET_TURF_ABOVE(us), Dm.num(dir));
        }
        if (Dm.truthy(Dm.bitand(dir, DOWN)))
        {
            Dm.mask(ref dir, Dm.bitnot(DOWN));
            return get_step(GET_TURF_BELOW(us), Dm.num(dir));
        }
        return get_step(@ref, Dm.num(dir));
    }

    // tg: code/modules/mapping/space_management/multiz_helpers.dm:11
    public static object get_dir_multiz(turf us = null, turf them = null)
    {
        us = get_turf(us);
        them = get_turf(them);
        if (!Dm.truthy(us) || !Dm.truthy(them))
            return NONE;
        if (us.z == them.z)
            return get_dir(us, them);
        else
        {
            turf T = GET_TURF_ABOVE(us) as turf;
            float dir = NONE;
            if (Dm.truthy(T) && (T.z == them.z))
                dir = UP;
            else
            {
                T = GET_TURF_BELOW(us) as turf;
                if (Dm.truthy(T) && (T.z == them.z))
                    dir = DOWN;
                else
                    return get_dir(us, them);
            }
            return Dm.bitor(dir, get_dir(us, them));
        }
    }

    // tg: code/modules/mapping/space_management/multiz_helpers.dm:31
    public static object get_lowest_turf(atom @ref = null)
    {
        turf us = get_turf(@ref);
        turf next = GET_TURF_BELOW(us) as turf;
        while (Dm.truthy(next))
        {
            us = next;
            next = GET_TURF_BELOW(us) as turf;
        }
        return us;
    }

    // I wish this was lisp
    // tg: code/modules/mapping/space_management/multiz_helpers.dm:40
    public static object get_highest_turf(atom @ref = null)
    {
        turf us = get_turf(@ref);
        turf next = GET_TURF_ABOVE(us) as turf;
        while (Dm.truthy(next))
        {
            us = next;
            next = GET_TURF_ABOVE(us) as turf;
        }
        return us;
    }
}
