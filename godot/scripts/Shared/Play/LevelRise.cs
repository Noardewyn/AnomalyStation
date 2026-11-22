using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: D325 (multi-z). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>D325: the stairs' rise under a point, metres (DV370); false where there is no ramp.</summary>
public delegate bool RampHeight(float x, float y, int z, out float height);

/// <summary>
/// DEVIATION(DV371): how far above (or below) its level's floor a thing is drawn while it changes level, so no level change is a jump:
/// an announced ride (<see cref="RideState"/>, protocol 77: an elevator's car and riders, a ladder's climber) crosses the level over its
/// ticks; any other change of the drawn height by a level or more with the thing staying about where it was (a level change with no arc,
/// a ride's last bit of difference; a fall through open space is the server's arc, DV435) eases from the height it was drawn at to the new one as a fall does, under gravity. The stairs'
/// flight (DV370) rises continuously and is no jump. One per entity; <see cref="Rise"/> once per frame.
/// </summary>
public sealed class LevelRise
{
    /// <summary>Levels per second squared: a fall of one level takes 0.4 s.</summary>
    public const double Gravity = 12.5;

    /// <summary>A change of the drawn height in one frame larger than this (levels) is a jump the rise eases.</summary>
    public const double Jump = 0.2;

    /// <summary>A move farther than this (tiles) in one frame is a teleport: the thing is drawn where it now is.</summary>
    public const float Near = 1.5f;

    /// <summary>Ticks a ride is kept for after its end while the drawn level has not changed yet (the newest state leads the drawn one).</summary>
    public const double Grace = 40;

    private bool _known;
    private float _x, _y;
    private double _height; // the drawn height last frame, levels (level + ramp + rise)
    private double _ease; // the eased difference when it started, levels (drawn minus target)
    private double _easeAt; // the clock when it started
    private RideState _ride;
    private int _rideFrom; // the drawn level the ride started from
    private RideState _next; // the ride's next leg, announced before the drawn level changed for this one

    /// <summary>
    /// The height to add to the drawn level's floor, metres: <paramref name="ramp"/> is the stairs' rise under the thing (metres, DV370),
    /// <paramref name="ride"/> its newest announced ride, <paramref name="renderTick"/> the tick drawn, <paramref name="newestTick"/> the replica's newest, <paramref name="clock"/> real seconds.
    /// </summary>
    public float Rise(float x, float y, int z, float ramp, RideState ride, double renderTick, long newestTick, double clock)
    {
        float level = TileSpace.LevelHeight;
        if (!ride.IsNone && ride != _ride && ride != _next)
        {
            // the next leg of a ride over several levels can be the newest state before the drawn level has changed for this one
            if (!_ride.IsNone && z == _rideFrom && ride.EndTick - ride.Ticks >= _ride.EndTick - 1)
                _next = ride;
            else
                (_ride, _rideFrom, _next) = (ride, z, RideState.None);
        }
        if (!_ride.IsNone && z != _rideFrom)
        {
            // arrived: the level change itself; the next leg starts from here
            bool leg = !_next.IsNone && z == _rideFrom + _ride.Dz;
            (_ride, _rideFrom, _next) = leg ? (_next, z, RideState.None) : (RideState.None, z, RideState.None);
        }
        // ended well before its end (a climb given up, a rider who stepped off) or long over and never arrived: no ride any more
        if (!_ride.IsNone && ride.IsNone && _next.IsNone && (newestTick < _ride.EndTick - 2 || renderTick > _ride.EndTick + Grace))
            _ride = RideState.None;
        double announced = 0;
        if (!_ride.IsNone)
        {
            double start = _ride.EndTick - _ride.Ticks;
            announced = Math.Clamp((renderTick - start) / _ride.Ticks, 0, 1) * _ride.Dz;
        }
        double target = z + ramp / level + announced;
        bool near = _known && Math.Abs(x - _x) <= Near && Math.Abs(y - _y) <= Near;
        if (!near)
            _ease = 0;
        else if (Math.Abs(target + Eased(clock) - _height) > Jump)
        {
            _ease = _height - target;
            _easeAt = clock;
        }
        double offset = Eased(clock);
        _height = target + offset;
        (_x, _y, _known) = (x, y, true);
        return (float)((announced + offset) * level);
    }

    // The eased difference now: it falls away as a body falls, from rest, over sqrt(2 |d| / g).
    private double Eased(double clock)
    {
        if (_ease == 0)
            return 0;
        double t = clock - _easeAt;
        double total = Math.Sqrt(2 * Math.Abs(_ease) / Gravity);
        if (t >= total)
        {
            _ease = 0;
            return 0;
        }
        double left = t / total;
        return _ease * (1 - left * left);
    }
}
