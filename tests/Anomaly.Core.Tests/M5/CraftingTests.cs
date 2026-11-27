using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 8: tg's crafting (datums/components/crafting/_crafting.dm, _recipes.dm, elements/slapcrafting.dm,
/// __HELPERS/global_lists.dm:14-135) in a booted toy room: the recipe lists in scope, the human's craft button on the HUD and
/// tg's "PersonalCrafting" window with its data, construct_item using up the right parts and keeping the tools, its do_after
/// timed by the tools, the failures of perform_all_checks, the window's make act, and slapcrafting (rods hit with a shard).
/// </summary>
public class CraftingTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public CraftingTests(ITestOutputHelper output) => _output = output;

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, bob);
    }

    private static T Spawn<T>(AudioRoom r, int x, int y, float? amount = null) where T : atom
        => r.In(() => (T)(amount is float a ? Dm.@new(typeof(T), locate(x, y, 1), a, FALSE) : Dm.@new(typeof(T), locate(x, y, 1))));

    private static List<T> On<T>(AudioRoom r, int x, int y) => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().ToList());

    private static datum_crafting_recipe RecipeOf(AudioRoom r, Type path)
        => r.In(() => GLOB.crafting_recipes_by_typepath[path] as datum_crafting_recipe);

    private static datum_component_personal_crafting Crafting(AudioRoom r, mob who)
        => r.In(() => who.GetComponent(typeof(datum_component_personal_crafting)) as datum_component_personal_crafting);

    // A tgui "update" payload as the server's UiWindow message (Replicator.Tgui: config, data and static data), encoded by the wire codec:
    // its bytes and its values (each null, number, string, array and object, as the codec counts them).
    private static (int Bytes, int Values) Encoded(JsonElement payload)
    {
        UiValue data = payload.TryGetProperty("data", out JsonElement d) ? UiValue.FromJson(d) : null;
        UiValue statics = payload.TryGetProperty("static_data", out JsonElement st) ? UiValue.FromJson(st) : null;
        var message = new UiWindowMessage
        {
            Kind = UiWindowKind.Update,
            Window = 1,
            Interface = payload.GetProperty("config").GetProperty("interface").GetProperty("name").GetString(),
            Title = payload.GetProperty("config").GetProperty("title").GetString(),
            Status = 2,
            Data = data,
            StaticData = statics,
        };
        return (Codec.Encode(message).Length, Count(data) + Count(statics));
    }

    private static int Count(UiValue value) => value == null ? 0 : value.Kind switch
    {
        UiValueKind.Array => 1 + value.Items.Sum(Count),
        UiValueKind.Object => 1 + value.Fields.Sum(f => Count(f.Value)),
        _ => 1,
    };

    // The last tgui "update" message of the client: (window id, payload).
    private static (string Window, JsonElement Payload) LastUpdate(mob who, bool withStatic = false)
    {
        string window = null;
        JsonElement payload = default;
        foreach (TguiOutput o in who.client.dm_tgui_output.ToList())
        {
            if (o.Kind != TguiOutputKind.Message)
                continue;
            using JsonDocument doc = JsonDocument.Parse(o.Json);
            if (doc.RootElement.GetProperty("type").GetString() != "update")
                continue;
            if (withStatic && !doc.RootElement.GetProperty("payload").TryGetProperty("static_data", out _))
                continue;
            payload = doc.RootElement.GetProperty("payload").Clone();
            window = o.WindowId;
        }
        return (window, payload);
    }

    [Fact]
    public void TheRecipeListsHoldTgsRecipesWhoseIngredientsThePortCanHold()
    {
        var r = new AudioRoom(4, 4, seed: 5801);
        r.In(() =>
        {
            int crafting = (int)Dm.length(GLOB.crafting_recipes);
            int cooking = (int)Dm.length(GLOB.cooking_recipes);
            int stacks = Dm.each_cast<datum_crafting_recipe>(GLOB.crafting_recipes).Count(x => x is datum_crafting_recipe_stack);
            _output.WriteLine($"crafting {crafting} (stack recipes {stacks}), cooking {cooking}, crafting atoms {Dm.length(GLOB.crafting_recipes_datums)}, cooking atoms {Dm.length(GLOB.cooking_recipes_datums)}");
            int inertCrafting = Dm.each_cast<datum_crafting_recipe>(GLOB.crafting_recipes).Count(x => InertSeam.IsInertType(x.result));
            int inertCooking = Dm.each_cast<datum_crafting_recipe>(GLOB.cooking_recipes).Count(x => InertSeam.IsInertType(x.result));
            _output.WriteLine($"results not ported (inert): crafting {inertCrafting}, cooking {inertCooking}");
            var all = Dm.each<Type>(valid_subtypesof(typeof(datum_crafting_recipe))).ToList();
            _output.WriteLine($"tg's recipe types {all.Count}, of which chemical reaction recipes (inert) {all.Count(InertSeam.IsInertType)}");
            Assert.True(crafting - stacks > 250, "the datum recipes of tg's crafting files are listed");
            Assert.True(cooking > 300, "the cooking recipes made of items are listed");
            // tg's recipes of rods (the slapcrafting ones), of the extinguisher and of the cell
            foreach (Type t in new[] { typeof(datum_crafting_recipe_spear), typeof(datum_crafting_recipe_ghettojetpack), typeof(datum_crafting_recipe_battery_match), typeof(datum_crafting_recipe_tether_anchor) })
                Assert.True(GLOB.crafting_recipes_by_typepath[t] is datum_crafting_recipe, t.Name);
            // the recipes that need a reagent are listed (since the restaurant: the reagents of the containers around are
            // ingredients): tg's "IED" needs 20 units of welding fuel; and the chemical reactions' recipe guides are in the cooking list
            var improv = Assert.IsAssignableFrom<datum_crafting_recipe>(GLOB.crafting_recipes_by_typepath[typeof(datum_crafting_recipe_improv_explosive)]);
            Assert.Contains(Dm.each(improv.reqs), req => ispath(req, typeof(datum_reagent_fuel)));
            Assert.Contains(Dm.each_cast<datum_crafting_recipe>(GLOB.cooking_recipes), x => x is datum_crafting_recipe_food_reaction && x.reaction != null);
            // init_crafting_recipes_atoms: results, ingredients and tools of every recipe
            Assert.True(Dm.@in(typeof(obj_item_spear), GLOB.crafting_recipes_datums));
            Assert.True(Dm.@in(typeof(obj_item_restraints_handcuffs_cable), GLOB.crafting_recipes_datums));
            // the window's category list: each recipe has tg's category (a food recipe is cooking: CAT_FOOD)
            Assert.All(Dm.each_cast<datum_crafting_recipe>(GLOB.cooking_recipes), x => Assert.True(Dm.@in(x.category, GLOB.crafting_category_food)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHumanHasTheCraftButtonAndItOpensTgsPersonalCraftingWindow()
    {
        var (r, bob) = Room(5802);
        datum_component_personal_crafting crafting = Crafting(r, bob);
        Assert.NotNull(crafting);
        var button = r.In(() => bob.hud_used.screen_objects[HUD_MOB_CRAFTING_MENU] as atom_movable_screen_craft);
        Assert.NotNull(button);
        Assert.Equal("craft", r.In(() => button.icon_state));
        Assert.Equal(ui_human_crafting, r.In(() => button.screen_loc as string));
        Assert.True(r.In(() => Dm.@in(button, bob.client.screen)), "the button is on the client's screen");

        // rods, iron and cable next to Bob: the "Tether Anchor" lacks only its tools, the "Skateboard" can be made
        Spawn<obj_item_stack_rods>(r, 5, 4, 20);
        Spawn<obj_item_stack_sheet_iron>(r, 5, 4, 20);
        Spawn<obj_item_stack_cable_coil>(r, 5, 4, 20);
        r.Step(2);
        bob.client.dm_tgui_output.Clear();
        r.Command(() => ClientSeam.Click(bob.client, button, "left=1;icon-x=16;icon-y=16"));
        r.Step(4);
        (string window, JsonElement payload) = LastUpdate(bob, withStatic: true);
        Assert.NotNull(window);
        Assert.Equal("PersonalCrafting", payload.GetProperty("config").GetProperty("interface").GetProperty("name").GetString());
        Assert.Equal("Crafting", payload.GetProperty("config").GetProperty("title").GetString());
        JsonElement data = payload.GetProperty("data");
        JsonElement statics = payload.GetProperty("static_data");
        Assert.Equal(0, data.GetProperty("mode").GetInt32());
        JsonElement recipes = statics.GetProperty("recipes");
        _output.WriteLine($"recipes {recipes.GetArrayLength()}, categories {statics.GetProperty("categories").GetArrayLength()}, atoms {statics.GetProperty("atom_data").GetArrayLength()}");
        Assert.Equal(r.In(() => Dm.length(GLOB.crafting_recipes_default)), recipes.GetArrayLength());
        Assert.Contains(statics.GetProperty("categories").EnumerateArray(), c => c.GetString() == CAT_STRUCTURE);
        Assert.Equal(22, statics.GetProperty("foodtypes").GetArrayLength());

        JsonElement support = recipes.EnumerateArray().First(x => x.GetProperty("name").GetString() == "Skateboard");
        string supportRef = support.GetProperty("ref").GetString();
        JsonElement anchor = recipes.EnumerateArray().First(x => x.GetProperty("name").GetString() == "Tether Anchor");
        Assert.Equal(new[] { "screwdriver", "wrench" }, anchor.GetProperty("tool_behaviors").EnumerateArray().Select(x => x.GetString()).ToArray());
        // the ids of reqs index atom_data (1-based), which carries the name and the client's picture of each atom
        JsonElement atoms = statics.GetProperty("atom_data");
        string[] reqNames = anchor.GetProperty("reqs").EnumerateObject().Select(p => atoms[int.Parse(p.Name) - 1].GetProperty("name").GetString()).ToArray();
        Assert.Contains("iron", reqNames);
        Assert.Contains("iron rod", reqNames);
        JsonElement rodAtom = atoms.EnumerateArray().First(a => a.GetProperty("name").GetString() == "iron rod");
        Assert.Equal("icons/obj/stack_objects.dmi", rodAtom.GetProperty("icon").GetString());
        Assert.Equal(0, rodAtom.GetProperty("is_reagent").GetInt32()); // tg sends FALSE (0)
        Assert.True(statics.GetProperty("material_occurences").GetArrayLength() > 0);
        // craftability: the support can be made, the anchor cannot (no screwdriver and wrench)
        JsonElement craftability = data.GetProperty("craftability");
        Assert.True(craftability.TryGetProperty(supportRef, out _), "the skateboard is craftable");
        Assert.False(craftability.TryGetProperty(anchor.GetProperty("ref").GetString(), out _), "the tether anchor lacks its tools");

        // tg's "make" act: the skateboard is built on Bob's tile and its rods are used up
        float rodsBefore = r.In(() => On<obj_item_stack_rods>(r, 5, 4).Sum(s => Dm.num(s.amount)));
        datum_crafting_recipe recipe = RecipeOf(r, typeof(datum_crafting_recipe_skateboard));
        float rodsNeeded = r.In(() => Dm.num(recipe.reqs[typeof(obj_item_stack_rods)]));
        r.Command(() => ClientSeam.TguiMessage(bob.client, window, "act/make", Dm.list(Dm.kv("recipe", supportRef))));
        r.Step(2);
        Assert.Equal(TRUE, r.In(() => Dm.num(crafting.busy))); // busy during the do_after
        r.Step((int)(r.In(() => Dm.num(recipe.time)) / 10 * Second) + Second);
        Assert.Equal(FALSE, r.In(() => Dm.num(crafting.busy)));
        Assert.Single(On<obj_vehicle_ridden_scooter_skateboard_improvised>(r, 4, 4));
        Assert.Equal(rodsBefore - rodsNeeded, r.In(() => On<obj_item_stack_rods>(r, 5, 4).Sum(s => Dm.num(s.amount))));

        // tg's "toggle_mode": the cooking recipes (update_static_data)
        bob.client.dm_tgui_output.Clear();
        r.Command(() => ClientSeam.TguiMessage(bob.client, window, "act/toggle_mode", Dm.list()));
        r.Step(4);
        (_, JsonElement cooking) = LastUpdate(bob, withStatic: true);
        Assert.Equal(1, cooking.GetProperty("data").GetProperty("mode").GetInt32());
        JsonElement cookingStatics = cooking.GetProperty("static_data");
        Assert.Equal(r.In(() => Dm.length(GLOB.cooking_recipes_default)), cookingStatics.GetProperty("recipes").GetArrayLength());
        JsonElement burger = cookingStatics.GetProperty("recipes").EnumerateArray().First(x => x.TryGetProperty("foodtypes", out _));
        Assert.True(burger.TryGetProperty("cuisine_category", out _));
        // the window as the server sends it (Codec.V16): tg's crafting window sends every recipe of the tab at once, the cooking tab the
        // most (its recipes with reagent ingredients and the chemical reactions' recipe guides)
        var (craftingBytes, craftingValues) = Encoded(payload);
        var (cookingBytes, cookingValues) = Encoded(cooking);
        _output.WriteLine($"encoded window: crafting {craftingBytes} bytes, {craftingValues} values; cooking {cookingBytes} bytes, {cookingValues} values");
        Assert.True(craftingBytes <= Limits.MaxUiWindowBytes && cookingBytes <= Limits.MaxUiWindowBytes, "the window fits Limits.MaxUiWindowBytes");
        Assert.True(craftingValues <= Limits.MaxUiValues && cookingValues <= Limits.MaxUiValues, "the window fits Limits.MaxUiValues");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ConstructItemUsesUpThePartsKeepsTheToolsAndTimesTheDoAfterByTheTools()
    {
        var (r, bob) = Room(5803);
        datum_component_personal_crafting crafting = Crafting(r, bob);
        datum_crafting_recipe anchor = RecipeOf(r, typeof(datum_crafting_recipe_tether_anchor));
        Spawn<obj_item_stack_rods>(r, 5, 4, 20);
        Spawn<obj_item_stack_sheet_iron>(r, 5, 4, 20);
        Spawn<obj_item_stack_cable_coil>(r, 3, 4, 20);
        r.Step(2);

        // without the tools: perform_all_checks says what is missing
        object fail = null;
        r.Command(() => Waiting.Detach(Run()));
        async DmTask Run() => fail = await crafting.construct_item(bob, anchor);
        r.Step(2);
        Assert.Equal(", missing tool.", fail);

        var screwdriver = Spawn<obj_item_screwdriver>(r, 4, 5);
        var wrench = Spawn<obj_item_wrench>(r, 4, 3);
        r.Step(2);
        // the do_after: time * (1 - 0.85) + time * 0.85 / 2 * (screwdriver speed + wrench speed) (_crafting.dm:254-279)
        float time = r.In(() => Dm.num(anchor.time));
        float expected = time - time * 0.85f + time * 0.85f / 2 * (r.In(() => Dm.num(screwdriver.toolspeed)) + r.In(() => Dm.num(wrench.toolspeed)));
        expected *= r.In(() => Dm.num(bob.cached_multiplicative_actions_slowdown)); // do_after's action speed (a good mood's high sanity is 0.9)
        long start = r.World.Tick;
        object made = null;
        r.Command(() => Waiting.Detach(Make()));
        async DmTask Make() => made = await crafting.construct_item(bob, anchor);
        while (made == null && r.World.Tick < start + 400)
            r.Step();
        long took = r.World.Tick - start;
        _output.WriteLine($"tether anchor: {made}, {took} ticks (expected about {expected / 10 * Second})");
        Assert.IsType<obj_item_tether_anchor>(made);
        Assert.InRange(took, (long)(expected / 10 * Second) - 2, (long)(expected / 10 * Second) + 4);
        // the parts: tg's reqs used up exactly (rods, iron, cable)
        datum_crafting_recipe recipe = anchor;
        foreach ((Type part, int x, int y) in new[] { (typeof(obj_item_stack_rods), 5, 4), (typeof(obj_item_stack_sheet_iron), 5, 4), (typeof(obj_item_stack_cable_coil), 3, 4) })
        {
            float left = r.In(() => Dm.each_cast<obj_item_stack>(locate(x, y, 1).contents).Where(s => s.GetType() == part).Sum(s => Dm.num(s.amount)));
            Assert.Equal(20 - r.In(() => Dm.num(recipe.reqs[part])), left);
        }
        // the tools are kept
        Assert.False(r.In(() => QDELETED(screwdriver)));
        Assert.False(r.In(() => QDELETED(wrench)));
        // the result's materials come from the parts used (set_custom_materials of total_materials)
        Assert.True(r.In(() => Dm.length(((atom)made).custom_materials)) > 0);

        // CRAFT_CHECK_DENSITY: a dense object on the tile (a locker; tables and grilles have IGNORE_DENSITY) refuses the skateboard
        datum_crafting_recipe support = RecipeOf(r, typeof(datum_crafting_recipe_skateboard));
        Spawn<obj_structure_closet>(r, 4, 4);
        r.Step(2);
        fail = null;
        r.Command(() => Waiting.Detach(Support()));
        async DmTask Support() => fail = await crafting.construct_item(bob, support);
        r.Step(2);
        Assert.Equal(", something is in the way!", fail);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void RodsHitWithAShardSlapcraftASpearFromTheCableRestraintsNearby()
    {
        var (r, bob) = Room(5804);
        var rods = Spawn<obj_item_stack_rods>(r, 5, 4, 5);
        Spawn<obj_item_restraints_handcuffs_cable>(r, 4, 3);
        var shard = r.In(() => Dm.@new<obj_item_shard>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(shard)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(bob.is_holding(shard))));
        // the rods carry tg's slapcrafting element with the spear and the prods
        r.Command(() => ClientSeam.Click(bob.client, rods, "left=1;icon-x=16;icon-y=16"));
        datum_crafting_recipe spear = RecipeOf(r, typeof(datum_crafting_recipe_spear));
        r.Step((int)(r.In(() => Dm.num(spear.time)) / 10 * Second) + Second);
        var spears = r.In(() => Dm.each_cast<atom>(range(1, bob)).OfType<obj_item_spear>().ToList());
        Assert.Single(spears);
        Assert.True(r.In(() => QDELETED(shard)), "the shard went into the spear");
        Assert.Empty(On<obj_item_restraints_handcuffs_cable>(r, 4, 3));
        Assert.Equal(4f, r.In(() => Dm.num(rods.amount)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OnlyThePlainPipeFittingCarriesTgsSlapcraftingRecipes()
    {
        // construction.dm:172-180 (the M5 leftovers, 2026-10-16): the quaternary fitting (tg's "normal" pipe) takes the ghetto
        // jetpack, the pipegun, the smoothbore disabler and the pneumatic cannon; the other fittings do not
        var (r, bob) = Room(5805);
        var plain = r.In(() => Dm.@new<obj_item_pipe_quaternary>(locate(5, 4, 1)));
        var binary = r.In(() => Dm.@new<obj_item_pipe_binary>(locate(6, 4, 1)));
        r.Step(2);
        Assert.True(r.In(() => plain._listen_lookup?[COMSIG_ATOM_ATTACKBY] != null), "the plain fitting's slapcrafting element");
        Assert.False(r.In(() => binary._listen_lookup?[COMSIG_ATOM_ATTACKBY] != null), "no element on a binary fitting");
        Assert.Equal(new[] { typeof(datum_crafting_recipe_ghettojetpack), typeof(datum_crafting_recipe_pipegun), typeof(datum_crafting_recipe_smoothbore_disabler), typeof(datum_crafting_recipe_improvised_pneumatic_cannon) },
            r.In(() => Dm.each<Type>(TgState.Current.obj_item_pipe__slapcraft_recipe_list).ToArray()));
        r.AssertNoRuntimes();
    }
}
