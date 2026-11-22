using System.Globalization;
using System.Text;

namespace Anomaly.Core.DmShim;

// Owner: values. Text rendering and the text builtins (docs/ai/translation-rules.md section 5, 3.9). Culture-invariant;
// positions and lengths count UTF-16 code units in the byte-indexed procs (DV4) and Unicode scalar values in the _char
// procs (DmTextUnits). Text-only parameters are string, parameters that DM lets take several kinds (a needle that may be
// a regex, a value to encode) are object; results are the kind DM returns. Position rules (negative positions count
// from the end, 0 means the end for End, out-of-range handling) follow the BYOND reference and, where it is silent,
// OpenDream's DreamProcNativeRoot and its BYOND-checked tests (Content.Tests/DMProject/Tests/Text, Builtins).
// /regex needles and delimiters are not supported yet (the /regex datum is not ported): such a call is a runtime error.
public static partial class Dm
{
    /// <summary>
    /// A DM value as text, as <c>"[x]"</c> renders it (rules 5.2, 5.5): null as "", numbers with 6 significant digits
    /// (<c>%g</c> style, invariant culture: <c>"[0.5]" == "0.5"</c>, <c>"[0.000001]" == "1e-06"</c>; whole numbers from
    /// one million to 2^24 in full), text as is, a typepath as its path, a list as "/list", an atom as its name with
    /// "The " when the name is improper (the text starts with it), other datums as their type path; a datum's
    /// <c>operator""</c> is called.
    /// </summary>
    public static string str(object x) => DmTextRender.Embedded(x, atStart: true);

    /// <summary>
    /// DM <c>text(FormatText, Args)</c> and every embedded-expression text <c>"...[e]..."</c> (BYOND ref
    /// <c>/proc/text</c>; rules 5.2): the template in DM syntax with each <c>[]</c> replaced by the next argument
    /// rendered by <see cref="str"/>, text macros (<c>\the</c>, <c>\a</c>, <c>\he</c>, <c>\proper</c>, ...) applied,
    /// <c>\[</c> kept literal.
    /// </summary>
    public static string text(string template, params object[] args) => DmTextRender.Template(template, args);

    /// <summary>DM <c>length(E)</c> (BYOND ref <c>/proc/length</c>): text length in UTF-16 code units (DV4), list length, 0 for null and every other value.</summary>
    public static float length(object E) => E switch
    {
        string s => s.Length,
        DmList list => list.Count,
        _ => 0f,
    };

    /// <summary>DM <c>length_char(E)</c>: text length in characters (Unicode scalar values), list length, 0 for null and every other value.</summary>
    public static float length_char(object E) => E switch
    {
        string s => DmTextUnits.CountRunes(s),
        DmList list => list.Count,
        _ => 0f,
    };

    /// <summary>
    /// DM <c>copytext(T, Start=1, End=0)</c> (BYOND ref <c>/proc/copytext</c>): the text from Start to End-1; End 0 (or
    /// past the end) is the end; negative positions count from the end (<c>copytext("Hi there", -5) == "there"</c>);
    /// Start 0 gives "" and a null T gives null when End is 0, else "" (OpenDream <c>NativeProc_copytext</c>).
    /// </summary>
    public static string copytext(object T, float Start = 1, float End = 0) => CopyText(T as string, Start, End, chars: false);

    /// <summary>DM <c>copytext_char(T, Start=1, End=0)</c>: <see cref="copytext"/> counting characters.</summary>
    public static string copytext_char(object T, float Start = 1, float End = 0) => CopyText(T as string, Start, End, chars: true);

    /// <summary>
    /// DM <c>findtext(Haystack, Needle, Start=1, End=0)</c> (BYOND ref <c>/proc/findtext</c>): case-insensitive; the
    /// position of the first match that lies within Start..End-1, or 0. A negative Start or End counts from the end
    /// (<c>findtext("Banana", "na", -3)</c> searches the final "ana"); an empty or null needle finds nothing.
    /// </summary>
    public static float findtext(object Haystack, object Needle, float Start = 1, float End = 0) => FindText(Haystack as string, Needle, Start, End, ignoreCase: true, chars: false);

    /// <summary>DM <c>findtext_char</c>: <see cref="findtext"/> counting characters.</summary>
    public static float findtext_char(object Haystack, object Needle, float Start = 1, float End = 0) => FindText(Haystack as string, Needle, Start, End, ignoreCase: true, chars: true);

    /// <summary>DM <c>findtextEx(Haystack, Needle, Start=1, End=0)</c> (BYOND ref <c>/proc/findtextEx</c>): case-sensitive <see cref="findtext"/>.</summary>
    public static float findtextEx(object Haystack, object Needle, float Start = 1, float End = 0) => FindText(Haystack as string, Needle, Start, End, ignoreCase: false, chars: false);

    /// <summary>DM <c>findtextEx_char</c>: <see cref="findtextEx"/> counting characters.</summary>
    public static float findtextEx_char(object Haystack, object Needle, float Start = 1, float End = 0) => FindText(Haystack as string, Needle, Start, End, ignoreCase: false, chars: true);

    /// <summary>
    /// DM <c>findlasttext(Haystack, Needle, Start=0, End=1)</c> (BYOND ref <c>/proc/findlasttext</c>): case-insensitive,
    /// searching backwards: the last match that starts at or before Start (0 is the end; negative counts from the end)
    /// and at or after End ("The earliest position in Haystack that can be matched"; 0 or negative counts from the end):
    /// <c>findlasttext("Banana", "na", -3) == 3</c> (OpenDream test <c>findlasttext</c>).
    /// </summary>
    public static float findlasttext(object Haystack, object Needle, float Start = 0, float End = 1) => FindLastText(Haystack as string, Needle, Start, End, ignoreCase: true, chars: false);

    /// <summary>DM <c>findlasttext_char</c>: <see cref="findlasttext"/> counting characters.</summary>
    public static float findlasttext_char(object Haystack, object Needle, float Start = 0, float End = 1) => FindLastText(Haystack as string, Needle, Start, End, ignoreCase: true, chars: true);

