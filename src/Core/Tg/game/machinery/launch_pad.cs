// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/launch_pad.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the bluespace launchpad, the briefcase launchpad and its remote (the LaunchpadRemote window):
// set an offset within range, launch or pull what stands on the pad or the target, the beam and the sparks. The diagnostic HUD
// with the target indicator (prepare_huds, the diagnostic hud and the hud image) is a deferred leaf, as for the airlocks; the
// pad's USB port takes its circuit component; the logs are rows.

// tg: code/game/machinery/launch_pad.dm:3
public partial class obj_machinery_launchpad : obj_machinery
{
    // tg: code/game/machinery/launch_pad.dm:1
    private const float BEAM_FADE_TIME = 1 * SECONDS;

    // tg: code/game/machinery/launch_pad.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // prepare_huds() and GLOB.huds[DATA_HUD_DIAGNOSTIC].add_atom_to_hud(src) (:39-41)
        Dm.Deferred("code/game/machinery/launch_pad.dm:39", "Initialize: prepare_huds() and the diagnostic HUD (the target indicator, M10+ with the diagnostic HUD)");

        Waiting.NoSleep(update_hud());
        return dot;
    }

    // tg: code/game/machinery/launch_pad.dm:45
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float max_range_multiplier = 0;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            max_range_multiplier += Dm.num(servo.tier);
        range = Dm.num_or_null(Dm.initial(this, nameof(range)));
        range *= max_range_multiplier;
        return dot;
    }

    // tg: code/game/machinery/launch_pad.dm:53
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        if (Dm.truthy(same_z_layer) && !QDELETED(this))
            await update_hud();
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
    }

    // tg: code/game/machinery/launch_pad.dm:58
    public override object Destroy(float? force = null)
    {
        // GLOB.huds[DATA_HUD_DIAGNOSTIC].remove_atom_from_hud(src) (:59-60)
        Dm.Deferred("code/game/machinery/launch_pad.dm:59", "Destroy: the diagnostic HUD (M10+ with the diagnostic HUD)");
        return base.Destroy(force);
    }

    // tg: code/game/machinery/launch_pad.dm:63
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (in_range(user, this) || isobserver(user))
            dot.Add(span_notice(I18n.T("The status display reads: Maximum range: <b>[]</b> units.", range)));
        return dot;
    }

    // tg: code/game/machinery/launch_pad.dm:68
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multi = tool as obj_item_multitool;
        object dot = NONE;
        if (!Dm.truthy(panel_open))
            return dot;

        multi.set_buffer(this);
        balloon_alert(user, I18n.T("saved to buffer"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/launch_pad.dm:77
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return Dm.truthy(stationary) ? default_deconstruction_screwdriver(user, tool) : NONE;
    }

    // tg: code/game/machinery/launch_pad.dm:80
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/launch_pad.dm:83
    public override object can_crowbar_deconstruct()
    {
        return Dm.truthy(base.can_crowbar_deconstruct()) && Dm.truthy(stationary);
    }

    // tg: code/game/machinery/launch_pad.dm:86
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        update_indicator();
    }

    // tg: code/game/machinery/launch_pad.dm:89
    public override void on_set_panel_open(object old_value = null)
    {
        update_indicator();
    }

    // tg: code/game/machinery/launch_pad.dm:92
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
            icon_state = Dm.text("[]-off", base_icon_state);
        else if (Dm.truthy(state_open))
            icon_state = Dm.text("[]-open", base_icon_state);
        else
            icon_state = Dm.text("[]-idle", base_icon_state);
        return dot;
    }

    // tg: code/game/machinery/launch_pad.dm:101
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        mob_dead_observer ghost = user;
        object dot = await base.attack_ghost(ghost);
        if (Dm.truthy(dot))
            return dot;
        float target_x = x + x_offset;
        float target_y = y + y_offset;
        turf target = locate(target_x, target_y, z);
        await ghost.forceMove(target);
        return dot;
    }

    /// Updates diagnostic huds
    // tg: code/game/machinery/launch_pad.dm:111
    public virtual async DmTask update_hud()
    {
        // var/image/holder = hud_list[DIAG_LAUNCHPAD_HUD]; holder.appearance = mutable_appearance('icons/effects/effects.dmi',
        // "launchpad_target", ...) (:112-114): the diagnostic HUD's target image
        update_indicator();

        if (Dm.truthy(stationary))
            await AddComponent(typeof(datum_component_usb_port), typecacheof(Dm.list(typeof(obj_item_circuit_component_bluespace_launchpad)), only_root_path: TRUE));
    }

    /// Whether this launchpad can send or receive.
    // tg: code/game/machinery/launch_pad.dm:122
    public virtual object is_available()
    {
        if (QDELETED(this) || !Dm.truthy(is_operational) || Dm.truthy(panel_open))
            return FALSE;
        return TRUE;
    }

    /// Updates the indicator icon.
    // tg: code/game/machinery/launch_pad.dm:128
    public virtual void update_indicator()
    {
        image holder = hud_list?[DIAG_LAUNCHPAD_HUD] as image;
        if (holder == null)
            return; // the diagnostic HUD is a deferred leaf (Initialize), so the pad has no hud image to move
        turf target_turf = null;
        if (Dm.truthy(is_available()))
            target_turf = locate(x + x_offset, y + y_offset, z);
        if (target_turf != null)
        {
            holder.icon_state = indicator_icon;
            holder.loc = target_turf;
        }
        else
        {
            holder.icon_state = null;
        }
    }

    /// Sets the offset of the launchpad.
    // tg: code/game/machinery/launch_pad.dm:140
    public virtual void set_offset(object x = null, object y = null)
    {
        if (Dm.truthy(teleporting))
            return;
        if (x != null && y != null)
        {
            x_offset = clamp(Dm.num(x), -Dm.num(range), Dm.num(range));
            y_offset = clamp(Dm.num(y), -Dm.num(range), Dm.num(range));
            log_message(Dm.text("changed the launchpad's x and y-offset parameters to X: [] Y: [].", x, y), LOG_GAME, log_globally: FALSE);
        }
        else if (x != null)
        {
            x_offset = clamp(Dm.num(x), -Dm.num(range), Dm.num(range));
            log_message(Dm.text("changed the launchpad's x-offset parameter to X: [].", x), LOG_GAME, log_globally: FALSE);
        }
        else if (y != null)
        {
            y_offset = clamp(Dm.num(y), -Dm.num(range), Dm.num(range));
            log_message(Dm.text("changed the launchpad's y-offset parameter to Y: [].", y), LOG_GAME, log_globally: FALSE);
        }
        update_indicator();
    }

    /// Checks if the launchpad can teleport.
    // tg: code/game/machinery/launch_pad.dm:160
    public virtual object teleport_checks()
    {
        if (!Dm.truthy(is_available()))
            return I18n.T("ERROR: Launchpad not operative. Make sure the launchpad is ready and powered.");

        if (Dm.truthy(teleporting))
            return I18n.T("ERROR: Launchpad busy.");

        area surrounding = get_area(this) as area;
        if (Dm.truthy(is_centcom_level(z)) || istype(surrounding, typeof(area_shuttle_supply)) || istype(surrounding, typeof(area_shuttle_transport)))
            return I18n.T("ERROR: Launchpad not operative. Heavy area shielding makes teleporting impossible.");

        return null;
    }

    /// Performs the teleport.
    /// sending - TRUE/FALSE depending on if the launch pad is teleporting *to* or *from* the target.
    /// alternate_log_name - An alternative name to use in logs, if `user` is not present..
    // tg: code/game/machinery/launch_pad.dm:176
    public virtual async DmTask doteleport(mob user = null, object sending = null, object alternate_log_name = null)
    {
        turf dest = get_turf(this);

        float target_x = x + x_offset;
        float target_y = y + y_offset;
        turf target = locate(target_x, target_y, z);
        area A = get_area(target) as area;

        flick(Dm.text("[]-beam", base_icon_state), this);

        //Change the indicator's icon to show that we're teleporting
        if (Dm.truthy(sending))
            indicator_icon = "launchpad_launch";
        else
            indicator_icon = "launchpad_pull";
        update_indicator();

        playsound(get_turf(this), Dm.resource("sound/items/weapons/flash.ogg"), 25, TRUE);
        teleporting = TRUE;

        if (!Dm.truthy(hidden))
        {
            playsound(target, Dm.resource("sound/items/weapons/flash.ogg"), 25, TRUE);
            do_sparks(5, TRUE, target, spark_type: typeof(datum_effect_system_basic_spark_spread_quantum));
        }

        await sleep(teleport_speed);

        //Set the indicator icon back to normal
        indicator_icon = "launchpad_target";
        update_indicator();

        if (!Dm.truthy(is_available()))
            return;

        teleporting = FALSE;
        if (!Dm.truthy(hidden))
        {
            // Takes twice as long to make sure it properly fades out.
            Beam(target, icon_state: teleport_beam, time: BEAM_FADE_TIME * 2, beam_type: typeof(obj_effect_ebeam_launchpad));
            playsound(target, Dm.resource("sound/items/weapons/emitter2.ogg"), 25, TRUE);
        }

        // use a lot of power
        await use_energy(active_power_usage);

        turf source = target;
        DmList log_msg = Dm.list();
        log_msg.Add(Dm.text("[] triggered a teleport ", Dm.truthy(alternate_log_name) ? alternate_log_name : key_name(user)));

        if (Dm.truthy(sending))
        {
            source = dest;
            dest = target;
        }

        playsound(get_turf(this), Dm.resource("sound/items/weapons/emitter2.ogg"), 25, TRUE);
        bool first = true;
        foreach (atom_movable ROI in Dm.each<atom_movable>(source))
        {
            if (ROI == this)
                continue;
            if (!istype(ROI, typeof(atom_movable)) || isdead(ROI) || iseyemob(ROI) || istype(ROI, typeof(obj_effect_dummy_phased_mob)))
                continue;//don't teleport these
            string on_chair = "";
            if (Dm.truthy(ROI.anchored))// if it's anchored, don't teleport
            {
                if (isliving(ROI))
                {
                    mob_living L = (mob_living)ROI;
                    if (L.buckled != null)
                    {
                        // TP people on office chairs
                        if (Dm.truthy(L.buckled.anchored))
                            continue;
                        on_chair = " (on a chair)";
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }
            }
            if (!first)
                log_msg.Add(", ");
            if (ismob(ROI))
            {
                mob T = (mob)ROI;
                log_msg.Add(Dm.text("[][]", key_name(T), on_chair));
            }
            else
            {
                log_msg.Add(Dm.text("[]", ROI.name));
                if (istype(ROI, typeof(obj_structure_closet)))
                {
                    log_msg.Add(" (");
                    bool first_inner = true;
                    foreach (atom_movable Q in Dm.each<atom_movable>(ROI))
                    {
                        if (!ismob(Q) && !isobj(Q))
                            continue; // as mob|obj
                        if (!first_inner)
                            log_msg.Add(", ");
                        first_inner = false;
                        if (ismob(Q))
                            log_msg.Add(Dm.text("[]", key_name(Q)));
                        else
                            log_msg.Add(Dm.text("[]", Q.name));
                    }
                    if (first_inner)
                        log_msg.Add("empty");
                    log_msg.Add(")");
                }
            }
            await do_teleport(ROI, dest, no_effects: !first ? TRUE : FALSE, channel: TELEPORT_CHANNEL_BLUESPACE);
            first = false;
        }

        if (first)
            log_msg.Add("nothing");
        log_msg.Add(Dm.text(" [] [], [], [] ([])", Dm.truthy(sending) ? "to" : "from", target_x, target_y, z, A != null ? A.name : "null area"));
        log_game(log_msg.Join(""));
    }
}

