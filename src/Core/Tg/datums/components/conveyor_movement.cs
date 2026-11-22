// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/conveyor_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The conveyors (the owner, 2026-11-04): the whole file. The var block is generated.

//Make a component to do things like gravity/flying checks
///Manages the loop caused by being on a conveyor belt
///Prevents movement while you're floating, etc
///Takes the direction to move, delay between steps, and time before starting to move as arguments
// tg: code/datums/components/conveyor_movement.dm:5
public partial class datum_component_convey : datum_component
{
    // tg: code/datums/components/conveyor_movement.dm:10
    [DmParams("direction", "speed", "start_delay")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object direction = Dm.arg(__extra, 0);
        object speed = Dm.arg(__extra, 1);
        object start_delay = Dm.arg(__extra, 2);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        living_parent = isliving(parent) ? TRUE : FALSE;
        this.speed = speed;
        if (!Dm.truthy(start_delay))
            start_delay = speed;
        atom_movable moving_parent = (atom_movable)parent;
        datum_move_loop loop = GLOB.move_manager.move(moving_parent, direction, delay: start_delay, subsystem: SSconveyors,
            flags: MOVEMENT_LOOP_IGNORE_PRIORITY | MOVEMENT_LOOP_OUTSIDE_CONTROL | MOVEMENT_LOOP_NO_DIR_UPDATE) as datum_move_loop;
        RegisterSignal(loop, COMSIG_MOVELOOP_PREPROCESS_CHECK, nameof(should_move));
        RegisterSignal(loop, COMSIG_QDELETING, nameof(loop_ended));
        RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(special_rotate));
        return null;
    }

    // tg: code/datums/components/conveyor_movement.dm:25
    [SignalHandler]
    public virtual object should_move(datum_move_loop source = null)
    {
        // SIGNAL_HANDLER
        source.delay = Dm.num(speed); //We use the default delay
        if (Dm.truthy(living_parent))
        {
            mob_living moving_mob = (mob_living)parent;
            if (Dm.truthy(Dm.bitand(moving_mob.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)) && !IS_UNCONSCIOUS_OR_CRIT(moving_mob))
                return MOVELOOP_SKIP_STEP;
        }
        atom_movable moving_parent = (atom_movable)parent;
        if (Dm.truthy(moving_parent.anchored) || !Dm.truthy(moving_parent.has_gravity()))
            return MOVELOOP_SKIP_STEP;
        // DEVIATION(DV347): a capsule mob rides the belts continuously (LocomotionSeam.BeltsAround): its loop keeps its timing and the
        // belts' enter and exit checks but does not move it
        if (LocomotionSeam.RidesBeltsContinuously(moving_parent))
            return MOVELOOP_SKIP_STEP;
        obj_machinery_conveyor belt = Dm.locate<obj_machinery_conveyor>(moving_parent.loc);
        if (!Dm.truthy(ISDIAGONALDIR(belt.dir)))
        {
            current_rotation = null;
            return null;
        }

        current_rotation = Dm.num(belt.operating) * -90; // belt.operating will be CONVEYOR_FORWARD or CONVEYOR_BACKWARD, 1 or -1
        if (Dm.truthy(Dm.bitxor(belt.inverted, belt.flipped)))
            current_rotation = -Dm.num(current_rotation);
        return null;
    }

    // tg: code/datums/components/conveyor_movement.dm:43
    [SignalHandler]
    public virtual void special_rotate(datum_move_loop source = null, object result = null)
    {
        // SIGNAL_HANDLER
        if (Dm.eq(result, MOVELOOP_SUCCESS) && Dm.truthy(current_rotation))
        {
            atom_movable turning_parent = (atom_movable)parent;
            Waiting.NoSleep(turning_parent.setDir(turn(turning_parent.dir, Dm.num(current_rotation))));
        }
    }

    // tg: code/datums/components/conveyor_movement.dm:49
    [SignalHandler]
    public virtual void loop_ended(datum source = null)
    {
        // SIGNAL_HANDLER
        if (QDELETED(this))
            return;
        qdel(this);
    }
}
