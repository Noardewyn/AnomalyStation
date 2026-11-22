// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/modules_engineering.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole. The engineering MOD modules: welding protection (the helmet's flash protection while sealed) and
// the Syndicate suit's optical suite (its overlay follows combat mode), the t-ray scan, magnetic stability (the magboots: no slips,
// gravity, a slowdown; the advanced kind without it), the emergency tether (a grappling hook fired at a wall, a structure or an
// anchor it lays, which tethers the wearer: datums/components/tether.dm) and the tether anchor, radiation protection (the parts
// radiation-proof, a geiger counter, the irradiation readout), the constructor (quick building, an RCD scan), the head protector and
// the water and resin misters. The var blocks are generated (D111).

///Welding Protection - Makes the helmet protect from flashes and welding.
// tg: code/modules/mod/modules/modules_engineering.dm:4
public partial class obj_item_mod_module_welding : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_engineering.dm:16
    public override async DmTask on_part_activation()
    {
        object part__1 = mod.get_part_from_slot(ITEM_SLOT_HEAD);
        obj_item_clothing head_cover = (Dm.truthy(part__1) ? part__1 : mod.get_part_from_slot(ITEM_SLOT_MASK) is var or__1 && Dm.truthy(or__1) ? or__1 : mod.get_part_from_slot(ITEM_SLOT_EYES)) as obj_item_clothing;
        if (istype(head_cover, typeof(obj_item_clothing)))
        {
            //this is a screen that displays an image, so flash sensitives can use this to protect against flashes.
            head_cover.flash_protect = FLASH_PROTECTION_WELDER_HYPER_SENSITIVE;
        }
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:22
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        deleting ??= FALSE;
        if (Dm.truthy(deleting))
            return;
        object part__1 = mod.get_part_from_slot(ITEM_SLOT_HEAD);
        obj_item_clothing head_cover = (Dm.truthy(part__1) ? part__1 : mod.get_part_from_slot(ITEM_SLOT_MASK) is var or__1 && Dm.truthy(or__1) ? or__1 : mod.get_part_from_slot(ITEM_SLOT_EYES)) as obj_item_clothing;
        if (istype(head_cover, typeof(obj_item_clothing)))
            head_cover.flash_protect = Dm.initial(head_cover, (obj_item_clothing p) => p.flash_protect);
    }
}

