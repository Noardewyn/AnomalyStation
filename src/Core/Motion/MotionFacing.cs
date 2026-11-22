namespace Anomaly.Core.Motion;

// Owner: M2 P4 (docs/ai/specs/locomotion.md section 4). Added beside the frozen contract types: the facing rule the bridge
// applies to tg's dir, engine-free so that a client or a bot can show the same facing.

/// <summary>
/// tg's <c>dir</c> from the view's yaw (scope 2b "Facing"): the yaw quantized to the four cardinal directions with M1's
/// 5 degree hysteresis (the client's <c>InputTracker</c>, D101): once a mob faces a direction it keeps it until the yaw is
/// more than 45 degrees plus <see cref="HysteresisYaw"/> away from that direction's bearing. Integer arithmetic only.
/// </summary>
public static class MotionFacing
{
    /// <summary>A quarter turn in yaw units (90 degrees).</summary>
    public const int QuarterTurn = 16384;

    /// <summary>5 degrees in yaw units (65536 per turn): 910.</summary>
    public const int HysteresisYaw = 910;

    /// <summary>The compass bearing of a cardinal BYOND direction (north 0, east 16384, south 32768, west 49152); 0 for anything else.</summary>
    public static ushort YawOf(byte dir) => dir switch
    {
        MotionDir.East => (ushort)QuarterTurn,
        MotionDir.South => (ushort)(2 * QuarterTurn),
        MotionDir.West => (ushort)(3 * QuarterTurn),
        _ => 0,
    };

    /// <summary>The cardinal direction nearest to <paramref name="yaw"/> (a bearing exactly between two goes clockwise).</summary>
    public static byte Nearest(ushort yaw) => (((yaw + QuarterTurn / 2) >> 14) & 3) switch
    {
        0 => MotionDir.North,
        1 => MotionDir.East,
        2 => MotionDir.South,
        _ => MotionDir.West,
    };

    /// <summary>
    /// The direction to face for <paramref name="yaw"/> when the mob faces <paramref name="current"/> now: the current one
    /// while the yaw stays within 45 degrees plus the hysteresis of it, else the nearest. A <paramref name="current"/> that is
    /// not a cardinal direction (0, a diagonal) gives the nearest.
    /// </summary>
    public static byte DirOf(ushort yaw, byte current)
    {
        if (current == MotionDir.North || current == MotionDir.South || current == MotionDir.East || current == MotionDir.West)
        {
            int off = (short)(ushort)(yaw - YawOf(current)); // signed difference, -32768 to 32767
            if (Math.Abs(off) <= QuarterTurn / 2 + HysteresisYaw)
                return current;
        }
        return Nearest(yaw);
    }
}
