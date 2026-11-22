namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
//
// How other entities are drawn (scope 2d, 1.6; replaces M1's tile glide, which is what produced the peak_speed hitch): a
// capsule mob sends its continuous position in every tick it moves, so the client draws it from those samples at the render
// time. A sample (T, p) says "after tick T the entity is at p"; the move of tick T happened during (T - 1, T]. So between two
// samples of consecutive ticks the entity goes in a straight line, and between two samples that are further apart it stands
// still until the last tick before the later one (it stopped, then started again), then moves.

/// <summary>One state of a capsule mob at a server tick: continuous position in 1/256 tile, level, yaw (256 per turn) and posture.</summary>
public readonly record struct PoseSample(long Tick, int PosX, int PosY, int Z, byte Yaw, byte Posture);

/// <summary>What the interpolation yields for one entity at one render time.</summary>
/// <param name="X">BYOND x with a fraction (a tile centre is a whole number).</param>
/// <param name="Y">BYOND y with a fraction.</param>
/// <param name="Z">The level.</param>
/// <param name="Yaw">256 per turn, compass, with a fraction (the caller rounds).</param>
/// <param name="Posture">tg's posture byte of the newest sample at or before the render time.</param>
/// <param name="Moving">The entity is between two samples of consecutive ticks (it is moving now).</param>
public readonly record struct SampledPose(float X, float Y, int Z, float Yaw, byte Posture, bool Moving);

/// <summary>Settings of the interpolation.</summary>
public sealed class InterpolationOptions
{
    /// <summary>The most an entity is extrapolated past the latest data the client has (ticks): a late Delta is bridged, a long stall holds.</summary>
    public double MaxExtrapolationTicks { get; init; } = 1.0;

    /// <summary>A move of more than this many tiles between two samples is a teleport: no motion is drawn between them.</summary>
    public float TeleportTiles { get; init; } = 2.5f;
}

/// <summary>The sample interpolation of other capsule mobs (a pure function of the samples and the render time).</summary>
public static class SampleInterpolator
{
    public const float SubunitsPerTile = 256f;

    /// <summary>
    /// The entity at <paramref name="renderTick"/>. <paramref name="newestFirst"/> are its kept samples, the newest first (at
    /// least one). <paramref name="latestTick"/> is the newest tick the client has heard of at all (the replica's tick): an
    /// entity whose newest sample is older than that has been standing still since, and one whose newest sample is that tick
    /// may have moved on in a Delta that is still on its way, so only then it is extrapolated, by at most
    /// <see cref="InterpolationOptions.MaxExtrapolationTicks"/>.
    /// </summary>
    public static bool TryEvaluate(ReadOnlySpan<PoseSample> newestFirst, double renderTick, long latestTick, InterpolationOptions options, out SampledPose pose)
    {
        int count = newestFirst.Length;
        if (count == 0)
        {
            pose = default;
            return false;
        }
        options ??= Default;
        PoseSample newest = newestFirst[0];
        if (renderTick >= newest.Tick)
        {
            float x = newest.PosX / SubunitsPerTile, y = newest.PosY / SubunitsPerTile;
            bool extrapolated = false;
            if (count >= 2 && newest.Tick == latestTick && renderTick > latestTick)
            {
                PoseSample before = newestFirst[1];
                if (newest.Tick - before.Tick == 1 && before.Z == newest.Z && !IsTeleport(before, newest, options))
                {
                    double extra = Math.Min(renderTick - latestTick, options.MaxExtrapolationTicks);
                    x += (float)((newest.PosX - before.PosX) * extra / SubunitsPerTile);
                    y += (float)((newest.PosY - before.PosY) * extra / SubunitsPerTile);
                    extrapolated = true;
                }
            }
            pose = new SampledPose(x, y, newest.Z, newest.Yaw, newest.Posture, extrapolated);
            return true;
        }

        // The pair of samples around the render time.
        for (int i = 0; i < count - 1; i++)
        {
            PoseSample later = newestFirst[i];
            PoseSample earlier = newestFirst[i + 1];
            if (earlier.Tick > renderTick)
                continue;
            // The move of `later` happens during (later.Tick - 1, later.Tick].
            double moveStart = later.Tick - 1;
            double u = renderTick <= moveStart ? 0.0 : (renderTick - moveStart);
            if (u > 1.0)
                u = 1.0;
            float yaw = LerpYaw(earlier.Yaw, later.Yaw, (float)u);
            if (later.Z != earlier.Z || IsTeleport(earlier, later, options))
            {
                // No motion between the two: the entity is where it was until the later sample's tick.
                pose = new SampledPose(earlier.PosX / SubunitsPerTile, earlier.PosY / SubunitsPerTile, earlier.Z, yaw, earlier.Posture, false);
                return true;
            }
            float px = (float)(earlier.PosX + (later.PosX - earlier.PosX) * u) / SubunitsPerTile;
            float py = (float)(earlier.PosY + (later.PosY - earlier.PosY) * u) / SubunitsPerTile;
            bool moving = u > 0.0 && (later.PosX != earlier.PosX || later.PosY != earlier.PosY);
            pose = new SampledPose(px, py, earlier.Z, yaw, earlier.Posture, moving);
            return true;
        }

        // Before the oldest sample: the entity has just appeared.
        PoseSample oldest = newestFirst[count - 1];
        pose = new SampledPose(oldest.PosX / SubunitsPerTile, oldest.PosY / SubunitsPerTile, oldest.Z, oldest.Yaw, oldest.Posture, false);
        return true;
    }

