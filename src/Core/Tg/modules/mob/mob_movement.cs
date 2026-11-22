// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/mob_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the /mob procs the Move chain reaches translated: Process_Spacemove, handle_spacemove_grabbing,
// get_spacemove_backup (M4b slice 1, the zero-gravity drift), has_gravity, mob_negates_gravity,
// abstract_move and Moved (wave 4, package C); client/Move (:46-150) whole and Process_Grab (M1 P1, the client layer;
// its resist_grab call since the 2026-10-13 movement fixes), with Process_Incorpmove (its basic kind since syndicate X2; the shadow and jaunt kinds,
// M2+); slip of /mob and /mob/living (M4b slice 3: wet floors). The drop_item verb, the other /mob/living procs and
// up/down (z movement by players) come with their mechanics.

// tg: code/modules/mob/mob_movement.dm:46 (interleaved types, rules 1.9)
// The [DmPath] of /client, a BYOND builtin type, is on its engine part Builtin/client.builtin.cs (rules 2.3).
public partial class client : datum
{
    /**
     * Move a client in a direction
     *
     * Huge proc, has a lot of functionality
     *
     * Mostly it will despatch to the mob that you are the owner of to actually move
     * in the physical realm
     *
     * Things that stop you moving as a mob:
     * * world time being less than your next move_delay
     * * not being in a mob, or that mob not having a loc
     * * missing the n and direction parameters
     * * being in remote control of an object (calls Moveobject instead)
     * * being dead (it ghosts you instead)
     *
     * Things that stop you moving as a mob living (why even have OO if you're just shoving it all
     * in the parent proc with istype checks right?):
     * * having incorporeal_move set (calls Process_Incorpmove() instead)
     * * being grabbed
     * * being buckled (relaymove() is called to the buckled atom instead)
     * * having your loc be some other mob (relaymove() is called on that mob instead)
     * * Not having MOBILITY_MOVE
     * * Failing Process_Spacemove() call
     *
     * At this point, if the mob is is confused, then a random direction and target turf will be calculated for you to travel to instead
     *
     * Now the parent call is made (to the byond builtin move), which moves you
     *
     * Some final move delay calculations (doubling if you moved diagonally successfully)
     *
     * if mob throwing is set I believe it's unset at this point via a call to finalize
     *
     * Finally if you're pulling an object and it's dense, you are turned 180 after the move
     * (if you ask me, this should be at the top of the move so you don't dance around)
     *
     */
    /// <summary>DEVIATION(DV54): a ghost's delay between tiles in deciseconds (10 tiles a second, half tg's every tick).</summary>
    public const float GhostMoveDelay = 1;

