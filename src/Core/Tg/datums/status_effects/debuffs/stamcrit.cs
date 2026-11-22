// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/stamcrit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M2 integration: a carbon whose stamina damage reaches maxHealth - crit_threshold enters stamina crit
// (carbon/damage_procs.dm:112).

// tg: code/datums/status_effects/debuffs/stamcrit.dm:1
[DmPath("/datum/status_effect/incapacitating/stamcrit")]
public partial class datum_status_effect_incapacitating_stamcrit : datum_status_effect_incapacitating
{
    // tg: code/datums/status_effects/debuffs/stamcrit.dm:2-5  the var overrides of the type block (rules 10.2)
    public datum_status_effect_incapacitating_stamcrit()
    {
        id = "stamcrit";
        status_type = STATUS_EFFECT_UNIQUE;
        // Lasts until we go back to 0 stamina, which is handled by the mob
        duration = STATUS_EFFECT_PERMANENT;
        tick_interval = STATUS_EFFECT_NO_TICK;
    }

    /// Cooldown between displaying warning messages that we hit diminishing returns
    // tg: code/datums/status_effects/debuffs/stamcrit.dm:8  COOLDOWN_DECLARE(warn_cd) (__DEFINES/cooldowns.dm: var/warn_cd = 0)
    public float warn_cd = 0;
    /// A counter that tracks every time we've taken enough damage to trigger diminishing returns
    // tg: code/datums/status_effects/debuffs/stamcrit.dm:10
    public float diminishing_return_counter = 0;

    // tg: code/datums/status_effects/debuffs/stamcrit.dm:12
    [DmParams("new_owner", "set_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object set_duration = Dm.arg(__extra, 0);
        object dot = null;
        dot = await base.on_creation(new_owner, Dm.forward(__extra, set_duration));
        if (!Dm.truthy(dot))
            return dot;

        // This should be in on apply but we need it to happen AFTER being added to the mob
        // (Because we need to wait until the status effect is in their status effect list, or we'll add two)
        if (Dm.lt(owner.get_stamina_loss(), 120))
        {
            // Puts you a little further into the initial stamcrit, makes stamcrit harder to outright counter with chems.
            await owner.adjust_stamina_loss(30, FALSE);
        }

        // Same
        RegisterSignal(owner, COMSIG_LIVING_ADJUST_STAMINA_DAMAGE, nameof(update_diminishing_return));
        RegisterSignal(owner, COMSIG_LIVING_STAMINA_UPDATE, nameof(check_remove));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/stamcrit.dm:27
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = null;
        if (owner.stat == DEAD)
            return FALSE;
        if (Dm.truthy(owner.check_stun_immunity(CANSTUN)))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(owner._listen_lookup?[COMSIG_LIVING_ENTER_STAMCRIT]) ? NONE : owner._SendSignal(COMSIG_LIVING_ENTER_STAMCRIT, Dm.list(owner))), STAMCRIT_CANCELLED)))
            return FALSE;

        dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;

        if (!IS_UNCONSCIOUS_OR_CRIT(owner))
            to_chat(owner, span_notice("You're too exhausted to keep going..."));
        owner.add_traits(Dm.list(TRAIT_INCAPACITATED, TRAIT_IMMOBILIZED, TRAIT_FLOORED), STAMINA);
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/stamcrit.dm:44
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_HEALTH_UPDATE);
        UnregisterSignal(owner, COMSIG_LIVING_ADJUST_STAMINA_DAMAGE);
        owner.remove_traits(Dm.list(TRAIT_INCAPACITATED, TRAIT_IMMOBILIZED, TRAIT_FLOORED), STAMINA);
        return await base.on_remove();
    }

    // tg: code/datums/status_effects/debuffs/stamcrit.dm:50
    [SignalHandler]
    public virtual object update_diminishing_return(datum source = null, object type = null, object amount = null, object forced = null)
    {
        if (Dm.le(amount, 0) || Dm.truthy(forced))
            return NONE;
        // Here we fake the effect of having diminishing returns
        // We don't actually decrease incoming stamina damage because that would be pointless, the mob is at stam damage cap anyways
        // Instead we just "ignore" the damage if we have a sufficiently high diminishing return counter
        float mod_amount = ceil(sqrt(Dm.num(amount)) / 2) - diminishing_return_counter;
        // We check base amount not mod_amount because we still want to up tick it even if we've already got a high counter
        // We also only uptick it after calculating damage so we start ticking up after the damage and not before
        float sw__1 = Dm.num(amount);
        if (sw__1 >= 5)
            diminishing_return_counter += 1;
        else if (sw__1 >= 2 && sw__1 <= 5) // Prevent chems from skyrockting DR
            diminishing_return_counter += 0.05f;
        if (mod_amount > 0)
            return NONE;

        // COOLDOWN_FINISHED(src, warn_cd) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (this.warn_cd <= world.time && !IS_UNCONSCIOUS_OR_CRIT(owner))
        {
            to_chat(owner, span_notice("You start to recover from the exhaustion!"));
            owner.visible_message(span_warning(I18n.T("[] starts to recover from the exhaustion!", owner)), ignored_mobs: owner);
            // COOLDOWN_START(src, warn_cd, 2.5 SECONDS) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
            this.warn_cd = world.time + (2.5f * SECONDS);
        }

        return COMPONENT_IGNORE_CHANGE;
    }

    // tg: code/datums/status_effects/debuffs/stamcrit.dm:75
    [SignalHandler]
    public virtual void check_remove(datum source = null)
    {
        if (isbasicmob(owner))
        {
            // tg: :78-81  a basic mob leaves stamina crit below its stamina_crit_threshold
            // GAP(lead): /mob/living/basic's staminaloss and stamina_crit_threshold are not declared (basic mobs, a later
            // milestone); no basic mob takes stamina damage in M2.
            Dm.NotTranslated("code/datums/status_effects/debuffs/stamcrit.dm:78", "check_remove: a basic mob's stamina_crit_threshold (basic mobs, later)");
            return;
        }

        if (owner.maxHealth - Dm.num(owner.get_stamina_loss()) > owner.crit_threshold)
            qdel(this);
    }
}
