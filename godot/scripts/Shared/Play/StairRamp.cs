namespace Anomaly.Client.Shared;

// D325 (multi-z). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// DEVIATION(DV370): how high a thing on tg's stairs is drawn and a walker's eye placed. tg's stairs are a floor picture: a mob walks onto
/// the stair tile on the floor and, leaving its far side in the stairs' dir, is moved to the level above (stairs.dm on_exit_stairs). The 3D
/// client draws the stairs as a flight that rises a level over their tile (the catalog's <c>"rest": "ramp"</c>), and lifts what stands on
/// the tile along it: from the floor at the edge opposite the dir to a level's height at the far edge, where the server's move puts the
/// walker on the floor of the level above, at the same height. Presentation only: the server's tiles and moves are tg's.
/// </summary>
public static class StairRamp
{
    /// <summary>
    /// How far up the flight a point is, 0 at its low edge to 1 at its high edge: <paramref name="x"/>, <paramref name="y"/> are tile
    /// coordinates (the tile's centre is its whole number), <paramref name="dir"/> BYOND's dir the stairs go up by (NORTH 1, SOUTH 2,
    /// EAST 4, WEST 8); a diagonal or no dir has no ramp (0).
    /// </summary>
    public static float Progress(byte dir, float x, float y, int tileX, int tileY)
    {
        float along = dir switch
        {
            1 => y - tileY,
            2 => tileY - y,
            4 => x - tileX,
            8 => tileX - x,
            _ => float.NaN,
        };
        return float.IsNaN(along) ? 0f : Math.Clamp(along + 0.5f, 0f, 1f);
    }
}
