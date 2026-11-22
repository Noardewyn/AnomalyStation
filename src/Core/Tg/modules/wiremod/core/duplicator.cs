// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/core/duplicator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, a circuit to JSON and back (the module duplicator, the admins' duplicate and load). The
// Load Circuit admin verb's body is the console's load-circuit (D275: the JSON comes as the command's text; tg asks for a file or
// a direct input). DV569: a component whose type is not live is not loaded (an error line, as for an unknown type).

public partial class datum_controller_global_vars
{
    // Determines if a port can have a predefined input value if it is of this type.
    // tg: code/modules/wiremod/core/duplicator.dm:4  GLOBAL_LIST_INIT(circuit_dupe_whitelisted_types, list(...))
    public DmList circuit_dupe_whitelisted_types;

    // tg: code/modules/wiremod/core/duplicator.dm:4  (GLOBAL_MANAGED(circuit_dupe_whitelisted_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcircuit_dupe_whitelisted_types()
    {
        circuit_dupe_whitelisted_types = Dm.list(
            PORT_TYPE_NUMBER,
            PORT_TYPE_STRING,
            PORT_TYPE_ANY,
            PORT_TYPE_OPTION);
        Dm.append(ref gvars_datum_init_order, "circuit_dupe_whitelisted_types");
    }
}

// tg: code/modules/wiremod/core/duplicator.dm:12
public partial class obj_item_integrated_circuit : obj_item
{
    // tg: code/modules/wiremod/core/duplicator.dm:1  #define LOG_ERROR(list, error) if(list) { list.Add(error) } (#undef at :127)
    private static void LOG_ERROR(DmList list, object error)
    {
        if (Dm.truthy(list))
            list.Add(error);
    }

