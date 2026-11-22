using System.Globalization;
using System.Linq;
using System.Text;
using Anomaly.Core.Tg;

namespace Anomaly.Tools.Maps;

/// <summary>A validation finding (docs/ai/specs/map-format.md 6): the rule, the .dmm line and model key, and what is wrong.</summary>
public sealed record MapFinding(int Rule, int Line, string Key, string Message, bool BadPath)
{
    public override string ToString() => $"line {Line} key \"{Key}\": rule {Rule}: {Message}";
}

/// <summary>What a conversion produced.</summary>
public sealed class ConversionResult
{
    /// <summary>The .as3dmap text (null when the map was refused).</summary>
    public string Text { get; init; }

    /// <summary>Every finding; the map is refused when any is not allowed.</summary>
    public IReadOnlyList<MapFinding> Findings { get; init; }

    /// <summary>The unknown paths and vars (tg's <c>bad_paths</c>), with the model keys that hold them.</summary>
    public IReadOnlyDictionary<string, SortedSet<string>> BadPaths { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int Levels { get; init; }

    /// <summary>The tiles (width x height x levels).</summary>
    public int Tiles { get; init; }

    /// <summary>The tile lines written (tiles that are not the default).</summary>
    public int Lines { get; init; }

    /// <summary>The atoms over all tiles (turfs, areas and movables).</summary>
    public int Atoms { get; init; }

    /// <summary>The distinct typepaths over all tiles.</summary>
    public int DistinctTypes { get; init; }

    /// <summary>The distinct areas over all tiles.</summary>
    public int Areas { get; init; }

    /// <summary>The movables over all tiles.</summary>
    public int Movables { get; init; }
}

/// <summary>
/// The .dmm (TGM or DMM) to .as3dmap converter (docs/ai/specs/map-format.md 3, 4, 6). Deterministic: two conversions of one
/// text are byte-equal. Members keep tg's order inside each model; values keep their literal text.
/// </summary>
public static class MapConverter
{
    /// <summary>The tool's version, written into the maps folder's stamp.</summary>
    public const string ToolVersion = "2";

    /// <summary>Converts <paramref name="dmmText"/>; <paramref name="sourceName"/> is the path written in the <c>source</c> line (with the tg commit when known).</summary>
    public static ConversionResult Convert(string dmmText, string sourceName, TypeIndex types) => Convert(dmmText, sourceName, types, allowBadPaths: false, tgCommit: null);

