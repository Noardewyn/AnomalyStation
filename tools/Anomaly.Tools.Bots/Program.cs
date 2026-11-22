using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Anomaly.Net;

namespace Anomaly.Tools.Bots;

// Owner: M1 P5. The command line (scope 2i: Anomaly.Tools.Bots --bots 50 --minutes 30 --behaviour wander, plus the server's
// host and port, a seed, a name prefix and a report file) is BotArgs; the loop is BotRunner.

/// <summary>Entry point of the bot runner: N bots in one process over LiteNetLib's manual mode, one loop at 20 Hz, no thread per bot.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
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
            return Run(args, Console.Out, Console.Error, stop.Token);
        }
        finally
        {
            Console.CancelKeyPress -= onCancelKey;
        }
    }

    /// <summary>
    /// The whole program without the process around it: parses <paramref name="args"/>, runs the bots until their time is up
    /// or <paramref name="stop"/> is set, prints the summary and writes the report. Returns the exit status: 0 after a run, 1
    /// when the run itself failed, 2 for a bad command line, 3 when a run that should have got bots in got none.
    /// </summary>
    public static int Run(string[] args, TextWriter output, TextWriter error, CancellationToken stop)
    {
        BotRunnerOptions options = BotArgs.Parse(args, out string problem);
        if (options == null)
        {
            if (problem == null)
            {
                output.WriteLine(BotArgs.Usage);
                return 0;
            }
            error.WriteLine("error: " + problem);
            error.WriteLine(BotArgs.Usage);
            return 2;
        }

        output.WriteLine(
            "Anomaly.Tools.Bots: {0} bots, behaviour {1}{6}, {2}:{3}, {4}, seed {5}",
            options.Bots,
            BotBehaviourNames.ToName(options.Behaviour),
            options.Host,
            options.Port,
            options.Minutes <= 0 ? "until stopped" : options.Minutes.ToString(System.Globalization.CultureInfo.InvariantCulture) + " min",
            options.Seed,
            options.Predict ? " (predicting)" : string.Empty);

        using var runner = new BotRunner(options, _ => new LiteNetClientTransport(manualMode: true));
        int status = 0;
        try
        {
            runner.Run(stop, output);
        }
        catch (Exception e)
        {
            // The report of what happened up to here is still worth having.
            error.WriteLine("error: the run failed: " + e);
            status = 1;
        }
        BotReport report = runner.BuildReport();
        output.WriteLine(Summary(report));

        if (options.ReportPath != null)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(options.ReportPath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(options.ReportPath, report.ToJson(includePerBot: !options.NoPerBot));
            output.WriteLine("report: " + options.ReportPath);
        }

        // A run against a server that never let a bot in is a failure of the run, not of the server under test; abusive bots
        // are expected to be refused.
        bool expectsJoin = !BotBehaviourNames.IsAbuse(options.Behaviour);
        if (status != 0) return status;
        return expectsJoin && report.Aggregate.BotsEverJoined == 0 ? 3 : 0;
    }

    /// <summary>The summary printed at the end of a run (the same numbers as the JSON report).</summary>
    public static string Summary(BotReport report)
    {
        BotAggregate a = report.Aggregate;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var text = new System.Text.StringBuilder();
        text.AppendLine(string.Format(inv, "bots: {0} (joined ever {1}, joined at end {2}), run {3:F1} s, {4} iterations ({5} late, max {6:F1} ms)",
            a.Bots, a.BotsEverJoined, a.BotsJoinedAtEnd, report.RunSeconds, report.Iterations, report.LateIterations, report.MaxIterationMs));
        text.AppendLine(string.Format(inv, "connected: {0:F0} s in all, {1:F1} s per bot; own moves {2}; other moves {3}",
            a.ConnectedSecondsTotal, a.ConnectedSecondsMean, a.OwnMoves, a.OtherMoves));
        text.AppendLine(string.Format(inv, "sent: {0} input messages ({1} frames), {2} actions, {3} shoves, {4} rests, {5} lobby presses; {6} round restarts ridden",
            a.InputsSent, a.FramesSent, a.ActionsSent, a.Shoves, a.Rests, a.LobbyActs, a.RoundRestarts));
        if (a.ActionsByKind.Count > 0) text.AppendLine("  actions: " + string.Join(", ", a.ActionsByKind.Select(k => k.Key + " " + k.Value)));
        if (a.Behaviours.Count > 1) text.AppendLine("  behaviours: " + string.Join(", ", a.Behaviours.Select(b => b.Key + " " + b.Value)));
        if (a.PredictingBots > 0)
        {
            text.AppendLine(string.Format(inv, "prediction ({0} bots): {1} reconciliations, {2} corrections, {3} snaps, {4} mismatches, max error {5:F3} tiles, {6} overflows",
                a.PredictingBots, a.Reconciliations, a.Corrections, a.Snaps, a.Mismatches, a.MaxErrorTiles, a.Overflows));
        }
        text.AppendLine(string.Format(inv, "bytes: in {0}, out {1}; per bot per second: in {2:F0}, out {3:F0}",
            a.BytesIn, a.BytesOut, a.BytesInPerBotPerSecond, a.BytesOutPerBotPerSecond));
        text.AppendLine(string.Format(inv, "desyncs: {0}; decode errors: {1}; disconnects: {2}", a.Desyncs, a.DecodeErrors, a.DisconnectsTotal));
        foreach (KeyValuePair<string, long> d in a.Disconnects) text.AppendLine("  " + d.Key + ": " + d.Value);
        text.Append(string.Format(inv, "rtt ms: mean {0:F1}, p50 {1}, p95 {2}, p99 {3}, max {4} ({5} samples)",
            a.RttMeanMs, a.RttP50Ms, a.RttP95Ms, a.RttP99Ms, a.RttMaxMs, a.RttSamples));
        return text.ToString();
    }
}
