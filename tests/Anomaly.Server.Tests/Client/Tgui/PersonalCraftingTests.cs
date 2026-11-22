using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// M5 slice 8 (D148): the client's PersonalCrafting window without the engine (tg's PersonalCrafting/*.tsx translated): it reads
/// tg's crafting data (recipes, categories, atom_data, craftability, material_occurences), opens on "Can Make" when something is
/// craftable, filters by category, material and search, draws each recipe's materials and tools with their pictures, enables the
/// Make button by craftability and busy, and sends tg's acts (make, make_mass, toggle_recipes, toggle_compact, toggle_mode).
/// </summary>
public class PersonalCraftingTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(UiValue data, TguiLocalState local)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "PersonalCrafting", "Crafting", 2, data, (a, p) => acts.Add((a, p?.ToJson())), local);
        UiNode tree = PersonalCrafting.Render(context);
        return (context, tree, acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    // tg's ui_static_data and ui_data merged (the shape of _crafting.dm:507-585): three recipes, five atoms.
    private const string Data = "{"
        + "\"busy\":0,\"mode\":0,\"display_craftable_only\":0,\"display_compact\":0,\"forced_mode\":0,"
        + "\"craftability\":{\"[0x1]\":1},"
        + "\"categories\":[\"Equipment\",\"Entertainment\"],"
        + "\"foodtypes\":[\"MEAT\",\"VEGETABLES\"],"
        + "\"diet\":{\"liked_food\":[\"JUNKFOOD\"],\"disliked_food\":[\"GROSS\"],\"toxic_food\":[\"TOXIC\"]},"
        + "\"icon_data\":[],"
        + "\"atom_data\":["
        + "{\"name\":\"skateboard\",\"is_reagent\":0,\"icon\":\"icons/mob/rideables/vehicles.dmi\",\"icon_state\":\"skateboard\"},"
        + "{\"name\":\"iron\",\"is_reagent\":0,\"icon\":\"icons/obj/stack_objects.dmi\",\"icon_state\":\"sheet-metal_3\"},"
        + "{\"name\":\"iron rod\",\"is_reagent\":0,\"icon\":\"icons/obj/stack_objects.dmi\",\"icon_state\":\"rods\"},"
        + "{\"name\":\"tether anchor\",\"is_reagent\":0,\"icon\":\"icons/obj/tether.dmi\",\"icon_state\":\"anchor\"},"
        + "{\"name\":\"cable coil\",\"is_reagent\":0,\"icon\":\"icons/obj/stack_objects.dmi\",\"icon_state\":\"coil\"}],"
        + "\"material_occurences\":[{\"atom_id\":\"2\",\"occurences\":2},{\"atom_id\":\"3\",\"occurences\":3}],"
        + "\"recipes\":["
        + "{\"ref\":\"[0x1]\",\"id\":1,\"category\":\"Entertainment\",\"name\":\"Skateboard\",\"desc\":\"An improvised skateboard.\",\"mass_craftable\":0,\"reqs\":{\"2\":5,\"3\":10}},"
        + "{\"ref\":\"[0x2]\",\"id\":4,\"category\":\"Equipment\",\"name\":\"Tether Anchor\",\"desc\":\"An anchor.\",\"mass_craftable\":1,\"tool_behaviors\":[\"screwdriver\",\"wrench\"],\"reqs\":{\"2\":5,\"3\":2,\"5\":15}},"
        + "{\"ref\":\"[0x3]\",\"id\":3,\"category\":\"Equipment\",\"name\":\"Rod\",\"desc\":\"\",\"mass_craftable\":0,\"non_craftable\":1,\"steps\":[\"Use iron\"],\"reqs\":{\"2\":1}}"
        + "]}";

    [Fact]
    public void TheWindowOpensOnCanMakeAndListsTheCraftableRecipeWithItsMaterials()
    {
        var local = new TguiLocalState();
        var (context, tree, acts) = Render(UiValue.FromJson(Data), local);
        Assert.Equal((700.0, 720.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Crafting", context.WindowTitle);
        // the categories: "Can Make" first with the craftable count, then tg's categories sorted
        Assert.Equal(new[] { "category:Can Make", "category:Entertainment", "category:Equipment" },
            tree.Walk().Where(n => n.Kind == UiKind.Tab && n.Key?.StartsWith("category:", StringComparison.Ordinal) == true).Select(n => n.Key));
        Assert.True(ByKey(tree, "category:Can Make").Selected);
        Assert.Contains("1", ByKey(tree, "category:Can Make").AllText());
        // "Can Make" shows the skateboard only, with its picture and its materials
        UiNode skateboard = ByKey(tree, "recipe:[0x1]");
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "recipe:[0x2]");
        Assert.Contains("Skateboard", skateboard.AllText());
        Assert.Contains("An improvised skateboard.", skateboard.AllText());
        Assert.Contains("Materials", skateboard.AllText());
        Assert.Contains("Iron 5x", skateboard.AllText());
        Assert.Contains("Iron Rod 10x", skateboard.AllText());
        Assert.Contains(skateboard.Walk(), n => n.DmIcon == "icons/mob/rideables/vehicles.dmi" && n.DmIconState == "skateboard");
        // its Make button sends tg's make with the recipe's ref
        UiNode make = ByKey(tree, "make:[0x1]");
        Assert.False(make.Disabled);
        Assert.Equal("Make", make.Text);
        Assert.Equal("hammer", make.Icon);
        make.OnClick();
        Assert.Equal(("make", "{\"recipe\":\"[0x1]\"}"), acts.Single());
        // the search placeholder counts tg's designs
        Assert.Equal("Search in 3 designs...", ByKey(tree, "search").Tooltip);
    }

    [Fact]
    public void TheCategoriesTheMaterialsTheSearchAndTheTogglesFollowTg()
    {
        var local = new TguiLocalState();
        var (_, tree, acts) = Render(UiValue.FromJson(Data), local);
        // Equipment: the anchor (not craftable: Make disabled; mass craftable: the repeat button) and the rod's steps
        ByKey(tree, "category:Equipment").OnClick();
        (_, tree, acts) = Render(UiValue.FromJson(Data), local);
        UiNode anchor = ByKey(tree, "recipe:[0x2]");
        Assert.True(ByKey(anchor, "make:[0x2]").Disabled);
        Assert.True(ByKey(anchor, "make_mass:[0x2]").Disabled);
        Assert.Contains("Tools", anchor.AllText());
        Assert.Contains(anchor.Walk(), n => n.Key == "tool:screwdriver" && n.Walk().Any(x => x.DmIcon == "icons/map_icons/items/_item.dmi"));
        Assert.Contains(anchor.Walk(), n => n.Key == "tool:wrench");
        Assert.Contains("Cable Coil 15x", anchor.AllText());
        UiNode rod = ByKey(tree, "recipe:[0x3]");
        Assert.DoesNotContain(rod.Walk(), n => n.Key == "make:[0x3]"); // non_craftable: no button
        Assert.Contains("Use iron", rod.AllText());
        // an ingredient that has its own recipe (the rod, atom 3) floats that recipe when hovered
        UiNode floating = anchor.Walk().First(n => n.Kind == UiKind.Floating);
        Assert.Equal("atom:3", floating.Key);
        Assert.Contains("Rod", floating.Content().AllText());

        // the Material tab: the materials by occurrences, the rods (3) first; its recipes
        ByKey(tree, "tab:material").OnClick();
        (_, tree, _) = Render(UiValue.FromJson(Data), local);
        Assert.Equal(new[] { "material:3", "material:2" }, tree.Walk().Where(n => n.Key?.StartsWith("material:", StringComparison.Ordinal) == true).Select(n => n.Key));
        Assert.Contains(tree.Walk(), n => n.Key == "recipe:[0x1]");
        Assert.Contains(tree.Walk(), n => n.Key == "recipe:[0x2]");
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "recipe:[0x3]"); // the rod needs iron only

        // the search ignores the categories
        ByKey(tree, "search").OnInput("tether");
        (_, tree, acts) = Render(UiValue.FromJson(Data), local);
        Assert.Equal(new[] { "recipe:[0x2]" }, tree.Walk().Where(n => n.Key?.StartsWith("recipe:", StringComparison.Ordinal) == true && !n.Key.EndsWith(":floating", StringComparison.Ordinal)).Select(n => n.Key));

        // the toggles and the mode buttons send tg's acts
        ByKey(tree, "toggle_recipes").OnClick();
        ByKey(tree, "toggle_compact").OnClick();
        ByKey(tree, "mode:craft").OnClick(); // already crafting: nothing
        ByKey(tree, "mode:cook").OnClick();
        Assert.Equal(new[] { "toggle_recipes", "toggle_compact", "toggle_mode" }, acts.Select(a => a.Action));
    }

    [Fact]
    public void TwoRecipesThatNeedEachOtherDoNotRecurse()
    {
        // the owner's crash (2026-10-16): the rod (atom 3) needs iron (atom 2), and here a recipe makes iron (id 2) from rods; each
        // ingredient's floating recipe is built only when it opens, as tgui-core's Floating renders it
        string cycle = Data.Replace("\"name\":\"Rod\",\"desc\":\"\",", "\"name\":\"Rod\",\"desc\":\"\",")
            .Replace("\"recipes\":[", "\"recipes\":[{\"ref\":\"[0x4]\",\"id\":2,\"category\":\"Equipment\",\"name\":\"Iron\",\"desc\":\"\",\"mass_craftable\":0,\"reqs\":{\"3\":2}},");
        var local = new TguiLocalState();
        var (_, tree, _) = Render(UiValue.FromJson(cycle), local);
        ByKey(tree, "category:Equipment").OnClick();
        (_, tree, _) = Render(UiValue.FromJson(cycle), local);
        UiNode iron = ByKey(tree, "recipe:[0x4]");
        UiNode rodFloat = iron.Walk().First(n => n.Kind == UiKind.Floating && n.Key == "atom:3");
        UiNode ironFloat = rodFloat.Content().Walk().First(n => n.Kind == UiKind.Floating && n.Key == "atom:2");
        Assert.Contains("Iron", ironFloat.Content().AllText());
    }

    private static string[] Tabs(UiNode tree) => tree.Walk().Where(n => n.Kind == UiKind.Tab && n.Key?.StartsWith("category:", StringComparison.Ordinal) == true).Select(n => n.Key).ToArray();

    [Fact]
    public void AFilterHidesTheCategoriesItLeavesEmpty()
    {
        // DEVIATION(DV44), the owner (2026-10-16): the search lists only the categories holding a match
        var local = new TguiLocalState();
        var (_, tree, _) = Render(UiValue.FromJson(Data), local);
        ByKey(tree, "search").OnInput("tether");
        (_, tree, _) = Render(UiValue.FromJson(Data), local);
        Assert.Equal(new[] { "category:Equipment" }, Tabs(tree));
        ByKey(tree, "search").OnInput("");
        (_, tree, _) = Render(UiValue.FromJson(Data), local);
        Assert.Equal(new[] { "category:Can Make", "category:Entertainment", "category:Equipment" }, Tabs(tree));

        // "craftable only": the skateboard's categories; an active category it empties gives way to the first one left
        ByKey(tree, "category:Equipment").OnClick();
        string craftableOnly = Data.Replace("\"display_craftable_only\":0", "\"display_craftable_only\":1");
        (_, tree, _) = Render(UiValue.FromJson(craftableOnly), local);
        Assert.Equal(new[] { "category:Can Make", "category:Entertainment" }, Tabs(tree));
        Assert.True(ByKey(tree, "category:Can Make").Selected);
        Assert.Contains(tree.Walk(), n => n.Key == "recipe:[0x1]");
    }

    [Fact]
    public void BusyDisablesMakeAndTheCompactListAndCookingReadTgsData()
    {
        string busy = Data.Replace("\"busy\":0", "\"busy\":1").Replace("\"display_compact\":0", "\"display_compact\":1");
        var (_, tree, _) = Render(UiValue.FromJson(busy), new TguiLocalState());
        UiNode make = ByKey(tree, "make:[0x1]");
        Assert.True(make.Disabled);
        Assert.Equal("circle-notch", make.Icon);
        // the compact row lists the materials in one line
        Assert.Contains("Iron 5x, Iron Rod 10x", ByKey(tree, "recipe:[0x1]").AllText());
        Assert.True(ByKey(tree, "toggle_compact").Checkbox);

        // cooking mode: utensils, "Ingredient", tg's food type row with the diet's face
        string cooking = Data.Replace("\"mode\":0", "\"mode\":1").Replace("\"craftability\":{\"[0x1]\":1}", "\"craftability\":[]");
        var local = new TguiLocalState();
        (_, tree, _) = Render(UiValue.FromJson(cooking), local);
        Assert.Equal("Ingredient", ByKey(tree, "tab:material").Text);
        Assert.Contains("recipes...", ByKey(tree, "search").Tooltip);
        UiNode junk = PersonalCrafting.FoodtypeContent("JUNKFOOD", UiValue.FromJson(cooking).Get("diet"), null);
        Assert.Contains(junk.Walk(), n => n.Icon == "face-laugh-beam" && n.Color == "good");
        Assert.Contains("Junkfood", junk.AllText());
        // nothing craftable: the first category of tg's list opens (Appetizer when cooking), no recipe of it: tg's notice
        Assert.True(ByKey(tree, "category:Can Make").Selected == false);
        Assert.Contains("No recipes found.", tree.AllText());
    }
}
