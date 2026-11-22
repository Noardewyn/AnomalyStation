using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LTSRBT.tsx (gaps P4d, D298: the LTSRBT's market
// placement): the loaded item's name, price and description and Place on Market. The loaded item's picture is not sent (the server
// renders no icons, D104), so tg's picture row is left out as tg leaves it out without one. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class LTSRBT
{
    // tg: LTSRBT.tsx:23
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        c.WindowTheme = "hackerman";
        var stack = new UiNode(UiKind.Stack) { Key = "stack", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "head" }.With(
                new UiNode(UiKind.Input)
                {
                    Key = "name",
                    Width = "80%",
                    TextValue = data.Str("name"),
                    Tooltip = "Insert a name",
                    OnCommit = value => c.Act("change_name", ("value", value)),
                },
                new UiNode(UiKind.NumberInput)
                {
                    Key = "price",
                    Step = 1,
                    Width = "20%",
                    Value = data.Num("price"),
                    MinValue = data.Num("min_price"),
                    MaxValue = data.Num("max_price"),
                    Unit = "cr",
                    OnChange = value => c.Act("change_price", ("value", value)),
                }),
            new UiNode(UiKind.Divider) { Key = "divider1" });
        if (data.Truthy("loaded_icon"))
            stack.With(new UiNode(UiKind.StackItem) { Key = "icon" });
        stack.With(
            new UiNode(UiKind.Divider) { Key = "divider2" },
            new UiNode(UiKind.StackItem) { Key = "descItem", Grow = 1 }.With(new UiNode(UiKind.TextArea)
            {
                Key = "desc",
                Height = "100%",
                Fluid = true,
                TextValue = data.Str("desc"),
                Tooltip = "Insert a description (or don't)",
                OnCommit = value => c.Act("change_desc", ("value", value)),
            }),
            new UiNode(UiKind.Divider) { Key = "divider3" },
            new UiNode(UiKind.StackItem) { Key = "placeItem" }.With(c.ButtonConfirm("place", new UiNode(UiKind.Button) { Key = "place", Fluid = true, Icon = "truck-arrow-right", Text = "Place on Market", OnClick = () => c.Act("place_on_market") })));
        return c.Window(300, 380, null, stack);
    }
}
