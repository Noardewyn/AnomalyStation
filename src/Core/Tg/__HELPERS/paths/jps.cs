// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/paths/jps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1, the AI).

/**
 * This file contains the stuff you need for using JPS (Jump Point Search) pathing, an alternative to A* that skips
 * over large numbers of uninteresting tiles resulting in much quicker pathfinding solutions. Mind that diagonals
 * cost the same as cardinal moves currently, so paths may look a bit strange, but should still be optimal.
 */

/// The JPS Node datum represents a turf that we find interesting enough to add to the open list and possibly search for new tiles from
// tg: code/__HELPERS/paths/jps.dm:16
public partial class datum_jps_node : datum
{
    // tg: code/__HELPERS/paths/jps.dm:32
    [DmParams("our_tile", "incoming_previous_node", "jumps_taken", "incoming_goal")]
    public override async DmTask New(params object[] __extra)
    {
        turf our_tile = Dm.arg<turf>(__extra, 0);
        datum_jps_node incoming_previous_node = Dm.arg<datum_jps_node>(__extra, 1);
        object jumps_taken = Dm.arg(__extra, 2);
        turf incoming_goal = Dm.arg<turf>(__extra, 3);
        tile = our_tile;
        jumps = jumps_taken;
        if (incoming_goal != null) // if we have the goal argument, this must be the first/starting node
            node_goal = incoming_goal;
        else if (incoming_previous_node != null) // if we have the parent, this is from a direct lateral/diagonal scan, we can fill it all out now
        {
            previous_node = incoming_previous_node;
            number_tiles = Dm.num(previous_node.number_tiles) + Dm.num(jumps);
            node_goal = previous_node.node_goal;
            heuristic = get_dist_euclidean(tile, node_goal);
            f_value = Dm.num(number_tiles) + Dm.num(heuristic);
        }
        // otherwise, no parent node means this is from a subscan lateral scan, so we just need the tile for now until we call [datum/jps/proc/update_parent] on it
    }

    // tg: code/__HELPERS/paths/jps.dm:45
    public override object Destroy(float? force = null)
    {
        previous_node = null;
        return base.Destroy(force);
    }

    // tg: code/__HELPERS/paths/jps.dm:49
    public virtual void update_parent(datum_jps_node new_parent = null)
    {
        previous_node = new_parent;
        node_goal = previous_node.node_goal;
        jumps = get_dist(tile, previous_node.tile);
        number_tiles = Dm.num(previous_node.number_tiles) + Dm.num(jumps);
        heuristic = get_dist_euclidean(tile, node_goal);
        f_value = Dm.num(number_tiles) + Dm.num(heuristic);
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/paths/jps.dm:57
    public static object HeapPathWeightCompare(datum_jps_node a = null, datum_jps_node b = null)
    {
        return Dm.num(b.f_value) - Dm.num(a.f_value);
    }
}

// tg: code/__HELPERS/paths/jps.dm:60
public partial class datum_pathfind_jps : datum_pathfind
{
    /// A helper macro for JPS, for telling when a node has forced neighbors that need expanding
    /// Only usable in the context of the jps datum because of the datum vars it relies on
    /// Checks if we are deviating from our "running" directions
    // tg: code/__HELPERS/paths/jps.dm:10
    private bool STEP_NOT_HERE_BUT_THERE(turf cur_turf, float dirA, float dirB)
        => !CAN_STEP(cur_turf, get_step(cur_turf, dirA), simulated_only, pass_info, avoid) && CAN_STEP(cur_turf, get_step(cur_turf, dirB), simulated_only, pass_info, avoid);
    /// Checks if a border object stops our parent from reaching a turf we CAN reach
    // tg: code/__HELPERS/paths/jps.dm:12
    private bool TURF_CANT_WE_CAN(turf parent_turf, float dir_parent, turf cur_turf, float dur_cur)
        => !CAN_STEP(parent_turf, get_step(parent_turf, dir_parent), simulated_only, pass_info, avoid) && CAN_STEP(cur_turf, get_step(cur_turf, dur_cur), simulated_only, pass_info, avoid);

    // tg: code/__HELPERS/paths/jps.dm:79
    public virtual void setup(atom_movable requester = null, DmList access = null, object max_distance = null, object simulated_only = null, object avoid = null, DmList on_finish = null, atom goal = null, object mintargetdist = null, object skip_first = null, object diagonal_handling = null)
    {
        this.requester = requester;
        pass_info = Dm.@new<datum_can_pass_info>(requester, access);
        this.max_distance = Dm.num(max_distance);
        this.simulated_only = simulated_only;
        this.avoid = avoid as turf;
        this.on_finish = on_finish;
        this.mintargetdist = Dm.num(mintargetdist);
        this.skip_first = Dm.num(skip_first);
        this.diagonal_handling = Dm.num(diagonal_handling);
        end = get_turf(goal);
        open = Dm.@new<datum_heap>("/proc/" + nameof(GlobalProcs.HeapPathWeightCompare));
        found_turfs = Dm.list();
    }