// tg: code/game/machinery/launch_pad.dm:155
public partial class obj_effect_ebeam_launchpad : obj_effect_ebeam
{
    // tg: code/game/machinery/launch_pad.dm:155
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        animate(this, Dm.named("alpha", 0), Dm.named("flags", ANIMATION_PARALLEL), Dm.named("time", 1 * SECONDS)); // BEAM_FADE_TIME
        return dot;
    }
}

//Starts in the briefcase. Don't spawn this directly, or it will runtime when closing.
// tg: code/game/machinery/launch_pad.dm:278
public partial class obj_machinery_launchpad_briefcase : obj_machinery_launchpad
{
    // tg: code/game/machinery/launch_pad.dm:292
    [DmParams("mapload", "_briefcase")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object _briefcase = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(_briefcase))
        {
            stack_trace(Dm.text("[] spawned without a briefcase.", this), "code/game/machinery/launch_pad.dm", 295);
            return INITIALIZE_HINT_QDEL;
        }
        briefcase = _briefcase as obj_item_storage_briefcase_launchpad;
        return dot;
    }

    // tg: code/game/machinery/launch_pad.dm:299
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(briefcase))
            qdel(briefcase);
        briefcase = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/launch_pad.dm:305
    public override object is_available()
    {
        if (Dm.truthy(closed))
            return FALSE;
        if (Dm.truthy(panel_open))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/launch_pad.dm:312
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        atom over_object = over;
        if (over_object == user)
        {
            if (briefcase == null)
                return null;
            user.visible_message(span_notice(I18n.T("[] starts closing []...", usr, this)), span_notice(I18n.T("You start closing []...", this)));
            if (Dm.truthy(await do_after(user, 3 * SECONDS, target: user)))
            {
                await user.put_in_hands(briefcase);
                await moveToNullspace(); //hides it from suitcase contents
                closed = TRUE;
                update_indicator();
            }
        }
        return null;
    }

    // tg: code/game/machinery/launch_pad.dm:323
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_launchpad_remote)))
            return NONE;
        obj_item_launchpad_remote remote = (obj_item_launchpad_remote)tool;
        if (IS_WEAKREF_OF(this, remote.pad)) //do not attempt to link when already linked
            return ITEM_INTERACT_BLOCKING;
        remote.pad = WEAKREF(this) as datum_weakref;
        to_chat(user, span_notice(I18n.T("You link [] to [].", this, remote)));
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/machinery/launch_pad.dm:334
public partial class obj_item_launchpad_remote : obj_item
{
    // tg: code/game/machinery/launch_pad.dm:344
    [DmParams("mapload", "pad")]
    public override object Initialize(object mapload = null, params object[] __extra) //remote spawns linked to the briefcase pad
    {
        object pad = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        this.pad = WEAKREF(pad as datum) as datum_weakref;
        return dot;
    }

