namespace Anomaly.Core.DmShim;

/// <summary>
/// The positions a text proc counts in (docs/ai/translation-rules.md 5.6, DV4): UTF-16 code units for the byte-indexed
/// procs (<c>copytext</c>, <c>findtext</c>, ...), Unicode scalar values for their <c>_char</c> variants, where a
/// surrogate pair is one character (BYOND counts characters there). Text without surrogates maps one to one, so the
/// two only differ for characters outside the Basic Multilingual Plane (emoji); Cyrillic is the same in both. A lone
/// surrogate counts as one character. Owner: values.
/// </summary>
internal readonly struct DmTextUnits
{
    /// <summary>The text.</summary>
    public readonly string Text;

    // The UTF-16 offset where each character starts, plus Text.Length at the end; null when every unit is one char.
    private readonly int[] _starts;
    private readonly bool _characters;

    public DmTextUnits(string text, bool characters)
    {
        Text = text ?? "";
        _characters = characters;
        _starts = characters && HasSurrogates(Text) ? RuneStarts(Text) : null;
    }

    /// <summary>The number of units.</summary>
    public int Length => _starts == null ? Text.Length : _starts.Length - 1;

    /// <summary>The UTF-16 offset of the 0-based unit <paramref name="unit"/> (0..Length).</summary>
    public int Offset(int unit) => _starts == null ? unit : _starts[unit];

    /// <summary>The 0-based unit that starts at, or contains, a UTF-16 offset.</summary>
    public int UnitAt(int offset)
    {
        if (_starts == null) return offset;
        int i = Array.BinarySearch(_starts, offset);
        return i >= 0 ? i : ~i - 1;
    }

    /// <summary>The text of the 0-based units from <paramref name="start"/> to <paramref name="end"/> (exclusive).</summary>
    public string Sub(int start, int end)
    {
        if (end <= start) return "";
        int from = Offset(start);
        return Text.Substring(from, Offset(end) - from);
    }

    /// <summary>The 0-based unit <paramref name="unit"/> as text (one char, or a surrogate pair).</summary>
    public string UnitText(int unit) => Sub(unit, unit + 1);

    /// <summary>The number of units of another text, counted the same way.</summary>
    public int LengthOf(string other) => _characters ? CountRunes(other) : other.Length;

    internal static int CountRunes(string s)
    {
        int count = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) i++;
            count++;
        }
        return count;
    }

    internal static bool HasSurrogates(string s)
    {
        foreach (char c in s)
        {
            if (char.IsSurrogate(c)) return true;
        }
        return false;
    }

    private static int[] RuneStarts(string s)
    {
        var starts = new List<int>(s.Length + 1);
        for (int i = 0; i < s.Length; i++)
        {
            starts.Add(i);
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) i++;
        }
        starts.Add(s.Length);
        return starts.ToArray();
    }
}
