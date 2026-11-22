using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace Anomaly.Server;

/// <summary>
/// A latency histogram with fixed buckets (scope 2i): recording never allocates. Values are whole microseconds from 0 to
/// <see cref="MaxValue"/> (larger ones count as <see cref="MaxValue"/>); buckets are exact below 64 µs and then 32 per
/// power of two, so a reported percentile is the upper edge of its bucket, at most about 3% above the true value (and
/// never above <see cref="Max"/>, which is exact). The layout is HdrHistogram's with two significant binary digits less.
/// </summary>
public sealed class LatencyHistogram
{
    /// <summary>The largest value kept apart (about 35 minutes in µs).</summary>
    public const long MaxValue = (1L << 31) - 1;

    private const int SubBuckets = 32;              // per power of two above the linear range
    private const int LinearRange = 2 * SubBuckets; // values below this have their own bucket
    // MaxValue's top bit is bit 30, so its bucket is SubBuckets * (30 - 5 + 1) + 31: 864 buckets (6.9 KB).
    private const int BucketCount = SubBuckets * (30 - 5 + 2);

    private readonly long[] _counts = new long[BucketCount];

    /// <summary>Values recorded.</summary>
    public long Count { get; private set; }

    /// <summary>The largest value recorded (µs), 0 when empty.</summary>
    public long Max { get; private set; }

    /// <summary>The smallest value recorded (µs), 0 when empty.</summary>
    public long Min { get; private set; }

    /// <summary>The sum of the values recorded (µs), for the mean.</summary>
    public long Sum { get; private set; }

    /// <summary>The mean (µs), 0 when empty.</summary>
    public double Mean => Count == 0 ? 0 : (double)Sum / Count;

    /// <summary>Records <paramref name="micros"/> (negative values count as 0).</summary>
    public void Record(long micros)
    {
        if (micros < 0) micros = 0;
        if (micros > MaxValue) micros = MaxValue;
        _counts[BucketOf(micros)]++;
        if (Count == 0 || micros < Min) Min = micros;
        if (micros > Max) Max = micros;
        Count++;
        Sum += micros;
    }

    /// <summary>
    /// The value at <paramref name="percentile"/> (0 to 100) in µs: the upper edge of the bucket that holds the value of
    /// rank <c>ceil(p / 100 * Count)</c>, capped at <see cref="Max"/>; 0 when empty.
    /// </summary>
    public long Percentile(double percentile)
    {
        if (Count == 0) return 0;
        if (percentile <= 0) return Min;
        // The epsilon keeps a rank that is a whole number in exact arithmetic (99.9% of 10000 = 9990) from rounding up.
        long rank = (long)Math.Ceiling(Math.Min(percentile, 100) / 100.0 * Count - 1e-9);
        if (rank < 1) rank = 1;
        long seen = 0;
        for (int i = 0; i < _counts.Length; i++)
        {
            seen += _counts[i];
            if (seen >= rank)
                return Math.Min(UpperEdge(i), Max);
        }
        return Max;
    }

    /// <summary>Forgets everything (a new window).</summary>
    public void Reset()
    {
        Array.Clear(_counts);
        Count = 0;
        Max = 0;
        Min = 0;
        Sum = 0;
    }

    /// <summary>The bucket of a value (public for the tests of the bucket math).</summary>
    public static int BucketOf(long value)
    {
        if (value < LinearRange) return (int)value;
        int msb = 63 - BitOperations.LeadingZeroCount((ulong)value); // >= 6
        int shift = msb - 5;                                         // keeps 6 significant bits: 32..63
        return SubBuckets * (shift + 1) + (int)((value >> shift) - SubBuckets);
    }

    /// <summary>The largest value of bucket <paramref name="index"/>.</summary>
    public static long UpperEdge(int index)
    {
        if (index < LinearRange) return index;
        int shift = index / SubBuckets - 1;
        long sub = index % SubBuckets + SubBuckets;
        return ((sub + 1) << shift) - 1;
    }

    /// <summary>The smallest value of bucket <paramref name="index"/>.</summary>
    public static long LowerEdge(int index)
    {
        if (index < LinearRange) return index;
        int shift = index / SubBuckets - 1;
        long sub = index % SubBuckets + SubBuckets;
        return sub << shift;
    }

    /// <summary>"p50 0.12 p95 0.31 p99 0.52 p99.9 1.10 max 2.31" (milliseconds).</summary>
    public string Summary()
        => string.Create(CultureInfo.InvariantCulture,
            $"p50 {Ms(Percentile(50))} p95 {Ms(Percentile(95))} p99 {Ms(Percentile(99))} p99.9 {Ms(Percentile(99.9))} max {Ms(Max)}");

