using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client.Render;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M3 R3 (D121, the owner's second walk: "items on tables are too high", "the hatches lie on the floor"): an item (<c>/obj/item</c>) is drawn at half the tile scale with
/// its picture's bottom on its support; the air vents and scrubbers lie flat on the floor; a random poster is a poster, not the placeholder checker.
/// </summary>
public sealed class WalkFix2ItemTests : WalkFixBase
{
    public WalkFix2ItemTests(ITestOutputHelper output) : base(output)
    {
    }

    private const string Scrubber = "/obj/machinery/atmospherics/components/unary/vent_scrubber/on/layer2";

    // ---- the item scale, one client setting ----

    [Fact]
    public void TheItemScaleIsHalfByDefaultAndOneSettingLikeTheWallAndEyeHeights()
    {
        Assert.Equal(0.5f, WorldScale.DefaultItemScale);
        Assert.Equal(0.5f, WorldScale.ItemScale);
        try
        {
            Assert.True(WorldScale.SetItemScale(0.4f));
            Assert.Equal(0.4f, WorldScale.ItemScale);
            Assert.False(WorldScale.SetItemScale(0.05f));
            Assert.False(WorldScale.SetItemScale(1.5f));
            Assert.False(WorldScale.SetItemScale(float.NaN));
            Assert.Equal(0.4f, WorldScale.ItemScale);
        }
        finally
        {
            WorldScale.ResetItemScale();
        }
        Assert.Equal(0.5f, WorldScale.ItemScale);
        Assert.True(WorldScale.TryParseItemScale("0.75", out float parsed));
        Assert.Equal(0.75f, parsed);
        Assert.False(WorldScale.TryParseItemScale("big", out _));
    }

    [Fact]
    public void TheItemScaleComesFromTheArgumentOrTheOptionsFile()
    {
        ClientArgs args = ClientArgs.Parse(new[] { "--fake-server", "--item-scale=0.4" });
        Assert.Empty(args.Errors);
        Assert.Equal(0.4f, args.ItemScale);
        Assert.Equal(0.6f, ClientArgs.Parse(new[] { "--item-scale", "0.6" }).ItemScale);
        Assert.Null(ClientArgs.Parse(new[] { "--fake-server" }).ItemScale);
        Assert.Null(ClientArgs.Parse(new[] { "--item-scale=3" }).ItemScale);
        Assert.NotEmpty(ClientArgs.Parse(new[] { "--item-scale=3" }).Errors);
        var options = new PlayOptions();
        Assert.Equal(0.5f, options.ItemScale);
        Assert.Contains("item_scale=0.5", options.ToText());
        var errors = new List<string>();
        PlayOptions read = PlayOptions.Parse("item_scale=0.3\n", errors);
        Assert.Empty(errors);
        Assert.Equal(0.3f, read.ItemScale);
        PlayOptions.Parse("item_scale=9\n", errors);
        Assert.Single(errors);
        Assert.Equal(0.3f, PlayOptions.Parse(read.ToText(), null).ItemScale);
    }

    // ---- an item rests on its support ----

    private static PlanEntity Thing(long id, string type, int x, int y, short px = 0, short py = 0) => new(id, type, x, y, 1, 2, TableRef.Public((uint)id), px, py, LightState.None, null);

    [Fact]
    public void AnItemIsPlacedOnTheFloorOrTheTableTopAtItsOwnOffset()
    {
        MapGrid grid = Grid(8, 8);
        var entities = new[]
        {
            Thing(1, "/obj/structure/table", 4, 4),
            Thing(2, "/obj/item/pen", 4, 4, 8, -4),    // on the table
            Thing(3, "/obj/item/pen", 5, 4, 8, -4),    // on the floor
            Thing(4, "/obj/machinery/microwave", 4, 4), // a machine on the table: resting at the tile scale
            Thing(5, "/obj/machinery/holopad", 6, 6),   // flat floor equipment
            Thing(6, "/obj/structure/closet", 6, 5),    // a structure stands where tg puts it
            Thing(7, "/mob/living/basic/mouse", 6, 4),  // a mob is the entity layer's, but a planned one is a billboard
            Thing(8, "/obj/structure/showcase/machinery/tv", 4, 4), // the newsfeed TV on its desk, not through it
            Thing(9, "/obj/machinery/hydroponics/soil/rich", 6, 3), // a soil plot's picture on the floor, not mid-air
        };
        Dictionary<long, Placed> placed = ChunkPlanner.Plan(grid, null, entities).Chunks.SelectMany(c => c.Placed).ToDictionary(p => p.EntityId);
        Assert.Equal((PlacedKind.Resting, WorldGeometry.TableTop), (placed[8].Kind, placed[8].Height));
        Assert.Equal((PlacedKind.Resting, 0f), (placed[9].Kind, placed[9].Height));
        Assert.Equal((PlacedKind.Item, WorldGeometry.TableTop), (placed[2].Kind, placed[2].Height));
        Assert.Equal((PlacedKind.Item, 0f), (placed[3].Kind, placed[3].Height));
        // 8 pixels east and 4 south at 2 m per 32 pixels is 0.5 m and 0.25 m, whatever the item scale (D329)
        Assert.Equal((0.5f, -0.25f), (placed[2].OffsetX, placed[2].OffsetY));
        Assert.Equal((0.5f, -0.25f), (placed[3].OffsetX, placed[3].OffsetY));
        Assert.Equal((PlacedKind.Resting, WorldGeometry.TableTop), (placed[4].Kind, placed[4].Height));
        Assert.Equal((PlacedKind.Resting, 0f), (placed[5].Kind, placed[5].Height));
        Assert.Equal((PlacedKind.Billboard, 0f), (placed[6].Kind, placed[6].Height));
        Assert.Equal(PlacedKind.Billboard, placed[7].Kind);
    }

    [Fact]
    public void TheItemScaleDoesNotMoveAnItemsOffset()
    {
        try
        {
            WorldScale.SetItemScale(1f);
            Assert.True(ChunkPlanner.TryPlace(Thing(1, "/obj/item/pen", 6, 6, 8, -4), (x, y, z) => false, out Placed pen));
            Assert.Equal((0.5f, -0.25f), (pen.OffsetX, pen.OffsetY));
            WorldScale.SetItemScale(0.25f);
            Assert.True(ChunkPlanner.TryPlace(Thing(1, "/obj/item/pen", 6, 6, 8, -4), (x, y, z) => false, out pen));
            Assert.Equal((0.5f, -0.25f), (pen.OffsetX, pen.OffsetY)); // D329: where tg put it on the tile
        }
        finally
        {
            WorldScale.ResetItemScale();
        }
    }

    [Theory]
    [InlineData("/obj/item/pen", true)]
    [InlineData("/obj/item/storage/toolbox/emergency", true)]
    [InlineData("/obj/item", true)]
    [InlineData("/obj/items/fake", false)]
    [InlineData("/obj/machinery/microwave", false)]
    [InlineData("/obj/structure/closet", false)]
    [InlineData("/mob/living/carbon/human", false)]
    public void OnlyAnObjItemIsAnItem(string path, bool item) => Assert.Equal(item, PropKinds.IsItem(path));

    [Fact]
    public void AWallMountedItemIsStillADecalAndNotAnItem()
    {
        // An intercom is an /obj/item/radio and a wall mount: the decal rule comes first.
        bool Wall(int x, int y, int z) => y == 7;
        Assert.True(ChunkPlanner.TryPlace(Thing(1, "/obj/item/radio/intercom/directional/north", 6, 6, 0, 27), Wall, out Placed intercom));
        Assert.Equal(PlacedKind.WallDecal, intercom.Kind);
    }

    [Fact]
    public void AnItemsPictureIsDrawnAtTheItemScaleAboutItsBottomCentreWithItsOffsetInTheFrameDropped()
    {
        // A pen drawn mid-frame: 6 clear rows under its pixels (0.375 m at tile scale) in a 32 pixel square.
        SpriteRect frame = SpriteRect.Of(32, 32, 0, 0);
        SpriteRect item = frame.AsItem(0.5, 6);
        Assert.Equal(frame.Left * 0.5, item.Left, 6);
        Assert.Equal(frame.Right * 0.5, item.Right, 6);
        // The drawn pixels' bottom is on the floor: the rectangle runs from -6 clear rows (scaled) to the picture's top (scaled): the pixel rows themselves start at y = 0.
        Assert.Equal(-6 * SpriteRect.MetresPerPixel * 0.5, item.Bottom, 6);
        Assert.Equal((32 - 6) * SpriteRect.MetresPerPixel * 0.5, item.Top, 6);
        // The pixel size follows the scale (the ray pick reads pixels by it).
        Assert.Equal(SpriteRect.MetresPerPixel * 0.5, item.PixelSize, 9);
        // No clear rows under it: the rectangle is the frame's, scaled.
        SpriteRect flush = frame.AsItem(0.5, 0);
        Assert.Equal((0.0, 32 * SpriteRect.MetresPerPixel * 0.5), (flush.Bottom, flush.Top));
        // A picture that reaches below its square (an overlay) keeps that part under the floor line only by its clear rows: the bottom row of drawn pixels is still at 0.
        SpriteRect below = SpriteRect.Of(32, 40, 0, 8).AsItem(0.5, 0);
        Assert.Equal(0.0, below.Bottom + 0.0, 9);
    }

    // ---- the floor hatches ----

    [Theory]
    [InlineData("/obj/machinery/atmospherics/components/unary/vent_pump", true)]
    [InlineData("/obj/machinery/atmospherics/components/unary/vent_pump/on/layer4", true)]
    [InlineData("/obj/machinery/atmospherics/components/unary/vent_pump/high_volume/siphon/monitored/air_output", true)]
    [InlineData("/obj/machinery/atmospherics/components/unary/vent_scrubber", true)]
    [InlineData("/obj/machinery/atmospherics/components/unary/vent_scrubber/on/layer2", true)]
    [InlineData("/obj/machinery/atmospherics/components/unary/passive_vent/layer2", true)]
    [InlineData("/obj/machinery/atmospherics/components/unary/outlet_injector/monitored/oxygen_input", true)]
    [InlineData("/obj/machinery/atmospherics/components/binary/dp_vent_pump", true)]
    [InlineData("/obj/structure/water_source/puddle", true)]
    [InlineData("/obj/structure/steam_vent", true)]
    [InlineData("/obj/machinery/atmospherics/components/unary/thermomachine/freezer", false)]
    [InlineData("/obj/machinery/atmospherics/components/unary/portables_connector", false)]
    [InlineData("/obj/machinery/atmospherics/components/binary/pump", false)]
    [InlineData("/obj/machinery/atmospherics/pipe/smart/manifold4w", false)]
    [InlineData("/obj/machinery/portable_atmospherics/scrubber", false)]
    public void TheFloorMountedAtmosphericsEndsAreHatches(string path, bool hatch) => Assert.Equal(hatch, PropKinds.IsFloorHatch(path));

    [Fact]
    public void AVentIsAFloorDecalWhateverWallIsBesideItAndKeepsItsDir()
    {
        // A wall north and east of the vent: it is no wall mount and no standing picture.
        bool Wall(int x, int y, int z) => y == 7 || x == 7;
        foreach (byte dir in new byte[] { 1, 2, 4, 8 })
        {
            Assert.True(ChunkPlanner.TryPlace(new PlanEntity(1, Scrubber, 6, 6, 1, dir, TableRef.Public(1), 0, 0, LightState.None, null), Wall, out Placed vent));
            Assert.Equal((PlacedKind.FloorDecal, RenderKind.Billboard, dir), (vent.Kind, vent.Shape, vent.Dir));
            Assert.Equal(FloorLayers.LiftOf(FloorLayers.Rank(FloorLayers.GamePlane, FloorLayers.GasScrubber)), vent.Height, 5); // at its layer's place in the stack (D122), not on the table
        }
        // A tile with a table makes no difference: a hatch is in the floor.
        MapGrid grid = Grid(8, 8);
        Dictionary<long, Placed> placed = ChunkPlanner.Plan(grid, null, new[] { Thing(1, "/obj/structure/table", 4, 4), Thing(2, Scrubber, 4, 4) }).Chunks.SelectMany(c => c.Placed).ToDictionary(p => p.EntityId);
        Assert.Equal(PlacedKind.FloorDecal, placed[2].Kind);
        Assert.True(placed[2].Height > 0f && placed[2].Height < WorldGeometry.TableTop, "the hatch is a few millimetres above the floor, not on the table top");
    }

    [Fact]
    public void ThePathRulesHideNothingTgShowsAndTheFakeFeedHidesPipesByTheTurf()
    {
        ITypeSource types = FakeMap.DefaultTypes();
        Assert.True(types.TryGet("/obj/machinery/atmospherics/components/unary/vent_scrubber/on/layer2", out TypeDefaults scrubber));
        Assert.True(scrubber.Invisibility <= 25f, "a scrubber is shown");
        Assert.False(string.IsNullOrEmpty(scrubber.IconState));
        Assert.True(types.TryGet("/obj/machinery/atmospherics/pipe/smart/manifold4w", out TypeDefaults pipe));
        Assert.True(pipe.Invisibility <= 25f, "a pipe is no longer hidden by its path (D122): the undertile rule hides it under a floor tile (FakeMap)");
        Assert.True(PropKinds.HasUndertile("/obj/machinery/atmospherics/pipe/smart/manifold4w"));
    }

    [Fact]
    public void EveryVentAndScrubberOfMetaLiesOnTheFloor()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        var hatches = world.Map.Entities.Where(e => PropKinds.IsFloorHatch(e.TypePath)).ToList();
        _output.WriteLine($"{hatches.Count} hatches: " + string.Join(", ", hatches.GroupBy(e => e.TypePath.Split('/')[^1].Replace("layer", "l")).Select(g => g.Key + " x" + g.Count()).Take(12)));
        int vents = hatches.Count(e => e.TypePath.Contains("/vent_pump", StringComparison.Ordinal) || e.TypePath.Contains("/vent_scrubber", StringComparison.Ordinal));
        Assert.True(vents > 450, "Meta has hundreds of air vents and scrubbers: " + vents);
        foreach (FakeMapEntity e in hatches)
        {
            Assert.True(world.Placed.TryGetValue(e.Id, out Placed p), e.TypePath);
            Assert.Equal(PlacedKind.FloorDecal, p.Kind);
            Assert.True(p.Height > 0f && p.Height < 0.06f, e.TypePath + " lies a few millimetres above the floor: " + p.Height);
        }
        Assert.Contains(world.Map.Entities, e => e.TypePath == Scrubber && e.X == 117 && e.Y == 109); // the owner's scrubber
    }

    // ---- everything else still stands on the floor ----

    [Fact]
    public void NothingOnMetaFloatsThatIsNotMountedOrOnATable()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        var tableCells = world.Map.Entities.Where(e => PropKinds.IsTable(e.TypePath)).Select(e => (e.X, e.Y)).ToHashSet();
        int standing = 0, items = 0, resting = 0;
        foreach (FakeMapEntity e in world.Map.Entities)
        {
            if (!world.Placed.TryGetValue(e.Id, out Placed p) || p.Kind is PlacedKind.WallDecal or PlacedKind.Piece or PlacedKind.FloorDecal)
                continue;
            bool onTable = tableCells.Contains((e.X, e.Y)) && PropKinds.StandsOnTable(e.TypePath);
            Assert.Equal(onTable ? WorldGeometry.TableTop : 0f, p.Height);
            Assert.Equal(PropKinds.IsItem(e.TypePath), p.Kind == PlacedKind.Item);
            Assert.Equal(p.Kind == PlacedKind.Resting, !PropKinds.IsItem(e.TypePath) && (onTable || PropKinds.RestsOnFloor(e.TypePath)));
            standing++;
            if (p.Kind == PlacedKind.Item)
                items++;
            if (p.Kind == PlacedKind.Resting)
                resting++;
        }
        _output.WriteLine($"{standing} standing things, {items} items, {resting} resting at the tile scale");
        Assert.True(items > 2000, "Meta has thousands of items: " + items);
        Assert.True(resting > 100, "tables hold machines and the floor holds pads: " + resting);
    }

    // ---- the random posters ----

    [Fact]
    public void NoPosterOfMetaIsThePlaceholderCheckerAndTheChoiceIsTheSameEveryLoad()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        var random = world.Map.Entities.Where(e => e.TypePath.Contains("/poster/random", StringComparison.Ordinal)).ToList();
        Assert.True(random.Count > 10, "Meta has random posters: " + random.Count);
        Assert.All(world.Map.Entities.Where(e => e.TypePath.StartsWith("/obj/structure/sign/poster", StringComparison.Ordinal)), e => Assert.NotEqual("random_anything", e.IconState));
        // tg picks a poster in the poster's Initialize; the fake feed picks by a hash of the cell: another load gives the same posters.
        FakeMap again = FakeMap.Load(FakeMapTests.MetaPath());
        Assert.Equal(random.Select(e => (e.Id, e.IconState, e.Name)), again.Entities.Where(e => e.TypePath.Contains("/poster/random", StringComparison.Ordinal)).Select(e => (e.Id, e.IconState, e.Name)));
        // And they are not all one poster.
        Assert.True(random.Select(e => e.IconState).Distinct().Count() > 5, "the choice varies");
    }

    [RealIconsFact]
    public void EveryRandomPosterOfMetaHasAPictureInThePosterIcon()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        IconLibrary library = RealIcons.NewLibrary();
        foreach (FakeMapEntity e in world.Map.Entities.Where(e => e.TypePath.Contains("/poster/random", StringComparison.Ordinal)))
            Assert.True(library.TryGetIcon(e.Icon, out IIcon icon) && icon.TryGetState(e.IconState, out _), $"{e.Icon}:{e.IconState}");
    }
}
