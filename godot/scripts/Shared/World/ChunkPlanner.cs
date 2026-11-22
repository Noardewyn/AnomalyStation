using System.Linq;
using System.Numerics;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.World;

// Owner: M3 P6. Engine-free. The chunk planner (scope 2i): which cells and entities go into which 16 x 16 tile chunk, as which
// shape. The renderer (godot/scripts/Render/) turns a plan into MultiMeshes; nothing here touches the engine, so the plan is tested.

/// <summary>What a tile instance is.</summary>
public enum TileShape : byte
{
    /// <summary>The floor of an open turf.</summary>
    Floor = 0,

    /// <summary>The ceiling above an open turf (space gets none, D116).</summary>
    Ceiling = 1,

    /// <summary>A closed turf: a box as high as the wall (D119: 2 x 2 x 2 m).</summary>
    Wall = 2,
}

/// <summary>One floor, ceiling or wall instance.</summary>
/// <param name="X">BYOND x.</param>
/// <param name="Y">BYOND y.</param>
/// <param name="Z">The level.</param>
/// <param name="Shape">What it is.</param>
/// <param name="Appearance">The turf's appearance (none: the renderer tints by <paramref name="TypeIndex"/>).</param>
/// <param name="TypeIndex">The index into <c>MapGrid.TurfTypes</c>.</param>
public readonly record struct TileCell(int X, int Y, int Z, TileShape Shape, TableRef Appearance, int TypeIndex)
{
    public Anomaly.Core.Motion.CeilingState Ceiling { get; init; }

    /// <summary>A wall's walls straight above and below it on its stack (<see cref="Smoothing.Above"/>, <see cref="Smoothing.Below"/>): its sides join them (the owner, 2026-11-10).</summary>
    public byte Stack { get; init; }

    /// <summary>A wall with no floor or wall of a linked level over it: its top is drawn at the full height (TopSink only keeps it under a floor above).</summary>
    public bool Roofless { get; init; }

    /// <summary>A wall's cardinal neighbours (BYOND bits) whose wall has a wall straight above it, and in the high four bits below it: its side faces' corners (<see cref="BlockFaces.WallFace"/>).</summary>
    public byte NearStack { get; init; }
    /// <summary>A wall's diagonal neighbours that are walls it joins (<see cref="BlockFaces.DiagonalsOf"/>): the inside of a corner keeps its frame.</summary>
    public byte Diagonals { get; init; }

    /// <summary>DV397: a floor or a ceiling with a ladder hatch's opening cut out (<see cref="TileKitParts.HatchFloor"/>, <see cref="TileKitParts.HatchCeiling"/>).</summary>
    public bool Hatch { get; init; }
}

/// <summary>How an entity is drawn.</summary>
public enum PlacedKind : byte
{
    /// <summary>A Y-axis billboard of the entity's picture (a static prop of its chunk's batch).</summary>
    Billboard = 0,

    /// <summary>A flat picture on a wall face (scope 2e).</summary>
    WallDecal,

    /// <summary>A shaped piece (D113): a door, window, grille, table, railing, flap or light fixture.</summary>
    Piece,

    /// <summary>
    /// An item (<c>/obj/item</c>, D121): a Y-axis billboard at <see cref="WorldScale.ItemScale"/> of the tile scale, about the bottom centre of its picture, which rests on its
    /// support (the floor, or the table top the tile has): the picture's own vertical offset in its frame is dropped.
    /// </summary>
    Item,

    /// <summary>
    /// A standing picture at the TILE scale whose drawn pixels rest on their support (M3 R3): a machine or small structure that stands on a table (tg draws it mid-frame in the top-down
    /// view, so the picture's own vertical offset would hold it above the top), and the flat floor equipment tg draws mid-frame too (a holopad, a navigation beacon), which would hover.
    /// </summary>
    Resting,

    /// <summary>
    /// A thing that lies flat on the floor (D121: an air vent or scrubber; D122: everything tg draws on the floor plane's topdown layers or on the pipe layers below <c>LOW_OBJ_LAYER</c>: the
    /// lattice, the disposal pipes, the wires, the pipes, the vents, the filters and pumps): a horizontal picture a few millimetres above the floor, at the tile scale, turned by its dir, at
    /// the height <see cref="Placed.Height"/> its layer gives (<see cref="FloorLayers"/>).
    /// </summary>
    FloorDecal,
}

/// <summary>
/// The facts about an entity the planner needs (the client reads them from a <c>ReplicaEntity</c>; the tests make them up).
/// </summary>
/// <param name="Id">The entity's world id.</param>
/// <param name="TypePath">The DM type path.</param>
/// <param name="X">BYOND x.</param>
/// <param name="Y">BYOND y.</param>
/// <param name="Z">The level.</param>
/// <param name="Dir">BYOND dir.</param>
/// <param name="Appearance">The public appearance.</param>
/// <param name="PixelX">The appearance's pixel_x.</param>
/// <param name="PixelY">The appearance's pixel_y.</param>
/// <param name="Light">The entity's light.</param>
/// <param name="IconState">The appearance's icon state text (null for none).</param>
/// <param name="Plane">M3 R5 (D122): the appearance's plane (0 when unknown), which with <paramref name="Layer"/> decides whether the thing lies flat under the floor tile's layers (<see cref="FloorLayers"/>).</param>
/// <param name="Layer">M3 R5 (D122): the appearance's layer (0 when unknown).</param>
public readonly record struct PlanEntity(long Id, string TypePath, int X, int Y, int Z, byte Dir, TableRef Appearance, short PixelX, short PixelY, LightState Light, string IconState,
    float Plane = 0f, float Layer = 0f);

