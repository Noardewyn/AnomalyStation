// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/vote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The owner (2026-11-08): the whole file, the vote verb and the vote action. The var blocks are generated. The chat line's
// "click here" link is BYOND's winset command link (byond://winset?command=vote), which ClientSeam.Topic runs as the verb.

// tg: code/controllers/subsystem/vote.dm:4  SUBSYSTEM_DEF(vote)
public partial class datum_controller_subsystem_vote : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(vote) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSvote) (__DEFINES/MC.dm:29) expanded inline.
    // tg: code/controllers/subsystem/vote.dm:4
    public override async DmTask New(params object[] __extra)
    {
        if (SSvote != this)
        {
            if (SSvote is datum_controller_subsystem_vote)
            {
                Recover();
                qdel(SSvote);
            }
            SSvote = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/vote.dm:23
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        foreach (Type vote_type in Dm.each<Type>(subtypesof(typeof(datum_vote))))
        {
            datum_vote vote = await Dm.new_async(vote_type) as datum_vote;
            if (!Dm.truthy(vote.is_accessible_vote()))
            {
                qdel(vote);
                continue;
            }

            possible_votes[vote.name] = vote;
        }

        return SS_INIT_SUCCESS;
    }

    // Called by master_controller
    // tg: code/controllers/subsystem/vote.dm:35
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        if (current_vote == null)
            return;
        current_vote.time_remaining = Dm.round((Dm.num(current_vote.started_time) + Dm.num(config.Get(typeof(datum_config_entry_number_vote_period))) - world.time) / 10);
        if (current_vote.time_remaining < 0)
            end_vote();
    }

    /// Ends the current vote.
    // tg: code/controllers/subsystem/vote.dm:43
    public virtual void end_vote()
    {
        if (current_vote == null)
            throw CRASH("assertion failed: current_vote"); // ASSERT(current_vote)
        process_vote_result();
        SStgui.close_uis(this);
        reset();
    }

    /// Resets all of our vars after votes conclude / are cancelled.
    // tg: code/controllers/subsystem/vote.dm:50
    public virtual void reset()
    {
        voted.Cut();
        voting.Cut();

        current_vote?.reset();
        current_vote = null;

        QDEL_LIST(generated_actions);

        SStgui.update_uis(this);
    }

    /**
     * Process the results of the vote.
     * Collects all the winners, breaks any ties that occur,
     * prints the results of the vote to the world,
     * and finally follows through with the effects of the vote.
     */
    // tg: code/controllers/subsystem/vote.dm:67
    public virtual void process_vote_result()
    {
        // First collect all the non-voters we have.
        DmList non_voters = Dm.sub(GLOB.directory.Copy(), voted) as DmList;
        // Remove AFK or clientless non-voters.
        foreach (object non_voter_ckey in Dm.each(non_voters.Copy()))
        {
            client non_voter_client = non_voters[non_voter_ckey] as client;
            if (non_voter_client == null || Dm.truthy(non_voter_client.is_afk()))
                Dm.remove(ref non_voters, non_voter_ckey);
        }

        // Now get the result of the vote.
        // This is a list, as we could have a tie (multiple winners).
        DmList winners = current_vote.get_vote_result(non_voters);

        // Now we should determine who actually won the vote.
        object final_winner = null;
        // 1 winner? That's the winning option
        if (Dm.length(winners) == 1)
            final_winner = winners[1];

        // More than 1 winner? Tiebreaker between all the winners
        else if (Dm.length(winners) > 1)
            final_winner = current_vote.tiebreaker(winners);

        // Announce the results of the vote to the world.
        object to_display = current_vote.get_result_text(winners, final_winner, non_voters);

        float total_votes = 0;
        DmList vote_choice_data = Dm.list();
        foreach (object choice in Dm.each(current_vote.choices))
        {
            object choice_votes = current_vote.choices[choice];
            total_votes += Dm.num(choice_votes);
            vote_choice_data[Dm.text("[]", choice)] = choice_votes;
        }

        // stringify the winners to prevent potential unimplemented serialization errors.
        // Perhaps this can be removed in the future and we assert that vote choices must implement serialization.
        string final_winner_string = Dm.truthy(final_winner) ? Dm.text("[]", final_winner) : "NO WINNER";
        DmList winners_string = Dm.list();

        if (Dm.length(winners) > 0)
        {
            foreach (object winner in Dm.each(winners))
                Dm.append(ref winners_string, Dm.text("[]", winner));
        }
        else
            winners_string = Dm.list("NO WINNER");

        DmList vote_log_data = Dm.list(
            Dm.kv("type", Dm.text("[]", current_vote.type)),
            Dm.kv("choices", vote_choice_data),
            Dm.kv("total", total_votes),
            Dm.kv("winners", winners_string),
            Dm.kv("final_winner", final_winner_string));
        log_vote("vote finalized", vote_log_data);
        if (Dm.truthy(to_display))
            to_chat(world, span_infoplain(vote_font(Dm.text("[]", to_display))));

        // Finally, doing any effects on vote completion
        current_vote.finalize_vote(final_winner);
    }

    // tg: code/controllers/subsystem/vote.dm:2  #define vote_font(text) ("<font color='purple'>" + text + "</font>")
    private static string vote_font(string text) => "<font color='purple'>" + text + "</font>";

    /**
     * One selection per person, and the selection with the most votes wins.
     */
    // tg: code/controllers/subsystem/vote.dm:128
    public virtual object submit_single_vote(mob voter = null, object their_vote = null)
    {
        if (current_vote == null)
            return null;
        if (!Dm.truthy(voter?.ckey))
            return null;
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_dead_vote))) && Dm.eq(voter.stat, DEAD) && voter.client?.holder == null)
            return null;
        // the port: a choice the vote does not have would be counted from nothing (tg's list would grow by it); a forged one is dropped
        if (!Dm.@in(their_vote, current_vote.choices))
            return null;

        // If user has already voted, remove their specific vote
        if (Dm.@in(voter.ckey, current_vote.choices_by_ckey))
        {
            object their_old_vote = current_vote.choices_by_ckey[voter.ckey];
            current_vote.choices[their_old_vote] = Dm.sub(current_vote.choices[their_old_vote], 1);
        }

        else
            Dm.append(ref voted, voter.ckey);

        current_vote.choices_by_ckey[voter.ckey] = their_vote;
        current_vote.choices[their_vote] = Dm.add(current_vote.choices[their_vote], 1);

        return TRUE;
    }

    /**
     * Any number of selections per person, and the selection with the most votes wins.
     */
    // tg: code/controllers/subsystem/vote.dm:153
    public virtual object submit_multi_vote(mob voter = null, object their_vote = null)
    {
        if (current_vote == null)
            return null;
        if (!Dm.truthy(voter?.ckey))
            return null;
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_dead_vote))) && Dm.eq(voter.stat, DEAD) && voter.client?.holder == null)
            return null;
        if (!Dm.@in(their_vote, current_vote.choices)) // the port, as submit_single_vote
            return null;

        else
            Dm.append(ref voted, voter.ckey);

        string key = Dm.str(voter.ckey) + Dm.str(their_vote);
        if (Dm.eq(current_vote.choices_by_ckey[key], 1))
        {
            current_vote.choices_by_ckey[key] = 0;
            current_vote.choices[their_vote] = Dm.sub(current_vote.choices[their_vote], 1);
        }

        else
        {
            current_vote.choices_by_ckey[key] = 1;
            current_vote.choices[their_vote] = Dm.add(current_vote.choices[their_vote], 1);
        }

        return TRUE;
    }

    /**
     * Initiates a vote, allowing all players to vote on something.
     *
     * * vote_type - The type of vote to initiate. Can be a [/datum/vote] typepath, a [/datum/vote] instance, or the name of a vote datum.
     * * vote_initiator_name - The ckey (if player initiated) or name that initiated a vote. Ex: "UristMcAdmin", "the server"
     * * vote_initiator - If a person / mob initiated the vote, this is the mob that did it
     * * forced - Whether we're forcing the vote to go through regardless of existing votes or other circumstances.
     */
    // tg: code/controllers/subsystem/vote.dm:183
    public virtual async DmTask<object> initiate_vote(object vote_type = null, object vote_initiator_name = null, mob vote_initiator = null, object forced = null)
    {
        forced ??= FALSE;
        if (!Dm.truthy(can_vote_start(vote_initiator, forced)))
            return FALSE;

        // Get our actual datum
        datum_vote to_vote;
        // If we were passed a path: find the path in possible_votes
        if (ispath(vote_type, typeof(datum_vote)))
        {
            Type vote_path = vote_type as Type;
            to_vote = possible_votes[Dm.initial<datum_vote, object>(vote_path, v => v.name)] as datum_vote;
        }

        // If we were passed an instance: use the instance
        else if (vote_type is datum_vote instance)
            to_vote = instance;

        // If we got neither a path or an instance, it could be a vote name, but is likely just an error / null
        else
        {
            to_vote = possible_votes[vote_type] as datum_vote;
            if (to_vote == null)
                stack_trace(Dm.text("Voting initiate_vote was passed an invalid vote type. (Got: [])", Dm.truthy(vote_type) ? vote_type : "null"), "code/controllers/subsystem/vote.dm", 204);
        }

        // No valid vote found? No vote
        if (to_vote == null)
        {
            if (vote_initiator != null)
                to_chat(vote_initiator, span_warning(I18n.T("Invalid voting choice.")));
            return FALSE;
        }

        // Vote can't be initiated in our circumstances? No vote
        if (!Dm.eq(to_vote.can_be_initiated(forced), VOTE_AVAILABLE))
            return FALSE;

        // Okay, we're ready to actually create a vote -
        // Do a reset, just to make sure
        reset();

        // Try to create the vote. If the creation fails, no vote
        if (!Dm.truthy(await to_vote.create_vote(vote_initiator)))
            return FALSE;

        if (!Dm.truthy(vote_initiator_name) && vote_initiator != null)
            vote_initiator_name = vote_initiator.key;

        // Okay, the vote's happening now, for real. Set it up.
        current_vote = to_vote;
        last_vote_time = world.time;

        object duration = config.Get(typeof(datum_config_entry_number_vote_period));
        object to_display = current_vote.initiate_vote(vote_initiator_name, duration);

        log_vote(to_display);
        to_chat(world, custom_boxed_message("purple_box center", span_infoplain(vote_font(Dm.text("[]<br>Type <b>vote</b> or click <a href='byond://winset?command=vote'>here</a> to place your votes.\nYou have [] to vote.",
            span_bold(Dm.text("[]", to_display)), DisplayTimeText(duration))))));

        // And now that it's going, give everyone a voter action
        foreach (client new_voter in Dm.each<client>(GLOB.clients))
        {
            datum_action_vote voting_action = Dm.@new<datum_action_vote>();
            voting_action.name = Dm.text("Vote: []", Dm.truthy(current_vote.override_question) ? current_vote.override_question : current_vote.name);
            await voting_action.Grant(new_voter.mob);

            Dm.append(ref new_voter.persistent_client.player_actions, voting_action);
            Dm.append(ref generated_actions, voting_action);
            if (Dm.truthy(current_vote.vote_sound) && Dm.truthy(new_voter.prefs.read_preference(typeof(datum_preference_toggle_sound_announcements))))
                Dm.output_to(new_voter, sound(current_vote.vote_sound)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        }

        return TRUE;
    }

    /**
     * Checks if we can start a vote.
     *
     * * vote_initiator - The mob that initiated the vote.
     * * forced - Whether we're forcing the vote to go through regardless of existing votes or other circumstances.
     *
     * Returns TRUE if we can start a vote, FALSE if we can't.
     */
    // tg: code/controllers/subsystem/vote.dm:254
    public virtual object can_vote_start(mob vote_initiator = null, object forced = null)
    {
        // Even if it's forced we can't vote before we're set up
        if (!MC_RUNNING(init_stage))
        {
            if (vote_initiator != null)
                to_chat(vote_initiator, span_warning(I18n.T("You cannot start a vote now, the server is not done initializing.")));
            return FALSE;
        }

        if (Dm.truthy(forced))
            return TRUE;

        float next_allowed_time = last_vote_time + Dm.num(config.Get(typeof(datum_config_entry_number_vote_delay)));
        if (next_allowed_time > world.time)
        {
            if (vote_initiator != null)
                to_chat(vote_initiator, span_warning(I18n.T("A vote was initiated recently. You must wait [] before a new vote can be started!", DisplayTimeText(next_allowed_time - world.time))));
            return FALSE;
        }

        if (current_vote != null)
        {
            if (vote_initiator != null)
                to_chat(vote_initiator, span_warning(I18n.T("There is already a vote in progress! Please wait for it to finish.")));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/controllers/subsystem/vote.dm:279
    public virtual async DmTask toggle_dead_voting(mob toggle_initiator = null)
    {
        object switch_deadvote_config = Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_dead_vote))) ? FALSE : TRUE;
        Waiting.NoSleep(config.Set(typeof(datum_config_entry_flag_no_dead_vote), switch_deadvote_config));
        string text_verb = !Dm.truthy(switch_deadvote_config) ? "enabled" : "disabled";
        log_admin(Dm.text("[] [] Dead Vote.", key_name(toggle_initiator), text_verb));
        message_admins(Dm.text("[] [] Dead Vote.", key_name_admin(toggle_initiator), text_verb));
        SSblackbox.record_feedback("nested tally", "admin_toggle", 1, Dm.list("Toggle Dead Vote", text_verb));
        await update_static_data_for_all_viewers();
    }

    // tg: code/controllers/subsystem/vote.dm:288
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/controllers/subsystem/vote.dm:291
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        // Tracks who is currently voting
        object ckey = user.client?.ckey;
        if (!Dm.@in(ckey, voting))
            Dm.append(ref voting, ckey); // voting |= user.client?.ckey
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "VotePanel");
            await ui.open();
        }
        return null;
    }

    // tg: code/controllers/subsystem/vote.dm:299
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        bool is_lower_admin = user.client?.holder != null;
        bool is_upper_admin = Dm.truthy(check_rights_for(user.client, R_ADMIN));

        data["user"] = Dm.list(
            Dm.kv("ckey", user.client?.ckey),
            Dm.kv("isGhost", Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_dead_vote))) && Dm.eq(user.stat, DEAD) && user.client?.holder == null ? TRUE : FALSE),
            Dm.kv("isLowerAdmin", is_lower_admin ? TRUE : FALSE),
            Dm.kv("isUpperAdmin", is_upper_admin ? TRUE : FALSE),
            // What the current user has selected in any ongoing votes.
            Dm.kv("singleSelection", current_vote?.choices_by_ckey[user.client?.ckey]),
            Dm.kv("multiSelection", current_vote?.choices_by_ckey));

        DmList all_vote_data = Dm.list();
        foreach (object vote_name in Dm.each(possible_votes))
        {
            if (possible_votes[vote_name] is not datum_vote vote)
                continue;

            object can_vote = vote.can_be_initiated(is_lower_admin ? TRUE : FALSE);
            DmList vote_data = Dm.list(
                Dm.kv("name", vote_name),
                Dm.kv("canBeInitiated", Dm.eq(can_vote, VOTE_AVAILABLE) ? TRUE : FALSE),
                Dm.kv("config", vote.is_config_enabled()),
                Dm.kv("message", Dm.eq(can_vote, VOTE_AVAILABLE) ? vote.default_message : can_vote));

            if (vote == current_vote)
            {
                DmList choices = Dm.list();
                foreach (object key in Dm.each(current_vote.choices))
                {
                    Dm.append(ref choices, Dm.list(Dm.list(
                        Dm.kv("name", key),
                        Dm.kv("votes", current_vote.choices[key]))));
                }

                data["currentVote"] = Dm.list(
                    Dm.kv("name", current_vote.name),
                    Dm.kv("question", current_vote.override_question),
                    Dm.kv("timeRemaining", current_vote.time_remaining),
                    Dm.kv("countMethod", current_vote.count_method),
                    Dm.kv("displayStatistics", current_vote.display_statistics),
                    Dm.kv("choices", choices),
                    Dm.kv("vote", vote_data));
            }

            Dm.append(ref all_vote_data, Dm.list(vote_data));
        }

        data["possibleVotes"] = all_vote_data;
        data["LastVoteTime"] = last_vote_time - world.time;

        return data;
    }

    // tg: code/controllers/subsystem/vote.dm:350
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["VoteCD"] = config.Get(typeof(datum_config_entry_number_vote_delay));
        data["deadVoteEnabled"] = config.Get(typeof(datum_config_entry_flag_no_dead_vote));
        return data;
    }

    // tg: code/controllers/subsystem/vote.dm:356
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob voter = usr as mob;

        switch (action)
        {
            case "cancel":
                if (voter.client?.holder == null)
                {
                    message_admins(Dm.text("[] tried to cancel the current vote while having no admin holder, this is potentially a malicious exploit and worth noting.", key_name(voter)));
                    return null;
                }

                voter.log_message("cancelled a vote.", LOG_ADMIN);
                message_admins(Dm.text("[] has cancelled the current vote.", key_name_admin(voter)));
                reset();
                return TRUE;

            case "endNow":
                if (voter.client?.holder == null)
                {
                    message_admins(Dm.text("[] tried to end the current vote while having no admin holder, this is potentially a malicious exploit and worth noting.", key_name(voter)));
                    return null;
                }

                voter.log_message("ended the current vote early", LOG_ADMIN);
                message_admins(Dm.text("[] has ended the current vote.", key_name_admin(voter)));
                if (current_vote != null) // the port: tg's ASSERT in end_vote would runtime on a click after the vote ended
                    end_vote();
                return TRUE;

            case "toggleDeadVote":
                if (!Dm.truthy(check_rights_for(voter.client, R_ADMIN)))
                {
                    message_admins(Dm.text("[] tried to toggle vote abillity for ghosts while having improper rights, this is potentially a malicious exploit and worth noting.", key_name(voter)));
                    return null;
                }

                await toggle_dead_voting(voter);
                return TRUE;

            case "toggleVote":
            {
                if (possible_votes[@params["voteName"]] is not datum_vote selected)
                    return null;
                if (!Dm.truthy(check_rights_for(voter.client, R_ADMIN)))
                {
                    message_admins(Dm.text("[] tried to toggle vote availability while having improper rights, this is potentially a malicious exploit and worth noting.", key_name(voter)));
                    return null;
                }

                return selected.toggle_votable();
            }

            case "callVote":
            {
                if (possible_votes[@params["voteName"]] is not datum_vote selected)
                    return null;

                // Whether the user actually can initiate this vote is checked in initiate_vote,
                // meaning you can't spoof initiate a vote you're not supposed to be able to
                return await initiate_vote(
                    vote_type: selected,
                    vote_initiator_name: voter.key,
                    vote_initiator: voter,
                    forced: GLOB.admin_datums[voter.ckey] != null ? TRUE : FALSE);
            }

            case "voteSingle":
                return submit_single_vote(voter, @params["voteOption"]);

            case "voteMulti":
                return submit_multi_vote(voter, @params["voteOption"]);

            case "resetCooldown":
                if (voter.client.holder == null)
                {
                    message_admins(Dm.text("[] tried to reset the vote cooldown while having no admin holder, this is potentially a malicious exploit and worth noting.", key_name(voter)));
                    return null;
                }

                last_vote_time = -INFINITY;
                return TRUE;
        }
        return null;
    }

    // tg: code/controllers/subsystem/vote.dm:443
    public override void ui_close(mob user = null)
    {
        Dm.remove(ref voting, user.client?.ckey);
    }
}

