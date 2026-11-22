using System;
using System.Numerics;

namespace Anomaly.Client.Shared.World;

// Owner: M3 P6. Engine-free. The geometry of the world renderer's meshes as data, so that the texture mapping is tested: WorldMeshes
// (godot/scripts/Render/) turns these quads into ArrayMeshes. The rule of the mapping (the owner, 2026-10-08): every face shows exactly
// ONE whole tg sprite, uv 0..1 over the whole face, with no wrap or repeat; a part that is a share of a bigger picture (an airlock panel,
// a flap strip) shows exactly its own share of that picture, and the parts together show the picture once.

/// <summary>One quad: four corners listed counter-clockwise seen from outside, with their uv.</summary>
/// <param name="Part">The part id the shaders select by (<see cref="PieceParts"/>; the tile kit's parts are <see cref="TileKitParts"/>).</param>
/// <param name="A">Corner a.</param>
/// <param name="B">Corner b.</param>
/// <param name="C">Corner c.</param>
/// <param name="D">Corner d.</param>
/// <param name="Normal">The outward normal.</param>
/// <param name="Ua">Uv of a.</param>
/// <param name="Ub">Uv of b.</param>
/// <param name="Uc">Uv of c.</param>
/// <param name="Ud">Uv of d.</param>
/// <param name="Arm">M3 R3 (D121): in the tile kit, 1 for a wall's side face (<see cref="TileKitParts.SideFace"/>); in the pieces, 0 for a face that is always drawn; otherwise the bit (the BYOND direction N 1, S 2, E 4, W 8 of a joined window or grille's arm; 1 and 2 for the low and high corner post of a border piece) the instance must have in its joints mask for the face to be drawn (the shader moves the others out of the view).</param>
public readonly record struct GeoQuad(int Part, Vector3 A, Vector3 B, Vector3 C, Vector3 D, Vector3 Normal, Vector2 Ua, Vector2 Ub, Vector2 Uc, Vector2 Ud, int Arm = 0)
{
    /// <summary>The four uv.</summary>
    public Vector2[] Uvs => new[] { Ua, Ub, Uc, Ud };

    /// <summary>The four corners.</summary>
    public Vector3[] Corners => new[] { A, B, C, D };
}

/// <summary>The parts of the tile kit mesh (the vertex's UV2.x).</summary>
public static class TileKitParts
{
    public const int Floor = 0, Ceiling = 1, Wall = 2;

    /// <summary>
    /// A diagonal corner wall (tg's <c>SMOOTH_DIAGONAL_CORNERS</c>: a shuttle's hull): the triangle of the tile cut along its north-west to south-east diagonal, the right angle at the north-east
    /// corner (junction 5, north and east). An instance turns it a quarter clockwise per step for the other three (<see cref="TurnsOf"/>).
    /// </summary>
    public const int DiagonalWall = 3;

    /// <summary>
    /// DEVIATION(DV397): a ladder's hatch: the floor (4) and the ceiling under it (5) with the ladder's opening cut out (<see cref="WorldGeometry.HatchWest"/>
    /// and the rest), where tg makes the whole turf see-through under its hole mask. The shader draws them as the floor and the ceiling.
    /// </summary>
    public const int HatchFloor = 4, HatchCeiling = 5;

    /// <summary>The quarter turns (clockwise from above) of the diagonal wall mesh for a junction number's cardinal bits (5 north and east, 6 south and east, 10 south and west, 9 north and west); -1 for any other.</summary>
    public static int TurnsOf(int junction) => (junction & 15) switch { 5 => 0, 6 => 1, 10 => 2, 9 => 3, _ => -1 };

    /// <summary>
    /// What the tile kit mesh's UV2.y (<c>GeoQuad.Arm</c>) holds for the four SIDE faces of the wall box (M3 R3, D121): they draw the wall type's one straight east-west picture
    /// (<see cref="StraightRun"/>), whatever junction the turf's own picture shows; the top (0) keeps the turf's own picture, the plan view of the wall that the junction describes.
    /// </summary>
    public const int SideFace = 1;
}

/// <summary>The world renderer's meshes as quads.</summary>
public static class WorldGeometry
{
    /// <summary>
    /// Metres above the floor of a table's top surface: what a thing standing on a table is lifted by (M3 R2): 0.75 m (<see cref="WorldScale.TableTop"/>, D157; it was half the
    /// standing eye under M3 R5, and a fixed 0.9 m before). Read when the world is built.
    /// </summary>
    public static float TableTop => WorldScale.TableTop;

