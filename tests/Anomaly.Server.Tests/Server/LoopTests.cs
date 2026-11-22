using System.IO;
using Anomaly.Core.Kernel;

namespace Anomaly.Server.Tests.Server;

/// <summary>
/// The server's loop, run in-process (docs/ai/specs/apps.md section 1; these replace the M0 ServerLoopTests and
/// ServerBootTests). Timing facts come from tg and BYOND: <c>world.fps = 20</c> (code/world.dm:19), <c>tick_lag</c> 0.5 ds,
/// so one second of game time is 20 ticks and <c>world.time</c> 10 ds.
/// </summary>
public class LoopTests
{
    private static (RunResult Result, string Output, string Errors, ServerHost Host) RunServer(ServerConfig config, ITickPacer pacer = null, CancellationToken stop = default)
    {
        var output = new StringWriter();
        var errors = new StringWriter();
        using var host = new ServerHost(config, output: output, errors: errors);
        RunResult result = host.Run(pacer ?? FastForwardPacer.Instance, stop);
        return (result, output.ToString(), errors.ToString(), host);
    }

    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();

    private static ServerConfig Empty(long ticks, ulong seed, int smoke = 0) => new()
    {
        Empty = true, Ticks = ticks, Seed = seed, SmokeActors = smoke, Console = false,
    };

    [Fact]
    public void TwoRunsWithTheSameSeedPrintTheSameLinesAndHash()
    {
        var first = RunServer(Empty(400, 7, smoke: 16));
        var second = RunServer(Empty(400, 7, smoke: 16));
        Assert.Equal(first.Result.FinalHash, second.Result.FinalHash);
        Assert.Contains("final tick=400 time=200.0 hash=" + ServerHost.FormatHash(first.Result.FinalHash) + " runtimes=0", first.Output);
        Assert.Equal(StableLines(first.Output), StableLines(second.Output));
        Assert.Equal("", first.Errors);
    }

    // The lines without the timings (status and summary lines carry wall-clock measurements).
    private static string[] StableLines(string output) => Lines(output).Where(l => !l.StartsWith("status ") && !l.StartsWith("summary ")).ToArray();

    [Fact]
    public void SeedsAndTheSmokeLoadChangeTheHash()
    {
        var a = RunServer(Empty(100, 1, smoke: 4));
        var b = RunServer(Empty(100, 2, smoke: 4));
        var none = RunServer(Empty(100, 1));
        Assert.NotEqual(a.Result.FinalHash, b.Result.FinalHash);
        Assert.NotEqual(a.Result.FinalHash, none.Result.FinalHash);
    }

    [Fact]
    public void TheHeaderAndTheFinalLine()
    {
        var run = RunServer(Empty(70, 5));
        string[] lines = Lines(run.Output);
        Assert.Equal("Anomaly.Server: seed=5 ticks=70 fps=20 mode=fast-forward world=empty smoke=0 speed-scale=0.5 dev-commands=on no-network", lines[0]);
        Assert.Contains("shutdown at tick 70", lines);
        Assert.Contains("final tick=70 time=35.0 hash=" + ServerHost.FormatHash(run.Result.FinalHash) + " runtimes=0", lines);
        Assert.Contains(lines, l => l.StartsWith("summary ticks=70 "));
        Assert.Equal(70, run.Result.Tick);
        Assert.False(run.Result.Stopped);
    }

    [Fact]
    public void StatusLinesComeEveryWindowAndTheHashOnlyWhenAsked()
    {
        var config = new ServerConfig { Empty = true, Ticks = 100, Seed = 3, Console = false, StatusEveryTicks = 40, HashEvery = 50 };
        var run = RunServer(config);
        string[] lines = Lines(run.Output);
        Assert.Equal(2, lines.Count(l => l.StartsWith("status tick=")));
        Assert.Contains(lines, l => l.StartsWith("status tick=40 ticks=40 "));
        Assert.Contains(lines, l => l.StartsWith("hash tick=50 time=25.0 hash="));
        Assert.DoesNotContain(lines, l => l.StartsWith("hash tick=100")); // the final line has it

        var quiet = RunServer(Empty(100, 3));
        Assert.DoesNotContain(Lines(quiet.Output), l => l.StartsWith("hash "));
        Assert.DoesNotContain(Lines(quiet.Output), l => l.StartsWith("status ")); // 200 ticks per window by default
    }

    [Fact]
    public void PacingNeverChangesWhatTheWorldComputes()
    {
        var counting = new CountingPacer();
        var paced = RunServer(Empty(120, 11, smoke: 8), counting);
        var fast = RunServer(Empty(120, 11, smoke: 8));
        Assert.Equal(120, counting.Calls);
        Assert.Equal(fast.Result.FinalHash, paced.Result.FinalHash);
    }

