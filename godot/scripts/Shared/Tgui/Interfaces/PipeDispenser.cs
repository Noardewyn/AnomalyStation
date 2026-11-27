using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PipeDispenser.tsx (the pipe, disposal pipe and transit
// tube dispensers), with the colour row, the smart pipe panel (RapidPipeDispenser.tsx) and the layer row (RapidPlumbingDevice.tsx)
// shared with the RPD's window. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class PipeDispenser
{
    // tg: PipeDispenser.tsx:72
    private static UiNode PipeTypeSection(TguiContext c)
    {
        IReadOnlyList<UiValue> categories = c.Data.Arr("categories");
        // React's useState(categories[0].cat_name): the value at the window's first render, kept
        string categoryName = c.Local.Get<string>("categoryName", null);
        if (categoryName == null)
        {
            categoryName = categories.FirstOrDefault()?.Str("cat_name") ?? "";
            c.Local.SetQuiet("categoryName", categoryName);
        }
        UiValue shownCategory = categories.FirstOrDefault(category => category.Str("cat_name") == categoryName) ?? categories.FirstOrDefault();
        string shownName = shownCategory?.Str("cat_name");

        return new UiNode(UiKind.Section) { Key = "pipe-types", Fill = true, Scrollable = true }.With(
            new UiNode(UiKind.Tabs).With(
                categories.Select(category =>
                {
                    string name = category.Str("cat_name");
                    return new UiNode(UiKind.Tab)
                    {
                        Key = "tab-" + name,
                        Icon = RapidPipeDispenser.ICON_BY_CATEGORY_NAME.TryGetValue(name, out string icon) ? icon : null,
                        Selected = name == shownName,
                        Text = name,
                        OnClick = () => c.SetState("categoryName", name),
                    };
                })),
            new UiNode(UiKind.Fragment) { Key = "recipes" }.With(
                (shownCategory?.Arr("recipes") ?? System.Array.Empty<UiValue>()).Select(recipe =>
                {
                    double index = recipe.Num("pipe_index");
                    double dir = recipe.Num("dir");
                    string pipeName = recipe.Str("pipe_name");
                    return new UiNode(UiKind.Button)
                    {
                        Key = "recipe-" + index + "-" + dir,
                        Fluid = true,
                        Tooltip = pipeName,
                        Text = pipeName,
                        OnClick = () => c.Act("pipe_type", ("pipe_type", index), ("pipe_dir", dir), ("category", shownName)),
                    };
                })));
    }

    // tg: PipeDispenser.tsx:113
    public static UiNode Render(TguiContext c)
    {
        double rootCategoryIndex = c.Data.Num("category");
        // tg: PipeDispenser.tsx:117  <Window width={530} height={530}>
        return c.Window(530, 530, null,
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                rootCategoryIndex == 0
                    ? new UiNode(UiKind.StackItem) { Key = "atmos" }.With(
                        new UiNode(UiKind.Stack) { Fill = true }.With(
                            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                                new UiNode(UiKind.Section).With(
                                    new UiNode(UiKind.LabeledList).With(
                                        RapidPipeDispenser.ColorItem(c),
                                        RapidPlumbingDevice.LayerSelect(c)))),
                            new UiNode(UiKind.StackItem) { Width = "90px" }.With(RapidPipeDispenser.SmartPipeBlockSection(c))))
                    : null,
                new UiNode(UiKind.StackItem) { Key = "types", Grow = 1 }.With(PipeTypeSection(c))));
    }
}
