// Translated from tgstation (snapshot 1d99bd7f): code/datums/votes/custom_vote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The owner (2026-11-08): the whole file. The var block is generated.

// tg: code/datums/votes/custom_vote.dm:4
public partial class datum_vote_custom_vote : datum_vote
{
    /// The max amount of options someone can have in a custom vote.
    // tg: code/datums/votes/custom_vote.dm:2
    private const int MAX_CUSTOM_VOTE_OPTIONS = 10;

    // Custom votes ares always accessible.
    // tg: code/datums/votes/custom_vote.dm:9
    public override object is_accessible_vote()
    {
        return TRUE;
    }

    // tg: code/datums/votes/custom_vote.dm:12
    public override void reset()
    {
        default_choices = null;
        override_question = null;
        count_method = VOTE_COUNT_METHOD_SINGLE;
        base.reset();
    }

    // tg: code/datums/votes/custom_vote.dm:18
    public override object can_be_initiated(object forced = null)
    {
        object dot = base.can_be_initiated(forced);
        if (!Dm.eq(dot, VOTE_AVAILABLE))
            return dot;
        if (Dm.truthy(forced))
            return dot;

        // Custom votes can only be created if they're forced to be made.
        // (Either an admin makes it, or otherwise.)
        return "Only admins can create custom votes.";
    }

    // tg: code/datums/votes/custom_vote.dm:29
    public override async DmTask<object> create_vote(mob vote_creator = null)
    {
        object custom_count_method = await tgui_input_list(
            user: vote_creator,
            message: "Single or multiple choice?",
            title: "Choice Method",
            items: Dm.list("Single", "Multiple"),
            @default: "Single");
        switch (custom_count_method)
        {
            case "Single":
                count_method = VOTE_COUNT_METHOD_SINGLE;
                break;
            case "Multiple":
                count_method = VOTE_COUNT_METHOD_MULTI;
                break;
            case null:
                return FALSE;
            default:
                stack_trace(Dm.text("Got '[]' in create_vote() for custom voting.", custom_count_method), "code/datums/votes/custom_vote.dm", 46);
                to_chat(vote_creator, span_boldwarning(I18n.T("Unknown choice method. Contact a coder.")));
                return FALSE;
        }

        object custom_win_method = await tgui_input_list(
            user: vote_creator,
            message: "How should the vote winner be determined?",
            title: "Winner Method",
            items: Dm.list("Simple", "Weighted Random", "No Winner"),
            @default: "Simple");
        switch (custom_win_method)
        {
            case "Simple":
                winner_method = VOTE_WINNER_METHOD_SIMPLE;
                break;
            case "Weighted Random":
                winner_method = VOTE_WINNER_METHOD_WEIGHTED_RANDOM;
                break;
            case "No Winner":
                winner_method = VOTE_WINNER_METHOD_NONE;
                break;
            case null:
                return FALSE;
            default:
                stack_trace(Dm.text("Got '[]' in create_vote() for custom voting.", custom_win_method), "code/datums/votes/custom_vote.dm", 66);
                to_chat(vote_creator, span_boldwarning(I18n.T("Unknown winner method. Contact a coder.")));
                return FALSE;
        }

        object display_stats = await tgui_alert(
            vote_creator,
            "Should voting statistics be public?",
            "Show voting stats?",
            Dm.list("Yes", "No"));

        if (display_stats == null)
            return FALSE;
        display_statistics = Dm.eq(display_stats, "Yes") ? TRUE : FALSE;

        if (!Dm.truthy(display_statistics))
        {
            object set_print_result = await tgui_alert(
                vote_creator,
                "Should the vote tally be public after the vote is concluded?",
                "Print vote tally after vote?",
                Dm.list("Yes", "No"));

            if (set_print_result == null)
                return FALSE;

            print_results = Dm.eq(set_print_result, "Yes") ? TRUE : FALSE;
        }

        override_question = await tgui_input_text(vote_creator, "What is the vote for?", "Custom Vote");
        if (!Dm.truthy(override_question))
            return FALSE;

        default_choices = Dm.list();
        for (int i = 1; i <= MAX_CUSTOM_VOTE_OPTIONS; i++)
        {
            object option = await tgui_input_text(vote_creator, Dm.text("Please enter an option, or hit cancel to finish. [] max.", MAX_CUSTOM_VOTE_OPTIONS), "Options", max_length: MAX_NAME_LEN);
            if (vote_creator?.client == null)
                return FALSE;
            if (!Dm.truthy(option))
                break;

            Dm.append(ref default_choices, capitalize(option));
        }

        if (Dm.length(default_choices) == 0)
            return FALSE;
        // Sanity for all the tgui input stalling we are doing
        if (vote_creator.client?.holder == null)
            return FALSE;

        return await base.create_vote(vote_creator);
    }

    // tg: code/datums/votes/custom_vote.dm:115
    public override object initiate_vote(object initiator = null, object duration = null)
    {
        object dot = base.initiate_vote(initiator, duration);
        return Dm.text("[]\n[]", dot, override_question);
    }
}