    private static readonly InterpolationOptions Default = new();

    private static bool IsTeleport(in PoseSample a, in PoseSample b, InterpolationOptions options)
    {
        float dx = Math.Abs(b.PosX - a.PosX) / SubunitsPerTile, dy = Math.Abs(b.PosY - a.PosY) / SubunitsPerTile;
        return Math.Max(dx, dy) > options.TeleportTiles;
    }

    /// <summary>The yaw (256 per turn) from <paramref name="a"/> to <paramref name="b"/> by the short way.</summary>
    public static float LerpYaw(byte a, byte b, float t)
    {
        int delta = ((b - a + 384) & 255) - 128; // -128 to 127
        float yaw = a + delta * t;
        if (yaw < 0f)
            yaw += 256f;
        else if (yaw >= 256f)
            yaw -= 256f;
        return yaw;
    }
}

/// <summary>
/// The visible-error limit of one drawn entity: the drawn position follows the target, and a jump of the target (a Delta
/// that was late, then caught up; a correction) is caught up at a bounded speed instead of in one frame, so no frame shows
/// a speed far above the entity's own. A small distance is closed at once; more than a teleport is a snap.
/// </summary>
public sealed class PoseSmoother
{
    private float _x, _y;
    private int _z;
    private float _lastTargetX, _lastTargetY;
    private bool _started;
    private float _targetSpeed;

    /// <summary>
    /// The slowest catch-up speed in tiles per second: above the fastest an entity runs (6.67 tiles per second at tg's run
    /// delay, 7.3 with the render clock 10 % fast), so that the limit never holds back a plain steady run, and low enough that
    /// a pop of the target is spread over a few frames.
    /// </summary>
    public float MinCatchUpTilesPerSecond { get; init; } = 9.0f;

    /// <summary>The catch-up speed relative to the target's own speed.</summary>
    public float CatchUpFactor { get; init; } = 1.4f;

    /// <summary>A gap above this many tiles snaps.</summary>
    public float SnapTiles { get; init; } = 2.5f;

    /// <summary>A gap below this many tiles is closed at once (0.1 of a pixel of a 2 m tile at 1080p is far smaller).</summary>
    public float CloseTiles { get; init; } = 0.004f;

    public float X => _x;

    public float Y => _y;

