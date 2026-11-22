// Translated from tgstation (snapshot 1d99bd7f): code/datums/candidate_poll.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file (the ghosts' polls, SSpolling). The var block is generated.

/// The datum that describes one instance of candidate polling
// tg: code/datums/candidate_poll.dm:2
public partial class datum_candidate_poll : datum
{
    // tg: code/datums/candidate_poll.dm:33
    [DmParams("polled_role", "polled_question", "poll_duration", "poll_ignoring_category", "poll_jumpable", "custom_response_messages")]
    public override async DmTask New(params object[] __extra)
    {
        object polled_role = Dm.arg(__extra, 0);
        object polled_question = Dm.arg(__extra, 1);
        object poll_duration = Dm.arg(__extra, 2);
        object poll_ignoring_category = Dm.arg(__extra, 3);
        object poll_jumpable = Dm.arg(__extra, 4);
        DmList custom_response_messages = Dm.arg<DmList>(__extra, 5) ?? Dm.list();
        role = polled_role;
        question = polled_question;
        duration = poll_duration;
        ignoring_category = poll_ignoring_category;
        jump_to_me = poll_jumpable as atom;
        signed_up = Dm.list();
        time_started = world.time;
        poll_key = Dm.text("[]_[]", question, Dm.truthy(role) ? role : "0");
        if (Dm.truthy(custom_response_messages.len))
            response_messages = custom_response_messages;
        foreach (object individual_message in Dm.each(response_messages.Copy()))
            response_messages[individual_message] = replacetext(response_messages[individual_message], "%ROLE%", role);
        await base.New();
    }

    // tg: code/datums/candidate_poll.dm:55
    public override object Destroy(float? force = null)
    {
        if (Dm.@in(this, SSpolling.currently_polling))
        {
            SSpolling.polling_finished(this);
            return QDEL_HINT_IWILLGC; // the above proc will call QDEL_IN(src, 0.5 SECONDS)
        }
        jump_to_me = null;
        signed_up = null;
        return base.Destroy(force);
    }

    // tg: code/datums/candidate_poll.dm:63
    [SignalHandler]
    public virtual void clear_alert_ref(atom_movable_screen_alert_poll_alert source = null)
    {
        Dm.remove(alert_buttons, source);
    }

