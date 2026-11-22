// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/robot/robot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2 and SI4, D326; the AI's shells came with the AI). The var block is generated (D111).

// tg: code/modules/mob/living/silicon/robot/robot.dm:8
public partial class mob_living_silicon_robot : mob_living_silicon
{
    private static ref DmList Initialize__alert_areas => ref TgState.Current.mob_living_silicon_robot__Initialize__alert_areas;

    /// The cell, typed (the var holds a typepath before Initialize)
    internal obj_item_stock_parts_power_store cell__obj => cell as obj_item_stock_parts_power_store;

    /**
     * Init args
     * * innate_laws: If set, the cyborg will use this lawset and will not linked to an ai. Make sure you're passing a copy of the lawset
     * * master_ai: If set, the cyborg will try to link to this ai on spawn rather than the first ai found
     * * aisync: If TRUE, will try to link to an AI on spawn. If FALSE, will not link to any AI on spawn, even if master_ai is set
     * * lawsync: Sets lawupdate variable - if FALSE we will link to an ai but we won't take their laws
     */
    // tg: code/modules/mob/living/silicon/robot/robot.dm:8
    [DmParams("mapload", "innate_laws", "master_ai", "aisync", "lawsync")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_ai_laws innate_laws = Dm.arg<datum_ai_laws>(__extra, 0);
        mob_living_silicon master_ai = Dm.arg<mob_living_silicon>(__extra, 1);
        object aisync = Dm.arg(__extra, 2) ?? TRUE;
        object lawsync = Dm.arg(__extra, 3) ?? lawupdate;
        spark_system = Dm.@new<datum_effect_system_basic_spark_spread>(this, 5, FALSE);
        spark_system.attach(this);

        add_traits(Dm.list(TRAIT_CAN_STRIP, TRAIT_FORCED_STANDING, TRAIT_KNOW_ENGI_WIRES, TRAIT_IGNORE_SURGERY_MODIFIERS), INNATE_TRAIT);
        Waiting.NoSleep(AddComponent(typeof(datum_component_tippable),
            Dm.named("tip_time", 3 * SECONDS),
            Dm.named("untip_time", 2 * SECONDS),
            Dm.named("self_right_time", 60 * SECONDS),
            Dm.named("post_tipped_callback", Dm.@new<datum_callback>(this, nameof(after_tip_over))),
            Dm.named("post_untipped_callback", Dm.@new<datum_callback>(this, nameof(after_righted))),
            Dm.named("roleplay_friendly", TRUE),
            Dm.named("roleplay_emotes", Dm.list(typeof(datum_emote_silicon_buzz), typeof(datum_emote_silicon_buzz2), typeof(datum_emote_silicon_beep))),
            Dm.named("roleplay_callback", Dm.@new<datum_callback>(this, nameof(untip_roleplay)))));

        set_wires(Dm.@new<datum_wires_robot>(this));
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_WIRES));
        Waiting.NoSleep(AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_cyborg)));
        RegisterSignal(this, COMSIG_PROCESS_BORGCHARGER_OCCUPANT, nameof(charge));
        RegisterSignal(this, COMSIG_LIGHT_EATER_ACT, nameof(on_light_eater));
        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_GOT_DAMPENED), nameof(on_dampen));

        previous_health = health;

        if (ispath(cell))
            cell = Dm.@new((Type)cell, this);

        create_modularInterface();

        model = Dm.@new<obj_item_robot_model>(this);
        Waiting.NoSleep(model.rebuild_modules());

        if (innate_laws is datum_ai_laws)
        {
            laws = innate_laws.copy_lawset() as datum_ai_laws;
            lawupdate = FALSE;
        }
        else
            make_laws();
        if (!Dm.truthy(aisync) || !Dm.truthy(lawsync))
            lawupdate = FALSE;
        // try connect to ai will update the lawset so we need to ensure law update is set before calling it
        if (Dm.truthy(aisync) && !Dm.truthy(Waiting.NoSleep(try_connect_to_ai(master_ai))))
        {
            datum_job human_ai_job = SSjob?.get_job(JOB_HUMAN_AI) as datum_job; // a host-built room without the Master has no SSjob (rules 10.6)
            // we failed to connect to an ai, but that might be because there is a human ai. so we have to check for that
            if (human_ai_job != null && Dm.truthy(human_ai_job.current_positions) && !Dm.truthy(laws.zeroth_borg))
            {
                laws.zeroth_borg = "Follow the orders of Big Brother.";
                laws.protected_zeroth = TRUE;
            }
            // however, if we found no ai and no human ai, we fall back to no law updates
            // and link ourselves to the first law rack if possible
            else
            {
                lawupdate = FALSE;
                Waiting.NoSleep(link_to_first_rack());
            }
        }

        if (!Dm.truthy(scrambledcodes) && builtInCamera == null)
        {
            builtInCamera = Dm.@new<obj_machinery_camera_silicon>(this);
            builtInCamera.c_tag = real_name;
            if (Dm.truthy(wires.is_cut(WIRE_CAMERA)))
                builtInCamera.camera_enabled = FALSE;
        }
        Waiting.NoSleep(update_icons());
        object dot = base.Initialize(mapload, Dm.forward(__extra, innate_laws, master_ai, aisync, lawsync));

        Waiting.NoSleep(LoadComponent(typeof(datum_component_bloodysoles_bot)));

        //If this body is meant to be a borg controlled by the AI player
        if (Dm.truthy(shell))
        {
            obj_item_borg_upgrade_ai board = Dm.@new<obj_item_borg_upgrade_ai>(this);
            Waiting.NoSleep(make_shell(board));
            Waiting.NoSleep(add_to_upgrades(board));
            ADD_TRAIT(this, TRAIT_CAN_GET_AI_TRACKING_MESSAGE, INNATE_TRAIT);
        }
        else
        {
            //MMI stuff. Held togheter by magic. ~Miauw
            if (mmi?.brainmob == null)
                mmi = Dm.@new<obj_item_brain_processor_organic>(this, Dm.@new<obj_item_organ_brain>());
            Waiting.NoSleep(setup_default_name());

            if (mmi.brainmob != null)
                gender = mmi.brainmob.gender;
        }

        aicamera = Dm.@new<obj_item_camera_siliconcam_robot_camera>(this);
        toner = tonermax;
        diag_hud_set_borgcell();
        Waiting.NoSleep(logevent("System brought online."));

        log_silicon(Dm.text("New cyborg [] created with []", key_name(this), connected_ai != null ? Dm.text("master AI: []", key_name(connected_ai)) : "no master AI"));
        log_current_laws();

        if (Initialize__alert_areas == null)
            Initialize__alert_areas = Dm.add(GLOB.the_station_areas, Dm.typesof(typeof(area_mine))) as DmList;
        if (Dm.truthy(is_station_level(z)))
            alert_control = Dm.@new<datum_station_alert>(this, Dm.list(ALARM_ATMOS, ALARM_FIRE, ALARM_POWER, ALARM_CAMERA, ALARM_BURGLAR, ALARM_MOTION), SSmapping.levels_by_trait(ZTRAIT_STATION), Initialize__alert_areas);
        else
            alert_control = Dm.@new<datum_station_alert>(this, Dm.list(ALARM_ATMOS, ALARM_FIRE, ALARM_POWER, ALARM_CAMERA, ALARM_BURGLAR, ALARM_MOTION), Dm.add(SSmapping.levels_by_trait(ZTRAIT_STATION), z), Initialize__alert_areas);
        RegisterSignal(alert_control.listener, COMSIG_ALARM_LISTENER_TRIGGERED, nameof(alarm_triggered));
        RegisterSignal(alert_control.listener, COMSIG_ALARM_LISTENER_CLEARED, nameof(alarm_cleared));
        alert_control.listener.RegisterSignal(this, COMSIG_LIVING_DEATH, nameof(datum_alarm_listener.prevent_alarm_changes));
        alert_control.listener.RegisterSignal(this, COMSIG_LIVING_REVIVE, nameof(datum_alarm_listener.allow_alarm_changes));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:126  (the cyborg's override, interleaved in silicon.dm)
    public override void create_modularInterface()
    {
        if (modularInterface == null)
        {
            modularInterface = Dm.@new<obj_item_modular_computer_pda_silicon_cyborg>(this);
            modularInterface.imprint_id(job_name: "Cyborg");
        }
        base.create_modularInterface();
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:105
    public override void set_suicide(object suicide_state = null)
    {
        base.set_suicide(suicide_state);
        mmi?.set_suicide(suicide_state);
    }

    /**
     * Sets the tablet theme and icon
     *
     * These variables are based on if the borg is a syndicate type or is emagged. This gets used in model change code
     * and also borg emag code.
     */
    // tg: code/modules/mob/living/silicon/robot/robot.dm:115
    public virtual async DmTask set_modularInterface_theme()
    {
        if (model is obj_item_robot_model_syndicate || Dm.truthy(emagged))
        {
            modularInterface.device_theme = PDA_THEME_SYNDICATE;
            modularInterface.icon_state = "tablet-silicon-syndicate";
        }
        else
        {
            modularInterface.device_theme = PDA_THEME_NTOS;
            modularInterface.icon_state = "tablet-silicon";
            modularInterface.icon_state_powered = "tablet-silicon";
            modularInterface.icon_state_unpowered = "tablet-silicon";
        }
        await modularInterface.update_icon();
    }

    //If there's an MMI in the robot, have it ejected when the mob goes away. --NEO
    // tg: code/modules/mob/living/silicon/robot/robot.dm:127
    public override object Destroy(float? force = null)
    {
        if (connected_ai != null)
            Waiting.NoSleep(set_connected_ai(null));
        if (Dm.truthy(shell))
            Dm.remove(ref GLOB.available_ai_shells, this);

        QDEL_NULL(ref modularInterface);
        QDEL_NULL(ref model);
        QDEL_NULL(ref eye_lights);
        QDEL_NULL(ref hat_overlay);
        QDEL_NULL(ref spark_system);
        QDEL_NULL(ref alert_control);
        QDEL_LIST(upgrades);
        QDEL_NULL(ref cell);
        QDEL_NULL(ref robot_suit);

        if (smoke_particles != null)
            remove_shared_particles(smoke_particles);
        if (spark_particles != null)
            remove_shared_particles(spark_particles);

        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:150
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        object dot = await base.Topic(href, href_list);
        //Show alerts window if user clicked on "Show alerts" in chat
        if (Dm.truthy(href_list["showalerts"]))
            await alert_control.ui_interact(this);
        if (Dm.truthy(locked) && Dm.truthy(href_list["unlock_self"]))
            await toggle_cover();
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:158
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:161
    public virtual async DmTask pick_model()
    {
        if (model.GetType() != typeof(obj_item_robot_model))
            return;

        if (Dm.truthy(wires.is_cut(WIRE_RESET_MODEL)))
        {
            to_chat(this, span_userdanger(I18n.T("ERROR: Model installer reply timeout. Please check internal connections.")));
            return;
        }

        if (Dm.eq(lockcharge, TRUE))
        {
            to_chat(this, span_userdanger(I18n.T("ERROR: Lockdown is engaged. Please disengage lockdown to pick module.")));
            return;
        }

        DmList model_list = Dm.list(
            Dm.kv("Engineering", typeof(obj_item_robot_model_engineering)),
            Dm.kv("Medical", typeof(obj_item_robot_model_medical)),
            Dm.kv("Miner", typeof(obj_item_robot_model_miner)),
            Dm.kv("Janitor", typeof(obj_item_robot_model_janitor)),
            Dm.kv("Service", typeof(obj_item_robot_model_service))
        );
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_disable_peaceborg))))
            model_list["Peacekeeper"] = typeof(obj_item_robot_model_peacekeeper);
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_disable_secborg))))
            model_list["Security"] = typeof(obj_item_robot_model_security);

        // Create radial menu for choosing borg model
        DmList model_icons = Dm.list();
        foreach (object option in Dm.each(model_list))
        {
            Type model_type = model_list[option] as Type;
            object model_icon = Dm.initial(model_type, "cyborg_base_icon");
            model_icons[option] = image(icon: Dm.resource("icons/mob/silicon/robots.dmi"), icon_state: model_icon);
        }

        object input_model = await show_radial_menu(this, this, model_icons, radius: 42);
        if (!Dm.truthy(input_model) || model.GetType() != typeof(obj_item_robot_model))
            return;

        await model.transform_to(model_list[input_model]);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:198
    public override void set_name() //we have our name-making proc to call after we make our mmi, just set identifier here
    {
        if (Dm.eq(identifier, 0))
            identifier = rand(1, 999);
    }

    /// Used to setup the a basic and (somewhat) unique name for the robot.
    // tg: code/modules/mob/living/silicon/robot/robot.dm:203
    public virtual async DmTask setup_default_name()
    {
        object new_name;
        // GLOB.current_anonymous_theme (only robotic renames will allow for anything other than the anonymous one): the anonymous
        // names (an admin's secret, admin tooling), never set, as in the human's apply_prefs_job
        if (Dm.truthy(custom_name))
            new_name = custom_name;
        else
            new_name = get_standard_name();
        if (!Dm.eq(new_name, real_name))
            await fully_replace_character_name(real_name, new_name);
    }

    /// Updates the borg name taking the client preferences into account.
    // tg: code/modules/mob/living/silicon/robot/robot.dm:216
    public virtual async DmTask updatename(client pref_source = null)
    {
        if (Dm.truthy(shell))
            return;
        if (pref_source == null)
            pref_source = client;
        object changed_name = "";
        // GLOB.current_anonymous_theme: the anonymous names (admin tooling), never set (see setup_default_name)
        if (Dm.truthy(custom_name))
            changed_name = custom_name;
        else if (pref_source != null && !Dm.eq(pref_source.prefs.read_preference(typeof(datum_preference_name_cyborg)), DEFAULT_CYBORG_NAME))
        {
            await apply_pref_name(typeof(datum_preference_name_cyborg), pref_source);
            return; //built in camera handled in proc
        }
        else
            changed_name = get_standard_name();

        await fully_replace_character_name(real_name, changed_name);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:235
    public virtual object get_standard_name()
    {
        return Dm.text("[][]-[]", Dm.truthy(designation) ? Dm.text("[] ", designation) : "", mmi.braintype, identifier);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:238
    public virtual async DmTask<object> ionpulse__proc()
    {
        if (!Dm.truthy(ionpulse_on))
            return null;

        if (!Dm.truthy(await cell__obj.use(0.01 * STANDARD_CELL_CHARGE)))
        {
            toggle_ionpulse();
            return null;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:247
    public virtual void toggle_ionpulse()
    {
        if (!Dm.truthy(ionpulse))
        {
            to_chat(this, span_notice(I18n.T("No thrusters are installed!")));
            return;
        }

        if (ion_trail == null)
            ion_trail = Dm.@new<datum_effect_system_trail_follow_ion>(this);

        ionpulse_on = Dm.not(ionpulse_on);
        to_chat(this, span_notice(Dm.truthy(ionpulse_on) ? I18n.T("You activate your ion thrusters.") : I18n.T("You deactivate your ion thrusters.")));
        if (Dm.truthy(ionpulse_on))
            Waiting.NoSleep(ion_trail.start());
        else
            ion_trail.stop();
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:262
    public override object get_status_tab_items()
    {
        DmList dot = base.get_status_tab_items() as DmList;
        if (cell != null)
            Dm.append(ref dot, Dm.text("Charge Left: []/[]", display_energy(cell__obj.charge), display_energy(cell__obj.maxcharge)));
        else
            Dm.append(ref dot, "No Cell Inserted!");

        if (connected_ai != null)
            Dm.append(ref dot, Dm.text("Master AI: []", connected_ai.name));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:272
    [SignalHandler]
    public virtual void alarm_triggered(datum source = null, object alarm_type = null, area source_area = null)
    {
        queueAlarm(I18n.T("--- [] alarm detected in []!", alarm_type, source_area.name), alarm_type);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:276
    [SignalHandler]
    public virtual void alarm_cleared(datum source = null, object alarm_type = null, area source_area = null)
    {
        queueAlarm(I18n.T("--- [] alarm in [] has been cleared.", alarm_type, source_area.name), alarm_type, FALSE);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:280
    public override async DmTask<object> can_interact_with(atom A = null, object treat_mob_as_adjacent = null)
    {
        if (A == modularInterface)
            return TRUE; //bypass for borg tablets
        if (Dm.truthy(low_power_mode))
            return FALSE;
        return await base.can_interact_with(A, treat_mob_as_adjacent);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:288
    public virtual async DmTask after_tip_over(mob user = null)
    {
        if (hat != null && !HAS_TRAIT(hat, TRAIT_NODROP))
            await hat.forceMove(drop_location() as atom);

        unbuckle_all_mobs();
    }

    ///For any special cases for robots after being righted.
    // tg: code/modules/mob/living/silicon/robot/robot.dm:295
    public virtual void after_righted(mob user = null)
    {
        return;
    }

    ///Toggles the Cyborg's cover lock, user is provided only if it's someone else doing it (not the borg itself)
    // tg: code/modules/mob/living/silicon/robot/robot.dm:299
    public virtual async DmTask toggle_cover(mob user = null)
    {
        locked = Dm.not(locked);
        balloon_alert(this, Dm.truthy(locked) ? I18n.T("cover locked") : I18n.T("cover unlocked"));
        await update_icons();
        if (user != null)
            balloon_alert(user, Dm.truthy(emagged) ? I18n.T("chassis cover lock glitches") : Dm.truthy(locked) ? I18n.T("chassis cover locked") : I18n.T("chassis cover unlocked"));
        await logevent(Dm.text("[] cover lock has been []", Dm.truthy(emagged) ? "ChÃ¥vÃis" : "Chassis", Dm.truthy(locked) ? "engaged" : "released"));
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:307
    public override async DmTask<object> regenerate_icons()
    {
        await update_icons();
        return null;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:310
    public override async DmTask update_icons()
    {
        cut_overlays();
        SSvis_overlays.remove_vis_overlay(this, managed_vis_overlays);
        icon_state = model.cyborg_base_icon;
        if (!IS_UNCONSCIOUS(this) && !Dm.truthy(IsStun()) && !Dm.truthy(IsParalyzed()) && !Dm.truthy(low_power_mode) && !Dm.truthy(is_empty_shell())) //Not dead, not stunned, not an unpiloted AI shell.
        {
            if (eye_lights == null)
                eye_lights = Dm.@new<mutable_appearance>();
            if (Dm.truthy(lamp_enabled) || Dm.truthy(lamp_doom))
            {
                eye_lights.icon_state = Dm.text("[]_l", Dm.truthy(model.special_light_key) ? Dm.text("[]", model.special_light_key) : Dm.text("[]", model.cyborg_base_icon));
                set_light_range(max(MINIMUM_USEFUL_LIGHT_RANGE, lamp_intensity));
                set_light_color(Dm.truthy(lamp_doom) ? COLOR_RED : lamp_color); //Red for doomsday killborgs, borg's choice otherwise
                SET_PLANE_EXPLICIT(eye_lights, ABOVE_LIGHTING_PLANE, this); //glowy eyes
            }
            else
            {
                eye_lights.icon_state = Dm.text("[]_e", Dm.truthy(model.special_light_key) ? Dm.text("[]", model.special_light_key) : Dm.text("[]", model.cyborg_base_icon));
                eye_lights.color = COLOR_WHITE;
                SET_PLANE_EXPLICIT(eye_lights, ABOVE_GAME_PLANE, this);
            }
            eye_lights.icon = icon;
            add_overlay(eye_lights);
        }

        obj_item_shield_module shield_module = Dm.locate<obj_item_shield_module>(this);
        if (shield_module != null && Dm.truthy(shield_module.active))
            add_overlay(shield_module.shield_overlay);
        else if (shield_module != null && !Dm.truthy(shield_module.active))
            cut_overlay(shield_module.shield_overlay);

        if (Dm.truthy(opened))
        {
            if (Dm.truthy(wiresexposed))
                add_overlay("ov-opencover +w");
            else if (cell != null)
                add_overlay("ov-opencover +c");
            else
                add_overlay("ov-opencover -c");
        }

        if (hat != null)
        {
            hat_overlay = await hat.build_worn_icon(default_layer: 20, default_icon_file: Dm.resource("icons/mob/clothing/head/default.dmi")) as mutable_appearance;
            update_worn_icons();
        }
        else if (hat_overlay != null)
            QDEL_NULL(ref hat_overlay);

        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:351
    public virtual void update_worn_icons()
    {
        if (hat_overlay == null)
            return;
        cut_overlay(hat_overlay);

        if (islist(hat_offset))
        {
            DmList offset = hat_offset[Dm.truthy(ISDIAGONALDIR(Dm.num(dir))) ? dir2text(Dm.bitand(dir, Dm.bitor(WEST, EAST))) : dir2text(dir)] as DmList;
            if (Dm.truthy(offset))
            {
                hat_overlay.pixel_w = Dm.num(offset[1]);
                hat_overlay.pixel_z = Dm.num(offset[2]);
            }
        }

        add_overlay(hat_overlay);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:364
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        // . = ..(): /atom/setDir returns nothing, so tg's `. != old_dir` holds whenever the old dir was set
        update_worn_icons();
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:370
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        if (Dm.truthy(same_z_layer) || QDELING(this))
        {
            await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
            return;
        }

        if (eye_lights != null)
        {
            cut_overlay(eye_lights);
            SET_PLANE_EXPLICIT(eye_lights, Dm.num(PLANE_TO_TRUE(eye_lights.plane)), this);
            add_overlay(eye_lights);
        }

        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:381
    public virtual async DmTask self_destruct(mob user = null)
    {
        turf groundzero = get_turf(this);
        message_admins(span_notice(Dm.text("[] detonated [] at []!", ADMIN_LOOKUPFLW(user), key_name_admin(this, Dm.truthy(client) ? TRUE : FALSE), ADMIN_VERBOSEJMP(groundzero))));
        user.log_message(Dm.text("detonated []!", key_name(this)), LOG_ATTACK);
        log_message(Dm.text("was detonated by []!", key_name(user)), LOG_ATTACK, log_globally: FALSE);

        log_combat(user, this, "detonated cyborg");
        log_silicon(Dm.text("CYBORG: [] has been detonated by [].", key_name(this), key_name(user)));
        if (connected_ai != null)
            to_chat(connected_ai, Dm.text("<br><br>[]<br>", span_alert(I18n.T("ALERT - Cyborg detonation detected: []", name))));

        if (Dm.truthy(emagged))
        {
            QDEL_NULL(ref mmi);
            await explosion(this, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 4, flame_range: 2);
        }
        else
            await explosion(this, devastation_range: -1, light_impact_range: 2);
        investigate_log("has self-destructed.", INVESTIGATE_DEATHS);
        await gib(DROP_ALL_REMAINS);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:400
    public virtual async DmTask UnlinkSelf()
    {
        await set_connected_ai(null);
        lawupdate = FALSE;
        await set_lockcharge(FALSE);
        scrambledcodes = TRUE;
        log_silicon(Dm.text("CYBORG: [] has been unlinked from an AI.", key_name(this)));
        //Disconnect its camera so it's not so easily tracked.
        if (!QDELETED(builtInCamera))
            QDEL_NULL(ref builtInCamera);
        // I'm trying to get the Cyborg to not be listed in the camera list
        // Instead of being listed as "deactivated". The downside is that I'm going
        // to have to check if every camera is null or not before doing anything, to prevent runtime errors.
        // I could change the network to null but I don't know what would happen, and it seems too hacky for me.
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:414
    public override async DmTask execute_mode()
    {
        if (Dm.truthy(incapacitated))
            return;
        obj_item W = get_active_held_item() as obj_item;
        if (W != null)
            await W.attack_self(this);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:422
    public virtual async DmTask SetLockdown(float? state = null)
    {
        state ??= TRUE;
        // They stay locked down if their wire is cut.
        if (Dm.truthy(wires?.is_cut(WIRE_LOCKDOWN)))
            state = TRUE;
        if (Dm.truthy(state))
        {
            throw_alert(ALERT_HACKED, typeof(atom_movable_screen_alert_locked));
            if (!Dm.truthy(ai_lockdown))
            {
                lockdown_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(lockdown_override), FALSE), 10 * MINUTES, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), Dm.bitor(TIMER_DELETE_ME, TIMER_STOPPABLE)), file: "code/modules/mob/living/silicon/robot/robot.dm", line: 429);
                to_chat(this, Dm.text("<br><br>[]<br>", span_alert(I18n.T("ALERT - Remote system lockdown engaged. Trying to hack the lockdown subsystem..."))));
            }
        }
        else
        {
            deltimer(lockdown_timer);
            clear_alert(ALERT_HACKED);
        }
        await set_lockcharge(state);
    }

    /// Allows the borg to unlock themselves after a lenghty period of time.
    // tg: code/modules/mob/living/silicon/robot/robot.dm:437
    public virtual async DmTask lockdown_override()
    {
        if (Dm.truthy(ai_lockdown))
        {
            to_chat(this, Dm.text("<br><br>[]<br>", span_alert(I18n.T("ALERT - Remote system lockdown override failed."))));
            return;
        }
        await set_lockcharge(FALSE);
        to_chat(this, Dm.text("<br><br>[]<br>", span_notice(I18n.T("ALERT - Remote system lockdown override successful."))));
        if (connected_ai != null)
            to_chat(connected_ai, Dm.text("<br><br>[]<br>", span_notice(I18n.T("ALERT - Cyborg [] successfully overriden the lockdown system", name))));
    }

    ///Reports the event of the change in value of the lockcharge variable.
    // tg: code/modules/mob/living/silicon/robot/robot.dm:447
    public virtual async DmTask<object> set_lockcharge(object new_lockcharge = null)
    {
        if (Dm.eq(new_lockcharge, lockcharge))
            return null;
        object dot = lockcharge;
        lockcharge = Dm.num(new_lockcharge);
        if (Dm.truthy(lockcharge))
        {
            if (!Dm.truthy(dot))
                ADD_TRAIT(this, TRAIT_IMMOBILIZED, LOCKED_BORG_TRAIT);
        }
        else if (Dm.truthy(dot))
            REMOVE_TRAIT(this, TRAIT_IMMOBILIZED, LOCKED_BORG_TRAIT);
        await logevent(Dm.text("System lockdown [].", Dm.truthy(lockcharge) ? "triggered" : "released"));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:460
    public virtual async DmTask SetEmagged(object new_state = null)
    {
        emagged = Dm.num(new_state);
        await model.rebuild_modules();
        await update_icons();
        if (Dm.truthy(emagged))
            throw_alert(ALERT_HACKED, typeof(atom_movable_screen_alert_hacked));
        else
            clear_alert(ALERT_HACKED);
        await set_modularInterface_theme();
    }

    /// Special handling for getting hit with a light eater
    // tg: code/modules/mob/living/silicon/robot/robot.dm:471
    [SignalHandler]
    public virtual object on_light_eater(mob_living_silicon_robot source = null, datum light_eater = null)
    {
        if (Dm.truthy(lamp_enabled))
            Waiting.NoSleep(smash_headlamp());
        return COMPONENT_BLOCK_LIGHT_EATER;
    }

    /// special handling for getting shot with a light disruptor/saboteur e.g. the fisher
    // tg: code/modules/mob/living/silicon/robot/robot.dm:478
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        await base.on_saboteur(source, disrupt_duration);
        if (Dm.truthy(lamp_enabled))
        {
            await toggle_headlamp(TRUE);
            balloon_alert(this, I18n.T("headlamp off!"));
        }
        disabled_time = world.time + Dm.num(disrupt_duration); // COOLDOWN_START (__DEFINES/cooldowns.dm), inline
        return TRUE;
    }

    /**
     * Handles headlamp smashing
     *
     * When called (such as by the shadowperson lighteater's attack), this proc will break the borg's headlamp
     * and then call toggle_headlamp to disable the light. It also plays a sound effect of glass breaking, and
     * tells the borg what happened to its chat. Broken lights can be repaired by using a flashlight on the borg.
     */
    // tg: code/modules/mob/living/silicon/robot/robot.dm:493
    public virtual async DmTask smash_headlamp()
    {
        if (!Dm.truthy(lamp_functional))
            return;
        lamp_functional = FALSE;
        playsound(this, Dm.resource("sound/effects/footstep/glass_step.ogg"), 50);
        await toggle_headlamp(TRUE);
        to_chat(this, span_danger(I18n.T("Your headlamp is broken! You'll need a human to help replace it.")));
    }

    /**
     * Handles headlamp toggling, disabling, and color setting.
     *
     * The initial if statment is a bit long, but the gist of it is that should the lamp be on AND the update_color
     * arg be true, we should simply change the color of the lamp but not disable it. Otherwise, should the turn_off
     * arg be true, the lamp already be enabled, any of the normal reasons the lamp would turn off happen, or the
     * update_color arg be passed with the lamp not on, we should set the lamp off. The update_color arg is only
     * ever true when this proc is called from the borg tablet, when the color selection feature is used.
     *
     * Arguments:
     * * arg1 - turn_off, if enabled will force the lamp into an off state (rather than toggling it if possible)
     * * arg2 - update_color, if enabled, will adjust the behavior of the proc to change the color of the light if it is already on.
     */
    // tg: code/modules/mob/living/silicon/robot/robot.dm:514
    public virtual async DmTask<object> toggle_headlamp(float? turn_off = null, float? update_color = null)
    {
        turn_off ??= FALSE;
        update_color ??= FALSE;
        //if both lamp is enabled AND the update_color flag is on, keep the lamp on. Otherwise, if anything listed is true, disable the lamp.
        if (!(disabled_time <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm), inline
        {
            balloon_alert(this, I18n.T("disrupted!"));
            return FALSE;
        }

        if (!(Dm.truthy(update_color) && Dm.truthy(lamp_enabled)) && (Dm.truthy(turn_off) || Dm.truthy(lamp_enabled) || Dm.truthy(update_color) || !Dm.truthy(lamp_functional) || IS_UNCONSCIOUS_OR_CRIT(this) || Dm.truthy(low_power_mode) || Dm.truthy(is_empty_shell())))
        {
            await set_light_on(Dm.truthy(lamp_functional) && stat != DEAD && Dm.truthy(lamp_doom) ? TRUE : FALSE); //If the lamp isn't broken and borg isn't dead, doomsday borgs cannot disable their light fully.
            set_light_color(COLOR_RED); //This should only matter for doomsday borgs, as any other time the lamp will be off and the color not seen
            set_light_range(1); //Again, like above, this only takes effect when the light is forced on by doomsday mode.
            lamp_enabled = FALSE;
            (hud_used?.screen_objects?[HUD_CYBORG_LAMP] as atom)?.update_appearance();
            await update_icons();
            return null;
        }

        set_light_range(max(MINIMUM_USEFUL_LIGHT_RANGE, lamp_intensity));
        set_light_color(Dm.truthy(lamp_doom) ? COLOR_RED : lamp_color); //Red for doomsday killborgs, borg's choice otherwise
        await set_light_on(TRUE);
        lamp_enabled = TRUE;
        (hud_used?.screen_objects?[HUD_CYBORG_LAMP] as atom)?.update_appearance();
        await update_icons();
        return null;
    }

    ///Completely deconstructs the borg, dropping the MMI/posibrain, removing applied upgrades and stripping the exoskeleton of all limbs,
    ///while also burning out the flashes and prying out the cabling and the cell used in construction
    // tg: code/modules/mob/living/silicon/robot/robot.dm:538
    public virtual async DmTask cyborg_deconstruct()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_BORG_SAFE_DECONSTRUCT]))
            _SendSignal(COMSIG_BORG_SAFE_DECONSTRUCT, Dm.list(this));
        if (Dm.truthy(shell))
            await undeploy();
        atom drop_to = drop_location() as atom;
        //remove installed upgrades
        foreach (obj_item_borg_upgrade upgrade_to_remove in Dm.each<obj_item_borg_upgrade>(upgrades))
            await upgrade_to_remove.forceMove(drop_to);
        if (robot_suit != null)
        {
            await robot_suit.drop_all_parts(drop_to);
            await robot_suit.forceMove(drop_to);
        }
        else
        {
            Dm.@new<obj_item_robot_suit>(drop_to);
            Dm.@new<obj_item_bodypart_leg_left_robot>(drop_to);
            Dm.@new<obj_item_bodypart_leg_right_robot>(drop_to);
            Dm.@new<obj_item_stack_cable_coil>(drop_to, 1);
            Dm.@new<obj_item_bodypart_chest_robot>(drop_to);
            Dm.@new<obj_item_bodypart_arm_left_robot>(drop_to);
            Dm.@new<obj_item_bodypart_arm_right_robot>(drop_to);
            Dm.@new<obj_item_bodypart_head_robot>(drop_to);
            for (int i = 1; i <= 2; i++)
            {
                obj_item_assembly_flash_handheld borgeye = Dm.@new<obj_item_assembly_flash_handheld>(drop_to);
                borgeye.burn_out();
            }
        }

        if (cell != null)
            await cell__obj.forceMove(drop_to); // Cell can be null, if removed beforehand
        obj_item_radio radio__typed = radio as obj_item_radio;
        if (radio__typed?.keyslot != null)
            await (radio__typed.keyslot as obj_item).forceMove(drop_to);
        if (radio__typed != null)
            radio__typed.keyslot = null;

        await dump_into_mmi(drop_to);

        qdel(this);
    }

    /// Dumps the current occupant of the cyborg into an MMI at the passed location
    /// Returns the borg's MMI on success
    // tg: code/modules/mob/living/silicon/robot/robot.dm:573
    public virtual async DmTask<object> dump_into_mmi(atom at_location = null)
    {
        at_location ??= drop_location() as atom;
        if (mmi == null)
            return null;

        obj_item_brain_processor removing = mmi;
        await mmi.forceMove(at_location); // Nulls it out via exited

        if (mind == null) // no one to transfer, just leave the MMI.
            return mmi;

        if (removing.brainmob != null)
        {
            if (removing.brainmob.stat == DEAD)
                await removing.brainmob.set_stat(STABLE);
            await mind.transfer_to(removing.brainmob);
            removing.update_appearance();
        }

        else
        {
            to_chat(this, span_bolddanger(I18n.T("Oops! Something went very wrong, your MMI was unable to receive your mind. You have been ghosted. Please make a bug report so we can fix this bug.")));
            ghostize();
            stack_trace("Borg MMI lacked a brainmob", "code/modules/mob/living/silicon/robot/robot.dm", 593);
        }

        return mmi;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:597
    public virtual void notify_ai(object notifytype = null, object oldname = null, object newname = null)
    {
        if (connected_ai == null)
            return;
        switch (Dm.num(notifytype))
        {
            case AI_NOTIFICATION_NEW_BORG: //New Cyborg
                to_chat(connected_ai, Dm.text("<br><br>[]<br>", span_notice(I18n.T("NOTICE - New cyborg connection detected: <a href='byond://?src=[];track=[]'>[]</a>", REF(connected_ai), html_encode(name), name))));
                break;
            case AI_NOTIFICATION_NEW_MODEL: //New Model
                to_chat(connected_ai, Dm.text("<br><br>[]<br>", span_notice(I18n.T("NOTICE - Cyborg model change detected: [] has loaded the [] model.", name, designation))));
                break;
            case AI_NOTIFICATION_CYBORG_RENAMED: //New Name
                to_chat(connected_ai, Dm.text("<br><br>[]<br>", span_notice(I18n.T("NOTICE - Cyborg reclassification detected: [] is now designated as [].", oldname, newname))));
                break;
            case AI_NOTIFICATION_AI_SHELL: //New Shell
                to_chat(connected_ai, Dm.text("<br><br>[]<br>", span_notice(I18n.T("NOTICE - New cyborg shell detected: <a href='byond://?src=[];track=[]'>[]</a>", REF(connected_ai), html_encode(name), name))));
                break;
            case AI_NOTIFICATION_CYBORG_DISCONNECTED: //Tampering with the wires
                to_chat(connected_ai, Dm.text("<br><br>[]<br>", span_notice(I18n.T("NOTICE - Remote telemetry lost with [].", name))));
                break;
        }
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:612
    public override async DmTask<object> can_perform_action(atom target = null, object action_bitflags = null)
    {
        if (Dm.truthy(lockcharge) || Dm.truthy(low_power_mode))
        {
            to_chat(this, span_warning(I18n.T("You can't do that right now!")));
            return FALSE;
        }
        return await base.can_perform_action(target, action_bitflags);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:618
    public override async DmTask updatehealth()
    {
        await base.updatehealth();
        update_damage_particles();
        if (!Dm.truthy(model.breakable_modules))
            return;

        /// the current percent health of the robot (-1 to 1)
        float percent_hp = Dm.num(health) / Dm.num(maxHealth);
        if (Dm.num(health) <= Dm.num(previous_health)) //if change in health is negative (we're losing hp)
        {
            if (percent_hp <= 0.5f)
                await break_cyborg_slot(3);

            if (percent_hp <= 0)
                await break_cyborg_slot(2);

            if (percent_hp <= -0.5f)
                await break_cyborg_slot(1);
        }

        else //if change in health is positive (we're gaining hp)
        {
            if (percent_hp >= 0.5f)
                repair_cyborg_slot(3);

            if (percent_hp >= 0)
                repair_cyborg_slot(2);

            if (percent_hp >= -0.5f)
                repair_cyborg_slot(1);
        }

        previous_health = health;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:648
    public override void update_stat()
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return;
        if (stat != DEAD && Dm.num(health) <= -Dm.num(maxHealth)) //die only once
        {
            Waiting.NoSleep(death());
            Waiting.NoSleep(toggle_headlamp(TRUE));
            return;
        }
        diag_hud_set_status();
        diag_hud_set_health();
        diag_hud_set_aishell();
        update_health_hud();
        Waiting.NoSleep(update_icons()); //Updates eye_light overlay
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:661
    public override async DmTask<object> revive(float? full_heal_flags = null, float? excess_healing = null, float? force_grab_ghost = null)
    {
        full_heal_flags ??= NONE;
        excess_healing ??= 0;
        force_grab_ghost ??= FALSE;
        object dot = await base.revive(full_heal_flags, excess_healing, force_grab_ghost);
        if (!Dm.truthy(dot))
            return dot;

        if (!QDELETED(builtInCamera) && !Dm.truthy(wires.is_cut(WIRE_CAMERA)))
            builtInCamera.toggle_cam(this, 0);
        if (Dm.truthy(Dm.bitand(full_heal_flags, HEAL_ADMIN)))
            locked = TRUE;
        if (eye_flash_timer != null)
        {
            deltimer(eye_flash_timer);
            eye_flash_timer = null;
        }
        await set_stat(STABLE);
        notify_ai(AI_NOTIFICATION_NEW_BORG);
        await toggle_headlamp(FALSE, TRUE); //This will reenable borg headlamps if doomsday is currently going on still.
        update_stat();
        return TRUE;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:679
    public override async DmTask<object> fully_replace_character_name(object oldname = null, object newname = null, float? log_new_name = null)
    {
        log_new_name ??= FALSE;
        object dot = await base.fully_replace_character_name(oldname, newname, log_new_name);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(oldname))
            notify_ai(AI_NOTIFICATION_CYBORG_RENAMED, oldname, newname);
        if (!QDELETED(builtInCamera))
        {
            builtInCamera.c_tag = real_name;
            modularInterface.imprint_id(name: real_name);
        }
        custom_name = Dm.str(newname);
        mmi?.set_name(newname);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:692
    public virtual async DmTask<object> ResetModel()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_BORG_SAFE_DECONSTRUCT]))
            _SendSignal(COMSIG_BORG_SAFE_DECONSTRUCT, Dm.list(this));
        await drop_all_held_items();

        foreach (obj_item_storage bag in Dm.each<obj_item_storage>(model.contents)) // drop all of the items that may be stored by the cyborg
        {
            foreach (obj_item item in Dm.each<obj_item>(bag))
                await item.forceMove(drop_location() as atom);
        }

        if (Dm.truthy(hasExpanded))
        {
            hasExpanded = FALSE;
            await update_transform(0.5f);
        }
        await logevent("Chassis model has been reset.");
        log_silicon(Dm.text("CYBORG: [] has reset their cyborg model.", key_name(this)));
        await model.transform_to(typeof(obj_item_robot_model));

        // Remove upgrades.
        foreach (obj_item_borg_upgrade I in Dm.each<obj_item_borg_upgrade>(upgrades))
            await I.forceMove(get_turf(this));

        ionpulse = FALSE;
        await revert_shell();

        return TRUE;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:716
    public virtual object has_model()
    {
        if (model == null || model.GetType() == typeof(obj_item_robot_model))
            return FALSE;
        else
            return TRUE;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:722
    public virtual void update_module_innate()
    {
        designation = model.name;
        if ((hud_used?.screen_objects?[HUD_CYBORG_HANDS]) is atom hands)
            hands.icon_state = model.model_select_icon;

        REMOVE_TRAITS_IN(this, MODEL_TRAIT);
        if (Dm.truthy(length(model.model_traits)))
            add_traits(model.model_traits, MODEL_TRAIT);

        hat_offset = model.hat_offset;
        Waiting.Detach(updatename()); // INVOKE_ASYNC
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:733
    public virtual async DmTask place_on_head(obj_item new_hat = null)
    {
        if (hat != null)
            await hat.forceMove(get_turf(this));
        hat = new_hat;
        await new_hat.forceMove(this);
        await update_icons();
    }

    /**
        *Checking Exited() to detect if a hat gets up and walks off.
        *Drones and pAIs might do this, after all.
    */
    // tg: code/modules/mob/living/silicon/robot/robot.dm:744
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (hat == gone)
        {
            hat = null;
            if (!QDELETED(this)) //Don't update icons if we are deleted.
                await update_icons();
        }

        if (gone == cell)
            cell = null;

        if (gone == mmi)
            mmi = null;
        return dot;
    }

    ///Called when a mob uses an upgrade on an open borg. Checks to make sure the upgrade can be applied
    // tg: code/modules/mob/living/silicon/robot/robot.dm:758
    public virtual async DmTask<object> apply_upgrade(obj_item_borg_upgrade new_upgrade = null, mob user = null)
    {
        if (user == null)
            return FALSE;
        if (Dm.@in(new_upgrade, upgrades))
            return FALSE;
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(new_upgrade))) //calling the upgrade's dropped() proc /before/ we add action buttons
            return FALSE;
        if (!Dm.truthy(await new_upgrade.action(this, user as mob_living)))
        {
            to_chat(user, span_danger(I18n.T("Upgrade error.")));
            await new_upgrade.forceMove(loc); //gets lost otherwise
            return FALSE;
        }
        to_chat(user, span_notice(I18n.T("You apply the upgrade to [].", this)));
        await add_to_upgrades(new_upgrade);
        return null;
    }

    ///Moves the upgrade inside the robot and registers relevant signals.
    // tg: code/modules/mob/living/silicon/robot/robot.dm:773
    public virtual async DmTask<object> add_to_upgrades(obj_item_borg_upgrade new_upgrade = null)
    {
        to_chat(this, I18n.T("----------------\nNew hardware detected...Identified as \"<b>[]</b>\"...Setup complete.\n----------------", new_upgrade));
        if (Dm.truthy(new_upgrade.one_use))
        {
            await logevent(Dm.text("Firmware [] run successfully.", new_upgrade));
            qdel(new_upgrade);
            return FALSE;
        }
        Dm.append(ref upgrades, new_upgrade);
        await new_upgrade.forceMove(this);
        RegisterSignal(new_upgrade, COMSIG_MOVABLE_MOVED, nameof(remove_from_upgrades));
        RegisterSignal(new_upgrade, COMSIG_QDELETING, nameof(on_upgrade_deleted));
        await logevent(Dm.text("Hardware [] installed successfully.", new_upgrade));
        return null;
    }

    ///Called when an upgrade is moved outside the robot. So don't call this directly, use forceMove etc.
    // tg: code/modules/mob/living/silicon/robot/robot.dm:786
    [SignalHandler]
    public virtual void remove_from_upgrades(obj_item_borg_upgrade old_upgrade = null)
    {
        if (loc == this)
            return;
        Waiting.NoSleep(old_upgrade.deactivate(this));
        Dm.remove(ref upgrades, old_upgrade);
        UnregisterSignal(old_upgrade, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_QDELETING));
    }

    ///Called when an applied upgrade is deleted.
    // tg: code/modules/mob/living/silicon/robot/robot.dm:795
    [SignalHandler]
    public virtual void on_upgrade_deleted(obj_item_borg_upgrade old_upgrade = null)
    {
        if (!QDELETED(this))
            Waiting.NoSleep(old_upgrade.deactivate(this));
        Dm.remove(ref upgrades, old_upgrade);
        UnregisterSignal(old_upgrade, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_QDELETING));
    }

    /**
     * make_shell: Makes an AI shell out of a cyborg unit
     *
     * Arguments:
     * * board - B.O.R.I.S. module board used for transforming the cyborg into AI shell
     */
    // tg: code/modules/mob/living/silicon/robot/robot.dm:808
    public virtual async DmTask<object> make_shell(obj_item_borg_upgrade_ai board = null)
    {
        if (board == null)
        {
            stack_trace("make_shell was called without a board argument! This is never supposed to happen!", "code/modules/mob/living/silicon/robot/robot.dm", 810);
            return FALSE;
        }

        shell = TRUE;
        braintype = "AI Shell";
        name = Dm.text("Empty AI Shell-[]", identifier);
        real_name = name;
        Dm.combine(ref GLOB.available_ai_shells, this);
        if (!QDELETED(builtInCamera))
            builtInCamera.c_tag = Dm.str(real_name); //update the camera name too
        diag_hud_set_aishell();
        if (Dm.truthy(lamp_enabled))
            await toggle_headlamp(TRUE);
        await update_icons();
        return null;
    }

    /// Is this an AI shell with no AI currently piloting it?
    // tg: code/modules/mob/living/silicon/robot/robot.dm:826
    public virtual object is_empty_shell()
    {
        return Dm.truthy(shell) && !Dm.truthy(deployed) ? TRUE : FALSE;
    }

    /**
     * revert_shell: Reverts AI shell back into a normal cyborg unit
     */
    // tg: code/modules/mob/living/silicon/robot/robot.dm:832
    public virtual async DmTask revert_shell()
    {
        if (!Dm.truthy(shell))
            return;
        await undeploy();
        foreach (obj_item_borg_upgrade_ai boris in Dm.each<obj_item_borg_upgrade_ai>(this))
        //A player forced reset of a borg would drop the module before this is called, so this is for catching edge cases
            qdel(boris);
        shell = FALSE;
        Dm.remove(ref GLOB.available_ai_shells, this);
        name = Dm.text("Unformatted Cyborg-[]", identifier);
        real_name = name;
        if (!QDELETED(builtInCamera))
            builtInCamera.c_tag = real_name;
        diag_hud_set_aishell();
        await update_icons();
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:926
    public override async DmTask<object> mouse_buckle_handling(mob_living M = null, mob_living user = null)
    {
        //Don't try buckling on INTENT_HARM so that silicons can search people's inventories without loading them
        if (Dm.truthy(can_buckle) && isliving(user) && isliving(M) && !Dm.@in(M, buckled_mobs) && ((user != this) || !Dm.truthy(combat_mode)))
            return await user_buckle_mob(M, user, check_loc: FALSE);
        return null;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:931
    public override async DmTask<object> is_buckle_possible(mob_living target = null, float? force = null, float? check_loc = null)
    {
        if (Dm.truthy(incapacitated))
            return FALSE;
        if (!HAS_TRAIT(target, TRAIT_CAN_MOUNT_CYBORGS))
        {
            target.visible_message(span_warning(I18n.T("[] really can't seem to mount []...", target, this)));
            return FALSE;
        }
        if (model != null && !Dm.truthy(model.allow_riding))
        {
            target.visible_message(span_boldwarning(I18n.T("Unfortunately, [] just can't seem to hold onto []!", target, this)));
            return FALSE;
        }

        return await base.is_buckle_possible(target, force, check_loc);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:943
    public override async DmTask<object> buckle_mob(mob_living M = null, float? force = null, float? check_loc = null, float? buckle_mob_flags = null)
    {
        buckle_mob_flags = RIDER_NEEDS_ARM; // just in case
        return await base.buckle_mob(M, force, check_loc, buckle_mob_flags);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:947
    public override async DmTask post_buckle_mob(mob_living victim_to_boot = null)
    {
        if (HAS_TRAIT(this, TRAIT_GOT_DAMPENED))
            await eject_riders();
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:951
    public override object can_resist()
    {
        if (Dm.truthy(lockcharge))
        {
            balloon_alert(this, I18n.T("locked down!"));
            return FALSE;
        }
        return base.can_resist();
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:957
    public override async DmTask execute_resist()
    {
        await base.execute_resist();
        if (!Dm.truthy(has_buckled_mobs()))
            return;
        foreach (mob unbuckle_me_now in Dm.each_cast<mob>(buckled_mobs.Copy()))
            await unbuckle_mob(unbuckle_me_now as mob_living, FALSE);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:964
    public virtual async DmTask<object> try_connect_to_ai(mob_living_silicon connect_to = null)
    {
        if (connect_to == null)
        {
            turf robot_turf = get_turf(this);
            connect_to = select_active_ai_with_fewest_borgs(robot_turf.z) as mob_living_silicon;
            if (connect_to == null)
                return FALSE;
        }

        await set_connected_ai(connect_to);
        if (connected_ai != null)
        {
            await sync_to_ai();
            lawupdate = TRUE;
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:979
    [SignalHandler]
    public virtual void charge(datum source = null, datum_callback charge_cell = null, object seconds_per_tick = null, object repairs = null, object sendmats = null)
    {
        if (model != null)
        {
            if (Dm.truthy(cell__obj.charge))
            {
                if (Dm.truthy(Waiting.NoSleep(model.respawn_consumable(this, Dm.num(cell__obj.charge) * 0.005f))))
                    Waiting.NoSleep(cell__obj.use(Dm.num(cell__obj.charge) * 0.005f));
            }
            if (Dm.truthy(sendmats))
                Waiting.NoSleep(model.restock_consumable());
        }
        if (Dm.truthy(repairs))
            Waiting.NoSleep(heal_bodypart_damage(Dm.num(repairs), Dm.num(repairs)));
        Waiting.NoSleep(charge_cell.Invoke(cell, seconds_per_tick));
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:992
    public virtual async DmTask set_connected_ai(object new_ai = null)
    {
        if (connected_ai == new_ai)
            return;
        mob_living_silicon_ai old_ai = connected_ai;
        connected_ai = new_ai as mob_living_silicon_ai;
        if (old_ai != null)
        {
            Dm.remove(ref old_ai.connected_robots, this);
            // if the borg has a malf AI zeroth law and has been unsynced from the malf AI, then remove the law
            if (connected_ai == null && Dm.truthy(IS_MALF_AI(old_ai)) && laws?.zeroth != null)
                laws.clear_zeroth_law(force: TRUE);
        }
        lamp_doom = FALSE;
        if (connected_ai != null)
        {
            Dm.combine(ref connected_ai.connected_robots, this);
            lamp_doom = connected_ai.doomsday_device != null ? TRUE : FALSE;
            await try_sync_laws();
        }
        await toggle_headlamp(FALSE, TRUE);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:1009
    public override object get_exp_list(object minutes = null)
    {
        DmList dot = base.get_exp_list(minutes) as DmList;
        dot[Dm.initial(typeof(datum_job_cyborg), "title")] = minutes;
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:1013
    public virtual void untip_roleplay()
    {
        to_chat(this, span_notice(I18n.T("Your frustration has empowered you! You can now right yourself faster!")));
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:1016
    public override object get_fire_overlay(object stacks = null, object on_fire = null)
    {
        return make_generic_fire_overlay();
    }

    /// Draw power from the robot
    // tg: code/modules/mob/living/silicon/robot/robot.dm:1020
    public virtual async DmTask draw_power(object power_to_draw = null)
    {
        if (cell != null)
            await cell__obj.use(power_to_draw);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:1023
    [SignalHandler]
    public virtual void on_dampen()
    {
        Waiting.NoSleep(eject_riders());
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:1027
    public virtual async DmTask eject_riders()
    {
        if (!Dm.truthy(length(buckled_mobs)))
            return;
        foreach (mob_living buckled_mob in Dm.each_cast<mob_living>(buckled_mobs.Copy()))
        {
            buckled_mob.visible_message(span_warning(I18n.T("[] is knocked off of [] by the charge in []'s chassis induced by the hyperkinetic dampener field!", buckled_mob, this, this)));
            await buckled_mob.Paralyze(1 * SECONDS);
            await unbuckle_mob(buckled_mob);
        }
        do_sparks(5, 0, this);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:1036
    public override object get_unconscious_appearance()
    {
        image static_overlay = image(Dm.resource("icons/effects/effects.dmi"), null, "static_base");
        static_overlay.blend_mode = BLEND_INSET_OVERLAY;

        image static_image = image(Dm.resource("icons/mob/silicon/robots.dmi"), this, "robot");
        static_image.appearance_flags = Dm.bitor(static_image.appearance_flags, KEEP_TOGETHER);
        Dm.append(ref static_image.overlays, static_overlay);
        static_image.@override = TRUE;
        static_image.name = "unknown cyborg";

        return static_image;
    }
}

// tg: code/modules/mob/living/silicon/robot/robot.dm:93  (the proc's static alert_areas, rules 7.6)
public sealed partial class TgState
{
    internal DmList mob_living_silicon_robot__Initialize__alert_areas;
}

// tg: code/modules/mob/living/silicon/robot/robot.dm:854
public partial class mob_living_silicon_robot : mob_living_silicon
{
    /**
     * deploy_init: Deploys AI unit into AI shell
     *
     * Arguments:
     * * AI - AI unit that initiated the deployment into the AI shell
     */
    // tg: code/modules/mob/living/silicon/robot/robot.dm:854
    public virtual async DmTask deploy_init(mob_living_silicon_ai AI = null)
    {
        real_name = Dm.text("[] [] Shell-[]", AI.real_name, designation, identifier);
        name = Dm.str(real_name);
        if (!QDELETED(builtInCamera))
            builtInCamera.c_tag = Dm.str(real_name); //update the camera name too
        mainframe = AI;
        deployed = TRUE;
        if (AI.client != null)
            set_gender(AI.client);
        await set_connected_ai(mainframe);
        Dm.combine(ref mainframe.connected_robots, this);
        lawupdate = TRUE;
        await lawsync();
        if (radio is obj_item_radio shell_radio && AI.radio is obj_item_radio ai_radio) //AI keeps all channels, including Syndie if it is a Traitor
        {
            if (Dm.truthy(Dm.bitand(ai_radio.special_channels, RADIO_SPECIAL_SYNDIE)))
                shell_radio.make_syndie();
            shell_radio.subspace_transmission = TRUE;
            shell_radio.command = TRUE;
            shell_radio.channels = ai_radio.channels;
            foreach (object chan in Dm.each(shell_radio.channels))
                LAZYSET(ref shell_radio.secure_radio_connections, chan, add_radio(shell_radio, GLOB.default_radio_channels[chan]));
        }

        diag_hud_set_aishell();
        await update_icons();
        await undeployment_action.Grant(this);
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:895
    public virtual async DmTask undeploy()
    {
        if (!Dm.truthy(deployed) || mind == null || mainframe == null)
            return;
        mainframe.UnregisterSignal(this, COMSIG_LIVING_DEATH);
        await mainframe.redeploy_action.Grant(mainframe);
        mainframe.redeploy_action.last_used_shell = this;
        await mind.transfer_to(mainframe);
        deployed = FALSE;
        mainframe.deployed_shell = null;
        await undeployment_action.Remove(this);
        if (Dm.truthy(lamp_enabled))
            await toggle_headlamp(TRUE);
        await update_icons();
        REMOVE_TRAIT(this, TRAIT_LOUD_BINARY, REF(mainframe));
        if (radio is obj_item_radio shell_radio) //Return radio to normal
            shell_radio.recalculateChannels();
        if (!QDELETED(builtInCamera))
            builtInCamera.c_tag = Dm.str(real_name); //update the camera name too
        diag_hud_set_aishell();
        mainframe.diag_hud_set_deployed();
        if (mainframe.laws != null)
            mainframe.laws.show_laws(mainframe); //Always remind the AI when switching
        if (mainframe.eyeobj != null)
            mainframe.eyeobj.setLoc(loc);
        mainframe = null;
    }

    // tg: code/modules/mob/living/silicon/robot/robot.dm:921
    public override async DmTask<object> attack_ai(mob user = null)
    {
        if (Dm.truthy(shell) && (connected_ai == null || connected_ai == user))
        {
            mob_living_silicon_ai AI = user as mob_living_silicon_ai;
            await AI.select_shell(this);
        }
        return null;
    }
}

// tg: code/modules/mob/living/silicon/robot/robot.dm:880
public partial class datum_action_innate_undeployment : datum_action_innate
{
    // tg: code/modules/mob/living/silicon/robot/robot.dm:886
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        if (!Dm.truthy(await base.Trigger(clicker, trigger_flags)))
            return FALSE;
        mob_living_silicon_robot shell_to_disconnect = owner as mob_living_silicon_robot;

        await shell_to_disconnect.undeploy();
        return TRUE;
    }
}

