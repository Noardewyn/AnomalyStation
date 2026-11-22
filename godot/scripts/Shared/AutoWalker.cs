using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>
/// The <c>--autowalk</c> behaviour for the soak and the screenshots: holds a random direction for a random time, and picks
/// another one at once when the own entity has not changed tile for <see cref="BlockedAfterSeconds"/> while a direction is
/// held (a wall, another mob in the way). Seeded, so a run repeats. It speaks absolute directions (it needs no camera).
/// Pure logic: the caller passes the monotonic time and the own tile.
/// </summary>
public sealed class AutoWalker
{
    private static readonly HeldDirs[] Cardinals = { HeldDirs.North, HeldDirs.South, HeldDirs.East, HeldDirs.West };

    private static readonly HeldDirs[] Diagonals =
    {
        HeldDirs.North | HeldDirs.East, HeldDirs.North | HeldDirs.West,
        HeldDirs.South | HeldDirs.East, HeldDirs.South | HeldDirs.West,
    };

    private readonly Random _random;
    private HeldDirs _current;
    private double _until;
    private bool _started;
    private int _lastX, _lastY;
    private double _lastChangeAt;

    public AutoWalker(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>No tile change for this long while walking means blocked (a step takes 0.15 s at tg's run delay, D98).</summary>
    public double BlockedAfterSeconds { get; init; } = 0.8;

    /// <summary>A walk lasts between these many seconds.</summary>
    public double MinHoldSeconds { get; init; } = 1.0;

    public double MaxHoldSeconds { get; init; } = 4.0;

    /// <summary>The directions to hold now (absolute).</summary>
    public HeldDirs Current => _current;

    /// <summary>How many times a direction was re-picked because it was blocked.</summary>
    public int BlockedRepicks { get; private set; }

    /// <summary>The directions to hold at local time <paramref name="nowSeconds"/> with the own entity on tile (<paramref name="ownX"/>, <paramref name="ownY"/>).</summary>
    public HeldDirs Update(double nowSeconds, int ownX, int ownY)
    {
        if (!_started)
        {
            _started = true;
            _lastX = ownX;
            _lastY = ownY;
            _lastChangeAt = nowSeconds;
            Pick(nowSeconds, avoid: HeldDirs.None);
            return _current;
        }
        if (ownX != _lastX || ownY != _lastY)
        {
            _lastX = ownX;
            _lastY = ownY;
            _lastChangeAt = nowSeconds;
        }
        bool blocked = _current != HeldDirs.None && nowSeconds - _lastChangeAt > BlockedAfterSeconds;
        if (blocked)
        {
            BlockedRepicks++;
            Pick(nowSeconds, avoid: _current);
        }
        else if (nowSeconds >= _until)
        {
            Pick(nowSeconds, avoid: HeldDirs.None);
        }
        return _current;
    }

    private void Pick(double now, HeldDirs avoid)
    {
        _lastChangeAt = now;
        double roll = _random.NextDouble();
        HeldDirs next;
        double hold;
        if (roll < 0.08 && avoid == HeldDirs.None)
        {
            next = HeldDirs.None;
            hold = 0.3 + _random.NextDouble() * 0.7;
        }
        else
        {
            HeldDirs[] set = roll < 0.78 ? Cardinals : Diagonals;
            do
            {
                next = set[_random.Next(set.Length)];
            }
            while (next == avoid);
            hold = MinHoldSeconds + _random.NextDouble() * (MaxHoldSeconds - MinHoldSeconds);
        }
        _current = next;
        _until = now + hold;
    }
}
