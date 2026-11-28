using System.Linq;
using System.Diagnostics;
using Godot;
using Anomaly.Core.Motion;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Models;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client;

/// <summary>
/// The world at MetaStation scale (M3 P6, D113, docs/ai/specs/apps.md 2.9), drawn in 16 x 16 tile chunks. Per chunk:
/// <list type="bullet">
/// <item><description><b>tiles</b>: one MultiMesh of floors, ceilings and walls (<see cref="WorldMeshes.TileKit"/>), tg's pictures the layers of a Texture2DArray (<see cref="TileKit"/>); a ceiling over every non-space cell only (D116: space stays dark);</description></item>
/// <item><description><b>props</b>: one MultiMesh of picture quads, the static props as fixed-Y billboards and the wall-mounted ones as flat decals on the wall face, from one shared atlas (<see cref="PropAtlas"/>);</description></item>
/// <item><description><b>pieces</b>: one MultiMesh of the shaped things (D113): windows, grilles, tables, railings, flaps, light fixtures, and the doors, whose panels slide by the open fraction the <see cref="DoorAnimation"/> gives them;</description></item>
/// <item><description>an <see cref="OccluderInstance3D"/> of the merged wall boxes.</description></item>
/// </list>
/// Chunk bounds give the frustum culling. Mobs and entities that move are nodes of the <see cref="EntityLayer"/>: this class is its
/// <see cref="IStaticPropHost"/> and hands an entity over when it moves (the static-versus-moving rule). Without the converted icons
/// (or a replica) the kit falls back to M1's tinted boxes and floor.
/// </summary>
public partial class MapRenderer : Node3D, IStaticPropHost
{
    private const int RebuildsPerFrame = 6;
    // Chunks whose floors and walls are made again in one frame after turfs changed (tg's ChangeTurf after the join).

    private readonly Dictionary<(int, int, int), ChunkBatch> _batches = new();
    private readonly Dictionary<long, StaticRecord> _records = new();
    // M4a P7: the static records by cell, for the world pick (CollectStatic).
    private readonly Dictionary<(int, int, int), List<StaticRecord>> _byCell = new();
    private readonly List<StaticRecord> _doors = new();
    private readonly HashSet<StaticRecord> _animated = new(); // D142: the things whose picture animates or flicks now (StepAnimations)
    private readonly HashSet<StaticRecord> _looks = new(); // M4v: the things an animate() chain may move or tint now (StepLooks)
    private double _clock; // seconds of Update since the renderer started: the animations' clock
    private readonly HashSet<ChunkBatch> _dirty = new();
    private bool[] _wall, _open;
    private int _width, _height, _levels;
    private TileKit _tileKit;
    private PropAtlas _atlas;
    private ModelLibrary _models;
    private ArrayMesh _quadMesh, _pieceMesh;
    private ReplicaWorld _replica;
    private AppearanceTextures _textures;
    private IAppearanceSource _tables;
    private OverlayStripper _stripper;
    // M3 R6: tg's classes, from which a border piece finds its full-tile counterpart (BorderCounterparts), and what that found per type and state.
    private readonly ReflectionTypeSource _types = new();
    private readonly Dictionary<(RenderKind, string, string), (string Icon, string State)?> _borderPictures = new();
    private WorldPlan _plan;
    private long _lastScannedTick = long.MinValue;
    // The turfs as the tiles draw them (the turf type index and the appearance of each cell), the chunks whose tiles are
    // to be made again, and each chunk's node of tiles and occluder.
    private int[] _drawnType;
    private TableRef[] _drawnLook;
    private Anomaly.Core.Motion.CeilingState[] _drawnCeiling;
    private TableRef[] _drawnCeilingLook;
    private bool _pictures;
    private ArrayMesh _tileMesh;
    private readonly List<TurfKind> _kinds = new();
    private readonly HashSet<(int, int, int)> _turfDirty = new();
    // Chunks whose tiles were made while a sheet of their pictures was still loading (IconLibrary.Background): TileKit gives such a tile a flat
    // tint, so the chunk is made again when a load lands (the owner, 2026-11-18: after Play and Stop in the map editor the floors laid where
    // the map as it was received has space stayed grey).
    private readonly HashSet<(int, int, int)> _turfsAwaitingIcons = new();
    private int _turfLoadsSeen;
    // Static things spawned while an icon of theirs was still loading (IconLibrary.Background): built again when a load lands.
    private readonly HashSet<StaticRecord> _awaitingIcons = new();
    private int _iconLoadsSeen;
    private readonly Dictionary<(int, int, int), Node3D> _turfNodes = new();
    private readonly Dictionary<(int, int, int), List<TurfSlot>> _turfSlots = new();
    private readonly List<int> _changedTurfGroups = new();

    /// <summary>The centre of the map in world metres (level 1, floor height), for cameras.</summary>
    public Vector3 Center { get; private set; }

    /// <summary>The extent of the map in metres (x = width, z = depth).</summary>
    public Vector2 Extent { get; private set; }

    /// <summary>Walls drawn.</summary>
    public int WallCount { get; private set; }

    /// <summary>Floor tiles drawn.</summary>
    public int FloorCount { get; private set; }

    /// <summary>How many tile pictures (layers) are tg's, and how many are flat tints.</summary>
    public int TexturedGroups => (_tileKit?.TexturedLayers ?? 0) - (_tileKit != null && _tileKit.HasCeilingLayer ? 1 : 0);

    public int TintedGroups => _tileKit?.TintedLayers ?? 0;

    /// <summary>Chunks drawn.</summary>
    public int ChunkCount => _batches.Count;

    /// <summary>The face of the door the plan stands at a cell (<see cref="DoorAxis"/>: NORTH, EAST), 0 for no door (the light layer's <c>DoorFaceAt</c>).</summary>
    public byte DoorFaceAt(int x, int y, int z) => _plan != null && _plan.Index.DoorFaces.TryGetValue((x, y, z), out byte face) ? face : (byte)0;

    /// <summary>Static props (billboards and decals) and pieces in the batches now.</summary>
    public int PropCount => _records.Values.Count(r => r.Placed.Kind != PlacedKind.Piece);

    public int PieceCount => _records.Values.Count(r => r.Placed.Kind == PlacedKind.Piece);

    /// <summary>Props currently drawn with catalog models, for development render checks.</summary>
    public int ModelCount => _records.Values.Count(r => r.Model != null);

    /// <summary>Entries in the prop atlas and its size.</summary>
    public int AtlasEntries => _atlas?.EntryCount ?? 0;

    public Vector2I AtlasSize => _atlas?.Size ?? Vector2I.Zero;

    /// <summary>Texture2DArray layers and pages of the tile kit.</summary>
    public int TileLayers => (_tileKit?.TexturedLayers ?? 0) + (_tileKit?.TintedLayers ?? 0);

    public int TilePages => _tileKit?.PageCount ?? 0;

    /// <summary>Seconds the last <c>Build</c> took (planning, pictures, atlas, meshes).</summary>
    public double LoadSeconds { get; private set; }

    /// <summary>Entities handed from the batch to the layer because they moved.</summary>
    public int HandedOver { get; private set; }

    /// <summary>Things that arrived while their model's textures decoded and were kept to be drawn as models (not handed to the layer flat).</summary>
    public int WaitedForTextures { get; private set; }

    public event Action<ReplicaEntity> PropMoved;

    /// <summary>False: the map draws tiles only and claims no entity (the M2 showcase draws every entity as a node).</summary>
    public bool DrawProps { get; set; } = true;

    /// <summary>The M1 look: tinted boxes and floor (no appearances).</summary>
    public void Build(MapGrid map) => Build(map, null, null);

    /// <summary>The world from a replica's turf appearances, entities and tables and the pictures of the entity layer, at once.</summary>
    public void Build(MapGrid map, ReplicaWorld replica, AppearanceTextures textures)
    {
        IEnumerator<float> steps = BuildSteps(map, replica, textures);
        while (steps.MoveNext())
        {
        }
    }

    /// <summary>What <see cref="BuildSteps"/> is doing now, for the loading screen.</summary>
    public string BuildPhase { get; private set; }

    /// <summary>
    /// <see cref="Build"/> in small steps (a chunk's tiles, one thing placed, one model built), so the game's loading screen spreads it over frames
    /// and its bar moves: each step yields how far the build is, 0 to 1. Nothing else may touch the renderer or the replica between the steps.
    /// </summary>
    public IEnumerator<float> BuildSteps(MapGrid map, ReplicaWorld replica, AppearanceTextures textures)
    {
        var watch = Stopwatch.StartNew();
        BuildPhase = "Planning the map";
        _width = map.Width;
        _height = map.Height;
        _levels = map.Levels;
        _replica = replica;
        _textures = replica == null ? null : textures;
        _tables = replica == null ? null : textures?.TablesOf(replica);
        // The overlays drawn as models on what they are stuck to (a C-4 charge) are left out of the pictures of things drawn in 3D (Look).
        _stripper = _tables == null ? null : new OverlayStripper(_tables, o => IsAttachedOverlay(o) || IsWallDent(o) || IsLadderRim(o));
        _tables = _stripper ?? _tables;
        bool pictures = _tables != null && IconPaths.Exists;
        _pictures = pictures;
        bool props = pictures && DrawProps;

        var planEntities = new List<PlanEntity>();
        if (replica != null && props)
        {
            foreach (ReplicaEntity e in replica.Entities)
            {
                if (IsStaticCandidate(e))
                    planEntities.Add(ToPlan(e));
            }
        }
        _plan = ChunkPlanner.Plan(map, pictures ? replica.TurfAppearance : null, planEntities, new PlanOptions { Smooths = pictures ? Smooths : null });
        HashSet<(int, int, int)> engines = EngineCells(replica);
        foreach (ChunkPlan planned in _plan.Chunks)
            DropEngineTiles(planned, engines);
        Dev.FrameProbe.Mark("build/plan", watch);
        BuildPhase = "Laying the floors and walls";
        yield return 0.1f;

        _quadMesh = WorldMeshes.PictureQuad();
        _pieceMesh = WorldMeshes.Pieces();
        ArrayMesh tileMesh = _tileMesh = WorldMeshes.TileKit();
        _tileKit = new TileKit(pictures ? textures : null, pictures ? _tables : null) { Stripper = _stripper };
        _atlas = props ? new PropAtlas(textures) : null;
        // The 3D models that replace tg's pictures (docs/ai/specs/models.md); their meshes and textures are made on first use.
        _models = _atlas != null ? new ModelLibrary(_atlas) : null;

        // Which cells are walls, for the pick ray; the pictures of the tiles.
        _wall = new bool[map.CellCount];
        _open = new bool[map.CellCount];
        var chunkTiles = new Dictionary<ChunkPlan, List<(TileCell Tile, int Page, int Layer, int Side, Color Faces)>>();
        foreach (ChunkPlan chunk in _plan.Chunks)
        {
            var list = new List<(TileCell, int, int, int, Color)>(chunk.Tiles.Count);
            foreach (TileCell tile in chunk.Tiles)
            {
                (int page, int layer) = _tileKit.LayerOf(tile, map.TurfTypes);
                list.Add((tile, page, layer, _tileKit.SideLayerOf(tile, page, layer), FaceLayers(tile, page, layer)));
                if (_tileKit.TryUnderlayLayer(tile, out int underPage, out int underLayer))
                {
                    // the floor and the ceiling the diagonal wall's cut corner shows
                    list.Add((tile with { Shape = TileShape.Floor }, underPage, underLayer, -1, default));
                    TileCell roof = tile with { Shape = TileShape.Ceiling, Appearance = TableRef.None, Ceiling = Anomaly.Core.Motion.CeilingState.Of(Anomaly.Core.Motion.CeilingState.Plating) };
                    (int roofPage, int roofLayer) = _tileKit.LayerOf(roof, map.TurfTypes);
                    list.Add((roof, roofPage, roofLayer, -1, default));
                }
                if (tile.Shape == TileShape.Wall)
                {
                    WallCount++;
                    _wall[((tile.Z - 1) * _height + (tile.Y - 1)) * _width + (tile.X - 1)] = true;
                }
                else if (tile.Shape == TileShape.Floor)
                {
                    FloorCount++;
                    _open[((tile.Z - 1) * _height + (tile.Y - 1)) * _width + (tile.X - 1)] = true;
                }
            }
            chunkTiles[chunk] = list;
            yield return 0.1f + 0.15f * chunkTiles.Count / _plan.Chunks.Count;
        }
        _tileKit.Finish();
        Dev.FrameProbe.Mark("build/tiles", watch);
        yield return 0.26f;

        foreach (ChunkPlan chunk in _plan.Chunks)
        {
            (float bx, float by, float bz, float sx, float sy, float sz) = chunk.Bounds(_width, _height);
            var bounds = new Aabb(new Vector3(bx, by - 0.5f, bz), new Vector3(sx, sy + 1f, sz));
            var root = new Node3D { Name = $"Chunk_{chunk.ChunkX}_{chunk.ChunkY}_{chunk.Z}" };
            AddChild(root);
            var turfs = new Node3D { Name = "Turfs" };
            root.AddChild(turfs);
            AddTiles(turfs, chunkTiles[chunk], tileMesh, bounds, (chunk.ChunkX, chunk.ChunkY, chunk.Z));
            AddOccluder(turfs, chunk);
            _turfNodes[(chunk.ChunkX, chunk.ChunkY, chunk.Z)] = turfs;
            _batches[(chunk.ChunkX, chunk.ChunkY, chunk.Z)] = new ChunkBatch(chunk, root, _quadMesh, _pieceMesh, _atlas?.Material, bounds, ModelMeshAt, ModelMaterialAt);
            yield return 0.26f + 0.06f * _batches.Count / _plan.Chunks.Count;
        }

        if (_atlas != null)
        {
            // the models' textures decode on the thread pool while the records are laid (each model starts its own), and the models that
            // found theirs decoding are built after the loop, waiting for them: the join decodes in parallel, not one texture after another
            _models.WaitForTextures = false;
            BuildPhase = "Placing the things";
            int things = _plan.Chunks.Sum(c => c.Placed.Count), placedSoFar = 0;
            foreach (ChunkPlan chunk in _plan.Chunks)
            {
                ChunkBatch batch = _batches[(chunk.ChunkX, chunk.ChunkY, chunk.Z)];
                foreach (Placed placed in chunk.Placed)
                {
                    if (replica.TryGet(placed.EntityId, out ReplicaEntity entity))
                    {
                        using (Dev.FrameProbe.Measure("record", entity.TypePath))
                            AddRecord(entity, placed, batch);
                    }
                    yield return 0.32f + 0.5f * ++placedSoFar / Math.Max(1, things);
                }
                AttachTurfs((chunk.ChunkX, chunk.ChunkY, chunk.Z), chunk.Tiles, batch);
                yield return 0.32f + 0.5f * placedSoFar / Math.Max(1, things);
            }
            Dev.FrameProbe.Mark("build/records", watch);
            BuildPhase = "Building the models";
            _models.WaitForTextures = true;
            List<StaticRecord> awaiting = _awaitingIcons.ToList();
            for (int i = 0; i < awaiting.Count; i++)
            {
                _awaitingIcons.Remove(awaiting[i]);
                BuildInstances(awaiting[i]);
                yield return 0.82f + 0.1f * (i + 1) / awaiting.Count;
            }
            _models.WaitForTextures = false; // in play a model whose textures are not in waits for them off the frame (RetryAwaitingIcons)
            Dev.FrameProbe.Mark("build/models", watch);
            BuildPhase = "Packing the pictures";
            int rebuilt = 0;
            foreach (ChunkBatch batch in _batches.Values)
            {
                batch.Rebuild();
                yield return 0.92f + 0.06f * ++rebuilt / _batches.Count;
            }
            _dirty.Clear();
            _atlas.Flush();
            Dev.FrameProbe.Mark("build/atlas", watch);
        }

        FollowTurfs(map);

        Extent = new Vector2(map.Width * TileSpace.TileSize, map.Height * TileSpace.TileSize);
        Center = new Vector3(Extent.X / 2f, 0, -Extent.Y / 2f);
        LoadSeconds = watch.Elapsed.TotalSeconds;
        BuildPhase = null;
        yield return 1f;
    }

    // ---- the third-person view's line of sight ----

    private int _sightVersion = -1;
    private int _sightHideAbove;
    private readonly HashSet<(int, int)> _chunksInSight = new();

    /// <summary>
    /// Third-person play (FovMask): a chunk with nothing in the own mob's sight, and not next to one that has, is not drawn (its
    /// tiles would be in the dark anyway); the chunks of other levels neither. Null draws every chunk of <paramref name="level"/>
    /// (the first-person, ghost and free views: the frustum, the far plane and the occluders cull as before), or of every level for
    /// 0. M10 slice G (D272): tg's levels are separate worlds unless multi-z links them (D108 loads none that do), so the station does
    /// not see the reserved level's shuttles and transit above it, nor a shuttle's passengers the station below.
    /// <paramref name="hideAbove"/> (the third-person view, the owner 2026-11-13): the levels of the stack above it are not drawn, so they do not cover the own level; 0 draws them.
    /// </summary>
    public void SetSight(FovMask mask, int level = 0, int hideAbove = 0)
    {
        if (mask == null)
        {
            _sightVersion = -1;
            foreach (KeyValuePair<(int X, int Y, int Z), ChunkBatch> kv in _batches)
            {
                bool show = (level == 0 || (_replica == null ? kv.Key.Z == level : _replica.SameStack(kv.Key.Z, level))) // D325: the own stack
                            && (hideAbove == 0 || kv.Key.Z <= hideAbove);
                if (kv.Value.Root.Visible != show)
                    kv.Value.Root.Visible = show;
            }
            SyncMaptextSight();
            return;
        }
        if (mask.Version == _sightVersion && hideAbove == _sightHideAbove)
            return;
        _sightVersion = mask.Version;
        _sightHideAbove = hideAbove;
        _chunksInSight.Clear();
        for (int j = 0; j < mask.Side; j++)
        {
            for (int i = 0; i < mask.Side; i++)
            {
                int x = mask.OriginX + i, y = mask.OriginY + j;
                if (mask.CellVisible(x, y, mask.Z))
                    _chunksInSight.Add((ChunkPlanner.ChunkIndex(x), ChunkPlanner.ChunkIndex(y)));
            }
        }
        foreach (KeyValuePair<(int X, int Y, int Z), ChunkBatch> kv in _batches)
        {
            bool show = false;
            if ((_replica == null ? kv.Key.Z == mask.Z : _replica.SameStack(kv.Key.Z, mask.Z)) && (hideAbove == 0 || kv.Key.Z <= hideAbove)) // D325: the levels of the own stack
            {
                for (int dx = -1; dx <= 1 && !show; dx++)
                {
                    for (int dy = -1; dy <= 1 && !show; dy++)
                        show = _chunksInSight.Contains((kv.Key.X + dx, kv.Key.Y + dy));
                }
            }
            if (kv.Value.Root.Visible != show)
                kv.Value.Root.Visible = show;
        }
        SyncMaptextSight();
    }

    // ---- turfs that change after the join ----

    // What the tiles were made from (the MapGrid as received, the replica's appearances), then the replica's cell changes: a
    // cell whose turf type or appearance is no longer what its tiles show marks its chunk (a Delta applied before the build
    // is caught here too).
    private void FollowTurfs(MapGrid map)
    {
        if (_replica == null)
            return;
        ushort[] cells = map.ExpandCells();
        _drawnType = new int[map.CellCount];
        _drawnLook = new TableRef[map.CellCount];
        _drawnCeiling = new Anomaly.Core.Motion.CeilingState[map.CellCount];
        _drawnCeilingLook = new TableRef[map.CellCount];
        foreach (var c in map.Ceilings) (_drawnCeiling[CellIndex(c.X, c.Y, c.Z)], _drawnCeilingLook[CellIndex(c.X, c.Y, c.Z)]) = (c.State, c.Look);
        int i = 0;
        for (int z = 1; z <= _levels; z++)
        {
            for (int y = 1; y <= _height; y++)
            {
                for (int x = 1; x <= _width; x++, i++)
                {
                    _drawnType[i] = cells[i];
                    _drawnLook[i] = _pictures ? _replica.TurfAppearance(x, y, z) : TableRef.None;
                    NoteTurf(x, y, z);
                }
            }
        }
        _replica.CellsChanged -= OnCellsChanged;
        _replica.CellsChanged += OnCellsChanged;
    }

    public override void _ExitTree()
    {
        if (_replica != null)
            _replica.CellsChanged -= OnCellsChanged;
        _atlas?.Release();
    }

    private void OnCellsChanged(IReadOnlyList<(int X, int Y, int Z)> cells)
    {
        foreach ((int x, int y, int z) in cells)
        {
            NoteTurf(x, y, z);
            // D325: a turf of a level that stands on another is the ceiling of the cell below it; and a wall on the level above joins a wall built or taken down
            // under it, as the corners of the walls beside both do (BlockFaces.WallFace): their chunks, and the neighbours' on this level, are made again
            if (InMap(x, y, z))
            {
                DirtyAround(x, y, z);
                if (z > 1 && _replica.IsLinkedBelow(z))
                    DirtyAround(x, y, z - 1);
                if (z < _levels && _replica.IsLinkedBelow(z + 1))
                    DirtyAround(x, y, z + 1);
            }
            RestackWindows(x, y, z);
        }
    }

