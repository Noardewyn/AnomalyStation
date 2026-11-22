// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/doors/door.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the door's closure that a bump needs (M3 P3, scope 2f; D117): Initialize, the bump gate and access, open and
// close with tg's animation states and segment delays, autoclose, the safe-close blockers, the appearance, power, the
// filler of multi-tile airlocks, the morgue's timings. Not here (inherited until their milestone): the context
// tips (M9), telekinesis (M8), EMP (M7, M10+), the crushing of a mecha (below). M4a P4a: the tool acts
// (welder, crowbar, their right clicks) and item_interaction with the ID swipe. M5 slice 1: the spark system, take_damage
// (its sparks) and play_attack_sound. M5x slice 1: ex_act and the blocks_explosives element (explosions). Gaps R4: the red alert's
// lifted access (the security level's signal and check_security_level). The var block is
// generated (D111).

// tg: code/game/machinery/doors/door.dm:6
[DmPath("/obj/machinery/door")]
public partial class obj_machinery_door : obj_machinery
{
    ///Default wait until doors autoclose
    // tg: code/game/machinery/doors/door.dm:2  #define DOOR_CLOSE_WAIT 60 (file-local, #undef at :789)
    private const int DOOR_CLOSE_WAIT = 60;
    /// Trait for checking if a mob is currently activating an unrestricted airlock open and thus has pressure pushes blocked
    // tg: code/game/machinery/doors/door.dm:4  #define TRAIT_UNRESTRICTED_AIRLOCK_OPENING (file-local, #undef at :790)
    private const string TRAIT_UNRESTRICTED_AIRLOCK_OPENING = "trait_unrestricted_airlock_opening";

    // tg: code/game/machinery/doors/door.dm:109
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        Waiting.NoSleep(AddElement(typeof(datum_element_blocks_explosives)));
        dot = base.Initialize(mapload, __extra);
        set_init_door_layer();
        if (Dm.truthy(multi_tile))
        {
            set_bounds();
            Waiting.NoSleep(set_filler());
            update_appearance(UPDATE_OVERLAYS);
        }
        air_update_turf(TRUE, TRUE);
        // GAP(lead): /atom/proc/register_context (datums/screentips/atom_context.dm:5) is not declared.
        Dm.Deferred("code/game/machinery/doors/door.dm:118", "Initialize: register_context(): the door's screentips (M10+)");
        if (Dm.truthy(elevator_mode))
        {
            if (Dm.truthy(transport_linked_id))
            {
                elevator_status = LIFT_PLATFORM_LOCKED;
                Dm.append(ref GLOB.elevator_doors, this);
            }
            else
            {
                stack_trace(Dm.text("Elevator door [] ([],[],[]) has no linked elevator ID!", this, x, y, z), "code/game/machinery/doors/door.dm", 124);
            }
        }
        spark_system = Dm.@new<datum_effect_system_basic_spark_spread>(this, 2, TRUE);
        if (Dm.truthy(density))
            Dm.combine(ref flags_1, PREVENT_CLICK_UNDER_1);
        else
            Dm.mask(ref flags_1, Dm.bitnot(PREVENT_CLICK_UNDER_1));

