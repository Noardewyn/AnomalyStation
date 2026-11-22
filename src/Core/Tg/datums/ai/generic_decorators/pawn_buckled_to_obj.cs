// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/pawn_buckled_to_obj.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates on the pawn being buckled to an obj; writes pawn.buckled to the escape target key as a side effect.
// tg: code/datums/ai/generic_decorators/pawn_buckled_to_obj.dm:1
public partial class datum_bt_node_decorator_pawn_buckled_to_obj : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/pawn_buckled_to_obj.dm:6
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(COMSIG_MOB_BUCKLED, COMSIG_MOB_UNBUCKLED), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/pawn_buckled_to_obj.dm:10
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_MOB_BUCKLED, COMSIG_MOB_UNBUCKLED));
    }

    // tg: code/datums/ai/generic_decorators/pawn_buckled_to_obj.dm:13
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living pawn = controller.pawn as mob_living;
        if (!isobj(pawn.buckled))
            return FALSE;
        controller.set_blackboard_key(target_key, pawn.buckled);
        return TRUE;
    }
}
