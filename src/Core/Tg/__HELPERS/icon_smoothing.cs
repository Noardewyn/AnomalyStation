// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/icon_smoothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//generic (by snowflake + others) tile smoothing code; smooth your icons with this!
/*
	Smoothed atoms are displayed using prebaked icon states which have been "cut" from a template to match a set of possible smoothing directions.
	This allows for a variety of effects, such as nice wall textures, gloopy slime, carpet that makes sense, etc etc etc

	To use this, just set your atom's 'smoothing_flags' var to SMOOTH_BITMASK. There are other smoothing strategies but that's all we need to worry about right now
	If you don't want your atom's icon to smooth with anything but atoms of the same type, set the string 'canSmoothWith' to null;
	Otherwise, put all the smoothing groups you want the atom icon to smooth with in 'canSmoothWith', including the group of the atom itself.
	Smoothing groups are just shared flags between objects. If one of the 'canSmoothWith' of A matches one of the `smoothing_groups` of B, then A will smooth with B.

	(tg's longer notes on diagonal, border and filtered smoothing: icon_smoothing.dm:12-47.)
*/

// M3 P4: the whole file. /turf/closed/wall/smooth (icon_smoothing.dm:473) holds only vars (the generated part).

// tg: code/__HELPERS/icon_smoothing.dm:49  GLOBAL_LIST_INIT(adjacent_direction_lookup, generate_adjacent_directions())
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/__HELPERS/icon_smoothing.dm:49  (GLOBAL_RAW(/list/adjacent_direction_lookup), __DEFINES/__globals.dm:32)
    public DmList adjacent_direction_lookup;

    // tg: code/__HELPERS/icon_smoothing.dm:49  (GLOBAL_MANAGED(adjacent_direction_lookup, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaladjacent_direction_lookup()
    {
        adjacent_direction_lookup = generate_adjacent_directions() as DmList;
        Dm.append(ref gvars_datum_init_order, "adjacent_direction_lookup");
    }
}

// tg: code/__HELPERS/icon_smoothing.dm:73  (the global procs, rules 7.8)
public static partial class GlobalProcs
{
    /* Attempting to mirror the below
     * Each 3x3 grid is a tile, with each X representing a direction a border object could be in IN said grid
     * Directions marked with A are acceptable smoothing targets, M is the example direction
     * The example given here is of a northfacing border object
    xxx AxA xxx
    xxx AxA xxx
    xxx AxA xxx

    AAA MMM AAA
    xxx AxA xxx
    xxx AxA xxx

    xxx xxx xxx
    xxx xxx xxx
    xxx xxx xxx
    */
    /// Encodes connectivity between border objects
    /// Returns a list accessable by a border object's dir, the direction between it and a target, and a target
    /// Said list will return the direction the two objects connect, if any exists (if the target isn't a border object and the direction is fine, return the inverse of the direction in use)
    // tg: code/__HELPERS/icon_smoothing.dm:73
    public static object generate_adjacent_directions()
    {
        // Have to hold all conventional dir pairs, so we size to the largest
        // We don't HAVE diagonal border objects, so I'm gonna pretend they'll never exist

        // You might be like, lemon, can't we use GLOB.cardinals/GLOB.alldirs here
        // No, they aren't loaded yet. life is pain
        DmList cardinals = Dm.list(NORTH, SOUTH, EAST, WEST);
        DmList alldirs = Dm.add(cardinals, Dm.list(Dm.bitor(NORTH, EAST), Dm.bitor(SOUTH, EAST), Dm.bitor(NORTH, WEST), Dm.bitor(SOUTH, WEST))) as DmList;
        object largest_cardinal = max(cardinals);
        object largest_dir = max(alldirs);

