// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/river.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D304): Lavaland's lava rivers.

public static partial class GlobalProcs
{
    private const float RANDOM_UPPER_X = 200;
    private const float RANDOM_UPPER_Y = 200;

    private const float RANDOM_LOWER_X = 50;
    private const float RANDOM_LOWER_Y = 50;

    // tg: code/game/turfs/open/river.dm:7
    public static async DmTask spawn_rivers(object target_z = null, object nodes = null, object turf_type = null, object whitelist_area = null,
        float? min_x = null, float? min_y = null, float? max_x = null, float? max_y = null)
    {
        min_x ??= RANDOM_LOWER_X;
        min_y ??= RANDOM_LOWER_Y;
        max_x ??= RANDOM_UPPER_X;
        max_y ??= RANDOM_UPPER_Y;
        float num_spawned = 0;
        float width = max_x.Value - min_x.Value;
        float height = max_y.Value - min_y.Value;
        turf corner = locate(min_x.Value, min_y.Value, Dm.num(target_z));
        DmList possible_locs = CORNER_BLOCK(corner, width, height);
        while (num_spawned < Dm.num(nodes) && Dm.truthy(possible_locs.len))
        {
            var T = (turf)pick(possible_locs);
            var A = get_area(T) as area;
            if (!istype(A, (Type)whitelist_area) || Dm.truthy(Dm.bitand(T.turf_flags, NO_LAVA_GEN)))
            {
                Dm.remove(ref possible_locs, T);
                continue;
            }
            Dm.@new<obj_effect_landmark_river_waypoint>(T);
            num_spawned++;
        }

        string z_key = Dm.text("[]", target_z);
        //make some randomly pathing rivers
        foreach (obj_effect_landmark_river_waypoint waypoints in Dm.each<obj_effect_landmark_river_waypoint>(GLOB.river_waypoint_list[z_key]))
        {
            if (!Dm.eq(waypoints.z, target_z) || Dm.truthy(waypoints.connected))
                continue;
            waypoints.connected = TRUE;
            // Workaround around ChangeTurf that's safe because of when this proc is called
            turf cur_turf = get_turf(waypoints);
            cur_turf = (turf)Dm.@new((Type)turf_type, cur_turf);
            turf target_turf = get_turf(pick(Dm.sub(GLOB.river_waypoint_list[z_key], waypoints) as DmList) as atom);
            if (target_turf == null)
                break;
            // PORT: a turf is its place in BYOND, but here a new turf replaces the object, and Spread may replace the river's own
            // tile again through a neighbour: the river and its target are kept as places, their turfs looked up when used
            float cx = cur_turf.x, cy = cur_turf.y, cz = cur_turf.z;
            float tx = target_turf.x, ty = target_turf.y, tz = target_turf.z;
            bool detouring = false;
            float cur_dir = get_dir(cur_turf, target_turf);
            while (cx != tx || cy != ty || cz != tz)
            {
                cur_turf = locate(cx, cy, cz);
                target_turf = locate(tx, ty, tz);
                if (detouring) //randomly snake around a bit
                {
                    if (Dm.prob(20))
                    {
                        detouring = false;
                        cur_dir = get_dir(cur_turf, target_turf);
                    }
                }
                else if (Dm.prob(20))
                {
                    detouring = true;
                    if (Dm.prob(50))
                        cur_dir = turn(cur_dir, 45);
                    else
                        cur_dir = turn(cur_dir, -45);
                }
                else
                {
                    cur_dir = get_dir(cur_turf, target_turf);
                }

                cur_turf = get_step(cur_turf, cur_dir);
                (cx, cy, cz) = (cur_turf.x, cur_turf.y, cur_turf.z);
                var new_area = get_area(cur_turf) as area;
                if (!istype(new_area, (Type)whitelist_area) || Dm.truthy(Dm.bitand(cur_turf.turf_flags, NO_LAVA_GEN))) //Rivers will skip ruins
                {
                    detouring = false;
                    cur_dir = get_dir(cur_turf, target_turf);
                    cur_turf = get_step(cur_turf, cur_dir);
                    (cx, cy, cz) = (cur_turf.x, cur_turf.y, cur_turf.z);
                    continue;
                }

                // Workaround around ChangeTurf that's safe because of when this proc is called
                var river_turf = (turf)Dm.@new((Type)turf_type, cur_turf);
                await river_turf.Spread(25, 11, whitelist_area);
            }
        }

        foreach (obj_effect_landmark_river_waypoint waypoint in Dm.each<obj_effect_landmark_river_waypoint>(GLOB.river_waypoint_list[z_key]))
            qdel(waypoint);

        Dm.remove(ref GLOB.river_waypoint_list, z_key);
    }
}

