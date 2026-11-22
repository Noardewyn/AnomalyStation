// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/turfs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only valid_build_direction and get_teleport_loc (the syndicate campaign G2) translated (M3 P4: the window's CanAllowThrough, game/objects/structures/window.dm:146)
// and get_turf_pixel with its two callees get_visual_offset and pixel_offset_turf (M4a P1: /atom/IsObscured's click turf,
// with tg's get_turf_pixel unit test), get_ranged_target_turf (M4b slice 3: a slip's slide) and get_ranged_target_turf_direct
// (M5x slice 1: an explosion's throw), /turf/proc/is_sunlight_blocked (M7 slice G1: the solar panels' occlusion) and get_blueprint_data
// (engineering E14: the station blueprints), get_offset_target_turf (D325: disposals), get_random_station_turf (syndicate G4c: a supply pod with nowhere safe to land); the other helpers of turfs.dm come with their users.

public static partial class GlobalProcs
{
    ///Returns a random turf on the station
    // tg: code/__HELPERS/turfs.dm:426
    public static object get_random_station_turf()
    {
        DmList turfs = get_area_turfs(pick(GLOB.the_station_areas)) as DmList;
        if (Dm.truthy(length(turfs)))
            return pick(turfs);
        return null;
    }

    /// Returns a random safe station turf, picking a department first so each department weighs the same
    // tg: code/__HELPERS/turfs.dm:454  (gaps P4d: the black market's teleport shipping)
    public static turf get_safe_random_station_turf_equal_weight()
    {
        // Big list of departments, each with lists of each area subtype.
        DmList department_areas = TgState.Current.GlobalProcs__get_safe_random_station_turf_equal_weight__department_areas ??= Dm.list(
            subtypesof(typeof(area_station_engineering)),
            subtypesof(typeof(area_station_medical)),
            subtypesof(typeof(area_station_science)),
            subtypesof(typeof(area_station_security)),
            subtypesof(typeof(area_station_service)),
            subtypesof(typeof(area_station_command)),
            subtypesof(typeof(area_station_hallway)),
            subtypesof(typeof(area_station_ai)),
            subtypesof(typeof(area_station_cargo)));

        var final_department = pick(department_areas) as DmList; // Pick a department
        DmList final_area_list = Dm.list();

        foreach (object checked_area in Dm.each(final_department)) // Check each area to make sure it exists on the station
        {
            if (Dm.@in(checked_area, GLOB.the_station_areas))
                Dm.append(ref final_area_list, checked_area);
        }

        if (!Dm.truthy(final_area_list.len)) // Failsafe
            return get_safe_random_station_turf();

        return get_safe_random_station_turf(final_area_list);
    }

    ///Returns a random turf on the station, excludes dense turfs (like walls) and areas that have valid_territory set to FALSE
    // tg: code/__HELPERS/turfs.dm:432  (syndicate N1a, D316: the nuclear disk's relocation)
    public static turf get_safe_random_station_turf(DmList areas_to_pick_from = null)
    {
        areas_to_pick_from ??= GLOB.the_station_areas;
        for (int i = 1; i <= 5; i++)
        {
            DmList turf_list = get_area_turfs(pick(areas_to_pick_from)) as DmList;
            turf target = null;
            while (turf_list.len != 0 && target == null)
            {
                int I = (int)rand(1, turf_list.len);
                var checked_turf = turf_list[I] as turf;
                var turf_area = (area)get_area(checked_turf);
                if (!Dm.truthy(checked_turf.density) && Dm.truthy(Dm.bitand(turf_area.area_flags, VALID_TERRITORY)) && !isgroundlessturf(checked_turf))
                {
                    bool clear = true;
                    foreach (obj checked_object in Dm.each<obj>(checked_turf.contents))
                    {
                        if (Dm.truthy(checked_object.density))
                        {
                            clear = false;
                            break;
                        }
                    }
                    if (clear)
                        target = checked_turf;
                }
                if (target == null)
                    turf_list.Cut(I, I + 1);
            }
            if (target != null)
                return target;
        }
        return null;
    }

    // tg: code/__HELPERS/turfs.dm  (M10 slice G2: a shuttle template's modular map roots)
    public static object is_type_on_turf(turf location = null, Type type_to_find = null)
    {
        if (location == null)
            return null;
        object found_type = Dm.locate_type(type_to_find, location);
        return found_type;
    }