    // tg: code/modules/mob/mob_movement.dm:46
    public virtual async DmTask<object> Move(object new_loc = null, object direct = null, params object[] __extra)
    {
        object dot = null;
        if (world.time < move_delay) //do not move anything ahead of this check please
            return FALSE;
        next_move_dir_add = NONE;
        next_move_dir_sub = NONE;
        float old_move_delay = move_delay;
        move_delay = world.time + world.tick_lag; //this is here because Move() can now be called mutiple times per tick
        if (!Dm.truthy(direct) || !Dm.truthy(new_loc))
            return FALSE;
        if (!Dm.truthy(mob?.loc))
            return FALSE;
        if (HAS_TRAIT(mob, TRAIT_NO_TRANSFORM))
            return FALSE; //This is sorta the goto stop mobs from moving trait
        if (!isliving(mob))
        {
            if (Dm.truthy(Dm.bitand((!Dm.truthy(mob._listen_lookup?[COMSIG_MOB_CLIENT_PRE_NON_LIVING_MOVE]) ? (object)NONE : mob._SendSignal(COMSIG_MOB_CLIENT_PRE_NON_LIVING_MOVE, Dm.list(mob, new_loc, direct))), COMSIG_MOB_CLIENT_BLOCK_PRE_NON_LIVING_MOVE)))
                return FALSE;
            // DEVIATION(DV54): a ghost waits GhostMoveDelay between tiles (tg moves it every tick, 20 tiles a second, too fast
            // for the 3D overview), counted from the last move as tg counts a living mob's delay below, sqrt(2) times longer on
            // a diagonal, and glides over that delay (tg's ghost glides a whole tile in its one tick, observer.dm:328).
            if (mob is mob_dead_observer ghost)
            {
                float ghostDelay = Dm.truthy(ISDIAGONALDIR(Dm.num(direct))) ? FLOOR(GhostMoveDelay * sqrt(2), world.tick_lag) : GhostMoveDelay;
                move_delay = (old_move_delay + world.tick_lag > world.time ? old_move_delay : world.time) + ghostDelay;
                return await ghost.Move(new_loc as atom, direct, DELAY_TO_GLIDE_SIZE(ghostDelay));
            }
            return await mob.Move(new_loc as atom, direct);
        }
        if (mob.stat == DEAD)
        {
            mob.ghostize();
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand((!Dm.truthy(mob._listen_lookup?[COMSIG_MOB_CLIENT_PRE_LIVING_MOVE]) ? (object)NONE : mob._SendSignal(COMSIG_MOB_CLIENT_PRE_LIVING_MOVE, Dm.list(mob, new_loc, direct))), COMSIG_MOB_CLIENT_BLOCK_PRE_LIVING_MOVE)))
            return FALSE;

        mob_living L = mob as mob_living; //Already checked for isliving earlier
        if (Dm.truthy(L.incorporeal_move) && !Dm.truthy(is_secret_level(mob.z))) //Move though walls
        {
            await Process_Incorpmove(direct);
            return FALSE;
        }

        if (Dm.truthy(mob.remote_control)) //we're controlling something, our movement is relayed to it
            return await mob.remote_control.relaymove(mob as mob_living, direct);

        if (isAI(mob))
        {
            mob_living_silicon_ai smoovin_ai = mob as mob_living_silicon_ai;
            smoovin_ai.AIMove(direct); // silicons SI4
            return null;
        }

        if (Dm.truthy(await Process_Grab())) //are we restrained by someone's grip?
            return dot;

        if (Dm.truthy(mob.buckled)) //if we're buckled to something, tell it we moved.
            return await mob.buckled.relaymove(mob as mob_living, direct);

        if (!Dm.truthy(Dm.bitand(L.mobility_flags, MOBILITY_MOVE)))
            return FALSE;

        if (ismovable(mob.loc)) //Inside an object, tell it we moved
        {
            atom loc_atom = mob.loc;
            return await loc_atom.relaymove(mob as mob_living, direct);
        }

        if (!Dm.truthy(await mob.Process_Spacemove(Dm.num_or_null(direct))))
            return FALSE;

        DmList args = Dm.args(new_loc, direct, __extra);
        if (Dm.truthy(Dm.bitand((!Dm.truthy(mob._listen_lookup?[COMSIG_MOB_CLIENT_PRE_MOVE]) ? (object)NONE : mob._SendSignal(COMSIG_MOB_CLIENT_PRE_MOVE, Dm.list(mob, args))), COMSIG_MOB_CLIENT_BLOCK_PRE_MOVE)))
            return FALSE;

        //We are now going to move
        object add_delay = mob.cached_multiplicative_slowdown;
        object glide_delay = add_delay;
        if (Dm.truthy(NSCOMPONENT(Dm.num(direct))) && Dm.truthy(EWCOMPONENT(Dm.num(direct))))
            glide_delay = FLOOR(Dm.num(Dm.mul(glide_delay, sqrt(2))), world.tick_lag);
        mob.set_glide_size(DELAY_TO_GLIDE_SIZE(Dm.num(glide_delay))); // set it now in case of pulled objects
        //If the move was recent, count using old_move_delay
        //We want fractional behavior and all
        if (old_move_delay + world.tick_lag > world.time)
            //Yes this makes smooth movement stutter if add_delay is too fractional
            //Yes this is better then the alternative
            move_delay = old_move_delay;
        else
            move_delay = world.time;

