// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice B: the energy gun base, whole. The weapon description element is attached (D291). The var block is generated (D111): `cell` holds the cell object (types tool, ObjectHoldingVars).

// tg: code/modules/projectiles/guns/energy.dm:1
public partial class obj_item_gun_energy : obj_item_gun
{
    // tg: code/modules/projectiles/guns/energy.dm:58
    public override void fire_sounds()
    {
        // What frequency the energy gun's sound will make
        float pitch_to_use = 1;

        obj_item_ammo_casing_energy shot = ammo_type[select] as obj_item_ammo_casing_energy;
        // What percentage of the full battery a shot will expend
        float shot_cost_percent = round(clamp(shot.e_cost / cell.maxcharge, 0, 1) * 100);
        // Ignore this on oversized/infinite cells or ammo without cost
        if (shot_cost_percent > 0 && shot_cost_percent < 100)
        {
            // The total amount of shots the fully charged energy gun can fire before running out
            float max_shots = round(100 / shot_cost_percent) - 1;
            // How many shots left before the energy gun's current battery runs out of energy
            float shots_left = round((round(clamp(cell.charge / cell.maxcharge, 0, 1) * 100)) / shot_cost_percent) - 1;
            pitch_to_use = LERP(1, 0.3f, Dm.num(Dm.pow((1 - (shots_left / max_shots)), 2)));
        }

        sound playing_sound = sound(Dm.truthy(suppressed) ? suppressed_sound : fire_sound);
        playing_sound.pitch = pitch_to_use;

        if (Dm.truthy(suppressed))
            playsound(this, playing_sound, suppressed_volume, vary_fire_sound, ignore_walls: FALSE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_distance: 0);
        else
            playsound(this, playing_sound, fire_sound_volume, vary_fire_sound);
    }