    ///Returns location. Returns null if no location was found.
    // tg: code/__HELPERS/turfs.dm:2  (the syndicate campaign G2: the syndicate teleporter)
    public static object get_teleport_loc(turf location = null, mob target = null, float? distance = null, float? density_check = null, float? closed_turf_check = null, float? errorx = null, float? errory = null, float? eoffsetx = null, float? eoffsety = null)
    {
        distance ??= 1;
        density_check ??= FALSE;
        closed_turf_check ??= FALSE;
        errorx ??= 0;
        errory ??= 0;
        eoffsetx ??= 0;
        eoffsety ??= 0;
        float dirx = 0;//Generic location finding variable.
        float diry = 0;

        float xoffset = 0;//Generic counter for offset location.
        float yoffset = 0;

        float b1xerror = 0;//Generic placing for point A in box. The lower left.
        float b1yerror = 0;
        float b2xerror = 0;//Generic placing for point B in box. The upper right.
        float b2yerror = 0;

        errorx = abs(errorx.Value);//Error should never be negative.
        errory = abs(errory.Value);

        float sw__1 = Dm.num(target.dir);//This can be done through equations but switch is the simpler method. And works fast to boot.
        //Directs on what values need modifying.
        if (sw__1 == 1)//North
        {
            diry += distance.Value;
            yoffset += eoffsety.Value;
            xoffset += eoffsetx.Value;
            b1xerror -= errorx.Value;
            b1yerror -= errory.Value;
            b2xerror += errorx.Value;
            b2yerror += errory.Value;
        }
        else if (sw__1 == 2)//South
        {
            diry -= distance.Value;
            yoffset -= eoffsety.Value;
            xoffset += eoffsetx.Value;
            b1xerror -= errorx.Value;
            b1yerror -= errory.Value;
            b2xerror += errorx.Value;
            b2yerror += errory.Value;
        }
        else if (sw__1 == 4)//East
        {
            dirx += distance.Value;
            yoffset += eoffsetx.Value;//Flipped.
            xoffset += eoffsety.Value;
            b1xerror -= errory.Value;//Flipped.
            b1yerror -= errorx.Value;
            b2xerror += errory.Value;
            b2yerror += errorx.Value;
        }
        else if (sw__1 == 8)//West
        {
            dirx -= distance.Value;
            yoffset -= eoffsetx.Value;//Flipped.
            xoffset += eoffsety.Value;
            b1xerror -= errory.Value;//Flipped.
            b1yerror -= errorx.Value;
            b2xerror += errory.Value;
            b2yerror += errorx.Value;
        }

        turf destination = locate(location.x + dirx, location.y + diry, location.z);

        if (destination == null)//If there isn't a destination.
            return null;

        if (!Dm.truthy(errorx) && !Dm.truthy(errory))//If errorx or y were not specified.
        {
            if (Dm.truthy(density_check) && Dm.truthy(destination.density))
                return null;
            if (Dm.truthy(closed_turf_check) && isclosedturf(destination))
                return null;//If closed was specified.
            if (destination.x > world.maxx || destination.x < 1)
                return null;
            if (destination.y > world.maxy || destination.y < 1)
                return null;
        }

        DmList destination_list = Dm.list();//To add turfs to list.
        /*This will draw a block around the target turf, given what the error is.
        Specifying the values above will basically draw a different sort of block.
        If the values are the same, it will be a square. If they are different, it will be a rectangle.
        In either case, it will center based on offset. Offset is position from center.
        Offset always calculates in relation to direction faced. In other words, depending on the direction of the teleport,
        the offset should remain positioned in relation to destination.*/

        turf center = locate(destination.x + xoffset, destination.y + yoffset, location.z);//So now, find the new center.

        //Now to find a box from center location and make that our destination.
        float width = (b2xerror - b1xerror) + 1;
        float height = (b2yerror - b1yerror) + 1;
        foreach (turf current_turf in Dm.each_cast<turf>(CORNER_BLOCK_OFFSET(center, width, height, b1xerror, b1yerror)))
        {
            if (Dm.truthy(density_check) && Dm.truthy(current_turf.density))
                continue;//If density was specified.
            if (Dm.truthy(closed_turf_check) && isclosedturf(current_turf))
                continue;//If closed was specified.
            if (current_turf.x > world.maxx || current_turf.x < 1)
                continue;//Don't want them to teleport off the map.
            if (current_turf.y > world.maxy || current_turf.y < 1)
                continue;
            destination_list.Add(current_turf);
        }

        if (!Dm.truthy(destination_list.Count))
            return null;

        destination = pick(destination_list) as turf;
        return destination;
    }