    internal static string Ms(long micros) => (micros / 1000.0).ToString("0.00", CultureInfo.InvariantCulture);
}

/// <summary>What one tick cost (the host fills it; <see cref="ServerMetrics.EndTick"/> records it).</summary>
public struct TickSample
{
    public long Tick;
    public long PollMicros;
    public long StepMicros;
    public long ReplicationMicros;
    public long SendMicros;
    public long TotalMicros;
    public long LatenessMicros;
    public bool Overrun;
    public int Sessions;
    public long BytesOut;
    public long BytesIn;

    /// <summary>M2: SSmobs fired in this tick (every living mob's Life ran in it): its step time is the "SSmobs tick" figure.</summary>
    public bool MobsFired;
}

/// <summary>
/// The server's metrics (scope 2i): per tick the world step, replication, network poll and send, the total, the pacer's
/// lateness and overruns, in allocation-free histograms per window (<see cref="ServerConfig.StatusEveryTicks"/>, 10 s) and
/// for the run; bytes per session per second (the host's own counters and the transport's); GC counts per generation,
/// the total GC pause, the bytes the host thread allocated per tick and the heap size. A status line per window, a CSV row
/// per tick (<c>--metrics FILE</c>) and a summary at shutdown.
/// </summary>
public sealed class ServerMetrics : IDisposable
{
    /// <summary>The CSV header (one row per tick).</summary>
    public const string CsvHeader = "tick,poll_us,step_us,replication_us,send_us,total_us,lateness_us,overrun,sessions,bytes_out,bytes_in,alloc_bytes,gc0,gc1,gc2,heap_bytes,mobs_fired";

    private readonly TextWriter _csv;
    private readonly char[] _row = new char[256];

    private long _allocMark;
    private int _gc0Mark, _gc1Mark, _gc2Mark;
    private TimeSpan _pauseMark;
    private long _windowTicks, _windowOverruns, _windowAlloc, _windowBytesOut, _windowBytesIn;
    private long _windowTransportOut, _windowTransportIn, _windowSessionTicks;
    private int _windowGc0, _windowGc1, _windowGc2;
    private TimeSpan _windowPauseStart;

    public ServerMetrics(TextWriter csv = null)
    {
        _csv = csv;
        _csv?.Write(CsvHeader + "\n"); // '\n' on every OS, as the rows end
        _allocMark = GC.GetAllocatedBytesForCurrentThread();
        _gc0Mark = GC.CollectionCount(0);
        _gc1Mark = GC.CollectionCount(1);
        _gc2Mark = GC.CollectionCount(2);
        _pauseMark = GC.GetTotalPauseDuration();
        StartGc = (_gc0Mark, _gc1Mark, _gc2Mark, _pauseMark);
        _windowGc0 = _gc0Mark; _windowGc1 = _gc1Mark; _windowGc2 = _gc2Mark; _windowPauseStart = _pauseMark;
    }

    public LatencyHistogram WindowStep { get; } = new();
    public LatencyHistogram WindowReplication { get; } = new();
    public LatencyHistogram WindowPoll { get; } = new();
    public LatencyHistogram WindowSend { get; } = new();
    public LatencyHistogram WindowTotal { get; } = new();
    public LatencyHistogram WindowLateness { get; } = new();
    /// <summary>M2: the world step of the ticks in which SSmobs fired (the heaviest ticks of the run, DV3), this window.</summary>
    public LatencyHistogram WindowMobsStep { get; } = new();
    /// <summary>M2: the same for the run.</summary>
    public LatencyHistogram RunMobsStep { get; } = new();
    public LatencyHistogram RunStep { get; } = new();
    public LatencyHistogram RunReplication { get; } = new();
    public LatencyHistogram RunPoll { get; } = new();
    public LatencyHistogram RunSend { get; } = new();
    public LatencyHistogram RunTotal { get; } = new();
    public LatencyHistogram RunLateness { get; } = new();

    /// <summary>Ticks recorded in the run.</summary>
    public long Ticks { get; private set; }

    /// <summary>Overruns in the run.</summary>
    public long Overruns { get; private set; }

    /// <summary>Bytes the host thread allocated in the run's ticks.</summary>
    public long AllocatedBytes { get; private set; }

