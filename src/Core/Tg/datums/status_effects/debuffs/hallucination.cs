// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/hallucination.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The sanity variant starts with a crazy or insane sanity (gaps MS1). The var block is generated (D111).

/// Hallucination status effect. How most hallucinations end up happening.
/// Hallucinations are drawn from the global weighted list, random_hallucination_weighted_list
// tg: code/datums/status_effects/debuffs/hallucination.dm:3
public partial class datum_status_effect_hallucination : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/hallucination.dm:25
    [DmParams("new_owner", "new_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object new_duration = Dm.arg(__extra, 0);
        if (isnum(new_duration))
            this.duration = Dm.num(new_duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, new_duration));
    }

    // tg: code/datums/status_effects/debuffs/hallucination.dm:30
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(owner.mob_biotypes, barred_biotypes)))
            return FALSE;
        if (HAS_TRAIT(owner, TRAIT_HALLUCINATION_IMMUNE))
            return FALSE;

        RegisterSignal(owner, COMSIG_LIVING_HEALTHSCAN, nameof(on_health_scan));
        RegisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_HALLUCINATION_IMMUNE), nameof(delete_self));
        if (iscarbon(owner))
        {
            RegisterSignal(owner, COMSIG_CARBON_CHECKING_BODYPART, nameof(on_check_bodypart));
            RegisterSignal(owner, COMSIG_CARBON_BUMPED_AIRLOCK_OPEN, nameof(on_bump_airlock));
        }

        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/hallucination.dm:45
    [SignalHandler]
    public virtual void delete_self()
    {
        qdel(this);
    }

    // tg: code/datums/status_effects/debuffs/hallucination.dm:49
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, Dm.list(
            COMSIG_LIVING_HEALTHSCAN,
            COMSIG_CARBON_CHECKING_BODYPART,
            COMSIG_CARBON_BUMPED_AIRLOCK_OPEN,
            SIGNAL_ADDTRAIT(TRAIT_HALLUCINATION_IMMUNE)));
        return null;
    }

    /// Signal proc for [COMSIG_LIVING_HEALTHSCAN]. Show we're hallucinating to (advanced) scanners.
    // tg: code/datums/status_effects/debuffs/hallucination.dm:58
    [SignalHandler]
    public virtual void on_health_scan(datum source = null, DmList render_list = null, object scanpower = null, mob user = null, object mode = null, object tochat = null)
    {
        if (Dm.num(scanpower) <= SCANPOWER_BASIC)
            return;
        Dm.append(render_list, "<span class='info ml-1'>");
        Dm.append(render_list, conditional_tooltip("Subject is hallucinating.", Dm.text("Supply antipsychotic medication, such as [] or [].", Dm.initial(typeof(datum_reagent_medicine_haloperidol), (datum_reagent p) => p.name), Dm.initial(typeof(datum_reagent_medicine_synaptizine), (datum_reagent p) => p.name)), tochat));
        Dm.append(render_list, "</span><br>");
    }

    /// Signal proc for [COMSIG_CARBON_CHECKING_BODYPART],
    /// checking bodyparts while hallucinating can cause them to appear more damaged than they are
    // tg: code/datums/status_effects/debuffs/hallucination.dm:68
    [SignalHandler]
    public virtual void on_check_bodypart(mob_living_carbon source = null, obj_item_bodypart examined = null, DmList check_list = null, DmList limb_damage = null)
    {
        if (prob(30))
            limb_damage[BRUTE] = Dm.num(limb_damage[BRUTE]) + rand(30, 40);
        if (prob(30))
            limb_damage[BURN] = Dm.num(limb_damage[BURN]) + rand(30, 40);
    }

    /// Signal proc for [COMSIG_CARBON_BUMPED_AIRLOCK_OPEN], bumping an airlock can cause a fake zap.
    /// This only happens on airlock bump, future TODO - make this chance roll for attack_hand opening airlocks too
    // tg: code/datums/status_effects/debuffs/hallucination.dm:78
    [SignalHandler]
    public virtual object on_bump_airlock(mob_living_carbon source = null, obj_machinery_door_airlock bumped = null)
    {
        // 1% chance to fake a shock.
        if (prob(99) || !Dm.truthy(source.should_electrocute()) || Dm.truthy(bumped.operating))
            return null;

        Waiting.NoSleep(source.cause_hallucination(typeof(datum_hallucination_shock), Dm.text("hallucinated shock from []", bumped)));
        return STOP_BUMP;
    }

    // tg: code/datums/status_effects/debuffs/hallucination.dm:88
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (owner.stat == DEAD)
            return null;
        if (!(hallucination_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return null;

        float lower_cd = lower_tick_interval;
        float upper_cd = upper_tick_interval;
        if (Dm.truthy(variable_tier))
        {
            float seconds_left = Dm.num(duration) / 10;
            if (seconds_left >= 0 && seconds_left <= 20)
            {
                max_hallucination_tier = HALLUCINATION_TIER_COMMON;
                lower_tick_interval *= 1.2f;
                upper_tick_interval *= 1.2f;
            }
            else if (seconds_left >= 20 && seconds_left <= 60)
                max_hallucination_tier = prob(10) ? HALLUCINATION_TIER_RARE : HALLUCINATION_TIER_UNCOMMON;
            else if (seconds_left >= 60 && seconds_left <= 120)
            {
                max_hallucination_tier = HALLUCINATION_TIER_RARE;
                lower_cd *= 0.75f;
                upper_cd *= 0.75f;
            }
            else if (seconds_left >= 120 && seconds_left <= INFINITY)
            {
                max_hallucination_tier = HALLUCINATION_TIER_VERYSPECIAL;
                lower_cd *= 0.5f;
                upper_cd *= 0.5f;
            }
        }

        object picked_hallucination = await get_random_hallucination(max_hallucination_tier, strict_tier);
        if (!Dm.truthy(await owner.cause_hallucination(picked_hallucination, Dm.text("[] status effect", id))))
        {
            lower_cd *= 0.25f;
            upper_cd *= 0.25f;
        }
        hallucination_cooldown = world.time + rand(lower_cd, upper_cd); // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
        return null;
    }
}

