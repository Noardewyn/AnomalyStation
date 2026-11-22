// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_verbs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file but the talk logs and the reset's admin notice (the admin logs).

// tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:2
public partial class mob_living_basic_guardian : mob_living_basic
{
    /// Pop out into the realm of the living.
    // tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:2
    public virtual async DmTask<object> manifest(object forced = null)
    {
        if (Dm.truthy(is_deployed()) || summoner == null || summoner.loc == null || istype(summoner.loc, typeof(obj_effect)) || (!(manifest_cooldown <= world.time) && !Dm.truthy(forced)) || Dm.truthy(locked)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline
            return FALSE;
        await forceMove(summoner.loc);
        Dm.@new<obj_effect_temp_visual_guardian_phase>(loc);
        manifest_cooldown = world.time + 1 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
        await reset_perspective();
        await manifest_effects();
        return TRUE;
    }

    /// Go and hide inside your boss.
    // tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:13
    public virtual async DmTask<object> recall(object forced = null)
    {
        if (!Dm.truthy(is_deployed()) || summoner == null || (!(manifest_cooldown <= world.time) && !Dm.truthy(forced)) || Dm.truthy(locked)) // COOLDOWN_FINISHED, inline
            return FALSE;
        Dm.@new<obj_effect_temp_visual_guardian_phase_out>(loc);
        await recall_effects();
        await forceMove(summoner);
        manifest_cooldown = world.time + 1 * SECONDS; // COOLDOWN_START, inline
        return TRUE;
    }

    /// Do something when we appear.
    // tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:23
    [ShouldCallParent]
    public virtual async DmTask manifest_effects()
    {
        // SEND_SIGNAL(src, COMSIG_GUARDIAN_MANIFESTED) (rules 11.1 (e), a statement)
        if (Dm.truthy(_listen_lookup?[COMSIG_GUARDIAN_MANIFESTED]))
            _SendSignal(COMSIG_GUARDIAN_MANIFESTED, Dm.list(this));
        await DmTask.CompletedTask;
    }

    /// Do something when we vanish.
    // tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:28
    [ShouldCallParent]
    public virtual async DmTask recall_effects()
    {
        // SEND_SIGNAL(src, COMSIG_GUARDIAN_RECALLED) (rules 11.1 (e), a statement)
        if (Dm.truthy(_listen_lookup?[COMSIG_GUARDIAN_RECALLED]))
            _SendSignal(COMSIG_GUARDIAN_RECALLED, Dm.list(this));
        await DmTask.CompletedTask;
    }

    /// Swap to a different mode... if we have one
    // tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:33
    public virtual async DmTask toggle_modes()
    {
        to_chat(this, span_bolddanger("You don't have another mode!"));
        await DmTask.CompletedTask;
    }


    /// Turn an internal light on or off.
    // tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:38
    public virtual void toggle_light()
    {
        if (!Dm.truthy(light_on))
        {
            to_chat(this, span_notice("You activate your light."));
            Waiting.NoSleep(set_light_on(TRUE));
        }
        else
        {
            to_chat(this, span_notice("You deactivate your light."));
            Waiting.NoSleep(set_light_on(FALSE));
        }
    }

    /// Speak with our boss at a distance
    // tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:47
    public virtual async DmTask communicate()
    {
        if (summoner == null)
            return;
        object sender_key = key;
        object input = await tgui_input_text(this, "Enter a message to tell your summoner", "Guardian", max_length: MAX_MESSAGE_LEN);
        if (!Dm.eq(sender_key, key) || !Dm.truthy(input)) //guardian got reset, or did not enter anything
            return;

        string preliminary_message = span_boldholoparasite(Dm.text("[]", input)); //apply basic color/bolding
        string my_message = Dm.text("<font color=\"[]\">[]:</font> []", guardian_colour, span_bolditalic(Dm.str(name)), preliminary_message); //add source, color source with the guardian's color

        to_chat(summoner, Dm.text("[]", my_message));
        DmList guardians = summoner.get_all_linked_holoparasites();
        foreach (object guardian in Dm.each(guardians))
            to_chat(guardian, Dm.text("[]", my_message));
        foreach (object dead_mob in Dm.each(GLOB.dead_mob_list))
        {
            string link = FOLLOW_LINK(dead_mob, this);
            to_chat(dead_mob, Dm.text("[] []", link, my_message));
        }

        log_talk(input, LOG_SAY, tag: "guardian");
    }
}


/// Speak with your guardian(s) at a distance.
// tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:70
public partial class datum_action_cooldown_mob_cooldown_guardian_comms : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:83
    public override async DmTask<object> Activate(atom target = null)
    {
        StartCooldown(360 * SECONDS);
        object input = await tgui_input_text(owner, "Enter a message to tell your guardian", "Message", max_length: MAX_MESSAGE_LEN);
        StartCooldown();
        if (!Dm.truthy(input))
            return FALSE;

        string preliminary_message = span_boldholoparasite(Dm.text("[]", input)); //apply basic color/bolding
        string my_message = span_boldholoparasite(Dm.text("<i>[]:</i> []", owner, preliminary_message)); //add source, color source with default grey...

        to_chat(owner, Dm.text("[]", my_message));
        mob_living living_owner = owner as mob_living;
        DmList guardians = living_owner.get_all_linked_holoparasites();
        foreach (mob_living_basic_guardian guardian in Dm.each_cast<mob_living_basic_guardian>(guardians))
            to_chat(guardian, Dm.text("<font color=\"[]\">[]:</font> []", guardian.guardian_colour, span_bolditalic(Dm.str(owner.real_name)), preliminary_message));
        foreach (object dead_mob in Dm.each(GLOB.dead_mob_list))
        {
            string link = FOLLOW_LINK(dead_mob, owner);
            to_chat(dead_mob, Dm.text("[] []", link, my_message));
        }
        owner.log_talk(input, LOG_SAY, tag: "guardian");

        return TRUE;
    }
}


