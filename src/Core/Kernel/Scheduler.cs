namespace Anomaly.Core.Kernel;

/// <summary>
/// Tick-ordered queue of continuations: the deterministic replacement for the BYOND sleep queue
/// (sleeping procs, <c>spawn</c>, the Master Controller loop). Entries run in (tick, insertion sequence)
/// order, which is BYOND's "sleeps are processed in the order received". Work scheduled for the current tick
/// while the queue drains runs in the same drain, after everything already queued (<c>sleep(0)</c>).
/// Each entry carries the <c>usr</c> that was ambient when it was scheduled and runs with it.
/// </summary>
public sealed class Scheduler
{
    private readonly PriorityQueue<Entry, Key> _queue = new(KeyComparer.Instance);
    private long _seq;
    private readonly World _world;

    // Zero-delay hops of the entry that is running now (-1: not inside an entry).
    private int _runningHops = -1;
    private bool _faultResume;

    /// <summary>
    /// A chain of zero-delay hops (<c>sleep(0)</c>, <c>spawn(0)</c>) longer than this inside one tick is a
    /// runaway loop: a sleeping proc gets a runtime error at its sleep (it unwinds to its isolation boundary
    /// and is reported there), a plain entry is reported and moved to the next tick.
    /// </summary>
    public int MaxZeroDelayHops { get; set; } = 100_000;

    /// <summary>
    /// Backstop for one drain: after this many runs the drain stops, reports, and leaves every remaining
    /// entry queued (nothing is dropped; they keep their keys and run first at the next drain).
    /// </summary>
    public int MaxRunsPerDrain { get; set; } = 1_000_000;

    internal Scheduler(World world) => _world = world;

    public int Pending => _queue.Count;

    /// <summary>Total number of entries ever scheduled; part of the state hash.</summary>
    public long Sequence => _seq;

    private readonly struct Entry
    {
        public readonly Action Run;
        public readonly object Usr;
        public readonly int Hops;
        public readonly bool IsSleep;

        public Entry(Action run, object usr, int hops, bool isSleep)
        {
            Run = run;
            Usr = usr;
            Hops = hops;
            IsSleep = isSleep;
        }
    }

    private readonly struct Key
    {
        public readonly long Tick;
        public readonly long Seq;
        public Key(long tick, long seq) { Tick = tick; Seq = seq; }
    }

    private sealed class KeyComparer : IComparer<Key>
    {
        public static readonly KeyComparer Instance = new();

        public int Compare(Key a, Key b)
        {
            if (a.Tick != b.Tick) return a.Tick < b.Tick ? -1 : 1;
            return a.Seq.CompareTo(b.Seq);
        }
    }

    /// <summary>
    /// Run <paramref name="action"/> at tick <paramref name="tick"/> (a tick that has passed means the current
    /// tick, after everything already queued). The ambient usr is captured and re-imposed when it runs.
    /// </summary>
    public void At(long tick, Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        Enqueue(tick, action, isSleep: false);
    }

    /// <summary>Run <paramref name="action"/> after <paramref name="ticks"/> ticks (0 or less: this tick, after the queue).</summary>
    public void After(long ticks, Action action) => At(SaturatingAdd(_world.Tick, ticks), action);

    internal void EnqueueSleep(long ticks, Action continuation) =>
        Enqueue(SaturatingAdd(_world.Tick, ticks), continuation, isSleep: true);

    private void Enqueue(long tick, Action run, bool isSleep)
    {
        long now = _world.Tick;
        int hops = 0;
        if (tick <= now)
        {
            tick = now;
            if (_runningHops >= 0) hops = _runningHops + 1;
        }
        _queue.Enqueue(new Entry(run, _world.usr, hops, isSleep), new Key(tick, _seq++));
    }

    /// <summary>a + b for a non-negative tick and a delay, saturating at long.MaxValue (a delay that never ends).</summary>
    internal static long SaturatingAdd(long tick, long ticks)
    {
        if (ticks <= 0) return tick;
        return ticks > long.MaxValue - tick ? long.MaxValue : tick + ticks;
    }

    // Float error guard: exact multiples of tick_lag (1.5 ds = 3 ticks) must not lose a tick to rounding.
    private const double RelativeEpsilon = 1e-6;

