// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/mapping.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M3 P2): the station path (D108: Meta's station level only): PreInit, Initialize with loadWorld, LoadGroup, the
// z-level management (manage_z_level, build_area_turfs, the linkages and gravities, zlevel_manager.cs, traits.cs) and
// generate_station_area_list. Initialize leaves out what D108 does not load (the space ruin and empty levels, the
// wilderness, away missions and the space transitions; Lavaland's ruins are seeded) and the BYOND
// plane cube (create_plane_offsets: client presentation; the plane macros keep their base case, max_plane_offset 0);
// M10 slice G1 (D272) adds the first transit level, the turf reservations and fire.
// A world without a selected map (MapSeam.Select: the host-built test and toy rooms, M2's server) keeps the M2 stub's
// SS_NO_INIT: its rooms are built by the host and SSmapping manages no level.

// tg: code/controllers/subsystem/mapping.dm:1 SUBSYSTEM_DEF(mapping)
[DmPath("/datum/controller/subsystem/mapping")]
public partial class datum_controller_subsystem_mapping : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/mapping.dm:2
    public datum_controller_subsystem_mapping()
    {
        name = "Mapping";
        // tg: no flags, runlevels = ALL. SS_NO_INIT and SS_NO_FIRE in a world without a selected map (see the file header): it
        // manages no level and no reservation.
        World world = World.CurrentOrNull;
        ss_flags = world != null && MapSeam.Selected(world) != null ? 0 : Dm.bitor(SS_NO_INIT, SS_NO_FIRE);
        runlevels = ALL;
    }

    // tg: code/controllers/subsystem/mapping.dm:3, :35, :38, :43, :80, :89 (object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        dependencies = Dm.list(
            typeof(datum_controller_subsystem_job),
            typeof(datum_controller_subsystem_processing_station),
            typeof(datum_controller_subsystem_processing_reagents));
        nuke_tiles = Dm.list(); // tg: code/controllers/subsystem/mapping.dm:10
        nuke_threats = Dm.list(); // :11
        map_templates = Dm.list(); // :16
        ruins_templates = Dm.list(); // :18
        active_ruins = Dm.alist(); // :21
        reserved_ruins = Dm.list(); // :24
        themed_ruins = Dm.list(); // :27
        shuttle_templates = Dm.list(); // :31
        shelter_templates = Dm.list(); // :32
        holodeck_templates = Dm.list(); // :33
        areas_in_z = Dm.list();
        z_level_to_plane_offset = Dm.list();
        z_level_to_stack = Dm.list(); // :41
        z_level_to_lowest_plane_offset = Dm.list();
        unused_turfs = Dm.list(); // :61
        used_turfs = Dm.list(); // :63
        lists_to_reserve = Dm.list(); // :65
        reservation_ready = Dm.list(); // :67
        biomes = Dm.list(); // :71
        multiz_levels = Dm.list();
        gravity_by_z_level = Dm.list();
        z_trait_levels = Dm.list(); // :92
    }

    // tg: code/controllers/subsystem/mapping.dm:97
    public override void PreInit()
    {
        base.PreInit();
        // tg: #ifdef FORCE_MAP  current_map = load_map_config(FORCE_MAP, FORCE_MAP_DIRECTORY)  #else ...  #endif
        // PORT: the host's map selection (MapSeam.Select) is tg's FORCE_MAP: the json of that name in MAP_DIRECTORY_MAPS
        // (mounted from the converted maps folder). A world without a selection has no station level (the host-built test
        // and toy rooms; SSmapping keeps SS_NO_INIT, see the file header), so it reads no map config: current_map stays
        // null, as in M2.
        MapSelection selection = MapSeam.Selected(World.Current);
        if (selection != null)
            current_map = (datum_map_config)load_map_config(selection.MapName, MAP_DIRECTORY_MAPS);
    }

    // DV509: a reserved level's least width and height: tg's border on both sides and 64 tiles between them, which hold the stand-in of CentCom's
    // emergency dock (50 and a tile around, DV190) and the transit of a shuttle 32 tiles long
    private const int ReservedLevelLeastSize = 2 * SHUTTLE_TRANSIT_BORDER + 64;

    // tg: code/controllers/subsystem/mapping.dm:104
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (Dm.truthy(initialized))
            return SS_INIT_SUCCESS;
        if (Dm.truthy(current_map.defaulted))
        {
            datum_map_config old_config = current_map;
            // tg: current_map = config.defaultmap (the maps.txt default, controllers/configuration: not loaded in M3, D108)
            Dm.Deferred("code/controllers/subsystem/mapping.dm:108", "Initialize: config.defaultmap (maps.txt and the map vote are not translated): the defaulted config stays");
            current_map = null;
            if (current_map == null || Dm.truthy(current_map.defaulted))
            {
                to_chat(world, span_boldannounce(Dm.text("Unable to load next or default map config, defaulting to [].", old_config.map_name)));
                current_map = old_config;
            }
        }
        // tg: :113-126  the plane cube (plane_offset_to_true, true_to_offset_planes, plane_to_offset, the FLOAT_PLANE cases,
        // plane_offset_blacklist, render_offset_blacklist, critical_planes, create_plane_offsets(0, 0)): BYOND's plane
        // masters, client presentation the 3D client does not have; the lists stay null so the plane macros keep their
        // base case (__HELPERS/_planes.cs).
        Dm.Deferred("code/controllers/subsystem/mapping.dm:113", "Initialize: the plane cube (create_plane_offsets) is BYOND client presentation");
        initialize_biomes();
        await loadWorld();
        determine_fake_sale();
        require_area_resort();
        process_teleport_locs(); //Sets up the wizard teleport locations
        preloadTemplates(); // M10 slice G2: the shuttles'; the survival capsules' shelters; the holodeck's programs; the ruins
        // tg: :134-157  #ifndef SKIP_SPACE_LEVELS: the space ruin and empty levels, the wilderness and the away missions: D108 loads
        // the station level only (with CentCom and Lavaland)
        await setup_ruins();
        // Run map generation after ruin space is reserved, since this space is used for the cave gen.
        await run_map_terrain_generation();
        // Generate our rivers, we do this here so the map doesn't load on top of them
        await setup_rivers();
        // Now that terrain generation is done, actually load the ruin maps in.
        await load_reserved_ruins();
        // now that the terrain is generated, including rivers, we can safely populate it with objects and mobs
        await run_map_terrain_population();
        // DEVIATION(DV509): tg's world is never smaller than CentCom's 255 x 255, so a reserved level always has room for a dock and for a shuttle's
        // transit. With no CentCom level the world of a small station that has a dock is grown here, after the station loaded at its own place and
        // before anything reads the world's size, to the least that has that room (tg's own procs: the new turfs are the global area's).
        bool expanded_x = Dm.world.maxx < ReservedLevelLeastSize, expanded_y = Dm.world.maxy < ReservedLevelLeastSize, has_dock = false;
        if (expanded_x || expanded_y)
        {
            foreach (turf T in Dm.each<turf>(block(1, 1, 1, Dm.world.maxx, Dm.world.maxy, Dm.world.maxz)))
            {
                if (Dm.locate<obj_docking_port_stationary>(T) == null)
                    continue;
                has_dock = true;
                break;
            }
        }
        if (has_dock)
        {
            Dm.world.increase_max_x(ReservedLevelLeastSize);
            Dm.world.increase_max_y(ReservedLevelLeastSize);
            // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_EXPANDED_WORLD_BOUNDS, expanded_x, expanded_y), as the map loader sends it (reader.cs)
            if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_EXPANDED_WORLD_BOUNDS]))
                SSdcs._SendSignal(COMSIG_GLOB_EXPANDED_WORLD_BOUNDS, Dm.list(SSdcs, expanded_x, expanded_y));
        }
        // Add the first transit level (M10 slice G1, D272: the shuttles' templates and transit docks)
        var base_transit = (datum_space_level)await add_reservation_zlevel();
        require_area_resort();
        // tg: :171 setup_map_transitions(): D108 (the space transitions link levels D108 does not load)
        generate_station_area_list();
        await initialize_reserved_level(base_transit.z_value);
        calculate_default_z_level_gravities();

        return SS_INIT_SUCCESS;
    }

    // The New() that SUBSYSTEM_DEF(mapping) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSmapping) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/mapping.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSmapping != this)
        {
            if (SSmapping is datum_controller_subsystem_mapping)
            {
                Recover();
                qdel(SSmapping);
            }
            SSmapping = this;
        }
        PreInit();
    }

    // The vars of mapping.dm that the plane macros (__HELPERS/_planes.dm: SET_PLANE_IMPLICIT, GET_NEW_PLANE,
    // GET_Z_PLANE_OFFSET, PLANE_TO_OFFSET) and /atom/proc/has_gravity read. The stub never fills them: max_plane_offset stays
    // 0, so the plane macros take their base case, and gravity_by_z_level stays empty.

    // tg: code/controllers/subsystem/mapping.dm:35  (W4.0: /area/proc/reg_in_areas_in_z fills it, areas.dm:377)
    public DmList areas_in_z;

    /// List of z level (as number) -> plane offset of that z level
    /// Used to maintain the plane cube
    // tg: code/controllers/subsystem/mapping.dm:38
    public DmList z_level_to_plane_offset;
    /// List of z level (as number) -> The lowest plane offset in that z stack
    // tg: code/controllers/subsystem/mapping.dm:43  (W4.0: GET_LOWEST_STACK_OFFSET, __HELPERS/_planes.cs)
    public DmList z_level_to_lowest_plane_offset;
    // This pair allows for easy conversion between an offset plane, and its true representation
    // Both are in the form "input plane" -> output plane(s)
    /// Assoc list of string plane values to their true, non offset representation
    // tg: code/controllers/subsystem/mapping.dm:47  (W4.0: PLANE_TO_TRUE, __HELPERS/_planes.cs)
    public DmList plane_offset_to_true;
    /// Assoc list of string plane to the plane's offset value
    // tg: code/controllers/subsystem/mapping.dm:51
    public DmList plane_to_offset;
    /// List of planes that do not allow for offsetting
    // tg: code/controllers/subsystem/mapping.dm:53
    public DmList plane_offset_blacklist;
    /// The largest plane offset we've generated so far
    // tg: code/controllers/subsystem/mapping.dm:59
    public float max_plane_offset = 0;

    ///list of all z level indices that form multiz connections and whether theyre linked up or down.
    ///list of lists, inner lists are of the form: list("up or down link direction" = TRUE)
    // tg: code/controllers/subsystem/mapping.dm:80  (W4.0: GET_TURF_ABOVE and GET_TURF_BELOW read it, __DEFINES/multiz.cs;
    // the stub never fills it, so no turf has a turf above or below)
    public DmList multiz_levels;

    ///shows the default gravity value for each z level. recalculated when gravity generators change.
    ///List in the form: list(z level num = max generator gravity in that z level OR the gravity level trait)
    // tg: code/controllers/subsystem/mapping.dm:89
    public DmList gravity_by_z_level;

    // tg: code/controllers/subsystem/mapping.dm:346
    public virtual object get_reservation_from_turf(turf T = null)
    {
        // RETURN_TYPE(/datum/turf_reservation)
        return used_turfs[T];
    }
}

