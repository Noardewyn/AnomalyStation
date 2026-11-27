// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/assembly.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/**
 * # Assembly Shell
 *
 * An assembly that triggers and can be triggered by wires.
 */
// tg: code/modules/wiremod/shell/assembly.dm:6
public partial class obj_item_assembly_wiremod : obj_item_assembly
{
    /// Valid types for `power_use_proxy` to be
    // tg: code/modules/wiremod/shell/assembly.dm:17  var/static/list/power_use_override_types
    public static ref DmList power_use_override_types => ref TgState.Current.obj_item_assembly_wiremod__power_use_override_types;

    // tg: code/modules/wiremod/shell/assembly.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_component_shell shell = Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(
            Dm.@new<obj_item_circuit_component_assembly_input>(),
            Dm.@new<obj_item_circuit_component_assembly_output>()
        ), SHELL_CAPACITY_SMALL)) as datum_component_shell;
        RegisterSignal(shell, COMSIG_SHELL_CIRCUIT_ATTACHED, nameof(on_circuit_attached));
        RegisterSignal(shell, COMSIG_SHELL_CIRCUIT_REMOVED, nameof(on_circuit_removed));
        RegisterSignal(this, COMSIG_ASSEMBLY_PRE_ATTACH, nameof(on_pre_attach));
        RegisterSignals(this, Dm.list(COMSIG_ASSEMBLY_ATTACHED, COMSIG_ASSEMBLY_ADDED_TO_BUTTON, COMSIG_ASSEMBLY_ADDED_TO_PRESSURE_PLATE), nameof(on_attached));
        RegisterSignals(this, Dm.list(COMSIG_ASSEMBLY_DETACHED, COMSIG_ASSEMBLY_REMOVED_FROM_BUTTON, COMSIG_ASSEMBLY_REMOVED_FROM_PRESSURE_PLATE), nameof(on_detached));
        return dot;
    }

    // tg: code/modules/wiremod/shell/assembly.dm:31
    [SignalHandler]
    public virtual void on_circuit_attached(datum_component_shell source = null)
    {
        RegisterSignal(source.attached_circuit, COMSIG_CIRCUIT_PRE_POWER_USAGE, nameof(override_circuit_power_usage));
    }

    // tg: code/modules/wiremod/shell/assembly.dm:35
    [SignalHandler]
    public virtual void on_circuit_removed(datum_component_shell source = null)
    {
        UnregisterSignal(source.attached_circuit, COMSIG_CIRCUIT_PRE_POWER_USAGE);
    }

    // tg: code/modules/wiremod/shell/assembly.dm:39
    [SignalHandler]
    public virtual object on_pre_attach(obj_item_circuit_component_wire_bundle source = null, atom holder = null)
    {
        if (!(source is obj_item_circuit_component_wire_bundle))
            return null;
        if (Dm.truthy(source.parent.admin_only))
            return null;
        if (istype(holder.wires, typeof(datum_wires_wire_bundle_component)))
        {
            datum_component_shell shell_comp = GetComponent(typeof(datum_component_shell)) as datum_component_shell;
            if (Dm.truthy(shell_comp.attached_circuit.admin_only))
                return null;
            return COMPONENT_CANCEL_ATTACH;
        }
        return null;
    }

    // tg: code/modules/wiremod/shell/assembly.dm:50
    [SignalHandler]
    public virtual void on_attached(object source = null, atom_movable holder = null)
    {
        if (Dm.truthy(is_type_in_list(holder, power_use_override_types)))
            power_use_proxy = holder;
    }

    // tg: code/modules/wiremod/shell/assembly.dm:55
    [SignalHandler]
    public virtual void on_detached(object source = null)
    {
        power_use_proxy = null;
    }

    // tg: code/modules/wiremod/shell/assembly.dm:59
    [SignalHandler]
    public virtual object override_circuit_power_usage(obj_item_integrated_circuit source = null, object power_to_use = null)
    {
        if (ismachinery(power_use_proxy))
        {
            obj_machinery machine = power_use_proxy as obj_machinery;
            if (!(Dm.truthy(machine.is_operational) && Dm.truthy(machine.anchored)))
                return null;
            if (Dm.truthy(Waiting.NoSleep(machine.use_energy(power_to_use, AREA_USAGE_EQUIP))))
                return COMPONENT_OVERRIDE_POWER_USAGE;
        }
        if (ismecha(power_use_proxy))
        {
            obj_vehicle_sealed_mecha mech = power_use_proxy as obj_vehicle_sealed_mecha;
            if (Dm.truthy(mech.use_energy(power_to_use)))
                return COMPONENT_OVERRIDE_POWER_USAGE;
        }
        if (istype(power_use_proxy, typeof(obj_item_mod_control)))
        {
            obj_item_mod_control modsuit = power_use_proxy as obj_item_mod_control;
            if (Dm.truthy(Waiting.NoSleep(modsuit.subtract_charge(power_to_use))))
                return COMPONENT_OVERRIDE_POWER_USAGE;
        }
        if (istype(power_use_proxy, typeof(obj_item_pressure_plate)))
        {
            if (!Dm.truthy(power_use_proxy.anchored))
                return null;
            area our_area = get_area(power_use_proxy) as area;
            if (Dm.truthy(our_area.apc == null ? null : Waiting.NoSleep(our_area.apc.use_energy(power_to_use, AREA_USAGE_EQUIP))))
                return COMPONENT_OVERRIDE_POWER_USAGE;
        }
        if (iscyborg(power_use_proxy))
        {
            mob_living_silicon_robot borg = power_use_proxy as mob_living_silicon_robot;
            if (Dm.truthy(borg.cell == null ? null : Waiting.NoSleep(((obj_item_stock_parts_power_store)borg.cell).use(power_to_use, TRUE))))
                return COMPONENT_OVERRIDE_POWER_USAGE;
        }
        return null;
    }

    // tg: code/modules/wiremod/shell/assembly.dm:84
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(dot, span_notice(I18n.T("You can also []secure [] by right-clicking it with a screwdriver, even if an integrated circuit is attached.", Dm.truthy(secured) ? "un" : "", this)));
        return dot;
    }

    // This is to bypass removing the circuit with a screwdriver left-click
    // tg: code/modules/wiremod/shell/assembly.dm:89
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        await screwdriver_act(user, tool);
        return null;
    }
}

