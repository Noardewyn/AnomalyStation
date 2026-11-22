// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/verbs/ooc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A (docs/ai/specs/social.md): the OOC verb, called by the say window's OOC channel (tgui_say's delegate_speech) as the speech
// verbs are (mob_say.cs). The admin side (holder, fakekeys, admin OOC colours, mutes, bans, the admin and access logs) is the admin
// tools (M10, D73): holder is never set and nobody is muted or banned. The OOC word filters read config/word_filter.toml, which the
// port has none of, so they match nothing. Chat emojis (emoji_parse) need tg's chat spritesheet (not-translated.md section 70).
// DEVIATION(DV161): the preferences are not ported (prefs is null), so a sender's and a receiver's preferences read as tg's defaults:
// no hearts, no BYOND member badge, nobody ignored, OOC shown (get_chat_toggles).

public partial class datum_controller_global_vars
{
    // tg: code/modules/client/verbs/ooc.dm:1  GLOBAL_VAR_INIT(OOC_COLOR, null)//If this is null, use the CSS for OOC. Otherwise, use a custom colour.
    public object OOC_COLOR;

    // tg: code/modules/client/verbs/ooc.dm:1
    public virtual void InitGlobalOOC_COLOR()
    {
        OOC_COLOR = null;
        Dm.append(ref gvars_datum_init_order, "OOC_COLOR");
    }

    // tg: code/modules/client/verbs/ooc.dm:2  GLOBAL_VAR_INIT(normal_ooc_colour, "#002eb8")
    public object normal_ooc_colour;

    // tg: code/modules/client/verbs/ooc.dm:2
    public virtual void InitGlobalnormal_ooc_colour()
    {
        normal_ooc_colour = "#002eb8";
        Dm.append(ref gvars_datum_init_order, "normal_ooc_colour");
    }
}

