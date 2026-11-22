// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/equipment/weapons/weapons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:1
public partial class obj_item_mecha_parts_mecha_equipment_weapon : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:25
    public override async DmTask<object> special_attaching_interaction(object attach_right = null, obj_vehicle_sealed_mecha mech = null, mob user = null, object checkonly = null)
    {
        obj_item_mecha_parts_mecha_equipment_concealed_weapon_bay bay;
        if (Dm.truthy(attach_right))
            bay = mech.equip_by_category[MECHA_R_ARM] as obj_item_mecha_parts_mecha_equipment_concealed_weapon_bay;
        else
            bay = mech.equip_by_category[MECHA_L_ARM] as obj_item_mecha_parts_mecha_equipment_concealed_weapon_bay;
        if (bay == null)
            return FALSE; //No bay, use normal attach procs
        if (Dm.truthy(checkonly))
            return TRUE;
        name = bay.name;
        icon = bay.icon;
        icon_state = bay.icon_state;
        await bay.detach();
        await bay.forceMove(this); //for later detaching
        await attach(mech, attach_right);
        user.visible_message(span_notice(I18n.T("[] inserts something into [].", user, this)), span_notice(I18n.T("You attach the [] into the concealed weapon bay.", Dm.initial(this, "name"))));
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:44
    public override async DmTask detach(atom moveto = null)
    {
        foreach (obj_item_mecha_parts_mecha_equipment_concealed_weapon_bay bay in Dm.each<obj_item_mecha_parts_mecha_equipment_concealed_weapon_bay>(contents.Copy()))
            await bay.forceMove(get_turf(chassis));
        name = Dm.initial(this, "name") as string;
        icon = Dm.initial(this, "icon");
        icon_state = Dm.initial(this, "icon_state") as string;
        await base.detach(moveto);
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:52
    public override async DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null)
    {
        if (!Dm.truthy(action_checks(target)))
            return FALSE;

        /// Find our mecha, find the opposite direction. Used for kickback while the mecha is drifting in zero-g to launch us in this direction.
        object newtonian_target = dir2angle(REVERSE_DIR(Dm.num(chassis.dir)));
        object dot = await base.action(source, target, modifiers);//start the cooldown early because of sleeps
        for (int projectiles_to_shoot = 1; projectiles_to_shoot <= projectiles_per_shot; projectiles_to_shoot++)
        {
            if (Dm.truthy(energy_drain) && !chassis.has_charge(energy_drain))//in case we run out of energy mid-burst, such as emp
                break;
            float spread = 0;
            if (Dm.truthy(variance))
            {
                if (Dm.truthy(randomspread))
                    spread = Dm.num(round((Dm.num(rand(0, 1)) - 0.5f) * variance, 1));
                else
                    spread = Dm.num(round((projectiles_to_shoot / projectiles_per_shot - 0.5f) * variance, 1));
            }

            obj_projectile projectile_obj = Dm.@new(projectile, get_turf(this)) as obj_projectile;
            projectile_obj.log_override = TRUE; //we log being fired ourselves a little further down.
            projectile_obj.firer = chassis;
            projectile_obj.fired_from = this; // mech = firer, equipment = fired from
            await projectile_obj.aim_projectile(target, source, modifiers, spread);
            if (isliving(source) && source.client != null) //dont want it to happen from syndie mecha npc mobs, they do direct fire anyways
                projectile_obj.hit_prone_targets = Dm.num((source as mob_living).combat_mode);
            await projectile_obj.fire();
            if (!Dm.truthy(projectile_obj.suppressed) && firing_effect_type != null)
            {
                object firing = Dm.@new(firing_effect_type, (object)chassis ?? get_turf(this), chassis.dir);
                ShotGeometry.MarkArm(projectile_obj, firing as atom, chassis.equip_by_category[MECHA_R_ARM] == this); // DEVIATION(DV361)
            }
            playsound(chassis, fire_sound, 50, TRUE);

            log_combat(source, target, Dm.text("fired [] at", projectile_obj), this, Dm.text("from [] at []", chassis, get_area_name(this, TRUE)));

            await sleep(Math.Max(0, projectile_delay));

            if (Dm.truthy(kickback))
                await chassis.newtonian_move(newtonian_target);
        }
        chassis.log_message(Dm.text("[] fired [], targeting [].", key_name(source), this, target), LOG_ATTACK);
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:199
public partial class obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal_cryo : obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:199
    public override async DmTask<object> try_attach_part(mob user = null, obj_vehicle_sealed_mecha themech = null, object attach_right = null)
    {
        bool has_molten = false;
        foreach (obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal thegun in Dm.each<obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal>(themech.flat_equipment))
        {
            if (istype(thegun, typeof(obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal_cryo)))
            {
                to_chat(user, span_warning(I18n.T("[] already has [] installed!", themech, thegun)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (istype(thegun, typeof(obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal_inferno)))
                has_molten = true;
        }
        if (has_molten)
        {
            foreach (obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal thegun in Dm.each<obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal>(themech.flat_equipment))
            {
                if (istype(thegun, typeof(obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal_inferno)))
                    thegun.equip_cooldown = 8;
            }
            equip_cooldown = 8;
        }
        return await base.try_attach_part(user, themech, attach_right);
    }
}

// tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:214
public partial class obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal_inferno : obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:214
    public override async DmTask<object> try_attach_part(mob user = null, obj_vehicle_sealed_mecha themech = null, object attach_right = null)
    {
        bool has_cryo = false;
        foreach (obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal thegun in Dm.each<obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal>(themech.flat_equipment))
        {
            if (istype(thegun, typeof(obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal_inferno)))
            {
                to_chat(user, span_warning(I18n.T("[] already has [] installed!", themech, thegun)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (istype(thegun, typeof(obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal_cryo)))
                has_cryo = true;
        }
        if (has_cryo)
        {
            foreach (obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal thegun in Dm.each<obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal>(themech.flat_equipment))
            {
                if (istype(thegun, typeof(obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal_cryo)))
                    thegun.equip_cooldown = 8;
            }
            equip_cooldown = 8;
        }
        return await base.try_attach_part(user, themech, attach_right);
    }
}

// tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:177
public partial class obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal : obj_item_mecha_parts_mecha_equipment_weapon_energy
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:229
    public override async DmTask detach(atom moveto = null)
    {
        foreach (obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal thermal_gun in Dm.each<obj_item_mecha_parts_mecha_equipment_weapon_energy_thermal>(chassis.flat_equipment))
            thermal_gun.equip_cooldown = 20;
        await base.detach(moveto);
    }
}

// tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:258
public partial class obj_item_mecha_parts_mecha_equipment_weapon_honker : obj_item_mecha_parts_mecha_equipment_weapon
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:270
    public override async DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null)
    {
        if (!Dm.truthy(action_checks(target)))
            return null;
        playsound(chassis, Dm.resource("sound/items/airhorn/airhorn.ogg"), 100, TRUE);
        to_chat(source, "<font color='red' size='5'>HONK</font>");
        DmList heard = hearers(6, chassis);
        Dm.remove(heard, chassis); // ohearers
        foreach (mob_living_carbon M in Dm.each<mob_living_carbon>(heard))
        {
            if (HAS_TRAIT(M, TRAIT_DEAF))
                continue;
            turf turf_check = get_turf(M);
            if (isspaceturf(turf_check) && !Dm.truthy(await turf_check.Adjacent(this))) //in space nobody can hear you honk.
                continue;
            to_chat(M, "<font color='red' size='7'>HONK</font>");
            await M.SetSleeping(0);
            await M.adjust_stutter(40 * SECONDS);
            await M.sound_damage(deafen: 30 * SECONDS);
            await M.Paralyze(60);
            if (prob(30))
            {
                await M.Stun(200);
                await M.Unconscious(80);
            }
            else
                await M.set_jitter_if_lower(1000 * SECONDS);
        }

        log_message(Dm.text("Honked from []. HONK!", name), LOG_MECHA);
        turf T = get_turf(this);
        message_admins(Dm.text("[] used a Mecha Honker in []", ADMIN_LOOKUPFLW(source), ADMIN_VERBOSEJMP(T)));
        source.log_message(Dm.text("used a Mecha Honker at [].", AREACOORD(T)), LOG_GAME);
        source.log_message(Dm.text("used a Mecha Honker at [].", AREACOORD(T)), LOG_ATTACK);
        return await base.action(source, target, modifiers);
    }
}

//Base ballistic weapon type
// tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:301
public partial class obj_item_mecha_parts_mecha_equipment_weapon_ballistic : obj_item_mecha_parts_mecha_equipment_weapon
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:310
    public override DmList get_snowflake_data() => Dm.list(
        Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_WEAPON_BALLISTIC),
        Dm.kv("projectiles", projectiles),
        Dm.kv("max_magazine", Dm.initial(this, nameof(projectiles))),
        Dm.kv("projectiles_cache", projectiles_cache),
        Dm.kv("projectiles_cache_max", projectiles_cache_max),
        Dm.kv("disabledreload", disabledreload),
        Dm.kv("ammo_type", ammo_type));

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:321
    public override object action_checks(atom target = null)
    {
        if (!Dm.truthy(base.action_checks(target)))
            return FALSE;
        if (Dm.num(projectiles) <= 0)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:328
    public override DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.eq(action, "reload"))
        {
            rearm();
            return DmTask<object>.FromResult(TRUE);
        }
        return DmTask<object>.FromResult(null);
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:333
    public override object rearm()
    {
        float full = Dm.num(Dm.initial(this, nameof(projectiles)));
        if (Dm.num(projectiles) < full)
        {
            float projectiles_to_add = full - Dm.num(projectiles);
            if (!Dm.truthy(projectiles_cache))
                return FALSE;
            if (projectiles_to_add <= Dm.num(projectiles_cache))
            {
                projectiles = Dm.num(projectiles) + projectiles_to_add;
                projectiles_cache = Dm.num(projectiles_cache) - projectiles_to_add;
            }
            else
            {
                projectiles = Dm.num(projectiles) + Dm.num(projectiles_cache);
                projectiles_cache = 0;
            }
            log_message(Dm.text("Rearmed [].", this), LOG_MECHA);
            return TRUE;
        }
        return null;
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:347
    public override object needs_rearm() => Dm.num(projectiles) <= 0 ? TRUE : FALSE;

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:351
    public override async DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null)
    {
        object dot = await base.action(source, target, modifiers);
        if (!Dm.truthy(dot))
            return dot;
        projectiles = Dm.num(projectiles) - projectiles_per_shot;
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:446
public partial class obj_item_mecha_parts_mecha_equipment_weapon_ballistic_launcher : obj_item_mecha_parts_mecha_equipment_weapon_ballistic
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:451
    public override async DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null)
    {
        if (!Dm.truthy(action_checks(target)))
            return null;
        chassis.timer_cooldown_start(COOLDOWN_MECHA_EQUIPMENT(type), get_equip_cooldown(target));
        chassis.use_energy(energy_drain);
        object newtonian_target = dir2angle(REVERSE_DIR(Dm.num(chassis.dir)));
        obj O = Dm.@new(projectile, chassis.loc) as obj;
        playsound(chassis, fire_sound, 50, TRUE);
        log_message(Dm.text("Launched a [] from [], targeting [].", O.name, name, target), LOG_MECHA);
        projectiles = Dm.num(projectiles) - 1;
        proj_init(O, source);
        await O.throw_at(target, missile_range, missile_speed, source, FALSE, diagonals_first: diags_first);
        await sleep(Math.Max(0, projectile_delay));
        if (Dm.truthy(kickback))
            await chassis.newtonian_move(newtonian_target);
        return TRUE;
    }

    //used for projectile initilisation (priming flashbang) and additional logging
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:469
    public virtual void proj_init(obj O = null, mob user = null)
    {
    }
}

// tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:473
public partial class obj_item_mecha_parts_mecha_equipment_weapon_ballistic_launcher_flashbang : obj_item_mecha_parts_mecha_equipment_weapon_ballistic_launcher
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:488
    public override void proj_init(obj F = null, mob user = null)
    {
        turf T = get_turf(this);
        message_admins(Dm.text("[] fired a [] in []", ADMIN_LOOKUPFLW(user), F, ADMIN_VERBOSEJMP(T)));
        user.log_message(Dm.text("fired a [] in [].", F, AREACOORD(T)), LOG_GAME);
        user.log_message(Dm.text("fired a [] in [].", F, AREACOORD(T)), LOG_ATTACK);
        _addtimer(Dm.@new<datum_callback>(F, "detonate"), det_time, file: "code/modules/vehicles/mecha/equipment/weapons/weapons.dm", line: 493);
    }
}

// tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:522
public partial class obj_item_mecha_parts_mecha_equipment_weapon_ballistic_launcher_mousetrap_mortar : obj_item_mecha_parts_mecha_equipment_weapon_ballistic_launcher
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:537
    public override void proj_init(obj M = null, mob user = null) => (M as obj_item_assembly_mousetrap_armed).secured = TRUE;
}

//Classic extending punching glove, but weaponised!
// tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:541
public partial class obj_item_mecha_parts_mecha_equipment_weapon_ballistic_launcher_punching_glove : obj_item_mecha_parts_mecha_equipment_weapon_ballistic_launcher
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:562
    public override DmList get_snowflake_data()
    {
        DmList dot = base.get_snowflake_data();
        dot["mode"] = Dm.truthy(harmful) ? "LETHAL FISTING" : "Cuddles";
        dot["mode_label"] = "Honk Severity";
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:567
    public override async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.handle_ui_act(action, @params, ui, state);
        if (Dm.eq(action, "change_mode"))
        {
            harmful = Dm.truthy(harmful) ? 0 : 1;
            if (Dm.truthy(harmful))
                to_chat(usr, span_warning(I18n.T("Lethal Fisting Enabled.")));
            else
                to_chat(usr, span_warning(I18n.T("Lethal Fisting Disabled.")));
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:577
    public override async DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null)
    {
        object dot = await base.action(source, target, modifiers);
        if (Dm.truthy(dot))
            to_chat(usr, "<font color='red' size='5'>HONK</font>");
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:582
    public override void proj_init(obj O = null, mob user = null)
    {
        obj_item_punching_glove PG = O as obj_item_punching_glove;
        if (PG == null)
            return;

        if (Dm.truthy(harmful))
            PG.throwforce = punch_damage;
        else
            PG.throwforce = 0;

        chassis.Beam(PG, icon_state: "chain", time: missile_range * 20, maxdistance: missile_range + 2);
    }
}

// tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:593
public partial class obj_item_punching_glove : obj_item
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:600
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        if (!Dm.truthy(base.throw_impact(hit_atom, throwingdatum, __extra)))
        {
            if (ismovable(hit_atom))
            {
                atom_movable AM = hit_atom as atom_movable;
                Waiting.Detach(AM.safe_throw_at(get_edge_target_turf(AM, get_dir(this, AM)) as atom, Math.Clamp(Dm.num(round(Dm.num(throwforce) / 5)), 2, 20), 2, force: MOVE_FORCE_EXTREMELY_STRONG)); //Throws them equal to damage/5, with a min range of 2 and max range of 20
            }
            qdel(this);
        }
        return null;
    }
}

