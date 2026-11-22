// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/speech.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the speech mutations: nervousness, wacky, heckacious larincks, mute, unintelligible, swedish, chav,
// elvis, stoner, medieval and pig latin. The var blocks are generated (D111).

// tg: code/datums/mutations/speech.dm:6
public partial class datum_mutation_nervousness : datum_mutation
{
    // tg: code/datums/mutations/speech.dm:13
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (SPT_PROB(5, seconds_per_tick))
            await owner.set_stutter_if_lower(20 * SECONDS);
    }
}

// tg: code/datums/mutations/speech.dm:17
public partial class datum_mutation_wacky : datum_mutation
{
    // tg: code/datums/mutations/speech.dm:25
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(owner, COMSIG_MOB_SAY, nameof(handle_speech));
        return dot;
    }

    // tg: code/datums/mutations/speech.dm:31
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        UnregisterSignal(owner, COMSIG_MOB_SAY);
        return dot;
    }

    // tg: code/datums/mutations/speech.dm:36
    [SignalHandler]
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        DmList spans = speech_args[SPEECH_SPANS] as DmList;
        Dm.combine(ref spans, SPAN_SANS);
        speech_args[SPEECH_SPANS] = spans;
    }
}

// tg: code/datums/mutations/speech.dm:41
public partial class datum_mutation_heckacious : datum_mutation
{
    // tg: code/datums/mutations/speech.dm:84  var/static/list/super_wacky_words (a proc-level static, rules 10.6)
    private static DmList super_wacky_words => TgState.Current.datum_mutation_heckacious__handle_speech__super_wacky_words ??= strings("heckacious.json", "heckacious") as DmList;

    // tg: code/datums/mutations/speech.dm:49
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(owner, COMSIG_LIVING_TREAT_MESSAGE, nameof(handle_caps));
        RegisterSignal(owner, COMSIG_MOB_SAY, nameof(handle_speech));
        return dot;
    }

    // tg: code/datums/mutations/speech.dm:56
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        UnregisterSignal(owner, Dm.list(COMSIG_LIVING_TREAT_MESSAGE, COMSIG_MOB_SAY));
        return dot;
    }

    // tg: code/datums/mutations/speech.dm:61
    [SignalHandler]
    public virtual void handle_caps(atom_movable source = null, DmList message_args = null)
    {
        message_args[TREAT_CAPITALIZE_MESSAGE] = FALSE;
    }

    // tg: code/datums/mutations/speech.dm:65
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        object message = speech_args[SPEECH_MESSAGE];
        if (!Dm.truthy(message))
            return;
        // Split for swapping purposes
        message = Dm.text(" [] ", message);

        // Splitting up each word in the text to manually apply our intended changes
        DmList message_words = splittext(message, " ");
        // What we use in the end
        DmList edited_message_words = null;

        foreach (object word in Dm.each(message_words))
        {
            string editing_word = Dm.str(word);
            if (editing_word == " " || editing_word == "")
                continue;
            // Used to replace the original later
            string og_word = editing_word;
            // Iterating through each replaceable-string in the .json

            // If the word doesn't get replaced we might do something with it later
            bool word_edited = false;
            foreach (object key in Dm.each(super_wacky_words))
            {
                object value = super_wacky_words[key];
                // If list, pick one value from said list
                if (value is DmList options)
                    value = pick(options);
                editing_word = replacetextEx(editing_word, Dm.text("[]", uppertext(key)), Dm.text("[]", uppertext(value)));
                editing_word = replacetextEx(editing_word, Dm.text("[]", capitalize(key)), Dm.text("[]", capitalize(value)));
                editing_word = replacetextEx(editing_word, Dm.text("[]", key), Dm.text("[]", value));
                // Enable if we actually found something to change
                if (editing_word != og_word)
                    word_edited = true;
            }

            // Random caps
            if (prob(10))
                editing_word = prob(85) ? Dm.str(uppertext(editing_word)) : LOWER_TEXT(editing_word);
            // some times....... we add DOTS...
            if (prob(10))
            {
                for (float dotnum = 1, dotnum__end = rand(2, 8); dotnum <= dotnum__end; dotnum++)
                    editing_word += ".";
            }
            // change for bold/italics/underline as well!
            if (prob(10))
            {
                string extra_emphasis = Dm.str(pick("+", "_", "|"));
                editing_word = extra_emphasis + editing_word + extra_emphasis;
            }

            // If no replacement we do it manually
            if (!word_edited)
            {
                if (prob(65))
                    editing_word = replacetext(editing_word, pick(VOWELS()), pick(VOWELS()));
                // Many more consonants, double it!
                for (float i = 1, i__end = rand(1, 2); i <= i__end; i++)
                    editing_word = replacetext(editing_word, pick(CONSONANTS()), pick(CONSONANTS()));
                // rarely, lettter is DOUBBLED...
                string patchword = "";
                for (float letter = 1, letter__end = length(editing_word); letter <= letter__end; letter++)
                {
                    if (prob(92))
                    {
                        patchword += textchar(editing_word, letter);
                        continue;
                    }
                    patchword += replacetext(textchar(editing_word, letter), "", textchar(editing_word, letter) + textchar(editing_word, letter));
                }
                editing_word = patchword;
            }

            LAZYADD(ref edited_message_words, editing_word);
        }

        string edited_message = jointext(edited_message_words, " ");

        message = trim(edited_message);

        speech_args[SPEECH_MESSAGE] = message;
    }
}

