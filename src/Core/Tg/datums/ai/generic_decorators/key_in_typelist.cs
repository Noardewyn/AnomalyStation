// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/key_in_typelist.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates child on the atom in `key` being an instance of a type in the typelist held at `typelist_key`. Use "invert": true for the opposite.
// tg: code/datums/ai/generic_decorators/key_in_typelist.dm:1
public partial class datum_bt_node_decorator_key_in_typelist : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/key_in_typelist.dm:8
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key), COMSIG_AI_BLACKBOARD_KEY_SET(typelist_key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(typelist_key)), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/key_in_typelist.dm:12
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key), COMSIG_AI_BLACKBOARD_KEY_SET(typelist_key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(typelist_key)));
    }

    // tg: code/datums/ai/generic_decorators/key_in_typelist.dm:25
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[key] as atom;
        DmList types = controller.blackboard[typelist_key] as DmList;
        if (QDELETED(target) || types == null)
            return FALSE;
        return is_type_in_list(target, types);
    }
}
