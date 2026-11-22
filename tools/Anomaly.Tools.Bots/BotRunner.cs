using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Tools.Bots;

/// <summary>The settings of a run of many bots (the command line of <see cref="Program"/>, scope 2i).</summary>
public sealed class BotRunnerOptions
{
    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 7777;

    public int Bots { get; init; } = 10;

    /// <summary>The length of the run in minutes, from the first connection; 0 runs until a stop request.</summary>
    public double Minutes { get; init; } = 1;

    /// <summary>What the bots do; <see cref="BotBehaviour.Mixed"/> gives bot <c>i</c> wander, rest, shove or wander by <c>i % 4</c>.</summary>
    public BotBehaviour Behaviour { get; init; } = BotBehaviour.Wander;

    /// <summary>Every bot predicts its own capsule (the headless prediction client).</summary>
    public bool Predict { get; init; }

    /// <summary>The run's seed; bot <c>i</c> gets <see cref="BotRandom.DeriveSeed"/>.</summary>
    public ulong Seed { get; init; } = 1;

    /// <summary>The bots are named <c>{prefix}001</c>, <c>{prefix}002</c>, ...</summary>
    public string NamePrefix { get; init; } = "Bot";

    /// <summary>Where the JSON report goes (null: none).</summary>
    public string ReportPath { get; init; }

    /// <summary>Seconds a bot keeps trying to join after a refusal that can pass (the server full or booting, no connection yet).</summary>
    public double JoinTimeoutSeconds { get; init; } = 60;

    /// <summary>Connect again after an unexpected disconnect.</summary>
    public bool Reconnect { get; init; }

    /// <summary>Bots started per second (the server refuses more than <see cref="Limits.MaxPendingConnectionRequests"/> pending requests); 0 starts all at once.</summary>
    public int RampPerSecond { get; init; } = 25;

    /// <summary>Seconds between progress lines.</summary>
    public int StatusEverySeconds { get; init; } = 10;

    /// <summary>Leave the per-bot rows out of the JSON report.</summary>
    public bool NoPerBot { get; init; }

    /// <summary>M10 slice J: dev commands the first joined bot sends at their second of the run (the lobby's start, the shuttle call), in order.</summary>
    public IReadOnlyList<(double Seconds, string Command)> DevSchedule { get; init; } = Array.Empty<(double, string)>();

    public const int MaxNamePrefixChars = Limits.MaxNameChars - 6;
}

/// <summary>
/// N bots in one process driven by one loop (scope 2i: no thread per bot; <see cref="Step"/> is one 50 ms iteration:
/// start the bots that are due, update every bot). The transports are made by a factory, so the same runner drives LiteNetLib
/// manual-mode clients (<see cref="Program"/>) and loopback clients (the tests).
/// </summary>
public sealed class BotRunner : IDisposable
{
    public const double PeriodMs = 1000.0 / Bot.UpdatesPerSecond;

    private readonly List<Bot> _bots = new();
    private readonly TourCoverage _coverage = new();
    private int _started;
    private int _devSent;
    private bool _shutdown;
    private long _lateIterations;
    private double _maxIterationMs;
    private double _elapsedSeconds;
    private List<string> _statesBeforeShutdown;

    /// <param name="transportFactory">Makes a client transport for bot number <c>index</c>; called for every connection of every bot.</param>
    public BotRunner(BotRunnerOptions options, Func<int, IClientTransport> transportFactory)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        if (transportFactory == null) throw new ArgumentNullException(nameof(transportFactory));

