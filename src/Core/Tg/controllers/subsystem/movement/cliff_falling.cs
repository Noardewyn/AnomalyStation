// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/movement/cliff_falling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the subsystem that slides whoever falls off a cliff down it, in the cliff's fall direction at its fall speed, until
// they leave the cliffs. The var block is generated.

/// Subsystem to handle falling of off cliffs
// tg: code/controllers/subsystem/movement/cliff_falling.dm:2 MOVEMENT_SUBSYSTEM_DEF(cliff_falling)
public partial class datum_controller_subsystem_movement_cliff_falling : datum_controller_subsystem_movement
{
    // The New() that MOVEMENT_SUBSYSTEM_DEF(cliff_falling) defines (__DEFINES/MC.dm:107-111), with NEW_SS_GLOBAL(SScliff_falling)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/movement/cliff_falling.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SScliff_falling != this)
        {
            if (SScliff_falling is datum_controller_subsystem_movement_cliff_falling)
            {
                Recover();
                qdel(SScliff_falling);
            }
            SScliff_falling = this;
        }
        PreInit();
        await DmTask.CompletedTask;
    }

    // The fire() that MOVEMENT_SUBSYSTEM_DEF(cliff_falling) defines (__DEFINES/MC.dm:112): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/movement/cliff_falling.dm:2
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }

    // tg: code/controllers/subsystem/movement/cliff_falling.dm:11
    public virtual void start_falling(atom_movable faller = null, turf_open_cliff cliff = null)
    {
        // Make them move
        object mover = GLOB.move_manager.move(moving: faller, direction: cliff.fall_direction, delay: cliff.fall_speed, subsystem: this, priority: MOVEMENT_ABOVE_SPACE_PRIORITY, flags: Dm.bitor(MOVEMENT_LOOP_OUTSIDE_CONTROL, MOVEMENT_LOOP_NO_DIR_UPDATE));

        cliff_grinders[faller] = mover;

        RegisterSignal(faller, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal(faller, COMSIG_QDELETING, nameof(clear_references));
        RegisterSignal(faller, COMSIG_MOVABLE_PRE_MOVE, nameof(check_move));
    }

    /// We just moved, so check if we're still moving right
    // tg: code/controllers/subsystem/movement/cliff_falling.dm:22
    [SignalHandler]
    public virtual void on_moved(atom_movable mover = null, turf old_loc = null)
    {
        turf_open_cliff new_cliff = mover.loc as turf_open_cliff;
        if (new_cliff == null) //not a cliff, lets clean up
        {
            datum_move_loop_move falling = cliff_grinders[mover] as datum_move_loop_move;
            clear_references(mover);
            qdel(falling);
            return;
        }

        new_cliff.on_fall(mover);

        // kept as tg has it: a type compared with the cliff itself, never equal, so the delay is set at every step
        if (Dm.eq(old_loc.type, new_cliff)) //same type of cliff, no worries
            return;

        datum_move_loop_move fall = cliff_grinders[mover] as datum_move_loop_move;
        fall.set_delay(new_cliff.fall_speed); //different cliff, so set the speed
    }

    // tg: code/controllers/subsystem/movement/cliff_falling.dm:40
    [SignalHandler]
    public virtual void on_qdel(atom_movable deletee = null)
    {
        clear_references(deletee);
    }

    // tg: code/controllers/subsystem/movement/cliff_falling.dm:45
    [SignalHandler]
    public virtual void clear_references(atom_movable deletee = null)
    {
        cliff_grinders.Remove(deletee);

        UnregisterSignal(deletee, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_QDELETING, COMSIG_MOVABLE_PRE_MOVE));
    }

    /// Check if we can move! We do this mostly to determine falling behaviour and make sure we're moving to valid tiles
    // tg: code/controllers/subsystem/movement/cliff_falling.dm:51
    [SignalHandler]
    public virtual object check_move(atom_movable mover = null, turf target = null)
    {
        turf_open_cliff cliff_turf = get_turf(mover) as turf_open_cliff;

        if (cliff_turf == null) //we arent on a cliff, WHY ARE WE HERE???
        {
            clear_references(mover);
            return null;
        }

        if (!Dm.truthy(cliff_turf.can_move(mover, target)))
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
        return null;
    }
}

public sealed partial class TgState
{
    internal datum_controller_subsystem_movement_cliff_falling SScliff_falling;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_movement_cliff_falling SScliff_falling => ref TgState.Current.SScliff_falling;
}
