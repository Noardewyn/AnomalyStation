// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/_ai_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1) but the admin event logger (EVLOG_*, the evlogging signal handlers and on_evlog_event_added, live-partial rows, as movement_types.cs).
// The compiled .bt.json files are embedded in this assembly (BehaviorTrees/, tools/build-behavior-trees.sh) where tg reads them from build/behavior_trees/ (DV251).

/*
AI controllers are a datumized form of AI that simulates the input a player would otherwise give to a atom. What this means is that these datums
have ways of interacting with a specific atom and control it. They posses a blackboard with the information the AI knows and has, and will plan behaviors it will try to execute through
multiple modular subtrees with behaviors
*/

// tg: code/datums/ai/_ai_controller.dm:7
public partial class datum_ai_controller : datum
{
    // Movement related things here
    ///Reference to the movement datum we use. Is a type on initialize but becomes a ref afterwards. (Hand-declared: a type and then an instance.)
    // tg: code/datums/ai/_ai_controller.dm:55
    public object ai_movement = typeof(datum_ai_movement_dumb);

    /// The instance change_ai_movement_type gave the var, which every use reads.
    public datum_ai_movement movement => ai_movement as datum_ai_movement;

    // tg: code/datums/ai/_ai_controller.dm:70
    [DmParams("new_pawn")]
    public override async DmTask New(params object[] __extra)
    {
        atom new_pawn = Dm.arg<atom>(__extra, 0);
        change_ai_movement_type(ai_movement);
        initialize_behavior_tree();

        if (new_pawn != null) // unit tests need the ai_controller to exist in isolation due to list schenanigans i hate it here
            await PossessPawn(new_pawn);
    }

