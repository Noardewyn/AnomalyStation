using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Reagents/index.tsx, Lookup.tsx, TagBox.tsx,
// RecipeLibrary.tsx and common/RecipeLookup.jsx, common/ReagentLookup.jsx (gaps package P5): the reaction search the chem dispenser
// opens on its beaker. The window reads the holder's ui_data (holder/ui_data.dm:115: the selected reagent and recipe, the tag filter,
// the beaker's reagents) and ui_static_data (:250: every reaction and the tags) and sends tg's acts. tg keeps the bookmarks in a
// module-level Set shared by every window; the port keeps them in the window's state (one list per open window). The reagent's wiki
// button runs a BYOND verb (Byond.command) that the port has not: it is left out. The rate profile's Chart.Line draws over its own data
// range, as tgui-core's does when no range is given. Original copyright and license: tgstation, AGPL-3.0 (code). See
// ../tgstation/LICENSE.

public static class Reagents
{
    // tg: Reagents/index.tsx:14
    public static UiNode Render(TguiContext c)
    {
        // tg: index.tsx:15  const pageState = useState(1)
        return c.Window(720, 850, null,
            new UiNode(UiKind.Stack) { Key = "root", Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem).With(Lookup(c)),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Section) { Key = "tags", Title = "Tags" }.With(TagBox(c))),
                new UiNode(UiKind.StackItem) { Grow = 2 }.With(RecipeLibrary(c))));
    }

    private static List<string> Bookmarks(TguiContext c) => c.UseState("bookmarks", new List<string>());

    // tg: Reagents/Lookup.tsx:11  Lookup
    private static UiNode Lookup(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue recipe = data.Get("reagent_mode_recipe");
        UiValue reagent = data.Get("reagent_mode_reagent");
        bool hasRecipe = recipe?.Truthy ?? false, hasReagent = reagent?.Truthy ?? false;
        return new UiNode(UiKind.Stack) { Key = "lookup", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section)
            {
                Key = "recipeLookup",
                Title = "Recipe lookup",
                MinWidth = "353px",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "sync",
                        Icon = "atom",
                        Color = data.Truthy("beakerSync") ? "green" : "red",
                        Tooltip = "When enabled the displayed reaction will automatically display ongoing reactions in the associated beaker.",
                        Text = "Beaker Sync",
                        OnClick = () => c.Act("beaker_sync"),
                    },
                    new UiNode(UiKind.Button) { Key = "searchRecipe", Icon = "search", Color = "purple", Tooltip = "Search for a recipe by product name", Text = "Search", OnClick = () => c.Act("search_recipe") },
                    new UiNode(UiKind.Button) { Key = "clearRecipe", Icon = "times", Color = "red", Disabled = !hasRecipe, OnClick = () => c.Act("recipe_click", ("id", null)) }),
            }.With(RecipeLookup(c, hasRecipe ? recipe : null))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section)
            {
                Key = "reagentLookup",
                Title = "Reagent lookup",
                MinWidth = "300px",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button) { Key = "searchReagent", Icon = "search", Tooltip = "Search for a reagent by name", Text = "Search", OnClick = () => c.Act("search_reagents") },
                    new UiNode(UiKind.Button) { Key = "clearReagent", Icon = "times", Color = "red", Disabled = !hasReagent, OnClick = () => c.Act("reagent_click", ("id", null)) }),
            }.With(ReagentLookup(c, hasReagent ? reagent : null))));
    }

    // tg: Reagents/TagBox.tsx:9  TagBox
    private static UiNode TagBox(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue bitflags = data.Get("bitflags");
        int selectedBitflags = (int)data.Num("selectedBitflags");
        List<string> allCategories = ReagentBitflags.BitflagInfo.Select(meta => meta.Category).Distinct().ToList();
        UiNode list = new UiNode(UiKind.LabeledList) { Key = "tagList" };
        foreach (string category in allCategories)
        {
            UiNode item = new UiNode(UiKind.LabeledItem) { Key = "cat:" + category, Label = category };
            foreach (ReagentBitflags.Info meta in ReagentBitflags.BitflagInfo.Where(m => m.Category == category))
            {
                int flag = (int)(bitflags?.Num(meta.Flag) ?? 0);
                item.With(new UiNode(UiKind.Button)
                {
                    Key = meta.Flag,
                    Selected = (selectedBitflags & flag) != 0,
                    Icon = meta.Icon,
                    Tooltip = meta.Tooltip,
                    Text = meta.Flag,
                    OnClick = () =>
                    {
                        c.Act(meta.Toggle);
                        c.SetState("page", 1.0);
                    },
                });
            }
            list.With(item);
        }
        return list;
    }

    // tg: Reagents/RecipeLibrary.tsx:15  matchBitflag
    private static bool MatchBitflag(int a, int b) => (a & b) != 0 && (a | b) == b;

    // tg: Reagents/RecipeLibrary.tsx:19  bitflagToIcon
    private static string BitflagToIcon(int flag, UiValue allFlags)
    {
        foreach (KeyValuePair<string, UiValue> entry in allFlags?.Fields ?? (IReadOnlyList<KeyValuePair<string, UiValue>>)Array.Empty<KeyValuePair<string, UiValue>>())
            foreach (ReagentBitflags.Info meta in ReagentBitflags.BitflagInfo)
                if (meta.Flag == entry.Key && (int)entry.Value.AsNum() == flag)
                    return meta.Icon;
        return null;
    }

    // tg: Reagents/RecipeLibrary.tsx:30  RecipeLibrary
    private static UiNode RecipeLibrary(TguiContext c)
    {
        UiValue data = c.Data;
        double page = c.UseState("page", 1.0);
        UiValue bitflags = data.Get("bitflags");
        IReadOnlyList<UiValue> currentReagents = data.Arr("currentReagents");
        HashSet<string> current = currentReagents.Select(r => r.AsStr()).ToHashSet(StringComparer.Ordinal);
        int selectedBitflags = (int)data.Num("selectedBitflags");
        bool reagentFilter = c.UseState("reagentFilter", true);
        bool bookmarkMode = c.UseState("bookmarkMode", false);
        List<string> bookmarks = Bookmarks(c);
        IReadOnlyList<UiValue> masterList = data.Arr("master_reaction_list");

        // tg: :44  matchReagents: every reagent in the beaker is a reactant
        bool MatchReagents(UiValue reaction) =>
            !reagentFilter || reaction.Arr("reactants").Count(reactant => current.Contains(reactant.Str("id"))) == currentReagents.Count;

        List<UiValue> visibleReactions = bookmarkMode
            ? bookmarks.Select(id => masterList.FirstOrDefault(r => r.Str("id") == id)).Where(r => r != null).ToList()
            : masterList.Where(reaction => (selectedBitflags == 0 || MatchBitflag(selectedBitflags, (int)reaction.Num("bitflags"))) && MatchReagents(reaction)).ToList();
        int startIndex = (int)(50 * (page - 1)), endIndex = (int)(50 * page);
        double pageIndexMax = Math.Ceiling(visibleReactions.Count / 50.0);

        UiNode buttons = new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Box) { Key = "beaker", Text = "Beaker: " + data.Str("linkedBeaker") + "  " },
            new UiNode(UiKind.Button)
            {
                Key = "filter",
                Icon = "search",
                Disabled = bookmarkMode,
                Color = reagentFilter ? "green" : "red",
                Text = "Filter by reagents in beaker",
                OnClick = () =>
                {
                    c.SetState("reagentFilter", !reagentFilter);
                    c.SetState("page", 1.0);
                },
            },
            new UiNode(UiKind.Button)
            {
                Key = "bookmarks",
                Icon = "book",
                Color = bookmarkMode ? "green" : "red",
                Text = "Bookmarks",
                OnClick = () =>
                {
                    c.SetState("bookmarkMode", !bookmarkMode);
                    c.SetState("page", 1.0);
                },
            },
            new UiNode(UiKind.Button) { Key = "prev", Icon = "minus", Disabled = page == 1, OnClick = () => c.SetState("page", Math.Max(page - 1, 1)) },
            new UiNode(UiKind.NumberInput)
            {
                Key = "page",
                Width = "25px",
                Step = 1,
                StepPixelSize = 3,
                Value = page,
                MinValue = 1,
                MaxValue = pageIndexMax,
                OnChange = value => c.SetState("page", value),
            },
            new UiNode(UiKind.Button) { Key = "next", Icon = "plus", Disabled = page == pageIndexMax, OnClick = () => c.SetState("page", Math.Min(page + 1, pageIndexMax)) });

        UiNode table = new UiNode(UiKind.Table) { Key = "reactions" }.With(new UiNode(UiKind.TableRow) { Key = "head" }.With(
            new UiNode(UiKind.TableCell) { Key = "reaction", Bold = true, Color = "label", Text = "Reaction" },
            new UiNode(UiKind.TableCell) { Key = "required", Bold = true, Color = "label", Text = "Required reagents" },
            new UiNode(UiKind.TableCell) { Key = "tags", Bold = true, Color = "label", Text = "Tags" },
            new UiNode(UiKind.TableCell) { Key = "save", Bold = true, Color = "label", Width = "20px", Text = !bookmarkMode ? "Save" : "Del" }));
        foreach (UiValue reaction in visibleReactions.Skip(startIndex).Take(Math.Max(0, endIndex - startIndex)))
        {
            string id = reaction.Str("id");
            UiNode reactants = new UiNode(UiKind.TableCell) { Key = "reactants" };
            foreach (UiValue reactant in reaction.Arr("reactants"))
            {
                string rid = reactant.Str("id");
                reactants.With(new UiNode(UiKind.Button)
                {
                    Key = "r:" + rid,
                    Icon = "vial",
                    Color = current.Contains(rid) ? "green" : null,
                    Text = reactant.Str("name"),
                    OnClick = () => c.Act("reagent_click", ("id", rid)),
                });
            }
            table.With(new UiNode(UiKind.TableRow) { Key = "row:" + id, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Key = "name", Bold = true, Color = "label" }.With(new UiNode(UiKind.Button)
                {
                    Key = "click",
                    Icon = "flask",
                    Color = "purple",
                    Text = reaction.Str("name"),
                    OnClick = () => c.Act("recipe_click", ("id", id)),
                }),
                reactants,
                new UiNode(UiKind.TableCell) { Key = "tag", Width = "60px" }.With(new UiNode(UiKind.Icon) { Icon = BitflagToIcon((int)reaction.Num("bitflags"), bitflags) ?? "question" }),
                new UiNode(UiKind.TableCell) { Key = "mark", Width = "20px" }.With(!bookmarkMode
                    ? new UiNode(UiKind.Button)
                    {
                        Key = "add",
                        Icon = "book",
                        Color = "green",
                        Disabled = bookmarks.Contains(id),
                        OnClick = () =>
                        {
                            c.SetState("bookmarks", bookmarks.Append(id).ToList());
                            c.Act("update_ui");
                        },
                    }
                    : new UiNode(UiKind.Button) { Key = "del", Icon = "trash", Color = "red", OnClick = () => c.SetState("bookmarks", bookmarks.Where(b => b != id).ToList()) })));
        }

        return new UiNode(UiKind.Section)
        {
            Key = "library",
            Fill = true,
            Scrollable = true,
            Title = bookmarkMode ? "Bookmarked recipes" : "Possible recipes",
            Buttons = buttons,
        }.With(table);
    }

    // tg: common/RecipeLookup.jsx:16  RecipeLookup
    private static UiNode RecipeLookup(TguiContext c, UiValue recipe)
    {
        if (recipe == null)
            return new UiNode(UiKind.Box) { Key = "none", Text = "No reaction selected!" };
        List<string> bookmarks = Bookmarks(c);
        string id = recipe.Str("id");
        bool isCold = recipe.Truthy("isColdRecipe");
        UiNode list = new UiNode(UiKind.LabeledList) { Key = "recipe" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Recipe" }.With(
                new UiNode(UiKind.Icon) { Key = "col", Icon = "circle", Color = recipe.Str("reagentCol") },
                new UiNode(UiKind.Box) { Key = "text", Bold = true, Text = " " + recipe.Str("name") },
                new UiNode(UiKind.Button) { Key = "prev", Icon = "arrow-left", Disabled = recipe.Num("subReactIndex") == 1, OnClick = () => c.Act("reduce_index", ("id", recipe.Str("name"))) },
                new UiNode(UiKind.Button) { Key = "next", Icon = "arrow-right", Disabled = recipe.Num("subReactIndex") == recipe.Num("subReactLen"), OnClick = () => c.Act("increment_index", ("id", recipe.Str("name"))) },
                new UiNode(UiKind.Button)
                {
                    Key = "bookmark",
                    Icon = "book",
                    Color = "green",
                    Disabled = bookmarks.Contains(id),
                    OnClick = () =>
                    {
                        c.SetState("bookmarks", bookmarks.Append(id).ToList());
                        c.Act("update_ui");
                    },
                }));
        IReadOnlyList<UiValue> products = recipe.Arr("products");
        if (recipe.Get("products")?.Truthy ?? false)
            list.With(new UiNode(UiKind.LabeledItem) { Key = "products", Label = "Products" }.With(products.Select(product => new UiNode(UiKind.Button)
            {
                Key = "p:" + product.Str("name"),
                Icon = "vial",
                Disabled = product.Truthy("hasProduct"),
                Text = TguiFormat.JsNumber(product.Num("ratio")) + "u " + product.Str("name"),
                OnClick = () => c.Act("reagent_click", ("id", product.Str("id"))),
            }).ToArray()));
        UiNode reactants = new UiNode(UiKind.LabeledItem) { Key = "reactants", Label = "Reactants" };
        foreach (UiValue reactant in recipe.Arr("reactants"))
        {
            string rid = reactant.Str("id");
            reactants.With(new UiNode(UiKind.Box) { Key = "r:" + rid }.With(
                new UiNode(UiKind.Button) { Key = "vial", Icon = "vial", Color = reactant.Str("color"), Text = TguiFormat.JsNumber(reactant.Num("ratio")) + "u " + reactant.Str("name"), OnClick = () => c.Act("reagent_click", ("id", rid)) },
                reactant.Truthy("tooltipBool")
                    ? new UiNode(UiKind.Button) { Key = "find", Icon = "flask", Color = "purple", Tooltip = reactant.Str("tooltip"), OnClick = () => c.Act("find_reagent_reaction", ("id", rid)) }
                    : null));
        }
        list.With(reactants);
        if (recipe.Get("catalysts")?.Truthy ?? false)
            list.With(new UiNode(UiKind.LabeledItem) { Key = "catalysts", Label = "Catalysts" }.With(recipe.Arr("catalysts").Select(catalyst => new UiNode(UiKind.Box) { Key = "c:" + catalyst.Str("id") }.With(new UiNode(UiKind.Button)
            {
                Key = "vial",
                Icon = "vial",
                Color = catalyst.Str("color"),
                Text = TguiFormat.JsNumber(catalyst.Num("ratio")) + "u " + catalyst.Str("name"),
                Tooltip = catalyst.Truthy("tooltipBool") ? catalyst.Str("tooltip") : null,
                OnClick = () => c.Act("reagent_click", ("id", catalyst.Str("id"))),
            })).ToArray()));
        if (recipe.Str("reqContainer").Length > 0)
            list.With(new UiNode(UiKind.LabeledItem) { Key = "container", Label = "Container" }.With(new UiNode(UiKind.Button)
            {
                Key = "req",
                Color = "transparent",
                Text = recipe.Str("reqContainer"),
                Tooltip = "The required container for this reaction to occur in.",
            }));
        UiNode purity = new UiNode(UiKind.LabeledList) { Key = "purityList" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "ph", Label = "Optimal pH range", Tooltip = "If your reaction is kept within these bounds then the purity of your product will be 100%", Text = TguiFormat.JsNumber(recipe.Num("lowerpH")) + "-" + TguiFormat.JsNumber(recipe.Num("upperpH")) });
        if (recipe.Get("inversePurity")?.Truthy ?? false)
            purity.With(new UiNode(UiKind.LabeledItem) { Key = "inverse", Label = "Inverse purity", Tooltip = "If your purity is below this it will 100% convert into the product's associated Inverse reagent on consumption.", Text = "<" + InversePurityText(recipe.Get("inversePurity")) + "%" });
        if (recipe.Get("minPurity")?.Truthy ?? false)
            purity.With(new UiNode(UiKind.LabeledItem) { Key = "min", Label = "Minimum purity", Tooltip = "If your purity is below this at any point during the reaction, it will cause negative effects, and if it remains below this value on completion it will convert into the product's associated Failed reagent.", Text = "<" + TguiFormat.JsNumber(recipe.Num("minPurity") * 100) + "%" });
        list.With(new UiNode(UiKind.LabeledItem) { Key = "purity", Label = "Purity" }.With(purity));

        // tg: RecipeLookup.jsx:202  the rate profile: the thermodynamics area in green, the explosive band in red
        UiNode chart = new UiNode(UiKind.Chart) { Key = "profile", Height = "50px" }.With(ProfileLine("thermo", recipe.Get("thermodynamics"), "#3cf072"));
        if (recipe.Get("explosive")?.Truthy ?? false)
            chart.With(ProfileLine("explosive", recipe.Get("explosive"), "#d92727"));
        string tempMin = TguiFormat.JsNumber(recipe.Num("tempMin")) + "K", explodeTemp = TguiFormat.JsNumber(recipe.Num("explodeTemp")) + "K";
        const string startTip = "The minimum temperature needed for this reaction to start. Heating it up past this point will increase the reaction rate.";
        const string hotTip = "The temperature at which it is overheated, causing negative effects on the reaction.";
        const string coldTip = "The temperature at which it is underheated, causing negative effects on the reaction.";
        UiNode temps = new UiNode(UiKind.Stack) { Key = "temps", Justify = "space-between" }.With(
            new UiNode(UiKind.StackItem) { Key = "low", Tooltip = isCold ? coldTip : startTip, Color = isCold ? "red" : null }.With(new UiNode(UiKind.Box) { Text = isCold ? explodeTemp : tempMin }),
            recipe.Get("explosive")?.Truthy ?? false
                ? new UiNode(UiKind.StackItem) { Key = "high", Tooltip = isCold ? startTip : hotTip, Color = !isCold ? "red" : null }.With(new UiNode(UiKind.Box) { Text = isCold ? tempMin : explodeTemp })
                : null);
        list.With(new UiNode(UiKind.LabeledItem) { Key = "rate", Label = "Rate profile" }.With(chart, temps));
        list.With(new UiNode(UiKind.LabeledItem) { Key = "dynamics", Label = "Dynamics" }.With(
            new UiNode(UiKind.LabeledList) { Key = "dyn" }.With(new UiNode(UiKind.LabeledItem)
            {
                Key = "optimal",
                Label = "Optimal rate",
                Tooltip = "The fastest rate the reaction can go, in units per second. This is the plateu region shown in the rate profile above.",
                Text = TguiFormat.JsNumber(recipe.Num("thermoUpper")) + "u/s",
            }),
            new UiNode(UiKind.Box) { Key = "thermics", Tooltip = "The heat generated by a reaction - exothermic produces heat, endothermic consumes heat.", Text = recipe.Str("thermics") }));
        return list;
    }

    /// The inverse purity is a number for a reaction with a product and the text "N/A" without one (holder/ui_data.dm:173); JS multiplies
    /// the text by 100 into NaN
    private static string InversePurityText(UiValue value) => value.Kind == UiValueKind.Number ? TguiFormat.JsNumber(value.AsNum() * 100) : "NaN";

    // tg: RecipeLookup.jsx:209  <Chart.Line data={pairs} strokeWidth={0} fillColor>: the line's range is its data's own
    private static UiNode ProfileLine(string key, UiValue pairs, string fill)
    {
        List<(double X, double Y)> points = (pairs?.Items ?? (IReadOnlyList<UiValue>)Array.Empty<UiValue>())
            .Select(p => (p.Items.Count > 0 ? p.Items[0].AsNum() : 0, p.Items.Count > 1 ? p.Items[1].AsNum() : 0)).ToList();
        if (points.Count == 0)
            return null;
        (double minX, double maxX) = (points.Min(p => p.X), points.Max(p => p.X));
        (double minY, double maxY) = (points.Min(p => p.Y), points.Max(p => p.Y));
        return new UiNode(UiKind.ChartLine)
        {
            Key = key,
            Data = points,
            RangeX = (minX, maxX == minX ? minX + 1 : maxX),
            RangeY = (minY, maxY == minY ? minY + 1 : maxY),
            FillColor = fill,
            StrokeColor = "rgba(0, 0, 0, 0)",
        };
    }

    // tg: common/ReagentLookup.jsx:8  ReagentLookup
    private static UiNode ReagentLookup(TguiContext c, UiValue reagent)
    {
        if (reagent == null)
            return new UiNode(UiKind.Box) { Key = "none", Text = "No reagent selected!" };
        UiNode properties = new UiNode(UiKind.LabeledList) { Key = "props" };
        if (reagent.Get("OD")?.Truthy ?? false)
            properties.With(new UiNode(UiKind.LabeledItem) { Key = "od", Label = "Overdose", Text = TguiFormat.JsNumber(reagent.Num("OD")) + "u" });
        IReadOnlyList<UiValue> addictions = reagent.Arr("addictions");
        if (addictions.Count > 0 && addictions[0].Truthy)
            properties.With(new UiNode(UiKind.LabeledItem) { Key = "addiction", Label = "Addiction" }.With(addictions.Select(a => new UiNode(UiKind.Box) { Key = a.AsStr(), Text = a.AsStr() }).ToArray()));
        properties.With(new UiNode(UiKind.LabeledItem) { Key = "meta", Label = "Metabolization rate", Text = TguiFormat.JsNumber(reagent.Num("metaRate")) + "u/s" });

        UiNode Linked(string key, string label, string nameField, string idField, string tooltip) =>
            reagent.Get(nameField)?.Truthy ?? false
                ? new UiNode(UiKind.LabeledItem) { Key = key, Label = label }.With(new UiNode(UiKind.Button)
                {
                    Key = "go",
                    Icon = "vial",
                    Tooltip = tooltip,
                    Text = reagent.Str(nameField),
                    OnClick = () => c.Act("reagent_click", ("id", reagent.Str(idField))),
                })
                : null;
        UiNode impurities = new UiNode(UiKind.LabeledList) { Key = "impure" }.With(
            Linked("impure", "Impure reagent", "impureReagent", "impureId", "This reagent will partially convert into this when the purity is above the Inverse purity on consumption."),
            Linked("inverse", "Inverse reagent", "inverseReagent", "inverseId", "This reagent will convert into this when the purity is below the Inverse purity on consumption."),
            Linked("failed", "Failed reagent", "failedReagent", "failedId", "This reagent will turn into this if the purity of the reaction is below the minimum purity on completion."));
        bool none = !(reagent.Get("failedReagent")?.Truthy ?? false) && !(reagent.Get("inverseReagent")?.Truthy ?? false) && !(reagent.Get("impureReagent")?.Truthy ?? false);

        return new UiNode(UiKind.LabeledList) { Key = "reagent" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Reagent" }.With(
                new UiNode(UiKind.Icon) { Key = "col", Icon = "circle", Color = reagent.Str("reagentCol") },
                new UiNode(UiKind.Box) { Key = "text", Text = " " + reagent.Str("name") }),
            new UiNode(UiKind.LabeledItem) { Key = "desc", Label = "Description", Text = reagent.Str("desc") },
            new UiNode(UiKind.LabeledItem) { Key = "ph", Label = "pH" }.With(
                new UiNode(UiKind.Icon) { Key = "col", Icon = "circle", Color = reagent.Str("pHCol") },
                new UiNode(UiKind.Box) { Key = "value", Text = " " + TguiFormat.JsNumber(reagent.Num("pH")) }),
            new UiNode(UiKind.LabeledItem) { Key = "properties", Label = "Properties" }.With(properties),
            new UiNode(UiKind.LabeledItem) { Key = "impurities", Label = "Impurities" }.With(
                impurities,
                reagent.Truthy("isImpure") ? new UiNode(UiKind.Box) { Key = "isImpure", Text = "This reagent is created by impurity." } : null,
                reagent.Truthy("deadProcess") ? new UiNode(UiKind.Box) { Key = "dead", Text = "This reagent works on the dead." } : null,
                none ? new UiNode(UiKind.Box) { Key = "noImpure", Text = "This reagent has no impure reagents." } : null),
            new UiNode(UiKind.LabeledItem) { Key = "find" }.With(new UiNode(UiKind.Button)
            {
                Key = "findReaction",
                Icon = "flask",
                Color = "purple",
                Text = "Find associated reaction",
                OnClick = () => c.Act("find_reagent_reaction", ("id", reagent.Str("id"))),
            }));
    }
}
