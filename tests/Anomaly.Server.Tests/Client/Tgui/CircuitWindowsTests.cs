using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps CI1 (D339, D341): the client's circuit windows. IntegratedCircuit (tg's IntegratedCircuit/*.jsx) draws the components as the
/// nodes of a node graph (DV571) with their ports' colours, the wires between them, the input fields of the free inputs by datatype,
/// the title row's buttons, and sends add_connection, remove_connection, set_component_coordinates, set_component_input,
/// get_component_value, perform_action, detach_component and the editor's own acts; its component and variable menus print and add
/// setters and getters. ComponentPrinter prints a design and ejects materials, CircuitModule edits the module's ports and
/// CircuitAdminPanel lists the circuits with their admin buttons. All four are registered under their names.
/// </summary>
public class CircuitWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json, TguiLocalState local = null, List<(string, string)> acts = null)
    {
        acts ??= new List<(string, string)>();
        var context = new TguiContext(1, iface, "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    // the first node of the key; a wrapper (a stack item, a row) that shares its key with the control inside gives the control
    private static UiNode ByKey(UiNode tree, string key)
    {
        UiNode found = tree.Walk().First(n => n.Key == key);
        while (found.Walk().Skip(1).FirstOrDefault(n => n.Key == key) is UiNode inner)
            found = inner;
        return found;
    }

    // two components: an arithmetic (two number inputs, the first wired from the second component's output) and a To String (an any input)
    private const string Circuit =
        "{\"components\":["
        + "{\"name\":\"Arithmetic\",\"x\":100,\"y\":50,\"removable\":1,\"color\":\"blue\",\"category\":\"Math\",\"ui_alerts\":{},\"ui_buttons\":{\"plus\":\"add\",\"minus\":\"remove\"},"
        + "\"input_ports\":[{\"name\":\"Arithmetic Option\",\"type\":\"option\",\"ref\":\"[0x10]\",\"connected_to\":[],\"color\":\"white\",\"current_data\":\"Add\",\"datatype_data\":[\"Add\",\"Subtract\"]},"
        + "{\"name\":\"Port\",\"type\":\"number\",\"ref\":\"[0x11]\",\"connected_to\":[\"[0x21]\"],\"color\":\"green\",\"current_data\":2,\"datatype_data\":null},"
        + "{\"name\":\"Port\",\"type\":\"number\",\"ref\":\"[0x12]\",\"connected_to\":[],\"color\":\"green\",\"current_data\":3,\"datatype_data\":null}],"
        + "\"output_ports\":[{\"name\":\"Output\",\"type\":\"number\",\"ref\":\"[0x13]\",\"color\":\"green\",\"datatype_data\":null}]},"
        + "null,"
        + "{\"name\":\"To String\",\"x\":-40,\"y\":10,\"removable\":0,\"color\":\"blue\",\"category\":\"String\",\"ui_alerts\":{\"lock\":\"Unremovable\"},\"ui_buttons\":null,"
        + "\"input_ports\":[{\"name\":\"Input\",\"type\":\"any\",\"ref\":\"[0x20]\",\"connected_to\":[],\"color\":\"blue\",\"current_data\":null,\"datatype_data\":null}],"
        + "\"output_ports\":[{\"name\":\"Output\",\"type\":\"string\",\"ref\":\"[0x21]\",\"color\":\"orange\",\"datatype_data\":null}]}],"
        + "\"variables\":[{\"name\":\"count\",\"datatype\":\"number\",\"color\":\"green\"},{\"name\":\"things\",\"datatype\":\"list\",\"color\":\"white\",\"is_list\":1}],"
        + "\"display_name\":\"Calculator\",\"examined_name\":null,\"grid_mode\":1,\"is_admin\":0,\"screen_x\":20,\"screen_y\":30,"
        + "\"global_basic_types\":[\"any\",\"string\",\"number\"],"
        + "\"circuit_components\":[{\"name\":\"Random\",\"description\":\"rand\",\"category\":\"Math\",\"type\":\"/obj/item/circuit_component/random\",\"input_ports\":[{\"name\":\"Minimum\",\"type\":\"number\",\"color\":\"green\"}],\"output_ports\":[{\"name\":\"Output\",\"type\":\"number\",\"color\":\"green\"}]},"
        + "{\"name\":\"Clock\",\"description\":\"tick\",\"category\":\"Utility\",\"type\":\"/obj/item/circuit_component/clock\",\"input_ports\":[],\"output_ports\":[]}],"
        + "\"stored_designs\":{\"/obj/item/circuit_component/random\":\"/datum/design/component/math/random\"}}";

    [Fact]
    public void TheEditorDrawsTheComponentsAsNodesWithTheirPortsAndWires()
    {
        Assert.NotNull(TguiInterfaces.Find("IntegratedCircuit"));
        var (context, tree, acts) = Render("IntegratedCircuit", IntegratedCircuit.Render, Circuit);
        Assert.Equal((1200.0, 800.0), (context.WindowWidth, context.WindowHeight));

        UiNode plane = tree.Walk().Single(n => n.Kind == UiKind.NodeGraph);
        Assert.Equal((20.0, 30.0), (plane.PosX, plane.PosY));
        Assert.True(plane.Selected); // grid mode
        Assert.Equal(2, plane.Children.Count); // the null (hidden) component is no node
        UiNode arithmetic = plane.Children[0];
        Assert.Equal(UiKind.GraphNode, arithmetic.Kind);
        Assert.Equal("Arithmetic", arithmetic.Text);
        Assert.Equal("#3cc62a", arithmetic.Background); // tg's Math category colour
        Assert.Equal((100.0, 50.0), (arithmetic.PosX, arithmetic.PosY));
        Assert.Equal(3, arithmetic.Children.Count); // three rows: three inputs, the output on the first
        Assert.Equal(("white", "green"), (arithmetic.Children[0].PortIn, arithmetic.Children[0].PortOut));
        Assert.Equal(("green", null), (arithmetic.Children[2].PortIn, arithmetic.Children[2].PortOut));
        UiNode toString = plane.Children[1];
        Assert.Equal("To String", toString.Text);
        Assert.DoesNotContain(toString.Walk(), n => n.Key == "detach"); // unremovable
        Assert.Equal("Unremovable", ByKey(toString, "alert-lock").Tooltip);

        // the wire: To String's output (node 1, port 0) to the arithmetic's second input (node 0, port 1), in the output's colour
        Assert.Equal(new[] { (1, 0, 0, 1, "orange") }, plane.GraphLinks);

        // the inputs: an option dropdown, a wired input's name only, a free number input with its clear button; any's upload and field
        UiNode option = arithmetic.Children[0].Walk().First(n => n.Kind == UiKind.Dropdown);
        Assert.Equal(new[] { "Add", "Subtract" }, option.Options);
        Assert.Equal("Add", option.TextValue);
        Assert.Equal("arithmetic option", ByKey(arithmetic.Children[0], "type").Text);
        Assert.DoesNotContain(arithmetic.Children[1].Walk(), n => n.Kind == UiKind.NumberInput);
        UiNode number = arithmetic.Children[2].Walk().First(n => n.Kind == UiKind.NumberInput);
        Assert.Equal(3, number.Value);
        Assert.Contains(toString.Walk(), n => n.Key == "default" && n.Text == "Input"); // any, null: the name button sets the default

        option.OnSelected("Subtract");
        number.OnChange(7);
        ByKey(arithmetic.Children[2], "set_null").OnClick();
        ByKey(toString, "default").OnClick();
        ByKey(arithmetic.Children[0], "value").OnClick(); // the output's name: its value
        ByKey(arithmetic, "button-plus").OnClick();
        ByKey(arithmetic, "detach").OnClick();
        ByKey(arithmetic, "info").OnClick();
        plane.OnConnect(0, 0, 1, 0);
        plane.OnDisconnect(1, 0, 0, 1);
        arithmetic.OnPortRightClick(false, 0);
        arithmetic.OnMoved(103, 47);
        Assert.Equal(new (string, string)[]
        {
            ("set_component_input", "{\"component_id\":1,\"port_id\":1,\"input\":\"Subtract\"}"),
            ("set_component_input", "{\"component_id\":1,\"port_id\":3,\"input\":7}"),
            ("set_component_input", "{\"component_id\":1,\"port_id\":3,\"input\":null,\"set_null\":true}"),
            ("set_component_input", "{\"component_id\":3,\"port_id\":1,\"input\":\"\"}"),
            ("get_component_value", "{\"component_id\":1,\"port_id\":1}"),
            ("perform_action", "{\"component_id\":1,\"action_name\":\"add\"}"),
            ("detach_component", "{\"component_id\":1}"),
            ("set_examined_component", "{\"component_id\":1,\"x\":0,\"y\":0}"),
            ("add_connection", "{\"input_port_id\":1,\"output_port_id\":1,\"input_component_id\":3,\"output_component_id\":1}"),
            ("remove_connection", "{\"component_id\":1,\"is_input\":true,\"port_id\":2}"),
            ("remove_connection", "{\"component_id\":1,\"is_input\":false,\"port_id\":1}"),
            ("set_component_coordinates", "{\"component_id\":1,\"rel_x\":100,\"rel_y\":50}"), // rounded to the grid
        }, acts);
    }

    [Fact]
    public void TheEditorsTitleRowAndMenusSendTheirActs()
    {
        var local = new TguiLocalState();
        var (_, tree, acts) = Render("IntegratedCircuit", IntegratedCircuit.Render, Circuit, local);
        Assert.Equal("Calculator", ByKey(tree, "display_name").TextValue);
        ByKey(tree, "display_name").OnCommit("Adder");
        ByKey(tree, "toggle_grid_mode").OnClick();
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "save_circuit"); // not an admin
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "variable_options");

        // the menus open in the window's own state
        ByKey(tree, "toggle_variable_menu").OnClick();
        ByKey(tree, "toggle_component_menu").OnClick();
        (_, tree, _) = Render("IntegratedCircuit", IntegratedCircuit.Render, Circuit, local, acts);
        UiNode variables = ByKey(tree, "variable_options");
        Assert.True(ByKey(ByKey(variables, "var-things"), "setter").Disabled); // a list variable has no setter
        ByKey(ByKey(variables, "var-count"), "setter").OnClick();
        ByKey(ByKey(variables, "var-count"), "getter").OnClick();
        ByKey(ByKey(variables, "var-count"), "remove").OnClick();
        ByKey(variables, "variable_name").OnInput("speed");
        (_, tree, _) = Render("IntegratedCircuit", IntegratedCircuit.Render, Circuit, local, acts);
        ByKey(ByKey(tree, "variable_options"), "add_list").OnClick();

        // the component menu lists the printer's designs only (not an admin): Random, not Clock; a click prints it near the corner
        UiNode menu = ByKey(tree, "component_menu");
        Assert.Contains("Random", menu.AllText());
        Assert.DoesNotContain("Clock", menu.AllText());
        Assert.Equal(new[] { "All", "Math" }, ByKey(menu, "category").Options);
        ByKey(menu, "print").OnClick();
        // a card dragged onto the plane lands where it is dropped (tg's handleComponentDropped), a getter too
        ByKey(menu, "print").OnDragStart();
        tree.Walk().Single(n => n.Kind == UiKind.NodeGraph).OnDropAt(200, 150);
        ByKey(ByKey(tree, "var-count"), "getter").OnDragStart();
        tree.Walk().Single(n => n.Kind == UiKind.NodeGraph).OnDropAt(10, 20);
        Assert.Equal(new (string, string)[]
        {
            ("set_display_name", "{\"display_name\":\"Adder\"}"),
            ("toggle_grid_mode", null),
            ("add_setter_or_getter", "{\"variable\":\"count\",\"is_setter\":true,\"rel_x\":40,\"rel_y\":30}"),
            ("add_setter_or_getter", "{\"variable\":\"count\",\"is_setter\":false,\"rel_x\":40,\"rel_y\":30}"),
            ("remove_variable", "{\"variable_name\":\"count\"}"),
            ("add_variable", "{\"variable_name\":\"speed\",\"variable_datatype\":\"any\",\"is_list\":true,\"is_assoc_list\":false}"),
            ("print_component", "{\"component_to_print\":\"/obj/item/circuit_component/random\",\"rel_x\":40,\"rel_y\":30}"),
            ("print_component", "{\"component_to_print\":\"/obj/item/circuit_component/random\",\"rel_x\":200,\"rel_y\":150}"),
            ("add_setter_or_getter", "{\"variable\":\"count\",\"is_setter\":false,\"rel_x\":10,\"rel_y\":20}"),
        }, acts);

        // an admin sees every component and the save button; a pan sends move_screen and keeps the new offset
        var (_, admin, adminActs) = Render("IntegratedCircuit", IntegratedCircuit.Render, Circuit.Replace("\"is_admin\":0", "\"is_admin\":1"), local);
        Assert.Contains("Clock", ByKey(admin, "component_menu").AllText());
        ByKey(admin, "save_circuit").OnClick();
        admin.Walk().Single(n => n.Kind == UiKind.NodeGraph).OnMoved(-5, 12);
        Assert.Equal(new (string, string)[] { ("save_circuit", null), ("move_screen", "{\"screen_x\":-5,\"screen_y\":12}") }, adminActs);
        (_, tree, _) = Render("IntegratedCircuit", IntegratedCircuit.Render, Circuit, local);
        Assert.Equal((-5.0, 12.0), (tree.Walk().Single(n => n.Kind == UiKind.NodeGraph).PosX, tree.Walk().Single(n => n.Kind == UiKind.NodeGraph).PosY));
    }

    [Fact]
    public void TheEditorShowsTheExaminedComponentAndAnEmptyMenusNotice()
    {
        string examined = Circuit
            .Replace("\"examined_name\":null", "\"examined_name\":\"Arithmetic\",\"examined_desc\":\"General arithmetic.\",\"examined_notices\":[{\"icon\":\"bolt\",\"content\":\"Energy Usage Per Input: 1 J\",\"color\":\"orange\"}]")
            .Replace("\"stored_designs\":{\"/obj/item/circuit_component/random\":\"/datum/design/component/math/random\"}", "\"stored_designs\":{}");
        var local = new TguiLocalState();
        local.Set("componentMenuOpen", true);
        var (_, tree, acts) = Render("IntegratedCircuit", IntegratedCircuit.Render, examined, local);
        UiNode info = ByKey(tree, "examined");
        Assert.Equal("Arithmetic", info.Title);
        Assert.Contains("General arithmetic.", info.AllText());
        Assert.Equal("Energy Usage Per Input: 1 J", ByKey(info, "notice0").Text);
        Assert.Contains("You can hit this integrated circuit onto a component printer", ByKey(tree, "notice").Text);
        ByKey(tree, "close_examined").OnClick();
        Assert.Equal(new (string, string)[] { ("remove_examined_component", null) }, acts);
    }

    [Fact]
    public void TheComponentPrinterPrintsDesignsAndEjectsMaterials()
    {
        Assert.NotNull(TguiInterfaces.Find("ComponentPrinter"));
        var (context, tree, acts) = Render("ComponentPrinter", ComponentPrinter.Render,
            "{\"SHEET_MATERIAL_AMOUNT\":100,\"materials\":[{\"name\":\"iron\",\"ref\":\"[0x5]\",\"amount\":500,\"color\":\"#878687\"}],"
            + "\"designs\":{\"/datum/design/component/math/arithmetic\":{\"name\":\"Arithmetic Component\",\"desc\":\"Adds.\",\"cost\":{\"iron\":50},"
            + "\"path\":\"/datum/design/component/math/arithmetic\",\"categories\":[\"/Circuitry/Math Components\"],\"icon\":\"arithmetic\"},"
            + "\"/datum/design/component/utility/clock\":{\"name\":\"Clock Component\",\"desc\":\"Ticks.\",\"cost\":{\"iron\":5000},"
            + "\"path\":\"/datum/design/component/utility/clock\",\"categories\":[\"/Circuitry/Utility Components\"],\"icon\":\"clock\"}}}");
        Assert.Equal((670.0, 600.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Component Printer", context.WindowTitle);
        UiNode arithmetic = ByKey(tree, "recipe:/datum/design/component/math/arithmetic");
        UiNode clock = ByKey(tree, "recipe:/datum/design/component/utility/clock");
        Assert.False(ByKey(arithmetic, "print").Disabled);
        Assert.True(ByKey(clock, "print").Disabled); // 5000 iron, 500 there
        ByKey(arithmetic, "print").OnClick();
        ByKey(clock, "print").OnClick();
        Assert.Equal(new (string, string)[] { ("print", "{\"design_path\":\"/datum/design/component/math/arithmetic\",\"amount\":1}") }, acts);

        // the debug printer prints anything, under its own title; the module duplicator's designs print by id
        var (debugContext, debug, debugActs) = Render("ComponentPrinter", ComponentPrinter.Render,
            "{\"debug\":1,\"SHEET_MATERIAL_AMOUNT\":100,\"materials\":[],\"designs\":{\"1\":{\"name\":\"Saved\",\"desc\":\"d\",\"cost\":{\"glass\":100},\"id\":\"1\",\"categories\":[\"/Saved Circuits\"],\"icon\":\"integrated_circuit\"}}}");
        Assert.Equal("Debug Component Printer", debugContext.WindowTitle);
        ByKey(debug, "print").OnClick();
        Assert.Equal(new (string, string)[] { ("print", "{\"design_path\":\"1\",\"amount\":1}") }, debugActs);
    }

    [Fact]
    public void TheCircuitModuleEditsItsPorts()
    {
        Assert.NotNull(TguiInterfaces.Find("CircuitModule"));
        var (context, tree, acts) = Render("CircuitModule", CircuitModule.Render,
            "{\"input_ports\":[{\"name\":\"Input Port\",\"type\":\"any\"}],\"output_ports\":[],\"global_port_types\":[\"any\",\"number\",\"string\"]}");
        Assert.Equal((600.0, 300.0), (context.WindowWidth, context.WindowHeight));
        ByKey(tree, "open_internal_circuit").OnClick();
        UiNode port = ByKey(tree, "in1");
        ByKey(port, "name").OnCommit("Speed");
        ByKey(port, "type").OnSelected("number");
        ByKey(port, "remove").OnClick();
        ByKey(tree, "add_output_port").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("open_internal_circuit", null),
            ("set_port_name", "{\"port_id\":1,\"is_input\":true,\"port_name\":\"Speed\"}"),
            ("set_port_type", "{\"port_id\":1,\"is_input\":true,\"port_type\":\"number\"}"),
            ("remove_input_port", "{\"port_id\":1}"),
            ("add_output_port", null),
        }, acts);
    }

    [Fact]
    public void TheCircuitAdminPanelListsTheCircuits()
    {
        Assert.NotNull(TguiInterfaces.Find("CircuitAdminPanel"));
        var (context, tree, acts) = Render("CircuitAdminPanel", CircuitAdminPanel.Render,
            "{\"circuits\":[{\"ref\":\"[0x9]\",\"name\":\"Calculator\",\"creator\":\"Bob\",\"has_inserter\":1},{\"ref\":\"[0xa]\",\"name\":\"Clock\",\"creator\":\"\",\"has_inserter\":0}]}");
        Assert.Equal((1200.0, 500.0, "Circuit Admin Panel"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));
        Assert.Contains("Calculator", tree.AllText());
        UiNode first = ByKey(tree, "[0x9]");
        Assert.Contains(first.Walk(), n => n.Key == "open_player_panel");
        Assert.DoesNotContain(ByKey(tree, "[0xa]").Walk(), n => n.Key == "open_player_panel");
        ByKey(first, "open_circuit").OnClick();
        ByKey(tree, "disable_circuit_sound").OnClick();
        Assert.Equal(new (string, string)[] { ("open_circuit", "{\"circuit\":\"[0x9]\"}"), ("disable_circuit_sound", null) }, acts);
    }
}
