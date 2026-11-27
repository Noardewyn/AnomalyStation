// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/atoms.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only pass (logging and chat, wave 3: the logger's recursive_jsonify calls it), get_nested_locs (the movable
// Entered and Exited call it, package D, wave 4), get_all_contents (a living mob's DirectAccess, every click; M2
// integration), get_all_contents_type (get_contents_custom_materials, atom_materials.dm; M5 slice 2) and get_oversized_icon_offsets (the progress bar of a do_after; M2 integration) and get (the item attack chain's telekinesis
// check, item_attack.dm; M4a P1) and contains (tgui's ui states; M5 slice 3) and spiral_range (empulse's range; M7 EMP) and can_see (the gas analyzer's reach; M7 slice T3) and urange (a stage six singularity's fire; the M7 delamination slice) and generate_items_inside (the medkits; M8 slice C) and get_cardinal_dir (gaps P3b: the tendril hammer's knockback) and is_source_facing_target (gaps ST2: the basketball's steal) and random_step (the slot machine's coins) translated; the other atom
// helpers come with their first callers.

// tg: code/__HELPERS/atoms.dm:2
public partial class atom : datum
{
    ///Returns the src and all recursive contents as a list.
    // tg: code/__HELPERS/atoms.dm:2
    public virtual object get_all_contents(object ignore_flag_1 = null)
    {
        DmList dot = null;
        dot = Dm.list(this);
        float i = 0;
        while (i < length(dot))
        {
            atom checked_atom = dot[++i] as atom;
            if (Dm.truthy(Dm.bitand(checked_atom.flags_1, ignore_flag_1)))
                continue;
            Dm.append(ref dot, checked_atom.contents);
        }
        return dot;
    }

    ///Like get_all_contents but skips the contents of atoms whose type is in the typecache (M10 slice G: turf/empty)
    // tg: code/__HELPERS/atoms.dm:23
    public virtual object get_all_contents_ignoring(DmList ignore_typecache = null)
    {
        if (!Dm.truthy(length(ignore_typecache)))
            return get_all_contents();
        DmList processing = Dm.list(this);
        DmList dot = Dm.list();
        float i = 0;
        while (i < length(processing))
        {
            atom checked_atom = processing[++i] as atom;
            if (Dm.truthy(ignore_typecache[checked_atom.type]))
                continue;
            Dm.append(ref processing, checked_atom.contents);
            Dm.append(ref dot, checked_atom);
        }
        return dot;
    }

    ///Returns the src and all recursive contents, but skipping going any deeper if an atom has a specific trait.
    // tg: code/__HELPERS/atoms.dm:37  (gaps SG1: the scanner gate's weapon scan)
    public virtual DmList get_all_contents_skipping_traits(object skipped_trait = null)
    {
        DmList dot = Dm.list(this);
        if (!Dm.truthy(skipped_trait))
            throw Dm.CRASH("get_all_contents_skipping_traits called without a skipped_trait", "code/__HELPERS/atoms.dm", 40);
        float i = 0;
        while (i < length(dot))
        {
            atom checked_atom = dot[++i] as atom;
            if (HAS_TRAIT(checked_atom, skipped_trait))
                continue;
            Dm.append(ref dot, checked_atom.contents);
        }
        return dot;
    }

    ///identical to get_all_contents but returns a list of atoms of the type passed in the argument.
    // tg: code/__HELPERS/atoms.dm:12
    public virtual object get_all_contents_type(object type = null)
    {
        DmList dot = null;
        DmList processing_list = Dm.list(this);
        dot = Dm.list();
        while (Dm.truthy(length(processing_list)))
        {
            atom checked_atom = processing_list[1] as atom;
            processing_list.Cut(1, 2);
            Dm.append(ref processing_list, checked_atom.contents);
            if (istype(checked_atom, type as Type))
                Dm.append(ref dot, checked_atom);
        }
        return dot;
    }
}

