using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ManufacturingSorter.tsx (engineering package E16). The
// window reads tg's ui_data of the conveyor sort-router (manufactorio/machines/sorter.dm:50) and sends its acts: new_filter, rotate,
// edit, del_filter, shift and rotate_unmet.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ManufacturingSorter
{
    // tg: ManufacturingSorter.tsx:19
    private static string Dir2Icon(double dir) => dir switch
    {
        1 => "arrow-up",
        2 => "arrow-down",
        4 => "arrow-right",
        8 => "arrow-left",
        _ => "arrow-up",
    };

    // tg: ManufacturingSorter.tsx:41
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> filters = data.Arr("filters");
        double unmetDir = data.Num("unmet_dir");

        var list = new UiNode(UiKind.LabeledList);
        for (int i = 0; i < filters.Count; i++)
        {
            UiValue filter = filters[i];
            string @ref = filter.Str("ref");
            // tg: ManufacturingSorter.tsx:65
            list.With(new UiNode(UiKind.LabeledItem) { Key = "filter:" + @ref, Label = $"{i + 1}. {filter.Str("name")}" }.With(
                new UiNode(UiKind.Button) { Key = "rotate:" + @ref, Icon = Dir2Icon(filter.Num("dir")), Text = "Output", OnClick = () => c.Act("rotate", ("ref", @ref)) },
                new UiNode(UiKind.Button) { Key = "edit:" + @ref, Icon = "pencil", OnClick = () => c.Act("edit", ("ref", @ref)) },
                new UiNode(UiKind.Button) { Key = "del:" + @ref, Icon = "trash", Color = "red", OnClick = () => c.Act("del_filter", ("ref", @ref)) },
                new UiNode(UiKind.Button) { Key = "up:" + @ref, Icon = "arrow-up", OnClick = () => c.Act("shift", ("ref", @ref), ("amount", -1)) },
                new UiNode(UiKind.Button) { Key = "down:" + @ref, Icon = "arrow-down", OnClick = () => c.Act("shift", ("ref", @ref), ("amount", 1)) }));
        }

        // tg: ManufacturingSorter.tsx:46  <Window width={450} height={350} title="Manufacturing Sorter">
        return c.Window(450, 350, "Manufacturing Sorter",
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Height = "90%" }.With(
                    // tg: ManufacturingSorter.tsx:51
                    new UiNode(UiKind.Section)
                    {
                        Key = "filters",
                        Title = "Filters",
                        Height = "100%",
                        Scrollable = true,
                        Buttons = new UiNode(UiKind.Button) { Key = "new_filter", Color = "green", Icon = "plus", Text = "New filter", OnClick = () => c.Act("new_filter") },
                    }.With(list)),
                // tg: ManufacturingSorter.tsx:105
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Stack).With(
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = "If no criteria is met, outputting to:" }),
                        new UiNode(UiKind.StackItem).With(
                            new UiNode(UiKind.Button) { Key = "rotate_unmet", Icon = Dir2Icon(unmetDir), OnClick = () => c.Act("rotate_unmet") })))));
    }
}
