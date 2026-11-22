using Anomaly.Protocol;

namespace Anomaly.Server;

/// <summary>
/// A token bucket in game time (D40): <see cref="RateLimit.PerSecond"/> tokens refill per second of ticks, at most
/// <see cref="RateLimit.Burst"/> are kept, and the bucket starts full. Counted in thousandths of a token so that the refill
/// per tick is exact for the rates of <see cref="Limits"/> and <see cref="ActionLimits"/>. Host thread only.
/// <para>
/// A stall credit (M3 I1b): game time stops while a tick overruns, but the clients keep sending in real time, so after a
/// stall of n ticks the host handles n ticks' worth of an honest client's commands at once. The host grants
/// <see cref="CreditStall"/> for the tick whose poll handles that backlog: n ticks of the rate, kept apart from the bucket
/// (above its burst) and gone at the next tick. A real flood sends far more than the rate and is still closed (D40).
/// </para>
/// </summary>
internal struct TokenBucket
{
    private readonly long _capacityMilli;
    private readonly long _perTickMilli;
    private long _milli;
    private long _tick;
    private long _creditMilli;
    private long _creditTick;

    public TokenBucket(RateLimit rate)
    {
        _capacityMilli = (long)rate.Burst * 1000;
        _perTickMilli = (long)rate.PerSecond * 1000 / Anomaly.Core.Kernel.World.Fps;
        _milli = _capacityMilli;
        _tick = 0;
        _creditMilli = 0;
        _creditTick = -1;
    }

    /// <summary>The tokens of <paramref name="delayedTicks"/> ticks that a stall held back, for the commands handled at <paramref name="tick"/> only.</summary>
    public void CreditStall(long tick, long delayedTicks)
    {
        _creditTick = tick;
        _creditMilli = Math.Max(0, delayedTicks) * _perTickMilli;
    }

    /// <summary>Takes one token at <paramref name="tick"/>; false when the bucket is empty (the command is dropped).</summary>
    public bool Take(long tick)
    {
        if (tick > _tick)
        {
            _milli = Math.Min(_capacityMilli, _milli + (tick - _tick) * _perTickMilli);
            _tick = tick;
        }
        if (_milli >= 1000)
        {
            _milli -= 1000;
            return true;
        }
        if (tick == _creditTick && _creditMilli >= 1000)
        {
            _creditMilli -= 1000;
            return true;
        }
        return false;
    }
}

/// <summary>
/// The per-kind rate limits of client actions (M2 P6, D40), on top of <see cref="Limits.ActionRate"/> that every action
/// takes a token from. They sit well above what a person does and far below what a script can send: a command over its rate
/// is dropped (not a strike), and a flood of drops closes the session like an InputFrame flood did in M1.
/// </summary>
public static class ActionLimits
{
    /// <summary>Rest, walk/run and combat mode: three keys a person presses a few times a minute.</summary>
    public static readonly RateLimit Toggle = new(4, 4);

    /// <summary>Clicks: tg's melee click delay is 8 ds (two clicks a second at most reach tg's own checks); menus share this bucket, and a burst of them must not lose clicks.</summary>
    public static readonly RateLimit Click = new(50, 50);

    /// <summary>Dev commands from a player's console and the dev menu's map tools (a click spawns, deletes or changes the air: the owner, 2026-10-13).</summary>
    public static readonly RateLimit Dev = new(8, 8);

    /// <summary>Version 5: the dev menu's gas probes (<see cref="Anomaly.Protocol.ActionKind.DevGasProbe"/>, twice a second while its gas overlay is on).</summary>
    public static readonly RateLimit DevProbe = new(4, 4);

    // ---- protocol version 4 (M4a contracts; the session buckets and their use are M4a P6's) ----

    /// <summary>Version 4: <see cref="Anomaly.Protocol.ActionKind.Keybinding"/> presses and releases (drop, equip, swap, activate, throw mode): a person taps them, a held key repeats nothing (tg's keyDown fires once).</summary>
    public static readonly RateLimit Keybinding = new(10, 5);

    /// <summary>
    /// Version 4: <see cref="Anomaly.Protocol.ActionKind.Hover"/>, sent when the crosshair's target changes. Over the rate a hover
    /// is dropped silently (not a strike: sweeping the view over a crowded room is normal); the server also runs at most one
    /// hover per session and tick, the last one (tg's <c>SSmouse_entered</c> keeps the last hovered atom per client).
    /// </summary>
    public static readonly RateLimit Hover = new(10, 5);

    /// <summary>The most actions a session may have waiting for their frame's tick (an action waits until the jitter buffer reaches its frame).</summary>
    public const int MaxPendingActions = 64;

    /// <summary>
    /// Consecutive ticks a session's last frame may repeat while no new one arrives before the host sends an idle frame in its
    /// place (half a second): a client that went silent must not run into a wall for the 5 seconds its connection takes to
    /// time out.
    /// </summary>
    public const int MaxRepeatedFrameTicks = 10;
}