    // The chunks of (x, y, z) and of its four neighbours.
    private void DirtyAround(int x, int y, int z)
    {
        _turfDirty.Add((ChunkPlanner.ChunkIndex(x), ChunkPlanner.ChunkIndex(y), z));
        foreach ((int dx, int dy) in Around)
        {
            if (InMap(x + dx, y + dy, z))
                _turfDirty.Add((ChunkPlanner.ChunkIndex(x + dx), ChunkPlanner.ChunkIndex(y + dy), z));
        }
    }

    // A ladder hole opened or closed: the floor of its cell and the ceiling under it are drawn again.
    private void DirtyHole(int x, int y, int z)
    {
        if (!InMap(x, y, z))
            return;
        _turfDirty.Add((ChunkPlanner.ChunkIndex(x), ChunkPlanner.ChunkIndex(y), z));
        if (z > 1)
            _turfDirty.Add((ChunkPlanner.ChunkIndex(x), ChunkPlanner.ChunkIndex(y), z - 1));
    }

    // A smoothing wall at (x, y, z): one a wall joins (a diagonal one keeps the inside of a corner framed).
    private bool JoinsWall(int x, int y, int z) => InMap(x, y, z) && KindOf(_replica.TurfTypeIndexAt(x, y, z)) == TurfKind.Closed && Smooths(x, y, z);

    // A closed turf straight over the closed turf at (x, y, z) on a linked level: the two walls' sides join (the owner, 2026-11-10).
    private bool ClosedAbove(int x, int y, int z)
        => z >= 1 && z < _levels && InMap(x, y, z) && _replica.IsLinkedBelow(z + 1)
            && KindOf(_replica.TurfTypeIndexAt(x, y, z)) == TurfKind.Closed && KindOf(_replica.TurfTypeIndexAt(x, y, z + 1)) == TurfKind.Closed
            && Smooths(x, y, z) && Smooths(x, y, z + 1);

    // The turf at (x, y, z) smooths: its picture's state has tg's junction (a wall that does not, tg's indestructible ones, joins nothing up or down either).
    private bool Smooths(int x, int y, int z)
        => !_pictures || _tables == null || _tables.TryGetAppearance(_replica.TurfAppearance(x, y, z), out AppearanceEntry entry)
            && WindowJunction.TryParse(_tables.ResolveString(entry.IconState), out _);

    // A floor or wall straight over (x, y, z) on a linked level: it covers a wall's top there.
    private bool Roofed(int x, int y, int z)
        => z < _levels && _replica.IsLinkedBelow(z + 1) && TurfStyle.ShowsUnderside(KindOf(_replica.TurfTypeIndexAt(x, y, z + 1)));

    private void NoteTurf(int x, int y, int z)
    {
        if (!InMap(x, y, z))
            return;
        int i = CellIndex(x, y, z);
        TableRef look = _pictures ? _replica.TurfAppearance(x, y, z) : TableRef.None;
        if (_replica.TurfTypeIndexAt(x, y, z) != _drawnType[i] || look != _drawnLook[i] || _replica.CeilingAt(x, y, z) != _drawnCeiling[i]
            || _replica.CeilingLookAt(x, y, z) != _drawnCeilingLook[i])
            _turfDirty.Add((ChunkPlanner.ChunkIndex(x), ChunkPlanner.ChunkIndex(y), z));
    }

    // The chunks whose turfs changed, made again within the frame's budget (at least one a frame): a count of four a frame took 20 ms.
    private void RebuildTurfs()
    {
        long start = Stopwatch.GetTimestamp();
        _rebuildEngines = EngineCells(_replica);
        foreach ((int, int, int) key in _turfDirty.ToList())
        {
            _turfDirty.Remove(key);
            RebuildTurfs(key);
            if (Stopwatch.GetElapsedTime(start).TotalMilliseconds >= WarmUpBudget)
                break;
        }
    }

    private HashSet<(int, int, int)> _rebuildEngines;

    // The owner (D301): a shuttle's engine block has no floor or ceiling drawn around it; (2026-11-02) the heaters' cells keep theirs
    private static HashSet<(int, int, int)> EngineCells(ReplicaWorld replica)
    {
        var cells = new HashSet<(int, int, int)>();
        foreach (ReplicaEntity e in replica.Entities)
        {
            if (e.TypePath.StartsWith("/obj/machinery/power/shuttle_engine", StringComparison.Ordinal)
                && !e.TypePath.StartsWith("/obj/machinery/power/shuttle_engine/heater", StringComparison.Ordinal))
                cells.Add((e.State.X, e.State.Y, e.State.Z));
        }
        return cells;
    }

    private static void DropEngineTiles(ChunkPlan chunk, HashSet<(int, int, int)> engines)
    {
        if (engines.Count > 0)
            chunk.Tiles.RemoveAll(t => t.Shape != TileShape.Wall && engines.Contains((t.X, t.Y, t.Z)));
    }

    // A chunk's floors, ceilings and walls made again from the replica's turfs (the planner's rule), with its occluder;the
    // wall and floor cells of the pick ray follow.
    private void RebuildTurfs((int X, int Y, int Z) key)
    {
        ChunkPlan chunk = _plan.Chunk(key.X, key.Y, key.Z);
        chunk.Tiles.Clear();
        List<(int, int, int)> wallsChanged = null;
        int z = key.Z;
        for (int y = chunk.MinY; y <= Math.Min(chunk.MinY + ChunkPlanner.ChunkSize - 1, _height); y++)
        {
            for (int x = chunk.MinX; x <= Math.Min(chunk.MinX + ChunkPlanner.ChunkSize - 1, _width); x++)
            {
                int i = CellIndex(x, y, z);
                int type = _replica.TurfTypeIndexAt(x, y, z);
                TableRef look = _pictures ? _replica.TurfAppearance(x, y, z) : TableRef.None;
                bool wasWall = _wall[i];
                _drawnType[i] = type;
                _drawnLook[i] = look;
                _drawnCeiling[i] = _replica.CeilingAt(x, y, z);
                _drawnCeilingLook[i] = _replica.CeilingLookAt(x, y, z);
                bool hatchAbove = _plan.Index.Holes.Contains((x, y, z + 1)); // DV397: the ceiling under a ladder's hatch has its opening
                if (_drawnCeiling[i].Exists && KindOf(type) != TurfKind.Closed)
                    chunk.Tiles.Add(new TileCell(x, y, z, TileShape.Ceiling, _pictures ? _drawnCeilingLook[i] : TableRef.None, type) { Ceiling = _drawnCeiling[i], Hatch = hatchAbove });
                else if (KindOf(type) is not TurfKind.Closed && z < _levels && _replica.IsLinkedBelow(z + 1) && KindOf(_replica.TurfTypeIndexAt(x, y, z + 1)) != TurfKind.Void
                    && TurfStyle.PlatingUnder(_replica.TurfTypes[_replica.TurfTypeIndexAt(x, y, z + 1)]))
                    chunk.Tiles.Add(ChunkPlanner.UnderLevelAbove(x, y, z, type) with { Hatch = hatchAbove }); // D325: the plating the floor above lies on, or a wall above stands on
                if (_wall[i])
                    WallCount--;
                if (_open[i])
                    FloorCount--;
                _wall[i] = _open[i] = false;
                switch (KindOf(type))
                {
                    case TurfKind.Closed:
                        chunk.Tiles.Add(new TileCell(x, y, z, TileShape.Wall, look, type) { Stack = ChunkPlanner.WallStack(ClosedAbove(x, y, z), ClosedAbove(x, y, z - 1)), Roofless = !Roofed(x, y, z), NearStack = ChunkPlanner.NearStackOf(x, y, z, ClosedAbove), Diagonals = (byte)BlockFaces.DiagonalsOf(x, y, z, JoinsWall) });
                        _wall[i] = true;
                        WallCount++;
                        break;
                    case TurfKind.Open:
                        chunk.Tiles.Add(new TileCell(x, y, z, TileShape.Floor, look, type) { Hatch = _plan.Index.Holes.Contains((x, y, z)) });

                        _open[i] = true;
                        FloorCount++;
                        break;
                }
                if (wasWall != _wall[i])
                    (wallsChanged ??= new List<(int, int, int)>()).Add((x, y, z));
            }
        }
        DropEngineTiles(chunk, _rebuildEngines);
        // M5 slice 6 (S4/S5's gap): a wall built or taken down beside a window or grille changes its joints; they are placed again (Rejoin, as
        // for a window built or destroyed beside them). What hangs on the wall follows it here, where the walls the plan asks about have changed.
        if (wallsChanged != null)
        {
            foreach ((int wx, int wy, int wz) in wallsChanged)
            {
                Rejoin(wx, wy, wz);
                HangAgainAround(wx, wy, wz);
            }
        }
        var tiles = new List<(TileCell Tile, int Page, int Layer, int Side, Color Faces)>(chunk.Tiles.Count);
        int misses = _textures?.Library.PendingMisses ?? 0;
        foreach (TileCell tile in chunk.Tiles)
        {
            (int page, int layer) = _tileKit.LayerOf(tile, _replica.TurfTypes);
            tiles.Add((tile, page, layer, _tileKit.SideLayerOf(tile, page, layer), FaceLayers(tile, page, layer)));
            if (_tileKit.TryUnderlayLayer(tile, out int underPage, out int underLayer))
            {
                // the floor and the ceiling the diagonal wall's cut corner shows
                tiles.Add((tile with { Shape = TileShape.Floor }, underPage, underLayer, -1, default));
                TileCell roof = tile with { Shape = TileShape.Ceiling, Appearance = TableRef.None, Ceiling = Anomaly.Core.Motion.CeilingState.Of(Anomaly.Core.Motion.CeilingState.Plating) };
                (int roofPage, int roofLayer) = _tileKit.LayerOf(roof, _replica.TurfTypes);
                tiles.Add((roof, roofPage, roofLayer, -1, default));
            }
        }
        _tileKit.Finish();
        if (_textures != null && _textures.Library.PendingMisses != misses)
        {
            _turfsAwaitingIcons.Add(key);
            _turfLoadsSeen = _textures.Library.CompletedLoads;
        }
        if (!_batches.TryGetValue(key, out ChunkBatch batch))
            batch = NewBatch(key.X, key.Y, key.Z);
        if (!_turfNodes.TryGetValue(key, out Node3D turfs))
        {
            turfs = new Node3D { Name = "Turfs" };
            batch.Root.AddChild(turfs);
        }
        AddTiles(turfs, tiles, _tileMesh, batch.Bounds, key);
        AddOccluder(turfs, chunk);
        _turfNodes[key] = turfs;
        if (_atlas != null)
            AttachTurfs(key, chunk.Tiles, batch); // a charge planted on a wall or a floor, or gone
    }

    private TurfKind KindOf(int type)
    {
        IReadOnlyList<string> types = _replica.TurfTypes;
        if (type < 0 || type >= types.Count)
            return TurfKind.Void;
        while (_kinds.Count < types.Count)
            _kinds.Add(TurfStyle.Classify(types[_kinds.Count]));
        return _kinds[type];
    }

    private int CellIndex(int x, int y, int z) => ((z - 1) * _height + (y - 1)) * _width + (x - 1);

    // A chunk's tiles, one MultiMesh per atlas page under the chunk's turfs node. A rebuilt chunk refills the MultiMeshes it has in place
    // (a page it no longer uses is emptied): a new MultiMesh per rebuild kept its engine RID until the GC freed the old one, so a singularity
    // or an explosion rebuilding chunks all the time could run Godot out of them (as the cascade's props did, the M7 visual check).
    // A wall's four side pictures, one layer each plus one (0: the side layer), as the instance colour the tile shader reads: south, east, north, west.
    private Color FaceLayers(TileCell tile, int page, int layer)
    {
        if (tile.Shape != TileShape.Wall)
            return default;
        int[] faces = _tileKit.FaceLayersOf(tile, page, layer);
        return faces == null ? default : new Color(faces[0] + 1, faces[1] + 1, faces[2] + 1, faces[3] + 1);
    }