/// Tell your slacking or distracted guardian to come home.
// tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:107
public partial class datum_action_cooldown_mob_cooldown_recall_guardian : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:120
    public override async DmTask<object> Activate(atom target = null)
    {
        mob_living living_owner = owner as mob_living;
        DmList guardians = living_owner.get_all_linked_holoparasites();
        foreach (mob_living_basic_guardian guardian in Dm.each<mob_living_basic_guardian>(guardians))
            await guardian.recall();
        StartCooldown();
        return TRUE;
    }
}

/// Replace an annoying griefer you were paired up to with a different but probably no less annoying player.
// tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:129
public partial class datum_action_cooldown_mob_cooldown_replace_guardian : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/guardian/guardian_verbs.dm:142
    public override async DmTask<object> Activate(atom target = null)
    {
        StartCooldown(5 * MINUTES);

        mob_living living_owner = owner as mob_living;
        DmList guardians = living_owner.get_all_linked_holoparasites();
        foreach (mob_living_basic_guardian resetting_guardian in Dm.each_cast<mob_living_basic_guardian>(guardians.Copy()))
        {
            if (!(resetting_guardian.resetting_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
                Dm.remove(guardians, resetting_guardian); //clear out guardians that are already reset
        }

        if (!Dm.truthy(length(guardians)))
        {
            to_chat(owner, span_holoparasite(Dm.text("You cannot reset [] yet.", length(guardians) > 1 ? "any of your guardians" : "your guardian")));
            StartCooldown();
            return FALSE;
        }

        mob_living_basic_guardian chosen_guardian = await tgui_input_list(owner, "Pick the guardian you wish to reset", "Guardian Reset", sort_names(guardians) as DmList) as mob_living_basic_guardian;
        if (chosen_guardian == null)
        {
            to_chat(owner, span_holoparasite(Dm.text("You decide not to reset [].", length(guardians) > 1 ? "any of your guardians" : "your guardian")));
            StartCooldown();
            return FALSE;
        }

        to_chat(owner, span_holoparasite(Dm.text("You attempt to reset <font color=\"[]\">[]</font>'s personality...", chosen_guardian.guardian_colour, span_bold(Dm.str(chosen_guardian.real_name)))));
        mob chosen_one = await SSpolling.poll_ghost_candidates(Dm.text("Do you want to play as [] []?", span_danger(Dm.text("[]'s", owner.real_name)), span_notice(chosen_guardian.theme.name)), check_jobban: ROLE_PAI, poll_time: 10 * SECONDS, alert_pic: chosen_guardian, jump_target: owner, role_name_text: chosen_guardian.theme.name, amount_to_pick: 1) as mob;
        if (chosen_one == null)
        {
            to_chat(owner, span_holoparasite(Dm.text("Your attempt to reset the personality of <font color=\"[]\">[]</font> appears to have failed... Looks like you're stuck with it for now.", chosen_guardian.guardian_colour, span_bold(Dm.str(chosen_guardian.real_name)))));
            StartCooldown();
            return FALSE;
        }
        to_chat(chosen_guardian, span_holoparasite("Your user reset you, and your body was taken over by a ghost. Looks like they weren't happy with your performance."));
        to_chat(owner, span_boldholoparasite(Dm.text("The personality of <font color=\"[]\">[]</font> has been successfully reset.", chosen_guardian.guardian_colour, chosen_guardian.theme.name)));
        message_admins(Dm.text("[] has taken control of ([])", key_name_admin(chosen_one), ADMIN_LOOKUPFLW(chosen_guardian)));
        chosen_guardian.ghostize(FALSE);
        chosen_guardian.PossessByPlayer(chosen_one.key);
        chosen_guardian.resetting_cooldown = world.time + 5 * MINUTES; // COOLDOWN_START, inline
        await chosen_guardian.guardian_rename(); //give it a new color and name, to show it's a new person
        await chosen_guardian.guardian_recolour();
        StartCooldown();
        return TRUE;
    }
}
