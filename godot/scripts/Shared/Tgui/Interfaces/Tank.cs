using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Tank.jsx (package E3, docs/ai/specs/engineering.md: the gas
// tank's window, opened by the secondary use of the tank in hand). Original copyright and license: tgstation, AGPL-3.0 (code). See
// ../tgstation/LICENSE.
//
// tg's three regulator buttons read data.ReleasePressure (a capital R that the data does not have), so the comparisons are always false
// and the buttons are never disabled: kept as it runs in tg. tg's formatPressure is the Canister window's (the same function).

public static class Tank
{
    // tg: tgui/packages/tgui/interfaces/Tank.jsx:17
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double minReleasePressure = data.Num("minReleasePressure");
        double maxReleasePressure = data.Num("maxReleasePressure");
        double leakPressure = data.Num("leakPressure");
        double fragmentPressure = data.Num("fragmentPressure");
        double tankPressure = data.Num("tankPressure");
        double releasePressure = data.Num("releasePressure");

        // tg: Tank.jsx:30  <Window width={275} height={120}>
        return c.Window(275, 120, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.LabeledControls).With(
                    // tg: Tank.jsx:34
                    new UiNode(UiKind.LabeledControl) { Label = "Pressure" }.With(
                        new UiNode(UiKind.RoundGauge)
                        {
                            Key = "tankPressure",
                            Size = 2,
                            Value = tankPressure,
                            MinValue = 0,
                            MaxValue = fragmentPressure * 1.15,
                            AlertAfter = leakPressure,
                            Ranges = new[]
                            {
                                ("good", 0.0, leakPressure),
                                ("average", leakPressure, fragmentPressure),
                                ("bad", fragmentPressure, fragmentPressure * 1.15),
                            },
                            Format = Canister.FormatPressure,
                        }),
                    // tg: Tank.jsx:51
                    new UiNode(UiKind.LabeledControl) { Label = "Pressure Regulator" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "min",
                            Icon = "fast-backward",
                            Disabled = false, // tg: data.ReleasePressure === data.minReleasePressure (undefined === number)
                            OnClick = () => c.Act("pressure", ("pressure", "min")),
                        },
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "releasePressure",
                            Value = releasePressure,
                            Width = "65px",
                            Unit = "kPa",
                            Step = 1,
                            MinValue = minReleasePressure,
                            MaxValue = maxReleasePressure,
                            OnChange = value => c.Act("pressure", ("pressure", value)),
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "max",
                            Icon = "fast-forward",
                            Disabled = false, // tg: data.ReleasePressure === data.maxReleasePressure
                            OnClick = () => c.Act("pressure", ("pressure", "max")),
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "reset",
                            Icon = "undo",
                            Disabled = false, // tg: data.ReleasePressure === data.defaultReleasePressure
                            OnClick = () => c.Act("pressure", ("pressure", "reset")),
                        }))));
    }
}
