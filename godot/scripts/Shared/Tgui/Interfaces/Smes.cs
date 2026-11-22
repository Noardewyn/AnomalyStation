using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Smes.tsx (M7 slice 2, D162).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Smes
{
    // tg: tgui/packages/tgui/interfaces/Smes.tsx:28  Common power multiplier
    private const double PowerMul = 1e3;

    // tg: tgui/packages/tgui/interfaces/Smes.tsx:30
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double capacity = data.Num("capacity");
        double charge = data.Num("charge");
        bool inputAttempt = data.Truthy("inputAttempt");
        bool inputting = data.Truthy("inputting");
        double inputLevel = data.Num("inputLevel");
        double inputLevelMax = data.Num("inputLevelMax");
        double inputAvailable = data.Num("inputAvailable");
        bool outputAttempt = data.Truthy("outputAttempt");
        bool outputting = data.Truthy("outputting");
        double outputLevel = data.Num("outputLevel");
        double outputLevelMax = data.Num("outputLevelMax");
        double outputUsed = data.Num("outputUsed");

        double capacityPercent = TguiFormat.Round(100 * (charge / capacity), 0.1);
        string inputState = capacityPercent >= 100 ? "good" : inputting ? "average" : "bad";
        string outputState = outputting ? "good" : charge > 0 ? "average" : "bad";

        // tg: Smes.tsx:57  <Window width={340} height={350}>
        return c.Window(340, 350, null,
            // tg: Smes.tsx:59
            new UiNode(UiKind.Section) { Title = "Stored Energy" }.With(
                new UiNode(UiKind.ProgressBar)
                {
                    Value = capacityPercent * 0.01,
                    Ranges = new[]
                    {
                        ("good", 0.5, double.PositiveInfinity),
                        ("average", 0.15, 0.5),
                        ("bad", double.NegativeInfinity, 0.15),
                    },
                }),
            // tg: Smes.tsx:70
            new UiNode(UiKind.Section)
            {
                Title = "Input",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "tryinput",
                    Icon = inputAttempt ? "sync-alt" : "times",
                    Selected = inputAttempt,
                    Text = inputAttempt ? "Auto" : "Off",
                    OnClick = () => c.Act("tryinput"),
                },
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Charge Mode" }.With(
                        new UiNode(UiKind.Box)
                        {
                            Key = "chargeMode",
                            Color = inputState,
                            Text = capacityPercent >= 100 ? "Fully Charged" : inputting ? "Charging" : "Not Charging",
                        }),
                    new UiNode(UiKind.LabeledItem) { Label = "Target Input" }.With(
                        Level(c, "input", inputLevel, inputLevelMax, inputAvailable)),
                    new UiNode(UiKind.LabeledItem) { Label = "Available" }.With(
                        new UiNode(UiKind.Box) { Key = "available", Text = TguiFormat.FormatPower(inputAvailable) }))),
            // tg: Smes.tsx:134
            new UiNode(UiKind.Section)
            {
                Title = "Output",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "tryoutput",
                    Icon = outputAttempt ? "power-off" : "times",
                    Selected = outputAttempt,
                    Text = outputAttempt ? "On" : "Off",
                    OnClick = () => c.Act("tryoutput"),
                },
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Output Mode" }.With(
                        new UiNode(UiKind.Box)
                        {
                            Key = "outputMode",
                            Color = outputState,
                            Text = outputting ? "Sending" : charge > 0 ? "Not Sending" : "No Charge",
                        }),
                    new UiNode(UiKind.LabeledItem) { Label = "Target Output" }.With(
                        Level(c, "output", outputLevel, outputLevelMax, null)),
                    new UiNode(UiKind.LabeledItem) { Label = "Outputting" }.With(
                        new UiNode(UiKind.Box) { Key = "outputting", Text = TguiFormat.FormatPower(outputUsed) }))));
    }

    // tg: Smes.tsx:85 and :150: the row of the min and back buttons, the slider, the forward and max buttons (one component here for
    // the two identical rows of the input and the output)
    private static UiNode Level(TguiContext c, string action, double level, double levelMax, double? fill)
    {
        return new UiNode(UiKind.Stack) { Key = action + "Level", Fill = true }.With(
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Button)
                {
                    Key = "min",
                    Icon = "fast-backward",
                    Disabled = level == 0,
                    OnClick = () => c.Act(action, ("target", "min")),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "backward",
                    Icon = "backward",
                    Disabled = level == 0,
                    OnClick = () => c.Act(action, ("adjust", -10000)),
                }),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                new UiNode(UiKind.Slider)
                {
                    Key = "slider",
                    Value = level / PowerMul,
                    FillValue = fill / PowerMul,
                    MinValue = 0,
                    MaxValue = levelMax / PowerMul,
                    Step = 5,
                    StepPixelSize = 4,
                    Format = value => TguiFormat.FormatPower(value * PowerMul, 1),
                    OnChange = value => c.Act(action, ("target", value * PowerMul)),
                }),
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Button)
                {
                    Key = "forward",
                    Icon = "forward",
                    Disabled = level == levelMax,
                    OnClick = () => c.Act(action, ("adjust", 10000)),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "max",
                    Icon = "fast-forward",
                    Disabled = level == levelMax,
                    OnClick = () => c.Act(action, ("target", "max")),
                }));
    }
}
