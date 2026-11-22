using System.IO;
using System.Text.RegularExpressions;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>
/// A minimal independent reader of tg's own TGM file (the clone's <c>_maps/map_files/MetaStation/MetaStation.dmm</c>): the
/// member paths of each model key and the key of each cell. It shares no code with the port's converter or map reader, so a
/// test that compares a loaded world with it checks the converter and the load path against tg's source map. TGM
/// (reader.dm:13-62): a model is <c>"key" = (</c>, one member per line (a path line at column 0, its var edits indented and
/// closed by <c>},</c>), the area last and closed by <c>)</c>; the grid is one column block per x, <c>(x,1,z) = {"</c> then one
/// key per line from the top row (y = maxy) down, then <c>"}</c>.
/// </summary>
internal sealed class TgDmm
{
    /// <summary>The member paths (movables first, then the turf, then the area) of each model key.</summary>
    public Dictionary<string, List<string>> Models { get; } = new(StringComparer.Ordinal);

    /// <summary>The key of the cell (x, y, z), x and y from 1.</summary>
    public Dictionary<(int X, int Y, int Z), string> Cells { get; } = new();

    public int MaxX { get; private set; }

    public int MaxY { get; private set; }

    public int MaxZ { get; private set; }

    private static readonly Regex ModelStart = new(@"^""([A-Za-z]+)"" = \($", RegexOptions.CultureInvariant);
    private static readonly Regex ColumnStart = new(@"^\((\d+),(\d+),(\d+)\) = \{""$", RegexOptions.CultureInvariant);

    public static TgDmm Load(string path)
    {
        var dmm = new TgDmm();
        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            Match model = ModelStart.Match(line);
            if (model.Success)
            {
                var members = new List<string>();
                for (i++; i < lines.Length; i++)
                {
                    string member = lines[i];
                    if (member.Length == 0 || member[0] != '/') continue; // a var edit line or the closing "}," of one
                    bool last = member.EndsWith(")", StringComparison.Ordinal) && !member.Contains('{', StringComparison.Ordinal);
                    int end = member.IndexOfAny(new[] { ',', '{', ')' });
                    members.Add(end < 0 ? member : member.Substring(0, end));
                    if (last) break;
                }
                dmm.Models[model.Groups[1].Value] = members;
                continue;
            }
            Match column = ColumnStart.Match(line);
            if (column.Success)
            {
                int x = int.Parse(column.Groups[1].Value);
                int z = int.Parse(column.Groups[3].Value);
                var keys = new List<string>();
                for (i++; i < lines.Length && lines[i] != "\"}"; i++) keys.Add(lines[i]);
                for (int r = 0; r < keys.Count; r++)
                {
                    int y = keys.Count - r;
                    dmm.Cells[(x, y, z)] = keys[r];
                    dmm.MaxY = Math.Max(dmm.MaxY, y);
                }
                dmm.MaxX = Math.Max(dmm.MaxX, x);
                dmm.MaxZ = Math.Max(dmm.MaxZ, z);
            }
        }
        return dmm;
    }

    /// <summary>The model of a cell: movables, turf, area (paths).</summary>
    public List<string> At(int x, int y, int z) => Models[Cells[(x, y, z)]];
}