        //Basically an optional override for our glide size
        //Sometimes you want to look like you're moving with a delay you don't actually have yet
        visual_delay = 0;
        float old_dir = mob.dir;
        atom old_loc = mob.loc;

        dot = await Move__d0(new_loc, direct);

        if (Dm.truthy(Dm.bitand(direct, Dm.sub(direct, 1))) && Dm.eq(mob.loc, new_loc)) //moved diagonally successfully
            add_delay = FLOOR(Dm.num(Dm.mul(add_delay, sqrt(2))), world.tick_lag);

        float after_glide = 0;
        if (Dm.truthy(visual_delay))
            after_glide = visual_delay;
        else
            after_glide = DELAY_TO_GLIDE_SIZE(Dm.num(add_delay));

        mob.set_glide_size(after_glide);

        move_delay += Dm.num(add_delay);
        if (Dm.truthy(dot)) // If mob is null here, we deserve the runtime
        {
            if (Dm.truthy(mob.throwing))
                mob.throwing.finalize(FALSE);

            // At this point we've moved the client's attached mob. This is one of the only ways to guess that a move was done
            // as a result of player input and not because they were pulled or any other magic.
            if (Dm.truthy(mob._listen_lookup?[COMSIG_MOB_CLIENT_MOVED]))
                mob._SendSignal(COMSIG_MOB_CLIENT_MOVED, Dm.list(mob, direct, old_dir, old_loc));
        }

        atom_movable P = mob.pulling;
        if (Dm.truthy(P) && !ismob(P) && Dm.truthy(P.density))
            await mob.setDir(REVERSE_DIR(mob.dir));
        return dot;
    }

    /**
     * Checks to see if you're being grabbed and if so attempts to break it
     *
     * Called by client/Move()
     */
    // tg: code/modules/mob/mob_movement.dm:152
    public virtual async DmTask<object> Process_Grab()
    {
        if (!Dm.truthy(mob.pulledby))
            return FALSE;
        if (mob.pulledby == mob.pulling && mob.pulledby.grab_state == GRAB_PASSIVE) //Don't autoresist passive grabs if we're grabbing them too.
            return FALSE;
        if (HAS_TRAIT(mob, TRAIT_INCAPACITATED))
        {
            move_delay = world.time + (1 * SECONDS); // COOLDOWN_START(src, move_delay, 1 SECONDS), expanded (rules 11.1 (a))
            return TRUE;
        }
        else if (HAS_TRAIT(mob, TRAIT_RESTRAINED))
        {
            move_delay = world.time + (1 * SECONDS); // COOLDOWN_START(src, move_delay, 1 SECONDS)
            to_chat(this, span_warning(I18n.T("You're restrained! You can't move!")));
            return TRUE;
        }
        // tg: :164  (resist_grab is translated since M4b slice 3, living.cs)
        return !Dm.truthy(await mob.resist_grab(TRUE)) ? TRUE : FALSE;
    }

    /**
     * Allows mobs to ignore density and phase through objects
     *
     * Called by client/Move()
     *
     * The behaviour depends on the incorporeal_move value of the mob
     *
     * * INCORPOREAL_MOVE_BASIC - forceMoved to the next tile with no stop
     * * INCORPOREAL_MOVE_SHADOW  - the same but leaves a cool effect path
     * * INCORPOREAL_MOVE_JAUNT - the same but blocked by holy tiles
     *
     * You'll note this is another mob living level proc living at the client level
     */
    // tg: code/modules/mob/mob_movement.dm:180
    // (syndicate X2: the basic kind, a ranged holoparasite's scout mode; the shadow walk and the jaunt wait for the ninja and the revenant)
    public virtual async DmTask<object> Process_Incorpmove(object direct = null)
    {
        turf mobloc = get_turf(mob) as turf;
        if (!isliving(mob))
            return null;
        mob_living L = (mob_living)mob;
        object sw__1 = L.incorporeal_move;
        if (Dm.eq(sw__1, INCORPOREAL_MOVE_BASIC))
        {
            turf T = get_step(L, Dm.num(direct)) as turf;
            if (T != null)
                await L.forceMove(T);
            await L.setDir(direct);
        }
        else if (Dm.eq(sw__1, INCORPOREAL_MOVE_SHADOW) || Dm.eq(sw__1, INCORPOREAL_MOVE_JAUNT))
        {
            // the shadow walk's trail (:190-231) and the jaunt's salt, NOJAUNT and blessed tiles (:232-252): the ninja's and the
            // revenant's movement, not ported (mobloc is read there)
            Dm.NotTranslated("code/modules/mob/mob_movement.dm:190", "Process_Incorpmove: the shadow walk and the jaunt (the ninja, the revenant)");
            return null;
        }
        return TRUE;
    }
}