        DmList direction_map = Dm.list_n(Dm.num(largest_cardinal));
        foreach (object dir in Dm.each(cardinals))
        {
            object left = turn(dir, 90);
            object right = turn(dir, -90);
            float opposite = REVERSE_DIR(Dm.num(dir));
            // Need to encode diagonals here because it's possible, even if it is always false
            DmList acceptable_adjacents = Dm.list_n(Dm.num(largest_dir));
            // Alright, what directions are acceptable to us
            foreach (object connectable_dir in Dm.each(Dm.add(cardinals, NONE)))
            {
                // And what border objects INSIDE those directions are alright
                DmList smoothable_dirs = Dm.list_n(Dm.num(Dm.add(largest_cardinal, 1))); // + 1 because we need to provide space for NONE to be a valid index
                // None is fine, we want to smooth with things on our own turf
                // We'll do the two dirs to our left and right
                // They connect.. "below" us and on their side
                if (Dm.eq(connectable_dir, NONE))
                {
                    smoothable_dirs[left] = dir_to_junction(Dm.bitor(opposite, left));
                    smoothable_dirs[right] = dir_to_junction(Dm.bitor(opposite, right));
                }
                // If it's to our right or left we'll include just the dir matching ours
                // Left edge touches only our left side, and so on
                else if (Dm.eq(connectable_dir, left))
                    smoothable_dirs[dir] = left;
                else if (Dm.eq(connectable_dir, right))
                    smoothable_dirs[dir] = right;
                // If it's straight on we'll include our direction as a link
                // Then include the two edges on the other side as diagonals
                else if (Dm.eq(connectable_dir, dir))
                {
                    smoothable_dirs[opposite] = dir;
                    smoothable_dirs[left] = dir_to_junction(Dm.bitor(dir, left));
                    smoothable_dirs[right] = dir_to_junction(Dm.bitor(dir, right));
                }
                // otherwise, go HOME, I don't want to encode anything for you
                else
                    continue;
                acceptable_adjacents[Dm.add(connectable_dir, 1)] = smoothable_dirs;
            }
            direction_map[dir] = acceptable_adjacents;
        }
        return direction_map;
    }

    /// Helper proc for smoothing border objects. Return this from smoothing_allowed() to get the desired effect
    /// Indexes into some global lists that encode direction bullshit
    // tg: code/__HELPERS/icon_smoothing.dm:122
    public static object should_border_obj_smooth(atom border_obj = null, atom target = null, object direction = null)
    {
        // If the target is also a border obj, take its dirs into account
        if (Dm.truthy(Dm.bitand(target.smoothing_flags, SMOOTH_BORDER_OBJECT)))
            return ((DmList)((DmList)GLOB.adjacent_direction_lookup[(border_obj?.dir is var or__1 && Dm.truthy(or__1) ? (object)or__1 : NORTH)])[Dm.add(direction, 1)])?[target.dir];
        // Otherwise, do it normal style
        if (Dm.truthy(((DmList)GLOB.adjacent_direction_lookup[(border_obj?.dir is var or__2 && Dm.truthy(or__2) ? (object)or__2 : NORTH)])[Dm.add(direction, 1)]))
            return REVERSE_DIR(Dm.num(direction));
        return NONE;
    }
}

// tg: code/__HELPERS/icon_smoothing.dm:132
public partial class atom : datum
{
    ///do not use, use QUEUE_SMOOTH(atom)
    // tg: code/__HELPERS/icon_smoothing.dm:132
    public virtual void smooth_icon__proc()
    {
        if (QDELETED(this))
            return;
        Dm.mask(ref smoothing_flags, Dm.bitnot(SMOOTH_QUEUED));
        Dm.combine(ref flags_1, HTML_USE_INITAL_ICON_1);
        if (!Dm.truthy(z))
            throw Dm.CRASH(Dm.text("[] called smooth_icon() without being on a z-level", type), "code/__HELPERS/icon_smoothing.dm", 138);
        if (Dm.truthy(Dm.bitand(smoothing_flags, Dm.bitor(SMOOTH_BITMASK, SMOOTH_BITMASK_CARDINALS))))
            bitmask_smooth();
        else
            throw Dm.CRASH(Dm.text("smooth_icon called for [] with smoothing_flags == []", this, smoothing_flags), "code/__HELPERS/icon_smoothing.dm", 142);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SMOOTHED_ICON]))
            this._SendSignal(COMSIG_ATOM_SMOOTHED_ICON, Dm.list(this));
    }
}

// tg: code/__HELPERS/icon_smoothing.dm:147
public partial class atom_movable : atom
{
    // As a rule, movables will most always care about smoothing changes
    // Turfs on the other hand, don't, so we don't do the update for THEM unless they explicitly request it
    // tg: code/__HELPERS/icon_smoothing.dm:147
    public override void smooth_icon__proc()
    {
        base.smooth_icon__proc();
        update_appearance(Dm.bitnot(UPDATE_SMOOTHING));
    }
}

