// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/mob_stat_at_least.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Passes when the mob held in a blackboard key has a stat value at that is at least X. Higher is more dead.
// tg: code/datums/ai/generic_decorators/mob_stat_at_least.dm:1
public partial class datum_bt_node_decorator_mob_stat_at_least : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/mob_stat_at_least.dm:10
    public override object register_observe_signals(atom pawn = null)
    {
        mob target = owning_controller?.blackboard[key] as mob;
        if (target != null)
        {
            observed_mob = target;
            RegisterSignal(target, COMSIG_MOB_STATCHANGE, nameof(on_signal_changed), @override: TRUE);
        }
        RegisterSignals(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)), nameof(on_mob_key_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/mob_stat_at_least.dm:18
    public override void unregister_observe_signals(atom pawn = null)
    {
        if (observed_mob != null)
        {
            UnregisterSignal(observed_mob, COMSIG_MOB_STATCHANGE);
            observed_mob = null;
        }
        UnregisterSignal(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)));
    }

    // tg: code/datums/ai/generic_decorators/mob_stat_at_least.dm:25
    [SignalHandler]
    public virtual void on_mob_key_changed(atom source = null, params object[] __extra)
    {
        mob target = owning_controller?.blackboard[key] as mob;
        if (target == observed_mob)
            return;
        if (observed_mob != null)
        {
            UnregisterSignal(observed_mob, COMSIG_MOB_STATCHANGE);
            observed_mob = null;
        }
        if (target != null)
        {
            observed_mob = target;
            RegisterSignal(target, COMSIG_MOB_STATCHANGE, nameof(on_signal_changed), @override: TRUE);
        }
        if (owning_controller != null)
            Waiting.NoSleep(on_observed_change(owning_controller, null));
    }

    // tg: code/datums/ai/generic_decorators/mob_stat_at_least.dm:41
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob target = controller.blackboard[key] as mob;
        if (!ismob(target))
            return FALSE;
        return Dm.num(target.stat >= min_stat);
    }
}
