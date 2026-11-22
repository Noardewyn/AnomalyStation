// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/text.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only sanitize (M5 slice 6: the finished airlock's passed name), trim (the configuration, its entries and world/ConfigLoaded use it, boot package, wave 3),
// semver_to_list (logging and chat, wave 3: the logger checks every category's schema version with it), and
// GLOB.hex_characters, random_string and random_color (M2 P2: a human's dna features, dna.dm:350), and capitalize (M2 F1:
// a random human name, the language's default_name) and siunit (M4c slice 2: a power cell's
// description) and format_text (M7 slice 3: an APC's name, get_area_name) and sanitize_css_class_name (E13: the RCD's sprite sheet classes), stripped_input (syndicate A1: the antagonist admin tools),
// scramble_message_replace_chars (syndicate A2: the hijacked shuttle console's announcements), full_capitalize (syndicate X2: the poll alert), reverse_text, piglatin_sentence and piglatin_word (gaps P3: the speech mutations), random_capital_letter (gaps P4a: the bank accounts), text2charlist (gaps B1: the expressive aphasia) translated; the other
// text helpers are translated with the code that uses them.

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/text.dm:427  (gaps P3, D327)
    public static object reverse_text(object text = null)
    {
        text ??= "";
        string new_text = "";
        float lentext = length(text);
        string letter = "";
        for (float i = 1; i <= lentext; i += length(letter))
        {
            letter = Dm.str(Dm.textchar(text, i));
            new_text = letter + new_text;
        }
        return new_text;
    }

    ///runs `piglatin_word()` proc on each word in a sentence. preserves caps and punctuation
    // tg: code/__HELPERS/text.dm:1109  (gaps P3, D327)
    public static object piglatin_sentence(object text = null)
    {
        float text_length = length(text);

        //remove caps since words will be shuffled
        text = LOWER_TEXT(text);
        //remove punctuation for same reasons as above
        string punctuation = "";
        DmList punctuation_hit_list = Dm.list("!", "?", ".", "-");
        for (float letter_index = text_length; letter_index >= 1; letter_index--)
        {
            object letter = Dm.textchar(text, letter_index);
            if (!Dm.@in(letter, punctuation_hit_list))
                break;
            punctuation += Dm.str(letter);
        }
        punctuation = Dm.str(reverse_text(punctuation));
        text = copytext(text, 1, (text_length + 1) - length(punctuation));

        //now piglatin each word
        DmList old_words = splittext(text, " ");
        DmList new_words = Dm.list();
        foreach (object word in Dm.each(old_words))
            Dm.append(ref new_words, piglatin_word(word));
        text = new_words.Join(" ");
        //replace caps and punc
        text = capitalize(text);
        text = Dm.str(text) + punctuation;
        return text;
    }

    ///takes "word", and returns it piglatinized.
    // tg: code/__HELPERS/text.dm:1137  (gaps P3, D327)
    public static object piglatin_word(object word = null)
    {
        if (length(word) == 1)
            return word;
        string first_letter = copytext(word, 1, 2);
        string first_two_letters = copytext(word, 1, 3);
        bool first_word_is_vowel = Dm.@in(first_letter, VOWELS());
        bool second_word_is_vowel = Dm.@in(copytext(word, 2, 3), VOWELS());
        //If a word starts with a vowel add the word "way" at the end of the word.
        if (first_word_is_vowel)
            return Dm.str(word) + Dm.str(pick("yay", "way", "hay")); //in cultures around the world it's different, so heck lets have fun and make it random. should still be readable
        //If a word starts with a consonant and a vowel, put the first letter of the word at the end of the word and add "ay."
        if (!first_word_is_vowel && second_word_is_vowel)
        {
            string rest = copytext(word, 2);
            rest += first_letter;
            return rest + "ay";
        }
        //If a word starts with two consonants move the two consonants to the end of the word and add "ay."
        if (!first_word_is_vowel && !second_word_is_vowel)
        {
            string rest = copytext(word, 3);
            rest += first_two_letters;
            return rest + "ay";
        }
        //otherwise unmutated
        return word;
    }

    /// Returns TRUE if the input_text ends with the ending
    // tg: code/__HELPERS/text.dm:1234  (silicons SI4d: the sprite editor's flood fill)
    public static object endswith(object input_text = null, object ending = null)
    {
        float input_length = LAZYLEN(ending);
        return Dm.truthy(Dm.findtext(input_text, ending, -input_length)) ? TRUE : FALSE;
    }

    //Adds 'char' ahead of 'text' until there are 'count' characters total
    // tg: code/__HELPERS/text.dm  (M10 slice G3: the shuttles' timer strings)
    public static string add_leading(object text = null, object count = null, object @char = null)
    {
        @char ??= " ";
        string s = Dm.text("[]", text);
        float charcount = Dm.num(count) - Dm.num(length_char(s));
        DmList chars_to_add = Dm.list_n(Math.Max(charcount + 1, 0));
        return Dm.text("[][]", jointext(chars_to_add, @char), s);
    }

    ///returns nothing with an alert instead of the message if it contains something in the ic filter, and sanitizes normally if the name is fine. It returns nothing so it backs out of the input the same way as if you had entered nothing.
    // tg: code/__HELPERS/text.dm:25  (engineering E16: the big manipulator's task names)
    public static async DmTask<object> sanitize_name(object target = null, float? allow_numbers = null, float? cap_after_symbols = null)
    {
        allow_numbers ??= FALSE;
        cap_after_symbols ??= TRUE;
        if (Dm.truthy(is_ic_filtered(target)) || Dm.truthy(is_soft_ic_filtered(target)))
        {
            await tgui_alert(usr, "You cannot set a name that contains a word prohibited in IC chat!");
            return "";
        }
        object result = reject_bad_name(target, allow_numbers: allow_numbers, strict: TRUE, cap_after_symbols: cap_after_symbols);
        if (!Dm.truthy(result))
        {
            await tgui_alert(usr, "Invalid name.");
            return "";
        }
        return sanitize(result);
    }

    // tg: code/__HELPERS/text.dm:43 (silicons SI8: the malf AI's voice changer)
    public static object strip_html(object text = null, object limit = null)
    {
        limit ??= MAX_MESSAGE_LEN;
        return sanitize(STRIP_HTML_SIMPLE(text, limit));
    }

    /// Runs STRIP_HTML_FULL and sanitize.
    // tg: code/__HELPERS/text.dm:48  (gaps SC2: the records consoles' crimes, notes and printouts)
    public static object strip_html_full(object text = null, float? limit = null)
    {
        limit ??= MAX_MESSAGE_LEN;
        return sanitize(STRIP_HTML_FULL(text, limit));
    }

    // tg: code/__DEFINES/text.dm:56  #define STRIP_HTML_SIMPLE(text, limit) (GLOB.angular_brackets.Replace(copytext(text, 1, limit), ""))
    // SHIM-GAP: the /regex datum is planned (rules Appendix C): GLOB.angular_brackets (_regexes.dm:16, regex(@"[<>]", "g")) is .NET's
    // on the same pattern, as sanitize's below.
    public static string STRIP_HTML_SIMPLE(object text, object limit) => AngularBrackets.Replace(Dm.copytext(text, 1, Dm.num(limit)), "");

    private static readonly Regex AngularBrackets = new(@"[<>]", RegexOptions.ECMAScript);

    // tg: code/__HELPERS/text.dm:37
    public static object sanitize(object text = null)
    {
        // SHIM-GAP: the /regex datum is planned (rules Appendix C), so the proc-level var/static/regex/regex =
        // regex(@"[\n\t]", "g") (:38) has no TgState field yet: its global Replace is .NET's on the same pattern.
        return html_encode(SanitizeNewlinesAndTabs.Replace(Dm.text("[]", text), "#"));
    }

    private static readonly Regex SanitizeNewlinesAndTabs = new(@"[\n\t]", RegexOptions.ECMAScript);

    /**
     * Returns the text if properly formatted, or null else (M10 slice G5: the communications console's status lines).
     * Things considered improper: larger than max_length; non-ASCII characters if ascii_only is set; only whitespace; the
     * <, >, \ and / characters; ASCII control characters (horizontal tab and new line not included).
     */
    // tg: code/__HELPERS/text.dm:91
    public static object reject_bad_text(object text = null, float? max_length = null, float? ascii_only = null)
    {
        max_length ??= 512;
        ascii_only ??= TRUE;
        // SHIM-GAP, as in sanitize: tg's var/static/regex patterns are .NET's on the same patterns
        string s = Dm.text("[]", text);
        if (Dm.truthy(ascii_only))
        {
            if (Dm.num(length(text)) > max_length)
                return null;
            if (RejectNonAscii.IsMatch(s))
                return null;
        }
        else if (Dm.num(length_char(text)) > max_length)
            return null;
        if (!RejectNonWhitespace.IsMatch(s))
            return null;
        if (RejectBadChars.IsMatch(s))
            return null;
        return text;
    }

    // tg: code/__HELPERS/text.dm:1166  (gaps S4b: the NTNRC chat client's names and messages)
    public static object reject_bad_chattext(object text = null, float? max_length = null)
    {
        max_length ??= 256;
        bool non_whitespace = false;
        if (Dm.num(length(text)) > max_length)
            return null;
        // text[i] and length(char) walk the text a character at a time; text2ascii(char) is its code point
        string s = Dm.text("[]", text);
        for (int i = 0; i < s.Length; i += char.IsSurrogatePair(s, i) ? 2 : 1)
        {
            int code = char.IsSurrogatePair(s, i) ? char.ConvertToUtf32(s, i) : s[i];
            if (code <= 31)
                return null;
            if (code == 32)
                continue;
            non_whitespace = true;
        }
        if (non_whitespace)
            return text;
        return null;
    }

    private static readonly Regex RejectNonAscii = new(@"[^\x20-\x7E\t\n]", RegexOptions.ECMAScript);
    private static readonly Regex RejectNonWhitespace = new(@"\S", RegexOptions.ECMAScript);
    private static readonly Regex RejectBadChars = new(@"[\\<>/\x00-\x08\x11-\x1F]", RegexOptions.ECMAScript);

    // tg: code/__HELPERS/text.dm:998
    public static object unintelligize(object message = null)
    {
        regex word_boundaries = Dm.regex(@"\b[\S]+\b", "g");
        string prefix = Dm.textchar(message, 1);
        if (prefix == ";")
            message = Dm.copytext(message, 1 + Dm.length(prefix));
        else if (Dm.@in(prefix, Dm.list(":", "#")))
        {
            prefix += Dm.textchar(message, 1 + Dm.length(prefix));
            message = Dm.copytext(message, Dm.length(prefix));
        }
        else
            prefix = "";

        DmList rearranged = Dm.list();
        while (Dm.truthy(word_boundaries.Find(message)))
        {
            object cword = word_boundaries.match;
            if (Dm.truthy(length(cword)))
                Dm.append(ref rearranged, cword);
        }
        shuffle_inplace(rearranged);
        return Dm.text("[][]", prefix, jointext(rearranged, " "));
    }

    //Checks if any of a given list of needles is in the haystack
    // tg: code/__HELPERS/text.dm:310  (gaps F1: the reagent holder's reaction messages)
    public static object text_in_list(object haystack = null, DmList needle_list = null, float start = 1, float end = 0)
    {
        foreach (object needle in Dm.each(needle_list))
        {
            if (Dm.truthy(Dm.findtext(haystack, needle, start, end)))
                return TRUE;
        }
        return FALSE;
    }

    //Returns a string with reserved characters and spaces before the first letter removed
    // tg: code/__HELPERS/text.dm:338
    public static object trim_left(object text = null)
    {
        for (float i = 1; i <= Dm.num(length(text)); i++)
        {
            if (Dm.text2ascii(text, i) > 32)
                return Dm.copytext(text, i);
        }
        return "";
    }

    //Returns a string with reserved characters and spaces after the last letter removed
    // tg: code/__HELPERS/text.dm:345  (gaps package G4: a memory's story)
    public static object trim_right(object text = null)
    {
        for (float i = Dm.num(length(text)); i > 0; i--)
        {
            if (Dm.text2ascii(text, i) > 32)
                return Dm.copytext(text, 1, i + 1);
        }
        return "";
    }

    //Returns a string with reserved characters and spaces before the first word and after the last word removed.
    // tg: code/__HELPERS/text.dm:368
    public static object trim(object text = null, object max_length = null)
    {
        if (Dm.truthy(max_length))
            text = copytext_char(text, 1, Dm.num(max_length));
        return (trimtext(text) is var or__1 && Dm.truthy(or__1) ? or__1 : ""); //users expect atleast an empty string
    }

    //Returns a string with the first element of the string capitalized.
    // tg: code/__HELPERS/text.dm:374
    public static object capitalize(object t = null)
    {
        object dot = null;
        dot = t;
        if (Dm.truthy(t))
        {
            // SHIM-GAP: BYOND 515 text indexing (t[1], the character at a position) is not in the shim; copytext(t, 1, 2)
            // gives the same one-character text (positions count UTF-16 code units, DV4).
            dot = copytext(t, 1, 2);
            return Dm.add(uppertext(dot), copytext(t, 1 + length(dot)));
        }
        return dot;
    }

    // tg: code/__HELPERS/text.dm:381  (syndicate X2: the ghosts' poll alert's role)
    public static object full_capitalize(object input = null)
    {
        regex first_letter = Dm.@new<regex>(@"[^A-z]*?([A-z]*)", "g");
        // replacetext(input, first_letter, /proc/capitalize): SHIM-GAP, the shim's replacetext takes no /regex needle; BYOND hands such a
        // call to the regex's Replace, which is written here
        return first_letter.Replace(input, "/proc/" + nameof(capitalize));
    }

    // tg: code/__HELPERS/text.dm:1217  (E13: the RCD family's sprite sheet classes)
    public static object sanitize_css_class_name(object name = null)
    {
        // SHIM-GAP: as semver_to_list's, the proc-level var/static/regex/regex = new(@"[^a-zA-Z0-9]","g") (:1218) is this replacement on
        // the same pattern (every match: the "g" flag)
        return Regex.Replace(Dm.text("[]", name), "[^a-zA-Z0-9]", "", RegexOptions.ECMAScript);
    }

    /// Converts a semver string into a list of numbers
    // tg: code/__HELPERS/text.dm:1222
    public static object semver_to_list(object semver_string = null)
    {
        // SHIM-GAP: the /regex datum is planned (rules Appendix C), so the proc-level var/static/regex/semver_regex =
        // regex(@"(\d+)\.(\d+)\.(\d+)", "") (:1222) has no TgState field yet: its Find and group[1..3] are this match on
        // the same pattern (ECMAScript \d: ASCII digits, as BYOND's regex; a null text matches nothing).
        Match semver_regex = Regex.Match(Dm.text("[]", semver_string), @"(\d+)\.(\d+)\.(\d+)", RegexOptions.ECMAScript);
        if (!semver_regex.Success)
            return null;

        return Dm.list(
            text2num(semver_regex.Groups[1].Value),
            text2num(semver_regex.Groups[2].Value),
            text2num(semver_regex.Groups[3].Value));
    }

    // tg: code/__HELPERS/text.dm:1094
    public static object scramble_message_replace_chars(object original = null, float? replaceprob = null, DmList replacementchars = null, float? replace_letters_only = null, float? replace_whitespace = null)
    {
        replaceprob ??= 25;
        replacementchars ??= Dm.list("$", "@", "!", "#", "%", "^", "&", "*");
        replace_letters_only ??= FALSE;
        replace_whitespace ??= FALSE;
        DmList @out = Dm.list();
        DmList whitespace = TgState.Current.scramble_message_replace_chars__whitespace ??= Dm.list(" ", "\n", "\t");
        for (float i = 1, i__end = Dm.num(length(original)); i <= i__end; i++)
        {
            string @char = Dm.textchar(original, i);
            if (!Dm.truthy(replace_whitespace) && Dm.@in(@char, whitespace))
            {
                Dm.append(@out, @char);
                continue;
            }
            // ISINRANGE(char, 65, 90) on a text (__DEFINES/maths.dm): DM's comparison of a text with a number
            if (Dm.truthy(replace_letters_only) && (!(Dm.le(65, @char) && Dm.le(@char, 90)) && !(Dm.le(97, @char) && Dm.le(@char, 122))))
            {
                Dm.append(@out, @char);
                continue;
            }
            Dm.append(@out, prob(replaceprob.Value) ? pick(replacementchars) : @char);
        }
        return @out.Join("");
    }
}

