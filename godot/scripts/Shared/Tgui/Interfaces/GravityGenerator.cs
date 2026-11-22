using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/GravityGenerator.jsx (engineering package E4). The window
// reads tg's ui_data of the main gravity generator (gravitygenerator.dm:287) and sends its one act, gentoggle (the breaker).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class GravityGenerator
{
    // tg: tgui/packages/tgui/interfaces/GravityGenerator.jsx:12
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool operational = data.Truthy("operational");

        // tg: GravityGenerator.jsx:16  <Window width={400} height={155}>
        return c.Window(400, 155, null,
            // tg: GravityGenerator.jsx:18
            !operational ? new UiNode(UiKind.NoticeBox) { Text = "No data available" } : null,
            operational ? Content(c, data) : null);
    }

    // tg: GravityGenerator.jsx:24  GravityGeneratorContent
    private static UiNode Content(TguiContext c, UiValue data)
    {
        bool breaker = data.Truthy("breaker");
        double chargeCount = data.Num("charge_count");
        double chargingState = data.Num("charging_state");
        bool on = data.Truthy("on");

        return new UiNode(UiKind.Section).With(
            new UiNode(UiKind.LabeledList).With(
                // tg: GravityGenerator.jsx:29
                new UiNode(UiKind.LabeledItem) { Label = "Power" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "power",
                        Icon = breaker ? "power-off" : "times",
                        Text = breaker ? "On" : "Off",
                        Selected = breaker,
                        Disabled = !data.Truthy("operational"),
                        OnClick = () => c.Act("gentoggle"),
                    }),
                // tg: GravityGenerator.jsx:38
                new UiNode(UiKind.LabeledItem) { Label = "Gravity Charge" }.With(
                    new UiNode(UiKind.ProgressBar)
                    {
                        Key = "charge",
                        Value = chargeCount / 100,
                        Ranges = new[]
                        {
                            ("good", 0.7, double.PositiveInfinity),
                            ("average", 0.3, 0.7),
                            ("bad", double.NegativeInfinity, 0.3),
                        },
                    }),
                // tg: GravityGenerator.jsx:49
                new UiNode(UiKind.LabeledItem) { Label = "Charge Mode" }.With(
                    chargingState == 0
                        ? (on
                            ? new UiNode(UiKind.Box) { Key = "mode", Color = "good", Text = "Fully Charged" }
                            : new UiNode(UiKind.Box) { Key = "mode", Color = "bad", Text = "Not Charging" })
                        : chargingState == 1
                            ? new UiNode(UiKind.Box) { Key = "mode", Color = "average", Text = "Charging" }
                            : chargingState == 2
                                ? new UiNode(UiKind.Box) { Key = "mode", Color = "average", Text = "Discharging" }
                                : null)));
    }
}