// tg: code/__HELPERS/icon_smoothing.dm:164
public partial class atom : datum
{
    /**
     * Allows for more direct control over filtering smoothing targets. Only runs if SMOOTH_PROC_FILTER (OR SMOOTH_BORDER_OBJECT) is enabled
     * Called as a final point of decision on if a smooth should go through or not.
     *
     * NOTE: this proc NEEDS TO BE pure (same result if inputs with the same state are passed in, has no side effects)
     * You are taking some control away from the smoothing system, so you also need to force an update if something you are dependent on changes. This can be hard.
     *
     * atom/smoothing_with : the atom we are attemption to smooth with, or null if SMOOTH_BORDER is set and we're talking to the edge of the map
     * direction : the cardinal direction from us to our target atom (provided for convenience)
     * junction : the smoothing junction this atom would normally lie on (you almost always want to return this)
     *
     * Returns the junction to use (you almost always want this to just be junction)
    */
    // tg: code/__HELPERS/icon_smoothing.dm:164
    [ShouldBePure]
    public virtual object smoothing_allowed(atom smoothing_with = null, object direction = null, object junction = null)
    {
        stack_trace(Dm.text("Unoverrided call to allow_smooth by [], if you're going to have SMOOTH_PROC_FILTER (or SMOOTH_BORDER_OBJECT) set you need to do something with it", type), "code/__HELPERS/icon_smoothing.dm", 166);
        return NONE;
    }

