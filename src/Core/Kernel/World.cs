using System.Threading;

namespace Anomaly.Core.Kernel;

public delegate void StateHashContributor(World world, ref StateHasher hasher);

/// <summary>
/// One simulated world: tick counter, time in tg units, deterministic RNG and the continuation
/// scheduler. All gameplay state hangs off a world. Translated code reaches "its" world through the
/// ambient <see cref="Current"/>, which <see cref="Step"/> and <see cref="Enter"/> establish; this is the
/// only ambient state in the core and it is thread-local, so worlds can run in parallel tests.
/// </summary>
public sealed class World
{
    /// <summary>tg <c>world.fps</c> (code/game/world.dm sets fps = 20).</summary>
    public const int Fps = 20;

    /// <summary>tg <c>world.tick_lag</c>: deciseconds per tick, 0.5.</summary>
    public const float TickLag = 10f / Fps;

    [ThreadStatic] private static World _current;

    public static World Current => _current ?? throw new InvalidOperationException("No ambient World: wrap the call in world.Enter() or run it from World.Step().");
    public static World CurrentOrNull => _current;

    public ulong Seed { get; }
    public long Tick { get; private set; }

    /// <summary>tg <c>world.time</c>: deciseconds since the world started.</summary>
    public float Time => Tick * TickLag;

    public Rng Rng { get; }
    public Scheduler Scheduler { get; }

    /// <summary>
    /// The registry of objects created by <c>Dm.@new</c> (docs/ai/specs/object-model.md section 6): id to object,
    /// weak entries, id-order iteration. Part of <see cref="ComputeStateHash"/>.
    /// </summary>
    public ObjectRegistry Objects { get; }

    /// <summary>
    /// DM <c>usr</c>, an implicit argument of every call (waiting-model.md section 10): an async proc's
    /// builder puts the caller's value back when the call expression returns, an awaiting caller resumes with
    /// the value it had when it began to wait, every scheduler entry (a woken sleeper, a spawned block) runs
    /// with the value that was ambient when it was scheduled, and each phase of a tick starts with null (a
    /// player command phase sets it per command). Typed <c>object</c> because the Kernel does not know the mob
    /// type.
    /// </summary>
    public object usr { get; set; }

    /// <summary>Called for every runtime error that reaches an isolation boundary (logged; the world carries on).</summary>
    public Action<Exception> RuntimeErrorSink { get; set; }

    /// <summary>
    /// A host-side measurement hook (M3 I1b): the world marks where a named piece of a tick begins and ends (the scheduler,
    /// each phase, each subsystem's fire and init), and the host times it with its own clock. The world never reads what
    /// it measures (D31), so a profiler changes nothing a tick computes. Null (the default): no calls.
    /// </summary>
    public IStepProfiler Profiler { get; set; }
    public int RuntimeErrorCount { get; private set; }

    // Completion trampoline of the awaiting chains of this world (see DmTaskState.Resume).
    internal int CompletionDepth;
    internal readonly Queue<(Action Continuation, object Usr)> DeferredCompletions = new();

    private long _nextId = 1;
    private readonly List<(string Name, Action<World> Run)> _phases = new();
    private readonly List<(string Name, StateHashContributor Hash)> _hashers = new();

    // Per-world extension slots (GetExtension), indexed by a per-type index; a slot holds CreatingMarker while
    // its factory runs.
    private object[] _extensions = Array.Empty<object>();
    private static readonly object CreatingMarker = new();
    private static int s_extensionSlotCount; // process-wide allocator of slot indexes, never game state

    public World(ulong seed = 1)
    {
        Seed = seed;
        Rng = new Rng(seed);
        Scheduler = new Scheduler(this);
        Objects = new ObjectRegistry();
        Rng.Reseed(Seed, Tick);
    }

    /// <summary>A stable identity for a new object (used by replication and state hashing).</summary>
    public long AllocateId() => _nextId++;

    public Scope Enter()
    {
        var prev = _current;
        _current = this;
        return new Scope(prev);
    }

    public readonly struct Scope : IDisposable
    {
        private readonly World _prev;
        internal Scope(World prev) => _prev = prev;
        public void Dispose() => _current = _prev;
    }

    /// <summary>
    /// Register a phase of the tick. Phases run in registration order after the first scheduler drain (player
    /// commands, later replication); each is an isolation boundary, starts with usr = null, and is followed by
    /// another drain so zero-delay work it issued (sleep(0), spawn(0)) resumes in the same tick.
    /// </summary>
    public void AddPhase(string name, Action<World> run) => _phases.Add((name, run));

