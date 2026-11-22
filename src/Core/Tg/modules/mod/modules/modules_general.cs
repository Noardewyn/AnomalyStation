// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/modules_general.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole but three leaves. The general MOD modules: storage (the suit's own storage, holding the suit
// storage item when the chestplate comes off), the ion jetpack (stabilized or not, thrust paid from the suit's charge) and its
// advanced kind, the jump jet (a powered leap one level up), the status readout (the wearer's vitals, DNA and diseases in the
// window, a flatline on death, multi-level suit sensors), the eating apparatus, the EMP shield and its advanced kind, the
// flashlight (colour and range set in the window) and the flashdark, the burger dispenser, longfall (no fall damage), the thermal
// regulator, the DNA lock, the plasma stabilizer, the hat stabilizer, the sign language radio, the joint torsion ratchet (charge
// from walking), the recycler (trash walked over becomes sheets) and its riot dart kind, the fishing glove and the shock
// absorber. Left with rows (not-translated.md 219): the fishing glove's rod (the profound_fisher component, :1013, :1022 and :1028)
// and its easier fishing (the adjust_fishing_difficulty element, :1020 and :1027), fishing being unported. The var blocks are
// generated (D111).

///Storage - Adds a storage component to the suit.
// tg: code/modules/mod/modules/modules_general.dm:4
public partial class obj_item_mod_module_storage : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (storage_type != null)
        {
            Waiting.NoSleep(create_storage(storage_type: storage_type));
            atom_storage.set_locked(STORAGE_FULLY_LOCKED);
        }
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:22
    public override async DmTask on_install()
    {
        await base.on_install();
        datum_storage modstorage = await mod.create_storage(storage_type: storage_type) as datum_storage;
        await modstorage.set_real_location(this);
        atom_storage.set_locked(STORAGE_NOT_LOCKED);
        obj_item_clothing suit = mod.get_part_from_slot(ITEM_SLOT_OCLOTHING) as obj_item_clothing;
        if (istype(suit, typeof(obj_item_clothing)))
            RegisterSignal(suit, COMSIG_ITEM_PRE_UNEQUIP, nameof(on_suit_unequip));
        // You could easily allow modsuits to be strip accessible like backpacks like so
        // But we figured there should be special handling for stuff like DNA locks first
        // mod.AddElement(/datum/element/strip_accessible_storage)
    }

    // tg: code/modules/mod/modules/modules_general.dm:34
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        atom_storage.set_locked(STORAGE_FULLY_LOCKED);
        QDEL_NULL(ref mod.atom_storage);
        if (!Dm.truthy(deleting))
            await atom_storage.remove_all(mod.drop_location() as atom);
        obj_item_clothing suit = mod.get_part_from_slot(ITEM_SLOT_OCLOTHING) as obj_item_clothing;
        if (istype(suit, typeof(obj_item_clothing)))
            UnregisterSignal(suit, COMSIG_ITEM_PRE_UNEQUIP);
        // mod.RemoveElement(/datum/element/strip_accessible_storage)
    }

    // tg: code/modules/mod/modules/modules_general.dm:45
    public virtual async DmTask on_suit_unequip(obj_item source = null, object force = null, atom newloc = null, object no_move = null, object invdrop = null, object silent = null)
    {
        if (QDELETED(source) || !Dm.truthy(invdrop) || mod.wearer == null || newloc == mod.wearer || mod.wearer.s_store == null)
            return;
        if (!Dm.truthy(atom_storage?.attempt_insert(mod.wearer.s_store, mod.wearer, @override: TRUE)))
        {
            balloon_alert(mod.wearer, I18n.T("storage failed!"));
            to_chat(mod.wearer, span_warning(I18n.T("[] fails to store [] inside itself!", this, mod.wearer.s_store)));
            return;
        }
        to_chat(mod.wearer, span_notice(I18n.T("[] stores [] inside itself.", this, mod.wearer.s_store)));
        await mod.wearer.temporarilyRemoveItemFromInventory(mod.wearer.s_store);
    }
}

///Ion Jetpack - Lets the user fly freely through space using battery charge.
// tg: code/modules/mod/modules/modules_general.dm:93
public partial class obj_item_mod_module_jetpack : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:115
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        thrust_callback = Dm.@new<datum_callback>(this, nameof(allow_thrust));
        Waiting.NoSleep(configure_jetpack(stabilize));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:120
    public override object Destroy(float? force = null)
    {
        thrust_callback = null;
        return base.Destroy(force);
    }

    /// configures/re-configures the jetpack component
    // tg: code/modules/mod/modules/modules_general.dm:130
    public virtual async DmTask configure_jetpack(object stabilize = null)
    {
        this.stabilize = Dm.num(stabilize);

        await AddComponent(
            typeof(datum_component_jetpack),
            this.stabilize,
            drift_force,
            COMSIG_MODULE_TRIGGERED,
            COMSIG_MODULE_DEACTIVATED,
            MOD_ABORT_USE,
            thrust_callback,
            thrust_callback,
            typeof(datum_effect_system_trail_follow_ion_grav_allowed)
        );

        if ((mod != null) && (mod.wearer != null) && mod.wearer.get_item_by_slot(slot_flags) == this)
        {
            if (!Dm.truthy(stabilize))
                ADD_TRAIT(mod.wearer, TRAIT_NOGRAV_ALWAYS_DRIFT, REF(this));
            else
                REMOVE_TRAIT(mod.wearer, TRAIT_NOGRAV_ALWAYS_DRIFT, REF(this));
        }
    }

    // tg: code/modules/mod/modules/modules_general.dm:151
    public override object get_configuration(mob user = null)
    {
        DmList dot = base.get_configuration(user) as DmList;
        dot["stabilizers"] = add_ui_configuration(I18n.T("Stabilizers"), "bool", stabilize);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:155
    public override async DmTask configure_edit(object key = null, object value = null)
    {
        if (Dm.eq(key, "stabilizers"))
            await configure_jetpack(text2num(value));
    }

    // tg: code/modules/mod/modules/modules_general.dm:160
    public virtual async DmTask<object> allow_thrust(float? use_fuel = null)
    {
        use_fuel ??= TRUE;
        if (!Dm.truthy(use_fuel))
            return check_power(use_energy_cost);
        if (!Dm.truthy(await drain_power(use_energy_cost)))
            return FALSE;
        return TRUE;
    }

    /// The port's own (the jetpack component's preview_stabilize, jetpack.cs): allow_thrust with use_fuel FALSE, which only reads.
    internal bool preview_thrust() => Dm.truthy(check_power(use_energy_cost));

    // tg: code/modules/mod/modules/modules_general.dm:167
    public override async DmTask on_activation(mob activator = null)
    {
        await mod.wearer.add_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_full_speed));
        if (!Dm.truthy(stabilize))
            ADD_TRAIT(mod.wearer, TRAIT_NOGRAV_ALWAYS_DRIFT, REF(this));
    }

    // tg: code/modules/mod/modules/modules_general.dm:172
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        mod.wearer.remove_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_full_speed));
        REMOVE_TRAIT(mod.wearer, TRAIT_NOGRAV_ALWAYS_DRIFT, REF(this));
    }
}

