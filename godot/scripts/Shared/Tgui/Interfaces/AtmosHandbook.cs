using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/common/AtmosHandbook.tsx and GasmixParser.tsx (M7 slice T3, D164).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Deviations of the widgets: tgui-core's fuzzy search ('smart' strategy) is a case-insensitive substring search here; jotai's two
// atoms (the active gas and reaction ids, shared by every window) are this window's local state; a box has no italic.

public static class AtmosHandbook
{
    private const int GasesTab = 1;
    private const int ReactionsTab = 2;

    // tg: AtmosHandbook.tsx:136
    private static string SpecificHeatColor(double specificHeat) => specificHeat <= 10 ? "red" : specificHeat <= 20 ? "orange" : specificHeat <= 100 ? "yellow" : specificHeat <= 200 ? "green" : "blue";

    // tg: AtmosHandbook.tsx:150
    private static string ExportValueColor(double exportValue) => exportValue <= 0.2 ? "red" : exportValue <= 0.5 ? "orange" : exportValue <= 2.5 ? "yellow" : exportValue <= 5 ? "green" : "blue";

    /// <summary>The ids the analyzer's reaction and gas buttons set (tgui's atmosHandbookHooks: setActiveGasId, setActiveReactionId).</summary>
    public static void SetActiveGas(TguiContext c, string id) => c.SetState("handbook:gas", id);

    public static void SetActiveReaction(TguiContext c, string id) => c.SetState("handbook:reaction", id);