/**
 * Global list of AREA TYPES that are associated with the station.
 *
 * This tracks the types of all areas in existence that are a UNIQUE_AREA and are on the station Z.
 *
 * This does not track the area instances themselves - See [GLOB.areas] for that.
 */
// GLOBAL_LIST_EMPTY(the_station_areas) (__DEFINES/__globals.dm:51), expanded inline (rules 10.6, 11.1 (b))
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/controllers/subsystem/mapping.dm:509
    public DmList the_station_areas;

    // tg: code/controllers/subsystem/mapping.dm:509  (GLOBAL_MANAGED(the_station_areas, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalthe_station_areas()
    {
        the_station_areas = Dm.list();
        Dm.append(ref gvars_datum_init_order, "the_station_areas");
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/mapping.dm:1  SUBSYSTEM_DEF(mapping): GLOBAL_REAL(SSmapping, /datum/controller/subsystem/mapping)
    internal datum_controller_subsystem_mapping SSmapping;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_mapping SSmapping => ref TgState.Current.SSmapping;
}

// M3 contracts (phase B): the roots of this file's procs that no part declared yet, with the signatures of
// docs/ai/specs/generated/slots.tsv (DmTask exactly where suspends = yes), Dm.NotTranslated until M3 P2 translates
// them (docs/ai/specs/m3-ownership.md). Overrides are not declared, so the inherited code keeps running until then.

