using System.Globalization;
using System.Reflection;
using System.Text;

namespace Anomaly.Core.DmShim;

/// <summary>
/// How DM renders values as text (docs/ai/translation-rules.md 5.2-5.5; BYOND ref "text", "macros (text)", <c>/proc/text</c>,
/// <c>/proc/num2text</c>): the embedded-expression renderer behind <c>Dm.str</c>, <c>Dm.text</c> and <c>I18n.T</c>, the
/// text macros, DM's number format, and the private-use characters that stand for <c>\proper</c> and <c>\improper</c>
/// inside stored text. Culture-invariant everywhere (rules 3.9). Owner: values.
/// </summary>
internal static class DmTextRender
{
    /// <summary>The character that <c>\proper</c> becomes inside text (rules 5.4; display code strips it).</summary>
    internal const char ProperMarker = '';

    /// <summary>The character that <c>\improper</c> becomes inside text.</summary>
    internal const char ImproperMarker = '';

    // ---- numbers ----

    /// <summary>
    /// A number as an embedded expression renders it (<c>"[x]"</c>): six significant digits in <c>%g</c> style with a
    /// lowercase two-digit exponent (<c>1e-06</c>, <c>1e+08</c>, <c>167.288</c>), except that a whole number from one
    /// million up to 2^24 keeps all its digits (<c>"[16777216]" == "16777216"</c>, <c>"[16777218]" ==
    /// "1.67772e+07"</c>; OpenDream <c>DreamValue.Stringify</c> and its BYOND-checked test <c>Math/Text.dm</c>);
    /// infinities are <c>inf</c> and <c>-inf</c>, NaN is <c>nan</c> (OpenDream test <c>stringify_inf</c>).
    /// </summary>
    internal static string FormatNumber(float f)
    {
        if (float.IsNaN(f)) return "nan";
        if (float.IsInfinity(f)) return f > 0 ? "inf" : "-inf";
        if (f > 16777216f) return FormatSignificant(f, 6);
        if (f >= 1000000f && f == MathF.Truncate(f)) return FormatSignificant(f, 8);
        return FormatSignificant(f, 6);
    }

