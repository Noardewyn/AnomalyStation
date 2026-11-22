using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Anomaly.Client.Shared.Tgui;

// M5 slice 3 (D148): the tgui-core helpers the translated interfaces call (tgui-core/format, /math, /string). tgui-core's sources are
// an npm dependency that the tg clone does not carry; these follow its documented behaviour (JavaScript's toFixed, the SI prefixes,
// the title case with tg's small words).

public static class TguiFormat
{
    /// <summary>JavaScript's <c>Number.prototype.toFixed(digits)</c> (tgui-core's <c>toFixed</c>): rounded half away from zero, invariant.</summary>
    public static string ToFixed(double value, int fractionDigits = 0)
    {
        double rounded = Math.Round(value, Math.Clamp(fractionDigits, 0, 15), MidpointRounding.AwayFromZero);
        return rounded.ToString("F" + Math.Clamp(fractionDigits, 0, 15).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>A number as JavaScript prints it (<c>String(n)</c>): whole numbers without a point, others in their shortest form.</summary>
    public static string JsNumber(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value > 0 ? "Infinity" : "-Infinity";
        if (value == Math.Floor(value) && Math.Abs(value) < 1e21) return value.ToString("0", CultureInfo.InvariantCulture);
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// tgui-core's <c>formatMoney(value, precision)</c> (gaps P4a: the bank machine, the cargo budget): the value fixed to the precision, its
    /// integer part grouped by thousands with a thin space (U+2009), the sign kept.
    /// </summary>
    public static string FormatMoney(double value, int precision = 0)
    {
        if (!double.IsFinite(value)) return JsNumber(value);
        string fixedText = ToFixed(Math.Abs(value), precision);
        bool negative = value < 0 && fixedText.Any(ch => ch is >= '1' and <= '9');
        string[] parts = fixedText.Split('.');
        string grouped = Regex.Replace(parts[0], @"\B(?=(\d{3})+(?!\d))", "\u2009");
        return (negative ? "-" : "") + grouped + (parts.Length > 1 ? "." + parts[1] : "");
    }

    private static readonly string[] SiSymbols = { "f", "p", "n", "μ", "m", " ", "k", "M", "G", "T", "P", "E", "Z", "Y" };
    private const int SiBaseIndex = 5;

    /// <summary>tgui-core's <c>formatSiUnit(value, minBase1000, unit)</c>: the value scaled to its SI prefix with two decimals (<c>"4.56 MPa"</c>).</summary>
    public static string FormatSiUnit(double value, int minBase1000 = -SiBaseIndex, string unit = "")
    {
        if (!double.IsFinite(value))
            return JsNumber(value);
        int realBase10 = value == 0 ? 0 : (int)Math.Floor(Math.Log10(Math.Abs(value)));
        int base10 = Math.Max(minBase1000 * 3, realBase10);
        int base1000 = (int)Math.Floor(base10 / 3.0);
        string symbol = SiSymbols[Math.Clamp(base1000 + SiBaseIndex, 0, SiSymbols.Length - 1)];
        double scaled = value / Math.Pow(1000, base1000);
        return (ToFixed(scaled, 2) + " " + symbol.Trim() + unit).Trim();
    }

    /// <summary>tgui-core's <c>formatPower(value, minBase1000)</c>: <see cref="FormatSiUnit"/> in watts (<c>"90.00 kW"</c>).</summary>
    public static string FormatPower(double value, int minBase1000 = 0) => FormatSiUnit(value, minBase1000, "W");

    /// <summary>tgui-core's <c>formatEnergy(value, minBase1000)</c>: <see cref="FormatSiUnit"/> in joules (<c>"10.00 MJ"</c>).</summary>
    public static string FormatEnergy(double value, int minBase1000 = 0) => FormatSiUnit(value, minBase1000, "J");

    /// <summary>
    /// tgui-core's <c>round(value, precision)</c>: <c>Math.round(value * 10 ** precision) / 10 ** precision</c>, so a precision of 0.1 (the
    /// SMES's <c>round(100 * (charge / capacity), 0.1)</c>) scales by 10 ** 0.1 and not by 10; a falsy or NaN value comes back as it is.
    /// </summary>
    public static double Round(double value, double precision = 0)
    {
        if (value == 0 || double.IsNaN(value))
            return value;
        double multiplier = Math.Pow(10, precision);
        return Math.Floor(value * multiplier + 0.5) / multiplier; // JavaScript's Math.round: half up
    }

    /// <summary>tgui-core's <c>clamp(value, min, max)</c>.</summary>
    public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;

    /// <summary>tgui-core's <c>clamp01</c>.</summary>
    public static double Clamp01(double value) => Clamp(value, 0, 1);

    private static readonly string[] LowerCaseWords = { "A", "An", "And", "As", "At", "But", "By", "For", "From", "In", "Into", "Near", "Nor", "Of", "On", "Onto", "Or", "The", "To", "With" };
    private static readonly string[] UpperCaseWords = { "Id", "Tv" };
    private static readonly Regex WordRegex = new(@"([^\W_]+[^\s-]*) *", RegexOptions.CultureInvariant);

    /// <summary>tgui-core's <c>toTitleCase</c>: each word capitalized, tg's small words lower case inside the text, "Id" and "Tv" upper case.</summary>
    public static string ToTitleCase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        string current = WordRegex.Replace(text, m => Capitalize(m.Value));
        foreach (string word in LowerCaseWords)
            current = Regex.Replace(current, @"\s" + word + @"\s", m => m.Value.ToLowerInvariant(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (string word in UpperCaseWords)
            current = Regex.Replace(current, @"\b" + word + @"\b", m => m.Value.ToUpperInvariant(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return current;
    }

    /// <summary>tgui-core's <c>capitalize</c>: the first letter upper case, the rest lower case.</summary>
    /// <summary>CSS's <c>text-transform: capitalize</c>: the first letter of every word upper-cased, the rest kept.</summary>
    public static string CssCapitalize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        var chars = text.ToCharArray();
        bool start = true;
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i]) || chars[i] == '\u00a0' || chars[i] == '-')
            {
                start = true;
                continue;
            }
            if (start)
                chars[i] = char.ToUpperInvariant(chars[i]);
            start = false;
        }
        return new string(chars);
    }

    /// <summary>tgui-core's <c>capitalizeAll</c> (E13): the first letter of the text and of every word after white space upper case, the rest kept.</summary>
    public static string CapitalizeAll(string text) => string.IsNullOrEmpty(text) ? text : Regex.Replace(text, @"(^\w{1})|(\s+\w{1})", m => m.Value.ToUpperInvariant());

    public static string Capitalize(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant();

    /// <summary>
    /// Gaps P2: tgui-core/format's <c>formatTime(val, formatType)</c>: deciseconds as <c>"1h2m3s"</c> (each part only when above zero)
    /// for <c>'short'</c>, else as <c>"hh:mm:ss"</c>.
    /// </summary>
    public static string FormatTime(double val, string formatType = "default")
    {
        long totalSeconds = (long)Math.Floor(val / 10);
        long hours = totalSeconds / 3600;
        long minutes = totalSeconds % 3600 / 60;
        long seconds = totalSeconds % 60;
        if (formatType == "short")
            return (hours > 0 ? hours + "h" : "") + (minutes > 0 ? minutes + "m" : "") + (seconds > 0 ? seconds + "s" : "");
        return hours.ToString("00", CultureInfo.InvariantCulture) + ":" + minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>tgui-core's <c>createSearch(searchText, stringifier)</c>: matches when the item's text contains the search, case-insensitively (an empty search matches all).</summary>
    public static Func<T, bool> CreateSearch<T>(string searchText, Func<T, string> stringifier)
    {
        string preparedSearch = (searchText ?? "").ToLowerInvariant().Trim();
        return item =>
        {
            if (preparedSearch.Length == 0)
                return true;
            string text = stringifier(item);
            return text != null && text.ToLowerInvariant().Trim().Contains(preparedSearch, StringComparison.Ordinal);
        };
    }

    /// <summary>JavaScript's <c>String.prototype.localeCompare</c> for the interfaces' sorts (invariant, case-insensitive first).</summary>
    public static int LocaleCompare(string a, string b) => string.Compare(a, b, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase) is var c && c != 0 ? c : string.CompareOrdinal(a, b);

    /// <summary>es-toolkit's <c>sortBy(list, [key])</c>: a stable sort by the key.</summary>
    public static List<T> SortBy<T>(IEnumerable<T> list, Func<T, string> key) => list.OrderBy(key, Comparer<string>.Create(LocaleCompare)).ToList();

    /// <summary>
    /// tgui-core/string's <c>decodeHtmlEntities</c>: <c>&lt;br&gt;</c> to a new line, the tags dropped, the named entities (nbsp, amp,
    /// quot, lt, gt, apos) and the decimal and hexadecimal character references decoded (the air alarm's vent and scrubber names, which
    /// tg's sanitize() escapes).
    /// </summary>
    public static string DecodeHtmlEntities(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        text = Regex.Replace(text, "<br>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</?[a-z0-9-_]+[^>]*>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "&(nbsp|amp|quot|lt|gt|apos);", m => m.Groups[1].Value switch
        {
            "nbsp" => " ",
            "amp" => "&",
            "quot" => "\"",
            "lt" => "<",
            "gt" => ">",
            _ => "'",
        });
        text = Regex.Replace(text, "&#?([0-9]+);", m => ((char)int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToString(), RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "&#x?([0-9a-f]+);", m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString(), RegexOptions.IgnoreCase);
        return text;
    }

    // tg: tgui/packages/tgui/constants.ts:168  const GASES (id, path, label, colour; E2 added the paths and colours for getGasFromPath)
    private static readonly (string Id, string Path, string Label, string Color)[] Gases =
    {
        ("o2", "/datum/gas/oxygen", "O₂", "blue"), ("n2", "/datum/gas/nitrogen", "N₂", "yellow"),
        ("co2", "/datum/gas/carbon_dioxide", "CO₂", "grey"), ("plasma", "/datum/gas/plasma", "Plasma", "pink"),
        ("water_vapor", "/datum/gas/water_vapor", "H₂O", "lightsteelblue"), ("hypernoblium", "/datum/gas/hypernoblium", "Hyper-nob", "teal"),
        ("n2o", "/datum/gas/nitrous_oxide", "N₂O", "bisque"), ("no2", "/datum/gas/nitrium", "Nitrium", "brown"),
        ("tritium", "/datum/gas/tritium", "Tritium", "limegreen"), ("bz", "/datum/gas/bz", "BZ", "mediumpurple"),
        ("pluoxium", "/datum/gas/pluoxium", "Pluoxium", "mediumslateblue"), ("miasma", "/datum/gas/miasma", "Miasma", "olive"),
        ("freon", "/datum/gas/freon", "Freon", "paleturquoise"), ("hydrogen", "/datum/gas/hydrogen", "H₂", "white"),
        ("healium", "/datum/gas/healium", "Healium", "salmon"), ("proto_nitrate", "/datum/gas/proto_nitrate", "Proto-Nitrate", "greenyellow"),
        ("zauker", "/datum/gas/zauker", "Zauker", "darkgreen"), ("halon", "/datum/gas/halon", "Halon", "purple"),
        ("helium", "/datum/gas/helium", "He", "aliceblue"), ("antinoblium", "/datum/gas/antinoblium", "Anti-Noblium", "maroon"),
        ("nitrium", "/datum/gas/nitrium", "Nitrium", "brown"),
    };

    // tg: tgui/packages/tgui/constants.ts:319
    public static string GetGasLabel(string gasId, string fallbackValue = null)
    {
        if (string.IsNullOrEmpty(gasId)) return string.IsNullOrEmpty(fallbackValue) ? "None" : fallbackValue;

        string gasSearchString = gasId.ToLowerInvariant();

        foreach (var gas in Gases)
            if (gas.Id == gasSearchString)
                return gas.Label;

        return string.IsNullOrEmpty(fallbackValue) ? "None" : fallbackValue;
    }

    // tg: tgui/packages/tgui/constants.ts:333  getGasColor (engineering E12a: the crystallizer's gas bars; E12b: the Hypertorus window's gas bars and recipe table)
    public static string GetGasColor(string gasId)
    {
        if (string.IsNullOrEmpty(gasId)) return "black";

        string gasSearchString = gasId.ToLowerInvariant();

        foreach (var gas in Gases)
            if (gas.Id == gasSearchString)
                return gas.Color;

        return "black";
    }

    // tg: tgui/packages/tgui/constants.ts:362  getGasFromPath: the gas of a /datum/gas path (its label and colour), null when none
    public static (string Label, string Color)? GetGasFromPath(string gasPath)
    {
        if (string.IsNullOrEmpty(gasPath)) return null;

        foreach (var gas in Gases)
            if (gas.Path == gasPath)
                return (gas.Label, gas.Color);
        return null;
    }
}
