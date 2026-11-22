using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/IntegratedCircuit/ (index.jsx, ObjectComponent.jsx, Port.jsx,
// DisplayName.jsx, FundamentalTypes.jsx, BasicInput.jsx, CircuitInfo.jsx, VariableMenu.jsx, ComponentMenu.jsx, DisplayComponent.jsx,
// constants.ts; gaps CI1, D339, D341). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// DEVIATION(DV571): tg's InfinitePlane of absolutely placed ObjectComponents with SVG wires is the kit's NodeGraph, which the Godot
// client draws with its own node graph (GraphEdit): a component is a graph node whose rows hold an input port at the left and an output
// port at the right (tg draws the inputs and the outputs in two columns); a wire is dragged from an output port to an input port (or the
// other way) and dragged off an input to remove it, besides tg's right click on a port; the plane's zoom and grid are Godot's. The
// component menu's and the variable menu's cards are dragged onto the plane as in tg, and a click on one also makes it, near the plane's
// top left corner; the examined component's info is a panel at the right instead of a box at the click. A port's composite ring (a list's or a table's
// datatypes) is not drawn; its colour is. Text fields send their text when committed (Enter or leaving the field).

public static class IntegratedCircuit
{
    // tg: IntegratedCircuit/constants.ts
    private const int DEFAULT_COMPONENT_MENU_LIMIT = 6;
    private const int VARIABLE_NOT_A_LIST = 1;
    private const int VARIABLE_LIST = 2;
    private const int VARIABLE_ASSOC_LIST = 3;

    /// <summary>Where a component made from a menu goes: this far into the plane's visible corner (DV571).</summary>
    private const double NewComponentOffset = 60;

    // tg: styles/interfaces/IntegratedCircuit.scss:84  $category-colors (bg, contrast)
    private static readonly Dictionary<string, (string Bg, string Contrast)> CategoryColors = new()
    {
        ["Utility"] = ("#0d6826", "white"),
        ["Entity"] = ("#aa32c8", "white"),
        ["Sensor"] = ("#e26e9b", "white"),
        ["Math"] = ("#3cc62a", "black"),
        ["Unassigned"] = ("#2a7dc6", "white"),
        ["String"] = ("#e16614", "white"),
        ["List"] = ("#ababab", "black"),
        ["Action"] = ("#2fb6b2", "black"),
        ["ID"] = ("#d6be1f", "black"),
        ["NTNet"] = ("#282bb8", "white"),
        ["Abstract"] = ("#2a7dc6", "white"),
        ["Admin"] = ("#db2424", "white"),
        ["BCI"] = ("#000000", "white"),
    };

    private static (string Bg, string Contrast) Category(string category) =>
        CategoryColors.TryGetValue(string.IsNullOrEmpty(category) ? "Unassigned" : category, out var colors) ? colors : CategoryColors["Unassigned"];

    // tg: IntegratedCircuit/index.jsx:405  render()
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> components = data.Arr("components");
        string display_name = data.Str("display_name");
        bool grid_mode = data.Truthy("grid_mode");
        bool is_admin = data.Truthy("is_admin");
        bool variableMenuOpen = c.UseState("variableMenuOpen", false);
        bool componentMenuOpen = c.UseState("componentMenuOpen", false);
        // The plane's offset: the server's screen_x, screen_y (static data) until this window pans it (tg's backgroundX, backgroundY)
        double screen_x = c.UseState("backgroundX", data.Num("screen_x"));
        double screen_y = c.UseState("backgroundY", data.Num("screen_y"));

        // tg: index.jsx:450  <Window width={1200} height={800} buttons={...}>
        var titleButtons = new UiNode(UiKind.Stack) { Key = "buttons" }.With(
            new UiNode(UiKind.StackItem) { Key = "name" }.With(new UiNode(UiKind.Input)
            {
                Key = "display_name",
                Tooltip = "Name",
                TextValue = display_name,
                CommitOnly = true,
                OnCommit = value => c.Act("set_display_name", ("display_name", value)),
            }),
            new UiNode(UiKind.StackItem) { Key = "variables" }.With(new UiNode(UiKind.Button)
            {
                Key = "toggle_variable_menu",
                Color = "transparent",
                Tooltip = "Show Variables Menu",
                Icon = "cog",
                Selected = variableMenuOpen,
                OnClick = () => c.SetState("variableMenuOpen", !variableMenuOpen),
            }),
            new UiNode(UiKind.StackItem) { Key = "components" }.With(new UiNode(UiKind.Button)
            {
                Key = "toggle_component_menu",
                Color = "transparent",
                Tooltip = "Show Components Menu",
                Icon = "plus",
                Selected = componentMenuOpen,
                OnClick = () => c.SetState("componentMenuOpen", !componentMenuOpen),
            }),
            new UiNode(UiKind.StackItem) { Key = "grid" }.With(new UiNode(UiKind.Button)
            {
                Key = "toggle_grid_mode",
                Color = "transparent",
                Tooltip = "Enable Grid Aligning",
                Icon = "th-large",
                Selected = grid_mode,
                OnClick = () => c.Act("toggle_grid_mode"),
            }),
            is_admin
                ? new UiNode(UiKind.StackItem) { Key = "save" }.With(new UiNode(UiKind.Button)
                {
                    Key = "save_circuit",
                    Color = "transparent",
                    Icon = "save",
                    OnClick = () => c.Act("save_circuit"),
                })
                : null);

        var row = new UiNode(UiKind.Stack) { Key = "plane_row", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "plane", Grow = 1 }.With(Plane(c, components, grid_mode, screen_x, screen_y)),
            data.Truthy("examined_name")
                ? new UiNode(UiKind.StackItem) { Key = "examined", Width = "220px" }.With(CircuitInfo(c, data))
                : null,
            componentMenuOpen
                ? new UiNode(UiKind.StackItem) { Key = "component_menu", Width = "300px" }.With(ComponentMenu(c, data, is_admin, screen_x, screen_y))
                : null);

        var content = new UiNode(UiKind.Stack) { Key = "root", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "title_buttons" }.With(titleButtons),
            new UiNode(UiKind.StackItem) { Key = "main", Grow = 1 }.With(row),
            variableMenuOpen
                ? new UiNode(UiKind.StackItem) { Key = "variable_menu", Height = "175px" }.With(VariableMenu(c, data, screen_x, screen_y))
                : null);
        return c.Window(1200, 800, null, content);
    }

    // tg: index.jsx:529  <InfinitePlane> of the ObjectComponents and their <Connections>
    private static UiNode Plane(TguiContext c, IReadOnlyList<UiValue> components, bool grid_mode, double screen_x, double screen_y)
    {
        var nodes = new List<UiNode>();
        var componentIds = new List<int>(); // a graph node's component id (1-based, tg's index + 1)
        var outputs = new Dictionary<string, (int Node, int Port, string Color)>(System.StringComparer.Ordinal);
        for (int i = 0; i < components.Count; i++)
        {
            UiValue comp = components[i];
            if (comp == null || comp.Kind == UiValueKind.Null)
                continue;
            IReadOnlyList<UiValue> output_ports = comp.Arr("output_ports");
            for (int p = 0; p < output_ports.Count; p++)
                outputs[output_ports[p].Str("ref")] = (nodes.Count, p, output_ports[p].Str("color"));
            componentIds.Add(i + 1);
            nodes.Add(ObjectComponent(c, comp, i + 1, grid_mode));
        }

        // tg: index.jsx:428  connections: every input's connected_to
        var links = new List<(int, int, int, int, string)>();
        int node = 0;
        foreach (UiValue comp in components)
        {
            if (comp == null || comp.Kind == UiValueKind.Null)
                continue;
            IReadOnlyList<UiValue> input_ports = comp.Arr("input_ports");
            for (int p = 0; p < input_ports.Count; p++)
            {
                foreach (UiValue output in input_ports[p].Arr("connected_to"))
                {
                    if (outputs.TryGetValue(output.AsStr(), out var from))
                        links.Add((from.Node, from.Port, node, p, string.IsNullOrEmpty(from.Color) ? "blue" : from.Color));
                }
            }
            node++;
        }

        return new UiNode(UiKind.NodeGraph)
        {
            Key = "plane",
            PosX = screen_x,
            PosY = screen_y,
            Selected = grid_mode,
            GraphLinks = links,
            // tg: index.jsx:133  handlePortUp: the wire from an output to an input
            OnConnect = (fromNode, fromPort, toNode, toPort) => c.Act("add_connection",
                ("input_port_id", toPort + 1),
                ("output_port_id", fromPort + 1),
                ("input_component_id", componentIds[toNode]),
                ("output_component_id", componentIds[fromNode])),
            // tg removes an input's wires with a right click on the port (handlePortRightClick): a wire dragged off its input does the same
            OnDisconnect = (fromNode, fromPort, toNode, toPort) => c.Act("remove_connection",
                ("component_id", componentIds[toNode]),
                ("is_input", true),
                ("port_id", toPort + 1)),
            // tg: index.jsx:330  handleComponentDropped, :305 handleVarDropped: a menu's card dragged onto the plane lands where it is dropped
            OnDropAt = (x, y) =>
            {
                string dragged = c.Local.Get<string>("dragging", null);
                c.Local.SetQuiet<string>("dragging", null);
                if (dragged == null)
                    return;
                if (dragged.StartsWith("setter:", System.StringComparison.Ordinal) || dragged.StartsWith("getter:", System.StringComparison.Ordinal))
                    c.Act("add_setter_or_getter", ("variable", dragged[7..]), ("is_setter", dragged[0] == 's'), ("rel_x", x), ("rel_y", y));
                else
                    c.Act("print_component", ("component_to_print", dragged), ("rel_x", x), ("rel_y", y));
            },
            // tg: index.jsx:262  handleMouseUp: move_screen with the plane's new offset
            OnMoved = (x, y) =>
            {
                c.SetState("backgroundX", x);
                c.SetState("backgroundY", y);
                c.Act("move_screen", ("screen_x", x), ("screen_y", y));
            },
        }.With(nodes);
    }

    // tg: IntegratedCircuit/ObjectComponent.jsx:88  roundToGrid
    private static double RoundToGrid(double value, bool gridMode) => gridMode ? System.Math.Round(value / 10) * 10 : value;

    // tg: IntegratedCircuit/ObjectComponent.jsx:94  render()
    private static UiNode ObjectComponent(TguiContext c, UiValue comp, int index, bool gridMode)
    {
        string category = comp.Str("category", "Unassigned");
        (string bg, string contrast) = Category(category);
        var titleButtons = new List<UiNode>();
        foreach (KeyValuePair<string, UiValue> button in comp.Obj("ui_buttons"))
        {
            string action_name = button.Value.AsStr();
            titleButtons.Add(new UiNode(UiKind.Button)
            {
                Key = "button-" + button.Key,
                Icon = button.Key,
                Background = bg,
                OnClick = () => c.Act("perform_action", ("component_id", index), ("action_name", action_name)),
            });
        }
        foreach (KeyValuePair<string, UiValue> alert in comp.Obj("ui_alerts"))
            titleButtons.Add(new UiNode(UiKind.Button) { Key = "alert-" + alert.Key, Icon = alert.Key, Background = bg, Tooltip = alert.Value.AsStr() });
        titleButtons.Add(new UiNode(UiKind.Button)
        {
            Key = "info",
            Icon = "info",
            Background = bg,
            OnClick = () => c.Act("set_examined_component", ("component_id", index), ("x", 0), ("y", 0)),
        });
        if (comp.Truthy("removable"))
            titleButtons.Add(new UiNode(UiKind.Button) { Key = "detach", Icon = "times", Background = bg, OnClick = () => c.Act("detach_component", ("component_id", index)) });

        IReadOnlyList<UiValue> input_ports = comp.Arr("input_ports");
        IReadOnlyList<UiValue> output_ports = comp.Arr("output_ports");
        var slots = new List<UiNode>();
        for (int row = 0; row < System.Math.Max(input_ports.Count, output_ports.Count); row++)
        {
            UiValue input = row < input_ports.Count ? input_ports[row] : null;
            UiValue output = row < output_ports.Count ? output_ports[row] : null;
            slots.Add(new UiNode(UiKind.GraphSlot)
            {
                Key = "row" + row,
                PortIn = input?.Str("color", "blue"),
                PortOut = output?.Str("color", "blue"),
            }.With(
                new UiNode(UiKind.StackItem) { Key = "in", Grow = 1 }.With(input == null ? null : DisplayName(c, input, false, index, row + 1)),
                new UiNode(UiKind.StackItem) { Key = "out" }.With(output == null ? null : DisplayName(c, output, true, index, row + 1))));
        }

        return new UiNode(UiKind.GraphNode)
        {
            Key = "component" + index,
            Text = comp.Str("name"),
            Background = bg,
            Color = contrast,
            PosX = comp.Num("x"),
            PosY = comp.Num("y"),
            Buttons = new UiNode(UiKind.Stack) { Key = "title" }.With(titleButtons.Select(b => new UiNode(UiKind.StackItem) { Key = b.Key }.With(b))),
            // tg: ObjectComponent.jsx:39  handleStopDrag
            OnMoved = (x, y) => c.Act("set_component_coordinates", ("component_id", index), ("rel_x", RoundToGrid(x, gridMode)), ("rel_y", RoundToGrid(y, gridMode))),
            // tg: index.jsx:216  handlePortRightClick
            OnPortRightClick = (isInput, port) => c.Act("remove_connection", ("component_id", index), ("is_input", isInput), ("port_id", port + 1)),
        }.With(slots);
    }

    // tg: IntegratedCircuit/DisplayName.jsx:8
    private static UiNode DisplayName(TguiContext c, UiValue port, bool isOutput, int componentId, int portIndex)
    {
        string type = port.Str("type", "unknown");
        bool hasInput = !isOutput && port.Arr("connected_to").Count == 0 && (componentId != 0 || type == "option") && IsFundamental(type);
        // tg: FundamentalTypes.jsx:143  DATATYPE_DISPLAY_HANDLERS.option
        string displayType = type == "option" ? port.Str("name").ToLowerInvariant() : type;
        string align = isOutput ? "right" : "left";

        UiNode main;
        if (hasInput)
            main = FundamentalInput(c, port, type, (val, extra) => SetInput(c, componentId, portIndex, val, extra));
        else if (isOutput)
        {
            main = new UiNode(UiKind.Button)
            {
                Key = "value",
                Color = "transparent",
                Text = port.Str("name"),
                OnClick = () => c.Act("get_component_value", ("component_id", componentId), ("port_id", portIndex)),
            };
        }
        else
            main = new UiNode(UiKind.Box) { Key = "name", Text = port.Str("name") };

        return new UiNode(UiKind.Stack) { Key = "display", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "main" }.With(new UiNode(UiKind.Box) { Key = "align", TextAlign = align }.With(main)),
            new UiNode(UiKind.StackItem) { Key = "type" }.With(new UiNode(UiKind.Box)
            {
                Key = "type",
                FontSize = 0.75,
                Opacity = 0.5,
                TextAlign = align,
                Text = string.IsNullOrEmpty(displayType) ? "unknown" : displayType,
            }));
    }

    // tg: DisplayName.jsx:25  act('set_component_input', { component_id, port_id, input: val, ...extraParams })
    private static void SetInput(TguiContext c, int componentId, int portIndex, object val, (string Key, object Value)[] extra)
    {
        var parameters = new List<(string, object)> { ("component_id", componentId), ("port_id", portIndex), ("input", val) };
        if (extra != null)
            parameters.AddRange(extra);
        c.Act("set_component_input", parameters.ToArray());
    }

    private static bool IsFundamental(string type) => type is "string" or "number" or "entity" or "datum" or "signal" or "option" or "any" or "boolean";

    private delegate void SetValue(object val, (string Key, object Value)[] extra);

    // tg: IntegratedCircuit/FundamentalTypes.jsx:12  FUNDAMENTAL_DATA_TYPES
    private static UiNode FundamentalInput(TguiContext c, UiValue port, string type, SetValue setValue)
    {
        string name = port.Str("name");
        string color = port.Str("color");
        UiValue value = port.Get("current_data");
        bool hasValue = value != null && value.Kind != UiValueKind.Null;
        switch (type)
        {
            case "string":
                return BasicInput(name, hasValue, setValue, "", new UiNode(UiKind.Input)
                {
                    Key = "string",
                    Tooltip = name,
                    TextValue = value.AsStr(),
                    Width = "96px",
                    CommitOnly = true,
                    OnCommit = text => setValue(text, null),
                });
            case "number":
                return BasicInput(name, hasValue, setValue, 0, new UiNode(UiKind.NumberInput)
                {
                    Key = "number",
                    Step = 1,
                    Value = value.AsNum(),
                    MinValue = -1e7, // tg's NumberInput here has no bounds
                    MaxValue = 1e7,
                    Color = color,
                    Unit = name,
                    OnChange = number => setValue(number, null),
                });
            case "entity":
            case "datum":
                return new UiNode(UiKind.Button)
                {
                    Key = type,
                    Text = name,
                    Color = "transparent",
                    Icon = "upload",
                    OnClick = () => setValue(null, new[] { ("marked_atom", (object)true) }),
                };
            case "signal":
                return new UiNode(UiKind.Button) { Key = "signal", Text = name, Color = "transparent", OnClick = () => setValue(null, null) };
            case "option":
            {
                UiValue extraData = port.Get("datatype_data");
                IReadOnlyList<string> options = extraData?.Kind == UiValueKind.Object
                    ? extraData.Fields.Select(f => f.Key).ToList()
                    : (extraData?.Items ?? System.Array.Empty<UiValue>()).Select(v => v.AsStr()).ToList();
                return new UiNode(UiKind.Dropdown)
                {
                    Key = "option",
                    Color = "transparent",
                    Options = options,
                    TextValue = hasValue ? value.AsStr() : null,
                    OnSelected = picked => setValue(picked, null),
                };
            }
            case "any":
                return BasicInput(name, hasValue, setValue, "", new UiNode(UiKind.Stack) { Key = "any" }.With(
                    new UiNode(UiKind.StackItem) { Key = "upload" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "upload",
                        Color = color,
                        Icon = "upload",
                        OnClick = () => setValue(null, new[] { ("marked_atom", (object)true) }),
                    }),
                    new UiNode(UiKind.StackItem) { Key = "text" }.With(new UiNode(UiKind.Input)
                    {
                        Key = "any",
                        Tooltip = name,
                        TextValue = value.AsStr(),
                        Width = "64px",
                        CommitOnly = true,
                        OnCommit = text => setValue(text, null),
                    })));
            case "boolean":
            {
                bool on = value?.Truthy ?? false;
                return new UiNode(UiKind.Stack) { Key = "boolean" }.With(
                    new UiNode(UiKind.StackItem) { Key = "name" }.With(new UiNode(UiKind.Box) { Text = name }),
                    new UiNode(UiKind.StackItem) { Key = "check" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "boolean",
                        Checkbox = on,
                        OnClick = () => setValue(!on, null),
                    }));
            }
            default:
                return new UiNode(UiKind.Box) { Text = name };
        }
    }

    // tg: IntegratedCircuit/BasicInput.jsx:3
    private static UiNode BasicInput(string name, bool hasValue, SetValue setValue, object defaultValue, UiNode child)
    {
        if (hasValue)
        {
            return new UiNode(UiKind.Stack) { Key = "basic" }.With(
                new UiNode(UiKind.StackItem) { Key = "null" }.With(new UiNode(UiKind.Button)
                {
                    Key = "set_null",
                    Color = "transparent",
                    Icon = "times",
                    OnClick = () => setValue(null, new[] { ("set_null", (object)true) }),
                }),
                new UiNode(UiKind.StackItem) { Key = "input" }.With(child));
        }
        return new UiNode(UiKind.Button) { Key = "default", Text = name, Color = "transparent", OnClick = () => setValue(defaultValue, null) };
    }

    // tg: IntegratedCircuit/CircuitInfo.jsx:3 (index.jsx:578: shown while examined_name is set; tg hides it at the next mouse down)
    private static UiNode CircuitInfo(TguiContext c, UiValue data)
    {
        var notices = data.Arr("examined_notices").Select((notice, i) => new UiNode(UiKind.StackItem) { Key = "notice" + i }.With(new UiNode(UiKind.Button)
        {
            Key = "notice" + i,
            Text = notice.Str("content"),
            Color = notice.Str("color"),
            Icon = notice.Str("icon"),
            Fluid = true,
        }));
        return new UiNode(UiKind.Section)
        {
            Key = "examined",
            Title = data.Str("examined_name"),
            Fill = true,
            Scrollable = true,
            Buttons = new UiNode(UiKind.Button) { Key = "close_examined", Icon = "times", Color = "transparent", OnClick = () => c.Act("remove_examined_component") },
        }.With(new UiNode(UiKind.Stack) { Key = "info", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "desc" }.With(new UiNode(UiKind.Box) { Key = "desc", Wrap = true, Text = data.Str("examined_desc") }),
            new UiNode(UiKind.StackItem) { Key = "notices" }.With(new UiNode(UiKind.Stack) { Key = "notices", Vertical = true }.With(notices))));
    }

    // tg: IntegratedCircuit/VariableMenu.jsx:41
    private static UiNode VariableMenu(TguiContext c, UiValue data, double screen_x, double screen_y)
    {
        string variable_name = c.UseState("variable_name", "");
        string variable_type = c.UseState("variable_type", "any");
        IReadOnlyList<string> types = data.Arr("global_basic_types").Select(t => t.AsStr()).ToList();

        var rows = data.Arr("variables").Select(val =>
        {
            string name = val.Str("name");
            string color = val.Str("color");
            // tg: index.jsx:278  handleVarClicked, dropped with handleVarDropped: add_setter_or_getter where it lands (DV571: a click)
            void Add(bool is_setter) => c.Act("add_setter_or_getter",
                ("variable", name),
                ("is_setter", is_setter),
                ("rel_x", NewComponentOffset - screen_x),
                ("rel_y", NewComponentOffset - screen_y));
            return new UiNode(UiKind.StackItem) { Key = "var-" + name }.With(new UiNode(UiKind.Stack) { Key = "row" }.With(
                new UiNode(UiKind.StackItem) { Key = "name", Grow = 1 }.With(new UiNode(UiKind.Box) { Text = name }),
                new UiNode(UiKind.StackItem) { Key = "type" }.With(new UiNode(UiKind.Button) { Key = "type", TextAlign = "center", Fluid = true, Color = color, Text = val.Str("datatype") }),
                new UiNode(UiKind.StackItem) { Key = "setter" }.With(new UiNode(UiKind.Button)
                {
                    Key = "setter",
                    Fluid = true,
                    Color = color,
                    Disabled = val.Truthy("is_list"),
                    Tooltip = "Drag me onto the circuit's grid to make a setter for this variable",
                    Icon = "pen",
                    OnClick = () => Add(true),
                    OnDragStart = () => c.Local.SetQuiet("dragging", "setter:" + name),
                }),
                new UiNode(UiKind.StackItem) { Key = "getter" }.With(new UiNode(UiKind.Button)
                {
                    Key = "getter",
                    Fluid = true,
                    Color = color,
                    Tooltip = "Drag me onto the circuit's grid to make a getter for this variable",
                    Icon = "book-open",
                    OnClick = () => Add(false),
                    OnDragStart = () => c.Local.SetQuiet("dragging", "getter:" + name),
                }),
                new UiNode(UiKind.StackItem) { Key = "remove" }.With(new UiNode(UiKind.Button)
                {
                    Key = "remove",
                    Icon = "times",
                    Color = "bad",
                    OnClick = () => c.Act("remove_variable", ("variable_name", name)),
                })));
        });

        // tg: index.jsx:612  onAddVariable
        void AddVariable(int listType) => c.Act("add_variable",
            ("variable_name", variable_name),
            ("variable_datatype", variable_type),
            ("is_list", listType == VARIABLE_LIST),
            ("is_assoc_list", listType == VARIABLE_ASSOC_LIST));

        UiNode AddButton(string key, string icon, int listType) => new UiNode(UiKind.StackItem) { Key = key, Grow = 1 }.With(new UiNode(UiKind.Button)
        {
            Key = key,
            Color = "green",
            Fluid = true,
            Icon = icon,
            OnClick = () => AddVariable(listType),
        });

        return new UiNode(UiKind.Section)
        {
            Key = "variable_options",
            Title = "Variable Options",
            Fill = true,
            Buttons = new UiNode(UiKind.Button) { Key = "close_variables", Icon = "times", Color = "transparent", OnClick = () => c.SetState("variableMenuOpen", false) },
        }.With(new UiNode(UiKind.Stack) { Key = "menu", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "variables", Fill = true, Scrollable = true }.With(
                new UiNode(UiKind.Stack) { Key = "variables", Vertical = true }.With(rows))),
            new UiNode(UiKind.StackItem) { Key = "new", Width = "25%" }.With(new UiNode(UiKind.Stack) { Key = "new", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "name" }.With(new UiNode(UiKind.Input)
                {
                    Key = "variable_name",
                    Tooltip = "Name",
                    Fluid = true,
                    TextValue = variable_name,
                    OnInput = text => c.SetState("variable_name", text),
                }),
                new UiNode(UiKind.StackItem) { Key = "type" }.With(new UiNode(UiKind.Dropdown)
                {
                    Key = "variable_type",
                    Options = types,
                    TextValue = variable_type,
                    Width = "100%",
                    OnSelected = picked => c.SetState("variable_type", picked),
                }),
                new UiNode(UiKind.StackItem) { Key = "add" }.With(new UiNode(UiKind.Stack) { Key = "add", Fill = true }.With(
                    AddButton("add_plain", "plus", VARIABLE_NOT_A_LIST),
                    AddButton("add_list", "list-ol", VARIABLE_LIST),
                    AddButton("add_assoc", "table-list", VARIABLE_ASSOC_LIST)))))));
    }

    // tg: IntegratedCircuit/ComponentMenu.jsx:52
    private static UiNode ComponentMenu(TguiContext c, UiValue data, bool showAll, double screen_x, double screen_y)
    {
        string selectedTab = c.UseState("selectedTab", "All");
        int currentLimit = c.UseState("currentLimit", DEFAULT_COMPONENT_MENU_LIMIT);
        string currentSearch = c.UseState("currentSearch", "");
        // tg: index.jsx:635  components={(stored_designs && Object.keys(stored_designs)) || []}
        var components = new HashSet<string>(data.Obj("stored_designs").Select(kv => kv.Key), System.StringComparer.Ordinal);
        // DEVIATION(DV568): circuit_components.json comes with the static data; tg sorts it by name (its comparator returns a boolean)
        IEnumerable<UiValue> componentData = data.Arr("circuit_components").OrderBy(v => v.Str("name").ToLowerInvariant(), System.StringComparer.Ordinal);

        var tabs = new List<string> { "All" };
        var shownComponents = componentData.Where(val =>
        {
            bool shouldShow = showAll || components.Contains(val.Str("type"));
            if (!shouldShow)
                return false;
            string category = val.Str("category");
            if (!tabs.Contains(category))
                tabs.Add(category);
            if (currentSearch != "")
                return val.Str("name").Contains(currentSearch, System.StringComparison.OrdinalIgnoreCase);
            return selectedTab == "All" || selectedTab == category;
        }).ToList();
        int trueLength = shownComponents.Count;

        var items = new List<UiNode>();
        if (trueLength == 0)
        {
            items.Add(new UiNode(UiKind.StackItem) { Key = "notice" }.With(new UiNode(UiKind.NoticeBox)
            {
                Key = "notice",
                Color = "info",
                Text = "You can hit this integrated circuit onto a component printer to link it so that you're able to remotely create and add components to this circuit.",
            }));
        }
        foreach (UiValue val in shownComponents.Take(currentLimit))
        {
            string type = val.Str("type");
            items.Add(new UiNode(UiKind.StackItem) { Key = type }.With(DisplayComponent(val,
                () => c.Act("print_component", ("component_to_print", type), ("rel_x", NewComponentOffset - screen_x), ("rel_y", NewComponentOffset - screen_y)),
                () => c.Local.SetQuiet("dragging", type))));
        }
        if (trueLength > currentLimit)
        {
            items.Add(new UiNode(UiKind.StackItem) { Key = "more" }.With(new UiNode(UiKind.Button)
            {
                Key = "show_more",
                TextAlign = "center",
                Text = "Show More",
                Fluid = true,
                OnClick = () => c.SetState("currentLimit", currentLimit + 5),
            }));
        }

        return new UiNode(UiKind.Section)
        {
            Key = "component_menu",
            Title = "Component Menu",
            Fill = true,
            Scrollable = true,
            Buttons = new UiNode(UiKind.Button) { Key = "close_components", Icon = "times", Color = "transparent", OnClick = () => c.SetState("componentMenuOpen", false) },
        }.With(new UiNode(UiKind.Stack) { Key = "menu", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "category" }.With(new UiNode(UiKind.Dropdown)
            {
                Key = "category",
                Width = "100%",
                Options = tabs,
                TextValue = selectedTab,
                Tooltip = "Category",
                Color = "transparent",
                OnSelected = value =>
                {
                    c.SetState("selectedTab", value);
                    c.SetState("currentSearch", "");
                    c.SetState("currentLimit", DEFAULT_COMPONENT_MENU_LIMIT);
                },
            }),
            new UiNode(UiKind.StackItem) { Key = "search" }.With(new UiNode(UiKind.Input)
            {
                Key = "search",
                Tooltip = "Search..",
                TextValue = currentSearch,
                Fluid = true,
                OnInput = text =>
                {
                    c.SetState("currentSearch", text);
                    c.SetState("selectedTab", "All");
                    c.SetState("currentLimit", DEFAULT_COMPONENT_MENU_LIMIT);
                },
            }),
            new UiNode(UiKind.StackItem) { Key = "list" }.With(new UiNode(UiKind.Stack) { Key = "list", Vertical = true }.With(items))));
    }

    // tg: IntegratedCircuit/DisplayComponent.jsx:36  (fixedSize, in the component menu); a click prints it (DV571)
    private static UiNode DisplayComponent(UiValue component, System.Action onPrint, System.Action onDrag)
    {
        (string bg, string contrast) = Category(component.Str("category"));
        IReadOnlyList<UiValue> input_ports = component.Arr("input_ports");
        IReadOnlyList<UiValue> output_ports = component.Arr("output_ports");
        UiNode PortName(UiValue port, bool isOutput, int i) => new UiNode(UiKind.StackItem) { Key = (isOutput ? "out" : "in") + i }.With(
            new UiNode(UiKind.Box)
            {
                Key = "port",
                TextAlign = isOutput ? "right" : "left",
                Color = port.Str("color", "blue"),
                Text = isOutput ? port.Str("name") + " ●" : "● " + port.Str("name"),
            });
        return new UiNode(UiKind.Stack) { Key = "display", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "title" }.With(new UiNode(UiKind.Button)
            {
                Key = "print",
                Fluid = true,
                Background = bg,
                Color = contrast,
                Text = component.Str("name"),
                Tooltip = component.Str("description"),
                OnClick = onPrint,
                OnDragStart = onDrag,
            }),
            new UiNode(UiKind.StackItem) { Key = "ports" }.With(new UiNode(UiKind.Stack) { Key = "ports" }.With(
                new UiNode(UiKind.StackItem) { Key = "inputs", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "inputs", Vertical = true }.With(
                    input_ports.Select((p, i) => PortName(p, false, i)))),
                new UiNode(UiKind.StackItem) { Key = "outputs" }.With(new UiNode(UiKind.Stack) { Key = "outputs", Vertical = true }.With(
                    output_ports.Select((p, i) => PortName(p, true, i)))))));
    }
}