    // tg: AtmosHandbook.tsx:88  GasSearchBar
    private static UiNode SearchBar(TguiContext c, string key, string placeholder, string query, Action<string> onEnter)
    {
        return new UiNode(UiKind.Stack) { Key = key + ":bar", Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                new UiNode(UiKind.Input)
                {
                    Key = key + ":search",
                    Fluid = true,
                    Tooltip = placeholder,
                    TextValue = query,
                    OnInput = value => c.SetState(key + ":query", value),
                    OnCommit = value => onEnter(value),
                }),
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Button) { Key = key + ":icon", Icon = "search" }));
    }

    private static IEnumerable<UiValue> Sorted(IEnumerable<UiValue> items) => items.OrderBy(i => i.Str("name"), StringComparer.Ordinal);

    // tg: AtmosHandbook.tsx:158  GasHandbook
    private static UiNode Gases(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> gasInfo = data.Arr("gasInfo");
        string activeGasId = c.UseState("handbook:gas", "");
        string query = c.UseState("gases:query", "");
        UiValue relevantGas = gasInfo.FirstOrDefault(g => g.Str("id") == activeGasId);
        IEnumerable<UiValue> shown = query.Length > 0 ? gasInfo.Where(TguiFormat.CreateSearch<UiValue>(query, g => g.Str("name"))) : gasInfo;

        UiNode left = new UiNode(UiKind.Stack) { Vertical = true, Fill = true };
        if (relevantGas != null)
        {
            double specificHeat = relevantGas.Num("specific_heat");
            double exportValue = relevantGas.Num("export_value");
            left.With(
                new UiNode(UiKind.StackItem) { Key = "gas:name" }.With(new UiNode(UiKind.Box) { Text = "> " + relevantGas.Str("name"), Bold = true, FontSize = "1.2em" }),
                new UiNode(UiKind.StackItem) { Key = "gas:desc" }.With(new UiNode(UiKind.Box) { Text = relevantGas.Str("description"), Wrap = true }),
                new UiNode(UiKind.StackItem) { Key = "gas:heat" }.With(
                    new UiNode(UiKind.Stack).With(
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box)
                        {
                            Text = "Specific Heat:",
                            Color = "label",
                            Underline = true,
                            Tooltip = "The amount of energy required to raise the temperature of one mole of gas by one Kelvin.\nIn simpler terms: The lower the specific heat of the gas, the harder it will be to heat it up.",
                        }),
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = TguiFormat.JsNumber(specificHeat), Color = SpecificHeatColor(specificHeat) }),
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = "J/(mol * K)" }))),
                exportValue > 0
                    ? new UiNode(UiKind.StackItem) { Key = "gas:export" }.With(
                        new UiNode(UiKind.Stack).With(
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box)
                            {
                                Text = "Export Value:",
                                Color = "label",
                                Underline = true,
                                Tooltip = "The amount of " + data.Str("moneyName") + " gained per mol of gas exported.",
                            }),
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = TguiFormat.JsNumber(exportValue), Color = ExportValueColor(exportValue) }),
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = data.Str("moneySymbol") + "/mol" })))
                    : null);
            if (relevantGas.Field("reactions") is { Kind: UiValueKind.Object } reactions && reactions.Fields.Count > 0)
            {
                // tg: AtmosHandbook.tsx:253  Object.entries(relevantGas.reactions).sort((a, b) => (a[1] > b[1] ? 1 : -1))
                var buttons = new UiNode(UiKind.Stack) { Vertical = true };
                foreach (KeyValuePair<string, UiValue> entry in reactions.Fields.OrderBy(f => f.Value.AsStr(), StringComparer.Ordinal))
                {
                    string reactionId = entry.Key;
                    buttons.With(new UiNode(UiKind.StackItem) { Key = "gasreaction:" + reactionId }.With(new UiNode(UiKind.Button)
                    {
                        Key = "gasreactionbutton:" + reactionId,
                        Fluid = true,
                        Text = entry.Value.AsStr(),
                        OnClick = () =>
                        {
                            SetActiveReaction(c, reactionId);
                            c.SetState("handbook:tab", ReactionsTab);
                        },
                    }));
                }
                left.With(new UiNode(UiKind.StackItem) { Key = "gas:reactions", Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Scrollable = true, Fill = true, Title = "Relevant Reactions" }.With(buttons)));
            }
        }
        else
            left.With(new UiNode(UiKind.StackItem) { Key = "gas:none", Align = "center" }.With(
                new UiNode(UiKind.Box) { Text = "No Gas Selected", Color = "label", FontSize = "1.5rem", TextAlign = "center" }));

        var list = new UiNode(UiKind.Stack) { Vertical = true };
        foreach (UiValue gas in Sorted(shown))
        {
            string id = gas.Str("id");
            list.With(new UiNode(UiKind.StackItem) { Key = "gaslist:" + id }.With(new UiNode(UiKind.Button)
            {
                Key = "gaslistbutton:" + id,
                Fluid = true,
                Text = gas.Str("name"),
                OnClick = () => SetActiveGas(c, id),
            }));
        }

        return new UiNode(UiKind.Section)
        {
            Fill = true,
            Key = "gases",
            Buttons = null,
            Title = null,
        }.With(
            SearchBar(c, "gases", "Gas Lookup", query, keyword =>
            {
                UiValue found = gasInfo.FirstOrDefault(g => g.Str("name").StartsWith(keyword, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                    SetActiveGas(c, found.Str("id"));
            }),
            new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Width = "60%" }.With(left),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section) { Scrollable = true, Fill = true }.With(list))));
    }

    // tg: AtmosHandbook.tsx:318  ReactionHandbook
    private static UiNode Reactions(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> reactionInfo = data.Arr("reactionInfo");
        string activeReactionId = c.UseState("handbook:reaction", "");
        string query = c.UseState("reactions:query", "");
        UiValue relevantReaction = reactionInfo.FirstOrDefault(r => r.Str("id") == activeReactionId);
        IEnumerable<UiValue> shown = query.Length > 0 ? reactionInfo.Where(TguiFormat.CreateSearch<UiValue>(query, r => r.Str("name"))) : reactionInfo;

        UiNode left = new UiNode(UiKind.Stack) { Vertical = true, Fill = true };
        if (relevantReaction != null)
        {
            var factors = new UiNode(UiKind.LabeledList);
            foreach (UiValue factor in relevantReaction.Arr("factors"))
            {
                string name = factor.Str("factor_name");
                var item = new UiNode(UiKind.LabeledItem) { Key = relevantReaction.Str("id") + "_" + name, Label = name };
                if (factor.Str("factor_type") == "gas" && factor.Truthy("factor_id"))
                {
                    string gasId = factor.Str("factor_id");
                    item.Label = null;
                    item.Buttons = new UiNode(UiKind.Button)
                    {
                        Key = "factorgas:" + gasId,
                        Fluid = true,
                        Text = name,
                        OnClick = () =>
                        {
                            SetActiveGas(c, gasId);
                            c.SetState("handbook:tab", GasesTab);
                        },
                    };
                }
                else if (factor.Truthy("tooltip"))
                {
                    item.Label = null;
                    item.Buttons = new UiNode(UiKind.Box) { Text = name + ":", Underline = true, Tooltip = factor.Str("tooltip") };
                }
                item.With(new UiNode(UiKind.Box) { Text = factor.Str("desc"), Wrap = true });
                factors.With(item);
            }
            left.With(
                new UiNode(UiKind.StackItem) { Key = "reaction:name" }.With(new UiNode(UiKind.Box) { Text = "> " + relevantReaction.Str("name"), Bold = true, FontSize = "1.2em" }),
                new UiNode(UiKind.StackItem) { Key = "reaction:desc" }.With(new UiNode(UiKind.Box) { Text = relevantReaction.Str("description"), Wrap = true }),
                new UiNode(UiKind.StackItem) { Key = "reaction:factors", Grow = 1 }.With(new UiNode(UiKind.Section) { Scrollable = true, Fill = true }.With(factors)));
        }
        else
            left.With(new UiNode(UiKind.StackItem) { Key = "reaction:none", Align = "center" }.With(
                new UiNode(UiKind.Box) { Text = "No reaction selected", Color = "label", FontSize = "1.5rem", TextAlign = "center" }));

        var list = new UiNode(UiKind.Stack) { Vertical = true };
        foreach (UiValue reaction in Sorted(shown))
        {
            string id = reaction.Str("id");
            list.With(new UiNode(UiKind.StackItem) { Key = "reactionlist:" + id }.With(new UiNode(UiKind.Button)
            {
                Key = "reactionlistbutton:" + id,
                Fluid = true,
                Text = reaction.Str("name"),
                OnClick = () => SetActiveReaction(c, id),
            }));
        }

        return new UiNode(UiKind.Section) { Fill = true, Key = "reactions" }.With(
            SearchBar(c, "reactions", "Reaction Lookup", query, keyword =>
            {
                UiValue found = reactionInfo.FirstOrDefault(r => r.Str("name").StartsWith(keyword, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                    SetActiveReaction(c, found.Str("id"));
            }),
            new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Width = "60%" }.With(left),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section) { Scrollable = true, Fill = true }.With(list))));
    }

    // tg: AtmosHandbook.tsx:385  AtmosHandbookContent
    public static UiNode Content(TguiContext c)
    {
        int tab = c.UseState("handbook:tab", GasesTab);
        return new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Tabs) { Fluid = true }.With(
                    new UiNode(UiKind.Tab) { Key = "tab:gases", Text = "Gases", Selected = tab == GasesTab, OnClick = () => c.SetState("handbook:tab", GasesTab) },
                    new UiNode(UiKind.Tab) { Key = "tab:reactions", Text = "Reactions", Selected = tab == ReactionsTab, OnClick = () => c.SetState("handbook:tab", ReactionsTab) })),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(tab == ReactionsTab ? Reactions(c) : Gases(c)));
    }
}

