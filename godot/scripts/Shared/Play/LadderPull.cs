using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: D325 (multi-z). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// DEVIATION(DV397): where a ladder's climber is drawn over the climb. tg climbs from any tile beside the ladder; in 3D a body climbing beside the
/// rails looked broken (the owner, 2026-11-11), so over an announced ride (DV371) that starts by a ladder the drawn place eases onto the spot before
/// its rails (<see cref="LadderBody.Front"/>), where the server puts the climber on the other level when the climb ends. A climb given up eases back.
/// One per entity; <see cref="Pull"/> once per frame.
/// </summary>
public sealed class LadderPull
{
    /// <summary>Seconds from the climb's start (as it is first drawn) to the ladder.</summary>
    public const double InSeconds = 0.3;

    /// <summary>Seconds to ease back to the body's own place after a climb ended without arriving.</summary>
    public const double OutSeconds = 0.25;

    private RideState _ride;
    private int _from;
    private float _toX, _toY;
    private double _share;

    /// <summary>
    /// Where to draw a thing whose own place is (<paramref name="x"/>, <paramref name="y"/>) on level <paramref name="z"/> (BYOND tiles), <paramref name="ride"/> its
    /// newest announced ride, <paramref name="ladderAt"/> whether a ladder stands on a tile, <paramref name="dt"/> real seconds since the last frame.
    /// </summary>
    public (float X, float Y) Pull(float x, float y, int z, RideState ride, double renderTick, long newestTick, double dt, Func<int, int, int, bool> ladderAt)
    {
        if (!ride.IsNone && ride != _ride && TryLadder(x, y, z, ladderAt, out float lx, out float ly))
            (_ride, _from, _toX, _toY) = (ride, z, lx, ly);
        // as LevelRise: held until the drawn level changes, or the ride clearly ended without arriving
        bool holding = !_ride.IsNone && z == _from
            && !(ride.IsNone && (newestTick < _ride.EndTick - 2 || renderTick > _ride.EndTick + LevelRise.Grace));
        // by the frames' clock, not the ride's ticks: a ride first drawn a few ticks after its start still eases in from where the body is
        if (holding)
            _share = Math.Min(1, _share + Math.Max(0, dt) / InSeconds);
        else
        {
            _ride = RideState.None;
            _share = Math.Max(0, _share - Math.Max(0, dt) / OutSeconds);
        }
        if (_share <= 0)
            return (x, y);
        float s = (float)(_share * _share * (3 - 2 * _share));
        return (x + (_toX - x) * s, y + (_toY - y) * s);
    }

    // The nearest spot before a ladder on the thing's tile or one beside it (tg's in_range).
    private static bool TryLadder(float x, float y, int z, Func<int, int, int, bool> ladderAt, out float lx, out float ly)
    {
        lx = ly = 0;
        if (ladderAt == null)
            return false;
        int cx = (int)MathF.Round(x), cy = (int)MathF.Round(y);
        float best = float.MaxValue;
        for (int ty = cy - 1; ty <= cy + 1; ty++)
        {
            for (int tx = cx - 1; tx <= cx + 1; tx++)
            {
                if (!ladderAt(tx, ty, z))
                    continue;
                (int fx, int fy) = LadderBody.Front(tx, ty, MotionMath.Quantize(MotionMath.DefaultRadius));
                float sx = MotionMath.ToTiles(fx), sy = MotionMath.ToTiles(fy);
                float d = (sx - x) * (sx - x) + (sy - y) * (sy - y);
                if (d < best)
                    (best, lx, ly) = (d, sx, sy);
            }
        }
        return best < float.MaxValue;
    }
}
