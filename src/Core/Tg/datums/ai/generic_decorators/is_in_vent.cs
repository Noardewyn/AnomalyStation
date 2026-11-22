// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/is_in_vent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates on the pawn currently being inside a vent (has TRAIT_MOVE_VENTCRAWLING).
// tg: code/datums/ai/generic_decorators/is_in_vent.dm:1
public partial class datum_bt_node_decorator_is_in_vent : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/is_in_vent.dm:4
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(SIGNAL_ADDTRAIT(TRAIT_MOVE_VENTCRAWLING), SIGNAL_REMOVETRAIT(TRAIT_MOVE_VENTCRAWLING)), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/is_in_vent.dm:8
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(SIGNAL_ADDTRAIT(TRAIT_MOVE_VENTCRAWLING), SIGNAL_REMOVETRAIT(TRAIT_MOVE_VENTCRAWLING)));
    }

    // tg: code/datums/ai/generic_decorators/is_in_vent.dm:11
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return Dm.num(HAS_TRAIT(controller.pawn, TRAIT_MOVE_VENTCRAWLING));
    }
}