    // tg: code/game/machinery/launch_pad.dm:348
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        await ui_interact(user);
        to_chat(user, span_notice(I18n.T("[] projects a display onto your retina.", this)));
        return dot;
    }

    // tg: code/game/machinery/launch_pad.dm:354
    public override object ui_state(mob user = null)
    {
        return GLOB.inventory_state;
    }

    // tg: code/game/machinery/launch_pad.dm:357
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "LaunchpadRemote");
            await ui.open();
        }
        ui.set_autoupdate(TRUE);
        return null;
    }

    // tg: code/game/machinery/launch_pad.dm:364
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        obj_machinery_launchpad_briefcase our_pad = pad?.resolve() as obj_machinery_launchpad_briefcase;
        data["has_pad"] = our_pad != null ? TRUE : FALSE;
        if (our_pad != null)
            data["pad_closed"] = our_pad.closed;
        if (our_pad == null || Dm.truthy(our_pad.closed))
            return data;

        data["pad_name"] = our_pad.display_name;
        data["range"] = our_pad.range;
        data["x"] = our_pad.x_offset;
        data["y"] = our_pad.y_offset;
        return data;
    }

    // tg: code/game/machinery/launch_pad.dm:379
    public virtual async DmTask teleport(mob user = null, obj_machinery_launchpad pad = null)
    {
        if (QDELETED(pad))
        {
            to_chat(user, span_warning(I18n.T("ERROR: Launchpad not responding. Check launchpad integrity.")));
            return;
        }
        object error_reason = pad.teleport_checks();
        if (Dm.truthy(error_reason))
        {
            to_chat(user, span_warning(Dm.text("[]", error_reason)));
            return;
        }
        await pad.doteleport(user, sending);
    }

    // tg: code/game/machinery/launch_pad.dm:389
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        obj_machinery_launchpad_briefcase our_pad = pad?.resolve() as obj_machinery_launchpad_briefcase;
        if (our_pad == null)
        {
            pad = null;
            return TRUE;
        }
        mob user = ui.user;
        object sw__1 = action;
        if (Dm.eq(sw__1, "set_pos"))
        {
            float? new_x = text2num(@params["x"]);
            float? new_y = text2num(@params["y"]);
            // sanitizes our ranges for us
            our_pad.set_offset(new_x, new_y);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "move_pos"))
        {
            float? plus_x = text2num(@params["x"]);
            float? plus_y = text2num(@params["y"]);
            if (Dm.truthy(plus_x) || Dm.truthy(plus_y))
            {
                // sanitizes our ranges for us
                our_pad.set_offset(
                    x: our_pad.x_offset + (plus_x ?? 0),
                    y: our_pad.y_offset + (plus_y ?? 0));
            }
            else
            {
                // for resetting
                our_pad.set_offset(
                    x: 0,
                    y: 0);
            }
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "rename"))
        {
            dot = TRUE;
            object new_name = reject_bad_name(@params["name"], allow_numbers: TRUE, max_length: MAX_NAME_LEN, cap_after_symbols: FALSE);
            if (!Dm.truthy(new_name))
                return dot;
            our_pad.display_name = Dm.text("[]", new_name);
        }
        else if (Dm.eq(sw__1, "remove"))
        {
            dot = TRUE;
            if (Dm.eq(await tgui_alert(user, I18n.T("Are you sure?"), I18n.T("Unlink Launchpad"), Dm.list("I'm Sure", "Abort")), "I'm Sure"))
                pad = null;
        }
        else if (Dm.eq(sw__1, "launch"))
        {
            sending = TRUE;
            await teleport(user, our_pad);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "pull"))
        {
            sending = FALSE;
            await teleport(user, our_pad);
            dot = TRUE;
        }
        return dot;
    }
}