// tg: code/controllers/subsystem/mapping.dm:218
public partial class datum_controller_subsystem_mapping : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/mapping.dm:218
    public virtual void calculate_default_z_level_gravities()
    {
        for (float z_level = 1, z_level__end = length(z_list); z_level <= z_level__end; z_level++)
            calculate_z_level_gravity(z_level);
    }

    // tg: code/controllers/subsystem/mapping.dm:222
    public virtual void generate_z_level_linkages()
    {
        for (float z_level = 1, z_level__end = length(z_list); z_level <= z_level__end; z_level++)
            generate_linkages_for_z_level(z_level);
    }

    // tg: code/controllers/subsystem/mapping.dm:226
    public virtual object generate_linkages_for_z_level(object z_level = null)
    {
        if (!isnum(z_level) || Dm.le(z_level, 0))
            return FALSE;

        if (Dm.lt(multiz_levels.len, z_level))
            multiz_levels.len = Dm.num(z_level);

        object z_above = level_trait(z_level, ZTRAIT_UP);
        object z_below = level_trait(z_level, ZTRAIT_DOWN);
        if (!(Dm.eq(z_above, TRUE) || Dm.eq(z_above, FALSE) || z_above == null) || !(Dm.eq(z_below, TRUE) || Dm.eq(z_below, FALSE) || z_below == null))
            stack_trace("Warning, numeric mapping offsets are deprecated. Instead, mark z level connections by setting UP/DOWN to true if the connection is allowed", "code/controllers/subsystem/mapping.dm", 236);
        multiz_levels[z_level] = Dm.list_n(LARGEST_Z_LEVEL_INDEX);
        ((DmList)multiz_levels[z_level])[Z_LEVEL_UP] = Dm.truthy(z_above) ? TRUE : FALSE;
        ((DmList)multiz_levels[z_level])[Z_LEVEL_DOWN] = Dm.truthy(z_below) ? TRUE : FALSE;
        return null;
    }

    // tg: code/controllers/subsystem/mapping.dm:241
    public virtual object calculate_z_level_gravity(object z_level_number = null)
    {
        if (!isnum(z_level_number) || Dm.lt(z_level_number, 1))
            return FALSE;

        object max_gravity = 0f;

        // M3 R4: the station's gravity generator registers in GLOB.gravity_generators (modules/power/gravitygenerator.cs).
        foreach (obj_machinery_gravity_generator_main grav_gen in Dm.each_cast<obj_machinery_gravity_generator_main>(GLOB.gravity_generators[Dm.text("[]", z_level_number)]))
            max_gravity = max(grav_gen.setting, max_gravity);

        max_gravity = Dm.truthy(max_gravity) ? max_gravity : (level_trait(z_level_number, ZTRAIT_GRAVITY) ?? 0f); //just to make sure no nulls
        if (!Dm.truthy(max_gravity)) max_gravity = 0f;
        // M3 R4: a level SSmapping does not manage (the host-built test and toy rooms, D108; tg manages every level, so
        // manage_z_level has sized the list) has no entry to store into, as level_trait has no traits for it
        // (not-translated.md section 3, traits.dm:5); a gravity generator standing there computes its gravity only.
        if (gravity_by_z_level == null || Dm.num(z_level_number) > gravity_by_z_level.len)
            return max_gravity;
        gravity_by_z_level[z_level_number] = max_gravity;
        return max_gravity;
    }

    /**
     * ##setup_ruins
     *
     * Sets up all of the ruins to be spawned
     */
    // tg: code/controllers/subsystem/mapping.dm:259
    public virtual async DmTask setup_ruins()
    {
        // Generate mining ruins
        DmList lava_ruins = levels_by_trait(ZTRAIT_LAVA_RUINS) as DmList;
        if (Dm.truthy(lava_ruins.len))
            await seedRuins(lava_ruins, Dm.num(config.Get(typeof(datum_config_entry_number_lavaland_budget))), Dm.list(typeof(area_lavaland_surface_outdoors_unexplored)), themed_ruins[ZTRAIT_LAVA_RUINS] as DmList, clear_below: TRUE, mineral_budget: 15, mineral_budget_update: OREGEN_PRESET_LAVALAND, ruins_type: ZTRAIT_LAVA_RUINS);

        DmList ice_ruins = levels_by_trait(ZTRAIT_ICE_RUINS) as DmList;
        if (Dm.truthy(ice_ruins.len))
            // needs to be whitelisted for underground too so place_below ruins work
            await seedRuins(ice_ruins, Dm.num(config.Get(typeof(datum_config_entry_number_icemoon_budget))), Dm.list(typeof(area_icemoon_surface_outdoors_unexplored), typeof(area_icemoon_underground_unexplored)), themed_ruins[ZTRAIT_ICE_RUINS] as DmList, clear_below: TRUE, mineral_budget: 4, mineral_budget_update: OREGEN_PRESET_TRIPLE_Z, ruins_type: ZTRAIT_ICE_RUINS);

        DmList ice_ruins_underground = levels_by_trait(ZTRAIT_ICE_RUINS_UNDERGROUND) as DmList;
        if (Dm.truthy(ice_ruins_underground.len))
            await seedRuins(ice_ruins_underground, Dm.num(config.Get(typeof(datum_config_entry_number_icemoon_budget))), Dm.list(typeof(area_icemoon_underground_unexplored)), themed_ruins[ZTRAIT_ICE_RUINS_UNDERGROUND] as DmList, clear_below: TRUE, mineral_budget: 21, ruins_type: ZTRAIT_ICE_RUINS_UNDERGROUND);

        // Generate deep space ruins
        DmList space_ruins = levels_by_trait(ZTRAIT_SPACE_RUINS) as DmList;
        if (Dm.truthy(space_ruins.len))
        {
            // Create a proportional budget by multiplying the amount of space ruin levels in the current map over the default amount
            float proportional_budget = Dm.round(Dm.num(config.Get(typeof(datum_config_entry_number_space_budget))) * (space_ruins.len / (float)DEFAULT_SPACE_RUIN_LEVELS));
            await seedRuins(space_ruins, proportional_budget, Dm.list(typeof(area_space)), themed_ruins[ZTRAIT_SPACE_RUINS] as DmList, mineral_budget: 0, ruins_type: ZTRAIT_SPACE_RUINS);
        }
    }

    ///loads all of the ruins we previously reserved space for
    // tg: code/controllers/subsystem/mapping.dm:282
    public virtual async DmTask load_reserved_ruins()
    {
        foreach (DmList reservation in Dm.each<DmList>(reserved_ruins))
        {
            var reserved_ruin = (datum_map_template_ruin)reservation[1];
            var central_turf = (turf)reservation[2];
            object clear_below = reservation[3];
            await load_ruin_now(reserved_ruin, central_turf, clear_below);
        }
        reserved_ruins.Cut();
    }

    /**
     * Immediately loads a single reserved ruin's map, and runs terrain generation for any
     * of the areas, since they get spawned AFTER normal terrain gen runs its pass
     */
    // tg: code/controllers/subsystem/mapping.dm:294
    public virtual async DmTask load_ruin_now(datum_map_template_ruin reserved_ruin = null, turf central_turf = null, object clear_below = null)
    {
        float starting_area_count = GLOB.areas.len;
        await reserved_ruin.load_reserved(central_turf, clear_below);
        for (float i = starting_area_count + 1; i <= GLOB.areas.len; i++)
        {
            var ruin_area = (area)GLOB.areas[i];
            await ruin_area.RunTerrainGeneration();
        }
    }

    // tg: code/controllers/subsystem/mapping.dm:303
    public virtual async DmTask setup_rivers()
    {
        // Generate mining ruins
        DmList lava_ruins = levels_by_trait(ZTRAIT_LAVA_RUINS) as DmList;
        foreach (object lava_z in Dm.each(lava_ruins))
            await spawn_rivers(lava_z, 3, typeof(turf_open_lava_smooth_lava_land_surface), typeof(area_lavaland_surface_outdoors_unexplored)); // +1 from the mapped in waypoint

        DmList ice_ruins = levels_by_trait(ZTRAIT_ICE_RUINS) as DmList;
        foreach (object ice_z in Dm.each(ice_ruins))
            await spawn_rivers(ice_z, 6, typeof(turf_open_lava_plasma_ice_moon), typeof(area_icemoon_surface_outdoors_unexplored_rivers));

        DmList ice_ruins_underground = levels_by_trait(ZTRAIT_ICE_RUINS_UNDERGROUND) as DmList;
        foreach (object ice_z in Dm.each(ice_ruins_underground))
            await spawn_rivers(ice_z, 4, level_trait(ice_z, ZTRAIT_BASETURF), typeof(area_icemoon_underground_unexplored_rivers));
    }

    // tg: code/controllers/subsystem/mapping.dm:317
    public virtual async DmTask wipe_reservations(float? wipe_safety_delay = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/mapping.dm:317", "wipe_reservations: M3 P2 (skeleton)");
    }

    // tg: code/controllers/subsystem/mapping.dm:339
    public virtual async DmTask safety_clear_transit_dock(obj_docking_port_stationary_transit T = null, obj_docking_port_mobile M = null, DmList returning = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/mapping.dm:339", "safety_clear_transit_dock: M3 P2 (skeleton)");
    }

    // tg: code/controllers/subsystem/mapping.dm:354
    public virtual void add_nuke_threat(datum nuke = null)
    {
        nuke_threats[nuke] = TRUE;
        check_nuke_threats();
    }

    // tg: code/controllers/subsystem/mapping.dm:358
    public virtual void remove_nuke_threat(datum nuke = null)
    {
        nuke_threats.Remove(nuke);
        check_nuke_threats();
    }

    // tg: code/controllers/subsystem/mapping.dm:362
    public virtual void check_nuke_threats()
    {
        foreach (object d in Dm.each<object>(nuke_threats.Copy()))
        {
            if (d is not datum dd || QDELETED(dd))
                nuke_threats.Remove(d);
        }

        foreach (turf_open_floor_circuit C in Dm.each<turf_open_floor_circuit>(nuke_tiles))
            C.update_appearance();
    }

    // tg: code/controllers/subsystem/mapping.dm:371
    public virtual void determine_fake_sale()
    {
        if (Dm.truthy(length(SSmapping.levels_by_all_traits(Dm.list(ZTRAIT_STATION, ZTRAIT_NOPARALLAX)))))
            Dm.append(ref GLOB.arcade_prize_pool, typeof(obj_item_stack_tile_fakeice_loaded));
        else
            Dm.append(ref GLOB.arcade_prize_pool, typeof(obj_item_stack_tile_fakespace_loaded));
    }

    // tg: code/controllers/subsystem/mapping.dm:403
    public virtual async DmTask<object> LoadGroup(DmList errorList = null, object name = null, object path = null, object files = null, DmList traits = null, DmList default_traits = null, float? silent = null, float? height_autosetup = null)
    {
        silent ??= FALSE;
        height_autosetup ??= TRUE;
        DmList dot = Dm.list();
        float start_time = REALTIMEOFDAY();

        if (!islist(files))  // handle single-level maps
            files = Dm.list(files);

        // check that the total z count of all maps matches the list of traits
        float total_z = 0;
        DmList parsed_maps = Dm.list();
        foreach (object file in Dm.each(files))
        {
            string full_path = Dm.text("_maps/[]/[]", path, file);
            var pm = Dm.@new<datum_parsed_map>(Dm.file(full_path));
            DmList bounds = pm?.bounds;
            if (!Dm.truthy(bounds))
            {
                Dm.combine(ref errorList, full_path);
                continue;
            }
            parsed_maps[pm] = total_z;  // save the start Z of this file
            total_z += Dm.num(bounds[MAP_MAXZ]) - Dm.num(bounds[MAP_MINZ]) + 1;
        }

        if (!Dm.truthy(length(traits)))  // null or empty - default
        {
            for (float i = 1, i__end = total_z; i <= i__end; i++)
                Dm.append(ref traits, Dm.list(default_traits.Copy()));
        }
        else if (!Dm.eq(total_z, traits.len))  // mismatch
        {
            // INIT_ANNOUNCE(X) (mapping.dm:461: to_chat(world, span_boldannounce("[X]")); log_world(X)), expanded inline
            string announce = Dm.text("WARNING: [] trait sets specified for [] z-levels in []!", traits.len, total_z, path);
            to_chat(world, span_boldannounce(announce));
            log_world(announce);
            if (total_z < traits.len)  // ignore extra traits
                traits.Cut(total_z + 1);
            while (total_z > traits.len)  // fall back to defaults on extra levels
                Dm.append(ref traits, Dm.list(default_traits.Copy()));
        }

        if (total_z > 1 && Dm.truthy(height_autosetup)) // it's a multi z map, and we haven't opted out of trait autosetup
        {
            for (float z = 1, z__end = total_z; z <= z__end; z++)
            {
                if (z == 1) // bottom z-level
                    ((DmList)traits[z])["Up"] = TRUE;
                else if (z == total_z) // top z-level
                    ((DmList)traits[z])["Down"] = TRUE;
                else
                {
                    ((DmList)traits[z])["Down"] = TRUE;
                    ((DmList)traits[z])["Up"] = TRUE;
                }
            }
        }

        // preload the relevant space_level datums
        float start_z = Dm.world.maxz + 1;
        float i2 = 0;
        foreach (object level in Dm.each(traits))
        {
            await add_new_zlevel(Dm.text("[][]", name, Dm.truthy(i2) ? Dm.text(" []", i2 + 1) : ""), level, contain_turfs: FALSE);
            ++i2;
        }

        // load the maps
        foreach (object P in Dm.each(parsed_maps))
        {
            var pm = (datum_parsed_map)P;
            DmList bounds = pm.bounds;
            float x_offset = Dm.truthy(bounds) ? round(Dm.world.maxx / 2f - Dm.num(bounds[MAP_MAXX]) / 2f) + 1 : 1;
            float y_offset = Dm.truthy(bounds) ? round(Dm.world.maxy / 2f - Dm.num(bounds[MAP_MAXY]) / 2f) + 1 : 1;
            // DEVIATION(DV239): CentCom (255 x 255) loads after a smaller station, where tg's world is never smaller than it; a level wider
            // than the world starts at 1 and grows it, rather than being centred at a negative offset that crops it (multiz_debug's docks)
            x_offset = Math.Max(x_offset, 1);
            y_offset = Math.Max(y_offset, 1);
            if (!Dm.truthy(await pm.load(x_offset, y_offset, start_z + Dm.num(parsed_maps[P]), no_changeturf: TRUE, new_z: TRUE)))
                Dm.combine(ref errorList, pm.original_path);
        }
        if (!Dm.truthy(silent))
        {
            string announce = Dm.text("Loaded [] in []s!", name, (REALTIMEOFDAY() - start_time) / 10);
            to_chat(world, span_boldannounce(announce));
            log_world(announce);
        }
        return parsed_maps;
    }

    // tg: code/controllers/subsystem/mapping.dm:462
    public virtual async DmTask loadWorld()
    {
        //if any of these fail, something has gone horribly, HORRIBLY, wrong
        DmList FailedZs = Dm.list();

        // ensure we have space_level datums for compiled-in maps
        InitializeDefaultZLevels();

        // load the station
        station_start = Dm.world.maxz + 1;
        // INIT_ANNOUNCE(X) (:461), expanded inline (rules 11.1 (e))
        string announce = Dm.text("Loading []...", current_map.map_name);
        to_chat(world, span_boldannounce(announce));
        log_world(announce);
        // ZTRAITS_STATION (__DEFINES/maps.dm:134), expanded inline
        await LoadGroup(FailedZs, "Station", current_map.map_path, current_map.map_file, current_map.traits as DmList, Dm.list(Dm.kv(ZTRAIT_LINKAGE, CROSSLINKED), Dm.kv(ZTRAIT_STATION, TRUE)), height_autosetup: current_map.height_autosetup);

        // DEVIATION(DV239): CentCom loads after the station (tg's loadWorld loads it first, as z 1), so the station stays on z 1, which every coordinate here assumes
        if (Dm.truthy(Dm.fexists("_maps/map_files/generic/CentCom.dmm")))
            await LoadGroup(FailedZs, "CentCom", "map_files/generic", "CentCom.dmm", default_traits: Dm.list(Dm.kv(ZTRAIT_CENTCOM, TRUE), Dm.kv(ZTRAIT_NOPHASE, TRUE)), silent: TRUE); // ZTRAITS_CENTCOM

        // tg: :476-481  if(SSdbcore.Connect()) the round's map_name query: no database (D76, a later milestone)

        // tg: :483-488  #ifndef SKIP_LAVALAND (D304); a maps folder without Lavaland keeps its stand-in docks (DV236)
        if (current_map.minetype == MINETYPE_LAVALAND)
        {
            if (Dm.truthy(Dm.fexists("_maps/map_files/Mining/Lavaland.dmm")))
                await LoadGroup(FailedZs, "Lavaland", "map_files/Mining", "Lavaland.dmm", default_traits: Dm.list(
                    Dm.kv(ZTRAIT_MINING, TRUE), Dm.kv(ZTRAIT_NOPARALLAX, TRUE), Dm.kv(ZTRAIT_ASHSTORM, TRUE), Dm.kv(ZTRAIT_LAVA_RUINS, TRUE),
                    Dm.kv(ZTRAIT_BOMBCAP_MULTIPLIER, 2), Dm.kv(ZTRAIT_BASETURF, typeof(turf_open_lava_smooth_lava_land_surface)))); // ZTRAITS_LAVALAND
        }
        else if (current_map.minetype != null && current_map.minetype != MINETYPE_NONE && current_map.minetype != MINETYPE_ICE)
        {
            string unknown = Dm.text("WARNING: An unknown minetype '[]' was set! This is being ignored! Update the maploader code!", current_map.minetype);
            to_chat(world, span_boldannounce(unknown));
            log_world(unknown);
        }

        if (Dm.truthy(length(FailedZs))) //but seriously, unless the server's filesystem is messed up this will never happen
        {
            string msg = Dm.text("RED ALERT! The following map files failed to load: []", FailedZs[1]);
            if (FailedZs.len > 1)
            {
                for (float I = 2, I__end = FailedZs.len; I <= I__end; I++)
                    msg += Dm.text(", []", FailedZs[I]);
            }
            msg += ". Yell at your server host!";
            to_chat(world, span_boldannounce(msg));
            log_world(msg);
        }

        // Custom maps are removed after station loading so the map files does not persist for no reason.
        if (current_map.map_path == CUSTOM_MAP_PATH)
            fdel(Dm.text("_maps/custom/[]", current_map.map_file));
    }

    /// Generates the global station area list, filling it with typepaths of unique areas found on the station Z.
    // tg: code/controllers/subsystem/mapping.dm:512
    public virtual void generate_station_area_list()
    {
        foreach (area_station station_area in Dm.each<area_station>(GLOB.areas))
        {
            if (!Dm.truthy(Dm.bitand(station_area.area_flags_mapping, UNIQUE_AREA)))
                continue;
            // is_station_level(z) (__HELPERS/_level_traits.dm:5), expanded inline (rules 11.1 (e))
            if (Dm.truthy(SSmapping.level_trait(station_area.z, ZTRAIT_STATION)))
                Dm.append(ref GLOB.the_station_areas, station_area.type);
        }

        if (!Dm.truthy(GLOB.the_station_areas.len))
            log_world("ERROR: Station areas list failed to generate!");
    }

    /// Generate the turfs of the area
    // tg: code/controllers/subsystem/mapping.dm:523
    public virtual async DmTask run_map_terrain_generation()
    {
        foreach (area A in Dm.each<area>(GLOB.areas))
            await A.RunTerrainGeneration();
    }

    /// Populate the turfs of the area
    // tg: code/controllers/subsystem/mapping.dm:528
    public virtual async DmTask run_map_terrain_population()
    {
        foreach (area A in Dm.each<area>(GLOB.areas))
            await A.RunTerrainPopulation();
    }

    // tg: code/controllers/subsystem/mapping.dm:532
    public virtual void preloadTemplates(string path = null)
    {
        path ??= "_maps/templates/";
        DmList filelist = flist(path);
        foreach (object map in Dm.each(filelist))
        {
            var T = (datum_map_template)Dm.@new(typeof(datum_map_template), Dm.text("[][]", path, map), Dm.text("[]", map));
            map_templates[T.name] = T;
        }

        preloadRuinTemplates();
        preloadShuttleTemplates();
        preloadShelterTemplates();
        preloadHolodeckTemplates();
    }

    // tg: code/controllers/subsystem/mapping.dm:543
    public virtual void preloadRuinTemplates()
    {
        // Still supporting bans by filename
        DmList banned = generateMapList("spaceruinblacklist.txt") as DmList;
        if (Dm.eq(current_map.minetype, MINETYPE_LAVALAND))
            Dm.append(ref banned, generateMapList("lavaruinblacklist.txt"));
        else if (Dm.truthy(current_map.blacklist_file))
            Dm.append(ref banned, generateMapList(current_map.blacklist_file));

        foreach (Type ruin_type in Dm.each<Type>(sort_list(subtypesof(typeof(datum_map_template_ruin)), "/proc/" + nameof(cmp_ruincost_priority))))
        {
            // screen out the abstract subtypes
            if (!Dm.truthy(Dm.initial(ruin_type, (datum_map_template_ruin r) => r.id)))
                continue;
            // DEVIATION(DV635): only the ruins whose map the host mounted (Lavaland's, maps/RandomRuins); tg preloads every ruin of
            // every planet and of space, whose maps the port has not converted (the ice moon is not loaded; the space ruins come with
            // the space level). A ruin whose map file is picked in its New (the random blood-drunk miner) is made to know its file.
            object prefix = Dm.initial(ruin_type, (datum_map_template_ruin r) => r.prefix);
            object suffix = Dm.initial(ruin_type, (datum_map_template_ruin r) => r.suffix);
            if (suffix != null ? !Dm.truthy(fexists(Dm.text("[][]", prefix, suffix))) : !Dm.truthy((flist(prefix) as DmList)?.len))
                continue;
            var R = (datum_map_template_ruin)Dm.@new(ruin_type);

            if (Dm.truthy(banned.Find(R.mappath)))
                continue;

            map_templates[R.name] = R;
            ruins_templates[R.name] = R;

            if (!Dm.truthy(Dm.@in(R.ruin_type, themed_ruins)))
                themed_ruins[R.ruin_type] = Dm.list();
            (themed_ruins[R.ruin_type] as DmList)[R.name] = R;
        }
    }

    // tg: code/controllers/subsystem/mapping.dm:568
    public virtual void preloadShuttleTemplates()
    {
        DmList unbuyable = generateMapList("unbuyableshuttles.txt") as DmList;

        foreach (Type shuttle_type in Dm.each<Type>(subtypesof(typeof(datum_map_template_shuttle))))
        {
            object suffix = Dm.initial(shuttle_type, (datum_map_template_shuttle p) => p.suffix);
            if (!Dm.truthy(suffix))
                continue;
            // DEVIATION(DV188): only the templates whose map the host mounted (the round-start docks' templates and the emergency
            // shuttle catalogue the converter writes, maps/shuttles); tg preloads every one of its 200-odd shuttle maps
            object mappath = Dm.text("[][]_[].dmm", Dm.initial(shuttle_type, (datum_map_template_shuttle p) => p.prefix), Dm.initial(shuttle_type, (datum_map_template_shuttle p) => p.port_id), suffix);
            if (!Dm.truthy(fexists(mappath)))
                continue;

            var S = (datum_map_template_shuttle)Dm.@new(shuttle_type);
            if (Dm.truthy(unbuyable.Find(S.mappath)))
                S.who_can_purchase = null;

            shuttle_templates[S.shuttle_id] = S;
            map_templates[S.shuttle_id] = S;
        }

        // DEVIATION(DV506): a shuttle that is a map of the maps folder and has no template type of its own (one made in the map editor) gets a template made
        // from its file, shuttles/<port id>/<port id>_<suffix>, with the name, the description and the price the json beside it gives (MapSettings). tg
        // writes a type for every shuttle, which a mapper without the code cannot.
        foreach ((string folder, string file, MapSettings data) in MapSeam.ShuttleFiles(World.Current))
        {
            if (!file.StartsWith(folder + "_", StringComparison.Ordinal) || shuttle_templates[file] != null)
                continue;
            MapSeam.MakeShuttle(World.Current, (folder, file.Substring(folder.Length + 1), data));
            try
            {
                var S = (datum_map_template_shuttle)Dm.@new(typeof(datum_map_template_shuttle));
                shuttle_templates[S.shuttle_id] = S;
                map_templates[S.shuttle_id] = S;
            }
            finally
            {
                MapSeam.MakeShuttle(World.Current, null);
            }
        }
    }

    // tg: code/controllers/subsystem/mapping.dm:583
    public virtual void preloadShelterTemplates()
    {
        foreach (Type item in Dm.each<Type>(subtypesof(typeof(datum_map_template_shelter))))
        {
            Type shelter_type = item;
            object mappath = Dm.initial(shelter_type, (datum_map_template_shelter p) => p.mappath);
            if (!Dm.truthy(mappath))
                continue;
            // DEVIATION(DV611): only the shelters whose map the host mounted (the converted _maps/templates/shelter_*.dmm and
            // fishing_*.dmm); a template whose file is absent (a host without the converted templates, a toy test room) would fail to load
            if (!Dm.truthy(fexists(mappath)))
                continue;
            var S = (datum_map_template_shelter)Dm.@new(shelter_type);

            shelter_templates[S.shelter_id] = S;
            map_templates[S.shelter_id] = S;
        }
    }

    // tg: code/controllers/subsystem/mapping.dm:593
    public virtual void preloadHolodeckTemplates()
    {
        foreach (Type item in Dm.each<Type>(subtypesof(typeof(datum_map_template_holodeck))))
        {
            Type holodeck_type = item;
            if (!Dm.truthy(Dm.initial(holodeck_type, (datum_map_template p) => p.mappath)))
                continue;
            var holo_template = (datum_map_template_holodeck)Dm.@new(holodeck_type);

            holodeck_templates[holo_template.template_id] = holo_template;
        }
    }
}

