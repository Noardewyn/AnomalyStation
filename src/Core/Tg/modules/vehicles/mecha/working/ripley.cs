// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/working/ripley.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/working/ripley.dm:1
public partial class obj_vehicle_sealed_mecha_ripley : obj_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/working/ripley.dm:55
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        update_pressure();
        return dot;
    }

    // DEVIATION(DV358): the pressure's speed is also taken as the pilot boards, not only after the first Move: a capsule walk's
    // first crossing would change the speed under a step the client already predicted at the old one
    public override async DmTask<object> moved_inside(mob_living newoccupant = null)
    {
        object dot = await base.moved_inside(newoccupant);
        update_pressure();
        return dot;
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_armor_plate), 3, typeof(obj_item_stack_sheet_animalhide_goliath_hide), typeof(datum_armor_armor_plate_ripley_goliath)));
        return dot;
    }

    /**
     * Makes the mecha go faster and halves the mecha drill cooldown if in Lavaland pressure.
     *
     * Checks for Lavaland pressure, if that works out the mech's speed is equal to fast_pressure_step_in and the cooldown for the mecha drill is halved. If not it uses slow_pressure_step_in and drill cooldown is normal.
     */
    // tg: code/modules/vehicles/mecha/working/ripley.dm:450
    public virtual void update_pressure()
    {
        turf T = get_turf(loc);

        if (Dm.truthy(lavaland_equipment_pressure_check(T)))
        {
            movedelay = !Dm.truthy(overclock_mode) ? fast_pressure_step_in : fast_pressure_step_in / overclock_coeff;
            foreach (obj_item_mecha_parts_mecha_equipment_drill drill in Dm.each<obj_item_mecha_parts_mecha_equipment_drill>(flat_equipment))
                drill.equip_cooldown = Dm.num(Dm.initial(drill, nameof(drill.equip_cooldown))) * 0.5f;
        }
        else
        {
            movedelay = !Dm.truthy(overclock_mode) ? slow_pressure_step_in : slow_pressure_step_in / overclock_coeff;
            foreach (obj_item_mecha_parts_mecha_equipment_drill drill in Dm.each<obj_item_mecha_parts_mecha_equipment_drill>(flat_equipment))
                drill.equip_cooldown = Dm.num(Dm.initial(drill, nameof(drill.equip_cooldown)));
        }
    }
}

// tg: code/modules/vehicles/mecha/working/ripley.dm:102
public partial class obj_vehicle_sealed_mecha_ripley_paddy : obj_vehicle_sealed_mecha_ripley
{
    // tg: code/modules/vehicles/mecha/working/ripley.dm:134
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        weewooloop = Dm.@new<datum_looping_sound_siren>(this, FALSE, FALSE);
        weewooloop.volume = 100;
        return dot;
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:139
    public override async DmTask<object> mob_exit(mob M = null, object silent = null, object randomstep = null, object forced = null)
    {
        obj_item_mecha_parts_mecha_equipment_ejector_seccage cargo_holder = Dm.locate<obj_item_mecha_parts_mecha_equipment_ejector_seccage>(equip_by_category[MECHA_UTILITY]);
        foreach (mob contained in Dm.each<mob>(cargo_holder?.contents))
            await cargo_holder.cheese_it(contained as mob_living);
        if (Dm.truthy(overclock_mode))
            await toggle_overclock(FALSE);
        return await base.mob_exit(M, silent, randomstep, forced);
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:147
    public override async DmTask<object> toggle_overclock(object forced_state = null)
    {
        object dot = await base.toggle_overclock(forced_state);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(overclock_mode))
            weewooloop.start();
        else
            weewooloop.stop();
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:158
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(overclock_mode))
            return dot;
        Dm.append(ref dot, mutable_appearance(icon, "paddy_sirens", null, this, ABOVE_LIGHTING_PLANE));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:164
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref weewooloop);
        return base.Destroy(force);
    }
}

// tg: code/modules/vehicles/mecha/working/ripley.dm:168
public partial class datum_action_vehicle_sealed_mecha_mech_overclock_siren : datum_action_vehicle_sealed_mecha_mech_overclock
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:166
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        await build_all_button_icons();
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:172
    public override object get_button_icon_state()
    {
        obj_vehicle_sealed_mecha_ripley_paddy secmech = chassis as obj_vehicle_sealed_mecha_ripley_paddy;
        return Dm.text("mech_siren_[]", Dm.truthy(secmech.overclock_mode) ? "on" : "off");
    }
}