/// <summary>An entity placed by the planner.</summary>
/// <param name="EntityId">The entity's id.</param>
/// <param name="Kind">Billboard, wall decal or piece.</param>
/// <param name="Shape">The D113 shape (billboard for decals and billboards).</param>
/// <param name="X">BYOND x of the tile.</param>
/// <param name="Y">BYOND y.</param>
/// <param name="Z">The level.</param>
/// <param name="Dir">The entity's dir (the billboard's facing).</param>
/// <param name="Face">The wall face (a BYOND dir) a decal, fixture or border piece sits on; for a door the face of its plane (NORTH: the plane runs along x, EAST: along y); 0 for none and for a window or grille, which have <paramref name="Joints"/> instead.</param>
/// <param name="Height">Metres above the floor of the centre of a wall decal or fixture; of a billboard or an item, how far it is lifted (the top of the table it stands on); of a floor decal, how far above the floor it lies (its layer's place in the stack, <see cref="FloorLayers.LiftOf"/>).</param>
/// <param name="OffsetX">Metres east of the tile centre (the root's pixel_x; along the wall for a decal). The picture carries no pixel offset of its own (M3 R2): this is the only place it is applied.</param>
/// <param name="OffsetY">Metres north of the tile centre (the root's pixel_y).</param>
/// <param name="Appearance">The entity's appearance.</param>
/// <param name="Light">The entity's light.</param>
/// <param name="IconState">The entity's icon state.</param>
/// <param name="InWall">A decal of wall furniture that stands IN the wall cell itself (tg's undirected signs, status displays and notice boards sit on the wall turf): <paramref name="Face"/> is then the way from the entity's own cell to the open side that carries the picture, not the way to a wall.</param>
/// <param name="Fin">A wall decal that stands OUT of the wall face, perpendicular to it, instead of lying flat on it (a security camera, the owner's play test 2026-10-14): tg's side view of it on a vertical plane through the wall's normal (<see cref="WallMounts.FinPlacement"/>).</param>
/// <param name="Scale">The size of a standing picture or a floor decal, times what the rules give it (<see cref="WorldLooks"/>: 1 as drawn).</param>
/// <param name="Joints">M3 R3 (D121): for a full-tile window or a grille the arms toward the neighbours it joins (<see cref="Joints"/>: the BYOND direction bits N 1, S 2, E 4, W 8); for a directional window, a railing or a border firelock the corner posts of its two ends (<see cref="BorderCorners.LowEnd"/>, <see cref="BorderCorners.HighEnd"/>); 0 for anything else.</param>
public readonly record struct Placed(long EntityId, PlacedKind Kind, RenderKind Shape, int X, int Y, int Z, byte Dir, byte Face, float Height, float OffsetX, float OffsetY, TableRef Appearance, LightState Light, string IconState, bool InWall = false, byte Joints = 0, bool Fin = false, float Scale = 1f);

/// <summary>One chunk: its cells and what stands on them.</summary>
public sealed class ChunkPlan
{
    public ChunkPlan(int chunkX, int chunkY, int z)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        Z = z;
    }

    /// <summary>The chunk's column (cells 1 + 16 * ChunkX ...).</summary>
    public int ChunkX { get; }

    /// <summary>The chunk's row.</summary>
    public int ChunkY { get; }

    /// <summary>The level.</summary>
    public int Z { get; }

    public List<TileCell> Tiles { get; } = new();

    public List<Placed> Placed { get; } = new();

    /// <summary>The lowest BYOND x of the chunk.</summary>
    public int MinX => 1 + ChunkX * ChunkPlanner.ChunkSize;

    /// <summary>The lowest BYOND y of the chunk.</summary>
    public int MinY => 1 + ChunkY * ChunkPlanner.ChunkSize;

    /// <summary>The world-space box of the chunk (metres) from its first to its last cell and the level's floor to its ceiling, for the AABB of its nodes: (min x, min y, min z), (size x, size y, size z).</summary>
    public (float MinX, float MinY, float MinZ, float SizeX, float SizeY, float SizeZ) Bounds(int mapWidth, int mapHeight)
    {
        int maxX = Math.Min(mapWidth, MinX + ChunkPlanner.ChunkSize - 1), maxY = Math.Min(mapHeight, MinY + ChunkPlanner.ChunkSize - 1);
        float x0 = (MinX - 1) * TileSpace.TileSize, x1 = maxX * TileSpace.TileSize;
        float z0 = -maxY * TileSpace.TileSize, z1 = -(MinY - 1) * TileSpace.TileSize;
        float y0 = TileSpace.WorldY(Z);
        return (x0, y0, z0, x1 - x0, TileSpace.LevelHeight, z1 - z0);
    }
}

/// <summary>What the planner needs besides the grid.</summary>
public sealed class PlanOptions
{
    /// <summary>Whether open cells get a ceiling (the F2 key hides it at run time).</summary>
    public bool Ceilings { get; init; } = true;

    /// <summary>Whether the closed turf at (x, y, z) smooths (its picture has tg's junctions): only such walls join the walls straight above and below them; null: every wall.</summary>
    public Func<int, int, int, bool> Smooths { get; init; }
}

/// <summary>The plan of a whole map.</summary>
public sealed class WorldPlan
{
    public WorldPlan(int width, int height, int levels)
    {
        Width = width;
        Height = height;
        Levels = levels;
    }

    public int Width { get; }

    public int Height { get; }

    public int Levels { get; }

    /// <summary>The non-empty chunks, in (z, y, x) order.</summary>
    public List<ChunkPlan> Chunks { get; } = new();

    /// <summary>What the planner learnt of the map's entities (the cells of windows, grilles, doors and tables, the doors' faces, the border corners); a later spawn is placed against it and added to it.</summary>
    public PlanIndex Index { get; internal set; } = new();

    private readonly Dictionary<(int, int, int), ChunkPlan> _byKey = new();

    /// <summary>The chunk with this key (created when new).</summary>
    public ChunkPlan Chunk(int chunkX, int chunkY, int z)
    {
        if (!_byKey.TryGetValue((chunkX, chunkY, z), out ChunkPlan chunk))
        {
            chunk = new ChunkPlan(chunkX, chunkY, z);
            _byKey[(chunkX, chunkY, z)] = chunk;
            Chunks.Add(chunk);
        }
        return chunk;
    }

    /// <summary>The chunk that holds the cell, null when it has none.</summary>
    public ChunkPlan ChunkOf(int x, int y, int z)
        => _byKey.TryGetValue(((x - 1) / ChunkPlanner.ChunkSize, (y - 1) / ChunkPlanner.ChunkSize, z), out ChunkPlan chunk) ? chunk : null;

    public int TileCount => Chunks.Sum(c => c.Tiles.Count);

    public int PlacedCount => Chunks.Sum(c => c.Placed.Count);
}

/// <summary>
/// What the planner knows of a map's entities beyond the grid (M3 R3): the cells of the full-tile windows, grilles, doors and tables, the face of every door's plane,
/// and where the border pieces meet. <see cref="ChunkPlanner.Plan"/> fills it from the entity list; a thing that spawns later is placed against it.
/// </summary>
public sealed class PlanIndex
{
    /// <summary>Cells with a full-tile window.</summary>
    public HashSet<(int X, int Y, int Z)> Windows { get; } = new();

    /// <summary>Cells with a grille.</summary>
    public HashSet<(int X, int Y, int Z)> Grilles { get; } = new();

    /// <summary>Cells with a girder (D152 (block windows): a girder block hides its face against another).</summary>
    public HashSet<(int X, int Y, int Z)> Girders { get; } = new();