///Jump Jet - Briefly removes the effect of gravity and pushes you up one z-level if possible.
// tg: code/modules/mod/modules/modules_general.dm:189
public partial class obj_item_mod_module_jump_jet : obj_item_mod_module
{
    /// Cooldown to use if we didn't actually launch a jump jet
    // tg: code/modules/mod/modules/modules_general.dm:186 (file-local, #undef at :222)
    private const float FAILED_ACTIVATION_COOLDOWN = 3 * SECONDS;

    // tg: code/modules/mod/modules/modules_general.dm:201
    public override async DmTask<object> on_use(mob activator = null)
    {
        if (Dm.truthy(DOING_INTERACTION(mod.wearer, mod.wearer)))
        {
            balloon_alert(activator, I18n.T("busy!"));
            return null;
        }
        balloon_alert(mod.wearer, I18n.T("launching..."));
        mod.wearer.Shake(duration: 1 * SECONDS);
        if (!Dm.truthy(await do_after(mod.wearer, 1 * SECONDS, target: mod.wearer)))
        {
            start_cooldown(FAILED_ACTIVATION_COOLDOWN); // Don't go on full cooldown if we failed to launch
            return FALSE;
        }
        playsound(mod.wearer, Dm.resource("sound/vehicles/rocketlaunch.ogg"), 100, TRUE);
        await mod.wearer.apply_status_effect(typeof(datum_status_effect_jump_jet));
        turf launch_from = get_turf(mod.wearer);
        if (Dm.truthy(await mod.wearer.zMove(UP, z_move_flags: ZMOVE_CHECK_PULLS)))
            launch_from.visible_message(span_warning(I18n.T("[] rockets into the air!", mod.wearer)));
        Dm.@new<obj_effect_temp_visual_jet_plume>(launch_from);

        obj_item_mod_module_jetpack linked_jetpack = Dm.locate<obj_item_mod_module_jetpack>(mod.modules);
        if ((linked_jetpack != null) && !Dm.truthy(linked_jetpack.active))
            await linked_jetpack.on_activation();
        return TRUE;
    }
}

