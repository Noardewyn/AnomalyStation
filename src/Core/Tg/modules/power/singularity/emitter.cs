// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/singularity/emitter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the emitter and the welded emitter (the M7 zaps slice: MetaStation's engine room has five), with their power through
// the grid, the lock with an ID, the anchoring and welding to the floor, their sounds and overlays, and the shot, fire_beam (a
// hitscan /obj/projectile/beam/emitter/hitscan through the projectile base: the M7 projectiles slice); an energy gun in the open
// emitter, the AI's lock, the emag, and the prototype emitter with its gunner (2026-11-05). The CTF emitter and the diode disks stay
// inert. The var blocks are
// generated (D111).

// tg: code/modules/power/singularity/emitter.dm:1
public partial class obj_machinery_power_emitter : obj_machinery_power
{
    // tg: code/modules/power/singularity/emitter.dm:67
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        //Add to the early process queue to prioritize power draw
        // rules 10.6: tg's Master creates SSmachines before the map; a test harness world may lack it.
        if (SSmachines != null)
            Dm.append(ref SSmachines.processing_early, this);
        set_wires(Dm.@new<datum_wires_emitter>(this));
        if (Dm.truthy(welded))
        {
            if (!Dm.truthy(anchored))
                Waiting.NoSleep(set_anchored(TRUE));
            connect_to_network();
        }