    // tg: code/__HELPERS/paths/jps.dm:93
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        requester = null;
        end = null;
        open = null;
        return dot;
    }

    // tg: code/__HELPERS/paths/jps.dm:99
    public override object start__proc()
    {
        start = start ?? get_turf(requester);
        object dot = base.start__proc();
        if (!Dm.truthy(dot))
            return dot;

        if (get_turf(end) == null)
        {
            stack_trace("Invalid JPS destination", "code/__HELPERS/paths/jps.dm", 106);
            return FALSE;
        }
        if (start.z != end.z || start == end) //no pathfinding between z levels
            return FALSE;
        if (Dm.truthy(max_distance) && max_distance < get_dist(start, end)) //if start turf is farther than max_distance from end turf, no need to do anything
            return FALSE;

        datum_jps_node current_processed_node = Dm.@new<datum_jps_node>(start, -1, 0, end);
        open.insert(current_processed_node);
        found_turfs[start] = TRUE; // i'm sure this is fine
        return TRUE;
    }

    // tg: code/__HELPERS/paths/jps.dm:118
    public override async DmTask<object> search_step()
    {
        object dot = await base.search_step();
        if (!Dm.truthy(dot))
            return dot;
        if (QDELETED(requester))
            return FALSE;

        while (!Dm.truthy(open.is_empty()) && path == null)
        {
            datum_jps_node current_processed_node = open.pop() as datum_jps_node; //get the lower f_value turf in the open list
            if (Dm.truthy(max_distance) && Dm.num(current_processed_node.number_tiles) > max_distance) //if too many steps, don't process that path
                continue;

            turf current_turf = current_processed_node.tile;
            foreach (object scan_direction in Dm.each(Dm.list(EAST, WEST, NORTH, SOUTH)))
                await lateral_scan_spec(current_turf, scan_direction, current_processed_node);

            foreach (object scan_direction in Dm.each(Dm.list(NORTHEAST, SOUTHEAST, NORTHWEST, SOUTHWEST)))
                await diag_scan_spec(current_turf, scan_direction, current_processed_node);

            // Stable, we'll just be back later
            if (TICK_CHECK())
                return TRUE;
        }
        return TRUE;
    }

    // tg: code/__HELPERS/paths/jps.dm:141
    public override async DmTask finished()
    {
        //we're done! turn our reversed path (end to start) into a path (start to end)
        found_turfs = null;
        QDEL_NULL(ref open);

        DmList path = this.path ?? Dm.list();
        path = reverseList(path) as DmList;
        object sw = diagonal_handling;
        if (Dm.eq(sw, DIAGONAL_REMOVE_CLUNKY))
            path = remove_clunky_diagonals(path, pass_info, simulated_only, avoid) as DmList;
        else if (Dm.eq(sw, DIAGONAL_REMOVE_ALL))
            path = remove_diagonals(path, pass_info, simulated_only, avoid) as DmList;
        if (Dm.truthy(skip_first) && length(path) > 0)
            path.Cut(1, 2);
        await hand_back(path);
        await base.finished();
    }

    /// Called when we've hit the goal with the node that represents the last tile, then sets the path var to that path so it can be returned by [datum/pathfind/proc/search]
    // tg: code/__HELPERS/paths/jps.dm:158
    public virtual void unwind_path(datum_jps_node unwind_node = null)
    {
        path = Dm.list();
        turf iter_turf = unwind_node.tile;
        path.Add(iter_turf);

        while (unwind_node.previous_node != null)
        {
            float dir_goal = get_dir(iter_turf, unwind_node.previous_node.tile);
            for (float i = 1, i__end = Dm.num(unwind_node.jumps); i <= i__end; i++)
            {
                iter_turf = get_step(iter_turf, dir_goal);
                path.Add(iter_turf);
            }
            unwind_node = unwind_node.previous_node;
        }
    }

