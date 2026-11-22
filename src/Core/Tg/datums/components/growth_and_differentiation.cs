// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/growth_and_differentiation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the happiness component (a row) (gaps package C4).

/**
 * ### Growth and Differentiation Component: Used to randomly "grow" a creature into a new entity over its lifespan.
 *
 * If we are passed a typepath, we will 100% grow into that type. However, if we are not passed a typepath, we will pick one from a subtype of the parent we were applied to!
 */
// tg: code/datums/components/growth_and_differentiation.dm:6
public partial class datum_component_growth_and_differentiation : datum_component
{
    // tg: code/datums/components/growth_and_differentiation.dm:36
    [DmParams("growth_time", "growth_path", "growth_probability", "lower_growth_value", "upper_growth_value", "scale_with_happiness", "signals_to_kill_on", "optional_checks", "optional_grow_behavior")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        object growth_time = Dm.arg(__extra, 0);
        object growth_path = Dm.arg(__extra, 1);
        object growth_probability = Dm.arg(__extra, 2);
        object scale_with_happiness = Dm.arg(__extra, 5);
        DmList signals_to_kill_on = Dm.arg(__extra, 6) as DmList;
        this.growth_path = growth_path as Type;
        this.growth_time = growth_time;
        initial_growth_probability = this.growth_probability = growth_probability;
        this.lower_growth_value = Dm.arg(__extra, 3);
        this.upper_growth_value = Dm.arg(__extra, 4);
        this.optional_checks = Dm.arg(__extra, 7) as datum_callback;
        this.optional_grow_behavior = Dm.arg(__extra, 8) as datum_callback;

        if (islist(signals_to_kill_on))
        {
            this.signals_to_kill_on = signals_to_kill_on;
            RegisterSignals(parent, this.signals_to_kill_on, nameof(stop_component_processing_entirely));
        }

        if (Dm.truthy(scale_with_happiness))
        {
            await parent.AddComponent(typeof(datum_component_happiness));
            RegisterSignal(parent, COMSIG_MOB_HAPPINESS_CHANGE, nameof(on_happiness_change));
        }

        // If we haven't started the round, we can't do timer stuff. Let's wait in case we're mapped in or something.
        if (!Dm.truthy(SSticker.HasRoundStarted()) && growth_time != null)
        {
            RegisterSignal(SSticker, COMSIG_TICKER_ROUND_STARTING, nameof(comp_on_round_start));
            return null;
        }

        return setup_growth_tracking();
    }

    // tg: code/datums/components/growth_and_differentiation.dm:73
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSdcs, this);
        deltimer(timer_id);
        optional_checks = null;
        optional_grow_behavior = null;
        return base.Destroy(force);
    }

    /// Wrapper for qdel() so we can pass it in RegisterSignals(). I hate it here too.
    // tg: code/datums/components/growth_and_differentiation.dm:81
    [SignalHandler]
    public virtual void stop_component_processing_entirely()
    {
        qdel(this);
    }

    /// What we invoke when the round starts so we can set up our timer.
    // tg: code/datums/components/growth_and_differentiation.dm:86
    [SignalHandler]
    public virtual void comp_on_round_start()
    {
        setup_growth_tracking();
        UnregisterSignal(SSticker, COMSIG_TICKER_ROUND_STARTING);
    }

    /// Sets up the two different systems for growth: the timer and the probability based one. Both can coexist. Return COMPONENT_INCOMPATIBLE if we fail to set up either.
    // tg: code/datums/components/growth_and_differentiation.dm:92
    public virtual object setup_growth_tracking()
    {
        bool did_we_add_at_least_one_thing = false;

        if (growth_time != null)
        {
            timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(grow), FALSE), Dm.num(growth_time), TIMER_STOPPABLE, file: "code/datums/components/growth_and_differentiation.dm", line: 96);
            if (timer_id != null) // realistically shouldn't happen considering how hardy addtimer() is but you can never be too sure
                did_we_add_at_least_one_thing = true;
        }

        if (growth_probability != null)
        {
            START_PROCESSING(SSdcs, this);
            did_we_add_at_least_one_thing = true;
        }

        if (!did_we_add_at_least_one_thing)
        {
            stack_trace("Growth and Differentiation Component: Neither growth time nor probability were set! This component is useless!", "code/datums/components/growth_and_differentiation.dm", 105);
            return COMPONENT_INCOMPATIBLE; // if we're invoked via COMSIG_TICKER_ROUND_STARTING this won't do anything (and shouldn't be invoked since we nullcheck growth_time before adding that signal anyways)
        }

        return null; // just for explicitness's sake, if they ever change Component's Initialize to have more return values make sure this is the one for "Success!"
    }

    // tg: code/datums/components/growth_and_differentiation.dm:110
    public override object process(object seconds_per_tick = null, params object[] __extra) // check the prob we were passed in, and if we're lucky, grow!
    {
        if (Dm.truthy(ready_to_grow))
        {
            Waiting.Detach(grow(FALSE));
            return null;
        }

        if (percent_grown >= 100)
        {
            ready_to_grow = TRUE;
            Waiting.Detach(grow(FALSE)); // lets not waste any more of SSmobs time this tick.
            return null;
        }

        if (SPT_PROB(Dm.num(growth_probability), seconds_per_tick))
        {
            if (Dm.eq(lower_growth_value, upper_growth_value))
                percent_grown += Dm.num(upper_growth_value);
            else
                percent_grown += rand(Dm.num(lower_growth_value), Dm.num(upper_growth_value));
        }
        return null;
    }

    // tg: code/datums/components/growth_and_differentiation.dm:126
    [SignalHandler]
    public virtual void on_happiness_change(datum source = null, object happiness_percentage = null)
    {
        float probability_to_add = Dm.num(initial_growth_probability) * Dm.num(happiness_percentage);
        growth_probability = Dm.min(Dm.num(initial_growth_probability) + probability_to_add, 100);
    }

    /// Grows the mob into its new form.
    // tg: code/datums/components/growth_and_differentiation.dm:133
    public virtual async DmTask grow(object silent = null)
    {
        if (optional_checks != null && !Dm.truthy(await optional_checks.Invoke())) // we failed our checks somehow, but we're still ready to grow. Let's wait until next tick to see if our circumstances have changed.
        {
            ready_to_grow = TRUE;
            return;
        }

        mob_living old_mob = parent as mob_living;
        if (old_mob.stat == DEAD)
        {
            qdel(this); // assume that we are priced out of growth once dead
            return;
        }

        STOP_PROCESSING(SSdcs, this);

        if (optional_grow_behavior != null) // basically growth_path is OK to be null but only if we have an optional grow behavior.
        {
            await optional_grow_behavior.Invoke();
            return;
        }

        if (!ispath(growth_path, typeof(mob_living)))
            throw Dm.CRASH("Growth and Differentiation Component: Growth path was not a mob type! If you wanted to do something special, please put it in the optional_grow_behavior callback instead!");

        object new_mob_name = initial(growth_path, "name");

        if (!Dm.truthy(silent))
            old_mob.visible_message(span_warning(Dm.text("[] grows into \\a []!", old_mob, new_mob_name)));

        mob_living transformed_mob = await old_mob.change_mob_type(growth_path, old_mob.loc, new_name: new_mob_name, delete_old_mob: TRUE) as mob_living;
        if (Dm.truthy(initial(growth_path, "unique_name")))
            transformed_mob.set_name();
        ADD_TRAIT(transformed_mob, TRAIT_MOB_HATCHED, INNATE_TRAIT);
    }
}
