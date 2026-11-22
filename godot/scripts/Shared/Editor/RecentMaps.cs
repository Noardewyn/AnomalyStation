using System.IO;
using System.Linq;

namespace Anomaly.Client.Shared.Editor;

/// <summary>The maps the editor had open last (File, Recent maps; docs/ai/specs/map-editor.md 5.2): the newest first, each once, the last ten.</summary>
public static class RecentMaps
{
    public const int Max = 10;

    /// <summary>The list with the map of <paramref name="path"/> at its head.</summary>
    public static string[] With(IEnumerable<string> recent, string path)
    {
        string full = Path.GetFullPath(path);
        // (a file has one name whatever its letters' case, but on Linux)
        StringComparison names = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return recent.Where(other => !string.Equals(other, full, names)).Prepend(full).Take(Max).ToArray();
    }
}
