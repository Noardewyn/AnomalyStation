// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/_ai_bt_decorators.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the EVLOG lines of on_observed_change (the admin event logger, as movement_types.cs) (gaps package C1). The var block is generated.

/**
 * Base decorator node. Wraps a single child with a condition check.
 *
 *
 * Supports observer aborts: register to watch specific signals, which triggers a re-check of the condition, potentially aborting the plan depending on the observer_abort settings.
 */
// tg: code/datums/ai/_ai_bt_decorators.dm:7
public partial class datum_bt_node_decorator : datum_bt_node
{
    // tg: code/datums/ai/_ai_bt_decorators.dm:33
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(observers_registered))
        {
            unregister_observe_signals(owning_controller?.pawn);
            if (Dm.truthy(is_polled))
            {
                if (owning_controller != null)
                    LAZYREMOVE(ref owning_controller.polling_observers, this);
                is_polled = FALSE;
            }
            observers_registered = FALSE;
            has_observer_signals = FALSE;
        }
        QDEL_NULL(ref child);
        return base.Destroy(force);
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:44
    public override object get_children()
    {
        return child != null ? Dm.list(child) : null;
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:47
    public override object has_active_descendants()
    {
        return Dm.num(child != null && Dm.truthy(child.has_active_descendants()));
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:50
    public override void finalize_node(datum_ai_controller controller = null, DmList to_visit = null)
    {
        base.finalize_node(controller, to_visit);
        if (child != null)
        {
            child.parent_node = this;
            Dm.append(ref to_visit, child);
        }
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:56
    public override void append_active_nodes(DmList lines = null, object indent = null)
    {
        if (child != null && Dm.truthy(child.has_active_descendants()))
        {
            Dm.append(ref lines, Dm.text("[][]", indent, label));
            child.append_active_nodes(lines, Dm.text("[]  ", indent));
        }
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:61
    public override void set_descriptor_children(DmList children_descs = null, datum_ai_controller controller = null)
    {
        datum_bt_node resolved = controller.get_or_build_node(children_descs[1]) as datum_bt_node;
        if (resolved != null)
            child = resolved;
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:66
    public override void collect_reset_children(DmList to_visit = null)
    {
        if (child != null)
            Dm.append(ref to_visit, child);
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:70
    public override void append_full_tree_state(DmList lines = null, object indent = null)
    {
        string observer_text = "";
        if (observer_abort != BT_ABORT_NONE)
        {
            string abort_name = "";
            if (observer_abort == BT_ABORT_SELF)
                abort_name = "SELF";
            else if (observer_abort == BT_ABORT_LOWER_PRIORITY)
                abort_name = "LOWER";
            else if (observer_abort == BT_ABORT_BOTH)
                abort_name = "BOTH";
            observer_text = Dm.text(" (abort-[])", abort_name);
        }
        Dm.append(ref lines, Dm.text("[][] [][]", indent, get_status_marker(), label, observer_text));
        if (child != null)
            child.append_full_tree_state(lines, Dm.text("[]  ", indent));
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:85
    public override object tick(datum_ai_controller controller = null, object seconds_per_tick = null)
    {
        if (!Dm.truthy(observers_registered))
        {
            observers_registered = TRUE;
            if (observer_abort != BT_ABORT_NONE)
            {
                has_observer_signals = Dm.num(register_observe_signals(controller.pawn));
                if (!Dm.truthy(has_observer_signals))
                {
                    is_polled = TRUE;
                    LAZYADDASSOC(ref controller.polling_observers, this, TRUE);
                }
            }
        }

        bool child_ticked = false;
        float result;
        if (child == null)
            return BT_FAILURE;
        bool no_ticking_condition = observer_abort == BT_ABORT_NONE || Dm.truthy(has_observer_signals);
        if ((no_ticking_condition || Dm.truthy(is_polled)) && Dm.truthy(child_active))
        {
            child_ticked = true;
            result = Dm.num(child.tick(controller, seconds_per_tick));
        }
        else if (Dm.truthy(Waiting.NoSleep(check_condition(controller))) == Dm.truthy(invert))
            result = BT_FAILURE;
        else
        {
            child_ticked = true;
            result = Dm.num(child.tick(controller, seconds_per_tick));
        }

        if (Dm.truthy(controller.cancelled_during_tick))
        {
            child_active = FALSE;
            return BT_FAILURE;
        }

        if (no_ticking_condition || Dm.truthy(is_polled))
        {
            child_active = Dm.num(result == BT_RUNNING);
            if (child_ticked && !Dm.truthy(child_active))
                on_child_complete(controller, result);
        }

        return result;
    }

    /**
     * Called when the child finishes (returns a non-RUNNING result after being ticked).
     * NOT called when the condition gate blocks the child, or when the tree is cancelled mid-tick.
     */
    // tg: code/datums/ai/_ai_bt_decorators.dm:123
    public virtual void on_child_complete(datum_ai_controller controller = null, object result = null)
    {
    }

    /**
     * Override to implement custom condition logic.
     */
    // tg: code/datums/ai/_ai_bt_decorators.dm:129
    public virtual async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return TRUE;
    }

    /**
     * Proc called by the observer system when a watched key changes.
     * Return TRUE if the decorator's condition would pass, FALSE otherwise.
     */
    // tg: code/datums/ai/_ai_bt_decorators.dm:136
    public virtual async DmTask<object> evaluate_for_observer(datum_ai_controller controller = null)
    {
        return Dm.num(Dm.truthy(await check_condition(controller)) != Dm.truthy(invert));
    }

    /**
     * Called by the controller's observer handler when a watched blackboard key changes.
     * Re-evaluates evaluate_for_observer() and aborts based on observer_abort policy.
     *
     * BT_ABORT_SELF:            condition became FALSE and we're running our children > cancel actions
     * BT_ABORT_LOWER_PRIORITY:  condition became TRUE and we're running lower priority nodes > cancel actions
     */
    /// Called by the controller's polling loop for decorators that have no signal observers.
    /// Sets a baseline on first call, then fires on_observed_change() only when the result changes.
    // tg: code/datums/ai/_ai_bt_decorators.dm:148
    public virtual async DmTask poll_condition(datum_ai_controller controller = null)
    {
        if (Dm.truthy(polling_rate) && last_poll_time + polling_rate > world.time)
            return;
        last_poll_time = world.time;
        object current = await evaluate_for_observer(controller);
        if (last_poll_result == null)
        {
            last_poll_result = current;
            return;
        }
        if (!Dm.eq(current, last_poll_result))
        {
            last_poll_result = current;
            await on_observed_change(controller, null);
        }
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:160
    public virtual async DmTask on_observed_change(datum_ai_controller controller = null, object key = null)
    {
        bool condition_result = Dm.truthy(await evaluate_for_observer(controller));

        if (!condition_result && Dm.truthy(Dm.bitand(observer_abort, BT_ABORT_SELF)))
        {
            float active = controller.active_execution_index;
            if (!Dm.truthy(execution_index) || (active >= execution_index && active <= last_execution_index))
                await controller.cancel_current_plan();
        }

        if (condition_result && Dm.truthy(Dm.bitand(observer_abort, BT_ABORT_LOWER_PRIORITY)))
        {
            float active = controller.active_execution_index;
            if (!Dm.truthy(execution_index) || !Dm.truthy(active) || active > last_execution_index)
                await controller.cancel_current_plan();
        }
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:175
    public override async DmTask reset_tick_state()
    {
        if (Dm.truthy(observers_registered))
        {
            unregister_observe_signals(owning_controller?.pawn);
            if (Dm.truthy(is_polled))
            {
                if (owning_controller != null)
                    LAZYREMOVE(ref owning_controller.polling_observers, this);
                is_polled = FALSE;
            }
            observers_registered = FALSE;
            has_observer_signals = FALSE;
        }
        last_poll_result = null;
        last_poll_time = 0;
        child_active = FALSE;
        await base.reset_tick_state();
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:188
    public override object assign_execution_indices(object counter = null)
    {
        execution_index = Dm.num(counter);
        float c = Dm.num(counter) + 1;
        if (child != null)
            c = Dm.num(child.assign_execution_indices(c));
        last_execution_index = c - 1;
        return c;
    }

    /// Override to register all signal observers for this decorator. Return TRUE if any were registered. If a decorator does not handle this and we have an observer_abort mode that isn't BT_ABORT_NONE, the system will fall back to ticking the condition every tick, which is less efficient but allows for reactivity without signals.
    // tg: code/datums/ai/_ai_bt_decorators.dm:197
    public virtual object register_observe_signals(atom pawn = null)
    {
        return FALSE;
    }

    /// Override to unregister all observers registered by register_observe_signals().
    // tg: code/datums/ai/_ai_bt_decorators.dm:201
    public virtual void unregister_observe_signals(atom pawn = null)
    {
    }

    /// Shared signal handler. Calls on_observed_change() with owning_controller.
    // tg: code/datums/ai/_ai_bt_decorators.dm:205
    [SignalHandler]
    public virtual void on_signal_changed(atom source = null, params object[] __extra)
    {
        if (owning_controller != null)
            Waiting.NoSleep(on_observed_change(owning_controller, null));
    }

    /// Returns TRUE if the blackboard key holds a non-null, non-deleted value.
    // tg: code/datums/ai/_ai_bt_decorators.dm:212
    public virtual object bb_key_exists(datum_ai_controller controller = null, object key = null)
    {
        return controller.blackboard_key_exists(key);
    }

    /// Returns TRUE if the blackboard value at key equals the given value.
    // tg: code/datums/ai/_ai_bt_decorators.dm:236
    public virtual object bb_key_equals(datum_ai_controller controller = null, object key = null, object value = null)
    {
        return Dm.num(Dm.eq(controller.blackboard[key], value));
    }

    /// Returns TRUE if the blackboard value at key is strictly greater than threshold.
    // tg: code/datums/ai/_ai_bt_decorators.dm:240
    public virtual object bb_key_greater(datum_ai_controller controller = null, object key = null, object threshold = null)
    {
        return Dm.num(Dm.num(controller.blackboard[key]) > Dm.num(threshold));
    }
}

/// Gates on whether the named override slot currently has an active override installed.
/// Observes COMSIG_AI_OVERRIDE_SLOT_CHANGED so it reacts immediately when a command is set or cleared.
// tg: code/datums/ai/_ai_bt_decorators.dm:217
public partial class datum_bt_node_decorator_override_id_set : datum_bt_node_decorator
{
    // tg: code/datums/ai/_ai_bt_decorators.dm:221
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        datum_bt_node_subtree potential_subtree = LAZYACCESS(controller.override_slots, override_id) as datum_bt_node_subtree;
        return Dm.num(potential_subtree.override_node != null);
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:225
    public override object register_observe_signals(atom pawn = null)
    {
        if (override_id == null)
            return FALSE;
        RegisterSignal(pawn, COMSIG_AI_OVERRIDE_SLOT_CHANGED(override_id), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/_ai_bt_decorators.dm:231
    public override void unregister_observe_signals(atom pawn = null)
    {
        if (override_id != null)
            UnregisterSignal(pawn, COMSIG_AI_OVERRIDE_SLOT_CHANGED(override_id));
    }
}
