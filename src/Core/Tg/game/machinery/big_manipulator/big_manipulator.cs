// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/big_manipulator/big_manipulator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the big manipulator, whole but for the screentip context (a live-partial row, D264). Built from its
// board with a servo (the servo's tier sets its speed range, its task limit and its colour), anchored with a wrench, it runs tg's
// BigManipulator window: the run button (a right click too), the speed, drop and unbuckle, the task disk, and the task list with tg's
// eight task kinds, each on one of the eight tiles around it, with its settings, filters and priorities. An ID card locks it; a monkey
// buckled into it (mouse drop) does the use and interact tasks. The wires: the power button and the drop. The var blocks are generated.
//
// The slot of try_press_on types its user `mob/living/carbon/human` (:404), but the right click and the window pass any mob, and a null
// user would turn the machine on without its checks: it is `mob` (rules 3.5's "-" cell; translation-rules Appendix A).

// tg: code/game/machinery/big_manipulator/big_manipulator.dm:1
public partial class obj_machinery_big_manipulator : obj_machinery
{
    /// Attempts to find a suitable turf near the manipulator for creating a cargo task.
    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:58
    public virtual object find_suitable_turf()
    {
        turf @base = get_turf(this);
        foreach (turf checked_turf in Dm.each<turf>(orange(@base, 1)))
        {
            if (!isclosedturf(checked_turf))
                return checked_turf;
        }
        return null;
    }

    /// Attempts to create a new task and assign it to the list.
    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:66
    public virtual object create_new_task(mob user = null, object task_type = null, turf new_turf = null)
    {
        if (Dm.length(tasks) >= interaction_point_limit)
        {
            balloon_alert(user, "task limit reached!");
            return FALSE;
        }

        datum_stock_part_servo locate_servo = Dm.locate<datum_stock_part_servo>(component_parts);
        object manipulator_tier = locate_servo != null ? locate_servo.tier : 1;

        datum_manipulator_task new_task = null;
        bool needs_turf = Dm.@in(task_type, Dm.list(TASK_TYPE_PICKUP, TASK_TYPE_DROP, TASK_TYPE_THROW, TASK_TYPE_USE, TASK_TYPE_INTERACT, TASK_TYPE_MOVE));

        if (needs_turf)
        {
            if (new_turf == null)
                new_turf = find_suitable_turf() as turf;
            if (new_turf == null)
                return FALSE;
        }

        switch (task_type)
        {
            case TASK_TYPE_PICKUP:
                new_task = Dm.@new<datum_manipulator_task_cargo_pickup>(new_turf, manipulator_tier);
                break;
            case TASK_TYPE_DROP:
                new_task = Dm.@new<datum_manipulator_task_cargo_dropoff_base_drop>(new_turf, manipulator_tier);
                break;
            case TASK_TYPE_THROW:
                new_task = Dm.@new<datum_manipulator_task_cargo_dropoff_base_throw>(new_turf, manipulator_tier);
                break;
            case TASK_TYPE_USE:
                new_task = Dm.@new<datum_manipulator_task_cargo_dropoff_base_use>(new_turf, manipulator_tier);
                break;
            case TASK_TYPE_INTERACT:
                new_task = Dm.@new<datum_manipulator_task_cargo_interact>(new_turf, manipulator_tier);
                break;
            case TASK_TYPE_MOVE:
                new_task = Dm.@new<datum_manipulator_task_cargo_move>(new_turf, manipulator_tier);
                break;
            case TASK_TYPE_WAIT:
                new_task = Dm.@new<datum_manipulator_task_simple_wait>();
                break;
            case TASK_TYPE_STOP:
                new_task = Dm.@new<datum_manipulator_task_simple_stop>();
                break;
        }

        if (QDELETED(new_task))
            return FALSE;

        Dm.append(tasks, new_task);

        if (new_task is datum_manipulator_task_cargo cargo_task)
        {
            cargo_task.offset_dx = new_turf.x - x;
            cargo_task.offset_dy = new_turf.y - y;
        }

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && new_task is datum_manipulator_task_cargo)
        {
            if (new_task is datum_manipulator_task_cargo_pickup pickup)
                Dm.append(pickup.interaction_priorities, Dm.@new<datum_manipulator_priority_pickup_humans>());
        }

