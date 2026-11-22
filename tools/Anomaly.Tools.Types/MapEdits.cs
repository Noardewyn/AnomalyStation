using System.Text.Json;
using Anomaly.Core.Tg;
using Anomaly.Tools.Maps;

namespace Anomaly.Tools.Types;

/// <summary>One var edit of a station map: the atom's typepath, the var, the kind of its value (a <see cref="DmVal"/> kind) and the map.</summary>
public sealed record MapEdit(string Path, string Var, string Kind, string Map);

/// <summary>
/// The var edits ("overrides") of tg's station maps, for the var typing (types-converter.md 3.5, "widening"): every map
/// file of every map json in <c>_maps/map_jsons</c> (the maps <c>config/maps.txt</c> can rotate to), read by the map
/// converter's own .dmm reader (<c>tools/Anomaly.Tools.Maps/DmmFile.cs</c>), and each value literal classified by the map
/// loader's <c>parse_constant</c> rules (<see cref="MapValue.Parse"/>), so the kinds are the ones the preloader assigns.
/// One edit per (path, var, kind), sorted: the result does not depend on file or line order.
/// </summary>
internal static class MapEdits
{
    public static List<MapEdit> Read(string tg)
    {
        string maps = Path.Combine(tg, "_maps");
        var seen = new SortedDictionary<(string, string, string), string>();
        foreach (string json in Directory.GetFiles(Path.Combine(maps, "map_jsons"), "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            foreach (string dmm in MapFiles(maps, json))
            {
                string rel = Path.GetRelativePath(tg, dmm).Replace('\\', '/');
                DmmFile file = DmmFile.Parse(File.ReadAllText(dmm), rel);
                foreach (DmmModel model in file.Models.Values)
                    foreach (DmmMember member in model.Members)
                        foreach ((string var, string literal) in member.Overrides)
                        {
                            var key = (member.Path, var, KindOf(literal));
                            if (!seen.ContainsKey(key)) seen[key] = rel;
                        }
            }
        }
        // The first map (in json name order) that gives the kind names it in the report.
        return seen.Select(kv => new MapEdit(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Value)).ToList();
    }

    private static IEnumerable<string> MapFiles(string maps, string json)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(json), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        string mapPath = doc.RootElement.GetProperty("map_path").GetString();
        JsonElement files = doc.RootElement.GetProperty("map_file");
        var names = files.ValueKind == JsonValueKind.Array ? files.EnumerateArray().Select(f => f.GetString()).ToList() : new List<string> { files.GetString() };
        foreach (string name in names)
        {
            string dmm = Path.Combine(maps, mapPath, name);
            if (!File.Exists(dmm)) throw new ToolException($"{json}: no map file {dmm}");
            yield return dmm;
        }
    }

    /// <summary>The <see cref="DmVal"/> kind of a map value literal: the DM value the preloader assigns (map-format.md 4.4).</summary>
    private static string KindOf(string literal) => MapValue.Parse(literal).Kind switch
    {
        MapValueKind.Number => "num",
        MapValueKind.List => "list",
        MapValueKind.Path => "type",
        MapValueKind.Resource => "res",
        MapValueKind.Null => "null",
        _ => "str", // Text, Empty ("") and tg's fallback (the literal as text)
    };
}