    // tg: code/datums/ai/_ai_controller.dm:77
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(UnpossessPawn(FALSE));
        if (Dm.truthy(ai_status))
        {
            Dm.remove_at(SSai_controllers.ai_controllers_by_status, ai_status, this);
            foreach (datum_controller_subsystem_ai_controllers controller_subsystem in Dm.each<datum_controller_subsystem_ai_controllers>(Master?.subsystems))
            {
                if (Dm.eq(controller_subsystem.planning_status, ai_status))
                {
                    Dm.remove(ref controller_subsystem.currentrun, this);
                    break;
                }
            }
        }
        our_cells = null;
        if (Dm.truthy(movement.moving_controllers[this]))
            movement.stop_moving_towards(this);
        QDEL_LIST(behavior_nodes);
        return base.Destroy(force);
    }

    ///Overrides the current ai_movement of this controller with a new one
    // tg: code/datums/ai/_ai_controller.dm:91
    public virtual void change_ai_movement_type(object new_movement = null)
    {
        ai_movement = SSai_movement.movement_types[new_movement];
    }

    ///Completely replaces the behavior_nodes with a new set based on argument provided.
    // tg: code/datums/ai/_ai_controller.dm:95
    public virtual void replace_behavior_nodes(DmList typepaths_of_new_subtrees = null)
    {
        DmList old_nodes = behavior_nodes;
        behavior_nodes = typepaths_of_new_subtrees;
        initialize_behavior_tree();
        QDEL_LIST(old_nodes);
    }

    /// Resolves the children/child of a composite or decorator node, creating configured instances.
    /// Safe to call on any node type; non-composite/non-decorator nodes are a no-op.
    // tg: code/datums/ai/_ai_controller.dm:103
    public virtual void resolve_node_children(datum_bt_node node = null)
    {
        if (node is datum_bt_node_composite comp)
        {
            if (!Dm.truthy(LAZYLEN(comp.children_typepaths)) || Dm.truthy(LAZYLEN(comp.children)))
                return;
            DmList resolved_children = Dm.list();
            foreach (object child_type in Dm.each(comp.children_typepaths))
            {
                DmList config = comp.children_typepaths[child_type] as DmList;
                datum_bt_node child = resolve_child_node(child_type, config) as datum_bt_node;
                if (child == null)
                {
                    stack_trace(Dm.text("BT composite [] references unknown child type []", node.type, child_type), "code/datums/ai/_ai_controller.dm", 113);
                    continue;
                }
                Dm.append(ref resolved_children, child);
            }
            comp.children = resolved_children;
        }
        else if (node is datum_bt_node_decorator dec)
        {
            if (dec.child_typepath == null || dec.child != null)
                return;
            dec.child = resolve_child_node(dec.child_typepath, null) as datum_bt_node;
            if (dec.child == null)
                stack_trace(Dm.text("BT decorator [] references unknown child type []", node.type, dec.child_typepath), "code/datums/ai/_ai_controller.dm", 123);
        }
        else if (node is datum_bt_node_subtree sub)
        {
            if (sub.behavior_nodes != null && sub.root == null)
                sub.root = build_node_from_descriptor(sub.behavior_nodes) as datum_bt_node;
            else if (sub.behavior_tree_json != null && sub.root == null)
            {
                DmList raw_desc = Dm.json_decode(BehaviorTreeFiles.Read(BT_COMPILED_PATH((string)sub.behavior_tree_json))) as DmList;
                if (Dm.truthy(LAZYLEN(sub.bindings)) || raw_desc[BT_DESC_BINDINGS] != null)
                    raw_desc = apply_bindings_to_descriptor(raw_desc, sub.bindings) as DmList;
                sub.root = build_node_from_descriptor(raw_desc) as datum_bt_node;
            }
        }
    }

    // Always creates a fresh instance regardless of whether config is provided.
    // tg: code/datums/ai/_ai_controller.dm:136
    public virtual object resolve_child_node(object child_type = null, DmList config = null)
    {
        if (!ispath(child_type, typeof(datum_bt_node)))
            return null;
        datum_bt_node child = Dm.@new((Type)child_type) as datum_bt_node;
        if (Dm.truthy(config))
            child.configure(config);
        resolve_node_children(child);
        return child;
    }

    // tg: code/datums/ai/_ai_controller.dm:145
    public virtual object get_or_build_node(object entry = null)
    {
        if (ispath(entry))
        {
            if (!ispath(entry, typeof(datum_bt_node)))
            {
                stack_trace(Dm.text("get_or_build_node() received non-BT typepath: []", entry), "code/datums/ai/_ai_controller.dm", 148);
                return null;
            }
            datum_bt_node node = Dm.@new((Type)entry) as datum_bt_node;
            resolve_node_children(node);
            return node;
        }
        if (islist(entry))
            return build_node_from_descriptor((DmList)entry);
        stack_trace(Dm.text("get_or_build_node() received unexpected entry type: []", entry), "code/datums/ai/_ai_controller.dm", 155);
        return null;
    }

    ///Loads and decodes a compiled BT JSON file into a node tree.
    // tg: code/datums/ai/_ai_controller.dm:159
    public virtual object load_tree_from_json(object path = null)
    {
        DmList desc = Dm.json_decode(BehaviorTreeFiles.Read((string)path)) as DmList;
        return build_node_from_descriptor(desc);
    }

    /**
     * Merges call-site binding overrides with the subtree's declared defaults,
     * then substitutes all $name placeholders in the descriptor tree.
     * Returns a new descriptor with BT_DESC_BINDINGS stripped and placeholders resolved.
     */
    // tg: code/datums/ai/_ai_controller.dm:169
    public virtual object apply_bindings_to_descriptor(DmList desc = null, DmList call_site_bindings = null)
    {
        DmList merged = Dm.list();
        DmList declared = desc[BT_DESC_BINDINGS] as DmList;
        foreach (object name in Dm.each(declared))
            merged[name] = ((DmList)declared[name])["default"];
        foreach (object name in Dm.each(call_site_bindings))
            merged[name] = call_site_bindings[name];
        return _substitute_bindings(desc, merged);
    }

    /// Recursively walks a descriptor list, replacing "$name" strings with their bound values.
    // tg: code/datums/ai/_ai_controller.dm:179
    public virtual object _substitute_bindings(DmList desc = null, DmList merged = null)
    {
        DmList @out = Dm.list();
        foreach (object key in Dm.each(desc))
        {
            if (Dm.eq(key, BT_DESC_BINDINGS))
                continue;
            object value = desc[key];
            if (islist(value))
                @out[key] = _substitute_bindings_in_list((DmList)value, merged);
            else if (value is string text && copytext(text, 1, 2) == "$")
            {
                string binding_name = copytext(text, 2);
                @out[key] = merged[binding_name] == null ? value : merged[binding_name];
            }
            else
                @out[key] = value;
        }
        return @out;
    }

    /// Substitutes bindings inside a descriptor value list, preserving assoc entries
    // tg: code/datums/ai/_ai_controller.dm:195
    public virtual object _substitute_bindings_in_list(DmList input = null, DmList merged = null)
    {
        DmList resolved_list = Dm.list();
        foreach (object item in Dm.each(input))
        {
            object assoc_value = isnum(item) ? null : input[item];
            if (assoc_value != null)
            {
                if (islist(assoc_value))
                    resolved_list[item] = _substitute_bindings_in_list((DmList)assoc_value, merged); //recursion baby
                else if (assoc_value is string text && copytext(text, 1, 2) == "$")
                {
                    string binding_name = copytext(text, 2);
                    resolved_list[item] = merged[binding_name] == null ? assoc_value : merged[binding_name];
                }
                else
                    resolved_list[item] = assoc_value;
            }
            else if (islist(item))
                Dm.append(ref resolved_list, Dm.list(_substitute_bindings((DmList)item, merged)));
            else if (item is string itemtext && copytext(itemtext, 1, 2) == "$")
            {
                string binding_name = copytext(itemtext, 2);
                object resolved = merged[binding_name] == null ? item : merged[binding_name];
                if (islist(resolved))
                    Dm.append(ref resolved_list, Dm.list(resolved));
                else
                    Dm.append(ref resolved_list, resolved);
            }
            else
                Dm.append(ref resolved_list, item);
        }
        return resolved_list;
    }

    /**
     * Recursively builds a BT node tree from a descriptor list.
     * BT_DESC_TYPE and BT_DESC_CHILDREN are consumed internally; all other keys are written
     * as vars onto the node. String values starting with "/" are resolved via text2path so
     * typepath args (e.g. "/datum/ai_movement/basic_avoidance") arrive as actual types.
     * If you put / in a string then yeah that might cause issues, should probably fix that later!
     */
    // tg: code/datums/ai/_ai_controller.dm:232
    public virtual object build_node_from_descriptor(DmList desc = null)
    {
        object raw_type = desc[BT_DESC_TYPE];
        if (!Dm.truthy(raw_type)) // This can happen if we have an overriden type with no binding. (e.g. subtrees not being overriden and default to null)
            return null;
        Type node_type = ispath(raw_type) ? (Type)raw_type : text2path(raw_type as string);
        if (node_type == null)
        {
            stack_trace(Dm.text("build_node_from_descriptor(): unknown typepath '[]'", raw_type), "code/datums/ai/_ai_controller.dm", 238);
            return null;
        }
        datum_bt_node node = Dm.@new(node_type) as datum_bt_node;
        foreach (object key in Dm.each(desc))
        {
            if (Dm.eq(key, BT_DESC_TYPE) || Dm.eq(key, BT_DESC_CHILDREN) || Dm.eq(key, BT_DESC_BINDINGS))
                continue;
            object value = desc[key];
            if (value is DmList resolved)
            {
                for (float i = 1, i__end = length(resolved); i <= i__end; i++)
                {
                    if (resolved[i] is string item)
                    {
                        Type as_path = text2path(item);
                        if (as_path != null)
                            resolved[i] = as_path;
                    }
                }
            }
            else if (value is string text)
            {
                Type as_path = text2path(text);
                if (as_path != null)
                    value = as_path;
            }
            Dm.set_var(node, (string)key, value);
        }
        resolve_node_children(node);
        DmList children_descs = desc[BT_DESC_CHILDREN] as DmList;
        if (Dm.truthy(LAZYLEN(children_descs)))
            node.set_descriptor_children(children_descs, this);
        return node;
    }

    /// Builds the per-controller BT node tree from behavior_nodes typepaths or descriptors, then finalizes it.
    // tg: code/datums/ai/_ai_controller.dm:263
    public virtual void initialize_behavior_tree()
    {
        if (behavior_tree_json != null && !Dm.truthy(LAZYLEN(behavior_nodes)))
        {
            string compiled_path = BT_COMPILED_PATH((string)behavior_tree_json); //Find the compiled version of this BT
            datum_bt_node root = load_tree_from_json(compiled_path) as datum_bt_node;
            if (root == null)
            {
                stack_trace(Dm.text("[] failed to load behavior tree from compiled JSON: []", type, compiled_path), "code/datums/ai/_ai_controller.dm", 268);
                return;
            }
            behavior_nodes = Dm.list(root);
            finalize_tree();
            return;
        }
        if (!Dm.truthy(LAZYLEN(behavior_nodes)))
            return;
        DmList temp_subtree_list = Dm.list();
        if (behavior_nodes[BT_DESC_TYPE] != null)
        {
            datum_bt_node node_instance = get_or_build_node(behavior_nodes) as datum_bt_node;
            if (node_instance == null)
                stack_trace(Dm.text("[]'s behavior_nodes BT descriptor could not be built", type), "code/datums/ai/_ai_controller.dm", 278);
            else
                Dm.append(ref temp_subtree_list, node_instance);
        }
        else
        {
            foreach (object entry in Dm.each(behavior_nodes))
            {
                datum_bt_node node_instance = get_or_build_node(entry) as datum_bt_node;
                if (node_instance == null)
                {
                    stack_trace(Dm.text("[]'s behavior_nodes contains unknown entry: []", type, entry), "code/datums/ai/_ai_controller.dm", 285);
                    continue;
                }
                Dm.append(ref temp_subtree_list, node_instance);
            }
        }
        behavior_nodes = temp_subtree_list;
        finalize_tree();
    }

    /// Walks the resolved tree to set owning_controller and parent_node on all nodes, populates
    /// override_slots, and assigns pre-order execution indices. Called after initialize_behavior_tree() and
    /// after set_behavior_tree_override() installs or removes an override node.
    // tg: code/datums/ai/_ai_controller.dm:295
    public virtual void finalize_tree()
    {
        if (!Dm.truthy(LAZYLEN(behavior_nodes)))
            return;
        override_slots = null;
        DmList to_visit = behavior_nodes.Copy();
        foreach (datum_bt_node root in Dm.each<datum_bt_node>(behavior_nodes))
            root.parent_node = null;
        float index = 1;
        while (index <= length(to_visit)) //while loop so we can recursively keep populating this list
        {
            datum_bt_node node = to_visit[index++] as datum_bt_node;
            node.finalize_node(this, to_visit);
        }
        object counter = 1;
        foreach (datum_bt_node root in Dm.each<datum_bt_node>(behavior_nodes))
            counter = root.assign_execution_indices(counter);
    }

    ///Proc to move from one pawn to another, this will destroy the target's existing controller.
    // tg: code/datums/ai/_ai_controller.dm:309
    public virtual async DmTask PossessPawn(atom new_pawn = null)
    {
        if (pawn != null) //Reset any old signals
            await UnpossessPawn(FALSE);

        if (new_pawn.ai_controller is datum_ai_controller) //Existing AI, kill it.
            QDEL_NULL(ref new_pawn.ai_controller);

        if (Dm.truthy(Dm.bitand(await TryPossessPawn(new_pawn), AI_CONTROLLER_INCOMPATIBLE)))
        {
            qdel(this);
            throw Dm.CRASH(Dm.text("[] attached to [] but these are not compatible!", this, new_pawn), "code/datums/ai/_ai_controller.dm", 319);
        }

        pawn = new_pawn;
        pawn.ai_controller = this;
        set_blackboard_key(BB_MY_PAWN, pawn, FALSE); //Don't track the datum we already handle qdel of pawn here.

        turf pawn_turf = get_turf(pawn);
        if (pawn_turf != null)
            Dm.append_at(SSai_controllers.ai_controllers_by_zlevel, pawn_turf.z, this);

        if (Dm.truthy(_listen_lookup?[COMSIG_AI_CONTROLLER_POSSESSED_PAWN]))
            _SendSignal(COMSIG_AI_CONTROLLER_POSSESSED_PAWN, Dm.list(this));
        if (Dm.truthy(pawn._listen_lookup?[COMSIG_PAWN_POSSESSED_BY_AI_CONTROLLER]))
            pawn._SendSignal(COMSIG_PAWN_POSSESSED_BY_AI_CONTROLLER, Dm.list(pawn, this));

        await reset_ai_status();
        RegisterSignal(pawn, COMSIG_MOVABLE_Z_CHANGED, nameof(on_changed_z_level));
        RegisterSignal(pawn, COMSIG_MOB_STATCHANGE, nameof(on_stat_changed));
        RegisterSignal(pawn, COMSIG_MOB_LOGIN, nameof(on_sentience_gained));
        RegisterSignal(pawn, COMSIG_QDELETING, nameof(on_pawn_qdeleted));
        // COMSIG_EVLOGGING_ENABLED, COMSIG_EVLOGGING_DISABLED: the admin event logger (file header)

        our_cells = Dm.@new<datum_cell_tracker>(interesting_dist, interesting_dist, 1);
        await set_new_cells();

        update_able_to_run();
        setup_able_to_run();

        RegisterSignal(pawn, COMSIG_MOVABLE_MOVED, nameof(update_grid));
    }

    // tg: code/datums/ai/_ai_controller.dm:349
    [SignalHandler]
    public virtual void update_grid(datum source = null, datum_spatial_grid_cell new_cell = null)
    {
        Waiting.NoSleep(set_new_cells());
    }

    // tg: code/datums/ai/_ai_controller.dm:354
    public virtual async DmTask set_new_cells()
    {
        if (our_cells == null)
            return;

        turf our_turf = get_turf(pawn);

        if (our_turf == null)
            return;

        DmList cell_collections = our_cells.recalculate_cells(our_turf) as DmList;

        foreach (datum old_grid in Dm.each_cast<datum>(cell_collections[2]))
            UnregisterSignal(old_grid, Dm.list(SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS), SPATIAL_GRID_CELL_EXITED(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS)));

        foreach (datum_spatial_grid_cell new_grid in Dm.each_cast<datum_spatial_grid_cell>(cell_collections[1]))
        {
            RegisterSignal(new_grid, SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS), nameof(on_client_enter));
            RegisterSignal(new_grid, SPATIAL_GRID_CELL_EXITED(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS), nameof(on_client_exit));
        }

        await recalculate_idle();
    }

    ///Returns TRUE if a living mob with a client is in one of our tracked spatial grid cells
    // tg: code/datums/ai/_ai_controller.dm:375
    public virtual object has_nearby_client()
    {
        if (our_cells == null)
            return FALSE;
        foreach (datum_spatial_grid_cell grid in Dm.each_cast<datum_spatial_grid_cell>(our_cells.member_cells))
        {
            foreach (mob_living found in Dm.each<mob_living>(grid.client_contents))
                return TRUE;
        }
        return FALSE;
    }

    ///Check if mob should go into idle/low-priority (from spatial cells)
    // tg: code/datums/ai/_ai_controller.dm:384
    public virtual async DmTask recalculate_idle(object exited = null)
    {
        if (Dm.eq(ai_status, AI_STATUS_OFF))
            return;

        float distance = INFINITY;
        if (exited is DmList exited_list)
            distance = get_dist(pawn, exited_list[1]);
        else if (isatom(exited))
            distance = get_dist(pawn, exited);

        if (distance <= interesting_dist) //is our target in between interesting cells?
            return;

        await reset_ai_status();
    }

    // tg: code/datums/ai/_ai_controller.dm:400
    [SignalHandler]
    public virtual void on_client_enter(datum source = null, DmList target_list = null)
    {
        if (Dm.eq(ai_status, AI_STATUS_ON))
            return;

        bool found_living = false;
        foreach (mob_living found in Dm.each<mob_living>(target_list))
        {
            found_living = true;
            break;
        }
        if (!found_living)
            return;

        Waiting.NoSleep(reset_ai_status());
    }

    // tg: code/datums/ai/_ai_controller.dm:411
    [SignalHandler]
    public virtual void on_client_exit(datum source = null, datum exited = null)
    {
        Waiting.NoSleep(recalculate_idle(exited));
    }

    /// Sets the AI on or off based on current conditions, call to reset after you've manually disabled it somewhere
    // tg: code/datums/ai/_ai_controller.dm:417
    public virtual async DmTask reset_ai_status()
    {
        await set_ai_status(get_expected_ai_status());
    }

    /**
     * Deliberately disables this AI until clear_forced_off() is called.
     * Unlike a bare set_ai_status(AI_STATUS_OFF), this survives status recalculations
     * from stat changes, z-level changes, client login/logout and the like.
     */
    // tg: code/datums/ai/_ai_controller.dm:425
    public virtual async DmTask force_ai_off(float? additional_flags = null)
    {
        additional_flags ??= NONE;
        forced_off = TRUE;
        await set_ai_status(AI_STATUS_OFF, additional_flags);
    }

    /// Undoes force_ai_off() and recalculates what status we should be in.
    // tg: code/datums/ai/_ai_controller.dm:430
    public virtual async DmTask clear_forced_off()
    {
        forced_off = FALSE;
        await reset_ai_status();
    }

    /**
     * Gets the AI status we expect the AI controller to be on at this current moment.
     * Returns AI_STATUS_OFF if it has been forced off, is inhabited by a Client and shouldn't be, is dead and cannot act while dead,
     * or is sleeping for performance (off-station with no nearby client and not flagged RUN_WHILE_UNWATCHED; client arrival wakes these automatically).
     * Otherwise returns AI_STATUS_ON or AI_STATUS_ON_LOW, see get_active_ai_status().
     */
    // tg: code/datums/ai/_ai_controller.dm:440
    public virtual object get_expected_ai_status()
    {
        if (Dm.truthy(forced_off))
            return AI_STATUS_OFF;

        if (get_turf(pawn) == null)
            return AI_STATUS_OFF;

        if (!ismob(pawn))
            return AI_STATUS_ON;

        mob_living mob_pawn = pawn as mob_living;
        if (!Dm.truthy(continue_processing_when_client) && Dm.truthy(((mob)pawn).client))
            return AI_STATUS_OFF;

        if (((mob)pawn).stat == DEAD)
        {
            if (Dm.truthy(Dm.bitand(ai_traits, CAN_ACT_WHILE_DEAD)))
                return AI_STATUS_ON;
            return AI_STATUS_OFF;
        }

        // tg: #ifdef TESTING ... #endif not translated (inactive in production)
        if (!Dm.truthy(able_to_run))
            return AI_STATUS_OFF;
        return get_active_ai_status();
    }

    /**
     * Classifies an active AI controller into a priority tier.
     * Returns AI_STATUS_ON for controllers on station/shuttle territory, with a nearby client,
     * Returns AI_STATUS_ON_LOW for other active controllers unless they have ALWAYS_HIGH_PRIORITY.
     * else its AI_STATUS_OFF
     */
    // tg: code/datums/ai/_ai_controller.dm:478
    public virtual object get_active_ai_status()
    {
        turf pawn_turf = get_turf(pawn);
        area pawn_area = pawn_turf != null ? get_area(pawn_turf) as area : null;
        // AI actually standing on the station or a shuttle always stays high priority
        if (pawn_area is area_station || pawn_area is area_shuttle)
            return AI_STATUS_ON;
        if (Dm.truthy(has_nearby_client()))
            return AI_STATUS_ON;
        if (Dm.truthy(Dm.bitand(ai_traits, RUN_WHILE_UNWATCHED)))
        {
            if (Dm.truthy(Dm.bitand(ai_traits, ALWAYS_HIGH_PRIORITY)))
                return AI_STATUS_ON;
            else
                return AI_STATUS_ON_LOW;
        }
        return AI_STATUS_OFF;
    }

    ///Called when the AI controller pawn changes z levels, we check if there's any clients on the new one and wake up the AI if there is.
    // tg: code/datums/ai/_ai_controller.dm:494
    [SignalHandler]
    public virtual void on_changed_z_level(atom source = null, turf old_turf = null, turf new_turf = null, object same_z_layer = null, object notify_contents = null)
    {
        if (ismob(pawn))
        {
            mob mob_pawn = (mob)pawn;
            if (Dm.truthy(mob_pawn?.client) && !Dm.truthy(continue_processing_when_client))
                return;
        }
        if (old_turf != null)
            Dm.remove_at(SSai_controllers.ai_controllers_by_zlevel, old_turf.z, this);
        if (new_turf == null)
            return;
        Dm.append_at(SSai_controllers.ai_controllers_by_zlevel, new_turf.z, this);
        Waiting.NoSleep(reset_ai_status());
    }

    ///Abstract proc for initializing the pawn to the new controller
    // tg: code/datums/ai/_ai_controller.dm:508
    public virtual async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        return null;
    }

    ///Proc for deinitializing the pawn to the old controller
    // tg: code/datums/ai/_ai_controller.dm:512
    public virtual async DmTask UnpossessPawn(object destroy = null)
    {
        if (pawn == null)
            return; // instantiated without an applicable pawn, fine

        if (Dm.truthy(_listen_lookup?[COMSIG_AI_CONTROLLER_UNPOSSESSED_PAWN]))
            _SendSignal(COMSIG_AI_CONTROLLER_UNPOSSESSED_PAWN, Dm.list(this));
        await reset_bt_tick_states();
        await set_ai_status(AI_STATUS_OFF);
        UnregisterSignal(pawn, Dm.list(COMSIG_MOVABLE_Z_CHANGED, COMSIG_MOB_LOGIN, COMSIG_MOB_LOGOUT, COMSIG_MOB_STATCHANGE, COMSIG_QDELETING));
        clear_able_to_run();
        if (Dm.truthy(movement.moving_controllers[this]))
            movement.stop_moving_towards(this);
        turf pawn_turf = get_turf(pawn);
        if (pawn_turf != null)
            Dm.remove_at(SSai_controllers.ai_controllers_by_zlevel, pawn_turf.z, this);
        pawn.ai_controller = null;
        pawn = null;
        if (Dm.truthy(destroy))
            qdel(this);
    }

    ///Call reset tick state on every node in the tree.
    // tg: code/datums/ai/_ai_controller.dm:532
    public virtual async DmTask reset_bt_tick_states()
    {
        if (!Dm.truthy(LAZYLEN(behavior_nodes)))
            return;
        DmList to_visit = behavior_nodes.Copy();
        float index = 1;
        while (index <= length(to_visit))
        {
            datum_bt_node node = to_visit[index++] as datum_bt_node;
            await node.reset_tick_state();
            node.collect_reset_children(to_visit);
        }
    }

    /**
     * Installs or removes a runtime override on the subtree slot registered with the given id.
     *
     * id - The ID for this slot
     * override_subtree - actual subtree we're setting
     */
    // tg: code/datums/ai/_ai_controller.dm:548
    public virtual async DmTask set_behavior_tree_override(object id = null, object override_subtree = null)
    {
        datum_bt_node_subtree slot = LAZYACCESS(override_slots, id) as datum_bt_node_subtree;
        if (slot == null)
            return;

        Type current_type = slot.override_node == null ? null : slot.override_node.type;
        if (Dm.eq(current_type, override_subtree))
            return;

        if (slot.override_node != null)
        {
            await slot.override_node.reset_tick_state();
            QDEL_NULL(ref slot.override_node);
        }

        string changed = COMSIG_AI_OVERRIDE_SLOT_CHANGED(id);
        if (override_subtree == null)
        {
            finalize_tree();
            if (Dm.truthy(pawn._listen_lookup?[changed]))
                pawn._SendSignal(changed, Dm.list(pawn, null));
            return;
        }

        datum_bt_node_subtree new_node = Dm.@new((Type)override_subtree) as datum_bt_node_subtree;
        resolve_node_children(new_node);
        slot.override_node = new_node;
        finalize_tree();
        await cancel_current_plan(); // Reset, not ideal; Maybe later on we can do this more gracefully.
        if (Dm.truthy(pawn._listen_lookup?[changed]))
            pawn._SendSignal(changed, Dm.list(pawn, override_subtree));
    }

    // tg: code/datums/ai/_ai_controller.dm:574
    public virtual void setup_able_to_run()
    {
        // paused_until is handled by PauseAi() manually
        RegisterSignals(pawn, Dm.list(SIGNAL_ADDTRAIT(TRAIT_AI_PAUSED), SIGNAL_REMOVETRAIT(TRAIT_AI_PAUSED)), nameof(update_able_to_run));
    }

    // tg: code/datums/ai/_ai_controller.dm:578
    public virtual void clear_able_to_run()
    {
        UnregisterSignal(pawn, Dm.list(SIGNAL_ADDTRAIT(TRAIT_AI_PAUSED), SIGNAL_REMOVETRAIT(TRAIT_AI_PAUSED)));
    }

    // tg: code/datums/ai/_ai_controller.dm:581
    [SignalHandler]
    public virtual void update_able_to_run()
    {
        object run_flags = get_able_to_run();
        if (Dm.truthy(Dm.bitand(run_flags, AI_UNABLE_TO_RUN)))
        {
            able_to_run = FALSE;
            movement.fail_movement(this);
        }
        else
            able_to_run = TRUE;
        Waiting.NoSleep(set_ai_status(get_expected_ai_status(), Dm.num(run_flags)));
    }

    ///Returns TRUE if the ai controller can actually run at the moment, FALSE otherwise
    // tg: code/datums/ai/_ai_controller.dm:591
    public virtual object get_able_to_run()
    {
        if (HAS_TRAIT(pawn, TRAIT_AI_PAUSED))
            return AI_UNABLE_TO_RUN;
        if (world.time < paused_until)
            return AI_UNABLE_TO_RUN;
        return NONE;
    }

    ///Can this pawn interact with objects?
    // tg: code/datums/ai/_ai_controller.dm:599
    public virtual object ai_can_interact(atom target = null)
    {
        return Dm.num(!QDELETED(pawn) && !QDELETED(target));
    }

    ///Interact with objects
    // tg: code/datums/ai/_ai_controller.dm:603
    public virtual async DmTask<object> ai_interact(object target = null, object combat_mode = null, DmList modifiers = null)
    {
        atom final_target = (isdatum(target) ? target : blackboard[target]) as atom; //incase we got a blackboard key instead

        if (!Dm.truthy(ai_can_interact(final_target)))
            return FALSE;

        string @params = list2params(modifiers);
        mob_living living_pawn = pawn as mob_living;
        if (combat_mode == null)
        {
            await living_pawn.ClickOn(final_target, @params);
            return TRUE;
        }

        object old_combat_mode = living_pawn.combat_mode;
        living_pawn.set_combat_mode(combat_mode);
        await living_pawn.ClickOn(final_target, @params);
        living_pawn.set_combat_mode(old_combat_mode);
        return TRUE;
    }

    ///This is where you decide what actions are taken by the AI.
    // tg: code/datums/ai/_ai_controller.dm:622
    public virtual void SelectBehaviors(object seconds_per_tick = null)
    {
        cancelled_during_tick = FALSE;
        if (Dm.truthy(LAZYLEN(polling_observers)))
        {
            foreach (datum_bt_node_decorator dec in Dm.each_cast<datum_bt_node_decorator>(polling_observers.Copy()))
                Waiting.NoSleep(dec.poll_condition(this));
        }
        foreach (datum_bt_node node in Dm.each_cast<datum_bt_node>(behavior_nodes))
        {
            if (Dm.eq(node.tick(this, seconds_per_tick), BT_RUNNING))
                break;
        }
    }

    ///This proc handles changing ai status and updates the planning subsystem list.
    // tg: code/datums/ai/_ai_controller.dm:633
    public virtual async DmTask<object> set_ai_status(object new_ai_status = null, float? additional_flags = null)
    {
        additional_flags ??= NONE;
        if (Dm.eq(ai_status, new_ai_status))
            return FALSE; //no change

        //remove old status, if we've got one
        if (Dm.truthy(ai_status))
        {
            Dm.remove_at(SSai_controllers.ai_controllers_by_status, ai_status, this);
            foreach (datum_controller_subsystem_ai_controllers controller_subsystem in Dm.each<datum_controller_subsystem_ai_controllers>(Master?.subsystems))
            {
                if (Dm.eq(controller_subsystem.planning_status, ai_status))
                {
                    Dm.remove(ref controller_subsystem.currentrun, this);
                    break;
                }
            }
        }
        ai_status = new_ai_status;
        last_bt_tick = 0; // don't count time spent in the previous status towards the next tick's seconds_per_tick
        Dm.append_at(SSai_controllers.ai_controllers_by_status, new_ai_status, this);
        if (Dm.eq(ai_status, AI_STATUS_OFF))
        {
            if (!Dm.truthy(Dm.bitand(additional_flags, AI_PREVENT_CANCEL_ACTIONS)))
                await cancel_current_plan();
        }
        return null;
    }

    // tg: code/datums/ai/_ai_controller.dm:654
    public virtual async DmTask cancel_current_plan()
    {
        active_execution_index = 0;
        cancelled_during_tick = TRUE;
        await reset_bt_tick_states();
    }

    /// Turn the controller on or off based on if you're alive, we only register to this if the flag is present so don't need to check again
    // tg: code/datums/ai/_ai_controller.dm:660
    [SignalHandler]
    public virtual void on_stat_changed(mob_living source = null, object new_stat = null)
    {
        Waiting.NoSleep(reset_ai_status());
        update_able_to_run();
    }

    // tg: code/datums/ai/_ai_controller.dm:665
    [SignalHandler]
    public virtual void on_sentience_gained()
    {
        UnregisterSignal(pawn, COMSIG_MOB_LOGIN);
        if (!Dm.truthy(continue_processing_when_client))
            Waiting.NoSleep(set_ai_status(AI_STATUS_OFF)); //Can't do anything while player is connected
        RegisterSignal(pawn, COMSIG_MOB_LOGOUT, nameof(on_sentience_lost));
    }

    // tg: code/datums/ai/_ai_controller.dm:672
    [SignalHandler]
    public virtual void on_sentience_lost()
    {
        UnregisterSignal(pawn, COMSIG_MOB_LOGOUT);
        Waiting.NoSleep(reset_ai_status()); //resume AI control now that the client is gone
        RegisterSignal(pawn, COMSIG_MOB_LOGIN, nameof(on_sentience_gained));
    }

    // Turn the controller off if the pawn has been qdeleted
    // tg: code/datums/ai/_ai_controller.dm:679
    [SignalHandler]
    public virtual void on_pawn_qdeleted(datum source = null)
    {
        sig_remove_from_blackboard(source);
        Waiting.NoSleep(set_ai_status(AI_STATUS_OFF));
        if (Dm.truthy(movement.moving_controllers[this]))
            movement.stop_moving_towards(this);
    }

    /// Use this proc to define how your controller defines what access the pawn has for the sake of pathfinding. Return the access list you want to use
    // tg: code/datums/ai/_ai_controller.dm:686
    public virtual object get_access()
    {
        if (!isliving(pawn))
            return null;
        mob_living living_pawn = (mob_living)pawn;
        return living_pawn.get_access();
    }

    /// Returns TRUE if the pawn can path to the target. minimum_distance is how close the path must get (0 = onto/adjacent to the target's turf); searches pass it from their own acquire_target leaf.
    // tg: code/datums/ai/_ai_controller.dm:693
    public virtual async DmTask<object> can_reach_target(atom target = null, float? distance = null, float? minimum_distance = null)
    {
        distance ??= 10;
        minimum_distance ??= 0;
        if (!isdatum(target)) //we dont need to check if its not a datum!
            return TRUE;
        if (get_turf(pawn) == get_turf(target))
            return TRUE;
        DmList path = await get_path_to(pawn as atom_movable, target, simulated_only: Dm.num(!HAS_TRAIT(pawn, TRAIT_SPACEWALK)), mintargetdist: minimum_distance, max_distance: distance, access: get_access()) as DmList;
        return Dm.num(Dm.truthy(length(path)));
    }

    /// Called when a target was found but couldn't be reached. Base no-op; override to record the target (e.g. add it to an ignore list).
    // tg: code/datums/ai/_ai_controller.dm:704
    public virtual void note_unreachable_target(atom target = null)
    {
    }

    /// Returns true if we have a blackboard key with the provided key and it is not qdeleting
    // tg: code/datums/ai/_ai_controller.dm:708
    public virtual object blackboard_key_exists(object key = null)
    {
        object key_value = blackboard[key];
        if (key_value is datum key_datum)
            return Dm.num(!QDELETED(key_datum));
        if (key_value is DmList key_list)
            return Dm.num(length(key_list) > 0);
        return Dm.num(Dm.truthy(key_value));
    }

    /// Used for above to track all the keys that have registered a signal
    // tg: code/datums/ai/_ai_controller.dm:757
    private const string TRAIT_AI_TRACKING = "tracked_by_ai";

    /**
     * Used to manage references to datum by AI controllers
     *
     * * tracked_datum - something being added to an ai blackboard
     * * key - the associated key
     * False when tg's macro returns from the proc that uses it.
     */
    // tg: code/datums/ai/_ai_controller.dm:723
    private bool TRACK_AI_DATUM_TARGET(object tracked_datum, object key)
    {
        if (isweakref(tracked_datum))
        {
            stack_trace(Dm.text("Weakref (Actual datum: []) found in ai datum blackboard! This is an outdated method of ai reference handling, please remove it.", ((datum_weakref)tracked_datum).resolve()), "code/datums/ai/_ai_controller.dm", 726);
        }
        else if (tracked_datum is datum _tracked_datum)
        {
            if (QDELETED(_tracked_datum))
            {
                stack_trace(Dm.text("Tried to track a qdeleted datum ([]) in ai datum blackboard (key: [])! Please ensure that we are not doing this by adding handling where necessary.", _tracked_datum, key), "code/datums/ai/_ai_controller.dm", 733);
                return false;
            }
            string source = Dm.text("[]_[]", REF(this), key);
            if (!HAS_TRAIT_FROM(_tracked_datum, TRAIT_AI_TRACKING, source))
            {
                RegisterSignal(_tracked_datum, COMSIG_QDELETING, nameof(sig_remove_from_blackboard), @override: TRUE);
                ADD_TRAIT(_tracked_datum, TRAIT_AI_TRACKING, source);
            }
        }
        return true;
    }

    /**
     * Used to clear previously set reference handing by AI controllers
     *
     * * tracked_datum - something being removed from an ai blackboard
     * * key - the associated key
     */
    // tg: code/datums/ai/_ai_controller.dm:747
    private void CLEAR_AI_DATUM_TARGET(object tracked_datum, object key)
    {
        if (tracked_datum is datum _tracked_datum)
        {
            REMOVE_TRAIT(_tracked_datum, TRAIT_AI_TRACKING, Dm.text("[]_[]", REF(this), key));
            if (!HAS_TRAIT(_tracked_datum, TRAIT_AI_TRACKING))
                UnregisterSignal(_tracked_datum, COMSIG_QDELETING);
        }
    }

    /**
     * Sets the key to the passed "thing".
     *
     * * key - A blackboard key
     * * thing - a value to set the blackboard key to.
     * * track_datum - whether we should track this ref for deletion, this should always be TRUE unless you really know wtf you're doing
     */
    // tg: code/datums/ai/_ai_controller.dm:766
    public virtual void set_blackboard_key(object key = null, object thing = null, float? track_datum = null)
    {
        track_datum ??= TRUE;
        // Assume it is an error when trying to set a value overtop a list
        if (islist(blackboard[key]))
            throw Dm.CRASH(Dm.text("set_blackboard_key attempting to set a blackboard value to key [] when it's a list!", key), "code/datums/ai/_ai_controller.dm", 769);
        // Don't do anything if it's already got this value
        if (Dm.eq(blackboard[key], thing))
            return;

        // Clear existing values
        if (blackboard[key] != null)
            clear_blackboard_key(key);

        if (Dm.truthy(track_datum) && !TRACK_AI_DATUM_TARGET(thing, key))
            return;
        blackboard[key] = thing;
        post_blackboard_key_set(key);
    }

    /**
     * Helper to force a key to be a certain thing no matter what's already there
     *
     * Useful for if you're overriding a list with a new list entirely,
     * as otherwise it would throw a runtime error from trying to override a list
     *
     * Not necessary to use if you aren't dealing with lists, as set_blackboard_key will clear the existing value
     * in that case already, but may be useful for clarity.
     *
     * * key - A blackboard key
     * * thing - a value to set the blackboard key to.
     */
    // tg: code/datums/ai/_ai_controller.dm:796
    public virtual void override_blackboard_key(object key = null, object thing = null)
    {
        if (Dm.eq(blackboard[key], thing))
            return;

        clear_blackboard_key(key);
        set_blackboard_key(key, thing);
    }

    /**
     * Sets the key at index thing to the passed value
     *
     * Assumes the key value is already a list, if not throws an error.
     *
     * * key - A blackboard key, with its value set to a list
     * * thing - a value which becomes the inner list value's key
     * * value - what to set the inner list's value to
     */
    // tg: code/datums/ai/_ai_controller.dm:812
    public virtual void set_blackboard_key_assoc(object key = null, object thing = null, object value = null)
    {
        if (!islist(blackboard[key]))
            throw Dm.CRASH(Dm.text("set_blackboard_key_assoc called on non-list key []!", key), "code/datums/ai/_ai_controller.dm", 814);
        DmList inner = (DmList)blackboard[key];
        // Don't do anything if it's already got this value
        if (Dm.eq(inner[thing], value))
            return;

        if (!TRACK_AI_DATUM_TARGET(thing, key))
            return;
        if (!TRACK_AI_DATUM_TARGET(value, key))
            return;
        inner[thing] = value;
        post_blackboard_key_set(key);
    }

    /**
     * Similar to [proc/set_blackboard_key_assoc] but operates under the assumption the key is a lazylist (so it will create a list)
     * More dangerous / easier to override values, only use when you want to use a lazylist
     *
     * * key - A blackboard key, with its value set to a list
     * * thing - a value which becomes the inner list value's key
     * * value - what to set the inner list's value to
     */
    // tg: code/datums/ai/_ai_controller.dm:832
    public virtual void set_blackboard_key_assoc_lazylist(object key = null, object thing = null, object value = null)
    {
        DmList inner = blackboard[key] as DmList ?? (DmList)(blackboard[key] = Dm.list());
        // Don't do anything if it's already got this value
        if (Dm.eq(inner[thing], value))
            return;

        if (!TRACK_AI_DATUM_TARGET(thing, key))
            return;
        if (!TRACK_AI_DATUM_TARGET(value, key))
            return;
        inner[thing] = value;
        post_blackboard_key_set(key);
    }

    /**
     * Called after we set a blackboard key, forwards signal information.
     */
    // tg: code/datums/ai/_ai_controller.dm:845
    public virtual void post_blackboard_key_set(object key = null)
    {
        if (pawn == null)
            return;
        string sig = COMSIG_AI_BLACKBOARD_KEY_SET(key);
        if (Dm.truthy(pawn._listen_lookup?[sig]))
            pawn._SendSignal(sig, Dm.list(pawn, key));
    }

    /**
     * Adds the passed "thing" to the associated key
     *
     * Works with lists or numbers, but not lazylists.
     *
     * * key - A blackboard key
     * * thing - a value to set the blackboard key to.
     */
    // tg: code/datums/ai/_ai_controller.dm:857
    public virtual void add_blackboard_key(object key = null, object thing = null)
    {
        if (!TRACK_AI_DATUM_TARGET(thing, key))
            return;
        Dm.append_at(blackboard, key, thing);
    }

    /**
     * Similar to [proc/add_blackboard_key], but performs an insertion rather than an add
     * Throws an error if the key is not a list already, intended only for use with lists
     *
     * * key - A blackboard key, with its value set to a list
     * * thing - a value to set the blackboard key to.
     */
    // tg: code/datums/ai/_ai_controller.dm:868
    public virtual void insert_blackboard_key(object key = null, object thing = null)
    {
        if (!islist(blackboard[key]))
            throw Dm.CRASH(Dm.text("insert_blackboard_key called on non-list key []!", key), "code/datums/ai/_ai_controller.dm", 870);
        if (!TRACK_AI_DATUM_TARGET(thing, key))
            return;
        Dm.combine_at(blackboard, key, thing);
    }

    /**
     * Adds the passed "thing" to the associated key, assuming key is intended to be a lazylist (so it will create a list)
     * More dangerous / easier to override values, only use when you want to use a lazylist
     *
     * * key - A blackboard key
     * * thing - a value to set the blackboard key to.
     */
    // tg: code/datums/ai/_ai_controller.dm:881
    public virtual void add_blackboard_key_lazylist(object key = null, object thing = null)
    {
        if (blackboard[key] == null)
            blackboard[key] = Dm.list();
        if (!TRACK_AI_DATUM_TARGET(thing, key))
            return;
        Dm.append_at(blackboard, key, thing);
    }

    /**
     * Similar to [proc/insert_blackboard_key_lazylist], but performs an insertion / or rather than an add
     *
     * * key - A blackboard key
     * * thing - a value to set the blackboard key to.
     */
    // tg: code/datums/ai/_ai_controller.dm:892
    public virtual void insert_blackboard_key_lazylist(object key = null, object thing = null)
    {
        if (blackboard[key] == null)
            blackboard[key] = Dm.list();
        if (!TRACK_AI_DATUM_TARGET(thing, key))
            return;
        Dm.combine_at(blackboard, key, thing);
        post_blackboard_key_set(key);
    }

    /**
     * Adds the value to the inner list at key with the inner key set to "thing"
     * Throws an error if the key is not a list already, intended only for use with lists
     *
     * * key - A blackboard key, with its value set to a list
     * * thing - a value which becomes the inner list value's key
     * * value - what to set the inner list's value to
     */
    // tg: code/datums/ai/_ai_controller.dm:905
    public virtual void add_blackboard_key_assoc(object key = null, object thing = null, object value = null)
    {
        if (!islist(blackboard[key]))
            throw Dm.CRASH(Dm.text("add_blackboard_key_assoc called on non-list key []!", key), "code/datums/ai/_ai_controller.dm", 907);
        if (!TRACK_AI_DATUM_TARGET(thing, key))
            return;
        if (!TRACK_AI_DATUM_TARGET(value, key))
            return;
        Dm.append_at((DmList)blackboard[key], thing, value);
    }

    /**
     * Similar to [proc/add_blackboard_key_assoc], assuming key is intended to be a lazylist (so it will create a list)
     * More dangerous / easier to override values, only use when you want to use a lazylist
     *
     * * key - A blackboard key, with its value set to a list
     * * thing - a value which becomes the inner list value's key
     * * value - what to set the inner list's value to
     */
    // tg: code/datums/ai/_ai_controller.dm:920
    public virtual void add_blackboard_key_assoc_lazylist(object key = null, object thing = null, object value = null)
    {
        if (blackboard[key] == null)
            blackboard[key] = Dm.list();
        if (!TRACK_AI_DATUM_TARGET(thing, key))
            return;
        if (!TRACK_AI_DATUM_TARGET(value, key))
            return;
        Dm.append_at((DmList)blackboard[key], thing, value);
    }

    /**
     * Clears the passed key, resetting it to null
     *
     * Not intended for use with list keys - use [proc/remove_thing_from_blackboard_key] if you are removing a value from a list at a key
     *
     * * key - A blackboard key
     */
    // tg: code/datums/ai/_ai_controller.dm:933
    public virtual void clear_blackboard_key(object key = null)
    {
        if (blackboard[key] == null)
            return;
        string preclear = COMSIG_AI_BLACKBOARD_KEY_PRECLEAR(key);
        if (pawn != null && Dm.truthy(Dm.truthy(pawn._listen_lookup?[preclear]) ? pawn._SendSignal(preclear, Dm.list(pawn)) : NONE))
            return;
        CLEAR_AI_DATUM_TARGET(blackboard[key], key);
        blackboard[key] = null;
        if (pawn == null)
            return;
        string cleared = COMSIG_AI_BLACKBOARD_KEY_CLEARED(key);
        if (Dm.truthy(pawn._listen_lookup?[cleared]))
            pawn._SendSignal(cleared, Dm.list(pawn, key));
    }

    /**
     * Remove the passed thing from the associated blackboard key
     *
     * Intended for use with lists, if you're just clearing a reference from a key use [proc/clear_blackboard_key]
     *
     * * key - A blackboard key
     * * thing - a value to set the blackboard key to.
     */
    // tg: code/datums/ai/_ai_controller.dm:951
    public virtual void remove_thing_from_blackboard_key(object key = null, object thing = null)
    {
        object associated_value = blackboard[key];
        if (associated_value == null)
            return;
        if (Dm.eq(thing, associated_value))
        {
            stack_trace("remove_thing_from_blackboard_key was called un-necessarily in a situation where clear_blackboard_key would suffice. ", "code/datums/ai/_ai_controller.dm", 956);
            clear_blackboard_key(key);
            return;
        }

        if (associated_value is not DmList associated_list)
            throw Dm.CRASH(Dm.text("remove_thing_from_blackboard_key called with an invalid \"thing\" argument ([]). (The associated value of the passed key is not a list and is also not the passed thing, meaning it is clearing an unintended value.)", thing), "code/datums/ai/_ai_controller.dm", 961);

        foreach (object inner_key in Dm.each(associated_list))
        {
            if (Dm.eq(inner_key, thing))
            {
                // flat list
                CLEAR_AI_DATUM_TARGET(thing, key);
                Dm.remove(ref associated_list, thing);
                return;
            }
            else if (Dm.eq(associated_list[inner_key], thing))
            {
                // assoc list
                CLEAR_AI_DATUM_TARGET(thing, key);
                Dm.remove(ref associated_list, inner_key);
                return;
            }
        }

        throw Dm.CRASH(Dm.text("remove_thing_from_blackboard_key called with an invalid \"thing\" argument ([]). (The passed value is not tracked in the passed list.)", thing), "code/datums/ai/_ai_controller.dm", 977);
    }

    ///removes a tracked object from a lazylist
    // tg: code/datums/ai/_ai_controller.dm:981
    public virtual void remove_from_blackboard_lazylist_key(object key = null, object thing = null)
    {
        DmList lazylist = blackboard[key] as DmList;
        if (lazylist == null)
            return;
        foreach (object key_index in Dm.each(lazylist))
        {
            if (Dm.eq(thing, key_index) || Dm.eq(lazylist[key_index], thing))
            {
                CLEAR_AI_DATUM_TARGET(thing, key);
                Dm.remove(ref lazylist, key_index);
                break;
            }
        }
        if (!Dm.truthy(LAZYLEN(lazylist)))
            clear_blackboard_key(key);
    }

    /// Signal proc to go through every key and remove the datum from all keys it finds
    // tg: code/datums/ai/_ai_controller.dm:993
    [SignalHandler]
    public virtual void sig_remove_from_blackboard(datum source = null)
    {
        DmList remove_queue = Dm.list(blackboard);
        float index = 1;
        while (index <= length(remove_queue))
        {
            DmList next_to_clear = remove_queue[index] as DmList;
            foreach (object inner_value in Dm.each(next_to_clear))
            {
                // We are a lists of lists, add the next value to the queue so we can handle references in there
                // (But we only need to bother checking the list if it's not empty.)
                if (inner_value is DmList inner_list && Dm.truthy(length(inner_list)))
                    UNTYPED_LIST_ADD(ref remove_queue, inner_value);

                // We found the value that's been deleted. Clear it out from this list
                else if (Dm.eq(inner_value, source))
                    Dm.remove(ref next_to_clear, inner_value);

                //if this is the case stop here. This means the list isn't associative (because an assoc list couldnt have a key for a number!)
                if (isnum(inner_value))
                    continue;

                object associated_value = next_to_clear[inner_value];
                if (!Dm.truthy(associated_value)) //This wasn't an associated list! we lied! its all been a trick. Try again next time.
                    continue;
                // We are an assoc lists of lists, the list at the next value so we can handle references in there
                // (But again, we only need to bother checking the list if it's not empty.)
                if (associated_value is DmList associated_list && Dm.truthy(length(associated_list)))
                    UNTYPED_LIST_ADD(ref remove_queue, associated_value);

                // We found the value that's been deleted, it was an assoc value. Clear it out entirely
                else if (Dm.eq(associated_value, source))
                {
                    Dm.remove(ref next_to_clear, inner_value);
                    string cleared = COMSIG_AI_BLACKBOARD_KEY_CLEARED(inner_value);
                    if (Dm.truthy(pawn._listen_lookup?[cleared]))
                        pawn._SendSignal(cleared, Dm.list(pawn));
                }
            }

            index += 1;
        }
    }
}