// tg: code/modules/mod/modules/modules_engineering.dm:29
public partial class obj_item_mod_module_welding_syndicate : obj_item_mod_module_welding
{
    // tg: code/modules/mod/modules/modules_engineering.dm:38
    public override object generate_worn_overlay(obj_item source = null, mutable_appearance standing = null)
    {
        if (mod.wearer == null || !Dm.truthy(mod.wearer.combat_mode))
            overlay_state_inactive = Dm.text("[]-[]", Dm.initial(this, (obj_item_mod_module_welding_syndicate p) => p.overlay_state_inactive), mod.skin);
        else
            overlay_state_inactive = Dm.text("module_armorbooster_on-[]", mod.skin);
        return base.generate_worn_overlay(source, standing);
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:46
    public override async DmTask on_part_activation()
    {
        await base.on_part_activation();
        RegisterSignal(mod.wearer, COMSIG_COMBAT_MODE_TOGGLED, nameof(on_combat_mode_toggle));
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:50
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_part_deactivation(deleting);
        UnregisterSignal(mod.wearer, COMSIG_COMBAT_MODE_TOGGLED);
    }

    /// Changes which overlay state we're using depending on combat mode status.
    // tg: code/modules/mod/modules/modules_engineering.dm:55
    [SignalHandler]
    public virtual void on_combat_mode_toggle(mob_living_carbon_human toggler = null)
    {
        playsound(this, Dm.resource("sound/vehicles/mecha/mechmove03.ogg"), 25, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        Waiting.NoSleep(update_clothing_slots());
    }
}

///T-Ray Scan - Scans the terrain for undertile objects.
// tg: code/modules/mod/modules/modules_engineering.dm:61
public partial class obj_item_mod_module_t_ray : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_engineering.dm:76
    public override async DmTask on_active_process(object seconds_per_tick = null)
    {
        t_ray_scan(mod.wearer, 0.8f * SECONDS, range);
    }
}

///Magnetic Stability - Gives the user a slowdown but makes them negate gravity and be immune to slips.
// tg: code/modules/mod/modules/modules_engineering.dm:80
public partial class obj_item_mod_module_magboot : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_engineering.dm:98
    public override async DmTask on_install()
    {
        await base.on_install();
        RegisterSignal(mod, COMSIG_MOD_UPDATE_SPEED, nameof(on_update_speed));
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:102
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        UnregisterSignal(mod, COMSIG_MOD_UPDATE_SPEED);
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:106
    public override async DmTask on_activation(mob activator = null)
    {
        mod.wearer.add_traits(active_traits, REF(this));
        await mod.update_speed();
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:110
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        mod.wearer.remove_traits(active_traits, REF(this));
        await mod.update_speed();
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:114
    [SignalHandler]
    public virtual void on_update_speed(datum source = null, DmList module_slowdowns = null, object prevent_slowdown = null)
    {
        if (!Dm.truthy(prevent_slowdown) && Dm.truthy(active))
            Dm.append(ref module_slowdowns, slowdown_active);
    }
}

///Emergency Tether - Shoots a grappling hook projectile in 0g that throws the user towards it.
// tg: code/modules/mod/modules/modules_engineering.dm:126
public partial class obj_item_mod_module_tether : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_engineering.dm:140
    public override async DmTask<object> used(mob activator = null)
    {
        if (HAS_TRAIT_FROM(mod.wearer, TRAIT_TETHER_ATTACHED, REF(this)))
        {
            balloon_alert(mod.wearer, I18n.T("already tethered!"));
            playsound(this, Dm.resource("sound/items/weapons/gun/general/dry_fire.ogg"), 25, TRUE);
            return FALSE;
        }
        return await base.used(activator);
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:147
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        obj_projectile tether = Dm.@new<obj_projectile_tether>(mod.wearer.loc, this);
        await tether.aim_projectile(target, mod.wearer);
        tether.firer = mod.wearer;
        playsound(this, Dm.resource("sound/items/weapons/batonextend.ogg"), 25, TRUE);
        Waiting.Detach(tether.fire()); // INVOKE_ASYNC(tether, TYPE_PROC_REF(/obj/projectile, fire))
        await drain_power(use_energy_cost);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:158
    public override object get_configuration(mob user = null)
    {
        DmList dot = base.get_configuration(user) as DmList;
        dot["cut_tethers"] = add_ui_configuration(I18n.T("Cut Tethers"), "button", "scissors");
        return dot;
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:162
    public override async DmTask configure_edit(object key = null, object value = null)
    {
        if (Dm.eq(key, "cut_tethers"))
            SEND_SIGNAL(COMSIG_MOD_TETHER_SNAP, Dm.list(this));
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:166
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        SEND_SIGNAL(COMSIG_MOD_TETHER_SNAP, Dm.list(this));
    }
}

// tg: code/modules/mod/modules/modules_engineering.dm:169
public partial class obj_projectile_tether : obj_projectile
{
    // tg: code/modules/mod/modules/modules_engineering.dm:188
    [DmParams("mapload", "module")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object module = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_PROJECTILE_ON_EMBEDDED, nameof(on_embedded));
        if (module != null)
            parent_module = module as obj_item_mod_module_tether;
        return dot;
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:194
    [SignalHandler]
    public virtual void on_embedded(datum source = null, obj_item payload = null, atom hit = null)
    {
        if (HAS_TRAIT_FROM(hit, TRAIT_TETHER_ATTACHED, REF(parent_module)))
            return;

        Waiting.NoSleep(firer.AddComponent(typeof(datum_component_tether), hit, 7, "MODtether", payload, Dm.named("parent_module", parent_module), Dm.named("tether_trait_source", REF(parent_module))));
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:202
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (isopenturf(loc))
            last_turf = loc as turf_open;
        return dot;
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:207
    public override async DmTask fire(object fire_angle = null, atom direct_target = null)
    {
        if (firer != null)
            line = firer.Beam(this, "line", Dm.resource("icons/obj/clothing/modsuit/mod_modules.dmi"), emissive: NONE);
        await base.fire(fire_angle, direct_target);
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:212
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (firer == null)
            return dot;

        // Funni is handled separately
        if (ismob(target))
            return dot;

        if (istype(target, typeof(obj_item_tether_anchor)) || isstructure(target) || ismachinery(target))
        {
            if (HAS_TRAIT_FROM(target, TRAIT_TETHER_ATTACHED, REF(parent_module)))
                return dot;
            float avoid_target_trait = FALSE;
            if (istype(target, typeof(obj_item_tether_anchor)))
                avoid_target_trait = TRUE;
            await firer.AddComponent(typeof(datum_component_tether), target, 7, "MODtether", Dm.named("parent_module", parent_module), Dm.named("tether_trait_source", REF(parent_module)), Dm.named("no_target_trait", avoid_target_trait));
            return dot;
        }

        float hitx = Dm.num(impact_x);
        float hity = Dm.num(impact_y);

        if (last_turf != null && last_turf != target && last_turf != target.loc)
        {
            float turf_dir = get_dir(last_turf, get_turf(target));
            if (Dm.truthy(Dm.bitand(turf_dir, NORTH)))
                hity += 32;
            if (Dm.truthy(Dm.bitand(turf_dir, SOUTH)))
                hity -= 32;
            if (Dm.truthy(Dm.bitand(turf_dir, EAST)))
                hitx += 32;
            if (Dm.truthy(Dm.bitand(turf_dir, WEST)))
                hitx -= 32;
        }

        obj_item_tether_anchor anchor = Dm.@new<obj_item_tether_anchor>(last_turf is var or__1 && Dm.truthy(or__1) ? or__1 : get_turf(target));
        anchor.pixel_x = hitx;
        anchor.pixel_y = hity;
        anchor.anchored = TRUE;
        anchor.reset_pixel_pos = TRUE;
        anchor.parent_module = parent_module;
        await firer.AddComponent(typeof(datum_component_tether), anchor, 7, "MODtether", Dm.named("parent_module", parent_module), Dm.named("tether_trait_source", REF(parent_module)));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:252
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref line);
        return base.Destroy(force);
    }
}

// tg: code/modules/mod/modules/modules_engineering.dm:256
public partial class obj_item_tether_anchor : obj_item
{
    // tg: code/modules/mod/modules/modules_engineering.dm:269
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ATOM_TETHER_SNAPPED, nameof(tether_snapped));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:273
    public override object Destroy(float? force = null)
    {
        // We don't need to worry about hanging refs in case our parent gets destroyed because then it snaps all tethers, which in turn destroys us
        parent_module = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:278
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("It can be secured by using a wrench on it. Use right-click to tether yourself to [].", this)));
        Dm.append(ref dot, span_info(I18n.T("LMB shortens the tether while RMB lengthens it. Ctrl-click to cut the tether.")));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:283
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        if (Dm.truthy(await default_unfasten_wrench(user, tool)))
        {
            pixel_x = 0;
            pixel_y = 0;
            reset_pixel_pos = FALSE;
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:292
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(await can_interact(user)) || !Dm.truthy(await IsReachableBy(user)) || !isturf(loc))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        if (HAS_TRAIT_FROM(user, TRAIT_TETHER_ATTACHED, REF(this)))
        {
            balloon_alert(user, I18n.T("already tethered!"));
            return null;
        }

        if (parent_module != null && HAS_TRAIT_FROM(user, TRAIT_TETHER_ATTACHED, REF(parent_module)))
        {
            balloon_alert(user, I18n.T("already tethered!"));
            return null;
        }

        balloon_alert(user, I18n.T("attached tether"));
        await user.AddComponent(typeof(datum_component_tether), this, 7, "tether", Dm.named("tether_trait_source", REF(this)));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:308
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (Dm.truthy(reset_pixel_pos))
        {
            pixel_x = 0;
            pixel_y = 0;
            reset_pixel_pos = FALSE;
        }
        return dot;
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:315
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        atom target = dropped;
        if (!Dm.truthy(await can_interact(user)) || !Dm.truthy(await IsReachableBy(user)) || !isturf(loc))
            return null;

        if (!isliving(target) || !Dm.truthy(await IsReachableBy(target as atom_movable)))
            return null;

        if (HAS_TRAIT_FROM(target, TRAIT_TETHER_ATTACHED, REF(this)))
        {
            balloon_alert(user, I18n.T("already tethered!"));
            return null;
        }

        if (parent_module != null && HAS_TRAIT_FROM(user, TRAIT_TETHER_ATTACHED, REF(parent_module)))
        {
            balloon_alert(user, I18n.T("already tethered!"));
            return null;
        }

        if (target == user)
        {
            balloon_alert(user, I18n.T("attached tether"));
            await user.AddComponent(typeof(datum_component_tether), this, 7, "tether", Dm.named("tether_trait_source", REF(this)), Dm.named("no_target_trait", TRUE));
            return null;
        }

        balloon_alert(user, I18n.T("attaching tether..."));
        to_chat(target, span_userdanger(I18n.T("[] is trying to attach a tether to you!", user)));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target)))
            return null;

        if (HAS_TRAIT_FROM(target, TRAIT_TETHER_ATTACHED, REF(this)))
        {
            balloon_alert(user, I18n.T("already tethered!"));
            return null;
        }

        if (parent_module != null && HAS_TRAIT_FROM(user, TRAIT_TETHER_ATTACHED, REF(parent_module)))
        {
            balloon_alert(user, I18n.T("already tethered!"));
            return null;
        }

        balloon_alert(user, I18n.T("attached tether"));
        to_chat(target, span_userdanger(I18n.T("[] attaches a tether to you!", user)));
        await target.AddComponent(typeof(datum_component_tether), this, 7, "tether", Dm.named("tether_trait_source", REF(this)), Dm.named("no_target_trait", TRUE));
        return null;
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:352
    [SignalHandler]
    public virtual void tether_snapped(datum_component_tether tether = null, object tether_source = null)
    {
        if (parent_module == null || !Dm.eq(tether_source, REF(parent_module)))
            return;

        // Destroy self if we've been created by a tether module
        do_sparks(3, TRUE, this);
        qdel(this);
    }
}

