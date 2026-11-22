// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/modules_antag.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole. The antagonists' MOD modules: the energy shield (the shielded component's recharging charges, the
// wizard's battlemage kind), the magic nullifier and neutralizer, the insignia stripe in the suit's skin colour, the anti slip, the
// "bite of 87" springlock disguised as a DNA lock, the flamethrower, the power kick, the chameleon (the suit disguised as any item of
// its slot), the plate compression, the psi-echo demoralizer, the infiltrator's core programs (unknown face and voice, silent steps,
// a hardened visor), the medical beamgun and the Wraith cloak (the stealth module's syndicate kind that saboteurs electronics,
// modules_ninja.dm). The syndicate's suits carry them (D316); the wizard's, the ninja's and the other antagonists' suits are D333's
// paused antagonists. The var blocks are generated (D111) but the chameleon module's (its disguise is a typepath).

///Energy Shield - Gives you a rechargeable energy shield that nullifies attacks.
// tg: code/modules/mod/modules/modules_antag.dm:4
public partial class obj_item_mod_module_energy_shield : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_antag.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        charges = max_charges;
        return dot;
    }

    // tg: code/modules/mod/modules/modules_antag.dm:39
    public override async DmTask on_part_activation()
    {
        await mod.AddComponent(
            typeof(datum_component_shielded),
            Dm.named("max_charges", max_charges),
            Dm.named("recharge_start_delay", recharge_start_delay),
            Dm.named("charge_increment_delay", charge_increment_delay),
            Dm.named("charge_recovery", charge_recovery),
            Dm.named("lose_multiple_charges", lose_multiple_charges),
            Dm.named("starting_charges", charges),
            Dm.named("can_block_overwhelming", block_overwhelming_attacks),
            Dm.named("shield_icon_file", shield_icon_file),
            Dm.named("shield_icon", shield_icon));
        RegisterSignal(mod.wearer, COMSIG_LIVING_CHECK_BLOCK, nameof(shield_reaction));
    }

    // tg: code/modules/mod/modules/modules_antag.dm:53
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        deleting ??= FALSE;
        datum_component_shielded shield = mod.GetComponent(typeof(datum_component_shielded)) as datum_component_shielded;
        charges = shield.current_charges;
        qdel(shield);
        UnregisterSignal(mod.wearer, COMSIG_LIVING_CHECK_BLOCK);
    }

    // tg: code/modules/mod/modules/modules_antag.dm:59
    [SignalHandler]
    public virtual object shield_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, float? damage = null, string attack_text = null, float? attack_type = null, float? armour_penetration = null, string damage_type = null)
    {
        damage ??= 0;
        attack_text ??= "the attack";
        attack_type ??= MELEE_ATTACK;
        armour_penetration ??= 0;
        damage_type ??= BRUTE;
        if (Dm.truthy(Waiting.NoSleep(mod.hit_reaction(owner, hitby, attack_text, 0, damage, attack_type))))
        {
            Waiting.NoSleep(drain_power(Dm.num(use_energy_cost) + Dm.num(use_energy_cost) * (Dm.eq(attack_type, OVERWHELMING_ATTACK) ? (damage.Value / 100) : 0)));
            return SUCCESSFUL_BLOCK;
        }
        return NONE;
    }
}

///Magic Nullifier - Protects you from magic.
// tg: code/modules/mod/modules/modules_antag.dm:92
public partial class obj_item_mod_module_anti_magic : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_antag.dm:104
    public override async DmTask on_part_activation()
    {
        mod.wearer.add_traits(Dm.list(TRAIT_ANTIMAGIC, TRAIT_HOLY), REF(this));
    }

    // tg: code/modules/mod/modules/modules_antag.dm:107
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        mod.wearer.remove_traits(Dm.list(TRAIT_ANTIMAGIC, TRAIT_HOLY), REF(this));
    }
}

// tg: code/modules/mod/modules/modules_antag.dm:110
public partial class obj_item_mod_module_anti_magic_wizard : obj_item_mod_module_anti_magic
{
    // tg: code/modules/mod/modules/modules_antag.dm:119
    public override async DmTask on_part_activation()
    {
        mod.wearer.add_traits(Dm.list(TRAIT_ANTIMAGIC, TRAIT_ANTIMAGIC_NO_SELFBLOCK), REF(this));
    }

