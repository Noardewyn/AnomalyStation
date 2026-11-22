using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ExosuitFabricator.tsx (silicons SI3, D326: the exosuit
// fabricator). Engine-free (compiled into the tests too). The window reads the fabricator's ui_static_data (the materials' sheet size and
// the designs) and ui_data (the materials, the queue, processing) and sends build, del_queue_part, clear_queue, build_queue, stop_queue
// and remove_mat. The FabricatorRecipe rows are kit buttons; a recipe's cost tooltip is text (the kit's tooltips are), and a design's
// picture is a class of tg's "design" sheet (TguiSpritesheets.Design32, DV44).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ExosuitFabricator
{
    // tg: ExosuitFabricator.tsx:21
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> materials = data.Arr("materials");
        double sheetMaterialAmount = data.Num("SHEET_MATERIAL_AMOUNT", 100);

        var availableMaterials = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (UiValue material in materials)
            availableMaterials[material.Str("name") ?? ""] = material.Num("amount");

        IReadOnlyList<UiValue> designs = data.Obj("designs").Select(kv => kv.Value).ToList();

        var browser = DesignBrowser.Render(c, designs,
            design => Recipe(c, design, availableMaterials, sheetMaterialAmount),
            category => new UiNode(UiKind.Button)
            {
                Key = "queueAll",
                Color = "transparent",
                Text = "Queue All",
                OnClick = () => c.Act("build", ("designs", UiValue.ListOf(category.Children.Select(design => UiValue.Of(design.Str("path")))))),
            });

        var left = new UiNode(UiKind.StackItem) { Key = "left", Grow = 1 }.With(
            new UiNode(UiKind.Stack) { Key = "leftColumn", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "browser", Grow = 1 }.With(browser),
                new UiNode(UiKind.StackItem) { Key = "materials" }.With(new UiNode(UiKind.Section) { Key = "materialSection" }.With(
                    MaterialAccessBar.Render(materials, sheetMaterialAmount, (@ref, amount) => c.Act("remove_mat", ("ref", @ref), ("amount", amount)))))));

        var right = new UiNode(UiKind.StackItem) { Key = "queue", Width = "420px" }.With(Queue(c, availableMaterials, sheetMaterialAmount));

        // <Window title="Exosuit Fabricator" width={1100} height={600}>
        return c.Window(1100, 600, "Exosuit Fabricator", new UiNode(UiKind.Stack) { Key = "root", Fill = true }.With(left, right));
    }

    // tg: ExosuitFabricator.tsx:95
    private static UiNode Recipe(TguiContext c, UiValue design, IReadOnlyDictionary<string, double> available, double sheetMaterialAmount)
    {
        string path = design.Str("path");
        bool canPrint = !design.Obj("cost").Any(kv => !available.TryGetValue(kv.Key, out double have) || have == 0 || UiData.AsNum(kv.Value, 0) > have);

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
                        c.Act("build", ("designs", UiValue.ListOf(new[] { UiValue.Of(path) })), ("now", true));
                },
            }.With(DesignBrowser.IconAndName(design.Str("icon"), design.Str("name")))),
            new UiNode(UiKind.StackItem) { Key = "queue" }.With(new UiNode(UiKind.Button)
            {
                Key = "add",
                Icon = "plus-circle",
                Color = "transparent",
                Disabled = !canPrint,
                Tooltip = "Add to Queue",
                OnClick = () => c.Act("build", ("designs", UiValue.ListOf(new[] { UiValue.Of(path) }))),
            }),
            new UiNode(UiKind.StackItem) { Key = "now" }.With(new UiNode(UiKind.Button)
            {
                Key = "buildNow",
                Icon = "play",
                Color = "transparent",
                Disabled = !canPrint,
                Tooltip = "Build Now",
                OnClick = () => c.Act("build", ("designs", UiValue.ListOf(new[] { UiValue.Of(path) })), ("now", true)),
            }));
    }

    // tg: ExosuitFabricator.tsx:183
    private static UiNode Queue(TguiContext c, IReadOnlyDictionary<string, double> availableMaterials, double sheetMaterialAmount)
    {
        UiValue data = c.Data;
        bool processing = data.Truthy("processing");
        IReadOnlyList<UiValue> queue = data.Arr("queue");
        var designs = data.Obj("designs").ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        var materialCosts = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (UiValue entry in queue)
        {
            if (!designs.TryGetValue(entry.Str("designPath") ?? "", out UiValue design))
                continue;
            foreach (var (materialName, materialCost) in design.Obj("cost"))
                materialCosts[materialName] = materialCosts.GetValueOrDefault(materialName) + UiData.AsNum(materialCost, 0);
        }

        var buttons = new UiNode(UiKind.Fragment).With(
            c.ButtonConfirm("clear", new UiNode(UiKind.Button)
            {
                Disabled = queue.Count == 0,
                Color = "bad",
                Icon = "minus-circle",
                Text = "Clear Queue",
                OnClick = () => c.Act("clear_queue"),
            }),
            processing
                ? new UiNode(UiKind.Button) { Key = "stop", Disabled = queue.Count == 0, Text = "Stop", Icon = "stop", OnClick = () => c.Act("stop_queue") }
                : new UiNode(UiKind.Button) { Key = "build", Disabled = queue.Count == 0, Text = "Build Queue", Icon = "play", OnClick = () => c.Act("build_queue") });

        return new UiNode(UiKind.Section) { Key = "queueSection", Fill = true }.With(
            new UiNode(UiKind.Stack) { Key = "queueColumn", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "head" }.With(
                    new UiNode(UiKind.Section) { Key = "queueHead", Title = "Queue", Fill = true, Buttons = buttons }.With(
                        MaterialCostSequence.Render("totals", sheetMaterialAmount, availableMaterials, materialCosts))),
                new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Key = "queueList", Fill = true, Scrollable = true }.With(QueueList(c, queue, designs, availableMaterials, sheetMaterialAmount)))));
    }

    // tg: ExosuitFabricator.tsx:266
    private static UiNode QueueList(TguiContext c, IReadOnlyList<UiValue> queue, IReadOnlyDictionary<string, UiValue> designs,
        IReadOnlyDictionary<string, double> availableMaterials, double sheetMaterialAmount)
    {
        var list = new UiNode(UiKind.Fragment) { Key = "jobs" };
        if (queue.Count == 0)
            return list;

        var accumulatedCosts = new Dictionary<string, double>(StringComparer.Ordinal);
        bool firstProcessing = queue[0].Truthy("processing");
        for (int index = 0; index < queue.Count; index++)
        {
            UiValue job = queue[index];
            designs.TryGetValue(job.Str("designPath") ?? "", out UiValue design);

            bool canPrint = true;
            if (design != null)
            {
                foreach (var (material, amount) in design.Obj("cost"))
                {
                    accumulatedCosts[material] = accumulatedCosts.GetValueOrDefault(material) + UiData.AsNum(amount, 0);
                    if (accumulatedCosts[material] > availableMaterials.GetValueOrDefault(material))
                        canPrint = false;
                }
            }

            bool processing = job.Truthy("processing");
            var row = new UiNode(UiKind.Stack) { Key = "job:" + job.Str("jobId"), Vertical = true };
            if (processing)
            {
                // the FabricatorRecipe__Progress bar: timeLeft over the design's construction time
                double total = design?.Num("constructionTime") ?? 0;
                row.With(new UiNode(UiKind.StackItem) { Key = "progress" }.With(new UiNode(UiKind.ProgressBar)
                {
                    Key = "bar",
                    Value = total > 0 ? job.Num("timeLeft") / total : 0,
                    MinValue = 0,
                    MaxValue = 1,
                }));
            }

            int removeIndex = index + (firstProcessing ? 0 : 1);
            var line = new UiNode(UiKind.Stack) { Key = "line", Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "title", Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "title",
                    Fluid = true,
                    Color = "transparent",
                    Disabled = !canPrint,
                    Tooltip = design == null ? null : MaterialCostSequence.CostText(design, sheetMaterialAmount),
                }.With(DesignBrowser.IconAndName(design?.Str("icon"), design?.Str("name")))));
            if (!processing)
            {
                line.With(new UiNode(UiKind.StackItem) { Key = "remove" }.With(new UiNode(UiKind.Button)
                {
                    Key = "remove",
                    Icon = "minus-circle",
                    Color = "transparent",
                    Tooltip = "Remove from Queue",
                    OnClick = () => c.Act("del_queue_part", ("index", removeIndex)),
                }));
            }
            row.With(new UiNode(UiKind.StackItem) { Key = "lineItem" }.With(line));
            list.With(row);
        }
        return list;
    }
}
