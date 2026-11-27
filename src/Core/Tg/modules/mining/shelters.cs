// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/shelters.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The survival capsules' shelter templates and where they may unfold: not on walls (but rock), in a shuttle or the holodeck, over a dense
// anchored thing or a banned one (the necropolis' stone tiles), nor sticking out of the map; and the fishing capsule's spots. SSmapping
// skips a template whose converted map the host did not mount (DV611).

///Map templates used for bluespace survival capsules.
// tg: code/modules/mining/shelters.dm:2
public partial class datum_map_template_shelter : datum_map_template
{
    // tg: code/modules/mining/shelters.dm:17
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        blacklisted_turfs = typecacheof(typeof(turf_closed)) as DmList;
        banned_areas = typecacheof(Dm.list(typeof(area_shuttle), typeof(area_station_holodeck))) as DmList;
    }

    // tg: code/modules/mining/shelters.dm:22
    public virtual object check_deploy(turf deploy_location = null, obj_item_survivalcapsule capsule = null, float? ignore_flags = null)
    {
        ignore_flags ??= NONE;
        var affected = (DmList)get_affected_turfs(deploy_location, centered: TRUE);
        foreach (turf turf in Dm.each<turf>(affected))
        {
            var area = get_area(turf) as area;
            if (Dm.truthy(is_type_in_typecache(area, banned_areas)))
                return SHELTER_DEPLOY_BAD_AREA;

            if (Dm.truthy(is_type_in_typecache(turf, blacklisted_turfs)))
                return SHELTER_DEPLOY_BAD_TURFS;

            foreach (obj @object in Dm.each<obj>(turf))
            {
                if (!Dm.truthy(Dm.bitand(ignore_flags, CAPSULE_IGNORE_ANCHORED_OBJECTS)) && Dm.truthy(@object.density) && Dm.truthy(@object.anchored))
                    return SHELTER_DEPLOY_ANCHORED_OBJECTS;
                if (!Dm.truthy(Dm.bitand(ignore_flags, CAPSULE_IGNORE_BANNED_OBJECTS)) && Dm.truthy(is_type_in_typecache(@object, banned_objects)))
                    return SHELTER_DEPLOY_BANNED_OBJECTS;
            }
        }

        // Check if the shelter sticks out of map borders
        float shelter_origin_x = deploy_location.x - Dm.round(width / 2);
        if (shelter_origin_x <= 1 || shelter_origin_x + width > Dm.world.maxx)
            return SHELTER_DEPLOY_OUTSIDE_MAP;
        float shelter_origin_y = deploy_location.y - Dm.round(height / 2);
        if (shelter_origin_y <= 1 || shelter_origin_y + height > Dm.world.maxy)
            return SHELTER_DEPLOY_OUTSIDE_MAP;

        return SHELTER_DEPLOY_ALLOWED;
    }
}

// tg: code/modules/mining/shelters.dm:48
public partial class datum_map_template_shelter_alpha : datum_map_template_shelter
{
    // tg: code/modules/mining/shelters.dm:57
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.remove(ref blacklisted_turfs, typesof(typeof(turf_closed_mineral)));
        banned_objects = typecacheof(typeof(obj_structure_stone_tile)) as DmList;
    }
}

// tg: code/modules/mining/shelters.dm:62
public partial class datum_map_template_shelter_beta : datum_map_template_shelter
{
    // tg: code/modules/mining/shelters.dm:72
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.remove(ref blacklisted_turfs, typesof(typeof(turf_closed_mineral)));
        banned_objects = typecacheof(typeof(obj_structure_stone_tile)) as DmList;
    }
}

// tg: code/modules/mining/shelters.dm:77
public partial class datum_map_template_shelter_charlie : datum_map_template_shelter
{
    // tg: code/modules/mining/shelters.dm:87
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.remove(ref blacklisted_turfs, typesof(typeof(turf_closed_mineral)));
        banned_objects = typecacheof(typeof(obj_structure_stone_tile)) as DmList;
    }
}

// tg: code/modules/mining/shelters.dm:92
public partial class datum_map_template_shelter_toilet : datum_map_template_shelter
{
    // tg: code/modules/mining/shelters.dm:100
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.remove(ref blacklisted_turfs, typesof(typeof(turf_closed_mineral)));
        banned_objects = typecacheof(typeof(obj_structure_stone_tile)) as DmList;
    }
}

///Not exactly mining shelters, but they make use of survival capsules code.
// tg: code/modules/mining/shelters.dm:105
public partial class datum_map_template_shelter_fishing : datum_map_template_shelter
{
    // tg: code/modules/mining/shelters.dm:120
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.remove(ref blacklisted_turfs, typesof(typeof(turf_closed_mineral)));
        Dm.append(ref blacklisted_turfs, typecacheof(typeof(turf_open_openspace)));
        // Stop the capsule from being used around pipes and cables (if not emagged) cuz it'd look bad and a bit disruptive.
        banned_objects = typecacheof(Dm.list(
            typeof(obj_structure_disposalpipe),
            typeof(obj_machinery_atmospherics_pipe),
            typeof(obj_structure_cable),
            typeof(obj_structure_transit_tube))) as DmList;
    }
}
