// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/pawn_inside_mob.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates on the pawn being located inside another mob (e.g. absorbed or shapeshifted).
// tg: code/datums/ai/generic_decorators/pawn_inside_mob.dm:1
public partial class datum_bt_node_decorator_pawn_inside_mob : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/pawn_inside_mob.dm:6
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignal(pawn, COMSIG_MOVABLE_MOVED, nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/pawn_inside_mob.dm:10
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, COMSIG_MOVABLE_MOVED);
    }

    // tg: code/datums/ai/generic_decorators/pawn_inside_mob.dm:13
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return Dm.num(ismob(controller.pawn.loc));
    }
}
