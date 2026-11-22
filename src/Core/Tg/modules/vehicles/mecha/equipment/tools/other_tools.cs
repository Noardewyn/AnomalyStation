// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/equipment/tools/other_tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Teleporter, Wormhole generator, Gravitational catapult, Armor booster modules,
// Repair droid, Tesla Energy relay, Generators

////////////////////////////////////////////// TELEPORTER ///////////////////////////////////////////////

// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:7
public partial class obj_item_mecha_parts_mecha_equipment_teleporter : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:17
    public override async DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null)
    {
        if (!Dm.truthy(action_checks(target)) || !Dm.truthy(check_teleport_valid(source, target, TELEPORT_CHANNEL_BLUESPACE)))
            return null;
        turf T = get_turf(target);
        if (T != null && Dm.eq(loc.z, T.z) && Dm.num(get_dist(loc, T)) <= teleport_range)
        {
            await do_teleport(chassis, T, 4, channel: TELEPORT_CHANNEL_BLUESPACE);
            return await base.action(source, target, modifiers);
        }
        return null;
    }
}

////////////////////////////////////////////// WORMHOLE GENERATOR //////////////////////////////////////////

// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:29
public partial class obj_item_mecha_parts_mecha_equipment_wormhole_generator : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:39
    public override async DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null)
    {
        if (!Dm.truthy(action_checks(target)) || !Dm.truthy(check_teleport_valid(source, target, TELEPORT_CHANNEL_WORMHOLE)))
            return null;
        area targetarea = pick(get_areas_in_range(100, chassis)) as area;
        if (targetarea == null)//Literally middle of nowhere how did you even get here
            return null;
        DmList validturfs = Dm.list();
        turf ourturf = get_turf(this);
        foreach (turf evaluated_turf in Dm.each<turf>(get_area_turfs(targetarea.type)))
        {
            if (Dm.truthy(evaluated_turf.density) || !Dm.eq(chassis.z, evaluated_turf.z))
                continue;
            foreach (obj evaluated_obj in Dm.each<obj>(evaluated_turf))
            {
                if (!Dm.truthy(evaluated_obj.density))
                    continue;
                validturfs.Add(evaluated_turf);
            }
        }

        turf target_turf = pick(validturfs) as turf;
        if (target_turf == null)
            return null;
        DmList created = create_portal_pair(ourturf, target_turf, 300, 1, typeof(obj_effect_portal_anom)) as DmList;
        message_admins(Dm.text("[] used a Wormhole Generator in []", ADMIN_LOOKUPFLW(source), ADMIN_VERBOSEJMP(ourturf)));
        source.log_message(Dm.text("used a Wormhole Generator in [].", AREACOORD(ourturf)), LOG_GAME);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.______qdel_list_wrapper), created), Dm.num(rand(150, 300)), file: "code/modules/vehicles/mecha/equipment/tools/other_tools.dm", line: 62); // QDEL_LIST_IN
        return await base.action(source, target, modifiers);
    }
}

/////////////////////////////////////// GRAVITATIONAL CATAPULT ///////////////////////////////////////////

// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:71
public partial class obj_item_mecha_parts_mecha_equipment_gravcatapult : obj_item_mecha_parts_mecha_equipment
{
    private const float GRAVSLING_MODE = 1;
    private const float GRAVPUSH_MODE = 2;

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:85
    public override async DmTask<object> action(mob source = null, atom target__atom = null, DmList modifiers = null)
    {
        atom_movable target = target__atom as atom_movable;
        if (!Dm.truthy(action_checks(target__atom)))
            return null;
        if (mode == GRAVSLING_MODE)
        {
            if (movable_target == null)
            {
                if (target == null || Dm.truthy(target.anchored) || Dm.num(target.move_resist) >= MOVE_FORCE_EXTREMELY_STRONG)
                {
                    to_chat(source, span_warning(I18n.T("Unable to lock on []!", target__atom)));
                    return null;
                }
                if (ismob(target))
                {
                    mob M = target as mob;
                    if (Dm.truthy(M.mob_negates_gravity()))
                    {
                        to_chat(source, span_warning(I18n.T("[] immune to gravitational impulses, unable to lock!", target)));
                        return null;
                    }
                }
                movable_target = target;
                to_chat(source, span_notice(I18n.T("locked on [].", target)));
            }
            else if (target__atom != movable_target)
            {
                if (Dm.@in(movable_target, view(chassis)))
                {
                    turf targ = get_turf(target__atom);
                    turf orig = get_turf(movable_target);
                    await movable_target.throw_at(target__atom, 14, 1.5f);
                    movable_target = null;
                    source.log_message(Dm.text("used a Gravitational Catapult to throw [] (From []) at [] ([]).", movable_target, AREACOORD(orig), target__atom, AREACOORD(targ)), LOG_GAME);
                    return await base.action(source, target__atom, modifiers);
                }
                movable_target = null;
                to_chat(source, span_notice(I18n.T("Lock on [] disengaged.", movable_target)));
            }
        }
        else if (mode == GRAVPUSH_MODE)
        {
            object atomstothrow;
            if (isturf(target__atom))
                atomstothrow = range(3, target__atom);
            else
                atomstothrow = orange(3, target__atom);
            foreach (atom_movable scatter in Dm.each<atom_movable>(atomstothrow))
            {
                if (Dm.truthy(scatter.anchored) || Dm.num(scatter.move_resist) >= MOVE_FORCE_EXTREMELY_STRONG)
                    continue;
                if (ismob(scatter))
                {
                    mob scatter_mob = scatter as mob;
                    if (Dm.truthy(scatter_mob.mob_negates_gravity()))
                        continue;
                }
                do_scatter(scatter, target__atom);
            }
            turf targetturf = get_turf(target__atom);
            source.log_message(Dm.text("used a Gravitational Catapult repulse wave on [].", AREACOORD(targetturf)), LOG_GAME);
            return await base.action(source, target__atom, modifiers);
        }
        return null;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:130
    public virtual void do_scatter(atom_movable scatter = null, atom target = null)
    {
        float dist = 5 - Dm.num(get_dist(scatter, target));
        float delay = 2;
        GLOB.move_manager.move_away(scatter, target, delay: delay, timeout: delay * dist, flags: MOVEMENT_LOOP_START_INSTANT, priority: MOVEMENT_ABOVE_SPACE_PRIORITY);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:135
    public override DmList get_snowflake_data() => Dm.list(
        Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_MODE),
        Dm.kv("mode", mode == GRAVPUSH_MODE ? "Push" : "Sling"),
        Dm.kv("mode_label", "Gravity Catapult"));

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:142
    public override DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.eq(action, "change_mode"))
        {
            mode++;
            if (mode > GRAVPUSH_MODE)
                mode = GRAVSLING_MODE;
            return DmTask<object>.FromResult(TRUE);
        }
        return DmTask<object>.FromResult(null);
    }
}

//////////////////////////// ARMOR BOOSTER MODULES //////////////////////////////////////////////////////////
// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:153
public partial class obj_item_mecha_parts_mecha_equipment_armor : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:158
    public override async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        await base.attach(new_mecha, attach_right);
        if (armor_mod != null)
            chassis.set_armor((chassis.get_armor() as datum_armor).add_other_armor(armor_mod));
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:163
    public override async DmTask detach(atom moveto = null)
    {
        if (armor_mod != null)
            chassis.set_armor((chassis.get_armor() as datum_armor).subtract_other_armor(armor_mod));
        await base.detach(moveto);
    }
}

// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:189
public partial class obj_item_mecha_parts_mecha_equipment_armor_antiemp_armor_booster : obj_item_mecha_parts_mecha_equipment_armor
{
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:202
    public override async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        await base.attach(new_mecha, attach_right);
        await chassis.AddElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_WIRES, EMP_NO_EXAMINE));
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:206
    public override async DmTask detach(atom moveto = null)
    {
        chassis.RemoveElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_WIRES, EMP_NO_EXAMINE));
        await base.detach(moveto);
    }
}

////////////////////////////////// REPAIR DROID //////////////////////////////////////////////////

// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:221
public partial class obj_item_mecha_parts_mecha_equipment_repair_droid : obj_item_mecha_parts_mecha_equipment
{
    private Anomaly.Core.DmShim.icon droid_icon(string state) => Dm.@new<Anomaly.Core.DmShim.icon>(icon, Dm.named("icon_state", state));

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:237
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        chassis?.cut_overlay(droid_overlay);
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:242
    public override async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        await base.attach(new_mecha, attach_right);
        droid_overlay = droid_icon("repair_droid");
        new_mecha.add_overlay(droid_overlay);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:247
    public override async DmTask detach(atom moveto = null)
    {
        chassis.cut_overlay(droid_overlay);
        STOP_PROCESSING(SSobj, this);
        await base.detach(moveto);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:252
    public override DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (!Dm.eq(action, "toggle"))
            return DmTask<object>.FromResult(null);
        chassis.cut_overlay(droid_overlay);
        if (Dm.truthy(active))
        {
            START_PROCESSING(SSobj, this);
            droid_overlay = droid_icon("repair_droid_a");
            log_message("Activated.", LOG_MECHA);
        }
        else
        {
            STOP_PROCESSING(SSobj, this);
            droid_overlay = droid_icon("repair_droid");
            log_message("Deactivated.", LOG_MECHA);
        }
        chassis.add_overlay(droid_overlay);
        return DmTask<object>.FromResult(null);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:267
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (chassis == null)
            return PROCESS_KILL;
        float dt = Dm.num(seconds_per_tick);
        float h_boost = health_boost * dt;
        bool repaired = false;
        if (Dm.truthy(Dm.bitand(chassis.internal_damage, MECHA_INT_SHORT_CIRCUIT)))
            h_boost *= -2;
        else if (Dm.truthy(chassis.internal_damage) && SPT_PROB(8, dt))
        {
            foreach (object int_dam_flag in Dm.each<object>(repairable_damage))
            {
                if (!Dm.truthy(Dm.bitand(chassis.internal_damage, int_dam_flag)))
                    continue;
                chassis.clear_internal_damage(int_dam_flag);
                repaired = true;
                break;
            }
        }
        if (h_boost < 0 || Dm.num(chassis.get_integrity()) < Dm.num(chassis.max_integrity))
        {
            Waiting.Detach(chassis.repair_damage(h_boost));
            repaired = true;
        }
        if (repaired)
        {
            if (!Dm.truthy(chassis.use_energy(energy_drain)))
            {
                active = FALSE;
                return PROCESS_KILL;
            }
        }
        else //no repair needed, we turn off
        {
            chassis.cut_overlay(droid_overlay);
            droid_overlay = droid_icon("repair_droid");
            chassis.add_overlay(droid_overlay);
            active = FALSE;
            return PROCESS_KILL;
        }
        return null;
    }
}

/////////////////////////////////////////// GENERATOR /////////////////////////////////////////////

// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:299
public partial class obj_item_mecha_parts_mecha_equipment_generator : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:321
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        generator_init();
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:325
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:329
    public override async DmTask detach(atom moveto = null)
    {
        STOP_PROCESSING(SSobj, this);
        active = FALSE;
        await base.detach(moveto);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:334
    public override DmList get_snowflake_data() => Dm.list(
        Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_GENERATOR),
        Dm.kv("fuel", fuel?.amount));

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:340
    public override DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.eq(action, "toggle"))
        {
            if (Dm.truthy(active))
            {
                to_chat(chassis.occupants, span_warning(I18n.T("Power generation enabled.")));
                START_PROCESSING(SSobj, this);
                log_message("Activated.", LOG_MECHA);
            }
            else
            {
                to_chat(chassis.occupants, span_warning(I18n.T("Power generation disabled.")));
                STOP_PROCESSING(SSobj, this);
                log_message("Deactivated.", LOG_MECHA);
            }
            return DmTask<object>.FromResult(TRUE);
        }
        return DmTask<object>.FromResult(null);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:352
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, fuel_type))
            return NONE;
        await load_fuel(tool as obj_item_stack_sheet, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:358
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (chassis == null)
        {
            active = FALSE;
            return PROCESS_KILL;
        }
        if (!Dm.truthy(fuel?.amount))
        {
            active = FALSE;
            log_message("Deactivated - no fuel.", LOG_MECHA);
            to_chat(chassis.occupants, span_notice(I18n.T("Fuel reserves depleted.")));
            return PROCESS_KILL;
        }
        object current_charge = chassis.get_charge();
        if (current_charge == null)
        {
            active = FALSE;
            to_chat(chassis.occupants, span_notice(I18n.T("No power cell detected.")));
            log_message("Deactivated.", LOG_MECHA);
            return PROCESS_KILL;
        }
        float dt = Dm.num(seconds_per_tick);
        //how much fuel are we using per tick
        float fuel_usage_rate = fuelrate_idle;
        if (Dm.num(current_charge) < Dm.num(chassis.cell.maxcharge))
        {
            fuel_usage_rate = fuelrate_active;
            chassis.give_power(rechargerate * dt);
        }
        fuel.amount = Dm.num(fuel.amount) - Math.Min(dt * fuel_usage_rate / SHEET_MATERIAL_AMOUNT, Dm.num(fuel.amount));
        return null;
    }

    ///Try to insert more fuel into the generator
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:381
    public virtual async DmTask load_fuel(obj_item_stack_sheet inserted_fuel = null, mob user = null)
    {
        if (Dm.eq(inserted_fuel.amount, 0)) //if we somehow have a sheet of 0 fuel
        {
            to_chat(user, span_warning(I18n.T("[] traces in target minimal! [] cannot be used as fuel.", fuel, inserted_fuel)));
            return;
        }
        //how much fuel is needed to fill the generator to its max capacity, in units
        float units_to_load = fuel == null ? max_fuel : Math.Max(max_fuel - Dm.num(fuel.amount) * SHEET_MATERIAL_AMOUNT, 0);
        if (units_to_load == 0)
        {
            to_chat(user, span_notice(I18n.T("Unit is full.")));
            return;
        }
        //how much new fuel are we inserting, in sheets
        float fuel_to_load = Math.Min(Math.Max(Dm.num(round(units_to_load / SHEET_MATERIAL_AMOUNT)), 1), Dm.num(inserted_fuel.amount));
        if (fuel != null)
            fuel.amount = Dm.num(fuel.amount) + fuel_to_load;
        else
            fuel = Dm.@new(inserted_fuel.type, this, fuel_to_load) as obj_item_stack_sheet;
        await inserted_fuel.use(fuel_to_load);
        to_chat(user, span_notice(I18n.T("[] unit[] of [] successfully loaded.", fuel_to_load, fuel_to_load == 1 ? "" : "s", fuel)));
    }

    ///Introduces the actual fuel type to be used, as well as the starting amount of said fuel
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:400
    public virtual void generator_init()
    {
        fuel = Dm.@new<obj_item_stack_sheet_mineral_plasma>(this, 10);
    }
}