// tg: code/modules/mob/mob_movement.dm:264
public partial class mob : atom_movable
{
    /**
     * Handles mob/living movement in space (or no gravity)
     *
     * Called by /client/Move()
     *
     * return TRUE for movement or FALSE for none
     *
     * You can move in space if you have a spacewalk ability
     */
    // tg: code/modules/mob/mob_movement.dm:264
    public override async DmTask<object> Process_Spacemove(float? movement_dir = null, float? continuous_move = null)
    {
        movement_dir ??= 0;
        continuous_move ??= FALSE;
        object dot = null;
        dot = await base.Process_Spacemove(movement_dir, continuous_move);
        if (Dm.truthy(dot) || HAS_TRAIT(this, TRAIT_SPACEWALK))
            return TRUE;

        // DEVIATION(DV412): a mob the wind blew into weightlessness drifts on past the hull: no handhold stops its drift (tg's backup does, a tile out)
        if (Dm.truthy(continuous_move) && (drift_handler != null ? drift_handler.dm_blown : Dm.num(world.time) - dm_blown_at <= BlownWindow))
            return FALSE;

        if (Dm.truthy(buckled))
            return TRUE;

        if (Dm.truthy(Dm.bitand(movement_type, FLYING)) || HAS_TRAIT(this, TRAIT_FREE_FLOAT_MOVEMENT))
            return TRUE;

        if (HAS_TRAIT(this, TRAIT_NOGRAV_ALWAYS_DRIFT))
            return FALSE;

        // object, rules 3.4: get_spacemove_backup also returns turfs (:330), which the declared /atom/movable would null
        object backup = await get_spacemove_backup(movement_dir, continuous_move);
        if (!Dm.truthy(backup))
            return FALSE;

        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_MOB_ATTEMPT_HALT_SPACEMOVE]) ? (object)NONE : _SendSignal(COMSIG_MOB_ATTEMPT_HALT_SPACEMOVE, Dm.list(this, movement_dir, continuous_move, backup))), COMPONENT_PREVENT_SPACEMOVE_HALT)))
            return FALSE;

        if (Dm.truthy(continuous_move) || !(backup is atom_movable) || !Dm.truthy(movement_dir) || Dm.truthy(((atom_movable)backup).anchored))
            return TRUE;

        // last pushoff exists for one reason
        // to ensure pushing a mob doesn't just lead to it considering us as backup, and failing
        last_pushoff = world.time;
        if (Dm.truthy(await ((atom_movable)backup).newtonian_move(dir2angle(REVERSE_DIR(Dm.num(movement_dir))), instant: TRUE))) //You're pushing off something movable, so it moves
        {
            // We set it down here so future calls to Process_Spacemove by the same pair in the same tick don't lead to fucky
            ((atom_movable)backup).last_pushoff = world.time;
            to_chat(this, span_info(I18n.T("You push off of [] to propel yourself.", backup)));
        }
        return TRUE;
    }

    /// We handle lattices via backups
    // tg: code/modules/mob/mob_movement.dm:298
    public override object handle_spacemove_grabbing()
    {
        return null;
    }

    /**
     * Finds a target near a mob that is viable for pushing off when moving.
     * Takes the intended movement direction as input, alongside if the context is checking if we're allowed to continue drifting
     * If include_floors is TRUE, includes floors *with gravity*
     */
    // tg: code/modules/mob/mob_movement.dm:306
    [DmParams("moving_direction", "continuous_move", "include_floors")]
    public override async DmTask<object> get_spacemove_backup(params object[] __extra)
    {
        object moving_direction = Dm.arg<object>(__extra, 0);
        object continuous_move = Dm.arg<object>(__extra, 1);
        object include_floors = Dm.arg<object>(__extra, 2) ?? FALSE;
        atom secondary_backup = null;
        DmList priority_dirs = Dm.@in(moving_direction, GLOB.cardinals) ? GLOB.cardinals : GLOB.diagonals;
        foreach (atom pushover in Dm.each<atom>(range(1, get_turf(this))))
        {
            if (pushover == this)
                continue;
            if (isarea(pushover))
                continue;
            // DEVIATION(DV534): a weightless capsule holds on only to what its hand reaches, a metre from its centre (tg: anything in range 1)
            if (!LocomotionSeam.InHandReach(this, pushover))
                continue;
            bool is_priority = pushover.loc == loc || Dm.@in(get_dir(this, pushover), priority_dirs);
            if (isturf(pushover))
            {
                turf turf = pushover as turf;
                if (isspaceturf(turf))
                    continue;
                if (!Dm.truthy(turf.density) && !Dm.truthy(mob_negates_gravity()))
                {
                    if (!Dm.truthy(include_floors) || !Dm.truthy(turf.has_gravity()))
                        continue;
                }
                if (is_priority)
                    return pushover;
                secondary_backup = pushover;
                continue;
            }

            atom_movable rebound = pushover as atom_movable;
            if (rebound == buckled)
                continue;
            if (ismob(rebound))
            {
                mob lover = rebound as mob;
                if (Dm.truthy(lover.buckled))
                    continue;
            }

            object pass_allowed = await rebound.CanPass(this, get_dir(rebound, this));
            if (!Dm.truthy(rebound.density) && Dm.truthy(pass_allowed) && !(rebound is obj_structure_lattice))
                continue;
            //Sometime this tick, this pushed off something. Doesn't count as a valid pushoff target
            if (Dm.eq(rebound.last_pushoff, world.time))
                continue;
            if (Dm.truthy(continuous_move) && !Dm.truthy(pass_allowed))
            {
                datum_move_loop_smooth_move rebound_engine = GLOB.move_manager.processing_on(rebound, SSnewtonian_movement) as datum_move_loop_smooth_move;
                // If you're moving toward it and you're both going the same direction, stop
                if (Dm.eq(moving_direction, get_dir(this, pushover)) && rebound_engine != null && Dm.eq(moving_direction, angle2dir(Dm.num(rebound_engine.angle))))
                    continue;
            }
            else if (!Dm.truthy(pass_allowed))
            {
                if (Dm.eq(moving_direction, get_dir(this, pushover))) // Can't push "off" of something that you're walking into
                    continue;
            }
            if (Dm.truthy(rebound.anchored))
            {
                if (is_priority)
                    return rebound;
                secondary_backup = rebound;
                continue;
            }
            if (pulling == rebound)
                continue;
            if (is_priority)
                return rebound;
            secondary_backup = rebound;
        }
        return secondary_backup;
    }

    // tg: code/modules/mob/mob_movement.dm:361
    public override object has_gravity(turf gravity_turf = null)
    {
        return (mob_negates_gravity() is var or__1 && Dm.truthy(or__1) ? or__1 : base.has_gravity(gravity_turf));
    }

    /**
     * Does this mob ignore gravity
     */
    // tg: code/modules/mob/mob_movement.dm:367
    public virtual object mob_negates_gravity()
    {
        return FALSE;
    }
}

