// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy/kinetic_accelerator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the file whole: the proto-kinetic accelerator, its bolt, the pressure rule (a quarter damage where
// lavaland_equipment_pressure_check fails, so on the station's air; full damage in thin air and space), the modkits' base and every
// modkit. Live-partial rows: the cyborg upgrade procs (AI and cyborgs), the screentip elements' rows stay where they were; gaps GU1
// (D333): the fantasy bonuses (the fantasy component that calls them is not ported) and the seclight mount. The projectile's parry waits for its trait's source (the parry component stays inert).

// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:1
public static partial class GlobalProcs
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:1  #define PKA_COOLDOWN_INCREMENT 3.2
    public const float PKA_COOLDOWN_INCREMENT = 3.2f;
}

// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:5
public partial class obj_item_gun_energy_recharge_kinetic_accelerator : obj_item_gun_energy_recharge
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:24
    public override async DmTask add_bayonet_point()
    {
        await AddComponent(typeof(datum_component_bayonet_attachable), Dm.named("offset_x", 20), Dm.named("offset_y", 12));
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:47 and :45 (the two Initialize of this type, in order)
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Only actual KAs can be converted
        if (type == typeof(obj_item_gun_energy_recharge_kinetic_accelerator))
        {
            // var/static/list/slapcraft_recipe_list (a proc-level static, rules 10.6)
            Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), Dm.named("slapcraft_recipes", TgState.Current.obj_item_gun_energy_recharge_kinetic_accelerator__Initialize__slapcraft_recipe_list)));
        }
        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_bare_hands), Dm.named("rmb_text", "Detach a modkit")));
        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_tools), TgState.Current.obj_item_gun_energy_recharge_kinetic_accelerator__Initialize__tool_behaviors));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:39
    public override async DmTask apply_fantasy_bonuses(object bonus = null)
    {
        await base.apply_fantasy_bonuses(bonus);
        max_mod_capacity = Dm.num(modify_fantasy_variable("max_mod_capacity", max_mod_capacity, Dm.num(bonus) * 10));
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:43
    public override async DmTask remove_fantasy_bonuses(object bonus = null)
    {
        max_mod_capacity = Dm.num(reset_fantasy_variable("max_mod_capacity", max_mod_capacity));
        await base.remove_fantasy_bonuses(bonus);
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:62
    public override async DmTask shoot_with_empty_chamber(mob_living user = null)
    {
        playsound(this, dry_fire_sound, 30, TRUE); //click sound but no to_chat message to cut on spam
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:66
    public override async DmTask add_seclight_point()
    {
        await AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")), Dm.named("light_overlay", "flight"), Dm.named("overlay_x", 15), Dm.named("overlay_y", 9));
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:73
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(max_mod_capacity))
        {
            Dm.append(ref dot, I18n.T("<b>[]%</b> mod capacity remaining.", get_remaining_mod_capacity()));
            Dm.append(ref dot, span_info(I18n.T("You can use a <b>crowbar</b> to remove all modules or <b>right-click</b> with an empty hand to remove a specific one.")));
            foreach (obj_item_borg_upgrade_modkit modkit_upgrade in Dm.each<obj_item_borg_upgrade_modkit>(modkits))
                Dm.append(ref dot, span_notice(I18n.T("There is \\a [] installed, using <b>[]%</b> capacity.", modkit_upgrade, modkit_upgrade.cost)));
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:81
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.length(modkits) > 0)
        {
            to_chat(user, span_notice(I18n.T("You pry all the modifications out.")));
            tool.play_tool_sound(this, 100);
            foreach (obj_item_borg_upgrade_modkit modkit_upgrade in Dm.each<obj_item_borg_upgrade_modkit>(modkits.Copy()))
            {
                if (Dm.truthy(modkit_upgrade.removable))
                    await modkit_upgrade.forceMove(drop_location() as atom); //uninstallation handled in Exited(), or /mob/living/silicon/robot/remove_from_upgrades() for borgs
            }
        }
        else
            to_chat(user, span_notice(I18n.T("There are no modifications currently installed.")));
        return TRUE;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:92
    public override async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        return await fire_gun(target, user, Dm.truthy(Waiting.NoSleep(user.Adjacent(target))) && !isturf(target) ? TRUE : FALSE, @params);
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:95
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        if (LAZYLEN(modkits) == 0)
            return SECONDARY_ATTACK_CONTINUE_CHAIN;

        DmList display_names = Dm.list();
        DmList items = Dm.list();
        for (int modkits_length = 1; modkits_length <= Dm.length(modkits); modkits_length++)
        {
            obj_item_borg_upgrade_modkit thing = modkits[modkits_length] as obj_item_borg_upgrade_modkit;
            if (!Dm.truthy(thing.removable))
                continue;
            string key = Dm.text("[] ([])", thing.name, modkits_length);
            display_names[key] = REF(thing);
            image item_image = Dm.image(thing.icon, null, thing.icon_state);
            if (Dm.length(thing.overlays) > 0)
                Dm.append(item_image.overlays, thing.overlays); // item_image.copy_overlays(thing)
            items[key] = item_image;
        }

        object pick = await show_radial_menu(user, this, items, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 36, require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(pick))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        object modkit_reference = display_names[pick];
        // locate(modkit_reference) in modkits
        obj_item_borg_upgrade_modkit modkit_to_remove = Dm.locate_ref(modkit_reference) as obj_item_borg_upgrade_modkit;
        if (modkit_to_remove != null && !Dm.@in(modkit_to_remove, modkits))
            modkit_to_remove = null;
        if (modkit_to_remove == null)
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (!Dm.truthy(await user.put_in_hands(modkit_to_remove)))
            await modkit_to_remove.forceMove(drop_location() as atom);
        update_appearance(UPDATE_ICON);

        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:130
    public virtual object check_menu(mob_living_carbon_human user = null)
    {
        if (!istype(user, typeof(mob_living_carbon_human)))
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:137
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (Dm.@in(gone, modkits))
        {
            obj_item_borg_upgrade_modkit MK = gone as obj_item_borg_upgrade_modkit;
            MK.uninstall(this);
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:143
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (istype(arrived, typeof(obj_item_borg_upgrade_modkit)))
            Dm.combine(ref modkits, arrived);
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:148
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_borg_upgrade_modkit)))
            return NONE;
        await (tool as obj_item_borg_upgrade_modkit).install(this, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:154
    public virtual float get_remaining_mod_capacity()
    {
        float current_capacity_used = 0;
        foreach (obj_item_borg_upgrade_modkit modkit_upgrade in Dm.each<obj_item_borg_upgrade_modkit>(modkits))
            current_capacity_used += modkit_upgrade.cost;
        return max_mod_capacity - current_capacity_used;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:160
    public virtual void modify_projectile(obj_projectile_kinetic kinetic_projectile = null)
    {
        kinetic_projectile.kinetic_gun = this; //do something special on-hit, easy!
        foreach (obj_item_borg_upgrade_modkit modkit_upgrade in Dm.each<obj_item_borg_upgrade_modkit>(modkits))
            modkit_upgrade.modify_projectile(kinetic_projectile);
    }
}

// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:166
public partial class obj_item_gun_energy_recharge_kinetic_accelerator_bdm : obj_item_gun_energy_recharge_kinetic_accelerator
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:170
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_item_borg_upgrade_modkit_cooldown_repeater_bdm repeater_mod = Dm.@new<obj_item_borg_upgrade_modkit_cooldown_repeater_bdm>();
        Waiting.NoSleep(repeater_mod.install(this));
        return dot;
    }
}

//Casing
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:189
public partial class obj_item_ammo_casing_energy_kinetic : obj_item_ammo_casing_energy
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:195
    public override async DmTask ready_proj(atom target = null, mob_living user = null, object quiet = null, string zone_override = null, atom fired_from = null)
    {
        await base.ready_proj(target, user, quiet, zone_override, fired_from);
        if (loc != null && istype(loc, typeof(obj_item_gun_energy_recharge_kinetic_accelerator)))
        {
            obj_item_gun_energy_recharge_kinetic_accelerator KA = loc as obj_item_gun_energy_recharge_kinetic_accelerator;
            KA.modify_projectile(loaded_projectile as obj_projectile_kinetic);
        }
    }
}

