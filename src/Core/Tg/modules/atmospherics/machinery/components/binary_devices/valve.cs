// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/binary_devices/valve.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the manual and digital valves): the picture, set_open, the reconcilation of both sides when open,
// interact with its 1 second switch, the valve sounds, and the digital valve's init and picture. Deferred leaves:
// the investigate logs (M10). The digital valve's USB port and its circuit component (/obj/item/circuit_component/digital_valve)
// are live. The var blocks are generated.

/*
It's like a regular ol' straight pipe, but you can turn it on and off.
*/

// tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:7
public partial class obj_machinery_atmospherics_components_binary_valve : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:4  #define MANUAL_VALVE "m" (file-local, #undef at :194)
    protected const string MANUAL_VALVE = "m";
    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:5  #define DIGITAL_VALVE "d" (file-local, #undef at :195)
    protected const string DIGITAL_VALVE = "d";

    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:26
    [DmParams("animation")]
    public override void update_icon_nopipes(params object[] __extra)
    {
        object animation = Dm.arg(__extra, 0);
        animation ??= FALSE;
        normalize_cardinal_directions();
        if (Dm.truthy(animation))
        {
            flick(Dm.text("[]valve_[][]-[]", valve_type, on, Dm.truthy(on) ? FALSE : TRUE, set_overlay_offset(piping_layer)), this);
            playsound(this, Dm.resource("sound/effects/valve_opening.ogg"), 50); // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:30
        }
        icon_state = Dm.text("[]valve_[]-[]", valve_type, Dm.truthy(on) ? "on" : "off", set_overlay_offset(piping_layer));
    }

    /**
     * Called by finish_interact(), switch between open and closed, reconcile the air between two pipelines
     */
    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:36
    public virtual async DmTask<object> set_open(object to_open = null)
    {
        if (Dm.eq(on, to_open))
            return null;
        if (Dm.truthy(_listen_lookup?[COMSIG_VALVE_SET_OPEN])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:7)
            _SendSignal(COMSIG_VALVE_SET_OPEN, Dm.list(this, to_open));
        object dot = on;
        set_on(to_open);
        if (Dm.truthy(on))
        {
            playsound(this, Dm.resource("sound/effects/gas_hissing.ogg"), 50); // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:43
            update_parents();
            datum_pipeline parent1 = parents[1] as datum_pipeline;
            await parent1.reconcile_air();
            investigate_log(Dm.text("was opened by []", usr != null ? key_name(usr) : "a remote signal"), INVESTIGATE_ATMOS);
            balloon_alert_to_viewers("valve opened");
            vent_movement = Dm.bitor(vent_movement, VENTCRAWL_ALLOWED);
        }
        else
        {
            investigate_log(Dm.text("was closed by []", usr != null ? key_name(usr) : "a remote signal"), INVESTIGATE_ATMOS);
            balloon_alert_to_viewers("valve closed");
            vent_movement = Dm.bitand(vent_movement, Dm.bitnot(VENTCRAWL_ALLOWED));
        }
        return dot;
    }

    // This is what handles the actual functionality of combining 2 pipenets when the valve is open
    // Basically when a pipenet updates it will consider both sides to be the same for the purpose of the gas update
    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:58
    public override async DmTask<object> return_pipenets_for_reconcilation(datum_pipeline requester = null)
    {
        object dot = await base.return_pipenets_for_reconcilation(requester);
        if (!Dm.truthy(on))
            return dot;
        DmList list__1 = dot as DmList;
        Dm.combine(ref list__1, parents[1]);
        Dm.combine(ref list__1, parents[2]);
        return list__1;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:65
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:66
        add_fingerprint(usr);
        if (Dm.truthy(switching))
            return null;
        update_icon_nopipes(TRUE);
        switching = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_interact)), 1 * SECONDS, file: "code/modules/atmospherics/machinery/components/binary_devices/valve.dm", line: 71);
        return null;
    }

    /**
     * Called by iteract() after a 1 second timer, calls toggle(), allows another interaction with the component.
     */
    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:76
    public virtual async DmTask finish_interact()
    {
        await set_open(Dm.truthy(on) ? FALSE : TRUE);
        switching = FALSE;
    }
}

// tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:80
public partial class obj_machinery_atmospherics_components_binary_valve_digital : obj_machinery_atmospherics_components_binary_valve
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:90
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_usb_port), typecacheof(Dm.list(typeof(obj_item_circuit_component_digital_valve)), only_root_path: TRUE)));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:149
    [DmParams("animation")]
    public override void update_icon_nopipes(params object[] __extra)
    {
        if (!Dm.truthy(is_operational))
        {
            normalize_cardinal_directions();
            icon_state = Dm.text("dvalve_nopower-[]", set_overlay_offset(piping_layer));
            return;
        }
        base.update_icon_nopipes(__extra);
    }
}

// tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:94
public partial class obj_item_circuit_component_digital_valve : obj_item_circuit_component
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:112
    public override async DmTask populate_ports()
    {
        open = add_input_port("Open", PORT_TYPE_SIGNAL) as datum_port_input;
        close = add_input_port("Close", PORT_TYPE_SIGNAL) as datum_port_input;

        is_open = add_output_port("Is Open", PORT_TYPE_BOOLEAN) as datum_port_output;
        opened = add_output_port("Opened", PORT_TYPE_SIGNAL) as datum_port_output;
        closed = add_output_port("Closed", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:120
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_atmospherics_components_binary_valve_digital)))
        {
            attached_valve = shell as obj_machinery_atmospherics_components_binary_valve_digital;
            RegisterSignal(attached_valve, COMSIG_VALVE_SET_OPEN, nameof(handle_valve_toggled));
        }
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:126
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        UnregisterSignal(attached_valve, COMSIG_VALVE_SET_OPEN);
        attached_valve = null;
        return base.unregister_usb_parent(shell);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:131
    [SignalHandler]
    public virtual void handle_valve_toggled(datum source = null, object on = null)
    {
        is_open.set_output(on);
        if (Dm.truthy(on))
            opened.set_output(COMPONENT_SIGNAL);
        else
            closed.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/valve.dm:139
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (attached_valve == null)
            return null;

        if (COMPONENT_TRIGGERED_BY(open, port) && !Dm.truthy(attached_valve.on))
            Waiting.NoSleep(attached_valve.set_open(TRUE));
        if (COMPONENT_TRIGGERED_BY(close, port) && Dm.truthy(attached_valve.on))
            Waiting.NoSleep(attached_valve.set_open(FALSE));
        return null;
    }
}
