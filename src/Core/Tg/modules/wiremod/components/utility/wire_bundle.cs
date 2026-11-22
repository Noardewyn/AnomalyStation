// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/utility/wire_bundle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

// tg: code/modules/wiremod/components/utility/wire_bundle.dm:1
public partial class obj_item_circuit_component_wire_bundle : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/utility/wire_bundle.dm:12
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice("Port count is proportional to shell capacity.", "orange", "plug"));
        Dm.append(dot, create_ui_notice(Dm.text("Max port count: []", MAX_WIRE_COUNT), "orange", "plug"));
        Dm.append(dot, create_ui_notice("Incompatible with assembly shell.", "red", "plug-circle-xmark"));
        return dot;
    }

    // tg: code/modules/wiremod/components/utility/wire_bundle.dm:18
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (isassembly(shell) && !Dm.truthy(parent.admin_only))
            return;
        if (shell.wires != null) // Don't add wires to shells that already have some.
            return;
        tracked_wires = Dm.@new<datum_wires_wire_bundle_component>(shell);
        shell.set_wires(tracked_wires);
        foreach (object wire in Dm.each(tracked_wires.wires))
        {
            wire_input_ports[add_input_port(Dm.text("Pulse []", wire), PORT_TYPE_SIGNAL)] = wire;
            wire_output_ports[wire] = add_output_port(Dm.text("[] Pulsed", wire), PORT_TYPE_SIGNAL);
        }
        RegisterSignal(tracked_wires, COMSIG_PULSE_WIRE, nameof(on_pulse_wire));
        RegisterSignal(shell, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_shell_requesting_context));
        RegisterSignal(shell, COMSIG_ATOM_ITEM_INTERACTION_SECONDARY, nameof(on_shell_secondary_interaction));
    }

    // tg: code/modules/wiremod/components/utility/wire_bundle.dm:33
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        await base.unregister_shell(shell);
        if (shell.wires != tracked_wires)
            return;
        UnregisterSignal(shell, Dm.list(COMSIG_ATOM_ITEM_INTERACTION_SECONDARY, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM));
        foreach (object color in Dm.each(tracked_wires.colors))
        {
            obj_item_assembly assembly = await tracked_wires.detach_assembly(color) as obj_item_assembly;
            if (assembly != null)
                await assembly.forceMove(drop_location() as atom);
        }
        shell.set_wires(null);
        QDEL_NULL(ref tracked_wires);
        foreach (datum_port_input in_port in Dm.each<datum_port_input>(wire_input_ports))
            remove_input_port(in_port);
        foreach (object wire in Dm.each(wire_output_ports))
        {
            datum_port_output out_port = wire_output_ports[wire] as datum_port_output;
            remove_output_port(out_port);
        }
        wire_input_ports.Cut();
        wire_output_ports.Cut();
    }

    // tg: code/modules/wiremod/components/utility/wire_bundle.dm:52
    public override async DmTask<object> add_to(obj_item_integrated_circuit added_to = null)
    {
        object dot = await base.add_to(added_to);
        if (HAS_TRAIT(added_to, TRAIT_COMPONENT_WIRE_BUNDLE))
            return FALSE;
        ADD_TRAIT(added_to, TRAIT_COMPONENT_WIRE_BUNDLE, REF(this));
        return dot;
    }

    // tg: code/modules/wiremod/components/utility/wire_bundle.dm:58
    public override void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        base.removed_from(removed_from);
        REMOVE_TRAIT(removed_from, TRAIT_COMPONENT_WIRE_BUNDLE, REF(this));
        base.removed_from(removed_from); // tg calls the parent twice (`. = ..()` and `return ..()`), kept
    }

    // tg: code/modules/wiremod/components/utility/wire_bundle.dm:63
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object dot = base.input_received(port, return_values);
        if (port == null)
            return dot;
        object wire = wire_input_ports[port];
        if (!Dm.truthy(wire))
            return dot;
        if (Dm.truthy(tracked_wires.is_cut(wire)))
            return dot;
        object color = tracked_wires.get_color_of_wire(wire);
        obj_item_assembly attached = tracked_wires.get_attached(color) as obj_item_assembly;
        if (attached != null)
            Waiting.NoSleep(attached.activate());
        return dot;
    }

    // tg: code/modules/wiremod/components/utility/wire_bundle.dm:76
    [SignalHandler]
    public virtual void on_pulse_wire(object source = null, object wire = null)
    {
        if (Dm.truthy(tracked_wires.is_cut(wire)))
            return;
        datum_port_output port = wire_output_ports[wire] as datum_port_output;
        if (!istype(port, typeof(datum_port_output)))
            return;
        port.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/components/utility/wire_bundle.dm:85
    public virtual object can_access_wires(atom source = null)
    {
        if (ismachinery(source))
        {
            obj_machinery machine = source as obj_machinery;
            return machine.panel_open;
        }
        return TRUE;
    }

    // tg: code/modules/wiremod/components/utility/wire_bundle.dm:91
    [SignalHandler]
    public virtual object on_shell_requesting_context(atom source = null, DmList context = null, obj_item item = null, mob user = null)
    {
        object dot = NONE;

        if (!Dm.truthy(is_wire_tool(item)))
            return dot;
        if (!Dm.truthy(can_access_wires(source)))
            return dot;
        context[SCREENTIP_CONTEXT_RMB] = "Interact with wires";
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/modules/wiremod/components/utility/wire_bundle.dm:102
    [SignalHandler]
    public virtual object on_shell_secondary_interaction(atom source = null, mob user = null, obj_item tool = null)
    {
        if (!Dm.truthy(is_wire_tool(tool)))
            return null;
        if (!Dm.truthy(can_access_wires(source)))
            return null;
        datum_component_shell shell_comp = source.GetComponent(typeof(datum_component_shell)) as datum_component_shell;
        if (Dm.truthy(shell_comp.locked))
        {
            source.balloon_alert(user, I18n.T("locked!"));
            return ITEM_INTERACT_FAILURE;
        }
        if (Dm.eq(Waiting.NoSleep(source.attempt_wire_interaction(user)), WIRE_INTERACTION_BLOCK))
            return ITEM_INTERACT_BLOCKING;
        return null;
    }
}
