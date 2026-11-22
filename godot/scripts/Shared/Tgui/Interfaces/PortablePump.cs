using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PortablePump.tsx (package E3, docs/ai/specs/engineering.md:
// the portable air pump's window). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class PortablePump
{
    // tg: tgui/packages/tgui/interfaces/PortablePump.tsx:22
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool direction = data.Truthy("direction");
        bool connected = data.Truthy("connected");
        UiValue holding = data.Field("holding");
        bool hasHolding = holding != null && holding.Truthy;
        double targetPressure = data.Num("targetPressure");
        double defaultPressure = data.Num("defaultPressure");
        double minPressure = data.Num("minPressure");
        double maxPressure = data.Num("maxPressure");
        string pump_or_port = connected ? "Port" : "Pump";
        string area_or_tank = hasHolding ? "Tank" : "Area";

        // tg: PortablePump.tsx:36  <Window width={300} height={340}>
        return c.Window(300, 340, null,
            PortableAtmos.PortableBasicInfo(c),
            // tg: PortablePump.tsx:39
            new UiNode(UiKind.Section)
            {
                Title = "Pumping",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "direction",
                    Color = !direction && !hasHolding ? "caution" : null,
                    Text = direction ? area_or_tank + " → " + pump_or_port : pump_or_port + " → " + area_or_tank,
                    OnClick = () => c.Act("direction"),
                },
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: PortablePump.tsx:55
                    new UiNode(UiKind.LabeledItem) { Label = "Output" }.With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "pressure",
                            Value = targetPressure,
                            Unit = "kPa",
                            Width = "75px",
                            MinValue = minPressure,
                            MaxValue = maxPressure,
                            Step = 10,
                            OnChange = value => c.Act("pressure", ("pressure", value)),
                        }),
                    // tg: PortablePump.tsx:70
                    new UiNode(UiKind.LabeledItem) { Label = "Presets" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "min",
                            Icon = "minus",
                            Disabled = targetPressure == minPressure,
                            OnClick = () => c.Act("pressure", ("pressure", "min")),
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "reset",
                            Icon = "sync",
                            Disabled = targetPressure == defaultPressure,
                            OnClick = () => c.Act("pressure", ("pressure", "reset")),
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "max",
                            Icon = "plus",
                            Disabled = targetPressure == maxPressure,
                            OnClick = () => c.Act("pressure", ("pressure", "max")),
                        }))));
    }
}
