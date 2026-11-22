using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemDispenser.tsx (gaps package P5). The window reads
// the chem dispenser's ui_data (chem_dispenser.dm:215: the energy, the dispensable chemicals, the recipes being recorded, the beaker)
// and ui_static_data (:265: the reaction list and its tag filters) and sends amount, dispense, remove, eject, insert, the recipe acts
// and reaction_lookup. The kit has no BlockQuote, hr or CSS text shadow: the recipe card is a Box with the label colour, the bars are
// Dividers; tgui-core's AnimatedNumber renders as its text. Original copyright and license: tgstation, AGPL-3.0 (code). See
// ../tgstation/LICENSE.

public static class ChemDispenser
{
    // tg: ChemDispenser.tsx:24
    private const int NO_DROPDOWN = 0, DROPDOWN_CLOSED = 1, DROPDOWN_OPEN = 2;

    private sealed record ReagentReaction(string Name, UiValue Reaction);

    // tg: ChemDispenser.tsx:88  reagentListToArray
    private static List<ReagentReaction> ReagentListToArray(UiValue data) =>
        data.Obj("reaction_list").Select(kv => new ReagentReaction(kv.Key, kv.Value)).ToList();

    // tg: ChemDispenser.tsx:97
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool recording = data.Get("recordingRecipe")?.Truthy ?? false;
        UiValue beaker = data.Get("beaker");
        bool hasBeaker = beaker?.Truthy ?? false;
        bool hasBeakerInHand = data.Truthy("hasBeakerInHand");
        IReadOnlyList<UiValue> chemicals = data.Arr("chemicals");
        bool showPhCol = c.UseState("showbaseph", false);
        bool showReactionList = c.UseState("showreactions", false);
        string searchTerm = c.UseState("searchterm", "");
        int filterByBitflag = c.UseState("filterbitflag", 0);
        List<string> pinnedReactions = c.UseState("pinnedreactions", new List<string>());

        IReadOnlyList<UiValue> beakerTransferAmounts = hasBeaker ? beaker.Arr("transferAmounts") : Array.Empty<UiValue>();
        // tg: ChemDispenser.tsx:124  recordedContents: Object.keys(recordingRecipe).map(id => ({ id, name: toTitleCase(id.replace(/_/, ' ')), volume }))
        UiValue recordedContents = null;
        if (recording)
        {
            recordedContents = UiValue.ListOf(data.Obj("recordingRecipe").Select(kv => UiValue.ObjectOf(
                ("id", UiValue.Of(kv.Key)),
                ("name", UiValue.Of(TguiFormat.ToTitleCase(ReplaceFirst(kv.Key, "_", " ")))),
                ("volume", kv.Value))));
        }

        List<ReagentReaction> reactionReagentList = ReagentListToArray(data);
        Func<ReagentReaction, bool> reactionSearch = TguiFormat.CreateSearch<ReagentReaction>(searchTerm, reaction => reaction.Name);

        // tg: ChemDispenser.tsx:140  filter by bitflags, base reagents and search; sort by name, pinned first
        List<ReagentReaction> filteredReactions = reactionReagentList
            .Where(reaction =>
            {
                int bitflags = (int)reaction.Reaction.Num("bitflags");
                if (filterByBitflag != 0 && (bitflags & filterByBitflag) != filterByBitflag)
                    return false;
                if (chemicals.Any(chem => chem.Str("title") == reaction.Name))
                    return false;
                return reactionSearch(reaction);
            })
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .OrderBy(r => pinnedReactions.Contains(r.Name) ? 0 : 1)
            .ToList();

        const int mainWidth = 565;
        const int reactionWidth = 245;
        int windowWidth = mainWidth + (showReactionList ? reactionWidth : 0);