    // tg: code/modules/mod/modules/modules_antag.dm:122
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        mod.wearer.remove_traits(Dm.list(TRAIT_ANTIMAGIC, TRAIT_ANTIMAGIC_NO_SELFBLOCK), REF(this));
    }
}

///Insignia - Gives you a skin specific stripe.
// tg: code/modules/mod/modules/modules_antag.dm:126
public partial class obj_item_mod_module_insignia : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_antag.dm:138
    public override object generate_worn_overlay(obj_item source = null, mutable_appearance standing = null)
    {
        overlay_state_inactive = Dm.text("[]-[]", Dm.initial(this, (obj_item_mod_module_insignia p) => p.overlay_state_inactive), mod.skin);
        object dot = base.generate_worn_overlay(source, standing);
        foreach (mutable_appearance appearance in Dm.each<mutable_appearance>(dot))
            appearance.color = color;
        return dot;
    }
}

///Anti Slip - Prevents you from slipping on water.
// tg: code/modules/mod/modules/modules_antag.dm:169
public partial class obj_item_mod_module_noslip : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_antag.dm:182
    public override async DmTask on_part_activation()
    {
        ADD_TRAIT(mod.wearer, TRAIT_NO_SLIP_WATER, REF(this));
    }

    // tg: code/modules/mod/modules/modules_antag.dm:185
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        REMOVE_TRAIT(mod.wearer, TRAIT_NO_SLIP_WATER, REF(this));
    }
}

//Bite of 87 Springlock - Equips faster, disguised as DNA lock.
// tg: code/modules/mod/modules/modules_antag.dm:189
public partial class obj_item_mod_module_springlock_bite_of_87 : obj_item_mod_module_springlock
{
    // tg: code/modules/mod/modules/modules_antag.dm:197
    public override async DmTask on_part_activation()
    {
        await base.on_part_activation();
        if (Dm.truthy(check_holidays(APRIL_FOOLS)) || prob(1))
        {
            await mod.set_mod_color("#b17f00");
            await mod.wearer.remove_atom_colour(WASHABLE_COLOUR_PRIORITY); // turns purple guy purple
            await mod.wearer.add_atom_colour("#704b96", FIXED_COLOUR_PRIORITY);
        }
    }
}

///Flamethrower - Launches fire across the area.
// tg: code/modules/mod/modules/modules_antag.dm:205
public partial class obj_item_mod_module_flamethrower : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_antag.dm:218
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        obj_projectile flame = Dm.@new<obj_projectile_bullet_incendiary_fire>(mod.wearer.loc);
        await flame.aim_projectile(target, mod.wearer);
        flame.firer = mod.wearer;
        playsound(this, Dm.resource("sound/items/modsuit/flamethrower.ogg"), 75, TRUE);
        Waiting.Detach(flame.fire()); // INVOKE_ASYNC(flame, TYPE_PROC_REF(/obj/projectile, fire))
        await drain_power(use_energy_cost);
        return dot;
    }
}