        return new_task;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:118
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_manipulator_arm();
        Waiting.NoSleep(process_upgrades());
        if (Dm.truthy(on))
            Waiting.NoSleep(toggle_power_state(null));
        set_wires(Dm.@new<datum_wires_big_manipulator>(this));
        register_context();

        update_strategies();
        return dot;
    }

    /// Checks the component tiers, adjusting the properties of the manipulator.
    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:129
    public virtual async DmTask process_upgrades()
    {
        datum_stock_part_servo locate_servo = Dm.locate<datum_stock_part_servo>(component_parts);
        if (locate_servo == null)
            return;

        float manipulator_tier = Dm.num(locate_servo.tier);
        if (manipulator_tier <= 1)
        {
            min_speed_multiplier = MIN_SPEED_MULTIPLIER_TIER_1;
            max_speed_multiplier = MAX_SPEED_MULTIPLIER_TIER_1;
            interaction_point_limit = MAX_TASKS_TIER_1;
            await set_greyscale(COLOR_YELLOW);
            if (manipulator_arm != null)
                await manipulator_arm.set_greyscale(COLOR_YELLOW);
        }
        else if (manipulator_tier == 2)
        {
            min_speed_multiplier = MIN_SPEED_MULTIPLIER_TIER_2;
            max_speed_multiplier = MAX_SPEED_MULTIPLIER_TIER_2;
            interaction_point_limit = MAX_TASKS_TIER_2;
            await set_greyscale(COLOR_ORANGE);
            if (manipulator_arm != null)
                await manipulator_arm.set_greyscale(COLOR_ORANGE);
        }
        else if (manipulator_tier == 3)
        {
            min_speed_multiplier = MIN_SPEED_MULTIPLIER_TIER_3;
            max_speed_multiplier = MAX_SPEED_MULTIPLIER_TIER_3;
            interaction_point_limit = MAX_TASKS_TIER_3;
            await set_greyscale(COLOR_RED);
            if (manipulator_arm != null)
                await manipulator_arm.set_greyscale(COLOR_RED);
        }
        else if (manipulator_tier >= 4)
        {
            min_speed_multiplier = MIN_SPEED_MULTIPLIER_TIER_4;
            max_speed_multiplier = MAX_SPEED_MULTIPLIER_TIER_4;
            interaction_point_limit = MAX_TASKS_TIER_4;
            await set_greyscale(COLOR_PURPLE);
            if (manipulator_arm != null)
                await manipulator_arm.set_greyscale(COLOR_PURPLE);
        }

        active_power_usage = BASE_MACHINE_ACTIVE_CONSUMPTION * BASE_POWER_USAGE * manipulator_tier;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:164
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        mob monkey_resolve = monkey_worker?.resolve() as mob;
        if (monkey_resolve != null)
            Dm.append(ref dot, I18n.T("You can see a poor [] buckled to []. You wonder if it's getting paid enough.", monkey_resolve.name, this));
        return dot;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:171
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await try_press_on(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:175
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await eject_task_disk(user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:205
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        if (task_disk != null)
        {
            await task_disk.forceMove((atom)drop_location());
            task_disk = null;
        }
        unregister_task_turf_signals();
        QDEL_NULL(ref manipulator_arm);
        QDEL_LIST(tasks);
        id_lock = null;
        await base.atom_deconstruct(disassembled);
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:215
    public override object Destroy(float? force = null)
    {
        unregister_task_turf_signals();
        QDEL_NULL(ref task_disk);
        QDEL_NULL(ref manipulator_arm);
        QDEL_LIST(tasks);
        id_lock = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:223
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (monkey_worker == null)
            return dot;

        mob_living_carbon_human_species_monkey poor_monkey = monkey_worker.resolve() as mob_living_carbon_human_species_monkey;
        if (gone != poor_monkey)
            return dot;

        Dm.remove(manipulator_arm.vis_contents, poor_monkey);
        poor_monkey.remove_offsets(type);
        monkey_worker = null;
        return dot;
    }

    /// Removes an invalid task from the list.
    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:238
    public virtual void remove_invalid_task(datum_manipulator_task task = null)
    {
        if (task == null)
            return;
        Dm.remove(tasks, task);
        qdel(task);
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:244
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;

        balloon_alert(user, "overloaded");
        obj_flags = Dm.bitor(obj_flags, EMAGGED);

        foreach (datum_manipulator_task_cargo_pickup pickup_task in Dm.each<datum_manipulator_task_cargo_pickup>(tasks))
            Dm.append(pickup_task.interaction_priorities, Dm.@new<datum_manipulator_priority_pickup_humans>());

        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:257
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool, time: 1 * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:262
    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        if (Dm.truthy(on) || Dm.truthy(stopping))
        {
            to_chat(user, span_warning(I18n.T("[] is activated!", this)));
            return FAILED_UNFASTEN;
        }
        return base.can_be_unfasten_wrench(user, silent);
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:268
    public override async DmTask<object> default_unfasten_wrench(mob user = null, obj_item wrench = null, float? time = null)
    {
        object dot = await base.default_unfasten_wrench(user, wrench, time);
        if (Dm.eq(dot, SUCCESSFUL_UNFASTEN))
        {
            if (Dm.truthy(anchored))
                validate_all_tasks();
        }
        return dot;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:274
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:277
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:280
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (Dm.istype(tool, typeof(obj_item_disk_manipulator)))
        {
            if (Dm.truthy(on) || Dm.truthy(stopping))
            {
                balloon_alert(user, "turn it off first!");
                return ITEM_INTERACT_BLOCKING;
            }
            if (task_disk != null)
            {
                await task_disk.forceMove((atom)drop_location());
                task_disk = null;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            task_disk = (obj_item_disk_manipulator)tool;
            balloon_alert(user, "disk inserted");
            SStgui.update_uis(this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (isidcard(tool))
        {
            if (id_lock == null)
            {
                id_lock = (datum_weakref)WEAKREF(tool);
                balloon_alert(user, "successfully locked");
                return ITEM_INTERACT_SUCCESS;
            }
            if (tool != id_lock.resolve())
            {
                balloon_alert(user, "locked by another id");
                return ITEM_INTERACT_BLOCKING;
            }
            id_lock = null;
            balloon_alert(user, "successfully unlocked");
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(panel_open) || !Dm.truthy(is_wire_tool(tool)))
            return NONE;
        await wires.interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:316
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        await process_upgrades();
        return dot;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:320
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        atom drop_point = over;
        if (Dm.truthy(on) || Dm.truthy(stopping))
        {
            balloon_alert(user, "turn it off first!");
            return null;
        }

        mob_living_carbon_human_species_monkey poor_monkey = monkey_worker?.resolve() as mob_living_carbon_human_species_monkey;
        if (poor_monkey == null)
            return null;

        balloon_alert(user, "trying to unbuckle...");
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
        {
            balloon_alert(user, "interrupted");
            return null;
        }

        balloon_alert(user, "unbuckled");
        await poor_monkey.drop_all_held_items();
        await poor_monkey.forceMove(drop_point);
        return null;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:334
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        atom monkey = dropped;
        if (Dm.truthy(on) || Dm.truthy(stopping))
        {
            balloon_alert(user, "turn it off first!");
            return null;
        }

        if (monkey_worker?.resolve() != null)
            return null;

        if (!HAS_TRAIT(monkey, TRAIT_LESSER_HUMANOID))
            return null;

        mob_living_carbon_human poor_monkey = (mob_living_carbon_human)monkey;
        if (Dm.truthy(poor_monkey.mind))
        {
            balloon_alert(user, "too smart!");
            return null;
        }

        poor_monkey.balloon_alert(user, "trying to buckle...");
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, poor_monkey)))
        {
            poor_monkey.balloon_alert(user, "interrupted");
            return null;
        }

        balloon_alert(user, "buckled");
        monkey_worker = (datum_weakref)WEAKREF(poor_monkey);
        await poor_monkey.drop_all_held_items();
        await poor_monkey.forceMove(this);
        Dm.append(manipulator_arm.vis_contents, poor_monkey);
        poor_monkey.dir = manipulator_arm.dir;
        poor_monkey.add_offsets(
            type,
            x_add: 32 + Dm.num(manipulator_arm.calculate_item_offset(TRUE, pixels_to_offset: 16)),
            y_add: 32 + Dm.num(manipulator_arm.calculate_item_offset(FALSE, pixels_to_offset: 16)));
        return null;
    }

    /// Attaching the arm effect to the core.
    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:368
    public virtual void create_manipulator_arm()
    {
        manipulator_arm = Dm.@new<obj_effect_big_manipulator_arm>(this);
        manipulator_arm.dir = NORTH;
        Dm.append(vis_contents, manipulator_arm);
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:373
    public virtual async DmTask toggle_power_state(mob user = null)
    {
        float newly_on = Dm.num(!Dm.truthy(on));

        if (user == null)
        {
            on = newly_on;
            return;
        }

        if (Dm.truthy(newly_on))
        {
            if (!Dm.truthy(powered__proc()))
            {
                balloon_alert(user, "no power!");
                return;
            }

            if (!Dm.truthy(anchored))
            {
                balloon_alert(user, "anchor first!");
                return;
            }

            validate_all_tasks();

            on = newly_on;
            SStgui.update_uis(this);
            await try_kickstart(user);
        }
        else
        {
            await drop_held_atom();
            on = newly_on;
            next_cycle_scheduled = FALSE;
            if (current_task != null && !Dm.truthy(stopping))
            {
                stopping = TRUE;
                _addtimer(Dm.@new<datum_callback>(this, nameof(complete_stopping_task)), 1 * SECONDS, file: "code/game/machinery/big_manipulator/big_manipulator.dm", line: 401);
            }
            else
            {
                stopping = FALSE;
                unregister_task_turf_signals();
                waiting_for_signal = FALSE;
            }
            SStgui.update_uis(this);
        }
    }

    /// Validates all cargo tasks, removing those on closed turfs.
    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:409
    public virtual void validate_all_tasks()
    {
        foreach (datum_manipulator_task_cargo cargo_task in Dm.each<datum_manipulator_task_cargo>(tasks))
        {
            if (!Dm.truthy(cargo_task.is_valid()))
            {
                Dm.remove(tasks, cargo_task);
                qdel(cargo_task);
            }
        }
    }

    /// Attempts to press the power button.
    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:416
    public virtual async DmTask try_press_on(mob user = null)
    {
        if (Dm.truthy(power_access_wire_cut))
        {
            balloon_alert(user, "unresponsive!");
            return;
        }

        if (Dm.truthy(stopping))
        {
            balloon_alert(user, "stopping in progress!");
            return;
        }

        await toggle_power_state(user);
        if (Dm.truthy(on))
            balloon_alert(user, "activated");
        else
            balloon_alert(user, "deactivated");
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:431
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (id_lock != null)
        {
            to_chat(user, span_warning(I18n.T("[] is locked behind ID authentication!", this)));
            ui?.close();
            return null;
        }
        if (!Dm.truthy(anchored))
        {
            to_chat(user, span_warning(I18n.T("[] isn't attached to the ground!", this)));
            ui?.close();
            return null;
        }
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "BigManipulator");
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:445
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["active"] = on;
        data["stopping"] = stopping;
        data["current_task"] = current_task != null ? REF(current_task) : null;
        data["speed_multiplier"] = speed_multiplier;
        data["min_speed_multiplier"] = min_speed_multiplier;
        data["max_speed_multiplier"] = max_speed_multiplier;
        data["manipulator_position"] = Dm.text("[],[]", x, y);
        data["tasking_strategy"] = tasking_strategy;
        data["has_monkey"] = Dm.num(monkey_worker?.resolve() != null);
        data["disk_inserted"] = Dm.num(task_disk != null);
        data["disk_read_only"] = task_disk?.read_only;
        data["disk_task_count"] = Dm.length(task_disk?.tasks_data);

        DmList tasks_data = Dm.list();
        foreach (datum_manipulator_task task in Dm.each<datum_manipulator_task>(tasks))
        {
            DmList td = Dm.list();
            td["name"] = task.name;
            td["id"] = REF(task);

            if (task is datum_manipulator_task_cargo_pickup pickup_task)
            {
                td["task_type"] = TASK_TYPE_PICKUP;
                td["turf"] = Dm.text("[],[]", pickup_task.offset_dx, pickup_task.offset_dy);
                td["filters_status"] = pickup_task.should_use_filters;
                td["item_filters"] = _collect_filter_names(pickup_task.atom_filters);
                td["settings_list"] = _collect_priorities(pickup_task.interaction_priorities);
                td["pickup_eagerness"] = pickup_task.pickup_eagerness;
            }
            else if (task is datum_manipulator_task_cargo_dropoff_base_drop drop_task)
            {
                td["task_type"] = TASK_TYPE_DROP;
                td["turf"] = Dm.text("[],[]", drop_task.offset_dx, drop_task.offset_dy);
                td["filters_status"] = drop_task.should_use_filters;
                td["item_filters"] = _collect_filter_names(drop_task.atom_filters);
                td["settings_list"] = _collect_priorities(drop_task.interaction_priorities);
                td["overflow_status"] = drop_task.overflow_status;
            }
            else if (task is datum_manipulator_task_cargo_dropoff_base_throw throw_task)
            {
                td["task_type"] = TASK_TYPE_THROW;
                td["turf"] = Dm.text("[],[]", throw_task.offset_dx, throw_task.offset_dy);
                td["filters_status"] = throw_task.should_use_filters;
                td["item_filters"] = _collect_filter_names(throw_task.atom_filters);
                td["settings_list"] = _collect_priorities(throw_task.interaction_priorities);
                td["throw_range"] = throw_task.throw_range;
            }
            else if (task is datum_manipulator_task_cargo_dropoff_base_use use_task)
            {
                td["task_type"] = TASK_TYPE_USE;
                td["turf"] = Dm.text("[],[]", use_task.offset_dx, use_task.offset_dy);
                td["filters_status"] = use_task.should_use_filters;
                td["item_filters"] = _collect_filter_names(use_task.atom_filters);
                td["settings_list"] = _collect_priorities(use_task.interaction_priorities);
                td["worker_use_rmb"] = use_task.worker_use_rmb;
                td["worker_combat_mode"] = use_task.worker_combat_mode;
                td["skip_anchored"] = use_task.skip_anchored;
            }
            else if (task is datum_manipulator_task_cargo_interact interact_task)
            {
                td["task_type"] = TASK_TYPE_INTERACT;
                td["turf"] = Dm.text("[],[]", interact_task.offset_dx, interact_task.offset_dy);
                td["filters_status"] = interact_task.should_use_filters;
                td["item_filters"] = _collect_filter_names(interact_task.atom_filters);
                td["settings_list"] = _collect_priorities(interact_task.interaction_priorities);
                td["worker_use_rmb"] = interact_task.worker_use_rmb;
                td["worker_combat_mode"] = interact_task.worker_combat_mode;
                td["skip_anchored"] = interact_task.skip_anchored;
            }
            else if (task is datum_manipulator_task_simple_wait wait_task)
            {
                td["task_type"] = TASK_TYPE_WAIT;
                td["time"] = wait_task.time_seconds;
            }
            else if (task is datum_manipulator_task_simple_stop stop_task)
            {
                td["task_type"] = TASK_TYPE_STOP;
                td["sub_name"] = stop_task.sub_name;
            }
            else if (task is datum_manipulator_task_cargo_move move_task)
            {
                td["task_type"] = TASK_TYPE_MOVE;
                td["turf"] = Dm.text("[],[]", move_task.offset_dx, move_task.offset_dy);
            }

            Dm.append(tasks_data, Dm.list(td));
        }

        data["tasks_data"] = tasks_data;
        return data;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:532
    public virtual object _collect_filter_names(DmList filters = null)
    {
        DmList names = Dm.list();
        foreach (object f in Dm.each(filters))
            Dm.append(names, Dm.initial(f, (atom p) => p.name));
        return names;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:538
    public virtual object _collect_priorities(DmList priorities = null)
    {
        DmList @out = Dm.list();
        foreach (datum_manipulator_priority pr in Dm.each<datum_manipulator_priority>(priorities))
        {
            DmList entry = Dm.list();
            entry["name"] = pr.name;
            entry["active"] = pr.active;
            Dm.append(@out, Dm.list(entry));
        }
        return @out;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:547
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "run_cycle":
                await try_press_on(ui.user);
                return TRUE;

            case "drop_held_atom":
                await drop_held_atom();
                return TRUE;

            case "create_task":
                create_new_task(ui.user, @params["task_type"]);
                await maybe_wake();
                return TRUE;

            case "reset_tasking_index":
                master_tasking.current_index = 1;
                balloon_alert(ui.user, "tasking index reset");
                await maybe_wake();
                return TRUE;

            case "cycle_tasking_strategy":
            {
                object new_strategy = @params["new_strategy"];
                if (Dm.@in(new_strategy, Dm.list(TASKING_SEQUENTIAL, TASKING_STRICT)))
                {
                    tasking_strategy = Dm.str(new_strategy);
                    update_strategies();
                }
                await maybe_wake();
                return TRUE;
            }

            case "adjust_interaction_speed":
            {
                float? new_speed = text2num(@params["new_speed"]);
                if (new_speed == null)
                    return FALSE;
                speed_multiplier = clamp(new_speed.Value, min_speed_multiplier, max_speed_multiplier);
                return TRUE;
            }

            case "unbuckle":
            {
                mob_living_carbon_human_species_monkey poor_monkey = monkey_worker?.resolve() as mob_living_carbon_human_species_monkey;
                if (poor_monkey != null)
                {
                    await poor_monkey.drop_all_held_items();
                    await poor_monkey.forceMove(get_turf(this));
                }
                monkey_worker = null;
                return TRUE;
            }

            case "adjust_task_param":
            {
                object success = await adjust_param_for_task(@params["taskId"], @params["param"], @params["value"], ui.user);
                if (Dm.truthy(success))
                    await maybe_wake();
                return success;
            }

            case "disk_eject":
                return await eject_task_disk(ui.user);

            case "disk_read":
                if (Dm.truthy(read_disk_tasks(ui.user)))
                    await maybe_wake();
                return TRUE;

            case "disk_write":
                return write_disk_tasks(ui.user);

            case "disk_clear":
                return clear_disk_tasks(ui.user);
        }
        return dot;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:618
    public virtual async DmTask<object> eject_task_disk(mob user = null)
    {
        if (Dm.truthy(on) || Dm.truthy(stopping))
        {
            balloon_alert(user, "turn it off first!");
            return FALSE;
        }
        if (task_disk == null)
            return FALSE;
        obj_item_disk_manipulator ejectable_disk = task_disk;
        task_disk = null;
        if (user != null && Dm.truthy(await user.put_in_hands(ejectable_disk)))
            balloon_alert(user, "disk ejected");
        else
        {
            await ejectable_disk.forceMove((atom)drop_location());
            balloon_alert(user, "disk dropped");
        }
        SStgui.update_uis(this);
        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:635
    public virtual object clear_disk_tasks(mob user = null)
    {
        if (Dm.truthy(on) || Dm.truthy(stopping))
        {
            balloon_alert(user, "turn it off first!");
            return FALSE;
        }
        if (task_disk == null)
            return FALSE;
        if (Dm.truthy(task_disk.read_only))
        {
            balloon_alert(user, "disk protected");
            return FALSE;
        }
        task_disk.set_tasks(Dm.list());
        balloon_alert(user, "cleared");
        SStgui.update_uis(this);
        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:650
    public virtual object write_disk_tasks(mob user = null)
    {
        if (Dm.truthy(on) || Dm.truthy(stopping))
        {
            balloon_alert(user, "turn it off first!");
            return FALSE;
        }
        if (task_disk == null)
            return FALSE;
        if (Dm.truthy(task_disk.read_only))
        {
            balloon_alert(user, "disk protected");
            return FALSE;
        }

        DmList @out = Dm.list();
        foreach (datum_manipulator_task task in Dm.each_cast<datum_manipulator_task>(tasks))
            Dm.append(@out, Dm.list(task.serialize()));

        task_disk.set_tasks(@out);
        balloon_alert(user, "written");
        SStgui.update_uis(this);
        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:669
    public virtual object read_disk_tasks(mob user = null)
    {
        if (Dm.truthy(on) || Dm.truthy(stopping))
        {
            balloon_alert(user, "turn it off first!");
            return FALSE;
        }
        if (task_disk == null)
            return FALSE;

        QDEL_LIST(tasks);
        tasks = Dm.list();
        current_task = null;

        turf @base = get_turf(this);
        datum_stock_part_servo locate_servo = Dm.locate<datum_stock_part_servo>(component_parts);
        object manipulator_tier = locate_servo != null ? locate_servo.tier : 1;

        foreach (object task_entry in Dm.each(task_disk.tasks_data))
        {
            if (Dm.length(tasks) >= interaction_point_limit)
                break;
            if (task_entry is not DmList task_data)
                continue;
            object task_type = task_data["type"];
            if (!Dm.ispath(task_type, typeof(datum_manipulator_task)))
                continue;
            datum_manipulator_task new_task;
            if (Dm.ispath(task_type, typeof(datum_manipulator_task_cargo)))
            {
                if (@base == null)
                    continue;
                if (task_data["offset"] is not DmList offset)
                    continue;
                object dx = offset["dx"];
                object dy = offset["dy"];
                if (!isnum(dx) || !isnum(dy))
                    continue;
                if (Dm.num(dx) < -1 || Dm.num(dx) > 1 || Dm.num(dy) < -1 || Dm.num(dy) > 1)
                    continue;
                if (Dm.num(dx) == 0 && Dm.num(dy) == 0)
                    continue;
                turf target_turf = locate(@base.x + Dm.num(dx), @base.y + Dm.num(dy), @base.z);
                if (target_turf == null || isclosedturf(target_turf))
                    continue;
                new_task = Dm.@new((Type)task_type, target_turf, manipulator_tier, Dm.named("serialized_data", task_data)) as datum_manipulator_task;
                if (new_task is datum_manipulator_task_cargo c)
                {
                    c.offset_dx = dx;
                    c.offset_dy = dy;
                }
            }
            else
                new_task = Dm.@new((Type)task_type, Dm.named("serialized_data", task_data)) as datum_manipulator_task;
            if (new_task == null || QDELETED(new_task))
                continue;
            Dm.append(tasks, new_task);
        }

        validate_all_tasks();
        balloon_alert(user, "loaded");
        SStgui.update_uis(this);
        return TRUE;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:724
    public virtual async DmTask<object> adjust_param_for_task(object task_ref = null, object param = null, object value = null, mob user = null)
    {
        if (!Dm.truthy(param))
            return FALSE;

        // locate(task_ref) in tasks
        datum_manipulator_task target_task = Dm.locate_ref(task_ref) as datum_manipulator_task;
        if (target_task == null || !Dm.@in(target_task, tasks))
            return FALSE;

        switch (param)
        {
            case "set_name":
                if (!Dm.truthy(value))
                    return FALSE;
                target_task.name = Dm.str(await sanitize_name(value, allow_numbers: TRUE));
                return TRUE;

            case "set_wait_time":
            {
                if (target_task is not datum_manipulator_task_simple_wait t)
                    return FALSE;
                t.time_seconds = clamp(Dm.num(text2num(value)), 1, 60);
                return TRUE;
            }

            case "remove_task":
                tasks.Remove(target_task);
                qdel(target_task);
                return TRUE;

            case "move_up":
            {
                float idx = tasks.Find(target_task);
                if (idx <= 1)
                    return FALSE;
                tasks.Swap(idx, idx - 1);
                return TRUE;
            }

            case "move_down":
            {
                float idx = tasks.Find(target_task);
                if (idx >= Dm.length(tasks))
                    return FALSE;
                tasks.Swap(idx, idx + 1);
                return TRUE;
            }

            case "move_to":
            {
                if (target_task is not datum_manipulator_task_cargo cargo_task)
                    return FALSE;
                float button_number = Dm.num(text2num((value as DmList)?["buttonNumber"]));
                if (button_number < 1 || button_number > 9)
                    return null;
                float dx = ((button_number - 1) % 3) - 1;
                float dy = 1 - round((button_number - 1) / 3);
                turf new_turf = locate(x + dx, y + dy, z);
                if (new_turf == null || isclosedturf(new_turf))
                    return FALSE;
                cargo_task.interaction_turf = new_turf;
                cargo_task.offset_dx = dx;
                cargo_task.offset_dy = dy;
                return TRUE;
            }

            case "toggle_filter_skip":
            {
                if (target_task is not datum_manipulator_task_cargo ct)
                    return FALSE;
                ct.should_use_filters = Dm.num(!Dm.truthy(ct.should_use_filters));
                return TRUE;
            }

            case "reset_atom_filters":
            {
                if (target_task is not datum_manipulator_task_cargo ct)
                    return FALSE;
                ct.atom_filters = Dm.list();
                return TRUE;
            }

            case "add_atom_filter_from_held":
            {
                if (target_task is not datum_manipulator_task_cargo ct)
                    return FALSE;
                obj_item held_item = user.get_active_held_item() as obj_item;
                if (held_item == null)
                    return FALSE;
                foreach (object filter_path in Dm.each(ct.atom_filters))
                {
                    if (Dm.istype(held_item, filter_path as Type))
                        return FALSE;
                }
                Dm.append(ct.atom_filters, held_item.type);
                return TRUE;
            }

            case "delete_filter":
            {
                if (target_task is not datum_manipulator_task_cargo ct)
                    return FALSE;
                ct.atom_filters.Cut(Dm.num(value), Dm.num(value) + 1);
                return TRUE;
            }

            case "toggle_priority":
            {
                if (target_task is not datum_manipulator_task_cargo current_task)
                    return FALSE;
                return current_task.tick_priority_by_index(value);
            }

            case "priority_move_up":
            {
                if (target_task is not datum_manipulator_task_cargo current_task)
                    return FALSE;
                return current_task.move_priority_up_by_index(value);
            }

            case "cycle_pickup_eagerness":
            {
                if (target_task is not datum_manipulator_task_cargo_pickup cycle_target_task)
                    return FALSE;
                cycle_target_task.pickup_eagerness = Dm.str(cycle_value(cycle_target_task.pickup_eagerness, Dm.list(PICKUP_CAN_WAIT, PICKUP_EAGER)));
                return TRUE;
            }

            case "cycle_overflow_status":
            {
                if (target_task is not datum_manipulator_task_cargo_dropoff_base_drop cycle_target_task)
                    return FALSE;
                cycle_target_task.overflow_status = Dm.str(cycle_value(cycle_target_task.overflow_status, Dm.list(POINT_OVERFLOW_ALLOWED, POINT_OVERFLOW_FILTERS, POINT_OVERFLOW_HELD, POINT_OVERFLOW_FORBIDDEN)));
                return TRUE;
            }

            case "cycle_throw_range":
            {
                if (target_task is not datum_manipulator_task_cargo_dropoff_base_throw cycle_target_task)
                    return FALSE;
                cycle_target_task.throw_range = Dm.num(cycle_value(cycle_target_task.throw_range, Dm.list(1, 2, 3, 4, 5, 6, 7)));
                return TRUE;
            }

            case "toggle_worker_rmb":
                if (target_task is datum_manipulator_task_cargo_dropoff_base_use use_rmb)
                {
                    use_rmb.worker_use_rmb = Dm.num(!Dm.truthy(use_rmb.worker_use_rmb));
                    return TRUE;
                }
                if (target_task is datum_manipulator_task_cargo_interact interact_rmb)
                {
                    interact_rmb.worker_use_rmb = Dm.num(!Dm.truthy(interact_rmb.worker_use_rmb));
                    return TRUE;
                }
                return FALSE;

            case "toggle_worker_combat":
                if (target_task is datum_manipulator_task_cargo_dropoff_base_use use_combat)
                {
                    use_combat.worker_combat_mode = Dm.num(!Dm.truthy(use_combat.worker_combat_mode));
                    return TRUE;
                }
                if (target_task is datum_manipulator_task_cargo_interact interact_combat)
                {
                    interact_combat.worker_combat_mode = Dm.num(!Dm.truthy(interact_combat.worker_combat_mode));
                    return TRUE;
                }
                return FALSE;

            case "toggle_skip_anchored":
                if (target_task is datum_manipulator_task_cargo_dropoff_base_use unanchor_use)
                {
                    unanchor_use.skip_anchored = Dm.num(!Dm.truthy(unanchor_use.skip_anchored));
                    return TRUE;
                }
                if (target_task is datum_manipulator_task_cargo_interact unanchor_interact)
                {
                    unanchor_interact.skip_anchored = Dm.num(!Dm.truthy(unanchor_interact.skip_anchored));
                    return TRUE;
                }
                return FALSE;
        }
        return null;
    }

    /// Cycles the given value in the given list.
    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:875
    public virtual object cycle_value(object current_value = null, DmList possible_values = null)
    {
        float current_index = possible_values.Find(current_value);
        if (current_index == 0)
            return possible_values[1];
        return possible_values[(current_index % Dm.length(possible_values)) + 1];
    }

    /// Retries the task loop if we're waiting for a signal and the machine is on.
    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:882
    public virtual async DmTask maybe_wake()
    {
        if (Dm.truthy(on) && !Dm.truthy(stopping) && Dm.truthy(waiting_for_signal))
            await something_happened();
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:886
    public virtual void update_strategies()
    {
        master_tasking = create_strategy(tasking_strategy) as datum_tasking_strategy;
    }

    // tg: code/game/machinery/big_manipulator/big_manipulator.dm:889
    public virtual object create_strategy(object strategy_mode = null)
    {
        switch (strategy_mode)
        {
            case TASKING_SEQUENTIAL:
                return Dm.@new<datum_tasking_strategy_sequential>();
            case TASKING_STRICT:
                return Dm.@new<datum_tasking_strategy_strict>();
        }
        return Dm.@new<datum_tasking_strategy_sequential>();
    }
}
