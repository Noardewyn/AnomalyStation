// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/gun.dm
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
 * # Circuit Gun
 *
 * A gun that lets you fire projectiles to enact circuitry.
 */
// tg: code/modules/wiremod/shell/gun.dm:6
public partial class obj_item_gun_energy_wiremod_gun : obj_item_gun_energy
{
    // tg: code/modules/wiremod/shell/gun.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_component_shell shell = Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(
            Dm.@new<obj_item_circuit_component_wiremod_gun>()
        ), SHELL_CAPACITY_MEDIUM)) as datum_component_shell;

        RegisterSignal(shell, COMSIG_SHELL_CIRCUIT_ATTACHED, nameof(on_circuit_attached));
        return dot;
    }

    // tg: code/modules/wiremod/shell/gun.dm:72
    [SignalHandler]
    public virtual void on_circuit_attached(datum_component_shell source = null)
    {
        if (istype(source, typeof(datum_component_shell)))
        {
            datum_component_shell comp = source;
            obj_item_integrated_circuit circuit = comp.attached_circuit;
            if (circuit.cell == null)
                return;
            object transferred = Waiting.NoSleep(cell.give(min(0.1f * STANDARD_CELL_CHARGE, circuit.cell.charge)));
            if (Dm.truthy(transferred))
                Waiting.NoSleep(circuit.cell.use(transferred, TRUE));
        }
    }
}

// tg: code/modules/wiremod/shell/gun.dm:44
public partial class obj_item_circuit_component_wiremod_gun : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/gun.dm:54
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        shooter = add_output_port("Shooter", PORT_TYPE_ATOM) as datum_port_output;
        shot = add_output_port("Shot Entity", PORT_TYPE_ATOM) as datum_port_output;
        signal = add_output_port("Shot", PORT_TYPE_SIGNAL) as datum_port_output;
        return dot;
    }

    // tg: code/modules/wiremod/shell/gun.dm:60
    public override async DmTask register_shell(atom_movable shell = null)
    {
        RegisterSignal(shell, COMSIG_PROJECTILE_ON_HIT, nameof(handle_shot));
        if (istype(shell, typeof(obj_item_gun_energy)))
            RegisterSignal(shell, COMSIG_GUN_CHAMBER_PROCESSED, nameof(handle_chamber));
    }

    // tg: code/modules/wiremod/shell/gun.dm:65
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, Dm.list(COMSIG_PROJECTILE_ON_HIT, COMSIG_GUN_CHAMBER_PROCESSED));
    }

    /**
     * Called when the shell item shoots something
     */
    // tg: code/modules/wiremod/shell/gun.dm:87
    [SignalHandler]
    public virtual void handle_shot(atom source = null, mob firer = null, atom target = null, object angle = null)
    {
        playsound(source, SFX_TERMINAL_TYPE, 25, FALSE);
        shooter.set_output(firer);
        shot.set_output(target);
        signal.set_output(COMPONENT_SIGNAL);
    }

    /**
     * Called when the shell item processes a new chamber
     */
    // tg: code/modules/wiremod/shell/gun.dm:98
    [SignalHandler]
    public virtual void handle_chamber(atom source = null)
    {
        if (parent?.cell == null)
            return;
        obj_item_gun_energy fired_gun = source as obj_item_gun_energy;
        object transferred = Waiting.NoSleep(fired_gun.cell.give(min(0.1f * STANDARD_CELL_CHARGE, parent.cell.charge)));
        if (Dm.truthy(transferred))
            Waiting.NoSleep(parent.cell.use(transferred, TRUE));
    }
}
