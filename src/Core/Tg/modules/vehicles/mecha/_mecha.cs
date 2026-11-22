// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/_mecha.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The mech's own log (log_message(..., LOG_MECHA)) is written. The chat icons
// ([icon2html(src, occupants)]) are left out of the lines as everywhere (a text chat).

/***************** WELCOME TO MECHA.DM, ENJOY YOUR STAY *****************/

/**
 * Mechs are now (finally) vehicles, this means you can make them multicrew
 * They can also grant select ability buttons based on occupant bitflags
 *
 * Movement is handled through vehicle_move() which is called by relaymove
 * Clicking is done by way of signals registering to the entering mob
 * NOTE: MMIS are NOT mobs but instead contain a brain that is, so you need special checks
 * AI also has special checks becaus it gets in and out of the mech differently
 * Always call remove_occupant(mob) when leaving the mech so the mob is removed properly
 *
 * For multi-crew, you need to set how the occupants receive ability bitflags corresponding to their status on the vehicle(i.e: driver, gunner etc)
 * Abilities can then be set to only apply for certain bitflags and are assigned as such automatically
 *
 * Clicks are wither translated into mech_melee_attack (see mech_melee_attack.dm)
 * Or are used to call action() on equipped gear
 * Cooldown for gear is on the mech because exploits
 * Cooldown for melee is on mech_melee_attack also because exploits
 */
// tg: code/modules/vehicles/mecha/_mecha.dm:22
public partial class obj_vehicle_sealed_mecha : obj_vehicle_sealed
{
    /// Keeps track of the mech's cell
    public obj_item_stock_parts_power_store cell; // tg: code/modules/vehicles/mecha/_mecha.dm:51
    /// Keeps track of the mech's capacitor
    public obj_item_stock_parts_capacitor capacitor; // tg: code/modules/vehicles/mecha/_mecha.dm:55