// tg: code/modules/mob/mob_movement.dm:380  (M4b slice 3)
public partial class mob : atom_movable
{
    /**
     * Called when this mob slips over, override as needed
     *
     * knockdown_amount - time (in deciseconds) the slip leaves them on the ground
     * slipped_on - optional, what'd we slip on? if not set, we assume they just fell over
     * lube - bitflag of "lube flags", see [mobs.dm] for more information
     * paralyze - time (in deciseconds) the slip leaves them paralyzed / unable to move
     * daze - time (in deciseconds) the slip leaves them vulnerable to shove stuns
     * force_drop = the slip forces them to drop held items
     */
    // tg: code/modules/mob/mob_movement.dm:380  (rules 3.2, typed vars that hold wider values: obj/slipped_on is atom, the slippery
    // component of a wet floor passes the turf; gaps package G4, the slip memory's "wet floor")
    public virtual async DmTask<object> slip(object knockdown_amount = null, atom slipped_on = null, object lube_flags = null, object paralyze = null, object daze = null, float? force_drop = null)
    {
        force_drop ??= FALSE;
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_SLIPPED]))
            _SendSignal(COMSIG_MOB_SLIPPED, Dm.list(this, knockdown_amount, slipped_on, lube_flags, paralyze, daze, force_drop));
        return TRUE;
    }
}

