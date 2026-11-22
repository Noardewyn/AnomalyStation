// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/do_once.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

///Only returns TRUE the first time. Optionally only locks if the child succeeds. Can be unlocked again
// tg: code/datums/ai/generic_decorators/do_once.dm:1
public partial class datum_bt_node_decorator_do_once : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/do_once.dm:8
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return Dm.num(!Dm.truthy(controller.blackboard_key_exists(lock_key)));
    }

    // tg: code/datums/ai/generic_decorators/do_once.dm:11
    public override void on_child_complete(datum_ai_controller controller = null, object result = null)
    {
        if (!Dm.truthy(only_lock_if_succeeded) || Dm.eq(result, BT_SUCCESS))
            controller.set_blackboard_key(lock_key, TRUE);
    }

    // tg: code/datums/ai/generic_decorators/do_once.dm:15
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(lock_key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(lock_key)), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/do_once.dm:19
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(lock_key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(lock_key)));
    }
}