// tg: code/__HELPERS/text.dm:437 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/text.dm:437  GLOBAL_LIST_INIT(hex_characters, list("0", ..., "f"))
    public DmList hex_characters;

    // tg: code/__HELPERS/text.dm:437  (GLOBAL_MANAGED(hex_characters, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhex_characters()
    {
        hex_characters = Dm.list("0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f");
        Dm.append(ref gvars_datum_init_order, "hex_characters");
    }

    // Engineering E12b (the EMPed HFR's garbled radio, hfr_procs.dm:383):
    // tg: code/__HELPERS/text.dm:438  GLOBAL_LIST_INIT(alphabet, list("a", ..., "z"))
    public DmList alphabet;

    // tg: code/__HELPERS/text.dm:438  (GLOBAL_MANAGED(alphabet, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalalphabet()
    {
        alphabet = Dm.list("a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z");
        Dm.append(ref gvars_datum_init_order, "alphabet");
    }

    // tg: code/__HELPERS/text.dm:439  GLOBAL_LIST_INIT(alphabet_upper, list("A", ..., "Z"))
    public DmList alphabet_upper;

    // tg: code/__HELPERS/text.dm:439  (GLOBAL_MANAGED(alphabet_upper, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalalphabet_upper()
    {
        alphabet_upper = Dm.list("A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z");
        Dm.append(ref gvars_datum_init_order, "alphabet_upper");
    }

    // tg: code/__HELPERS/text.dm:440  GLOBAL_LIST_INIT(numerals, list("1", ..., "0"))
    public DmList numerals;

    // tg: code/__HELPERS/text.dm:440  (GLOBAL_MANAGED(numerals, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnumerals()
    {
        numerals = Dm.list("1", "2", "3", "4", "5", "6", "7", "8", "9", "0");
        Dm.append(ref gvars_datum_init_order, "numerals");
    }

    // tg: code/__HELPERS/text.dm:441  GLOBAL_LIST_INIT(space, list(" "))
    public DmList space;

    // tg: code/__HELPERS/text.dm:441  (GLOBAL_MANAGED(space, list(" ")), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalspace()
    {
        space = Dm.list(" ");
        Dm.append(ref gvars_datum_init_order, "space");
    }
}