    /**
     * Basic smoothing proc. The atom checks for adjacent directions to smooth with and changes the icon_state based on that.
     *
     * Returns the previous smoothing_junction state so the previous state can be compared with the new one after the proc ends, and see the changes, if any.
     *
    */
    // tg: code/__HELPERS/icon_smoothing.dm:175
    public virtual void bitmask_smooth()
    {
        float new_junction = NONE;

        // cache for sanic speed
        object canSmoothWith = this.canSmoothWith;
        area home_base = get_area(this) as area;
        Type area_limited_icon_smoothing = home_base?.area_limited_icon_smoothing;

        float smooth_border = Dm.bitand(smoothing_flags, SMOOTH_BORDER);
        float smooth_obj = Dm.bitand(smoothing_flags, SMOOTH_OBJ);
        float border_object_smoothing = Dm.bitand(smoothing_flags, SMOOTH_BORDER_OBJECT);
        float smooth_proc_filter = Dm.bitand(smoothing_flags, SMOOTH_PROC_FILTER);
        float just_set_junction = Dm.num(!(Dm.truthy(border_object_smoothing) || Dm.truthy(smooth_proc_filter)));

        // The file-local macros SEARCH_ADJ_IN_DIR (:186), SMOOTH_AGAINST (:209) and JUNCTION_FOUND (:217), #undef'd at :275, are
        // expanded as local functions (rules 11.4): they read and write this proc's locals by closure, as the macros' text
        // does, and SEARCH_ADJ_IN_DIR's `break set_adj_in_dir` (the end of its labelled do/while block) is its return.
        // A JUNCTION_FOUND that asks for `break set_adj_in_dir` reports it by returning true.

        // tg: code/__HELPERS/icon_smoothing.dm:186  #define SEARCH_ADJ_IN_DIR(direction, direction_flag)
        void SEARCH_ADJ_IN_DIR(float direction, float direction_flag)
        {
            turf neighbor = get_step(this, direction);
            if (Dm.truthy(neighbor) && (!Dm.truthy(area_limited_icon_smoothing) || istype(neighbor.loc, area_limited_icon_smoothing)))
            {
                object neighbor_smoothing_groups = neighbor.smoothing_groups;
                if (Dm.truthy(neighbor_smoothing_groups))
                {
                    if (SMOOTH_AGAINST(neighbor, direction, direction_flag, neighbor_smoothing_groups))
                        return;
                }
                if (Dm.truthy(smooth_obj))
                {
                    foreach (atom_movable thing in Dm.each_cast<atom_movable>(neighbor))
                    {
                        object thing_smoothing_groups = thing.smoothing_groups;
                        if (!Dm.truthy(thing.anchored) || thing_smoothing_groups == null)
                            continue;
                        if (SMOOTH_AGAINST(thing, direction, direction_flag, thing_smoothing_groups))
                            return;
                    }
                }
            }
            else if (Dm.truthy(smooth_border))
            {
                // JUNCTION_FOUND(null, direction, direction_flag): here its plain `break`s leave the do/while block as well
                JUNCTION_FOUND(null, direction, direction_flag, out _);
            }
        }

        // tg: code/__HELPERS/icon_smoothing.dm:209  #define SMOOTH_AGAINST(thing, direction, direction_flag, their_groups)
        bool SMOOTH_AGAINST(atom thing, float direction, float direction_flag, object their_groups)
        {
            foreach (object target in Dm.each(canSmoothWith))
            {
                if (Dm.truthy(Dm.bitand(((DmList)canSmoothWith)[target], ((DmList)their_groups)[target])) &&
                    (!Dm.truthy(Dm.bitand(thing.smoothing_flags, SMOOTH_PROC_FILTER)) || Dm.truthy(thing.smoothing_allowed(this, REVERSE_DIR(direction), reverse_junction(direction_flag)))))
                {
                    if (JUNCTION_FOUND(thing, direction, direction_flag, out bool break_loop))
                        return true;
                    if (break_loop)
                        break;
                }
            }
            return false;
        }

        // tg: code/__HELPERS/icon_smoothing.dm:217  #define JUNCTION_FOUND(target, direction, direction_flag)
        // Returns true for `break set_adj_in_dir`; break_loop is its plain `break` (of the loop around it).
        bool JUNCTION_FOUND(atom target, float direction, float direction_flag, out bool break_loop)
        {
            break_loop = false;
            if (Dm.truthy(just_set_junction))
            {
                Dm.combine(ref new_junction, direction_flag);
                return true;
            }
            if (Dm.truthy(border_object_smoothing))
            {
                Dm.combine(ref new_junction, Dm.num(smoothing_allowed(target, direction, direction_flag)));
                break_loop = true; /* border objects can find more then one junction on one turf, sooo */
                return false;
            }
            if (Dm.truthy(smooth_proc_filter))
            {
                float old_junction = new_junction;
                Dm.combine(ref new_junction, Dm.num(smoothing_allowed(target, direction, direction_flag)));
                if (old_junction == new_junction)
                {
                    break_loop = true;
                    return false;
                }
                return true;
            }
            return false;
        }

        // Let's go over all our cardinals
        if (Dm.truthy(border_object_smoothing))
        {
            SEARCH_ADJ_IN_DIR(NORTH, NORTH);
            SEARCH_ADJ_IN_DIR(SOUTH, SOUTH);
            SEARCH_ADJ_IN_DIR(EAST, EAST);
            SEARCH_ADJ_IN_DIR(WEST, WEST);
            // We want to check against stuff in our own turf
            SEARCH_ADJ_IN_DIR(NONE, NONE);
            // Border objects don't do diagonals, so GO HOME
            set_smoothed_icon_state(new_junction);
            return;
        }

        SEARCH_ADJ_IN_DIR(NORTH, NORTH);
        SEARCH_ADJ_IN_DIR(SOUTH, SOUTH);
        SEARCH_ADJ_IN_DIR(EAST, EAST);
        SEARCH_ADJ_IN_DIR(WEST, WEST);

        // If there's nothing going on already
        if (Dm.truthy(Dm.bitand(smoothing_flags, SMOOTH_BITMASK_CARDINALS)) || !Dm.truthy(Dm.bitand(new_junction, Dm.bitor(NORTH, SOUTH))) || !Dm.truthy(Dm.bitand(new_junction, Dm.bitor(EAST, WEST))))
        {
            set_smoothed_icon_state(new_junction);
            return;
        }

        if (Dm.truthy(Dm.bitand(new_junction, NORTH_JUNCTION)))
        {
            if (Dm.truthy(Dm.bitand(new_junction, WEST_JUNCTION)))
                SEARCH_ADJ_IN_DIR(NORTHWEST, NORTHWEST_JUNCTION);

            if (Dm.truthy(Dm.bitand(new_junction, EAST_JUNCTION)))
                SEARCH_ADJ_IN_DIR(NORTHEAST, NORTHEAST_JUNCTION);
        }

        if (Dm.truthy(Dm.bitand(new_junction, SOUTH_JUNCTION)))
        {
            if (Dm.truthy(Dm.bitand(new_junction, WEST_JUNCTION)))
                SEARCH_ADJ_IN_DIR(SOUTHWEST, SOUTHWEST_JUNCTION);

            if (Dm.truthy(Dm.bitand(new_junction, EAST_JUNCTION)))
                SEARCH_ADJ_IN_DIR(SOUTHEAST, SOUTHEAST_JUNCTION);
        }

        set_smoothed_icon_state(new_junction);
    }

