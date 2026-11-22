using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Canister.tsx (M5 slice 3, D148).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Canister
{
    // tg: tgui/packages/tgui/interfaces/Canister.tsx:20
    public static string FormatPressure(double value)
    {
        if (value < 10000)
            return TguiFormat.ToFixed(value) + " kPa";
        return TguiFormat.FormatSiUnit(value * 1000, 1, "Pa");
    }

    // tg: tgui/packages/tgui/interfaces/Canister.tsx:50
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool shielding = data.Truthy("shielding");
        UiValue holdingTank = data.Field("holdingTank");
        bool hasHoldingTank = holdingTank != null && holdingTank.Truthy;
        double pressureLimit = data.Num("pressureLimit");
        bool valveOpen = data.Truthy("valveOpen");
        double tankPressure = data.Num("tankPressure");
        double releasePressure = data.Num("releasePressure");
        double defaultReleasePressure = data.Num("defaultReleasePressure");
        double minReleasePressure = data.Num("minReleasePressure");
        double maxReleasePressure = data.Num("maxReleasePressure");
        bool portConnected = data.Truthy("portConnected");
        double cellCharge = data.Num("cellCharge");
        bool hasHypernobCrystal = data.Truthy("hasHypernobCrystal");
        bool reactionSuppressionEnabled = data.Truthy("reactionSuppressionEnabled");
        double holdingTankFragPressure = data.Num("holdingTankFragPressure");
        double holdingTankLeakPressure = data.Num("holdingTankLeakPressure");

        // tg: Canister.tsx:71  <Window width={350} height={335}>
        return c.Window(350, 335, null,
            new UiNode(UiKind.Stack) { Vertical = true, Height = "100%" }.With(
                new UiNode(UiKind.StackItem) { Key = "top" }.With(
                    // tg: Canister.tsx:75
                    new UiNode(UiKind.Section)
                    {
                        Title = "Canister",
                        Buttons = new UiNode(UiKind.Fragment).With(
                            new UiNode(UiKind.Button)
                            {
                                Icon = shielding ? "power-off" : "times",
                                Text = shielding ? "Shielding-ON" : "Shielding-OFF",
                                Selected = shielding,
                                OnClick = () => c.Act("shielding"),
                            },
                            new UiNode(UiKind.Button) { Icon = "pencil-alt", Text = "Relabel", OnClick = () => c.Act("relabel") },
                            new UiNode(UiKind.Button) { Icon = "palette", OnClick = () => c.Act("recolor") }),
                    }.With(
                        new UiNode(UiKind.LabeledControls).With(
                            // tg: Canister.tsx:95
                            new UiNode(UiKind.LabeledControl) { Label = "Pressure", MinWidth = "66px" }.With(
                                new UiNode(UiKind.RoundGauge)
                                {
                                    Key = "tankPressure",
                                    Size = 1.75,
                                    Value = tankPressure,
                                    MinValue = 0,
                                    MaxValue = pressureLimit,
                                    AlertAfter = pressureLimit * 0.7,
                                    Ranges = new[]
                                    {
                                        ("good", 0.0, pressureLimit * 0.7),
                                        ("average", pressureLimit * 0.7, pressureLimit * 0.85),
                                        ("bad", pressureLimit * 0.85, pressureLimit),
                                    },
                                    Format = FormatPressure,
                                }),
                            // tg: Canister.tsx:110
                            new UiNode(UiKind.LabeledControl) { Label = "Regulator" }.With(
                                new UiNode(UiKind.Box).With(
                                    new UiNode(UiKind.Knob)
                                    {
                                        Key = "releasePressure",
                                        Size = 1.25,
                                        Color = valveOpen ? "yellow" : null,
                                        Value = releasePressure,
                                        Unit = "kPa",
                                        MinValue = minReleasePressure,
                                        MaxValue = maxReleasePressure,
                                        Step = 5,
                                        StepPixelSize = 1,
                                        OnChange = value => c.Act("pressure", ("pressure", value)),
                                    },
                                    new UiNode(UiKind.Button)
                                    {
                                        Key = "max",
                                        Color = "transparent",
                                        Icon = "fast-forward",
                                        OnClick = () => c.Act("pressure", ("pressure", maxReleasePressure)),
                                    },
                                    new UiNode(UiKind.Button)
                                    {
                                        Key = "default",
                                        Color = "transparent",
                                        Icon = "undo",
                                        OnClick = () => c.Act("pressure", ("pressure", defaultReleasePressure)),
                                    })),
                            // tg: Canister.tsx:155
                            new UiNode(UiKind.LabeledControl) { Label = "Valve" }.With(
                                new UiNode(UiKind.Button)
                                {
                                    Key = "valve",
                                    Width = "50px",
                                    FontSize = "11px",
                                    Height = 2,
                                    TextAlign = "center",
                                    Color = valveOpen ? (hasHoldingTank ? "caution" : "danger") : null,
                                    Text = valveOpen ? "Open" : "Closed",
                                    OnClick = () => c.Act("valve"),
                                }),
                            // tg: Canister.tsx:168
                            new UiNode(UiKind.LabeledControl) { Label = "Port" }.With(
                                new UiNode(UiKind.Icon)
                                {
                                    Size = 1.25,
                                    Icon = portConnected ? "plug" : "times",
                                    Color = portConnected ? "good" : "bad",
                                    Tooltip = portConnected ? "Connected" : "Disconnected",
                                }))),
                    // tg: Canister.tsx:184
                    new UiNode(UiKind.Section).With(
                        new UiNode(UiKind.LabeledList).With(
                            new UiNode(UiKind.LabeledItem) { Label = "Cell Charge" }.With(
                                new UiNode(UiKind.Box) { Text = cellCharge > 0 ? TguiFormat.JsNumber(cellCharge) + "%" : "Missing Cell" }),
                            hasHypernobCrystal
                                ? new UiNode(UiKind.LabeledItem) { Label = "Reaction Suppression" }.With(
                                    new UiNode(UiKind.Button)
                                    {
                                        Icon = reactionSuppressionEnabled ? "snowflake" : "times",
                                        Text = reactionSuppressionEnabled ? "Enabled" : "Disabled",
                                        Selected = reactionSuppressionEnabled,
                                        OnClick = () => c.Act("reaction_suppression"),
                                    })
                                : null))),
                // tg: Canister.tsx:204
                new UiNode(UiKind.StackItem) { Key = "tank", Grow = 1 }.With(
                    new UiNode(UiKind.Section)
                    {
                        Fill = true,
                        Title = "Holding Tank",
                        Buttons = hasHoldingTank
                            ? new UiNode(UiKind.Button)
                            {
                                Icon = "eject",
                                Color = valveOpen ? "danger" : null,
                                Text = "Eject",
                                OnClick = () => c.Act("eject"),
                            }
                            : null,
                    }.With(
                        hasHoldingTank
                            ? new UiNode(UiKind.LabeledList).With(
                                new UiNode(UiKind.LabeledItem) { Label = "Label" }.With(
                                    new UiNode(UiKind.Box) { Text = holdingTank.Str("name") }),
                                new UiNode(UiKind.LabeledItem) { Label = "Pressure" }.With(
                                    new UiNode(UiKind.RoundGauge)
                                    {
                                        Key = "holdingTankPressure",
                                        Value = holdingTank.Num("tankPressure"),
                                        MinValue = 0,
                                        MaxValue = holdingTankFragPressure * 1.15,
                                        AlertAfter = holdingTankLeakPressure,
                                        Ranges = new[]
                                        {
                                            ("good", 0.0, holdingTankLeakPressure),
                                            ("average", holdingTankLeakPressure, holdingTankFragPressure),
                                            ("bad", holdingTankFragPressure, holdingTankFragPressure * 1.15),
                                        },
                                        Format = FormatPressure,
                                        Size = 1.75,
                                    }))
                            : null,
                        !hasHoldingTank ? new UiNode(UiKind.Box) { Color = "average", Text = "No Holding Tank" } : null))));
    }
}
