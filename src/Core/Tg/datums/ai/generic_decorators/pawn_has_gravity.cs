// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/pawn_has_gravity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates child on the pawn having gravity. Use invert = TRUE to gate on weightlessness instead.
// tg: code/datums/ai/generic_decorators/pawn_has_gravity.dm:1
public partial class datum_bt_node_decorator_pawn_has_gravity : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/pawn_has_gravity.dm:4
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignal(pawn, COMSIG_LIVING_GRAVITY_CHANGED, nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/pawn_has_gravity.dm:8
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, COMSIG_LIVING_GRAVITY_CHANGED);
    }

    // tg: code/datums/ai/generic_decorators/pawn_has_gravity.dm:11
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return Dm.num(controller.pawn is mob_living living_pawn && Dm.truthy(living_pawn.has_gravity()));
    }
}
