// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/anomaly/anomaly_placer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;


namespace Anomaly.Core.Tg;

// The M7 delamination slice (G6, D167): the whole file (the dimensional anomaly relocates with it; the anomaly events come with the
// random events, M10). The type has no vars of its own but the static below.

// tg: code/modules/events/anomaly/anomaly_placer.dm:1
public partial class datum_anomaly_placer : datum
{
    // tg: code/modules/events/anomaly/anomaly_placer.dm:2  var/static/list/allowed_areas (a type-level static, rules 10.6)
    public static ref DmList allowed_areas => ref TgState.Current.datum_anomaly_placer__allowed_areas;

    // tg: code/modules/events/anomaly/anomaly_placer.dm:59  var/static/list/safe_area_types (a proc-level static, rules 10.6)
    private static ref DmList generateAllowedAreas__safe_area_types => ref TgState.Current.datum_anomaly_placer__generateAllowedAreas__safe_area_types;

    // tg: code/modules/events/anomaly/anomaly_placer.dm:70  var/static/list/unsafe_area_subtypes (a proc-level static, rules 10.6)
    private static ref DmList generateAllowedAreas__unsafe_area_subtypes => ref TgState.Current.datum_anomaly_placer__generateAllowedAreas__unsafe_area_subtypes;

    /**
     * Returns an area which is safe to place an anomaly inside.
     */
    // tg: code/modules/events/anomaly/anomaly_placer.dm:7
    public virtual object findValidArea()
    {
        if (!Dm.truthy(allowed_areas))
            generateAllowedAreas();
        DmList possible_areas = typecache_filter_list(GLOB.areas, allowed_areas) as DmList;
        if (!Dm.truthy(length(possible_areas)))
            throw Dm.CRASH("No valid areas for anomaly found.", "code/modules/events/anomaly/anomaly_placer.dm", 12);

        area landing_area = pick(possible_areas) as area;
        DmList turf_test = get_area_turfs(landing_area) as DmList;
        if (!Dm.truthy(turf_test.len))
            throw Dm.CRASH(Dm.text("Anomaly : No valid turfs found for [] - []", landing_area, landing_area.type), "code/modules/events/anomaly/anomaly_placer.dm", 17);

        return landing_area;
    }

    /**
     * Returns a turf which is safe to place an anomaly on.
     *
     * Arguments
     * * target_area - Area to return a turf from.
     */
    // tg: code/modules/events/anomaly/anomaly_placer.dm:27
    public virtual async DmTask<object> findValidTurf(area target_area = null)
    {
        DmList valid_turfs = Dm.list();
        foreach (turf try_turf in Dm.each_cast<turf>(get_area_turfs(target_area)))
        {
            if (!Dm.truthy(await is_valid_destination(try_turf)))
                continue;
            Dm.append(ref valid_turfs, try_turf);
        }

        if (!Dm.truthy(valid_turfs.len))
            throw Dm.CRASH(Dm.text("Dimensional anomaly attempted to reach invalid location [].", target_area), "code/modules/events/anomaly/anomaly_placer.dm", 35);

        return pick(valid_turfs);
    }

    /**
     * Returns true if the provided turf is valid to place an anomaly on.
     *
     * Arguments
     * * tested - Turf to try landing on.
     */
    // tg: code/modules/events/anomaly/anomaly_placer.dm:44
    public virtual async DmTask<object> is_valid_destination(turf tested = null)
    {
        if (isspaceturf(tested))
            return FALSE;
        if (Dm.truthy(await tested.is_blocked_turf(exclude_mobs: TRUE)))
            return FALSE;
        if (islava(tested))
            return FALSE;
        if (ischasm(tested))
            return FALSE;
        return TRUE;
    }

    /**
     * Populates the allowed areas list.
     */
    // tg: code/modules/events/anomaly/anomaly_placer.dm:57
    public virtual void generateAllowedAreas()
    {
        //Places that shouldn't explode
        generateAllowedAreas__safe_area_types ??= typecacheof(Dm.list(
            typeof(area_station_ai_satellite_chamber),
            typeof(area_station_ai_upload_chamber),
            typeof(area_station_engineering),
            typeof(area_station_science_ordnance_bomb),
            typeof(area_station_solars),
            typeof(area_station_holodeck),
            typeof(area_station_maintenance)
            )) as DmList;

        //Subtypes from the above that actually should explode.
        generateAllowedAreas__unsafe_area_subtypes ??= typecacheof(Dm.list(typeof(area_station_engineering_break_room))) as DmList;

        allowed_areas = Dm.add(Dm.sub(make_associative(GLOB.the_station_areas), generateAllowedAreas__safe_area_types), generateAllowedAreas__unsafe_area_subtypes) as DmList;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/events/anomaly/anomaly_placer.dm:2  var/static/list/allowed_areas
    internal DmList datum_anomaly_placer__allowed_areas;

    // tg: code/modules/events/anomaly/anomaly_placer.dm:59  var/static/list/safe_area_types
    internal DmList datum_anomaly_placer__generateAllowedAreas__safe_area_types;

    // tg: code/modules/events/anomaly/anomaly_placer.dm:70  var/static/list/unsafe_area_subtypes
    internal DmList datum_anomaly_placer__generateAllowedAreas__unsafe_area_subtypes;
}