public static partial class GlobalProcs
{
    ///Creates new items inside an atom based on a list
    // tg: code/__HELPERS/atoms.dm:306
    public static void generate_items_inside(DmList items_list = null, object where_to = null)
    {
        foreach (Type each_item in Dm.each_cast<Type>(items_list))
        {
            for (float i = 1, i__end = Dm.num(items_list[each_item]); i <= i__end; i++)
                Dm.@new(each_item, where_to);
        }
    }

    ///Returns a list of all locations (except the area) the movable is within.
    // tg: code/__HELPERS/atoms.dm:49
    public static object get_nested_locs(atom_movable atom_on_location = null, float? include_turf = null)
    {
        include_turf ??= FALSE;
        DmList dot = null;
        dot = Dm.list();
        atom location = atom_on_location.loc;
        turf our_turf = get_turf(atom_on_location);
        while (Dm.truthy(location) && location != our_turf)
        {
            Dm.append(ref dot, location);
            location = location.loc;
        }
        if (Dm.truthy(our_turf) && Dm.truthy(include_turf)) //At this point, only the turf is left, provided it exists.
            Dm.append(ref dot, our_turf);
        return dot;
    }

}

// tg: code/__HELPERS/atoms.dm:407 (interleaved types, rules 1.9)
public partial class atom : datum
{
    ///Returns true if the src countain the atom target
    // tg: code/__HELPERS/atoms.dm:407
    public virtual object contains(atom target = null)
    {
        if (!Dm.truthy(target))
            return FALSE;
        for (atom location = target.loc; Dm.truthy(location); location = location.loc)
        {
            if (location == this)
                return TRUE;
        }
        return null;
    }
}

public static partial class GlobalProcs
{
    /**
     * Compare source's dir, the clockwise dir of source and the anticlockwise dir of source
     * To the opposite dir of the dir returned by get_dir(target,source)
     * If one of them is a match, then source is facing target
    **/
    // tg: code/__HELPERS/atoms.dm:147  (gaps ST2: the basketball's steal)
    public static object is_source_facing_target(atom source = null, atom target = null)
    {
        if (source == null || target == null)
            return FALSE;
        if (isliving(source))
        {
            mob_living source_mob = (mob_living)source;
            if (Dm.eq(source_mob.body_position, LYING_DOWN))
                return FALSE;
        }
        float goal_dir = get_dir(source, target);
        float clockwise_source_dir = turn(source.dir, -45);
        float anticlockwise_source_dir = turn(source.dir, 45);

        if (source.dir == goal_dir || clockwise_source_dir == goal_dir || anticlockwise_source_dir == goal_dir)
            return TRUE;
        return FALSE;
    }

    ///A do nothing proc
    // tg: code/__HELPERS/atoms.dm:415
    public static void pass(params object[] __extra)
    {
        return;
    }
}

// tg: code/__HELPERS/atoms.dm:419 (interleaved types, rules 1.9)
public partial class atom : datum
{
    // tg: code/__HELPERS/atoms.dm:419
    public virtual object get_oversized_icon_offsets()
    {
        if (!Dm.truthy(base_pixel_x) && !Dm.truthy(base_pixel_y) && !Dm.truthy(base_pixel_w) && !Dm.truthy(base_pixel_z))
            return Dm.list(Dm.kv("x", 0), Dm.kv("y", 0));
        // tg: :422-428  var/list/icon_dimensions = get_icon_dimensions(icon) ... return list("x" = ..., "y" = ...)
        // DEVIATION(DV108): the core knows no .dmi metadata, so an icon counts as 32 by 32: nothing oversized to reverse (a beacon's pixel_z reached here, J6)
        return Dm.list(Dm.kv("x", 0), Dm.kv("y", 0));
    }
}

