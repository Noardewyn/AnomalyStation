using System.Collections.Generic;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LaunchpadConsole.tsx: the launchpad console's pads
// as vertical tabs and the selected pad's control (its name field, the offset buttons and fields, launch and pull), which the
// briefcase launchpad's LaunchpadRemote window draws too. Original copyright and license: tgstation, AGPL-3.0 (code). See
// ../tgstation/LICENSE.

public static class LaunchpadConsole
{
    // tg: LaunchpadConsole.tsx:32  buttonConfigs (x or y left out is not sent, as tgui's act drops undefined)
    private static readonly (string Icon, double Rotation, string Text, int? X, int? Y)[][] ButtonConfigs =
    {
        new (string, double, string, int?, int?)[] { ("arrow-left", 45, null, -1, 1), ("arrow-left", 0, null, -1, null), ("arrow-down", 45, null, -1, -1) },
        new (string, double, string, int?, int?)[] { ("arrow-up", 0, null, null, 1), (null, 0, "R", 0, 0), ("arrow-down", 0, null, null, -1) },
        new (string, double, string, int?, int?)[] { ("arrow-up", 45, null, 1, 1), ("arrow-right", 0, null, 1, null), ("arrow-right", 45, null, 1, -1) },
    };

    // tg: LaunchpadConsole.tsx:47
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> launchpads = data.Arr("launchpads");
        double selectedId = data.Num("selected_id");
        UiNode content = launchpads.Count == 0
            ? new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No Pads Connected" }
            : new UiNode(UiKind.Stack) { Key = "body", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "tabs", Grow = 1 }.With(LaunchpadTabs(c)),
                new UiNode(UiKind.StackItem) { Key = "control", Grow = 3 }.With(
                    selectedId == 0 ? new UiNode(UiKind.Box) { Key = "select", Text = "Please select a pad" } : LaunchpadControl(c)));
        return c.Window(475, 275, c.ConfigTitle, content);
    }

    // tg: LaunchpadConsole.tsx:104
    private static UiNode LaunchpadTabs(TguiContext c)
    {
        UiValue data = c.Data;
        double selectedId = data.Num("selected_id");
        var tabs = new UiNode(UiKind.Tabs) { Key = "pads", Vertical = true };
        foreach (UiValue pad in data.Arr("launchpads"))
        {
            double id = pad.Num("id");
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "pad-" + id, Selected = id == selectedId, Text = pad.Str("name"),
                OnClick = () => c.Act("select_pad", ("id", id)),
            });
        }
        return new UiNode(UiKind.Section) { Fill = true }.With(tabs);
    }

    // tg: LaunchpadConsole.tsx:79
    public static UiNode LaunchpadControl(TguiContext c) =>
        new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(LaunchpadTitle(c)),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                new UiNode(UiKind.Section) { Fill = true }.With(
                    new UiNode(UiKind.Stack) { Fill = true }.With(
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(LaunchpadButtonPad(c)),
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(TargetingControls(c))))),
            new UiNode(UiKind.StackItem).With(DeliveryButtons(c)));

    // tg: LaunchpadConsole.tsx:132
    private static UiNode LaunchpadTitle(TguiContext c) =>
        new UiNode(UiKind.Section).With(
            new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Input)
                    {
                        Key = "pad_name",
                        TextValue = c.Data.Str("pad_name"),
                        Width = "170px",
                        OnCommit = value => c.Act("rename", ("name", value)),
                    }),
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Button) { Key = "remove", Icon = "times", Color = "bad", Text = "Remove", OnClick = () => c.Act("remove") })));

    // tg: LaunchpadConsole.tsx:157
    private static UiNode LaunchpadButtonPad(TguiContext c)
    {
        var rows = new List<UiNode>();
        for (int i = 0; i < ButtonConfigs.Length; i++)
        {
            var row = new UiNode(UiKind.StackItem) { Key = "row" + i };
            for (int j = 0; j < ButtonConfigs[i].Length; j++)
            {
                var config = ButtonConfigs[i][j];
                var parameters = new List<(string, object)>();
                if (config.X != null)
                    parameters.Add(("x", config.X.Value));
                if (config.Y != null)
                    parameters.Add(("y", config.Y.Value));
                row.With(new UiNode(UiKind.Button)
                {
                    Key = "move" + i + "-" + j,
                    Fluid = true,
                    Icon = config.Icon,
                    Rotation = config.Rotation,
                    Text = config.Text,
                    OnClick = () => c.Act("move_pos", parameters.ToArray()),
                });
            }
            rows.Add(row);
        }
        return new UiNode(UiKind.Section) { Fill = true, Title = "Controls", TextAlign = "center" }.With(
            new UiNode(UiKind.Stack) { Fill = true, Justify = "center" }.With(rows.ToArray()));
    }

    // tg: LaunchpadConsole.tsx:189
    private static UiNode TargetingControls(TguiContext c)
    {
        UiValue data = c.Data;
        double range = data.Num("range");
        UiNode Axis(string axis, double value, string icon) =>
            new UiNode(UiKind.Stack) { Key = "axis-" + axis }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Box) { FontSize = "26px", Text = axis.ToUpperInvariant() + " " }.With(
                        new UiNode(UiKind.Icon) { Icon = icon })),
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "pos-" + axis,
                        FontSize = "26px",
                        Height = "30px",
                        MaxValue = range,
                        MinValue = -range,
                        OnChange = v => c.Act("set_pos", (axis, v)),
                        Step = 1,
                        StepPixelSize = 10,
                        Value = value,
                        Width = "90px",
                    }));
        return new UiNode(UiKind.Section) { Fill = true, Title = "Target", TextAlign = "center" }.With(
            Axis("x", data.Num("x"), "arrows-alt-h"),
            Axis("y", data.Num("y"), "arrows-alt-v"));
    }

    // tg: LaunchpadConsole.tsx:230
    private static UiNode DeliveryButtons(TguiContext c) =>
        new UiNode(UiKind.Section) { Fill = true }.With(
            new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Button) { Key = "launch", Fluid = true, Icon = "upload", TextAlign = "center", Text = "Launch", OnClick = () => c.Act("launch") }),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Button) { Key = "pull", Fluid = true, Icon = "download", TextAlign = "center", Text = "Pull", OnClick = () => c.Act("pull") })));
}
