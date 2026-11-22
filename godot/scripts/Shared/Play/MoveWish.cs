namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The movement keys and the view yaw as the world-space wish vector of a <c>MotionInput</c> (scope 2b: "the client turns
/// yaw and keys into the world-space wish vector, so the server does no trigonometry"). East is +x, north is +y, the unit
/// of the vector is full speed; a diagonal is as fast as a straight line (DV15: the same in every direction). The
/// client's own trigonometry is plain double math: only the controller's step has to be bit-exact (Anomaly.Core.Motion).
/// <para>
/// Yaw is Godot's, as <see cref="ViewInput"/> and <c>CameraRig</c> keep it: radians about +Y, counter-clockwise seen from
/// above, 0 faces north, +90 degrees faces west. The wire's yaw is a compass bearing (a <c>ushort</c>, 65536 per turn,
/// clockwise from north), which is the negative of it.
/// </para>
/// </summary>
public static class MoveWish
{
    /// <summary>The wish of <paramref name="keys"/> (opposite keys cancel, as tg's own sanity check does) for a view facing <paramref name="godotYawRadians"/>: east and north components, together at most 1 long.</summary>
    public static (float East, float North) FromKeys(MoveKeys keys, double godotYawRadians)
    {
        keys = ViewInput.Cancel(keys);
        double forward = ((keys & MoveKeys.Forward) != 0 ? 1 : 0) - ((keys & MoveKeys.Back) != 0 ? 1 : 0);
        double right = ((keys & MoveKeys.Right) != 0 ? 1 : 0) - ((keys & MoveKeys.Left) != 0 ? 1 : 0);
        if (forward == 0 && right == 0)
            return (0f, 0f);
        double sin = Math.Sin(godotYawRadians), cos = Math.Cos(godotYawRadians);
        // Facing: (-sin, cos) in (east, north); to its right: (cos, sin).
        double east = -sin * forward + cos * right;
        double north = cos * forward + sin * right;
        double length = Math.Sqrt(east * east + north * north);
        if (length > 1.0)
        {
            east /= length;
            north /= length;
        }
        return ((float)east, (float)north);
    }

    /// <summary>A component of the wish in -1 to 1 as the wire's <c>sbyte</c> (-127 to 127).</summary>
    public static sbyte ToWire(float component)
    {
        if (float.IsNaN(component))
            return 0;
        return (sbyte)Math.Clamp((int)Math.Round(component * 127.0, MidpointRounding.AwayFromZero), -127, 127);
    }

    /// <summary>The wire's compass bearing of a Godot yaw.</summary>
    public static ushort CompassYaw(double godotYawRadians)
    {
        if (double.IsNaN(godotYawRadians) || double.IsInfinity(godotYawRadians))
            return 0;
        double turns = -godotYawRadians / (2 * Math.PI);
        turns -= Math.Floor(turns);
        return (ushort)((int)Math.Round(turns * 65536.0, MidpointRounding.AwayFromZero) & 0xFFFF);
    }

    /// <summary>A compass bearing as a Godot yaw in radians, -pi to pi.</summary>
    public static double GodotYaw(ushort compass)
    {
        double yaw = -compass * (2 * Math.PI / 65536.0);
        return Math.IEEERemainder(yaw, 2 * Math.PI);
    }

    /// <summary>The replicated yaw byte (256 per turn, compass) of a 16-bit bearing.</summary>
    public static byte YawByte(ushort compass) => (byte)(compass >> 8);

    /// <summary>The 16-bit bearing the middle of a yaw byte stands for.</summary>
    public static ushort FromYawByte(byte yaw) => (ushort)((yaw << 8) | 0x80);
}

/// <summary>
/// Turns what the keys did over one tick into the frame of that tick. The client sends one frame per tick of its input
/// clock, and a key pressed in the middle of a tick must still show on the next rendered frame and must not make the view
/// jump back at the tick's end. So the frame carries the time-weighted wish: a key held for 40 per cent of the tick is 0.4 of
/// full speed (the wire's wish is analog), and the camera's render-only partial step of the tick in progress is the same
/// integral so far (<see cref="Peek"/>): at the tick's end it equals the step the frame makes.
/// </summary>
public sealed class WishIntegrator
{
    private double _east;
    private double _north;

    /// <summary>Adds a wish held for <paramref name="seconds"/>.</summary>
    public void Add(float east, float north, double seconds)
    {
        if (seconds <= 0)
            return;
        _east += east * seconds;
        _north += north * seconds;
    }

    /// <summary>The integral so far over <paramref name="periodSeconds"/> (the tick's length at the clock's rate), at most one long, as the wire's components.</summary>
    public (sbyte East, sbyte North) Peek(double periodSeconds)
    {
        (float east, float north) = Average(periodSeconds);
        return (MoveWish.ToWire(east), MoveWish.ToWire(north));
    }

    /// <summary>As <see cref="Peek"/>, in -1 to 1.</summary>
    public (float East, float North) Average(double periodSeconds)
    {
        if (periodSeconds <= 0)
            return (0f, 0f);
        double east = _east / periodSeconds, north = _north / periodSeconds;
        double length = Math.Sqrt(east * east + north * north);
        if (length > 1.0)
        {
            east /= length;
            north /= length;
        }
        return ((float)east, (float)north);
    }

    /// <summary>The frame's wish for a tick of <paramref name="periodSeconds"/>, and a fresh start.</summary>
    public (sbyte East, sbyte North) Take(double periodSeconds)
    {
        (sbyte east, sbyte north) = Peek(periodSeconds);
        Reset();
        return (east, north);
    }

    /// <summary>Forgets what was added.</summary>
    public void Reset()
    {
        _east = 0;
        _north = 0;
    }
}
