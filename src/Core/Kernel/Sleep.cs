using System.Runtime.CompilerServices;

namespace Anomaly.Core.Kernel;

/// <summary>Awaitable that suspends the calling proc until a later scheduler drain (DM <c>sleep</c>).</summary>
public readonly struct SleepAwaitable
{
    private readonly long _ticks;
    public SleepAwaitable(long ticks) => _ticks = ticks;

    public SleepAwaiter GetAwaiter() => new SleepAwaiter(_ticks);
}

public readonly struct SleepAwaiter : ICriticalNotifyCompletion, IDmAwaiter
{
    private readonly long _ticks;
    internal SleepAwaiter(long ticks) => _ticks = ticks;

    // sleep() always yields, even with a zero delay: other ready work runs first.
    public bool IsCompleted => false;

    // usr survives the sleep: the scheduler entry captures the ambient usr and re-imposes it on resumption.
    public void OnCompleted(Action continuation) => World.Current.Scheduler.EnqueueSleep(_ticks, continuation);
    public void UnsafeOnCompleted(Action continuation) => World.Current.Scheduler.EnqueueSleep(_ticks, continuation);

    /// <summary>Throws into a proc whose zero-delay chain the scheduler cut as a runaway loop.</summary>
    public void GetResult() => World.CurrentOrNull?.Scheduler.ThrowIfRunawayResume();
}

public static class Sleeping
{
    /// <summary>
    /// DM <c>sleep(ds)</c>: suspend for <paramref name="deciseconds"/>. A positive delay becomes
    /// max(1, floor(ds / tick_lag)) ticks (BYOND/OpenDream rounding); 0, negative or NaN resumes later in the
    /// same tick, after everything already queued.
    /// </summary>
    public static SleepAwaitable Sleep(float deciseconds) => new SleepAwaitable(Scheduler.TicksFromDeciseconds(deciseconds));

    /// <summary>Suspend for an exact number of ticks (0 or less: later in the same tick).</summary>
    public static SleepAwaitable SleepTicks(long ticks) => new SleepAwaitable(Math.Max(0, ticks));
}