        sparks = Dm.@new<datum_effect_system_basic_spark_spread>(this, 5, TRUE);
        sparks.attach(this);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_SELF, EMP_PROTECT_WIRES)));
        // AddComponent(/datum/component/usb_port, typecacheof(list(/obj/item/circuit_component/emitter), only_root_path = TRUE)): the
        // integrated circuits' port (wiremod, after the core version)
        Dm.Deferred("code/modules/power/singularity/emitter.dm:81", "Initialize: the usb_port component of the integrated circuits (wiremod, M10+)");
        return dot;
    }

    // tg: code/modules/power/singularity/emitter.dm:87
    public override async DmTask<object> cable_layer_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(panel_open))
            return NONE;
        if (Dm.truthy(welded))
        {
            balloon_alert(user, I18n.T("unweld first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.cable_layer_act(user, tool);
    }

    // tg: code/modules/power/singularity/emitter.dm:95
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = null;
        dot = await base.set_anchored(anchorvalue);
        if (!Dm.truthy(anchored) && Dm.truthy(welded)) //make sure they're keep in sync in case it was forcibly unanchored by badmins or by a megafauna.
            welded = FALSE;
        return dot;
    }

    // tg: code/modules/power/singularity/emitter.dm:100
    public override async DmTask<object> RefreshParts()
    {
        object dot = null;
        dot = await base.RefreshParts();
        float max_fire_delay = 12 * SECONDS;
        float fire_shoot_delay = 12 * SECONDS;
        float min_fire_delay = 2.4f * SECONDS;
        float power_usage = 350;
        foreach (datum_stock_part_micro_laser laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
        {
            max_fire_delay -= 2 * SECONDS * Dm.num(laser.tier);
            min_fire_delay -= 0.4f * SECONDS * Dm.num(laser.tier);
            fire_shoot_delay -= 2 * SECONDS * Dm.num(laser.tier);
        }
        maximum_fire_delay = max_fire_delay;
        minimum_fire_delay = min_fire_delay;
        fire_delay = fire_shoot_delay;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            power_usage -= 50 * Dm.num(servo.tier);
        update_mode_power_usage(ACTIVE_POWER_USE, power_usage);
        return dot;
    }

    // tg: code/modules/power/singularity/emitter.dm:117
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(welded))
            Dm.append(ref dot, span_info(I18n.T("It's moored firmly to the floor. You can unsecure its moorings with a <b>welder</b>.")));
        else if (Dm.truthy(anchored))
            Dm.append(ref dot, span_info(I18n.T("It's currently anchored to the floor. You can secure its moorings with a <b>welder</b>, or remove it with a <b>wrench</b>.")));
        else
            Dm.append(ref dot, span_info(I18n.T("It's not anchored to the floor. You can secure it in place with a <b>wrench</b>.")));

        if (!Dm.truthy(in_range(user, this)) && !isobserver(user))
            return dot;

        if (!Dm.truthy(active))
            Dm.append(ref dot, span_notice(I18n.T("Its status display is currently turned off.")));
        else if (!Dm.truthy(powered))
            Dm.append(ref dot, span_notice(I18n.T("Its status display is glowing faintly.")));
        else
        {
            Dm.append(ref dot, span_notice(I18n.T("Its status display reads: Emitting one beam between <b>[]</b> and <b>[]</b>.", DisplayTimeText(minimum_fire_delay * fire_rate_mod), DisplayTimeText(maximum_fire_delay * fire_rate_mod))));
            Dm.append(ref dot, span_notice(I18n.T("Power consumption at <b>[]</b>.", display_power(active_power_usage, convert: FALSE))));
        }
        return dot;
    }

    // tg: code/modules/power/singularity/emitter.dm:137
    public override object should_have_node()
    {
        return welded;
    }

    // tg: code/modules/power/singularity/emitter.dm:140
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(SSticker?.IsRoundInProgress()))
        {
            var T = get_turf(this) as turf;
            message_admins(Dm.text("[] deleted at [].", this, ADMIN_VERBOSEJMP(T)));
            log_game(Dm.text("[] deleted at [].", this, AREACOORD(T)));
            investigate_log(Dm.text("deleted at [].", AREACOORD(T)), INVESTIGATE_ENGINE);
        }
        QDEL_NULL(ref sparks);
        return base.Destroy(force);
    }

    // tg: code/modules/power/singularity/emitter.dm:149
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (!Dm.truthy(active))
            return dot;
        string laser_color = COLOR_VIBRANT_LIME;
        if (!Dm.truthy(powered))
            laser_color = COLOR_ORANGE; //stank low power orange
        else if (Dm.truthy(diskie))
            laser_color = diskie.laser_color;
        mutable_appearance overlay = mutable_appearance(icon, "emitter_overlay") as mutable_appearance;
        overlay.color = laser_color;
        Dm.append(ref dot, overlay);
        // . += emissive_appearance(icon, "emitter_overlay", src, alpha = src.alpha): DEVIATION(DV12), the glow on the emissive plane
        return dot;
    }

    // tg: code/modules/power/singularity/emitter.dm:163
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(panel_open))
            icon_state = Dm.text("[]_open", base_icon_state);
        else
            icon_state = base_icon_state as string;
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/power/singularity/emitter.dm:170
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (!Dm.truthy(welded))
        {
            to_chat(user, span_warning(I18n.T("[] needs to be firmly secured to the floor first!", this)));
            return FALSE;
        }
        if (!Dm.truthy(powernet))
        {
            to_chat(user, span_warning(I18n.T("\\The [] isn't connected to a wire!", this)));
            return FALSE;
        }
        if (Dm.truthy(locked) || !Dm.truthy(allow_switch_interact))
        {
            to_chat(user, span_warning(I18n.T("The controls are locked!")));
            return FALSE;
        }

        if (Dm.truthy(active))
            active = FALSE;
        else
        {
            active = TRUE;
            shot_number = 0;
            fire_delay = maximum_fire_delay;
        }

        to_chat(user, span_notice(I18n.T("You turn [] [].", Dm.truthy(active) ? "on" : "off", this)));
        message_admins(Dm.text("[] turned [] by [] in []", this, Dm.truthy(active) ? "ON" : "OFF", ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(this)));
        log_game(Dm.text("[] turned [] by [] in []", this, Dm.truthy(active) ? "ON" : "OFF", key_name(user), AREACOORD(this)));
        investigate_log(Dm.text("turned [] by [] at []", Dm.truthy(active) ? "ON" : "OFF", key_name(user), AREACOORD(this)), INVESTIGATE_ENGINE);
        update_appearance();
        if (Dm.truthy(this._listen_lookup?[COMSIG_EMITTER_MACHINE_SET_ON]))
            this._SendSignal(COMSIG_EMITTER_MACHINE_SET_ON, Dm.list(this, Dm.truthy(active) ? TRUE : FALSE));
        return null;
    }

    // tg: code/modules/power/singularity/emitter.dm:196
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        object dot = null;
        if (ismegafauna(user) && Dm.truthy(anchored))
        {
            await set_anchored(FALSE);
            user.visible_message(span_warning(I18n.T("[] rips [] free from its moorings!", user, this)));
        }
        else
            dot = await base.attack_animal(user, modifiers);
        if (Dm.truthy(dot) && !Dm.truthy(anchored))
            await step(this, get_dir(user, this));
        return dot;
    }

    // tg: code/modules/power/singularity/emitter.dm:209
    public override object process_early(params object[] __extra)
    {
        object seconds_per_tick = Dm.arg(__extra, 0);
        float power_usage = active_power_usage * Dm.num(seconds_per_tick);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return null;
        if (!Dm.truthy(welded) || (!Dm.truthy(powernet) && Dm.truthy(power_usage)))
        {
            active = FALSE;
            update_appearance();
            return null;
        }
        if (!Dm.truthy(active))
            return null;
        if (Dm.truthy(power_usage) && Dm.num(surplus()) < power_usage)
        {
            if (Dm.truthy(powered))
            {
                powered = FALSE;
                update_appearance();
                investigate_log(Dm.text("lost power and turned OFF at []", AREACOORD(this)), INVESTIGATE_ENGINE);
                log_game(Dm.text("[] lost power in []", this, AREACOORD(this)));
            }
            return null;
        }

        add_load(power_usage);
        if (!Dm.truthy(powered))
        {
            powered = TRUE;
            update_appearance();
            investigate_log(Dm.text("regained power and turned ON at []", AREACOORD(this)), INVESTIGATE_ENGINE);
        }
        if (charge <= 80)
            charge += 2.5f * Dm.num(seconds_per_tick);
        if (!Dm.truthy(check_delay()) || manual == TRUE)
            return FALSE;
        Waiting.NoSleep(fire_beam());
        return null;
    }

    // tg: code/modules/power/singularity/emitter.dm:238
    public virtual object check_delay()
    {
        if ((last_shot + fire_delay) <= world.time)
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/power/singularity/emitter.dm:243
    public virtual async DmTask<object> fire_beam_pulse()
    {
        if (!Dm.truthy(check_delay()))
            return FALSE;
        if (!Dm.truthy(welded))
            return FALSE;
        if (Dm.num(surplus()) >= active_power_usage)
        {
            add_load(active_power_usage);
            await fire_beam();
        }
        return null;
    }

    // tg: code/modules/power/singularity/emitter.dm:252
    public virtual async DmTask<object> fire_beam(mob user = null)
    {
        // The M7 projectiles slice
        obj_projectile projectile = Dm.@new(projectile_type, get_turf(this)) as obj_projectile;
        playsound(this, projectile_sound, 50, TRUE);
        if (prob(35))
            await sparks.start();
        projectile.firer = (Dm.truthy(user) ? (atom_movable)user : this);
        projectile.fired_from = this;
        if (Dm.truthy(last_projectile_params))
        {
            projectile.p_x = Dm.num_or_null(((DmList)last_projectile_params)[2]);
            projectile.p_y = Dm.num_or_null(((DmList)last_projectile_params)[3]);
            await projectile.fire(((DmList)last_projectile_params)[1]);
        }
        else
            await projectile.fire(dir2angle(dir));
        if (!Dm.truthy(manual))
        {
            last_shot = world.time;
            if (shot_number < 3 || Dm.truthy(no_shot_counter))
            {
                fire_delay = 20 * fire_rate_mod;
                shot_number++;
            }
            else
            {
                fire_delay = rand(minimum_fire_delay, maximum_fire_delay) * fire_rate_mod;
                shot_number = 0;
            }
        }
        if (Dm.truthy(_listen_lookup?[COMSIG_EMITTER_MACHINE_ON_FIRE]))
            _SendSignal(COMSIG_EMITTER_MACHINE_ON_FIRE, Dm.list(this));
        return projectile;
    }

    // tg: code/modules/power/singularity/emitter.dm:276
    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        if (Dm.truthy(active))
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("Turn \\the [] off first!", this)));
            return FAILED_UNFASTEN;
        }

        else if (Dm.truthy(welded))
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("[] is welded to the floor!", this)));
            return FAILED_UNFASTEN;
        }

        return base.can_be_unfasten_wrench(user, silent);
    }

    // tg: code/modules/power/singularity/emitter.dm:289
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = null;
        dot = await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/singularity/emitter.dm:294
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        obj_item item = tool;
        await base.welder_act(user, item);
        if (Dm.truthy(active))
        {
            to_chat(user, span_warning(I18n.T("Turn [] off first!", this)));
            return TRUE;
        }

        if (Dm.truthy(welded))
        {
            if (!Dm.truthy(item.tool_start_check(user, amount: 1)))
                return TRUE;
            user.visible_message(span_notice(I18n.T("[] starts to cut \\the [] free from the floor.", user.name, this)),
                span_notice(I18n.T("You start to cut [] free from the floor...", this)),
                span_hear(I18n.T("You hear welding.")));
            if (!Dm.truthy(await item.use_tool(this, user, 20, 1, 50)))
                return FALSE;
            welded = FALSE;
            to_chat(user, span_notice(I18n.T("You cut [] free from the floor.", this)));
            disconnect_from_network();
            update_cable_icons_on_turf(get_turf(this));
            return TRUE;
        }

        if (!Dm.truthy(anchored))
        {
            to_chat(user, span_warning(I18n.T("[] needs to be wrenched to the floor!", this)));
            return TRUE;
        }
        if (!Dm.truthy(item.tool_start_check(user, amount: 1)))
            return TRUE;
        user.visible_message(span_notice(I18n.T("[] starts to weld \\the [] to the floor.", user.name, this)),
            span_notice(I18n.T("You start to weld [] to the floor...", this)),
            span_hear(I18n.T("You hear welding.")));
        if (!Dm.truthy(await item.use_tool(this, user, 20, 1, 50)))
            return FALSE;
        welded = TRUE;
        to_chat(user, span_notice(I18n.T("You weld [] to the floor.", this)));
        connect_to_network();
        update_cable_icons_on_turf(get_turf(this));
        return TRUE;
    }

    // tg: code/modules/power/singularity/emitter.dm:330
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        obj_item item = tool;
        if (Dm.truthy(panel_open) && Dm.truthy(gun))
            return await remove_gun(user);
        if (Dm.truthy(panel_open) && Dm.truthy(diskie))
            return await remove_disk(user);
        return await default_deconstruction_crowbar(user, item);
    }

    // tg: code/modules/power/singularity/emitter.dm:337
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    /// Attempt to toggle the controls lock of the emitter
    // tg: code/modules/power/singularity/emitter.dm:341
    public virtual void togglelock(mob user = null)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            to_chat(user, span_warning(I18n.T("The lock seems to be broken!")));
            return;
        }
        if (!Dm.truthy(Waiting.NoSleep(allowed__proc(user))))
        {
            to_chat(user, span_danger(I18n.T("Access denied.")));
            return;
        }
        if (!Dm.truthy(active))
        {
            to_chat(user, span_warning(I18n.T("The controls can only be locked when \\the [] is online!", this)));
            return;
        }
        locked = Dm.num(!Dm.truthy(locked));
        to_chat(user, span_notice(I18n.T("You [] the controls.", Dm.truthy(this.locked) ? "lock" : "unlock")));
    }

    // tg: code/modules/power/singularity/emitter.dm:354
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(tool.GetID()))
        {
            togglelock(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(panel_open))
            return NONE;

        if (Dm.truthy(is_wire_tool(tool)))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(gun))
            return NONE;

        if (istype(tool, typeof(obj_item_gun_energy)))
        {
            if (Dm.truthy(diskie))
            {
                to_chat(user, span_warning(I18n.T("Remove the Diode Disk before inserting a gun.")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await integrate(tool as obj_item_gun_energy, user)))
                return ITEM_INTERACT_BLOCKING;
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_emitter_disk)))
        {
            obj_item_emitter_disk config_disk = (obj_item_emitter_disk)tool;
            if (!Dm.truthy(await user.transferItemToLoc(config_disk, this)))
            {
                balloon_alert(user, I18n.T("stuck in hand!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(diskie))
            {
                await user.put_in_hands(diskie);
                balloon_alert(user, I18n.T("disks swapped!"));
            }
            else
                balloon_alert(user, I18n.T("disk inserted"));
            diskie = config_disk;
            projectile_type = diskie.stored_proj;
            projectile_sound = diskie.stored_sound;
            fire_rate_mod = diskie.fire_rate_mod;
            no_shot_counter = diskie.no_shot_counter;
            playsound(this, Dm.resource("sound/machines/card_slide.ogg"), 50);
            to_chat(user, span_notice(I18n.T("You update the []'s diode configuration with the [].", this, config_disk)));
            update_appearance();
            if (Dm.truthy(diskie.consumable))
                qdel(diskie);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/power/singularity/emitter.dm:402
    public virtual async DmTask<object> integrate(obj_item_gun_energy energy_gun = null, mob user = null)
    {
        if (!istype(energy_gun, typeof(obj_item_gun_energy)))
            return null;
        if (!Dm.truthy(await user.transferItemToLoc(energy_gun, this)))
            return null;
        if (Dm.truthy(Dm.bitand(energy_gun.gun_flags, TURRET_INCOMPATIBLE)))
        {
            user.balloon_alert(user, I18n.T("[] won't fit!", energy_gun));
            return null;
        }
        gun = energy_gun;
        gun_properties = gun.get_turret_properties() as DmList;
        set_projectile();
        return TRUE;
    }

    // tg: code/modules/power/singularity/emitter.dm:205
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        togglelock(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/power/singularity/emitter.dm:453
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        locked = FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        balloon_alert(user, I18n.T("id lock shorted out"));
        return TRUE;
    }

    // tg: code/modules/power/singularity/emitter.dm:415
    public virtual async DmTask<object> remove_gun(mob user = null)
    {
        if (!Dm.truthy(gun))
            return null;
        await user.put_in_hands(gun);
        gun = null;
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        gun_properties = Dm.list();
        set_projectile();
        return TRUE;
    }

    // tg: code/modules/power/singularity/emitter.dm:425
    public virtual async DmTask<object> remove_disk(mob user = null)
    {
        if (!Dm.truthy(diskie))
            return null;
        if (Dm.truthy(diskie.consumed_on_removal))
            qdel(diskie);
        else
            await user.put_in_hands(diskie);
        diskie = null;
        playsound(this, Dm.resource("sound/machines/card_slide.ogg"), 50, TRUE);
        update_appearance();
        set_projectile();
        return TRUE;
    }

    // tg: code/modules/power/singularity/emitter.dm:439
    public virtual void set_projectile()
    {
        if (Dm.truthy(LAZYLEN(gun_properties)))
        {
            if (Dm.truthy(mode) || !Dm.truthy(gun_properties["lethal_projectile"]))
            {
                projectile_type = gun_properties["stun_projectile"] as Type;
                projectile_sound = gun_properties["stun_projectile_sound"];
            }
            else
            {
                projectile_type = gun_properties["lethal_projectile"] as Type;
                projectile_sound = gun_properties["lethal_projectile_sound"];
            }
            return;
        }
        projectile_type = Dm.initial(this, (obj_machinery_power_emitter p) => p.projectile_type);
        projectile_sound = Dm.initial(this, (obj_machinery_power_emitter p) => p.projectile_sound);
        fire_rate_mod = Dm.initial(this, (obj_machinery_power_emitter p) => p.fire_rate_mod);
        no_shot_counter = Dm.initial(this, (obj_machinery_power_emitter p) => p.no_shot_counter);
    }
}

// tg: code/modules/power/singularity/emitter.dm:83
public partial class obj_machinery_power_emitter_welded : obj_machinery_power_emitter
{
    // tg: code/modules/power/singularity/emitter.dm:83
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        welded = TRUE;
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        return dot;
    }
}

// tg: code/modules/power/singularity/emitter.dm:463
public partial class obj_machinery_power_emitter_prototype : obj_machinery_power_emitter
{
    //BUCKLE HOOKS

    // tg: code/modules/power/singularity/emitter.dm:478
    public override async DmTask<object> unbuckle_mob(mob_living buckled_mob = null, float? force = null, float? can_fall = null)
    {
        playsound(this, "sound/vehicles/mecha/mechmove01.ogg", 50, TRUE);
        manual = FALSE;
        foreach (obj_item item in Dm.each<obj_item>(buckled_mob.held_items))
        {
            if (istype(item, typeof(obj_item_turret_control)))
                qdel(item);
        }
        if (istype(buckled_mob, typeof(mob_living)))
        {
            buckled_mob.pixel_x = buckled_mob.base_pixel_x;
            buckled_mob.pixel_y = buckled_mob.base_pixel_y;
            // DEVIATION(DV349): tg's client.view_size.resetToDefault(): a first-person view has no top-down view size to widen
        }
        await auto.Remove(buckled_mob);
        return await base.unbuckle_mob(buckled_mob, force, can_fall);
    }

    // tg: code/modules/power/singularity/emitter.dm:492
    public override async DmTask<object> user_buckle_mob(mob_living buckled_mob = null, mob user = null, float? check_loc = null)
    {
        if (Dm.truthy(user.incapacitated) || !istype(user, typeof(mob)))
            return null;
        foreach (atom_movable atom in Dm.each<atom_movable>(get_turf(this)))
        {
            if (Dm.truthy(atom.density) && (atom != this && atom != buckled_mob))
                return null;
        }
        await buckled_mob.forceMove(get_turf(this));
        await base.user_buckle_mob(buckled_mob, user, check_loc);
        playsound(this, "sound/vehicles/mecha/mechmove01.ogg", 50, TRUE);
        buckled_mob.pixel_y = 14;
        layer = 4.1f;
        // DEVIATION(DV349): tg's client.view_size.setTo(view_range): a first-person view has no top-down view size to widen
        if (auto == null)
            auto = Dm.@new<datum_action_innate_proto_emitter_firing>();
        await auto.Grant(buckled_mob, this);
        return null;
    }
}

// tg: code/modules/power/singularity/emitter.dm:510
public partial class datum_action_innate_proto_emitter : datum_action_innate
{
    // tg: code/modules/power/singularity/emitter.dm:516
    public override object Destroy(float? force = null)
    {
        proto_emitter = null;
        buckled_mob = null;
        return base.Destroy(force);
    }

    // tg: code/modules/power/singularity/emitter.dm:521
    [DmParams("user", "proto")]
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        proto_emitter = Dm.arg(__extra, 0) as obj_machinery_power_emitter_prototype;
        buckled_mob = grant_to as mob_living_carbon;
        return await base.Grant(grant_to, __extra);
    }
}

// tg: code/modules/power/singularity/emitter.dm:527
public partial class datum_action_innate_proto_emitter_firing : datum_action_innate_proto_emitter
{
    // tg: code/modules/power/singularity/emitter.dm:531
    public override async DmTask<object> Activate()
    {
        if (Dm.truthy(proto_emitter.manual))
        {
            playsound(proto_emitter, "sound/vehicles/mecha/mechmove01.ogg", 50, TRUE);
            proto_emitter.manual = FALSE;
            name = "Switch to Manual Firing";
            desc = "The emitter will only fire on your command and at your designated target";
            button_icon_state = "mech_zoom_on";
            foreach (obj_item item in Dm.each<obj_item>(buckled_mob.held_items))
            {
                if (istype(item, typeof(obj_item_turret_control)))
                    qdel(item);
            }
            await build_all_button_icons();
            return null;
        }
        playsound(proto_emitter, "sound/vehicles/mecha/mechmove01.ogg", 50, TRUE);
        name = "Switch to Automatic Firing";
        desc = "Emitters will switch to periodic firing at your last target";
        button_icon_state = "mech_zoom_off";
        proto_emitter.manual = TRUE;
        foreach (object things in Dm.each(buckled_mob.held_items))
        {
            obj_item item = things as obj_item;
            if (item != null)
            {
                if (!Dm.truthy(await buckled_mob.dropItemToGround(item)))
                    continue;
                obj_item_turret_control turret_control = Dm.@new<obj_item_turret_control>();
                await buckled_mob.put_in_hands(turret_control);
            }
            else //Entries in the list should only ever be items or null, so if it's not an item, we can assume it's an empty hand
            {
                obj_item_turret_control turret_control = Dm.@new<obj_item_turret_control>();
                await buckled_mob.put_in_hands(turret_control);
            }
        }
        await build_all_button_icons();
        return null;
    }
}

// tg: code/modules/power/singularity/emitter.dm:562
public partial class obj_item_turret_control : obj_item
{
    // tg: code/modules/power/singularity/emitter.dm:571
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, ABSTRACT_ITEM_TRAIT);
        return dot;
    }

    // tg: code/modules/power/singularity/emitter.dm:575
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            return NONE;
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/modules/power/singularity/emitter.dm:580
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        obj_machinery_power_emitter emitter = user.buckled as obj_machinery_power_emitter;
        await emitter.setDir(get_dir(emitter, interacting_with));
        await user.setDir(emitter.dir);
        float sw__1 = Dm.num(emitter.dir);
        if (sw__1 == NORTH)
        {
            emitter.layer = 3.9f;
            user.pixel_x = 0;
            user.pixel_y = -14;
        }
        else if (sw__1 == NORTHEAST)
        {
            emitter.layer = 3.9f;
            user.pixel_x = -8;
            user.pixel_y = -12;
        }
        else if (sw__1 == EAST)
        {
            emitter.layer = 4.1f;
            user.pixel_x = -14;
            user.pixel_y = 0;
        }
        else if (sw__1 == SOUTHEAST)
        {
            emitter.layer = 3.9f;
            user.pixel_x = -8;
            user.pixel_y = 12;
        }
        else if (sw__1 == SOUTH)
        {
            emitter.layer = 4.1f;
            user.pixel_x = 0;
            user.pixel_y = 14;
        }
        else if (sw__1 == SOUTHWEST)
        {
            emitter.layer = 3.9f;
            user.pixel_x = 8;
            user.pixel_y = 12;
        }
        else if (sw__1 == WEST)
        {
            emitter.layer = 4.1f;
            user.pixel_x = 14;
            user.pixel_y = 0;
        }
        else if (sw__1 == NORTHWEST)
        {
            emitter.layer = 3.9f;
            user.pixel_x = 8;
            user.pixel_y = -12;
        }

        emitter.last_projectile_params = calculate_projectile_angle_and_pixel_offsets(user, null, modifiers);

        if (emitter.charge >= 10 && Dm.num(world.time) > delay)
        {
            emitter.charge -= 10;
            await emitter.fire_beam(user);
            delay = Dm.num(world.time) + 10;
        }
        else if (emitter.charge < 10)
            playsound(this, "sound/machines/buzz/buzz-sigh.ogg", 50, TRUE);
        return null;
    }
}
