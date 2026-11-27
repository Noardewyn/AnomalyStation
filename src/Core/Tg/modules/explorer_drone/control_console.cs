// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/control_console.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the exploration drone control console (the ExodroneConsole window): taking control of one drone, its tools,
// travel, exploration, events and adventure choices, jettisoning its cargo, its self-destruct, and the alert screen while the drone
// waits for a decision. The var block is generated (D111).

// tg: code/modules/explorer_drone/control_console.dm:1
public partial class obj_machinery_computer_exodrone_control_console : obj_machinery_computer
{
    // tg: code/modules/explorer_drone/control_console.dm:11
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ExodroneConsole", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/control_console.dm:18
    public virtual async DmTask start_drone_control(obj_item_exodrone drone = null)
    {
        if (!Dm.truthy(drone.controlled))//Only one controller per drone at once to make it saner
        {
            ///End control if we had previous drone
            await end_drone_control();
            controlled_drone = drone;
            controlled_drone.controlled = TRUE;
            RegisterSignal(controlled_drone, COMSIG_QDELETING, nameof(drone_destroyed));
            RegisterSignal(controlled_drone, COMSIG_EXODRONE_STATUS_CHANGED, nameof(on_exodrone_status_changed));
            await update_icon();
            playsound(this, Dm.resource("sound/machines/terminal/terminal_on.ogg"), 20, vary: TRUE);
        }
    }

    // tg: code/modules/explorer_drone/control_console.dm:29
    [SignalHandler]
    public virtual void on_exodrone_status_changed()
    {
        //Notify we need human action and switch screeb icon to alert.
        playsound(this, Dm.resource("sound/machines/terminal/terminal_processing.ogg"), 20, vary: TRUE);
        Waiting.NoSleep(update_icon());
    }

    // tg: code/modules/explorer_drone/control_console.dm:35
    [SignalHandler]
    public virtual void drone_destroyed()
    {
        signal_lost = TRUE;
        playsound(this, Dm.resource("sound/machines/terminal/terminal_alert.ogg"), 20, vary: TRUE);
        Waiting.NoSleep(end_drone_control());
    }

