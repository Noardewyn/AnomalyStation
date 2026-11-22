// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/movement/ai_movement_jps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps packages C1 and C3).

/**
 * This movement datum represents smart-pathing
 */
// tg: code/datums/ai/movement/ai_movement_jps.dm:4
public partial class datum_ai_movement_jps : datum_ai_movement
{
    // tg: code/datums/ai/movement/ai_movement_jps.dm:10
    public override object start_moving_towards(datum_ai_controller controller = null, atom current_movement_target = null, object min_distance = null, object delay_override = null)
    {
        object dot = base.start_moving_towards(controller, current_movement_target, min_distance, delay_override);
        if (!Dm.truthy(dot))
            return FALSE;
        atom_movable moving = controller.pawn as atom_movable;
        object delay = Dm.truthy(delay_override) ? delay_override : controller.movement_delay;

        datum_move_loop loop = setup_moveloop(controller, current_movement_target, moving, delay, min_distance) as datum_move_loop;

        RegisterSignal(loop, COMSIG_MOVELOOP_PREPROCESS_CHECK, nameof(pre_move));
        RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(post_move));
        RegisterSignal(loop, COMSIG_MOVELOOP_JPS_REPATH, nameof(repath_incoming));

        return loop;
    }

    // tg: code/datums/ai/movement/ai_movement_jps.dm:26
    public virtual object setup_moveloop(datum_ai_controller controller = null, atom current_movement_target = null, atom_movable moving = null, object delay = null, object min_distance = null)
    {
        return GLOB.move_manager.jps_move(moving,
            current_movement_target,
            delay,
            repath_delay: 0.5f * SECONDS,
            simulated_only: Dm.num(!HAS_TRAIT(controller.pawn, TRAIT_SPACEWALK)),
            max_path_length: maximum_length,
            minimum_distance: min_distance,
            access: controller.get_access() as DmList,
            subsystem: SSai_movement,
            diagonal_handling: diagonal_flags,
            extra_info: controller);
    }

    // tg: code/datums/ai/movement/ai_movement_jps.dm:41
    public override void update_movement_target(datum_ai_controller controller = null, atom new_target = null)
    {
        base.update_movement_target(controller, new_target);
        datum_move_loop_has_target_jps loop = GLOB.move_manager.processing_on(controller.pawn as atom_movable, SSai_movement) as datum_move_loop_has_target_jps;
        if (loop != null)
            Waiting.DetachSync(() => loop.recalculate_path());
    }

    // tg: code/datums/ai/movement/ai_movement_jps.dm:47
    [SignalHandler]
    public virtual void repath_incoming(datum_move_loop_has_target_jps source = null)
    {
        datum_ai_controller controller = source.extra_info as datum_ai_controller;

        source.access = controller.get_access() as DmList;
        // minimum_distance was set at loop creation; no need to update it on repath
    }
}

// tg: code/datums/ai/movement/ai_movement_jps.dm:54
public partial class datum_ai_movement_jps_bot : datum_ai_movement_jps
{
    // tg: code/datums/ai/movement/ai_movement_jps.dm:59
    public override object start_moving_towards(datum_ai_controller controller = null, atom current_movement_target = null, object min_distance = null, object delay_override = null)
    {
        datum_move_loop loop = base.start_moving_towards(controller, current_movement_target, min_distance, delay_override) as datum_move_loop;
        atom our_pawn = controller.pawn;
        if (our_pawn == null)
            return null;
        our_pawn.RegisterSignal(loop, COMSIG_MOVELOOP_JPS_FINISHED_PATHING, nameof(mob_living_basic_bot.generate_bot_path));
        return loop;
    }
}

// tg: code/datums/ai/movement/ai_movement_jps.dm:72
public partial class datum_ai_movement_jps_bot_mulebot : datum_ai_movement_jps_bot
{
    // tg: code/datums/ai/movement/ai_movement_jps.dm:76
    public override object setup_moveloop(datum_ai_controller controller = null, atom current_movement_target = null, atom_movable moving = null, object delay = null, object min_distance = null)
    {
        return GLOB.move_manager.frustrations_move(moving,
            current_movement_target,
            delay,
            repath_delay: 0.5f * SECONDS,
            simulated_only: Dm.num(!HAS_TRAIT(controller.pawn, TRAIT_SPACEWALK)),
            max_path_length: maximum_length,
            minimum_distance: min_distance,
            access: controller.get_access() as DmList,
            subsystem: SSai_movement,
            diagonal_handling: diagonal_flags,
            extra_info: controller);
    }

    // tg: code/datums/ai/movement/ai_movement_jps.dm:91
    public override object start_moving_towards(datum_ai_controller controller = null, atom current_movement_target = null, object min_distance = null, object delay_override = null)
    {
        datum_move_loop loop = base.start_moving_towards(controller, current_movement_target, min_distance, delay_override) as datum_move_loop;
        atom our_pawn = controller.pawn;
        our_pawn.RegisterSignal(loop, COMSIG_MOVELOOP_JPS_FRUSTRATION_INCREMENTED, nameof(mob_living_basic_bot_mulebot.handle_buzzing));
        return null; // tg's proc falls off its end
    }
}
