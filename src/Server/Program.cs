using System.IO;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;

namespace Anomaly.Server;

/// <summary>
/// Entry point of the headless server (docs/ai/specs/apps.md section 1): <c>dotnet run --project src/Server -- [options]</c>.
/// Reads <c>config/server.txt</c> and the command line (<see cref="ServerConfig"/>), boots the tg world in the test room,
/// listens on LiteNetLib, reads console commands from standard input, and runs until <c>--ticks</c>, Ctrl+C, SIGTERM or
/// the console's <c>quit</c>, each of which shuts down cleanly (exit 0). A second Ctrl+C kills the process. Exit 2 for a
/// bad command line or config, 1 for a replay whose final hash differs from the recorded one or a failure to start.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        ServerConfig config = ServerConfig.Parse(args, ReadFileOrNull, out string error, () => (ulong)Random.Shared.NextInt64());
        if (config == null)
        {
            if (error == null)
            {
                Console.Out.WriteLine(ServerConfig.Usage);
                return 0;
            }
            Console.Error.WriteLine("error: " + error);
            Console.Error.WriteLine(ServerConfig.Usage);
            return 2;
        }
        foreach (string warning in config.Warnings)
            Console.Error.WriteLine("config: " + warning);
        // The build a log or a play-test report came from (docs/versioning.md).
        Console.Out.WriteLine("Anomaly Station server " + BuildInfo.Display + ", protocol " + ProtocolVersion.Current);

        CommandLog replay = null;
        if (config.ReplayPath != null)
        {
            string text = ReadFileOrNull(config.ReplayPath);
            replay = text == null ? null : CommandLog.Parse(text, out error);
            if (replay == null)
            {
                Console.Error.WriteLine("error: --replay " + config.ReplayPath + ": " + (error ?? "file not found"));
                return 2;
            }
            config = replay.ReplayConfig(config);
        }

        // scope risk 2: fewer blocking gen2 collections in a paced run; the loop never calls GC.Collect.
        if (config.Realtime)
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancelKey = (_, e) =>
        {
            if (stop.IsCancellationRequested) return; // the second Ctrl+C terminates the process
            e.Cancel = true;
            stop.Cancel();
        };
        Console.CancelKeyPress += onCancelKey;
        using var onSigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            stop.Cancel();
        });

        try
        {
            TextWriter metrics = config.MetricsFile != null ? new StreamWriter(config.MetricsFile) : null;
            TextWriter record = config.RecordPath != null ? new StreamWriter(config.RecordPath) : null;
            IServerTransport transport = replay == null ? new LiteNetServerTransport() : null;
            // M1 P4's replicator (R3, D99); a replay and the empty world replicate nothing (NoReplicator). M10 slice C: with interest management.
            IReplicator replicator = replay == null && !config.Empty
                ? new Replicator(new ReplicatorOptions { Interest = config.NoInterest ? null : new InterestOptions() })
                : null;
            using var host = new ServerHost(config, transport, replicator, output: Console.Out, errors: Console.Error,
                replay: replay, metricsCsv: metrics, recordTo: record);
            host.Start();
            if (config.Console && replay == null)
                ConsoleInput.Start(host, Console.In);

            ITickPacer pacer = config.Realtime ? new RealtimePacer() : FastForwardPacer.Instance;
            RunResult result = host.Run(pacer, stop.Token);

            if (replay?.FinalHash is ulong recorded)
            {
                if (recorded != result.FinalHash)
                {
                    Console.Out.WriteLine("replay: MISMATCH: recorded " + ServerHost.FormatHash(recorded) + ", replayed " + ServerHost.FormatHash(result.FinalHash));
                    return 1;
                }
                Console.Out.WriteLine("replay: the final hash equals the recorded one");
            }
            return 0;
        }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= onCancelKey;
        }
    }

    private static string ReadFileOrNull(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
}

/// <summary>The console's reader thread: it only hands lines to the host (scope 2e), which parses them between ticks.</summary>
public static class ConsoleInput
{
    /// <summary>Starts a background thread that reads <paramref name="input"/> line by line until it ends.</summary>
    public static Thread Start(ServerHost host, TextReader input)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(input);
        var thread = new Thread(() =>
        {
            try
            {
                string line;
                while ((line = input.ReadLine()) != null)
                    host.EnqueueConsoleLine(line);
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        })
        {
            IsBackground = true,
            Name = "Anomaly.Server console",
        };
        thread.Start();
        return thread;
    }
}

/// <summary>A replicator that replicates nothing: an empty world or a replay (no network).</summary>
public sealed class NoReplicator : IReplicator
{
    public void Attach(Anomaly.Core.Kernel.World world)
    {
    }

    public DeltaFrame BuildDelta(long tick)
    {
        LastStats = new ReplicationStats(tick, 0, 0, 0);
        return new DeltaFrame(tick, ReadOnlyMemory<byte>.Empty, 0, 0, 0);
    }

    public JoinMessages BuildJoin(long tick, long ownEntity, uint lastInputSeq) => new(Array.Empty<byte>(), Array.Empty<byte>());

    public JoinMessages BuildJoin(long tick, in PrivateInputs inputs) => new(Array.Empty<byte>(), Array.Empty<byte>());

    public PrivateFrame BuildPrivate(long tick, in PrivateInputs inputs) => new(ReadOnlyMemory<byte>.Empty, 0, 0);

    public void Forget(int session)
    {
    }

    public ReplicationStats LastStats { get; private set; }
}