///Power kick - Lets the user launch themselves at someone to kick them.
// tg: code/modules/mod/modules/modules_antag.dm:230
public partial class obj_item_mod_module_power_kick : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_antag.dm:247
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        mod.wearer.visible_message(span_warning(I18n.T("[] starts charging a kick!", mod.wearer)),
            blind_message: span_hear(I18n.T("You hear a charging sound.")));
        playsound(this, Dm.resource("sound/items/modsuit/loader_charge.ogg"), 75, TRUE);
        balloon_alert(mod.wearer, I18n.T("you start charging..."));
        animate(mod.wearer, 0.3f * SECONDS, Dm.named("pixel_z", 16), Dm.named("flags", ANIMATION_RELATIVE), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
        _addtimer(Dm.@new<datum_callback>(mod.wearer, nameof(atom.SpinAnimation), 3, 2), 0.3f * SECONDS, file: "code/modules/mod/modules/modules_antag.dm", line: 255);
        if (!Dm.truthy(await do_after(mod.wearer, 1 * SECONDS, target: mod)))
        {
            animate(mod.wearer, 0.2f * SECONDS, Dm.named("pixel_z", -16), Dm.named("flags", ANIMATION_RELATIVE), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
            return dot;
        }
        animate(mod.wearer);
        await drain_power(use_energy_cost);
        playsound(this, Dm.resource("sound/items/modsuit/loader_launch.ogg"), 75, TRUE);
        float angle = Dm.num(get_angle(mod.wearer, target)) + 180;
        mod.wearer.transform = (mod.wearer.transform as matrix).Turn(angle);
        RegisterSignal(mod.wearer, COMSIG_MOVABLE_IMPACT, nameof(on_throw_impact));
        await mod.wearer.throw_at(target, range: 7, speed: 2, thrower: mod.wearer, spin: FALSE, gentle: TRUE, callback: Dm.@new<datum_callback>(this, nameof(on_throw_end), mod.wearer, -angle));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_antag.dm:268
    public virtual void on_throw_end(mob user = null, object angle = null)
    {
        if (user == null)
            return;
        user.transform = (user.transform as matrix).Turn(Dm.num(angle));
        animate(user, 0.2f * SECONDS, Dm.named("pixel_z", -16), Dm.named("flags", ANIMATION_RELATIVE), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
    }

    // tg: code/modules/mod/modules/modules_antag.dm:274
    [SignalHandler]
    public virtual void on_throw_impact(mob_living source = null, atom target = null, datum_thrownthing thrownthing = null)
    {
        UnregisterSignal(source, COMSIG_MOVABLE_IMPACT);
        if (mod?.wearer == null)
            return;
        if (isliving(target))
        {
            mob_living living_target = target as mob_living;
            Waiting.NoSleep(living_target.apply_damage(damage, BRUTE, mod.wearer.zone_selected, wound_bonus: wounding_power));
            Waiting.NoSleep(living_target.Knockdown(knockdown_time));
        }
        else if (Dm.truthy(target.uses_integrity))
            Waiting.NoSleep(target.take_damage(damage, BRUTE, MELEE));
        else
            return;
        Waiting.NoSleep(mod.wearer.do_attack_animation(target, ATTACK_EFFECT_SMASH));
    }
}

///Chameleon - lets the suit disguise as any item that would fit on that slot.
// tg: code/modules/mod/modules/modules_antag.dm:291
public partial class obj_item_mod_module_chameleon : obj_item_mod_module
{
    /// The path of the item we're disguised as.
    // tg: code/modules/mod/modules/modules_antag.dm:303: var/obj/item/current_disguise holds the typepath picked out of
    // possible_disguises, so it is Type (rules 3.2: typepaths read from a list); the hand-written field puts the class in Hand mode,
    // so its constructor and list initializers are written here (types-converter.md 3.2)
    public Type current_disguise;

    // tg: code/modules/mod/modules/modules_antag.dm:291
    public obj_item_mod_module_chameleon()
    {
        name = Dm.text("\\improper MOD chameleon module");
        desc = "A module using chameleon technology to disguise the suit as another object.";
        icon_state = "chameleon";
        module_type = 1;
        complexity = 2;
        cooldown_time = 5;
    }

    // tg: code/modules/mod/modules/modules_antag.dm:297
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        incompatible_modules = Dm.list(typeof(obj_item_mod_module_chameleon)); // tg: code/modules/mod/modules/modules_antag.dm:297
        allow_flags = Dm.list(12); // tg: code/modules/mod/modules/modules_antag.dm:299 (a list: tg's own, kept)
        possible_disguises = Dm.list(); // tg: code/modules/mod/modules/modules_antag.dm:301
    }

    // tg: code/modules/mod/modules/modules_antag.dm:305
    public override async DmTask on_install()
    {
        await base.on_install();
        DmList all_disguises = sort_list(subtypesof(get_path_by_slot(mod.slot_flags)), "/proc/" + nameof(cmp_typepaths_asc)) as DmList;
        foreach (Type clothing_path in Dm.each_cast<Type>(all_disguises))
        {
            Type clothing = clothing_path;
            if (!Dm.truthy(Dm.initial(clothing, (obj_item p) => p.icon_state)))
                continue;
            string chameleon_item_name = Dm.text("[] ([])", Dm.initial(clothing, (obj_item p) => p.name), Dm.initial(clothing, (obj_item p) => p.icon_state));
            possible_disguises[chameleon_item_name] = clothing_path;
        }
    }

    // tg: code/modules/mod/modules/modules_antag.dm:315
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        if (current_disguise != null)
            await return_look();
        possible_disguises = null;
    }

    // tg: code/modules/mod/modules/modules_antag.dm:321
    public override async DmTask<object> used(mob activator = null)
    {
        if (Dm.truthy(mod.active) || Dm.truthy(mod.activating))
        {
            balloon_alert(activator, I18n.T("unit active!"));
            return FALSE;
        }
        return await base.used(activator);
    }

    // tg: code/modules/mod/modules/modules_antag.dm:327
    public override async DmTask<object> on_use(mob activator = null)
    {
        if (current_disguise != null)
        {
            await return_look();
            return null;
        }
        object picked_name = await tgui_input_list(activator, I18n.T("Select look to change into"), I18n.T("Chameleon Settings"), possible_disguises);
        if (!Dm.truthy(possible_disguises[picked_name]) || Dm.truthy(mod.active) || Dm.truthy(mod.activating))
            return null;
        current_disguise = possible_disguises[picked_name] as Type;
        await update_look();
        return null;
    }

    // tg: code/modules/mod/modules/modules_antag.dm:337
    public virtual async DmTask update_look()
    {
        mod.name = Dm.initial(current_disguise, (obj_item p) => p.name);
        mod.desc = Dm.initial(current_disguise, (obj_item p) => p.desc);
        mod.icon_state = Dm.initial(current_disguise, (obj_item p) => p.icon_state);
        mod.icon = Dm.initial(current_disguise, (obj_item p) => p.icon);
        mod.worn_icon = Dm.initial(current_disguise, (obj_item p) => p.worn_icon);
        mod.alternate_worn_layer = Dm.initial(current_disguise, (obj_item p) => p.alternate_worn_layer);
        mod.lefthand_file = Dm.initial(current_disguise, (obj_item p) => p.lefthand_file);
        mod.righthand_file = Dm.initial(current_disguise, (obj_item p) => p.righthand_file);
        mod.worn_icon_state = Dm.initial(current_disguise, (obj_item p) => p.worn_icon_state);
        mod.inhand_icon_state = Dm.initial(current_disguise, (obj_item p) => p.inhand_icon_state);
        await update_clothing_slots();
        RegisterSignal(mod, COMSIG_MOD_ACTIVATE, nameof(return_look));
    }

    // tg: code/modules/mod/modules/modules_antag.dm:351
    public virtual async DmTask return_look()
    {
        datum_mod_theme theme = mod.theme as datum_mod_theme;
        mod.name = Dm.text("[] []", theme.name, Dm.initial(mod, (obj_item_mod_control p) => p.name));
        mod.desc = Dm.text("[] []", Dm.initial(mod, (obj_item_mod_control p) => p.desc), theme.desc);
        mod.icon_state = Dm.text("[]-[]", mod.skin, Dm.initial(mod, (obj_item_mod_control p) => p.icon_state));
        DmList mod_skin = theme.variants[mod.skin] as DmList;
        mod.icon = mod_skin[MOD_ICON_OVERRIDE] is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.resource("icons/obj/clothing/modsuit/mod_clothing.dmi");
        mod.worn_icon = mod_skin[MOD_WORN_ICON_OVERRIDE] is var or__2 && Dm.truthy(or__2) ? or__2 : Dm.resource("icons/mob/clothing/modsuit/mod_clothing.dmi");
        mod.lefthand_file = Dm.initial(mod, (obj_item_mod_control p) => p.lefthand_file);
        mod.righthand_file = Dm.initial(mod, (obj_item_mod_control p) => p.righthand_file);
        mod.worn_icon_state = null;
        mod.inhand_icon_state = null;
        await update_clothing_slots();
        current_disguise = null;
        UnregisterSignal(mod, COMSIG_MOD_ACTIVATE);
    }
}

