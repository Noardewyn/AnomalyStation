using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M3 R5 (D122, the owner after the first walk on the real server and the third walk on the fake feed): what tg draws on its floor layers lies flat under the floor tile's
/// layers (the lattice, the disposal pipes, the wires, the pipes, the vents, the holopad), stacked in tg's order; the fake feed hides what tg's undertile element hides under a
/// floor tile; the tables are lower; the pod doors roll up into the ceiling.
/// </summary>
public sealed class FloorLayerTests : WalkFixBase
{
    public FloorLayerTests(ITestOutputHelper output) : base(output)
    {
    }

    private const float Fp = FloorLayers.FloorPlane, Gp = FloorLayers.GamePlane, Top = FloorLayers.TopdownLayer;

    private static float Rank(float plane, float layer) => FloorLayers.Rank(plane, layer);

    // ---- the rule ----

    // the owner (2026-11-14): a ladder linked to nothing (ladder00, every ladder of a single-level station) is tg's picture lying on its tile; a linked one stands (its model)
    [Fact]
    public void AnUnlinkedLadderLiesFlatAndALinkedOneStands()
    {
        Assert.True(FloorLayers.TryLift(Gp, 2.9f, "/obj/structure/ladder", out float lift, "ladder00"));
        Assert.InRange(lift, FloorLayers.BaseLift, 0.05f);
        Assert.False(FloorLayers.TryLift(Gp, 2.9f, "/obj/structure/ladder", out _, "ladder10"));
        Assert.False(FloorLayers.TryLift(Gp, 2.9f, "/obj/structure/table", out _, "ladder00"));
    }

    [Fact]
    public void TheLayersStackInTgsOrderFromTheTurfsLayersUpToTheDuctsAndThePipeBridges()
    {
        float[] stack =
        {
            Rank(Fp, Top + 4),                      // TURF_DECAL_LAYER (the lowest rank: the turf's own layers)
            Rank(Fp, Top + FloorLayers.LatticeOffset),
            Rank(Fp, Top + FloorLayers.DisposalPipeOffset),
            Rank(Fp, Top + FloorLayers.WireOffset),
            Rank(Fp, Top + FloorLayers.BelowCatwalkOffset), // what the undertile element moves hidden pipes to under a glass floor
            Rank(Fp, Top + FloorLayers.CatwalkOffset),
            Rank(Fp, Top + 15),                     // ABOVE_OPEN_TURF_LAYER: a holopad, an igniter, stairs
            Rank(Fp, Top + 18),                     // CLEANABLE_FLOOR_OBJECT_LAYER
            Rank(Gp, FloorLayers.GasPipeHidden),
            Rank(Gp, FloorLayers.WireBridge),
            Rank(Gp, FloorLayers.WireTerminal),
            Rank(Gp, FloorLayers.GasScrubber),
            Rank(Gp, FloorLayers.GasPipeVisible),
            Rank(Gp, FloorLayers.GasFilter),
            Rank(Gp, FloorLayers.GasPump),
            Rank(Gp, FloorLayers.PlumbingPipeVisible),
            Rank(Gp, FloorLayers.HighPipe),
            Rank(Gp, FloorLayers.TramFloor),         // an elevator's platform (D325)
        };
        for (int i = 1; i < stack.Length; i++)
            Assert.True(stack[i] > stack[i - 1], $"layer {i} must be above layer {i - 1}: {stack[i]} <= {stack[i - 1]}");
        Assert.True(stack.All(r => r >= 0f));
    }

    [Fact]
    public void AnImageOfNoPlaneInTheTopdownBlockLiesFlatLikeTheFloorPlanesAndAGamePlaneLayerStaysStanding()
    {
        // tg's engineering goggles' thermal overlay: image(icon, turf, "greyOverlay", ABOVE_OPEN_TURF_LAYER), no plane set (plane 0)
        Assert.Equal(Rank(Fp, Top + 15), Rank(0f, Top + 15));
        Assert.True(Rank(0f, Top + 15) > Rank(0f, Top + FloorLayers.WireOffset));
        // plane 0 below the block is the game plane as before: an item's layer (3) stands, a pipe's (2.47) lies
        Assert.Equal(-1f, Rank(0f, 3f));
        Assert.True(Rank(0f, FloorLayers.GasPipeVisible) >= 0f);
        // the real server sends such an image's plane as BYOND's FLOAT_PLANE (it takes the turf's): the same as no plane
        Assert.Equal(Rank(Fp, Top + 15), Rank(FloorLayers.FloatPlane, Top + 15));
        Assert.Equal(Rank(Fp, Top + 10.995f), Rank(FloorLayers.FloatPlane, Top + 10.995f)); // a pipe connection arrow on a hidden pipe's layer
        Assert.Equal(-1f, Rank(FloorLayers.FloatPlane, 3f));
    }

