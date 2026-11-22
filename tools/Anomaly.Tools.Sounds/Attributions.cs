using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Anomaly.Tools.Sounds;

/// <summary>The licence of one sound file as the attribution files of tg's sound/ tree state it.</summary>
/// <param name="Label">The licence label (several joined with " + " when the attribution names more than one).</param>
/// <param name="NonCommercial">True when any named licence forbids commercial use.</param>
/// <param name="Unknown">True when an attribution file names the sound but states no licence we recognise.</param>
/// <param name="Source">The attribution file that names the sound (relative to sound/), null for the default licence.</param>
/// <param name="Derived">True when the non-commercial flag comes from a line that names this sound beside another non-commercial one.</param>
public sealed record FileLicence(string Label, bool NonCommercial, bool Unknown, string Source, bool Derived = false);

/// <summary>
/// Reads every licence note (<c>attribution(s).txt</c>, <c>credit(s).txt</c>, <c>license.txt</c>) under tg's sound/ tree. The files are free text in at least ten styles
/// ("name.ogg - url , License: X", "{ names } - made by Y, license: Z", "sound/path/name.ogg -- url -- License: Z", prose with the
/// licence on a later line, YAML-like entries, ...), so this is a line scanner with a few rules, not a grammar:
/// <list type="bullet">
/// <item>a line names sounds by file name (<c>x.ogg</c>), by path (<c>sound/items/x.ogg</c>), by bare stem (<c>portal_open</c> matches
/// <c>portal_open1.ogg</c>), by two words (<c>helmet drop</c>), or by a bracket or number range (<c>x[1,2].ogg</c>, <c>x 1-7</c>,
/// <c>x_1 to x_6</c>); URLs are blanked first; names are looked up under the attribution file's own folder;</item>
/// <item>a line with names and a licence gives it to those names; a line with names only opens a group that takes the licences of the
/// following lines (also after a blank line, and across a <c>{ ... }</c> block whose closing line carries the licence);</item>
/// <item>a licence line with no open group is a statement about the folder: it applies to its sounds that nothing else licenses;</item>
/// <item>a line that names several sounds beside one that is non-commercial makes the others non-commercial too (derived work).</item>
/// </list>
/// What it cannot read ends up as <see cref="FileLicence.Unknown"/>, and a non-commercial mention that reached no file is a warning.
/// </summary>
public sealed class Attributions
{
    private sealed class Mention
    {
        public string Where;
        public string Text;
        public int Applied;
    }

    private sealed class Acc
    {
        public readonly List<LicenceMention> Mentions = new();
        public readonly List<string> Sources = new();
        public bool Named;
        public bool Derived;
    }

    private sealed class MultiLine
    {
        public List<string> Names;
        public string Source;
    }

