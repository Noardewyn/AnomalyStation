namespace Anomaly.Core.Motion;

// Owner: the M3 contracts step (complete, frozen: docs/ai/specs/m3-ownership.md). The one definition of a blocked face between
// two tiles, which MotionStep (M3 P5's face blocking), the server's collision view and the client's predictor share.

/// <summary>
/// The faces of tiles (M3: <c>ON_BORDER_1</c> objects such as directional windows, windoors, railings). A face is the
/// border between a tile and its cardinal neighbour; it is blocked when either tile's <see cref="TileEdges"/> has the face
/// toward the other. The invariant P5's tests pin: for every view, tile and direction, the controller lets the capsule's
/// centre cross the border exactly when the neighbour is not solid and <see cref="IsFaceBlocked"/> is false, on the server
/// and in the predictor alike (bit-equal states for the same inputs and views).
/// </summary>
public static class TileFaces
{
    /// <summary>The face of a tile toward a cardinal <see cref="MotionDir"/> (None for anything else).</summary>
    public static TileEdges Toward(byte dir) => dir switch
    {
        MotionDir.North => TileEdges.North,
        MotionDir.South => TileEdges.South,
        MotionDir.East => TileEdges.East,
        MotionDir.West => TileEdges.West,
        _ => TileEdges.None,
    };

    /// <summary>The faces a BYOND direction names: its cardinal components (NORTHEAST 5 is the north and the east face).</summary>
    public static TileEdges OfDir(int byondDir) => (TileEdges)(byondDir & (MotionDir.North | MotionDir.South | MotionDir.East | MotionDir.West));

    /// <summary>The opposite face (north and south, east and west swapped).</summary>
    public static TileEdges Opposite(TileEdges faces)
    {
        TileEdges o = TileEdges.None;
        if ((faces & TileEdges.North) != 0) o |= TileEdges.South;
        if ((faces & TileEdges.South) != 0) o |= TileEdges.North;
        if ((faces & TileEdges.East) != 0) o |= TileEdges.West;
        if ((faces & TileEdges.West) != 0) o |= TileEdges.East;
        return o;
    }

    /// <summary>The cardinal neighbour of (x, y) toward <paramref name="dir"/> (the tile itself for a non-cardinal value).</summary>
    public static (int X, int Y) Neighbour(int x, int y, byte dir) => dir switch
    {
        MotionDir.North => (x, y + 1),
        MotionDir.South => (x, y - 1),
        MotionDir.East => (x + 1, y),
        MotionDir.West => (x - 1, y),
        _ => (x, y),
    };

    /// <summary>
    /// Whether the face between (x, y, z) and its neighbour toward the cardinal <paramref name="dir"/> is blocked: the tile's
    /// own face toward <paramref name="dir"/>, or the neighbour's face back. False for a non-cardinal <paramref name="dir"/>.
    /// </summary>
    public static bool IsFaceBlocked(ICollisionView view, int x, int y, int z, byte dir)
    {
        ArgumentNullException.ThrowIfNull(view);
        TileEdges leaving = Toward(dir);
        if (leaving == TileEdges.None)
            return false;
        (int nx, int ny) = Neighbour(x, y, dir);
        return (view.EdgesOf(x, y, z) & leaving) != 0 || (view.EdgesOf(nx, ny, z) & Opposite(leaving)) != 0;
    }
}
