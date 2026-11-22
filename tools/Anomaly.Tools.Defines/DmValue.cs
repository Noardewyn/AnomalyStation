using System.Globalization;
using System.Text;

namespace Anomaly.Tools.Defines;

/// <summary>The result of folding a define body: a DM number, text, null, or the reason it is not a constant.</summary>
public abstract class DmValue
{
}

/// <summary>A DM number: IEEE binary32.</summary>
public sealed class DmNum : DmValue
{
    public DmNum(float value)
    {
        Value = value;
    }

    public float Value { get; }
}

public sealed class DmText : DmValue
{
    public DmText(string value)
    {
        Value = value;
    }

    public string Value { get; }
}

public sealed class DmNull : DmValue
{
    public static readonly DmNull Instance = new();

    private DmNull()
    {
    }
}

/// <summary>Not a compile-time constant. <see cref="Kind"/> is the manifest kind (list, typepath, runtime, ...).</summary>
public sealed class DmNotConst : DmValue
{
    public DmNotConst(string kind, string reason)
    {
        Kind = kind;
        Reason = reason;
    }

    public string Kind { get; }

    public string Reason { get; }
}

/// <summary>DM text literal decoding and number-to-text conversion.</summary>
public static class DmTextRules
{
    private static readonly HashSet<string> TextMacros = new(StringComparer.Ordinal)
    {
        "the", "The", "a", "an", "A", "An", "he", "He", "she", "She", "his", "His", "him", "himself", "herself", "hers",
        "proper", "improper", "th", "s", "icon", "ref", "roman", "Roman",
    };

    /// <summary>
    /// Decodes the escapes of one literal segment (BYOND ref "text" and "macros (text)"): <c>\n</c>, <c>\t</c>,
    /// <c>\"</c>, <c>\\</c>, <c>\[</c>, <c>\]</c> and the like give the character, <c>\&lt;</c> and
    /// <c>\&gt;</c> give <c>&amp;lt;</c> and <c>&amp;gt;</c>, <c>\ </c> skips the space, <c>\xNN</c>,
    /// <c>\uNNNN</c> and <c>\UNNNNNN</c> give the character with that code. A text macro (<c>\the</c>,
    /// <c>\improper</c>, <c>\s</c>, <c>\ref</c>, ...) depends on the values it is used with, so a literal holding one
    /// is not a constant: the method returns null and the reason. A run of letters after the backslash that is not a
    /// macro name starts with the <c>n</c> or <c>t</c> escape (tg's songs write <c>\nC4</c> for a newline and C4).
    /// </summary>
    public static string Decode(string literal, out string reason)
    {
        reason = null;
        if (literal.IndexOf('\\') < 0)
            return literal;
        var sb = new StringBuilder(literal.Length);
        for (int i = 0; i < literal.Length; i++)
        {
            char c = literal[i];
            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }
            if (i + 1 >= literal.Length)
            {
                reason = "a backslash at the end of the text";
                return null;
            }
            char e = literal[i + 1];
            if (TryHexEscape(literal, i, out char? hex, out int hexLength))
            {
                sb.Append(hex.Value);
                i += hexLength - 1;
                continue;
            }
            if (char.IsAsciiLetter(e))
            {
                int j = i + 1;
                while (j < literal.Length && char.IsAsciiLetter(literal[j]))
                    j++;
                string run = literal.Substring(i + 1, j - i - 1);
                if (TextMacros.Contains(run))
                {
                    reason = $"text macro \\{run} (applied when the text is used)";
                    return null;
                }
                if (e == 'n')
                    sb.Append('\n');
                else if (e == 't')
                    sb.Append('\t');
                else
                {
                    reason = $"unknown escape \\{run}";
                    return null;
                }
                i++;
                continue;
            }
            switch (e)
            {
                case '<':
                    sb.Append("&lt;");
                    break;
                case '>':
                    sb.Append("&gt;");
                    break;
                case ' ':
                    break; // "\(space)": skip a space
                case '"':
                case '\\':
                case '\'':
                case '[':
                case ']':
                case '{':
                case '}':
                    sb.Append(e);
                    break;
                default:
                    reason = $"escape \\{e}";
                    return null;
            }
            i++;
        }
        return sb.ToString();
    }

    private static bool TryHexEscape(string s, int i, out char? value, out int length)
    {
        value = null;
        length = 0;
        char e = s[i + 1];
        int digits = e switch { 'x' => 2, 'u' => 4, 'U' => 6, _ => 0 };
        if (digits == 0 || i + 2 + digits > s.Length)
            return false;
        string hex = s.Substring(i + 2, digits);
        if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code) || code > 0xFFFF)
            return false;
        value = (char)code;
        length = 2 + digits;
        return true;
    }

    /// <summary>
    /// A number as DM prints it in <c>"[x]"</c> (translation-rules.md 5.5): six significant digits, %g style, trailing
    /// zeros removed, <c>1e+06</c> for a million.
    /// </summary>
    public static string NumberToText(float value)
    {
        if (value == 0)
            return "0";
        string g = ((double)value).ToString("G6", CultureInfo.InvariantCulture);
        int e = g.IndexOf('E');
        if (e < 0)
            return g;
        string mantissa = g.Substring(0, e);
        string exponent = g.Substring(e + 1);
        char sign = exponent[0] == '-' ? '-' : '+';
        string digits = exponent.TrimStart('+', '-').TrimStart('0');
        if (digits.Length < 2)
            digits = digits.PadLeft(2, '0');
        return $"{mantissa}e{sign}{digits}";
    }
}
