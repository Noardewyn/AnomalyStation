// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/is_grabbing_target.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

///Are we grabbing the specified target key?
// tg: code/datums/ai/generic_decorators/is_grabbing_target.dm:1
public partial class datum_bt_node_decorator_is_grabbing_target : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/is_grabbing_target.dm:6
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        atom_movable target = controller.blackboard[key] as atom_movable;
        mob_living our_mob = controller.pawn as mob_living;
        if (QDELETED(target) || our_mob.pulling != target)
        {
            controller.clear_blackboard_key(key);
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/is_grabbing_target.dm:14
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(COMSIG_LIVING_START_PULL, COMSIG_ATOM_NO_LONGER_PULLING, COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/is_grabbing_target.dm:18
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_LIVING_START_PULL, COMSIG_ATOM_NO_LONGER_PULLING, COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)));
    }
}
