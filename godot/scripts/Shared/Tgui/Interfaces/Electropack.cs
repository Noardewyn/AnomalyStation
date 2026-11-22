using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Electropack.tsx (gaps J5, D298).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Electropack
{
    // tg: Electropack.tsx:19
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool power = data.Truthy("power");
        double code = data.Num("code");
        double frequency = data.Num("frequency");
        double minFrequency = data.Num("minFrequency");
        double maxFrequency = data.Num("maxFrequency");
        return c.Window(260, 137, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Power" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "power",
                            Icon = power ? "power-off" : "times",
                            Selected = power,
                            Text = power ? "On" : "Off",
                            OnClick = () => c.Act("power"),
                        }),
                    new UiNode(UiKind.LabeledItem)
                    {
                        Label = "Frequency",
                        Buttons = new UiNode(UiKind.Button) { Key = "resetfreq", Icon = "sync", Text = "Reset", OnClick = () => c.Act("reset", ("reset", "freq")) },
                    }.With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "freq",
                            Unit = "kHz",
                            Step = 0.2,
                            StepPixelSize = 6,
                            AllowFloats = true,
                            MinValue = minFrequency / 10,
                            MaxValue = maxFrequency / 10,
                            Value = frequency / 10,
                            Format = v => TguiFormat.ToFixed(v, 1),
                            Width = "80px",
                            OnChange = v => c.Act("freq", ("freq", v)),
                        }),
                    new UiNode(UiKind.LabeledItem)
                    {
                        Label = "Code",
                        Buttons = new UiNode(UiKind.Button) { Key = "resetcode", Icon = "sync", Text = "Reset", OnClick = () => c.Act("reset", ("reset", "code")) },
                    }.With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "code",
                            Step = 1,
                            StepPixelSize = 6,
                            MinValue = 1,
                            MaxValue = 100,
                            Value = code,
                            Width = "80px",
                            OnChange = v => c.Act("code", ("code", v)),
                        }))));
    }
}
