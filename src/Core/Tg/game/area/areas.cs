// Translated from tgstation (snapshot 1d99bd7f): code/game/area/areas.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, per the per-proc plan of the wave-4 scope (package B): the procs of later milestones are
// Dm.NotTranslated with the milestone named. The W4.0 contracts wrote the var block (rules 10.2), tg's overrides of
// BYOND's /area defaults (dm_tg_vars, rules 10.2 "Builtin types", including luminosity from
// modules/lighting/lighting_area.dm:2) and the object-creating defaults (dm_init_vars). The [DmPath] of /area is on its
// engine part, Builtin/area.builtin.cs; this file holds the [DmPath] of /area/space, tg's /world/area (code/world.dm),
// which areas.dm declares first in .dme order (/area/space/update_icon_state, :481; rules 2.3).

/**
 * # area
 *
 * A grouping of tiles into a logical space, mostly used by map editors
 */
// tg: code/game/area/areas.dm:6
public partial class area : atom
{
    // tg: code/game/area/areas.dm:7-15, :108, modules/lighting/lighting_area.dm:2  tg's overrides of BYOND's /area defaults
    // (rules 10.2, "Builtin types"), in .dme order
    private partial void dm_tg_vars()
    {
        name = "Space";
        icon = Dm.resource("icons/area/areas_misc.dmi");
        icon_state = "unknown";
        layer = AREA_LAYER;
        //Keeping this on the default plane, GAME_PLANE, will make area overlays fail to render on FLOOR_PLANE.
        plane = AREA_PLANE;
        mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
        invisibility = INVISIBILITY_LIGHTING;
        tacmap_color = null;
        flags_1 = CAN_BE_DIRTY_1;
        luminosity = 1;
    }