    /// <summary>The last finished window's status text (for the console's <c>status</c>), null before the first.</summary>
    public string LastWindow { get; private set; }

    private (int Gc0, int Gc1, int Gc2, TimeSpan Pause) StartGc { get; }

    /// <summary>Records one tick (no allocation unless a CSV row is written, which formats into a reused buffer).</summary>
    public void EndTick(in TickSample s)
    {
        long alloc = GC.GetAllocatedBytesForCurrentThread();
        long allocTick = alloc - _allocMark;
        _allocMark = alloc;
        AllocatedBytes += allocTick;
        _windowAlloc += allocTick;

        Record(WindowStep, RunStep, s.StepMicros);
        if (s.MobsFired) Record(WindowMobsStep, RunMobsStep, s.StepMicros);
        Record(WindowReplication, RunReplication, s.ReplicationMicros);
        Record(WindowPoll, RunPoll, s.PollMicros);
        Record(WindowSend, RunSend, s.SendMicros);
        Record(WindowTotal, RunTotal, s.TotalMicros);
        Record(WindowLateness, RunLateness, s.LatenessMicros);
        Ticks++;
        _windowTicks++;
        if (s.Overrun) { Overruns++; _windowOverruns++; }
        _windowBytesOut += s.BytesOut;
        _windowBytesIn += s.BytesIn;
        _windowSessionTicks += s.Sessions;

        if (_csv != null)
        {
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
            long heap = GC.GetTotalMemory(false);
            Span<char> row = _row;
            int n = 0;
            Append(row, ref n, s.Tick); row[n++] = ',';
            Append(row, ref n, s.PollMicros); row[n++] = ',';
            Append(row, ref n, s.StepMicros); row[n++] = ',';
            Append(row, ref n, s.ReplicationMicros); row[n++] = ',';
            Append(row, ref n, s.SendMicros); row[n++] = ',';
            Append(row, ref n, s.TotalMicros); row[n++] = ',';
            Append(row, ref n, s.LatenessMicros); row[n++] = ',';
            row[n++] = s.Overrun ? '1' : '0'; row[n++] = ',';
            Append(row, ref n, s.Sessions); row[n++] = ',';
            Append(row, ref n, s.BytesOut); row[n++] = ',';
            Append(row, ref n, s.BytesIn); row[n++] = ',';
            Append(row, ref n, allocTick); row[n++] = ',';
            Append(row, ref n, gc0 - _gc0Mark); row[n++] = ',';
            Append(row, ref n, gc1 - _gc1Mark); row[n++] = ',';
            Append(row, ref n, gc2 - _gc2Mark); row[n++] = ',';
            Append(row, ref n, heap); row[n++] = ',';
            row[n++] = s.MobsFired ? '1' : '0';
            row[n++] = '\n';
            _csv.Write(row.Slice(0, n));
            _gc0Mark = gc0; _gc1Mark = gc1; _gc2Mark = gc2;
        }
    }

    /// <summary>Version 13: the sound plays the per-session budget dropped in the run (the quietest first, protocol.md section 22).</summary>
    public long SoundsDropped { get; private set; }

    private long _windowSoundsDropped;

    /// <summary>Version 13: counts plays the sound budget dropped (the host, after a session's Sounds message is built).</summary>
    public void AddSoundsDropped(int count)
    {
        SoundsDropped += count;
        _windowSoundsDropped += count;
    }

    /// <summary>Adds transport-counted bytes (LiteNetLib's counters) of the window.</summary>
    public void AddTransportBytes(long sent, long received)
    {
        _windowTransportOut += sent;
        _windowTransportIn += received;
    }

