using Anomaly.Core.Motion;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Tools.Bots;

/// <summary>
/// What one <see cref="Bot"/> counted (scope 2i, P5: connected time, inputs sent, moves observed, bytes, disconnects with
/// reasons, desyncs, RTT). Plain counters, written by the bot on its own thread; time is counted in
/// <see cref="Bot.Update"/> calls and turned into seconds by the report (the runner knows the real update rate).
/// </summary>
public sealed class BotStats
{
    /// <summary>The number of 1 ms RTT buckets; the last one also holds everything above.</summary>
    public const int RttBuckets = 256;

    public BotStats(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>The behaviour the bot actually runs (for <see cref="BotBehaviour.Mixed"/>, the one its index got), as its name on the command line.</summary>
    public string Behaviour { get; set; }

    /// <summary>M3 P5 (tour). Re-plans because the bot stood still.</summary>
    public long TourReplans { get; set; }

    /// <summary>M3 P5 (tour). Times the bot stood still for the stuck limit.</summary>
    public long TourStuckEvents { get; set; }

    /// <summary>M3 P5 (tour). The bot is in a stuck streak now.</summary>
    public bool TourStuck { get; set; }

    /// <summary>M3 P5 (tour). Ticks spent pressing into a closed door.</summary>
    public long TourDoorPressTicks { get; set; }

    /// <summary><see cref="Bot.Update"/> calls so far.</summary>
    public long Updates { get; set; }

    /// <summary>Updates spent in <see cref="BotState.Joined"/> (the connected time).</summary>
    public long ConnectedUpdates { get; set; }

    /// <summary>Connection attempts (<see cref="Bot.Connect"/> calls).</summary>
    public long ConnectAttempts { get; set; }

    /// <summary>Times the join sequence completed (Welcome, MapGrid and Snapshot).</summary>
    public long Joins { get; set; }

    /// <summary>MotionInput messages sent on the Input channel (the flood bot's included; one per update for a walking bot).</summary>
    public long InputsSent { get; set; }

    /// <summary>Input frames sent (the newest of each message; the copies the messages repeat are not counted).</summary>
    public long FramesSent { get; set; }

    /// <summary>Action messages sent, by <see cref="ActionKind"/> (the index is the kind's value).</summary>
    public long[] ActionsByKind { get; } = new long[ActionKindCount];

    /// <summary>The number of <see cref="ActionKind"/> values (None included, never sent).</summary>
    public const int ActionKindCount = 8;

    /// <summary>All actions sent.</summary>
    public long ActionsSent
    {
        get
        {
            long n = 0;
            foreach (long k in ActionsByKind) n += k;
            return n;
        }
    }

    /// <summary>Right clicks on another body: the shoves the bot asked for.</summary>
    public long ShovesSent { get; set; }

    /// <summary>M10 slice J. Presses in tg's lobby and late join menu (Ready, Join, the job).</summary>
    public long LobbyActs { get; set; }

    /// <summary>M10 slice J. Rounds the server ended under the bot (D306: the BetweenRounds text, after which it joins the next world on the same connection).</summary>
    public long RoundRestarts { get; set; }

    /// <summary>M4a P6. Interactions a <see cref="BotBehaviour.Fiddle"/> bot sent (hovers, clicks and keybindings together).</summary>
    public long FiddleActions { get; set; }

    /// <summary>Times the bot asked to lie down (a Rest from standing).</summary>
    public long RestsStarted { get; set; }

    /// <summary>Abuse messages sent that are not ordinary frames or actions (garbage, oversized, forged, bad, over-cap).</summary>
    public long AbuseSent { get; set; }

    /// <summary>Deltas applied.</summary>
    public long DeltasApplied { get; set; }

    /// <summary>Snapshots applied (one per join).</summary>
    public long SnapshotsApplied { get; set; }

    /// <summary>Position changes of the bot's own entity seen in Deltas.</summary>
    public long OwnMoves { get; set; }

    /// <summary>Position changes of every other entity seen in Deltas.</summary>
    public long OtherMoves { get; set; }

    /// <summary>Entities that appeared in Deltas.</summary>
    public long SpawnsSeen { get; set; }

    /// <summary>Entities that disappeared in Deltas.</summary>
    public long DespawnsSeen { get; set; }

    /// <summary>ServerText messages received.</summary>
    public long ServerTexts { get; set; }

    /// <summary>Protocol payload bytes sent (the transport's own framing excluded).</summary>
    public long BytesOut { get; set; }

    /// <summary>Protocol payload bytes received.</summary>
    public long BytesIn { get; set; }

    /// <summary>Messages sent.</summary>
    public long MessagesOut { get; set; }

    /// <summary>Messages received.</summary>
    public long MessagesIn { get; set; }

    /// <summary>
    /// Desync detections: the replica refused a Delta (a gap or an out-of-order tick), a join message did not decode, or
    /// the tick of the replica did not advance by one. The bot leaves and (if it reconnects) joins again.
    /// </summary>
    public long Desyncs { get; set; }

    /// <summary>The text of the last desync.</summary>
    public string LastDesync { get; set; }

    /// <summary>Messages that did not decode, or arrived where they do not belong, but did not break the replica (a bad ServerText, an unknown id).</summary>
    public long DecodeErrors { get; set; }

    /// <summary>Exceptions a bot's update let out (the runner catches them so that the run goes on; a clean run has none).</summary>
    public long Exceptions { get; set; }

    /// <summary>The first such exception, as text.</summary>
    public string LastException { get; set; }

    /// <summary>Connections that ended, by kind and reason, for example <c>RemoteClose:Kicked</c> or <c>LocalClose:None</c>.</summary>
    public Dictionary<string, long> Disconnects { get; } = new();

    /// <summary>The last disconnect as text (<c>Kind:Reason text</c>), null while none ended.</summary>
    public string LastDisconnect { get; set; }

    /// <summary>All connections that ended.</summary>
    public long DisconnectsTotal { get; set; }

    // ---- prediction (a bot with BotOptions.Predict; the counters of its MotionPredictor, summed over its connections) ----

    /// <summary>True when the bot predicted at least once.</summary>
    public bool Predicted { get; set; }

    /// <summary>Authoritative states reconciled.</summary>
    public long Reconciliations { get; set; }

    /// <summary>Reconciliations whose error was at least 1 cm (smoothed or snapped).</summary>
    public long Corrections { get; set; }

    /// <summary>Reconciliations that snapped (an error over a tile).</summary>
    public long Snaps { get; set; }

    /// <summary>Reconciliations whose replay differed from the old prediction in any bit.</summary>
    public long Mismatches { get; set; }

    /// <summary>The largest prediction error seen, in tiles.</summary>
    public double MaxErrorTiles { get; set; }

    /// <summary>Frames the predictor dropped because the server acknowledged nothing for a whole ring.</summary>
    public long Overflows { get; set; }

    /// <summary>Adds the growth of a predictor's counters since <paramref name="seen"/> and remembers <paramref name="now"/> as seen.</summary>
    public void AddPrediction(ref PredictionCounters seen, in PredictionCounters now)
    {
        Predicted = true;
        Reconciliations += now.Reconciliations - seen.Reconciliations;
        Corrections += now.Corrections - seen.Corrections;
        Snaps += now.Snaps - seen.Snaps;
        Mismatches += now.Mismatches - seen.Mismatches;
        Overflows += now.Overflows - seen.Overflows;
        if (now.MaxErrorTiles > MaxErrorTiles) MaxErrorTiles = now.MaxErrorTiles;
        seen = now;
    }

    /// <summary>RTT samples (the transport's <see cref="IClientTransport.RoundTripMs"/>, once per second of updates while joined).</summary>
    public long RttSamples { get; set; }

    public long RttSumMs { get; set; }

    public int RttMaxMs { get; set; }

    /// <summary>RTT histogram, 1 ms buckets (<see cref="RttBuckets"/>).</summary>
    public int[] RttHistogram { get; } = new int[RttBuckets];

    public void AddRtt(int ms)
    {
        if (ms < 0) ms = 0;
        RttSamples++;
        RttSumMs += ms;
        if (ms > RttMaxMs) RttMaxMs = ms;
        RttHistogram[Math.Min(ms, RttBuckets - 1)]++;
    }

    public void AddDisconnect(TransportDisconnectKind kind, DisconnectReason reason)
    {
        DisconnectsTotal++;
        string key = kind + ":" + reason;
        Disconnects.TryGetValue(key, out long n);
        Disconnects[key] = n + 1;
    }
}
