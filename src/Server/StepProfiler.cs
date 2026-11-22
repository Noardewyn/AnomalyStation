using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Anomaly.Core.Kernel;

namespace Anomaly.Server;

/// <summary>
/// The host's side of <see cref="World.Profiler"/> (M3 I1b, the stalls): times each named piece of a tick (the scheduler,
/// the world's phases, each subsystem's fire, tg's MC costs; each subsystem's init at boot) with the host's clock, and
/// keeps per-window totals and maxima. A tick whose step is slow is reported with its pieces and the GC's collections and
/// pause during it. Host thread only; nothing here enters the world.
/// </summary>
public sealed class StepProfiler : IStepProfiler
{
    private readonly struct Open
    {
        public Open(string name, long start) { Name = name; Start = start; }
        public string Name { get; }
        public long Start { get; }
    }

    public sealed class Entry
    {
        public long Count;
        public double TotalMs;
        public double MaxMs;
        public double TickMs; // in the current tick
    }

    private readonly Stack<Open> _open = new();
    private readonly Dictionary<string, Entry> _window = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entry> _all = new(StringComparer.Ordinal);
    private readonly List<string> _touched = new();
    private int _gc0, _gc1, _gc2;
    private TimeSpan _pause;
    private long _allocated, _jitMethods;
    private TimeSpan _jitTime;

    /// <summary>The init times of the subsystems at boot, in order (milliseconds).</summary>
    public List<(string Name, double Ms)> Inits { get; } = new();

    private string _initNow;
    private int _initsDone;

    /// <summary>The subsystem whose init runs at boot (null before the first), for any thread to read: how far a start is.</summary>
    public string InitNow => Volatile.Read(ref _initNow);

    /// <summary>How many subsystems have finished their init at boot, for any thread to read.</summary>
    public int InitsDone => Volatile.Read(ref _initsDone);

    public void Begin(string name)
    {
        _open.Push(new Open(name ?? "?", Stopwatch.GetTimestamp()));
        if (name != null && name.StartsWith("init ", StringComparison.Ordinal))
            Volatile.Write(ref _initNow, name.Substring(5));
    }

    public void End(string name)
    {
        long now = Stopwatch.GetTimestamp();
        // Pieces nest; an init that slept across ticks may end in a later tick, so an unmatched End is skipped.
        while (_open.Count > 0)
        {
            Open o = _open.Pop();
            if (!string.Equals(o.Name, name ?? "?", StringComparison.Ordinal)) continue;
            double ms = (now - o.Start) * 1000.0 / Stopwatch.Frequency;
            if (o.Name.StartsWith("init ", StringComparison.Ordinal))
            {
                Inits.Add((o.Name.Substring(5), ms));
                Interlocked.Increment(ref _initsDone);
                return;
            }
            Add(_window, o.Name, ms, track: true);
            Add(_all, o.Name, ms, track: false);
            return;
        }
    }

    private void Add(Dictionary<string, Entry> into, string name, double ms, bool track)
    {
        if (!into.TryGetValue(name, out Entry e)) into[name] = e = new Entry();
        e.Count++;
        e.TotalMs += ms;
        if (ms > e.MaxMs) e.MaxMs = ms;
        if (track)
        {
            if (e.TickMs == 0) _touched.Add(name);
            e.TickMs += ms;
        }
    }

    /// <summary>Starts a tick: the GC counters it is compared with.</summary>
    public void BeginTick()
    {
        foreach (string name in _touched) _window[name].TickMs = 0;
        _touched.Clear();
        _gc0 = GC.CollectionCount(0);
        _gc1 = GC.CollectionCount(1);
        _gc2 = GC.CollectionCount(2);
        _pause = GC.GetTotalPauseDuration();
        _allocated = GC.GetAllocatedBytesForCurrentThread();
        _jitMethods = System.Runtime.JitInfo.GetCompiledMethodCount(currentThread: false);
        _jitTime = System.Runtime.JitInfo.GetCompilationTime(currentThread: false);
    }

    /// <summary>The GC's work since <see cref="BeginTick"/>: collections by generation and the pause.</summary>
    public (int Gen0, int Gen1, int Gen2, double PauseMs) GcSinceTickStart()
        => (GC.CollectionCount(0) - _gc0, GC.CollectionCount(1) - _gc1, GC.CollectionCount(2) - _gc2, (GC.GetTotalPauseDuration() - _pause).TotalMilliseconds);

    /// <summary>The slow-tick line: the step, the GC during it and the pieces of the tick, largest first.</summary>
    public string SlowTick(long tick, double stepMs)
    {
        var gc = GcSinceTickStart();
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"slow tick={tick} step={stepMs:0.0} ms | gc gen0+{gc.Gen0} gen1+{gc.Gen1} gen2+{gc.Gen2} pause {gc.PauseMs:0.0} ms | allocated {(GC.GetAllocatedBytesForCurrentThread() - _allocated) / (1024 * 1024)} MB | jit +{System.Runtime.JitInfo.GetCompiledMethodCount(currentThread: false) - _jitMethods} methods {(System.Runtime.JitInfo.GetCompilationTime(currentThread: false) - _jitTime).TotalMilliseconds:0} ms | heap {GC.GetTotalMemory(false) / (1024 * 1024)} MB |");
        foreach (string name in _touched.OrderByDescending(n => _window[n].TickMs).Take(8))
            sb.Append(CultureInfo.InvariantCulture, $" {name} {_window[name].TickMs:0.0}");
        return sb.ToString();
    }

    /// <summary>The window's summary (the status line's companion): the pieces with the largest totals, then a reset.</summary>
    public string WindowSummary(long ticks)
    {
        var sb = new StringBuilder("costs (ms per tick avg/max):");
        foreach (var kv in _window.OrderByDescending(kv => kv.Value.TotalMs).Take(10))
            sb.Append(CultureInfo.InvariantCulture, $" {kv.Key} {kv.Value.TotalMs / Math.Max(1, ticks):0.00}/{kv.Value.MaxMs:0.0}");
        foreach (Entry e in _window.Values) { e.Count = 0; e.TotalMs = 0; e.MaxMs = 0; }
        return sb.ToString();
    }

    /// <summary>All the run's pieces (name, count, total ms, max ms), largest total first.</summary>
    public IEnumerable<(string Name, long Count, double TotalMs, double MaxMs)> Totals()
        => _all.OrderByDescending(kv => kv.Value.TotalMs).Select(kv => (kv.Key, kv.Value.Count, kv.Value.TotalMs, kv.Value.MaxMs));
}