/// Version without the initial fuel
// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:404
public partial class obj_item_mecha_parts_mecha_equipment_generator_printed : obj_item_mecha_parts_mecha_equipment_generator
{
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:406
    public override void generator_init()
    {
    }
}

/////////////////////////////////////////// THRUSTERS /////////////////////////////////////////////

// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:411
public partial class obj_item_mecha_parts_mecha_equipment_thrusters : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:421
    public override async DmTask<object> try_attach_part(mob user = null, obj_vehicle_sealed_mecha mecha = null, object attach_right = null)
    {
        foreach (obj_item part in Dm.each<obj_item>(mecha.equip_by_category[MECHA_UTILITY]))
        {
            if (istype(part, type))
            {
                to_chat(user, span_warning(I18n.T("[] already has this thruster package!", mecha)));
                return ITEM_INTERACT_BLOCKING;
            }
        }
        return await base.try_attach_part(user, mecha, attach_right);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:428
    public override async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        new_mecha.active_thrusters = this; //Enable by default
        await base.attach(new_mecha, attach_right);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:432
    public override async DmTask detach(atom moveto = null)
    {
        if (chassis.active_thrusters == this)
            chassis.active_thrusters = null;
        await base.detach(moveto);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:437
    public override async DmTask set_active(object active__1 = null)
    {
        await base.set_active(active__1);
        if (Dm.truthy(active__1))
        {
            START_PROCESSING(SSobj, this);
            enable();
            log_message("Activated.", LOG_MECHA);
        }
        else
        {
            STOP_PROCESSING(SSobj, this);
            disable();
            log_message("Deactivated.", LOG_MECHA);
        }
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:448
    public virtual void enable()
    {
        if (chassis.active_thrusters == this)
            return;
        chassis.active_thrusters = this;
        to_chat(chassis.occupants, span_notice(I18n.T("[] enabled.", this)));
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:454
    public virtual void disable()
    {
        if (chassis.active_thrusters != this)
            return;
        chassis.active_thrusters = null;
        to_chat(chassis.occupants, span_notice(I18n.T("[] disabled.", this)));
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:460
    public virtual async DmTask<object> thrust(object movement_dir = null)
    {
        if (chassis == null)
            return FALSE;
        await generate_effect(movement_dir);
        return TRUE; //This parent should never exist in-game outside admeme use, so why not let it be a creative thruster?
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:466
    public virtual async DmTask generate_effect(object movement_dir = null)
    {
        obj_effect_particle_effect E = Dm.@new(effect_type, get_turf(chassis)) as obj_effect_particle_effect;
        float reverse = Dm.num(REVERSE_DIR(Dm.num(movement_dir)));
        E.dir = reverse;
        await step(E, reverse);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), E), 5, file: "code/modules/vehicles/mecha/equipment/tools/other_tools.dm", line: 470); // QDEL_IN
    }
}

// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:472
public partial class obj_item_mecha_parts_mecha_equipment_thrusters_gas : obj_item_mecha_parts_mecha_equipment_thrusters
{
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:478
    public override async DmTask<object> thrust(object movement_dir = null)
    {
        if (chassis == null)
            return FALSE;
        obj_machinery_portable_atmospherics_canister internal_tank = chassis.get_internal_tank();
        if (internal_tank == null)
            return FALSE;
        datum_gas_mixture our_mix = await internal_tank.return_air() as datum_gas_mixture;
        float moles = Dm.num(our_mix.total_moles());
        if (moles < move_cost)
        {
            our_mix.remove(moles);
            return FALSE;
        }
        our_mix.remove(move_cost);
        await generate_effect(movement_dir);
        return TRUE;
    }
}

//for mechs with built-in thrusters, should never really exist un-attached to a mech
// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:493
public partial class obj_item_mecha_parts_mecha_equipment_thrusters_ion : obj_item_mecha_parts_mecha_equipment_thrusters
{
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:499
    public override async DmTask<object> thrust(object movement_dir = null)
    {
        if (chassis == null)
            return FALSE;
        if (Dm.truthy(chassis.use_energy(chassis.step_energy_drain)))
        {
            await generate_effect(movement_dir);
            return TRUE;
        }
        return FALSE;
    }
}

///////////////////////////////////// CONCEALED WEAPON BAY ////////////////////////////////////////

// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:509
public partial class obj_item_mecha_parts_mecha_equipment_concealed_weapon_bay : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:514
    public override async DmTask<object> try_attach_part(mob user = null, obj_vehicle_sealed_mecha M = null, object attach_right = null)
    {
        if (Dm.truthy(Dm.bitand(M.mech_type, EXOSUIT_MODULE_COMBAT)))
        {
            to_chat(user, span_warning(I18n.T("[] does not have the correct bolt configuration!", M)));
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.try_attach_part(user, M, attach_right);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:520
    public override async DmTask<object> special_attaching_interaction(object attach_right = null, obj_vehicle_sealed_mecha mech = null, mob user = null, object checkonly = null)
    {
        if (Dm.truthy(checkonly))
            return TRUE;
        obj_item_mecha_parts_mecha_equipment existing_equip;
        if (Dm.truthy(attach_right))
            existing_equip = mech.equip_by_category[MECHA_R_ARM] as obj_item_mecha_parts_mecha_equipment;
        else
            existing_equip = mech.equip_by_category[MECHA_L_ARM] as obj_item_mecha_parts_mecha_equipment;
        if (existing_equip != null)
        {
            name = existing_equip.name;
            icon = existing_equip.icon;
            icon_state = existing_equip.icon_state;
            qdel(existing_equip);
            user.visible_message(span_notice(I18n.T("[] hollows out [] and puts something in.", user, this)), span_notice(I18n.T("You attach the concealed weapon bay to [] within the shell of [].", mech, this)));
        }
        else
            user.visible_message(span_notice(I18n.T("[] attaches [] to [].", user, this, mech)), span_notice(I18n.T("You attach [] to [].", this, mech)));
        await attach(mech, attach_right);
        mech.mech_type = Dm.bitor(mech.mech_type, EXOSUIT_MODULE_CONCEALED_WEP_BAY);
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:540
    public override async DmTask detach(atom moveto = null)
    {
        obj_vehicle_sealed_mecha mech = chassis;
        await base.detach(moveto);
        name = Dm.initial(this, "name") as string;
        icon = Dm.initial(this, "icon");
        icon_state = Dm.initial(this, "icon_state") as string;
        if (Dm.locate<obj_item_mecha_parts_mecha_equipment_concealed_weapon_bay>(mech.contents) == null) //if no others exist
            mech.mech_type = Dm.bitand(mech.mech_type, Dm.bitnot(EXOSUIT_MODULE_CONCEALED_WEP_BAY));
    }
}

// tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:549
public partial class obj_item_mecha_parts_camera_kit : obj_item_mecha_parts
{
    // tg: code/modules/vehicles/mecha/equipment/tools/other_tools.dm:557
    public override async DmTask<object> try_attach_part(mob user = null, obj_vehicle_sealed_mecha mech = null, object attach_right = null)
    {
        if (mech.chassis_camera != null)
        {
            balloon_alert(user, I18n.T("already has a camera!"));
            return ITEM_INTERACT_BLOCKING;
        }

        object dot = await base.try_attach_part(user, mech, attach_right);

        mech.chassis_camera = Dm.@new<obj_machinery_camera_exosuit>(mech);
        mech.chassis_camera.update_c_tag(mech);
        mech.diag_hud_set_camera();
        return dot;
    }
}
