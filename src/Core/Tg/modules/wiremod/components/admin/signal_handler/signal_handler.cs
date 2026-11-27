// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/admin/signal_handler/signal_handler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # Signal Handler Component
 *
 * A component that registers signals on events and listens for them.
 */
// tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:9
public partial class obj_item_circuit_component_signal_handler : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:1
    private const string COMP_SIGNAL_HANDLER_GLOBAL = "Global";
    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:2
    private const string COMP_SIGNAL_HANDLER_OBJECT = "Object";

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:59  var/static/list/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_signal_handler__populate_options__component_options;

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:58
    public override void populate_options()
    {
        signal_handler_options = add_option_port("Signal Handler Options", populate_options__component_options, trigger: null) as datum_port_input_option;

        signal_id = add_option_port("Signal ID", GLOB.integrated_circuit_signal_ids, trigger: null) as datum_port_input_option;
        signal_map = GLOB.integrated_circuit_signal_ids;
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:68
    public override async DmTask populate_ports()
    {
        instant = add_input_port("Instant", PORT_TYPE_BOOLEAN, order: 0.5f, trigger: null, @default: TRUE) as datum_port_input;
        register = add_input_port("Register", PORT_TYPE_SIGNAL, order: 2, trigger: nameof(register_signals)) as datum_port_input;
        unregister = add_input_port("Unregister", PORT_TYPE_SIGNAL, order: 2, trigger: nameof(unregister_signals)) as datum_port_input;
        unregister_all = add_input_port("Unregister All", PORT_TYPE_SIGNAL, order: 2, trigger: nameof(unregister_signals_all)) as datum_port_input;

        add_source_entity();
        event_triggered = add_output_port("Triggered", PORT_TYPE_INSTANT_SIGNAL, order: 2) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:77
    public virtual void add_source_entity()
    {
        if (target != null)
            remove_input_port(target);
        if (entity != null)
            remove_output_port(entity);

        target = add_input_port("Target", PORT_TYPE_DATUM, order: 1, trigger: null) as datum_port_input;
        entity = add_output_port("Source Entity", PORT_TYPE_DATUM, order: 0) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:86
    public override void save_data_to_list(DmList component_data = null)
    {
        base.save_data_to_list(component_data);
        component_data["signal_id"] = signal_id.value;
        component_data["signal_port_data"] = signal_ports;
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:91
    public override async DmTask load_data_from_list(DmList component_data = null)
    {
        signal_id.set_value(component_data["signal_id"], force: TRUE);
        registered_signal = signal_id.value;
        load_new_ports(component_data["signal_port_data"] as DmList);
        custom_signal = TRUE;
        await base.load_data_from_list(component_data);
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:99
    public override void pre_input_received(datum_port_input port = null)
    {
        if (!Dm.eq(signal_id.value, registered_signal))
        {
            custom_signal = FALSE;
            unregister_signals_all(port);
            registered_signal = signal_id.value;
            DmList data = signal_map[registered_signal] as DmList;
            if (data != null)
                load_new_ports(data);
        }

        if (signal_handler_options == port)
            set_signal_options(port);
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:111
    [ShouldNotSleep]
    public virtual void set_signal_options(datum_port_input port = null)
    {
        object sw__1 = signal_handler_options.value;
        if (Dm.eq(sw__1, COMP_SIGNAL_HANDLER_GLOBAL))
        {
            signal_id.possible_options = GLOB.integrated_circuit_global_signal_ids;
            signal_map = GLOB.integrated_circuit_global_signal_ids;
            remove_output_port(entity);
            remove_input_port(target);
            target = null;
            entity = null;
        }
        else if (Dm.eq(sw__1, COMP_SIGNAL_HANDLER_OBJECT))
        {
            signal_id.possible_options = GLOB.integrated_circuit_signal_ids;
            signal_map = GLOB.integrated_circuit_signal_ids;
            add_source_entity();
        }

        if (!Dm.truthy(custom_signal))
            signal_id.set_value(null, TRUE);
        unregister_signals_all(port);
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:132
    [ShouldNotSleep]
    public virtual void register_signals(datum_port_input port = null)
    {
        datum target_datum = target?.value as datum;
        if (Dm.eq(signal_handler_options.value, COMP_SIGNAL_HANDLER_GLOBAL))
            target_datum = SSdcs;

        if (target_datum != null)
        {
            log_admin_circuit(Dm.text("[] registered the signal '[]' on []", parent.get_creator(), registered_signal, target_datum));
            // We override because an admin may try registering a signal on the same object/datum again, so this prevents any runtimes from occuring
            RegisterSignal(target_datum, registered_signal, nameof(handle_signal_received), @override: TRUE);
            Dm.combine(ref registered_entities, WEAKREF(target_datum));
        }
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:144
    public virtual void load_new_ports(DmList ports_to_load = null)
    {
        foreach (object input_port__it in Dm.each(input_signal_ports))
            remove_input_port((datum_port_input)input_port__it);
        foreach (object output_port__it in Dm.each(output_signal_ports))
            remove_output_port((datum_port_output)output_port__it);
        input_signal_ports = Dm.list();
        output_signal_ports = Dm.list();

        signal_ports = ports_to_load;
        foreach (DmList data in Dm.each<DmList>(signal_ports))
        {
            if (Dm.truthy(data["is_response"]))
            {
                datum_port_input bitflag_input = add_input_port(data["name"], PORT_TYPE_SIGNAL, order: 3, trigger: nameof(handle_bitflag_received)) as datum_port_input;
                input_signal_ports[bitflag_input] = data["bitflag"];
            }
            else
                Dm.append(output_signal_ports, add_output_port(data["name"], data["type"], order: 1));
        }
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:162
    [ShouldNotSleep]
    public virtual void unregister_signals_all(datum_port_input port = null)
    {
        foreach (object weakref_of_object__it in Dm.each(registered_entities))
        {
            datum_weakref weakref_of_object = (datum_weakref)weakref_of_object__it;
            datum datum_to_unregister = weakref_of_object.resolve() as datum;
            if (datum_to_unregister == null)
                continue;
            UnregisterSignal(datum_to_unregister, registered_signal);
        }
        registered_entities.Cut();
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:172
    [ShouldNotSleep]
    public virtual void unregister_signals(datum_port_input port = null)
    {
        datum registered_datum = target?.value as datum;
        if (Dm.eq(signal_handler_options.value, COMP_SIGNAL_HANDLER_GLOBAL))
            registered_datum = SSdcs;

        if (registered_datum == null)
            return;

        UnregisterSignal(registered_datum, registered_signal);
        Dm.remove(ref registered_entities, WEAKREF(registered_datum));
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:185
    public virtual void run_ports_on_args(DmList arguments = null)
    {
        object first_arg = popleft(arguments);
        if (entity != null)
            entity.set_output(first_arg);

        foreach (object port__it in Dm.each(output_signal_ports))
            ((datum_port_output)port__it).set_output(popleft(arguments));
        event_triggered.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:194
    [SignalHandler]
    public virtual object handle_signal_received(params object[] __extra)
    {
        DmList arguments = Dm.list(__extra);

        if (!Dm.truthy(instant.value))
        {
            run_ports_on_args(arguments);
            return null;
        }

        // usr is not supposed to be defined whilst these execute, which it can be for some signal IDs.
        // Especially if you try to proccall something - it'll fail because of this reason.
        // No other way to solve this problem without refactoring proccall code, but it's admin tooling so it's whatever.
        mob temp_usr = usr;
        usr = null;

        DmList displayArgs = arguments.Copy();
        log_admin_circuit(Dm.text("[] received a signal from [] ([]) with the parameters \\[[]\\]", parent.get_creator(), popleft(displayArgs), registered_signal, displayArgs.Join(", ")));
        SScircuit_component.queue_instant_run();
        run_ports_on_args(arguments);
        DmList output = SScircuit_component.execute_instant_run() as DmList;

        usr = temp_usr;

        if (output == null)
        {
            message_admins(Dm.text("[] took too much CPU time trying to handle a signal. Reduce the amount of circuit components attached to your [] circuit component.", parent.get_creator_admin(), name));
            return null;
        }

        return output["bitflag"] is var or__1 && Dm.truthy(or__1) ? or__1 : NONE;
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:219
    [ShouldNotSleep]
    public virtual void handle_bitflag_received(datum_port_input port = null, DmList return_values = null)
    {
        if (return_values == null)
            return;

        if (!Dm.truthy(return_values["bitflag"]))
            return_values["bitflag"] = NONE;

        object bitflag = input_signal_ports[port];
        log_admin_circuit(Dm.text("[] received bitflag [] for '[]'", parent.get_creator(), bitflag, registered_signal));
        return_values["bitflag"] = Dm.bitor(return_values["bitflag"], bitflag);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/admin/signal_handler/signal_handler.dm:59
    internal DmList obj_item_circuit_component_signal_handler__populate_options__component_options = Dm.list(
        "Object",
        "Global");
}