    // tg: code/modules/projectiles/guns/energy.dm:81
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (!Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
        {
            if (cell != null) // a gun whose cell is gone (Exited, below), which tg leaves to a runtime
                await cell.use(round(cell.charge / emp_resistance / Dm.num(severity)));
            chambered = null; //we empty the chamber
            await recharge_newshot(); //and try to charge a new shot
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy.dm:89
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        if (istype(@interface, typeof(obj_item_inducer)))
        {
            to_chat(user, span_alert(I18n.T("Error: unable to interface with [].", @interface)));
            return null;
        }
        return cell;
    }

    // tg: code/modules/projectiles/guns/energy.dm:95
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (cell_type != null)
            cell = Dm.@new(cell_type, this) as obj_item_stock_parts_power_store;
        else
            cell = Dm.@new<obj_item_stock_parts_power_store>(this);
        if (!Dm.truthy(dead_cell))
            Waiting.NoSleep(cell.give(cell.maxcharge));
        if (cell != null && Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            cell.resistance_flags = Dm.bitor(cell.resistance_flags, INDESTRUCTIBLE);
        cell.resistance_flags = Dm.bitor(cell.resistance_flags, BOMB_PROOF);
        cell.custom_materials = null; //do not give printed energy guns more mats than they already have.
        update_ammo_types();
        Waiting.NoSleep(recharge_newshot(TRUE));
        if (Dm.truthy(selfcharge))
            START_PROCESSING(SSobj, this);
        update_appearance();
        RegisterSignal(this, COMSIG_ITEM_RECHARGED, nameof(instant_recharge));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy.dm:115
    public override async DmTask add_weapon_description()
    {
        await AddElement(typeof(datum_element_weapon_description), Dm.named("attached_proc", nameof(add_notes_energy)));
    }

    /**
     *
     * Outputs type-specific weapon stats for energy-based firearms based on its firing modes
     * and the stats of those firing modes. Esoteric firing modes like ion are currently not supported
     * but can be added easily
     *
     */
    // tg: code/modules/projectiles/guns/energy.dm:125
    public virtual object add_notes_energy()
    {
        DmList readout = Dm.list();
        // Make sure there is something to actually retrieve
        if (ammo_type.Count == 0)
            return null;
        Type exam_proj;
        Dm.append(ref readout, Dm.text("\n[]", I18n.T("Standard models of this projectile weapon have [].", span_warning(Dm.text("[] mode\\s", ammo_type.Count)))));
        Dm.append(ref readout, "Our heroic interns have shown that one can theoretically stay standing after...");
        if (projectile_damage_multiplier <= 0)
        {
            Dm.append(ref readout, I18n.T("a theoretically infinite number of shots on [] mode due to esoteric or nonexistent offensive potential.", span_warning("every")));
            return readout.Join("\n"); // Sending over the singular string, rather than the whole list
        }
        foreach (obj_item_ammo_casing_energy for_ammo in Dm.each_cast<obj_item_ammo_casing_energy>(ammo_type))
        {
            exam_proj = for_ammo.projectile_type;
            if (!ispath(exam_proj))
                continue;
            if (Dm.num(Dm.initial(exam_proj, (obj_projectile p) => p.damage)) > 0) // Don't divide by 0!!!!!
            {
                Dm.append(ref readout, I18n.T("[] on [] mode before collapsing from [].", span_warning(Dm.text("[] shot\\s", HITS_TO_CRIT((Dm.num(Dm.initial(exam_proj, (obj_projectile p) => p.damage)) * projectile_damage_multiplier) * Dm.num(for_ammo.pellets)))), span_warning(for_ammo.select_name), Dm.eq(Dm.initial(exam_proj, (obj_projectile p) => p.damage_type), STAMINA) ? "immense pain" : "their wounds"));
                if (Dm.num(Dm.initial(exam_proj, (obj_projectile p) => p.stamina)) > 0) // In case a projectile does damage AND stamina damage (Energy Crossbow)
                    Dm.append(ref readout, I18n.T("[] on [] mode before collapsing from immense pain.", span_warning(Dm.text("[] shot\\s", HITS_TO_CRIT((Dm.num(Dm.initial(exam_proj, (obj_projectile p) => p.stamina)) * projectile_damage_multiplier) * Dm.num(for_ammo.pellets)))), span_warning(for_ammo.select_name)));
            }
            else
            {
                Dm.append(ref readout, I18n.T("a theoretically infinite number of shots on [] mode.", span_warning(for_ammo.select_name)));
            }
        }

        return readout.Join("\n"); // Sending over the singular string, rather than the whole list
    }

    // tg: code/modules/projectiles/guns/energy.dm:149
    public virtual void update_ammo_types()
    {
        obj_item_ammo_casing_energy shot;
        for (float i = 1; i <= ammo_type.Count; i++)
        {
            Type shottype = ammo_type[i] as Type;
            shot = Dm.@new(shottype, this) as obj_item_ammo_casing_energy;
            ammo_type[i] = shot;
        }
        shot = ammo_type[select] as obj_item_ammo_casing_energy;
        fire_sound = shot.fire_sound;
        fire_delay = shot.delay;
    }

    // tg: code/modules/projectiles/guns/energy.dm:159
    public override object Destroy(float? force = null)
    {
        if (cell != null)
            QDEL_NULL(ref cell);
        STOP_PROCESSING(SSobj, this);

        // Intentional cast.
        // Sometimes ammo_type has paths, sometimes it has atom.
        foreach (atom item in Dm.each<atom>(ammo_type))
            qdel(item);
        ammo_type = null;

        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/guns/energy.dm:172
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == cell)
        {
            cell = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy.dm:178
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(selfcharge) && cell != null && Dm.num(cell.percent()) < 100)
        {
            charge_timer += Dm.num(seconds_per_tick);
            if (charge_timer < charge_delay)
                return null;
            charge_timer = 0;
            Waiting.NoSleep(cell.give(self_charge_amount * Dm.num(seconds_per_tick)));
            if (chambered == null) //if empty chamber we try to charge a new shot
                Waiting.NoSleep(recharge_newshot(TRUE));
            update_appearance();
        }
        return null;
    }

    // tg: code/modules/projectiles/guns/energy.dm:189
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        // DEVIATION(DV27): an inert energy gun's ammo_type still holds the casing types its Initialize did not make (D112)
        if (InertSeam.MissingInitialize(this, "attack_self", user))
            return TRUE;

        if (ammo_type.Count > 1 && Dm.truthy(can_select))
            await select_fire(user as mob_living);
        return null;
    }