///Plate Compression - Compresses the suit to normal size
// tg: code/modules/mod/modules/modules_antag.dm:367
public partial class obj_item_mod_module_plate_compression : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_antag.dm:379
    public override async DmTask on_install()
    {
        await base.on_install();
        old_size = mod.w_class;
        mod.update_weight_class(new_size);
    }

    // tg: code/modules/mod/modules/modules_antag.dm:384
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        mod.update_weight_class(old_size);
        old_size = null;
        if (mod.loc == null)
            return;
        datum_storage holding_storage = mod.loc.atom_storage;
        if (holding_storage == null || Dm.num(holding_storage.max_specific_storage) >= Dm.num(mod.w_class))
            return;
        await mod.forceMove(mod.drop_location() as atom);
    }
}

// tg: code/modules/mod/modules/modules_antag.dm:395
public partial class obj_item_mod_module_demoralizer : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_antag.dm:407
    public override async DmTask on_part_activation()
    {
        datum_demoralise_moods_module mood_category = Dm.@new<datum_demoralise_moods_module>();
        demoralizer = await Dm.new_async<datum_proximity_monitor_advanced_demoraliser>(mod.wearer, 7, TRUE, mood_category);
    }

    // tg: code/modules/mod/modules/modules_antag.dm:411
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        QDEL_NULL(ref demoralizer);
    }
}

