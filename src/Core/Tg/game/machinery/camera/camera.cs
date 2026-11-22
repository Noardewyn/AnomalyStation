// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/camera/camera.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with its module (the camera network, M9).
// No procs yet. The var block (c_tag, which the comparator helpers name, and tg's icon, icon_state and layer, so a mapped
// camera reaches the client with its picture) is generated (D111, M3 I1d); the procs come here.

// tg: code/game/machinery/camera/camera.dm:26
[DmPath("/obj/machinery/camera")]
public partial class obj_machinery_camera : obj_machinery
{
    // tg: code/game/machinery/camera/camera.dm:141
    public override object get_turfs_to_mount_on() => Dm.list(get_step(this, Dm.num(dir)));

    // tg: code/game/machinery/camera/camera.dm:200
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (Dm.truthy(isEmpProof(TRUE))) //don't reveal it's upgraded if was done via MALF AI Upgrade Camera Network ability
            Dm.append(ref dot, span_info(I18n.T("It has electromagnetic interference shielding installed.")));
        else
            Dm.append(ref dot, span_info(I18n.T("It can be shielded against electromagnetic interference with some <b>plasma</b>.")));

        if (Dm.truthy(isXRay(TRUE))) //don't reveal it's upgraded if was done via MALF AI Upgrade Camera Network ability
            Dm.append(ref dot, span_info(I18n.T("It has an X-ray photodiode installed.")));
        else
            Dm.append(ref dot, span_info(I18n.T("It can be upgraded with an X-ray photodiode with an <b>analyzer</b>.")));

        if (Dm.truthy(isMotion()))
            Dm.append(ref dot, span_info(I18n.T("It has a proximity sensor installed.")));
        else
            Dm.append(ref dot, span_info(I18n.T("It can be upgraded with a <b>proximity sensor</b>.")));

        if (!Dm.truthy(camera_enabled))
        {
            Dm.append(ref dot, span_info(I18n.T("It's currently deactivated.")));
            if (!Dm.truthy(panel_open) && Dm.truthy(powered__proc()))
                Dm.append(ref dot, span_notice(I18n.T("You'll need to open its maintenance panel with a <b>screwdriver</b> to turn it back on.")));
        }

        if (Dm.truthy(panel_open))
        {
            Dm.append(ref dot, span_info(I18n.T("Its maintenance panel is currently open.")));
            if (!Dm.truthy(camera_enabled) && Dm.truthy(powered__proc()))
                Dm.append(ref dot, span_info(I18n.T("It can reactivated with <b>wirecutters</b>.")));
        }
        return dot;
    }
}

// tg: code/game/machinery/camera/camera.dm:284 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_machinery_camera : obj_machinery
{
    // tg: code/game/machinery/camera/camera.dm:284
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        if (Dm.truthy(camera_enabled) && Dm.num(current_size) >= STAGE_FIVE) // If the singulo is strong enough to pull anchored objects and the camera is still active, turn off the camera as it gets ripped off the wall.
        {
            toggle_cam(null, 0);
        }
        return await base.singularity_pull(singularity, current_size);
    }
}

// M10 slice D (D268): the camera's procs the security camera console needs, with its state's pictures. The AI's camera chunks
// (SScameras' freelook), the alarms' consumers (the AI), EMPs, upgrades, the motion detector and the construction stay with their
// milestones (Dm.Deferred / NotTranslated at each call).
// tg: code/game/machinery/camera/camera.dm
public partial class obj_machinery_camera : obj_machinery
{
    private const float CAMERA_POWER_CONSUMPTION = BASE_MACHINE_ACTIVE_CONSUMPTION * 0.02f; // tg: camera.dm:1
    private const float XRAY_POWER_MOD = 10; // tg: camera.dm:2
    private const float MOTION_POWER_MOD = 4; // tg: camera.dm:3
    private const float EMP_POWER_MOD = 1.25f; // tg: camera.dm:4

