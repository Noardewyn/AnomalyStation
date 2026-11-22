// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/combat/savannah_ivanov.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * ## Savannah-Ivanov!
 *
 * A two person mecha that delegates moving to the driver and shooting to the pilot.
 * ...Hilarious, right?
 */
// tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:15
public partial class obj_vehicle_sealed_mecha_savannah_ivanov : obj_vehicle_sealed_mecha
{
    ///how much time between charge_level going up by 1
    internal const float SKYFALL_SINGLE_CHARGE_TIME = 2 * SECONDS;
    ///enough charge level to take off, basically done charging
    internal const float SKYFALL_CHARGELEVEL_LAUNCH = 5;
    ///how much time you're in the air
    internal const float TOTAL_SKYFALL_LEAP_TIME = 3 * SECONDS;

    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:52
    public override object get_mecha_occupancy_state()
    {
        float driver_present = driver_amount() != 0 ? 1 : 0;
        float gunner_present = return_amount_of_controllers_with_flag(VEHICLE_CONTROL_EQUIPMENT) > 0 ? 1 : 0;
        DmList drivers = return_drivers() as DmList;
        string leap_state = null;
        if (Dm.truthy(length(drivers)))
        {
            datum_action_vehicle_sealed_mecha_skyfall action = LAZYACCESSASSOC(occupant_actions, drivers[1], typeof(datum_action_vehicle_sealed_mecha_skyfall)) as datum_action_vehicle_sealed_mecha_skyfall;
            leap_state = action != null && action.skyfall_charge_level > 2 ? "leap_" : "";
        }
        return Dm.text("[]_[][]_[]", base_icon_state, leap_state, gunner_present, driver_present);
    }

    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:62
    public override async DmTask auto_assign_occupant_flags(mob M = null)
    {
        if (driver_amount() < max_drivers) //movement
            await add_control_flags(M, Dm.bitor(VEHICLE_CONTROL_DRIVE, VEHICLE_CONTROL_SETTINGS));
        else //weapons
            await add_control_flags(M, Dm.bitor(VEHICLE_CONTROL_MELEE, VEHICLE_CONTROL_EQUIPMENT));
    }

    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:68
    public override void generate_actions()
    {
        initialize_passenger_action_type(typeof(datum_action_vehicle_sealed_mecha_swap_seat));
        base.generate_actions();
        initialize_controller_action_type(typeof(datum_action_vehicle_sealed_mecha_skyfall), VEHICLE_CONTROL_DRIVE);
        initialize_controller_action_type(typeof(datum_action_vehicle_sealed_mecha_ivanov_strike), VEHICLE_CONTROL_EQUIPMENT);
    }