// tg: code/controllers/subsystem/mapping.dm:602
public partial class client : datum
{
    // tg: code/controllers/subsystem/mapping.dm:602
    public virtual async DmTask __avd_load_away_mission()
    {
        Dm.NotTranslated("code/controllers/subsystem/mapping.dm:602", "__avd_load_away_mission: M3 P2 (skeleton)");
    }
}

// tg: code/controllers/subsystem/mapping.dm:640
public partial class datum_controller_subsystem_mapping : datum_controller_subsystem
{
    /// Adds a new reservation z level. A bit of space that can be handed out on request
    /// Of note, reservations default to transit turfs, to make their most common use, shuttles, faster
    // tg: code/controllers/subsystem/mapping.dm:640
    public virtual async DmTask<object> add_reservation_zlevel(object for_shuttles = null)
    {
        num_of_res_levels++;
        return await add_new_zlevel(Dm.text("Transit/Reserved #[]", num_of_res_levels), Dm.list(Dm.kv(ZTRAIT_RESERVED, TRUE)));
    }

    /// Requests a /datum/turf_reservation based on the given width, height, and z_size. You can specify a z_reservation to use a specific z level, or leave it null to use any z level.
    // tg: code/controllers/subsystem/mapping.dm:645
    public virtual async DmTask<object> request_turf_block_reservation(object width = null, object height = null, float? z_size = null, object z_reservation = null, Type reservation_type = null, object turf_type_override = null)
    {
        z_size ??= 1;
        reservation_type ??= typeof(datum_turf_reservation);
        // UNTIL((!z_reservation || reservation_ready["[z_reservation]"]) && !clearing_reserved_turfs) (__DEFINES/_helpers.dm), expanded inline
        while (!((z_reservation == null || Dm.truthy(reservation_ready[Dm.text("[]", z_reservation)])) && !Dm.truthy(clearing_reserved_turfs)))
            await stoplag();
        var reserve = (datum_turf_reservation)Dm.@new(reservation_type);
        if (turf_type_override != null)
            reserve.turf_type = (Type)turf_type_override;
        if (z_reservation == null)
        {
            foreach (object i in Dm.each(levels_by_trait(ZTRAIT_RESERVED)))
            {
                if (Dm.truthy(await reserve.reserve(width, height, z_size, i)))
                    return reserve;
            }
            //If we didn't return at this point, theres a good chance we ran out of room on the exisiting reserved z levels, so lets try a new one
            var newReserved = (datum_space_level)await add_reservation_zlevel();
            await initialize_reserved_level(newReserved.z_value);
            if (Dm.truthy(await reserve.reserve(width, height, z_size, newReserved.z_value)))
                return reserve;
        }
        else
        {
            if (!Dm.truthy(level_trait(z_reservation, ZTRAIT_RESERVED)))
            {
                qdel(reserve);
                return null;
            }
            else
            {
                if (Dm.truthy(await reserve.reserve(width, height, z_size, z_reservation)))
                    return reserve;
            }
        }
        // QDEL_NULL(reserve) (__DEFINES/qdel.dm), expanded inline
        qdel(reserve);
        return null;
    }