public partial class client : datum
{
    ///talking in OOC uses this
    // tg: code/modules/client/verbs/ooc.dm:5  GAME_VERB(/client, ooc, VERB_OOC, null): the body
    public virtual async DmTask __gvb_ooc(DmList structured_args = null)
    {
        object msg = structured_args["msg"];
        if (Dm.truthy(GLOB.say_disabled)) //This is here to try to identify lag problems
        {
            to_chat(usr, span_danger(I18n.T("Speech is currently admin-disabled.")));
            return;
        }

        object client_initalized = VALIDATE_CLIENT_INITIALIZATION(this);
        if (mob == null || !Dm.truthy(client_initalized))
        {
            if (!Dm.truthy(client_initalized))
                unvalidated_client_error(this); // we only want to throw this warning message when it's directly related to client failure.

            to_chat(usr, span_warning(I18n.T("Failed to send your OOC message. You attempted to send the following message:\n[]", span_big(Dm.str(msg)))));
            return;
        }

        if (holder == null)
        {
            if (!Dm.truthy(GLOB.ooc_allowed))
            {
                to_chat(this, span_danger(I18n.T("OOC is globally muted.")));
                return;
            }
            if (!Dm.truthy(GLOB.dooc_allowed) && Dm.eq(mob.stat, DEAD))
            {
                to_chat(usr, span_danger(I18n.T("OOC for dead mobs has been turned off.")));
                return;
            }
            // tg: :26-28  if(prefs.muted & MUTE_OOC): the admin mutes (M10); prefs read as defaults (DV161): not muted
        }
        // tg: :29-31  if(is_banned_from(ckey, "OOC")): the bans are the admin tools (M10); nobody is banned
        if (QDELETED(this))
            return;

        msg = trim(copytext_char(sanitize(msg), 1, MAX_MESSAGE_LEN));
        object raw_msg = msg;

        // tg: :38-50  is_ooc_filtered and is_soft_ooc_filtered with their report, log and soft alert: the port has no word filter
        // config, so the filters match nothing (see the file comment)

        if (!Dm.truthy(msg))
            return;

        // tg: :55  msg = emoji_parse(msg): the chat emojis (not-translated.md section 70); the text is kept as typed

        if (Dm.truthy(SSticker.HasRoundStarted()) && (Dm.@in(Dm.textchar(msg, 1), Dm.list(".", ";", ":", "#")) || Dm.truthy(findtext_char(msg, "say", 1, 5))))
        {
            if (!Dm.eq(await tgui_alert(usr, I18n.T("Your message \"[]\" looks like it was meant for in game communication, say it in OOC?", raw_msg), I18n.T("Meant for OOC?"), Dm.list(I18n.T("Yes"), I18n.T("No"))), I18n.T("Yes")))
                return;
        }

        if (holder == null)
        {
            if (Dm.truthy(handle_spam_prevention(msg, MUTE_OOC)))
                return;
            if (Dm.truthy(findtext(msg, "byond://")))
            {
                to_chat(this, span_boldannounce(I18n.T("Advertising other servers is not allowed.")));
                log_admin(Dm.text("[] has attempted to advertise in OOC: []", key_name(this), msg));
                message_admins(Dm.text("[] has attempted to advertise in OOC: []", key_name_admin(this), msg));
                return;
            }
        }

        if (!Dm.truthy(Dm.bitand(get_chat_toggles(this), CHAT_OOC)))
        {
            to_chat(this, span_danger(I18n.T("You have OOC muted.")));
            return;
        }

        // tg: :75  mob.log_talk(raw_msg, LOG_OOC): the OOC log (the admin logs, M10)

        object keyname = key;
        // tg: :77-93  the key's tags (prefs.hearted's heart, gaps R5, and the BYOND member badge of prefs.unlock_content with
        // MEMBER_PUBLIC) drawn from the chat spritesheet (sheet.icon_tag): the chat icons (not-translated.md 70, the owner: later); the
        // member's OOC colour: the preferences read as defaults (DV161)

        //The linkify span classes and linkify=TRUE below make ooc text get clickable chat href links if you pass in something resembling a url
        foreach (client receiver in Dm.each<client>(GLOB.clients))
        {
            // tg: :97  if(!receiver.prefs) continue // Client being created or deleted: no client has prefs (DV161), so a client being
            // deleted is what is skipped
            if (QDELETED(receiver))
                continue;
            if (!Dm.truthy(Dm.bitand(get_chat_toggles(receiver), CHAT_OOC)))
                continue;
            // tg: :101  if(holder?.fakekey in receiver.prefs.ignoring): nobody is ignored (DV161)
            float avoid_highlight = receiver == this ? TRUE : FALSE;
            if (holder != null)
            {
                if (!Dm.truthy(holder.fakekey) || receiver.holder != null)
                {
                    if (Dm.truthy(check_rights_for(this, R_ADMIN)))
                    {
                        object ooc_color = Dm.truthy(ooc_colour) ? ooc_colour : prefs.read_preference(typeof(datum_preference_color_ooc_color));
                        to_chat(receiver, span_adminooc(Dm.text("[][] <EM>[][]:</EM> <span class='message linkify'>[]</span>", Dm.truthy(config.Get(typeof(datum_config_entry_flag_allow_admin_ooccolor))) && Dm.truthy(ooc_color) ? Dm.text("<font color=[]>", ooc_color) : "", span_prefix(I18n.T("OOC:")), keyname, Dm.truthy(holder.fakekey) ? Dm.text("/([])", holder.fakekey) : "", msg)), avoid_highlighting: avoid_highlight);
                    }
                    else
                        to_chat(receiver, span_adminobserverooc(span_prefix(I18n.T("OOC:</span> <EM>[][]:</EM> <span class='message linkify'>[]", keyname, Dm.truthy(holder.fakekey) ? Dm.text("/([])", holder.fakekey) : "", msg))), avoid_highlighting: avoid_highlight);
                }
                else
                {
                    if (Dm.truthy(GLOB.OOC_COLOR))
                        to_chat(receiver, Dm.text("<span class='oocplain'><font color='[]'><b>[] <EM>[]:</EM> <span class='message linkify'>[]</span></b></font></span>", GLOB.OOC_COLOR, span_prefix(I18n.T("OOC:")), Dm.truthy(holder.fakekey) ? holder.fakekey : key, msg), avoid_highlighting: avoid_highlight);
                    else
                        to_chat(receiver, span_ooc(span_prefix(I18n.T("OOC:</span> <EM>[]:</EM> <span class='message linkify'>[]", Dm.truthy(holder.fakekey) ? holder.fakekey : key, msg))), avoid_highlighting: avoid_highlight);
                }
            }
            // tg: :118  else if(!(key in receiver.prefs.ignoring)): nobody is ignored (DV161)
            else
            {
                if (Dm.truthy(ooc_colour))
                    to_chat(receiver, Dm.text("<span class='oocplain'><font color='[]'><b>[] <EM>[]:</EM> <span class='message linkify'>[]</span></b></font></span>", ooc_colour, span_prefix(I18n.T("OOC:")), keyname, msg), avoid_highlighting: avoid_highlight);
                else if (Dm.truthy(GLOB.OOC_COLOR))
                    to_chat(receiver, Dm.text("<span class='oocplain'><font color='[]'><b>[] <EM>[]:</EM> <span class='message linkify'>[]</span></b></font></span>", GLOB.OOC_COLOR, span_prefix(I18n.T("OOC:")), keyname, msg), avoid_highlighting: avoid_highlight);
                else
                    to_chat(receiver, span_ooc(span_prefix(I18n.T("OOC:</span> <EM>[]:</EM> <span class='message linkify'>[]", keyname, msg))), avoid_highlighting: avoid_highlight);
            }
        }
    }

    // tg: code/modules/client/verbs/ooc.dm:5  the verb proc (as say_verb's, mob_say.cs): a detached call of the body; the one argument
    // (VERB_ARG_SOURCE_INPUT) is the positional message.
    public virtual void ooc(object msg = null, params object[] __extra)
    {
        Waiting.Detach(__gvb_ooc(Dm.list(Dm.kv("msg", msg))));
    }
}

// tg: code/modules/client/verbs/ooc.dm:131 (interleaved types, rules 1.9; M10 slice F: the ticker turns OOC off at the round start)
public static partial class GlobalProcs
{
    // tg: code/modules/client/verbs/ooc.dm:131
    public static void toggle_ooc(object toggle = null)
    {
        if (toggle != null) //if we're specifically en/disabling ooc
        {
            if (!Dm.eq(toggle, GLOB.ooc_allowed))
                GLOB.ooc_allowed = Dm.num(toggle);
            else
                return;
        }
        else //otherwise just toggle it
            GLOB.ooc_allowed = Dm.truthy(GLOB.ooc_allowed) ? FALSE : TRUE;
        to_chat(world, Dm.text("<span class='oocplain'><B>The OOC channel has been globally [].</B></span>", Dm.truthy(GLOB.ooc_allowed) ? "enabled" : "disabled"));
    }

    // tg: code/modules/client/verbs/ooc.dm:141
    public static void toggle_dooc(object toggle = null)
    {
        if (toggle != null)
        {
            if (!Dm.eq(toggle, GLOB.dooc_allowed))
                GLOB.dooc_allowed = Dm.num(toggle);
            else
                return;
        }
        else
            GLOB.dooc_allowed = Dm.truthy(GLOB.dooc_allowed) ? FALSE : TRUE;
    }
}