    // tg: code/datums/candidate_poll.dm:67
    public virtual object sign_up(mob candidate = null, float? silent = null)
    {
        silent ??= FALSE;
        if (candidate == null || candidate.key == null || candidate.client == null)
            return FALSE;
        if (Dm.@in(candidate, signed_up))
        {
            if (!Dm.truthy(silent))
                to_chat(candidate, span_warning(Dm.str(response_messages[POLL_RESPONSE_ALREADY_SIGNED])));
            return FALSE;
        }
        if (Dm.num(time_left()) <= 0)
        {
            if (!Dm.truthy(silent))
            {
                to_chat(candidate, span_danger("Sorry, you were too late for the consideration!"));
                Dm.output_to(candidate, sound(Dm.resource("sound/machines/buzz/buzz-sigh.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            }
            return FALSE;
        }

        Dm.append(signed_up, candidate);

        log_ghost_poll(Dm.text("Player [] signed candidate poll", candidate.key), data: Dm.list(
            Dm.kv("player key", candidate.key),
            Dm.kv("role name", role),
            Dm.kv("poll question", question)
        ));

        if (!Dm.truthy(silent))
        {
            to_chat(candidate, span_notice(Dm.str(response_messages[POLL_RESPONSE_SIGNUP])));
            // Sign them up for any other polls with the same mob type
            foreach (datum_candidate_poll existing_poll in Dm.each_cast<datum_candidate_poll>(SSpolling.currently_polling))
            {
                if (this != existing_poll && Dm.eq(poll_key, existing_poll.poll_key) && !Dm.@in(candidate, existing_poll.signed_up))
                    existing_poll.sign_up(candidate, TRUE);
            }
        }
        foreach (atom_movable_screen_alert_poll_alert linked_button in Dm.each_cast<atom_movable_screen_alert_poll_alert>(alert_buttons))
            linked_button.update_candidates_number_overlay();
        return TRUE;
    }

    // tg: code/datums/candidate_poll.dm:98
    public virtual object remove_candidate(mob candidate = null, float? silent = null)
    {
        silent ??= FALSE;
        if (candidate == null || candidate.key == null || candidate.client == null)
            return FALSE;
        if (!Dm.@in(candidate, signed_up))
        {
            if (!Dm.truthy(silent))
                to_chat(candidate, span_warning(Dm.str(response_messages[POLL_RESPONSE_NOT_SIGNED])));
            return FALSE;
        }

        if (Dm.num(time_left()) <= 0)
        {
            if (!Dm.truthy(silent))
                to_chat(candidate, span_danger(Dm.str(response_messages[POLL_RESPONSE_TOO_LATE_TO_UNREGISTER])));
            return FALSE;
        }

        Dm.remove(signed_up, candidate);

        log_ghost_poll(Dm.text("Player [] removed from poll candidacy", candidate.key), data: Dm.list(
            Dm.kv("player key", candidate.key),
            Dm.kv("role name", role),
            Dm.kv("poll question", question)
        ));

        if (!Dm.truthy(silent))
        {
            to_chat(candidate, span_danger(Dm.str(response_messages[POLL_RESPONSE_UNREGISTERED])));

            foreach (datum_candidate_poll existing_poll in Dm.each_cast<datum_candidate_poll>(SSpolling.currently_polling))
            {
                if (this != existing_poll && Dm.eq(poll_key, existing_poll.poll_key) && Dm.@in(candidate, existing_poll.signed_up))
                    existing_poll.remove_candidate(candidate, TRUE);
            }
        }
        foreach (atom_movable_screen_alert_poll_alert linked_button in Dm.each_cast<atom_movable_screen_alert_poll_alert>(alert_buttons))
            linked_button.update_candidates_number_overlay();
        return TRUE;
    }

    // tg: code/datums/candidate_poll.dm:129
    public virtual void do_never_for_this_round(mob candidate = null)
    {
        DmList ignore_list = GLOB.poll_ignore[ignoring_category] as DmList;
        if (ignore_list == null)
            GLOB.poll_ignore[ignoring_category] = Dm.list();
        Dm.append(GLOB.poll_ignore[ignoring_category] as DmList, candidate.ckey);
        to_chat(candidate, span_danger("Choice registered: Never for this round."));
        remove_candidate(candidate, silent: TRUE);
    }

    // tg: code/datums/candidate_poll.dm:137
    public virtual void undo_never_for_this_round(mob candidate = null)
    {
        Dm.remove(GLOB.poll_ignore[ignoring_category] as DmList, candidate.ckey);
        to_chat(candidate, span_notice("Choice registered: Eligible for this round"));
    }

    // tg: code/datums/candidate_poll.dm:141
    public virtual void trim_candidates()
    {
        list_clear_nulls(signed_up);
        foreach (mob candidate in Dm.each_cast<mob>(signed_up.Copy()))
        {
            if (candidate.key == null || candidate.client == null)
                Dm.remove(signed_up, candidate);
        }
    }

    // tg: code/datums/candidate_poll.dm:147
    public virtual object time_left()
    {
        return Dm.num(duration) - (world.time - Dm.num(time_started));
    }


    /// Print to chat which candidate was selected
    // tg: code/datums/candidate_poll.dm:152
    public virtual void announce_chosen(DmList poll_recipients = null)
    {
        if (!Dm.truthy(length(chosen_candidates)))
            return;
        foreach (mob chosen in Dm.each<mob>(chosen_candidates))
        {
            client chosen_client = chosen.client;
            foreach (mob poll_recipient in Dm.each_cast<mob>(poll_recipients))
                to_chat(poll_recipient, span_ooc(Dm.text("[][][] was selected.", isobserver(poll_recipient) ? FOLLOW_LINK(poll_recipient, chosen_client.mob) : null, span_warning(Dm.text(" [] Poll: ", full_capitalize(role))), key_name(chosen_client, include_name: FALSE))));
        }
    }
}
