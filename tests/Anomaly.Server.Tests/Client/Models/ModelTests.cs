using System.IO;
using System.Numerics;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>The 3D models (docs/ai/specs/models.md): the repository's catalog and files, the rules that pick the parts, and how the parts fit the world.</summary>
public sealed class ModelTests
{
    private const float Tile = 2f, Wall = 2f, TableTop = 0.6f;

    private static string Root
    {
        get
        {
            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "godot", "assets", "models");
                if (File.Exists(Path.Combine(candidate, ModelCatalog.FileName)))
                    return candidate;
            }
            throw new InvalidOperationException("godot/assets/models/models.json not found above the test's folder");
        }
    }

    private static ModelCatalog Catalog => ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));

    private static ModelMeshData Build(string id, string part)
    {
        ModelEntry entry = Catalog.Entries.Single(e => e.Id == id);
        ModelPart spec = entry.Parts[part];
        return ModelMesh.Build(GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, spec.File))), entry, spec, Tile, Wall, TableTop);
    }

    private static (Vector3 Min, Vector3 Max) Bounds(ModelMeshData m)
        => (m.Positions.Aggregate(Vector3.Min), m.Positions.Aggregate(Vector3.Max));

    [Fact]
    public void EveryPartOfEveryModelBuilds()
    {
        foreach (ModelEntry entry in Catalog.Entries.Where(e => !e.Hidden)) // a hidden entry draws nothing (a gravity generator's outer parts)
        {
            foreach (string part in ModelRules.PartsOf(entry.Rule).Concat(entry.Parts.Keys).Distinct())
            {
                ModelMeshData mesh = Build(entry.Id, part);
                Assert.True(mesh.Triangles > 0, $"{entry.Id}.{part} has no triangles");
                Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.Positions.Length - 1));
                Assert.All(mesh.Uvs, uv => Assert.True(uv.X is >= -0.001f and <= 1.001f && uv.Y is >= -0.001f and <= 1.001f, $"{entry.Id}.{part}: uv {uv}"));
                if (entry.Texture == ModelTexture.Model)
                {
                    // Its texture, or only plain colours (canister materials and solar glass, drawn as swatches).
                    ModelPart spec = entry.Parts[part];
                    GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, spec.File)));
                    bool plain = ModelMesh.MaterialsOf(file, spec).All(m => m >= 0 && file.Materials[m].Image < 0);
                    Assert.True(mesh.Image >= 0 || plain, $"{entry.Id}.{part} has no texture");
                }
            }
        }
    }

    [Theory]
    [InlineData("icons/obj/doors/airlocks/station/maintenance.dmi", "closed", "station_airlock")]
    [InlineData("icons/obj/doors/airlocks/station/freezer.dmi", "opening", "station_airlock")]
    [InlineData("icons/obj/doors/airlocks/station/overlays.dmi", "closed", null)]
    [InlineData("icons/obj/doors/airlocks/public/glass.dmi", "closed", "public_airlock")]
    [InlineData("icons/obj/doors/airlocks/external/external.dmi", "closed", "external_airlock")]
    [InlineData("icons/obj/doors/airlocks/external/overlays.dmi", "closed", null)]
    [InlineData("icons/obj/doors/airlocks/hatch/maintenance.dmi", "closed", "hatch_airlock")]
    [InlineData("icons/obj/doors/airlocks/hatch/centcom.dmi", "opening", "hatch_airlock")]
    [InlineData("icons/obj/doors/airlocks/vault/vault.dmi", "closed", "vault_airlock")]
    [InlineData("icons/obj/doors/airlocks/highsec/highsec.dmi", "closed", "highsec_airlock")]
    [InlineData("icons/obj/storage/crates.dmi", "securecrateopen", "crate_metal_crate")]
    [InlineData("icons/obj/storage/crates.dmi", "weaponcrate", "crate_ribbed_crate")]
    [InlineData("icons/obj/storage/crates.dmi", "cardboard_moth", "crate_cardboard_crate")]
    [InlineData("icons/obj/storage/crates.dmi", "mail", "crate_mail_crate")]
    [InlineData("icons/obj/storage/crates.dmi", "freezer", "crate_freezer_crate")]
    [InlineData("icons/obj/storage/closet.dmi", "freezer", "locker_freezer")]
    [InlineData("icons/obj/storage/crates.dmi", "grave", null)]
    [InlineData("icons/obj/smooth_structures/table.dmi", "table-38", "metal_table")]
    [InlineData("icons/obj/smooth_structures/glass_table.dmi", "glass_table-0", "table_glass_table")]
    [InlineData("icons/obj/smooth_structures/wood_table.dmi", "wood_table-38", "table_wood_table")]
    [InlineData("icons/obj/smooth_structures/rollingtable.dmi", "rollingtable", "table_rollingtable")]
    [InlineData("icons/obj/doors/doorfireglass.dmi", "door_open", "firelock_glass")]
    [InlineData("icons/obj/doors/windoor.dmi", "rightsecureopen", "windoor_right")]
    [InlineData("icons/obj/doors/windoor.dmi", "clockwork", null)]
    [InlineData("icons/obj/machines/computer.dmi", "computer", "computer")]
    [InlineData("icons/obj/machines/computer.dmi", "laptop", "medical_laptop")]
    [InlineData("icons/obj/chairs.dmi", "chair", "metal_chair")]
    [InlineData("icons/obj/chairs.dmi", "stool", "chair_stool")]
    [InlineData("icons/obj/chairs.dmi", "officechair_white", "chair_office_white")]
    [InlineData("icons/obj/machines/vending.dmi", "cigs-off", "vending_cigs")]
    [InlineData("icons/obj/machines/vending.dmi", "cigs-light-mask", null)]
    [InlineData("icons/obj/structures.dmi", "rack", "storage_rack")]
    [InlineData("icons/obj/watercloset.dmi", "toilet10", "toilet")]
    [InlineData("gags:/datum/greyscale_config/canister|#c6c0b5", "", "canister_default")]
    [InlineData("gags:/datum/greyscale_config/canister/stripe|#2786e5#e8fefe", "", "canister_stripe")]
    [InlineData("gags:/datum/greyscale_config/canister/double_stripe|#9fba6c#3d4680", "", "canister_double_stripe")]
    [InlineData("gags:/datum/greyscale_config/canister/hazard|#f62800#000000", "", "canister_hazard")]
    [InlineData("gags:/datum/greyscale_config/stationary_canister|#ffffff", "canister-12", "stationary_tank_j12")]
    [InlineData("icons/obj/machines/engine/supermatter.dmi", "sm", "supermatter")]
    [InlineData("icons/obj/machines/engine/supermatter.dmi", "sm-glow", "supermatter_glow")]
    [InlineData("icons/obj/machines/engine/supermatter.dmi", "sm_shard", null)]
    public void TheCatalogNamesWhatEachModelDraws(string icon, string state, string model)
        => Assert.Equal(model, Catalog.Find(icon, state)?.Id);

    [Theory]
    [InlineData(0, ModelRules.Single, 0)]
    [InlineData(4, ModelRules.End, 0)] // tg's EAST
    [InlineData(1, ModelRules.End, 1)] // NORTH: the east end turned a quarter left
    [InlineData(8, ModelRules.End, 2)] // WEST
    [InlineData(2, ModelRules.End, 3)] // SOUTH
    [InlineData(12, ModelRules.Straight, 0)] // east and west
    [InlineData(3, ModelRules.Straight, 1)] // north and south
    [InlineData(1 | 4 | 16, ModelRules.Corner, 0)] // north, east and the north-east diagonal: a block
    [InlineData(1 | 4, ModelRules.CornerOpen, 0)] // an L
    [InlineData(1 | 8 | 128, ModelRules.Corner, 1)] // north and west
    [InlineData(2 | 8, ModelRules.CornerOpen, 2)] // south and west
    [InlineData(2 | 4 | 32, ModelRules.Corner, 3)] // south and east
    [InlineData(1 | 4 | 8, ModelRules.Tee, 0)]
    [InlineData(2 | 4 | 8, ModelRules.Tee, 2)]
    [InlineData(15, ModelRules.Center, 0)]
    [InlineData(255, ModelRules.Center, 0)]
    public void ATablesJunctionPicksTheKitsModuleAndTurn(int junction, string part, int turns)
        => Assert.Equal((part, turns), ModelRules.Smooth4(junction));

    [Fact]
    public void EveryCardinalMaskHasAModuleWhoseTurnJoinsExactlyThoseSides()
    {
        // The modules join unturned: an end east, a straight run east and west, a corner north and east, a tee north, east and west.
        var unturned = new Dictionary<string, int>
        {
            [ModelRules.Single] = 0,
            [ModelRules.End] = ModelRules.East,
            [ModelRules.Straight] = ModelRules.East | ModelRules.West,
            [ModelRules.Corner] = ModelRules.North | ModelRules.East,
            [ModelRules.CornerOpen] = ModelRules.North | ModelRules.East,
            [ModelRules.Tee] = ModelRules.North | ModelRules.East | ModelRules.West,
            [ModelRules.Center] = 15,
        };
        for (int junction = 0; junction < 16; junction++)
        {
            (string part, int turns) = ModelRules.Smooth4(junction);
            int mask = unturned[part];
            for (int k = 0; k < turns; k++)
                mask = ModelRules.TurnLeft(mask);
            Assert.Equal(ModelRules.Mask(junction), mask);
        }
    }

    [Fact]
    public void ATableModuleFillsTheTileAndStandsAsHighAsTheTableTop()
    {
        // The kit's top is 0.75 m high; the catalog's source height is the height that is fitted to the world's table top (0.75: exactly on it).
        float top = 0.75f * TableTop / Catalog.Entries.Single(e => e.Id == "metal_table").SourceHeight;
        foreach (string part in ModelRules.PartsOf(ModelRule.Smooth4))
        {
            (Vector3 min, Vector3 max) = Bounds(Build("metal_table", part));
            Assert.Equal(top, max.Y, 3);
            Assert.InRange(min.X, -1.004f, -0.8f);
            Assert.InRange(max.X, 0.8f, 1.004f);
        }
        // A connected side reaches the tile's edge, where the neighbour's top goes on.
        Assert.Equal(1f + ModelMesh.JoinOverlap, Bounds(Build("metal_table", ModelRules.Center)).Max.X, 3);
        Assert.Equal(0f, Bounds(Build("metal_table", ModelRules.Single)).Min.Y, 3);
    }

    [Fact]
    public void ATableModulesOpenSidesAreDrawnInToItsFootprintAndItsJoinedSidesReachTheTilesEdge()
    {
        // The owner (2026-10-19): tables 0.8 of a tile. Each module unturned joins ModelRules.JoinsOf (north is -z): a joined side reaches the tile's edge (1 m),
        // an open one stops at the footprint's share of it, so a run of tables is unbroken and narrower than its tiles.
        ModelEntry table = Catalog.Entries.Single(e => e.Id == "metal_table");
        Assert.Equal(0.8f, table.Footprint, 3);
        foreach (string part in ModelRules.PartsOf(ModelRule.Smooth4))
        {
            (Vector3 min, Vector3 max) = Bounds(Build("metal_table", part));
            int joins = ModelRules.JoinsOf(part);
            float Want(int side) => (joins & side) != 0 ? 1f + ModelMesh.JoinOverlap : table.Footprint; // a joined side overlaps the neighbour a hair
            Assert.Equal(Want(ModelRules.East), max.X, 3);
            Assert.Equal(-Want(ModelRules.West), min.X, 3);
            Assert.Equal(-Want(ModelRules.North), min.Z, 3);
            Assert.Equal(Want(ModelRules.South), max.Z, 3);
        }
        // a leg moves in with its side and keeps its 10 cm section
        float scale = TableTop / table.SourceHeight;
        ModelMeshData single = Build("metal_table", ModelRules.Single);
        Vector3[] low = single.Positions.Where(p => p.Y < 0.66f * scale - 0.01f && p.X > 0f && p.Z > 0f).ToArray();
        Assert.Equal(0.10f * scale, low.Max(p => p.X) - low.Min(p => p.X), 3);
        Assert.True(low.Max(p => p.X) <= table.Footprint + 1e-3f, "the leg stands under the drawn-in top");
    }

    [Fact]
    public void ATablesTopShowsTgsJunctionPictureWithItsOutlineOnTheOpenSides()
    {
        // The owner (2026-10-19): the top shows tg's table-N, the outline on the open sides. A single table (table-0): its top's corners at tg's outline,
        // 2 pixels in from the west, north and east of the 32-pixel cell and on row 25 at the south, where tg's front begins.
        ModelEntry table = Catalog.Entries.Single(e => e.Id == "metal_table");
        Assert.True(table.SpriteTop);
        ModelPart single = table.Parts[ModelRules.Single];
        var asked = new List<int>();
        ModelMeshData mesh = ModelMesh.Build(GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, single.File))), table, single, Tile, Wall, TableTop, junction: 0,
            topCell: junction => { asked.Add(junction); return new Vector4(0f, 0f, 1f, 1f); });
        Assert.Contains(0, asked);
        float top = mesh.Positions.Max(p => p.Y);
        Vector2[] corners = Enumerable.Range(0, mesh.Positions.Length).Where(i => mesh.Normals[i].Y > 0.9f && mesh.Positions[i].Y > top - 1e-4f)
            .Select(i => new Vector2(mesh.Uvs[i].X * 32f, (1f - mesh.Uvs[i].Y) * 32f)).ToArray();
        Assert.NotEmpty(corners);
        Assert.Equal(2f, corners.Min(c => c.X), 2);
        Assert.Equal(30f, corners.Max(c => c.X), 2);
        Assert.Equal(2f, corners.Min(c => c.Y), 2);
        Assert.Equal(25f, corners.Max(c => c.Y), 2);

        // a straight run turned a quarter (north and south joined): tg's table-3, its top running to the cell's north and south edges
        ModelPart straight = table.Parts[ModelRules.Straight];
        asked.Clear();
        mesh = ModelMesh.Build(GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, straight.File))), table, straight, Tile, Wall, TableTop, junction: ModelRules.TgNorth | ModelRules.TgSouth,
            topCell: junction => { asked.Add(junction); return new Vector4(0f, 0f, 1f, 1f); });
        Assert.Contains(ModelRules.TgNorth | ModelRules.TgSouth, asked);
        top = mesh.Positions.Max(p => p.Y);
        float[] rows = Enumerable.Range(0, mesh.Positions.Length).Where(i => mesh.Normals[i].Y > 0.9f && mesh.Positions[i].Y > top - 1e-4f)
            .Select(i => (1f - mesh.Uvs[i].Y) * 32f).ToArray();
        Assert.Equal(0f, rows.Min(), 2);
        Assert.Equal(32f, rows.Max(), 2);
    }

    [Fact]
    public void AnLsInnerCornerIsCutOutOfTheCornerModulesTopAndItsLegStandsUnderTheTop()
    {
        // The owner (2026-10-19): an L of tables showed a square of the corner module past its neighbours' drawn-in edges. tg's table-6 (joined east and
        // south, not south-east): nothing of the module lies in the south-east square past the footprint, and its legs stand under the top.
        ModelEntry table = Catalog.Entries.Single(e => e.Id == "metal_table");
        int junction = ModelRules.TgEast | ModelRules.TgSouth;
        (string part, _) = ModelRules.Smooth4(junction);
        Assert.Equal(ModelRules.CornerOpen, part);
        ModelPart spec = table.Parts[part];
        ModelMeshData mesh = ModelMesh.Build(GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, spec.File))), table, spec, Tile, Wall, TableTop, junction: junction);
        float reach = Tile / 2f * table.Footprint;
        for (int t = 0; t + 2 < mesh.Indices.Length; t += 3)
        {
            Vector3 c = (mesh.Positions[mesh.Indices[t]] + mesh.Positions[mesh.Indices[t + 1]] + mesh.Positions[mesh.Indices[t + 2]]) / 3f;
            Assert.False(c.X > reach + 1e-3f && c.Z > reach + 1e-3f, $"a face at ({c.X}, {c.Z}) in the cut corner");
        }
        // the open sides (north and west) are drawn in, the joined ones reach the edge
        (Vector3 min, Vector3 max) = Bounds(mesh);
        Assert.Equal(-reach, min.X, 3);
        Assert.Equal(-reach, min.Z, 3);
        Assert.Equal(1f + ModelMesh.JoinOverlap, max.X, 3);
        Assert.Equal(1f + ModelMesh.JoinOverlap, max.Z, 3);
    }

    [Fact]
    public void ATablesLegsKeepTheirProportions()
    {
        // The single table's legs are 10 cm square in the kit (its top 9 cm thick); the 0.9 m cell is stretched to 2 m but the legs are scaled as the height is.
        float scale = TableTop / Catalog.Entries.Single(e => e.Id == "metal_table").SourceHeight;
        ModelMeshData single = Build("metal_table", ModelRules.Single);
        Vector3[] low = single.Positions.Where(p => p.Y < 0.66f * scale - 0.01f && p.X > 0f && p.Z > 0f).ToArray();
        Assert.Equal(0.10f * scale, low.Max(p => p.X) - low.Min(p => p.X), 3);
    }

    [Fact]
    public void TheChairIsFittedAsTheTableThenScaled()
    {
        ModelEntry chair = Catalog.Entries.Single(e => e.Id == "metal_chair");
        (Vector3 min, Vector3 max) = Bounds(Build("metal_chair", "model"));
        Assert.Equal(0.97f * TableTop / chair.SourceHeight * chair.Scale, max.Y, 2);
        Assert.Equal(0.544f * TableTop / chair.SourceHeight * chair.Scale, max.X - min.X, 2);
    }

    [Fact]
    public void ABodyLiesOnTheBedsAndStretchersTopButNotOnAChair()
    {
        // DV127 (M8y): a patient buckled to a bed, a medical bed or a roller bed is drawn on its model's top (MapRenderer.TryLieOn)
        ModelCatalog catalog = Catalog;
        Assert.True(catalog.Find("icons/obj/bed.dmi", "bed").RestOnTop);
        foreach (string state in new[] { "emerg_down", "emerg_up", "med_down", "med_up" })
            Assert.True(catalog.Find("icons/obj/medical/medical_bed.dmi", state).RestOnTop, state);
        Assert.False(catalog.Find("icons/obj/chairs.dmi", "chair").RestOnTop);
    }

    [Fact]
    public void AChairIsTurnedByItsDirAndLooksSouthUnturned()
    {
        ModelEntry chair = Catalog.Entries.Single(e => e.Id == "metal_chair");
        Assert.Equal(0f, ModelRules.Choose(chair, "chair", 2, 0)[0].Yaw, 4);
        Assert.Equal(MathF.PI, MathF.Abs(ModelRules.Choose(chair, "chair", 1, 0)[0].Yaw), 4);
    }

    [Fact]
    public void ATramDoorSpansTowardItsFillerWhateverTheWallsSay()
    {
        // door.dm get_adjusted_dir: NORTH or SOUTH puts the filler east (the model unturned, its +x east), EAST or WEST north (+x turned to north, -z)
        ModelEntry door = Catalog.Entries.Single(e => e.Id == "tram_door");
        Assert.True(door.MultiTile);
        foreach (byte face in new byte[] { 0, 1, 4 })
        {
            Assert.Equal(0f, ModelRules.Choose(door, "closed", 2, face)[0].Yaw, 4);
            float yaw = ModelRules.Choose(door, "closed", 4, face)[0].Yaw;
            Assert.Equal(1f, MathF.Sin(yaw), 4); // Godot's basis about y takes +x to (cos, 0, -sin): -z, north
        }
    }

    [Fact]
    public void AButtonPressedAgainWhileItsSuccessLightShowsKeepsTheSecondLightOnItsModel()
    {
        // The owner (2026-10-19): a door button clicked again within tg's 1 second flick_overlay_view of button-overlay-success had a second success
        // visual the model already drawn draws; refusing it as "no new variant" left it hanging below the button as a green picture.
        const string icon = "icons/obj/machines/wallmounts.dmi";
        ModelCatalog catalog = Catalog;
        string[] appearance = { "button-light-mask" };
        ModelEntry first = catalog.FindVisualVariant(icon, "button", appearance, "button-overlay-success");
        Assert.Equal("door_button_success", first?.Id);
        ModelEntry second = catalog.FindVisualVariant(icon, "button", appearance.Append("button-overlay-success"), "button-overlay-success");
        Assert.Same(first, second);
        Assert.Equal("door_button_error", catalog.FindVisualVariant(icon, "button", appearance, "button-overlay-error")?.Id);
        // A visual no variant names is not absorbed: it is drawn as its own picture.
        Assert.Null(catalog.FindVisualVariant(icon, "button", appearance, "button-overlay-unknown"));
    }

    [Fact]
    public void AnElevatorCallButtonsLightIsDrawnByItsModelsPart()
    {
        // The owner (2026-11-13): the call button's success light (a part drawn with tram-overlay-success, not a variant of its own) hung below the model, flat on the wall
        const string icon = "icons/obj/machines/wallmounts.dmi";
        string[] appearance = { "tram-light-mask" };
        Assert.Equal("tram_call_button", Catalog.FindVisualVariant(icon, "tram", appearance, "tram-overlay-success")?.Id);
        Assert.Equal("tram_call_button", Catalog.FindVisualVariant(icon, "tram", appearance, "tram-overlay-error")?.Id);
        Assert.Null(Catalog.FindVisualVariant(icon, "tram", appearance, "tram-overlay-unknown"));
    }

    [Fact]
    public void AnOpenAirlockKeepsAStripOfEachLeafInTheDoorway()
    {
        const float pixel = Tile / 32f;
        ModelMeshData left = Build("station_airlock", "left"), right = Build("station_airlock", "right");
        // Open, a leaf slides by its UV2.x: where the seam steps out three pixels of the left leaf stay inside the doorway; the right leaf's farthest edge keeps one.
        Assert.Equal(-1f + 3 * pixel, Bounds(left).Max.X + left.Uv2[0].X, 4);
        Assert.Equal(1f - pixel, Bounds(right).Min.X + right.Uv2[0].X + 0.125f, 4);
        Assert.All(left.Uv2, uv => Assert.True(uv.X < 0f));
        Assert.All(right.Uv2, uv => Assert.True(uv.X > 0f));
        // The glass inserts are marked.
        Assert.Contains(left.Uv2, uv => uv.Y == ModelMeshData.GlassMark);
    }

    [Fact]
    public void ASpriteFaceTakesThePixelItStandsOn()
    {
        const float p = 2f / 32f;
        Assert.Equal(new Vector2(0f, 0f), ModelMesh.SpriteUv(new Vector3(-1f, 0f, 0.08f), Vector3.UnitZ, 2f, 2f));
        Assert.Equal(new Vector2(1f, 1f), ModelMesh.SpriteUv(new Vector3(1f, 2f, -0.08f), -Vector3.UnitZ, 2f, 2f));
        // A side face takes the pixel inside the thing: the west side at x -1 takes column 0, a face looking east at column 16 takes column 15.
        Assert.InRange(ModelMesh.SpriteUv(new Vector3(-1f, 1f, 0f), -Vector3.UnitX, 2f, 2f).X * 32f, 0f, 1f);
        Assert.InRange(ModelMesh.SpriteUv(new Vector3(-1f + 16 * p, 1f, 0f), Vector3.UnitX, 2f, 2f).X * 32f, 15f, 16f);
        // A face on a half pixel (a hatch's teeth, 0.969 m up) takes a pixel's inside, never the line between two: nearest sampling speckled both.
        float row = ModelMesh.SpriteUv(new Vector3(0f, 15.5f * p, 0f), -Vector3.UnitY, 2f, 2f).Y * 32f;
        Assert.True(Math.Abs(row - MathF.Round(row)) > 0.2f, $"row {row} is on a pixel's edge");
    }

    [Fact]
    public void AMalformedCatalogOrFileIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => ModelCatalog.Parse("""{ "models": [ { "id": "x", "icons": [], "rule": "airlock", "texture": "sprite", "parts": {} } ] }"""));
        Assert.Throws<InvalidDataException>(() => ModelCatalog.Parse("""{ "models": [ { "id": "x", "icons": [], "rule": "crate", "texture": "cell", "parts": { "lid": "a.glb" } } ] }"""));
        Assert.Throws<InvalidDataException>(() => GlbFile.Read(new byte[20]));
    }

    [Theory]
    [InlineData("locker_wardrobe")]
    [InlineData("locker_shelves")]
    [InlineData("locker_rack")]
    [InlineData("locker_freezer")]
    public void ALockersShelvesAreFoundInsideItsBody(string id)
    {
        ModelMeshData body = Build(id, ModelRules.Body);
        Vector3[] slots = ModelMesh.Shelves(body);
        (Vector3 min, Vector3 max) = Bounds(body);
        Assert.NotEmpty(slots);
        Assert.All(slots, s =>
        {
            Assert.InRange(s.X, min.X, max.X);
            Assert.InRange(s.Z, min.Z, max.Z);
            Assert.InRange(s.Y, 0f, max.Y - 0.05f); // inside, under the roof
        });
        // The highest shelf first.
        Assert.Equal(slots.Max(s => s.Y), slots[0].Y);
    }

    [Fact]
    public void ALockersDoorIsBuiltAboutItsHinge()
    {
        ModelMeshData front = Build("locker_wardrobe", ModelRules.DoorFront), inside = Build("locker_wardrobe", ModelRules.DoorInside);
        // The hinge on the left front edge (the GLB's node at x -0.45, z 0.2825), the door reaching from it to the right.
        Assert.Equal(-0.45f, front.Pivot.X, 3);
        Assert.Equal(0.2825f, front.Pivot.Z, 3);
        Assert.Equal(front.Pivot, inside.Pivot);
        Assert.InRange(Bounds(front).Min.X, -0.01f, 0.01f);
        Assert.InRange(Bounds(front).Max.X, 0.85f, 0.95f);
        // Each face of the door maps one icon frame of closet.png (0..1), the front and its back are separate parts.
        Assert.All(front.Uvs.Concat(inside.Uvs), uv => Assert.True(uv.X is >= 0f and <= 1f && uv.Y is >= 0f and <= 1f));
        Assert.True(front.Triangles > inside.Triangles);
    }

    [Fact]
    public void CellUvsMapAPrimitiveOverItsIconFrame()
    {
        // A 640 x 624 sheet of 32 x 48 frames: a face over pixels (232..247, 19..47) of the frame at (224, 0) maps 0.25..0.72 across and v up.
        Vector2[] uv = ModelMesh.CellUvs(new[] { new Vector2(232f / 640, 19f / 624), new Vector2(247f / 640, 47f / 624) }, (640, 624), 32, 48);
        Assert.Equal(8f / 32, uv[0].X, 4);
        Assert.Equal(1f - 19f / 48, uv[0].Y, 4);
        Assert.Equal(23f / 32, uv[1].X, 4);
        Assert.Equal(1f - 47f / 48, uv[1].Y, 4);
    }

    [Theory]
    [InlineData(new[] { "generic_door" }, false, "generic")]
    [InlineData(new[] { "blue_door", "locked" }, false, "blue")]
    [InlineData(new[] { "generic_open" }, true, null)]
    [InlineData(new[] { "locked" }, null, null)]
    [InlineData(new string[0], null, null)]
    public void AClosetsDoorIsReadFromItsOverlays(string[] overlays, bool? open, string door)
    {
        Assert.Equal(open, ClosetLook.Open(overlays, out string seen));
        Assert.Equal(door, seen);
    }

    [Theory]
    [InlineData(1, 2)] // a wall north: it looks south, as tg draws it
    [InlineData(8, 4)] // west: east
    [InlineData(4, 8)] // east: west
    [InlineData(2, 1)] // south: north
    [InlineData(0, 2)] // none: south
    [InlineData(1 | 8, 2)] // a corner: the north wall first
    public void AClosetLooksAwayFromTheWallBehindIt(int walls, int face)
        => Assert.Equal((byte)face, ModelRules.FaceAwayFromWall(dir => (walls & dir) != 0));

    [Fact]
    public void TheClosetsAndCratesKeepTheWayTheyFaceAndTurnAwayFromTheWall()
    {
        foreach (ModelEntry entry in Catalog.Entries.Where(e => e.Rule is ModelRule.Closet or ModelRule.Crate))
            Assert.True(entry.AwayFromWall && entry.KeepOnPush, entry.Id);
        // A thing whose dir tg draws (dir4: a chair, a bed, a sink) keeps tg's way; only things tg draws one way may be turned from the wall.
        Assert.All(Catalog.Entries.Where(e => e.Rule is ModelRule.Dir4 or ModelRule.Console or ModelRule.Airlock or ModelRule.Smooth4), e => Assert.False(e.AwayFromWall || e.KeepOnPush, e.Id));
    }

    // Where a part is when its door is fully open: its fitted bounds moved by its slide.
    private static (Vector3 Min, Vector3 Max) OpenBounds(ModelMeshData m)
    {
        Vector2 slide = ModelMeshData.SlideOf(m.Uv2[0]);
        (Vector3 min, Vector3 max) = Bounds(m);
        var by = new Vector3(slide.X, slide.Y, 0f);
        return (min + by, max + by);
    }

    [Fact]
    public void AnExternalAirlocksLeavesPartIntoTheCeilingAndTheFloorAndKeepAStrip()
    {
        const float pixel = Tile / 32f;
        ModelMeshData frame = Build("external_airlock", "frame"), upper = Build("external_airlock", "upper"), lower = Build("external_airlock", "lower");
        Assert.All(frame.Uv2, uv => Assert.Equal(Vector2.Zero, ModelMeshData.SlideOf(uv))); // the frame stays
        Assert.All(upper.Uv2, uv => Assert.True(ModelMeshData.SlideOf(uv).Y > 0f && ModelMeshData.SlideOf(uv).X == 0f));
        Assert.All(lower.Uv2, uv => Assert.True(ModelMeshData.SlideOf(uv).Y < 0f));
        // Open, the upper leaf keeps about one sprite pixel in the doorway past the frame's border (three sprite pixels), a hair less, so that its middle tongue
        // is inside the lintel, not in the plane of its underside; the lower one sinks wholly under the border (its top flush with the border showed through it).
        Assert.InRange(OpenBounds(upper).Min.Y, Wall - 4 * pixel + 0.005f, Wall - 4 * pixel + 0.02f);
        Assert.True(OpenBounds(lower).Max.Y < 3 * pixel - 0.05f, $"lower {OpenBounds(lower).Max.Y}");
        // The window's glass is marked; the leaves are thinner than the frame, so they slide inside it.
        Assert.Contains(upper.Uv2, ModelMeshData.IsGlass);
        Assert.True(Bounds(upper).Max.Z < Bounds(frame).Max.Z);
    }

    [Fact]
    public void AHatchsSectionsPartToTheCornersAndKeepACornerEach()
    {
        const float pixel = Tile / 32f;
        foreach ((string part, int sx, int sy) in new[] { ("upperLeft", -1, 1), ("upperRight", 1, 1), ("lowerLeft", -1, -1), ("lowerRight", 1, -1) })
        {
            ModelMeshData m = Build("hatch_airlock", part);
            (Vector3 min, Vector3 max) = OpenBounds(m);
            // Open, five sprite pixels of each section stay inside the doorway (the jamb at x = +-1, the floor and the ceiling), at its corner.
            float across = sx < 0 ? max.X - -1f : 1f - min.X, up = sy > 0 ? Wall - min.Y : max.Y;
            Assert.Equal(5 * pixel, across, 3);
            Assert.Equal(5 * pixel, up, 3);
        }
    }

    [Theory]
    [InlineData("public_airlock")]
    [InlineData("vault_airlock")]
    public void AnOpenDoublesLeafDoorKeepsThreePixelsOfEachLeafInTheDoorway(string id)
    {
        const float pixel = Tile / 32f;
        ModelMeshData left = Build(id, "left"), right = Build(id, "right");
        Assert.Equal(-1f + 3 * pixel, OpenBounds(left).Max.X, 3);
        // The right leaf's slab (a vault's lock reaches past it) keeps as much.
        Assert.Equal(1f - 3 * pixel, ModelMeshData.SlideOf(right.Uv2[0]).X + (id == "vault_airlock" ? 0f : Bounds(right).Min.X), 3);
    }

    [Fact]
    public void AGlassTopIsSeeThroughByItsMaterialsAlphaAndADoorsGlassByItsPicture()
    {
        ModelMeshData glassTop = Build("table_glass_table", ModelRules.Single);
        Assert.Contains(glassTop.Uv2, uv => ModelMeshData.IsGlass(uv) && MathF.Abs(ModelMeshData.GlassAlphaOf(uv) - 0.42f) < 0.006f);
        Assert.All(glassTop.Uv2, uv => Assert.Equal(Vector2.Zero, ModelMeshData.SlideOf(uv)));
        // A sliding door's glass keeps its slide and takes tg's glass from its picture (its own alpha 1).
        ModelMeshData upper = Build("external_airlock", "upper");
        Assert.All(upper.Uv2.Where(ModelMeshData.IsGlass), uv => Assert.Equal(1f, ModelMeshData.GlassAlphaOf(uv)));
        Assert.All(upper.Uv2, uv => Assert.True(ModelMeshData.SlideOf(uv).Y > 0.5f));
        // The encoding round-trips.
        var mark = new Vector2(0.3f, ModelMeshData.Uv2Y(-0.8f, glass: true, alpha: 0.35f));
        Assert.Equal(-0.8f, ModelMeshData.SlideOf(mark).Y, 4);
        Assert.Equal(0.35f, ModelMeshData.GlassAlphaOf(mark), 4);
    }

    [Fact]
    public void EveryVariantsOwnTextureIsThere()
    {
        // A table variant shows its own tg sheet, the icon it is drawn for (the kit's uv point into the sheet's layout, which every variant of a family shares).
        foreach (ModelEntry entry in Catalog.Entries.Where(e => e.Image != null))
        {
            if (entry.Image.EndsWith(".dmi", StringComparison.Ordinal))
                Assert.Contains(entry.Image, Catalog.Entries.SelectMany(e => e.Icons)); // a flipped table shows its upright table's sheet
            else
                Assert.True(File.Exists(Path.Combine(Root, entry.Image)), $"{entry.Id}: {entry.Image}");
        }
        Assert.Equal(23, Catalog.Entries.Count(e => e.Id.StartsWith("table_", StringComparison.Ordinal) && !e.Id.EndsWith("_flipped", StringComparison.Ordinal)));
    }

    [Fact]
    public void AFirelockStandsInFrontOfItsAirlockAndAWindoorOnTheTilesEdge()
    {
        ModelEntry firelock = Catalog.Entries.Single(e => e.Id == "firelock_glass"), windoor = Catalog.Entries.Single(e => e.Id == "windoor_left");
        // The firelock's frame (0.26 m deep) clears the deepest airlock leaf (the vault's, 0.155 m from the middle).
        float frameHalf = Bounds(Build("firelock_glass", "frame")).Max.Z;
        Assert.True(firelock.Offset.Z - frameHalf > 0.155f, $"firelock frame from {firelock.Offset.Z - frameHalf}");
        Assert.Equal(-0.94f, windoor.Offset.Z, 3); // WorldGeometry.Edge
    }

    [Fact]
    public void AComputerDrawsItsBodyScreenAndKeyboardTurnedByItsDir()
    {
        ModelEntry computer = Catalog.Entries.Single(e => e.Id == "computer");
        ModelPlacement[] south = ModelRules.Choose(computer, "computer", 2, 0), north = ModelRules.Choose(computer, "computer", 1, 0);
        Assert.Equal(new[] { ModelRules.Body, ModelRules.Screen, ModelRules.Keyboard }, south.Select(p => p.Part));
        Assert.Equal(0f, south[0].Yaw, 4);
        Assert.Equal(MathF.PI, MathF.Abs(north[0].Yaw), 4);
        // The keyboard juts toward its front (south, +Z unturned), where its operator stands.
        Assert.True(Bounds(Build("computer", ModelRules.Keyboard)).Max.Z > Bounds(Build("computer", ModelRules.Body)).Max.Z - 0.01f);
    }

    [Fact]
    public void ATextureTransformSelectsTheTileAModelsUvAreLocalTo()
    {
        // The cigarette vendor's cabinet uv are local to its 32 x 32 tile; KHR_texture_transform puts them in the vending sheet's tile.
        ModelEntry vendor = Catalog.Entries.Single(e => e.Id == "vending_cigs");
        GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, vendor.Parts["model"].File)));
        GlbMaterial atlas = file.Materials.First(m => m.Name.StartsWith("Vending_Front", StringComparison.Ordinal));
        Assert.NotEqual(Vector2.One, atlas.UvScale);
        int index = file.Materials.IndexOf(atlas);
        ModelMeshData cabinet = ModelMesh.Build(file, vendor, vendor.Parts["model"], Tile, Wall, TableTop, index);
        // Every uv lies in the tile: from offset to offset + scale (v up in the mesh).
        Assert.All(cabinet.Uvs, uv => Assert.InRange(uv.X, atlas.UvOffset.X - 0.001f, atlas.UvOffset.X + atlas.UvScale.X + 0.001f));
        Assert.All(cabinet.Uvs, uv => Assert.InRange(1f - uv.Y, atlas.UvOffset.Y - 0.001f, atlas.UvOffset.Y + atlas.UvScale.Y + 0.001f));
    }

    [Fact]
    public void AVendingMachinesTexturedFacesAreLiveFramesOfItsOwnState()
    {
        // The snack machine's materials each show one frame of tg's vending sheet (the embedded image is the sheet): its "snack" frame, south. Such a face is drawn
        // with the machine's picture as tg composes it now, while it shows "snack" or a variant of it (off, broken, its deny flick).
        string stem = Path.Combine(Root, "..", "tg", "icons", "obj", "machines", "vending");
        if (!File.Exists(stem + ".json"))
            return; // the converted icons are not in this checkout
        ModelEntry vendor = Catalog.Entries.Single(e => e.Id == "vending_snack");
        ModelPart model = vendor.Parts["model"];
        GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, model.File)));
        var sheet = Anomaly.Client.Shared.Icons.IconSheet.Parse(File.ReadAllText(stem + ".json"), null);
        int[] textured = ModelMesh.MaterialsOf(file, model).Where(m => file.Materials[m].Image >= 0).ToArray();
        Assert.NotEmpty(textured);
        ModelFrame first = null;
        foreach (int m in textured)
        {
            (int width, int height) = ModelMesh.SheetSize(file, file.Materials[m]);
            ModelFrame frame = ModelFrame.Of(file.Materials[m], ModelMesh.UvsOf(file, model, m), sheet, width, height);
            Assert.NotNull(frame);
            Assert.Equal(("snack", Anomaly.Client.Shared.Icons.ByondDir.South), (frame.State, frame.Dir));
            first ??= frame;
        }
        // Built as a live frame, its uv are local to the frame.
        ModelMeshData front = ModelMesh.Build(file, vendor, model, Tile, Wall, TableTop, textured[0], first);
        Assert.All(front.Uvs, uv => Assert.True(uv.X >= -0.001f && uv.X <= 1.001f && uv.Y >= -0.001f && uv.Y <= 1.001f, uv.ToString()));
    }

    [Fact]
    public void AConveyorsBeltIsALiveFrameOfItsStateAndDir()
    {
        // The owner's report (2026-11-05): running belts stood still and their corners showed a straight belt. The belt's faces must all lie in the first frame of the
        // entry's state and of the module's dir, or the client keeps a still texture (ModelFrame.Of); the straight is made for south, the corner for southeast.
        string stem = Path.Combine(Root, "..", "tg", "icons", "obj", "machines", "recycling");
        if (!File.Exists(stem + ".json"))
            return; // the converted icons are not in this checkout
        var sheet = Anomaly.Client.Shared.Icons.IconSheet.Parse(File.ReadAllText(stem + ".json"), null);
        (string Id, string State)[] entries =
        {
            ("conveyor_stopped", "conveyor0"), ("conveyor_forward", "conveyor1"), ("conveyor_reverse", "conveyor-1"),
            ("conveyor_flipped_stopped", "conveyor0-flipped"), ("conveyor_flipped_forward", "conveyor1-flipped"), ("conveyor_flipped_reverse", "conveyor-1-flipped"),
        };
        foreach ((string id, string state) in entries)
            foreach ((string part, byte dir) in new[] { ("model", Anomaly.Client.Shared.Icons.ByondDir.South), ("corner", Anomaly.Client.Shared.Icons.ByondDir.FromIndex(4)) })
            {
                ModelPart model = Catalog.Entries.Single(e => e.Id == id).Parts[part];
                GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, model.File)));
                var frames = ModelMesh.MaterialsOf(file, model).Where(m => file.Materials[m].Image >= 0).Select(m =>
                {
                    (int width, int height) = ModelMesh.SheetSize(file, file.Materials[m]);
                    return ModelFrame.Of(file.Materials[m], ModelMesh.UvsOf(file, model, m), sheet, width, height);
                });
                Assert.Contains(frames, f => f != null && f.State == state && f.Dir == dir);
            }
    }

    [Fact]
    public void ASuitStorageUnitsOverlayPartsAreFramesOfItsOverlayStates()
    {
        // The owner's report (2026-10-19): the UV cycle did not show. The unit's body is a live frame of "classic" (tg's picture with its overlays: the red
        // lights, the UV); its status lights, stored suit, helmet and storage are frames of tg's overlay states, drawn only while it has them (overlayParts),
        // so the closed unit's green lights no longer cover the cycle's red ones.
        string stem = Path.Combine(Root, "..", "tg", "icons", "obj", "machines", "suit_storage");
        if (!File.Exists(stem + ".json"))
            return; // the converted icons are not in this checkout
        // The game draws the flat-coloured v3 unit now (the owner, 2026-10-27); the atlas model and its entry keep the mechanism's check.
        ModelEntry unit = ModelCatalog.Parse("""
            { "models": [ { "id": "suit_storage", "icons": [ "icons/obj/machines/suit_storage.dmi" ], "states": [ "classic" ], "rule": "single",
              "overlayParts": true, "texture": "model", "parts": { "model": "station_props/machines/suit_storage_classic.glb" } } ] }
            """).Entries.Single();
        Assert.True(unit.OverlayParts);
        ModelPart model = unit.Parts["model"];
        GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, model.File)));
        var sheet = Anomaly.Client.Shared.Icons.IconSheet.Parse(File.ReadAllText(stem + ".json"), null);
        var states = new HashSet<string>();
        foreach (int m in ModelMesh.MaterialsOf(file, model).Where(m => file.Materials[m].Image >= 0))
        {
            (int width, int height) = ModelMesh.SheetSize(file, file.Materials[m]);
            ModelFrame frame = ModelFrame.Of(file.Materials[m], ModelMesh.UvsOf(file, model, m), sheet, width, height);
            Assert.NotNull(frame);
            states.Add(frame.State);
        }
        Assert.Contains("classic", states);
        Assert.Contains("classic_lights_closed", states);
        Assert.Contains("classic_suit", states);
        Assert.Contains("classic_helm", states);
        Assert.False(ModelFrame.Follows("classic_lights_closed", "classic")); // not the body's state: an overlay's part
    }

    [Fact]
    public void ASupermattersPartsFollowItsStateAndOverlays()
    {
        // supermatter.dm: update_icon's sm-glow is its own entry (the same crystal playing sm-glow's frames); update_overlays' sm-psy swaps the crystal for the
        // reddened one; causality_field (the final countdown), holiday_lights and santa_hat add their nodes.
        ModelEntry sm = Catalog.Find("icons/obj/machines/engine/supermatter.dmi", "sm");
        ModelEntry glow = Catalog.Find("icons/obj/machines/engine/supermatter.dmi", "sm-glow");
        string[] Drawn(ModelEntry e, params string[] overlays)
            => ModelRules.Choose(e, "", 2, 0).Select(p => e.Parts[p.Part]).Where(p => p.DrawnWith(overlays)).Select(p => p.Node).ToArray();
        Assert.Equal(new[] { "Supermatter_Base", "Supermatter_Crystal" }, Drawn(sm, "sm-emissive"));
        Assert.Equal(new[] { "Supermatter_Base", "Supermatter_Crystal" }, Drawn(glow, "sm-emissive"));
        Assert.Equal(new[] { "Supermatter_Base", "Supermatter_CrystalPsy", "Supermatter_CausalityField" }, Drawn(glow, "sm-psy", "causality_field"));
        Assert.Equal(new[] { "Supermatter_Base", "Supermatter_CrystalPsy", "Supermatter_HolidayLights", "Supermatter_HolidayLightsBlink", "Supermatter_SantaHat" },
            Drawn(sm, "sm-psy", "holiday_lights", "santa_hat"));
        // The crystal and the field are drawn by their own shaders, playing tg's frames: sm-emissive's three (0.7, 0.5 and 1 deciseconds), sm-glow's four
        // (1, 0.5, 0.1, 1) each with its colour, causality_field's four (10, 1, 1, 1); half the holiday bulbs go out for holiday_lights_e's 1-decisecond frame.
        Assert.Equal(ModelPart.CrystalShader, sm.Parts["crystal"].Shader);
        Assert.Equal(new[] { 0.07f, 0.05f, 0.1f }, sm.Parts["crystal"].Pulse.Select(f => f.Seconds));
        Assert.Equal(new[] { 0.1f, 0.05f, 0.01f, 0.1f }, glow.Parts["crystal"].Pulse.Select(f => f.Seconds));
        Assert.True(glow.Parts["crystal"].Pulse[2].Scale.Y < glow.Parts["crystal"].Pulse[0].Scale.Y); // the hottest frame is the reddest
        Assert.Equal(ModelPart.FieldShader, sm.Parts["field"].Shader);
        Assert.Equal(new[] { 1f, 0.1f, 0.1f, 0.1f }, sm.Parts["field"].Pulse.Select(f => f.Seconds));
        Assert.False(sm.Parts["lightsBlink"].LitAt(0.95));
        Assert.True(sm.Parts["lightsBlink"].LitAt(0.5));
        Assert.NotNull(glow.Parts["crystal"].Halo);
    }

    [Fact]
    public void AnOutlinesHullHasItsCornersJoined()
    {
        // two flat faces meeting on an edge, their corners split (as the flat-shaded crystal is): the shared corners take the mean of both normals, the others keep theirs
        Vector3[] positions = { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 0), new(0, 1, 0), new(0, 0, 1) };
        Vector3[] normals = { Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX };
        Vector3[] smooth = ModelMesh.SmoothNormals(positions, normals);
        Vector3 mean = Vector3.Normalize(new Vector3(1, 0, 1));
        Assert.Equal(mean, smooth[0]);
        Assert.Equal(mean, smooth[3]);
        Assert.Equal(mean, smooth[2]);
        Assert.Equal(Vector3.UnitZ, smooth[1]);
        Assert.Equal(Vector3.UnitX, smooth[5]);
    }

    [Fact]
    public void AModelShowsItsFirstRaysAndOutline()
    {
        // _sm_delam.dm:115 adds the rays; the singularity strategy adds an outline of size 1 (common_delams.dm:37)
        var blur = new Anomaly.Protocol.FilterEntry { Kind = Anomaly.Protocol.FilterKind.Blur };
        var rays = new Anomaly.Protocol.FilterEntry { Kind = Anomaly.Protocol.FilterKind.Rays, Size = 20f };
        var outline = new Anomaly.Protocol.FilterEntry { Kind = Anomaly.Protocol.FilterKind.Outline, Size = 1f };
        Assert.False(ModelFilters.Any(new[] { blur }));
        Assert.False(ModelFilters.Any(null));
        Assert.Same(rays, ModelFilters.Rays(new[] { blur, rays, new Anomaly.Protocol.FilterEntry { Kind = Anomaly.Protocol.FilterKind.Rays } }));
        Assert.Same(outline, ModelFilters.Outline(new[] { rays, outline }));
        Assert.Equal(0.0625f, ModelFilters.OutlineWidth(outline)); // one pixel of the 2 m, 32-pixel tile
    }

    [Theory]
    [InlineData("\"shader\": \"glass\"")]
    [InlineData("\"shader\": \"crystal\", \"pulse\": [ [ 0, 1 ] ]")]
    [InlineData("\"shader\": \"crystal\", \"pulse\": [ [ 0.1, 1, 1 ] ]")]
    [InlineData("\"shader\": \"crystal\", \"halo\": \"#zz0000\"")]
    public void ABadShaderPartIsRefused(string spec)
    {
        string json = "{ \"models\": [ { \"id\": \"x\", \"icons\": [ \"a.dmi\" ], \"rule\": \"single\", \"texture\": \"model\", "
            + "\"source\": { \"width\": 1, \"height\": 1 }, \"fit\": { \"width\": \"tile\", \"height\": 1 }, "
            + "\"parts\": { \"model\": { \"file\": \"x.glb\", " + spec + " } } } ] }";
        Assert.Throws<InvalidDataException>(() => ModelCatalog.Parse(json));
    }

    [Fact]
    public void ACanistersPartsFollowItsOverlays()
    {
        // canister.dm update_overlays: can-open while it holds a tank (the door stands open, the tank in the pocket), can-0 .. can-3 by its pressure (one to four
        // lights of the level bar; none under 10 kPa).
        ModelEntry canister = Catalog.Entries.Single(e => e.Id == "canister_double_stripe");
        string[] Drawn(params string[] overlays)
            => ModelRules.Choose(canister, "", 2, 0).Select(p => p.Part).Where(p => canister.Parts[p].DrawnWith(overlays)).ToArray();
        Assert.Equal("model", ModelRules.Choose(canister, "", 2, 0)[0].Part);
        Assert.Equal(new[] { "model", "valve", "door", "window", "hatch", "portOff" }, Drawn());
        Assert.Equal(new[] { "model", "valve", "doorOpen", "tank", "window", "hatch", "level0", "level1", "level2", "portOff" }, Drawn("can-open", "can-2"));
        Assert.Contains("level3", Drawn("can-3"));
        Assert.DoesNotContain("level1", Drawn("can-0"));
        // The port light: red off a connector port, green on one (can-connector, tg's connected_port).
        Assert.Equal(new[] { "model", "valve", "door", "window", "hatch", "portOn" }, Drawn("can-connector"));
        // can-0 (10 kPa to 5 atm) is tg's only animated light: two frames of 2 deciseconds, the second empty; the steady first light is can-1 .. can-3's.
        Assert.Equal(new[] { "model", "valve", "door", "window", "hatch", "level0Blink", "portOff" }, Drawn("can-0"));
        // The open panel (cell_hatch) swings the hatch down onto the cell; the powered shielding is a shell whose bands flicker.
        Assert.Equal(new[] { "model", "valve", "door", "window", "hatchOpen", "cell", "shield", "shieldBands", "portOff" }, Drawn("cell_hatch", "shielding"));
        Assert.True(canister.Parts["shieldBands"].BlinkOn > 0f);
        // The held tank takes its own sprite's colour (DV73), the window the gases' inside it (update_window).
        Assert.Equal("icons/obj/canisters.dmi", canister.Parts["tank"].TintIcon);
        Assert.Equal("window-base", canister.Parts["window"].TintVisuals);
        ModelPart blink = canister.Parts["level0Blink"];
        Assert.Equal(canister.Parts["level0"].Node, blink.Node);
        Assert.True(blink.LitAt(0.1));
        Assert.False(blink.LitAt(0.3));
        Assert.True(blink.LitAt(4.05));
        Assert.True(canister.Parts["level0"].LitAt(0.3)); // steady
    }

    [Fact]
    public void AHeldTanksColourIsItsSpritesMainColour()
    {
        // DV73: the 3D held tank takes its tg sprite's coloured pixels' mean (the oxygen tank's blue, the plasma one's orange), not the grey valve's.
        string root = Path.Combine(Root, "..", "tg");
        if (!File.Exists(Path.Combine(root, "icons", "obj", "canisters.png")))
            return; // the converted icons are not in this checkout
        var icons = new Anomaly.Client.Shared.Icons.IconLibrary(new Anomaly.Client.Shared.Icons.DirectoryIconFiles(root));
        Assert.True(icons.TryGetIcon("icons/obj/canisters.dmi", out Anomaly.Client.Shared.Icons.IIcon tanks));
        Assert.True(tanks.TryGetState("oxygen", out Anomaly.Client.Shared.Icons.IIconState oxygen));
        Assert.True(tanks.TryGetState("plasma", out Anomaly.Client.Shared.Icons.IIconState plasma));
        (float r, float g, float b, float coverage) = Anomaly.Client.Shared.Icons.SpriteColors.Mean(oxygen, dominant: true);
        Assert.True(b > r && b > g && coverage > 0f, $"oxygen {r} {g} {b}");
        (r, g, b, _) = Anomaly.Client.Shared.Icons.SpriteColors.Mean(plasma, dominant: true);
        Assert.True(r > b, $"plasma {r} {g} {b}");
    }

    [Fact]
    public void ACanistersShellAndStripesTakeItsGagsColours()
    {
        const string oxygen = "gags:/datum/greyscale_config/canister/stripe|#2786e5#e8fefe";
        Assert.Equal(0x2786E5FFu, ModelColors.Swatch("Canister_Flat_Gags1", System.Numerics.Vector4.One, oxygen));
        Assert.Equal(0xE8FEFEFFu, ModelColors.Swatch("Canister_Flat_Gags2", System.Numerics.Vector4.One, oxygen));
        Assert.False(ModelColors.TryGags("Canister_Flat_Gags3", oxygen, out _));
        Assert.False(ModelColors.TryGags("Canister_Flat_Gags1", "icons/obj/chairs.dmi", out _));
        // A Flat material is its authored sRGB colour (glTF's linear factor converted back); another keeps the linear factor (the doors' edges, the glass).
        var linear = new System.Numerics.Vector4(0.10224173f, 0.09305896f, 0.08650046f, 1f); // sRGB #5a5653
        Assert.Equal(0x5A5653FFu, ModelColors.Swatch("Canister_Flat_Dark", linear, oxygen));
        Assert.Equal(0x1A1816FFu, ModelColors.Swatch("Door_Dark_Edges", linear, null));
        // A Tint_ material is blended toward the part's tint by the tint's alpha: wholly (a held tank), half (a thin gas in the window), not at all.
        var white = new System.Numerics.Vector4(1f, 1f, 1f, 1f);
        Assert.Equal(0x2786E5FFu, ModelColors.Swatch("Canister_Flat_Tint_Tank", white, null, 0x2786E5FFu));
        Assert.Equal(0x937FF2FFu, ModelColors.Swatch("Canister_Flat_Tint_Window", white, null, 0x2700E580u));
        Assert.Equal(0xFFFFFFFFu, ModelColors.Swatch("Canister_Flat_Tint_Window", white, null, 0x2700E500u));
        Assert.Equal(0xFFFFFFFFu, ModelColors.Swatch("Canister_Flat_Dark", white, null, 0x2700E5FFu)); // not a Tint_ material
        // Pictures over each other (the gases in the window): nothing is clear; one at full opacity is its colour; a thin one over nothing keeps its alpha.
        Assert.Equal(0u, ModelColors.Blend(Array.Empty<(float, float, float, float)>()) & 0xFF);
        Assert.Equal(0xFF0000FFu, ModelColors.Blend(new[] { (0f, 0f, 1f, 0.5f), (1f, 0f, 0f, 1f) }));
        Assert.Equal(0x7Fu, ModelColors.Blend(new[] { (1f, 1f, 1f, 0.5f) }) & 0xFF);
        // Every pattern's file has the two GAGS materials its shell and stripes are drawn in (the plain one only the shell's).
        foreach (string id in new[] { "canister_stripe", "canister_double_stripe", "canister_hazard" })
        {
            GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, Catalog.Entries.Single(e => e.Id == id).Parts["model"].File)));
            Assert.Contains(file.Materials, m => m.Name == "Canister_Flat_Gags1");
            Assert.Contains(file.Materials, m => m.Name == "Canister_Flat_Gags2");
        }
    }

    [Theory]
    [InlineData("cabinet_wood", 2)]
    [InlineData("gun_case", 2)]
    [InlineData("box_cardboard", 4)]
    [InlineData("locker_old", 1)]
    public void AClosetsLeavesSwingOnTheirOwnHinges(string id, int fronts)
    {
        // The other storage of closet.dmi: each leaf (a cabinet's two doors, a box's flaps) is a part built about its own hinge, which swings it its own way.
        ModelEntry closet = Catalog.Entries.Single(e => e.Id == id);
        Assert.Equal(ModelRule.Closet, closet.Rule);
        string[] leaves = closet.Parts.Keys.Where(k => k.StartsWith(ModelRules.DoorFront, StringComparison.Ordinal)).ToArray();
        Assert.Equal(fronts, leaves.Length);
        Bounds(Build(id, ModelRules.Body));
        foreach (string leaf in closet.Parts.Keys.Where(k => k != ModelRules.Body))
        {
            ModelMeshData mesh = Build(id, leaf);
            Assert.NotEqual(Vector3.Zero, mesh.Pivot);
            Assert.NotEqual(0f, closet.Parts[leaf].Degrees);
        }
        if (fronts == 2 && id != "box_metal")
            Assert.True(closet.Parts["doorFront"].Degrees * closet.Parts["doorFront2"].Degrees < 0f, "the two doors open apart");
    }

    [Theory]
    [InlineData("tube", new[] { "tube" }, "light_tube_on")]
    [InlineData("tube", new[] { "tube_flickering" }, "light_tube_on")]
    [InlineData("tube", new[] { "tube_nightshift" }, "light_tube_nightshift")]
    [InlineData("tube", new string[0], "light_tube_off")]
    [InlineData("tube_emergency", new[] { "tube_emergency" }, "light_tube_emergency")]
    [InlineData("tube_emergency", new string[0], "light_tube_off")]
    [InlineData("tube-broken", new string[0], "light_tube_broken")]
    [InlineData("bulb-empty", new string[0], "light_bulb_empty")]
    [InlineData("floor", new[] { "floor" }, "light_floor_on")]
    public void ALampsModelIsPickedByItsStateAndItsGlowOverlay(string state, string[] overlays, string model)
        // light.dm: update_icon_state gives the state, update_overlays the lit lamp's glow overlay (none when off), which picks lit, night or emergency.
        => Assert.Equal(model, Catalog.Find("icons/obj/lighting.dmi", state, overlays)?.Id);

    [Fact]
    public void AChargeStuckToSomethingIsDrawnByAnAttachModel()
    {
        // plastic.dm:150: the overlay plastic-explosive2 on its target is drawn as the charge's model on it (rule attach); the item's own states are not.
        ModelEntry planted = Catalog.Find("icons/obj/weapons/grenade.dmi", "plastic-explosive2");
        Assert.Equal("c4_planted", planted?.Id);
        Assert.Equal(ModelRule.Attach, planted.Rule);
        // DV64: its timer window lights on the blink's bright frame (the second of plastic-explosive2's two).
        Assert.Equal("C4_Display", planted.FlashMaterial);
        Assert.Equal(new[] { 1 }, planted.FlashFrames);
        Assert.Null(Catalog.Find("icons/obj/weapons/grenade.dmi", "plastic-explosive0"));
    }

    [Theory]
    [InlineData("icons/obj/machines/wallmounts.dmi", "alarmp", new[] { "alarm0" }, "air_alarm_normal")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "alarmp", new[] { "alarm2" }, "air_alarm_warning")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "alarmp_remote", new[] { "alarm1" }, "air_alarm_remote_hazard")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "alarmp", new string[0], "air_alarm_unpowered")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "alarm_b1", new string[0], "air_alarm_frame")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "fire0", new[] { "fire_2" }, "fire_alarm_level_2")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "fire0", new[] { "fire_alerting" }, "fire_alarm_alerting")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "fire0", new string[0], "fire_alarm_unpowered")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "fire_b2", new string[0], "fire_alarm_panel_open")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "apc0", new string[0], "apc_closed_base")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "apc0", new[] { "apcox-1", "apco3-2", "apco0-3", "apco1-3", "apco2-3" }, "apc_charged")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "apc0", new[] { "apcox-1", "apco3-0", "apco0-3", "apco1-3", "apco2-3" }, "apc_not_charging")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "apc0", new[] { "apcox-1", "apco3-0" }, "apc_breaker_off")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "apc0", new[] { "apcox-1", "apco3-0", "apco0-0", "apco1-0", "apco2-0" }, "apc_channels_off")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "apc2-b-nocover", new string[0], "apc_broken_removed_cell")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "light-off", new string[0], "light_switch_off")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "newscaster_off", new[] { "newscaster_wanted", "newscaster_alert" }, "newscaster_wanted_alert")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "newscaster_off", new[] { "newscaster_normal", "crack1" }, "newscaster_normal_crack_1")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "newscaster_off", new[] { "newscaster_normal" }, "newscaster_normal")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "newscaster_off", new[] { "crack3" }, "newscaster_off_crack_3")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "req_comp_off", new[] { "req_comp_open" }, "requests_console_panel_open")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "req_comp_off", new[] { "req_comp2" }, "requests_console_high_priority")]
    [InlineData("icons/obj/machines/status_display.dmi", "frame", new[] { "redalert" }, "status_display_redalert")]
    [InlineData("icons/obj/machines/status_display.dmi", "frame", new string[0], "status_display_blank")]
    [InlineData("icons/obj/wallmounts.dmi", "extinguisher_default", new[] { "door" }, "extinguisher_cabinet_default_closed")]
    [InlineData("icons/obj/wallmounts.dmi", "extinguisher_mini", new[] { "door_open" }, "extinguisher_cabinet_mini_open")]
    [InlineData("icons/obj/wallmounts.dmi", "extinguisher", new[] { "door" }, "extinguisher_cabinet_empty_closed")]
    [InlineData("icons/obj/wallmounts.dmi", "extinguisher", new string[0], "extinguisher_cabinet_frame")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "intercom", new[] { "intercom_m", "intercom_s" }, "station_intercom_two_way")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "intercom", new[] { "intercom_s" }, "station_intercom_listening")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "intercom", new string[0], "station_intercom_quiet")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "intercom-p", new string[0], "station_intercom_off")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "button", new[] { "button-light-mask" }, "door_button_normal")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "button-warning-nopower", new string[0], "door_button_warning_nopower")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "button", new[] { "button-light-mask", "button-overlay-success" }, "door_button_success")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "button-warning", new[] { "button-overlay-error" }, "door_button_warning_error")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "button-open", new[] { "button-overlay-board", "button-overlay-device" }, "door_button_open_board_device")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "button-warning-open", new[] { "button-overlay-signaler" }, "door_button_warning_open_signaler")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "button-open", new string[0], "door_button_open_empty")]
    [InlineData("icons/obj/wallmounts.dmi", "mflash1", new string[0], "mounted_flash_ready")]
    [InlineData("icons/obj/wallmounts.dmi", "mflash1-p", new string[0], "mounted_flash_off")]
    [InlineData("icons/obj/machines/sec.dmi", "recharger", new[] { "recharger-charging" }, "weapon_recharger_charging")]
    [InlineData("icons/obj/machines/sec.dmi", "recharger", new[] { "recharger-open" }, "weapon_recharger_open")]
    [InlineData("icons/obj/machines/sec.dmi", "recharger", new string[0], "weapon_recharger_off")]
    [InlineData("icons/obj/machines/cell_charger.dmi", "ccharger", new[] { "ccharger-o3", "cell", "ccharger-standard-on" }, "cell_charger_level_3")]
    [InlineData("icons/obj/machines/cell_charger.dmi", "ccharger", new[] { "hcell" }, "cell_charger_hcell")]
    [InlineData("icons/obj/machines/cell_charger.dmi", "ccharger", new[] { "cell" }, "cell_charger_off")]
    [InlineData("icons/obj/machines/cell_charger.dmi", "ccharger", new string[0], "cell_charger_empty")]
    [InlineData("icons/obj/machines/fax.dmi", "fax", new[] { "contain_photo" }, "fax_loaded_photo")]
    [InlineData("icons/obj/machines/fax.dmi", "fax", new string[0], "fax_idle")]
    [InlineData("icons/obj/machines/microwave.dmi", "back_on", new[] { "door_charge", "mw1" }, "microwave_charging")]
    [InlineData("icons/obj/machines/microwave.dmi", "mw_complete", new string[0], "microwave_idle")]
    [InlineData("icons/obj/machines/microwave.dmi", "engi_back_off", new[] { "engi_cell_25" }, "microwave_engineering_cell_25")]
    [InlineData("icons/obj/machines/microwave.dmi", "engi_back_off", new[] { "engi_cell_100" }, "microwave_engineering_idle")]
    [InlineData("icons/obj/machines/kitchen.dmi", "juicer", new[] { "juicer-beaker", "juicer-on" }, "reagent_grinder_beaker_ready")]
    [InlineData("icons/obj/machines/kitchen.dmi", "juicer", new string[0], "reagent_grinder_empty_off")]
    [InlineData("icons/obj/digital_clock.dmi", "digital_clock_base", new string[0], "digital_clock")]
    [InlineData("icons/obj/medical/chemical_tanks.dmi", "pepper", new string[0], "pepper_spray_refiller")]
    [InlineData("icons/obj/machines/wall_healer.dmi", "wall_healer", new[] { "bar0", "bar5", "bar_shadow" }, "wall_healer")]
    [InlineData("icons/obj/machines/wall_healer.dmi", "wall_healer", new[] { "open", "bar0" }, "wall_healer")]
    [InlineData("icons/obj/machines/wall_healer.dmi", "wall_healer", new string[0], "wall_healer")]
    [InlineData("icons/obj/lighting.dmi", "bulb-construct-stage2", new string[0], "light_bulb_construct_stage2")]
    public void AWallMountsModelIsPickedByItsStateAndItsOverlays(string icon, string state, string[] overlays, string model)
        // _air_alarm.dm, firealarm.dm, camera.dm: update_icon_state gives the state (the build stage with the panel open), update_overlays the light.
        => Assert.Equal(model, Catalog.Find(icon, state, overlays)?.Id);

    [Fact]
    public void AFlicksFramesAreDrawnByTheirOwnModels()
    {
        // flasher.dm:122: flash() flicks mflash_flash, three frames (delays 1, 1, 3): the bulb starts, burns yellow, then white; each frame has its model.
        const string icon = "icons/obj/wallmounts.dmi";
        Assert.Equal("mounted_flash_flash_start", Catalog.FindFlickFrame(icon, "mflash_flash", 0)?.Id);
        Assert.Equal("mounted_flash_flash_white", Catalog.FindFlickFrame(icon, "mflash_flash", 2)?.Id);
        Assert.Equal("mounted_flash_flash_white", Catalog.FindFlickFrame(icon, "mflash_flash", 5)?.Id); // past the last: the last
        Assert.Null(Catalog.FindFlickFrame(icon, "mflash1", 0)); // the thing's own state has no flick models
        Assert.Equal("mounted_flash_ready", Catalog.Find(icon, "mflash1")?.Id);
    }

    [Fact]
    public void FloorGasPumpsAndConnectorsKeepTheirSprites()
    {
        // The owner: floor atmospherics (pumps, connectors) stay 2D, their models were removed.
        Assert.Null(Catalog.Find("icons/obj/machines/atmospherics/binary_devices.dmi", "pump_on-1"));
        Assert.Null(Catalog.Find("icons/obj/machines/atmospherics/unary_devices.dmi", "connector"));
    }

    [Theory]
    [InlineData("icons/obj/machines/camera.dmi", "camera")]
    [InlineData("icons/obj/machines/camera.dmi", "camera_off")]
    [InlineData("icons/obj/machines/camera.dmi", "xraycamera")]
    [InlineData("icons/obj/machines/camera.dmi", "xraycamera_off")]
    public void CamerasKeepTheirAnimatedSprites(string icon, string state)
    {
        // The owner, 2026-10-23: cameras must remain 2D in every operational state (the vents and scrubbers are models again, 2026-10-29).
        Assert.Null(Catalog.Find(icon, state));
        Assert.Null(Catalog.Find(icon, state, new[] { "broken", "overclocked", "vent_cap", "scrub_cap", "camera_panel", "camera_on", "camera_in_use", "camera_emp", "xraycamera_on", "xraycamera_in_use", "xraycamera_emp" }));
    }

    [Theory]
    [InlineData("vent_off", "vent_pump_off")]
    [InlineData("vent_out", "vent_pump_out")]
    [InlineData("vent_welded", "vent_pump_welded")]
    [InlineData("scrub_on", "vent_scrubber_on")]
    [InlineData("scrub_purge", "vent_scrubber_purge")]
    public void VentsAndScrubbersLieOnTheFloorTurnedByTheirDir(string state, string id)
    {
        // vent_pump.dm, vent_scrubber.dm update_icon_nopipes: a slab per state; a pump's broken overlay keeps its state's slab
        ModelEntry e = Catalog.Find("icons/obj/machines/atmospherics/unary_devices.dmi", state, new[] { "broken" });
        Assert.Equal(id, e.Id);
        Assert.Equal(ModelRule.Dir4, e.Rule);
        // sunk to the holopad's height above the floor (the owner, 2026-10-29)
        ModelEntry holopad = Catalog.Find("icons/obj/machines/floor.dmi", "holopad0");
        float Top(ModelEntry m) => 0.055f * ModelMesh.Scale(m, 2, 2, .75f).Vertical + m.Offset.Y;
        Assert.InRange(Top(e) - Top(holopad), -0.001f, 0.001f);
    }

    [Theory]
    [InlineData("ai-slipper0", "foam_dispenser")]
    [InlineData("ai-slipper1", "foam_dispenser_ready")]
    public void TheFoamDispenserIsAFloorPlateAtTheHolopadsHeight(string state, string id)
    {
        // ai_slipper.dm: ai-slipper0 without power, empty or cooling down, ai-slipper1 ready; a 1.24 m plate, the sprite's 20 of 32 px of the tile
        ModelEntry e = Catalog.Find("icons/obj/devices/tool.dmi", state);
        Assert.Equal(id, e.Id);
        Assert.Equal(1f, ModelMesh.Scale(e, 2, 2, .75f).Vertical);
        ModelEntry holopad = Catalog.Find("icons/obj/machines/floor.dmi", "holopad0");
        float Top(ModelEntry m) => 0.055f * ModelMesh.Scale(m, 2, 2, .75f).Vertical + m.Offset.Y;
        Assert.InRange(Top(e) - Top(holopad), -0.001f, 0.001f);
    }

    [Fact]
    public void ADir8ModelTakesItsCornerForADiagonalDir()
    {
        // A conveyor or a railing: the straight module for a cardinal dir (south unturned), the corner (made for southeast) for a diagonal one.
        ModelEntry railing = Catalog.Entries.Single(e => e.Id == "railing");
        Assert.Equal(ModelRule.Dir8, railing.Rule);
        ModelPlacement south = ModelRules.Choose(railing, "railing", 2, 0)[0], southEast = ModelRules.Choose(railing, "railing", 6, 0)[0];
        Assert.Equal(("model", 0f), (south.Part, south.Yaw));
        Assert.Equal(("corner", 0f), (southEast.Part, southEast.Yaw));
        ModelPlacement southWest = ModelRules.Choose(railing, "railing", 10, 0)[0];
        Assert.Equal("corner", southWest.Part);
        Assert.Equal(-MathF.PI / 2f, MathF.IEEERemainder(southWest.Yaw, 2f * MathF.PI), 4); // a quarter turn clockwise from above: east goes south, south goes west
        ModelEntry lamp = Catalog.Entries.Single(e => e.Id == "light_tube_on");
        Assert.Equal(ModelRule.Wall, lamp.Rule);
        Assert.Equal(0f, ModelRules.Choose(lamp, "tube", 0, 1)[0].Yaw, 4); // on the north wall unturned
    }

    [Fact]
    public void ASingleModelTurnsByItsDir()
    {
        // A canister's dir is the server's (its "dir" rule, DV56): the dev rotate turns it, so its model turns too; so does any other single model's
        // (the owner, 2026-10-20: a microwave on a table could not be turned).
        ModelEntry canister = Catalog.Entries.First(e => e.Id.StartsWith("canister_", StringComparison.Ordinal));
        Assert.Equal(ModelRule.Single, canister.Rule);
        Assert.Equal(0f, ModelRules.Choose(canister, "", 2, 0)[0].Yaw, 4);
        Assert.NotEqual(0f, ModelRules.Choose(canister, "", 4, 0)[0].Yaw);
        Assert.Equal(MathF.PI, MathF.Abs(ModelRules.Choose(canister, "", 1, 0)[0].Yaw), 4);
        ModelEntry holopad = Catalog.Entries.Single(e => e.Id == "holopad");
        Assert.Equal(0f, ModelRules.Choose(holopad, "holopad0", 2, 0)[0].Yaw, 4);
        Assert.Equal(MathF.PI, MathF.Abs(ModelRules.Choose(holopad, "holopad0", 1, 0)[0].Yaw), 4);
    }

    [Theory]
    [InlineData("snack", "snack", true)]
    [InlineData("snack", "snack-off", true)]
    [InlineData("snack", "snack-broken", true)]
    [InlineData("snack", "snack-deny", true)]
    [InlineData("mirror", "mirror_broke", true)]
    [InlineData("snack", "snackblue", false)]
    [InlineData("toilet00", "toilet01", false)]
    [InlineData("holopad0", "holopad1", false)]
    public void ALiveFrameFollowsItsStateAndItsVariants(string authored, string shown, bool follows)
        => Assert.Equal(follows, ModelFrame.Follows(authored, shown));

    [Fact]
    public void AModelWithSeveralMaterialsIsDrawnAsOnePiecePerMaterial()
    {
        // The rounded tank keeps its shell, feet, rim, gas, glass and glint as independent materials.
        ModelEntry tank = Catalog.Entries.Single(e => e.Id == "stationary_tank");
        GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, tank.Parts["end"].File)));
        int[] materials = ModelMesh.MaterialsOf(file, tank.Parts["end"]);
        Assert.Equal(6, materials.Length);
        Assert.All(materials, m => Assert.True(file.Materials[m].Image < 0, file.Materials[m].Name));
        Assert.Contains(file.Materials, m => m.Name.Contains("Tint_Gas", StringComparison.Ordinal));
        Assert.Contains(file.Materials, m => m.Blend);
        // A plain glass colour (no texture) is a swatch: the solar panel's cover has one.
        GlbFile old = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, "station_props/power/solar_panel.glb")));
        Assert.Contains(old.Materials, m => m.Blend && m.Image < 0);
    }

    [Fact]
    public void ATanksModulesReachTheTilesEdgeOnTheSidesTheyJoin()
    {
        // The rounded end joins east exactly at the tile edge. Exterior portholes stay within the tile.
        ModelEntry tank = Catalog.Entries.Single(e => e.Id == "stationary_tank");
        ModelPlacement end = ModelRules.Choose(tank, "canister-4", 0, 0)[0]; // tg's EAST
        Assert.Equal(ModelRules.End, end.Part);
        Assert.Equal(0f, end.Yaw, 4);
        (Vector3 min, Vector3 max) = Bounds(Build("stationary_tank", ModelRules.End));
        Assert.Equal(Tile / 2f, max.X, 3);
        Assert.InRange(min.X, -.955f, -.950f);
        (Vector3 cmin, Vector3 cmax) = Bounds(Build("stationary_tank", ModelRules.Center));
        Assert.Equal(Tile / 2f, cmax.X, 3);
        Assert.Equal(-Tile / 2f, cmin.Z, 3);
    }

    [Fact]
    public void ThingsOnARackRestOnItsSlats()
    {
        // tg's rack is a low box (10 sprite pixels) with a grating of 1 px slats and higher rails on top: things rest on the slats, between the rails.
        ModelEntry rack = Catalog.Entries.Single(e => e.Id == "storage_rack");
        Assert.True(rack.RestOnShelves);
        ModelMeshData mesh = Build("storage_rack", "model");
        float slats = 11 * Tile / 32f;
        Assert.Contains(mesh.Positions, p => MathF.Abs(p.Y - slats) < 0.001f);
        Assert.All(rack.Slots, s => Assert.Equal(slats, s.Y, 3));
        Assert.All(rack.Slots, s => Assert.InRange(MathF.Abs(s.X), 0f, 0.5f - 1.5f * Tile / 32f)); // inside the rails (8 px from the middle)
    }

    [Fact]
    public void EveryCanisterVariantIsATgGagsKey()
    {
        // canister.dm: /datum/greyscale_config/canister (or its double and stripe) with greyscale_colors; the client's icon is "gags:<config>|<colours>".
        // One entry per config draws every colour of it (a gas's, a repainted canister's).
        string[] keys = Catalog.Entries.Where(e => e.Id.StartsWith("canister_", StringComparison.Ordinal)).SelectMany(e => e.Icons).ToArray();
        Assert.Equal(4, keys.Length);
        Assert.All(keys, k => Assert.Matches(@"^gags:/datum/greyscale_config/canister(/[a-z_]+)?\|\*$", k));
    }

    [Fact]
    public void AnAirlockDrawsEveryPartTurnedToItsFace()
    {
        ModelEntry hatch = Catalog.Entries.Single(e => e.Id == "hatch_airlock");
        ModelPlacement[] placed = ModelRules.Choose(hatch, "closed", 0, 4);
        Assert.Equal(new[] { "upperLeft", "upperRight", "lowerLeft", "lowerRight" }, placed.Select(p => p.Part));
        Assert.All(placed, p => Assert.Equal(placed[0].Yaw, p.Yaw));
    }

    [Theory]
    [InlineData("crate_metal_crate", "crate", new string[0], false)]
    [InlineData("crate_metal_crate", "securecrateopen", new string[0], true)]
    [InlineData("crate_cardboard_crate", "cardboard", new string[0], false)]
    [InlineData("crate_cardboard_crate", "cardboard", new[] { "cardboardopen" }, true)]
    [InlineData("crate_animal_crate", "crittercrate", new[] { "crittercrate_door" }, false)]
    [InlineData("crate_animal_crate", "crittercrate", new[] { "crittercrate_door_open" }, true)]
    [InlineData("crate_mail_crate", "mailsealed", new string[0], false)]
    [InlineData("crate_mail_crate", "mail", new string[0], true)]
    [InlineData("crate_mail_crate", "mailopen", new string[0], true)]
    public void ACratesOpenIsReadAsTgSetsIt(string id, string state, string[] overlays, bool open)
        => Assert.Equal(open, CrateLook.Open(Catalog.Entries.Single(e => e.Id == id), state, overlays));

    [Fact]
    public void ACratesPartsShowItsClosedOpenOrNamedState()
    {
        ModelEntry metal = Catalog.Entries.Single(e => e.Id == "crate_metal_crate");
        ModelPart body = metal.Parts[ModelRules.Body], inside = metal.Parts[ModelRules.Inside];
        // Shut: the body is the composed crate (null), the inside its open state; open: the closed and the open states.
        Assert.Null(CrateLook.PictureState(metal, body, "securecrate", open: false, shut: true));
        Assert.Equal("securecrateopen", CrateLook.PictureState(metal, inside, "securecrate", open: false, shut: true));
        Assert.Equal("securecrate", CrateLook.PictureState(metal, body, "securecrateopen", open: true, shut: false));
        Assert.Equal("securecrateopen", CrateLook.PictureState(metal, inside, "securecrateopen", open: true, shut: false));
        ModelEntry mail = Catalog.Entries.Single(e => e.Id == "crate_mail_crate");
        Assert.Equal("mailsealed", CrateLook.PictureState(mail, mail.Parts[ModelRules.Body], "mail", open: true, shut: false));
        Assert.Equal("mail", CrateLook.PictureState(mail, mail.Parts[ModelRules.Inside], "mail", open: true, shut: false));
        ModelEntry box = Catalog.Entries.Single(e => e.Id == "crate_cardboard_crate");
        Assert.Equal("cardboardopen", CrateLook.PictureState(box, box.Parts[ModelRules.Inside], "cardboard_moth", open: true, shut: false));
    }

    [Fact]
    public void ACratesLidIsBuiltAboutItsHingeAndSwingsUp()
    {
        ModelEntry metal = Catalog.Entries.Single(e => e.Id == "crate_metal_crate");
        ModelMeshData lid = Build("crate_metal_crate", "lid"), body = Build("crate_metal_crate", ModelRules.Body);
        // The hinge on the back top edge, the lid reaching forward (south, +Z) from it.
        Assert.Equal(Bounds(body).Min.Z, lid.Pivot.Z, 2);
        Assert.InRange(lid.Pivot.Y, Bounds(body).Max.Y - 0.01f, Bounds(body).Max.Y + 0.01f);
        Assert.InRange(Bounds(lid).Min.Z, -0.01f, 0.01f);
        // Swung by its degrees about its axis, its front edge rises above the hinge.
        ModelPart spec = metal.Parts["lid"];
        var turn = Matrix4x4.CreateFromAxisAngle(spec.Axis, spec.Degrees * MathF.PI / 180f);
        Vector3 front = Vector3.Transform(new Vector3(0f, 0f, Bounds(lid).Max.Z), turn);
        Assert.True(front.Y > 0.5f, $"the open lid's front edge at {front}");
    }

    [Theory]
    [InlineData("crate_metal_crate")]
    [InlineData("crate_ancient_sarcophagus")]
    [InlineData("crate_cardboard_crate")]
    public void ThingsInAnOpenCrateLieOnItsFloor(string id)
    {
        ModelMeshData inside = Build(id, ModelRules.Inside), body = Build(id, ModelRules.Body);
        Vector3[] slots = ModelMesh.Floor(inside);
        Assert.NotEmpty(slots);
        Assert.All(slots, s =>
        {
            Assert.InRange(s.Y, 0.01f, 0.2f); // on the floor of the crate, not on its rim
            Assert.InRange(s.X, Bounds(body).Min.X, Bounds(body).Max.X);
            Assert.InRange(s.Z, Bounds(body).Min.Z, Bounds(body).Max.Z);
        });
    }

    [Fact]
    public void ABodyLiesOnTheModelsTopOverTheTileCentreNotItsHighestPart()
    {
        // a mattress 0.5 m up over the whole tile and a headrest 0.8 m up at its north end, as a bed's (DV127)
        static (Vector3[], int[]) Quad(float x0, float z0, float x1, float z1, float y) =>
            (new[] { new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1) }, new[] { 0, 1, 2, 0, 2, 3 });
        (Vector3[] mp, int[] mi) = Quad(-1f, -1f, 1f, 1f, 0.5f);
        (Vector3[] hp, int[] hi) = Quad(-1f, -1f, 1f, -0.6f, 0.8f);
        var bed = new ModelMeshData { Positions = mp.Concat(hp).ToArray(), Indices = mi.Concat(hi.Select(k => k + 4)).ToArray() };
        Assert.Equal(0.5f, ModelMesh.TopAtCentre(bed), 3);
        bed.Pivot = new Vector3(0f, 0.1f, 0f);
        Assert.Equal(0.6f, ModelMesh.TopAtCentre(bed), 3);
        Assert.True(float.IsNaN(ModelMesh.TopAtCentre(new ModelMeshData { Positions = hp, Indices = hi })));
    }
}