    /// <summary>Moves the drawn position toward the target over <paramref name="dtSeconds"/> and returns it.</summary>
    public (float X, float Y) Follow(float targetX, float targetY, int z, double dtSeconds)
    {
        if (!_started || z != _z || dtSeconds <= 0)
        {
            if (!_started || z != _z)
            {
                _x = targetX;
                _y = targetY;
                _z = z;
                _started = true;
                _lastTargetX = targetX;
                _lastTargetY = targetY;
                _targetSpeed = 0f;
            }
            return (_x, _y);
        }
        float tdx = targetX - _lastTargetX, tdy = targetY - _lastTargetY;
        float instant = MathF.Sqrt(tdx * tdx + tdy * tdy) / (float)dtSeconds;
        // The target's speed, smoothed: a one-frame spike of the target must not raise the limit it is meant to bound.
        _targetSpeed += (Math.Min(instant, 40f) - _targetSpeed) * (float)Math.Min(1.0, dtSeconds / 0.1);
        _lastTargetX = targetX;
        _lastTargetY = targetY;

        float dx = targetX - _x, dy = targetY - _y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance > SnapTiles || distance <= CloseTiles)
        {
            _x = targetX;
            _y = targetY;
            return (_x, _y);
        }
        float limit = MathF.Max(MinCatchUpTilesPerSecond, CatchUpFactor * _targetSpeed) * (float)dtSeconds;
        if (distance <= limit)
        {
            _x = targetX;
            _y = targetY;
        }
        else
        {
            _x += dx / distance * limit;
            _y += dy / distance * limit;
        }
        return (_x, _y);
    }

    /// <summary>Starts over at a position.</summary>
    public void Snap(float x, float y, int z)
    {
        _x = x;
        _y = y;
        _z = z;
        _started = true;
        _lastTargetX = x;
        _lastTargetY = y;
        _targetSpeed = 0f;
    }
}

/// <summary>
/// The per-frame speed of the entities that are drawn (the run summary's <c>p99_speed</c> and <c>peak_speed</c>, scope 1.6:
/// "report the p99 per-frame speed instead of the maximum"): for every entity and every frame, the distance it was drawn
/// to move divided by the frame's real time, in metres per second, teleports left out. One bad frame in 200,000 moves the
/// maximum and not the 99th percentile; a habit moves both.
/// </summary>
public sealed class FrameSpeedStats
{
    private const double BinWidth = 0.25;
    private const int Bins = 600; // up to 150 m/s; the rest goes into the last bin
    private readonly long[] _histogram = new long[Bins + 1];
    private readonly Dictionary<long, (float X, float Y, int Z, double At)> _last = new();
    private long _count;

    /// <summary>A jump of more than this many tiles in one frame is a teleport and no speed.</summary>
    public float TeleportTiles { get; init; } = 2.5f;

    /// <summary>Samples taken.</summary>
    public long Count => _count;

    /// <summary>The largest speed, m/s.</summary>
    public double Max { get; private set; }

    /// <summary>Records where an entity is drawn at local time <paramref name="nowSeconds"/> (call once per entity per frame).</summary>
    public void Observe(long id, float x, float y, int z, double nowSeconds)
    {
        if (_last.TryGetValue(id, out var last) && last.Z == z && nowSeconds - last.At > 0.0005)
        {
            double dtiles = Math.Sqrt((x - last.X) * (double)(x - last.X) + (y - last.Y) * (double)(y - last.Y));
            if (dtiles <= TeleportTiles)
            {
                double speed = dtiles * TileSpace.TileSize / (nowSeconds - last.At);
                int bin = (int)Math.Min(Bins, speed / BinWidth);
                _histogram[bin]++;
                _count++;
                if (speed > Max)
                    Max = speed;
            }
        }
        _last[id] = (x, y, z, nowSeconds);
    }

    /// <summary>Forgets an entity (it despawned).</summary>
    public void Forget(long id) => _last.Remove(id);

    /// <summary>The speed below which <paramref name="fraction"/> of the samples fall (the upper edge of its bin), m/s; 0 with no samples.</summary>
    public double Percentile(double fraction)
    {
        if (_count == 0)
            return 0;
        long target = (long)Math.Ceiling(fraction * _count);
        long seen = 0;
        for (int i = 0; i <= Bins; i++)
        {
            seen += _histogram[i];
            if (seen >= target)
                return (i + 1) * BinWidth;
        }
        return Max;
    }

    /// <summary>The 99th percentile, m/s.</summary>
    public double P99 => Percentile(0.99);
}