    /// <summary>
    /// Converts <paramref name="dmmText"/>. With <paramref name="allowBadPaths"/> unknown paths and vars are left out (and
    /// listed) instead of refusing the map; every other finding still refuses it.
    /// </summary>
    public static ConversionResult Convert(string dmmText, string sourceName, TypeIndex types, bool allowBadPaths, string tgCommit)
    {
        types ??= TypeIndex.Permissive();
        DmmFile dmm = DmmFile.Parse(dmmText, sourceName);
        var findings = new List<MapFinding>();
        var badPaths = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        // The members of every model, validated once per model; their line text in our format.
        var modelText = new Dictionary<string, string>(StringComparer.Ordinal);
        var memberPaths = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var modelMembers = new Dictionary<string, List<DmmMember>>(StringComparer.Ordinal);
        foreach (DmmModel model in dmm.Models.Values.OrderBy(m => m.Line))
        {
            string text = ModelText(model, types, findings, badPaths, out string[] paths, out List<DmmMember> kept);
            modelText[model.Key] = text;
            memberPaths[text] = paths;
            modelMembers[model.Key] = kept;
        }

        int w = dmm.MaxX, h = dmm.MaxY, levels = dmm.MaxZ;
        if (w < 1 || h < 1 || levels < 1) findings.Add(new MapFinding(6, 1, "", "the map has no grid", false));
        if (w > 255 || h > 255 || levels > 16) findings.Add(new MapFinding(6, 1, "", $"the map's size {w} x {h} x {levels} is above 255 x 255 x 16", false));
        foreach (((int X, int Y, int Z) tile, int line) in dmm.Duplicates)
            findings.Add(new MapFinding(6, line, "", $"the tile {tile.X},{tile.Y},{tile.Z} is in two grid sets", false));
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        // D147: the tiles of doors on plating take their neighbour's floor; their lines are made here.
        var tileText = new Dictionary<(int, int, int), string>();
        if (findings.All(f => f.Rule != 6))
        {
            foreach (((int, int, int) tile, DmmMember turf) in DoorFloors.Plan(w, h, levels, t => dmm.Tiles.TryGetValue(t, out string k) && modelMembers.TryGetValue(k, out List<DmmMember> l) ? l : null, types))
            {
                List<DmmMember> members = DoorFloors.Apply(modelMembers[dmm.Tiles[tile]], turf, types);
                string text = MembersText(members);
                tileText[tile] = text;
                memberPaths[text] = members.Select(m => m.Path).ToArray();
            }
            for (int z = 1; z <= levels; z++)
            {
                for (int y = 1; y <= h; y++)
                {
                    for (int x = 1; x <= w; x++)
                    {
                        if (!dmm.Tiles.TryGetValue((x, y, z), out string key))
                        {
                            findings.Add(new MapFinding(6, 1, "", $"the tile {x},{y},{z} is not in any grid set", false));
                            continue;
                        }
                        if (!modelText.TryGetValue(key, out string text))
                        {
                            findings.Add(new MapFinding(6, 1, key, $"the tile {x},{y},{z} names a model that does not exist", false));
                            continue;
                        }
                        if (tileText.TryGetValue((x, y, z), out string changed)) text = changed;
                        counts[text] = counts.TryGetValue(text, out int c) ? c + 1 : 1;
                    }
                }
            }
        }

        bool refused = findings.Any(f => !(allowBadPaths && f.BadPath));
        if (refused)
        {
            return new ConversionResult { Findings = findings, BadPaths = badPaths, Width = w, Height = h, Levels = levels };
        }

        // The default: the most frequent model (ties: the ordinal smallest text).
        string defaultText = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;
        var sb = new StringBuilder(4 << 20);
        sb.Append("anomaly-map ").Append(MapText.FormatVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("source ").Append(sourceName).Append(" tg ").Append(string.IsNullOrEmpty(tgCommit) ? "unknown" : tgCommit).Append('\n');
        sb.Append("size ").Append(w).Append(' ').Append(h).Append(' ').Append(levels).Append('\n');
        sb.Append("chunk ").Append(MapText.ChunkSize).Append('\n');
        sb.Append("default ").Append(defaultText).Append('\n');
        int lines = 0, atoms = 0, movables = 0;
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        var areas = new HashSet<string>(StringComparer.Ordinal);
        const int C = MapText.ChunkSize;
        for (int z = 1; z <= levels; z++)
        {
            for (int cy = 1; cy <= h; cy += C)
            {
                for (int cx = 1; cx <= w; cx += C)
                {
                    bool header = false;
                    for (int y = Math.Min(cy + C - 1, h); y >= cy; y--)
                    {
                        for (int x = cx; x <= Math.Min(cx + C - 1, w); x++)
                        {
                            if (!tileText.TryGetValue((x, y, z), out string text)) text = modelText[dmm.Tiles[(x, y, z)]];
                            string[] paths = memberPaths[text];
                            atoms += paths.Length;
                            foreach (string p in paths)
                            {
                                distinct.Add(p);
                                if (!types.IsA(p, "/turf") && !types.IsA(p, "/area")) movables++;
                            }
                            areas.Add(paths[^1]);
                            if (text == defaultText) continue;
                            if (!header)
                            {
                                sb.Append('\n').Append("[chunk ").Append(cx).Append(' ').Append(cy).Append(' ').Append(z).Append("]\n");
                                header = true;
                            }
                            sb.Append(x).Append(',').Append(y).Append(": ").Append(text).Append('\n');
                            lines++;
                        }
                    }
                }
            }
        }
        return new ConversionResult
        {
            Text = sb.ToString(),
            Findings = findings,
            BadPaths = badPaths,
            Width = w,
            Height = h,
            Levels = levels,
            Tiles = w * h * levels,
            Lines = lines,
            Atoms = atoms,
            DistinctTypes = distinct.Count,
            Areas = areas.Count,
            Movables = movables,
        };
    }

    /// <summary>Whether <paramref name="text"/> is a typepath of map-format.md 4.3 (<c>/seg/seg</c>, segments [A-Za-z0-9_]+).</summary>
    public static bool IsTypePath(string text)
    {
        if (text.Length < 2 || text[0] != '/') return false;
        bool segment = false;
        for (int i = 1; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '/')
            {
                if (!segment) return false;
                segment = false;
            }
            else if (char.IsAsciiLetterOrDigit(c) || c == '_') segment = true;
            else return false;
        }
        return segment;
    }