///Radiation Protection - Protects the user from radiation, gives them a geiger counter and rad info in the panel.
// tg: code/modules/mod/modules/modules_engineering.dm:372
public partial class obj_item_mod_module_rad_protection : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_engineering.dm:387
    public override async DmTask on_part_activation()
    {
        await AddComponent(typeof(datum_component_geiger_sound));
        ADD_TRAIT(mod.wearer, TRAIT_BYPASS_EARLY_IRRADIATED_CHECK, REF(this));
        RegisterSignal(mod.wearer, COMSIG_IN_RANGE_OF_IRRADIATION, nameof(on_pre_potential_irradiation));
        foreach (obj_item part in Dm.each<obj_item>(mod.get_parts(all: TRUE)))
            ADD_TRAIT(part, TRAIT_RADIATION_PROTECTED_CLOTHING, MOD_TRAIT);
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:394
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        qdel(GetComponent(typeof(datum_component_geiger_sound)));
        REMOVE_TRAIT(mod.wearer, TRAIT_BYPASS_EARLY_IRRADIATED_CHECK, REF(this));
        UnregisterSignal(mod.wearer, COMSIG_IN_RANGE_OF_IRRADIATION);
        foreach (obj_item part in Dm.each<obj_item>(mod.get_parts(all: TRUE)))
            REMOVE_TRAIT(part, TRAIT_RADIATION_PROTECTED_CLOTHING, MOD_TRAIT);
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:401
    public override object add_ui_data()
    {
        DmList dot = base.add_ui_data() as DmList;
        dot["is_user_irradiated"] = mod.wearer != null ? Dm.num(HAS_TRAIT(mod.wearer, TRAIT_IRRADIATED)) : FALSE;
        dot["background_radiation_level"] = perceived_threat_level;
        dot["health_max"] = mod.wearer?.getMaxHealth() is var or__1 && Dm.truthy(or__1) ? or__1 : 0;
        dot["loss_tox"] = mod.wearer?.get_tox_loss() is var or__2 && Dm.truthy(or__2) ? or__2 : 0;
        return dot;
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:408
    [SignalHandler]
    public virtual void on_pre_potential_irradiation(datum source = null, datum_radiation_pulse_information pulse_information = null, object insulation_to_target = null)
    {
        perceived_threat_level = get_perceived_radiation_danger(pulse_information, insulation_to_target);
        // VARSET_CALLBACK(src, perceived_threat_level, null): ___callbackvarset is not declared (vars is not translated, rules 10.7),
        // so the timer calls perceived_threat_level__varset (as toys.cs)
        _addtimer(Dm.@new<datum_callback>(this, nameof(perceived_threat_level__varset), null), TIME_WITHOUT_RADIATION_BEFORE_RESET, Dm.num(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE)), file: "code/modules/mod/modules/modules_engineering.dm", line: 412);
    }

    // tg: code/__HELPERS/varset_callback.dm:3  VARSET_CALLBACK(src, perceived_threat_level, value)
    public virtual void perceived_threat_level__varset(object var_value = null) => perceived_threat_level = var_value;
}