// tg: code/datums/mutations/speech.dm:136
public partial class datum_mutation_mute : datum_mutation
{
    // tg: code/datums/mutations/speech.dm:144
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_MUTE, GENETIC_MUTATION);
        return dot;
    }

    // tg: code/datums/mutations/speech.dm:150
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        REMOVE_TRAIT(owner, TRAIT_MUTE, GENETIC_MUTATION);
        return dot;
    }
}

// tg: code/datums/mutations/speech.dm:155
public partial class datum_mutation_unintelligible : datum_mutation
{
    // tg: code/datums/mutations/speech.dm:163
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_UNINTELLIGIBLE_SPEECH, GENETIC_MUTATION);
        return dot;
    }

    // tg: code/datums/mutations/speech.dm:169
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        REMOVE_TRAIT(owner, TRAIT_UNINTELLIGIBLE_SPEECH, GENETIC_MUTATION);
        return dot;
    }
}

// tg: code/datums/mutations/speech.dm:174
public partial class datum_mutation_swedish : datum_mutation
{
    // tg: code/datums/mutations/speech.dm:182  var/static/list/language_mutilation (rules 10.6)
    private static DmList language_mutilation => TgState.Current.datum_mutation_swedish__language_mutilation ??= Dm.list(
        Dm.kv("w", "v"), Dm.kv("j", "y"), Dm.kv("bo", "bjo"), Dm.kv("a", Dm.list("å", "ä", "æ", "a")), Dm.kv("o", Dm.list("ö", "ø", "o")));

    // tg: code/datums/mutations/speech.dm:184
    [DmParams("copymut")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        await AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", language_mutilation), Dm.named("end_string", Dm.list("", ", bork", ", bork, bork")), Dm.named("end_string_chance", 30));
    }
}

// tg: code/datums/mutations/speech.dm:188
public partial class datum_mutation_chav : datum_mutation
{
    // tg: code/datums/mutations/speech.dm:197
    [DmParams("copymut")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        await AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", strings("chav_replacement.json", "chav")), Dm.named("end_string", ", mate"), Dm.named("end_string_chance", 30));
    }
}

// tg: code/datums/mutations/speech.dm:201
public partial class datum_mutation_elvis : datum_mutation
{
    // tg: code/datums/mutations/speech.dm:210
    [DmParams("copymut")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        await AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", strings("elvis_replacement.json", "elvis")));
    }

