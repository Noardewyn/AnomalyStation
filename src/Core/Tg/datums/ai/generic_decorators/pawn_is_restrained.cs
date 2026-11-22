// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/pawn_is_restrained.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates on the pawn having TRAIT_RESTRAINED; reacts to trait add/remove signals.
// tg: code/datums/ai/generic_decorators/pawn_is_restrained.dm:1
public partial class datum_bt_node_decorator_pawn_is_restrained : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/pawn_is_restrained.dm:6
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(SIGNAL_ADDTRAIT(TRAIT_RESTRAINED), SIGNAL_REMOVETRAIT(TRAIT_RESTRAINED)), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/pawn_is_restrained.dm:10
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(SIGNAL_ADDTRAIT(TRAIT_RESTRAINED), SIGNAL_REMOVETRAIT(TRAIT_RESTRAINED)));
    }

    // tg: code/datums/ai/generic_decorators/pawn_is_restrained.dm:13
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return Dm.num(HAS_TRAIT(controller.pawn, TRAIT_RESTRAINED));
    }
}
