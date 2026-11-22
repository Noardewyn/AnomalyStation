// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/staggered.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Staggered, Next Shove Stuns, No Side Kick
/// Status effects related to shoving effects and collisions due to shoving

/// Staggered can occur most often via shoving, but can also occur in other places too.
// tg: code/datums/status_effects/debuffs/staggered.dm:5
[DmPath("/datum/status_effect/staggered")]
public partial class datum_status_effect_staggered : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/staggered.dm:6-9  the var overrides of the type block (rules 10.2)
    public datum_status_effect_staggered()
    {
        id = "staggered";
        tick_interval = 0.8f * SECONDS;
        alert_type = null;
        remove_on_fullheal = TRUE;
    }

    // tg: code/datums/status_effects/debuffs/staggered.dm:11
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/staggered.dm:15
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        //you can't stagger the dead.
        if (owner.stat == DEAD || HAS_TRAIT(owner, TRAIT_NO_STAGGER))
            return FALSE;

        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(clear_staggered));
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_staggered));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/staggered.dm:24
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_DEATH);
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_staggered));
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/staggered.dm:29
public partial class datum_status_effect_staggered : datum_status_effect
{
    /// Signal proc that self deletes our staggered effect
    // tg: code/datums/status_effects/debuffs/staggered.dm:29
    [SignalHandler]
    public virtual void clear_staggered(datum source = null)
    {

        qdel(this);
    }

    // tg: code/datums/status_effects/debuffs/staggered.dm:34
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        //you can't stagger the dead - in case somehow you die mid-stagger
        if (owner.stat == DEAD || HAS_TRAIT(owner, TRAIT_NO_STAGGER))
        {
            qdel(this);
            return null;
        }
        if (HAS_TRAIT(owner, TRAIT_FAKEDEATH))
            return null;
        Waiting.DetachSync(() => owner.do_stagger_animation());
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/staggered.dm:45
public partial class mob_living : mob
{
    /// Helper proc that causes the mob to do a stagger animation.
    /// Doesn't change significantly, just meant to represent swaying back and forth
    // tg: code/datums/status_effects/debuffs/staggered.dm:45
    public virtual void do_stagger_animation()
    {
        animate(this, Dm.named("pixel_w", 3), Dm.named("time", 0.2f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
        animate(Dm.named("pixel_w", -6), Dm.named("time", 0.4f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_w", 3), Dm.named("time", 0.2f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
    }
}

/// Status effect specifically for instances where someone is vulnerable to being stunned when shoved.
// tg: code/datums/status_effects/debuffs/staggered.dm:51
[DmPath("/datum/status_effect/dazed")]
public partial class datum_status_effect_dazed : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/staggered.dm:52-56  the var overrides of the type block (rules 10.2)
    public datum_status_effect_dazed()
    {
        id = "dazed";
        status_type = STATUS_EFFECT_UNIQUE;
        tick_interval = 0.6f * SECONDS;
        alert_type = null;
        remove_on_fullheal = TRUE;
    }

    // tg: code/datums/status_effects/debuffs/staggered.dm:60
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 3 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/staggered.dm:64
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        //Let's just clear this if they're dead or we can't stun them on a shove
        if (owner.stat == DEAD || HAS_TRAIT(owner, TRAIT_NO_SIDE_KICK) || HAS_TRAIT(owner, TRAIT_IMMOBILIZED))
            return FALSE;
        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(clear_daze_on_death));
        RegisterSignal(owner, COMSIG_LIVING_SET_BODY_POSITION, nameof(clear_daze_on_stand));
        RegisterSignals(owner, Dm.list(
            COMSIG_LIVING_STATUS_PARALYZE,
            COMSIG_LIVING_STATUS_STUN,
            COMSIG_LIVING_STATUS_IMMOBILIZE), nameof(clear_daze)
        );
        ADD_TRAIT(owner, TRAIT_DAZED, TRAIT_STATUS_EFFECT(id));
        dazed_overlay = mutable_appearance(icon: Dm.resource("icons/effects/effects.dmi"), icon_state: "dazed") as mutable_appearance;
        owner.add_overlay(dazed_overlay);
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/staggered.dm:80
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, Dm.list(
            COMSIG_LIVING_DEATH,
            COMSIG_LIVING_SET_BODY_POSITION,
            COMSIG_LIVING_STATUS_PARALYZE,
            COMSIG_LIVING_STATUS_STUN,
            COMSIG_LIVING_STATUS_IMMOBILIZE
        ));
        REMOVE_TRAIT(owner, TRAIT_DAZED, TRAIT_STATUS_EFFECT(id));
        if (Dm.truthy(dazed_overlay))
            clear_dazed_overlay();
        return null;
    }