    /// Loads a circuit based on json data at a location. Can also load usb connections, such as arrest consoles.
    // tg: code/modules/wiremod/core/duplicator.dm:12
    public virtual async DmTask load_circuit_data(object json_data = null, DmList errors = null)
    {
        DmList general_data = Dm.json_decode(json_data) as DmList;

        if (!Dm.truthy(general_data))
        {
            LOG_ERROR(errors, "Invalid json format!");
            return;
        }

        DmList variable_data = general_data["variables"] as DmList;
        foreach (DmList variable in Dm.each_cast<DmList>(variable_data))
        {
            object variable_name = variable["name"];
            datum_circuit_variable variable_datum = Dm.@new<datum_circuit_variable>(variable_name, variable["datatype"]);
            circuit_variables[variable_name] = variable_datum;
            if (Dm.truthy(variable["is_assoc_list"]))
            {
                assoc_list_variables[variable_name] = variable_datum;
                variable_datum.set_value(Dm.list());
            }
            else if (Dm.truthy(variable["is_list"]))
            {
                list_variables[variable_name] = variable_datum;
                variable_datum.set_value(Dm.list());
            }
            else
                modifiable_circuit_variables[variable_name] = variable_datum;
        }

        admin_only = Dm.num(general_data["admin_only"]);

        if (Dm.truthy(general_data["display_name"]))
            set_display_name(general_data["display_name"]);

        DmList circuit_data = general_data["components"] as DmList;
        DmList identifiers_to_circuit = Dm.list();
        foreach (object identifier in Dm.each(circuit_data))
        {
            DmList component_data = circuit_data[identifier] as DmList;
            object type = text2path(component_data["type"] as string);
            if (!ispath(type, typeof(obj_item_circuit_component)))
            {
                LOG_ERROR(errors, Dm.text("Invalid path for circuit component, expected [], got []", typeof(obj_item_circuit_component), type));
                continue;
            }
            // DEVIATION(DV569): a component whose type is not live is not made (its ports would be missing): an error line instead
            if (InertSeam.IsInertType((Type)type))
            {
                LOG_ERROR(errors, Dm.text("[] is not ported yet", type));
                continue;
            }
            obj_item_circuit_component component = await load_component(type) as obj_item_circuit_component;
            identifiers_to_circuit[identifier] = component;
            await component.load_data_from_list(component_data);
            component.SEND_SIGNAL(COMSIG_CIRCUIT_COMPONENT_LOAD_DATA, Dm.list(component, component_data));

            DmList input_ports_data = component_data["input_ports_stored_data"] as DmList;
            foreach (object port_name in Dm.each(input_ports_data))
            {
                datum_port_input port = null;
                DmList port_data = input_ports_data[port_name] as DmList;
                foreach (datum_port_input port_to_check in Dm.each_cast<datum_port_input>(component.input_ports))
                {
                    if (Dm.eq(port_to_check.name, port_name))
                    {
                        port = port_to_check;
                        break;
                    }
                }

                if (port == null)
                {
                    LOG_ERROR(errors, Dm.text("Port '[]' not found on [] when trying to set it to a value of []!", port_name, component.type, port_data["stored_data"]));
                    continue;
                }

                port.set_input(port_data["stored_data"]);
            }
        }

        DmList external_objects = general_data["external_objects"] as DmList;
        foreach (object identifier in Dm.each(external_objects))
        {
            DmList object_data = external_objects[identifier] as DmList;
            object type = text2path(object_data["type"] as string);
            if (!ispath(type))
            {
                LOG_ERROR(errors, Dm.text("Invalid path for external object, expected a path, got []", type));
                continue;
            }
            atom_movable @object = await Dm.new_async((Type)type, drop_location()) as atom_movable;
            DmList connected_components = Dm.list();
            foreach (object component_id in Dm.each(object_data["connected_components"]))
            {
                obj_item_circuit_component component = identifiers_to_circuit[component_id] as obj_item_circuit_component;
                if (component == null)
                    continue;
                Dm.append(connected_components, component);
            }
            @object.SEND_SIGNAL(COMSIG_MOVABLE_CIRCUIT_LOADED, Dm.list(@object, this, connected_components));
        }

        foreach (object identifier in Dm.each(identifiers_to_circuit))
        {
            obj_item_circuit_component component = identifiers_to_circuit[identifier] as obj_item_circuit_component;
            DmList component_data = circuit_data[identifier] as DmList;

            DmList connections = component_data["connections"] as DmList;
            foreach (object port_name in Dm.each(connections))
            {
                datum_port_input port = null;
                DmList connection_data = connections[port_name] as DmList;
                foreach (datum_port_input port_to_check in Dm.each_cast<datum_port_input>(component.input_ports))
                {
                    if (Dm.eq(port_to_check.name, port_name))
                    {
                        port = port_to_check;
                        break;
                    }
                }

                if (port == null)
                {
                    LOG_ERROR(errors, Dm.text("Port [] not found for [].", port_name, component.type));
                    continue;
                }

                if (Dm.truthy(connection_data["stored_data"]))
                {
                    if (!Dm.@in(port.datatype, GLOB.circuit_dupe_whitelisted_types))
                        continue;
                    port.set_input(connection_data["stored_data"]);
                    continue;
                }

                // The || list(connected_data) is for backwards compatibility with when inputs could only be connected to up to one output.
                foreach (DmList output_data in Dm.each<DmList>(Dm.truthy(connection_data["connected_ports"]) ? connection_data["connected_ports"] : Dm.list(connection_data)))
                {
                    obj_item_circuit_component connected_component = identifiers_to_circuit[output_data["component_id"]] as obj_item_circuit_component;
                    if (connected_component == null)
                    {
                        LOG_ERROR(errors, Dm.text("No connected component found for [] for port []. (connected component identifier: [])", component.type, connection_data["port_name"], connection_data["component_id"]));
                        continue;
                    }

                    datum_port_output output_port = null;
                    object output_port_name = output_data["port_name"];
                    foreach (datum_port_output port_to_check in Dm.each_cast<datum_port_output>(connected_component.output_ports))
                    {
                        if (Dm.eq(port_to_check.name, output_port_name))
                        {
                            output_port = port_to_check;
                            break;
                        }
                    }

                    if (output_port == null)
                    {
                        LOG_ERROR(errors, Dm.text("No output port found for [] for port [] on component []", component.type, output_port_name, connected_component.type));
                        continue;
                    }

                    port.connect(output_port);
                }
            }
        }

        SEND_SIGNAL(COMSIG_CIRCUIT_POST_LOAD, Dm.list(this));
    }

