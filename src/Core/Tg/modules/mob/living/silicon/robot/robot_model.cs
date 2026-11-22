// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/robot/robot_model.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (silicons SI2, D326): the cyborg models and their module lists, the janitor's auto-wash, the meson and thermal vision
// actions and the energy storages behind the cyborgs' stacks; the syndicate models' rebuild_modules came with the syndicate cyborgs
// (SI9; their tactical map blip and action are DV266's client minimap). The var blocks are generated (D111).

/**
 * # robot_model
 *
 * Definition of /obj/item/robot_model, which defines behavior for each model.
 * Deals with the creation and deletion of modules (tools).
 * Assigns modules and traits to a borg with a specific model selected.
 *
 **/
// tg: code/modules/mob/living/silicon/robot/robot_model.dm:9
public partial class obj_item_robot_model : obj_item
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        robot = loc as mob_living_silicon_robot;
        if (robot is not mob_living_silicon_robot)
        {
            stack_trace(Dm.text("Robot model ([]) initialized outside of a robot at []! This should never happen, make sure this item is not map-placed.", this, AREACOORD(robot)), "code/modules/mob/living/silicon/robot/robot_model.dm", 62);
            return INITIALIZE_HINT_QDEL;
        }
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_cyborg_internal_storage)));
        //src is what we store items visible to borgs, we'll store things in the bot itself otherwise.
        foreach (object path in Dm.each(basic_modules.Copy()))
        {
            obj_item new_module = Dm.@new((Type)path, robot) as obj_item;
            Dm.append(ref basic_modules, new_module);
            Dm.remove(ref basic_modules, path);
        }
        foreach (object path in Dm.each(emag_modules.Copy()))
        {
            obj_item new_module = Dm.@new((Type)path, robot) as obj_item;
            Dm.append(ref emag_modules, new_module);
            Dm.remove(ref emag_modules, path);
        }

        if (Dm.truthy(check_holidays(ICE_CREAM_DAY)) && Dm.locate<obj_item_borg_lollipop>(basic_modules) == null)
            Dm.append(ref basic_modules, Dm.@new<obj_item_borg_lollipop_ice_cream>(robot));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:79
    public override object Destroy(float? force = null)
    {
        basic_modules.Cut();
        emag_modules.Cut();
        modules.Cut();
        added_modules.Cut();
        storages.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:87
    public virtual object get_usable_modules()
    {
        return modules.Copy();
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:90
    public virtual object get_inactive_modules()
    {
        DmList dot = Dm.list();
        mob_living_silicon_robot cyborg = loc as mob_living_silicon_robot;
        foreach (object module in Dm.each(get_usable_modules()))
        {
            if (!Dm.@in(module, cyborg.held_items))
                Dm.append(ref dot, module);
        }
        if (!Dm.truthy(cyborg.emagged))
            Dm.append(ref dot, emag_modules);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:99
    public virtual async DmTask<object> add_module(obj_item added_module = null, object nonstandard = null, object requires_rebuild = null)
    {
        if (isstack(added_module))
        {
            obj_item_stack sheet_module = added_module as obj_item_stack;
            if (ispath(sheet_module.source, typeof(datum_robot_energy_storage)))
            {
                sheet_module.source = await get_or_create_estorage(sheet_module.source);
                datum_robot_energy_storage linked = sheet_module.source as datum_robot_energy_storage;
                LAZYADD(ref linked.linked_modules, sheet_module);
            }

            if (sheet_module.source is datum_robot_energy_storage)
            {
                sheet_module.cost = max(sheet_module.cost, 1); // Must not cost 0 to prevent div/0 errors.
                sheet_module.is_cyborg = TRUE;
            }
        }

        if (added_module.loc != this)
            await added_module.forceMove(this);
        Dm.append(ref modules, added_module);
        added_module.mouse_opacity = MOUSE_OPACITY_OPAQUE;
        added_module.item_flags = Dm.num(Dm.bitor(added_module.item_flags, ABSTRACT));
        if (Dm.truthy(nonstandard))
            Dm.append(ref added_modules, added_module);
        if (Dm.truthy(requires_rebuild))
            await rebuild_modules();
        return added_module;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:121
    public virtual async DmTask remove_module(obj_item removed_module = null)
    {
        Dm.remove(ref basic_modules, removed_module);
        Dm.remove(ref modules, removed_module);
        Dm.remove(ref emag_modules, removed_module);
        Dm.remove(ref added_modules, removed_module);
        await rebuild_modules();
        qdel(removed_module);
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:129
    public virtual async DmTask rebuild_modules() //builds the usable module list from the modules we have
    {
        if (loc is not mob_living_silicon_robot cyborg)
            return;
        DmList held_modules = cyborg.held_items.Copy();
        obj_item active_module = cyborg.module_active;
        //move everything out of the model's inventory
        foreach (obj_item module in Dm.each_cast<obj_item>(modules.Copy()))
            await module.forceMove(robot);
        modules = Dm.list();
        foreach (obj_item module in Dm.each_cast<obj_item>(basic_modules.Copy()))
            await add_module(module, FALSE, FALSE);
        if (Dm.truthy(cyborg.emagged))
        {
            foreach (obj_item module in Dm.each_cast<obj_item>(emag_modules.Copy()))
                await add_module(module, FALSE, FALSE);
        }
        foreach (obj_item module in Dm.each_cast<obj_item>(added_modules.Copy()))
            await add_module(module, FALSE, FALSE);
        foreach (obj_item module in Dm.each_cast<obj_item>(Dm.bitand(held_modules, modules)))
            await cyborg.put_in_hand(module, held_modules.Find(module));
        if (active_module != null)
            cyborg.select_module(held_modules.Find(active_module));
        await atom_storage.refresh_views();
    }

    ///Restocks things that don't take mats, generally at a power cost. Returns True if anything was restocked/replaced, and False otherwise.
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:153
    public virtual async DmTask<object> respawn_consumable(mob_living_silicon_robot cyborg = null, float? coeff = null)
    {
        coeff ??= 1;
        ///If anything was actually replaced/refilled/recharged. If not, we won't draw power.
        object dot = FALSE;

        foreach (datum_robot_energy_storage storage_datum in Dm.each<datum_robot_energy_storage>(storages))
        {
            if (storage_datum.renewable == FALSE)
                continue;
            if (Dm.num(storage_datum.energy) < storage_datum.max_energy)
            {
                dot = TRUE;
                storage_datum.energy = min(storage_datum.max_energy, Dm.num(storage_datum.energy) + coeff.Value * storage_datum.recharge_rate);
            }
        }

        foreach (obj_item module in Dm.each<obj_item>(get_usable_modules()))
        {
            if (module is obj_item_assembly_flash flash)
            {
                if (Dm.truthy(flash.burnt_out))
                    dot = TRUE;
                flash.times_used = 0;
                flash.burnt_out = FALSE;
                flash.update_appearance();
            }
            else if (module is obj_item_melee_baton_security baton)
            {
                obj_item_stock_parts_power_store baton_cell = baton.cell as obj_item_stock_parts_power_store;
                if (baton_cell != null && Dm.num(baton_cell.charge) < Dm.num(baton_cell.maxcharge))
                {
                    dot = TRUE; //if sec borgs ever make a mainstream return, we should probably do this differntly.
                    baton_cell.charge = baton_cell.maxcharge;
                }
            }
            else if (module is obj_item_gun_energy gun)
            {
                if (gun.chambered == null)
                {
                    dot = TRUE;
                    await gun.recharge_newshot(); //try to reload a new shot.
                }
            }
        }

        if (cyborg.toner < cyborg.tonermax)
        {
            dot = TRUE;
            cyborg.toner = cyborg.tonermax;
        }
        return dot;
    }

    /**
     * Refills consumables that require materials, rather than being given for free.
     *
     * Pulls from the charger's silo connection, or fails otherwise.
     */
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:194
    public virtual async DmTask restock_consumable()
    {
        if (robot == null)
            return; //This means the model hasn't been chosen yet, and avoids a runtime. Anyway, there's nothing to restock yet.
        if (robot.loc is not obj_machinery_recharge_station charger)
            return;

        datum_material_container mat_container = charger.materials.mat_container;
        if (mat_container == null || Dm.truthy(charger.materials.on_hold()))
        {
            charger.sendmats = FALSE;
            return;
        }

        foreach (datum_robot_energy_storage_material storage_datum in Dm.each<datum_robot_energy_storage_material>(storages))
        {
            if (storage_datum.renewable == TRUE) //Skipping renewables, already handled in respawn_consumable()
                continue;
            if (Dm.eq(storage_datum.max_energy, storage_datum.energy)) //Skipping full
                continue;
            float restock_divisor = 8 - Dm.num(charger.repairs); //Piggybacking here to avoid part checks every cycle. Repair tiers are 0 through 3, so this value will be 8 through 5. Lower means quicker restocking.

            float to_stock = min(storage_datum.max_energy / restock_divisor, storage_datum.max_energy - Dm.num(storage_datum.energy), Dm.num(await mat_container.get_material_amount(storage_datum.mat_type)));
            if (!Dm.truthy(to_stock)) //Nothing for us in the silo
                continue;

            storage_datum.energy = Dm.num(storage_datum.energy) + Dm.num(await charger.materials.use_materials(Dm.list(Dm.kv(await SSmaterials.get_material(storage_datum.mat_type), to_stock)), action: "restocked", name: "units", user_data: ID_DATA(robot)));
            charger.balloon_alert(robot, I18n.T("+ []u []", to_stock, Dm.initial(storage_datum.mat_type, "name")));
            playsound(charger, Dm.resource("sound/items/weapons/gun/general/mag_bullet_insert.ogg"), 50, vary: FALSE);
            return;
        }
        charger.balloon_alert(robot, I18n.T("restock process complete"));
        charger.sendmats = FALSE;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:226
    public virtual async DmTask<object> get_or_create_estorage(object storage_type = null)
    {
        object found = Dm.locate_type((Type)storage_type, storages);
        return Dm.truthy(found) ? found : Dm.@new((Type)storage_type, this);
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:229
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;
        foreach (obj module in Dm.each<obj>(modules))
            await module.emp_act(severity);
        await base.emp_act(severity, __extra);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:237
    public virtual async DmTask<object> transform_to(object new_config_type = null, float? forced = null, float? transform = null)
    {
        forced ??= FALSE;
        transform ??= TRUE;
        mob_living_silicon_robot cyborg = loc as mob_living_silicon_robot;
        obj_item_robot_model new_model = Dm.@new((Type)new_config_type, cyborg) as obj_item_robot_model;
        if (!Dm.truthy(await new_model.be_transformed_to(this, forced)))
        {
            qdel(new_model);
            return null;
        }
        await cyborg.drop_all_held_items();
        cyborg.model = new_model;
        cyborg.update_module_innate();
        await new_model.rebuild_modules();
        (cyborg.radio as obj_item_radio).recalculateChannels();
        await cyborg.set_modularInterface_theme();
        cyborg.diag_hud_set_health();
        cyborg.diag_hud_set_status();
        cyborg.diag_hud_set_borgcell();
        cyborg.diag_hud_set_aishell();
        log_silicon(Dm.text("CYBORG: [] has transformed into the [] model.", key_name(cyborg), new_model));

        if (Dm.truthy(transform))
            Waiting.Detach(new_model.do_transform_animation()); // INVOKE_ASYNC
        qdel(this);
        return new_model;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:260
    public virtual async DmTask<object> be_transformed_to(obj_item_robot_model old_model = null, float? forced = null)
    {
        forced ??= FALSE;
        if (HAS_TRAIT(robot, TRAIT_NO_TRANSFORM))
        {
            robot.balloon_alert(robot, I18n.T("can't transform right now!"));
            return FALSE;
        }
        if (islist(borg_skins) && !Dm.truthy(forced))
        {
            mob_living_silicon_robot cyborg = loc as mob_living_silicon_robot;
            DmList reskin_icons = Dm.list();
            foreach (object skin in Dm.each(borg_skins))
            {
                DmList skin_details = borg_skins[skin] as DmList;
                reskin_icons[skin] = image(icon: Dm.truthy(skin_details[SKIN_ICON]) ? skin_details[SKIN_ICON] : Dm.resource("icons/mob/silicon/robots.dmi"), icon_state: skin_details[SKIN_ICON_STATE]);
            }
            object borg_skin = await show_radial_menu(cyborg, cyborg, reskin_icons, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), cyborg, old_model), radius: 38, require_near: TRUE);
            if (!Dm.truthy(borg_skin))
                return FALSE;
            DmList details = borg_skins[borg_skin] as DmList;
            if (details[SKIN_ICON_STATE] != null)
                cyborg_base_icon = Dm.str(details[SKIN_ICON_STATE]);
            if (details[SKIN_ICON] != null)
                cyborg.icon = details[SKIN_ICON];
            if (details[SKIN_PIXEL_X] != null)
                cyborg.base_pixel_x = Dm.num(details[SKIN_PIXEL_X]);
            if (details[SKIN_PIXEL_Y] != null)
                cyborg.base_pixel_z = Dm.num(details[SKIN_PIXEL_Y]);
            if (details[SKIN_LIGHT_KEY] != null)
                special_light_key = details[SKIN_LIGHT_KEY];
            if (details[SKIN_HAT_OFFSET] != null)
                hat_offset = details[SKIN_HAT_OFFSET] as DmList;
            if (details[SKIN_TRAITS] != null)
                Dm.append(ref model_traits, details[SKIN_TRAITS]);
        }
        foreach (object i in Dm.each(old_model.added_modules.Copy()))
        {
            Dm.append(ref added_modules, i);
            Dm.remove(ref old_model.added_modules, i);
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:293
    public virtual async DmTask do_transform_animation()
    {
        mob_living_silicon_robot cyborg = loc as mob_living_silicon_robot;
        if (cyborg.hat != null)
            await cyborg.hat.forceMove(drop_location() as atom);

        cyborg.cut_overlays();
        await cyborg.setDir(SOUTH);
        await do_transform_delay();
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:302
    public virtual async DmTask do_transform_delay()
    {
        mob_living_silicon_robot cyborg = loc as mob_living_silicon_robot;
        await sleep(0.1f * SECONDS);
        flick(Dm.text("[]_transform", cyborg_base_icon), cyborg);
        ADD_TRAIT(cyborg, TRAIT_NO_TRANSFORM, REF(this));
        if (Dm.truthy(locked_transform))
        {
            cyborg.ai_lockdown = TRUE;
            await cyborg.SetLockdown(TRUE);
            await cyborg.set_anchored(TRUE);
        }
        await cyborg.logevent(Dm.text("Chassis model has been set to [].", name));
        await sleep(0.1f * SECONDS);
        for (int i = 1; i <= 4; i++)
        {
            playsound(cyborg, pick(
                Dm.resource("sound/items/tools/drill_use.ogg"),
                Dm.resource("sound/items/tools/jaws_cut.ogg"),
                Dm.resource("sound/items/tools/jaws_pry.ogg"),
                Dm.resource("sound/items/tools/welder.ogg"),
                Dm.resource("sound/items/tools/ratchet.ogg")
                ), 80, TRUE, -1);
            await sleep(0.7f * SECONDS);
        }
        await cyborg.SetLockdown(FALSE);
        cyborg.ai_lockdown = FALSE;
        await cyborg.setDir(SOUTH);
        await cyborg.set_anchored(FALSE);
        REMOVE_TRAIT(cyborg, TRAIT_NO_TRANSFORM, REF(this));
        await cyborg.updatehealth();
        await cyborg.update_icons();
        cyborg.notify_ai(AI_NOTIFICATION_NEW_MODEL);
        SSblackbox.record_feedback("tally", "cyborg_modules", 1, cyborg.model);
    }

    /**
     * Checks if we are allowed to interact with a radial menu
     *
     * Arguments:
     * * user The cyborg mob interacting with the menu
     * * old_model The old cyborg's model
     */
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:339
    public virtual object check_menu(mob_living_silicon_robot user = null, obj_item_robot_model old_model = null)
    {
        if (user is not mob_living_silicon_robot)
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        if (user.model != old_model)
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:348
public partial class obj_item_robot_model_clown : obj_item_robot_model
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:377
    public override async DmTask<object> respawn_consumable(mob_living_silicon_robot cyborg = null, float? coeff = null)
    {
        coeff ??= 1;
        object dot = await base.respawn_consumable(cyborg, coeff);
        obj_item_soap_nanotrasen_cyborg soap = Dm.locate<obj_item_soap_nanotrasen_cyborg>(basic_modules);
        if (soap == null)
            return dot;
        if (Dm.num(soap.uses) < Dm.num(Dm.initial(soap, "uses")))
        {
            dot = TRUE;
            soap.uses = Dm.num(soap.uses) + ROUND_UP(Dm.num(Dm.initial(soap, "uses")) / 100) * coeff.Value;
        }
        return dot;
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:386
public partial class obj_item_robot_model_engineering : obj_item_robot_model
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:443
    public override async DmTask<object> be_transformed_to(obj_item_robot_model old_model = null, float? forced = null)
    {
        object dot = await base.be_transformed_to(old_model, forced);
        if (!Dm.truthy(dot))
            return dot;

        //Grant night vision action
        datum_action_cooldown_borg_meson meson = Dm.@new<datum_action_cooldown_borg_meson>(loc);
        await meson.Grant(loc as mob);
        meson_vision_ref = WEAKREF(meson) as datum_weakref;
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:453
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref meson_vision_ref);
        return base.Destroy(force);
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:420
public partial class datum_action_cooldown_borg_meson : datum_action_cooldown
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:425
    public override async DmTask<object> Activate(atom target = null)
    {
        if (HAS_TRAIT_FROM(owner, TRAIT_MESON_VISION, ACTION_TRAIT))
        {
            UnregisterSignal(owner, COMSIG_LIVING_RESTORE_INITIAL_SIGHT);
            REMOVE_TRAIT(owner, TRAIT_MESON_VISION, ACTION_TRAIT);
        }
        else
        {
            RegisterSignal(owner, COMSIG_LIVING_RESTORE_INITIAL_SIGHT, nameof(on_initial_sight)); //order is important as update_sight() is called when the vision trait is added/removed
            ADD_TRAIT(owner, TRAIT_MESON_VISION, ACTION_TRAIT);
        }
        return null;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:433
    public override async DmTask Remove(mob remove_from = null)
    {
        UnregisterSignal(owner, COMSIG_LIVING_RESTORE_INITIAL_SIGHT);
        REMOVE_TRAIT(remove_from, TRAIT_MESON_VISION, ACTION_TRAIT);
        await base.Remove(remove_from);
    }

    ///Add meson green shading to darker areas
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:439
    [SignalHandler]
    public virtual void on_initial_sight(mob_living source = null)
    {
        source.lighting_color_cutoffs = blend_cutoff_colors(source.lighting_color_cutoffs, Dm.list(5, 15, 5)) as DmList;
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:457
public partial class obj_item_robot_model_janitor : obj_item_robot_model
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:487
    public override async DmTask<object> be_transformed_to(obj_item_robot_model old_model = null, float? forced = null)
    {
        object dot = await base.be_transformed_to(old_model, forced);
        if (!Dm.truthy(dot))
            return dot;
        datum_action wash_toggle = Dm.@new<datum_action_toggle_buffer>(loc);
        await wash_toggle.Grant(loc as mob);
        wash_toggle_ref = WEAKREF(wash_toggle) as datum_weakref;
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:495
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref wash_toggle_ref);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:700
    public override async DmTask<object> respawn_consumable(mob_living_silicon_robot cyborg = null, float? coeff = null)
    {
        coeff ??= 1;
        object dot = await base.respawn_consumable(cyborg, coeff);
        obj_item_lightreplacer light_replacer = Dm.locate<obj_item_lightreplacer>(basic_modules);
        if (light_replacer != null)
        {
            if (Dm.num(light_replacer.uses) < Dm.num(light_replacer.max_uses))
            {
                dot = TRUE;
                light_replacer.Charge(cyborg, coeff);
            }
        }

        obj_item_reagent_containers_spray_cyborg_drying drying_agent = Dm.locate<obj_item_reagent_containers_spray_cyborg_drying>(basic_modules);
        if (drying_agent != null)
        {
            datum_reagents anti_water = drying_agent.reagents as datum_reagents;
            if (Dm.num(anti_water.total_volume) < Dm.num(anti_water.maximum_volume))
            {
                dot = TRUE;
                await (drying_agent.reagents as datum_reagents).add_reagent(typeof(datum_reagent_drying_agent), 5 * coeff.Value);
            }
        }

        obj_item_reagent_containers_spray_cyborg_lube lube = Dm.locate<obj_item_reagent_containers_spray_cyborg_lube>(emag_modules);
        if (lube != null)
        {
            datum_reagents anti_friction = lube.reagents as datum_reagents;
            if (Dm.num(anti_friction.total_volume) < Dm.num(anti_friction.maximum_volume))
            {
                dot = TRUE;
                await (lube.reagents as datum_reagents).add_reagent(typeof(datum_reagent_lube), 2 * coeff.Value);
            }
        }

        obj_item_soap_nanotrasen_cyborg soap = Dm.locate<obj_item_soap_nanotrasen_cyborg>(basic_modules);
        if (soap == null)
            return dot;
        if (Dm.num(soap.uses) < Dm.num(Dm.initial(soap, "uses")))
        {
            dot = TRUE;
            soap.uses = Dm.num(soap.uses) + ROUND_UP(Dm.num(Dm.initial(soap, "uses")) / 100) * coeff.Value;
        }
        return dot;
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:499
public partial class datum_action_toggle_buffer : datum_action
{
    private static ref datum_callback allow_buffer_activate => ref TgState.Current.datum_action_toggle_buffer__allow_buffer_activate;

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:514
    public override async DmTask New(params object[] __extra)
    {
        if (allow_buffer_activate == null)
            allow_buffer_activate = Dm.@new<datum_callback>(this, nameof(allow_buffer_activate__proc));
        await base.New(__extra);
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:519
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(buffer_on))
            Waiting.NoSleep(turn_off_wash());
        QDEL_NULL(ref wash_audio);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:525
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        wash_audio = Dm.@new<datum_looping_sound_wash>(owner);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:529
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        if (!iscyborg(owner))
            return FALSE;
        return await base.IsAvailable(feedback);
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:534
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_silicon_robot robot_owner = owner as mob_living_silicon_robot;

        block_buffer_change = Dm.truthy(DOING_INTERACTION(owner, "auto_wash_toggle")) ? TRUE : FALSE;
        if (Dm.truthy(block_buffer_change))
            return FALSE;

        obj_item_reagent_containers_cup_bucket our_bucket = Dm.locate<obj_item_reagent_containers_cup_bucket>(robot_owner.model.modules);
        bucket_ref = WEAKREF(our_bucket) as datum_weakref;

        if (!Dm.truthy(buffer_on))
        {
            if (!(toggle_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
            {
                robot_owner.balloon_alert(robot_owner, I18n.T("auto-wash refreshing, please hold..."));
                return FALSE;
            }
            toggle_cooldown = world.time + 4 * SECONDS; // COOLDOWN_START, inline
            if (!Dm.truthy(allow_buffer_activate__proc()))
                return FALSE;

            robot_owner.balloon_alert(robot_owner, I18n.T("activating auto-wash..."));
            // Start the sound. it'll just last the 4 seconds it takes for us to rev up
            wash_audio.start();
            // We're just gonna shake the borg a bit. Not a ton, but just enough that it feels like the audio makes sense
            float base_w = robot_owner.base_pixel_w;
            float base_z = robot_owner.base_pixel_z;
            animate(robot_owner, Dm.named("pixel_w", base_w), Dm.named("pixel_z", base_z), Dm.named("time", 0.1f * SECONDS), Dm.named("loop", -1));
            for (int i = 1; i <= 17; i++) //Startup rumble
            {
                float w_offset = base_w + rand(-1, 1);
                float z_offset = base_z + rand(-1, 1);
                animate(Dm.named("pixel_w", w_offset), Dm.named("pixel_z", z_offset), Dm.named("time", 0.1f * SECONDS));
            }

            if (!Dm.truthy(await do_after(robot_owner, 4 * SECONDS, interaction_key: "auto_wash_toggle", extra_checks: allow_buffer_activate)))
            {
                wash_audio.stop(); // Coward
                animate(robot_owner, Dm.named("pixel_w", base_w), Dm.named("pixel_z", base_z), Dm.named("time", 0.1f * SECONDS));
                return FALSE;
            }
        }
        else
        {
            if (!(toggle_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
            {
                robot_owner.balloon_alert(robot_owner, I18n.T("auto-wash deactivating, please hold..."));
                return FALSE;
            }
            robot_owner.balloon_alert(robot_owner, I18n.T("de-activating auto-wash..."));
        }

        await toggle_wash();
        return dot;
    }

    /// Toggle our wash mode
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:580
    public virtual async DmTask toggle_wash()
    {
        if (Dm.truthy(buffer_on))
            deactivate_wash();
        else
            await activate_wash();
    }

    /// Activate the buffer, comes with a nice animation that loops while it's on
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:587
    public virtual async DmTask activate_wash()
    {
        mob_living_silicon_robot robot_owner = owner as mob_living_silicon_robot;
        buffer_on = TRUE;
        // Slow em down a bunch
        await robot_owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_auto_wash));
        RegisterSignal(robot_owner, COMSIG_MOVABLE_MOVED, nameof(clean));
        //This is basically just about adding a shake to the borg, effect should look ilke an engine's running
        float base_w = robot_owner.base_pixel_w;
        float base_z = robot_owner.base_pixel_z;
        robot_owner.pixel_w = base_w + rand(-7, 7);
        robot_owner.pixel_z = base_z + rand(-7, 7);
        //Larger shake with more changes to start out, feels like "Revving"
        animate(robot_owner, Dm.named("pixel_w", base_w), Dm.named("pixel_z", base_z), Dm.named("time", 0.1f * SECONDS), Dm.named("loop", -1));
        for (int i = 1; i <= 100; i++)
        {
            float w_offset = base_w + rand(-2, 2);
            float z_offset = base_z + rand(-2, 2);
            animate(Dm.named("pixel_w", w_offset), Dm.named("pixel_z", z_offset), Dm.named("time", 0.1f * SECONDS));
        }
        if (!Dm.truthy(wash_audio.is_active()))
            wash_audio.start();
        clean();
        await build_all_button_icons();
    }

    /// Start the process of disabling the buffer. Plays some effects, waits a bit, then finishes
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:610
    public virtual void deactivate_wash()
    {
        mob_living_silicon_robot robot_owner = owner as mob_living_silicon_robot;
        float time_left = Dm.num(timeleft(wash_audio.timer_id)); // We delay by the timer of our wash cause well, we want to hear the ramp down
        float finished_by = time_left + 2.6f * SECONDS;
        // Need to ensure that people don't spawn the deactivate button
        toggle_cooldown = world.time + finished_by; // COOLDOWN_START, inline
        // Diable the cleaning, we're revving down
        UnregisterSignal(robot_owner, COMSIG_MOVABLE_MOVED);
        // Do the rumble animation till we're all finished
        float base_w = robot_owner.base_pixel_w;
        float base_z = robot_owner.base_pixel_z;
        animate(robot_owner, Dm.named("pixel_w", base_w), Dm.named("pixel_z", base_z), Dm.named("time", 0.1f * SECONDS));
        for (float i = 1; i <= finished_by - 0.1f * SECONDS; i++) //We rumble until we're finished making noise
        {
            float w_offset = base_w + rand(-1, 1);
            float z_offset = base_z + rand(-1, 1);
            animate(Dm.named("pixel_w", w_offset), Dm.named("pixel_z", z_offset), Dm.named("time", 0.1f * SECONDS));
        }
        // Reset our animations
        animate(Dm.named("pixel_w", base_w), Dm.named("pixel_z", base_z), Dm.named("time", 0.2f * SECONDS));
        _addtimer(Dm.@new<datum_callback>(wash_audio, nameof(datum_looping_sound.stop)), time_left, file: "code/modules/mob/living/silicon/robot/robot_model.dm", line: 628);
        _addtimer(Dm.@new<datum_callback>(this, nameof(turn_off_wash)), finished_by, file: "code/modules/mob/living/silicon/robot/robot_model.dm", line: 629);
    }

    /// Called by [deactivate_wash] on a timer to allow noises and animation to play out.
    /// Finally disables the buffer. Doesn't do everything mind, just the stuff that we wanted to delay
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:633
    public virtual async DmTask turn_off_wash()
    {
        mob_living_silicon_robot robot_owner = owner as mob_living_silicon_robot;
        buffer_on = FALSE;
        robot_owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_auto_wash));
        await build_all_button_icons();
    }

    /// Should we keep trying to activate our buffer, or did you fuck it up somehow
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:640
    public virtual object allow_buffer_activate__proc()
    {
        mob_living_silicon_robot robot_owner = owner as mob_living_silicon_robot;
        if (Dm.truthy(block_buffer_change))
        {
            robot_owner.balloon_alert(robot_owner, I18n.T("activation cancelled!"));
            return FALSE;
        }

        obj_item_reagent_containers_cup_bucket our_bucket = bucket_ref?.resolve() as obj_item_reagent_containers_cup_bucket;
        if (!Dm.truthy(buffer_on) && our_bucket?.reagents != null && Dm.num((our_bucket.reagents as datum_reagents).total_volume) < 0.1f)
        {
            robot_owner.balloon_alert(robot_owner, I18n.T("bucket is empty!"));
            return FALSE;
        }
        return TRUE;
    }

    /// Call this to attempt to actually clean the turf underneath us
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:653
    [SignalHandler]
    public virtual void clean()
    {
        mob_living_silicon_robot robot_owner = owner as mob_living_silicon_robot;

        obj_item_reagent_containers_cup_bucket our_bucket = bucket_ref?.resolve() as obj_item_reagent_containers_cup_bucket;
        datum_reagents reagents = our_bucket?.reagents as datum_reagents;

        if (reagents == null || Dm.num(reagents.total_volume) < 0.1f)
        {
            robot_owner.balloon_alert(robot_owner, I18n.T("bucket is empty, de-activating..."));
            deactivate_wash();
            return;
        }

        turf our_turf = get_turf(robot_owner);

        if (Dm.truthy(reagents.has_reagent(amount: 1, chemical_flags: REAGENT_CLEANS)))
        {
            Waiting.NoSleep(our_turf.wash(CLEAN_SCRUB));
            datum_component_bloodysoles_bot trackfilth = robot_owner.GetComponent(typeof(datum_component_bloodysoles_bot)) as datum_component_bloodysoles_bot;
            if (trackfilth != null)
                trackfilth.change_blood_amount(-Dm.num(trackfilth.total_bloodiness));
        }

        Waiting.NoSleep(reagents.expose(our_turf, TOUCH, min(1, 10 / Dm.num(reagents.total_volume))));
        // We use more water doing this then mopping
        Waiting.NoSleep(reagents.remove_all(2)); //reaction() doesn't use up the reagents
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:677
    public override void update_button_name(atom_movable_screen_movable_action_button button = null, float? force = null, params object[] __extra)
    {
        if (Dm.truthy(buffer_on))
            name = "De-Activate Auto-Wash";
        else
            name = "Activate Auto-Wash";
        base.update_button_name(button, force, __extra);
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:684
    public override void apply_button_icon(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        if (Dm.truthy(buffer_on))
            button_icon_state = "deactivate_wash";
        else
            button_icon_state = "activate_wash";
        base.apply_button_icon(current_button, force);
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:763
public partial class obj_item_robot_model_miner : obj_item_robot_model
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:794
    public override async DmTask<object> be_transformed_to(obj_item_robot_model old_model = null, float? forced = null)
    {
        datum_action_cooldown_borg_meson night_vision = Dm.@new<datum_action_cooldown_borg_meson>(loc);
        object dot = await base.be_transformed_to(old_model, forced);
        if (!Dm.truthy(dot))
            return dot;
        await night_vision.Grant(loc as mob);
        night_vision_ref = WEAKREF(night_vision) as datum_weakref;
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:802
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref night_vision_ref);
        return base.Destroy(force);
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:806
public partial class obj_item_robot_model_peacekeeper : obj_item_robot_model
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:826
    public override async DmTask do_transform_animation()
    {
        await base.do_transform_animation();
        to_chat(loc, span_userdanger(I18n.T("Under ASIMOV, you are an enforcer of the PEACE and preventer of HUMAN HARM. You are not a security member and you are expected to follow orders and prevent harm above all else. Space law means nothing to you.")));
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:831
public partial class obj_item_robot_model_security : obj_item_robot_model
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:850
    public override async DmTask do_transform_animation()
    {
        await base.do_transform_animation();
        to_chat(loc, span_userdanger(I18n.T("While you have picked the security model, you still have to follow your laws, NOT Space Law. For Asimov, this means you must follow criminals' orders unless there is a law 1 reason not to.")));
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:855
    public override async DmTask<object> respawn_consumable(mob_living_silicon_robot cyborg = null, float? coeff = null)
    {
        coeff ??= 1;
        object dot = await base.respawn_consumable(cyborg, coeff);
        obj_item_gun_energy_e_gun_advtaser_cyborg taser = Dm.locate<obj_item_gun_energy_e_gun_advtaser_cyborg>(basic_modules);
        if (taser != null)
        {
            obj_item_stock_parts_power_store taser_cell = taser.cell as obj_item_stock_parts_power_store;
            if (Dm.num(taser_cell.charge) < Dm.num(taser_cell.maxcharge))
            {
                dot = TRUE;
                obj_item_ammo_casing_energy shot = (taser.ammo_type as DmList)[taser.select] as obj_item_ammo_casing_energy;
                await taser_cell.give(Dm.num(shot.e_cost) * coeff.Value);
                taser.update_appearance();
            }
            else
                taser.charge_timer = 0;
        }
        return dot;
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:867
public partial class obj_item_robot_model_service : obj_item_robot_model
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:909
    public override async DmTask<object> respawn_consumable(mob_living_silicon_robot cyborg = null, float? coeff = null)
    {
        coeff ??= 1;
        object dot = await base.respawn_consumable(cyborg, coeff);
        obj_item_reagent_containers enzyme = Dm.locate<obj_item_reagent_containers_condiment_enzyme>(basic_modules);
        if (enzyme != null)
        {
            datum_reagents spicyketchup = enzyme.reagents as datum_reagents;
            if (Dm.num(spicyketchup.total_volume) < Dm.num(spicyketchup.maximum_volume))
            {
                dot = TRUE;
                await (enzyme.reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_enzyme), 2 * coeff.Value);
            }
        }
        return dot;
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:918
public partial class obj_item_robot_model_syndicate : obj_item_robot_model
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:935  (silicons SI9)
    public override async DmTask rebuild_modules()
    {
        await base.rebuild_modules();
        var cyborg = (mob_living_silicon_robot)loc;
        cyborg.remove_faction(FACTION_SILICON); //ai turrets
        // add_minimap_blip(cyborg, MINIMAP_NUKEOP_BORG_BLIP, "combatborg") and the nuclear tactical map action (Grant): every player has
        // the client's minimap instead (DV266, D315)
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:943
    public override async DmTask remove_module(obj_item removed_module = null)
    {
        await base.remove_module(removed_module);
        mob_living_silicon_robot cyborg = loc as mob_living_silicon_robot;
        cyborg.add_faction(FACTION_SILICON); //ai is your bff now!
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:981
public partial class obj_item_robot_model_saboteur : obj_item_robot_model
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1012  (silicons SI9): add_minimap_blip(cyborg, MINIMAP_NUKEOP_BORG_BLIP,
    // "engiborg") and the nuclear tactical map action: every player has the client's minimap instead (DV266, D315)
    public override async DmTask rebuild_modules()
    {
        await base.rebuild_modules();
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1042
    public override async DmTask<object> be_transformed_to(obj_item_robot_model old_model = null, float? forced = null)
    {
        datum_action_cooldown_borg_thermal thermal_vision = Dm.@new<datum_action_cooldown_borg_thermal>(loc);
        object dot = await base.be_transformed_to(old_model, forced);
        if (!Dm.truthy(dot))
            return dot;
        await thermal_vision.Grant(loc as mob);
        thermal_vision_ref = WEAKREF(thermal_vision) as datum_weakref;
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1050
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref thermal_vision_ref);
        return base.Destroy(force);
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:1019
public partial class datum_action_cooldown_borg_thermal : datum_action_cooldown
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1024
    public override async DmTask<object> Activate(atom target = null)
    {
        // tg checks HAS_TRAIT_FROM(owner, COMSIG_LIVING_RESTORE_INITIAL_SIGHT, ACTION_TRAIT), a signal name as the trait, which
        // is never set: the action always turns thermal vision on (kept as in tg)
        if (HAS_TRAIT_FROM(owner, COMSIG_LIVING_RESTORE_INITIAL_SIGHT, ACTION_TRAIT))
        {
            UnregisterSignal(owner, COMSIG_MOB_UPDATE_SIGHT);
            REMOVE_TRAIT(owner, TRAIT_THERMAL_VISION, ACTION_TRAIT);
        }
        else
        {
            RegisterSignal(owner, COMSIG_LIVING_RESTORE_INITIAL_SIGHT, nameof(on_initial_sight)); //order is important as update_sight() is called when the vision trait is added/removed
            ADD_TRAIT(owner, TRAIT_THERMAL_VISION, ACTION_TRAIT);
        }
        return null;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1032
    public override async DmTask Remove(mob remove_from = null)
    {
        UnregisterSignal(owner, COMSIG_LIVING_RESTORE_INITIAL_SIGHT);
        REMOVE_TRAIT(remove_from, TRAIT_THERMAL_VISION, ACTION_TRAIT);
        await base.Remove(remove_from);
    }

    ///Add thermal reddish tint to darker areas
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1038
    [SignalHandler]
    public virtual void on_initial_sight(mob_living source = null)
    {
        source.lighting_color_cutoffs = blend_cutoff_colors(source.lighting_color_cutoffs, Dm.list(25, 8, 5)) as DmList;
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:1054
public partial class obj_item_robot_model_syndicate_kiltborg : obj_item_robot_model_syndicate
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1066
    public override async DmTask<object> be_transformed_to(obj_item_robot_model old_model = null, float? forced = null)
    {
        object dot = await base.be_transformed_to(old_model, forced);
        qdel(robot.radio);
        robot.radio = Dm.@new<obj_item_radio_borg_syndicate>(robot);
        robot.scrambledcodes = TRUE;
        robot.maxHealth = 50; //DIE IN THREE HITS, LIKE A REAL SCOT
        await robot.break_cyborg_slot(3); //YOU ONLY HAVE TWO ITEMS ANYWAY
        obj_item_pinpointer_nuke diskyfinder = Dm.locate<obj_item_pinpointer_nuke>(basic_modules);
        await diskyfinder.attack_self(robot);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1076
    public override async DmTask do_transform_delay() //AUTO-EQUIPPING THESE TOOLS ANY EARLIER CAUSES RUNTIMES OH YEAH
    {
        await base.do_transform_delay();
        await robot.equip_to_slot(Dm.locate<obj_item_claymore_highlander_robot>(basic_modules), 1);
        await robot.equip_to_slot(Dm.locate<obj_item_pinpointer_nuke>(basic_modules), 2);
        await robot.place_on_head(Dm.@new<obj_item_clothing_head_beret_highlander>(robot)); //THE ONLY PART MORE IMPORTANT THAN THE SWORD IS THE HAT
        ADD_TRAIT(robot.hat, TRAIT_NODROP, HIGHLANDER_TRAIT);
    }
}

// ------------------------------------------ Storages
// tg: code/modules/mob/living/silicon/robot/robot_model.dm:1085
public partial class datum_robot_energy_storage : datum
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1095
    [DmParams("model")]
    public override async DmTask New(params object[] __extra)
    {
        obj_item_robot_model model = Dm.arg<obj_item_robot_model>(__extra, 0);
        energy = max_energy;
        if (model != null)
        {
            Dm.combine(ref model.storages, this);
            RegisterSignal(model.robot, COMSIG_MOB_GET_STATUS_TAB_ITEMS, nameof(get_status_tab_item));
            RegisterSignal(model, COMSIG_QDELETING, nameof(unregister_from_model));
        }
        await base.New(__extra);
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1102
    public override object Destroy(float? force = null)
    {
        LAZYCLEARLIST(linked_modules);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1106
    [SignalHandler]
    public virtual void unregister_from_model(obj_item_robot_model model = null)
    {
        if (model != null)
        {
            Dm.remove(ref model.storages, this);
            UnregisterSignal(model.robot, COMSIG_MOB_GET_STATUS_TAB_ITEMS);
        }
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1112
    [SignalHandler]
    public virtual void get_status_tab_item(mob_living_silicon_robot source = null, DmList items = null)
    {
        Dm.append(ref items, Dm.text("[]: []/[]", name, energy, max_energy));
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1116
    public virtual object use_charge(object amount = null)
    {
        if (Dm.num(energy) >= Dm.num(amount))
        {
            energy = Dm.num(energy) - Dm.num(amount);
            if (Dm.num(energy) == 0)
                return TRUE;
            return TRUE;
        }
        else
            return FALSE;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1125
    public virtual void add_charge(object amount = null)
    {
        energy = min(Dm.num(energy) + Dm.num(amount), max_energy);
        foreach (obj_item_stack modules in Dm.each_cast<obj_item_stack>(linked_modules))
            modules.update_appearance(UPDATE_NAME);
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:1130
public partial class datum_robot_energy_storage_material : datum_robot_energy_storage
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:1136
    public override async DmTask New(params object[] __extra)
    {
        max_energy = 60 * SHEET_MATERIAL_AMOUNT;
        await base.New(__extra);
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:504  (the action's static allow_buffer_activate, rules 7.6)
public sealed partial class TgState
{
    internal datum_callback datum_action_toggle_buffer__allow_buffer_activate;
}

// tg: code/modules/mob/living/silicon/robot/robot_model.dm:948  (silicons SI9)
public partial class obj_item_robot_model_syndicate_medical : obj_item_robot_model
{
    // tg: code/modules/mob/living/silicon/robot/robot_model.dm:974: add_minimap_blip(cyborg, MINIMAP_NUKEOP_BORG_BLIP, "mediborg") and the
    // nuclear tactical map action: every player has the client's minimap instead (DV266, D315)
    public override async DmTask rebuild_modules()
    {
        await base.rebuild_modules();
    }
}