    // tg: code/__HELPERS/turfs.dm:494
    public static object valid_build_direction(turf dest_turf = null, object test_dir = null, float? is_fulltile = null)
    {
        is_fulltile ??= FALSE;
        if (!Dm.truthy(dest_turf))
            return FALSE;
        foreach (obj turf_content in Dm.each<obj>(dest_turf))
        {
            if (Dm.truthy(Dm.bitand(turf_content.obj_flags, BLOCKS_CONSTRUCTION_DIR)))
            {
                if (Dm.truthy(is_fulltile))  // for making it so fulltile things can't be built over directional things--a special case
                    return FALSE;
                if (Dm.eq(turf_content.dir, test_dir))
                    return FALSE;
            }
        }
        return TRUE;
    }
}

// tg: code/__HELPERS/turfs.dm:214  (the global procs, rules 7.8)
public static partial class GlobalProcs
{
    /**
     * Returns the turf located at the map edge in the specified direction relative to target_atom used for mass driver
     * it takes into account:
     * Pixel_x/y
     * Matrix x/y
     * NOTE: if your atom has non-standard bounds then this proc
     * will handle it, but:
     * if the bounds are even, then there are an even amount of "middle" turfs, the one to the EAST, NORTH, or BOTH is picked
     * this may seem bad, but you're at least as close to the center of the atom as possible, better than byond's default loc being all the way off)
     * if the bounds are odd, the true middle turf of the atom is returned
    **/
    // tg: code/__HELPERS/turfs.dm:214
    public static object get_turf_pixel(atom checked_atom = null)
    {
        turf atom_turf = get_turf(checked_atom); //use checked_atom's turfs, as its coords are the same as checked_atom's AND checked_atom's coords are lost if it is inside another atom
        if (!Dm.truthy(atom_turf))
            return null;
        if (Dm.truthy(Dm.bitand(checked_atom.flags_1, IGNORE_TURF_PIXEL_OFFSET_1)))
            return atom_turf;

        DmList offsets = get_visual_offset(checked_atom) as DmList;
        return pixel_offset_turf(atom_turf, offsets);
    }

    /**
     * Returns how visually "off" the atom is from its source turf as a list of x, y (in pixel steps)
     * it takes into account:
     * Pixel_x/y
     * Matrix x/y
     * Icon width/height
    **/
    // tg: code/__HELPERS/turfs.dm:231
    public static object get_visual_offset(atom checked_atom = null)
    {
        //Find checked_atom's matrix so we can use its X/Y pixel shifts
        matrix atom_matrix = matrix(checked_atom.transform);

        float pixel_x_offset = checked_atom.pixel_x + checked_atom.pixel_w + Dm.num(atom_matrix.get_x_shift());
        float pixel_y_offset = checked_atom.pixel_y + checked_atom.pixel_z + Dm.num(atom_matrix.get_y_shift());

        //Irregular objects
        // tg: :240  var/list/icon_dimensions = get_icon_dimensions_pure(checked_atom.icon)
        // GAP(lead): get_icon_dimensions_pure (__HELPERS/icons.dm:1313) reads the .dmi's width and height, which the core
        // does not load (the shim's /icon has no Width or Height); every icon reads as ICON_SIZE_X x ICON_SIZE_Y here, so the
        // oversized-icon term below adds nothing (DEVIATION(DV18), narrowed by M4a P1 to this term).
        DmList icon_dimensions = Dm.list(Dm.kv("width", ICON_SIZE_X), Dm.kv("height", ICON_SIZE_Y));
        object checked_atom_icon_height = icon_dimensions["height"];
        object checked_atom_icon_width = icon_dimensions["width"];
        if (!Dm.eq(checked_atom_icon_height, ICON_SIZE_Y) || !Dm.eq(checked_atom_icon_width, ICON_SIZE_X))
        {
            pixel_x_offset += ((Dm.num(checked_atom_icon_width) / ICON_SIZE_X) - 1) * (ICON_SIZE_X * 0.5f);
            pixel_y_offset += ((Dm.num(checked_atom_icon_height) / ICON_SIZE_Y) - 1) * (ICON_SIZE_Y * 0.5f);
        }

        return Dm.list(pixel_x_offset, pixel_y_offset);
    }

    /**
     * Takes a turf, and a list of x and y pixel offsets and returns the turf that the offset position best lands in
    **/
    // tg: code/__HELPERS/turfs.dm:251
    public static object pixel_offset_turf(turf offset_from = null, DmList offsets = null)
    {
        //DY and DX
        float rough_x = round(round(Dm.num(offsets[1]), ICON_SIZE_X) / ICON_SIZE_X);
        float rough_y = round(round(Dm.num(offsets[2]), ICON_SIZE_Y) / ICON_SIZE_Y);

        float final_x = clamp(offset_from.x + rough_x, 1, world.maxx);
        float final_y = clamp(offset_from.y + rough_y, 1, world.maxy);