    // tg: code/datums/mutations/speech.dm:214
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await DmTask.CompletedTask;
        switch (Dm.num(pick(1, 2)))
        {
            case 1:
                if (SPT_PROB(7.5f, seconds_per_tick))
                {
                    DmList dancetypes = Dm.list("swinging", "fancy", "stylish", "20'th century", "jivin'", "rock and roller", "cool", "salacious", "bashing", "smashing");
                    object dancemoves = pick(dancetypes);
                    owner.visible_message(I18n.T("<b>[]</b> busts out some [] moves!", owner, dancemoves));
                }
                break;
            case 2:
                if (SPT_PROB(7.5f, seconds_per_tick))
                    owner.visible_message(I18n.T("<b>[]</b> []!", owner, pick(I18n.T("jiggles their hips"), I18n.T("rotates their hips"), I18n.T("gyrates their hips"), I18n.T("taps their foot"), I18n.T("dances to an imaginary song"), I18n.T("jiggles their legs"), I18n.T("snaps their fingers"))));
                break;
        }
    }
}

// tg: code/datums/mutations/speech.dm:225
public partial class datum_mutation_stoner : datum_mutation
{
    // tg: code/datums/mutations/speech.dm:232
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        owner.grant_language(typeof(datum_language_beachbum), source: LANGUAGE_STONER);
        owner.add_blocked_language(Dm.sub(subtypesof(typeof(datum_language)), typeof(datum_language_beachbum)), source: LANGUAGE_STONER);
        return dot;
    }

    // tg: code/datums/mutations/speech.dm:239
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        owner.remove_language(typeof(datum_language_beachbum), source: LANGUAGE_STONER);
        owner.remove_blocked_language(Dm.sub(subtypesof(typeof(datum_language)), typeof(datum_language_beachbum)), source: LANGUAGE_STONER);
        return null;
    }
}

// tg: code/datums/mutations/speech.dm:244
public partial class datum_mutation_medieval : datum_mutation
{
    // tg: code/datums/mutations/speech.dm:253
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(owner, COMSIG_MOB_SAY, nameof(handle_speech));
        return dot;
    }

    // tg: code/datums/mutations/speech.dm:259
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        UnregisterSignal(owner, COMSIG_MOB_SAY);
        return dot;
    }

    // tg: code/datums/mutations/speech.dm:264
    [SignalHandler]
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        object message = speech_args[SPEECH_MESSAGE];
        if (Dm.truthy(message))
        {
            message = Dm.text(" [] ", message);
            DmList medieval_words = strings("medieval_replacement.json", "medieval") as DmList;
            DmList startings = strings("medieval_replacement.json", "startings") as DmList;
            foreach (object key in Dm.each(medieval_words))
            {
                object value = medieval_words[key];
                if (value is DmList options)
                    value = pick(options);
                if (Dm.eq(uppertext(key), key))
                    value = uppertext(value);
                if (Dm.eq(capitalize(key), key))
                    value = capitalize(value);
                // replacetextEx(message, regex(...), value): a /regex needle is the regex's Replace (BYOND ref)
                message = regex(Dm.text("\\b[]\\b", REGEX_QUOTE(key)), "ig").Replace(message, value);
            }
            message = trim(message);
            object chosen_starting = pick(startings);
            message = Dm.text("[] []", chosen_starting, message);

            speech_args[SPEECH_MESSAGE] = message;
        }
    }
}

// tg: code/datums/mutations/speech.dm:287
public partial class datum_mutation_piglatin : datum_mutation
{
    // tg: code/datums/mutations/speech.dm:296
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(owner, COMSIG_MOB_SAY, nameof(handle_speech));
        return dot;
    }

    // tg: code/datums/mutations/speech.dm:302
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        UnregisterSignal(owner, COMSIG_MOB_SAY);
        return dot;
    }

    // tg: code/datums/mutations/speech.dm:307
    [SignalHandler]
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        object spoken_message = speech_args[SPEECH_MESSAGE];
        spoken_message = piglatin_sentence(spoken_message);
        speech_args[SPEECH_MESSAGE] = spoken_message;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/mutations/speech.dm:84  var/static/list/super_wacky_words = strings("heckacious.json", "heckacious")
    internal DmList datum_mutation_heckacious__handle_speech__super_wacky_words;
    // tg: code/datums/mutations/speech.dm:182  var/static/list/language_mutilation
    internal DmList datum_mutation_swedish__language_mutilation;
}