    ///Changes the icon state based on the new junction bitmask
    // tg: code/__HELPERS/icon_smoothing.dm:285
    public virtual object set_smoothed_icon_state(object new_junction = null)
    {
        object dot = null;
        dot = smoothing_junction;
        smoothing_junction = new_junction;
        icon_state = Dm.text("[]-[]", base_icon_state, smoothing_junction);
        return dot;
    }
}

// tg: code/__HELPERS/icon_smoothing.dm:290
[DmPath("/turf/closed")]
public partial class turf_closed : turf
{
    // tg: code/__HELPERS/icon_smoothing.dm:281  #define DEFAULT_UNDERLAY_ICON_STATE "plating" (file-local, #undef at :337)
    private const string DEFAULT_UNDERLAY_ICON_STATE = "plating";

    // tg: code/__HELPERS/icon_smoothing.dm:290
    public override object set_smoothed_icon_state(object new_junction = null)
    {
        // Avoid calling ..() here to avoid setting icon_state twice, which is expensive given how hot this proc is
        object old_junction = smoothing_junction;
        smoothing_junction = new_junction;

        if (!Dm.truthy(Dm.bitand(smoothing_flags, SMOOTH_DIAGONAL_CORNERS)))
        {
            icon_state = Dm.text("[]-[]", base_icon_state, smoothing_junction);
            return null;
        }

        object sw__1 = new_junction;
        if (Dm.eq(sw__1, Dm.bitor(NORTH_JUNCTION, WEST_JUNCTION))
            || Dm.eq(sw__1, Dm.bitor(NORTH_JUNCTION, EAST_JUNCTION))
            || Dm.eq(sw__1, Dm.bitor(SOUTH_JUNCTION, WEST_JUNCTION))
            || Dm.eq(sw__1, Dm.bitor(SOUTH_JUNCTION, EAST_JUNCTION))
            || Dm.eq(sw__1, Dm.bitor(Dm.bitor(NORTH_JUNCTION, WEST_JUNCTION), NORTHWEST_JUNCTION))
            || Dm.eq(sw__1, Dm.bitor(Dm.bitor(NORTH_JUNCTION, EAST_JUNCTION), NORTHEAST_JUNCTION))
            || Dm.eq(sw__1, Dm.bitor(Dm.bitor(SOUTH_JUNCTION, WEST_JUNCTION), SOUTHWEST_JUNCTION))
            || Dm.eq(sw__1, Dm.bitor(Dm.bitor(SOUTH_JUNCTION, EAST_JUNCTION), SOUTHEAST_JUNCTION)))
        {
            icon_state = Dm.text("[]-[]-diagonal", base_icon_state, smoothing_junction);
            if (Dm.eq(new_junction, old_junction) || Dm.truthy(fixed_underlay)) // Mutable underlays?
                return null;

            float junction_dir = Dm.num(reverse_ndir(smoothing_junction));
            float turned_adjacency = REVERSE_DIR(junction_dir);
            turf neighbor_turf = get_step(this, Dm.bitand(turned_adjacency, Dm.bitor(NORTH, SOUTH)));
            mutable_appearance underlay_appearance = mutable_appearance(layer: LOW_FLOOR_LAYER, offset_spokesman: this, plane: FLOOR_PLANE) as mutable_appearance;
            if (!Dm.truthy(neighbor_turf.get_smooth_underlay_icon(underlay_appearance, this, turned_adjacency)))
            {
                neighbor_turf = get_step(this, Dm.bitand(turned_adjacency, Dm.bitor(EAST, WEST)));

                if (!Dm.truthy(neighbor_turf.get_smooth_underlay_icon(underlay_appearance, this, turned_adjacency)))
                {
                    neighbor_turf = get_step(this, turned_adjacency);

                    if (!Dm.truthy(neighbor_turf.get_smooth_underlay_icon(underlay_appearance, this, turned_adjacency)))
                    {
                        if (!Dm.truthy(get_smooth_underlay_icon(underlay_appearance, this, turned_adjacency))) //if all else fails, ask our own turf
                        {
                            underlay_appearance.icon = Dm.resource("icons/turf/floors.dmi"); // DEFAULT_UNDERLAY_ICON (icon_smoothing.dm:280, file-local)
                            underlay_appearance.icon_state = DEFAULT_UNDERLAY_ICON_STATE;
                        }
                    }
                }
            }
            Dm.append(underlays, underlay_appearance);
        }
        else
            icon_state = Dm.text("[]-[]", base_icon_state, smoothing_junction);
        return null;
    }
}