    /// <summary>
    /// The directional windows by tile and the edge they stand on (D152 (block windows): a pane's frame follows the panes beside it,
    /// <see cref="BlockFaces.BorderWindowJunction"/>; a windoor is a door of its own, so the panes beside it keep their frame toward it, the owner 2026-10-16).
    /// </summary>
    public HashSet<(int X, int Y, int Z, byte Face)> BorderWindows { get; } = new();

    /// <summary>Cells with a door that closes a passage: an airlock or any other door, a pod door, shutters, a firelock that fills its tile.</summary>
    public HashSet<(int X, int Y, int Z)> Doors { get; } = new();

    /// <summary>Cells with a door frame a window or grille joins: the doors of <see cref="Doors"/> but the firelocks, which stand in a hall beside the frames.</summary>
    public HashSet<(int X, int Y, int Z)> FrameDoors { get; } = new();

    /// <summary>Cells with a table.</summary>
    public HashSet<(int X, int Y, int Z)> Tables { get; } = new();

    /// <summary>The face of the plane of the door in each cell of <see cref="Doors"/> (<see cref="DoorAxis"/>).</summary>
    public Dictionary<(int X, int Y, int Z), byte> DoorFaces { get; } = new();

    /// <summary>Where the border pieces end.</summary>
    public BorderCorners Corners { get; } = new();

    /// <summary>
    /// D325: tg's ladder hole (ladders.dm make_base_transparent: a ladder down onto the level under it makes its open turf see-through, masked by the hole): the
    /// client cuts the ladder's hatch out of the floor there and out of the ceiling under it (DV397, <see cref="TileCell.Hatch"/>), so the ladder goes down
    /// through an opening (the owner, 2026-11-10, 2026-11-11). The effect itself draws nothing.
    /// </summary>
    public HashSet<(int X, int Y, int Z)> Holes { get; } = new();

    public const string LadderHoleType = "/obj/effect/abstract/ladder_hole";

    /// <summary>
    /// M5 slice 5: forgets the full-tile window or grille of a cell when none is left there (<paramref name="windowLeft"/>, <paramref name="grilleLeft"/>: whether
    /// another one still stands in it), so that its neighbours stop joining it.
    /// </summary>
    public void Forget((int X, int Y, int Z) cell, bool windowLeft, bool grilleLeft, bool frameDoorLeft = true, bool doorLeft = true, bool girderLeft = true)
    {
        if (!girderLeft)
            Girders.Remove(cell);
        if (!windowLeft)
            Windows.Remove(cell);
        if (!grilleLeft)
            Grilles.Remove(cell);
        // M5 slice 6: a door frame taken apart (an airlock deconstructed into its assembly) or destroyed is no longer a frame the windows and grilles
        // beside it join, nor a door across the passage.
        if (!frameDoorLeft)
            FrameDoors.Remove(cell);
        if (!doorLeft)
        {
            Doors.Remove(cell);
            DoorFaces.Remove(cell);
        }
    }

    /// <summary>Records an entity by what it is drawn as.</summary>
    public void Add(PlanEntity e)
    {
        var cell = (e.X, e.Y, e.Z);
        if (e.TypePath == LadderHoleType)
        {
            Holes.Add(cell);
            return;
        }
        switch (PropKinds.Classify(e.TypePath, e.IconState))
        {
            case RenderKind.FullWindow:
                Windows.Add(cell);
                break;
            case RenderKind.Grille:
                Grilles.Add(cell);
                break;
            case RenderKind.Girder:
            case RenderKind.FalseWall:
                Girders.Add(cell); // a false wall hides its face against a girder or another false wall, as against a wall
                break;
            case RenderKind.Airlock:
            case RenderKind.PodDoor:
                Doors.Add(cell);
                FrameDoors.Add(cell);
                break;
            case RenderKind.Firelock:
                Doors.Add(cell);
                break;
            case RenderKind.Table:
                Tables.Add(cell);
                break;
            case RenderKind shape when BorderCorners.IsBorderShape(shape):
                Corners.Add(e.X, e.Y, e.Z, ChunkPlanner.BorderFace(e));
                if (shape == RenderKind.DirectionalWindow)
                    BorderWindows.Add((e.X, e.Y, e.Z, ChunkPlanner.BorderFace(e)));
                break;
        }
    }
}

/// <summary>
/// What <see cref="ChunkPlanner.TryPlace(PlanEntity, PlanProbe, out Placed)"/> asks about the cells around an entity. Every member but <see cref="IsWall"/> may be null.
/// </summary>
public sealed class PlanProbe
{
    /// <summary>A closed turf or a full-tile window: what a mount can sit on, and what a sign in a wall cell stands in.</summary>
    public Func<int, int, int, bool> IsWall { get; init; }

    /// <summary>A table in the cell (what lifts an item and the small machines); null: none.</summary>
    public Func<int, int, int, bool> IsTable { get; init; }

    /// <summary>An open floor (which an undirected sign in a wall cell turns toward); null: not <see cref="IsWall"/>.</summary>
    public Func<int, int, int, bool> IsOpen { get; init; }

    /// <summary>What a cell is to a door that decides which way to stand (<see cref="DoorAxis"/>); null: <see cref="IsWall"/> blocks and any other cell is open.</summary>
    public Func<int, int, int, CellClass> Classify { get; init; }

    /// <summary>Whether a window or grille arm joins this cell (a wall, a full-tile window, a grille, a door frame); null: <see cref="IsWall"/>.</summary>
    public Func<int, int, int, bool> Connects { get; init; }

    /// <summary>The face of the plane of the door already placed in a cell (0 for none): a door beside it shares its axis when its own cells do not decide.</summary>
    public Func<int, int, int, byte> DoorFace { get; init; }

    /// <summary>
    /// M5 slice 5: a full-tile window in the cell (null: none known). Between two full-tile windows tg's own smoothing junction decides the arm (<see cref="WindowJunction"/>):
    /// an unanchored window is not smoothed with by its neighbours (icon_smoothing.dm:201), so it shows as loose.
    /// </summary>
    public Func<int, int, int, bool> IsFullWindow { get; init; }

    /// <summary>Where the border pieces meet (null: no corner posts).</summary>
    public BorderCorners Corners { get; init; }

    internal Func<int, int, int, bool> Open => IsOpen ?? ((x, y, z) => !IsWall(x, y, z));

    internal Func<int, int, int, CellClass> Class => Classify ?? ((x, y, z) => IsWall(x, y, z) ? CellClass.Blocking : Open(x, y, z) ? CellClass.Open : CellClass.Void);

    internal Func<int, int, int, bool> Joins => Connects ?? IsWall;
}

