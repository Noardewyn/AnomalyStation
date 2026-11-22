using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: values (written in wave 1b). Direction builtins that need no tg type (rules 1.5): turn() of a direction.
// turn() of a matrix or an icon belongs to the presentation seam and is planned (rules Appendix C).
public static partial class Dm
{
    // The eight directions counterclockwise from EAST, 45 degrees apart (BYOND ref /proc/turn: "An angle in degrees
    // (counterclockwise rotation)"; OpenDream DreamProcNativeRoot.NativeProc_turn: EAST 0, NORTHEAST 45, NORTH 90, ...).
    // Spans over constant data, not static arrays: no mutable static state (object-model.md section 10).
    private static ReadOnlySpan<float> TurnRing => new float[] { EAST, NORTHEAST, NORTH, NORTHWEST, WEST, SOUTHWEST, SOUTH, SOUTHEAST };

    // The direction an invalid Dir turns into, by a draw of 0..7 (OpenDream's order: North, South, East, West,
    // Northeast, Southeast, Southwest, Northwest).
    private static ReadOnlySpan<float> TurnRandom => new float[] { NORTH, SOUTH, EAST, WEST, NORTHEAST, SOUTHEAST, SOUTHWEST, NORTHWEST };

    /// <summary>
    /// DM <c>turn(Dir, Angle)</c> of a direction (BYOND ref <c>/proc/turn</c>): <paramref name="Dir"/> rotated
    /// counterclockwise by <paramref name="Angle"/> degrees (<c>turn(NORTH, 90) == WEST</c>, <c>turn(WEST, -90) ==
    /// NORTH</c>, <c>turn(NORTH, 45) == NORTHWEST</c>). "Only multiples of 45 are allowed for angles": another angle is
    /// "treated as the closest multiple of 45 to 0" (truncated, <c>turn(NORTH, 50) == NORTHWEST</c>). Dir is truncated
    /// to an integer; one that is not one of the eight directions ("such as 0, or something like UP or DOWN") gives "a
    /// random direction unless the angle is also 0", drawn from the world RNG (rules 4.13); with an angle of 0 it gives
    /// 0 (OpenDream; the reference does not say). The steps are counted in double, so a huge angle still turns by a
    /// multiple of 45; an angle that is not finite turns to EAST, as in OpenDream.
    /// </summary>
    public static float turn(float Dir, float Angle)
    {
        int start = float.IsFinite(Dir) ? TurnRing.IndexOf(MathF.Truncate(Dir)) : -1;
        if (start < 0)
            return Angle == 0 ? 0f : TurnRandom[World.Current.Rng.NextInt(0, 7)];
        double steps = Math.Truncate((double)Angle / 45.0);
        if (!double.IsFinite(steps)) return EAST;
        double index = (start + steps) % 8;
        if (index < 0) index += 8;
        return TurnRing[(int)index];
    }

    /// <summary>
    /// DM <c>turn(Dir, Angle)</c> with a value of any kind: a number is a direction (<see cref="turn(float, float)"/>,
    /// returned boxed); <c>turn(Matrix, Angle)</c> is a new matrix, the copy turned clockwise by Angle degrees (BYOND ref
    /// <c>/proc/turn</c>: "the matrix rotated clockwise"; OpenDream copies the matrix and turns it as <c>/matrix/proc/Turn</c>
    /// does; M4v slice 4, tg's tipped canister); <c>turn(Icon, Angle)</c> belongs to the presentation seam and is planned
    /// (icon values do not exist yet); anything else, null included, is a runtime error (OpenDream: "expected icon, matrix
    /// or integer").
    /// </summary>
    public static object turn(object Dir, float Angle)
    {
        if (TryGetNumber(Dir, out float dir)) return turn(dir, Angle);
        if (Dir is matrix m)
            return m.Copy().Turn(Angle);
        throw new DmRuntimeException("turn(): expected a direction, a matrix or an icon, got " + KindOf(Dir));
    }
}
