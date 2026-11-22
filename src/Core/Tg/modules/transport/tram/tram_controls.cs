// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/tram/tram_controls.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the whole file. The screen's emissive overlay is lighting (DEVIATION DV12).

// tg: code/modules/transport/tram/tram_controls.dm:1
public partial class obj_machinery_computer_tram_controls : obj_machinery_computer
{
    // tg: code/modules/transport/tram/tram_controls.dm:55
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        link_circuit();
        if (Dm.eq(install_type, STANDALONE))
        {
            base_icon_state = "tram_alt";
            icon_state = "tram_alt_controls";
        }
        if (!Dm.truthy(id_tag))
            id_tag = assign_random_name();
        SStransport.hello(this, name, id_tag);
        RegisterSignal(SStransport, COMSIG_TRANSPORT_RESPONSE, nameof(call_response));
        find_tram();

        datum_transport_controller_linear_tram tram = transport_ref?.resolve() as datum_transport_controller_linear_tram;
        if (tram != null)
            RegisterSignal(SStransport, COMSIG_TRANSPORT_UPDATED, nameof(update_display));
    }

    // tg: code/modules/transport/tram/tram_controls.dm:70
    public override async DmTask<object> RefreshParts()
    {
        link_circuit(overwrite: TRUE);
        return await base.RefreshParts();
    }

    // tg: code/modules/transport/tram/tram_controls.dm:74
    public override object update_current_power_usage()
    {
        return null; // We get power from area rectifiers
    }

    /**
     * Links the specific_transport_id of the circuitboard and the tram controls machine.
     * By default it will only link machines that don't yet have a specific_transport_id.
     */
    // tg: code/modules/transport/tram/tram_controls.dm:84
    public virtual void link_circuit(float? overwrite = null)
    {
        overwrite ??= FALSE;
        obj_item_circuitboard_computer_tram_controls my_circuit = circuit_value as obj_item_circuitboard_computer_tram_controls; // GAP(types): the board instance (_machinery.cs)
        if (my_circuit == null)
            return; // a typepath circuit before the board exists (tg's istype cast would runtime on it too)

        // mapped subtypes push their install_type to the circuitboard, otherwise we pull it
        if (install_type == null || Dm.truthy(overwrite))
            install_type = my_circuit.install_type;
        else
            my_circuit.install_type = install_type;

        // mapped subtypes push their specific_transport_id to the circuitboard, otherwise we pull it
        if (specific_transport_id == null || Dm.truthy(overwrite))
            specific_transport_id = my_circuit.specific_transport_id;
        else
            my_circuit.specific_transport_id = Dm.text("[]", specific_transport_id);
    }

    /**
     * Finds the tram from the console
     *
     * Locates tram parts in the lift global list after everything is done.
     */
    // tg: code/modules/transport/tram/tram_controls.dm:105
    public virtual void find_tram()
    {
        foreach (datum_transport_controller_linear transport in Dm.each<datum_transport_controller_linear>(SStransport.transports_by_type[TRANSPORT_TYPE_TRAM]))
        {
            if (Dm.eq(transport.specific_transport_id, specific_transport_id))
            {
                transport_ref = WEAKREF(transport) as datum_weakref;
                return;
            }
        }
    }

    // tg: code/modules/transport/tram/tram_controls.dm:111
    public override object ui_state(mob user = null)
    {
        return GLOB.not_incapacitated_state;
    }

    // tg: code/modules/transport/tram/tram_controls.dm:114
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        datum_transport_controller_linear_tram tram = transport_ref?.resolve() as datum_transport_controller_linear_tram;

        if (tram != null && Dm.truthy(tram.controller_active))
            return UI_CLOSE;
        if (!in_range(user, this) && !isobserver(user))
            return UI_CLOSE;
        return await base.ui_status(user, state);
    }

    // tg: code/modules/transport/tram/tram_controls.dm:123
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "TramControl", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/transport/tram/tram_controls.dm:130
    public override async DmTask<object> ui_data(mob user = null)
    {
        datum_transport_controller_linear_tram tram_controller = transport_ref?.resolve() as datum_transport_controller_linear_tram;
        DmList data = Dm.list();
        data["moving"] = tram_controller?.controller_active;
        data["broken"] = tram_controller == null || tram_controller.paired_cabinet == null ? TRUE : FALSE;
        obj_effect_landmark_transport_nav_beacon_tram_platform current_loc = tram_controller?.idle_platform;
        if (current_loc != null)
            data["tram_location"] = current_loc.name;
        return data;
    }

    // tg: code/modules/transport/tram/tram_controls.dm:140
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["destinations"] = get_destinations();
        return data;
    }

    /**
     * Finds the destinations for the tram console gui
     *
     * Pulls tram landmarks from the landmark gobal list
     * and uses those to show the proper icons and destination
     * names for the tram console gui.
     */
    // tg: code/modules/transport/tram/tram_controls.dm:152
    public virtual object get_destinations()
    {
        DmList dot = Dm.list();
        foreach (obj_effect_landmark_transport_nav_beacon_tram_platform destination in Dm.each<obj_effect_landmark_transport_nav_beacon_tram_platform>(SStransport.nav_beacons[specific_transport_id]))
        {
            DmList this_destination = Dm.list();
            this_destination["name"] = destination.name;
            this_destination["dest_icons"] = destination.tgui_icons;
            this_destination["id"] = destination.platform_code;
            Dm.append(ref dot, Dm.list(this_destination));
        }
        return dot;
    }

    // tg: code/modules/transport/tram/tram_controls.dm:161
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "send"))
        {
            obj_effect_landmark_transport_nav_beacon_tram_platform destination_platform = null;
            foreach (obj_effect_landmark_transport_nav_beacon_tram_platform destination in Dm.each<obj_effect_landmark_transport_nav_beacon_tram_platform>(SStransport.nav_beacons[specific_transport_id]))
            {
                if (Dm.eq(destination.platform_code, @params["destination"]))
                {
                    destination_platform = destination;
                    break;
                }
            }

            if (destination_platform == null)
                return FALSE;

            SStransport.incoming_request(this, specific_transport_id, destination_platform.platform_code);
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/transport/tram/tram_controls.dm:180
    public virtual void update_display(datum source = null, datum_transport_controller_linear_tram controller = null, object controller_active = null, object controller_status = null, object travel_direction = null, obj_effect_landmark_transport_nav_beacon_tram_platform destination_platform = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
        {
            icon_screen = null;
            update_appearance();
            return;
        }

        if (controller != null && !Dm.eq(controller.specific_transport_id, specific_transport_id))
            return;

        if (controller == null || !Dm.truthy(controller.controller_operational))
        {
            icon_screen = Dm.text("[]_broken", base_icon_state);
            update_appearance();
            return;
        }

        if (destination_platform == null)
        {
            icon_screen = Dm.text("[]", specific_transport_id);
            update_appearance();
            return;
        }

        if ((controller.status_bits & EMERGENCY_STOP) != 0 || (controller.status_bits & SYSTEM_FAULT) != 0)
        {
            icon_screen = Dm.text("[]_NIS", base_icon_state);
            update_appearance();
            return;
        }

        if (Dm.truthy(controller_active))
        {
            icon_screen = Dm.text("[]_0[]", base_icon_state, travel_direction);
            update_appearance();
            return;
        }

        icon_screen = "";
        icon_screen += Dm.text("[]", base_icon_state);
        icon_screen += Dm.text("_[]", destination_platform.platform_code);

        update_appearance();
    }

    // tg: code/modules/transport/tram/tram_controls.dm:217
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        await base.on_construction(user, __extra);
        obj_item_circuitboard_computer_tram_controls my_circuit = circuit_value as obj_item_circuitboard_computer_tram_controls; // GAP(types): the board instance (_machinery.cs)
        install_type = my_circuit.install_type;
        float facing = Dm.num(dir);
        if (Dm.eq(install_type, SPLIT_WINDOW))
        {
            base_icon_state = "tram";
            icon_state = "tram_controls";
            if (facing == NORTH) { pixel_x = 8; pixel_y = -32; }
            else if (facing == SOUTH) { pixel_x = -8; pixel_y = 32; }
            else if (facing == EAST) { pixel_x = -32; pixel_y = -8; }
            else if (facing == WEST) { pixel_x = 32; pixel_y = 8; }
        }
        else if (Dm.eq(install_type, NORMAL_WINDOW))
        {
            base_icon_state = "tram";
            icon_state = "tram_controls";
            if (facing == NORTH) pixel_y = -32;
            else if (facing == SOUTH) pixel_y = 32;
            else if (facing == EAST) pixel_x = -32;
            else if (facing == WEST) pixel_x = 32;
        }
        else
        {
            base_icon_state = "tram_alt";
            icon_state = "tram_alt_controls";
        }
    }

    // tg: code/modules/transport/tram/tram_controls.dm:254
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (icon_screen == null)
            return dot;

        // tg's emissive_appearance of the screen is lighting (DV12); /obj/machinery/computer's update_overlays already draws icon_screen
        return dot;
    }

    // tg: code/modules/transport/tram/tram_controls.dm:262
    public override object power_change()
    {
        object dot = base.power_change();
        datum_transport_controller_linear_tram tram = transport_ref?.resolve() as datum_transport_controller_linear_tram;
        if (tram == null)
        {
            icon_screen = Dm.text("[]_broken", base_icon_state);
            update_appearance();
            return dot;
        }

        update_display(this, tram, tram.controller_active, tram.controller_status, tram.travel_direction, tram.destination_platform);
        return dot;
    }

    // tg: code/modules/transport/tram/tram_controls.dm:272
    public virtual void call_response(object controller = null, DmList relevant = null, object response_code = null, object response_info = null)
    {
        datum_transport_controller_linear_tram tram = transport_ref?.resolve() as datum_transport_controller_linear_tram;
        if (tram == null)
            return;
        if (Dm.truthy(SStts.tts_enabled))
            tram.nav_beacon.voice = SStts.tram_voice;

        if (Dm.eq(response_code, REQUEST_FAIL))
        {
            if (!Dm.truthy(LAZYFIND(relevant, this)))
                return;

            if (Dm.eq(response_info, NOT_IN_SERVICE))
                Waiting.Detach(tram.nav_beacon.say(I18n.T("The tram is not in service. Please contact the nearest engineer.")));
            else if (Dm.eq(response_info, INVALID_PLATFORM))
                Waiting.Detach(tram.nav_beacon.say(I18n.T("Configuration error. Please contact the nearest engineer.")));
            else if (Dm.eq(response_info, INTERNAL_ERROR))
                Waiting.Detach(tram.nav_beacon.say(I18n.T("Tram controller error. Please contact the nearest engineer or crew member with telecommunications access to reset the controller.")));
        }
    }
}
