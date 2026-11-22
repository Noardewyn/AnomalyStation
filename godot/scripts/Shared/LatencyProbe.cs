using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>
/// Measures how long the player waits (WORKLOG question 1: own movement is not predicted in M1, so the latency is felt
/// directly). Two numbers, both in milliseconds:
/// <list type="bullet">
/// <item><description><b>input to ack</b>: from sending a motion frame to the first Delta whose
/// <c>LastInputSeq</c> covers it (the server applied it in the command phase of a tick): one round trip plus up to a tick.</description></item>
/// <item><description><b>input to motion</b>: from the frame that starts a walk (the held mask goes from none to some, the
/// own entity at rest) to the first frame in which the own entity is drawn moving by more than
/// <see cref="MotionThresholdTiles"/>: adds the server's pacing (tg's run delay, D98), the glide start and the render delay.
/// A walk that never moves (a wall in the way) is dropped after <see cref="MotionTimeoutSeconds"/>.</description></item>
/// </list>
/// Pure logic: the caller passes the monotonic time in seconds.
/// </summary>
public sealed class LatencyProbe
{
    /// <summary>The own entity counts as moving once it is drawn further than this from where it was at the key press (tiles).</summary>
    public const float MotionThresholdTiles = 0.05f;

    /// <summary>A probe that sees no motion after this long is dropped (the walk was blocked).</summary>
    public const double MotionTimeoutSeconds = 3.0;

    /// <summary>A walk starts a probe only when the own entity has been still for this long (a glide of the last walk would read as instant motion).</summary>
    public const double RestSeconds = 0.35;

    private const int MaxPending = 64;

    private readonly Queue<(uint Seq, double SentAt)> _pending = new();
    private readonly Rolling _ack = new();
    private readonly Rolling _motion = new();

    private HeldDirs _lastMask;
    private bool _active;
    private double _startedAt;
    private float _startX, _startY;
    private bool _havePose;
    private float _lastX, _lastY;
    private double _lastMovingAt = double.NegativeInfinity;

    /// <summary>A key press is being timed.</summary>
    public bool Measuring => _active;

    /// <summary>Input to ack: the latest and the mean of the last 16, with the count (0 when none yet).</summary>
    public (double LastMs, double MeanMs, int Count) Ack => _ack.Stats;

    /// <summary>Input to motion: the latest and the mean of the last 16, with the count.</summary>
    public (double LastMs, double MeanMs, int Count) Motion => _motion.Stats;

    /// <summary>Call when an input frame is sent. <paramref name="ownX"/> and <paramref name="ownY"/> are the own entity's drawn position.</summary>
    public void OnInputSent(uint seq, HeldDirs dirs, double nowSeconds, float ownX, float ownY)
    {
        if (_pending.Count >= MaxPending)
            _pending.Dequeue();
        _pending.Enqueue((seq, nowSeconds));

        if (dirs == HeldDirs.None)
        {
            _active = false;
        }
        else if (_lastMask == HeldDirs.None && !_active && nowSeconds - _lastMovingAt >= RestSeconds)
        {
            _active = true;
            _startedAt = nowSeconds;
            _startX = ownX;
            _startY = ownY;
        }
        _lastMask = dirs;
    }

    /// <summary>Call for each Delta (or whenever the replica's last applied input seq is read).</summary>
    public void OnAck(uint lastInputSeq, double nowSeconds)
    {
        while (_pending.Count > 0 && _pending.Peek().Seq <= lastInputSeq)
        {
            var sent = _pending.Dequeue();
            _ack.Add((nowSeconds - sent.SentAt) * 1000.0);
        }
    }

    /// <summary>Call every frame with the own entity's drawn position.</summary>
    public void OnOwnPose(float x, float y, double nowSeconds)
    {
        if (_havePose && (Math.Abs(x - _lastX) > 0.002f || Math.Abs(y - _lastY) > 0.002f))
            _lastMovingAt = nowSeconds;
        _havePose = true;
        _lastX = x;
        _lastY = y;

        if (!_active)
            return;
        if (Math.Max(Math.Abs(x - _startX), Math.Abs(y - _startY)) > MotionThresholdTiles)
        {
            _motion.Add((nowSeconds - _startedAt) * 1000.0);
            _active = false;
        }
        else if (nowSeconds - _startedAt > MotionTimeoutSeconds)
        {
            _active = false;
        }
    }

    /// <summary>Forgets what is pending (a new connection).</summary>
    public void Reset()
    {
        _pending.Clear();
        _active = false;
        _lastMask = HeldDirs.None;
        _havePose = false;
        _lastMovingAt = double.NegativeInfinity;
    }

    private sealed class Rolling
    {
        private readonly double[] _values = new double[16];
        private int _next;
        private int _count;
        private double _last;
        private int _total;

        public (double LastMs, double MeanMs, int Count) Stats
        {
            get
            {
                if (_count == 0)
                    return (0, 0, _total);
                double sum = 0;
                for (int i = 0; i < _count; i++)
                    sum += _values[i];
                return (_last, sum / _count, _total);
            }
        }

        public void Add(double value)
        {
            _values[_next] = value;
            _next = (_next + 1) % _values.Length;
            if (_count < _values.Length)
                _count++;
            _last = value;
            _total++;
        }
    }
}