    /// <summary>Metres above the floor of the underside of a table's top (the top is 14 cm thick).</summary>
    public static float TableTopUnder => WorldScale.TableTop - TableTopThickness;

    /// <summary>The thickness of a table's top (metres).</summary>
    public const float TableTopThickness = 0.14f;

    /// <summary>The tile edge, where border objects stand (metres from the tile centre, toward the face the piece is turned to).</summary>
    public const float Edge = -0.94f;

    /// <summary>A light fixture's box as the mesh has it (metres): half its width and height around its centre, and its depth out of the
    /// wall face. MapRenderer scales the width and height to the fitting's picture (D142); the pick box (PieceBoxes.Lamp) follows.</summary>
    public const float LampHalfWidth = 0.42f, LampHalfHeight = 0.07f, LampDepth = 0.14f;

    /// <summary>How far from the tile's middle plane a firelock's slabs stand, and their thickness (metres): outside an airlock's panels (0.1) and a pod door's slab (0.12), so that the frame never fights with a door in the same tile.</summary>
    public const float FirelockOffset = 0.12f, FirelockThickness = 0.03f;

    /// <summary>The tile kit at the current <see cref="WorldScale.WallHeight"/> (D119: 2 m).</summary>
    public static List<GeoQuad> TileKit() => TileKit(WorldScale.WallHeight);

    /// <summary>
    /// DV397: a ladder hatch's opening in a tile, in shares of half the tile from its centre: from west to east, and from south to north (the ladder stands at its
    /// north side, LadderBody's rails): 1.2 x 0.9 m of a 2 m tile (the owner widened it from 0.6 m, 2026-11-13), the ladder models' hatch frame (ladder_down, ladder_through).
    /// </summary>
    public const float HatchWest = -0.6f, HatchEast = 0.6f, HatchSouth = 0.05f, HatchNorth = 0.95f;

    /// <summary>How far a wall box's top face is sunk into the box (metres), under the floor of the level above (D325).</summary>
    public const float TopSink = 0.02f;