/// <summary>Plans the world's chunks.</summary>
public static class ChunkPlanner
{
    /// <summary>Tiles per chunk side (scope 2i: 16 x 16, 256 chunks on Meta).</summary>
    public const int ChunkSize = 16;

    /// <summary>The chunk column of a BYOND x.</summary>
    public static int ChunkIndex(int cell) => (cell - 1) / ChunkSize;

    /// <summary>The face a border piece stands on: its dir when that is a cardinal one, else SOUTH.</summary>
    public static byte BorderFace(PlanEntity e) => WallMounts.IsCardinal(e.Dir) ? e.Dir : ByondDirConst.South;

    /// <summary>
    /// Plans the map: every non-space cell becomes tiles (a wall box, or a floor and a ceiling), every entity a placed object
    /// by its type (<see cref="PropKinds"/>) and, when it stands beside a wall that its pixel offset or type points at, a wall decal
    /// or fixture (<see cref="WallMounts"/>). An entity with no picture and no shape draws nothing. The doors are turned across their passage
    /// (<see cref="DoorAxis"/>), the full-tile windows and grilles get their arms and the border pieces their corner posts (<see cref="Joints"/>).
    /// </summary>
    /// <summary>
    /// D325: the ceiling of a cell under a level of its multi-z stack is the underside of the turf above (tg has no ceiling: the floor
    /// above is the boundary), drawn as a plating ceiling; open space above leaves the cell open to the level above.
    /// </summary>
    /// <summary>A wall's <see cref="TileCell.NearStack"/>: <paramref name="stacked"/>(x, y, z) says the wall there has a wall straight above it.</summary>
    public static byte NearStackOf(int x, int y, int z, Func<int, int, int, bool> stacked)
    {
        int bits = 0;
        foreach ((int dx, int dy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
        {
            int dir = BlockFaces.DirOf(dx, dy);
            if (stacked(x + dx, y + dy, z)) bits |= dir;
            if (stacked(x + dx, y + dy, z - 1)) bits |= dir << 4;
        }
        return (byte)bits;
    }

    /// <summary>A wall's <see cref="TileCell.Stack"/>: whether walls stand straight above and below it on its stack.</summary>
    public static byte WallStack(bool above, bool below) => (byte)((above ? Smoothing.Above : 0) | (below ? Smoothing.Below : 0));

    // The underside of the turf of the level above, over an open cell: plating, which a floor tile is laid on and a wall stands on (the owner, 2026-11-17:
    // a wall takes the place of the floor's tile at most, never of its plating; from 2026-11-10 a wall above had shown its own picture there).
    // Under a glass floor there is none: the pane is drawn to both sides (TurfStyle.PlatingUnder).
    public static TileCell UnderLevelAbove(int x, int y, int z, int type)
        => new(x, y, z, TileShape.Ceiling, TableRef.None, type) { Ceiling = Anomaly.Core.Motion.CeilingState.Of(Anomaly.Core.Motion.CeilingState.Plating) };

    public static WorldPlan Plan(MapGrid map, Func<int, int, int, TableRef> turfAppearance, IEnumerable<PlanEntity> entities, PlanOptions options = null)
    {
        options ??= new PlanOptions();
        var plan = new WorldPlan(map.Width, map.Height, map.Levels);
        ushort[] cells = map.ExpandCells();
        var kinds = new TurfKind[map.TurfTypes.Count];
        for (int i = 0; i < kinds.Length; i++)
            kinds[i] = TurfStyle.Classify(map.TurfTypes[i]);

        var entityList = entities as IReadOnlyList<PlanEntity> ?? entities.ToList();
        PlanIndex index = plan.Index;
        foreach (PlanEntity e in entityList)
            index.Add(e);
        bool InMap(int x, int y, int z) => x >= 1 && y >= 1 && z >= 1 && x <= map.Width && y <= map.Height && z <= map.Levels;
        TurfKind KindAt(int x, int y, int z) => kinds[cells[(x - 1) + (y - 1) * map.Width + (z - 1) * map.Width * map.Height]];
        bool IsClosedTurf(int x, int y, int z) => InMap(x, y, z) && KindAt(x, y, z) == TurfKind.Closed;
        bool IsOpenTurf(int x, int y, int z) => InMap(x, y, z) && KindAt(x, y, z) == TurfKind.Open;
        bool IsWall(int x, int y, int z) => IsClosedTurf(x, y, z) || index.Windows.Contains((x, y, z));
        byte[] solid = map.SolidBits;
        bool IsSolidBit(int x, int y, int z)
        {
            int i = (x - 1) + (y - 1) * map.Width + (z - 1) * map.Width * map.Height;
            return solid != null && (i >> 3) < solid.Length && (solid[i >> 3] & (1 << (i & 7))) != 0;
        }
        CellClass Classify(int x, int y, int z)
        {
            if (!InMap(x, y, z))
                return CellClass.Void;
            var cell = (x, y, z);
            if (KindAt(x, y, z) == TurfKind.Closed || index.Windows.Contains(cell) || index.Grilles.Contains(cell) || index.Doors.Contains(cell) || IsSolidBit(x, y, z))
                return CellClass.Blocking;
            return KindAt(x, y, z) == TurfKind.Open ? CellClass.Open : CellClass.Void;
        }
        bool Connects(int x, int y, int z)
            => IsClosedTurf(x, y, z) || index.Windows.Contains((x, y, z)) || index.Grilles.Contains((x, y, z)) || index.FrameDoors.Contains((x, y, z));
        foreach (KeyValuePair<(int X, int Y, int Z), byte> face in DoorAxis.ResolveAll(index.Doors.Where(d => InMap(d.X, d.Y, d.Z)), Classify))
            index.DoorFaces[face.Key] = face.Value;

        var ceilings = map.Ceilings.ToDictionary(c => (c.X, c.Y, c.Z));
        int cellIndex = 0;
        for (int z = 1; z <= map.Levels; z++)
        {
            for (int y = 1; y <= map.Height; y++)
            {
                for (int x = 1; x <= map.Width; x++, cellIndex++)
                {
                    int type = cells[cellIndex];
                    bool hatchAbove = index.Holes.Contains((x, y, z + 1)); // DV397: the ceiling under a ladder's hatch has its opening
                    if (options.Ceilings && ceilings.TryGetValue((x, y, z), out var ceiling) && ceiling.State.Exists && kinds[type] != TurfKind.Closed)
                        plan.Chunk(ChunkIndex(x), ChunkIndex(y), z).Tiles.Add(new TileCell(x, y, z, TileShape.Ceiling, ceiling.Look, type) { Ceiling = ceiling.State, Hatch = hatchAbove });
                    else if (options.Ceilings && type < kinds.Length && kinds[type] != TurfKind.Closed && z < map.Levels && map.LinkedBelowLevels.Contains(z + 1)
                        && cells[cellIndex + map.Width * map.Height] < map.TurfTypes.Count && TurfStyle.PlatingUnder(map.TurfTypes[cells[cellIndex + map.Width * map.Height]]))
                        plan.Chunk(ChunkIndex(x), ChunkIndex(y), z).Tiles.Add(UnderLevelAbove(x, y, z, type) with { Hatch = hatchAbove });
                    if (type >= kinds.Length || kinds[type] == TurfKind.Void)
                        continue;
                    ChunkPlan chunk = plan.Chunk(ChunkIndex(x), ChunkIndex(y), z);
                    TableRef appearance = turfAppearance == null ? TableRef.None : turfAppearance(x, y, z);
                    if (kinds[type] == TurfKind.Closed)
                    {
                        int level = map.Width * map.Height;
                        bool roofed = z < map.Levels && map.LinkedBelowLevels.Contains(z + 1) && cells[cellIndex + level] < kinds.Length && TurfStyle.ShowsUnderside(kinds[cells[cellIndex + level]]);
                        bool Closed(int cx, int cy, int cz) => cx >= 1 && cy >= 1 && cx <= map.Width && cy <= map.Height && cz >= 1 && cz <= map.Levels
                            && cells[(cz - 1) * level + (cy - 1) * map.Width + (cx - 1)] is var t && t < kinds.Length && kinds[t] == TurfKind.Closed;
                        // (a wall that does not smooth, tg's indestructible ones, joins nothing above or below, as it joins nothing along its row)
                        bool Joins(int cx, int cy, int cz) => Closed(cx, cy, cz) && (options.Smooths == null || options.Smooths(cx, cy, cz));
                        bool Stacked(int cx, int cy, int cz) => cz >= 1 && cz < map.Levels && map.LinkedBelowLevels.Contains(cz + 1) && Joins(cx, cy, cz) && Joins(cx, cy, cz + 1);
                        bool above = Stacked(x, y, z), below = Stacked(x, y, z - 1);
                        chunk.Tiles.Add(new TileCell(x, y, z, TileShape.Wall, appearance, type)
                        {
                            Stack = WallStack(above, below), Roofless = !roofed, NearStack = NearStackOf(x, y, z, Stacked), Diagonals = (byte)BlockFaces.DiagonalsOf(x, y, z, Joins),
                        });
                        continue;
                    }
                    chunk.Tiles.Add(new TileCell(x, y, z, TileShape.Floor, appearance, type) { Hatch = index.Holes.Contains((x, y, z)) });

                }
            }
        }

        var probe = new PlanProbe
        {
            IsWall = IsWall,
            IsTable = (x, y, z) => index.Tables.Contains((x, y, z)),
            IsOpen = IsOpenTurf,
            Classify = Classify,
            Connects = Connects,
            DoorFace = (x, y, z) => index.DoorFaces.TryGetValue((x, y, z), out byte face) ? face : (byte)0,
            Corners = index.Corners,
            IsFullWindow = (x, y, z) => index.Windows.Contains((x, y, z)),
        };
        foreach (PlanEntity e in entityList)
        {
            if (!InMap(e.X, e.Y, e.Z))
                continue;
            if (!TryPlace(e, probe, out Placed placed))
                continue;
            plan.Chunk(ChunkIndex(e.X), ChunkIndex(e.Y), e.Z).Placed.Add(placed);
        }
        plan.Chunks.Sort((a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.ChunkY != b.ChunkY ? a.ChunkY.CompareTo(b.ChunkY) : a.ChunkX.CompareTo(b.ChunkX));
        return plan;
    }

    /// <summary>How one entity is drawn; false when it draws nothing.</summary>
    public static bool TryPlace(PlanEntity e, Func<int, int, int, bool> isWall, out Placed placed) => TryPlace(e, new PlanProbe { IsWall = isWall }, out placed);

    /// <summary>How one entity is drawn (no table lookup): <paramref name="isWallLike"/> is "a wall, a window or a door"; false when it draws nothing.</summary>
    public static bool TryPlace(PlanEntity e, Func<int, int, int, bool> isWall, Func<int, int, int, bool> isWallLike, out Placed placed)
        => TryPlace(e, new PlanProbe { IsWall = isWall, Connects = isWallLike, Classify = (x, y, z) => isWallLike(x, y, z) ? CellClass.Blocking : CellClass.Open }, out placed);

    /// <summary>How one entity is drawn (no open-floor lookup: every non-wall tile is open); false when it draws nothing.</summary>
    public static bool TryPlace(PlanEntity e, Func<int, int, int, bool> isWall, Func<int, int, int, bool> isWallLike, Func<int, int, int, bool> isTable, out Placed placed)
        => TryPlace(e, isWall, isWallLike, isTable, null, out placed);

    /// <summary>
    /// How one entity is drawn; false when it draws nothing. <paramref name="isWallLike"/> is "a wall, a window or a door" for the shapes that stand across a corridor
    /// (the older form of <see cref="PlanProbe.Classify"/>: a wall-like cell blocks, any other is open). <paramref name="isTable"/> (null: none) says a tile has a table, whose top
    /// lifts the items and small machines that stand on it (<see cref="PropKinds.StandsOnTable"/>, <see cref="WorldGeometry.TableTop"/>). <paramref name="isOpen"/> (null: "not a
    /// wall") says a tile is an open floor, which an undirected sign in a wall cell is turned toward (<see cref="WallMounts.TryPlaceInWall"/>).
    /// </summary>
    public static bool TryPlace(PlanEntity e, Func<int, int, int, bool> isWall, Func<int, int, int, bool> isWallLike, Func<int, int, int, bool> isTable, Func<int, int, int, bool> isOpen, out Placed placed)
        => TryPlace(e, new PlanProbe
        {
            IsWall = isWall,
            IsTable = isTable,
            IsOpen = isOpen,
            Connects = isWallLike,
            Classify = (x, y, z) => isWallLike(x, y, z) ? CellClass.Blocking : (isOpen ?? ((cx, cy, cz) => !isWall(cx, cy, cz)))(x, y, z) ? CellClass.Open : CellClass.Void,
        }, out placed);

    /// <summary>The farthest a billboard's pixel offset moves it from its tile's centre (metres): it stays inside its own tile, not in the wall beside it.</summary>
    public const float MaxGroundOffset = 0.9f;

    /// <summary>
    /// How one entity is drawn against the cells around it; false when it draws nothing. A door's <see cref="Placed.Face"/> is NORTH when its plane runs along x (the
    /// walls, or what blocks, east and west of it) and EAST when it runs along y (<see cref="DoorAxis"/>); a full-tile window or grille gets its <see cref="Placed.Joints"/>
    /// (<see cref="Joints.Arms"/>) and no face; a border piece its dir's face and its corner posts. An item (<see cref="PropKinds.IsItem"/>) is placed at its support: the
    /// table top when the tile has a table, else the floor, and its ground offset is tg's own, not scaled with it (D329). A thing on tg's
    /// floor layers (<see cref="FloorLayers"/>: the topdown block, the pipes, the vents) is a <see cref="PlacedKind.FloorDecal"/> at the height its layer gives.
    /// </summary>
    public static bool TryPlace(PlanEntity e, PlanProbe probe, out Placed placed, WorldLooks looks = null)
    {
        if (e.TypePath == PlanIndex.LadderHoleType)
        {
            placed = default;
            return false; // the hole in the floor is the hatch cut out of it (PlanIndex.Holes, TileCell.Hatch)
        }
        RenderKind shape = PropKinds.Classify(e.TypePath, e.IconState);
        float offsetX = e.PixelX * (TileSpace.TileSize / 32f), offsetY = e.PixelY * (TileSpace.TileSize / 32f);
        if (shape == RenderKind.Billboard)
        {
            if (e.Appearance.IsNone)
            {
                placed = default;
                return false;
            }
            // The type's own look (WorldLooks, the dev menu's tuning): drawn standing or flat whatever its layer, its size and its move on the ground.
            WorldLook look = (looks ?? WorldLooks.Active).Find(e.TypePath);
            // A thing of tg's floor layers (D122) lies flat on its tile, stacked by its layer: the lattice, the disposal pipes, the wires and the other topdown things, the pipes,
            // the vents, the scrubbers (D121's hatches), the filters, the pumps. It is no wall furniture and no standing picture.
            float floorLift = FloorLayers.TunedLift;
            if (look.Mode == WorldLookMode.Floor || look.Mode == WorldLookMode.Auto && FloorLayers.TryLift(e.Plane, e.Layer, e.TypePath, out floorLift, e.IconState))
            {
                placed = new Placed(e.Id, PlacedKind.FloorDecal, RenderKind.Billboard, e.X, e.Y, e.Z, e.Dir, 0, floorLift,
                    Math.Clamp(offsetX, -MaxGroundOffset, MaxGroundOffset) + look.X, Math.Clamp(offsetY, -MaxGroundOffset, MaxGroundOffset) + look.Z, e.Appearance, e.Light, e.IconState,
                    Scale: look.Size);
                return true;
            }
            // Wall furniture that stands in the wall cell itself (an undirected sign) goes on the face of its own cell toward the open side; it comes first,
            // because from inside a wall every neighbour that is a wall would otherwise make it a mount on that wall.
            if (WallMounts.TryPlaceInWall(e.TypePath, e.X, e.Y, e.Z, e.Dir, probe.IsWall, probe.Open, out byte inFace, out float inHeight))
            {
                placed = new Placed(e.Id, PlacedKind.WallDecal, RenderKind.Billboard, e.X, e.Y, e.Z, e.Dir, inFace, inHeight, 0f, 0f, e.Appearance, e.Light, e.IconState, InWall: true);
                return true;
            }
            if (WallMounts.TryPlace(e.TypePath, e.X, e.Y, e.Z, e.Dir, e.PixelX, e.PixelY, probe.IsWall, out byte face, out float height, out float along))
            {
                placed = new Placed(e.Id, PlacedKind.WallDecal, RenderKind.Billboard, e.X, e.Y, e.Z, e.Dir, face, height, along, 0f, e.Appearance, e.Light, e.IconState,
                    Fin: PropKinds.IsWallFin(e.TypePath));
                return true;
            }
            // The root's pixel offset is a ground offset (east = +x, north = -z in the renderer), kept inside the tile; the picture does not carry it.
            // A thing that shares its tile with a table stands on the table top, as tg draws it. An item is drawn at the item scale (D121); its offset
            // is where tg put it on the tile, not scaled (D329: the place a hand put it on a table is where it lies).
            float lift = probe.IsTable != null && PropKinds.StandsOnTable(e.TypePath) && probe.IsTable(e.X, e.Y, e.Z) ? WorldGeometry.TableTop : 0f;
            bool item = PropKinds.IsItem(e.TypePath);
            PlacedKind standing = item ? PlacedKind.Item : lift > 0f || PropKinds.RestsOnFloor(e.TypePath) ? PlacedKind.Resting : PlacedKind.Billboard;
            placed = new Placed(e.Id, standing, RenderKind.Billboard, e.X, e.Y, e.Z, e.Dir, 0, lift,
                Math.Clamp(offsetX, -MaxGroundOffset, MaxGroundOffset) + look.X, Math.Clamp(offsetY, -MaxGroundOffset, MaxGroundOffset) + look.Z, e.Appearance, e.Light, e.IconState,
                Scale: look.Size);
            return true;
        }

        byte pieceFace = 0;
        byte joints = 0;
        float pieceHeight = 0f;
        switch (shape)
        {
            case RenderKind.LightFixture:
                // A fixture sits on the wall its pixel offset or its dir points at; without a wall there it keeps the dir's face.
                if (WallMounts.TryPlace(e.TypePath, e.X, e.Y, e.Z, e.Dir, e.PixelX, e.PixelY, probe.IsWall, out pieceFace, out pieceHeight, out float alongLight))
                    offsetX = alongLight;
                else
                {
                    pieceFace = WallMounts.IsCardinal(e.Dir) ? e.Dir : ByondDirConst.North;
                    pieceHeight = PropKinds.MountHeight(e.TypePath);
                }
                offsetY = 0f;
                break;
            case RenderKind.DirectionalWindow:
            case RenderKind.Railing:
            case RenderKind.FirelockBorder:
                // A border piece stands on the edge its dir faces; where a piece of the other axis ends at one of its corners it gets a post there.
                pieceFace = WallMounts.IsCardinal(e.Dir) ? e.Dir : ByondDirConst.South;
                joints = probe.Corners?.PostsFor(e.X, e.Y, e.Z, pieceFace) ?? 0;
                offsetX = offsetY = 0f;
                break;
            case RenderKind.Flap:
            case RenderKind.Windoor:
                pieceFace = WallMounts.IsCardinal(e.Dir) ? e.Dir : ByondDirConst.South;
                offsetX = offsetY = 0f;
                break;
            case RenderKind.FullWindow:
            case RenderKind.Grille:
            case RenderKind.Girder:
            case RenderKind.FalseWall:
                // Joined in 3D (D121): a centre post and an arm toward each neighbour it joins.
                joints = Joints.Arms(e.X, e.Y, e.Z, shape == RenderKind.FullWindow ? WindowJunction.Joins(e, probe) : probe.Joins, probe.Class);
                offsetX = offsetY = 0f;
                break;
            case RenderKind.Airlock:
            case RenderKind.PodDoor:
            case RenderKind.Firelock:
            case RenderKind.DoorAssembly:
            case RenderKind.Curtain:
                // Across the passage it closes (D121): its own cell's face when the plan already decided it, else the rule (and the doors beside it).
                pieceFace = probe.DoorFace?.Invoke(e.X, e.Y, e.Z) ?? 0;
                if (pieceFace == 0)
                    pieceFace = DoorAxis.Choose(e.X, e.Y, e.Z, probe.Class, probe.DoorFace, e.Dir);
                offsetX = offsetY = 0f;
                break;
            default:
                offsetX = offsetY = 0f;
                break;
        }
        placed = new Placed(e.Id, PlacedKind.Piece, shape, e.X, e.Y, e.Z, e.Dir, pieceFace, pieceHeight, offsetX, offsetY, e.Appearance, e.Light, e.IconState, Joints: joints);
        return true;
    }
}

/// <summary>Wall-mounted objects (scope 2e): which wall face a picture goes on, and at what height.</summary>
public static class WallMounts
{
    /// <summary>Pixels of offset toward a neighbour from which an entity counts as mounted on it (scope 2e).</summary>
    public const int MinOffsetPixels = 16;

    /// <summary>True for NORTH, SOUTH, EAST and WEST.</summary>
    public static bool IsCardinal(byte dir) => dir is ByondDirConst.North or ByondDirConst.South or ByondDirConst.East or ByondDirConst.West;

    /// <summary>
    /// The axis of a thing that stands across a passage: NORTH (its plane runs along x, so it spans from the wall on the west to the wall on the east) or EAST (its plane
    /// runs along y). A cell for which <paramref name="isWallLike"/> is true blocks, any other is open: the rule is <see cref="DoorAxis.Choose"/>, which scores both axes
    /// (M3 R3 replaced the old test that looked at walls only and fell back to NORTH).
    /// </summary>
    public static byte AxisFace(int x, int y, int z, Func<int, int, int, bool> isWallLike)
        => DoorAxis.Choose(x, y, z, (cx, cy, cz) => isWallLike(cx, cy, cz) ? CellClass.Blocking : CellClass.Open);

    /// <summary>The (dx, dy) of a cardinal BYOND direction (north is +y).</summary>
    public static (int Dx, int Dy) Step(byte face) => face switch
    {
        ByondDirConst.North => (0, 1),
        ByondDirConst.South => (0, -1),
        ByondDirConst.East => (1, 0),
        _ => (-1, 0),
    };

    /// <summary>The cardinal direction of a one-tile step (<see cref="Step"/> backwards): what a mount in a cell faces to hang on the wall cell that far from it; 0 for no step.</summary>
    public static byte DirOfStep(int dx, int dy) => (dx, dy) switch
    {
        (0, 1) => ByondDirConst.North,
        (0, -1) => ByondDirConst.South,
        (1, 0) => ByondDirConst.East,
        (-1, 0) => ByondDirConst.West,
        _ => (byte)0,
    };

    /// <summary>
    /// The wall face of an entity on tile (<paramref name="x"/>, <paramref name="y"/>), or false when it is not mounted. Its pixel offset
    /// decides when it is at least <see cref="MinOffsetPixels"/> toward a neighbour (tg's directional helpers: <c>/directional/north</c>
    /// is 32 pixels north for a sign, 26 for a fire alarm, 29 for an extinguisher cabinet; the offsets come from tg's own classes); a wall
    /// furniture type without such an offset (a light or a camera, which tg draws by its dir: <see cref="PropKinds.IsWallMountType"/>) uses its dir.
    /// A seat, a sink, a shower and the like (<see cref="PropKinds.IsNeverDecal"/>) is never a mount. The
    /// neighbour must be a wall (<paramref name="isWall"/>): a mount beside open floor is just a picture. <paramref name="along"/> is
    /// the offset in metres along the wall (east for a north or south face, north for an east or west one).
    /// </summary>
    public static bool TryPlace(string typePath, int x, int y, int z, byte dir, int pixelX, int pixelY, Func<int, int, int, bool> isWall, out byte face, out float height, out float along)
    {
        face = 0;
        height = 0f;
        along = 0f;
        if (PropKinds.IsNeverDecal(typePath))
            return false;
        int ax = Math.Abs(pixelX), ay = Math.Abs(pixelY);
        // Two readings of which face it is mounted on, the offset's first: a pixel offset of 16 or more toward a neighbour, then the dir of wall furniture
        // that tg draws by it. A camera facing north with pixel_x 16 sits ALONG the north wall, half a tile east of the middle: the east neighbour is not its wall.
        byte byOffset = 0, byDir = 0;
        if (Math.Max(ax, ay) >= MinOffsetPixels)
            byOffset = ax >= ay ? (pixelX > 0 ? ByondDirConst.East : ByondDirConst.West) : (pixelY > 0 ? ByondDirConst.North : ByondDirConst.South);
        if (IsCardinal(dir) && PropKinds.IsWallMountType(typePath))
            byDir = dir;
        byte candidate = 0;
        foreach (byte c in new[] { byOffset, byDir })
        {
            if (c == 0)
                continue;
            (int dx, int dy) = Step(c);
            if (isWall(x + dx, y + dy, z))
            {
                candidate = c;
                break;
            }
        }
        if (candidate == 0)
            return false;
        face = candidate;
        height = PropKinds.MountHeight(typePath);
        along = (candidate is ByondDirConst.North or ByondDirConst.South ? pixelX : pixelY) * (TileSpace.TileSize / 32f);
        return true;
    }

    private static readonly byte[] FaceOrder = { ByondDirConst.South, ByondDirConst.North, ByondDirConst.East, ByondDirConst.West };

    /// <summary>
    /// The face of the wall cell itself for wall furniture that stands IN it: tg's signs, status displays and notice boards without the
    /// directional helpers are mapped on the wall turf, where BYOND draws them over the wall (its south face, the front of the oblique view). The
    /// face is the way the entity's <c>dir</c> points when that side is open floor, else the first open side in the order south, north, east, west.
    /// False when the type is not wall furniture, the cell is not a wall, or no side is open.
    /// </summary>
    public static bool TryPlaceInWall(string typePath, int x, int y, int z, byte dir, Func<int, int, int, bool> isWall, Func<int, int, int, bool> isOpen, out byte face, out float height)
    {
        face = 0;
        height = 0f;
        if (!PropKinds.IsWallMountType(typePath) || PropKinds.IsNeverDecal(typePath) || !isWall(x, y, z))
            return false;
        if (IsCardinal(dir))
        {
            (int dx, int dy) = Step(dir);
            if (isOpen(x + dx, y + dy, z))
            {
                face = dir;
                height = PropKinds.MountHeight(typePath);
                return true;
            }
        }
        foreach (byte candidate in FaceOrder)
        {
            (int dx, int dy) = Step(candidate);
            if (isOpen(x + dx, y + dy, z))
            {
                face = candidate;
                height = PropKinds.MountHeight(typePath);
                return true;
            }
        }
        // No floor beside it (a sign on the outside of the hull): any side that is not a wall, space included.
        foreach (byte candidate in FaceOrder)
        {
            (int dx, int dy) = Step(candidate);
            if (!isWall(x + dx, y + dy, z))
            {
                face = candidate;
                height = PropKinds.MountHeight(typePath);
                return true;
            }
        }
        return false;
    }

    /// <summary>How far in front of the wall face a decal's picture floats (metres), so that it does not fight with the wall.</summary>
    public const float DecalLift = 0.012f;

    /// <summary>
    /// Where a wall decal's quad goes in the renderer's space (metres; X east, Y up, Z south, tile centres from <see cref="TileSpace"/>): its
    /// origin (the middle of the picture's height, on the face of the wall cell its <see cref="Placed.Face"/> names, lifted by
    /// <see cref="DecalLift"/>, moved <see cref="Placed.OffsetX"/> along the wall) and the yaw that turns its +Z (the picture's front) into the room.
    /// <paramref name="pictureHeight"/> is the drawn picture's height in metres (the mount height is moved so that the whole picture stays on the face).
    /// The picture itself carries no root pixel offset (M3 R2), so this is the one place the offset along the wall is applied.
    /// </summary>
    public static (Vector3 Origin, float Yaw) DecalPlacement(Placed p, float pictureHeight)
    {
        (int dx, int dy) = Step(p.Face);
        // The face plane is 1 m from the entity's own cell centre toward Face (the neighbouring wall, or for furniture in the wall cell the open side); the
        // picture's front looks away from the wall: back toward the tile (-Face) for a mount beside a wall, out of the wall cell (+Face) for one in it.
        var step = new Vector3(dx, 0, -dy);
        Vector3 facing = p.InWall ? step : -step;
        float yaw = MathF.Atan2(facing.X, facing.Z);
        var along = p.Face is ByondDirConst.North or ByondDirConst.South ? new Vector3(p.OffsetX, 0, 0) : new Vector3(0, 0, -p.OffsetX);
        float mountHeight = WorldScale.FitMount(p.Height, pictureHeight, WorldScale.WallHeight);
        var centre = new Vector3(TileSpace.WorldX(p.X), TileSpace.WorldY(p.Z), TileSpace.WorldZ(p.Y));
        return (centre + step * (TileSpace.TileSize / 2f) + facing * DecalLift + along + new Vector3(0, mountHeight, 0), yaw);
    }

    /// <summary>
    /// The direction of tg's picture a fin (<see cref="Placed.Fin"/>) shows: a side view, the east picture (tg draws a camera on the east wall at the tile's
    /// east edge, its lens pointing west) on a north or east wall, the west one on a south or west wall. Its mount is at the picture's right edge in the
    /// east picture, at its left edge in the west one.
    /// </summary>
    public static byte FinPictureDir(byte face) => face is ByondDirConst.North or ByondDirConst.East ? ByondDirConst.East : ByondDirConst.West;

    /// <summary>
    /// Where a fin's quad goes (metres, the renderer's space as <see cref="DecalPlacement"/>) and its yaw. The quad stands at a right angle to the
    /// wall, on the vertical plane through the wall's normal, its picture's mount edge (<see cref="FinPictureDir"/>; the edge of tg's 32 pixel frame)
    /// on the wall face: the frame's centre is the tile's centre at the mount height, moved <see cref="Placed.OffsetX"/> along the wall. Its front faces
    /// east for a north or south wall and south for an east or west one, the side from which the picture reads the right way round (it is drawn on
    /// both sides). It never turns: the camera's pan is its picture's own frames, played as tg plays them.
    /// </summary>
    public static (Vector3 Origin, float Yaw) FinPlacement(Placed p, float pictureHeight)
    {
        Vector3 front = p.Face is ByondDirConst.North or ByondDirConst.South ? new Vector3(1, 0, 0) : new Vector3(0, 0, 1);
        var along = p.Face is ByondDirConst.North or ByondDirConst.South ? new Vector3(p.OffsetX, 0, 0) : new Vector3(0, 0, -p.OffsetX);
        float mountHeight = WorldScale.FitMount(p.Height, pictureHeight, WorldScale.WallHeight);
        var centre = new Vector3(TileSpace.WorldX(p.X), TileSpace.WorldY(p.Z), TileSpace.WorldZ(p.Y)) + along + new Vector3(0, mountHeight, 0);
        return (centre, MathF.Atan2(front.X, front.Z));
    }
}

/// <summary>
/// A wall picture tg splits over neighbouring tiles (the station maps' left and right halves, signs_maps.dm), seen in 3D (the owner, 2026-10-17): tg's top-down view
/// reads a run of pictures west to east, left to right. On a wall's face that looks north (a mount on the wall south of its tile) the viewer in the room looks south,
/// with the west on the right, so the run would show its parts in the reverse order, each part's joined edge away from its neighbour. The run is mirrored instead:
/// each part takes the place of its mirror in the run, and the picture reads as tg's again.
/// </summary>
public static class SplitPictures
{
    /// <summary>
    /// How many tiles east the part at <paramref name="x"/> moves: <paramref name="edges"/> tells, for the decal on the same wall face at a column, whether its picture
    /// reaches the west and east edge of its frame (null: none there). The run is the parts joined at touching edges; a part not in a run does not move.
    /// </summary>
    public static int MirrorShift(int x, Func<int, (bool West, bool East)?> edges)
    {
        int x0 = x, x1 = x;
        while (edges(x0) is { West: true } && edges(x0 - 1) is { East: true })
            x0--;
        while (edges(x1) is { East: true } && edges(x1 + 1) is { West: true })
            x1++;
        return x0 + x1 - 2 * x;
    }
}
