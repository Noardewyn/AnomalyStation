using System.Globalization;
using System.Text;

namespace Anomaly.Protocol;

// Owner: M1 P2 (an addition to the contracts: the codec only guarantees structure, this is the semantic half of Hello).

/// <summary>
/// The rules for <see cref="Hello.Name"/> (scope 2e "Handshake"), shared by every host and client: the codec accepts any
/// valid UTF-8 name within <see cref="Limits.MaxNameBytes"/>, and the server host answers a name that breaks these rules
/// with <see cref="DisconnectReason.InvalidName"/> (so the player learns why, instead of a generic
/// <see cref="DisconnectReason.BadHello"/>).
/// </summary>
public static class NameRules
{
    /// <summary>
    /// True when <paramref name="name"/> is a legal player name: not null or empty, at most <see cref="Limits.MaxNameChars"/>
    /// characters (Unicode scalar values, as tg counts <c>length_char</c>), no control, format, separator or unassigned
    /// characters, and not only whitespace.
    /// </summary>
    public static bool IsValid(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        int chars = 0;
        bool visible = false;
        foreach (Rune rune in name.EnumerateRunes())
        {
            if (++chars > Limits.MaxNameChars)
                return false;
            switch (Rune.GetUnicodeCategory(rune))
            {
                case UnicodeCategory.Control:
                case UnicodeCategory.Format:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.ParagraphSeparator:
                case UnicodeCategory.OtherNotAssigned:
                case UnicodeCategory.PrivateUse:
                case UnicodeCategory.Surrogate:
                    return false;
                case UnicodeCategory.SpaceSeparator:
                    break;
                default:
                    visible = true;
                    break;
            }
        }

        return visible;
    }
}