public partial class mob : atom_movable
{
    /// Mob level verb that allows players to vote on the current vote.
    // tg: code/controllers/subsystem/vote.dm:447  GAME_VERB(/mob, vote, "Vote", "OOC"): the body
    public virtual async DmTask __gvb_vote(DmList structured_args = null)
    {
        if (!Dm.truthy(SSvote.initialized))
        {
            to_chat(usr, span_notice(I18n.T("<i>Voting is not set up yet!</i>")));
            return;
        }

        await SSvote.ui_interact(usr as mob);
    }

    // tg: code/controllers/subsystem/vote.dm:447  the verb proc
    public virtual void vote(params object[] __extra)
    {
        Waiting.Detach(__gvb_vote(Dm.list()));
    }
}

/// Datum action given to mobs that allows players to vote on the current vote.
// tg: code/controllers/subsystem/vote.dm:454
public partial class datum_action_vote : datum_action
{
    // tg: code/controllers/subsystem/vote.dm:459
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        return TRUE; // Democracy is always available to the free people
    }

    // tg: code/controllers/subsystem/vote.dm:462
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;

        owner.vote();
        await Remove(owner);
        return dot;
    }

    // We also need to remove our action from the player actions when we're cleaning up.
    // tg: code/controllers/subsystem/vote.dm:471
    public override async DmTask Remove(mob removed_from = null)
    {
        if (removed_from.persistent_client != null)
            Dm.remove(ref removed_from.persistent_client.player_actions, this);

        else if (Dm.truthy(removed_from.ckey))
        {
            datum_persistent_client persistent_client = GLOB.persistent_clients_by_ckey[removed_from.ckey] as datum_persistent_client;
            if (persistent_client != null)
                Dm.remove(ref persistent_client.player_actions, this);
        }

        await base.Remove(removed_from);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/vote.dm:4  SUBSYSTEM_DEF(vote): GLOBAL_REAL(SSvote, /datum/controller/subsystem/vote)
    internal datum_controller_subsystem_vote SSvote;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_vote SSvote => ref TgState.Current.SSvote;
}