        // tg: ChemDispenser.tsx:178  the Status section's buttons
        UiNode statusButtons = new UiNode(UiKind.Fragment).With(
            recording ? new UiNode(UiKind.Box) { Key = "recording", Color = "red", Text = "● Recording" } : null,
            new UiNode(UiKind.Button)
            {
                Key = "phcol",
                Icon = "cog",
                Tooltip = "Color code the reagents by pH",
                Selected = showPhCol,
                OnClick = () => c.SetState("showbaseph", !showPhCol),
            },
            new UiNode(UiKind.Button)
            {
                Key = "lookup",
                Icon = "book",
                Disabled = !hasBeaker,
                Tooltip = hasBeaker ? "Look up recipes and reagents!" : "Please insert a beaker!",
                Text = "Reactions",
                OnClick = () => c.Act("reaction_lookup"),
            },
            new UiNode(UiKind.Button)
            {
                Key = "showRecipes",
                Icon = showReactionList ? "arrow-left" : "arrow-right",
                Text = "Recipes",
                OnClick = () => c.SetState("showreactions", !showReactionList),
            });

        double energy = data.Num("energy"), maxEnergy = data.Num("maxEnergy");
        UiNode status = new UiNode(UiKind.Section) { Key = "status", Title = "Status", Buttons = statusButtons }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Energy" }.With(new UiNode(UiKind.ProgressBar)
                {
                    Key = "energy",
                    Value = maxEnergy > 0 ? energy / maxEnergy : 0,
                    Text = UnitsText(data.Get("displayedUnits")) + " / " + UnitsText(data.Get("displayedMaxUnits")) + " units",
                })));

        // tg: ChemDispenser.tsx:227  Custom Recipes
        IReadOnlyList<KeyValuePair<string, UiValue>> recipes = data.Obj("recipes");
        UiNode recipeButtons = new UiNode(UiKind.Fragment).With(
            !recording ? new UiNode(UiKind.Button) { Key = "clear", Color = "transparent", Text = "Clear recipes", OnClick = () => c.Act("clear_recipes") } : null,
            !recording ? new UiNode(UiKind.Button) { Key = "record", Icon = "circle", Disabled = !hasBeaker, Text = "Record", OnClick = () => c.Act("record_recipe") } : null,
            recording ? new UiNode(UiKind.Button) { Key = "discard", Icon = "ban", Color = "transparent", Text = "Discard", OnClick = () => c.Act("cancel_recording") } : null,
            recording ? new UiNode(UiKind.Button) { Key = "save", Icon = "save", Color = "green", Text = "Save", OnClick = () => c.Act("save_recording") } : null);
        UiNode recipeBox = new UiNode(UiKind.Stack) { Key = "recipeList", Wrap = true };
        foreach (KeyValuePair<string, UiValue> recipe in recipes)
        {
            string name = recipe.Key;
            recipeBox.With(new UiNode(UiKind.Button) { Key = "recipe:" + name, Icon = "tint", Width = "129.5px", Text = name, OnClick = () => c.Act("dispense_recipe", ("recipe", name)) });
        }
        if (recipes.Count == 0)
            recipeBox.With(new UiNode(UiKind.Box) { Key = "none", Color = "light-gray", Text = "No recipes." });
        UiNode customRecipes = new UiNode(UiKind.Section) { Key = "custom", Title = "Custom Recipes", Buttons = recipeButtons }.With(recipeBox);

        // tg: ChemDispenser.tsx:290  Dispense
        UiNode dispenseAmounts = new UiNode(UiKind.Fragment).With(beakerTransferAmounts.Select(amount => new UiNode(UiKind.Button)
        {
            Key = "amount:" + TguiFormat.JsNumber(amount.AsNum()),
            Icon = "plus",
            Selected = amount.AsNum() == data.Num("amount"),
            Text = TguiFormat.JsNumber(amount.AsNum()),
            OnClick = () => c.Act("amount", ("target", amount.AsNum())),
        }).ToArray());
        UiNode chemicalGrid = new UiNode(UiKind.Stack) { Key = "chemicals", Wrap = true };
        foreach (UiValue chemical in chemicals)
            chemicalGrid.With(new UiNode(UiKind.StackItem) { Key = "chem:" + chemical.Str("id"), Width = "24.5%" }.With(ReagentDispenseButton(c, chemical, showPhCol, true, null)));
        UiNode dispense = new UiNode(UiKind.Section) { Key = "dispense", Title = "Dispense", Buttons = dispenseAmounts }.With(chemicalGrid);

        // tg: ChemDispenser.tsx:318  Beaker
        UiNode removeAmounts = new UiNode(UiKind.Fragment).With(beakerTransferAmounts.Select(amount => new UiNode(UiKind.Button)
        {
            Key = "remove:" + TguiFormat.JsNumber(amount.AsNum()),
            Icon = "minus",
            Disabled = recording,
            Text = TguiFormat.JsNumber(amount.AsNum()),
            OnClick = () => c.Act("remove", ("amount", amount.AsNum())),
        }).ToArray());
        UiNode beakerBody = hasBeaker || recording
            ? BeakerDisplay.Display(c, beaker, recordedContents, recording ? "Virtual beaker" : null, data.Truthy("showpH"))
            : new UiNode(UiKind.Stack) { Key = "nobeaker", Justify = "space-between", Align = "center" }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Color = "label", Text = "No beaker loaded." }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "insert",
                    Icon = "eject",
                    Disabled = !hasBeakerInHand,
                    Tooltip = !hasBeakerInHand ? "You need to hold a container in your hand!" : null,
                    Text = "Insert",
                    OnClick = () => c.Act("insert"),
                }));
        UiNode beakerSection = new UiNode(UiKind.Section) { Key = "beaker", Fill = true, Title = "Beaker", Buttons = removeAmounts }.With(beakerBody);

        UiNode main = new UiNode(UiKind.StackItem) { Key = "main", Width = mainWidth + "px", Grow = 1 }.With(
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem).With(status),
                new UiNode(UiKind.StackItem).With(customRecipes),
                new UiNode(UiKind.StackItem).With(dispense),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(beakerSection)));

        UiNode root = new UiNode(UiKind.Stack) { Key = "root", Fill = true }.With(main);
        if (showReactionList)
            root.With(ReactionListPanel(c, data, reactionWidth, searchTerm, filterByBitflag, pinnedReactions, filteredReactions));

        // tg: ChemDispenser.tsx:166  <Window width={windowWidth} height={620}><Window.Content scrollable>
        return c.Window(windowWidth, 620, null, root);
    }

    // tg: ChemDispenser.tsx:373  the Recipes panel
    private static UiNode ReactionListPanel(TguiContext c, UiValue data, int width, string searchTerm, int filterByBitflag, List<string> pinnedReactions, List<ReagentReaction> filteredReactions)
    {
        UiNode flags = new UiNode(UiKind.Stack) { Key = "flags", Wrap = true };
        foreach (KeyValuePair<string, UiValue> entry in data.Obj("all_bitflags"))
        {
            string readable = entry.Key;
            int flag = (int)entry.Value.AsNum();
            flags.With(new UiNode(UiKind.StackItem) { Key = "flag:" + readable, Grow = 1 }.With(new UiNode(UiKind.Button)
            {
                Fluid = true,
                TextAlign = "center",
                FontSize = "0.9em",
                Tooltip = ReagentBitflags.BitflagInfo.FirstOrDefault(bf => bf.Flag == readable)?.Tooltip ?? "",
                Selected = (filterByBitflag & flag) != 0,
                Text = readable,
                OnClick = () => c.SetState("filterbitflag", flag ^ filterByBitflag),
            }));
        }

        UiNode list = new UiNode(UiKind.Stack) { Key = "reactions", Vertical = true };
        if (filteredReactions.Count > 0)
        {
            foreach (ReagentReaction reaction in filteredReactions)
                list.With(new UiNode(UiKind.StackItem) { Key = "reaction:" + reaction.Name }.With(
                    ReactionDisplay(c, data, pinnedReactions.Contains(reaction.Name) ? DROPDOWN_OPEN : DROPDOWN_CLOSED, reaction, pinnedReactions, 0)));
        }
        else
            list.With(new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No reactions found." });

        return new UiNode(UiKind.StackItem) { Key = "recipesPanel", Width = width + "px" }.With(
            new UiNode(UiKind.Section) { Title = "Recipes", Fill = true }.With(
                new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack).With(
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Input)
                        {
                            Key = "search",
                            Tooltip = "Search reactions...", // the kit draws an Input's Tooltip as its placeholder
                            TextValue = searchTerm,
                            Fluid = true,
                            OnInput = value => c.SetState("searchterm", value),
                        }),
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "clearSearch", Icon = "times", Disabled = searchTerm == "", OnClick = () => c.SetState("searchterm", "") }),
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "unpin", Icon = "thumbtack", Disabled = pinnedReactions.Count == 0, OnClick = () => c.SetState("pinnedreactions", new List<string>()) }))),
                    new UiNode(UiKind.Divider) { Key = "d1" },
                    new UiNode(UiKind.StackItem).With(flags),
                    new UiNode(UiKind.Divider) { Key = "d2" },
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section) { Scrollable = true, Fill = true }.With(list)))));
    }

    // tg: ChemDispenser.tsx:465  ReagentDispenseButton
    private static UiNode ReagentDispenseButton(TguiContext c, UiValue chemical, bool showPhCol, bool mainscreen, string prefix)
    {
        IReadOnlyList<UiValue> recipeReagents = c.Data.Arr("recipeReagents");
        string id = chemical.Str("id");
        return new UiNode(UiKind.Button)
        {
            Key = "dispense:" + id + ":" + prefix,
            Icon = "tint",
            Fluid = true,
            Color = showPhCol ? chemical.Str("pHCol") : chemical.Str("color"),
            Tooltip = mainscreen ? "pH: " + TguiFormat.JsNumber(chemical.Num("pH")) : null,
            Selected = mainscreen && recipeReagents.Any(r => r.AsStr() == id),
            Text = (prefix ?? "") + chemical.Str("title"),
            OnClick = () => c.Act("dispense", ("reagent", id)),
        };
    }

    // tg: ChemDispenser.tsx:528  ReactionDisplay
    private static UiNode ReactionDisplay(TguiContext c, UiValue data, int dropdownState, ReagentReaction reaction, List<string> pinnedReactions, int depth)
    {
        UiValue r = reaction.Reaction;
        UiNode recipeList = new UiNode(UiKind.Box) { Key = "recipe", Color = "label" }.With(
            new UiNode(UiKind.Stack) { Vertical = true }.With(HorizontalBarWithText("Formula")).With(
                r.Arr("required_reagents").Select(reagent => new UiNode(UiKind.StackItem) { Key = reaction.Name + "-" + reagent.Str("name") + "-req" }.With(
                    ReactionComponentDisplay(c, data, reagent, pinnedReactions, depth))).ToArray()).With(
                r.Arr("required_catalysts").Count > 0 ? HorizontalBarWithText(r.Arr("required_reagents").Count == 1 ? "Catalyst" : "Catalysts") : null).With(
                r.Arr("required_catalysts").Select(catalyst => new UiNode(UiKind.StackItem) { Key = reaction.Name + "-" + catalyst.Str("name") + "-cat" }.With(
                    ReactionComponentDisplay(c, data, catalyst, pinnedReactions, depth))).ToArray()).With(
                HorizontalBarWithText("Optimal temperature"),
                new UiNode(UiKind.StackItem) { Key = "temp", FontSize = "0.9em" }.With(new UiNode(UiKind.Box) { Text = GetTemperatureMessage(r.Num("lower_temperature"), r.Num("upper_temperature")) }),
                HorizontalBarWithText("Optimal pH range"),
                new UiNode(UiKind.StackItem) { Key = "ph", FontSize = "0.9em" }.With(new UiNode(UiKind.Box) { Text = GetPHMessage(r.Num("lower_ph"), r.Num("upper_ph")) })));

        bool pinned = pinnedReactions.Contains(reaction.Name);
        UiNode header = new UiNode(UiKind.Stack) { Align = "center" }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = "pin",
                Icon = "thumbtack",
                Selected = pinned,
                // tg: :604  onContextMenu pins this one alone: the kit's right click
                OnClickWith = click =>
                {
                    if (click.Right)
                    {
                        c.SetState("pinnedreactions", new List<string> { reaction.Name });
                        return;
                    }
                    c.SetState("pinnedreactions", pinned ? pinnedReactions.Where(rid => rid != reaction.Name).ToList() : pinnedReactions.Append(reaction.Name).ToList());
                },
            }),
            new UiNode(UiKind.StackItem) { Grow = 1, Tooltip = r.Str("description") }.With(new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box) { Text = reaction.Name }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.ColorBox) { Key = "color", Color = r.Str("color") }))));

        UiNode body = dropdownState == NO_DROPDOWN
            ? recipeList
            : new UiNode(UiKind.Collapsible) { Key = "collapse", Title = "Recipe", Open = dropdownState == DROPDOWN_OPEN }.With(recipeList);

        return new UiNode(UiKind.Box) { Key = "card:" + reaction.Name, Background = "black" }.With(
            new UiNode(UiKind.Stack) { Vertical = true }.With(
                new UiNode(UiKind.StackItem).With(header),
                new UiNode(UiKind.StackItem).With(body)));
    }

    // tg: ChemDispenser.tsx:670  getTemperatureMessage
    private static string GetTemperatureMessage(double lower, double upper)
    {
        string l = TguiFormat.JsNumber(lower), u = TguiFormat.JsNumber(upper);
        if (lower == upper)
            return "Forms at " + l + "°K";
        if (lower > 300 && upper > 300)
            return "Heat between " + l + "°K-" + u + "°K";
        if (lower < 300 && upper < 300)
            return "Cool between " + TguiFormat.JsNumber(Math.Min(upper, lower)) + "°K-" + TguiFormat.JsNumber(Math.Max(upper, lower)) + "°K";
        return "Keep between " + l + "°K-" + u + "°K";
    }

    // tg: ChemDispenser.tsx:684  getPHMessage
    private static string GetPHMessage(double lower, double upper) =>
        lower == upper ? "Keep at pH " + TguiFormat.JsNumber(lower) : "Keep between pH " + TguiFormat.JsNumber(lower) + "-" + TguiFormat.JsNumber(upper);

    // tg: ChemDispenser.tsx:709  ReactionComponentDisplay: a base reagent dispenses, another recipe opens in a popup, else disabled
    private static UiNode ReactionComponentDisplay(TguiContext c, UiValue data, UiValue component, List<string> pinnedReactions, int depth)
    {
        string name = component.Str("name");
        double amount = component.Num("amount");
        UiValue baseReagent = data.Arr("chemicals").FirstOrDefault(chem => chem.Str("title") == name);
        if (baseReagent != null)
            return ReagentDispenseButton(c, baseReagent, false, false, FormatReagentName(amount));

        ReagentReaction foundRecipe = ReagentListToArray(data).FirstOrDefault(reaction => reaction.Name == name);
        if (foundRecipe == null || depth >= 3) // the popup chain stops at three levels (the kit has no lazy hover popups)
            return new UiNode(UiKind.Button) { Key = "unknown:" + name, Fluid = true, Disabled = true, Icon = "question", Text = FormatReagentName(amount, name) };

        // tg: :760  <Floating hoverOpen placement="left" content={<ReactionDisplay dropdownState={NO_DROPDOWN} .../>}>
        return new UiNode(UiKind.Floating)
        {
            Key = "floating:" + name,
            Content = () => new UiNode(UiKind.Box) { Background = "hsl(0, 0%, 15%)" }.With(ReactionDisplay(c, data, NO_DROPDOWN, foundRecipe, pinnedReactions, depth + 1)),
        }.With(new UiNode(UiKind.Button) { Key = "book:" + name, Icon = "book", Fluid = true, Text = FormatReagentName(amount, name) });
    }

    // tg: ChemDispenser.tsx:812  formatReagentName
    private static string FormatReagentName(double amount, string name = null)
    {
        if (string.IsNullOrEmpty(name))
            return TguiFormat.JsNumber(amount) + " part ";
        return TguiFormat.JsNumber(amount) + " part" + (amount == 1 ? "" : "s") + " " + name;
    }

    // tg: ChemDispenser.tsx:818  HorizontalBarWithText
    private static UiNode HorizontalBarWithText(string text) =>
        new UiNode(UiKind.StackItem) { Key = "bar:" + text }.With(new UiNode(UiKind.Stack) { Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Divider)),
            new UiNode(UiKind.StackItem) { FontSize = "0.95em" }.With(new UiNode(UiKind.Box) { Text = text }),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Divider))));

    /// The data's displayedUnits is a number (tg's type says string): JS's string concatenation prints it in full
    private static string UnitsText(UiValue value) => value?.Kind == UiValueKind.Number ? TguiFormat.JsNumber(value.AsNum()) : value?.AsStr() ?? "undefined";

    private static string ReplaceFirst(string text, string find, string replace)
    {
        int index = text.IndexOf(find, StringComparison.Ordinal);
        return index < 0 ? text : text[..index] + replace + text[(index + find.Length)..];
    }
}