// tg: code/__HELPERS/text.dm:443 (interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/text.dm:443
    public static object random_string(object length = null, DmList characters = null)
    {
        object dot = null;
        dot = "";
        for (float i = 1, i__end = Dm.num(length); i <= i__end; i++)
            Dm.append(ref dot, pick(characters));
        return dot;
    }

    // tg: code/__HELPERS/text.dm:453 (M3 I1d: the graffiti spawner's colour)
    public static object random_short_color()
    {
        return random_string(3, GLOB.hex_characters);
    }

    // tg: code/__HELPERS/text.dm:456
    public static object random_color()
    {
        return random_string(6, GLOB.hex_characters);
    }

    // tg: code/__HELPERS/text.dm:459 (gaps RD4: the relic's cardboard disguise)
    public static object ready_random_color()
    {
        return "#" + Dm.str(random_string(6, GLOB.hex_characters));
    }

    /// Formats a number to human readable form with the appropriate SI unit.
    // tg: code/__HELPERS/text.dm:1078
    public static object siunit(object value = null, object unit = null, float? maxdecimals = null)
    {
        maxdecimals ??= 1;
        DmList si_isolated = siunit_isolated(value, unit, maxdecimals) as DmList;
        return Dm.text("[][]", si_isolated[SI_COEFFICIENT], si_isolated[SI_UNIT]);
    }

    /// The game code never uses Pa, but kPa, since 1 Pa is too small to reasonably handle
    /// Thus, to ensure correct conversion from any kPa in game code, this value needs to be multiplied by 10e3 to get Pa, which the siunit() proc expects
    // tg: code/__HELPERS/text.dm:1089
    public static object siunit_pressure(object value_in_kpa = null, float? maxdecimals = null)
    {
        object pressure_adj = Dm.mul(value_in_kpa, 1000); //to adjust for using kPa instead of Pa
        return siunit(pressure_adj, "Pa", maxdecimals);
    }

    // tg: code/__HELPERS/text.dm:1185
    public static object format_text(object text = null)
    {
        return replacetext(replacetext(text, Dm.text("\\proper "), ""), Dm.text("\\improper "), "");
    }
}

