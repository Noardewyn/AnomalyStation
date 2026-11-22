// Translated from tgstation (snapshot 1d99bd7f): code/modules/language/_language.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but vv_edit_var (view variables, admin tooling, M10+): the vars, display_icon_type (M9 slice A: speech), the random names
// (default_name, get_random_name, get_random_unique_name; M2 F1: a human's random name, gaps L2: the syllable names) and the scrambling with
// its word and sentence caches (C5: a monkey's speech).

/// Datum based languages. Easily editable and modular.
// tg: code/modules/language/_language.dm:7
[DmPath("/datum/language")]
public partial class datum_language : datum
{
    // tg: code/modules/language/_language.dm:8
    public datum_language()
    {
        abstract_type = typeof(datum_language);
    }

    /// Fluff name of language if any.
    // tg: code/modules/language/_language.dm:10
    public string name = "an unknown language";
    /// Short description for 'Check Languages'.
    // tg: code/modules/language/_language.dm:12
    public string desc = "A language.";
    /// Character used to speak in language
    /// If `null`, the language cannot be spoken with a key, requires being set as a default language or forced speech.
    // tg: code/modules/language/_language.dm:15
    public object key;
    /// Various language flags.
    // tg: code/modules/language/_language.dm:17
    public float flags = NONE;
    /// Used when scrambling text for a non-speaker.
    // tg: code/modules/language/_language.dm:19
    public DmList syllables;
    /// List of characters that will randomly be inserted between syllables.
    // tg: code/modules/language/_language.dm:21
    public DmList special_characters;

    // These modify how syllables are combined.
    /// Likelihood of making a new sentence after each syllable.
    // tg: code/modules/language/_language.dm:25
    public float sentence_chance = 2;
    /// Likelihood of making a new sentence after each word.
    // tg: code/modules/language/_language.dm:27
    public float between_word_sentence_chance = 0;
    /// Likelihood of adding a space between syllables.
    // tg: code/modules/language/_language.dm:29
    public float space_chance = 20;
    /// Likelyhood of adding a space between words.
    // tg: code/modules/language/_language.dm:31
    public float between_word_space_chance = 100;
    /// Scramble word interprets the word as this much longer than it really is (low end)
    /// You can set this to an arbitarily large negative number to make all words only one syllable.
    // tg: code/modules/language/_language.dm:34
    public float additional_syllable_low = -1;
    /// Scramble word interprets the word as this much longer than it really is (high end)
    /// You can set this to an arbitarily large negative number to make all words only one syllable.
    // tg: code/modules/language/_language.dm:37
    public float additional_syllable_high = 3;

    /// Spans to apply from this language
    // tg: code/modules/language/_language.dm:40
    public DmList spans;
    /**
     * Cache of recently scrambled text
     * This allows commonly reused words to not require a full re-scramble every time.
     * Is limited to the last SCRAMBLE_CACHE_LEN words spoken. After surpassing this limit,
     * the oldest word will be removed from the cache and rescrambled if spoken again.
     *
     * Case insensitive, punctuation insensitive.
     */
    // tg: code/modules/language/_language.dm:49
    [DmPrivate]
    public DmList scramble_cache;
    /**
     * Scramble cache, but for the 1000 most common words in the English language.
     * These are never rescrambled, so they will consistently be the same thing.
     *
     * Case insensitive, punctuation insensitive.
     */
    // tg: code/modules/language/_language.dm:56
    [DmPrivate]
    public DmList most_common_cache;
    /**
     * Cache of recently spoken sentences
     * So if one person speaks over the radio, everyone hears the same thing.
     *
     * This is an assoc list [sentence] = [key, scrambled_text]
     * Where key is a string that is used to determine context about the listener (like what languages they know)
     *
     * Case sensitive, punctuation sensitive.
     */
    // tg: code/modules/language/_language.dm:66
    [DmPrivate]
    public DmList last_sentence_cache;

    /// The language that an atom knows with the highest "default_priority" is selected by default.
    // tg: code/modules/language/_language.dm:69
    public float default_priority = 0;
    /// If TRUE, when generating names, we will always use the default human namelist, even if we have syllables set.
    /// This is to be used for languages with very outlandish syllable lists (like pirates).
    // tg: code/modules/language/_language.dm:72
    public float always_use_default_namelist = FALSE;
    /// Icon displayed in the chat window when speaking this language.
    /// if you are seeing someone speak popcorn language, then something is wrong.
    // tg: code/modules/language/_language.dm:75
    public object icon = Dm.resource("icons/ui/chat/language.dmi");
    /// Icon state displayed in the chat window when speaking this language.
    // tg: code/modules/language/_language.dm:77
    public string icon_state = "unknown";

