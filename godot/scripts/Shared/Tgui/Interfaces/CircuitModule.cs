using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/CircuitModule.jsx (gaps CI1, D339, D341: the module
// component's ports). Engine-free (compiled into the tests too). A port's name is sent when its field is committed (Enter or leaving it).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class CircuitModule
{
    // tg: CircuitModule.jsx:5
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<string> global_port_types = data.Arr("global_port_types").Select(t => t.AsStr()).ToList();

        UiNode Ports(string title, string key, bool is_input) =>
            new UiNode(UiKind.Section) { Key = key, Title = title }.With(new UiNode(UiKind.Stack) { Key = key, Vertical = true }.With(
                data.Arr(key).Select((val, index) => PortEntry(c, val, index + 1, is_input, global_port_types))
                    .Append(new UiNode(UiKind.StackItem) { Key = "add" }.With(new UiNode(UiKind.Button)
                    {
                        Key = is_input ? "add_input_port" : "add_output_port",
                        Fluid = true,
                        Text = is_input ? "Add Input Port" : "Add Output Port",
                        Color = "good",
                        Icon = "plus",
                        OnClick = () => c.Act(is_input ? "add_input_port" : "add_output_port"),
                    }))));

        // <Window width={600} height={300}> <Window.Content scrollable>
        return c.Window(600, 300, null, new UiNode(UiKind.Stack) { Key = "root", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "open" }.With(new UiNode(UiKind.Button)
            {
                Key = "open_internal_circuit",
                Text = "View Internal Circuit",
                TextAlign = "center",
                Fluid = true,
                OnClick = () => c.Act("open_internal_circuit"),
            }),
            new UiNode(UiKind.StackItem) { Key = "ports" }.With(new UiNode(UiKind.Stack) { Key = "ports" }.With(
                new UiNode(UiKind.StackItem) { Key = "inputs", Grow = 1 }.With(Ports("Input Ports", "input_ports", true)),
                new UiNode(UiKind.StackItem) { Key = "outputs", Grow = 1 }.With(Ports("Output Ports", "output_ports", false))))));
    }

    // tg: CircuitModule.jsx:110  PortEntry
    private static UiNode PortEntry(TguiContext c, UiValue val, int port_id, bool is_input, IReadOnlyList<string> datatypeOptions) =>
        new UiNode(UiKind.StackItem) { Key = (is_input ? "in" : "out") + port_id }.With(new UiNode(UiKind.Stack) { Key = "entry" }.With(
            new UiNode(UiKind.StackItem) { Key = "name", Grow = 1 }.With(new UiNode(UiKind.Input)
            {
                Key = "name",
                Tooltip = "Name",
                TextValue = val.Str("name"),
                Fluid = true,
                CommitOnly = true,
                OnCommit = value => c.Act("set_port_name", ("port_id", port_id), ("is_input", is_input), ("port_name", value)),
            }),
            new UiNode(UiKind.StackItem) { Key = "type" }.With(new UiNode(UiKind.Dropdown)
            {
                Key = "type",
                TextValue = val.Str("type"),
                Options = datatypeOptions,
                OnSelected = type => c.Act("set_port_type", ("port_id", port_id), ("is_input", is_input), ("port_type", type)),
            }),
            new UiNode(UiKind.StackItem) { Key = "remove" }.With(new UiNode(UiKind.Button)
            {
                Key = "remove",
                Icon = "times",
                Color = "red",
                OnClick = () => c.Act(is_input ? "remove_input_port" : "remove_output_port", ("port_id", port_id)),
            })));
}