///Status Readout - Puts a lot of information including health, nutrition, fingerprints, temperature to the suit TGUI.
// tg: code/modules/mod/modules/modules_general.dm:225
public partial class obj_item_mod_module_status_readout : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:252
    public override object add_ui_data()
    {
        DmList dot = base.add_ui_data() as DmList;
        dot["display_time"] = display_time;
        dot["shift_time"] = round_timestamp();
        dot["shift_id"] = GLOB.round_id;
        dot["health"] = mod.wearer?.health is var or__1 && Dm.truthy(or__1) ? or__1 : 0;
        dot["health_max"] = mod.wearer?.getMaxHealth() is var or__2 && Dm.truthy(or__2) ? or__2 : 0;
        if (Dm.truthy(display_detailed_vitals))
        {
            dot["loss_brute"] = mod.wearer?.get_brute_loss() is var or__3 && Dm.truthy(or__3) ? or__3 : 0;
            dot["loss_fire"] = mod.wearer?.get_fire_loss() is var or__4 && Dm.truthy(or__4) ? or__4 : 0;
            dot["loss_tox"] = mod.wearer?.get_tox_loss() is var or__5 && Dm.truthy(or__5) ? or__5 : 0;
            dot["loss_oxy"] = mod.wearer?.get_oxy_loss() is var or__6 && Dm.truthy(or__6) ? or__6 : 0;
            dot["body_temperature"] = mod.wearer?.bodytemperature is var or__7 && Dm.truthy(or__7) ? or__7 : 0;
            dot["nutrition"] = mod.wearer?.nutrition is var or__8 && Dm.truthy(or__8) ? or__8 : 0;
        }
        if (Dm.truthy(display_dna))
        {
            dot["dna_unique_identity"] = mod.wearer != null ? md5(mod.wearer.dna.unique_identity) : null;
            dot["dna_unique_enzymes"] = mod.wearer?.dna.unique_enzymes;
        }
        dot["viruses"] = null;
        if (!Dm.truthy(length(mod.wearer?.diseases)))
            return dot;
        DmList viruses = Dm.list();
        foreach (datum_disease virus in Dm.each_cast<datum_disease>(mod.wearer.diseases))
        {
            DmList virus_data = Dm.list();
            virus_data["name"] = virus.name;
            virus_data["type"] = virus.spread_text;
            virus_data["stage"] = virus.stage;
            virus_data["maxstage"] = virus.max_stages;
            virus_data["cure"] = virus.cure_text;
            Dm.append(ref viruses, Dm.list(virus_data));
        }
        dot["viruses"] = viruses;

        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:285
    public override object get_configuration(mob user = null)
    {
        DmList dot = base.get_configuration(user) as DmList;
        dot["display_detailed_vitals"] = add_ui_configuration(I18n.T("Detailed Vitals"), "bool", display_detailed_vitals);
        dot["display_dna"] = add_ui_configuration(I18n.T("DNA Information"), "bool", display_dna);
        dot["sensor_boost"] = add_ui_configuration(I18n.T("Suit Sensor Booster"), "bool", sensor_boost);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:291
    public override async DmTask configure_edit(object key = null, object value = null)
    {
        if (Dm.eq(key, "display_detailed_vitals"))
            display_detailed_vitals = Dm.num(text2num(value));
        else if (Dm.eq(key, "display_dna"))
            display_dna = Dm.num(text2num(value));
        else if (Dm.eq(key, "sensor_boost"))
        {
            sensor_boost = Dm.num(text2num(value));
            update_sensor_booster();
        }
    }

    // tg: code/modules/mod/modules/modules_general.dm:301
    public override async DmTask on_part_activation()
    {
        RegisterSignal(mod.wearer, COMSIG_LIVING_DEATH, nameof(death_sound__proc));
        update_sensor_booster();
    }

    // tg: code/modules/mod/modules/modules_general.dm:305
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        UnregisterSignal(mod.wearer, COMSIG_LIVING_DEATH);
        REMOVE_TRAIT(mod.wearer, TRAIT_MULTIZ_SUIT_SENSORS, REF(this));
    }

    // tg: code/modules/mod/modules/modules_general.dm:309
    public virtual void update_sensor_booster()
    {
        if (Dm.truthy(sensor_boost))
            ADD_TRAIT(mod.wearer, TRAIT_MULTIZ_SUIT_SENSORS, REF(this));
        else
            REMOVE_TRAIT(mod.wearer, TRAIT_MULTIZ_SUIT_SENSORS, REF(this));
    }

    // tg: code/modules/mod/modules/modules_general.dm:315
    [SignalHandler]
    public virtual void death_sound__proc(mob_living_carbon_human wearer = null)
    {
        if (Dm.truthy(death_sound) && Dm.truthy(death_sound_volume))
            playsound(wearer, death_sound, death_sound_volume, FALSE);
    }
}

///Eating Apparatus - Lets the user eat/drink with the suit on.
// tg: code/modules/mod/modules/modules_general.dm:321
public partial class obj_item_mod_module_mouthhole : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:340
    public override async DmTask on_install()
    {
        await base.on_install();
        obj_item_clothing helmet = mod.get_part_from_slot(ITEM_SLOT_HEAD) as obj_item_clothing;
        if (istype(helmet, typeof(obj_item_clothing)))
        {
            former_helmet_flags = Dm.num(helmet.flags_cover);
            former_visor_helmet_flags = Dm.num(helmet.visor_flags_cover);
            helmet.flags_cover = Dm.num(Dm.bitand(helmet.flags_cover, Dm.bitnot(Dm.bitor(HEADCOVERSMOUTH, PEPPERPROOF))));
            helmet.visor_flags_cover = Dm.num(Dm.bitand(helmet.visor_flags_cover, Dm.bitnot(Dm.bitor(HEADCOVERSMOUTH, PEPPERPROOF))));
        }
        obj_item_clothing mask = mod.get_part_from_slot(ITEM_SLOT_MASK) as obj_item_clothing;
        if (istype(mask, typeof(obj_item_clothing)))
        {
            former_mask_flags = Dm.num(mask.flags_cover);
            former_visor_mask_flags = Dm.num(mask.visor_flags_cover);
            mask.flags_cover = Dm.num(Dm.bitand(mask.flags_cover, Dm.bitnot(Dm.bitor(MASKCOVERSMOUTH, PEPPERPROOF))));
            mask.visor_flags_cover = Dm.num(Dm.bitand(mask.visor_flags_cover, Dm.bitnot(Dm.bitor(MASKCOVERSMOUTH, PEPPERPROOF))));
        }
    }

    // tg: code/modules/mod/modules/modules_general.dm:355
    public override object can_install(obj_item_mod_control mod = null)
    {
        obj_item_clothing helmet = mod.get_part_from_slot(ITEM_SLOT_HEAD) as obj_item_clothing;
        obj_item_clothing mask = mod.get_part_from_slot(ITEM_SLOT_MASK) as obj_item_clothing;
        if (istype(helmet, typeof(obj_item_clothing)) && Dm.truthy(Dm.bitand(Dm.bitor(helmet.flags_cover, helmet.visor_flags_cover), Dm.bitor(HEADCOVERSMOUTH, PEPPERPROOF))))
            return base.can_install(mod);
        if (istype(mask, typeof(obj_item_clothing)) && Dm.truthy(Dm.bitand(Dm.bitor(mask.flags_cover, mask.visor_flags_cover), Dm.bitor(MASKCOVERSMOUTH, PEPPERPROOF))))
            return base.can_install(mod);
        return FALSE;
    }

    // tg: code/modules/mod/modules/modules_general.dm:364
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        if (Dm.truthy(deleting))
            return;
        obj_item_clothing helmet = mod.get_part_from_slot(ITEM_SLOT_HEAD) as obj_item_clothing;
        if (istype(helmet, typeof(obj_item_clothing)))
        {
            helmet.flags_cover = Dm.num(Dm.bitor(helmet.flags_cover, former_helmet_flags));
            helmet.visor_flags_cover = Dm.num(Dm.bitor(helmet.visor_flags_cover, former_visor_helmet_flags));
        }
        obj_item_clothing mask = mod.get_part_from_slot(ITEM_SLOT_MASK) as obj_item_clothing;
        if (istype(mask, typeof(obj_item_clothing)))
        {
            mask.flags_cover = Dm.num(Dm.bitor(mask.flags_cover, former_mask_flags));
            mask.visor_flags_cover = Dm.num(Dm.bitor(mask.visor_flags_cover, former_visor_mask_flags));
        }
    }
}

