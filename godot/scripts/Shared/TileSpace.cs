namespace Anomaly.Client.Shared;

// Owner: M1 P6 (the Godot client). Engine-free: no Godot API here, so these files are compiled into the client and, linked,
// into tests/Anomaly.Server.Tests (Client/).

/// <summary>
/// How BYOND tile coordinates become metres in the client's 3D space (D33: a tile is 2 m; D119: the ceiling is 2 m too, <see cref="WorldScale.WallHeight"/>).
/// <para>
/// BYOND x grows to the east, y grows to the north, z is the level. Godot's X is east, Y is up and its default forward is
/// -Z, so the map's north is -Z: a tile (x, y) has its centre at X = (x - 0.5) * 2, Z = -(y - 0.5) * 2. A level is
/// <see cref="LevelHeight"/> above the one below. A mob's position is the tile centre at floor height: its capsule is
/// lifted by half its height by the renderer.
/// </para>
/// </summary>
public static class TileSpace
{
    /// <summary>Metres per tile (D33).</summary>
    public const float TileSize = 2f;

    /// <summary>Metres from one level's floor to the next: the wall height (D119: 2 m by default, the one setting <see cref="WorldScale.WallHeight"/>).</summary>
    public static float LevelHeight => WorldScale.WallHeight;

    /// <summary>The camera's height above the floor in first person standing (D157: 1.3 m, the one setting <see cref="WorldScale.EyeHeight"/>; 0.4 m lying).</summary>
    public static float EyeHeight => WorldScale.EyeHeight;

    /// <summary>A mob capsule's radius (M1 placeholder, D33: the final size is fixed on the first-person prototype).</summary>
    public const float CapsuleRadius = 0.4f;

    /// <summary>A mob capsule's height.</summary>
    public const float CapsuleHeight = 1.8f;

    /// <summary>The server's tick rate (tg world.fps 20; <c>Welcome.TickRate</c> says the same).</summary>
    public const double TickRate = 20.0;

    /// <summary>Godot X of a (possibly fractional) BYOND x.</summary>
    public static float WorldX(float tileX) => (tileX - 0.5f) * TileSize;

    /// <summary>Godot Z of a (possibly fractional) BYOND y (north is -Z).</summary>
    public static float WorldZ(float tileY) => -(tileY - 0.5f) * TileSize;

    /// <summary>Godot Y of the floor of BYOND level <paramref name="z"/> (levels start at 1).</summary>
    public static float WorldY(int z) => (z - 1) * LevelHeight;

    /// <summary>
    /// The Godot yaw (rotation about +Y in radians, counter-clockwise seen from above, 0 faces -Z = north) that faces the
    /// BYOND direction <paramref name="dir"/> (NORTH 1, SOUTH 2, EAST 4, WEST 8 and their sums; 0 or an impossible mix gives 0).
    /// </summary>
    public static float DirYaw(byte dir)
    {
        int dx = ((dir & 4) != 0 ? 1 : 0) - ((dir & 8) != 0 ? 1 : 0);
        int dy = ((dir & 1) != 0 ? 1 : 0) - ((dir & 2) != 0 ? 1 : 0);
        if (dx == 0 && dy == 0)
            return 0f;
        return (float)Math.Atan2(-dx, dy);
    }

    /// <summary>The name of a heading as <see cref="ViewInput.QuarterTurns"/> numbers it (0 north, 1 west, 2 south, 3 east).</summary>
    public static string HeadingName(int quarterTurns) => ((quarterTurns % 4 + 4) % 4) switch
    {
        0 => "NORTH",
        1 => "WEST",
        2 => "SOUTH",
        _ => "EAST",
    };
}
