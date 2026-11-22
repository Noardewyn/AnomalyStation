// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/hearted.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps R5, D327): the end-of-round commendations. When the emergency shuttle leaves, a few of the round's players are
// asked to thank someone anonymously; the commended get the hearted status (a heart before their key in OOC, for 24 hours of
// world.realtime) at the round's end. It also holds the [DmPath] of /datum/controller/subsystem/ticker, which hearted.dm declares first
// in .dme order (rules 2.3).

// tg: code/__HELPERS/hearted.dm:2
[DmPath("/datum/controller/subsystem/ticker")]
public partial class datum_controller_subsystem_ticker : datum_controller_subsystem
{
    /// Called when the shuttle starts launching back to centcom, polls a few random players who joined the round for commendations
    // tg: code/__HELPERS/hearted.dm:2
    public virtual void poll_hearts()
    {
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_number_commendation_percent_poll))))
            return;

        float number_to_ask = round(LAZYLEN(GLOB.joined_player_list) * Dm.num(config.Get(typeof(datum_config_entry_number_commendation_percent_poll)))) + Dm.num(rand(0, 1));
        if (number_to_ask == 0)
        {
            message_admins("Not enough eligible players to poll for commendations.");
            return;
        }
        message_admins(Dm.text("Polling [] players for commendations.", number_to_ask));

        // We need the list to be random, otherwise we'll end up always asking the same people to give out commendations.
        var eligible_player_list = shuffle(GLOB.joined_player_list) as DmList;

        foreach (object i in Dm.each(eligible_player_list))
        {
            var check_mob = get_mob_by_ckey(i) as mob;
            if (check_mob?.mind == null || check_mob.client == null)
                continue;
            // maybe some other filters like bans or whatever
            Waiting.Detach(check_mob.query_heart(1)); // INVOKE_ASYNC(check_mob, TYPE_PROC_REF(/mob, query_heart), 1)
            number_to_ask--;
            if (number_to_ask <= 0)
                break;
        }
    }

    /// Once the round is actually over, cycle through the ckeys in the hearts list and give them the hearted status
    // tg: code/__HELPERS/hearted.dm:26
    public virtual void handle_hearts()
    {
        DmList message = Dm.list("The following players were commended this round: ");
        float i = 0;
        foreach (object hearted_ckey in Dm.each(hearts))
        {
            i++;
            var hearted_mob = get_mob_by_ckey(hearted_ckey) as mob;
            if (hearted_mob?.client == null)
                continue;
            hearted_mob.client.adjust_heart();
            Dm.append(ref message, Dm.text("[][]", hearted_ckey, i == hearts.len ? "" : ", "));
        }
        message_admins(message.Join(""));
    }
}

public partial class mob : atom_movable
{
    /// Ask someone if they'd like to award a commendation for the round, 3 tries to get the name they want before we give up
    // tg: code/__HELPERS/hearted.dm:39
    public virtual async DmTask query_heart(float? attempt = null)
    {
        attempt ??= 1;
        if (client == null || attempt > 3)
            return;
        if (attempt == 1 && !Dm.eq(await tgui_alert(this, I18n.T("Was there another character you noticed being kind this round that you would like to anonymously thank?"), "<3?", Dm.list(I18n.T("Yes"), I18n.T("No")), timeout: 30 * SECONDS), I18n.T("Yes")))
            return;

        object heart_nominee = null;
        switch ((int)attempt)
        {
            case 1:
                heart_nominee = await tgui_input_text(this, I18n.T("What was their name? Just a first or last name may be enough."), "<3?", max_length: MAX_NAME_LEN);
                break;
            case 2:
                heart_nominee = await tgui_input_text(this, I18n.T("Try again, what was their name? Just a first or last name may be enough."), "<3?", max_length: MAX_NAME_LEN);
                break;
            case 3:
                heart_nominee = await tgui_input_text(this, I18n.T("One more try, what was their name? Just a first or last name may be enough."), "<3?", max_length: MAX_NAME_LEN);
                break;
        }

        if (!Dm.truthy(heart_nominee))
            return;

        heart_nominee = LOWER_TEXT(heart_nominee);
        var name_checks = get_mob_by_name(heart_nominee) as DmList;
        if (name_checks == null || name_checks.len == 0)
        {
            await query_heart(attempt + 1);
            return;
        }
        name_checks = shuffle(name_checks) as DmList;

        foreach (object i in Dm.each(name_checks))
        {
            var heart_contender = (mob)i;
            if (heart_contender == this)
                continue;

            object answer = await tgui_alert(this, I18n.T("Is this the person: []?", heart_contender.real_name), "<3?", Dm.list(I18n.T("Yes!"), I18n.T("Nope"), I18n.T("Cancel")), timeout: 15 * SECONDS);
            if (Dm.eq(answer, I18n.T("Yes!")))
            {
                heart_contender.receive_heart(this);
                return;
            }
            else if (Dm.eq(answer, I18n.T("Nope")))
                continue;
            else
                return;
        }

        await query_heart(attempt + 1);
    }

    /*
    * Once we've confirmed who we're commending, either set their status now or log it for the end of the round
    *
    * This used to be reversed, being named nominate_heart and being called on the mob sending the commendation and the first argument being
    * the heart_recepient, but that was confusing and unintuitive, so now src is the person being commended and the sender is now the first argument.
    *
    * Arguments:
    * * heart_sender: The reference to the mob who sent the commendation, just for the purposes of logging
    * * duration: How long from the moment it's applied the heart will last
    * * instant: If TRUE (or if the round is already over), we'll give them the heart status now, if FALSE, we wait until the end of the round (which is the standard behavior)
    */
    // tg: code/__HELPERS/hearted.dm:91
    public virtual void receive_heart(mob heart_sender = null, float? duration = null, float? instant = null)
    {
        duration ??= 24 * HOURS;
        instant ??= FALSE;
        if (client == null)
            return;
        to_chat(heart_sender, span_nicegreen(I18n.T("Commendation sent!")));
        message_admins(Dm.text("[] commended [] []", key_name(heart_sender), key_name(this), Dm.truthy(instant) ? "(instant)" : ""));
        log_admin_private(Dm.text("[] commended [] []", key_name(heart_sender), key_name(this), Dm.truthy(instant) ? "(instant)" : ""));
        if (Dm.truthy(instant) || SSticker.current_state == GAME_STATE_FINISHED)
            client.adjust_heart(duration);
        else
            LAZYADD(ref SSticker.hearts, ckey);
    }
}