    ///Sets up a z level as reserved
    ///This is not for wiping reserved levels, use wipe_reservations() for that.
    ///If this is called after SSatom init, it will call Initialize on all turfs on the passed z, as its name promises
    // tg: code/controllers/subsystem/mapping.dm:678
    public virtual async DmTask initialize_reserved_level(object z = null)
    {
        // UNTIL(!clearing_reserved_turfs) (__DEFINES/_helpers.dm), expanded inline: regardless, lets add a check just in case.
        while (Dm.truthy(clearing_reserved_turfs))
            await stoplag();
        clearing_reserved_turfs = TRUE; //This operation will likely clear any existing reservations, so lets make sure nothing tries to make one while we're doing it.
        if (!Dm.truthy(level_trait(z, ZTRAIT_RESERVED)))
        {
            clearing_reserved_turfs = FALSE;
            throw Dm.CRASH("Invalid z level prepared for reservations.", "code/controllers/subsystem/mapping.dm", 683);
        }
        float zlevel = Dm.num(z);
        DmList reserved_block = block(
            SHUTTLE_TRANSIT_BORDER, SHUTTLE_TRANSIT_BORDER, zlevel,
            Dm.world.maxx - SHUTTLE_TRANSIT_BORDER, Dm.world.maxy - SHUTTLE_TRANSIT_BORDER, zlevel);
        foreach (turf T in Dm.each<turf>(reserved_block))
        {
            // No need to empty() these, because they just got created and are already /turf/open/space/basic.
            T.turf_flags = UNUSED_RESERVATION_TURF;
            T.blocks_air = TRUE;
            // CHECK_TICK (__DEFINES/_tick.dm:21), awaited in a suspending proc
            if (TICK_CHECK())
                await stoplag();
        }

        // Gotta create these suckers if we've not done so already
        if (Dm.truthy(SSatoms.initialized))
            SSatoms.InitializeAtoms(Z_TURFS(zlevel));

        unused_turfs[Dm.text("[]", z)] = reserved_block;
        reservation_ready[Dm.text("[]", z)] = TRUE;
        clearing_reserved_turfs = FALSE;
    }