///EMP Shield - Protects the suit from EMPs.
// tg: code/modules/mod/modules/modules_general.dm:378
public partial class obj_item_mod_module_emp_shield : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:392
    public override async DmTask on_install()
    {
        await base.on_install();
        await mod.AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL);
        DmList all_parts = mod.get_parts() as DmList;
        foreach (obj_item_clothing mod_part in Dm.each_cast<obj_item_clothing>(all_parts))
            mod_part.emp_protection = Dm.num(mod_part.emp_protection) + protection_factor / all_parts.Count;
    }

    // tg: code/modules/mod/modules/modules_general.dm:399
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        mod.RemoveElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL);
        DmList all_parts = mod.get_parts() as DmList;
        foreach (obj_item_clothing mod_part in Dm.each_cast<obj_item_clothing>(all_parts))
            mod_part.emp_protection = Dm.num(mod_part.emp_protection) - protection_factor / all_parts.Count;
    }
}

// tg: code/modules/mod/modules/modules_general.dm:406
public partial class obj_item_mod_module_emp_shield_advanced : obj_item_mod_module_emp_shield
{
    // tg: code/modules/mod/modules/modules_general.dm:413
    public override async DmTask on_part_activation()
    {
        await mod.wearer.AddElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_SELF, EMP_PROTECT_CONTENTS));
    }

    // tg: code/modules/mod/modules/modules_general.dm:416
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        mod.wearer.RemoveElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_SELF, EMP_PROTECT_CONTENTS));
    }
}

///Flashlight - Gives the suit a customizable flashlight.
// tg: code/modules/mod/modules/modules_general.dm:420
public partial class obj_item_mod_module_flashlight : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:445
    public override async DmTask on_activation(mob activator = null)
    {
        set_light_flags(Dm.bitor(light_flags, LIGHT_ATTACHED));
        await set_light_on(active);
        active_power_cost = base_power * Dm.num(light_range);
    }

    // tg: code/modules/mod/modules/modules_general.dm:450
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        set_light_flags(Dm.bitand(light_flags, Dm.bitnot(LIGHT_ATTACHED)));
        await set_light_on(active);
    }

    // tg: code/modules/mod/modules/modules_general.dm:454
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        object dot = await base.on_saboteur(source, disrupt_duration);
        if (Dm.truthy(active))
        {
            await on_deactivation();
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:460
    public override async DmTask<object> on_process(object seconds_per_tick = null)
    {
        active_power_cost = base_power * Dm.num(light_range);
        return await base.on_process(seconds_per_tick);
    }

    // tg: code/modules/mod/modules/modules_general.dm:464
    public override object generate_worn_overlay(obj_item source = null, mutable_appearance standing = null)
    {
        object dot = base.generate_worn_overlay(source, standing);
        if (!Dm.truthy(active))
            return dot;
        mutable_appearance light_icon = mutable_appearance(overlay_icon_file, "module_light_on", layer: standing.layer + 0.2f, appearance_flags: RESET_COLOR) as mutable_appearance;
        light_icon.color = light_color;
        Dm.append(ref dot, light_icon);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:472
    public override object get_configuration(mob user = null)
    {
        DmList dot = base.get_configuration(user) as DmList;
        dot["light_color"] = add_ui_configuration(I18n.T("Light Color"), "color", light_color);
        dot["light_range"] = add_ui_configuration(I18n.T("Light Range"), "number", light_range);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:477
    public override async DmTask configure_edit(object key = null, object value = null)
    {
        if (Dm.eq(key, "light_color"))
        {
            value = await tgui_color_picker(usr, I18n.T("Pick new light color"), I18n.T("Flashlight Color"));
            if (!Dm.truthy(value))
                return;
            if (Dm.truthy(is_color_dark(value, 50)))
            {
                balloon_alert(mod.wearer, I18n.T("too dark!"));
                return;
            }
            set_light_color(value);
            await update_clothing_slots();
        }
        else if (Dm.eq(key, "light_range"))
            set_light_range(clamp(Dm.num(value), Dm.num(min_range), Dm.num(max_range)));
    }
}

///Like the flashlight module, except the light color is stuck to black and cannot be changed.
// tg: code/modules/mod/modules/modules_general.dm:492
public partial class obj_item_mod_module_flashlight_darkness : obj_item_mod_module_flashlight
{
    // tg: code/modules/mod/modules/modules_general.dm:502
    public override object get_configuration(mob user = null)
    {
        object dot = base.get_configuration(user);
        Dm.remove(ref dot, "light_color");
        return dot;
    }
}

///Dispenser - Dispenses an item after a time passes.
// tg: code/modules/mod/modules/modules_general.dm:507
public partial class obj_item_mod_module_dispenser : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:525
    public override async DmTask<object> on_use(mob activator = null)
    {
        if (Dm.truthy(dispense_time) && !Dm.truthy(await do_after(mod.wearer, dispense_time, target: mod)))
        {
            balloon_alert(mod.wearer, I18n.T("interrupted!"));
            return FALSE;
        }
        obj_item dispensed = Dm.@new(dispense_type, mod.wearer.loc) as obj_item;
        await mod.wearer.put_in_hands(dispensed);
        balloon_alert(activator, I18n.T("[] dispensed", dispensed));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 100, TRUE);
        await drain_power(use_energy_cost);
        return dispensed;
    }
}

