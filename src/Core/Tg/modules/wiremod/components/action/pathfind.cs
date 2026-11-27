// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/action/pathfind.dm
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
 * # Pathfinding component
 *
 * Calcualtes a path, returns a list of entities. Each entity is the next step in the path. Can be used with the direction component to move.
 */
// tg: code/modules/wiremod/components/action/pathfind.dm:6
public partial class obj_item_circuit_component_pathfind : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/action/pathfind.dm:31
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        // Not necessary to show the same path cooldown, since it doesn't change much for the player
        Dm.append(dot, create_ui_notice(Dm.text("Pathfinding Cooldown: []", DisplayTimeText(different_path_cooldown)), "orange", "stopwatch"));
        Dm.append(dot, create_ui_notice(Dm.text("Maximum Range: [] tiles", max_range), "orange", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/action/pathfind.dm:37
    public override async DmTask populate_ports()
    {
        input_X = add_input_port("Target X", PORT_TYPE_NUMBER, trigger: null) as datum_port_input;
        input_Y = add_input_port("Target Y", PORT_TYPE_NUMBER, trigger: null) as datum_port_input;
        id_card = add_input_port("ID Card", PORT_TYPE_ATOM, trigger: null) as datum_port_input;

        output = add_output_port("Next step", PORT_TYPE_ATOM) as datum_port_output;
        finished = add_output_port("Arrived to destination", PORT_TYPE_SIGNAL) as datum_port_output;
        failed = add_output_port("Failed", PORT_TYPE_SIGNAL) as datum_port_output;
        reason_failed = add_output_port("Fail reason", PORT_TYPE_STRING) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/action/pathfind.dm:47
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        Waiting.Detach(perform_pathfinding(port));
        return null;
    }

    // tg: code/modules/wiremod/components/action/pathfind.dm:50
    public virtual async DmTask perform_pathfinding(datum_port_input port = null)
    {
        object target_X = input_X.value;
        if (target_X == null)
            return;

        object target_Y = input_Y.value;
        if (target_Y == null)
            return;

        object access = Dm.list();
        if (isidcard(id_card.value))
        {
            obj_item_card_id id = id_card.value as obj_item_card_id;
            access = id.GetAccess();
        }
        else if (Dm.truthy(id_card.value))
        {
            failed.set_output(COMPONENT_SIGNAL);
            reason_failed.set_output("Object marked is not an ID! Using no ID instead.");
        }

        // Get both the current turf and the destination's turf
        turf current_turf = get_location() as turf;
        turf destination = locate(Dm.num(target_X), Dm.num(target_Y), Dm.num(current_turf?.z));

        // We're already here! No need to do anything.
        if (current_turf == destination)
        {
            finished.set_output(COMPONENT_SIGNAL);
            old_dest = null;
            TIMER_COOLDOWN_END(parent, COOLDOWN_CIRCUIT_PATHFIND_SAME);
            next_turf = null;
            return;
        }

        obj_item_integrated_circuit cd_source = parent;
        // If we're going to the same place and the cooldown hasn't subsided, we're probably on the same path as before
        if (destination == old_dest && Dm.truthy(TIMER_COOLDOWN_RUNNING(parent, COOLDOWN_CIRCUIT_PATHFIND_SAME)))
        {
            // Check if the current turf is the same as the current turf we're supposed to be in. If so, then we set the next step as the next turf on the list
            if (current_turf == next_turf)
            {
                popleft(path);
                next_turf = get_turf(path[1]);
                output.set_output(next_turf);

                // Restart the cooldown since we don't need a new path ( TIMER_COOLDOWN_START might restart the timer by itself and i dont need to call TIMER_COOLDOWN_END, but better safe than sorry )
                TIMER_COOLDOWN_END(parent, COOLDOWN_CIRCUIT_PATHFIND_SAME);
                // TIMER_COOLDOWN_START (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
                LAZYSET(ref cd_source.cooldowns, COOLDOWN_CIRCUIT_PATHFIND_SAME, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), cd_source, COOLDOWN_CIRCUIT_PATHFIND_SAME), same_path_cooldown, file: "code/modules/wiremod/components/action/pathfind.dm", line: 90));
            }
        }
        else // Either we're not going to the same place or the cooldown is over. Either way, we need a new path
        {
            if (destination != old_dest && Dm.truthy(TIMER_COOLDOWN_RUNNING(parent, COOLDOWN_CIRCUIT_PATHFIND_DIF)))
            {
                failed.set_output(COMPONENT_SIGNAL);
                reason_failed.set_output("Cooldown still active!");
                return;
            }

            TIMER_COOLDOWN_END(parent, COOLDOWN_CIRCUIT_PATHFIND_SAME);

            old_dest = destination;
            path = await get_path_to(this, destination, max_range, access: access) as DmList;
            if (length(path) == 0 || path == null) // Check if we can even path there
            {
                next_turf = null;
                failed.set_output(COMPONENT_SIGNAL);
                reason_failed.set_output("Can't go there!");
                return;
            }
            else
            {
                // TIMER_COOLDOWN_START (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
                LAZYSET(ref cd_source.cooldowns, COOLDOWN_CIRCUIT_PATHFIND_DIF, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), cd_source, COOLDOWN_CIRCUIT_PATHFIND_DIF), different_path_cooldown, file: "code/modules/wiremod/components/action/pathfind.dm", line: 108));
                next_turf = get_turf(path[1]);
                output.set_output(next_turf);
            }
            // TIMER_COOLDOWN_START (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
            LAZYSET(ref cd_source.cooldowns, COOLDOWN_CIRCUIT_PATHFIND_SAME, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), cd_source, COOLDOWN_CIRCUIT_PATHFIND_SAME), same_path_cooldown, file: "code/modules/wiremod/components/action/pathfind.dm", line: 111));
        }
    }
}
