// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/equipment/tools/work_tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//Hydraulic clamp, Kill clamp, Extinguisher, RCD, Cable layer.

// tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:5
public partial class obj_item_mecha_parts_mecha_equipment_hydraulic_clamp : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:26
    public override async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        await base.attach(new_mecha, attach_right);
        workmech = chassis as obj_vehicle_sealed_mecha_ripley;
        ADD_TRAIT(chassis, TRAIT_OREBOX_FUNCTIONAL, TRAIT_MECH_EQUIPMENT(type));
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:31
    public override async DmTask detach(atom moveto = null)
    {
        REMOVE_TRAIT(chassis, TRAIT_OREBOX_FUNCTIONAL, TRAIT_MECH_EQUIPMENT(type));
        workmech = null;
        await base.detach(moveto);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:36
    public override async DmTask<object> use_tool(atom target = null, mob_living user = null, object delay = null, float? amount = null, float? volume = null, datum_callback extra_checks = null, float? timed_action_flags = null)
        => await do_after_mecha(target, user, delay);

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:39
    public override async DmTask<object> do_after_checks(atom target = null, object flags = null)
    {
        // Gotta be close to the target
        if (!Dm.truthy(await loc.Adjacent(target)))
            return FALSE;
        return await base.do_after_checks(target, flags);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:45
    public override async DmTask<object> action(mob source__mob = null, atom target = null, DmList modifiers = null)
    {
        mob_living source = source__mob as mob_living;
        if (!Dm.truthy(action_checks(target)))
            return null;
        if (workmech.cargo_hold == null)
            throw Dm.CRASH(Dm.text("Mech [] has a clamp device, but no internal storage. This should be impossible.", chassis), "code/modules/vehicles/mecha/equipment/tools/work_tools.dm", 49);

        if (ismecha(target))
        {
            obj_vehicle_sealed_mecha M = target as obj_vehicle_sealed_mecha;
            bool have_ammo = false;
            foreach (obj_item_mecha_ammo box in Dm.each<obj_item_mecha_ammo>(workmech.cargo_hold.contents))
            {
                if (istype(box, typeof(obj_item_mecha_ammo)) && Dm.truthy(box.rounds))
                {
                    have_ammo = true;
                    if (Dm.truthy(await M.ammo_resupply(box, source, TRUE)))
                        return await base.action(source, target, modifiers);
                }
            }
            if (have_ammo)
                to_chat(source, I18n.T("No further supplies can be provided to [].", M));
            else
                to_chat(source, I18n.T("No providable supplies found in cargo hold"));
            return null;
        }

        if (istype(target, typeof(obj_machinery_door_firedoor)) || istype(target, typeof(obj_machinery_door_airlock)))
        {
            obj_machinery_door target_door = target as obj_machinery_door;
            playsound(chassis, clampsound, 50, FALSE, -6);
            await target_door.try_to_crowbar(this, source, TRUE);
            return await base.action(source, target, modifiers);
        }

        if (isobj(target))
        {
            obj clamptarget = target as obj;
            if (Dm.truthy(clamptarget.anchored))
            {
                to_chat(source, span_warning(I18n.T("[] is firmly secured!", target)));
                return null;
            }
            if (Dm.num(workmech.cargo_hold.contents.Count) >= Dm.num(workmech.cargo_hold.cargo_capacity))
            {
                to_chat(source, span_warning(I18n.T("Not enough room in cargo compartment!")));
                return null;
            }
            playsound(chassis, clampsound, 50, FALSE, -6);
            chassis.visible_message(span_notice(I18n.T("[] lifts [] and starts to load it into cargo compartment.", chassis, target)));
            await clamptarget.set_anchored(TRUE);
            if (!Dm.truthy(await do_after_cooldown(target, source, flags: Dm.bitor(MECH_DO_AFTER_DIR_CHANGE_FLAG, MECH_DO_AFTER_ADJACENCY_FLAG))))
            {
                await clamptarget.set_anchored(FALSE);
                return null;
            }
            await clamptarget.set_anchored(FALSE);
            await clamptarget.forceMove(workmech.cargo_hold);
            if (chassis.ore_box == null && istype(clamptarget, typeof(obj_structure_ore_box)))
                chassis.ore_box = clamptarget as obj_structure_ore_box;
            to_chat(source, span_notice(I18n.T("[] successfully loaded.", target)));
            log_message(Dm.text("Loaded []. Cargo compartment capacity: []", clamptarget, workmech.cargo_hold.cargo_capacity - workmech.cargo_hold.contents.len), LOG_MECHA);
            return await base.action(source, target, modifiers);
        }

        if (!isliving(target))
            return await base.action(source, target, modifiers);

        mob_living victim = target as mob_living;
        if (Dm.eq(victim.stat, DEAD))
            return await base.action(source, target, modifiers);

        if (!Dm.truthy(source.combat_mode))
        {
            await step_away(victim, chassis);
            if (Dm.truthy(killer_clamp))
                target.visible_message(span_danger(I18n.T("[] tosses [] like a piece of paper!", chassis, target)),
                    span_userdanger(I18n.T("[] tosses you like a piece of paper!", chassis)));
            else
            {
                to_chat(source, span_notice(I18n.T("You push [] out of the way.", target)));
                chassis.visible_message(span_notice(I18n.T("[] pushes [] out of the way.", chassis, target)),
                    span_notice(I18n.T("[] pushes you aside.", chassis)));
            }
            return await base.action(source, target, modifiers);
        }

        if (Dm.truthy(await victim.check_block(chassis, clamp_damage, name as string, attack_type: OVERWHELMING_ATTACK)))
            return await base.action(source, target, modifiers);

        if (iscarbon(victim) && Dm.truthy(killer_clamp))//meme clamp here
        {
            mob_living_carbon carbon_victim = target as mob_living_carbon;
            bool torn_off = false;
            obj_item_bodypart affected = carbon_victim.get_bodypart(BODY_ZONE_L_ARM) as obj_item_bodypart;
            if (affected != null)
            {
                await affected.dismember(damtype);
                torn_off = true;
            }
            affected = carbon_victim.get_bodypart(BODY_ZONE_R_ARM) as obj_item_bodypart;
            if (affected != null)
            {
                await affected.dismember(damtype);
                torn_off = true;
            }
            if (torn_off)
            {
                playsound(this, get_dismember_sound(), 80, TRUE);
                carbon_victim.visible_message(span_danger(I18n.T("[] rips []'s arms off!", chassis, carbon_victim)),
                    span_userdanger(I18n.T("[] rips your arms off!", chassis)));
                log_combat(source, carbon_victim, "removed both arms with a real clamp,", Dm.text("[]", name), Dm.text("(COMBAT MODE: [] (DAMTYPE: [])", uppertext(source.combat_mode), uppertext(damtype)));
                return await base.action(source, target, modifiers);
            }
        }
        float armor_check = Math.Clamp(Dm.num(victim.run_armor_check(null, MELEE)) / 3, 0, 100); //our target only benefits from a third of their armor. Because it's a huge ass clamp
        victim.visible_message(span_danger(I18n.T("[] squeezes []!", chassis, victim)),
            span_userdanger(I18n.T("[] squeezes you!", chassis)),
            span_hear(I18n.T("You hear something crack.")));
        log_combat(source, victim, "attacked", Dm.text("[]", name), Dm.text("(Combat mode: []) (DAMTYPE: [])", Dm.truthy(source.combat_mode) ? "On" : "Off", uppertext(damtype)));
        float final_damage = isalien(victim) ? clamp_damage * 3 : clamp_damage;
        await chassis.do_attack_animation(victim);
        playsound(chassis, clampsound, 30, FALSE, -6);
        await victim.apply_damage(final_damage, BRUTE, blocked: armor_check, spread_damage: TRUE);
        return await base.action(source, target, modifiers);
    }
}

// tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:154
public partial class obj_item_mecha_parts_mecha_equipment_extinguisher : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:169
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(400);
        Waiting.NoSleep((reagents as datum_reagents).add_reagent(typeof(datum_reagent_water), 400));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:174
    public virtual async DmTask spray_extinguisher(mob user = null)
    {
        datum_reagents held = reagents as datum_reagents;
        if (Dm.num(held.total_volume) < required_amount)
            return;

        foreach (turf targetturf in Dm.each<turf>(RANGE_TURFS(1, chassis)))
        {
            obj_effect_particle_effect_water_extinguisher water = Dm.@new<obj_effect_particle_effect_water_extinguisher>(targetturf);
            datum_reagents water_reagents = Dm.@new<datum_reagents>(required_amount / 8); //required_amount/8, because the water usage is split between eight sprays. As of this comment, required_amount/8 = 10u each.
            water.reagents = water_reagents;
            water_reagents.my_atom = water;
            await held.trans_to(water, required_amount / 8);
            water.move_at(get_step(chassis, Dm.num(get_dir(targetturf, chassis))), 2, 4); //Target is the tile opposite of the mech as the starting turf.
        }

        playsound(chassis, Dm.resource("sound/effects/extinguish.ogg"), 75, TRUE, -3);
    }

    /**
     * Handles attempted refills of the extinguisher.
     *
     * The mech can only refill an extinguisher that is in front of it.
     * Only water tank objects can be used.
     */
    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:195
    public virtual async DmTask attempt_refill(mob user = null)
    {
        datum_reagents held = reagents as datum_reagents;
        if (Dm.eq(held.maximum_volume, held.total_volume))
            return;
        turf in_front = get_step(chassis, Dm.num(chassis.dir)) as turf;
        obj_structure_reagent_dispensers_watertank refill_source = Dm.locate<obj_structure_reagent_dispensers_watertank>(in_front);
        if (refill_source == null)
        {
            to_chat(user, span_notice(I18n.T("Refill failed. No compatible tank found.")));
            return;
        }
        datum_reagents source_reagents = refill_source.reagents as datum_reagents;
        if (!Dm.truthy(source_reagents?.total_volume))
        {
            to_chat(user, span_notice(I18n.T("Refill failed. Source tank empty.")));
            return;
        }

        await source_reagents.trans_to(this, held.maximum_volume);
        playsound(chassis, Dm.resource("sound/effects/refill.ogg"), 50, TRUE, -6);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:210
    public override DmList get_snowflake_data()
    {
        datum_reagents held = reagents as datum_reagents;
        return Dm.list(
            Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_EXTINGUISHER),
            Dm.kv("reagents", held.total_volume),
            Dm.kv("total_reagents", held.maximum_volume),
            Dm.kv("reagents_required", required_amount));
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:218
    public override async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.eq(action, "activate"))
        {
            await spray_extinguisher(usr);
            return TRUE;
        }
        if (Dm.eq(action, "refill"))
        {
            await attempt_refill(usr);
            return TRUE;
        }
        return null;
    }
}

// tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:230
public partial class obj_item_mecha_parts_mecha_equipment_rcd : obj_item_mecha_parts_mecha_equipment
{
    ///Maximum range the RCD can construct at.
    private const float RCD_RANGE = 3;

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:247
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        internal_rcd = Dm.@new<obj_item_construction_rcd_exosuit>(this);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:251
    public override object Destroy(float? force = null)
    {
        initial_location = null;
        QDEL_NULL(ref internal_rcd);
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:256
    public override DmList get_snowflake_data() => Dm.list(
        Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_RCD),
        Dm.kv("scan_ready", internal_rcd.destructive_scan_cooldown <= world.time ? TRUE : FALSE), // COOLDOWN_FINISHED(internal_rcd, destructive_scan_cooldown)
        Dm.kv("deconstructing", deconstruct_active),
        Dm.kv("mode", internal_rcd.design_title));

    /// Set the RCD's owner when attaching and detaching it
    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:265
    public override async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        internal_rcd.owner = new_mecha;
        await base.attach(new_mecha, attach_right);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:269
    public override async DmTask detach(atom moveto = null)
    {
        internal_rcd.owner = null;
        await base.detach(moveto);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:273
    public override async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.eq(action, "rcd_scan"))
        {
            if (!(internal_rcd.destructive_scan_cooldown <= world.time)) // !COOLDOWN_FINISHED
                return FALSE;
            rcd_scan(internal_rcd);
            internal_rcd.destructive_scan_cooldown = world.time + RCD_DESTRUCTIVE_SCAN_COOLDOWN; // COOLDOWN_START
            return TRUE;
        }
        if (Dm.eq(action, "toggle_deconstruct"))
        {
            deconstruct_active = Dm.truthy(deconstruct_active) ? 0 : 1;
            return TRUE;
        }
        if (Dm.eq(action, "change_mode"))
        {
            foreach (mob driver in Dm.each<mob>(chassis.return_drivers()))
                await internal_rcd.ui_interact(driver);
            return TRUE;
        }
        return null;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:290
    public override async DmTask<object> do_after_checks(atom target = null, object flags = null)
    {
        // Checks if mech moved during operation
        if (chassis.loc != initial_location)
            return FALSE;

        // Cancel build if design changes
        if (!Dm.truthy(deconstruct_active) && Dm.truthy(internal_rcd.blueprint_changed))
            return FALSE;

        return await base.do_after_checks(target, flags);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:321
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is obj_item_rcd_upgrade upgrade)
        {
            await internal_rcd.install_upgrade(upgrade, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:301
    public override async DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null)
    {
        if (!Dm.truthy(action_checks(target)))
            return null;
        // No meson action!
        if (!Dm.@in(target, view(RCD_RANGE, get_turf(chassis))))
            return null;
        if (Dm.num(get_dist(chassis, target)) > RCD_RANGE)
        {
            balloon_alert(source, I18n.T("out of range!"));
            return null;
        }
        initial_location = chassis.loc;

        await base.action(source, target, modifiers); // do this now because the do_after can take a while

        object construction_mode = internal_rcd.mode;
        if (Dm.truthy(deconstruct_active)) // deconstruct isn't in the RCD menu so switch it to deconstruct mode and set it back when it's done
            internal_rcd.mode = RCD_DECONSTRUCT;
        await internal_rcd.rcd_create(target, source);
        internal_rcd.mode = Dm.num(construction_mode);
        return TRUE;
    }
}

//Dunno where else to put this so shrug
// tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:330
public partial class obj_item_mecha_parts_mecha_equipment_ripleyupgrade : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:338
    public override object can_attach(obj_vehicle_sealed_mecha mecha = null, object attach_right = null, mob user = null)
    {
        if (mecha.type != typeof(obj_vehicle_sealed_mecha_ripley))
        {
            to_chat(user, span_warning(I18n.T("This conversion kit can only be applied to APLU MK-I models.")));
            return FALSE;
        }
        obj_vehicle_sealed_mecha_ripley workmech = mecha as obj_vehicle_sealed_mecha_ripley;
        if (workmech.cargo_hold != null && Dm.truthy(LAZYLEN(workmech.cargo_hold.contents)))
        {
            to_chat(user, span_warning(I18n.T("[]'s cargo hold must be empty before this conversion kit can be applied.", mecha)));
            return FALSE;
        }
        if (!Dm.truthy(Dm.bitand(mecha.mecha_flags, PANEL_OPEN))) //non-removable upgrade, so lets make sure the pilot or owner has their say.
        {
            to_chat(user, span_warning(I18n.T("[] panel must be open in order to allow this conversion kit.", mecha)));
            return FALSE;
        }
        if (Dm.truthy(LAZYLEN(mecha.occupants))) //We're actually making a new mech and swapping things over, it might get weird if players are involved
        {
            to_chat(user, span_warning(I18n.T("[] must be unoccupied before this conversion kit can be applied.", mecha)));
            return FALSE;
        }
        if (mecha.cell == null) //Turns out things break if the cell is missing
        {
            to_chat(user, span_warning(I18n.T("The conversion process requires a cell installed.")));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:357
    public override async DmTask attach(obj_vehicle_sealed_mecha markone = null, object attach_right = null)
    {
        obj_vehicle_sealed_mecha newmech = Dm.@new(result, get_turf(markone), 1) as obj_vehicle_sealed_mecha;
        if (newmech == null)
            return;
        QDEL_NULL(ref newmech.cell);
        if (markone.cell != null)
        {
            newmech.cell = markone.cell;
            await markone.cell.forceMove(newmech);
            markone.cell = null;
        }
        QDEL_NULL(ref newmech.scanmod);
        if (markone.scanmod != null)
        {
            newmech.scanmod = markone.scanmod;
            await markone.scanmod.forceMove(newmech);
            markone.scanmod = null;
        }
        QDEL_NULL(ref newmech.capacitor);
        if (markone.capacitor != null)
        {
            newmech.capacitor = markone.capacitor;
            await markone.capacitor.forceMove(newmech);
            markone.capacitor = null;
        }
        QDEL_NULL(ref newmech.servo);
        if (markone.servo != null)
        {
            newmech.servo = markone.servo;
            await markone.servo.forceMove(newmech);
            markone.servo = null;
        }
        newmech.update_part_values();
        foreach (obj_item_mecha_parts_mecha_equipment equipment in Dm.each<obj_item_mecha_parts_mecha_equipment>(markone.flat_equipment?.Copy())) //Move the equipment over...
        {
            if (istype(equipment, typeof(obj_item_mecha_parts_mecha_equipment_ejector)))
                continue; //the new mech already has one.
            bool righthandgun = markone.equip_by_category[MECHA_R_ARM] == equipment;
            await equipment.detach(newmech);
            await equipment.attach(newmech, righthandgun ? TRUE : FALSE);
        }
        newmech.dna_lock = markone.dna_lock;
        newmech.mecha_flags = Dm.bitor(newmech.mecha_flags, Dm.bitand(markone.mecha_flags, Dm.bitnot(Dm.initial(markone, nameof(markone.mecha_flags))))); // transfer any non-inherent flags like PANEL_OPEN and LIGHTS_ON
        await newmech.set_light_on(Dm.bitand(newmech.mecha_flags, LIGHTS_ON)); // in case the lights were on
        newmech.strafe = markone.strafe;
        //Integ set to the same percentage integ as the old mecha, rounded to be whole number
        newmech.update_integrity(round(Dm.num(markone.get_integrity()) / Dm.num(markone.max_integrity) * Dm.num(newmech.get_integrity())));
        if (!Dm.eq(markone.name, Dm.initial(markone, "name")))
            newmech.name = markone.name;
        markone.wreckage = null;
        if (HAS_TRAIT(markone, TRAIT_MECHA_CREATED_NORMALLY))
            ADD_TRAIT(newmech, TRAIT_MECHA_CREATED_NORMALLY, REF(newmech));
        qdel(markone);
        playsound(get_turf(newmech), Dm.resource("sound/items/tools/ratchet.ogg"), 50, TRUE);
    }
}

// tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:402
public partial class obj_item_mecha_parts_mecha_equipment_ripleyupgrade_paddy : obj_item_mecha_parts_mecha_equipment_ripleyupgrade
{
    // tg: code/modules/vehicles/mecha/equipment/tools/work_tools.dm:410
    public override object can_attach(obj_vehicle_sealed_mecha mecha = null, object attach_right = null, mob user = null)
    {
        if (Dm.truthy(mecha.equip_by_category[MECHA_L_ARM]) || Dm.truthy(mecha.equip_by_category[MECHA_R_ARM])) //Paddys can't use RIPLEY-type equipment
        {
            to_chat(user, span_warning(I18n.T("This kit cannot be applied with hardpoint equipment attached.")));
            return FALSE;
        }
        return base.can_attach(mecha, attach_right, user);
    }
}
