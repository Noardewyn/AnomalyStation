using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/RapidDecorationDevice.tsx (engineering package E13).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// The decoration pictures are tg's "rdd" sprite sheet (asset_cache/assets/rdd.dm), drawn from the client's copy of tg's .dmi files
// (TguiSpritesheets, DV44).

public static class RapidDecorationDevice
{
    // tg: RapidDecorationDevice.tsx:24
    private static UiNode DecorationCard(UiValue design, bool selected, Action onClick)
    {
        string name = design.Str("name");
        return new UiNode(UiKind.ImageButton)
        {
            Key = "design-" + name,
            Selected = selected,
            Color = "transparent",
            Tooltip = TguiFormat.CapitalizeAll(name),
            DmIcon = TguiSpritesheets.Rdd32,
            DmIconState = design.Str("icon"),
            OnClick = onClick,
        };
    }

    // tg: RapidDecorationDevice.tsx:46
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> categories = data.Arr("categories");
        string selected_category = data.Str("selected_category");
        string selected_design = data.Str("selected_design", null);
        return c.Window(470, 580, null,
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Section).With(
                        new UiNode(UiKind.LabeledList).With(
                            new UiNode(UiKind.LabeledItem) { Label = "Units Left", Text = TguiFormat.JsNumber(data.Num("matter")) + "/" + TguiFormat.JsNumber(data.Num("max_matter")) + " Units" },
                            new UiNode(UiKind.LabeledItem) { Label = "Selected", Text = !string.IsNullOrEmpty(selected_design) ? TguiFormat.CapitalizeAll(selected_design) : "None" }))),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Scrollable = true, Fill = true }.With(
                        categories.Select(category =>
                        {
                            string cat_name = category.Str("cat_name");
                            return new UiNode(UiKind.Section) { Key = "category-" + cat_name }.With(
                                new UiNode(UiKind.Stack) { Wrap = true }.With(
                                    category.Arr("designs").Select(design => new UiNode(UiKind.StackItem) { Key = design.Str("name") }.With(
                                        DecorationCard(design,
                                            design.Str("name") == selected_design && cat_name == selected_category,
                                            () => c.Act("design", ("category", cat_name), ("name", design.Str("name"))))))));
                        })))));
    }
}
