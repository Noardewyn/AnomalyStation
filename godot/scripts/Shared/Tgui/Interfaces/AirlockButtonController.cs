using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AirlockButtonController.tsx (package E8: the access
// consoles of virology's and xenobiology's airlocks). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AirlockButtonController
{
    // tg: AirlockButtonController.tsx:15
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string interior_door = data.Str("interior_door", null);
        string exterior_door = data.Str("exterior_door", null);

        // tg: AirlockButtonController.tsx:19  <Window width={500} height={130}>
        return c.Window(500, 130, null,
            new UiNode(UiKind.Section) { Title = "Airlock Controller", TextAlign = "center" }.With(
                string.IsNullOrEmpty(interior_door) && string.IsNullOrEmpty(exterior_door)
                    ? new UiNode(UiKind.NoticeBox) { Key = "no-doors", Color = "danger", Text = "No doors detected" }
                    : new UiNode(UiKind.Stack).With(
                        string.IsNullOrEmpty(interior_door) ? null : new UiNode(UiKind.StackItem) { Grow = 1, TextAlign = "center" }.With(RetrieveButton(c, interior_door)),
                        string.IsNullOrEmpty(exterior_door) ? null : new UiNode(UiKind.StackItem) { Grow = 1, TextAlign = "center" }.With(RetrieveButton(c, exterior_door)))));
    }

    // tg: AirlockButtonController.tsx:43
    private static UiNode RetrieveButton(TguiContext c, string airlockType)
    {
        UiValue data = c.Data;
        string interior_door = data.Str("interior_door", null);
        string exterior_door = data.Str("exterior_door", null);
        bool interior = airlockType == interior_door;
        bool our_door_closed = data.Truthy(interior ? "interior_door_closed" : "exterior_door_closed");
        bool opposite_door_closed = data.Truthy(interior ? "exterior_door_closed" : "interior_door_closed");
        string opposite_door = interior ? exterior_door : interior_door;
        string which = interior ? "interior door" : "exterior door";

        return new UiNode(UiKind.Button)
        {
            Key = interior ? "interior" : "exterior",
            Icon = our_door_closed ? "lock-open" : "lock",
            Color = "green",
            FontSize = 1.5,
            TextAlign = "center",
            Disabled = data.Truthy("busy"),
            OnClick = () =>
            {
                if (!our_door_closed)
                    c.Act("close", ("requested_door", airlockType));
                else
                    c.Act("open", ("requested_door", airlockType));
            },
            Text = !our_door_closed
                ? "Close " + which
                : !opposite_door_closed && !string.IsNullOrEmpty(opposite_door)
                    ? "Cycle to " + which
                    : "Open " + which,
        };
    }
}