// tg: code/__HELPERS/icon_smoothing.dm:332
public partial class turf_open_floor : turf_open
{
    // tg: code/__HELPERS/icon_smoothing.dm:332
    public override object set_smoothed_icon_state(object new_junction = null)
    {
        if (Dm.truthy(broken) || Dm.truthy(burnt))
            return null;
        return base.set_smoothed_icon_state(new_junction);
    }
}

// tg: code/__HELPERS/icon_smoothing.dm:341  (the global procs, rules 7.8)
public static partial class GlobalProcs
{
    //Icon smoothing helpers
    // tg: code/__HELPERS/icon_smoothing.dm:341
    public static void smooth_zlevel(object zlevel = null, float? now = null)
    {
        now ??= FALSE;
        DmList away_turfs = Z_TURFS(Dm.num(zlevel));
        foreach (turf turf_to_smooth in Dm.each_cast<turf>(away_turfs))
        {
            if (Dm.truthy(Dm.bitand(turf_to_smooth.smoothing_flags, USES_SMOOTHING)))
            {
                if (Dm.truthy(now))
                    turf_to_smooth.smooth_icon__proc();
                else
                    QUEUE_SMOOTH(turf_to_smooth);
            }
            foreach (atom_movable movable_to_smooth in Dm.each_cast<atom_movable>(turf_to_smooth))
            {
                if (Dm.truthy(Dm.bitand(movable_to_smooth.smoothing_flags, USES_SMOOTHING)))
                {
                    if (Dm.truthy(now))
                        movable_to_smooth.smooth_icon__proc();
                    else
                        QUEUE_SMOOTH(movable_to_smooth);
                }
            }
        }
    }
}

