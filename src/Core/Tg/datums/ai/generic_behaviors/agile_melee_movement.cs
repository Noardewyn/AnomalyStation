// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/agile_melee_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

///Yakety sax type melee movement; If you can attack (next_move off cooldown), then get in range, otherwise, move inbetween standdoff_min and max, if within the band; randomly jitter in the band.
// tg: code/datums/ai/generic_behaviors/agile_melee_movement.dm:13
public partial class datum_bt_node_ai_behavior_agile_melee_movement : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/agile_melee_movement.dm:2-10 (file-local)
    /// Not moving under our own direction yet.
    private const int AGILE_MELEE_MODE_NONE = 0;
    /// Beelining into melee range, min distance 1.
    private const int AGILE_MELEE_MODE_CLOSING = 1;
    /// Backstepping out of melee range, min distance 0.
    private const int AGILE_MELEE_MODE_RETREATING = 2;
    /// Shuffling around inside the standoff band, min distance 0.
    private const int AGILE_MELEE_MODE_JITTERING = 3;
    /// Standing still for a beat after landing a hit.
    private const int AGILE_MELEE_MODE_HOLDING = 4;

    // tg: code/datums/ai/generic_behaviors/agile_melee_movement.dm:33
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return FALSE;
        if (isliving(controller.pawn))
        {
            mob_living living_pawn = (mob_living)controller.pawn;
            last_next_move = Dm.num(living_pawn.next_move);
        }
        RegisterSignal(controller.pawn, COMSIG_MOB_AI_MOVEMENT_FAILED, nameof(on_movement_failed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_behaviors/agile_melee_movement.dm:43
    [SignalHandler]
    public virtual void on_movement_failed(atom source = null)
    {
        movement_failed = TRUE;
    }

    // tg: code/datums/ai/generic_behaviors/agile_melee_movement.dm:47
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (Dm.truthy(movement_failed))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        mob pawn = controller.pawn as mob;
        float distance = get_dist(pawn, target);

        if (Dm.truthy(wait_after_attack) && isliving(pawn))
        {
            mob_living living_pawn = (mob_living)pawn;
            if (Dm.num(living_pawn.next_move) > last_next_move)
                hold_until = world.time + wait_after_attack;
            last_next_move = Dm.num(living_pawn.next_move);
        }

        if (hold_until > world.time)
        {
            set_movement_mode(controller, AGILE_MELEE_MODE_HOLDING, Dm.initial(controller, (datum_ai_controller c) => c.ai_movement));
            return AI_BEHAVIOR_INSTANT;
        }

        if (Dm.truthy(can_swing(pawn)) || distance > standoff_max)
        {
            close_in(controller, target);
            return AI_BEHAVIOR_INSTANT;
        }

        if (distance < standoff_min)
        {
            Waiting.NoSleep(retreat(controller, target));
            return AI_BEHAVIOR_INSTANT;
        }

        Waiting.NoSleep(jitter(controller, target));
        return AI_BEHAVIOR_INSTANT;
    }

    // tg: code/datums/ai/generic_behaviors/agile_melee_movement.dm:79
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        UnregisterSignal(controller.pawn, COMSIG_MOB_AI_MOVEMENT_FAILED);
        movement_failed = FALSE;
        current_mode = AGILE_MELEE_MODE_NONE;
        last_next_move = 0;
        hold_until = 0;
        controller.movement.stop_moving_towards(controller);
        controller.change_ai_movement_type(Dm.initial(controller, (datum_ai_controller c) => c.ai_movement));
        await base.finish_action(controller, succeeded);
    }

    /// Whether our melee attack is off cooldown, meaning it's worth being adjacent right now.
    // tg: code/datums/ai/generic_behaviors/agile_melee_movement.dm:90
    public virtual object can_swing(mob pawn = null)
    {
        if (!isliving(pawn))
            return TRUE;
        mob_living living_pawn = (mob_living)pawn;
        return Dm.num(world.time >= Dm.num(living_pawn.next_move));
    }

    ///Switches which mode is driving our movement, tearing the current moveloop down first.
    // tg: code/datums/ai/generic_behaviors/agile_melee_movement.dm:98
    public virtual void set_movement_mode(datum_ai_controller controller = null, object mode = null, object movement_type = null)
    {
        if (Dm.eq(mode, current_mode))
            return;
        controller.movement.stop_moving_towards(controller);
        controller.change_ai_movement_type(movement_type);
        current_mode = Dm.num(mode);
    }

    /// Beelines into melee range. Retargeting to a new target is handled inside start_moving_towards().
    // tg: code/datums/ai/generic_behaviors/agile_melee_movement.dm:106
    public virtual void close_in(datum_ai_controller controller = null, atom target = null)
    {
        set_movement_mode(controller, AGILE_MELEE_MODE_CLOSING, Dm.initial(controller, (datum_ai_controller c) => c.ai_movement));
        controller.movement.start_moving_towards(controller, target, 1);
    }

    /// Steps one tile away from target using backstep avoidance, falling back to shuffled directions if blocked.
    // tg: code/datums/ai/generic_behaviors/agile_melee_movement.dm:111
    public virtual async DmTask<object> retreat(datum_ai_controller controller = null, atom target = null)
    {
        set_movement_mode(controller, AGILE_MELEE_MODE_RETREATING, typeof(datum_ai_movement_basic_avoidance_backstep));
        mob pawn = controller.pawn as mob;
        pawn.face_atom(target);
        turf next_step = get_step_away(pawn, target);
        if (next_step != null && !Dm.truthy(await next_step.is_blocked_turf(exclude_mobs: TRUE)))
        {
            controller.movement.start_moving_towards(controller, next_step, 0, controller.movement_delay);
            return TRUE;
        }
        DmList all_dirs = GLOB.alldirs.Copy();
        Dm.remove(ref all_dirs, get_dir(pawn, next_step));
        Dm.remove(ref all_dirs, get_dir(pawn, target));
        shuffle_inplace(all_dirs);
        foreach (object dir in Dm.each(all_dirs))
        {
            next_step = get_step(pawn, Dm.num(dir));
            if (next_step != null && !Dm.truthy(await next_step.is_blocked_turf(exclude_mobs: TRUE)))
            {
                controller.movement.start_moving_towards(controller, next_step, 0, controller.movement_delay);
                return TRUE;
            }
        }
        return FALSE;
    }

    /// Takes one step to a random adjacent turf, anywhere that keeps us inside the standoff band.
    // tg: code/datums/ai/generic_behaviors/agile_melee_movement.dm:131
    public virtual async DmTask jitter(datum_ai_controller controller = null, atom target = null)
    {
        if (!prob(jitter_chance))
            return;

        mob pawn = controller.pawn as mob;
        DmList candidates = Dm.list();
        foreach (object direction in Dm.each(GLOB.alldirs))
        {
            turf step_turf = get_step(pawn, Dm.num(direction));
            if (step_turf == null || Dm.truthy(await step_turf.is_blocked_turf(exclude_mobs: TRUE)))
                continue;
            float step_distance = get_dist(step_turf, target);
            if (step_distance < standoff_min || step_distance > standoff_max)
                continue;
            Dm.append(ref candidates, step_turf);
        }

        if (!Dm.truthy(length(candidates)))
            return;

        set_movement_mode(controller, AGILE_MELEE_MODE_JITTERING, Dm.initial(controller, (datum_ai_controller c) => c.ai_movement));
        pawn.face_atom(target);
        controller.movement.start_moving_towards(controller, pick(candidates) as atom, 0, controller.movement_delay);
    }
}
