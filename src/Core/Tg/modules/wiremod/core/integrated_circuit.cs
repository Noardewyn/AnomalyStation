// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/core/integrated_circuit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the integrated circuit: its cell, its components and their wires, its variables, its
// shell, and the IntegratedCircuit window (the editor). DV568: the component menu's list comes with the window's static data
// (tg's page fetches circuit_components.json, an asset; DV44), and lists the live components only. The circuit's download for the
// admins (attempt_save_to: BYOND's ftp of a file to the client) is a deferred leaf. The var block is generated (D111).

public partial class datum_controller_global_vars
{
    /// A list of all integrated circuits
    // tg: code/modules/wiremod/core/integrated_circuit.dm:2  GLOBAL_LIST_EMPTY_TYPED(integrated_circuits, /obj/item/integrated_circuit)
    public DmList integrated_circuits;

    // tg: code/modules/wiremod/core/integrated_circuit.dm:2  (GLOBAL_MANAGED(integrated_circuits, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalintegrated_circuits()
    {
        integrated_circuits = Dm.list();
        Dm.append(ref gvars_datum_init_order, "integrated_circuits");
    }
}

/**
 * # Integrated Circuitboard
 *
 * A circuitboard that holds components that work together
 *
 * Has a limited amount of power.
 */
// tg: code/modules/wiremod/core/integrated_circuit.dm:11
public partial class obj_item_integrated_circuit : obj_item
{
    // tg: code/modules/wiremod/core/integrated_circuit.dm:94
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Dm.append(GLOB.integrated_circuits, this);

