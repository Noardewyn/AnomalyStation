using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/TrainingMachine.tsx (gaps ST4, the AURUMILL
// training machine). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class TrainingMachine
{
    // tg: tgui/packages/tgui/interfaces/TrainingMachine.tsx:21
    public static UiNode Render(TguiContext c)
    {
        // tg: TrainingMachine.tsx:23  <Window width={230} height={150} title="AURUMILL">
        return c.Window(230, 150, "AURUMILL",
            new UiNode(UiKind.Section) { Title = "Training Machine", Fill = true }.With(TrainingControls(c)));
    }

    /** Creates a labeledlist of controls */
    // tg: tgui/packages/tgui/interfaces/TrainingMachine.tsx:35
    private static UiNode TrainingControls(TguiContext c)
    {
        UiValue data = c.Data;
        double movespeed = data.Num("movespeed");
        double range = data.Num("range");
        bool moving = data.Truthy("moving");

        return new UiNode(UiKind.LabeledControls).With(
            new UiNode(UiKind.LabeledControl) { Label = "Speed" }.With(
                new UiNode(UiKind.Knob)
                {
                    Key = "movespeed",
                    Size = 1.2,
                    Step = 0.5,
                    StepPixelSize = 10,
                    Value = movespeed,
                    MinValue = 1,
                    MaxValue = 10,
                    OnChange = value => c.Act("movespeed", ("movespeed", value)),
                }),
            new UiNode(UiKind.LabeledControl) { Label = "Range" }.With(
                new UiNode(UiKind.Knob)
                {
                    Key = "range",
                    Size = 1.2,
                    Step = 1,
                    StepPixelSize = 50,
                    Value = range,
                    MinValue = 1,
                    MaxValue = 7,
                    OnChange = value => c.Act("range", ("range", value)),
                }),
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Divider) { Vertical = true }),
            new UiNode(UiKind.StackItem).With(
                // tg's button holds a bold 1.4em Box with its text; the kit's button draws its own text, so it carries it
                new UiNode(UiKind.Button)
                {
                    Key = "toggle",
                    Fluid = true,
                    Selected = moving,
                    Bold = true,
                    FontSize = "1.4em",
                    TextAlign = "center",
                    Text = moving ? "END" : "BEGIN",
                    OnClick = () => c.Act("toggle"),
                }));
    }
}
