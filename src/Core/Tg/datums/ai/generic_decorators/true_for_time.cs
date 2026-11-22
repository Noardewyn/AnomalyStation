// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/true_for_time.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/**
 * Passes while its duration has not elapsed.
 * Starts timing on the first tick; resets when the tree resets.
 *
 * duration: how long to pass, in deciseconds (e.g. "10 SECONDS").
 * succeed_on_timeout FALSE (default): counts as succeed and lets lower nodes pass
*/
// tg: code/datums/ai/generic_decorators/true_for_time.dm:1
public partial class datum_bt_node_decorator_true_for_time : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/true_for_time.dm:15
    public override object register_observe_signals(atom pawn = null)
    {
        timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(on_timeout)), Dm.num(duration), Dm.bitor(TIMER_STOPPABLE, TIMER_DELETE_ME), file: "code/datums/ai/generic_decorators/true_for_time.dm", line: 16);
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/true_for_time.dm:19
    public override void unregister_observe_signals(atom pawn = null)
    {
        if (Dm.truthy(timer_id))
        {
            deltimer(timer_id);
            timer_id = null;
        }
        timed_out = FALSE;
    }

    // tg: code/datums/ai/generic_decorators/true_for_time.dm:25
    [SignalHandler]
    public virtual void on_timeout()
    {
        timer_id = null;
        timed_out = TRUE;
        if (Dm.truthy(succeed_on_timeout))
            return; // handled locally by tick(), no plan-wide cancel needed
        if (owning_controller != null)
            Waiting.NoSleep(on_observed_change(owning_controller, null));
    }

    // tg: code/datums/ai/generic_decorators/true_for_time.dm:34
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return Dm.num(!Dm.truthy(timed_out));
    }

    // tg: code/datums/ai/generic_decorators/true_for_time.dm:37
    public override object tick(datum_ai_controller controller = null, object seconds_per_tick = null)
    {
        if (Dm.truthy(succeed_on_timeout) && Dm.truthy(timed_out))
        {
            if (Dm.truthy(child_active) && child != null)
            {
                Waiting.NoSleep(child.reset_subtree_tick_states());
                child_active = FALSE;
            }
            return BT_SUCCESS;
        }
        return base.tick(controller, seconds_per_tick);
    }
}
