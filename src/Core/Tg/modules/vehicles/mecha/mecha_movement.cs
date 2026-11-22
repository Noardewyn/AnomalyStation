// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mecha_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV358): where the locomotion phase runs, it walks the mech (LocomotionSeam.Mechs.cs) and vehicle_move's step does not happen: the port procs at the
// end of the class are what the phase asks. Without the phase (a Core world) tg's tile steps stand.

// tg: code/modules/vehicles/mecha/mecha_movement.dm:2
public partial class obj_vehicle_sealed_mecha : obj_vehicle_sealed
{
    /// Sets the direction of the mecha and all of its occcupents, required for FOV. Alternatively one could make a recursive contents registration and register topmost direction changes in the fov component
    // tg: code/modules/vehicles/mecha/mecha_movement.dm:2
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        foreach (mob_living occupant in Dm.each<mob_living>(occupants))
            await occupant.setDir(newdir);
    }

    ///Called when the mech moves
    // tg: code/modules/vehicles/mecha/mecha_movement.dm:8
    public virtual void on_move()
    {
        Waiting.Detach(collect_ore());
        // DEVIATION(DV358): a walked mech's steps follow the distance it walks (StepMech), as a walker's footsteps do (DV233)
        if (!LocomotionSeam.IsBridging(this))
            play_stepsound();
    }

    ///Collects ore when we move, if there is an orebox and it is functional
    // tg: code/modules/vehicles/mecha/mecha_movement.dm:15
    public virtual async DmTask collect_ore()
    {
        if (ore_box == null || !HAS_TRAIT(this, TRAIT_OREBOX_FUNCTIONAL))
            return;
        foreach (obj_item_stack_ore ore in Dm.each<obj_item_stack_ore>(range(1, this)))
        {
            //we can reach it and it's in front of us? grab it!
            if (Dm.truthy(await ore.Adjacent(this)) && (Dm.truthy(Dm.bitand(get_dir(this, ore), dir)) || ore.loc == loc))
                await ore.forceMove(ore_box);
        }
        foreach (obj_item_boulder boulder in Dm.each<obj_item_boulder>(range(1, this)))
        {
            //As above, but for boulders
            if (Dm.truthy(await boulder.Adjacent(this)) && (Dm.truthy(Dm.bitand(get_dir(this, boulder), dir)) || boulder.loc == loc))
                await boulder.forceMove(ore_box);
        }
    }

    ///Plays the mech step sound effect. Split from movement procs so that other mechs (HONK) can override this one specific part.
    // tg: code/modules/vehicles/mecha/mecha_movement.dm:29
    public virtual void play_stepsound()
    {
        if (Dm.truthy(Dm.bitand(mecha_flags, QUIET_STEPS)))
            return;

        // if we are on the second step of the diagonal movement, don't play step sound
        if (Dm.eq(moving_diagonally, SECOND_DIAG_STEP))
            return;

        playsound(this, stepsound, 40, TRUE);
    }

    // Do whatever you do to mobs to these fuckers too
    // tg: code/modules/vehicles/mecha/mecha_movement.dm:40
    public override async DmTask<object> Process_Spacemove(float? movement_dir = null, float? continuous_move = null)
    {
        movement_dir ??= 0;
        continuous_move ??= FALSE;
        object dot = await base.Process_Spacemove(movement_dir, continuous_move);
        if (Dm.truthy(dot))
            return TRUE;

        object backup_found = await get_spacemove_backup(movement_dir, continuous_move);
        if (Dm.truthy(backup_found))
        {
            atom_movable backup = backup_found as atom_movable;
            if (backup == null || !Dm.truthy(movement_dir) || Dm.truthy(backup.anchored) || Dm.truthy(continuous_move)) //get_spacemove_backup() already checks if a returned turf is solid, so we can just go
                return TRUE;
            last_pushoff = world.time;
            if (Dm.truthy(await backup.newtonian_move(dir2angle(REVERSE_DIR(Dm.num(movement_dir))), instant: TRUE)))
            {
                backup.last_pushoff = world.time;
                step_silent = TRUE;
                if (Dm.truthy(return_drivers()))
                    to_chat(occupants, span_info(I18n.T("The [] push off [] to propel yourself.", this, backup)));
            }
            return TRUE;
        }

        if (active_thrusters != null && Dm.truthy(await active_thrusters.thrust(movement_dir)))
        {
            step_silent = TRUE;
            return TRUE;
        }
        return FALSE;
    }

    ///Called when the driver turns with the movement lock key
    // tg: code/modules/vehicles/mecha/mecha_movement.dm:62
    public virtual object on_turn(mob_living driver = null, object direction = null) => COMSIG_IGNORE_MOVEMENT_LOCK;

    // tg: code/modules/vehicles/mecha/mecha_movement.dm:66
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (!Dm.truthy(canmove) || !Dm.@in(user, return_drivers()))
            return TRUE;
        if (!Dm.truthy(await vehicle_move(direction)))
            return TRUE;
        user.SEND_SIGNAL(COMSIG_MOB_DROVE_MECH, Dm.list(user, this));
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/mecha_movement.dm:73
    public override async DmTask<object> vehicle_move(object direction = null, object forcerotate = null)
    {
        forcerotate ??= FALSE;
        if (!(cooldown_vehicle_move <= world.time)) // !COOLDOWN_FINISHED(src, cooldown_vehicle_move)
            return FALSE;

        cooldown_vehicle_move = world.time + Dm.num(movedelay); // COOLDOWN_START(src, cooldown_vehicle_move, movedelay)

        if (Dm.truthy(completely_disabled))
            return FALSE;

        if (!Dm.truthy(direction))
            return FALSE;

        if (!Dm.truthy(can_move(direction)))
            return FALSE;

        if (ismovable(loc)) //Mech is inside an object, tell it we moved
        {
            atom loc_atom = loc;
            return await loc_atom.relaymove(null, direction); // tg passes the mech as the user; no relaymove of a movable that holds a mech reads it
        }

        if (!Dm.truthy(await Process_Spacemove(Dm.num(direction))))
            return FALSE;

        object olddir = dir;

        if (Dm.truthy(Dm.bitand(internal_damage, MECHA_INT_CONTROL_LOST)))
            direction = pick(GLOB.alldirs);

        //only mechs with diagonal movement may move diagonally
        if (!Dm.truthy(allow_diagonal_movement) && Dm.truthy(ISDIAGONALDIR(Dm.num(direction))))
            return TRUE;

        bool keyheld = capsule_alt_held();

        //if we're not facing the way we're going rotate us
        // if we're not strafing or if we are forced to rotate or if we are holding down the key
        if (!Dm.eq(dir, direction) && (!Dm.truthy(strafe) || Dm.truthy(forcerotate) || keyheld))
        {
            await setDir(direction);
            if (!Dm.truthy(Dm.bitand(mecha_flags, QUIET_TURNS)))
                playsound(this, turnsound, 40, TRUE);
            if (keyheld || !Dm.truthy(pivot_step)) //If we pivot step, we don't return here so we don't just come to a stop
                return TRUE;
        }

        set_glide_size(DELAY_TO_GLIDE_SIZE(Dm.num(movedelay)));
        //Otherwise just walk normally
        object dot = await try_step_multiz(direction);

        if (Dm.truthy(strafe))
            await setDir(olddir);
        return dot;
    }

    // A tile crossing of the capsule walk: what a chassis' vehicle_move does after a successful step (DV358).
    public virtual void capsule_crossed()
    {
    }

    /// Check if anything is blocking our movement
    // tg: code/modules/vehicles/mecha/mecha_movement.dm:128
    public virtual object can_move(object direction = null)
    {
        if (Dm.truthy(toppled))
            return FALSE;

        obj_machinery_portable_atmospherics_canister internal_tank = get_internal_tank();
        if (Dm.truthy(internal_tank?.connected_port))
        {
            if (!Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_MECHA_MESSAGE)))
            {
                to_chat(occupants, span_warning(I18n.T("Unable to move while connected to the air system port!")));
                timer_cooldown_start(COOLDOWN_MECHA_MESSAGE, 2 * SECONDS);
            }
            return FALSE;
        }

        DmList missing_parts = Dm.list();
        if (cell == null)
            missing_parts.Add("power cell");
        if (capacitor == null)
            missing_parts.Add("capacitor");
        if (servo == null)
            missing_parts.Add("servo");

        if (Dm.truthy(length(missing_parts)))
        {
            if (!Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_MECHA_MESSAGE)))
            {
                to_chat(occupants, span_warning(I18n.T("Missing [].", english_list(missing_parts))));
                timer_cooldown_start(COOLDOWN_MECHA_MESSAGE, 2 * SECONDS);
            }
            return FALSE;
        }

        if (step_energy_drain != 0 && !Dm.truthy(use_energy(step_energy_drain)))
        {
            if (!Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_MECHA_MESSAGE)))
            {
                to_chat(occupants, span_warning(I18n.T("Insufficient power to move!")));
                timer_cooldown_start(COOLDOWN_MECHA_MESSAGE, 2 * SECONDS);
            }
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/mecha_movement.dm:159
    public override async DmTask<object> Bump(atom obstacle = null)
    {
        object dot = await base.Bump(obstacle);
        if (Dm.truthy(dot)) //mech was thrown/door/whatever
            return dot;
        await try_bumpsmash(obstacle);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_movement.dm:165
    public virtual async DmTask try_bumpsmash(atom obstacle = null)
    {
        // Whether or not we're on our mecha melee cooldown
        bool on_cooldown = Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_MECHA_MELEE_ATTACK));
        // DEVIATION(DV358): a walked mech's push goes the way it moves, which is not its dir (the view's)
        float push_dir = capsule_bump_dir != 0 ? capsule_bump_dir : Dm.num(dir);

        if (Dm.truthy(bumpsmash) && !on_cooldown)
        {
            // Our pilot for this evening
            DmList mobster = return_drivers();
            if (Dm.truthy(await obstacle.mech_melee_attack(this, mobster[1] as mob_living)))
                timer_cooldown_start(COOLDOWN_MECHA_MELEE_ATTACK, melee_cooldown * 0.3f);
            if (QDELETED(obstacle) || Dm.truthy(await obstacle.CanPass(this, Dm.truthy(get_dir(obstacle, this)) ? get_dir(obstacle, this) : push_dir))) // The else is in case the obstacle is in the same turf.
                await step(this, push_dir);
        }

        if (isobj(obstacle))
        {
            obj obj_obstacle = obstacle as obj;
            if (!Dm.truthy(obj_obstacle.anchored) && Dm.num(obj_obstacle.move_resist) <= Dm.num(move_force))
                await step(obstacle as atom_movable, push_dir);
        }
        else if (ismob(obstacle))
        {
            mob mob_obstacle = obstacle as mob;
            if (Dm.num(mob_obstacle.move_resist) <= Dm.num(move_force))
                await step(obstacle as atom_movable, push_dir);
        }
    }

    //Following procs are camera static update related and are basically ripped off of code\modules\mob\living\silicon\silicon_movement.dm

    //We only call a camera static update if we have successfully moved and have a camera installed
    // tg: code/modules/vehicles/mecha/mecha_movement.dm:190
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (chassis_camera == null || !Dm.truthy(chassis_camera.can_use()))
            return dot;
        // Delay's a bit faster then standard cameras to "avoid running out of the camera's fov" whatever that means
        // An EMPd mecha with a lowered view_range on its camera can still sometimes run out into static before updating, however.
        SScameras.camera_moved(chassis_camera, get_turf(old_loc), get_turf(chassis_camera), 0.5f * SECONDS);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_movement.dm:199
    public virtual async DmTask right_self(object fallen_angle = null)
    {
        if (!Dm.truthy(toppled))
            return;
        toppled = FALSE;
        matrix to_turn = turn(transform, 360 - Dm.num(fallen_angle)) as matrix;
        animate(this, Dm.named("transform", to_turn), Dm.named("time", 0.5f * SECONDS));
        playsound(this, Dm.resource("sound/vehicles/mecha/nominal.ogg"), 50);
    }

    // ---- DEVIATION(DV358): what the locomotion phase asks of a walked mech (port procs; tg's lines are vehicle_move's and can_move's) ----

    /// The direction of the last tick's walk while the phase's bump runs (0: none): tg's dir, which a walked mech does not walk by.
    public float capsule_bump_dir;

    /// Whether the driver may steer it at all: relaymove's canmove, vehicle_move's completely_disabled.
    public virtual bool capsule_may_drive() => Dm.truthy(canmove) && !Dm.truthy(completely_disabled);

    /// vehicle_move's checks up to its step, in tg's order, at tg's cadence: the direction it moves (a lost control's random one), 0 when it does not.
    public virtual async DmTask<float> capsule_drive_gate(float direction)
    {
        if (!Dm.truthy(can_move(direction)))
            return 0;
        if (!Dm.truthy(await Process_Spacemove(direction)))
            return 0;
        if (Dm.truthy(Dm.bitand(internal_damage, MECHA_INT_CONTROL_LOST)))
            return Dm.num(pick(GLOB.alldirs));
        foreach (mob driver in Dm.each<mob>(return_drivers()))
            driver.SEND_SIGNAL(COMSIG_MOB_DROVE_MECH, Dm.list(driver, this));
        return direction;
    }

    /// can_move without its energy and its messages: what the phase tells the prediction between two gates.
    public virtual bool capsule_can_move()
        => !Dm.truthy(toppled) && !Dm.truthy(get_internal_tank()?.connected_port) && cell != null && capacitor != null && servo != null
        && (step_energy_drain == 0 || Dm.num(cell.charge) >= step_energy_drain);

    /// The strafe lock: tg's strafe holds the dir while it moves, Alt turns it all the same.
    public virtual bool capsule_facing_locked() => Dm.truthy(strafe) && !capsule_alt_held();

    private bool capsule_alt_held()
    {
        if (!Dm.truthy(strafe))
            return false;
        foreach (mob driver in Dm.each<mob>(return_drivers()))
        {
            if (Dm.truthy(driver.client?.keys_held?["Alt"]))
                return true;
        }
        return false;
    }

    /// vehicle_move's turn: setDir and its sound.
    public virtual async DmTask capsule_turn(float direction)
    {
        await setDir(direction);
        if (!Dm.truthy(Dm.bitand(mecha_flags, QUIET_TURNS)))
            playsound(this, turnsound, 40, TRUE);
    }
}
