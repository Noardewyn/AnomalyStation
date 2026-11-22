// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/core/component.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the circuit component base: its ports, its trigger (the cell's charge per input, the
// trigger port, the output signal), its UI notices and the shell and USB hooks its subtypes fill. The var block is generated (D111).

/**
 * # Integrated Circuit Component
 *
 * A component that performs a function when given an input
 *
 * Can be attached to an integrated circuitboard, where it can then
 * be connected between other components to provide an output or to receive
 * an input. This is the base type of all components
 */
// tg: code/modules/wiremod/core/component.dm:10
public partial class obj_item_circuit_component : obj_item
{
    /// Called when the option ports should be set up
    // tg: code/modules/wiremod/core/component.dm:73
    public virtual void populate_options()
    {
        return;
    }

    /// Called when the rest of the ports should be set up
    // tg: code/modules/wiremod/core/component.dm:77
    public virtual async DmTask populate_ports()
    {
        return;
    }

    /// Extension of add_input_port. Simplifies the code to make an option port to reduce boilerplate
    // tg: code/modules/wiremod/core/component.dm:81
    public virtual object add_option_port(object name = null, DmList list_to_use = null, float? order = null, string trigger = null)
    {
        order ??= 0;
        trigger ??= nameof(input_received);
        return add_input_port(name, PORT_TYPE_OPTION, order: order, trigger: trigger, port_type: typeof(datum_port_input_option), extra_args: Dm.list(Dm.kv("possible_options", list_to_use)));
    }