    /// <summary>Whether <paramref name="text"/> is a var name of map-format.md 4.3 (<c>[A-Za-z_][A-Za-z0-9_]*</c>).</summary>
    public static bool IsVarName(string text)
    {
        if (text.Length == 0 || !(char.IsAsciiLetter(text[0]) || text[0] == '_')) return false;
        foreach (char c in text)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_')) return false;
        }
        return true;
    }

    // One model as a line of our format, validating rules 1-5 (map-format.md 6).
    private static string ModelText(DmmModel model, TypeIndex types, List<MapFinding> findings, SortedDictionary<string, SortedSet<string>> badPaths, out string[] paths, out List<DmmMember> kept)
    {
        kept = new List<DmmMember>();
        foreach (DmmMember member in model.Members)
        {
            if (!IsTypePath(member.Path) || !types.HasType(member.Path))
            {
                findings.Add(new MapFinding(1, model.Line, model.Key, "unknown path " + member.Path, true));
                Bad(badPaths, member.Path, model.Key);
                continue;
            }
            var overrides = new List<(string Var, string Literal)>();
            foreach ((string var, string literal) in member.Overrides)
            {
                if (!IsVarName(var) || !types.HasVar(member.Path, var))
                {
                    findings.Add(new MapFinding(3, model.Line, model.Key, $"{member.Path} has no var '{var}'", true));
                    Bad(badPaths, member.Path + "." + var, model.Key);
                    continue;
                }
                CheckValue(literal, var, member.Path, model, types, findings);
                overrides.Add((var, literal));
            }
            kept.Add(new DmmMember(member.Path, overrides));
        }

        // rule 2: movables, then one or more turfs, then the area
        int count = kept.Count;
        if (count < 2 || !types.IsA(kept[^1].Path, "/area"))
            findings.Add(new MapFinding(2, model.Line, model.Key, "the last member is not an /area", false));
        int firstTurf = kept.FindIndex(m => types.IsA(m.Path, "/turf"));
        if (firstTurf < 0)
            findings.Add(new MapFinding(2, model.Line, model.Key, "no /turf", false));
        for (int i = 0; i < count - 1; i++)
        {
            bool turf = types.IsA(kept[i].Path, "/turf");
            if (types.IsA(kept[i].Path, "/area"))
                findings.Add(new MapFinding(2, model.Line, model.Key, "an /area before the last member: " + kept[i].Path, false));
            else if (firstTurf >= 0 && i > firstTurf && !turf)
                findings.Add(new MapFinding(2, model.Line, model.Key, "a movable after the turf: " + kept[i].Path, false));
        }

        paths = kept.Select(m => m.Path).ToArray();
        return MembersText(kept);
    }

