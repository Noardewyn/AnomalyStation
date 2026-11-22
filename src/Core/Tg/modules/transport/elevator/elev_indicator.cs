// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/elevator/elev_indicator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D325 (multi-z, elevators): the whole file. The emissive overlays are lighting (DEVIATION DV12).

/**
 * An indicator display aka an elevator hall lantern w/ floor number
 */
// tg: code/modules/transport/elevator/elev_indicator.dm:4
public partial class obj_machinery_lift_indicator : obj_machinery
{
    // tg: code/modules/transport/elevator/elev_indicator.dm:40
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();

        foreach (datum_transport_controller_linear possible_match in Dm.each<datum_transport_controller_linear>(SStransport.transports_by_type[TRANSPORT_TYPE_ELEVATOR]))
        {
            if (!Dm.eq(possible_match.specific_transport_id, linked_elevator_id))
                continue;

            lift_ref = WEAKREF(possible_match) as datum_weakref;
            RegisterSignal(possible_match, COMSIG_LIFT_SET_DIRECTION, nameof(on_lift_direction));
        }
    }

    // tg: code/modules/transport/elevator/elev_indicator.dm:50
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (!Dm.truthy(is_operational))
        {
            Dm.append(ref dot, span_notice(I18n.T("The display is dark.")));
            return dot;
        }

        string dirtext;
        if (Dm.eq(current_lift_direction, UP))
            dirtext = "travelling upwards";
        else if (Dm.eq(current_lift_direction, DOWN))
            dirtext = "travelling downwards";
        else
            dirtext = "stopped";

        Dm.append(ref dot, span_notice(I18n.T("The elevator is at floor [], [].", current_lift_floor, dirtext)));
        return dot;
    }

    /**
     * Update state, and only process if elevator is moving.
     */
    // tg: code/modules/transport/elevator/elev_indicator.dm:72
    public virtual void on_lift_direction(datum source = null, object direction = null)
    {
        datum_transport_controller_linear lift = lift_ref?.resolve() as datum_transport_controller_linear;
        if (lift == null)
            return;

        set_lift_state(direction, current_lift_floor);
        update_operating();
    }

    // tg: code/modules/transport/elevator/elev_indicator.dm:82
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        await base.on_set_is_operational(old_value);

        update_operating();
    }

    /**
     * Update processing state.
     *
     * Returns whether we are still processing.
     */
    // tg: code/modules/transport/elevator/elev_indicator.dm:92
    public virtual object update_operating()
    {
        // Let process() figure it out to have the logic in one place.
        bool should_process = !Dm.eq(process(), PROCESS_KILL);
        if (should_process)
        {
            begin_processing();
            return TRUE;
        }
        end_processing();
        return FALSE;
    }

    // tg: code/modules/transport/elevator/elev_indicator.dm:101
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        datum_transport_controller_linear lift = lift_ref?.resolve() as datum_transport_controller_linear;

        // Check for stopped states.
        if (lift == null || !Dm.truthy(is_operational))
        {
            // elevator missing, or we lost power.
            set_lift_state(0, 0, force: Dm.num(!Dm.truthy(is_operational)));
            return PROCESS_KILL;
        }

        Waiting.NoSleep(use_energy(active_power_usage));

        obj_structure_transport_linear lift_part = lift.transport_modules[1] as obj_structure_transport_linear;

        if (QDELETED(lift_part))
        {
            set_lift_state(0, 0, force: Dm.num(!Dm.truthy(is_operational)));
            return PROCESS_KILL;
        }

        // Update
        // DEVIATION(DV239): the station starts at z 1 here, z 2 in tg, so the floor number is z + 1 - the offset
        set_lift_state(current_lift_direction, ((obj_structure_transport_linear)lift.transport_modules[1]).z + 1 - lowest_floor_offset);

        // elevator's not moving, we're done; we just had to update the floor number one last time.
        if (!Dm.truthy(current_lift_direction))
            return PROCESS_KILL;
        return null;
    }

    /**
     * Set the state and update appearance.
     */
    // tg: code/modules/transport/elevator/elev_indicator.dm:133
    public virtual void set_lift_state(object new_direction = null, object new_floor = null, float? force = null)
    {
        force ??= FALSE;
        if (Dm.eq(new_direction, current_lift_direction) && Dm.eq(new_floor, current_lift_floor) && !Dm.truthy(force))
            return;

        current_lift_direction = Dm.num(new_direction);
        current_lift_floor = Dm.num(new_floor);
        update_appearance();
    }

    // tg: code/modules/transport/elevator/elev_indicator.dm:141
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);

        if (!Dm.truthy(is_operational))
        {
            Waiting.NoSleep(set_light(l_on: FALSE));
            maptext = "";
            return dot;
        }

        Waiting.NoSleep(set_light(l_on: TRUE));
        maptext = Dm.text("<div style='font-family: TinyUnicode; font-size: 12pt; color: []'>[]</div>", COLOR_DISPLAY_BLUE, current_lift_floor);
        return dot;
    }

    // tg: code/modules/transport/elevator/elev_indicator.dm:152
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (!Dm.truthy(is_operational))
            return dot;

        // DEVIATION(DV12): . += emissive_appearance(icon, "[base_icon_state]e", offset_spokesman = src, alpha = src.alpha)
        Dm.Deferred("code/modules/transport/elevator/elev_indicator.dm:156", "update_overlays: the display's emissive_appearance (DEVIATION DV12)");

        if (!Dm.truthy(current_lift_direction))
            return dot;

        string arrow_icon_state = Dm.text("[][]", base_icon_state, Dm.eq(current_lift_direction, UP) ? "up" : "down");

        Dm.append(ref dot, mutable_appearance(icon, arrow_icon_state));
        // DEVIATION(DV12): . += emissive_appearance(icon, "[arrow_icon_state]e", offset_spokesman = src, alpha = src.alpha)
        return dot;
    }
}
