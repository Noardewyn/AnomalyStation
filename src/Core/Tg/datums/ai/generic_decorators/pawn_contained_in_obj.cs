// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/pawn_contained_in_obj.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates on the pawn being inside an obj (not a turf, mob, or mob_holder); writes pawn.loc to the escape target key.
// tg: code/datums/ai/generic_decorators/pawn_contained_in_obj.dm:1
public partial class datum_bt_node_decorator_pawn_contained_in_obj : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/pawn_contained_in_obj.dm:6
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(COMSIG_MOVABLE_MOVED), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/pawn_contained_in_obj.dm:10
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_MOVABLE_MOVED));
    }

    // tg: code/datums/ai/generic_decorators/pawn_contained_in_obj.dm:13
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living pawn = controller.pawn as mob_living;
        if (isturf(pawn.loc) || ismob(pawn.loc) || pawn.loc is obj_item_mob_holder || HAS_TRAIT(controller.pawn, TRAIT_MOVE_VENTCRAWLING))
            return FALSE;
        controller.set_blackboard_key(target_key, pawn.loc);
        return TRUE;
    }
}
