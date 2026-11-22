// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/pawn_grabbed_by_enemy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates on the pawn being grabbed above GRAB_PASSIVE by a mob the targeting strategy considers an enemy.
// tg: code/datums/ai/generic_decorators/pawn_grabbed_by_enemy.dm:1
public partial class datum_bt_node_decorator_pawn_grabbed_by_enemy : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/pawn_grabbed_by_enemy.dm:6
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(COMSIG_LIVING_GET_PULLED, COMSIG_ATOM_NO_LONGER_PULLED, COMSIG_MOVABLE_CHANGED_GRABBED_STATE), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/pawn_grabbed_by_enemy.dm:10
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_LIVING_GET_PULLED, COMSIG_ATOM_NO_LONGER_PULLED, COMSIG_MOVABLE_CHANGED_GRABBED_STATE));
    }

    // tg: code/datums/ai/generic_decorators/pawn_grabbed_by_enemy.dm:13
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living pawn = controller.pawn as mob_living;
        mob puller = pawn.pulledby as mob;
        if (puller == null || Dm.num(puller.grab_state) <= GRAB_PASSIVE)
            return FALSE;
        datum_targeting_strategy strategy = GET_TARGETING_STRATEGY(controller.blackboard[targeting_strategy_key]);
        if (strategy == null || !Dm.truthy(await strategy.is_valid_target(pawn, puller)))
            return FALSE;
        DmList friends = controller.blackboard[BB_FRIENDS_LIST] as DmList ?? Dm.list();
        return Dm.num(!Dm.@in(puller, friends));
    }
}