    /**
     * For performing lateral scans from a given starting turf.
     *
     * These scans are called from both the main search loop, as well as subscans for diagonal scans, and they treat finding interesting turfs slightly differently.
     * If we're doing a normal lateral scan, we already have a parent node supplied, so we just create the new node and immediately insert it into the heap, ezpz.
     * If we're part of a subscan, we still need for the diagonal scan to generate a parent node, so we return a node datum with just the turf and let the diag scan
     * proc handle transferring the values and inserting them into the heap.
     *
     * Arguments:
     * * original_turf: What turf did we start this scan at?
     * * heading: What direction are we going in? Obviously, should be cardinal
     * * parent_node: Only given for normal lateral scans, if we don't have one, we're a diagonal subscan.
    */
    // tg: code/__HELPERS/paths/jps.dm:184
    public virtual async DmTask<object> lateral_scan_spec(turf original_turf = null, object heading = null, datum_jps_node parent_node = null)
    {
        float steps_taken = 0;

        turf current_turf = original_turf;
        turf lag_turf = original_turf;
        datum_can_pass_info pass_info = this.pass_info;
        float head = Dm.num(heading);

        while (true)
        {
            if (path != null)
                return null;
            lag_turf = current_turf;
            current_turf = get_step(current_turf, head);
            steps_taken++;
            if (!CAN_STEP(lag_turf, current_turf, simulated_only, pass_info, avoid))
                return null;

            if (current_turf == end || (Dm.truthy(mintargetdist) && get_dist(current_turf, end) <= mintargetdist && !Dm.truthy(await diagonally_blocked(current_turf, end))))
            {
                datum_jps_node final_node = Dm.@new<datum_jps_node>(current_turf, parent_node, steps_taken);
                found_turfs[current_turf] = TRUE;
                if (parent_node != null) // if this is a direct lateral scan we can wrap up, if it's a subscan from a diag, we need to let the diag make their node first, then finish
                    unwind_path(final_node);
                return final_node;
            }
            else if (Dm.truthy(found_turfs[current_turf])) // already visited, essentially in the closed list
                return null;
            else
                found_turfs[current_turf] = TRUE;

            if (parent_node != null && Dm.num(parent_node.number_tiles) + steps_taken > max_distance)
                return null;

            bool interesting = false; // have we found a forced neighbor that would make us add this turf to the open list?

            if (head == NORTH)
            {
                if (STEP_NOT_HERE_BUT_THERE(current_turf, WEST, NORTHWEST) || STEP_NOT_HERE_BUT_THERE(current_turf, EAST, NORTHEAST)
                    || TURF_CANT_WE_CAN(get_step(current_turf, EAST), NORTH, current_turf, NORTHEAST) || TURF_CANT_WE_CAN(get_step(current_turf, WEST), NORTH, current_turf, NORTHWEST))
                    interesting = true;
            }
            else if (head == SOUTH)
            {
                if (STEP_NOT_HERE_BUT_THERE(current_turf, WEST, SOUTHWEST) || STEP_NOT_HERE_BUT_THERE(current_turf, EAST, SOUTHEAST)
                    || TURF_CANT_WE_CAN(get_step(current_turf, EAST), SOUTH, current_turf, SOUTHEAST) || TURF_CANT_WE_CAN(get_step(current_turf, WEST), SOUTH, current_turf, SOUTHWEST))
                    interesting = true;
            }
            else if (head == EAST)
            {
                if (STEP_NOT_HERE_BUT_THERE(current_turf, NORTH, NORTHEAST) || STEP_NOT_HERE_BUT_THERE(current_turf, SOUTH, SOUTHEAST)
                    || TURF_CANT_WE_CAN(get_step(current_turf, SOUTH), EAST, current_turf, SOUTHEAST) || TURF_CANT_WE_CAN(get_step(current_turf, NORTH), EAST, current_turf, NORTHEAST))
                    interesting = true;
            }
            else if (head == WEST)
            {
                if (STEP_NOT_HERE_BUT_THERE(current_turf, NORTH, NORTHWEST) || STEP_NOT_HERE_BUT_THERE(current_turf, SOUTH, SOUTHWEST)
                    || TURF_CANT_WE_CAN(get_step(current_turf, SOUTH), WEST, current_turf, SOUTHWEST) || TURF_CANT_WE_CAN(get_step(current_turf, NORTH), WEST, current_turf, NORTHWEST))
                    interesting = true;
            }

            if (interesting)
            {
                datum_jps_node newnode = Dm.@new<datum_jps_node>(current_turf, parent_node, steps_taken);
                if (parent_node != null) // if we're a diagonal subscan, we'll handle adding ourselves to the heap in the diag
                    open.insert(newnode);
                return newnode;
            }
        }
    }

