using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Light;

// Engine-free (compiled into the client and, linked, into tests/Anomaly.Server.Tests/Client/Light).
// DEVIATION(DV62): tg draws a flashlight as a cone-shaped overlay sprite cast ahead of its holder, seen from above; the grid
// has that light at the server's cast (DV61). In first person the beam is a spot light too: the own one on the camera, every
// other one at its holder's hand along the way its holder looks (the replicated yaw of a capsule mob, else the way the
// server cast the light), or low over the floor for a flashlight lying there.

/// <summary>Where and how a flashlight's (a directional overlay light's) spot light is drawn.</summary>
public static class FlashlightBeam
{
    /// <summary>The spot's energy per unit of tg's light power.</summary>
    public const float EnergyPerPower = 3f;

    /// <summary>The spot's cone half-angle in degrees.</summary>
    public const float Angle = 32f;

    /// <summary>The height (metres above the floor) of a flashlight in a standing holder's hand.</summary>
    public const float HandHeight = 1.15f;

    /// <summary>The height of a flashlight in a lying holder's hand.</summary>
    public const float LyingHeight = 0.35f;

    /// <summary>The height of a flashlight lying on the floor.</summary>
    public const float FloorHeight = 0.12f;

    /// <summary>
    /// How far ahead of its holder's centre (in tiles) the beam starts, so that it does not light its holder from inside (the owner, 2026-10-29:
    /// 0.15, was 0.3, which showed the light apart from the one holding it).
    /// </summary>
    public const float Ahead = 0.15f;

    /// <summary>The downward tilt of another holder's beam in radians (their pitch is not replicated; a hand points a little down).</summary>
    public const float Tilt = 0.12f;

    /// <summary>Whether a light is a flashlight's beam: a directional overlay light (OVERLAY_LIGHT_DIRECTIONAL or OVERLAY_LIGHT_BEAM) that is on.</summary>
    public static bool IsBeam(in LightState light) =>
        !light.IsNone && light.On && light.Power > 0f
        && (light.System == TgLightMath.OverlayLightDirectional || light.System == TgLightMath.OverlayLightBeam);

    /// <summary>The spot's range in tiles: as far as tg's light reaches (its range past the cast of two tiles, plus one).</summary>
    public static float RangeTiles(in LightState light) => light.Range + 3f;

    /// <summary>The spot's energy.</summary>
    public static float Energy(in LightState light) => EnergyPerPower * light.Power;

    /// <summary>The share of white mixed into a flashlight's colour as the client draws it (the owner, 2026-10-17: a whiter beam than tg's #ffc44d).</summary>
    public const float Whiten = 0.65f;

    /// <summary>
    /// A flashlight's light as the client draws it, in the grid and as a spot: its colour mixed <see cref="Whiten"/> of the way
    /// to white. Other lights are returned as they are.
    /// </summary>
    public static LightState Presented(in LightState light)
    {
        if (light.System != TgLightMath.OverlayLightDirectional && light.System != TgLightMath.OverlayLightBeam)
            return light;
        uint c = light.Rgba;
        uint rgba = (Mix(c, 24) << 24) | (Mix(c, 16) << 16) | (Mix(c, 8) << 8) | (c & 255u);
        return light with { Rgba = rgba };
    }

    private static uint Mix(uint rgba, int shift)
    {
        uint channel = (rgba >> shift) & 255u;
        return (uint)MathF.Round(channel + (255 - channel) * Whiten) & 255u;
    }

    /// <summary>The spot's colour (0..1 channels) from the light's RGBA.</summary>
    public static (float R, float G, float B) Color(in LightState light) =>
        (((light.Rgba >> 24) & 255) / 255f, ((light.Rgba >> 16) & 255) / 255f, ((light.Rgba >> 8) & 255) / 255f);

    /// <summary>
    /// The beam's heading as a yaw byte (256 per turn, clockwise from north): the holder's own yaw when it is a capsule mob
    /// (<paramref name="hasYaw"/>), else the way the server cast the light (its centre is <c>-offset</c> tiles away: tg's
    /// <c>directional_offset</c> follows the parent's dir), else the light's direction, else the entity's.
    /// </summary>
    public static float YawByte(bool hasYaw, float yaw, in LightState light, byte entityDir)
    {
        if (hasYaw)
            return yaw;
        if (light.OffsetX != 0f || light.OffsetY != 0f)
        {
            double degrees = Math.Atan2(-light.OffsetX, -light.OffsetY) * 180.0 / Math.PI;
            if (degrees < 0)
                degrees += 360.0;
            return (float)(degrees * 256.0 / 360.0);
        }
        byte dir = light.Dir != 0 ? light.Dir : entityDir;
        return Icons.BillboardMath.YawOfDir(dir);
    }

    /// <summary>The unit heading (tiles: +x east, +y north) of a yaw byte.</summary>
    public static (float X, float Y) Heading(float yawByte)
    {
        double a = yawByte * (2 * Math.PI / 256.0);
        return ((float)Math.Sin(a), (float)Math.Cos(a));
    }

    /// <summary>
    /// The beam's origin: (<paramref name="x"/>, <paramref name="y"/>) in tiles and its height in metres. A carried flashlight
    /// (<paramref name="carried"/>) is at its holder's hand, <see cref="Ahead"/> tiles along the heading; a flashlight on
    /// the floor is at its tile, low.
    /// </summary>
    public static (float X, float Y, float Height) Origin(float x, float y, float yawByte, bool carried, bool lying)
    {
        if (!carried)
            return (x, y, FloorHeight);
        (float hx, float hy) = Heading(yawByte);
        return (x + hx * Ahead, y + hy * Ahead, lying ? LyingHeight : HandHeight);
    }
}
