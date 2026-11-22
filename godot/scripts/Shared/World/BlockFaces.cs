namespace Anomaly.Client.Shared.World;

// Owner: D152 (block windows, the owner 2026-10-16). Engine-free. D121 drew a full-tile window, a grille and a girder as a thin slab
// that joins its neighbours with arms and shows ONE picture (tg's straight east-west run). D152 draws them as tile-sized blocks, like the walls, and lets
// tg's own smoothing kit frame every block: each side face of a window or girder block shows tg's lone pane (junction 0), the frame round all its edges. The client option block_windows draws D121's slabs instead.

/// <summary>The faces of a block piece (a full-tile window, a grille or a girder drawn as a block): which are hidden, and which smoothing state each shows.</summary>
public static class BlockFaces
{
    /// <summary>The shapes D152 draws as blocks.</summary>
    public static bool Applies(RenderKind shape) => shape is RenderKind.FullWindow or RenderKind.Grille or RenderKind.Girder or RenderKind.FalseWall;

    /// <summary>The block's side faces, in the order of their mesh parts (<see cref="PieceParts.BlockSouth"/> on), as the BYOND direction each looks out to.</summary>
    public static readonly byte[] Sides = { ByondDirConst.South, ByondDirConst.North, ByondDirConst.East, ByondDirConst.West };

    /// <summary>The mesh part of a side face (<paramref name="face"/> a cardinal BYOND direction) or of the top (0). The bottom is <see cref="PieceParts.BlockBottom"/>.</summary>
    public static int PartOf(byte face) => face switch
    {
        ByondDirConst.South => PieceParts.BlockSouth,
        ByondDirConst.North => PieceParts.BlockNorth,
        ByondDirConst.East => PieceParts.BlockEast,
        ByondDirConst.West => PieceParts.BlockWest,
        _ => PieceParts.BlockTop,
    };

    /// <summary>
    /// The junction of the picture a face shows, from the block's own smoothing junction (tg's, in its icon state). The picture is tg's top-down sprite stood up on
    /// the face: its left edge is the face's left as the viewer outside sees it, so the face's left neighbour is the picture's WEST bit and its right neighbour the EAST
    /// bit; the face has no neighbour above or below, so the frame shows along its top and bottom (tg's <c>window-12</c> for the middle of a run). The top (0) shows
    /// the junction itself, seen from above with north up, as tg draws it.
    /// </summary>
    public static int FaceJunction(int junction, byte face)
    {
        (int left, int right) = face switch
        {
            ByondDirConst.South => (Smoothing.West, Smoothing.East),
            ByondDirConst.North => (Smoothing.East, Smoothing.West),
            ByondDirConst.East => (Smoothing.South, Smoothing.North),
            ByondDirConst.West => (Smoothing.North, Smoothing.South),
            _ => (0, 0),
        };
        if (left == 0)
            return junction;
        return ((junction & left) != 0 ? Smoothing.West : 0) | ((junction & right) != 0 ? Smoothing.East : 0);
    }

    /// <summary>
    /// The junction kept to the cardinal neighbours <paramref name="joined"/> says are there (a diagonal stays only with both of its sides): tg's windows smooth only with
    /// windows (window.dm:754), so a run's end against a wall shows its frame even where a feed joined it to the wall.
    /// </summary>
    public static int Restrict(int junction, int joined)
    {
        int j = junction & joined & 15;
        if ((junction & Smoothing.NorthEast) != 0 && (j & (Smoothing.North | Smoothing.East)) == (Smoothing.North | Smoothing.East)) j |= Smoothing.NorthEast;
        if ((junction & Smoothing.SouthEast) != 0 && (j & (Smoothing.South | Smoothing.East)) == (Smoothing.South | Smoothing.East)) j |= Smoothing.SouthEast;
        if ((junction & Smoothing.SouthWest) != 0 && (j & (Smoothing.South | Smoothing.West)) == (Smoothing.South | Smoothing.West)) j |= Smoothing.SouthWest;
        if ((junction & Smoothing.NorthWest) != 0 && (j & (Smoothing.North | Smoothing.West)) == (Smoothing.North | Smoothing.West)) j |= Smoothing.NorthWest;
        return j;
    }

    /// <summary>
    /// A side face's edge at the inside of a corner (the owner, 2026-11-10: a corner is framed outside and inside): the block joins its neighbour at that side, but the
    /// cell in front of that neighbour holds a joined block too, so the neighbour's face there is covered and this face meets the next one's at a right angle; that
    /// edge keeps its frame. <paramref name="diagonals"/>: the diagonal cells that hold a joined block (<see cref="Smoothing.NorthEast"/> ...). An outer corner's
    /// edge joins nothing at its side, so it is framed anyway (no bent pane: the owner, 2026-11-10).
    /// </summary>
    public static int InnerCorners(int faceJunction, byte face, int junction, int diagonals)
    {
        if (face == 0 || diagonals == 0)
            return faceJunction;
        (int lx, int ly) = LeftOf(face);
        (int fx, int fy) = FrontOf(face);
        if ((junction & DirOf(lx, ly)) != 0 && (diagonals & DiagonalOf(lx + fx, ly + fy)) != 0) faceJunction &= ~Smoothing.West;
        if ((junction & DirOf(-lx, -ly)) != 0 && (diagonals & DiagonalOf(-lx + fx, -ly + fy)) != 0) faceJunction &= ~Smoothing.East;
        return faceJunction;
    }

