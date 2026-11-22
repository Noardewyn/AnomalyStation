// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/human_say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A (docs/ai/specs/social.md). binarycheck (the binary channel of silicons and the cybernetic AI brain) is not declared: the
// silicons are after the core version.

public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_say.dm:3
    public override async DmTask<object> say(object message = null, object bubble_type = null, DmList spans = null, float? sanitize = null, object language = null, float? ignore_spam = null, object forced = null, float? filterproof = null, float? message_range = null, datum_saymode saymode = null, DmList message_mods = null)
    {
        if (!HAS_TRAIT(this, TRAIT_SPEAKS_CLEARLY))
        {
            if (Dm.textchar(message, 1) != "*")
            {
                message = human_say__tongueless_lower.Replace(message, Dm.pick("aa", "oo", "'"));
                message = human_say__tongueless_upper.Replace(message, Dm.pick("AA", "OO", "'"));
            }
        }
        return await base.say(message, bubble_type, spans, sanitize, language, ignore_spam, forced, filterproof, message_range, saymode, message_mods);
    }

    // tg: code/modules/mob/living/carbon/human/human_say.dm:17  var/static/regex/tongueless_lower = new("\[gdntke]+", "g")
    private static ref regex human_say__tongueless_lower => ref TgState.Current.mob_living_carbon_human__say__tongueless_lower;

    // tg: code/modules/mob/living/carbon/human/human_say.dm:18  var/static/regex/tongueless_upper = new("\[GDNTKE]+", "g")
    private static ref regex human_say__tongueless_upper => ref TgState.Current.mob_living_carbon_human__say__tongueless_upper;

    // tg: code/modules/mob/living/carbon/human/human_say.dm:24
    public override object get_default_say_verb()
    {
        obj_item_organ_tongue tongue = get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        if (tongue == null)
        {
            if (HAS_TRAIT(this, TRAIT_SIGN_LANG))
                return I18n.T("signs");
            return I18n.T("gurgles");
        }
        return (Dm.truthy(tongue.temp_say_mod) ? tongue.temp_say_mod : Dm.truthy(tongue.say_mod) ? tongue.say_mod : base.get_default_say_verb());
    }

    // tg: code/modules/mob/living/carbon/human/human_say.dm:32
    public override object get_voice(float? add_id_name = null)
    {
        add_id_name ??= FALSE;
        if (HAS_TRAIT(this, TRAIT_UNKNOWN_VOICE))
            return I18n.T("Unknown");
        object id_name = get_id_name("");
        if (HAS_TRAIT(this, TRAIT_VOICE_MATCHES_ID) && Dm.truthy(id_name))
            return id_name;
        if (Dm.truthy(override_voice))
            return override_voice;
        // DV173 (D56): a voice from a hidden or disfigured face is "Unknown"; tg speaks with the real name ("John Doe (as Unknown)")
        if (!Dm.truthy(get_face_name("")))
            return I18n.T("Unknown");
        if (Dm.truthy(add_id_name) && Dm.eq(real_name, id_name)) // Allows for "Captain John" to have the voice "Captain Join" and not "John"
            return get_id_name("", honorifics: TRUE);
        return real_name;
    }

    // tg: code/modules/mob/living/carbon/human/human_say.dm:44
    public override object get_message_voice(float? visible_name = null)
    {
        object dot = base.get_message_voice(visible_name);
        if (!Dm.eq(dot, name))
            dot = Dm.text("[] (as [])", dot, get_id_name("Unknown", honorifics: TRUE));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human_say.dm:63  (Poly has a copy of this, lazy bastard)
    public override async DmTask<object> radio__proc(object message = null, DmList message_mods = null, DmList spans = null, object language = null)
    {
        message_mods ??= Dm.list();
        object dot = await base.radio__proc(message, message_mods, spans, language);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.truthy(message_mods[MODE_HEADSET]))
        {
            if (Dm.truthy(ears))
                ears.talk_into(this, message, null, spans, language, message_mods);
            return Dm.bitor(ITALICS, REDUCE_RANGE);
        }
        else if (Dm.eq(message_mods[RADIO_EXTENSION], MODE_DEPARTMENT))
        {
            if (Dm.truthy(ears))
                ears.talk_into(this, message, message_mods[RADIO_EXTENSION], spans, language, message_mods);
            return Dm.bitor(ITALICS, REDUCE_RANGE);
        }
        else if (Dm.truthy(GLOB.default_radio_channels[message_mods[RADIO_EXTENSION]]))
        {
            if (Dm.truthy(ears))
            {
                ears.talk_into(this, message, message_mods[RADIO_EXTENSION], spans, language, message_mods);
                return Dm.bitor(ITALICS, REDUCE_RANGE);
            }
        }

        return FALSE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/human/human_say.dm:17-18  say's var/static/regex tongueless_lower and tongueless_upper
    internal regex mob_living_carbon_human__say__tongueless_lower = Dm.regex("[gdntke]+", "g");
    internal regex mob_living_carbon_human__say__tongueless_upper = Dm.regex("[GDNTKE]+", "g");
}
