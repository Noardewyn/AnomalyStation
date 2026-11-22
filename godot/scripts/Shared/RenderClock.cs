namespace Anomaly.Client.Shared;

/// <summary>
/// The client's render time in server ticks (scope 2g: the latest server tick minus about 2 ticks, that is 100 ms, smoothed
/// against arrival jitter). The world is drawn as it was <see cref="RenderTick"/>, a number with a fraction that grows by
/// 20 per second of real time, so motion is smooth at any frame rate and does not stutter when a tick arrives a little
/// early or late.
/// <para>
/// How: every arriving tick T at local time t gives an offset <c>T - t * 20</c>. A tick that arrives late (jitter) gives a
/// smaller offset than one that arrives promptly, so the largest offset of the last <see cref="Window"/> arrivals is the
/// best estimate of "what the newest tick would be now at the fastest delivery". The target is that estimate minus
/// <see cref="DelayTicks"/>. The render time follows the target by changing its speed at most +-10 % (it never stops and
/// never runs backwards), and jumps to it only when the two are more than <see cref="SnapTicks"/> apart (a resume after a
/// stall, a server restart). Pure logic: the caller passes the monotonic time in seconds.
/// </para>
/// </summary>
public sealed class RenderClock
{
    /// <summary>The default render delay in ticks (100 ms at 20 TPS).</summary>
    public const double DefaultDelayTicks = 2.0;

    private readonly double[] _offsets;
    private int _count;
    private int _next;
    private double _renderTick;
    private double _lastNow;
    private bool _started;

    /// <param name="delayTicks">How many ticks behind the estimated latest tick the world is drawn.</param>
    /// <param name="window">How many arrivals the estimate looks back over (40 is 2 s at 20 TPS).</param>
    public RenderClock(double delayTicks = DefaultDelayTicks, int window = 40)
    {
        if (window < 1)
            throw new ArgumentOutOfRangeException(nameof(window));
        DelayTicks = delayTicks;
        _offsets = new double[window];
    }

    /// <summary>Ticks behind the estimated latest tick.</summary>
    public double DelayTicks { get; }

    /// <summary>The arrivals the estimate looks back over.</summary>
    public int Window => _offsets.Length;

    /// <summary>The render time is further than this from the target: it jumps instead of catching up (ticks).</summary>
    public double SnapTicks { get; init; } = 8.0;

    /// <summary>How fast the speed correction reacts: ticks per second of speed change for each tick of error.</summary>
    public double Gain { get; init; } = 2.0;

    /// <summary>The largest speed change, in ticks per second (2 is 10 % of 20).</summary>
    public double MaxSpeedChange { get; init; } = 2.0;

    /// <summary>
    /// How far the render time may run past the newest tick the client has data for (M2: the samples of capsule mobs are
    /// extrapolated by at most this much, so past it there is nothing new to show). The clock slows down as it approaches
    /// the limit and never passes it, so a late Delta is not followed by a jump (scope 1.6, the peak_speed hitch).
    /// </summary>
    public double MaxAheadTicks { get; init; } = 1.0;

    /// <summary>The slowest the clock runs when it is ahead of the data, as a factor of the tick rate.</summary>
    public double MinAheadSpeed { get; init; } = 0.1;

    /// <summary>True once a tick has arrived.</summary>
    public bool Started => _started;

    /// <summary>The server time to draw, in ticks with a fraction (meaningful once <see cref="Started"/>).</summary>
    public double RenderTick => _renderTick;

    /// <summary>The estimated newest server tick at the last update (ticks with a fraction).</summary>
    public double EstimatedTick => _count == 0 ? 0 : BestOffset() + _lastNow * TileSpace.TickRate;

    /// <summary>The estimated gap between the estimated newest tick and the render time, in ticks (the delay when settled).</summary>
    public double LagTicks => EstimatedTick - _renderTick;

    /// <summary>Records that the state of server tick <paramref name="tick"/> arrived at local time <paramref name="nowSeconds"/>.</summary>
    public void OnServerTick(long tick, double nowSeconds)
    {
        _offsets[_next] = tick - nowSeconds * TileSpace.TickRate;
        _next = (_next + 1) % _offsets.Length;
        if (_count < _offsets.Length)
            _count++;
        if (!_started)
        {
            _started = true;
            _lastNow = nowSeconds;
            _renderTick = Target(nowSeconds);
        }
    }

    /// <summary>
    /// Moves the render time to local time <paramref name="nowSeconds"/> (call once per frame, after the arrivals of the frame).
    /// <paramref name="dataTick"/> is the newest tick the client has data for (the replica's tick): when the render time is
    /// ahead of it the clock slows down (and stops at <see cref="MaxAheadTicks"/> past it); NaN: no such limit.
    /// </summary>
    public void Update(double nowSeconds, double dataTick = double.NaN)
    {
        if (!_started)
            return;
        double dt = nowSeconds - _lastNow;
        _lastNow = nowSeconds;
        if (dt <= 0)
            return;
        double target = Target(nowSeconds);
        // The error after the render time has advanced at the plain tick rate: only what is left is corrected by speeding up or slowing down.
        double error = target - (_renderTick + TileSpace.TickRate * dt);
        if (Math.Abs(error) > SnapTicks)
        {
            _renderTick = target;
            LimitToData(dataTick);
            return;
        }
        double speed = TileSpace.TickRate + Math.Clamp(error * Gain, -MaxSpeedChange, MaxSpeedChange);
        if (!double.IsNaN(dataTick))
        {
            double ahead = _renderTick - dataTick;
            if (ahead > 0)
                speed *= Math.Max(MinAheadSpeed, 1.0 - ahead / MaxAheadTicks);
        }
        _renderTick += speed * dt;
        LimitToData(dataTick);
    }

    private void LimitToData(double dataTick)
    {
        if (!double.IsNaN(dataTick) && _renderTick > dataTick + MaxAheadTicks)
            _renderTick = dataTick + MaxAheadTicks;
    }

    /// <summary>Forgets everything (a new connection).</summary>
    public void Reset()
    {
        _count = 0;
        _next = 0;
        _started = false;
        _renderTick = 0;
    }

    private double Target(double nowSeconds) => BestOffset() + nowSeconds * TileSpace.TickRate - DelayTicks;

    private double BestOffset()
    {
        double best = double.NegativeInfinity;
        for (int i = 0; i < _count; i++)
        {
            if (_offsets[i] > best)
                best = _offsets[i];
        }
        return best;
    }
}
