using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M3 R2: where things go: wall decals on the face of their wall, nothing that stands on the floor on a wall, items on table tops, firelocks as
/// pieces, doors at rest by their own picture.
/// </summary>
public sealed class WalkFixPlannerTests : WalkFixBase
{
    public WalkFixPlannerTests(ITestOutputHelper output) : base(output)
    {
    }

    // ---- Meta ----

    [Fact]
    public void EveryDirectionalMountPointingAtAWallIsAWallDecalOrAFixture()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        var source = FakeMap.DefaultTypes();
        int mounts = 0, onWall = 0;
        var misses = new List<string>();
        foreach (FakeMapEntity e in world.Map.Entities)
        {
            if (!e.TypePath.Contains("/directional/", StringComparison.Ordinal) || PropKinds.IsNeverDecal(e.TypePath))
                continue;
            // tg's offset of the type (from its class, not the map's overrides) points at a face: a mount, and the neighbour that way is a wall or a full-tile window.
            Assert.True(source.TryGet(e.TypePath, out TypeDefaults tg));
            int ax = Math.Abs((int)tg.PixelX), ay = Math.Abs((int)tg.PixelY);
            byte face = 0;
            if (Math.Max(ax, ay) >= WallMounts.MinOffsetPixels)
                face = ax >= ay ? (tg.PixelX > 0 ? (byte)4 : (byte)8) : (tg.PixelY > 0 ? (byte)1 : (byte)2);
            else if (PropKinds.IsWallMountType(e.TypePath) && WallMounts.IsCardinal(e.Dir))
                face = e.Dir;
            if (face == 0)
                continue;
            (int dx, int dy) = WallMounts.Step(face);
            if (!world.IsWallOrWindow(e.X + dx, e.Y + dy))
                continue;
            mounts++;
            if (world.Placed.TryGetValue(e.Id, out Placed placed) && (placed.Kind == PlacedKind.WallDecal || placed.Shape == RenderKind.LightFixture))
            {
                onWall++;
                continue;
            }
            misses.Add($"{e.TypePath} at ({e.X},{e.Y}) face {face}: {(world.Placed.TryGetValue(e.Id, out Placed p) ? p.Kind + " " + p.Shape : "not placed")}");
        }
        _output.WriteLine($"{mounts} mounts beside a wall or window, {onWall} on it");
        Assert.True(mounts > 1500, "Meta has thousands of wall mounts: " + mounts);
        Assert.True(misses.Count == 0, string.Join("\n", misses.Take(15)));
    }

    [Fact]
    public void NoSeatTableOrSinkIsAWallDecalAndNothingFliesInTheAir()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        string[] floorFamilies = { "/obj/structure/chair", "/obj/structure/table", "/obj/structure/sink", "/obj/machinery/shower", "/obj/structure/bed" };
        int checkedCount = 0;
        foreach (FakeMapEntity e in world.Map.Entities)
        {
            if (!floorFamilies.Any(f => PropKinds.Is(e.TypePath, f)))
                continue;
            checkedCount++;
            if (!world.Placed.TryGetValue(e.Id, out Placed p))
                continue;
            Assert.NotEqual(PlacedKind.WallDecal, p.Kind);
            // Nothing that stands on the floor is lifted (a stool was lifted 2 m by its picture's baked offset) or sent more than a tile away.
            Assert.Equal(0f, p.Height);
            Assert.InRange(Math.Abs(p.OffsetX), 0f, ChunkPlanner.MaxGroundOffset);
            Assert.InRange(Math.Abs(p.OffsetY), 0f, ChunkPlanner.MaxGroundOffset);
        }
        Assert.True(checkedCount > 300, "Meta has hundreds of seats, tables and sinks: " + checkedCount);
        // And the stools in particular, whatever their path says.
        Assert.Contains(world.Map.Entities, e => e.TypePath.StartsWith("/obj/structure/chair/stool/directional/", StringComparison.Ordinal));
        foreach (FakeMapEntity stool in world.Map.Entities.Where(e => e.TypePath.StartsWith("/obj/structure/chair/stool", StringComparison.Ordinal)))
            Assert.True(Math.Abs(stool.PixelX) < WallMounts.MinOffsetPixels && Math.Abs(stool.PixelY) < WallMounts.MinOffsetPixels, stool.TypePath);
    }

    [Fact]
    public void NoDoorOfMetaIsABillboard()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        int doors = 0;
        foreach (FakeMapEntity e in world.Map.Entities.Where(e => PropKinds.Is(e.TypePath, "/obj/machinery/door")))
        {
            doors++;
            Assert.NotEqual(RenderKind.Billboard, PropKinds.Classify(e.TypePath));
            Assert.True(world.Placed.TryGetValue(e.Id, out Placed p), e.TypePath);
            Assert.Equal(PlacedKind.Piece, p.Kind);
        }
        Assert.True(doors > 1000, "Meta has over a thousand doors: " + doors);
        Assert.Contains(world.Map.Entities, e => PropKinds.Classify(e.TypePath) == RenderKind.Firelock);
    }

    [Fact]
    public void NoDoorTypeOfTgIsABillboard()
    {
        var kinds = new Dictionary<string, RenderKind>();
        foreach (string path in AllClassPaths().Where(p => PropKinds.Is(p, "/obj/machinery/door")))
            kinds[path] = PropKinds.Classify(path);
        Assert.True(kinds.Count > 100, "tg has over a hundred door types: " + kinds.Count);
        Assert.DoesNotContain(kinds, k => k.Value == RenderKind.Billboard);
        Assert.Equal(RenderKind.Firelock, kinds["/obj/machinery/door/firedoor"]);
        Assert.Equal(RenderKind.FirelockBorder, kinds["/obj/machinery/door/firedoor/border_only"]);
    }

    [Fact]
    public void ThingsOnATableOnMetaStandOnItsTop()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        var tableCells = new HashSet<(int, int)>(world.Map.Entities.Where(e => PropKinds.IsTable(e.TypePath)).Select(e => (e.X, e.Y)));
        int lifted = 0;
        foreach (FakeMapEntity e in world.Map.Entities.Where(e => PropKinds.Is(e.TypePath, "/obj/item") && tableCells.Contains((e.X, e.Y))))
        {
            if (!world.Placed.TryGetValue(e.Id, out Placed p) || p.Kind != PlacedKind.Item)
                continue;
            lifted++;
            Assert.Equal(WorldGeometry.TableTop, p.Height);
        }
        Assert.True(lifted > 50, "Meta has dozens of items on tables: " + lifted);
        // The desk of the medbay foyer (94,107): its things are on the table.
        Assert.Contains(world.Map.Entities, e => e.TypePath == "/obj/structure/desk_bell" && e.X == 94 && e.Y == 107 && world.Placed[e.Id].Height == WorldGeometry.TableTop);
    }

    [Fact]
    public void EveryDecalOfMetaSitsOnTheFaceOfItsWallInsideItsTile()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        int decals = 0, shifted = 0;
        foreach (Placed p in world.Placed.Values.Where(p => p.Kind == PlacedKind.WallDecal))
        {
            decals++;
            (System.Numerics.Vector3 origin, _) = WallMounts.DecalPlacement(p, 0.6f);
            (int dx, int dy) = WallMounts.Step(p.Face);
            // The face between the entity's cell and its neighbour that way: 1 m from the cell's centre (the mount's wall is the neighbour; furniture in a
            // wall cell is on its own cell's face toward the open side, which is the same plane); the decal floats 1.2 cm in front of it.
            float faceX = TileSpace.WorldX(p.X) + dx * 1f, faceZ = TileSpace.WorldZ(p.Y) - dy * 1f;
            if (dx != 0)
                Assert.InRange(Math.Abs(origin.X - faceX), 0.011f, 0.013f);
            else
                Assert.InRange(Math.Abs(origin.Z - faceZ), 0.011f, 0.013f);
            // Along the wall it stays inside its own tile but for the few the map itself shifts by half a tile or more (a fire alarm of (118,105) was drawn on the next tile's shutter).
            float along = dx != 0 ? origin.Z - TileSpace.WorldZ(p.Y) : origin.X - TileSpace.WorldX(p.X);
            Assert.InRange(Math.Abs(along), 0f, 2.5f);
            if (Math.Abs(along) > 1.0f)
                shifted++;
            Assert.InRange(origin.Y, 0.1f, WorldScale.WallHeight);
        }
        Assert.True(decals > 1500, "Meta has thousands of wall decals: " + decals);
        // The map's own corner signs (tg's northeast and southwest helpers, 32 pixels along as well as toward the wall) and a few shifted buttons lie farther along their wall: a few percent.
        Assert.True(shifted * 30 < decals, $"{shifted} of {decals} decals lie more than a tile along their wall");
        // The signs that stand in a wall cell (they were billboards sticking out of it) are decals of the open side: some on Meta.
        Assert.True(world.Placed.Values.Count(p => p.InWall) > 30, "Meta has dozens of signs mapped on the wall turf");
    }

    // ---- the decal's transform ----

    [Theory]
    [InlineData(ByondDirConst.North, 0, 1)]
    [InlineData(ByondDirConst.South, 0, -1)]
    [InlineData(ByondDirConst.East, 1, 0)]
    [InlineData(ByondDirConst.West, -1, 0)]
    public void ADecalsOriginLiesOnTheFaceOfTheNeighbouringWallCell(byte face, int dx, int dy)
    {
        // Tile (10, 20) with its wall one cell away toward the face: the wall cell's face toward the tile is 1 m from either centre, and the decal floats 1.2 cm in front of it.
        var placed = new Placed(1, PlacedKind.WallDecal, RenderKind.Billboard, 10, 20, 1, face, face, 1.1f, 0f, 0f, TableRef.Public(1), LightState.None, null);
        (System.Numerics.Vector3 origin, float yaw) = WallMounts.DecalPlacement(placed, 0.5f);
        float faceX = TileSpace.WorldX(10 + dx) - dx * 1f, faceZ = TileSpace.WorldZ(20 + dy) + dy * 1f;
        Assert.Equal(faceX - dx * WallMounts.DecalLift, origin.X, 4);
        Assert.Equal(faceZ + dy * WallMounts.DecalLift, origin.Z, 4);
        Assert.Equal(1.1f, origin.Y, 4);
        // The picture's front (+Z turned by the yaw about the vertical axis) faces back into the room, away from the wall.
        Assert.Equal(-dx, MathF.Sin(yaw), 4);
        Assert.Equal(dy, MathF.Cos(yaw), 4);
    }

    [Theory]
    [InlineData(ByondDirConst.South, 0, -1)]
    [InlineData(ByondDirConst.North, 0, 1)]
    [InlineData(ByondDirConst.East, 1, 0)]
    [InlineData(ByondDirConst.West, -1, 0)]
    public void ASignInTheWallCellIsOnItsOwnCellsFaceTowardTheOpenSide(byte face, int dx, int dy)
    {
        // The sign's entity is in the wall cell (10, 20); the open side is the way Face points. The picture is on that face of the wall cell and looks out of it.
        var placed = new Placed(1, PlacedKind.WallDecal, RenderKind.Billboard, 10, 20, 1, 2, face, 1.5f, 0f, 0f, TableRef.Public(1), LightState.None, null, InWall: true);
        (System.Numerics.Vector3 origin, float yaw) = WallMounts.DecalPlacement(placed, 0.5f);
        Assert.Equal(TileSpace.WorldX(10) + dx * (1f + WallMounts.DecalLift), origin.X, 4);
        Assert.Equal(TileSpace.WorldZ(20) - dy * (1f + WallMounts.DecalLift), origin.Z, 4);
        Assert.Equal(dx, MathF.Sin(yaw), 4);
        Assert.Equal(-dy, MathF.Cos(yaw), 4);
    }

    [Fact]
    public void WallFurnitureInAWallCellFacesTheFloorNotTheNextWall()
    {
        // (6, 6) is a wall with a wall to its west and east and floor north and south; (6, 7) and (6, 5) are floor.
        bool IsWall(int x, int y, int z) => (x == 6 && y == 6) || x == 5 || x == 7;
        bool IsOpen(int x, int y, int z) => x == 6 && y != 6;
        Assert.True(ChunkPlanner.TryPlace(Entity(1, "/obj/structure/sign/departments/medbay/alt", 6, 6, 2), IsWall, IsWall, null, IsOpen, out Placed south));
        Assert.Equal((PlacedKind.WallDecal, true, ByondDirConst.South), (south.Kind, south.InWall, south.Face));
        Assert.True(ChunkPlanner.TryPlace(Entity(2, "/obj/structure/noticeboard", 6, 6, 1), IsWall, IsWall, null, IsOpen, out Placed north));
        Assert.Equal(ByondDirConst.North, north.Face); // its dir, when that side is open
        Assert.True(ChunkPlanner.TryPlace(Entity(3, "/obj/structure/sign/directions/evac", 6, 6, 4), IsWall, IsWall, null, IsOpen, out Placed fallback));
        Assert.Equal(ByondDirConst.South, fallback.Face); // east is a wall: the first open side
        // A sign on a floor cell with no wall where its dir points is a picture there; with one it is a mount on it.
        Assert.True(ChunkPlanner.TryPlace(Entity(4, "/obj/structure/sign/departments/medbay/alt", 6, 8, 2), IsWall, IsWall, null, IsOpen, out Placed onFloor));
        Assert.Equal(PlacedKind.Billboard, onFloor.Kind);
        Assert.True(ChunkPlanner.TryPlace(Entity(6, "/obj/structure/sign/departments/medbay/alt", 6, 7, 2), IsWall, IsWall, null, IsOpen, out Placed beside));
        Assert.Equal((PlacedKind.WallDecal, false, ByondDirConst.South), (beside.Kind, beside.InWall, beside.Face));
        // A sign on the hull with only space beside it faces the space.
        bool Hull(int x, int y, int z) => x == 6 && y == 6;
        Assert.True(ChunkPlanner.TryPlace(Entity(5, "/obj/structure/sign/warning/biohazard", 6, 6, 2), Hull, Hull, null, (x, y, z) => false, out Placed outside));
        Assert.Equal((PlacedKind.WallDecal, ByondDirConst.South), (outside.Kind, outside.Face));
    }

    [Fact]
    public void ADecalMovesAlongItsWallByTheOffsetAndNotAcrossIt()
    {
        // The fire alarm of the lead's y+-90 shots: east face, pixel_y -4 (a quarter metre south). Moved along z, not x.
        var placed = new Placed(1, PlacedKind.WallDecal, RenderKind.Billboard, 118, 105, 1, 4, 4, 1.1f, -4 * (TileSpace.TileSize / 32f), 0f, TableRef.Public(1), LightState.None, null);
        (System.Numerics.Vector3 origin, _) = WallMounts.DecalPlacement(placed, 0.5f);
        Assert.Equal(TileSpace.WorldX(118) + 1f - WallMounts.DecalLift, origin.X, 4);
        // OffsetX is the offset along the wall: east face, it runs north (-z), so a negative one goes south (+z).
        Assert.Equal(TileSpace.WorldZ(105) + 0.25f, origin.Z, 4);
    }

    // ---- the planner ----

    private static bool Wall(int x, int y, int z) => x < 5 || x > 7 || y < 5 || y > 7;

    [Fact]
    public void AStoolBesideAWallIsNeverADecalWhateverItsPathOrOffsetSays()
    {
        // The wall is north of (6, 7): the old rule made any /directional/ type with a cardinal dir a mount, and a stool of 32 pixels was lifted 2 m.
        foreach (short py in new short[] { 0, 32 })
        {
            Assert.True(ChunkPlanner.TryPlace(Entity(1, "/obj/structure/chair/stool/directional/north", 6, 7, 1, 0, py), Wall, out Placed p));
            Assert.Equal(PlacedKind.Billboard, p.Kind);
        }
        foreach (string path in new[] { "/obj/structure/sink/directional/north", "/obj/machinery/shower/directional/north", "/obj/structure/chair/office/directional/north" })
        {
            Assert.True(ChunkPlanner.TryPlace(Entity(1, path, 6, 7, 1, 0, 28), Wall, out Placed p), path);
            Assert.Equal(PlacedKind.Billboard, p.Kind);
        }
        // A fire alarm with the same offset is a mount.
        Assert.True(ChunkPlanner.TryPlace(Entity(1, "/obj/machinery/firealarm/directional/north", 6, 7, 1, 0, 26), Wall, out Placed alarm));
        Assert.Equal(PlacedKind.WallDecal, alarm.Kind);
        // A camera and a light construct have no offset and use their dir.
        Assert.True(ChunkPlanner.TryPlace(Entity(1, "/obj/machinery/camera/directional/north", 6, 7, 1), Wall, out Placed camera));
        Assert.Equal(PlacedKind.WallDecal, camera.Kind);
        Assert.True(ChunkPlanner.TryPlace(Entity(1, "/obj/structure/light_construct/directional/north", 6, 7, 1), Wall, out Placed construct));
        Assert.Equal(PlacedKind.WallDecal, construct.Kind);
        // A camera facing north that is shifted 16 pixels east sits along the north wall (the east neighbour is floor): the dir's face is the wall.
        Assert.True(ChunkPlanner.TryPlace(Entity(1, "/obj/machinery/camera", 6, 7, 1, 16, 0), Wall, out Placed along));
        Assert.Equal((PlacedKind.WallDecal, ByondDirConst.North, 1f), (along.Kind, along.Face, along.OffsetX));
    }

    [Fact]
    public void ABillboardsPixelOffsetIsItsGroundOffsetAndNothingElse()
    {
        // 8 pixels east and 4 south: half a metre east, a quarter metre south; kept inside the tile. An item is drawn at the item scale (D121, half the tile scale),
        // its offset is tg's own (D329: it lies where a hand put it)
        Assert.True(ChunkPlanner.TryPlace(Entity(1, "/obj/item/pen", 6, 6, 2, 8, -4), Wall, out Placed pen));
        Assert.Equal((PlacedKind.Item, 0.5f, -0.25f, 0f), (pen.Kind, pen.OffsetX, pen.OffsetY, pen.Height));
        Assert.True(ChunkPlanner.TryPlace(Entity(3, "/obj/machinery/microwave", 6, 6, 2, 8, -4), Wall, out Placed machine));
        Assert.Equal((PlacedKind.Billboard, 0.5f, -0.25f), (machine.Kind, machine.OffsetX, machine.OffsetY)); // a machine keeps the tile scale
        Assert.True(ChunkPlanner.TryPlace(Entity(2, "/obj/machinery/shower", 6, 6, 2, 40, -40), Wall, out Placed far));
        Assert.Equal((ChunkPlanner.MaxGroundOffset, -ChunkPlanner.MaxGroundOffset), (far.OffsetX, far.OffsetY));
    }

    [Fact]
    public void AnItemAndASmallMachineOnATableStandOnItsTop()
    {
        MapGrid grid = Grid(8, 8);
        var entities = new[]
        {
            Entity(1, "/obj/structure/table", 4, 4),
            Entity(2, "/obj/item/pen", 4, 4, 2, 3, 2),
            Entity(3, "/obj/machinery/microwave", 4, 4),
            Entity(4, "/obj/item/pen", 5, 4),
            Entity(5, "/obj/structure/chair/office", 4, 4),
            Entity(6, "/obj/structure/desk_bell", 4, 4),
        };
        Dictionary<long, Placed> placed = ChunkPlanner.Plan(grid, null, entities).Chunks.SelectMany(c => c.Placed).ToDictionary(p => p.EntityId);
        Assert.Equal(WorldGeometry.TableTop, placed[2].Height);
        Assert.Equal(WorldGeometry.TableTop, placed[3].Height);
        Assert.Equal(WorldGeometry.TableTop, placed[6].Height);
        Assert.Equal(0f, placed[4].Height); // the same pen on the floor
        Assert.Equal(0f, placed[5].Height); // a structure does not climb a table
        Assert.Equal(PlacedKind.Item, placed[2].Kind);
        Assert.Equal(0.1875f, placed[2].OffsetX, 4); // its own small offset is kept (D329: not scaled with the item)
    }

    [Fact]
    public void AFirelockIsAThinPieceInTheDoorPlaneAndABorderFirelockAnEdgePiece()
    {
        // A doorway between walls east and west: the plane runs along x (NORTH); between walls north and south it runs along y (EAST).
        bool EastWestWalls(int x, int y, int z) => (x == 4 || x == 6) && y == 5;
        Assert.True(ChunkPlanner.TryPlace(Entity(1, "/obj/machinery/door/firedoor", 5, 5), EastWestWalls, out Placed doorway));
        Assert.Equal((PlacedKind.Piece, RenderKind.Firelock, ByondDirConst.North), (doorway.Kind, doorway.Shape, doorway.Face));
        bool NorthSouthWalls(int x, int y, int z) => x == 5 && (y == 4 || y == 6);
        Assert.True(ChunkPlanner.TryPlace(Entity(2, "/obj/machinery/door/firedoor/heavy", 5, 5), NorthSouthWalls, out Placed crossing));
        Assert.Equal((RenderKind.Firelock, ByondDirConst.East), (crossing.Shape, crossing.Face));
        // The border-only firelock stands on the edge its dir faces.
        foreach (byte dir in new byte[] { 1, 2, 4, 8 })
        {
            Assert.True(ChunkPlanner.TryPlace(Entity(3, "/obj/machinery/door/firedoor/border_only", 5, 5, dir), (x, y, z) => false, out Placed edge));
            Assert.Equal((PlacedKind.Piece, RenderKind.FirelockBorder, dir), (edge.Kind, edge.Shape, edge.Face));
        }
        Assert.Equal(new[] { PieceParts.FirelockSouth, PieceParts.FirelockNorth }, PieceParts.PartsOf(RenderKind.Firelock));
        Assert.Equal(new[] { PieceParts.FirelockEdge }, PieceParts.PartsOf(RenderKind.FirelockBorder));
        Assert.False(PieceParts.IsDoor(RenderKind.Firelock)); // D117 keeps firelocks open: no animation
    }

    // ---- the firelock's geometry ----

    private static List<GeoQuad> Part(int part) => WorldGeometry.Pieces().Where(q => q.Part == part).ToList();

    [Fact]
    public void AFirelocksSlabsShowTheSpriteOnceFromBothSidesAndClearTheDoorsInTheSameTile()
    {
        float wall = WorldScale.WallHeight;
        foreach (int part in new[] { PieceParts.FirelockSouth, PieceParts.FirelockNorth })
        {
            List<GeoQuad> quads = Part(part);
            Assert.NotEmpty(quads);
            // Each slab is one sprite over x -1..1 and y 0..wall on its +Z and -Z faces (the frame reads from both sides).
            foreach (GeoQuad q in quads.Where(q => MathF.Abs(q.Normal.Z) > 0.5f))
            {
                Assert.Equal(0f, q.Uvs.Min(u => u.X), 4);
                Assert.Equal(1f, q.Uvs.Max(u => u.X), 4);
                Assert.Equal(0f, q.Uvs.Min(u => u.Y), 4);
                Assert.Equal(1f, q.Uvs.Max(u => u.Y), 4);
                Assert.Equal(wall, q.Corners.Max(c => c.Y), 4);
            }
            // Outside an airlock's panels (z +-0.1) and a pod door's slab (+-0.12): no z-fighting.
            Assert.True(quads.All(q => q.Corners.All(c => MathF.Abs(c.Z) >= 0.12f - 1e-4f)), "firelock part " + part);
        }
        Assert.True(Part(PieceParts.FirelockSouth).All(q => q.Corners.All(c => c.Z > 0)));
        Assert.True(Part(PieceParts.FirelockNorth).All(q => q.Corners.All(c => c.Z < 0)));
        // The edge slab sits just inside the tile edge, off the windoor's pane (edge +-0.04).
        Assert.True(Part(PieceParts.FirelockEdge).All(q => q.Corners.All(c => c.Z > WorldGeometry.Edge + 0.04f && c.Z < 0)));
        Assert.True(PieceParts.Count <= 32, "a part number must fit the shader's colour channel (part / 32)");
    }

    // ---- doors rest by their own picture ----

    [Theory]
    [InlineData("open", false)]
    [InlineData("leftopen", false)]
    [InlineData("closed", true)]
    [InlineData("opening", null)]
    [InlineData("closing", null)]
    [InlineData("deny", null)]
    [InlineData("left", null)]
    [InlineData(null, null)]
    public void ADoorAtRestSaysItselfWhetherItBlocks(string state, bool? solid) => Assert.Equal(solid, DoorAnimation.OwnSolid(state));

    [Theory]
    [InlineData("leftopening", DoorPhase.Opening)]
    [InlineData("rightsecureclosing", DoorPhase.Closing)]
    [InlineData("leftdeny", DoorPhase.Deny)]
    [InlineData("left", DoorPhase.Steady)]
    [InlineData("leftsecureopen", DoorPhase.Steady)]
    public void AWindoorsStatesAreItsBaseStateAndTheDoorWords(string state, DoorPhase phase) => Assert.Equal(phase, DoorAnimation.PhaseOf(state));

    [Theory]
    [InlineData("left", true)]
    [InlineData("rightsecure", true)]
    [InlineData("leftopen", false)]
    [InlineData("leftopening", true)]
    [InlineData("leftclosing", false)]
    [InlineData("leftdeny", true)]
    [InlineData(null, true)]
    public void AWindoorIsShutByItsStateNotByItsCellWhichIsNeverSolid(string state, bool shut) => Assert.Equal(shut, DoorAnimation.WindoorShut(state));

    [Fact]
    public void AnOpenShutterBehindAWindowStartsOpenAlthoughItsCellIsSolid()
    {
        // The R&D shutters of the owner's shots: /preopen (open) in the cell of a full-tile window, so the cell is solid. Its own picture decides.
        bool cellSolid = true;
        DoorAnimState behindWindow = DoorAnimation.Initial(DoorAnimation.OwnSolid("open") ?? cellSolid);
        Assert.Equal(1f, behindWindow.Open);
        DoorAnimation.Step(ref behindWindow, "open", DoorAnimation.OwnSolid("open") ?? cellSolid, 1f);
        Assert.Equal(1f, behindWindow.Open);
        Assert.False(behindWindow.Moving);
        // A closed shutter in the same cell stays shut; an animating one follows the cell.
        Assert.Equal(0f, DoorAnimation.Initial(DoorAnimation.OwnSolid("closed") ?? false).Open);
        Assert.Equal(0f, DoorAnimation.Initial(DoorAnimation.OwnSolid("opening") ?? cellSolid).Open);
    }
}