    /// <summary>The tile offset a side face looks out to.</summary>
    public static (int Dx, int Dy) FrontOf(byte face) => face switch
    {
        ByondDirConst.North => (0, 1),
        ByondDirConst.South => (0, -1),
        ByondDirConst.East => (1, 0),
        ByondDirConst.West => (-1, 0),
        _ => (0, 0),
    };

    /// <summary>The smoothing junction's diagonal bit of a diagonal tile offset (0 for any other).</summary>
    public static int DiagonalOf(int dx, int dy) => (dx, dy) switch
    {
        (1, 1) => Smoothing.NorthEast,
        (1, -1) => Smoothing.SouthEast,
        (-1, -1) => Smoothing.SouthWest,
        (-1, 1) => Smoothing.NorthWest,
        _ => 0,
    };

    /// <summary>The diagonal cells around (x, y, z) that hold a block <paramref name="joins"/> says it joins, as <see cref="DiagonalOf"/> bits.</summary>
    public static int DiagonalsOf(int x, int y, int z, Func<int, int, int, bool> joins)
    {
        int bits = 0;
        foreach ((int dx, int dy) in new[] { (1, 1), (1, -1), (-1, -1), (-1, 1) })
        {
            if (joins(x + dx, y + dy, z)) bits |= DiagonalOf(dx, dy);
        }
        return bits;
    }

    /// <summary>
    /// The icon state a face of a smoothed block shows (<paramref name="joined"/>: the cardinal neighbours that may join it; <c>window-5</c>'s south face is
    /// <c>window-0</c>, the north face of an L's corner <c>window-4</c>: framed at the corner); null when the state carries no junction. <paramref name="diagonals"/>:
    /// <see cref="InnerCorners"/>.
    /// </summary>
    public static string FaceState(string iconState, byte face, int joined = Smoothing.North | Smoothing.South | Smoothing.East | Smoothing.West, int stack = 0, int diagonals = 0)
    {
        if (!WindowJunction.TryParse(iconState, out int junction))
            return null;
        junction = Restrict(junction, joined);
        int dash = iconState.LastIndexOf('-');
        if (dash < 1)
            return null;
        int faceJunction = Stacked(InnerCorners(FaceJunction(junction, face), face, junction, diagonals), face, stack);
        return Smoothing.StateOf(iconState.Substring(0, dash), faceJunction);
    }

    /// <summary>A side face's stack bits: the block joins the block straight above it (<see cref="Smoothing.Above"/>) or below it (<see cref="Smoothing.Below"/>), and the face's
    /// left or right neighbour joins its own block above or below (the corners).</summary>
    public const int UpLeft = 4, UpRight = 8, DownLeft = 16, DownRight = 32;

    /// <summary>
    /// A side face joined to the blocks above and below it (the owner, 2026-11-10: blocks join up and down as they do along a row, unless a floor lies between them):
    /// the face's top is tg's NORTH, its bottom SOUTH; a corner joins where its two sides do and the neighbour along the row is stacked too. The top (0) is not a side.
    /// </summary>
    public static int Stacked(int faceJunction, byte face, int stack)
    {
        if (face == 0 || stack == 0)
            return faceJunction;
        int j = faceJunction | ((stack & Smoothing.Above) != 0 ? Smoothing.North : 0) | ((stack & Smoothing.Below) != 0 ? Smoothing.South : 0);
        if ((j & (Smoothing.North | Smoothing.West)) == (Smoothing.North | Smoothing.West) && (stack & UpLeft) != 0) j |= Smoothing.NorthWest;
        if ((j & (Smoothing.North | Smoothing.East)) == (Smoothing.North | Smoothing.East) && (stack & UpRight) != 0) j |= Smoothing.NorthEast;
        if ((j & (Smoothing.South | Smoothing.West)) == (Smoothing.South | Smoothing.West) && (stack & DownLeft) != 0) j |= Smoothing.SouthWest;
        if ((j & (Smoothing.South | Smoothing.East)) == (Smoothing.South | Smoothing.East) && (stack & DownRight) != 0) j |= Smoothing.SouthEast;
        return j;
    }

