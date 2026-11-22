// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/talk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the logs /atom/proc/log_message dispatches to (log_say, log_whisper, log_telecomms, log_speech_indicators;
// D325) and the speech-formatting helpers log_sayverb_talk, log_talk and log_directed_talk (silicons SI1, D326).

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/logging/talk.dm:45
    public static void log_say(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_SAY, text, data);
    }

    // tg: code/__HELPERS/logging/talk.dm:49
    public static void log_whisper(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_WHISPER, text, data);
    }

    // tg: code/__HELPERS/logging/talk.dm:63
    public static void log_telecomms(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_TELECOMMS, text, data);
    }

    // tg: code/__HELPERS/logging/talk.dm:67
    public static void log_speech_indicators(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_SPEECH_INDICATOR, text, data);
    }
}

// tg: code/__HELPERS/logging/talk.dm:15
public partial class atom
{
    /**
     * Helper for logging chat messages that may or may not have a custom say verb,
     * or be a pure radio emote outright.
     *
     * This proc reads the `message_mods` to determine
     * in what ways the given message should be logged,
     * and forwards it to other logging procs as such.
     * Arguments:
     * * message - The message being logged
     * * message_mods - A list of message modifiers, i.e. whispering/singing.
     * * tag - tag that indicates the type of text(announcement, telepathy, etc)
     * * log_globally - boolean checking whether or not we write this log to the log file
     * * forced_by - source that forced the dialogue if any
     */
    // tg: code/__HELPERS/logging/talk.dm:15
    public virtual void log_sayverb_talk(object message = null, DmList message_mods = null, object tag = null, float? log_globally = null, object forced_by = null)
    {
        message_mods ??= Dm.list();
        log_globally ??= TRUE;
        // If it's just the custom say verb, log it to emotes.
        if (Dm.truthy(message_mods[MODE_CUSTOM_SAY_ERASE_INPUT]))
        {
            log_talk(message_mods[MODE_CUSTOM_SAY_EMOTE], LOG_RADIO_EMOTE, tag, log_globally, forced_by);
            return;
        }

        if (Dm.truthy(message_mods[WHISPER_MODE]))
            log_talk(message, LOG_WHISPER, tag, log_globally, forced_by, message_mods[MODE_CUSTOM_SAY_EMOTE]);
        else
            log_talk(message, LOG_SAY, tag, log_globally, forced_by, message_mods[MODE_CUSTOM_SAY_EMOTE]);
    }

    /**
     * Helper for logging chat messages or other logs with arbitrary inputs (e.g. announcements)
     *
     * This proc compiles a log string by prefixing the tag to the message
     * and suffixing what it was forced_by if anything
     * if the message lacks a tag and suffix then it is logged on its own
     * Arguments:
     * * message - The message being logged
     * * message_type - the type of log the message is(ATTACK, SAY, etc)
     * * tag - tag that indicates the type of text(announcement, telepathy, etc)
     * * log_globally - boolean checking whether or not we write this log to the log file
     * * forced_by - source that forced the dialogue if any
     */
    // tg: code/__HELPERS/logging/talk.dm:39
    public virtual void log_talk(object message = null, object message_type = null, object tag = null, float? log_globally = null, object forced_by = null, object custom_say_emote = null)
    {
        log_globally ??= TRUE;
        string prefix = Dm.truthy(tag) ? Dm.text("([]) ", tag) : "";
        string suffix = Dm.truthy(forced_by) ? Dm.text(" FORCED by []", forced_by) : "";
        log_message(Dm.text("[][]\"[]\"[]", prefix, Dm.truthy(custom_say_emote) ? Dm.text("*[]*, ", custom_say_emote) : "", message, suffix), message_type, log_globally: log_globally);
    }
}

public static partial class GlobalProcs
{
    /// Helper for logging of messages with only one sender and receiver (i.e. mind links)
    // tg: code/__HELPERS/logging/talk.dm:53
    public static void log_directed_talk(atom source = null, atom target = null, object message = null, object message_type = null, object tag = null)
    {
        if (!Dm.truthy(tag))
        {
            stack_trace("Unspecified tag for private message", "code/__HELPERS/logging/talk.dm", 55);
            tag = "UNKNOWN";
        }

        source.log_talk(message, message_type, tag: Dm.text("[] to []", tag, key_name(target)));
        if (source != target)
            target.log_talk(message, LOG_VICTIM, tag: Dm.text("[] from []", tag, key_name(source)), log_globally: FALSE);
    }
}
