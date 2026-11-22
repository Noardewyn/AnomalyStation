// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/verbs/adminpm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice G6 (D282): tg's admin PMs, both ways. Not translated: the admin verbs Admin PM Mob and Admin PM (the context menu and the
// client list: the console's "pm KEY" asks for the message), TgsPm and GetTgsStealthKey (TGS's chat commands; no TGS bot) and the
// stealth keys (findTrueKey: the port's admins have no stealth mode). BYOND's input() as message is tgui's multiline text prompt
// (DV217). The EXTERNAL_PM_USER branches stay: nothing reaches them without TGS.

public partial class client : datum
{
    // tg: code/modules/admin/verbs/adminpm.dm:1-2
    public const int EXTERNALREPLYCOUNT = 2;
    public const string EXTERNAL_PM_USER = "IRCKEY";

    // BYOND input(src, prompt, title) as message|null (DV217): tgui's multiline text prompt; null when cancelled
    private async DmTask<object> input_message(object prompt, object title, object @default = null)
        => await tgui_input_text(mob, prompt, title, @default, multiline: TRUE);

    /// Replys to some existing ahelp, reply to whom, which can be a client or ckey
    // tg: code/modules/admin/verbs/adminpm.dm:53
    public virtual async DmTask cmd_ahelp_reply(object whom = null)
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
            return;

        if (Dm.truthy(Dm.bitand(prefs.muted, MUTE_ADMINHELP)))
        {
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Reply: You are unable to use admin PM-s (muted).")), confidential: TRUE);
            return;
        }

        // We use the ckey here rather then keeping the client to ensure resistance to client logouts mid execution
        if (whom is client boi)
            whom = boi.ckey;

        object ambiguious_recipient = disambiguate_client(whom);
        if (ambiguious_recipient is not client)
        {
            if (holder != null)
                to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Reply: Client not found.")), confidential: TRUE);
            return;
        }

        // Existing client case
        var recipient = ambiguious_recipient as client;

        // The ticket our recipient is using
        datum_admin_help recipient_ticket = recipient?.current_ticket;
        // Any past interactions with the recipient ticket
        DmList recipient_interactions = recipient_ticket?.ticket_interactions;
        // Any opening interactions with the recipient ticket, IE: interactions started before the ticket first receives a response
        DmList opening_interactions = recipient_ticket?.opening_responders;
        // Our recipient's admin holder, if one exists
        datum_admins recipient_holder = recipient?.holder;
        // The ckey of our recipient
        object recipient_ckey = recipient?.ckey;
        // Our recipient's fake key, if they are faking their ckey
        object recipient_fake_key = recipient_holder?.fakekey;
        // Our ckey, with our mob's name if one exists, formatted with a reply link
        object our_linked_name = key_name_admin(this);
        // The recipient's ckey, formatted with a reply link
        object recipient_linked_ckey = key_name_admin(recipient, FALSE);
        // The recipient's ckey, formatted slightly with html
        object formatted_recipient_ckey = key_name(recipient, FALSE, FALSE);

        string message_prompt = "Message:";
        if (recipient_ticket != null)
        {
            message_admins(Dm.text("[] has started replying to []'s admin help.", our_linked_name, recipient_linked_ckey));
            // If none's interacted with the ticket yet
            if (Dm.length(recipient_interactions) == 1)
            {
                if (Dm.truthy(Dm.length(opening_interactions))) // Inform the admin that they aren't the first
                {
                    object printable_interators = english_list(opening_interactions);
                    Dm.output_to(this, sound(Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), volume: 30)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
                    message_prompt += Dm.text("\n\n**This ticket is already being responded to by: []**", printable_interators);
                }
                // add the admin who is currently responding to the list of people responding
                LAZYADD(ref recipient_ticket.opening_responders, this);
            }
        }

        string request = "Private message to";
        if (Dm.truthy(recipient_fake_key))
            request = Dm.text("[] an Administrator.", request);
        else
            request = Dm.text("[] [].", request, formatted_recipient_ckey);

        object message = await input_message(message_prompt, request);

        if (recipient_ticket != null)
            LAZYREMOVE(ref recipient_ticket.opening_responders, this);