// tg: code/__HELPERS/text.dm:895 (interleaved types, rules 1.9; M9 slice D2: the station name's written numbers)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/text.dm:895
    public static DmList int_to_words(object number = null, object carried_string = null, object capitalise = null)
    {
        // var/static/list/tens and ones: constant lists, made per call (never written)
        DmList tens = Dm.list("", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety");
        DmList ones = Dm.list("one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen");
        float n = Dm.round(Dm.num(number));

        if (n > 999)
            return null;

        if (n <= 0)
            return null;

        DmList @string = Dm.list();

        if (n >= 100)
        {
            Dm.append(ref @string, int_to_words(n / 100, "hundred"));
            n = Dm.round(Dm.mod(n, 100));
            if (n == 0)
                return Dm.add(@string, carried_string) as DmList;
            Dm.append(ref @string, "and");
        }

        //if number is more than 19, divide it
        if (n > 19)
        {
            object temp_num = tens[(int)(n / 10)];
            if (Dm.mod(n, 10) != 0)
            {
                temp_num = Dm.add(temp_num, "-");
                if (Dm.truthy(capitalise))
                    temp_num = Dm.add(temp_num, capitalize(ones[(int)Dm.mod(n, 10)]));
                else
                    temp_num = Dm.add(temp_num, ones[(int)Dm.mod(n, 10)]);
            }
            Dm.append(ref @string, temp_num);
        }
        else
            Dm.append(ref @string, ones[(int)n]);

        if (Dm.truthy(carried_string))
            Dm.append(ref @string, carried_string);

        return @string;
    }

    // tg: code/__HELPERS/text.dm:940
    public static object convert_integer_to_words(object number = null, object capitalise = null)
    {
        if (!isnum(number))
            return null;

        float n = Dm.num(number);
        bool negative = false;
        if (n < 0)
        {
            n = Dm.abs(n);
            negative = true;
        }

        n = Dm.round(n);

        if (n > 999999999)
            return negative ? -n : n;

        if (n == 0)
            return Dm.truthy(capitalise) ? "Zero" : "zero";

        //stores word representation of given number
        DmList output = Dm.list();

        //handles millions
        DmList millions = int_to_words(Dm.mod(n / 1000000, 1000), "million", capitalise);
        if (Dm.length(millions) > 0)
            Dm.append(ref output, millions);

        ///handles thousands
        DmList thousands = int_to_words(Dm.mod(n / 1000, 1000), "thousand", capitalise);
        if (Dm.length(thousands) > 0)
            Dm.append(ref output, thousands);

        if (output.len > 0 && Dm.mod(n, 1000) != 0 && Dm.mod(n, 1000) < 100) //e.g. One Thousand And Ninety-Nine, instead of One Thousand Ninety-Nine
            Dm.append(ref output, "and");

        //handles digits at ones, tens and hundreds places (if any)
        Dm.append(ref output, int_to_words(Dm.mod(n, 1000), "", capitalise));

        for (int index = 1; index <= output.len; index++)
        {
            if (output[index] == null)
            {
                Dm.remove(ref output, output[index]);
                continue;
            }
            if (Dm.truthy(capitalise))
                output[index] = capitalize(output[index]);
        }

        if (output.len == 0)
            return negative ? -n : n;

        string number_in_words = "";
        if (negative)
            number_in_words += Dm.truthy(capitalise) ? "Negative " : "negative ";

        number_in_words += output.Join(" ");

        return number_in_words;
    }
}