// tg: code/modules/mob/mob_movement.dm:384  (M4b slice 3)
public partial class mob_living : mob
{
    // tg: code/modules/mob/mob_movement.dm:384
    public override async DmTask<object> slip(object knockdown_amount = null, atom slipped_on = null, object lube_flags = null, object paralyze = null, object daze = null, float? force_drop = null)
    {
        force_drop ??= FALSE;
        add_mob_memory(typeof(datum_memory_was_slipped), Dm.named("antagonist", slipped_on));
        await add_mood_event("slipped", typeof(datum_mood_event_slipped));
        await add_personality_mood_to_viewers(this, "slip_observed", Dm.list(Dm.kv(typeof(datum_personality_whimsical), typeof(datum_mood_event_whimsical_slip))), range: 5);
        return await base.slip(knockdown_amount, slipped_on, lube_flags, paralyze, daze, force_drop);
    }
}

// tg: code/modules/mob/mob_movement.dm:514
public partial class mob_living : mob
{
    /**
     * Toggles move intent between walk and run, calling set_move_intent.
     */
    // tg: code/modules/mob/mob_movement.dm:514
    public virtual async DmTask toggle_move_intent()
    {
        if (move_intent == MOVE_INTENT_RUN)
            await set_move_intent(MOVE_INTENT_WALK);
        else
            await set_move_intent(MOVE_INTENT_RUN);
    }

    /**
     * Sets the move intent of the mob. Triggers an update the move intent hud as well.
     * Args:
     * - new_state: The move state we're being set to.
     */
    // tg: code/modules/mob/mob_movement.dm:525
    public virtual async DmTask set_move_intent(object new_state = null)
    {
        if (Dm.eq(move_intent, new_state))
            return;
        move_intent = (string)new_state;
        (hud_used?.screen_objects[HUD_MOB_MOVE_INTENT] as atom)?.update_appearance();
        await update_move_intent_slowdown();
        if (Dm.truthy(_listen_lookup?[COMSIG_MOVE_INTENT_TOGGLED]))
            _SendSignal(COMSIG_MOVE_INTENT_TOGGLED, Dm.list(this));
    }
}

