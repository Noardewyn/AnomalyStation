// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/fakedeath.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Reviving Stasis (the fake death and stasis, the revival readied after 40 seconds, eight times
// longer when gutted, the revival with the missing limbs but the head regrown, the early exit when revived by other means, the
// button that turns into Revive). The var block is generated (D111); revive's static list is a TgState field (rules 10.6).

// tg: code/modules/antagonists/changeling/powers/fakedeath.dm:1
public partial class datum_action_changeling_fakedeath : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:107  revive's var/static/list/dont_regenerate
    private static ref DmList revive__dont_regenerate => ref TgState.Current.datum_action_changeling_fakedeath__revive__dont_regenerate;

    //Fake our own death and fully heal. You will appear to be dead but regenerate fully after a short delay.
    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:19
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);
        if (Dm.truthy(revive_ready))
        {
            Waiting.Detach(revive(user as mob_living_carbon));
            return TRUE;
        }

        float death_duration_mod = 1;
        if (Dm.truthy(user.has_status_effect(typeof(datum_status_effect_gutted))))
            death_duration_mod *= 8; // Anti-megafauna cheese

        if (!Dm.truthy(await enable_fakedeath(user, duration_modifier: death_duration_mod)))
            throw Dm.CRASH("Changeling revive failed to enter fakedeath when it should have been in a valid state to.", "code/modules/antagonists/changeling/powers/fakedeath.dm", 30);

        to_chat(user, span_changeling(I18n.T("We begin our stasis, preparing energy to arise once more.")));
        if (death_duration_mod > 1)
            to_chat(user, span_changeling(span_bold(I18n.T("Our body has sustained severe damage, and will take []longer to regenerate.", death_duration_mod >= 5 ? I18n.T("far ") : ""))));
        return TRUE;
    }

    /// Used to enable fakedeath and register relevant signals / start timers
    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:38
    public virtual async DmTask<object> enable_fakedeath(mob_living changeling = null, float? duration_modifier = null)
    {
        duration_modifier ??= 1;
        if (Dm.truthy(revive_ready) || HAS_TRAIT_FROM(changeling, TRAIT_DEATHCOMA, CHANGELING_TRAIT))
            return null;

        await changeling.fakedeath(CHANGELING_TRAIT);
        ADD_TRAIT(changeling, TRAIT_STASIS, CHANGELING_TRAIT);
        _addtimer(Dm.@new<datum_callback>(this, nameof(ready_to_regenerate), changeling), fakedeath_duration * Dm.num(duration_modifier), TIMER_UNIQUE, file: "code/modules/antagonists/changeling/powers/fakedeath.dm", line: 44);
        // Basically, these let the ling exit stasis without giving away their ling-y-ness if revived through other means
        RegisterSignal(changeling, SIGNAL_REMOVETRAIT(TRAIT_DEATHCOMA), nameof(fakedeath_reset));
        RegisterSignal(changeling, COMSIG_MOB_STATCHANGE, nameof(on_stat_change));
        return TRUE;
    }

    /// Removes the signals for fakedeath and listening for hapless doctors
    /// healing a changeling who went into stasis after actually dying, and
    /// also removes changeling stasis
    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:53
    public virtual void disable_stasis_and_fakedeath(mob_living changeling = null)
    {
        REMOVE_TRAIT(changeling, TRAIT_STASIS, CHANGELING_TRAIT);
        UnregisterSignal(changeling, SIGNAL_REMOVETRAIT(TRAIT_DEATHCOMA));
        UnregisterSignal(changeling, COMSIG_MOB_STATCHANGE);
    }

    /// This proc is called to reset the chemical cost of the revival
    /// as well as the revive ready flag and button states.
    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:60
    public virtual async DmTask reset_chemical_cost()
    {
        chemical_cost = 15;
        revive_ready = FALSE;
        await build_all_button_icons(Dm.bitor(UPDATE_BUTTON_NAME, UPDATE_BUTTON_ICON));
    }

    /// Sets [revive_ready] to TRUE and updates the button icons.
    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:66
    public virtual async DmTask enable_revive(mob_living changeling = null)
    {
        if (Dm.truthy(revive_ready))
            return;

        chemical_cost = 0;
        revive_ready = TRUE;
        await build_all_button_icons(Dm.bitor(UPDATE_BUTTON_NAME, UPDATE_BUTTON_ICON));
    }

    /// Signal proc to stop the revival process if the changeling exits their stasis early.
    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:75
    [SignalHandler]
    public virtual void fakedeath_reset(mob_living source = null)
    {
        if (HAS_TRAIT_FROM(source, TRAIT_DEATHCOMA, CHANGELING_TRAIT))
            return;

        disable_stasis_and_fakedeath(source);
    }

    /// Signal proc to exit fakedeath early if we're revived from being previously dead
    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:84
    [SignalHandler]
    public virtual void on_stat_change(mob_living source = null, object new_stat = null, object old_stat = null)
    {
        if (!Dm.eq(old_stat, DEAD))
            return;

        source.cure_fakedeath(CHANGELING_TRAIT);
        to_chat(source, span_changeling(I18n.T("We exit our stasis early.")));
        Waiting.NoSleep(reset_chemical_cost());
    }

    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:94
    public virtual async DmTask revive(mob_living_carbon user = null)
    {
        if (!(user is mob_living_carbon))
            return;
        if (!HAS_TRAIT_FROM(user, TRAIT_DEATHCOMA, CHANGELING_TRAIT))
            return;

        user.cure_fakedeath(CHANGELING_TRAIT);
        // Heal all damage and some minor afflictions,
        float flags_to_heal = Dm.bitor(Dm.bitor(Dm.bitor(HEAL_DAMAGE, HEAL_BODY), HEAL_STATUS), HEAL_CC_STATUS);
        // but leave out limbs so we can do it specially
        await user.revive(Dm.bitand(flags_to_heal, Dm.bitnot(HEAL_LIMBS)));
        to_chat(user, span_changeling(I18n.T("We have revived ourselves.")));

        // headless changelings are funny
        if (!Dm.truthy(length(Dm.sub(user.get_missing_limbs(), revive__dont_regenerate))))
            return;

        playsound(user, Dm.resource("sound/effects/magic/demon_consume.ogg"), 50, TRUE);
        user.visible_message(
            span_warning(I18n.T("[]'s missing limbs reform, making a loud, grotesque sound!", user)),
            span_userdanger(I18n.T("Your limbs regrow, making a loud, crunchy sound and giving you great pain!")),
            span_hear(I18n.T("You hear organic matter ripping and tearing!")));
        await user.emote("scream");
        // Manually call this (outside of revive/fullheal) so we can pass our blacklist
        await user.regenerate_limbs(revive__dont_regenerate);
    }

    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:121
    public virtual async DmTask ready_to_regenerate(mob user = null)
    {
        if (QDELETED(this) || QDELETED(user))
            return;

        datum_antagonist_changeling ling = IS_CHANGELING(user) as datum_antagonist_changeling;
        if (QDELETED(ling) || !Dm.@in(this, Dm.add(ling.innate_powers, ling.purchased_powers))) // checking both innate and purchased for full coverage
            return;
        if (!HAS_TRAIT_FROM(user, TRAIT_DEATHCOMA, CHANGELING_TRAIT))
            return;

        to_chat(user, span_changeling(I18n.T("We are ready to revive.")));
        await enable_revive(user as mob_living);
    }

    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:134
    [DmParams("user")]
    public override async DmTask<object> can_sting(mob_living user = null, mob_living target = null)
    {
        if (Dm.truthy(revive_ready))
            return await base.can_sting(user, target);

        if (!Dm.truthy(can_enter_stasis(user)))
            return null;
        //Confirmation for living changelings if they want to fake their death
        if (user.stat != DEAD)
        {
            if (!Dm.eq(await tgui_alert(user, I18n.T("Are we sure we wish to fake our own death?"), I18n.T("Feign Death"), Dm.list(I18n.T("Yes"), I18n.T("No"))), I18n.T("Yes")))
                return null;
            if (QDELETED(user) || QDELETED(this) || !Dm.truthy(can_enter_stasis(user)))
                return null;
        }

        return await base.can_sting(user, target);
    }

    /// We wait until after we actually deduct chemical cost (or don't deduct
    /// if it's the 0 cost we get for revival) before we reset the chemical cost
    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:151
    [DmParams("user")]
    public override async DmTask<object> try_to_sting(mob_living user = null, mob_living target = null)
    {
        object dot = null;
        dot = await base.try_to_sting(user, target);
        if (!Dm.truthy(dot) || !Dm.truthy(revive_ready))
            return dot;
        await reset_chemical_cost();
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:157
    public virtual object can_enter_stasis(mob_living user = null)
    {
        if (HAS_TRAIT_FROM(user, TRAIT_DEATHCOMA, CHANGELING_TRAIT))
        {
            user.balloon_alert(user, I18n.T("already reviving!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:163
    public override void update_button_name(atom_movable_screen_movable_action_button button = null, float? force = null, params object[] __extra)
    {
        if (Dm.truthy(revive_ready))
        {
            name = "Revive";
            desc = "We arise once more.";
        }
        else
        {
            name = "Reviving Stasis";
            desc = "We fall into a stasis, allowing us to regenerate and trick our enemies.";
        }
        base.update_button_name(button, force, __extra);
    }

    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:172
    public override void apply_button_icon(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        button_icon_state = Dm.truthy(revive_ready) ? "revive" : "fake_death";
        base.apply_button_icon(current_button, force);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/antagonists/changeling/powers/fakedeath.dm:107  revive's var/static/list/dont_regenerate
    internal DmList datum_action_changeling_fakedeath__revive__dont_regenerate = Dm.list(BODY_ZONE_HEAD);
}
