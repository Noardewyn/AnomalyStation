using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SolarControl.tsx (M7 slice G1, D163).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class SolarControl
{
    // tg: tgui/packages/tgui/interfaces/SolarControl.tsx:31
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double supply = data.Num("supply");
        double capacity = data.Num("capacity");
        double azimuthCurrent = data.Num("azimuth_current");
        double azimuthRate = data.Num("azimuth_rate");
        double maxRotationRate = data.Num("max_rotation_rate");
        double trackingState = data.Num("tracking_state");
        double connectedPanels = data.Num("connected_panels");
        bool connectedTracker = data.Truthy("connected_tracker");
        UiValue history = data.Get("history");
        List<double> supplyHistory = history.Arr("supply").Select(v => v.AsNum()).ToList();
        List<double> capacityHistory = history.Arr("capacity").Select(v => v.AsNum()).ToList();
        // tg: SolarControl.tsx:53  const maxValue = Math.max(1, ...history.capacity, ...history.supply)
        double maxValue = Math.Max(1, Math.Max(capacityHistory.Count > 0 ? capacityHistory.Max() : double.NegativeInfinity, supplyHistory.Count > 0 ? supplyHistory.Max() : double.NegativeInfinity));

        // tg: SolarControl.tsx:56  <Window width={330} height={330}>
        return c.Window(330, 330, null,
            // tg: SolarControl.tsx:58
            new UiNode(UiKind.Section) { Title = "Status" }.With(
                // tg: SolarControl.tsx:59  <Box mb={1} position="relative" overflow="visible" height="64px" backgroundColor={'black'}>
                new UiNode(UiKind.Chart) { Key = "chart", Height = "64px" }.With(
                    Line(capacityHistory, maxValue, "rgba(150, 117, 39, 1)", "rgba(150, 117, 39, 0.5)", "capacity"),
                    Line(supplyHistory, maxValue, "rgba(235, 210, 52, 1)", "rgba(235, 210, 52, 0.5)", "supply")),
                // tg: SolarControl.tsx:82
                new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem).With(
                        new UiNode(UiKind.LabeledList).With(
                            new UiNode(UiKind.LabeledItem) { Label = "Power output" }.With(
                                new UiNode(UiKind.ProgressBar)
                                {
                                    Key = "output",
                                    Value = capacity > 0 ? supply / capacity : 0,
                                    MinValue = 0,
                                    MaxValue = 1,
                                    Ranges = new[]
                                    {
                                        ("good", 0.66, double.PositiveInfinity),
                                        ("average", 0.33, 0.66),
                                        ("bad", double.NegativeInfinity, 0.33),
                                    },
                                    Text = capacity > 0
                                        ? $"{TguiFormat.FormatPower(supply)} of {TguiFormat.FormatPower(capacity)} ({TguiFormat.JsNumber(TguiFormat.Round(100 * supply / capacity))}%)"
                                        : TguiFormat.FormatPower(0),
                                }),
                            new UiNode(UiKind.LabeledItem) { Label = "Solar panels" }.With(
                                new UiNode(UiKind.Box) { Key = "panels", Color = connectedPanels > 0 ? "good" : "bad", Text = TguiFormat.JsNumber(connectedPanels) }),
                            new UiNode(UiKind.LabeledItem) { Label = "Solar tracker" }.With(
                                new UiNode(UiKind.Box) { Key = "tracker", Color = connectedTracker ? "good" : "bad", Text = connectedTracker ? "OK" : "N/A" }))))),
            // tg: SolarControl.tsx:114
            new UiNode(UiKind.Section)
            {
                Title = "Controls",
                Buttons = new UiNode(UiKind.Button) { Key = "refresh", Icon = "sync", Text = "Scan for new hardware", OnClick = () => c.Act("refresh") },
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: SolarControl.tsx:125
                    new UiNode(UiKind.LabeledItem) { Label = "Tracking" }.With(
                        new UiNode(UiKind.Button) { Key = "off", Icon = "times", Text = "Off", Selected = trackingState == 0, OnClick = () => c.Act("tracking", ("mode", 0)) },
                        new UiNode(UiKind.Button) { Key = "timed", Icon = "clock-o", Text = "Timed", Selected = trackingState == 1, OnClick = () => c.Act("tracking", ("mode", 1)) },
                        new UiNode(UiKind.Button) { Key = "auto", Icon = "sync", Text = "Auto", Selected = trackingState == 2, Disabled = !connectedTracker, OnClick = () => c.Act("tracking", ("mode", 2)) }),
                    // tg: SolarControl.tsx:147
                    new UiNode(UiKind.LabeledItem) { Label = "Azimuth" }.With(
                        new UiNode(UiKind.Icon) { Key = "arrow", Icon = "arrow-up", Rotation = azimuthCurrent },
                        trackingState == 0 || trackingState == 1
                            ? new UiNode(UiKind.NumberInput)
                            {
                                Key = "azimuth",
                                Width = "52px",
                                Unit = "°",
                                Step = 1,
                                StepPixelSize = 2,
                                MinValue = -360,
                                MaxValue = 720,
                                Value = azimuthCurrent,
                                AllowFloats = true,
                                CommitOnly = true,
                                OnChange = value => c.Act("azimuth", ("value", value)),
                            }
                            : null,
                        trackingState == 1
                            ? new UiNode(UiKind.NumberInput)
                            {
                                Key = "rate",
                                Width = "80px",
                                Unit = "°/m",
                                Step = 0.01,
                                StepPixelSize = 1,
                                MinValue = -maxRotationRate - 0.01,
                                MaxValue = maxRotationRate + 0.01,
                                Value = azimuthRate,
                                AllowFloats = true,
                                CommitOnly = true,
                                // tg: SolarControl.tsx:175  format={(rate) => { const sign = Math.sign(rate) > 0 ? '+' : '-'; return sign + Math.abs(rate); }}
                                Format = rate => (Math.Sign(rate) > 0 ? "+" : "-") + TguiFormat.JsNumber(Math.Abs(rate)),
                                OnChange = value => c.Act("azimuth_rate", ("value", value)),
                            }
                            : null,
                        trackingState == 2
                            ? new UiNode(UiKind.Box) { Key = "auto-azimuth", Color = "label", Text = $"{TguiFormat.JsNumber(azimuthCurrent)} ° (auto)" }
                            : null))));
    }

    // tg: SolarControl.tsx:62  <Chart.Line p={1} fillPositionedParent data={...} rangeX={[0, data.length - 1]} rangeY={[0, maxValue]} strokeColor fillColor />
    private static UiNode Line(List<double> values, double maxValue, string stroke, string fill, string key) => new(UiKind.ChartLine)
    {
        Key = key,
        Data = values.Select((value, i) => ((double)i, value)).ToList(),
        RangeX = (0, values.Count - 1),
        RangeY = (0, maxValue),
        StrokeColor = stroke,
        FillColor = fill,
    };
}
