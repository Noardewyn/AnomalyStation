// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/big_manipulator/big_manipulator_interactions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the big manipulator's work cycle, whole: the next task by the tasking strategy, the arm turned toward its tile in
// 45 degree steps (animate on the arm's transform, each step 0.3 s over the speed multiplier), the pick up, drop (into storage or on
// the floor), throw, use with the held item and interact with an empty hand (the buckled monkey's), and the wait for something to
// enter or leave a task's tile when nothing can run. The var blocks are generated.
//
// The slot of try_use_thing types its task `/datum/manipulator_task/cargo/interact` (:166), but the use task (a /cargo/dropoff_base
// subtype) calls it too, and tg reads the same-named settings of both by name: the parameter is their common `/cargo` type (rules
// 3.5's "-" cell; translation-rules Appendix A) and the settings are read from whichever of the two it is.

// tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:1
public partial class obj_machinery_big_manipulator : obj_machinery
{
    /// We have no tasks to execute for some reason. Waits for a turf signal to retry.
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:2
    public virtual object nothing_ever_happens()
    {
        if (Dm.truthy(stopping))
        {
            complete_stopping_task();
            return FALSE;
        }

        current_task = null;
        waiting_for_signal = TRUE;
        register_task_turf_signals();

        return FALSE;
    }

    /// A signal ran or some settings changed; checking if we can run the tasks now.
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:15
    public virtual async DmTask something_happened()
    {
        next_cycle_scheduled = FALSE;
        await step_tasks();
    }

    /// Runs the next task. Or doesn't.
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:20
    public virtual async DmTask step_tasks()
    {
        if (!Dm.truthy(on) || Dm.truthy(stopping))
            return;
        next_cycle_scheduled = FALSE;
        if (Dm.truthy(waiting_for_signal))
        {
            unregister_task_turf_signals();
            waiting_for_signal = FALSE;
        }
        if (!Dm.truthy(Dm.length(tasks)))
        {
            nothing_ever_happens();
            return;
        }
        datum_manipulator_task next_task = master_tasking.get_next_task(tasks, this) as datum_manipulator_task;
        if (next_task == null)
        {
            nothing_ever_happens();
            return;
        }
        current_task = next_task;
        SStgui.update_uis(this);
        await next_task.run_task(this);
    }

    /// Attempts to launch the work cycle. Should only be ran on pressing the "Run" button.
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:39
    public virtual async DmTask<object> try_kickstart(mob user = null)
    {
        if (!Dm.truthy(on) || !Dm.truthy(anchored) || Dm.truthy(stopping) || current_task != null)
            return FALSE;

        if (!Dm.truthy(await use_energy(active_power_usage, force: FALSE)))
        {
            on = FALSE;
            balloon_alert_to_viewers("not enough power!");
            return FALSE;
        }

        next_cycle_scheduled = FALSE;
        await step_tasks();
        return null;
    }

