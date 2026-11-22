using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Blueprints.tsx (engineering package E14). The window reads
// tg's ui_data and ui_static_data of the station blueprints (blueprints.dm:64, :95) and sends tg's acts: create_area, edit_area,
// toggle_allow_shuttle_docking, view_legend, exit_legend, view_wireset, view_blueprints, hide_blueprints and refresh. Original copyright
// and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// Not drawn: the fluff notice's italics, the buttons' centred text and padding, the wire buttons' ellipsis and flex basis, the stack's
// gaps (the kit has no such styles).

public static class Blueprints
{
    // tg: tgui/packages/tgui/interfaces/Blueprints.tsx:32
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string legend = data.Str("legend");
        // tg: Blueprints.tsx:37  <Window width={450} height={340}> <Window.Content scrollable>
        return c.Window(450, 340, null,
            legend == data.Str("legend_viewing_list") ? WireList(c)
            : legend == data.Str("legend_off") ? MainMenu(c)
            : WireArea(c));
    }

    // tg: Blueprints.tsx:52
    private static UiNode WireList(TguiContext c) => new UiNode(UiKind.Section).With(
        new UiNode(UiKind.Button) { Key = "back", Fluid = true, Icon = "chevron-left", Text = "Back", OnClick = () => c.Act("exit_legend") },
        new UiNode(UiKind.Stack) { Key = "devices", Fill = true, Wrap = true }.With(
            c.Data.Arr("wire_devices").Select(wire =>
            {
                string reference = wire.Str("ref");
                return new UiNode(UiKind.StackItem) { Key = reference, Grow = 1 }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "wireset:" + reference,
                        Fluid = true,
                        Text = wire.Str("name"),
                        OnClick = () => c.Act("view_wireset", ("view_wireset", reference)),
                    });
            })));

    // tg: Blueprints.tsx:80
    private static UiNode WireArea(TguiContext c) => new UiNode(UiKind.Section).With(
        new UiNode(UiKind.Button) { Key = "back", Fluid = true, Icon = "chevron-left", Text = "Back", OnClick = () => c.Act("view_legend") },
        new UiNode(UiKind.Box) { Key = "wires_name", Bold = true, Text = c.Data.Str("wires_name") + " wires:" },
        new UiNode(UiKind.Fragment).With(c.Data.Arr("wire_data").Select(wire =>
        {
            // <Box ml={1} m={1}><span style={{ color: wire.color }}>{wire.color}</span>: {wire.message}</Box>
            string color = wire.Str("color");
            return new UiNode(UiKind.Stack) { Key = "wire:" + wire.Str("message") }.With(
                new UiNode(UiKind.Box) { Key = "color", Color = color, Text = color },
                new UiNode(UiKind.Box) { Key = "message", Text = ": " + wire.Str("message") });
        })));

    // tg: Blueprints.tsx:100
    private static UiNode MainMenu(TguiContext c)
    {
        UiValue data = c.Data;
        bool viewing = data.Truthy("viewing");
        return new UiNode(UiKind.Section) { Title = data.Str("station_name") + " blueprints" }.With(
            new UiNode(UiKind.Box) { Key = "fluff", FontSize = 0.9, Text = data.Str("fluff_notice"), Wrap = true },
            new UiNode(UiKind.Divider),
            new UiNode(UiKind.Box) { Key = "notice", Bold = true, TextAlign = "center", Text = data.Str("area_notice") },
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem).With(MenuButton("create", "pencil", "Create or modify an existing area", () => c.Act("create_area"))),
                new UiNode(UiKind.StackItem).With(MenuButton("rename", "font", "Change area name", () => c.Act("edit_area"))),
                // tg: Blueprints.tsx:139  iconPosition="right" (the kit draws the icon first)
                new UiNode(UiKind.StackItem).With(MenuButton("docking", data.Truthy("area_allows_shuttle_docking") ? "toggle-on" : "toggle-off",
                    "Allow shuttle docking", () => c.Act("toggle_allow_shuttle_docking"))),
                new UiNode(UiKind.StackItem).With(MenuButton("legend", "chevron-right", "View wire color legend", () => c.Act("view_legend"))),
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Stack) { Fill = true }.With(
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                            new UiNode(UiKind.Button)
                            {
                                Key = "structure",
                                Fluid = true,
                                Icon = "wrench",
                                Color = viewing ? "red" : null,
                                Text = (viewing ? "Hide" : "View") + " structural data",
                                OnClick = () => c.Act(viewing ? "hide_blueprints" : "view_blueprints"),
                            }),
                        viewing
                            ? new UiNode(UiKind.StackItem).With(
                                new UiNode(UiKind.Button) { Key = "refresh", Icon = "refresh", Tooltip = "Refresh structural data", OnClick = () => c.Act("refresh") })
                            : null))));
    }

    private static UiNode MenuButton(string key, string icon, string text, Action act) =>
        new UiNode(UiKind.Button) { Key = key, Fluid = true, Icon = icon, Text = text, OnClick = act };
}
