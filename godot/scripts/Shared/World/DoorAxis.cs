using System.Linq;

namespace Anomaly.Client.Shared.World;

// Owner: M3 R3. Engine-free. Which way a door stands (D121: "doors stand across the passage they close"). Until M3 R3 a door looked only at the wall-like cells
// east-west and north-south of it and fell back to NORTH, so a door between two hydroponics trays, a firelock in a row across a hall, or an airlock in a window gap
// stood turned the wrong way. The rule below scores BOTH axes from what the neighbours are.

/// <summary>What a neighbouring cell is to a door that decides which way to stand.</summary>
public enum CellClass : byte
{
    /// <summary>Space, the edge of the map, or anything that is neither a floor to walk on nor in the way: it says nothing.</summary>
    Void = 0,

    /// <summary>A floor a mob walks on (nothing dense stands on it).</summary>
    Open = 1,

    /// <summary>In the way: a closed turf, a full-tile window, a grille, a door, a dense thing that is not a mob (a hydroponics tray, a machine, a table).</summary>
    Blocking = 2,
}

/// <summary>
/// The door axis rule. A door closes a passage: its plane runs along the axis whose two neighbours BLOCK, across the other axis, whose two neighbours are OPEN (the
/// way through). <see cref="Choose"/> scores both candidate planes from the four neighbours: a plane along x scores a point for each blocking cell east and west and
/// for each open cell north and south, a plane along y the reverse; the higher score wins. A tie (a door in the open, or a corner) goes to the neighbouring door
/// that is already decided (a door beside a door shares its axis: a row of firelocks across a hall, a double airlock), else to the same scores two and three cells
/// further along each axis, else to NORTH (the plane along x), the old default. The face is the one <see cref="Placed.Face"/> holds for a piece that stands across
/// a corridor: NORTH when the plane runs along x (walls east and west of the door) and EAST when it runs along y.
/// </summary>
public static class DoorAxis
{
    /// <summary>The face of the plane of the door at (x, y, z) from its four neighbours alone: NORTH, EAST, or 0 for a tie.</summary>
    public static byte Near(int x, int y, int z, Func<int, int, int, CellClass> classify) => Decide(x, y, z, classify, 1, 1);

    /// <summary>The face from the cells two and three further out only: NORTH, EAST, or 0 for a tie.</summary>
    public static byte Far(int x, int y, int z, Func<int, int, int, CellClass> classify) => Decide(x, y, z, classify, 2, 3);

    private static byte Decide(int x, int y, int z, Func<int, int, int, CellClass> classify, int from, int to)
    {
        int alongX = 0, alongY = 0;
        for (int k = from; k <= to; k++)
        {
            alongX += Count(classify(x - k, y, z), CellClass.Blocking) + Count(classify(x + k, y, z), CellClass.Blocking)
                + Count(classify(x, y - k, z), CellClass.Open) + Count(classify(x, y + k, z), CellClass.Open);
            alongY += Count(classify(x, y - k, z), CellClass.Blocking) + Count(classify(x, y + k, z), CellClass.Blocking)
                + Count(classify(x - k, y, z), CellClass.Open) + Count(classify(x + k, y, z), CellClass.Open);
        }
        return alongX > alongY ? ByondDirConst.North : alongY > alongX ? ByondDirConst.East : (byte)0;
    }

    private static int Count(CellClass cell, CellClass wanted) => cell == wanted ? 1 : 0;

    /// <summary>
    /// The face of one door: its neighbours' scores, else the face of a neighbouring door that <paramref name="decidedFaceAt"/> knows (the first of west, east, south,
    /// north; it returns 0 for none), else the far scores, else the axis of the door's own <paramref name="ownDir"/> (tg's door assembly takes its builder's dir,
    /// stack.dm:469, and the airlock the assembly's, door_assembly.dm:350: the owner's play test, 2026-10-16, a door built in the open faced one way), else NORTH.
    /// <paramref name="decidedFaceAt"/> may be null.
    /// </summary>
    public static byte Choose(int x, int y, int z, Func<int, int, int, CellClass> classify, Func<int, int, int, byte> decidedFaceAt = null, byte ownDir = 0)
    {
        byte near = Near(x, y, z, classify);
        if (near != 0)
            return near;
        if (decidedFaceAt != null)
        {
            byte shared = FromNeighbours(x, y, z, decidedFaceAt);
            if (shared != 0)
                return shared;
        }
        byte far = Far(x, y, z, classify);
        if (far != 0)
            return far;
        return ownDir is ByondDirConst.East or ByondDirConst.West ? ByondDirConst.East : ByondDirConst.North;
    }

    private static byte FromNeighbours(int x, int y, int z, Func<int, int, int, byte> decidedFaceAt)
    {
        byte face = decidedFaceAt(x - 1, y, z);
        if (face == 0)
            face = decidedFaceAt(x + 1, y, z);
        if (face == 0)
            face = decidedFaceAt(x, y - 1, z);
        if (face == 0)
            face = decidedFaceAt(x, y + 1, z);
        return face;
    }

    /// <summary>
    /// The faces of every door of a map at once, in a fixed order (z, y, x) so that the answer never depends on the order the doors are listed in: the doors the
    /// neighbours decide first; then the tied ones take the face of a decided neighbouring door, repeatedly (a row of doors in the open takes the axis of the
    /// door at its end that is beside a wall); the ones left alone are decided by the far scores, one at a time (each may decide its neighbours in turn), else NORTH.
    /// </summary>
    public static Dictionary<(int X, int Y, int Z), byte> ResolveAll(IEnumerable<(int X, int Y, int Z)> doors, Func<int, int, int, CellClass> classify)
    {
        var faces = new Dictionary<(int X, int Y, int Z), byte>();
        var pending = new List<(int X, int Y, int Z)>();
        foreach ((int X, int Y, int Z) door in doors.Distinct().OrderBy(d => d.Z).ThenBy(d => d.Y).ThenBy(d => d.X))
        {
            byte near = Near(door.X, door.Y, door.Z, classify);
            if (near != 0)
                faces[door] = near;
            else
                pending.Add(door);
        }
        byte Known(int x, int y, int z) => faces.TryGetValue((x, y, z), out byte face) ? face : (byte)0;
        while (pending.Count > 0)
        {
            bool changed = false;
            for (int i = 0; i < pending.Count;)
            {
                (int x, int y, int z) = pending[i];
                byte shared = FromNeighbours(x, y, z, Known);
                if (shared == 0)
                {
                    i++;
                    continue;
                }
                faces[pending[i]] = shared;
                pending.RemoveAt(i);
                changed = true;
            }
            if (changed || pending.Count == 0)
                continue;
            // Nothing beside a decided door: the first one decides alone, by the cells further out.
            (int fx, int fy, int fz) = pending[0];
            byte far = Far(fx, fy, fz, classify);
            faces[pending[0]] = far != 0 ? far : ByondDirConst.North;
            pending.RemoveAt(0);
        }
        return faces;
    }
}
