using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SpaceHeater.jsx (engineering package E9). The window reads
// tg's ui_data of the space heater (spaceheater.dm:253) and of the improvised chem heater (:407) and sends tg's acts: power, mode, target,
// eject and ejectBeaker. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// Not drawn: the empty LabeledList.Divider that closes the thermostat list (a spacer row; the kit has no such node).

public static class SpaceHeater
{
    // tg: tgui/packages/tgui/interfaces/SpaceHeater.jsx:13
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool on = data.Truthy("on");
        bool open = data.Truthy("open");
        bool hasPowercell = data.Truthy("hasPowercell");
        string mode = data.Str("mode");
        double powerLevel = data.Num("powerLevel");

        // tg: SpaceHeater.jsx:16  <Window width={400} height={305}>
        return c.Window(400, 305, null,
            // tg: SpaceHeater.jsx:18
            new UiNode(UiKind.Section)
            {
                Title = "Power",
                Buttons = new UiNode(UiKind.Fragment).With(
                    data.Truthy("chemHacked")
                        ? new UiNode(UiKind.Button)
                        {
                            Key = "eject-beaker",
                            Icon = "eject",
                            Text = "Eject beaker",
                            Disabled = !data.Truthy("beaker"),
                            OnClick = () => c.Act("ejectBeaker"),
                        }
                        : null,
                    new UiNode(UiKind.Button)
                    {
                        Key = "eject",
                        Icon = "eject",
                        Text = "Eject Cell",
                        Disabled = !hasPowercell || !open,
                        OnClick = () => c.Act("eject"),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "power",
                        Icon = on ? "power-off" : "times",
                        Text = on ? "On" : "Off",
                        Selected = on,
                        Disabled = !hasPowercell,
                        OnClick = () => c.Act("power"),
                    }),
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: SpaceHeater.jsx:48  <LabeledList.Item label="Cell" color={!data.hasPowercell && 'bad'}> (the colour is the value cell's)
                    new UiNode(UiKind.LabeledItem) { Label = "Cell" }.With(
                        hasPowercell
                            ? new UiNode(UiKind.ProgressBar)
                            {
                                Key = "cell",
                                Value = powerLevel / 100,
                                Text = TguiFormat.JsNumber(powerLevel) + "%",
                                Ranges = new[]
                                {
                                    ("good", 0.6, double.PositiveInfinity),
                                    ("average", 0.3, 0.6),
                                    ("bad", double.NegativeInfinity, 0.3),
                                },
                            }
                            : new UiNode(UiKind.Box) { Key = "cell", Color = "bad", Text = "None" }))),
            // tg: SpaceHeater.jsx:66
            new UiNode(UiKind.Section) { Title = "Thermostat" }.With(
                new UiNode(UiKind.LabeledList).With(
                    CurrentTemperature(data),
                    // tg: SpaceHeater.jsx:84
                    new UiNode(UiKind.LabeledItem) { Label = "Target Temperature" }.With(
                        open
                            ? new UiNode(UiKind.NumberInput)
                            {
                                Key = "target",
                                Value = ParseFloat(data.Field("targetTemp")),
                                Width = "65px",
                                Unit = "°C",
                                Step = 1,
                                MinValue = data.Num("minTemp"),
                                MaxValue = data.Num("maxTemp"),
                                OnChange = value => c.Act("target", ("target", value)),
                            }
                            : new UiNode(UiKind.Box) { Key = "target", Text = data.Str("targetTemp") + "°C" }),
                    // tg: SpaceHeater.jsx:100
                    new UiNode(UiKind.LabeledItem) { Label = "Mode" }.With(
                        !open && !string.IsNullOrEmpty(mode)
                            ? new UiNode(UiKind.Box) { Key = "mode", Text = TguiFormat.Capitalize(mode) }
                            : new UiNode(UiKind.Fragment).With(
                                ModeButton(c, "auto", "Auto", "thermometer-half", mode),
                                ModeButton(c, "heat", "Heat", "fire-alt", mode),
                                ModeButton(c, "cool", "Cool", "fan", mode))))));
    }

    // tg: SpaceHeater.jsx:70  the box's colour follows the gap between the target and the current temperature
    private static UiNode CurrentTemperature(UiValue data)
    {
        // JavaScript: "N/A" minus a number is NaN, and every comparison with NaN is false, so the box reads good
        double gap = Math.Abs(data.Num("targetTemp") - AsJsNumber(data.Field("currentTemp")));
        return new UiNode(UiKind.LabeledItem) { Label = "Current Temperature" }.With(
            new UiNode(UiKind.Box)
            {
                Key = "current",
                FontSize = "18px",
                Color = gap > 50 ? "bad" : gap > 20 ? "average" : "good",
                Text = data.Str("currentTemp") + "°C",
            });
    }

    private static UiNode ModeButton(TguiContext c, string value, string text, string icon, string mode) => new UiNode(UiKind.Button)
    {
        Key = "mode:" + value,
        Icon = icon,
        Text = text,
        Selected = mode == value,
        OnClick = () => c.Act("mode", ("mode", value)),
    };

    private static double AsJsNumber(UiValue v) => v?.Kind == UiValueKind.String
        ? double.TryParse(v.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d) ? d : double.NaN
        : v == null || v.Kind == UiValueKind.Null ? double.NaN : v.AsNum();

    // JavaScript's parseFloat of the target ("20" or 20)
    private static double ParseFloat(UiValue v) => v?.Kind == UiValueKind.Number ? v.Number : AsJsNumber(v);
}
