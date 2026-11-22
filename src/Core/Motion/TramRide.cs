namespace Anomaly.Core.Motion;

// DEVIATION(DV403): a tram's trip as the server announces it (protocol version 80): tg moves the tram a tile at a time on SStransport,
// on a schedule known when the trip starts, so the clients draw it and its riders moving evenly and the own predictor carries a walker
// on it exactly as the server's bridge does. Shared by the server (the record beside the tram), the wire and the client.

/// <summary>
/// DV403: a tram's announced trip: the car's footprint before its first step (<see cref="X0"/>, <see cref="Y0"/>, <see cref="Width"/> by
/// <see cref="Height"/> tiles on the tram's level), its direction, and its <see cref="Steps"/> tile steps, the first at the end of the world
/// phase of <see cref="StartTick"/> (SStransport's fire) and each next one <see cref="TicksPerStep"/> ticks later. <see cref="None"/> (no steps)
/// for none.
/// </summary>
public readonly record struct TramRide(long StartTick, ushort TicksPerStep, ushort Steps, byte Dir, short X0, short Y0, byte Width, byte Height)
{
    public static readonly TramRide None = default;

    public bool IsNone => Steps == 0;

    /// <summary>The step's tile offset (BYOND's EAST is +x, NORTH is +y).</summary>
    public (int Dx, int Dy) Unit => ((Dir & MotionDir.East) != 0 ? 1 : (Dir & MotionDir.West) != 0 ? -1 : 0,
        (Dir & MotionDir.North) != 0 ? 1 : (Dir & MotionDir.South) != 0 ? -1 : 0);

    /// <summary>The steps taken by the end of tick <paramref name="tick"/> (its world phase included).</summary>
    public int StepsBy(long tick)
    {
        if (IsNone || tick < StartTick)
            return 0;
        return (int)Math.Min(Steps, (tick - StartTick) / Math.Max((ushort)1, TicksPerStep) + 1);
    }

    /// <summary>
    /// The steps taken by the time <paramref name="tick"/> (ticks with a fraction), evenly: each step drawn over the <see cref="TicksPerStep"/>
    /// ticks before the tick it lands in, so at a whole tick it is <see cref="StepsBy"/> of the tick a step lands in.
    /// </summary>
    public double StepsAt(double tick)
    {
        if (IsNone)
            return 0;
        double steps = (tick - StartTick) / Math.Max((ushort)1, TicksPerStep) + 1;
        return Math.Clamp(steps, 0, Steps);
    }

    /// <summary>Whether tile (<paramref name="x"/>, <paramref name="y"/>) is under the car after <paramref name="steps"/> steps.</summary>
    public bool Covers(int x, int y, int steps)
    {
        if (IsNone)
            return false;
        (int dx, int dy) = Unit;
        int left = X0 + dx * steps, bottom = Y0 + dy * steps;
        return x >= left && x < left + Width && y >= bottom && y < bottom + Height;
    }

    /// <summary>
    /// Whether the car steps in tick <paramref name="tick"/>, and the step's tile offset, when tile (<paramref name="x"/>, <paramref name="y"/>) is
    /// under it then (tg's group_move takes what stands on it before the capsules step: the carry of that tick).
    /// </summary>
    public bool CarriesAt(long tick, int x, int y, out int dx, out int dy)
    {
        dx = dy = 0;
        if (IsNone || tick < StartTick || (tick - StartTick) % Math.Max((ushort)1, TicksPerStep) != 0)
            return false;
        int before = StepsBy(tick - 1);
        if (before >= Steps || !Covers(x, y, before))
            return false;
        (dx, dy) = Unit;
        return true;
    }
}
