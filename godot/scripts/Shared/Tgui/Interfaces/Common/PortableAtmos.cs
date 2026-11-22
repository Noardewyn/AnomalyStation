using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces.Common;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/common/PortableAtmos.tsx (package E3, docs/ai/specs/engineering.md:
// the status and holding tank sections of the PortablePump and PortableScrubber windows). Original copyright and license: tgstation,
// AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// tgui-core's AnimatedNumber renders as its text (the number drawn at once, as the other interfaces do). The kit's LabeledItem has no
// colour prop: the Port row's colour is on its text, as the air alarm's rows do. The kit's Height is a minimum, so the holding tank
// section's minHeight="82px" is its Height.

public static class PortableAtmos
{
    // tg: PortableAtmos.tsx:25  PortableBasicInfo (a fragment of two sections)
    public static UiNode PortableBasicInfo(TguiContext c)
    {
        UiValue data = c.Data;
        bool connected = data.Truthy("connected");
        UiValue holding = data.Field("holding");
        bool hasHolding = holding != null && holding.Truthy;
        bool on = data.Truthy("on");
        double pressure = data.Num("pressure");
        bool hasHypernobCrystal = data.Truthy("hasHypernobCrystal");
        bool reactionSuppressionEnabled = data.Truthy("reactionSuppressionEnabled");

        return new UiNode(UiKind.Fragment).With(
            // tg: PortableAtmos.tsx:40
            new UiNode(UiKind.Section)
            {
                Title = "Status",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "power",
                    Icon = on ? "power-off" : "times",
                    Text = on ? "On" : "Off",
                    Selected = on,
                    OnClick = () => c.Act("power"),
                },
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Pressure" }.With(
                        new UiNode(UiKind.Box) { Key = "pressure-readout", Text = TguiFormat.JsNumber(pressure) + " kPa" }),
                    new UiNode(UiKind.LabeledItem) { Label = "Port" }.With(
                        new UiNode(UiKind.Box) { Key = "port", Color = connected ? "good" : "average", Text = connected ? "Connected" : "Not Connected" }),
                    // tg: PortableAtmos.tsx:61
                    hasHypernobCrystal
                        ? new UiNode(UiKind.LabeledItem) { Label = "Reaction Suppression" }.With(
                            new UiNode(UiKind.Button)
                            {
                                Key = "reaction_suppression",
                                Icon = reactionSuppressionEnabled ? "snowflake" : "times",
                                Text = reactionSuppressionEnabled ? "Enabled" : "Disabled",
                                Selected = reactionSuppressionEnabled,
                                OnClick = () => c.Act("reaction_suppression"),
                            })
                        : null)),
            // tg: PortableAtmos.tsx:74
            new UiNode(UiKind.Section)
            {
                Title = "Holding Tank",
                Height = "82px",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "eject",
                    Icon = "eject",
                    Text = "Eject",
                    Disabled = !hasHolding,
                    OnClick = () => c.Act("eject"),
                },
            }.With(
                hasHolding
                    ? new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Label = "Label" }.With(
                            new UiNode(UiKind.Box) { Key = "holding-name", Text = holding.Str("name") }),
                        new UiNode(UiKind.LabeledItem) { Label = "Pressure" }.With(
                            new UiNode(UiKind.Box) { Key = "holding-pressure", Text = TguiFormat.JsNumber(holding.Num("pressure")) + " kPa" }))
                    : new UiNode(UiKind.Box) { Key = "no-holding", Color = "average", Text = "No holding tank" }));
    }
}
