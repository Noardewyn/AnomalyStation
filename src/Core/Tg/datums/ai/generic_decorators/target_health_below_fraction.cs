// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/target_health_below_fraction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Passes when the living mob held in a blackboard key has health below a fraction of its max health.
// tg: code/datums/ai/generic_decorators/target_health_below_fraction.dm:1
public partial class datum_bt_node_decorator_target_health_below_fraction : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/target_health_below_fraction.dm:9
    public override object register_observe_signals(atom pawn = null)
    {
        mob_living target = owning_controller?.blackboard[key] as mob_living;
        if (target != null)
        {
            observed_mob = target;
            RegisterSignal(target, COMSIG_LIVING_HEALTH_UPDATE, nameof(on_signal_changed), @override: TRUE);
        }
        RegisterSignals(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)), nameof(on_mob_key_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/target_health_below_fraction.dm:17
    public override void unregister_observe_signals(atom pawn = null)
    {
        if (observed_mob != null)
        {
            UnregisterSignal(observed_mob, COMSIG_LIVING_HEALTH_UPDATE);
            observed_mob = null;
        }
        UnregisterSignal(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)));
    }

    // tg: code/datums/ai/generic_decorators/target_health_below_fraction.dm:24
    [SignalHandler]
    public virtual void on_mob_key_changed(atom source = null, params object[] __extra)
    {
        mob_living target = owning_controller?.blackboard[key] as mob_living;
        if (target == observed_mob)
            return;
        if (observed_mob != null)
        {
            UnregisterSignal(observed_mob, COMSIG_LIVING_HEALTH_UPDATE);
            observed_mob = null;
        }
        if (target != null)
        {
            observed_mob = target;
            RegisterSignal(target, COMSIG_LIVING_HEALTH_UPDATE, nameof(on_signal_changed), @override: TRUE);
        }
        if (owning_controller != null)
            Waiting.NoSleep(on_observed_change(owning_controller, null));
    }

    // tg: code/datums/ai/generic_decorators/target_health_below_fraction.dm:37
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        if (controller.blackboard[key] is not mob_living target)
            return FALSE;
        return Dm.num(target.health < target.maxHealth * fraction);
    }
}