// tg: code/__HELPERS/atoms.dm:312 (global procs; generate_items_inside, :306, is above)
public static partial class GlobalProcs
{
    ///Forces the atom to take a step in a random direction
    // tg: code/__HELPERS/atoms.dm:134  (the slot machine's and the roulette's coins)
    public static async DmTask random_step(atom_movable moving_atom = null, object steps = null, object chance = null)
    {
        float initial_chance = Dm.num(chance);
        float steps__1 = Dm.num(steps);
        float chance__1 = Dm.num(chance);
        while (steps__1 > 0)
        {
            if (prob(chance__1))
                await step(moving_atom, Dm.num(pick(GLOB.alldirs)));
            chance__1 = max(chance__1 - (initial_chance / steps__1), 0);
            steps__1--;
        }
    }

    // tg: code/__HELPERS/atoms.dm:312
    public static object get(object loc = null, Type type = null)
    {
        while (Dm.truthy(loc))
        {
            if (istype(loc, type))
                return loc;
            loc = ((atom)loc).loc;
        }
        return null;
    }

    //Gets the topmost loose container given the passed in LOOSE_CONTAINER flags
    // tg: code/__HELPERS/atoms.dm:320 (gaps RD4: the relics' teleports, shields and blood)
    public static atom_movable get_loose_container(atom_movable loose = null, float? container_flags = null)
    {
        container_flags ??= ALL;
        while (ismovable(loose.loc))
        {
            if (!Dm.truthy(Dm.bitand(container_flags, LOOSE_CONTAINER_INCLUDE_STORAGE)) && isitem(loose))
            {
                var item = (obj_item)loose;
                if (Dm.truthy(Dm.bitand(item.item_flags, IN_STORAGE)))
                    break;
            }
            var movable = (atom_movable)loose.loc;
            if (Dm.truthy(movable.anchored))
                break;
            if (isliving(movable))
            {
                var living = (mob_living)movable;
                if (!Dm.truthy(Dm.bitand(container_flags, LOOSE_CONTAINER_INCLUDE_INVENTORY)))
                {
                    DmList equipped = living.get_equipped_items(Dm.bitor(INCLUDE_HELD, INCLUDE_POCKETS)) as DmList;
                    if (Dm.@in(loose, equipped) && !HAS_TRAIT(loose, TRAIT_NODROP))
                    {
                        if (loose is obj_item_mod_control && Dm.truthy(Dm.bitand(container_flags, LOOSE_CONTAINER_INCLUDE_SEALED_MODSUIT)))
                        {
                            // gaps MO1: the MODsuit's part datums are live (mod/mod_control.cs)
                            var modsuit = (obj_item_mod_control)loose;
                            bool @sealed = true;
                            foreach (datum_mod_part part in Dm.each_cast<datum_mod_part>(modsuit.get_part_datums(TRUE)))
                            {
                                if ((part.part_item == modsuit || part.part_item.loc != modsuit) && !Dm.truthy(part.@sealed))
                                {
                                    @sealed = false;
                                    break;
                                }
                            }
                            if (!@sealed)
                                break;
                        }
                        else
                            break;
                    }
                }
                if (living.buckled != null)
                {
                    if (Dm.truthy(living.buckled.anchored))
                        break;
                    else
                    {
                        var buckled_obj = (obj)living.buckled;
                        Waiting.NoSleep(buckled_obj.unbuckle_mob(living));
                    }
                }
            }
            if (!Dm.truthy(Dm.bitand(container_flags, LOOSE_CONTAINER_INCLUDE_CLOSET)) && iscloset(movable))
                break;
            if (!Dm.truthy(Dm.bitand(container_flags, LOOSE_CONTAINER_INCLUDE_MECH_EQUIPMENT)) && movable is obj_item_mecha_parts_mecha_equipment)
                break;
            if (!Dm.truthy(Dm.bitand(container_flags, LOOSE_CONTAINER_INCLUDE_VEHICLE)) && isvehicle(movable))
            {
                var vehicle = (obj_vehicle)movable;
                if (vehicle.is_occupant(loose as mob))
                    break;
            }
            if (!Dm.truthy(Dm.bitand(container_flags, LOOSE_CONTAINER_INCLUDE_STOMACH)) && movable is obj_item_organ_stomach)
            {
                var stomach = (obj_item_organ_stomach)movable;
                if (Dm.@in(loose, stomach.stomach_contents))
                    break;
            }
            loose = movable;
        }
        return loose;
    }
}

