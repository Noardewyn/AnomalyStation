using System.IO;
using System.Text;
using System.Text.Json;
using Anomaly.Protocol;

namespace Anomaly.Tools.Bots;

/// <summary>The sums over every bot of a run (scope 2i, P5: the aggregate report).</summary>
public sealed class BotAggregate
{
    public int Bots { get; init; }
    public int BotsEverJoined { get; init; }
    /// <summary>Bots in <see cref="BotState.Joined"/> when the run ended, before they left on purpose.</summary>
    public int BotsJoinedAtEnd { get; init; }
    public long Joins { get; init; }
    public double ConnectedSecondsTotal { get; init; }
    public double ConnectedSecondsMean { get; init; }
    /// <summary>MotionInput messages sent.</summary>
    public long InputsSent { get; init; }
    /// <summary>Input frames sent (the newest of each message).</summary>
    public long FramesSent { get; init; }
    /// <summary>Action messages sent.</summary>
    public long ActionsSent { get; init; }
    /// <summary>Action messages by kind (<see cref="BotReport.ActionName"/>), the kinds that were sent.</summary>
    public SortedDictionary<string, long> ActionsByKind { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Right clicks on another body.</summary>
    public long Shoves { get; init; }
    /// <summary>M10 slice J: presses in the lobby and the late join menu, and round restarts ridden.</summary>
    public long LobbyActs { get; init; }
    public long RoundRestarts { get; init; }
    /// <summary>Times a bot lay down on purpose.</summary>
    public long Rests { get; init; }
    /// <summary>Bots by the behaviour they actually ran (a mixed run names wander, rest and shove).</summary>
    public SortedDictionary<string, int> Behaviours { get; init; } = new(StringComparer.Ordinal);
    public long AbuseSent { get; init; }
    public long DeltasApplied { get; init; }
    public long OwnMoves { get; init; }
    public long OtherMoves { get; init; }
    public long SpawnsSeen { get; init; }
    public long DespawnsSeen { get; init; }
    public long ServerTexts { get; init; }
    public long BytesIn { get; init; }
    public long BytesOut { get; init; }

    /// <summary>Payload bytes received per connected bot per second (the "bytes per player" the soak reports).</summary>
    public double BytesInPerBotPerSecond { get; init; }

    public double BytesOutPerBotPerSecond { get; init; }
    public long Desyncs { get; init; }
    public long DecodeErrors { get; init; }
    public long Exceptions { get; init; }
    public string FirstException { get; init; }
    public long DisconnectsTotal { get; init; }

    /// <summary>Disconnects by <c>Kind:Reason</c>, summed over the bots (sorted by key).</summary>
    public SortedDictionary<string, long> Disconnects { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Bots that predicted their own capsule.</summary>
    public int PredictingBots { get; init; }
    public long Reconciliations { get; init; }
    public long Corrections { get; init; }
    public long Snaps { get; init; }
    public long Mismatches { get; init; }
    public double MaxErrorTiles { get; init; }
    public long Overflows { get; init; }

    public long RttSamples { get; init; }
    public double RttMeanMs { get; init; }
    public int RttP50Ms { get; init; }
    public int RttP95Ms { get; init; }
    public int RttP99Ms { get; init; }
    public int RttMaxMs { get; init; }
}

/// <summary>One bot's row of the report.</summary>
public sealed class BotRow
{
    public string Name { get; init; }
    public string State { get; init; }
    /// <summary>The behaviour the bot actually ran.</summary>
    public string Behaviour { get; init; }
    public long Joins { get; init; }
    public double ConnectedSeconds { get; init; }
    public long InputsSent { get; init; }
    public long FramesSent { get; init; }
    public long ActionsSent { get; init; }
    public long Shoves { get; init; }
    public long Rests { get; init; }
    public bool Predicted { get; init; }
    public long Corrections { get; init; }
    public long Snaps { get; init; }
    public long Mismatches { get; init; }
    public double MaxErrorTiles { get; init; }
    public long OwnMoves { get; init; }
    public long OtherMoves { get; init; }
    public long BytesIn { get; init; }
    public long BytesOut { get; init; }
    public long Desyncs { get; init; }
    public long DisconnectsTotal { get; init; }
    public string LastDisconnect { get; init; }
    public double RttMeanMs { get; init; }
    public int RttMaxMs { get; init; }
}

/// <summary>
/// The per-bot and aggregate report of a run (scope 2i): the math over <see cref="BotStats"/> and the JSON the soak script
/// reads. <see cref="Build"/> is pure, so the numbers are tested without a network.
/// </summary>
public sealed class BotReport
{
    public string Behaviour { get; set; }

    /// <summary>M3 P5. The tour's coverage (null for the other behaviours).</summary>
    public TourReport Tour { get; set; }

    /// <summary>True when the bots predicted their own capsule (<c>--predict</c>).</summary>
    public bool Predict { get; set; }

    public string Host { get; set; }
    public int Port { get; set; }
    public ulong Seed { get; set; }

    /// <summary>The length of the run in seconds (wall clock in the runner).</summary>
    public double RunSeconds { get; set; }

    /// <summary>Seconds one <see cref="Bot.Update"/> stands for (0.05 at 20 Hz).</summary>
    public double SecondsPerUpdate { get; set; }

    /// <summary>Runner iterations that took longer than their 50 ms (the bots' own lateness; it limits how far the load can be trusted).</summary>
    public long LateIterations { get; set; }

    public double MaxIterationMs { get; set; }
    public long Iterations { get; set; }
    public BotAggregate Aggregate { get; init; }
    public List<BotRow> PerBot { get; init; } = new();

    /// <summary>The report's name of an action kind: <c>rest</c>, <c>toggle_move_intent</c>, <c>toggle_combat_mode</c>, <c>click</c>, <c>dev_command</c>, <c>keybinding</c>, <c>hover</c>.</summary>
    public static string ActionName(int kind)
    {
        switch ((ActionKind)kind)
        {
            case ActionKind.Rest: return "rest";
            case ActionKind.ToggleMoveIntent: return "toggle_move_intent";
            case ActionKind.ToggleCombatMode: return "toggle_combat_mode";
            case ActionKind.Click: return "click";
            case ActionKind.DevCommand: return "dev_command";
            case ActionKind.Keybinding: return "keybinding";
            case ActionKind.Hover: return "hover";
            default: return "kind" + kind;
        }
    }

    /// <param name="secondsPerUpdate">Seconds one update stands for: 0.05 for a 20 Hz loop, or the measured average.</param>
    public static BotReport Build(IReadOnlyList<BotStats> stats, IReadOnlyList<string> finalStates, double secondsPerUpdate)
    {
        if (stats == null) throw new ArgumentNullException(nameof(stats));
        if (secondsPerUpdate <= 0) throw new ArgumentOutOfRangeException(nameof(secondsPerUpdate), secondsPerUpdate, "must be positive");

        int everJoined = 0;
        int joinedAtEnd = 0;
        long joins = 0, connectedUpdates = 0, inputs = 0, frames = 0, actions = 0, shoves = 0, lobbyActs = 0, roundRestarts = 0, rests = 0, abuse = 0, deltas = 0, own = 0, others = 0, spawns = 0, despawns = 0;
        long reconciliations = 0, corrections = 0, snaps = 0, mismatches = 0, overflows = 0;
        double maxError = 0;
        int predicting = 0;
        var actionKinds = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var behaviours = new SortedDictionary<string, int>(StringComparer.Ordinal);
        long texts = 0, bytesIn = 0, bytesOut = 0, desyncs = 0, decodeErrors = 0, exceptions = 0, disconnects = 0, rttSamples = 0, rttSum = 0;
        string firstException = null;
        int rttMax = 0;
        var histogram = new long[BotStats.RttBuckets];
        var reasons = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var rows = new List<BotRow>(stats.Count);

        for (int i = 0; i < stats.Count; i++)
        {
            BotStats s = stats[i];
            string state = finalStates != null && i < finalStates.Count ? finalStates[i] : null;
            if (s.Joins > 0) everJoined++;
            if (state == nameof(BotState.Joined)) joinedAtEnd++;
            joins += s.Joins;
            connectedUpdates += s.ConnectedUpdates;
            inputs += s.InputsSent;
            frames += s.FramesSent;
            actions += s.ActionsSent;
            shoves += s.ShovesSent;
            lobbyActs += s.LobbyActs;
            roundRestarts += s.RoundRestarts;
            rests += s.RestsStarted;
            if (s.Predicted) predicting++;
            reconciliations += s.Reconciliations;
            corrections += s.Corrections;
            snaps += s.Snaps;
            mismatches += s.Mismatches;
            overflows += s.Overflows;
            if (s.MaxErrorTiles > maxError) maxError = s.MaxErrorTiles;
            for (int k = 0; k < s.ActionsByKind.Length; k++)
            {
                if (s.ActionsByKind[k] == 0) continue;
                string kind = ActionName(k);
                actionKinds.TryGetValue(kind, out long sent);
                actionKinds[kind] = sent + s.ActionsByKind[k];
            }
            if (s.Behaviour != null)
            {
                behaviours.TryGetValue(s.Behaviour, out int bots);
                behaviours[s.Behaviour] = bots + 1;
            }
            abuse += s.AbuseSent;
            deltas += s.DeltasApplied;
            own += s.OwnMoves;
            others += s.OtherMoves;
            spawns += s.SpawnsSeen;
            despawns += s.DespawnsSeen;
            texts += s.ServerTexts;
            bytesIn += s.BytesIn;
            bytesOut += s.BytesOut;
            desyncs += s.Desyncs;
            decodeErrors += s.DecodeErrors;
            exceptions += s.Exceptions;
            firstException ??= s.LastException;
            disconnects += s.DisconnectsTotal;
            rttSamples += s.RttSamples;
            rttSum += s.RttSumMs;
            if (s.RttMaxMs > rttMax) rttMax = s.RttMaxMs;
            for (int b = 0; b < histogram.Length; b++) histogram[b] += s.RttHistogram[b];
            foreach (KeyValuePair<string, long> d in s.Disconnects)
            {
                reasons.TryGetValue(d.Key, out long n);
                reasons[d.Key] = n + d.Value;
            }

            rows.Add(new BotRow
            {
                Name = s.Name,
                State = state,
                Behaviour = s.Behaviour,
                Joins = s.Joins,
                ConnectedSeconds = s.ConnectedUpdates * secondsPerUpdate,
                InputsSent = s.InputsSent,
                FramesSent = s.FramesSent,
                ActionsSent = s.ActionsSent,
                Shoves = s.ShovesSent,
                Rests = s.RestsStarted,
                Predicted = s.Predicted,
                Corrections = s.Corrections,
                Snaps = s.Snaps,
                Mismatches = s.Mismatches,
                MaxErrorTiles = s.MaxErrorTiles,
                OwnMoves = s.OwnMoves,
                OtherMoves = s.OtherMoves,
                BytesIn = s.BytesIn,
                BytesOut = s.BytesOut,
                Desyncs = s.Desyncs,
                DisconnectsTotal = s.DisconnectsTotal,
                RttMeanMs = s.RttSamples == 0 ? 0 : (double)s.RttSumMs / s.RttSamples,
                RttMaxMs = s.RttMaxMs,
                LastDisconnect = s.LastDisconnect,
            });
        }

        double connectedSeconds = connectedUpdates * secondsPerUpdate;
        var aggregate = new BotAggregate
        {
            Bots = stats.Count,
            BotsEverJoined = everJoined,
            BotsJoinedAtEnd = joinedAtEnd,
            Joins = joins,
            ConnectedSecondsTotal = connectedSeconds,
            ConnectedSecondsMean = stats.Count == 0 ? 0 : connectedSeconds / stats.Count,
            InputsSent = inputs,
            FramesSent = frames,
            ActionsSent = actions,
            ActionsByKind = actionKinds,
            Shoves = shoves,
            LobbyActs = lobbyActs,
            RoundRestarts = roundRestarts,
            Rests = rests,
            Behaviours = behaviours,
            AbuseSent = abuse,
            DeltasApplied = deltas,
            OwnMoves = own,
            OtherMoves = others,
            SpawnsSeen = spawns,
            DespawnsSeen = despawns,
            ServerTexts = texts,
            BytesIn = bytesIn,
            BytesOut = bytesOut,
            BytesInPerBotPerSecond = connectedSeconds <= 0 ? 0 : bytesIn / connectedSeconds,
            BytesOutPerBotPerSecond = connectedSeconds <= 0 ? 0 : bytesOut / connectedSeconds,
            Desyncs = desyncs,
            DecodeErrors = decodeErrors,
            Exceptions = exceptions,
            FirstException = firstException,
            DisconnectsTotal = disconnects,
            Disconnects = reasons,
            PredictingBots = predicting,
            Reconciliations = reconciliations,
            Corrections = corrections,
            Snaps = snaps,
            Mismatches = mismatches,
            MaxErrorTiles = maxError,
            Overflows = overflows,
            RttSamples = rttSamples,
            RttMeanMs = rttSamples == 0 ? 0 : (double)rttSum / rttSamples,
            RttP50Ms = Percentile(histogram, rttSamples, 0.50, rttMax),
            RttP95Ms = Percentile(histogram, rttSamples, 0.95, rttMax),
            RttP99Ms = Percentile(histogram, rttSamples, 0.99, rttMax),
            RttMaxMs = rttMax,
        };

        return new BotReport { Aggregate = aggregate, PerBot = rows, SecondsPerUpdate = secondsPerUpdate };
    }

    /// <summary>
    /// The smallest bucket (1 ms each) whose cumulative count reaches <paramref name="p"/> of <paramref name="total"/>
    /// (nearest rank); the overflow bucket answers with <paramref name="max"/>. 0 for no samples.
    /// </summary>
    public static int Percentile(long[] histogram, long total, double p, int max)
    {
        if (total <= 0) return 0;
        long rank = Math.Max(1, (long)Math.Ceiling(p * total));
        long cumulative = 0;
        for (int b = 0; b < histogram.Length; b++)
        {
            cumulative += histogram[b];
            if (cumulative >= rank) return b == histogram.Length - 1 ? Math.Max(b, max) : b;
        }
        return max;
    }

    /// <summary>Writes the report as indented JSON (snake_case keys, as the other M1 result files).</summary>
    public string ToJson(bool includePerBot = true)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteString("tool", "Anomaly.Tools.Bots");
            w.WriteString("behaviour", Behaviour);
            w.WriteBoolean("predict", Predict);
            if (Tour != null)
            {
                w.WriteStartObject("tour");
                w.WriteNumber("reachable_tiles", Tour.ReachableTiles);
                w.WriteNumber("covered_tiles", Tour.CoveredTiles);
                w.WriteNumber("coverage_share", Math.Round(Tour.CoverageShare, 4));
                w.WriteNumber("doors_passed", Tour.DoorsPassed);
                w.WriteNumber("door_press_ticks", Tour.DoorPressTicks);
                w.WriteNumber("replans", Tour.Replans);
                w.WriteNumber("stuck_bots", Tour.StuckBots);
                w.WriteNumber("stuck_now", Tour.StuckNow);
                w.WriteEndObject();
            }

            w.WriteString("host", Host);
            w.WriteNumber("port", Port);
            w.WriteNumber("seed", Seed);
            w.WriteNumber("run_seconds", Math.Round(RunSeconds, 3));
            w.WriteNumber("seconds_per_update", Math.Round(SecondsPerUpdate, 6));
            w.WriteNumber("iterations", Iterations);
            w.WriteNumber("late_iterations", LateIterations);
            w.WriteNumber("max_iteration_ms", Math.Round(MaxIterationMs, 3));

            BotAggregate a = Aggregate;
            w.WriteStartObject("aggregate");
            w.WriteNumber("bots", a.Bots);
            w.WriteNumber("bots_ever_joined", a.BotsEverJoined);
            w.WriteNumber("bots_joined_at_end", a.BotsJoinedAtEnd);
            w.WriteNumber("joins", a.Joins);
            w.WriteNumber("connected_seconds_total", Math.Round(a.ConnectedSecondsTotal, 3));
            w.WriteNumber("connected_seconds_mean", Math.Round(a.ConnectedSecondsMean, 3));
            w.WriteNumber("inputs_sent", a.InputsSent);
            w.WriteNumber("frames_sent", a.FramesSent);
            w.WriteNumber("actions_sent", a.ActionsSent);
            w.WriteStartObject("actions");
            foreach (KeyValuePair<string, long> kind in a.ActionsByKind) w.WriteNumber(kind.Key, kind.Value);
            w.WriteEndObject();
            w.WriteNumber("shoves", a.Shoves);
            w.WriteNumber("lobby_acts", a.LobbyActs);
            w.WriteNumber("round_restarts", a.RoundRestarts);
            w.WriteNumber("rests", a.Rests);
            w.WriteStartObject("behaviours");
            foreach (KeyValuePair<string, int> b in a.Behaviours) w.WriteNumber(b.Key, b.Value);
            w.WriteEndObject();
            w.WriteNumber("abuse_sent", a.AbuseSent);
            w.WriteNumber("deltas_applied", a.DeltasApplied);
            w.WriteNumber("own_moves", a.OwnMoves);
            w.WriteNumber("other_moves", a.OtherMoves);
            w.WriteNumber("spawns_seen", a.SpawnsSeen);
            w.WriteNumber("despawns_seen", a.DespawnsSeen);
            w.WriteNumber("server_texts", a.ServerTexts);
            w.WriteNumber("bytes_in", a.BytesIn);
            w.WriteNumber("bytes_out", a.BytesOut);
            w.WriteNumber("bytes_in_per_bot_per_second", Math.Round(a.BytesInPerBotPerSecond, 1));
            w.WriteNumber("bytes_out_per_bot_per_second", Math.Round(a.BytesOutPerBotPerSecond, 1));
            w.WriteNumber("desyncs", a.Desyncs);
            w.WriteNumber("decode_errors", a.DecodeErrors);
            w.WriteNumber("exceptions", a.Exceptions);
            if (a.FirstException != null) w.WriteString("first_exception", a.FirstException);
            w.WriteNumber("disconnects_total", a.DisconnectsTotal);
            w.WriteStartObject("disconnects");
            foreach (KeyValuePair<string, long> d in a.Disconnects) w.WriteNumber(d.Key, d.Value);
            w.WriteEndObject();
            w.WriteStartObject("prediction");
            w.WriteNumber("predicting_bots", a.PredictingBots);
            w.WriteNumber("reconciliations", a.Reconciliations);
            w.WriteNumber("corrections", a.Corrections);
            w.WriteNumber("snaps", a.Snaps);
            w.WriteNumber("mismatches", a.Mismatches);
            w.WriteNumber("max_error_tiles", Math.Round(a.MaxErrorTiles, 4));
            w.WriteNumber("overflows", a.Overflows);
            w.WriteEndObject();
            w.WriteStartObject("rtt_ms");
            w.WriteNumber("samples", a.RttSamples);
            w.WriteNumber("mean", Math.Round(a.RttMeanMs, 2));
            w.WriteNumber("p50", a.RttP50Ms);
            w.WriteNumber("p95", a.RttP95Ms);
            w.WriteNumber("p99", a.RttP99Ms);
            w.WriteNumber("max", a.RttMaxMs);
            w.WriteEndObject();
            w.WriteEndObject();

            if (includePerBot)
            {
                w.WriteStartArray("per_bot");
                foreach (BotRow r in PerBot)
                {
                    w.WriteStartObject();
                    w.WriteString("name", r.Name);
                    w.WriteString("state", r.State);
                    w.WriteString("behaviour", r.Behaviour);
                    w.WriteNumber("joins", r.Joins);
                    w.WriteNumber("connected_seconds", Math.Round(r.ConnectedSeconds, 3));
                    w.WriteNumber("inputs_sent", r.InputsSent);
                    w.WriteNumber("frames_sent", r.FramesSent);
                    w.WriteNumber("actions_sent", r.ActionsSent);
                    w.WriteNumber("shoves", r.Shoves);
                    w.WriteNumber("rests", r.Rests);
                    if (r.Predicted)
                    {
                        w.WriteNumber("corrections", r.Corrections);
                        w.WriteNumber("snaps", r.Snaps);
                        w.WriteNumber("mismatches", r.Mismatches);
                        w.WriteNumber("max_error_tiles", Math.Round(r.MaxErrorTiles, 4));
                    }
                    w.WriteNumber("own_moves", r.OwnMoves);
                    w.WriteNumber("other_moves", r.OtherMoves);
                    w.WriteNumber("bytes_in", r.BytesIn);
                    w.WriteNumber("bytes_out", r.BytesOut);
                    w.WriteNumber("desyncs", r.Desyncs);
                    w.WriteNumber("disconnects", r.DisconnectsTotal);
                    if (r.LastDisconnect != null) w.WriteString("last_disconnect", r.LastDisconnect);
                    w.WriteNumber("rtt_mean_ms", Math.Round(r.RttMeanMs, 2));
                    w.WriteNumber("rtt_max_ms", r.RttMaxMs);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

/// <summary>M3 P5. What a <see cref="BotBehaviour.Tour"/> run covered: the share of the reachable walkable (non-space) tiles the bots reached, the doors they stood on, the bots that got stuck.</summary>
public sealed class TourReport
{
    public int ReachableTiles { get; init; }
    public int CoveredTiles { get; init; }
    public double CoverageShare { get; init; }
    public int DoorsPassed { get; init; }
    public long DoorPressTicks { get; init; }
    public long Replans { get; init; }
    public int StuckBots { get; init; }
    public int StuckNow { get; init; }
}