    [Fact]
    public void TheVariantsOfOneLayerAreOrderedByTheirOwnOffsets()
    {
        // A cable's three layers (cable.dm: WIRE_LAYER - 0.01, WIRE_LAYER, WIRE_LAYER + 0.01) and the multilayer hub (WIRE_LAYER - 0.02).
        float[] cables = { Rank(Fp, Top + 9.98f), Rank(Fp, Top + 9.99f), Rank(Fp, Top + 10f), Rank(Fp, Top + 10.01f) };
        for (int i = 1; i < cables.Length; i++)
            Assert.True(cables[i] > cables[i - 1], "cable layers: " + string.Join(", ", cables));
        Assert.True(cables[0] > Rank(Fp, Top + 9f) && cables[^1] < Rank(Fp, Top + 11f), "a cable stays between the disposals and the layer above the wires");
        // A pipe's five piping layers (pipes.dm: initial layer + (piping_layer - 3) * 0.001 + its colour * 0.0001): each above the one before, none reaching the next layer.
        float[] pipes = Enumerable.Range(1, 5).Select(p => Rank(Gp, FloorLayers.GasPipeHidden + (p - 3) * 0.001f + 0.0003f)).ToArray();
        for (int i = 1; i < pipes.Length; i++)
            Assert.True(pipes[i] > pipes[i - 1], "piping layers: " + string.Join(", ", pipes));
        Assert.True(pipes[^1] < Rank(Gp, FloorLayers.WireBridge));
        Assert.True(Rank(Gp, FloorLayers.GasPipeVisible + 0.002f) < Rank(Gp, FloorLayers.GasFilter));
        // Two colours of one pipe layer are told apart too.
        Assert.True(Rank(Gp, 2.35f + 0.0004f) > Rank(Gp, 2.35f + 0.0001f));
    }

    [Fact]
    public void EveryLayerOfTheStackLiesAFewMillimetresAboveTheFloorAndNoTwoLayersShareAHeight()
    {
        float[] layers =
        {
            Top + 8, Top + 9, Top + 10, Top + 11, Top + 12, Top + 13, Top + 15, Top + 18,
        };
        float[] game = { 2.35f, 2.44f, 2.45f, 2.46f, 2.47f, 2.48f, 2.49f, 2.495f, 2.54f };
        var lifts = layers.Select(l => FloorLayers.LiftOf(Rank(Fp, l))).Concat(game.Select(l => FloorLayers.LiftOf(Rank(Gp, l)))).ToList();
        Assert.True(lifts.All(l => l >= FloorLayers.BaseLift && l < 0.05f), string.Join(", ", lifts));
        Assert.True(lifts.OrderBy(l => l).Zip(lifts.OrderBy(l => l).Skip(1), (a, b) => b - a).All(d => d >= 0.001f), "heights must differ by at least a millimetre: " + string.Join(", ", lifts.OrderBy(l => l)));
    }

    [Theory]
    [InlineData(-4f, 2.5f)]   // LOW_OBJ_LAYER: standing things from here up
    [InlineData(-4f, 2.55f)]  // CLEANABLE_OBJECT_LAYER
    [InlineData(-4f, 2.8f)]   // TABLE_LAYER
    [InlineData(-4f, 2.9f)]   // BELOW_OBJ_LAYER: machines
    [InlineData(-4f, 3f)]     // OBJ_LAYER: items
    [InlineData(-4f, 3.2f)]
    [InlineData(-4f, 4.25f)]  // WALL_OBJ_LAYER
    [InlineData(-4f, 2.08f)]  // ABOVE_NORMAL_TURF_LAYER
    [InlineData(-4f, 2.3f)]
    [InlineData(-3f, 2.46f)]  // another plane: not the floor, whatever the layer
    [InlineData(25f, 2.46f)]
    [InlineData(-6f, 3f)]     // a floor-plane thing with a layer tg does not give the topdown block
    [InlineData(-6f, 2.4f)]
    [InlineData(-6f, 10019.5f)]
    public void ThingsAbovePipeHeightOrOnOtherPlanesStayUpright(float plane, float layer) => Assert.True(Rank(plane, layer) < 0f, $"plane {plane} layer {layer}");

    [Fact]
    public void ThePlaneOfALevelOfAMultiZStackIsTrued()
    {
        Assert.Equal(-6, FloorLayers.TruePlane(-6));
        Assert.Equal(-6, FloorLayers.TruePlane(-6 - FloorLayers.PlaneRange)); // a level one up
        Assert.Equal(-6, FloorLayers.TruePlane(-6 - 3 * FloorLayers.PlaneRange));
        Assert.Equal(-4, FloorLayers.TruePlane(-4 - 2 * FloorLayers.PlaneRange));
        Assert.Equal(25, FloorLayers.TruePlane(25));
        Assert.True(Rank(-6 - FloorLayers.PlaneRange, Top + 10) >= 0f, "a wire on the floor plane of an upper level lies flat");
        Assert.True(Rank(-4 - FloorLayers.PlaneRange, 2.47f) >= 0f, "a pipe of an upper level lies flat");
    }