// tg: code/game/turfs/open/river.dm:68
public partial class obj_effect_landmark_river_waypoint : obj_effect_landmark
{
    // tg: code/game/turfs/open/river.dm:72
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        string z_key = Dm.text("[]", z);
        DmList waypoints = GLOB.river_waypoint_list[z_key] as DmList;
        LAZYADD(ref waypoints, this);
        GLOB.river_waypoint_list[z_key] = waypoints;
        return dot;
    }

    // tg: code/game/turfs/open/river.dm:76
    public override object Destroy(float? force = null)
    {
        string z_key = Dm.text("[]", z);
        DmList waypoints = GLOB.river_waypoint_list[z_key] as DmList;
        LAZYREMOVE(ref waypoints, this);
        GLOB.river_waypoint_list[z_key] = waypoints;
        return base.Destroy(force);
    }
}

// tg: code/game/turfs/open/river.dm:80
public partial class turf
{
    // tg: code/game/turfs/open/river.dm:80
    public virtual async DmTask Spread(float? probability = null, float? prob_loss = null, object whitelisted_area = null)
    {
        probability ??= 30;
        prob_loss ??= 25;
        if (probability <= 0)
            return;
        DmList cardinal_turfs = Dm.list();
        DmList diagonal_turfs = Dm.list();
        Type logged_turf_type = null;
        foreach (turf canidate in Dm.each<turf>(Dm.sub(RANGE_TURFS(1, this), this)))
        {
            if (canidate == null || (Dm.truthy(canidate.density) && !ismineralturf(canidate)) || isindestructiblefloor(canidate))
                continue;

            var new_area = get_area(canidate) as area;
            if ((!istype(new_area, whitelisted_area as Type) && Dm.truthy(whitelisted_area)) || Dm.truthy(Dm.bitand(canidate.turf_flags, NO_LAVA_GEN)))
                continue;

            if (logged_turf_type == null && ismineralturf(canidate))
            {
                var mineral_canidate = (turf_closed_mineral)canidate;
                logged_turf_type = mineral_canidate.turf_type;
            }

            if (Dm.@in(get_dir(this, canidate), GLOB.cardinals))
                Dm.append(ref cardinal_turfs, canidate);
            else
                Dm.append(ref diagonal_turfs, canidate);
        }

        foreach (turf cardinal_canidate in Dm.each<turf>(cardinal_turfs)) //cardinal turfs are always changed but don't always spread
        {
            // NOTE: WE ARE SKIPPING CHANGETURF HERE
            // The calls in this proc only serve to provide a satisfactory (if it's not ALREADY this) check. They do not actually call changeturf
            // This is safe because this proc can only be run during mapload, and nothing has initialized by now so there's nothing to inherit or delete
            // PORT: ChangeTurf returns the new turf (a turf reference does not follow its replacement here)
            if (istype(cardinal_canidate, logged_turf_type))
                continue;
            var changed = await cardinal_canidate.ChangeTurf(type, baseturfs as DmList, CHANGETURF_SKIP) as turf;
            if (changed != null && Dm.prob(probability.Value))
            {
                if (Dm.truthy(baseturfs))
                    changed.baseturfs = baseturfs;
                await changed.Spread(probability - prob_loss, prob_loss, whitelisted_area);
            }
        }

        foreach (turf diagonal_canidate in Dm.each<turf>(diagonal_turfs)) //diagonal turfs only sometimes change, but will always spread if changed
        {
            // Important NOTE: SEE ABOVE
            turf changed = null;
            if (!istype(diagonal_canidate, logged_turf_type) && Dm.prob(probability.Value)
                && (changed = await diagonal_canidate.ChangeTurf(type, baseturfs as DmList, CHANGETURF_SKIP) as turf) != null)
            {
                if (Dm.truthy(baseturfs))
                    changed.baseturfs = baseturfs;
                await changed.Spread(probability - prob_loss, prob_loss, whitelisted_area);
            }
            else if (ismineralturf(diagonal_canidate))
            {
                var diagonal_mineral = (turf_closed_mineral)diagonal_canidate;
                // SEE ABOVE, THIS IS ONLY VERY RARELY SAFE
                Dm.@new(diagonal_mineral.turf_type, diagonal_mineral);
            }
        }
    }
}