    private void AddTiles(Node3D root, List<(TileCell Tile, int Page, int Layer, int Side, Color Faces)> tiles, ArrayMesh mesh, Aabb bounds, (int, int, int) key)
    {
        _turfSlots.Remove(key); // a rebuilt chunk's tiles are new instances
        List<TurfSlot> slots = null;
        var used = new HashSet<string>();
        Span<float> v = stackalloc float[MultiMeshBuffer.Stride];
        // a glass floor is drawn as the glass ceilings are, blended (the owner, 2026-11-13: cut by the opaque floors' scissor, one glass floor was
        // solid and another was not there at all)
        foreach (var page in tiles.GroupBy(t => (Page: t.Page, Glass: t.Tile.Shape == TileShape.Ceiling ? t.Tile.Ceiling.IsGlass
            : t.Tile.Shape == TileShape.Floor && t.Tile.TypeIndex >= 0 && t.Tile.TypeIndex < _replica.TurfTypes.Count && TurfStyle.GlassFloor(_replica.TurfTypes[t.Tile.TypeIndex]))))
        {
            var items = page.ToList();
            string name = "Tiles" + page.Key.Page + (page.Key.Glass ? "Glass" : "");
            used.Add(name);
            MultiMeshInstance3D node = root.GetNodeOrNull<MultiMeshInstance3D>(name);
            if (node == null)
            {
                node = new MultiMeshInstance3D
                {
                    Name = name,
                    Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = mesh },
                    MaterialOverride = System.Environment.GetEnvironmentVariable("ANOMALY_TILE_DEBUG") == "1" ? new StandardMaterial3D { AlbedoColor = Colors.Red } : _tileKit.MaterialOf(page.Key.Page, page.Key.Glass),
                    CustomAabb = bounds,
                    // D288: both sides, so the ceiling (a face turned down) shades the rooms from the sun above it
                    CastShadow = page.Key.Glass ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.DoubleSided,
                };
                root.AddChild(node);
            }
            node.Visible = true;
            MultiMesh multi = node.Multimesh;
            multi.InstanceCount = items.Count;
            var buffer = new float[items.Count * MultiMeshBuffer.Stride];
            for (int i = 0; i < items.Count; i++)
            {
                TileCell t = items[i].Tile;
                Vector3 at = TileCenter(t.X, t.Y, t.Z);
                // x: the layer of the tile's own picture, y: the part, z: the layer of a wall's side picture plus one (0: the sides show the own picture).
                // M4v slice 2: an animated picture's tile shows the layer of the frame its picture is at now (StepTurfFrames moves it on).
                int group = t.Shape == TileShape.Ceiling ? -1 : _tileKit.GroupOf(page.Key.Page, items[i].Layer);
                int layer = group >= 0 ? _tileKit.Frames.LayerNow(group) : items[i].Layer;
                // a diagonal corner wall is the triangle part, turned a quarter per step (clockwise from above: Godot's yaw by -90 degrees each)
                int turns = _tileKit.DiagonalTurnsOf(t);
                float c = turns is 1 or 3 ? 0f : turns == 2 ? -1f : 1f, s = turns == 1 ? -1f : turns == 3 ? 1f : 0f;
                (v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8]) = (c, 0f, s, 0f, 1f, 0f, -s, 0f, c);
                float part = turns >= 0 ? TileKitParts.DiagonalWall : t.Hatch ? (t.Shape == TileShape.Ceiling ? TileKitParts.HatchCeiling : TileKitParts.HatchFloor) : (float)t.Shape;
                (v[9], v[10], v[11]) = (at.X, at.Y, at.Z);
                // w: a ceiling's support (1, or -1 unsupported); a wet floor 2 (TileKit.IsWet); a wall with nothing over it 3 (its top at the full height, turf_kit.gdshader);
                // a polished floor 4 (TurfStyle.Polished: it reflects the room); a floor or ceiling block that gives light glows, 5 (-5 unsupported; protocol 96)
                bool glows = t.Shape is TileShape.Floor or TileShape.Ceiling && !_replica.TurfLightAt(t.X, t.Y, t.Z, t.Shape == TileShape.Ceiling).IsNone;
                float condition = t.Shape == TileShape.Ceiling ? (t.Ceiling.Supported ? 1f : -1f) * (glows ? 5f : 1f) : glows ? 5f : _tileKit.IsWet(t) ? 2f : t.Roofless ? 3f
                    : t.Shape == TileShape.Floor && t.TypeIndex >= 0 && t.TypeIndex < _replica.TurfTypes.Count && TurfStyle.Polished(_replica.TurfTypes[t.TypeIndex]) ? 4f : 0f;
                // colour: a wall's side picture of each face (FaceLayers); custom: as above
                Color faces = items[i].Faces;
                (v[12], v[13], v[14], v[15]) = (faces.R, faces.G, faces.B, faces.A);
                (v[16], v[17], v[18], v[19]) = (layer, part, items[i].Side + 1, condition);
                MultiMeshBuffer.Write(buffer, i, v);
                if (group >= 0)
                    (slots ??= new List<TurfSlot>()).Add(new TurfSlot { Multi = multi, Index = i, Group = group, Part = part, Side = items[i].Side + 1, Condition = condition });
            }
            multi.Buffer = buffer;
        }
        foreach (MultiMeshInstance3D stale in root.GetChildren().OfType<MultiMeshInstance3D>())
        {
            if (used.Contains(stale.Name))
                continue;
            stale.Multimesh.InstanceCount = 0;
            stale.Visible = false;
        }
        if (slots != null)
            _turfSlots[key] = slots;
    }

    // M4v slice 2 (D143): the tiles of an animated turf picture (TileKit.GroupOf), per chunk, whose layer StepTurfFrames moves on.
    private sealed class TurfSlot
    {
        public MultiMesh Multi;
        public int Index;
        public int Group;
        public float Part;
        public float Side;
        public float Condition;
    }

    // Once per frame: the animated turf pictures whose frame changed write their new layer into their tiles' instances (no chunk is rebuilt).
    private void StepTurfFrames()
    {
        if (_tileKit == null || _tileKit.Frames.Count == 0 || _turfSlots.Count == 0)
            return;
        _changedTurfGroups.Clear();
        _tileKit.Frames.Step(_clock, _changedTurfGroups);
        if (_changedTurfGroups.Count == 0)
            return;
        foreach (List<TurfSlot> slots in _turfSlots.Values)
            foreach (TurfSlot slot in slots)
                if (_changedTurfGroups.Contains(slot.Group))
                    slot.Multi.SetInstanceCustomData(slot.Index, new Color(_tileKit.Frames.LayerNow(slot.Group), slot.Part, slot.Side, slot.Condition));
    }

    // The chunk's occluder, made again when its walls changed (one node; the old occluder's RID is freed at once rather than at the GC, as the
    // tiles' MultiMeshes are kept). A new occluder makes Godot build its occlusion culling's scene again: a floor's new look keeps the old one.
    private readonly Dictionary<(int X, int Y, int Z), string> _occluderWalls = new();

    private void AddOccluder(Node3D root, ChunkPlan chunk)
    {
        var node = root.GetNodeOrNull<OccluderInstance3D>("Occluder");
        var walls = chunk.Tiles.Where(t => t.Shape == TileShape.Wall).Select(t => (t.X, t.Y)).ToList();
        string key = string.Join(";", walls);
        var at = (chunk.ChunkX, chunk.ChunkY, chunk.Z);
        if (_occluderWalls.TryGetValue(at, out string had) && had == key && (node != null || walls.Count == 0))
            return;
        _occluderWalls[at] = key;
        Occluder3D old = node?.Occluder;
        if (walls.Count > 0)
        {
            if (node == null)
                root.AddChild(node = new OccluderInstance3D { Name = "Occluder" });
            node.Occluder = WorldMeshes.Occluder(OccluderPlanner.Merge(walls, chunk.Z));
        }
        else if (node != null)
            node.Occluder = null;
        old?.Dispose();
    }

    private static Vector3 TileCenter(int x, int y, int z) => new(TileSpace.WorldX(x), TileSpace.WorldY(z), TileSpace.WorldZ(y));

    /// <summary>
    /// M4v slice 3 (protocol version 10): where a short-lived visual on the static prop <paramref name="entityId"/> is drawn
    /// (tg's flick_overlay_view on it; VisualAnchor): on a wall decal's face, or as a billboard at the prop's resting point (an
    /// item's at the item scale). False when the renderer does not draw that entity (it is a node of the entity layer, or unknown).
    /// </summary>
    public bool TryVisualAnchor(long entityId, Vector3 viewer, out VisualPlacement placement)
    {
        placement = default;
        if (!_records.TryGetValue(entityId, out StaticRecord record))
            return false;
        Placed p = record.Placed;
        var eye = new System.Numerics.Vector3(viewer.X, viewer.Y, viewer.Z);
        if (p.Kind == PlacedKind.WallDecal && !p.Fin && record.PictureId >= 0)
        {
            placement = VisualAnchor.OnWallDecal(p, _atlas.QuadHeightOf(record.PictureId));
            return true;
        }
        Vector3 feet = TileCenter(p.X, p.Y, p.Z) + new Vector3(p.OffsetX, p.Kind == PlacedKind.Piece ? 0f : p.Height, -p.OffsetY);
        float scale = p.Kind == PlacedKind.Item ? WorldScale.ItemScale : 1f;
        placement = VisualAnchor.OnBillboard(new System.Numerics.Vector3(feet.X, feet.Y, feet.Z), scale, eye);
        return true;
    }

    /// <summary>
    /// A short-lived visual on a thing a model draws (tg's flick_overlay_view: a button's button-overlay-success): when its model has a variant with that overlay
    /// (an entry whose overlays name it), the thing is drawn by that variant until <see cref="ReleaseVisual"/> and the visual is not drawn on its own (true).
    /// A wall model has no picture on the wall for the visual to lie on, so without this it would hang below it as a billboard.
    /// </summary>
    public bool TryAbsorbVisual(long hostId, long visualId, TableRef visualAppearance)
    {
        if (!_records.TryGetValue(hostId, out StaticRecord record) || record.Model == null)
            return false;
        if (record.VisualOverlays.ContainsKey(visualId))
            return true;
        if (!_replica.TryGetAppearance(visualAppearance, out AppearanceEntry look) || !_replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry entry))
            return false;
        string state = _replica.ResolveString(look.IconState);
        ModelEntry variant = _models.Catalog.FindVisualVariant(_replica.ResolveResource(entry.Icon), record.SeenState, ModelOverlayStates(record, entry), state);
        if (variant == null)
            return false;
        record.VisualOverlays[visualId] = state;
        if (variant != record.Model)
            Rebuild(record); // a second visual of the same state (pressed again while it shows) is drawn by the model already there
        return true;
    }

    /// <summary>
    /// M8x slice C (D210): the static prop <paramref name="entityId"/> holds a visual helper drawn into its picture as it animates (tg's cryo
    /// occupant, <see cref="VisInsets"/>), which the entity layer composes: a picture prop (no model) is handed to it, as a moved one is. False
    /// when the renderer does not draw that entity as such a prop.
    /// </summary>
    public bool HandOver(long entityId)
    {
        // a thing still waiting for its textures may be about to get a model: handed over now it would stay a picture (the owner, 2026-11-08: a cryo cell at the join)
        if (!_records.TryGetValue(entityId, out StaticRecord record) || record.Model != null || _awaitingIcons.Contains(record)
            || record.Placed.Kind is not (PlacedKind.Billboard or PlacedKind.Item or PlacedKind.Resting))
            return false;
        RemoveRecord(record);
        HandedOver++;
        PropMoved?.Invoke(record.Entity);
        return true;
    }

    /// <summary>The visual <paramref name="visualId"/> ended: a thing whose model drew it (<see cref="TryAbsorbVisual"/>) is drawn as before.</summary>
    public void ReleaseVisual(long hostId, long visualId)
    {
        if (_records.TryGetValue(hostId, out StaticRecord record) && record.VisualOverlays.Remove(visualId))
            Rebuild(record);
    }

    private void Rebuild(StaticRecord record)
    {
        BuildInstances(record);
        _dirty.Add(record.Batch);
    }

    // The overlay states a model is picked by: the appearance's, and those of the visuals on it its model draws (TryAbsorbVisual).
    private IEnumerable<string> ModelOverlayStates(StaticRecord record, AppearanceEntry entry)
        => record.VisualOverlays.Count == 0 ? OverlayStates(entry) : OverlayStates(entry).Concat(record.VisualOverlays.Values).ToList();

    public void SetCeilingVisible(bool visible) => _tileKit?.SetParameter("ceilings", visible);

    // ---- the static-versus-moving rule ----

    // A visual (NetField.VisHost) is drawn on its host by the entity layer, never batched: one present at the join (the cryo cell's occupant, M8x) was planned as a prop of its own.
    // DV416: a thing in free motion or resting off its tile's middle is drawn where its motion puts it, not planned on its tile
    private static bool IsStaticCandidate(ReplicaEntity e) => !e.IsVisual && e.State.Flight.IsNone && StaticPropRule.IsCandidate(e.TypePath, e.HasPos);

    private PlanEntity ToPlan(ReplicaEntity e)
    {
        EntityState s = e.State;
        short px = 0, py = 0;
        string state = null;
        float plane = 0f, layer = 0f;
        if (_replica.TryGetAppearance(s.Appearance, out AppearanceEntry entry))
        {
            px = entry.PixelX;
            py = entry.PixelY;
            state = _replica.ResolveString(entry.IconState);
            plane = entry.Plane;
            layer = entry.Layer;
        }
        return new PlanEntity(e.Id, e.TypePath, s.X, s.Y, s.Z, s.Dir, s.Appearance, px, py, s.Light, state, plane, layer);
    }

    public bool OnSpawned(ReplicaEntity entity)
    {
        if (_atlas == null || !IsStaticCandidate(entity))
            return false;
        int misses = _textures.Library.PendingMisses;
        if (_records.TryGetValue(entity.Id, out StaticRecord known))
            return Claim(known, misses);
        PlanEntity p = ToPlan(entity);
        if (p.X < 1 || p.Y < 1 || p.X > _width || p.Y > _height || p.Z < 1 || p.Z > _levels)
            return true;
        // A thing that spawns after the build joins what the plan knows (a window's arms, a door's face, a border corner); what is already drawn is not redrawn for it.
        _plan.Index.Add(p);
        if (p.TypePath == PlanIndex.LadderHoleType)
        {
            DirtyHole(p.X, p.Y, p.Z);
            return true;
        }
        if (!ChunkPlanner.TryPlace(p, Probe, out Placed placed))
            return true; // nothing to draw for it
        if (placed.Kind == PlacedKind.Piece && placed.Face != 0 && placed.Shape is RenderKind.Airlock or RenderKind.PodDoor or RenderKind.Firelock)
            _plan.Index.DoorFaces[(p.X, p.Y, p.Z)] = placed.Face;
        if (!_batches.TryGetValue((ChunkPlanner.ChunkIndex(p.X), ChunkPlanner.ChunkIndex(p.Y), p.Z), out ChunkBatch batch))
            batch = NewBatch(ChunkPlanner.ChunkIndex(p.X), ChunkPlanner.ChunkIndex(p.Y), p.Z);
        _iconsCanWait = true;
        AddRecord(entity, placed, batch);
        _iconsCanWait = false;
        _dirty.Add(batch);
        if (JoinsNeighbours(entity.TypePath))
            Rejoin(p.X, p.Y, p.Z); // M5 slice 5: a window or grille built beside others joins them, and they it
        if (placed.Kind == PlacedKind.Piece && placed.Shape == RenderKind.FullWindow)
            HangAgainAround(p.X, p.Y, p.Z);
        if (placed.Kind == PlacedKind.Piece && placed.Shape == RenderKind.DirectionalWindow)
            RedrawBorderWindowsAround(p.X, p.Y, p.Z, except: entity.Id);
        if (placed.Kind == PlacedKind.Piece && placed.Shape == RenderKind.Table)
            ReplaceOnTable((p.X, p.Y, p.Z), entity.Id);
        return Claim(_records[entity.Id], misses);
    }

    // An entity whose picture could not be composed (its icon is missing) is not the batch's: the layer draws it as a node with its placeholder,
    // as before M3. An entity with no appearance at all has nothing to draw and is claimed.
    // An entity whose icon is still loading, or whose model's textures still decode (BuildInstances put it in _awaitingIcons: handed to the
    // layer, it stayed a flat picture for good), stays the batch's, drawn when they are in (RetryAwaitingIcons).
    private bool Claim(StaticRecord record, int pendingMisses)
    {
        // a hidden part (ModelEntry.Hidden: a gravity generator's outer parts, spawned after the map) has no instance of its own: another thing's model draws it
        if (record.Instances.Count > 0 || record.Placed.Appearance.IsNone || record.Model is { Hidden: true })
            return true;
        if (_textures.Library.PendingMisses != pendingMisses)
        {
            _awaitingIcons.Add(record);
            return true;
        }
        if (_awaitingIcons.Contains(record))
        {
            WaitedForTextures++;
            return true;
        }
        RemoveRecord(record);
        return false;
    }

    // When background icon loads or model texture decodes landed: the waiting things are built again, within RetryBudgetMs a frame (the rest
    // the next frame); one whose icon turned out missing goes to the layer's placeholder; a model still decoding keeps waiting.
    private const double RetryBudgetMs = 4;
    private int _decodesSeen;
    private bool _retryUnfinished;
    private bool _iconsCanWait; // the build in hand comes back to a thing with nothing drawn while its icon loads (Claim, RetryAwaitingIcons)

    private void RetryAwaitingIcons()
    {
        int loads = _textures.Library.CompletedLoads, decodes = _models?.DecodesDone ?? 0;
        if (_awaitingIcons.Count == 0 || (!_retryUnfinished && loads == _iconLoadsSeen && decodes == _decodesSeen))
            return;
        (_iconLoadsSeen, _decodesSeen, _retryUnfinished) = (loads, decodes, false);
        long start = Stopwatch.GetTimestamp();
        foreach (StaticRecord record in _awaitingIcons.ToList())
        {
            if (Stopwatch.GetElapsedTime(start).TotalMilliseconds >= RetryBudgetMs)
            {
                _retryUnfinished = true;
                break;
            }
            int misses = _textures.Library.PendingMisses, pending = _models?.PendingTextures ?? 0;
            _iconsCanWait = true;
            BuildInstances(record);
            _iconsCanWait = false;
            if (_models != null && _models.PendingTextures != pending)
                continue;
            if (record.Instances.Count > 0)
            {
                if (_textures.Library.PendingMisses == misses)
                    _awaitingIcons.Remove(record);
                _dirty.Add(record.Batch);
            }
            else if (_textures.Library.PendingMisses == misses)
            {
                RemoveRecord(record);
                HandedOver++;
                PropMoved?.Invoke(record.Entity);
            }
        }
    }

    // The neighbours of what left are redrawn once a frame, when all of it is gone: a strip of the map leaving redrew each window's neighbours,
    // most of them about to leave too (35 of the 40 ms of a tick's 300 despawns).
    private readonly HashSet<(int X, int Y, int Z)> _leftCells = new();
    private readonly List<Placed> _leftBorders = new();

    private void RedrawAroundWhatLeft()
    {
        foreach ((int X, int Y, int Z) cell in _leftCells)
            Unjoin(cell);
        _leftCells.Clear();
        foreach (Placed placed in _leftBorders)
            ForgetBorderWindow(placed);
        _leftBorders.Clear();
    }

    public bool OnDespawned(ReplicaEntity entity)
    {
        if (entity.TypePath == PlanIndex.LadderHoleType && _plan != null && _plan.Index.Holes.Remove((entity.State.X, entity.State.Y, entity.State.Z)))
        {
            DirtyHole(entity.State.X, entity.State.Y, entity.State.Z);
            return true;
        }
        if (!_records.TryGetValue(entity.Id, out StaticRecord record))
            return false;
        RemoveRecord(record);
        if (JoinsNeighbours(entity.TypePath))
            _leftCells.Add(record.Cell); // M5 slice 5: a destroyed window or grille leaves its neighbours' arms toward it
        if (record.Placed.Kind == PlacedKind.Piece && record.Placed.Shape == RenderKind.DirectionalWindow)
            _leftBorders.Add(record.Placed);
        if (record.Placed.Kind == PlacedKind.Piece && record.Placed.Shape == RenderKind.Table)
        {
            if (!_byCell.TryGetValue(record.Cell, out List<StaticRecord> left) || !left.Any(o => o.Placed.Kind == PlacedKind.Piece && o.Placed.Shape == RenderKind.Table))
                _plan.Index.Tables.Remove(record.Cell);
            ReplaceOnTable(record.Cell, entity.Id);
        }
        return true;
    }

    // A table that comes after the things on its tile (a chunk's things arrive in any order: the owner's report, 2026-10-20, a microwave under its table),
    // or goes from under them: they stand on its top or on the floor again.
    private void ReplaceOnTable((int X, int Y, int Z) cell, long table)
    {
        if (!_byCell.TryGetValue(cell, out List<StaticRecord> here))
            return;
        foreach (StaticRecord other in here.ToArray())
        {
            if (other.Entity.Id != table && other.Placed.Kind != PlacedKind.Piece)
                Replace(other, rebuildAlways: true);
        }
    }

    // M5 slice 5 (D142): the things whose arms join their neighbours (D121), and the doors and walls they join to.
    private static bool JoinsNeighbours(string typePath)
        => PropKinds.Classify(typePath) is RenderKind.FullWindow or RenderKind.Grille or RenderKind.Girder or RenderKind.FalseWall or RenderKind.Airlock or RenderKind.PodDoor;

    // The cell lost a joined thing: the index forgets what no longer stands there, and the neighbours are placed again.
    private void Unjoin((int X, int Y, int Z) cell)
    {
        bool windowLeft = false, grilleLeft = false, frameDoorLeft = false, doorLeft = false, girderLeft = false;
        if (_byCell.TryGetValue(cell, out List<StaticRecord> here))
        {
            foreach (StaticRecord other in here)
            {
                windowLeft |= other.Placed.Shape == RenderKind.FullWindow;
                grilleLeft |= other.Placed.Kind == PlacedKind.Piece && other.Placed.Shape == RenderKind.Grille;
                girderLeft |= other.Placed.Kind == PlacedKind.Piece && other.Placed.Shape is RenderKind.Girder or RenderKind.FalseWall;
                frameDoorLeft |= other.Placed.Kind == PlacedKind.Piece && other.Placed.Shape is RenderKind.Airlock or RenderKind.PodDoor;
                doorLeft |= other.Placed.Kind == PlacedKind.Piece && other.Placed.Shape is RenderKind.Airlock or RenderKind.PodDoor or RenderKind.Firelock;
            }
        }
        _plan.Index.Forget(cell, windowLeft, grilleLeft, frameDoorLeft, doorLeft, girderLeft);
        Rejoin(cell.X, cell.Y, cell.Z);
        if (!windowLeft)
            HangAgainAround(cell.X, cell.Y, cell.Z); // (what hung on a window that is gone)
    }

    // Places the full-tile windows and grilles beside a cell again (their arms toward it), redrawing those whose joints changed.
    private void Rejoin(int x, int y, int z)
    {
        // D152: a grille under a window lies on the floor, one with none is a block of bars: a window built on it or taken off redraws it
        if (WorldScale.BlockPieces && _byCell.TryGetValue((x, y, z), out List<StaticRecord> here))
        {
            foreach (StaticRecord other in here.ToArray())
            {
                if (other.Placed.Kind == PlacedKind.Piece && other.Placed.Shape == RenderKind.Grille)
                    BuildInstances(other);
            }
        }
        foreach ((int dx, int dy) in Neighbours)
        {
            if (!_byCell.TryGetValue((x + dx, y + dy, z), out List<StaticRecord> there))
                continue;
            foreach (StaticRecord other in there.ToArray())
            {
                if (other.Placed.Kind == PlacedKind.Piece && other.Placed.Shape is RenderKind.FullWindow or RenderKind.Grille or RenderKind.Girder or RenderKind.FalseWall)
                    Replace(other, rebuildAlways: WorldScale.BlockPieces); // a block's hidden faces follow more than its arms
                else if (other.Placed.Kind == PlacedKind.Piece && IsDoorShape(other.Placed.Shape))
                    Reface(other);
            }
        }
        // the diagonal windows: the inside of a corner they make with it (BlockFaces.InnerCorners)
        foreach ((int dx, int dy) in Around)
        {
            if (dx == 0 || dy == 0 || !_byCell.TryGetValue((x + dx, y + dy, z), out List<StaticRecord> there))
                continue;
            foreach (StaticRecord other in there.ToArray())
            {
                if (other.Placed.Kind == PlacedKind.Piece && other.Placed.Shape == RenderKind.FullWindow)
                    Replace(other, rebuildAlways: WorldScale.BlockPieces);
            }
        }
        // the windows straight over and under it join it, or no longer do
        RestackWindows(x, y, z, -1, -1);
        RestackWindows(x, y, z, 1, 1);
    }

    private static readonly (int Dx, int Dy)[] Neighbours = { (0, 1), (0, -1), (1, 0), (-1, 0) };
    private static readonly (int Dx, int Dy)[] Around = { (0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, -1), (-1, 1) };

    private static bool IsDoorShape(RenderKind shape) => shape is RenderKind.Airlock or RenderKind.PodDoor or RenderKind.Firelock or RenderKind.DoorAssembly or RenderKind.Curtain;

    // The owner's play test (2026-10-16): a door decides its axis from what is around it (DoorAxis); a wall built or taken down beside it, or
    // its own dir turned, decides it again (the plan's stored face of its cell is forgotten first, so the rule runs).
    private void Reface(StaticRecord record)
    {
        (int X, int Y, int Z) cell = (record.Placed.X, record.Placed.Y, record.Placed.Z);
        bool had = _plan.Index.DoorFaces.Remove(cell, out byte before);
        if (!ChunkPlanner.TryPlace(ToPlan(record.Entity), Probe, out Placed again) || again.Kind != PlacedKind.Piece)
        {
            if (had)
                _plan.Index.DoorFaces[cell] = before;
            return;
        }
        if (again.Face != 0 && again.Shape is RenderKind.Airlock or RenderKind.PodDoor or RenderKind.Firelock)
            _plan.Index.DoorFaces[cell] = again.Face;
        if (again.Face == record.Placed.Face)
            return;
        record.Placed = again;
        BuildInstances(record);
    }

    // Plans a window or grille again from what its entity says now (its joints, or a grille broken into tg's sprite) and redraws it, within
    // the frame's budget (StepReplace): a door or a window that arrived rebuilt each neighbour's model at once, inside the message or the
    // tick's scan that brought it, and a station's windows streaming in made frames of 20-50 ms.
    private void Replace(StaticRecord record, bool rebuildAlways)
        => _replaceSoon[record] = rebuildAlways || (_replaceSoon.TryGetValue(record, out bool always) && always);

    private readonly Dictionary<StaticRecord, bool> _replaceSoon = new();

    private void StepReplace()
    {
        if (_replaceSoon.Count == 0)
            return;
        using var probe = Dev.FrameProbe.Scope("map/replace");
        long start = Stopwatch.GetTimestamp();
        foreach ((StaticRecord record, bool always) in _replaceSoon.ToList())
        {
            _replaceSoon.Remove(record);
            if (_records.TryGetValue(record.Entity.Id, out StaticRecord live) && ReferenceEquals(live, record))
                ReplaceNow(record, always);
            if (Stopwatch.GetElapsedTime(start).TotalMilliseconds >= WarmUpBudget)
                break;
        }
    }

    private void ReplaceNow(StaticRecord record, bool rebuildAlways)
    {
        if (!ChunkPlanner.TryPlace(ToPlan(record.Entity), Probe, out Placed again))
            return;
        bool changed = again.Joints != record.Placed.Joints || again.Kind != record.Placed.Kind || again.Shape != record.Placed.Shape;
        record.Placed = again;
        if (changed || rebuildAlways)
            BuildInstances(record);
    }

    private ChunkBatch NewBatch(int cx, int cy, int z)
    {
        var plan = _plan.Chunk(cx, cy, z);
        (float bx, float by, float bz, float sx, float sy, float sz) = plan.Bounds(_width, _height);
        var root = new Node3D { Name = $"Chunk_{cx}_{cy}_{z}" };
        _sightVersion = -1; // SetSight decides whether the new chunk shows
        AddChild(root);
        var batch = new ChunkBatch(plan, root, _quadMesh, _pieceMesh, _atlas?.Material, new Aabb(new Vector3(bx, by - 0.5f, bz), new Vector3(sx, sy + 1f, sz)), ModelMeshAt, ModelMaterialAt);
        _batches[(cx, cy, z)] = batch;
        return batch;
    }

    private PlanProbe _probe;

    // What the planner asks about the cells around a thing that spawns after the build: the grid's walls and floors, what the plan knows of windows, grilles, doors and tables,
    // and the replica's solidity (a dense thing that is not a mob stands in a door's way).
    private PlanProbe Probe => _probe ??= new PlanProbe
    {
        IsWall = (x, y, z) => IsClosedAt(x, y, z) || _plan.Index.Windows.Contains((x, y, z)),
        IsTable = (x, y, z) => _plan.Index.Tables.Contains((x, y, z)),
        IsOpen = IsOpenAt,
        Classify = ClassifyAt,
        Connects = (x, y, z) => IsClosedAt(x, y, z) || _plan.Index.Windows.Contains((x, y, z)) || _plan.Index.Grilles.Contains((x, y, z)) || _plan.Index.FrameDoors.Contains((x, y, z)),
        DoorFace = (x, y, z) => _plan.Index.DoorFaces.TryGetValue((x, y, z), out byte face) ? face : (byte)0,
        Corners = _plan.Index.Corners,
        IsFullWindow = (x, y, z) => _plan.Index.Windows.Contains((x, y, z)),
    };

    private bool InMap(int x, int y, int z) => x >= 1 && y >= 1 && z >= 1 && x <= _width && y <= _height && z <= _levels;

    private bool IsClosedAt(int x, int y, int z) => InMap(x, y, z) && _wall[((z - 1) * _height + (y - 1)) * _width + (x - 1)];

    // An open floor cell (a sign in a wall cell faces one).
    private bool IsOpenAt(int x, int y, int z) => InMap(x, y, z) && _open[((z - 1) * _height + (y - 1)) * _width + (x - 1)];

    // What a cell is to a door that decides which way to stand (DoorAxis).
    private CellClass ClassifyAt(int x, int y, int z)
    {
        if (!InMap(x, y, z))
            return CellClass.Void;
        var cell = (x, y, z);
        PlanIndex index = _plan.Index;
        if (IsClosedAt(x, y, z) || index.Windows.Contains(cell) || index.Grilles.Contains(cell) || index.Doors.Contains(cell) || _replica.IsSolid(x, y, z))
            return CellClass.Blocking;
        return IsOpenAt(x, y, z) ? CellClass.Open : CellClass.Void;
    }

    // Whether the door blocks: its own resting picture says so (open shutters behind a window are open although the cell is solid), else the cell's solidity.
    private bool DoorSolid(StaticRecord record)
        => record.Placed.Shape == RenderKind.Windoor
            ? DoorAnimation.WindoorShut(record.SeenState) // a windoor never makes its cell solid: its own state says whether it is shut
            : DoorAnimation.OwnSolid(record.SeenState) ?? _replica.IsSolid(record.Cell.X, record.Cell.Y, record.Cell.Z);

    private void AddRecord(ReplicaEntity entity, Placed placed, ChunkBatch batch)
    {
        var record = new StaticRecord
        {
            Entity = entity,
            Placed = placed,
            Batch = batch,
            Cell = (placed.X, placed.Y, placed.Z),
            SeenAppearance = entity.State.Appearance,
            SeenDir = entity.State.Dir,
            IsDoor = PieceParts.IsDoor(placed.Shape),
        };
        record.SeenState = StateOf(record.SeenAppearance);
        record.AnimationStart = _clock;
        if (record.IsDoor)
        {
            record.SeenSolid = DoorSolid(record);
            record.Door = DoorAnimation.Initial(record.SeenSolid);
            _doors.Add(record);
        }
        _records[entity.Id] = record;
        if (!_byCell.TryGetValue(record.Cell, out List<StaticRecord> here))
            _byCell[record.Cell] = here = new List<StaticRecord>();
        here.Add(record);
        BuildInstances(record);
        RebuildDeckLinks(record, record.Model);
        record.SeenFlickTick = entity.State.FlickTick;
        StartFlick(record); // a thing that flicked as it appeared (a spawned vent without a pipe shuts down)
        record.SeenAnimation = entity.State.Animation;
        if (record.SeenAnimation != null)
            _looks.Add(record); // a thing that animates as it appears (an item dropped from a hand glides down)
    }

    // DEVIATION(DV402): a multi-deck hub's parts depend on the hubs of its model over and under it (ModelPart.Deck): the one that appears or goes redraws them.
    private bool DeckLinked(StaticRecord record, ModelEntry model, int dz)
        => _byCell.TryGetValue((record.Cell.Item1, record.Cell.Item2, record.Cell.Item3 + dz), out List<StaticRecord> there) && there.Exists(r => r.Model == model);

    // shortcut: any hub of the model counts, not only one on the same piping layer (tg's is_connectable); upgrade when a map stacks adapters of two layers on one tile.
    private void RebuildDeckLinks(StaticRecord record, ModelEntry model)
    {
        if (model == null || !model.Parts.Values.Any(p => p.Deck != null))
            return;
        for (int dz = -1; dz <= 1; dz += 2)
        {
            if (_byCell.TryGetValue((record.Cell.Item1, record.Cell.Item2, record.Cell.Item3 + dz), out List<StaticRecord> there))
                foreach (StaticRecord other in there.Where(r => r != record && r.Model == model).ToArray())
                    Rebuild(other);
        }
    }

    private void RemoveRecord(StaticRecord record)
    {
        _awaitingIcons.Remove(record);
        _replaceSoon.Remove(record);
        ModelEntry deckModel = record.Model;
        ClearInstances(record);
        _records.Remove(record.Entity.Id);
        if (_byCell.TryGetValue(record.Cell, out List<StaticRecord> here))
            here.Remove(record);
        RebuildDeckLinks(record, deckModel);
        if (record.IsDoor)
            _doors.Remove(record);
        _closets.Remove(record);
        _partFlicks.Remove(record);
        _sunTurning.Remove(record);
        _carried.Remove(record);
        _rideCarried.Remove(record);
        record.CarryOffset = Vector3.Zero;
        _animated.Remove(record);
        _looks.Remove(record);
        _dirty.Add(record.Batch);
    }

    private void ClearInstances(StaticRecord record)
    {
        ClearMaptext(record);
        DetachFromRecord(record);
        ClearModelLights(record);
        if (record.Instances.Count == 0)
            return;
        record.Batch.RemoveAll(new HashSet<PropInstance>(record.Instances));
        record.Instances.Clear();
        record.LiveFrames.Clear();
        record.OverlayFrames.Clear();
        _overlayAnimated.Remove(record);
        record.Hinged.Clear();
        record.Spinning.Clear();
        record.Blinking.Clear();
        record.SunHeads.Clear();
        record.Batch.Dirty = true;
    }

    private string StateOf(TableRef appearance)
        => _replica.TryGetAppearance(appearance, out AppearanceEntry entry) ? _replica.ResolveString(entry.IconState) : null;

    // Makes (or remakes) the record's instances from its placement and picture, and the models of its attached overlays (a charge on a door).
    private void BuildInstances(StaticRecord record)
    {
        int misses = _textures.Library.PendingMisses, pending = _models?.PendingTextures ?? 0;
        BuildOwnInstances(record);
        // a model rebuilt while it is carried (a transit pod turned by its tube on the way) glides from its new transforms (StepCarried)
        if (_carried.Contains(record))
        {
            record.CarryBase = record.Instances.Select(i => i.Transform).ToList();
            foreach (PropInstance instance in record.Instances)
                if (!record.Hinged.Any(h => ReferenceEquals(h.Instance, instance)) && !record.Spinning.Any(h => ReferenceEquals(h.Instance, instance)))
                    record.Batch.SetModelTransform(instance, instance.Transform with { Origin = instance.Transform.Origin + record.CarryOffset });
        }
        AttachToRecord(record);
        AddMaptext(record); // gaps W1: its maptext (MapRenderer.Maptext.cs)
        // a build that drew something with an icon still loading (a door's panels without their overlays), or a model whose textures still
        // decode (nothing drawn yet), is built again when they are in
        if ((record.Instances.Count > 0 && _textures.Library.PendingMisses != misses) || (_models != null && _models.PendingTextures != pending))
            _awaitingIcons.Add(record);
    }

    private void BuildOwnInstances(StaticRecord record)
    {
        ClearInstances(record);
        record.Model = null;
        record.Animation = null;
        record.LiveState = null;
        record.LiveSquare = false;
        Placed p = record.Placed;
        Vector3 center = TileCenter(p.X, p.Y, p.Z);
        if (p.Kind == PlacedKind.Piece)
        {
            BuildPiece(record, center);
            return;
        }
        // A standing thing a 3D model replaces (a chair): the model, on the spot the billboard would stand.
        // A hidden part with an overlay (the gravity generator's centre: tg's startup .. activated field) goes on as a billboard of that overlay alone (Look).
        if (p.Kind is PlacedKind.Billboard or PlacedKind.Resting or PlacedKind.FloorDecal && TryBuildModel(record, center + new Vector3(p.OffsetX, p.Height, -p.OffsetY), -1)
            && !HiddenOverlay(record, out _))
        {
            if (p.Kind == PlacedKind.FloorDecal)
                AddPipeUnderlays(record, center);
            return;
        }
        if (p.Kind == PlacedKind.Item && PropKinds.ItemHasModel(record.Entity.TypePath) && TryBuildModel(record, ItemModelOrigin(record, center), -1))
            return;
        // Wall furniture a wall model replaces (an alarm, a camera): the model on the face its decal would be on. One in the wall cell itself stands on the open
        // tile before that face, so that the face is behind it as for a mount beside the wall.
        if (p.Kind == PlacedKind.WallDecal)
        {
            (int fx, int fy) = WallMounts.Step(p.Face);
            if (TryBuildModel(record, p.InWall ? TileCenter(p.X + fx, p.Y + fy, p.Z) : center, -1))
                return;
        }
        int id, frames;
        if (p.Kind == PlacedKind.FloorDecal || p.Fin || FrontOnly(record))
        {
            // A floor hatch: its picture for its own dir (it lies on the floor turned by it, not by where the viewer stands). A camera's fin: tg's side view
            // of it (WallMounts.FinPictureDir), whose frames play as tg's do (the pan of its lens).
            if (!_atlas.TryPictureFacing(_tables, Look(record), (byte)FacingOf(record), trim: true, out id) || _atlas.Size.X == 0)
                return;
            frames = 1;
        }
        else if (!_atlas.TryPicture(_tables, Look(record), wantFrames: true, trim: true, out id, out frames) || _atlas.Size.X == 0)
            return;
        record.PictureId = id;
        record.Frames = frames;
        // D142: an icon state that animates (a vent's fan, a computer's screen) steps through its pictures (StepAnimations)
        if (!_atlas.TryAnimation(_tables, Look(record), FacingOf(record), trim: true, once: false, out record.Animation))
            record.Animation = null;
        id = PictureNow(record, out frames);
        record.ShownId = id;
        Track(record);
        PropInstance instance;
        if (p.Kind is PlacedKind.Billboard or PlacedKind.Item or PlacedKind.Resting)
        {
            // The ground offset is the root's pixel offset (the picture carries none: M3 R2; an item's is scaled with it, M3 R3); Height lifts a thing onto the table it stands on.
            // An item (D121) is drawn at the item scale about the bottom centre of its picture, which rests on the instance's origin: the shader drops the picture's own vertical offset.
            // A resting billboard (a machine on a table, a holopad) is the same at the tile scale.
            // DV49: a wide picture tg centres by its pixel_x (a tree) takes it across the view instead, as the shader's sideways move (the basis' y.z).
            // A tuned type (WorldLooks) is drawn at its size about its feet (the basis, which the shader turns about the icon's centre: the origin goes down
            // to keep the feet where they were) and moved up by hand.
            float across = AcrossOf(record) * p.Scale;
            WorldLook look = WorldLooks.Active.Find(record.Entity.TypePath);
            Vector3 rest = p.Kind == PlacedKind.Item && TryShelf(record, out Vector3 shelf) ? shelf
                : center + new Vector3(across == 0f ? p.OffsetX : look.X, p.Height > 0f && p.Height == WorldGeometry.TableTop ? TableTopAt(p.X, p.Y, p.Z) : p.Height, -p.OffsetY);
            float pivot = (p.Kind == PlacedKind.Item ? WorldScale.ItemScale : 1f) * (TileSpace.TileSize / 2f
                - (p.Kind is PlacedKind.Item or PlacedKind.Resting ? _atlas.CentreHeightOf(id) - _atlas.QuadHeightOf(id) / 2f : 0f));
            rest.Y += look.Y - pivot * (1f - p.Scale);
            instance = new PropInstance
            {
                // A thing on a closet's tile rests on its shelf (docs/ai/specs/models.md).
                Transform = new Transform3D(TunedBasis(p.Scale, across), rest),
                // its place in its tile's draw order (DepthRank: tg's layer, then its id), so the things of one tile never fight or swap
                Color = new Color((p.Kind == PlacedKind.Item ? PieceParts.ModeItem : p.Kind == PlacedKind.Resting ? PieceParts.ModeResting : PieceParts.ModeBillboard) / 8f,
                    DepthRank.Of(_replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry layered) ? layered.Layer : 0f, record.Entity.Id), 0, 0),
                Custom = new Color(id % 2048, id / 2048, BillboardMath.YawOfDir(p.Dir), frames),
            };
        }
        else if (p.Kind == PlacedKind.FloorDecal)
        {
            // The thing lies flat a few millimetres above the floor, at the tile scale, the picture's up pointing north. Its height is its layer's place in the stack (D122: the
            // lattice, the disposals, the wires, the pipes, the vents; Placed.Height), sent to the shader as green in 1/16 m so that the order holds at every distance.
            instance = new PropInstance
            {
                Transform = new Transform3D(TunedBasis(p.Scale, 0f), center + new Vector3(p.OffsetX, 0f, -p.OffsetY)), // (a tuned size about the tile's centre)
                Color = new Color(PieceParts.ModeFloorDecal / 8f, p.Height * PieceParts.LiftScale, 0, 0),
                Custom = new Color(id % 2048, id / 2048, 0, 1),
            };
        }
        else if (p.Fin)
        {
            // A camera standing out of the wall at a right angle (WallMounts.FinPlacement); it never turns.
            (System.Numerics.Vector3 at, float yaw) = WallMounts.FinPlacement(p, _atlas.QuadHeightOf(id));
            instance = new PropInstance
            {
                Transform = new Transform3D(Basis.Identity, new Vector3(at.X, at.Y, at.Z)),
                Color = new Color(PieceParts.ModeDecal / 8f, 0, 0, YawTurns(yaw)),
                Custom = new Color(id % 2048, id / 2048, 0, 1),
            };
        }
        else
        {
            // A decal on the wall face (WallMounts.DecalPlacement: on the face of the wall cell, the mount height moved so that the whole picture
            // stays on it, the offset along the wall from the root's pixel offset, which the picture does not carry).
            (System.Numerics.Vector3 at, float yaw) = WallMounts.DecalPlacement(DecalPlaced(record), _atlas.QuadHeightOf(id));
            var origin = new Vector3(at.X, at.Y, at.Z);
            instance = new PropInstance
            {
                Transform = new Transform3D(Basis.Identity, origin),
                Color = new Color(PieceParts.ModeDecal / 8f, 0, 0, YawTurns(yaw)),
                Custom = new Color(id % 2048, id / 2048, 0, 1),
            };
        }
        record.Instances.Add(instance);
        record.Batch.Quads.Add(instance);
        if (p.Kind == PlacedKind.WallDecal && !p.Fin && TryOtherGlassFace(p, out Placed other))
        {
            (System.Numerics.Vector3 at, float yaw) = WallMounts.DecalPlacement(other, _atlas.QuadHeightOf(id));
            var back = new PropInstance
            {
                Transform = new Transform3D(Basis.Identity, new Vector3(at.X, at.Y, at.Z)),
                Color = new Color(PieceParts.ModeDecal / 8f, 0, 0, YawTurns(yaw)),
                Custom = new Color(id % 2048, id / 2048, 0, 1),
            };
            record.Instances.Add(back);
            record.Batch.Quads.Add(back);
        }
        record.Batch.Dirty = true;
        _dirty.Add(record.Batch);
        if (p.Kind == PlacedKind.WallDecal && !p.Fin)
            RejoinSplitNeighbours(record);
    }

    // A floor hatch's model (a vent, a scrubber) is its own sprite alone: tg's pipe stubs under it (components_base.dm update_icon: the pipe_underlays.dmi
    // underlays, from the tile's edge to its middle) lie flat on its tile as the pipes do, so that the pipe reaches the vent.
    private void AddPipeUnderlays(StaticRecord record, Vector3 center)
    {
        Placed p = record.Placed;
        if (!PropKinds.Is(record.Entity.TypePath, "/obj/machinery/atmospherics") || !_replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry entry))
            return;
        foreach (TableRef under in entry.Underlays)
        {
            if (!_tables.TryGetAppearance(under, out AppearanceEntry underlay) || underlay.Alpha == 0
                || !_atlas.TryPictureFacing(_tables, under, (byte)p.Dir, trim: true, out int id))
                continue;
            var instance = new PropInstance
            {
                Transform = new Transform3D(Basis.Identity, center + new Vector3(p.OffsetX, 0f, -p.OffsetY)),
                Color = new Color(PieceParts.ModeFloorDecal / 8f, p.Height * PieceParts.LiftScale, 0, 0),
                Custom = new Color(id % 2048, id / 2048, 0, 1),
            };
            record.Instances.Add(instance);
            record.Batch.Quads.Add(instance);
        }
        record.Batch.Dirty = true;
        _dirty.Add(record.Batch);
    }

    // The resting look of a batched picture (prop_atlas.gdshaderinc): its tuned size (WorldLooks) in the transform's linear part and DV49's move across the view.
    private static Basis TunedBasis(float scale, float across)
        => scale == 1f && across == 0f ? Basis.Identity : new Basis(new Vector3(scale, 0f, 0f), new Vector3(0f, scale, across), Vector3.Back);

    // DV49: how far a standing picture tg centres on its tile by its pixel_x alone (a tree: 64 pixels at -16) is moved across the view, in metres; 0 for any other.
    private float AcrossOf(StaticRecord record)
    {
        Placed p = record.Placed;
        if (p.Kind is not (PlacedKind.Billboard or PlacedKind.Resting) || _textures == null || _tables == null
            || !_replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry root) || root.PixelX == 0
            || !_textures.TryGet(_tables, Look(record), ByondDir.South, 0, out ComposedRaster raster, out _))
            return 0f;
        return SpriteRect.Of(raster.Image.Width, raster.Image.Height, raster.OriginX, raster.OriginY).IsCentredWide(root.PixelX)
            ? root.PixelX * (TileSpace.TileSize / 32f) : 0f;
    }

    // M4v slice 4: a wall decal's (or a fin's) yaw travels in its instance colour's alpha, in turns (0 to 1), so that its basis is free for
    // animate()'s look as a billboard's is (prop_atlas.gdshader).
    private static float YawTurns(float yaw)
    {
        float turns = yaw / Mathf.Tau;
        return turns - Mathf.Floor(turns);
    }

    // The direction a thing's pictures are composed for: a floor decal's own dir, a fin's side view; -1: by where the viewer stands.
    // A console or computer always shows tg's front view (its south picture), the billboard turned to the viewer (the owner, 2026-10-15: its
    // pictures for the other dirs are tg's side and back views, so walking round it the screen turned away).
    private int FacingOf(StaticRecord record)
    {
        Placed p = record.Placed;
        return p.Kind == PlacedKind.FloorDecal ? p.Dir : p.Fin ? WallMounts.FinPictureDir(p.Face) : FrontOnly(record) ? ByondDir.South : -1;
    }

    private static bool FrontOnly(StaticRecord record)
        => record.Placed.Kind is PlacedKind.Billboard or PlacedKind.Resting
            && (PropKinds.Is(record.Entity.TypePath, "/obj/machinery/computer") || PropKinds.Is(record.Entity.TypePath, "/obj/machinery/modular_computer"));

    private void BuildPiece(StaticRecord record, Vector3 center)
    {
        Placed p = record.Placed;
        int id = -1;
        record.PictureIsFallback = false;
        record.PieceOwnPicture = false;
        record.Animation = null; // M4v slice 2: the picture getters below set the animation of a picture that plays tg's frames
        bool found;
        if (p.Shape == RenderKind.Grille && IsBlock(p))
            found = TryOwnPiecePicture(record, out id); // a grille block shows tg's grille sprite on its faces
        else if (p.Shape == RenderKind.Grille)
        {
            id = _atlas.Swatch(0x6E7378FF);
            found = true;
        }
        else if (p.Shape == RenderKind.LightFixture)
            found = TryLampPicture(record, out id);
        else if (BorderCounterparts.Applies(p.Shape))
        {
            found = TryBorderPicture(record, out id);
            if (found && WorldScale.BlockPieces && p.Shape == RenderKind.DirectionalWindow)
                id = BorderWindowPicture(record, id);
        }
        else if (PieceParts.IsDoor(p.Shape))
            found = TryDoorPicture(record, out id);
        else if (p.Shape == RenderKind.FullWindow)
            found = TryWindowPicture(record, out id);
        else if (p.Shape is RenderKind.Girder or RenderKind.FalseWall)
            found = TryStraightRunPicture(record, out id);
        else
            found = TryOwnPiecePicture(record, out id);
        if (!found)
            return;
        record.PictureId = id;
        record.Frames = 1;
        id = PictureNow(record, out _); // a flick that plays, else the animation's picture now, else the still one
        record.ShownId = id;
        // A piece a 3D model replaces (a station airlock, a metal table): the model, which may take this picture (an airlock's, with tg's overlays and
        // its animation); a model with its own texture plays no icon animation.
        if (TryBuildModel(record, center, id, out bool ownTexture))
        {
            if (ownTexture)
            {
                record.Animation = null;
                record.Flick = null;
            }
            SplitAcrossGlass(record);
            MirrorFirelock(record);
            Track(record);
            return;
        }
        Track(record);
        if (IsBlock(p))
        {
            BuildBlock(record, center, id);
            return;
        }
        // A joined window or grille is built in world directions (its arms are told apart by the joints mask), so it is never turned.
        float yaw = p.Face != 0 && p.Shape is not (RenderKind.FullWindow or RenderKind.Grille or RenderKind.Girder or RenderKind.FalseWall) ? TileSpace.DirYaw(p.Face) : 0f;
        Vector3 origin = center + (p.Shape == RenderKind.LightFixture ? new Vector3(0, p.Height, 0) : Vector3.Zero);
        if (p.Shape == RenderKind.LightFixture && p.OffsetX != 0f)
            origin += p.Face is ByondDirConst.North or ByondDirConst.South ? new Vector3(p.OffsetX, 0, 0) : new Vector3(0, 0, -p.OffsetX);
        var basis = new Basis(Vector3.Up, yaw);
        if (p.Shape == RenderKind.Curtain)
            basis *= Basis.FromScale(new Vector3(1f, 1f, CurtainThin)); // a cloth sheet, not the pod door's 24 cm slab it borrows
        if (p.Shape == RenderKind.LightFixture)
            basis *= Basis.FromScale(new Vector3(record.LampHalfWidth / WorldGeometry.LampHalfWidth, record.LampHalfHeight / WorldGeometry.LampHalfHeight, 1f));
        float glow = p.Shape == RenderKind.LightFixture && LightIsOn(record) ? 1f : 0f;
        IEnumerable<int> parts = PieceParts.PartsOf(p.Shape);
        // A border piece that meets another at a corner gets its corner posts as one more instance (the part holds both ends; the joints mask says which).
        // (not a block window's: its pictures frame each wall of panes at its ends, BorderWindowJunction)
        if (p.Joints != 0 && BorderCorners.GetsPosts(p.Shape) && !(WorldScale.BlockPieces && p.Shape == RenderKind.DirectionalWindow))
            parts = parts.Append(PieceParts.PostPartOf(p.Shape));
        // D147: a full-tile window stands on its base, drawn in the window frame's steel.
        if (p.Shape == RenderKind.FullWindow)
            parts = parts.Append(PieceParts.WindowBase);
        foreach (int part in parts)
        {
            int partId = part == PieceParts.WindowBase ? WindowBasePicture() : id;
            var instance = new PropInstance
            {
                Transform = new Transform3D(basis, origin),
                Color = new Color(PieceParts.ModePiece / 8f, part / 32f, glow, record.IsDoor ? record.Door.Open : part == PieceParts.DirectionalWindow ? BorderPane(record) : HoloPane(record)),
                // The joints mask (z): the arms of a joined window or grille, the corner posts of a border piece.
                Custom = new Color(partId % 2048, partId / 2048, p.Joints | PaneCaps(part, p), 1),
                Wall = PieceParts.BlocksLight(p.Shape, record.Entity.TypePath),
            };
            record.Instances.Add(instance);
            record.Batch.Pieces.Add(instance);
        }
        SplitAcrossGlass(record);
        record.Batch.Dirty = true;
        _dirty.Add(record.Batch);
    }

    private Mesh ModelMeshAt(int index) => _models.MeshAt(index);

    // A holofirelock's slabs (tg's holosign/barrier/atmos, alpha 150 over pixels of alpha 150) are see-through: their pixels drawn as a stipple of their alpha
    private static float HoloPane(StaticRecord record)
        => record.Placed.Shape == RenderKind.Firelock && PropKinds.Is(record.Entity.TypePath, "/obj/structure/holosign") ? 1f : 0f;

    // Gaps S2: a thing tg turns by a quarter (a vending machine tipped over: tilting.dm's fall_and_crush, turn(transform, 90 or 270)) lies on
    // its side: the sprite's top turned east (clockwise, 90) or west (270) is the model's top falling that way. -1 or 1 (the turn's sign), 0 upright.
    private static float QuarterTurn(AppearanceEntry entry)
        => entry.Transform is { Length: 6 } t && MathF.Abs(t[0]) < 0.01f && MathF.Abs(MathF.Abs(t[1]) - 1f) < 0.01f ? MathF.Sign(t[1]) : 0f;

    // The model turned about the world's north-south axis, its middle on the tile's middle and its new underside on the floor.
    private Transform3D LyingTransform(Vector3 origin, float yaw, int bodyMesh, float turn)
    {
        var upright = new Basis(Vector3.Up, yaw);
        var fall = new Basis(Vector3.Back, -turn * Mathf.Pi / 2f); // +Y to +X (east) for a clockwise turn
        Aabb box = ModelMeshAt(bodyMesh)?.GetAabb() ?? new Aabb(Vector3.Zero, Vector3.One);
        Vector3 middle = upright * box.GetCenter();
        float halfWide = (MathF.Abs(upright.X.X) * box.Size.X + MathF.Abs(upright.Z.X) * box.Size.Z) / 2f;
        return new Transform3D(fall * upright, origin + new Vector3(0f, halfWide, 0f) - fall * middle + new Vector3(middle.X, 0f, middle.Z));
    }

    private Material ModelMaterialAt(int index) => _models.MaterialAt(index);

    /// <summary>
    /// The whole mirrors (tg's <c>/obj/structure/mirror</c>, not broken) within <paramref name="range"/> metres of <paramref name="eye"/>: the middle
    /// of each one's glass, a hair before its picture, and the way it faces (Render/MirrorPass.cs).
    /// </summary>
    public void MirrorsNear(Vector3 eye, float range, List<(Vector3 Centre, Vector3 Normal)> into)
    {
        into.Clear();
        if (_atlas == null || _tables == null)
            return;
        foreach (StaticRecord record in _records.Values)
        {
            Placed p = record.Placed;
            if (p.Kind != PlacedKind.WallDecal || p.Fin || record.Model != null || record.PictureId < 0
                || record.Entity.TypePath == null || !record.Entity.TypePath.StartsWith("/obj/structure/mirror", StringComparison.Ordinal))
                continue;
            if (!_tables.TryGetAppearance(Look(record), out AppearanceEntry look) || _tables.ResolveString(look.IconState) is not ("mirror" or "magic_mirror"))
                continue; // tg's mirror_broke reflects nothing
            (System.Numerics.Vector3 at, float yaw) = WallMounts.DecalPlacement(DecalPlaced(record), _atlas.QuadHeightOf(record.PictureId));
            var normal = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
            Vector3 centre = new Vector3(at.X, at.Y, at.Z) + normal * 0.004f;
            if (centre.DistanceTo(eye) <= range)
                into.Add((centre, normal));
        }
    }

    // The azimuth (degrees, tg's: clockwise from north) a solar panel or tracker is turned to: tg turns the glass and the dish, overlays in its vis_contents, by a transform
    // (solar.dm:174, tracker.dm:84), which the replicator flattens into the thing's overlays; a head not turned yet faces azimuth 0, as tg's does (ModelRule.Sun).
    // DEVIATION(DV74): tg's 2.5 s tween (an animate() of a vis_contents child) is not sent; the head turns to the end transform along its own eased
    // curve (SunHeadMotion), slower than tg's.
    private float SunAzimuth(AppearanceEntry entry, ModelEntry model)
    {
        foreach (TableRef overlay in entry.Overlays)
        {
            if (_replica.TryGetAppearance(overlay, out AppearanceEntry look) && look.Transform is { Length: 6 } t
                && (_replica.ResolveString(look.IconState) ?? string.Empty).StartsWith(model.Azimuth ?? string.Empty, StringComparison.Ordinal))
                return ModelRules.AzimuthOf(t[0], t[1]);
        }
        return 0f;
    }

    // The 3D model that replaces the thing's picture (ModelLibrary, docs/ai/specs/models.md): the parts its rule picks (a table's module by tg's junction, a chair turned
    // by its dir, an airlock's leaves across its passage), each an instance of the chunk's MultiMesh of that part. A model that takes the entity's picture is given it
    // (spriteId). False when no model draws the thing or a part cannot be built: it keeps its 2D picture.
    private bool TryBuildModel(StaticRecord record, Vector3 origin, int spriteId) => TryBuildModel(record, origin, spriteId, out _);

    /// <summary>
    /// tg's merged transport platform (transport_module.dm create_modular_set: a tram's or a wide elevator's other tiles are the corner module's overlays,
    /// each at its pixel offset): the pieces of each such overlay that has a model of its own (a subfloor, a cut corner), placed on its tile relative to the
    /// thing's origin. Empty for a thing with no such overlays; null while a model's textures still decode.
    /// </summary>
    private List<(int Mesh, int Picture, Transform3D At)> MergedTileModels(AppearanceEntry entry)
    {
        var found = new List<(int, int, Transform3D)>();
        foreach (TableRef reference in entry.Overlays)
        {
            if (!_tables.TryGetAppearance(reference, out AppearanceEntry overlay) || overlay.PixelX % 32 != 0 || overlay.PixelY % 32 != 0 || overlay.PixelX == 0 && overlay.PixelY == 0)
                continue;
            string state = _tables.ResolveString(overlay.IconState);
            ModelEntry model = _models.Catalog.Find(_tables.ResolveResource(overlay.Icon), state);
            if (model == null || model.Texture != ModelTexture.Model || model.Rule != ModelRule.Single)
                continue;
            _models.PrepareFamily(model, overlay.Rgba, null);
            var shift = new Vector3(overlay.PixelX / 32f * TileSpace.TileSize, 0f, -overlay.PixelY / 32f * TileSpace.TileSize);
            foreach (ModelPlacement placement in ModelRules.Choose(model, state, overlay.Dir, 0))
            {
                if (!_models.TryPieces(model, placement.Part, overlay.Rgba, out ModelPiece[] pieces))
                    return null;
                foreach (ModelPiece piece in pieces)
                    found.Add((piece.Mesh, piece.Picture, new Transform3D(new Basis(Vector3.Up, placement.Yaw), shift)));
            }
        }
        return found;
    }

    private bool TryBuildModel(StaticRecord record, Vector3 origin, int spriteId, out bool ownTexture)
    {
        using (Dev.FrameProbe.Measure("model", record.Entity.TypePath))
            return TryBuildModelNow(record, origin, spriteId, out ownTexture);
    }

    private bool TryBuildModelNow(StaticRecord record, Vector3 origin, int spriteId, out bool ownTexture)
    {
        ownTexture = false;
        if (_models == null || !_replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry entry))
            return false;
        // An entry may need one of the appearance's overlays (a lamp lit, at night, in an emergency: its glow overlay; ModelEntry.Overlays).
        // A thing with no icon of its own draws only its overlays (tg's single-port pipes: update_pipe_icon's icon = null, the multi-deck adapter): their icon finds its model.
        string modelIcon = _replica.ResolveResource(entry.Icon) ?? ModelOverlays.All(_tables, entry).Select(o => _tables.ResolveResource(o.Entry.Icon)).FirstOrDefault(i => i != null);
        ModelEntry model = record.ModelOverride ?? _models.Catalog.Find(modelIcon, record.SeenState, ModelOverlayStates(record, entry));
        if (model == null || model.Texture == ModelTexture.Sprite && spriteId < 0)
            return false;
        // Stairs with no level above them (every flight of a single-level station) lead nowhere: tg's flat picture on the floor, not a flight (as a lone ladder lies down).
        if (model.RestOnRamp && !LevelAbove(record.Cell.Z))
            return false;
        // A wall decal is drawn by a wall model only (ModelRule.Wall).
        if (record.Placed.Kind == PlacedKind.WallDecal && model.Rule != ModelRule.Wall)
            return false;
        // A wall model's offset reaches for a wall: off any wall (a button on the warden's desk) it would hang in the air, so tg's picture stands where the map put it.
        if (model.Rule == ModelRule.Wall && record.Placed.Kind is PlacedKind.Billboard or PlacedKind.Resting or PlacedKind.FloorDecal)
            return false;
        // drawn by another thing's model (ModelEntry.Hidden: a gravity generator's outer parts)
        if (model.Hidden)
        {
            record.Model = model;
            return true;
        }
        ownTexture = model.Texture == ModelTexture.Model;
        if (model.Rule is ModelRule.Closet or ModelRule.Crate)
        {
            ownTexture = true; // its pictures are its states', not the appearance's animation
            return model.Rule == ModelRule.Closet ? TryBuildCloset(record, entry, model, origin) : TryBuildCrate(record, entry, model, origin);
        }
        if (model.Rule == ModelRule.Console && model.Texture != ModelTexture.Model)
        {
            ownTexture = true;
            return TryBuildConsole(record, entry, model, origin);
        }
        // A wall model in the wall cell stands on the open tile before its face: the wall behind it is the other way.
        byte face = record.Placed.InWall ? (byte)(record.Placed.Face is ByondDirConst.North or ByondDirConst.South ? record.Placed.Face ^ 3 : record.Placed.Face ^ 12)
            : record.Placed.Face;
        float sunAzimuth = model.Rule == ModelRule.Sun ? SunAzimuth(entry, model) : 0f;
        ModelPlacement[] placements = ModelRules.Choose(model, record.SeenState, record.Entity.State.Dir, face, sunAzimuth);
        SunHeadMotion sun = model.Rule == ModelRule.Sun ? AimSunHead(record, model, sunAzimuth) : null;
        var parts = new List<(int Mesh, int Picture, float Yaw, bool Live, float Glow, ModelPart Spec, bool Shaded, Vector3 Pivot, AtlasAnimation Animation)>(placements.Length);
        ModelFrame liveFrame = null;
        int livePicture = -1;
        HashSet<string> overlays = null;
        string icon = _replica.ResolveResource(entry.Icon);
        string gags = icon != null && icon.StartsWith("gags:", StringComparison.Ordinal) ? icon : null;
        _models.PrepareFamily(model, entry.Rgba, gags);
        for (int i = 0; i < placements.Length; i++)
        {
            // A part drawn with certain overlays only (ModelPart.Overlays: a canister's pressure lights, its held tank behind the open door).
            if (model.Parts.TryGetValue(placements[i].Part, out ModelPart partSpec)
                && (!partSpec.DrawnWith(overlays ??= ModelOverlayStates(record, entry).ToHashSet()) || !partSpec.ShownIn(record.SeenState)
                    || !partSpec.ShownFacing(record.Entity.State.Dir)
                    || partSpec.Deck != null && !partSpec.ShownOnDeck(DeckLinked(record, model, 1), DeckLinked(record, model, -1))))
                continue;
            if (partSpec.OverlayPixelW is short slot && !ModelOverlays.HasSlot(_tables, entry, partSpec.Overlays, slot))
                continue;
            if (model.Texture == ModelTexture.Model)
            {
                // A model with its own textures: one piece per material (its atlas tiles, a light layer, plain glass), tinted by tg's colour where the material says so.
                // A table whose top shows tg's picture (ModelEntry.SpriteTop) is built for its junction, turned and its top's uv laid in the world's frame, its
                // empty inner corners cut: placed unturned.
                int junction = -1;
                if (model.SpriteTop && model.Rule == ModelRule.Smooth4)
                {
                    junction = ModelRules.Junction(record.SeenState);
                    placements[i] = placements[i] with { Yaw = 0f };
                }
                // A part tinted by a picture of the thing's (ModelPart.TintIcon: a canister's held tank; TintVisuals: the gases in its window).
                uint tint = partSpec?.TintIcon != null ? OverlayIconTint(entry, partSpec.TintIcon)
                    : partSpec?.TintVisuals != null ? VisualsTint(entry, partSpec.TintVisuals)
                    : partSpec?.TintOverlay != null ? ModelOverlays.Tint(_tables, entry, partSpec.TintOverlay) : entry.Rgba;
                int pending = _models.PendingTextures;
                if (!_models.TryPieces(model, placements[i].Part, tint, out ModelPiece[] pieces, junction, gags))
                    return _models.PendingTextures != pending; // its textures still decode: nothing drawn until they are in, not its flat picture
                foreach (ModelPiece piece in pieces)
                {
                    // An overlay's part (ModelEntry.OverlayParts) is drawn while the appearance has that overlay.
                    if (model.OverlayParts && piece.Frame?.State != null && !ModelFrame.Follows(piece.Frame.State, record.SeenState)
                        && !(overlays ??= ModelOverlayStates(record, entry).ToHashSet()).Contains(piece.Frame.State))
                        continue;
                    // A live frame (ModelFrame): the thing's own picture as tg composes it now (its state, its overlays) for the frame's dir, while it shows the
                    // frame's state or a variant of it (a vending machine off or broken); else the frame the model was made from.
                    ModelFrame surfaceFrame = partSpec.LiveFrame(piece.Frame);
                    bool live = partSpec.Live && surfaceFrame != null && (liveFrame == null || liveFrame.Dir == surfaceFrame.Dir) && ModelFrame.Follows(surfaceFrame.State, record.SeenState)
                        && (livePicture >= 0 || TryLivePicture(record, surfaceFrame, out livePicture));
                    if (live)
                        liveFrame ??= surfaceFrame;
                    // A light layer (ModelLightLayers: a lamp's glow, tg's lighting_overlay; an alarm's light, its emissive_appearance) is bright in the dark,
                    // as a lit lamp's picture. Its strength is the material's (a button's light mask is a soft halo, tg's mask alpha).
                    float glow = ModelLightLayers.Is(piece.Material) || piece.Emissive ? piece.Glow : 0f;
                    glow = partSpec.Glow ?? glow;
                    if (partSpec.GlowOverlays.Length > 0 && !partSpec.GlowOverlays.Any((overlays ??= ModelOverlayStates(record, entry).ToHashSet()).Contains))
                        glow = 0f;
                    AtlasAnimation animation = null;
                    int piecePicture = live ? livePicture : piece.Picture;
                    if (!live && partSpec.Live && model.OverlayFrames && piece.Frame?.State != null
                        && (overlays ??= ModelOverlayStates(record, entry).ToHashSet()).Contains(piece.Frame.State)
                        && TryModelOverlayPicture(entry, piece.Frame, out int overlayPicture, out animation))
                        piecePicture = overlayPicture;
                    parts.Add((piece.Mesh, piecePicture, placements[i].Yaw, live, glow, partSpec, piece.Shaded && glow <= 0f, piece.Pivot, animation));
                }
                continue;
            }
            if (!_models.TryPart(model, placements[i].Part, out int mesh, out int picture))
                return false;
            if (model.Texture == ModelTexture.Sprite)
            {
                // The entity's picture, or a part's own state of its icon (an external airlock's frame shows the open state, which has no leaves in it).
                string own = model.Parts[placements[i].Part].Picture;
                picture = spriteId;
                if (own != "closed" && _atlas.TryLibraryPicture(_replica.ResolveResource(entry.Icon), own, trim: false, out int ownPicture))
                    picture = ownPicture;
            }
            parts.Add((mesh, picture, placements[i].Yaw, false, 0f, partSpec, false, Vector3.Zero, null));
        }
        if (parts.Count == 0)
            return false;
        // tg's merged platform: its other tiles are its overlays at their pixel offsets, each drawn by its own model there
        int pendingBefore = _models.PendingTextures;
        List<(int Mesh, int Picture, Transform3D At)> merged = MergedTileModels(entry);
        if (merged == null)
            return _models.PendingTextures != pendingBefore;
        // A wall fixture (a lamp, an alarm, a camera) at its mount height and its offset along the wall, as the planner placed it (ModelRule.Wall).
        if (model.Rule == ModelRule.Wall && record.Placed.Kind is PlacedKind.Piece or PlacedKind.WallDecal)
        {
            Placed wp = record.Placed;
            origin += new Vector3(0f, wp.Height, 0f)
                      + (wp.Face is ByondDirConst.North or ByondDirConst.South ? new Vector3(wp.OffsetX, 0f, 0f) : new Vector3(0f, 0f, -wp.OffsetX));
        }
        // The catalog's offset, in the model's own frame (a firelock beside its tile's airlock, a windoor on the tile's edge, a lamp on the wall's face).
        origin += new Basis(Vector3.Up, parts[0].Yaw) * new Vector3(model.Offset.X, model.Offset.Y, model.Offset.Z);
        // A door that is not one of the door animation's (a firelock) slides by its own state (SlideByState).
        bool byState = model.Rule == ModelRule.Airlock && !record.IsDoor;
        if (byState)
        {
            ReadSlideState(record, model);
            ownTexture = true; // its parts show their own states; tg's flick drives the slide instead of its frames
        }
        bool hinged = model.Rule is ModelRule.Wall or ModelRule.Single or ModelRule.Dir4
                      && (model.OpenOverlays.Length > 0 || model.OpenStates.Length > 0 || model.Parts.Values.Any(p => p.Flick != null));
        if (hinged)
        {
            record.ClosetOpening = CrateLook.Open(model, record.SeenState, overlays ??= ModelOverlayStates(record, entry).ToHashSet());
            if (!record.ClosetSeen)
                record.ClosetOpen = record.ClosetOpening ? 1f : 0f;
            record.ClosetSeen = true;
            record.ModelOrigin = origin;
            record.ModelYaw = parts[0].Yaw;
            if (!_closets.Contains(record))
                _closets.Add(record);
        }
        // a lamp's light layer carries its glow plus 2, so the shader gives lamps their own strength (anomaly_lamp_glow)
        float lampMark = record.Placed.Shape == RenderKind.LightFixture ? 2f : 0f;
        float lying = QuarterTurn(entry);
        foreach ((int mesh, int picture, float yaw, bool live, float glow, ModelPart spec, bool shaded, Vector3 pivot, AtlasAnimation animation) in parts)
        {
            var instance = new PropInstance
            {
                Transform = lying != 0f ? LyingTransform(origin, yaw, parts[0].Mesh, lying) : new Transform3D(new Basis(Vector3.Up, yaw), origin),
                Color = new Color(PieceParts.ModeModel / 8f, glow > 0f ? PieceParts.Light / 32f : 0f, glow > 0f ? glow + lampMark : 0f,
                    record.IsDoor ? record.Door.Open : byState ? record.ClosetOpen : 0f),
                // w 2: a flat-colour piece the shader shades by its normals (prop_atlas.gdshader)
                Custom = new Color(picture % 2048, picture / 2048, 0, shaded ? 2 : 1),
                Mesh = mesh,
                Wall = PieceParts.BlocksLight(record.Placed.Shape, record.Entity.TypePath),
            };
            // a solar panel's or tracker's head, built about its tilt pivot, shows the aim it is turning through (StepSunHeads)
            if (sun != null && spec?.Pivot == true)
            {
                float yawOffset = yaw - ModelRules.AzimuthYaw(sunAzimuth); // the catalog's turn of the part
                // a panel's head stands a little higher, so that the pedestal's beam does not show under it when it is tilted down to the horizon
                Vector3 headPivot = model.Azimuth == "solar_panel_" ? pivot + new Vector3(0f, SolarPanelLift, 0f) : pivot;
                record.SunHeads.Add((instance, headPivot, yawOffset));
                instance.Transform = SunHeadTransform(origin, headPivot, yawOffset, sun);
            }
            if (hinged && spec.Pivot && (spec.Degrees != 0f || spec.Move != default || spec.Flick != null || spec.OpenScale != System.Numerics.Vector3.One))
            {
                var axis = new Vector3(spec.Axis.X, spec.Axis.Y, spec.Axis.Z);
                (float sx, float sy) = ModelMesh.Scale(model, TileSpace.TileSize, WorldScale.WallHeight, WorldScale.TableTop);
                var move = new Vector3(spec.Move.X * sx, spec.Move.Y * sy, spec.Move.Z * sx);
                Vector3 flickMove = spec.Flick == null ? Vector3.Zero : new Vector3(spec.Flick.Move.X * sx, spec.Flick.Move.Y * sy, spec.Flick.Move.Z * sx);
                var openScale = new Vector3(spec.OpenScale.X, spec.OpenScale.Y, spec.OpenScale.Z);
                instance.Transform = HingeTransform(record, pivot, axis, spec.Degrees, move, spec.Flick, flickMove, openScale);
                record.Hinged.Add((instance, pivot, axis, spec.Degrees, move, spec.Flick, flickMove, openScale));
            }
            else if (spec?.Pivot == true && spec.Spin != 0f)
            {
                var axis = new Vector3(spec.Axis.X, spec.Axis.Y, spec.Axis.Z).Normalized();
                record.ModelOrigin = origin;
                record.ModelYaw = parts[0].Yaw;
                instance.Transform = SpinTransform(record, pivot, axis, spec.Spin);
                record.Spinning.Add((instance, pivot, axis, spec.Spin));
                _spinners.Add(record);
            }
            record.Instances.Add(instance);
            record.Batch.AddModel(instance);
            if (animation != null)
            {
                record.OverlayFrames.Add((instance, animation));
                _overlayAnimated.Add(record);
            }
            if (live)
                record.LiveFrames.Add(instance);
            if (spec != null && spec.BlinkOn > 0f && spec.BlinkOff > 0f)
            {
                int offPicture = spec.OffColor == null ? -1 : _atlas.Swatch(ModelColors.Swatch(null, System.Numerics.Vector4.One, null, color: spec.OffColor));
                record.Blinking.Add((instance, instance.Transform, spec, instance.Color, instance.Custom, offPicture));
                _blinkers.Add(record);
            }
        }
        foreach ((int mesh, int picture, Transform3D at) in merged)
            AddModelInstance(record, mesh, picture, at with { Origin = at.Origin + origin });
        record.PictureId = parts[0].Picture;
        if (liveFrame != null)
        {
            // Its live frames play the state's animation and its flicks (StepAnimations, StartFlick), as the picture would; the animation's pictures are
            // stored in the authored frame dimensions, including wide signs and taller frames.
            ownTexture = false;
            record.LiveState = liveFrame.State;
            record.LiveDir = liveFrame.Dir;
            record.LiveSquare = IsSquareFrame(liveFrame);
            record.PictureId = livePicture;
            record.Frames = 1;
            if (!_atlas.TryModelAnimation(_tables, Look(record), liveFrame.Dir, liveFrame.Width, liveFrame.Height, out record.Animation))
                record.Animation = null;
            // Tiles of a belt seen at different times still meet at their joints: they all play by the one clock.
            if (model.Rule == ModelRule.Dir8)
                record.AnimationStart = 0;
            record.ShownId = livePicture;
            Track(record);
        }
        record.Model = model;
        record.ModelOrigin = origin;
        record.ModelYaw = parts[0].Yaw;
        foreach (var part in parts.GroupBy(p => p.Spec).Select(g => g.First()))
            if (part.Spec?.Light != null)
                AddModelLight(record, part.Spec, part.Pivot);
        TrackModelFilters(record, entry);
        record.Batch.Dirty = true;
        _dirty.Add(record.Batch);
        if (model.Rule == ModelRule.Smooth4 || model.RestOnTop)
            RebuildRestingOn(record); // the things on the table or bed rest on its own top
        if (byState && !_closets.Contains(record))
            _closets.Add(record);
        return true;
    }

    // A live frame's picture: the thing's appearance composed for the frame's dir, the frame's size (a 32 x 32 square for any dir; a taller frame, a railing's
    // 32 x 48, for the south one, the way the closets' frames are cut).
    private bool TryLivePicture(StaticRecord record, ModelFrame frame, out int id)
    {
        using (Dev.FrameProbe.Measure("live", record.Entity.TypePath))
            return TryLivePictureNow(record, frame, out id);
    }

    private bool TryLivePictureNow(StaticRecord record, ModelFrame frame, out int id)
    {
        if (IsSquareFrame(frame))
            return _atlas.TryPictureFacing(_tables, Look(record), frame.Dir, trim: false, out id);
        id = -1;
        return frame.Dir == ByondDir.South && _atlas.TryPictureFrame(_tables, Look(record), frame.Width, frame.Height, out id);
    }

    private static bool IsSquareFrame(ModelFrame frame)
        => frame.Width == AppearanceCompositor.DefaultIconSize && frame.Height == AppearanceCompositor.DefaultIconSize;

    private static bool IsOpenLooking(string state) => state is "open" or "opening" or "closing" or "deny";

    // D147: a full-tile window's base shows tg's catwalk grating (the joined state, no edges) over dark steel; a flat steel without the icons.
    private const uint WindowBaseColour = 0x2E3236FF;

    private int WindowBasePicture()
        => _atlas.TryLibraryPictureOver("icons/obj/smooth_structures/catwalk.dmi", "catwalk-255", WindowBaseColour, out int id) ? id : _atlas.Swatch(WindowBaseColour);

    // A piece drawn with its own appearance (a table, a railing, flaps, a firelock; a door at rest closed or denying): its picture, and
    // (M4v slice 2, D143) the animation of its icon states by tg's delays: a firelock's opening and closing frames (it does not move in
    // 3D, its slab shows them), its deny blink and its alarm lights; an airlock's emergency and RETA blink, sparks and deny lights.
    private bool TryOwnPiecePicture(StaticRecord record, out int id)
    {
        TableRef appearance = Look(record);
        if (!_atlas.TryPicture(_tables, appearance, wantFrames: false, trim: false, out id, out _))
            return false;
        record.PieceOwnPicture = true;
        if (!_atlas.TryAnimation(_tables, appearance, ByondDir.South, trim: false, once: false, out record.Animation))
            record.Animation = null;
        return true;
    }

    // M4v slice 2 (DoorPictures): the panels show the door's own appearance (animated) while it is closed or denies; while it opens
    // the first picture of its opening states and while it closes the last of its closing ones (the closed position of every layer,
    // with the green lights; the panels slide by themselves); a door first seen open the base closed state of its icon.
    private bool TryDoorPicture(StaticRecord record, out int id)
    {
        id = -1;
        int misses = _textures.Library.PendingMisses;
        DoorPicture pick = DoorPictures.Of(record.SeenState, built: false);
        if (pick == DoorPicture.Own && TryOwnPiecePicture(record, out id))
            return true; // (a state the icon lacks and no overlay to draw, a pod door's "deny", takes the closed state below)
        // its sheet is still loading: the door is drawn when it is in; the closed state below would block the frame on that load (45 ms, seconds on a busy machine)
        if (_iconsCanWait && _textures.Library.PendingMisses != misses)
            return false;
        if ((pick is DoorPicture.OwnFirst or DoorPicture.OwnLast) && _atlas.TryStill(_tables, Look(record), last: pick == DoorPicture.OwnLast, out id))
            return true;
        if (_replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry entry) && _atlas.TryLibraryPicture(_replica.ResolveResource(entry.Icon), "closed", trim: false, out id))
        {
            record.PictureIsFallback = true;
            return true;
        }
        return false;
    }

    // ONE picture per window type (D121): the straight east-west run of the type's own icon, "<base>-12", whatever junction state the server or the fake feed sends
    // (StraightRun, the rule the walls' side faces use too). The joint is drawn as geometry (the arms and the post of the piece). A state that is not a base and a junction, or an
    // icon without that run, keeps the appearance's own picture.
    private bool TryWindowPicture(StaticRecord record, out int id)
    {
        id = -1;
        if (StraightRun.TryFind(_tables, Look(record), _textures.Library, out string icon, out string state))
        {
            // M5 slice 1 (D142): a damaged window shows tg's crack overlay over its pane (window.dm:425-437)
            if (WindowCracks.TryFind(_tables, Look(record), out string crackIcon, out string crackState)
                && _atlas.TryLibraryPictureWithOverlay(icon, state, crackIcon, crackState, trim: false, out id))
                return true;
            if (_atlas.TryLibraryPicture(icon, state, trim: false, out id))
                return true;
        }
        return _atlas.TryPicture(_tables, Look(record), wantFrames: false, trim: false, out id, out _);
    }

    // M5 slice 4 (D142): a girder shows the straight east-west run of its own smoothing states ("girder-12", "reinforced-12"), as a window does, the
    // joints drawn as geometry; a girder without smoothing states (the displaced girder's "displaced") keeps its own picture.
    private bool TryStraightRunPicture(StaticRecord record, out int id)
    {
        if (StraightRun.TryFind(_tables, Look(record), _textures.Library, out string icon, out string state)
            && _atlas.TryLibraryPicture(icon, state, trim: false, out id))
            return true;
        return _atlas.TryPicture(_tables, Look(record), wantFrames: false, trim: false, out id, out _);
    }

    // M3 R6 (the owner's fourth walk): a directional window, a windoor and a border firelock are drawn by tg in the top-down view, a band of a few pixels along one edge of the square, so on a
    // pane as high as the wall only a low strip showed. They take the picture of their full-tile counterpart (BorderCounterparts: the window's straight east-west run, the full firelock's
    // state of the same name), the way the full-tile windows do (D121). A piece whose types know no counterpart stretches the opaque band of its own sprite over the pane.
    private bool TryBorderPicture(StaticRecord record, out int id)
    {
        id = -1;
        record.PictureIsFallback = false;
        RenderKind shape = record.Placed.Shape;
        string own = record.SeenState ?? StateOf(record.Entity.State.Appearance);
        // A windoor's pane is the same glass shut and open: only the firelock's picture follows its state.
        string stateKey = shape == RenderKind.FirelockBorder ? own : string.Empty;
        var key = (shape, record.Entity.TypePath, stateKey);
        if (!_borderPictures.TryGetValue(key, out (string Icon, string State)? picture))
        {
            picture = BorderCounterparts.TryPicture(shape, record.Entity.TypePath, own, _types, _textures.Library, out string icon, out string state) ? (icon, state) : null;
            _borderPictures[key] = picture;
        }
        if (picture != null && _atlas.TryLibraryPicture(picture.Value.Icon, picture.Value.State, trim: false, out id))
        {
            // M4v slice 2: the counterpart state's own frames (a border firelock's opening, closing and deny; the glass is still)
            record.Animation = _atlas.LibraryAnimation(picture.Value.Icon, picture.Value.State, trim: false);
            return true;
        }
        if (shape == RenderKind.Windoor)
            return TryDoorPicture(record, out id);
        return _atlas.TryPicture(_tables, Look(record), wantFrames: false, trim: true, out id, out _);
    }

    private bool LightIsOn(StaticRecord record)
    {
        LightState light = record.Entity.State.Light;
        string state = record.SeenState ?? string.Empty;
        bool broken = state.Contains("empty", StringComparison.Ordinal) || state.Contains("broken", StringComparison.Ordinal) || state.Contains("burned", StringComparison.Ordinal);
        return !broken && (light.IsNone || light.On);
    }

    // tg's light/overlay_icon (light.dm:16): update_overlays' glow of a working lamp, emissive_appearance(overlay_icon, base_state).
    private const string LampGlowIcon = "icons/obj/lighting_overlay.dmi";

    // tg's pixels at one scale on a lamp's box: the tube's trimmed 22 x 4 fills the D113 box (0.84 x 0.14 m), and a bulb is drawn small at
    // its own proportions instead of stretched to a tube's width.
    private const float LampMetresPerPixel = 0.0375f;

    // D142: tg's own picture on the lamp's box, so a tube, a bulb and their states can be told apart: the fixture's icon_state
    // (update_icon_state: the base state, _emergency, -empty, -burned, -broken), the state's first dir, south (the tube or bulb along the
    // tile's edge, the straight east-west run the box's front face shows, as windows and walls take one picture per type, D121), trimmed to
    // the fitting. A lit lamp has tg's glow sheet at its base state put over it (the pixels tg's emissive overlay lights: the tube or the
    // bulb, not the fitting), and the shader lights it. A colour swatch is left only for an icon the client does not have (the box keeps
    // the mesh's size then).
    private bool TryLampPicture(StaticRecord record, out int id)
    {
        record.SeenLit = LightIsOn(record);
        record.LampHalfWidth = WorldGeometry.LampHalfWidth;
        record.LampHalfHeight = WorldGeometry.LampHalfHeight;
        string state = record.SeenState ?? string.Empty;
        int cut = state.IndexOfAny(new[] { '-', '_' });
        string baseState = cut > 0 ? state.Substring(0, cut) : state;
        if (state.Length > 0 && _replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry entry)
            && ((record.SeenLit && _atlas.TryLitLibraryPicture(_replica.ResolveResource(entry.Icon), state, LampGlowIcon, baseState, trim: true, out id))
                || _atlas.TryLibraryPicture(_replica.ResolveResource(entry.Icon), state, trim: true, out id)))
        {
            (int w, int h) = _atlas.PixelSizeOf(id);
            if (w > 0 && h > 0)
            {
                record.LampHalfWidth = Math.Min(WorldGeometry.LampHalfWidth, w * LampMetresPerPixel / 2f);
                record.LampHalfHeight = Math.Min(WorldGeometry.LampHalfHeight * 2.5f, h * LampMetresPerPixel / 2f);
            }
            return true;
        }
        id = _atlas.Swatch(LightColor(record));
        return true;
    }

    private uint LightColor(StaticRecord record)
    {
        if (!LightIsOn(record))
            return 0x282828FF;
        LightState light = record.Entity.State.Light;
        if (light.IsNone)
            return 0xF2F6FFFF;
        // The glow is the light's own colour, lifted toward white so that a dim colour still reads as a lit tube.
        uint r = (light.Rgba >> 24) & 0xFF, g = (light.Rgba >> 16) & 0xFF, b = (light.Rgba >> 8) & 0xFF;
        r = (r + 255) / 2;
        g = (g + 255) / 2;
        b = (b + 255) / 2;
        return (r << 24) | (g << 16) | (b << 8) | 0xFF;
    }

    // ---- per frame ----

    /// <summary>
    /// Once per frame: uploads new atlas entries, rebuilds changed chunks, and on every new server tick looks for static entities that moved
    /// (they go to the layer as nodes), changed their picture, or are doors whose state or cell changed; then slides the moving doors.
    /// </summary>
    // The frame time the model families' warm-up may take (ModelLibrary.StepWarmUp).
    public const double WarmUpBudgetMs = 3;

    /// <summary>The frame time the model families' warm-up may take: <see cref="WarmUpBudgetMs"/> in play, more under the loading screen.</summary>
    public double WarmUpBudget { get; set; } = WarmUpBudgetMs;

    /// <summary>Model parts queued to be built before they show (<see cref="ModelLibrary.StepWarmUp"/>): the loading screen waits for none.</summary>
    public int WarmUpPending => (_models?.WarmUpCount ?? 0) + _replaceSoon.Count;

    public void Update(double delta) => Update(delta, double.NaN);

    /// <summary>
    /// Each frame: the turfs, the static things' changes (the scan of a new tick), their animations, doors and closets, and (with
    /// <paramref name="renderTick"/>, the entity layer's render tick) the glide of the model-drawn things being moved.
    /// </summary>
    public void Update(double delta, double renderTick)
    {
        StepCarried(renderTick);
        if (_turfsAwaitingIcons.Count > 0 && _textures.Library.CompletedLoads != _turfLoadsSeen)
        {
            _turfDirty.UnionWith(_turfsAwaitingIcons);
            _turfsAwaitingIcons.Clear();
        }
        if (_turfDirty.Count > 0)
        {
            using var probe = Dev.FrameProbe.Scope("map/RebuildTurfs");
            RebuildTurfs();
        }
        if (_atlas == null || _replica == null)
            return;
        RedrawAroundWhatLeft();
        _textures.Library.Pump();
        RetryAwaitingIcons();
        _clock += delta;
        if (_replica.Tick != _lastScannedTick)
        {
            _lastScannedTick = _replica.Tick;
            using var probe = Dev.FrameProbe.Scope("map/Scan");
            Scan();
        }
        using (Dev.FrameProbe.Scope("map/StepAnimations"))
        {
            StepAnimations();
            RetryTurfAttachments();
            StepAttachments();
        }
        using (Dev.FrameProbe.Scope("map/StepLooks"))
            StepLooks();
        StepTurfFrames();
        float dt = (float)delta;
        foreach (StaticRecord door in _doors)
        {
            // M10 slice E: an airlock the client guessed opening slides as tg's opening does, its cell passable from the guessed frame
            string guessed = _replica.GuessedDoorState(door.Entity.Id);
            if (guessed != null)
                door.Door.Moving = true;
            else if (!door.Door.Moving)
                continue;
            DoorAnimation.Step(ref door.Door, guessed ?? door.SeenState, guessed != null ? DoorSolid(door) : door.SeenSolid, dt);
            PushDoor(door);
        }
        StepClosets(dt);
        StepPartFlicks(dt);
        StepSunHeads(dt);
        StepBlinks();
        StepSpins();
        StepModelLights();
        StepModelOverlayFrames();
        StepModelFilters();
        StepMaptext();
        StepReplace();
        using (Dev.FrameProbe.Scope("map/atlas.Flush"))
            _atlas.Flush();
        if (_dirty.Count > 0)
        {
            using var probe = Dev.FrameProbe.Scope("map/batch.Rebuild");
            int rebuilt = 0;
            foreach (ChunkBatch batch in _dirty.ToList())
            {
                batch.Rebuild();
                _dirty.Remove(batch);
                if (++rebuilt >= RebuildsPerFrame)
                    break;
            }
        }
        using (Dev.FrameProbe.Scope("map/models.WarmUp"))
            _models?.StepWarmUp(WarmUpBudget);
        using (Dev.FrameProbe.Scope("map/batch.Upload"))
            ChunkBatch.UploadPending();
    }

    // D142: BYOND's flick(state, atom) (NetField.Flick): the flicked state's pictures play once over the thing's appearance from the
    // tick the server says it started in, then its own picture (or animation) is back. M4v slice 2: a piece drawn with its own
    // appearance (a table, a firelock, a door at rest) plays it on its faces too (the south square, as its picture); a piece drawn with
    // a swatch, a counterpart's picture or a fallback (a grille, a lamp, a window, a windoor's glass) does not.
    private void StartFlick(StaticRecord record)
    {
        EntityState s = record.Entity.State;
        // A firelock's model shows its parts' own states and slides by its state (ReadSlideState): a flick's frames (its deny) are not drawn on its leaves.
        if (SlidesByState(record))
            return;
        StartPartFlick(record); // a mass driver's carriage, an outlet's doors; its pictures play on as well
        if (TryStartFlickModel(record))
            return;
        bool piece = record.Placed.Kind == PlacedKind.Piece, live = record.LiveFrames.Count > 0;
        // A model's live frames play a flick of their own shape (a vending machine's deny, its vend), not one of another.
        if (s.FlickAppearance.IsNone || (live ? !record.LiveSquare || !ModelFrame.Follows(record.LiveState, StateOf(s.FlickAppearance))
                : piece ? !record.PieceOwnPicture || record.Instances.Count == 0 : record.Instances.Count != 1))
            return;
        double age = Math.Max(0, _replica.Tick - s.FlickTick) / (double)MotionMath.TicksPerSecond;
        if (!_atlas.TryAnimation(_tables, s.FlickAppearance, live ? record.LiveDir : piece ? ByondDir.South : FacingOf(record), trim: !piece && !live, once: true,
                out AtlasAnimation flick)
            || flick == null || age >= flick.Timeline.Length)
            return;
        record.Flick = flick;
        record.FlickStart = _clock - age;
        Track(record);
    }

    // A flick on a thing a model draws whose frames have models of their own (ModelEntry.FlickFrame: a mounted flash's burst): those models stand in for it,
    // frame by frame at the flick's timing (StepFlickModel), and its own model comes back when the flick ends.
    private bool TryStartFlickModel(StaticRecord record)
    {
        EntityState s = record.Entity.State;
        if (_models == null || (record.Model == null && record.ModelOverride == null) || s.FlickAppearance.IsNone
            || !_replica.TryGetAppearance(s.FlickAppearance, out AppearanceEntry look))
            return false;
        string icon = _replica.ResolveResource(look.Icon), state = StateOf(s.FlickAppearance);
        if (_models.Catalog.FindFlickFrame(icon, state, 0) == null)
            return false;
        double age = Math.Max(0, _replica.Tick - s.FlickTick) / (double)MotionMath.TicksPerSecond;
        if (!_atlas.TryAnimation(_tables, s.FlickAppearance, ByondDir.South, trim: false, once: true, out AtlasAnimation flick) || flick == null
            || age >= flick.Timeline.Length)
            return true;
        record.Flick = flick;
        record.FlickStart = _clock - age;
        record.FlickModelState = state;
        record.FlickModelIcon = icon;
        StepFlickModel(record);
        Track(record);
        return true;
    }

    private void StepFlickModel(StaticRecord record)
    {
        double t = _clock - record.FlickStart;
        ModelEntry now = null;
        if (record.Flick != null && t < record.Flick.Timeline.Length)
            now = _models.Catalog.FindFlickFrame(record.FlickModelIcon, record.FlickModelState, record.Flick.Timeline.FrameAt(t));
        else
        {
            record.Flick = null;
            record.FlickModelState = null;
        }
        if (now == record.ModelOverride)
            return;
        record.ModelOverride = now;
        Rebuild(record);
    }

    // The picture a thing shows now and its number of direction frames: its flick while one plays, else its animation's picture, else its still picture.
    private int PictureNow(StaticRecord record, out int directions)
    {
        if (record.Flick != null)
        {
            double t = _clock - record.FlickStart;
            if (t < record.Flick.Timeline.Length)
            {
                directions = record.Flick.Directions;
                return record.Flick.Ids[record.Flick.Timeline.FrameAt(t)];
            }
            record.Flick = null;
        }
        if (record.Animation != null)
        {
            directions = record.Animation.Directions;
            return record.Animation.Ids[record.Animation.Timeline.FrameAt(_clock - record.AnimationStart)];
        }
        directions = record.Frames;
        return record.PictureId;
    }

    private void Track(StaticRecord record)
    {
        if (record.Flick != null || record.Animation != null) _animated.Add(record);
        else _animated.Remove(record);
    }

    // Once per frame: every animated or flicking thing whose picture changed gets the new picture id in place (no chunk rebuild).
    private void StepAnimations()
    {
        if (_animated.Count == 0)
            return;
        List<StaticRecord> still = null;
        foreach (StaticRecord record in _animated)
        {
            if (record.FlickModelState != null)
            {
                StepFlickModel(record);
                if (record.FlickModelState == null && record.Animation == null)
                    (still ??= new List<StaticRecord>()).Add(record);
                continue;
            }
            int id = PictureNow(record, out int directions);
            if (record.Flick == null && record.Animation == null)
                (still ??= new List<StaticRecord>()).Add(record);
            if (id == record.ShownId || record.Instances.Count == 0)
                continue;
            if (record.LiveFrames.Count > 0)
            {
                // A model's live frames show the picture; its other pieces keep theirs
                record.ShownId = id;
                foreach (PropInstance part in record.LiveFrames)
                    record.Batch.SetPieceCustom(part, new Color(id % 2048, id / 2048, part.Custom.B, part.Custom.A));
                continue;
            }
            if (record.Placed.Kind == PlacedKind.Piece)
            {
                // M4v slice 2: every part of a piece shows the one picture (its joints mask and its one direction stay)
                record.ShownId = id;
                foreach (PropInstance part in record.Instances)
                {
                    if (Mathf.RoundToInt(part.Color.G * 32f) >= PieceParts.WindowBase)
                        continue; // D147: a window's base keeps its steel swatch; a block's faces keep their own smoothing pictures
                    record.Batch.SetPieceCustom(part, new Color(id % 2048, id / 2048, part.Custom.B, part.Custom.A));
                }
                continue;
            }
            if (record.Instances.Count != 1)
                continue;
            record.ShownId = id;
            PropInstance instance = record.Instances[0];
            record.Batch.SetQuadCustom(instance, new Color(id % 2048, id / 2048, instance.Custom.B, directions));
        }
        if (still != null)
            foreach (StaticRecord record in still)
                _animated.Remove(record);
    }

    /// <summary>The server tick the world is drawn at (the entity layer's render tick); NaN: the replica's latest tick.</summary>
    public double RenderTick { get; set; } = double.NaN;

    /// <summary>The drawn poses of the entities (the session's): a model carried along follows its thing's drawn pose (DV67); null: the replica's glide.</summary>
    public IPoseSource Poses { get; set; }

    // M4v slice 1 (protocol version 9, D143): tg's animate() chains on a static billboard, item or resting machine (AnimationPlayback):
    // pixel_x and pixel_y move the quad on the ground (scaled with an item, as its placement), pixel_z and the transform's vertical move
    // lift it; the rest of the look rides in the instance's basis, which a billboard does not use otherwise (prop_atlas.gdshader: the
    // transform's linear part and pixel_w in the quad's plane, the colour and alpha factors). Decals, floor decals and pieces stay still.
    private void StepLooks()
    {
        if (_looks.Count == 0)
            return;
        double tick = double.IsNaN(RenderTick) ? _replica.Tick : RenderTick;
        List<StaticRecord> done = null;
        foreach (StaticRecord record in _looks)
        {
            if (!StepLook(record, tick))
                (done ??= new List<StaticRecord>()).Add(record);
        }
        if (done != null)
            foreach (StaticRecord record in done)
                _looks.Remove(record);
    }

    // One record's look at the tick; false when it has none now (its quad is back at rest).
    private bool StepLook(StaticRecord record, double tick)
    {
        Placed p = record.Placed;
        if (record.Instances.Count != 1 || p.Kind is not (PlacedKind.Billboard or PlacedKind.Item or PlacedKind.Resting or PlacedKind.FloorDecal or PlacedKind.WallDecal))
            return false;
        PropInstance instance = record.Instances[0];
        if (!ReferenceEquals(instance, record.LookInstance))
        {
            record.LookInstance = instance; // built anew (a new appearance): its transform is the resting one
            record.LookRest = instance.Transform;
        }
        AnimatedLook look = AnimatedLook.None;
        bool playing = record.SeenAnimation != null && _replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry root)
            && AnimationPlayback.TryEvaluate(record.SeenAnimation, tick, MotionMath.TicksPerSecond, AnimatedBase.Of(root), out look)
            && !look.IsNone;
        if (!playing)
        {
            if (instance.Transform != record.LookRest)
                record.Batch.SetQuadTransform(instance, record.LookRest);
            return record.SeenAnimation != null && record.SeenAnimation.EndTick(MotionMath.TicksPerSecond) > tick; // not started yet, or between steps
        }
        float metresPerPixel = TileSpace.TileSize / 32f * (p.Kind == PlacedKind.Item ? WorldScale.ItemScale : 1f);
        Affine t = look.Transform;
        Vector3 move;
        double sideways;
        if (p.Kind == PlacedKind.FloorDecal)
        {
            // M4v slice 4: a thing lying on the floor, seen from above as BYOND draws it: every move is on the ground (east, north), pixel_w and the
            // transform's x move east, pixel_z and its y move north.
            move = new Vector3((float)(look.PixelX + look.PixelW + t.C), 0f, -(float)(look.PixelY + look.PixelZ + t.F)) * metresPerPixel;
            sideways = 0;
        }
        else if (p.Kind == PlacedKind.WallDecal)
        {
            // M4v slice 4: a decal on the wall face (or a fin): pixel_x, pixel_w and the transform's x move it along the wall (the shader, in its
            // plane), pixel_y, pixel_z and its y up.
            move = new Vector3(0f, (float)(look.PixelY + look.PixelZ + t.F) * metresPerPixel, 0f);
            sideways = (look.PixelX + look.PixelW + t.C) * metresPerPixel;
        }
        else
        {
            move = new Vector3((float)look.PixelX * metresPerPixel, (float)(look.PixelZ + t.F) * metresPerPixel, -(float)look.PixelY * metresPerPixel);
            sideways = (t.C + look.PixelW) * metresPerPixel + record.LookRest.Basis.Y.Z; // (DV49: a wide picture's rest is moved across already)
        }
        var origin = record.LookRest.Origin + move;
        float size = record.LookRest.Basis.X.X; // a tuned size (WorldLooks; 1 for none) under animate()'s transform
        var basis = new Basis(
            new Vector3((float)t.A * size, (float)t.D * size, 1f - (float)Math.Min(look.Alpha, 1.0)),
            new Vector3((float)t.B * size, (float)t.E * size, (float)sideways),
            new Vector3(1f - (float)look.Red, 1f - (float)look.Green, (float)look.Blue));
        record.Batch.SetQuadTransform(instance, new Transform3D(basis, origin));
        return true;
    }

    private void Scan()
    {
        List<StaticRecord> moved = null;
        List<StaticRecord> replanned = null;
        foreach (StaticRecord record in _records.Values)
        {
            EntityState s = record.Entity.State;
            // DV371: a thing an elevator carries (an announced ride: the platform's floor, what lies on it) is moved in its batch over the ride, from its level to
            // the next, and kept so across the level change; it is planned anew where it stands once the rise is over (StepCarried). Planned at the new level at
            // once, the platform jumped there (the owner's report, 2026-11-09).
            // DV403: a thing on a tram's trip (its floor, walls and windows, what stands in it) is carried in its batch along the whole way, drawn where
            // the pose source puts it (the tram's even progress), and planned anew where it stands once the trip is over
            TramRide trip = TramRide.None;
            // (until its last step lands: then it stands where it is drawn, and is planned there once instead of carried every tick)
            bool onTram = _replica != null && _replica.TryTramAt(s.X, s.Y, s.Z, _replica.Tick, out trip) && trip.StepsBy(_replica.Tick) < trip.Steps;
            bool riding = !s.Ride.IsNone || _rideCarried.Contains(record) || onTram;
            if (riding && (onTram || record.Placed.Kind != PlacedKind.Piece && Math.Abs(record.Cell.X - s.X) <= 1 && Math.Abs(record.Cell.Y - s.Y) <= 1))
            {
                _rideCarried.Add(record);
                if (onTram)
                    record.Batch.Reach(TripBounds(trip, s.Z));
                if (!_carried.Contains(record) || StaticPropRule.HasMoved(record.Cell, s.X, s.Y, s.Z))
                    Carry(record, s);
                continue;
            }
            // DV416: a thing set in free motion (a throw, a blast, the air's push): a model is carried in its batch along it, a picture handed to the entity layer
            if (!s.Flight.IsNone && record.Placed.Kind != PlacedKind.Piece)
            {
                if (record.Model != null)
                {
                    if (!_carried.Contains(record) || StaticPropRule.HasMoved(record.Cell, s.X, s.Y, s.Z))
                        Carry(record, s);
                }
                else
                    (moved ??= new List<StaticRecord>()).Add(record);
                continue;
            }
            if (StaticPropRule.HasMoved(record.Cell, s.X, s.Y, s.Z))
            {
                if (StaticPropRule.Replans(record.Placed.Kind == PlacedKind.Piece, record.Cell, s.X, s.Y, s.Z))
                {
                    // planned against the walls and floors of the new place: their tiles first (a shuttle's turfs and things arrive together)
                    if (!TurfsAwaited(s.X, s.Y, s.Z))
                        (replanned ??= new List<StaticRecord>()).Add(record);
                }
                else if (record.Model != null && record.Placed.Kind != PlacedKind.Piece)
                    Carry(record, s);
                else if (record.Placed.Kind != PlacedKind.Piece)
                    (moved ??= new List<StaticRecord>()).Add(record);
                continue;
            }
            // Version 26 (D156): a filter's animate() chain recomposes the picture as it plays, which the entity layer does (the atlas
            // keeps one picture): a standing picture, an item or a resting thing whose filter animates is handed to it, as a moved one is.
            if (record.Model == null && record.Placed.Kind is PlacedKind.Billboard or PlacedKind.Item or PlacedKind.Resting && AnimatesFilter(record.Entity.Animation))
            {
                (moved ??= new List<StaticRecord>()).Add(record);
                continue;
            }
            // A wall mount hung on another face of the same cell (the dev mount and rotate tools, DV56).
            if (record.Placed.Kind == PlacedKind.WallDecal && (s.Dir != record.SeenDir || s.Appearance != record.SeenAppearance))
                Remount(record, s);
            // A flat thing's height is its layer's place and its picture is turned by its dir (D122): when the undertile element changes its plane and layer (a tile under it opens),
            // or the atmos init gives a pipe its connections, it is planned again from what the entity says now.
            if (s.Appearance != record.SeenAppearance && PropKinds.Is(record.Entity.TypePath, "/obj/structure/ladder")
                && (StateOf(s.Appearance) == LadderBody.LyingState) != (record.SeenState == LadderBody.LyingState))
            {
                // a ladder linked or unlinked (ladders.dm update_icon_state): its model stands, or tg's picture of it lies flat on the tile
                record.SeenAppearance = s.Appearance;
                record.SeenState = StateOf(s.Appearance);
                record.AnimationStart = _clock;
                Replace(record, rebuildAlways: true);
            }
            else if (record.Placed.Kind == PlacedKind.FloorDecal && (s.Appearance != record.SeenAppearance || s.Dir != record.SeenDir))
            {
                record.SeenDir = s.Dir;
                if (ChunkPlanner.TryPlace(ToPlan(record.Entity), Probe, out Placed again) && again.Kind == PlacedKind.FloorDecal)
                    record.Placed = again;
                record.SeenAppearance = s.Appearance;
                record.SeenState = StateOf(s.Appearance);
                record.AnimationStart = _clock;
                BuildInstances(record);
            }
            else if (s.Appearance != record.SeenAppearance && PropKinds.Is(record.Entity.TypePath, "/obj/structure/grille")
                && PropKinds.IsBrokenGrilleState(StateOf(s.Appearance)) != PropKinds.IsBrokenGrilleState(record.SeenState))
            {
                // M5 slice 5 (D142): a grille broken (tg's brokengrille, grille.dm:331) or repaired with rods (:343) changes what it is drawn as; the
                // windows and grilles beside it join it or let it go.
                record.SeenAppearance = s.Appearance;
                record.SeenState = StateOf(s.Appearance);
                record.AnimationStart = _clock;
                PlanEntity now = ToPlan(record.Entity);
                if (PropKinds.Classify(now.TypePath, now.IconState) == RenderKind.Grille)
                    _plan.Index.Add(now);
                else
                    _plan.Index.Forget(record.Cell, windowLeft: true, grilleLeft: false);
                Replace(record, rebuildAlways: true);
                Rejoin(record.Cell.X, record.Cell.Y, record.Cell.Z);
            }
            else if (s.Appearance != record.SeenAppearance && PropKinds.Is(record.Entity.TypePath, "/obj/structure/falsewall")
                && PropKinds.IsOpenFalseWallState(StateOf(s.Appearance)) != PropKinds.IsOpenFalseWallState(record.SeenState))
            {
                // A false wall opened or shut (false_walls.dm:53-90): a wall block shut, tg's frame sprite open; the blocks beside it show or hide their faces toward it.
                record.SeenAppearance = s.Appearance;
                record.SeenState = StateOf(s.Appearance);
                record.AnimationStart = _clock;
                PlanEntity now = ToPlan(record.Entity);
                if (PropKinds.Classify(now.TypePath, now.IconState) == RenderKind.FalseWall)
                    _plan.Index.Add(now);
                else
                    _plan.Index.Forget(record.Cell, windowLeft: true, grilleLeft: true, girderLeft: false);
                Replace(record, rebuildAlways: true);
                Rejoin(record.Cell.X, record.Cell.Y, record.Cell.Z);
            }
            else if (s.Appearance != record.SeenAppearance && record.Placed.Kind == PlacedKind.Piece && record.Placed.Shape == RenderKind.FullWindow)
            {
                // M5 slice 5: a full-tile window's smoothing junction (its icon state) follows its neighbours' anchoring (icon_smoothing.dm:201): its arms are
                // placed again with its new look (the crack overlays included).
                record.SeenAppearance = s.Appearance;
                record.SeenState = StateOf(s.Appearance);
                record.AnimationStart = _clock;
                Replace(record, rebuildAlways: true);
            }
            else if (s.Appearance != record.SeenAppearance)
            {
                record.SeenAppearance = s.Appearance;
                record.SeenState = StateOf(s.Appearance);
                record.AnimationStart = _clock;
                if (record.IsDoor)
                {
                    // DoorPictures: closed or denying, the appearance's own picture again with its overlays (the panel, welded, the bolt,
                    // emergency and deny lights, sparks) and their frames; opening or closing, the closed position of its states; a door
                    // that came to rest open keeps its panels' picture.
                    if (DoorPictures.Of(record.SeenState, built: true) != DoorPicture.Keep)
                        BuildInstances(record);
                    record.Door.Moving = true;
                }
                else
                    BuildInstances(record);
            }
            // A door turned by its dir (the dev rotate) decides its axis again.
            else if (s.Dir != record.SeenDir && record.Placed.Kind == PlacedKind.Piece && IsDoorShape(record.Placed.Shape))
            {
                record.SeenDir = s.Dir;
                Reface(record);
            }
            // A thing a model draws turned by its dir (a chair) is turned again.
            else if (s.Dir != record.SeenDir && record.Instances.Any(i => i.Mesh >= 0))
            {
                record.SeenDir = s.Dir;
                BuildInstances(record);
            }
            // A lamp turned on or off (its light on the wire; tg's emissive overlay is not replicated, DV12): lit by the shader or not.
            else if (record.Placed.Shape == RenderKind.LightFixture && LightIsOn(record) != record.SeenLit)
                BuildInstances(record);
            if (s.FlickTick != record.SeenFlickTick)
            {
                record.SeenFlickTick = s.FlickTick;
                StartFlick(record);
            }
            if (!ReferenceEquals(s.Animation, record.SeenAnimation))
            {
                record.SeenAnimation = s.Animation;
                _looks.Add(record);
                if (_texts.ContainsKey(record))
                    _textsScrolling.Add(record); // gaps W1: a text that starts to scroll
            }
            if (record.IsDoor)
            {
                bool solid = DoorSolid(record);
                if (solid != record.SeenSolid)
                {
                    record.SeenSolid = solid;
                    record.Door.Moving = true;
                }
            }
        }
        if (replanned != null)
        {
            foreach (StaticRecord record in replanned)
            {
                OnDespawned(record.Entity);
                if (!OnSpawned(record.Entity))
                {
                    HandedOver++;
                    PropMoved?.Invoke(record.Entity);
                }
            }
        }
        if (moved == null)
            return;
        foreach (StaticRecord record in moved)
        {
            bool mount = record.Placed.Kind == PlacedKind.WallDecal;
            RemoveRecord(record);
            // A wall mount hung on another wall stays a decal of the batch (the dev mount tool, DV56); one torn off it is handed over.
            PlanEntity plan = ToPlan(record.Entity);
            if (mount && ChunkPlanner.TryPlace(plan, Probe, out Placed placed) && placed.Kind == PlacedKind.WallDecal)
            {
                (int cx, int cy) = (ChunkPlanner.ChunkIndex(plan.X), ChunkPlanner.ChunkIndex(plan.Y));
                if (!_batches.TryGetValue((cx, cy, plan.Z), out ChunkBatch batch))
                    batch = NewBatch(cx, cy, plan.Z);
                AddRecord(record.Entity, placed, batch);
                _dirty.Add(batch);
                continue;
            }
            HandedOver++;
            PropMoved?.Invoke(record.Entity);
        }
    }

    private bool TurfsAwaited(int x, int y, int z)
    {
        (int cx, int cy) = (ChunkPlanner.ChunkIndex(x), ChunkPlanner.ChunkIndex(y));
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (_turfDirty.Contains((cx + dx, cy + dy, z)))
                    return true;
            }
        }
        return false;
    }

    // The owner's play test (2026-11-03): a window spawner's window has a later id than the camera mapped beside it, so the camera that arrived
    // first found no wall there and stood as a picture turning to the viewer. The same with a wall the first plan did not know (the owner,
    // 2026-11-18, the map editor after Play and Stop: the scene is planned from the map as it was received, and a wall built since comes with
    // the first rebuild of its chunk, after the panels hung on it), and the other way round with a wall taken down, whose panels hung on in
    // the air. What stands beside a cell whose wall or window came or went, and in it, is planned again and drawn where it now belongs: on
    // the wall's face, or as a picture on its own tile.
    private void HangAgainAround(int x, int y, int z)
    {
        foreach ((int dx, int dy) in new[] { (0, 0), (0, 1), (0, -1), (1, 0), (-1, 0) })
        {
            if (!_byCell.TryGetValue((x + dx, y + dy, z), out List<StaticRecord> here))
                continue;
            foreach (StaticRecord record in here.ToList())
            {
                // (a model and a shaped piece stand by themselves; what lies on the floor's layers, the cables and the pipes, hangs on no wall)
                if (record.Model != null || record.Placed.Kind is PlacedKind.Piece or PlacedKind.FloorDecal)
                    continue;
                EntityState s = record.Entity.State;
                if (StaticPropRule.HasMoved(record.Cell, s.X, s.Y, s.Z))
                    continue; // (on its way to another place, a shuttle's cargo: the scan plans it there)
                if (!ChunkPlanner.TryPlace(ToPlan(record.Entity), Probe, out Placed again) || again.Kind == PlacedKind.Piece)
                    continue;
                bool hung = record.Placed.Kind == PlacedKind.WallDecal, hangs = again.Kind == PlacedKind.WallDecal;
                if (!hung && !hangs || hung && hangs && again.Face == record.Placed.Face && again.InWall == record.Placed.InWall)
                    continue;
                record.Placed = again;
                BuildInstances(record);
                _dirty.Add(record.Batch);
            }
        }
    }

    // Plans a wall mount again from what its entity says now and redraws it when its place changed. A model's dir is seen by the turn below.
    private void Remount(StaticRecord record, EntityState s)
    {
        bool turned = record.Model == null && s.Dir != record.SeenDir;
        if (turned)
            record.SeenDir = s.Dir;
        if (!ChunkPlanner.TryPlace(ToPlan(record.Entity), Probe, out Placed again) || again.Kind != PlacedKind.WallDecal)
            return;
        Placed was = record.Placed;
        if (!turned && again.Face == was.Face && again.OffsetX == was.OffsetX && again.Height == was.Height && again.InWall == was.InWall && again.Fin == was.Fin)
            return;
        record.Placed = again;
        BuildInstances(record);
    }

    private static bool AnimatesFilter(EntityAnimation animation)
    {
        if (animation?.Tracks == null)
            return false;
        foreach (AnimationTrack t in animation.Tracks)
            if (t != null && t.Filter != 0) return true;
        return false;
    }

    // The owner's play test (2026-10-16): a locker, a crate or a chair that a 3D model draws turned into its 2D picture when it was pulled,
    // pushed or blown by the air (handed to the entity layer, which draws billboards). It stays the batch's: its model glides as the layer
    // would glide its picture (the replica's tile glide at the render tick), its cell is the one the server says, and once it rests it is
    // placed again on its new tile (its shelf, its facing, its chunk).
    private readonly HashSet<StaticRecord> _carried = new();

    // DV371: the carried records an announced ride moves (Scan); at rest they go back into their level's batch, a picture as much as a model.
    private readonly HashSet<StaticRecord> _rideCarried = new();

    // DV403: the ground a tram's trip covers, from its footprint at the start to the one at the end, a level high
    private static Aabb TripBounds(TramRide trip, int z)
    {
        (int ux, int uy) = trip.Unit;
        int x0 = Math.Min(trip.X0, trip.X0 + ux * trip.Steps), y0 = Math.Min(trip.Y0, trip.Y0 + uy * trip.Steps);
        int x1 = Math.Max(trip.X0, trip.X0 + ux * trip.Steps) + trip.Width, y1 = Math.Max(trip.Y0, trip.Y0 + uy * trip.Steps) + trip.Height;
        Vector3 a = new(TileSpace.WorldX(x0 - 1), TileSpace.WorldY(z), TileSpace.WorldZ(y0 - 1));
        Vector3 b = new(TileSpace.WorldX(x1 + 1), TileSpace.WorldY(z) + TileSpace.LevelHeight, TileSpace.WorldZ(y1 + 1));
        return new Aabb(a, Vector3.Zero).Expand(b);
    }

    private void Carry(StaticRecord record, EntityState s)
    {
        if (_carried.Add(record))
            record.CarryBase = record.Instances.Select(i => i.Transform).ToList();
        if (_byCell.TryGetValue(record.Cell, out List<StaticRecord> old))
            old.Remove(record);
        record.Cell = (s.X, s.Y, s.Z);
        if (!_byCell.TryGetValue(record.Cell, out List<StaticRecord> here))
            _byCell[record.Cell] = here = new List<StaticRecord>();
        here.Add(record);
    }

    private void StepCarried(double renderTick)
    {
        if (_carried.Count == 0)
            return;
        List<StaticRecord> rested = null;
        foreach (StaticRecord record in _carried)
        {
            EntityState s = record.Entity.State;
            float x = s.X, y = s.Y;
            int z = s.Z;
            float rise = 0f;
            if (Poses != null && Poses.TryGetPose(record.Entity, out RenderPose drawn))
            {
                x = drawn.X;
                y = drawn.Y;
                z = drawn.Z;
                rise = drawn.Rise; // DV371: a level change's rise
            }
            else if (!double.IsNaN(renderTick) && record.Entity.TryInterpolate(renderTick, out EntityPose glide))
            {
                x = glide.X;
                y = glide.Y;
                z = glide.Z;
            }
            Placed p = record.Placed;
            record.CarryOffset = new Vector3(TileSpace.WorldX(x) - TileSpace.WorldX(p.X), TileSpace.WorldY(z) - TileSpace.WorldY(p.Z) + rise, TileSpace.WorldZ(y) - TileSpace.WorldZ(p.Y));
            for (int i = 0; i < record.Instances.Count && i < record.CarryBase.Count; i++)
            {
                if (record.Hinged.Any(h => ReferenceEquals(h.Instance, record.Instances[i])) || record.Spinning.Any(h => ReferenceEquals(h.Instance, record.Instances[i])))
                    continue; // a door on its hinge: HingeTransform adds the offset
                Transform3D at = record.CarryBase[i];
                if (record.Model != null || record.Placed.Kind == PlacedKind.Piece) // a piece's instance is in its batch's piece mesh (DV403: a tram's walls)
                    record.Batch.SetModelTransform(record.Instances[i], at with { Origin = at.Origin + record.CarryOffset });
                else
                    record.Batch.SetQuadTransform(record.Instances[i], at with { Origin = at.Origin + record.CarryOffset }); // a picture a ride carries (DV371)
            }
            foreach ((PropInstance instance, Vector3 pivot, Vector3 axis, float degrees, Vector3 move, ModelFlick flick, Vector3 flickMove, Vector3 openScale) in record.Hinged)
                record.Batch.SetModelTransform(instance, HingeTransform(record, pivot, axis, degrees, move, flick, flickMove, openScale));
            foreach ((PropInstance instance, Vector3 pivot, Vector3 axis, float speed) in record.Spinning)
                record.Batch.SetModelTransform(instance, SpinTransform(record, pivot, axis, speed));
            if (x == s.X && y == s.Y && z == s.Z && rise == 0f && s.Ride.IsNone && s.Flight.IsNone)
                (rested ??= new List<StaticRecord>()).Add(record);
        }
        if (rested == null)
            return;
        foreach (StaticRecord record in rested)
        {
            ReplicaEntity entity = record.Entity;
            if (record.Placed.Kind == PlacedKind.Piece)
            {
                // DV403: a tram's wall or window at the trip's end is planned anew there, joined to its neighbours, as a moved piece is (Scan)
                OnDespawned(entity);
                if (!OnSpawned(entity))
                {
                    HandedOver++;
                    PropMoved?.Invoke(entity);
                }
                continue;
            }
            bool rode = _rideCarried.Contains(record); // a picture a ride carried stays in the batch, at its new level
            RemoveRecord(record);
            PlanEntity plan = ToPlan(entity);
            if (ChunkPlanner.TryPlace(plan, Probe, out Placed placed) && placed.Kind != PlacedKind.Piece)
            {
                if (!_batches.TryGetValue((ChunkPlanner.ChunkIndex(plan.X), ChunkPlanner.ChunkIndex(plan.Y), plan.Z), out ChunkBatch batch))
                    batch = NewBatch(ChunkPlanner.ChunkIndex(plan.X), ChunkPlanner.ChunkIndex(plan.Y), plan.Z);
                AddRecord(entity, placed, batch);
                _dirty.Add(batch);
                if (rode || _records[entity.Id].Model != null || _awaitingIcons.Contains(_records[entity.Id])) // a model whose textures still decode
                    continue;
                RemoveRecord(_records[entity.Id]); // no model there: the layer draws its picture, as before
            }
            HandedOver++;
            PropMoved?.Invoke(entity);
        }
    }

    private void PushDoor(StaticRecord door)
    {
        float flash = door.Door.Denied ? 1f : 0f;
        foreach (PropInstance instance in door.Instances)
        {
            Color c = instance.Color;
            door.Batch.SetPieceColor(instance, new Color(c.R, c.G, flash, door.Door.Open));
        }
    }

    // ---- picks ----

    /// <summary>M4a P7: the map's size in tiles and levels (the world pick's frame).</summary>
    public (int Width, int Height, int Levels) Size => (_width, _height, _levels);

    /// <summary>M4a P7: true for a closed turf (a wall); a wall face is a hit of the world pick and stops its ray.</summary>
    public bool IsWallCell(int x, int y, int z) => IsClosedAt(x, y, z);

    /// <summary>M4a P7: true when the cell is an open floor, which has a ceiling over it (space gets none, D116).</summary>
    public bool HasCeilingAt(int x, int y, int z) => _replica?.CeilingAt(x, y, z).Exists ?? (InMap(x, y, z) && _drawnCeiling != null && _drawnCeiling[CellIndex(x, y, z)].Exists);

    /// <summary>D325: true when the cell is open space (nothing drawn) over a level linked under it: the world pick goes on down.</summary>
    public bool OpenBelow(int x, int y, int z)
        => _replica != null && z > 1 && InMap(x, y, z) && _replica.IsLinkedBelow(z) && KindOf(_replica.TurfTypeIndexAt(x, y, z)) == TurfKind.Void;

    /// <summary>True when a 3D model draws the static thing (its way to face is the model's turn; a picture's is one of its sides).</summary>
    public bool DrawnByModel(long entityId) => _records.TryGetValue(entityId, out StaticRecord record) && (record.Model != null || record.ModelOverride != null);

    /// <summary>D325: true when a level stands on level <paramref name="z"/>.</summary>
    public bool LevelAbove(int z) => _replica != null && z >= 1 && z < _levels && _replica.IsLinkedBelow(z + 1);

    // Whether a piece stands open for its pick boxes. A firelock is open by its own state (firedoor.dm: door_open, door_closed while it stands): a shut airlock
    // on its tile makes the cell solid too, and the open firelock's full box would then take the clicks meant for the airlock (a C-4 charge planted on it, the
    // owner's report, 2026-10-18).
    private bool PieceOpen(StaticRecord record)
    {
        Placed p = record.Placed;
        return record.IsDoor ? !record.SeenSolid : p.Shape == RenderKind.Firelock && (record.SeenState is string firelock
            ? ModelRules.SlidesOpen(firelock) : !_replica.IsSolid(p.X, p.Y, p.Z));
    }

    /// <summary>As <see cref="EntityLayer.EffectsClickThrough"/>, for the batched things.</summary>
    public bool EffectsClickThrough { get; set; } = true;

    /// <summary>
    /// M4a P7 (interaction.md 4.1): the pick shapes of the static things in cell (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>):
    /// a prop's picture as a standing quad at its scale on its support (the picture's alpha decides a hit), a wall decal as a thin slab on its face, a
    /// piece as its boxes (<see cref="PieceBoxes"/>; an open door's panels are only at its ends). Floor decals (pipes, wires) are not picked: the floor is.
    /// </summary>
    public void CollectStatic(int x, int y, int z, List<PickShape> into)
    {
        if (_replica == null || !_byCell.TryGetValue((x, y, z), out List<StaticRecord> here))
            return;
        foreach (StaticRecord record in here)
        {
            if (EffectsClickThrough && ClickThrough.IsType(record.Entity?.TypePath))
                continue; // a temp visual that stands still is batched here: as mouse-transparent as in the entity layer
            Placed p = record.Placed;
            // a model-drawn thing being moved is picked where it is drawn (StepCarried)
            double cx = (p.X - 0.5) * TileSpace.TileSize + record.CarryOffset.X, cy = (p.Y - 0.5) * TileSpace.TileSize - record.CarryOffset.Z;
            double wall = WorldScale.WallHeight;
            // Anything a 3D model draws is picked on its model (the owner, 2026-10-20), but the shaped pieces: a door's, a window's and a lamp's boxes know
            // its open state and its glass. Its picture is no guide: a model's PictureId is the whole tg sheet it samples.
            if (p.Kind != PlacedKind.Piece && (record.Model != null || record.ModelOverride != null) && AddModelBoxes(record, wall, into))
                continue;
            switch (p.Kind)
            {
                case PlacedKind.Piece when p.Shape == RenderKind.LightFixture:
                    {
                        PieceBox lamp = PieceBoxes.Lamp(p, record.LampHalfWidth, record.LampHalfHeight);
                        into.Add(PickShape.BoxOf(p.EntityId, PickSurface.Piece, p.X, p.Y, p.Z, cx + lamp.MinEast, cy + lamp.MinNorth, lamp.MinUp, cx + lamp.MaxEast, cy + lamp.MaxNorth, lamp.MaxUp, wall));
                        break;
                    }
                case PlacedKind.Piece:
                    bool open = PieceOpen(record);
                    bool standalone = p.Shape == RenderKind.Grille && !_plan.Index.Windows.Contains((p.X, p.Y, p.Z));
                    foreach (PieceBox box in AcrossGlass(p) ? PieceBoxes.AcrossGlass(p, wall, open) : PieceBoxes.For(p, wall, open, standalone))
                    {
                        PickShape piece = PickShape.BoxOf(p.EntityId, PickSurface.Piece, p.X, p.Y, p.Z, cx + box.MinEast, cy + box.MinNorth, box.MinUp, cx + box.MaxEast, cy + box.MaxNorth, box.MaxUp, wall);
                        // a table's top is picked thicker than drawn: what stands on it (a microwave) keeps the click at its foot (the owner, 2026-10-20)
                        piece.Yields = p.Shape == RenderKind.Table;
                        into.Add(piece);
                    }
                    break;
                case PlacedKind.WallDecal when p.Fin:
                    if (_atlas != null && record.PictureId >= 0)
                    {
                        // A camera's fin: a thin slab from the wall face into the room as deep as its picture is wide.
                        (System.Numerics.Vector3 at, float _) = WallMounts.FinPlacement(p, _atlas.QuadHeightOf(record.PictureId));
                        double height = Math.Max(0.3, _atlas.QuadHeightOf(record.PictureId));
                        double depth = Math.Max(0.3, _atlas.PixelSizeOf(record.PictureId).Width * (TileSpace.TileSize / 32.0));
                        (int fx, int fy) = WallMounts.Step(p.Face);
                        double wallEast = TileSpace.WorldX(p.X) + fx * TileSpace.TileSize / 2.0, wallNorth = -TileSpace.WorldZ(p.Y) + fy * TileSpace.TileSize / 2.0;
                        double up = at.Y - TileSpace.WorldY(p.Z), halfThick = 0.08;
                        bool alongX = p.Face is ByondDirConst.North or ByondDirConst.South;
                        double e0 = alongX ? at.X - halfThick : Math.Min(wallEast, wallEast - fx * depth), e1 = alongX ? at.X + halfThick : Math.Max(wallEast, wallEast - fx * depth);
                        double n0 = alongX ? Math.Min(wallNorth, wallNorth - fy * depth) : -at.Z - halfThick, n1 = alongX ? Math.Max(wallNorth, wallNorth - fy * depth) : -at.Z + halfThick;
                        into.Add(PickShape.BoxOf(p.EntityId, PickSurface.StaticProp, p.X, p.Y, p.Z, e0, n0, up - height / 2, e1, n1, up + height / 2, wall));
                    }
                    break;
                case PlacedKind.WallDecal:
                    if (_atlas != null && record.PictureId >= 0)
                    {
                        (System.Numerics.Vector3 at, float _) = WallMounts.DecalPlacement(DecalPlaced(record), _atlas.QuadHeightOf(record.PictureId));
                        double height = Math.Max(0.3, _atlas.QuadHeightOf(record.PictureId));
                        double east = at.X, north = -at.Z, up = at.Y - TileSpace.WorldY(p.Z);
                        bool alongX = p.Face is ByondDirConst.North or ByondDirConst.South;
                        double halfAlong = 0.5, halfThick = 0.08;
                        into.Add(PickShape.BoxOf(p.EntityId, PickSurface.StaticProp, p.X, p.Y, p.Z,
                            east - (alongX ? halfAlong : halfThick), north - (alongX ? halfThick : halfAlong), up - height / 2,
                            east + (alongX ? halfAlong : halfThick), north + (alongX ? halfThick : halfAlong), up + height / 2, wall));
                    }
                    break;
                case PlacedKind.FloorDecal:
                    // Pipes, cables, vents, scrubbers and the other things lying flat (D122) are picked on their own pixels at their layer's height,
                    // as drawn (their own dir, the picture's up pointing north), so the crosshair finds them over the floor (the owner's play test
                    // 2026-10-14: they could not be aimed at, clicked or deleted).
                    if (_textures != null && _tables != null && _textures.TryGet(_tables, Look(record), p.Dir, 0, out ComposedRaster flat, out _))
                    {
                        SpriteRect rect = SpriteRect.OfFlat(flat.Image.Width, flat.Image.Height, flat.OriginX, flat.OriginY).Times(p.Scale);
                        into.Add(PickShape.FlatPicture(p.EntityId, PickSurface.StaticProp, p.X, p.Y, p.Z, cx + p.OffsetX, cy + p.OffsetY, p.Height + 0.004, rect, flat.Image, flat.OriginX, flat.OriginY));
                    }
                    break;
                default:
                    if (_textures != null && _tables != null && _textures.TryGet(_tables, Look(record), ByondDir.South, 0, out ComposedRaster raster, out _))
                    {
                        Raster image = raster.Image;
                        SpriteRect rect = SpriteRect.Of(image.Width, image.Height, raster.OriginX, raster.OriginY);
                        if (p.Kind is PlacedKind.Item or PlacedKind.Resting)
                        {
                            AtlasBuilder.TrimBounds(image, out _, out int trimY, out _, out int trimHeight);
                            rect = rect.AsItem(p.Kind == PlacedKind.Item ? WorldScale.ItemScale : 1.0, trimHeight > 0 ? image.Height - (trimY + trimHeight) : 0);
                        }
                        // Picked where it is drawn (BuildInstances): an item on a rack's or a closet's shelf there, over the rack (PickShape.OnTop), so it can be
                        // taken (the owner's report, 2026-10-19: a rack's items were picked on the floor under it); a thing on a table at the table model's top.
                        if (p.Kind == PlacedKind.Item && TryShelf(record, out Vector3 shelf))
                        {
                            PickShape onShelf = PickShape.StandingPicture(p.EntityId, PickSurface.StaticProp, p.X, p.Y, p.Z, shelf.X, -shelf.Z, shelf.Y - TileSpace.WorldY(p.Z), rect, image, raster.OriginX, raster.OriginY);
                            onShelf.OnTop = true;
                            onShelf.FacesEye = true;
                            into.Add(onShelf);
                        }
                        else
                        {
                            // DV49: picked where it is drawn, moved across the view (AcrossOf)
                            float across = AcrossOf(record) * p.Scale;
                            WorldLook look = WorldLooks.Active.Find(record.Entity.TypePath);
                            rect = rect.Times(p.Scale);
                            if (across != 0f)
                                rect = rect with { Left = rect.Left + across, Right = rect.Right + across };
                            PickShape standing = PickShape.StandingPicture(p.EntityId, PickSurface.StaticProp, p.X, p.Y, p.Z, cx + (across == 0f ? p.OffsetX : look.X), cy + p.OffsetY,
                                (p.Height > 0f && p.Height == WorldGeometry.TableTop ? TableTopAt(p.X, p.Y, p.Z) : p.Height) + look.Y, rect, image, raster.OriginX, raster.OriginY);
                            standing.FacesEye = p.Kind == PlacedKind.Item; // (prop_atlas.gdshaderinc: an item faces the eye)
                            into.Add(standing);
                        }
                    }
                    break;
            }
        }
    }

    // One pick box per piece a model-drawn thing's instances draw (world boxes of its meshes, where they are drawn: turned, carried, copied), at least a
    // hand's thickness on each axis so that a flat plate can be aimed at. False when it has no model instance yet.
    private bool AddModelBoxes(StaticRecord record, double wall, List<PickShape> into)
    {
        Placed p = record.Placed;
        double floor = TileSpace.WorldY(p.Z);
        bool any = false;
        foreach (PropInstance instance in record.Instances)
        {
            if (instance.Mesh < 0)
                continue;
            var parts = _models.PickBoxesAt(instance.Mesh);
            for (int i = 0; i < (parts?.Count ?? 1); i++)
            {
                Aabb local = parts == null ? ModelMeshAt(instance.Mesh).GetAabb()
                    : new Aabb(new Vector3(parts[i].Min.X, parts[i].Min.Y, parts[i].Min.Z), new Vector3(parts[i].Max.X - parts[i].Min.X, parts[i].Max.Y - parts[i].Min.Y, parts[i].Max.Z - parts[i].Min.Z));
                Aabb box = (instance.Transform * local).Grow(0.04f);
                PickShape shape = PickShape.BoxOf(p.EntityId, PickSurface.StaticProp, p.X, p.Y, p.Z, box.Position.X, -box.End.Z, box.Position.Y - floor,
                    box.End.X, -box.Position.Z, box.End.Y - floor, wall);
                shape.Yields = true;
                into.Add(shape);
            }
            any = true;
        }
        return any;
    }

    /// <summary>
    /// The distance along the unit ray to the first wall it enters (a wall hides what is behind it from a pick), or <paramref name="maxDistance"/>
    /// when there is none within it. The ray passes over walls (above their height) and under floors untouched.
    /// </summary>
    public float RayDistance(Vector3 origin, Vector3 direction, float maxDistance)
    {
        if (_wall == null)
            return maxDistance;
        // Walk the grid cell by cell (Amanatides and Woo): cell (x, y) spans BYOND x - 0.5 .. x + 0.5, so with gx = world x / 2 + 1 the cell is floor(gx).
        double gx = origin.X / TileSpace.TileSize + 1.0, gy = -origin.Z / TileSpace.TileSize + 1.0;
        double dx = direction.X / TileSpace.TileSize, dy = -direction.Z / TileSpace.TileSize;
        int cx = (int)Math.Floor(gx), cy = (int)Math.Floor(gy);
        int stepX = dx > 0 ? 1 : -1, stepY = dy > 0 ? 1 : -1;
        double tNextX = Math.Abs(dx) < 1e-12 ? double.MaxValue : ((dx > 0 ? cx + 1 - gx : gx - cx) / Math.Abs(dx));
        double tNextY = Math.Abs(dy) < 1e-12 ? double.MaxValue : ((dy > 0 ? cy + 1 - gy : gy - cy) / Math.Abs(dy));
        double tDeltaX = Math.Abs(dx) < 1e-12 ? double.MaxValue : 1.0 / Math.Abs(dx);
        double tDeltaY = Math.Abs(dy) < 1e-12 ? double.MaxValue : 1.0 / Math.Abs(dy);
        double t = 0;
        for (int guard = 0; guard < _width + _height + 4 && t <= maxDistance; guard++)
        {
            double y = origin.Y + t * direction.Y;
            int level = (int)Math.Floor(y / TileSpace.LevelHeight) + 1;
            if (cx >= 1 && cy >= 1 && cx <= _width && cy <= _height && level >= 1 && level <= _levels && _wall[((level - 1) * _height + (cy - 1)) * _width + (cx - 1)])
                return (float)Math.Min(t, maxDistance);
            if (tNextX < tNextY)
            {
                t = tNextX;
                tNextX += tDeltaX;
                cx += stepX;
            }
            else
            {
                if (tNextY == double.MaxValue)
                    break;
                t = tNextY;
                tNextY += tDeltaY;
                cy += stepY;
            }
            if (cx < 0 || cy < 0 || cx > _width + 1 || cy > _height + 1)
                break;
        }
        return maxDistance;
    }

}
