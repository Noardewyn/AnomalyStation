// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/tram/tram_doors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the whole file.

// tg: code/modules/transport/tram/tram_doors.dm:4
public partial class obj_machinery_door_airlock_tram : obj_machinery_door_airlock
{
    /// Amount of travel distance to force open tram doors while moving
    // tg: code/modules/transport/tram/tram_doors.dm:2
    private const float TRAM_DOOR_RELEASE_THRESHOLD = 17;

    // tg: code/modules/transport/tram/tram_doors.dm:26 (the earlier of the type's two Initialize definitions, rules 2.5)
    public object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(id_tag))
            id_tag = assign_random_name();
        return dot;
    }

    // tg: code/modules/transport/tram/tram_doors.dm:175
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Initialize__d0(mapload, __extra);
        RemoveElement(typeof(datum_element_atmos_sensitive), mapload);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/transport/tram/tram_doors.dm:31
    public override async DmTask<object> open(float? forced = null, mob_living opener = null)
    {
        forced ??= DEFAULT_DOOR_CHECKS;
        if (Dm.truthy(welded) || Dm.truthy(locked) || Dm.truthy(seal))
            return FALSE;

        if (!Dm.truthy(density))
            return TRUE;

        if (forced == DEFAULT_DOOR_CHECKS && (Dm.truthy(operating) || !Dm.truthy(hasPower()) || Dm.truthy(wires.is_cut(WIRE_OPEN))))
            return FALSE;

        SEND_SIGNAL(COMSIG_AIRLOCK_OPEN, Dm.list(this, FALSE));
        if (opener != null)
            opener.SEND_SIGNAL(COMSIG_MOB_OPENED_AIRLOCK, Dm.list(opener, forced));
        float animate_open = forced == BYPASS_DOOR_CHECKS ? FALSE : TRUE;
        await set_airlock_state(AIRLOCK_OPENING, animate_open, force_type: forced);

        float passable_delay = Dm.num(animation_segment_delay(AIRLOCK_OPENING_PASSABLE));
        if (forced >= BYPASS_DOOR_CHECKS)
            passable_delay = 0;

        await sleep(passable_delay);
        set_density(FALSE);
        if (filler != null)
            filler.set_density(FALSE);
        Dm.mask(ref flags_1, Dm.bitnot(PREVENT_CLICK_UNDER_1));
        air_update_turf(TRUE, FALSE);
        float open_delay = forced == BYPASS_DOOR_CHECKS ? (0.2f * SECONDS) : (Dm.num(animation_segment_delay(AIRLOCK_OPENING_FINISHED)) - passable_delay);
        await sleep(open_delay);
        layer = OPEN_DOOR_LAYER;
        await set_airlock_state(AIRLOCK_OPEN, animated: FALSE);

        return TRUE;
    }

    // tg: code/modules/transport/tram/tram_doors.dm:63
    public override async DmTask<object> close(float? forced = null, params object[] __extra)
    {
        float? force_crush = Dm.arg<float?>(__extra, 0);
        forced ??= DEFAULT_DOOR_CHECKS;
        force_crush ??= FALSE;
        retry_counter = Dm.num(retry_counter) + 1;
        if (Dm.num(retry_counter) >= 3 || Dm.truthy(force_crush) || forced == BYPASS_DOOR_CHECKS)
        {
            await try_to_close(forced: BYPASS_DOOR_CHECKS);
            return null;
        }

        if (Dm.num(retry_counter) > 1)
            playsound(this, Dm.resource("sound/machines/tram/door_chime.ogg"), 40, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);

        _addtimer(Dm.@new<datum_callback>(this, nameof(verify_status)), 2.7f * SECONDS, file: "code/modules/transport/tram/tram_doors.dm", line: 72);
        await try_to_close();
        return null;
    }

    /**
     * Perform a close attempt and report TRUE/FALSE if it worked
     */
    // tg: code/modules/transport/tram/tram_doors.dm:81
    public virtual async DmTask<object> try_to_close(float? forced = null)
    {
        forced ??= DEFAULT_DOOR_CHECKS;
        if (Dm.truthy(operating) || Dm.truthy(welded) || Dm.truthy(locked) || Dm.truthy(seal))
            return FALSE;
        if (Dm.truthy(density))
            return TRUE;
        crushing_in_progress = TRUE;
        bool hungry_door = forced == BYPASS_DOOR_CHECKS || !Dm.truthy(safe);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || !Dm.truthy(safe))
        {
            do_sparks(3, TRUE, this);
            playsound(this, SFX_SPARKS, vol: 75, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        }
        await use_energy(50 * JOULES);
        layer = CLOSED_DOOR_LAYER;
        await set_airlock_state(AIRLOCK_CLOSING, animated: TRUE, force_type: forced);
        float unpassable_delay = Dm.num(animation_segment_delay(AIRLOCK_CLOSING_UNPASSABLE));
        await sleep(unpassable_delay);
        if (!hungry_door)
        {
            foreach (turf checked_turf in Dm.each<turf>(locs))
            {
                foreach (atom_movable blocker in Dm.each<atom_movable>(checked_turf))
                {
                    if (Dm.truthy(blocker.density) && blocker != this) //something is blocking the door
                    {
                        Waiting.Detach(say(I18n.T("Please stand clear of the doors!")));
                        playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 60, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
                        layer = OPEN_DOOR_LAYER;
                        await set_airlock_state(AIRLOCK_OPEN, animated: FALSE, force_type: forced);
                        return FALSE;
                    }
                }
            }
        }
        SEND_SIGNAL(COMSIG_AIRLOCK_CLOSE, Dm.list(this));
        float opaque_delay = Dm.num(animation_segment_delay(AIRLOCK_CLOSING_OPAQUE)) - unpassable_delay;
        await sleep(opaque_delay);
        set_density(TRUE);
        if (filler != null)
            filler.set_density(TRUE);
        Dm.combine(ref flags_1, PREVENT_CLICK_UNDER_1);
        air_update_turf(TRUE, TRUE);
        await crush();
        crushing_in_progress = FALSE;
        float close_delay = Dm.num(animation_segment_delay(AIRLOCK_CLOSING_FINISHED)) - unpassable_delay - opaque_delay;
        await sleep(close_delay);
        await set_airlock_state(AIRLOCK_CLOSED, animated: FALSE, force_type: forced);
        retry_counter = 0;
        return TRUE;
    }

    // tg: code/modules/transport/tram/tram_doors.dm:125
    public override object animation_length(object animation = null)
    {
        if (Dm.eq(animation, DOOR_OPENING_ANIMATION))
            return 1.8f * SECONDS;
        if (Dm.eq(animation, DOOR_CLOSING_ANIMATION))
            return 2.5f * SECONDS;
        return null;
    }

    // tg: code/modules/transport/tram/tram_doors.dm:132
    public override object animation_segment_delay(object animation = null)
    {
        if (Dm.eq(animation, AIRLOCK_OPENING_TRANSPARENT))
            return 0.9f * SECONDS;
        if (Dm.eq(animation, AIRLOCK_OPENING_PASSABLE))
            return 0.9f * SECONDS;
        if (Dm.eq(animation, AIRLOCK_OPENING_FINISHED))
            return 1.8f * SECONDS;
        if (Dm.eq(animation, AIRLOCK_CLOSING_UNPASSABLE))
            return 0.9f * SECONDS;
        if (Dm.eq(animation, AIRLOCK_CLOSING_OPAQUE))
            return 1.6f * SECONDS;
        if (Dm.eq(animation, AIRLOCK_CLOSING_FINISHED))
            return 2.5f * SECONDS;
        return null;
    }

    /**
     * Crush the jerk holding up the tram from moving
     *
     * Tram doors need their own crush proc because the normal one
     * leaves you stunned far too long, leading to the doors crushing
     * you over and over, no escape!
     */
    // tg: code/modules/transport/tram/tram_doors.dm:156
    public override async DmTask crush()
    {
        foreach (turf checked_turf in Dm.each<turf>(locs))
        {
            foreach (mob_living future_pancake in Dm.each<mob_living>(checked_turf))
            {
                future_pancake.visible_message(span_warning(I18n.T("[] beeps angrily and closes on []!", this, future_pancake)), span_userdanger(I18n.T("[] beeps angrily and closes on you!", this)));
                object sig_return = future_pancake.SEND_SIGNAL(COMSIG_LIVING_DOORCRUSHED, Dm.list(future_pancake, this));
                await future_pancake.add_splatter_floor(loc as turf);
                log_combat(this, future_pancake, "crushed");
                float door_wounding = Dm.truthy(Dm.bitand(sig_return, DOORCRUSH_NO_WOUND)) ? CANT_WOUND : 10;
                await future_pancake.apply_damage(DOOR_CRUSH_DAMAGE * 2, BRUTE, BODY_ZONE_CHEST, wound_bonus: door_wounding, attacking_item: this);
                await future_pancake.Paralyze(2 * SECONDS);
                if (ishuman(future_pancake))
                    Waiting.Detach(future_pancake.emote("scream"));
            }

            foreach (obj_vehicle_sealed_mecha mech in Dm.each<obj_vehicle_sealed_mecha>(checked_turf)) // Your fancy metal won't save you here!
            {
                await mech.take_damage(DOOR_CRUSH_DAMAGE);
                log_combat(this, mech, "crushed");
            }
        }
    }

    /**
     * Checks if the door close action was successful. Retries if it failed
     *
     * If some jerk is blocking the doors, they've had enough warning by attempt 3,
     * take a chunk of skin, people have places to be!
     */
    // tg: code/modules/transport/tram/tram_doors.dm:180
    public virtual async DmTask verify_status()
    {
        if (Dm.eq(airlock_state, AIRLOCK_CLOSED))
            return;

        if (Dm.num(retry_counter) < 2)
        {
            await close();
            return;
        }

        playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 60, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        Waiting.Detach(say(I18n.T("YOU'RE HOLDING UP THE TRAM, ASSHOLE!")));
        await close(forced: BYPASS_DOOR_CHECKS);
    }

    /**
     * Set the weakref for the tram we're attached to
     */
    // tg: code/modules/transport/tram/tram_doors.dm:194
    public virtual void find_tram()
    {
        foreach (datum_transport_controller_linear_tram tram in Dm.each<datum_transport_controller_linear_tram>(SStransport.transports_by_type[TRANSPORT_TYPE_TRAM]))
        {
            if (Dm.eq(tram.specific_transport_id, transport_linked_id))
                transport_ref = WEAKREF(tram) as datum_weakref;
        }
    }

    // tg: code/modules/transport/tram/tram_doors.dm:180
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        Waiting.Detach(open()); // INVOKE_ASYNC
        Dm.append(ref SStransport.doors, this);
        find_tram();
    }

    // tg: code/modules/transport/tram/tram_doors.dm:186
    public override object Destroy(float? force = null)
    {
        Dm.remove(SStransport.doors, this);
        return base.Destroy(force);
    }

    /**
     * Tram doors can be opened with hands when unpowered
     */
    // tg: code/modules/transport/tram/tram_doors.dm:193
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It has an emergency mechanism to open using [] in the event of an emergency.", EXAMINE_HINT("just your hands"))));
        return dot;
    }

    /**
     * Tram doors can be opened with hands when unpowered
     */
    // tg: code/modules/transport/tram/tram_doors.dm:200
    public override async DmTask<object> try_safety_unlock(mob user = null)
    {
        if (release_cooldown > world.time) // !COOLDOWN_FINISHED(src, release_cooldown)
            return null;

        if (!Dm.truthy(hasPower()) && Dm.truthy(density))
        {
            release_cooldown = world.time + 1.2f * SECONDS; // COOLDOWN_START(src, release_cooldown, 1.2 SECONDS)
            playsound(this, soundin: Dm.resource("sound/machines/airlock/airlockforced.ogg"), vol: 40, vary: FALSE);
            balloon_alert_to_viewers(I18n.T("pulling emergency exit!"), vision_distance: COMBAT_MESSAGE_RANGE);
            if (Dm.truthy(await do_after(user, 1.2f * SECONDS, target: this)))
                await open(forced: BYPASS_DOOR_CHECKS);
        }
        return null;
    }

    /**
     * If you pry (bump) the doors open midtravel, open quickly so you can jump out and make a daring escape.
     */
    // tg: code/modules/transport/tram/tram_doors.dm:214
    public override async DmTask bumpopen(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(operating) || !Dm.truthy(density))
            return;

        if (!Dm.truthy(hasPower()))
        {
            await try_safety_unlock(user);
            return;
        }

        if (release_cooldown > world.time) // !COOLDOWN_FINISHED(src, release_cooldown)
            return;

        datum_transport_controller_linear_tram tram_part = transport_ref?.resolve() as datum_transport_controller_linear_tram;
        add_fingerprint(user);
        if (!Dm.truthy(tram_part.controller_active))
            return;
        if ((tram_part.travel_remaining < TRAM_DOOR_RELEASE_THRESHOLD || tram_part.travel_remaining > tram_part.travel_trip_length - TRAM_DOOR_RELEASE_THRESHOLD) && Dm.truthy(tram_part.controller_active))
            return; // we're already animating, don't reset that
        release_cooldown = world.time + 1.2f * SECONDS; // COOLDOWN_START(src, release_cooldown, 1.2 SECONDS)
        playsound(this, soundin: Dm.resource("sound/machines/airlock/airlockforced.ogg"), vol: 40, vary: FALSE);
        balloon_alert_to_viewers(I18n.T("pulling emergency exit!"), vision_distance: COMBAT_MESSAGE_RANGE);
        if (Dm.truthy(await do_after(user, delay: 0.6f * SECONDS, timed_action_flags: IGNORE_USER_LOC_CHANGE | IGNORE_SLOWDOWNS)))
            await open(forced: BYPASS_DOOR_CHECKS);
    }

    // tg: code/modules/transport/tram/tram_doors.dm:236
    public override async DmTask animation_effects(object animation = null, float? force_type = null)
    {
        force_type ??= DEFAULT_DOOR_CHECKS;
        if (force_type == BYPASS_DOOR_CHECKS)
            return;

        if (Dm.eq(animation, DOOR_OPENING_ANIMATION))
        {
            await use_energy(50 * JOULES);
            playsound(this, soundin: doorOpen, vol: 40, vary: FALSE);
        }
        else if (Dm.eq(animation, DOOR_CLOSING_ANIMATION))
        {
            await use_energy(50 * JOULES);
            playsound(this, soundin: doorClose, vol: 40, vary: FALSE);
        }
        else if (Dm.eq(animation, DOOR_DENY_ANIMATION))
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(handle_deny_end)), 0.6f * SECONDS, file: "code/modules/transport/tram/tram_doors.dm", line: 249);
        }
    }
}