    // tg: code/game/area/areas.dm:21-136  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        turfs_by_zlevel = Dm.list();
        turfs_to_uncontain_by_zlevel = Dm.list();
        firealarms = Dm.list();
        active_alarms = Dm.list();
        lights = Dm.list();
        air_vents = Dm.list();
        air_scrubbers = Dm.list();
    }

    /// List of all turfs currently inside this area as nested lists indexed by zlevel.
    /// Acts as a filtered version of area.contents For faster lookup
    /// (area.contents is actually a filtered loop over world)
    /// Semi fragile, but it prevents stupid so I think it's worth it
    // tg: code/game/area/areas.dm:21
    public DmList turfs_by_zlevel;
    /// turfs_by_z_level can hold MASSIVE lists, so rather then adding/removing from it each time we have a problem turf
    /// We should instead store a list of turfs to REMOVE from it, then hook into a getter for it
    /// There is a risk of this and contained_turfs leaking, so a subsystem will run it down to 0 incrementally if it gets too large
    /// This uses the same nested list format as turfs_by_zlevel
    // tg: code/game/area/areas.dm:26
    public DmList turfs_to_uncontain_by_zlevel;

    /// General flag for area properties
    // tg: code/game/area/areas.dm:29
    public float area_flags = Dm.bitor(Dm.bitor(VALID_TERRITORY, BLOBS_ALLOWED), CULT_PERMITTED);
    /// Flag for mapping related area properties (such as cavegen)
    // tg: code/game/area/areas.dm:31
    public float area_flags_mapping = UNIQUE_AREA;

    ///Do we have an active fire alarm?
    // tg: code/game/area/areas.dm:34
    public float fire = FALSE;
    ///A var for whether the area allows for detecting fires/etc. Disabled or enabled at a fire alarm, checked by fire locks.
    // tg: code/game/area/areas.dm:36
    public float fire_detect = TRUE;
    ///A list of all fire locks in this area. Used by fire alarm panels when resetting fire locks or activating all in an area
    // tg: code/game/area/areas.dm:38
    public DmList firedoors;
    ///A list of firelocks currently active. Used by fire alarms when setting their icons.
    // tg: code/game/area/areas.dm:40
    public DmList active_firelocks;
    ///A list of all fire alarms in this area. Used by firelocks and burglar alarms to change icon state.
    // tg: code/game/area/areas.dm:42
    public DmList firealarms;
    ///Alarm type to count of sources. Not usable for ^ because we handle fires differently
    // tg: code/game/area/areas.dm:44
    public DmList active_alarms;
    /// The current alarm fault status
    // tg: code/game/area/areas.dm:46
    public float fault_status = AREA_FAULT_NONE;
    /// The source machinery for the area's fault status
    // tg: code/game/area/areas.dm:48
    public object fault_location;
    ///List of all lights in our area
    // tg: code/game/area/areas.dm:50
    public DmList lights;
    ///We use this just for fire alarms, because they're area based right now so one alarm going poof shouldn't prevent you from clearing your alarms listing. Fire alarms and fire locks will set and clear alarms.
    // tg: code/game/area/areas.dm:52
    public datum_alarm_handler alarm_manager;

    // tg: code/game/area/areas.dm:54
    public float lightswitch = TRUE;

    /// All beauty in this area combined, only includes indoor area.
    // tg: code/game/area/areas.dm:57
    public float totalbeauty = 0;
    /// Beauty average per open turf in the area
    // tg: code/game/area/areas.dm:59
    public float beauty = 0;
    /// If a room is too big it doesn't have beauty.
    // tg: code/game/area/areas.dm:61
    public float beauty_threshold = 150;

    /// For space, the asteroid, lavaland, etc. Used with blueprints or with weather to determine if we are adding a new area (vs editing a station room)
    // tg: code/game/area/areas.dm:64
    public float outdoors = FALSE;
    /// Whether or not this area unifies all of its motion sensors.
    // tg: code/game/area/areas.dm:66
    public float motion_monitored = FALSE;

    /// Size of the area in open turfs, only calculated for indoors areas.
    // tg: code/game/area/areas.dm:69
    public float areasize = 0;

    /// Bonus mood for being in this area
    // tg: code/game/area/areas.dm:72
    public float mood_bonus = 0;
    /// Mood message for being here, only shows up if mood_bonus != 0
    // tg: code/game/area/areas.dm:74
    public string mood_message = "This area is pretty nice!";
    /// Does the mood bonus require a trait?
    // tg: code/game/area/areas.dm:76
    public object mood_trait;

    ///Will objects this area be needing power?
    // tg: code/game/area/areas.dm:79
    public float requires_power = TRUE;
    /// This gets overridden to 1 for space in area/.
    // tg: code/game/area/areas.dm:81
    public float always_unpowered = FALSE;

    // tg: code/game/area/areas.dm:83
    public obj_machinery_power_apc apc;

    // tg: code/game/area/areas.dm:85
    public float power_equip = TRUE;
    // tg: code/game/area/areas.dm:86
    public float power_light = TRUE;
    // tg: code/game/area/areas.dm:87
    public float power_environ = TRUE;
    // tg: code/game/area/areas.dm:88
    public float power_apc_charge = TRUE;
    /// The default gravity for the area
    // tg: code/game/area/areas.dm:90
    public float default_gravity = ZERO_GRAVITY;

    // tg: code/game/area/areas.dm:92
    public float parallax_movedir = NONE;

    // tg: code/game/area/areas.dm:94
    public string ambience_index = AMBIENCE_GENERIC;
    ///A list of sounds to pick from every so often to play to clients.
    // tg: code/game/area/areas.dm:96
    public DmList ambientsounds;
    ///Does this area immediately play an ambience track upon enter?
    // tg: code/game/area/areas.dm:98
    public float forced_ambience = FALSE;
    ///The background droning loop that plays 24/7
    // tg: code/game/area/areas.dm:100
    public object ambient_buzz = Dm.resource("sound/ambience/general/shipambience.ogg");
    ///The volume of the ambient buzz
    // tg: code/game/area/areas.dm:102
    public float ambient_buzz_vol = 35;
    ///Used to decide what the minimum time between ambience is
    // tg: code/game/area/areas.dm:104
    public float min_ambience_cooldown = 4 * SECONDS;
    ///Used to decide what the maximum time between ambience is
    // tg: code/game/area/areas.dm:106
    public float max_ambience_cooldown = 10 * SECONDS;


    // tg: code/game/area/areas.dm:110
    public DmList cameras;

    /// Typepath to limit the areas (subtypes included) that atoms in this area can smooth with. Used for shuttles.
    // tg: code/game/area/areas.dm:113
    public Type area_limited_icon_smoothing;

    /// The energy usage of the area in the last machines SS tick.
    // tg: code/game/area/areas.dm:116
    public DmList energy_usage;

    /// Wire assignment for airlocks in this area
    // tg: code/game/area/areas.dm:119
    public Type airlock_wires = typeof(datum_wires_airlock);

    /// Should we actually be running our mapgen if one is set?
    /// FALSE here with a set map_generator allows the area to probe from shared generators without actually generating its' turfs
    // tg: code/game/area/areas.dm:123
    public float use_mapgen = TRUE;
    /// Datum that would be used in terrain generation behavior
    // tg: code/game/area/areas.dm:125  the generator's type, then its instance once get_generator made it
    public object map_generator;
    /// Should the map_generator be shared with all other willing areas on our z_level?
    // tg: code/game/area/areas.dm:127
    public float share_map_generator = TRUE;

    ///Used to decide what kind of reverb the area makes sound have
    // tg: code/game/area/areas.dm:130
    public float sound_environment = SOUND_ENVIRONMENT_NONE;

    /// List of all air vents in the area
    // tg: code/game/area/areas.dm:133
    public DmList air_vents;

    /// List of all air scrubbers in the area
    // tg: code/game/area/areas.dm:136
    public DmList air_scrubbers;

    /// Are shuttles allowed to dock in this area
    // tg: code/game/area/areas.dm:139
    public float allow_shuttle_docking = FALSE;

    /// If TRUE, then this area will be skipped entirely by minimap rendering.
    // tg: code/game/area/areas.dm:142
    public float skip_minimap_rendering = FALSE;
}