///Longfall - Nullifies fall damage, removing charge instead.
// tg: code/modules/mod/modules/modules_general.dm:537
public partial class obj_item_mod_module_longfall : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:550
    public override async DmTask on_part_activation()
    {
        RegisterSignal(mod.wearer, COMSIG_LIVING_Z_IMPACT, nameof(z_impact_react));
    }

    // tg: code/modules/mod/modules/modules_general.dm:553
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        UnregisterSignal(mod.wearer, COMSIG_LIVING_Z_IMPACT);
    }

    // tg: code/modules/mod/modules/modules_general.dm:556
    [SignalHandler]
    public virtual object z_impact_react(datum source = null, object levels = null, turf fell_on = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(drain_power(use_energy_cost * Dm.num(levels)))))
            return NONE;
        Dm.@new<obj_effect_temp_visual_mook_dust>(fell_on);

        /// Boolean that tracks whether we fell more than one z-level. If TRUE, we stagger our wearer.
        bool extreme_fall = false;

        if (Dm.num(levels) >= 2)
        {
            extreme_fall = true;
            Waiting.NoSleep(mod.wearer.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * Dm.num(levels), 10 * SECONDS));
        }

        mod.wearer.visible_message(
            span_notice(extreme_fall
                ? I18n.T("[] lands on [] safely, but barely manages to stay on [] feet.", mod.wearer, fell_on, p_their())
                : I18n.T("[] lands on [] safely, and quite stylishly on [] feet.", mod.wearer, fell_on, p_their())),
            span_notice(I18n.T("[] protects you from the damage!", this))
        );
        return Dm.bitor(Dm.bitor(ZIMPACT_CANCEL_DAMAGE, ZIMPACT_NO_MESSAGE), ZIMPACT_NO_SPIN);
    }
}

///Thermal Regulator - Regulates the wearer's core temperature.
// tg: code/modules/mod/modules/modules_general.dm:576
public partial class obj_item_mod_module_thermal_regulator : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:595
    public override object get_configuration(mob user = null)
    {
        DmList dot = base.get_configuration(user) as DmList;
        dot["temperature_setting"] = add_ui_configuration(I18n.T("Temperature"), "number", temperature_setting - T0C);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:599
    public override async DmTask configure_edit(object key = null, object value = null)
    {
        if (Dm.eq(key, "temperature_setting"))
            temperature_setting = clamp(Dm.num(value) + T0C, min_temp, max_temp);
    }

    // tg: code/modules/mod/modules/modules_general.dm:604
    public override async DmTask on_active_process(object seconds_per_tick = null)
    {
        mod.wearer.adjust_bodytemperature(get_temp_change_amount(temperature_setting - Dm.num(mod.wearer.bodytemperature), BODYTEMP_SUIT_CHANGE_RATE * Dm.num(seconds_per_tick)));
    }
}

///DNA Lock - Prevents people without the set DNA from activating the suit.
// tg: code/modules/mod/modules/modules_general.dm:608
public partial class obj_item_mod_module_dna_lock : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:623
    public override async DmTask on_install()
    {
        await base.on_install();
        RegisterSignal(mod, COMSIG_MOD_ACTIVATE, nameof(on_mod_activation));
        RegisterSignal(mod, COMSIG_MOD_MODULE_REMOVAL, nameof(on_mod_removal));
        RegisterSignal(mod, COMSIG_ATOM_EMP_ACT, nameof(on_emp));
        RegisterSignal(mod, COMSIG_ATOM_EMAG_ACT, nameof(on_emag));
    }

    // tg: code/modules/mod/modules/modules_general.dm:630
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        UnregisterSignal(mod, COMSIG_MOD_ACTIVATE);
        UnregisterSignal(mod, COMSIG_MOD_MODULE_REMOVAL);
        UnregisterSignal(mod, COMSIG_ATOM_EMP_ACT);
        UnregisterSignal(mod, COMSIG_ATOM_EMAG_ACT);
    }

    // tg: code/modules/mod/modules/modules_general.dm:637
    public override async DmTask<object> on_use(mob activator = null)
    {
        dna = mod.wearer.dna.unique_enzymes;
        balloon_alert(activator, I18n.T("dna updated"));
        await drain_power(use_energy_cost);
        return null;
    }

    // tg: code/modules/mod/modules/modules_general.dm:642
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        on_emp(this, severity, dot);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:646
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        return on_emag(this, user, emag_card);
    }

    // tg: code/modules/mod/modules/modules_general.dm:650
    public virtual object dna_check(mob user = null)
    {
        if (!iscarbon(user))
            return FALSE;
        mob_living_carbon carbon_user = user as mob_living_carbon;
        if (!Dm.truthy(dna) || (Dm.truthy(carbon_user.has_dna()) && Dm.eq(carbon_user.dna.unique_enzymes, dna)))
            return TRUE;
        balloon_alert(user, I18n.T("dna locked!"));
        return FALSE;
    }

    // tg: code/modules/mod/modules/modules_general.dm:659
    [SignalHandler]
    public virtual void on_emp(datum source = null, object severity = null, object protection = null)
    {
        if (Dm.truthy(Dm.bitand(protection, EMP_PROTECT_SELF)))
            return;
        dna = null;
    }

    // tg: code/modules/mod/modules/modules_general.dm:665
    [SignalHandler]
    public virtual object on_emag(datum source = null, mob user = null, obj_item_card_emag emag_card = null)
    {
        dna = null;
        return TRUE;
    }

    // tg: code/modules/mod/modules/modules_general.dm:671
    [SignalHandler]
    public virtual object on_mod_activation(datum source = null, mob user = null)
    {
        if (!Dm.truthy(dna_check(user)))
            return MOD_CANCEL_ACTIVATE;
        return null;
    }

    // tg: code/modules/mod/modules/modules_general.dm:677
    [SignalHandler]
    public virtual object on_mod_removal(datum source = null, mob user = null)
    {
        if (!Dm.truthy(dna_check(user)))
            return MOD_CANCEL_REMOVAL;
        return null;
    }
}