//Projectiles
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:203
public partial class obj_projectile_kinetic : obj_projectile
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:215
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddComponent(/datum/component/parriable_projectile, parry_callback = CALLBACK(src, on_parry)): the mining parry; nothing in the
        // port grants TRAIT_MINING_PARRYING yet (the component stays inert), so it is a row (not-translated.md 115)
        Dm.Deferred("code/modules/projectiles/guns/energy/kinetic_accelerator.dm:217", "Initialize: AddComponent(/datum/component/parriable_projectile): the mining parry");
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:219
    public override object Destroy(float? force = null)
    {
        kinetic_gun = null;
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:223
    public override async DmTask<object> prehit_pierce(atom target = null)
    {
        if (Dm.truthy(is_type_in_typecache(target, kinetic_gun?.ignored_mob_types)))
            return PROJECTILE_PIERCE_PHASE;
        object dot = await base.prehit_pierce(target);
        if (Dm.eq(dot, PROJECTILE_PIERCE_PHASE))
            return dot;
        foreach (obj_item_borg_upgrade_modkit modkit_upgrade in Dm.each<obj_item_borg_upgrade_modkit>(kinetic_gun?.modkits))
            await modkit_upgrade.projectile_prehit(this, target, kinetic_gun);
        if (!Dm.truthy(pressure_decrease_active) && !Dm.truthy(lavaland_equipment_pressure_check(get_turf(target))))
        {
            name = Dm.text("weakened []", name);
            damage = damage * pressure_decrease;
            pressure_decrease_active = TRUE;
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:236
    public virtual void on_parry(mob user = null)
    {
        // Ensure that if the user doesn't have tracer mod we're still visible
        icon_state = "ka_tracer";
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:243
    public override async DmTask on_range()
    {
        if (!Dm.truthy(pressure_decrease_active) && !Dm.truthy(lavaland_equipment_pressure_check(get_turf(this))))
        {
            name = Dm.text("weakened []", name);
            damage = damage * pressure_decrease;
            pressure_decrease_active = TRUE;
        }

        await strike_thing(loc);
        await base.on_range();
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:252
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        await strike_thing(target);
        return await base.on_hit(target, blocked, pierce_hit);
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:256
    public virtual async DmTask strike_thing(atom target = null)
    {
        turf target_turf = get_turf(target);
        if (target_turf == null)
            target_turf = get_turf(this);
        if (kinetic_gun != null) //hopefully whoever shot this was not very, very unfortunate.
        {
            DmList mods = kinetic_gun.modkits;
            foreach (obj_item_borg_upgrade_modkit modkit_upgrade in Dm.each<obj_item_borg_upgrade_modkit>(mods))
                await modkit_upgrade.projectile_strike_predamage(this, target_turf, target, kinetic_gun);
            foreach (obj_item_borg_upgrade_modkit modkit_upgrade in Dm.each<obj_item_borg_upgrade_modkit>(mods))
                await modkit_upgrade.projectile_strike(this, target_turf, target, kinetic_gun);
        }
        if (ismineralturf(target_turf))
        {
            turf_closed_mineral M = target_turf as turf_closed_mineral;
            await M.gets_drilled(firer as mob, 1);
            if (iscarbon(firer))
            {
                mob_living_carbon carbon_firer = firer as mob_living_carbon;
                object skill_modifier = 1;
                // If there is a mind, check for skill modifier to allow them to reload faster.
                if (Dm.truthy(carbon_firer.mind))
                    skill_modifier = carbon_firer.mind.get_skill_modifier(typeof(datum_skill_mining), SKILL_SPEED_MODIFIER);
                // A runtime in tg: an accelerator gone before its bolt struck leaves no gun to reload.
                kinetic_gun?.attempt_reload(Dm.num(Dm.mul(kinetic_gun.recharge_time, skill_modifier))); //If you hit a mineral, you might get a quicker reload. epic gamer style.
            }
        }
        obj_effect_temp_visual_kinetic_blast K = Dm.@new<obj_effect_temp_visual_kinetic_blast>(target_turf);
        K.color = color;
    }
}

//mecha_kineticgun version of the projectile
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:281
public partial class obj_projectile_kinetic_mech : obj_projectile_kinetic
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:284
    public override async DmTask strike_thing(atom target = null)
    {
        await base.strike_thing(target);
        Dm.@new<obj_effect_temp_visual_explosion_fast>(get_turf(target));

        foreach (turf_closed_mineral mineral_turf in Dm.each<turf_closed_mineral>(Dm.sub(RANGE_TURFS(2, target), target)))
            await mineral_turf.drill_aoe(firer as mob, 0.1f);

        foreach (mob_living living_mob in Dm.each<mob_living>(Dm.sub(Dm.sub(range(2, target), firer), target)))
        {
            if (!ismining(living_mob))
                continue;
            object armor = living_mob.run_armor_check(def_zone, Dm.str(armor_flag), armour_penetration: armour_penetration);
            await living_mob.apply_damage(damage, Dm.str(damage_type), def_zone, Dm.num(armor));
            to_chat(living_mob, span_userdanger(I18n.T("You're struck by a []!", name)));
        }
    }
}

//Modkits
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:300
public partial class obj_item_borg_upgrade_modkit : obj_item_borg_upgrade
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:321
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Occupies <b>[]%</b> of mod capacity.", cost)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:325
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_gun_energy_recharge_kinetic_accelerator)) || issilicon(user))
            return NONE;
        await install(tool as obj_item_gun_energy_recharge_kinetic_accelerator, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:337
    public virtual async DmTask<object> install(obj_item_gun_energy_recharge_kinetic_accelerator KA = null, mob user = null, float? transfer_to_loc = null)
    {
        transfer_to_loc ??= TRUE;
        if (Dm.truthy(minebot_upgrade))
        {
            if (Dm.truthy(minebot_exclusive) && !istype(KA.loc, typeof(mob_living_basic_mining_drone)))
            {
                if (user != null)
                    to_chat(user, span_notice(I18n.T("The modkit you're trying to install is only rated for minebot use.")));
                return FALSE;
            }
        }
        else if (istype(KA.loc, typeof(mob_living_basic_mining_drone)))
        {
            if (user != null)
                to_chat(user, span_notice(I18n.T("The modkit you're trying to install is not rated for minebot use.")));
            return FALSE;
        }

        // type_to_limit = denied_type, or src: DM's istype(x, an instance) tests against the instance's type
        System.Type type_to_limit = denied_type;

        if (type_to_limit == null && Dm.truthy(maximum_of_type))
            type_to_limit = type;

        if (type_to_limit != null)
        {
            float number_of_denied = 0;
            foreach (obj_item_borg_upgrade_modkit modkit_upgrade in Dm.each<obj_item_borg_upgrade_modkit>(KA.modkits))
            {
                if (istype(modkit_upgrade, type_to_limit))
                    number_of_denied++;
                if (Dm.truthy(maximum_of_type) && number_of_denied >= maximum_of_type || !Dm.truthy(maximum_of_type) && Dm.truthy(number_of_denied)) //if we denied a type, or we have a maximum to reach, break
                {
                    if (user != null)
                        to_chat(user, span_notice(I18n.T("The modkit you're trying to install would conflict with an already installed modkit. Remove existing modkits first.")));
                    return FALSE;
                }
            }
        }

        if (KA.get_remaining_mod_capacity() < cost)
        {
            to_chat(user, span_notice(I18n.T("You don't have room(<b>[]%</b> remaining, []% needed) to install this modkit. Use a crowbar or right click with an empty hand to remove existing modkits.", KA.get_remaining_mod_capacity(), cost)));
            return FALSE;
        }

        if (Dm.truthy(transfer_to_loc))
        {
            if (user != null && !Dm.truthy(await user.transferItemToLoc(this, KA)))
                return FALSE;
            else if (user == null)
                await forceMove(KA);
        }

        if (user != null)
        {
            to_chat(user, span_notice(I18n.T("You install the modkit.")));
            playsound(loc, Dm.resource("sound/items/tools/screwdriver.ogg"), 100, TRUE);
        }
        Dm.combine(ref KA.modkits, this);
        return TRUE;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:385
    public virtual void uninstall(obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        KA.modkits.Remove(this);
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:388
    public virtual void modify_projectile(obj_projectile_kinetic K = null) { }

    //use this one for effects you want to trigger before any damage is done at all and before damage is decreased by pressure
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:391
    public virtual async DmTask projectile_prehit(obj_projectile_kinetic K = null, atom target = null, obj_item_gun_energy_recharge_kinetic_accelerator KA = null) { }

    //use this one for effects you want to trigger before mods that do damage
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:393
    public virtual async DmTask projectile_strike_predamage(obj_projectile_kinetic K = null, turf target_turf = null, atom target = null, obj_item_gun_energy_recharge_kinetic_accelerator KA = null) { }

    //and this one for things that don't need to trigger before other damage-dealing mods
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:395
    public virtual async DmTask projectile_strike(obj_projectile_kinetic K = null, turf target_turf = null, atom target = null, obj_item_gun_energy_recharge_kinetic_accelerator KA = null) { }
}

//Range
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:399
public partial class obj_item_borg_upgrade_modkit_range : obj_item_borg_upgrade_modkit
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:405
    public override void modify_projectile(obj_projectile_kinetic K = null) => K.range = Dm.num(K.range) + modifier;
}

//Damage
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:411
public partial class obj_item_borg_upgrade_modkit_damage : obj_item_borg_upgrade_modkit
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:416
    public override void modify_projectile(obj_projectile_kinetic K = null) => K.damage = K.damage + modifier;
}

