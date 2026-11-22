using System.Globalization;
using System.Text.RegularExpressions;

namespace Anomaly.Client.Shared.Tgui;

// Owner: M9 slice C (the owner's playtest, 2026-10-23: tgui's icons showed as dots). Engine-free (compiled into the tests too).
// tgui draws its icons with Font Awesome (tgui-core's Icon: "fas fa-NAME"; tg ships Font Awesome Free 6.7.2 in html/font-awesome). This reads
// the stylesheet's ".fa-NAME:before{content:"\fXXX"}" rules into the names' code points; the Godot side draws them with the solid face.

public static class FontAwesomeNames
{
    private static readonly Regex Rule = new(@"((?:\.fa-[a-z0-9-]+:before,?)+)\{content:""(?:\\([0-9a-fA-F]+)|([^""\\]))""\}", RegexOptions.CultureInvariant);
    private static readonly Regex Name = new(@"\.fa-([a-z0-9-]+):before", RegexOptions.CultureInvariant);

    /// <summary>Every icon name (aliases included) of a Font Awesome stylesheet and its character.</summary>
    public static Dictionary<string, string> Parse(string css)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match rule in Rule.Matches(css ?? ""))
        {
            string glyph = rule.Groups[2].Success ? char.ConvertFromUtf32(int.Parse(rule.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)) : rule.Groups[3].Value;
            foreach (Match name in Name.Matches(rule.Groups[1].Value))
                map.TryAdd(name.Groups[1].Value, glyph);
        }
        return map;
    }
}