public partial class datum_controller_global_vars
{
    /**
     * A list of teleport locations
     *
     * Adding a wizard area teleport list because motherfucking lag -- Urist
     * I am far too lazy to make it a proper list of areas so I'll just make it run the usual teleport routine at the start of the game
     */
    // tg: code/game/area/areas.dm:150  GLOBAL_LIST_EMPTY(teleportlocs)
    public DmList teleportlocs;

    // tg: code/game/area/areas.dm:150  (GLOBAL_MANAGED(teleportlocs, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalteleportlocs()
    {
        teleportlocs = Dm.list();
        Dm.append(ref gvars_datum_init_order, "teleportlocs");
    }
}

public static partial class GlobalProcs
{
    /**
     * Generate a list of turfs you can teleport to from the areas list
     *
     * Includes areas if they're not a shuttle or not not teleport or have no contents
     *
     * The chosen turf is the first item in the areas contents that is a station level
     *
     * The returned list of turfs is sorted by name
     */
    // tg: code/game/area/areas.dm:161
    public static void process_teleport_locs()
    {
        foreach (area AR in Dm.each<area>(get_sorted_areas()))
        {
            if (AR is area_shuttle || Dm.truthy(Dm.bitand(AR.area_flags, NOTELEPORT)))
                continue;
            if (Dm.truthy(GLOB.teleportlocs[AR.name]))
                continue;
            if (!Dm.truthy(AR.has_contained_turfs()))
                continue;
            // is_station_level(z) (__HELPERS/_level_traits.dm:5), expanded inline (rules 11.1 (e))
            if (Dm.truthy(SSmapping.level_trait(AR.z, ZTRAIT_STATION)))
                GLOB.teleportlocs[AR.name] = AR;
        }
    }
}

// tg: code/game/area/areas.dm:177  (interleaved types, rules 1.9)
public partial class area : atom
{
    /**
     * Called when an area loads
     *
     *  Adds the item to the GLOB.areas_by_type list based on area type
     */
    // tg: code/game/area/areas.dm:177
    public override async DmTask New(params object[] __extra)
    {
        // This interacts with the map loader, so it needs to be set immediately
        // rather than waiting for atoms to initialize.
        if (Dm.truthy(Dm.bitand(area_flags_mapping, UNIQUE_AREA)))
            GLOB.areas_by_type[type] = this;
        Dm.append(ref GLOB.areas, this);
        energy_usage = Dm.list_n(AREA_USAGE_LEN); // Some atoms would like to use power in Initialize()
        alarm_manager = Dm.@new<datum_alarm_handler>(this); // just in case
        await base.New(__extra);
    }

    /*
     * Initialize this area
     *
     * initializes the dynamic area lighting and also registers the area with the z level via
     * reg_in_areas_in_z
     *
     * returns INITIALIZE_HINT_LATELOAD
     */
    // tg: code/game/area/areas.dm:195
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        icon_state = "";
        if (!Dm.truthy(ambientsounds))
            ambientsounds = GLOB.ambience_assoc[ambience_index] as DmList;

