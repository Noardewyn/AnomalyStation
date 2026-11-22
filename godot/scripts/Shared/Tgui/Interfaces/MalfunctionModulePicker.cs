using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MalfunctionModulePicker.tsx (silicons SI8, D326): the
// malfunctioning AI's module shop, tg's GenericUplink (Uplink.cs) over the modules with the hacked APC count. Engine-free (compiled into
// the tests too). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MalfunctionModulePicker
{
    // tg: MalfunctionModulePicker.tsx:25
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double processingTime = data.Num("processingTime");
        double hackedAPCs = data.Num("hackedAPCs");
        var categories = data.Arr("categories").Select(v => v.AsStr()).ToList();
        var items = data.Arr("modules").Select(module =>
        {
            double cost = module.Num("cost"), minimumApcs = module.Num("minimumApcs");
            return new Uplink.Item(
                Id: module.Str("name"),
                Name: module.Str("name"),
                Icon: module.Str("icon"),
                IconState: module.Str("icon_state"),
                Category: module.Str("category"),
                Desc: module.Str("desc") + (minimumApcs > 0 ? " Requires at least " + TguiFormat.JsNumber(minimumApcs) + " APCs hacked." : ""),
                LockOthers: false,
                Cost: TguiFormat.JsNumber(cost) + " PT",
                PopulationTooltip: "",
                InsufficientPopulation: false,
                Disabled: processingTime < cost || hackedAPCs < minimumApcs,
                Ref: null);
        }).ToList();

        c.WindowTheme = "malfunction";
        return c.Window(620, 525, null,
            new UiNode(UiKind.Stack) { Key = "picker", Vertical = true, Fill = true }.With(
                // tg's inline boxes after the terminal icon: one row here
                new UiNode(UiKind.StackItem) { Key = "apcs" }.With(new UiNode(UiKind.Section) { Key = "apcs" }.With(new UiNode(UiKind.Stack) { Key = "row", Align = "center" }.With(
                    new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Key = "icon", Icon = "terminal" }),
                    new UiNode(UiKind.StackItem) { Key = "label" }.With(new UiNode(UiKind.Box) { Key = "label", Bold = true, Text = "Hacked APCs:" }),
                    new UiNode(UiKind.StackItem) { Key = "count" }.With(new UiNode(UiKind.Box) { Key = "count", Text = TguiFormat.JsNumber(hackedAPCs) })))),
                new UiNode(UiKind.StackItem) { Key = "shop", Grow = 1 }.With(
                    Uplink.GenericUplink(c, TguiFormat.JsNumber(processingTime) + " PT", categories, items, item => c.Act("buy", ("name", item.Name))))));
    }
}
