// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/firealarm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the fire alarms): /obj/machinery/firealarm's init (the area, the wall mount), Destroy, the area
// relationship (setup_area_power_relationship, on_enter_area, on_exit_area, handle_fire), update_name, set_status, the
// pictures and the light, alarm and reset (pulling the alarm closes the area's firelocks, the right click cracks them
// open), the hand clicks, and the thermal sensor toggles, the EMP (M7); the siren (/datum/looping_sound/firealarm, M4b audio slice 5). Deferred
// leaves: the wires (/datum/wires/firealarm: hacking, M7), the circuit USB port (circuits), the turf's hand
// redirect and the screentips (M4a, M9), the emissives (DEVIATION DV12) and the game log lines (M10). M5 slice 7 (D148): add_context,
// the construction states and tool acts (the wires window a not-translated leaf, M7), the repair, the damage, the break
// and the deconstruction; the wall frame (/obj/item/wallframe/firealarm) and its electronics are live. Generated
// live-partial stubs: the AI and cyborg clicks (M10+; the pseudocircuit since silicons SI2) (E13: the RCD's procs),
// the singularity (M10+). The examine is translated (X1). The party button (/obj/machinery/firealarm/partyalarm) and the circuit component
// stay inert. Gaps R4: the security level's picture and light colour and its signal. The var block is generated.

// tg: code/game/machinery/firealarm.dm:20
public partial class obj_machinery_firealarm : obj_machinery
{
    // tg: code/game/machinery/firealarm.dm:59
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        id_tag = assign_random_name();
        if (!Dm.truthy(mapload))
        {
            buildstage = FIRE_ALARM_BUILD_NO_CIRCUIT;
            set_panel_open(TRUE);
        }
        if (Dm.eq(name, Dm.initial(this, (atom p) => p.name)))
            update_name();
        my_area = get_area(this) as area;
        LAZYADD(ref my_area.firealarms, this);

        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        // the port's test worlds may boot without the subsystem (tg always has it)
        if (SSsecurity_level != null)
            RegisterSignal(SSsecurity_level, COMSIG_SECURITY_LEVEL_CHANGED, nameof(check_security_level));
        soundloop = Dm.@new<datum_looping_sound_firealarm>(this, FALSE);
        // set_wires(new /datum/wires/firealarm(src)): the fire alarm's wires (hacking, M7); can_reset, can_trigger and
        // can_toggle_detection keep their TRUE defaults until then
        set_wires(Dm.@new<datum_wires_firealarm>(this));

        // AddComponent(/datum/component/usb_port, ...): circuits; AddComponent(/datum/component/redirect_attack_hand_from_turf,
        // screentip_texts = list(lmb_text = "Turn on alarm", rmb_text = "Turn off alarm")): clicking the wall below (clicks, M4a);
        // register_context(): screentips (M9)
        Dm.Deferred("code/game/machinery/firealarm.dm:75", "Initialize: the USB port (circuits), redirect_attack_hand_from_turf (clicks, M4a) and register_context (screentips, M10+)");
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/firealarm.dm:90
    public override object Destroy(float? force = null)
    {
        if (my_area != null)
        {
            LAZYREMOVE(ref my_area.firealarms, this);
            my_area = null;
        }
        QDEL_NULL(ref soundloop);
        return base.Destroy(force);
    }

    // Area sensitivity is traditionally tied directly to power use, as an optimization
    // But since we want it for fire reacting, we disregard that
    // tg: code/game/machinery/firealarm.dm:99
    public override object setup_area_power_relationship()
    {
        object dot = base.setup_area_power_relationship();
        if (!Dm.truthy(dot))
            return dot;
        area our_area = get_area(this) as area;
        RegisterSignal(our_area, COMSIG_AREA_FIRE_CHANGED, nameof(handle_fire));
        handle_fire(our_area, our_area.fire);
        return dot;
    }

    // tg: code/game/machinery/firealarm.dm:107
    public override void on_enter_area(datum source = null, area area_to_register = null)
    {
        //were already registered to an area. exit from here first before entering into an new area
        if (my_area != null)
            return;
        base.on_enter_area(source, area_to_register);

        my_area = area_to_register;
        LAZYADD(ref my_area.firealarms, this);

        RegisterSignal(area_to_register, COMSIG_AREA_FIRE_CHANGED, nameof(handle_fire));
        handle_fire(area_to_register, area_to_register.fire);
        update_appearance();
    }

