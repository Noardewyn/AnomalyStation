// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/pawn_has_trait_from.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates child on the pawn having a given trait from a specific source. Use "invert": true to gate on the trait being absent.
// tg: code/datums/ai/generic_decorators/pawn_has_trait_from.dm:1
public partial class datum_bt_node_decorator_pawn_has_trait_from : datum_bt_node_decorator
{
    /// The source the trait must originate from.
    public object source;

    // tg: code/datums/ai/generic_decorators/pawn_has_trait_from.dm:8
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(SIGNAL_ADDTRAIT(trait), SIGNAL_REMOVETRAIT(trait)), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/pawn_has_trait_from.dm:12
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(SIGNAL_ADDTRAIT(trait), SIGNAL_REMOVETRAIT(trait)));
    }

    // tg: code/datums/ai/generic_decorators/pawn_has_trait_from.dm:15
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return Dm.num(HAS_TRAIT_FROM(controller.pawn, trait, source));
    }
}