        if (Dm.truthy(requires_power))
            luminosity = 0;
        else
        {
            power_light = TRUE;
            power_equip = TRUE;
            power_environ = TRUE;

            if (Dm.truthy(static_lighting))
                luminosity = 0;
        }

        dot = base.Initialize(mapload, __extra);

        if (!Dm.truthy(static_lighting))
            blend_mode = BLEND_MULTIPLY;

        reg_in_areas_in_z();

        update_base_lighting();

        return INITIALIZE_HINT_LATELOAD;
    }

    /**
     * Sets machine power levels in the area
     */
    // tg: code/game/area/areas.dm:224  (set waitfor = FALSE, inherited from /atom/LateInitialize: with AddComponent not
    // translated this body cannot sleep, so it needs no wrapper, waiting-model.md 5.4)
    public override void LateInitialize(params object[] __extra)
    {
        power_change(); // all machines set to current power level, also updates icon
        update_beauty();
        if (Dm.truthy(motion_monitored))
            Dm.Deferred("code/game/area/areas.dm:228", "LateInitialize: AddComponent(/datum/component/monitored_area): the motion-monitored areas' camera alarms come with the cameras' motion detection (Lavaland's labour camp has one, D304)");
    }

    /// Generate turfs, including cool cave wall gen
    // tg: code/game/area/areas.dm:231
    public virtual async DmTask RunTerrainGeneration()
    {
        if (!Dm.truthy(use_mapgen) || map_generator == null)
            return;
        map_generator = get_generator();
        DmList turfs = Dm.list();
        foreach (turf T in Dm.each<turf>(contents))
            Dm.append(ref turfs, T);
        await ((datum_map_generator)map_generator).generate_terrain(turfs, this);
    }

    /// Populate the previously generated terrain with mobs and objects
    // tg: code/game/area/areas.dm:241
    public virtual async DmTask RunTerrainPopulation()
    {
        if (!Dm.truthy(use_mapgen) || map_generator == null)
            return;
        map_generator = get_generator();
        DmList turfs = Dm.list();
        foreach (turf T in Dm.each<turf>(contents))
            Dm.append(ref turfs, T);
        await ((datum_map_generator)map_generator).populate_terrain(turfs, this);
    }

    // tg: code/game/area/areas.dm:250
    public virtual async DmTask test_gen()
    {
        if (!Dm.truthy(use_mapgen) || map_generator == null)
            return;
        map_generator = get_generator();
        DmList turfs = Dm.list();
        foreach (turf T in Dm.each<turf>(contents))
            Dm.append(ref turfs, T);
        await ((datum_map_generator)map_generator).generate_terrain(turfs, this);
    }

    /// Gets own, or shared, map generator
    // tg: code/game/area/areas.dm:260
    public virtual datum_map_generator get_generator()
    {
        // RETURN_TYPE(/datum/map_generator)
        if (map_generator == null)
            return null;

        if (map_generator is datum_map_generator own)
            return own;

        if (!Dm.truthy(share_map_generator))
        {
            map_generator = Dm.@new((Type)map_generator);
            return (datum_map_generator)map_generator;
        }

        string z_key = Dm.text("[]", z);
        DmList z_generators = GLOB.map_generators_by_z[z_key] as DmList;
        if (z_generators != null && Dm.truthy(z_generators[map_generator]))
            map_generator = z_generators[map_generator];

        if (map_generator is not datum_map_generator)
        {
            object new_generator = Dm.@new((Type)map_generator);
            DmList by_z = GLOB.map_generators_by_z[z_key] as DmList;
            LAZYSET(ref by_z, map_generator, new_generator);
            GLOB.map_generators_by_z[z_key] = by_z;
            map_generator = new_generator;
        }

        return (datum_map_generator)map_generator;
    }

    /// Returns the highest zlevel that this area contains turfs for
    // tg: code/game/area/areas.dm:284
    public virtual object get_highest_zlevel()
    {
        for (float area_zlevel = length(turfs_by_zlevel), area_zlevel__end = 1; area_zlevel >= area_zlevel__end; area_zlevel += -1)
        {
            if (length(turfs_to_uncontain_by_zlevel) >= area_zlevel)
            {
                if (length(turfs_by_zlevel[area_zlevel]) - length(turfs_to_uncontain_by_zlevel[area_zlevel]) > 0)
                    return area_zlevel;
            }
            else
            {
                if (Dm.truthy(length(turfs_by_zlevel[area_zlevel])))
                    return area_zlevel;
            }
        }
        return 0;
    }

    /// Returns a nested list of lists with all turfs split by zlevel.
    /// only zlevels with turfs are returned. The order of the list is not guaranteed.
    // tg: code/game/area/areas.dm:296
    public virtual object get_zlevel_turf_lists()
    {
        if (Dm.truthy(length(turfs_to_uncontain_by_zlevel)))
            cannonize_contained_turfs();

        DmList zlevel_turf_lists = Dm.list();

        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(turfs_by_zlevel))
        {
            if (Dm.truthy(length(zlevel_turfs)))
                Dm.append(ref zlevel_turf_lists, Dm.list(zlevel_turfs));
        }

        return zlevel_turf_lists;
    }

    /// Returns a list with all turfs in this zlevel.
    // tg: code/game/area/areas.dm:309
    public virtual object get_turfs_by_zlevel(object zlevel = null)
    {
        if (Dm.ge(length(turfs_to_uncontain_by_zlevel), zlevel) && Dm.truthy(length(turfs_to_uncontain_by_zlevel[zlevel])))
            cannonize_contained_turfs_by_zlevel(zlevel);

        if (Dm.lt(length(turfs_by_zlevel), zlevel))
            return Dm.list();

        return turfs_by_zlevel[zlevel];
    }


    /// Merges a list containing all of the turfs zlevel lists from get_zlevel_turf_lists inside one list. Use get_zlevel_turf_lists() or get_turfs_by_zlevel() unless you need all the turfs in one list to avoid generating large lists
    // tg: code/game/area/areas.dm:320
    public virtual object get_turfs_from_all_zlevels()
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(get_zlevel_turf_lists()))
            Dm.append(ref dot, zlevel_turfs);
        return dot;
    }

    /// Ensures that the contained_turfs list properly represents the turfs actually inside us
    // tg: code/game/area/areas.dm:326
    public virtual void cannonize_contained_turfs_by_zlevel(object zlevel_to_clean = null, float? _autoclean = null)
    {
        _autoclean ??= TRUE;
        // This is massively suboptimal for LARGE removal lists
        // Try and keep the mass removal as low as you can. We'll do this by ensuring
        // We only actually add to contained turfs after large changes (Also the management subsystem)
        // Do your damndest to keep turfs out of /area/space as a stepping stone
        // That sucker gets HUGE and will make this take actual seconds
        if (Dm.le(zlevel_to_clean, length(turfs_by_zlevel)) && Dm.le(zlevel_to_clean, length(turfs_to_uncontain_by_zlevel)))
            Dm.remove_at(turfs_by_zlevel, zlevel_to_clean, turfs_to_uncontain_by_zlevel[zlevel_to_clean]);

        if (!Dm.truthy(_autoclean)) // Removes empty lists from the end of this list
        {
            turfs_to_uncontain_by_zlevel[zlevel_to_clean] = Dm.list();
            return;
        }

        float new_length = length(turfs_to_uncontain_by_zlevel);
        // Walk backwards thru the list
        for (float i = length(turfs_to_uncontain_by_zlevel), i__end = 0; i >= i__end; i += -1)
        {
            if (Dm.truthy(i) && Dm.truthy(length(turfs_to_uncontain_by_zlevel[i])))
                break; // Stop the moment we find a useful list
            new_length = i;
        }

        if (new_length < length(turfs_to_uncontain_by_zlevel))
            turfs_to_uncontain_by_zlevel.len = new_length;

        if (Dm.ge(new_length, zlevel_to_clean))
            turfs_to_uncontain_by_zlevel[zlevel_to_clean] = Dm.list();
    }


    /// Ensures that the contained_turfs list properly represents the turfs actually inside us
    // tg: code/game/area/areas.dm:354
    public virtual void cannonize_contained_turfs()
    {
        for (float area_zlevel = 1, area_zlevel__end = length(turfs_to_uncontain_by_zlevel); area_zlevel <= area_zlevel__end; area_zlevel++)
            cannonize_contained_turfs_by_zlevel(area_zlevel, _autoclean: FALSE);

        turfs_to_uncontain_by_zlevel = Dm.list();
    }


    /// Returns TRUE if we have contained turfs, FALSE otherwise
    // tg: code/game/area/areas.dm:362
    public virtual object has_contained_turfs()
    {
        for (float area_zlevel = 1, area_zlevel__end = length(turfs_by_zlevel); area_zlevel <= area_zlevel__end; area_zlevel++)
        {
            if (length(turfs_to_uncontain_by_zlevel) >= area_zlevel)
            {
                if (length(turfs_by_zlevel[area_zlevel]) - length(turfs_to_uncontain_by_zlevel[area_zlevel]) > 0)
                    return TRUE;
            }
            else
            {
                if (Dm.truthy(length(turfs_by_zlevel[area_zlevel])))
                    return TRUE;
            }
        }
        return FALSE;
    }

    /**
     * Register this area as belonging to a z level
     *
     * Ensures the item is added to the SSmapping.areas_in_z list for this z
     */
    // tg: code/game/area/areas.dm:377
    public virtual void reg_in_areas_in_z()
    {
        if (!Dm.truthy(has_contained_turfs()))
            return;
        DmList areas_in_z = SSmapping.areas_in_z;
        update_areasize();
        if (!Dm.truthy(z))
        {
            // WARNING(MSG) (__HELPERS/logging/_logging.dm:2), expanded inline (rules 11.1 (d), (f): src, usr, __FILE__, __LINE__)
            warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("No z found for []", this), "code/game/area/areas.dm", 383, this, usr));
            return;
        }
        if (!Dm.truthy(areas_in_z[Dm.text("[]", z)]))
            areas_in_z[Dm.text("[]", z)] = Dm.list();
        Dm.append_at(areas_in_z, Dm.text("[]", z), this);
    }

    /**
     * Destroy an area and clean it up
     *
     * Removes the area from GLOB.areas_by_type and also stops it processing on SSobj
     *
     * This is despite the fact that no code appears to put it on SSobj, but
     * who am I to argue with old coders
     */
    // tg: code/game/area/areas.dm:397
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        if (Dm.eq(GLOB.areas_by_type[type], this))
            GLOB.areas_by_type[type] = null;
        //this is not initialized until get_sorted_areas() is called so we have to do a null check
        if (GLOB.sortedAreas != null)
            Dm.remove(ref GLOB.sortedAreas, this);
        //just for sanity sake cause why not
        if (GLOB.areas != null)
            Dm.remove(ref GLOB.areas, this);
        if (GLOB.custom_areas != null)
            Dm.remove(ref GLOB.custom_areas, this);
        //machinery cleanup
        STOP_PROCESSING(SSobj, this);
        QDEL_NULL(ref alarm_manager);
        firedoors = null;
        //atmos cleanup
        firealarms = null;
        air_vents = null;
        air_scrubbers = null;
        //turf cleanup
        turfs_by_zlevel = null;
        turfs_to_uncontain_by_zlevel = null;
        //parent cleanup
        return base.Destroy(force);
    }

    /**
     * Close and lock a door passed into this proc
     *
     * Does this need to exist on area? probably not
     */
    // tg: code/game/area/areas.dm:427  (set waitfor = FALSE: the wrapper of waiting-model.md 5.4; the door's close may sleep).
    // Gaps ST2: translated for the display cases' burglar alarm.
    public virtual void close_and_lock_door(obj_machinery_door DOOR = null)
    {
        Waiting.WaitforFalse(close_and_lock_door__body(DOOR));
    }

    private async DmTask close_and_lock_door__body(obj_machinery_door DOOR)
    {
        await DOOR.close();
        if (Dm.truthy(DOOR.density))
            DOOR.@lock();
    }

    /**
     * Raise a burglar alert for this area
     *
     * Close and locks all doors in the area and alerts silicon mobs of a break in
     *
     * Alarm auto resets after 600 ticks
     */
    // tg: code/game/area/areas.dm:440
    // Gaps ST2: translated for the display cases' burglar alarm (displaycase.dm:105).
    public virtual void burglaralert(obj trigger = null)
    {
        //Trigger alarm effect
        set_fire_effect(TRUE);
        //Lockdown airlocks
        // for(var/obj/machinery/door/door in src): an area's contents are its turfs here (Builtin/area.builtin.cs), so the doors are
        // looked for on the area's turfs, as BYOND lists them (as turret_control.dm:47)
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each_cast<turf>(zlevel_turfs))
            {
                foreach (obj_machinery_door door in Dm.each<obj_machinery_door>(area_turf.contents))
                    close_and_lock_door(door);
            }
        }
    }


    /**
     * Set the fire alarm visual affects in an area
     *
     * Allows interested parties (lights and fire alarms) to react
     */
    // tg: code/game/area/areas.dm:453
    public virtual void set_fire_effect(object new_fire = null, object fault_type = null, object fault_source = null)
    {
        if (Dm.eq(new_fire, fire))
            return;
        fire = Dm.num(new_fire);
        fault_status = Dm.num(fault_type);
        if (Dm.truthy(fire))
            fault_location = fault_source;
        else
            fault_location = null;
        if (Dm.truthy(this._listen_lookup?[COMSIG_AREA_FIRE_CHANGED]))
            this._SendSignal(COMSIG_AREA_FIRE_CHANGED, Dm.list(this, fire));
    }

    /// Ensures areas render their weather properly
    // tg: code/game/area/areas.dm:465
    [DmParams()]
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon(updates, __extra);
        foreach (datum_weather weather in Dm.each_cast<datum_weather>(SSweather?.processing))
        {
            if (weather.stage != END_STAGE && Dm.@in(this, weather.impacted_areas))
                weather.update_areas();
        }
        return dot;
    }

    /// Sets the area's parallax movedir (the direction parallax will animate in while in the area)
    // tg: code/game/area/areas.dm:472
    public virtual void set_parallax_movedir(object new_dir = null)
    {
        if (Dm.eq(parallax_movedir, new_dir))
            return;
        parallax_movedir = Dm.num(new_dir);
        if (Dm.truthy(this._listen_lookup?[COMSIG_AREA_PARALLAX_DIR_CHANGED]))
            this._SendSignal(COMSIG_AREA_PARALLAX_DIR_CHANGED, Dm.list(this, parallax_movedir));
    }
}

