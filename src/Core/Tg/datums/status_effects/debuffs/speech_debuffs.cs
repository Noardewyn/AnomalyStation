// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/speech_debuffs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A: the whole file, the speech impediments (stutter, derpspeech, slurring) that treat_message applies to what a living
// mob says. The social anxiety quirk's mood (anxiety stutter) is the quirks' (get_quirk is a not-ported leaf, M8 quirks); the TTS
// filter rides with the message as tg's and is spoken by nothing until text to speech (D254). The var blocks are generated (D111).

// tg: code/datums/status_effects/debuffs/speech_debuffs.dm:1
public partial class datum_status_effect_speech : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:11
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:15
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_LIVING_TREAT_MESSAGE, nameof(handle_message));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:19
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_TREAT_MESSAGE);
        return null;
    }

    /**
     * Signal proc for [COMSIG_LIVING_TREAT_MESSAGE]
     *
     * Iterates over all of the characters in the passed message
     * and calls apply_speech() on each.
     */
    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:28
    [SignalHandler]
    public virtual void handle_message(datum source = null, DmList message_args = null)
    {
        string phrase = Dm.html_decode(message_args[TREAT_MESSAGE_ARG]);
        if (Dm.length(phrase) == 0)
            return;

        string final_phrase = "";
        DmList words = splittext(phrase, " ");
        DmList new_words = Dm.list();
        for (float i = 1; i <= length(words); i++)
            Dm.append(new_words, apply_speech(words[i], i));
        final_phrase = jointext(new_words, " ");

        if (final_phrase == phrase)
            return; // No change was done, whatever

        if (Dm.length(tts_filter) > 0)
            message_args[TREAT_TTS_FILTER_ARG] = Dm.add(message_args[TREAT_TTS_FILTER_ARG], tts_filter);
        if (Dm.truthy(make_tts_message_original))
            message_args[TREAT_TTS_MESSAGE_ARG] = message_args[TREAT_MESSAGE_ARG];

        message_args[TREAT_MESSAGE_ARG] = sanitize(final_phrase);
    }

    /**
     * Applies the speech effects on the past character, changing
     * the original_word into some modified_word
     *
     * Return the newly modified word
     */
    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:58
    public virtual object apply_speech(object original_word = null, float index = 0)
    {
        stack_trace(Dm.text("[] didn't implement apply_speech.", type), "code/datums/status_effects/debuffs/speech_debuffs.dm", 59);
        return original_word;
    }
}

// tg: code/datums/status_effects/debuffs/speech_debuffs.dm:62
public partial class datum_status_effect_speech_stutter : datum_status_effect_speech
{
    /// Regex to apply generically to stuttered words
    /// * 1st capture group is any leading invalid characters (can be empty)
    /// * 2nd capture group is either a digraph (th, qu, ch) or a consonant
    /// * 3rd capture group is the rest of the word (can be empty)
    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:80  VAR_FINAL/static/regex/stutter_regex
    public static ref regex stutter_regex => ref TgState.Current.datum_status_effect_speech_stutter__stutter_regex;

    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:82
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        stutter_regex ??= Dm.regex(@"^([\s""'()[\]{}.!?,:;_`~-]*\b)([^aeoiuh\d]h|qu|[^\d])(.*)", "i");
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:86
    public override object apply_speech(object original_word = null, float index = 0)
    {
        if (!prob(stutter_prob))
            return original_word;

        if (Dm.truthy(stutter_regex.Find(original_word)))
            return Dm.text("[][][]", stutter_regex.group[1], stutter_char(stutter_regex.group[2]), stutter_regex.group[3]);

        return original_word; // i give up
    }

    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:95
    public virtual object stutter_char(object some_char = null)
    {
        if (prob(four_char_chance))
            return Dm.text("[]-[]-[]-[]", some_char, some_char, some_char, some_char);
        if (prob(three_char_chance))
            return Dm.text("[]-[]-[]", some_char, some_char, some_char);
        if (prob(two_char_chance))
            return Dm.text("[]-[]", some_char, some_char);

        return some_char;
    }
}

// tg: code/datums/status_effects/debuffs/speech_debuffs.dm:105
public partial class datum_status_effect_speech_stutter_anxiety : datum_status_effect_speech_stutter
{
    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:113
    public override void handle_message(datum source = null, DmList message_args = null)
    {
        if (HAS_TRAIT(owner, TRAIT_FEARLESS) || HAS_TRAIT(owner, TRAIT_SIGN_LANG))
            stutter_prob = 0;
        else
        {
            // var/datum/quirk/social_anxiety/host_quirk = owner.get_quirk(/datum/quirk/social_anxiety): the quirks are not ported
            // (get_quirk is a not-ported leaf that finds none), so host_quirk?.calculate_mood_mod() is null
            owner.get_quirk(typeof(datum_quirk_social_anxiety));
            stutter_prob = Dm.clamp(0f * 0.5f, 5, 50);
        }
        base.handle_message(source, message_args);
    }
}