    /// Schedules a group of turfs to be handed back to the reservation system's control
    /// If await is true, will sleep until the turfs are finished work
    // tg: code/controllers/subsystem/mapping.dm:704
    public virtual async DmTask reserve_turfs(DmList turfs = null, float? @await = null)
    {
        @await ??= FALSE;
        Dm.append(ref lists_to_reserve, Dm.list(turfs));
        if (Dm.truthy(@await))
        {
            // UNTIL(!length(turfs)) (__DEFINES/_helpers.dm), expanded inline
            while (Dm.truthy(length(turfs)))
                await stoplag();
        }
    }

    // tg: code/controllers/subsystem/mapping.dm:183
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        // Cache for sonic speed
        DmList unused_turfs = this.unused_turfs;
        var world_area = (area)GLOB.areas_by_type[Dm.world.area];
        // tg caches world_area.contents here and adds each turf to it below; the port moves the turf with loc instead (the same act,
        // atom.builtin.cs: "contents += X is X.loc = src"), because a handed-out contents list of the world area, which holds every
        // space turf, is reconciled whole at the next coordinate read: 24 s of a Meta boot for a thousand turfs
        DmList world_turf_contents_by_z = world_area.turfs_by_zlevel;
        DmList lists_to_reserve = this.lists_to_reserve;
        float index = 0;
        while (index < length(lists_to_reserve))
        {
            var packet = (DmList)lists_to_reserve[index + 1];
            float packetlen = length(packet);
            while (Dm.truthy(packetlen))
            {
                if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                {
                    if (Dm.truthy(index))
                        lists_to_reserve.Cut(1, index);
                    return;
                }
                // TURF-IDENTITY: tg's listed turf names its location, whose turf may have been replaced since it was listed
                var reserving_turf = (turf)turf.dm_identity((turf)packet[packetlen]);
                await reserving_turf.empty(typeof(turf_open_space_basic), typeof(turf_open_space_basic), null, TRUE); // RESERVED_TURF_TYPE (__DEFINES/maps.dm)
                reserving_turf = (turf)turf.dm_identity(reserving_turf); // TURF-IDENTITY: tg's reserving_turf names the new space turf
                string zkey = Dm.text("[]", reserving_turf.z);
                // LAZYINITLIST(unused_turfs["[reserving_turf.z]"]) (__HELPERS/_lists.dm), expanded inline
                if (unused_turfs[zkey] == null)
                    unused_turfs[zkey] = Dm.list();
                Dm.combine_at(unused_turfs, zkey, reserving_turf);
                var old_area = (area)reserving_turf.loc;
                // LISTASSERTLEN(old_area.turfs_to_uncontain_by_zlevel, reserving_turf.z, list()) (__HELPERS/_lists.dm:114), expanded inline
                ListAssertLen(old_area.turfs_to_uncontain_by_zlevel, reserving_turf.z);
                Dm.append_at(old_area.turfs_to_uncontain_by_zlevel, reserving_turf.z, reserving_turf);
                reserving_turf.turf_flags = UNUSED_RESERVATION_TURF;
                // reservation turfs are not allowed to interact with atmos at all
                reserving_turf.blocks_air = TRUE;

                reserving_turf.loc = world_area; // world_contents += reserving_turf
                ListAssertLen(world_turf_contents_by_z, reserving_turf.z);
                Dm.append_at(world_turf_contents_by_z, reserving_turf.z, reserving_turf);
                packet.len--;
                packetlen = length(packet);
            }

            index++;
        }
        lists_to_reserve.Cut(1, index);
    }

    // tg: code/controllers/subsystem/mapping.dm:710
    public virtual async DmTask do_wipe_turf_reservations()
    {
        Dm.NotTranslated("code/controllers/subsystem/mapping.dm:710", "do_wipe_turf_reservations: M3 P2 (skeleton)");
    }

    // tg: code/controllers/subsystem/mapping.dm:728
    public virtual void initialize_biomes()
    {
        foreach (Type biome_path in Dm.each<Type>(subtypesof(typeof(datum_biome))))
        {
            var biome_instance = Dm.@new(biome_path);
            biomes[biome_path] = Dm.add(biomes[biome_path], biome_instance);
        }
    }

    // tg: code/controllers/subsystem/mapping.dm:733
    public virtual void reg_in_areas_in_z(DmList areas = null)
    {
        foreach (area A in Dm.each<area>(areas))
            A.reg_in_areas_in_z();
    }

    // tg: code/controllers/subsystem/mapping.dm:738
    public virtual async DmTask<object> get_isolated_ruin_z()
    {
        if (isolated_ruins_z == null)
        {
            isolated_ruins_z = await add_new_zlevel("Isolated Ruins/Reserved", Dm.list(Dm.kv(ZTRAIT_RESERVED, TRUE), Dm.kv(ZTRAIT_ISOLATED_RUINS, TRUE))) as datum_space_level;
            await initialize_reserved_level(isolated_ruins_z.z_value);
        }
        return isolated_ruins_z.z_value;
    }

    // tg: code/controllers/subsystem/mapping.dm:746
    public virtual void manage_z_level(datum_space_level new_z = null, object filled_with_space = null, float? contain_turfs = null)
    {
        contain_turfs ??= TRUE;
        // First, add the z
        Dm.append(ref z_list, new_z);

        // Then we build our lookup lists
        float z_value = new_z.z_value;
        // We are guarenteed that we'll always grow bottom up
        // Suck it jannies
        z_level_to_plane_offset.len += 1;
        z_level_to_lowest_plane_offset.len += 1;
        gravity_by_z_level.len += 1;
        z_level_to_stack.len += 1;
        // Bare minimum we have ourselves
        z_level_to_stack[z_value] = Dm.list(z_value);
        // 0's the default value, we'll update it later if required
        z_level_to_plane_offset[z_value] = 0f;
        z_level_to_lowest_plane_offset[z_value] = 0f;

        // Now we check if this plane is offset or not
        object below_offset = new_z.traits[ZTRAIT_DOWN];
        if (Dm.truthy(below_offset))
            update_plane_tracking(new_z);

        if (Dm.truthy(contain_turfs))
            build_area_turfs(z_value, filled_with_space);

        if (SSweather != null && Dm.truthy(SSweather.initialized))
            SSweather.generate_holders(z_value, z_value);
    }

    // tg: code/controllers/subsystem/mapping.dm:775
    public virtual void build_area_turfs(object z_level = null, object space_guaranteed = null)
    {
        // If we know this is filled with default tiles, we can use the default area
        // Faster
        if (Dm.truthy(space_guaranteed))
        {
            var global_area = (area)GLOB.areas_by_type[Dm.world.area];
            // LISTASSERTLEN(global_area.turfs_by_zlevel, z_level, list()) (__HELPERS/_lists.dm:114), expanded inline (rules 11.1 (e))
            ListAssertLen(global_area.turfs_by_zlevel, Dm.num(z_level));
            // Z_TURFS(ZLEVEL) (__DEFINES/turfs.dm:26), expanded inline
            global_area.turfs_by_zlevel[z_level] = block(1, 1, Dm.num(z_level), Dm.world.maxx, Dm.world.maxy, Dm.num(z_level));
            return;
        }

        foreach (turf to_contain in Dm.each<turf>(block(1, 1, Dm.num(z_level), Dm.world.maxx, Dm.world.maxy, Dm.num(z_level))))
        {
            var our_area = (area)to_contain.loc;
            ListAssertLen(our_area.turfs_by_zlevel, Dm.num(z_level));
            Dm.append_at(our_area.turfs_by_zlevel, z_level, to_contain);
        }
    }

    // LISTASSERTLEN(list, index, list()) (__HELPERS/_lists.dm:114) for the area lists here, expanded as turf.cs's
    // change_area expands it inline (rules 11.1 (e)): grow `list` to `index`, filling the new slots with new lists.
    internal static void ListAssertLen(DmList list, float index)
    {
        if (length(list) < index)
        {
            float _OLD_LENGTH = length(list);
            list.len = index;
            for (float _INDEX_TO_ASSIGN_TO = _OLD_LENGTH + 1; _INDEX_TO_ASSIGN_TO <= index; _INDEX_TO_ASSIGN_TO++)
                list[_INDEX_TO_ASSIGN_TO] = Dm.list();
        }
    }

    // tg: code/controllers/subsystem/mapping.dm:789
    public virtual void update_plane_tracking(datum_space_level update_with = null)
    {
        // We're essentially going to walk down the stack of connected z levels, and set their plane offset as we go
        // DEVIATION(DV367): every level keeps the plane offset 0 (z_level_to_plane_offset, z_level_to_lowest_plane_offset and
        // max_plane_offset stay 0, so generate_offset_lists never runs): BYOND's offset planes only draw a level below through
        // a transparent turf, and the 3D client draws every level of a stack at its height (D325). The stack itself is tg's.
        float plane_offset = 0;
        datum_space_level current_z = update_with;
        DmList levels_checked = Dm.list();
        DmList z_stack = Dm.list();
        while (true)
        {
            float z_level = current_z.z_value;
            Dm.append(ref z_stack, z_level);
            z_level_to_plane_offset[z_level] = 0f; // tg: = plane_offset (DV367)
            Dm.append(ref levels_checked, current_z);
            if (!Dm.truthy(current_z.traits[ZTRAIT_DOWN])) // If there's nothing below, stop looking
                break;
            // Otherwise, down down down we go
            current_z = z_list[z_level - 1] as datum_space_level;
            plane_offset += 1;
        }

        /// Updates the lowest offset value
        foreach (datum_space_level level_to_update in Dm.each<datum_space_level>(levels_checked))
        {
            z_level_to_lowest_plane_offset[level_to_update.z_value] = 0f; // tg: = plane_offset (DV367)
            z_level_to_stack[level_to_update.z_value] = z_stack;
        }
        // tg: :812-821, max_plane_offset = max(max_plane_offset, plane_offset), generate_offset_lists and
        // COMSIG_PLANE_OFFSET_INCREASE: max_plane_offset stays 0 (DV367), so tg's early return (:813) is always taken
    }

    // tg: code/controllers/subsystem/mapping.dm:824
    public virtual void generate_offset_lists(object gen_from = null, object new_offset = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/mapping.dm:824", "generate_offset_lists: M3 P2 (skeleton)");
    }

    // tg: code/controllers/subsystem/mapping.dm:834
    public virtual void create_plane_offsets(object gen_from = null, object new_offset = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/mapping.dm:834", "create_plane_offsets: M3 P2 (skeleton)");
    }

    // tg: code/controllers/subsystem/mapping.dm:864
    /// Takes a turf or a z level, and returns a list of all the z levels that are connected to it
    public virtual DmList get_connected_levels(object connected = null)
    {
        object z_level = connected;
        if (isturf(z_level))
            z_level = ((turf)connected).z;
        // GAP(lead): a level that add_new_zlevel did not make (the toy and test worlds grow world.maxz by hand) is its own stack;
        // tg's levels all come through add_new_zlevel (:757), so a map world never takes this branch
        if (!(z_level is float z) || z < 1 || z > z_level_to_stack.len)
            return z_level == null ? Dm.list() : Dm.list(z_level);
        return z_level_to_stack[z_level] as DmList;
    }

    // tg: code/controllers/subsystem/mapping.dm:870
    public virtual async DmTask<object> lazy_load_template(object template_key = null, float? force = null)
    {
        force ??= FALSE;
        // UNTIL(initialized) (__DEFINES/_helpers.dm), expanded inline
        while (!Dm.truthy(initialized))
            await stoplag();
        // var/static/lazy_loading = FALSE; UNTIL(!lazy_loading)
        while (TgState.Current.datum_controller_subsystem_mapping__lazy_load_template__lazy_loading)
            await stoplag();

        TgState.Current.datum_controller_subsystem_mapping__lazy_load_template__lazy_loading = true;
        object dot;
        try
        {
            dot = await _lazy_load_template(template_key, force);
        }
        finally
        {
            TgState.Current.datum_controller_subsystem_mapping__lazy_load_template__lazy_loading = false; // a CRASH in the load must not lock it (tg's runtime ends the proc, its static stays set)
        }
        return dot;
    }

    // tg: code/controllers/subsystem/mapping.dm:882
    [DmPrivate]
    public virtual async DmTask<object> _lazy_load_template(object template_key = null, float? force = null)
    {
        force ??= FALSE;
        if (Dm.truthy(LAZYACCESS(loaded_lazy_templates, template_key)) && !Dm.truthy(force))
        {
            var template = GLOB.lazy_templates[template_key] as datum_lazy_template;
            return template.reservations[1];
        }
        loaded_lazy_templates ??= Dm.list(); // LAZYSET(loaded_lazy_templates, template_key, TRUE)
        loaded_lazy_templates[template_key] = TRUE;

        var target = GLOB.lazy_templates[template_key] as datum_lazy_template;
        if (target == null)
            throw Dm.CRASH(Dm.text("Attempted to lazy load a template key that does not exist: '[]'", template_key), "code/controllers/subsystem/mapping.dm", 892);
        return await target.lazy_load();
    }

    // tg: code/controllers/subsystem/mapping.dm:896
    public virtual object is_planetary()
    {
        // a world without a map selection has no map config (the host-built test and toy rooms, PreInit above): it is not planetary
        return current_map?.planetary ?? FALSE;
    }

    // tg: code/controllers/subsystem/mapping.dm:901
    public virtual async DmTask load_all_away_missions(object map_directory = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/mapping.dm:901", "load_all_away_missions: M3 P2 (skeleton)");
    }

    // tg: code/controllers/subsystem/mapping.dm:958
    public virtual void validate_z_level_loading(DmList checkable_levels = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/mapping.dm:958", "validate_z_level_loading: M3 P2 (skeleton)");
    }
}

// tg: code/controllers/subsystem/mapping.dm:974
public partial class datum_map_config : datum
{
    // tg: code/controllers/subsystem/mapping.dm:974
    public virtual object return_map_name(object webmap_included = null)
    {
        object text;
        if (Dm.truthy(feedback_link))
            text = Dm.text("<a href='byond://?action=openLink&link=[]'>[]</a>", url_encode(feedback_link), map_name);
        else
            text = map_name;
        if (Dm.truthy(webmap_included) && SSmapping.current_map.mapping_url != null)
            text = Dm.text("[] | <a href='byond://?action=openWebMap'>(Show Map)</a>", text);
        return text;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/mapping.dm:874  var/static/lazy_loading = FALSE
    internal bool datum_controller_subsystem_mapping__lazy_load_template__lazy_loading;
}
