using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Anomaly.Tools.Maps;

/// <summary>
/// DV436 (the owner, 2026-11-14): tg's multi-z debug map gets empty space levels under and over its three decks, real levels one can build on and move
/// through. Applied to tg's text before the conversion (tg's files are never changed): the new levels are space open space (what the map patch makes the
/// decks' airless open space, DV411; written here, not patched, as changing ~25k turfs at boot stalls atmos) over a bottom level of plain space, the decks'
/// own plain space becomes space open space so the column below is reachable, the top deck gets an airless plating roof, the elevator's preset floor names and hall lanterns keep numbering the decks from 1, and the map json gets a trait set per level.
/// </summary>
public static class MultizPadding
{
    public const string Map = "multiz_debug";
    public const int Below = 5, Above = 5;

    private const string OpenTurf = "/turf/open/space/openspace", AirlessTurf = "/turf/open/openspace/airless", SpaceTurf = "/turf/open/space/basic", RoofTurf = "/turf/open/floor/plating/airless";
    private static readonly Regex Block = new(@"^\((\d+),(\d+),(\d+)\) = \{""", RegexOptions.Multiline);
    private static readonly Regex Key = new(@"^""([A-Za-z]+)"" = \(", RegexOptions.Multiline);

    /// <summary>tg's multiz.dmm with <see cref="Below"/> levels under it and <see cref="Above"/> over it.</summary>
    public static string PadDmm(string dmm)
    {
        dmm = dmm.Replace("\r\n", "\n");
        int bodyAt = Block.Match(dmm).Index;
        string header = dmm[..bodyAt].Replace(SpaceTurf + ",", OpenTurf + ",");
        string body = dmm[bodyAt..];

        // DV239 keys the preset names by tg's z (the station's bottom deck is 2) and the lanterns number a floor z + 1 - the offset
        header = Regex.Replace(header, @"preset_destination_names = list\(([^)]*)\)",
            m => "preset_destination_names = list(" + Regex.Replace(m.Groups[1].Value, @"""(\d+)""=", k => $"\"{int.Parse(k.Groups[1].Value) + Below}\"=") + ")");
        header = Regex.Replace(header, @"(/obj/machinery/lift_indicator[^{\n]*\{\n)", "$1\tlowest_floor_offset = " + (1 + Below) + ";\n");

        var keys = Key.Matches(header).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        int keyLength = keys.First().Length;
        // the top deck had nothing over it: a roof goes over all of it but its outer airless open space, or its air vents up (an open turf's zAirOut(UP))
        MatchCollection defs = Key.Matches(header);
        var indoor = defs.Select((m, i) => (Key: m.Groups[1].Value, Text: header[m.Index..(i + 1 < defs.Count ? defs[i + 1].Index : header.Length)]))
            .Where(d => !d.Text.Contains("\n" + OpenTurf + ",\n", StringComparison.Ordinal) && !d.Text.Contains("\n" + AirlessTurf + ",\n", StringComparison.Ordinal)).Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
        string[] free = Enumerable.Range(0, 52 * 52).Select(i => Letter(i / 52) + Letter(i % 52)).Where(k => k.Length == keyLength && !keys.Contains(k)).Take(3).ToArray();
        if (keyLength != 2 || free.Length < 3) throw new FormatException("multiz.dmm: no free two-letter keys for the padding");
        string open = free[0], space = free[1], roof = free[2];
        header = header.TrimEnd('\n') + $"\n\"{open}\" = (\n{OpenTurf},\n/area/space)\n\"{space}\" = (\n{SpaceTurf},\n/area/space)\n\"{roof}\" = (\n{RoofTurf},\n/area/space)\n\n";

        MatchCollection blocks = Block.Matches(body);
        int width = blocks.Max(m => int.Parse(m.Groups[1].Value)), levels = blocks.Max(m => int.Parse(m.Groups[3].Value));
        int height = body[blocks[0].Index..].Split('\n').Skip(1).TakeWhile(l => l != "\"}").Count();
        var roofed = new HashSet<(int X, int Row)>();
        foreach (Match m in blocks.Where(m => int.Parse(m.Groups[3].Value) == levels))
        {
            string[] rows = body[(m.Index + m.Length)..].Split('\n').Skip(1).Take(height).ToArray();
            for (int row = 0; row < height; row++)
                if (indoor.Contains(rows[row])) roofed.Add((int.Parse(m.Groups[1].Value), row));
        }
        if (roofed.Count == 0) throw new FormatException("multiz.dmm: the top deck has nothing to roof");
        body = Block.Replace(body, m => $"({m.Groups[1].Value},{m.Groups[2].Value},{int.Parse(m.Groups[3].Value) + Below}) = {{\"");

        string Level(int z, Func<int, int, string> key)
            => string.Concat(Enumerable.Range(1, width).Select(x => $"({x},1,{z}) = {{\"\n" + string.Concat(Enumerable.Range(0, height).Select(row => key(x, row) + "\n")) + "\"}\n"));
        string under = string.Concat(Enumerable.Range(1, Below).Select(z => Level(z, (_, _) => z == 1 ? space : open)));
        int first = levels + Below + 1;
        string over = string.Concat(Enumerable.Range(first, Above).Select(z => Level(z, (x, row) => z == first && roofed.Contains((x, row)) ? roof : open)));
        return header + under + body.TrimEnd('\n') + "\n" + over;
    }

    /// <summary>The map json with a trait set per level: the bottom one plain, every other one over open space (tg's multi-z Baseturf).</summary>
    public static string PadJson(string json)
    {
        JsonObject root = JsonNode.Parse(json, documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true })!.AsObject();
        JsonArray old = root["traits"]!.AsArray();
        JsonObject Open() => new() { ["Baseturf"] = "/turf/open/space/openspace", ["Linkage"] = "Cross" };
        var traits = new JsonArray { new JsonObject { ["Linkage"] = "Cross" } };
        for (int i = 1; i < Below; i++) traits.Add(Open());
        traits.Add(Open()); // the bottom deck, now over the open column
        foreach (JsonNode deck in old.Skip(1)) traits.Add(deck!.DeepClone());
        for (int i = 0; i < Above; i++) traits.Add(Open());
        root["traits"] = traits;
        return root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n";
    }

    private static string Letter(int i) => ((char)(i < 26 ? 'a' + i : 'A' + i - 26)).ToString();
}
