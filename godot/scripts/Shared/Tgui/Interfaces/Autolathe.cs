using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Autolathe.tsx (gaps RD1, D333: the autolathe).
// Engine-free (compiled into the tests too). The window reads the autolathe's ui_static_data (the materials' sheet size and the designs,
// with customMaterials for a design of a material requirement) and ui_data (the materials, their total and maximum, active) and sends
// make and eject. It shares the fabricators' design browser and cost sequence (Interfaces/Common). A material's bar is the kit's
// ProgressBar in the material's colour over the amount (tg draws the black remainder mirrored over a bar of the material's colour, which
// looks the same); a cost tooltip is text; a design's picture is a class of tg's "design" sheet (TguiSpritesheets.Design32, DV44);
// Button.Input is an input that commits the multiplier (as OreRedemptionMachine's).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Autolathe
{
    // tg: Autolathe.tsx:34
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double materialtotal = data.Num("materialtotal");
        double materialsmax = data.Num("materialsmax");
        bool active = data.Truthy("active");
        double sheetMaterialAmount = data.Num("SHEET_MATERIAL_AMOUNT", 100);
        IReadOnlyList<UiValue> designs = data.Arr("designs");

        List<UiValue> filteredMaterials = data.Arr("materials").Where(material => material.Num("amount") > 0).ToList();

        var availableMaterials = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (UiValue material in filteredMaterials)
            availableMaterials[material.Str("name") ?? ""] = material.Num("amount");

        var totals = new UiNode(UiKind.LabeledList) { Key = "totals" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "total", Label = "Total Materials" }.With(new UiNode(UiKind.ProgressBar)
            {
                Key = "totalBar",
                Value = materialtotal,
                MinValue = 0,
                MaxValue = materialsmax,
                Ranges = new[]
                {
                    ("good", materialsmax * 0.85, materialsmax),
                    ("average", materialsmax * 0.25, materialsmax * 0.85),
                    ("bad", 0.0, materialsmax * 0.25),
                },
                Text = TguiFormat.JsNumber(materialtotal / sheetMaterialAmount) + "/" + TguiFormat.JsNumber(materialsmax / sheetMaterialAmount) + " sheets",
            }),
            new UiNode(UiKind.LabeledItem) { Key = "materials" }.With(
                filteredMaterials.Count > 0
                    ? new UiNode(UiKind.Collapsible) { Key = "materialList", Title = "Materials" }.With(
                        new UiNode(UiKind.LabeledList) { Key = "list" }.With(filteredMaterials.Select(material => MaterialRow(c, material, materialsmax, sheetMaterialAmount))))
                    : null));

        var content = new UiNode(UiKind.Stack) { Key = "root", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "head" }.With(new UiNode(UiKind.Section) { Key = "totalSection", Title = "Total Materials" }.With(totals)),
            new UiNode(UiKind.StackItem) { Key = "browser", Grow = 1 }.With(
                DesignBrowser.Render(c, designs, design => AutolatheRecipe(c, design, availableMaterials, sheetMaterialAmount), busy: active)));

        // <Window title="Autolathe" width={670} height={600}>
        return c.Window(670, 600, "Autolathe", content);
    }

    // tg: Autolathe.tsx:84  a material's row: its sheets and its eject buttons
    private static UiNode MaterialRow(TguiContext c, UiValue material, double materialsmax, double sheetMaterialAmount)
    {
        string name = material.Str("name") ?? "";
        double amount = material.Num("amount");
        string @ref = material.Str("ref");

        var row = new UiNode(UiKind.Stack) { Key = "row", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "bar", Grow = 1 }.With(new UiNode(UiKind.ProgressBar)
            {
                Key = "amount",
                Value = amount,
                MaxValue = materialsmax,
                Color = material.Str("color"),
                Text = TguiFormat.JsNumber(amount / sheetMaterialAmount) + " sheets",
            }),
            new UiNode(UiKind.StackItem) { Key = "eject" }.With(new UiNode(UiKind.Box) { Key = "label", Text = "Eject:" }));

        // [1, 5, Math.floor(material.amount / SHEET_MATERIAL_AMOUNT)].sort(): JavaScript sorts the numbers as text
        double[] amounts = new[] { 1, 5, Math.Floor(amount / sheetMaterialAmount) }
            .OrderBy(a => TguiFormat.JsNumber(a), StringComparer.Ordinal).ToArray();
        for (int i = 0; i < amounts.Length; i++)
        {
            double amt = amounts[i];
            row.With(new UiNode(UiKind.StackItem) { Key = "x:" + i }.With(new UiNode(UiKind.Button)
            {
                Key = "eject:" + i,
                Disabled = amount < sheetMaterialAmount * amt,
                Text = "x" + TguiFormat.JsNumber(amt),
                OnClick = () => c.Act("eject", ("ref", @ref), ("amount", amt)),
            }));
        }

        return new UiNode(UiKind.LabeledItem) { Key = "mat:" + name, Label = TguiFormat.Capitalize(name) }.With(row);
    }

    // tg: Autolathe.tsx:212  AutolatheRecipe: how many it can print
    private static double MaxMult(UiValue design, IReadOnlyDictionary<string, double> availableMaterials)
    {
        double maxmult;
        if (design.Truthy("customMaterials"))
        {
            double largestMat = availableMaterials.Values.Aggregate(0.0, (accumulator, amount) => Math.Max(accumulator, amount));

            maxmult = largestMat > 0
                ? design.Obj("cost").Aggregate(double.PositiveInfinity, (accumulator, kv) => Math.Min(accumulator, largestMat / UiData.AsNum(kv.Value, 0)))
                : 0;
        }
        else
        {
            maxmult = design.Obj("cost").Aggregate(double.PositiveInfinity,
                (accumulator, kv) => Math.Min(accumulator, availableMaterials.GetValueOrDefault(kv.Key) / UiData.AsNum(kv.Value, 0)));
        }
        return Math.Min(Math.Floor(maxmult), 50);
    }

    // tg: Autolathe.tsx:206
    private static UiNode AutolatheRecipe(TguiContext c, UiValue design, IReadOnlyDictionary<string, double> availableMaterials, double sheetMaterialAmount)
    {
        string path = design.Str("path");
        double maxmult = MaxMult(design, availableMaterials);
        bool canPrint = maxmult > 0;

        return new UiNode(UiKind.Stack) { Key = "recipe:" + path, Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "info" }.With(new UiNode(UiKind.Button)
            {
                Key = "desc",
                Icon = "question-circle",
                Color = "transparent",
                Disabled = !canPrint,
                Tooltip = design.Str("desc"),
            }),
            new UiNode(UiKind.StackItem) { Key = "title", Grow = 1 }.With(new UiNode(UiKind.Button)
            {
                Key = "print",
                Fluid = true,
                Color = "transparent",
                Disabled = !canPrint,
                Tooltip = MaterialCostSequence.CostText(design, sheetMaterialAmount),
                OnClick = () =>
                {
                    if (canPrint)
                        c.Act("make", ("design_path", path), ("multiplier", 1));
                },
            }.With(DesignBrowser.IconAndName(design.Str("icon"), design.Str("name")))),
            new UiNode(UiKind.StackItem) { Key = "five" }.With(PrintButton(c, design, 5, sheetMaterialAmount, maxmult)),
            new UiNode(UiKind.StackItem) { Key = "ten" }.With(PrintButton(c, design, 10, sheetMaterialAmount, maxmult)),
            // <Button.Input buttonText={`[Max: ${maxmult}]`}>
            new UiNode(UiKind.StackItem) { Key = "custom" }.With(new UiNode(UiKind.Input)
            {
                Key = "max",
                Width = 6.0,
                Tooltip = "[Max: " + TguiFormat.JsNumber(maxmult) + "]",
                OnCommit = value => c.Act("make", ("design_path", path), ("multiplier", value)),
            }));
    }

    // tg: Autolathe.tsx:167  PrintButton
    private static UiNode PrintButton(TguiContext c, UiValue design, int quantity, double sheetMaterialAmount, double maxmult)
    {
        string path = design.Str("path");
        bool canPrint = maxmult >= quantity;
        return new UiNode(UiKind.Button)
        {
            Key = "x" + quantity,
            Color = "transparent",
            Disabled = !canPrint,
            Tooltip = MaterialCostSequence.CostText(design, sheetMaterialAmount, quantity),
            Text = "×" + quantity,
            OnClick = () =>
            {
                if (canPrint)
                    c.Act("make", ("design_path", path), ("multiplier", quantity));
            },
        };
    }
}