// tg: code/datums/status_effects/debuffs/speech_debuffs.dm:121
public partial class datum_status_effect_speech_stutter_obsession : datum_status_effect_speech_stutter
{
    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:130
    public override void handle_message(datum source = null, DmList message_args = null)
    {
        if (Dm.truthy(should_stutter()))
        {
            datum_brain_trauma_special_obsessed obsession_trauma = get_obsession() as datum_brain_trauma_special_obsessed;
            stutter_prob = Dm.initial(this, (datum_status_effect_speech_stutter_obsession p) => p.stutter_prob);
            four_char_chance = Dm.initial(this, (datum_status_effect_speech_stutter_obsession p) => p.four_char_chance) * Dm.max(1 - (obsession_trauma.time_spend_creeping / (10 * SECONDS)), 0.01f);
            three_char_chance = Dm.initial(this, (datum_status_effect_speech_stutter_obsession p) => p.three_char_chance) * Dm.max(1 - (obsession_trauma.time_spend_creeping / (20 * SECONDS)), 0.01f);
            two_char_chance = Dm.initial(this, (datum_status_effect_speech_stutter_obsession p) => p.two_char_chance) * Dm.max(1 - (obsession_trauma.time_spend_creeping / (40 * SECONDS)), 0.01f);
        }
        else
            stutter_prob = 0;
        base.handle_message(source, message_args);
    }

    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:141
    public override object apply_speech(object original_word = null, float index = 0)
    {
        object dot = base.apply_speech(original_word, index);
        if (Dm.eq(dot, original_word) || !Dm.truthy(should_stutter()))
            return dot;
        datum_brain_trauma_special_obsessed obsession_trauma = get_obsession() as datum_brain_trauma_special_obsessed;
        to_chat(owner, span_warning(I18n.T("Being near [] makes you nervous and stutter...", obsession_trauma.obsession.real_name)));
        stutter_cooldown = world.time + Dm.rand(4, 8) * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:149
    public virtual object get_obsession()
    {
        if (!ishuman(owner))
            return null;

        mob_living_carbon_human human_owner = owner as mob_living_carbon_human;
        return human_owner.has_trauma_type(typeof(datum_brain_trauma_special_obsessed));
    }

    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:156
    public virtual object should_stutter()
    {
        if (HAS_TRAIT(owner, TRAIT_FEARLESS))
            return FALSE;
        if (!(stutter_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline
            return FALSE;
        datum_brain_trauma_special_obsessed obsession_trauma = get_obsession() as datum_brain_trauma_special_obsessed;
        if (obsession_trauma == null || !Dm.truthy(obsession_trauma.viewing) || Dm.truthy(obsession_trauma.witnessed_death))
            return FALSE;
        return TRUE;
    }
}

// tg: code/datums/status_effects/debuffs/speech_debuffs.dm:167
public partial class datum_status_effect_speech_stutter_derpspeech : datum_status_effect_speech_stutter
{
    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:174
    public override void handle_message(datum source = null, DmList message_args = null)
    {
        object message = Dm.html_decode(message_args[TREAT_MESSAGE_ARG]);

        message = replacetext(message, " am ", " ");
        message = replacetext(message, " is ", " ");
        message = replacetext(message, " are ", " ");
        message = replacetext(message, "you", "u");
        message = replacetext(message, "help", "halp");
        message = replacetext(message, "grief", "grife");
        message = replacetext(message, "space", "spess");
        message = replacetext(message, "carp", "crap");
        message = replacetext(message, "reason", "raisin");

        if (prob(capitalize_prob))
        {
            object exclamation = pick("!", "!!", "!!!");
            message = uppertext(message);
            message = Dm.add(message, Dm.text("[]", stutter_char(exclamation)));
        }

        message_args[TREAT_MESSAGE_ARG] = message;

        mob_living living_source = source as mob_living;
        if (!isliving(source) || Dm.truthy(living_source.has_status_effect(typeof(datum_status_effect_speech_stutter))))
            return;

        // If we're not stuttering, we have a chance of calling parent here, adding stutter effects
        if (prob(message_stutter_prob))
        {
            base.handle_message(source, message_args);
            return;
        }

        // Otherwise just return and don't call parent, we already modified our speech
    }
}

// tg: code/datums/status_effects/debuffs/speech_debuffs.dm:205
public partial class datum_status_effect_speech_slurring : datum_status_effect_speech
{
    /// Regex for characters we won't apply any slurring to
    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:228  var/static/regex/no_slur
    public static ref regex no_slur => ref TgState.Current.datum_status_effect_speech_slurring__no_slur;

    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:233
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object dot = await base.on_creation(new_owner, __extra);
        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(text_modification_file))
            throw Dm.CRASH(Dm.text("[] was created without a text modification file.", type), "code/datums/status_effects/debuffs/speech_debuffs.dm", 239);

        DmList speech_changes = strings(text_modification_file, "replacements") as DmList;
        common_replacements = (speech_changes["characters"] as DmList)["common"] as DmList;
        uncommon_replacements = (speech_changes["characters"] as DmList)["uncommon"] as DmList;
        string_replacements = speech_changes["string_replacements"] as DmList;
        string_additions = speech_changes["string_additions"] as DmList;

        no_slur ??= Dm.regex(@"[ ""'()[\]{}.!?,:;_`~-]");
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:250
    public override object apply_speech(object original_word = null, float index = 0)
    {
        string original_char = "";
        string modified_word = "";
        for (float i = 1; i <= Dm.length(original_word); i += Dm.length(original_char))
        {
            original_char = Dm.textchar(original_word, i);
            modified_word += Dm.str(slur_character(original_char, i));
        }
        return modified_word;
    }

    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:258
    public virtual object slur_character(object original_char = null, float index = 0)
    {
        object modified_char = original_char;
        bool allow_slurring = index != 1 && !Dm.truthy(no_slur.Find(modified_char));
        object lower_char = LOWER_TEXT(modified_char);
        if (prob(common_prob) && Dm.@in(lower_char, common_replacements))
        {
            object to_replace = common_replacements[lower_char];
            modified_char = islist(to_replace) ? pick(to_replace as DmList) : to_replace;
            replacement_dupe_check = FALSE;
        }
        else if (prob(uncommon_prob) && Dm.@in(modified_char, uncommon_replacements))
        {
            object to_replace = uncommon_replacements[modified_char];
            modified_char = islist(to_replace) ? pick(to_replace as DmList) : to_replace;
            replacement_dupe_check = FALSE;
        }
        else if (allow_slurring) // Don't do replacements on the first character, or punctuation
        {
            if (!Dm.truthy(replacement_dupe_check) && prob(replacement_prob))
            {
                float replacements_len = length(string_replacements);
                float additions_len = length(string_additions);
                if (Dm.truthy(replacements_len) && Dm.truthy(additions_len))
                {
                    // Calculate the probability of grabbing a replacement vs an addition
                    float weight = (replacements_len + additions_len) / replacements_len * 100;
                    if (prob(weight))
                        modified_char = pick(string_replacements);
                    else
                        modified_char = Dm.add(modified_char, pick(string_additions));
                }
                else if (Dm.truthy(replacements_len))
                    modified_char = pick(string_replacements);
                else if (Dm.truthy(additions_len))
                    modified_char = Dm.add(modified_char, pick(string_additions));
                replacement_dupe_check = TRUE;
            }
            else if (prob(doubletext_prob))
            {
                modified_char = Dm.add(modified_char, Dm.text("[][]", modified_char, prob(50) ? "" : modified_char));
                replacement_dupe_check = FALSE;
            }
        }
        else
        {
            // If we don't allow replacements we don't want the next character to be a replacement either
            // This makes some more coherent speech by disallowing structures like "Y'''e"
            replacement_dupe_check = TRUE;
        }

        return modified_char;
    }
}

// tg: code/datums/status_effects/debuffs/speech_debuffs.dm:310
public partial class datum_status_effect_speech_slurring_drunk : datum_status_effect_speech_slurring
{
    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:319
    public override void handle_message(datum source = null, DmList message_args = null)
    {
        float current_drunkness = Dm.num(owner.get_drunk_amount());
        // These numbers are arbitarily picked
        // Common replacements start at about 20, and maxes out at about 85
        common_prob = Dm.clamp((current_drunkness * 0.8f) - 16, 4, 50);
        // Uncommon replacements (burping) start at 50 and max out at 110 (when you are dying)
        uncommon_prob = Dm.clamp((current_drunkness * 0.3f) - 10, 0, 12);
        // Replacements start at 20 and max out at about 60
        replacement_prob = Dm.clamp((current_drunkness * 0.6f) - 8, 0, 12);
        // Double texting start out at about 25 and max out at about 60
        doubletext_prob = Dm.clamp((current_drunkness * 0.8f) - 8, 2, 50);
        base.handle_message(source, message_args);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:80  VAR_FINAL/static/regex/stutter_regex
    internal regex datum_status_effect_speech_stutter__stutter_regex;

    // tg: code/datums/status_effects/debuffs/speech_debuffs.dm:228  var/static/regex/no_slur
    internal regex datum_status_effect_speech_slurring__no_slur;
}