    // tg: code/modules/projectiles/guns/energy.dm:197
    public override object can_shoot()
    {
        obj_item_ammo_casing_energy shot = ammo_type[select] as obj_item_ammo_casing_energy;
        return !QDELETED(cell) ? (cell.charge >= shot.e_cost ? TRUE : FALSE) : FALSE;
    }

    // tg: code/modules/projectiles/guns/energy.dm:201
    [DmParams("no_cyborg_drain")]
    public override async DmTask recharge_newshot(params object[] __extra)
    {
        object no_cyborg_drain = Dm.arg(__extra, 0);
        if (ammo_type == null || cell == null)
            return;

        if (Dm.truthy(use_cyborg_cell) && !Dm.truthy(no_cyborg_drain))
        {
            if (iscyborg(loc))
            {
                mob_living_silicon_robot robot = (mob_living_silicon_robot)loc;
                if (robot.cell__obj != null)
                {
                    obj_item_ammo_casing_energy shot = ammo_type[select] as obj_item_ammo_casing_energy; //Necessary to find cost of shot
                    if (Dm.truthy(await robot.cell__obj.use(shot.e_cost))) //Take power from the borg...
                        await cell.give(shot.e_cost); //... to recharge the shot
                }
            }
        }

        if (chambered == null)
        {
            obj_item_ammo_casing_energy AC = ammo_type[select] as obj_item_ammo_casing_energy;
            if (cell.charge >= AC.e_cost) //if there's enough power in the cell cell...
            {
                chambered = AC; //...prepare a new shot based on the current ammo type selected
                if (chambered.loaded_projectile == null)
                {
                    chambered.newshot();
                    await base.recharge_newshot(Dm.forward(__extra, no_cyborg_drain));
                    return;
                }
            }
        }
    }

    // tg: code/modules/projectiles/guns/energy.dm:221
    public override async DmTask handle_chamber(float? empty_chamber = null, float? from_firing = null, float? chamber_next_round = null)
    {
        if (chambered != null && chambered.loaded_projectile == null) //if loaded_projectile is null, i.e the shot has been fired...
        {
            obj_item_ammo_casing_energy shot = chambered as obj_item_ammo_casing_energy;
            await cell.use(shot.e_cost);//... drain the cell cell
        }
        chambered = null; //either way, released the prepared shot
        await recharge_newshot(); //try to charge a new shot
    }

