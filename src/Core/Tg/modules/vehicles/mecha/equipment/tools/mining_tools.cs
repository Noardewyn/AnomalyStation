// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/equipment/tools/mining_tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Drill, Diamond drill, Mining scanner

// tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:8
public partial class obj_item_mecha_parts_mecha_equipment_drill : obj_item_mecha_parts_mecha_equipment
{
    internal const float DRILL_BASIC = 1;
    internal const float DRILL_HARDENED = 2;

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering_mecha),
            Dm.named("speed", 5 * SECONDS),
            Dm.named("effectiveness", 100),
            Dm.named("bonus_modifier", null),
            Dm.named("butcher_sound", null),
            Dm.named("disabled", TRUE)));
        ADD_TRAIT(this, TRAIT_INSTANTLY_PROCESSES_BOULDERS, INNATE_TRAIT);
        ADD_TRAIT(this, TRAIT_BOULDER_BREAKER, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:38
    public override DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (!Dm.eq(action, "toggle"))
            return DmTask<object>.FromResult(null);
        if (Dm.truthy(active))
            RegisterSignal(chassis, COMSIG_MECHA_MELEE_CLICK, nameof(on_mech_click));
        else
            UnregisterSignal(chassis, COMSIG_MECHA_MELEE_CLICK);
        return DmTask<object>.FromResult(TRUE);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:49
    public override async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        await base.attach(new_mecha, attach_right);
        RegisterSignal(chassis, COMSIG_MOVABLE_BUMP, nameof(bump_mine));
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:53
    public override async DmTask detach(atom moveto = null)
    {
        UnregisterSignal(chassis, COMSIG_MOVABLE_BUMP);
        await base.detach(moveto);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:57
    public override object Destroy(float? force = null)
    {
        if (chassis != null)
            UnregisterSignal(chassis, COMSIG_MOVABLE_BUMP);
        return base.Destroy(force);
    }

    ///Called whenever the mech bumps into something; action() handles checking if it is a mineable turf
    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:63
    public virtual void bump_mine(obj_vehicle_sealed_mecha bumper = null, atom bumped_into = null)
    {
        DmList drivers = chassis.return_drivers() as DmList;
        if (!Dm.truthy(LAZYLEN(drivers))) //I don't know if this is possible but just in case
            return;

        //Just use the first one /shrug
        Waiting.Detach(drill_action(drivers[1] as mob, bumped_into, null, true));
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:72
    public override async DmTask<object> do_after_checks(atom target = null, object flags = null)
    {
        // Gotta be close to the target
        if (!Dm.truthy(await loc.Adjacent(target)))
            return FALSE;
        // Check if we can still use the equipment & use power for every iteration of do after
        if (!Dm.truthy(action_checks(target)))
            return FALSE;
        return await base.do_after_checks(target, flags);
    }

    ///Redirects clicks to use the drill if possible when enabled
    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:82
    public virtual object on_mech_click(atom mech = null, mob source = null, atom target = null, object on_cooldown = null, object adjacent = null)
    {
        if (Dm.truthy(on_cooldown) || !Dm.truthy(adjacent))
            return null;
        Waiting.Detach(drill_action(source, target, null, false));
        return COMPONENT_CANCEL_MELEE_CLICK;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:89
    public override DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null) => drill_action(source, target, modifiers, false);

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:89  action(source, target, modifiers, bumped)
    public virtual async DmTask<object> drill_action(mob source = null, atom target = null, DmList modifiers = null, bool bumped = false)
    {
        //If bumped, only bother drilling mineral turfs
        if (bumped)
        {
            if (!ismineralturf(target))
                return null;

            //Prevent drilling into gibtonite more than once; code mostly from MODsuit drill
            if (target is turf_closed_mineral_gibtonite giberal_turf)
            {
                if (!Dm.eq(giberal_turf.stage, GIBTONITE_UNSTRUCK))
                {
                    playsound(chassis, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
                    to_chat(source, span_warning(I18n.T("Active gibtonite ore deposit detected! Safety protocols preventing continued drilling.")));
                    return null;
                }
            }
        }
        else
        {
            // We can only drill non-space turfs, living mobs and objects.
            if (isspaceturf(target) || !(isliving(target) || isobj(target) || isturf(target)))
                return null;

            // For whatever reason we can't drill things that acid won't even stick too, and probably
            // shouldn't waste our time drilling indestructible things.
            if (isobj(target))
            {
                obj target_obj = target as obj;
                if (Dm.truthy(Dm.bitand(target_obj.resistance_flags, Dm.bitor(UNACIDABLE, INDESTRUCTIBLE))))
                    return null;
            }
        }

        // Check if we can even use the equipment to begin with.
        if (!Dm.truthy(action_checks(target)))
            return null;

        // You can't drill harder by clicking more.
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(source, target)) && Dm.truthy(await do_after_cooldown(target, source, DOAFTER_SOURCE_MECHADRILL)))
            return null;

        target.visible_message(span_warning(I18n.T("[] starts to drill [].", chassis, target)),
            span_userdanger(I18n.T("[] starts to drill you!", chassis)),
            span_hear(I18n.T("You hear drilling.")));

        log_message(Dm.text("Started drilling []", target), LOG_MECHA);

        // Drilling a turf is a one-and-done procedure.
        if (isturf(target))
        {
            turf T = target as turf;
            object dot = await base.action(source, target, modifiers);
            await T.drill_act(this, source);
            return dot;
        }

        // Drilling objects and mobs is a repeating procedure.
        while (Dm.truthy(await do_after_mecha(target, source, drill_delay)))
        {
            if (isliving(target))
            {
                await drill_mob(target as mob_living, source as mob_living);
                playsound(this, Dm.resource("sound/items/weapons/drill.ogg"), 40, TRUE);
            }
            else if (isobj(target))
            {
                obj obj_target = target as obj;
                if (obj_target is obj_item_boulder nu_boulder)
                    await nu_boulder.manual_process(this, source as mob_living);
                else
                    await obj_target.take_damage(15, BRUTE, 0, FALSE, get_dir(chassis, target));
                playsound(this, Dm.resource("sound/items/weapons/drill.ogg"), 40, TRUE);
            }

            // If we caused a qdel drilling the target, we can stop drilling them.
            // Prevents starting a do_after on a qdeleted target.
            if (QDELETED(target))
                break;
        }

        return await base.action(source, target, modifiers);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:157
    public override object get_equip_cooldown(atom target = null)
    {
        if (isturf(target))
            return equip_cooldown * 0.1f;
        return equip_cooldown;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:192
    public virtual async DmTask move_ores() => await chassis.collect_ore();

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:195
    public virtual async DmTask drill_mob(mob_living target = null, mob_living user = null)
    {
        target.visible_message(span_danger(I18n.T("[] is drilling [] with []!", chassis, target, this)),
            span_userdanger(I18n.T("[] is drilling you with []!", chassis, this)));
        log_combat(user, target, "drilled", Dm.text("[]", name), Dm.text("Combat mode: [])(DAMTYPE: [])", Dm.truthy(user.combat_mode) ? "On" : "Off", uppertext(damtype)));
        if (Dm.eq(target.stat, DEAD) && Dm.num(target.get_brute_loss()) >= Dm.num(target.maxHealth) * 2)
        {
            log_combat(user, target, "gibbed", name);
            if (Dm.truthy(LAZYLEN(target.butcher_results)) || Dm.truthy(LAZYLEN(target.guaranteed_butcher_results)))
                SEND_SIGNAL(COMSIG_MECHA_DRILL_MOB, Dm.list(this, chassis, target));
            else
            {
                target.investigate_log(Dm.text("has been gibbed by [] (attached to []).", this, chassis), INVESTIGATE_DEATHS);
                await target.gib(DROP_ALL_REMAINS);
            }
            return;
        }

        if (aimed_limb_missing(user, target)) // DEVIATION(DV592)
            return;
        //drill makes a hole
        // DEVIATION(DV591): an aimed melee hit lands on the selected zone; tg's 80% zone roll is not made
        object def_zone = target.get_random_valid_zone(user.zone_selected, 100);
        await target.apply_damage(
            10,
            BRUTE,
            def_zone,
            blocked: Dm.num(target.run_armor_check(def_zone, MELEE)),
            wound_bonus: 30,
            exposed_wound_bonus: 50,
            sharpness: SHARP_POINTY);

        //blood splatters
        await target.create_splatter(get_dir(chassis, target));
    }
}

// tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:162
public partial class turf
{
    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:162
    public virtual DmTask drill_act(obj_item_mecha_parts_mecha_equipment_drill drill = null, mob user = null) => DmTask.CompletedTask;
}

// tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:165
public partial class turf_closed_wall
{
    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:165
    public override async DmTask drill_act(obj_item_mecha_parts_mecha_equipment_drill drill = null, mob user = null)
    {
        if (Dm.truthy(await drill.do_after_mecha(this, user, 60 / drill.drill_level)))
        {
            drill.log_message(Dm.text("Drilled through []", this), LOG_MECHA);
            await dismantle_wall(TRUE, FALSE);
        }
    }
}

// tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:170
public partial class turf_closed_wall_r_wall
{
    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:170
    public override async DmTask drill_act(obj_item_mecha_parts_mecha_equipment_drill drill = null, mob user = null)
    {
        if (drill.drill_level >= obj_item_mecha_parts_mecha_equipment_drill.DRILL_HARDENED)
        {
            if (Dm.truthy(await drill.do_after_mecha(this, user, 120 / drill.drill_level)))
            {
                drill.log_message(Dm.text("Drilled through []", this), LOG_MECHA);
                await dismantle_wall(TRUE, FALSE);
            }
        }
        else
            to_chat(user, span_danger(I18n.T("[] is too durable to drill through.", this)));
    }
}

// tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:178
public partial class turf_closed_mineral
{
    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:178
    public override async DmTask drill_act(obj_item_mecha_parts_mecha_equipment_drill drill = null, mob user = null)
    {
        foreach (turf_closed_mineral wall in Dm.each<turf_closed_mineral>(range(drill.chassis, 1)))
        {
            if (Dm.truthy(Dm.bitand(get_dir(drill.chassis, wall), drill.chassis.dir)))
                await wall.gets_drilled();
        }
        drill.log_message(Dm.text("[] drilled through []", user, this), LOG_MECHA);
        await drill.move_ores();
    }
}

// tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:185
public partial class turf_open_misc_asteroid
{
    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:185
    public override async DmTask drill_act(obj_item_mecha_parts_mecha_equipment_drill drill = null, mob user = null)
    {
        foreach (turf_open_misc_asteroid floor in Dm.each<turf_open_misc_asteroid>(range(1, drill.chassis)))
        {
            if (Dm.truthy(Dm.bitand(get_dir(drill.chassis, floor), drill.chassis.dir)) && Dm.truthy(floor.can_dig()))
                await floor.getDug();
        }
        drill.log_message(Dm.text("Drilled through []", this), LOG_MECHA);
        await drill.move_ores();
    }
}

// tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:234
public partial class obj_item_mecha_parts_mecha_equipment_mining_scanner : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:245
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSfastprocess, this);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:249
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (loc == null)
        {
            STOP_PROCESSING(SSfastprocess, this);
            qdel(this);
        }
        if (scanning_time > world.time)
            return null;
        if (chassis == null || !ismecha(loc))
            return null;
        if (!Dm.truthy(LAZYLEN(chassis.occupants)))
            return null;
        scanning_time = world.time + Dm.num(get_equip_cooldown());
        mineral_scan_pulse(get_turf(this), scanner: this);
        return null;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:262
    public override DmList get_snowflake_data() => Dm.list(
        Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_ORE_SCANNER),
        Dm.kv("cooldown", Math.Max(0, area_scan_cooldown - world.time))); // COOLDOWN_TIMELEFT

    // tg: code/modules/vehicles/mecha/equipment/tools/mining_tools.dm:268
    public override async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.eq(action, "area_scan"))
        {
            if (!(area_scan_cooldown <= world.time))
                return FALSE;
            area_scan_cooldown = world.time + 15 * SECONDS;
            foreach (mob_living driver in Dm.each<mob_living>(chassis.return_drivers()))
            {
                foreach (obj_structure_ore_vent vent in Dm.each<obj_structure_ore_vent>(range(5, chassis)))
                    await vent.scan_and_confirm(driver, TRUE);
            }
            return TRUE;
        }
        return null;
    }
}
