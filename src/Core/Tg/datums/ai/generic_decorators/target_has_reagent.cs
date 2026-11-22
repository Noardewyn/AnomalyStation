// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/target_has_reagent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates child on the atom held in a blackboard key having a specific reagent in its reagent list. Use "invert": true to gate on the reagent being absent.
// tg: code/datums/ai/generic_decorators/target_has_reagent.dm:1
public partial class datum_bt_node_decorator_target_has_reagent : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/target_has_reagent.dm:10
    public override object register_observe_signals(atom pawn = null)
    {
        atom target = owning_controller?.blackboard[key] as atom;
        if (target?.reagents != null)
        {
            observed_holder = target.reagents as datum_reagents;
            RegisterSignal(observed_holder, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(on_signal_changed), @override: TRUE);
        }
        RegisterSignals(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)), nameof(on_target_key_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/target_has_reagent.dm:18
    public override void unregister_observe_signals(atom pawn = null)
    {
        if (observed_holder != null)
        {
            UnregisterSignal(observed_holder, COMSIG_REAGENTS_HOLDER_UPDATED);
            observed_holder = null;
        }
        UnregisterSignal(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)));
    }

    /// Fires when the blackboard key changes. Rebinds the reagent holder observer to the new target and re-evaluates.
    // tg: code/datums/ai/generic_decorators/target_has_reagent.dm:25
    [SignalHandler]
    public virtual void on_target_key_changed(atom source = null, params object[] __extra)
    {
        atom target = owning_controller?.blackboard[key] as atom;
        datum_reagents new_holder = target?.reagents as datum_reagents;
        if (new_holder == observed_holder)
            return;
        if (observed_holder != null)
        {
            UnregisterSignal(observed_holder, COMSIG_REAGENTS_HOLDER_UPDATED);
            observed_holder = null;
        }
        if (new_holder != null)
        {
            observed_holder = new_holder;
            RegisterSignal(observed_holder, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(on_signal_changed), @override: TRUE);
        }
        if (owning_controller != null)
            Waiting.NoSleep(on_observed_change(owning_controller, null));
    }

    // tg: code/datums/ai/generic_decorators/target_has_reagent.dm:40
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[key] as atom;
        if (QDELETED(target) || target.reagents == null)
            return FALSE;
        return Dm.num(Dm.truthy(((datum_reagents)target.reagents).has_reagent(reagent_type)));
    }
}