// tg: code/__HELPERS/text.dm:289 (interleaved types, rules 1.9; M9 slice E: the notepad's and the file manager's file names)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/text.dm:289
    public static object filter_illegal_filename_chars(object name = null)
    {
        // var/regex/illegal_chars = new("\[\\/:*?\"<>|]", "g"); return !findtext(name, illegal_chars): the regex (a shim gap) is its
        // character class, / : * ? " < > |
        string text = Dm.str(name) ?? "";
        return text.IndexOfAny(new[] { '/', ':', '*', '?', '"', '<', '>', '|' }) < 0 ? TRUE : FALSE;
    }

    // tg: code/__HELPERS/text.dm:293
    public static object filter_filename_pda(object name = null)
    {
        if (is_ic_filtered_for_pdas(name) != null || is_soft_ic_filtered_for_pdas(name) != null)
            return FALSE;
        if (!Dm.truthy(filter_illegal_filename_chars(name)))
            return FALSE;
        return TRUE;
    }
}

// tg: code/__HELPERS/text.dm:147 (interleaved types, rules 1.9; M10 slice F: the character's name preference)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/text.dm:147  #define NO_CHARS_DETECTED 0 (file-local, #undef at :249)
    private const int NO_CHARS_DETECTED = 0;
    // tg: code/__HELPERS/text.dm:148  #define SPACES_DETECTED 1 (file-local, #undef at :250)
    private const int SPACES_DETECTED = 1;
    // tg: code/__HELPERS/text.dm:149  #define SYMBOLS_DETECTED 2 (file-local, never #undef'd but used only here)
    private const int SYMBOLS_DETECTED = 2;
    // tg: code/__HELPERS/text.dm:150  #define NUMBERS_DETECTED 3 (file-local, #undef at :251)
    private const int NUMBERS_DETECTED = 3;
    // tg: code/__HELPERS/text.dm:151  #define LETTERS_DETECTED 4 (file-local, #undef at :252)
    private const int LETTERS_DETECTED = 4;

    //Filters out undesirable characters from names
    // tg: code/__HELPERS/text.dm:161
    public static object reject_bad_name(object t_in = null, object allow_numbers = null, object max_length = null, object ascii_only = null, object strict = null, object cap_after_symbols = null, object cap_at_start = null)
    {
        allow_numbers ??= FALSE;
        max_length ??= MAX_NAME_LEN;
        ascii_only ??= TRUE;
        strict ??= FALSE;
        cap_after_symbols ??= TRUE;
        cap_at_start ??= TRUE;
        if (!Dm.truthy(t_in))
            return null; //Rejects the input if it is null

        float number_of_alphanumeric = 0;
        int last_char_group = NO_CHARS_DETECTED;
        string t_out = "";
        float t_len = length(t_in);
        float charcount = 0;
        string @char = "";

        // This is a sanity short circuit, if the users name is three times the maximum allowable length of name
        // We bail out on trying to process the name at all, as it could be a bug or malicious input and we don't
        // Want to iterate all of it.
        if (t_len > 3 * MAX_NAME_LEN)
            return null;
        for (float i = 1; i <= t_len; i += length(@char))
        {
            @char = Dm.textchar(t_in, i);
            float ascii = text2ascii(@char);
            if (ascii >= 65 && ascii <= 90) // A  .. Z
            {
                //Uppercase Letters
                number_of_alphanumeric++;
                last_char_group = LETTERS_DETECTED;
            }
            else if (ascii >= 97 && ascii <= 122) // a  .. z
            {
                //Lowercase Letters
                if (((last_char_group == NO_CHARS_DETECTED || last_char_group == SPACES_DETECTED) && Dm.truthy(cap_at_start)) || (Dm.truthy(cap_after_symbols) && last_char_group == SYMBOLS_DETECTED)) //start of a word
                    @char = uppertext(@char);
                number_of_alphanumeric++;
                last_char_group = LETTERS_DETECTED;
            }
            else if (ascii >= 48 && ascii <= 57) // 0  .. 9
            {
                //Numbers
                if (!Dm.truthy(allow_numbers)) //allow name to start with number if AI/Borg
                {
                    if (Dm.truthy(strict))
                        return null;
                    continue;
                }
                number_of_alphanumeric++;
                last_char_group = NUMBERS_DETECTED;
            }
            else if (ascii == 39 || ascii == 45 || ascii == 46) // '  -  .
            {
                //Common name punctuation
                if (last_char_group == NO_CHARS_DETECTED)
                {
                    if (Dm.truthy(strict))
                        return null;
                    continue;
                }
                last_char_group = SYMBOLS_DETECTED;
            }
            else if (ascii == 126 || ascii == 124 || ascii == 64 || ascii == 58 || ascii == 35 || ascii == 36 || ascii == 37 || ascii == 38 || ascii == 42 || ascii == 43) // ~   |   @  :  #  $  %  &  *  +
            {
                //Other symbols that we'll allow (mainly for AI)
                if (last_char_group == NO_CHARS_DETECTED || !Dm.truthy(allow_numbers)) //suppress at start of string
                {
                    if (Dm.truthy(strict))
                        return null;
                    continue;
                }
                last_char_group = SYMBOLS_DETECTED;
            }
            else if (ascii == 32)
            {
                //Space
                if (last_char_group == NO_CHARS_DETECTED || last_char_group == SPACES_DETECTED) //suppress double-spaces and spaces at start of string
                {
                    if (Dm.truthy(strict))
                        return null;
                    continue;
                }
                last_char_group = SPACES_DETECTED;
            }
            else if (ascii >= 127)
            {
                if (Dm.truthy(ascii_only))
                {
                    if (Dm.truthy(strict))
                        return null;
                    continue;
                }
                last_char_group = SYMBOLS_DETECTED; //for now, we'll treat all non-ascii characters like symbols even though most are letters
            }
            else
            {
                continue;
            }
            t_out += @char;
            charcount++;
            if (charcount >= Dm.num(max_length))
                break;
        }

        if (number_of_alphanumeric < 2)
            return null; //protects against tiny names like "A" and also names like "' ' ' ' ' ' ' '"

        if (last_char_group == SPACES_DETECTED)
            t_out = copytext_char(t_out, 1, -1); //removes the last character (in this case a space)

        if (!Dm.truthy(filter_name_ic(t_out)))
            return null;

        return t_out;
    }

    /// Much more permissive version of reject_bad_name().
    /// Returns a trimmed string or null if the name is invalid.
    /// Allows most characters except for IC chat prohibited words.
    // tg: code/__HELPERS/text.dm:259 (gaps BR1: the hacker alias preference)
    public static object permissive_sanitize_name(object value = null)
    {
        if (!istext(value)) // Not a string
            return null;

        float name_length = length(value);
        if (name_length < 3) // Too short
            return null;

        if (name_length > 3 * MAX_NAME_LEN) // Bad input
            return null;

        object trimmed = trim(value, MAX_NAME_LEN);
        if (!Dm.truthy(filter_name_ic(trimmed))) // Contains IC chat prohibited words
            return null;

        return trim(trimmed);
    }

    // tg: code/__HELPERS/text.dm:278
    public static object filter_name_ic(object name = null)
    {
        foreach (string bad_name in new[] { "space", "floor", "wall", "r-wall", "monkey", "unknown", "inactive ai" }) //prevents these common metagamey names
        {
            if (Dm.truthy(cmptext(name, bad_name)))
                return FALSE; //(not case sensitive)
        }

        // Protects against names containing IC chat prohibited words.
        if (is_ic_filtered(name) != null || is_soft_ic_filtered(name) != null)
            return FALSE;

        return TRUE;
    }
}