    [Fact]
    public void AnUnknownPlaneAndLayerMeansTheGamePlaneAndOnlyTheHatchesAreFlatByTheirType()
    {
        Assert.True(Rank(0f, 0f) < 0f);
        Assert.True(FloorLayers.Rank(0f, 0f, "/obj/machinery/atmospherics/components/unary/vent_pump/on") >= 0f);
        Assert.True(FloorLayers.Rank(0f, 0f, "/obj/machinery/atmospherics/components/unary/thermomachine/freezer") < 0f);
        Assert.True(FloorLayers.Rank(0f, 0f, "/obj/structure/lattice") < 0f, "the lattice is told by its layer, not by its path");
        Assert.True(Rank(0f, 2.47f) >= 0f, "plane 0 is the game plane for a pipe layer");
    }

    // ---- the planner ----

    private static PlanEntity Layered(long id, string type, int x, int y, float plane, float layer, byte dir = 2, short px = 0, short py = 0)
        => new(id, type, x, y, 1, dir, TableRef.Public((uint)id), px, py, LightState.None, null, plane, layer);

    [Fact]
    public void TheThingsOfOneTileLieFlatAndStackInTheirLayersOrderWhateverTheEntityOrder()
    {
        MapGrid grid = Grid(8, 8);
        var entities = new[]
        {
            Layered(1, "/obj/machinery/atmospherics/pipe/smart/manifold4w/general/visible", 4, 4, Gp, 2.47f, 4),
            Layered(2, "/obj/structure/cable", 4, 4, Fp, Top + 10),
            Layered(3, "/obj/structure/lattice", 4, 4, Fp, Top + 8),
            Layered(4, "/obj/structure/disposalpipe/segment", 4, 4, Fp, Top + 9, 1),
            Layered(5, "/obj/machinery/atmospherics/pipe/smart/manifold4w/supply/hidden/layer4", 4, 4, Gp, 2.35f + 0.001f, 4),
            Layered(6, "/obj/machinery/atmospherics/components/unary/vent_pump/on", 4, 4, Gp, 2.46f, 8),
            Layered(7, "/obj/machinery/atmospherics/components/binary/pump", 4, 4, Gp, 2.49f),
            Layered(8, "/obj/structure/table", 4, 4, Gp, 2.8f),
            Layered(9, "/obj/item/paper", 4, 4, Gp, 3f),
        };
        Dictionary<long, Placed> placed = ChunkPlanner.Plan(grid, null, entities).Chunks.SelectMany(c => c.Placed).ToDictionary(p => p.EntityId);
        foreach (long id in new long[] { 1, 2, 3, 4, 5, 6, 7 })
            Assert.Equal((PlacedKind.FloorDecal, RenderKind.Billboard), (placed[id].Kind, placed[id].Shape));
        // lattice < disposal < wire < hidden pipe < vent < visible pipe < pump
        long[] order = { 3, 4, 2, 5, 6, 1, 7 };
        for (int i = 1; i < order.Length; i++)
            Assert.True(placed[order[i]].Height > placed[order[i - 1]].Height, $"entity {order[i]} must lie above {order[i - 1]}");
        Assert.All(order, id => Assert.InRange(placed[id].Height, FloorLayers.BaseLift, 0.05f));
        // the table is a piece and the paper stays upright: an item
        Assert.Equal(PlacedKind.Piece, placed[8].Kind);
        Assert.Equal(PlacedKind.Item, placed[9].Kind);
        // their dirs are kept for the picture to be turned by
        Assert.Equal((byte)4, placed[1].Dir);
        Assert.Equal((byte)1, placed[4].Dir);
    }

    [Fact]
    public void AFlatThingKeepsItsPixelOffsetAsAGroundOffsetAndNoWallOrTableMakesItAMountOrLiftsIt()
    {
        MapGrid grid = Grid(8, 8);
        // A wall north of the cable's tile and a table on it: neither moves a floor thing. Pipes shift by pixels for their piping layer.
        bool Wall(int x, int y, int z) => y == 5;
        Assert.True(ChunkPlanner.TryPlace(Layered(1, "/obj/machinery/atmospherics/pipe/smart/simple/general/visible", 4, 4, Gp, 2.47f, 4, -5, 0), Wall, out Placed pipe));
        Assert.Equal(PlacedKind.FloorDecal, pipe.Kind);
        Assert.Equal(-5 * (2f / 32f), pipe.OffsetX, 5);
        Assert.Equal(0f, pipe.OffsetY, 5);
        Dictionary<long, Placed> placed = ChunkPlanner.Plan(grid, null, new[]
        {
            Layered(1, "/obj/structure/table", 4, 4, Gp, 2.8f),
            Layered(2, "/obj/structure/cable", 4, 4, Fp, Top + 10),
            Layered(3, "/obj/machinery/holopad", 4, 4, Fp, Top + 15),
        }).Chunks.SelectMany(c => c.Placed).ToDictionary(p => p.EntityId);
        Assert.True(placed[2].Height < 0.02f && placed[3].Height < 0.03f, "on a table's tile a floor thing is still on the floor");
        Assert.Equal(PlacedKind.FloorDecal, placed[3].Kind);
    }