///Plasma Stabilizer - Prevents plasmamen from igniting in the suit
// tg: code/modules/mod/modules/modules_general.dm:684
public partial class obj_item_mod_module_plasma_stabilizer : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:698
    public override object generate_worn_overlay(obj_item source = null, mutable_appearance standing = null)
    {
        object dot = base.generate_worn_overlay(source, standing);
        if (!Dm.truthy(dot))
            return dot;

        mutable_appearance visor_overlay = mod.get_visor_overlay(standing) as mutable_appearance;
        visor_overlay.appearance_flags = Dm.num(Dm.bitor(visor_overlay.appearance_flags, RESET_COLOR));
        visor_overlay.color = COLOR_VIOLET;
        Dm.append(ref dot, visor_overlay);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:708
    public override void on_equip()
    {
        ADD_TRAIT(mod.wearer, TRAIT_HEAD_ATMOS_SEALED, REF(this));
    }

    // tg: code/modules/mod/modules/modules_general.dm:711
    public override void on_unequip()
    {
        REMOVE_TRAIT(mod.wearer, TRAIT_HEAD_ATMOS_SEALED, REF(this));
    }
}

///Hat Stabilizer - Allows displaying a hat over the MOD-helmet, à la plasmamen helmets.
// tg: code/modules/mod/modules/modules_general.dm:717
public partial class obj_item_mod_module_hat_stabilizer : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:732
    public override async DmTask on_part_activation()
    {
        obj_item_clothing helmet = mod.get_part_from_slot(ITEM_SLOT_HEAD) as obj_item_clothing;
        if (!istype(helmet, typeof(obj_item_clothing)))
            return;
        // Override pre-existing component
        await helmet.AddComponent(typeof(datum_component_hat_stabilizer), Dm.named("loose_hat", FALSE));
    }

    // tg: code/modules/mod/modules/modules_general.dm:739
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        deleting ??= FALSE;
        if (Dm.truthy(deleting))
            return;
        obj_item_clothing helmet = mod.get_part_from_slot(ITEM_SLOT_HEAD) as obj_item_clothing;
        if (!istype(helmet, typeof(obj_item_clothing)))
            return;
        // Override again!
        await helmet.AddComponent(typeof(datum_component_hat_stabilizer), Dm.named("loose_hat", TRUE));
    }
}

///Sign Language Translator - allows people to sign over comms using the modsuit's gloves.
// tg: code/modules/mod/modules/modules_general.dm:758
public partial class obj_item_mod_module_signlang_radio : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:770
    public override async DmTask on_part_activation()
    {
        ADD_TRAIT(mod.wearer, TRAIT_CAN_SIGN_ON_COMMS, REF(this));
    }

    // tg: code/modules/mod/modules/modules_general.dm:773
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        REMOVE_TRAIT(mod.wearer, TRAIT_CAN_SIGN_ON_COMMS, REF(this));
    }
}

///A module that recharges the suit by an itsy tiny bit whenever the user takes a step.
// tg: code/modules/mod/modules/modules_general.dm:777
public partial class obj_item_mod_module_joint_torsion : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:786
    public override async DmTask on_part_activation()
    {
        if (!Dm.truthy(Dm.bitand(mod.wearer.movement_type, Dm.bitor(FLOATING, FLYING))))
            RegisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        /// This way we don't even bother to call on_moved() while flying/floating
        RegisterSignal(mod.wearer, COMSIG_MOVETYPE_FLAG_ENABLED, nameof(on_movetype_flag_enabled));
        RegisterSignal(mod.wearer, COMSIG_MOVETYPE_FLAG_DISABLED, nameof(on_movetype_flag_disabled));
    }

    // tg: code/modules/mod/modules/modules_general.dm:793
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        UnregisterSignal(mod.wearer, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_MOVETYPE_FLAG_ENABLED, COMSIG_MOVETYPE_FLAG_DISABLED));
    }

    // tg: code/modules/mod/modules/modules_general.dm:796
    [SignalHandler]
    public virtual void on_movetype_flag_enabled(datum source = null, object flag = null, object old_state = null)
    {
        if (!Dm.truthy(Dm.bitand(old_state, Dm.bitor(FLOATING, FLYING))) && Dm.truthy(Dm.bitand(flag, Dm.bitor(FLOATING, FLYING))))
            UnregisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED);
    }

    // tg: code/modules/mod/modules/modules_general.dm:801
    [SignalHandler]
    public virtual void on_movetype_flag_disabled(datum source = null, object flag = null, object old_state = null)
    {
        if (Dm.truthy(Dm.bitand(old_state, Dm.bitor(FLOATING, FLYING))) && !Dm.truthy(Dm.bitand(mod.wearer.movement_type, Dm.bitor(FLOATING, FLYING))))
            RegisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED, nameof(on_moved));
    }

    // tg: code/modules/mod/modules/modules_general.dm:806
    [SignalHandler]
    public virtual void on_moved(mob_living_carbon_human wearer = null, atom old_loc = null, object movement_dir = null, object forced = null)
    {
        //Shouldn't work if the wearer isn't really walking/running around.
        if (Dm.truthy(forced) || Dm.truthy(wearer.throwing) || Dm.eq(wearer.body_position, LYING_DOWN) || Dm.truthy(wearer.buckled) || Dm.truthy(CHECK_MOVE_LOOP_FLAGS(wearer, MOVEMENT_LOOP_OUTSIDE_CONTROL)))
            return;
        Waiting.NoSleep(mod.core.add_charge(power_per_step));
    }
}

