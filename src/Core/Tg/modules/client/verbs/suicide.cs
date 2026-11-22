// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/verbs/suicide.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI1, D326: the silicons' suicide messages override get_visible_suicide_message and
// get_blind_suicide_message).

// tg: code/modules/client/verbs/suicide.dm:4
public partial class mob_living : mob
{
    /// Verb to simply kill yourself (in a very visual way to all players) in game! How family-friendly. Can be governed by a series of multiple checks (i.e. confirmation, is it allowed in this area, etc.) which are
    /// handled and called by the proc this verb invokes. It's okay to block this, because we typically always give mobs in-game the ability to Ghost out of their current mob irregardless of context. This, in contrast,
    /// can have as many different checks as you desire to prevent people from doing the deed to themselves.
    // tg: code/modules/client/verbs/suicide.dm:4  GAME_VERB_HIDDEN(/mob/living, suicide, "suicide"): the body
    public virtual async DmTask __gvb_suicide(DmList structured_args = null)
    {
        await handle_suicide();
    }

    // tg: code/modules/client/verbs/suicide.dm:4
    // GAME_VERB_HIDDEN(/mob/living, suicide, "suicide") expands (__DEFINES/verb.dm:12-31) to the type
    // /datum/verb_metadata/mob/living/suicide (the verb registry), the verb below, which hands its arguments to
    // SSverbs.invoke_verb, and the proc __gvb_suicide with the body; as succumb (living.cs), a caller without a client does nothing.
    public virtual void suicide(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_suicide(Dm.list()));
    }

    /// Actually handles the bare basics of the suicide process. Message type is the message we want to dispatch in the world regarding the suicide, using the defines in this file.
    /// The order of operations here is important, if you want to make specific behaviour for a kind of mob killing itself you likely want to override perform_basic_suicide()
    // tg: code/modules/client/verbs/suicide.dm:9
    public async DmTask handle_suicide()
    {
        if (!Dm.truthy(await suicide_alert()))
            return;

        set_suicide(TRUE);

        obj_item held_item = get_active_held_item() as obj_item;
        object damage_type = !Dm.truthy(_listen_lookup?[COMSIG_LIVING_SUICIDE_ACT]) ? NONE : _SendSignal(COMSIG_LIVING_SUICIDE_ACT, Dm.list(this));
        if (!Dm.truthy(damage_type))
            damage_type = held_item != null ? await held_item.suicide_act(this) : null;

        if (Dm.truthy(damage_type))
        {
            if (Dm.truthy(await apply_suicide_damage(held_item, damage_type)))
                await final_checkout(held_item, apply_damage: FALSE);
            return;
        }

        await perform_basic_suicide();
    }

    /// Kill yourself without any items
    // tg: code/modules/client/verbs/suicide.dm:27
    public virtual async DmTask perform_basic_suicide()
    {
        send_applicable_messages();
        await final_checkout();
    }

    /// Proc that handles adding the TRAIT_SUICIDED on the mob in question, as well as additional operations to ensure that everything goes smoothly when we're certain that this person is going to kill themself.
    /// suicide_state is a boolean, and we handle adding/removing the trait in question. Have the trait function reference this mob as the source if we want to do in-depth tracking of where a suicided trait comes from.
    /// For example, the /mob/dead/observer that will inevitably come from the suicidee will inherit the suicided trait upon creation, and keep this reference. Handy for doing checking should we need it.
    // tg: code/modules/client/verbs/suicide.dm:34
    public virtual void set_suicide(object suicide_state = null)
    {
        if (Dm.truthy(suicide_state))
        {
            ADD_TRAIT(this, TRAIT_SUICIDED, REF(this));
            add_to_mob_suicide_list();
        }
        else
        {
            REMOVE_TRAIT(this, TRAIT_SUICIDED, REF(this));
            remove_from_mob_suicide_list();
        }
    }

    /// Sends a TGUI Alert to the person attempting to commit suicide. Returns TRUE if they confirm they want to die, FALSE otherwise. Check can_suicide here as well.
    // tg: code/modules/client/verbs/suicide.dm:43
    public virtual async DmTask<object> suicide_alert()
    {
        // Save this for later to ensure that if we change ckeys somehow, we exit out of the suicide.
        object oldkey = ckey;
        if (!Dm.truthy(can_suicide()))
            return FALSE;

        object confirm = await tgui_alert(this, "Are you sure you want to commit suicide?", "Confirm Suicide", Dm.list("Yes", "No"));

        // ensure our situation didn't change while we were sleeping waiting for the tgui_alert.
        if (!Dm.truthy(can_suicide()) || !Dm.eq(ckey, oldkey))
            return FALSE;

        if (Dm.eq(confirm, "Yes"))
            return TRUE;

        balloon_alert(this, I18n.T("suicide attempt aborted!"));
        return FALSE;
    }

    /// Checks if we are in a valid state to suicide (not already suiciding, capable of actually killing ourselves, area checks, etc.) Returns TRUE if we can suicide, FALSE if we can not.
    // tg: code/modules/client/verbs/suicide.dm:62
    public virtual object can_suicide()
    {
        if (HAS_TRAIT_FROM_ONLY(this, TRAIT_SUICIDED, REF(this)))
        {
            to_chat(this, span_warning(I18n.T("You are already commiting suicide!")));
            return FALSE;
        }

        area checkable = get_area(this) as area;
        if (Dm.truthy(Dm.bitand(checkable.area_flags, BLOCK_SUICIDE)))
        {
            to_chat(this, span_warning(I18n.T("You can't commit suicide here! You can ghost if you'd like.")));
            return FALSE;
        }

        if (!IS_UNCONSCIOUS_OR_CRIT(this))
            return TRUE;

        if (stat == DEAD)
        {
            to_chat(this, span_warning(I18n.T("You're already dead!")));
            return FALSE;
        }

        if (IS_UNCONSCIOUS(this))
        {
            to_chat(this, span_warning(I18n.T("You need to be conscious to commit suicide!")));
            return FALSE;
        }

        to_chat(this, span_warning(I18n.T("You can't commit suicide while in a critical condition!")));
        return FALSE;
    }

    /// Inserts in logging and death + mind dissociation when we're fully done with ending the life of our mob, as well as adjust the health. We will disallow re-entering the body when this is called.
    /// The suicide_tool variable is used in order to allow suicide log to properly put stuff in investigate log.
    /// Set apply_damage to FALSE in order to not do damage (in case it's handled elsewhere in the verb or another proc that the suicide tree calls). Will dissociate client from mind and ghost the player regardless.
    // tg: code/modules/client/verbs/suicide.dm:89
    public virtual async DmTask final_checkout(obj_item suicide_tool = null, float? apply_damage = null)
    {
        apply_damage ??= TRUE;
        if (Dm.truthy(apply_damage)) // enough to really drive home the point that they are DEAD.
            await apply_suicide_damage();

        suicide_log(suicide_tool);
        await death(FALSE);
        ghostize(FALSE);
    }

    /// Send all suicide-related messages out to the world. message_type can be used to change out the dispatched suicide message depending on the suicide context.
    // tg: code/modules/client/verbs/suicide.dm:98
    public virtual void send_applicable_messages(object message_type = null)
    {
        visible_message(span_danger(Dm.str(get_visible_suicide_message())), span_userdanger(Dm.str(get_visible_suicide_message())), span_hear(Dm.str(get_blind_suicide_message())));
    }

    /// Returns a subtype-specific flavorful string pertaining to this exact living mob's ending their own life to those who can see it (visible message).
    /// If you don't want a message, prefer to override send_applicable_messages() on your subtype instead.
    // tg: code/modules/client/verbs/suicide.dm:103
    public virtual object get_visible_suicide_message()
    {
        return I18n.T("[] begins to fall down. It looks like [] lost the will to live.", this, p_theyve());
    }

    /// Returns an appropriate string for what people who lack visibility hear when this mob kills itself.
    /// If you don't want a message, prefer to override send_applicable_messages() on your subtype instead.
    // tg: code/modules/client/verbs/suicide.dm:108
    public virtual object get_blind_suicide_message()
    {
        return I18n.T("You hear something hitting the floor.");
    }

    /// Inserts logging in both the mob's logs and the investigate log pertaining to their death. Suicide tool is the object we used to commit suicide, if one was held and used (presently only humans use this arg).
    // tg: code/modules/client/verbs/suicide.dm:112
    public virtual void suicide_log(obj_item suicide_tool = null)
    {
        investigate_log("has died from committing suicide.", INVESTIGATE_DEATHS);
        log_message(Dm.text("committed suicide as []", this.type), LOG_ATTACK);
    }

    /// The actual proc that will apply the damage to the suiciding mob. damage_type is the actual type of damage we want to deal, if that matters.
    /// Return TRUE if we actually apply any real damage, FALSE otherwise.
    // tg: code/modules/client/verbs/suicide.dm:118
    public virtual async DmTask<object> apply_suicide_damage(obj_item suicide_tool = null, object damage_type = null)
    {
        damage_type ??= NONE;
        if (Dm.eq(damage_type, NONE))
        {
            await adjust_oxy_loss(max(Dm.num(maxHealth) * 2 - Dm.num(get_tox_loss()) - Dm.num(get_fire_loss()) - Dm.num(get_brute_loss()) - Dm.num(get_oxy_loss()), 0));
            return TRUE;
        }

        if (Dm.truthy(Dm.bitand(damage_type, SHAME)))
        {
            await adjust_stamina_loss(max_stamina);
            if (!Dm.truthy(has_status_effect(typeof(datum_status_effect_incapacitating_stamcrit))))
                await Paralyze(10 * SECONDS, ignore_canstun: TRUE); // No will to go on, for 10 seconds
            set_suicide(FALSE);
            await add_mood_event("shameful_suicide", typeof(datum_mood_event_shameful_suicide));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(damage_type, MANUAL_SUICIDE_NONLETHAL)))
        {
            set_suicide(FALSE);
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(damage_type, MANUAL_SUICIDE))) // Assume that the suicide tool will handle the death.
        {
            suicide_log(suicide_tool);
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(damage_type, Dm.bitor(Dm.bitor(BRUTELOSS, FIRELOSS), Dm.bitor(OXYLOSS, TOXLOSS)))))
        {
            await handle_suicide_damage_spread(damage_type);
            return TRUE;
        }

        return FALSE;
    }

    /// If we want to apply multiple types of damage to a carbon mob based on the way they suicide, this is the proc that handles that.
    /// Currently only compatible with Brute, Burn, Toxin, and Suffocation Damage. damage_type is the bitflag that carries the information.
    // tg: code/modules/client/verbs/suicide.dm:146
    public virtual async DmTask handle_suicide_damage_spread(object damage_type = null)
    {
        // We split up double the total health the mob has, then spread it out.
        float damage_to_apply = (Dm.num(maxHealth) * 2); // For humans, this value comes out to 200.
        // The multiplier that we divide damage_to_apply by.
        float damage_mod = 0;
        // We don't want to damage_type again and again, this will hold the results.
        DmList filtered_damage_types = Dm.list();

        foreach (object type in Dm.each(Dm.list(BRUTELOSS, FIRELOSS, OXYLOSS, TOXLOSS)))
        {
            if (!Dm.truthy(Dm.bitand(type, damage_type)))
                continue;
            damage_mod++;
            Dm.append(filtered_damage_types, type);
        }

        damage_mod = max(1, damage_mod); // division by zero is silly
        damage_to_apply = (damage_to_apply / damage_mod);

        foreach (object filtered_type in Dm.each(filtered_damage_types))
        {
            object sw__1 = filtered_type;
            if (Dm.eq(sw__1, BRUTELOSS))
                await adjust_brute_loss(damage_to_apply);
            else if (Dm.eq(sw__1, FIRELOSS))
                await adjust_fire_loss(damage_to_apply);
            else if (Dm.eq(sw__1, OXYLOSS))
                await adjust_oxy_loss(damage_to_apply);
            else if (Dm.eq(sw__1, TOXLOSS))
                await adjust_tox_loss(damage_to_apply);
        }
    }
}
