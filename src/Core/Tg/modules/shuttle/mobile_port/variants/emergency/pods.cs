// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/variants/emergency/pods.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G3 (D272), D299: the escape pod's mobile docking port, its control console, the random lavaland dock (Meta's pods' lavaland
// docks: no lavaland, D108, so they find no turf and delete themselves as tg's do) and the pod's storage.

// tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:3
[DmPath("/obj/docking_port/mobile/pod")]
public partial class obj_docking_port_mobile_pod : obj_docking_port_mobile
{
    // tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:8
    public override object request(obj_docking_port_stationary destination_port = null, area signal_origin = null, object reason = null, object red_alert = null, object set_coefficient = null)
    {
        var connected_computer = get_control_console() as obj_machinery_computer_shuttle;
        if (connected_computer is not obj_machinery_computer_shuttle_pod)
            return FALSE;
        if (!(Dm.num(SSsecurity_level.get_current_level_as_number()) >= SEC_LEVEL_RED) && !Dm.truthy(Dm.bitand(connected_computer.obj_flags, EMAGGED)))
        {
            to_chat(usr, span_warning("Escape pods will only launch during \"Code Red\" security alert."));
            return FALSE;
        }
        if (Dm.eq(launch_status, UNLAUNCHED))
        {
            launch_status = EARLY_LAUNCHED;
            return base.request(destination_port);
        }
        return null;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:19
    public override object cancel(area signal_origin = null)
    {
        return null;
    }
}

// tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:22
public partial class obj_machinery_computer_shuttle_pod : obj_machinery_computer_shuttle
{
    // tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:34
    [DmParams("mapload", "C")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (SSsecurity_level != null) // a toy world has none
            RegisterSignal(SSsecurity_level, COMSIG_SECURITY_LEVEL_CHANGED, nameof(check_lock));
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:38
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        locked = FALSE;
        balloon_alert(user, "alert level checking disabled");
        icon_screen = "emagged_general";
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:48
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        object dot = await base.connect_to_shuttle(mapload, port, dock);
        if (port != null)
        {
            //Checks if the computer has already added the shuttle destination with the initial id
            //This has to be done because connect_to_shuttle is called again after its ID is updated
            //due to conflicting id names
            string base_shuttle_destination = Dm.text(";[]_lavaland", Dm.initial(port, "shuttle_id"));
            string shuttle_destination = Dm.text(";[]_lavaland", port.shuttle_id);

            float position = findtext(possible_destinations, base_shuttle_destination);
            if (Dm.truthy(position))
            {
                if (base_shuttle_destination == shuttle_destination)
                    return dot;
                possible_destinations = splicetext(possible_destinations, position, position + length(base_shuttle_destination), shuttle_destination);
                return dot;
            }

            possible_destinations += shuttle_destination;
        }
        return dot;
    }

    // DEVIATION(DV236): with no lavaland loaded a pod's random docks are gone, so it launches for its recovery ship dock, where the endgame sends it
    public override DmList get_valid_destinations()
    {
        DmList valid_destinations = base.get_valid_destinations();
        var pod = (obj_docking_port_mobile)SSshuttle.getShuttle(shuttleId);
        if (!Dm.truthy(valid_destinations.len) && SSshuttle.getDock(Dm.text("[]_away", shuttleId)) is obj_docking_port_stationary away
            && Dm.truthy(pod.check_dock(away, silent: TRUE)) && away != pod.destination)
            Dm.append(ref valid_destinations, Dm.list(Dm.list(Dm.kv("id", away.shuttle_id), Dm.kv("name", away.name))));
        return valid_destinations;
    }

    /**
     * Signal handler for checking if we should lock or unlock escape pods accordingly to a newly set security level
     *
     * Arguments:
     * * source The datum source of the signal
     * * new_level The new security level that is in effect
     */
    // tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:73
    public virtual void check_lock(datum source = null, object new_level = null)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return;
        locked = Dm.num(new_level) < SEC_LEVEL_RED ? TRUE : FALSE;
    }
}

// tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:80
[DmPath("/obj/docking_port/stationary/random")]
public partial class obj_docking_port_stationary_random : obj_docking_port_stationary
{
    // tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:90
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
            return dot;

        DmList turfs = get_area_turfs(target_area) as DmList;
        float original_len = turfs.len;
        while (Dm.truthy(turfs.len))
        {
            var picked_turf = (turf)pick(turfs);
            if (picked_turf.x < edge_distance || picked_turf.y < edge_distance || (Dm.world.maxx + 1 - picked_turf.x) < edge_distance || (Dm.world.maxy + 1 - picked_turf.y) < edge_distance)
            {
                Dm.remove(ref turfs, picked_turf);
            }
            else
            {
                Waiting.NoSleep(forceMove(picked_turf));
                return dot;
            }
        }

        // Fallback: couldn't find anything
        warning(Dm.text("docking port '[]' could not be randomly placed in []: of [] turfs, none were suitable", shuttle_id, target_area, original_len));
        unregister(); // DEVIATION(DV236): tg's plain QDEL lets a dock live (Destroy without force) and stay a destination; with no lavaland every one of these comes here
        return INITIALIZE_HINT_QDEL;
    }
}

// tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:130
public partial class obj_item_storage_pod : obj_item_storage
{
    // tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:139
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("wall_safe[]", Dm.truthy(atom_storage?.locked) ? "_locked" : "");
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:143
    public override async DmTask<object> create_storage(object max_slots = null, object max_specific_storage = null, object max_total_storage = null, DmList canhold = null, DmList canthold = null, Type storage_type = null, object rustle_sound = null, object remove_rustle_sound = null)
    {
        object dot = await base.create_storage(max_slots, max_specific_storage, max_total_storage, canhold, canthold, storage_type, rustle_sound, remove_rustle_sound);
        update_appearance(UPDATE_ICON_STATE);
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/pods.dm:149
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_head_helmet_space_orange>(this);
        Dm.@new<obj_item_clothing_head_helmet_space_orange>(this);
        Dm.@new<obj_item_clothing_suit_space_orange>(this);
        Dm.@new<obj_item_clothing_suit_space_orange>(this);
        Dm.@new<obj_item_clothing_mask_gas>(this);
        Dm.@new<obj_item_clothing_mask_gas>(this);
        Dm.@new<obj_item_tank_internals_oxygen_red>(this);
        Dm.@new<obj_item_tank_internals_oxygen_red>(this);
        Dm.@new<obj_item_pickaxe_emergency>(this);
        Dm.@new<obj_item_pickaxe_emergency>(this);
        Dm.@new<obj_item_survivalcapsule>(this);
        Dm.@new<obj_item_storage_toolbox_emergency>(this);
        Dm.@new<obj_item_bodybag_environmental>(this);
        Dm.@new<obj_item_bodybag_environmental>(this);
    }
}
