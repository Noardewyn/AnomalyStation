// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/target_has_trait.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates child on the atom held in a blackboard key having a given trait. Use "invert": true to gate on the trait being absent.
// tg: code/datums/ai/generic_decorators/target_has_trait.dm:1
public partial class datum_bt_node_decorator_target_has_trait : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/target_has_trait.dm:10
    public override object register_observe_signals(atom pawn = null)
    {
        atom target = owning_controller?.blackboard[key] as atom;
        if (target != null)
        {
            observed_target = target;
            RegisterSignals(target, Dm.list(SIGNAL_ADDTRAIT(trait), SIGNAL_REMOVETRAIT(trait)), nameof(on_signal_changed), @override: TRUE);
        }
        RegisterSignals(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)), nameof(on_target_key_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/target_has_trait.dm:18
    public override void unregister_observe_signals(atom pawn = null)
    {
        if (observed_target != null)
        {
            UnregisterSignal(observed_target, Dm.list(SIGNAL_ADDTRAIT(trait), SIGNAL_REMOVETRAIT(trait)));
            observed_target = null;
        }
        UnregisterSignal(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)));
    }

    // tg: code/datums/ai/generic_decorators/target_has_trait.dm:25
    [SignalHandler]
    public virtual void on_target_key_changed(atom source = null, params object[] __extra)
    {
        atom target = owning_controller?.blackboard[key] as atom;
        if (target == observed_target)
            return;
        if (observed_target != null)
        {
            UnregisterSignal(observed_target, Dm.list(SIGNAL_ADDTRAIT(trait), SIGNAL_REMOVETRAIT(trait)));
            observed_target = null;
        }
        if (target != null)
        {
            observed_target = target;
            RegisterSignals(target, Dm.list(SIGNAL_ADDTRAIT(trait), SIGNAL_REMOVETRAIT(trait)), nameof(on_signal_changed), @override: TRUE);
        }
        if (owning_controller != null)
            Waiting.NoSleep(on_observed_change(owning_controller, null));
    }

    // tg: code/datums/ai/generic_decorators/target_has_trait.dm:40
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[key] as atom;
        if (QDELETED(target))
            return FALSE;
        return Dm.num(HAS_TRAIT(target, trait));
    }
}