//Cooldown
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:422
public partial class obj_item_borg_upgrade_modkit_cooldown : obj_item_borg_upgrade_modkit
{
    // Recalculate recharge time after adding or removing cooldown mods.
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:429
    public virtual float get_recharge_time(obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        float new_recharge_time = Dm.num(Dm.initial(KA.type, (obj_item_gun_energy_recharge_kinetic_accelerator g) => g.recharge_time));
        // istype(modkit_upgrade, src): DM tests against the instance's type
        foreach (obj_item_borg_upgrade_modkit modkit_upgrade in Dm.each<obj_item_borg_upgrade_modkit>(KA.modkits))
        {
            if (istype(modkit_upgrade, type))
                new_recharge_time += modkit_upgrade.modifier;
        }
        return max(new_recharge_time, 0);
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:437
    public override async DmTask<object> install(obj_item_gun_energy_recharge_kinetic_accelerator KA = null, mob user = null, float? transfer_to_loc = null)
    {
        object dot = await base.install(KA, user, transfer_to_loc);
        if (Dm.truthy(dot))
            KA.recharge_time = get_recharge_time(KA);
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:442
    public override void uninstall(obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        base.uninstall(KA);
        KA.recharge_time = get_recharge_time(KA);
    }
}

// AoE blasts
// These are a subtype of cooldown so they can affect firing speed
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:460
public partial class obj_item_borg_upgrade_modkit_cooldown_aoe : obj_item_borg_upgrade_modkit_cooldown
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:471
    public override async DmTask<object> install(obj_item_gun_energy_recharge_kinetic_accelerator KA = null, mob user = null, float? transfer_to_loc = null)
    {
        object dot = await base.install(KA, user, transfer_to_loc);
        if (!Dm.truthy(dot))
            return dot;
        foreach (obj_item_borg_upgrade_modkit_cooldown_aoe AOE in Dm.each<obj_item_borg_upgrade_modkit_cooldown_aoe>(KA.modkits)) //make sure only one of the aoe modules has values if somebody has multiple
        {
            if (Dm.truthy(AOE.stats_stolen) || AOE == this)
                continue;
            modifier += AOE.modifier; //take its modifiers
            damage_modifier += AOE.damage_modifier;
            AOE.modifier = 0;
            AOE.damage_modifier = 0;
            turf_aoe += AOE.turf_aoe;
            AOE.turf_aoe = FALSE;
            AOE.stats_stolen = TRUE;
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:486
    public override void uninstall(obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        base.uninstall(KA);
        modifier = Dm.num(Dm.initial(type, (obj_item_borg_upgrade_modkit_cooldown_aoe m) => m.modifier)); //get our modifiers back
        damage_modifier = Dm.num(Dm.initial(type, (obj_item_borg_upgrade_modkit_cooldown_aoe m) => m.damage_modifier));
        turf_aoe = Dm.num(Dm.initial(type, (obj_item_borg_upgrade_modkit_cooldown_aoe m) => m.turf_aoe));
        stats_stolen = FALSE;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:493
    public override void modify_projectile(obj_projectile_kinetic K = null) => K.name = "kinetic explosion";

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:496
    public override async DmTask projectile_strike(obj_projectile_kinetic kinetic_blast = null, turf target_turf = null, atom target = null, obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        if (Dm.truthy(stats_stolen))
            return;
        Dm.@new<obj_effect_temp_visual_explosion_fast>(target_turf);
        if (Dm.truthy(turf_aoe))
        {
            foreach (object T in Dm.each(Dm.sub(RANGE_TURFS(1, target_turf), target_turf)))
            {
                if (ismineralturf(T))
                {
                    turf_closed_mineral M = T as turf_closed_mineral;
                    await M.gets_drilled(kinetic_blast.firer as mob, 0.1f);
                }
            }
        }

        if (!Dm.truthy(damage_modifier))
            return;

        foreach (mob_living living_mob in Dm.each<mob_living>(Dm.sub(Dm.sub(range(1, target), kinetic_blast.firer), target)))
        {
            if (!ismining(living_mob))
                continue;
            object armor = living_mob.run_armor_check(kinetic_blast.def_zone, Dm.str(kinetic_blast.armor_flag), armour_penetration: kinetic_blast.armour_penetration);
            await living_mob.apply_damage(kinetic_blast.damage * damage_modifier, Dm.str(kinetic_blast.damage_type), kinetic_blast.def_zone, Dm.num(armor));
            to_chat(living_mob, span_userdanger(I18n.T("You're struck by a []!", kinetic_blast.name)));
        }
    }
}

// Minebot passthrough
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:538
public partial class obj_item_borg_upgrade_modkit_minebot_passthrough : obj_item_borg_upgrade_modkit
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:543
    public override async DmTask<object> install(obj_item_gun_energy_recharge_kinetic_accelerator KA = null, mob user = null, float? transfer_to_loc = null)
    {
        object dot = await base.install(KA, user, transfer_to_loc);
        LAZYADD(ref KA.ignored_mob_types, typecacheof(typeof(mob_living_basic_mining_drone)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:547
    public override void uninstall(obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        base.uninstall(KA);
        LAZYREMOVE(ref KA.ignored_mob_types, typecacheof(typeof(mob_living_basic_mining_drone)));
    }
}

// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:552
public partial class obj_item_borg_upgrade_modkit_human_passthrough : obj_item_borg_upgrade_modkit
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:557
    public override async DmTask<object> install(obj_item_gun_energy_recharge_kinetic_accelerator KA = null, mob user = null, float? transfer_to_loc = null)
    {
        object dot = await base.install(KA, user, transfer_to_loc);
        LAZYADD(ref KA.ignored_mob_types, typecacheof(typeof(mob_living_carbon_human)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:561
    public override void uninstall(obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        base.uninstall(KA);
        LAZYREMOVE(ref KA.ignored_mob_types, typecacheof(typeof(mob_living_carbon_human)));
    }
}

//Tendril-unique modules
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:567
public partial class obj_item_borg_upgrade_modkit_cooldown_repeater : obj_item_borg_upgrade_modkit_cooldown
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:574
    public override async DmTask projectile_strike_predamage(obj_projectile_kinetic K = null, turf target_turf = null, atom target = null, obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        bool valid_repeat = false;
        if (isliving(target))
        {
            mob_living L = target as mob_living;
            if (!Dm.eq(L.stat, DEAD))
                valid_repeat = true;
        }
        if (ismineralturf(target_turf))
            valid_repeat = true;
        if (valid_repeat)
        {
            await KA.cell.use(KA.cell.charge);
            KA.attempt_reload(KA.recharge_time * 0.25f); //If you hit, the cooldown drops to 0.75 seconds.
        }
    }
}

// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:593
public partial class obj_item_borg_upgrade_modkit_lifesteal : obj_item_borg_upgrade_modkit
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:600
    public override async DmTask projectile_prehit(obj_projectile_kinetic K = null, atom target = null, obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        if (isliving(target) && isliving(K.firer))
        {
            mob_living L = target as mob_living;
            if (Dm.eq(L.stat, DEAD))
                return;
            L = K.firer as mob_living;
            await L.heal_ordered_damage(modifier, TgState.Current.obj_item_borg_upgrade_modkit_lifesteal__damage_heal_order);
        }
    }
}

// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:609
public partial class obj_item_borg_upgrade_modkit_resonator_blasts : obj_item_borg_upgrade_modkit
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:616
    public override async DmTask projectile_strike(obj_projectile_kinetic K = null, turf target_turf = null, atom target = null, obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        if (target_turf != null && !ismineralturf(target_turf)) //Don't make fields on mineral turfs.
        {
            obj_effect_temp_visual_resonance R = Dm.locate<obj_effect_temp_visual_resonance>(target_turf);
            if (R != null)
            {
                R.damage_multiplier = modifier;
                R.burst();
                return;
            }
            Dm.@new<obj_effect_temp_visual_resonance>(target_turf, K.firer, null, RESONATOR_MODE_MANUAL, 100); //manual detonate mode and will NOT spread
        }
    }
}

// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:626
public partial class obj_item_borg_upgrade_modkit_bounty : obj_item_borg_upgrade_modkit
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:635
    public override async DmTask projectile_prehit(obj_projectile_kinetic K = null, atom target = null, obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        if (isliving(target))
        {
            mob_living L = target as mob_living;
            DmList existing_marks = L.has_status_effect_list(typeof(datum_status_effect_syphon_mark)) as DmList;
            foreach (object i in Dm.each(existing_marks))
            {
                datum_status_effect_syphon_mark SM = i as datum_status_effect_syphon_mark;
                if (SM.reward_target == this) //we want to allow multiple people with bounty modkits to use them, but we need to replace our own marks so we don't multi-reward
                {
                    SM.reward_target = null;
                    qdel(SM);
                }
            }
            await L.apply_status_effect(typeof(datum_status_effect_syphon_mark), this);
        }
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:646
    public override async DmTask projectile_strike(obj_projectile_kinetic K = null, turf target_turf = null, atom target = null, obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        if (isliving(target))
        {
            mob_living L = target as mob_living;
            if (Dm.truthy(bounties_reaped[L.type]))
            {
                float kill_modifier = 1;
                if (Dm.truthy(K.pressure_decrease_active))
                    kill_modifier *= K.pressure_decrease;
                object armor = L.run_armor_check(K.def_zone, Dm.str(K.armor_flag), "", "", K.armour_penetration);
                await L.apply_damage(Dm.num(bounties_reaped[L.type]) * kill_modifier, Dm.str(K.damage_type), K.def_zone, Dm.num(armor));
            }
        }
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:656
    public virtual void get_kill(mob_living L = null)
    {
        float bonus_mod = 1;
        if (ismegafauna(L)) //megafauna reward
            bonus_mod = 4;
        if (!Dm.truthy(bounties_reaped[L.type]))
            bounties_reaped[L.type] = min(modifier * bonus_mod, maximum_bounty);
        else
            bounties_reaped[L.type] = min(Dm.num(bounties_reaped[L.type]) + (modifier * bonus_mod), maximum_bounty);
    }
}

//Indoors
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:667
public partial class obj_item_borg_upgrade_modkit_indoors : obj_item_borg_upgrade_modkit
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:673
    public override void modify_projectile(obj_projectile_kinetic K = null) => K.pressure_decrease *= modifier;
}

//Trigger Guard
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:679
public partial class obj_item_borg_upgrade_modkit_trigger_guard : obj_item_borg_upgrade_modkit
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:685
    public override async DmTask<object> install(obj_item_gun_energy_recharge_kinetic_accelerator KA = null, mob user = null, float? transfer_to_loc = null)
    {
        object dot = await base.install(KA, user, transfer_to_loc);
        if (Dm.truthy(dot))
            KA.trigger_guard = TRIGGER_GUARD_ALLOW_ALL;
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:690
    public override void uninstall(obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        KA.trigger_guard = TRIGGER_GUARD_NORMAL;
        base.uninstall(KA);
    }
}

//Cosmetic
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:698
public partial class obj_item_borg_upgrade_modkit_chassis_mod : obj_item_borg_upgrade_modkit
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:705
    public override async DmTask<object> install(obj_item_gun_energy_recharge_kinetic_accelerator KA = null, mob user = null, float? transfer_to_loc = null)
    {
        object dot = await base.install(KA, user, transfer_to_loc);
        if (Dm.truthy(dot))
        {
            KA.icon_state = chassis_icon;
            KA.inhand_icon_state = chassis_icon;
            KA.name = chassis_name;
            if (iscarbon(KA.loc))
                await (KA.loc as mob_living_carbon).update_held_items();
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:715
    public override void uninstall(obj_item_gun_energy_recharge_kinetic_accelerator KA = null)
    {
        KA.icon_state = Dm.str(Dm.initial(KA.type, (obj_item_gun_energy_recharge_kinetic_accelerator g) => g.icon_state));
        KA.inhand_icon_state = Dm.str(Dm.initial(KA.type, (obj_item_gun_energy_recharge_kinetic_accelerator g) => g.inhand_icon_state));
        KA.name = Dm.str(Dm.initial(KA.type, (obj_item_gun_energy_recharge_kinetic_accelerator g) => g.name));
        if (iscarbon(KA.loc))
            Waiting.NoSleep((KA.loc as mob_living_carbon).update_held_items());
        base.uninstall(KA);
    }
}

// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:731
public partial class obj_item_borg_upgrade_modkit_tracer : obj_item_borg_upgrade_modkit
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:737
    public override void modify_projectile(obj_projectile_kinetic K = null)
    {
        K.icon_state = "ka_tracer";
        K.color = bolt_color;
    }
}

// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:742
public partial class obj_item_borg_upgrade_modkit_tracer_adjustable : obj_item_borg_upgrade_modkit_tracer
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:745
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        Waiting.Detach(choose_bolt_color(user));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:749
    public virtual async DmTask choose_bolt_color(mob user = null)
    {
        object new_color = await tgui_color_picker(user, "", I18n.T("Choose Color"), bolt_color);
        bolt_color = Dm.truthy(new_color) ? Dm.str(new_color) : bolt_color;
    }
}

// The static lists of the accelerator and its modkits (rules 10.6: type-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:30  var/static/list/slapcraft_recipe_list
    internal DmList obj_item_gun_energy_recharge_kinetic_accelerator__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_ebow));
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:53  var/static/list/tool_behaviors
    internal DmList obj_item_gun_energy_recharge_kinetic_accelerator__Initialize__tool_behaviors = Dm.list(Dm.kv(TOOL_CROWBAR, Dm.list(Dm.kv(SCREENTIP_CONTEXT_LMB, "Eject all modkits"))));
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:597  var/static/list/damage_heal_order
    internal DmList obj_item_borg_upgrade_modkit_lifesteal__damage_heal_order = Dm.list(BRUTE, BURN, OXY);
}

// tg: code/datums/status_effects/neutral.dm:37
public partial class datum_status_effect_syphon_mark : datum_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:44
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        obj_item_borg_upgrade_modkit_bounty new_reward_target = Dm.arg<obj_item_borg_upgrade_modkit_bounty>(__extra, 0);
        object dot = await base.on_creation(new_owner);
        if (Dm.truthy(dot))
            reward_target = new_reward_target;
        return dot;
    }

    // tg: code/datums/status_effects/neutral.dm:49
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (Dm.eq(owner.stat, DEAD))
            return FALSE;
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/neutral.dm:54
    public virtual void get_kill()
    {
        if (!QDELETED(reward_target))
            reward_target.get_kill(owner);
    }

    // tg: code/datums/status_effects/neutral.dm:58
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (Dm.eq(owner.stat, DEAD))
        {
            get_kill();
            qdel(this);
        }
        return null;
    }

    // tg: code/datums/status_effects/neutral.dm:63
    public override async DmTask<object> on_remove()
    {
        get_kill();
        return await base.on_remove();
    }
}

// The modkits' cyborg install and removal (silicons SI2, D326)
// tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:331
public partial class obj_item_borg_upgrade_modkit : obj_item_borg_upgrade
{
    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:331
    public override async DmTask<object> action(mob_living_silicon_robot R = null, mob_living user = null)
    {
        object dot = await base.action(R, user);
        if (Dm.truthy(dot))
        {
            foreach (obj_item_gun_energy_recharge_kinetic_accelerator_cyborg H in Dm.each<obj_item_gun_energy_recharge_kinetic_accelerator_cyborg>(R.model.modules))
                return await install(H, usr, FALSE);
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/kinetic_accelerator.dm:379
    public override async DmTask<object> deactivate(mob_living_silicon_robot R = null, mob_living user = null)
    {
        object dot = await base.deactivate(R, user);
        if (Dm.truthy(dot))
        {
            foreach (obj_item_gun_energy_recharge_kinetic_accelerator_cyborg KA in Dm.each<obj_item_gun_energy_recharge_kinetic_accelerator_cyborg>(R.model.modules))
                uninstall(KA);
        }
        return dot;
    }
}