// tg: code/modules/wiremod/shell/assembly.dm:95
public partial class obj_item_circuit_component_assembly_input : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/assembly.dm:101
    public override async DmTask populate_ports()
    {
        signal = add_output_port("Signal", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/assembly.dm:104
    public override async DmTask register_shell(atom_movable shell = null)
    {
        RegisterSignals(shell, Dm.list(COMSIG_ASSEMBLY_PULSED, COMSIG_ITEM_ATTACK_SELF), nameof(on_pulsed));
    }

    // tg: code/modules/wiremod/shell/assembly.dm:107
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, Dm.list(COMSIG_ASSEMBLY_PULSED, COMSIG_ITEM_ATTACK_SELF));
    }

    // tg: code/modules/wiremod/shell/assembly.dm:110
    [SignalHandler]
    public virtual void on_pulsed(datum source = null, mob pulser = null)
    {
        signal.set_output(COMPONENT_SIGNAL);
    }
}

// tg: code/modules/wiremod/shell/assembly.dm:114
public partial class obj_item_circuit_component_assembly_output : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/assembly.dm:122
    public override async DmTask populate_ports()
    {
        signal = add_input_port("Signal", PORT_TYPE_SIGNAL) as datum_port_input;
    }

    // tg: code/modules/wiremod/shell/assembly.dm:125
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (isassembly(shell))
            attached_assembly = shell as obj_item_assembly;
    }

    // tg: code/modules/wiremod/shell/assembly.dm:130
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        attached_assembly = null;
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/shell/assembly.dm:134
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        Waiting.NoSleep(attached_assembly.pulse());
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/shell/assembly.dm:17
    internal DmList obj_item_assembly_wiremod__power_use_override_types = Dm.list(typeof(obj_machinery), typeof(obj_vehicle_sealed_mecha), typeof(obj_item_mod_control), typeof(obj_item_pressure_plate), typeof(mob_living_silicon_robot));
}