        RegisterSignal(this, COMSIG_ATOM_USB_CABLE_TRY_ATTACH, nameof(on_atom_usb_cable_try_attach));
        return dot;
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:105
    public override object Destroy(float? force = null)
    {
        foreach (obj_item_circuit_component to_delete in Dm.each<obj_item_circuit_component>(attached_components))
        {
            Waiting.NoSleep(remove_component(to_delete));
            qdel(to_delete);
        }
        QDEL_LIST_ASSOC_VAL(circuit_variables);
        QDEL_LIST_ASSOC_VAL(list_variables);
        attached_components.Cut();
        shell = null;
        examined_component = null;
        owner_id = null;
        QDEL_NULL(ref cell);
        Dm.remove(GLOB.integrated_circuits, this);
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:119
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (cell != null)
            Dm.append(ref dot, span_notice(I18n.T("The charge meter reads []%.", cell != null ? round(Dm.num(cell.percent()), 1) : 0)));
        else
            Dm.append(ref dot, span_notice(I18n.T("There is no power cell installed.")));
        return dot;
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:126
    public override object drop_location()
    {
        if (shell != null)
            return shell.drop_location();
        return base.drop_location();
    }

    /**
     * Sets the cell of the integrated circuit.
     *
     * Arguments:
     * * cell_to_set - The new cell of the circuit. Can be null.
     **/
    // tg: code/modules/wiremod/core/integrated_circuit.dm:137
    public virtual void set_cell(obj_item_stock_parts_power_store cell_to_set = null)
    {
        SEND_SIGNAL(COMSIG_CIRCUIT_SET_CELL, Dm.list(this, cell_to_set));
        cell = cell_to_set;
    }

    /**
     * Sets the locked status of the integrated circuit.
     *
     * Arguments:
     * * new_value - A boolean that determines if the circuit is locked or not.
     **/
    // tg: code/modules/wiremod/core/integrated_circuit.dm:147
    public virtual void set_locked(object new_value = null)
    {
        SEND_SIGNAL(COMSIG_CIRCUIT_SET_LOCKED, Dm.list(this, new_value));
        locked = Dm.num(new_value);
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:151
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_circuit_component)))
            return Dm.truthy(await add_component_manually(tool as obj_item_circuit_component, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;

        if (istype(tool, typeof(obj_item_stock_parts_power_store_cell)))
        {
            if (cell != null)
            {
                balloon_alert(user, I18n.T("there already is a cell inside!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            set_cell(tool as obj_item_stock_parts_power_store);
            tool.add_fingerprint(user);
            user.visible_message(span_notice(I18n.T("[] inserts a power cell into [].", user, this)), span_notice(I18n.T("You insert the power cell into [].", this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (isidcard(tool))
        {
            balloon_alert(user, I18n.T("owner id set for []", tool));
            owner_id = WEAKREF(tool) as datum_weakref;
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:175
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (cell == null)
        {
            balloon_alert(user, I18n.T("power cell missing!"));
            return ITEM_INTERACT_BLOCKING;
        }

        tool.play_tool_sound(this);
        user.visible_message(span_notice(I18n.T("[] unscrews the power cell from [].", user, this)), span_notice(I18n.T("You unscrew the power cell from [].", this)));
        await cell.forceMove(drop_location() as atom);
        set_cell(null);
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Registers an movable atom as a shell
     *
     * No functionality is done here. This is so that input components
     * can properly register any signals on the shell.
     * Arguments:
     * * new_shell - The new shell to register.
     */
    // tg: code/modules/wiremod/core/integrated_circuit.dm:194
    public virtual async DmTask set_shell(atom_movable new_shell = null)
    {
        remove_current_shell();
        set_on(TRUE);
        SEND_SIGNAL(COMSIG_CIRCUIT_SET_SHELL, Dm.list(this, new_shell));
        shell = new_shell;
        RegisterSignal(shell, COMSIG_QDELETING, nameof(remove_current_shell));
        foreach (obj_item_circuit_component attached_component in Dm.each_cast<obj_item_circuit_component>(attached_components))
        {
            await attached_component.register_shell(shell);
            // Their input ports may be updated with user values, but the outputs haven't updated
            // because on is FALSE
            attached_component.trigger_component();
        }
    }

    /**
     * Unregisters the current shell attached to this circuit.
     */
    // tg: code/modules/wiremod/core/integrated_circuit.dm:209
    [SignalHandler]
    public virtual void remove_current_shell()
    {
        if (shell == null)
            return;
        shell.name = Dm.str(Dm.initial(shell, (atom s) => s.name));
        foreach (obj_item_circuit_component attached_component in Dm.each_cast<obj_item_circuit_component>(attached_components))
            Waiting.NoSleep(attached_component.unregister_shell(shell));
        UnregisterSignal(shell, COMSIG_QDELETING);
        shell = null;
        set_on(FALSE);
        SEND_SIGNAL(COMSIG_CIRCUIT_SHELL_REMOVED, Dm.list(this));
    }

    /**
     * Sets the on status of the integrated circuit.
     *
     * Arguments:
     * * new_value - A boolean that determines if the circuit is on or not.
     **/
    // tg: code/modules/wiremod/core/integrated_circuit.dm:227
    public virtual void set_on(object new_value = null)
    {
        SEND_SIGNAL(COMSIG_CIRCUIT_SET_ON, Dm.list(this, new_value));
        on = Dm.num(new_value);
    }

    /**
     * Used for checking if another component of to_check's type exists in the circuit.
     * Introspects through modules.
     *
     * Arguments:
     * * to_check - The component to check.
     **/
    // tg: code/modules/wiremod/core/integrated_circuit.dm:238
    public virtual object is_duplicate(obj_item_circuit_component to_check = null)
    {
        foreach (object component in Dm.each(attached_components))
        {
            if (component == to_check)
                continue;
            if (istype(component, to_check.type))
                return TRUE;
            if (istype(component, typeof(obj_item_circuit_component_module)))
            {
                obj_item_circuit_component_module module = component as obj_item_circuit_component_module;
                foreach (object module_component in Dm.each(module.internal_circuit.attached_components))
                {
                    if (module_component == to_check)
                        continue;
                    if (istype(module_component, to_check.type))
                        return TRUE;
                }
            }
        }
        return FALSE;
    }

    /**
     * Adds a component to the circuitboard
     *
     * Once the component is added, the ports can be attached to other components
     */
    // tg: code/modules/wiremod/core/integrated_circuit.dm:258
    public virtual async DmTask<object> add_component(obj_item_circuit_component to_add = null, mob_living user = null)
    {
        if (to_add.parent != null)
            return FALSE;

        if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_CIRCUIT_ADD_COMPONENT, Dm.list(this, to_add, user)), COMPONENT_CANCEL_ADD_COMPONENT)))
            return FALSE;

        if (!Dm.truthy(await to_add.add_to(this)))
            return FALSE;

        if (Dm.truthy(Dm.bitand(to_add.circuit_flags, CIRCUIT_NO_DUPLICATES)))
        {
            if (Dm.truthy(is_duplicate(to_add)))
            {
                to_chat(user, span_danger(I18n.T("You can't insert multiple instances of this component into the same circuit!")));
                return FALSE;
            }
        }

        object success = FALSE;
        if (user != null)
            success = await user.transferItemToLoc(to_add, this);
        else
            success = await to_add.forceMove(this);

        if (!Dm.truthy(success))
            return FALSE;

        to_add.rel_x = rand(COMPONENT_MIN_RANDOM_POS, COMPONENT_MAX_RANDOM_POS) - screen_x;
        to_add.rel_y = rand(COMPONENT_MIN_RANDOM_POS, COMPONENT_MAX_RANDOM_POS) - screen_y;
        to_add.SEND_SIGNAL(COMSIG_CIRCUIT_COMPONENT_ADDED, Dm.list(to_add, this));
        to_add.parent = this;
        Dm.append(attached_components, to_add);
        current_size += to_add.circuit_size;
        RegisterSignal(to_add, COMSIG_MOVABLE_MOVED, nameof(component_move_handler));
        SStgui?.update_uis(this); // rules 10.6: a test harness world may lack SStgui

        if (shell != null)
            await to_add.register_shell(shell);
        return TRUE;
    }

    /**
     * Adds a component to the circuitboard through a manual action.
     */
    // tg: code/modules/wiremod/core/integrated_circuit.dm:298
    public virtual async DmTask<object> add_component_manually(obj_item_circuit_component to_add = null, mob_living user = null)
    {
        if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_CIRCUIT_ADD_COMPONENT_MANUALLY, Dm.list(this, to_add, user)), COMPONENT_CANCEL_ADD_COMPONENT)))
            return FALSE;

        return await add_component(to_add, user);
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:304
    [SignalHandler]
    public virtual void component_move_handler(obj_item_circuit_component source = null)
    {
        if (source.loc != this)
            Waiting.NoSleep(remove_component(source));
    }

    /**
     * Removes a component to the circuitboard
     *
     * This removes all connects between the ports
     */
    // tg: code/modules/wiremod/core/integrated_circuit.dm:314
    public virtual async DmTask remove_component(obj_item_circuit_component to_remove = null)
    {
        if (shell != null)
            await to_remove.unregister_shell(shell);

        UnregisterSignal(to_remove, COMSIG_MOVABLE_MOVED);
        current_size -= to_remove.circuit_size;
        Dm.remove(attached_components, to_remove);
        to_remove.disconnect();
        to_remove.parent = null;
        to_remove.SEND_SIGNAL(COMSIG_CIRCUIT_COMPONENT_REMOVED, Dm.list(to_remove, this));
        SStgui?.update_uis(this); // rules 10.6: a test harness world may lack SStgui
        to_remove.removed_from(this);
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:327
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:330
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/simple/circuit_assets), get_asset_datum(/datum/asset/json/circuit_components)): the
        // asset transport is the client's (DV44); the client draws the grid itself and the component list comes with the static data
        return Dm.list();
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:336
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["global_basic_types"] = GLOB.wiremod_basic_types;
        dot["screen_x"] = screen_x;
        dot["screen_y"] = screen_y;
        // DEVIATION(DV568): tg's ComponentMenu fetches circuit_components.json (/datum/asset/json/circuit_components, an asset);
        // the native window takes it with the static data
        dot["circuit_components"] = await circuit_components_json();

        obj_machinery_component_printer printer = linked_component_printer?.resolve() as obj_machinery_component_printer;
        if (printer == null)
            return dot;
        dot["stored_designs"] = printer.current_unlocked_designs;
        return dot;
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:347
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        DmList components = Dm.list();
        dot["components"] = components;
        foreach (obj_item_circuit_component component in Dm.each_cast<obj_item_circuit_component>(attached_components))
        {
            if (Dm.truthy(Dm.bitand(component.circuit_flags, CIRCUIT_FLAG_HIDDEN)))
            {
                Dm.append(components, Dm.list((object)null));
                continue;
            }

            DmList component_data = Dm.list();
            DmList input_ports_data = Dm.list();
            component_data["input_ports"] = input_ports_data;
            foreach (datum_port_input port in Dm.each_cast<datum_port_input>(component.input_ports))
            {
                object current_data = port.value;
                if (isatom(current_data)) // Prevent passing the name of the atom.
                    current_data = null;
                DmList connected_to = Dm.list();
                foreach (datum_port_output output in Dm.each_cast<datum_port_output>(port.connected_ports))
                    Dm.append(connected_to, REF(output));
                Dm.append(input_ports_data, Dm.list(Dm.list(
                    Dm.kv("name", port.name),
                    Dm.kv("type", port.datatype),
                    Dm.kv("ref", REF(port)), // The ref is the identifier to work out what it is connected to
                    Dm.kv("connected_to", connected_to),
                    Dm.kv("color", port.color),
                    Dm.kv("current_data", current_data),
                    Dm.kv("datatype_data", port.datatype_ui_data(user)))));
            }
            DmList output_ports_data = Dm.list();
            component_data["output_ports"] = output_ports_data;
            foreach (datum_port_output port in Dm.each_cast<datum_port_output>(component.output_ports))
            {
                Dm.append(output_ports_data, Dm.list(Dm.list(
                    Dm.kv("name", port.name),
                    Dm.kv("type", port.datatype),
                    Dm.kv("ref", REF(port)),
                    Dm.kv("color", port.color),
                    Dm.kv("datatype_data", port.datatype_ui_data(user)))));
            }

            component_data["name"] = component.display_name;
            component_data["x"] = component.rel_x;
            component_data["y"] = component.rel_y;
            component_data["removable"] = component.removable;
            component_data["color"] = component.ui_color;
            component_data["category"] = component.category;
            component_data["ui_alerts"] = component.ui_alerts;
            component_data["ui_buttons"] = component.ui_buttons;
            Dm.append(components, Dm.list(component_data));
        }

        DmList variables = Dm.list();
        dot["variables"] = variables;
        foreach (object variable_name in Dm.each(circuit_variables))
        {
            datum_circuit_variable variable = circuit_variables[variable_name] as datum_circuit_variable;
            DmList variable_data = Dm.list();
            variable_data["name"] = variable.name;
            variable_data["datatype"] = variable.datatype;
            variable_data["color"] = variable.color;
            if (islist(variable.value))
                variable_data["is_list"] = TRUE;
            Dm.append(variables, Dm.list(variable_data));
        }

        dot["display_name"] = display_name;

        obj_item_circuit_component examined = null;
        if (examined_component != null)
            examined = examined_component.resolve() as obj_item_circuit_component;

        dot["examined_name"] = examined?.display_name;
        dot["examined_desc"] = examined?.desc;
        dot["examined_notices"] = examined?.get_ui_notices();
        dot["examined_rel_x"] = examined_rel_x;
        dot["examined_rel_y"] = examined_rel_y;
        dot["grid_mode"] = grid_mode;

        dot["is_admin"] = Dm.num((Dm.truthy(admin_only) || isAdminGhostAI__gap(user)) && Dm.truthy(check_rights_for(user.client, R_VAREDIT)));
        return dot;
    }

    // GAP(lead): /proc/isAdminGhostAI (modules/mob/mob_helpers.dm:357) is not declared (an admin observer with AI interact, M10):
    // no mob is one yet.
    private static bool isAdminGhostAI__gap(mob user) => false;

    // tg: code/modules/wiremod/core/integrated_circuit.dm:419
    public override object ui_host(mob user = null)
    {
        if (shell != null)
            return shell;
        return base.ui_host(user);
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:424
    public override async DmTask<object> can_interact(mob user = null, float? require_adjacent_turf = null)
    {
        if (Dm.truthy(locked))
            return FALSE;
        return await base.can_interact(user, require_adjacent_turf);
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:429
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        object dot = await base.ui_status(user, state);

        if (isobserver(user))
            dot = max(Dm.num(dot), UI_UPDATE);

        // Extra protection because ui_state will not close the UI if they already have the ui open,
        // as ui_state is only set during
        if (Dm.truthy(admin_only))
        {
            if (!Dm.truthy(check_rights_for(user.client, R_VAREDIT)))
                return UI_CLOSE;
            else
                return UI_INTERACTIVE;
        }
        return dot;
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:443
    public override object ui_state(mob user = null)
    {
        if (shell == null)
            return GLOB.hands_state;
        return GLOB.physical_obscured_state;
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:448
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "IntegratedCircuit", name);
            await ui.open();
            ui.set_autoupdate(FALSE);
        }
        return null;
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:457  #define WITHIN_RANGE(id, table) (id >= 1 && id <= length(table)) (#undef at :714)
    private static bool WITHIN_RANGE(object id, object table) => Dm.ge(id, 1) && Dm.le(id, length(table));

    // tg: code/modules/wiremod/core/integrated_circuit.dm:457
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "add_connection":
            {
                object input_component_id = text2num(@params["input_component_id"]);
                object output_component_id = text2num(@params["output_component_id"]);
                object input_port_id = text2num(@params["input_port_id"]);
                object output_port_id = text2num(@params["output_port_id"]);
                if (!WITHIN_RANGE(input_component_id, attached_components) || !WITHIN_RANGE(output_component_id, attached_components))
                    return null;
                obj_item_circuit_component input_component = attached_components[input_component_id] as obj_item_circuit_component;
                obj_item_circuit_component output_component = attached_components[output_component_id] as obj_item_circuit_component;

                if (!WITHIN_RANGE(input_port_id, input_component.input_ports) || !WITHIN_RANGE(output_port_id, output_component.output_ports))
                    return null;
                datum_port_input input_port = input_component.input_ports[input_port_id] as datum_port_input;
                datum_port_output output_port = output_component.output_ports[output_port_id] as datum_port_output;

                if (!Dm.truthy(input_port.can_receive_from_datatype(output_port.datatype)))
                    return null;
                input_port.connect(output_port);
                dot = TRUE;
                break;
            }
            case "remove_connection":
            {
                object component_id = text2num(@params["component_id"]);
                object is_input = @params["is_input"];
                object port_id = text2num(@params["port_id"]);

                if (!WITHIN_RANGE(component_id, attached_components))
                    return null;
                obj_item_circuit_component component = attached_components[component_id] as obj_item_circuit_component;

                DmList port_table;
                if (Dm.truthy(is_input))
                    port_table = component.input_ports;
                else
                    port_table = component.output_ports;

                if (!WITHIN_RANGE(port_id, port_table))
                    return null;

                datum_port port = port_table[port_id] as datum_port;
                port.disconnect_all();
                dot = TRUE;
                break;
            }
            case "detach_component":
            {
                object component_id = text2num(@params["component_id"]);
                if (!WITHIN_RANGE(component_id, attached_components))
                    return null;
                obj_item_circuit_component component = attached_components[component_id] as obj_item_circuit_component;
                if (!Dm.truthy(component.removable))
                    return null;
                component.disconnect();
                await remove_component(component);

                mob user = ui.user;
                if (component.loc == this)
                    await user.put_in_hands(component);
                obj_machinery_component_printer printer = linked_component_printer?.resolve() as obj_machinery_component_printer;
                if (printer != null)
                    await printer.base_item_interaction(user as mob_living, component);
                dot = TRUE;
                break;
            }
            case "set_component_coordinates":
            {
                object component_id = text2num(@params["component_id"]);
                if (!WITHIN_RANGE(component_id, attached_components))
                    return null;
                obj_item_circuit_component component = attached_components[component_id] as obj_item_circuit_component;
                component.rel_x = min(max(-COMPONENT_MAX_POS, Dm.num(text2num(@params["rel_x"]))), COMPONENT_MAX_POS);
                component.rel_y = min(max(-COMPONENT_MAX_POS, Dm.num(text2num(@params["rel_y"]))), COMPONENT_MAX_POS);
                dot = TRUE;
                break;
            }
            case "set_component_input":
            {
                object component_id = text2num(@params["component_id"]);
                object port_id = text2num(@params["port_id"]);
                if (!WITHIN_RANGE(component_id, attached_components))
                    return null;
                obj_item_circuit_component component = attached_components[component_id] as obj_item_circuit_component;
                if (!WITHIN_RANGE(port_id, component.input_ports))
                    return null;
                datum_port_input port = component.input_ports[port_id] as datum_port_input;

                if (Dm.truthy(@params["set_null"]))
                {
                    port.set_input(null);
                    return TRUE;
                }

                if (Dm.truthy(@params["marked_atom"]))
                {
                    if (!Dm.truthy(Dm.bitand(port.datatype_handler.datatype_flags, DATATYPE_FLAG_ALLOW_ATOM_INPUT)))
                        return null;
                    obj_item_multitool_circuit marker = usr.get_active_held_item() as obj_item_multitool_circuit;
                    // Let's admins upload marked datums to an entity port.
                    if (!istype(marker, typeof(obj_item_multitool_circuit)))
                    {
                        client user_client = usr.client;
                        if (!Dm.truthy(check_rights_for(user_client, R_VAREDIT)))
                            return TRUE;
                        atom marked_atom = user_client.holder?.marked_datum as atom;
                        if (marked_atom == null)
                            return TRUE;
                        port.set_input(marked_atom);
                        balloon_alert(usr, I18n.T("updated []'s value to marked object.", port.name));
                        return TRUE;
                    }
                    if (marker.marked_atom == null)
                    {
                        port.set_input(null);
                        await marker.say(I18n.T("Cleared port ('[]')'s value.", port.name));
                        return TRUE;
                    }
                    await marker.say(I18n.T("Updated port ('[]')'s value to the marked entity.", port.name));
                    port.set_input(marker.marked_atom);
                    return TRUE;
                }

                object user_input = port.handle_manual_input(usr, @params["input"]);
                if (user_input == null)
                    return TRUE;
                port.set_input(user_input);
                dot = TRUE;
                break;
            }
            case "get_component_value":
            {
                object component_id = text2num(@params["component_id"]);
                object port_id = text2num(@params["port_id"]);
                if (!WITHIN_RANGE(component_id, attached_components))
                    return null;
                obj_item_circuit_component component = attached_components[component_id] as obj_item_circuit_component;
                if (!WITHIN_RANGE(port_id, component.output_ports))
                    return null;

                datum_port_output port = component.output_ports[port_id] as datum_port_output;
                object value = port.value;
                if (isatom(value))
                    value = PORT_TYPE_ATOM;
                else if (value == null)
                    value = "null";
                string string_form = Dm.copytext(Dm.text("[]", value), 1, PORT_MAX_STRING_DISPLAY);
                if (length(string_form) >= PORT_MAX_STRING_DISPLAY - 1)
                    string_form += "...";
                balloon_alert(usr, I18n.T("[] value: []", port.name, string_form));
                dot = TRUE;
                break;
            }
            case "set_display_name":
            {
                object new_name = @params["display_name"];

                if (Dm.truthy(new_name))
                    set_display_name(@params["display_name"]);
                else
                    set_display_name("");
                dot = TRUE;
                break;
            }
            case "toggle_grid_mode":
                toggle_grid_mode();
                dot = TRUE;
                break;
            case "set_examined_component":
            {
                object component_id = text2num(@params["component_id"]);
                if (!WITHIN_RANGE(component_id, attached_components))
                    return null;
                examined_component = WEAKREF(attached_components[component_id] as datum) as datum_weakref;
                examined_rel_x = Dm.num(text2num(@params["x"]));
                examined_rel_y = Dm.num(text2num(@params["y"]));
                dot = TRUE;
                break;
            }
            case "remove_examined_component":
                examined_component = null;
                dot = TRUE;
                break;
            case "save_circuit":
                return attempt_save_to(usr.client);
            case "add_variable":
            {
                string variable_identifier = trim(Dm.copytext(Dm.str(@params["variable_name"]), 1, PORT_MAX_NAME_LENGTH)) as string;
                if (Dm.@in(variable_identifier, circuit_variables))
                    return TRUE;
                if (variable_identifier == "")
                    return TRUE;
                object variable_datatype = @params["variable_datatype"];
                if (!Dm.@in(variable_datatype, GLOB.wiremod_basic_types))
                    return null;
                if (Dm.truthy(@params["is_list"]))
                    variable_datatype = PORT_TYPE_LIST(variable_datatype);
                else if (Dm.truthy(@params["is_assoc_list"]))
                    variable_datatype = PORT_TYPE_ASSOC_LIST(PORT_TYPE_STRING, variable_datatype);
                datum_circuit_variable variable = Dm.@new<datum_circuit_variable>(variable_identifier, variable_datatype);
                if (Dm.truthy(@params["is_assoc_list"]))
                {
                    variable.set_value(Dm.list());
                    assoc_list_variables[variable_identifier] = variable;
                }
                else if (Dm.truthy(@params["is_list"]))
                {
                    variable.set_value(Dm.list());
                    list_variables[variable_identifier] = variable;
                }
                else
                    modifiable_circuit_variables[variable_identifier] = variable;
                circuit_variables[variable_identifier] = variable;
                dot = TRUE;
                break;
            }
            case "remove_variable":
            {
                object variable_identifier = @params["variable_name"];
                if (!Dm.@in(variable_identifier, circuit_variables))
                    return null;
                datum_circuit_variable variable = circuit_variables[variable_identifier] as datum_circuit_variable;
                if (variable == null)
                    return null;
                Dm.remove(circuit_variables, variable_identifier);
                Dm.remove(list_variables, variable_identifier);
                Dm.remove(modifiable_circuit_variables, variable_identifier);
                qdel(variable);
                dot = TRUE;
                break;
            }
            case "add_setter_or_getter":
            {
                if (setter_and_getter_count >= max_setters_and_getters)
                {
                    balloon_alert(usr, I18n.T("setter and getter count at maximum capacity"));
                    return null;
                }
                Type designated_type = typeof(obj_item_circuit_component_variable_getter);
                if (Dm.truthy(@params["is_setter"]))
                    designated_type = typeof(obj_item_circuit_component_variable_setter);
                obj_item_circuit_component_variable component = Dm.@new(designated_type, this) as obj_item_circuit_component_variable;
                if (!Dm.truthy(await add_component(component, usr as mob_living)))
                {
                    qdel(component);
                    return null;
                }
                component.variable_name.set_input(@params["variable"]);
                component.rel_x = Dm.num(text2num(@params["rel_x"]));
                component.rel_y = Dm.num(text2num(@params["rel_y"]));
                RegisterSignal(component, COMSIG_CIRCUIT_COMPONENT_REMOVED, nameof(clear_setter_or_getter));
                setter_and_getter_count++;
                return TRUE;
            }
            case "move_screen":
                screen_x = Dm.num(text2num(@params["screen_x"]));
                screen_y = Dm.num(text2num(@params["screen_y"]));
                break;
            case "perform_action":
            {
                object component_id = text2num(@params["component_id"]);
                if (!WITHIN_RANGE(component_id, attached_components))
                    return null;
                obj_item_circuit_component component = attached_components[component_id] as obj_item_circuit_component;
                component.SEND_SIGNAL(COMSIG_CIRCUIT_COMPONENT_PERFORM_ACTION, Dm.list(component, ui.user, @params["action_name"]));
                await component.ui_perform_action(ui.user, @params["action_name"]);
                break;
            }
            case "print_component":
            {
                object component_path = text2path(@params["component_to_print"] as string);
                obj_item_circuit_component component;
                if ((!Dm.truthy(admin_only) && !isAdminGhostAI__gap(ui.user)) || !Dm.truthy(check_rights_for(ui.user.client, R_SPAWN)))
                {
                    obj_machinery_component_printer printer = linked_component_printer?.resolve() as obj_machinery_component_printer;
                    if (printer == null)
                    {
                        balloon_alert(ui.user, I18n.T("linked printer not found!"));
                        return null;
                    }
                    component = await printer.print_component(component_path, user_data: ID_DATA(usr)) as obj_item_circuit_component;
                    if (component == null)
                    {
                        balloon_alert(ui.user, I18n.T("failed to make the component!"));
                        return null;
                    }
                }
                else
                {
                    if (!ispath(component_path, typeof(obj_item_circuit_component)))
                        return null;
                    component = Dm.@new((Type)component_path, drop_location()) as obj_item_circuit_component;
                    component.datum_flags = Dm.bitor(component.datum_flags, DF_VAR_EDITED);
                }
                if (!Dm.truthy(await add_component(component)))
                    return null;
                component.rel_x = Dm.num(text2num(@params["rel_x"]));
                component.rel_y = Dm.num(text2num(@params["rel_y"]));
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:702
    public override object balloon_alert(mob viewer = null, object text = null)
    {
        if (shell != null)
            return shell.balloon_alert(viewer, text);
        return base.balloon_alert(viewer, text);
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:707
    [SignalHandler]
    public virtual void clear_setter_or_getter(datum source = null)
    {
        // This'll also be called in the Destroy() override of /obj/item/circuit_component
        if (!QDELING(source))
            qdel(source);
        setter_and_getter_count--;
    }

    // tg: code/modules/wiremod/core/integrated_circuit.dm:714
    [SignalHandler]
    public virtual object on_atom_usb_cable_try_attach(datum source = null, obj_item_usb_cable usb_cable = null, mob user = null)
    {
        usb_cable.balloon_alert(user, I18n.T("circuit needs to be in a compatible shell"));
        return COMSIG_CANCEL_USB_CABLE_ATTACK;
    }

    /// Sets the display name that appears on the shell.
    // tg: code/modules/wiremod/core/integrated_circuit.dm:720
    public virtual void set_display_name(object new_name = null)
    {
        display_name = Dm.copytext_char(Dm.str(new_name), 1, label_max_length);
        if (shell == null)
            return;

        if (display_name != "")
        {
            if (!Dm.truthy(admin_only))
                shell.name = Dm.text("[] ([])", Dm.initial(shell, (atom s) => s.name), strip_html(display_name));
            else
                shell.name = display_name;
        }
        else
            shell.name = Dm.str(Dm.initial(shell, (atom s) => s.name));
    }

    /// Toggles the grid mode property for this circuit.
    // tg: code/modules/wiremod/core/integrated_circuit.dm:734
    public virtual void toggle_grid_mode()
    {
        grid_mode = Dm.num(!Dm.truthy(grid_mode));
    }

    /**
     * Returns the creator of the integrated circuit. Used in admin messages and other related things.
     */
    // tg: code/modules/wiremod/core/integrated_circuit.dm:740
    public virtual object get_creator_admin()
    {
        return get_creator(include_link: TRUE);
    }

    /**
     * Returns the creator of the integrated circuit. Used in admin logs and other related things.
     */
    // tg: code/modules/wiremod/core/integrated_circuit.dm:746
    public virtual object get_creator(float? include_link = null)
    {
        include_link ??= FALSE;
        datum_mind inserter = null;
        if (inserter_mind != null)
            inserter = inserter_mind.resolve() as datum_mind;

        obj_item_card_id id_card = null;
        if (owner_id != null)
            id_card = owner_id.resolve() as obj_item_card_id;

        return Dm.text("[] (Shell: [], Inserter: [], Owner ID: [])", this, (object)shell ?? "*null*", key_name(inserter, include_link), (object)id_card?.name ?? "*null*");
    }

    /// Attempts to save a circuit to a given client
    // tg: code/modules/wiremod/core/integrated_circuit.dm:758
    public virtual object attempt_save_to(client saver = null)
    {
        if (!Dm.truthy(check_rights_for(saver, R_VAREDIT)))
            return FALSE;
        // var/temp_file = file("data/CircuitDownloadTempFile"); fdel(temp_file); WRITE_FILE(temp_file, convert_to_json());
        // DIRECT_OUTPUT(saver, ftp(temp_file, "[display_name || "circuit"].json")): BYOND's file download to the client
        Dm.Deferred("code/modules/wiremod/core/integrated_circuit.dm:778", "attempt_save_to: the circuit's JSON download to the admin's client (BYOND's ftp; the console's load-circuit takes the JSON back)");
        return TRUE;
    }
}

// tg: code/modules/wiremod/core/integrated_circuit.dm:100
public partial class obj_item_integrated_circuit_loaded : obj_item_integrated_circuit
{
    // tg: code/modules/wiremod/core/integrated_circuit.dm:101
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_cell(Dm.@new<obj_item_stock_parts_power_store_cell_high>(this));
        return dot;
    }
}
