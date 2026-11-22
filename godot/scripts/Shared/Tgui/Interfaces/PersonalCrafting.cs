using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PersonalCrafting/ (index.tsx, types.ts,
// constants.ts, helpers.ts, GroupTitle.tsx, content/RecipeContent.tsx, AtomContent.tsx, MaterialContent.tsx, ToolContent.tsx,
// FoodtypeContent.tsx) (M5 slice 8, D148): the crafting menu of tg's /datum/component/personal_crafting (its ui_static_data's
// recipes, categories, atom_data and material_occurences, its ui_data's craftability, mode and busy).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// The sprite sheet class names of tg (findIcon's "crafting32x32 a<id>", ToolContent's "crafting32x32 <tool>") are pictures of
// the client's own .dmi library: an atom's picture is its atom_data entry's icon and icon_state (the server's seam for tg's
// sprite sheet, DV44), a tool's is tg's add_tool_icons table (ToolIcons below). VirtualList is a plain list (the host's
// scroll container); the Floating popup of a recipe's ingredient is the kit's Floating (shown while hovered).

public static class PersonalCrafting
{
    // ---- types.ts ----

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/types.ts:3
    public enum MODE
    {
        crafting,
        cooking,
    }

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/types.ts:8
    public enum TABS
    {
        category,
        material,
    }

    // ---- constants.ts ----

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/constants.ts:3 (the keys are PreferencesMenu's Food enum values)
    public static readonly IReadOnlyDictionary<string, string> TYPE_ICONS = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Can Make"] = "utensils",
        ["ALCOHOL"] = "wine-glass",
        ["BREAKFAST"] = "sun",
        ["BUGS"] = "bug",
        ["CLOTH"] = "tshirt",
        ["DAIRY"] = "cheese",
        ["EGG"] = "egg",
        ["FRIED"] = "fire",
        ["FRUIT"] = "apple-alt",
        ["GORE"] = "skull",
        ["GRAIN"] = "wheat-awn",
        ["GROSS"] = "trash",
        ["JUNKFOOD"] = "pizza-slice",
        ["MEAT"] = "bacon",
        ["NUTS"] = "seedling",
        ["ORANGES"] = "apple-alt",
        ["PINEAPPLE"] = "apple-alt",
        ["RAW"] = "drumstick-bite",
        ["SEAFOOD"] = "fish",
        ["STONE"] = "gem",
        ["SUGAR"] = "candy-cane",
        ["TOXIC"] = "biohazard",
        ["VEGETABLES"] = "carrot",
    };

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/constants.ts:29 (Object.keys keeps this order)
    public static readonly IReadOnlyList<KeyValuePair<string, string>> CATEGORY_ICONS_CRAFTING = new KeyValuePair<string, string>[]
    {
        new("Can Make", "hammer"),
        new("Weapons Melee", "hand-fist"),
        new("Weapons Ranged", "gun"),
        new("Weapon Ammo", "box"),
        new("Robotics", "robot"),
        new("Misc", "shapes"),
        new("Tribal", "campground"),
        new("Clothing", "shirt"),
        new("Drinks", "wine-bottle"),
        new("Chemistry", "microscope"),
        new("Atmospherics", "fan"),
        new("Structures", "cube"),
        new("Tiles", "border-all"),
        new("Windows", "person-through-window"),
        new("Doors", "door-open"),
        new("Furniture", "chair"),
        new("Equipment", "calculator"),
        new("Containers", "briefcase"),
        new("Tools", "screwdriver-wrench"),
        new("Entertainment", "masks-theater"),
        new("Blood Cult", "users"),
        new("Construction", "trowel-bricks"),
    };

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/constants.ts:54
    public static readonly IReadOnlyList<KeyValuePair<string, string>> CATEGORY_ICONS_COOKING = new KeyValuePair<string, string>[]
    {
        new("Can Make", "utensils"),
        new("Appetizer", "bread-slice"),
        new("Bread", "bread-slice"),
        new("Burger", "burger"),
        new("Burrito", "circle-o"),
        new("Cake", "cake-candles"),
        new("Candy", "candy-cane"),
        new("Component", "cubes"),
        new("Cookie", "cookie"),
        new("Dessert", "face-grin-beam-sweat"),
        new("Frozen", "ice-cream"),
        new("Italian", "pizza-slice"),
        new("Japanese", "earth-asia"),
        new("Lizard", "dragon"),
        new("Martian", "globe"),
        new("Meat", "bacon"),
        new("Mexican", "earth-americas"),
        new("Mothic", "shirt"),
        new("Noodles", "wheat-awn"),
        new("Pastry", "cookie"),
        new("Pie", "chart-pie"),
        new("Pizza", "pizza-slice"),
        new("Rice", "bowl-rice"),
        new("Salad", "leaf"),
        new("Sandwich", "hotdog"),
        new("Soup", "mug-hot"),
        new("Sushi", "fish"),
        new("Taco", "circle-o"),
        new("Terran", "earth-europe"),
        new("Uncategorized", "shapes"),
        new("Main Course", "drumstick-bite"),
        new("Breakfast", "sun"),
        new("Snack", "compress"),
        new("Condiment", "bottle-water"),
    };

    /// <summary>
    /// tg's tool pictures of the crafting sprite sheet (code/modules/asset_cache/assets/crafting.dm:54
    /// add_tool_icons): the tool behaviour's .dmi file and state, which the window draws for ToolContent's
    /// <c>crafting32x32 &lt;tool&gt;</c> class.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Icon, string State)> ToolIcons = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["crowbar"] = ("icons/obj/tools.dmi", "crowbar"),
        ["multitool"] = ("icons/obj/devices/tool.dmi", "multitool"),
        ["screwdriver"] = ("icons/map_icons/items/_item.dmi", "/obj/item/screwdriver"),
        ["wirecutter"] = ("icons/obj/tools.dmi", "cutters_map"),
        ["wrench"] = ("icons/obj/tools.dmi", "wrench"),
        ["welder"] = ("icons/obj/tools.dmi", "welder"),
        ["analyzer"] = ("icons/obj/devices/scanner.dmi", "analyzer"),
        ["mining"] = ("icons/obj/mining.dmi", "minipick"),
        ["shovel"] = ("icons/obj/mining.dmi", "spade"),
        ["retractor"] = ("icons/obj/medical/surgery_tools.dmi", "retractor"),
        ["hemostat"] = ("icons/obj/medical/surgery_tools.dmi", "hemostat"),
        ["cautery"] = ("icons/obj/medical/surgery_tools.dmi", "cautery"),
        ["drill"] = ("icons/obj/medical/surgery_tools.dmi", "drill"),
        ["scalpel"] = ("icons/obj/medical/surgery_tools.dmi", "scalpel"),
        ["saw"] = ("icons/obj/medical/surgery_tools.dmi", "saw"),
        ["bonesetter"] = ("icons/obj/medical/surgery_tools.dmi", "bonesetter"),
        ["knife"] = ("icons/obj/service/kitchen.dmi", "knife"),
        ["bloodfilter"] = ("icons/obj/medical/surgery_tools.dmi", "bloodfilter"),
        ["rolling pin"] = ("icons/obj/service/kitchen.dmi", "rolling_pin"),
        ["rustscraper"] = ("icons/obj/tools.dmi", "wirebrush"),
    };

    // ---- helpers.ts ----

    /// <summary>findIcon: the atom's picture (tg's sprite sheet class; here the atom_data entry's icon and state).</summary>
    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/helpers.ts:5
    public static (string Icon, string State) findIcon(double atom_id, UiValue data)
    {
        IReadOnlyList<UiValue> atomData = data.Arr("atom_data");
        int index = (int)atom_id - 1;
        if (index < 0 || index >= atomData.Count)
            return (null, null);
        return (atomData[index].Str("icon", null), atomData[index].Str("icon_state", null));
    }

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/helpers.ts:14
    public static string getFAIcon(string category, bool mode)
    {
        IReadOnlyList<KeyValuePair<string, string>> icons = mode ? CATEGORY_ICONS_COOKING : CATEGORY_ICONS_CRAFTING;
        foreach (KeyValuePair<string, string> kv in icons)
            if (kv.Key == category)
                return kv.Value;
        return "circle";
    }

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/helpers.ts:19
    public static List<T> toggleArrayItem<T>(List<T> array, T item)
    {
        if (array == null) return new List<T> { item };
        return array.Contains(item) ? array.Where(i => !EqualityComparer<T>.Default.Equals(i, item)).ToList() : array.Append(item).ToList();
    }

    // ---- GroupTitle.tsx ----

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/GroupTitle.tsx:7
    public static UiNode GroupTitle(string title) =>
        new UiNode(UiKind.Stack) { Key = "group:" + title }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Divider) { Rule = true }),
            new UiNode(UiKind.StackItem) { Color = "gray" }.With(new UiNode(UiKind.Box) { Color = "gray", Text = title }),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Divider) { Rule = true }));

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/GroupTitle.tsx:25 (the dashed rules are dividers)
    public static UiNode SubGroupTitle(string title) =>
        new UiNode(UiKind.Stack) { Key = "subgroup:" + title }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Divider) { Rule = true }),
            new UiNode(UiKind.StackItem) { Grow = 1, TextAlign = "center" }.With(new UiNode(UiKind.Box) { Text = title, TextAlign = "center" }),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Divider) { Rule = true }));

    // ---- index.tsx ----

    // React's useState(initial): the initial value is taken once, at the first render (stored then).
    private static T State<T>(TguiContext c, string key, Func<T> initial)
    {
        const string unset = "\u0001unset";
        object stored = c.Local.Get<object>(key, unset);
        if (stored is string s && s == unset)
        {
            T value = initial();
            c.Local.SetQuiet(key, (object)value);
            return value;
        }
        return stored is T t ? t : default;
    }

    private static bool Mode(UiValue data) => data.Truthy("mode");

    private static IEnumerable<string> Keys(UiValue obj) => obj?.Kind == UiValueKind.Object ? obj.Fields.Select(f => f.Key) : Enumerable.Empty<string>();

    private static int CraftableCount(UiValue data) => Keys(data.Get("craftability")).Count();

    private static bool Craftable(UiValue data, string reference) => data.Get("craftability")?.Get(reference)?.Truthy ?? false;

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/index.tsx:34
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool mode = Mode(data);
        bool busy = data.Truthy("busy");
        bool forced_mode = data.Truthy("forced_mode");
        bool display_compact = data.Truthy("display_compact");
        bool display_craftable_only = data.Truthy("display_craftable_only");
        UiValue diet = data.Get("diet");
        IReadOnlyList<UiValue> atom_data = data.Arr("atom_data");
        IReadOnlyList<UiValue> allRecipes = data.Arr("recipes");

        string searchText = c.UseState("searchText", "");
        int pages = c.UseState("pages", 1);

        string DEFAULT_CAT_CRAFTING = CATEGORY_ICONS_CRAFTING[1].Key;
        string DEFAULT_CAT_COOKING = CATEGORY_ICONS_COOKING[1].Key;

        string activeCategory = State(c, "activeCategory", () =>
            CraftableCount(data) > 0
                ? "Can Make"
                : mode
                    ? DEFAULT_CAT_COOKING
                    : DEFAULT_CAT_CRAFTING);

        List<string> Distinct(Func<UiValue, string> key) => allRecipes.Select(key).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();
        // tg: :61-97
        List<string> allFoodCuisines = Distinct(r => r.Str("cuisine_category", null));
        List<string> allDishCategories = Distinct(r => r.Str("dish_category", null));
        List<string> allMealCategories = Distinct(r => r.Str("meal_category", null));
        List<string> allFoodtypes = allRecipes.SelectMany(r => r.Arr("foodtypes").Select(f => f.AsStr())).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();

        List<string> activeFoodCuisine = c.UseState<List<string>>("activeFoodCuisine", null);
        List<string> activeDishCategory = c.UseState<List<string>>("activeDishCategory", null);
        List<string> activeMealCategory = c.UseState<List<string>>("activeMealCategory", null);
        List<string> activeFoodType = c.UseState<List<string>>("activeFoodType", null);

        // tg: :104  sortBy(data.material_occurences, [(material) => -material.occurences])
        List<UiValue> material_occurences = data.Arr("material_occurences").OrderBy(m => -m.Num("occurences")).ToList();
        string activeMaterial = State(c, "activeMaterial", () => material_occurences.Count > 0 ? material_occurences[0].Str("atom_id") : null);
        TABS tabMode = c.UseState("tabMode", TABS.category);

        Func<UiValue, bool> searchName = TguiFormat.CreateSearch<UiValue>(searchText, item => item.Str("name"));

        string getMaterialName(string atomId)
        {
            int idNum = int.TryParse(atomId, out int n) ? n : 0;
            UiValue atom = idNum >= 1 && idNum <= atom_data.Count ? atom_data[idNum - 1] : null;
            string name = atom?.Str("name");
            return string.IsNullOrEmpty(name) ? atomId : name;
        }

        Func<UiValue, bool> searchMaterial = TguiFormat.CreateSearch<UiValue>(searchText, material => getMaterialName(material.Str("atom_id")));

        List<UiValue> filteredMaterials = searchText.Length > 0 && tabMode == TABS.material
            ? material_occurences.Where(searchMaterial).ToList()
            : material_occurences;

        // tg: :129
        bool categoryMatch(UiValue recipe)
        {
            if (tabMode == TABS.material)
                return Keys(recipe.Get("reqs")).Contains(activeMaterial);
            else if (tabMode == TABS.category)
            {
                if (activeCategory == "Can Make")
                    return Craftable(data, recipe.Str("ref"));
                if (activeCategory == "Foods")
                {
                    if (activeFoodCuisine?.Count > 0 && !activeFoodCuisine.Contains(recipe.Str("cuisine_category")))
                        return false;
                    if (activeDishCategory?.Count > 0 && !activeDishCategory.Contains(recipe.Str("dish_category")))
                        return false;
                    if (activeMealCategory?.Count > 0 && !activeMealCategory.Contains(recipe.Str("meal_category")))
                        return false;
                    // only shows recipes with all the selected food types,
                    // rather than any recipe with any selected food type
                    if (activeFoodType?.Count > 0 && recipe.Arr("foodtypes").Count(ft => activeFoodType.Contains(ft.AsStr())) != activeFoodType.Count)
                        return false;
                }
                return recipe.Str("category") == activeCategory;
            }
            return true;
        }

        // DEVIATION(DV44): the owner (2026-10-16): while the name search or "craftable only" filters the recipes, the left column lists only the
        // categories that still hold a matching recipe (and "craftable only" the materials of a craftable one; tg lists them all), and an
        // active category or material the filter empties gives way to the first one left.
        bool filtering = searchText.Length > 0 || display_craftable_only;
        bool matchesFilter(UiValue recipe) => (!display_craftable_only || Craftable(data, recipe.Str("ref"))) && (searchText.Length == 0 || searchName(recipe));
        bool inCategory(UiValue recipe, string category) => category == "Can Make" ? Craftable(data, recipe.Str("ref")) : recipe.Str("category") == category;
        bool categoryShown(string category) => !filtering || allRecipes.Any(r => matchesFilter(r) && inCategory(r, category));
        // the material tab's search is tg's search of the material names (searchMaterial above), so only "craftable only" hides a material
        bool materialShown(string atomId) => !display_craftable_only || allRecipes.Any(r => Craftable(data, r.Str("ref")) && Keys(r.Get("reqs")).Contains(atomId));
        filteredMaterials = filteredMaterials.Where(m => materialShown(m.Str("atom_id"))).ToList();

        // tg: :190
        List<string> canMake = new() { "Can Make" };
        List<string> categories = canMake
            .Concat(data.Arr("categories").Select(x => x.AsStr()).OrderBy(x => x, StringComparer.Ordinal))
            .Where(i => i == "Weaponry" || !string.IsNullOrEmpty(i))
            .Where(categoryShown)
            .ToList();
        if (filtering && tabMode == TABS.category && searchText.Length == 0 && !categories.Contains(activeCategory) && categories.Count > 0)
            activeCategory = categories[0];
        if (filtering && tabMode == TABS.material && searchText.Length == 0 && filteredMaterials.Count > 0 && !filteredMaterials.Any(m => m.Str("atom_id") == activeMaterial))
            activeMaterial = filteredMaterials[0].Str("atom_id");

        // tg: :170
        List<UiValue> recipes = allRecipes.Where(recipe =>
            // If craftable only is selected, then filter by craftability
            (!display_craftable_only || Craftable(data, recipe.Str("ref"))) &&
            // Ignore categories and types when searching
            (searchText.Length > 0 || categoryMatch(recipe))).ToList();
        // tg: :178  sortBy(recipes, [(recipe) => [activeCategory === 'Can Make' ? 99 - reqs : Number(craftable), recipe.name.toLowerCase()]])
        recipes = recipes
            .OrderBy(recipe => activeCategory == "Can Make" ? 99 - Keys(recipe.Get("reqs")).Count() : Craftable(data, recipe.Str("ref")) ? 1 : 0)
            .ThenBy(recipe => recipe.Str("name").ToLowerInvariant(), StringComparer.Ordinal)
            .ToList();
        if (searchText.Length > 0)
            recipes = recipes.Where(searchName).ToList();

        int pageSize = searchText.Length > 0
            ? display_compact
                ? 20
                : 10
            : display_compact
                ? 60
                : 30;
        int displayLimit = pageSize * pages;

        // tg: :208  the left column
        var tabsNode = new UiNode(UiKind.Tabs) { Vertical = true };
        if (tabMode == TABS.material)
        {
            foreach (UiValue material in filteredMaterials)
            {
                string atomId = material.Str("atom_id");
                tabsNode.With(new UiNode(UiKind.Tab)
                {
                    Key = "material:" + atomId,
                    Selected = activeMaterial == atomId && searchText.Length == 0,
                    OnClick = () =>
                    {
                        c.SetState("activeMaterial", (object)atomId);
                        c.SetState("pages", 1);
                        if (searchText.Length > 0)
                            c.SetState("searchText", "");
                    },
                }.With(MaterialContent(data, atomId, material.Num("occurences"))));
            }
        }
        if (tabMode == TABS.category)
        {
            foreach (string category in categories)
            {
                string cat = category;
                var tabContent = new UiNode(UiKind.Stack) { Vertical = true }.With(
                    new UiNode(UiKind.StackItem).With(
                        new UiNode(UiKind.Stack).With(
                            new UiNode(UiKind.StackItem) { Width = "14px", TextAlign = "center" }.With(
                                new UiNode(UiKind.Icon) { Icon = getFAIcon(cat, mode), Color = cat == "Blood Cult" ? "red" : null }),
                            new UiNode(UiKind.StackItem) { Grow = 1, Color = cat == "Blood Cult" ? "red" : null }.With(
                                new UiNode(UiKind.Box) { Text = cat, Color = cat == "Blood Cult" ? "red" : null }),
                            cat == "Can Make"
                                ? new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = CraftableCount(data).ToString(System.Globalization.CultureInfo.InvariantCulture) })
                                : null)));
                // Foods are further categorized by cuisine and dish type
                if (cat == "Foods" && activeCategory == cat)
                    tabContent.With(new UiNode(UiKind.StackItem) { FontSize = 0.95 }.With(FoodFilters(c, data, mode, diet,
                        allFoodCuisines, allDishCategories, allMealCategories, allFoodtypes,
                        activeFoodCuisine, activeDishCategory, activeMealCategory, activeFoodType)));
                tabsNode.With(new UiNode(UiKind.Tab)
                {
                    Key = "category:" + cat,
                    Selected = activeCategory == cat && searchText.Length == 0,
                    OnClick = () =>
                    {
                        c.SetState("activeCategory", (object)cat);
                        c.SetState("pages", 1);
                        if (searchText.Length > 0)
                            c.SetState("searchText", "");
                    },
                }.With(tabContent));
            }
        }

        var left = new UiNode(UiKind.Section) { Fill = true }.With(
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true, Justify = "space-between" }.With(
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Input)
                    {
                        Key = "search",
                        Fluid = true,
                        Tooltip = "Search in " + allRecipes.Count + (mode ? " recipes..." : " designs..."),
                        TextValue = searchText,
                        OnInput = value =>
                        {
                            c.SetState("pages", 1);
                            c.SetState("searchText", value);
                        },
                    }),
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Tabs) { Fluid = true, TextAlign = "center" }.With(
                        new UiNode(UiKind.Tab)
                        {
                            Key = "tab:category",
                            Text = "Category",
                            Selected = tabMode == TABS.category,
                            OnClick = () =>
                            {
                                if (tabMode == TABS.category)
                                    return;
                                c.SetState("tabMode", TABS.category);
                                c.SetState("pages", 1);
                                IReadOnlyList<UiValue> dataCategories = data.Arr("categories");
                                c.SetState("activeCategory", (object)(CraftableCount(data) > 0 ? "Can Make" : dataCategories.Count > 0 ? dataCategories[0].AsStr() : null));
                            },
                        },
                        new UiNode(UiKind.Tab)
                        {
                            Key = "tab:material",
                            Text = mode ? "Ingredient" : "Material",
                            Selected = tabMode == TABS.material,
                            OnClick = () =>
                            {
                                if (tabMode == TABS.material)
                                    return;
                                c.SetState("tabMode", TABS.material);
                                c.SetState("pages", 1);
                                c.SetState("activeMaterial", (object)(material_occurences.Count > 0 ? material_occurences[0].Str("atom_id") : null));
                            },
                        })),
                new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true }.With(tabsNode),
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Divider) { Rule = true },
                    new UiNode(UiKind.Button)
                    {
                        Key = "toggle_recipes",
                        Fluid = true,
                        Checkbox = display_craftable_only,
                        Text = "Can make only",
                        OnClick = () => c.Act("toggle_recipes"),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "toggle_compact",
                        Fluid = true,
                        Checkbox = display_compact,
                        Text = "Compact list",
                        OnClick = () => c.Act("toggle_compact"),
                    }),
                !forced_mode
                    ? new UiNode(UiKind.StackItem).With(
                        new UiNode(UiKind.Stack) { TextAlign = "center" }.With(
                            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                                new UiNode(UiKind.Button)
                                {
                                    Key = "mode:craft",
                                    Fluid = true,
                                    Checkbox = !mode,
                                    Icon = "hammer",
                                    Text = "Craft",
                                    Color = !mode ? "good" : null,
                                    OnClick = () =>
                                    {
                                        if (!mode)
                                            return;
                                        c.SetState("tabMode", TABS.category);
                                        c.SetState("activeCategory", (object)DEFAULT_CAT_CRAFTING);
                                        c.Act("toggle_mode");
                                    },
                                }),
                            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                                new UiNode(UiKind.Button)
                                {
                                    Key = "mode:cook",
                                    Fluid = true,
                                    Checkbox = mode,
                                    Icon = "utensils",
                                    Text = "Cook",
                                    Color = mode ? "good" : null,
                                    OnClick = () =>
                                    {
                                        if (mode)
                                            return;
                                        c.SetState("tabMode", TABS.category);
                                        c.SetState("activeCategory", (object)DEFAULT_CAT_COOKING);
                                        c.Act("toggle_mode");
                                    },
                                })))
                    : null));

        // tg: :561  the recipe list
        var right = new UiNode(UiKind.Section) { Key = "recipes", Fill = true, Scrollable = true };
        if (recipes.Count > 0)
        {
            foreach (UiValue item in recipes.Take(displayLimit))
                right.With(display_compact ? RecipeContentCompact(c, item) : RecipeContent(c, item));
        }
        else
            right.With(new UiNode(UiKind.NoticeBox) { Text = "No recipes found." });
        if (recipes.Count > displayLimit)
        {
            right.With(new UiNode(UiKind.Section)
            {
                Key = "load-more",
                TextAlign = "center",
                OnClick = () => c.SetState("pages", pages + 1),
            }.With(new UiNode(UiKind.Box) { TextAlign = "center", Text = "Load " + Math.Min(pageSize, recipes.Count - displayLimit) + " more..." }));
        }

        return c.Window(700, 720, null,
            new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Width = "200px" }.With(left),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(right)));
    }

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/index.tsx:352-479 (the food filters under the active "Foods" tab)
    private static UiNode FoodFilters(TguiContext c, UiValue data, bool mode, UiValue diet, List<string> cuisines, List<string> dishes,
        List<string> meals, List<string> foodtypes, List<string> activeCuisine, List<string> activeDish, List<string> activeMeal, List<string> activeType)
    {
        var column = new UiNode(UiKind.Stack) { Vertical = true };
        void Group(string title, List<string> values, List<string> active, string key, bool icons, bool transparent)
        {
            column.With(new UiNode(UiKind.StackItem).With(SubGroupTitle(title)));
            foreach (string value in values)
            {
                string v = value;
                column.With(new UiNode(UiKind.StackItem) { Key = key + ":" + v }.With(new UiNode(UiKind.Button)
                {
                    Key = key + ":" + v,
                    Fluid = true,
                    Color = transparent ? "transparent" : null,
                    Checkbox = active?.Contains(v) ?? false,
                    Icon = icons ? getFAIcon(v, mode) : null,
                    Text = icons ? v : null,
                    OnClick = () =>
                    {
                        c.SetState(key, toggleArrayItem(active, v));
                        c.SetState("pages", 1);
                    },
                }.With(icons ? null : FoodtypeContent(v, diet, CraftableCount(data)))));
            }
        }
        Group("Cuisines", cuisines, activeCuisine, "activeFoodCuisine", true, true);
        Group("Dishes", dishes, activeDish, "activeDishCategory", true, false);
        Group("Meals", meals, activeMeal, "activeMealCategory", true, false);
        Group("Type", foodtypes, activeType, "activeFoodType", false, false);
        return column;
    }

    // ---- content/RecipeContent.tsx ----

    private static string AtomName(UiValue data, int atom_id) => atom_id >= 1 && atom_id <= data.Arr("atom_data").Count ? data.Arr("atom_data")[atom_id - 1].Str("name", null) : null;

    private static bool AtomIsReagent(UiValue data, int atom_id) => atom_id >= 1 && atom_id <= data.Arr("atom_data").Count && data.Arr("atom_data")[atom_id - 1].Truthy("is_reagent");

    private static string AmountText(string name, bool is_reagent, double amount) => is_reagent
        ? name + " " + TguiFormat.JsNumber(amount) + "u"
        : amount > 1
            ? name + " " + TguiFormat.JsNumber(amount) + "x"
            : name;

    private static UiNode Picture(UiValue data, double atom_id, double scale = 1)
    {
        (string icon, string state) = findIcon(atom_id, data);
        return new UiNode(UiKind.Box) { DmIcon = icon, DmIconState = state, Size = scale };
    }

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/content/RecipeContent.tsx:23
    public static UiNode RecipeContentCompact(TguiContext c, UiValue item)
    {
        UiValue data = c.Data;
        var parts = new List<string>();
        // reqs
        parts.Add(string.Join(", ", Keys(item.Get("reqs")).Select(id =>
        {
            int atom_id = int.TryParse(id, out int n) ? n : 0;
            return AmountText(AtomName(data, atom_id), AtomIsReagent(data, atom_id), item.Get("reqs").Num(id));
        })));
        string text = parts[0];
        if (item.Get("chem_catalysts") is { Kind: UiValueKind.Object } catalysts)
            text += ", " + string.Join(", ", Keys(catalysts).Select(id =>
            {
                int atom_id = int.TryParse(id, out int n) ? n : 0;
                return AmountText(AtomName(data, atom_id), AtomIsReagent(data, atom_id), catalysts.Num(id));
            }));
        foreach (string list in new[] { "tool_paths", "machinery", "structures" })
        {
            if (item.Get(list) is { Kind: UiValueKind.Array } ids)
                text += ", " + string.Join(", ", ids.Items.Select(x => AtomName(data, (int)x.AsNum())));
        }

        UiNode right;
        if (!item.Truthy("non_craftable"))
        {
            right = new UiNode(UiKind.Box).With(
                item.Get("tool_behaviors") is { Kind: UiValueKind.Array } behaviors
                    ? new UiNode(UiKind.Icon) { Icon = "screwdriver-wrench", Tooltip = "Tools: " + string.Join(", ", behaviors.Items.Select(b => b.AsStr())) }
                    : null,
                CraftButton(c, item));
        }
        else
        {
            right = item.Get("steps") is { Kind: UiValueKind.Array } steps
                ? new UiNode(UiKind.Box) { FontSize = 1.5 }.With(new UiNode(UiKind.Icon) { Icon = "circle-question-o", Tooltip = string.Join("\n", steps.Items.Select(s => s.AsStr())) })
                : null;
        }

        return new UiNode(UiKind.Section) { Key = "recipe:" + item.Str("ref") }.With(
            new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(Picture(data, item.Num("id"))),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Stack).With(
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box).With( // block flow
                            new UiNode(UiKind.Box) { Bold = true, Text = TguiFormat.CssCapitalize(item.Str("name")) },
                            new UiNode(UiKind.Box) { Color = "gray", Text = TguiFormat.CssCapitalize(text), Wrap = true })),
                        new UiNode(UiKind.StackItem).With(right)))));
    }

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/content/RecipeContent.tsx:129
    public static UiNode RecipeContent(TguiContext c, UiValue item, bool nodesc = false, bool showIcon = true)
    {
        UiValue data = c.Data;
        bool mode = Mode(data);
        UiValue diet = data.Get("diet");

        var details = new UiNode(UiKind.Box);
        UiValue reqs = item.Get("reqs");
        if (reqs is { Kind: UiValueKind.Object } && reqs.Fields.Count > 0)
        {
            var box = new UiNode(UiKind.Box).With(GroupTitle(mode ? "Ingredients" : "Materials"));
            foreach (string atom_id in Keys(reqs))
                box.With(AtomContent(c, atom_id, reqs.Num(atom_id)));
            details.With(box);
        }
        if (item.Get("chem_catalysts") is { Kind: UiValueKind.Object } catalysts)
        {
            var box = new UiNode(UiKind.Box).With(GroupTitle("Catalysts"));
            foreach (string atom_id in Keys(catalysts))
                box.With(AtomContent(c, atom_id, catalysts.Num(atom_id)));
            details.With(box);
        }
        UiValue toolPaths = item.Get("tool_paths");
        UiValue toolBehaviors = item.Get("tool_behaviors");
        if (toolPaths is { Kind: UiValueKind.Array } || toolBehaviors is { Kind: UiValueKind.Array })
        {
            var box = new UiNode(UiKind.Box).With(GroupTitle("Tools"));
            foreach (UiValue tool in toolPaths?.Items ?? Array.Empty<UiValue>())
                box.With(AtomContent(c, tool.AsStr(), 1));
            foreach (UiValue tool in toolBehaviors?.Items ?? Array.Empty<UiValue>())
                box.With(ToolContent(tool.AsStr()));
            details.With(box);
        }
        foreach ((string list, string title) in new[] { ("machinery", "Machinery"), ("structures", "Structures") })
        {
            if (item.Get(list) is { Kind: UiValueKind.Array } ids)
            {
                var box = new UiNode(UiKind.Box).With(GroupTitle(title));
                foreach (UiValue atom_id in ids.Items)
                    box.With(AtomContent(c, atom_id.AsStr(), 1));
                details.With(box);
            }
        }

        UiNode steps = null;
        if (item.Arr("steps").Count > 0)
        {
            steps = new UiNode(UiKind.Box).With(GroupTitle("Steps"));
            foreach (UiValue step in item.Arr("steps"))
                steps.With(new UiNode(UiKind.Box) { Text = "• " + step.AsStr(), Wrap = true }); // <ul><li>
        }

        var middle = new UiNode(UiKind.StackItem) { Grow = 5 }.With(new UiNode(UiKind.Box).With( // a Stack.Item's children flow as blocks
            !nodesc || item.Truthy("non_craftable")
                ? new UiNode(UiKind.Box) { Bold = true, Text = TguiFormat.CssCapitalize(item.Str("name")) }
                : CraftButton(c, item, item.Str("name")),
            !nodesc && !string.IsNullOrEmpty(item.Str("desc")) ? new UiNode(UiKind.Box) { Color = "gray", Text = item.Str("desc"), Wrap = true } : null,
            item.Truthy("has_food_effect")
                ? new UiNode(UiKind.Box) { Color = "pink" }.With(new UiNode(UiKind.Icon) { Icon = "wand-magic-sparkles" }, new UiNode(UiKind.Box) { Color = "pink", Text = "Special effect on consumption." })
                : null,
            details,
            steps));

        UiNode side = null;
        if (!nodesc)
        {
            var sideItem = new UiNode(UiKind.StackItem);
            if (item.Num("complexity") != 0)
                sideItem.With(new UiNode(UiKind.Box) { Color = "gray", Width = "104px", Text = "Complexity: " + TguiFormat.JsNumber(item.Num("complexity")) });
            if (item.Arr("foodtypes").Count > 0)
            {
                var types = new UiNode(UiKind.Box) { Color = "gray", Width = "104px" }.With(new UiNode(UiKind.Divider) { Rule = true });
                foreach (UiValue foodtype in item.Arr("foodtypes"))
                    types.With(FoodtypeContent(foodtype.AsStr(), diet, null));
                sideItem.With(types);
            }
            side = new UiNode(UiKind.StackItem) { Grow = 2 }.With(
                new UiNode(UiKind.Stack) { Vertical = true }.With(
                    new UiNode(UiKind.StackItem).With(CraftButton(c, item)),
                    sideItem));
        }

        return new UiNode(UiKind.Section) { Key = "recipe:" + item.Str("ref") + (nodesc ? ":floating" : "") }.With(
            new UiNode(UiKind.Stack).With(
                showIcon ? new UiNode(UiKind.StackItem) { MinWidth = "64px" }.With(Picture(data, item.Num("id"), 1.5)) : null,
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Stack).With(middle, side))));
    }

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/content/RecipeContent.tsx:297
    public static UiNode CraftButton(TguiContext c, UiValue item, string buttonText = null)
    {
        UiValue data = c.Data;
        bool busy = data.Truthy("busy");
        bool mode = Mode(data);
        if (item.Truthy("non_craftable")) return null;
        bool craftable = !item.Truthy("non_craftable") && Craftable(data, item.Str("ref"));
        string reference = item.Str("ref");

        return new UiNode(UiKind.Stack) { Key = "craft:" + reference }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "make:" + reference,
                    Fluid = true,
                    TextAlign = "center",
                    Disabled = !craftable || busy,
                    Icon = busy
                        ? "circle-notch"
                        : mode
                            ? "utensils"
                            : "hammer",
                    Text = buttonText ?? "Make",
                    OnClick = () => c.Act("make", ("recipe", reference)),
                }),
            new UiNode(UiKind.StackItem).With(
                item.Truthy("mass_craftable")
                    ? new UiNode(UiKind.Button)
                    {
                        Key = "make_mass:" + reference,
                        MinWidth = "30px",
                        TextAlign = "center",
                        Tooltip = "Repeat this craft until you run out of ingredients.",
                        Disabled = !craftable || busy,
                        Icon = "repeat",
                        OnClick = () => c.Act("make_mass", ("recipe", reference)),
                    }
                    : null));
    }

    // ---- content/AtomContent.tsx ----

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/content/AtomContent.tsx:14
    public static UiNode AtomContent(TguiContext c, string atom_id, double amount)
    {
        UiValue data = c.Data;
        int atom_id_int = int.TryParse(atom_id, out int n) ? n : 0;

        // check if the atom itself has a recipe associated
        List<UiValue> hasRecipe = data.Arr("recipes").Where(recipe => (int)recipe.Num("id") == atom_id_int).ToList();
        if (hasRecipe.Count <= 0)
            return AtomContentInner(data, atom_id_int, amount, false);

        string key = "shownRecipe:" + atom_id;
        int shown = Math.Clamp(c.UseState(key, 0), 0, hasRecipe.Count - 1);
        UiValue recipe = hasRecipe[shown];
        // add a tooltip to recursively show the recipe for this atom (built when it opens, UiNode.Content)
        UiNode Floating()
        {
            var floating = new UiNode(UiKind.Box) { Key = "floating:" + atom_id };
            if (hasRecipe.Count > 1)
            {
                floating.With(new UiNode(UiKind.Stack) { Align = "center", Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button)
                    {
                        Key = "prev:" + atom_id,
                        Fluid = true,
                        Icon = "arrow-left",
                        TextAlign = "center",
                        OnClick = () => c.SetState(key, (shown - 1 + hasRecipe.Count) % hasRecipe.Count),
                    }),
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button)
                    {
                        Key = "next:" + atom_id,
                        Fluid = true,
                        Icon = "arrow-right",
                        TextAlign = "center",
                        OnClick = () => c.SetState(key, (shown + 1) % hasRecipe.Count),
                    })));
            }
            return floating.With(RecipeContent(c, recipe, nodesc: true, showIcon: false));
        }

        // tgui-core's Floating renders its content only while open; built at once, two recipes that need each other recursed without end
        return new UiNode(UiKind.Floating) { Key = "atom:" + atom_id, Content = Floating }.With(
            new UiNode(UiKind.Box).With(AtomContentInner(data, atom_id_int, amount, true)));
    }

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/content/AtomContent.tsx:138
    private static UiNode AtomContentInner(UiValue data, int atom_id, double amount, bool underline)
    {
        UiValue atom = atom_id >= 1 && atom_id <= data.Arr("atom_data").Count ? data.Arr("atom_data")[atom_id - 1] : null;
        string name = atom?.Str("name") ?? "";
        string suffix = atom != null && atom.Truthy("is_reagent") ? " " + TguiFormat.JsNumber(amount) + "u" : amount > 1 ? " " + TguiFormat.JsNumber(amount) + "x" : "";
        return new UiNode(UiKind.Stack) { Key = "atomrow:" + atom_id }.With(
            new UiNode(UiKind.StackItem).With(Picture(data, atom_id)),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = TguiFormat.CssCapitalize(name) + suffix, Underline = underline }));
    }

    // ---- content/MaterialContent.tsx ----

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/content/MaterialContent.tsx:12
    public static UiNode MaterialContent(UiValue data, string atom_id_text, double occurences)
    {
        int atom_id = int.TryParse(atom_id_text, out int n) ? n : 0;
        string name = AtomName(data, atom_id) ?? "";
        return new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem).With(Picture(data, atom_id)),
            new UiNode(UiKind.StackItem) { Grow = 1, Align = "center" }.With(new UiNode(UiKind.Box) { Text = TguiFormat.CssCapitalize(name) }),
            new UiNode(UiKind.StackItem) { Align = "center" }.With(new UiNode(UiKind.Box) { Text = TguiFormat.JsNumber(occurences) }));
    }

    // ---- content/ToolContent.tsx ----

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/content/ToolContent.tsx:8
    public static UiNode ToolContent(string tool)
    {
        (string icon, string state) = ToolIcons.TryGetValue(tool ?? "", out var picture) ? picture : (null, null);
        return new UiNode(UiKind.Stack) { Key = "tool:" + tool }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { DmIcon = icon, DmIconState = state }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = tool }));
    }

    // ---- content/FoodtypeContent.tsx ----

    // tg: tgui/packages/tgui/interfaces/PersonalCrafting/content/FoodtypeContent.tsx:12
    public static UiNode FoodtypeContent(string type, UiValue diet, int? craftableCount)
    {
        string iconName = "", iconColor = "";

        // We use iconName in the return to see if this went through.
        if (type != "Can Make" && diet != null && diet.Kind == UiValueKind.Object)
        {
            if (diet.Arr("liked_food").Any(x => x.AsStr() == type))
            {
                iconName = "face-laugh-beam";
                iconColor = "good";
            }
            else if (diet.Arr("disliked_food").Any(x => x.AsStr() == type))
            {
                iconName = "face-tired";
                iconColor = "average";
            }
            else if (diet.Arr("toxic_food").Any(x => x.AsStr() == type))
            {
                iconName = "skull-crossbones";
                iconColor = "bad";
            }
        }

        return new UiNode(UiKind.Stack) { Key = "foodtype:" + type }.With(
            new UiNode(UiKind.StackItem) { Width = "14px", TextAlign = "center" }.With(new UiNode(UiKind.Icon) { Icon = TYPE_ICONS.TryGetValue(type ?? "", out string icon) ? icon : "circle" }),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box) { Text = TguiFormat.CssCapitalize((type ?? "").ToLowerInvariant()) }),
            new UiNode(UiKind.StackItem).With(type == "Can Make"
                ? new UiNode(UiKind.Box) { Text = (craftableCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture) }
                : iconName.Length > 0 ? new UiNode(UiKind.Icon) { Icon = iconName, Color = iconColor } : null));
    }
}