// tg: code/__HELPERS/atoms.dm:201 (M7 EMP: empulse's range)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/atoms.dm:183  (the M7 delamination slice: a stage six singularity's combust_mobs)
    public static object urange(float? dist = null, atom center = null, float? orange = null, float? areas = null)
    {
        dist ??= 0;
        center ??= usr;
        orange ??= FALSE;
        areas ??= FALSE;
        DmList dot = null;
        if (!Dm.truthy(dist))
        {
            if (!Dm.truthy(orange))
                return Dm.list(center);
            else
                return Dm.list();
        }

        DmList turfs = RANGE_TURFS(dist, center);
        if (Dm.truthy(orange))
            Dm.remove(ref turfs, get_turf(center));
        dot = Dm.list();
        foreach (turf checked_turf in Dm.each_cast<turf>(turfs))
        {
            Dm.append(ref dot, checked_turf);
            Dm.append(ref dot, checked_turf.contents);
            if (Dm.truthy(areas))
                Dm.combine(ref dot, checked_turf.loc);
        }
        return dot;
    }

    ///similar function to range(), but with no limitations on the distance; will search spiralling outwards from the center
    // tg: code/__HELPERS/atoms.dm:201
    public static object spiral_range(float? dist = null, object center = null, float? orange = null)
    {
        dist ??= 0;
        center ??= World.Current.usr;
        orange ??= FALSE;
        DmList atom_list = Dm.list();
        turf t_center = get_turf(center as atom);
        if (t_center == null)
            return Dm.list();

        if (!Dm.truthy(orange))
        {
            Dm.append(atom_list, t_center);
            Dm.append(atom_list, t_center.contents);
        }

        if (!Dm.truthy(dist))
            return atom_list;

        turf checked_turf;
        float y;
        float x;
        float c_dist = 1;

        while (c_dist <= dist)
        {
            y = t_center.y + c_dist;
            x = t_center.x - c_dist + 1;
            for (; x <= t_center.x + c_dist; x++)
            {
                checked_turf = locate(x, y, t_center.z);
                if (checked_turf != null)
                {
                    Dm.append(atom_list, checked_turf);
                    Dm.append(atom_list, checked_turf.contents);
                }
            }

            y = t_center.y + c_dist - 1;
            x = t_center.x + c_dist;
            for (float yy = t_center.y - c_dist; yy <= y; yy++)
            {
                checked_turf = locate(x, yy, t_center.z);
                if (checked_turf != null)
                {
                    Dm.append(atom_list, checked_turf);
                    Dm.append(atom_list, checked_turf.contents);
                }
            }

            y = t_center.y - c_dist;
            x = t_center.x + c_dist - 1;
            for (float xx = t_center.x - c_dist; xx <= x; xx++)
            {
                checked_turf = locate(xx, y, t_center.z);
                if (checked_turf != null)
                {
                    Dm.append(atom_list, checked_turf);
                    Dm.append(atom_list, checked_turf.contents);
                }
            }

            y = t_center.y - c_dist + 1;
            x = t_center.x - c_dist;
            for (; y <= t_center.y + c_dist; y++)
            {
                checked_turf = locate(x, y, t_center.z);
                if (checked_turf != null)
                {
                    Dm.append(atom_list, checked_turf);
                    Dm.append(atom_list, checked_turf.contents);
                }
            }
            c_dist++;
        }

        return atom_list;
    }

    // tg: code/__HELPERS/atoms.dm:61
    public static object can_see(atom source = null, atom target = null, float? length = null) // I couldnt be arsed to do actual raycasting :I This is horribly inaccurate.
    {
        length ??= 5;
        turf current = get_turf(source);
        turf target_turf = get_turf(target);
        if (get_dist(source, target) > length)
            return FALSE;
        if (current == target_turf)
            return TRUE;
        float steps = 1;
        if (current == target_turf)//they are on the same turf, source can see the target
            return TRUE;
        current = get_step_towards(current, target_turf);
        while (current != target_turf)
        {
            if (steps > length)
                return FALSE;
            if (current.directional_opacity == ALL_CARDINALS) // IS_OPAQUE_TURF(current)
                return FALSE;
            current = get_step_towards(current, target_turf);
            steps++;
        }
        return TRUE;
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/atoms.dm:258
    public static object get_closest_atom(Type type = null, DmList atom_list = null, object source = null)
    {
        object closest_atom = null;
        float closest_distance = 0;
        foreach (object atom in Dm.each(atom_list))
        {
            if (!istype(atom, type))
                continue;
            float distance = get_dist(source, atom);
            if (!Dm.truthy(closest_atom))
            {
                closest_distance = distance;
                closest_atom = atom;
            }
            else
            {
                if (closest_distance > distance)
                {
                    closest_distance = distance;
                    closest_atom = atom;
                }
            }
        }
        return closest_atom;
    }
}

