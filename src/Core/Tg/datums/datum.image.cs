// Translated from tgstation (snapshot 1d99bd7f): code/datums/datum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// <summary>
/// M6 slice 7b (D156): tg's /datum filter procs (datum.dm:334-528) as BYOND runs them on an /image, which is a datum there but not in
/// the shim (docs/ai/specs/shim-ownership.md: where /image lives is an open item). The same tg lines as <see cref="datum"/>'s, with the
/// image as src (its <see cref="image.filter_data"/>, <see cref="image.filter_cache"/> and <c>filters</c>), as extension methods so a
/// translated <c>overlay.add_filter(...)</c> reads as tg's. tg runs them on mutable appearances: an atom's colour filter on a worn
/// overlay (atom_color.dm:136), the blood cutout of a filtered item (decals/blood.dm:46), a human's height filters
/// (human_update_icons.dm:813).
/// </summary>
public static class image_filter_procs
{
    // tg: code/datums/datum.dm:334
    public static void add_filter(this image src, object name = null, object priority = null, DmList @params = null, float? update = null)
    {
        update ??= TRUE;
        LAZYINITLIST(ref src.filter_data);
        LAZYINITLIST(ref src.filter_cache);
        DmList copied_parameters = @params.Copy();
        copied_parameters["name"] = name;
        copied_parameters["priority"] = priority;
        for (float index = 1; index <= length(src.filter_data); index++)
        {
            var filter_info = src.filter_data[index] as DmList;
            if (!Dm.eq(filter_info["name"], name))
                continue;
            Dm.remove(ref src.filter_data, Dm.list(filter_info));
            Dm.remove(ref src.filter_cache, src.filter_cache[index]);
            break;
        }

        // BINARY_INSERT_DEFINE(list(copied_parameters), filter_data, SORT_VAR_NO_TYPE, copied_parameters, SORT_PRIORITY_INDEX, COMPARE_KEY)
        // (__HELPERS/_lists.dm:216, inline by rules 11.1 (a), (b)), as datum.add_filter
        do
        {
            DmList __BIN_LIST = src.filter_data;
            float __BIN_CTTL = length(__BIN_LIST);
            if (!Dm.truthy(__BIN_CTTL))
                Dm.append(ref __BIN_LIST, Dm.list(copied_parameters));
            else
            {
                float __BIN_LEFT = 1;
                float __BIN_RIGHT = __BIN_CTTL;
                float __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                object __BIN_ITEM = null;
                while (__BIN_LEFT < __BIN_RIGHT)
                {
                    __BIN_ITEM = __BIN_LIST[__BIN_MID];
                    if (Dm.le(SORT_PRIORITY_INDEX(__BIN_ITEM), SORT_PRIORITY_INDEX(copied_parameters)))
                        __BIN_LEFT = __BIN_MID + 1;
                    else
                        __BIN_RIGHT = __BIN_MID;
                    __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                }
                __BIN_ITEM = __BIN_LIST[__BIN_MID];
                __BIN_MID = Dm.gt(SORT_PRIORITY_INDEX(__BIN_ITEM), SORT_PRIORITY_INDEX(copied_parameters)) ? __BIN_MID : __BIN_MID + 1;
                __BIN_LIST.Insert(__BIN_MID, Dm.list(copied_parameters));
            }
        }
        while (Dm.truthy(FALSE));

        for (float index = 1; index <= length(src.filter_data); index++)
        {
            var filter_info = src.filter_data[index] as DmList;
            if (!Dm.eq(filter_info["name"], name))
                continue;
            DmList arguments = filter_info.Copy();
            Dm.remove(ref arguments, "priority");
            src.filter_cache.Insert(index, Dm.filter(Dm.arglist(arguments)));
            break;
        }

        if (Dm.truthy(update))
            src.filters = src.filter_cache;
    }

    /// A version of add_filter that takes a list of filters to add rather than being individual, to limit appearance updates
    // tg: code/datums/datum.dm:365
    public static void add_filters(this image src, DmList filters = null, float? update = null)
    {
        update ??= TRUE;
        foreach (DmList individual_filter in Dm.each_cast<DmList>(filters))
            src.add_filter(individual_filter["name"], individual_filter["priority"], individual_filter["params"] as DmList, update: FALSE);
        if (Dm.truthy(update))
            src.filters = src.filter_cache;
    }

    /// Returns the filter associated with the passed key
    // tg: code/datums/datum.dm:491
    public static object get_filter(this image src, object name = null)
    {
        // BYOND's filters list of an image without filters is empty (a lookup gives null); the port keeps null for none.
        return (src.filters as DmList)?[name];
    }

    /// Removes the passed filter, or multiple filters, if supplied with a list.
    // tg: code/datums/datum.dm:503
    public static object remove_filter(this image src, object name_or_names = null, float? update = null)
    {
        update ??= TRUE;
        if (!Dm.truthy(src.filter_data))
            return null;
        DmList names = islist(name_or_names) ? name_or_names as DmList : Dm.list(name_or_names);
        object dot = FALSE;
        DmList new_data = Dm.list();
        DmList new_cache = Dm.list();
        for (float index = 1; index <= length(src.filter_data); index++)
        {
            var filter_info = src.filter_data[index] as DmList;
            if (!Dm.@in(filter_info["name"], names))
            {
                Dm.append(ref new_data, Dm.list(filter_info));
                Dm.append(ref new_cache, src.filter_cache[index]);
            }
        }
        src.filter_data = new_data;
        src.filter_cache = new_cache;
        if (Dm.truthy(update))
            src.filters = src.filter_cache;
        return dot;
    }

    // tg: code/datums/datum.dm:523
    public static void clear_filters(this image src)
    {
        src.filter_data = null;
        src.filter_cache = null;
        src.filters = null;
    }
}