        if (Dm.truthy(final_x) || Dm.truthy(final_y))
            return locate(final_x, final_y, offset_from.z);
        return offset_from;
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/turfs.dm:131  (M5 slice 7: the unwrenched pressurised pipe's thrown user)
    public static object get_edge_target_turf(atom target_atom = null, object direction = null)
    {
        turf target = locate(target_atom.x, target_atom.y, target_atom.z) as turf;
        if (target_atom == null || target == null)
            return 0;
        //since NORTHEAST == NORTH|EAST, etc, doing it this way allows for diagonal mass drivers in the future
        //and isn't really any more complicated

        float x = target_atom.x;
        float y = target_atom.y;
        if (Dm.truthy(Dm.bitand(direction, NORTH)))
            y = world.maxy;
        else if (Dm.truthy(Dm.bitand(direction, SOUTH))) //you should not have both NORTH and SOUTH in the provided direction
            y = 1;
        if (Dm.truthy(Dm.bitand(direction, EAST)))
            x = world.maxx;
        else if (Dm.truthy(Dm.bitand(direction, WEST)))
            x = 1;
        if (Dm.truthy(ISDIAGONALDIR(Dm.num(direction)))) //let's make sure it's accurately-placed for diagonals
        {
            float lowest_distance_to_map_edge = min(abs(x - target_atom.x), abs(y - target_atom.y));
            return get_ranged_target_turf(target_atom, direction, lowest_distance_to_map_edge);
        }
        return locate(x, y, target_atom.z);
    }

    // tg: code/__HELPERS/turfs.dm:198  (D325: disposals' pipe_eject and a broken pipe's spill)
    public static object get_offset_target_turf(atom target_atom = null, object dx = null, object dy = null)
    {
        float x = min(world.maxx, max(1, target_atom.x + Dm.num(dx)));
        float y = min(world.maxy, max(1, target_atom.y + Dm.num(dy)));
        return locate(x, y, target_atom.z);
    }

    // tg: code/__HELPERS/turfs.dm:157  (M4b slice 3)
    public static object get_ranged_target_turf(atom target_atom = null, object direction = null, object range = null)
    {
        float x = target_atom.x;
        float y = target_atom.y;
        if (Dm.truthy(Dm.bitand(direction, NORTH)))
            y = min(world.maxy, y + Dm.num(range));
        else if (Dm.truthy(Dm.bitand(direction, SOUTH)))
            y = max(1, y - Dm.num(range));
        if (Dm.truthy(Dm.bitand(direction, EAST)))
            x = min(world.maxx, x + Dm.num(range));
        else if (Dm.truthy(Dm.bitand(direction, WEST))) //if you have both EAST and WEST in the provided direction, then you're gonna have issues
            x = max(1, x - Dm.num(range));

        return locate(x, y, target_atom.z);
    }

    /**
     * Get ranged target turf, but with direct targets as opposed to directions
     *
     * Starts at atom starting_atom and gets the exact angle between starting_atom and target
     * Moves from starting_atom with that angle, Range amount of times, until it stops, bound to map size
     * Arguments:
     * * starting_atom - Initial Firer / Position
     * * target - Target to aim towards
     * * range - Distance of returned target turf from starting_atom
     * * offset - Angle offset, 180 input would make the returned target turf be in the opposite direction
     */
    // tg: code/__HELPERS/turfs.dm:184
    public static object get_ranged_target_turf_direct(atom starting_atom = null, atom target = null, object range = null, object offset = null)
    {
        float angle = ATAN2(target.x - starting_atom.x, target.y - starting_atom.y);
        if (Dm.truthy(offset))
            angle += Dm.num(offset);
        turf starting_turf = get_turf(starting_atom);
        for (float i = 1, i__end = Dm.num(range); i <= i__end; i++)
        {
            turf check = locate(starting_atom.x + cos(angle) * i, starting_atom.y + sin(angle) * i, starting_atom.z);
            if (!Dm.truthy(check))
                break;
            starting_turf = check;
        }

        return starting_turf;
    }
}

// tg: code/__HELPERS/turfs.dm:372 (M5x slice 5: the singularity's eating range)
public static partial class GlobalProcs
{
    /**
     * Returns a list of turfs in a spiral pattern around the center, closest first (the ring at 1 from its north-west corner
     * clockwise, then the ring at 2, ...). Used by the singularity to pick what it eats and pulls.
     */
    // tg: code/__HELPERS/turfs.dm:372
    public static object spiral_range_turfs(object dist = null, object center = null, float? orange = null, DmList outlist = null, object tick_checked = null)
    {
        dist ??= 0;
        center ??= World.Current.usr;
        orange ??= FALSE;
        outlist ??= Dm.list();
        outlist.Cut();
        if (!Dm.truthy(dist))
        {
            Dm.append(outlist, center);
            return outlist;
        }