    /// <summary>The window's status line, then a new window.</summary>
    public string EndWindow(long tick)
    {
        int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        TimeSpan pause = GC.GetTotalPauseDuration();
        double seconds = _windowTicks / (double)Anomaly.Core.Kernel.World.Fps;
        double avgSessions = _windowTicks == 0 ? 0 : _windowSessionTicks / (double)_windowTicks;
        double perSessionOut = avgSessions <= 0 || seconds <= 0 ? 0 : _windowBytesOut / avgSessions / seconds;
        double perSessionIn = avgSessions <= 0 || seconds <= 0 ? 0 : _windowBytesIn / avgSessions / seconds;
        double transportOut = avgSessions <= 0 || seconds <= 0 ? 0 : _windowTransportOut / avgSessions / seconds;
        double transportIn = avgSessions <= 0 || seconds <= 0 ? 0 : _windowTransportIn / avgSessions / seconds;
        string line = string.Create(CultureInfo.InvariantCulture,
            $"status tick={tick} ticks={_windowTicks} sessions={avgSessions:0.#} | total {WindowTotal.Summary()} | step {WindowStep.Summary()} | "
            + $"repl p99 {LatencyHistogram.Ms(WindowReplication.Percentile(99))} poll p99 {LatencyHistogram.Ms(WindowPoll.Percentile(99))} "
            + $"send p99 {LatencyHistogram.Ms(WindowSend.Percentile(99))} late p99 {LatencyHistogram.Ms(WindowLateness.Percentile(99))} overruns={_windowOverruns} | "
            + $"B/s per session out {perSessionOut:0} in {perSessionIn:0} (transport out {transportOut:0} in {transportIn:0}) | "
            + $"mobs-tick {(WindowMobsStep.Count == 0 ? "-" : WindowMobsStep.Summary())} | gc {gc0 - _windowGc0}/{gc1 - _windowGc1}/{gc2 - _windowGc2} pause {(pause - _windowPauseStart).TotalMilliseconds:0.0} ms "
            + $"alloc/tick {(_windowTicks == 0 ? 0 : _windowAlloc / _windowTicks)} B heap {GC.GetTotalMemory(false) / (1024.0 * 1024.0):0.0} MB");
        if (_windowSoundsDropped > 0)
            line += " | sounds dropped " + _windowSoundsDropped.ToString(CultureInfo.InvariantCulture);
        _windowSoundsDropped = 0;
        _windowGc0 = gc0; _windowGc1 = gc1; _windowGc2 = gc2; _windowPauseStart = pause;
        _windowTicks = 0; _windowOverruns = 0; _windowAlloc = 0; _windowBytesOut = 0; _windowBytesIn = 0;
        _windowTransportOut = 0; _windowTransportIn = 0; _windowSessionTicks = 0;
        WindowMobsStep.Reset();
        WindowStep.Reset(); WindowReplication.Reset(); WindowPoll.Reset(); WindowSend.Reset(); WindowTotal.Reset(); WindowLateness.Reset();
        _csv?.Flush();
        LastWindow = line;
        return line;
    }

    /// <summary>The run's summary (several lines).</summary>
    public string Summary(long totalBytesOut, long totalBytesIn, int sessionsServed, int runtimes)
    {
        TimeSpan pause = GC.GetTotalPauseDuration() - StartGc.Pause;
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"summary ticks={Ticks} overruns={Overruns} sessions_served={sessionsServed} runtimes={runtimes}\n");
        sb.Append(CultureInfo.InvariantCulture, $"summary total {RunTotal.Summary()} mean {RunTotal.Mean / 1000.0:0.000}\n");
        sb.Append(CultureInfo.InvariantCulture, $"summary step {RunStep.Summary()}\n");
        sb.Append(CultureInfo.InvariantCulture, $"summary mobs-tick (world step of the {RunMobsStep.Count} ticks SSmobs fired in) {(RunMobsStep.Count == 0 ? "-" : RunMobsStep.Summary())}\n");
        sb.Append(CultureInfo.InvariantCulture, $"summary replication {RunReplication.Summary()}\n");
        sb.Append(CultureInfo.InvariantCulture, $"summary poll {RunPoll.Summary()}\n");
        sb.Append(CultureInfo.InvariantCulture, $"summary send {RunSend.Summary()}\n");
        sb.Append(CultureInfo.InvariantCulture, $"summary lateness {RunLateness.Summary()}\n");
        sb.Append(CultureInfo.InvariantCulture,
            $"summary bytes out={totalBytesOut} in={totalBytesIn} gc {GC.CollectionCount(0) - StartGc.Gc0}/{GC.CollectionCount(1) - StartGc.Gc1}/{GC.CollectionCount(2) - StartGc.Gc2} "
            + $"pause {pause.TotalMilliseconds:0.0} ms alloc/tick {(Ticks == 0 ? 0 : AllocatedBytes / Ticks)} B heap {GC.GetTotalMemory(false) / (1024.0 * 1024.0):0.0} MB");
        return sb.ToString();
    }

    public void Dispose() => _csv?.Dispose();

    private static void Record(LatencyHistogram window, LatencyHistogram run, long micros)
    {
        window.Record(micros);
        run.Record(micros);
    }

    private static void Append(Span<char> row, ref int n, long value)
    {
        value.TryFormat(row.Slice(n), out int written, default, CultureInfo.InvariantCulture);
        n += written;
    }
}
