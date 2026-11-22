// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/voice.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (D307): the voice and its pitch. Not translated: the blip base and number (tg's blips, the sound of a line a listener does
// not understand, are not ported: such a line is voiced scrambled, DV262).

/// TTS voice preference
// tg: code/modules/client/preferences/voice.dm:2
[DmPath("/datum/preference/choiced/voice")]
public partial class datum_preference_choiced_voice : datum_preference_choiced
{
    // tg: code/modules/client/preferences/voice.dm:9
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(SStts?.tts_enabled))
            return FALSE;
        return base.is_accessible(preferences);
    }

    // tg: code/modules/client/preferences/voice.dm:14
    public override DmList init_possible_values()
    {
        if (Dm.truthy(SStts?.tts_enabled))
            return SStts.available_speakers;
        // tg: :17-23  data/cached_tts_voices.json, the last TTS server's voices: the port's are always there (DV262)
        return Dm.list("invalid");
    }

    // tg: code/modules/client/preferences/voice.dm:25
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        if (Dm.truthy(SStts?.tts_enabled) && (!Dm.@in(value, SStts.available_speakers) || !datum_controller_subsystem_tts.fits_gender(value, target.gender)))
            value = SStts.random_tts_voice(target.gender); // As a failsafe
        target.voice = value;
    }

    // DEVIATION(D311, the owner 2026-11-03): a female character speaks with a woman's voice, a male one with a man's, any other (or a
    // species without sexes, which tg makes plural) with any; a saved voice that does not fit is replaced by a fitting one
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        object value = base.deserialize(input, preferences);
        if (preferences == null || !Dm.truthy(SStts?.tts_enabled))
            return value;
        object gender = character_gender(preferences);
        return datum_controller_subsystem_tts.fits_gender(value, gender) ? value : SStts.random_tts_voice(gender);
    }

    /// D311: the gender the character will have (gender.dm's apply_to_human: plural on a species without sexes).
    public static object character_gender(datum_preferences preferences)
    {
        Type species = preferences.read_preference(typeof(datum_preference_choiced_species)) as Type;
        if (species != null && !Dm.truthy(Dm.initial(species, (datum_species p) => p.sexes)))
            return PLURAL;
        return preferences.read_preference(typeof(datum_preference_choiced_gender));
    }
}

// tg: code/modules/client/preferences/voice.dm:30
[DmPath("/datum/preference/numeric/tts_voice_pitch")]
public partial class datum_preference_numeric_tts_voice_pitch : datum_preference_numeric
{
    // DEVIATION(D311, the owner 2026-11-03): -5 to 5 semitones, not tg's -12 to 12 (the far ends distort the port's voices)
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        minimum = -5;
        maximum = 5;
    }

    // tg: code/modules/client/preferences/voice.dm:38
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(SStts?.tts_enabled) || !Dm.truthy(SStts?.pitch_enabled))
            return FALSE;
        return base.is_accessible(preferences);
    }

    // tg: code/modules/client/preferences/voice.dm:43
    public override object create_default_value()
    {
        return 0;
    }

    // tg: code/modules/client/preferences/voice.dm:46
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        if (Dm.truthy(SStts?.tts_enabled) && Dm.truthy(SStts?.pitch_enabled))
            target.pitch = Dm.num(value);
    }
}