    /// <summary>
    /// A number in <c>%g</c> style with <paramref name="digits"/> significant digits (at least 1): fixed notation when
    /// the exponent is from -5 to digits-1, else <c>d.ddde+XX</c>; trailing zeros removed; invariant culture
    /// (<c>num2text(999.23123, 2) == "1e+03"</c>, <c>num2text(999.23123, 25) == "999.231201171875"</c>, OpenDream test
    /// <c>num2text</c>).
    /// </summary>
    internal static string FormatSignificant(float f, int digits)
    {
        if (float.IsNaN(f)) return "nan";
        if (float.IsInfinity(f)) return f > 0 ? "inf" : "-inf";
        if (digits < 1) digits = 1;
        if (digits > 1000) digits = 1000; // .NET caps the precision; a float has no more than 112 significant digits
        string s = f.ToString("G" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        int e = s.IndexOf('E');
        if (e < 0) return s;
        // .NET writes "1E+06"; C's %g writes "1e+06" (at least two exponent digits in both).
        return s.Substring(0, e) + "e" + s.Substring(e + 1);
    }

    // ---- values ----

    /// <summary>
    /// A value as <c>"[x]"</c> renders it: null as "", text as is, numbers by <see cref="FormatNumber"/>, a typepath as
    /// its path, a list as <c>/list</c>; an object with a name (an atom, a datum with a <c>name</c> var) as its name
    /// preceded by "the " when the name is improper ("An object expression will display the object's name preceded by
    /// the text macro \the or \The if no other article has been specified. Capitalization of the article is inferred
    /// from context.", BYOND ref "text"; the port capitalizes it at the start of the text); a datum's
    /// <c>operator""</c> (<c>operator__text</c>, rules 7.10) decides its own text (atoms are not searched for one: tg
    /// defines it only on <c>/datum/timedevent</c>); any other object is its DM type path.
    /// </summary>
    internal static string Embedded(object x, bool atStart)
    {
        switch (x)
        {
            case null:
                return "";
            case string s:
                return s;
            case Type t:
                return TypePathText(t);
            case DmList:
                return "/list";
            case DmFileRef file: // file(Path) renders as its path (BYOND ref /proc/file; W3.0 file seam)
                return file.Path;
            case DmResource resource: // a resource literal renders as its file's path
                return resource.Path;
        }
        if (Dm.TryGetNumber(x, out float f)) return FormatNumber(f);
        if (x is not IDmAtom && Dm.TryCallOperator(x, "operator__text", null, out object text)) return text as string ?? Embedded(text, atStart);
        if (TryGetName(x, out string name))
        {
            string shown = StripMarkers(name);
            return IsProper(name) ? shown : (atStart ? "The " : "the ") + shown;
        }
        return TypePathText(x.GetType());
    }

    /// <summary>
    /// A value as text without an article: what <c>jointext</c>, <c>L.Join</c>, <c>json_encode</c> keys and the
    /// embedded expression after <c>\the</c>/<c>\a</c> use (OpenDream <c>Stringify</c> / <c>GetDisplayName</c>): an
    /// object with a name is its name with the proper/improper markers removed.
    /// </summary>
    internal static string Plain(object x)
    {
        if (x is string s) return s;
        if (x is DmFileRef file) return file.Path;
        if (x is DmResource resource) return resource.Path;
        if (x is IDmAtom atom) return StripMarkers(atom.name ?? "");
        if (x != null && x is not Type && x is not DmList && !Dm.TryGetNumber(x, out _))
        {
            if (Dm.TryCallOperator(x, "operator__text", null, out object text)) return text as string ?? Plain(text);
            if (TryGetName(x, out string name)) return StripMarkers(name);
        }
        return Embedded(x, atStart: false);
    }

    /// <summary>The DM path of a typepath value (the type registry's <c>DmTypes.PathOf</c>), else its C# name.</summary>
    internal static string TypePathText(Type t) => DmTypes.PathOf(t) ?? t.FullName;

    /// <summary>The first <c>[DmPath]</c> on a class chain (a translated class carries it on one part), or null.</summary>
    internal static string DmPathOf(Type type)
    {
        for (Type t = type; t != null; t = t.BaseType)
        {
            if (Attribute.GetCustomAttribute(t, typeof(DmPathAttribute), inherit: false) is DmPathAttribute path) return path.Path;
        }
        return null;
    }

    // The name of an object that has one: an atom's name (IDmAtom), or a datum's text var "name".
    private static bool TryGetName(object x, out string name)
    {
        if (x is IDmAtom atom)
        {
            name = atom.name ?? "";
            return true;
        }
        if (x is IDmObject || x is exception)
        {
            Type t = x.GetType();
            FieldInfo field = t.GetField("name", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.GetValue(x) is string fieldName)
            {
                name = fieldName;
                return true;
            }
            PropertyInfo property = t.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.GetIndexParameters().Length == 0 && property.GetValue(x) is string propertyName)
            {
                name = propertyName;
                return true;
            }
        }
        name = null;
        return false;
    }

    // The gender of an object for \he, \his, \a ... (an atom's gender, or a datum's text var "gender"), or null.
    private static string GenderOf(object x)
    {
        if (x is IDmAtom atom) return atom.gender;
        if (x is not IDmObject) return null;
        FieldInfo field = x.GetType().GetField("gender", BindingFlags.Public | BindingFlags.Instance);
        return field?.GetValue(x) as string;
    }

    /// <summary>
    /// Whether a name is a proper noun (BYOND ref "macros (text)"; OpenDream <c>DreamObject.StringIsProper</c>): an empty
    /// name, a <c>\proper</c> name or one starting with whitespace or a capital letter is proper, unless it is marked
    /// <c>\improper</c>.
    /// </summary>
    internal static bool IsProper(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        char first = name[0];
        if (first == ProperMarker) return true;
        if (first == ImproperMarker) return false;
        return char.IsWhiteSpace(first) || char.IsUpper(first);
    }

