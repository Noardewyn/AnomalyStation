using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ModularShieldGen.tsx (engineering package E10). The
// window reads tg's ui_data of the modular shield generator (modular_shield.dm:273) and sends its three acts: set_radius,
// toggle_exterior, toggle_shields. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ModularShieldGen
{
    // tg: tgui/packages/tgui/interfaces/ModularShieldGen.tsx:28
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double maxStrength = data.Num("max_strength");
        double maxRegeneration = data.Num("max_regeneration");
        double currentRegeneration = data.Num("current_regeneration");
        double maxRadius = data.Num("max_radius");
        double currentRadius = data.Num("current_radius");
        double currentStrength = data.Num("current_strength");
        bool active = data.Truthy("active");
        bool exteriorOnly = data.Truthy("exterior_only");
        bool recovering = data.Truthy("recovering");
        bool initiatingField = data.Truthy("initiating_field");

        // tg: ModularShieldGen.tsx:44  <Window title="Modular Shield Generator" width={690} height={225}>
        return c.Window(690, 225, "Modular Shield Generator",
            new UiNode(UiKind.Stack) { Fill = true }.With(
                // tg: ModularShieldGen.tsx:47
                new UiNode(UiKind.StackItem) { Grow = 2 }.With(
                    new UiNode(UiKind.Section) { Title = "Shield Strength", Color = recovering ? "red" : "white" }.With(
                        Bar("strength", currentStrength, maxStrength, $"{TguiFormat.JsNumber(currentStrength)}/{TguiFormat.JsNumber(maxStrength)}")),
                    // tg: ModularShieldGen.tsx:64
                    new UiNode(UiKind.Section) { Title = "Regeneration and Radius" }.With(
                        Bar("regeneration", currentRegeneration, maxRegeneration,
                            $"Regeneration {TguiFormat.JsNumber(currentRegeneration)}/{TguiFormat.JsNumber(maxRegeneration)}"),
                        new UiNode(UiKind.Section).With(
                            Bar("radius", currentRadius, maxRadius, $"Radius {TguiFormat.JsNumber(currentRadius)}/{TguiFormat.JsNumber(maxRadius)}")))),
                // tg: ModularShieldGen.tsx:91
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Title = "Settings" }.With(
                        new UiNode(UiKind.LabeledList).With(
                            new UiNode(UiKind.LabeledItem) { Label = "Set Radius" }.With(
                                new UiNode(UiKind.NumberInput)
                                {
                                    Key = "set-radius",
                                    Disabled = active,
                                    Fluid = true,
                                    Step = 1,
                                    Value = currentRadius,
                                    MinValue = 3,
                                    MaxValue = maxRadius,
                                    CommitOnly = true,
                                    OnChange = value => c.Act("set_radius", ("new_radius", value)),
                                }),
                            // tg: ModularShieldGen.tsx:107
                            new UiNode(UiKind.LabeledItem) { Label = "Limitations" }.With(
                                new UiNode(UiKind.Button)
                                {
                                    Key = "exterior",
                                    Disabled = active,
                                    Text = exteriorOnly ? "External only" : "Internal & External",
                                    OnClick = () => c.Act("toggle_exterior"),
                                }))),
                    // tg: ModularShieldGen.tsx:117
                    new UiNode(UiKind.Section).With(
                        new UiNode(UiKind.LabeledList).With(
                            new UiNode(UiKind.LabeledItem) { Label = "Toggle Power" }.With(
                                new UiNode(UiKind.Button)
                                {
                                    Key = "power",
                                    Bold = true,
                                    Disabled = recovering || initiatingField,
                                    Selected = active,
                                    Text = active ? "On" : "Off",
                                    Icon = "power-off",
                                    OnClick = () => c.Act("toggle_shields"),
                                }))))));
    }

    // tg: ModularShieldGen.tsx:53  <ProgressBar value maxValue ranges={good: [max * 0.75, max], average: [max * 0.25, max * 0.75], bad: [0, max * 0.25]}>
    internal static UiNode Bar(string key, double value, double max, string text) => new UiNode(UiKind.ProgressBar)
    {
        Key = key,
        Value = value,
        MinValue = 0,
        MaxValue = max,
        Ranges = new[]
        {
            ("good", max * 0.75, max),
            ("average", max * 0.25, max * 0.75),
            ("bad", 0.0, max * 0.25),
        },
        Text = text,
    };
}