/**
 * Update the icon of the area (overridden to always be null for space
 */
// tg: code/game/area/areas.dm:481
[DmPath("/area/space")]
public partial class area_space : area
{
    // tg: code/game/area/areas.dm:481  (SHOULD_CALL_PARENT(FALSE) is dropped, rules 7.9)
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = null;
        return null;
    }
}

// tg: code/game/area/areas.dm:581  (interleaved types, rules 1.9; the power procs of :492-567 are in areas.power.cs)
public partial class area : atom
{
    /**
     * Call back when an atom enters an area
     *
     * Sends signals COMSIG_AREA_ENTERED and COMSIG_ENTER_AREA (to a list of atoms)
     *
     * If the area has ambience, then it plays some ambience music to the ambience channel
     */
    // tg: code/game/area/areas.dm:581  (set waitfor = FALSE: this body cannot sleep, so it needs no wrapper,
    // waiting-model.md 5.4; it is async only because the Entered slot suspends through other overrides)
    [DmParams("arrived", "old_area")]
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_area = null, DmList old_locs = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_AREA_ENTERED]))
            this._SendSignal(COMSIG_AREA_ENTERED, Dm.list(this, arrived, old_area));

        if (!Dm.truthy(arrived.important_recursive_contents?[RECURSIVE_CONTENTS_AREA_SENSITIVE]))
            return null;
        foreach (atom_movable recipient in Dm.each_cast<atom_movable>(arrived.important_recursive_contents[RECURSIVE_CONTENTS_AREA_SENSITIVE]))
        {
            if (Dm.truthy(recipient._listen_lookup?[COMSIG_ENTER_AREA]))
                recipient._SendSignal(COMSIG_ENTER_AREA, Dm.list(recipient, this));
        }

        if (ismob(arrived))
        {
            mob mob = arrived as mob;
            mob.update_ambience_area(this);
        }
        return null;
    }

    /**
     * Called when an atom exits an area
     *
     * Sends signals COMSIG_AREA_EXITED and COMSIG_EXIT_AREA (to a list of atoms)
     */
    // tg: code/game/area/areas.dm:599
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_AREA_EXITED]))
            this._SendSignal(COMSIG_AREA_EXITED, Dm.list(this, gone, direction));
        if (Dm.truthy(gone._listen_lookup?[COMSIG_MOVABLE_EXITED_AREA]))
            gone._SendSignal(COMSIG_MOVABLE_EXITED_AREA, Dm.list(gone, this, direction));

        if (!Dm.truthy(gone.important_recursive_contents?[RECURSIVE_CONTENTS_AREA_SENSITIVE]))
            return null;
        foreach (atom_movable recipient in Dm.each_cast<atom_movable>(gone.important_recursive_contents[RECURSIVE_CONTENTS_AREA_SENSITIVE]))
        {
            if (Dm.truthy(recipient._listen_lookup?[COMSIG_EXIT_AREA]))
                recipient._SendSignal(COMSIG_EXIT_AREA, Dm.list(recipient, this));
        }
        return null;
    }

    ///Divides total beauty in the room by roomsize to allow us to get an average beauty per tile.
    // tg: code/game/area/areas.dm:609
    public virtual object update_beauty()
    {
        if (!Dm.truthy(areasize))
        {
            beauty = 0;
            return FALSE;
        }
        if (areasize >= beauty_threshold)
        {
            beauty = 0;
            return FALSE; //Too big
        }
        beauty = Dm.div(totalbeauty, areasize);
        if (Dm.truthy(this._listen_lookup?[COMSIG_AREA_BEAUTY_UPDATED]))
            this._SendSignal(COMSIG_AREA_BEAUTY_UPDATED, Dm.list(this));
        return null;
    }

    /**
     * Setup an area (with the given name)
     *
     * Sets the area name, sets all status var's to false and adds the area to the sorted area list
     */
    // tg: code/game/area/areas.dm:624
    public virtual void setup(object a_name = null)
    {
        name = (string)a_name;
        power_equip = FALSE;
        power_light = FALSE;
        power_environ = FALSE;
        always_unpowered = FALSE;
        Dm.mask(ref area_flags, Dm.bitnot(Dm.bitor(Dm.bitor(VALID_TERRITORY, BLOBS_ALLOWED), CULT_PERMITTED)));
        require_area_resort();
    }
    /**
     * Set the area size of the area
     *
     * This is the number of open turfs in the area contents, or FALSE if the outdoors var is set
     *
     */
    // tg: code/game/area/areas.dm:638
    public virtual object update_areasize()
    {
        if (Dm.truthy(outdoors))
            return FALSE;
        areasize = 0;
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(get_zlevel_turf_lists()))
        {
            foreach (turf_open thisvarisunused in Dm.each<turf_open>(zlevel_turfs))
                areasize++;
        }
        return null;
    }

    /**
     * Causes a runtime error
     */
    // tg: code/game/area/areas.dm:649
    public override object AllowDrop()
    {
        throw Dm.CRASH("Bad op: area/AllowDrop() called", "code/game/area/areas.dm", 650);
    }

    /**
     * Causes a runtime error
     */
    // tg: code/game/area/areas.dm:655
    public override object drop_location()
    {
        throw Dm.CRASH("Bad op: area/drop_location() called", "code/game/area/areas.dm", 656);
    }

    /// A hook so areas can modify the incoming args of ChangeTurf
    // tg: code/game/area/areas.dm:659
    // Appendix A (M3 P3, decided): added_layer is Type, the undecided parameter of slots.tsv: place_on_top passes its
    // typepath (baseturfs.dm:38), and /area/shuttle's override tests it with ispath (areas/shuttles.dm:23).
    public virtual object place_on_top_react(DmList new_baseturfs = null, Type added_layer = null, object flags = null)
    {
        return flags;
    }


    /// Called when a living mob that spawned here, joining the round, receives the player client.
    // tg: code/game/area/areas.dm:664
    public virtual void on_joining_game(mob_living boarder = null)
    {
        return;
    }

    /**
     * Returns the name of an area, with the original name if the area name has been changed.
     *
     * If an area has not been renamed, returns the area name. If it has been modified (by blueprints or other means)
     * returns the current name, as well as the initial value, in the format of [Current Location Name (Original Name)]
     */

    // tg: code/game/area/areas.dm:674
    public virtual object get_original_area_name()
    {
        if (name == Dm.initial(this, (atom p) => p.name))
            return name;
        return Dm.text("[] ([])", name, Dm.initial(this, (atom p) => p.name));
    }
}

/**
 * A blank area subtype solely used by the golem area editor for the purpose of
 * allowing golems to create new areas without suffering from the hazard_area debuffs.
 */
// tg: code/game/area/areas.dm:683
[DmPath("/area/golem")]
public partial class area_golem : area
{
    // tg: code/game/area/areas.dm:684
    public area_golem()
    {
        name = "Golem Territory";
    }
}
