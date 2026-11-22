using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PortableGenerator.jsx (M7 slice 2, D162).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class PortableGenerator
{
    // tg: tgui/packages/tgui/interfaces/PortableGenerator.jsx:14
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double stackPercent = data.Num("stack_percent");
        bool active = data.Truthy("active");
        double sheets = data.Num("sheets");
        double currentHeat = data.Num("current_heat");
        bool connected = data.Truthy("connected");
        string stackPercentState = stackPercent > 50 ? "good" : stackPercent > 15 ? "average" : "bad";

        // tg: PortableGenerator.jsx:22  <Window width={450} height={340}> <Window.Content scrollable>
        return c.Window(450, 340, null,
            // tg: PortableGenerator.jsx:24
            !data.Truthy("anchored") ? new UiNode(UiKind.NoticeBox) { Text = "Generator not anchored." } : null,
            // tg: PortableGenerator.jsx:25
            new UiNode(UiKind.Section) { Title = "Status" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Power switch" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "power",
                            Icon = active ? "power-off" : "times",
                            OnClick = () => c.Act("toggle_power"),
                            Disabled = !data.Truthy("ready_to_boot"),
                            Text = active ? "On" : "Off",
                        }),
                    new UiNode(UiKind.LabeledItem) { Label = data.Str("sheet_name") + " sheets" }.With(
                        new UiNode(UiKind.Box) { Key = "sheets", Color = stackPercentState, Text = TguiFormat.JsNumber(sheets) },
                        sheets >= 1
                            ? new UiNode(UiKind.Button)
                            {
                                Key = "eject",
                                Icon = "eject",
                                Disabled = active,
                                OnClick = () => c.Act("eject"),
                                Text = "Eject",
                            }
                            : null),
                    new UiNode(UiKind.LabeledItem) { Label = "Current sheet level" }.With(
                        new UiNode(UiKind.ProgressBar)
                        {
                            Key = "level",
                            Value = stackPercent / 100,
                            Ranges = new[]
                            {
                                ("good", 0.1, double.PositiveInfinity),
                                ("average", 0.01, 0.1),
                                ("bad", double.NegativeInfinity, 0.01),
                            },
                        }),
                    new UiNode(UiKind.LabeledItem) { Label = "Heat level" }.With(
                        currentHeat < 100
                            ? new UiNode(UiKind.Box) { Key = "heat", Color = "good", Text = "Nominal" }
                            : currentHeat < 200
                                ? new UiNode(UiKind.Box) { Key = "heat", Color = "average", Text = "Caution" }
                                : new UiNode(UiKind.Box) { Key = "heat", Color = "bad", Text = "DANGER" }))),
            // tg: PortableGenerator.jsx:78
            new UiNode(UiKind.Section) { Title = "Output" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Current output" }.With(
                        new UiNode(UiKind.Box) { Key = "output", Text = data.Str("power_output") }),
                    new UiNode(UiKind.LabeledItem) { Label = "Adjust output" }.With(
                        new UiNode(UiKind.Button) { Key = "lower", Icon = "minus", OnClick = () => c.Act("lower_power"), Text = data.Str("power_generated") },
                        new UiNode(UiKind.Button) { Key = "higher", Icon = "plus", OnClick = () => c.Act("higher_power"), Text = data.Str("power_generated") }),
                    new UiNode(UiKind.LabeledItem) { Label = "Power available" }.With(
                        new UiNode(UiKind.Box)
                        {
                            Key = "available",
                            Color = !connected ? "bad" : null,
                            Text = connected ? data.Str("power_available") : "Unconnected",
                        }))));
    }
}
