// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/verbs/adminhelp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice G6 (D282): tg's admin help, the tickets, their panels (browse() pages, DV216) and the player's Adminhelp window. Not
// translated: the stat panel (stat_entry and the statclick objects' Click: the port has no stat panel; the admins list the tickets
// with the console's "tickets"), the blackbox (SSblackbox.LogAhelp and record_feedback: the feedback database), TGS (send2adminchat,
// send2otherserver and the cross-server comms, tgsadminwho: no TGS bot; the Discord webhook is translated), window_flash (BYOND's
// taskbar flash) and check_asay_links (the admin say's links); get_mob_by_name came with gaps R5 (the commendations). BYOND's input() for the ticket's title is
// tgui_input_text (DV217).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/admin/verbs/adminhelp.dm:6  GLOBAL_DATUM_INIT(ahelp_tickets, /datum/admin_help_tickets, new)
    public datum_admin_help_tickets ahelp_tickets;

    // tg: code/modules/admin/verbs/adminhelp.dm:6
    public virtual void InitGlobalahelp_tickets()
    {
        ahelp_tickets = Dm.@new<datum_admin_help_tickets>();
        Dm.append(ref gvars_datum_init_order, "ahelp_tickets");
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:717  GLOBAL_DATUM_INIT(admin_help_ui_handler, /datum/admin_help_ui_handler, new)
    public datum_admin_help_ui_handler admin_help_ui_handler;

    // tg: code/modules/admin/verbs/adminhelp.dm:717
    public virtual void InitGlobaladmin_help_ui_handler()
    {
        admin_help_ui_handler = Dm.@new<datum_admin_help_ui_handler>();
        Dm.append(ref gvars_datum_init_order, "admin_help_ui_handler");
    }
}

/**
 * # Adminhelp Ticket Manager
 */