        if (Dm.truthy(glass))
            Dm.combine(ref pass_flags, PASSWINDOW);
        //doors only block while dense though so we have to use the proc
        real_explosion_block = explosion_block;
        update_explosive_block();
        // the port's test worlds may boot without the subsystem (tg always has it)
        if (SSsecurity_level != null)
            RegisterSignal(SSsecurity_level, COMSIG_SECURITY_LEVEL_CHANGED, nameof(check_security_level));

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_barricade)));
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/doors/door.dm:138  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_machinery_door__Initialize__loc_connections;

    // tg: code/game/machinery/doors/door.dm:145
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(red_alert_access))
        {
            if (Dm.num(SSsecurity_level.get_current_level_as_number()) >= SEC_LEVEL_RED)
                Dm.append(ref dot, span_notice(I18n.T("Due to a security threat, its access requirements have been lifted!")));
            else
                Dm.append(ref dot, span_notice(I18n.T("In the event of a red alert, its access requirements will automatically lift.")));
        }
        if (Dm.truthy(has_access_panel))
            Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel is [].", Dm.truthy(panel_open) ? I18n.T("open") : I18n.T("<b>screwed</b> in place"))));
        return dot;
    }

    // tg: code/game/machinery/doors/door.dm:168
    public override object check_access_list(DmList access_list = null)
    {
        if (Dm.truthy(red_alert_access) && Dm.num(SSsecurity_level.get_current_level_as_number()) >= SEC_LEVEL_RED)
            return TRUE;
        return base.check_access_list(access_list);
    }

    // tg: code/game/machinery/doors/door.dm:173
    public virtual void set_init_door_layer()
    {
        if (Dm.truthy(density))
            layer = closingLayer;
        else
            layer = Dm.initial(this, (obj_machinery_door p) => p.layer);
    }

    // tg: code/game/machinery/doors/door.dm:179
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(elevator_mode))
            Dm.remove(GLOB.elevator_doors, this);
        QDEL_NULL(ref spark_system);
        QDEL_NULL(ref filler);
        air_update_turf(TRUE, FALSE);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/doors/door.dm:187 (an earlier duplicate definition in the same type: __d0, rules 2.5)
    [DmParams()]
    public new async DmTask<object> Move__d0(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null) // new: it hides atom_movable's Move__d0, BYOND's builtin Move (rules 7.1)
    {
        if (Dm.truthy(multi_tile))
            await set_filler();
        return await base.Move(newloc, direction, glide_size_override, update_dir);
    }

    /**
     * Sets the bounds of the airlock. For use with multi-tile airlocks.
     * If the airlock is multi-tile, it will set the bounds to be the size of the airlock.
     * If the airlock doesn't already have a filler object, it will create one.
     * If the airlock already has a filler object, it will move it to the correct location.
     */
    // tg: code/game/machinery/doors/door.dm:198
    public virtual async DmTask set_filler()
    {
        if (!Dm.truthy(multi_tile))
            return;
        if (!Dm.truthy(filler))
        {
            filler = Dm.@new<obj_structure_fluff_airlock_filler>(get_step(this, Dm.num(get_adjusted_dir(dir))));
            filler.pair_airlock(this);
        }
        else
            filler.loc = get_step(this, Dm.num(get_adjusted_dir(dir)));

        filler.density = density;
        await filler.set_opacity(opacity);
    }

    /**
     * Checks which way the airlock is facing and adjusts the direction accordingly.
     * For use with multi-tile airlocks.
     *
     * @param dir direction to adjust
     * @return adjusted direction
     */
    // tg: code/game/machinery/doors/door.dm:217
    public virtual object get_adjusted_dir(object dir = null)
    {
        if (Dm.@in(dir, Dm.list(NORTH, SOUTH)))
            return EAST;
        else
            return NORTH;
    }

    /**
     * Signal handler for checking if we notify our surrounding that access requirements are lifted accordingly to a newly set security level
     *
     * Arguments:
     * * source The datum source of the signal
     * * new_level The new security level that is in effect
     */
    // tg: code/game/machinery/doors/door.dm:230
    [SignalHandler]
    public virtual void check_security_level(datum source = null, object new_level = null)
    {
        // SIGNAL_HANDLER
        if (Dm.num(new_level) <= SEC_LEVEL_BLUE)
            return;
        if (!Dm.truthy(red_alert_access))
            return;
        audible_message(span_notice(I18n.T("[] whirr[] as [] automatically lift[] access requirements!", this, p_s(), p_they(), p_s())));
        playsound(this, "sound/machines/airlock/boltsup.ogg", 50, TRUE);
    }

    // tg: code/game/machinery/doors/door.dm:240
    public virtual async DmTask<object> try_safety_unlock(mob user = null)
    {
        return FALSE;
    }

    /**
     * Called when attempting to remove the seal from an airlock
     *
     * Here because we need to call it and return if there was a seal so we don't try to open the door
     * or try its safety lock while it's sealed
     * Arguments:
     * * user - the mob attempting to remove the seal
     */
    // tg: code/game/machinery/doors/door.dm:251
    public virtual async DmTask<object> try_remove_seal(mob user = null)
    {
        return null;
    }

    // tg: code/game/machinery/doors/door.dm:254
    // set waitfor = FALSE (inherited from /atom/Bumped): this body may sleep through bumpopen and open, so it is the
    // wrapper of waiting-model.md 5.4 (the body never sets . before a sleep: no box).
    public override object Bumped(atom_movable AM = null)
    {
        return Waiting.WaitforFalse(Bumped__body(AM, null), null);
    }

    private async DmTask<object> Bumped__body(atom_movable AM, DotBox<object> dot)
    {
        base.Bumped(AM);
        if (Dm.truthy(operating) || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || (!Dm.truthy(can_open_with_hands) && Dm.truthy(density)))
            return null;
        if (ismob(AM))
        {
            mob B = AM as mob;
            if ((isdrone(B) || iscyborg(B)) && IS_UNCONSCIOUS_OR_CRIT(B))
                return null;
            if (isliving(AM))
            {
                mob_living M = AM as mob_living;
                //Can bump-open maybe 3 airlocks per second. This is to prevent weird mass door openings
                //While keeping things feeling snappy
                if (world.time - M.last_bumped <= 0.3f * SECONDS)
                    return null;
                M.last_bumped = world.time;
                if (HAS_TRAIT(M, TRAIT_HANDS_BLOCKED) && !Dm.truthy(check_access(null)) && !Dm.truthy(emergency))
                    return null;
                if (Dm.truthy(await try_safety_unlock(M)))
                    return null;
                await bumpopen(M);
                return null;
            }
            return null;
        }

        if (isitem(AM))
        {
            obj_item I = AM as obj_item;
            if (!Dm.truthy(density) || (I.w_class < WEIGHT_CLASS_NORMAL && !Dm.truthy(LAZYLEN(I.GetAccess()))))
                return null;
            if (Dm.truthy(requiresID()) && Dm.truthy(check_access(I)))
                await open();
            else if (Dm.truthy(unrestricted_side(I)) && !Dm.truthy(delayed_unres_open))
                await open();
            else
                await run_animation(DOOR_DENY_ANIMATION);
            return null;
        }
        return null;
    }

    // tg: code/game/machinery/doors/door.dm:289
    [DmParams()]
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = null;
        turf T = loc as turf;
        dot = await Move__d0(newloc, direction, glide_size_override, update_dir);
        if (Dm.truthy(density)) //Gotta be closed my friend
            move_update_air(T);
        return dot;
    }

    // tg: code/game/machinery/doors/door.dm:295
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(dot))
            return dot;
        // Snowflake handling for PASSGLASS.
        if (mover is atom_movable && Dm.truthy(Dm.bitand(mover.pass_flags, PASSGLASS)))
            return Dm.not(opacity);
        return dot;
    }

    // tg: code/game/machinery/doors/door.dm:303
    [DmParams("user")]
    public virtual async DmTask bumpopen(mob user = null, params object[] __extra)
    {
        await try_to_activate_door(user, access_bypass: FALSE, bumped: TRUE);
    }

    // tg: code/game/machinery/doors/door.dm:306
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        // Stops people without +USE from being able to click-open airlocks
        // Explicitly not a generic check - if you make this generic, AIs (and more) won't be able to open doors
        if (user is mob_living living_user)
        {
            if (!Dm.truthy(Dm.bitand(living_user.mobility_flags, MOBILITY_USE)))
                return dot;
        }
        if (Dm.truthy(await try_remove_seal(user)))
            return dot;
        if (Dm.truthy(await try_safety_unlock(user)))
            return dot;
        return await try_to_activate_door(user);
    }

    // tg: code/game/machinery/doors/door.dm:322  /obj/machinery/door/attack_tk: telekinesis (M8), not declared.

    // tg: code/game/machinery/doors/door.dm:327
    public override async DmTask<object> allowed__proc(mob accessor = null)
    {
        return (await base.allowed__proc(accessor) is var or__1 && Dm.truthy(or__1) ? or__1 : emergency);
    }

    /// A mob is trying to open or close the door
    // tg: code/game/machinery/doors/door.dm:331
    public virtual async DmTask<object> try_to_activate_door(mob user = null, float? access_bypass = null, float? bumped = null)
    {
        access_bypass ??= FALSE;
        bumped ??= FALSE;
        // tg: code/game/machinery/doors/door.dm:332
        add_fingerprint(user);
        if (Dm.truthy(operating) || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;

        if (!Dm.truthy(bumped) && !Dm.truthy(can_open_with_hands))
            return FALSE;

        if (Dm.truthy(elevator_mode) && !Dm.eq(elevator_status, LIFT_PLATFORM_UNLOCKED))
            return FALSE;

        // note: if the ID wire is cut no ID cards are checked at all! (This is intentional!)
        if (Dm.truthy(access_bypass) || (Dm.truthy(requiresID()) && Dm.truthy(await user_can_activate_door(user))))
        {
            if (Dm.truthy(density))
                await open(opener: user as mob_living);
            else
                await close();
            return TRUE;
        }

        if (!Dm.truthy(operating) && Dm.truthy(density))
            await run_animation(DOOR_DENY_ANIMATION);
        return FALSE;
    }

    /// Used in try_to_activate_door
    // tg: code/game/machinery/doors/door.dm:355
    [DmPrivate]
    public virtual async DmTask<object> user_can_activate_door(mob user = null)
    {
        if (Dm.truthy(await allowed__proc(user)))
            return TRUE;
        foreach (mob_living human_backpack in Dm.each<mob_living>(user.buckled_mobs))
        {
            if (Dm.truthy(await allowed__proc(human_backpack)))
                return TRUE;
        }
        if (Dm.truthy(unrestricted_side(user)))
            return (!Dm.truthy(delayed_unres_open) ? (object)TRUE : await attempt_delayed_unres_open(user));
        return FALSE;
    }

    /// Allows for specific side of airlocks to be unrestricted (IE, can exit maint freely, but need access to enter)
    // tg: code/game/machinery/doors/door.dm:367
    // Appendix A (M3 P3, decided): opener is object, not slots.tsv's mob: Bumped asks it of a thrown item (:283), a "-"
    // cell of rules 3.5.
    public virtual object unrestricted_side(object opener = null)
    {
        return Dm.bitand(get_dir(this, opener), unres_sides);
    }

    /// Initiates a do_after to open the door after a delay for unrestricted openers
    /// Returns TRUE if we successfully finished the do_after, FALSE otherwise
    // tg: code/game/machinery/doors/door.dm:372
    public virtual async DmTask<object> attempt_delayed_unres_open(mob opener = null)
    {
        if (Dm.gt(opener.do_after_count(), 0)) // not allowed to do this if you're doing something else. just wait lad.
            return FALSE;

        await stoplag(1); // allow the door to process any allow/deny responses first
        float do_after_time = rand(delayed_unres_time_lower, delayed_unres_time_upper);
        ADD_TRAIT(opener, TRAIT_UNRESTRICTED_AIRLOCK_OPENING, REF(this));
        RegisterSignal(opener, COMSIG_ATOM_PRE_PRESSURE_PUSH, nameof(stop_pressure_during_unres_open), @override: TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(deregister_pressure_push_signal), opener), do_after_time + 0.5f * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/game/machinery/doors/door.dm", line: 380); // extra half-second to be safe, else this is just a guarantee we remove the signal.

        // GAP(lead): SSblackbox is not declared (round statistics, M10).
        Dm.Deferred("code/game/machinery/doors/door.dm:382", "attempt_delayed_unres_open: SSblackbox.record_feedback(\"tally\", \"unrestricted_airlock_usage\", ...), round statistics (M10+)"); // statcollecting on how often people try to use this.
        balloon_alert(opener, I18n.T("activating unrestricted latch..."));

        if (get_area(this) is area_station_maintenance)
            playsound(get_turf(this), Dm.resource("sound/machines/airlock/airlock_latch_hiss.ogg"), 45, vary: TRUE, falloff_exponent: (SOUND_FALLOFF_EXPONENT * 2)); // sound travels further in maintenance muahaha
        else
            playsound(get_turf(this), Dm.resource("sound/machines/airlock/airlock_latch_hiss.ogg"), 30, vary: TRUE, extrarange: MEDIUM_RANGE_SOUND_EXTRARANGE, falloff_exponent: (SOUND_FALLOFF_EXPONENT * 1.5f));

        if (Dm.truthy(await do_after(opener, do_after_time, target: this)))
        {
            Dm.Deferred("code/game/machinery/doors/door.dm:391", "attempt_delayed_unres_open: SSblackbox.record_feedback of the success, round statistics (M10+)"); // no need to tally failures as we can assume it as long as we have this + the total
            return TRUE;
        }

        deregister_pressure_push_signal(opener); // if you fail the do_after early then you lose your pressure immunity, womp.
        return FALSE;
    }

    /// While activating the door, we are able to block pressure pushes since we're "grasping the override handle" or something similar to that.
    /// This basically exists to prevent the door's delay from being SUPREMELY annoying when you're trying to escape pressure-based damage during the unrestricted latch do_after.
    // tg: code/game/machinery/doors/door.dm:399
    [SignalHandler]
    public virtual object stop_pressure_during_unres_open(mob source = null)
    {
        if (QDELETED(source))
            return null;

        // COOLDOWN_FINISHED(src, pressure_push_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(pressure_push_cooldown <= world.time)) // avoid spam
            return COMSIG_ATOM_BLOCKS_PRESSURE;

        // have both since this is a newer mechanic and i want it to be a bit more obvious why for the time being
        balloon_alert(source, I18n.T("resisting pressure!"));
        to_chat(source, span_warning(I18n.T("You're holding onto the unrestricted latch, preventing pressure from pushing you away!")));
        // COOLDOWN_START(src, pressure_push_cooldown, 5 SECONDS) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        pressure_push_cooldown = world.time + (5 * SECONDS);
        return COMSIG_ATOM_BLOCKS_PRESSURE;
    }

    /// Exists to ensure that we always deregister the pressure push blocking signal. Can be called multiple times safely as we check the trait.
    // tg: code/game/machinery/doors/door.dm:414
    public virtual void deregister_pressure_push_signal(mob opener = null)
    {
        if (!HAS_TRAIT_FROM(opener, TRAIT_UNRESTRICTED_AIRLOCK_OPENING, REF(this)))
            return;

        UnregisterSignal(opener, COMSIG_ATOM_PRE_PRESSURE_PUSH);
        REMOVE_TRAIT(opener, TRAIT_UNRESTRICTED_AIRLOCK_OPENING, REF(this));
    }


    // tg: code/game/machinery/doors/door.dm:422
    public virtual async DmTask try_to_weld(obj_item_weldingtool W = null, mob user = null)
    {
        return;
    }

    /// Called when the user right-clicks on the door with a welding tool.
    // tg: code/game/machinery/doors/door.dm:426
    public virtual async DmTask try_to_weld_secondary(obj_item_weldingtool tool = null, mob user = null)
    {
        return;
    }


    // tg: code/game/machinery/doors/door.dm:430
    public virtual async DmTask try_to_crowbar(obj_item acting_object = null, mob user = null, float? forced = null)
    {
        forced ??= FALSE;
        return;
    }

    /// Called when the user right-clicks on the door with a crowbar.
    // tg: code/game/machinery/doors/door.dm:434 (an earlier duplicate definition in the same type: __d0, rules 2.5)
    public async DmTask try_to_crowbar_secondary__d0(obj_item acting_object = null, mob user = null)
    {
        return;
    }

    // tg: code/game/machinery/doors/door.dm:437
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        await try_to_weld(tool as obj_item_weldingtool, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/door.dm:441
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        float forced_open = FALSE;
        if (istype(tool, typeof(obj_item_crowbar)))
        {
            obj_item_crowbar crowbar = tool as obj_item_crowbar;
            forced_open = crowbar.force_opens;
        }
        await try_to_crowbar(tool, user, forced_open);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/door.dm:449
    public virtual async DmTask try_to_crowbar_secondary(obj_item acting_object = null, mob user = null)
    {
        await try_to_crowbar(null, user, FALSE);
    }

    // tg: code/game/machinery/doors/door.dm:452
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_access_key)))
        {
            obj_item_access_key key = (obj_item_access_key)tool;
            if (!Dm.truthy(await key.attempt_open_door(user, this)))
                return ITEM_INTERACT_BLOCKING;

            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(user.combat_mode) && istype(tool, typeof(obj_item_fireaxe)))
        {
            await try_to_crowbar(tool, user, FALSE);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(Dm.bitand(tool.item_flags, NOBLUDGEON)) || Dm.truthy(user.combat_mode))
            return NONE;

        if (istype(tool, typeof(obj_item_stack_sheet_mineral_wood)))
            return NONE; // we need this so our can_barricade element can be called using COMSIG_ATOM_ATTACKBY

        if (Dm.truthy(await try_to_activate_door(user)))
            return ITEM_INTERACT_SUCCESS;

        return NONE;
    }

    // tg: code/game/machinery/doors/door.dm:475
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        // allows you to crowbar doors while in combat mode
        if (Dm.truthy(user.combat_mode) && Dm.eq(tool.tool_behaviour, TOOL_CROWBAR))
            return await crowbar_act_secondary(user, tool);
        return await base.item_interaction_secondary(user, tool, modifiers);
    }

    // tg: code/game/machinery/doors/door.dm:481
    public override async DmTask<object> welder_act_secondary(mob_living user = null, obj_item tool = null)
    {
        await try_to_weld_secondary(tool as obj_item_weldingtool, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/door.dm:485
    public override async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
    {
        float forced_open = FALSE;
        if (istype(tool, typeof(obj_item_crowbar)))
        {
            obj_item_crowbar crowbar = tool as obj_item_crowbar;
            forced_open = crowbar.force_opens;
        }
        // tg passes forced_open as a third argument, which try_to_crowbar_secondary (door.dm:449) does not take (DM drops it)
        await try_to_crowbar_secondary(tool, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/door.dm:493
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_flag ??= 0;
        sound_effect ??= 1;
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.truthy(dot) && Dm.num(atom_integrity) > 0)
        {
            if (Dm.num(damage_amount) >= 10 && prob(30))
                await spark_system.start();
        }
        return dot;
    }

    // tg: code/game/machinery/doors/door.dm:499
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
        {
            if (Dm.truthy(glass))
                playsound(loc, Dm.resource("sound/effects/glass/glasshit.ogg"), 90, TRUE);
            else if (Dm.truthy(damage_amount))
                playsound(loc, Dm.resource("sound/items/weapons/smash.ogg"), 50, TRUE);
            else
                playsound(this, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
        }
        else if (Dm.eq(sw__1, BURN))
            playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/game/machinery/doors/door.dm:511
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (prob(20 / Dm.num(severity)) && (istype(this, typeof(obj_machinery_door_airlock)) || istype(this, typeof(obj_machinery_door_window))))
            Waiting.Detach(open()); // INVOKE_ASYNC(src, PROC_REF(open))
        return dot;
    }

    // tg: code/game/machinery/doors/door.dm:518
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
        {
            if (Dm.truthy(panel_open))
                icon_state = Dm.text("o_[]_opening", base_icon_state);
            else
                icon_state = Dm.text("[]_opening", base_icon_state);
        }
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
        {
            if (Dm.truthy(panel_open))
                icon_state = Dm.text("o_[]_closing", base_icon_state);
            else
                icon_state = Dm.text("[]_closing", base_icon_state);
        }
        else if (Dm.eq(sw__1, DOOR_DENY_ANIMATION))
        {
            if (!Dm.truthy(machine_stat))
                icon_state = Dm.text("[]_deny", base_icon_state);
        }
        else
            icon_state = Dm.text("[]_[]", base_icon_state, Dm.truthy(density) ? "closed" : "open");
        return dot;
    }

    // tg: code/game/machinery/doors/door.dm:537
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, mutable_appearance(icon, "panel_open"));
        return dot;
    }

    /// Returns the delay to use for the passed in animation
    /// We'll do our cleanup once the delay runs out
    // tg: code/game/machinery/doors/door.dm:544
    public virtual object animation_length(object animation = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
            return 0.6f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
            return 0.6f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_DENY_ANIMATION))
            return 0.3f * SECONDS;
        return null;
    }

    /// Returns the time required to hit particular points in an animation
    /// Used to manage delays for opening/closing and such
    // tg: code/game/machinery/doors/door.dm:555
    public virtual object animation_segment_delay(object animation = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_PASSABLE))
            return 0.5f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_OPENING_FINISHED))
            return 0.6f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_UNPASSABLE))
            return 0.2f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_FINISHED))
            return 0.6f * SECONDS;
        return null;
    }

    /// Override this to do misc tasks on animation start
    // tg: code/game/machinery/doors/door.dm:567
    public virtual async DmTask animation_effects(object animation = null, float? force_type = null)
    {
        force_type ??= DEFAULT_DOOR_CHECKS;
        return;
    }

    /// Used to start a new animation
    /// Accepts the animation to start as an arg
    // tg: code/game/machinery/doors/door.dm:572
    public virtual async DmTask run_animation(object animation = null, float? force_type = null)
    {
        force_type ??= DEFAULT_DOOR_CHECKS;
        set_animation(animation);
        _addtimer(Dm.@new<datum_callback>(this, nameof(set_animation), null), Dm.num_or_null(animation_length(animation)), Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/game/machinery/doors/door.dm", line: 574);
        await animation_effects(animation, force_type);
    }

    // React to our animation changing
    // tg: code/game/machinery/doors/door.dm:578
    public virtual void set_animation(object animation = null)
    {
        this.animation = animation;
        update_appearance();
    }

    /// Public proc that simply handles opening the door. Returns TRUE if the door was opened, FALSE otherwise.
    /// Use argument "forced" in conjunction with try_to_force_door_open if you want/need additional checks depending on how sorely you need the door opened.
    // tg: code/game/machinery/doors/door.dm:584
    public virtual async DmTask<object> open(float? forced = null, mob_living opener = null)
    {
        forced ??= DEFAULT_DOOR_CHECKS;
        if (!Dm.truthy(density))
            return TRUE;
        if (Dm.truthy(operating))
            return FALSE;

        if (Dm.truthy(opener))
        {
            if (Dm.truthy(opener._listen_lookup?[COMSIG_MOB_OPENED_DOOR]))
                opener._SendSignal(COMSIG_MOB_OPENED_DOOR, Dm.list(opener, forced));
        }

        operating = TRUE;
        await use_energy(active_power_usage);
        await run_animation(DOOR_OPENING_ANIMATION);
        await set_opacity(FALSE);
        object passable_delay = animation_segment_delay(DOOR_OPENING_PASSABLE);
        // SLEEP_NOT_DEL(passable_delay) (__DEFINES/_helpers.dm:35), inline (rules 11.1 (c))
        if (QDELETED(this))
            return null;
        await sleep(Dm.num(passable_delay));
        set_density(FALSE);
        Dm.mask(ref flags_1, Dm.bitnot(PREVENT_CLICK_UNDER_1));
        object open_delay = Dm.sub(animation_segment_delay(DOOR_OPENING_FINISHED), passable_delay);
        // SLEEP_NOT_DEL(open_delay) (__DEFINES/_helpers.dm:35), inline (rules 11.1 (c))
        if (QDELETED(this))
            return null;
        await sleep(Dm.num(open_delay));
        layer = Dm.initial(this, (obj_machinery_door p) => p.layer);
        update_appearance();
        await set_opacity(FALSE);
        operating = FALSE;
        air_update_turf(TRUE, FALSE);
        if (Dm.truthy(autoclose))
            autoclose_in(DOOR_CLOSE_WAIT);
        return TRUE;
    }

    /// Private proc that runs a series of checks to see if we should forcibly open the door. Returns TRUE if we should open the door, FALSE otherwise. Implemented in child types.
    /// In case a specific behavior isn't covered, we should default to TRUE just to be safe (simply put, this proc should have an explicit reason to return FALSE).
    // tg: code/game/machinery/doors/door.dm:614
    public virtual object try_to_force_door_open(float? force_type = null)
    {
        force_type ??= DEFAULT_DOOR_CHECKS;
        return TRUE; // the base "door" can always be forced open since there's no power or anything like emagging it to prevent an open, not even invoked on the base type anyways.
    }

    /// Public proc that simply handles closing the door. Returns TRUE if the door was closed, FALSE otherwise.
    /// Use argument "forced" in conjuction with try_to_force_door_shut if you want/need additional checks depending on how sorely you need the door closed.
    // tg: code/game/machinery/doors/door.dm:619
    [DmParams("forced")]
    public virtual async DmTask<object> close(float? forced = null, params object[] __extra)
    {
        forced ??= DEFAULT_DOOR_CHECKS;
        if (Dm.truthy(density))
            return TRUE;
        if (Dm.truthy(operating) || Dm.truthy(welded))
            return FALSE;
        if (Dm.truthy(safe))
        {
            foreach (atom_movable M in Dm.each<atom_movable>(get_turf(this)))
            {
                if (Dm.truthy(M.density) && M != this) //something is blocking the door
                {
                    if (Dm.truthy(autoclose))
                        autoclose_in(DOOR_CLOSE_WAIT);
                    return FALSE;
                }
            }
        }

        operating = TRUE;

        await run_animation(DOOR_CLOSING_ANIMATION);
        layer = closingLayer;
        object unpassable_delay = animation_segment_delay(DOOR_CLOSING_UNPASSABLE);
        // SLEEP_NOT_DEL(unpassable_delay) (__DEFINES/_helpers.dm:35), inline (rules 11.1 (c))
        if (QDELETED(this))
            return null;
        await sleep(Dm.num(unpassable_delay));
        set_density(TRUE);
        Dm.combine(ref flags_1, PREVENT_CLICK_UNDER_1);
        object close_delay = Dm.sub(animation_segment_delay(DOOR_CLOSING_FINISHED), unpassable_delay);
        // SLEEP_NOT_DEL(close_delay) (__DEFINES/_helpers.dm:35), inline (rules 11.1 (c))
        if (QDELETED(this))
            return null;
        await sleep(Dm.num(close_delay));
        update_appearance();
        if (Dm.truthy(visible) && !Dm.truthy(glass))
            await set_opacity(TRUE);
        operating = FALSE;
        air_update_turf(TRUE, TRUE);

        if (!Dm.truthy(can_crush))
            return TRUE;

        if (Dm.truthy(safe))
            await CheckForMobs();
        else
            await crush();
        return TRUE;
    }

    /// Private proc that runs a series of checks to see if we should forcibly shut the door. Returns TRUE if we should shut the door, FALSE otherwise. Implemented in child types.
    /// In case a specific behavior isn't covered, we should default to TRUE just to be safe (simply put, this proc should have an explicit reason to return FALSE).
    // tg: code/game/machinery/doors/door.dm:658
    public virtual object try_to_force_door_shut(float? force_type = null)
    {
        force_type ??= DEFAULT_DOOR_CHECKS;
        return TRUE; // the base "door" can always be forced shut
    }

    // tg: code/game/machinery/doors/door.dm:661
    public virtual async DmTask CheckForMobs()
    {
        if (Dm.truthy(Dm.locate<mob_living>(get_turf(this))))
        {
            await sleep(0.1f * SECONDS);
            await open();
        }
    }

    // tg: code/game/machinery/doors/door.dm:666
    public virtual async DmTask crush()
    {
        foreach (turf checked_turf in Dm.each<turf>(locs))
        {
            foreach (mob_living future_pancake in Dm.each<mob_living>(checked_turf))
            {
                future_pancake.visible_message(span_warning(I18n.T("[] closes on [], crushing []!", this, future_pancake, future_pancake.p_them())), span_userdanger(I18n.T("[] closes on you and crushes you!", this)));
                object sig_return = !Dm.truthy(future_pancake._listen_lookup?[COMSIG_LIVING_DOORCRUSHED]) ? NONE : future_pancake._SendSignal(COMSIG_LIVING_DOORCRUSHED, Dm.list(future_pancake, this)); // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:5)
                await future_pancake.add_splatter_floor(loc as turf);
                log_combat(this, future_pancake, "crushed");
                float door_wounding = Dm.truthy(Dm.bitand(sig_return, DOORCRUSH_NO_WOUND)) ? CANT_WOUND : 10;
                if (isalien(future_pancake))  //For xenos
                {
                    await future_pancake.apply_damage(DOOR_CRUSH_DAMAGE * 1.5f, BRUTE, BODY_ZONE_CHEST, wound_bonus: door_wounding, attacking_item: this); //Xenos go into crit after aproximately the same amount of crushes as humans.
                    await future_pancake.emote("roar");
                }
                else if (ishuman(future_pancake)) //For humans
                {
                    await future_pancake.emote(HAS_TRAIT(future_pancake, TRAIT_SIMIAN) ? "screech" : "scream");
                    await future_pancake.apply_damage(DOOR_CRUSH_DAMAGE, BRUTE, BODY_ZONE_CHEST, wound_bonus: door_wounding, attacking_item: this);
                    await future_pancake.Paralyze(10 * SECONDS);
                }
                else //for simple_animals & borgs
                {
                    await future_pancake.apply_damage(DOOR_CRUSH_DAMAGE, BRUTE, BODY_ZONE_CHEST, wound_bonus: door_wounding, attacking_item: this);
                }
            }
            foreach (obj_vehicle_sealed_mecha mech in Dm.each<obj_vehicle_sealed_mecha>(checked_turf)) // Your fancy metal won't save you here!
            {
                Dm.NotTranslated("code/game/machinery/doors/door.dm:682", "crush: an unsafe door closing on a mecha (mech.take_damage(DOOR_CRUSH_DAMAGE), log_combat; mechs, M5)");
            }
        }
    }

    // tg: code/game/machinery/doors/door.dm:687
    public virtual async DmTask autoclose__proc()
    {
        if (!QDELETED(this) && !Dm.truthy(density) && !Dm.truthy(operating) && !Dm.truthy(locked) && !Dm.truthy(welded) && Dm.truthy(autoclose))
            await close();
    }

    // tg: code/game/machinery/doors/door.dm:691
    public virtual void autoclose_in(object wait = null)
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(autoclose__proc)), Dm.num_or_null(wait), Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_NO_HASH_WAIT), TIMER_OVERRIDE), file: "code/game/machinery/doors/door.dm", line: 692);
    }

    // tg: code/game/machinery/doors/door.dm:694
    public virtual object requiresID()
    {
        return 1;
    }

    // tg: code/game/machinery/doors/door.dm:697
    public virtual object hasPower()
    {
        return Dm.not(Dm.bitand(machine_stat, NOPOWER));
    }

    // tg: code/game/machinery/doors/door.dm:700
    public override object block_superconductivity() // All non-glass airlocks block heat, this is intended.
    {
        if (Dm.truthy(opacity) || Dm.truthy(heat_proof))
            return 1;
        return 0;
    }
}