    /// By default, random names picks this many names
    // tg: code/modules/language/_language.dm:80
    public float default_name_count = 2;
    /// By default, random names picks this many syllables (min)
    // tg: code/modules/language/_language.dm:82
    public float default_name_syllable_min = 2;
    /// By default, random names picks this many syllables (max)
    // tg: code/modules/language/_language.dm:84
    public float default_name_syllable_max = 4;
    /// What char to place in between randomly generated names
    // tg: code/modules/language/_language.dm:86
    public string random_name_spacer = " ";

    /**
     * Assoc Lazylist of other language types that would have a degree of mutual understanding with this language.
     * For example, you could do `list(/datum/language/common = 50)` to say that this language has a 50% chance to understand common words
     * And yeah if you give a 100% chance, they can basically just understand the language.
     * Not sure why you would do that though.
     */
    // tg: code/modules/language/_language.dm:94
    public DmList mutual_understanding;

    // tg: code/modules/language/_language.dm:49, :56, :66  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        scramble_cache = Dm.list();
        most_common_cache = Dm.list();
        last_sentence_cache = Dm.list();
    }

    /// Returns the display icon type a hearer sees for this language (DISPLAY_LANGUAGE_ICON_NONE, _PARTIAL or _FULL)
    // tg: code/modules/language/_language.dm:115
    public virtual object display_icon_type(atom_movable hearer = null, DmList message_mods = null)
    {
        if (Dm.truthy(hearer.has_language(type)))
        {
            if (Dm.truthy(Dm.bitand(flags, LANGUAGE_HIDE_ICON_IF_UNDERSTOOD)))
                return DISPLAY_LANGUAGE_ICON_NONE;
        }
        else
        {
            if (Dm.truthy(hearer.has_partial_language(type)) || Dm.truthy(LAZYACCESSASSOC(message_mods, LANGUAGE_MUTUAL_BONUS, type)))
                return DISPLAY_LANGUAGE_ICON_PARTIAL;
            if (Dm.truthy(Dm.bitand(flags, LANGUAGE_HIDE_ICON_IF_NOT_UNDERSTOOD)))
                return DISPLAY_LANGUAGE_ICON_NONE;
        }

        return DISPLAY_LANGUAGE_ICON_FULL;
    }

    /// Simple helper for getting a default firstname lastname
    // tg: code/modules/language/_language.dm:129
    public virtual object default_name(string gender = null)
    {
        gender ??= NEUTER;
        if (gender != MALE && gender != FEMALE)
            gender = (string)pick(MALE, FEMALE);
        if (gender == FEMALE)
            return Dm.add(Dm.add(capitalize(pick(GLOB.first_names_female)), " "), capitalize(pick(GLOB.last_names)));
        return Dm.add(Dm.add(capitalize(pick(GLOB.first_names_male)), " "), capitalize(pick(GLOB.last_names)));
    }

    /**
     * Generates a random name this language would use.
     *
     * * gender: What gender to generate from, if neuter / plural coin flips between male and female
     * * name_count: How many names to generate in, by default 2, for firstname lastname
     * * syllable_count: How many syllables to generate in each name, min
     * * syllable_max: How many syllables to generate in each name, max
     * * force_use_syllables: If the name should be generated from the syllables list.
     * Only used for subtypes which implement custom name lists. Also requires the language has syllables set.
     */
    // tg: code/modules/language/_language.dm:147
    public virtual object get_random_name(string gender = null, object name_count = null, object syllable_min = null, object syllable_max = null, float? force_use_syllables = null, params object[] __extra)
    {
        gender ??= NEUTER;
        name_count ??= default_name_count;
        syllable_min ??= default_name_syllable_min;
        syllable_max ??= default_name_syllable_max;
        force_use_syllables ??= FALSE;
        if (gender != MALE && gender != FEMALE)
            gender = (string)pick(MALE, FEMALE);
        if (!Dm.truthy(length(syllables)) || Dm.truthy(always_use_default_namelist))
            return default_name(gender);

        DmList full_name = Dm.list();
        for (float i = 1, i__end = Dm.num(name_count); i <= i__end; i++)
        {
            object new_name = "";
            for (float j = 1, j__end = Dm.num(rand(default_name_syllable_min, default_name_syllable_max)); j <= j__end; j++)
                Dm.append(ref new_name, pick_weight_recursive(syllables));
            Dm.append(full_name, capitalize(LOWER_TEXT(new_name)));
        }

        return jointext(full_name, random_name_spacer);
    }

    /// Generates a random name, and attempts to ensure it is unique (IE, no other mob in the world has it)
    // tg: code/modules/language/_language.dm:169
    public virtual object get_random_unique_name(params object[] __extra)
    {
        DmList args = Dm.args(__extra);
        object result = Waiting.NoSleep(Dm.Call(this, nameof(get_random_name), Dm.arglist(args)));
        for (float i = 1, i__end = 10; i <= i__end; i++)
        {
            if (!Dm.truthy(findname(result)))
                break;
            result = Waiting.NoSleep(Dm.Call(this, nameof(get_random_name), Dm.arglist(args)));
        }

        return result;
    }
}

