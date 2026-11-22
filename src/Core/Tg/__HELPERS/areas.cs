// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/areas.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only require_area_resort and get_sorted_areas translated (package B, wave 4: /area/proc/setup and
// process_teleport_locs call them, areas.dm:631, :162); the other area helpers (blueprint area editing, area lookups)
// come with the map (M3). M5 slice 4: set_turfs_to_area and set_turf_to_area (a lattice built in deep space moves its turf to
// the nearstation area, lattice.dm:31, and back when it goes, lattice.dm:51). The ghosts (2026-10-16): get_area_turfs (a ghost
// made without a body turf, or on a secret level, goes to the arrivals shuttle, observer.dm:127). M7 slice 3 (the APC):
// get_area_instance_from_text (a mapper's areastring, apc_main.dm:187) and set_turf_to_area's assign_to_area. The M7 supermatter
// slice: mobs_in_area_type (the engineers who turn a cascade around, supermatter.dm:627). Engineering E14 (the station blueprints):
// detect_room, create_area, rename_area and set_area_machinery_title. create_atmos_zone and the other area lookups come with their users.

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/areas.dm:1  #define BP_MAX_ROOM_SIZE 300 (file-local, #undef'd at :209, rules 11.4)
    private const float BP_MAX_ROOM_SIZE = 300;

    // tg: code/__HELPERS/areas.dm:2-3  (defined outside code/__DEFINES and never #undef'd: __HELPERS/shuttle.dm's checks return them too)
    internal const float EXTRA_ROOM_CHECK_SKIP = 1;
    internal const float EXTRA_ROOM_CHECK_FAIL = 2;

    // tg: code/__HELPERS/areas.dm:41, :110, :115, :339  (proc-level var/static: rules 10.6, TgState fields)
    private static ref DmList detect_room__cardinal_cache => ref TgState.Current.detect_room__cardinal_cache;
    private static ref DmList create_area__area_or_turf_fail_types => ref TgState.Current.create_area__area_or_turf_fail_types;
    private static ref DmList create_area__blacklisted_areas => ref TgState.Current.create_area__blacklisted_areas;
    private static ref DmList set_area_machinery_title__to_rename => ref TgState.Current.set_area_machinery_title__to_rename;

    // Gets an atmos isolated contained space
    // Returns an associative list of turf|dirs pairs
    // The dirs are connected turfs in the same space
    // break_if_found is a typecache of turf/area types to return false if found
    // extra_check is an optional callback to invoke on each turf checked, and can specify whether to skip processing the turf or return false
    // Please keep this proc type agnostic. If you need to restrict it do it elsewhere or add an arg.
    // tg: code/__HELPERS/areas.dm:11
    public static async DmTask<object> detect_room(turf origin = null, DmList break_if_found = null, float? max_size = null, datum_callback extra_check = null)
    {
        break_if_found ??= Dm.list();
        max_size ??= INFINITY;
        if (Dm.truthy(origin.blocks_air))
            return Dm.list(origin);

        DmList dot = Dm.list();
        DmList checked_turfs = Dm.list();
        DmList found_turfs = Dm.list(origin);
        while (Dm.truthy(Dm.length(found_turfs)))
        {
            turf sourceT = found_turfs[1] as turf;
            found_turfs.Cut(1, 2);
            object dir_flags = checked_turfs[sourceT];
            foreach (object dir in Dm.each(GLOB.alldirs))
            {
                if (Dm.length(dot) > max_size)
                    return dot;
                if (Dm.truthy(Dm.bitand(dir_flags, dir))) // This means we've checked this dir before, probably from the other turf
                    continue;
                turf checkT = get_step(sourceT, Dm.num(dir)) as turf;
                if (!Dm.truthy(checkT))
                    continue;
                checked_turfs[sourceT] = Dm.bitor(checked_turfs[sourceT], dir);
                checked_turfs[checkT] = Dm.bitor(checked_turfs[checkT], REVERSE_DIR(Dm.num(dir)));
                object sw__1 = extra_check != null ? await extra_check.Invoke(checkT) : null;
                if (Dm.eq(sw__1, EXTRA_ROOM_CHECK_SKIP))
                    continue;
                if (Dm.eq(sw__1, EXTRA_ROOM_CHECK_FAIL))
                    return FALSE;
                dot[sourceT] = Dm.bitor(dot[sourceT], dir);
                dot[checkT] = Dm.bitor(dot[checkT], REVERSE_DIR(Dm.num(dir)));
                if (Dm.truthy(break_if_found[checkT.type]) || Dm.truthy(break_if_found[checkT.loc.type]))
                    return FALSE;
                detect_room__cardinal_cache ??= Dm.list(Dm.kv(Dm.text("[]", NORTH), TRUE), Dm.kv(Dm.text("[]", EAST), TRUE), Dm.kv(Dm.text("[]", SOUTH), TRUE), Dm.kv(Dm.text("[]", WEST), TRUE));
                if (!Dm.truthy(detect_room__cardinal_cache[Dm.text("[]", dir)]) || !TURFS_CAN_SHARE(sourceT, checkT))
                    continue;
                Dm.append(ref found_turfs, checkT); // Since checkT is connected, add it to the list to be processed
            }
        }
        return dot;
    }

    // tg: code/__HELPERS/areas.dm:83
    public static void set_turfs_to_area(DmList turfs = null, area new_area = null, DmList affected_areas = null)
    {
        affected_areas ??= Dm.list();
        foreach (turf the_turf in Dm.each<turf>(turfs))
            set_turf_to_area(the_turf, new_area, affected_areas);
    }

    // tg: code/__HELPERS/areas.dm:87
    public static void set_turf_to_area(turf the_turf = null, area new_area = null, DmList affected_areas = null)
    {
        affected_areas ??= Dm.list();
        area old_area = the_turf.loc as area;

        //keep rack of all areas affected by turf changes
        affected_areas[old_area.name] = old_area;

        //move the turf to its new area and unregister it from the old one
        the_turf.change_area(old_area, new_area);

        //inform atoms on the turf that their area has changed
        foreach (atom stuff in Dm.each<atom>(the_turf))
        {
            //unregister the stuff from its old area
            if (Dm.truthy(stuff._listen_lookup?[COMSIG_EXIT_AREA]))
                stuff._SendSignal(COMSIG_EXIT_AREA, Dm.list(stuff, old_area));

            //register the stuff to its new area. special exception for apc as its not registered to this signal
            if (istype(stuff, typeof(obj_machinery_power_apc)))
            {
                obj_machinery_power_apc area_apc = stuff as obj_machinery_power_apc;
                area_apc.assign_to_area();
            }
            else if (Dm.truthy(stuff._listen_lookup?[COMSIG_ENTER_AREA]))
                stuff._SendSignal(COMSIG_ENTER_AREA, Dm.list(stuff, new_area));
        }
    }

    // tg: code/__HELPERS/areas.dm:108
    public static async DmTask<object> create_area(mob creator = null, Type new_area_type = null)
    {
        new_area_type ??= typeof(area);
        // Passed into the above proc as list/break_if_found
        create_area__area_or_turf_fail_types ??= typecacheof(Dm.list(
            typeof(turf_open_space),
            typeof(area_shuttle))) as DmList;
        // Ignore these areas and dont let people expand them. They can expand into them though
        create_area__blacklisted_areas ??= typecacheof(Dm.list(
            typeof(area_space),
            typeof(area_station_asteroid))) as DmList;

        string error = "";
        DmList turfs = await detect_room(get_turf(creator) as turf, create_area__area_or_turf_fail_types, BP_MAX_ROOM_SIZE * 2) as DmList;
        float turf_count = Dm.length(turfs);
        if (!Dm.truthy(turf_count))
            error = I18n.T("The new area must be completely airtight and not a part of a shuttle.");
        else if (turf_count > BP_MAX_ROOM_SIZE)
            error = I18n.T("The room you're in is too big. It is []% larger than allowed.", turf_count >= BP_MAX_ROOM_SIZE * 2 ? I18n.T("more than 100") : (object)(((turf_count / BP_MAX_ROOM_SIZE) - 1) * 100));
        if (Dm.truthy(error))
        {
            to_chat(creator, span_warning(error));
            return null;
        }

        DmList apc_map = Dm.list();
        DmList areas = Dm.list(Dm.kv("New Area", new_area_type));
        for (float i = 1; i <= turf_count; i++)
        {
            turf the_turf = turfs[i] as turf;
            area place = get_area(the_turf) as area;
            if (Dm.truthy(create_area__blacklisted_areas[place.type]))
                continue;
            if (!Dm.truthy(place.requires_power) || Dm.truthy(Dm.bitand(place.area_flags, NOTELEPORT)) || Dm.truthy(Dm.bitand(place.area_flags, HIDDEN_AREA)))
                continue; // No expanding powerless rooms etc
            if (!Dm.truthy(TURF_SHARES(the_turf))) // No expanding areas of walls/something blocking this turf because that defeats the whole point of them used to separate areas
                continue;
            if (place.apc != null)
                apc_map[place.name] = place.apc;
            if (Dm.length(apc_map) > 1) // When merging 2 or more areas make sure we arent merging their apc into 1 area
            {
                to_chat(creator, span_warning(I18n.T("Multiple APC's detected in the vicinity. only 1 is allowed.")));
                return null;
            }
            areas[place.name] = place;
        }

        object area_choice = await tgui_input_list(creator, I18n.T("Choose an area to expand or make a new area"), I18n.T("Area Expansion"), areas);
        if (area_choice == null)
        {
            to_chat(creator, span_warning(I18n.T("No choice selected. The area remains undefined.")));
            return null;
        }
        area_choice = areas[area_choice];

        area newA;
        area oldA = get_area(get_turf(creator)) as area;
        if (!isarea(area_choice))
        {
            object str = await tgui_input_text(creator, I18n.T("New area name"), I18n.T("Blueprint Editing"), max_length: MAX_NAME_LEN);
            if (!Dm.truthy(str))
                return null;
            newA = Dm.@new((Type)area_choice) as area;
            Waiting.NoSleep(newA.AddComponent(typeof(datum_component_custom_area)));
            newA.setup(str);
            newA.default_gravity = oldA.default_gravity;
            GLOB.custom_areas[newA] = TRUE;
            require_area_resort(); //new area registered. resort the names
        }
        else
            newA = area_choice as area;

        //we haven't done anything. let's get outta here
        if (newA == oldA)
        {
            to_chat(creator, span_warning(I18n.T("Selected choice is same as the area your standing in. No area changes were requested.")));
            return null;
        }

        /**
         * A list of all machinery tied to an area along with the area itself. key=area name,value=list(area,list of machinery)
         * we use this to keep track of what areas are affected by the blueprints & what machinery of these areas needs to be reconfigured accordingly
         */
        DmList affected_areas = Dm.list();
        set_turfs_to_area(turfs, newA, affected_areas);

        newA.reg_in_areas_in_z();

        if (!isarea(area_choice) && Dm.truthy(newA.static_lighting))
            newA.create_area_lighting_objects();

        //convert map to list
        DmList area_list = Dm.list();
        foreach (object area_name in Dm.each(affected_areas))
            Dm.append(ref area_list, affected_areas[area_name]);
        // SEND_GLOBAL_SIGNAL(COMSIG_AREA_CREATED, newA, area_list, creator) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_AREA_CREATED]))
            SSdcs._SendSignal(COMSIG_AREA_CREATED, Dm.list(SSdcs, newA, area_list, creator));
        to_chat(creator, span_notice(I18n.T("You have created a new area, named []. It is now weather proof, and constructing an APC will allow it to be powered.", newA.name)));
        creator.log_message(Dm.text("created a new area: [] (previously \"[]\")", AREACOORD(creator), oldA.name), LOG_GAME);

        //purge old areas that had all their turfs merged into the new one i.e. old empty areas. also recompute fire doors
        for (float i = 1; i <= Dm.length(area_list); i++)
        {
            area merged_area = area_list[i] as area;

            //recompute fire doors affecting areas
            foreach (obj_machinery_door_firedoor FD in Dm.each_cast<obj_machinery_door_firedoor>(merged_area.firedoors))
                FD.CalculateAffectingAreas();

            //no more turfs in this area. Time to clean up
            if (!Dm.truthy(merged_area.has_contained_turfs()))
                qdel(merged_area);
        }

        return TRUE;
    }

    // tg: code/__HELPERS/areas.dm:211
    public static void require_area_resort()
    {
        GLOB.sortedAreas = null;
    }

    /// Returns a sorted version of GLOB.areas, by name
    // tg: code/__HELPERS/areas.dm:215
    public static object get_sorted_areas()
    {
        if (!Dm.truthy(GLOB.sortedAreas))
            GLOB.sortedAreas = sortTim(GLOB.areas.Copy(), "/proc/" + nameof(GlobalProcs.cmp_name_asc)) as DmList;
        return GLOB.sortedAreas;
    }

    // tg: code/__HELPERS/areas.dm:222
    public static object get_area_instance_from_text(object areatext = null)
    {
        if (istext(areatext))
            areatext = text2path((string)areatext);
        return GLOB.areas_by_type[areatext];
    }

    //Takes: Area type as text string or as typepath OR an instance of the area.
    //Returns: A list of all areas of that type in the world.
    // tg: code/__HELPERS/areas.dm:229  (gaps P4d: send_supply_pod_to_area's areas)
    public static DmList get_areas(object areatype = null, float? subtypes = null)
    {
        subtypes ??= TRUE;
        if (istext(areatype))
            areatype = text2path((string)areatype);
        else if (isarea(areatype))
        {
            area areatemp = areatype as area;
            areatype = areatemp.type;
        }
        else if (!ispath(areatype))
            return null;

        DmList areas = Dm.list();
        if (Dm.truthy(subtypes))
        {
            DmList cache = typecacheof(areatype) as DmList;
            foreach (area area_to_check in Dm.each_cast<area>(GLOB.areas))
            {
                if (Dm.truthy(cache[area_to_check.type]))
                    Dm.append(ref areas, area_to_check);
            }
        }
        else
        {
            foreach (area area_to_check in Dm.each_cast<area>(GLOB.areas))
            {
                if (Dm.eq(area_to_check.type, areatype))
                    Dm.append(ref areas, area_to_check);
            }
        }
        return areas;
    }

    //Takes: Area type as text string or as typepath OR an instance of the area.
    //Returns: A list of all turfs in areas of that type of that type in the world.
    // tg: code/__HELPERS/areas.dm:260
    public static object get_area_turfs(object areatype = null, object target_z = null, float? subtypes = null)
    {
        target_z ??= 0;
        subtypes ??= FALSE;
        if (istext(areatype))
        {
            areatype = text2path((string)areatype);
        }
        else if (isarea(areatype))
        {
            area areatemp = areatype as area;
            areatype = areatemp.type;
        }
        else if (!ispath(areatype))
        {
            return null;
        }
        // Pull out the areas
        DmList areas_to_pull = Dm.list();
        if (Dm.truthy(subtypes))
        {
            DmList cache = typecacheof(areatype) as DmList;
            foreach (area area_to_check in Dm.each_cast<area>(GLOB.areas))
            {
                if (!Dm.truthy(cache[area_to_check.type]))
                    continue;
                Dm.append(ref areas_to_pull, area_to_check);
            }
        }
        else
        {
            foreach (area area_to_check in Dm.each_cast<area>(GLOB.areas))
            {
                if (!Dm.eq(area_to_check.type, areatype))
                    continue;
                Dm.append(ref areas_to_pull, area_to_check);
            }
        }

        // Now their turfs
        DmList turfs = Dm.list();
        foreach (area pull_from in Dm.each_cast<area>(areas_to_pull))
        {
            if (Dm.eq(target_z, 0))
            {
                foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(pull_from.get_zlevel_turf_lists()))
                    Dm.append(ref turfs, zlevel_turfs);
            }
            else
            {
                Dm.append(ref turfs, pull_from.get_turfs_by_zlevel(target_z));
            }
        }
        return turfs;
    }

    // tg: code/__HELPERS/areas.dm:295
    public static object mobs_in_area_type(DmList checked_areas = null)
    {
        DmList mobs_in_area = Dm.list();
        foreach (mob_living mob in Dm.each_cast<mob_living>(GLOB.mob_living_list))
        {
            if (QDELETED(mob))
                continue;
            foreach (object area in Dm.each(checked_areas))
            {
                if (istype(get_area(mob), area as Type))
                {
                    Dm.append(mobs_in_area, mob);
                    break;
                }
            }
        }
        return mobs_in_area;
    }

    /**
     * rename_area
     * Renames an area to the given new name, updating all machines' names and firedoors
     * to properly ensure alarms and machines are named correctly at all times.
     * Args:
     * - area_to_rename: The area that's being renamed.
     * - new_name: The name we're changing said area to.
     */
    // tg: code/__HELPERS/areas.dm:314
    public static object rename_area(area area_to_rename = null, object new_name = null)
    {
        string prevname = Dm.text("[]", area_to_rename.name);
        set_area_machinery_title(area_to_rename, new_name, prevname);
        area_to_rename.name = Dm.str(new_name);
        require_area_resort(); //area renamed so resort the names

        if (Dm.truthy(LAZYLEN(area_to_rename.firedoors)))
        {
            foreach (obj_machinery_door_firedoor area_firedoors in Dm.each_cast<obj_machinery_door_firedoor>(area_to_rename.firedoors))
                area_firedoors.CalculateAffectingAreas();
        }
        area_to_rename.update_areasize();
        return TRUE;
    }

    /**
     * Renames all machines in a defined area from the old title to the new title.
     * Used when renaming an area to ensure that all machiens are labeled the new area's machine.
     * Args:
     * - area_renaming: The area being renamed, which we'll check turfs from to rename machines in.
     * - title: The new name of the area that we're swapping into.
     * - oldtitle: The old name of the area that we're replacing text from.
     */
    // tg: code/__HELPERS/areas.dm:334
    public static void set_area_machinery_title(area area_renaming = null, object title = null, object oldtitle = null)
    {
        if (!Dm.truthy(oldtitle)) // or replacetext goes to infinite loop
            return;

        //stuff tied to the area to rename
        set_area_machinery_title__to_rename ??= typecacheof(Dm.list(
            typeof(obj_machinery_airalarm),
            typeof(obj_machinery_atmospherics_components_unary_vent_scrubber),
            typeof(obj_machinery_atmospherics_components_unary_vent_pump),
            typeof(obj_machinery_door),
            typeof(obj_machinery_firealarm),
            typeof(obj_machinery_light_switch),
            typeof(obj_machinery_power_apc),
            typeof(obj_machinery_camera))) as DmList;
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(area_renaming.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each_cast<turf>(zlevel_turfs))
            {
                foreach (obj machine in Dm.each_cast<obj>(typecache_filter_list(area_turf.contents, set_area_machinery_title__to_rename)))
                    machine.name = replacetext(machine.name, oldtitle, title);
            }
        }
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/areas.dm:41  detect_room's var/static/list/cardinal_cache
    internal DmList detect_room__cardinal_cache;
    // tg: code/__HELPERS/areas.dm:110  create_area's var/static/list/area_or_turf_fail_types
    internal DmList create_area__area_or_turf_fail_types;
    // tg: code/__HELPERS/areas.dm:115  create_area's var/static/list/blacklisted_areas
    internal DmList create_area__blacklisted_areas;
    // tg: code/__HELPERS/areas.dm:339  set_area_machinery_title's var/static/list/to_rename
    internal DmList set_area_machinery_title__to_rename;
}