///long claw of the law
// tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:632
public partial class obj_item_mecha_parts_mecha_equipment_weapon_paddy_claw : obj_item_mecha_parts_mecha_equipment_weapon
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:654
    public override async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        await base.attach(new_mecha, attach_right);
        secmech = chassis as obj_vehicle_sealed_mecha_ripley;
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:658
    public override async DmTask detach(atom moveto = null)
    {
        secmech = null;
        await base.detach(moveto);
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:662
    public override async DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null)
    {
        if (secmech.cargo_hold == null) //We did try
            throw Dm.CRASH(Dm.text("Mech [] has a claw device, but no internal storage. This should be impossible.", chassis), "code/modules/vehicles/mecha/equipment/weapons/weapons.dm", 664);
        if (!Dm.truthy(action_checks(target)))
            return null;
        object dot = null;
        if (isliving(target))
        {
            dot = await base.action(source, target, modifiers);
            mob_living mobtarget = target as mob_living;
            if (Dm.eq(mobtarget.move_resist, MOVE_FORCE_OVERPOWERING)) //No megafauna or bolted AIs, please.
            {
                balloon_alert(source, I18n.T("too strong!"));
                return dot;
            }
            if (secmech.cargo_hold.contents.Count >= Dm.num(secmech.cargo_hold.cargo_capacity))
            {
                balloon_alert(source, I18n.T("no room!"));
                return dot;
            }

            playsound(chassis, clampsound, 50, FALSE, -6);
            mobtarget.visible_message(span_notice(I18n.T("[] lifts [] into its internal holding cell.", chassis, mobtarget)), span_userdanger(I18n.T("[] grips you with [] and prepares to load you into []!", chassis, this, secmech.cargo_hold)));
            if (!Dm.truthy(await do_after_cooldown(mobtarget, source, flags: Dm.bitor(MECH_DO_AFTER_DIR_CHANGE_FLAG, MECH_DO_AFTER_ADJACENCY_FLAG))))
                return dot;
            await mobtarget.forceMove(secmech.cargo_hold);
            log_message(Dm.text("Loaded []. Cargo compartment capacity: []", mobtarget, secmech.cargo_hold.cargo_capacity - secmech.cargo_hold.contents.len), LOG_MECHA);
            to_chat(source, span_notice(I18n.T("[] successfully loaded.", mobtarget)));
            to_chat(mobtarget, span_warning(I18n.T("You have been moved into []. You can attempt to resist out if you wish.", secmech.cargo_hold)));
            if (Dm.truthy(autocuff) && iscarbon(target))
            {
                mob_living_carbon carbontarget = target as mob_living_carbon;
                await carbontarget.set_handcuffed(Dm.@new(cuff_type, carbontarget));
            }
            return dot;
        }

        if (istype(target, typeof(obj_machinery_door)))
        {
            dot = await base.action(source, target, modifiers);
            obj_machinery_door target_door = target as obj_machinery_door;
            playsound(chassis, clampsound, 50, FALSE, -6);
            await target_door.try_to_crowbar(this, source);
            return dot;
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:697
    public override DmList get_snowflake_data() => Dm.list(
        Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_CLAW),
        Dm.kv("autocuff", autocuff));

    // tg: code/modules/vehicles/mecha/equipment/weapons/weapons.dm:703
    public override DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.eq(action, "togglecuff"))
        {
            autocuff = Dm.truthy(autocuff) ? 0 : 1;
            return DmTask<object>.FromResult(TRUE);
        }
        return DmTask<object>.FromResult(null);
    }
}
