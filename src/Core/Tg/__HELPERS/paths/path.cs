// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/paths/path.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The jump point search's entry and its datums (gaps package C1, the AI). Left as rows: get_swarm_path_to and get_sssp and the path maps (sssp.dm: only tg's
// admin path debugger calls them at this snapshot) and /datum/can_pass_info/compare_against, which only the path maps read.

public static partial class GlobalProcs
{
    /**
     * This is the proc you use whenever you want to have pathfinding more complex than "try stepping towards the thing".
     * If no path was found, returns an empty list, which is important for bots like medibots who expect an empty list rather than nothing.
     * It will yield until a path is returned, using magic
     *
     * Arguments:
     * * requester: The movable atom that's trying to find the path
     * * end: What we're trying to path to. It doesn't matter if this is a turf or some other atom, we're gonna just path to the turf it's on anyway
     * * max_distance: The maximum number of steps we can take in a given path to search (default: 30, 0 = infinite)
     * * mintargetdistance: Minimum distance to the target before path returns, could be used to get near a target, but not right to it - for an AI mob with a gun, for example.
     * * access: A list representing what access we have and what doors we can open.
     * * simulated_only: Whether we consider tur fs without atmos simulation (AKA do we want to ignore space)
     * * exclude: If we want to avoid a specific turf, like if we're a mulebot who already got blocked by some turf
     * * skip_first: Whether or not to delete the first item in the path. This would be done because the first item is the starting tile, which can break movement for some creatures.
     * * diagonal_handling: defines how we handle diagonal moves. see __DEFINES/path.dm
     */
    // tg: code/__HELPERS/paths/path.dm:17
    public static async DmTask<object> get_path_to(atom_movable requester = null, atom end = null, float? max_distance = null, object mintargetdist = null, object access = null, float? simulated_only = null, turf exclude = null, float? skip_first = null, float? diagonal_handling = null)
    {
        max_distance ??= 30;
        access ??= Dm.list();
        simulated_only ??= TRUE;
        skip_first ??= TRUE;
        diagonal_handling ??= DIAGONAL_REMOVE_CLUNKY;
        DmList hand_around = Dm.list();
        // We're guaranteed that list will be the first list in pathfinding_finished's argset because of how callback handles the arguments list
        DmList await_ = Dm.list(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(pathfinding_finished), hand_around));
        if (!Dm.truthy(SSpathfinder.pathfind(requester, end, max_distance, mintargetdist, access, simulated_only, exclude, skip_first, diagonal_handling, await_)))
            return Dm.list();

