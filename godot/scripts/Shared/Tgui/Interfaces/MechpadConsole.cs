using System.Collections.Generic;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MechpadConsole.jsx: the orbital mech pad console's
// pads in a column, and the selected pad's name field, its remove button and its launch button (mech only when the lifeform wire is
// mended); "No Launch Pad Connected." without the console's own pad. The section's title field stands at the top of the section (the
// kit's section titles are text). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MechpadConsole
{
    // tg: MechpadConsole.jsx:13
    private static UiNode MechpadControl(TguiContext c)
    {
        UiValue data = c.Data;
        bool connected = data.Truthy("connected_mechpad");
        bool padActive = data.Truthy("pad_active");
        bool mechonly = data.Truthy("mechonly");
        var header = new UiNode(UiKind.Stack) { Key = "header" }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                new UiNode(UiKind.Input)
                {
                    Key = "pad_name",
                    TextValue = data.Str("pad_name"),
                    Width = "200px",
                    OnCommit = value => c.Act("rename", ("name", value)),
                }),
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Button) { Key = "remove", Icon = "times", Text = "Remove", Color = "bad", OnClick = () => c.Act("remove") }));
        UiNode body = !connected
            ? new UiNode(UiKind.Box) { Key = "no-pad", Color = "bad", TextAlign = "center", Text = "No Launch Pad Connected." }
            : new UiNode(UiKind.Button)
            {
                Key = "launch", Fluid = true, Icon = "upload", Disabled = !padActive, Text = mechonly ? "Launch (Mech Only)" : "Launch",
                Color = mechonly ? "default" : "good", TextAlign = "center", OnClick = () => c.Act("launch"),
            };
        return new UiNode(UiKind.Section) { Key = "control" }.With(header, body);
    }

    // tg: MechpadConsole.jsx:56
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> mechpads = data.Arr("mechpads");
        double selectedId = data.Num("selected_id");
        UiNode content;
        if (mechpads.Count == 0)
            content = new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No Pads Connected" };
        else
        {
            var padList = new UiNode(UiKind.StackItem) { Key = "pads", Width = "140px" };
            foreach (UiValue mechpad in mechpads)
            {
                double id = mechpad.Num("id");
                string name = mechpad.Str("name");
                padList.With(new UiNode(UiKind.Button)
                {
                    Key = "pad-" + name, Fluid = true, Text = name, Selected = selectedId == id, Color = "transparent",
                    OnClick = () => c.Act("select_pad", ("id", id)),
                });
            }
            content = new UiNode(UiKind.Section) { Key = "pads-section" }.With(
                new UiNode(UiKind.Stack) { Key = "row" }.With(
                    padList,
                    new UiNode(UiKind.StackItem) { Key = "divider" }.With(new UiNode(UiKind.Divider) { Key = "div", Vertical = true }),
                    new UiNode(UiKind.StackItem) { Key = "selected", Grow = 1 }.With(
                        selectedId != 0 ? MechpadControl(c) : new UiNode(UiKind.Box) { Key = "select", Text = "Please select a pad" })));
        }
        return c.Window(475, 130, c.ConfigTitle, content);
    }
}
