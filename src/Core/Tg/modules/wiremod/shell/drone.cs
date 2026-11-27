// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/drone.dm
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
 * # Drone
 *
 * A movable mob that can be fed inputs on which direction to travel.
 */
// tg: code/modules/wiremod/shell/drone.dm:6
public partial class mob_living_circuit_drone : mob_living
{
    // tg: code/modules/wiremod/shell/drone.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(
            Dm.@new<obj_item_circuit_component_bot_circuit>(),
            Dm.@new<obj_item_circuit_component_remotecam_drone>()
        ), SHELL_CAPACITY_LARGE));
        return dot;
    }

    // tg: code/modules/wiremod/shell/drone.dm:24
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (health < maxHealth)
        {
            if (health > maxHealth / 3)
                Dm.append(dot, I18n.T("[]'s parts look loose.", this));
            else
                Dm.append(dot, I18n.T("[]'s parts look very loose!", this));
        }
        else
            Dm.append(dot, I18n.T("[] is in pristine condition.", this));
        return dot;
    }

    // tg: code/modules/wiremod/shell/drone.dm:34
    public override async DmTask updatehealth()
    {
        await base.updatehealth();
        if (health < 0)
            await gib();
    }

    // tg: code/modules/wiremod/shell/drone.dm:39
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.welder_act(user, tool);
        if (health == maxHealth)
        {
            balloon_alert(user, I18n.T("already at maximum integrity!"));
            return TRUE;
        }
        if (Dm.truthy(await tool.use_tool(this, user, 1 * SECONDS, volume: 50)))
            await heal_overall_damage(50, 50);
        return TRUE;
    }
}

// tg: code/modules/wiremod/shell/drone.dm:48
public partial class obj_item_circuit_component_bot_circuit : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/drone.dm:67
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (ismob(shell))
            RegisterSignal(shell, COMSIG_PROCESS_BORGCHARGER_OCCUPANT, nameof(on_borg_charge));
    }

    // tg: code/modules/wiremod/shell/drone.dm:72
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, COMSIG_PROCESS_BORGCHARGER_OCCUPANT);
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/shell/drone.dm:76
    [SignalHandler]
    public virtual void on_borg_charge(datum source = null, datum_callback charge_cell = null, object seconds_per_tick = null)
    {
        if (parent.cell == null)
            return;
        Waiting.NoSleep(charge_cell.Invoke(parent.cell, seconds_per_tick));
    }

    // tg: code/modules/wiremod/shell/drone.dm:82
    public override async DmTask populate_ports()
    {
        north = add_input_port("Move North", PORT_TYPE_SIGNAL) as datum_port_input;
        east = add_input_port("Move East", PORT_TYPE_SIGNAL) as datum_port_input;
        south = add_input_port("Move South", PORT_TYPE_SIGNAL) as datum_port_input;
        west = add_input_port("Move West", PORT_TYPE_SIGNAL) as datum_port_input;
    }

    // tg: code/modules/wiremod/shell/drone.dm:88
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        mob_living shell = parent.shell as mob_living;
        if (!(shell is mob_living) || IS_UNCONSCIOUS_OR_CRIT(shell))
            return null;

        float direction = 0;

        if (COMPONENT_TRIGGERED_BY(north, port) && north_delay <= world.time) // COOLDOWN_FINISHED
        {
            direction = NORTH;
            north_delay = world.time + move_delay; // COOLDOWN_START
        }
        else if (COMPONENT_TRIGGERED_BY(east, port) && east_delay <= world.time) // COOLDOWN_FINISHED
        {
            direction = EAST;
            east_delay = world.time + move_delay; // COOLDOWN_START
        }
        else if (COMPONENT_TRIGGERED_BY(south, port) && south_delay <= world.time) // COOLDOWN_FINISHED
        {
            direction = SOUTH;
            south_delay = world.time + move_delay; // COOLDOWN_START
        }
        else if (COMPONENT_TRIGGERED_BY(west, port) && west_delay <= world.time) // COOLDOWN_FINISHED
        {
            direction = WEST;
            west_delay = world.time + move_delay; // COOLDOWN_START
        }

        if (!Dm.truthy(direction))
            return null;

        if (ismovable(shell.loc)) //Inside an object, tell it we moved
        {
            atom loc_atom = shell.loc;
            Waiting.NoSleep(loc_atom.relaymove(shell, direction));
            return null;
        }

        if (Dm.truthy(Waiting.NoSleep(shell.Process_Spacemove(direction))))
            Waiting.NoSleep(shell.Move(get_step(shell, direction), direction));
        return null;
    }
}