    /// Our visual cue for the vulnerable state this status effect puts us in.
    // tg: code/datums/status_effects/debuffs/staggered.dm:58
    public mutable_appearance dazed_overlay;
}

// tg: code/datums/status_effects/debuffs/staggered.dm:94
public partial class datum_status_effect_dazed : datum_status_effect
{
    /// If our owner is either stunned, paralzyed or immobilized, we remove the status effect.
    /// This is both an anti-chainstun measure and a sanity check.
    // tg: code/datums/status_effects/debuffs/staggered.dm:94
    [SignalHandler]
    public virtual void clear_daze(mob_living source = null, float? amount = null, float? ignore_canstun = null)
    {
        amount ??= 0;
        ignore_canstun ??= FALSE;

        if (Dm.num(amount) > 0)
        {
            // Making absolutely sure we're removing this overlay
            clear_dazed_overlay();
            qdel(this);
        }
    }

    // tg: code/datums/status_effects/debuffs/staggered.dm:102
    [SignalHandler]
    public virtual void clear_daze_on_stand(mob_living source = null, object new_position = null)
    {

        if (Dm.eq(new_position, STANDING_UP))
        {
            clear_dazed_overlay();
            qdel(this);
        }
    }

    // tg: code/datums/status_effects/debuffs/staggered.dm:109
    [SignalHandler]
    public virtual void clear_daze_on_death(mob_living source = null)
    {

        clear_dazed_overlay();
        qdel(this);
    }

    /// Clears our overlay where needed.
    // tg: code/datums/status_effects/debuffs/staggered.dm:116
    public virtual void clear_dazed_overlay()
    {
        owner.cut_overlay(dazed_overlay);
        dazed_overlay = null;
    }
}

/// Status effect to prevent stuns from a shove
/// Only applied by shoving someone to paralyze them
// tg: code/datums/status_effects/debuffs/staggered.dm:122
[DmPath("/datum/status_effect/no_side_kick")]
public partial class datum_status_effect_no_side_kick : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/staggered.dm:123-128  the var overrides of the type block (rules 10.2)
    public datum_status_effect_no_side_kick()
    {
        id = "no side kick";
        duration = 3.6f * SECONDS;
        status_type = STATUS_EFFECT_UNIQUE;
        tick_interval = 0.6f * SECONDS;
        alert_type = null;
        remove_on_fullheal = TRUE;
    }

    // tg: code/datums/status_effects/debuffs/staggered.dm:130
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        // Once again, clear if dead
        if (owner.stat == DEAD)
            return FALSE;
        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(clear_on_death));
        ADD_TRAIT(owner, TRAIT_NO_SIDE_KICK, TRAIT_STATUS_EFFECT(id));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/staggered.dm:138
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, Dm.list(COMSIG_LIVING_DEATH));
        REMOVE_TRAIT(owner, TRAIT_NO_SIDE_KICK, TRAIT_STATUS_EFFECT(id));
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/staggered.dm:142
public partial class datum_status_effect_no_side_kick : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/staggered.dm:142
    [SignalHandler]
    public virtual void clear_on_death(mob_living source = null)
    {

        qdel(this);
    }
}
