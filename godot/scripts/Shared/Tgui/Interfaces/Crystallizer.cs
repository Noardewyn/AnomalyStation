using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Crystallizer.tsx (engineering package E12a: the
// crystallizer's window). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Crystallizer
{
    // tg: Crystallizer.tsx:38
    private static double LogScale(double value) => Math.Log2(16 + Math.Max(0, value)) - 4;

    // tg: Crystallizer.tsx:40
    public static UiNode Render(TguiContext c)
    {
        // <Window width={500} height={600}> <Window.Content scrollable>
        return c.Window(500, 600, null, Controls(c), Requirements(c), Gases(c));
    }

    // tg: Crystallizer.tsx:52
    private static UiNode Controls(TguiContext c)
    {
        UiValue data = c.Data;
        double gas_input = data.Num("gas_input");
        bool on = data.Truthy("on");
        string selected = data.Str("selected");
        IReadOnlyList<UiValue> selected_recipes = data.Arr("selected_recipes");

        return new UiNode(UiKind.Section) { Title = "Controls" }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Power" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "power",
                        Icon = on ? "power-off" : "times",
                        Text = on ? "On" : "Off",
                        Selected = on,
                        OnClick = () => c.Act("power"),
                    }),
                // tg: Crystallizer.tsx:67  one button per recipe, wrapped at the window's width as tgui's inline buttons are
                new UiNode(UiKind.LabeledItem) { Label = "Recipe", Wrap = true }.With(
                    // tg keys them by id, which the blank base recipe shares with Nothing (""): the kit's keys are their places
                    selected_recipes.Select((recipe, index) =>
                    {
                        string id = recipe.Str("id");
                        return new UiNode(UiKind.Button)
                        {
                            Key = "recipe" + index,
                            Selected = id == selected,
                            Text = recipe.Str("name"),
                            OnClick = () => c.Act("recipe", ("mode", id)),
                        };
                    }).ToArray()),
                // tg: Crystallizer.tsx:80
                new UiNode(UiKind.LabeledItem) { Label = "Gas Input" }.With(
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "gas_input",
                        Value = gas_input,
                        Step = 0.1,
                        Width = "63px",
                        Unit = "moles/s",
                        MinValue = 0,
                        MaxValue = 250,
                        OnChange = value => c.Act("gas_input", ("gas_input", value)),
                    })));
    }

    // tg: Crystallizer.tsx:101
    private static UiNode Requirements(TguiContext c)
    {
        UiValue data = c.Data;
        string requirements = data.Str("requirements");
        double internal_temperature = data.Num("internal_temperature");
        double progress_bar = data.Num("progress_bar");

        return new UiNode(UiKind.Section) { Title = "Requirements and progress" }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Progress" }.With(
                    new UiNode(UiKind.ProgressBar)
                    {
                        Key = "progress",
                        Value = progress_bar / 100,
                        Ranges = new[] { ("good", 0.67, 1.0), ("average", 0.34, 0.66), ("bad", 0.0, 0.33) },
                    }),
                // tg: Crystallizer.tsx:117  <Box m={1} preserveWhitespace>: the label keeps the lines (the margin is not drawn)
                new UiNode(UiKind.LabeledItem) { Label = "Recipe" }.With(
                    new UiNode(UiKind.Box) { Key = "requirements", Text = requirements }),
                // tg: Crystallizer.tsx:122
                new UiNode(UiKind.LabeledItem) { Label = "Temperature" }.With(
                    new UiNode(UiKind.ProgressBar)
                    {
                        Key = "temperature",
                        Value = LogScale(internal_temperature),
                        MinValue = 0,
                        MaxValue = LogScale(10000),
                        Ranges = new[]
                        {
                            ("teal", double.NegativeInfinity, LogScale(80)),
                            ("good", LogScale(80), LogScale(600)),
                            ("average", LogScale(600), LogScale(5000)),
                            ("bad", LogScale(5000), double.PositiveInfinity),
                        },
                        Text = $"{TguiFormat.ToFixed(internal_temperature)} K",
                    })));
    }

    // tg: Crystallizer.tsx:144
    private static UiNode Gases(TguiContext c)
    {
        IReadOnlyList<UiValue> internal_gas_data = c.Data.Arr("internal_gas_data");

        return new UiNode(UiKind.Section) { Title = "Gases" }.With(
            new UiNode(UiKind.LabeledList).With(
                internal_gas_data.Select(gas =>
                {
                    string name = gas.Str("name");
                    double amount = gas.Num("amount");
                    return new UiNode(UiKind.LabeledItem) { Key = name, Label = name }.With(
                        new UiNode(UiKind.ProgressBar)
                        {
                            Key = "gas:" + gas.Str("id"),
                            Color = TguiFormat.GetGasColor(gas.Str("id")),
                            Value = amount,
                            MinValue = 0,
                            MaxValue = 1000,
                            Text = $"{TguiFormat.ToFixed(amount, 2)} moles",
                        });
                }).ToArray()));
    }
}
