using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The play test of 2026-11-14: the AI upload's custom law board (/obj/item/ai_module/law_core/full/custom) read
/// config/silicon_laws.txt, which TgStrings mounts from the tgstation clone, but the server build shipped only tg's strings/, so it fell
/// back to Asimov with tg's "Invalid custom AI laws" runtime. The build ships the clone's files outside strings/ too, and a host's own
/// silicon_laws.txt in its config folder replaces tg's, as a tg server operator edits it.
/// </summary>
public sealed class TgConfigFilesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "anomaly-tgconfig-" + Guid.NewGuid().ToString("N"));

    public TgConfigFilesTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void AHostsOwnSiliconLawsReplaceTgs()
    {
        File.WriteAllText(Path.Combine(_folder, "silicon_laws.txt"), "# a host's laws\n\nServe the crew.\nKeep the station whole.\n");
        var config = new ServerConfig
        {
            Seed = 5, RoomWidth = 16, RoomHeight = 16, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f,
            ConfigFile = Path.Combine(_folder, "server.txt"),
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, config, strict: false);
        w.Run(20);
        int before = w.World.RuntimeErrorCount;
        using (w.World.Enter())
        {
            var laws = Dm.@new<datum_ai_laws_custom>();
            Assert.Equal(new[] { "Serve the crew.", "Keep the station whole." }, Enumerable.Range(1, laws.inherent.Count).Select(i => Dm.str(laws.inherent[(float)i])));
        }
        Assert.Equal(before, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void TheServerBuildShipsTheTgFilesTheServerMountsBesideItsStrings()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "build-server.sh"));
        Assert.Contains("cp -R \"$tg/strings\"", script, StringComparison.Ordinal);
        foreach (string file in TgStrings.Files.Where(f => !f.StartsWith("strings/", StringComparison.Ordinal)))
            Assert.Contains("\"$tg/" + file + "\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheClientExportShipsTheTgFilesTheEditorsWorldMounts()
    {
        // A Linux player's Anomaly Editor (2026-11-19): the export had no tg strings, so the editor's world refused its own connection
        // with "client/New failed: pick() from an empty list" (an empty name list)
        string script = File.ReadAllText(Path.Combine(RepoRoot(), "godot", "tools", "export-common.sh"));
        Assert.Contains("cp -R \"$tg/strings\"", script, StringComparison.Ordinal);
        Assert.Contains("tgstation.dme", script, StringComparison.Ordinal);
        foreach (string file in TgStrings.Files.Where(f => !f.StartsWith("strings/", StringComparison.Ordinal)))
            Assert.Contains("\"$tg/" + file + "\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExportsOwnTgFolderIsFoundAboveItsAssemblies()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TgStrings.EnvironmentVariable)))
            return; // the variable wins over any folder
        string shipped = Path.Combine(_folder, "tgstation");
        Directory.CreateDirectory(shipped);
        File.WriteAllText(Path.Combine(shipped, "tgstation.dme"), "");
        string data = Path.Combine(_folder, "data_Anomaly.Client_linuxbsd_x86_64");
        Directory.CreateDirectory(data);
        Assert.Equal(Path.GetFullPath(shipped), TgStrings.Locate(data));
    }

    [Fact]
    public void TheServerBuildShipsTheChangelog()
    {
        // the server finds CHANGELOG.md up from its folder (ServerHost.MountChangelog); a deployed one has only what the build copies
        string script = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "build-server.sh"));
        Assert.Contains("cp \"$repo/CHANGELOG.md\" \"$out/CHANGELOG.md\"", script, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AnomalyStation.sln")))
                return dir.FullName;
        }
        throw new DirectoryNotFoundException("no AnomalyStation.sln above " + AppContext.BaseDirectory);
    }
}
