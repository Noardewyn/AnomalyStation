// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/_ai_behavior.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the EVLOG lines (the admin event logger, as movement_types.cs) (gaps package C1). The var block is generated.

/// Base type for AI behavior leaf nodes in the behavior tree system.
/// setup() is called once on first activation, perform() each tick while running.
/// Returns BT_SUCCESS / BT_FAILURE on completion, BT_RUNNING while active.
// tg: code/datums/ai/_ai_behavior.dm:4
public partial class datum_bt_node_ai_behavior : datum_bt_node
{
    // tg: code/datums/ai/_ai_behavior.dm:22
    public override object has_active_descendants()
    {
        return running;
    }

    // tg: code/datums/ai/_ai_behavior.dm:25
    public override object get_status_marker()
    {
        if (Dm.truthy(running))
            return "*";
        return base.get_status_marker();
    }

    // tg: code/datums/ai/_ai_behavior.dm:30
    public override void append_active_nodes(DmList lines = null, object indent = null)
    {
        if (Dm.truthy(running))
            Dm.append(ref lines, Dm.text("[][]", indent, span_bold(Dm.text("● []", label))));
    }

    /**
     * ai behavior tick. Runs setup() once on first activation, then perform() each tick.
     * Respects per-controller cooldowns set by AI_BEHAVIOR_DELAY.
     * Returns BT_SUCCESS / BT_FAILURE on completion, BT_RUNNING while active.
     */
    // tg: code/datums/ai/_ai_behavior.dm:39
    public override object tick(datum_ai_controller controller = null, object seconds_per_tick = null)
    {
        if (next_perform_time > world.time)
        {
            if (!Dm.truthy(running) && Dm.truthy(failed_last_perform))
                return BT_FAILURE;
            controller.active_execution_index = execution_index;
            return BT_RUNNING;
        }

        if (controller.bt_execution_log != null) // dont track if we're not  viewing
        {
            if (length(controller.bt_execution_log) < BT_EXECUTION_LOG_MAX)
                Dm.append(ref controller.bt_execution_log, execution_index);
        }

        if (!Dm.truthy(running))
        {
            if (!Dm.truthy(Waiting.NoSleep(setup(controller))))
                return BT_FAILURE;
            running = TRUE;
        }

        object process_flags = perform(seconds_per_tick, controller);

        if (Dm.truthy(Dm.bitand(process_flags, AI_BEHAVIOR_DELAY)))
            next_perform_time = world.time + Dm.num(get_cooldown(controller));
        if (Dm.truthy(Dm.bitand(process_flags, AI_BEHAVIOR_SUCCEEDED)))
        {
            failed_last_perform = FALSE;
            Waiting.NoSleep(finish_action(controller, TRUE));
            return BT_SUCCESS;
        }
        if (Dm.truthy(Dm.bitand(process_flags, AI_BEHAVIOR_FAILED)))
        {
            failed_last_perform = TRUE;
            Waiting.NoSleep(finish_action(controller, FALSE));
            return BT_FAILURE;
        }
        controller.active_execution_index = execution_index;
        return BT_RUNNING;
    }

    /// Returns the cooldown to apply after a AI_BEHAVIOR_DELAY perform(). Override for conditional delays.
    // tg: code/datums/ai/_ai_behavior.dm:75
    public virtual object get_cooldown(datum_ai_controller cooldown_for = null)
    {
        return time_between_perform;
    }

    /// Called when this behavior first activates on a controller. Return FALSE to abort (returns BT_FAILURE).
    // tg: code/datums/ai/_ai_behavior.dm:79
    public virtual async DmTask<object> setup(datum_ai_controller controller = null)
    {
        return TRUE;
    }

    /// Called each tick while the behavior is running. Returns AI_BEHAVIOR_* flags.
    // tg: code/datums/ai/_ai_behavior.dm:83
    public virtual object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        return null;
    }

    /// Called when the behavior finishes (succeeded or failed). Subtypes should call ..().
    // tg: code/datums/ai/_ai_behavior.dm:88
    public virtual async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        running = FALSE;
        async_running = FALSE;
        async_finished = FALSE;
        async_result_flags = NONE;
    }

    ///Checks if we're running async behavior, and if its finished, returns result flags
    // tg: code/datums/ai/_ai_behavior.dm:96
    public virtual object handle_async()
    {
        if (Dm.truthy(async_running))
            return AI_BEHAVIOR_DELAY;
        if (Dm.truthy(async_finished))
            return Dm.bitor(async_result_flags, AI_BEHAVIOR_DELAY);
        return NONE;
    }

    ///Marks that async behavior has started and runs perform_async
    // tg: code/datums/ai/_ai_behavior.dm:104
    public virtual object start_async()
    {
        async_running = TRUE;
        Waiting.Detach(perform_async(owning_controller));
        object handled = handle_async();
        return Dm.truthy(handled) ? handled : AI_BEHAVIOR_DELAY;
    }

    ///Override this if you have sleeping behavior, be sure to implement the other async procs in perform()
    // tg: code/datums/ai/_ai_behavior.dm:110
    public virtual async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        return null;
    }

    /// Call from an async behavior after its sleeping call, before committing side effects. FALSE means the behavior was aborted/reset mid-flight  bail out without side effects.
    // tg: code/datums/ai/_ai_behavior.dm:114
    public virtual object async_still_valid()
    {
        return Dm.num(Dm.truthy(async_running) && !QDELETED(owning_controller?.pawn));
    }

    /// Call from an async behavior to commit its result. No-op if the behavior was aborted mid-flight.
    // tg: code/datums/ai/_ai_behavior.dm:118
    public virtual void finish_async(object result_flags = null)
    {
        if (!Dm.truthy(async_still_valid()))
            return;
        async_result_flags = Dm.num(result_flags);
        async_finished = TRUE;
        async_running = FALSE;
    }

    // tg: code/datums/ai/_ai_behavior.dm:125
    public virtual void modify_cooldown(object new_next_perform_time = null)
    {
        next_perform_time = Dm.num(new_next_perform_time);
    }

    // tg: code/datums/ai/_ai_behavior.dm:128
    public override async DmTask reset_tick_state()
    {
        if (Dm.truthy(running))
            await finish_action(owning_controller, FALSE);
        await base.reset_tick_state();
    }
}