// tg: code/modules/vehicles/mecha/working/ripley.dm:219
public partial class obj_vehicle_sealed_mecha_ripley_mining : obj_vehicle_sealed_mecha_ripley
{
    // tg: code/modules/vehicles/mecha/working/ripley.dm:223
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.Detach(fit_out());
        return dot;
    }

    private async DmTask fit_out()
    {
        await take_damage(125);
        if (cell != null)
            cell.charge = Dm.num(FLOOR(Dm.num(cell.charge) * 0.25f, 1)); //Starts at very low charge
        if (prob(70)) //Maybe add a drill
        {
            if (prob(15)) //Possible diamond drill... Feeling lucky?
                await Dm.@new<obj_item_mecha_parts_mecha_equipment_drill_diamonddrill>().attach(this);
            else
                await Dm.@new<obj_item_mecha_parts_mecha_equipment_drill>().attach(this);
        }
        else //Add plasma cutter if no drill
            await Dm.@new<obj_item_mecha_parts_mecha_equipment_weapon_energy_plasma>().attach(this);

        //Attach hydraulic clamp
        await Dm.@new<obj_item_mecha_parts_mecha_equipment_hydraulic_clamp>().attach(this, TRUE);
        await Dm.@new<obj_item_mecha_parts_mecha_equipment_mining_scanner>().attach(this);
    }
}

// tg: code/modules/vehicles/mecha/working/ripley.dm:249
public partial class obj_vehicle_sealed_mecha_ripley_cargo : obj_vehicle_sealed_mecha_ripley
{
    // tg: code/modules/vehicles/mecha/working/ripley.dm:256
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        //Attach hydraulic clamp ONLY
        Waiting.Detach(Dm.@new<obj_item_mecha_parts_mecha_equipment_hydraulic_clamp>().attach(this));

