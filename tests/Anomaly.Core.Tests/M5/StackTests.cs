using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 2: tg's stacks (game/objects/items/stacks/stack.dm, stack_recipe.dm, sheets, rods, tiles, glass.dm) and materials
/// (atom_materials.dm, SSmaterials, the material datums) in a booted toy room: merging, splitting and using, make_item with its
/// do_after and building_checks, the recipes' results and amounts (4 floor tiles and 2 rods a sheet), the materials of a built
/// stack against a spawned one (tg's crafting test's parity), the stack icon states by amount, tg's stack_singular_name test,
/// the StackCrafting window's data and its "make" action, and the glass shard's caltrop.
/// </summary>
public class StackTests
{
    private const int Second = 20;

    private static T New<T>(AudioRoom r, int x, int y, float amount) where T : obj_item_stack
        => r.In(() => (T)Dm.@new(typeof(T), locate(x, y, 1), amount));

    private static List<T> On<T>(AudioRoom r, int x, int y) => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().ToList());

    private static float Amount(AudioRoom r, obj_item_stack s) => r.In(() => Dm.num(s.amount));

    // A recipe of a stack recipe list by its title (nested lists searched as tg's is_valid_recipe does).
    private static datum_stack_recipe Recipe(DmList recipes, string title)
    {
        foreach (object entry in Dm.each(recipes))
        {
            if (entry is datum_stack_recipe recipe && recipe.title == title)
                return recipe;
            if (entry is datum_stack_recipe_list list && Recipe(list.recipes, title) is { } nested)
                return nested;
        }
        return null;
    }

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, bob);
    }

    private static T InHand<T>(AudioRoom r, mob who, float amount) where T : obj_item_stack
    {
        T stack = r.In(() => (T)Dm.@new(typeof(T), who.loc, amount, FALSE));
        r.In(() => Waiting.NoSleep(who.put_in_hands(stack, merge_stacks: FALSE)));
        Assert.True(r.In(() => Dm.truthy(who.is_holding(stack))), "the stack is held");
        return stack;
    }

    [Fact]
    public void StacksOnOneTileMergeAtInitAndSplitAndUseKeepTheirAmounts()
    {
        var r = new AudioRoom(10, 10, seed: 5201);
        var first = New<obj_item_stack_sheet_iron>(r, 5, 5, 10);
        var second = New<obj_item_stack_sheet_iron>(r, 5, 5, 20);
        r.Step();
        // stack.dm:107 LateInitialize -> merge_with_loc: the second stack merges into the first and is deleted
        var irons = On<obj_item_stack_sheet_iron>(r, 5, 5);
        Assert.Single(irons);
        Assert.Equal(30f, Amount(r, irons[0]));
        Assert.True(r.In(() => QDELETED(second)), "the merged stack is gone");

        // over max_amount at init: the excess makes new full stacks (stack.dm:73-75)
        var big = New<obj_item_stack_sheet_glass>(r, 6, 6, 120);
        r.Step();
        var glasses = On<obj_item_stack_sheet_glass>(r, 6, 6);
        Assert.Equal(120f, glasses.Sum(g => Amount(r, g)));
        Assert.All(glasses, g => Assert.True(Amount(r, g) <= 50));

        // split_stack: a new stack of the amount, the rest stays (stack.dm:781)
        obj_item_stack piece = r.In(() => Waiting.NoSleep(first.split_stack(7)) as obj_item_stack);
        Assert.NotNull(piece);
        Assert.Equal(7f, Amount(r, piece));
        Assert.Equal(23f, Amount(r, first));
        Assert.Same(r.In(() => first.mats_per_unit), r.In(() => piece.mats_per_unit)); // the cached material set is shared

        // use: an amount larger than the stack fails, the exact amount deletes it (stack.dm:586, is_zero_amount)
        Assert.Equal(FALSE, Dm.num(r.In(() => Waiting.NoSleep(first.use(24)))));
        Assert.Equal(TRUE, Dm.num(r.In(() => Waiting.NoSleep(first.use(23)))));
        Assert.True(r.In(() => QDELETED(first)), "a used up stack is deleted");

        // merge into a stack: the limit of max_amount (stack.dm:696)
        var a = New<obj_item_stack_rods>(r, 2, 2, 45);
        var b = New<obj_item_stack_rods>(r, 3, 3, 10);
        r.Step();
        object moved = r.In(() => Waiting.NoSleep(b.merge(a)));
        Assert.Equal(5f, Dm.num(moved));
        Assert.Equal(50f, Amount(r, a));
        Assert.Equal(5f, Amount(r, b));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStackIconStateFollowsTheAmountAndTheMaterialsFollowTheUnits()
    {
        var r = new AudioRoom(10, 10, seed: 5202);
        var iron = New<obj_item_stack_sheet_iron>(r, 5, 5, 5);
        r.Step();
        // stack.dm:225: a third or less is the base state, two thirds "_2", more "_3"
        Assert.Equal("sheet-metal", r.In(() => iron.icon_state));
        r.In(() => Waiting.NoSleep(iron.add(20)));
        Assert.Equal("sheet-metal_2", r.In(() => iron.icon_state));
        r.In(() => Waiting.NoSleep(iron.add(25)));
        Assert.Equal("sheet-metal_3", r.In(() => iron.icon_state));
        // the weight class follows too (get_weight_from_size): a full stack is full_w_class
        Assert.Equal(WEIGHT_CLASS_NORMAL, r.In(() => iron.w_class));
        // custom_materials = mats_per_unit * amount (update_custom_materials, initialize_materials)
        datum_material ironMat = r.In(() => Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_iron))) as datum_material);
        Assert.Equal(50 * SHEET_MATERIAL_AMOUNT, Dm.num(r.In(() => iron.custom_materials[ironMat])));

        // rods show their count up to five (rods.dm:80)
        var rods = New<obj_item_stack_rods>(r, 6, 6, 3);
        r.Step();
        Assert.Equal("rods-3", r.In(() => rods.icon_state));
        r.In(() => Waiting.NoSleep(rods.add(10)));
        Assert.Equal("rods", r.In(() => rods.icon_state));

        // a cable piece and a coil (cable.dm:531-553)
        var cable = New<obj_item_stack_cable_coil>(r, 7, 7, 2);
        r.Step();
        Assert.Equal("coil2", r.In(() => cable.icon_state));
        Assert.Equal("cable piece", r.In(() => cable.name));
        r.In(() => Waiting.NoSleep(cable.add(10)));
        Assert.Equal("coil", r.In(() => cable.icon_state));
        Assert.Equal("cable coil", r.In(() => cable.name));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FloorTilesAndRodsComeFourAndTwoASheetWithTheSheetsMaterials()
    {
        var (r, bob) = Room(5203);
        var iron = InHand<obj_item_stack_sheet_iron>(r, bob, 50);
        datum_stack_recipe tile = r.In(() => Recipe(iron.recipes, "floor tile"));
        datum_stack_recipe rod = r.In(() => Recipe(iron.recipes, "iron rod"));
        Assert.NotNull(tile);
        Assert.NotNull(rod);
        Assert.Equal((1f, 4f, 20f), (tile.req_amount, tile.res_amount, tile.max_res_amount));
        Assert.Equal((1f, 2f, 60f), (rod.req_amount, rod.res_amount, rod.max_res_amount));

        // make_item x5: 5 sheets used, 20 tiles put in the hands (stack.dm:451-453, :508)
        Assert.Equal(TRUE, Dm.num(r.In(() => Waiting.NoSleep(iron.make_item(bob, tile, 5)))));
        Assert.Equal(45f, Amount(r, iron));
        var tiles = r.In(() => Dm.each_cast<obj_item>(bob.held_items).OfType<obj_item_stack_tile_iron_base>().ToList());
        Assert.Single(tiles);
        Assert.Equal(20f, Amount(r, tiles[0]));

        // materials parity (tg's crafting unit test, crafting.dm:139-152): a crafted stack's mats_per_unit against a spawned one
        var spawnedTiles = r.In(() => Dm.@new<obj_item_stack_tile_iron_base>(null, 20, FALSE));
        Assert.Equal(TRUE, Dm.num(r.In(() => tiles[0].compare_materials(spawnedTiles))));
        Assert.True(r.In(() => Dm.equiv(tiles[0].mats_per_unit, spawnedTiles.mats_per_unit)), "the tiles' mats_per_unit");

        // rods: 1 sheet -> 2 rods, with iron's half sheet a rod
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(tiles[0])));
        Assert.Equal(TRUE, Dm.num(r.In(() => Waiting.NoSleep(iron.make_item(bob, rod, 1)))));
        var rods = r.In(() => Dm.each_cast<obj_item>(bob.held_items).OfType<obj_item_stack_rods>().ToList());
        Assert.Single(rods);
        Assert.Equal(2f, Amount(r, rods[0]));
        Assert.Equal(44f, Amount(r, iron));
        var spawnedRods = r.In(() => Dm.@new<obj_item_stack_rods>(null, 2, FALSE));
        Assert.Equal(TRUE, Dm.num(r.In(() => rods[0].compare_materials(spawnedRods))));
        datum_material ironMat = r.In(() => Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_iron))) as datum_material);
        Assert.Equal(HALF_SHEET_MATERIAL_AMOUNT, Dm.num(r.In(() => rods[0].mats_per_unit[ironMat])));

        r.AssertNoRuntimes();
    }

    /// <summary>
    /// DEVIATION(DV65): a recipe is built where the builder's crosshair aims (the last map thing hovered), when that is the
    /// builder's turf or an open turf next to it the builder reaches; a turf farther away, a wall and nothing aimed build on
    /// the builder's own turf as tg does. The aim survives the pointer leaving the map (the recipe window).
    /// </summary>
    [Fact]
    public void ARecipeIsBuiltWhereTheBuilderAims()
    {
        var (r, bob) = Room(5211);
        var iron = InHand<obj_item_stack_sheet_iron>(r, bob, 30);
        datum_stack_recipe stool = r.In(() => Recipe(iron.recipes, "stool"));
        client player = r.In(() => bob.client);
        Assert.NotNull(player);

        // nothing aimed: tg's own turf
        Assert.Equal(TRUE, Dm.num(r.In(() => Waiting.NoSleep(iron.make_item(bob, stool, 1)))));
        Assert.Single(On<obj_structure_chair_stool>(r, 4, 4));

        // aimed at the floor east of Bob, then the pointer leaves the map: the stool goes east
        r.In(() => ClientSeam.Hover(player, locate(5, 4, 1)));
        r.In(() => ClientSeam.Unhover(player));
        Assert.Same(r.In(() => locate(5, 4, 1)), r.In(() => iron.build_location(bob)));
        Assert.Equal(TRUE, Dm.num(r.In(() => Waiting.NoSleep(iron.make_item(bob, stool, 1)))));
        Assert.Single(On<obj_structure_chair_stool>(r, 5, 4));
        Assert.Single(On<obj_structure_chair_stool>(r, 4, 4));

        // aimed at a thing on the floor north of Bob: its turf
        obj_item_stack_sheet_iron loose = New<obj_item_stack_sheet_iron>(r, 4, 5, 1);
        r.In(() => ClientSeam.Hover(player, loose));
        Assert.Same(r.In(() => locate(4, 5, 1)), r.In(() => iron.build_location(bob)));

        // CRAFT_ONE_PER_TURF and the other checks read the aimed turf: a second stool east is refused
        r.In(() => ClientSeam.Hover(player, locate(5, 4, 1)));
        Assert.Equal(FALSE, Dm.num(r.In(() => iron.building_checks(bob, stool, 1))));

        // too far: the open neighbour toward it
        r.In(() => ClientSeam.Hover(player, locate(7, 4, 1)));
        Assert.Same(r.In(() => locate(5, 4, 1)), r.In(() => iron.build_location(bob)));

        // nothing dense under the builder: a stool and a directional window can (it closes one side), a girder and a fulltile
        // window cannot; aimed at the next tile they can
        var glass = InHand<obj_item_stack_sheet_glass>(r, bob, 20);
        datum_stack_recipe thin = r.In(() => Recipe(glass.recipes, "directional window"));
        datum_stack_recipe full = r.In(() => Recipe(glass.recipes, "fulltile window"));
        datum_stack_recipe girder = r.In(() => Recipe(iron.recipes, "wall girders (anchored)"));
        Assert.False(obj_item_stack.dense_result(stool));
        Assert.False(r.In(() => obj_item_stack.dense_result(thin)));
        Assert.True(r.In(() => obj_item_stack.dense_result(full)));
        Assert.True(r.In(() => obj_item_stack.dense_result(girder)));
        r.In(() => ClientSeam.Hover(player, locate(4, 4, 1)));
        Assert.Equal(FALSE, Dm.num(r.In(() => glass.building_checks(bob, full, 1))));
        Assert.Equal(FALSE, Dm.num(r.In(() => iron.building_checks(bob, girder, 1))));
        Assert.Equal(TRUE, Dm.num(r.In(() => glass.building_checks(bob, thin, 1))));
        r.In(() => ClientSeam.Hover(player, locate(4, 3, 1)));
        Assert.Equal(TRUE, Dm.num(r.In(() => glass.building_checks(bob, full, 1))));

        // a wall: the own turf
        r.In(() => Waiting.NoSleep(locate(3, 4, 1).ChangeTurf(typeof(turf_closed_wall))));
        r.In(() => ClientSeam.Hover(player, locate(3, 4, 1)));
        Assert.Same(r.In(() => locate(4, 4, 1)), r.In(() => iron.build_location(bob)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MakeItemWaitsForItsDoAfterAndChecksTheTile()
    {
        var (r, bob) = Room(5204);
        var iron = InHand<obj_item_stack_sheet_iron>(r, bob, 20);
        datum_stack_recipe girder = r.In(() => Recipe(iron.recipes, "wall girders (anchored)"));
        Assert.Equal(4 * SECONDS, Dm.num(girder.time));
        client player = r.In(() => bob.client);

        // DV65: a wall is not built on the tile Bob stands on; aimed at the tile east of him, it is built there
        Assert.Equal(FALSE, Dm.num(r.In(() => iron.building_checks(bob, girder, 1))));
        r.In(() => ClientSeam.Hover(player, locate(5, 4, 1)));

        bool done = false;
        object result = null;
        r.In(() => spawn(-1, async () =>
        {
            result = await iron.make_item(bob, girder, 1);
            done = true;
        }));
        r.Step(3 * Second);
        Assert.False(done, "the girder takes four seconds");
        Assert.Empty(On<obj_structure_girder>(r, 5, 4));
        r.Step(2 * Second);
        Assert.True(done);
        Assert.Equal(TRUE, Dm.num(result));
        var girders = On<obj_structure_girder>(r, 5, 4);
        Assert.Single(girders);
        Assert.Equal(18f, Amount(r, iron));

        // CRAFT_ONE_PER_TURF: a second girder on the tile is refused before the do_after (stack.dm:535)
        Assert.Equal(FALSE, Dm.num(r.In(() => iron.building_checks(bob, girder, 1))));
        // the girder is dense: CRAFT_CHECK_DENSITY refuses a stool on it too (stack.dm:554)
        datum_stack_recipe stool = r.In(() => Recipe(iron.recipes, "stool"));
        Assert.Equal(FALSE, Dm.num(r.In(() => iron.building_checks(bob, stool, 1))));

        // CRAFT_ON_SOLID_GROUND: not over space (stack.dm:544-552)
        r.In(() => Waiting.NoSleep(((atom_movable)bob).forceMove(locate(6, 4, 1))));
        r.In(() => Waiting.NoSleep(locate(6, 4, 1).ChangeTurf(typeof(turf_open_space))));
        r.In(() => ClientSeam.Hover(player, locate(6, 4, 1)));
        Assert.Equal(FALSE, Dm.num(r.In(() => iron.building_checks(bob, stool, 1))));
        // not enough material for the amount
        r.In(() => Waiting.NoSleep(((atom_movable)bob).forceMove(locate(7, 4, 1))));
        r.In(() => ClientSeam.Hover(player, locate(7, 5, 1)));
        Assert.Equal(TRUE, Dm.num(r.In(() => iron.building_checks(bob, stool, 1))));
        Assert.Equal(FALSE, Dm.num(r.In(() => iron.building_checks(bob, stool, 19))));

        // a moved builder interrupts the do_after: nothing is built, nothing is used
        done = false;
        r.In(() => spawn(-1, async () =>
        {
            result = await iron.make_item(bob, girder, 1);
            done = true;
        }));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(((atom_movable)bob).forceMove(locate(8, 4, 1))));
        r.Step(5 * Second);
        Assert.True(done);
        Assert.Null(result);
        Assert.Empty(On<obj_structure_girder>(r, 7, 5));
        Assert.Equal(18f, Amount(r, iron));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStackCraftingWindowListsTgsRecipesAndItsMakeActionBuilds()
    {
        var (r, bob) = Room(5205);
        var iron = InHand<obj_item_stack_sheet_iron>(r, bob, 10);
        bob.client.dm_tgui_output.Clear();
        r.In(() => Waiting.NoSleep(iron.attack_self(bob)));
        r.Step(2);
        var outputs = bob.client.dm_tgui_output.ToList();
        bob.client.dm_tgui_output.Clear();
        JsonElement payload = default;
        string window = null;
        foreach (TguiOutput o in outputs)
        {
            if (o.Kind != TguiOutputKind.Message)
                continue;
            using JsonDocument doc = JsonDocument.Parse(o.Json);
            if (doc.RootElement.GetProperty("type").GetString() != "update")
                continue;
            // the window's first update carries its static data; a later one (SStgui's periodic update, whose tick depends on
            // the subsystems' order: M5x slice 1 added SSexplosions) has only the data
            if (!doc.RootElement.GetProperty("payload").TryGetProperty("static_data", out _))
                continue;
            payload = doc.RootElement.GetProperty("payload").Clone();
            window = o.WindowId;
        }
        Assert.NotNull(window);
        Assert.Equal("StackCrafting", payload.GetProperty("config").GetProperty("interface").GetProperty("name").GetString());
        Assert.Equal(10, payload.GetProperty("data").GetProperty("amount").GetInt32());
        JsonElement recipes = payload.GetProperty("static_data").GetProperty("recipes");
        JsonElement floorTile = recipes.GetProperty("floor tile");
        Assert.Equal(4, floorTile.GetProperty("res_amount").GetInt32());
        Assert.Equal(20, floorTile.GetProperty("max_res_amount").GetInt32());
        Assert.Equal("tile", floorTile.GetProperty("icon_state").GetString());
        // a recipe list is a nested object (recursively_build_recipes), the iron's material adds tg's base recipes
        Assert.Equal(JsonValueKind.Object, recipes.GetProperty("airlock assemblies").GetProperty("standard airlock assembly").ValueKind);
        Assert.True(recipes.TryGetProperty("Chair", out _), "MATERIAL_BASIC_RECIPES adds SSmaterials' base recipes");

        // the "make" act with the recipe's ref and a multiplier of 2 (stack.dm:351-356)
        string reference = floorTile.GetProperty("ref").GetString();
        r.Command(() => ClientSeam.TguiMessage(bob.client, window, "act/make", Dm.list(Dm.kv("ref", reference), Dm.kv("multiplier", "2"))));
        r.Step(4);
        Assert.Equal(8f, Amount(r, iron));
        var tiles = r.In(() => Dm.each_cast<obj_item>(bob.held_items).OfType<obj_item_stack_tile_iron_base>().ToList());
        Assert.Single(tiles);
        Assert.Equal(8f, Amount(r, tiles[0]));
        r.AssertNoRuntimes();
    }

    // tg: code/modules/unit_tests/stack_singular_name.dm:7
    [Fact]
    public void StackSingularName()
    {
        var r = new AudioRoom(4, 4, seed: 5206);
        r.In(() =>
        {
            foreach (Type stack_check in Dm.each<Type>(valid_subtypesof(typeof(obj_item_stack))))
            {
                if (!Dm.truthy(Dm.initial(stack_check, (obj_item_stack s) => s.singular_name)))
                    Assert.Fail(Dm.text("[] is missing a singular name!", stack_check));
            }
        });
    }

    [Fact]
    public void AGlassShardCutsABarefootHumanAndPlaysItsStepSound()
    {
        var r = new AudioRoom(10, 6, seed: 5207);
        mob_living_carbon_human bob = r.Join(3, 3, "Bob");
        r.In(() => { if (bob.shoes != null) qdel(bob.shoes); });
        var shard = r.In(() => Dm.@new<obj_item_shard>(locate(4, 3, 1)));
        r.Step(Second);
        Assert.NotNull(r.In(() => shard.GetComponent(typeof(datum_component_caltrop))));
        float before = r.In(() => Dm.num(bob.get_brute_loss()));
        int heard = r.Sounds.Heard.Count;
        r.In(() => Waiting.NoSleep(bob.Move(locate(4, 3, 1), EAST)));
        r.Step();
        float after = r.In(() => Dm.num(bob.get_brute_loss()));
        Assert.True(after > before, $"the shard cut the bare foot ({before} -> {after})");
        Assert.Contains(r.Sounds.Heard.Skip(heard), s => s.File.EndsWith("effects/footstep/glass_step.ogg"));
        Assert.True(r.In(() => Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_caltropped)))), "the caltropped status");

        // with shoes the feet are covered (caltrop.dm:99-101): no damage
        mob_living_carbon_human ann = r.Dummy(3, 5);
        var shoes = r.In(() => Dm.@new<obj_item_clothing_shoes_sneakers_black>(locate(3, 5, 1)));
        r.In(() => Waiting.NoSleep(ann.equip_to_slot_or_del(shoes, ITEM_SLOT_FEET)));
        Assert.Same(shoes, r.In(() => ann.shoes));
        r.In(() => Dm.@new<obj_item_shard>(locate(4, 5, 1)));
        r.Step(Second);
        float annBefore = r.In(() => Dm.num(ann.get_brute_loss()));
        r.In(() => Waiting.NoSleep(ann.Move(locate(4, 5, 1), EAST)));
        r.Step();
        Assert.Equal(annBefore, r.In(() => Dm.num(ann.get_brute_loss())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SheetMaterialsAreTgsMaterialDatums()
    {
        var r = new AudioRoom(6, 6, seed: 5208);
        var glass = New<obj_item_stack_sheet_glass>(r, 3, 3, 2);
        var rglass = New<obj_item_stack_sheet_rglass>(r, 4, 3, 1);
        r.Step();
        datum_material g = r.In(() => Waiting.NoSleep(glass.get_master_material()) as datum_material);
        Assert.IsType<datum_material_glass>(g);
        Assert.Equal(typeof(obj_item_shard), g.shard_type);
        Assert.Equal(2 * SHEET_MATERIAL_AMOUNT, Dm.num(r.In(() => glass.custom_materials[g])));
        // reinforced glass: half a sheet of iron and a sheet of glass a sheet, the main material first (sortTim dsc)
        Assert.Equal(2, r.In(() => rglass.custom_materials.len));
        Assert.IsType<datum_material_glass>(r.In(() => rglass.custom_materials[1]));
        Assert.Equal("list(/datum/material/glass = SHEET_MATERIAL_AMOUNT, /datum/material/iron = HALF_SHEET_MATERIAL_AMOUNT)", r.In(() => rglass.transcribe_materials_list(null)));
        // the alloys break down into their components
        datum_material plasteelMat = r.In(() => Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_alloy_plasteel))) as datum_material);
        DmList plasteel = r.In(() => Waiting.NoSleep(plasteelMat.return_composition(100f)) as DmList);
        Assert.Equal(2, plasteel.len);
        r.AssertNoRuntimes();
    }
}
