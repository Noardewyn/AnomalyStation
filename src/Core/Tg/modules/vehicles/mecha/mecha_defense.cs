// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mecha_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*!
 * # Mecha defence explanation
 * Mechs focus is on a more heavy-but-slower damage approach
 * For this they have the following mechanics
 *
 * ## Backstab
 * Basically the tldr is that mechs are less flexible so we encourage good positioning, pretty simple
 * ## Armor modules
 * Pretty simple, adds armor, you can choose against what
 * ## Internal damage
 * When taking damage will force you to take some time to repair, encourages improvising in a fight
 * Targeting different def zones will damage them to encurage a more strategic approach to fights
 * where they target the "dangerous" modules
 */

// tg: code/modules/vehicles/mecha/mecha_defense.dm:17
public partial class obj_vehicle_sealed_mecha : obj_vehicle_sealed
{
    /// tries to damage mech equipment depending on damage and where is being targeted
    // tg: code/modules/vehicles/mecha/mecha_defense.dm:17
    public virtual async DmTask try_damage_component(object damage = null, object def_zone = null)
    {
        if (Dm.num(damage) < component_damage_threshold)
            return;
        obj_item_mecha_parts_mecha_equipment gear = null;
        if (Dm.eq(def_zone, BODY_ZONE_L_ARM))
            gear = equip_by_category[MECHA_L_ARM] as obj_item_mecha_parts_mecha_equipment;
        else if (Dm.eq(def_zone, BODY_ZONE_R_ARM))
            gear = equip_by_category[MECHA_R_ARM] as obj_item_mecha_parts_mecha_equipment;
        if (gear == null)
            return;
        float component_health = Dm.num(gear.get_integrity());
        // always leave at least 1 health
        float damage_to_deal = Math.Min(component_health - 1, Dm.num(damage));
        if (damage_to_deal <= 0)
            return;

        await gear.take_damage(damage_to_deal);
        if (Dm.num(gear.get_integrity()) <= 1)
        {
            to_chat(occupants, span_danger(I18n.T("[] is critically damaged!", gear)));
            playsound(this, gear.destroy_sound, 50);
        }
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:39
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= "";
        sound_effect ??= TRUE;
        armour_penetration ??= 0;
        object damage_taken = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.num(damage_taken) <= 0 || Dm.num(atom_integrity) < 0)
            return damage_taken;

        diag_hud_set_mechhealth();
        if (spark_system != null)
            Waiting.Detach(spark_system.start());
        try_deal_internal_damage(damage_taken);
        if (Dm.num(damage_taken) >= 5 || prob(33))
            to_chat(occupants, span_userdanger(I18n.T("Taking damage!")));
        log_message(Dm.text("Took [] points of damage. Damage type: []", damage_taken, damage_type), LOG_MECHA);