    [Fact]
    public void AStopRequestShutsDownInTheNextTick()
    {
        using var stop = new CancellationTokenSource();
        var pacer = new CountingPacer { CancelAtCall = 51, Source = stop }; // cancelled while waiting for tick 51
        var stopped = RunServer(new ServerConfig { Empty = true, Seed = 13, SmokeActors = 5, Console = false }, pacer, stop.Token);
        Assert.True(stopped.Result.Stopped);
        Assert.Equal(51, stopped.Result.Tick); // the shutdown tick
        Assert.Contains("stop requested: stopped at tick 51", stopped.Output);

        var exact = RunServer(Empty(51, 13, smoke: 5));
        Assert.Equal(exact.Result.FinalHash, stopped.Result.FinalHash);
    }

    [Fact]
    public void ZeroTicksRunNothing()
    {
        var run = RunServer(Empty(0, 1));
        Assert.Equal(0, run.Result.Tick);
        Assert.Contains("final tick=0 time=0.0 hash=", run.Output);
    }

    [Fact]
    public void ABootedServerIsDeterministicAndDiffersFromAnEmptyWorld()
    {
        var config = new ServerConfig { Ticks = 60, Seed = 4, RoomWidth = 16, RoomHeight = 16, Console = false };
        var first = RunServer(config);
        var second = RunServer(config);
        var empty = RunServer(Empty(60, 4));
        Assert.StartsWith("Anomaly.Server: seed=4 ticks=60 fps=20 mode=fast-forward world=tg room=16x16 layout=rooms space=3 smoke=0 speed-scale=0.5 dev-commands=on no-network", first.Output);
        Assert.Equal(first.Result.FinalHash, second.Result.FinalHash);
        Assert.Equal(StableLines(first.Output), StableLines(second.Output));
        Assert.NotEqual(empty.Result.FinalHash, first.Result.FinalHash);
        Assert.Contains("joins open at tick", first.Output);
        Assert.Equal(60, first.Result.Tick);
    }

    [Fact]
    public void ARuntimeInTheWorldIsReportedAndTheLoopCarriesOn()
    {
        var output = new StringWriter();
        var errors = new StringWriter();
        using var host = new ServerHost(Empty(20, 1), output: output, errors: errors);
        host.World.Scheduler.After(3, () => throw new InvalidOperationException("boom"));
        RunResult result = host.Run(FastForwardPacer.Instance);
        Assert.Equal(20, result.Tick);
        string[] lines = Lines(errors.ToString()).ToArray();
        Assert.Equal("runtime at tick 3: InvalidOperationException: boom", lines[0]);
        Assert.All(lines.Skip(1), l => Assert.StartsWith("    at ", l)); // the first time a site fails: its stack
        Assert.EndsWith(" runtimes=1", Lines(output.ToString()).Single(l => l.StartsWith("final ")));
    }

    [Fact]
    public void ARealtimePacerKeepsTheTickRateOfTg()
    {
        // 20 ticks per second: the first tick starts at once, each next one a tick_lag later, so 6 ticks take 5 tick lengths
        // (250 ms). Lower bound only: a loaded machine may be slower, never faster.
        var pacer = new RealtimePacer();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 6; i++) pacer.WaitForNextTick(CancellationToken.None);
        Assert.True(clock.ElapsedMilliseconds >= 245, $"6 ticks took {clock.ElapsedMilliseconds} ms, expected at least 250 ms");
        Assert.True(pacer.LastLatenessMs >= 0);
    }

    [Fact]
    public void TheMetricsCsvHasARowPerTick()
    {
        var csv = new StringWriter();
        using (var host = new ServerHost(Empty(30, 2), metricsCsv: csv))
            host.Run(FastForwardPacer.Instance);
        string[] rows = Lines(csv.ToString());
        Assert.Equal(ServerMetrics.CsvHeader, rows[0]);
        Assert.Equal(31, rows.Length);
        Assert.StartsWith("1,", rows[1]);
        Assert.StartsWith("30,", rows[30]);
        Assert.All(rows.Skip(1), r => Assert.Equal(ServerMetrics.CsvHeader.Count(c => c == ','), r.Count(c => c == ',')));
    }

    [Fact]
    public void TheHostNeverForcesACollection()
    {
        // scope risk 2: the loop never calls GC.Collect (the state hash forces full GCs, which is why it is off by default).
        string path = typeof(ServerHost).Assembly.Location;
        using var stream = File.OpenRead(path);
        using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
        var md = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
        foreach (var handle in md.MemberReferences)
        {
            var member = md.GetMemberReference(handle);
            if (member.Parent.Kind != System.Reflection.Metadata.HandleKind.TypeReference) continue;
            var type = md.GetTypeReference((System.Reflection.Metadata.TypeReferenceHandle)member.Parent);
            string name = md.GetString(type.Namespace) + "." + md.GetString(type.Name) + "." + md.GetString(member.Name);
            Assert.NotEqual("System.GC.Collect", name);
        }
    }

    private sealed class CountingPacer : ITickPacer
    {
        public int Calls;
        public int CancelAtCall = -1;
        public CancellationTokenSource Source;

        public void WaitForNextTick(CancellationToken stop)
        {
            Calls++;
            if (Calls == CancelAtCall) Source.Cancel();
        }

        public double LastLatenessMs => 0;
        public long Overruns => 0;
    }
}
