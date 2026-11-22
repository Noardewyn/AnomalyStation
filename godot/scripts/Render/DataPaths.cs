using System.IO;
using Godot;

namespace Anomaly.Client.Render;

/// <summary>
/// Where the client's plain-file data is (tg's converted icons and sounds, the 3D models, the converted maps): the folders the client reads itself, beside
/// the project in a checkout and beside the executable in a build (godot/tools/export-*.sh copy them there). In the project, <c>res://</c> is the project's
/// folder. An exported build has no such folder (its <c>res://</c> is the pack): its data is found beside the executable (Windows), in the macOS bundle's
/// <c>Contents/Resources</c>, beside the <c>.app</c> (a signed bundle cannot take files after it is signed), or in the working folder.
/// </summary>
public static class DataPaths
{
    /// <summary>The folder or file at <paramref name="relative"/> (a <c>/</c> path such as <c>assets/tg</c>), the first place that has it; the project's when none does.</summary>
    public static string Find(string relative)
    {
        string inProject = ProjectSettings.GlobalizePath("res://" + relative);
        if (Path.IsPathRooted(inProject) && Exists(inProject))
            return inProject;
        string exe = Path.GetDirectoryName(OS.GetExecutablePath()) ?? string.Empty;
        // (the last two before the working folder: Anomaly Editor's, in the export's editor/ folder, its own or in its .app; map-editor.md 5.13)
        foreach (string dir in new[] { exe, Path.Combine(exe, "..", "Resources"), Path.Combine(exe, "..", "..", ".."), Path.Combine(exe, ".."), Path.Combine(exe, "..", "..", "..", ".."), Directory.GetCurrentDirectory() })
        {
            string candidate = Path.GetFullPath(Path.Combine(dir, relative));
            if (Exists(candidate))
                return candidate;
        }
        return inProject;
    }

    private static bool Exists(string path) => Directory.Exists(path) || File.Exists(path);
}
