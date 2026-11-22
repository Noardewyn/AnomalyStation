using System.Reflection;

namespace Anomaly.Protocol;

/// <summary>
/// The version of this build (docs/versioning.md), read from the informational version that Version.props and the SDK
/// stamp on every assembly: <c>0.5.0+&lt;commit&gt;</c>, with <c>.dirty</c> after the commit when the build was made from
/// uncommitted changes. The server prints it when it starts and the client shows it on the connect screen and in its
/// window title, so a play-test report names the build it came from. It is not the wire protocol's version
/// (<see cref="ProtocolVersion"/>), which alone decides whether a client and a server can talk.
/// </summary>
public static class BuildInfo
{
    private const int ShortCommitLength = 8;

    /// <summary>The release number, <c>MAJOR.MINOR.PATCH</c> (Version.props).</summary>
    public static string Version { get; }

    /// <summary>The commit the build was made from, its first 8 hex digits; empty when the build had no git.</summary>
    public static string Commit { get; }

    /// <summary>True when the build was made from a working tree with uncommitted changes to tracked files.</summary>
    public static bool Dirty { get; }

    /// <summary>The one-line form for logs, titles and reports: <c>0.5.0 (37558c67)</c>, <c>0.5.0 (37558c67, dirty)</c>
    /// or <c>0.5.0</c>.</summary>
    public static string Display => Format(Version, Commit, Dirty);

    static BuildInfo()
    {
        string informational = typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Parse(informational, out string version, out string commit, out bool dirty);
        Version = version;
        Commit = commit;
        Dirty = dirty;
    }

    /// <summary>
    /// Splits an informational version such as <c>0.5.0+37558c67…</c> or <c>0.5.0+37558c67….dirty</c> into its number,
    /// its short commit and its dirty mark. A missing or empty value parses as version <c>0.0.0</c> with no commit.
    /// </summary>
    public static void Parse(string informational, out string version, out string commit, out bool dirty)
    {
        version = "0.0.0";
        commit = "";
        dirty = false;
        if (string.IsNullOrEmpty(informational))
            return;
        int plus = informational.IndexOf('+');
        if (plus < 0)
        {
            version = informational;
            return;
        }
        version = informational.Substring(0, plus);
        string metadata = informational.Substring(plus + 1);
        const string dirtySuffix = ".dirty";
        if (metadata.EndsWith(dirtySuffix, StringComparison.Ordinal))
        {
            dirty = true;
            metadata = metadata.Substring(0, metadata.Length - dirtySuffix.Length);
        }
        commit = metadata.Length > ShortCommitLength ? metadata.Substring(0, ShortCommitLength) : metadata;
    }

    /// <summary>The <see cref="Display"/> form of the given parts.</summary>
    public static string Format(string version, string commit, bool dirty)
    {
        if (string.IsNullOrEmpty(commit))
            return version;
        return version + " (" + commit + (dirty ? ", dirty" : "") + ")";
    }
}
