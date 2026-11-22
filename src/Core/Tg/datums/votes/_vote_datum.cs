// Translated from tgstation (snapshot 1d99bd7f): code/datums/votes/_vote_datum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The owner (2026-11-08): the whole file. The var block is generated.

/**
 * # Vote Singleton
 *
 * A singleton datum that represents a type of vote for the voting subsystem.
 */
// tg: code/datums/votes/_vote_datum.dm:6
public partial class datum_vote : datum
{
    /**
     * Used to determine if this vote is a possible
     * vote type for the vote subsystem.
     *
     * If FALSE is returned, this vote singleton
     * will not be created when the vote subsystem initializes,
     * meaning no one will be able to hold this vote.
     */
    // tg: code/datums/votes/_vote_datum.dm:49
    public virtual object is_accessible_vote()
    {
        return Dm.length(default_choices) > 0 ? TRUE : FALSE;
    }

    /**
     * Resets our vote to its default state.
     */
    // tg: code/datums/votes/_vote_datum.dm:55
    public virtual void reset()
    {
        choices.Cut();
        choices_by_ckey.Cut();
        started_time = null;
        time_remaining = -1;
    }

    /**
     * If this vote has a config associated, toggles it between enabled and disabled.
     */
    // tg: code/datums/votes/_vote_datum.dm:66
    public virtual object toggle_votable()
    {
        return null;
    }

    /**
     * If this vote has a config associated, returns its value (True or False, usually).
     * If it has no config, returns -1.
     */
    // tg: code/datums/votes/_vote_datum.dm:73
    public virtual object is_config_enabled()
    {
        return -1;
    }

    /**
     * Checks if the passed mob can initiate this vote.
     *
     * * forced - if being invoked by someone who is an admin
     *
     * Return VOTE_AVAILABLE if the mob can initiate the vote.
     * Return a string with the reason why the mob can't initiate the vote.
     */
    // tg: code/datums/votes/_vote_datum.dm:84
    public virtual object can_be_initiated(object forced = null)
    {
        forced ??= FALSE;
        if (!Dm.truthy(forced) && !Dm.truthy(is_config_enabled()))
            return "This vote is currently disabled by the server configuration.";

        return VOTE_AVAILABLE;
    }

    /**
     * Called prior to the vote being initiated.
     *
     * Return FALSE to prevent the vote from being initiated.
     */
    // tg: code/datums/votes/_vote_datum.dm:97
    public virtual async DmTask<object> create_vote(mob vote_creator = null)
    {
        foreach (object key in Dm.each(default_choices))
            choices[key] = 0;

        return TRUE;
    }

    /**
     * Called when this vote is actually initiated.
     *
     * Return a string - the text displayed to the world when the vote is initiated.
     */
    // tg: code/datums/votes/_vote_datum.dm:110
    public virtual object initiate_vote(object initiator = null, object duration = null)
    {
        started_time = world.time;
        time_remaining = Dm.round(Dm.num(duration) / 10);

        return Dm.text("[] started by [].", Dm.truthy(contains_vote_in_name) ? Dm.text("[]", capitalize(name)) : Dm.text("[] vote", capitalize(name)), Dm.truthy(initiator) ? initiator : "Central Command");
    }

    /**
     * Gets the result of the vote.
     *
     * non_voters - a list of all ckeys who didn't vote in the vote.
     *
     * Returns a list of all options that won.
     * If there were no votes at all, the list will be length = 0, non-null.
     * If only one option one, the list will be length = 1.
     * If there was a tie, the list will be length > 1.
     */
    // tg: code/datums/votes/_vote_datum.dm:129
    public virtual DmList get_vote_result(DmList non_voters = null)
    {
        switch (winner_method)
        {
            case VOTE_WINNER_METHOD_NONE:
                return Dm.list();
            case VOTE_WINNER_METHOD_SIMPLE:
                return get_simple_winner();
            case VOTE_WINNER_METHOD_WEIGHTED_RANDOM:
                return get_random_winner();
        }

        stack_trace(Dm.text("invalid select winner method: []. Defaulting to simple.", winner_method), "code/datums/votes/_vote_datum.dm", 141);
        return get_simple_winner();
    }

