// Translated from tgstation (snapshot 1d99bd7f): code/datums/votes/restart_vote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The owner (2026-11-08): the whole file but the map vote's revert (SSmap_vote is not ported: no map rotation list). The var block
// is generated.

// tg: code/datums/votes/restart_vote.dm:4
public partial class datum_vote_restart_vote : datum_vote
{
    // tg: code/datums/votes/restart_vote.dm:1
    private const string CHOICE_RESTART = "Restart Round";
    // tg: code/datums/votes/restart_vote.dm:2
    private const string CHOICE_CONTINUE = "Continue Playing";

    /// This proc checks to see if any admins are online for the purposes of this vote to see if it can pass. Returns TRUE if there are valid admins online (Has +SERVER and is not AFK), FALSE otherwise.
    // tg: code/datums/votes/restart_vote.dm:15
    public virtual object admins_present()
    {
        foreach (client online_admin in Dm.each<client>(GLOB.admins))
        {
            if (Dm.truthy(online_admin.is_afk()) || !Dm.truthy(check_rights_for(online_admin, R_SERVER)))
                continue;

            return TRUE;
        }

        return FALSE;
    }

    // tg: code/datums/votes/restart_vote.dm:24
    public override object toggle_votable()
    {
        Waiting.NoSleep(config.Set(typeof(datum_config_entry_flag_allow_vote_restart), Dm.truthy(config.Get(typeof(datum_config_entry_flag_allow_vote_restart))) ? FALSE : TRUE));
        return null;
    }

    // tg: code/datums/votes/restart_vote.dm:27
    public override object is_config_enabled()
    {
        return config.Get(typeof(datum_config_entry_flag_allow_vote_restart));
    }

    // tg: code/datums/votes/restart_vote.dm:30
    public override async DmTask<object> create_vote(mob vote_creator = null)
    {
        object dot = await base.create_vote(vote_creator);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(admins_present()))
            return dot;
        async_alert_about_admins(vote_creator);
        return dot;
    }

    // tg: code/datums/votes/restart_vote.dm:38
    public virtual void async_alert_about_admins(mob vote_creator = null)
    {
        Waiting.Detach(tgui_alert(vote_creator, "Note: Regardless of the results of this vote, the round will not automatically restart because an active admin is online.")); // set waitfor = FALSE
    }

    // tg: code/datums/votes/restart_vote.dm:43
    public override DmList get_vote_result(DmList non_voters = null)
    {
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_default_no_vote))))
        {
            // Default no votes will add non-voters to "Continue Playing"
            choices[CHOICE_CONTINUE] = Dm.add(choices[CHOICE_CONTINUE], Dm.length(non_voters));
        }

        return base.get_vote_result(non_voters);
    }

    // tg: code/datums/votes/restart_vote.dm:50
    public override void finalize_vote(object winning_option = null)
    {
        if (Dm.eq(winning_option, CHOICE_CONTINUE))
            return;

        if (Dm.eq(winning_option, CHOICE_RESTART))
        {
            if (Dm.truthy(admins_present()))
            {
                to_chat(world, span_boldannounce(I18n.T("Notice: A restart vote will not restart the server automatically because there are active admins on.")));
                message_admins("A restart vote has passed, but there are active admins on with +SERVER, so it has been canceled. If you wish, you may restart the server.");
                return;
            }

            // If there was a previous map vote, we revert the change.
            // tg: restart_vote.dm:61-64  if(!isnull(SSmap_vote.next_map_config)) ... revert_next_map(): SSmap_vote is not ported (no map
            // rotation list, loadmaplist is deferred), so no next map is ever set and the branch never runs

            SSticker.force_ending = FORCE_END_ROUND;
            log_game("End round forced by successful restart vote.");
            return;
        }

        throw CRASH(Dm.text("[] wasn't passed a valid winning choice. (Got: [])", type, Dm.truthy(winning_option) ? winning_option : "null"));
    }
}