    /// <summary>
    /// DM <c>replacetext(Haystack, Needle, Replacement, Start=1, End=0)</c> (BYOND ref <c>/proc/replacetext</c>):
    /// case-insensitive; every match within Start..End-1 is replaced. "If the Needle is a lowercase word, the
    /// replacement will be changed to uppercase or all caps if the found text is uppercase or all caps"
    /// (<c>replacetext("One on one", "one", "two") == "Two on two"</c>). A null or empty Needle puts the Replacement
    /// after every character in the range but the last (OpenDream test <c>Replacetext</c>). Replacement is text (other
    /// values are rendered as <c>"[x]"</c>); a proc as Replacement and a regex Needle are not supported yet.
    /// </summary>
    public static string replacetext(object Haystack, object Needle, object Replacement, float Start = 1, float End = 0) => ReplaceText(Haystack as string, Needle, Replacement, Start, End, ignoreCase: true, chars: false);

    /// <summary>DM <c>replacetext_char</c>: <see cref="replacetext"/> counting characters.</summary>
    public static string replacetext_char(object Haystack, object Needle, object Replacement, float Start = 1, float End = 0) => ReplaceText(Haystack as string, Needle, Replacement, Start, End, ignoreCase: true, chars: true);

    /// <summary>DM <c>replacetextEx(Haystack, Needle, Replacement, Start=1, End=0)</c> (BYOND ref <c>/proc/replacetextEx</c>): case-sensitive, no case adjustment.</summary>
    public static string replacetextEx(object Haystack, object Needle, object Replacement, float Start = 1, float End = 0) => ReplaceText(Haystack as string, Needle, Replacement, Start, End, ignoreCase: false, chars: false);

    /// <summary>DM <c>replacetextEx_char</c>: <see cref="replacetextEx"/> counting characters.</summary>
    public static string replacetextEx_char(object Haystack, object Needle, object Replacement, float Start = 1, float End = 0) => ReplaceText(Haystack as string, Needle, Replacement, Start, End, ignoreCase: false, chars: true);

    /// <summary>
    /// DM <c>splicetext(Text, Start=1, End=0, Insert="")</c> (BYOND ref <c>/proc/splicetext</c>): Text with Start to End-1
    /// replaced by Insert (<c>splicetext("banana", 3, 6, "laclav") == "balaclava"</c>). Negative positions count from the
    /// end and an End past the end is the end; a Start of 0 or past the end, or after End, is the runtime error "bad text
    /// or out of bounds" (OpenDream tests <c>splicetext</c>, <c>splicetext_runtime</c>). A null or empty Text gives Insert
    /// (null when Insert is empty and Text null).
    /// </summary>
    public static string splicetext(object Text, float Start = 1, float End = 0, object Insert = null) => SpliceText(Text as string, Start, End, Insert == null ? "" : Insert as string, chars: false);

    /// <summary>DM <c>splicetext_char</c>: <see cref="splicetext"/> counting characters.</summary>
    public static string splicetext_char(object Text, float Start = 1, float End = 0, object Insert = null) => SpliceText(Text as string, Start, End, Insert == null ? "" : Insert as string, chars: true);

    /// <summary>
    /// DM <c>spantext(Haystack, Needles, Start=1)</c> (BYOND ref <c>/proc/spantext</c>): how many consecutive characters
    /// from Start are among Needles (case-sensitive; <c>spantext("apples, oranges", ", ", 7) == 2</c>); Start 0 gives 0,
    /// a negative Start counts from the end.
    /// </summary>
    public static float spantext(object Haystack, object Needles, float Start = 1) => SpanText(Haystack as string, Needles as string, Start, chars: false);

    /// <summary>DM <c>spantext_char</c>: <see cref="spantext"/> counting characters.</summary>
    public static float spantext_char(object Haystack, object Needles, float Start = 1) => SpanText(Haystack as string, Needles as string, Start, chars: true);

    /// <summary>
    /// DM <c>nonspantext(Haystack, Needles, Start=1)</c> (BYOND ref <c>/proc/nonspantext</c>): how many consecutive
    /// characters from Start are not among Needles (<c>nonspantext("apples, oranges", ", ") == 6</c>); Start 0 or past
    /// the end gives 0, a negative Start counts from the end (OpenDream test <c>Nonspantext</c>).
    /// </summary>
    public static float nonspantext(object Haystack, object Needles, float Start = 1) => NonSpanText(Haystack as string, Needles as string, Start, chars: false);

    /// <summary>DM <c>nonspantext_char</c>: <see cref="nonspantext"/> counting characters.</summary>
    public static float nonspantext_char(object Haystack, object Needles, float Start = 1) => NonSpanText(Haystack as string, Needles as string, Start, chars: true);

    /// <summary>
    /// DM <c>uppertext(T)</c> of any value (rules 3.5: a builtin's text argument takes the object as is): text in upper
    /// case; any other value, null included, is returned unchanged (OpenDream <c>NativeProc_uppertext</c>: "return
    /// arg"). Declared before the text overload so that a call by name binds this one.
    /// </summary>
    public static object uppertext(object T) => T is string s ? s.ToUpperInvariant() : T;

    /// <summary>DM <c>uppertext(T)</c> (BYOND ref <c>/proc/uppertext</c>): invariant upper case (<c>"hi there"</c> to <c>"HI THERE"</c>); null stays null.</summary>
    public static string uppertext(string T) => T?.ToUpperInvariant();

    /// <summary>DM <c>lowertext(T)</c> of any value: text in lower case, any other value unchanged (OpenDream <c>NativeProc_lowertext</c>).</summary>
    public static object lowertext(object T) => T is string s ? s.ToLowerInvariant() : T;

