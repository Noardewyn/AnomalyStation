using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AtmosControlConsole.tsx (package E5: the atmospherics
// control consoles, their chamber's sensor reading and its input injector and output vent). Original copyright and license: tgstation,
// AGPL-3.0 (code). See ../tgstation/LICENSE.
// As in AtmosHandbook.cs, a box has no italic.

public static class AtmosControlConsole
{
    // tg: tgui/packages/tgui/interfaces/AtmosControlConsole.tsx:38
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> chambers = data.Arr("chambers");
        double maxInput = data.Num("maxInput");
        double maxOutput = data.Num("maxOutput");
        bool reconnecting = data.Truthy("reconnecting");
        bool control = data.Truthy("control");
        string defaultGas = data.Truthy("defaultGas") ? data.Str("defaultGas") : null;
        string chamberId = c.UseState("chamber", chambers.FirstOrDefault()?.Str("id"));
        UiValue selectedChamber = chambers.Count == 1 ? chambers[0] : chambers.FirstOrDefault(chamber => chamber.Str("id") == chamberId);

        if (defaultGas != null)
            AtmosHandbook.SetActiveGas(c, defaultGas);

        var top = new UiNode(UiKind.Fragment);
        // tg: AtmosControlConsole.tsx:62  the chamber selection of a console with several chambers (the ordnance one)
        if (chambers.Count > 1)
        {
            top.With(new UiNode(UiKind.Section) { Key = "selection", Title = "Chamber Selection" }.With(
                new UiNode(UiKind.Dropdown)
                {
                    Key = "chamber",
                    Width = "100%",
                    Options = chambers.Select(chamber => chamber.Str("name")).ToArray(),
                    TextValue = selectedChamber?.Str("name"),
                    OnSelected = value => c.SetState("chamber", chambers.FirstOrDefault(chamber => chamber.Str("name") == value)?.Str("id") ?? chambers[0].Str("id")),
                }));
        }

        // tg: AtmosControlConsole.tsx:77  the chamber's reading
        UiValue gasmix = selectedChamber?.Get("gasmix");
        top.With(new UiNode(UiKind.Section)
        {
            Key = "reading",
            Title = selectedChamber != null ? selectedChamber.Str("name") : "Chamber Reading",
            Buttons = reconnecting ? new UiNode(UiKind.Button) { Key = "reconnect", Icon = "undo", Text = "Reconnect", OnClick = () => c.Act("reconnect") } : null,
        }.With(
            gasmix != null && gasmix.Truthy
                ? GasmixParser.Render(gasmix, id => AtmosHandbook.SetActiveGas(c, id), id => AtmosHandbook.SetActiveReaction(c, id))
                : new UiNode(UiKind.Box) { Key = "no-sensor", Text = "No Sensors Detected!" }));

        // tg: AtmosControlConsole.tsx:100  the controls of a console that has them
        if (selectedChamber != null && control)
        {
            string id = selectedChamber.Str("id");
            UiValue input = selectedChamber.Get("input_info");
            UiValue output = selectedChamber.Get("output_info");
            top.With(new UiNode(UiKind.Section) { Key = "controls", Title = "Chamber Controls" }.With(
                new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem) { Key = "input", Grow = 1 }.With(
                        input != null && input.Truthy
                            ? new UiNode(UiKind.LabeledList).With(
                                new UiNode(UiKind.LabeledItem) { Label = "Input Injector" }.With(
                                    new UiNode(UiKind.Button)
                                    {
                                        Key = "toggle_input",
                                        Icon = input.Truthy("active") ? "power-off" : "times",
                                        Text = input.Truthy("active") ? "Injecting" : "Off",
                                        Selected = input.Truthy("active"),
                                        OnClick = () => c.Act("toggle_input", ("chamber", id)),
                                    }),
                                new UiNode(UiKind.LabeledItem) { Label = "Input Rate" }.With(
                                    new UiNode(UiKind.NumberInput)
                                    {
                                        Key = "adjust_input",
                                        Step = 1,
                                        Value = input.Num("amount"),
                                        Unit = "L/s",
                                        Width = "63px",
                                        MinValue = 0,
                                        MaxValue = maxInput,
                                        OnChange = value => c.Act("adjust_input", ("chamber", id), ("rate", value)),
                                    }))
                            : new UiNode(UiKind.Box) { Key = "no-input", Text = "No Input Device Detected!" }),
                    new UiNode(UiKind.StackItem) { Key = "output", Grow = 1 }.With(
                        output != null && output.Truthy
                            ? new UiNode(UiKind.LabeledList).With(
                                new UiNode(UiKind.LabeledItem) { Label = "Output Regulator" }.With(
                                    new UiNode(UiKind.Button)
                                    {
                                        Key = "toggle_output",
                                        Icon = output.Truthy("active") ? "power-off" : "times",
                                        Text = output.Truthy("active") ? "Open" : "Closed",
                                        Selected = output.Truthy("active"),
                                        OnClick = () => c.Act("toggle_output", ("chamber", id)),
                                    }),
                                new UiNode(UiKind.LabeledItem) { Label = "Output Pressure" }.With(
                                    new UiNode(UiKind.NumberInput)
                                    {
                                        Key = "adjust_output",
                                        Value = output.Num("amount"),
                                        Unit = "kPa",
                                        Width = "75px",
                                        MinValue = 0,
                                        MaxValue = maxOutput,
                                        Step = 10,
                                        OnChange = value => c.Act("adjust_output", ("chamber", id), ("rate", value)),
                                    }))
                            : new UiNode(UiKind.Box) { Key = "no-output", Text = "No Output Device Detected !" }))));
        }

        // tg: AtmosControlConsole.tsx:57  <Window width={550} height={600}> <Window.Content scrollable>
        return c.Window(550, 600, null,
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "top" }.With(top),
                // tg: AtmosControlConsole.tsx:196
                new UiNode(UiKind.StackItem) { Key = "handbook", Grow = 1 }.With(AtmosHandbook.Content(c))));
    }
}
