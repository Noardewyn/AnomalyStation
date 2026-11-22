using System.IO;
using Godot;

namespace Anomaly.Client.Render;

/// <summary>
/// Where the converted tg icons are (tools/Anomaly.Tools.Icons writes them into <c>godot/assets/tg</c>, git-ignored and
/// <c>.gdignore</c>d: Godot never imports them, the client reads the files itself). <c>ANOMALY_ICON_ROOT</c> overrides the folder.
/// </summary>
public static class IconPaths
{
    /// <summary>The folder of the converted icons (it may not exist; see <see cref="Exists"/>).</summary>
    public static string Root
    {
        get
        {
            string overrideRoot = System.Environment.GetEnvironmentVariable("ANOMALY_ICON_ROOT");
            return string.IsNullOrEmpty(overrideRoot) ? DataPaths.Find("assets/tg") : overrideRoot;
        }
    }

    /// <summary>True when the folder holds converted icons.</summary>
    public static bool Exists => Directory.Exists(Path.Combine(Root, "icons"));
}