        while (!Dm.truthy(length(hand_around)))
            await stoplag();
        DmList return_val = hand_around[1] as DmList;
        if (return_val == null || QDELETED(requester) || QDELETED(end)) // It's trash, just hand back empty to make it easy
            return Dm.list();
        return return_val;
    }

    /// Uses funny pass by reference bullshit to take the output created by pathfinding, and insert it into a return list
    /// We'll be able to use this return list to tell a sleeping proc to continue execution
    // tg: code/__HELPERS/paths/path.dm:78
    public static void pathfinding_finished(DmList return_list = null, object hand_back = null)
    {
        // We use += here to behave nicely with lists
        Dm.append(ref return_list, LIST_VALUE_WRAP_LISTS(hand_back));
    }

    /**
     * Processes a path (list of turfs), removes any diagonal moves that would lead to a weird bump
     *
     * path - The path to process down
     * pass_info - Holds all the info about what this path attempt can go through
     * simulated_only - If we are not allowed to pass space turfs
     * avoid - A turf to be avoided
     */
    // tg: code/__HELPERS/paths/path.dm:154
    public static object remove_clunky_diagonals(DmList path = null, datum_can_pass_info pass_info = null, object simulated_only = null, turf avoid = null)
    {
        if (length(path) < 2)
            return path;
        DmList modified_path = Dm.list();

        for (float i = 1, i__end = length(path) - 1; i <= i__end; i++)
        {
            turf current_turf = path[i] as turf;
            Dm.append(ref modified_path, current_turf);
            turf next_turf = path[i + 1] as turf;
            float movement_dir = get_dir(current_turf, next_turf);
            if (!Dm.truthy(Dm.bitand(movement_dir, movement_dir - 1))) //cardinal movement, no need to verify
                continue;
            //If the first diagonal movement step is invalid (north/south), replace with a sidestep first, with an implied vertical step in next_turf
            float vertical_only = Dm.bitand(movement_dir, Dm.bitor(NORTH, SOUTH));
            if (!CAN_STEP(current_turf, get_step(current_turf, vertical_only), simulated_only, pass_info, avoid))
                Dm.append(ref modified_path, get_step(current_turf, Dm.bitand(movement_dir, Dm.bitnot(vertical_only))));
        }
        Dm.append(ref modified_path, path[length(path)]);

        return modified_path;
    }

    /**
     * Processes a path (list of turfs), removes any diagonal moves
     *
     * path - The path to process down
     * pass_info - Holds all the info about what this path attempt can go through
     * simulated_only - If we are not allowed to pass space turfs
     * avoid - A turf to be avoided
     */
    // tg: code/__HELPERS/paths/path.dm:182
    public static object remove_diagonals(DmList path = null, datum_can_pass_info pass_info = null, object simulated_only = null, turf avoid = null)
    {
        if (length(path) < 2)
            return path;
        DmList modified_path = Dm.list();

        for (float i = 1, i__end = length(path) - 1; i <= i__end; i++)
        {
            turf current_turf = path[i] as turf;
            Dm.append(ref modified_path, current_turf);
            turf next_turf = path[i + 1] as turf;
            float movement_dir = get_dir(current_turf, next_turf);
            if (!Dm.truthy(Dm.bitand(movement_dir, movement_dir - 1))) //cardinal movement, no need to verify
                continue;
            float vertical_only = Dm.bitand(movement_dir, Dm.bitor(NORTH, SOUTH));
            // If we can't go directly north/south, we will first go to the side,
            if (!CAN_STEP(current_turf, get_step(current_turf, vertical_only), simulated_only, pass_info, avoid))
                Dm.append(ref modified_path, get_step(current_turf, Dm.bitand(movement_dir, Dm.bitnot(vertical_only))));
            else // Otherwise, we'll first go north/south, then to the side
                Dm.append(ref modified_path, get_step(current_turf, vertical_only));
        }
        Dm.append(ref modified_path, path[length(path)]);

        return modified_path;
    }

    /**
     * A helper macro to see if it's possible to step from the first turf into the second one, minding things like door access and directional windows.
     * If you really want to optimize things, optimize this, cuz this gets called a lot.
     * We do early next.density check despite it being already checked in LinkBlockedWithAccess for short-circuit performance
     */
    // tg: code/__DEFINES/path.dm:12
    public static bool CAN_STEP(turf cur_turf, turf next, object simulated_only, datum_can_pass_info pass_info, turf avoid)
        => next != null && !Dm.truthy(next.density) && !(Dm.truthy(simulated_only) && Dm.truthy(datum_controller_subsystem_pathfinder.space_type_cache[next.type])) && next != avoid && !Dm.truthy(cur_turf.LinkBlockedWithAccess(next, pass_info));
}

/// The datum used to handle the JPS pathfinding, completely self-contained
// tg: code/__HELPERS/paths/path.dm:83
public partial class datum_pathfind : datum
{
    /// The turf we started at (a turf, by tg's declaration; the generator read the var's name)
    // tg: code/__HELPERS/paths/path.dm:85
    public turf start;