        return damage_taken;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:53
    public override async DmTask<object> run_atom_armor(object damage_amount = null, object damage_type = null, object damage_flag = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_flag ??= 0;
        object dot = await base.run_atom_armor(damage_amount, damage_type, damage_flag, attack_dir, armour_penetration);
        if (Dm.truthy(attack_dir))
        {
            float facing_modifier = Dm.num(get_armour_facing(Math.Abs(Dm.num(dir2angle(dir)) - Dm.num(dir2angle(attack_dir)))));
            if (Dm.truthy(dot))
                dot = Dm.num(dot) * facing_modifier;
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:60
    public override async DmTask<object> attack_hand(mob user__mob = null, DmList modifiers = null, params object[] __extra)
    {
        mob_living user = user__mob as mob_living;
        object dot = await base.attack_hand(user__mob, modifiers, __extra);
        if (Dm.truthy(dot) || !Dm.truthy(user.combat_mode))
            return dot;
        user.changeNext_move(CLICK_CD_MELEE); // Ugh. Ideally we shouldn't be setting cooldowns outside of click code.
        await user.do_attack_animation(this, ATTACK_EFFECT_PUNCH);
        playsound(loc, Dm.resource("sound/items/weapons/tap.ogg"), 40, TRUE, -1);
        user.visible_message(span_danger(I18n.T("[] hits []. Nothing happens.", user, this)), null, null, COMBAT_MESSAGE_RANGE);
        log_message(Dm.text("Attack by hand/paw (no damage). Attacker - [].", user), LOG_MECHA, color: "red");
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:70
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null) => await attack_hand(user, modifiers);

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:73
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        log_message(Dm.text("Attack by alien. Attacker - [].", user), LOG_MECHA, color: "red");
        playsound(loc, Dm.resource("sound/items/weapons/slash.ogg"), 100, TRUE);
        return await attack_generic(user, Dm.num(rand(user.melee_damage_lower, user.melee_damage_upper)), BRUTE, MELEE, 0);
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:96
    public override object hulk_damage() => 15;

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:99
    public override async DmTask<object> attack_hulk(mob_living_carbon_human user = null, params object[] __extra)
    {
        object dot = await base.attack_hulk(user, __extra);
        if (Dm.truthy(dot))
        {
            log_message(Dm.text("Attack by hulk. Attacker - [].", user), LOG_MECHA, color: "red");
            log_combat(user, this, "punched", "hulk powers");
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:78
    public override async DmTask<object> attack_animal(mob user__mob = null, DmList modifiers = null)
    {
        mob_living_simple_animal user = user__mob as mob_living_simple_animal;
        log_message(Dm.text("Attack by simple animal. Attacker - [].", user), LOG_MECHA, color: "red");
        if (!Dm.truthy(user.melee_damage_upper) && !Dm.truthy(user.obj_damage))
        {
            await user.emote("custom", message: Dm.text("[] [].", user.friendly_verb_continuous, this));
            return FALSE;
        }

        float play_soundeffect = 1;
        if (Dm.truthy(user.environment_smash))
        {
            play_soundeffect = 0;
            playsound(this, Dm.resource("sound/effects/bang.ogg"), 50, TRUE);
        }

        float animal_damage = Dm.num(rand(user.melee_damage_lower, user.melee_damage_upper));
        if (Dm.truthy(user.obj_damage))
            animal_damage = Dm.num(user.obj_damage);
        animal_damage = Math.Min(animal_damage, 20 * Dm.num(user.environment_smash));
        log_combat(user, this, "attacked");
        return await attack_generic(user, animal_damage, user.melee_damage_type as string, MELEE, play_soundeffect);
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:112
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null) //wrapper
    {
        log_message(Dm.text("Hit by [].", hitting_atom), LOG_MECHA, color: "red");
        return await base.hitby(hitting_atom, skipcatch, hitpush, blocked, throwingdatum);
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:144
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        log_message(Dm.text("Affected by explosion of severity: [].", severity), LOG_MECHA, color: "red");
        return base.ex_act(severity, target, __extra);
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:105
    public override async DmTask<object> blob_act(obj_structure_blob B = null)
    {
        log_message(Dm.text("Attack by blob. Attacker - [].", B), LOG_MECHA, color: "red");
        await take_damage(30, BRUTE, MELEE, 0, get_dir(this, B));
        return null;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:109
    public override DmTask<object> attack_tk(mob user = null) => DmTask<object>.FromResult(null);

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:116
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        // Determine our potential to shoot through the mech and into the cockpit, hitting the pilot
        float kill_the_meat = Dm.num(hitting_projectile.armour_penetration);
        if (Dm.truthy(hitting_projectile.armor_flag))
            kill_the_meat -= Dm.num(get_armor_rating(hitting_projectile.armor_flag));
        kill_the_meat = Math.Clamp(kill_the_meat, 0, 100);
        // Allows bullets to hit the pilot of open-canopy mechs, or if the bullet penetrates to the pilot, or the bullet can pass through structures
        if (!Dm.truthy(LAZYLEN(occupants)) || Dm.truthy(Dm.bitand(mecha_flags, SILICON_PILOT)))
            return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
        if (!Dm.eq(def_zone, BODY_ZONE_HEAD) && !Dm.eq(def_zone, BODY_ZONE_CHEST))
            return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
        if (Dm.truthy(Dm.bitand(mecha_flags, IS_ENCLOSED)) && !(kill_the_meat != 0 && prob(kill_the_meat) && !Dm.truthy(Dm.bitand(mecha_flags, CANNOT_OVERPENETRATE))) && !Dm.truthy(Dm.bitand(hitting_projectile.pass_flags, Dm.bitor(PASSSTRUCTURE, PASSVEHICLE))))
            return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
        mob_living hitmob = pick(occupants) as mob_living;
        return await hitmob.projectile_hit(hitting_projectile, def_zone, piercing_hit); //If we've passed any of the above conditions, the pilot can be hit
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:132
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        object dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        log_message(Dm.text("Hit by projectile. Type: []([]).", hitting_projectile, hitting_projectile.damage_type), LOG_MECHA, color: "red");
        // yes we *have* to run the armor calc proc here I love tg projectile code too
        await try_damage_component(await run_atom_armor(
            hitting_projectile.damage,
            hitting_projectile.damage_type,
            hitting_projectile.armor_flag,
            REVERSE_DIR(hitting_projectile.dir),
            Dm.num(hitting_projectile.armour_penetration)), def_zone);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:148
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        severity = Dm.num(severity) - 1;

        if (Dm.eq(severity, EXPLODE_DEVASTATE))
        {
            if (flat_equipment != null)
                Dm.append(ref SSexplosions.high_mov_atom, flat_equipment);
            if (trackers != null)
                Dm.append(ref SSexplosions.high_mov_atom, trackers);
            if (occupants != null)
                Dm.append(ref SSexplosions.high_mov_atom, occupants);
        }
        else if (Dm.eq(severity, EXPLODE_HEAVY))
        {
            if (flat_equipment != null)
                Dm.append(ref SSexplosions.med_mov_atom, flat_equipment);
            if (trackers != null)
                Dm.append(ref SSexplosions.med_mov_atom, trackers);
            if (occupants != null)
                Dm.append(ref SSexplosions.med_mov_atom, occupants);
        }
        else if (Dm.eq(severity, EXPLODE_LIGHT))
        {
            if (flat_equipment != null)
                Dm.append(ref SSexplosions.low_mov_atom, flat_equipment);
            if (trackers != null)
                Dm.append(ref SSexplosions.low_mov_atom, trackers);
            if (occupants != null)
                Dm.append(ref SSexplosions.low_mov_atom, occupants);
        }
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:174
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;

        float mecha_explodies_vulnerability = Dm.num(severity) * Dm.num(capacitor.rating); //The more severe the EMP, the worse the outcome. The higher the tier of the capacitor, the less severe the outcome.

        if (Dm.truthy(get_charge()))
            use_energy(round(Dm.num(cell.maxcharge) / 2 / mecha_explodies_vulnerability, 1));

        float how_hard_are_we_explodies = Dm.num(rand(MECH_EMP_DAMAGE_LOWER, MECH_EMP_DAMAGE_UPPER));
        await take_damage(how_hard_are_we_explodies / mecha_explodies_vulnerability, BURN);

        log_message("EMP detected", LOG_MECHA, color: "red");

        //Mess with the focus of the inbuilt camera if present
        if (chassis_camera != null && !Dm.truthy(chassis_camera.is_emp_scrambled))
        {
            chassis_camera.setViewRange(chassis_camera.short_range);
            chassis_camera.is_emp_scrambled = TRUE;
            diag_hud_set_camera();
            _addtimer(Dm.@new<datum_callback>(chassis_camera, nameof(obj_machinery_camera_exosuit.emp_refocus), this), 10 * SECONDS / Dm.num(severity), file: "code/modules/vehicles/mecha/mecha_defense.dm", line: 194);
        }

        foreach (obj_item_mecha_parts_mecha_tracking tracker in Dm.each<obj_item_mecha_parts_mecha_tracking>(trackers?.Copy())) // Go through our list of trackers and potentially delete our trackers due to an EMP.
        {
            if (prob(MECH_EMP_BEACON_DESTRUCTION_PROB / Dm.num(severity)))
            {
                if (Dm.truthy(Dm.bitand(mecha_flags, SILICON_PILOT)) && Dm.truthy(tracker.ai_beacon)) // ignore any beacons which allows our AI pilot to be in the mech. Even if it isn't using a beacon, let's just do this to be safe. The code doesn't make a distinction! YAY!
                    continue; // Does this mean that a AI tracking beacon can be EMP'd over and over without risk of self-destruction? Yes. Is this a nerf to silicon mechs? I guess. Do I care? No.
                qdel(tracker);
            }
        }

        if (!Dm.truthy(equipment_disabled) && Dm.truthy(LAZYLEN(occupants))) //prevent spamming this message with back-to-back EMPs
            to_chat(occupants, span_warning(I18n.T("Error -- Connection to equipment control unit has been lost.")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(restore_equipment)), 3 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/vehicles/mecha/mecha_defense.dm", line: 204);
        equipment_disabled = TRUE;
        set_mouse_pointer();
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:208
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null) => Dm.num(exposed_temperature) > max_temperature ? TRUE : FALSE;

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:211
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        log_message("Exposed to dangerous temperature.", LOG_MECHA, color: "red");
        await take_damage(5, BURN, 0, 1);
        return null;
    }

    //Check if we should ignite the pilot of an open-canopy mech
    // tg: code/modules/vehicles/mecha/mecha_defense.dm:215
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        object dot = await base.fire_act(exposed_temperature, exposed_volume);
        if (Dm.truthy(Dm.bitand(mecha_flags, IS_ENCLOSED)) || Dm.truthy(Dm.bitand(mecha_flags, SILICON_PILOT)))
            return dot;
        foreach (mob_living cookedalive in Dm.each<mob_living>(occupants))
        {
            if (Dm.num(cookedalive.fire_stacks) < 5)
            {
                await cookedalive.adjust_fire_stacks(1);
                cookedalive.ignite_mob();
            }
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:224
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (istype(tool, typeof(obj_item_mecha_parts)))
        {
            obj_item_mecha_parts parts = tool as obj_item_mecha_parts;
            return await parts.try_attach_part(user, this, TRUE);
        }
        return await base.item_interaction_secondary(user, tool, modifiers);
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:230
    public override async DmTask<object> attackby(obj_item weapon = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attackby(weapon, user, modifiers, attack_modifiers);
        //If our weapon that we are hitting the mech with has armour penetration, we could potentially get a hit in on the occupant
        float peeling_the_onion = Math.Clamp(Dm.num(weapon.armour_penetration) - Dm.num(get_armor_rating(MELEE)) / 2, 0, 100);

        if (peeling_the_onion != 0 && prob(peeling_the_onion) && !Dm.truthy(Dm.bitand(mecha_flags, CANNOT_OVERPENETRATE))
            && Dm.truthy(LAZYLEN(occupants))
            && !Dm.truthy(Dm.bitand(mecha_flags, SILICON_PILOT)))
        {
            mob_living hitmob = pick(occupants) as mob_living;
            await weapon.melee_attack_chain(user, hitmob, modifiers, Dm.list(Dm.kv(Dm.text("[]", FORCE_MULTIPLIER), peeling_the_onion / 100), Dm.kv(Dm.text("[]", SILENCE_DEFAULT_MESSAGES), TRUE))); //Perform an extra attack on the occupant if all the above conditions pass
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:241
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_brain_processor)))
        {
            Dm.NotTranslated("code/modules/vehicles/mecha/mecha_defense.dm:242", "item_interaction: an MMI pilot (mmi_move_inside; the brain processors)");
            return ITEM_INTERACT_BLOCKING;
        }

        if (istype(tool, typeof(obj_item_mecha_ammo)))
        {
            if (Dm.truthy(await ammo_resupply(tool as obj_item_mecha_ammo, user)))
                return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_rcd_upgrade)))
            return await upgrade_rcd(tool as obj_item_rcd_upgrade, user);

        if (Dm.truthy(tool.GetID()))
        {
            if (!Dm.truthy(await allowed__proc(user)))
            {
                if (Dm.truthy(Dm.bitand(mecha_flags, ID_LOCK_ON)))
                    balloon_alert(user, I18n.T("access denied!"));
                else
                    balloon_alert(user, I18n.T("unable to set id lock!"));
                return ITEM_INTERACT_BLOCKING;
            }
            mecha_flags = Dm.bitxor(mecha_flags, ID_LOCK_ON);
            balloon_alert(user, I18n.T("[] id lock!", Dm.truthy(Dm.bitand(mecha_flags, ID_LOCK_ON)) ? "enabled" : "disabled"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_mecha_parts)))
        {
            obj_item_mecha_parts part = tool as obj_item_mecha_parts;
            return await part.try_attach_part(user, this, FALSE);
        }

        if (Dm.truthy(is_wire_tool(tool)) && Dm.truthy(Dm.bitand(mecha_flags, PANEL_OPEN)))
        {
            if (Dm.truthy(user.combat_mode))
                return null;
            if (Dm.truthy(await wires.interact(user)))
                return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stock_parts)))
            return await try_insert_part(tool as obj_item_stock_parts, user);

        return NONE;
    }