public static partial class GlobalProcs
{
    /// Resolves the compiled JSON path from a behavior tree's source JSON path (e.g. "code/datums/ai/dog/dog.bt.json" -> "build/behavior_trees/code/datums/ai/dog/dog.bt.compiled.json").
    // tg: code/__DEFINES/ai/behavior_trees.dm:67
    public static string BT_COMPILED_PATH(string json_path) => replacetext("build/behavior_trees/" + json_path, ".json", ".compiled.json");
}

/// The compiled behaviour trees: tools/build-behavior-trees.sh runs tg's tools/build_bt.py and copies its output to BehaviorTrees/, embedded in this assembly, so the server needs no tg clone
/// at run time (DV251). Each reader decodes its own copy of the text, as tg's file2text and json_decode do; a world keeps the texts it read (rules 10.6).
public static class BehaviorTreeFiles
{
    public static string Read(string compiled_path)
    {
        var cache = TgState.Current.behavior_tree_texts;
        if (cache.TryGetValue(compiled_path, out string text))
            return text;
        using var stream = typeof(BehaviorTreeFiles).Assembly.GetManifestResourceStream(compiled_path)
            ?? throw new DmRuntimeException("no such behaviour tree file: " + compiled_path);
        using var reader = new System.IO.StreamReader(stream);
        return cache[compiled_path] = reader.ReadToEnd();
    }
}

public sealed partial class TgState
{
    internal readonly System.Collections.Generic.Dictionary<string, string> behavior_tree_texts = new();
}
