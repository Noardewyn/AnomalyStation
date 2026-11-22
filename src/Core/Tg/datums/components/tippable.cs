// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/tippable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the log lines (gaps package C3).

/**
 * Tippable component. For making mobs able to be tipped, like cows and medibots.
 */
// tg: code/datums/components/tippable.dm:4
public partial class datum_component_tippable : datum_component
{
    // tg: code/datums/components/tippable.dm:30
    [DmParams("tip_time", "untip_time", "self_right_time", "pre_tipped_callback", "post_tipped_callback", "post_untipped_callback", "roleplay_friendly", "roleplay_emotes", "roleplay_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        this.tip_time = Dm.num(Dm.arg(__extra, 0) ?? 3 * SECONDS);
        this.untip_time = Dm.num(Dm.arg(__extra, 1) ?? 1 * SECONDS);
        this.self_right_time = Dm.num(Dm.arg(__extra, 2) ?? 60 * SECONDS);
        this.pre_tipped_callback = Dm.arg<datum_callback>(__extra, 3);
        this.post_tipped_callback = Dm.arg<datum_callback>(__extra, 4);
        this.post_untipped_callback = Dm.arg<datum_callback>(__extra, 5);
        this.roleplay_friendly = Dm.num(Dm.arg(__extra, 6) ?? FALSE);
        this.roleplay_emotes = Dm.arg<DmList>(__extra, 7);
        this.roleplay_callback = Dm.arg<datum_callback>(__extra, 8);
        return null;
    }

    // tg: code/datums/components/tippable.dm:54
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND_SECONDARY, nameof(interact_with_tippable));
        if (Dm.truthy(roleplay_friendly))
            RegisterSignal(parent, COMSIG_MOB_EMOTE, nameof(accept_roleplay));
    }

    // tg: code/datums/components/tippable.dm:60
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ATOM_ATTACK_HAND_SECONDARY);
    }

    // tg: code/datums/components/tippable.dm:63
    public override object Destroy(float? force = null)
    {
        pre_tipped_callback = null;
        post_tipped_callback = null;
        post_untipped_callback = null;
        roleplay_callback = null;
        return base.Destroy(force);
    }

    /**
     * Attempt to interact with [source], either tipping it or helping it up.
     *
     * source - the mob being tipped over
     * user - the mob interacting with source
     */
    // tg: code/datums/components/tippable.dm:76
    [SignalHandler]
    public virtual object interact_with_tippable(mob_living source = null, mob user = null)
    {
        mob_living living_user = user as mob_living;
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, source)))
            return null;
        if (living_user != null && !Dm.truthy(living_user.combat_mode))
            return null;

        if (Dm.truthy(is_tipped))
            Waiting.Detach(try_untip(source, user));
        else
            Waiting.Detach(try_tip(source, user));

        return COMPONENT_SECONDARY_CANCEL_ATTACK_CHAIN;
    }

    /**
     * Try to tip over [tipped_mob].
     * If the mob is dead, or optional callback returns a value, or our do-after fails, we don't tip the mob.
     * Otherwise, upon completing of the do_after, tip over the mob.
     *
     * tipped_mob - the mob being tipped over
     * tipper - the mob tipping the tipped_mob
     */
    // tg: code/datums/components/tippable.dm:100
    public virtual async DmTask try_tip(mob_living tipped_mob = null, mob tipper = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(tipped_mob) && !HAS_TRAIT(tipped_mob, TRAIT_FORCED_STANDING))
            return;

        if (pre_tipped_callback != null && Dm.truthy(await pre_tipped_callback.Invoke(tipper)))
            return;

        if (Dm.num(tip_time) > 0)
        {
            to_chat(tipper, span_warning(Dm.text("You begin tipping over []...", tipped_mob)));
            tipped_mob.visible_message(
                span_warning(Dm.text("[] begins tipping over [].", tipper, tipped_mob)),
                span_userdanger(Dm.text("[] begins tipping you over!", tipper)),
                ignored_mobs: tipper);

            if (!Dm.truthy(await do_after(tipper, Dm.num(tip_time), target: tipped_mob)))
            {
                to_chat(tipper, span_danger(Dm.text("You fail to tip over [].", tipped_mob)));
                return;
            }
        }
        await do_tip(tipped_mob, tipper);
    }

    /**
     * Actually tip over the mob, setting it to tipped.
     * Also invoking any callbacks we have, with the tipper as the argument,
     * and set a timer to right our self-right our tipped mob if we can.
     *
     * tipped_mob - the mob who was tipped
     * tipper - the mob who tipped the tipped_mob
     */
    // tg: code/datums/components/tippable.dm:130
    public virtual async DmTask do_tip(mob_living tipped_mob = null, mob tipper = null)
    {
        if (QDELETED(tipped_mob))
            throw Dm.CRASH("Tippable component: do_tip() called with QDELETED tipped_mob!", "code/datums/components/tippable.dm", 132);
        if (Dm.truthy(is_tipped)) // sanity check in case multiple people try to tip at the same time
            return;

        to_chat(tipper, span_warning(Dm.text("You tip over [].", tipped_mob)));
        tipped_mob.visible_message(
            span_warning(Dm.text("[] tips over [].", tipper, tipped_mob)),
            span_userdanger(Dm.text("You are tipped over by []!", tipper)),
            ignored_mobs: tipper);

        set_tipped_status(tipped_mob, TRUE);
        if (post_tipped_callback != null)
            await post_tipped_callback.Invoke(tipper);
        if (self_right_time == null)
            return;
        else if (Dm.num(self_right_time) <= 0)
            await right_self(tipped_mob);
        else
            self_untip_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(right_self), tipped_mob), Dm.num(self_right_time), Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), file: "code/datums/components/tippable.dm", line: 151);
    }

    /**
     * Try to untip a mob that has been tipped.
     * After a do-after is completed, we untip the mob.
     *
     * tipped_mob - the mob who is tipped
     * untipper - the mob who is untipping the tipped_mob
     */
    // tg: code/datums/components/tippable.dm:160
    public virtual async DmTask try_untip(mob_living tipped_mob = null, mob untipper = null)
    {
        if (Dm.num(untip_time) > 0)
        {
            to_chat(untipper, span_notice(Dm.text("You begin righting []...", tipped_mob)));
            tipped_mob.visible_message(
                span_notice(Dm.text("[] begins righting [].", untipper, tipped_mob)),
                span_notice(Dm.text("[] begins righting you.", untipper)),
                ignored_mobs: untipper);

            if (!Dm.truthy(await do_after(untipper, Dm.num(untip_time), target: tipped_mob)))
            {
                to_chat(untipper, span_warning(Dm.text("You fail to right [].", tipped_mob)));
                return;
            }
        }

        await do_untip(tipped_mob, untipper);
    }

    /**
     * Actually untip over the mob, setting it to untipped.
     * Also invoke any untip callbacks we have, with the untipper as the argument.
     *
     * tipped_mob - the mob who was tipped
     * tipper - the mob who tipped the tipped_mob
     */
    // tg: code/datums/components/tippable.dm:182
    public virtual async DmTask do_untip(mob_living tipped_mob = null, mob untipper = null)
    {
        if (QDELETED(tipped_mob))
            return;
        if (!Dm.truthy(is_tipped)) // sanity check in case multiple people try to untip at the same time
            return;

        to_chat(untipper, span_notice(Dm.text("You right [].", tipped_mob)));
        tipped_mob.visible_message(
            span_notice(Dm.text("[] rights [].", untipper, tipped_mob)),
            span_notice(Dm.text("You are righted by []!", untipper)),
            ignored_mobs: untipper);

        if (Dm.truthy(self_untip_timer))
            deltimer(self_untip_timer);
        set_tipped_status(tipped_mob, FALSE);
        if (post_untipped_callback != null)
            await post_untipped_callback.Invoke(untipper);
    }

    /**
     * Proc called after a timer to have a tipped mob un-tip itself after a certain length of time.
     * Sets our mob to untipped and invokes the untipped callback without any arguments if we have one.
     *
     * tipped_mob - the mob who was tipped, and is freeing itself
     */
    // tg: code/datums/components/tippable.dm:206
    public virtual async DmTask right_self(mob_living tipped_mob = null)
    {
        if (!Dm.truthy(is_tipped) || QDELETED(tipped_mob))
            return;

        set_tipped_status(tipped_mob, FALSE);
        if (post_untipped_callback != null)
            await post_untipped_callback.Invoke();

        tipped_mob.visible_message(
            span_notice(Dm.text("[] rights itself.", tipped_mob)),
            span_notice("You right yourself."));
    }

    /**
     * Toggles our tipped status between tipped or untipped (TRUE or FALSE)
     * also handles rotating our mob and adding immobilization traits
     *
     * tipped_mob - the mob we're setting to tipped or untipped
     * new_status - the tipped status we're setting the mob to - TRUE for tipped, FALSE for untipped
     */
    // tg: code/datums/components/tippable.dm:225
    public virtual void set_tipped_status(mob_living tipped_mob = null, float? new_status = null)
    {
        new_status ??= FALSE;
        is_tipped = new_status.Value;
        if (Dm.truthy(is_tipped))
        {
            tipped_mob.transform = turn(tipped_mob.transform, 180);
            tipped_mob.add_traits(Dm.list(TRAIT_MOB_TIPPED, TRAIT_IMMOBILIZED), TIPPED_OVER);
            return;
        }

        tipped_mob.transform = turn(tipped_mob.transform, -180);
        tipped_mob.remove_traits(Dm.list(TRAIT_MOB_TIPPED, TRAIT_IMMOBILIZED), TIPPED_OVER);
    }

    /**
     * Accepts "roleplay" in the form of emotes, which removes a quarter of the remaining time left to untip ourself.
     *
     * Arguments:
     * * mob/living/user - The tipped mob
     * * datum/emote/emote - The emote used by the mob
     */
    // tg: code/datums/components/tippable.dm:246
    [SignalHandler]
    public virtual void accept_roleplay(mob_living user = null, datum_emote emote = null)
    {
        if (!Dm.truthy(is_tipped))
            return;
        if (Dm.truthy(roleplayed))
            return;
        if (!Dm.truthy(is_type_in_list(emote, roleplay_emotes)))
            return;
        float time_left = Dm.num(timeleft(self_untip_timer));
        deltimer(self_untip_timer);
        self_untip_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(right_self), user), time_left * 0.75f, Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), file: "code/datums/components/tippable.dm", line: 256);
        roleplayed = TRUE;
        if (roleplay_callback != null)
            Waiting.Detach(roleplay_callback.Invoke(user));
    }
}