    /// Converts a circuit into json.
    // tg: code/modules/wiremod/core/duplicator.dm:130
    public virtual object convert_to_json()
    {
        DmList circuit_to_identifiers = Dm.list();
        DmList identifiers = Dm.list();
        DmList external_objects = Dm.list(); // Objects that are connected to a component. These objects will be linked to the components.
        foreach (obj_item_circuit_component component in Dm.each_cast<obj_item_circuit_component>(attached_components))
        {
            string identifier = Dm.text("[][]", component.type, length(identifiers));
            Dm.append(identifiers, identifier);
            circuit_to_identifiers[component] = identifier;
            DmList objects = Dm.list();
            component.SEND_SIGNAL(COMSIG_CIRCUIT_COMPONENT_SAVE, Dm.list(component, objects));

            foreach (atom_movable @object in Dm.each_cast<atom_movable>(objects))
            {
                if (Dm.@in(@object, external_objects))
                {
                    Dm.append(external_objects[@object] as DmList, identifier);
                    continue;
                }
                external_objects[@object] = Dm.list(identifier);
            }
        }

        DmList circuit_data = Dm.list();
        foreach (obj_item_circuit_component component in Dm.each_cast<obj_item_circuit_component>(circuit_to_identifiers))
        {
            object identifier = circuit_to_identifiers[component];
            DmList component_data = Dm.list();

            component_data["type"] = component.type;

            DmList connections = Dm.list();
            DmList input_ports_stored_data = Dm.list();
            foreach (datum_port_input input in Dm.each_cast<datum_port_input>(component.input_ports))
            {
                DmList connection_data = Dm.list();
                if (!Dm.truthy(LAZYLEN(input.connected_ports)))
                {
                    if (input.value == null || !Dm.@in(input.datatype, GLOB.circuit_dupe_whitelisted_types))
                        continue;
                    connection_data["stored_data"] = input.value;
                    input_ports_stored_data[input.name] = connection_data;
                    continue;
                }
                DmList connected_ports = Dm.list();
                connection_data["connected_ports"] = connected_ports;
                foreach (datum_port_output output in Dm.each_cast<datum_port_output>(input.connected_ports))
                {
                    Dm.append(connected_ports, Dm.list(Dm.list(
                        Dm.kv("component_id", circuit_to_identifiers[output.connected_component]),
                        Dm.kv("port_name", output.name))));
                }
                connections[input.name] = connection_data;
            }
            component_data["connections"] = connections;
            component_data["input_ports_stored_data"] = input_ports_stored_data;

            component.SEND_SIGNAL(COMSIG_CIRCUIT_COMPONENT_SAVE_DATA, Dm.list(component, component_data));
            component.save_data_to_list(component_data);
            circuit_data[identifier] = component_data;
        }

        DmList external_objects_key = Dm.list();
        foreach (atom_movable @object in Dm.each_cast<atom_movable>(external_objects))
        {
            DmList new_data = Dm.list();
            new_data["type"] = @object.type;
            new_data["connected_components"] = external_objects[@object];
            external_objects_key[Dm.text("[][]", @object.type, length(external_objects_key))] = new_data;
        }

        DmList general_data = Dm.list();
        general_data["components"] = circuit_data;
        general_data["external_objects"] = external_objects_key;
        general_data["display_name"] = display_name;
        general_data["admin_only"] = admin_only;

        DmList variables = Dm.list();
        foreach (object variable_identifier in Dm.each(circuit_variables))
        {
            DmList new_data = Dm.list();
            datum_circuit_variable variable = circuit_variables[variable_identifier] as datum_circuit_variable;
            new_data["name"] = variable.name;
            new_data["datatype"] = variable.datatype;
            if (Dm.@in(variable_identifier, assoc_list_variables))
                new_data["is_assoc_list"] = TRUE;
            else if (Dm.@in(variable_identifier, list_variables))
                new_data["is_list"] = TRUE;
            Dm.append(variables, Dm.list(new_data));
        }
        general_data["variables"] = variables;

        SEND_SIGNAL(COMSIG_CIRCUIT_PRE_SAVE_TO_JSON, Dm.list(this, general_data));

        return Dm.json_encode(general_data);
    }

    // tg: code/modules/wiremod/core/duplicator.dm:208
    public virtual async DmTask<object> load_component(object type = null)
    {
        obj_item_circuit_component component = await Dm.new_async((Type)type, this) as obj_item_circuit_component;
        await add_component(component);
        return component;
    }
}

// tg: code/modules/wiremod/core/duplicator.dm:214
public partial class obj_item_circuit_component : obj_item
{
    /// Saves data to a list. Shouldn't be used unless you are quite literally saving the data of a component to a list. Input value is the list to save the data to
    // tg: code/modules/wiremod/core/duplicator.dm:214
    public virtual void save_data_to_list(DmList component_data = null)
    {
        component_data["rel_x"] = rel_x;
        component_data["rel_y"] = rel_y;
    }

    /// Loads data from a list
    // tg: code/modules/wiremod/core/duplicator.dm:219
    public virtual async DmTask load_data_from_list(DmList component_data = null)
    {
        rel_x = Dm.num(component_data["rel_x"]);
        rel_y = Dm.num(component_data["rel_y"]);
    }
}

// tg: code/modules/wiremod/core/duplicator.dm:223
public partial class client : datum
{
    // ADMIN_VERB(load_circuit, R_VAREDIT, "Load Circuit", "Loads a circuit from a file or direct input.", ADMIN_CATEGORY_FUN): the
    // verb's body, which the admin console's load-circuit runs with the JSON it was typed (D275; the verb's rights are the console's;
    // tg's alert asks for a file or a direct input, the console's text is the direct input)
    // tg: code/modules/wiremod/core/duplicator.dm:223
    public async DmTask load_circuit(string txt)
    {
        DmList errors = Dm.list();

        if (!Dm.truthy(txt))
            return;

        obj_item_integrated_circuit_loaded circuit = await Dm.new_async<obj_item_integrated_circuit_loaded>(mob.drop_location());
        await circuit.load_circuit_data(txt, errors);

        if (Dm.truthy(length(errors)))
        {
            to_chat(this, span_warning(I18n.T("The following errors were found whilst compiling the circuit data:")));
            foreach (object error in Dm.each(errors))
                to_chat(this, span_warning(Dm.str(error)));
        }
    }
}