        int retryUpdates = (int)Math.Min(int.MaxValue / 2, Math.Round(options.JoinTimeoutSeconds * Bot.UpdatesPerSecond));
        for (int i = 0; i < options.Bots; i++)
        {
            int index = i;
            var botOptions = new BotOptions
            {
                Name = options.NamePrefix + (index + 1).ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
                Seed = BotRandom.DeriveSeed(options.Seed, index),
                Behaviour = options.Behaviour,
                Coverage = _coverage,
                Index = index,
                Predict = options.Predict,
                ReconnectOnDisconnect = options.Reconnect,
                JoinRetryUpdates = retryUpdates,
            };
            _bots.Add(new Bot(() => transportFactory(index), botOptions));
        }
    }

    public BotRunnerOptions Options { get; }

    public IReadOnlyList<Bot> Bots => _bots;

    /// <summary>Iterations done.</summary>
    public long Iteration { get; private set; }

    /// <summary>Bots whose first connection was started.</summary>
    public int Started => _started;

    /// <summary>Bots in <see cref="BotState.Joined"/> now.</summary>
    public int JoinedCount
    {
        get
        {
            int n = 0;
            foreach (Bot bot in _bots) if (bot.State == BotState.Joined) n++;
            return n;
        }
    }

    /// <summary>One iteration: starts the bots the ramp makes due, then updates every started bot.</summary>
    public void Step()
    {
        if (!_shutdown)
        {
            int due = Options.RampPerSecond <= 0
                ? _bots.Count
                : (int)Math.Min(_bots.Count, (Iteration + 1) * Options.RampPerSecond / Bot.UpdatesPerSecond);
            for (; _started < due; _started++)
            {
                Bot starting = _bots[_started];
                Guard(starting, () => starting.Connect(Options.Host, Options.Port));
            }
        }
        for (int i = 0; i < _started; i++)
        {
            Bot bot = _bots[i];
            Guard(bot, bot.Update);
        }
        Iteration++;
    }

    // A due command waits for a joined bot (the server may still be booting a round).
    private void SendDueDevCommands(double seconds, TextWriter log)
    {
        while (_devSent < Options.DevSchedule.Count && Options.DevSchedule[_devSent].Seconds <= seconds)
        {
            string command = Options.DevSchedule[_devSent].Command;
            Bot sender = _bots.FirstOrDefault(b => b.SendDevCommand(command));
            if (sender == null) return;
            log?.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "t={0:F0}s {1} sent '{2}'", seconds, sender.Options.Name, command));
            _devSent++;
        }
    }

    /// <summary>Runs one call of a bot. A bug in one bot (or a transport that misbehaves) must not end a 30-minute run: it is counted and reported.</summary>
    private static void Guard(Bot bot, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            bot.Stats.Exceptions++;
            bot.Stats.LastException ??= e.ToString();
        }
    }

    /// <summary>
    /// Runs paced at 20 iterations a second until <see cref="BotRunnerOptions.Minutes"/> have passed or
    /// <paramref name="stop"/> is set, prints a progress line every <see cref="BotRunnerOptions.StatusEverySeconds"/>,
    /// then leaves the server and lets the disconnects through. Returns the run's wall time in seconds.
    /// </summary>
    public double Run(CancellationToken stop, TextWriter log)
    {
        var clock = Stopwatch.StartNew();
        double endSeconds = Options.Minutes <= 0 ? double.PositiveInfinity : Options.Minutes * 60.0;
        double nextStatus = Options.StatusEverySeconds;
        double nextMs = PeriodMs;

        while (!stop.IsCancellationRequested && clock.Elapsed.TotalSeconds < endSeconds)
        {
            double beginMs = clock.Elapsed.TotalMilliseconds;
            Step();
            SendDueDevCommands(clock.Elapsed.TotalSeconds, log);
            double nowMs = clock.Elapsed.TotalMilliseconds;
            double workMs = nowMs - beginMs;
            if (workMs > _maxIterationMs) _maxIterationMs = workMs;
            if (workMs > PeriodMs) _lateIterations++;

            if (Options.StatusEverySeconds > 0 && clock.Elapsed.TotalSeconds >= nextStatus)
            {
                log?.WriteLine(StatusLine(clock.Elapsed.TotalSeconds));
                log?.Flush();
                nextStatus += Options.StatusEverySeconds;
            }

            double waitMs = nextMs - clock.Elapsed.TotalMilliseconds;
            if (waitMs > 0)
            {
                // A wait that a stop request ends at once; the loop is a load, not a metric (its lateness is reported).
                stop.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(waitMs));
                nextMs += PeriodMs;
            }
            else
            {
                // Behind schedule: take the next slot from now instead of a burst of catch-up iterations.
                nextMs = clock.Elapsed.TotalMilliseconds + PeriodMs;
            }
        }

        _elapsedSeconds = clock.Elapsed.TotalSeconds;
        Shutdown(maxIterations: 3 * Bot.UpdatesPerSecond);
        return _elapsedSeconds;
    }

    /// <summary>Leaves the server with every bot and iterates until all are gone (or <paramref name="maxIterations"/> passed).</summary>
    public void Shutdown(int maxIterations)
    {
        // The report says who was in the game when the run ended, not who had left by then.
        _statesBeforeShutdown ??= _bots.ConvertAll(bot => bot.State.ToString());
        _shutdown = true;
        foreach (Bot bot in _bots) bot.Disconnect();
        for (int i = 0; i < maxIterations && !AllGone(); i++)
        {
            Step();
        }
    }

    private bool AllGone()
    {
        foreach (Bot bot in _bots)
        {
            if (bot.State == BotState.Connecting || bot.State == BotState.Joined) return false;
        }
        return true;
    }

    /// <summary>The progress line of the run so far.</summary>
    public string StatusLine(double seconds)
    {
        long inputs = 0, actions = 0, shoves = 0, rests = 0, corrections = 0, own = 0, others = 0, desyncs = 0, disconnects = 0;
        foreach (Bot bot in _bots)
        {
            inputs += bot.Stats.InputsSent;
            actions += bot.Stats.ActionsSent;
            shoves += bot.Stats.ShovesSent;
            rests += bot.Stats.RestsStarted;
            corrections += bot.Stats.Corrections;
            own += bot.Stats.OwnMoves;
            others += bot.Stats.OtherMoves;
            desyncs += bot.Stats.Desyncs;
            disconnects += bot.Stats.DisconnectsTotal;
        }
        string line = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "t={0:F0}s started={1}/{2} joined={3} inputs={4} actions={5} shoves={6} rests={7} ownMoves={8} otherMoves={9} desyncs={10} disconnects={11} lateIterations={12}",
            seconds, _started, _bots.Count, JoinedCount, inputs, actions, shoves, rests, own, others, desyncs, disconnects, _lateIterations);
        return Options.Predict ? line + " corrections=" + corrections.ToString(System.Globalization.CultureInfo.InvariantCulture) : line;
    }

    /// <summary>The report of the run so far (call after <see cref="Run"/> for the final one).</summary>
    public BotReport BuildReport()
    {
        var stats = new List<BotStats>(_bots.Count);
        foreach (Bot bot in _bots) stats.Add(bot.Stats);
        List<string> states = _statesBeforeShutdown ?? _bots.ConvertAll(bot => bot.State.ToString());

        double seconds = _elapsedSeconds > 0 ? _elapsedSeconds : Iteration * (PeriodMs / 1000.0);
        double secondsPerUpdate = Iteration > 0 && seconds > 0 ? seconds / Iteration : PeriodMs / 1000.0;
        BotReport report = BotReport.Build(stats, states, secondsPerUpdate);
        report.Behaviour = BotBehaviourNames.ToName(Options.Behaviour);
        if (Options.Behaviour == BotBehaviour.Tour)
        {
            report.Tour = new TourReport
            {
                ReachableTiles = _coverage.Reachable,
                CoveredTiles = _coverage.Covered,
                CoverageShare = _coverage.Share,
                DoorsPassed = _coverage.DoorsPassed,
                DoorPressTicks = stats.Sum(s => s.TourDoorPressTicks),
                Replans = stats.Sum(s => s.TourReplans),
                StuckBots = stats.Count(s => s.TourStuckEvents > 0),
                StuckNow = stats.Count(s => s.TourStuck),
            };
        }
        report.Host = Options.Host;
        report.Port = Options.Port;
        report.Seed = Options.Seed;
        report.Predict = Options.Predict;
        report.RunSeconds = seconds;
        report.Iterations = Iteration;
        report.LateIterations = _lateIterations;
        report.MaxIterationMs = _maxIterationMs;
        return report;
    }

    public void Dispose()
    {
        foreach (Bot bot in _bots) bot.Dispose();
    }
}
