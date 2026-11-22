// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/verbs/pray.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M9 slice A): the pray verb, called by the say window's Pray channel (tgui_say's delegate_speech) as the speech verbs are
// (mob_say.cs). M10x slice A: the prayer's type, its signal and the admins' boxed message, without the requests manager, the chat icon
// and the admins' links. M10 slice G5: message_centcom and message_syndicate (the communications console); gaps CM1: nuke_request.

public partial class mob : atom_movable
{
    // tg: code/modules/admin/verbs/pray.dm:2  GAME_VERB(/mob, pray, VERB_PRAY, null): the body
    public virtual async DmTask __gvb_pray(DmList structured_args = null)
    {
        object message = structured_args["message"];
        if (Dm.truthy(GLOB.say_disabled)) //This is here to try to identify lag problems
        {
            to_chat(this, span_danger(I18n.T("Speech is currently admin-disabled.")), confidential: TRUE);
            return;
        }

        message = copytext_char(sanitize(message), 1, MAX_MESSAGE_LEN);
        if (!Dm.truthy(message))
            return;
        log_prayer(Dm.text("[]/([]): []", key, name, message));
        if (client != null)
        {
            // tg: :12-14  if(src.client.prefs.muted & MUTE_PRAY): the admin mutes (M10); prefs read as defaults (DV161): not muted
            if (Dm.truthy(client.handle_spam_prevention(message, MUTE_PRAY)))
                return;
        }

        object prayer_type = DEFAULT_PRAYER;
        DmList deities = Dm.list();
        if (Dm.eq(job, JOB_CHAPLAIN))
        {
            prayer_type = CHAPLAIN_PRAYER;
            if (Dm.truthy(GLOB.deity))
                deities.Add(GLOB.deity);
        }
        // tg: :25-30  IS_CULTIST(src) and IS_HERETIC_OR_MONSTER(src): the antagonists are the alpha's; nobody is one
        else if (HAS_TRAIT(this, TRAIT_SPIRITUAL))
            prayer_type = SPIRITUAL_PRAYER;
        else if (HAS_TRAIT(this, TRAIT_EVIL))
            prayer_type = EVIL_PRAYER;

        object cross = mutable_appearance("icons/obj/storage/book.dmi", GLOB.prayer_type_to_icon_state[prayer_type] as string);

        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_SEND_PRAYER]))
            SSdcs._SendSignal(COMSIG_GLOB_SEND_PRAYER, Dm.list(SSdcs, this, message, prayer_type, cross, deities));

        object msg_tmp = message;
        // tg: :41  GLOB.requests.pray(src.client, message, src.job == JOB_CHAPLAIN): the requests manager (the admin panels, M10+)
        // tg: :42  icon2html(cross, GLOB.admins), ADMIN_FULLMONTY(src) and ADMIN_SC(src): the chat icons (not-translated.md 70) and the
        // admins' links (M10+), as message_centcom's: key_name_admin names the one who prays
        message = span_adminnotice(Dm.text("<b><font color=[]>[][]: </font>[]:</b> []", GLOB.prayer_type_to_font_color[prayer_type], prayer_type, Dm.truthy(Dm.length(deities)) ? Dm.text(" (to [])", english_list(deities)) : "", key_name_admin(this), span_linkify(Dm.str(message))));
        message = custom_boxed_message(GLOB.prayer_type_to_message_box[prayer_type] as string, message);
        foreach (client C in Dm.each<client>(GLOB.admins))
        {
            if (Dm.truthy(Dm.bitand(get_chat_toggles(C), CHAT_PRAYER)))
                to_chat(C, message, type: MESSAGE_TYPE_PRAYER, confidential: TRUE);
        }
        to_chat(this, span_info(I18n.T("You pray to the gods: \"[]\"", msg_tmp)), confidential: TRUE);

        // tg: :49  BLACKBOX_LOG_ADMIN_VERB("Prayer"): the blackbox (M10)
    }

    // tg: code/modules/admin/verbs/pray.dm:2  the verb proc (as say_verb's): a detached call of the body; a caller without a client
    // does nothing; the one argument (VERB_ARG_SOURCE_INPUT) is the positional message.
    public virtual void pray(object message = null, params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_pray(Dm.list(Dm.kv("message", message))));
    }
}

public static partial class GlobalProcs
{
    /// Used by communications consoles to message CentCom
    // tg: code/modules/admin/verbs/pray.dm:54
    public static void message_centcom(object text = null, mob sender = null)
    {
        object msg = copytext_char(sanitize(text), 1, MAX_MESSAGE_LEN);
        // tg: :56  GLOB.requests.message_centcom(sender.client, msg): the requests manager (the admin tools, M10+)
        // tg: ADMIN_FULLMONTY(sender) and ADMIN_CENTCOM_REPLY(sender): the admins' links (M10+); key_name_admin names the sender
        msg = span_adminnotice(Dm.text("<b><font color=orange>CENTCOM:</font>[]:</b> []", key_name_admin(sender), msg));
        // tg: :58-60  the comms_notification preference's sound for each admin: the admins' preferences (M10+)
        to_chat(GLOB.admins, msg, type: MESSAGE_TYPE_PRAYER, confidential: TRUE);
        foreach (obj_machinery_computer_communications console in Dm.each<obj_machinery_computer_communications>(GLOB.shuttle_caller_list))
            console.override_cooldown();
    }

    /// Used by communications consoles to message the Syndicate
    // tg: code/modules/admin/verbs/pray.dm:66
    public static void message_syndicate(object text = null, mob sender = null)
    {
        object msg = copytext_char(sanitize(text), 1, MAX_MESSAGE_LEN);
        // tg: :68  GLOB.requests.message_syndicate(sender.client, msg): the requests manager (the admin tools, M10+)
        msg = span_adminnotice(Dm.text("<b><font color=crimson>SYNDICATE:</font>[]:</b> []", key_name_admin(sender), msg));
        to_chat(GLOB.admins, msg, type: MESSAGE_TYPE_PRAYER, confidential: TRUE);
        foreach (obj_machinery_computer_communications console in Dm.each<obj_machinery_computer_communications>(GLOB.shuttle_caller_list))
            console.override_cooldown();
    }

    /// Used by communications consoles to request the nuclear launch codes
    // tg: code/modules/admin/verbs/pray.dm:78  (gaps CM1: the communications console's request)
    public static void nuke_request(object text = null, mob sender = null)
    {
        object msg = copytext_char(sanitize(text), 1, MAX_MESSAGE_LEN);
        // tg: :80  GLOB.requests.nuke_request(sender.client, msg): the requests manager (the admin tools, M10+)
        // tg: ADMIN_FULLMONTY(sender), ADMIN_CENTCOM_REPLY(sender) and ADMIN_SET_SD_CODE: the admins' links (M10+); key_name_admin names
        // the sender
        msg = span_adminnotice(Dm.text("<b><font color=orange>NUKE CODE REQUEST:</font>[]:</b> []", key_name_admin(sender), msg));
        foreach (client staff in Dm.each<client>(GLOB.admins))
            Dm.output_to(staff, sound(Dm.resource("sound/misc/server-ready.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        to_chat(GLOB.admins, msg, type: MESSAGE_TYPE_PRAYER, confidential: TRUE);
        foreach (obj_machinery_computer_communications console in Dm.each<obj_machinery_computer_communications>(GLOB.shuttle_caller_list))
            console.override_cooldown();
    }
}