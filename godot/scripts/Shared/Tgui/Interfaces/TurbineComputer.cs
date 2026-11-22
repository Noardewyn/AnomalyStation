using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/TurbineComputer.tsx (engineering package E11). The
// window reads tg's ui_data of the gas turbine control computer (turbine_computer.dm:57) and sends its acts, toggle_power and regulate.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class TurbineComputer
{
    // tg: tgui/packages/tgui/interfaces/TurbineComputer.tsx:118
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        // tg: TurbineComputer.tsx:122  <Window width={310} height={240}>
        return c.Window(310, 240, null, data.Truthy("connected") ? TurbineDisplay(c, data) : OutOfService());
    }

    // tg: TurbineComputer.tsx:29
    private static UiNode TurbineDisplay(TguiContext c, UiValue data)
    {
        bool active = data.Truthy("active");
        double rpm = data.Num("rpm");
        return new UiNode(UiKind.Section)
        {
            Title = "Status",
            // tg: TurbineComputer.tsx:35
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "toggle_power",
                Icon = active ? "power-off" : "times",
                Text = active ? "Online" : "Offline",
                Selected = active,
                Disabled = rpm >= 1000,
                OnClick = () => c.Act("toggle_power"),
            },
        }.With(
            new UiNode(UiKind.LabeledList).With(
                // tg: TurbineComputer.tsx:46
                new UiNode(UiKind.LabeledItem) { Label = "Intake Regulator" }.With(
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "regulator",
                        Value = data.Num("regulator") * 100,
                        Unit = "%",
                        Step = 1,
                        MinValue = 1,
                        MaxValue = 100,
                        OnChange = value => c.Act("regulate", ("regulate", value * 0.01)),
                    }),
                // tg: TurbineComputer.tsx:61
                new UiNode(UiKind.LabeledItem) { Label = "Turbine Integrity" }.With(
                    new UiNode(UiKind.ProgressBar)
                    {
                        Key = "integrity",
                        Value = data.Num("integrity"),
                        MinValue = 0,
                        MaxValue = 100,
                        Ranges = new[] { ("good", 60.0, 100.0), ("average", 40.0, 59.0), ("bad", 0.0, 39.0) },
                    }),
                // tg: TurbineComputer.tsx:74
                new UiNode(UiKind.LabeledItem) { Key = "rpm", Label = "Turbine Speed", Text = TguiFormat.JsNumber(rpm) + " RPM" },
                new UiNode(UiKind.LabeledItem) { Key = "max_rpm", Label = "Max Turbine Speed", Text = TguiFormat.JsNumber(data.Num("max_rpm")) + " RPM" },
                new UiNode(UiKind.LabeledItem) { Key = "temp", Label = "Input Temperature", Text = TguiFormat.JsNumber(data.Num("temp")) + " K" },
                new UiNode(UiKind.LabeledItem) { Key = "max_temperature", Label = "Max Temperature", Text = TguiFormat.JsNumber(data.Num("max_temperature")) + " K" },
                new UiNode(UiKind.LabeledItem) { Key = "power", Label = "Generated Power", Text = TguiFormat.FormatPower(data.Num("power")) }));
    }

    // tg: TurbineComputer.tsx:100
    private static UiNode OutOfService() => new UiNode(UiKind.Modal) { Key = "out_of_service" }.With(
        new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { TextAlign = "center" }.With(
                new UiNode(UiKind.Box)
                {
                    TextAlign = "center",
                    Width = "300px",
                    Wrap = true,
                    Text = "Parts not connected, close all mantainence panels/use a multitool on the rotor before trying again",
                })));
}
