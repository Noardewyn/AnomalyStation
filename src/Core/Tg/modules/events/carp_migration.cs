// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/carp_migration.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4 (D327): the whole file but the admins' carp directions (/datum/event_admin_setup, as the other events'): a school of space
// carp (and now and then a megacarp) at every carp spawn, swimming through the station toward a point inside it and off into space.
// The var blocks are generated (D111).

// tg: code/modules/events/carp_migration.dm:1
public partial class datum_round_event_control_carp_migration : datum_round_event_control
{
    // tg: code/modules/events/carp_migration.dm:14
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!HAS_TRAIT(SSstation, STATION_TRAIT_CARP_INFESTATION))
            return;
        weight *= 3;
        max_occurrences *= 2;
        earliest_start *= 0.5f;
    }
}

// tg: code/modules/events/carp_migration.dm:22
public partial class datum_round_event_carp_migration : datum_round_event
{
    // tg: code/modules/events/carp_migration.dm:36
    public override async DmTask<object> setup()
    {
        start_when = rand(40, 60);
        return null;
    }

    // tg: code/modules/events/carp_migration.dm:39
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce(Dm.text("[] have been detected near [], please stand-by.", fluff_signal, station_name()), "Lifesign Alert");
    }

    // tg: code/modules/events/carp_migration.dm:42
    public override async DmTask<object> start()
    {
        // Stores the most recent fish we spawn
        mob_living_basic_carp fish = null;
        foreach (obj_effect_landmark_carpspawn spawn_point in Dm.each<obj_effect_landmark_carpspawn>(GLOB.landmarks_list))
        {
            if (prob(95))
                fish = (mob_living_basic_carp)Dm.@new(carp_type, spawn_point.loc);
            else
            {
                fish = (mob_living_basic_carp)Dm.@new(boss_type, spawn_point.loc);
                fishannounce(fish); //Prefer to announce the megacarps over the regular fishies
            }

            string z_level_key = Dm.text("[]", spawn_point.z);
            if (!Dm.truthy(z_migration_paths[z_level_key]))
                z_migration_paths[z_level_key] = pick_carp_migration_points(z_level_key);
            if (Dm.truthy(z_migration_paths[z_level_key])) // Still possible we failed to set anything here if we're unlucky
                fish.migrate_to(z_migration_paths[z_level_key] as DmList);
        }
        fishannounce(fish);
        return null;
    }

    /// Generate two locations for carp to travel to, one in the station and one off in space
    // tg: code/modules/events/carp_migration.dm:62
    public virtual DmList pick_carp_migration_points(string z_level_key = null)
    {
        var valid_areas = Dm.list();
        DmList station_areas = GLOB.the_station_areas;
        foreach (area potential_area in Dm.each_cast<area>(SSmapping.areas_in_z[z_level_key]))
        {
            if (!Dm.truthy(is_type_in_list(potential_area, station_areas)))
                continue;
            Dm.append(valid_areas, potential_area);
        }
        turf station_turf = get_safe_random_station_turf(valid_areas);
        if (station_turf == null)
            return Dm.list();
        var exit_turf = get_edge_target_turf(station_turf, pick(GLOB.alldirs)) as turf;
        return Dm.list(WEAKREF(station_turf), WEAKREF(exit_turf));
    }

    // tg: code/modules/events/carp_migration.dm:75
    public virtual void fishannounce(atom fish = null)
    {
        if (!Dm.truthy(hasAnnounced))
        {
            announce_to_ghosts(fish); //Only anounce the first fish
            hasAnnounced = TRUE;
        }
    }
}