public static class GasmixParser
{
    // tg: GasmixParser.tsx:32  GasmixParser (gasesOnClick and reactionOnClick given, the analyzer's call)
    public static UiNode Render(UiValue gasmix, Action<string> gasesOnClick, Action<string> reactionOnClick, bool detailedReactions = false)
    {
        double totalMoles = gasmix.Num("total_moles");
        if (totalMoles <= 0)
            return new UiNode(UiKind.Box) { Text = "No Gas Detected!" };

        var gases = new UiNode(UiKind.LabeledList);
        foreach (UiValue gas in gasmix.Arr("gases"))
        {
            IReadOnlyList<UiValue> g = gas.Items;
            string id = g[0].AsStr();
            string name = g[1].AsStr();
            double amount = g[2].AsNum();
            var item = new UiNode(UiKind.LabeledItem) { Key = name };
            if (gasesOnClick != null)
                item.LabelNode = new UiNode(UiKind.Button) { Key = "gas:" + id, Fluid = true, Text = name, OnClick = () => gasesOnClick(id) };
            else
                item.Label = name;
            item.With(new UiNode(UiKind.Box) { Text = TguiFormat.ToFixed(amount, 2) + " mol (" + TguiFormat.ToFixed(amount / totalMoles * 100, 2) + " %)" });
            gases.With(item);
        }

        UiNode root = new UiNode(UiKind.Stack) { Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem) { Width = "60%" }.With(gases),
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                        new UiNode(UiKind.LabeledList).With(
                            new UiNode(UiKind.LabeledItem) { Label = "Temperature" }.With(new UiNode(UiKind.Box) { Text = (totalMoles != 0 ? TguiFormat.ToFixed(gasmix.Num("temperature"), 2) : "-") + " K" }),
                            new UiNode(UiKind.LabeledItem) { Label = "Volume" }.With(new UiNode(UiKind.Box) { Text = (totalMoles != 0 ? TguiFormat.ToFixed(gasmix.Num("volume"), 2) : "-") + " L" }),
                            new UiNode(UiKind.LabeledItem) { Label = "Pressure" }.With(new UiNode(UiKind.Box) { Text = (totalMoles != 0 ? TguiFormat.ToFixed(gasmix.Num("pressure"), 2) : "-") + " kPa" }))))));

        IReadOnlyList<UiValue> reactions = gasmix.Arr("reactions");
        if (reactions.Count > 0)
        {
            var list = new UiNode(UiKind.Stack) { Vertical = detailedReactions };
            foreach (UiValue reaction in reactions)
            {
                IReadOnlyList<UiValue> r = reaction.Items;
                string id = r[0].AsStr();
                string name = r[1].AsStr();
                UiNode label = reactionOnClick != null
                    ? new UiNode(UiKind.Button) { Key = "reaction:" + id, Text = name, OnClick = () => reactionOnClick(id) }
                    : new UiNode(UiKind.Box) { Text = name };
                list.With(new UiNode(UiKind.StackItem) { Key = gasmix.Str("reference") + "-" + id }.With(label,
                    detailedReactions ? new UiNode(UiKind.Box) { Text = "- " + r[2].AsStr() } : null));
            }
            root.With(new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Stack) { Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Align = "center" }.With(new UiNode(UiKind.Box) { Text = "Active Reactions", Color = "label", FontSize = "1.2rem", TextAlign = "center" }),
                    new UiNode(UiKind.StackItem).With(list))));
        }
        return root;
    }
}
