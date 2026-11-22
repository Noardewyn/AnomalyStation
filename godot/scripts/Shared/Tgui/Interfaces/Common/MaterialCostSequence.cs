using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces.Common;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Fabrication/MaterialCostSequence.tsx (silicons SI3, D326:
// the exosuit fabricator's costs). Engine-free (compiled into the tests too). The kit's tooltips are text, so CostText gives the same
// sequence as a line for a recipe's tooltip.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MaterialCostSequence
{
    /// <summary>tg: MaterialCostSequence.tsx:57  the costs of <paramref name="design"/> (or <paramref name="costMap"/>) times <paramref name="amount"/>.</summary>
    public static UiNode Render(string key, double sheetMaterialAmount, IReadOnlyDictionary<string, double> available = null,
        IReadOnlyDictionary<string, double> costMap = null, UiValue design = null, double amount = 1)
    {
        if (costMap == null && design == null)
            return null;
        Dictionary<string, double> costs = Costs(costMap, design);

        // <Flex wrap justify="space-around" align="center">
        var row = new UiNode(UiKind.Stack) { Key = key, Justify = "space-around", Align = "center" };
        foreach (var (material, quantity) in costs)
        {
            double sheets = amount * quantity / sheetMaterialAmount;
            string color = available == null ? null
                : amount * quantity * 2 <= available.GetValueOrDefault(material) ? "#fff"
                : amount * quantity <= available.GetValueOrDefault(material) ? "#f08f11"
                : "#db2828";
            row.With(new UiNode(UiKind.StackItem) { Key = "cost:" + material }.With(
                new UiNode(UiKind.Stack) { Key = "column", Vertical = true, Align = "center" }.With(
                    new UiNode(UiKind.StackItem) { Key = "icon" }.With(MaterialAccessBar.MaterialIcon(material, sheets)),
                    new UiNode(UiKind.StackItem) { Key = "label" }.With(new UiNode(UiKind.Box) { Key = "amount", Color = color, Text = TguiFormat.FormatSiUnit(sheets, 0) }))));
        }
        return row;
    }

    /// <summary>The same sequence as text ("iron 2, glass 1"), for a tooltip.</summary>
    public static string CostText(UiValue design, double sheetMaterialAmount, double amount = 1) =>
        string.Join(", ", Costs(null, design).Select(kv => kv.Key + " " + TguiFormat.FormatSiUnit(amount * kv.Value / sheetMaterialAmount, 0)));

    // tg: MaterialCostSequence.tsx:65  costMap ??= {}; the design's costs added to it
    private static Dictionary<string, double> Costs(IReadOnlyDictionary<string, double> costMap, UiValue design)
    {
        var costs = costMap == null ? new Dictionary<string, double>(StringComparer.Ordinal) : new Dictionary<string, double>(costMap, StringComparer.Ordinal);
        if (design != null)
        {
            foreach (var (name, value) in design.Obj("cost"))
                costs[name] = costs.GetValueOrDefault(name) + UiData.AsNum(value, 0);
        }
        return costs;
    }
}