    private static readonly Regex Url = new(@"h?https?://\S+", RegexOptions.Compiled);
    private static readonly Regex Bracket = new(@"([\w\-./]+?)\[(\d[\d,.\s]*)\](?:\.ogg)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NumberRange = new(@"([A-Za-z_]{3,})[ _]?(\d+)\s*-\s*(\d+)\b", RegexOptions.Compiled);
    private static readonly Regex ThroughRange = new(@"([A-Za-z_/]+?)(\d+)(?:\.ogg)?\s+(?:to|through)\s+(?:\./)?[A-Za-z_/]+?(\d+)(?:\.ogg)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Token = new(@"[A-Za-z0-9_\-./]+", RegexOptions.Compiled);

    private readonly Dictionary<string, FileLicence> _files = new(StringComparer.Ordinal);

    /// <summary>The warnings (a non-commercial mention that matched no file, names matching several files).</summary>
    public List<string> Warnings { get; } = new();

    /// <summary>The licence of <paramref name="soundPath"/> (a path relative to sound/, forward slashes); the default label when no attribution names it.</summary>
    public FileLicence Of(string soundPath) =>
        _files.TryGetValue(soundPath, out FileLicence licence) ? licence : new FileLicence(Licences.Default, false, false, null);

    /// <summary>Reads the attribution files under <paramref name="soundDirectory"/> and resolves every path of <paramref name="oggPaths"/> (relative to it).</summary>
    public static Attributions Load(string soundDirectory, IReadOnlyCollection<string> oggPaths)
    {
        var result = new Attributions();
        var all = oggPaths.OrderBy(p => p, StringComparer.Ordinal).ToList();
        var acc = new Dictionary<string, Acc>(StringComparer.Ordinal);
        var multi = new List<MultiLine>();
        var dirLevel = new List<(string Dir, string Source, List<LicenceMention> Mentions, List<Mention> Origins)>();
        var origins = new List<Mention>();

        var files = Directory.EnumerateFiles(soundDirectory, "*.txt", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(soundDirectory, f).Replace('\\', '/'))
            .Where(r => IsLicenceNote(Path.GetFileName(r)))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();
        foreach (string rel in files)
        {
            string text = File.ReadAllText(Path.Combine(soundDirectory, rel));
            result.ProcessFile(rel, text, all, acc, multi, dirLevel, origins);
        }

        // A folder-wide licence reaches the sounds of that folder that no line licenses.
        foreach ((string dir, string source, List<LicenceMention> mentions, List<Mention> from) in dirLevel)
        {
            foreach (string path in all.Where(p => DirOf(p) == dir))
            {
                if (!acc.TryGetValue(path, out Acc a))
                    acc[path] = a = new Acc();
                if (a.Mentions.Count > 0)
                    continue;
                a.Mentions.AddRange(mentions);
                a.Sources.Add(source);
                a.Named = true;
                foreach (Mention m in from)
                    m.Applied++;
            }
        }

        // A line that names several sounds, one of them non-commercial: the others are derived from it.
        foreach (MultiLine line in multi)
        {
            Acc nc = line.Names.Select(n => acc[n]).FirstOrDefault(a => a.Mentions.Any(m => m.NonCommercial));
            if (nc == null)
                continue;
            LicenceMention source = nc.Mentions.First(m => m.NonCommercial);
            foreach (string name in line.Names)
            {
                Acc a = acc[name];
                if (a.Mentions.Any(m => m.NonCommercial))
                    continue;
                a.Mentions.Add(new LicenceMention(source.Label, true));
                a.Derived = true;
            }
        }

        foreach ((string path, Acc a) in acc)
        {
            string source = a.Sources.Count > 0 ? a.Sources[0] : null;
            if (a.Mentions.Count == 0)
            {
                if (a.Named)
                    result._files[path] = new FileLicence("unspecified (named in sound/" + source + ")", false, true, source);
                continue;
            }
            string label = string.Join(" + ", a.Mentions.Select(m => m.Label).Distinct());
            result._files[path] = new FileLicence(label, a.Mentions.Any(m => m.NonCommercial), false, source, a.Derived);
        }
        foreach (Mention m in origins.Where(m => m.Applied == 0))
            result.Warnings.Add("a non-commercial mention reached no sound file: " + m.Where + ": " + m.Text.Trim());
        return result;
    }

    // D344: tg names its licence notes attribution(s).txt, but also credit(s).txt, license.txt and one attritbution.txt
    internal static bool IsLicenceNote(string name) =>
        new[] { "attribution", "attritbution", "credit", "license", "licence" }.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static string DirOf(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path.Substring(0, slash);
    }

    private void ProcessFile(string attrRel, string text, List<string> all, Dictionary<string, Acc> acc, List<MultiLine> multi,
        List<(string Dir, string Source, List<LicenceMention> Mentions, List<Mention> Origins)> dirLevel, List<Mention> origins)
    {
        string dir = DirOf(attrRel);
        string prefix = dir.Length == 0 ? "" : dir + "/";
        var subtree = all.Where(p => p.StartsWith(prefix, StringComparison.Ordinal)).ToList();

        HashSet<string> group = null;
        bool extendable = false, inBrace = false, groupColon = false, orphan = false;
        var braceSet = new HashSet<string>(StringComparer.Ordinal);
        var licensed = new HashSet<string>(StringComparer.Ordinal);
        var dirMentions = new List<LicenceMention>();
        var dirOrigins = new List<Mention>();

        void Assign(IEnumerable<string> names, List<LicenceMention> mentions, Mention origin)
        {
            foreach (string name in names)
            {
                if (!acc.TryGetValue(name, out Acc a))
                    acc[name] = a = new Acc();
                a.Named = true;
                foreach (LicenceMention m in mentions)
                    if (!a.Mentions.Contains(m))
                        a.Mentions.Add(m);
                if (!a.Sources.Contains(attrRel))
                    a.Sources.Add(attrRel);
                licensed.Add(name);
                if (origin != null)
                    origin.Applied++;
            }
        }

        string[] lines = text.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            string trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                extendable = false;
                orphan = false;
                // A list of samples that follows a lead-in ending in a colon ("made from several samples:") may run on past blank lines;
                // any other group ends with its paragraph.
                if (!groupColon)
                    group = null;
                continue;
            }
            if (trimmed.StartsWith('{'))
            {
                inBrace = true;
                groupColon = false;
                braceSet.Clear();
                group = null;
                extendable = false;
                raw = trimmed.Substring(1);
            }
            bool closes = trimmed.StartsWith('}');
            if (closes)
            {
                group = braceSet.Where(n => !licensed.Contains(n)).ToHashSet(StringComparer.Ordinal);
                extendable = false;
                inBrace = false;
                groupColon = false;
                raw = trimmed.Substring(1);
            }

            List<string> names = Resolve(raw, dir, subtree, out List<string> ambiguous, out bool unresolvedSubject);
            List<LicenceMention> mentions = Licences.Find(Url.Replace(raw, " "));
            Mention origin = null;
            if (mentions.Any(m => m.NonCommercial))
            {
                origin = new Mention { Where = "sound/" + attrRel + ":" + (i + 1), Text = raw };
                origins.Add(origin);
            }

            if (unresolvedSubject && names.Count + ambiguous.Count == 0)
            {
                // "roaring_fire.ogg made from: ... License: X" where roaring_fire.ogg is no longer in tg: the licence is that
                // sound's, not the previous group's. (A non-commercial one is reported as a warning: it reached no file.)
                group = null;
                extendable = false;
                orphan = true;
                continue;
            }
            if (orphan && names.Count + ambiguous.Count == 0)
            {
                // The licence lines of that gone sound: they belong to nobody.
                continue;
            }
            orphan = false;

            if (names.Count + ambiguous.Count > 0 && mentions.Count > 0)
            {
                // A name that fits several files: the line is read as a whole or not at all. A non-commercial statement is applied to
                // every file the name could mean (the safe side); any other licence is left unapplied and reported.
                if (ambiguous.Count > 0)
                {
                    List<LicenceMention> nc = mentions.Where(m => m.NonCommercial).ToList();
                    if (nc.Count > 0)
                    {
                        Assign(names, nc, origin);
                        Assign(ambiguous, nc, origin);
                    }
                    Warnings.Add("sound/" + attrRel + ":" + (i + 1) + ": a name fits several files (" + (nc.Count > 0 ? "non-commercial: all marked" : "not applied") + "): " + raw.Trim());
                }
                else
                {
                    Assign(names, mentions, origin);
                }
                if (names.Count > 1)
                    multi.Add(new MultiLine { Names = names.ToList(), Source = attrRel });
                group = null;
                extendable = false;
            }
            else if (names.Count > 0)
            {
                foreach (string n in names)
                {
                    if (!acc.TryGetValue(n, out Acc a))
                        acc[n] = a = new Acc();
                    a.Named = true;
                    if (!a.Sources.Contains(attrRel))
                        a.Sources.Add(attrRel);
                }
                if (extendable && group != null)
                    group.UnionWith(names);
                else
                    group = new HashSet<string>(names, StringComparer.Ordinal);
                groupColon = raw.TrimEnd().EndsWith(':');
                extendable = true;
                if (inBrace)
                    braceSet.UnionWith(names);
                if (names.Count > 1)
                    multi.Add(new MultiLine { Names = names.ToList(), Source = attrRel });
            }
            else if (mentions.Count > 0)
            {
                if (group != null && group.Count > 0)
                    Assign(group, mentions, origin);
                else
                {
                    dirOrigins.AddRange(origin != null ? new[] { origin } : Array.Empty<Mention>());
                    foreach (LicenceMention m in mentions)
                        if (!dirMentions.Contains(m))
                            dirMentions.Add(m);
                }
                extendable = false;
            }
            else
            {
                extendable = false;
            }
        }
        if (dirMentions.Count > 0)
            dirLevel.Add((dir, attrRel, dirMentions, dirOrigins));
    }