    /// <summary>
    /// The tile kit: part 0 the floor (y 0), part 1 the ceiling (y = <paramref name="height"/>), part 2 the wall box (2 x <paramref name="height"/> x 2 m,
    /// four sides and the top; D119: 2 x 2 x 2 m, so a side is square). Uv for the turf
    /// pictures: u to the viewer's right, v running DOWN the picture (1 at the bottom edge, 0 at the top). Every face maps the picture
    /// exactly once: a floor and a ceiling tile one sprite per 2 x 2 m tile, a wall side its sprite over 2 m by <paramref name="height"/> (square
    /// at the default, stretched only when the setting is changed), the top one sprite.
    /// </summary>
    public static List<GeoQuad> TileKit(float height)
    {
        var quads = new List<GeoQuad>();
        float h = TileSpace.TileSize / 2f;
        // Floor, seen from above: north (-Z) is the picture's top.
        quads.Add(new GeoQuad(TileKitParts.Floor, new(-h, 0, -h), new(-h, 0, h), new(h, 0, h), new(h, 0, -h), Vector3.UnitY, new(0, 0), new(0, 1), new(1, 1), new(1, 0)));
        // Ceiling, seen from below.
        quads.Add(new GeoQuad(TileKitParts.Ceiling, new(-h, height, -h), new(h, height, -h), new(h, height, h), new(-h, height, h), -Vector3.UnitY, new(0, 0), new(1, 0), new(1, 1), new(0, 1)));
        // DV397: the hatch's floor and ceiling, four strips around the opening (north is -Z), each with its share of the tile's picture.
        float hw = HatchWest * h, he = HatchEast * h, hs = -HatchSouth * h, hn = -HatchNorth * h;
        Vector2 Uv(float x, float z) => new((x + h) / (2 * h), (z + h) / (2 * h));
        foreach ((float x0, float x1, float z0, float z1) in new[] { (-h, h, hs, h), (-h, h, -h, hn), (-h, hw, hn, hs), (he, h, hn, hs) })
        {
            quads.Add(new GeoQuad(TileKitParts.HatchFloor, new(x0, 0, z0), new(x0, 0, z1), new(x1, 0, z1), new(x1, 0, z0), Vector3.UnitY, Uv(x0, z0), Uv(x0, z1), Uv(x1, z1), Uv(x1, z0)));
            quads.Add(new GeoQuad(TileKitParts.HatchCeiling, new(x0, height, z0), new(x1, height, z0), new(x1, height, z1), new(x0, height, z1), -Vector3.UnitY, Uv(x0, z0), Uv(x1, z0), Uv(x1, z1), Uv(x0, z1)));
        }
        // South (+Z), east (+X), north (-Z), west (-X) seen from outside, left to right; the foot is v 1, the top v 0.
        quads.Add(new GeoQuad(TileKitParts.Wall, new(-h, 0, h), new(h, 0, h), new(h, height, h), new(-h, height, h), Vector3.UnitZ, new(0, 1), new(1, 1), new(1, 0), new(0, 0), TileKitParts.SideFace));
        quads.Add(new GeoQuad(TileKitParts.Wall, new(h, 0, h), new(h, 0, -h), new(h, height, -h), new(h, height, h), Vector3.UnitX, new(0, 1), new(1, 1), new(1, 0), new(0, 0), TileKitParts.SideFace));
        quads.Add(new GeoQuad(TileKitParts.Wall, new(h, 0, -h), new(-h, 0, -h), new(-h, height, -h), new(h, height, -h), -Vector3.UnitZ, new(0, 1), new(1, 1), new(1, 0), new(0, 0), TileKitParts.SideFace));
        quads.Add(new GeoQuad(TileKitParts.Wall, new(-h, 0, -h), new(-h, 0, h), new(-h, height, h), new(-h, height, -h), -Vector3.UnitX, new(0, 1), new(1, 1), new(1, 0), new(0, 0), TileKitParts.SideFace));
        // The tops sit TopSink below the wall's height: on a multi-z map (D325) the floor of the level above lies at exactly that height over the wall and
        // fought with it (the owner's report, 2026-11-09: the walls of the level below showed through the floor).
        float top = height - TopSink;
        quads.Add(new GeoQuad(TileKitParts.Wall, new(-h, top, h), new(h, top, h), new(h, top, -h), new(-h, top, -h), Vector3.UnitY, new(0, 1), new(1, 1), new(1, 0), new(0, 0)));
        // The diagonal wall: the north and east sides as the box has them, the slanted face toward the south-west, the top a triangle (a quad with its last corner twice).
        quads.Add(new GeoQuad(TileKitParts.DiagonalWall, new(h, 0, -h), new(-h, 0, -h), new(-h, height, -h), new(h, height, -h), -Vector3.UnitZ, new(0, 1), new(1, 1), new(1, 0), new(0, 0), TileKitParts.SideFace));
        quads.Add(new GeoQuad(TileKitParts.DiagonalWall, new(h, 0, h), new(h, 0, -h), new(h, height, -h), new(h, height, h), Vector3.UnitX, new(0, 1), new(1, 1), new(1, 0), new(0, 0), TileKitParts.SideFace));
        quads.Add(new GeoQuad(TileKitParts.DiagonalWall, new(-h, 0, -h), new(h, 0, h), new(h, height, h), new(-h, height, -h), new(-0.70710677f, 0, 0.70710677f), new(0, 1), new(1, 1), new(1, 0), new(0, 0), TileKitParts.SideFace));
        quads.Add(new GeoQuad(TileKitParts.DiagonalWall, new(h, top, h), new(h, top, -h), new(-h, top, -h), new(-h, top, -h), Vector3.UnitY, new(1, 1), new(1, 0), new(0, 0), new(0, 0)));
        return quads;
    }

    /// <summary>The unit picture quad of billboards and wall decals: x from -0.5 to 0.5, y from 0 to 1, facing +Z, uv.y 0 at the bottom; the picture once.</summary>
    public static List<GeoQuad> PictureQuad()
        => new() { new GeoQuad(0, new(-0.5f, 0, 0), new(0.5f, 0, 0), new(0.5f, 1, 0), new(-0.5f, 1, 0), Vector3.UnitZ, new(0, 0), new(1, 0), new(1, 1), new(0, 1)) };

    /// <summary>A rectangle of the picture a box's front and back faces map to: in the box's x and y (metres).</summary>
    private readonly record struct Ref(float X0, float X1, float Y0, float Y1);

