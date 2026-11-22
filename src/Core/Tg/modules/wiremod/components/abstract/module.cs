// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/abstract/module.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the module component (a circuit inside a component, its input and output components
// linked to the module's ports) and its CircuitModule window. The var blocks are generated (D111).

/**
 * # Module Component
 *
 * A component that has an input, output
 */
// tg: code/modules/wiremod/components/abstract/module.dm:6
public partial class obj_item_circuit_component_module : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/abstract/module.dm:99
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == null)
            return;
        datum_port_output port_to_update = linked_ports[port] as datum_port_output;
        if (port_to_update == null)
            throw Dm.CRASH(Dm.text("[] doesn't have a linked port in []!", port.type, type), "code/modules/wiremod/components/abstract/module.dm", 104);

        port_to_update.set_output(port.value);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:112
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        internal_circuit = Dm.@new<obj_item_integrated_circuit_module>(this);
        internal_circuit.attached_module = this;

        input_component = Dm.@new<obj_item_circuit_component_module_input>(internal_circuit);
        input_component.attached_module = this;
        Waiting.NoSleep(internal_circuit.add_component(input_component));
        input_component.rel_x = 0;
        input_component.rel_y = 200;

        output_component = Dm.@new<obj_item_circuit_component_module_output>(internal_circuit);
        output_component.attached_module = this;
        Waiting.NoSleep(internal_circuit.add_component(output_component));
        output_component.rel_x = 400;
        output_component.rel_y = 200;
        return dot;
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:129
    public override void save_data_to_list(DmList component_data = null)
    {
        base.save_data_to_list(component_data);
        component_data["integrated_circuit"] = internal_circuit.convert_to_json();

        DmList input_data = Dm.list();
        foreach (datum_port_input input_port in Dm.each_cast<datum_port_input>(input_ports))
        {
            Dm.append(input_data, Dm.list(Dm.list(
                Dm.kv("name", input_port.name),
                Dm.kv("type", input_port.datatype))));
        }

        DmList output_data = Dm.list();
        foreach (datum_port_output output_port in Dm.each_cast<datum_port_output>(output_ports))
        {
            Dm.append(output_data, Dm.list(Dm.list(
                Dm.kv("name", output_port.name),
                Dm.kv("type", output_port.datatype))));
        }

        component_data["input_ports"] = input_data;
        component_data["output_ports"] = output_data;
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:150
    public override async DmTask load_data_from_list(DmList component_data = null)
    {
        await base.load_data_from_list(component_data);

        DmList input_ports = component_data["input_ports"] as DmList;
        foreach (DmList port_data in Dm.each_cast<DmList>(input_ports))
            add_and_link_input_port(port_data["name"], port_data["type"]);

        DmList output_ports = component_data["output_ports"] as DmList;
        foreach (DmList port_data in Dm.each_cast<DmList>(output_ports))
            add_and_link_output_port(port_data["name"], port_data["type"]);

        if (Dm.truthy(component_data["integrated_circuit"]))
            await internal_circuit.load_circuit_data(component_data["integrated_circuit"]);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:164
    public virtual void add_and_link_input_port(object name = null, object type = null)
    {
        datum_port new_port = add_input_port(name, type) as datum_port;
        linked_ports[new_port] = input_component.add_output_port(name, type);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:168
    public virtual void add_and_link_output_port(object name = null, object type = null)
    {
        datum_port new_port = output_component.add_input_port(name, type) as datum_port;
        linked_ports[new_port] = add_output_port(name, type);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:172
    public override async DmTask<object> add_to(obj_item_integrated_circuit added_to = null)
    {
        object dot = await base.add_to(added_to);
        RegisterSignal(added_to, COMSIG_CIRCUIT_SET_CELL, nameof(handle_set_cell));
        RegisterSignal(added_to, COMSIG_CIRCUIT_SET_ON, nameof(handle_set_on));
        RegisterSignal(added_to, COMSIG_CIRCUIT_SET_SHELL, nameof(handle_set_shell));
        internal_circuit.set_cell(added_to.cell);
        await internal_circuit.set_shell(added_to.shell);
        internal_circuit.set_on(added_to.on);
        return dot;
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:182
    public override void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        internal_circuit.set_cell(null);
        internal_circuit.set_on(FALSE);
        internal_circuit.remove_current_shell();
        UnregisterSignal(removed_from, Dm.list(
            COMSIG_CIRCUIT_SET_CELL,
            COMSIG_CIRCUIT_SET_ON,
            COMSIG_CIRCUIT_SET_SHELL));
        base.removed_from(removed_from);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:193
    [SignalHandler]
    public virtual void handle_set_cell(datum source = null, obj_item_stock_parts_power_store_cell cell = null)
    {
        internal_circuit.set_cell(cell);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:197
    [SignalHandler]
    public virtual void handle_set_on(datum source = null, object new_value = null)
    {
        internal_circuit.set_on(new_value);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:201
    [SignalHandler]
    public virtual void handle_set_shell(datum source = null, atom_movable new_shell = null)
    {
        Waiting.NoSleep(internal_circuit.set_shell(new_shell));
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:205
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref input_component);
        QDEL_NULL(ref output_component);
        QDEL_NULL(ref internal_circuit);
        linked_ports = null;
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:212
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        DmList input_ports_data = Dm.list();
        dot["input_ports"] = input_ports_data;
        foreach (datum_port_input input_port in Dm.each_cast<datum_port_input>(input_ports))
        {
            Dm.append(input_ports_data, Dm.list(Dm.list(
                Dm.kv("name", input_port.name),
                Dm.kv("type", input_port.datatype))));
        }

        DmList output_ports_data = Dm.list();
        dot["output_ports"] = output_ports_data;
        foreach (datum_port_output output_port in Dm.each_cast<datum_port_output>(output_ports))
        {
            Dm.append(output_ports_data, Dm.list(Dm.list(
                Dm.kv("name", output_port.name),
                Dm.kv("type", output_port.datatype))));
        }
        return dot;
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:228
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["global_port_types"] = GLOB.wiremod_basic_types;
        return dot;
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:232
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_circuit_component)))
            return NONE;
        await internal_circuit.item_interaction(user, tool, modifiers);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:238  #define WITHIN_RANGE(id, table) (id >= 1 && id <= length(table)) (#undef at :315)
    private static bool WITHIN_RANGE(object id, object table) => Dm.ge(id, 1) && Dm.le(id, length(table));

    // tg: code/modules/wiremod/components/abstract/module.dm:240
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "open_internal_circuit":
                await internal_circuit.interact(usr);
                dot = TRUE;
                break;
            case "add_input_port":
                if (length(input_ports) > port_limit)
                    return null;
                add_and_link_input_port("Input Port", PORT_TYPE_ANY);
                dot = TRUE;
                break;
            case "remove_input_port":
            {
                object port_id = text2num(@params["port_id"]);
                if (!WITHIN_RANGE(port_id, input_ports))
                    return null;
                datum_port removed_port = input_ports[port_id] as datum_port;
                Dm.remove(linked_ports, removed_port);
                remove_input_port(removed_port as datum_port_input);
                input_component.remove_output_port(input_component.output_ports[port_id] as datum_port_output);
                dot = TRUE;
                break;
            }
            case "add_output_port":
                if (length(output_ports) > port_limit)
                    return null;
                add_and_link_output_port("Output Port", PORT_TYPE_ANY);
                dot = TRUE;
                break;
            case "remove_output_port":
            {
                object port_id = text2num(@params["port_id"]);
                if (!WITHIN_RANGE(port_id, output_ports))
                    return null;

                datum_port removed_port = output_component.input_ports[port_id] as datum_port;
                Dm.remove(linked_ports, removed_port);
                remove_output_port(output_ports[port_id] as datum_port_output);
                output_component.remove_input_port(removed_port as datum_port_input);
                dot = TRUE;
                break;
            }
            case "set_port_name":
            case "set_port_type":
            {
                object port_id = text2num(@params["port_id"]);
                object is_input = @params["is_input"];

                DmList ports_to_use;
                DmList internal_ports_to_use;
                if (Dm.truthy(is_input))
                {
                    ports_to_use = input_ports;
                    internal_ports_to_use = input_component.output_ports;
                }
                else
                {
                    ports_to_use = output_ports;
                    internal_ports_to_use = output_component.input_ports;
                }

                if (!WITHIN_RANGE(port_id, ports_to_use))
                    return null;

                datum_port component_port = ports_to_use[port_id] as datum_port;
                datum_port internal_component_port = internal_ports_to_use[port_id] as datum_port;

                if (Dm.eq(action, "set_port_type"))
                {
                    object port_type = @params["port_type"];
                    if (!Dm.@in(port_type, GLOB.wiremod_basic_types))
                        return null;
                    component_port.set_datatype(port_type);
                    internal_component_port.set_datatype(port_type);
                }
                else
                {
                    object port_name = @params["port_name"];
                    if (!Dm.truthy(port_name))
                        return null;
                    port_name = strip_html(port_name, PORT_MAX_NAME_LENGTH);
                    component_port.name = port_name;
                    internal_component_port.name = port_name;
                }
                dot = TRUE;
                break;
            }
        }

        if (Dm.truthy(dot))
            SStgui?.update_uis(internal_circuit); // rules 10.6: a test harness world may lack SStgui
        return dot;
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:317
    public override async DmTask ui_perform_action(mob user = null, object action = null)
    {
        await interact(user);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:320
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "CircuitModule", name);
            await ui.open();
            ui.set_autoupdate(FALSE);
        }
        return null;
    }
}