    /// <summary>The sound files a line names (resolved to relative paths) and the names that fit several files.</summary>
    private static List<string> Resolve(string line, string dir, List<string> subtree, out List<string> ambiguous, out bool unresolvedSubject)
    {
        unresolvedSubject = false;
        var found = new List<string>();
        var ambig = new List<string>();
        string text = Url.Replace(line.Replace('\\', '/').Replace('\t', ' '), " ");
        text = Expand(text);

        var words = new List<string>();
        foreach (Match m in Token.Matches(text))
        {
            string t = m.Value;
            while (t.StartsWith("./", StringComparison.Ordinal))
                t = t.Substring(2);
            t = t.Trim('.', '-', '/');
            if (t.Length == 0)
                continue;
            words.Add(t);
            List<string> hit = Lookup(t, dir, subtree, out List<string> amb);
            Add(hit, amb);
            // The line starts with a sound file name that matches no file: the line is about a sound that is gone from tg.
            if (words.Count == 1 && hit.Count + amb.Count == 0 && t.EndsWith(".ogg", StringComparison.Ordinal)
                && line.Replace('\\', '/').TrimStart(' ', '\t', '-', '*', '\'', '"', '`').StartsWith(t, StringComparison.Ordinal))
                unresolvedSubject = true;
        }
        // Two words in a row that read like a stem ("helmet drop" for helmet_drop1.ogg).
        string[] plain = Regex.Split(text, @"\s+").Select(w => w.Trim(',', ';', ':', '(', ')', '"', '\'', '.', '-')).Where(w => w.Length > 0).ToArray();
        for (int i = 0; i + 1 < plain.Length; i++)
        {
            if (!Regex.IsMatch(plain[i], @"^[A-Za-z][A-Za-z0-9]*$") || !Regex.IsMatch(plain[i + 1], @"^[A-Za-z][A-Za-z0-9]*$"))
                continue;
            Add(Lookup(plain[i] + "_" + plain[i + 1], dir, subtree, out List<string> amb), amb);
        }
        ambiguous = ambig;
        return found;

        void Add(List<string> hit, List<string> amb)
        {
            foreach (string h in hit)
                if (!found.Contains(h))
                    found.Add(h);
            foreach (string a in amb)
                if (!ambig.Contains(a) && !found.Contains(a))
                    ambig.Add(a);
        }
    }

