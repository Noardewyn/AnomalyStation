using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/TramController.tsx (the tram's controller cabinet,
// tram_controller.dm's ui_data and ui_static_data; it sends dispatch, estop, reset, dopen, dclose and togglesensors).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class TramController
{
    // tg: TramController.tsx:97 and :109
    private static readonly (string, double, double)[] LoadRanges = { ("good", double.NegativeInfinity, 5), ("average", 5, 7.5), ("bad", 7.5, double.PositiveInfinity) };
    private static readonly (string, double, double)[] SpeedRanges = { ("good", 28, double.PositiveInfinity), ("average", 24, 28), ("bad", 0.1, 24), ("white", double.NegativeInfinity, 0) };

    // tg: TramController.tsx:43
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool controllerActive = data.Truthy("controllerActive");
        bool controllerOperational = data.Truthy("controllerOperational");
        bool recoveryMode = data.Truthy("recoveryMode");
        double currentSpeed = data.Num("currentSpeed");
        bool statusSF = data.Truthy("statusSF"), statusCE = data.Truthy("statusCE"), statusES = data.Truthy("statusES"), statusPD = data.Truthy("statusPD");
        bool statusDR = data.Truthy("statusDR"), statusCL = data.Truthy("statusCL"), statusBS = data.Truthy("statusBS");
        string tripDestination = c.UseState("tripDestination", "");

        c.WindowTheme = "dark";
        return c.Window(778, 327, "Tram Controller",
            new UiNode(UiKind.Stack) { Key = "body" }.With(
                new UiNode(UiKind.StackItem) { Key = "left", Grow = 4 }.With(
                    new UiNode(UiKind.Section) { Key = "status", Title = "System Status" }.With(
                        new UiNode(UiKind.LabeledList) { Key = "list" }.With(
                            Item("System ID", data.Str("transportId")),
                            Item("Controller Queue", controllerActive ? "Processing" : "Ready", controllerActive ? "blue" : "good"),
                            Item("Mechanical Status", controllerOperational ? "Normal" : "Fault", controllerOperational ? "good" : "bad"),
                            Item("Processor Status", recoveryMode ? "Overload" : "Normal", recoveryMode ? "average" : "good"),
                            new UiNode(UiKind.LabeledItem) { Key = "load", Label = "Processor Load" }.With(
                                new UiNode(UiKind.ProgressBar) { Key = "bar", Value = data.Num("currentLoad"), MinValue = 0, MaxValue = 15, Ranges = LoadRanges }),
                            new UiNode(UiKind.LabeledItem) { Key = "speed", Label = "Current Speed" }.With(
                                new UiNode(UiKind.ProgressBar)
                                {
                                    Key = "bar", Value = currentSpeed, MinValue = 0, MaxValue = 32, Ranges = SpeedRanges,
                                    Text = TguiFormat.ToFixed(currentSpeed * 2.25, 0) + " km/h",
                                }))),
                    new UiNode(UiKind.Section) { Key = "location", Title = "Location Data" }.With(
                        new UiNode(UiKind.LabeledList) { Key = "list" }.With(
                            Item("Direction", data.Num("travelDirection") == 4 ? "Outbound" : "Inbound"),
                            Item("Idle Platform", data.Str("idlePlatform"), controllerActive ? null : "blue"),
                            Item("Destination Platform", data.Str("destinationPlatform"), controllerActive ? "blue" : null)))),
                new UiNode(UiKind.StackItem) { Key = "right", Grow = 6 }.With(
                    new UiNode(UiKind.Section) { Key = "controls", Title = "Controls" }.With(
                        new UiNode(UiKind.NoticeBox) { Key = "notice", Text = "Nanotrasen is not responsible for any injuries or fatalities caused by usage of the tram." },
                        Command(c, "reset", "arrows-rotate", "yellow", "28%", "Reset/Enable"),
                        Command(c, "estop", "square", "bad", "28%", "E-Stop/Disable"),
                        new UiNode(UiKind.Button)
                        {
                            Key = "dispatch", Icon = "play", Color = "green", Disabled = statusES || statusSF, Width = "42%", TextAlign = "center",
                            OnClick = () => c.Act("dispatch", ("tripDestination", tripDestination)), Text = "Start: Destination",
                        },
                        new UiNode(UiKind.Dropdown)
                        {
                            Key = "destination", Width = "98.5%", Options = data.Arr("destinations").Select(d => d.Str("name")).ToList(),
                            TextValue = tripDestination, Tooltip = "Pick a Destination", OnSelected = value => c.SetState("tripDestination", value),
                        },
                        Command(c, "dopen", "bars", "blue", "25%", "Open Doors"),
                        Command(c, "dclose", "bars", "blue", "25%", "Close Doors"),
                        Command(c, "togglesensors", "bars", statusBS ? "good" : "bad", "48%", "Bypass Door Sensors")),
                    new UiNode(UiKind.Section) { Key = "operational", Title = "Operational" }.With(
                        Lamp("ESTOP", statusES ? "red" : "transparent"),
                        Lamp("FAULT", statusSF ? "yellow" : "transparent"),
                        Lamp("COMMS", statusCE ? "teal" : "transparent"),
                        Lamp("RQST", statusPD ? "blue" : "transparent"),
                        Lamp("DOORS", statusDR ? "transparent" : "blue"),
                        Lamp("BUSY", statusCL ? "blue" : "transparent")))));
    }

    private static UiNode Item(string label, string text, string color = null)
        => new UiNode(UiKind.LabeledItem) { Key = label, Label = label, Color = color, Text = text };

    private static UiNode Command(TguiContext c, string action, string icon, string color, string width, string text)
        => new UiNode(UiKind.Button) { Key = action, Icon = icon, Color = color, Width = width, TextAlign = "center", OnClick = () => c.Act(action), Text = text };

    private static UiNode Lamp(string text, string color)
        => new UiNode(UiKind.Button) { Key = text, Color = color, Width = "16%", TextAlign = "center", Text = text };
}