    // Pass through obstacles freely if we're flying
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:75
    public override object CanPassThrough(atom blocker = null, object movement_dir = null, object blocker_opinion = null)
    {
        if (!Dm.truthy(flying) || Dm.truthy(throwing))
            return base.CanPassThrough(blocker, movement_dir, blocker_opinion);
        turf destination_turf = get_step(loc, Dm.num(movement_dir)) as turf;
        if (!Dm.truthy(check_teleport_valid(this, destination_turf)) || Dm.truthy(SSmapping.level_trait(destination_turf.z, ZTRAIT_NOPHASE)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:83
    public override bool can_interact_with(atom target = null, mob user = null, DmList modifiers = null)
    {
        bool dot = base.can_interact_with(target, user, modifiers);
        if (!dot || !Dm.truthy(flying))
            return dot;
        balloon_alert(user, I18n.T("not while airborne!"));
        return false;
    }
}

///Savannah Skyfall
// tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:91
public partial class datum_action_vehicle_sealed_mecha_skyfall : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:99
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (owner == null || chassis == null || !Dm.@in(owner, chassis.occupants))
            return dot;
        obj_vehicle_sealed_mecha_savannah_ivanov savannah = chassis as obj_vehicle_sealed_mecha_savannah_ivanov;
        if (Dm.truthy(savannah.flying))
        {
            to_chat(owner, span_warning(I18n.T("You're already airborne!")));
            return dot;
        }
        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(savannah, COOLDOWN_MECHA_SKYFALL)))
        {
            object timeleft = S_TIMER_COOLDOWN_TIMELEFT(savannah, COOLDOWN_MECHA_SKYFALL);
            to_chat(owner, span_warning(I18n.T("You need to wait [] before attempting to Skyfall.", DisplayTimeText(timeleft, 1))));
            return dot;
        }
        if (Dm.truthy(skyfall_charge_level))
        {
            abort_skyfall();
            return dot;
        }
        savannah.balloon_alert(owner, I18n.T("charging skyfall..."));
        Waiting.Detach(skyfall_charge_loop());
        return dot;
    }

    /**
     * ## skyfall_charge_loop
     *
     * The actual skyfall loop itself. Repeatedly calls itself after a do_after, so any interruptions will call abort_skyfall and end the loop
     * the other way the loop ends is if charge level (var it's ticking up) gets to SKYFALL_CHARGELEVEL_LAUNCH, in which case it ends the loop and does the ability.
     */
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:125
    public virtual async DmTask skyfall_charge_loop()
    {
        if (!Dm.truthy(await do_after(owner, obj_vehicle_sealed_mecha_savannah_ivanov.SKYFALL_SINGLE_CHARGE_TIME, target: chassis)))
        {
            abort_skyfall();
            return;
        }
        skyfall_charge_level++;
        switch (skyfall_charge_level)
        {
            case 1:
                chassis.visible_message(span_warning(I18n.T("[] clicks and whirrs for a moment, with a low hum emerging from the legs.", chassis)));
                playsound(chassis, Dm.resource("sound/items/tools/rped.ogg"), 50, TRUE);
                break;
            case 2:
                chassis.visible_message(span_warning(I18n.T("[] begins to shake, the sounds of electricity growing louder.", chassis)));
                chassis.Shake(1, 1, obj_vehicle_sealed_mecha_savannah_ivanov.SKYFALL_SINGLE_CHARGE_TIME - 1); // -1 gives space between the animates, so they don't interrupt eachother
                break;
            case 3:
                chassis.visible_message(span_warning(I18n.T("[] assumes a pose as it rattles violently.", chassis)));
                chassis.Shake(2, 2, obj_vehicle_sealed_mecha_savannah_ivanov.SKYFALL_SINGLE_CHARGE_TIME - 1);
                await chassis.spark_system.start();
                chassis.update_appearance(UPDATE_ICON_STATE);
                break;
            case 4:
                chassis.visible_message(span_warning(I18n.T("[] sparks and shutters as it finalizes preparation.", chassis)));
                playsound(chassis, Dm.resource("sound/vehicles/mecha/skyfall_power_up.ogg"), 50, TRUE);
                chassis.Shake(3, 3, obj_vehicle_sealed_mecha_savannah_ivanov.SKYFALL_SINGLE_CHARGE_TIME - 1);
                await chassis.spark_system.start();
                break;
            case obj_vehicle_sealed_mecha_savannah_ivanov.SKYFALL_CHARGELEVEL_LAUNCH:
                chassis.visible_message(span_danger(I18n.T("[] leaps into the air!", chassis)));
                playsound(chassis, Dm.resource("sound/items/weapons/gun/general/rocket_launch.ogg"), 50, TRUE);
                break;
        }
        if (skyfall_charge_level != obj_vehicle_sealed_mecha_savannah_ivanov.SKYFALL_CHARGELEVEL_LAUNCH)
        {
            await skyfall_charge_loop();
            return;
        }
        chassis.timer_cooldown_start(COOLDOWN_MECHA_SKYFALL, skyfall_cooldown_time); // S_TIMER_COOLDOWN_START
        button_icon_state = "mech_savannah_cooldown";
        await build_all_button_icons();
        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_button_icon)), skyfall_cooldown_time, file: "code/modules/vehicles/mecha/combat/savannah_ivanov.dm", line: 156);
        foreach (mob_living shaken in Dm.each<mob_living>(range(7, chassis)))
            shake_camera(shaken, 3, 3);

        turf launch_turf = get_turf(chassis);
        Dm.@new<obj_effect_hotspot>(launch_turf);
        launch_turf.hotspot_expose(700, 50, 1);
        Dm.@new<obj_effect_skyfall_landingzone>(launch_turf, chassis);
        chassis.resistance_flags = Dm.bitor(chassis.resistance_flags, INDESTRUCTIBLE); //not while jumping at least
        chassis.mecha_flags = Dm.bitor(chassis.mecha_flags, Dm.bitor(QUIET_STEPS, Dm.bitor(QUIET_TURNS, CANNOT_INTERACT)));
        obj_vehicle_sealed_mecha_savannah_ivanov savannah = chassis as obj_vehicle_sealed_mecha_savannah_ivanov;
        savannah.flying = TRUE;
        chassis.movedelay = 1;
        chassis.set_density(FALSE);
        chassis.layer = ABOVE_ALL_MOB_LAYER;
        Dm.animate(chassis, Dm.named("alpha", 0), Dm.named("time", 8), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)), Dm.named("flags", ANIMATION_PARALLEL));
        Dm.animate(chassis, Dm.named("pixel_z", 400), Dm.named("time", 10), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)), Dm.named("flags", ANIMATION_PARALLEL)); //Animate our rising mech (just like pods hehe)
        _addtimer(Dm.@new<datum_callback>(this, nameof(begin_landing)), 2 * SECONDS, file: "code/modules/vehicles/mecha/combat/savannah_ivanov.dm", line: 173);
    }

    /**
     * ## begin_landing
     *
     * Called by skyfall_charge_loop after some time if it reaches full charge level.
     * it's just the animations of the mecha coming down + another timer for the final landing effect
     */
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:181
    public virtual void begin_landing()
    {
        Dm.animate(chassis, Dm.named("pixel_z", 0), Dm.named("time", 10), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)), Dm.named("flags", ANIMATION_PARALLEL));
        Dm.animate(chassis, Dm.named("alpha", 255), Dm.named("time", 8), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)), Dm.named("flags", ANIMATION_PARALLEL));
        _addtimer(Dm.@new<datum_callback>(this, nameof(land)), 1 * SECONDS, file: "code/modules/vehicles/mecha/combat/savannah_ivanov.dm", line: 184);
    }

    /**
     * ## land
     *
     * Called by skyfall_charge_loop after some time if it reaches full charge level.
     * it's just the animations of the mecha coming down + another timer for the final landing effect
     */
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:192
    public virtual async DmTask land()
    {
        turf landed_on = get_turf(chassis);
        chassis.visible_message(span_danger(I18n.T("[] lands from above!", chassis)));
        playsound(chassis, Dm.resource("sound/effects/explosion/explosion1.ogg"), 50, 1);
        chassis.resistance_flags = Dm.bitand(chassis.resistance_flags, Dm.bitnot(INDESTRUCTIBLE));
        chassis.mecha_flags = Dm.bitand(chassis.mecha_flags, Dm.bitnot(Dm.bitor(QUIET_STEPS, Dm.bitor(QUIET_TURNS, CANNOT_INTERACT))));
        obj_vehicle_sealed_mecha_savannah_ivanov savannah = chassis as obj_vehicle_sealed_mecha_savannah_ivanov;
        savannah.flying = FALSE;
        chassis.movedelay = Dm.num(Dm.initial(chassis, nameof(chassis.movedelay)));
        chassis.set_density(TRUE);
        chassis.layer = Dm.num(Dm.initial(chassis, nameof(chassis.layer)));
        SET_PLANE(chassis, Dm.num(Dm.initial(chassis, nameof(chassis.plane))), landed_on);
        skyfall_charge_level = 0;
        chassis.update_appearance(UPDATE_ICON_STATE);
        foreach (mob_living shaken in Dm.each<mob_living>(range(7, chassis)))
            shake_camera(shaken, 5, 5);
        foreach (object thing in Dm.each<object>(range(1, chassis)))
        {
            if (isopenturf(thing))
            {
                (thing as turf_open_floor)?.break_tile();
                continue;
            }
            if (isclosedturf(thing) && thing == landed_on)
            {
                await (thing as turf_closed).ScrapeAway();
                continue;
            }
            if (isobj(thing))
            {
                obj crushed_object = thing as obj;
                if (crushed_object == chassis || crushed_object.loc == chassis)
                    continue;
                await crushed_object.take_damage(150); //same as a hulk punch, makes sense to me
                continue;
            }
            if (isliving(thing))
            {
                mob_living crushed_victim = thing as mob_living;
                if (Dm.@in(crushed_victim, chassis.occupants))
                    continue;
                if (!Dm.@in(crushed_victim, landed_on.contents))
                {
                    to_chat(crushed_victim, span_userdanger(I18n.T("The tremors from [] landing sends you flying!", chassis)));
                    object fly_away_direction = get_dir(chassis, crushed_victim);
                    await crushed_victim.throw_at(get_edge_target_turf(crushed_victim, fly_away_direction) as atom, 4, 3);
                    await crushed_victim.adjust_brute_loss(15);
                    continue;
                }
                to_chat(crushed_victim, span_userdanger(I18n.T("[] crashes down on you from above!", chassis)));
                if (IS_UNCONSCIOUS_OR_CRIT(crushed_victim))
                {
                    crushed_victim.investigate_log("has been gibbed by a falling Savannah Ivanov mech.", INVESTIGATE_DEATHS);
                    await crushed_victim.gib(DROP_ALL_REMAINS);
                    continue;
                }
                await crushed_victim.adjust_brute_loss(80);
            }
        }
    }

    /**
     * ## abort_skyfall
     *
     * Called by skyfall_charge_loop if the charging is interrupted.
     * Applies cooldown and resets charge level
     */
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:246
    public virtual void abort_skyfall()
    {
        chassis.balloon_alert(owner, I18n.T("skyfall aborted"));
        chassis.timer_cooldown_start(COOLDOWN_MECHA_MISSILE_STRIKE, skyfall_charge_level * 10 * SECONDS); //so aborting skyfall later in the process imposes a longer cooldown
        skyfall_charge_level = 0;
        chassis.update_appearance(UPDATE_ICON_STATE);
    }

    /**
     * ## reset_button_icon
     *
     * called after an addtimer when the cooldown is finished with the skyfall, resets the icon
     */
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:257
    public virtual async DmTask reset_button_icon()
    {
        button_icon_state = "mech_savannah";
        await build_all_button_icons();
    }
}

// tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:261
public partial class datum_action_vehicle_sealed_mecha_ivanov_strike : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:270
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(aiming_missile))
            end_missile_targeting();
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:275
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (chassis == null || !Dm.@in(owner, chassis.occupants))
            return dot;
        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(chassis, COOLDOWN_MECHA_MISSILE_STRIKE)))
        {
            object timeleft = S_TIMER_COOLDOWN_TIMELEFT(chassis, COOLDOWN_MECHA_MISSILE_STRIKE);
            to_chat(owner, span_warning(I18n.T("You need to wait [] before firing another Ivanov Strike.", DisplayTimeText(timeleft, 1))));
            return dot;
        }
        if (Dm.truthy(aiming_missile))
            end_missile_targeting();
        else
            start_missile_targeting();
        return dot;
    }

    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:295
    public virtual async DmTask reset_button_icon()
    {
        button_icon_state = "mech_ivanov";
        await build_all_button_icons();
    }

    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:305
    public virtual void start_missile_targeting()
    {
        chassis.balloon_alert(owner, I18n.T("missile mode on (click to target)"));
        aiming_missile = TRUE;
        rockets_left = 3;
        RegisterSignal(chassis, COMSIG_MECHA_MELEE_CLICK, nameof(on_melee_click));
        RegisterSignal(chassis, COMSIG_MECHA_EQUIPMENT_CLICK, nameof(on_equipment_click));
        owner.client.mouse_override_icon = Dm.resource("icons/effects/mouse_pointers/supplypod_down_target.dmi");
        owner.update_mouse_pointer();
        owner.overlay_fullscreen("ivanov", typeof(atom_movable_screen_fullscreen_ivanov_display), 1);
        Dm.output_to(owner, Dm.resource("sound/machines/terminal/terminal_on.ogg")); //spammable so I don't want to make it audible to anyone else
    }

    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:322
    public virtual void end_missile_targeting()
    {
        aiming_missile = FALSE;
        rockets_left = 0;
        UnregisterSignal(chassis, Dm.list(COMSIG_MECHA_MELEE_CLICK, COMSIG_MECHA_EQUIPMENT_CLICK));
        if (owner?.client != null)
            owner.client.mouse_override_icon = null;
        owner?.update_mouse_pointer();
        owner?.clear_fullscreen("ivanov");
    }

    ///signal called from clicking with no equipment
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:331
    public virtual void on_melee_click(datum source = null, mob_living pilot = null, atom target = null, object on_cooldown = null, object is_adjacent = null)
    {
        if (target == null)
            return;
        Waiting.Detach(drop_missile(get_turf(target)));
    }

    ///signal called from clicking with equipment
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:338
    public virtual void on_equipment_click(datum source = null, mob_living pilot = null, atom target = null)
    {
        if (target == null)
            return;
        Waiting.Detach(drop_missile(get_turf(target)));
    }

    /**
     * ## drop_missile
     *
     * Called via intercepted clicks when the missile ability is active
     * Spawns a droppod and starts the cooldown of the missile strike ability
     * arguments:
     * * target_turf: turf of the atom that was clicked on
     */
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:352
    public virtual async DmTask drop_missile(turf target_turf = null)
    {
        rockets_left--;
        if (rockets_left <= 0)
            end_missile_targeting();
        Dm.output_to(owner, Dm.resource("sound/machines/beep/triple_beep.ogg"));
        chassis.timer_cooldown_start(COOLDOWN_MECHA_MISSILE_STRIKE, strike_cooldown_time); // S_TIMER_COOLDOWN_START
        await podspawn(Dm.list(
            Dm.kv("target", target_turf),
            Dm.kv("style", typeof(datum_pod_style_missile)),
            Dm.kv("effectMissile", TRUE),
            Dm.kv("explosionSize", Dm.list(0f, 0f, 1f, 2f))));
        button_icon_state = "mech_ivanov_cooldown";
        await build_all_button_icons();
        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_button_icon)), strike_cooldown_time, file: "code/modules/vehicles/mecha/combat/savannah_ivanov.dm", line: 366);
    }
}

