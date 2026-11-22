using System.Diagnostics;
using System.Threading;
using Anomaly.Core.Kernel;

namespace Anomaly.Server;

/// <summary>
/// Decides when the next tick may start. Pacing lives only in the host: the core has no wall clock (D31, DV3),
/// so how ticks are paced never changes what a tick computes.
/// </summary>
public interface ITickPacer
{
    /// <summary>Blocks until the next tick may start, or until <paramref name="stop"/> is cancelled.</summary>
    void WaitForNextTick(CancellationToken stop);

    /// <summary>How late the tick that the last <see cref="WaitForNextTick"/> released started, against its schedule, in milliseconds (0 when on time or fast-forward).</summary>
    double LastLatenessMs { get; }

    /// <summary>How many times the schedule restarted after an overrun of a whole tick or more.</summary>
    long Overruns { get; }
}

/// <summary>Runs ticks back to back, as fast as the machine allows (the default; tests, replays, measurements).</summary>
public sealed class FastForwardPacer : ITickPacer
{
    public static readonly FastForwardPacer Instance = new();

    public void WaitForNextTick(CancellationToken stop) { }

    public double LastLatenessMs => 0;

    public long Overruns => 0;
}

/// <summary>
/// Paces ticks at tg's rate: one tick every <c>world.tick_lag</c> (0.5 ds = 50 ms, 20 per second; tg
/// code/world.dm:19 sets <c>fps = 20</c>). A tick that starts late by less than one tick keeps the schedule, so
/// timer jitter does not slow the long-run rate. A tick that overran by a whole tick or more is not followed by a
/// burst of catch-up ticks: the schedule restarts from now, as in BYOND, where an overtime tick "still only counts
/// as one tick" and <c>world.time</c> is game time, not real time (BYOND reference, <c>world.time</c>).
/// </summary>
public sealed class RealtimePacer : ITickPacer
{
    private static readonly long TickLength = (long)Math.Round(Stopwatch.Frequency * (World.TickLag / 10.0));
    private static readonly long SpinMargin = Stopwatch.Frequency * 6 / 1000; // 6 ms

    private readonly Stopwatch _clock = new();
    private long _nextStart;

    /// <summary>How many times the schedule restarted after an overrun (a measure of lag).</summary>
    public long Overruns { get; private set; }

    public double LastLatenessMs { get; private set; }

    public void WaitForNextTick(CancellationToken stop)
    {
        if (!_clock.IsRunning)
        {
            _clock.Start();
            _nextStart = TickLength; // the first tick starts now
            LastLatenessMs = 0;
            return;
        }

        long scheduled = _nextStart;
        long now = _clock.ElapsedTicks;
        if (now < _nextStart)
        {
            // A timed wait oversleeps by a few milliseconds (measured on macOS: about 5 ms per tick), so it sleeps until
            // SpinMargin before the start and yields the rest: a tick never starts early and rarely more than a fraction
            // of a millisecond late, at the cost of yielding the CPU for about SpinMargin per tick.
            long coarse = _nextStart - SpinMargin - now;
            if (coarse > 0 && stop.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(Math.Floor(coarse * 1000.0 / Stopwatch.Frequency))))
            {
                LastLatenessMs = 0;
                return; // a stop request: the caller runs its shutdown tick now
            }
            while (_clock.ElapsedTicks < _nextStart && !stop.IsCancellationRequested)
                Thread.Yield();
        }
        else if (now - _nextStart >= TickLength)
        {
            Overruns++;
            _nextStart = now; // no catch-up burst
        }
        _nextStart += TickLength;
        LastLatenessMs = Math.Max(0, (_clock.ElapsedTicks - scheduled) * 1000.0 / Stopwatch.Frequency);
    }
}
