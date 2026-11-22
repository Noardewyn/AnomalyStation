using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Reflector.tsx (the M7 supermatter slice, D163:
// MetaStation's engine reflectors). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Reflector
{
    // tg: tgui/packages/tgui/interfaces/Reflector.tsx:19
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string reflectorName = data.Str("reflector_name");
        double rotationAngle = data.Num("rotation_angle");

        // tg: Reflector.tsx:28-118  the presets: three columns of arrows (iconRotation turns the arrow by 45 degrees)
        UiNode Preset(string icon, double iconRotation, double angle) => new UiNode(UiKind.TableRow).With(
            new UiNode(UiKind.Button) { Key = "preset-" + TguiFormat.JsNumber(angle), Icon = icon, Rotation = iconRotation, OnClick = () => c.Act("rotate", ("rotation_angle", angle)) });
        // tg: Reflector.tsx:147-215  the -5/-10/-15 and +5/+10/+15 steps (act 'calculate')
        UiNode Step(double delta) => new UiNode(UiKind.Button)
        {
            Key = "step" + (delta > 0 ? "+" : "") + TguiFormat.JsNumber(delta),
            Fluid = true,
            Icon = delta > 0 ? "redo-alt" : "undo-alt",
            Text = (delta > 0 ? "+" : "") + TguiFormat.JsNumber(delta),
            OnClick = () => c.Act("calculate", ("rotation_angle", delta)),
        };

        // tg: Reflector.tsx:23  <Window title={reflector_name} height={200} width={219}>
        return c.Window(219, 200, reflectorName,
            new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Section) { Title = "Presets", TextAlign = "center", Fill = true }.With(
                        new UiNode(UiKind.Table).With(
                            new UiNode(UiKind.TableCell).With(
                                Preset("arrow-left", 45, 315),
                                Preset("arrow-left", 0, 270),
                                Preset("arrow-left", -45, 225)),
                            new UiNode(UiKind.TableCell).With(
                                Preset("arrow-up", 0, 0),
                                new UiNode(UiKind.TableRow).With(
                                    new UiNode(UiKind.Icon) { Key = "angle", Icon = "angle-double-up", Size = 1.66, Rotation = rotationAngle }),
                                Preset("arrow-down", 0, 180)),
                            new UiNode(UiKind.TableCell).With(
                                Preset("arrow-right", -45, 45),
                                Preset("arrow-right", 0, 90),
                                Preset("arrow-right", 45, 135))))),
                new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.Section) { Title = "Angle", TextAlign = "center", Fill = true }.With(
                        new UiNode(UiKind.LabeledControls).With(
                            new UiNode(UiKind.LabeledControl) { Label = "Set rotation" }.With(
                                new UiNode(UiKind.NumberInput)
                                {
                                    Key = "rotation",
                                    Value = rotationAngle,
                                    Unit = "degrees",
                                    MinValue = 0,
                                    MaxValue = 359,
                                    Step = 1,
                                    StepPixelSize = 1,
                                    OnChange = value => c.Act("rotate", ("rotation_angle", value)),
                                })),
                        new UiNode(UiKind.Stack) { Fill = true }.With(
                            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                                new UiNode(UiKind.StackItem).With(Step(-5)), Step(-10), Step(-15)),
                            new UiNode(UiKind.Stack) { Vertical = true }.With(
                                new UiNode(UiKind.StackItem).With(Step(5)), Step(10), Step(15)))))));
    }
}
