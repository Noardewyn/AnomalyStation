using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ElevatorPanel.tsx (D325, multi-z: the elevator panel,
// elev_panel.dm's ui_data and ui_static_data; it sends move_lift, emergency_door and reset_doors). As the other windows of the kit: a
// Dimmer is a notice over the window's top (the kit has no overlay), Blink draws its icon steadily, the Section's align is its text align.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ElevatorPanel
{
    // tg: ElevatorPanel.tsx:42
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool is_emergency = data.Truthy("is_emergency");
        bool doors_open = data.Truthy("doors_open");
        bool lift_exists = data.Truthy("lift_exists");
        bool currently_moving = data.Truthy("currently_moving");
        double current_floor = data.Num("current_floor");
        string emergency_level = data.Str("emergency_level");
        IReadOnlyList<UiValue> all_floor_data = data.Arr("all_floor_data");

        // tg: ElevatorPanel.tsx:68  const calculatedHeight = clamp(all_floor_data.length * 90, 400, 600);
        double calculatedHeight = Math.Clamp(all_floor_data.Count * 90, 400, 600);

        var floors = new UiNode(UiKind.Stack) { Key = "floors", Vertical = true, Width = "90%" };
        for (int index = 0; index < all_floor_data.Count; index++)
        {
            UiValue floor = all_floor_data[index];
            double z_level = floor.Num("z_level");
            floors.Children.Add(new UiNode(UiKind.StackItem) { Key = "floor" + index }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "button",
                    Bold = true,
                    FontSize = "14px",
                    Fluid = true,
                    TextAlign = "left",
                    Icon = "circle",
                    Disabled = z_level == current_floor,
                    OnClick = () => c.Act("move_lift", ("z", z_level)),
                    Text = floor.Str("name"),
                }));
        }

        // tg: ElevatorPanel.tsx:116  the door button: Reset Doors while they are held open, else Emergency (red alert and above)
        UiNode doorButton = doors_open
            ? new UiNode(UiKind.Button)
            {
                Key = "reset",
                Width = "65%",
                Icon = "door-closed",
                Tooltip = "Closes all elevator doors, except those on the level of the elevator.",
                OnClick = () => c.Act("reset_doors"),
                Text = "Reset Doors",
            }
            : new UiNode(UiKind.Button)
            {
                Key = "emergency",
                Width = "65%",
                Icon = "door-open",
                Disabled = !is_emergency,
                Color = "bad",
                Tooltip = is_emergency ? "In case of emergency, opens all lift doors." : $"The station is only at {emergency_level} alert.",
                OnClick = () => c.Act("emergency_door"),
                Text = "Emergency",
            };

        // tg: ElevatorPanel.tsx:70  <Window width={200} height={calculatedHeight} theme="retro">
        c.WindowTheme = "retro";
        return c.Window(200, calculatedHeight, null,
            !lift_exists ? NoLiftDimmer() : null,
            new UiNode(UiKind.Stack) { Key = "body", Vertical = true, Height = "100%" }.With(
                new UiNode(UiKind.StackItem) { Key = "top" }.With(
                    new UiNode(UiKind.Section) { Key = "floor", Title = "Floor", TextAlign = "center" }.With(FloorPanel(c))),
                new UiNode(UiKind.StackItem) { Key = "middle", Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Key = "floors", Fill = true, TextAlign = "center" }.With(
                        currently_moving ? MovingDimmer() : null,
                        floors)),
                new UiNode(UiKind.StackItem) { Key = "bottom" }.With(
                    new UiNode(UiKind.Section) { Key = "doors" }.With(doorButton))));
    }

    // tg: ElevatorPanel.tsx:138
    private static UiNode NoLiftDimmer()
        => new UiNode(UiKind.NoticeBox) { Key = "no-lift", TextAlign = "center" }.With(
            new UiNode(UiKind.Stack) { Key = "stack", Vertical = true, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Key = "icon", Icon = "exclamation", Size = 8 }),
                new UiNode(UiKind.StackItem) { Key = "text", FontSize = "16px", Text = "No elevator connected." }));

    // tg: ElevatorPanel.tsx:151
    private static UiNode MovingDimmer()
        => new UiNode(UiKind.NoticeBox) { Key = "moving", TextAlign = "center" }.With(
            new UiNode(UiKind.Stack) { Key = "stack", Vertical = true, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Key = "icon", Icon = "spinner", Spin = true, Size = 8 }),
                new UiNode(UiKind.StackItem) { Key = "text", FontSize = "16px", Text = "Moving..." }));

    // tg: ElevatorPanel.tsx:164
    private static UiNode FloorPanel(TguiContext c)
    {
        UiValue data = c.Data;
        double current_floor = data.Num("current_floor");
        bool currently_moving = data.Truthy("currently_moving");
        double currently_moving_to_floor = data.Num("currently_moving_to_floor");

        return new UiNode(UiKind.Stack) { Key = "panel", Width = "50%", Background = "black", Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "arrows" }.With(
                new UiNode(UiKind.Stack) { Key = "stack", Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "up" }.With(
                        ArrowIcon("arrow-up", currently_moving && currently_moving_to_floor != 0 && currently_moving_to_floor > current_floor)),
                    new UiNode(UiKind.StackItem) { Key = "down" }.With(
                        ArrowIcon("arrow-down", currently_moving && currently_moving_to_floor != 0 && currently_moving_to_floor < current_floor)))),
            new UiNode(UiKind.StackItem) { Key = "number" }.With(
                new UiNode(UiKind.Box) { Key = "number", Color = "white", FontSize = "50px", Bold = true, Text = TguiFormat.JsNumber(current_floor - 1) }));
    }

    // tg: ElevatorPanel.tsx:209
    private static UiNode ArrowIcon(string icon, bool is_moving)
        => new UiNode(UiKind.Icon) { Key = icon, Icon = icon, Color = is_moving ? "green" : "grey", Size = 2 };
}