        Waiting.Detach(take_damage(Dm.num(max_integrity) * 0.5f, sound_effect: FALSE)); //Low starting health
        if (GLOB.cargo_ripley == null && Dm.truthy(mapload))
            GLOB.cargo_ripley = this;
        ADD_TRAIT(this, TRAIT_MECHA_DIAGNOSTIC_CREATED, REF(this)); //It was built *long* before the shift started.
        return dot;
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:268
    public override object Destroy(float? force = null)
    {
        if (GLOB.cargo_ripley == this)
            GLOB.cargo_ripley = null;
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:274
    public override void populate_parts()
    {
        cell = Dm.@new<obj_item_stock_parts_power_store_cell_high>(this);
        //No scanmod for Big Bess
        capacitor = Dm.@new<obj_item_stock_parts_capacitor>(this);
        servo = Dm.@new<obj_item_stock_parts_servo>(this);
        update_part_values();
    }
}

// tg: code/modules/vehicles/mecha/working/ripley.dm:281
public partial class obj_item_mecha_parts_mecha_equipment_ejector : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/working/ripley.dm:292
    public override async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        await base.attach(new_mecha, attach_right);
        (chassis as obj_vehicle_sealed_mecha_ripley).cargo_hold = this;
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:297
    public override async DmTask detach(atom moveto = null)
    {
        (chassis as obj_vehicle_sealed_mecha_ripley).cargo_hold = null;
        await drop_contents(isturf(moveto) ? moveto : moveto?.drop_location() as atom);
        await base.detach(moveto);
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:303
    public override object Destroy(float? force = null)
    {
        // Failsafe so we don't delete players
        atom droploc = drop_location() as atom ?? get_turf(this);
        foreach (mob stored in Dm.each<mob>(get_all_contents()))
        {
            if (stored.client != null)
            {
                stack_trace(Dm.text("[] was in [] when it was deleted! We skipped deconstruct(), or something.", stored, this), "code/modules/vehicles/mecha/working/ripley.dm", 308);
                Waiting.Detach(stored.forceMove(droploc));
            }
        }
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:312
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        await drop_contents(drop_location() as atom);
        await base.atom_deconstruct(disassembled);
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:316
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        await drop_contents(drop_location() as atom, 10 * Dm.num(severity));
        await base.contents_explosion(severity, target);
    }

    /// Spit out everything in our storage
    // tg: code/modules/vehicles/mecha/working/ripley.dm:321
    public virtual async DmTask drop_contents(atom drop_loc = null, float drop_prob = 100)
    {
        foreach (atom_movable stored in Dm.each<atom_movable>(contents.Copy()))
        {
            if (prob(drop_prob))
            {
                await stored.forceMove(drop_loc ?? get_turf(this));
                await step_rand(stored);
            }
        }
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:327
    public override async DmTask<object> relay_container_resist_act(mob_living user = null, obj container = null)
    {
        to_chat(user, span_notice(I18n.T("You lean on the back of [] and start pushing so it falls out of [].", container, this)));
        if (Dm.truthy(await do_after(user, 30 * SECONDS, target: container)))
        {
            if (user == null || IS_UNCONSCIOUS_OR_CRIT(user) || user.loc != this || container.loc != this)
                return null;
            to_chat(user, span_notice(I18n.T("You successfully pushed [] out of []!", container, this)));
            await container.forceMove(drop_location() as atom);
        }
        else if (user.loc == this) //so we don't get the message if we resisted multiple times and succeeded.
            to_chat(user, span_warning(I18n.T("You fail to push [] out of []!", container, this)));
        return null;
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:338
    public override DmList get_snowflake_data()
    {
        DmList cargo = Dm.list();
        DmList data = Dm.list(
            Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_EJECTOR),
            Dm.kv("cargo_capacity", cargo_capacity),
            Dm.kv("cargo", cargo));
        foreach (atom entry in Dm.each<atom>(contents))
            cargo.Add(Dm.list(Dm.kv("name", entry.name), Dm.kv("ref", REF(entry))));
        return data;
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:351
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return TRUE;
        if (Dm.eq(action, "eject"))
        {
            obj crate = Dm.locate_ref(@params["cargoref"]) is obj found && Dm.@in(found, contents) ? found : null;
            if (crate == null)
                return FALSE;
            to_chat(chassis.occupants, span_notice(I18n.T("You unload [].", crate)));
            await crate.forceMove(drop_location() as atom);
            if (crate == chassis.ore_box)
                chassis.ore_box = null;
            playsound(chassis, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
            log_message(Dm.text("Unloaded []. Cargo compartment capacity: []", crate, cargo_capacity - contents.len), LOG_MECHA);
            return TRUE;
        }
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/working/ripley.dm:367
public partial class obj_item_mecha_parts_mecha_equipment_ejector_seccage : obj_item_mecha_parts_mecha_equipment_ejector
{
    // tg: code/modules/vehicles/mecha/working/ripley.dm:372
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_MOB_REMOVING_CUFFS, nameof(stop_cuff_removal));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:376
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(this, COMSIG_MOB_REMOVING_CUFFS);
        foreach (mob freebird in Dm.each<mob>(contents.Copy())) //Let's not qdel people iside the mech kthx
            Waiting.Detach(cheese_it(freebird as mob_living));
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:382
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        RegisterSignal(arrived, COMSIG_MOB_REMOVING_CUFFS, nameof(stop_cuff_removal));
        return await base.Entered(arrived, old_loc, old_locs);
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:386
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        UnregisterSignal(gone, COMSIG_MOB_REMOVING_CUFFS);
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:390
    public virtual object stop_cuff_removal(datum source = null, obj_item cuffs = null)
    {
        to_chat(source, span_warning(I18n.T("You don't have the room to remove []!", cuffs)));
        return COMSIG_MOB_BLOCK_CUFF_REMOVAL;
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:395
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.eq(action, "eject"))
        {
            mob passenger = Dm.locate_ref(@params["cargoref"]) is mob found && Dm.@in(found, contents) ? found : null;
            if (passenger == null)
                return FALSE;
            to_chat(chassis.occupants, span_notice(I18n.T("You unload [].", passenger)));
            await passenger.forceMove(drop_location() as atom);
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/_step", passenger, chassis.dir), 1, file: "code/modules/vehicles/mecha/working/ripley.dm", line: 401); //That's right, one tick. Just enough to cause the tile move animation.
            playsound(chassis, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
            log_message(Dm.text("Unloaded []. Cargo compartment capacity: []", passenger, cargo_capacity - contents.len), LOG_MECHA);
            return TRUE;
        }
        return await base.ui_act(action, @params, ui, state);
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:408
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        float breakout_time = 1 * MINUTES;

        if (Dm.num(user.mob_size) > MOB_SIZE_HUMAN)
            breakout_time = 6 * SECONDS;

        to_chat(user, span_notice(I18n.T("You begin attempting a breakout. (This will take around [] and [] needs to remain stationary.)", DisplayTimeText(breakout_time), chassis)));
        if (!Dm.truthy(await do_after(user, breakout_time, target: chassis)))
            return null;
        to_chat(user, span_notice(I18n.T("You break out of the [].", this)));
        playsound(chassis, Dm.resource("sound/items/tools/crowbar.ogg"), 100, TRUE);
        await cheese_it(user);
        foreach (mob freebird in Dm.each<mob>(contents.Copy()))
        {
            if (user != freebird)
            {
                to_chat(freebird, span_warning(I18n.T("[] has managed to open the hatch, and you fall out with him. You're free!", user)));
                await cheese_it(freebird as mob_living);
            }
        }
        return null;
    }

    // tg: code/modules/vehicles/mecha/working/ripley.dm:425
    public virtual async DmTask cheese_it(mob_living escapee = null)
    {
        float range__1 = Dm.num(rand(1, 3));
        float variance = Dm.num(rand(-45, 45));
        float angle = 180;
        turf current_turf = get_turf(this);
        switch (Dm.num(chassis?.dir))
        {
            case NORTH:
                angle = 270;
                break;
            case EAST:
                angle = 180;
                break;
            case SOUTH:
                angle = 90;
                break;
            case WEST:
                angle = 0;
                break;
        }
        float target_x = Dm.num(round(range__1 * Dm.num(cos(angle + variance)), 1)) + Dm.num(current_turf.x);
        float target_y = Dm.num(round(range__1 * Dm.num(sin(angle + variance)), 1)) + Dm.num(current_turf.y);
        await escapee.Knockdown(1); //Otherwise everyone hits eachother while being thrown
        await escapee.forceMove(drop_location() as atom);
        await escapee.throw_at(locate(target_x, target_y, current_turf.z) as atom, range__1, 1);
    }
}
