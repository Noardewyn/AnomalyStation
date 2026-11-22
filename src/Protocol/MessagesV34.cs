namespace Anomaly.Protocol;

// Version 34 (M7x slice A, D177, docs/ai/specs/protocol.md section 43): a shot hits where it lands.
// - A click and the new mouse actions (MouseDown, MouseDrag, MouseUp: BYOND's /client/MouseDown, MouseDrag and MouseUp, which tg's full
//   auto listens to) carry the camera's aim, the eye's point and the crosshair's direction (ShotAim). The server checks the eye against
//   the mob's own position and writes the aim into tg's click params as one port key, anomaly-aim (DV94).
// - The entity's Shot field (NetField.Shot): where a projectile's 3D line starts, its height there, its slope and its heading, so the
//   client draws a projectile, its tracers and its impacts at the shot's height (DV78 revised).
// - The CameraShake message's kind: tg's recoil_camera kick (Recoil), which the first-person client turns into a camera rotation (DV96).

/// <summary>
/// Version 34: the aim of a click or a mouse action, the first-person camera's ray: the eye's point and the crosshair's direction. The
/// server trusts the point only near the mob (DV94); the direction is the player's own.
/// </summary>
/// <param name="East">The eye's east coordinate in 1/256 tile (Anomaly.Core.Motion's units: a tile's centre is 256 times its x), 0 to <see cref="Limits.MaxAimCoordinate"/>.</param>
/// <param name="North">The eye's north coordinate in 1/256 tile.</param>
/// <param name="Up">The eye's height above its level's floor in millimetres.</param>
/// <param name="Yaw">The direction's compass bearing, 65536 per turn clockwise from north (as <see cref="MotionInput.Yaw"/>).</param>
/// <param name="Pitch">The direction's elevation, 65536 per turn, positive up, -16384 to 16384 (straight down to straight up).</param>
/// <param name="Seen">Version 88 (DV527): the render tick the client drew the world at when it aimed, in 1/16 tick (the replica's tick numbers); 0: not said.</param>
public readonly record struct ShotAim(int East, int North, ushort Up, ushort Yaw, short Pitch, long Seen = 0)
{
    /// <summary>The largest pitch either way: a quarter turn.</summary>
    public const short MaxPitch = 16384;

    /// <summary>Whether the values are inside the wire's ranges.</summary>
    public bool InRange => East >= 0 && East <= Limits.MaxAimCoordinate && North >= 0 && North <= Limits.MaxAimCoordinate && Pitch >= -MaxPitch && Pitch <= MaxPitch && Seen >= 0;

    /// <summary>Version 88: <see cref="Seen"/> in ticks.</summary>
    public double SeenTicks => Seen / 16.0;
}

/// <summary>
/// Version 34: where a projectile's 3D line runs (<see cref="NetField.Shot"/>), in tg's tile pixels (32 to a tile, heights too; a tile's centre
/// is 32 times its x). The height at a map point p is <c>OriginH + Slope * |p - origin|</c>.
/// </summary>
/// <param name="OriginX">The line's start, east, in absolute tile pixels (0 to <see cref="Limits.MaxShotCoordinate"/>).</param>
/// <param name="OriginY">The line's start, north.</param>
/// <param name="OriginH">The height at the start above the level's floor, 1/16 pixel.</param>
/// <param name="Slope">Height pixels per horizontal pixel, 1/4096.</param>
/// <param name="Angle">The heading, 65536 per turn clockwise from north (tg's projectile <c>angle</c>).</param>
public readonly record struct ShotState(int OriginX, int OriginY, short OriginH, short Slope, ushort Angle)
{
    /// <summary>The height at the start in tile pixels.</summary>
    public double HeightPixels => OriginH / 16.0;

    /// <summary>The slope in pixels per pixel.</summary>
    public double SlopeValue => Slope / 4096.0;

    /// <summary>The heading in degrees clockwise from north.</summary>
    public double AngleDegrees => Angle * 360.0 / 65536.0;

    /// <summary>The height in tile pixels at the absolute map point (<paramref name="x"/>, <paramref name="y"/>) in tile pixels.</summary>
    public double HeightAt(double x, double y)
    {
        double dx = x - OriginX, dy = y - OriginY;
        return HeightPixels + SlopeValue * Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The point of the line nearest to (<paramref name="x"/>, <paramref name="y"/>) in tile pixels: the origin plus the heading times the point's distance along it.</summary>
    public (double X, double Y) Project(double x, double y)
    {
        double a = AngleDegrees * Math.PI / 180.0;
        double ux = Math.Sin(a), uy = Math.Cos(a);
        double along = (x - OriginX) * ux + (y - OriginY) * uy;
        return (OriginX + ux * along, OriginY + uy * along);
    }
}

/// <summary>Version 34: what a <see cref="CameraShakeMessage"/>'s path is.</summary>
public enum CameraShakeKind : byte
{
    /// <summary>tg's <c>shake_camera</c>: the view jerks about and comes back (the eye moves).</summary>
    Shake = 0,

    /// <summary>tg's <c>recoil_camera</c> (a gun's kick): one step away from the shot and back with BYOND's back easing; the first-person client turns the view (DV96).</summary>
    Recoil = 1,
}

public sealed partial class CameraShakeMessage
{
    /// <summary>Version 34: the path's kind.</summary>
    public CameraShakeKind Kind { get; set; }
}