        turf t_center = get_turf(center as atom);
        if (t_center == null)
            return outlist;

        DmList turf_list = outlist;
        turf checked_turf;
        float y;
        float x;
        float c_dist = 1;

        if (!Dm.truthy(orange))
            Dm.append(turf_list, t_center);

        while (c_dist <= Dm.num(dist))
        {
            y = t_center.y + c_dist;
            x = t_center.x - c_dist + 1;
            for (; x <= t_center.x + c_dist; x++)
            {
                checked_turf = locate(x, y, t_center.z);
                if (checked_turf != null)
                    Dm.append(turf_list, checked_turf);
            }

            y = t_center.y + c_dist - 1;
            x = t_center.x + c_dist;
            for (float yy = t_center.y - c_dist; yy <= y; yy++)
            {
                checked_turf = locate(x, yy, t_center.z);
                if (checked_turf != null)
                    Dm.append(turf_list, checked_turf);
            }

            y = t_center.y - c_dist;
            x = t_center.x + c_dist - 1;
            for (float xx = t_center.x - c_dist; xx <= x; xx++)
            {
                checked_turf = locate(xx, y, t_center.z);
                if (checked_turf != null)
                    Dm.append(turf_list, checked_turf);
            }

            y = t_center.y - c_dist + 1;
            x = t_center.x - c_dist;
            for (; y <= t_center.y + c_dist; y++)
            {
                checked_turf = locate(x, y, t_center.z);
                if (checked_turf != null)
                    Dm.append(turf_list, checked_turf);
            }
            c_dist++;
            if (Dm.truthy(tick_checked))
                CHECK_TICK(); // (TICK_CHECK stays false, DV3/DV33)
        }

        return turf_list;
    }
}

// tg: code/__HELPERS/turfs.dm:528 (engineering E14: the station blueprints' structural view)
public static partial class GlobalProcs
{
    /**
     * get_blueprint_data
     * Gets a list of turfs around a central turf and gets the blueprint data in a list
     * Args:
     * - central_turf: The center turf we're getting data from.
     * - viewsize: The viewsize we're getting the turfs around central_turf of.
     */
    // tg: code/__HELPERS/turfs.dm:528
    public static object get_blueprint_data(turf central_turf = null, object viewsize = null)
    {
        DmList blueprint_data_returned = Dm.list();
        DmList dimensions = getviewsize(viewsize);
        float horizontal_radius = Dm.num(dimensions[1]) / 2;
        float vertical_radius = Dm.num(dimensions[2]) / 2;
        foreach (turf nearby_turf in Dm.each_cast<turf>(RECT_TURFS(horizontal_radius, vertical_radius, central_turf)))
        {
            if (Dm.truthy(nearby_turf.blueprint_data))
                Dm.append(blueprint_data_returned, nearby_turf.blueprint_data);
        }
        return blueprint_data_returned;
    }
}

// M7 slice G1 (Meta's solars, D163): the sun's occlusion check, which the solar panels read.
public partial class turf
{
    /**
     * Check if the turf has access to the sun, by checking for opaque turfs in the direction of the sun.
     *
     * occlusion_distance - determines how far away we check to see if the sun is blocked
     *
     * Returns TRUE if the sun is blocked
     * Returns FALSE otherwise
     */
    // tg: code/__HELPERS/turfs.dm:563
    public virtual object is_sunlight_blocked(float? occlusion_distance = null)
    {
        occlusion_distance ??= 20;
        if (HAS_TRAIT(this, TRAIT_TURF_SUN_BLOCKED))
            return TRUE;

        float target_x = round(sin(SSsun.azimuth), 0.01f);
        float target_y = round(cos(SSsun.azimuth), 0.01f);
        float x_hit = x;
        float y_hit = y;
        turf hit;
        for (float run = 1; run <= occlusion_distance; run++)
        {
            x_hit += target_x;
            y_hit += target_y;
            hit = locate(round(x_hit, 1), round(y_hit, 1), z);
            if (hit.directional_opacity == ALL_CARDINALS) // IS_OPAQUE_TURF (__DEFINES/lighting.dm)
                return TRUE;
            if (hit.x == 1 || hit.x == world.maxx || hit.y == 1 || hit.y == world.maxy) // edge of the map
                break;
        }

        return FALSE;
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/turfs.dm:456  var/static/list/department_areas
    internal DmList GlobalProcs__get_safe_random_station_turf_equal_weight__department_areas;
}

