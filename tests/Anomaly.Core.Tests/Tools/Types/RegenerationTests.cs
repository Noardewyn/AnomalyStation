using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Anomaly.Core.Tests.Tools.Types;

/// <summary>
/// The checked-in outputs of the types converter equal a fresh run (docs/ai/specs/types-converter.md 2): runs the tool's
/// <c>--check</c> when both clones exist (it compiles tg with OpenDream, about 50 s), skipped otherwise. Each run gets its
/// own scratch folder (removed afterwards): two suites running in parallel on the tool's shared default scratch once hung
/// (M3 R4), so a run never reuses another run's cached model.
/// </summary>
public class RegenerationTests
{
    [TypesToolFact]
    public void ARunOfTheToolIsByteEqualToTheCheckedInOutputs()
    {
        string repo = TgClone.RepositoryRoot;
        string scratch = Path.Combine(Path.GetTempPath(), "anomaly-types-check-" + Guid.NewGuid().ToString("N"));
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = repo,
        };
        foreach (string a in new[]
        {
            "run", "-c", "Release", "--property:UseSharedCompilation=false", "--project", Path.Combine(repo, "tools", "Anomaly.Tools.Types"), "--",
            "--tg", TgClone.Root, "--opendream", OpenDreamClone.Root, "--out", repo, "--check",
            "--scratch", scratch,
        })
            psi.ArgumentList.Add(a);
        psi.Environment["ANOMALY_OPENDREAM"] = OpenDreamClone.Root;
        // No build process may outlive the run: a reused MSBuild node or the compiler server that `dotnet run` starts inherits
        // the redirected output pipes, so reading them to the end waited until that process idled out (10-15 minutes at no CPU,
        // which looked like a hung test).
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        psi.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        string output;
        int exitCode;
        try
        {
            using Process p = Process.Start(psi);
            Task<string> stdout = p.StandardOutput.ReadToEndAsync();
            Task<string> stderr = p.StandardError.ReadToEndAsync();
            bool exited = p.WaitForExit(TimeSpan.FromMinutes(14));
            if (!exited)
                p.Kill(entireProcessTree: true);
            Assert.True(exited, "the types converter ran longer than 14 minutes");
            output = stdout.Result + stderr.Result;
            exitCode = p.ExitCode;
        }
        finally
        {
            try { if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        Assert.True(exitCode == 0, "the checked-in outputs differ from a fresh run of tools/Anomaly.Tools.Types:\n" + output);
        Assert.Contains("check: all", output);
    }
}
