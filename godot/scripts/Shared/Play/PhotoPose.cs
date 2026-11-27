namespace Anomaly.Client.Shared;

/// <summary>
/// DV685 (protocol version 99): where a photo looks. tg's camera frames a square of tiles around the target tile; the photographer's client
/// renders it from its own eye: toward the target tile's middle at half the wall height, wide enough that the frame's width fits at that
/// distance. A shot of the photographer's own tile looks along the view, tilted down. Engine-free (compiled into the tests too).
/// </summary>
public static class PhotoPose
{
    /// <summary>The narrowest and widest horizontal field of view, in degrees.</summary>
    public const float MinFovDegrees = 30f, MaxFovDegrees = 90f;

    /// <summary>How far a shot of the own tile tilts down, in radians (30 degrees).</summary>
    public const float OwnTilePitch = -0.5236f;

    /// <summary>The fewest pixels on a side the server takes.</summary>
    public const int MinSide = 16;

    /// <summary>
    /// The Godot yaw and pitch (radians) and the horizontal field of view (degrees) of a photo from the eye at (<paramref name="eyeX"/>,
    /// <paramref name="eyeY"/>, <paramref name="eyeZ"/>) looking along <paramref name="viewYaw"/>, of BYOND tile (<paramref name="x"/>,
    /// <paramref name="y"/>, <paramref name="z"/>) framed <paramref name="tilesX"/> tiles wide.
    /// </summary>
    public static (float Yaw, float Pitch, float FovDegrees) Of(float eyeX, float eyeY, float eyeZ, float viewYaw, int x, int y, int z, int tilesX)
    {
        float dx = TileSpace.WorldX(x) - eyeX;
        float dz = TileSpace.WorldZ(y) - eyeZ;
        float dy = TileSpace.WorldY(z) + TileSpace.LevelHeight * 0.5f - eyeY;
        float flat = MathF.Sqrt(dx * dx + dz * dz);
        float yaw, pitch, distance;
        if (flat < TileSpace.TileSize * 0.5f)
        {
            yaw = viewYaw;
            pitch = OwnTilePitch;
            distance = TileSpace.TileSize;
        }
        else
        {
            yaw = MathF.Atan2(-dx, -dz); // Godot's camera looks along -Z; north is -Z
            pitch = MathF.Atan2(dy, flat);
            distance = MathF.Sqrt(flat * flat + dy * dy);
        }
        float width = Math.Max(1, tilesX) * TileSpace.TileSize;
        float fov = 2f * MathF.Atan(width / 2f / distance) * 180f / MathF.PI;
        return (yaw, pitch, Math.Clamp(fov, MinFovDegrees, MaxFovDegrees));
    }

    /// <summary>The size a photo shrinks to when its PNG is over the cap: three quarters of each side, never under <see cref="MinSide"/> (null: it cannot shrink).</summary>
    public static (int Width, int Height)? Shrunk(int width, int height)
    {
        int w = width * 3 / 4, h = height * 3 / 4;
        return w < MinSide || h < MinSide ? null : (w, h);
    }
}
