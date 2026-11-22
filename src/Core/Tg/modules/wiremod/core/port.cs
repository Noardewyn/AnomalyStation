// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/core/port.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the circuit components' ports: their values and datatypes, the wires from an output
// to the inputs it feeds (a value reaches the inputs one SScircuit_component fire later) and the type checks that break a wire
// whose types no longer match. /datum/port's `order` stays hand-written (an earlier reader declared it here, rules 10.2);
// its other vars and the input and output ports' var blocks are generated (D111).

/**
 * # Component Port
 *
 * A port used by a component. Connects to other ports.
 */
// tg: code/modules/wiremod/core/port.dm:6
[DmPath("/datum/port")]
public partial class datum_port : datum
{
    /// The weight of the port. Determines the
    // tg: code/modules/wiremod/core/port.dm:26 (null-used: rules 3.2 V4)
    public float? order = 1;

    // tg: code/modules/wiremod/core/port.dm:28
    [DmParams("to_connect", "name", "datatype", "order")]
    public override async DmTask New(params object[] __extra)
    {
        obj_item_circuit_component to_connect = Dm.arg<obj_item_circuit_component>(__extra, 0);
        object name = Dm.arg(__extra, 1);
        object datatype = Dm.arg(__extra, 2);
        float? order = Dm.arg<float?>(__extra, 3) ?? 1;
        if (to_connect == null)
        {
            qdel(this);
            return;
        }
        await base.New(__extra);
        connected_component = to_connect;
        this.name = name;
        this.order = order;
        set_datatype(datatype);
    }

    // tg: code/modules/wiremod/core/port.dm:38
    public override object Destroy(float? force = null)
    {
        disconnect_all();
        connected_component = null;
        datatype_handler = null;
        return base.Destroy(force);
    }

    /**
     * Sets the port's value to value.
     * Casts to the port's datatype (e.g. number -> string), and assumes this can be done.
     */
    // tg: code/modules/wiremod/core/port.dm:48
    public virtual void set_value(object value = null, float? force = null)
    {
        force ??= FALSE;
        if (isweakref(value))
        {
            datum_weakref reference_to_obj = value as datum_weakref;
            value = reference_to_obj.resolve();
        }

        if (!Dm.eq(this.value, value) || Dm.truthy(force))
        {
            if (isdatum(this.value))
                UnregisterSignal(this.value as datum, COMSIG_QDELETING);
            if (Dm.truthy(datatype_handler.is_extensive))
                this.value = datatype_handler.convert_value_extensive(this, value, force);
            else
                this.value = datatype_handler.convert_value(this, value, force);
            if (isdatum(value))
                RegisterSignal(value as datum, COMSIG_QDELETING, nameof(null_value));
        }
        SEND_SIGNAL(COMSIG_PORT_SET_VALUE, Dm.list(this, value));
    }

    /**
     * Sets the datatype of the port.
     *
     * Arguments:
     * * new_type - The type this port is to be set to.
     */
    // tg: code/modules/wiremod/core/port.dm:83
    public virtual void set_datatype(object type_to_set = null)
    {
        if (Dm.eq(type_to_set, datatype))
            return;

        datatype_handler?.on_loss(this);
        datatype_handler = null;

        datum_circuit_datatype handler = GLOB.circuit_datatypes[type_to_set] as datum_circuit_datatype;
        if (handler == null || !Dm.truthy(handler.is_compatible(this)))
        {
            type_to_set = PORT_TYPE_ANY;
            handler = GLOB.circuit_datatypes[type_to_set] as datum_circuit_datatype;
            // We can't leave this port without a type or else it'll just keep spewing out unnecessary and unneeded runtimes as well as leaving the circuit in a broken state.
            stack_trace(Dm.text("[] port attempted to be set to an incompatible datatype! (target datatype to set: [])", this, type_to_set), "code/modules/wiremod/core/port.dm", 96);
        }

        datatype = type_to_set;
        datatype_handler = handler;
        color = datatype_handler.color;
        datatype_handler.on_gain(this);
        this.value = null;
        SEND_SIGNAL(COMSIG_PORT_SET_TYPE, Dm.list(this, type_to_set));
        if (connected_component?.parent != null)
            SStgui?.update_uis(connected_component.parent); // rules 10.6: a test harness world may lack SStgui
    }

    /**
     * Returns the data from the datatype
     */
    // tg: code/modules/wiremod/core/port.dm:115
    public virtual object datatype_ui_data(mob user = null)
    {
        return datatype_handler.datatype_ui_data(this);
    }

    /**
     * Disconnects a port from all other ports.
     *
     * Called by [/obj/item/circuit_component] whenever it is disconnected from
     * an integrated circuit
     */
    // tg: code/modules/wiremod/core/port.dm:133
    public virtual void disconnect_all()
    {
        value = null;
        SEND_SIGNAL(COMSIG_PORT_DISCONNECT, Dm.list(this));
    }

    /// Do our part in setting all source references anywhere to null.
    // tg: code/modules/wiremod/core/port.dm:150
    [SignalHandler]
    public virtual void on_value_qdeleting(datum source = null)
    {
        if (Dm.eq(value, source))
            value = null;
        else
            stack_trace(Dm.text("Impossible? [] should only receive COMSIG_QDELETING from an atom currently in the port, not [].", this, source), "code/modules/wiremod/core/port.dm", 155);
    }

    /// Signal handler proc to null the input if an atom is deleted. An update is not sent because this was not set by anything.
    // tg: code/modules/wiremod/core/port.dm:228
    [SignalHandler]
    public virtual void null_value(datum source = null)
    {
        if (Dm.eq(value, source))
            value = null;
    }
}