    // tg: code/modules/projectiles/guns/energy.dm:228
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        message ??= TRUE;
        zone_override ??= "";
        bonus_spread ??= 0;
        if (chambered == null && Dm.truthy(can_shoot()))
            await process_chamber(); // If the gun was drained and then recharged, load a new shot.
        return await base.process_fire(target, user, message, @params, zone_override, bonus_spread);
    }

    // tg: code/modules/projectiles/guns/energy.dm:233
    [DmParams("user", "target", "message", "params", "zone_override", "randomized_gun_spread", "randomized_bonus_spread", "rand_spr", "iteration")]
    public override async DmTask<object> process_burst(mob_living user = null, atom target = null, float? message = null, object @params = null, string zone_override = null, float? random_spread = null, float? burst_spread_mult = null, float? iteration = null, params object[] __extra)
    {
        message ??= TRUE;
        zone_override ??= "";
        // tg's override names nine parameters (randomized_gun_spread, randomized_bonus_spread, rand_spr, iteration) and its ..() passes them
        // on in order: the base reads the 6th as random_spread, the 7th as burst_spread_mult and the 8th as iteration; the 9th is dropped.
        random_spread ??= 0;
        burst_spread_mult ??= 0;
        iteration ??= 0;
        if (chambered == null && Dm.truthy(can_shoot()))
            await process_chamber(); // Ditto.
        return await base.process_burst(user, target, message, @params, zone_override, random_spread, burst_spread_mult, iteration, __extra);
    }

    // tg: code/modules/projectiles/guns/energy.dm:238
    public virtual async DmTask select_fire(mob_living user = null)
    {
        select++;
        if (select > ammo_type.Count)
            select = 1;
        obj_item_ammo_casing_energy shot = ammo_type[select] as obj_item_ammo_casing_energy;
        fire_sound = shot.fire_sound;
        fire_delay = shot.delay;
        if (Dm.truthy(shot.muzzle_flash_color))
            set_light_color(shot.muzzle_flash_color);
        if (Dm.truthy(shot.select_name) && user != null)
            balloon_alert(user, I18n.T("set to []", shot.select_name));
        chambered = null;
        await recharge_newshot(TRUE);
        update_appearance();
        if (Dm.truthy(fire_mode_switch_sound))
            playsound(this, fire_mode_switch_sound, 50, TRUE);
    }

    // tg: code/modules/projectiles/guns/energy.dm:255
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object skip_inhand = Dm.initial(this, (obj_item p) => p.inhand_icon_state); //only build if we aren't using a preset inhand icon
        object skip_worn_icon = Dm.initial(this, (obj_item p) => p.worn_icon_state); //only build if we aren't using a preset worn icon

        if (Dm.truthy(skip_inhand) && Dm.truthy(skip_worn_icon)) //if we don't have either, don't do the math.
            return await base.update_icon_state(__extra);

        object ratio = get_charge_ratio();
        string temp_icon_to_use = Dm.initial(this, (atom p) => p.icon_state);
        if (Dm.truthy(modifystate))
        {
            obj_item_ammo_casing_energy shot = ammo_type[select] as obj_item_ammo_casing_energy;
            temp_icon_to_use += Dm.text("[]", shot.select_name);
        }

        temp_icon_to_use += Dm.text("[]", ratio);
        if (!Dm.truthy(skip_inhand))
            inhand_icon_state = temp_icon_to_use;
        if (!Dm.truthy(skip_worn_icon))
            worn_icon_state = temp_icon_to_use;
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/projectiles/guns/energy.dm:276
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(automatic_charge_overlays))
            return dot;

        string overlay_icon_state = Dm.text("[]_charge", icon_state);
        object shot = ammo_type[select]; // a casing, or its typepath before update_ammo_types (an inert gun drawn in a case): initial() reads both (rules 3.4)

        if (Dm.truthy(modifystate))
        {
            if (Dm.truthy(single_shot_type_overlay))
                Dm.append(ref dot, Dm.text("[]_[]", icon_state, Dm.initial(shot, (obj_item_ammo_casing_energy p) => p.select_name)));
            overlay_icon_state += Dm.text("_[]", Dm.initial(shot, (obj_item_ammo_casing_energy p) => p.select_name));
        }

        float ratio = Dm.num(get_charge_ratio());
        if (ratio == 0 && Dm.truthy(display_empty))
        {
            Dm.append(ref dot, Dm.text("[]_empty", icon_state));
            return dot;
        }

        if (Dm.truthy(shot_type_fluff_overlay))
            Dm.append(ref dot, Dm.text("[]_[]_extra", icon_state, Dm.initial(shot, (obj_item_ammo_casing_energy p) => p.select_name)));

        if (Dm.truthy(shaded_charge))
        {
            Dm.append(ref dot, Dm.text("[]_charge[]", icon_state, ratio));
            return dot;
        }
        mutable_appearance charge_overlay = mutable_appearance(icon, overlay_icon_state) as mutable_appearance;
        for (float i = ratio; i >= 1; i--)
        {
            charge_overlay.pixel_w = ammo_x_offset * (i - 1);
            charge_overlay.pixel_z = ammo_y_offset * (i - 1);
            Dm.append(ref dot, Dm.@new<mutable_appearance>(charge_overlay));
        }
        return dot;
    }

    ///Used by update_icon_state() and update_overlays()
    // tg: code/modules/projectiles/guns/energy.dm:308
    public virtual object get_charge_ratio()
    {
        return Dm.truthy(can_shoot()) ? CEILING(clamp(cell.charge / cell.maxcharge, 0, 1) * charge_sections, 1) : 0;
        // Sets the ratio to 0 if the gun doesn't have enough charge to fire, or if its power cell is removed.
    }

    // tg: code/modules/projectiles/guns/energy.dm:312
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (user != null && Dm.truthy(can_shoot()) && Dm.truthy(await can_trigger_gun(user)) && Dm.truthy(user.get_bodypart(BODY_ZONE_HEAD)))
        {
            user.visible_message(span_suicide(I18n.T("[] is putting the barrel of [] in [] mouth. It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
            await sleep(2.5f * SECONDS);
            if (Dm.truthy(user.is_holding(this)))
            {
                user.visible_message(span_suicide(I18n.T("[] melts [] face off with []!", user, user.p_their(), this)));
                playsound(loc as atom, fire_sound, 50, TRUE, -1);
                obj_item_ammo_casing_energy shot = ammo_type[select] as obj_item_ammo_casing_energy;
                await cell.use(shot.e_cost);
                update_appearance();
                return FIRELOSS;
            }
            else
            {
                user.visible_message(span_suicide(I18n.T("[] panics and starts choking to death!", user)));
                return OXYLOSS;
            }
        }
        else
        {
            user.visible_message(span_suicide(I18n.T("[] is pretending to melt [] face off with []! It looks like [] trying to commit suicide!</b>", user, user.p_their(), this, user.p_theyre())));
            playsound(this, dry_fire_sound, 30, TRUE);
            return OXYLOSS;
        }
    }

    // tg: code/modules/projectiles/guns/energy.dm:341
    public override async DmTask<object> ignition_effect(atom A = null, mob user = null)
    {
        object dot = null;
        if (!Dm.truthy(can_shoot()) || ammo_type[select] == null)
        {
            await shoot_with_empty_chamber();
            dot = "";
        }
        else
        {
            obj_item_ammo_casing_energy E = ammo_type[select] as obj_item_ammo_casing_energy;
            obj_projectile_energy loaded_projectile = E.loaded_projectile as obj_projectile_energy;
            if (loaded_projectile == null)
            {
                dot = "";
            }
            else if (loaded_projectile.damage <= 0 || Dm.eq(loaded_projectile.damage_type, STAMINA))
            {
                user.visible_message(span_danger(I18n.T("[] tries to light [] with [], but it doesn't do anything. Dumbass.", user, A.loc == user ? Dm.text("[] []", user.p_their(), A.name) : A, this)));
                playsound(user, E.fire_sound, 50, TRUE);
                playsound(user, loaded_projectile.hitsound, 50, TRUE);
                await cell.use(E.e_cost);
                dot = "";
            }
            else if (!Dm.eq(loaded_projectile.damage_type, BURN))
            {
                user.visible_message(span_danger(I18n.T("[] tries to light [] with [], but only succeeds in utterly destroying it. Dumbass.", user, A.loc == user ? Dm.text("[] []", user.p_their(), A.name) : A, this)));
                playsound(user, E.fire_sound, 50, TRUE);
                playsound(user, loaded_projectile.hitsound, 50, TRUE);
                await cell.use(E.e_cost);
                qdel(A);
                dot = "";
            }
            else
            {
                playsound(user, E.fire_sound, 50, TRUE);
                playsound(user, loaded_projectile.hitsound, 50, TRUE);
                await cell.use(E.e_cost);
                dot = span_rose(I18n.T("[] casually lights [] with []. Damn.", user, A.loc == user ? Dm.text("[] []", user.p_their(), A.name) : A, this));
            }
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy.dm:369
    [SignalHandler]
    public virtual void instant_recharge()
    {
        if (cell == null)
            return;
        cell.charge = cell.maxcharge;
        Waiting.NoSleep(recharge_newshot(TRUE));
        update_appearance();
    }
}