///Constructor - Lets you build quicker and create RCD holograms.
// tg: code/modules/mod/modules/modules_engineering.dm:415
public partial class obj_item_mod_module_constructor : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_engineering.dm:431
    public override async DmTask on_part_activation()
    {
        ADD_TRAIT(mod.wearer, TRAIT_QUICK_BUILD, REF(this));
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:434
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        REMOVE_TRAIT(mod.wearer, TRAIT_QUICK_BUILD, REF(this));
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:437
    public override async DmTask<object> on_use(mob activator = null)
    {
        rcd_scan(this, fade_time: 10 * SECONDS);
        await drain_power(use_energy_cost);
        return null;
    }
}

///Safety-First Head Protection - Protects your brain matter from sudden impacts.
// tg: code/modules/mod/modules/modules_engineering.dm:442
public partial class obj_item_mod_module_headprotector : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_engineering.dm:455
    public override async DmTask on_part_activation()
    {
        ADD_TRAIT(mod.wearer, TRAIT_HEAD_INJURY_BLOCKED, REF(this));
    }

    // tg: code/modules/mod/modules/modules_engineering.dm:458
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        REMOVE_TRAIT(mod.wearer, TRAIT_HEAD_INJURY_BLOCKED, REF(this));
    }
}

///Mister - Sprays water over an area.
// tg: code/modules/mod/modules/modules_engineering.dm:462
public partial class obj_item_mod_module_mister : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_engineering.dm:476
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        create_reagents(volume, OPENCONTAINER);
        return base.Initialize(mapload, __extra);
    }
}

///Resin Mister - Sprays resin over an area.
// tg: code/modules/mod/modules/modules_engineering.dm:481
public partial class obj_item_mod_module_mister_atmos : obj_item_mod_module_mister
{
    // tg: code/modules/mod/modules/modules_engineering.dm:488
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep((reagents as datum_reagents).add_reagent(typeof(datum_reagent_water), volume));
        return dot;
    }
}