    // tg: code/modules/vehicles/mecha/_mecha.dm:22
    public obj_vehicle_sealed_mecha()
    {
        name = "exosuit";
        desc = "Exosuit";
        icon = Dm.resource("icons/mob/rideables/mecha.dmi");
        resistance_flags = 34;
        max_integrity = 300;
        armor_type = typeof(datum_armor_sealed_mecha);
        force = 5;
        movedelay = 10;
        move_force = 2999;
        move_resist = 9000;
        light_system = 3;
        light_on = 0;
        light_range = 6;
        generic_canpass = 0;
        mouse_pointer = Dm.resource("icons/effects/mouse_pointers/mecha_mouse.dmi");
        inertia_force_weight = 5;
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:208
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object built_manually = __extra.Length > 0 ? __extra[0] : null;
        object dot = base.Initialize(mapload);
        ui_view = Dm.@new<atom_movable_screen_map_view>();
        ui_view.generate_view(Dm.text("mech_view_[]", REF(this)));
        RegisterSignal(this, COMSIG_MOVABLE_MOVED, nameof(on_move));
        RegisterSignal(this, COMSIG_LIGHT_EATER_ACT, nameof(on_light_eater));

        spark_system = Dm.@new<datum_effect_system_basic_spark_spread>(this, 2, FALSE);
        spark_system.attach(this);

        cabin_air = Dm.@new<datum_gas_mixture>(cabin_volume);

        if (!Dm.truthy(built_manually))
            populate_parts();
        update_access();
        set_wires(Dm.@new<datum_wires_mecha>(this));
        START_PROCESSING(SSobj, this);
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));
        log_message(Dm.text("[] created.", name), LOG_MECHA);
        GLOB.mechas_list.Add(this); //global mech list
        prepare_huds();
        datum_atom_hud_data_diagnostic diag_hud = GLOB.huds[DATA_HUD_DIAGNOSTIC] as datum_atom_hud_data_diagnostic;
        diag_hud.add_atom_to_hud(this);
        diag_hud_set_mechhealth();
        diag_hud_set_mechcell();
        diag_hud_set_mechstat();
        update_appearance();

        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        become_hearing_sensitive(ROUNDSTART_TRAIT);
        add_traits(Dm.list(TRAIT_ASHSTORM_IMMUNE, TRAIT_SNOWSTORM_IMMUNE), ROUNDSTART_TRAIT); //stormy weather (keeps rainin' all the time)
        foreach (object key in Dm.each<object>(equip_by_category.Copy()))
        {
            if (Dm.eq(key, MECHA_L_ARM) || Dm.eq(key, MECHA_R_ARM))
            {
                Type path = equip_by_category[key] as Type;
                if (path == null)
                    continue;
                obj_item_mecha_parts_mecha_equipment thing = Dm.@new(path) as obj_item_mecha_parts_mecha_equipment;
                Waiting.NoSleep(thing.attach(this, Dm.eq(key, MECHA_R_ARM) ? TRUE : FALSE));
                continue;
            }
            foreach (Type path__1 in Dm.each<Type>((equip_by_category[key] as DmList).Copy()))
            {
                obj_item_mecha_parts_mecha_equipment thing = Dm.@new(path__1) as obj_item_mecha_parts_mecha_equipment;
                Waiting.NoSleep(thing.attach(this, FALSE));
                Dm.remove(equip_by_category[key] as DmList, path__1);
            }
        }

        Waiting.NoSleep(AddElement(typeof(datum_element_falling_hazard), Dm.kv("damage", 80), Dm.kv("wound_bonus", 10), Dm.kv("hardhat_safety", FALSE), Dm.kv("crushes", TRUE)));
        Waiting.NoSleep(AddElement(typeof(datum_element_hostile_machine)));
        LocomotionSeam.RegisterMech(this); // DEVIATION(DV358)
        return dot;
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:259
    public override object Destroy(float? force = null)
    {
        // If the former occupants get polymorphed, mutated, chestburstered,
        // or otherwise replaced by another mob, that mob is no longer in .occupants
        // and gets deleted with the mech. However, they do remain in .contents
        DmList potential_occupants = Dm.bitor(contents, occupants) as DmList;
        foreach (mob buggy_ejectee in Dm.each<mob>(potential_occupants))
            Waiting.NoSleep(mob_exit(buggy_ejectee, silent: TRUE, forced: TRUE));

        if (Dm.truthy(LAZYLEN(flat_equipment)))
        {
            foreach (obj_item_mecha_parts_mecha_equipment equip in Dm.each<obj_item_mecha_parts_mecha_equipment>(flat_equipment.Copy()))
            {
                Waiting.NoSleep(equip.detach(loc));
                qdel(equip);
            }
        }

        STOP_PROCESSING(SSobj, this);
        LAZYCLEARLIST(flat_equipment);

        QDEL_NULL(ref ore_box);

        QDEL_NULL(ref cell);
        QDEL_NULL(ref scanmod);
        QDEL_NULL(ref capacitor);
        QDEL_NULL(ref servo);
        QDEL_NULL(ref cabin_air);
        QDEL_NULL(ref spark_system);
        QDEL_NULL(ref ui_view);
        QDEL_LIST(trackers);
        QDEL_NULL(ref chassis_camera);

        GLOB.mechas_list.Remove(this); //global mech list
        datum_atom_hud_data_diagnostic diag_hud = GLOB.huds[DATA_HUD_DIAGNOSTIC] as datum_atom_hud_data_diagnostic;
        diag_hud.remove_atom_from_hud(this); //YEET
        LocomotionSeam.UnregisterMech(this); // DEVIATION(DV358)
        return base.Destroy(force);
    }

    /// Add parts on mech spawning. Skipped in manual construction.
    // tg: code/modules/vehicles/mecha/_mecha.dm:294
    public virtual void populate_parts()
    {
        cell = Dm.@new<obj_item_stock_parts_power_store_cell_high>(this);
        scanmod = Dm.@new<obj_item_stock_parts_scanning_module>(this);
        capacitor = Dm.@new<obj_item_stock_parts_capacitor>(this);
        servo = Dm.@new<obj_item_stock_parts_servo>(this);
        update_part_values();
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:301
    public virtual void locate_parts()
    {
        cell = Dm.locate<obj_item_stock_parts_power_store>(contents);
        diag_hud_set_mechcell();
        scanmod = Dm.locate<obj_item_stock_parts_scanning_module>(contents);
        capacitor = Dm.locate<obj_item_stock_parts_capacitor>(contents);
        servo = Dm.locate<obj_item_stock_parts_servo>(contents);
        update_part_values();
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:310
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = get_mecha_occupancy_state() as string;
        return await base.update_icon_state(__extra);
    }

    /**
     * Toggles Weapons Safety
     *
     * Handles enabling or disabling the safety function.
     */
    // tg: code/modules/vehicles/mecha/_mecha.dm:319
    public virtual void set_safety(mob user = null)
    {
        weapons_safety = Dm.truthy(weapons_safety) ? 0 : 1;
        if (!Dm.truthy(safety_sound_custom))
            Dm.output_to(user, sound(Dm.resource("sound/machines/beep/beep.ogg"), volume: 25)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        balloon_alert(user, I18n.T("equipment []", Dm.truthy(weapons_safety) ? "safe" : "ready"));
        set_mouse_pointer();
        SEND_SIGNAL(COMSIG_MECH_SAFETIES_TOGGLE, Dm.list(this, user, weapons_safety));
    }

    /**
     * Updates the pilot's mouse cursor override.
     *
     * If the mech's weapons safety is enabled, there should be no override, and the user gets their regular mouse cursor. If safety
     * is off but the mech's equipment is disabled (such as by EMP), the cursor should be the red disabled version. Otherwise, if
     * safety is off and the equipment is functional, the cursor should be the regular green cursor. This proc sets the cursor.
     * correct and then updates it for each mob in the occupants list.
     */
    // tg: code/modules/vehicles/mecha/_mecha.dm:334
    public virtual void set_mouse_pointer()
    {
        if (Dm.truthy(weapons_safety))
            mouse_pointer = "";
        else
        {
            if (Dm.truthy(equipment_disabled))
                mouse_pointer = Dm.resource("icons/effects/mouse_pointers/mecha_mouse-disable.dmi");
            else
                mouse_pointer = Dm.resource("icons/effects/mouse_pointers/mecha_mouse.dmi");
        }

        foreach (mob mob_occupant in Dm.each<mob>(occupants))
            mob_occupant.update_mouse_pointer();
    }

    //override this proc if you need to split up mecha control between multiple people (see savannah_ivanov.dm)
    // tg: code/modules/vehicles/mecha/_mecha.dm:347
    public override async DmTask auto_assign_occupant_flags(mob occupant = null)
    {
        if (driver_amount() < max_drivers)
            await add_control_flags(occupant, FULL_MECHA_CONTROL);
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:351
    public override void generate_actions()
    {
        initialize_passenger_action_type(typeof(datum_action_vehicle_sealed_mecha_mech_eject));
        if (Dm.truthy(Dm.bitand(mecha_flags, IS_ENCLOSED)))
            initialize_controller_action_type(typeof(datum_action_vehicle_sealed_mecha_mech_toggle_cabin_seal), VEHICLE_CONTROL_SETTINGS);
        if (Dm.truthy(can_use_overclock) && overclock_action_type != null)
            initialize_passenger_action_type(overclock_action_type);
        initialize_controller_action_type(typeof(datum_action_vehicle_sealed_mecha_mech_toggle_lights), VEHICLE_CONTROL_SETTINGS);
        initialize_controller_action_type(typeof(datum_action_vehicle_sealed_mecha_mech_toggle_safeties), VEHICLE_CONTROL_SETTINGS);
        initialize_controller_action_type(typeof(datum_action_vehicle_sealed_mecha_mech_view_stats), VEHICLE_CONTROL_SETTINGS);
        initialize_controller_action_type(typeof(datum_action_vehicle_sealed_mecha_strafe), VEHICLE_CONTROL_DRIVE);
    }

    /// Generates action buttons for all eligible equipment and grants them to the occupant with VEHICLE_CONTROL_SETTINGS flag.
    // tg: code/modules/vehicles/mecha/_mecha.dm:370
    public virtual async DmTask generate_equipment_actions(mob occupant = null)
    {
        if (!Dm.@in(occupant, occupants) || !Dm.truthy(Dm.bitand(occupants[occupant], VEHICLE_CONTROL_SETTINGS)))
            return;
        foreach (obj_item_mecha_parts_mecha_equipment equipment in Dm.each<obj_item_mecha_parts_mecha_equipment>(flat_equipment))
        {
            if (!is_equipment_valid_for_action(equipment))
                continue;

            await grant_equipment_action(occupant, equipment);
        }
    }

    /// Removes all equipment actions from a specific occupant.
    // tg: code/modules/vehicles/mecha/_mecha.dm:380
    public virtual void remove_all_equipment_actions(mob occupant = null)
    {
        DmList actions = LAZYACCESS(occupant_actions, occupant) as DmList;
        if (actions == null)
            return;

        foreach (object equipment_type in Dm.each<object>(actions.Copy()))
        {
            if (!ispath(equipment_type, typeof(obj_item_mecha_parts_mecha_equipment)))
                continue;
            remove_action_type_from_mob(equipment_type as Type, occupant);
        }
    }

    /**
     * Grants a specific equipment action to an occupant.
     * Creates a new action, sets up the chassis and equipment references, and grants it to the mob.
     */
    // tg: code/modules/vehicles/mecha/_mecha.dm:394
    public virtual async DmTask grant_equipment_action(mob occupant = null, obj_item_mecha_parts_mecha_equipment equipment = null)
    {
        datum_action_vehicle_sealed_mecha_equipment action = Dm.@new(equipment.action_type) as datum_action_vehicle_sealed_mecha_equipment; // We cannot use grant_action_type_to_mob() because:
        action.set_chassis(this);                                                                                                          // 1. grant_action_type_to_mob() works with a single predefined action type
        await action.set_equipment(equipment);                                                                                             // 2. We create unique action instances for each equipment with specific equipment references

        await action.Grant(occupant);
        if (occupant_actions[occupant] == null)
            occupant_actions[occupant] = Dm.list();
        // Use equipment type as actiontype for remove_action_type_from_mob() compatibility
        (occupant_actions[occupant] as DmList)[equipment.type] = action;
    }

    /**
     * Called when equipment is attached to the mecha.
     * Grants equipment actions to current occupants with VEHICLE_CONTROL_SETTINGS flag.
     */
    // tg: code/modules/vehicles/mecha/_mecha.dm:408
    public virtual async DmTask on_equipment_attach(obj_item_mecha_parts_mecha_equipment equipment = null)
    {
        if (!is_equipment_valid_for_action(equipment))
            return;

        foreach (mob occupant in Dm.each<mob>(occupants))
        {
            if (!Dm.truthy(Dm.bitand(occupants[occupant], VEHICLE_CONTROL_SETTINGS)))
                continue;
            await grant_equipment_action(occupant, equipment);
        }
    }

    /**
     * Called when equipment is detached from the mecha.
     * Removes equipment actions from all current occupants.
     */
    // tg: code/modules/vehicles/mecha/_mecha.dm:421
    public virtual void on_equipment_detach(obj_item_mecha_parts_mecha_equipment equipment = null)
    {
        foreach (mob occupant in Dm.each<mob>(occupants))
            remove_action_type_from_mob(equipment.type, occupant);
    }

    /// Create actions only for equipment that can be toggled or triggered, excluding air tanks.
    // tg: code/modules/vehicles/mecha/_mecha.dm:426
    public virtual bool is_equipment_valid_for_action(obj_item_mecha_parts_mecha_equipment equipment = null)
    {
        if (!(Dm.truthy(equipment.can_be_toggled) || Dm.truthy(equipment.can_be_triggered)))
            return false;

        if (istype(equipment, typeof(obj_item_mecha_parts_mecha_equipment_air_tank))) // this thing has its own button
            return false;

        return true;
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:435
    public virtual object get_mecha_occupancy_state()
    {
        if (Dm.truthy(Dm.bitand(mecha_flags, SILICON_PILOT)) && Dm.truthy(silicon_icon_state))
            return silicon_icon_state;
        if (Dm.truthy(LAZYLEN(occupants)))
            return base_icon_state;
        return Dm.text("[]-open", base_icon_state);
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:441
    public override object get_cell(atom_movable @interface = null, mob user = null) => cell;

    // tg: code/modules/vehicles/mecha/_mecha.dm:444
    public override async DmTask<object> rust_heretic_act(object rust_strength = null)
    {
        await take_damage(500, BRUTE);
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:447
    public virtual void restore_equipment()
    {
        equipment_disabled = FALSE;
        foreach (mob mob_occupant in Dm.each<mob>(occupants))
        {
            Dm.output_to(mob_occupant, sound(Dm.resource("sound/items/timer.ogg"), volume: 50)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            to_chat(mob_occupant, span_notice(I18n.T("Equipment control unit has been rebooted successfully.")));
        }
        set_mouse_pointer();
    }

    /// Updates the values given by scanning module and capacitor tier, called when a part is removed or inserted.
    // tg: code/modules/vehicles/mecha/_mecha.dm:456
    public virtual void update_part_values()
    {
        update_energy_drain();

        float initial_danger = Dm.num(Dm.initial(this, nameof(overclock_temp_danger)));
        if (capacitor != null)
            overclock_temp_danger = initial_danger * Dm.num(capacitor.rating);
        else
            overclock_temp_danger = initial_danger;
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:464
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(LAZYLEN(flat_equipment)))
        {
            Dm.append(ref dot, span_notice(I18n.T("It's equipped with:")));
            foreach (obj_item_mecha_parts_mecha_equipment ME in Dm.each<obj_item_mecha_parts_mecha_equipment>(flat_equipment))
            {
                if (istype(ME, typeof(obj_item_mecha_parts_mecha_equipment_concealed_weapon_bay)))
                    continue;
                Dm.append(ref dot, span_notice(I18n.T("\\A [].", ME)));
            }
        }
        if (Dm.truthy(Dm.bitand(mecha_flags, PANEL_OPEN)))
        {
            if (servo != null)
                Dm.append(ref dot, span_notice(I18n.T("Servo reduces movement power usage by []%", 100 - Dm.num(round(100 / Dm.num(servo.rating))))));
            else
                Dm.append(ref dot, span_warning(I18n.T("It's missing a servo.")));
            if (capacitor != null)
                Dm.append(ref dot, span_notice(I18n.T("Capacitor increases armor against energy attacks by [].", Dm.num(capacitor.rating) * 5)));
            else
                Dm.append(ref dot, span_warning(I18n.T("It's missing a capacitor.")));
            if (scanmod == null)
                Dm.append(ref dot, span_warning(I18n.T("It's missing a scanning module.")));
        }
        if (!Dm.truthy(Dm.bitand(mecha_flags, IS_ENCLOSED)))
        {
            if (Dm.truthy(Dm.bitand(mecha_flags, SILICON_PILOT)))
                Dm.append(ref dot, span_notice(I18n.T("[] appears to be piloting itself...", this)));
            else
            {
                foreach (object occupante in Dm.each<object>(occupants))
                    Dm.append(ref dot, span_notice(I18n.T("You can see [] inside.", occupante)));
                if (ishuman(user))
                {
                    mob_living_carbon_human H = user as mob_living_carbon_human;
                    foreach (object held_item in Dm.each<object>(H.held_items))
                    {
                        if (!isgun(held_item))
                            continue;
                        Dm.append(ref dot, span_warning(I18n.T("It looks like you can hit the pilot directly if you target the center or above.")));
                        break; //in case user is holding two guns
                    }
                }
            }
        }
        Dm.append(ref dot, span_notice(I18n.T("It has a <a href='byond://?src=[];list_armor=1'>tag</a> listing its protection classes.", REF(this))));
        // tg: code/modules/vehicles/mecha/mecha_defense.dm:377 (a second definition of the proc, chained by ..())
        if (Dm.truthy(Dm.bitand(mecha_flags, PANEL_OPEN)))
            Dm.append(ref dot, span_notice(I18n.T("The panel is open. You could use a <b>crowbar</b> to eject parts or lock the panel back with a <b>screwdriver</b>.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("You could unlock the maintenance cover with a <b>screwdriver</b>.")));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:498
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        object dot = await base.Topic(href, href_list);

        if (Dm.truthy(href_list["list_armor"]))
        {
            DmList readout = Dm.list();

            datum_armor armor = get_armor() as datum_armor;
            bool added_damage_header = false;
            foreach (object damage_key in Dm.each<object>(Dm.list(BIO, BOMB, BULLET, ENERGY, LASER, MELEE, WOUND)))
            {
                object rating = armor.get_rating(damage_key);
                if (!Dm.truthy(rating))
                    continue;
                if (!added_damage_header)
                {
                    readout.Add(I18n.T("<b><u>ARMOR (I-X)</u></b>"));
                    added_damage_header = true;
                }
                readout.Add(Dm.text("[] []", armor_to_protection_name(damage_key), armor_to_protection_class(rating)));
            }

            bool added_durability_header = false;
            foreach (object durability_key in Dm.each<object>(Dm.list(ACID, FIRE)))
            {
                object rating = armor.get_rating(durability_key);
                if (!Dm.truthy(rating))
                    continue;
                if (!added_durability_header)
                {
                    readout.Add(I18n.T("<b><u>DURABILITY (I-X)</u></b>"));
                    added_durability_header = true;
                }
                readout.Add(Dm.text("[] []", armor_to_protection_name(durability_key), armor_to_protection_class(rating)));
            }

            readout.Add(I18n.T("It can withstand temperatures up to []K.", max_temperature));
            if (Dm.truthy(Dm.bitand(mecha_flags, IS_ENCLOSED)))
                readout.Add(I18n.T("It fully encloses its occupants, protecting them from the atmosphere or lack thereof."));

            string formatted_readout = span_notice(Dm.text("<b>[]</b><hr>[]", I18n.T("PROTECTION CLASSES"), jointext(readout, "\n")));
            to_chat(usr, boxed_message(formatted_readout));
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:532
    public override object generate_integrity_message()
    {
        object examine_text = "";
        float integrity = Dm.num(atom_integrity) / Dm.num(max_integrity) * 100;

        if (Dm.in_to(integrity, 85, 100))
            examine_text = I18n.T("It's fully intact.");
        else if (Dm.in_to(integrity, 65, 85))
            examine_text = I18n.T("It's slightly damaged.");
        else if (Dm.in_to(integrity, 45, 65))
            examine_text = I18n.T("It's badly damaged.");
        else if (Dm.in_to(integrity, 25, 45))
            examine_text = span_warning(I18n.T("It's heavily damaged."));
        else
            examine_text = span_warning(I18n.T("It's falling apart!"));

        return examine_text;
    }

    /// Locate an internal tack in the utility modules
    // tg: code/modules/vehicles/mecha/_mecha.dm:551
    public virtual obj_machinery_portable_atmospherics_canister get_internal_tank()
    {
        obj_item_mecha_parts_mecha_equipment_air_tank module = Dm.locate<obj_item_mecha_parts_mecha_equipment_air_tank>(equip_by_category[MECHA_UTILITY] as DmList);
        return module?.internal_tank;
    }

    //processing internal damage, temperature, air regulation, alert updates, lights power use.
    // tg: code/modules/vehicles/mecha/_mecha.dm:556
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        float dt = Dm.num(seconds_per_tick);
        if (Dm.truthy(overclock_mode) || overclock_temp > 0)
            process_overclock_effects(dt);
        if (Dm.truthy(internal_damage))
            process_internal_damage_effects(dt);
        if (Dm.truthy(cabin_sealed))
            process_cabin_air(dt);
        if (Dm.truthy(length(occupants)))
            process_occupants(dt);
        process_constant_power_usage(dt);
        //Diagnostic HUD updates
        diag_hud_set_mechhealth();
        diag_hud_set_mechcell();
        diag_hud_set_mechstat();
        return null;
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:571
    public virtual void process_overclock_effects(float seconds_per_tick = 0)
    {
        if (!Dm.truthy(overclock_mode) && overclock_temp > 0)
        {
            overclock_temp -= seconds_per_tick;
            return;
        }
        float temp_gain = seconds_per_tick * (1 + 1 / Dm.num(movedelay));
        overclock_temp = Math.Min(overclock_temp + temp_gain, overclock_temp_danger * 2);
        if (overclock_temp < overclock_temp_danger)
            return;
        if (overclock_temp >= overclock_temp_danger && Dm.truthy(overclock_safety))
        {
            Waiting.Detach(toggle_overclock(FALSE));
            return;
        }
        float damage_chance = 100 * ((overclock_temp - overclock_temp_danger) / (overclock_temp_danger * 2));
        if (SPT_PROB(damage_chance, seconds_per_tick))
        {
            do_sparks(5, TRUE, this);
            try_deal_internal_damage(damage_chance);
            Waiting.Detach(take_damage(seconds_per_tick, BURN, 0, 0));
        }
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:588
    public virtual void process_internal_damage_effects(float seconds_per_tick = 0)
    {
        if (Dm.truthy(Dm.bitand(internal_damage, MECHA_INT_FIRE)))
        {
            if (!Dm.truthy(Dm.bitand(internal_damage, MECHA_INT_TEMP_CONTROL)) && SPT_PROB(2.5f, seconds_per_tick))
                clear_internal_damage(MECHA_INT_FIRE);
            if (cabin_air != null && Dm.truthy(cabin_sealed) && Dm.num(cabin_air.return_volume()) > 0)
            {
                if (Dm.num(cabin_air.return_pressure()) > PUMP_DEFAULT_PRESSURE * 30 && !Dm.truthy(Dm.bitand(internal_damage, MECHA_CABIN_AIR_BREACH)))
                    set_internal_damage(MECHA_CABIN_AIR_BREACH);
                cabin_air.temperature = Math.Min(6000 + T0C, Dm.num(cabin_air.temperature) + Dm.num(rand(5, 7.5f)) * seconds_per_tick);
                float cabin_temperature = Dm.num(cabin_air.return_temperature());
                if (cabin_temperature > max_temperature / 2)
                    Waiting.Detach(take_damage(seconds_per_tick * 2 / Dm.num(round(max_temperature / cabin_temperature, 0.1f)), BURN, 0, 0));
            }
        }

        if (Dm.truthy(Dm.bitand(internal_damage, MECHA_CABIN_AIR_BREACH)) && cabin_air != null && Dm.truthy(cabin_sealed)) //remove some air from cabin_air
        {
            datum_gas_mixture leaked_gas = cabin_air.remove_ratio(SPT_PROB_RATE(0.05f, seconds_per_tick)) as datum_gas_mixture;
            if (loc != null)
                Waiting.Detach(loc.assume_air(leaked_gas));
            else
                qdel(leaked_gas);
        }

        if (Dm.truthy(Dm.bitand(internal_damage, MECHA_INT_SHORT_CIRCUIT)) && Dm.truthy(get_charge()))
        {
            Waiting.Detach(spark_system.start());
            float damage_energy_consumption = 0.005f * STANDARD_CELL_CHARGE * seconds_per_tick;
            use_energy(damage_energy_consumption);
            cell.maxcharge = Dm.num(cell.maxcharge) - Math.Min(damage_energy_consumption, Dm.num(cell.maxcharge));
        }
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:612
    public virtual void process_cabin_air(float seconds_per_tick = 0)
    {
        if (!Dm.truthy(Dm.bitand(internal_damage, MECHA_INT_TEMP_CONTROL)) && cabin_air != null && Dm.num(cabin_air.return_volume()) > 0)
        {
            float heat_capacity = Dm.num(cabin_air.heat_capacity());
            float required_energy = Math.Abs(T20C - Dm.num(cabin_air.temperature)) * heat_capacity;
            required_energy = Math.Min(required_energy, 1000);
            if (required_energy < 1)
                return;
            float delta_temperature = required_energy / heat_capacity;
            if (delta_temperature != 0)
            {
                if (Dm.num(cabin_air.temperature) < T20C)
                    cabin_air.temperature = Dm.num(cabin_air.temperature) + delta_temperature;
                else
                    cabin_air.temperature = Dm.num(cabin_air.temperature) - delta_temperature;
            }
        }
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:626
    public virtual void process_occupants(float seconds_per_tick = 0)
    {
        foreach (mob_living occupant__it in Dm.each<mob_living>(occupants.Copy()))
        {
            mob_living occupant = occupant__it;
            if (!Dm.truthy(Dm.bitand(mecha_flags, IS_ENCLOSED)) && Dm.truthy(occupant?.incapacitated)) //no sides mean it's easy to just sorta fall out if you're incapacitated.
            {
                Waiting.Detach(mob_exit(occupant, randomstep: TRUE)); //bye bye
                continue;
            }
            if (cell != null && Dm.truthy(cell.maxcharge))
            {
                float cellcharge = Dm.num(cell.charge) / Dm.num(cell.maxcharge);
                if (cellcharge >= 0.75f)
                    occupant.clear_alert(ALERT_CHARGE);
                else if (Dm.in_to(cellcharge, 0.5f, 0.75f))
                    occupant.throw_alert(ALERT_CHARGE, typeof(atom_movable_screen_alert_lowcell_mech), 1);
                else if (Dm.in_to(cellcharge, 0.25f, 0.5f))
                    occupant.throw_alert(ALERT_CHARGE, typeof(atom_movable_screen_alert_lowcell_mech), 2);
                else if (Dm.in_to(cellcharge, 0.01f, 0.25f))
                    occupant.throw_alert(ALERT_CHARGE, typeof(atom_movable_screen_alert_lowcell_mech), 3);
                else
                    occupant.throw_alert(ALERT_CHARGE, typeof(atom_movable_screen_alert_emptycell_mech));
            }
            else
                occupant.throw_alert(ALERT_CHARGE, typeof(atom_movable_screen_alert_nocell));
            float integrity = Dm.num(atom_integrity) / Dm.num(max_integrity) * 100;
            if (Dm.in_to(integrity, 30, 45))
                occupant.throw_alert(ALERT_MECH_DAMAGE, typeof(atom_movable_screen_alert_low_mech_integrity), 1);
            else if (Dm.in_to(integrity, 15, 35))
                occupant.throw_alert(ALERT_MECH_DAMAGE, typeof(atom_movable_screen_alert_low_mech_integrity), 2);
            else if (integrity <= 15)
                occupant.throw_alert(ALERT_MECH_DAMAGE, typeof(atom_movable_screen_alert_low_mech_integrity), 3);
            else
                occupant.clear_alert(ALERT_MECH_DAMAGE);
            atom checking = occupant.loc;
            // recursive check to handle all cases regarding very nested occupants,
            // such as brainmob inside brainitem inside MMI inside mecha
            while (checking != null)
            {
                if (isturf(checking))
                {
                    // hit a turf before hitting the mecha, seems like they have been moved out
                    occupant.clear_alert(ALERT_CHARGE);
                    occupant.clear_alert(ALERT_MECH_DAMAGE);
                    occupant = null;
                    break;
                }
                else if (checking == this)
                    break;  // all good
                checking = checking.loc;
            }
        }
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:670
    public virtual void process_constant_power_usage(float seconds_per_tick = 0)
    {
        if (Dm.truthy(Dm.bitand(mecha_flags, LIGHTS_ON)) && !Dm.truthy(use_energy(light_power_drain * seconds_per_tick)))
        {
            Dm.mask(ref mecha_flags, Dm.bitnot(LIGHTS_ON));
            Waiting.Detach(set_light_on(Dm.bitand(mecha_flags, LIGHTS_ON)));
            playsound(this, Dm.resource("sound/machines/clockcult/brass_skewer.ogg"), 40, TRUE);
            log_message("Toggled lights off due to the lack of power.", LOG_MECHA);
        }
    }

    /// Called when a driver clicks somewhere. Handles everything like equipment, punches, etc.
    // tg: code/modules/vehicles/mecha/_mecha.dm:678
    public virtual object on_mouseclick(mob user = null, atom target = null, DmList modifiers = null)
    {
        if (Dm.truthy(LAZYACCESS(modifiers, MIDDLE_CLICK)))
        {
            set_safety(user);
            return COMSIG_MOB_CANCEL_CLICKON;
        }
        if (Dm.truthy(weapons_safety))
            return null;
        object dot = null;
        if (isAI(user)) //For AIs: If safeties are off, use mech functions. If safeties are on, use AI functions.
            dot = COMSIG_MOB_CANCEL_CLICKON;
        if (Dm.truthy(modifiers[SHIFT_CLICK])) //Allows things to be examined.
            return dot;
        if (!isturf(target) && !isturf(target.loc)) // Prevents inventory from being drilled
            return dot;
        if (!can_interact_with(target, user, modifiers))
            return dot;
        if (this == target)
            return dot;
        object dir_to_target = get_dir(this, target);
        // DEVIATION(DV358): the view, not a tile step, turns the mech: every target ahead of the pilot's eye is in its reach, as for a walker's hands
        // tg: if(!(mecha_flags & OMNIDIRECTIONAL_ATTACKS) && dir_to_target && !(dir_to_target & dir))//wrong direction
        if (!LocomotionSeam.IsMechWalked(this) && !Dm.truthy(Dm.bitand(mecha_flags, OMNIDIRECTIONAL_ATTACKS)) && Dm.truthy(dir_to_target) && !Dm.truthy(Dm.bitand(dir_to_target, dir)))
            return dot;
        if (Dm.truthy(Dm.bitand(internal_damage, MECHA_INT_CONTROL_LOST)))
            target = pick(view(3, target)) as atom;
        mob_living livinguser = user as mob_living;
        if (!Dm.@in(livinguser, return_controllers_with_flag(VEHICLE_CONTROL_EQUIPMENT)))
        {
            balloon_alert(user, I18n.T("wrong seat for equipment!"));
            return dot;
        }
        Waiting.Detach(mouseclick_act(user, livinguser, target, modifiers));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:704 The rest of on_mouseclick: a signal handler does not sleep, and Adjacent is a task here.
    private async DmTask mouseclick_act(mob user, mob_living livinguser, atom target, DmList modifiers)
    {
        obj_item_mecha_parts_mecha_equipment selected;
        if (Dm.eq(modifiers[BUTTON], RIGHT_CLICK))
            selected = equip_by_category[MECHA_R_ARM] as obj_item_mecha_parts_mecha_equipment;
        else
            selected = equip_by_category[MECHA_L_ARM] as obj_item_mecha_parts_mecha_equipment;
        bool adjacent = Dm.truthy(await Adjacent(target));
        if (selected != null)
        {
            if (!adjacent && Dm.truthy(Dm.bitand(selected.range, MECHA_RANGED)))
            {
                if (HAS_TRAIT(livinguser, TRAIT_PACIFISM) && Dm.truthy(selected.harmful))
                {
                    to_chat(livinguser, span_warning(I18n.T("You don't want to harm other living beings!")));
                    return;
                }
                if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_MECHA_EQUIPMENT_CLICK, Dm.list(this, livinguser, target)), COMPONENT_CANCEL_EQUIPMENT_CLICK)))
                    return;
                Waiting.Detach(selected.action(user, target, modifiers));
                return;
            }
            if (adjacent && Dm.truthy(Dm.bitand(selected.range, MECHA_MELEE)))
            {
                if (isliving(target) && Dm.truthy(selected.harmful) && HAS_TRAIT(livinguser, TRAIT_PACIFISM))
                {
                    to_chat(livinguser, span_warning(I18n.T("You don't want to harm other living beings!")));
                    return;
                }
                if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_MECHA_EQUIPMENT_CLICK, Dm.list(this, livinguser, target)), COMPONENT_CANCEL_EQUIPMENT_CLICK)))
                    return;
                Waiting.Detach(selected.action(user, target, modifiers));
                return;
            }
        }
        if (!Dm.@in(livinguser, return_controllers_with_flag(VEHICLE_CONTROL_MELEE)))
        {
            to_chat(livinguser, span_warning(I18n.T("You're in the wrong seat to interact with your hands.")));
            return;
        }
        bool on_cooldown = Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_MECHA_MELEE_ATTACK));
        if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_MECHA_MELEE_CLICK, Dm.list(this, livinguser, target, on_cooldown ? TRUE : FALSE, adjacent ? TRUE : FALSE)), COMPONENT_CANCEL_MELEE_CLICK)))
            return;
        if (on_cooldown || !adjacent)
            return;
        if (Dm.truthy(Dm.bitand(internal_damage, MECHA_INT_CONTROL_LOST)))
            target = pick(oview(1, this)) as atom;

        if (!has_charge(melee_energy_drain))
            return;
        use_energy(melee_energy_drain);

        user.SEND_SIGNAL(COMSIG_MOB_USED_CLICK_MECH_MELEE, Dm.list(user, this));
        if (Dm.truthy(await target.mech_melee_attack(this, user as mob_living)))
            timer_cooldown_start(COOLDOWN_MECHA_MELEE_ATTACK, melee_cooldown);
    }

    // TIMER_COOLDOWN_START(src, index, time) (__DEFINES/cooldowns.dm:77, it expands addtimer)
    internal void timer_cooldown_start(object cd_index, object cd_time)
    {
        LAZYSET(ref cooldowns, cd_index, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), this, cd_index), Dm.num(cd_time), TIMER_STOPPABLE, file: "code/modules/vehicles/mecha/_mecha.dm", line: 744));
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:746
    public virtual bool can_interact_with(atom target = null, mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(completely_disabled) || Dm.truthy(is_currently_ejecting) || Dm.truthy(Dm.bitand(mecha_flags, CANNOT_INTERACT)))
            return false;
        if (Dm.truthy(user.incapacitated))
            return false;
        if (!Dm.truthy(get_charge()))
            return false;
        return true;
    }

    /// Driver alt clicks anything while in mech
    // tg: code/modules/vehicles/mecha/_mecha.dm:756
    public virtual object on_click_alt(mob user = null, atom target = null, object @params = null)
    {
        object dot = COMSIG_MOB_CANCEL_CLICKON; // Cancel base_click_alt

        if (target != this)
            return dot;

        if (!Dm.@in(user, occupants))
            return dot;

        if (!Dm.@in(user, return_controllers_with_flag(VEHICLE_CONTROL_DRIVE)))
        {
            to_chat(user, span_warning(I18n.T("You're in the wrong seat to control movement.")));
            return dot;
        }

        toggle_strafe();
        return dot;
    }

    /// Middle mouse click signal wrapper for AI users
    // tg: code/modules/vehicles/mecha/_mecha.dm:775
    public virtual void on_middlemouseclick(mob user = null, atom target = null, object @params = null)
    {
        if (isAI(user))
            on_mouseclick(user, target, @params as DmList);
    }

    /// Displays a special speech bubble when someone inside the mecha speaks
    // tg: code/modules/vehicles/mecha/_mecha.dm:781
    public virtual void display_speech_bubble(datum source = null, DmList speech_args = null)
    {
        DmList speech_bubble_recipients = Dm.list();
        foreach (mob listener in Dm.each<mob>(get_hearers_in_view(7, this)))
        {
            if (listener.client != null)
                speech_bubble_recipients.Add(listener.client);
        }

        image mech_speech = image(Dm.resource("icons/mob/effects/talk.dmi"), this, Dm.text("machine[]", say_test(speech_args[SPEECH_MESSAGE])), MOB_LAYER + 1);
        flick_overlay_global(mech_speech, speech_bubble_recipients, 3 * SECONDS);
    }

    // Atmospheric stuff
    // tg: code/modules/vehicles/mecha/_mecha.dm:792
    public override object remove_air(object amount = null)
    {
        if (Dm.truthy(Dm.bitand(mecha_flags, IS_ENCLOSED)) && Dm.truthy(cabin_sealed))
            return cabin_air.remove(amount);
        return base.remove_air(amount);
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:797
    public override async DmTask<object> return_air()
    {
        if (Dm.truthy(Dm.bitand(mecha_flags, IS_ENCLOSED)) && Dm.truthy(cabin_sealed))
            return cabin_air;
        return await base.return_air();
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:802
    public override object return_analyzable_air() => cabin_air;

    /// Fetches pressure of the gas mixture we are using
    // tg: code/modules/vehicles/mecha/_mecha.dm:806
    public virtual async DmTask<object> return_pressure()
    {
        datum_gas_mixture air = await return_air() as datum_gas_mixture;
        return air?.return_pressure();
    }

    /// Fetches temp of the gas mixture we are using
    // tg: code/modules/vehicles/mecha/_mecha.dm:811
    public override async DmTask<object> return_temperature()
    {
        datum_gas_mixture air = await return_air() as datum_gas_mixture;
        return air?.return_temperature();
    }

    /// Makes cabin unsealed, dumping cabin air outside or airtight filling the cabin with external air mix
    // tg: code/modules/vehicles/mecha/_mecha.dm:816
    public virtual async DmTask set_cabin_seal(mob user = null, object cabin_sealed__1 = null)
    {
        if (!Dm.truthy(Dm.bitand(mecha_flags, IS_ENCLOSED)))
        {
            balloon_alert(user, I18n.T("cabin can't be sealed!"));
            log_message("Tried to seal cabin. This mech can't be airtight.", LOG_MECHA);
            return;
        }
        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_MECHA_CABIN_SEAL)))
        {
            balloon_alert(user, I18n.T("on cooldown!"));
            return;
        }
        timer_cooldown_start(COOLDOWN_MECHA_CABIN_SEAL, 1 * SECONDS);

        cabin_sealed = Dm.truthy(cabin_sealed__1) ? 1 : 0;

        datum_gas_mixture environment_air = await loc.return_air() as datum_gas_mixture;
        if (environment_air != null)
        {
            if (Dm.truthy(cabin_sealed))
                // Fill cabin with air
                environment_air.pump_gas_to(cabin_air, environment_air.return_pressure());
            else
            {
                // Dump cabin air
                datum_gas_mixture removed_gases = cabin_air.remove_ratio(1) as datum_gas_mixture;
                if (loc != null)
                    await loc.assume_air(removed_gases);
                else
                    qdel(removed_gases);
            }
        }

        obj_item_mecha_parts_mecha_equipment_air_tank tank = Dm.locate<obj_item_mecha_parts_mecha_equipment_air_tank>(equip_by_category[MECHA_UTILITY] as DmList);
        foreach (mob occupant in Dm.each<mob>(occupants))
        {
            datum_action action = Dm.locate<datum_action_vehicle_sealed_mecha_mech_toggle_cabin_seal>(occupant.actions);
            if (tank != null && Dm.truthy(cabin_sealed) && Dm.truthy(tank.auto_pressurize_on_seal))
            {
                if (!Dm.truthy(tank.active))
                    await tank.set_active(TRUE);
                else
                {
                    action.button_icon_state = "mech_cabin_pressurized";
                    await action.build_all_button_icons();
                }
            }
            else
            {
                action.button_icon_state = Dm.text("mech_cabin_[]", Dm.truthy(cabin_sealed) ? "closed" : "open");
                await action.build_all_button_icons();
            }

            balloon_alert(occupant, I18n.T("cabin []", Dm.truthy(cabin_sealed) ? "sealed" : "unsealed"));
        }
        log_message(Dm.text("Cabin [].", Dm.truthy(cabin_sealed) ? "sealed" : "unsealed"), LOG_MECHA);
        playsound(this, Dm.resource("sound/machines/airlock/airlock.ogg"), 50, TRUE);
    }

    /// Special light eater handling
    // tg: code/modules/vehicles/mecha/_mecha.dm:859
    public virtual object on_light_eater(obj_vehicle_sealed source = null, datum light_eater = null)
    {
        if (Dm.truthy(Dm.bitand(mecha_flags, HAS_LIGHTS)))
        {
            visible_message(span_danger(I18n.T("[]'s lights burn out!", this)));
            Dm.mask(ref mecha_flags, Dm.bitnot(HAS_LIGHTS));
        }
        Waiting.Detach(set_light_on(FALSE));
        foreach (mob occupant in Dm.each<mob>(occupants))
            remove_action_type_from_mob(typeof(datum_action_vehicle_sealed_mecha_mech_toggle_lights), occupant);
        return COMPONENT_BLOCK_LIGHT_EATER;
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:869
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        await base.on_saboteur(source, disrupt_duration);
        if (Dm.truthy(Dm.bitand(mecha_flags, HAS_LIGHTS)) && Dm.truthy(light_on))
        {
            await set_light_on(FALSE);
            return TRUE;
        }
        return null;
    }

    /// Apply corresponding accesses
    // tg: code/modules/vehicles/mecha/_mecha.dm:876
    public virtual void update_access()
    {
        req_access = Dm.truthy(one_access) ? Dm.list() : accesses;
        req_one_access = Dm.truthy(one_access) ? accesses : Dm.list();
    }

    /// Electrocute user from power celll
    // tg: code/modules/vehicles/mecha/_mecha.dm:881
    public override async DmTask<object> shock__proc(mob_living shocking = null, float? chance = null, object shock_source = null, float? siemens_coeff = null)
    {
        chance ??= 100;
        if (Dm.num(get_charge()) < 1)
            return FALSE;
        siemens_coeff ??= 0.7f;
        return await base.shock__proc(shocking, chance, shock_source, siemens_coeff);
    }

    /// Toggle mech overclock with a button or by hacking
    // tg: code/modules/vehicles/mecha/_mecha.dm:889
    public virtual async DmTask<object> toggle_overclock(object forced_state = null)
    {
        if (forced_state != null)
        {
            if (Dm.eq(overclock_mode, forced_state))
                return FALSE;
            overclock_mode = Dm.num(forced_state);
        }
        else
            overclock_mode = Dm.truthy(overclock_mode) ? 0 : 1;

        log_message(Dm.text("Toggled [].", overclock_name), LOG_MECHA);
        foreach (mob occupant in Dm.each<mob>(occupants))
        {
            balloon_alert(occupant, I18n.T("[] []", overclock_name, Dm.truthy(overclock_mode) ? "on" : "off"));
            datum_action act = Dm.locate<datum_action_vehicle_sealed_mecha_mech_overclock>(occupant.actions);
            if (act != null)
                await act.build_all_button_icons(UPDATE_BUTTON_ICON);
        }

        if (Dm.truthy(overclock_mode))
        {
            movedelay = Dm.num(movedelay) / overclock_coeff;
            visible_message(span_notice(I18n.T("[] starts heating up, making humming sounds.", this)));
        }
        else
        {
            movedelay = Dm.num(movedelay) * overclock_coeff;
            visible_message(span_notice(I18n.T("[] cools down and the humming stops.", this)));
        }
        update_energy_drain();
        return TRUE;
    }

    /// Update the energy drain according to parts and status
    // tg: code/modules/vehicles/mecha/_mecha.dm:913
    public virtual void update_energy_drain()
    {
        float initial_step = Dm.num(Dm.initial(this, nameof(step_energy_drain)));
        float initial_melee = Dm.num(Dm.initial(this, nameof(melee_energy_drain)));
        float initial_light = Dm.num(Dm.initial(this, nameof(light_power_drain)));
        if (servo != null)
            step_energy_drain = initial_step / Dm.num(servo.rating);
        else
            step_energy_drain = 2 * initial_step;

        if (Dm.truthy(overclock_mode))
            step_energy_drain *= overclock_coeff;

        if (capacitor != null)
        {
            melee_energy_drain = initial_melee / Dm.num(capacitor.rating);
            light_power_drain = initial_light / Dm.num(capacitor.rating);
        }
        else
        {
            melee_energy_drain = initial_melee;
            light_power_drain = initial_light;
        }
    }

    /// Toggle lights on/off
    // tg: code/modules/vehicles/mecha/_mecha.dm:930
    public virtual async DmTask toggle_lights(object forced_state = null, mob user = null)
    {
        if (!Dm.truthy(Dm.bitand(mecha_flags, HAS_LIGHTS)))
        {
            if (user != null)
                balloon_alert(user, I18n.T("mech has no lights!"));
            return;
        }
        if (!Dm.truthy(Dm.bitand(mecha_flags, LIGHTS_ON)) && !Dm.eq(forced_state, FALSE) && Dm.num(get_charge()) < Dm.num(power_to_energy(light_power_drain, scheduler: SSobj)))
        {
            if (user != null)
                balloon_alert(user, I18n.T("no power for lights!"));
            return;
        }
        mecha_flags = Dm.bitxor(mecha_flags, LIGHTS_ON);
        await set_light_on(Dm.bitand(mecha_flags, LIGHTS_ON));
        playsound(this, Dm.resource("sound/machines/clockcult/brass_skewer.ogg"), 40, TRUE);
        log_message(Dm.text("Toggled lights [].", Dm.truthy(Dm.bitand(mecha_flags, LIGHTS_ON)) ? "on" : "off"), LOG_MECHA);
        foreach (mob occupant in Dm.each<mob>(occupants))
        {
            datum_action act = Dm.locate<datum_action_vehicle_sealed_mecha_mech_toggle_lights>(occupant.actions);
            if (Dm.truthy(Dm.bitand(mecha_flags, LIGHTS_ON)))
                act.button_icon_state = "mech_lights_on";
            else
                act.button_icon_state = "mech_lights_off";
            balloon_alert(occupant, I18n.T("lights []", Dm.truthy(Dm.bitand(mecha_flags, LIGHTS_ON)) ? "on" : "off"));
            await act.build_all_button_icons();
        }
    }

    // tg: code/modules/vehicles/mecha/_mecha.dm:952
    public virtual async DmTask melee_attack_effect(mob_living victim = null, object heavy = null)
    {
        if (Dm.truthy(heavy))
            await victim.Unconscious(2 * SECONDS);
        else
            await victim.Knockdown(4 * SECONDS);
    }
}
