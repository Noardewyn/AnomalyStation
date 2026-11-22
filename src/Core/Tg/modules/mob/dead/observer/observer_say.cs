// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/observer/observer_say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A (docs/ai/specs/social.md): a ghost hears the living (Hear) and speaks in dead chat (say ends in say_dead, the ghosts beyond
// D151's scope, not-translated.md section 16). The admin radio keys of a ghost's say are the admin tools (M10): holder is never set.
// The OOC word filters match nothing (the port has no word filter config, see ooc.cs). FOLLOW_LINK, the "(F)" link in front of
// what a ghost hears, orbits the speaker: orbiting is outside D151's scope (not-translated.md section 16), so the line has no link.

public partial class mob_dead_observer : mob_dead
{
    // tg: code/modules/mob/dead/observer/observer_say.dm:1
    public override object check_emote(object message = null, object forced = null)
    {
        return Waiting.Detach(emote(Dm.copytext(message, Dm.length(Dm.textchar(message, 1)) + 1), intentional: Dm.truthy(forced) ? FALSE : TRUE, force_silence: TRUE));
    }

    //Modified version of get_message_mods, removes the trimming, the only thing we care about here is admin channels
    // tg: code/modules/mob/dead/observer/observer_say.dm:5
    public override object get_message_mods(object message = null, DmList mods = null)
    {
        string key = Dm.textchar(message, 1);
        if (Dm.@in(key, GLOB.department_radio_prefixes) && Dm.length(message) > Dm.length(key) + 1 && !Dm.truthy(mods[RADIO_EXTENSION]))
        {
            mods[RADIO_KEY] = LOWER_TEXT(Dm.textchar(message, 1 + Dm.length(key)));
            mods[RADIO_EXTENSION] = GLOB.department_radio_keys[mods[RADIO_KEY]];
        }
        return message;
    }

    // tg: code/modules/mob/dead/observer/observer_say.dm:12
    public override async DmTask<object> say(object message = null, object bubble_type = null, DmList spans = null, float? sanitize = null, object language = null, float? ignore_spam = null, object forced = null, float? filterproof = null, float? message_range = null, datum_saymode saymode = null, DmList message_mods = null)
    {
        message_mods ??= Dm.list();
        message = trim(message); //trim now and sanitize after checking for special admin radio keys

        // tg: :27-38  is_ooc_filtered and is_soft_ooc_filtered: the port has no word filter config (see the file comment)

        if (!Dm.truthy(message))
            return null;
        message = get_message_mods(message, message_mods);
        if (client?.holder != null && (Dm.eq(message_mods[RADIO_EXTENSION], MODE_ADMIN) || Dm.eq(message_mods[RADIO_EXTENSION], MODE_DEADMIN) || (Dm.eq(message_mods[RADIO_EXTENSION], MODE_PUPPET) && mind?.current != null)))
        {
            // tg: :42-51  asay, dsay and a puppet's body: the admin tools (M10)
            Dm.Deferred("code/modules/mob/dead/observer/observer_say.dm:42", "say: a ghost admin's asay, dsay and puppet keys (the admin tools, M10+)");
            return null;
        }

        message = copytext_char(GlobalProcs.sanitize(message), 1, MAX_MESSAGE_LEN);
        if (Dm.textchar(message, 1) == "*" && Dm.truthy(check_emote(message, forced)))
            return null;

        return say_dead(message);
    }

    // tg: code/modules/mob/dead/observer/observer_say.dm:58
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        message_mods ??= Dm.list();
        await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        // tg: :60-68  to_follow and FOLLOW_LINK(src, to_follow): orbiting is outside D151's scope (see the file comment)
        object is_custom_emote = message_mods[MODE_CUSTOM_SAY_ERASE_INPUT];
        // Create map text prior to modifying message for goonchat
        if (Dm.truthy(safe_read_pref(client, typeof(datum_preference_toggle_enable_runechat))) && (Dm.truthy(safe_read_pref(client, typeof(datum_preference_toggle_enable_runechat_non_mobs))) || ismob(speaker)))
        {
            if (Dm.truthy(is_custom_emote))
                create_chat_message(speaker, null, message_mods[MODE_CUSTOM_SAY_EMOTE], spans, EMOTE_MESSAGE);
            else
                create_chat_message(speaker, message_language, raw_message, spans);
        }
        // Recompose the message, because it's scrambled by default
        object message = await compose_message(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods);
        to_chat(this, html: Dm.text("[]", message), avoid_highlighting: speaker == this ? TRUE : FALSE);
        return Dm.bitor(HEAR_HEARD, HEAR_UNDERSTOOD);
    }
}