// tg: code/game/machinery/launch_pad.dm:442
public partial class obj_item_circuit_component_bluespace_launchpad : obj_item_circuit_component
{
    // tg: code/game/machinery/launch_pad.dm:458
    public override object get_ui_notices()
    {
        object dot = base.get_ui_notices();

        if (attached_launchpad == null)
            return dot;

        Dm.append(ref dot, create_ui_notice(Dm.text("Minimum Range: []", -Dm.num(attached_launchpad.range)), "orange", "minus"));
        Dm.append(ref dot, create_ui_notice(Dm.text("Maximum Range: []", attached_launchpad.range), "orange", "plus"));
        return dot;
    }

    // tg: code/game/machinery/launch_pad.dm:467
    public override async DmTask populate_ports()
    {
        x_pos = add_input_port("X offset", PORT_TYPE_NUMBER) as datum_port_input;
        y_pos = add_input_port("Y offset", PORT_TYPE_NUMBER) as datum_port_input;
        send_trigger = add_input_port("Send", PORT_TYPE_SIGNAL) as datum_port_input;
        retrieve_trigger = add_input_port("Retrieve", PORT_TYPE_SIGNAL) as datum_port_input;

        sent = add_output_port("Sent", PORT_TYPE_SIGNAL) as datum_port_output;
        retrieved = add_output_port("Retrieved", PORT_TYPE_SIGNAL) as datum_port_output;
        why_fail = add_output_port("Fail reason", PORT_TYPE_STRING) as datum_port_output;
        on_fail = add_output_port("Failed", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/game/machinery/launch_pad.dm:478
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_launchpad)))
            attached_launchpad = shell as obj_machinery_launchpad;
    }

    // tg: code/game/machinery/launch_pad.dm:483
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        attached_launchpad = null;
        return base.unregister_usb_parent(shell);
    }

    // tg: code/game/machinery/launch_pad.dm:487
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (attached_launchpad == null)
        {
            why_fail.set_output("Not connected!");
            on_fail.set_output(COMPONENT_SIGNAL);
            return null;
        }

        if (abs(Dm.num(x_pos.value)) > Dm.num(attached_launchpad.range) || abs(Dm.num(y_pos.value)) > Dm.num(attached_launchpad.range))
        {
            why_fail.set_output("Out of range!");
            on_fail.set_output(COMPONENT_SIGNAL);
            return null;
        }

        attached_launchpad.set_offset(x_pos.value, y_pos.value);

        if (COMPONENT_TRIGGERED_BY(port, x_pos))
        {
            x_pos.set_value(attached_launchpad.x_offset);
            return null;
        }

        if (COMPONENT_TRIGGERED_BY(port, y_pos))
        {
            y_pos.set_value(attached_launchpad.y_offset);
            return null;
        }

        object checks = attached_launchpad.teleport_checks();
        if (checks != null)
        {
            why_fail.set_output(checks);
            on_fail.set_output(COMPONENT_SIGNAL);
            return null;
        }

        if (COMPONENT_TRIGGERED_BY(send_trigger, port))
        {
            Waiting.Detach(attached_launchpad.doteleport(null, TRUE, parent.get_creator()));
            sent.set_output(COMPONENT_SIGNAL);
        }

        if (COMPONENT_TRIGGERED_BY(retrieve_trigger, port))
        {
            Waiting.Detach(attached_launchpad.doteleport(null, FALSE, parent.get_creator()));
            retrieved.set_output(COMPONENT_SIGNAL);
        }
        return null;
    }
}
