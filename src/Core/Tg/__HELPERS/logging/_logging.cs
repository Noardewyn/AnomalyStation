// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/_logging.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only warning, notice, start_log, shutdown_logging (logging and chat, wave 3), key_name, key_name_admin,
// key_name_and_tag and loc_name (wave 4, package C: /mob/Initialize logs every mob's tag with key_name; the client
// branches of key_name, M1 P1; its mind branch, syndicate A1) translated.
// WARNING(MSG) (:2) is expanded inline at each use (rules 11.1 (d), (f): it reads src and usr and takes __FILE__ and
// __LINE__); NOTICE(MSG) (:8) has no use in tg; testing(msg) and the testing_profile_* macros expand to nothing in
// production (:48-54, TESTING undefined, rules 1.7), so their uses leave no statement; log_test (:67-71) and
// log_reftracker/log_harddel (:73-84) are inactive in production. /atom/proc/log_message (:98, D325: the package wrapper's attack
// log) translated; __check_serialization_semver comes with its first caller.

public static partial class GlobalProcs
{
    //print a warning message to world.log
    // tg: code/__HELPERS/logging/_logging.dm:3
    public static void warning(object msg = null)
    {
        msg = Dm.text("## WARNING: []", msg);
        log_world(msg);
    }

    //not an error or a warning, but worth to mention on the world log, just in case.
    // tg: code/__HELPERS/logging/_logging.dm:9
    public static void notice(object msg = null)
    {
        msg = Dm.text("## NOTICE: []", msg);
        log_world(msg);
    }

    /* For logging round startup. */
    // tg: code/__HELPERS/logging/_logging.dm:152
    public static void start_log(object log = null)
    {
        WRITE_LOG(log, Dm.text("Starting up round ID [].\n-------------------------", GLOB.round_id));
    }

    /* Close open log handles. This should be called as late as possible, and no logging should hapen after. */
    // tg: code/__HELPERS/logging/_logging.dm:156
    public static void shutdown_logging()
    {
        rustg_log_close_all();
        logger.shutdown_logging();
    }

    /* Helper procs for building detailed log lines */
    // tg: code/__HELPERS/logging/_logging.dm:161
    public static object key_name(object whom = null, object include_link = null, float? include_name = null)
    {
        include_name ??= TRUE;
        object dot = null;
        mob M = null;
        client C = null;
        object key = null;
        string ckey = null;
        object fallback_name = null;

        if (!Dm.truthy(whom))
            return "*null*";
        if (whom is client)
        {
            C = whom as client;
            M = C.mob;
            key = C.key;
            ckey = C.ckey;
        }
        else if (ismob(whom))
        {
            M = whom as mob;
            C = M.client;
            key = M.key;
            ckey = M.ckey;
        }
        else if (istext(whom))
        {
            key = whom;
            ckey = Dm.ckey(whom);
            C = GLOB.directory[ckey] as client;
            if (Dm.truthy(C))
                M = C.mob;
        }
        else if (whom is datum_mind)
        {
            datum_mind mind = (datum_mind)whom;
            key = mind.key;
            ckey = Dm.ckey(key);
            if (mind.current != null)
            {
                M = mind.current;
                if (M.client != null)
                    C = M.client;
            }
            else
                fallback_name = mind.name;
        }
        else // Catch-all cases if none of the types above match
        {
            string swhom = null;

            if (whom is atom)
            {
                atom A = whom as atom;
                swhom = Dm.text("[]", A.name);
            }
            else if (isdatum(whom))
                swhom = Dm.text("[]", whom);

            if (!Dm.truthy(swhom))
                swhom = "*invalid*";

            return Dm.text("\\[[]\\]", swhom);
        }

        dot = "";

        if (!Dm.truthy(ckey))
            include_link = FALSE;

        if (Dm.truthy(key))
        {
            if (Dm.truthy(C?.holder) && Dm.truthy(C.holder.fakekey) && !Dm.truthy(include_name))
            {
                if (Dm.truthy(include_link))
                {
                    // GAP(lead): /client/proc/getStealthKey (modules/admin/stealthmode.dm) is not declared (admins, M10); a
                    // stealthed admin needs the admin tooling.
                    Dm.NotTranslated("code/__HELPERS/logging/_logging.dm:208", "key_name: the stealth key of a stealthed admin's link (getStealthKey; admin tooling, M10+)");
                }
                Dm.append(ref dot, "Administrator");
            }
            else
            {
                if (Dm.truthy(include_link))
                    Dm.append(ref dot, Dm.text("<a href='byond://?priv_msg=[]'>", ckey));
                Dm.append(ref dot, key);
            }
            if (!Dm.truthy(C))
                Dm.append(ref dot, "[DC]");

            if (Dm.truthy(include_link))
                Dm.append(ref dot, "</a>");
        }
        else
            Dm.append(ref dot, "*no key*");

