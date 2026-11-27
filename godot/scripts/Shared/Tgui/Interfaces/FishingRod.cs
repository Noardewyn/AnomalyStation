using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/FishingRod.tsx. The rod's three slots (bait, line,
// hook), each a button with the slotted part's picture and name that takes, swaps or removes the part in hand, and the rod's
// description under them. tg sends each part's picture as a PNG; the port's server sends the part's icon file and state, drawn
// from the client's copy of tg's sprites (DV679).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class FishingRod
{
    // tg: FishingRod.tsx:59
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        // tg: FishingRod.tsx:72  <Window height={450} width={400}>
        return c.Window(400, 450, null,
            new UiNode(UiKind.Section) { Key = "slots" }.With(
                Slot(c, "Bait", "bait", data.Get("bait_name"), data.Get("bait_icon")),
                Slot(c, "Line", "line", data.Get("line_name"), data.Get("line_icon")),
                Slot(c, "Hook", "hook", data.Get("hook_name"), data.Get("hook_icon"))),
            new UiNode(UiKind.Section) { Key = "description", Text = data.Str("description") });
    }

    // tg: FishingRod.tsx:24  FishingRodSlot
    private static UiNode Slot(TguiContext c, string name, string slot, UiValue currentItemName, UiValue currentItemIcon)
    {
        bool hasIcon = currentItemIcon != null && !currentItemIcon.IsNull;
        return new UiNode(UiKind.Section) { Key = "slot-" + slot, Title = name }.With(
            new UiNode(UiKind.Stack) { Key = "row" }.With(
                new UiNode(UiKind.StackItem) { Key = "button", Grow = 1 }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "action",
                        Fluid = true,
                        OnClick = () => c.Act("slot_action", ("slot", slot)),
                    }.With(
                        new UiNode(UiKind.Stack) { Key = "content" }.With(
                            new UiNode(UiKind.StackItem) { Key = "picture" }.With(
                                // <Image width="64px" height="64px" src={`data:image/jpeg;base64,${current_item_icon}`} />
                                hasIcon
                                    ? new UiNode(UiKind.Box)
                                    {
                                        Key = "icon",
                                        DmIcon = currentItemIcon.Str("icon"),
                                        DmIconState = currentItemIcon.Str("icon_state"),
                                        Width = "64px",
                                        Height = "64px",
                                    }
                                    : null),
                            new UiNode(UiKind.StackItem) { Key = "name", Grow = 1 }.With(
                                // {current_item_name ?? 'None'}
                                new UiNode(UiKind.Box)
                                {
                                    Key = "label",
                                    TextAlign = "center",
                                    Text = currentItemName == null || currentItemName.IsNull ? "None" : currentItemName.AsStr(),
                                }))))));
    }
}