// Sanity related hallucinations
// tg: code/datums/status_effects/debuffs/hallucination.dm:121
public partial class datum_status_effect_hallucination_sanity : datum_status_effect_hallucination
{
    // tg: code/datums/status_effects/debuffs/hallucination.dm:128
    [SignalHandler]
    public override void on_health_scan(datum source = null, DmList render_list = null, object scanpower = null, mob user = null, object mode = null, object tochat = null)
    {
        return;
    }

    // tg: code/datums/status_effects/debuffs/hallucination.dm:131
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (owner.mob_mood == null)
            return FALSE;

        update_intervals();
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/debuffs/hallucination.dm:138
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        update_intervals();
    }

    // tg: code/datums/status_effects/debuffs/hallucination.dm:141
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        // Using psicodine / happiness / whatever to become fearless will stop sanity based hallucinations
        if (HAS_TRAIT(owner, TRAIT_FEARLESS))
            return null;

        return await base.tick(seconds_between_ticks);
    }

    /// Updates our upper and lower intervals based on our owner's current sanity level.
    // tg: code/datums/status_effects/debuffs/hallucination.dm:149
    public virtual void update_intervals()
    {
        if (owner.mob_mood.sanity_level == SANITY_LEVEL_CRAZY)
        {
            upper_tick_interval = 8 * MINUTES;
            lower_tick_interval = 4 * MINUTES;
        }
        else if (owner.mob_mood.sanity_level == SANITY_LEVEL_INSANE)
        {
            upper_tick_interval = 4 * MINUTES;
            lower_tick_interval = 2 * MINUTES;
        }
        else
        {
            stack_trace(Dm.text("[] was assigned a mob which was not crazy or insane. (was: [])", type, owner.mob_mood.sanity_level), "code/datums/status_effects/debuffs/hallucination.dm", 160);
            qdel(this);
        }
    }
}

// tg: code/datums/status_effects/debuffs/hallucination.dm:165
public partial class datum_status_effect_hallucination_perceptomatrix : datum_status_effect_hallucination
{
    // tg: code/datums/status_effects/debuffs/hallucination.dm:171
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        object new_duration = Dm.arg(__extra, 0);
        this.duration = Dm.num(this.duration) + Dm.num(new_duration);
    }

    // tg: code/datums/status_effects/debuffs/hallucination.dm:174
    [DmParams("new_owner", "new_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object new_duration = Dm.arg(__extra, 0);
        if (isnum(new_duration))
        {
            this.lower_tick_interval = Dm.num(new_duration) * 0.2f;
            this.upper_tick_interval = Dm.num(new_duration);
        }
        return await base.on_creation(new_owner, Dm.forward(__extra, new_duration));
    }
}