// tg: code/modules/mob/mob_movement.dm:534
public partial class mob : atom_movable
{
    ///Moves a mob upwards in z level
    // tg: code/modules/mob/mob_movement.dm:534
    public virtual async DmTask<object> up()
    {
        if (remote_control != null)
            return await remote_control.relaymove(this as mob_living, UP);

        turf current_turf = get_turf(this);

        if (loc is atom_movable loc_atom) //Inside an object, tell it we moved
            return await loc_atom.relaymove(this as mob_living, UP);

        var current_ladder = Dm.locate<obj_structure_ladder>(current_turf);
        if (current_ladder != null)
        {
            await current_ladder.use(this, TRUE);
            return null;
        }

        if (!Dm.truthy(can_z_move(UP, current_turf, null, ZMOVE_CAN_FLY_CHECKS | ZMOVE_FEEDBACK)))
            return null;
        // DEVIATION(DV422): no balloon and no one-second do_after with its bar (tg's :545-547): a short flight the clients draw rising or sinking
        // (DV371's announced ride), then tg's zMove if the way is still open
        if (!await FlyLevel(UP, 1))
            return null;
        if (Dm.truthy(await zMove(UP, z_move_flags: ZMOVE_FLIGHT_FLAGS | ZMOVE_FEEDBACK)))
            to_chat(this, span_notice(I18n.T("You move upwards.")));
        return null;
    }

    /// <summary>DV422: the deciseconds a flight up or down a level takes (tg's do_after is a second).</summary>
    public const float FlyLevelDeciseconds = 3;

    internal bool dm_flying_level;

    // DV422: the flight's wait; false when the flyer can no longer move a level that way when it ends
    private async DmTask<bool> FlyLevel(float dir, int dz)
    {
        if (dm_flying_level)
            return false; // one flight at a time, as tg's do_after allows one
        dm_flying_level = true;
        LevelRides.Start(this, dz, FlyLevelDeciseconds);
        await sleep(FlyLevelDeciseconds);
        LevelRides.End(this);
        dm_flying_level = false;
        return !QDELETED(this) && Dm.truthy(can_z_move(dir, get_turf(this), null, ZMOVE_CAN_FLY_CHECKS));
    }

    ///Moves a mob down a z level
    // tg: code/modules/mob/mob_movement.dm:557
    public virtual async DmTask<object> down()
    {
        if (remote_control != null)
            return await remote_control.relaymove(this as mob_living, DOWN);

        turf current_turf = get_turf(this);

        if (loc is atom_movable loc_atom) //Inside an object, tell it we moved
            return await loc_atom.relaymove(this as mob_living, DOWN);

        var current_ladder = Dm.locate<obj_structure_ladder>(current_turf);
        if (current_ladder != null)
        {
            await current_ladder.use(this, FALSE);
            return null;
        }

        if (!Dm.truthy(can_z_move(DOWN, current_turf, null, ZMOVE_CAN_FLY_CHECKS | ZMOVE_FEEDBACK)))
            return null;
        // DEVIATION(DV422): no balloon and no one-second do_after with its bar (tg's :545-547): a short flight the clients draw rising or sinking
        // (DV371's announced ride), then tg's zMove if the way is still open
        if (!await FlyLevel(DOWN, -1))
            return null;
        if (Dm.truthy(await zMove(DOWN, z_move_flags: ZMOVE_FLIGHT_FLAGS | ZMOVE_FEEDBACK)))
            to_chat(this, span_notice(I18n.T("You move down.")));
        return FALSE;
    }
}

// tg: code/modules/mob/mob_movement.dm:582
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob_movement.dm:582  (tg names the parameter destination; the C# parameter keeps the slot's
    // name, rules 7.2)
    [DmParams("destination")]
    public override async DmTask<object> abstract_move(atom new_loc = null)
    {
        turf new_turf = get_turf(new_loc);
        if (Dm.truthy(new_turf) && (new_turf is turf_cordon_secret || Dm.truthy(is_secret_level(new_turf.z))) && !Dm.truthy(client?.holder))
            return null;
        return await base.abstract_move(new_loc);
    }

    // tg: code/modules/mob/mob_movement.dm:588
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = null;
        dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (Dm.truthy(client) && Dm.truthy(LAZYLEN(sound_tokens)))
            SSsound_tokens.clients_needing_update[client] = TRUE;
        return dot;
    }
}
