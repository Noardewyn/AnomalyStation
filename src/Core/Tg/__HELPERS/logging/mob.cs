// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/mob.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: log_mob_tag (wave 4, package C: /mob/Initialize calls it for every mob, mob.dm:101) and /mob/log_message (:14,
// D325: the package wrapper's attack log) and log_silicon (:9, silicons SI1, D326) translated.

// tg: code/__HELPERS/logging/mob.dm:6
public partial class mob : atom_movable
{
    /**
     * Logs a message to the mob_tags log, including the mobs tag
     * Arguments:
     * * text - text to log.
     */
    // tg: code/__HELPERS/logging/mob.dm:6
    public virtual void log_mob_tag(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_DEBUG_MOBTAG, text, data);
    }

    /// Logs a message in a mob's individual log, and in the global logs as well if log_globally is true
    // tg: code/__HELPERS/logging/mob.dm:14
    public override object log_message(object message = null, object message_type = null, object color = null, float? log_globally = null, DmList data = null)
    {
        log_globally ??= TRUE;
        if (!Dm.truthy(LAZYLEN(message)))
        {
            stack_trace("Empty message", "code/__HELPERS/logging/mob.dm", 16);
            return null;
        }

        // Cannot use the list as a map if the key is a number, so we stringify it (thank you BYOND)
        string smessage_type = num2text(Dm.num(message_type), MAX_BITFLAG_DIGITS);

        // a client always has its persistent client in tg; the port's harness clients (the toy rooms' joins) may have none, so the
        // persistent log is kept only when there is one
        if (client != null && persistent_client != null)
        {
            if (!islist(persistent_client.logging[smessage_type]))
                persistent_client.logging[smessage_type] = Dm.list();
        }

        if (!islist(logging[smessage_type]))
            logging[smessage_type] = Dm.list();

        object colored_message = message;
        if (Dm.truthy(color))
        {
            if (Dm.text("[]", color).StartsWith('#'))
                colored_message = Dm.text("<font color=[]>[]</font>", color, message);
            else
                colored_message = Dm.text("<font color='[]'>[]</font>", color, message);
        }

        //This makes readability a bit better for admins.
        switch ((int)Dm.num(message_type))
        {
            case LOG_WHISPER:
                colored_message = Dm.text("(WHISPER) []", colored_message);
                break;
            case LOG_OOC:
                colored_message = Dm.text("(OOC) []", colored_message);
                break;
            case LOG_ASAY:
                colored_message = Dm.text("(ASAY) []", colored_message);
                break;
            case LOG_EMOTE:
                colored_message = Dm.text("(EMOTE) []", colored_message);
                break;
            case LOG_RADIO_EMOTE:
                colored_message = Dm.text("(RADIOEMOTE) []", colored_message);
                break;
        }

        DmList timestamped_message = Dm.list();
        timestamped_message[Dm.text("\\[[]\\] [] [] (Event #[])", server_timestamp(format: "YYYY-MM-DD hh:mm:ss"), key_name_and_tag(this), loc_name(this), LAZYLEN(logging[smessage_type]))] = colored_message;

        Dm.append((DmList)logging[smessage_type], timestamped_message);

        if (client != null && persistent_client != null)
            Dm.append((DmList)persistent_client.logging[smessage_type], timestamped_message);

        return base.log_message(message, message_type, color, log_globally, data);
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/logging/mob.dm:9
    public static void log_silicon(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_SILICON, text, data);
    }
}