// tg: code/modules/admin/verbs/adminhelp.dm:11
public partial class datum_admin_help_tickets : datum
{
    // tg: code/modules/admin/verbs/adminhelp.dm:23
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(active_tickets);
        QDEL_LIST(closed_tickets);
        QDEL_LIST(resolved_tickets);
        QDEL_NULL(ref astatclick);
        QDEL_NULL(ref cstatclick);
        QDEL_NULL(ref rstatclick);
        return base.Destroy(force);
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:32
    public virtual datum_admin_help TicketByID(object id = null)
    {
        DmList lists = Dm.list(active_tickets, closed_tickets, resolved_tickets);
        foreach (DmList I in Dm.each<DmList>(lists))
        {
            foreach (datum_admin_help AH in Dm.each<datum_admin_help>(I))
            {
                if (Dm.eq(AH.id, id))
                    return AH;
            }
        }
        return null;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:40
    public virtual DmList TicketsByCKey(object ckey = null)
    {
        DmList dot = Dm.list();
        DmList lists = Dm.list(active_tickets, closed_tickets, resolved_tickets);
        foreach (DmList I in Dm.each<DmList>(lists))
        {
            foreach (datum_admin_help AH in Dm.each<datum_admin_help>(I))
            {
                if (Dm.eq(AH.initiator_ckey, ckey))
                    dot.Add(AH);
            }
        }
        return dot;
    }

    //private
    // tg: code/modules/admin/verbs/adminhelp.dm:50
    public virtual void ListInsert(datum_admin_help new_ticket = null)
    {
        DmList ticket_list;
        if (Dm.eq(new_ticket.state, AHELP_ACTIVE))
            ticket_list = active_tickets;
        else if (Dm.eq(new_ticket.state, AHELP_CLOSED))
            ticket_list = closed_tickets;
        else if (Dm.eq(new_ticket.state, AHELP_RESOLVED))
            ticket_list = resolved_tickets;
        else
            throw Dm.CRASH(Dm.text("Invalid ticket state: []", new_ticket.state), "code/modules/admin/verbs/adminhelp.dm", 61);
        int num_closed = (int)ticket_list.len;
        if (num_closed > 0)
        {
            for (int I = 1; I <= num_closed; I++)
            {
                var AH = ticket_list[I] as datum_admin_help;
                if (AH.id > new_ticket.id)
                {
                    ticket_list.Insert(I, new_ticket);
                    return;
                }
            }
        }
        ticket_list.Add(new_ticket);
    }

    //opens the ticket listings for one of the 3 states
    // tg: code/modules/admin/verbs/adminhelp.dm:71
    public virtual void BrowseTickets(object state = null)
    {
        DmList l2b = null;
        string title = null;
        if (Dm.eq(state, AHELP_ACTIVE))
        {
            l2b = active_tickets;
            title = "Active Tickets";
        }
        else if (Dm.eq(state, AHELP_CLOSED))
        {
            l2b = closed_tickets;
            title = "Closed Tickets";
        }
        else if (Dm.eq(state, AHELP_RESOLVED))
        {
            l2b = resolved_tickets;
            title = "Resolved Tickets";
        }
        if (l2b == null)
            return;
        DmList dat = Dm.list(Dm.text("<html><head><meta http-equiv='Content-Type' content='text/html; charset=UTF-8'><title>[]</title></head>", title));
        dat.Add(Dm.text("<A href='byond://?_src_=holder;[];ahelp_tickets=[]'>Refresh</A><br><br>", HrefToken(), state));
        foreach (datum_admin_help AH in Dm.each<datum_admin_help>(l2b))
            dat.Add(Dm.text("[]<br>", span_adminnotice(Dm.text("[]: <A href='byond://?_src_=holder;[];ahelp=[];ahelp_action=ticket'>[]: []</A>", span_adminhelp(Dm.text("Ticket #[]", AH.id)), HrefToken(), REF(AH), AH.initiator_key_name, AH.name))));

        ClientSeam.Browse(usr, dat.Join(""), Dm.text("window=ahelp_list[];size=600x480", state)); // usr << browse(...)
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:95  stat_entry: the stat panel (not translated)

    //Reassociate still open ticket if one exists
    // tg: code/modules/admin/verbs/adminhelp.dm:116
    public virtual void ClientLogin(client C = null)
    {
        C.current_ticket = CKey2ActiveTicket(C.ckey);
        if (C.current_ticket != null)
        {
            C.current_ticket.initiator = C;
            C.current_ticket.AddInteraction("Client reconnected.");
            // SSblackbox.LogAhelp(C.current_ticket.id, "Reconnected", "Client reconnected", C.ckey): the blackbox (not translated)
        }
    }

    //Dissasociate ticket
    // tg: code/modules/admin/verbs/adminhelp.dm:124
    public virtual void ClientLogout(client C = null)
    {
        if (C.current_ticket != null)
        {
            datum_admin_help T = C.current_ticket;
            T.AddInteraction("Client disconnected.");
            // INVOKE_ASYNC(SSblackbox, LogAhelp, T.id, "Disconnected", ...): the blackbox (not translated)
            T.initiator = null;
        }
    }

    //Get a ticket given a ckey
    // tg: code/modules/admin/verbs/adminhelp.dm:133
    public virtual datum_admin_help CKey2ActiveTicket(object ckey = null)
    {
        foreach (datum_admin_help AH in Dm.each<datum_admin_help>(active_tickets))
        {
            if (Dm.eq(AH.initiator_ckey, ckey))
                return AH;
        }
        return null;
    }
}

/**
 * # Adminhelp Ticket
 */
// tg: code/modules/admin/verbs/adminhelp.dm:169
public partial class datum_admin_help : datum
{
    // GAP(types): the generated var block types id as a typepath; it is the ticket's number.
    /// Unique ID of the ticket
    // tg: code/modules/admin/verbs/adminhelp.dm:171
    public float id;

    /// Static counter used for generating each ticket ID
    // tg: code/modules/admin/verbs/adminhelp.dm:193  var/static/ticket_counter = 0 (rules 10.6)
    public static ref float ticket_counter => ref TgState.Current.datum_admin_help__ticket_counter;

    // tg: code/modules/admin/verbs/adminhelp.dm:164-166
    public const int WEBHOOK_NONE = 0;
    public const int WEBHOOK_URGENT = 1;
    public const int WEBHOOK_NON_URGENT = 2;

    /**
     * Call this on its own to create a ticket, don't manually assign current_ticket
     *
     * Arguments:
     * * msg_raw - The first message of this admin_help: used for the initial title of the ticket
     * * is_bwoink - Boolean operator, TRUE if this ticket was started by an admin PM
     */
    // tg: code/modules/admin/verbs/adminhelp.dm:212
    public override async DmTask New(params object[] __extra)
    {
        object msg_raw = __extra.Length > 0 ? __extra[0] : null;
        var C = __extra.Length > 1 ? __extra[1] as client : null;
        object is_bwoink = __extra.Length > 2 ? __extra[2] : null;
        object urgent = __extra.Length > 3 ? __extra[3] : FALSE;
        //clean the input msg
        object msg = sanitize(copytext_char(msg_raw, 1, MAX_MESSAGE_LEN));
        if (!Dm.truthy(msg) || C == null || C.mob == null)
        {
            qdel(this);
            return;
        }

        id = ++ticket_counter;
        opened_at = world.time;

        name = copytext_char(msg, 1, 100);

        initiator = C;
        initiator_ckey = initiator.ckey;
        initiator_key_name = key_name(initiator, FALSE, TRUE);
        if (initiator.current_ticket != null) //This is a bug
        {
            stack_trace("Multiple ahelp current_tickets", "code/modules/admin/verbs/adminhelp.dm", 229);
            initiator.current_ticket.AddInteraction("Ticket erroneously left open by code");
            initiator.current_ticket.Close();
        }
        initiator.current_ticket = this;

        TimeoutVerb();

        // statclick = new(null, src): the stat panel (not translated)
        ticket_interactions = Dm.list();
        player_interactions = Dm.list();

        if (Dm.truthy(is_bwoink))
        {
            AddInteraction(Dm.text("<font color='blue'>[] PM'd []</font>", key_name_admin(usr), LinkedReplyName()), player_message: Dm.text("<font color='blue'>[] PM'd []</font>", key_name_admin(usr, include_name: FALSE), LinkedReplyName()));
            message_admins(Dm.text("<font color='blue'>Ticket [] created</font>", TicketHref(Dm.text("#[]", id))));
        }
        else
        {
            MessageNoRecipient(msg_raw, urgent);
            send_message_to_tgs(msg, urgent);
        }
        Dm.append(ref GLOB.ahelp_tickets.active_tickets, this);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:247
    public virtual datum_discord_embed format_embed_discord(object message = null)
    {
        var embed = Dm.@new<datum_discord_embed>();
        embed.title = Dm.text("Ticket #[]", id);
        // embed.description = "<byond://[world.internet_address]:[world.port]>": BYOND's join link (the port's server address is the
        // host's, not the world's)
        embed.author = key_name(initiator_ckey);
        string round_state = null;
        float current_state = Dm.num(SSticker.current_state);
        if (current_state == GAME_STATE_STARTUP || current_state == GAME_STATE_PREGAME || current_state == GAME_STATE_SETTING_UP)
            round_state = "Round has not started";
        else if (current_state == GAME_STATE_PLAYING)
        {
            round_state = "Round is ongoing.";
            // SSshuttle.emergency.getModeStr() and getTimerStr(): the emergency shuttle's call line, when there is a shuttle
            if (SSshuttle?.emergency != null && Dm.truthy(SSshuttle.emergency.getModeStr()))
            {
                round_state += Dm.text("\n[]: []", SSshuttle.emergency.getModeStr(), SSshuttle.emergency.getTimerStr());
                if (Dm.truthy(SSticker.emergency_reason))
                    round_state += Dm.text(", Shuttle call reason: []", SSticker.emergency_reason);
            }
        }
        else if (current_state == GAME_STATE_FINISHED)
            round_state = "Round has ended";
        DmList admin_counts = get_admin_counts(R_BAN);
        string stealth_admins = Dm.str(Dm.jointext(admin_counts["stealth"] as DmList, ", "));
        string afk_admins = Dm.str(Dm.jointext(admin_counts["afk"] as DmList, ", "));
        string other_admins = Dm.str(Dm.jointext(admin_counts["noflags"] as DmList, ", "));
        string admin_text = "";
        string player_count = Dm.text("**Total**: [], **Living**: [], **Dead**: [], **Observers**: []", Dm.length(GLOB.clients), Dm.length(GLOB.alive_player_list), Dm.length(GLOB.dead_player_list), Dm.length(GLOB.current_observers_list));
        if (stealth_admins.Length > 0)
            admin_text += Dm.text("**Stealthed**: []\n", stealth_admins);
        if (afk_admins.Length > 0)
            admin_text += Dm.text("**AFK**: []\n", afk_admins);
        if (other_admins.Length > 0)
            admin_text += Dm.text("**Lacks +BAN**: []\n", other_admins);
        embed.fields = Dm.list(
            Dm.kv("CKEY", initiator_ckey),
            Dm.kv("PLAYERS", player_count),
            Dm.kv("ROUND STATE", round_state),
            Dm.kv("ROUND ID", GLOB.round_id),
            Dm.kv("ROUND TIME", round_timestamp()),
            Dm.kv("MESSAGE", message),
            Dm.kv("ADMINS", admin_text));
        object ahelp_link_config = config.Get(typeof(datum_config_entry_string_adminhelp_ahelp_link)); // CONFIG_GET(string/adminhelp_ahelp_link)
        if (Dm.truthy(ahelp_link_config))
        {
            object ahelp_link = replacetext(ahelp_link_config, "$RID", GLOB.round_id);
            ahelp_link = replacetext(ahelp_link, "$TID", id);
            embed.url = ahelp_link;
        }
        return embed;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:291
    public virtual void send_message_to_tgs(object message = null, object urgent = null)
    {
        object message_to_send = message;

        if (Dm.truthy(urgent))
        {
            object extra_message = config.Get(typeof(datum_config_entry_string_urgent_ahelp_message)); // CONFIG_GET(string/urgent_ahelp_message)
            to_chat(initiator, span_boldwarning(I18n.T("Notified admins to prioritize your ticket")));
            datum_discord_embed embed = format_embed_discord(message);
            embed.content = extra_message;
            embed.footer = "This player requested an admin";
            send2adminchat_webhook(embed, urgent: TRUE);
            webhook_sent = WEBHOOK_URGENT;
        }
        //send it to TGS if nobody is on and tell us how many were on
        float admin_number_present = send2tgs_adminless_only(initiator_ckey, Dm.text("Ticket #[]: []", id, message_to_send));
        log_admin_private(Dm.text("Ticket #[]: []: [] - heard by [] non-AFK admins who have +BAN.", id, key_name(initiator), name, admin_number_present));
        if (admin_number_present <= 0)
        {
            to_chat(initiator, span_notice(I18n.T("No active admins are online, your adminhelp was sent to admins who are available through IRC or Discord.")), confidential: TRUE);
            heard_by_no_admins = TRUE;
            object regular_webhook_url = config.Get(typeof(datum_config_entry_string_regular_adminhelp_webhook_url)); // CONFIG_GET(string/regular_adminhelp_webhook_url)
            if (Dm.truthy(regular_webhook_url) && (!Dm.truthy(urgent) || !Dm.eq(regular_webhook_url, config.Get(typeof(datum_config_entry_string_urgent_adminhelp_webhook_url)))))
            {
                object extra_message = config.Get(typeof(datum_config_entry_string_ahelp_message)); // CONFIG_GET(string/ahelp_message)
                datum_discord_embed embed = format_embed_discord(message);
                embed.content = extra_message;
                embed.footer = Dm.text("This player sent an ahelp when no admins are available []", Dm.truthy(urgent) ? "and also requested an admin" : "");
                send2adminchat_webhook(embed, urgent: FALSE);
                webhook_sent = WEBHOOK_NON_URGENT;
            }
        }
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:346
    public override object Destroy(float? force = null)
    {
        RemoveActive();
        Dm.remove(ref GLOB.ahelp_tickets.closed_tickets, this);
        Dm.remove(ref GLOB.ahelp_tickets.resolved_tickets, this);
        return base.Destroy(force);
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:352
    public virtual void AddInteraction(object formatted_message = null, object player_message = null)
    {
        if (usr != null && !Dm.eq(usr.ckey, initiator_ckey))
        {
            Dm.combine(ref admins_involved, usr.ckey);
            if (Dm.truthy(heard_by_no_admins))
            {
                heard_by_no_admins = FALSE;
                // send2adminchat(initiator_ckey, "Ticket #[id]: Answered by [key_name(usr)]"): TGS (not translated)
            }
        }

        ticket_interactions.Add(Dm.text("[]: []", server_timestamp(), formatted_message));
        if (player_message != null)
            player_interactions.Add(Dm.text("[]: []", server_timestamp(), player_message));
    }

    //Removes the ahelp verb and returns it after 2 minutes
    // tg: code/modules/admin/verbs/adminhelp.dm:364
    public virtual void TimeoutVerb()
    {
        remove_verb(initiator, client.ADMINHELP_VERB);
        initiator.adminhelptimerid = _addtimer(Dm.@new<datum_callback>(initiator, nameof(client.giveadminhelpverb)), 1200, TIMER_STOPPABLE, file: "code/modules/admin/verbs/adminhelp.dm", line: 366); //2 minute cooldown of admin helps
    }

    //private
    // tg: code/modules/admin/verbs/adminhelp.dm:369
    public virtual string FullMonty(object ref_src = null)
    {
        if (!Dm.truthy(ref_src))
            ref_src = Dm.text("[]", REF(this));
        // ADMIN_FULLMONTY_NONAME(initiator.mob): the admin panels' links (player panel, view variables, subtle message, follow, smite;
        // not translated): the console's commands take the key
        string dot = "";
        dot += Dm.text(" (<A href='byond://?_src_=holder;[];showmessageckey=[]'>NOTES</A>)", HrefToken(), initiator.ckey);
        if (Dm.eq(state, AHELP_ACTIVE))
        {
            // CONFIG_GET(flag/popup_admin_pm)'s POPUP link: BYOND's alert popup (not translated)
            dot += ClosureLinks(ref_src);
        }
        return dot;
    }

    //private
    // tg: code/modules/admin/verbs/adminhelp.dm:380
    public virtual string ClosureLinks(object ref_src = null)
    {
        if (!Dm.truthy(ref_src))
            ref_src = Dm.text("[]", REF(this));
        string dot = Dm.text(" (<A href='byond://?_src_=holder;[];ahelp=[];ahelp_action=reject'>REJT</A>)", HrefToken(forceGlobal: TRUE), ref_src);
        dot += Dm.text(" (<A href='byond://?_src_=holder;[];ahelp=[];ahelp_action=icissue'>IC</A>)", HrefToken(forceGlobal: TRUE), ref_src);
        dot += Dm.text(" (<A href='byond://?_src_=holder;[];ahelp=[];ahelp_action=close'>CLOSE</A>)", HrefToken(forceGlobal: TRUE), ref_src);
        dot += Dm.text(" (<A href='byond://?_src_=holder;[];ahelp=[];ahelp_action=resolve'>RSLVE</A>)", HrefToken(forceGlobal: TRUE), ref_src);
        return dot;
    }

    //private
    // tg: code/modules/admin/verbs/adminhelp.dm:389
    public virtual string LinkedReplyName(object ref_src = null)
    {
        if (!Dm.truthy(ref_src))
            ref_src = Dm.text("[]", REF(this));
        return Dm.text("<A href='byond://?_src_=holder;[];ahelp=[];ahelp_action=reply'>[]</A>", HrefToken(forceGlobal: TRUE), ref_src, initiator_key_name);
    }

    //private
    // tg: code/modules/admin/verbs/adminhelp.dm:395
    public virtual string TicketHref(object msg = null, object ref_src = null, object action = null)
    {
        action ??= "ticket";
        if (!Dm.truthy(ref_src))
            ref_src = Dm.text("[]", REF(this));
        return Dm.text("<A href='byond://?_src_=holder;[];ahelp=[];ahelp_action=[]'>[]</A>", HrefToken(forceGlobal: TRUE), ref_src, action, msg);
    }

    //message from the initiator without a target, all admins will see this
    //won't bug irc/discord
    // tg: code/modules/admin/verbs/adminhelp.dm:402
    public virtual void MessageNoRecipient(object msg = null, object urgent = null)
    {
        msg = sanitize(copytext_char(msg, 1, MAX_MESSAGE_LEN));
        string ref_src = Dm.text("[]", REF(this));
        //Message to be sent to all admins
        string admin_msg = fieldset_block(
            span_adminhelp(Dm.text("Ticket []", TicketHref(Dm.text("#[]", id), ref_src))),
            Dm.text("<b>[]</b>\n\n[]\n\n<b class='smaller'>[]</b>", LinkedReplyName(ref_src), span_linkify(Dm.str(keywords_lookup(msg))), FullMonty(ref_src)),
            "boxed_message red_box");

        AddInteraction(Dm.text("<font color='red'>[]: []</font>", LinkedReplyName(ref_src), msg), player_message: Dm.text("<font color='red'>[]: []</font>", LinkedReplyName(ref_src), msg));
        log_admin_private(Dm.text("Ticket #[]: []: []", id, key_name(initiator), msg));

        //send this msg to all admins
        foreach (client X in Dm.each<client>(GLOB.admins))
        {
            if (Dm.truthy(Dm.bitand(X.prefs.toggles, SOUND_ADMINHELP)))
                Dm.output_to(X, sound(Dm.resource("sound/effects/adminhelp.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            // window_flash(X, ignorepref = TRUE): BYOND's taskbar flash (not translated)
            to_chat(X, type: MESSAGE_TYPE_ADMINPM, html: admin_msg, confidential: TRUE);
        }

        //show it to the person adminhelping too
        reply_to_admins_notification(msg);
        // SSblackbox.LogAhelp(id, "Ticket Opened", msg, null, initiator.ckey, urgent = urgent): the blackbox (not translated)
    }

    /// Sends a message to the player that they are replying to admins.
    // tg: code/modules/admin/verbs/adminhelp.dm:431
    public virtual void reply_to_admins_notification(object message = null)
    {
        to_chat(
            initiator,
            type: MESSAGE_TYPE_ADMINPM,
            html: span_notice(Dm.text("PM to-<b>Admins</b>: []", span_linkify(Dm.str(message)))),
            confidential: TRUE);

        player_replied = TRUE;
    }

    //Reopen a closed ticket
    // tg: code/modules/admin/verbs/adminhelp.dm:442
    public virtual void Reopen()
    {
        if (Dm.eq(state, AHELP_ACTIVE))
        {
            to_chat(usr, span_warning(I18n.T("This ticket is already open.")), confidential: TRUE);
            return;
        }

        if (GLOB.ahelp_tickets.CKey2ActiveTicket(initiator_ckey) != null)
        {
            to_chat(usr, span_warning(I18n.T("This user already has an active ticket, cannot reopen this one.")), confidential: TRUE);
            return;
        }

        // statclick = new(null, src): the stat panel (not translated)
        Dm.append(ref GLOB.ahelp_tickets.active_tickets, this);
        Dm.remove(ref GLOB.ahelp_tickets.closed_tickets, this);
        Dm.remove(ref GLOB.ahelp_tickets.resolved_tickets, this);
        // the closed and resolved tallies' SSblackbox.record_feedback: the blackbox (not translated)
        state = AHELP_ACTIVE;
        closed_at = null;
        if (initiator != null)
            initiator.current_ticket = this;

        AddInteraction(Dm.text("<font color='purple'>Reopened by []</font>", key_name_admin(usr)), player_message: "Ticket reopened!");
        string msg = span_adminhelp(Dm.text("Ticket [] reopened by [].", TicketHref(Dm.text("#[]", id)), key_name_admin(usr)));
        message_admins(msg);
        log_admin_private(msg);
        // SSblackbox.LogAhelp and record_feedback: the blackbox (not translated)
        TicketPanel(); //can only be done from here, so refresh it
    }

    //private
    // tg: code/modules/admin/verbs/adminhelp.dm:474
    public virtual void RemoveActive()
    {
        if (!Dm.eq(state, AHELP_ACTIVE))
            return;
        closed_at = world.time;
        QDEL_NULL(ref statclick);
        Dm.remove(ref GLOB.ahelp_tickets.active_tickets, this);
        if (initiator != null && initiator.current_ticket == this)
            initiator.current_ticket = null;

        if (Dm.truthy(_listen_lookup?[COMSIG_ADMIN_HELP_MADE_INACTIVE]))
            _SendSignal(COMSIG_ADMIN_HELP_MADE_INACTIVE, Dm.list(this)); // SEND_SIGNAL
    }

    //Mark open ticket as closed/meme
    // tg: code/modules/admin/verbs/adminhelp.dm:486
    public virtual void Close(object key_name = null, object silent = null)
    {
        key_name ??= key_name_admin(usr);
        if (!Dm.eq(state, AHELP_ACTIVE))
            return;
        RemoveActive();
        state = AHELP_CLOSED;
        GLOB.ahelp_tickets.ListInsert(this);
        AddInteraction(Dm.text("<font color='red'>Closed by [].</font>", key_name), player_message: "<font color='red'>Ticket closed!</font>");
        if (!Dm.truthy(silent))
        {
            // SSblackbox.record_feedback: the blackbox (not translated)
            string msg = Dm.text("Ticket [] closed by [].", TicketHref(Dm.text("#[]", id)), key_name);
            message_admins(msg);
            log_admin_private(msg);
        }
    }

    //Mark open ticket as resolved/legitimate, returns ahelp verb
    // tg: code/modules/admin/verbs/adminhelp.dm:501
    public virtual void Resolve(object key_name = null, object silent = null)
    {
        key_name ??= key_name_admin(usr);
        if (!Dm.eq(state, AHELP_ACTIVE))
            return;
        RemoveActive();
        state = AHELP_RESOLVED;
        GLOB.ahelp_tickets.ListInsert(this);

        if (initiator != null)
            _addtimer(Dm.@new<datum_callback>(initiator, nameof(client.giveadminhelpverb)), 5 * SECONDS, file: "code/modules/admin/verbs/adminhelp.dm", line: 508);

        AddInteraction(Dm.text("<font color='green'>Resolved by [].</font>", key_name), player_message: "<font color='green'>Ticket resolved!</font>");
        to_chat(initiator, span_adminhelp(I18n.T("Your ticket has been resolved by an admin. The Adminhelp verb will be returned to you shortly.")), confidential: TRUE);
        if (!Dm.truthy(silent))
        {
            // SSblackbox.record_feedback: the blackbox (not translated)
            string msg = Dm.text("Ticket [] resolved by []", TicketHref(Dm.text("#[]", id)), key_name);
            message_admins(msg);
            log_admin_private(msg);
        }
    }

    //Close and return ahelp verb, use if ticket is incoherent
    // tg: code/modules/admin/verbs/adminhelp.dm:520
    public virtual void Reject(object key_name = null)
    {
        key_name ??= key_name_admin(usr);
        if (!Dm.eq(state, AHELP_ACTIVE))
            return;

        if (initiator != null)
        {
            initiator.giveadminhelpverb();

            Dm.output_to(initiator, sound(Dm.resource("sound/effects/adminhelp.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)

            to_chat(initiator, I18n.T("<font color='red' size='4'><b>- AdminHelp Rejected! -</b></font>"), confidential: TRUE);
            to_chat(initiator, I18n.T("<font color='red'><b>Your admin help was rejected.</b> The adminhelp verb has been returned to you so that you may try again.</font>"), confidential: TRUE);
            to_chat(initiator, I18n.T("Please try to be calm, clear, and descriptive in admin helps, do not assume the admin has seen any related events, and clearly state the names of anybody you are reporting."), confidential: TRUE);
        }

        // SSblackbox.record_feedback: the blackbox (not translated)
        string msg = Dm.text("Ticket [] rejected by []", TicketHref(Dm.text("#[]", id)), key_name);
        message_admins(msg);
        log_admin_private(msg);
        AddInteraction(Dm.text("Rejected by [].", key_name), player_message: "Ticket rejected!");
        // SSblackbox.LogAhelp: the blackbox (not translated)
        Close(silent: TRUE);
    }

    //Resolve ticket with IC Issue message
    // tg: code/modules/admin/verbs/adminhelp.dm:542
    public virtual void ICIssue(object key_name = null)
    {
        key_name ??= key_name_admin(usr);
        if (!Dm.eq(state, AHELP_ACTIVE))
            return;

        string msg = I18n.T("<font color='red' size='4'><b>- AdminHelp marked as IC issue! -</b></font><br>");
        msg += I18n.T("<font color='red'>Your issue has been determined by an administrator to be an in character issue and does NOT require administrator intervention at this time. For further resolution you should pursue options that are in character.</font>");

        if (initiator != null)
            to_chat(initiator, msg, confidential: TRUE);

        // SSblackbox.record_feedback: the blackbox (not translated)
        msg = Dm.text("Ticket [] marked as IC by []", TicketHref(Dm.text("#[]", id)), key_name);
        message_admins(msg);
        log_admin_private(msg);
        AddInteraction(Dm.text("Marked as IC issue by []", key_name), player_message: "Marked as IC issue!");
        // SSblackbox.LogAhelp: the blackbox (not translated)
        Resolve(silent: TRUE);
    }

    //Show the ticket panel
    // tg: code/modules/admin/verbs/adminhelp.dm:561
    public virtual void TicketPanel()
    {
        DmList dat = Dm.list(Dm.text("<html><head><meta http-equiv='Content-Type' content='text/html; charset=UTF-8'><title>Ticket #[]</title></head>", id));
        string ref_src = Dm.text("[]", REF(this));
        dat.Add(Dm.text("<h4>Admin Help Ticket #[]: []</h4>", id, LinkedReplyName(ref_src)));
        dat.Add(Dm.text("<b>State: []</b>", ticket_status()));
        dat.Add(Dm.text("[][][][]", FOURSPACES, TicketHref("Refresh", ref_src), FOURSPACES, TicketHref("Re-Title", ref_src, "retitle")));
        if (!Dm.eq(state, AHELP_ACTIVE))
            dat.Add(Dm.text("[][]", FOURSPACES, TicketHref("Reopen", ref_src, "reopen")));
        dat.Add(Dm.text("<br><br>Opened at: [] (Approx [] ago)", round_timestamp(wtime: Dm.num(opened_at)), DisplayTimeText(world.time - Dm.num(opened_at))));
        if (Dm.truthy(closed_at))
            dat.Add(Dm.text("<br>Closed at: [] (Approx [] ago)", round_timestamp(wtime: Dm.num(closed_at)), DisplayTimeText(world.time - Dm.num(closed_at))));
        dat.Add("<br><br>");
        if (initiator != null)
            dat.Add(Dm.text("<b>Actions:</b> []<br>", FullMonty(ref_src)));
        else
            dat.Add(Dm.text("<b>DISCONNECTED</b>[][]<br>", FOURSPACES, ClosureLinks(ref_src)));
        dat.Add("<br><b>Log:</b><br><br>");
        foreach (object I in Dm.each(ticket_interactions))
            dat.Add(Dm.text("[]<br>", I));

        // Helper for opening directly to player ticket history
        // the Player Ticket History link (player_ticket_history: the ticket history database, not translated)

        // Append any tickets also opened by this user if relevant
        DmList related_tickets = GLOB.ahelp_tickets.TicketsByCKey(initiator_ckey);
        if (related_tickets.len > 1)
        {
            dat.Add("<br/><b>Other Tickets by User</b><br/>");
            foreach (datum_admin_help related_ticket in Dm.each<datum_admin_help>(related_tickets))
            {
                if (related_ticket.id == id)
                    continue;
                dat.Add(Dm.text("[] ([]): []<br/>", related_ticket.TicketHref(Dm.text("#[]", related_ticket.id)), related_ticket.ticket_status(), related_ticket.name));
            }
        }
        dat.Add("</html>");

        ClientSeam.Browse(usr, dat.Join(""), Dm.text("window=ahelp[];size=750x480", id)); // usr << browse(...)
    }

    /**
     * Renders the current status of the ticket into a displayable string
     */
    // tg: code/modules/admin/verbs/adminhelp.dm:600
    public virtual string ticket_status()
    {
        if (Dm.eq(state, AHELP_ACTIVE))
            return "<font color='red'>OPEN</font>";
        if (Dm.eq(state, AHELP_RESOLVED))
            return "<font color='green'>RESOLVED</font>";
        if (Dm.eq(state, AHELP_CLOSED))
            return "CLOSED";
        stack_trace(Dm.text("Invalid ticket state: []", state), "code/modules/admin/verbs/adminhelp.dm", 609);
        return "INVALID, CALL A CODER";
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:612
    public virtual async DmTask Retitle()
    {
        // DEVIATION(DV217): input(usr, ..., name) as text|null is tgui's text prompt
        object new_title = await tgui_input_text(usr, "Enter a title for the ticket", "Rename Ticket", name);
        if (Dm.truthy(new_title))
        {
            name = new_title;
            //not saying the original name cause it could be a long ass message
            string msg = Dm.text("Ticket [] titled [] by []", TicketHref(Dm.text("#[]", id)), name, key_name_admin(usr));
            message_admins(msg);
            log_admin_private(msg);
        }
        TicketPanel(); //we have to be here to do this
    }

    //Forwarded action from admin/Topic
    // tg: code/modules/admin/verbs/adminhelp.dm:623
    public virtual async DmTask Action(object action = null)
    {
        // testing("Ahelp action: [action]"): a TESTING build's log (not translated)
        if (webhook_sent != WEBHOOK_NONE)
        {
            var embed = Dm.@new<datum_discord_embed>();
            embed.title = Dm.text("Ticket #[]", id);
            object ahelp_link_config = config.Get(typeof(datum_config_entry_string_adminhelp_ahelp_link)); // CONFIG_GET(string/adminhelp_ahelp_link)
            if (Dm.truthy(ahelp_link_config))
            {
                object ahelp_link = replacetext(ahelp_link_config, "$RID", GLOB.round_id);
                ahelp_link = replacetext(ahelp_link, "$TID", id);
                embed.url = ahelp_link;
            }
            embed.description = Dm.text("[] has sent an action to this ticket. Action ID: []", key_name(usr), action);
            if (webhook_sent == WEBHOOK_URGENT)
                send2adminchat_webhook(embed, urgent: TRUE);
            if (webhook_sent == WEBHOOK_NON_URGENT || !Dm.eq(config.Get(typeof(datum_config_entry_string_regular_adminhelp_webhook_url)), config.Get(typeof(datum_config_entry_string_urgent_adminhelp_webhook_url))))
                send2adminchat_webhook(embed, urgent: FALSE);
            webhook_sent = WEBHOOK_NONE;
        }
        switch (Dm.str(action))
        {
            case "ticket":
                TicketPanel();
                break;
            case "retitle":
                await Retitle();
                break;
            case "reject":
                Reject();
                break;
            case "reply":
                await usr.client.cmd_ahelp_reply(initiator);
                break;
            case "icissue":
                ICIssue();
                break;
            case "close":
                Close();
                break;
            case "resolve":
                Resolve();
                break;
            case "reopen":
                Reopen();
                break;
        }
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:656
    public virtual void player_ticket_panel()
    {
        DmList dat = Dm.list("<html><head><meta http-equiv='Content-Type' content='text/html; charset=UTF-8'><title>Player Ticket</title></head>");
        dat.Add("<b>State: ");
        if (Dm.eq(state, AHELP_ACTIVE))
            dat.Add("<font color='red'>OPEN</font></b>");
        else if (Dm.eq(state, AHELP_RESOLVED))
            dat.Add("<font color='green'>RESOLVED</font></b>");
        else if (Dm.eq(state, AHELP_CLOSED))
            dat.Add("CLOSED</b>");
        else
            dat.Add("UNKNOWN</b>");
        dat.Add(Dm.text("\n[]<A href='byond://?_src_=usr;player_ticket_panel=1'>Refresh</A>", FOURSPACES));
        dat.Add(Dm.text("<br><br>Opened at: [] (Approx [] ago)", round_timestamp("hh:mm:ss", Dm.num(opened_at)), DisplayTimeText(world.time - Dm.num(opened_at))));
        if (Dm.truthy(closed_at))
            dat.Add(Dm.text("<br>Closed at: [] (Approx [] ago)", round_timestamp("hh:mm:ss", Dm.num(closed_at)), DisplayTimeText(world.time - Dm.num(closed_at))));
        dat.Add("<br><br>");
        dat.Add("<br><b>Log:</b><br><br>");
        foreach (object interaction in Dm.each(player_interactions))
            dat.Add(Dm.text("[]<br>", interaction));

        var player_panel = Dm.@new<datum_browser>(usr, Dm.text("ahelp[]", id), 0, 620, 480);
        player_panel.set_content(dat.Join(""));
        player_panel.open();
    }
}

public partial class client : datum
{
    // GAP(types): the generated var block types it as a number (its initial 0); it holds the cooldown's timer id (TIMER_STOPPABLE).
    /// Client var used for returning the ahelp verb
    // tg: code/modules/admin/verbs/adminhelp.dm:2
    public object adminhelptimerid = 0;

    /// M10x slice G6: tg's <c>/client/verb/adminhelp</c> in <see cref="verbs"/> (tg names a verb by its proc path).
    public const string ADMINHELP_VERB = "/client/verb/adminhelp";

    // tg: code/modules/admin/verbs/adminhelp.dm:712
    public virtual void giveadminhelpverb()
    {
        add_verb(this, ADMINHELP_VERB);
        deltimer(adminhelptimerid);
        adminhelptimerid = 0;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:795  GAME_VERB_HIDDEN(/client, no_tgui_adminhelp, "NoTguiAdminhelp"): the body
    public virtual async DmTask __gvb_no_tgui_adminhelp(DmList structured_args = null)
    {
        object message = structured_args["message"];
        if (Dm.truthy(adminhelptimerid))
            return;

        message = trim(message);

        GLOB.admin_help_ui_handler.perform_adminhelp(this, message, FALSE);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:795  the verb proc
    public virtual void no_tgui_adminhelp(object message = null, params object[] __extra)
    {
        Waiting.Detach(__gvb_no_tgui_adminhelp(Dm.list(Dm.kv("message", message))));
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:804  GAME_VERB(/client, adminhelp, "Adminhelp", "Admin"): the body
    public virtual async DmTask __gvb_adminhelp(DmList structured_args = null)
    {
        await GLOB.admin_help_ui_handler.ui_interact(mob);
        to_chat(this, span_boldnotice(Dm.text(I18n.T("Adminhelp failing to open or work? <a href='byond://?src=[];tguiless_adminhelp=1'>Click here</a>"), REF(this))));
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:804  the verb proc
    public virtual void adminhelp(params object[] __extra)
    {
        Waiting.Detach(__gvb_adminhelp(Dm.list()));
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:808  GAME_VERB(/client, view_latest_ticket, "View Latest Ticket", "Admin"): the body
    public virtual async DmTask __gvb_view_latest_ticket(DmList structured_args = null)
    {
        if (current_ticket == null)
        {
            // Check if the client had previous tickets, and show the latest one
            DmList prev_tickets = Dm.list();
            datum_admin_help last_ticket = null;
            // Check all resolved tickets for this player
            foreach (datum_admin_help resolved_ticket in Dm.each<datum_admin_help>(GLOB.ahelp_tickets.resolved_tickets))
            {
                if (Dm.eq(resolved_ticket.initiator_ckey, ckey)) // Initiator is a misnomer, it's always the non-admin player even if an admin bwoinks first
                    prev_tickets.Add(resolved_ticket);
            }
            // Check all closed tickets for this player
            foreach (datum_admin_help closed_ticket in Dm.each<datum_admin_help>(GLOB.ahelp_tickets.closed_tickets))
            {
                if (Dm.eq(closed_ticket.initiator_ckey, ckey))
                    prev_tickets.Add(closed_ticket);
            }
            // Take the most recent entry of prev_tickets and open the panel on it
            if (Dm.truthy(LAZYLEN(prev_tickets)))
            {
                last_ticket = pop(prev_tickets) as datum_admin_help;
                last_ticket.player_ticket_panel();
                return;
            }

            // client had no tickets this round
            to_chat(this, span_warning(I18n.T("You have not had an ahelp ticket this round.")));
            return;
        }

        current_ticket.player_ticket_panel();
        await DmTask.CompletedTask;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:808  the verb proc
    public virtual void view_latest_ticket(params object[] __extra)
    {
        Waiting.Detach(__gvb_view_latest_ticket(Dm.list()));
    }
}

// tg: code/modules/admin/verbs/adminhelp.dm:719
public partial class datum_admin_help_ui_handler : datum
{
    // tg: code/modules/admin/verbs/adminhelp.dm:722
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:725
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        DmList admins = get_admin_counts(R_BAN);
        dot["adminCount"] = Dm.length(admins["present"]);
        return dot;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:730
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["bannedFromUrgentAhelp"] = is_banned_from(user.ckey, "Urgent Adminhelp");
        dot["urgentAhelpPromptMessage"] = config.Get(typeof(datum_config_entry_string_urgent_ahelp_user_prompt)); // CONFIG_GET(string/urgent_ahelp_user_prompt)
        object webhook_url = config.Get(typeof(datum_config_entry_string_urgent_adminhelp_webhook_url)); // CONFIG_GET(string/urgent_adminhelp_webhook_url)
        if (Dm.truthy(webhook_url))
            dot["urgentAhelpEnabled"] = TRUE;
        return dot;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:738
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Adminhelp");
            await ui.open();
            ui.set_autoupdate(FALSE);
        }
        return null;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:745
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        client user_client = usr.client;
        object message = sanitize_text(trim(@params["message"]));
        bool urgent = Dm.truthy(@params["urgent"]);
        DmList admins = get_admin_counts(R_BAN);
        if (Dm.length(admins["present"]) != 0 || Dm.truthy(is_banned_from(user_client.ckey, "Urgent Adminhelp")))
            urgent = false;

        if (Dm.truthy(user_client.adminhelptimerid))
            return null;

        perform_adminhelp(user_client, message, urgent ? TRUE : FALSE);
        ui.close();
        return null;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:762
    public virtual void perform_adminhelp(client user_client = null, object message = null, object urgent = null)
    {
        if (Dm.truthy(GLOB.say_disabled)) //This is here to try to identify lag problems
        {
            to_chat(usr, span_danger(I18n.T("Speech is currently admin-disabled.")), confidential: TRUE);
            return;
        }

        if (!Dm.truthy(message))
            return;

        //handle muting and automuting
        if (Dm.truthy(Dm.bitand(user_client.prefs.muted, MUTE_ADMINHELP)))
        {
            to_chat(user_client, span_danger(I18n.T("Error: Admin-PM: You cannot send adminhelps (Muted).")), confidential: TRUE);
            return;
        }
        if (Dm.truthy(user_client.handle_spam_prevention(message, MUTE_ADMINHELP)))
            return;

        // BLACKBOX_LOG_ADMIN_VERB("Adminhelp"): the blackbox (not translated)

        if (Dm.truthy(urgent))
        {
            // COOLDOWN_FINISHED(src, ahelp_cooldowns?[user_client.ckey]) and COOLDOWN_START(src, ahelp_cooldowns[user_client.ckey], ...)
            if (!(Dm.num(ahelp_cooldowns?[user_client.ckey]) < world.time))
                urgent = FALSE; // Prevent abuse
            else
                ahelp_cooldowns[user_client.ckey] = world.time + Dm.num(config.Get(typeof(datum_config_entry_number_urgent_ahelp_cooldown))) * (1 * SECONDS);
        }

        if (user_client.current_ticket != null)
        {
            user_client.current_ticket.TimeoutVerb();
            if (Dm.truthy(urgent))
            {
                object sanitized_message = sanitize(copytext_char(message, 1, MAX_MESSAGE_LEN));
                user_client.current_ticket.send_message_to_tgs(sanitized_message, urgent: TRUE);
            }
            user_client.current_ticket.MessageNoRecipient(message, urgent);
            return;
        }

        Dm.@new<datum_admin_help>(message, user_client, FALSE, urgent);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/admin/verbs/adminhelp.dm:193  /datum/admin_help/var/static/ticket_counter
    internal float datum_admin_help__ticket_counter;
}

public static partial class GlobalProcs
{
    // tg: code/modules/admin/verbs/adminhelp.dm:317
    public static void send2adminchat_webhook(object message_or_embed = null, object urgent = null)
    {
        object webhook = config.Get(typeof(datum_config_entry_string_urgent_adminhelp_webhook_url)); // CONFIG_GET(string/urgent_adminhelp_webhook_url)
        if (!Dm.truthy(urgent))
            webhook = config.Get(typeof(datum_config_entry_string_regular_adminhelp_webhook_url)); // CONFIG_GET(string/regular_adminhelp_webhook_url)

        if (!Dm.truthy(webhook))
            return;
        DmList webhook_info = Dm.list();
        if (Dm.istext(message_or_embed))
        {
            object message_content = replacetext(replacetext(message_or_embed, "\\proper", ""), "\\improper", "");
            message_content = has_discord_embeddable_links.Replace(Dm.str(replacetext(message_content, "`", "")), " ```$1``` ");
            webhook_info["content"] = message_content;
        }
        else
        {
            var embed = message_or_embed as datum_discord_embed;
            webhook_info["embeds"] = Dm.list(embed.convert_to_list());
            if (Dm.truthy(embed.content))
                webhook_info["content"] = embed.content;
        }
        object webhook_name = config.Get(typeof(datum_config_entry_string_adminhelp_webhook_name)); // CONFIG_GET(string/adminhelp_webhook_name)
        if (Dm.truthy(webhook_name))
            webhook_info["username"] = webhook_name;
        object webhook_pfp = config.Get(typeof(datum_config_entry_string_adminhelp_webhook_pfp)); // CONFIG_GET(string/adminhelp_webhook_pfp)
        if (Dm.truthy(webhook_pfp))
            webhook_info["avatar_url"] = webhook_pfp;
        // Uncomment when servers are moved to TGS4
        // send2chat(new /datum/tgs_message_conent("[initiator_ckey] | [message_content]"), "ahelp", TRUE)
        DmList headers = Dm.list();
        headers["Content-Type"] = "application/json";
        var request = Dm.@new<datum_http_request>();
        request.prepare(RUSTG_HTTP_METHOD_POST, webhook, Dm.json_encode(webhook_info), headers, "tmp/response.json");
        request.fire_and_forget();
    }

    /// Use this proc when an admin takes action that may be related to an open ticket on what
    /// what can be a client, ckey, or mob
    /// player_message: If the message should be shown in the player ticket panel, fill this out
    /// log_in_blackbox: Whether or not this message with the blackbox system.
    /// If disabled, this message should be logged with a different proc call
    // tg: code/modules/admin/verbs/adminhelp.dm:842
    public static datum_admin_help admin_ticket_log(object what = null, object message = null, object player_message = null, object log_in_blackbox = null)
    {
        // log_in_blackbox: SSblackbox.LogAhelp (not translated)
        client mob_client;
        if (what is mob Mob)
            mob_client = Mob.client;
        else
            mob_client = what as client;
        if (mob_client != null && mob_client.current_ticket != null)
        {
            if (player_message == null)
                mob_client.current_ticket.AddInteraction(message);
            else
                mob_client.current_ticket.AddInteraction(message, player_message);
            return mob_client.current_ticket;
        }
        if (Dm.istext(what)) //ckey
        {
            datum_admin_help active_admin_help = GLOB.ahelp_tickets.CKey2ActiveTicket(what);
            if (active_admin_help != null)
            {
                if (player_message == null)
                    active_admin_help.AddInteraction(message);
                else
                    active_admin_help.AddInteraction(message, player_message);
                return active_admin_help;
            }
        }
        return null;
    }

    //
    // HELPER PROCS
    //

    // tg: code/modules/admin/verbs/adminhelp.dm:872
    public static DmList get_admin_counts(object requiredflags = null)
    {
        requiredflags ??= R_BAN;
        DmList dot = Dm.list(Dm.kv("total", Dm.list()), Dm.kv("noflags", Dm.list()), Dm.kv("afk", Dm.list()), Dm.kv("stealth", Dm.list()), Dm.kv("present", Dm.list()));
        foreach (client X in Dm.each<client>(GLOB.admins))
        {
            (dot["total"] as DmList).Add(X);
            if (!Dm.eq(requiredflags, NONE) && !Dm.truthy(check_rights_for(X, requiredflags)))
                (dot["noflags"] as DmList).Add(X);
            else if (Dm.truthy(X.is_afk()))
                (dot["afk"] as DmList).Add(X);
            else if (Dm.truthy(X.holder.fakekey))
                (dot["stealth"] as DmList).Add(X);
            else
                (dot["present"] as DmList).Add(X);
        }
        return dot;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:885
    public static float send2tgs_adminless_only(object source = null, object msg = null, object requiredflags = null)
    {
        requiredflags ??= R_BAN;
        DmList adm = get_admin_counts(requiredflags);
        var activemins = adm["present"] as DmList;
        float dot = activemins.len;
        // the "No admins online" line to send2adminchat and send2otherserver: TGS and the cross-server comms (not translated)
        return dot;
    }

    // tg: code/modules/admin/verbs/adminhelp.dm:962
    public static object keywords_lookup(object msg = null, object external = null)
    {
        //This is a list of words which are ignored by the parser when comparing message contents for names. MUST BE IN LOWER CASE!
        DmList adminhelp_ignored_words = Dm.list("unknown", "the", "a", "an", "of", "monkey", "alien", "as", "i");

        //explode the input msg into a list
        DmList msglist = Dm.splittext(msg, " ");

        //generate keywords lookup
        DmList surnames = Dm.list();
        DmList forenames = Dm.list();
        DmList ckeys = Dm.list();
        string founds = "";
        foreach (mob M in Dm.each<mob>(GLOB.mob_list))
        {
            DmList indexing = Dm.list(M.real_name, M.name);
            if (M.mind != null)
                indexing.Add(M.mind.name);

            foreach (object @string in Dm.each(indexing))
            {
                DmList L = Dm.splittext(@string, " ");
                int surname_found = 0;
                //surnames
                for (int i = (int)L.len; i >= 1; i--)
                {
                    string word = Dm.ckey(L[i]);
                    if (Dm.truthy(word))
                    {
                        surnames[word] = M;
                        surname_found = i;
                        break;
                    }
                }
                //forenames
                for (int i = 1; i <= surname_found - 1; i++)
                {
                    string word = Dm.ckey(L[i]);
                    if (Dm.truthy(word))
                        forenames[word] = M;
                }
                //ckeys
                ckeys[M.ckey] = M;
            }
        }

        bool ai_found = false;
        string result = "";
        DmList mobs_found = Dm.list();
        foreach (object original_word in Dm.each(msglist))
        {
            string word = Dm.ckey(original_word);
            if (Dm.truthy(word))
            {
                if (!Dm.@in(word, adminhelp_ignored_words))
                {
                    if (word == "ai")
                        ai_found = true;
                    else
                    {
                        var found = ckeys[word] as mob;
                        if (found == null)
                        {
                            found = surnames[word] as mob;
                            if (found == null)
                                found = forenames[word] as mob;
                        }
                        if (found != null)
                        {
                            if (!Dm.@in(found, mobs_found))
                            {
                                mobs_found.Add(found);
                                if (!ai_found && isAI(found))
                                    ai_found = true;
                                bool is_antag = Dm.truthy(found.is_antag());
                                founds += Dm.text("Name: []([]) Key: [] Ckey: [] [] ", found.name, found.real_name, found.key, found.ckey, is_antag ? "(Antag)" : null);
                                // the word's ? (adminmoreinfo) and F (adminplayerobservefollow) links: the admin panels (not translated)
                                result += Dm.text("[] ", original_word);
                                continue;
                            }
                        }
                    }
                }
            }
            result += Dm.text("[] ", original_word);
        }
        if (Dm.truthy(external))
        {
            if (founds == "")
                return "Search Failed";
            else
                return founds;
        }

        return result;
    }
}

// Gaps R5 (D327): the mobs a message names (the commendations' nominee, hearted.dm:56)
public static partial class GlobalProcs
{
    // tg: code/modules/admin/verbs/adminhelp.dm:1031
    public static object get_mob_by_name(object msg = null)
    {
        //This is a list of words which are ignored by the parser when comparing message contents for names. MUST BE IN LOWER CASE!
        DmList ignored_words = Dm.list("unknown", "the", "a", "an", "of", "monkey", "alien", "as", "i");

        //explode the input msg into a list
        var msglist = splittext(msg, " ") as DmList;

        //who might fit the shoe
        DmList potential_hits = Dm.list();

        foreach (object i in Dm.each(GLOB.mob_list))
        {
            var M = (mob)i;
            DmList nameWords = Dm.list();
            if (M.mind == null)
                continue;

            foreach (object @string in Dm.each(splittext(LOWER_TEXT(M.real_name), " ") as DmList))
            {
                if (!Dm.@in(@string, ignored_words))
                    Dm.append(ref nameWords, @string);
            }
            foreach (object @string in Dm.each(splittext(LOWER_TEXT(M.name), " ") as DmList))
            {
                if (!Dm.@in(@string, ignored_words))
                    Dm.append(ref nameWords, @string);
            }

            foreach (object @string in Dm.each(nameWords))
            {
                if (Dm.@in(@string, msglist))
                {
                    Dm.append(ref potential_hits, M);
                    break;
                }
            }
        }

        return potential_hits;
    }
}