    // tg: code/game/machinery/firealarm.dm:120
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        name = Dm.text("[] [] []", get_area_name(my_area), Dm.initial(this, (atom p) => p.name), id_tag);
        return dot;
    }

    // tg: code/game/machinery/firealarm.dm:124
    public override void on_exit_area(datum source = null, area area_to_unregister = null)
    {
        //we cannot unregister from an area we never registered to in the first place
        if (my_area != area_to_unregister)
            return;
        base.on_exit_area(source, area_to_unregister);

        UnregisterSignal(area_to_unregister, COMSIG_AREA_FIRE_CHANGED);
        LAZYREMOVE(ref my_area.firealarms, this);
        my_area = null;
    }

    // tg: code/game/machinery/firealarm.dm:134
    [SignalHandler]
    public virtual void handle_fire(area source = null, object new_fire = null)
    {
        set_status();
    }

    /**
     * Sets the sound state, and then calls update_icon()
     *
     * This proc exists to be called by areas and firelocks
     * so that it may update its icon and start or stop playing
     * the alarm sound based on the state of an area variable.
     */
    // tg: code/game/machinery/firealarm.dm:145
    public virtual void set_status()
    {
        if (!(Dm.truthy(my_area.fire) || Dm.truthy(LAZYLEN(my_area.active_firelocks))) || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            soundloop.stop();
        }
        update_appearance();
    }

    // tg: code/game/machinery/firealarm.dm:150
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);
        if (!Dm.eq(buildstage, FIRE_ALARM_BUILD_SECURED))
            Waiting.NoSleep(set_light(l_on: FALSE));
        else if ((Dm.truthy(my_area?.fire) || Dm.truthy(LAZYLEN(my_area?.active_firelocks))) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && !Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
            Waiting.NoSleep(set_light(l_on: TRUE, l_range: 2.5f, l_power: 1.5f));
        else
            Waiting.NoSleep(set_light(l_on: TRUE, l_range: 1.6f, l_power: 1));
        return dot;
    }

    // tg: code/game/machinery/firealarm.dm:159
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            icon_state = Dm.text("fire_b[]", buildstage);
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            icon_state = "firex";
            return await base.update_icon_state(__extra);
        }
        icon_state = "fire0";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/firealarm.dm:169
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return dot;

        if (Dm.truthy(panel_open))
            return dot;

        // DEVIATION(DV12): the emissive_appearance overlays of every branch below (the glow on the emissive plane) are not
        // created (lighting planes and emissives are not ported).
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            Dm.append(ref dot, mutable_appearance(icon, "fire_emag"));
            await set_light(l_color: LIGHT_COLOR_BLUE);
        }
        else if (!(Dm.truthy(my_area?.fire) || Dm.truthy(LAZYLEN(my_area?.active_firelocks))))
        {
            if (Dm.truthy(my_area?.fire_detect)) //If this is false, someone disabled it. Leave the light missing, a good hint to anyone paying attention.
            {
                if (Dm.truthy(is_station_level(z)))
                {
                    object current_level = SSsecurity_level?.get_current_level_as_number() ?? SEC_LEVEL_GREEN; // a test world without the subsystem: tg's starting level
                    Dm.append(ref dot, mutable_appearance(icon, Dm.text("fire_[]", current_level)));
                    object level_color = SSsecurity_level?.current_security_level?.fire_alarm_light_color;
                    await set_light(l_color: Dm.truthy(level_color) ? level_color : LIGHT_COLOR_BLUEGREEN);
                }
                else
                {
                    Dm.append(ref dot, mutable_appearance(icon, "fire_offstation"));
                    await set_light(l_color: LIGHT_COLOR_FAINT_BLUE);
                }
            }
            else
            {
                Dm.append(ref dot, mutable_appearance(icon, "fire_disabled"));
                await set_light(l_color: COLOR_WHITE);
            }
        }
        else if (Dm.truthy(my_area?.fire_detect) && Dm.truthy(my_area?.fire))
        {
            Dm.append(ref dot, mutable_appearance(icon, "fire_alerting"));
            await set_light(l_color: LIGHT_COLOR_INTENSE_RED);
        }
        else
        {
            Dm.append(ref dot, mutable_appearance(icon, "fire_alerting"));
            await set_light(l_color: LIGHT_COLOR_INTENSE_RED);
        }
        return dot;
    }

    // tg: code/game/machinery/firealarm.dm:207
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);

        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;

        if (prob(50 / Dm.num(severity)))
            alarm();
        return dot;
    }

    // tg: code/game/machinery/firealarm.dm:217
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        update_appearance();
        visible_message(span_warning(I18n.T("Sparks fly out of []!", this)));
        if (user != null)
        {
            balloon_alert(user, "circuitry fried");
            user.log_message(Dm.text("emagged [].", this), LOG_ATTACK);
        }
        playsound(this, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        set_status();
        return TRUE;
    }

    /**
     * Signal handler for checking if we should update fire alarm appearance accordingly to a newly set security level
     *
     * Arguments:
     * * source The datum source of the signal
     * * new_level The new security level that is in effect
     */
    // tg: code/game/machinery/firealarm.dm:237
    [SignalHandler]
    public virtual void check_security_level(datum source = null, object new_level = null)
    {
        if (Dm.truthy(is_station_level(z)))
            update_appearance();
    }

    /**
     * Sounds the fire alarm and closes all firelocks in the area. Also tells the area to color the lights red.
     *
     * Arguments:
     * * mob/user is the user that pulled the alarm.
     */
    // tg: code/game/machinery/firealarm.dm:249
    public virtual void alarm(mob user = null, float? silent = null)
    {
        silent ??= FALSE;
        if (!Dm.truthy(is_operational) || !Dm.truthy(can_trigger) || Dm.truthy(my_area?.fire))
            return;
        my_area.alarm_manager.send_alarm(ALARM_FIRE, my_area);
        // This'll setup our visual effects, so we only need to worry about the alarm
        foreach (obj_machinery_door_firedoor firelock in Dm.each<obj_machinery_door_firedoor>(my_area.firedoors))
            firelock.activate(FIRELOCK_ALARM_TYPE_GENERIC);
        if (user != null)
        {
            if (!Dm.truthy(silent))
                balloon_alert(user, "triggered alarm!");
            user.log_message("triggered a fire alarm.", LOG_GAME);
        }
        my_area.fault_status = AREA_FAULT_MANUAL;
        my_area.fault_location = name;
        soundloop.start(); //Manually pulled fire alarms will make the sound, rather than the doors.
        if (Dm.truthy(_listen_lookup?[COMSIG_FIREALARM_ON_TRIGGER])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:7)
            _SendSignal(COMSIG_FIREALARM_ON_TRIGGER, Dm.list(this));
        update_use_power(ACTIVE_POWER_USE);
    }

    /**
     * Resets all firelocks in the area. Also tells the area to disable alarm lighting, if it was enabled.
     *
     * Arguments:
     * * mob/user is the user that reset the alarm.
     */
    // tg: code/game/machinery/firealarm.dm:272
    public virtual void reset(mob user = null, float? silent = null)
    {
        silent ??= FALSE;
        if (!Dm.truthy(is_operational) || !Dm.truthy(can_reset))
            return;
        my_area.alarm_manager.clear_alarm(ALARM_FIRE, my_area);
        // Clears all fire doors and their effects for now
        // They'll reclose if there's a problem
        foreach (obj_machinery_door_firedoor firelock in Dm.each<obj_machinery_door_firedoor>(my_area.firedoors))
            firelock.crack_open();
        if (user != null)
        {
            if (!Dm.truthy(silent))
                balloon_alert(user, "reset alarm");
            user.log_message("reset a fire alarm.", LOG_GAME);
        }
        soundloop.stop();
        if (Dm.truthy(_listen_lookup?[COMSIG_FIREALARM_ON_RESET])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:7)
            _SendSignal(COMSIG_FIREALARM_ON_RESET, Dm.list(this));
        update_use_power(IDLE_POWER_USE);
    }

    // tg: code/game/machinery/firealarm.dm:288
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot) || !Dm.eq(buildstage, FIRE_ALARM_BUILD_SECURED))
            return dot;
        alarm(user);
        return TRUE;
    }

    // tg: code/game/machinery/firealarm.dm:295
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN) || !Dm.eq(buildstage, FIRE_ALARM_BUILD_SECURED))
            return dot;
        // tg: code/game/machinery/firealarm.dm:299
        add_fingerprint(user);
        reset(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/firealarm.dm:559
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(my_area?.fire) || Dm.truthy(LAZYLEN(my_area?.active_firelocks)))
        {
            Dm.append(ref dot, I18n.T("The local area hazard light is flashing."));
            Dm.append(ref dot, I18n.T("The fault location display is [] ([]).", my_area.fault_location, my_area.fault_status == AREA_FAULT_AUTOMATIC ? I18n.T("Automatic Detection") : I18n.T("Manual Trigger")));
            if (Dm.truthy(is_station_level(z)))
                Dm.append(ref dot, I18n.T("The station security alert level is [].", SSsecurity_level.get_current_level_as_text()));
            Dm.append(ref dot, I18n.T("<b>Left-Click</b> to activate all firelocks in this area."));
            Dm.append(ref dot, I18n.T("<b>Right-Click</b> to reset firelocks in this area."));
        }
        else
        {
            if (Dm.truthy(is_station_level(z)))
                Dm.append(ref dot, I18n.T("The station security alert level is [].", SSsecurity_level.get_current_level_as_text()));
            Dm.append(ref dot, I18n.T("The local area thermal detection light is [].", Dm.truthy(my_area.fire_detect) ? I18n.T("lit") : I18n.T("unlit")));
            Dm.append(ref dot, I18n.T("<b>Left-Click</b> to activate all firelocks in this area."));
        }
        return dot;
    }

    /// Toggles automatic fire detection on or off
    // tg: code/game/machinery/firealarm.dm:588
    public virtual void toggle_fire_detect(mob user = null, float? silent = null)
    {
        silent ??= FALSE;
        if (!Dm.truthy(can_toggle_detection))
        {
            if (user != null && !Dm.truthy(silent))
                balloon_alert(user, "thermal sensors unresponsive!");
            return;
        }
        if (Dm.truthy(my_area.fire_detect))
            disable_fire_detect(user);
        else
            enable_fire_detect(user);
        if (user != null && !Dm.truthy(silent))
            balloon_alert(user, Dm.text("thermal sensors []", Dm.truthy(my_area.fire_detect) ? "enabled" : "disabled"));
    }

    /// Stops the area from automatically activating firelocks
    // tg: code/game/machinery/firealarm.dm:601
    public virtual void disable_fire_detect(mob user = null)
    {
        if (!Dm.truthy(my_area.fire_detect))
            return;
        my_area.fire_detect = FALSE;
        foreach (obj_machinery_firealarm fire_panel in Dm.each<obj_machinery_firealarm>(my_area.firealarms))
            fire_panel.update_appearance();
        // Used to force all the firelocks to update, if the zone is not manually activated
        if (!Dm.eq(my_area.fault_status, AREA_FAULT_MANUAL))
            reset();
        user?.log_message(Dm.text("disabled firelock sensors using [].", this), LOG_GAME);
    }

    /// Enables the area to automatically activate firelocks
    // tg: code/game/machinery/firealarm.dm:613
    public virtual void enable_fire_detect(mob user = null)
    {
        if (Dm.truthy(my_area.fire_detect))
            return;
        my_area.fire_detect = TRUE;
        foreach (obj_machinery_firealarm fire_panel in Dm.each<obj_machinery_firealarm>(my_area.firealarms))
            fire_panel.update_appearance();
        // See above
        if (!Dm.eq(my_area.fault_status, AREA_FAULT_MANUAL))
            reset();
        user?.log_message(Dm.text("enabled firelock sensors using [].", this), LOG_GAME);
    }

    // M5 slice 7 (the wall-mounted frames, D148): the fire alarm's construction states (buildstage: no circuit, no wires,
    // secured) with the screwdriver, the wirecutters, the crowbar, the wrench, the cable and the electronics, the welder's
    // repair, its damage and break, and its deconstruction.
    // tg: code/game/machinery/firealarm.dm:315
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = null;
        if (issilicon(user))
        {
            context[SCREENTIP_CONTEXT_CTRL_LMB] = "Toggle automatic fire detection";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Turn on";
            context[SCREENTIP_CONTEXT_RMB] = "Turn off";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        object sw__1 = held_item.tool_behaviour;
        if (Dm.eq(sw__1, TOOL_SCREWDRIVER))
        {
            if (Dm.eq(buildstage, FIRE_ALARM_BUILD_SECURED))
            {
                context[SCREENTIP_CONTEXT_RMB] = Dm.text("[] wires", Dm.truthy(panel_open) ? "Unexpose" : "Expose");
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
        }
        else if (Dm.eq(sw__1, TOOL_WELDER))
        {
            if (Dm.truthy(panel_open))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Repair";
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
        }
        else if (Dm.eq(sw__1, TOOL_WIRECUTTER))
        {
            if (Dm.truthy(panel_open) && Dm.eq(buildstage, FIRE_ALARM_BUILD_SECURED))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Examine wires";
                context[SCREENTIP_CONTEXT_RMB] = "Remove wires";
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
        }
        else if (Dm.eq(sw__1, TOOL_MULTITOOL))
        {
            if (Dm.truthy(panel_open) && Dm.eq(buildstage, FIRE_ALARM_BUILD_SECURED))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Examine wires";
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
        }
        else if (Dm.eq(sw__1, TOOL_CROWBAR))
        {
            if (Dm.truthy(panel_open) && Dm.eq(buildstage, FIRE_ALARM_BUILD_NO_WIRES))
            {
                context[SCREENTIP_CONTEXT_RMB] = "Remove circuit";
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
        }
        else if (Dm.eq(sw__1, TOOL_WRENCH))
        {
            if (Dm.truthy(panel_open) && Dm.eq(buildstage, FIRE_ALARM_BUILD_NO_CIRCUIT))
            {
                context[SCREENTIP_CONTEXT_RMB] = "Remove fire alarm";
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
        }

        if (held_item is obj_item_stack_cable_coil && Dm.truthy(panel_open) && Dm.eq(buildstage, FIRE_ALARM_BUILD_NO_WIRES))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Install wires";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        if ((held_item is obj_item_electronics_firealarm || held_item is obj_item_electroadaptive_pseudocircuit) && Dm.truthy(panel_open) && Dm.eq(buildstage, FIRE_ALARM_BUILD_NO_CIRCUIT))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Install circuit";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        return dot;
    }

    // tg: code/game/machinery/firealarm.dm:362
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(buildstage, FIRE_ALARM_BUILD_SECURED))
            return NONE;
        toggle_panel_open();
        tool.play_tool_sound(this);
        balloon_alert_to_viewers(I18n.T("wires []", Dm.truthy(panel_open) ? "exposed" : "unexposed"));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/firealarm.dm:371
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await screwdriver_act(user, tool);
    }

    // tg: code/game/machinery/firealarm.dm:374
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
            return NONE;
        if (Dm.num(atom_integrity) >= max_integrity)
        {
            balloon_alert(user, I18n.T("already in good condition!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert_to_viewers(I18n.T("repairing..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, amount: 1, volume: 50, extra_checks: Dm.@new<datum_callback>(this, nameof(state_callback), null, TRUE))))
            return ITEM_INTERACT_BLOCKING;
        await repair_damage(INFINITY);
        balloon_alert_to_viewers(I18n.T("repaired"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/firealarm.dm:389
    public override async DmTask<object> wirecutter_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
            return NONE;
        if (!Dm.eq(buildstage, FIRE_ALARM_BUILD_SECURED))
        {
            balloon_alert(user, I18n.T("no wires to cut!"));
            return ITEM_INTERACT_BLOCKING;
        }

        tool.play_tool_sound(this);
        Dm.@new<obj_item_stack_cable_coil>(user.loc, 5);
        balloon_alert_to_viewers(I18n.T("wires removed"));
        buildstage = FIRE_ALARM_BUILD_NO_WIRES;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/firealarm.dm:403
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
            return NONE;
        if (!Dm.eq(buildstage, FIRE_ALARM_BUILD_NO_WIRES))
            return NONE;

        loc.balloon_alert_to_viewers(I18n.T("removing circuit..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50, extra_checks: Dm.@new<datum_callback>(this, nameof(state_callback), FIRE_ALARM_BUILD_NO_WIRES, TRUE))))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            balloon_alert_to_viewers(I18n.T("broken circuit removed"));
            await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
        }
        else
        {
            balloon_alert_to_viewers(I18n.T("circuit removed"));
            Dm.@new<obj_item_electronics_firealarm>(user.drop_location());
        }
        buildstage = FIRE_ALARM_BUILD_NO_CIRCUIT;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/firealarm.dm:422
    public override async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await crowbar_act(user, tool);
    }

    // tg: code/game/machinery/firealarm.dm:425
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
            return NONE;
        if (!Dm.eq(buildstage, FIRE_ALARM_BUILD_NO_CIRCUIT))
        {
            balloon_alert(user, I18n.T("remove [] first!", Dm.eq(buildstage, FIRE_ALARM_BUILD_SECURED) ? "wires" : "circuit"));
            return ITEM_INTERACT_BLOCKING;
        }

        loc.balloon_alert_to_viewers(I18n.T("[] removed", Dm.initial(typeof(obj_item_wallframe_firealarm), (obj_item_wallframe_firealarm f) => f.name)));
        Dm.@new<obj_item_wallframe_firealarm>(user.drop_location());
        tool.play_tool_sound(loc);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/firealarm.dm:438
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await wrench_act(user, tool);
    }

    // tg: code/game/machinery/firealarm.dm:441
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object dot = await base.tool_act(user, tool, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        // is_wire_tool(tool) (datums/wires/_wires.dm:13, check_secured = FALSE)
        if (!(Dm.eq(tool.tool_behaviour, TOOL_WIRECUTTER) || Dm.eq(tool.tool_behaviour, TOOL_MULTITOOL) || tool is obj_item_assembly))
            return NONE;
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("expose wires first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        await wires.interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/firealarm.dm:453
    public virtual async DmTask<object> cable_act(mob_living user = null, obj_item_stack_cable_coil coil = null)
    {
        if (!Dm.eq(buildstage, FIRE_ALARM_BUILD_NO_WIRES))
            return NONE;
        if (!Dm.truthy(await coil.use(5)))
        {
            balloon_alert(user, I18n.T("need 5 cables!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert_to_viewers(I18n.T("wires installed"));
        buildstage = FIRE_ALARM_BUILD_SECURED;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/firealarm.dm:465
    public virtual async DmTask<object> electronics_act(mob_living user = null, obj_item_electronics_firealarm circuit = null)
    {
        if (!Dm.eq(buildstage, FIRE_ALARM_BUILD_NO_CIRCUIT))
            return NONE;
        if (!Dm.truthy(await user.transferItemToLoc(circuit, this)))
        {
            balloon_alert(user, I18n.T("can't install!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert_to_viewers(I18n.T("circuit installed"));
        qdel(circuit);
        buildstage = FIRE_ALARM_BUILD_NO_WIRES;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/firealarm.dm:478  (silicons SI2: the cyborg's electroadaptive pseudocircuit)
    public virtual async DmTask<object> pseudocircuit_act(mob_living user = null, obj_item_electroadaptive_pseudocircuit pseudocircuit = null)
    {
        if (!Dm.eq(buildstage, FIRE_ALARM_BUILD_NO_CIRCUIT))
            return NONE;
        if (!Dm.truthy(await pseudocircuit.adapt_circuit(user as mob_living_silicon_robot, circuit_cost: 0.015f * STANDARD_CELL_CHARGE)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert_to_viewers(I18n.T("circuit installed"));
        buildstage = FIRE_ALARM_BUILD_NO_WIRES;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/firealarm.dm:489
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(panel_open))
            return NONE;

        if (tool is obj_item_stack_cable_coil)
            return await cable_act(user, (obj_item_stack_cable_coil)tool);

        if (tool is obj_item_electronics_firealarm)
            return await electronics_act(user, (obj_item_electronics_firealarm)tool);

        if (tool is obj_item_electroadaptive_pseudocircuit)
            return await pseudocircuit_act(user, (obj_item_electroadaptive_pseudocircuit)tool);

        return NONE;
    }

    // tg: code/game/machinery/firealarm.dm:504
    public virtual object state_callback(object desired_build_state = null, object desired_panel_state = null)
    {
        return Dm.num((desired_build_state == null || Dm.eq(buildstage, desired_build_state))
            && (desired_panel_state == null || Dm.eq(panel_open, desired_panel_state)));
    }

    // Taking melee damage always triggers the alarm if panel is open
    // tg: code/game/machinery/firealarm.dm:523
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attacked_by(attacking_item, user, modifiers, attack_modifiers);
        if (Dm.num(dot) <= 0 || !Dm.truthy(panel_open) || !Dm.eq(buildstage, FIRE_ALARM_BUILD_SECURED))
            return dot;
        alarm();
        return dot;
    }

    // Taking any damage has a rng chance of triggering the alarm regardless of panel state
    // tg: code/game/machinery/firealarm.dm:530
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        sound_effect ??= 1;
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (!Dm.truthy(dot)) // no damage received
            return dot;
        if (Dm.num(atom_integrity) <= 0 || !Dm.eq(buildstage, FIRE_ALARM_BUILD_SECURED))
            return dot;
        if (prob(Dm.num(damage_amount) * 3))
            alarm();
        return dot;
    }

    // tg: code/game/machinery/firealarm.dm:544
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        if (Dm.eq(buildstage, FIRE_ALARM_BUILD_NO_CIRCUIT)) //can't break the electronics if there isn't any inside.
            return null;
        return await base.atom_break(damage_flag);
    }

    // tg: code/game/machinery/firealarm.dm:549
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(loc);
        if (Dm.num(buildstage) > FIRE_ALARM_BUILD_NO_CIRCUIT)
        {
            obj_item item = Dm.@new<obj_item_electronics_firealarm>(loc);
            if (!Dm.truthy(disassembled))
                item.update_integrity(item.max_integrity * 0.5f);
        }
        if (Dm.num(buildstage) > FIRE_ALARM_BUILD_NO_WIRES)
            Dm.@new<obj_item_stack_cable_coil>(loc, 3);
        return null;
    }
}

// tg: code/game/machinery/firealarm.dm:5
public partial class obj_item_wallframe_firealarm : obj_item_wallframe
{
    // tg: code/game/machinery/firealarm.dm:13
    public override async DmTask<object> try_build(atom support = null, mob user = null)
    {
        area A = get_area(user) as area;
        if (Dm.truthy(A.always_unpowered))
        {
            balloon_alert(user, I18n.T("cannot place in this area!"));
            return FALSE;
        }
        return await base.try_build(support, user);
    }
}

// tg: code/game/machinery/firealarm.dm:539 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_machinery_firealarm : obj_machinery
{
    // tg: code/game/machinery/firealarm.dm:539
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        if (Dm.num(current_size) >= STAGE_FIVE) // If the singulo is strong enough to pull anchored objects, the fire alarm experiences integrity failure
            await deconstruct();
        return await base.singularity_pull(singularity, current_size);
    }
}

// tg: code/game/machinery/firealarm.dm:508 (E13: the RCD's simple circuits upgrade)
public partial class obj_machinery_firealarm : obj_machinery
{
    // tg: code/game/machinery/firealarm.dm:508
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if ((buildstage == FIRE_ALARM_BUILD_NO_CIRCUIT) && Dm.truthy(Dm.bitand(the_rcd.construction_upgrades, RCD_UPGRADE_SIMPLE_CIRCUITS)))
            return Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 1));
        return FALSE;
    }

    // tg: code/game/machinery/firealarm.dm:513
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        object sw__1 = rcd_data[RCD_DESIGN_MODE];
        if (Dm.eq(sw__1, RCD_WALLFRAME))
        {
            balloon_alert_to_viewers(I18n.T("circuit installed"));
            buildstage = FIRE_ALARM_BUILD_NO_WIRES;
            update_appearance();
            return TRUE;
        }
        return FALSE;
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/machinery/firealarm.dm:303
public partial class obj_machinery_firealarm : obj_machinery
{
    // tg: code/game/machinery/firealarm.dm:303
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return await attack_hand(user);
    }

    // tg: code/game/machinery/firealarm.dm:306
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user);
    }

    // tg: code/game/machinery/firealarm.dm:309
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        return await attack_hand(user);
    }

    // tg: code/game/machinery/firealarm.dm:312
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user);
    }

    // tg: code/game/machinery/firealarm.dm:575
    public override async DmTask BorgCtrlClick(mob_living_silicon_robot user = null)
    {
        if (get_dist(this, user) <= Dm.num(user.interaction_range) && !Dm.truthy(user.control_disabled))
        {
            await AICtrlClick(user);
            return;
        }
        await base.BorgCtrlClick(user);
    }

    // tg: code/game/machinery/firealarm.dm:581
    public override async DmTask AICtrlClick(mob_living_silicon user = null)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(user, I18n.T("control circuitry malfunctioning!"));
            return;
        }
        toggle_fire_detect(user);
    }
}