//misc effects

///a simple indicator of where the skyfall is going to land.
// tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:371
public partial class obj_effect_skyfall_landingzone : obj_effect
{
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:383
    [DmParams("mapload", "mecha")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_vehicle_sealed_mecha mecha__1 = Dm.arg(__extra, 0) as obj_vehicle_sealed_mecha;
        object dot = base.Initialize(mapload);
        if (mecha__1 == null)
        {
            stack_trace("Skyfall landing zone created without mecha", "code/modules/vehicles/mecha/combat/savannah_ivanov.dm", 386);
            return INITIALIZE_HINT_QDEL;
        }
        mecha = mecha__1;
        Dm.animate(this, Dm.named("alpha", 255), Dm.named("time", obj_vehicle_sealed_mecha_savannah_ivanov.TOTAL_SKYFALL_LEAP_TIME / 2), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_OUT)));
        RegisterSignal(mecha, COMSIG_MOVABLE_MOVED, nameof(follow));
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), obj_vehicle_sealed_mecha_savannah_ivanov.TOTAL_SKYFALL_LEAP_TIME, file: "code/modules/vehicles/mecha/combat/savannah_ivanov.dm", line: 391); //when the animations land
        return dot;
    }

    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:393
    public override object Destroy(float? force = null)
    {
        mecha = null;
        return base.Destroy(force);
    }

    ///called when the mecha moves
    // tg: code/modules/vehicles/mecha/combat/savannah_ivanov.dm:398
    public virtual void follow(datum source_mecha = null) => Waiting.Detach(forceMove(get_turf(source_mecha)));
}
