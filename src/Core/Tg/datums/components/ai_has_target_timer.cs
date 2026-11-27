// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/ai_has_target_timer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Increments a blackboard key while the attached mob is engaged with a particular target, does nothing else on its own
// tg: code/datums/components/ai_has_target_timer.dm:2
public partial class datum_component_ai_target_timer : datum_component
{
    // tg: code/datums/components/ai_has_target_timer.dm:14
    [DmParams("increment_key", "target_key")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object increment_key = Dm.arg(__extra, 0);
        object target_key = Dm.arg(__extra, 1);
        increment_key ??= BB_BASIC_MOB_HAS_TARGET_TIME;
        target_key ??= BB_CURRENT_TARGET;
        object dot = await base.Initialize(Dm.forward(__extra, increment_key, target_key));
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        mob_living mob_parent = parent as mob_living;
        if (mob_parent.ai_controller == null)
            return COMPONENT_INCOMPATIBLE;
        this.increment_key = increment_key;
        this.target_key = target_key;
        return dot;
    }

    // tg: code/datums/components/ai_has_target_timer.dm:24
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_AI_BLACKBOARD_KEY_SET(target_key), nameof(changed_target));
        RegisterSignal(parent, COMSIG_AI_BLACKBOARD_KEY_CLEARED(target_key), nameof(lost_target));
        ADD_TRAIT(parent, TRAIT_SUBTREE_REQUIRED_OPERATIONAL_DATUM, type);
    }

    // tg: code/datums/components/ai_has_target_timer.dm:30
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        finalise_losing_target();
        UnregisterSignal(parent, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(target_key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(target_key)));
        REMOVE_TRAIT(parent, TRAIT_SUBTREE_REQUIRED_OPERATIONAL_DATUM, type);
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/datums/components/ai_has_target_timer.dm:36
    public override object Destroy(float? force = null)
    {
        finalise_losing_target();
        return base.Destroy(force);
    }

    /// When we get a new target, reset the timer and start processing
    // tg: code/datums/components/ai_has_target_timer.dm:41
    [SignalHandler]
    public virtual void changed_target(mob_living source = null)
    {
        mob_living living_parent = parent as mob_living;
        atom new_target = (living_parent.ai_controller as datum_ai_controller).blackboard[target_key] as atom;
        deltimer(reset_clock_timer);
        if (new_target == last_target)
            return;
        time_on_target = 0;
        store_current_time();
        START_PROCESSING(SSdcs, this);
        if (last_target != null)
            UnregisterSignal(last_target, COMSIG_QDELETING);
        RegisterSignal(new_target, COMSIG_QDELETING, nameof(finalise_losing_target));
        last_target = new_target;
    }

    /// When we lose our target, start a short timer in case we reacquire it very quickly
    // tg: code/datums/components/ai_has_target_timer.dm:57
    [SignalHandler]
    public virtual void lost_target()
    {
        reset_clock_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(finalise_losing_target)), 3 * SECONDS, Dm.bitor(TIMER_STOPPABLE, TIMER_DELETE_ME), file: "code/datums/components/ai_has_target_timer.dm", line: 59);
    }

    /// Called if we have had no target for long enough
    // tg: code/datums/components/ai_has_target_timer.dm:62
    public virtual void finalise_losing_target()
    {
        deltimer(reset_clock_timer);
        STOP_PROCESSING(SSdcs, this);
        if (last_target != null)
            UnregisterSignal(last_target, COMSIG_QDELETING);
        last_target = null;
        time_on_target = 0;
        if (!QDELETED(parent))
            store_current_time();
    }

    /// Store the current time on our timer in our blackboard key
    // tg: code/datums/components/ai_has_target_timer.dm:73
    public virtual void store_current_time()
    {
        mob_living living_parent = parent as mob_living;
        (living_parent.ai_controller as datum_ai_controller).set_blackboard_key(increment_key, time_on_target);
    }

    // tg: code/datums/components/ai_has_target_timer.dm:77
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        time_on_target += Dm.num(seconds_per_tick) * SECONDS;
        store_current_time();
        return null;
    }
}
