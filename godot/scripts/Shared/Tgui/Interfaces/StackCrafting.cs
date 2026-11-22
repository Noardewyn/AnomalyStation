using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/StackCrafting.tsx (M5 slice 3, D148): the stack
// recipe window (tg's /obj/item/stack ui_static_data "recipes" and ui_data "amount"; the stacks come in slice 2).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class StackCrafting
{
    /**
     * Type guard for recipe vs recipe list
     */
    // tg: tgui/packages/tgui/interfaces/StackCrafting.tsx:59
    public static bool IsRecipeList(UiValue value) => value.Get("ref") == null;

    /**
     * Filter recipe list by keys, resursing into subcategories.
     * Returns the filtered list, or undefined, if there is no list left.
     */
    // tg: tgui/packages/tgui/interfaces/StackCrafting.tsx:69
    public static List<KeyValuePair<string, UiValue>> FilterRecipeList(IReadOnlyList<KeyValuePair<string, UiValue>> list, Func<string, bool> keyFilter)
    {
        var filteredList = list
            .SelectMany(entry =>
            {
                (string key, UiValue recipe) = (entry.Key, entry.Value);

                // If category name matches, return the whole thing.
                if (keyFilter(key))
                    return new[] { entry };

                if (recipe.Kind == UiValueKind.Object && IsRecipeList(recipe))
                {
                    // otherwise, filter sub-entries.
                    List<KeyValuePair<string, UiValue>> subEntries = FilterRecipeList(recipe.Fields, keyFilter);
                    if (subEntries != null)
                        return new[] { new KeyValuePair<string, UiValue>(key, UiValue.ObjectOf(subEntries)) };
                }

                return Array.Empty<KeyValuePair<string, UiValue>>();
            })
            // Sort items so that lists are on top and recipes underneath.
            // Plus everything is in alphabetical order.
            .OrderBy(e => IsRecipeList(e.Value) ? 0 : 1)
            .ThenBy(e => e.Key, Comparer<string>.Create(TguiFormat.LocaleCompare))
            .ToList();

        return filteredList.Count > 0 ? filteredList : null;
    }

    // tg: tgui/packages/tgui/interfaces/StackCrafting.tsx:106
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double amount = data.Num("amount");
        IReadOnlyList<KeyValuePair<string, UiValue>> recipes = data.Obj("recipes");

        string searchText = c.UseState("searchText", "");
        Func<string, bool> testSearch = TguiFormat.CreateSearch<string>(searchText, item => item);
        List<KeyValuePair<string, UiValue>> filteredRecipes = FilterRecipeList(recipes, testSearch);

        double height = TguiFormat.Clamp(96 + recipes.Count * 37, 250, 500);

        return c.Window(400, height, null,
            new UiNode(UiKind.Section)
            {
                Fill = true,
                Scrollable = true,
                Title = "Amount: " + TguiFormat.JsNumber(amount),
                // tg: :124  <SearchBar expensive style={{ width: '15em' }} query={searchText} onSearch={...} />
                Buttons = new UiNode(UiKind.Input)
                {
                    Key = "search",
                    Icon = "search",
                    Width = "15em",
                    TextValue = searchText,
                    Tooltip = "Search...",
                    OnInput = value => c.SetState("searchText", value),
                },
            }.With(
                filteredRecipes != null
                    ? RecipeListBox(c, filteredRecipes, amount, "")
                    : new UiNode(UiKind.NoticeBox) { Text = "No recipes found." }));
    }

    // tg: tgui/packages/tgui/interfaces/StackCrafting.tsx:142
    private static UiNode RecipeListBox(TguiContext c, IReadOnlyList<KeyValuePair<string, UiValue>> recipes, double amount, string path)
    {
        var box = new UiNode(UiKind.Fragment);
        foreach ((string title, UiValue recipe) in recipes)
        {
            if (recipe.Kind == UiValueKind.Object && IsRecipeList(recipe))
            {
                box.With(new UiNode(UiKind.Collapsible) { Key = path + "/" + title, Title = TguiFormat.ToTitleCase(title ?? "") }.With(
                    RecipeListBox(c, recipe.Fields, amount, path + "/" + title)));
            }
            else
                box.With(RecipeBox(c, title, recipe, amount));
        }
        return box;
    }

    // tg: tgui/packages/tgui/interfaces/StackCrafting.tsx:174
    public static double BuildMultiplier(UiValue recipe, double amount)
    {
        if (recipe.Num("req_amount") > amount)
            return 0;

        return Math.Floor(amount / recipe.Num("req_amount"));
    }

    // tg: tgui/packages/tgui/interfaces/StackCrafting.tsx:182
    private static UiNode Multipliers(TguiContext c, UiValue recipe, double maxMultiplier)
    {
        double maxM = Math.Min(maxMultiplier, Math.Floor(recipe.Num("max_res_amount") / recipe.Num("res_amount")));
        double[] multipliers = { 5, 10, 25 };
        var finalResult = new UiNode(UiKind.Fragment);
        UiValue reference = recipe.Get("ref");

        foreach (double multiplier in multipliers)
        {
            if (maxM >= multiplier)
            {
                double m = multiplier;
                finalResult.With(new UiNode(UiKind.Button)
                {
                    Key = "x" + TguiFormat.JsNumber(m),
                    Bold = true,
                    Color = "transparent",
                    FontSize = 0.75,
                    Width = "32px",
                    OnClick = () => c.Act("make", ("ref", reference), ("multiplier", m)),
                    Text = TguiFormat.JsNumber(m * recipe.Num("res_amount")) + "x",
                });
            }
        }

        if (Array.IndexOf(multipliers, maxM) == -1)
        {
            finalResult.With(new UiNode(UiKind.Button)
            {
                Key = "xmax",
                Bold = true,
                Color = "transparent",
                FontSize = 0.75,
                Width = "32px",
                OnClick = () => c.Act("make", ("ref", reference), ("multiplier", maxM)),
                Text = TguiFormat.JsNumber(maxM * recipe.Num("res_amount")) + "x",
            });
        }

        return finalResult;
    }

    // tg: tgui/packages/tgui/interfaces/StackCrafting.tsx:243
    private static UiNode RecipeBox(TguiContext c, string title, UiValue recipe, double amount)
    {
        double res_amount = recipe.Num("res_amount");
        double max_res_amount = recipe.Num("max_res_amount");
        double req_amount = recipe.Num("req_amount");
        UiValue reference = recipe.Get("ref");

        string resAmountLabel = res_amount > 1 ? TguiFormat.JsNumber(res_amount) + "x " : "";
        string sheetSuffix = req_amount > 1 ? "s" : "";
        string buttonName = resAmountLabel + title;
        string reqSheets = TguiFormat.JsNumber(req_amount) + " sheet" + sheetSuffix;

        double maxMultiplier = BuildMultiplier(recipe, amount);

        return new UiNode(UiKind.ImageButton)
        {
            Key = title,
            Fluid = true,
            DmIcon = recipe.Str("icon"),
            DmIconState = recipe.Str("icon_state"),
            Size = 32,
            Disabled = maxMultiplier == 0,
            Buttons = max_res_amount > 1 && maxMultiplier > 1 ? Multipliers(c, recipe, maxMultiplier) : null,
            OnClick = () => c.Act("make", ("ref", reference), ("multiplier", 1)),
        }.With(
            new UiNode(UiKind.Stack) { TextAlign = "left" }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box) { Text = TguiFormat.ToTitleCase(buttonName) }),
                new UiNode(UiKind.StackItem) { Align = "center" }.With(new UiNode(UiKind.Box) { FontSize = 0.8, Color = "gray", Text = reqSheets })));
    }

    /// <summary>The recipe count of a recipe list, for the window's height (tgui's <c>Object.keys(recipes).length</c>).</summary>
    public static int TopLevelCount(UiValue data) => data.Obj("recipes").Count;

    internal static string Invariant(double d) => d.ToString(CultureInfo.InvariantCulture);
}
