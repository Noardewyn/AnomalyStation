using System.Diagnostics;
using System.Reflection;

namespace Anomaly.Tools.Types;

/// <summary>The two clones the tool reads, their commits, and the scratch folder tg is compiled in.</summary>
internal static class Clones
{
    /// <summary>OpenDream's pinned commit (D110).</summary>
    public const string OpenDreamPin = "1362abc5";

    /// <summary>RobustToolbox's pinned commit (OpenDream's submodule).</summary>
    public const string RobustToolboxPin = "af2a7d04";

    /// <summary>tg's snapshot (D86).</summary>
    public const string TgPin = "1d99bd7f";

    /// <summary>
    /// tg's production configuration (translation-rules.md 1.7), the define set of tools/coloring (color_procs.py
    /// PREDEFINED): CBT and DISABLE_DREAMLUAU defined, DM_VERSION 516 and DM_BUILD 1685 (OpenDream sets those two
    /// itself from its settings). OpenDream always defines OPENDREAM; CIBUILDING is added, as tg's own CI compiles with
    /// OpenDream, so that tg does not define OPENDREAM_REAL, which would drop BYOND-only code from the build (the tracy
    /// admin verbs, the debugger hooks). Under OPENDREAM, CIBUILDING does not define UNIT_TESTS
    /// (_compile_options.dm:162). What OPENDREAM still changes is listed in types-converter.md 2.1.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Defines = new SortedDictionary<string, string>(StringComparer.Ordinal)
    {
        ["CBT"] = "",
        ["CIBUILDING"] = "",
        ["DISABLE_DREAMLUAU"] = "",
    };

    public const int DmVersion = 516;
    public const int DmBuild = 1685;

    public static string DefinesText => string.Join(" ", Defines.Select(d => d.Value.Length == 0 ? d.Key : d.Key + "=" + d.Value))
        + $" DM_VERSION={DmVersion} DM_BUILD={DmBuild}";

    /// <summary>The OpenDream clone this build compiled its front end from (null when built without one).</summary>
    public static string CompiledOpenDream => typeof(Clones).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "AnomalyOpenDream")?.Value;

    public static string GitHead(string dir)
    {
        try
        {
            var psi = new ProcessStartInfo("git", new[] { "-C", dir, "rev-parse", "HEAD" })
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using Process p = Process.Start(psi);
            string head = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return p.ExitCode == 0 && head.Length >= 8 ? head : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// A folder of symbolic links to tg's top-level entries plus a copy of <c>tgstation.dme</c>, so that OpenDream
    /// writes its output (<c>tgstation.json</c>) there and never into tg.
    /// </summary>
    public static string MakeFarm(string tg, string scratch)
    {
        string farm = Path.Combine(scratch, "tgfarm");
        if (Directory.Exists(farm)) Directory.Delete(farm, recursive: true);
        Directory.CreateDirectory(farm);
        foreach (string entry in Directory.EnumerateFileSystemEntries(tg).OrderBy(e => e, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(entry);
            if (name is "tgstation.dme" or "tgstation.json" or "tgstation.int" or "tgstation.dmb" or "tgstation.rsc" or ".git") continue;
            string link = Path.Combine(farm, name);
            if (Directory.Exists(entry)) Directory.CreateSymbolicLink(link, entry);
            else File.CreateSymbolicLink(link, entry);
        }
        File.Copy(Path.Combine(tg, "tgstation.dme"), Path.Combine(farm, "tgstation.dme"));
        return Path.Combine(farm, "tgstation.dme");
    }
}