    /// <summary>Text without the <c>\proper</c> and <c>\improper</c> markers.</summary>
    internal static string StripMarkers(string s)
    {
        if (s == null || (s.IndexOf(ProperMarker) < 0 && s.IndexOf(ImproperMarker) < 0)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c != ProperMarker && c != ImproperMarker) sb.Append(c);
        }
        return sb.ToString();
    }

    // ---- templates: DM text with embedded expressions and text macros ----

    private enum Prefix
    {
        None,
        LowerThe,
        UpperThe,
        LowerA,
        UpperA,
        LowerRoman,
        UpperRoman,
        Icon,
        Ref,
    }

    /// <summary>
    /// <c>Dm.text(template, args)</c> (rules 5.2): the template is the DM literal in DM syntax with each embedded
    /// expression replaced by <c>[]</c>; each <c>[]</c> takes the next argument (missing ones are null). Escapes and
    /// text macros follow BYOND ref "macros (text)" with OpenDream's parser rules (DMParserHelper.ExpressionFromString):
    /// a macro is a backslash and a run of letters; <c>\the</c>, <c>\The</c>, <c>\a</c>, <c>\an</c>, <c>\A</c>,
    /// <c>\An</c> take one following space and apply to the next <c>[]</c>, which then shows only an object's or a
    /// text's name (a number shows nothing); <c>\roman</c>, <c>\Roman</c>, <c>\icon</c>, <c>\ref</c> apply to the next
    /// <c>[]</c>; <c>\he</c>, <c>\his</c>, <c>\him</c>, <c>\himself</c>, <c>\hers</c> (and capitals), <c>\s</c> and
    /// <c>\th</c> apply to the previous one; <c>\proper</c> and <c>\improper</c> become marker characters and drop the
    /// spaces after them; <c>\n</c>, <c>\t</c>, <c>\"</c>, <c>\\</c>, <c>\[</c>, <c>\]</c>, <c>\'</c>, <c>\ </c>,
    /// <c>\xNN</c>, <c>\uNNNN</c>, <c>\UNNNNNN</c> insert characters, <c>\&lt;</c> and <c>\&gt;</c> the entities
    /// <c>&amp;lt;</c> and <c>&amp;gt;</c>, <c>\...</c> nothing (it only suppresses a final newline of output).
    /// </summary>
    internal static string Template(string template, object[] args)
    {
        if (template == null) return "";
        args ??= Dm.OneNull();
        var sb = new StringBuilder(template.Length + 16);
        int next = 0;            // the next argument
        int lastStart = -1;      // where the previous []'s text starts in sb
        int lastEnd = -1;
        object last = null;      // the previous [] argument
        bool haveLast = false;
        Prefix prefix = Prefix.None;
        int n = template.Length;
        for (int i = 0; i < n; i++)
        {
            char c = template[i];
            if (c == '[')
            {
                int j = i + 1;
                while (j < n && (template[j] == ' ' || template[j] == '\t')) j++;
                if (j < n && template[j] == ']')
                {
                    object arg = next < args.Length ? args[next] : null;
                    next++;
                    int start = sb.Length;
                    RenderArgument(sb, arg, prefix);
                    if (prefix != Prefix.Ref)
                    {
                        last = arg;
                        haveLast = true;
                        lastStart = start;
                        lastEnd = sb.Length;
                    }
                    prefix = Prefix.None;
                    i = j;
                    continue;
                }
                sb.Append(c);
                continue;
            }
            if (c != '\\' || i == n - 1)
            {
                sb.Append(c);
                continue;
            }
            char e = template[++i];
            switch (e)
            {
                case 'x':
                    i = AppendCodePoint(sb, template, i, 2);
                    continue;
                case 'u':
                    i = AppendCodePoint(sb, template, i, 4);
                    continue;
                case 'U':
                    i = AppendCodePoint(sb, template, i, 6);
                    continue;
                case '<':
                    sb.Append("&lt;");
                    continue;
                case '>':
                    sb.Append("&gt;");
                    continue;
                case '.':
                    if (i + 2 < n && template[i + 1] == '.' && template[i + 2] == '.') i += 2;  // \... suppresses a final newline
                    else sb.Append('.');
                    continue;
                case '\n':
                    while (i + 1 < n && (template[i + 1] == ' ' || template[i + 1] == '\t')) i++;
                    continue;
            }
            if (!char.IsLetter(e))
            {
                sb.Append(e);   // \" \\ \[ \] \' and \(space) insert the character itself
                continue;
            }
            int wordStart = i;
            while (i + 1 < n && char.IsLetter(template[i + 1])) i++;
            string word = template.Substring(wordStart, i - wordStart + 1);
            bool takeOneSpace = false;
            switch (word)
            {
                case "proper":
                case "Proper":
                    sb.Append(ProperMarker);
                    while (i + 1 < n && template[i + 1] == ' ') i++;
                    break;
                case "improper":
                case "Improper":
                    sb.Append(ImproperMarker);
                    while (i + 1 < n && template[i + 1] == ' ') i++;
                    break;
                case "the":
                    prefix = Prefix.LowerThe;
                    takeOneSpace = true;
                    break;
                case "The":
                    prefix = Prefix.UpperThe;
                    takeOneSpace = true;
                    break;
                case "a":
                case "an":
                    prefix = Prefix.LowerA;
                    takeOneSpace = true;
                    break;
                case "A":
                case "An":
                    prefix = Prefix.UpperA;
                    takeOneSpace = true;
                    break;
                case "roman":
                    prefix = Prefix.LowerRoman;
                    break;
                case "Roman":
                    prefix = Prefix.UpperRoman;
                    break;
                case "icon":
                    prefix = Prefix.Icon;
                    break;
                case "ref":
                    prefix = Prefix.Ref;
                    break;
                case "he":
                case "she":
                    AppendPronoun(sb, haveLast, last, "he", "she", "they", "it");
                    break;
                case "He":
                case "She":
                    AppendPronoun(sb, haveLast, last, "He", "She", "They", "It");
                    break;
                case "his":
                    AppendPronoun(sb, haveLast, last, "his", "her", "their", "its");
                    break;
                case "His":
                    AppendPronoun(sb, haveLast, last, "His", "Her", "Their", "Its");
                    break;
                case "him":
                case "Him":
                    AppendPronoun(sb, haveLast, last, "him", "her", "them", "it");
                    break;
                case "himself":
                case "herself":
                    AppendPronoun(sb, haveLast, last, "himself", "herself", "themself", "itself");
                    break;
                case "hers":
                    AppendPronoun(sb, haveLast, last, "his", "hers", "theirs", "its");
                    break;
                case "Hers":
                    AppendPronoun(sb, haveLast, last, "His", "Hers", "Theirs", "Its");
                    break;
                case "s":
                    if (!(haveLast && Dm.TryGetNumber(last, out float count) && count == 1f)) sb.Append('s');
                    break;
                case "th":
                    if (haveLast)
                    {
                        string ordinal = Ordinal(last);
                        sb.Remove(lastStart, lastEnd - lastStart).Insert(lastStart, ordinal);
                        lastEnd = lastStart + ordinal.Length;
                    }
                    break;
                default:
                    // \n and \t followed by letters ("\nfoo"), as OpenDream's parser reads them; other unknown macros
                    // (BYOND's old formatting codes such as \red) insert nothing.
                    if (word[0] == 'n') sb.Append('\n').Append(word, 1, word.Length - 1);
                    else if (word[0] == 't') sb.Append('\t').Append(word, 1, word.Length - 1);
                    break;
            }
            if (takeOneSpace && i + 1 < n && template[i + 1] == ' ') i++;
        }
        return sb.ToString();
    }

    private static void RenderArgument(StringBuilder sb, object arg, Prefix prefix)
    {
        switch (prefix)
        {
            case Prefix.None:
                sb.Append(Embedded(arg, sb.Length == 0));
                return;
            case Prefix.Ref:
                sb.Append(Dm.@ref(arg));
                return;
            case Prefix.Icon:
                return; // the presentation seam has no inline icons yet; DM shows the icon, never text
            case Prefix.LowerRoman:
            case Prefix.UpperRoman:
                if (Dm.TryGetNumber(arg, out float number)) sb.Append(Roman(number, prefix == Prefix.UpperRoman));
                else sb.Append(NameOnly(arg));
                return;
        }
        // \the and \a: the article (when the name is improper), then the bare name.
        string name = ArticleName(arg, out bool plural);
        if (name == null) return;
        if (!IsProper(name))
        {
            string shown = StripMarkers(name);
            switch (prefix)
            {
                case Prefix.LowerThe: sb.Append("the "); break;
                case Prefix.UpperThe: sb.Append("The "); break;
                case Prefix.LowerA: sb.Append(plural ? "some " : StartsWithVowel(shown) ? "an " : "a "); break;
                case Prefix.UpperA: sb.Append(plural ? "Some " : StartsWithVowel(shown) ? "An " : "A "); break;
            }
        }
        sb.Append(StripMarkers(name));
    }

    // The name an article macro looks at: an object's name or a text; null for other values (they show nothing,
    // OpenDream test StringInterpolation7: "\a [123]" == "").
    private static string ArticleName(object arg, out bool plural)
    {
        plural = false;
        if (arg is string s) return s;
        if (arg == null || Dm.TryGetNumber(arg, out _) || arg is Type || arg is DmList) return null;
        if (TryGetName(arg, out string name))
        {
            plural = GenderOf(arg) == Dm.PLURAL;
            return name;
        }
        return null;
    }

    // The [] after \roman for a non-number: an object's or a text's name, nothing for other values.
    private static string NameOnly(object arg)
    {
        string name = ArticleName(arg, out _);
        return name == null ? "" : StripMarkers(name);
    }

    private static bool StartsWithVowel(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        switch (char.ToLowerInvariant(s[0]))
        {
            case 'a':
            case 'e':
            case 'i':
            case 'o':
            case 'u':
                return true;
            default:
                return false;
        }
    }

    // \he and friends: by the gender of the previous [] (BYOND ref /atom/var/gender: neuter, male, female, plural);
    // nothing when it has no gender.
    private static void AppendPronoun(StringBuilder sb, bool haveLast, object last, string male, string female, string plural, string neuter)
    {
        if (!haveLast) return;
        switch (GenderOf(last))
        {
            case Dm.MALE: sb.Append(male); break;
            case Dm.FEMALE: sb.Append(female); break;
            case Dm.PLURAL: sb.Append(plural); break;
            case Dm.NEUTER: sb.Append(neuter); break;
        }
    }

    // \th replaces the previous []'s text (OpenDream test StringInterpolation5): a number becomes its integer part
    // with the English ordinal suffix (1st, 2nd, 3rd, 11th, 21st, -1th), anything else 0th.
    private static string Ordinal(object last)
    {
        if (!Dm.TryGetNumber(last, out float f) || float.IsNaN(f) || float.IsInfinity(f)) return "0th";
        double t = Math.Truncate((double)f);
        long n = t >= long.MaxValue ? long.MaxValue : t <= long.MinValue ? long.MinValue : (long)t;
        long tens = n % 100;
        string suffix = (tens >= 11 && tens <= 13) ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return n.ToString(CultureInfo.InvariantCulture) + suffix;
    }

    // \roman: the integer part as a roman numeral; NaN shows "-", infinities "inf" (OpenDream test StringInterpolation6).
    private static string Roman(float value, bool upper)
    {
        if (float.IsNaN(value)) return "-";
        var sb = new StringBuilder();
        if (value < 0)
        {
            sb.Append('-');
            value = -value;
        }
        if (float.IsInfinity(value)) return sb.Append("inf").ToString();
        long n = value >= long.MaxValue ? long.MaxValue : (long)value;
        string[] symbols = upper
            ? new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" }
            : new[] { "m", "cm", "d", "cd", "c", "xc", "l", "xl", "x", "ix", "v", "iv", "i" };
        int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
        for (int k = 0; k < values.Length && n > 0; k++)
        {
            while (n >= values[k])
            {
                sb.Append(symbols[k]);
                n -= values[k];
            }
        }
        return sb.ToString();
    }

    // \xNN, \uNNNN, \UNNNNNN: exactly that many hex digits (OpenDream test Expression/String/unicode: "ሴ56" is
    // U+1234 followed by "56"); returns the index of the last character consumed. Too few digits insert nothing.
    private static int AppendCodePoint(StringBuilder sb, string template, int letterIndex, int digits)
    {
        int start = letterIndex + 1;
        if (start + digits > template.Length) return letterIndex;
        if (!int.TryParse(template.AsSpan(start, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code)) return letterIndex;
        if (code > 0x10FFFF || (code >= 0xD800 && code <= 0xDFFF)) return start + digits - 1;
        sb.Append(char.ConvertFromUtf32(code));
        return start + digits - 1;
    }
}
