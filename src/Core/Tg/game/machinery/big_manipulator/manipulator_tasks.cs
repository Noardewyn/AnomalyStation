// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/big_manipulator/manipulator_tasks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the big manipulator's tasks, whole: wait, stop, move, pick up, drop, throw, use the held item and interact with an
// empty hand, each on one of the eight tiles around the manipulator, with its item filters, its ordered interaction priorities, its
// settings and its serialization for the task disk. The var blocks are generated.

// tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:1
public partial class datum_manipulator_task : datum
{
    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:4
    public virtual object can_run(obj_machinery_big_manipulator manipulator = null)
    {
        return FALSE;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:7
    public virtual async DmTask run_task(obj_machinery_big_manipulator manipulator = null)
    {
        return;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:10
    public virtual object serialize()
    {
        return Dm.list(Dm.kv("type", type));
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:13
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        return;
    }
}

// ===== WAIT =====

// tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:19
public partial class datum_manipulator_task_simple_wait : datum_manipulator_task_simple
{
    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:23
    public override object can_run(obj_machinery_big_manipulator manipulator = null)
    {
        foreach (datum_manipulator_task_cargo task in Dm.each<datum_manipulator_task_cargo>(manipulator.tasks))
        {
            if (Dm.truthy(task.can_run(manipulator)))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:29
    public override async DmTask run_task(obj_machinery_big_manipulator manipulator = null)
    {
        manipulator.schedule_next_cycle(time_seconds * SECONDS);
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:32
    public override object serialize()
    {
        DmList data = (DmList)base.serialize();
        data["time_seconds"] = time_seconds;
        return data;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:37
    [DmParams("serialized_data")]
    public override async DmTask New(params object[] __extra)
    {
        DmList serialized_data = Dm.arg<DmList>(__extra, 0);
        await base.New(__extra);
        if (serialized_data != null)
            time_seconds = Dm.num(serialized_data["time_seconds"]);
        return;
    }
}

// ===== STOP =====

// tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:45
public partial class datum_manipulator_task_simple_stop : datum_manipulator_task_simple
{
    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:49
    [DmParams("serialized_data")]
    public override async DmTask New(params object[] __extra)
    {
        DmList serialized_data = Dm.arg<DmList>(__extra, 0);
        New__preset_sub_names ??= Dm.list(
            "while at it.",
            "and step away.",
            "in the name of common sense.",
            "just stop.",
            "enough of this.");
        sub_name = Dm.str(pick(New__preset_sub_names));
        if (serialized_data != null)
            sub_name = serialized_data["sub_name"] is var named && Dm.truthy(named) ? Dm.str(named) : "";
        await base.New(__extra);
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:50  var/static/list/preset_sub_names (a proc-level static, rules 10.6)
    private static ref DmList New__preset_sub_names => ref TgState.Current.datum_manipulator_task_simple_stop__New__preset_sub_names;

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:62
    public override object can_run(obj_machinery_big_manipulator manipulator = null)
    {
        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:65
    public override async DmTask run_task(obj_machinery_big_manipulator manipulator = null)
    {
        manipulator.complete_stopping_task();
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:68
    public override object serialize()
    {
        DmList data = (DmList)base.serialize();
        data["sub_name"] = sub_name;
        return data;
    }
}

// ===== MOVE =====

// tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:75
public partial class datum_manipulator_task_cargo_move : datum_manipulator_task_cargo
{
    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:78
    public override object fill_priority_list(object manipulator_tier = null)
    {
        return Dm.list();
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:81
    public override object can_run(obj_machinery_big_manipulator manipulator = null)
    {
        return is_valid();
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:84
    public override async DmTask run_task(obj_machinery_big_manipulator manipulator = null)
    {
        await manipulator.rotate_to_point(this, this, nameof(try_move));
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:87
    public virtual void try_move(obj_machinery_big_manipulator manipulator = null)
    {
        manipulator.schedule_next_cycle();
    }
}

// ===== BASE CARGO =====

// tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:92
public partial class datum_manipulator_task_cargo : datum_manipulator_task
{
    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:100
    [DmParams("new_turf", "manipulator_tier", "serialized_data")]
    public override async DmTask New(params object[] __extra)
    {
        turf new_turf = Dm.arg<turf>(__extra, 0);
        object manipulator_tier = Dm.arg<object>(__extra, 1);
        DmList serialized_data = Dm.arg<DmList>(__extra, 2);
        if (serialized_data != null)
        {
            if (serialized_data["offset"] is DmList offset)
            {
                offset_dx = offset["dx"];
                offset_dy = offset["dy"];
            }
            if (new_turf != null)
                interaction_turf = new_turf;

            should_use_filters = Dm.num(Dm.truthy(serialized_data["should_use_filters"]));
            atom_filters = serialized_data["atom_filters"] is DmList filters && Dm.truthy(filters) ? filters : Dm.list();

            if (serialized_data["interaction_priorities"] is DmList prios_data)
            {
                interaction_priorities = Dm.list();
                foreach (object prio_entry in Dm.each(prios_data))
                {
                    if (prio_entry is not DmList prio_data)
                        continue;
                    object prio_type = prio_data["type"];
                    if (!Dm.ispath(prio_type, typeof(datum_manipulator_priority)))
                        continue;
                    datum_manipulator_priority prio = (datum_manipulator_priority)Dm.@new((Type)prio_type);
                    prio.active = Dm.num(Dm.truthy(prio_data["active"]));
                    Dm.append(interaction_priorities, prio);
                }
            }

            await base.New(__extra);
            return;
        }

        if (new_turf == null)
        {
            stack_trace("New manipulator task created with no valid turf reference passed.", "code/game/machinery/big_manipulator/manipulator_tasks.dm", 126);
            qdel(this);
            return;
        }

        if (isclosedturf(new_turf))
        {
            qdel(this);
            return;
        }

        interaction_turf = new_turf;
        interaction_priorities = (DmList)fill_priority_list(manipulator_tier);
        await base.New(__extra);
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:139
    public virtual object fill_priority_list(object manipulator_tier = null)
    {
        return Dm.list();
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:142
    public virtual object find_type_priority(float? skip_anchored = null, float? respect_filters = null)
    {
        skip_anchored ??= FALSE;
        respect_filters ??= FALSE;
        atom best_candidate = null;
        float best_priority_index = INFINITY;

        foreach (atom_movable thing in Dm.each_cast<atom_movable>(interaction_turf.contents))
        {
            for (float i = 1; i <= Dm.length(interaction_priorities); i++)
            {
                if (i >= best_priority_index)
                    break;

                datum_manipulator_priority prio = (datum_manipulator_priority)interaction_priorities[i];

                if (!Dm.truthy(prio.active) || prio.atom_typepath == typeof(turf))
                    continue;

                if (!Dm.istype(thing, prio.atom_typepath))
                    continue;

                if (Dm.truthy(skip_anchored) && Dm.truthy(thing.anchored))
                    continue;

                if (isliving(thing))
                {
                    mob_living living_mob = (mob_living)thing;
                    if (living_mob.stat == DEAD)
                        continue;
                }

                if (Dm.truthy(respect_filters) && Dm.truthy(should_use_filters) && isitem(thing) && !Dm.truthy(check_filters_for_atom(thing)))
                    continue;

                best_candidate = thing;
                best_priority_index = i;

                if (best_priority_index == 1)
                    return best_candidate;
                break;
            }
        }

        for (float i = 1; i <= Dm.length(interaction_priorities); i++)
        {
            if (i >= best_priority_index)
                break;
            datum_manipulator_priority prio = (datum_manipulator_priority)interaction_priorities[i];
            if (Dm.truthy(prio.active) && prio.atom_typepath == typeof(turf))
            {
                best_candidate = interaction_turf;
                best_priority_index = i;
                break;
            }
        }

        return best_candidate;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:188
    public virtual object move_priority_up_by_index(object index = null)
    {
        if (!Dm.truthy(index))
            return FALSE;
        interaction_priorities.Swap(Dm.num(index), Dm.num(index) + 1);
        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:194
    public virtual object tick_priority_by_index(object index = null, float? reset = null)
    {
        reset ??= FALSE;
        datum_manipulator_priority target_priority = (datum_manipulator_priority)interaction_priorities[Dm.num(index) + 1];
        if (Dm.truthy(reset))
            target_priority.active = TRUE;
        else
            target_priority.active = Dm.num(!Dm.truthy(target_priority.active));
        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:202
    public virtual object is_valid()
    {
        if (interaction_turf == null)
            return FALSE;
        return Dm.num(!isclosedturf(interaction_turf));
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:207
    public virtual object check_filters_for_atom(atom_movable target = null)
    {
        if (target == null || Dm.truthy(target.anchored) || HAS_TRAIT(target, TRAIT_NODROP))
            return FALSE;

        if (!Dm.truthy(should_use_filters))
            return Dm.num(isitem(target));

        foreach (object filter_path in Dm.each(atom_filters))
        {
            if (Dm.istype(target, filter_path as Type))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:219
    public override object can_run(obj_machinery_big_manipulator manipulator = null)
    {
        return is_valid();
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:222
    public override object serialize()
    {
        DmList data = (DmList)base.serialize();
        data["offset"] = Dm.list(
            Dm.kv("dx", offset_dx),
            Dm.kv("dy", offset_dy));
        data["should_use_filters"] = should_use_filters;
        data["atom_filters"] = atom_filters;
        DmList prios = Dm.list();
        data["interaction_priorities"] = prios;
        foreach (datum_manipulator_priority prio in Dm.each_cast<datum_manipulator_priority>(interaction_priorities))
        {
            Dm.append(prios, Dm.list(Dm.list(
                Dm.kv("type", prio.type),
                Dm.kv("active", prio.active))));
        }
        return data;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:238
    public override object Destroy(float? force = null)
    {
        interaction_turf = null;
        QDEL_LIST(interaction_priorities);
        return base.Destroy(force);
    }
}

// ===== PICKUP =====

// tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:247
public partial class datum_manipulator_task_cargo_pickup : datum_manipulator_task_cargo
{
    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:251
    public override object fill_priority_list(object manipulator_tier = null)
    {
        DmList priorities = Dm.list(
            Dm.@new<datum_manipulator_priority_pickup_items>(),
            Dm.@new<datum_manipulator_priority_pickup_closets>());
        if (Dm.num(manipulator_tier) >= 4)
            Dm.append(priorities, Dm.@new<datum_manipulator_priority_pickup_humans>());
        return priorities;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:260
    public override object check_filters_for_atom(atom_movable target = null)
    {
        if (target == null || Dm.truthy(target.anchored) || HAS_TRAIT(target, TRAIT_NODROP))
            return FALSE;

        bool matched_priority = false;
        foreach (datum_manipulator_priority_pickup prio in Dm.each<datum_manipulator_priority_pickup>(interaction_priorities))
        {
            if (!Dm.truthy(prio.active))
                continue;
            if (Dm.istype(target, prio.atom_typepath))
            {
                if (ishuman(target))
                {
                    mob_living_carbon_human human_target = (mob_living_carbon_human)target;
                    if (human_target.stat == DEAD)
                        continue;
                }
                matched_priority = true;
                break;
            }
        }

        if (!matched_priority)
            return FALSE;

        if (Dm.truthy(should_use_filters) && Dm.truthy(Dm.length(atom_filters)))
        {
            foreach (object filter_path in Dm.each(atom_filters))
            {
                if (Dm.istype(target, filter_path as Type))
                    return TRUE;
            }
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:287
    public override object can_run(obj_machinery_big_manipulator manipulator = null)
    {
        if (!Dm.truthy(base.can_run(manipulator)))
            return FALSE;
        if (manipulator.held_object != null)
            return FALSE;
        foreach (atom_movable candidate in Dm.each_cast<atom_movable>(interaction_turf.contents))
        {
            if (!Dm.truthy(check_filters_for_atom(candidate)))
                continue;
            if (pickup_eagerness == PICKUP_EAGER)
                return TRUE;
            foreach (datum_manipulator_task_cargo_dropoff_base dest in Dm.each<datum_manipulator_task_cargo_dropoff_base>(manipulator.tasks))
            {
                if (Dm.truthy(dest.can_accept(candidate)))
                    return TRUE;
            }
        }
        return FALSE;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:302
    public override async DmTask run_task(obj_machinery_big_manipulator manipulator = null)
    {
        await manipulator.rotate_to_point(this, this, nameof(try_pickup));
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:305
    public virtual async DmTask try_pickup(obj_machinery_big_manipulator manipulator = null)
    {
        atom_movable selected = find_pickup_candidate(manipulator) as atom_movable;
        if (selected == null)
        {
            manipulator.nothing_ever_happens();
            return;
        }

        if (Dm.truthy(selected.anchored) || HAS_TRAIT(selected, TRAIT_NODROP))
        {
            manipulator.nothing_ever_happens();
            return;
        }

        if (isitem(selected))
        {
            obj_item selected_item = (obj_item)selected;
            if (Dm.truthy(Dm.bitand(selected_item.item_flags, Dm.bitor(ABSTRACT, DROPDEL))))
            {
                manipulator.nothing_ever_happens();
                return;
            }
        }

        await selected.forceMove(manipulator);
        manipulator.held_object = (datum_weakref)WEAKREF(selected);
        manipulator.manipulator_arm.update_claw(manipulator.held_object);
        manipulator.schedule_next_cycle();
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:325
    public override object serialize()
    {
        DmList data = (DmList)base.serialize();
        data["pickup_eagerness"] = pickup_eagerness;
        return data;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:330
    [DmParams("new_turf", "manipulator_tier", "serialized_data")]
    public override async DmTask New(params object[] __extra)
    {
        DmList serialized_data = Dm.arg<DmList>(__extra, 2);
        await base.New(__extra);
        if (serialized_data != null)
            pickup_eagerness = Dm.str(serialized_data["pickup_eagerness"]);
        return;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:336
    public virtual object find_pickup_candidate(obj_machinery_big_manipulator manipulator = null)
    {
        DmList candidates = Dm.list();

        foreach (atom_movable candidate in Dm.each_cast<atom_movable>(interaction_turf.contents))
        {
            if (Dm.truthy(candidate.anchored) || HAS_TRAIT(candidate, TRAIT_NODROP))
                continue;
            if (!Dm.truthy(check_filters_for_atom(candidate)))
                continue;
            if (pickup_eagerness == PICKUP_EAGER)
            {
                Dm.append(candidates, candidate);
                continue;
            }
            foreach (datum_manipulator_task_cargo_dropoff_base dest in Dm.each<datum_manipulator_task_cargo_dropoff_base>(manipulator.tasks))
            {
                if (Dm.truthy(dest.can_accept(candidate)))
                {
                    Dm.append(candidates, candidate);
                    break;
                }
            }
        }

        if (!Dm.truthy(Dm.length(candidates)))
            return null;

        return manipulator.master_tasking.get_next_candidate(candidates);
    }
}

// ===== BASE DROPOFF =====
// Base type for anything that accepts a `held_object`: drop, throw, use.
// Pickup iterates by this type to find a target point.

// tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:362
public partial class datum_manipulator_task_cargo_dropoff_base : datum_manipulator_task_cargo
{
    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:365
    public virtual object can_accept(atom_movable target = null)
    {
        if (!Dm.truthy(is_valid()))
            return FALSE;
        if (Dm.truthy(should_use_filters) && !Dm.truthy(check_filters_for_atom(target)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:384
    public override async DmTask run_task(obj_machinery_big_manipulator manipulator = null)
    {
        await manipulator.rotate_to_point(this, this, nameof(try_dropoff));
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:387
    public virtual async DmTask<object> try_dropoff(obj_machinery_big_manipulator manipulator = null)
    {
        obj actual_held_object = manipulator.held_object?.resolve() as obj;
        if (actual_held_object == null || actual_held_object.loc != manipulator)
        {
            manipulator.nothing_ever_happens();
            return FALSE;
        }
        await do_dropoff(manipulator);
        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:395
    public override object serialize()
    {
        DmList data = (DmList)base.serialize();
        return data;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:400
    public virtual async DmTask do_dropoff(obj_machinery_big_manipulator manipulator = null)
    {
        return;
    }
}

// tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:480
public partial class datum_manipulator_task_cargo_dropoff_base_use : datum_manipulator_task_cargo_dropoff_base
{
    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:372
    public override object can_run(obj_machinery_big_manipulator manipulator = null)
    {
        if (!Dm.truthy(base.can_run(manipulator)))
            return FALSE;
        atom_movable target = manipulator.held_object?.resolve() as atom_movable;
        if (target == null)
            return FALSE;
        if (manipulator.monkey_worker?.resolve() == null)
            return FALSE;
        if (!Dm.truthy(find_type_priority(skip_anchored, TRUE)))
            return FALSE;
        return can_accept(target);
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:486
    public override object fill_priority_list(object manipulator_tier = null)
    {
        DmList priorities = Dm.list(
            Dm.@new<datum_manipulator_priority_interact_with_living>(),
            Dm.@new<datum_manipulator_priority_interact_with_structure>(),
            Dm.@new<datum_manipulator_priority_interact_with_machinery>(),
            Dm.@new<datum_manipulator_priority_interact_with_items>(),
            Dm.@new<datum_manipulator_priority_interact_with_turf>());
        if (Dm.num(manipulator_tier) == 4)
            Dm.append(priorities, Dm.@new<datum_manipulator_priority_interact_with_vehicles>());
        return priorities;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:498
    public override object can_accept(atom_movable target = null)
    {
        if (!Dm.truthy(is_valid()))
            return FALSE;
        return Dm.num(find_type_priority(skip_anchored, TRUE) != null);
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:503
    public override object serialize()
    {
        DmList data = (DmList)base.serialize();
        data["worker_combat_mode"] = worker_combat_mode;
        data["worker_use_rmb"] = worker_use_rmb;
        data["skip_anchored"] = skip_anchored;
        return data;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:510
    [DmParams("new_turf", "manipulator_tier", "serialized_data")]
    public override async DmTask New(params object[] __extra)
    {
        DmList serialized_data = Dm.arg<DmList>(__extra, 2);
        await base.New(__extra);
        if (serialized_data != null)
        {
            worker_combat_mode = Dm.num(Dm.truthy(serialized_data["worker_combat_mode"]));
            worker_use_rmb = Dm.num(Dm.truthy(serialized_data["worker_use_rmb"]));
            skip_anchored = Dm.num(Dm.truthy(serialized_data["skip_anchored"]));
        }
        return;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:518
    public override async DmTask do_dropoff(obj_machinery_big_manipulator manipulator = null)
    {
        // tg passes the use task to try_use_thing, whose parameter is typed /cargo/interact: it reads the same-named settings by name
        await manipulator.try_use_thing(this);
    }
}

// ===== DROP =====

// tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:405
public partial class datum_manipulator_task_cargo_dropoff_base_drop : datum_manipulator_task_cargo_dropoff_base
{
    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:409
    public override object fill_priority_list(object manipulator_tier = null)
    {
        return Dm.list(
            Dm.@new<datum_manipulator_priority_drop_in_storage>(),
            Dm.@new<datum_manipulator_priority_drop_on_floor>());
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:415
    public override object can_accept(atom_movable target = null)
    {
        if (!Dm.truthy(base.can_accept(target)))
            return FALSE;

        DmList atoms_on_the_turf = interaction_turf.contents;
        switch (overflow_status)
        {
            case POINT_OVERFLOW_ALLOWED:
                return TRUE;
            case POINT_OVERFLOW_FILTERS:
                foreach (atom_movable movable_atom in Dm.each_cast<atom_movable>(atoms_on_the_turf))
                {
                    if (Dm.truthy(check_filters_for_atom(movable_atom)))
                        return FALSE;
                }
                break;
            case POINT_OVERFLOW_HELD:
                foreach (atom_movable movable_atom in Dm.each_cast<atom_movable>(atoms_on_the_turf))
                {
                    if (Dm.istype(movable_atom, target?.type))
                        return FALSE;
                }
                break;
            case POINT_OVERFLOW_FORBIDDEN:
                if (Dm.truthy(Dm.locate<obj_item>(atoms_on_the_turf)))
                    return FALSE;
                break;
        }

        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:437
    public override object serialize()
    {
        DmList data = (DmList)base.serialize();
        data["overflow_status"] = overflow_status;
        return data;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:442
    [DmParams("new_turf", "manipulator_tier", "serialized_data")]
    public override async DmTask New(params object[] __extra)
    {
        DmList serialized_data = Dm.arg<DmList>(__extra, 2);
        await base.New(__extra);
        if (serialized_data != null)
            overflow_status = Dm.str(serialized_data["overflow_status"]);
        return;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:448
    public override async DmTask do_dropoff(obj_machinery_big_manipulator manipulator = null)
    {
        await manipulator.try_drop_thing(this);
    }
}

// ===== THROW =====

// tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:453
public partial class datum_manipulator_task_cargo_dropoff_base_throw : datum_manipulator_task_cargo_dropoff_base
{
    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:457
    public override object can_accept(atom_movable target = null)
    {
        if (!Dm.truthy(is_valid()))
            return FALSE;
        if (Dm.truthy(should_use_filters) && !Dm.truthy(check_filters_for_atom(target)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:464
    public override object serialize()
    {
        DmList data = (DmList)base.serialize();
        data["throw_range"] = throw_range;
        return data;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:469
    [DmParams("new_turf", "manipulator_tier", "serialized_data")]
    public override async DmTask New(params object[] __extra)
    {
        DmList serialized_data = Dm.arg<DmList>(__extra, 2);
        await base.New(__extra);
        if (serialized_data != null)
            throw_range = Dm.num(serialized_data["throw_range"]);
        return;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:475
    public override async DmTask do_dropoff(obj_machinery_big_manipulator manipulator = null)
    {
        await manipulator.throw_thing(this);
    }
}

// ===== INTERACT (empty hand) =====

// tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:523
public partial class datum_manipulator_task_cargo_interact : datum_manipulator_task_cargo
{
    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:529
    public override object fill_priority_list(object manipulator_tier = null)
    {
        DmList priorities = Dm.list(
            Dm.@new<datum_manipulator_priority_interact_with_living>(),
            Dm.@new<datum_manipulator_priority_interact_with_structure>(),
            Dm.@new<datum_manipulator_priority_interact_with_machinery>(),
            Dm.@new<datum_manipulator_priority_interact_with_items>(),
            Dm.@new<datum_manipulator_priority_interact_with_turf>());
        if (Dm.num(manipulator_tier) == 4)
            Dm.append(priorities, Dm.@new<datum_manipulator_priority_interact_with_vehicles>());
        return priorities;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:541
    public override object can_run(obj_machinery_big_manipulator manipulator = null)
    {
        if (!Dm.truthy(base.can_run(manipulator)))
            return FALSE;
        return Dm.num(find_type_priority(skip_anchored, TRUE) != null);
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:546
    public override async DmTask run_task(obj_machinery_big_manipulator manipulator = null)
    {
        await manipulator.rotate_to_point(this, this, nameof(try_interact));
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:549
    public override object serialize()
    {
        DmList data = (DmList)base.serialize();
        data["worker_combat_mode"] = worker_combat_mode;
        data["worker_use_rmb"] = worker_use_rmb;
        data["skip_anchored"] = skip_anchored;
        return data;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:556
    [DmParams("new_turf", "manipulator_tier", "serialized_data")]
    public override async DmTask New(params object[] __extra)
    {
        DmList serialized_data = Dm.arg<DmList>(__extra, 2);
        await base.New(__extra);
        if (serialized_data != null)
        {
            worker_combat_mode = Dm.num(Dm.truthy(serialized_data["worker_combat_mode"]));
            worker_use_rmb = Dm.num(Dm.truthy(serialized_data["worker_use_rmb"]));
            skip_anchored = Dm.num(Dm.truthy(serialized_data["skip_anchored"]));
        }
        return;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:564
    public virtual async DmTask try_interact(obj_machinery_big_manipulator manipulator = null)
    {
        atom_movable held = manipulator.held_object?.resolve() as atom_movable;
        if (held != null)
        {
            if (manipulator.monkey_worker?.resolve() == null)
            {
                manipulator.nothing_ever_happens();
                return;
            }
            await manipulator.try_use_thing(this);
        }
        else
            await manipulator.use_thing_with_empty_hand(this);
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/big_manipulator/manipulator_tasks.dm:50  var/static/list/preset_sub_names
    internal DmList datum_manipulator_task_simple_stop__New__preset_sub_names;
}