    /// Gets the winner of the vote, selecting the choice with the most votes.
    // tg: code/datums/votes/_vote_datum.dm:145
    public virtual DmList get_simple_winner()
    {
        float highest_vote = 0;
        DmList current_winners = Dm.list();

        foreach (object option in Dm.each(choices))
        {
            float vote_count = Dm.num(choices[option]);
            if (vote_count < highest_vote)
                continue;

            if (vote_count > highest_vote)
            {
                highest_vote = vote_count;
                current_winners = Dm.list(option);
                continue;
            }
            Dm.append(ref current_winners, option);
        }

        return Dm.length(current_winners) > 0 ? current_winners : Dm.list();
    }

    /// Gets the winner of the vote, selecting a random choice from all choices based on their vote count.
    // tg: code/datums/votes/_vote_datum.dm:163
    public virtual DmList get_random_winner()
    {
        object winner = pick_weight(choices);
        return Dm.truthy(winner) ? Dm.list(winner) : Dm.list();
    }

    /**
     * Gets the resulting text displayed when the vote is completed.
     *
     * all_winners - list of all options that won. Can be multiple, in the event of ties.
     * real_winner - the option that actually won.
     * non_voters - a list of all ckeys who didn't vote in the vote.
     *
     * Return a formatted string of text to be displayed to everyone.
     */
    // tg: code/datums/votes/_vote_datum.dm:176
    public virtual object get_result_text(DmList all_winners = null, object real_winner = null, DmList non_voters = null)
    {
        string title_text = "";
        string returned_text = "";
        if (Dm.truthy(override_question))
            title_text += span_bold(Dm.str(override_question));
        else
            title_text += span_bold(Dm.text("[] Vote", capitalize(name)));

        returned_text += "Winner Selection: ";
        switch (winner_method)
        {
            case VOTE_WINNER_METHOD_NONE:
                returned_text += "None";
                break;
            case VOTE_WINNER_METHOD_WEIGHTED_RANDOM:
                returned_text += "Weighted Random";
                break;
            default:
                returned_text += "Simple";
                break;
        }

        float total_votes = 0; // for determining percentage of votes
        foreach (object option in Dm.each(choices))
            total_votes += Dm.num(choices[option]);

        if (total_votes <= 0)
            return span_bold("Vote Result: Inconclusive - No Votes!");

        if (Dm.truthy(display_statistics) || Dm.truthy(print_results))
        {
            returned_text += "\nResults:";
            foreach (object option in Dm.each(choices))
            {
                returned_text += "\n";
                float votes = Dm.num(choices[option]);
                string percentage_text = "";
                if (votes > 0)
                {
                    float actual_percentage = Dm.round((votes / total_votes) * 100, 0.1f);
                    string text = Dm.text("[]", actual_percentage);
                    float spaces_needed = 5 - Dm.length(text);
                    for (int _ = 1; _ <= spaces_needed; _++)
                        returned_text += " ";
                    percentage_text += Dm.text("[]%", text);
                }
                else
                    percentage_text = "    0%";
                returned_text += Dm.text("[] | []: []", percentage_text, span_bold(Dm.text("[]", option)), choices[option]);
            }
        }

        if (!Dm.truthy(real_winner)) // vote has no winner or cannot be won, but still had votes
            return returned_text;

        returned_text += "\n";
        returned_text += Dm.str(get_winner_text(all_winners, real_winner, non_voters));

        return fieldset_block(title_text, returned_text, "boxed_message purple_box");
    }

    /**
     * Gets the text that displays the winning options within the result text.
     *
     * all_winners - list of all options that won. Can be multiple, in the event of ties.
     * real_winner - the option that actually won.
     * non_voters - a list of all ckeys who didn't vote in the vote.
     *
     * Return a formatted string of text to be displayed to everyone.
     */
    // tg: code/datums/votes/_vote_datum.dm:232
    public virtual object get_winner_text(DmList all_winners = null, object real_winner = null, DmList non_voters = null)
    {
        string returned_text = "";
        if (Dm.length(all_winners) > 1)
        {
            returned_text += Dm.text("\n[]", span_bold("Vote Tied Between:"));
            foreach (object a_winner in Dm.each(all_winners))
                returned_text += Dm.text("\n\t[]", a_winner);
        }

        returned_text += span_bold(Dm.text("\nVote Result: []", real_winner));
        return returned_text;
    }

    /**
     * How this vote handles a tiebreaker between multiple winners.
     */
    // tg: code/datums/votes/_vote_datum.dm:246
    public virtual object tiebreaker(DmList winners = null)
    {
        return pick(winners);
    }

    /**
     * Called when a vote is actually all said and done.
     * Apply actual vote effects here.
     */
    // tg: code/datums/votes/_vote_datum.dm:252
    public virtual void finalize_vote(object winning_option = null)
    {
    }
}