// gaps package P6 (D298): the medical beamgun's line of sight
// tg: code/__HELPERS/atoms.dm:367
public static partial class GlobalProcs
{
    /**
     * Checks if the target is in line of sight of the user.
     * args:
     * * user = Origin to start at.
     * * target = End point.
     * * pass_args = pass_flags given to dummy object to allow it to ignore certain types of blockades.
     */
    // tg: code/__HELPERS/atoms.dm:375  (target: tg declares mob/target, but the MOD teleporter module passes the turf it jumps to,
    // modules_science.dm:116, and the proc reads it only as the line's end point; held as an atom, gaps MO2)
    public static async DmTask<object> los_check(atom_movable user = null, atom target = null, float? pass_args = null, datum_callback mid_check = null)
    {
        pass_args ??= Dm.num(Dm.bitor(Dm.bitor(PASSTABLE, PASSGLASS), PASSGRILLE));
        turf user_turf = user.loc as turf;
        if (!istype(user_turf, typeof(turf)))
            return FALSE;
        obj dummy = Dm.@new<obj>(user_turf);
        dummy.pass_flags = Dm.num(Dm.bitor(dummy.pass_flags, pass_args)); //Grille/Glass so it can be used through common windows
        turf previous_step = user_turf;
        bool first_step = true;
        foreach (turf next_step in Dm.each_cast<turf>(Dm.sub(get_line(user_turf, target) as DmList, user_turf) as DmList))
        {
            if (first_step)
            {
                foreach (obj blocker in Dm.each<obj>(user_turf))
                {
                    if (!Dm.truthy(blocker.density) || !Dm.truthy(Dm.bitand(blocker.flags_1, ON_BORDER_1)))
                        continue;
                    if (Dm.truthy(await blocker.CanPass(dummy, get_dir(user_turf, next_step))))
                        continue;
                    return FALSE; // Could not leave the first turf. (tg leaves the dummy there)
                }
                first_step = false;
            }
            if (Dm.truthy(next_step.density))
            {
                qdel(dummy);
                return FALSE;
            }
            foreach (atom_movable movable in Dm.each_cast<atom_movable>(next_step.contents.Copy()))
            {
                if (!Dm.truthy(await movable.CanPass(dummy, get_dir(next_step, previous_step))))
                {
                    qdel(dummy);
                    return FALSE;
                }
            }
            if (mid_check != null && Dm.eq(await mid_check.Invoke(user, target, pass_args, next_step, dummy), FALSE)) // specify false as it may return null if there's no check
            {
                qdel(dummy);
                return FALSE;
            }
            previous_step = next_step;
        }
        qdel(dummy);
        return TRUE;
    }
}

// tg: code/__HELPERS/atoms.dm:82 (gaps P3b)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/atoms.dm:82
    public static float get_cardinal_dir(atom start = null, atom end = null)
    {
        float dx = abs(end.x - start.x);
        float dy = abs(end.y - start.y);
        return Dm.bitand(get_dir(start, end), rand() * (dx + dy) < dy ? 3 : 12);
    }
}