    /// <summary>DM <c>lowertext(T)</c> (BYOND ref <c>/proc/lowertext</c>): invariant lower case; null stays null.</summary>
    public static string lowertext(string T) => T?.ToLowerInvariant();

    /// <summary>
    /// BYOND <c>md5(T)</c>: the MD5 hash of a text as 32 lowercase hex digits (BYOND ref <c>/proc/md5</c>; M6 slice 1, the
    /// fingerprints of tg's forensics, <c>md5(dna.unique_identity)</c>). Null for anything but a text: BYOND also hashes a
    /// file's contents, which tg's ported code never asks.
    /// </summary>
    public static string md5(object T)
    {
        if (T is not string text)
            return null;
        byte[] hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// BYOND <c>sha1(T)</c>: the SHA-1 hash of a text as 40 lowercase hex digits (BYOND ref <c>/proc/sha1</c>; D308, tg's TTS identifier of a
    /// line). Null for anything but a text, as <see cref="md5(object)"/>.
    /// </summary>
    public static string sha1(object T)
    {
        if (T is not string text)
            return null;
        byte[] hash = System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>DM <c>trimtext(Text)</c> (BYOND ref <c>/proc/trimtext</c>): without leading and trailing whitespace ("All Unicode whitespace characters are counted"); null and any other non-text value give null (OpenDream <c>NativeProc_trimtext</c>).</summary>
    public static string trimtext(object Text) => (Text as string)?.Trim();

    /// <summary>
    /// DM <c>splittext(Text, Delimiter, Start=1, End=0, include_delimiters=0)</c> (BYOND ref <c>/proc/splittext</c>): the
    /// pieces of Text between case-sensitive Delimiter matches ("a,,b,c" gives four pieces); splits are only looked for
    /// between Start and End-1, the text outside that range stays attached to the first and last pieces ("the start and
    /// end positions do NOT trim the string", OpenDream test <c>Splittext</c>); with include_delimiters the delimiters
    /// are pieces too. Empty or null text gives an empty list. A regex Delimiter is not supported yet.
    /// </summary>
    public static DmList splittext(object Text, object Delimiter, float Start = 1, float End = 0, float include_delimiters = 0) => SplitText(Text as string, Delimiter, Start, End, include_delimiters, chars: false);

    /// <summary>DM <c>splittext_char</c>: <see cref="splittext"/> counting characters.</summary>
    public static DmList splittext_char(object Text, object Delimiter, float Start = 1, float End = 0, float include_delimiters = 0) => SplitText(Text as string, Delimiter, Start, End, include_delimiters, chars: true);

    /// <summary>
    /// DM <c>jointext(List, Glue, Start=1, End=0)</c> (BYOND ref <c>/proc/jointext</c>): the items as text (objects as
    /// their names, numbers in DM format) joined by Glue; the same as <c>List.Join(Glue, Start, End)</c>. A null List
    /// gives ""; any other non-list is a runtime error. A Glue that is not text joins with nothing (OpenDream
    /// <c>/list/proc/Join</c> reads it with <c>TryGetValueAsString</c>).
    /// </summary>
    public static string jointext(object List, object Glue, float Start = 1, float End = 0)
    {
        if (List == null) return "";
        if (List is DmList list) return list.Join(Glue as string, Start, End);
        throw new DmRuntimeException("jointext(): " + KindOf(List) + " is not a list");
    }

    /// <summary>
    /// DM <c>sorttext(T1, T2, ...)</c> (BYOND ref <c>/proc/sorttext</c>): case-insensitive; 1 if the texts are in
    /// ascending order (<c>sorttext("A", "B") == 1</c>), -1 if descending, 0 otherwise (equal neighbours, mixed order,
    /// fewer than two texts). Any value is accepted (tg sorts mobs and typepaths with <c>cmp_text_asc</c>): a value that
    /// is not text, null included, sorts before every text and equal to another non-text value (OpenDream
    /// <c>NativeProc_sorttext</c>: 1 when only T2 is text, -1 when only T1 is, 0 when neither is).
    /// </summary>
    public static float sorttext(params object[] T) => SortText(T, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// DM <c>sorttextEx(T1, T2, ...)</c> (BYOND ref <c>/proc/sorttextEx</c>): <see cref="sorttext"/> case-sensitively
    /// (ordinal: upper case sorts before lower case, <c>sorttextEx("B", "a") == 1</c>; OpenDream
    /// <c>NativeProc_sorttextEx</c>, <c>StringComparison.Ordinal</c>); non-text values as in <see cref="sorttext"/>.
    /// </summary>
    public static float sorttextEx(params object[] T) => SortText(T, StringComparison.Ordinal);

    /// <summary>
    /// DM <c>cmptext(T1, T2, ...)</c> (BYOND ref <c>/proc/cmptext</c>): 1 if all arguments are equal ignoring case (and
    /// the <c>\proper</c> and <c>\improper</c> macros), else 0; a non-text argument (null or any other value) gives 0
    /// (OpenDream <c>NativeProc_cmptext</c>).
    /// </summary>
    public static float cmptext(params object[] T) => CmpText(T, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// DM <c>cmptextEx(T1, T2, ...)</c> (BYOND ref <c>/proc/cmptextEx</c>): <see cref="cmptext"/> case-sensitively
    /// (<c>cmptextEx("Hi", "HI") == 0</c>); the <c>\proper</c> and <c>\improper</c> macros are ignored as in
    /// <see cref="cmptext"/>; a non-text argument gives 0 (OpenDream <c>NativeProc_cmptextEx</c>).
    /// </summary>
    public static float cmptextEx(params object[] T) => CmpText(T, StringComparison.Ordinal);

    /// <summary>
    /// DM <c>ckey(Key)</c> (BYOND ref <c>/proc/ckey</c>): the key in canonical form, lower case with only letters a-z,
    /// digits and '@' kept (OpenDream test <c>ckey</c>: <c>ckey("...-=[];'\,./_+{}:|&lt;&gt;?`~ABC def") == "@abcdef"</c>);
    /// null and any other non-text value give null (OpenDream <c>NativeProc_ckey</c>).
    /// </summary>
    public static string ckey(object Key)
    {
        if (Key is not string key) return null;
        var sb = new StringBuilder(key.Length);
        foreach (char ch in key)
        {
            char c = char.ToLowerInvariant(ch);
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '@') sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// DM <c>ckeyEx(Text)</c> (BYOND ref <c>/proc/ckeyEx</c>): "The same text stripped of all punctuation and space.
    /// Unlike ckey(), case is preserved as are the '-' and '_' characters" (and '@', as in ckey); null and any other
    /// non-text value give null (OpenDream <c>NativeProc_ckeyEx</c>).
    /// </summary>
    public static string ckeyEx(object Text)
    {
        if (Text is not string text) return null;
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '@' || c == '-' || c == '_') sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>DM <c>ascii2text(N)</c> (BYOND ref <c>/proc/ascii2text</c>): the character with code N, any Unicode code point (<c>ascii2text(65) == "A"</c>); an invalid code is a runtime error.</summary>
    public static string ascii2text(float N)
    {
        if (float.IsNaN(N) || N < 0f || N > 0x10FFFF) throw new DmRuntimeException("ascii2text(): invalid character code " + num2text(N));
        int code = (int)N;
        if (code >= 0xD800 && code <= 0xDFFF) throw new DmRuntimeException("ascii2text(): invalid character code " + num2text(N));
        return char.ConvertFromUtf32(code);
    }

    /// <summary>
    /// DM <c>text2ascii(T, pos=1)</c> (BYOND ref <c>/proc/text2ascii</c>): the code at pos (a UTF-16 position, so a
    /// character outside the BMP gives its high surrogate, DV4); pos 0 is 1, a negative pos counts from the end, out of
    /// range (and a null or other non-text T) gives 0 (OpenDream <c>NativeProc_text2ascii</c>).
    /// </summary>
    public static float text2ascii(object T, float pos = 1) => T is string text ? TextToAscii(text, pos) : 0f;

    private static float TextToAscii(string T, float pos)
    {
        int p = TextPos(pos);
        if (p == 0) p = 1;
        else if (p < 0) p += T.Length + 1;
        if (p < 1 || p > T.Length) return 0f;
        return T[p - 1];
    }

    /// <summary>
    /// DM <c>T[i]</c> on a text (BYOND 515): the character that starts at index <paramref name="i"/> (a UTF-16 position in the port, rules 5.6;
    /// a character outside the BMP is its two units, so a loop that steps by <c>length(char)</c> walks characters as BYOND's byte loop does).
    /// An index outside the text is BYOND's "index out of bounds" runtime error.
    /// </summary>
    public static string textchar(object T, float i)
    {
        string text = T as string ?? throw new DmRuntimeException("cannot index " + (T == null ? "null" : "a non-text value") + " as text");
        int p = TextPos(i);
        if (p < 1 || p > text.Length)
            throw new DmRuntimeException("index out of bounds");
        return char.IsHighSurrogate(text[p - 1]) && p < text.Length && char.IsLowSurrogate(text[p]) ? text.Substring(p - 1, 2) : text.Substring(p - 1, 1);
    }

    /// <summary>DM <c>text2ascii_char(T, pos=1)</c>: the code of the character at pos (counting characters; <c>text2ascii_char("abcdéfg", 6) == 102</c>).</summary>
    public static float text2ascii_char(object T, float pos = 1) => T is string text ? TextToAsciiChar(text, pos) : 0f;

    private static float TextToAsciiChar(string T, float pos)
    {
        var u = new DmTextUnits(T, characters: true);
        int p = TextPos(pos);
        if (p == 0) p = 1;
        else if (p < 0) p += u.Length + 1;
        if (p < 1 || p > u.Length) return 0f;
        string ch = u.UnitText(p - 1);
        return ch.Length == 2 ? char.ConvertToUtf32(ch[0], ch[1]) : ch[0];
    }

    /// <summary>
    /// DM <c>num2text(N, SigFig=6)</c> (BYOND ref <c>/proc/num2text</c>; rules 5.5): <c>%g</c> style with SigFig
    /// significant digits, invariant culture: "scientific notation will only be used when there are more than 6 digits"
    /// (<c>num2text(1000000) == "1e+06"</c>, <c>num2text(999999) == "999999"</c>, OpenDream test <c>num2text</c>).
    /// </summary>
    public static string num2text(float N, float SigFig = 6) => DmTextRender.FormatSignificant(N, TextPos(SigFig));

    /// <summary>
    /// DM <c>num2text(N, Digits, Radix)</c>: the integer part of N in base Radix (2 to 36, lower-case digits), left-padded
    /// with zeros to Digits digits (<c>num2text(11, 2, 16) == "0b"</c>, <c>num2text(343, 0, 7) == "1000"</c>,
    /// <c>num2text(-20, 0, 16) == "-14"</c>).
    /// </summary>
    public static string num2text(float N, float Digits, float Radix)
    {
        int radix = TextPos(Radix);
        if (radix < 2 || radix > 36) throw new DmRuntimeException("num2text(): invalid radix " + num2text(Radix));
        double t = float.IsNaN(N) ? 0d : Math.Truncate((double)N);
        long value = t >= long.MaxValue ? long.MaxValue : t <= -long.MaxValue ? -long.MaxValue : (long)t;
        bool negative = value < 0;
        if (negative) value = -value;
        var digits = new StringBuilder();
        do
        {
            int digit = (int)(value % radix);
            digits.Insert(0, (char)(digit < 10 ? '0' + digit : 'a' + digit - 10));
            value /= radix;
        }
        while (value > 0);
        int width = TextPos(Digits);
        while (digits.Length < width) digits.Insert(0, '0');
        return negative ? "-" + digits : digits.ToString();
    }

    /// <summary>
    /// DM <c>text2num(T)</c> (BYOND ref <c>/proc/text2num</c>; rules 5.5): the number at the start of T, after
    /// surrounding whitespace ("Any non-numeric text following the initial portion will be ignored. If there is no
    /// initial numeric portion, the result is null": <c>text2num("4 2") == 4</c>, <c>text2num(".4") == 0.4</c>,
    /// <c>text2num("0xA") == 10</c>, <c>text2num("nan")</c> is null; OpenDream test <c>text2num</c>), with BYOND's decimal
    /// exponent (<c>text2num("2e+005") == 200000</c>); a number passes through, null stays null, any other value is a
    /// runtime error. Invariant culture.
    /// </summary>
    public static float? text2num(object T) => text2num(T, 10f);

    /// <summary>DM <c>text2num(T, radix)</c>: the same in base radix, 2 to 36 (<c>text2num("F.A", 16) == 15.625</c>, <c>text2num("Z", 36) == 35</c>).</summary>
    public static float? text2num(object T, float radix)
    {
        switch (T)
        {
            case null:
                return null;
            case string s:
                int r = TextPos(radix);
                if (r < 2 || r > 36) throw new DmRuntimeException("text2num(): invalid radix " + num2text(radix));
                double? parsed = ParseNumberPrefix(s.Trim(), r);
                return parsed.HasValue ? (float)parsed.Value : null;
            default:
                if (TryGetNumber(T, out float f)) return f;
                throw new DmRuntimeException("text2num(): invalid argument " + KindOf(T));
        }
    }

    /// <summary>
    /// DM <c>html_encode(PlainText)</c> (BYOND ref <c>/proc/html_encode</c>): <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>,
    /// <c>"</c> and <c>'</c> as entities (<c>&amp;amp;</c>, <c>&amp;lt;</c>, <c>&amp;gt;</c>, <c>&amp;quot;</c>,
    /// <c>&amp;#39;</c>, the set OpenDream's <c>HttpUtility.HtmlEncode</c> escapes); other characters, Cyrillic
    /// included, stay as they are. null gives ""; any other value is encoded as its text <c>"[x]"</c> (OpenDream
    /// <c>NativeProc_html_encode</c> stringifies its argument).
    /// </summary>
    public static string html_encode(object PlainText) => HtmlEncode(PlainText as string ?? (PlainText == null ? "" : str(PlainText)));

    private static string HtmlEncode(string PlainText)
    {
        if (string.IsNullOrEmpty(PlainText)) return "";
        var sb = new StringBuilder(PlainText.Length + 16);
        foreach (char c in PlainText)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&#39;"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>DM <c>html_decode(HtmlText)</c> (BYOND ref <c>/proc/html_decode</c>): named and numeric entities back to characters; null gives "", any other value its text <c>"[x]"</c> (OpenDream stringifies it).</summary>
    public static string html_decode(object HtmlText)
    {
        string text = HtmlText as string ?? (HtmlText == null ? "" : str(HtmlText));
        return text.Length == 0 ? "" : System.Net.WebUtility.HtmlDecode(text);
    }

    /// <summary>
    /// DM <c>json_encode(Value, flags)</c> (BYOND ref <c>/proc/json_encode</c>; JSON_PRETTY_PRINT indents): numbers,
    /// text, null; a list as an array, or as an object keyed by the items' text when any item has an associated value
    /// (OpenDream test <c>JsonEncode</c>: <c>json_encode(list("A" = 3, "B" = 5)) == {"A":3,"B":5}</c>); a matrix as its six
    /// components; a typepath and any other object as its text; NaN and infinities as <c>{"__number__":"NaN"}</c> and
    /// so on. Nesting deeper than 20 is written as null. Uses System.Text.Json.
    /// </summary>
    public static string json_encode(object Value, float flags = 0) => DmJson.Encode(Value, pretty: TextPos(flags) == 1);

    /// <summary>
    /// DM <c>json_decode(JSON, flags)</c> (BYOND ref <c>/proc/json_decode</c>): arrays become lists, objects associative
    /// lists with text keys, numbers DM numbers, true and false 1 and 0, <c>{"__number__": ...}</c> the special numbers;
    /// invalid JSON or a JSON that is not text (null included) is a runtime error (OpenDream
    /// <c>NativeProc_json_decode</c>: "Unknown value").
    /// </summary>
    public static object json_decode(object JSON, float flags = 0)
        => JSON == null || JSON is string ? DmJson.Decode((string)JSON) : throw new DmRuntimeException("json_decode(): " + KindOf(JSON) + " is not JSON");

    /// <summary>
    /// DM <c>params2list(Params)</c> (BYOND ref <c>/proc/params2list</c>): "a=1;b=2" (or with '&amp;') as an associative
    /// list of text; a key without '=' gets "", a repeated key a list of its values (OpenDream test <c>params2list</c>);
    /// keys and values are URL-decoded. A null Params, or any other value that is not text, gives an empty list
    /// (OpenDream <c>NativeProc_params2list</c>).
    /// </summary>
    public static DmList params2list(object Params) => DmJson.ParamsToList(Params as string);

    /// <summary>
    /// DM <c>list2params(List)</c> (BYOND ref <c>/proc/list2params</c>): the items joined with '&amp;', each
    /// <c>key=value</c> when it has an associated value, URL-encoded; a non-list gives "".
    /// </summary>
    public static string list2params(object List) => List is DmList list ? DmJson.ListToParams(list) : "";

    /// <summary>
    /// DM <c>time2text(timestamp, format, timezone)</c> (BYOND ref <c>/proc/time2text</c>): the timestamp (deciseconds
    /// since 00:00 UTC, January 1, 2000, such as <c>world.realtime</c>) formatted with the fields YYYY, YY, Month, MMM,
    /// MM, Day, DDD, DD, hh, mm, ss (default "DDD MMM DD hh:mm:ss YYYY"). A timestamp from 0 to 864000 is a time of the
    /// current date ("This way time2text() can return accurate results for world.timeofday"), the current date being
    /// that of <c>world.realtime</c>. No wall clock and no host time zone (DV3): the world's local time is UTC unless
    /// <paramref name="timezone"/> gives an offset in hours.
    /// </summary>
    public static string time2text(float timestamp, string format = null, float? timezone = null) => DmTime.Format(timestamp, format, timezone);

    /// <summary>
    /// DM <c>rgb(R, G, B)</c>, <c>rgb(R, G, B, A)</c>, <c>rgb(x, y, z, space=...)</c> (BYOND ref <c>/proc/rgb</c>):
    /// "#rrggbb" or, with A, "#rrggbbaa", lower-case hex; components clamped to 0..255 and truncated. <paramref
    /// name="space"/> is a COLORSPACE_* constant (tg passes it by name): HSV and HSL take hue 0-360 and the others 0-100
    /// (OpenDream <c>SharedOperations.ParseRgb</c> and test <c>rgb</c>: <c>rgb(291.70734, 56.164383, 85.882355,
    /// space=COLORSPACE_HSV) == "#ca60db"</c>); an unknown space is RGB.
    /// </summary>
    public static string rgb(float R, float G, float B, float? A = null, float? space = null) => DmColor.Rgb(R, G, B, A, space);

    /// <summary>
    /// DM <c>rgb2num(color, space)</c> (BYOND ref <c>/proc/rgb2num</c>): the components of "#rgb", "#rgba", "#rrggbb" or
    /// "#rrggbbaa" as a list (alpha appended when the color has one), in RGB, HSV or HSL; a null color is "#FFFFFF" (the
    /// default argument); an unparsable color is the runtime error "bad color".
    /// </summary>
    public static DmList rgb2num(string color, float space = 0) => DmColor.RgbToNum(color, space);

    /// <summary>
    /// DM <c>gradient(A, B, index)</c> (BYOND ref <c>/proc/gradient</c>) for its two-colour form, the one tg calls (gaps P5: the
    /// meth crystal's tint): the colour at <paramref name="index"/> (0 is A, 1 is B, clamped) of the linear RGB(A) blend, as
    /// "#rrggbb" ("#rrggbbaa" when either colour has alpha). The gradient-list form and the colour-space keyword are not ported.
    /// </summary>
    public static string gradient(string A, string B, float index)
    {
        DmList a = rgb2num(A), b = rgb2num(B);
        float t = index < 0 ? 0 : index > 1 ? 1 : index;
        float lerp(int i, float fallback) => (i <= a.len ? Dm.num(a[i]) : fallback) * (1 - t) + (i <= b.len ? Dm.num(b[i]) : fallback) * t;
        bool alpha = a.len > 3 || b.len > 3;
        return alpha ? rgb(lerp(1, 0), lerp(2, 0), lerp(3, 0), lerp(4, 255)) : rgb(lerp(1, 0), lerp(2, 0), lerp(3, 0));
    }

    // ---- implementations ----

    // A text-proc position argument, truncated (NaN and huge values land outside every text).
    private static int TextPos(float value)
    {
        if (float.IsNaN(value)) return int.MinValue / 2;
        if (value >= int.MaxValue / 2) return int.MaxValue / 2;
        if (value <= int.MinValue / 2) return int.MinValue / 2;
        return (int)value;
    }

    // A text needle: text, or null; a /regex needle is handled by its caller (findtext and replacetext hand the search to the
    // regex, BYOND ref findtext and replacetext; OpenDream NativeProc_findtext and NativeProc_replacetext).
    private static string NeedleText(object needle, string proc)
    {
        if (needle == null || needle is string) return (string)needle;
        if (TryGetNumber(needle, out _)) return null;
        throw new DmRuntimeException(proc + "(): a " + KindOf(needle) + " needle is not text or a /regex");
    }

    private static string CopyText(string T, float Start, float End, bool chars)
    {
        int end = TextPos(End);
        if (T == null) return end == 0 ? null : "";
        var u = new DmTextUnits(T, chars);
        int len = u.Length;
        int start = TextPos(Start);
        if (end <= 0) end += len + 1;
        else if (end > len + 1) end = len + 1;
        if (start == 0) return "";
        if (start < 0) start += len + 1;
        if (start < 1) start = 1;
        if (start > len || end <= start) return "";
        return u.Sub(start - 1, end - 1);
    }

    private static float FindText(string haystack, object needleValue, float Start, float End, bool ignoreCase, bool chars)
    {
        // a regex needle is the same as Needle.Find(Haystack, Start, End) (BYOND ref findtext); the regex's own flags set the case
        if (needleValue is regex findRegex)
            return num(chars ? findRegex.Find_char(haystack, Start, End) : findRegex.Find(haystack, Start, End));
        string needle = NeedleText(needleValue, ignoreCase ? "findtext" : "findtextEx");
        int failures = (haystack == null ? 1 : 0) + (needle == null ? 1 : 0);
        if (failures > 0 || haystack.Length == 0 || needle.Length == 0) return failures == 2 ? 1f : 0f;
        var u = new DmTextUnits(haystack, chars);
        int len = u.Length;
        int start = TextPos(Start);
        int end = TextPos(End);
        if (start > len || start == 0) return 0f;
        if (start < 0) start = Math.Max(len + start + 1, 1);
        if (end < 0) end = len + end + 1;
        if (end == 0 || end > len + 1) end = len + 1;
        if (end <= start) return 0f;
        int from = u.Offset(start - 1);
        int to = u.Offset(end - 1);
        int found = haystack.IndexOf(needle, from, to - from, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        return found < 0 ? 0f : u.UnitAt(found) + 1;
    }

    private static float FindLastText(string haystack, object needleValue, float Start, float End, bool ignoreCase, bool chars)
    {
        string needle = NeedleText(needleValue, ignoreCase ? "findlasttext" : "findlasttextEx");
        int failures = (haystack == null ? 1 : 0) + (needle == null ? 1 : 0);
        if (failures > 0 || haystack.Length == 0 || needle.Length == 0) return failures == 2 ? 1f : 0f;
        var u = new DmTextUnits(haystack, chars);
        int len = u.Length;
        int start = TextPos(Start);
        int end = TextPos(End);
        int latest = start > 0 ? start : start == 0 ? len : len + start + 1;
        int earliest = end > 0 ? end : len + end + 1;
        latest = Math.Min(latest, len - u.LengthOf(needle) + 1);
        earliest = Math.Max(earliest, 1);
        StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (int p = latest; p >= earliest; p--)
        {
            if (haystack.AsSpan(u.Offset(p - 1)).StartsWith(needle, comparison)) return p;
        }
        return 0f;
    }

    private static string ReplaceText(string haystack, object needleValue, object replacementValue, float Start, float End, bool ignoreCase, bool chars)
    {
        // a regex needle is the same as Needle.Replace(Haystack, Replacement, Start, End) (BYOND ref replacetext); the regex's own
        // flags set the case and whether every match is replaced
        if (needleValue is regex replaceRegex)
            return (chars ? replaceRegex.Replace_char(haystack, replacementValue, Start, End) : replaceRegex.Replace(haystack, replacementValue, Start, End)) as string;
        string needle = NeedleText(needleValue, ignoreCase ? "replacetext" : "replacetextEx");
        if (haystack == null) return null;
        var u = new DmTextUnits(haystack, chars);
        int len = u.Length;
        int start = TextPos(Start);
        int end = TextPos(End);
        if (start == 0) return haystack;
        if (start < 0) start = Math.Max(start + len + 1, 1);
        if (end <= 0) end = Math.Max(end + len + 1, start);
        if (end > len + 1) end = len + 1;
        string replacement = replacementValue as string ?? (replacementValue == null ? "" : str(replacementValue));
        if (string.IsNullOrEmpty(needle))
        {
            if (replacement.Length == 0) return haystack;
            if (start == 1) start = 2;
            end = Math.Min(end, len);
            var spread = new StringBuilder(haystack.Length + replacement.Length * len);
            for (int i = 0; i < len; i++)
            {
                spread.Append(u.UnitText(i));
                if (i >= start - 2 && i < end - 1) spread.Append(replacement);
            }
            return spread.ToString();
        }
        if (start > len || end <= start) return haystack;
        int from = u.Offset(start - 1);
        int to = u.Offset(end - 1);
        StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        bool adjustCase = ignoreCase && IsLowercaseWord(needle);
        var sb = new StringBuilder(haystack.Length);
        sb.Append(haystack, 0, from);
        int at = from;
        while (at <= to - needle.Length)
        {
            int found = haystack.IndexOf(needle, at, to - at, comparison);
            if (found < 0) break;
            sb.Append(haystack, at, found - at);
            sb.Append(adjustCase ? MatchCase(replacement, haystack.Substring(found, needle.Length)) : replacement);
            at = found + needle.Length;
        }
        sb.Append(haystack, at, haystack.Length - at);
        return sb.ToString();
    }

    // "If the Needle is a lowercase word": it has letters and no upper-case letter.
    private static bool IsLowercaseWord(string needle)
    {
        bool letters = false;
        foreach (char c in needle)
        {
            if (char.IsUpper(c)) return false;
            if (char.IsLetter(c)) letters = true;
        }
        return letters;
    }

    // The replacement in the case of the found text: all caps when the found text has two or more letters, all upper
    // case; capitalized when its first letter is upper case; as given otherwise.
    private static string MatchCase(string replacement, string found)
    {
        int letters = 0, upper = 0;
        char firstLetter = '\0';
        foreach (char c in found)
        {
            if (!char.IsLetter(c)) continue;
            if (letters == 0) firstLetter = c;
            letters++;
            if (char.IsUpper(c)) upper++;
        }
        if (letters >= 2 && upper == letters) return replacement.ToUpperInvariant();
        if (letters > 0 && char.IsUpper(firstLetter) && replacement.Length > 0)
            return char.ToUpperInvariant(replacement[0]) + replacement.Substring(1);
        return replacement;
    }

    private static string SpliceText(string text, float Start, float End, string insert, bool chars)
    {
        if (text == null) return string.IsNullOrEmpty(insert) ? null : insert;
        if (text.Length == 0) return insert ?? "";
        var u = new DmTextUnits(text, chars);
        int len = u.Length;
        int start = TextPos(Start);
        int end = TextPos(End);
        if (end == 0 || end > len + 1) end = len + 1;
        if (start < 0) start = Math.Max(start + len + 1, 1);
        if (end < 0) end = Math.Min(end + len + 1, len);
        if (start == 0 || start > len || start > end) throw new DmRuntimeException("splicetext(): bad text or out of bounds");
        return u.Sub(0, start - 1) + insert + u.Sub(end - 1, len);
    }

    private static float SpanText(string haystack, string needles, float Start, bool chars)
    {
        if (haystack == null || needles == null) return 0f;
        int start = TextPos(Start);
        if (start == 0) return 0f;
        var u = new DmTextUnits(haystack, chars);
        int len = u.Length;
        if (start < 0) start = Math.Max(start + len + 1, 1);
        int count = 0;
        for (int p = start; p <= len; p++)
        {
            if (needles.IndexOf(u.UnitText(p - 1), StringComparison.Ordinal) < 0) break;
            count++;
        }
        return count;
    }

    private static float NonSpanText(string haystack, string needles, float Start, bool chars)
    {
        if (haystack == null) return 0f;
        if (needles == null) return 1f;
        var u = new DmTextUnits(haystack, chars);
        int len = u.Length;
        int start = TextPos(Start);
        if (start == 0 || start > len) return 0f;
        if (start < 0) start = Math.Max(start + len + 1, 1);
        int count = 0;
        for (int p = start; p <= len; p++)
        {
            if (needles.Length > 0 && needles.IndexOf(u.UnitText(p - 1), StringComparison.Ordinal) >= 0) break;
            count++;
        }
        return count;
    }

    private static DmList SplitText(string text, object delimiterValue, float Start, float End, float includeDelimiters, bool chars)
    {
        var result = new DmList();
        if (string.IsNullOrEmpty(text)) return result;
        if (delimiterValue != null && delimiterValue is not string && !TryGetNumber(delimiterValue, out _))
            throw new DmRuntimeException("splittext(): a " + KindOf(delimiterValue) + " delimiter (a /regex) is not supported yet");
        if (delimiterValue is not string delimiter) return result;
        var u = new DmTextUnits(text, chars);
        int len = u.Length;
        int start = TextPos(Start);
        int end = TextPos(End);
        if (start < 0) start += len + 1;
        if (end < 0) end += len + 1;
        if (start < 1) start = 1;
        if (start > len + 1) start = len + 1;
        if (end == 0 || end > len + 1) end = len + 1;
        if (end < start) end = start;
        string before = u.Sub(0, start - 1);
        string middle = u.Sub(start - 1, end - 1);
        string after = u.Sub(end - 1, len);
        // an empty delimiter splits the text into its units, code units here and characters for splittext_char (tg's PIN puzzle reads
        // a password's digits so, puzzle_pieces.dm:591; OpenDream's Split("") would keep it whole; dm-conformance.md)
        string[] pieces;
        if (delimiter.Length == 0)
        {
            var mu = new DmTextUnits(middle, chars);
            pieces = new string[Math.Max(mu.Length, 1)];
            pieces[0] = "";
            for (int unit = 0; unit < mu.Length; unit++)
                pieces[unit] = mu.UnitText(unit);
        }
        else
            pieces = middle.Split(delimiter);
        bool include = TextPos(includeDelimiters) != 0;
        for (int i = 0; i < pieces.Length; i++)
        {
            string piece = pieces[i];
            if (i == 0) piece = before + piece;
            if (i == pieces.Length - 1) piece += after;
            result.AppendItem(piece);
            if (include && i < pieces.Length - 1) result.AppendItem(delimiter);
        }
        return result;
    }

    // sorttext and sorttextEx: 1 ascending, -1 descending, 0 otherwise, over every neighbouring pair.
    private static float SortText(object[] T, StringComparison comparison)
    {
        T ??= OneNull();
        if (T.Length < 2) return 0f;
        bool ascending = true, descending = true;
        for (int i = 0; i + 1 < T.Length; i++)
        {
            int c = CompareSortText(T[i] as string, T[i + 1] as string, comparison);
            if (c >= 0) ascending = false;
            if (c <= 0) descending = false;
        }
        return ascending ? 1f : descending ? -1f : 0f;
    }

    // sorttext's order of two values: a value that is not text (null included, OpenDream TryGetValueAsString) before
    // every text, two of them equal; texts ordinal, case-insensitive or not.
    private static int CompareSortText(string a, string b, StringComparison comparison)
    {
        if (a == null) return b == null ? 0 : -1;
        if (b == null) return 1;
        return string.Compare(a, b, comparison);
    }

    // cmptext and cmptextEx: 1 when every argument is text equal to the first (the \proper and \improper markers
    // ignored), else 0; no arguments give 1.
    private static float CmpText(object[] T, StringComparison comparison)
    {
        T ??= OneNull();
        if (T.Length == 0) return 1f;
        if (T[0] is not string firstText) return 0f;
        string first = DmTextRender.StripMarkers(firstText);
        for (int i = 1; i < T.Length; i++)
        {
            if (T[i] is not string other || !string.Equals(first, DmTextRender.StripMarkers(other), comparison)) return 0f;
        }
        return 1f;
    }

    // OpenDream DreamProcNativeHelpers.StringToDouble: an optional sign, "0x" switching base 10 to 16, digits of the
    // radix with at most one '.', stopping at the first other character; null when there is no digit.
    private static double? ParseNumberPrefix(string value, int radix)
    {
        if (string.IsNullOrEmpty(value)) return null;
        int i = 0;
        bool negative = value[0] == '-';
        if (negative || value[0] == '+') i++;
        if (string.CompareOrdinal(value, i, "0x", 0, 2) == 0 && (radix == 10 || radix == 16))
        {
            radix = 16;
            i += 2;
        }
        int letterDigits = Math.Max(radix - 10, 0);
        double? result = null;
        int fractionDigit = 0;
        for (; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '.')
            {
                if (fractionDigit != 0) break;
                fractionDigit = 1;
                continue;
            }
            int digit;
            if (c >= '0' && c <= '9' && c - '0' < radix) digit = c - '0';
            else if (c >= 'A' && c < 'A' + letterDigits) digit = c - 'A' + 10;
            else if (c >= 'a' && c < 'a' + letterDigits) digit = c - 'a' + 10;
            else break;
            result ??= 0;
            if (fractionDigit == 0)
            {
                result = result * radix + digit;
            }
            else
            {
                result += digit / Math.Pow(radix, fractionDigit);
                fractionDigit++;
            }
        }
        // BYOND reads a decimal exponent after the digits ("2e+005" is 200000, as BYOND writes large numbers; "1e5", "2.5E-3");
        // an "e" with no digits after it ends the number (M3 I1b).
        if (result != null && radix == 10 && i < value.Length && (value[i] == 'e' || value[i] == 'E'))
        {
            int j = i + 1;
            bool negativeExponent = false;
            if (j < value.Length && (value[j] == '+' || value[j] == '-'))
            {
                negativeExponent = value[j] == '-';
                j++;
            }
            int first = j, exponent = 0;
            for (; j < value.Length && value[j] >= '0' && value[j] <= '9'; j++)
                exponent = Math.Min(exponent * 10 + (value[j] - '0'), 1000);
            if (j > first) result *= Math.Pow(10, negativeExponent ? -exponent : exponent);
        }
        if (negative && result != null) result = -result;
        return result;
    }
}