    // tg: code/__HELPERS/paths/path.dm:100
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        Dm.remove(ref SSpathfinder.active_pathing, this);
        Dm.remove(ref SSpathfinder.currentrun, this);
        Waiting.NoSleep(hand_back(null));
        avoid = null;
        return dot;
    }

    /**
     * "starts" off the pathfinding, by storing the values this datum will need to work later on
     *  returns FALSE if it fails to setup properly, TRUE otherwise
     */
    // tg: code/__HELPERS/paths/path.dm:111
    public virtual object start__proc()
    {
        if (start == null)
        {
            stack_trace("Invalid pathfinding start", "code/__HELPERS/paths/path.dm", 113);
            return FALSE;
        }
        return TRUE;
    }

    /**
     * search_step() is the workhorse of pathfinding. It'll do the searching logic, and will slowly build up a path
     * returns TRUE if everything is stable, FALSE if the pathfinding logic has failed, and we need to abort
     */
    // tg: code/__HELPERS/paths/path.dm:121
    public virtual async DmTask<object> search_step()
    {
        return TRUE;
    }

    /**
     * early_exit() is called when something goes wrong in processing, and we need to halt the pathfinding NOW
     */
    // tg: code/__HELPERS/paths/path.dm:127
    public virtual async DmTask early_exit()
    {
        await hand_back(null);
        qdel(this);
    }

    /**
     * Cleanup pass for the pathfinder. This tidies up the path, and fufills the pathfind's obligations
     */
    // tg: code/__HELPERS/paths/path.dm:134
    public virtual async DmTask finished()
    {
        qdel(this);
    }

    /**
     * Call to return a value to whoever spawned this pathfinding work
     * Will fail if it's already been called
     */
    // tg: code/__HELPERS/paths/path.dm:141
    public virtual async DmTask hand_back(object value = null)
    {
        foreach (datum_callback finished in Dm.each_cast<datum_callback>(on_finish))
            await finished.Invoke(value);
        on_finish = null;
    }
}

public partial class turf : atom
{
    // tg: code/__HELPERS/paths/path.dm:241  var/static/list/directional_blocker_cache (rules 10.6: a TgState field)
    private static ref DmList directional_blocker_cache => ref TgState.Current.turf__directional_blocker_cache;

    /**
     * For seeing if we can actually move between 2 given turfs while accounting for our access and the requester's pass_flags
     *
     * Assumes destinantion turf is non-dense - check and shortcircuit in code invoking this proc to avoid overhead.
     * Makes some other assumptions, such as assuming that unless declared, non dense objects will not block movement.
     * It's fragile, but this is VERY much the most expensive part of pathing, so it'd better be fast
     *
     * Arguments:
     * * destination_turf - Where are we going from where we are?
     * * pass_info - Holds all the info about what this path attempt can go through
    */
    // tg: code/__HELPERS/paths/path.dm:215
    public virtual object LinkBlockedWithAccess(turf destination_turf = null, datum_can_pass_info pass_info = null)
    {
        if (destination_turf.x != x && destination_turf.y != y) //diagonal
        {
            float in_dir = get_dir(destination_turf, this); // eg. northwest (1+8) = 9 (00001001)
            float first_step_direction_a = Dm.bitand(in_dir, 3); // eg. north   (1+8)&3 (0000 0011) = 1 (0000 0001)
            float first_step_direction_b = Dm.bitand(in_dir, 12); // eg. west   (1+8)&12 (0000 1100) = 8 (0000 1000)

            foreach (object first_step_direction in Dm.each(Dm.list(first_step_direction_a, first_step_direction_b)))
            {
                turf midstep_turf = get_step(destination_turf, Dm.num(first_step_direction));
                bool way_blocked = Dm.truthy(midstep_turf.density) || Dm.truthy(LinkBlockedWithAccess(midstep_turf, pass_info)) || Dm.truthy(midstep_turf.LinkBlockedWithAccess(destination_turf, pass_info));
                if (!way_blocked)
                    return FALSE;
            }
            return TRUE;
        }
        float actual_dir = get_dir(this, destination_turf);

        /// These are generally cheaper than looping contents so they go first
        // This is already assumed to be true
        //if(TURF_PATHING_PASS_DENSITY)
        //	if(destination_turf.density)
        //		return TRUE
        if (Dm.eq(destination_turf.pathing_pass_method, TURF_PATHING_PASS_PROC))
        {
            if (!Dm.truthy(destination_turf.CanAStarPass(actual_dir, pass_info)))
                return TRUE;
        }
        else if (Dm.eq(destination_turf.pathing_pass_method, TURF_PATHING_PASS_NO))
            return TRUE;

        directional_blocker_cache ??= typecacheof(Dm.list(typeof(obj_structure_window), typeof(obj_machinery_door_window), typeof(obj_structure_railing), typeof(obj_machinery_door_firedoor_border_only))) as DmList;
        DmList blockers = directional_blocker_cache;
        // Source border object checks
        foreach (obj border in Dm.each<obj>(this))
        {
            if (!Dm.truthy(blockers[border.type]))
                continue;
            if (!Dm.truthy(border.density) && Dm.eq(border.can_astar_pass, CANASTARPASS_DENSITY))
                continue;
            if (!Dm.truthy(border.CanAStarPass(actual_dir, pass_info)))
                return TRUE;
        }

        // Destination blockers check
        float reverse_dir = get_dir(destination_turf, this);
        foreach (obj iter_object in Dm.each<obj>(destination_turf))
        {
            // This is an optimization because of the massive call count of this code
            if (!Dm.truthy(iter_object.density) && Dm.eq(iter_object.can_astar_pass, CANASTARPASS_DENSITY))
                continue;
            if (!Dm.truthy(iter_object.CanAStarPass(reverse_dir, pass_info)))
                return TRUE;
        }
        return FALSE;
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/paths/path.dm:241
    internal DmList turf__directional_blocker_cache;
}

// Could easily be a struct if/when we get that
/**
 * Holds all information about what an atom can move through
 * Passed into CanAStarPass to provide context for a pathing attempt
 *
 * Also used to check if using a cached path_map is safe
 * There are some vars here that are unused. They exist to cover cases where requester_ref is used
 * They're the properties of requester_ref used in those cases.
 * It's kinda annoying, but there's some proc chains we can't convert to this datum
 */
// tg: code/__HELPERS/paths/path.dm:271
[DmPath("/datum/can_pass_info")]
public partial class datum_can_pass_info : datum
{
    /// What we can pass through. Mirrors /atom/movable/pass_flags
    // tg: code/__HELPERS/paths/path.dm:276
    public float pass_flags = NONE;