// tg: code/__HELPERS/icon_smoothing.dm:356
public partial class atom : datum
{
    // tg: code/__HELPERS/icon_smoothing.dm:356
    public virtual void set_can_smooth_with(object canSmoothWith = null)
    {
        if (!Dm.truthy(canSmoothWith))
        {
            this.canSmoothWith = null;
            return;
        }
        // PARSE_CAN_SMOOTH_WITH(canSmoothWith, src.canSmoothWith, smoothing_flags) (__DEFINES/icon_smoothing.dm:250), expanded
        // inline (rules 11.1 (d): it reads the caller's canSmoothWith)
        if (Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(focus_only/sorted_smoothing_groups) is FALSE in production (rules 1.7)
        {
            // ASSERT_SORTED_SMOOTHING_GROUPS(canSmoothWith) (__DEFINES/icon_smoothing.dm:265)
            DmList unwrapped = json_decode(Dm.text("\\[[]0\\]", canSmoothWith)) as DmList;
            unwrapped.len--;
            assert_sorted(unwrapped, Dm.text("canSmoothWith ([])", type));
        }
        /* S_OBJ is always negative, and we are guaranteed to be sorted. */
        // SHIM-GAP: BYOND 515 text indexing (canSmoothWith[1], the character at a position) is not in the shim;
        // copytext(canSmoothWith, 1, 2) gives the same one-character text.
        if (Dm.eq(copytext(canSmoothWith, 1, 2), "-"))
            Dm.combine(ref smoothing_flags, SMOOTH_OBJ);
        SET_SMOOTHING_GROUPS(canSmoothWith, ref this.canSmoothWith);
    }

    // tg: code/__HELPERS/icon_smoothing.dm:362
    public virtual void set_smoothing_groups(object smoothing_groups = null)
    {
        if (!Dm.truthy(smoothing_groups))
        {
            this.smoothing_groups = null;
            return;
        }
        // PARSE_SMOOTHING_GROUPS(smoothing_groups, src.smoothing_groups) (__DEFINES/icon_smoothing.dm:243), expanded inline
        // (rules 11.1 (d): its test reads the caller's smoothing_groups)
        if (Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(focus_only/sorted_smoothing_groups) is FALSE in production (rules 1.7)
        {
            // ASSERT_SORTED_SMOOTHING_GROUPS(smoothing_groups) (__DEFINES/icon_smoothing.dm:265)
            DmList unwrapped = json_decode(Dm.text("\\[[]0\\]", smoothing_groups)) as DmList;
            unwrapped.len--;
            assert_sorted(unwrapped, Dm.text("smoothing_groups ([])", type));
        }
        SET_SMOOTHING_GROUPS(smoothing_groups, ref this.smoothing_groups);
    }
}

// tg: code/__HELPERS/icon_smoothing.dm:369  (the global procs, rules 7.8)
public static partial class GlobalProcs
{
    /// Takes a direction, turns it into all the junctions that contain it
    // tg: code/__HELPERS/icon_smoothing.dm:369
    public static object dir_to_all_junctions(object dir = null)
    {
        float handback = NONE;
        if (Dm.truthy(Dm.bitand(dir, NORTH)))
            Dm.combine(ref handback, Dm.bitor(Dm.bitor(NORTH_JUNCTION, NORTHEAST_JUNCTION), NORTHWEST_JUNCTION));
        if (Dm.truthy(Dm.bitand(dir, SOUTH)))
            Dm.combine(ref handback, Dm.bitor(Dm.bitor(SOUTH_JUNCTION, SOUTHEAST_JUNCTION), SOUTHWEST_JUNCTION));
        if (Dm.truthy(Dm.bitand(dir, EAST)))
            Dm.combine(ref handback, Dm.bitor(Dm.bitor(EAST_JUNCTION, SOUTHEAST_JUNCTION), NORTHEAST_JUNCTION));
        if (Dm.truthy(Dm.bitand(dir, WEST)))
            Dm.combine(ref handback, Dm.bitor(Dm.bitor(WEST_JUNCTION, NORTHWEST_JUNCTION), SOUTHWEST_JUNCTION));
        return handback;
    }

    /// Takes a direction, turns it into all the junctions that it lines up with
    // tg: code/__HELPERS/icon_smoothing.dm:382
    public static object all_junctions_of_dir(object dir = null)
    {
        if (Dm.eq(dir, NORTH))
            return Dm.bitor(Dm.bitor(NORTH_JUNCTION, NORTHEAST_JUNCTION), NORTHWEST_JUNCTION);
        if (Dm.eq(dir, SOUTH))
            return Dm.bitor(Dm.bitor(SOUTH_JUNCTION, SOUTHEAST_JUNCTION), SOUTHWEST_JUNCTION);
        if (Dm.eq(dir, EAST))
            return Dm.bitor(Dm.bitor(EAST_JUNCTION, SOUTHEAST_JUNCTION), NORTHEAST_JUNCTION);
        if (Dm.eq(dir, WEST))
            return Dm.bitor(Dm.bitor(WEST_JUNCTION, NORTHWEST_JUNCTION), SOUTHWEST_JUNCTION);
        return NONE;
    }

    // tg: code/__HELPERS/icon_smoothing.dm:393
    public static object dir_to_junction(object dir = null)
    {
        object sw__1 = dir;
        if (Dm.eq(sw__1, NORTH))
            return NORTH_JUNCTION;
        else if (Dm.eq(sw__1, SOUTH))
            return SOUTH_JUNCTION;
        else if (Dm.eq(sw__1, WEST))
            return WEST_JUNCTION;
        else if (Dm.eq(sw__1, EAST))
            return EAST_JUNCTION;
        else if (Dm.eq(sw__1, NORTHWEST))
            return NORTHWEST_JUNCTION;
        else if (Dm.eq(sw__1, NORTHEAST))
            return NORTHEAST_JUNCTION;
        else if (Dm.eq(sw__1, SOUTHEAST))
            return SOUTHEAST_JUNCTION;
        else if (Dm.eq(sw__1, SOUTHWEST))
            return SOUTHWEST_JUNCTION;
        else
            return NONE;
    }

    // tg: code/__HELPERS/icon_smoothing.dm:414
    public static object reverse_junction(object junction = null)
    {
        float handback = NONE;
        if (Dm.truthy(Dm.bitand(junction, NORTH_JUNCTION)))
            Dm.combine(ref handback, SOUTH_JUNCTION);
        if (Dm.truthy(Dm.bitand(junction, SOUTH_JUNCTION)))
            Dm.combine(ref handback, NORTH_JUNCTION);
        if (Dm.truthy(Dm.bitand(junction, EAST_JUNCTION)))
            Dm.combine(ref handback, WEST_JUNCTION);
        if (Dm.truthy(Dm.bitand(junction, WEST_JUNCTION)))
            Dm.combine(ref handback, EAST_JUNCTION);
        if (Dm.truthy(Dm.bitand(junction, NORTHEAST_JUNCTION)))
            Dm.combine(ref handback, SOUTHWEST_JUNCTION);
        if (Dm.truthy(Dm.bitand(junction, SOUTHWEST_JUNCTION)))
            Dm.combine(ref handback, NORTHEAST_JUNCTION);
        if (Dm.truthy(Dm.bitand(junction, NORTHWEST_JUNCTION)))
            Dm.combine(ref handback, SOUTHEAST_JUNCTION);
        if (Dm.truthy(Dm.bitand(junction, SOUTHEAST_JUNCTION)))
            Dm.combine(ref handback, NORTHWEST_JUNCTION);
        return handback;
    }

    // tg: code/__HELPERS/icon_smoothing.dm:434
    public static object reverse_ndir(object ndir = null)
    {
        object sw__1 = ndir;
        if (Dm.eq(sw__1, NORTH_JUNCTION))
            return NORTH;
        else if (Dm.eq(sw__1, SOUTH_JUNCTION))
            return SOUTH;
        else if (Dm.eq(sw__1, WEST_JUNCTION))
            return WEST;
        else if (Dm.eq(sw__1, EAST_JUNCTION))
            return EAST;
        else if (Dm.eq(sw__1, NORTHWEST_JUNCTION))
            return NORTHWEST;
        else if (Dm.eq(sw__1, NORTHEAST_JUNCTION))
            return NORTHEAST;
        else if (Dm.eq(sw__1, SOUTHEAST_JUNCTION))
            return SOUTHEAST;
        else if (Dm.eq(sw__1, SOUTHWEST_JUNCTION))
            return SOUTHWEST;
        else if (Dm.eq(sw__1, Dm.bitor(NORTH_JUNCTION, WEST_JUNCTION)))
            return NORTHWEST;
        else if (Dm.eq(sw__1, Dm.bitor(NORTH_JUNCTION, EAST_JUNCTION)))
            return NORTHEAST;
        else if (Dm.eq(sw__1, Dm.bitor(SOUTH_JUNCTION, WEST_JUNCTION)))
            return SOUTHWEST;
        else if (Dm.eq(sw__1, Dm.bitor(SOUTH_JUNCTION, EAST_JUNCTION)))
            return SOUTHEAST;
        else if (Dm.eq(sw__1, Dm.bitor(Dm.bitor(NORTH_JUNCTION, WEST_JUNCTION), NORTHWEST_JUNCTION)))
            return NORTHWEST;
        else if (Dm.eq(sw__1, Dm.bitor(Dm.bitor(NORTH_JUNCTION, EAST_JUNCTION), NORTHEAST_JUNCTION)))
            return NORTHEAST;
        else if (Dm.eq(sw__1, Dm.bitor(Dm.bitor(SOUTH_JUNCTION, WEST_JUNCTION), SOUTHWEST_JUNCTION)))
            return SOUTHWEST;
        else if (Dm.eq(sw__1, Dm.bitor(Dm.bitor(SOUTH_JUNCTION, EAST_JUNCTION), SOUTHEAST_JUNCTION)))
            return SOUTHEAST;
        else
            return NONE;
    }
}
