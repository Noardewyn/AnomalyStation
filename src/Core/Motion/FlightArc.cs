namespace Anomaly.Core.Motion;

// DEVIATION(DV416): a movable's free motion as the server announces it (protocol version 81): a thrown thing's ballistic arc, a thing
// falling or sliding after it, a thing the air pushes along the floor, and the point it came to rest at. The server moves the thing
// along the same closed form, so every client draws exactly the server's path from one segment until the next one replaces it.
// Shared by the server (FlightSeam), the wire and the client.

/// <summary>
/// DV416: one segment of a movable's free motion. From <see cref="StartTick"/> (server ticks with a fraction) the point is
/// (<see cref="X0"/>, <see cref="Y0"/>) tiles (BYOND coordinates: a tile's centre is a whole number) plus the velocity times the time,
/// on level <see cref="Z"/>, <see cref="Lift0"/> tile pixels above that level's floor (32 to a level) plus <see cref="VLift"/> times the
/// time, less half <see cref="Gravity"/> times its square. A horizontal speed under <see cref="Friction"/> slows down at that rate until
/// it stops. A segment with no speed and no gravity is a rest point: the thing lies there (it is drawn there until it is moved).
/// <see cref="None"/> (level 0) for none: the thing is drawn on its tile as before.
/// </summary>
public readonly record struct FlightArc(double StartTick, float X0, float Y0, int Z, float Lift0, float VX, float VY, float VLift, float Gravity, float Friction = 0f)
{
    /// <summary>Tile pixels to a tile, and from a level's floor to its ceiling.</summary>
    public const float PixelsPerTile = 32f;

    public static readonly FlightArc None = default;

    public bool IsNone => Z == 0;

    /// <summary>Whether the segment moves (it has a speed or falls); false for a rest point and for none.</summary>
    public bool IsMoving => !IsNone && (VX != 0f || VY != 0f || VLift != 0f || Gravity != 0f);

    /// <summary>A rest point at (<paramref name="x"/>, <paramref name="y"/>) tiles, <paramref name="lift"/> pixels up, from <paramref name="tick"/>.</summary>
    public static FlightArc Rest(double tick, float x, float y, int z, float lift) => new(tick, x, y, z, lift, 0f, 0f, 0f, 0f);

    /// <summary>The horizontal speed's share of the time that has passed <paramref name="dt"/> ticks in (friction: it stops at <see cref="StopTicks"/>).</summary>
    private double Travel(double dt)
    {
        if (Friction <= 0f)
            return dt;
        double stop = StopTicks;
        double t = Math.Min(dt, stop);
        double speed = Math.Sqrt((double)VX * VX + (double)VY * VY);
        return speed <= 0 ? 0 : t - Friction * t * t / (2 * speed);
    }

    /// <summary>The ticks after the start at which friction stops the horizontal motion (infinity without friction).</summary>
    public double StopTicks
    {
        get
        {
            if (Friction <= 0f)
                return double.PositiveInfinity;
            return Math.Sqrt((double)VX * VX + (double)VY * VY) / Friction;
        }
    }

    /// <summary>The point at <paramref name="tick"/> (never before the start): tiles, tiles, tile pixels above <see cref="Z"/>'s floor.</summary>
    public (double X, double Y, double Lift) At(double tick)
    {
        double dt = Math.Max(0, tick - StartTick);
        double travel = Travel(dt);
        return (X0 + VX * travel, Y0 + VY * travel, Lift0 + VLift * dt - 0.5 * Gravity * dt * dt);
    }

    /// <summary>The velocity at <paramref name="tick"/>: tiles a tick, tiles a tick, tile pixels a tick.</summary>
    public (double VX, double VY, double VLift) VelocityAt(double tick)
    {
        double dt = Math.Max(0, tick - StartTick);
        double share = 1;
        if (Friction > 0f)
        {
            double speed = Math.Sqrt((double)VX * VX + (double)VY * VY);
            share = speed <= 0 ? 0 : Math.Max(0, 1 - Friction * dt / speed);
        }
        return (VX * share, VY * share, VLift - Gravity * dt);
    }
}
