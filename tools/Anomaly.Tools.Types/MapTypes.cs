using System.Text.RegularExpressions;

namespace Anomaly.Tools.Types;

/// <summary>The typepaths a .dmm map uses (its key definitions), for the Meta closure (types-converter.md 7).</summary>
internal static class MapTypes
{
    public const string MetaStation = "_maps/map_files/MetaStation/MetaStation.dmm";

    private static readonly Regex PathAtLineStart = new(@"^\s*\(?(/[a-z][A-Za-z0-9_/]*)", RegexOptions.Compiled);
    private static readonly Regex PathInList = new(@"[(,]\s*(/[A-Za-z0-9_]+(?:/[A-Za-z0-9_]+)*)", RegexOptions.Compiled);

    public static List<string> Read(string dmm)
    {
        var types = new SortedSet<string>(StringComparer.Ordinal);
        bool inKeys = true;
        foreach (string line in File.ReadLines(dmm))
        {
            if (line.StartsWith("(1,1,", StringComparison.Ordinal)) inKeys = false; // the grid follows the keys
            if (!inKeys) continue;
            Match m = PathAtLineStart.Match(line);
            if (m.Success) types.Add(m.Groups[1].Value.TrimEnd('/'));
            if (line.StartsWith('"'))
                foreach (Match x in PathInList.Matches(line)) types.Add(x.Groups[1].Value);
        }
        return types.ToList();
    }
}