// tg: code/modules/wiremod/components/abstract/module.dm:25
public partial class obj_item_integrated_circuit_module : obj_item_integrated_circuit
{
    // tg: code/modules/wiremod/components/abstract/module.dm:28
    public override object ui_host(mob user = null)
    {
        if (attached_module != null)
            return attached_module.ui_host();
        return base.ui_host(user);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:33
    public override void set_display_name(object new_name = null)
    {
        base.set_display_name(new_name);
        attached_module.display_name = Dm.str(new_name);
        attached_module.name = Dm.text("module ([])", new_name);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:38
    public override async DmTask<object> load_component(object type = null)
    {
        if (attached_module == null)
            return await base.load_component(type);

        if (ispath(type, typeof(obj_item_circuit_component_module_input)))
            return attached_module.input_component;

        if (ispath(type, typeof(obj_item_circuit_component_module_output)))
            return attached_module.output_component;

        return await base.load_component(type);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:50
    public override async DmTask<object> add_component(obj_item_circuit_component to_add = null, mob_living user = null)
    {
        if (Dm.truthy(Dm.bitand(to_add.circuit_flags, CIRCUIT_FLAG_REFUSE_MODULE)))
        {
            balloon_alert(user, I18n.T("doesn't fit into module!"));
            return null;
        }
        object dot = await base.add_component(to_add, user);
        if (attached_module != null)
            attached_module.circuit_size += to_add.circuit_size;
        return dot;
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:58
    public override async DmTask remove_component(obj_item_circuit_component to_remove = null)
    {
        if (attached_module != null)
            attached_module.circuit_size -= to_remove.circuit_size;
        await base.remove_component(to_remove);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:63
    public override object Destroy(float? force = null)
    {
        attached_module = null;
        return base.Destroy(force);
    }
}

// tg: code/modules/wiremod/components/abstract/module.dm:67
public partial class obj_item_circuit_component_module_input : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/abstract/module.dm:76
    public override object Destroy(float? force = null)
    {
        attached_module = null;
        return base.Destroy(force);
    }
}

// tg: code/modules/wiremod/components/abstract/module.dm:80
public partial class obj_item_circuit_component_module_output : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/abstract/module.dm:89
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == null)
            return;
        // We don't check the parent here because frankly, we don't care. We only sync our input with the module's output
        datum_port_output port_to_update = attached_module.linked_ports[port] as datum_port_output;
        if (port_to_update == null)
            throw Dm.CRASH(Dm.text("[] doesn't have a linked port in []!", port.type, type), "code/modules/wiremod/components/abstract/module.dm", 95);

        port_to_update.set_output(port.value);
    }

    // tg: code/modules/wiremod/components/abstract/module.dm:108
    public override object Destroy(float? force = null)
    {
        attached_module = null;
        return base.Destroy(force);
    }
}
