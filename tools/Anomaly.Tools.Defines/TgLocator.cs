namespace Anomaly.Tools.Defines;

/// <summary>
/// Finds the read-only tgstation clone: <c>--tg</c>, else <c>$ANOMALY_TGSTATION</c>, else <c>../tgstation</c> next to
/// the repository (the directory holding <c>AnomalyStation.sln</c>), else, inside a git worktree, <c>../tgstation</c>
/// next to the main checkout.
/// </summary>
public static class TgLocator
{
    public const string EnvironmentVariable = "ANOMALY_TGSTATION";
    public const string SolutionFile = "AnomalyStation.sln";

    public static string Locate(string explicitPath, params string[] startDirectories)
    {
        if (!string.IsNullOrEmpty(explicitPath))
            return IsTgRoot(explicitPath) ? Path.GetFullPath(explicitPath) : throw new DirectoryNotFoundException($"--tg {explicitPath}: no tgstation.dme there");
        string fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrEmpty(fromEnvironment))
            return IsTgRoot(fromEnvironment) ? Path.GetFullPath(fromEnvironment) : throw new DirectoryNotFoundException($"${EnvironmentVariable}={fromEnvironment}: no tgstation.dme there");
        foreach (string start in startDirectories.Where(s => !string.IsNullOrEmpty(s)))
        {
            string repo = FindRepositoryRoot(start);
            if (repo == null)
                continue;
            string next = Path.GetFullPath(Path.Combine(repo, "..", "tgstation"));
            if (IsTgRoot(next))
                return next;
            string main = MainCheckout(repo);
            if (main != null)
            {
                string besideMain = Path.GetFullPath(Path.Combine(main, "..", "tgstation"));
                if (IsTgRoot(besideMain))
                    return besideMain;
            }
        }
        return null;
    }

    public static bool IsTgRoot(string path) => File.Exists(Path.Combine(path, "tgstation.dme"));

    /// <summary>The nearest directory at or above <paramref name="start"/> that holds the solution file.</summary>
    public static string FindRepositoryRoot(string start)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(start));
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFile)))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// For a git worktree (<c>.git</c> is a file <c>gitdir: &lt;main&gt;/.git/worktrees/&lt;name&gt;</c> whose
    /// <c>commondir</c> points at the main <c>.git</c>), the main checkout's directory; null otherwise.
    /// </summary>
    public static string MainCheckout(string repo)
    {
        string dotGit = Path.Combine(repo, ".git");
        if (!File.Exists(dotGit))
            return null;
        string pointer = File.ReadAllText(dotGit).Trim();
        if (!pointer.StartsWith("gitdir:", StringComparison.Ordinal))
            return null;
        string gitDir = Path.GetFullPath(Path.Combine(repo, pointer.Substring(7).Trim()));
        string commonDir = gitDir;
        string commonFile = Path.Combine(gitDir, "commondir");
        if (File.Exists(commonFile))
            commonDir = Path.GetFullPath(Path.Combine(gitDir, File.ReadAllText(commonFile).Trim()));
        return Path.GetDirectoryName(commonDir.TrimEnd(Path.DirectorySeparatorChar));
    }
}