    /// Try to insert a stock part into the mech
    // tg: code/modules/vehicles/mecha/mecha_defense.dm:284
    public virtual async DmTask<object> try_insert_part(obj_item_stock_parts tool = null, mob_living user = null)
    {
        if (!Dm.truthy(Dm.bitand(mecha_flags, PANEL_OPEN)))
        {
            balloon_alert(user, I18n.T("open the panel first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (istype(tool, typeof(obj_item_stock_parts_power_store_cell)))
        {
            if (cell != null)
            {
                balloon_alert(user, I18n.T("already installed!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this, silent: FALSE)))
                return ITEM_INTERACT_BLOCKING;

            cell = tool as obj_item_stock_parts_power_store;
            balloon_alert(user, I18n.T("installed power cell"));
            diag_hud_set_mechcell();
            playsound(this, Dm.resource("sound/items/tools/screwdriver2.ogg"), 50, FALSE);
            log_message("Power cell installed", LOG_MECHA);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stock_parts_scanning_module)))
        {
            if (scanmod != null)
            {
                balloon_alert(user, I18n.T("already installed!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this, silent: FALSE)))
                return ITEM_INTERACT_BLOCKING;

            scanmod = tool as obj_item_stock_parts_scanning_module;
            balloon_alert(user, I18n.T("installed scanning module"));
            playsound(this, Dm.resource("sound/items/tools/screwdriver2.ogg"), 50, FALSE);
            log_message(Dm.text("[] installed", tool), LOG_MECHA);
            update_part_values();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stock_parts_capacitor)))
        {
            if (capacitor != null)
            {
                balloon_alert(user, I18n.T("already installed!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this, silent: FALSE)))
                return ITEM_INTERACT_BLOCKING;

            capacitor = tool as obj_item_stock_parts_capacitor;
            balloon_alert(user, I18n.T("installed capacitor"));
            playsound(this, Dm.resource("sound/items/tools/screwdriver2.ogg"), 50, FALSE);
            log_message(Dm.text("[] installed", tool), LOG_MECHA);
            update_part_values();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stock_parts_servo)))
        {
            if (servo != null)
            {
                balloon_alert(user, I18n.T("already installed!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this, silent: FALSE)))
                return ITEM_INTERACT_BLOCKING;

            servo = tool as obj_item_stock_parts_servo;
            balloon_alert(user, I18n.T("installed servo"));
            playsound(this, Dm.resource("sound/items/tools/screwdriver2.ogg"), 50, FALSE);
            log_message(Dm.text("[] installed", tool), LOG_MECHA);
            update_part_values();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:351
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        float final_force = Dm.num(CALCULATE_FORCE(attacking_item, attack_modifiers)) * Dm.num(attacking_item.get_demolition_modifier(this));
        if (final_force == 0)
            return 0;

        object damage_taken = await take_damage(final_force, attacking_item.damtype as string, MELEE, 1, get_dir(this, user));
        await try_damage_component(damage_taken, user.zone_selected);

        string hit_verb = Dm.truthy(length(attacking_item.attack_verb_simple)) ? Dm.str(pick(attacking_item.attack_verb_simple)) : I18n.T("hit");
        string mark = Dm.truthy(damage_taken) ? "." : I18n.T(", without leaving a mark!");
        user.visible_message(
            span_danger(I18n.T("[] [][] [] with [][]", user, hit_verb, plural_s(hit_verb), this, attacking_item, mark)),
            span_danger(I18n.T("You [] [] with [][]", hit_verb, this, attacking_item, mark)),
            span_hear(I18n.T("You hear a [].", hit_verb)),
            COMBAT_MESSAGE_RANGE);

        log_combat(user, this, "attacked", attacking_item);
        log_message(Dm.text("Attacked by []. Item - [], Damage - []", user, attacking_item, damage_taken), LOG_MECHA);
        return damage_taken;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:371
    public override async DmTask<object> attack_generic(mob user = null, float? damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, float? armor_penetration = null)
    {
        object dot = await base.attack_generic(user, damage_amount, damage_type, damage_flag, sound_effect, armor_penetration);
        if (Dm.truthy(dot))
        {
            await try_damage_component(dot, user.zone_selected);
            diag_hud_set_mechhealth();
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:384
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await base.screwdriver_act(user, tool);

        if (Dm.truthy(LAZYLEN(occupants)))
        {
            balloon_alert(user, I18n.T("panel blocked"));
            return TRUE;
        }

        mecha_flags = Dm.bitxor(mecha_flags, PANEL_OPEN);
        balloon_alert(user, Dm.truthy(Dm.bitand(mecha_flags, PANEL_OPEN)) ? I18n.T("panel open") : I18n.T("panel closed"));
        tool.play_tool_sound(this);
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:396
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        await base.crowbar_act(user, tool);
        if (istype(tool, typeof(obj_item_crowbar_mechremoval)))
        {
            obj_item_crowbar_mechremoval remover = tool as obj_item_crowbar_mechremoval;
            await remover.empty_mech(this, user);
            return TRUE;
        }
        if (!Dm.truthy(Dm.bitand(mecha_flags, PANEL_OPEN)))
        {
            balloon_alert(user, I18n.T("open the panel first!"));
            return TRUE;
        }
        if (Dm.truthy(dna_lock) && Dm.truthy(user.has_dna()))
        {
            mob_living_carbon user_carbon = user as mob_living_carbon;
            if (!Dm.eq(user_carbon.dna.unique_enzymes, dna_lock))
            {
                balloon_alert(user, I18n.T("access with this DNA denied!"));
                return TRUE;
            }
        }
        if (Dm.truthy(Dm.bitand(mecha_flags, ID_LOCK_ON)) && !Dm.truthy(await allowed__proc(user)))
        {
            balloon_alert(user, I18n.T("access denied!"));
            return TRUE;
        }

        DmList stock_parts = Dm.list();
        if (cell != null)
            stock_parts.Add(cell);
        if (scanmod != null)
            stock_parts.Add(scanmod);
        if (capacitor != null)
            stock_parts.Add(capacitor);
        if (servo != null)
            stock_parts.Add(servo);

        if (!Dm.truthy(length(stock_parts)))
        {
            balloon_alert(user, I18n.T("no parts!"));
            return TRUE;
        }

        obj_item_stock_parts part_to_remove = await tgui_input_list(user, I18n.T("Which part to remove?"), I18n.T("Part Removal"), stock_parts) as obj_item_stock_parts;
        if (part_to_remove == null || !Dm.@in(part_to_remove, contents))
            return TRUE;

        await user.put_in_hands(part_to_remove);
        locate_parts();
        diag_hud_set_mechcell();
        tool.play_tool_sound(this);
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:438
    public override async DmTask<object> welder_act(mob_living user = null, obj_item W = null)
    {
        if (Dm.truthy(user.combat_mode))
            return null;
        if (Dm.truthy(DOING_INTERACTION(user, this)))
        {
            balloon_alert(user, I18n.T("you're already repairing this!"));
            return TRUE;
        }
        if (Dm.num(atom_integrity) >= Dm.num(max_integrity))
        {
            balloon_alert(user, I18n.T("it's not damaged!"));
            return TRUE;
        }
        if (!Dm.truthy(W.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return TRUE;
        user.balloon_alert_to_viewers(I18n.T("started welding []", this), I18n.T("started repairing []", this));
        audible_message(span_hear(I18n.T("You hear welding.")));
        bool did_the_thing = false;
        while (Dm.num(atom_integrity) < Dm.num(max_integrity))
        {
            if (Dm.truthy(await W.use_tool(this, user, 2.5f * SECONDS, volume: 50)))
            {
                did_the_thing = true;
                await repair_damage(10);
                audible_message(span_hear(I18n.T("You hear welding.")));
            }
            else
                break;
        }

        if (did_the_thing)
            user.balloon_alert_to_viewers(I18n.T("[] repaired []", Dm.num(atom_integrity) >= Dm.num(max_integrity) ? "fully" : "partially", this));
        else
            user.balloon_alert_to_viewers(I18n.T("stopped welding []", this), I18n.T("interrupted the repair!"));
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:466
    public virtual async DmTask full_repair(object charge_cell = null)
    {
        await repair_damage(max_integrity);
        if (cell != null && Dm.truthy(charge_cell))
        {
            cell.charge = cell.maxcharge;
            diag_hud_set_mechcell();
        }
        if (Dm.truthy(Dm.bitand(internal_damage, MECHA_INT_FIRE)))
            clear_internal_damage(MECHA_INT_FIRE);
        if (Dm.truthy(Dm.bitand(internal_damage, MECHA_INT_TEMP_CONTROL)))
            clear_internal_damage(MECHA_INT_TEMP_CONTROL);
        if (Dm.truthy(Dm.bitand(internal_damage, MECHA_INT_SHORT_CIRCUIT)))
            clear_internal_damage(MECHA_INT_SHORT_CIRCUIT);
        if (Dm.truthy(Dm.bitand(internal_damage, MECHA_CABIN_AIR_BREACH)))
            clear_internal_damage(MECHA_CABIN_AIR_BREACH);
        if (Dm.truthy(Dm.bitand(internal_damage, MECHA_INT_CONTROL_LOST)))
            clear_internal_damage(MECHA_INT_CONTROL_LOST);
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:482
    public override async DmTask<object> repair_damage(object amount = null)
    {
        object dot = await base.repair_damage(amount);
        diag_hud_set_mechhealth();
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:486
    public override async DmTask<object> narsie_act(params object[] __extra) => await emp_act(EMP_HEAVY);

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:489
    public override async DmTask do_attack_animation(atom A = null, object visual_effect_icon = null, obj_item used_item = null, object no_effect = null, float? fov_effect = null, object item_animation_override = null)
    {
        if (!Dm.truthy(no_effect) && !Dm.truthy(visual_effect_icon))
        {
            visual_effect_icon = ATTACK_EFFECT_SMASH;
            if (Dm.eq(damtype, BURN))
                visual_effect_icon = ATTACK_EFFECT_MECHFIRE;
            else if (Dm.eq(damtype, TOX))
                visual_effect_icon = ATTACK_EFFECT_MECHTOXIN;
        }
        await base.do_attack_animation(A, visual_effect_icon, used_item, no_effect, fov_effect, item_animation_override);
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:498
    public virtual async DmTask<object> ammo_resupply(obj_item_mecha_ammo ammo = null, mob user = null, object fail_chat_override = null)
    {
        fail_chat_override ??= FALSE;
        if (!Dm.truthy(ammo.rounds))
        {
            if (!Dm.truthy(fail_chat_override))
                balloon_alert(user, I18n.T("the box is empty!"));
            return FALSE;
        }

        float ammo_needed;
        bool found_gun = false;

        foreach (obj_item_mecha_parts_mecha_equipment_weapon_ballistic gun in Dm.each<obj_item_mecha_parts_mecha_equipment_weapon_ballistic>(flat_equipment))
        {
            ammo_needed = 0;

            if (!Dm.eq(gun.ammo_type, ammo.ammo_type))
                continue;
            found_gun = true;
            if (Dm.truthy(ammo.direct_load))
                ammo_needed = Dm.num(Dm.initial(gun, nameof(gun.projectiles))) - Dm.num(gun.projectiles);
            else
                ammo_needed = Dm.num(gun.projectiles_cache_max) - Dm.num(gun.projectiles_cache);

            if (ammo_needed == 0)
                continue;

            if (ammo_needed < Dm.num(ammo.rounds))
            {
                if (Dm.truthy(ammo.direct_load))
                    gun.projectiles = Dm.num(gun.projectiles) + ammo_needed;
                else
                    gun.projectiles_cache = Dm.num(gun.projectiles_cache) + ammo_needed;
                playsound(get_turf(user) as atom, ammo.load_audio, 50, TRUE);
                to_chat(user, span_notice(I18n.T("You add [] [][] to \\the []", ammo_needed, ammo.ammo_type, ammo_needed > 1 ? "s" : "", gun)));
                ammo.rounds = Dm.num(ammo.rounds) - ammo_needed;
                if (ammo.custom_materials != null) //Change material content of the ammo box according to the amount of ammo deposited into the weapon
                {
                    /// list of materials contained in the ammo box after we put it through the equation so we can stick this list into set_custom_materials()
                    DmList new_material_content = Dm.list();
                    float initial_rounds = Dm.num(Dm.initial(ammo, nameof(ammo.rounds)));
                    foreach (datum_material current_material in Dm.each<datum_material>(ammo.custom_materials))
                    {
                        if (istype(current_material, typeof(datum_material_iron))) //we can flatten an empty ammo box into a sheet of iron (2000 units) so we have to make sure the box always has this amount at minimum
                            new_material_content[current_material] = (Dm.num(ammo.custom_materials[current_material]) - SHEET_MATERIAL_AMOUNT) * (Dm.num(ammo.rounds) / initial_rounds) + SHEET_MATERIAL_AMOUNT;
                        else
                            new_material_content[current_material] = Dm.num(ammo.custom_materials[current_material]) * (Dm.num(ammo.rounds) / initial_rounds);
                    }
                    await ammo.set_custom_materials(new_material_content);
                }
                ammo.update_name();
                return TRUE;
            }

            if (Dm.truthy(ammo.direct_load))
                gun.projectiles = Dm.num(gun.projectiles) + Dm.num(ammo.rounds);
            else
                gun.projectiles_cache = Dm.num(gun.projectiles_cache) + Dm.num(ammo.rounds);

            playsound(get_turf(user) as atom, ammo.load_audio, 50, TRUE);
            to_chat(user, span_notice(I18n.T("You add [] [][] to \\the []", ammo.rounds, ammo.ammo_type, Dm.num(ammo.rounds) > 1 ? "s" : "", gun)));
            if (Dm.truthy(ammo.qdel_on_empty))
            {
                qdel(ammo);
                return TRUE;
            }
            ammo.rounds = 0;
            await ammo.set_custom_materials(Dm.list(Dm.kv(typeof(datum_material_iron), SHEET_MATERIAL_AMOUNT)));
            ammo.update_appearance();
            return TRUE;
        }

        if (!Dm.truthy(fail_chat_override))
        {
            if (found_gun)
                balloon_alert(user, I18n.T("ammo storage is full!"));
            else
                balloon_alert(user, I18n.T("can't use this ammo!"));
        }
        return FALSE;
    }

    ///Upgrades any attached RCD equipment.
    // tg: code/modules/vehicles/mecha/mecha_defense.dm:564
    public virtual async DmTask<object> upgrade_rcd(obj_item_rcd_upgrade rcd_upgrade = null, mob user = null)
    {
        foreach (obj_item_mecha_parts_mecha_equipment_rcd rcd_equip in Dm.each<obj_item_mecha_parts_mecha_equipment_rcd>(flat_equipment))
        {
            if (Dm.truthy(await rcd_equip.internal_rcd.install_upgrade(rcd_upgrade, user)))
                return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/vehicles/mecha/mecha_defense.dm:571
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        if (spark_system != null)
            await spark_system.start();
        await loc.assume_air(cabin_air);

        mob_living_silicon_ai unlucky_ai = null;
        foreach (mob_living occupant in Dm.each<mob_living>(occupants.Copy()))
        {
            if (!isAI(occupant))
            {
                await mob_exit(occupant, forced: TRUE);
                if (!isbrain(occupant)) // who would win.. 1 brain vs 1 sleep proc..
                    await occupant.SetSleeping(destruction_sleep_duration);
                continue;
            }

            Dm.NotTranslated("code/modules/vehicles/mecha/mecha_defense.dm:583", "atom_destruction: an AI pilot (mecha_ai_interaction.dm)");
        }

        if (ore_box != null)
            Waiting.Detach(ore_box.dump_box_contents());

        if (wreckage != null)
        {
            obj_structure_mecha_wreckage wreck = Dm.@new(wreckage, loc, unlucky_ai) as obj_structure_mecha_wreckage;
            foreach (obj_item_mecha_parts_mecha_equipment equipment in Dm.each<obj_item_mecha_parts_mecha_equipment>(flat_equipment?.Copy()))
            {
                if (Dm.truthy(equipment.detachable) && prob(30))
                {
                    wreck.crowbar_salvage.Add(equipment);
                    await equipment.detach(wreck); //detaches from src into wreck
                    equipment.active = TRUE;
                }
                else
                {
                    await equipment.detach(loc);
                    qdel(equipment);
                }
            }

            if (cell != null)
            {
                wreck.crowbar_salvage.Add(cell);
                await cell.forceMove(wreck);
                await cell.use(rand(0, cell.charge), TRUE);
                cell = null;
            }
        }
        await base.atom_destruction(damage_flag);
    }
}
