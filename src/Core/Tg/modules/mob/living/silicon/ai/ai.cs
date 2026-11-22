// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the AI, whole: its core, eye, cameras, tracking, alarms, holograms, hotkeys, shells, power and core links. The
// malfunction's picker and modules are SI8's; the mechs the AI may take over are inert. The var block is generated (D111).

// tg: code/modules/mob/living/silicon/ai/ai_defines.dm:1
public partial class mob_living_silicon_ai : mob_living_silicon
{
    private const string HOLOGRAM_CHOICE_CHARACTER = "Existing Character"; // tg: ai.dm:1
    private const string CHARACTER_TYPE_SELF = "My Character"; // tg: ai.dm:2
    private const string CHARACTER_TYPE_CREWMEMBER = "Station Member"; // tg: ai.dm:3

    /**
     * Init args
     * * target_ai - The mob taking control of the AI
     * * base_laws - An instance or typepath of a law datum, if provided the AI will use that lawset instead of making its own
     * * default_link - An ai_law_rack object to link to on spawn, if possible. Base laws is preferred over this.
     * * force_mind_move - If TRUE, forces the mind to move even if it is inactive.
     */
    // tg: code/modules/mob/living/silicon/ai/ai.dm:12
    [DmParams("mapload", "target_ai", "base_laws", "default_link", "force_mind_move")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob target_ai = Dm.arg<mob>(__extra, 0);
        object base_laws = Dm.arg(__extra, 1);
        obj_machinery_ai_law_rack_base default_link = Dm.arg<obj_machinery_ai_law_rack_base>(__extra, 2);
        object force_mind_move = Dm.arg(__extra, 3) ?? FALSE;
        object dot = base.Initialize(mapload);
        if (target_ai == null) //If there is no player/brain inside.
        {
            Dm.@new<obj_structure_ai_core>(loc, CORE_STATE_FINISHED); //New empty terminal.
            return INITIALIZE_HINT_QDEL; //Delete AI.
        }

        if (target_ai.client != null)
            set_gender(target_ai.client);

        ADD_TRAIT(this, TRAIT_NO_TELEPORT, AI_ANCHOR_TRAIT);
        status_flags = Dm.bitand(status_flags, Dm.bitnot(CANPUSH)); //AI starts anchored, so dont push it

        if (base_laws is datum_ai_laws given_laws)
            laws = given_laws.copy_lawset() as datum_ai_laws;
        else if (ispath(base_laws, typeof(datum_ai_laws)))
            laws = Dm.@new((Type)base_laws) as datum_ai_laws;
        else
        {
            make_laws();
            if (Dm.truthy(default_link?.can_link_to(this)))
                Waiting.NoSleep(default_link.link_silicon(this, announce: FALSE));
            else
                Waiting.NoSleep(link_to_first_rack());
        }

        datum_antagonist_malf_ai malf_datum = IS_MALF_AI(target_ai) as datum_antagonist_malf_ai;
        if (malf_datum != null)
            Waiting.NoSleep(malf_datum.add_law_zero());

        law_ui.update_inherent_stated_laws(laws);

        create_eye();

        if (target_ai.mind != null && (Dm.truthy(target_ai.mind.active) || Dm.truthy(force_mind_move)))
        {
            Waiting.NoSleep(target_ai.mind.transfer_to(this));
            if (Dm.truthy(is_antag()))
            {
                to_chat(this, span_userdanger(I18n.T("You have been installed as an AI! ")));
                to_chat(this, span_danger(I18n.T("You must obey your silicon laws above all else. Your objectives will consider you to be dead.")));
            }
            mind.has_ever_been_ai = TRUE;
        }
        else if (Dm.truthy(target_ai.ckey))
            PossessByPlayer(target_ai.ckey);

        to_chat(this, span_bold(I18n.T("You are playing the station's AI. The AI cannot move, but can interact with many objects while viewing them (through cameras).")));
        to_chat(this, span_bold(I18n.T("To look at other parts of the station, click on yourself to get a camera menu.")));
        to_chat(this, span_bold(I18n.T("While observing through a camera, you can use most (networked) devices which you can see, such as computers, APCs, intercoms, doors, etc.")));
        to_chat(this, I18n.T("To use something, simply click on it."));
        to_chat(this, I18n.T("For department channels, use the following say commands:"));
        to_chat(this, I18n.T(":o - AI Private, :c - Command, :s - Security, :e - Engineering, :u - Supply, :v - Service, :m - Medical, :n - Science, :h - Holopad."));

        Waiting.Detach(show_laws());

        to_chat(this, span_bold(I18n.T("These laws may be changed by other players, random events, or by you becoming malfunctioning.")));

        job = "AI";

        create_modularInterface(); // INVOKE_ASYNC of a proc that does not sleep

        // /mob/living/silicon/ai/apply_prefs_job() uses these to set these procs at mapload
        // this is used when a person is being inserted into an AI core during a round
        if (client != null)
        {
            Waiting.Detach(apply_pref_name(typeof(datum_preference_name_ai), client));
            apply_pref_hologram_display(client); // INVOKE_ASYNC of a proc that does not sleep
            set_gender(client);
        }

        Waiting.Detach(set_core_display_icon(null, client));

        spark_system = Dm.@new<datum_effect_system_basic_spark_spread>(this, 5, FALSE);
        spark_system.attach(this);

        SSverbs.assign_verb(this, typeof(datum_verb_metadata_mob_living_silicon_ai_show_laws_verb)); // ASSIGN_GAME_VERB(src, /mob/living/silicon/ai, show_laws_verb)

        aiMulti = Dm.@new<obj_item_multitool>(this);
        aicamera = Dm.@new<obj_item_camera_siliconcam_ai_camera>(this);

        Waiting.NoSleep(deploy_action.Grant(this));
        // DEVIATION(DV424): the meson vision switch
        grant_meson_vision_action();

        if (isturf(loc))
        {
            SSverbs.assign_verb(this, typeof(datum_verb_metadata_mob_living_silicon_ai_ai_network_change));
            SSverbs.assign_verb(this, typeof(datum_verb_metadata_mob_living_silicon_ai_ai_hologram_change));
            SSverbs.assign_verb(this, typeof(datum_verb_metadata_mob_living_silicon_ai_botcall));
            SSverbs.assign_verb(this, typeof(datum_verb_metadata_mob_living_silicon_ai_control_integrated_radio));
            SSverbs.assign_verb(this, typeof(datum_verb_metadata_mob_living_silicon_ai_set_automatic_say_channel));
        }

        GLOB.ai_list.Add(this);
        GLOB.shuttle_caller_list.Add(this);

        //They aren't given a c_tag so they don't show up in camera consoles
        builtInCamera = Dm.@new<obj_machinery_camera_silicon>(this);

        DmList alert_areas = TgState.Current.mob_living_silicon_ai__alert_areas;
        if (alert_areas == null)
            TgState.Current.mob_living_silicon_ai__alert_areas = alert_areas = Dm.add(GLOB.the_station_areas, typesof(typeof(area_mine))) as DmList;
        if (Dm.truthy(is_station_level(z)))
            alert_control = Dm.@new<datum_station_alert>(this, Dm.list(ALARM_ATMOS, ALARM_FIRE, ALARM_POWER, ALARM_CAMERA, ALARM_BURGLAR, ALARM_MOTION), SSmapping.levels_by_trait(ZTRAIT_STATION), alert_areas, Dm.named("camera_view", TRUE));
        else
            alert_control = Dm.@new<datum_station_alert>(this, Dm.list(ALARM_ATMOS, ALARM_FIRE, ALARM_POWER, ALARM_CAMERA, ALARM_BURGLAR, ALARM_MOTION), Dm.add(SSmapping.levels_by_trait(ZTRAIT_STATION), z), alert_areas, Dm.named("camera_view", TRUE));
        RegisterSignal(alert_control.listener, COMSIG_ALARM_LISTENER_TRIGGERED, nameof(alarm_triggered));
        RegisterSignal(alert_control.listener, COMSIG_ALARM_LISTENER_CLEARED, nameof(alarm_cleared));

        ai_tracking_tool = Dm.@new<datum_trackable>(this);
        RegisterSignal(ai_tracking_tool, COMSIG_TRACKABLE_TRACKING_STARTED, nameof(on_track_started));
        RegisterSignal(ai_tracking_tool, COMSIG_TRACKABLE_TRACKING_TARGET, nameof(on_track_target));
        RegisterSignal(ai_tracking_tool, COMSIG_TRACKABLE_GLIDE_CHANGED, nameof(tracked_glidesize_changed));

        add_traits(Dm.list(TRAIT_PULL_BLOCKED, TRAIT_AI_ACCESS, TRAIT_HANDS_BLOCKED, TRAIT_CAN_GET_AI_TRACKING_MESSAGE, TRAIT_LOUD_BINARY), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_block_area_power_fail)));

        //Heads up to other binary chat listeners that a new AI is online and listening to Binary.
        if (Dm.truthy(announce_init_to_others) && !Dm.truthy(is_centcom_level(z))) //Skip new syndicate AIs and also new AIs on centcom Z
        {
            foreach (mob McMobby in Dm.each<mob>(GLOB.player_list))
            {
                if (McMobby == this)
                    continue;
                if (!Dm.truthy(McMobby.binarycheck()))
                    continue;
                to_chat(McMobby, span_binarysay(Dm.text("<span class=[]>\\[ SYSTEM \\] NEW REMOTE HOST HAS CONNECTED TO THIS CHANNEL -- ID: []</span>", SPAN_COMMAND, this)), type: MESSAGE_TYPE_RADIO);
            }
        }

        RegisterSignal(this, COMSIG_SILICON_MODULE_RACK_LAWSET_UPDATE, nameof(lawset_updated_sync_borgs));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:128
    public override async DmTask mind_initialize()
    {
        await mind_initialize__d0(); // ..(): the first definition (datums/mind/initialization.dm:24, rules 2.5)
        mind.has_ever_been_ai = TRUE;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:139
    public override void key_down(object _key = null, client user = null, object full_key = null)
    {
        string key = Dm.str(_key);
        if (Dm.truthy(findtext(key, "numpad"))) //if it's a numpad number, we can convert it to just the number
            key = Dm.str(copytext(key, 7, 8)); //strings, lists, same thing really
        switch (key)
        {
            case "`":
            case "0":
                if (cam_prev != null)
                {
                    ai_tracking_tool.reset_tracking();
                    eyeobj.setLoc(cam_prev);
                }
                return;
            case "1": case "2": case "3": case "4": case "5": case "6": case "7": case "8": case "9":
            {
                object hotkey = text2num(key);
                if (Dm.truthy(user.keys_held["Ctrl"])) //do we assign a new hotkey?
                {
                    cam_hotkeys[hotkey] = eyeobj.loc;
                    to_chat(this, I18n.T("Location saved to Camera Group [].", hotkey));
                    return;
                }
                if (Dm.truthy(cam_hotkeys[hotkey])) //if this is false, no hotkey for this slot exists.
                {
                    cam_prev = eyeobj.loc;
                    ai_tracking_tool.reset_tracking();
                    eyeobj.setLoc(cam_hotkeys[hotkey]);
                    return;
                }
                break;
            }
        }
        base.key_down(_key, user, full_key);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:160
    public override object Destroy(float? force = null)
    {
        GLOB.ai_list.Remove(this);
        GLOB.shuttle_caller_list.Remove(this);
        SSshuttle?.autoEvac(); // a host-built room without the Master has no SSshuttle (rules 10.6)
        QDEL_NULL(ref eyeobj); // No AI, no Eye
        QDEL_NULL(ref spark_system);
        QDEL_NULL(ref malf_picker);
        QDEL_NULL(ref doomsday_device);
        QDEL_NULL(ref robot_control);
        QDEL_NULL(ref status_display_picker);
        QDEL_NULL(ref aiMulti);
        QDEL_NULL(ref alert_control);
        QDEL_NULL(ref ai_tracking_tool);
        malfhack = null;
        current = null;
        bot_ref = null;
        controlled_equipment = null;
        break_core_link();
        apc_override = null;
        if (ai_voicechanger != null)
        {
            ai_voicechanger.owner = null;
            ai_voicechanger = null;
        }
        return base.Destroy(force);
    }

    /// Removes all malfunction-related abilities from the AI
    // tg: code/modules/mob/living/silicon/ai/ai.dm:185
    public virtual void remove_malf_abilities()
    {
        QDEL_NULL(ref modules_action);
        foreach (datum_ai_module_malf AM in Dm.each<datum_ai_module_malf>(current_modules))
        {
            foreach (datum_action A in Dm.each<datum_action>(actions?.Copy()))
            {
                if (Dm.istype(A, Dm.initial(AM.GetType(), (datum_ai_module_malf m) => m.power_type)))
                    qdel(A);
            }
        }
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:192
    public override object ignite_mob(object silent = null)
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:199
    public virtual async DmTask set_core_display_icon(object input = null, client C = null)
    {
        portrait_appearance = null;

        object preferred_choice;
        if (Dm.truthy(input))
            preferred_choice = input;
        else if (C != null && C.prefs != null)
            preferred_choice = C.prefs.read_preference(typeof(datum_preference_choiced_ai_core_display));
        else if (client != null && client.prefs != null)
            preferred_choice = client.prefs.read_preference(typeof(datum_preference_choiced_ai_core_display));
        else
            preferred_choice = null;

        display_icon_override = await resolve_ai_icon(preferred_choice);

        update_appearance();

        if (loc is obj_item_aicard card)
            card.update_appearance();
    }

    /// Apply an AI's hologram preference
    // tg: code/modules/mob/living/silicon/ai/ai.dm:220
    public virtual void apply_pref_hologram_display(client player_client = null)
    {
        if (Dm.truthy(player_client.prefs?.read_preference(typeof(datum_preference_choiced_ai_hologram_display))))
        {
            object hologram_choice = player_client.prefs.read_preference(typeof(datum_preference_choiced_ai_hologram_display));
            if (Dm.eq(hologram_choice, "Random"))
                hologram_choice = pick(GLOB.ai_hologram_icons);

            hologram_appearance = mutable_appearance(GLOB.ai_hologram_icons[hologram_choice], Dm.str(GLOB.ai_hologram_icon_state[hologram_choice])) as mutable_appearance;
        }

        hologram_appearance ??= mutable_appearance(GLOB.ai_hologram_icons[AI_HOLOGRAM_DEFAULT], Dm.str(GLOB.ai_hologram_icon_state[AI_HOLOGRAM_DEFAULT])) as mutable_appearance;
    }

    /// Apply an AI's emote display preference
    // tg: code/modules/mob/living/silicon/ai/ai.dm:231
    public virtual void apply_pref_emote_display(client player_client = null)
    {
        if (Dm.truthy(player_client.prefs?.read_preference(typeof(datum_preference_choiced_ai_emote_display))))
        {
            object emote_choice = player_client.prefs.read_preference(typeof(datum_preference_choiced_ai_emote_display));

            if (Dm.eq(emote_choice, "Random"))
            {
                if (!Dm.truthy(length(GLOB.ai_status_display_all_options)))
                    init_ai_status_display_options();
                emote_choice = pick(GLOB.ai_status_display_all_options);
            }

            apply_emote_display(emote_choice);
        }
    }

    /// Apply an emote to all AI status displays on the station
    // tg: code/modules/mob/living/silicon/ai/ai.dm:244
    public virtual void apply_emote_display(object emote = null)
    {
        foreach (obj_machinery_status_display_ai ai_display in Dm.each<obj_machinery_status_display_ai>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_status_display_ai))))
        {
            ai_display.emotion = Dm.str(emote);
            ai_display.update();
        }
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:249  GAME_VERB_DESC(/mob/living/silicon/ai, pick_icon, "Set AI Core Display", ...): the verb proc
    public virtual void pick_icon(params object[] __extra)
    {
        Waiting.Detach(__gvb_pick_icon(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:249  the body
    public virtual async DmTask __gvb_pick_icon(DmList structured_args = null)
    {
        if (Dm.truthy(incapacitated))
        {
            to_chat(this, span_warning(I18n.T("You cannot access the core display controls in your current state.")));
            return;
        }

        core_display_picker ??= Dm.@new<datum_ai_core_display_picker>(this);
        await core_display_picker.ui_interact(this);

        if (loc is obj_item_aicard card)
            card.update_appearance();
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:263  GAME_VERB_DESC(/mob/living/silicon/ai, pick_status_display, "Set AI Status Display", ...): the verb proc
    public virtual void pick_status_display(params object[] __extra)
    {
        Waiting.Detach(__gvb_pick_status_display(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:263  the body
    public virtual async DmTask __gvb_pick_status_display(DmList structured_args = null)
    {
        if (Dm.truthy(incapacitated))
        {
            to_chat(this, span_warning(I18n.T("You cannot access the status display controls in your current state.")));
            return;
        }

        status_display_picker ??= Dm.@new<datum_ai_status_display_picker>(this);
        await status_display_picker.ui_interact(this);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:273
    public override object get_status_tab_items()
    {
        DmList dot = base.get_status_tab_items() as DmList ?? Dm.list();
        if (IS_UNCONSCIOUS_OR_CRIT(this))
        {
            Dm.append(ref dot, "Systems nonfunctional");
            return dot;
        }
        Dm.append(ref dot, Dm.text("System integrity: []%", (health + 100) * 0.5f));
        if (isturf(loc)) //only show if we're "in" a core
            Dm.append(ref dot, Dm.text("Backup Power: []%", battery * 0.5f));
        Dm.append(ref dot, Dm.text("Connected cyborgs: []", length(connected_robots)));
        foreach (mob_living_silicon_robot connected_robot in Dm.each<mob_living_silicon_robot>(connected_robots))
        {
            string robot_status = "Nominal";
            obj_item_stock_parts_power_store robot_cell = connected_robot.cell as obj_item_stock_parts_power_store;
            if (Dm.truthy(connected_robot.shell))
                robot_status = "AI SHELL";
            else if (IS_UNCONSCIOUS_OR_CRIT(connected_robot) || connected_robot.client == null)
                robot_status = "OFFLINE";
            else if (robot_cell == null || Dm.num(robot_cell.charge) <= 0)
                robot_status = "DEPOWERED";
            //Name, Health, Battery, Model, Area, and Status! Everything an AI wants to know about its borgies!
            Dm.append(ref dot, Dm.list(Dm.list(Dm.text("[]: ", connected_robot.name),
                Dm.text("S.Integrity: []% | Cell: [] | Model: [] | Loc: [] | Status: []",
                    connected_robot.health,
                    robot_cell != null ? Dm.text("[]/[]", display_energy(robot_cell.charge), display_energy(robot_cell.maxcharge)) : "Empty",
                    connected_robot.designation, get_area_name(connected_robot, TRUE), robot_status),
                Dm.text("src=[];track_cyborg=[]", REF(this), text_ref(connected_robot)))));
        }
        Dm.append(ref dot, Dm.text("AI shell beacons detected: []", LAZYLEN(GLOB.available_ai_shells))); //Count of total AI shells
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:302
    public virtual async DmTask ai_call_shuttle()
    {
        if (Dm.truthy(control_disabled))
        {
            to_chat(usr, span_warning(I18n.T("Wireless control is disabled!")));
            return;
        }

        object can_evac_or_fail_reason = SSshuttle.canEvac();
        if (!Dm.eq(can_evac_or_fail_reason, TRUE))
        {
            to_chat(usr, span_alert(Dm.text("[]", can_evac_or_fail_reason)));
            return;
        }

        object reason = await tgui_input_text(
            this,
            Dm.text("What is the nature of your emergency? ([] characters required.)", CALL_SHUTTLE_REASON_LENGTH),
            "Confirm Shuttle Call",
            max_length: MAX_MESSAGE_LEN,
            encode: FALSE
        );

        if (Dm.truthy(incapacitated))
            return;

        if (Dm.truthy(trim(reason)))
            SSshuttle.requestEvac(this, reason);

        // hack to display shuttle timer
        if (!datum_controller_subsystem_shuttle.EMERGENCY_IDLE_OR_RECALLED())
        {
            foreach (obj_machinery_computer_communications C in Dm.each<obj_machinery_computer_communications>(GLOB.shuttle_caller_list))
                C.post_status("shuttle");
        }
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:334
    public override async DmTask<object> can_interact_with(atom A = null, object treat_mob_as_adjacent = null)
    {
        object dot = await base.can_interact_with(A, treat_mob_as_adjacent);
        if (Dm.truthy(dot))
            return dot;
        turf ai_turf = get_turf(this);
        turf target_turf = get_turf(A);

        if (target_turf == null)
            return null;

        if (!Dm.truthy(is_valid_z_level(ai_turf, target_turf)))
            return FALSE;

        if (loc is obj_item_aicard)
        {
            if (ai_turf == null)
                return FALSE;
            return ISINRANGE(target_turf.x, ai_turf.x - interaction_range, ai_turf.x + interaction_range)
                && ISINRANGE(target_turf.y, ai_turf.y - interaction_range, ai_turf.y + interaction_range) ? TRUE : FALSE;
        }
        return SScameras.is_visible_by_cameras(target_turf) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:354
    public override async DmTask cancel_camera(params object[] __extra)
    {
        await view_core();
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:357  GAME_VERB_HIDDEN(/mob/living/silicon/ai, ai_camera_track, "track"): the verb proc
    public virtual void ai_camera_track(params object[] __extra)
    {
        Waiting.Detach(__gvb_ai_camera_track(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:357  the body
    public virtual async DmTask __gvb_ai_camera_track(DmList structured_args = null)
    {
        await ai_tracking_tool.track_input(this);
    }

    ///Called when an AI starts tracking a new target, before the eye moves. Saves the return point for the "last camera" hotkey.
    // tg: code/modules/mob/living/silicon/ai/ai.dm:362
    public virtual void on_track_started(datum_trackable source = null, mob_living target = null)
    {
        if (eyeobj != null)
            cam_prev = get_turf(eyeobj);
    }

    ///Called when an AI finds their tracking target.
    // tg: code/modules/mob/living/silicon/ai/ai.dm:368
    public virtual void on_track_target(datum_trackable source = null, mob_living target = null)
    {
        if (eyeobj != null)
            eyeobj.setLoc(get_turf(target));
        else
            Waiting.NoSleep(view_core());
    }

    /// Keeps our rate of gliding in step with the mob we're following
    // tg: code/modules/mob/living/silicon/ai/ai.dm:376
    public virtual void tracked_glidesize_changed(datum_trackable source = null, mob_living target = null, object new_glide_size = null)
    {
        if (eyeobj != null)
            eyeobj.glide_size = Dm.num(new_glide_size);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:381  GAME_VERB(/mob/living/silicon/ai, toggle_anchor, "Toggle Floor Bolts", "AI Commands"): the verb proc
    public virtual void toggle_anchor(params object[] __extra)
    {
        Waiting.Detach(__gvb_toggle_anchor(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:381  the body
    public virtual async DmTask __gvb_toggle_anchor(DmList structured_args = null)
    {
        if (!isturf(loc)) // if their location isn't a turf
            return; // stop
        if (Dm.eq(stat, DEAD))
            return;
        if (Dm.truthy(incapacitated))
        {
            if (battery < 50)
            {
                to_chat(this, span_warning(I18n.T("Insufficient backup power!")));
                return;
            }
            battery = battery - 50;
            to_chat(this, span_notice(I18n.T("You route power from your backup battery to move the bolts.")));
        }
        flip_anchored();
        to_chat(this, I18n.T("<b>You are now []anchored.</b>", Dm.truthy(is_anchored) ? "" : "un"));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:396
    public virtual void flip_anchored()
    {
        if (Dm.truthy(is_anchored))
        {
            is_anchored = !Dm.truthy(is_anchored) ? TRUE : FALSE;
            move_resist = MOVE_FORCE_NORMAL;
            status_flags = Dm.bitor(status_flags, CANPUSH); //we want the core to be push-able when un-anchored
            REMOVE_TRAIT(this, TRAIT_NO_TELEPORT, AI_ANCHOR_TRAIT);
        }
        else
        {
            is_anchored = !Dm.truthy(is_anchored) ? TRUE : FALSE;
            move_resist = MOVE_FORCE_OVERPOWERING;
            status_flags = Dm.bitand(status_flags, Dm.bitnot(CANPUSH)); //we dont want the core to be push-able when anchored
            ADD_TRAIT(this, TRAIT_NO_TELEPORT, AI_ANCHOR_TRAIT);
        }
    }

    /// Creates an MMI of the AI based on its configuration.
    // tg: code/modules/mob/living/silicon/ai/ai.dm:409
    public virtual obj_item_brain_processor make_mmi(atom destination = null)
    {
        obj_item_brain_processor copied_mmi;
        if (Dm.truthy(posibrain_inside))
        {
            copied_mmi = Dm.@new<obj_item_brain_processor_positronic>(destination, FALSE);
            Waiting.NoSleep(copied_mmi.brainmob.set_stat(STABLE)); // posis are dead by default
        }
        else
            copied_mmi = Dm.@new<obj_item_brain_processor_organic>(destination, Dm.@new<obj_item_organ_brain>());

        copied_mmi.brainmob.gender = gender;
        copied_mmi.set_name(real_name);
        copied_mmi.set_suicide(HAS_TRAIT(this, TRAIT_SUICIDED) ? TRUE : FALSE);

        copied_mmi.update_appearance();
        return copied_mmi;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:420
    public virtual async DmTask<obj_structure_ai_core> ai_mob_to_structure()
    {
        disconnect_shell();
        ShutOffDoomsdayDevice();
        obj_structure_ai_core ai_core = Dm.@new<obj_structure_ai_core>(get_turf(this), CORE_STATE_FINISHED, make_mmi());
        if (mind != null)
            await mind.transfer_to(ai_core.core_mmi.brainmob);
        qdel(this);
        return ai_core;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:428
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        await base.Topic(href, href_list);
        if (usr != this)
            return null;

        if (Dm.truthy(href_list["track_cyborg"]))
        {
            mob_living_silicon_robot cyborg = locate_ref_in(href_list["track_cyborg"], connected_robots) as mob_living_silicon_robot;
            if (cyborg == null)
                return null;
            ai_tracking_tool.set_tracked_mob(cyborg);
        }

        if (Dm.truthy(href_list["emergencyAPC"])) //This check comes before incapacitated because the only time it would be useful is when we have no power.
        {
            if (apc_override == null)
            {
                to_chat(this, span_notice(I18n.T("APC backdoor is no longer available.")));
                return null;
            }
            await apc_override.ui_interact(this);
            return null;
        }

        if (Dm.truthy(incapacitated))
            return null;

        if (Dm.truthy(href_list["switchcamera"]))
            switchCamera(locate_ref_in(href_list["switchcamera"], SScameras.cameras) as obj_machinery_camera);
        if (Dm.truthy(href_list["showalerts"]))
            await alert_control.ui_interact(this);
        // #ifdef AI_VOX: the announcement words' links (AI_VOX is defined, __DEFINES/_flags.dm)
        if (Dm.truthy(href_list["say_word"]))
        {
            await play_vox_word(href_list["say_word"], null, this);
            return null;
        }
        if (Dm.truthy(href_list["show_tablet_note"]))
        {
            if (Dm.truthy(last_tablet_note_seen))
                ClientSeam.Browse(this, last_tablet_note_seen, "window=show_tablet");
        }
        //Carn: holopad requests
        if (Dm.truthy(href_list["jump_to_holopad"]))
        {
            obj_machinery_holopad Holopad = locate_ref_in(href_list["jump_to_holopad"], SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_holopad)) as DmList) as obj_machinery_holopad;
            if (Holopad != null)
            {
                cam_prev = get_turf(eyeobj);
                eyeobj.setLoc(Holopad);
            }
            else
                to_chat(this, span_notice(I18n.T("Unable to locate the holopad.")));
        }
        if (Dm.truthy(href_list["project_to_holopad"]))
        {
            obj_machinery_holopad Holopad = locate_ref_in(href_list["project_to_holopad"], SSmachines.get_machines_by_type(typeof(obj_machinery_holopad)) as DmList) as obj_machinery_holopad;
            if (Holopad != null)
            {
                lastloc = get_turf(eyeobj);
                await Holopad.attack_ai_secondary(this); //may as well recycle
            }
            else
                to_chat(this, span_notice(I18n.T("Unable to project to the holopad.")));
        }
        if (Dm.truthy(href_list["ai_take_control"])) //Mech domination
        {
            // the mechs are inert (not this campaign): tg's mech domination (transfer_ai(AI_MECH_HACK)) has no mech to find
            Dm.Deferred("code/modules/mob/living/silicon/ai/ai.dm:476", "Topic ai_take_control: the AI's mech domination (the mechs are not ported)");
        }
        if (Dm.truthy(href_list["show_paper_note"]))
        {
            obj_item_paper paper_note = Dm.locate<obj_item_paper>(href_list["show_paper_note"]);
            if (paper_note == null)
                return null;

            await paper_note.show_through_camera(usr as mob_living);
        }
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:506
    public virtual bool switchCamera(obj_machinery_camera C = null)
    {
        if (QDELETED(C))
            return false;

        if (QDELETED(eyeobj))
        {
            Waiting.NoSleep(view_core());
            return false;
        }

        ai_tracking_tool.reset_tracking();

        // ok, we're alive, camera is good and in our network...
        eyeobj.setLoc(get_turf(C));
        return true;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:520  GAME_VERB_PROC_DESC(/mob/living/silicon/ai, botcall, "Access Robot Control", ...): the verb proc
    public virtual void botcall(params object[] __extra)
    {
        Waiting.Detach(__gvb_botcall(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:520  the body
    public virtual async DmTask __gvb_botcall(DmList structured_args = null)
    {
        robot_control ??= Dm.@new<datum_robot_control>(this);
        await robot_control.ui_interact(this);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:527
    public virtual async DmTask set_waypoint(atom A = null)
    {
        turf turf_check = get_turf(A);
        //The target must be in view of a camera or near the core.
        if (Dm.@in(turf_check, range(get_turf(this))))
            await call_bot(turf_check);
        else if (SScameras.is_visible_by_cameras(turf_check))
            await call_bot(turf_check);
        else
            to_chat(this, span_danger(I18n.T("Selected location is not visible.")));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:536
    public virtual async DmTask call_bot(turf waypoint = null)
    {
        mob_living bot = bot_ref?.resolve() as mob_living;
        if (bot == null)
            return;
        mob_living_basic_bot basic_bot = bot as mob_living_basic_bot;
        object summon_success = await basic_bot.summon_bot(this, waypoint, grant_all_access: TRUE);

        string chat_message = Dm.truthy(summon_success) ? "Sending command to bot..." : "Interface error. Unit is already in use.";
        to_chat(this, span_notice(I18n.T(chat_message)));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:546
    public virtual object alarm_triggered(datum source = null, object alarm_type = null, area source_area = null)
    {
        DmList cameras = source_area.cameras;
        object home_name = source_area.name;

        if (Dm.truthy(length(cameras)))
        {
            obj_machinery_camera cam = cameras[1] as obj_machinery_camera;
            if (Dm.truthy(cam.can_use()))
                queueAlarm(Dm.text("--- [] alarm detected in []! (<A href=byond://?src=[];switchcamera=[]>[]</A>)", alarm_type, home_name, REF(this), REF(cam), cam.c_tag), alarm_type);
            else
            {
                bool first_run = false;
                string dat2 = "";
                foreach (obj_machinery_camera camera in Dm.each<obj_machinery_camera>(cameras))
                {
                    dat2 += Dm.text("[]<A href=byond://?src=[];switchcamera=[]>[]</A>", !first_run ? "" : " | ", REF(this), REF(camera), camera.c_tag);
                    first_run = true;
                }
                queueAlarm(Dm.text("--- [] alarm detected in []! ([])", alarm_type, home_name, dat2), alarm_type);
            }
        }
        else
            queueAlarm(Dm.text("--- [] alarm detected in []! (No Camera)", alarm_type, home_name), alarm_type);
        return 1;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:565
    public virtual void alarm_cleared(datum source = null, object alarm_type = null, area source_area = null)
    {
        queueAlarm(Dm.text("--- [] alarm in [] has been cleared.", alarm_type, source_area.name), alarm_type, 0);
    }

    //Replaces /mob/living/silicon/ai/verb/change_network() in ai.dm & camera.dm
    //Adds in /mob/living/silicon/ai/proc/ai_network_change() instead
    //Addition by Mord_Sith to define AI's network change ability
    // tg: code/modules/mob/living/silicon/ai/ai.dm:572  GAME_VERB_PROC(/mob/living/silicon/ai, ai_network_change, "Jump To Network", "AI Commands"): the verb proc
    public virtual void ai_network_change(params object[] __extra)
    {
        Waiting.Detach(__gvb_ai_network_change(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:572  the body
    public virtual async DmTask __gvb_ai_network_change(DmList structured_args = null)
    {
        ai_tracking_tool.reset_tracking();
        DmList cameralist = Dm.list();

        if (Dm.truthy(incapacitated))
            return;

        mob_living_silicon_ai U = usr as mob_living_silicon_ai;

        foreach (obj_machinery_camera C in Dm.each<obj_machinery_camera>(SScameras.cameras))
        {
            turf camera_turf = get_turf(C); //get camera's turf in case it's built into something so we don't get z=0

            DmList tempnetwork = C.network;
            if (camera_turf == null || !(Dm.truthy(is_station_level(camera_turf.z)) || Dm.truthy(is_mining_level(camera_turf.z)) || Dm.@in(CAMERANET_NETWORK_SS13, tempnetwork)))
                continue;
            if (!Dm.truthy(C.can_use()))
                continue;
            tempnetwork.Remove(CAMERANET_NETWORK_RD, CAMERANET_NETWORK_ORDNANCE, CAMERANET_NETWORK_PRISON);
            if (Dm.truthy(length(tempnetwork)))
            {
                foreach (object i in Dm.each(C.network))
                    cameralist[i] = i;
            }
        }
        DmList old_network = network;
        object chosen = await tgui_input_list(U, "Which network would you like to view?", "Camera Network", sort_list(cameralist) as DmList);

        if (U.eyeobj == null)
        {
            await U.view_core();
            return;
        }

        if (chosen == null)
            network = old_network; // If nothing is selected
        else
        {
            network = Dm.list(chosen);
            foreach (obj_machinery_camera C in Dm.each<obj_machinery_camera>(SScameras.cameras))
            {
                if (!Dm.truthy(C.can_use()))
                    continue;
                if (Dm.@in(chosen, C.network))
                {
                    U.eyeobj.setLoc(get_turf(C));
                    break;
                }
            }
        }
        to_chat(this, span_notice(I18n.T("Switched to the \"[]\" camera network.", uppertext(chosen ?? Dm.str(old_network?.Join(", "))))));
    }
//End of code by Mord_Sith

    //I am the icon meister. Bow fefore me. //>fefore
    // tg: code/modules/mob/living/silicon/ai/ai.dm:614  GAME_VERB_PROC_DESC(/mob/living/silicon/ai, ai_hologram_change, "Change Hologram", ...): the verb proc
    public virtual void ai_hologram_change(params object[] __extra)
    {
        Waiting.Detach(__gvb_ai_hologram_change(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:614  the body
    public virtual async DmTask<object> __gvb_ai_hologram_change(DmList structured_args = null)
    {
        if (Dm.truthy(incapacitated))
            return null;

        ai_holocolor = await tgui_color_picker(usr, "Choose a color for your hologram", "Hologram Color");
        if (Dm.truthy(ai_holocolor))
        {
            DmList ai_holo_hsv = rgb2hsv(ai_holocolor);
            DmList default_hsv = rgb2hsv(COLOR_AI_HOLOGRAM_BLUE);

            default_hsv[1] = ai_holo_hsv[1];

            ai_holocolor = hsv2rgb(default_hsv);
        }

        DmList choices = TgState.Current.mob_living_silicon_ai__hologram_choices ??= Dm.add(assoc_to_keys(GLOB.ai_hologram_category_options), HOLOGRAM_CHOICE_CHARACTER) as DmList;
        object choice = await tgui_input_list(usr, "What kind of hologram do you want?", "Customize", choices);
        if (!Dm.truthy(choice))
            return null;

        if (Dm.eq(choice, HOLOGRAM_CHOICE_CHARACTER))
        {
            object character_type = await tgui_alert(usr,
                "Would you like to base it off of your current character loadout, or a crewmember on the manifest?", "Customize",
                Dm.list(CHARACTER_TYPE_CREWMEMBER, CHARACTER_TYPE_SELF)
            );
            if (Dm.eq(character_type, CHARACTER_TYPE_CREWMEMBER))
            {
                DmList personnel_list = Dm.list();

                foreach (datum_record_locked record in Dm.each<datum_record_locked>(GLOB.manifest.locked)) //Look in data core locked.
                    personnel_list[Dm.text("[]: []", record.name, record.rank)] = record.character_appearance; //Pull names, rank, and image.
                if (!Dm.truthy(length(personnel_list)))
                {
                    await tgui_alert(usr, "No suitable records found. Aborting.");
                    return null;
                }

                object input = await tgui_input_list(usr, "Select a crew member", "Station Member", sort_list(personnel_list) as DmList);
                if (input == null)
                    return null;
                if (personnel_list[input] == null)
                    return null;
                if (personnel_list[input] is mutable_appearance character_icon)
                {
                    character_icon.dir = SOUTH; // /image/proc/setDir (icons.dm:829) on an appearance
                    hologram_appearance = character_icon;
                }
            }
            else if (Dm.eq(character_type, CHARACTER_TYPE_SELF))
            {
                object confirmation = await tgui_alert(usr,
                    Dm.text("WARNING: Your AI hologram will take the appearance of your currently selected character ([]). Are you sure you want to proceed?", usr.client.prefs?.read_preference(typeof(datum_preference_name_real_name))), "Customize",
                    Dm.list("Yes", "No")
                );
                if (Dm.eq(confirmation, "Yes"))
                {
                    mob_living_carbon_human_dummy ai_dummy = Dm.@new<mob_living_carbon_human_dummy>();
                    mutable_appearance dummy_appearance = await usr.client.prefs.render_new_preview_appearance(ai_dummy) as mutable_appearance;
                    qdel(ai_dummy);
                    if (dummy_appearance != null)
                        hologram_appearance = dummy_appearance;
                    return TRUE;
                }
                else
                    return FALSE;
            }
        }
        else
        {
            DmList options = GLOB.ai_hologram_category_options[choice] as DmList;
            object option = await tgui_input_list(usr, "Select a hologram", "Hologram", options);
            if (!Dm.truthy(option))
                return null;

            object icon = GLOB.ai_hologram_icons[option];
            if (!Dm.truthy(icon))
                throw Dm.CRASH(Dm.text("hologram icon for [] does not exist in global icon list", option), "code/modules/mob/living/silicon/ai/ai.dm", 684);

            object icon_state = GLOB.ai_hologram_icon_state[option];
            if (!istext(icon_state)) //Possible that the icon_state can be "", which is valid
                throw Dm.CRASH(Dm.text("hologram icon_state for [] does not exist in global icon_state list", option), "code/modules/mob/living/silicon/ai/ai.dm", 688);

            hologram_appearance = mutable_appearance(icon, Dm.str(icon_state)) as mutable_appearance;
        }
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:719
    public virtual void toggle_camera_light()
    {
        camera_light_on = !Dm.truthy(camera_light_on) ? TRUE : FALSE;

        if (!Dm.truthy(camera_light_on))
        {
            to_chat(this, I18n.T("Camera lights deactivated."));

            foreach (obj_machinery_camera C in Dm.each<obj_machinery_camera>(lit_cameras))
            {
                Waiting.NoSleep(C.set_light(0));
                lit_cameras = Dm.list();
            }

            return;
        }

        light_cameras();

        to_chat(this, I18n.T("Camera lights activated."));
    }

    // Allows AIs to turn their hologram instead on alt-move
    // tg: code/modules/mob/living/silicon/ai/ai.dm:737
    public override async DmTask keybind_face_direction(object direction = null)
    {
        if (current is obj_machinery_holopad active_pad && Dm.truthy(active_pad.masters[this]))
        {
            obj_effect_overlay_holo_pad_hologram ai_holo = active_pad.masters[this] as obj_effect_overlay_holo_pad_hologram;
            await ai_holo.setDir(direction);
            return;
        }
        await base.keybind_face_direction(direction);
    }

    //AI_CAMERA_LUMINOSITY

    // tg: code/modules/mob/living/silicon/ai/ai.dm:747
    public virtual void light_cameras()
    {
        DmList visible = Dm.list();
        foreach (datum_camerachunk chunk in Dm.each<datum_camerachunk>(eyeobj.visibleCameraChunks))
        {
            foreach (object z_key in Dm.each(chunk.cameras))
            {
                foreach (obj_machinery_camera camera in Dm.each<obj_machinery_camera>(chunk.cameras[z_key]))
                {
                    if (camera == null || !Dm.truthy(camera.can_use()) || get_dist(camera, eyeobj) > 7 || !Dm.truthy(camera.internal_light))
                        continue;
                    visible = Dm.bitor(visible, camera) as DmList;
                }
            }
        }

        DmList add = Dm.sub(visible, lit_cameras) as DmList;
        DmList remove = Dm.sub(lit_cameras, visible) as DmList;

        foreach (obj_machinery_camera C in Dm.each<obj_machinery_camera>(remove))
        {
            Dm.remove(ref lit_cameras, C); //Removed from list before turning off the light so that it doesn't check the AI looking away.
            C.Togglelight(0);
        }
        foreach (obj_machinery_camera C in Dm.each<obj_machinery_camera>(add))
        {
            C.Togglelight(1);
            lit_cameras = Dm.bitor(lit_cameras, C) as DmList;
        }
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:769  GAME_VERB_PROC_DESC(/mob/living/silicon/ai, control_integrated_radio, "Transceiver Settings", ...): the verb proc
    public virtual void control_integrated_radio(params object[] __extra)
    {
        Waiting.Detach(__gvb_control_integrated_radio(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:769  the body
    public virtual async DmTask __gvb_control_integrated_radio(DmList structured_args = null)
    {
        if (Dm.truthy(incapacitated))
            return;

        to_chat(this, I18n.T("Accessing Subspace Transceiver control..."));
        if (radio != null)
            await (radio as obj_item_radio).interact(this);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:778
    public virtual void set_syndie_radio()
    {
        (radio as obj_item_radio)?.make_syndie();
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:782  GAME_VERB_PROC_DESC(/mob/living/silicon/ai, set_automatic_say_channel, "Set Auto Announce Mode", ...): the verb proc
    public virtual void set_automatic_say_channel(params object[] __extra)
    {
        Waiting.Detach(__gvb_set_automatic_say_channel(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:782  the body
    public virtual async DmTask __gvb_set_automatic_say_channel(DmList structured_args = null)
    {
        if (Dm.truthy(incapacitated))
            return;
        await set_autosay();
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:788
    public override async DmTask<object> transfer_ai(object interaction = null, mob user = null, mob_living_silicon_ai AI = null, obj_item_aicard card = null)
    {
        if (!Dm.truthy(await base.transfer_ai(interaction, user, AI, card)))
            return null;
        if (!Dm.eq(interaction, AI_TRANS_TO_CARD)) //The only possible interaction. Upload AI mob to a card.
            return null;
        if (!Dm.truthy(can_be_carded))
        {
            user.balloon_alert(user, I18n.T("transfer failed!"));
            return null;
        }
        disconnect_shell(); //If the AI is controlling a borg, force the player back to core!
        if (mind == null)
        {
            user.balloon_alert(user, I18n.T("no intelligence detected!")); // average tg coder am i right
            return null;
        }
        ShutOffDoomsdayDevice();
        obj_structure_ai_core new_core = Dm.@new<obj_structure_ai_core>(loc, CORE_STATE_FINISHED, make_mmi());
        new_core.circuit.battery = battery;
        ai_restore_power(); //So the AI initially has power.
        set_control_disabled(TRUE); //Can't control things remotely if you're stuck in a card!
        radio_enabled = FALSE; //No talking on the built-in radio for you either!
        await forceMove(card);
        card.AI = this;
        to_chat(this, I18n.T("You have been downloaded to a mobile storage device. Remote device connection severed."));
        to_chat(user, Dm.text("[]: [] ([].exe) removed from host terminal and stored within local memory.", span_boldnotice(I18n.T("Transfer successful")), name, rand(1000, 9999)));
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:812
    public override async DmTask<object> can_perform_action(atom target = null, object action_bitflags = null)
    {
        if (Dm.truthy(control_disabled))
        {
            to_chat(this, span_warning(I18n.T("You can't do that right now!")));
            return FALSE;
        }
        return can_see(target) && Dm.truthy(await base.can_perform_action(target, action_bitflags)) ? TRUE : FALSE; //stop AIs from leaving windows open and using then after they lose vision
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:818
    public virtual bool can_see(atom A = null)
    {
        if (isturf(loc)) //AI in core, check if on cameras
        {
            //get_turf_pixel() is because APCs in maint aren't actually in view of the inner camera
            //apc_override is needed here because AIs use their own APC when depowered
            return SScameras.is_visible_by_cameras(get_turf_pixel(A) as atom) || A == apc_override;
        }
        //AI is carded/shunted
        //view(src) returns nothing for carded/shunted AIs and they have X-ray vision so just use get_dist
        DmList viewscale = getviewsize(client.view);
        return get_dist(this, A) <= max(Dm.num(viewscale[1]) * 0.5f, Dm.num(viewscale[2]) * 0.5f);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:829
    public virtual async DmTask relay_speech(atom_movable speaker = null, datum_language message_language = null, object raw_message = null, object radio_freq = null, DmList spans = null, DmList message_mods = null)
    {
        message_mods ??= Dm.list();
        object raw_translation = translate_language(speaker, message_language, raw_message, spans, message_mods);
        atom_movable source = speaker.GetSource() as atom_movable ?? speaker; // is the speaker virtual/radio
        object treated_message = source.generate_messagepart(raw_translation, spans, message_mods);

        string start = "Relayed Speech: ";
        object namepart = speaker.get_message_voice();
        string hrefpart = Dm.text("<a href='byond://?src=[];track=[]'>", REF(this), html_encode(namepart));
        string jobpart = "Unknown";

        // if voice is concealed, job is concealed
        // on the other hand we don't care about TRAIT_UNKNOWN_APPEARANCE
        // (AI can associate voice -> name -> crew record -> job)
        if (!HAS_TRAIT(speaker, TRAIT_UNKNOWN_VOICE))
        {
            if (speaker is mob_living living_speaker)
            {
                if (Dm.truthy(living_speaker.job))
                    jobpart = Dm.text("[]", living_speaker.job);
            }
            if (speaker is obj_effect_overlay_holo_pad_hologram holo)
            {
                if (Dm.truthy(holo.Impersonation?.job))
                    jobpart = Dm.text("[]", holo.Impersonation.job);
                else if (Dm.truthy(usr?.job)) // not great, but AI holograms have no other usable ref
                    jobpart = Dm.text("[]", usr.job);
            }
        }

        string rendered = Dm.text("<i><span class='game say'>[][]<span class='message'>[]</span></span></i>", start, span_name(Dm.text("[][] ([])</a> ", hrefpart, namepart, jobpart)), treated_message);

        if (Dm.truthy(client?.prefs.read_preference(typeof(datum_preference_toggle_enable_runechat))) && (Dm.truthy(client.prefs.read_preference(typeof(datum_preference_toggle_enable_runechat_non_mobs))) || ismob(speaker)))
            create_chat_message(speaker, message_language, raw_translation, spans);
        show_message(rendered, 2);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:863
    public override async DmTask<object> fully_replace_character_name(object oldname = null, object newname = null, float? log_new_name = null)
    {
        object dot = await base.fully_replace_character_name(oldname, newname, log_new_name);
        if (!Dm.truthy(dot))
            return dot;
        if (eyeobj != null)
        {
            eyeobj.name = Dm.text("[] (AI Eye)", newname);
            modularInterface.imprint_id(name: real_name);
        }

        // Notify Cyborgs
        foreach (mob_living_silicon_robot slave in Dm.each<mob_living_silicon_robot>(connected_robots))
            await slave.show_laws();
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:894
    public virtual async DmTask add_malf_picker()
    {
        if (malf_picker != null)
        {
            stack_trace(Dm.text("Attempted to give malf AI malf picker to \\[[]\\], who already has a malf picker.", this), "code/modules/mob/living/silicon/ai/ai.dm", 896);
            return;
        }
        to_chat(this, I18n.T("In the top left corner of the screen you will find the Malfunction Modules button, where you can purchase various abilities, from upgraded surveillance to station ending doomsday devices."));
        to_chat(this, I18n.T("You are also capable of hacking APCs, which grants you more points to spend on your Malfunction powers. The drawback is that a hacked APC will give you away if spotted by the crew. Hacking an APC takes 60 seconds."));
        await view_core(); //A BYOND bug requires you to be viewing your core before your verbs update
        malf_picker = Dm.@new<datum_module_picker>();
        modules_action = Dm.@new<datum_action_innate_choose_modules>(malf_picker);
        Waiting.NoSleep(modules_action.Grant(this));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:905
    public override async DmTask<object> reset_perspective(atom new_eye = null, params object[] __extra)
    {
        // SHOULD_CALL_PARENT(FALSE) // I hate you all
        if (Dm.truthy(camera_light_on))
            light_cameras();
        if (new_eye is obj_machinery_camera)
            current = null; // current = new_eye: tg keeps the camera in the AI's holopad var (typed /obj/machinery/holopad in the port, ai_defines.dm:163)
        if (client == null)
            return null;

        if (ismovable(new_eye))
        {
            if (new_eye != GLOB.ai_camera_room_landmark)
                end_multicam();
            client.perspective = EYE_PERSPECTIVE;
            await client.set_eye(new_eye);
        }
        else
        {
            end_multicam();
            if (isturf(loc))
            {
                if (eyeobj != null)
                {
                    await client.set_eye(eyeobj);
                    client.perspective = EYE_PERSPECTIVE;
                }
                else
                {
                    await client.set_eye(client.mob);
                    client.perspective = MOB_PERSPECTIVE;
                }
            }
            else
            {
                client.perspective = EYE_PERSPECTIVE;
                await client.set_eye(loc);
            }
        }
        update_sight();
        if (client.eye != this)
            (client.eye as atom)?.get_remote_view_fullscreens(this);
        else
            clear_fullscreen("remote_view", 0);

        // I am so sorry
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_RESET_PERSPECTIVE]))
            _SendSignal(COMSIG_MOB_RESET_PERSPECTIVE, Dm.list(this));
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:940
    public override async DmTask<object> revive(float? full_heal_flags = null, float? excess_healing = null, float? force_grab_ghost = null)
    {
        object dot = await base.revive(full_heal_flags, excess_healing, force_grab_ghost);
        if (!Dm.truthy(dot))
            return dot;

        update_appearance();

        set_eyeobj_visible(TRUE);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:949
    public virtual async DmTask malfhacked(obj_machinery_power_apc apc = null)
    {
        malfhack = null;
        malfhacking = FALSE;
        clear_alert(ALERT_HACKING_APC);

        if (apc == null || QDELETED(apc) || Dm.truthy(Dm.bitand(apc.machine_stat, BROKEN)))
        {
            to_chat(this, span_danger(I18n.T("Hack aborted. The designated APC no longer exists on the power network.")));
            playsound(get_turf(this), "sound/machines/buzz/buzz-two.ogg", 50, TRUE, ignore_walls: FALSE);
            return;
        }
        if (Dm.truthy(apc.aidisabled))
        {
            to_chat(this, span_danger(I18n.T("Hack aborted. [] is no longer responding to our systems.", apc)));
            playsound(get_turf(this), "sound/machines/buzz/buzz-sigh.ogg", 50, TRUE, ignore_walls: FALSE);
            return;
        }

        malf_picker.processing_time = Dm.num(malf_picker.processing_time) + max(0, 9 - Dm.length(hacked_apcs)); // Less resources for each apc hacked, 9 instead of 10 is because you will get 1 as soon as the hacked apc processes
        area apcarea = apc.area;
        datum_ai_module_malf_destructive_nuke_station doom_n_boom = Dm.locate<datum_ai_module_malf_destructive_nuke_station>(malf_picker.possible_modules["Destructive Modules"]);
        if (doom_n_boom != null && Dm.truthy(is_type_in_list(apcarea, doom_n_boom.discount_areas)) && !Dm.truthy(is_type_in_list(apcarea, doom_n_boom.hacked_command_areas)))
        {
            Dm.append(ref doom_n_boom.hacked_command_areas, apcarea);
            doom_n_boom.cost = max(50, 130 - (Dm.length(doom_n_boom.hacked_command_areas) * 20));
            datum_antagonist_malf_ai malf_ai_datum = mind.has_antag_datum(typeof(datum_antagonist_malf_ai)) as datum_antagonist_malf_ai;
            if (malf_ai_datum != null)
                await malf_ai_datum.update_static_data_for_all_viewers();
            else //combat software AIs use a different UI
                await malf_picker.update_static_data_for_all_viewers();
        }
        if (apc.malfai != null) // another malf hacked this one; counter-hack!
        {
            to_chat(apc.malfai, span_warning(I18n.T("An adversarial subroutine has counter-hacked []!", apc)));
            Dm.remove(ref apc.malfai.hacked_apcs, apc);
        }
        apc.malfai = this;
        apc.malfhack = TRUE;
        apc.locked = TRUE;
        apc.coverlocked = TRUE;
        apc.flicker_hacked_icon();
        apc.set_hacked_hud();
        Dm.append(ref hacked_apcs, apc);
        playsound(get_turf(this), "sound/machines/ding.ogg", 50, TRUE, ignore_walls: FALSE);
        to_chat(this, I18n.T("Hack complete. [] is now under your exclusive control.", apc));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:985  GAME_VERB_DESC(/mob/living/silicon/ai, deploy_to_shell, "Deploy to Shell", ...): the verb proc
    public virtual void deploy_to_shell(params object[] __extra)
    {
        Waiting.Detach(__gvb_deploy_to_shell(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:985  the body
    public virtual async DmTask __gvb_deploy_to_shell(DmList structured_args = null)
    {
        await select_shell();
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:989
    public virtual async DmTask select_shell(mob_living_silicon_robot target = null)
    {
        if (Dm.truthy(incapacitated))
            return;
        if (Dm.truthy(control_disabled))
        {
            to_chat(this, span_warning(I18n.T("Wireless networking module is offline.")));
            return;
        }

        DmList possible = Dm.list();

        foreach (mob_living_silicon_robot R in Dm.each<mob_living_silicon_robot>(GLOB.available_ai_shells))
        {
            if (Dm.truthy(R.shell) && !Dm.truthy(R.deployed) && !Dm.eq(R.stat, DEAD) && (R.connected_ai == null || R.connected_ai == this))
                Dm.append(ref possible, R);
        }

        if (!Dm.truthy(LAZYLEN(possible)))
        {
            to_chat(this, I18n.T("No usable AI shell beacons detected."));
            return;
        }

        if (target == null || !Dm.@in(target, possible)) //If the AI is looking for a new shell, or its pre-selected shell is no longer valid
            target = await tgui_input_list(this, "Which body to control?", "Direct Control", sort_names(possible) as DmList) as mob_living_silicon_robot;

        if (target == null)
            return;
        if (Dm.eq(target.stat, DEAD) || Dm.truthy(target.deployed) || !(target.connected_ai == null || target.connected_ai == this))
            return;
        else if (mind != null)
        {
            RegisterSignal(target, COMSIG_LIVING_DEATH, nameof(disconnect_shell));
            deployed_shell = target;
            await target.deploy_init(this);
            await mind.transfer_to(target);
            ADD_TRAIT(target, TRAIT_LOUD_BINARY, REF(this));
        }
        diag_hud_set_deployed();
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1054
    public virtual void disconnect_shell(params object[] __extra)
    {
        if (deployed_shell != null) //Forcibly call back AI in event of things such as damage, EMP or power loss.
        {
            to_chat(this, span_danger(I18n.T("Your remote connection has been reset!")));
            Waiting.NoSleep(deployed_shell.undeploy());
        }
        diag_hud_set_deployed();
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1061
    public override void resist()
    {
        return;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1064
    public virtual void camera_visibility(mob_eye_camera_ai moved_eye = null)
    {
        SScameras.update_eye_chunk(moved_eye);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1067
    public override async DmTask<object> forceMove(atom destination = null, params object[] __extra)
    {
        object dot = await base.forceMove(destination);
        if (Dm.truthy(dot))
            end_multicam();
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1072
    public override async DmTask<object> up()
    {
        if (Dm.truthy(await eyeobj.zMove(UP, z_move_flags: ZMOVE_FEEDBACK)))
            to_chat(this, span_notice(I18n.T("You move upwards.")));
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1076
    public override async DmTask<object> down()
    {
        if (Dm.truthy(await eyeobj.zMove(DOWN, z_move_flags: ZMOVE_FEEDBACK)))
            to_chat(this, span_notice(I18n.T("You move down.")));
        return null;
    }

    /// Proc to hook behavior to the changes of the value of [aiRestorePowerRoutine].
    // tg: code/modules/mob/living/silicon/ai/ai.dm:1081
    public virtual object setAiRestorePowerRoutine(float new_value = 0)
    {
        if (new_value == aiRestorePowerRoutine)
            return null;
        float dot = aiRestorePowerRoutine;
        aiRestorePowerRoutine = new_value;
        if (Dm.truthy(aiRestorePowerRoutine))
        {
            if (!Dm.truthy(dot))
                ADD_TRAIT(this, TRAIT_INCAPACITATED, POWER_LACK_TRAIT);
        }
        else if (Dm.truthy(dot))
            REMOVE_TRAIT(this, TRAIT_INCAPACITATED, POWER_LACK_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1093
    public virtual async DmTask show_camera_list()
    {
        DmList cameras = SScameras.get_available_camera_by_tag_list(network);
        object camera_tag = await tgui_input_list(this, "Choose which camera you want to view", "Cameras", cameras);
        if (camera_tag == null)
            return;

        obj_machinery_camera chosen_camera = cameras[camera_tag] as obj_machinery_camera;
        if (chosen_camera == null)
            return;

        switchCamera(chosen_camera);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1105
    public override async DmTask on_handsblocked_start()
    {
        await DmTask.CompletedTask;
        return; // AIs have no hands
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1108
    public override void on_handsblocked_end()
    {
        return; // AIs have no hands
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1111
    public override object get_exp_list(object minutes = null)
    {
        DmList dot = base.get_exp_list(minutes) as DmList;
        dot[Dm.initial(typeof(datum_job_ai), (datum_job j) => j.title)] = minutes;
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1115
    public override object get_voice(float? add_id_name = null)
    {
        if (Dm.truthy(ai_voicechanger?.changing_voice))
            return ai_voicechanger.say_name;
        return base.get_voice(add_id_name);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1120
    public override object get_unconscious_appearance()
    {
        image static_overlay = Dm.image(Dm.resource("icons/effects/effects.dmi"), null, "static_base");
        static_overlay.blend_mode = BLEND_INSET_OVERLAY;

        image static_image = Dm.image(Dm.resource("icons/mob/silicon/ai.dmi"), this, "ai-empty");
        static_image.appearance_flags = Dm.bitor(static_image.appearance_flags, KEEP_TOGETHER);
        Dm.append(ref static_image.overlays, static_overlay);
        static_image.@override = TRUE;
        static_image.name = "unknown AI";

        return static_image;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1132
    public virtual void set_control_disabled(object control_disabled = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_SILICON_AI_SET_CONTROL_DISABLED]))
            _SendSignal(COMSIG_SILICON_AI_SET_CONTROL_DISABLED, Dm.list(this, control_disabled));
        this.control_disabled = Dm.num(control_disabled);
    }

    /// Establishes a "core link" with a supplied core structure.
    /// This will register multiple signals and give the AI a strong reference to it.
    /// See [proc/resolve_core_link] or [proc/break_core_link] for ways to end the connection.
    // tg: code/modules/mob/living/silicon/ai/ai.dm:1140
    public virtual void create_core_link(obj_structure_ai_core core = null)
    {
        if (linked_core != null) //uh oh
            break_core_link();
        linked_core = core;

        //this block is kind of sketchy, but I don't think this should cause any problems
        qdel(core.core_mmi);
        core.core_mmi = make_mmi(core);

        RegisterSignals(linked_core, Dm.list(COMSIG_ATOM_DESTRUCTION, COMSIG_QDELETING), nameof(on_core_destroyed));
        RegisterSignals(linked_core, Dm.list(
            COMSIG_ATOM_ITEM_INTERACTION,
            COMSIG_ATOM_TOOL_ACT(TOOL_CROWBAR),
            COMSIG_ATOM_TOOL_ACT(TOOL_WRENCH),
            COMSIG_ATOM_TOOL_ACT(TOOL_WELDER),
            COMSIG_ATOM_TOOL_ACT(TOOL_WIRECUTTER),
            COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER)
        ), nameof(on_core_item_interaction));
        RegisterSignal(linked_core, COMSIG_ATOM_TAKE_DAMAGE, nameof(on_core_take_damage));
        RegisterSignal(linked_core, COMSIG_ATOM_EXITED, nameof(on_core_exited));
    }

    /// Elegantly closes the AI's link to a core structure,
    /// moving them to its location and cleaning it up. This is generally what you want to call.
    /// Prefer calling [proc/break_core_link] directly if the connection is meant to be suddenly severed.
    // tg: code/modules/mob/living/silicon/ai/ai.dm:1164
    public virtual async DmTask resolve_core_link()
    {
        if (linked_core == null) //oh no bro
            throw Dm.CRASH("tried to resolve a core link with no core!!!!", "code/modules/mob/living/silicon/ai/ai.dm", 1166);

        await forceMove(linked_core.loc);
        obj_structure_ai_core unlinked_core = linked_core;
        break_core_link();
        qdel(unlinked_core);
        await cancel_camera();
    }

    /// Handles unregistering the AI from its core. The core itself will not be cleaned up.
    /// Prefer calling [proc/resolve_core_link] if the connection is being closed elegantly.
    // tg: code/modules/mob/living/silicon/ai/ai.dm:1176
    public virtual void break_core_link()
    {
        if (linked_core == null)
            return;

        UnregisterSignal(linked_core, Dm.list(
            COMSIG_QDELETING, COMSIG_ATOM_DESTRUCTION, //on_core_destroyed
            //on_core_item_interaction
            COMSIG_ATOM_ITEM_INTERACTION,
            COMSIG_ATOM_TOOL_ACT(TOOL_CROWBAR),
            COMSIG_ATOM_TOOL_ACT(TOOL_WRENCH),
            COMSIG_ATOM_TOOL_ACT(TOOL_WELDER),
            COMSIG_ATOM_TOOL_ACT(TOOL_WIRECUTTER),
            COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER),
            COMSIG_ATOM_TAKE_DAMAGE, //on_core_take_damage
            COMSIG_ATOM_EXITED //on_core_exited
        ));
        linked_core = null;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1194
    public virtual object on_core_item_interaction(datum source = null, mob_living user = null, obj_item tool = null, DmList processing_recipes = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        to_chat(this, span_danger(I18n.T("CORE TAMPERING DETECTED!")));
        return NONE;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1202
    public virtual object on_core_take_damage(datum source = null, object damage_taken = null, params object[] __extra)
    {
        if (Dm.num(damage_taken) > 0)
            to_chat(this, span_danger(I18n.T("CORE DAMAGE DETECTED!")));
        return NONE;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1209
    public virtual void on_core_destroyed(datum source = null, object damage_flag = null)
    {
        to_chat(this, span_danger(I18n.T("Your core has been destroyed!")));
        ShutOffDoomsdayDevice();
        break_core_link();
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1216
    public virtual void on_core_exited(datum source = null, atom_movable gone = null, object direction = null)
    {
        if (gone is obj_item_brain_processor processor_gone)
        {
            on_core_destroyed(source, NONE);
            if (!Dm.truthy(IS_MALF_AI(this))) //don't pull back shunted malf AIs
            {
                Waiting.NoSleep(death(TRUE, FALSE));
                ///the drop_mmi param determines whether the MMI is dropped at their current location
                ///which in this case would be somewhere else, so we drop their MMI at the core instead
                if (mind != null)
                    Waiting.NoSleep(mind.transfer_to(processor_gone.brainmob));
                qdel(this);
            }
        }
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1230
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = "ai-core";
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1234
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        string screen_state; // Display
        string lights_state; // Lights

        if (client == null && mind == null)
        {
            screen_state = "ai-empty";

            lights_state = "lights_active";

            await set_light(0.2f, 0.2f, LIGHT_COLOR_FAINT_CYAN);
        }
        else if (Dm.eq(stat, DEAD))
        {
            object @base = display_icon_override ?? "ai";
            string dead_state = Dm.text("[]_dead", @base);

            if (Dm.truthy(icon_exists(icon, dead_state)))
                screen_state = dead_state;
            else
                screen_state = "ai_dead";

            mutable_appearance screen_overlay = mutable_appearance(icon, screen_state) as mutable_appearance;
            screen_overlay.appearance_flags = Dm.bitor(RESET_COLOR, KEEP_APART);
            Dm.append(ref dot, screen_overlay);

            lights_state = "lights_dead";
            await set_light(0.2f, 0.2f, LIGHT_COLOR_FAINT_CYAN);
        }
        else
        {
            lights_state = "lights_active";
            await set_light(0.3f, 0.3f, LIGHT_COLOR_CYAN);

            if (portrait_appearance != null)
                Dm.append(ref dot, portrait_appearance);
            else
            {
                screen_state = Dm.str(display_icon_override ?? "ai");
                mutable_appearance screen_overlay = mutable_appearance(icon, screen_state) as mutable_appearance;
                screen_overlay.layer = FLOAT_LAYER + 0.1f;
                screen_overlay.appearance_flags = Dm.bitor(RESET_COLOR, KEEP_APART);
                Dm.append(ref dot, screen_overlay);
                // DEVIATION(DV12): . += emissive_appearance(icon, screen_state, src)
            }
        }

        // Lights
        mutable_appearance lights_overlay = mutable_appearance(icon, lights_state) as mutable_appearance;
        lights_overlay.layer = FLOAT_LAYER;
        lights_overlay.appearance_flags = Dm.bitor(RESET_COLOR, KEEP_APART);
        Dm.append(ref dot, lights_overlay);

        // DEVIATION(DV12): . += emissive_appearance(icon, lights_state, src)
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1286
    public virtual void lawset_updated_sync_borgs(datum source = null, obj_machinery_ai_law_rack rack = null, object announce = null)
    {
        announce ??= TRUE;
        foreach (mob_living_silicon_robot bot in Dm.each<mob_living_silicon_robot>(connected_robots))
        {
            if (Dm.truthy(Waiting.NoSleep(bot.try_sync_laws())) && Dm.truthy(announce))
            {
                Waiting.Detach(bot.show_laws());
                bot.law_change_counter++;
            }
        }
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:1294
    public override async DmTask<object> point_at(atom pointed_atom = null, float? intentional = null, Type pointer_type = null)
    {
        if (Dm.@in(pointed_atom, contents))
            return FALSE;
        turf target_turf = get_turf(pointed_atom);
        if (target_turf == null)
            return FALSE;
        obj_machinery_holopad best_pad = null;
        foreach (obj_machinery_holopad pad in Dm.each<obj_machinery_holopad>(SSmachines.get_machines_by_type(typeof(obj_machinery_holopad))))
        {
            if (!Dm.truthy(pad.on_network) || !Dm.truthy(pad.is_operational) || Dm.truthy(pad.pointing))
                continue;
            if (!Dm.truthy(pad.validate_location(target_turf)))
                continue;
            turf pad_turf = get_turf(pad);
            if (!SScameras.is_visible_by_cameras(pad_turf))
                continue;
            if (best_pad == null || get_dist(pad_turf, target_turf) < get_dist(get_turf(best_pad), target_turf))
                best_pad = pad;
        }
        if (best_pad == null)
            return FALSE;
        obj visual = await best_pad.holo_point(pointed_atom, invisibility);
        if (visual == null)
            return FALSE;
        if (Dm.truthy(_listen_lookup?[COMSIG_MOVABLE_POINTED]))
            _SendSignal(COMSIG_MOVABLE_POINTED, Dm.list(this, pointed_atom, visual, intentional));
        return TRUE;
    }
}

// tg: code/modules/mob/living/silicon/ai/ai.dm:694
public partial class datum_action_innate_core_return : datum_action_innate
{
    // tg: code/modules/mob/living/silicon/ai/ai.dm:700
    public override async DmTask<object> Grant(mob new_owner = null, params object[] __extra)
    {
        object dot = await base.Grant(new_owner);
        RegisterSignal(new_owner, COMSIG_SILICON_AI_VACATE_APC, nameof(returned_to_core));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:704
    public virtual void returned_to_core(datum source = null)
    {
        Waiting.NoSleep(Remove(source as mob));
        UnregisterSignal(source, COMSIG_SILICON_AI_VACATE_APC);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:710
    public override async DmTask<object> Activate()
    {
        if (owner.loc is not obj_machinery_power_apc apc)
        {
            to_chat(owner, span_notice(I18n.T("You are already in your Main Core.")));
            return null;
        }
        if ((owner as mob_living_silicon_ai)?.linked_core != null)
            await apc.malfvacate();
        else
        {
            to_chat(owner, span_danger(I18n.T("Linked core not detected!")));
            return null;
        }
        qdel(this);
        return null;
    }
}

// tg: code/modules/mob/living/silicon/ai/ai.dm:876
public partial class datum_action_innate_choose_modules : datum_action_innate
{
    // tg: code/modules/mob/living/silicon/ai/ai.dm:883
    [DmParams("picker")]
    public override async DmTask New(params object[] __extra)
    {
        object picker = Dm.arg(__extra, 0);
        await base.New(__extra);
        if (picker is datum_module_picker module_picker)
            this.module_picker = module_picker;
        else
            throw Dm.CRASH("choose_modules action created with non module picker", "code/modules/mob/living/silicon/ai/ai.dm", 888);
    }

    // tg: code/modules/mob/living/silicon/ai/ai.dm:890
    public override async DmTask<object> Activate()
    {
        await module_picker.ui_interact(owner);
        return null;
    }
}

// tg: code/modules/mob/living/silicon/ai/ai.dm:1021
public partial class datum_action_innate_deploy_shell : datum_action_innate
{
    // tg: code/modules/mob/living/silicon/ai/ai.dm:1027
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        if (!Dm.truthy(await base.Trigger(clicker, trigger_flags)))
            return null;
        if (owner is not mob_living_silicon_ai AI)
            return null;
        await AI.select_shell();
        return null;
    }
}

// tg: code/modules/mob/living/silicon/ai/ai.dm:1035
public partial class datum_action_innate_deploy_last_shell : datum_action_innate
{
    // tg: code/modules/mob/living/silicon/ai/ai.dm:1042
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        if (!Dm.truthy(await base.Trigger(clicker, trigger_flags)))
            return null;
        if (owner == null)
            return null;
        if (last_used_shell != null)
        {
            mob_living_silicon_ai AI = owner as mob_living_silicon_ai;
            await AI.select_shell(last_used_shell);
        }
        else
            await Remove(owner); //If the last shell is blown, destroy it.
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/silicon/ai/ai.dm:96  var/static/list/alert_areas
    internal DmList mob_living_silicon_ai__alert_areas;
    // tg: code/modules/mob/living/silicon/ai/ai.dm:635  var/static/list/choices
    internal DmList mob_living_silicon_ai__hologram_choices;
}

public static partial class GlobalProcs
{
    /// Converts an rgb color into a list storing hsva
    /// Exists because it's useful to have a guaranteed alpha value
    // tg: code/__HELPERS/icons.dm:276
    public static DmList rgb2hsv(object rgb = null)
    {
        DmList hsv = Dm.rgb2num(Dm.str(rgb), COLORSPACE_HSV);
        if (Dm.length(hsv) < 4)
            hsv.Add(255); // Max alpha, just to make life easy
        return hsv;
    }

    /// Converts a list storing hsva into an rgb color
    // tg: code/__HELPERS/icons.dm:283
    public static string hsv2rgb(DmList hsv = null)
    {
        if (Dm.length(hsv) < 3)
            return COLOR_BLACK;
        if (Dm.length(hsv) == 3)
            return Dm.rgb(Dm.num(hsv[1]), Dm.num(hsv[2]), Dm.num(hsv[3]), space: COLORSPACE_HSV);
        return Dm.rgb(Dm.num(hsv[1]), Dm.num(hsv[2]), Dm.num(hsv[3]), Dm.num(hsv[4]), space: COLORSPACE_HSV);
    }
}
