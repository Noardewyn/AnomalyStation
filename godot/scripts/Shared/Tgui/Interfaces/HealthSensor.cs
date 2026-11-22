using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/HealthSensor.tsx (gaps AS1): the health sensor assembly
// (health.dm:93). tgui-core's AnimatedNumber renders as its text.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class HealthSensor
{
    // tg: HealthSensor.tsx:23  ranges={{ good: [0.5, Infinity], average: [0.2, 0.5], bad: [-Infinity, 0.2] }}
    private static readonly (string, double, double)[] HealthRanges =
    {
        ("good", 0.5, double.PositiveInfinity),
        ("average", 0.2, 0.5),
        ("bad", double.NegativeInfinity, 0.2),
    };

    // tg: HealthSensor.tsx:18
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue health = data.Get("health");
        bool scanning = data.Truthy("scanning");
        bool target = data.Truthy("target");

        // tg: HealthSensor.tsx:23  <Window width={360} height={115}>
        return c.Window(360, 115, null,
            new UiNode(UiKind.Section)
            {
                Title = "Health Sensor",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button) { Key = "scanning", Icon = scanning ? "power-off" : "times", Text = scanning ? "On" : "Off", Selected = scanning, OnClick = () => c.Act("scanning") },
                    new UiNode(UiKind.Button) { Key = "target", Icon = target ? "skull" : "heartbeat", Color = "red", Text = target ? "Checking for Death" : "Checking for Crit", OnClick = () => c.Act("target") }),
            }.With(
                // tg: HealthSensor.tsx:44  {health !== undefined && (<ProgressBar ...>)}: a null health (never scanned) is defined
                health != null
                    ? new UiNode(UiKind.ProgressBar)
                    {
                        Key = "health",
                        Value = scanning ? data.Num("health") / 100 : 0,
                        Ranges = HealthRanges,
                        Text = scanning ? TguiFormat.JsNumber(data.Num("health")) : "Off",
                    }
                    : null));
    }
}
