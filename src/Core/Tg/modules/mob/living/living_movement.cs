// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/living_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M2 P1 (the living core): the file is translated except zMove of living mobs (:117-129, multi-z and
// riding, later milestones: their /atom/movable roots run until then) and /mob/set_currently_z_moving (:155, buckled
// z-moves).

// tg: code/modules/mob/living/living_movement.dm:1
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_movement.dm:1
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = null;
        dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        await update_turf_movespeed(loc as turf_open);
        if (HAS_TRAIT(this, TRAIT_NEGATES_GRAVITY))
        {
            if (!isgroundlessturf(loc))
                ADD_TRAIT(this, TRAIT_IGNORING_GRAVITY, IGNORING_GRAVITY_NEGATION);
            else
                REMOVE_TRAIT(this, TRAIT_IGNORING_GRAVITY, IGNORING_GRAVITY_NEGATION);
        }

        turf old_turf = get_turf(old_loc);
        turf new_turf = get_turf(this);
        // If we're moving to/from nullspace, refresh
        // Easier then adding nullchecks to all this shit, and technically right since a null turf means nograv
        if (old_turf == null || new_turf == null)
        {
            if (!QDELING(this))
                await refresh_gravity();
            return dot;
        }
        // If the turf gravity has changed, then it's possible that our state has changed, so update
        if (HAS_TRAIT(old_turf, TRAIT_FORCED_GRAVITY) != HAS_TRAIT(new_turf, TRAIT_FORCED_GRAVITY) || new_turf.force_no_gravity != old_turf.force_no_gravity)
            await refresh_gravity();

        // Going to do area gravity checking here
        area old_area = old_turf.loc as area;
        area new_area = new_turf.loc as area;
        // If the area gravity has changed, then it's possible that our state has changed, so update
        // DEVIATION(DV185): a change of the area's NO_GRAVITY flag refreshes too (tg skips it: a floor of nearstation or solars next to an
        // airlock kept the weightless state inside the station).
        if (old_area.default_gravity != new_area.default_gravity
            || Dm.truthy(Dm.bitand(old_area.area_flags, NO_GRAVITY)) != Dm.truthy(Dm.bitand(new_area.area_flags, NO_GRAVITY)))
            await refresh_gravity();
        return dot;
    }

    // tg: code/modules/mob/living/living_movement.dm:29  (the second definition of on_changed_z_level on /mob/living; the
    // first, living.dm:2013, is on_changed_z_level__d0, rules 7.5)
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await on_changed_z_level__d0(old_turf, new_turf, same_z_layer, notify_contents);

        if (!Dm.truthy(old_turf) || !Dm.truthy(new_turf) || !Dm.eq(SSmapping.gravity_by_z_level[old_turf.z], SSmapping.gravity_by_z_level[new_turf.z]))
            await refresh_gravity();
    }

    /// Living Mob use event based gravity
    /// We check here to ensure we haven't dropped any gravity changes
    // tg: code/modules/mob/living/living_movement.dm:37
    public virtual async DmTask gravity_setup()
    {
        on_negate_gravity(this);
        await refresh_gravity();
    }

    /// Handles gravity effects. Call if something about our gravity has potentially changed!
    // tg: code/modules/mob/living/living_movement.dm:42
    public virtual async DmTask refresh_gravity()
    {
        object old_grav_state = gravity_state;
        gravity_state = has_gravity();
        if (Dm.eq(gravity_state, old_grav_state))
            return;

        await update_gravity(gravity_state);
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_GRAVITY_CHANGED]))
            _SendSignal(COMSIG_LIVING_GRAVITY_CHANGED, Dm.list(this, gravity_state, old_grav_state));
        if (Dm.gt(gravity_state, STANDARD_GRAVITY))
            gravity_animate();
        else if (Dm.gt(old_grav_state, STANDARD_GRAVITY))
            remove_filter("gravity");
    }

    // tg: code/modules/mob/living/living_movement.dm:55
    public override object mob_negates_gravity()
    {
        return Dm.num(HAS_TRAIT_FROM(this, TRAIT_IGNORING_GRAVITY, IGNORING_GRAVITY_NEGATION));
    }

    // tg: code/modules/mob/living/living_movement.dm:58
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(mover.throwing))
        {
            // tg: :63 (filled by M4a P2 with the throwing state, as the lead allowed)
            mob thrower = mover.throwing.get_thrower() as mob;
            return Dm.num(!Dm.truthy(density) || (body_position == LYING_DOWN) || (thrower == this && !ismob(mover)));
        }
        if (buckled == mover)
            return TRUE;
        if (ismob(mover) && (Dm.@in(mover, buckled_mobs)))
            return TRUE;
        return Dm.num(!Dm.truthy(mover.density) || body_position == LYING_DOWN);
    }

    // tg: code/modules/mob/living/living_movement.dm:71
    public override async DmTask update_config_movespeed()
    {
        await update_move_intent_slowdown();
        await base.update_config_movespeed();
    }

    // tg: code/modules/mob/living/living_movement.dm:75
    public virtual async DmTask update_move_intent_slowdown()
    {
        await add_movespeed_modifier(get_move_intent_slowdown());
    }

    // tg: code/modules/mob/living/living_movement.dm:78
    public virtual object get_move_intent_slowdown()
    {
        if (move_intent == MOVE_INTENT_WALK)
            return typeof(datum_movespeed_modifier_config_walk_run_walk);
        return typeof(datum_movespeed_modifier_config_walk_run_run);
    }

    // tg: code/modules/mob/living/living_movement.dm:83
    public virtual async DmTask update_turf_movespeed(turf_open turf = null)
    {
        if (isopenturf(turf) && !HAS_TRAIT(turf, TRAIT_TURF_IGNORE_SLOWDOWN))
        {
            if (turf.slowdown != current_turf_slowdown)
            {
                await add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_turf_slowdown), multiplicative_slowdown: turf.slowdown);
                current_turf_slowdown = turf.slowdown;
            }
        }
        else if (Dm.truthy(current_turf_slowdown))
        {
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_turf_slowdown));
            current_turf_slowdown = 0;
        }
    }

    // tg: code/modules/mob/living/living_movement.dm:92
    public virtual async DmTask update_pull_movespeed()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_UPDATING_PULL_MOVESPEED]))
            _SendSignal(COMSIG_LIVING_UPDATING_PULL_MOVESPEED, Dm.list(this));

        if (Dm.truthy(pulling))
        {
            if (isliving(pulling))
            {
                mob_living L = pulling as mob_living;
                if (!Dm.truthy(slowed_by_drag) || L.body_position == STANDING_UP || Dm.truthy(L.buckled) || grab_state >= GRAB_AGGRESSIVE)
                {
                    remove_movespeed_modifier(typeof(datum_movespeed_modifier_bulky_drag));
                    return;
                }
                await add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_bulky_drag), multiplicative_slowdown: PULL_PRONE_SLOWDOWN);
                return;
            }
            if (isobj(pulling))
            {
                // tg: :104  var/obj/structure/S = pulling  (DM does not check the declared type; drag_slowdown is an /obj var,
                // objs.dm:37, so the C# local is an obj and any pulled object reads it, as in BYOND)
                obj S = pulling as obj;
                if (!Dm.truthy(slowed_by_drag) || !Dm.truthy(S.drag_slowdown))
                {
                    remove_movespeed_modifier(typeof(datum_movespeed_modifier_bulky_drag));
                    return;
                }
                await add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_bulky_drag), multiplicative_slowdown: S.drag_slowdown);
                return;
            }
        }
        remove_movespeed_modifier(typeof(datum_movespeed_modifier_bulky_drag));
    }

    // tg: code/modules/mob/living/living_movement.dm:131
    public override object can_z_move(object direction = null, turf start = null, turf destination = null, float? z_move_flags = null, mob_living rider = null)
    {
        float flags = z_move_flags ?? ZMOVE_FLIGHT_FLAGS;
        if (Dm.truthy(Dm.bitand(flags, ZMOVE_INCAPACITATED_CHECKS)) && Dm.truthy(incapacitated))
        {
            if (Dm.truthy(Dm.bitand(flags, ZMOVE_FEEDBACK)))
                to_chat(rider ?? (object)this, span_warning(Dm.text("[] can't do that right now!", rider != null ? this : "You")));
            return FALSE;
        }
        if (!Dm.truthy(buckled) || !Dm.truthy(Dm.bitand(flags, ZMOVE_ALLOW_BUCKLED)))
        {
            if (!Dm.truthy(Dm.bitand(flags, ZMOVE_FALL_CHECKS)) && Dm.truthy(incorporeal_move) && (rider == null || Dm.truthy(rider.incorporeal_move)))
            {
                //An incorporeal mob will ignore obstacles unless it's a potential fall (it'd suck hard) or is carrying corporeal mobs.
                //Coupled with flying/floating, this allows the mob to move up and down freely.
                //By itself, it only allows the mob to move down.
                flags = Dm.bitor(flags, ZMOVE_IGNORE_OBSTACLES);
            }
            return base.can_z_move(direction, start, destination, flags, rider);
        }
        object answer = buckled.SEND_SIGNAL(COMSIG_BUCKLED_CAN_Z_MOVE, Dm.list(buckled, direction, start, destination, flags, this));
        if (Dm.eq(answer, COMPONENT_RIDDEN_ALLOW_Z_MOVE)) // Can be ridden.
            return buckled.can_z_move(direction, start, destination, flags, this);
        if (Dm.eq(answer, COMPONENT_RIDDEN_STOP_Z_MOVE)) // Is a ridable but can't be ridden right now. Feedback messages already done.
            return FALSE;
        if (!Dm.truthy(Dm.bitand(flags, ZMOVE_CAN_FLY_CHECKS)) && !Dm.truthy(buckled.anchored))
            return buckled.can_z_move(direction, start, destination, flags, this);
        if (Dm.truthy(Dm.bitand(flags, ZMOVE_FEEDBACK)))
            to_chat(this, span_warning(Dm.text("Unbuckle from [] first.", buckled)));
        return FALSE;
    }

    // tg: code/modules/mob/living/living_movement.dm:160
    public override async DmTask keybind_face_direction(object direction = null)
    {
        if (!Dm.truthy(Dm.bitand(mobility_flags, MOBILITY_MOVE)))
            return;
        await base.keybind_face_direction(direction);
    }
}