    // Uv of a piece box face, v UP (0 at the bottom of the picture): u runs to the viewer's right (+X seen from the south, -X from the north,
    // -Z from the east, +Z from the west, +X from above with north up). The faces along Z map the reference rectangle when there is one (so
    // the parts of a door each show their share), every other face its own extent, 0..1.
    private static void Box(List<GeoQuad> quads, Vector3 min, Vector3 max, int part, Ref? reference = null, bool top = true, int arm = 0, bool skipMinX = false, bool skipMaxX = false,
        bool backAsFront = false)
    {
        float x0 = min.X, y0 = min.Y, z0 = min.Z, x1 = max.X, y1 = max.Y, z1 = max.Z;
        Ref r = reference ?? new Ref(x0, x1, y0, y1);
        float rw = r.X1 - r.X0, rh = r.Y1 - r.Y0;
        float dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
        Vector2 Front(float x, float y) => new((x - r.X0) / rw, (y - r.Y0) / rh);
        // backAsFront: the -Z face's picture lies as the +Z face's (mirrored as seen from the north), so a picture's column stays at the same end of the box on both faces
        Vector2 Back(float x, float y) => backAsFront ? Front(x, y) : new((r.X1 - x) / rw, (y - r.Y0) / rh);
        Vector2 East(float z, float y) => new((z1 - z) / dz, (y - y0) / dy);
        Vector2 West(float z, float y) => new((z - z0) / dz, (y - y0) / dy);
        Vector2 Top(float x, float z) => new((x - x0) / dx, (z1 - z) / dz);
        // +Z face (seen from the south).
        quads.Add(new GeoQuad(part, new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1), Vector3.UnitZ, Front(x0, y0), Front(x1, y0), Front(x1, y1), Front(x0, y1), arm));
        // -Z face (seen from the north).
        quads.Add(new GeoQuad(part, new(x1, y0, z0), new(x0, y0, z0), new(x0, y1, z0), new(x1, y1, z0), -Vector3.UnitZ, Back(x1, y0), Back(x0, y0), Back(x0, y1), Back(x1, y1), arm));
        // +X face (seen from the east).
        if (!skipMaxX)
            quads.Add(new GeoQuad(part, new(x1, y0, z1), new(x1, y0, z0), new(x1, y1, z0), new(x1, y1, z1), Vector3.UnitX, East(z1, y0), East(z0, y0), East(z0, y1), East(z1, y1), arm));
        // -X face (seen from the west).
        if (!skipMinX)
            quads.Add(new GeoQuad(part, new(x0, y0, z0), new(x0, y0, z1), new(x0, y1, z1), new(x0, y1, z0), -Vector3.UnitX, West(z0, y0), West(z1, y0), West(z1, y1), West(z0, y1), arm));
        if (top)
            quads.Add(new GeoQuad(part, new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0), new(x0, y1, z0), Vector3.UnitY, Top(x0, z1), Top(x1, z1), Top(x1, z0), Top(x0, z0), arm));
    }

    // D147: the window base: its top shows the grating picture once over the tile (WindowBaseCell), at the pixel size of every floor tile; a side's uv are the top's as if the side were folded up flat beside it, so the grating runs on
    // over the edge without a seam (the picture is tg's joined catwalk, which tiles).
    private static void GratedBase(List<GeoQuad> quads)
    {
        const float h = WindowBaseHeight, cell = WindowBaseCell;
        static Vector2 TopUv(float x, float z) => new((x + 1f) / cell, (1f - z) / cell);
        // a side point (x, y, z) unfolded onto the top's plane, outward by its depth under the top
        static Vector2 Unfolded(Vector3 p, Vector3 normal) => TopUv(p.X + normal.X * (h - p.Y), p.Z + normal.Z * (h - p.Y));
        void Add(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Func<Vector3, Vector2> uv)
        {
            Vector2[] t = { uv(a), uv(b), uv(c), uv(d) };
            // the quad's cell: the uv moved so that it lies in 0..1
            var shift = new Vector2(MathF.Floor(MathF.Min(MathF.Min(t[0].X, t[1].X), MathF.Min(t[2].X, t[3].X)) + 0.001f), MathF.Floor(MathF.Min(MathF.Min(t[0].Y, t[1].Y), MathF.Min(t[2].Y, t[3].Y)) + 0.001f));
            quads.Add(new GeoQuad(PieceParts.WindowBase, a, b, c, d, normal, t[0] - shift, t[1] - shift, t[2] - shift, t[3] - shift));
        }
        for (float a = -1f; a < 1f - 0.001f; a += cell)
        {
            float b = a + cell;
            Add(new(a, 0, 1), new(b, 0, 1), new(b, h, 1), new(a, h, 1), Vector3.UnitZ, p => Unfolded(p, Vector3.UnitZ));
            Add(new(b, 0, -1), new(a, 0, -1), new(a, h, -1), new(b, h, -1), -Vector3.UnitZ, p => Unfolded(p, -Vector3.UnitZ));
            Add(new(1, 0, b), new(1, 0, a), new(1, h, a), new(1, h, b), Vector3.UnitX, p => Unfolded(p, Vector3.UnitX));
            Add(new(-1, 0, a), new(-1, 0, b), new(-1, h, b), new(-1, h, a), -Vector3.UnitX, p => Unfolded(p, -Vector3.UnitX));
            for (float z = -1f; z < 1f - 0.001f; z += cell)
            {
                float z1 = z + cell;
                Add(new(a, h, z1), new(b, h, z1), new(b, h, z), new(a, h, z), Vector3.UnitY, p => TopUv(p.X, p.Z));
            }
        }
    }

    // The same quads turned a quarter about the vertical axis (+x becomes north, -z), with the arm bit of the new direction: how the east arm of a joined piece
    // becomes its north arm and the west arm its south arm. The uv stay with their faces, so the picture runs along the arm whichever way it points.
    private static IEnumerable<GeoQuad> TurnedNorth(IEnumerable<GeoQuad> arm, int armBit)
    {
        static Vector3 Turn(Vector3 v) => new(v.Z, v.Y, -v.X);
        foreach (GeoQuad q in arm)
            yield return q with { A = Turn(q.A), B = Turn(q.B), C = Turn(q.C), D = Turn(q.D), Normal = Turn(q.Normal), Arm = armBit };
    }

    /// <summary>The joint bits of the arms of a joined window or grille (<see cref="Joints"/>) and the corner posts of a border piece (<see cref="BorderCorners"/>).</summary>
    private const int ArmNorth = Joints.North, ArmSouth = Joints.South, ArmEast = Joints.East, ArmWest = Joints.West;

    /// <summary>
    /// D152 (block windows): the marks of a pane's end caps (its -x and +x faces, a directional window's or a windoor's), drawn only when the instance's joints mask
    /// has the bit; the piece renderer sets both unless the pane runs on into the next one at that end. Above the corner posts' bits (1, 2), which share the mask.
    /// </summary>
    public const int PaneCapLow = 4, PaneCapHigh = 8;

    // A pane box whose -x and +x faces are its end caps (PaneCapLow, PaneCapHigh). backAsFront: a directional window's run is framed at its ends
    // by its junction picture (BlockFaces.BorderWindowJunction), so its back face must not mirror the side frame onto the joint (the owner, 2026-10-29).
    private static void Capped(List<GeoQuad> quads, Vector3 min, Vector3 max, int part, Ref reference, bool backAsFront = false)
    {
        var box = new List<GeoQuad>();
        Box(box, min, max, part, reference, backAsFront: backAsFront);
        foreach (GeoQuad q in box)
            quads.Add(q.Normal.X < -0.5f ? q with { Arm = PaneCapLow } : q.Normal.X > 0.5f ? q with { Arm = PaneCapHigh } : q);
    }

    /// <summary>The metres of an open pod door or shutters left under the ceiling, so the doorway shows it has one (the owner, 2026-10-15); prop_atlas.gdshader's POD_DOOR_LIP.</summary>
    public const float PodDoorLip = 0.12f;

    /// <summary>D147: the height of a full-tile window's base (metres), above the highest floor layer (FloorLayers: under 5 cm).</summary>
    public const float WindowBaseHeight = 0.15f;

    /// <summary>D147: the side of the square the window base's grating picture covers once: the whole tile, so its pixels are as large as a floor tile's (metres).</summary>
    public const float WindowBaseCell = 2f;

    /// <summary>The half width of a full-tile window's slab and of its centre post (metres): the slab is 12 cm thick.</summary>
    public const float SlabHalf = 0.06f;

    /// <summary>The width of a corner post of a border piece along its piece, and how much it is thicker than the piece on each side (metres).</summary>
    public const float PostWidth = 0.12f, PostThick = 0.06f;

    /// <summary>The shaped pieces at the current <see cref="WorldScale.WallHeight"/> (D119: 2 m).</summary>
    public static List<GeoQuad> Pieces() => Pieces(WorldScale.WallHeight);

    /// <summary>
    /// The shaped pieces, all built for a piece on the NORTH side of its tile (outward is -Z); an instance turns them to their face. Heights
    /// are metres. The full-height pieces (the windows, the grille, the airlock's panels, the pod door, the windoor and the flaps) are
    /// <paramref name="wall"/> tall, 2 m by default (D119), so each tile-wide face is a square 2 x 2 m and tg's 32 x 32 sprite sits on it undistorted.
    /// A part that is a share of a picture has a reference rectangle (the whole door, the whole pane): a full-tile window, a directional window,
    /// a windoor and a pod door map their sprite over x -1..1 and y 0..<paramref name="wall"/>, the airlock's two panels the whole door sprite
    /// once between them (the left panel u 0..0.5, the right 0.5..1), the flaps the same over their strips, and the railing over its own height
    /// (1.02 m). The grille's bars and the light box are drawn with a flat swatch (their uv do not matter).
    /// </summary>
    public static List<GeoQuad> Pieces(float wall)
    {
        var quads = new List<GeoQuad>();
        const float edge = Edge; // the tile edge, where border objects stand
        var tileSprite = new Ref(-1f, 1f, 0f, wall); // one sprite over the tile's width and the wall's height
        // A full-tile window (D121): a centre post and an arm toward each neighbour it joins, as high as the wall, 12 cm thick through the middle of the tile. The east
        // and west arms show their share of the one picture (the straight east-west run), the picture running along the slab; the north arm is the east arm turned and
        // the south arm the west arm turned, so a straight run of either axis reads the same. A run is the post and two arms: the whole picture once, as the old slab.
        Box(quads, new(-SlabHalf, 0f, -SlabHalf), new(SlabHalf, wall, SlabHalf), PieceParts.FullWindow, tileSprite);
        var windowEast = new List<GeoQuad>();
        Box(windowEast, new(SlabHalf, 0f, -SlabHalf), new(1f, wall, SlabHalf), PieceParts.FullWindow, tileSprite, arm: ArmEast, skipMinX: true);
        var windowWest = new List<GeoQuad>();
        Box(windowWest, new(-1f, 0f, -SlabHalf), new(-SlabHalf, wall, SlabHalf), PieceParts.FullWindow, tileSprite, arm: ArmWest, skipMaxX: true);
        quads.AddRange(windowEast);
        quads.AddRange(windowWest);
        quads.AddRange(TurnedNorth(windowEast, ArmNorth));
        quads.AddRange(TurnedNorth(windowWest, ArmSouth));
        // A directional window: a pane on one edge, as high as the wall too (tg's sprite is a full tile square); a corner post at either end where it meets a piece of the other axis.
        // Its two end caps carry PaneCapLow and PaneCapHigh: a pane that runs on into the next (D152 (block windows)) hides them, as they show through the glass.
        Capped(quads, new(-1f, 0f, edge - 0.04f), new(1f, wall, edge + 0.04f), PieceParts.DirectionalWindow, tileSprite, backAsFront: true);
        // A grille (D121): a centre bar and, toward each neighbour it joins, an arm of three bars 30 cm apart in a frame (the top and bottom rails run to the tile edge,
        // so a run of grilles has continuous rails); the old 7 bars at 30 cm are the post and the two arms of a straight run. (wall - 0.1) m tall: a square at 2 m.
        // The grille is a flat swatch, no sprite, and the faces are lit alike and drawn from both sides, so a bar is two crossed quads and a rail one upright and one flat quad:
        // every instance of the piece mesh pays for every vertex of it, and a grille with boxes for bars would have cost more than the rest of the mesh together.
        Bar(quads, 0f, wall, 0);
        var grilleEast = new List<GeoQuad>();
        var grilleWest = new List<GeoQuad>();
        for (int i = 1; i <= 3; i++)
        {
            Bar(grilleEast, i * 0.3f, wall, ArmEast);
            Bar(grilleWest, -i * 0.3f, wall, ArmWest);
        }
        Rail(grilleEast, 0f, 1f, 0.05f, 0.12f, ArmEast);
        Rail(grilleEast, 0f, 1f, wall - 0.12f, wall - 0.05f, ArmEast);
        Rail(grilleWest, -1f, 0f, 0.05f, 0.12f, ArmWest);
        Rail(grilleWest, -1f, 0f, wall - 0.12f, wall - 0.05f, ArmWest);
        quads.AddRange(grilleEast);
        quads.AddRange(grilleWest);
        quads.AddRange(TurnedNorth(grilleEast, ArmNorth));
        quads.AddRange(TurnedNorth(grilleWest, ArmSouth));
        // A table: a top on four legs; every face shows the table's sprite once.
        Box(quads, new(-0.97f, TableTopUnder, -0.97f), new(0.97f, TableTop, 0.97f), PieceParts.Table);
        foreach (float sx in new[] { -0.85f, 0.85f })
        {
            foreach (float sz in new[] { -0.85f, 0.85f })
                Box(quads, new(sx - 0.07f, 0f, sz - 0.07f), new(sx + 0.07f, TableTopUnder, sz + 0.07f), PieceParts.Table, top: false);
        }
        // A railing: a top rail and three posts on one edge, over one picture 2 m wide and as high as the rail (1.02 m).
        var railing = new Ref(-1f, 1f, 0f, 1.02f);
        Box(quads, new(-1f, 0.92f, edge - 0.04f), new(1f, 1.02f, edge + 0.04f), PieceParts.Railing, railing);
        foreach (float px in new[] { -0.95f, 0f, 0.95f })
            Box(quads, new(px - 0.04f, 0f, edge - 0.04f), new(px + 0.04f, 0.92f, edge + 0.04f), PieceParts.Railing, railing, top: false);
        // Plastic flaps: hanging strips across a doorway from the lintel to the floor, each showing its share of one picture 2 m wide and as high as the wall.
        for (int i = 0; i < 4; i++)
        {
            float x = -0.77f + i * (1.54f / 3f); // four strips 0.46 m wide that stay inside the doorway
            Box(quads, new(x - 0.23f, 0f, -0.03f), new(x + 0.23f, wall, 0.03f), PieceParts.Flap, tileSprite, top: false);
        }
        // A light fixture: a box on the wall face (the wall is at z -1), a tube or a bulb (scaled to its picture by MapRenderer, D142).
        Box(quads, new(-LampHalfWidth, -LampHalfHeight, -1f), new(LampHalfWidth, LampHalfHeight, -1f + LampDepth), PieceParts.Light);
        // An airlock: two panels in the doorway, together the door's sprite once.
        Box(quads, new(-1f, 0f, -0.1f), new(0f, wall, 0.1f), PieceParts.AirlockLeft, tileSprite);
        Box(quads, new(0f, 0f, -0.1f), new(1f, wall, 0.1f), PieceParts.AirlockRight, tileSprite);
        // A pod door or shutters: one slab that drops.
        Box(quads, new(-1f, 0f, -0.12f), new(1f, wall, 0.12f), PieceParts.PodDoor, tileSprite);
        // A windoor: a pane on the edge that slides (tg's windoor is a full tile sprite too: as high as the wall, D119); its end caps as the directional window's.
        Capped(quads, new(-1f, 0f, edge - 0.04f), new(1f, wall, edge + 0.04f), PieceParts.Windoor, tileSprite);
        // A firelock (M3 R2): its sprite (the frame when it is open, the whole slab when closed) on a thin slab on each side of the doorway, so
        // that it reads from both sides and clears an airlock's panels and a pod door's slab in the same tile; the alpha scissor leaves just the frame.
        Box(quads, new(-1f, 0f, FirelockOffset), new(1f, wall, FirelockOffset + FirelockThickness), PieceParts.FirelockSouth, tileSprite, top: false);
        Box(quads, new(-1f, 0f, -FirelockOffset - FirelockThickness), new(1f, wall, -FirelockOffset), PieceParts.FirelockNorth, tileSprite, top: false);
        // A border-only firelock: one slab just inside the tile edge its dir faces (the windoor stands on the edge itself).
        Box(quads, new(-1f, 0f, edge + 0.05f), new(1f, wall, edge + 0.05f + FirelockThickness), PieceParts.FirelockEdge, tileSprite, top: false);
        // The corner posts of the border pieces (D121): two parts of their own, drawn as extra instances only where a piece needs a post (BorderCorners), so the pieces without one
        // do not pay for them. The window's and the border firelock's are as high as the wall, the railing's as high as its rail.
        CornerPosts(quads, PieceParts.BorderPost, edge - PostThick, edge + PostThick, wall, tileSprite);
        CornerPosts(quads, PieceParts.RailPost, edge - PostThick, edge + PostThick, 1.02f, railing);
        // D147: a full-tile window's base, a low block over the whole tile, so the plating under the glass does not show its pipes and cables.
        GratedBase(quads);
        // D152 (block windows) (BlockFaces): a tile-sized block, each face a part of its own showing one whole picture, v up, the viewer's right u 1.
        BlockFaceQuads(quads, wall);
        return quads;
    }

    // The faces of a block piece: the four sides, as high as the wall, the top (north up, as tg draws it) and the bottom (the grille's only face).
    private static void BlockFaceQuads(List<GeoQuad> quads, float wall)
    {
        Vector2 a = new(0, 0), b = new(1, 0), c = new(1, 1), d = new(0, 1);
        quads.Add(new GeoQuad(PieceParts.BlockSouth, new(-1, 0, 1), new(1, 0, 1), new(1, wall, 1), new(-1, wall, 1), Vector3.UnitZ, a, b, c, d));
        quads.Add(new GeoQuad(PieceParts.BlockNorth, new(1, 0, -1), new(-1, 0, -1), new(-1, wall, -1), new(1, wall, -1), -Vector3.UnitZ, a, b, c, d));
        quads.Add(new GeoQuad(PieceParts.BlockEast, new(1, 0, 1), new(1, 0, -1), new(1, wall, -1), new(1, wall, 1), Vector3.UnitX, a, b, c, d));
        quads.Add(new GeoQuad(PieceParts.BlockWest, new(-1, 0, -1), new(-1, 0, 1), new(-1, wall, 1), new(-1, wall, -1), -Vector3.UnitX, a, b, c, d));
        quads.Add(new GeoQuad(PieceParts.BlockTop, new(-1, wall, 1), new(1, wall, 1), new(1, wall, -1), new(-1, wall, -1), Vector3.UnitY, a, b, c, d));
        // The bottom, at the floor, seen from above like the top (an instance lifts it).
        quads.Add(new GeoQuad(PieceParts.BlockBottom, new(-1, 0, 1), new(1, 0, 1), new(1, 0, -1), new(-1, 0, -1), Vector3.UnitY, a, b, c, d));
    }

    // A grille bar (a flat swatch): two crossed upright quads, 6 cm wide, from 5 cm above the floor to 5 cm under the top.
    private static void Bar(List<GeoQuad> quads, float x, float wall, int arm)
    {
        const float half = 0.03f;
        float y0 = 0.05f, y1 = wall - 0.05f;
        quads.Add(new GeoQuad(PieceParts.Grille, new(x - half, y0, 0), new(x + half, y0, 0), new(x + half, y1, 0), new(x - half, y1, 0), Vector3.UnitZ, new(0, 0), new(1, 0), new(1, 1), new(0, 1), arm));
        quads.Add(new GeoQuad(PieceParts.Grille, new(x, y0, half), new(x, y0, -half), new(x, y1, -half), new(x, y1, half), Vector3.UnitX, new(0, 0), new(1, 0), new(1, 1), new(0, 1), arm));
    }

    // A grille rail (a flat swatch) from x0 to x1 between y0 and y1: an upright quad and a flat one on top.
    private static void Rail(List<GeoQuad> quads, float x0, float x1, float y0, float y1, int arm)
    {
        const float half = 0.04f;
        quads.Add(new GeoQuad(PieceParts.Grille, new(x0, y0, 0), new(x1, y0, 0), new(x1, y1, 0), new(x0, y1, 0), Vector3.UnitZ, new(0, 0), new(1, 0), new(1, 1), new(0, 1), arm));
        quads.Add(new GeoQuad(PieceParts.Grille, new(x0, y1, half), new(x1, y1, half), new(x1, y1, -half), new(x0, y1, -half), Vector3.UnitY, new(0, 0), new(1, 0), new(1, 1), new(0, 1), arm));
    }

    // The two corner posts of a border piece (D121): 12 cm along the piece at either end, a little thicker than the piece on each side, drawn only when the instance's joints mask
    // has the end's bit (BorderCorners.LowEnd for the -x end, HighEnd for the +x end): where a piece of the other axis ends at that tile corner and would leave a notch. The face
    // toward the piece is left out (it is inside the post and the piece).
    private static void CornerPosts(List<GeoQuad> quads, int part, float z0, float z1, float height, Ref reference)
    {
        Box(quads, new(-1f, 0f, z0), new(-1f + PostWidth, height, z1), part, reference, arm: BorderCorners.LowEnd, skipMaxX: true);
        Box(quads, new(1f - PostWidth, 0f, z0), new(1f, height, z1), part, reference, arm: BorderCorners.HighEnd, skipMinX: true);
    }
}