        if (!Dm.truthy(message))
        {
            message_admins(Dm.text("[] has cancelled their reply to []'s admin help.", our_linked_name, recipient_linked_ckey));
            return;
        }

        if (recipient == null || recipient.gc_destroyed != null) //We lost the client during input, disconnected or relogged.
        {
            if (Dm.truthy(GLOB.directory[recipient_ckey])) // Client has reconnected, lets try to recover
                whom = GLOB.directory[recipient_ckey];
            else
            {
                to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Reply: Client not found.")), confidential: TRUE);
                to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: Dm.text("[]<br>[]", span_danger(I18n.T("<b>Message not sent:</b>")), message), confidential: TRUE);
                recipient_ticket?.AddInteraction(Dm.text("<b>No client found, message not sent:</b><br>[]", message));
                return;
            }
        }
        await cmd_admin_pm(whom, message);
    }

    //takes input from cmd_admin_pm_context, cmd_admin_pm_panel or /client/Topic and sends them a PM.
    //Fetching a message if needed.
    //whom here is a client, a ckey, or [EXTERNAL_PM_USER] if this is from tgs. message is the default message to send
    // tg: code/modules/admin/verbs/adminpm.dm:147
    public virtual async DmTask cmd_admin_pm(object whom = null, object message = null)
    {
        if (Dm.truthy(Dm.bitand(prefs.muted, MUTE_ADMINHELP)))
        {
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM: You are unable to use admin PM-s (muted).")), confidential: TRUE);
            return;
        }

        if (holder == null && current_ticket == null) //no ticket? https://www.youtube.com/watch?v=iHSPf6x1Fdo
        {
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("You can no longer reply to this ticket, please open another one by using the Adminhelp verb if need be.")), confidential: TRUE);
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_notice(Dm.text(I18n.T("Message: []"), message)), confidential: TRUE);
            return;
        }

        // We use the ckey here rather then keeping the client to ensure resistance to client logouts mid execution
        if (whom is client boi)
            whom = boi.ckey;

        object message_to_send = await request_adminpm_message(disambiguate_client(whom), message);
        if (!Dm.truthy(message_to_send))
            return;

        if (!Dm.truthy(sends_adminpm_message(disambiguate_client(whom), message_to_send)))
            return;

        notify_adminpm_message(disambiguate_client(whom), message_to_send);
    }

    /// Requests an admin pm message to send
    /// message_target here can be either [EXTERNAL_PM_USER], indicating that this message is intended for some external chat channel
    /// or a /client, which we will then store info about to ensure logout -> logins are protected as expected
    /// Accepts an optional existing message, which will be used in place of asking the recipient assuming all other conditions are met
    /// Returns the message to send or null if no message is found
    /// Sleeps
    // tg: code/modules/admin/verbs/adminpm.dm:187
    public virtual async DmTask<object> request_adminpm_message(object ambiguious_recipient = null, object existing_message = null)
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
            return null;

        if (Dm.eq(ambiguious_recipient, EXTERNAL_PM_USER))
        {
            if (!Dm.truthy(externalreplyamount)) //to prevent people from spamming irc/discord
            {
                to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Message: External reply cap hit.")), confidential: TRUE);
                return null;
            }
            object external_msg = "";
            if (Dm.truthy(existing_message))
                external_msg = existing_message;
            else
                external_msg = await input_message("Message:", "Private message to Administrator");

            if (!Dm.truthy(external_msg))
            {
                to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Message: No message input.")), confidential: TRUE);
                return null;
            }

            if (holder != null)
            {
                to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Message: Use the admin IRC/Discord channel, nerd.")), confidential: TRUE);
                return null;
            }
            return external_msg;
        }

        if (ambiguious_recipient is not client)
        {
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Message: Client not found.")), confidential: TRUE);
            return null;
        }

        var recipient = ambiguious_recipient as client;
        // Stored in case client is deleted between this and after the message is input
        object recipient_ckey = recipient?.ckey;
        // Stored in case client is deleted between this and after the message is input
        datum_admin_help recipient_ticket = recipient?.current_ticket;
        // Our current active ticket
        datum_admin_help our_ticket = current_ticket;
        // If our recipient is an admin, this is their admins datum
        datum_admins recipient_holder = recipient?.holder;
        // If our recipient has a fake name, this is it
        object recipient_fake_key = recipient_holder?.fakekey;
        // Just the recipient's ckey, formatted for htmlifying stuff
        object recipient_print_key = key_name(recipient, FALSE, FALSE);

        // The message we intend on returning
        object msg = "";
        if (Dm.truthy(existing_message))
            msg = existing_message;
        else
        {
            string request = "Private message to";
            if (Dm.truthy(recipient_fake_key))
                request = Dm.text("[] an Administrator.", request);
            else
                request = Dm.text("[] [].", request, recipient_print_key);
            //get message text, limit its length.and clean/escape html
            msg = await input_message("Message:", request);
            msg = trim(msg);
        }

        if (!Dm.truthy(msg))
        {
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Message: No message input.")), confidential: TRUE);
            return null;
        }

        if (recipient != null && recipient.gc_destroyed == null)
            return msg;
        // Client has disappeared due to logout
        if (Dm.truthy(GLOB.directory[recipient_ckey])) // Client has reconnected, lets try to recover
            return msg;

        // We don't tell standard users if a ticket drops because admins have a way to actually see
        // Past tickets, and well, admins are the ones who might ban you if you ignore them
        if (holder != null)
        {
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Message: Client not found.")), confidential: TRUE);
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: Dm.text("[]<br>[]", span_danger(I18n.T("<b>Message not sent:</b>")), msg), confidential: TRUE);
            recipient_ticket?.AddInteraction(Dm.text("<b>No client found, message not sent:</b><br>[]", msg));
            return null;
        }
        our_ticket?.MessageNoRecipient(msg);
        return null;
    }

    /// Sends a pm message via the tickets system
    /// message_target here can be either [EXTERNAL_PM_USER], indicating that this message is intended for some external chat channel
    /// or a /client, in which case we send in the standard form
    /// send_message is the raw message to send, it will be filtered and treated to ensure we do not break any text handling
    /// Returns FALSE if the send failed, TRUE otherwise
    // tg: code/modules/admin/verbs/adminpm.dm:291
    public virtual object sends_adminpm_message(object ambiguious_recipient = null, object send_message = null)
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
            return FALSE;

        send_message = adminpm_filter_text(ambiguious_recipient, send_message);
        if (!Dm.truthy(send_message))
            return null;

        if (Dm.truthy(handle_spam_prevention(send_message, MUTE_ADMINHELP)))
            // handle_spam_prevention does its own "hey buddy ya fucker up here's what happen"
            return FALSE;

        object raw_message = send_message;

        if (holder != null)
            send_message = emoji_parse(send_message);

        object keyword_parsed_msg = keywords_lookup(send_message);
        // Stores a bit of html with our ckey, name, and a linkified string to click and rely to us with
        object name_key_with_link = key_name(this, TRUE, TRUE);

        if (Dm.eq(ambiguious_recipient, EXTERNAL_PM_USER))
        {
            datum_admin_help new_admin_help = admin_ticket_log(this,
                Dm.text("<font color='red'>Reply PM from-<b>[]</b> to <i>External</i>: []</font>", name_key_with_link, keyword_parsed_msg),
                player_message: Dm.text("<font color='red'>Reply PM from-<b>[]</b> to <i>External</i>: []</font>", name_key_with_link, send_message));

            new_admin_help?.reply_to_admins_notification(raw_message);

            externalreplyamount--;

            // send2adminchat(category, raw_message): TGS (not translated)
            return TRUE;
        }

        if (ambiguious_recipient is not client)
        {
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Send: Client not found.")), confidential: TRUE);
            return FALSE;
        }

        var recipient = ambiguious_recipient as client;
        datum_admins recipient_holder = recipient.holder;
        datum_admins our_holder = holder;

        // The sound preferences of the recipient, at least that's how we'll be using it here
        float sound_prefs = recipient?.prefs?.toggles ?? 0;
        // Stores a bit of html that contains the ckey of the recipient, its mob's name if any exist, and a link to reply to them with
        object their_name_with_link = key_name(recipient, TRUE, TRUE);
        // Stores a bit of html with our ckey highlighted as a reply link
        object link_to_us = key_name(this, TRUE, FALSE);
        // Stores a bit of html with outhe ckey of the recipientr highlighted as a reply link
        object link_to_their = key_name(recipient, TRUE, FALSE);
        // Our current ticket, can (supposedly) be null here
        datum_admin_help ticket = current_ticket;
        // The recipient's current ticket, could in theory? maybe? be null here
        datum_admin_help recipient_ticket = recipient?.current_ticket;

        // If we should do a full on boink, so with the text and extra flair and everything
        // We want to always do this so long as WE are an admin, and we're messaging the "loser" of the converstation
        bool full_boink = false;
        // Only admins can perform boinks
        if (our_holder != null)
            full_boink = true;
        // Tickets will only generate for the non admin/admin being boinked. This check is to ensure boinked admins don't send the same
        // ADMINISTRAITOR PRIVATE MESSAGE text to their boinker every time they respond
        if (recipient_holder != null && ticket != null)
            full_boink = false;

        // If we're gonna boink em, do it now
        // It is worth noting this will always generate the target a ticket if they don't already have one (tickets will generate if a player ahelps automatically, outside this logic)
        // So past this point, because of our block above here, we can be reasonably guarenteed that the user will have a ticket
        if (full_boink)
        {
            // Do BIG RED TEXT
            // Full boinks will always be done to players, so we are not guarenteed that they won't have a ticket
            if (recipient_ticket == null)
            {
                Dm.@new<datum_admin_help>(send_message, recipient, TRUE);
                // the recached ticket ids and SSblackbox.LogAhelp: the blackbox (not translated)
            }

            recipient.receive_ahelp(
                link_to_us,
                span_linkify(Dm.str(send_message)));

            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_notice(Dm.text("Admin PM to-<b>[]</b>: []", their_name_with_link, span_linkify(Dm.str(send_message)))), confidential: TRUE);

            admin_ticket_log(recipient,
                Dm.text("<font color='purple'>PM From []: []</font>", name_key_with_link, keyword_parsed_msg),
                log_in_blackbox: FALSE,
                player_message: Dm.text("<font color='purple'>PM From []: []</font>", link_to_us, send_message));

            // the reply's SSblackbox.LogAhelp: the blackbox (not translated)

            //always play non-admin recipients the adminhelp sound
            Dm.output_to(recipient, sound(Dm.resource("sound/effects/adminhelp.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            return TRUE;
        }

        // Ok if we're here, either this message is for an admin, or someone somehow figured out how to send a new message as a player
        // First case well, first
        if (our_holder == null && recipient_holder == null) //neither are admins
        {
            if (ticket == null)
            {
                to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Send: Non-admin to non-admin PM communication is forbidden.")), confidential: TRUE);
                to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: Dm.text("[]<br>[]", span_danger(I18n.T("<b>Message not sent:</b>")), send_message), confidential: TRUE);
                return FALSE;
            }
            ticket.MessageNoRecipient(send_message);
            return TRUE;
        }

        // Ok by this point the recipient has to be an admin, and this is either an admin on admin event, or a player replying to an admin

        // You're replying to a ticket that is closed. Bad move. You must have started replying before the close, and then got input()'d
        // Lets be nice and pass this off to a new ticket, as we recomend above
        if (ticket == null)
        {
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Send: Attempted to send a reply to a closed ticket.")), confidential: TRUE);
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_notice(I18n.T("Relaying message to a new admin help.")), confidential: TRUE);
            GLOB.admin_help_ui_handler.perform_adminhelp(this, raw_message, FALSE);
            return FALSE;
        }

        // Let's play some music for the admin, only if they want it tho
        if (Dm.truthy(Dm.bitand(sound_prefs, SOUND_ADMINHELP)))
            Dm.output_to(recipient, sound(Dm.resource("sound/effects/adminhelp.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)

        if (Dm.truthy(ticket._listen_lookup?[COMSIG_ADMIN_HELP_REPLIED]))
            ticket._SendSignal(COMSIG_ADMIN_HELP_REPLIED, Dm.list(ticket)); // SEND_SIGNAL

        // Admin on admin violence first
        if (our_holder != null)
        {
            recipient.receive_ahelp(
                name_key_with_link,
                span_linkify(Dm.str(keyword_parsed_msg)),
                "danger");

            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_notice(Dm.text("Admin PM to-<b>[]</b>: []", their_name_with_link, span_linkify(Dm.str(keyword_parsed_msg)))), confidential: TRUE);

            //omg this is dumb, just fill in both their logs
            string interaction_message = Dm.text("<font color='purple'>PM from-<b>[]</b> to-<b>[]</b>: []</font>", name_key_with_link, their_name_with_link, keyword_parsed_msg);
            string player_interaction_message = Dm.text("<font color='purple'>PM from-<b>[]</b> to-<b>[]</b>: []</font>", link_to_us, link_to_their, send_message);
            admin_ticket_log(this,
                interaction_message,
                log_in_blackbox: FALSE,
                player_message: player_interaction_message);
            if (recipient != this) //reeee
            {
                admin_ticket_log(recipient,
                    interaction_message,
                    log_in_blackbox: FALSE,
                    player_message: player_interaction_message);
            }

            // SSblackbox.LogAhelp: the blackbox (not translated)
            return TRUE;
        }

        // This is us (a player) trying to talk to the recipient (an admin)
        string replymsg = Dm.text("Reply PM from-<b>[]</b>: []", name_key_with_link, span_linkify(Dm.str(keyword_parsed_msg)));
        string player_replymsg = Dm.text("Reply PM from-<b>[]</b>: []", link_to_us, span_linkify(Dm.str(send_message)));
        admin_ticket_log(this,
            Dm.text("<font color='red'>[]</font>", replymsg),
            log_in_blackbox: FALSE,
            player_message: player_replymsg);
        to_chat(recipient, type: MESSAGE_TYPE_ADMINPM, html: span_danger(replymsg), confidential: TRUE);

        ticket.reply_to_admins_notification(send_message);
        // SSblackbox.LogAhelp: the blackbox (not translated)

        return TRUE;
    }

    /// Notifies all admins about the existence of an admin pm, then logs the pm
    /// message_target here can be either [EXTERNAL_PM_USER], indicating that this message is intended for some external chat channel
    /// or a /client, in which case we send in the standard form
    /// log_message is the raw message to send, it will be filtered and treated to ensure we do not break any text handling
    // tg: code/modules/admin/verbs/adminpm.dm:501
    public virtual void notify_adminpm_message(object ambiguious_recipient = null, object log_message = null)
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
            return;

        // First we filter, because these procs can be called by anyone with debug, and I don't trust that check
        // gotta make sure none's fucking about
        log_message = adminpm_filter_text(ambiguious_recipient, log_message);
        if (!Dm.truthy(log_message))
            return;

        object raw_message = log_message;

        if (holder != null)
            log_message = emoji_parse(log_message);

        object keyword_parsed_msg = keywords_lookup(log_message);
        // Shows our ckey and the name of any mob we might be possessing
        object our_name = key_name(this);
        // Shows our ckey/name embedded inside a clickable link to reply to this message
        object our_linked_ckey = key_name(this, TRUE, FALSE);
        // Our current active ticket
        datum_admin_help ticket = current_ticket;
        // Our current ticket id, if one exists
        float? ticket_id = ticket?.id;

        if (Dm.eq(ambiguious_recipient, EXTERNAL_PM_USER))
        {
            // Guard against the possibility of a null, since it'll runtime and spit out the contents of what should be a private ticket.
            if (ticket != null)
                log_admin_private(Dm.text("PM: Ticket #[]: []->External: []", ticket_id, our_name, sanitize_text(trim(raw_message))));
            else
                log_admin_private(Dm.text("PM: []->External: []", our_name, sanitize_text(trim(raw_message))));
            foreach (client lad in Dm.each<client>(GLOB.admins))
                to_chat(lad, type: MESSAGE_TYPE_ADMINPM, html: span_notice(Dm.text("<B>PM: []-&gt;External:</B> []", our_linked_ckey, keyword_parsed_msg)), confidential: TRUE);
            return;
        }
        if (ambiguious_recipient is not client)
        {
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Notify: Client not found.")), confidential: TRUE);
            return;
        }

        var recipient = ambiguious_recipient as client;
        // The key of our recipient
        object recipient_key = recipient?.key;
        // Shows the recipient's ckey and the name of any mob it might be possessing
        object recipient_name = key_name(recipient);
        // Shows the recipient's ckey/name embedded inside a clickable link to reply to this message
        object recipient_linked_ckey = key_name(recipient, TRUE, FALSE);

        // window_flash(recipient, ignorepref = TRUE): BYOND's taskbar flash (not translated)
        if (ticket != null)
            log_admin_private(Dm.text("PM: Ticket #[]: []->[]: []", ticket_id, our_name, recipient_name, sanitize_text(trim(raw_message))));
        else
            log_admin_private(Dm.text("PM: []->[]: []", our_name, recipient_name, sanitize_text(trim(raw_message))));
        //we don't use message_admins here because the sender/receiver might get it too
        foreach (client lad in Dm.each<client>(GLOB.admins))
        {
            if (Dm.eq(lad.key, key) || Dm.eq(lad.key, recipient_key)) //check to make sure client/lad isn't the sender or recipient
                continue;
            to_chat(lad, type: MESSAGE_TYPE_ADMINPM, html: span_notice(Dm.text("<B>PM: []-&gt;[]:</B> []", our_linked_ckey, recipient_linked_ckey, keyword_parsed_msg)), confidential: TRUE);
        }
    }

    /// Accepts a message and an ambiguious recipient (some sort of client representative, or [EXTERNAL_PM_USER])
    /// Returns the filtered message if it passes all checks, or null if the send fails
    // tg: code/modules/admin/verbs/adminpm.dm:569
    public virtual object adminpm_filter_text(object ambiguious_recipient = null, object message = null)
    {
        if (Dm.truthy(Dm.bitand(prefs.muted, MUTE_ADMINHELP)))
        {
            to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Filter: You are unable to use admin PM-s (muted).")), confidential: TRUE);
            return null;
        }

        //clean the message if it's not sent by a high-rank admin
        if (!Dm.truthy(check_rights(R_SERVER | R_DEBUG, 0)) || Dm.eq(ambiguious_recipient, EXTERNAL_PM_USER)) //no sending html to the poor bots
        {
            message = sanitize(copytext_char(message, 1, MAX_MESSAGE_LEN));
            if (!Dm.truthy(message))
            {
                to_chat(this, type: MESSAGE_TYPE_ADMINPM, html: span_danger(I18n.T("Error: Admin-PM-Filter: Your message contained only HTML, it's been sanitized away and the message disregarded.")), confidential: TRUE);
                return null;
            }
        }
        return message;
    }

    // tg: code/modules/admin/verbs/adminpm.dm:747
    public virtual void receive_ahelp(object reply_to = null, object message = null, object span_class = null)
    {
        span_class ??= "adminsay";
        to_chat(
            this,
            type: MESSAGE_TYPE_ADMINPM,
            html: fieldset_block(
                span_adminhelp(I18n.T("Administrator private message")),
                Dm.text("<span class='[]'>Admin PM from-<b>[]</b></span>\n\n<span class='[]'>[]</span>\n\n<i class='adminsay'>[]</i>", span_class, reply_to, span_class, message, I18n.T("Click on the administrator's name to reply.")),
                "boxed_message red_box"),
            confidential: TRUE);

        if (current_ticket != null)
            current_ticket.player_replied = FALSE;

        if (Dm.truthy(_listen_lookup?[COMSIG_ADMIN_HELP_RECEIVED]))
            _SendSignal(COMSIG_ADMIN_HELP_RECEIVED, Dm.list(this, message)); // SEND_SIGNAL
    }
}

public static partial class GlobalProcs
{
    /// Takes an argument which could be either a ckey, /client, or IRC marker, and returns a client if possible
    /// Returns [EXTERNAL_PM_USER] if an IRC marker is detected
    /// Otherwise returns null
    // tg: code/modules/admin/verbs/adminpm.dm:731
    public static object disambiguate_client(object whom = null)
    {
        if (whom is client)
            return whom;

        if (!Dm.istext(whom) || !(Dm.length(whom) >= 1))
            return null;

        object searching_ckey = whom;
        // if(IS_FAKE_KEY(whom)) searching_ckey = findTrueKey(whom): the stealth keys (not translated: no stealth mode)

        if (Dm.eq(searching_ckey, client.EXTERNAL_PM_USER))
            return client.EXTERNAL_PM_USER;

        return GLOB.directory[searching_ckey];
    }
}
