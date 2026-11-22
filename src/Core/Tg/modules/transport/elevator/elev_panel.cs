// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/elevator/elev_panel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D325 (multi-z, elevators): the whole file. The light mask's emissive overlay is lighting (DEVIATION DV12).

/**
 * # Elevator control panel
 *
 * A wallmounted simple machine that controls elevators,
 * allowing users to enter a UI to move it up or down
 */
// tg: code/modules/transport/elevator/elev_panel.dm:12
public partial class obj_machinery_elevator_control_panel : obj_machinery
{
    // tg: code/modules/transport/elevator/elev_panel.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Initialize__tool_behaviors ??= Dm.list(
            Dm.kv(TOOL_MULTITOOL, Dm.list(Dm.kv(SCREENTIP_CONTEXT_LMB, "Reset Panel"))));
        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_tools), Initialize__tool_behaviors));
        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_bare_hands), Dm.named("lmb_text", I18n.T("Send Elevator"))));

        maploaded = Dm.num(mapload);
        // Maploaded panels link in post_machine_initialize...
        if (Dm.truthy(mapload))
            return dot;

        // And non-mapload panels link in Initialize
        Waiting.NoSleep(link_with_lift(log_error: FALSE));
        return dot;
    }

    // tg: code/modules/transport/elevator/elev_panel.dm:59  var/static/list/tool_behaviors (rules 10.6)
    private static ref DmList Initialize__tool_behaviors => ref TgState.Current.obj_machinery_elevator_control_panel__Initialize__tool_behaviors;

    // tg: code/modules/transport/elevator/elev_panel.dm:73
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        // If we weren't maploaded, we probably already linked (or tried to link) in Initialize().
        if (!Dm.truthy(maploaded))
            return;

        // This is exclusively for linking in mapload, just to ensure all elevator parts are created,
        // and also so we can throw mapping errors to let people know if they messed up setup.
        await link_with_lift(log_error: TRUE);
    }

    /// Link with associated transport controllers, only log failure to find a lift in LateInit because those are mapped in
    // tg: code/modules/transport/elevator/elev_panel.dm:84
    public virtual async DmTask link_with_lift(float? log_error = null)
    {
        log_error ??= FALSE;
        datum_transport_controller_linear lift = get_associated_lift() as datum_transport_controller_linear;
        if (lift == null)
        {
            if (Dm.truthy(log_error))
                log_mapping(Dm.text("Elevator control panel at [] found no associated lift to link with, this may be a mapping error.", AREACOORD(this)));
            return;
        }

        lift_weakref = WEAKREF(lift) as datum_weakref;
        await populate_destinations_list(lift);
        if (Dm.@in(linked_elevator_id, GLOB.elevator_music))
        {
            obj_effect_abstract_elevator_music_zone music = GLOB.elevator_music[linked_elevator_id] as obj_effect_abstract_elevator_music_zone;
            music.link_to_panel(this);
        }
    }

    // tg: code/modules/transport/elevator/elev_panel.dm:97
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;

        obj_flags = Dm.num(Dm.bitor(obj_flags, EMAGGED));

        datum_transport_controller_linear lift = lift_weakref?.resolve() as datum_transport_controller_linear;
        if (lift == null)
            return FALSE;

        foreach (obj_structure_transport_linear lift_platform in Dm.each<obj_structure_transport_linear>(lift.transport_modules))
        {
            lift_platform.violent_landing = TRUE;
            lift_platform.warns_on_down_movement = FALSE;
            lift_platform.elevator_vertical_speed = Dm.num(Dm.initial(lift_platform, nameof(lift_platform.elevator_vertical_speed))) * 0.5f;
        }

        foreach (obj_machinery_door elevator_door in Dm.each<obj_machinery_door>(GLOB.elevator_doors))
        {
            if (!Dm.eq(elevator_door.transport_linked_id, linked_elevator_id))
                continue;
            if (Dm.truthy(Dm.bitand(elevator_door.obj_flags, EMAGGED)))
                continue;
            elevator_door.elevator_status = LIFT_PLATFORM_UNLOCKED;
            Waiting.Detach(elevator_door.open(BYPASS_DOOR_CHECKS));
            elevator_door.obj_flags = Dm.num(Dm.bitor(elevator_door.obj_flags, EMAGGED));
        }

        playsound(this, SFX_SPARKS, 100, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        balloon_alert(user, I18n.T("safeties overridden"));
        return TRUE;
    }

    // tg: code/modules/transport/elevator/elev_panel.dm:125
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        datum_transport_controller_linear lift = lift_weakref?.resolve() as datum_transport_controller_linear;
        if (lift == null)
            return null;

        balloon_alert(user, I18n.T("resetting panel..."));
        playsound(this, Dm.resource("sound/machines/locktoggle.ogg"), 50, TRUE);
        if (!Dm.truthy(await do_after(user, 6 * SECONDS, this)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return TRUE;
        }

        if (QDELETED(lift) || !Dm.truthy(length(lift.transport_modules)))
            return null;

        // If we were emagged, reset us
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            foreach (obj_structure_transport_linear lift_platform in Dm.each<obj_structure_transport_linear>(lift.transport_modules))
            {
                lift_platform.violent_landing = Dm.num(Dm.initial(lift_platform, nameof(lift_platform.violent_landing)));
                lift_platform.warns_on_down_movement = Dm.num(Dm.initial(lift_platform, nameof(lift_platform.warns_on_down_movement)));
                lift_platform.elevator_vertical_speed = Dm.num(Dm.initial(lift_platform, nameof(lift_platform.elevator_vertical_speed)));
            }

            foreach (obj_machinery_door elevator_door in Dm.each<obj_machinery_door>(GLOB.elevator_doors))
            {
                if (!Dm.eq(elevator_door.transport_linked_id, linked_elevator_id))
                    continue;
                if (!Dm.truthy(Dm.bitand(elevator_door.obj_flags, EMAGGED)))
                    continue;
                elevator_door.obj_flags = Dm.num(Dm.bitand(elevator_door.obj_flags, ~EMAGGED));
                Waiting.Detach(elevator_door.close());
            }

            obj_flags = Dm.num(Dm.bitand(obj_flags, ~EMAGGED));
        }

        // If we had doors open, stop the timer and reset them
        if (Dm.truthy(door_reset_timerid))
            deltimer(door_reset_timerid);
        reset_doors();

        // Be vague about whether something was accomplished or not
        balloon_alert(user, I18n.T("panel reset"));
        playsound(this, Dm.resource("sound/machines/locktoggle.ogg"), 50, TRUE);

        return TRUE;
    }

    /// Find the elevator associated with our lift button.
    // tg: code/modules/transport/elevator/elev_panel.dm:167
    public virtual object get_associated_lift()
    {
        foreach (datum_transport_controller_linear possible_match in Dm.each<datum_transport_controller_linear>(SStransport.transports_by_type[TRANSPORT_TYPE_ELEVATOR]))
        {
            if (!Dm.eq(possible_match.specific_transport_id, linked_elevator_id))
                continue;

            return possible_match;
        }

        return null;
    }

    /// Goes through and populates the linked_elevator_destination list with all possible destinations the lift can go.
    // tg: code/modules/transport/elevator/elev_panel.dm:177
    public virtual async DmTask populate_destinations_list(datum_transport_controller_linear linked_lift = null)
    {
        // This list will track all the raw z-levels which we found that we can travel to
        DmList raw_destinations = Dm.list();

        // Get a list of all the starting locs our elevator starts at
        DmList starting_locs = Dm.list();
        foreach (obj_structure_transport_linear lift_piece in Dm.each<obj_structure_transport_linear>(linked_lift.transport_modules))
        {
            Dm.combine(ref starting_locs, lift_piece.locs);
            // The raw destination list will start with all the z's we start at
            Dm.combine(ref raw_destinations, lift_piece.z);
        }

        // Get all destinations below us
        add_destinations_in_a_direction_recursively(starting_locs, DOWN, raw_destinations);
        // Get all destinations above us
        add_destinations_in_a_direction_recursively(starting_locs, UP, raw_destinations);

        linked_elevator_destination = Dm.list();
        foreach (object z_level in Dm.each(raw_destinations))
        {
            // Check if this z-level has a preset destination associated.
            // DEVIATION(DV239): the station starts at z 1 here, z 2 in tg, so the map's preset names are keyed by z + 1
            float tg_z = Dm.num(z_level) + 1;
            object preset_name = preset_destination_names?[num2text(tg_z)];
            // If we don't have a preset name, use Floor z-1 for the title.
            // z - 1 is used because the station z-level is 2, and goes up.
            linked_elevator_destination[Dm.text("[]", z_level)] = Dm.truthy(preset_name) ? preset_name : Dm.text("Floor []", tg_z - 1);
        }

        // Reverse the destination list.
        // By this point the list will go from bottom floor to top floor,
        // which is unintuitive when passed to the UI to show to users.
        // This way we have the top floors at the top, and the bottom floors the bottom.
        reverse_range(linked_elevator_destination);
        await update_static_data_for_all_viewers();
    }

    /**
     * Recursively adds destinations to the list of linked_elevator_destination
     * until it fails to find a valid stopping point in the passed direction.
     */
    // tg: code/modules/transport/elevator/elev_panel.dm:215
    public virtual void add_destinations_in_a_direction_recursively(DmList turfs_to_check = null, object direction = null, DmList destinations = null)
    {
        // Only vertical elevators are supported -  use trams for horizontal ones.
        if (!Dm.eq(direction, UP) && !Dm.eq(direction, DOWN))
            throw Dm.CRASH(Dm.text("[] was given an invalid direction in add_destinations_in_a_direction_recursively!", type), "code/modules/transport/elevator/elev_panel.dm", 215);

        DmList checked_turfs = Dm.list();
        // Go through every turf passed in our list of turfs to check.
        foreach (turf place in Dm.each<turf>(turfs_to_check))
        {
            // If the place we're checking isn't openspace, then we can't go downwards
            if (Dm.eq(direction, DOWN) && place is not turf_open_openspace)
                return;

            // Check the turf at the next level (either above or below the place we're checking)
            turf next_level = get_step_multiz(place, direction) as turf;
            // No turf = at the edge of a map vertically
            if (next_level == null)
                return;
            // If the next level above us has a roof, we can't move up
            if (Dm.eq(direction, UP) && next_level is not turf_open_openspace)
                return;

            // Otherwise, we can feasibly move our direction with this turf
            Dm.append(ref checked_turfs, next_level);
        }

        // If we somehow found no turfs, BUT made it this far, something definitely went wrong.
        if (!Dm.truthy(length(checked_turfs)))
            throw Dm.CRASH(Dm.text("[] found no turfs in add_destinations_in_a_direction_recursively!", type), "code/modules/transport/elevator/elev_panel.dm", 238);

        // Add the Zs of all the found turfs as possible destinations
        foreach (turf found in Dm.each<turf>(checked_turfs))
        {
            // We check all turfs we found in case of multi-z memes.
            Dm.combine(ref destinations, found.z);
        }

        // And recursively call the proc with all the turfs we found on the next level
        add_destinations_in_a_direction_recursively(checked_turfs, direction, destinations);
    }

    // tg: code/modules/transport/elevator/elev_panel.dm:251
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "ElevatorPanel", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/transport/elevator/elev_panel.dm:257
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        // We moved up a z-level, probably via the elevator itself, so don't preserve the UI.
        if (!Dm.eq(user.z, z))
            return UI_CLOSE;

        // Our lift is gone entirely - look, but don't touch.
        if (lift_weakref?.resolve() == null)
            return UI_UPDATE;

        // We're non-functional - don't do anything.
        if (!Dm.truthy(check_panel()))
            return UI_DISABLED;

        // Otherwise, just check default state (is the user conscious and close, etc).
        return await base.ui_status(user, state);
    }

    // tg: code/modules/transport/elevator/elev_panel.dm:273
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["emergency_level"] = capitalize(SSsecurity_level.get_current_level_as_text());
        data["is_emergency"] = Dm.num(Dm.num(SSsecurity_level.get_current_level_as_number()) >= SEC_LEVEL_RED);
        data["doors_open"] = Dm.num(Dm.truthy(door_reset_timerid));

        datum_transport_controller_linear lift = lift_weakref?.resolve() as datum_transport_controller_linear;
        if (lift != null)
        {
            data["lift_exists"] = TRUE;
            data["currently_moving"] = Dm.bitand(lift.controller_status, CONTROLS_LOCKED);
            data["currently_moving_to_floor"] = last_move_target;
            data["current_floor"] = ((obj_structure_transport_linear)lift.transport_modules[1]).z;
        }
        else
        {
            data["lift_exists"] = FALSE;
            data["currently_moving"] = FALSE;
            data["current_floor"] = 0; // 0 shows up as "Floor -1" in the UI, which is fine for what it is
        }

        return data;
    }

    // tg: code/modules/transport/elevator/elev_panel.dm:294
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        data["all_floor_data"] = Dm.list();
        foreach (object destination in Dm.each(linked_elevator_destination))
        {
            DmList floors = (DmList)data["all_floor_data"];
            Dm.append(ref floors, Dm.list(Dm.list(
                Dm.kv("name", linked_elevator_destination[destination]),
                Dm.kv("z_level", text2num(destination)))));
            data["all_floor_data"] = floors;
        }

        return data;
    }

    // tg: code/modules/transport/elevator/elev_panel.dm:306
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(check_panel()))
            return TRUE; // We shouldn't be usable right now, update UI

        switch (action)
        {
            case "move_lift":
            {
                if (!Dm.truthy(await allowed__proc(usr)))
                {
                    balloon_alert(usr, I18n.T("access denied!"));
                    return dot;
                }

                object desired_z = @params["z"];
                // num2text here is required as the z is stored as strings in the list, but passed here as a number.
                if (!Dm.@in(num2text(Dm.num(desired_z)), linked_elevator_destination))
                    return TRUE; // Something is inaccurate, update UI

                datum_transport_controller_linear lift = lift_weakref?.resolve() as datum_transport_controller_linear;
                if (lift == null || Dm.truthy(Dm.bitand(lift.controller_status, CONTROLS_LOCKED)))
                    return TRUE; // We shouldn't be moving anything, update UI

                Waiting.Detach(lift.move_to_zlevel(desired_z, Dm.@new<datum_callback>(this, nameof(check_panel)), usr));
                last_move_target = desired_z;
                return TRUE; // Succcessfully initiated a move. Regardless of whether it actually works, update the UI
            }

            case "emergency_door":
            {
                datum_transport_controller_linear lift = lift_weakref?.resolve() as datum_transport_controller_linear;
                if (lift == null)
                    return TRUE; // Something is wrong, update UI

                // The emergency door button is only available at red alert or higher.
                // This is so people don't keep it in emergency mode 100% of the time.
                if (Dm.num(SSsecurity_level.get_current_level_as_number()) < SEC_LEVEL_RED)
                    return TRUE; // The security level might have been lowered since last update, so update UI

                // Open all elevator doors, it's an emergency dang it!
                lift.update_lift_doors(action: CYCLE_OPEN);
                door_reset_timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(reset_doors)), 3 * MINUTES, TIMER_UNIQUE | TIMER_STOPPABLE, file: "code/modules/transport/elevator/elev_panel.dm", line: 342);
                return TRUE; // We opened up all the doors, update the UI so the emergency button is replaced correctly
            }

            case "reset_doors":
            {
                if (!Dm.truthy(door_reset_timerid))
                    return TRUE; // All the doors were shut since last update, so update UI

                deltimer(door_reset_timerid);
                reset_doors();
                return TRUE; // We closed all the doors, update the UI so the door button is replaced correctly
            }
        }
        return dot;
    }

    /// Callback for move_to_zlevel to ensure the elevator can continue to move.
    // tg: code/modules/transport/elevator/elev_panel.dm:352
    public virtual object check_panel()
    {
        if (QDELETED(this))
            return FALSE;
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return FALSE;

        return TRUE;
    }

    /// Helper proc to go through all of our desetinations and reset all elevator doors,
    /// closing doors on z-levels the elevator is away from, and opening doors on the z the elevator is
    // tg: code/modules/transport/elevator/elev_panel.dm:362
    public virtual void reset_doors()
    {
        datum_transport_controller_linear lift = lift_weakref?.resolve() as datum_transport_controller_linear;
        if (lift == null)
            return;

        DmList zs_we_are_present_on = lift.get_zs_we_are_on() as DmList;
        DmList zs_we_are_absent = Dm.list();

        foreach (object z_level_key in Dm.each(linked_elevator_destination))
        {
            object z_level = text2num(z_level_key); // Stored as strings.
            if (Dm.@in(z_level, zs_we_are_present_on))
                continue;

            Dm.combine(ref zs_we_are_absent, z_level);
        }

        // Open all the doors on the zs we should be open on,
        // and close all doors we aren't on. Simple enough.
        lift.update_lift_doors(zs_we_are_present_on, action: CYCLE_OPEN);
        lift.update_lift_doors(zs_we_are_absent, action: CYCLE_CLOSED);

        door_reset_timerid = null;
    }

    // tg: code/modules/transport/elevator/elev_panel.dm:385
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(light_mask))
            return dot;

        if (!Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) && !Dm.truthy(panel_open))
        {
            // DEVIATION(DV12): . += emissive_appearance(icon, light_mask, src, alpha = alpha)
            Dm.Deferred("code/modules/transport/elevator/elev_panel.dm:392", "update_overlays: the panel's light mask emissive_appearance (DEVIATION DV12)");
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/transport/elevator/elev_panel.dm:59  var/static/list/tool_behaviors
    internal DmList obj_machinery_elevator_control_panel__Initialize__tool_behaviors;
}