    // tg: code/__HELPERS/paths/path.dm:319
    [DmParams("construct_from", "access", "no_id", "call_depth")]
    public override async DmTask New(params object[] __extra)
    {
        atom_movable construct_from = Dm.arg<atom_movable>(__extra, 0);
        DmList access = Dm.arg<DmList>(__extra, 1);
        float? no_id = Dm.arg<float?>(__extra, 2);
        float? call_depth = Dm.arg<float?>(__extra, 3);
        no_id ??= FALSE;
        call_depth ??= 0;
        // No infiniloops
        if (Dm.num(call_depth) > 10)
            return;
        if (Dm.truthy(access))
            this.access = access.Copy();
        this.no_id = Dm.num(no_id);

        if (construct_from == null)
            return;

        requester_ref = WEAKREF(construct_from) as datum_weakref;
        pass_flags = Dm.num(construct_from.pass_flags);
        movement_type = Dm.num(construct_from.movement_type);
        thrown = Dm.num(Dm.truthy(construct_from.throwing));
        anchored = Dm.num(construct_from.anchored);
        has_gravity = Dm.num(Dm.truthy(construct_from.has_gravity()));
        if (ismob(construct_from))
        {
            mob mob_construct = (mob)construct_from;
            incapacitated = Dm.num(mob_construct.incapacitated);
            if (Dm.truthy(mob_construct.buckled))
                buckled_info = Dm.@new<datum_can_pass_info>(mob_construct.buckled, access, no_id, Dm.num(call_depth) + 1);
        }
        if (isobserver(construct_from))
            is_observer = TRUE;
        if (isliving(construct_from))
        {
            mob_living living_construct = (mob_living)construct_from;
            is_living = TRUE;
            can_ventcrawl = Dm.num(HAS_TRAIT(living_construct, TRAIT_VENTCRAWLER_ALWAYS) || HAS_TRAIT(living_construct, TRAIT_VENTCRAWLER_NUDE));
            mob_size = living_construct.mob_size;
            incorporeal_move = Dm.num(living_construct.incorporeal_move);
        }
        if (iseyemob(construct_from))
            camera_type = construct_from.type;
        is_bot = Dm.num(isbot(construct_from));

        if (Dm.truthy(construct_from.pulling))
            pulling_info = Dm.@new<datum_can_pass_info>(construct_from.pulling, access, no_id, Dm.num(call_depth) + 1);
    }
}