// tg: code/__HELPERS/text.dm:448 (interleaved types, rules 1.9; M10 slice F: sanitize_hexcolor's default)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/text.dm:448
    public static string repeat_string(object times = null, object @string = null)
    {
        @string ??= "";
        string dot = "";
        for (int i = 1; i <= Dm.num(times); i++)
            dot += Dm.str(@string);
        return dot;
    }
}

// tg: code/__HELPERS/text.dm:1258 (gaps package C5: the languages' scrambling)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/text.dm:1253  #define STRIPPED_PUNCTUATION (REGEX_QUOTE("!?.~;:,|+_`-"))
    private static string STRIPPED_PUNCTUATION => REGEX_QUOTE("!?.~;:,|+_`-");

    // tg: code/__HELPERS/text.dm:1258
    public static object strip_outer_punctuation(object input = null)
    {
        regex pre_word_regex = Dm.regex("^(?:[" + STRIPPED_PUNCTUATION + "]{0,3})(.*)", "m");
        if (Dm.truthy(pre_word_regex.Find(input)))
            input = pre_word_regex.group[1];

        regex post_word_regex = Dm.regex("^(.*?)(?:[" + STRIPPED_PUNCTUATION + "]{0,3})$", "m");
        if (Dm.truthy(post_word_regex.Find(input)))
            return trim(post_word_regex.group[1] as string);

        return trim(input as string);
    }

    /// Find what punctuation is at the end of the input, returns it.
    /// Ignores emphasis (|, +, _)
    // tg: code/__HELPERS/text.dm:1273
    public static string find_last_punctuation(object input = null)
    {
        regex punctuation_regex = Dm.regex("([!?.~;:,-]{1,3})[|+_\\s]*$", "m");
        if (Dm.truthy(punctuation_regex.Find(input)))
            return punctuation_regex.group[1] as string;

        return "";
    }

    /// Checks if the passed string is all uppercase, ignoring punctuation and numbers and symbols
    // tg: code/__HELPERS/text.dm:1281
    public static object is_uppercase(object input = null)
    {
        regex lowercase_regex = Dm.regex("[a-z]", "g");
        if (Dm.truthy(lowercase_regex.Find(input)))
            return FALSE;
        return TRUE;
    }

    // tg: code/__HELPERS/text.dm:67
    public static object htmlrendertext(object t = null)
    {
        // Trim "whitespace" by lazily capturing word characters in the middle
        regex matchMiddle = Dm.regex("^\\s*([\\W\\w]*?)\\s*$");
        if (!Dm.truthy(matchMiddle.Find(t)))
            return t;
        t = matchMiddle.group[1];

        // Replace any non-space whitespace characters with spaces, and also multiple occurrences with just one space
        regex matchSpacing = Dm.regex("\\s+", "g");
        t = replacetext(t, matchSpacing, " ");

        return t;
    }

    // tg: code/__HELPERS/text.dm:856
    public static object thtotext(object number = null)
    {
        if (!isnum(number))
            return null;
        return Dm.num(number) switch
        {
            1 => "first",
            2 => "second",
            3 => "third",
            4 => "fourth",
            5 => "fifth",
            6 => "sixth",
            7 => "seventh",
            8 => "eighth",
            9 => "ninth",
            10 => "tenth",
            11 => "eleventh",
            12 => "twelfth",
            _ => Dm.text("[]\\th", number),
        };
    }
}