    /// <summary>
    /// DM delay rounding for <c>sleep</c> and <c>spawn</c> (BYOND/OpenDream): a positive delay in deciseconds
    /// becomes max(1, floor(ds / tick_lag)) ticks; 0, negative or NaN is 0 (this tick, after the queue).
    /// Delays beyond the long range (tg INFINITY = 1e31, +inf) saturate to long.MaxValue: never.
    /// </summary>
    public static long TicksFromDeciseconds(float deciseconds)
    {
        if (!(deciseconds > 0f)) return 0;
        double t = (double)deciseconds / World.TickLag;
        double floor = Math.Floor(t + t * RelativeEpsilon);
        if (floor >= 9.2233720368547758E18) return long.MaxValue;
        return Math.Max(1L, (long)floor);
    }

    /// <summary>
    /// The number of whole ticks after which at least <paramref name="deciseconds"/> of game time have passed
    /// (ceil, for host and test code such as <see cref="World.StepDeciseconds"/>; not DM sleep rounding).
    /// </summary>
    public static long TicksCovering(float deciseconds)
    {
        if (!(deciseconds > 0f)) return 0;
        double t = (double)deciseconds / World.TickLag;
        double ceil = Math.Ceiling(t - t * RelativeEpsilon);
        if (ceil >= 9.2233720368547758E18) return long.MaxValue;
        return Math.Max(1L, (long)ceil);
    }

    /// <summary>
    /// Run every entry due at or before <paramref name="tick"/>, including ones added meanwhile, in
    /// (tick, sequence) order. Each entry is an isolation boundary: an exception it throws is reported and
    /// the drain continues. Returns the number of entries run.
    /// </summary>
    public int RunDue(long tick)
    {
        int runs = 0;
        while (_queue.TryPeek(out var entry, out var key) && key.Tick <= tick)
        {
            if (runs >= MaxRunsPerDrain)
            {
                _world.ReportRuntime(new WaitingException(
                    $"Scheduler drain stopped after {runs} runs (MaxRunsPerDrain); the remaining due entries "
                    + "stay queued and run first at the next drain."));
                break;
            }
            _queue.Dequeue();
            runs++;

            if (entry.Hops > MaxZeroDelayHops && !entry.IsSleep)
            {
                _world.ReportRuntime(new WaitingException(
                    $"Runaway zero-delay loop: a spawn chain made {entry.Hops} zero-delay hops in tick {_world.Tick}; "
                    + "its next entry is moved to the next tick."));
                _queue.Enqueue(new Entry(entry.Run, entry.Usr, 0, false), new Key(SaturatingAdd(_world.Tick, 1), _seq++));
                continue;
            }

            var previousUsr = _world.usr;
            int previousHops = _runningHops;
            _world.usr = entry.Usr;
            _runningHops = entry.Hops;
            _faultResume = entry.IsSleep && entry.Hops > MaxZeroDelayHops;
            try
            {
                entry.Run();
            }
            catch (Exception e)
            {
                _world.ReportRuntime(e);
            }
            finally
            {
                _faultResume = false;
                _runningHops = previousHops;
                _world.usr = previousUsr;
            }
        }
        return runs;
    }

    internal void ThrowIfRunawayResume()
    {
        if (!_faultResume) return;
        _faultResume = false;
        throw new WaitingException(
            $"Runaway zero-delay loop: a proc made more than {MaxZeroDelayHops} consecutive sleep(0) hops in tick "
            + $"{_world.Tick}; it is stopped at its sleep.");
    }

    public void HashInto(ref StateHasher h)
    {
        h.Add(_seq);
        h.Add(_queue.Count);
        // Wake ticks of pending entries, in a stable order (a copy sorted by key; keys are unique).
        var keys = new List<Key>(_queue.Count);
        foreach (var (_, key) in _queue.UnorderedItems) keys.Add(key);
        keys.Sort(KeyComparer.Instance);
        foreach (var k in keys) { h.Add(k.Tick); h.Add(k.Seq); }
    }
}

/// <summary>
/// A runtime error raised by the waiting model itself (a runaway zero-delay loop, a proc that slept inside a
/// no-sleep context, a stopped drain). Reported like any DM runtime.
/// </summary>
public sealed class WaitingException : Exception
{
    public WaitingException(string message) : base(message) { }
}
