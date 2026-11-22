using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PipeScrubber.tsx (package E3, docs/ai/specs/engineering.md:
// the pipe scrubber's window). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// The kit has no padding or margin props and no line height: LabeledControls p={1} and the power button's my={0.5} and lineHeight={2} are
// not drawn (the button keeps its width and font size). tgui's inline filter buttons flow in rows: the kit's wrapped Stack. React's key
// (filter.id) is undefined in tg's data, so the key here is the gas id. formatPressure is the Canister window's (the same function).

public static class PipeScrubber
{
    // tg: PipeScrubber.tsx:70  a gauge of a pressure against its limit (the buffer's and the tank's differ in the numbers only)
    private static UiNode Gauge(string key, double value, double limit) =>
        new UiNode(UiKind.RoundGauge)
        {
            Key = key,
            Size = 1.75,
            Value = value,
            MinValue = 0,
            MaxValue = limit,
            AlertAfter = limit * 0.7,
            Ranges = new[]
            {
                ("good", 0.0, limit * 0.7),
                ("average", limit * 0.7, limit * 0.85),
                ("bad", limit * 0.85, limit),
            },
            Format = Canister.FormatPressure,
        };

    // tg: tgui/packages/tgui/interfaces/PipeScrubber.tsx:50
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool on = data.Truthy("on");
        bool connected = data.Truthy("connected");
        bool direction = data.Truthy("direction");
        double pressureTank = data.Num("pressureTank");
        double pressureLimitTank = data.Num("pressureLimitTank");
        double pressurePump = data.Num("pressurePump");
        double pressureLimitPump = data.Num("pressureLimitPump");
        bool hasHypernobCrystal = data.Truthy("hasHypernobCrystal");
        bool reactionSuppressionEnabled = data.Truthy("reactionSuppressionEnabled");

        // tg: PipeScrubber.tsx:68  <Window width={400} height={350}>
        return c.Window(400, 350, null,
            // tg: PipeScrubber.tsx:70
            new UiNode(UiKind.Section)
            {
                Title = "Status",
                Buttons = hasHypernobCrystal
                    ? new UiNode(UiKind.Button)
                    {
                        Key = "reaction_suppression",
                        Icon = reactionSuppressionEnabled ? "snowflake" : "times",
                        Text = reactionSuppressionEnabled ? "Reaction Suppression Enabled" : "Reaction Suppression Disabled",
                        Selected = reactionSuppressionEnabled,
                        OnClick = () => c.Act("reaction_suppression"),
                    }
                    : null,
            }.With(
                new UiNode(UiKind.LabeledControls).With(
                    // tg: PipeScrubber.tsx:89
                    new UiNode(UiKind.LabeledControl) { Label = "Buffer Port" }.With(
                        new UiNode(UiKind.Box).With(
                            new UiNode(UiKind.Icon)
                            {
                                Key = "port",
                                Size = 2,
                                Icon = connected ? "plug-circle-check" : "plug-circle-xmark",
                                Color = connected ? "good" : "bad",
                                Tooltip = connected ? "Connected" : "Disconnected",
                            })),
                    // tg: PipeScrubber.tsx:103
                    new UiNode(UiKind.LabeledControl) { Label = "Buffer" }.With(Gauge("pressurePump", pressurePump, pressureLimitPump)),
                    // tg: PipeScrubber.tsx:118
                    new UiNode(UiKind.LabeledControl) { Label = "Tank" }.With(Gauge("pressureTank", pressureTank, pressureLimitTank)),
                    // tg: PipeScrubber.tsx:133
                    new UiNode(UiKind.LabeledControl) { Label = "Pump" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "power",
                            Width = 6,
                            FontSize = "18px",
                            Icon = "power-off",
                            Selected = on,
                            Text = on ? "On" : "Off",
                            OnClick = () => c.Act("power"),
                        }))),
            // tg: PipeScrubber.tsx:150
            new UiNode(UiKind.Section)
            {
                Title = "Direction",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "direction",
                    Text = direction ? "Buffer → Tank" : "Tank → Buffer",
                    OnClick = () => c.Act("direction"),
                },
            }.With(
                // tg: PipeScrubber.tsx:156
                direction
                    ? new UiNode(UiKind.Fragment).With(
                        new UiNode(UiKind.Box) { Text = "Filtering gases from the buffer into the internal tank." },
                        new UiNode(UiKind.Section).With(
                            new UiNode(UiKind.Stack) { Wrap = true }.With(
                                data.Arr("filterTypes").Select(filter =>
                                {
                                    bool enabled = filter.Truthy("enabled");
                                    string gasId = filter.Str("gasId");
                                    return new UiNode(UiKind.Button)
                                    {
                                        Key = "filter:" + gasId,
                                        Icon = enabled ? "check-square-o" : "square-o",
                                        Text = TguiFormat.GetGasLabel(gasId, filter.Str("gasName")),
                                        Selected = enabled,
                                        OnClick = () => c.Act("toggle_filter", ("val", gasId)),
                                    };
                                }))))
                    : null,
                // tg: PipeScrubber.tsx:174
                !direction ? new UiNode(UiKind.Box) { Text = "Dumping internal tank gases into the buffer." } : null));
    }
}
