using System;
using System.IO;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
/// <summary>
/// The client's user folder moved when the game was named Anomaly Station (D347): Godot kept it as app_userdata/SpaceStation3D,
/// project.godot now names its own folder AnomalyStation. A start moves what the old folder holds (the options, the reconnect
/// tokens that are also the admin keys, the map editor's settings, the speech models) into the new one, once: an entry the new
/// folder already has stays where it is, and the old logs stay behind.
/// </summary>
public static class UserFolderMove
{
    /// <summary>The old folder of <paramref name="dataDir"/> (Godot's OS.GetDataDir()): Godot names its folder "godot" on Linux.</summary>
    public static string OldFolder(string dataDir, bool linux) => Path.Combine(dataDir, linux ? "godot" : "Godot", "app_userdata", "SpaceStation3D");

    /// <summary>The files that were named after the old name: old name, new name.</summary>
    private static readonly (string Old, string New)[] Renamed = { ("ss3d-options.cfg", "options.cfg"), ("ss3d-imgui.ini", "imgui.ini") };

    /// <summary>Moves the entries of <paramref name="oldDir"/> into <paramref name="newDir"/>; returns how many were moved.</summary>
    public static int Run(string oldDir, string newDir)
    {
        if (!Directory.Exists(oldDir) || Path.GetFullPath(oldDir).Equals(Path.GetFullPath(newDir), StringComparison.OrdinalIgnoreCase))
            return 0;
        Directory.CreateDirectory(newDir);
        int moved = 0;
        foreach (string entry in Directory.EnumerateFileSystemEntries(oldDir))
        {
            string name = Path.GetFileName(entry);
            if (name == "logs")
                continue;
            foreach (var (old, renamed) in Renamed)
                if (name == old)
                    name = renamed;
            string target = Path.Combine(newDir, name);
            if (File.Exists(target) || Directory.Exists(target))
                continue;
            if (Directory.Exists(entry))
                Directory.Move(entry, target);
            else
                File.Move(entry, target);
            moved++;
        }
        return moved;
    }
}
