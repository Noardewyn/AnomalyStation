namespace Anomaly.Tools.Defines;

/// <summary>Turns the bodies of dmdoc and trailing comments into plain doc text.</summary>
public static class DocText
{
    /// <summary>Strips the gutter <c>*</c> of block comment lines, trims, drops blank edges and joins with <c>\n</c>; null when empty.</summary>
    public static string Clean(IEnumerable<string> pieces)
    {
        var lines = new List<string>();
        foreach (string piece in pieces)
        {
            foreach (string raw in piece.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith('*'))
                    line = line.Substring(1).Trim();
                if (line.Length == 0 && (lines.Count == 0 || lines[^1].Length == 0))
                    continue;
                lines.Add(line);
            }
        }
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }
}