/// Module that shoves garbage inside its material container when the user crosses it, and eject the recycled material with MMB.
// tg: code/modules/mod/modules/modules_general.dm:814
public partial class obj_item_mod_module_recycler : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:843
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (!Dm.truthy(length(accepted_mats)))
            accepted_mats = SSmaterials.get_materials_by_flag(MATERIAL_SILO_STORED) as DmList;

        container = Dm.@new<datum_material_container>(
            this,
            accepted_mats,
            50 * SHEET_MATERIAL_AMOUNT,
            Dm.bitor(MATCONTAINER_EXAMINE, MATCONTAINER_NO_INSERT),
            Dm.named("container_signals", Dm.list(Dm.kv(COMSIG_MATCONTAINER_STACK_RETRIEVED, nameof(obj_item_mod_module_recycler.InsertSheets))))
        );
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:859
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref container);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/modules/modules_general.dm:863
    public override async DmTask on_activation(mob activator = null)
    {
        RegisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED, nameof(on_wearer_moved));
    }

    // tg: code/modules/mod/modules/modules_general.dm:866
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        UnregisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED);
        if (mod.wearer.loc != null)
            UnregisterSignal(mod.wearer.loc, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON));
    }

    // tg: code/modules/mod/modules/modules_general.dm:871
    [SignalHandler]
    public virtual void on_wearer_moved(datum source = null, atom old_loc = null, object dir = null, object forced = null)
    {
        if (old_loc != null)
            UnregisterSignal(mod.wearer.loc, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON));
        if (mod.wearer.loc != null)
        {
            RegisterSignal(mod.wearer.loc, COMSIG_ATOM_ENTERED, nameof(on_obj_entered));
            RegisterSignal(mod.wearer.loc, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(on_atom_initialized_on));
        }
        foreach (obj_item item in Dm.each<obj_item>(mod.wearer.loc))
        {
            if (!Dm.truthy(is_type_in_list(item, allowed_item_types)))
                return;
            Waiting.NoSleep(insert_trash(item));
        }
    }

    // tg: code/modules/mod/modules/modules_general.dm:883
    [SignalHandler]
    public virtual void on_obj_entered(atom new_loc = null, atom_movable arrived = null, atom old_loc = null)
    {
        if (Dm.truthy(is_type_in_list(arrived, allowed_item_types)))
            Waiting.NoSleep(insert_trash(arrived as obj_item));
    }

    // tg: code/modules/mod/modules/modules_general.dm:888
    [SignalHandler]
    public virtual void on_atom_initialized_on(atom loc = null, atom new_atom = null)
    {
        // Give the new atom the time to fully initialize and maybe live if the wearer moves away.
        if (Dm.truthy(is_type_in_list(new_atom, allowed_item_types)))
            _addtimer(Dm.@new<datum_callback>(this, nameof(insert_trash_if_nearby), new_atom), 0.5f * SECONDS, file: "code/modules/mod/modules/modules_general.dm", line: 892);
    }

    // tg: code/modules/mod/modules/modules_general.dm:894
    public virtual async DmTask insert_trash_if_nearby(atom new_atom = null)
    {
        if (new_atom != null && mod?.wearer != null && new_atom.loc == mod.wearer.loc)
            await insert_trash(new_atom as obj_item);
    }

    // tg: code/modules/mod/modules/modules_general.dm:898
    public virtual async DmTask insert_trash(obj_item item = null)
    {
        object retrieved = await container.insert_item(item, multiplier: efficiency);
        if (Dm.eq(retrieved, MATERIAL_INSERT_ITEM_NO_MATS)) //even if it doesn't have any material to give, trash is trash.
            qdel(item);
        playsound(this, SFX_RUSTLE, 50, TRUE, -5);
    }

    // tg: code/modules/mod/modules/modules_general.dm:904
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        if (target?.atom_storage == null)
        {
            target = get_turf(target);
            if (!isopenturf(target) || !Dm.truthy(await mod.wearer.Adjacent(target)))
                return FALSE;
        }
        await dispense(target);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:914
    public virtual async DmTask dispense(atom target = null)
    {
        if (Dm.truthy(await container.retrieve_all(target)))
        {
            balloon_alert(mod.wearer, I18n.T("material dispensed"));
            playsound(this, Dm.resource("sound/machines/microwave/microwave-end.ogg"), 50, TRUE);
            return;
        }
        balloon_alert(mod.wearer, I18n.T("not enough material"));
        playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, TRUE);
    }

    // tg: code/modules/mod/modules/modules_general.dm:922
    [SignalHandler]
    public virtual void InsertSheets(obj_item recycler = null, obj_item_stack sheets = null, atom context = null)
    {
        Waiting.NoSleep(attempt_insert_storage(sheets));
    }

    // tg: code/modules/mod/modules/modules_general.dm:927
    public virtual async DmTask attempt_insert_storage(obj_item to_drop = null)
    {
        if (!isturf(to_drop.loc) && !Dm.truthy(to_drop.loc.atom_storage?.attempt_insert(to_drop, mod.wearer, @override: TRUE)))
            await to_drop.forceMove(to_drop.loc.drop_location() as atom);
    }
}