// tg: code/game/machinery/doors/door.dm:708
public partial class obj_machinery_door_morgue : obj_machinery_door
{
    // tg: code/game/machinery/doors/door.dm:708
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/machinery/doors/door.dm:710", "Initialize: AddComponent(/datum/component/redirect_attack_hand_from_turf, interact_check = CALLBACK(src, PROC_REF(drag_check))): clicking the floor below (clicks, M4a)");
        return dot;
    }

    // if dragging, block redirect_Attack_hand_from_turf
    // tg: code/game/machinery/doors/door.dm:713
    public virtual object drag_check(mob user = null)
    {
        if (Dm.truthy(user.pulling))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/doors/door.dm:721
    public override object animation_length(object animation = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
            return 1.5f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
            return 1.5f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_DENY_ANIMATION))
            return 0.1f * SECONDS;
        return null;
    }

    // tg: code/game/machinery/doors/door.dm:730
    public override object animation_segment_delay(object animation = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_PASSABLE))
            return 1.4f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_OPENING_FINISHED))
            return 1.5f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_UNPASSABLE))
            return 0.2f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_FINISHED))
            return 1.5f * SECONDS;
        return null;
    }
}

// tg: code/game/machinery/doors/door.dm:718
public partial class obj_machinery_door : obj_machinery
{
    // tg: code/game/machinery/doors/door.dm:718
    public override object get_dumping_location()
    {
        return null;
    }
}