    // tg: code/modules/wiremod/core/component.dm:84
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(name, COMPONENT_DEFAULT_NAME))
            name = Dm.text("[] []", LOWER_TEXT(display_name), COMPONENT_DEFAULT_NAME);
        populate_options();
        Waiting.NoSleep(populate_ports());
        if (Dm.truthy(Dm.bitand(circuit_flags, CIRCUIT_FLAG_INPUT_SIGNAL)) && trigger_input == null)
            trigger_input = add_input_port("Trigger", PORT_TYPE_SIGNAL, order: 2) as datum_port_input;
        if (Dm.truthy(Dm.bitand(circuit_flags, CIRCUIT_FLAG_OUTPUT_SIGNAL)) && trigger_output == null)
            trigger_output = add_output_port("Triggered", PORT_TYPE_SIGNAL, order: 2) as datum_port_output;
        update_ui_alerts();
        return dot;
    }

    // tg: code/modules/wiremod/core/component.dm:96
    public override object Destroy(float? force = null)
    {
        if (parent != null)
        {
            // Prevents a Destroy() recursion
            obj_item_integrated_circuit old_parent = parent;
            parent = null;
            Waiting.NoSleep(old_parent.remove_component(this));
        }

        trigger_input = null;
        trigger_output = null;

        QDEL_LIST(output_ports);
        QDEL_LIST(input_ports);
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/core/component.dm:110
    public override object drop_location()
    {
        if (parent?.shell != null)
            return parent.shell.drop_location();
        return base.drop_location();
    }

    // tg: code/modules/wiremod/core/component.dm:115
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.bitand(circuit_flags, CIRCUIT_FLAG_REFUSE_MODULE)))
            Dm.append(ref dot, span_notice(I18n.T("It's incompatible with module components.")));
        return dot;
    }

    /// updates the ui alerts in the given component. new_flag adds flags, remove_flag removes them
    // tg: code/modules/wiremod/core/component.dm:121
    public virtual void update_ui_alerts(object new_flag = null, object remove_flag = null)
    {
        if (Dm.truthy(new_flag))
            circuit_flags = Dm.bitor(circuit_flags, Dm.num(new_flag));
        if (Dm.truthy(remove_flag))
            circuit_flags = Dm.bitand(circuit_flags, Dm.bitnot(Dm.num(remove_flag)));
        if (Dm.truthy(Dm.bitand(circuit_flags, CIRCUIT_FLAG_INSTANT)))
            ui_alerts["tachometer-alt"] = "Instant";
        else
            Dm.remove(ui_alerts, "tachometer-alt");
        if (Dm.truthy(Dm.bitand(circuit_flags, CIRCUIT_FLAG_DISABLED)))
            ui_alerts["exclamation"] = "Non-functional";
        else
            Dm.remove(ui_alerts, "exclamation");
    }

    /**
     * Called when a shell is registered from the component/the component is added to a circuit.
     *
     * Register all signals here on the shell.
     * Arguments:
     * * shell - Shell being registered
     */
    // tg: code/modules/wiremod/core/component.dm:142
    public virtual async DmTask register_shell(atom_movable shell = null)
    {
        return;
    }

    /**
     * Called when a shell is unregistered from the component/the component is removed from a circuit.
     *
     * Unregister all signals here on the shell.
     * Arguments:
     * * shell - Shell being unregistered
     */
    // tg: code/modules/wiremod/core/component.dm:152
    public virtual async DmTask unregister_shell(atom_movable shell = null)
    {
        return;
    }

    /**
     * Disconnects a component from other components
     *
     * Disconnects both the input and output ports of the component
     */
    // tg: code/modules/wiremod/core/component.dm:160
    public virtual void disconnect()
    {
        foreach (datum_port_output port_to_disconnect in Dm.each_cast<datum_port_output>(output_ports))
            port_to_disconnect.disconnect_all();

        foreach (datum_port_input port_to_disconnect in Dm.each_cast<datum_port_input>(input_ports))
            port_to_disconnect.disconnect_all();
    }

    /**
     * Adds an input port and returns it
     *
     * Arguments:
     * * name - The name of the input port
     * * type - The datatype it handles
     * * trigger - Whether this input port triggers an update on the component when updated.
     */
    // tg: code/modules/wiremod/core/component.dm:175
    public virtual object add_input_port(object name = null, object type = null, float? order = null, string trigger = null, object @default = null, Type port_type = null, object extra_args = null, params object[] __extra)
    {
        order ??= 1;
        trigger ??= nameof(input_received);
        port_type ??= typeof(datum_port_input);
        // ADAPTED: tg passes list(src) + args + extra_args through arglist; the port's New declares (to_connect, name, datatype, order,
        // trigger, default) and the option port adds possible_options, so the positions after default (port_type, extra_args) that
        // DM's New ignores are not passed, and extra_args' keyed entries are its named arguments.
        var arguments = new List<object> { this, name, type, order, trigger, @default };
        if (extra_args is DmList extra)
        {
            foreach (object key in Dm.each(extra))
            {
                object associated = key is string ? extra[key] : null;
                arguments.Add(associated != null ? Dm.named(Dm.str(key), associated) : key);
            }
        }
        datum_port_input input_port = Dm.@new(port_type, arguments.ToArray()) as datum_port_input;
        Dm.append(input_ports, input_port);
        sortTim(input_ports, "/proc/" + nameof(cmp_port_order_asc));
        if (parent != null)
            SStgui?.update_uis(parent); // rules 10.6: a test harness world may lack SStgui
        return input_port;
    }

    /**
     * Removes an input port and deletes it. This will not cleanup any references made by derivatives of the circuit component
     *
     * Arguments:
     * * input_port - The input port to remove.
     */
    // tg: code/modules/wiremod/core/component.dm:193
    public virtual void remove_input_port(datum_port_input input_port = null)
    {
        Dm.remove(input_ports, input_port);
        qdel(input_port);
        if (parent != null)
            SStgui?.update_uis(parent); // rules 10.6: a test harness world may lack SStgui
        return; //explicitly set the port to null if used like this: `port = remove_input_port(port)`
    }

    /**
     * Adds an output port and returns it
     *
     * Arguments:
     * * name - The name of the output port
     * * type - The datatype it handles.
     */
    // tg: code/modules/wiremod/core/component.dm:207
    public virtual object add_output_port(object name = null, object type = null, float? order = null, params object[] __extra)
    {
        order ??= 1;
        datum_port_output output_port = Dm.@new<datum_port_output>(this, name, type, order);
        Dm.append(output_ports, output_port);
        sortTim(output_ports, "/proc/" + nameof(cmp_port_order_asc));
        if (parent != null)
            SStgui?.update_uis(parent); // rules 10.6: a test harness world may lack SStgui
        return output_port;
    }

    /**
     * Removes an output port and deletes it. This will not cleanup any references made by derivatives of the circuit component
     *
     * Arguments:
     * * output_port - The output port to remove.
     */
    // tg: code/modules/wiremod/core/component.dm:223
    public virtual void remove_output_port(datum_port_output output_port = null)
    {
        Dm.remove(output_ports, output_port);
        qdel(output_port);
        if (parent != null)
            SStgui?.update_uis(parent); // rules 10.6: a test harness world may lack SStgui
        return; //explicitly set the port to null if used like this: `port = remove_output_port(port)`
    }

    /**
     * Called whenever an input is received from one of the ports.
     *
     * Return value indicates whether the trigger was successful or not.
     * Arguments:
     * * port - Can be null. The port that sent the input
     * * return_values - Only defined if the component is receiving an input due to instant execution. Contains the values to be returned once execution has stopped.
     */
    // tg: code/modules/wiremod/core/component.dm:239
    [ShouldNotSleep]
    public virtual object trigger_component(datum_port_input port = null, DmList return_values = null)
    {
        pre_input_received(port);
        if (!Dm.truthy(should_receive_input(port)))
            return FALSE;

        object result;
        if (port != null)
        {
            object proc_to_call = port.trigger;
            if (!Dm.truthy(proc_to_call))
                return FALSE;
            result = Waiting.NoSleep(Dm.Call(this, Dm.str(proc_to_call), port, return_values));
        }
        else
            result = input_received(null, return_values);

        if (Dm.truthy(result))
            return FALSE;

        if (Dm.truthy(Dm.bitand(circuit_flags, CIRCUIT_FLAG_OUTPUT_SIGNAL)))
            trigger_output.set_output(COMPONENT_SIGNAL);
        return TRUE;
    }

    // tg: code/modules/wiremod/core/component.dm:261
    public virtual void set_circuit_size(object new_size = null)
    {
        if (parent != null)
            parent.current_size -= circuit_size;

        circuit_size = Dm.num(new_size);

        if (parent != null)
            parent.current_size += circuit_size;
    }

    /**
     * Called whether this circuit component should receive an input.
     * If this returns false, the proc that is supposed to be triggered will not be called and an output signal will not be sent.
     * This is to only return false if flow of execution should be stopped because something bad has happened (e.g. no power)
     * Returning no value in input_received() is not an issue because it means flow of execution will continue even if the component failed to execute properly.
     *
     * Return value indicates whether or not
     * Arguments:
     * * port - Can be null. The port that sent the input
     */
    // tg: code/modules/wiremod/core/component.dm:280
    [ShouldNotSleep]
    public virtual object should_receive_input(datum_port_input port = null)
    {
        if (!Dm.truthy(parent?.on))
            return FALSE;

        if (!Dm.truthy(parent.admin_only))
        {
            if (Dm.truthy(Dm.bitand(circuit_flags, CIRCUIT_FLAG_ADMIN)))
            {
                message_admins(Dm.text("[] tried to execute on [] that has admin_only set to 0", display_name, parent.get_creator_admin()));
                return FALSE;
            }

            object flags = parent.SEND_SIGNAL(COMSIG_CIRCUIT_PRE_POWER_USAGE, Dm.list(parent, energy_usage_per_input));
            if (!Dm.truthy(Dm.bitand(flags, COMPONENT_OVERRIDE_POWER_USAGE)))
            {
                obj_item_stock_parts_power_store cell = parent.get_cell() as obj_item_stock_parts_power_store;
                if (cell == null || !Dm.truthy(Waiting.NoSleep(cell.use(energy_usage_per_input))))
                    return FALSE;
            }
        }

        if ((port == null || Dm.eq(port.trigger, nameof(input_received))) && Dm.truthy(Dm.bitand(circuit_flags, CIRCUIT_FLAG_INPUT_SIGNAL)) && !COMPONENT_TRIGGERED_BY(trigger_input, port))
            return FALSE;

        return TRUE;
    }

    /// Called when trying to get the physical location of this object
    // tg: code/modules/wiremod/core/component.dm:302
    public virtual object get_location()
    {
        return get_turf(this) ?? get_turf(parent?.shell);
    }

    // tg: code/modules/wiremod/core/component.dm:305
    public override object balloon_alert(mob viewer = null, object text = null)
    {
        if (parent != null)
            return parent.balloon_alert(viewer, text);
        return base.balloon_alert(viewer, text);
    }

    /// Called before input_received and should_receive_input. Used to perform behaviour that shouldn't care whether the input should be received or not.
    // tg: code/modules/wiremod/core/component.dm:312
    [ShouldNotSleep]
    public virtual void pre_input_received(datum_port_input port = null)
    {
        return;
    }

    /**
     * Called from trigger_component. General component behaviour should go in this proc. This is the default proc that is called if no trigger proc is specified.
     *
     * Return value indicates that the circuit should not send an output signal.
     * Arguments:
     * * port - Can be null. The port that sent the input
     * * return_values - Only defined if the component is receiving an input due to instant execution. Contains the values to be returned once execution has stopped.
     */
    // tg: code/modules/wiremod/core/component.dm:324
    [ShouldNotSleep]
    public virtual object input_received(datum_port_input port = null, DmList return_values = null)
    {
        return null;
    }

    /// Called when this component is about to be added to an integrated_circuit.
    // tg: code/modules/wiremod/core/component.dm:329
    public virtual async DmTask<object> add_to(obj_item_integrated_circuit added_to = null)
    {
        if (Dm.truthy(Dm.bitand(circuit_flags, CIRCUIT_FLAG_ADMIN)))
            ADD_TRAIT(added_to, TRAIT_CIRCUIT_UNDUPABLE, REF(this));
        return TRUE;
    }

    /// Called when this component is removed from an integrated_circuit.
    // tg: code/modules/wiremod/core/component.dm:335
    public virtual void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        REMOVE_TRAIT(removed_from, TRAIT_CIRCUIT_UNDUPABLE, REF(this));
        return;
    }

    /**
     * Gets the UI notices to be displayed on the CircuitInfo panel.
     *
     * Returns a list of buttons in the following format
     * list(
     *   "icon" = ICON(string)
     *   "content" = CONTENT(string)
     *   "color" = COLOR(string, not a hex)
     * )
     */
    // tg: code/modules/wiremod/core/component.dm:349
    public virtual object get_ui_notices()
    {
        DmList dot = Dm.list();

        if (Dm.truthy(Dm.bitand(circuit_flags, CIRCUIT_FLAG_INSTANT)))
            Dm.append(dot, create_ui_notice("Instant Execution", "red", "tachometer-alt"));

        if (!Dm.truthy(removable))
            Dm.append(dot, create_ui_notice("Unremovable", "red", "lock"));

        if (Dm.truthy(length(required_shells)))
        {
            Dm.append(dot, create_ui_notice("Supported Shells:", "green", "notes-medical"));
            foreach (object shell in Dm.each(required_shells))
                Dm.append(dot, create_ui_notice(Dm.initial(shell, (atom_movable s) => s.name), "green", "plus-square"));
        }

        if (Dm.truthy(length(input_ports)))
            Dm.append(dot, create_ui_notice(Dm.text("Energy Usage Per Input: []", display_energy(energy_usage_per_input)), "orange", "bolt"));
        return dot;
    }

    /**
     * Called when a special button is pressed on this component in the UI.
     *
     * Arguments:
     * * user - Interacting mob
     * * action - A string for which action is being performed. No parameters passed because it's only a button press.
     */
    // tg: code/modules/wiremod/core/component.dm:374
    public virtual async DmTask ui_perform_action(mob user = null, object action = null)
    {
        return;
    }

    /**
     * Creates a UI notice entry to be used in get_ui_notices()
     *
     * Returns a list that can then be added to the return list in get_ui_notices()
     */
    // tg: code/modules/wiremod/core/component.dm:382
    [ShouldBePure, ShouldNotOverride]
    public virtual object create_ui_notice(object content = null, object color = null, object icon = null)
    {
        return Dm.list(Dm.list(
            Dm.kv("icon", icon),
            Dm.kv("content", content),
            Dm.kv("color", color)));
    }

    // tg: code/modules/wiremod/core/component.dm:391
    public override object ui_host(mob user = null)
    {
        if (parent != null)
            return parent.ui_host();
        return base.ui_host(user);
    }

    /**
     * Creates a table UI notice entry to be used in get_ui_notices()
     *
     * Returns a list that can then be added to the return list in get_ui_notices()
     * Used by components to list their available columns. Recommended to use at the end of get_ui_notices()
     */
    // tg: code/modules/wiremod/core/component.dm:402
    [ShouldBePure, ShouldNotOverride]
    public virtual object create_table_notices(DmList entries = null, string column_name = null, string column_name_plural = null)
    {
        column_name ??= "Column";
        column_name_plural ??= "Columns";
        DmList dot = Dm.list();
        Dm.append(dot, create_ui_notice(Dm.text("Available []:", column_name_plural), "grey", "question-circle"));

        foreach (object entry in Dm.each(entries))
            Dm.append(dot, create_ui_notice(Dm.text("[] Name: '[]'", column_name, entry), "grey", "columns"));
        return dot;
    }

    /**
     * Called when a circuit component is added to an object with a USB port.
     *
     * Arguments:
     * * shell - The object that USB cables can connect to
     */
    // tg: code/modules/wiremod/core/component.dm:418
    public virtual void register_usb_parent(atom_movable shell = null)
    {
        return;
    }

    /**
     * Called when a circuit component is removed from an object with a USB port.
     *
     * Arguments:
     * * shell - The object that USB cables can connect to
     */
    // tg: code/modules/wiremod/core/component.dm:427
    public virtual object unregister_usb_parent(atom_movable shell = null)
    {
        return null;
    }

    /**
     * Called when a circuit component requests to send Ntnet data signal.
     *
     * Arguments:
     * * port - The required list port needed by the Ntnet receive
     * * key - The encryption key
     * * signal_type - The signal type used for sending this global signal (optional, default is COMSIG_GLOB_CIRCUIT_NTNET_DATA_SENT)
     */
    // tg: code/modules/wiremod/core/component.dm:438
    public virtual void send_ntnet_data(datum_port_input port = null, object key = null, string signal_type = null)
    {
        signal_type ??= COMSIG_GLOB_CIRCUIT_NTNET_DATA_SENT;
        // SEND_GLOBAL_SIGNAL(signal_type, list(...)) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        SSdcs.SEND_SIGNAL(signal_type, Dm.list(SSdcs, Dm.list(Dm.kv("data", port.value), Dm.kv("enc_key", key), Dm.kv("port", WEAKREF(port)))));
    }
}