    /// <summary>
    /// Register a contributor to <see cref="ComputeStateHash"/>. Contributors are hashed in ordinal order of their
    /// names (registration order among equal names), so the hash does not depend on the order in which lazily
    /// created extensions registered theirs; a contributor that adds nothing leaves no trace, not even its name, so
    /// state in its default (empty) form hashes like state that was never created. A contributor must only read.
    /// </summary>
    public void AddStateHasher(string name, StateHashContributor hash)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(hash);
        _hashers.Add((name, hash));
    }

    /// <summary>
    /// The world's instance of an extension type (docs/ai/specs/object-model.md section 10): <c>TgState</c>, the
    /// shim's <c>DmWorldState</c>. One instance per world and type, created by <paramref name="create"/> on first
    /// use and kept for the world's lifetime; later reads are an array access. Extensions are not hashed by the
    /// Kernel: an extension whose state matters registers its own contributor with <see cref="AddStateHasher"/>,
    /// which adds nothing while that state is the default, so that merely creating the extension (a host reading a
    /// value, installing a sink) never changes the hash.
    /// Pass a static lambda (<c>static w => new TgState(w)</c>) so no delegate is allocated per call.
    /// </summary>
    /// <exception cref="InvalidOperationException">The factory returned null or asked for its own extension.</exception>
    public T GetExtension<T>(Func<World, T> create) where T : class
    {
        int index = ExtensionSlot<T>.Index;
        object[] slots = _extensions;
        if ((uint)index < (uint)slots.Length && slots[index] is T existing) return existing;
        return CreateExtension(index, create);
    }

    private T CreateExtension<T>(int index, Func<World, T> create) where T : class
    {
        ArgumentNullException.ThrowIfNull(create);
        if (index >= _extensions.Length) Array.Resize(ref _extensions, Math.Max(index + 1, _extensions.Length * 2));
        if (ReferenceEquals(_extensions[index], CreatingMarker))
            throw new InvalidOperationException($"World.GetExtension<{typeof(T).Name}>: the factory asked for its own extension.");
        _extensions[index] = CreatingMarker;
        T value;
        try
        {
            value = create(this) ?? throw new InvalidOperationException($"World.GetExtension<{typeof(T).Name}>: the factory returned null.");
        }
        catch
        {
            _extensions[index] = null;
            throw;
        }
        _extensions[index] = value; // the factory may have created other extensions and resized the array
        return value;
    }

    // One slot index per extension type, assigned on first use; an immutable per-type cache.
    private static class ExtensionSlot<T>
    {
        internal static readonly int Index = Interlocked.Increment(ref s_extensionSlotCount) - 1;
    }

    /// <summary>
    /// Advance the world by one tick (50 ms of game time): Tick++, reseed the RNG, drain the scheduler (woken
    /// sleepers in FIFO order, including the Master Controller coroutine and so every subsystem fire), then
    /// each phase followed by a drain of the work it made due this tick.
    /// </summary>
    public void Step()
    {
        using var scope = Enter();
        var outerUsr = usr;
        Tick++;
        Rng.Reseed(Seed, Tick);
        usr = null;
        IStepProfiler profiler = Profiler;
        profiler?.Begin(SchedulerProfileName);
        Scheduler.RunDue(Tick);
        profiler?.End(SchedulerProfileName);
        for (int i = 0; i < _phases.Count; i++)
        {
            usr = null;
            profiler?.Begin(_phases[i].Name);
            try { _phases[i].Run(this); }
            catch (Exception e) { ReportRuntime(e); }
            profiler?.End(_phases[i].Name);
            usr = null;
            profiler?.Begin(SchedulerProfileName);
            Scheduler.RunDue(Tick);
            profiler?.End(SchedulerProfileName);
        }
        usr = outerUsr;
    }

    /// <summary>The name <see cref="Profiler"/> gets for the scheduler's runs of due sleepers and spawns.</summary>
    public const string SchedulerProfileName = "scheduler";

    public void Step(int ticks)
    {
        for (int i = 0; i < ticks; i++) Step();
    }

    /// <summary>
    /// Advance by whole ticks until at least <paramref name="deciseconds"/> of game time have passed (ceil;
    /// this is host/test timing, not DM sleep rounding).
    /// </summary>
    public void StepDeciseconds(float deciseconds)
    {
        long ticks = Scheduler.TicksCovering(deciseconds);
        if (ticks > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(deciseconds), deciseconds, "Too many ticks to step.");
        Step((int)ticks);
    }

    /// <summary>Report a runtime error at an isolation boundary: log it and let the world carry on.</summary>
    public void ReportRuntime(Exception e)
    {
        RuntimeErrorCount++;
        RuntimeErrorSink?.Invoke(e);
    }

    /// <summary>
    /// Hash of the whole simulation state: tick, RNG, scheduler queue, the object registry (forces a full GC
    /// when it has entries, <see cref="ObjectRegistry.HashInto"/>) and every registered contributor (in name order;
    /// one that adds nothing is skipped, <see cref="AddStateHasher"/>). Contributors registered while the hash runs
    /// are not part of it.
    /// </summary>
    public ulong ComputeStateHash()
    {
        var h = StateHasher.Create();
        h.Add(Seed);
        h.Add(Tick);
        h.Add(Rng.State);
        h.Add(_nextId);
        h.Add((ulong)RuntimeErrorCount);
        Scheduler.HashInto(ref h);
        Objects.HashInto(ref h);
        var order = new (string Name, int Index)[_hashers.Count];
        for (int i = 0; i < order.Length; i++) order[i] = (_hashers[i].Name, i);
        Array.Sort(order, static (a, b) =>
        {
            int byName = string.CompareOrdinal(a.Name, b.Name);
            return byName != 0 ? byName : a.Index.CompareTo(b.Index);
        });
        foreach (var (name, index) in order)
        {
            StateHasher before = h;
            h.Add(name);
            StateHasher named = h;
            _hashers[index].Hash(this, ref h);
            if (h.Value == named.Value) h = before; // the contributor added nothing: no trace, not even its name
        }
        return h.Value;
    }
}

/// <summary>
/// What <see cref="World.Profiler"/> is called with: <see cref="Begin"/> and <see cref="End"/> around a named piece of work,
/// properly nested (a subsystem's fire inside its phase). Host thread only; the host's clock, never the world's.
/// </summary>
public interface IStepProfiler
{
    void Begin(string name);
    void End(string name);
}