/**
 * # Output Port
 *
 * An output port that many input ports can connect to
 *
 * Sends a signal whenever the output value is changed
 */
// tg: code/modules/wiremod/core/port.dm:125
public partial class datum_port_output : datum_port
{
    // tg: code/modules/wiremod/core/port.dm:74
    public virtual void set_output(object value = null)
    {
        set_value(value);
    }
}

/**
 * # Input Port
 *
 * An input port remembers connected output ports.
 *
 * Registers the PORT_SET_VALUE signal on each connected port,
 * and keeps its value equal to the last such signal received.
 */
// tg: code/modules/wiremod/core/port.dm:165
public partial class datum_port_input : datum_port
{
    // tg: code/modules/wiremod/core/port.dm:172
    [DmParams("to_connect", "name", "datatype", "order", "trigger", "default")]
    public override async DmTask New(params object[] __extra)
    {
        object trigger = Dm.arg(__extra, 4);
        object @default = Dm.arg(__extra, 5);
        await base.New(__extra);
        if (QDELETED(this)) // ..() qdel'd a port with no component: tg's set_value below then runtimes on the null datatype handler
            return;
        set_value(@default);
        if (Dm.truthy(trigger))
            this.trigger = trigger;
    }

    /**
     * Updates the value of the input and calls input_received on the connected component
     */
    // tg: code/modules/wiremod/core/port.dm:67
    public virtual void set_input(object value = null, DmList return_values = null)
    {
        if (QDELETED(this)) //Pain
            return;
        set_value(value);
        if (Dm.truthy(trigger))
            connected_component.trigger_component(this, return_values);
    }

    // The earlier of the two definitions of /datum/port/input/set_datatype (rules 7.5): the later one's ..() calls it.
    // tg: code/modules/wiremod/core/port.dm:107
    public void set_datatype__d0(object new_type = null)
    {
        foreach (datum_port_output output in Dm.each_cast<datum_port_output>(connected_ports))
            check_type(output);
        base.set_datatype(new_type);
    }

    // tg: code/modules/wiremod/core/port.dm:195
    [DmParams("new_type")]
    public override void set_datatype(object type_to_set = null)
    {
        object new_type = type_to_set;
        set_datatype__d0(new_type);
        foreach (datum_port_output port in Dm.each_cast<datum_port_output>(connected_ports))
            check_type(port);
    }

    // tg: code/modules/wiremod/core/port.dm:137
    public override void disconnect_all()
    {
        base.disconnect_all();
        foreach (datum_port_output output in Dm.each_cast<datum_port_output>(connected_ports))
            disconnect(output);
    }

    // tg: code/modules/wiremod/core/port.dm:142
    [SignalHandler]
    public virtual void disconnect(datum_port_output output = null)
    {
        LAZYREMOVE(ref connected_ports, output);
        UnregisterSignal(output, COMSIG_PORT_SET_VALUE);
        UnregisterSignal(output, COMSIG_PORT_SET_TYPE);
        UnregisterSignal(output, COMSIG_PORT_DISCONNECT);
    }

    /**
     * Connects an input port to an output port.
     *
     * Arguments:
     * * output - The output port to connect to.
     */
    // tg: code/modules/wiremod/core/port.dm:184
    public virtual void connect(datum_port_output output = null)
    {
        if (Dm.@in(output, connected_ports))
            return;
        LAZYADD(ref connected_ports, output);
        RegisterSignal(output, COMSIG_PORT_SET_VALUE, nameof(receive_value));
        RegisterSignal(output, COMSIG_PORT_SET_TYPE, nameof(check_type));
        RegisterSignal(output, COMSIG_PORT_DISCONNECT, nameof(disconnect));
        // For signals, we don't update the input to prevent sending a signal when connecting ports.
        if (!Dm.truthy(Dm.bitand(datatype_handler.datatype_flags, DATATYPE_FLAG_AVOID_VALUE_UPDATE)))
            set_input(output.value);
    }

    /**
     * Determines if a datatype is compatible with another port of a different type.
     *
     * Arguments:
     * * other_datatype - The datatype to check
     */
    // tg: code/modules/wiremod/core/port.dm:206
    public virtual object can_receive_from_datatype(object datatype_to_check = null)
    {
        return datatype_handler.can_receive_from_datatype(datatype_to_check);
    }

    /**
     * Determines if a datatype is compatible with another port of a different type.
     *
     * Arguments:
     * * other_datatype - The datatype to check
     */
    // tg: code/modules/wiremod/core/port.dm:215
    public virtual object handle_manual_input(mob user = null, object manual_input = null)
    {
        if (Dm.truthy(Dm.bitand(datatype_handler.datatype_flags, DATATYPE_FLAG_ALLOW_MANUAL_INPUT)))
            return datatype_handler.handle_manual_input(this, user, manual_input);
        return null;
    }

    /**
     * Mirror value updates from connected output ports after an input_receive_delay.
     */
    // tg: code/modules/wiremod/core/port.dm:223
    [SignalHandler]
    public virtual void receive_value(datum_port_output output = null, object value = null)
    {
        SScircuit_component.add_callback(this, Dm.@new<datum_callback>(this, nameof(set_input), value));
    }

    /**
     * Handle type updates from connected output ports, breaking uncastable connections.
     */
    // tg: code/modules/wiremod/core/port.dm:236
    [SignalHandler]
    public virtual void check_type(datum_port_output output = null)
    {
        if (!Dm.truthy(can_receive_from_datatype(output.datatype)))
            disconnect(output);
    }
}