///A black market variant of the above that dispenses riot foam dart boxes
// tg: code/modules/mod/modules/modules_general.dm:932
public partial class obj_item_mod_module_recycler_donk : obj_item_mod_module_recycler
{
    // tg: code/modules/mod/modules/modules_general.dm:946
    public override async DmTask dispense(atom target = null)
    {
        if (!Dm.truthy(await container.use_amount_mat(required_amount, typeof(datum_material_iron))))
        {
            balloon_alert(mod.wearer, I18n.T("not enough material"));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, TRUE);
            return;
        }
        obj_item_ammo_box product = Dm.@new(ammobox_type, target) as obj_item_ammo_box;
        await attempt_insert_storage(product);
        balloon_alert(mod.wearer, I18n.T("ammo box dispensed."));
        playsound(this, Dm.resource("sound/machines/microwave/microwave-end.ogg"), 50, TRUE);
    }
}

// tg: code/modules/mod/modules/modules_general.dm:956
public partial class obj_item_mod_module_fishing_glove : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:967
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:971
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null && equipped != null)
        {
            context[SCREENTIP_CONTEXT_RMB] = I18n.T("Remove rod");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (istype(held_item, typeof(obj_item_fishing_rod)))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Insert rod");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return null;
    }

    // tg: code/modules/mod/modules/modules_general.dm:979
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("You can [] the modsuit gloves to open the fishing rod interface once attached and activated.", EXAMINE_HINT(I18n.T("right-click")))));
        if (equipped != null)
            // [icon2html(equipped, user)]: the rod's picture in the chat line; the 3D client's chat is text (chat icons, M10+), so the rod's
            // name stands in for it
            Dm.append(ref dot, span_info(I18n.T("it has a [] installed. [] to remove it.", equipped, EXAMINE_HINT(I18n.T("Right-Click")))));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_general.dm:985
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_fishing_rod)))
            return await base.item_interaction(user, tool, modifiers, __extra);
        // tg's own: neither failure returns, so the rod is still taken in after the first and its alert after the second
        if (equipped != null)
            balloon_alert(user, I18n.T("already has rod!"));
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            user.balloon_alert(user, I18n.T("it's stuck!"));
        equipped = tool as obj_item_fishing_rod;
        balloon_alert(user, I18n.T("rod inserted"));
        playsound(this, Dm.resource("sound/items/click.ogg"), 50, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mod/modules/modules_general.dm:997
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (equipped == null)
            return dot;
        await user.put_in_hands(equipped);
        balloon_alert(user, I18n.T("rod removed"));
        playsound(this, Dm.resource("sound/items/click.ogg"), 50, TRUE);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/mod/modules/modules_general.dm:1008
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == equipped)
        {
            equipped = null;
            obj_item gloves = mod?.get_part_from_slot(ITEM_SLOT_GLOVES) as obj_item;
            if (gloves != null && !QDELETED(mod))
            {
                // qdel(gloves.GetComponent(/datum/component/profound_fisher)): the rod's fishing interface on the gloves (fishing is
                // not ported; the component is inert and never added)
                Dm.Deferred("code/modules/mod/modules/modules_general.dm:1013", "Exited: qdel(gloves.GetComponent(/datum/component/profound_fisher)), the gloves' fishing interface (fishing, M10+)");
            }
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/mod/modules/modules_general.dm:1016
    public override async DmTask on_part_activation()
    {
        obj_item gloves = mod.get_part_from_slot(ITEM_SLOT_GLOVES) as obj_item;
        if (gloves == null)
            return;
        // gloves.AddElement(/datum/element/adjust_fishing_difficulty, -5); if(equipped) gloves.AddComponent(/datum/component/profound_fisher,
        // equipped, delete_rod_when_deleted = FALSE): easier fishing and the rod's fishing interface on the gloves (fishing is not ported)
        Dm.Deferred("code/modules/mod/modules/modules_general.dm:1020", "on_part_activation: the adjust_fishing_difficulty element and the profound_fisher component on the gloves (fishing, M10+)");
    }

    // tg: code/modules/mod/modules/modules_general.dm:1024
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        deleting ??= FALSE;
        obj_item gloves = mod.get_part_from_slot(ITEM_SLOT_GLOVES) as obj_item;
        if (gloves != null && !Dm.truthy(deleting))
        {
            // gloves.RemoveElement(/datum/element/adjust_fishing_difficulty); qdel(gloves.GetComponent(/datum/component/profound_fisher))
            Dm.Deferred("code/modules/mod/modules/modules_general.dm:1027", "on_part_deactivation: the adjust_fishing_difficulty element and the profound_fisher component off the gloves (fishing, M10+)");
        }
    }
}

// tg: code/modules/mod/modules/modules_general.dm:1030
public partial class obj_item_mod_module_shock_absorber : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_general.dm:1039
    public override async DmTask on_part_activation()
    {
        await base.on_part_activation();
        ADD_TRAIT(mod.wearer, TRAIT_BATON_RESISTANCE, REF(this));
        RegisterSignal(mod.wearer, COMSIG_MOB_BATONED, nameof(mob_batoned));
    }

    // tg: code/modules/mod/modules/modules_general.dm:1044
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        await base.on_part_deactivation(deleting);
        REMOVE_TRAIT(mod.wearer, TRAIT_BATON_RESISTANCE, REF(this));
        UnregisterSignal(mod.wearer, COMSIG_MOB_BATONED);
    }

    // tg: code/modules/mod/modules/modules_general.dm:1049
    [SignalHandler]
    public virtual void mob_batoned(datum source = null)
    {
        Waiting.NoSleep(drain_power(use_energy_cost));
        do_sparks(5, TRUE, mod.wearer.loc);
    }
}