// tg: code/modules/mod/modules/modules_antag.dm:414
public partial class obj_item_mod_module_infiltrator : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_antag.dm:428
    public override async DmTask on_install()
    {
        await base.on_install();
        ADD_TRAIT(mod, TRAIT_EXAMINE_SKIP, REF(this));
    }

    // tg: code/modules/mod/modules/modules_antag.dm:432
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        REMOVE_TRAIT(mod, TRAIT_EXAMINE_SKIP, REF(this));
    }

    // tg: code/modules/mod/modules/modules_antag.dm:436
    public override async DmTask on_part_activation()
    {
        mod.wearer.add_traits(traits_to_add, REF(this));
        RegisterSignal(mod.wearer, COMSIG_TRY_MODIFY_SPEECH, nameof(on_speech_modification));
        obj_item_organ_tongue user_tongue = mod.wearer.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        user_tongue.temp_say_mod = "states";
        obj_item_clothing head_cover = mod.get_part_from_slot(ITEM_SLOT_HEAD) as obj_item_clothing;
        if (istype(head_cover, typeof(obj_item_clothing)))
            head_cover.flash_protect = FLASH_PROTECTION_WELDER_HYPER_SENSITIVE;
    }

    // tg: code/modules/mod/modules/modules_antag.dm:445
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        deleting ??= FALSE;
        mod.wearer.remove_traits(traits_to_add, REF(this));
        UnregisterSignal(mod.wearer, COMSIG_TRY_MODIFY_SPEECH);
        obj_item_organ_tongue user_tongue = mod.wearer.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        user_tongue.temp_say_mod = Dm.initial(user_tongue, (obj_item_organ_tongue p) => p.temp_say_mod);
        if (Dm.truthy(deleting))
            return;
        obj_item_clothing head_cover = mod.get_part_from_slot(ITEM_SLOT_HEAD) as obj_item_clothing;
        if (istype(head_cover, typeof(obj_item_clothing)))
            head_cover.flash_protect = Dm.initial(head_cover, (obj_item_clothing p) => p.flash_protect);
    }

    // tg: code/modules/mod/modules/modules_antag.dm:456
    [SignalHandler]
    public virtual object on_speech_modification(datum source = null)
    {
        if (!Dm.truthy(mod.active))
            return null;
        //Prevent speech modifications if the suit is active
        return PREVENT_MODIFY_SPEECH;
    }
}

