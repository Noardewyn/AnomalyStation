using System.Linq;
using System.Text.RegularExpressions;

namespace Anomaly.Tools.Sounds;

/// <summary>One licence a line of an attribution file states: a normalised label and whether it forbids commercial use.</summary>
public sealed record LicenceMention(string Label, bool NonCommercial);

/// <summary>
/// Finds the licences a line of text names. tg's attribution files are free text ("License: CC BY-NC 4.0", "(CC 0)", "Attribution
/// NonCommercial 4.0", "CC-by-SA", "licensed under CC Attribution 3.0", ...), so this is a list of patterns, applied in order; each
/// match is blanked out so that a later pattern cannot read the same words again ("CC BY-NC" must not also count as "CC BY").
/// </summary>
public static class Licences
{
    /// <summary>The label of every file no attribution names (tgstation README: all assets are under CC 3.0 BY-SA unless indicated otherwise).</summary>
    public const string Default = "CC BY-SA 3.0 (tg README)";

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly (Regex Pattern, Func<Match, LicenceMention> Make)[] Patterns =
    {
        // Non-commercial first: "CC BY-NC 4.0", "CC-BY-NC-SA-3.0", "Attribution NonCommercial 4.0", "Noncommercial 4.0 License".
        (new Regex(@"cc[ -]?by[ -]?nc[ -]?sa(?:[ -]?(\d(?:\.\d)?))?", Options), m => Nc("CC BY-NC-SA", m.Groups[1].Value)),
        (new Regex(@"cc[ -]?by[ -]?nc(?:[ -]?(\d(?:\.\d)?))?", Options), m => Nc("CC BY-NC", m.Groups[1].Value)),
        (new Regex(@"attributions?[ -]?non[ -]?commercial[ -]?(?:share[ -]?alike[ -]?)?(\d(?:\.\d)?)?", Options), m => Nc("CC BY-NC", m.Groups[1].Value)),
        (new Regex(@"non[ -]?commercial(?: (\d\.\d))?", Options), m => Nc("CC BY-NC", m.Groups[1].Value)),
        // Public domain dedication: "CC0", "CC 0", "CC-0", "Creative Commons 0", "Creating Commons 0", "CC Attributions 0".
        (new Regex(@"(?:cc[ -]?attributions?[ -]?0|cc[ -]?0|cc zero|(?:creative|creating) commons[ -]?0|attributions? 0)(?:[ -]?1\.0)?", Options), m => new LicenceMention("CC0 1.0", false)),
        (new Regex(@"creative commons (\d\.\d) by[ -]?sa", Options), m => new LicenceMention("CC BY-SA " + m.Groups[1].Value, false)),
        (new Regex(@"cc[ -]?by[ -]?sa(?:[ -]?(\d(?:\.\d)?))?", Options), m => new LicenceMention(Versioned("CC BY-SA", m.Groups[1].Value), false)),
        (new Regex(@"cc[ -]?(?:by|attributions?)[ -]?(\d(?:\.\d)?)", Options), m => new LicenceMention(Versioned("CC BY", m.Groups[1].Value), false)),
        (new Regex(@"attributions? (\d(?:\.\d)?)(?: international)?", Options), m => new LicenceMention(Versioned("CC BY", m.Groups[1].Value), false)),
        (new Regex(@"\bcc[ -]?by\b", Options), m => new LicenceMention("CC BY", false)),
        (new Regex(@"\bcc ?([34])\b", Options), m => new LicenceMention(Versioned("CC BY", m.Groups[1].Value), false)),
        (new Regex(@"sampling\+", Options), m => new LicenceMention("CC Sampling+ 1.0", false)),
        (new Regex(@"pixabay content license", Options), m => new LicenceMention("Pixabay Content License", false)),
        (new Regex(@"zapsplat", Options), m => new LicenceMention("Zapsplat Standard License", false)),
        (new Regex(@"\blesf\b", Options), m => new LicenceMention("LESF (SoundFishing)", false)),
        (new Regex(@"royalty-free and copyright-free", Options), m => new LicenceMention("Royalty-free (Uppbeat)", false)),
        (new Regex(@"public domain", Options), m => new LicenceMention("Public domain", false)),
    };

    private static LicenceMention Nc(string name, string version) => new(Versioned(name, version), true);

    private static string Versioned(string name, string version)
    {
        if (string.IsNullOrEmpty(version))
            return name;
        return name + " " + (version.Contains('.') ? version : version + ".0");
    }

    /// <summary>The licences named in <paramref name="line"/>, in the order they are found, without repeats (CC0 hides a "public domain" beside it).</summary>
    public static List<LicenceMention> Find(string line)
    {
        var found = new List<LicenceMention>();
        string work = line;
        foreach ((Regex pattern, Func<Match, LicenceMention> make) in Patterns)
        {
            work = pattern.Replace(work, m =>
            {
                LicenceMention mention = make(m);
                if (!found.Contains(mention))
                    found.Add(mention);
                return new string(' ', m.Length);
            });
        }
        if (found.Any(f => f.Label == "CC0 1.0"))
            found.RemoveAll(f => f.Label == "Public domain");
        return found;
    }
}