// tg: code/modules/language/_language.dm:179 (gaps package C5: a monkey's speech reaches its hearers scrambled)
public partial class datum_language : datum
{
    // tg: code/modules/language/_language.dm:2
    private const int SCRAMBLE_CACHE_LEN = 50;
    // tg: code/modules/language/_language.dm:4
    private const int SENTENCE_CACHE_LEN = 20;

    /// Checks the word cache for a word
    // tg: code/modules/language/_language.dm:179
    public virtual object read_word_cache(object input = null)
    {
        // we generally want "The" and "the" to translate to the same thing.
        // so we lowercase everything, making it case insensitive.
        object lowertext_input = LOWER_TEXT(input as string);
        if (Dm.truthy(most_common_cache[lowertext_input]))
            return most_common_cache[lowertext_input];

        object dot = scramble_cache[lowertext_input];
        if (Dm.truthy(dot) && !Dm.eq(scramble_cache[1], lowertext_input))
        {
            // bumps it to the top of the cache
            Dm.remove(ref scramble_cache, lowertext_input);
            scramble_cache[lowertext_input] = dot;
        }
        return dot;
    }

    /// Adds a word to the cache
    // tg: code/modules/language/_language.dm:195
    public virtual void write_word_cache(object input = null, object scrambled_text = null)
    {
        object lowertext_input = LOWER_TEXT(input as string);
        // The most common words are always cached
        if (Dm.truthy(GLOB.most_common_words_frequency[lowertext_input]))
        {
            most_common_cache[lowertext_input] = scrambled_text;
            return;
        }
        // Add it to cache, cutting old entries if the list is too long
        scramble_cache[lowertext_input] = scrambled_text;
        if (Dm.num(length(scramble_cache)) > SCRAMBLE_CACHE_LEN)
            scramble_cache.Cut(1, scramble_cache.len - SCRAMBLE_CACHE_LEN + 1);
    }

    /// Checks the sentence cache for a sentence
    // tg: code/modules/language/_language.dm:208
    public virtual object read_sentence_cache(object input = null)
    {
        // the only handling we do is capitalizing the first word, as say auto-capitalizes the first word anyway
        // the actual structure of the sentence is otherwise case sensitive so it's preserved
        object input_capitalized = capitalize(input as string);
        object dot = last_sentence_cache[input_capitalized];
        if (Dm.truthy(dot) && !Dm.eq(last_sentence_cache[1], input_capitalized))
        {
            // bumps it to the top of the cache (don't anticipate this happening often)
            Dm.remove(ref last_sentence_cache, input_capitalized);
            last_sentence_cache[input_capitalized] = dot;
        }
        return dot;
    }

    /// Adds a sentence to the cache, though the sentence should be modified with a key
    // tg: code/modules/language/_language.dm:221
    public virtual void write_sentence_cache(object input = null, object key = null, object result_scramble = null)
    {
        object input_capitalized = capitalize(input as string);
        // Add to the cache (the cache being an assoc list of assoc lists), cutting old entries if the list is too long
        DmList entry = last_sentence_cache[input_capitalized] as DmList ?? Dm.list();
        entry[key] = result_scramble;
        last_sentence_cache[input_capitalized] = entry;
        if (Dm.num(length(last_sentence_cache)) > SENTENCE_CACHE_LEN)
            last_sentence_cache.Cut(1, last_sentence_cache.len - SENTENCE_CACHE_LEN + 1);
    }

    /**
     * Scramble a paragraph in this language.
     *
     * Takes into account any languages the hearer knows that has mutual understanding with this language.
     */
    // tg: code/modules/language/_language.dm:234
    public virtual object scramble_paragraph(object input = null, DmList mutual_languages = null)
    {
        // perfect understanding, no need to scramble
        if (Dm.num(mutual_languages?[type]) >= 100)
            return input;

        regex first_sentence = Dm.regex("(.+?(?:[\\.!\\?]|$))", "g");
        DmList new_paragraph = Dm.list();
        while (Dm.truthy(first_sentence.Find(input)))
            Dm.append(ref new_paragraph, scramble_sentence(trim(first_sentence.group[1] as string), mutual_languages));
        return jointext(new_paragraph, " ");
    }