    /// <summary>
    /// The junction of the picture a wall's side face shows (the owner, 2026-11-10: a built wall is framed where it meets what it does not join, along its row as up and
    /// down, as the full-tile windows are; D121 drew every side as the straight run), at a corner outside and inside too (<see cref="InnerCorners"/>, <paramref name="diagonals"/>).
    /// <paramref name="junction"/>: the wall's own tg junction;
    /// <paramref name="stack"/>: <see cref="Smoothing.Above"/> and <see cref="Smoothing.Below"/>; <paramref name="near"/>: the cardinal neighbours (BYOND bits) whose wall has a wall
    /// above it, and in the high four bits below it (the face's corners).
    /// </summary>
    public static int WallFace(int junction, byte face, int stack, int near, int diagonals = 0)
    {
        (int lx, int ly) = LeftOf(face);
        int left = DirOf(lx, ly), right = DirOf(-lx, -ly);
        int bits = stack & (Smoothing.Above | Smoothing.Below);
        if ((stack & Smoothing.Above) != 0 && (near & left) != 0) bits |= UpLeft;
        if ((stack & Smoothing.Above) != 0 && (near & right) != 0) bits |= UpRight;
        if ((stack & Smoothing.Below) != 0 && (near >> 4 & left) != 0) bits |= DownLeft;
        if ((stack & Smoothing.Below) != 0 && (near >> 4 & right) != 0) bits |= DownRight;
        return Stacked(InnerCorners(FaceJunction(junction, face), face, junction, diagonals), face, bits);
    }

    /// <summary>The BYOND direction bit of a cardinal tile offset.</summary>
    public static int DirOf(int dx, int dy) => dy > 0 ? ByondDirConst.North : dy < 0 ? ByondDirConst.South : dx > 0 ? ByondDirConst.East : dx < 0 ? ByondDirConst.West : 0;

    /// <summary>The tile offset of a side face's left neighbour as the viewer outside sees it (the right one is the opposite).</summary>
    public static (int Dx, int Dy) LeftOf(byte face) => face switch
    {
        ByondDirConst.South => (-1, 0),
        ByondDirConst.North => (1, 0),
        ByondDirConst.East => (0, -1),
        ByondDirConst.West => (0, 1),
        _ => (0, 0),
    };

    /// <summary>
    /// The side faces of the block at (x, y, z) that are hidden, as BYOND direction bits: a face against a block of its own kind that fills the next tile (a window against
    /// a window its own junction joins, as tg's smoothing does: an unanchored neighbour is not joined and keeps both faces; a grille against a grille; a girder against a
    /// girder), and a girder's or grille's face against a closed turf. A window's face against a wall is drawn (the owner: the run's end shows its whole frame through the
    /// glass); the renderer sets it a hair inside the tile so it never fights with the wall's side.
    /// </summary>
    public static byte Covered(int x, int y, int z, RenderKind shape, string iconState, Func<int, int, int, bool> closed, Func<int, int, int, bool> sameKind)
    {
        int junction = 0;
        bool hasJunction = shape != RenderKind.Grille && WindowJunction.TryParse(iconState, out junction);
        int covered = 0;
        foreach ((byte dir, int dx, int dy) in Around)
        {
            int nx = x + dx, ny = y + dy;
            if (closed(nx, ny, z) && shape != RenderKind.FullWindow)
                covered |= dir;
            else if (sameKind(nx, ny, z) && (!hasJunction || shape != RenderKind.FullWindow || (junction & dir) != 0))
                covered |= dir;
        }
        return (byte)covered;
    }

    private static readonly (byte Dir, int Dx, int Dy)[] Around =
    {
        (ByondDirConst.North, 0, 1), (ByondDirConst.South, 0, -1), (ByondDirConst.East, 1, 0), (ByondDirConst.West, -1, 0),
    };

    /// <summary>
    /// The junction of the picture a directional window's pane shows (D152 (block windows)): tg's directional windows do not smooth, so the client joins a pane to
    /// the panes it runs on, a pane on the same edge of the next tile along it. A pane of the other axis at a corner does not join it (the owner, 2026-10-29: each
    /// wall of panes is framed at its own ends, so a box of panes shows a vertical frame at its corners). The pane is built for the NORTH edge and turned
    /// (<see cref="BorderCorners.Ends"/>): its low end is the picture's left (WEST), its high end the right (EAST) as its front shows it; the frame runs along
    /// its top and bottom.
    /// </summary>
    public static int BorderWindowJunction(int x, int y, int z, byte face, Func<int, int, int, byte, bool> pane)
    {
        ((int X, int Y) low, (int X, int Y) high) = BorderCorners.Ends(x, y, face);
        int ax = (high.X - low.X) / 2, ay = (high.Y - low.Y) / 2;
        bool lowJoined = pane(x - ax, y - ay, z, face);
        bool highJoined = pane(x + ax, y + ay, z, face);
        return (lowJoined ? Smoothing.West : 0) | (highJoined ? Smoothing.East : 0);
    }
}