// tg: code/game/machinery/doors/door.dm:741
public partial class obj_machinery_door : obj_machinery
{
    // tg: code/game/machinery/doors/door.dm:741
    public virtual void @lock()
    {
        return;
    }

    // tg: code/game/machinery/doors/door.dm:744
    public virtual void unlock()
    {
        return;
    }

    // tg: code/game/machinery/doors/door.dm:747
    public virtual async DmTask hostile_lockdown(mob origin = null)
    {
        if (!Dm.truthy(machine_stat)) //So that only powered doors are closed.
            await close(); //Close ALL the doors!
    }

    // tg: code/game/machinery/doors/door.dm:751
    public virtual async DmTask disable_lockdown()
    {
        if (!Dm.truthy(machine_stat)) //Opens only powered doors.
            await open(); //Open everything!
    }

    // tg: code/game/machinery/doors/door.dm:755  (set waitfor = FALSE, inherited; it sleeps only through ..(), so no wrapper of
    // its own, waiting-model.md 5.4)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        //if it blows up a wall it should blow up a door
        return base.ex_act(Dm.truthy(severity) ? min(EXPLODE_DEVASTATE, Dm.num(severity) + 1) : EXPLODE_NONE, target, __extra);
    }

    // tg: code/game/machinery/doors/door.dm:759
    public override object power_change()
    {
        object dot = null;
        dot = base.power_change();
        if (Dm.truthy(dot) && !Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            autoclose_in(DOOR_CLOSE_WAIT);
        return dot;
    }

    // tg: code/game/machinery/doors/door.dm:764  (the M7 zaps slice)
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        object dot = null;
        zap_flags = Dm.bitand(zap_flags, Dm.bitnot(ZAP_OBJ_DAMAGE));
        dot = await base.zap_act(power, zap_flags);
        return dot;
    }

    /// Signal proc for [COMSIG_ATOM_MAGICALLY_UNLOCKED]. Open up when someone casts knock.
    // tg: code/game/machinery/doors/door.dm:769
    [SignalHandler]
    public virtual void on_magic_unlock(datum source = null, datum_action_cooldown_spell_aoe_knock spell = null, atom caster = null)
    {
        Waiting.Detach(open());
    }

    // tg: code/game/machinery/doors/door.dm:774
    public override object set_density(object new_value = null)
    {
        object dot = null;
        dot = base.set_density(new_value);
        update_explosive_block();
        return dot;
    }

    // tg: code/game/machinery/doors/door.dm:778
    public virtual void update_explosive_block()
    {
        set_explosion_block(real_explosion_block);
    }

    // Kinda roundabout, essentially if we're dense, we respect real_explosion_block
    // Otherwise, we block nothing
    // tg: code/game/machinery/doors/door.dm:783
    public override void set_explosion_block(object explosion_block = null)
    {
        real_explosion_block = explosion_block;
        if (Dm.truthy(density))
        {
            base.set_explosion_block(explosion_block);
            return;
        }
        base.set_explosion_block(0);
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/doors/door.dm:138  var/static/list/loc_connections = list(...)
    internal DmList obj_machinery_door__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_MAGICALLY_UNLOCKED, nameof(obj_machinery_door.on_magic_unlock)));
}
