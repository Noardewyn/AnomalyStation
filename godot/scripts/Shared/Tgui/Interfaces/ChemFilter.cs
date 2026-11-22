using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemFilter.tsx (engineering package E15, plumbing).
// The window reads tg's ui_data of the chemical filter (filter.dm:25) and sends add and remove for its left and right lists.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ChemFilter
{
    // tg: ChemFilter.tsx:21
    internal static UiNode ChemFilterPane(TguiContext c, string title, IReadOnlyList<UiValue> list, string buttonColor)
    {
        string titleKey = title.ToLowerInvariant();
        return new UiNode(UiKind.Section)
        {
            Title = title,
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "add-" + titleKey,
                Icon = "plus",
                Color = buttonColor,
                Text = "Add Reagent",
                OnClick = () => c.Act("add", ("which", titleKey)),
            },
        }.With(
            list.Select(v => v.AsStr()).Select(filter => new UiNode(UiKind.Button)
            {
                Key = titleKey + "-" + filter,
                Fluid = true,
                Icon = "minus",
                Text = filter,
                OnClick = () => c.Act("remove", ("which", titleKey), ("reagent", filter)),
            }));
    }

    // tg: ChemFilter.tsx:63
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        // tg: ChemFilter.tsx:68  <Window width={500} height={300}>
        return c.Window(500, 300, null,
            new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(ChemFilterPane(c, "Right", data.Arr("right"), "red")),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(ChemFilterPane(c, "Left", data.Arr("left"), "yellow"))));
    }
}
