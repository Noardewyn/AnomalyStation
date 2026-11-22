// Translated from tgstation (snapshot 1d99bd7f): code/modules/chatter/chatter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps TY1 (D333): the whole file. The chatter voices a line of speech as a string of short sounds, one per word, at the pace of its
// letters and punctuation (the owl and griffin action figures; the chattering tongues call it too). Its global regex walks over empty
// matches at the spaces, which the shim's regex moves past (dm-conformance.md, gaps TY1).

public static partial class GlobalProcs
{
    // tg: code/modules/chatter/chatter.dm:23  var/static/list/punctuation (a proc-level static, rules 10.6)
    private static ref DmList chatter_speak__punctuation => ref TgState.Current.GlobalProcs__chatter_speak__punctuation;

    // tg: code/modules/chatter/chatter.dm:1
    public static void chatter(object message = null, object phomeme = null, atom speaker = null)
    {
        // We want to transform any message into a list of numbers
        // and punctuation marks
        // For example:
        // "Hi." -> [2, '.']
        // "HALP GEROGE MELLONS, that swine, is GRIFFIN ME!"
        // -> [4, 6, 7, ',', 4, 5, ',', '2', 7, 2, '!']
        // "fuck,thissentenceissquashed" -> [4, ',', 21]

        regex R = Dm.regex("([\\l\\d]*)([^\\l\\d\\s])?", "g");
        DmList letter_count = Dm.list();
        while (!Dm.eq(R.Find(message), 0))
        {
            if (Dm.truthy(R.group[1]))
                Dm.append(ref letter_count, length(R.group[1]));
            if (Dm.truthy(R.group[2]))
                Dm.append(ref letter_count, R.group[2]);
        }
        chatter_speak(speaker, letter_count, phomeme);
    }

    /// We're going to take a list that dictates the pace of speech, and a sentence fragment to say
    /// Then say() that fragment at that pace
    /// You can pass in a starting delay to wait before speaking the next sound
    // tg: code/modules/chatter/chatter.dm:22
    public static void chatter_speak(atom speaker = null, DmList letter_count = null, object phomeme = null, float? extra_delay = null)
    {
        extra_delay ??= 0;
        float delay = Dm.num(extra_delay);
        for (float i = 1, i__end = Dm.length(letter_count); i <= i__end; i++)
        {
            object item = letter_count[i];
            if (Dm.@in(item, chatter_speak__punctuation))
            {
                // simulate pausing in talking
                // ignore semi-colons because of their use in HTML escaping
                if (Dm.@in(item, Dm.list(",", ":")))
                    delay += 0.3f * SECONDS;
                if (Dm.@in(item, Dm.list("!", "?", ".")))
                    delay += 0.6f * SECONDS;
                continue;
            }

            if (!isnum(item))
                continue;
            letter_count.Cut(1, i + 1);
            DmList current_context = letter_count;

            float length = Dm.num(min(item, 10));
            if (length == 0)
            {
                // "verbalise" long spaces
                delay += 0.1f * SECONDS;
            }

            if (delay != 0)
                _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.chatter_speak_word), speaker, letter_count, phomeme, length), delay, flags: TIMER_CLIENT_TIME, file: "code/modules/chatter/chatter.dm", line: 45);
            else
                chatter_speak_word(speaker, current_context, phomeme, length);
            break; //We use the loop as a handy way of dealing with punctuation, the actual looping operation here happens in timers that call timers
        }
    }

    // tg: code/modules/chatter/chatter.dm:52
    public static void chatter_speak_word(atom speaker = null, DmList letter_count = null, object phomeme = null, object length = null)
    {
        string path = Dm.text("sound/runtime/chatter/[]_[].ogg", phomeme, length);
        atom loc = speaker.loc;
        playsound(loc, path,
            vol: 40, vary: 0, extrarange: 3);

        object delay = Dm.mul(Dm.add(length, 1), chatter_get_delay_multiplier(phomeme));
        chatter_speak(speaker, letter_count, phomeme, Dm.num(delay));
    }

    // tg: code/modules/chatter/chatter.dm:61
    public static object chatter_get_delay_multiplier(object phomeme = null)
    {
        object dot = null;
        dot = 0.1f * SECONDS;
        object sw__1 = phomeme;
        if (Dm.eq(sw__1, "papyrus"))
            dot = 0.05f * SECONDS;
        else if (Dm.eq(sw__1, "griffin"))
            dot = 0.05f * SECONDS;
        else if (Dm.eq(sw__1, "sans"))
            dot = 0.07f * SECONDS;
        else if (Dm.eq(sw__1, "owl"))
            dot = 0.07f * SECONDS;
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/chatter/chatter.dm:23  var/static/list/punctuation = list(",",":",";",".","?","!","\'","-")
    internal DmList GlobalProcs__chatter_speak__punctuation = Dm.list(",", ":", ";", ".", "?", "!", "'", "-");
}
