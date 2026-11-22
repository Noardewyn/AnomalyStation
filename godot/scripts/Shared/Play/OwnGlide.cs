using Anomaly.Core.Motion;

namespace Anomaly.Client.Shared;

// Owner: M4c slice 7 (the own view in zero gravity, D146); smoothed after the space play test (2026-10-15). Engine-free: no Godot API here
// (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The own body in zero gravity (M4c slice 7, D146): tg's steps and drift move it on the server, and the client draws it gliding along the
/// tiles the server's states name (a slide's target, or where the capsule stands). The space play test (2026-10-15) found M4c's glide (BYOND's:
/// toward the newest tile at tg's <c>glide_size</c>, then still until the next step) jerked: tg's steps come 3 and 2 ticks apart for a
/// 2.4-tick delay, so the body stopped for a frame or two at every tile, a diagonal and a zigzag drift changed speed at every tile, and a
/// drift speeding up caught up in bursts. Now the drawn point travels along the server's path at an even speed:
/// <list type="bullet">
/// <item><description><b>The path</b>: the tiles in the order the states name them, as a polyline; a diagonal step (one move of two tiles in a
/// tick) is one straight segment, and the corners of a zigzag (a drift at a shallow angle) are rounded by averaging the path over
/// <see cref="SmoothTiles"/> either side of the drawn point.</description></item>
/// <item><description><b>The speed</b>: tg's own, the replicated <c>glide_size</c> (pixels a tick on each axis, as BYOND glides) along the
/// path's recent segments; without it the path's measured rate over its last steps; eased over <see cref="RateSeconds"/>.</description></item>
/// <item><description><b>A small buffer</b>: the point aims to be where the server's last step started plus the time since it at that speed,
/// less <see cref="MarginSeconds"/> for the network's jitter; so it is about one step behind the server's tile, as BYOND's glide is
/// (it starts at the step and ends a delay later), a step that comes a tick late or early bends the aim a little instead of stopping
/// the body, and the speed changes by at most <see cref="Accel"/> (speeding up) or <see cref="Brake"/> (slowing down).</description></item>
/// <item><description><b>Never past the server</b>: the point stops on the newest tile, braking in time when the steps end (the server
/// stopped it, or the key was let go).</description></item>
/// </list>
/// Nothing is predicted and nothing is corrected: the server stays authoritative. A target more than <see cref="SnapTiles"/> from the path's
/// end (a teleport) or on another level is taken at once. The caller passes the frame time.
/// </summary>
public sealed class OwnGlide
{
    /// <summary>A target further than this (tiles, on either axis) from the path's end is a jump, not a glide.</summary>
    public const float SnapTiles = 2.5f;

    /// <summary>The rate when tg's glide size is unknown and too few steps were seen: BYOND's default glide of 8 pixels per tick.</summary>
    public const float DefaultTilesPerTick = 8f / 32f;

    /// <summary>The buffer's margin for the network's jitter (seconds behind the server's step schedule).</summary>
    public const double MarginSeconds = 0.05;

    /// <summary>How fast the drawn point closes on its aim (seconds: the time constant).</summary>
    public const double CloseSeconds = 0.15;

    /// <summary>The largest change of the speed (tiles a second, each second) while gliding.</summary>
    public const double Accel = 60.0;

    /// <summary>The braking toward the path's end (tiles a second, each second): the body stops on the newest tile, nearly as BYOND's glide ends.</summary>
    public const double Brake = 400.0;

    /// <summary>The half width of the path's averaging (tiles along it): a zigzag's corners are rounded over it.</summary>
    public const double SmoothTiles = 0.5;

    /// <summary>How fast the reference speed follows a new one (seconds: the time constant).</summary>
    public const double RateSeconds = 0.1;

    /// <summary>The share of a step's lateness or earliness the schedule takes on at each step.</summary>
    public const double ScheduleShare = 0.35;

    /// <summary>A drawn point this far behind the path's end (tiles) jumps to a tile behind it (a stall of the link).</summary>
    public const double FarBehindTiles = 5.0;

    private readonly List<Vertex> _path = new();
    private readonly List<Grown> _steps = new();
    private double _s, _u, _now, _rate, _schedule;
    private float _serverRate;
    private readonly double _restSeconds;
    private readonly double _defaultTilesPerTick;
    private readonly double _marginSeconds;

    /// <summary>
    /// A glide; <paramref name="restSeconds"/> is the pause after which a step starts a new flight, <paramref name="defaultTilesPerTick"/> the speed
    /// before the steps' own rate is measured (BYOND's default glide for the own body; a slower thing, tg's singularity, passes its own pace),
    /// <paramref name="marginSeconds"/> how far behind the server's steps the point aims (<see cref="MarginSeconds"/>).
    /// </summary>
    public OwnGlide(double restSeconds = 1.0, double defaultTilesPerTick = DefaultTilesPerTick, double marginSeconds = MarginSeconds)
    {
        _restSeconds = restSeconds;
        _defaultTilesPerTick = defaultTilesPerTick;
        _marginSeconds = marginSeconds;
    }

