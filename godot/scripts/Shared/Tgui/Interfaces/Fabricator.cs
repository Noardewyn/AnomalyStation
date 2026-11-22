using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Fabricator.tsx (gaps RD1, D333: the protolathes, circuit
// imprinters and techfabs). Engine-free (compiled into the tests too). The window reads the fabricator's ui_static_data (the materials'
// sheet size, the designs and its name) and ui_data (the materials, onHold, busy) and sends build and remove_mat. It shares the exosuit
// fabricator's design browser, material bar and cost sequence (Interfaces/Common). The FabricatorRecipe rows are kit buttons; a cost
// tooltip is text (the kit's tooltips are); a design's picture is a class of tg's "design" sheet (TguiSpritesheets.Design32, DV44);
// Button.Input is an input that commits the amount (as OreRedemptionMachine's). The on-hold Dimmer is the kit's Modal.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Fabricator
{
    // tg: Fabricator.tsx:19
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string fabName = data.Str("fabName");
        bool onHold = data.Truthy("onHold");
        bool busy = data.Truthy("busy");
        double sheetMaterialAmount = data.Num("SHEET_MATERIAL_AMOUNT", 100);
        IReadOnlyList<UiValue> materials = data.Arr("materials");

        // Reduce the material count array to a map of actually available materials.
        var availableMaterials = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (UiValue material in materials)
            availableMaterials[material.Str("name") ?? ""] = material.Num("amount");

        IReadOnlyList<UiValue> designs = data.Obj("designs").Select(kv => kv.Value).ToList();

        var content = new UiNode(UiKind.Stack) { Key = "root", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "browser", Grow = 1 }.With(
                DesignBrowser.Render(c, designs, design => Recipe(c, design, availableMaterials, sheetMaterialAmount), busy: busy)),
            new UiNode(UiKind.StackItem) { Key = "materials" }.With(new UiNode(UiKind.Section) { Key = "materialSection" }.With(
                MaterialAccessBar.Render(materials, sheetMaterialAmount, (@ref, amount) => c.Act("remove_mat", ("ref", @ref), ("amount", amount))))));

        // tg: Fabricator.tsx:61  {!!onHold && <Dimmer>Mineral access is on hold, please contact the quartermaster.</Dimmer>}
        UiNode dimmer = onHold
            ? new UiNode(UiKind.Modal) { Key = "onHold" }.With(new UiNode(UiKind.Box)
            {
                Key = "onHoldText",
                FontSize = "2em",
                TextAlign = "center",
                Text = "Mineral access is on hold, please contact the quartermaster.",
            })
            : null;

        // <Window title={fabName} width={670} height={600}>
        return c.Window(670, 600, fabName, dimmer, content);
    }

    /// <summary>tg: Fabricator.tsx:85  canPrint: every material of the cost is there <paramref name="quantity"/> times over.</summary>
    private static bool CanPrint(UiValue design, IReadOnlyDictionary<string, double> available, double quantity) =>
        !design.Obj("cost").Any(kv => !available.TryGetValue(kv.Key, out double have) || have == 0 || UiData.AsNum(kv.Value, 0) * quantity > have);

    // tg: Fabricator.tsx:78  PrintButton
    private static UiNode PrintButton(TguiContext c, UiValue design, int quantity, IReadOnlyDictionary<string, double> available, double sheetMaterialAmount)
    {
        string path = design.Str("path");
        return new UiNode(UiKind.Button)
        {
            Key = "x" + quantity,
            Color = "transparent",
            Disabled = !CanPrint(design, available, quantity), // FabricatorRecipe__Button--disabled
            Tooltip = MaterialCostSequence.CostText(design, sheetMaterialAmount, quantity),
            Text = "×" + quantity,
            OnClick = () => c.Act("build", ("design_path", path), ("amount", quantity)),
        };
    }

    // tg: Fabricator.tsx:116  CustomPrint
    private static UiNode CustomPrint(TguiContext c, UiValue design, IReadOnlyDictionary<string, double> available)
    {
        string path = design.Str("path");
        double maxMult = design.Obj("cost").Aggregate(double.PositiveInfinity,
            (accumulator, kv) => Math.Min(accumulator, available.GetValueOrDefault(kv.Key) / UiData.AsNum(kv.Value, 0)));
        maxMult = Math.Min(Math.Floor(maxMult), 50);

        // <Button.Input buttonText={`[Max: ${maxMult}]`}>; the --disabled class of its box only greys it in tg
        return new UiNode(UiKind.Input)
        {
            Key = "max",
            Width = 6.0,
            Tooltip = "[Max: " + TguiFormat.JsNumber(maxMult) + "]",
            OnCommit = value => c.Act("build", ("design_path", path), ("amount", value)),
        };
    }

    // tg: Fabricator.tsx:150  Recipe
    private static UiNode Recipe(TguiContext c, UiValue design, IReadOnlyDictionary<string, double> available, double sheetMaterialAmount)
    {
        string path = design.Str("path");
        bool canPrint = CanPrint(design, available, 1);

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
                        c.Act("build", ("design_path", path), ("amount", 1));
                },
            }.With(DesignBrowser.IconAndName(design.Str("icon"), design.Str("name")))),
            new UiNode(UiKind.StackItem) { Key = "five" }.With(PrintButton(c, design, 5, available, sheetMaterialAmount)),
            new UiNode(UiKind.StackItem) { Key = "ten" }.With(PrintButton(c, design, 10, available, sheetMaterialAmount)),
            new UiNode(UiKind.StackItem) { Key = "custom" }.With(CustomPrint(c, design, available)));
    }
}
