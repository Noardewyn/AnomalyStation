// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/polling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file but the stat line (stat panels, rules 13), the taskbar flash (window_flash, the
// client window) and the chat border's icon (icon2html, the chat icons gap). The var block is generated.

// tg: code/controllers/subsystem/polling.dm:1 SUBSYSTEM_DEF(polling)
public partial class datum_controller_subsystem_polling : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(polling) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSpolling) (__DEFINES/MC.dm:29) expanded
    // inline (it reads src).
    // tg: code/controllers/subsystem/polling.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSpolling != this)
        {
            if (SSpolling is datum_controller_subsystem_polling)
            {
                Recover();
                qdel(SSpolling);
            }
            SSpolling = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/polling.dm:11
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        if (!Dm.truthy(currently_polling)) // if polls_active is TRUE then this shouldn't happen, but still..
            currently_polling = Dm.list();

        foreach (datum_candidate_poll running_poll in Dm.each_cast<datum_candidate_poll>(currently_polling.Copy()))
        {
            if (Dm.num(running_poll.time_left()) <= 0)
                polling_finished(running_poll);
        }
        await DmTask.CompletedTask;
    }

    /**
     * Starts a poll.
     *
     * Arguments
     * * question: Optional, The question to ask the candidates. If null, a default question will be used. ("Do you want to play as role?")
     * * role: Optional, An antag role (IE, ROLE_TRAITOR) to pass, it won't show to any candidates who don't have it in their preferences.
     * * check_jobban: Optional, What jobban role / flag to check, it won't show to any candidates who have this jobban.
     * * poll_time: How long the poll will last.
     * * ignore_category: Optional, A poll category. If a candidate has this category in their ignore list, they won't be polled.
     * * flash_window: If TRUE, the candidate's window will flash when they're polled.
     * * list/group: A list of candidates to poll.
     * * alert_pic: Optional, An /atom or an /image to display on the poll alert.
     * * jump_target: An /atom to teleport/jump to, if alert_pic is an /atom defaults to that.
     * * role_name_text: Optional, A string to display in logging / the (default) question. If null, the role name will be used.
     * * list/custom_response_messages: Optional, A list of strings to use as responses to the poll. If null, the default responses will be used. see __DEFINES/polls.dm for valid keys to use.
     * * start_signed_up: If TRUE, all candidates will start signed up for the poll, making it opt-out rather than opt-in.
     * * amount_to_pick: Lets you pick candidates and return a single mob or list of mobs that were chosen.
     * * chat_text_border_icon: Object or path to make an icon of to decorate the chat announcement.
     * * announce_chosen: Whether we should announce the chosen candidates in chat. This is ignored unless amount_to_pick is greater than 0.
     *
     * Returns a list of all mobs who signed up for the poll, OR, in the case that amount_to_pick is equal to 1 the singular mob/null if no available candidates.
     */
    // tg: code/controllers/subsystem/polling.dm:41
    public virtual async DmTask<object> poll_candidates(object question = null, object role = null, object check_jobban = null, float? poll_time = null, object ignore_category = null, float? flash_window = null, DmList group = null, object alert_pic = null, object jump_target = null, object role_name_text = null, DmList custom_response_messages = null, float? start_signed_up = null, float? amount_to_pick = null, object chat_text_border_icon = null, float? announce_chosen = null)
    {
        poll_time ??= 30 * SECONDS;
        flash_window ??= TRUE;
        start_signed_up ??= FALSE;
        amount_to_pick ??= 0;
        announce_chosen ??= TRUE;

        if (group.len == 0)
            return null;
        if (Dm.truthy(role) && !Dm.truthy(role_name_text))
            role_name_text = role;
        if (Dm.truthy(role_name_text) && !Dm.truthy(question))
            question = Dm.text("Do you want to play as []?", span_notice(Dm.str(role_name_text)));
        if (!Dm.truthy(question))
            question = "Do you want to play as a special role?";
        log_ghost_poll("Candidate poll started.", data: Dm.list(
            Dm.kv("role name", role_name_text),
            Dm.kv("poll question", question),
            Dm.kv("poll duration", DisplayTimeText(poll_time))
        ));

        // Start firing
        total_polls++;

        if (jump_target == null && isatom(alert_pic))
            jump_target = alert_pic;

        datum_candidate_poll new_poll = Dm.@new<datum_candidate_poll>(role_name_text, question, poll_time, ignore_category, jump_target, custom_response_messages);
        LAZYADD(ref currently_polling, new_poll);

        string category = Dm.text("[]_poll_alert", new_poll.poll_key);

        foreach (mob candidate_mob in Dm.each_cast<mob>(group))
        {
            if (candidate_mob.client == null)
                continue;
            // Universal opt-out for all players.
            if (!Dm.truthy(candidate_mob.client.prefs.read_preference(typeof(datum_preference_toggle_ghost_roles))))
                continue;
            // Opt-out for admins whom are currently adminned.
            if (!Dm.truthy(candidate_mob.client.prefs.read_preference(typeof(datum_preference_toggle_ghost_roles_as_admin))) && candidate_mob.client.holder != null)
                continue;
            if (!Dm.truthy(await is_eligible(candidate_mob, role, check_jobban, ignore_category)))
                continue;

            if (Dm.truthy(start_signed_up))
                new_poll.sign_up(candidate_mob, TRUE);
            if (Dm.truthy(flash_window))
            {
                // window_flash(candidate_mob.client) (__HELPERS/game.dm:197): the client's taskbar flash is a winset (the client window)
                Dm.Deferred("code/controllers/subsystem/polling.dm:98", "poll_candidates: window_flash(client), the game window's taskbar flash (the client window)");
            }

            // If we somehow send two polls for the same mob type, but with a duration on the second one shorter than the time left on the first one,
            // we need to keep the first one's timeout rather than use the shorter one
            atom_movable_screen_alert_poll_alert current_alert = LAZYACCESS(candidate_mob.alerts, category) as atom_movable_screen_alert_poll_alert;
            float alert_time = poll_time.Value;
            datum_candidate_poll alert_poll = new_poll;
            if (current_alert != null && Dm.num(current_alert.timeout) > (world.time + poll_time.Value - world.tick_lag))
            {
                alert_time = Dm.num(current_alert.timeout) - world.time + world.tick_lag;
                alert_poll = current_alert.poll;
            }

            // Send them an on-screen alert
            atom_movable_screen_alert_poll_alert poll_alert_button = candidate_mob.throw_alert(category, typeof(atom_movable_screen_alert_poll_alert), timeout_override: alert_time, no_anim: TRUE) as atom_movable_screen_alert_poll_alert;
            if (poll_alert_button == null)
                continue;

            Dm.append(new_poll.alert_buttons, poll_alert_button);
            new_poll.RegisterSignal(poll_alert_button, COMSIG_QDELETING, nameof(datum_candidate_poll.clear_alert_ref));

            poll_alert_button.icon = ui_style2icon(candidate_mob.client?.prefs?.read_preference(typeof(datum_preference_choiced_ui_style)));
            poll_alert_button.desc = Dm.text("[]", question);
            poll_alert_button.show_time_left = TRUE;
            poll_alert_button.poll = alert_poll;
            poll_alert_button.set_role_overlay();
            poll_alert_button.update_stacks_overlay();
            poll_alert_button.update_candidates_number_overlay();
            poll_alert_button.update_signed_up_overlay();


            // Sign up inheritance and stacking
            foreach (datum_candidate_poll other_poll in Dm.each_cast<datum_candidate_poll>(currently_polling))
            {
                if (new_poll == other_poll || !Dm.eq(new_poll.poll_key, other_poll.poll_key))
                    continue;
                // If there's already a poll for an identical mob type ongoing and the client is signed up for it, sign them up for this one
                if (Dm.@in(candidate_mob, other_poll.signed_up) && Dm.truthy(new_poll.sign_up(candidate_mob, TRUE)))
                    break;
            }

            // Image to display
            image poll_image;
            if (ispath(alert_pic, typeof(atom)) || isatom(alert_pic))
            {
                poll_image = Dm.@new<mutable_appearance>(alert_pic);
                poll_image.pixel_z = 0;
            }
            else if (alert_pic != null)
                poll_image = alert_pic as image;
            else
                poll_image = image(Dm.resource("icons/effects/effects.dmi"), icon_state: "static");

            if (poll_image != null)
            {
                poll_image.layer = FLOAT_LAYER;
                poll_image.plane = poll_alert_button.plane;
                poll_alert_button.add_overlay(poll_image);
            }

            // Chat message
            string act_jump = "";
            string custom_link_style_start = "<style>a:visited{color:Crimson !important}</style>";
            string custom_link_style_end = "style='color:DodgerBlue;font-weight:bold;-dm-text-outline: 1px black'";
            if (isatom(alert_pic) && isobserver(candidate_mob))
                act_jump = Dm.text("[]<a href='byond://?src=[];jump=1'[]>[Teleport]</a>", custom_link_style_start, REF(poll_alert_button), custom_link_style_end);
            string act_signup = Dm.text("[]<a href='byond://?src=[];signup=1'[]>[[]]</a>", custom_link_style_start, REF(poll_alert_button), custom_link_style_end, Dm.truthy(start_signed_up) ? "Opt out" : "Sign Up");
            string act_never = "";
            if (Dm.truthy(ignore_category))
                act_never = Dm.text("[]<a href='byond://?src=[];never=1'[]>[Never For This Round]</a>", custom_link_style_start, REF(poll_alert_button), custom_link_style_end);

            if (!Dm.truthy(duplicate_message_check(alert_poll))) //Only notify people once. They'll notice if there are multiple and we don't want to spam people.
            {
                // ghost poll prompt sound handling
                object polling_sound_pref = candidate_mob.client?.prefs.read_preference(typeof(datum_preference_choiced_sound_ghost_poll_prompt));
                object polling_sound_volume = candidate_mob.client?.prefs.read_preference(typeof(datum_preference_numeric_sound_ghost_poll_prompt_volume));
                if (!Dm.eq(polling_sound_pref, GHOST_POLL_PROMPT_DISABLED) && Dm.truthy(polling_sound_volume))
                {
                    object polling_sound;
                    if (Dm.eq(polling_sound_pref, GHOST_POLL_PROMPT_1))
                        polling_sound = Dm.resource("sound/misc/prompt1.ogg");
                    else
                        polling_sound = Dm.resource("sound/misc/prompt2.ogg");
                    Dm.output_to(candidate_mob, Dm.sound(polling_sound, volume: Dm.num(polling_sound_volume))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
                }

                string surrounding_icon = null;
                if (Dm.truthy(chat_text_border_icon))
                {
                    // the border's picture: icon2html(image, candidate_mob, extra_classes = "bigicon"), the chat icons gap (not-translated 76)
                    Dm.Deferred("code/controllers/subsystem/polling.dm:182", "poll_candidates: the chat border's icon2html picture (the chat icons)");
                }
                string final_message = boxed_message(Dm.text("<span style='text-align:center;display:block'>[] <span style='font-size:1.2em'>[]</span> []\n[]      []      []</span>", surrounding_icon, span_ooc(Dm.str(question)), surrounding_icon, act_jump, act_signup, act_never));
                to_chat(candidate_mob, final_message);
            }

            // Start processing it so it updates visually the timer
            START_PROCESSING(SSprocessing, poll_alert_button);
        }

        // Sleep until the time is up
        while (!Dm.truthy(new_poll.finished)) // UNTIL(new_poll.finished) (__DEFINES/_helpers.dm:31), expanded inline
            await stoplag();
        if (!Dm.truthy(amount_to_pick))
            return new_poll.signed_up;
        if (!Dm.truthy(length(new_poll.signed_up)))
            return null;
        for (float pick = 1; pick <= amount_to_pick; pick++)
        {
            // There may be less people signed up than amount_to_pick
            // pick_n_take returns the default return value of null if passed an empty list, so just break in that case rather than adding null to the list.
            if (!Dm.truthy(length(new_poll.signed_up)))
                break;
            Dm.append(new_poll.chosen_candidates, pick_n_take(new_poll.signed_up));
        }
        if (Dm.truthy(announce_chosen))
            new_poll.announce_chosen(group);
        if (new_poll.chosen_candidates.len == 1)
        {
            object chosen_one = pick(new_poll.chosen_candidates);
            return chosen_one;
        }
        return new_poll.chosen_candidates;
    }

    // tg: code/controllers/subsystem/polling.dm:208
    public virtual async DmTask<object> poll_ghost_candidates(object question = null, object role = null, object check_jobban = null, float? poll_time = null, object ignore_category = null, float? flashwindow = null, object alert_pic = null, object jump_target = null, object role_name_text = null, DmList custom_response_messages = null, float? start_signed_up = null, float? amount_to_pick = null, object chat_text_border_icon = null, float? announce_chosen = null)
    {
        poll_time ??= 30 * SECONDS;
        flashwindow ??= TRUE;
        start_signed_up ??= FALSE;
        amount_to_pick ??= 0;
        announce_chosen ??= TRUE;

        DmList candidates = Dm.list();
        if (!Dm.truthy(Dm.bitand(GLOB.ghost_role_flags, GHOSTROLE_STATION_SENTIENCE)))
            return null;
        foreach (mob_dead_observer ghost_player in Dm.each<mob_dead_observer>(GLOB.player_list))
            Dm.append(candidates, ghost_player);

        // tg: #ifdef TESTING ... #endif not translated (inactive in production)

        return await poll_candidates(question, role, check_jobban, poll_time, ignore_category, flashwindow, candidates, alert_pic, jump_target, role_name_text, custom_response_messages, start_signed_up, amount_to_pick, chat_text_border_icon, announce_chosen);
    }

    // tg: code/controllers/subsystem/polling.dm:237
    public virtual async DmTask<object> poll_ghosts_for_target(object question = null, object role = null, object check_jobban = null, float? poll_time = null, atom_movable checked_target = null, object ignore_category = null, float? flashwindow = null, object alert_pic = null, object jump_target = null, object role_name_text = null, DmList custom_response_messages = null, float? start_signed_up = null, object chat_text_border_icon = null, float? announce_chosen = null)
    {
        poll_time ??= 30 * SECONDS;
        flashwindow ??= TRUE;
        start_signed_up ??= FALSE;
        announce_chosen ??= TRUE;

        DmList currently_polling_targets = TgState.Current.datum_controller_subsystem_polling__currently_polling_targets ??= Dm.list();
        if (Dm.truthy(currently_polling_targets.Find(checked_target)))
            return null;
        Dm.append(currently_polling_targets, checked_target);
        mob chosen_one = await poll_ghost_candidates(question, role, check_jobban, poll_time, ignore_category, flashwindow, alert_pic, jump_target, role_name_text, custom_response_messages, start_signed_up, amount_to_pick: 1, chat_text_border_icon: chat_text_border_icon, announce_chosen: announce_chosen) as mob;
        Dm.remove(currently_polling_targets, checked_target);
        if (checked_target == null || QDELETED(checked_target) || checked_target.loc == null)
            return null;
        return chosen_one;
    }

    // tg: code/controllers/subsystem/polling.dm:263
    public virtual async DmTask<object> poll_ghosts_for_targets(object question = null, object role = null, object check_jobban = null, float? poll_time = null, DmList checked_targets = null, object ignore_category = null, float? flashwindow = null, object alert_pic = null, object jump_target = null, object role_name_text = null, DmList custom_response_messages = null, float? start_signed_up = null, object chat_text_border_icon = null)
    {
        poll_time ??= 30 * SECONDS;
        flashwindow ??= TRUE;
        start_signed_up ??= FALSE;

        DmList candidate_list = await poll_ghost_candidates(question, role, check_jobban, poll_time, ignore_category, flashwindow, alert_pic, jump_target, role_name_text, custom_response_messages, start_signed_up, chat_text_border_icon: chat_text_border_icon) as DmList;
        foreach (atom_movable potential_target in Dm.each_cast<atom_movable>(checked_targets.Copy()))
        {
            if (QDELETED(potential_target) || potential_target.loc == null)
                Dm.remove(checked_targets, potential_target);
        }
        if (!Dm.truthy(length(checked_targets)))
            return Dm.list();
        return candidate_list;
    }

    // tg: code/controllers/subsystem/polling.dm:286
    public virtual async DmTask<object> is_eligible(mob potential_candidate = null, object role = null, object check_jobban = null, object the_ignore_category = null)
    {
        await DmTask.CompletedTask;
        if (potential_candidate.key == null || potential_candidate.client == null)
            return FALSE;
        if (Dm.truthy(the_ignore_category))
        {
            if (Dm.@in(potential_candidate.ckey, GLOB.poll_ignore[the_ignore_category]))
                return FALSE;
        }
        if (Dm.truthy(role) && potential_candidate.client != null)
        {
            if (!Dm.@in(role, potential_candidate.client.prefs.be_special))
                return FALSE;
            if (Dm.gt(potential_candidate.client.get_days_to_play_antag(role), 0))
                return FALSE;
        }

        if (Dm.truthy(check_jobban))
        {
            if (Dm.truthy(is_banned_from(potential_candidate.ckey, Dm.add(Dm.list(ROLE_SYNDICATE), check_jobban))))
                return FALSE;
        }

        return TRUE;
    }

    // tg: code/controllers/subsystem/polling.dm:304
    public virtual void polling_finished(datum_candidate_poll finishing_poll = null)
    {
        Dm.remove(currently_polling, finishing_poll);
        // Trim players who aren't eligible anymore
        float length_pre_trim = length(finishing_poll.signed_up);
        finishing_poll.trim_candidates();

        log_ghost_poll("Candidate poll completed.", data: Dm.list(
            Dm.kv("role name", finishing_poll.role),
            Dm.kv("poll question", finishing_poll.question),
            Dm.kv("signed up count", length_pre_trim),
            Dm.kv("trimmed candidate count", length(finishing_poll.signed_up))
        ));

        finishing_poll.finished = TRUE;

        // Take care of updating the remaining screen alerts if a similar poll is found, or deleting them.
        if (Dm.truthy(length(finishing_poll.alert_buttons)))
        {
            foreach (atom_movable_screen_alert_poll_alert alert in Dm.each_cast<atom_movable_screen_alert_poll_alert>(finishing_poll.alert_buttons.Copy()))
            {
                if (Dm.truthy(duplicate_message_check(finishing_poll)))
                    alert.update_stacks_overlay();
                else
                    alert.owner.clear_alert(Dm.text("[]_poll_alert", finishing_poll.poll_key));
            }
        }

        //More than enough time for the the `UNTIL()` stopping loop in `poll_candidates()` to be over, and the results to be turned in.
        // QDEL_IN(finishing_poll, 0.5 SECONDS) (__DEFINES/qdel.dm:60), expanded inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 0.5f * SECONDS > GC_FILTER_QUEUE ? WEAKREF(finishing_poll) : finishing_poll), 0.5f * SECONDS, file: "code/controllers/subsystem/polling.dm", line: 328);
    }

    // tg: code/controllers/subsystem/polling.dm:330
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/polling.dm:330", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    ///Is there a multiple of the given event type running right now?
    // tg: code/controllers/subsystem/polling.dm:338
    public virtual object duplicate_message_check(datum_candidate_poll poll_to_check = null)
    {
        foreach (datum_candidate_poll running_poll in Dm.each_cast<datum_candidate_poll>(currently_polling))
        {
            if ((Dm.eq(running_poll.poll_key, poll_to_check.poll_key) && running_poll != poll_to_check) && Dm.num(running_poll.time_left()) > 0)
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/controllers/subsystem/polling.dm:344
    public virtual object get_next_poll_to_finish()
    {
        float lowest_time_left = INFINITY;
        datum_candidate_poll next_poll_to_finish = null;
        foreach (datum_candidate_poll poll in Dm.each_cast<datum_candidate_poll>(currently_polling))
        {
            float time_left = Dm.num(poll.time_left());
            if (time_left >= lowest_time_left)
                continue;
            lowest_time_left = time_left;
            next_poll_to_finish = poll;
        }

        if (next_poll_to_finish == null)
            return FALSE;

        return next_poll_to_finish;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/polling.dm:1  SUBSYSTEM_DEF(polling): GLOBAL_REAL(SSpolling, /datum/controller/subsystem/polling)
    internal datum_controller_subsystem_polling SSpolling;

    // tg: code/controllers/subsystem/polling.dm:253  var/static/list/atom/movable/currently_polling_targets = list()
    internal DmList datum_controller_subsystem_polling__currently_polling_targets;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_polling SSpolling => ref TgState.Current.SSpolling;
}
