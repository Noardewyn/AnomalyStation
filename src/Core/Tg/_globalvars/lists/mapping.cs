// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/mapping.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the W4.0 contracts completed it from the wave-3 partial, which held GLOB.areas_by_type only).

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/lists/mapping.dm:1  GLOBAL_LIST_INIT(cardinals, list(...))
    public DmList cardinals;

    // tg: code/_globalvars/lists/mapping.dm:1  (GLOBAL_MANAGED(cardinals, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcardinals()
    {
        cardinals = Dm.list(
            NORTH,
            SOUTH,
            EAST,
            WEST);
        Dm.append(ref gvars_datum_init_order, "cardinals");
    }

    // tg: code/_globalvars/lists/mapping.dm:7  GLOBAL_LIST_INIT(cardinals_multiz, list(...))
    public DmList cardinals_multiz;

    // tg: code/_globalvars/lists/mapping.dm:7  (GLOBAL_MANAGED(cardinals_multiz, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcardinals_multiz()
    {
        cardinals_multiz = Dm.list(
            NORTH,
            SOUTH,
            EAST,
            WEST,
            UP,
            DOWN);
        Dm.append(ref gvars_datum_init_order, "cardinals_multiz");
    }

    // tg: code/_globalvars/lists/mapping.dm:15  GLOBAL_LIST_INIT(diagonals, list(...))
    public DmList diagonals;

    // tg: code/_globalvars/lists/mapping.dm:15  (GLOBAL_MANAGED(diagonals, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldiagonals()
    {
        diagonals = Dm.list(
            NORTHEAST,
            NORTHWEST,
            SOUTHEAST,
            SOUTHWEST);
        Dm.append(ref gvars_datum_init_order, "diagonals");
    }

    // tg: code/_globalvars/lists/mapping.dm:21  GLOBAL_LIST_INIT(corners_multiz, list(...))
    public DmList corners_multiz;

    // tg: code/_globalvars/lists/mapping.dm:21  (GLOBAL_MANAGED(corners_multiz, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcorners_multiz()
    {
        corners_multiz = Dm.list(
            Dm.bitor(UP, NORTHEAST),
            Dm.bitor(UP, NORTHWEST),
            Dm.bitor(UP, SOUTHEAST),
            Dm.bitor(UP, SOUTHWEST),
            Dm.bitor(DOWN, NORTHEAST),
            Dm.bitor(DOWN, NORTHWEST),
            Dm.bitor(DOWN, SOUTHEAST),
            Dm.bitor(DOWN, SOUTHWEST));
        Dm.append(ref gvars_datum_init_order, "corners_multiz");
    }

    // tg: code/_globalvars/lists/mapping.dm:31  GLOBAL_LIST_INIT(diagonals_multiz, list(...))
    public DmList diagonals_multiz;

    // tg: code/_globalvars/lists/mapping.dm:31  (GLOBAL_MANAGED(diagonals_multiz, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldiagonals_multiz()
    {
        diagonals_multiz = Dm.list(
            NORTHEAST,
            NORTHWEST,
            SOUTHEAST,
            SOUTHWEST,

            Dm.bitor(UP, NORTH),
            Dm.bitor(UP, SOUTH),
            Dm.bitor(UP, EAST),
            Dm.bitor(UP, WEST),
            Dm.bitor(UP, NORTHEAST),
            Dm.bitor(UP, NORTHWEST),
            Dm.bitor(UP, SOUTHEAST),
            Dm.bitor(UP, SOUTHWEST),

            Dm.bitor(DOWN, NORTH),
            Dm.bitor(DOWN, SOUTH),
            Dm.bitor(DOWN, EAST),
            Dm.bitor(DOWN, WEST),
            Dm.bitor(DOWN, NORTHEAST),
            Dm.bitor(DOWN, NORTHWEST),
            Dm.bitor(DOWN, SOUTHEAST),
            Dm.bitor(DOWN, SOUTHWEST));
        Dm.append(ref gvars_datum_init_order, "diagonals_multiz");
    }

    // tg: code/_globalvars/lists/mapping.dm:55  GLOBAL_LIST_INIT(alldirs_multiz, list(...))
    public DmList alldirs_multiz;

    // tg: code/_globalvars/lists/mapping.dm:55  (GLOBAL_MANAGED(alldirs_multiz, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalalldirs_multiz()
    {
        alldirs_multiz = Dm.list(
            NORTH,
            SOUTH,
            EAST,
            WEST,
            NORTHEAST,
            NORTHWEST,
            SOUTHEAST,
            SOUTHWEST,

            UP,
            Dm.bitor(UP, NORTH),
            Dm.bitor(UP, SOUTH),
            Dm.bitor(UP, EAST),
            Dm.bitor(UP, WEST),
            Dm.bitor(UP, NORTHEAST),
            Dm.bitor(UP, NORTHWEST),
            Dm.bitor(UP, SOUTHEAST),
            Dm.bitor(UP, SOUTHWEST),

            DOWN,
            Dm.bitor(DOWN, NORTH),
            Dm.bitor(DOWN, SOUTH),
            Dm.bitor(DOWN, EAST),
            Dm.bitor(DOWN, WEST),
            Dm.bitor(DOWN, NORTHEAST),
            Dm.bitor(DOWN, NORTHWEST),
            Dm.bitor(DOWN, SOUTHEAST),
            Dm.bitor(DOWN, SOUTHWEST));
        Dm.append(ref gvars_datum_init_order, "alldirs_multiz");
    }

    // tg: code/_globalvars/lists/mapping.dm:85  GLOBAL_LIST_INIT(alldirs, list(...))
    public DmList alldirs;

    // tg: code/_globalvars/lists/mapping.dm:85  (GLOBAL_MANAGED(alldirs, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalalldirs()
    {
        alldirs = Dm.list(
            NORTH,
            SOUTH,
            EAST,
            WEST,
            NORTHEAST,
            NORTHWEST,
            SOUTHEAST,
            SOUTHWEST);
        Dm.append(ref gvars_datum_init_order, "alldirs");
    }

    // tg: code/_globalvars/lists/mapping.dm:95  GLOBAL_LIST_INIT(alldirs_dmi_order, list(...))
    public DmList alldirs_dmi_order;

    // tg: code/_globalvars/lists/mapping.dm:95  (GLOBAL_MANAGED(alldirs_dmi_order, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalalldirs_dmi_order()
    {
        alldirs_dmi_order = Dm.list(
            SOUTH,
            NORTH,
            EAST,
            WEST,
            SOUTHEAST,
            SOUTHWEST,
            NORTHEAST,
            NORTHWEST);
        Dm.append(ref gvars_datum_init_order, "alldirs_dmi_order");
    }

    // tg: code/_globalvars/lists/mapping.dm:106  GLOBAL_LIST_INIT(cardinal_angles, list(...))
    public DmList cardinal_angles;

    // tg: code/_globalvars/lists/mapping.dm:106  (GLOBAL_MANAGED(cardinal_angles, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcardinal_angles()
    {
        cardinal_angles = Dm.list(
            Dm.kv(Dm.text("[]", NORTH), 0),
            Dm.kv(Dm.text("[]", SOUTH), 180),
            Dm.kv(Dm.text("[]", EAST), 90),
            Dm.kv(Dm.text("[]", WEST), 270));
        Dm.append(ref gvars_datum_init_order, "cardinal_angles");
    }

    /// list of all landmarks created
    // tg: code/_globalvars/lists/mapping.dm:114  GLOBAL_LIST_EMPTY(landmarks_list)
    public DmList landmarks_list;

    // tg: code/_globalvars/lists/mapping.dm:114  (GLOBAL_MANAGED(landmarks_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGloballandmarks_list()
    {
        landmarks_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "landmarks_list");
    }

    /// list of all job spawn points created
    // tg: code/_globalvars/lists/mapping.dm:116  GLOBAL_LIST_EMPTY(start_landmarks_list)
    public DmList start_landmarks_list;

    // tg: code/_globalvars/lists/mapping.dm:116  (GLOBAL_MANAGED(start_landmarks_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstart_landmarks_list()
    {
        start_landmarks_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "start_landmarks_list");
    }

    /// list of all department security spawns
    // tg: code/_globalvars/lists/mapping.dm:118  GLOBAL_LIST_EMPTY(department_security_spawns)
    public DmList department_security_spawns;

    // tg: code/_globalvars/lists/mapping.dm:118  (GLOBAL_MANAGED(department_security_spawns, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldepartment_security_spawns()
    {
        department_security_spawns = Dm.list();
        Dm.append(ref gvars_datum_init_order, "department_security_spawns");
    }

    /// List of generic landmarks placed around the map where there are likely to be players and are identifiable at a glance -
    /// Such as public hallways, department rooms, head of staff offices, and non-generic maintenance locations
    // tg: code/_globalvars/lists/mapping.dm:121  GLOBAL_LIST_EMPTY(generic_event_spawns)
    public DmList generic_event_spawns;

    // tg: code/_globalvars/lists/mapping.dm:121  (GLOBAL_MANAGED(generic_event_spawns, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgeneric_event_spawns()
    {
        generic_event_spawns = Dm.list();
        Dm.append(ref gvars_datum_init_order, "generic_event_spawns");
    }

    /// Assoc list of "job titles" to "job landmarks"
    /// These will take precedence over normal job spawnpoints if created,
    /// essentially allowing a user to override generic job spawnpoints with a specific one
    // tg: code/_globalvars/lists/mapping.dm:125  GLOBAL_LIST_EMPTY(jobspawn_overrides)
    public DmList jobspawn_overrides;

    // tg: code/_globalvars/lists/mapping.dm:125  (GLOBAL_MANAGED(jobspawn_overrides, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaljobspawn_overrides()
    {
        jobspawn_overrides = Dm.list();
        Dm.append(ref gvars_datum_init_order, "jobspawn_overrides");
    }

    // tg: code/modules/mob/living/basic/pets/sloth.dm:1  GLOBAL_DATUM(cargo_sloth, /mob/living/basic/sloth)
    public mob_living_basic_sloth cargo_sloth;

    public virtual void InitGlobalcargo_sloth()
    {
        Dm.append(ref gvars_datum_init_order, "cargo_sloth");
    }

    // tg: code/_globalvars/lists/mapping.dm:127  GLOBAL_LIST_EMPTY(gorilla_start)
    public DmList gorilla_start;

    // tg: code/_globalvars/lists/mapping.dm:127  (GLOBAL_MANAGED(gorilla_start, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgorilla_start()
    {
        gorilla_start = Dm.list();
        Dm.append(ref gvars_datum_init_order, "gorilla_start");
    }

    // tg: code/_globalvars/lists/mapping.dm:128  GLOBAL_LIST_EMPTY(wizardstart)
    public DmList wizardstart;

    // tg: code/_globalvars/lists/mapping.dm:128  (GLOBAL_MANAGED(wizardstart, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwizardstart()
    {
        wizardstart = Dm.list();
        Dm.append(ref gvars_datum_init_order, "wizardstart");
    }

    // tg: code/_globalvars/lists/mapping.dm:129  GLOBAL_LIST_EMPTY(newplayer_start)
    public DmList newplayer_start;

    // tg: code/_globalvars/lists/mapping.dm:129  (GLOBAL_MANAGED(newplayer_start, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnewplayer_start()
    {
        newplayer_start = Dm.list();
        Dm.append(ref gvars_datum_init_order, "newplayer_start");
    }

    // tg: code/_globalvars/lists/mapping.dm:130  GLOBAL_LIST_EMPTY(prisonwarp) //admin prisoners go to these
    public DmList prisonwarp;

    // tg: code/_globalvars/lists/mapping.dm:130  (GLOBAL_MANAGED(prisonwarp, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalprisonwarp()
    {
        prisonwarp = Dm.list();
        Dm.append(ref gvars_datum_init_order, "prisonwarp");
    }

    // tg: code/_globalvars/lists/mapping.dm:131  GLOBAL_LIST_EMPTY(holdingfacility) //captured people go here (ninja energy net)
    public DmList holdingfacility;

    // tg: code/_globalvars/lists/mapping.dm:131  (GLOBAL_MANAGED(holdingfacility, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalholdingfacility()
    {
        holdingfacility = Dm.list();
        Dm.append(ref gvars_datum_init_order, "holdingfacility");
    }

    // tg: code/_globalvars/lists/mapping.dm:132  GLOBAL_LIST_EMPTY(generic_maintenance_landmarks) //generic spawn areas in maintenance, used for some ghostroles
    public DmList generic_maintenance_landmarks;

    // tg: code/_globalvars/lists/mapping.dm:132  (GLOBAL_MANAGED(generic_maintenance_landmarks, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgeneric_maintenance_landmarks()
    {
        generic_maintenance_landmarks = Dm.list();
        Dm.append(ref gvars_datum_init_order, "generic_maintenance_landmarks");
    }

    // tg: code/_globalvars/lists/mapping.dm:133  GLOBAL_LIST_EMPTY(tdome1)
    public DmList tdome1;

    // tg: code/_globalvars/lists/mapping.dm:133  (GLOBAL_MANAGED(tdome1, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltdome1()
    {
        tdome1 = Dm.list();
        Dm.append(ref gvars_datum_init_order, "tdome1");
    }

    // tg: code/_globalvars/lists/mapping.dm:134  GLOBAL_LIST_EMPTY(tdome2)
    public DmList tdome2;

    // tg: code/_globalvars/lists/mapping.dm:134  (GLOBAL_MANAGED(tdome2, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltdome2()
    {
        tdome2 = Dm.list();
        Dm.append(ref gvars_datum_init_order, "tdome2");
    }

    // tg: code/_globalvars/lists/mapping.dm:135  GLOBAL_LIST_EMPTY(tdomeobserve)
    public DmList tdomeobserve;

    // tg: code/_globalvars/lists/mapping.dm:135  (GLOBAL_MANAGED(tdomeobserve, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltdomeobserve()
    {
        tdomeobserve = Dm.list();
        Dm.append(ref gvars_datum_init_order, "tdomeobserve");
    }

    // tg: code/_globalvars/lists/mapping.dm:136  GLOBAL_LIST_EMPTY(tdomeadmin)
    public DmList tdomeadmin;

    // tg: code/_globalvars/lists/mapping.dm:136  (GLOBAL_MANAGED(tdomeadmin, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltdomeadmin()
    {
        tdomeadmin = Dm.list();
        Dm.append(ref gvars_datum_init_order, "tdomeadmin");
    }

    // tg: code/_globalvars/lists/mapping.dm:137  GLOBAL_LIST_EMPTY(prisonwarped) //list of players already warped
    public DmList prisonwarped;

    // tg: code/_globalvars/lists/mapping.dm:137  (GLOBAL_MANAGED(prisonwarped, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalprisonwarped()
    {
        prisonwarped = Dm.list();
        Dm.append(ref gvars_datum_init_order, "prisonwarped");
    }

    // tg: code/_globalvars/lists/mapping.dm:138  GLOBAL_LIST_EMPTY(blobstart) //stationloving objects, blobs, santa
    public DmList blobstart;

    // tg: code/_globalvars/lists/mapping.dm:138  (GLOBAL_MANAGED(blobstart, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalblobstart()
    {
        blobstart = Dm.list();
        Dm.append(ref gvars_datum_init_order, "blobstart");
    }

    // tg: code/_globalvars/lists/mapping.dm:139  GLOBAL_LIST_EMPTY(navigate_destinations) //list of all destinations used by the navigate verb
    public DmList navigate_destinations;

    // tg: code/_globalvars/lists/mapping.dm:139  (GLOBAL_MANAGED(navigate_destinations, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnavigate_destinations()
    {
        navigate_destinations = Dm.list();
        Dm.append(ref gvars_datum_init_order, "navigate_destinations");
    }

    // tg: code/_globalvars/lists/mapping.dm:140  GLOBAL_LIST_EMPTY(secequipment) //sec equipment lockers that scale with the number of sec players
    public DmList secequipment;

    // tg: code/_globalvars/lists/mapping.dm:140  (GLOBAL_MANAGED(secequipment, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsecequipment()
    {
        secequipment = Dm.list();
        Dm.append(ref gvars_datum_init_order, "secequipment");
    }

    // tg: code/_globalvars/lists/mapping.dm:141  GLOBAL_LIST_EMPTY(deathsquadspawn)
    public DmList deathsquadspawn;

    // tg: code/_globalvars/lists/mapping.dm:141  (GLOBAL_MANAGED(deathsquadspawn, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldeathsquadspawn()
    {
        deathsquadspawn = Dm.list();
        Dm.append(ref gvars_datum_init_order, "deathsquadspawn");
    }

    // tg: code/_globalvars/lists/mapping.dm:142  GLOBAL_LIST_EMPTY(emergencyresponseteamspawn)
    public DmList emergencyresponseteamspawn;

    // tg: code/_globalvars/lists/mapping.dm:142  (GLOBAL_MANAGED(emergencyresponseteamspawn, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalemergencyresponseteamspawn()
    {
        emergencyresponseteamspawn = Dm.list();
        Dm.append(ref gvars_datum_init_order, "emergencyresponseteamspawn");
    }

    // tg: code/_globalvars/lists/mapping.dm:143  GLOBAL_LIST_EMPTY(ruin_landmarks)
    public DmList ruin_landmarks;

    // tg: code/_globalvars/lists/mapping.dm:143  (GLOBAL_MANAGED(ruin_landmarks, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalruin_landmarks()
    {
        ruin_landmarks = Dm.list();
        Dm.append(ref gvars_datum_init_order, "ruin_landmarks");
    }

    // tg: code/_globalvars/lists/mapping.dm:144  GLOBAL_LIST_EMPTY(bar_areas)
    public DmList bar_areas;

    // tg: code/_globalvars/lists/mapping.dm:144  (GLOBAL_MANAGED(bar_areas, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbar_areas()
    {
        bar_areas = Dm.list();
        Dm.append(ref gvars_datum_init_order, "bar_areas");
    }

    // tg: code/_globalvars/lists/mapping.dm:145  GLOBAL_LIST_EMPTY(river_waypoint_list) // Assoc list of river waypoint markers by z-level
    public DmList river_waypoint_list;

    // tg: code/_globalvars/lists/mapping.dm:145  (GLOBAL_MANAGED(river_waypoint_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalriver_waypoint_list()
    {
        river_waypoint_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "river_waypoint_list");
    }

    // tg: code/_globalvars/lists/mapping.dm:146  GLOBAL_LIST_EMPTY(mining_center) // For determining vent size ranked lists, epicenters for comparison goes here.
    public DmList mining_center;

    // tg: code/_globalvars/lists/mapping.dm:146  (GLOBAL_MANAGED(mining_center, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmining_center()
    {
        mining_center = Dm.list();
        Dm.append(ref gvars_datum_init_order, "mining_center");
    }

    // tg: code/_globalvars/lists/mapping.dm:147  GLOBAL_LIST_EMPTY(map_generators_by_z) // Assoc list of shared generators by z by type
    public DmList map_generators_by_z;

    // tg: code/_globalvars/lists/mapping.dm:147  (GLOBAL_MANAGED(map_generators_by_z, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmap_generators_by_z()
    {
        map_generators_by_z = Dm.list();
        Dm.append(ref gvars_datum_init_order, "map_generators_by_z");
    }

    /// List of all the maps that have been cached for /proc/load_map
    // tg: code/_globalvars/lists/mapping.dm:150  GLOBAL_LIST_EMPTY(cached_maps)
    public DmList cached_maps;

    // tg: code/_globalvars/lists/mapping.dm:150  (GLOBAL_MANAGED(cached_maps, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcached_maps()
    {
        cached_maps = Dm.list();
        Dm.append(ref gvars_datum_init_order, "cached_maps");
    }

    /// Just a list of all the area objects in the game
    /// Note, areas can have duplicate types
    // tg: code/_globalvars/lists/mapping.dm:154  GLOBAL_LIST_EMPTY(areas)
    public DmList areas;

    // tg: code/_globalvars/lists/mapping.dm:154  (GLOBAL_MANAGED(areas, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalareas()
    {
        areas = Dm.list();
        Dm.append(ref gvars_datum_init_order, "areas");
    }

    /// Used by jump-to-area etc. Updated by area/updateName()
    /// If this is null, it needs to be recalculated. Use get_sorted_areas() as a getter please
    // tg: code/_globalvars/lists/mapping.dm:157  GLOBAL_LIST_EMPTY(sortedAreas)
    public DmList sortedAreas;

    // tg: code/_globalvars/lists/mapping.dm:157  (GLOBAL_MANAGED(sortedAreas, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsortedAreas()
    {
        sortedAreas = Dm.list();
        Dm.append(ref gvars_datum_init_order, "sortedAreas");
    }

    /// An association from typepath to area instance. Only includes areas with `unique` set.
    // tg: code/_globalvars/lists/mapping.dm:159  GLOBAL_LIST_EMPTY_TYPED(areas_by_type, /area)
    public DmList areas_by_type;

    // tg: code/_globalvars/lists/mapping.dm:159  (GLOBAL_MANAGED(areas_by_type, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalareas_by_type()
    {
        areas_by_type = Dm.list();
        Dm.append(ref gvars_datum_init_order, "areas_by_type");
    }

    /// A list of player-created areas.
    // tg: code/_globalvars/lists/mapping.dm:161  GLOBAL_LIST_EMPTY_TYPED(custom_areas, /area)
    public DmList custom_areas;

    // tg: code/_globalvars/lists/mapping.dm:161  (GLOBAL_MANAGED(custom_areas, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcustom_areas()
    {
        custom_areas = Dm.list();
        Dm.append(ref gvars_datum_init_order, "custom_areas");
    }

    // tg: code/_globalvars/lists/mapping.dm:163  GLOBAL_LIST_EMPTY(all_abstract_markers)
    public DmList all_abstract_markers;

    // tg: code/_globalvars/lists/mapping.dm:163  (GLOBAL_MANAGED(all_abstract_markers, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_abstract_markers()
    {
        all_abstract_markers = Dm.list();
        Dm.append(ref gvars_datum_init_order, "all_abstract_markers");
    }

    /// Global list of megafauna spawns on cave gen
    // tg: code/_globalvars/lists/mapping.dm:166  GLOBAL_LIST_INIT(megafauna_spawn_list, list(...))
    public DmList megafauna_spawn_list;

    // tg: code/_globalvars/lists/mapping.dm:166  (GLOBAL_MANAGED(megafauna_spawn_list, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmegafauna_spawn_list()
    {
        megafauna_spawn_list = Dm.list(
            Dm.kv(typeof(mob_living_simple_animal_hostile_megafauna_bubblegum), 6),
            Dm.kv(typeof(mob_living_simple_animal_hostile_megafauna_colossus), 2),
            Dm.kv(typeof(mob_living_simple_animal_hostile_megafauna_dragon), 4));
        Dm.append(ref gvars_datum_init_order, "megafauna_spawn_list");
    }

    /// List of how many ore vents spawned, and of what size.
    // tg: code/_globalvars/lists/mapping.dm:173  GLOBAL_LIST_INIT(ore_vent_sizes, list(...))
    public DmList ore_vent_sizes;

    // tg: code/_globalvars/lists/mapping.dm:173  (GLOBAL_MANAGED(ore_vent_sizes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalore_vent_sizes()
    {
        ore_vent_sizes = Dm.list(
            Dm.kv(LARGE_VENT_TYPE, 0),
            Dm.kv(MEDIUM_VENT_TYPE, 0),
            Dm.kv(SMALL_VENT_TYPE, 0));
        Dm.append(ref gvars_datum_init_order, "ore_vent_sizes");
    }
}
