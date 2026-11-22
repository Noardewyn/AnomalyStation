// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs/stop_drop_roll.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Whole (M4b slice 3: a burning carbon resists to put itself out; M8 slice G: the /hallucinating subtype, the fire hallucination).

// tg: code/datums/status_effects/buffs/stop_drop_roll.dm:1
public partial class datum_status_effect_stop_drop_roll : datum_status_effect
{
    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:11
    [DmParams("new_owner", "ignore_immobilize")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object ignore_immobilize = Dm.arg(__extra, 0) ?? FALSE;
        this.ignore_immobilize = Dm.num(ignore_immobilize);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:15
    [DmParams()]
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!iscarbon(owner))
            return FALSE;

        if (!Dm.truthy(await owner.Knockdown(tick_interval * 2, ignore_canstun: TRUE)) || owner.body_position != LYING_DOWN)
        {
            to_chat(owner, span_warning(I18n.T("You try to stop, drop, and roll - but you can't get on the ground!")));
            return FALSE;
        }

        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(stop_rolling));
        RegisterSignal(owner, COMSIG_LIVING_SET_BODY_POSITION, nameof(body_position_changed));
        ADD_TRAIT(owner, TRAIT_HANDS_BLOCKED, TRAIT_STATUS_EFFECT(id)); // they're kinda busy!

        bar = Dm.@new<datum_progressbar>(owner, MAX_FIRE_STACKS, owner, get_bar_progress());
        await start_rolling();

        foreach (obj_item dropped in Dm.each<obj_item>(owner.loc?.contents))
            await dropped.extinguish(); // Effectively extinguish your items by rolling on them
        return TRUE;
    }

    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:34
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_LIVING_SET_BODY_POSITION));
        REMOVE_TRAIT(owner, TRAIT_HANDS_BLOCKED, TRAIT_STATUS_EFFECT(id));
        bar.end_progress();
        bar = null;
        return null;
    }

    /// Get the current progress for the progress bar
    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:41
    public virtual object get_bar_progress()
    {
        return MAX_FIRE_STACKS - max(0, owner.fire_stacks);
    }

    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:44
    public virtual async DmTask start_rolling()
    {
        owner.visible_message(
            span_danger(I18n.T("[] rolls on the floor, trying to put []self out!", owner, owner.p_them())),
            span_notice(I18n.T("You stop, drop, and roll!"))
        );
        // Start with one weaker roll
        await reduce_firestacks(0.25f);
    }

    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:52
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if ((!Dm.truthy(ignore_immobilize) && HAS_TRAIT(owner, TRAIT_IMMOBILIZED)) || HAS_TRAIT(owner, TRAIT_INCAPACITATED))
        {
            qdel(this);
            return null;
        }

        if (!Dm.truthy(await owner.Knockdown(tick_interval * 1.2f, ignore_canstun: TRUE)))
        {
            stop_rolling();
            return null;
        }

        owner.spin(spintime: tick_interval, speed: tick_interval / 4);
        if (!Dm.truthy(await reduce_firestacks(1)))
            return null;

        stop_rolling_successful();
        return null;
    }

    /// Return TRUE to stop the us from rolling.
    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:68
    public virtual async DmTask<object> reduce_firestacks(float? amt = null)
    {
        amt ??= 1;
        await owner.adjust_fire_stacks(-1 * amt.Value);
        bar.update(get_bar_progress());
        return owner.fire_stacks <= 0 ? TRUE : FALSE;
    }

    /// Called when we just, stop rolling, due to movement or other reasons. Maybe still on fire, maybe not.
    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:74
    [SignalHandler]
    public virtual void stop_rolling(datum source = null, params object[] __extra)
    {
        // SIGNAL_HANDLER

        if (!QDELING(owner))
            to_chat(owner, span_notice(I18n.T("You stop rolling around.")));
        qdel(this);
    }

    /// Called when we've successfully extinguished ourselves.
    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:82
    public virtual void stop_rolling_successful()
    {
        owner.visible_message(
            span_danger(I18n.T("[] successfully extinguishes []self!", owner, owner.p_them())),
            span_notice(I18n.T("You extinguish yourself."))
        );
        qdel(this);
    }

    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:89
    [SignalHandler]
    public virtual void body_position_changed(datum source = null, object new_value = null, object old_value = null)
    {
        // SIGNAL_HANDLER

        if (!Dm.eq(new_value, LYING_DOWN))
            stop_rolling();
    }
}

/// Subtype of rolling triggered when someone hallucinating fire tries to stop, drop, and roll.
// tg: code/datums/status_effects/buffs/stop_drop_roll.dm:96
public partial class datum_status_effect_stop_drop_roll_hallucinating : datum_status_effect_stop_drop_roll
{
    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:101
    [DmParams("new_owner", "hallucination_weakref")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        datum_weakref hallucination_weakref = Dm.arg<datum_weakref>(__extra, 0);
        this.hallucination_weakref = hallucination_weakref;
        return await base.on_creation(new_owner, Dm.forward(__extra, hallucination_weakref));
    }

    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:105
    public override object get_bar_progress()
    {
        datum_hallucination_fire hallucination = hallucination_weakref?.resolve() as datum_hallucination_fire;
        return 20 - max(0, hallucination?.fake_firestacks ?? 0);
    }

    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:109
    public override async DmTask start_rolling()
    {
        owner.visible_message(
            span_danger(I18n.T("[] starts rolling around on the floor, flailing about!", owner)),
            span_notice(I18n.T("You stop, drop, and roll!")));
        await reduce_firestacks(1); // more effective cause it's not real
    }

    // tg: code/datums/status_effects/buffs/stop_drop_roll.dm:116
    public override async DmTask<object> reduce_firestacks(float? amt = null)
    {
        amt ??= 1;
        datum_hallucination_fire hallucination = hallucination_weakref?.resolve() as datum_hallucination_fire;
        if (hallucination == null)
            return TRUE;

        hallucination.fake_firestacks += (-1 * amt.Value);
        bar.update(get_bar_progress());
        if (hallucination.fake_firestacks <= 0)
        {
            hallucination.clear_fire();
            return TRUE;
        }
        return FALSE;
    }
}
