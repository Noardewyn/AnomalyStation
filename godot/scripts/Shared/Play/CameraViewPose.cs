using System.Globalization;

namespace Anomaly.Client.Shared;

/// <summary>
/// M10 slice D (D268): where the camera console's second 3D view looks from. tg's security camera hangs on the wall its <c>dir</c> points
/// to (camera.dm:141 <c>get_turfs_to_mount_on</c>: the turf one step in its dir), so the view sits near that wall under the ceiling,
/// looks the other way into the tile and the room, and tilts down. Engine-free (compiled into the tests too).
/// </summary>
public static class CameraViewPose
{
    /// <summary>How far from the tile's centre toward its wall the lens is, in metres (the tile is 2 m: just off the wall).</summary>
    public const float WallOffset = 0.8f;

    /// <summary>The lens height as a share of the level's height (under the ceiling, where the camera sprite hangs).</summary>
    public const float HeightShare = 0.85f;

    /// <summary>How far the view tilts down, in radians (about 20 degrees).</summary>
    public const float Pitch = -0.35f;

    /// <summary>The Godot position, yaw and pitch of the view of a camera on BYOND tile (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) facing <paramref name="dir"/>.</summary>
    public static (float X, float Y, float Z, float Yaw, float Pitch) Of(int x, int y, int z, byte dir)
    {
        int dx = ((dir & 4) != 0 ? 1 : 0) - ((dir & 8) != 0 ? 1 : 0);
        int dy = ((dir & 1) != 0 ? 1 : 0) - ((dir & 2) != 0 ? 1 : 0);
        float wx = TileSpace.WorldX(x) + dx * WallOffset;
        float wz = TileSpace.WorldZ(y) - dy * WallOffset; // north is -Z
        float wy = TileSpace.WorldY(z) + TileSpace.LevelHeight * HeightShare;
        return (wx, wy, wz, TileSpace.DirYaw(Opposite(dir)), dir == 0 ? 0f : Pitch);
    }

    /// <summary>BYOND's opposite direction (north and south, east and west swapped).</summary>
    public static byte Opposite(byte dir)
        => (byte)(((dir & 1) << 1) | ((dir & 2) >> 1) | ((dir & 4) << 1) | ((dir & 8) >> 1));

    /// <summary>The entity id of a tg REF (<c>"[0x1f]"</c>), 0 for anything else.</summary>
    public static long IdOfRef(string reference)
    {
        if (reference == null || reference.Length < 5 || !reference.StartsWith("[0x", StringComparison.Ordinal) || reference[^1] != ']')
            return 0;
        return long.TryParse(reference.AsSpan(3, reference.Length - 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long id) ? id : 0;
    }
}