    [Fact]
    public void TheHatchOfTheThirdWalkIsTheHolopadTgDrawsOnTheFloorPlane()
    {
        // The owner's view at 79.85,93.55 yaw -109: a round grey-blue grate with red lights standing up like a signpost; it is the holopad at (82,93), on tg's FLOOR_PLANE at
        // ABOVE_OPEN_TURF_LAYER (holopad.dm: layer = ABOVE_OPEN_TURF_LAYER, plane = FLOOR_PLANE). R3 drew it upright (D121: "holopads stay upright"); the owner calls it a hatch.
        Assert.True(ChunkPlanner.TryPlace(Layered(1, "/obj/machinery/holopad", 82, 93, Fp, Top + 15), (x, y, z) => false, out Placed pad));
        Assert.Equal(PlacedKind.FloorDecal, pad.Kind);
        // Without the appearance's plane and layer (a feed that does not send them) it keeps its old resting look.
        Assert.True(ChunkPlanner.TryPlace(new PlanEntity(1, "/obj/machinery/holopad", 82, 93, 1, 2, TableRef.Public(1), 0, 0, LightState.None, null), (x, y, z) => false, out pad));
        Assert.Equal(PlacedKind.Resting, pad.Kind);
    }

    [Fact]
    public void WhatStandsUpStandsUp()
    {
        MapGrid grid = Grid(8, 8);
        var entities = new[]
        {
            Layered(1, "/obj/item/paper", 4, 4, Gp, 3f),
            Layered(2, "/obj/structure/bed", 5, 4, Gp, 2.9f),
            Layered(3, "/obj/machinery/atmospherics/components/unary/thermomachine/freezer", 6, 4, Gp, 3f),
            Layered(4, "/obj/machinery/atmospherics/components/tank/air", 7, 4, Gp, 3.3f),
            Layered(5, "/obj/structure/closet", 4, 5, Gp, 2.9f),
            Layered(6, "/mob/living/basic/mouse", 5, 5, Gp, 4f),
            Layered(7, "/obj/machinery/light/directional/north", 6, 5, Gp, 4.25f, 1),
        };
        Dictionary<long, Placed> placed = ChunkPlanner.Plan(grid, null, entities).Chunks.SelectMany(c => c.Placed).ToDictionary(p => p.EntityId);
        Assert.All(placed.Values, p => Assert.NotEqual(PlacedKind.FloorDecal, p.Kind));
    }

    [Fact]
    public void TheShaderTakesTheHeightFromTheInstanceAndTheMapRendererSendsIt()
    {
        string shader = File.ReadAllText(RepoFile("godot", "shaders", "prop_atlas.gdshaderinc"));
        Assert.Contains("floor_lift + COLOR.g / 16.0", shader);
        Assert.Equal(16f, PieceParts.LiftScale);
        string renderer = File.ReadAllText(RepoFile("godot", "scripts", "MapRenderer.cs"));
        Assert.Contains("p.Height * PieceParts.LiftScale", renderer);
        // The height must survive the half float the instance colour is stored as (3e-5 m at the top of the range).
        float top = FloorLayers.LiftOf(Rank(Gp, FloorLayers.HighPipe));
        Assert.True(top * PieceParts.LiftScale < 1f);
        Assert.Equal(top, (float)(Half)(top * PieceParts.LiftScale) / PieceParts.LiftScale, 4);
    }

    private static string RepoFile(params string[] parts)
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException(string.Join("/", parts));
    }

    // ---- the fake feed's undertile ----

    private const string Plates = @"anomaly-map 1
size 8 3 1
chunk 32
default /turf/open/space/basic | /area/space