    // A tile's members as a line of our format (map-format.md 4.3).
    private static string MembersText(List<DmmMember> kept)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < kept.Count; i++)
        {
            if (i > 0) sb.Append(" | ");
            sb.Append(kept[i].Path);
            if (kept[i].Overrides.Count == 0) continue;
            sb.Append('{');
            for (int k = 0; k < kept[i].Overrides.Count; k++)
            {
                if (k > 0) sb.Append("; ");
                sb.Append(kept[i].Overrides[k].Var).Append(" = ").Append(kept[i].Overrides[k].Literal);
            }
            sb.Append('}');
        }
        return sb.ToString();
    }

    // rules 4 and 5: the value parses by map-format.md 4.4 rules 1-7 (a bare word only as a list key), a list never has a
    // plain item before an association (tg's readlist misreads it), a typepath names a type, and the literal is one line.
    private static void CheckValue(string literal, string var, string path, DmmModel model, TypeIndex types, List<MapFinding> findings)
    {
        if (literal.Contains('\n') || literal.Contains('\r') || literal.Contains('\t'))
        {
            findings.Add(new MapFinding(4, model.Line, model.Key, $"{path}.{var}: a value on more than one line or with a tab", false));
            return;
        }
        MapValue value;
        try
        {
            value = MapValue.Parse(literal);
        }
        catch (MapTextException e)
        {
            findings.Add(new MapFinding(4, model.Line, model.Key, $"{path}.{var}: {e.Message}", false));
            return;
        }
        Check(value, false);

        void Check(MapValue v, bool isKey)
        {
            switch (v.Kind)
            {
                case MapValueKind.Fallback when !isKey:
                    findings.Add(new MapFinding(4, model.Line, model.Key, $"{path}.{var}: '{v.Literal}' is not a constant (tg would read it as text)", false));
                    break;
                case MapValueKind.Path when !types.HasType(v.Text):
                    findings.Add(new MapFinding(5, model.Line, model.Key, $"{path}.{var}: the typepath {v.Text} does not exist", false));
                    break;
                case MapValueKind.List:
                    bool plain = false;
                    foreach ((MapValue key, MapValue item) in v.Items)
                    {
                        if (key == null) plain = true;
                        else if (plain)
                            findings.Add(new MapFinding(4, model.Line, model.Key, $"{path}.{var}: a plain item before an association in {v.Literal}", false));
                        if (key != null) Check(key, true);
                        Check(item, false);
                    }
                    break;
            }
        }
    }

    private static void Bad(SortedDictionary<string, SortedSet<string>> badPaths, string what, string key)
    {
        if (!badPaths.TryGetValue(what, out SortedSet<string> keys)) badPaths[what] = keys = new SortedSet<string>(StringComparer.Ordinal);
        keys.Add(key);
    }

    /// <summary>
    /// The round trip (map-format.md 3, <c>--check</c>): the converted text read back by the engine reader gives every tile
    /// the .dmm's model (the same members in the same order, the same overrides and literals; unknown paths and vars that
    /// were left out are left out here too). Returns the differences (at most <paramref name="limit"/>).
    /// </summary>
    public static List<string> RoundTrip(string dmmText, string convertedText, TypeIndex types, int limit = 20)
    {
        types ??= TypeIndex.Permissive();
        var differences = new List<string>();
        DmmFile dmm = DmmFile.Parse(dmmText, "dmm");
        ParsedMapText map = MapText.Parse(convertedText, "converted");
        if (map.Width != dmm.MaxX || map.Height != dmm.MaxY || map.Levels != dmm.MaxZ)
        {
            differences.Add($"size {map.Width} x {map.Height} x {map.Levels}, the .dmm's {dmm.MaxX} x {dmm.MaxY} x {dmm.MaxZ}");
            return differences;
        }
        var kept = dmm.Models.ToDictionary(kv => kv.Key, kv => kv.Value.Members
            .Where(m => types.HasType(m.Path))
            .Select(m => new DmmMember(m.Path, m.Overrides.Where(o => types.HasVar(m.Path, o.Var)).ToList()))
            .ToList(), StringComparer.Ordinal);
        // D147: the door tiles the converter gave a neighbour's floor are expected with it.
        Dictionary<(int X, int Y, int Z), DmmMember> doorFloors = DoorFloors.Plan(dmm.MaxX, dmm.MaxY, dmm.MaxZ, t => dmm.Tiles.TryGetValue(t, out string k) ? kept[k] : null, types);
        foreach (((int X, int Y, int Z) tile, string key) in dmm.Tiles)
        {
            List<DmmMember> expected = doorFloors.TryGetValue(tile, out DmmMember turf) ? DoorFloors.Apply(kept[key], turf, types) : kept[key];
            IReadOnlyList<MapMember> actual = map.ModelAt(tile.X, tile.Y, tile.Z).Members;
            string problem = Compare(expected, actual);
            if (problem == null) continue;
            differences.Add($"{tile.X},{tile.Y},{tile.Z} (key {key}): {problem}");
            if (differences.Count >= limit) break;
        }
        return differences;
    }

    private static string Compare(List<DmmMember> expected, IReadOnlyList<MapMember> actual)
    {
        if (expected.Count != actual.Count) return $"{actual.Count} members, the .dmm's {expected.Count}";
        for (int i = 0; i < expected.Count; i++)
        {
            if (expected[i].Path != actual[i].Path) return $"member {i + 1} is {actual[i].Path}, the .dmm's {expected[i].Path}";
            if (expected[i].Overrides.Count != actual[i].Overrides.Count) return $"{actual[i].Path}: {actual[i].Overrides.Count} overrides, the .dmm's {expected[i].Overrides.Count}";
            for (int k = 0; k < expected[i].Overrides.Count; k++)
            {
                (string var, string literal) = expected[i].Overrides[k];
                if (var != actual[i].Overrides[k].Var || literal != actual[i].Overrides[k].Value.Literal)
                    return $"{actual[i].Path}: override {k + 1} is {actual[i].Overrides[k].Var} = {actual[i].Overrides[k].Value.Literal}, the .dmm's {var} = {literal}";
            }
        }
        return null;
    }
}