    // tg: code/modules/explorer_drone/control_console.dm:41
    public virtual async DmTask end_drone_control()
    {
        if (Dm.truthy(controlled_drone))
        {
            controlled_drone.controlled = FALSE;
            UnregisterSignal(controlled_drone, Dm.list(COMSIG_QDELETING, COMSIG_EXODRONE_STATUS_CHANGED));
            controlled_drone = null;
            await update_icon();
            playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 20, vary: TRUE);
        }
    }

    // tg: code/modules/explorer_drone/control_console.dm:49
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        Waiting.NoSleep(end_drone_control());
        return dot;
    }

    // tg: code/modules/explorer_drone/control_console.dm:53
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = await base.ui_static_data(user) as DmList;
        dot["all_tools"] = GLOB.exodrone_tool_metadata;
        dot["all_bands"] = GLOB.exoscanner_bands;
        return dot;
    }

    // tg: code/modules/explorer_drone/control_console.dm:58
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;
        dot["signal_lost"] = signal_lost;
        dot["drone"] = controlled_drone;
        if (Dm.truthy(controlled_drone))
        {
            dot["drone_status"] = controlled_drone.drone_status;
            dot["drone_name"] = controlled_drone.name;
            dot["drone_integrity"] = controlled_drone.get_integrity();
            dot["drone_max_integrity"] = controlled_drone.max_integrity;
            dot["drone_log"] = controlled_drone.drone_log;
            dot["configurable"] = Dm.num(controlled_drone.drone_status == EXODRONE_IDLE);
            dot["cargo"] = controlled_drone.get_cargo_data();
            dot["drone_travel_coefficent"] = controlled_drone.get_travel_coeff();
            object travel_error = controlled_drone.travel_error();
            dot["can_travel"] = Dm.not(travel_error);
            dot["travel_error"] = Dm.truthy(travel_error) ? travel_error : "";
            switch (controlled_drone.drone_status) //could move this down to drone.ui_data()
            {
                case EXODRONE_IDLE:
                    dot["sites"] = build_exploration_site_ui_data();
                    dot["site"] = null;
                    break;
                case EXODRONE_TRAVEL:
                    dot["travel_time"] = controlled_drone.travel_time;
                    dot["travel_time_left"] = timeleft(controlled_drone.travel_timer_id);
                    break;
                case EXODRONE_BUSY:
                    dot["wait_time_left"] = controlled_drone.busy_time_left();
                    dot["wait_message"] = controlled_drone.busy_message;
                    break;
                case EXODRONE_EXPLORATION:
                    dot["sites"] = build_exploration_site_ui_data();
                    dot["site"] = controlled_drone.location.site_data(exploration: TRUE);
                    dot["event"] = controlled_drone.current_event_ui_data;
                    break;
                case EXODRONE_ADVENTURE:
                    dot["adventure_data"] = controlled_drone.get_adventure_data();
                    break;
            }
        }
        else
        {
            DmList exodrones = Dm.list();
            foreach (obj_item_exodrone drone in Dm.each<obj_item_exodrone>(GLOB.exodrones))
                Dm.append(ref exodrones, Dm.list(Dm.list(Dm.kv("name", drone.name), Dm.kv("controlled", drone.controlled), Dm.kv("description", drone.ui_description()), Dm.kv("ref", REF(drone)))));
            dot["all_drones"] = exodrones;
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/control_console.dm:96
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        /// Show alert screen if the drone is in a mode that requires decisionmaking
        if (Dm.truthy(controlled_drone) && (controlled_drone.drone_status == EXODRONE_IDLE || controlled_drone.drone_status == EXODRONE_EXPLORATION || controlled_drone.drone_status == EXODRONE_ADVENTURE))
            icon_screen = "alert:2";
        else
            icon_screen = Dm.initial(this, (obj_machinery_computer_exodrone_control_console p) => p.icon_screen);
        return await base.update_overlays(__extra);
    }

    // tg: code/modules/explorer_drone/control_console.dm:104
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "select_drone":
            {
                obj_item_exodrone selected = Dm.locate_ref(@params["drone_ref"]) is obj_item_exodrone found && Dm.@in(found, GLOB.exodrones) ? found : null; // locate(params["drone_ref"]) in GLOB.exodrones
                if (Dm.truthy(selected))
                    await start_drone_control(selected);
                return TRUE;
            }
            case "end_control":
                await end_drone_control();
                return TRUE;
            case "confirm_signal_lost":
                signal_lost = FALSE;
                return TRUE;
            case "self_destruct":
                await controlled_drone.handle_deconstruct();
                return TRUE;
            case "add_tool":
                if (Dm.truthy(controlled_drone) && controlled_drone.drone_status == EXODRONE_IDLE)
                    controlled_drone.add_tool(@params["tool_type"]);
                return TRUE;
            case "remove_tool":
                if (Dm.truthy(controlled_drone) && controlled_drone.drone_status == EXODRONE_IDLE)
                    controlled_drone.remove_tool(@params["tool_type"]);
                return TRUE;
            case "start_travel":
                if (Dm.truthy(controlled_drone) && !Dm.truthy(controlled_drone.travel_error()))
                {
                    datum_exploration_site target_site = null;
                    if (Dm.truthy(@params["target_site"]))
                    {
                        target_site = Dm.locate_ref(@params["target_site"]) is datum_exploration_site found && Dm.@in(found, GLOB.exploration_sites) ? found : null; // locate(params["target_site"]) in GLOB.exploration_sites
                        if (!Dm.truthy(target_site))
                            return TRUE;
                        if (!Dm.truthy(controlled_drone.check_blacklist()))
                        {
                            await say(I18n.T("Error - An unauthorized object was found inside the cargo!"));
                            return TRUE;
                        }
                    }
                    await controlled_drone.launch_for(target_site);
                }
                return TRUE;
            case "explore":
                if (Dm.truthy(controlled_drone) && controlled_drone.drone_status == EXODRONE_EXPLORATION)
                    controlled_drone.explore_site();
                return TRUE;
            case "explore_event":
                if (Dm.truthy(controlled_drone) && controlled_drone.drone_status == EXODRONE_EXPLORATION)
                {
                    datum_exploration_event chosen_event = Dm.locate_ref(@params["target_event"]) is datum_exploration_event found && Dm.@in(found, controlled_drone.location.events) ? found : null; // locate(params["target_event"]) in controlled_drone.location.events
                    if (Dm.truthy(chosen_event))
                        controlled_drone.explore_site(chosen_event);
                }
                break; // tg returns nothing here: no update follows the act
            case "adventure_choice":
                if (Dm.truthy(controlled_drone) && controlled_drone.drone_status == EXODRONE_ADVENTURE)
                    controlled_drone.current_adventure?.select_choice(@params["choice"]);
                return TRUE;
            case "start_event":
                if (Dm.truthy(controlled_drone) && Dm.truthy(controlled_drone.current_event_ui_data))
                {
                    datum_exploration_event_simple chosen_event = Dm.locate_ref(controlled_drone.current_event_ui_data["ref"]) is datum_exploration_event_simple found && Dm.@in(found, controlled_drone.location.events) ? found : null; // locate(controlled_drone.current_event_ui_data["ref"]) in controlled_drone.location.events
                    if (Dm.truthy(chosen_event))
                        await chosen_event.fire(controlled_drone);
                }
                return TRUE;
            case "skip_event":
                if (Dm.truthy(controlled_drone) && Dm.truthy(controlled_drone.current_event_ui_data))
                {
                    datum_exploration_event_simple chosen_event = Dm.locate_ref(controlled_drone.current_event_ui_data["ref"]) is datum_exploration_event_simple found && Dm.@in(found, controlled_drone.location.events) ? found : null; // locate(controlled_drone.current_event_ui_data["ref"]) in controlled_drone.location.events
                    if (Dm.truthy(chosen_event.skippable))
                        chosen_event.end(controlled_drone);
                }
                return TRUE;
            case "jettison":
                if (Dm.truthy(controlled_drone))
                {
                    obj thing_to_jettison = Dm.locate_ref(@params["target_ref"]) is obj found && Dm.@in(found, controlled_drone.contents) ? found : null; // locate(params["target_ref"]) in controlled_drone.contents
                    if (Dm.truthy(thing_to_jettison))
                    {
                        controlled_drone.drone_log__proc(I18n.T("Jettisoned []", thing_to_jettison));
                        if (controlled_drone.drone_status == EXODRONE_IDLE)
                            await thing_to_jettison.forceMove(controlled_drone.drop_location() as atom);
                        else
                            qdel(thing_to_jettison); //this might need some limitations
                    }
                }
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/control_console.dm:179
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/simple/adventure)), the preset screens: the asset transport is the client's (DV44); the
        // ExodroneConsole window draws them from its own copy of tg's icons/ui/adventure/ pictures.
        return Dm.list();
    }
}
