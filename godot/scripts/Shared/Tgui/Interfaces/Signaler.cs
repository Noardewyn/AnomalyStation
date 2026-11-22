using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Signaler.tsx (M9 slice D). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Signaler
{
    // tg: tgui/packages/tgui/interfaces/Signaler.tsx:15
    public static UiNode Render(TguiContext c) => c.Window(280, 128, null, Content(c));

    // tg: tgui/packages/tgui/interfaces/Signaler.tsx:26  SignalerContent
    public static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        double code = data.Num("code");
        double frequency = data.Num("frequency");
        double cooldown = data.Num("cooldown");
        double minFrequency = data.Num("minFrequency");
        double maxFrequency = data.Num("maxFrequency");
        return new UiNode(UiKind.Section).With(
            new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Color = "label", Text = "Frequency:" },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.NumberInput)
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
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "resetfreq", Icon = "sync", Text = "Reset", OnClick = () => c.Act("reset", ("reset", "freq")) })),
            new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Color = "label", Text = "Code:" },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.NumberInput)
                {
                    Key = "code",
                    Step = 1,
                    StepPixelSize = 6,
                    MinValue = 1,
                    MaxValue = 100,
                    Value = code,
                    Width = "80px",
                    OnChange = v => c.Act("code", ("code", v)),
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "resetcode", Icon = "sync", Text = "Reset", OnClick = () => c.Act("reset", ("reset", "code")) })),
            new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "signal",
                    Fluid = true,
                    Tooltip = cooldown != 0 ? "Cooldown: " + TguiFormat.JsNumber(cooldown * 0.1) + " seconds" : null,
                    Icon = "arrow-up",
                    Text = "Send Signal",
                    TextAlign = "center",
                    OnClick = () => c.Act("signal"),
                })));
    }
}