    /// <summary>The steps' measured pace comes before the glide size once three steps are known (a thing tg moves by steps of its own pace: a pull, a throw, the singularity).</summary>
    public bool MeasuredFirst { get; init; }

    /// <summary>How fast the point may speed up (tiles a second, a second; <see cref="Accel"/> for the own body: a thrown thing takes its speed at once).</summary>
    public double Acceleration { get; init; } = Accel;

    /// <summary>The aim is also this share of a step's time behind the server (on top of the margin): a slow pace's step a little late does not stop the point.</summary>
    public double MarginShare { get; init; }

    private readonly record struct Vertex(double X, double Y, double Arc, double Cheb);

    // A step the server took: when its state came, the path's length before and after it, and its Chebyshev length after it.
    private readonly record struct Grown(double At, double From, double To, double Cheb);

    /// <summary>The body is drawn by the glide.</summary>
    public bool Active { get; private set; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public int Z { get; private set; }

    /// <summary>The drawn point's speed along the path (tiles a second).</summary>
    public float Speed => (float)_u;

    /// <summary>The path still ahead of the drawn point (tiles): how far behind the server's tile the body is drawn.</summary>
    public float Behind => _path.Count == 0 ? 0f : (float)(_path[^1].Arc - _s);

    /// <summary>Starts drawing the body at (x, y, z) (where it was drawn before).</summary>
    public void Start(float x, float y, int z)
    {
        Active = true;
        Z = z;
        _serverRate = 0f;
        _rate = 0;
        Restart(x, y);
    }

    public void Stop() => Active = false;

    private void Restart(double x, double y)
    {
        _path.Clear();
        _steps.Clear();
        _path.Add(new Vertex(x, y, 0, 0));
        _s = 0;
        _u = 0;
        _schedule = 0;
        X = (float)x;
        Y = (float)y;
    }

    /// <summary>A server state: its tile (a slide's target or the centre) is where the path goes next.</summary>
    public void Follow(in MotionState state)
    {
        if (!Active)
            return;
        bool sliding = state.Slide.Active;
        FollowTile(MotionMath.ToTiles(sliding ? state.Slide.TargetX : state.X), MotionMath.ToTiles(sliding ? state.Slide.TargetY : state.Y), state.Z);
        // The server slide's own rate (tiles a tick on the longer axis), for a glide size the client does not know.
        if (sliding && state.Slide.TicksLeft > 0)
            _serverRate = Math.Max(Math.Abs(state.Slide.TargetX - state.X), Math.Abs(state.Slide.TargetY - state.Y)) / (float)MotionMath.SubunitsPerTile / state.Slide.TicksLeft;
    }

    /// <summary>A server tile (<paramref name="tx"/>, <paramref name="ty"/> in tiles, level <paramref name="z"/>) is where the path goes next.</summary>
    public void FollowTile(double tx, double ty, int z)
    {
        if (!Active)
            return;
        Vertex end = _path[^1];
        double dx = tx - end.X, dy = ty - end.Y;
        if (z != Z || Math.Max(Math.Abs(dx), Math.Abs(dy)) > SnapTiles)
        {
            Z = z;
            Restart(tx, ty);
        }
        else if (Math.Abs(dx) > 1e-4 || Math.Abs(dy) > 1e-4)
        {
            double length = Math.Sqrt(dx * dx + dy * dy);
            _path.Add(new Vertex(tx, ty, end.Arc + length, end.Cheb + Math.Max(Math.Abs(dx), Math.Abs(dy))));
            // The schedule (where the server's steps begin, advancing at the reference speed) is pulled a share of the way to this step's start:
            // a step that comes a tick early or late bends the aim a little instead of stopping or rushing the body. The first step of a
            // flight sets it.
            bool resting = _steps.Count == 0 || _now - _steps[^1].At > _restSeconds;
            _schedule = resting ? end.Arc : _schedule + ScheduleShare * (end.Arc - _schedule);
            if (resting)
                _steps.Clear(); // a new flight: the rest before it is not its pace
            _steps.Add(new Grown(_now, end.Arc, end.Arc + length, _path[^1].Cheb));
            if (_steps.Count > 8)
                _steps.RemoveAt(0);
        }
    }

    /// <summary>Lets <paramref name="seconds"/> pass; <paramref name="glideSize"/> is tg's glide size of the own mob (pixels a tick, 0: unknown).</summary>
    public void Advance(double seconds, float glideSize)
    {
        if (!Active || !(seconds > 0))
            return;
        _now += seconds;
        double end = _path[^1].Arc;
        if (end - _s > FarBehindTiles)
        {
            _s = end - 1;
        }

        // The reference speed (tiles a second along the path), eased; zero once the steps have stopped coming.
        double reference = Reference(glideSize, out bool stopped, out double stepSeconds);
        _rate += (reference - _rate) * (1 - Math.Exp(-seconds / RateSeconds));

        // The aim: where the server's last step began, plus the time since its state came at the reference speed, less the margin; never past
        // the newest tile. With no step under way the aim is the newest tile.
        _schedule += _rate * seconds;
        double aim = end;
        if (stopped)
            _schedule = end;
        else
            aim = Math.Min(end, _schedule - _rate * (_marginSeconds + MarginShare * stepSeconds));
        double wanted = Math.Max(0, (stopped ? 0 : _rate) + (aim - _s) / CloseSeconds);
        double brakeCap = Math.Sqrt(2 * Brake * Math.Max(0, end - _s));
        wanted = Math.Min(wanted, brakeCap);
        double change = Math.Clamp(wanted - _u, -Acceleration * seconds, Acceleration * seconds);
        _u = Math.Min(_u + change, brakeCap);
        _s = Math.Min(end, _s + _u * seconds);
        if (end - _s < 1e-6 && stopped)
            _u = 0;

        (double x, double y) = Smoothed(_s, end);
        X = (float)x;
        Y = (float)y;
        Trim();
    }

    // tg's glide size along the recent path (BYOND glides each axis at it, so a diagonal tile is as long in time as a straight one), else the
    // path's measured rate over its last steps, else the server slide's rate, else BYOND's default. Stopped: no step for longer than one
    // and a half steps' time.
    private double Reference(float glideSize, out bool stopped, out double stepSeconds)
    {
        stepSeconds = 0;
        stopped = _steps.Count == 0;
        if (stopped)
            return 0;
        // The Euclidean length per Chebyshev tile of the recent path (1 straight, 1.41 diagonal, between for a zigzag).
        int first = Math.Max(0, _path.Count - 5);
        double cheb = _path[^1].Cheb - _path[first].Cheb, arc = _path[^1].Arc - _path[first].Arc;
        double ratio = cheb > 1e-6 ? arc / cheb : 1;
        double rate;
        bool measured = _steps.Count >= 3 && _steps[^1].At - _steps[0].At > 1e-3;
        if (MeasuredFirst && measured)
            rate = (_steps[^1].Cheb - _steps[0].Cheb) / (_steps[^1].At - _steps[0].At) * ratio;
        else if (glideSize > 0)
            rate = glideSize / 32.0 * MotionMath.TicksPerSecond * ratio;
        else if (measured)
            rate = (_steps[^1].Cheb - _steps[0].Cheb) / (_steps[^1].At - _steps[0].At) * ratio; // in tiles of the longer axis, as tg steps: a mix of straight and diagonal steps keeps its pace
        else
            rate = (_serverRate > 0 ? _serverRate : _defaultTilesPerTick) * MotionMath.TicksPerSecond * ratio;
        Grown last = _steps[^1];
        double expected = (last.To - last.From) / Math.Max(rate, 0.5);
        stepSeconds = expected;
        stopped = _now - last.At > 1.5 * expected + 0.05;
        return stopped ? 0 : rate;
    }

    // The path averaged over SmoothTiles either side of `s` (a triangle of five samples), the window narrowed near the ends so they are exact.
    private (double X, double Y) Smoothed(double s, double end)
    {
        double h = Math.Min(SmoothTiles, Math.Min(s - _path[0].Arc, end - s));
        if (h <= 1e-6)
            return PointAt(s);
        double x = 0, y = 0;
        ReadOnlySpan<double> offsets = stackalloc double[] { -1, -0.5, 0, 0.5, 1 };
        ReadOnlySpan<double> weights = stackalloc double[] { 1, 2, 3, 2, 1 };
        for (int i = 0; i < 5; i++)
        {
            (double px, double py) = PointAt(s + offsets[i] * h);
            x += px * weights[i];
            y += py * weights[i];
        }
        return (x / 9, y / 9);
    }

    private (double X, double Y) PointAt(double arc)
    {
        if (arc <= _path[0].Arc)
            return (_path[0].X, _path[0].Y);
        for (int i = 1; i < _path.Count; i++)
        {
            Vertex b = _path[i];
            if (arc <= b.Arc)
            {
                Vertex a = _path[i - 1];
                double k = b.Arc - a.Arc > 1e-9 ? (arc - a.Arc) / (b.Arc - a.Arc) : 1;
                return (a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k);
            }
        }
        return (_path[^1].X, _path[^1].Y);
    }

    // Drops the path behind the averaging window (two points are always kept).
    private void Trim()
    {
        while (_path.Count > 2 && _path[1].Arc < _s - SmoothTiles - 1e-3)
            _path.RemoveAt(0);
    }
}