public static partial class GlobalProcs
{
    /**
     * Used to get a properly sanitized input. Returns null if cancel is pressed.
     *
     * Arguments
     ** user - Target of the input prompt.
     ** message - The text inside of the prompt.
     ** title - The window title of the prompt.
     ** max_length - If you intend to impose a length limit - default is 1024.
     ** no_trim - Prevents the input from being trimmed if you intend to parse newlines or whitespace.
    */
    // tg: code/__HELPERS/text.dm:119
    public static async DmTask<object> stripped_input(mob user = null, string message = null, string title = null, string @default = null, float? max_length = null, float? no_trim = null)
    {
        message ??= "";
        title ??= "";
        @default ??= "";
        max_length ??= MAX_MESSAGE_LEN;
        no_trim ??= FALSE;
        // DEVIATION(DV340): BYOND's input(user, message, title, default) as text|null is tgui's text prompt (as DV217's)
        object user_input = await tgui_input_text(user, message, title, @default, encode: FALSE);
        if (user_input == null) // User pressed cancel
            return null;
        if (Dm.truthy(no_trim))
            return copytext_char(html_encode(user_input), 1, max_length.Value);
        else
            return trim(html_encode(user_input), max_length.Value); //trim is "outside" because html_encode can expand single symbols into multiple symbols (such as turning < into &lt;)
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/text.dm:1096  scramble_message_replace_chars' var/static/list/whitespace
    internal DmList scramble_message_replace_chars__whitespace;
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/text.dm:1056
    public static string num2loadingbar(float percent = 0, float numSquares = 20, float reverse = FALSE)
    {
        string loadstring = "";
        for (int i = 1; i <= numSquares; i++)
        {
            float limit = Dm.truthy(reverse) ? numSquares - percent * numSquares : percent * numSquares;
            loadstring += i <= limit ? "█" : "░";
        }
        return "[" + loadstring + "]";
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/text.dm:995  (gaps P4a: a bank account's pay token)
    public static string random_capital_letter()
    {
        return uppertext(Dm.str(pick(GLOB.alphabet)));
    }
}

// tg: code/__HELPERS/text.dm:517-670  (silicons SI6b: the newscaster's stories)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/text.dm:517
    public static object parsemarkdown_basic_step1(object t = null, float limited = FALSE)
    {
        if (length(t) <= 0)
            return null;

        // This parses markdown with no custom rules

        // Escape backslashed

        t = replacetext(t, "$", "$-");
        t = replacetext(t, "\\\\", "$1");
        t = replacetext(t, "\\**", "$2");
        t = replacetext(t, "\\*", "$3");
        t = replacetext(t, "\\__", "$4");
        t = replacetext(t, "\\_", "$5");
        t = replacetext(t, "\\^", "$6");
        t = replacetext(t, "\\((", "$7");
        t = replacetext(t, "\\))", "$8");
        t = replacetext(t, "\\|", "$9");
        t = replacetext(t, "\\%", "$0");

        // Escape  single characters that will be used

        t = replacetext(t, "!", "$a");

        // Parse hr and small

        if (!Dm.truthy(limited))
        {
            t = replacetext(t, "((", "<font size=\"1\">");
            t = replacetext(t, "))", "</font>");
            t = replacetext(t, regex("(-){3,}", "gm"), "<hr>");
            t = replacetext(t, regex("^\\((-){3,}\\)$", "gm"), "$1");

            // Parse lists

            DmList tlist = splittext(t, "\n");
            int tlistlen = (int)tlist.len;
            int listlevel = -1;
            int singlespace = -1; // if 0, double spaces are used before asterisks, if 1, single are
            for (int i = 1; i <= tlistlen; i++)
            {
                string line = Dm.str(tlist[i]);
                float count_asterisk = length(replacetext(line, regex("[^\\*]+", "g"), ""));
                if (count_asterisk % 2 == 1 && Dm.truthy(findtext(line, regex("^\\s*\\*", "g")))) // there is an extra asterisk in the beggining
                {
                    float count_w = length(replacetext(line, regex("^( *)\\*.*$", "g"), "$1")); // whitespace before asterisk
                    line = replacetext(line, regex("^ *(\\*.*)$", "g"), "$1");

                    if (singlespace == -1 && count_w == 2)
                    {
                        if (listlevel == 0)
                            singlespace = 0;
                        else
                            singlespace = 1;
                    }

                    if (singlespace == 0)
                        count_w = count_w % 2 != 0 ? round(count_w / 2 + 0.25f) : count_w / 2;

                    line = replacetext(line, regex("\\*", ""), "<li>");
                    while (listlevel < count_w)
                    {
                        line = "<ul>" + line;
                        listlevel++;
                    }
                    while (listlevel > count_w)
                    {
                        line = "</ul>" + line;
                        listlevel--;
                    }
                }
                else
                {
                    while (listlevel >= 0)
                    {
                        line = "</ul>" + line;
                        listlevel--;
                    }
                }

                tlist[i] = line;
            }
            // end for

            string joined = Dm.str(tlist[1]);
            for (int i = 2; i <= tlistlen; i++)
                joined += "\n" + Dm.str(tlist[i]);
            t = joined;

            while (listlevel >= 0)
            {
                t = Dm.str(t) + "</ul>";
                listlevel--;
            }
        }
        else
        {
            t = replacetext(t, "((", "");
            t = replacetext(t, "))", "");
        }

        // Parse headers

        t = replacetext(t, regex("^#(?!#) ?(.+)$", "gm"), "<h2>$1</h2>");
        t = replacetext(t, regex("^##(?!#) ?(.+)$", "gm"), "<h3>$1</h3>");
        t = replacetext(t, regex("^###(?!#) ?(.+)$", "gm"), "<h4>$1</h4>");
        t = replacetext(t, regex("^#### ?(.+)$", "gm"), "<h5>$1</h5>");

        // Parse most rules

        t = replacetext(t, regex("\\*([^\\*]*)\\*", "g"), "<i>$1</i>");
        t = replacetext(t, regex("_([^_]*)_", "g"), "<i>$1</i>");
        t = replacetext(t, "<i></i>", "!");
        t = replacetext(t, "</i><i>", "!");
        t = replacetext(t, regex("\\!([^\\!]+)\\!", "g"), "<b>$1</b>");
        t = replacetext(t, regex("\\^([^\\^]+)\\^", "g"), "<font size=\"4\">$1</font>");
        t = replacetext(t, regex("\\|([^\\|]+)\\|", "g"), "<center>$1</center>");
        t = replacetext(t, "!", "</i><i>");

        return t;
    }

    // tg: code/__HELPERS/text.dm:619
    public static object parsemarkdown_basic_step2(object t = null)
    {
        if (length(t) <= 0)
            return null;

        // Restore the single characters used

        t = replacetext(t, "$a", "!");

        // Redo the escaping

        t = replacetext(t, "$1", "\\");
        t = replacetext(t, "$2", "**");
        t = replacetext(t, "$3", "*");
        t = replacetext(t, "$4", "__");
        t = replacetext(t, "$5", "_");
        t = replacetext(t, "$6", "^");
        t = replacetext(t, "$7", "((");
        t = replacetext(t, "$8", "))");
        t = replacetext(t, "$9", "|");
        t = replacetext(t, "$0", "%");
        t = replacetext(t, "$-", "$");

        return t;
    }

    // tg: code/__HELPERS/text.dm:643
    public static object parsemarkdown_basic(object t = null, float limited = FALSE)
    {
        t = parsemarkdown_basic_step1(t, limited);
        t = parsemarkdown_basic_step2(t);
        return t;
    }

    // tg: code/__HELPERS/text.dm:648
    public static object parsemarkdown(object t = null, mob user = null, float limited = FALSE)
    {
        if (length(t) <= 0)
            return null;

        // Premanage whitespace

        t = replacetext(t, regex("[^\\S\\r\\n ]", "g"), "  ");

        t = parsemarkdown_basic_step1(t);

        t = replacetext(t, regex("%s(?:ign)?(?=\\s|$)", "igm"), user != null ? Dm.text("<font face=\"[]\"><i>[]</i></font>", SIGNATURE_FONT, user.real_name) : "<span class=\"paper_field\"></span>");
        t = replacetext(t, regex("%f(?:ield)?(?=\\s|$)", "igm"), "<span class=\"paper_field\"></span>");

        t = parsemarkdown_basic_step2(t);

        // Manage whitespace

        t = replacetext(t, regex("(?:\\r\\n?|\\n)", "g"), "<br>");

        t = replacetext(t, "  ", "&nbsp;&nbsp;");

        // Done

        return t;
    }

    // tg: code/__HELPERS/text.dm:673  (gaps B1: the expressive aphasia's scrambled words)
    public static object text2charlist(object text = null)
    {
        string @char = "";
        float lentext = length(text);
        DmList dot = Dm.list();
        for (float i = 1; i <= lentext; i += length(@char))
        {
            @char = Dm.textchar(text, i);
            Dm.append(dot, @char);
        }
        return dot;
    }
}

// tg: code/__HELPERS/text.dm:1188 (gaps H4: the janitorial cart's trash bag; the item's examine tags, the parcel's name and the
// clothing's storage line kept local copies until now)
public static partial class GlobalProcs
{
    ///Returns a string based on the weight class define used as argument
    // tg: code/__HELPERS/text.dm:1189
    public static object weight_class_to_text(object w_class = null)
    {
        object dot = null;
        switch (Dm.num(w_class))
        {
            case WEIGHT_CLASS_TINY:
                dot = "tiny";
                break;
            case WEIGHT_CLASS_SMALL:
                dot = "small";
                break;
            case WEIGHT_CLASS_NORMAL:
                dot = "normal-sized";
                break;
            case WEIGHT_CLASS_BULKY:
                dot = "bulky";
                break;
            case WEIGHT_CLASS_HUGE:
                dot = "huge";
                break;
            case WEIGHT_CLASS_GIGANTIC:
                dot = "gigantic";
                break;
            default:
                dot = "";
                break;
        }
        return dot;
    }

    // tg: code/__HELPERS/text.dm:1206
    public static object weight_class_to_tooltip(object w_class = null)
    {
        switch (Dm.num(w_class))
        {
            case >= WEIGHT_CLASS_TINY and <= WEIGHT_CLASS_SMALL:
                return "This item can fit into pockets, boxes and backpacks.";
            case WEIGHT_CLASS_NORMAL:
                return "This item can fit into backpacks.";
            case >= WEIGHT_CLASS_BULKY and <= WEIGHT_CLASS_GIGANTIC:
                return "This item is too large to fit into any standard storage.";
        }
        return "";
    }
}