    /// <summary>Writes out <c>x[1,2].ogg</c>, <c>x 1-7</c> and <c>x_1 to x_6</c> as plain names appended to the text.</summary>
    private static string Expand(string text)
    {
        var extra = new StringBuilder();
        text = Bracket.Replace(text, m =>
        {
            var numbers = new List<int>();
            string[] parts = m.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out int n))
                    numbers.Add(n);
                else if (parts[i].StartsWith("..", StringComparison.Ordinal) && numbers.Count > 0 && i + 1 < parts.Length && int.TryParse(parts[i + 1], out int to))
                    for (int k = numbers[^1] + 1; k < to; k++)
                        numbers.Add(k);
            }
            var sb = new StringBuilder();
            foreach (int n in numbers)
                sb.Append(m.Groups[1].Value).Append(n).Append(".ogg ");
            return sb.ToString();
        });
        foreach (Match m in NumberRange.Matches(text))
            AddRange(extra, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
        foreach (Match m in ThroughRange.Matches(text))
            AddRange(extra, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
        return text + " " + extra;
    }

    private static void AddRange(StringBuilder sb, string stem, string from, string to)
    {
        if (!int.TryParse(from, out int lo) || !int.TryParse(to, out int hi) || hi < lo || hi - lo > 40)
            return;
        stem = stem.TrimEnd(' ');
        if (stem.EndsWith("cc", StringComparison.OrdinalIgnoreCase))
            return;
        for (int n = lo; n <= hi; n++)
            sb.Append(' ').Append(stem).Append(n);
    }

    /// <summary>The files a name token fits: by path, by file name, or by stem (exact, stem plus digits, or stem followed by an underscore part).</summary>
    private static List<string> Lookup(string token, string dir, List<string> subtree, out List<string> ambiguous)
    {
        ambiguous = new List<string>();
        string lower = token;   // file names are matched case-sensitively: "SS13" and "Ping.ogg" in prose are not sounds
        if (lower.EndsWith(".mp3", StringComparison.Ordinal))
        {
            // tg's one mp3 is credited by its own path; any other .mp3 named is a source file, never an .ogg of the same stem
            string t = lower.TrimStart('/');
            if (t.StartsWith("sound/", StringComparison.Ordinal))
                t = t.Substring(6);
            return Choose(subtree.Where(f => f == t || f.EndsWith("/" + t, StringComparison.Ordinal)).ToList(), dir, ambiguous);
        }
        bool hasExt = lower.EndsWith(".ogg", StringComparison.Ordinal);
        if (!hasExt && Regex.IsMatch(lower, @"\.[A-Za-z0-9]{2,5}$", RegexOptions.None) && !Regex.IsMatch(lower, @"\.\d+$"))
            return new List<string>();   // x.wav, a version number or a host name: not one of ours
        string stem = hasExt ? lower.Substring(0, lower.Length - 4) : lower;
        if (stem.Length < 4 && !hasExt)
            return new List<string>();
        if (!stem.Any(char.IsLetter))
            return new List<string>();

        List<string> hits;
        if (stem.Contains('/'))
        {
            string p = stem.TrimStart('/');
            foreach (string lead in new[] { "sound/", "sounds/" })
                if (p.StartsWith(lead, StringComparison.Ordinal))
                    p = p.Substring(lead.Length);
            hits = subtree.Where(f =>
            {
                string noExt = f.Substring(0, f.Length - 4);
                return noExt == p || noExt.EndsWith("/" + p, StringComparison.Ordinal);
            }).ToList();
            if (hits.Count > 0)
                return Choose(hits, dir, ambiguous);
            stem = stem.Substring(stem.LastIndexOf('/') + 1);
            if (stem.Length == 0)
                return new List<string>();
        }

        bool plainWord = !hasExt && !stem.Contains('_') && !stem.Any(char.IsDigit);
        IEnumerable<string> pool = plainWord ? subtree.Where(f => DirOf(f) == dir) : subtree;
        var exact = new List<string>();
        var family = new List<string>();
        foreach (string f in pool)
        {
            string name = f.Substring(f.LastIndexOf('/') + 1);
            string fs = name.Substring(0, name.Length - 4);
            if (fs == stem)
                exact.Add(f);
            else if (!hasExt && !char.IsDigit(stem[^1])
                && ((fs.StartsWith(stem, StringComparison.Ordinal) && fs.Skip(stem.Length).All(char.IsDigit))
                    || (stem.Contains('_') && stem.Length >= 6 && fs.StartsWith(stem + "_", StringComparison.Ordinal))))
                family.Add(f);
        }
        hits = exact.Count > 0 ? exact : family;
        return Choose(hits, dir, ambiguous);
    }

    /// <summary>One hit: the file. Several: those in the attribution's own folder if exactly one, else all of them are ambiguous.</summary>
    private static List<string> Choose(List<string> hits, string dir, List<string> ambiguous)
    {
        if (hits.Count <= 1)
            return hits;
        // The stem of a family (portal_open -> portal_open1, portal_open2) names all of them: they share one folder and one stem.
        List<string> own = hits.Where(h => DirOf(h) == dir).ToList();
        if (own.Count == 1)
            return own;
        if (hits.Select(DirOf).Distinct().Count() == 1)
            return hits;
        ambiguous.AddRange(hits);
        return new List<string>();
    }
}
