using System;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/RadioactiveMicrolaser.jsx (the syndicate campaign
// G2, D316: the radioactive microlaser). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class RadioactiveMicrolaser
{
    // tg: tgui/packages/tgui/interfaces/RadioactiveMicrolaser.jsx:11
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool irradiate = data.Truthy("irradiate");
        bool stealth = data.Truthy("stealth");
        bool scanmode = data.Truthy("scanmode");
        double intensity = data.Num("intensity");
        double wavelength = data.Num("wavelength");
        bool onCooldown = data.Truthy("on_cooldown");
        string cooldown = data.Str("cooldown");
        c.WindowTheme = "syndicate";

        UiNode Adjust(string action, string icon, double adjust) =>
            new UiNode(UiKind.Button) { Key = action + adjust, Icon = icon, OnClick = () => c.Act(action, ("adjust", adjust)) };

        // tg: RadioactiveMicrolaser.jsx:70-127  the four step buttons around the number field
        UiNode Setting(string label, string action, double value, double min, double max) =>
            new UiNode(UiKind.LabeledItem) { Label = label }.With(
                Adjust(action, "fast-backward", -5),
                Adjust(action, "backward", -1),
                new UiNode(UiKind.Box) { Text = " " },
                new UiNode(UiKind.NumberInput)
                {
                    Key = action + "-value",
                    Value = Math.Round(value),
                    Width = "40px",
                    MinValue = min,
                    MaxValue = max,
                    Step = 1,
                    OnChange = v => c.Act(action, ("target", v)),
                },
                new UiNode(UiKind.Box) { Text = " " },
                Adjust(action, "forward", 1),
                Adjust(action, "fast-forward", 5));

        // tg: RadioactiveMicrolaser.jsx:24  <Window title="Radioactive Microlaser" width={320} height={335} theme="syndicate">
        return c.Window(320, 335, "Radioactive Microlaser",
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Laser Status" }.With(
                        new UiNode(UiKind.Box) { Color = onCooldown ? "average" : "good", Text = onCooldown ? "Recharging" : "Ready" }))),
            new UiNode(UiKind.Section) { Title = "Scanner Controls" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Irradiation" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "irradiate",
                            Icon = irradiate ? "power-off" : "times",
                            Text = irradiate ? "On" : "Off",
                            Selected = irradiate,
                            OnClick = () => c.Act("irradiate"),
                        }),
                    new UiNode(UiKind.LabeledItem) { Label = "Stealth Mode" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "stealth",
                            Icon = stealth ? "eye-slash" : "eye",
                            Text = stealth ? "On" : "Off",
                            Disabled = !irradiate,
                            Selected = stealth,
                            OnClick = () => c.Act("stealth"),
                        }),
                    new UiNode(UiKind.LabeledItem) { Label = "Scan Mode" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "scanmode",
                            Icon = scanmode ? "mortar-pestle" : "heartbeat",
                            Text = scanmode ? "Scan Reagents" : "Scan Health",
                            Disabled = irradiate && stealth,
                            OnClick = () => c.Act("scanmode"),
                        }))),
            new UiNode(UiKind.Section) { Title = "Laser Settings" }.With(
                new UiNode(UiKind.LabeledList).With(
                    Setting("Radiation Intensity", "radintensity", intensity, 1, 20),
                    Setting("Radiation Wavelength", "radwavelength", wavelength, 0, 120),
                    new UiNode(UiKind.LabeledItem) { Label = "Laser Cooldown" }.With(
                        new UiNode(UiKind.Box) { Bold = true, Text = cooldown }))));
    }
}
