using System.IO;
using System.Text.RegularExpressions;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>The build's version (docs/versioning.md): the informational version's parts and the stamp from Version.props.</summary>
public class BuildInfoTests
{
    [Theory]
    [InlineData("0.5.0+37558c67fe6d1e498b414b5d41ef8205c7cc435c", "0.5.0", "37558c67", false, "0.5.0 (37558c67)")]
    [InlineData("0.5.0+37558c67fe6d1e498b414b5d41ef8205c7cc435c.dirty", "0.5.0", "37558c67", true, "0.5.0 (37558c67, dirty)")]
    [InlineData("0.6.2+abc", "0.6.2", "abc", false, "0.6.2 (abc)")]
    [InlineData("1.0.0-pre-alpha+37558c67fe6d.dirty", "1.0.0-pre-alpha", "37558c67", true, "1.0.0-pre-alpha (37558c67, dirty)")]
    [InlineData("0.5.0", "0.5.0", "", false, "0.5.0")]
    [InlineData("", "0.0.0", "", false, "0.0.0")]
    [InlineData(null, "0.0.0", "", false, "0.0.0")]
    public void ParsesTheInformationalVersion(string informational, string version, string commit, bool dirty, string display)
    {
        BuildInfo.Parse(informational, out string v, out string c, out bool d);
        Assert.Equal(version, v);
        Assert.Equal(commit, c);
        Assert.Equal(dirty, d);
        Assert.Equal(display, BuildInfo.Format(v, c, d));
    }

    [Fact]
    public void TheBuildCarriesTheVersionOfVersionProps()
    {
        string props = File.ReadAllText(Path.Combine(RepoRoot(), "Version.props"));
        string expected = Regex.Match(props, @"<Version>([^<]+)</Version>").Groups[1].Value;
        Assert.Equal(expected, BuildInfo.Version);
        Assert.Matches(@"^\d+\.\d+\.\d+(-(pre-alpha|alpha|beta))?$", BuildInfo.Version); // docs/versioning.md, D280
    }

    /// <summary>A protocol change raises the game's version (docs/versioning.md): two builds of one version always talk.
    /// Fails until tools/release.sh writes the new protocol into Version.props with a new version.</summary>
    [Fact]
    public void TheVersionNamesTheProtocolItSpeaks()
    {
        string props = File.ReadAllText(Path.Combine(RepoRoot(), "Version.props"));
        string pinned = Regex.Match(props, @"<AnomalyProtocolVersion>([^<]+)</AnomalyProtocolVersion>").Groups[1].Value;
        Assert.True(pinned == ProtocolVersion.Current.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"the protocol is version {ProtocolVersion.Current} but Version.props pins {pinned} to version {BuildInfo.Version}: "
            + "raise the version with tools/release.sh");
    }

    // From this source file up, so the test finds the repository wherever its binaries were built.
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source));
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Version.props")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir.FullName;
    }
}
