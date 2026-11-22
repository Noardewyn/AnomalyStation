// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/_ai_bt_composites.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The var block is generated.

/**
 * Base composite node. Holds an ordered list of child bt_node instances.
 * */
// tg: code/datums/ai/_ai_bt_composites.dm:4
public partial class datum_bt_node_composite : datum_bt_node
{
    // tg: code/datums/ai/_ai_bt_composites.dm:10
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(children);
        return base.Destroy(force);
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:14
    public override object get_children()
    {
        return children;
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:17
    public override object has_active_descendants()
    {
        if (!Dm.truthy(children))
            return FALSE;
        foreach (datum_bt_node child in Dm.each_cast<datum_bt_node>(children))
        {
            if (Dm.truthy(child.has_active_descendants()))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:25
    public override void finalize_node(datum_ai_controller controller = null, DmList to_visit = null)
    {
        base.finalize_node(controller, to_visit);
        if (!Dm.truthy(children))
            return;
        foreach (datum_bt_node child in Dm.each_cast<datum_bt_node>(children))
            child.parent_node = this;
        Dm.append(ref to_visit, children);
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:33
    public override void set_descriptor_children(DmList children_descs = null, datum_ai_controller controller = null)
    {
        DmList resolved = Dm.list();
        foreach (object child_entry in Dm.each(children_descs))
        {
            datum_bt_node child_node = controller.get_or_build_node(child_entry) as datum_bt_node;
            if (child_node != null)
                Dm.append(ref resolved, child_node);
        }
        children = resolved;
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:41
    public override void collect_reset_children(DmList to_visit = null)
    {
        if (Dm.truthy(children))
            Dm.append(ref to_visit, children);
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:45
    public override object assign_execution_indices(object counter = null)
    {
        execution_index = Dm.num(counter);
        float c = Dm.num(counter) + 1;
        foreach (datum_bt_node child in Dm.each<datum_bt_node>(children))
            c = Dm.num(child.assign_execution_indices(c));
        last_execution_index = c - 1;
        return c;
    }
}

/**
 * Sequence node: ticks children in order.
 * Returns BT_FAILURE on first child failure.
 * Returns BT_RUNNING on first child returning BT_RUNNING (stops further evaluation).
 * Returns BT_SUCCESS only if all children succeed.
 *
 * Resumes from the last RUNNING child index rather than restarting from child 1.
 */
// tg: code/datums/ai/_ai_bt_composites.dm:61
public partial class datum_bt_node_composite_sequence : datum_bt_node_composite
{
    // tg: code/datums/ai/_ai_bt_composites.dm:67
    public override object tick(datum_ai_controller controller = null, object seconds_per_tick = null)
    {
        float start = Dm.truthy(running_child_index) ? running_child_index : 1;
        for (float i = start, i__end = length(children); i <= i__end; i++)
        {
            datum_bt_node child = children[i] as datum_bt_node;
            float child_result = Dm.num(child.tick(controller, seconds_per_tick));
            if (Dm.truthy(controller.cancelled_during_tick))
                return BT_FAILURE;
            if (child_result != BT_SUCCESS)
            {
                if (child_result == BT_RUNNING)
                    running_child_index = i;
                else
                    running_child_index = 0;
                return child_result;
            }
        }

        running_child_index = 0;
        return BT_SUCCESS;
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:86
    public override async DmTask reset_tick_state()
    {
        await base.reset_tick_state();
        running_child_index = 0;
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:90
    public override void append_active_nodes(DmList lines = null, object indent = null)
    {
        bool found_active = false;
        foreach (datum_bt_node child in Dm.each_cast<datum_bt_node>(children))
        {
            if (found_active)
                Dm.append(ref lines, Dm.text("[]↑ []", indent, child.label));
            else if (Dm.truthy(child.has_active_descendants()))
            {
                found_active = true;
                child.append_active_nodes(lines, indent);
            }
        }
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:99
    public override void append_full_tree_state(DmList lines = null, object indent = null)
    {
        string child_info = Dm.truthy(running_child_index) ? Dm.text(" (child []/[])", running_child_index, length(children)) : "";
        Dm.append(ref lines, Dm.text("[][] SEQUENCE[]", indent, get_status_marker(), child_info));
        foreach (datum_bt_node child in Dm.each_cast<datum_bt_node>(children))
            child.append_full_tree_state(lines, Dm.text("[]  ", indent));
    }
}

/**
 * Selector node: ticks children in order.
 * Returns the first non-BT_FAILURE result (BT_SUCCESS or BT_RUNNING), stopping further evaluation.
 * Returns BT_FAILURE only if all children fail.
 *
 * Resumes from the last RUNNING child index rather than restarting from child 1.

 */
// tg: code/datums/ai/_ai_bt_composites.dm:113
public partial class datum_bt_node_composite_selector : datum_bt_node_composite
{
    // tg: code/datums/ai/_ai_bt_composites.dm:119
    public override object tick(datum_ai_controller controller = null, object seconds_per_tick = null)
    {
        float start = Dm.truthy(running_child_index) ? running_child_index : 1;
        for (float i = start, i__end = length(children); i <= i__end; i++)
        {
            datum_bt_node child = children[i] as datum_bt_node;
            float child_result = Dm.num(child.tick(controller, seconds_per_tick));
            if (Dm.truthy(controller.cancelled_during_tick))
                return BT_FAILURE;
            if (child_result != BT_FAILURE)
            {
                if (child_result == BT_RUNNING)
                    running_child_index = i;
                else
                    running_child_index = 0;
                return child_result;
            }
        }

        running_child_index = 0;
        return BT_FAILURE;
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:138
    public override async DmTask reset_tick_state()
    {
        await base.reset_tick_state();
        running_child_index = 0;
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:142
    public override void append_active_nodes(DmList lines = null, object indent = null)
    {
        foreach (datum_bt_node child in Dm.each_cast<datum_bt_node>(children))
        {
            if (Dm.truthy(child.has_active_descendants()))
            {
                child.append_active_nodes(lines, indent);
                return;
            }
        }
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:148
    public override void append_full_tree_state(DmList lines = null, object indent = null)
    {
        string child_info = Dm.truthy(running_child_index) ? Dm.text(" (child []/[])", running_child_index, length(children)) : "";
        Dm.append(ref lines, Dm.text("[][] SELECTOR[]", indent, get_status_marker(), child_info));
        foreach (datum_bt_node child in Dm.each_cast<datum_bt_node>(children))
            child.append_full_tree_state(lines, Dm.text("[]  ", indent));
    }
}

/**
 * Subplan node: Runs child and applies configurable restart policies
 * when the child completes instead of propagating completion directly.
 *
 * success_policy:
 *   BT_SUBPLAN_SUCCEED_ON_SUCCESS (default)  propagates BT_SUCCESS when all children succeed.
 *   BT_SUBPLAN_LOOP_ON_SUCCESS               resets all children and returns BT_RUNNING, restarting next tick.
 *
 * failure_policy:
 *   BT_SUBPLAN_FAIL_ON_FAILURE (default)  propagates BT_FAILURE when a child fails.
 *   BT_SUBPLAN_LOOP_ON_FAILURE            resets all children and returns BT_RUNNING, restarting next tick.
 *
 * Combining both loop policies creates an infinite loop that only exits via an external observer abort or cancel_current_plan(), so be careful pls
 */
// tg: code/datums/ai/_ai_bt_composites.dm:168
public partial class datum_bt_node_composite_subplan : datum_bt_node_composite
{
    // tg: code/datums/ai/_ai_bt_composites.dm:179
    public override object tick(datum_ai_controller controller = null, object seconds_per_tick = null)
    {
        if (loop_delay > 0 && next_loop_time > world.time)
            return BT_RUNNING;

        datum_bt_node child = LAZYACCESS(children, 1) as datum_bt_node;
        if (child == null)
        {
            next_loop_time = 0;
            return BT_FAILURE;
        }

        float child_result = Dm.num(child.tick(controller, seconds_per_tick));
        if (Dm.truthy(controller.cancelled_during_tick))
            return BT_FAILURE;

        if (child_result == BT_RUNNING)
            return BT_RUNNING;

        if (child_result == BT_FAILURE)
        {
            if (failure_policy == BT_SUBPLAN_LOOP_ON_FAILURE)
            {
                Waiting.NoSleep(child.reset_tick_state());
                if (loop_delay > 0)
                    next_loop_time = world.time + loop_delay;
                return BT_RUNNING;
            }
            next_loop_time = 0;
            return BT_FAILURE;
        }

        if (success_policy == BT_SUBPLAN_LOOP_ON_SUCCESS)
        {
            Waiting.NoSleep(child.reset_tick_state());
            if (loop_delay > 0)
                next_loop_time = world.time + loop_delay;
            return BT_RUNNING;
        }
        next_loop_time = 0;
        return BT_SUCCESS;
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:212
    public override async DmTask reset_tick_state()
    {
        await base.reset_tick_state();
        next_loop_time = 0;
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:216
    public override void set_descriptor_children(DmList children_descs = null, datum_ai_controller controller = null)
    {
        base.set_descriptor_children(children_descs, controller);
        if (length(children) > 1)
        {
            datum_bt_node_composite_sequence legacy_subplan_sequence = Dm.@new<datum_bt_node_composite_sequence>();
            legacy_subplan_sequence.children = children;
            children = Dm.list(legacy_subplan_sequence);
        }
    }
}

/**
 * Parallel node: ticks ALL children every planning cycle, regardless of intermediate results.
 * Success and failure are determined by the configurable success_policy and failure_policy.
 *
 * Intended use: run multiple independent branches simultaneously, e.g.  action + locomotion
 */
// tg: code/datums/ai/_ai_bt_composites.dm:229
public partial class datum_bt_node_composite_parallel : datum_bt_node_composite
{
    // tg: code/datums/ai/_ai_bt_composites.dm:247
    public override object tick(datum_ai_controller controller = null, object seconds_per_tick = null)
    {
        float succeeded = 0;
        float failed = 0;
        float primary_result = 0;

        for (float i = 1, i__end = length(children); i <= i__end; i++)
        {
            datum_bt_node child = children[i] as datum_bt_node;

            if (i > 1 && Dm.truthy(repeat_secondary) && repeat_secondary_delay > 0 && Dm.num(LAZYACCESS(secondary_ready_at, i)) > world.time)
                continue; // secondary child is waiting out its repeat delay

            float child_result = Dm.num(child.tick(controller, seconds_per_tick));
            if (Dm.truthy(controller.cancelled_during_tick))
                return BT_FAILURE;

            if (i == 1)
            {
                primary_result = child_result;
                if (child_result == BT_SUCCESS)
                    succeeded++;
                else if (child_result == BT_FAILURE)
                    failed++;
            }
            else if (Dm.truthy(repeat_secondary) && child_result != BT_RUNNING)
            {
                Waiting.NoSleep(child.reset_tick_state());
                if (repeat_secondary_delay > 0)
                {
                    if (length(secondary_ready_at) < i)
                        LAZYSETLEN(ref secondary_ready_at, i);
                    secondary_ready_at[i] = world.time + repeat_secondary_delay;
                }
            }
            else
            {
                if (child_result == BT_SUCCESS)
                    succeeded++;
                else if (child_result == BT_FAILURE)
                    failed++;
            }
        }

        if (Dm.truthy(finish_on_primary) && primary_result != BT_RUNNING)
        {
            // Secondaries may be mid-RUNNING here, so the reset must recurse to cancel any deeper active behaviors.
            for (float i = 2, i__end = length(children); i <= i__end; i++)
            {
                datum_bt_node child = children[i] as datum_bt_node;
                Waiting.NoSleep(child.reset_subtree_tick_states());
            }
            return primary_result;
        }

        if ((failure_policy == BT_PARALLEL_FAILURE_CHILD_ONE && primary_result == BT_FAILURE) ||
                (failure_policy == BT_PARALLEL_FAILURE_ANY && failed > 0))
            return BT_FAILURE;
        if ((success_policy == BT_PARALLEL_SUCCESS_CHILD_ONE && primary_result == BT_SUCCESS) ||
                (success_policy == BT_PARALLEL_SUCCESS_ALL && succeeded == length(children)))
            return BT_SUCCESS;
        return BT_RUNNING;
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:295
    public override async DmTask reset_tick_state()
    {
        await base.reset_tick_state();
        secondary_ready_at = null;
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:299
    public override void append_active_nodes(DmList lines = null, object indent = null)
    {
        foreach (datum_bt_node child in Dm.each_cast<datum_bt_node>(children))
        {
            if (Dm.truthy(child.has_active_descendants()))
                child.append_active_nodes(lines, indent);
        }
    }

    // tg: code/datums/ai/_ai_bt_composites.dm:304
    public override void append_full_tree_state(DmList lines = null, object indent = null)
    {
        Dm.append(ref lines, Dm.text("[][] PARALLEL", indent, get_status_marker()));
        foreach (datum_bt_node child in Dm.each_cast<datum_bt_node>(children))
            child.append_full_tree_state(lines, Dm.text("[]  ", indent));
    }
}