    /**
     * Scrambles a sentence in this language.
     *
     * Takes into account any languages the hearer knows that has mutual understanding with this language.
     */
    // tg: code/modules/language/_language.dm:250
    public virtual object scramble_sentence(object input = null, DmList mutual_languages = null)
    {
        object cache_key = Dm.text("[]-understanding", Dm.truthy(mutual_languages?[type]) ? mutual_languages[type] : 0);
        DmList cache = read_sentence_cache(input) as DmList;
        if (Dm.truthy(cache?[cache_key]))
            return cache[cache_key];

        // List of words that will be recombined into a sentence
        DmList scrambled_words = Dm.list();
        // List which indexes correspond to words in scrambled_words, records whether the word was translated
        // Can't be a single assoc list because duplicates are expected
        DmList translated_index = Dm.list();
        foreach (object word in Dm.each(splittext(input as string, " ")))
        {
            float translate_prob = Dm.num(mutual_languages?[type]);
            object base_word = strip_outer_punctuation(html_decode(word as string));
            if (translate_prob > 0)
            {
                // the probability of managing to understand a word is based on how common it is (up to +20%, though -5% for very uncommon words)
                // 1000 words in the list, so words outside the list are just treated as "the 1250th most common word"
                object frequency = GLOB.most_common_words_frequency[LOWER_TEXT(base_word as string)];
                float commonness = Dm.truthy(frequency) ? Dm.num(frequency) : 1250;
                translate_prob += Math.Max(20 - (commonness / 1000) * 20, -5);
                if (prob(translate_prob))
                {
                    Dm.append(ref scrambled_words, word);
                    Dm.append(ref translated_index, FALSE);
                    continue;
                }
            }

            object scrambled_word = scramble_word(base_word);
            Dm.append(ref scrambled_words, scrambled_word);
            Dm.append(ref translated_index, Dm.num(!Dm.eq(scrambled_word, base_word)));
        }

        // start building the new sentence. first word is capitalized and otherwise untouched
        string sentence = capitalize(scrambled_words[1] as string) as string;
        for (int i = 2; i <= scrambled_words.len; i++)
        {
            object word = scrambled_words[i];
            // this was not translated so just throw it in
            if (!Dm.truthy(translated_index[i]))
            {
                sentence += Dm.text(" []", word);
                continue;
            }
            // if the last word was scrambled, always include a space
            if (Dm.truthy(translated_index[i - 1]) || prob(between_word_space_chance))
                sentence += " ";
            // lastly try inserting a new sentence
            else if (prob(between_word_sentence_chance))
            {
                sentence += ". ";
                word = capitalize(word as string);
            }

            sentence += word;
        }

        // scrambling the word will drop punctuation, so we need to re-add it at the end
        // (however we don't need to do anything if the last word was not translated)
        if (Dm.truthy(translated_index[scrambled_words.len]))
            sentence += find_last_punctuation(input);

        write_sentence_cache(input, cache_key, sentence);

        return sentence;
    }

    /**
     * Scrambles a single word in this language.
     */
    // tg: code/modules/language/_language.dm:308
    public virtual object scramble_word(object input = null)
    {
        // If the input is cached already, move it to the end of the cache and return it
        object word = read_word_cache(input);
        if (Dm.truthy(word))
            return Dm.truthy(is_uppercase(input)) && Dm.num(length_char(input as string)) >= 2 ? uppertext(word as string) : word;

        if (!Dm.truthy(length(syllables)))
            word = stars(input);
        else
        {
            float input_size = Math.Max(Dm.num(length_char(input as string)) + rand(additional_syllable_low, additional_syllable_high), 1);
            bool add_space = false;
            bool add_period = false;
            string built = "";
            while (Dm.num(length_char(built)) < input_size)
            {
                // add in the last syllable's period or space first
                if (add_period)
                    built += ". ";
                else if (add_space)
                    built += " ";
                // insert special chars if we're not at the start of the word
                else if (built != "" && prob(1) && Dm.truthy(length(special_characters)))
                    built += pick(special_characters) as string;
                // generate the next syllable (capitalize if we just added a period)
                string next = pick_weight_recursive(syllables) as string;
                built += add_period ? capitalize(next) : next;
                // determine if the next syllable gets a period or space
                add_period = prob(sentence_chance);
                add_space = prob(space_chance);
            }
            word = built;
        }

        write_word_cache(input, word);

        // If they're shouting, we're shouting
        return Dm.truthy(is_uppercase(input)) && Dm.num(length_char(input as string)) >= 2 ? uppertext(word as string) : word;
    }
}