// tg: code/modules/mod/modules/modules_antag.dm:480
public partial class obj_item_mod_module_stealth_wraith : obj_item_mod_module_stealth
{
    // tg: code/modules/mod/modules/modules_antag.dm:493
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot) || target == mod.wearer)
            return dot;
        if (get_dist(mod.wearer, target) > 6)
        {
            balloon_alert(mod.wearer, I18n.T("can't reach that!"));
            return dot;
        }
        if (istype(target, typeof(obj_machinery_power_apc))) //Bit too strong for a module so this is blacklisted
        {
            balloon_alert(mod.wearer, I18n.T("can't disable apc!"));
            return dot;
        }

        DmList things_to_disrupt = Dm.list(target);
        if (isliving(target))
        {
            mob_living live_target = target as mob_living;
            Dm.append(ref things_to_disrupt, live_target.get_all_gear(Dm.num(Dm.bitor(Dm.bitor(INCLUDE_PROSTHETICS, INCLUDE_ABSTRACT), INCLUDE_ACCESSORIES))));
        }

        foreach (atom disrupted in Dm.each<atom>(things_to_disrupt))
        {
            if (Dm.truthy(await disrupted.on_saboteur(this, 1 * MINUTES)))
                await mod.add_charge(DEFAULT_CHARGE_DRAIN * 250);
        }
        return dot;
    }

    // tg: code/modules/mod/modules/modules_antag.dm:513
    public override async DmTask on_activation(mob activator = null)
    {
        return; // Don't activate stealth when the module is activated because the stealth portion of the module is fully passive
    }

    // tg: code/modules/mod/modules/modules_antag.dm:516
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        display_message ??= TRUE;
        deleting ??= FALSE;
        await base.on_deactivation(activator, display_message, deleting);
        UnregisterSignal(mod.wearer, Dm.list(COMSIG_LIVING_MOB_BUMP, COMSIG_ATOM_BUMPED, COMSIG_MOB_FIRED_GUN));
    }

    // tg: code/modules/mod/modules/modules_antag.dm:520
    public override async DmTask on_part_activation()
    {
        await start_stealth();
    }

    // tg: code/modules/mod/modules/modules_antag.dm:523
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        UnregisterSignal(mod.wearer, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_MOB_ITEM_ATTACK, COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_ATTACK_HAND, COMSIG_ATOM_BULLET_ACT, COMSIG_ATOM_HITBY, COMSIG_ATOM_HULK_ATTACK, COMSIG_ATOM_ATTACK_PAW, COMSIG_CARBON_CUFF_ATTEMPTED, COMSIG_LIVING_MOB_BUMP, COMSIG_ATOM_BUMPED, COMSIG_MOB_FIRED_GUN));
        animate(mod.wearer, Dm.named("alpha", 255), Dm.named("time", 1.5f * SECONDS));
        stealth_active = FALSE;
    }

    // tg: code/modules/mod/modules/modules_antag.dm:528
    public virtual async DmTask start_stealth()
    {
        if (!(recloak_timer <= world.time)) // Prevents being able to bypass the cooldown by disabling and re-enabling the module
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(start_stealth)), max(0, recloak_timer - world.time), file: "code/modules/mod/modules/modules_antag.dm", line: 530);
            return;
        }
        RegisterSignals(mod.wearer, Dm.list(COMSIG_LIVING_MOB_BUMP, COMSIG_ATOM_BUMPED, COMSIG_MOB_FIRED_GUN), nameof(unstealth));
        RegisterSignal(mod.wearer, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_unarmed_attack));
        RegisterSignal(mod.wearer, COMSIG_ATOM_BULLET_ACT, nameof(on_bullet_act));
        RegisterSignals(mod.wearer, Dm.list(COMSIG_MOB_ITEM_ATTACK, COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_ATTACK_HAND, COMSIG_ATOM_HITBY, COMSIG_ATOM_HULK_ATTACK, COMSIG_ATOM_ATTACK_PAW, COMSIG_CARBON_CUFF_ATTEMPTED), nameof(unstealth));
        animate(mod.wearer, Dm.named("alpha", stealth_alpha), Dm.named("time", 1.5f * SECONDS));
        await drain_power(use_energy_cost);
        stealth_active = TRUE;
    }

    // tg: code/modules/mod/modules/modules_antag.dm:540
    public override void unstealth(datum source = null)
    {
        if (!Dm.truthy(stealth_active))
            return;
        to_chat(mod.wearer, span_warning(I18n.T("[] gets discharged from contact!", this)));
        do_sparks(2, TRUE, this);
        Waiting.NoSleep(drain_power(use_energy_cost));
        // Don't deactivate() directly as the module may not be active in the first place when stealthing
        Waiting.NoSleep(on_deactivation());
        if (!Dm.truthy(mod.active))
            return;
        recloak_timer = world.time + 20 * SECONDS;
        _addtimer(Dm.@new<datum_callback>(this, nameof(start_stealth)), 20 * SECONDS, file: "code/modules/mod/modules/modules_antag.dm", line: 550);
        stealth_active = FALSE;
    }

    // tg: code/modules/mod/modules/modules_antag.dm:554
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        Dm.append(ref dot, span_info(I18n.T("The Wraith Module does not simply bend light around the user to obscure their visual pattern, but actively attacks and overloads surrounding light emitting objects, repurposing this energy to power the suit. It is possible that this technology has its origins in Spider Clan advancements, but the exact source of the Wraith Module is highly disputed. No group has stepped forward to claim it as their handiwork due to the political consequences of having stolen Spider Clan tech and their inevitable retaliation for such transgressions. Most point fingers at Cybersun Industries, but murmurs suggest it could even be even more clandestine organizations amongst the Syndicate branches. Whatever the case, if you are looking at one of these right now, don't show it to a space ninja.")));
        return dot;
    }
}
