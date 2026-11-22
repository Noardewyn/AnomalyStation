// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/toy_talk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A: the whole file (a held severed head speaks for its holder with .l or .r). The say log (log_talk) is the admin logs (M10).

/**
 * Allows people to talk via the item with .l or .r
 *
 * Be sure to override [/atom/movable/proc/GetVoice] if you want the item's "voice" to not default to itself
 */
// tg: code/datums/elements/toy_talk.dm:6
public partial class datum_element_toy_talk : datum_element
{
    // tg: code/datums/elements/toy_talk.dm:8
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ITEM_TALK_INTO, nameof(do_talk));
        return dot;
    }

    // tg: code/datums/elements/toy_talk.dm:15
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ITEM_TALK_INTO);
        return dot;
    }

    // tg: code/datums/elements/toy_talk.dm:19
    [SignalHandler]
    public virtual object do_talk(obj_item source = null, mob speaker = null, object message = null, object channel = null, DmList spans = null, object language = null, DmList message_mods = null)
    {
        if (!ismob(speaker) || Dm.truthy(message_mods[MODE_HEADSET]) || Dm.truthy(message_mods[MODE_RELAY]))
            return NONE;

        message_mods[MODE_RELAY] = TRUE; // Redundant (given NOPASS) but covers our bases
        // tg: :26  speaker.log_talk(message, LOG_SAY, tag = "toy talk ([source])"): the say log (the admin logs, M10)
        Waiting.NoSleep(source.say(message, language: language, sanitize: FALSE, message_mods: Dm.list(Dm.kv(MODE_RELAY, TRUE))));
        return NOPASS;
    }
}