        if (Dm.truthy(include_name))
        {
            if (Dm.truthy(M))
            {
                if (Dm.truthy(M.real_name))
                    Dm.append(ref dot, Dm.text("/([])", M.real_name));
                else if (Dm.truthy(M.name))
                    Dm.append(ref dot, Dm.text("/([])", M.name));
            }
            else if (Dm.truthy(fallback_name))
                Dm.append(ref dot, Dm.text("/([])", fallback_name));
        }

        return dot;
    }

    // tg: code/__HELPERS/logging/_logging.dm:243
    public static object key_name_admin(object whom = null, float? include_name = null)
    {
        include_name ??= TRUE;
        return key_name(whom, TRUE, include_name);
    }

    // tg: code/__HELPERS/logging/_logging.dm:246
    public static object key_name_and_tag(object whom = null, object include_link = null, float? include_name = null)
    {
        include_name ??= TRUE;
        string tag = "!tagless!"; // whom can be null in key_name() so lets set this as a safety
        if (isatom(whom))
        {
            atom subject = whom as atom;
            tag = subject.tag;
        }
        return Dm.text("[] ([])", key_name(whom, include_link, include_name), tag);
    }

    // tg: code/__HELPERS/logging/_logging.dm:253
    public static object loc_name(atom A = null)
    {
        if (!(A is atom))
            return "(INVALID LOCATION)";

        turf T = A as turf;
        if (!(T is turf))
            T = get_turf(A);

        if (T is turf)
            return Dm.text("([])", AREACOORD(T));
        else if (Dm.truthy(A.loc))
            return "(UNKNOWN (?, ?, ?))";
        return null;
    }
}

public partial class atom
{
    /**
     * Generic logging helper
     *
     * reads the type of the log
     * and writes it to the respective log file
     * unless log_globally is FALSE
     * Arguments:
     * * message - The message being logged
     * * message_type - the type of log the message is(ATTACK, SAY, etc)
     * * color - color of the log text
     * * log_globally - boolean checking whether or not we write this log to the log file
     */
    // tg: code/__HELPERS/logging/_logging.dm:98
    public virtual object log_message(object message = null, object message_type = null, object color = null, float? log_globally = null, DmList data = null)
    {
        log_globally ??= TRUE;
        if (!Dm.truthy(log_globally))
            return null;

        object log_text = Dm.text("[] [] []", key_name_and_tag(this), message, loc_name(this));
        switch ((int)Dm.num(message_type))
        {
            /// ship both attack logs and victim logs to the end of round attack.log just to ensure we don't lose information
            case LOG_ATTACK:
            case LOG_VICTIM:
                log_attack(log_text, data);
                break;
            case LOG_SAY:
                log_say(log_text, data);
                break;
            case LOG_WHISPER:
                log_whisper(log_text, data);
                break;
            case LOG_EMOTE:
                log_emote(log_text, data);
                break;
            case LOG_RADIO_EMOTE:
                log_radio_emote(log_text, data);
                break;
            case LOG_DSAY:
                log_dsay(log_text, data);
                break;
            case LOG_PDA:
                log_pda(log_text, data);
                break;
            case LOG_CHAT:
                log_chat(log_text, data);
                break;
            case LOG_COMMENT:
                log_comment(log_text, data);
                break;
            case LOG_TELECOMMS:
                log_telecomms(log_text, data);
                break;
            case LOG_TRANSPORT:
                log_transport(log_text, data);
                break;
            case LOG_ECON:
                log_econ(log_text, data);
                break;
            case LOG_OOC:
                log_ooc(log_text, data);
                break;
            case LOG_ADMIN:
                log_admin(log_text, data);
                break;
            case LOG_ADMIN_PRIVATE:
                log_admin_private(log_text, data);
                break;
            case LOG_ASAY:
                log_adminsay(log_text, data);
                break;
            case LOG_OWNERSHIP:
                log_game(log_text, data);
                break;
            case LOG_GAME:
                log_game(log_text, data);
                break;
            case LOG_MECHA:
                log_mecha(log_text, data);
                break;
            case LOG_SHUTTLE:
                log_shuttle(log_text, data);
                break;
            case LOG_SPEECH_INDICATORS:
                log_speech_indicators(log_text, data);
                break;
            default:
                stack_trace(Dm.text("Invalid individual logging type: []. Defaulting to [] (LOG_GAME).", message_type, LOG_GAME), "code/__HELPERS/logging/_logging.dm", 147);
                log_game(log_text, data);
                break;
        }
        return null;
    }
}
