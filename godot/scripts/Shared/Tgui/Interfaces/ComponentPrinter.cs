using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ComponentPrinter.tsx (gaps CI1, D339, D341: the component
// printer, the debug component printer and the module duplicator). Engine-free (compiled into the tests too). The window reads the
// printer's designs and materials and sends print and remove_mat, through the fabricators' design browser and material bar
// (Interfaces/Common). A recipe's cost tooltip is text (the kit's tooltips are); a design's picture is a class of tg's "design" sheet
// (TguiSpritesheets.Design32, DV44).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ComponentPrinter
{
    // tg: ComponentPrinter.tsx:20
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> materials = data.Arr("materials");
        double sheetMaterialAmount = data.Num("SHEET_MATERIAL_AMOUNT", 100);
        bool debug = data.Truthy("debug");

        // Reduce the material count array to a map of actually available materials.
        var availableMaterials = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (UiValue material in materials)
            availableMaterials[material.Str("name") ?? ""] = material.Num("amount");

        IReadOnlyList<UiValue> designs = data.Obj("designs").Select(kv => kv.Value).ToList();

        var content = new UiNode(UiKind.Stack) { Key = "root", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "browser", Grow = 1 }.With(
                DesignBrowser.Render(c, designs, design => Recipe(c, design, availableMaterials, sheetMaterialAmount, debug))),
            new UiNode(UiKind.StackItem) { Key = "materials" }.With(new UiNode(UiKind.Section) { Key = "materialSection" }.With(
                MaterialAccessBar.Render(materials, sheetMaterialAmount, (@ref, amount) => c.Act("remove_mat", ("ref", @ref), ("amount", amount))))));

        // <Window title={`${debug && 'Debug '}Component Printer`} width={670} height={600} theme={debug ? 'admin' : undefined}>: tg's
        // template writes "undefined" or "false" before the title when debug is unset; the port writes nothing then
        if (debug)
            c.WindowTheme = "admin";
        return c.Window(670, 600, (debug ? "Debug " : "") + "Component Printer", content);
    }

    // tg: ComponentPrinter.tsx:76  Recipe
    private static UiNode Recipe(TguiContext c, UiValue design, IReadOnlyDictionary<string, double> available, double sheetMaterialAmount, bool debug)
    {
        // tg sends design.path, which the module duplicator's designs lack (they carry an id, component_printer.dm:470-490), so tg's
        // duplicator never prints from its window: the id is sent when there is no path
        string path = design.Field("path") is { Kind: not UiValueKind.Null } p ? p.AsStr() : design.Str("id");
        bool canPrint = debug || !design.Obj("cost").Any(kv => !available.TryGetValue(kv.Key, out double have) || have == 0 || UiData.AsNum(kv.Value, 0) > have);

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
                        c.Act("print", ("design_path", path), ("amount", 1));
                },
            }.With(DesignBrowser.IconAndName(design.Str("icon"), design.Str("name")))));
    }
}