    /// Safely schedules the next step to prevent overlapping.
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:52
    public virtual void schedule_next_cycle(float? time_seconds = null)
    {
        time_seconds ??= BASE_TASK_DELAY;
        if (Dm.truthy(next_cycle_scheduled) || Dm.truthy(stopping))
            return;

        next_cycle_scheduled = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(step_tasks)), time_seconds, file: "code/game/machinery/big_manipulator/big_manipulator_interactions.dm", line: 57);
    }

    /// Rotates the manipulator arm to face the target task's turf.
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:60
    public virtual async DmTask<object> rotate_to_point(datum_manipulator_task_cargo target_task = null, object callback_object = null, object callback = null)
    {
        if (Dm.truthy(stopping))
            return null;

        if (target_task == null)
            return FALSE;

        float target_dir = get_dir(get_turf(this), target_task.interaction_turf);
        float target_angle = Dm.num(dir2angle(target_dir));
        float current_angle = Dm.num(((manipulator_arm.transform as matrix) ?? matrix()).get_angle());
        float angle_diff = Dm.num(closer_angle_difference(current_angle, target_angle));

        float num_rotations = round(abs(angle_diff) / 45);

        if (!Dm.truthy(num_rotations))
        {
            datum_callback cb = Dm.@new<datum_callback>(callback_object, callback, this);
            await cb.Invoke();
            return TRUE;
        }

        float rotation_step = 45 * sign(angle_diff);
        do_step_rotation(target_task, callback_object, callback, current_angle, target_angle, rotation_step);
        return TRUE;
    }

    /// Does a 45 degree step, animating the claw
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:83
    public virtual void do_step_rotation(datum_manipulator_task_cargo target_task = null, object callback_object = null, object callback = null, object current_angle = null, object target_angle = null, object rotation_step = null)
    {
        if (Dm.truthy(stopping))
            return;

        float angle_diff = Dm.num(closer_angle_difference(current_angle, target_angle));
        if (abs(angle_diff) < abs(Dm.num(rotation_step)))
        {
            matrix final_matrix = matrix();
            final_matrix.Turn(Dm.num(target_angle));
            animate(manipulator_arm, Dm.named("transform", final_matrix), Dm.named("time", BASE_INTERACTION_TIME / speed_multiplier));
            _addtimer(Dm.@new<datum_callback>(callback_object, callback, this), BASE_INTERACTION_TIME / speed_multiplier, file: "code/game/machinery/big_manipulator/big_manipulator_interactions.dm", line: 92);
            return;
        }

        float next_angle = Dm.num(current_angle) + Dm.num(rotation_step);
        matrix next_matrix = matrix();
        next_matrix.Turn(next_angle);
        animate(manipulator_arm, Dm.named("transform", next_matrix), Dm.named("time", BASE_INTERACTION_TIME / speed_multiplier));

        _addtimer(Dm.@new<datum_callback>(this, nameof(do_step_rotation), target_task, callback_object, callback, next_angle, target_angle, rotation_step), BASE_INTERACTION_TIME / speed_multiplier, file: "code/game/machinery/big_manipulator/big_manipulator_interactions.dm", line: 100);
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:102
    public virtual async DmTask<object> try_drop_thing(datum_manipulator_task_cargo_dropoff_base_drop destination_task = null)
    {
        object drop_endpoint = destination_task.find_type_priority();
        obj actual_held_object = held_object?.resolve() as obj;

        if (drop_endpoint == null)
            return FALSE;

        atom drop_target = (atom)drop_endpoint;
        if (drop_target.atom_storage != null && actual_held_object != null && !Dm.truthy(drop_target.atom_storage.attempt_insert(actual_held_object as obj_item, @override: TRUE, messages: FALSE)))
        {
            await actual_held_object.forceMove((atom)drop_target.drop_location());
            finish_manipulation();
            return TRUE;
        }

        if (actual_held_object != null)
            await actual_held_object.forceMove(drop_target);
        finish_manipulation();
        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:118
    public virtual async DmTask try_use_thing(datum_manipulator_task_cargo destination_task = null)
    {
        if (Dm.truthy(stopping) || Dm.truthy(use_in_progress))
            return;

        use_in_progress = TRUE;

        obj obj_resolve = held_object?.resolve() as obj;
        mob_living_carbon_human_species_monkey monkey_resolve = monkey_worker?.resolve() as mob_living_carbon_human_species_monkey;
        turf destination_turf = destination_task.interaction_turf;

        if (obj_resolve == null || QDELETED(obj_resolve) || obj_resolve.loc != this)
        {
            use_in_progress = FALSE;
            finish_manipulation();
            return;
        }

        if (monkey_resolve == null || destination_turf == null)
        {
            use_in_progress = FALSE;
            finish_manipulation();
            return;
        }

        if (monkey_resolve.loc != this)
        {
            use_in_progress = FALSE;
            finish_manipulation();
            return;
        }

        obj_item held_item = (obj_item)obj_resolve;
        atom type_to_use = destination_task.find_type_priority(task_skip_anchored(destination_task), TRUE) as atom;

        if (type_to_use == null)
        {
            use_in_progress = FALSE;
            finish_manipulation();
            return;
        }

        if (isitem(type_to_use) && !Dm.truthy(destination_task.check_filters_for_atom((atom_movable)type_to_use)))
        {
            use_in_progress = FALSE;
            finish_manipulation();
            return;
        }

        if (destination_task is datum_manipulator_task_cargo_interact && Dm.truthy(destination_task.should_use_filters) && isitem(type_to_use) && !Dm.truthy(destination_task.check_filters_for_atom((atom_movable)type_to_use)))
        {
            use_in_progress = FALSE;
            finish_manipulation();
            return;
        }

        atom original_loc = held_item.loc;
        turf target_turf = get_turf(type_to_use);

        // Temporarily move monkey adjacent to target for Adjacency() checks
        if (target_turf != null && get_turf(this) != target_turf)
        {
            turf adjacent_turf = get_step(target_turf, get_dir(target_turf, get_turf(this)));
            if (adjacent_turf != null && !isclosedturf(adjacent_turf))
                await monkey_resolve.forceMove(adjacent_turf);
        }

        if (Dm.truthy(await monkey_resolve.put_in_active_hand(held_item)))
        {
            if (Dm.truthy(held_item.GetComponent(typeof(datum_component_two_handed))))
                await held_item.attack_self(monkey_resolve);

            float old_combat_mode = Dm.num(monkey_resolve.combat_mode);
            monkey_resolve.combat_mode = task_worker_combat_mode(destination_task);
            await held_item.melee_attack_chain(monkey_resolve, type_to_use, Dm.list(Dm.kv(RIGHT_CLICK, Dm.truthy(task_worker_use_rmb(destination_task)) ? TRUE : FALSE)));
            monkey_resolve.combat_mode = old_combat_mode;
            await do_attack_animation(destination_turf);
            await manipulator_arm.do_attack_animation(destination_turf);
        }

        await restore_monkey_tracking(monkey_resolve);

        if (QDELETED(held_item) || held_item == null || (held_item.loc != monkey_resolve && held_item.loc != original_loc))
        {
            held_object = null;
            manipulator_arm.update_claw(null);
            use_in_progress = FALSE;
            finish_manipulation();
            return;
        }

        if (held_item.loc == monkey_resolve)
            await held_item.forceMove(original_loc);

        use_in_progress = FALSE;
        current_task = null;
        SStgui.update_uis(this);
        if (Dm.truthy(stopping))
        {
            complete_stopping_task();
            return;
        }
        schedule_next_cycle();
    }

    // The use and interact tasks' same-named settings that tg reads by name through try_use_thing's /cargo/interact-typed parameter.
    private static float task_skip_anchored(datum_manipulator_task_cargo task) =>
        task is datum_manipulator_task_cargo_interact interact ? interact.skip_anchored : (task as datum_manipulator_task_cargo_dropoff_base_use)?.skip_anchored ?? 0;

    private static float task_worker_combat_mode(datum_manipulator_task_cargo task) =>
        task is datum_manipulator_task_cargo_interact interact ? interact.worker_combat_mode : (task as datum_manipulator_task_cargo_dropoff_base_use)?.worker_combat_mode ?? 0;

    private static float task_worker_use_rmb(datum_manipulator_task_cargo task) =>
        task is datum_manipulator_task_cargo_interact interact ? interact.worker_use_rmb : (task as datum_manipulator_task_cargo_dropoff_base_use)?.worker_use_rmb ?? 0;

    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:200
    public virtual async DmTask throw_thing(datum_manipulator_task_cargo_dropoff_base_throw throw_task = null)
    {
        turf drop_turf = throw_task.interaction_turf;
        atom_movable held_atom = held_object?.resolve() as atom_movable;

        await held_atom.forceMove(drop_turf);
        await do_attack_animation(drop_turf);
        await manipulator_arm.do_attack_animation(drop_turf);

        if (isliving(held_atom) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            held_atom.dir = get_dir(get_turf(held_atom), get_turf(this));
            finish_manipulation();
            return;
        }

        await held_atom.throw_at(get_edge_target_turf(get_turf(this), get_dir(get_turf(this), get_turf(held_atom))) as atom, throw_task.throw_range, 2);
        finish_manipulation();
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:216
    public virtual async DmTask use_thing_with_empty_hand(datum_manipulator_task_cargo_interact destination_task = null)
    {
        mob_living_carbon_human_species_monkey monkey_resolve = monkey_worker?.resolve() as mob_living_carbon_human_species_monkey;
        if (monkey_resolve == null)
        {
            finish_manipulation();
            return;
        }

        atom type_to_use = destination_task.find_type_priority(destination_task.skip_anchored, TRUE) as atom;
        if (type_to_use == null)
        {
            finish_manipulation();
            return;
        }

        if (Dm.truthy(destination_task.should_use_filters) && isitem(type_to_use) && !Dm.truthy(destination_task.check_filters_for_atom((atom_movable)type_to_use)))
        {
            finish_manipulation();
            return;
        }

        // temporarily move monkey adjacent to target for Adjacency() checks
        turf target_turf = get_turf(type_to_use);
        if (target_turf != null && get_turf(this) != target_turf)
        {
            turf adjacent_turf = get_step(target_turf, get_dir(target_turf, get_turf(this)));
            if (adjacent_turf != null && !isclosedturf(adjacent_turf))
                await monkey_resolve.forceMove(adjacent_turf);
        }

        float old_combat_mode = Dm.num(monkey_resolve.combat_mode);
        monkey_resolve.combat_mode = destination_task.worker_combat_mode;
        await monkey_resolve.UnarmedAttack(type_to_use, modifiers: Dm.list(Dm.kv(RIGHT_CLICK, Dm.truthy(destination_task.worker_use_rmb) ? TRUE : FALSE)));
        monkey_resolve.combat_mode = old_combat_mode;

        turf dest_turf = destination_task.interaction_turf;
        if (dest_turf != null)
        {
            await do_attack_animation(dest_turf);
            await manipulator_arm.do_attack_animation(dest_turf);
        }

        await restore_monkey_tracking(monkey_resolve);

        finish_manipulation();
    }

    /// Restores monkey tracking after a temporary move outside src cleared it via Exited.
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:256
    public virtual async DmTask restore_monkey_tracking(mob_living_carbon_human_species_monkey monkey = null)
    {
        if (monkey.loc != this)
            await monkey.forceMove(this);
        if (monkey_worker?.resolve() != null)
            return;
        monkey_worker = (datum_weakref)WEAKREF(monkey);
        Dm.append(manipulator_arm.vis_contents, monkey);
        monkey.add_offsets(
            type,
            x_add: 32 + Dm.num(manipulator_arm.calculate_item_offset(TRUE, pixels_to_offset: 16)),
            y_add: 32 + Dm.num(manipulator_arm.calculate_item_offset(FALSE, pixels_to_offset: 16)));
    }

    /// Completes the current manipulation action and schedules the next step.
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:270
    public virtual void finish_manipulation()
    {
        held_object = null;
        manipulator_arm.update_claw(null);
        current_task = null;

        SStgui.update_uis(this);

        if (Dm.truthy(stopping))
        {
            complete_stopping_task();
            return;
        }

        schedule_next_cycle();
    }

    /// Completes the stopping task and transitions to idle
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:284
    public virtual void complete_stopping_task()
    {
        on = FALSE;
        stopping = FALSE;
        next_cycle_scheduled = FALSE;
        current_task = null;
        unregister_task_turf_signals();
        waiting_for_signal = FALSE;
        SStgui.update_uis(this);
    }

    /// Registers enter/exit signals on all unique cargo task turfs.
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:295
    public virtual void register_task_turf_signals()
    {
        unregister_task_turf_signals();
        foreach (datum_manipulator_task_cargo task in Dm.each<datum_manipulator_task_cargo>(tasks))
        {
            if (task.interaction_turf == null || Dm.@in(task.interaction_turf, signal_turfs))
                continue;
            Dm.append(signal_turfs, task.interaction_turf);
            RegisterSignals(task.interaction_turf, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_EXITED), nameof(on_task_turf_changed));
        }
    }

    /// Unregisters all previously registered turf signals.
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:304
    public virtual void unregister_task_turf_signals()
    {
        foreach (turf t in Dm.each<turf>(signal_turfs))
            UnregisterSignal(t, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_EXITED));
        signal_turfs = Dm.list();
    }

    /// Fires when something enters or leaves a watched task turf.
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:310
    public virtual void on_task_turf_changed(datum source = null)
    {
        if (!Dm.truthy(on) || Dm.truthy(stopping) || !Dm.truthy(waiting_for_signal))
            return;
        Waiting.NoSleep(something_happened());
    }

    /// Drop the held atom.
    // tg: code/game/machinery/big_manipulator/big_manipulator_interactions.dm:316
    public virtual async DmTask drop_held_atom()
    {
        if (held_object == null)
            return;
        obj obj_resolve = held_object?.resolve() as obj;
        if (obj_resolve != null)
            await obj_resolve.forceMove(get_turf(obj_resolve));
        finish_manipulation();
    }
}