    // tg: code/game/machinery/camera/camera.dm:115
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        foreach (object network_name in Dm.each(network.Copy()))
        {
            network.Remove(network_name);
            network.Add(LOWER_TEXT(network_name));
        }

        SScameras.cameras.Add(this);

        myarea = get_room_area() as area;

        if (Dm.truthy(camera_enabled))
        {
            SScameras.add_camera_to_chunk(this);
            LAZYADD(ref myarea.cameras, this);
            if (Dm.truthy(mapload) && !Dm.truthy(start_active) && Dm.truthy(is_station_level(z)) && Dm.prob(3))
                toggle_cam();
            else //this is handled by toggle_camera, so no need to update it twice.
                update_appearance();
        }
        alarm_manager = Dm.@new<datum_alarm_handler>(this);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom(mark_for_late_init: TRUE));
        return dot;
    }

    // The first definition of Destroy (tg defines it again in motion.dm, whose ..() calls this one, rules 2.5).
    // tg: code/game/machinery/camera/camera.dm:144
    public virtual object Destroy__d0(float? force = null)
    {
        if (Dm.truthy(can_use()))
            toggle_cam(null, 0); //kick anyone viewing out and remove from the camera chunks
        SScameras.cameras.Remove(this);
        cancelCameraAlarm();
        if (isarea(myarea))
            LAZYREMOVE(ref myarea.cameras, this);
        QDEL_NULL(ref alarm_manager);
        QDEL_NULL(ref last_shown_paper);
        QDEL_NULL(ref xray_module);
        QDEL_NULL(ref emp_module);
        QDEL_NULL(ref proximity_monitor);
        // moveToNullspace will clear us from our current chunks
        return base.Destroy(force);
    }

    // tg: code/game/machinery/camera/camera.dm:337
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        string xray_module = Dm.truthy(isXRay(TRUE)) ? "xray" : "";
        if (!Dm.truthy(camera_enabled) || Dm.truthy(Dm.bitand(machine_stat, EMPED)))
        {
            icon_state = Dm.text("[][]_off", xray_module, base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[][]", xray_module, base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/camera/camera.dm:345
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, Dm.text("[]_panel", base_icon_state));

        // DEVIATION(DV12): the emissive_appearance overlays (the glow on the emissive plane) are not created.
        string xray_module = Dm.truthy(isXRay(TRUE)) ? "xray" : "";
        if (Dm.truthy(Dm.bitand(machine_stat, EMPED)))
        {
            Dm.append(ref dot, Dm.text("[][]_emp", xray_module, base_icon_state));
            return dot;
        }
        if (Dm.truthy(camera_enabled))
            Dm.append(ref dot, Dm.text("[][]_[]", xray_module, base_icon_state, Dm.truthy(in_use_lights) ? "in_use" : "on"));
        return dot;
    }

    // tg: code/game/machinery/camera/camera.dm:359
    public virtual void toggle_cam(mob user = null, object displaymessage = null)
    {
        displaymessage ??= TRUE;
        camera_enabled = Dm.truthy(camera_enabled) ? FALSE : TRUE;
        if (Dm.truthy(can_use()))
        {
            SScameras.add_camera_to_chunk(this);
            if (isturf(loc))
            {
                myarea = get_area(this) as area;
                LAZYADD(ref myarea.cameras, this);
            }
            else
                myarea = null;
        }
        else
        {
            Waiting.NoSleep(set_light(0));
            SScameras.remove_camera_from_chunk(this);
            if (isarea(myarea))
                LAZYREMOVE(ref myarea.cameras, this);
        }
        // We are not guarenteed that the camera will be on a turf. account for that
        string change_msg = "deactivates";
        if (Dm.truthy(camera_enabled))
        {
            change_msg = "reactivates";
            triggerCameraAlarm();
            if (!QDELETED(this)) //We'll be doing it anyway in destroy
                _addtimer(Dm.@new<datum_callback>(this, nameof(cancelCameraAlarm)), 10 * SECONDS, file: "code/game/machinery/camera/camera.dm", line: 382);
        }
        if (Dm.truthy(displaymessage))
        {
            if (user != null)
            {
                visible_message(span_danger(I18n.T("[] [] []!", user, change_msg, this)));
                add_hiddenprint(user);
            }
            else
                visible_message(span_danger(I18n.T("\\The [] []!", this, change_msg)));

            playsound(this, "sound/items/tools/wirecutter.ogg", 100, TRUE);
        }
        update_appearance(); //update Initialize() if you remove this.
        calculate_active_power();

        // now disconnect anyone using the camera
        //Apparently, this will disconnect anyone even if the camera was re-activated.
        //I guess that doesn't matter since they can't use it anyway?
        foreach (mob O in Dm.each<mob>(GLOB.player_list))
        {
            if (O.client?.eye == this)
            {
                Waiting.NoSleep(O.reset_perspective(null));
                to_chat(O, span_warning(I18n.T("The screen bursts into static!")));
            }
        }
    }

    // tg: code/game/machinery/camera/camera.dm:399
    public virtual void triggerCameraAlarm()
    {
        alarm_on = TRUE;
        alarm_manager.send_alarm(ALARM_CAMERA, this, this);
    }

    // tg: code/game/machinery/camera/camera.dm:403
    public virtual void cancelCameraAlarm()
    {
        alarm_on = FALSE;
        alarm_manager?.clear_alarm(ALARM_CAMERA);
    }

    // tg: code/game/machinery/camera/camera.dm:407
    public virtual object can_use()
    {
        if (!Dm.truthy(camera_enabled))
            return FALSE;
        if (Dm.truthy(Dm.bitand(machine_stat, EMPED)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/camera/camera.dm:277
    public virtual void setViewRange(float num = 7)
    {
        view_range = num;
        SScameras.update_visibility(this);
    }

    // tg: code/game/machinery/camera/camera.dm:474
    ///Called when the camera starts being watched on a camera console.
    public virtual void on_start_watching(datum source = null)
    {
    }

    // tg: code/game/machinery/camera/camera.dm:478
    ///Called when the camera stops being watched on a camera console.
    public virtual void on_stop_watching(datum source = null)
    {
    }

    // tg: code/game/machinery/camera/camera.dm:481
    public virtual void calculate_active_power()
    {
        if (!Dm.truthy(can_use()))
        {
            active_power_usage = 0;
            return;
        }

        float xray_power_mod = Dm.truthy(Dm.bitand(camera_upgrade_bitflags, CAMERA_UPGRADE_XRAY)) && !Dm.truthy(malf_xray_firmware_present) ? XRAY_POWER_MOD : 1;
        float motion_power_mod = Dm.truthy(Dm.bitand(camera_upgrade_bitflags, CAMERA_UPGRADE_MOTION)) ? MOTION_POWER_MOD : 1;
        float EMP_power_mod = Dm.truthy(Dm.bitand(camera_upgrade_bitflags, CAMERA_UPGRADE_EMP_PROOF)) && !Dm.truthy(malf_emp_firmware_present) ? EMP_POWER_MOD : 1;

        active_power_usage = CAMERA_POWER_CONSUMPTION * xray_power_mod * motion_power_mod * EMP_power_mod;
    }

    // tg: code/game/machinery/camera/presets.dm:155
    public virtual object isEmpProof(object ignore_malf_upgrades = null)
        => Dm.truthy(Dm.bitand(camera_upgrade_bitflags, CAMERA_UPGRADE_EMP_PROOF)) && !(Dm.truthy(ignore_malf_upgrades) && Dm.truthy(malf_emp_firmware_active)) ? TRUE : FALSE;

    // tg: code/game/machinery/camera/presets.dm:182
    public virtual object isXRay(object ignore_malf_upgrades = null)
        => Dm.truthy(Dm.bitand(camera_upgrade_bitflags, CAMERA_UPGRADE_XRAY)) && !(Dm.truthy(ignore_malf_upgrades) && Dm.truthy(malf_xray_firmware_active)) ? TRUE : FALSE;

    // tg: code/game/machinery/camera/presets.dm:208
    public virtual object isMotion() => Dm.bitand(camera_upgrade_bitflags, CAMERA_UPGRADE_MOTION);
}

// Silicons SI4 (D326): the rest of camera.dm: the camera chunks' procs (moving, the EMP, breaking, what a camera sees), the AI's click,
// the upgrades' removal, the deconstruction and the remote view.
public partial class obj_machinery_camera : obj_machinery
{
    // tg: code/game/machinery/camera/camera.dm:159
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        foreach (object i in Dm.each(network.Copy()))
        {
            network.Remove(i);
            network.Add(Dm.text("[]_[]", port.shuttle_id, i));
        }
        return null;
    }

    // tg: code/game/machinery/camera/camera.dm:164
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone != null && gone == xray_module)
        {
            xray_module = null;
            update_appearance();
            if (Dm.truthy(malf_xray_firmware_present))
                malf_xray_firmware_active = malf_xray_firmware_present; //re-enable firmware based upgrades after the part is removed.
            removeXRay(malf_xray_firmware_present); //make sure we don't remove MALF upgrades.
        }
        else if (gone != null && gone == emp_module)
        {
            emp_module = null;
            if (Dm.truthy(malf_emp_firmware_present))
                malf_emp_firmware_active = malf_emp_firmware_present; //re-enable firmware based upgrades after the part is removed.
            removeEmpProof(malf_emp_firmware_present); //make sure we don't remove MALF upgrades
        }
        else if (gone != null && (object)gone == proximity_monitor)
        {
            emp_module = null;
            removeMotion();
        }
        return dot;
    }

    // tg: code/game/machinery/camera/camera.dm:183
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        SScameras.camera_moved(this, get_turf(old_loc), get_turf(loc));
        return dot;
    }

    // tg: code/game/machinery/camera/camera.dm:187
    public virtual void create_prox_monitor()
    {
        if (proximity_monitor == null)
        {
            proximity_monitor = Dm.@new<datum_proximity_monitor>(this, 1);
            RegisterSignal(proximity_monitor, COMSIG_QDELETING, nameof(proximity_deleted));
        }
    }

    // tg: code/game/machinery/camera/camera.dm:192
    public virtual void proximity_deleted(datum source = null)
    {
        proximity_monitor = null;
    }

    // tg: code/game/machinery/camera/camera.dm:196
    public virtual void set_area_motion(datum_motion_group group = null)
    {
        area_motion = group;
        create_prox_monitor();
    }

    // tg: code/game/machinery/camera/camera.dm:228
    [DmParams("severity", "reset_time")]
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        float reset_time = __extra.Length > 0 && __extra[0] != null ? Dm.num(__extra[0]) : 90 * SECONDS;
        object dot = await base.emp_act(severity);
        if (!Dm.truthy(camera_enabled))
            return dot;
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (!prob(150 / Dm.num(severity)))
            return dot;
        network = Dm.list();
        SScameras.remove_camera_from_chunk(this);
        await set_machine_stat(Dm.bitor(machine_stat, EMPED));
        await set_light(0);
        emped = Dm.num(emped) + 1; //Increase the number of consecutive EMP's
        update_appearance();
        calculate_active_power();
        _addtimer(Dm.@new<datum_callback>(this, nameof(post_emp_reset), emped, network), reset_time, file: "code/game/machinery/camera/camera.dm", line: 242);
        foreach (mob M in Dm.each<mob>(GLOB.player_list))
        {
            if (M.client?.eye == this)
            {
                await M.reset_perspective(null);
                to_chat(M, span_warning(I18n.T("The screen bursts into static!")));
            }
        }
        return dot;
    }

    // tg: code/game/machinery/camera/camera.dm:248
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        await base.on_saboteur(source, disrupt_duration);
        //lasts twice as much so we don't have to constantly shoot cameras just to be S T E A L T H Y
        await emp_act(EMP_LIGHT, Dm.num(disrupt_duration) * 2);
        return TRUE;
    }

    // tg: code/game/machinery/camera/camera.dm:254
    public virtual async DmTask post_emp_reset(object thisemp = null, object previous_network = null)
    {
        if (QDELETED(this))
            return;
        triggerCameraAlarm(); //camera alarm triggers even if multiple EMPs are in effect.
        if (!Dm.eq(emped, thisemp)) //Only fix it if the camera hasn't been EMP'd again
            return;
        network = previous_network as DmList;
        await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(EMPED)));
        update_appearance();
        if (Dm.truthy(can_use()))
            SScameras.add_camera_to_chunk(this);
        emped = 0; //Resets the consecutive EMP count
        _addtimer(Dm.@new<datum_callback>(this, nameof(cancelCameraAlarm)), 10 * SECONDS, file: "code/game/machinery/camera/camera.dm", line: 266);
        calculate_active_power();
    }

    // tg: code/game/machinery/camera/camera.dm:269
    public override async DmTask<object> attack_ai(mob user = null)
    {
        if (user is not mob_living_silicon_ai ai)
            return null;
        if (!Dm.truthy(can_use()))
            return null;
        ai.switchCamera(this);
        return null;
    }

    ///Drops a specific upgrade and nulls it where necessary.
    // tg: code/game/machinery/camera/camera.dm:290
    public virtual void drop_upgrade(object upgrade_dropped = null)
    {
        if (upgrade_dropped is atom_movable dropped_upgrade)
            Waiting.NoSleep(dropped_upgrade.forceMove(drop_location() as atom));
        if (upgrade_dropped != null && upgrade_dropped == xray_module)
        {
            xray_module = null;
            if (Dm.truthy(malf_xray_firmware_present))
                malf_xray_firmware_active = malf_xray_firmware_present; //re-enable firmware based upgrades after the part is removed.
            update_appearance();
        }
        else if (upgrade_dropped != null && upgrade_dropped == emp_module)
        {
            emp_module = null;
            if (Dm.truthy(malf_emp_firmware_present))
                malf_emp_firmware_active = malf_emp_firmware_present; //re-enable firmware based upgrades after the part is removed.
        }
        else if (upgrade_dropped != null && upgrade_dropped == proximity_monitor)
            proximity_monitor = null;
    }

    // tg: code/game/machinery/camera/camera.dm:307
    public override async DmTask<object> run_atom_armor(object damage_amount = null, object damage_type = null, object damage_flag = null, object attack_dir = null, float? armour_penetration = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return damage_amount;
        return await base.run_atom_armor(damage_amount, damage_type, damage_flag, attack_dir, armour_penetration);
    }

    // tg: code/game/machinery/camera/camera.dm:312
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        if (!Dm.truthy(camera_enabled))
            return null;
        object dot = await base.atom_break(damage_flag);
        if (Dm.truthy(dot))
        {
            triggerCameraAlarm();
            toggle_cam(null, 0);
        }
        return dot;
    }

    // tg: code/game/machinery/camera/camera.dm:320
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (!Dm.truthy(disassembled))
        {
            if (camera_construction_state >= CAMERA_STATE_WIRED)
                Dm.@new<obj_item_stack_cable_coil>(drop_location(), 2);
            Dm.@new<obj_item_stack_sheet_iron>(loc);
            return null;
        }

        obj_item_wallframe_camera dropped_cam = Dm.@new<obj_item_wallframe_camera>(drop_location());
        dropped_cam.update_integrity(Dm.num(dropped_cam.max_integrity) * 0.5f);
        if (camera_construction_state >= CAMERA_STATE_WIRED)
            Dm.@new<obj_item_stack_cable_coil>(drop_location(), 2);
        if (xray_module != null)
            drop_upgrade(xray_module);
        if (emp_module != null)
            drop_upgrade(emp_module);
        if (proximity_monitor != null)
            drop_upgrade(proximity_monitor);
        return null;
    }

    /// Returns an alist of turfs in this camera's view. This includes turfs that are "obscured by darkness" from the camera's POV.
    /// Format is "alist[turf] = null", if you need to check individual objects use "length(can_see & list(turf))". Only "&" and "in" work for checking contents, but "in" is much slower.
    /// Always have the return value of can_see as the left-hand operand, otherwise it uses list checks instead of alist checks and your CPU time gets thrown in a blender.
    // tg: code/game/machinery/camera/camera.dm:417
    public virtual DmList can_see()
    {
        DmList see = Dm.list();
        turf pos = get_turf(this);
        turf directly_above = GET_TURF_ABOVE(pos) as turf;
        bool check_lower = pos != get_lowest_turf(pos);
        bool check_higher = directly_above != null && istransparentturf(directly_above) && pos != get_highest_turf(pos);

        if (Dm.truthy(isXRay()))
            Dm.append(ref see, RANGE_TURFS(view_range, pos));
        else
        {
            float lum = pos.luminosity;
            pos.luminosity = 6;
            foreach (turf seen in Dm.each<turf>(view(view_range, pos)))
                Dm.append(ref see, seen);
            pos.luminosity = lum;
        }

        if (check_lower || check_higher)
        {
            // Haha datum var access KILL ME
            foreach (turf seen in Dm.each<turf>(see.Copy()))
            {
                if (check_lower)
                {
                    turf below = seen;
                    while (below != null && istransparentturf(below))
                    {
                        below = GET_TURF_BELOW(below) as turf;
                        Dm.append(ref see, RANGE_TURFS(1, below));
                    }
                }
                if (check_higher)
                {
                    turf above = GET_TURF_ABOVE(seen) as turf;
                    while (above != null && istransparentturf(above))
                    {
                        Dm.append(ref see, RANGE_TURFS(1, above));
                        above = GET_TURF_ABOVE(above) as turf;
                    }
                }
            }
        }

        return see;
    }

    // tg: code/game/machinery/camera/camera.dm:449
    public virtual void Togglelight(float on = 0)
    {
        foreach (mob_living_silicon_ai A in Dm.each<mob_living_silicon_ai>(GLOB.ai_list))
        {
            foreach (obj_machinery_camera cam in Dm.each<obj_machinery_camera>(A.lit_cameras))
            {
                if (cam == this)
                    return;
            }
        }
        if (Dm.truthy(on))
            Waiting.NoSleep(set_light(AI_CAMERA_LUMINOSITY));
        else
            Waiting.NoSleep(set_light(0));
    }

    // tg: code/game/machinery/camera/camera.dm:458
    public override void get_remote_view_fullscreens(mob user = null)
    {
        if (view_range == short_range) //unfocused
            user.overlay_fullscreen("remote_view", typeof(atom_movable_screen_fullscreen_impaired), 2);
    }

    // tg: code/game/machinery/camera/camera.dm:462
    public override object update_remote_sight(mob_living user = null, DmList new_sight = null)
    {
        user.set_invis_see(SEE_INVISIBLE_LIVING); //can't see ghosts through cameras
        user.lighting_cutoff = LIGHTING_CUTOFF_VISIBLE;
        user.lighting_color_cutoffs = Dm.list(0, 0, 0);
        if (Dm.truthy(isXRay()))
            user.set_sight(Dm.bitor(Dm.bitor(SEE_TURFS, SEE_MOBS), SEE_OBJS));
        else
            user.set_sight(SEE_TURFS);
        return TRUE;
    }
}

/**
 * Camera assembly frame
 * Putting this on a wall will put a deconstructed camera machine on the wall.
 */
// tg: code/game/machinery/camera/camera.dm:14  (silicons SI4)
public partial class obj_item_wallframe_camera : obj_item_wallframe
{
    // tg: code/game/machinery/camera/camera.dm:23
    public override object find_support_structure(atom structure = null)
    {
        return structure is obj_structure_window ? structure : base.find_support_structure(structure);
    }
}