[chunk 1 1 1]
1,2: /obj/structure/cable{icon_state = ""x""} | /obj/structure/cable | /obj/structure/disposalpipe/segment | /obj/machinery/atmospherics/pipe/smart/manifold4w/general/hidden | /obj/structure/lattice | /turf/open/floor/iron | /area/station/hallway
2,2: /obj/structure/cable | /obj/structure/disposalpipe/segment | /obj/machinery/atmospherics/pipe/smart/manifold4w/general/hidden | /obj/structure/lattice | /obj/machinery/navbeacon | /turf/open/floor/plating | /area/station/hallway
3,2: /obj/structure/cable | /obj/structure/disposalpipe/segment | /obj/machinery/atmospherics/pipe/smart/manifold4w/general/hidden | /obj/structure/lattice | /turf/open/space/basic | /area/space
4,2: /obj/structure/cable | /obj/structure/disposalpipe/segment | /obj/machinery/atmospherics/pipe/smart/manifold4w/general/hidden | /obj/structure/lattice | /turf/open/floor/catwalk_floor | /area/station/hallway
5,2: /obj/structure/cable | /obj/structure/disposalpipe/segment | /obj/machinery/atmospherics/pipe/smart/manifold4w/general/hidden | /obj/structure/lattice | /turf/open/floor/glass | /area/station/hallway
6,2: /obj/structure/cable | /obj/structure/disposalpipe/segment | /obj/machinery/atmospherics/pipe/smart/manifold4w/general/hidden | /obj/machinery/navbeacon | /turf/closed/wall | /area/station/hallway
7,2: /obj/structure/cable | /obj/structure/cable | /obj/machinery/atmospherics/components/unary/vent_pump/on | /obj/machinery/atmospherics/components/binary/pump | /turf/open/floor/iron/dark | /area/station/hallway
";

    private static string[] TypesAt(FakeMap map, int x) => map.Entities.Where(e => e.X == x).Select(e => e.TypePath).OrderBy(t => t, StringComparer.Ordinal).ToArray();

    [Fact]
    public void UnderAnIntactFloorTileTheFakeFeedShowsNoPipeCableOrDisposalButPlatingSpaceCatwalkAndGlassShowThem()
    {
        FakeMap map = FakeMap.Parse(Plates, "plates");
        string[] iron = TypesAt(map, 1);
        Assert.Equal(new[] { "/obj/structure/lattice" }, iron); // the lattice has no undertile element: it shows anywhere
        foreach (int x in new[] { 2, 3, 4, 5 })
        {
            string[] types = TypesAt(map, x);
            Assert.Contains("/obj/structure/cable", types);
            Assert.Contains("/obj/structure/disposalpipe/segment", types);
            Assert.Contains("/obj/machinery/atmospherics/pipe/smart/manifold4w/general/hidden", types);
            Assert.Contains("/obj/structure/lattice", types);
        }
        Assert.Contains("/obj/machinery/navbeacon", TypesAt(map, 2));
        Assert.Empty(TypesAt(map, 6)); // inside a wall (a turf with the default accessibility) everything is hidden
        // The machines of the atmospherics room stand on a floor tile, in view: only the pipes and wires hide.
        string[] dark = TypesAt(map, 7);
        Assert.Equal(new[] { "/obj/machinery/atmospherics/components/binary/pump", "/obj/machinery/atmospherics/components/unary/vent_pump/on" }, dark);
    }

    [Fact]
    public void UnderAGlassFloorOrACatwalkTheThingsMoveToTheFloorPlaneAtTheBelowCatwalkLayerLikeTgsUndertileDoes()
    {
        FakeMap map = FakeMap.Parse(Plates, "plates");
        foreach (int x in new[] { 4, 5 })
        {
            // The pipe is on the game plane: it moves. The cable and the disposal pipe are on the floor plane already: "we only want to change the layer/plane for things that
            // aren't already on the floor plane" (undertile.dm), so they keep their layer.
            FakeMapEntity moved = map.Entities.First(e => e.X == x && e.TypePath.StartsWith("/obj/machinery/atmospherics/pipe", StringComparison.Ordinal));
            Assert.Equal(FloorLayers.FloorPlane, moved.Plane);
            Assert.InRange(moved.Layer, FloorLayers.TopdownLayer + FloorLayers.BelowCatwalkOffset - 0.003f, FloorLayers.TopdownLayer + FloorLayers.BelowCatwalkOffset + 0.006f);
            Assert.Equal(10010f, map.Entities.First(e => e.X == x && e.TypePath == "/obj/structure/cable").Layer);
            Assert.Equal(10009f, map.Entities.First(e => e.X == x && e.TypePath.StartsWith("/obj/structure/disposalpipe", StringComparison.Ordinal)).Layer);
        }
        // On plating they keep their own plane and layer (a cable is on the floor plane anyway, a pipe on the game plane).
        FakeMapEntity cable = map.Entities.First(e => e.X == 2 && e.TypePath == "/obj/structure/cable");
        FakeMapEntity pipe = map.Entities.First(e => e.X == 2 && e.TypePath.StartsWith("/obj/machinery/atmospherics/pipe", StringComparison.Ordinal));
        Assert.Equal((-6f, 10010f), (cable.Plane, cable.Layer));
        Assert.Equal(-4f, pipe.Plane);
        Assert.InRange(pipe.Layer, 2.35f, 2.354f); // the class's layer and the colour's place (0.0001 each)
        // And the lattice (never undertile) keeps its own over a catwalk.
        Assert.Equal(10008f, map.Entities.First(e => e.X == 4 && e.TypePath == "/obj/structure/lattice").Layer);
    }

    [Fact]
    public void EverythingTheFakeFeedShowsOfTheFloorLayersIsFlatAndTheEntitiesCarryTheirPlaneThroughTheTables()
    {
        FakeMap map = FakeMap.Parse(Plates, "plates");
        FakeMapBinding binding = map.Bind(new AppearanceTables());
        var tables = new AppearanceTables();
        binding = map.Bind(tables);
        for (int i = 0; i < map.Entities.Count; i++)
        {
            FakeMapEntity e = map.Entities[i];
            AppearanceEntry entry = tables.Appearances[(int)binding.EntityAppearances[i].Index - 1];
            Assert.Equal((e.Layer, (short)e.Plane), (entry.Layer, entry.Plane));
        }
        var entities = map.Entities.Select((e, i) => new PlanEntity(e.Id, e.TypePath, e.X, e.Y, e.Z, e.Dir, binding.EntityAppearances[i], e.PixelX, e.PixelY, e.Light, e.IconState, e.Plane, e.Layer)).ToList();
        Dictionary<long, Placed> placed = ChunkPlanner.Plan(binding.Grid, null, entities).Chunks.SelectMany(c => c.Placed).ToDictionary(p => p.EntityId);
        foreach (FakeMapEntity e in map.Entities.Where(e => e.TypePath != "/obj/machinery/navbeacon"))
        {
            Assert.True(placed.TryGetValue(e.Id, out Placed p), e.TypePath);
            Assert.Equal(PlacedKind.FloorDecal, p.Kind);
        }
    }

    [Fact]
    public void ACableLinksToTheCablesOfItsLayerBesideItAndShowsThatPictureInTheFakeFeed()
    {
        const string row = @"anomaly-map 1
size 5 3 1
chunk 32
default /turf/open/space/basic | /area/space

[chunk 1 1 1]
1,2: /obj/structure/cable | /turf/open/floor/plating | /area/station/hallway
2,2: /obj/structure/cable | /turf/open/floor/plating | /area/station/hallway
3,2: /obj/structure/cable | /obj/structure/cable/layer1 | /turf/open/floor/plating | /area/station/hallway
3,3: /obj/structure/cable | /turf/open/floor/plating | /area/station/hallway
5,2: /obj/structure/cable | /turf/open/floor/plating | /area/station/hallway
";
        FakeMap map = FakeMap.Parse(row, "row");
        string StateAt(int x, int y, string type) => map.Entities.First(e => e.X == x && e.Y == y && e.TypePath == type).IconState;
        Assert.Equal("l2-4", StateAt(1, 2, "/obj/structure/cable"));
        Assert.Equal("l2-4-8", StateAt(2, 2, "/obj/structure/cable"));
        Assert.Equal("l2-1-8", StateAt(3, 2, "/obj/structure/cable")); // the cable above and the one to the west
        Assert.Equal("l2-2", StateAt(3, 3, "/obj/structure/cable"));
        Assert.Equal("l2-noconnection", StateAt(5, 2, "/obj/structure/cable"));
        Assert.Equal("l1-noconnection", StateAt(3, 2, "/obj/structure/cable/layer1")); // layer 1 never links to layer 2
        // The picture states exist in tg's layer_cable.dmi for every state a cable can take; the check of the real icons is in the picture tests.
    }

    [Fact]
    public void OnMetaOnlyPlatingSpaceCatwalkAndGlassShowTheUndertileThingsAndAFlooredTileShowsNone()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        MapGrid grid = world.Binding.Grid;
        ushort[] cells = grid.ExpandCells();
        ITypeSource types = FakeMap.DefaultTypes();
        var access = new Dictionary<string, int>();
        foreach (string turf in grid.TurfTypes)
            access[turf] = types.TryGet(turf, out TypeDefaults d) ? d.UnderfloorAccessibility : 0;
        int shown = 0, hiddenTypesSeen = 0;
        var byType = new Dictionary<string, int>();
        foreach (FakeMapEntity e in world.Map.Entities.Where(e => PropKinds.HasUndertile(e.TypePath)))
        {
            string turf = grid.TurfTypes[cells[(e.X - 1) + (e.Y - 1) * grid.Width]];
            if (!PropKinds.TileAccess.Shows(access[turf]))
                hiddenTypesSeen++;
            shown++;
            string family = e.TypePath.StartsWith("/obj/structure/cable", StringComparison.Ordinal) ? "cable" : e.TypePath.StartsWith("/obj/structure/disposalpipe", StringComparison.Ordinal) ? "disposal" : e.TypePath.StartsWith("/obj/machinery/atmospherics/pipe", StringComparison.Ordinal) ? "pipe" : "other";
            byType[family] = byType.GetValueOrDefault(family) + 1;
        }
        _output.WriteLine($"{shown} undertile things shown: " + string.Join(", ", byType.Select(kv => kv.Key + " " + kv.Value)));
        Assert.Equal(0, hiddenTypesSeen);
        Assert.True(byType["cable"] > 500 && byType["pipe"] > 500 && byType["disposal"] > 100, "plating and space show the infrastructure of the maintenance tunnels");
        // The medbay's maintenance hall (76,93) is plating: its cable shows; the hall tiles of medbay (iron) show none.
        Assert.Contains(world.Map.Entities, e => e.X == 76 && e.Y == 93 && e.TypePath == "/obj/structure/cable");
        int cablesOnIron = world.Map.Entities.Count(e => e.TypePath.StartsWith("/obj/structure/cable", StringComparison.Ordinal) && grid.TurfTypes[cells[(e.X - 1) + (e.Y - 1) * grid.Width]].StartsWith("/turf/open/floor/iron", StringComparison.Ordinal));
        Assert.Equal(0, cablesOnIron);
    }

    [Fact]
    public void OnMetaEveryFloorPlaneOrPipeLayerThingLiesFlatAndNothingElseDoesAndTheRankIsTheLayersOwn()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        var flat = new Dictionary<string, int>();
        foreach (FakeMapEntity e in world.Map.Entities)
        {
            Assert.True(world.Placed.TryGetValue(e.Id, out Placed p), e.TypePath);
            bool shouldBeFlat = FloorLayers.Rank(e.Plane, e.Layer, e.TypePath) >= 0f;
            // A thing that is a piece (a door, a table) is never flat: tg's tables are on the game plane at TABLE_LAYER anyway, and the stairs are the planner's pieces.
            Assert.Equal(shouldBeFlat && p.Kind != PlacedKind.Piece, p.Kind == PlacedKind.FloorDecal);
            if (p.Kind == PlacedKind.FloorDecal)
            {
                string key = e.TypePath.Split('/').Take(4).Aggregate((a, b) => a + "/" + b);
                flat[key] = flat.GetValueOrDefault(key) + 1;
            }
        }
        _output.WriteLine(string.Join(", ", flat.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value)));
        Assert.True(flat.Values.Sum() > 4000, "Meta has thousands of lattices, cables, pipes and disposals on plating and in space: " + flat.Values.Sum());
        // The lattice and the cables are the most of them.
        Assert.True(flat["/obj/structure/lattice"] > 1500 && flat["/obj/structure/cable"] > 800);
    }

    [Fact]
    public void OnMetaNoTwoFlatThingsOfDifferentLayersOnATileShareAHeight()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        var tiles = new Dictionary<(int, int), List<(float Lift, float Layer, string Type)>>();
        foreach (FakeMapEntity e in world.Map.Entities)
        {
            if (world.Placed[e.Id].Kind != PlacedKind.FloorDecal)
                continue;
            if (!tiles.TryGetValue((e.X, e.Y), out List<(float, float, string)> list))
                tiles[(e.X, e.Y)] = list = new List<(float, float, string)>();
            list.Add((world.Placed[e.Id].Height, e.Layer, e.TypePath));
        }
        int stacked = 0;
        foreach (List<(float Lift, float Layer, string Type)> list in tiles.Values.Where(l => l.Count > 1))
        {
            stacked++;
            // Things of exactly one layer (two disposal pipes of a trunk and a segment, tg gives both the same) share a height in tg too; different layers must not.
            var ordered = list.GroupBy(i => i.Layer).Select(g => g.First()).OrderBy(i => i.Lift).ToList();
            for (int i = 1; i < ordered.Count; i++)
                Assert.True(ordered[i].Lift - ordered[i - 1].Lift > 0.00005f, $"{ordered[i - 1].Type}@{ordered[i - 1].Layer} and {ordered[i].Type}@{ordered[i].Layer} are {ordered[i].Lift - ordered[i - 1].Lift} m apart");
        }
        _output.WriteLine($"{stacked} tiles hold more than one flat thing");
        Assert.True(stacked > 300);
    }

    // ---- the tables ----

    [Fact]
    public void ATablesTopIs0Point75MetresWhateverTheEye()
    {
        Assert.Equal(0.75f, WorldScale.TableTop, 4); // D157: it stays where D137's eye of 1.5 m put it
        Assert.Equal(WorldScale.TableTopHeight, WorldGeometry.TableTop);
        Assert.Equal(WorldGeometry.TableTop - WorldGeometry.TableTopThickness, WorldGeometry.TableTopUnder, 5);
        try
        {
            Assert.True(WorldScale.SetEyeHeight(1.6f));
            Assert.Equal(0.75f, WorldScale.TableTop, 4);
            Assert.Equal(0.75f, WorldGeometry.TableTop, 4);
            Assert.Equal(0.75f, WorldGeometry.Pieces().Where(q => q.Part == PieceParts.Table).Max(q => q.Corners.Max(c => c.Y)), 4);
        }
        finally
        {
            WorldScale.ResetEyeHeight();
        }
        Assert.Equal(0.75f, WorldGeometry.Pieces().Where(q => q.Part == PieceParts.Table).Max(q => q.Corners.Max(c => c.Y)), 4);
    }

    [Fact]
    public void AnItemOnATableRestsOnTheLoweredTopAndTheTopIsBelowTheEyeAndTheWaist()
    {
        MapGrid grid = Grid(8, 8);
        Dictionary<long, Placed> placed = ChunkPlanner.Plan(grid, null, new[]
        {
            Layered(1, "/obj/structure/table/reinforced", 4, 4, Gp, 2.8f),
            Layered(2, "/obj/item/pen", 4, 4, Gp, 3f),
            Layered(3, "/obj/machinery/microwave", 4, 4, Gp, 2.9f),
        }).Chunks.SelectMany(c => c.Placed).ToDictionary(p => p.EntityId);
        Assert.Equal(0.75f, placed[2].Height, 4);
        Assert.Equal(0.75f, placed[3].Height, 4);
        Assert.True(WorldGeometry.TableTop < WorldScale.EyeHeight - 0.5f, "the top is well under the eye: a standing eye looks down on it");
    }

    // ---- the pod doors ----

    [Fact]
    public void PodDoorsAndShuttersRollUpIntoTheCeilingAndAreCutThere()
    {
        string shader = File.ReadAllText(RepoFile("godot", "shaders", "prop_atlas.gdshaderinc"));
        string[] lines = shader.Split('\n');
        int part9 = Array.FindIndex(lines, l => l.Contains("want == 9.0", StringComparison.Ordinal));
        Assert.True(part9 > 0);
        string block = string.Join("\n", lines.Skip(part9).Take(7));
        Assert.Contains("VERTEX.y += open * (wall_height", block); // up, not down
        Assert.DoesNotContain("VERTEX.y -= open * (wall_height", shader);
        Assert.Contains("v_clip = MODEL_MATRIX[3].y + wall_height", block); // cut at the ceiling of its own level
        Assert.Contains("v_world.y > v_clip", shader);
    }

    // ---- the navigation computer's landing zone (DV382) ----

    [Fact]
    public void TheNavigationComputersLandingZoneLiesFlatOverTheFloorsOwnThings()
    {
        // tg's ABOVE_GAME_PLANE (-3): the zone's tiles and icons at ABOVE_NORMAL_TURF_LAYER, the designated spot at NAVIGATION_EYE_LAYER above them
        Assert.True(FloorLayers.TryLift(FloorLayers.AboveGamePlane, FloorLayers.AboveNormalTurf, null, out float tiles));
        Assert.True(FloorLayers.TryLift(FloorLayers.AboveGamePlane, FloorLayers.NavigationEye, null, out float spot));
        Assert.True(spot > tiles);
        Assert.True(FloorLayers.TryLift(FloorLayers.GamePlane, FloorLayers.TramFloor, null, out float tram));
        Assert.True(tiles > tram, "over everything else on the floor");
        // another thing of that plane still stands
        Assert.False(FloorLayers.TryLift(FloorLayers.AboveGamePlane, 4.0f, null, out _));
    }

    // ---- the greenscreen camera's tile mark (DV383) ----

    [Fact]
    public void TheGreenscreensMarkLiesOnItsTile()
    {
        // tg's location indicator: GAME_PLANE, BELOW_OPEN_DOOR_LAYER (2.6); a plain thing of that layer still stands
        const string mark = "/obj/effect/abstract/greenscreen_location_indicator";
        Assert.True(ChunkPlanner.TryPlace(new PlanEntity(1, mark, 6, 6, 1, ByondDirConst.South, TableRef.Public(1), 0, 0, LightState.None, "blank_semi_transparent", FloorLayers.GamePlane, 2.6f), (x, y, z) => false, out Placed placed));
        Assert.Equal(PlacedKind.FloorDecal, placed.Kind);
        Assert.True(FloorLayers.TryLift(FloorLayers.GamePlane, FloorLayers.TramFloor, null, out float tram));
        Assert.True(placed.Height > tram, "over everything else on the floor");
        Assert.False(FloorLayers.TryLift(FloorLayers.GamePlane, 2.6f, "/obj/effect/abstract", out _));
    }

    // ---- the cleanables tg moves to the game plane (the owner, 2026-11-20: the space ants stood up as a billboard) ----

    [Fact]
    public void TheGamePlaneCleanablesLieOnTheFloorAndTheCobwebStands()
    {
        Assert.True(FloorLayers.TryLift(Gp, FloorLayers.LowObj, "/obj/effect/decal/cleanable/ants", out float ants));
        Assert.True(FloorLayers.TryLift(Gp, FloorLayers.CleanableObject, "/obj/effect/decal/cleanable/ash", out float ash));
        Assert.True(FloorLayers.TryLift(Gp, FloorLayers.Gib, "/obj/effect/decal/cleanable/blood/gibs", out float gibs));
        Assert.True(FloorLayers.TryLift(Gp, FloorLayers.TramFloor, null, out float tram));
        Assert.True(tram < ants && ants < ash && ash < gibs, "over a tram's platform, in tg's layer order");
        Assert.True(FloorLayers.TryLift(FloorLayers.AboveGamePlane, FloorLayers.AboveNormalTurf, null, out float zone));
        Assert.True(gibs < zone);
        Assert.False(FloorLayers.TryLift(Gp, 3.2f, "/obj/effect/decal/cleanable/cobweb", out _)); // WALL_OBJ_LAYER
        Assert.False(FloorLayers.TryLift(Gp, 3.3f, "/obj/effect/decal/cleanable/blood/hitsplatter", out _)); // ABOVE_WINDOW_LAYER
        Assert.False(FloorLayers.TryLift(Gp, FloorLayers.LowObj, "/obj/structure/bed", out _), "a plain thing of that layer still stands");
    }
}