    /**
     * For performing diagonal scans from a given starting turf.
     *
     * Unlike lateral scans, these only are called from the main search loop, so we don't need to worry about returning anything,
     * though we do need to handle the return values of our lateral subscans of course.
     *
     * Arguments:
     * * original_turf: What turf did we start this scan at?
     * * heading: What direction are we going in? Obviously, should be diagonal
     * * parent_node: We should always have a parent node for diagonals
    */
    // tg: code/__HELPERS/paths/jps.dm:258
    public virtual async DmTask diag_scan_spec(turf original_turf = null, object heading = null, datum_jps_node parent_node = null)
    {
        float steps_taken = 0;
        turf current_turf = original_turf;
        turf lag_turf = original_turf;
        datum_can_pass_info pass_info = this.pass_info;
        float head = Dm.num(heading);

        while (true)
        {
            if (path != null)
                return;
            lag_turf = current_turf;
            current_turf = get_step(current_turf, head);
            steps_taken++;
            if (!CAN_STEP(lag_turf, current_turf, simulated_only, pass_info, avoid))
                return;

            if (current_turf == end || (Dm.truthy(mintargetdist) && get_dist(current_turf, end) <= mintargetdist && !Dm.truthy(await diagonally_blocked(current_turf, end))))
            {
                datum_jps_node final_node = Dm.@new<datum_jps_node>(current_turf, parent_node, steps_taken);
                found_turfs[current_turf] = TRUE;
                unwind_path(final_node);
                return;
            }
            else if (Dm.truthy(found_turfs[current_turf])) // already visited, essentially in the closed list
                return;
            else
                found_turfs[current_turf] = TRUE;

            if (Dm.num(parent_node.number_tiles) + steps_taken > max_distance)
                return;

            bool interesting = false; // have we found a forced neighbor that would make us add this turf to the open list?
            datum_jps_node possible_child_node = null; // otherwise, did one of our lateral subscans turn up something?

            if (head == NORTHWEST)
            {
                if (STEP_NOT_HERE_BUT_THERE(current_turf, EAST, NORTHEAST) || STEP_NOT_HERE_BUT_THERE(current_turf, SOUTH, SOUTHWEST)
                    || TURF_CANT_WE_CAN(lag_turf, NORTH, current_turf, EAST) || TURF_CANT_WE_CAN(lag_turf, WEST, current_turf, SOUTH))
                    interesting = true;
                else
                    possible_child_node = (await lateral_scan_spec(current_turf, WEST) as datum_jps_node) ?? (await lateral_scan_spec(current_turf, NORTH) as datum_jps_node);
            }
            else if (head == NORTHEAST)
            {
                if (STEP_NOT_HERE_BUT_THERE(current_turf, WEST, NORTHWEST) || STEP_NOT_HERE_BUT_THERE(current_turf, SOUTH, SOUTHEAST)
                    || TURF_CANT_WE_CAN(lag_turf, NORTH, current_turf, WEST) || TURF_CANT_WE_CAN(lag_turf, EAST, current_turf, SOUTH))
                    interesting = true;
                else
                    possible_child_node = (await lateral_scan_spec(current_turf, EAST) as datum_jps_node) ?? (await lateral_scan_spec(current_turf, NORTH) as datum_jps_node);
            }
            else if (head == SOUTHWEST)
            {
                if (STEP_NOT_HERE_BUT_THERE(current_turf, EAST, SOUTHEAST) || STEP_NOT_HERE_BUT_THERE(current_turf, NORTH, NORTHWEST)
                    || TURF_CANT_WE_CAN(lag_turf, SOUTH, current_turf, EAST) || TURF_CANT_WE_CAN(lag_turf, WEST, current_turf, NORTH))
                    interesting = true;
                else
                    possible_child_node = (await lateral_scan_spec(current_turf, SOUTH) as datum_jps_node) ?? (await lateral_scan_spec(current_turf, WEST) as datum_jps_node);
            }
            else if (head == SOUTHEAST)
            {
                if (STEP_NOT_HERE_BUT_THERE(current_turf, WEST, SOUTHWEST) || STEP_NOT_HERE_BUT_THERE(current_turf, NORTH, NORTHEAST)
                    || TURF_CANT_WE_CAN(lag_turf, SOUTH, current_turf, WEST) || TURF_CANT_WE_CAN(lag_turf, EAST, current_turf, NORTH))
                    interesting = true;
                else
                    possible_child_node = (await lateral_scan_spec(current_turf, SOUTH) as datum_jps_node) ?? (await lateral_scan_spec(current_turf, EAST) as datum_jps_node);
            }

            if (interesting || possible_child_node != null)
            {
                datum_jps_node newnode = Dm.@new<datum_jps_node>(current_turf, parent_node, steps_taken);
                open.insert(newnode);
                if (possible_child_node != null)
                {
                    possible_child_node.update_parent(newnode);
                    open.insert(possible_child_node);
                    if (possible_child_node.tile == end || (Dm.truthy(mintargetdist) && get_dist(possible_child_node.tile, end) <= mintargetdist))
                        unwind_path(possible_child_node);
                }
                return;
            }
        }
    }
}
