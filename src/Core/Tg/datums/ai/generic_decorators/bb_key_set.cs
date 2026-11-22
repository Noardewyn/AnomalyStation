// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/bb_key_set.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Is the key set to a non-null value
// tg: code/datums/ai/generic_decorators/bb_key_set.dm:1
public partial class datum_bt_node_decorator_bb_key_set : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/bb_key_set.dm:5
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/bb_key_set.dm:9
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)));
    }

    // tg: code/datums/ai/generic_decorators/bb_key_set.dm:12
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return controller.blackboard_key_exists(key);
    }
}
