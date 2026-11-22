// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/machine_frame.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 7 (machine frames, D148): the whole file. The machine frame's states (empty, wired with 5 cable, board installed),
// the board's required components (stacks, stock part datums, items) added by hand, prying them out, cutting the wires, and
// the screwdriver finishing the machine from the board's build_path with the frame's parts as its replacement parts. The
// RPED's part installation (install_parts_from_part_replacer, look_for) came with the RPED (gaps OD3). The examine is
// translated (X1). The var blocks are generated.

// tg: code/game/machinery/machine_frame.dm:1
public partial class obj_structure_frame_machine : obj_structure_frame
{
    // the board as the machine board (tg's circuit var is typed /obj/item/circuitboard/machine here)
    private obj_item_circuitboard_machine circuit_machine => circuit_value as obj_item_circuitboard_machine;

    // tg: code/game/machinery/machine_frame.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/game/machinery/machine_frame.dm:16
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(components);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/machine_frame.dm:20
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (Dm.num(state) >= FRAME_STATE_WIRED)
            Dm.@new<obj_item_stack_cable_coil>(drop_location(), 5);
        await dump_contents();
        await base.atom_deconstruct(disassembled);
    }

    // tg: code/game/machinery/machine_frame.dm:26
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if (held_item == null)
            return dot;

        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH) && !Dm.truthy(circuit_machine?.needs_anchored))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[]chor", Dm.truthy(anchored) ? "Unan" : "An");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        object sw__1 = state;
        if (Dm.eq(sw__1, FRAME_STATE_EMPTY))
        {
            if (held_item is obj_item_stack_cable_coil)
            {
                context[SCREENTIP_CONTEXT_LMB] = "Wire Frame";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (Dm.eq(held_item.tool_behaviour, TOOL_WELDER))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Unweld frame";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Disassemble frame";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }
        else if (Dm.eq(sw__1, FRAME_STATE_WIRED))
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_WIRECUTTER))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Cut wires";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (istype(held_item, board_type))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Insert board";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }
        else if (Dm.eq(sw__1, FRAME_STATE_BOARD_INSTALLED))
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Pry out components";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            {
                bool needs_components = false;
                foreach (object component in Dm.each(req_components))
                {
                    if (!Dm.truthy(req_components[component]))
                        continue;
                    needs_components = true;
                    break;
                }
                if (!needs_components)
                {
                    context[SCREENTIP_CONTEXT_LMB] = "Complete frame";
                    return CONTEXTUAL_SCREENTIP_SET;
                }
            }
            else if (held_item is not obj_item_storage_part_replacer)
            {
                foreach (object component in Dm.each(req_components))
                {
                    if (!Dm.truthy(req_components[component]))
                        continue;
                    Type stock_part_path = null;
                    if (ispath(component, typeof(obj_item)))
                        stock_part_path = (Type)component;
                    else if (ispath(component, typeof(datum_stock_part)))
                        stock_part_path = Dm.initial((Type)component, (datum_stock_part p) => p.physical_object_type);
                    if (istype(held_item, stock_part_path))
                    {
                        context[SCREENTIP_CONTEXT_LMB] = "Insert part";
                        return CONTEXTUAL_SCREENTIP_SET;
                    }
                }
            }
        }
        return dot;
    }

    // tg: code/game/machinery/machine_frame.dm:81
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(circuit_machine?.needs_anchored))
            Dm.append(ref dot, span_notice(I18n.T("It can be [] []", EXAMINE_HINT(I18n.T("anchored")), Dm.truthy(anchored) ? I18n.T("loose.") : I18n.T("into place."))));
        if (state == FRAME_STATE_EMPTY)
        {
            if (!Dm.truthy(anchored))
                Dm.append(ref dot, span_notice(I18n.T("It can be [] or [] apart.", EXAMINE_HINT(I18n.T("welded")), EXAMINE_HINT(I18n.T("screwed")))));
            Dm.append(ref dot, span_info(I18n.T("It should be [] with 5 cables.", EXAMINE_HINT(I18n.T("wired")))));
            return dot;
        }
        if (state == FRAME_STATE_WIRED)
            Dm.append(ref dot, span_notice(I18n.T("Its wires can be [].", EXAMINE_HINT(I18n.T("cut")))));
        if (state != FRAME_STATE_BOARD_INSTALLED)
        {
            Dm.append(ref dot, span_warning(I18n.T("It's missing a circuit board!")));
            return dot;
        }
        if (!Dm.truthy(length(req_components)))
        {
            Dm.append(ref dot, span_info(I18n.T("It requires no components.")));
            return dot;
        }

        DmList nice_list = Dm.list();
        foreach (object component in Dm.each(req_components))
        {
            if (!Dm.truthy(req_components[component]))
                continue;
            Dm.append(ref nice_list, Dm.list(I18n.T("[] []\\s", req_components[component], req_component_names[component])));
        }
        Dm.append(ref dot, span_info(I18n.T("It requires [].", english_list(nice_list, I18n.T("no more components")))));

        Dm.append(ref dot, span_notice(I18n.T("All the components can be [] out.", EXAMINE_HINT(I18n.T("pried")))));
        if (!Dm.truthy(length(nice_list)))
            Dm.append(ref dot, span_info(I18n.T("The frame should be [] to complete it.", EXAMINE_HINT(I18n.T("screwed")))));
        return dot;
    }

    // tg: code/game/machinery/machine_frame.dm:110
    public override async DmTask dump_contents(params object[] __extra)
    {
        atom drop_loc = drop_location() as atom;

        // We need a snowflake check for stack items since they don't exist anymore
        foreach (object component in Dm.each(circuit_machine?.req_components))
        {
            if (!ispath(component, typeof(obj_item_stack)))
                continue;
            Type stack_path = (Type)component;
            float stack_amount = Dm.num(circuit_machine.req_components[component]) - Dm.num(req_components[component]);
            if (stack_amount > 0)
                Dm.@new(stack_path, drop_loc, stack_amount);
        }

        // Rest of the stuff can just be spat out (this includes the circuitboard0)
        foreach (object component in Dm.each(components?.Copy()))
        {
            if (ismovable(component))
            {
                atom_movable atom_component = (atom_movable)component;
                await atom_component.forceMove(drop_loc);
            }
            else if (component is datum_stock_part stock_part_datum)
            {
                Type physical_object_type = Dm.initial(stock_part_datum, (datum_stock_part p) => p.physical_object_type);
                Dm.@new(physical_object_type, drop_loc);
            }
            else
                stack_trace(Dm.text("Invalid component [] was found in constructable frame", component), "code/game/machinery/machine_frame.dm", 133);
        }

        components = null;
        req_components = null;
        req_component_names = null;
    }

    // tg: code/game/machinery/machine_frame.dm:140
    public override async DmTask<object> install_board(mob_living user = null, obj_item_circuitboard board = null, float? by_hand = null)
    {
        by_hand ??= TRUE;
        if (Dm.eq(state, FRAME_STATE_EMPTY))
        {
            balloon_alert(user, I18n.T("needs wiring!"));
            return FALSE;
        }
        if (Dm.eq(state, FRAME_STATE_BOARD_INSTALLED))
        {
            balloon_alert(user, I18n.T("circuit already installed!"));
            return FALSE;
        }
        if (!Dm.truthy(anchored) && board is obj_item_circuitboard_machine machine_board && Dm.truthy(machine_board.needs_anchored))
        {
            balloon_alert(user, I18n.T("frame must be anchored!"));
            return FALSE;
        }

        return await base.install_board(user, board, by_hand);
    }

    // tg: code/game/machinery/machine_frame.dm:153
    public override void circuit_added(obj_item_circuitboard added = null)
    {
        obj_item_circuitboard_machine added_machine = (obj_item_circuitboard_machine)added;
        state = FRAME_STATE_BOARD_INSTALLED;
        update_appearance(UPDATE_ICON_STATE);

        //add circuit board as the first component to the list of components
        //required for part_replacer to locate it while exchanging parts
        //so it does not early return in /obj/machinery/proc/exchange_parts
        components = Dm.list(circuit_value);
        req_components = added_machine.req_components?.Copy();
        if (!Dm.truthy(req_components))
            return;

        //creates a list of names from all the required parts
        req_component_names = Dm.list();
        foreach (object component_path in Dm.each(req_components))
        {
            if (!ispath(component_path))
                continue;

            if (ispath(component_path, typeof(obj_item_stack)))
            {
                Type stack_path = (Type)component_path;
                if (Dm.truthy(Dm.initial(stack_path, (obj_item_stack p) => p.singular_name)))
                    req_component_names[component_path] = Dm.initial(stack_path, (obj_item_stack p) => p.singular_name);
                else
                    req_component_names[component_path] = Dm.initial(stack_path, (obj_item_stack p) => p.name);
            }
            else if (ispath(component_path, typeof(datum_stock_part)))
            {
                Type stock_part = (Type)component_path;
                Type physical_object_type = Dm.initial(stock_part, (datum_stock_part p) => p.physical_object_type);

                req_component_names[component_path] = Dm.initial(physical_object_type, (obj_item p) => p.name);
            }
            else if (ispath(component_path, typeof(obj_item_stock_parts)))
            {
                Type stock_part = (Type)component_path;

                if (!Dm.truthy(added_machine.specific_parts) && Dm.truthy(Dm.initial(stock_part, (obj_item_stock_parts p) => p.base_name)))
                    req_component_names[component_path] = Dm.initial(stock_part, (obj_item_stock_parts p) => p.base_name);
                else
                    req_component_names[component_path] = Dm.initial(stock_part, (obj_item_stock_parts p) => p.name);
            }
            else if (ispath(component_path, typeof(obj_item)))
            {
                Type part = (Type)component_path;

                req_component_names[component_path] = Dm.initial(part, (obj_item p) => p.name);
            }
            else
            {
                stack_trace(Dm.text("Invalid component part [] in [], couldn't get its name", component_path, GetType()), "code/game/machinery/machine_frame.dm", 193);
                req_component_names[component_path] = Dm.text("[] (this is a bug)", component_path);
            }
        }
    }

    // tg: code/game/machinery/machine_frame.dm:197
    public override void circuit_removed(obj_item_circuitboard removed = null)
    {
        Dm.remove(ref components, removed);
        state = FRAME_STATE_WIRED;
        update_appearance(UPDATE_ICON_STATE);
    }

    /**
     * Returns the instance of path1 in list, else path2 in list
     *
     * Arguments
     * * parts - the list of parts to search
     * * path1 - the first path to search for
     * * path2 - the second path to search for, if path1 is not found
     */
    // tg: code/game/machinery/machine_frame.dm:210
    [DmPrivate]
    public virtual object look_for(DmList parts = null, object path1 = null, object path2 = null)
    {
        return Dm.locate_type((Type)path1, parts) ?? (path2 != null ? Dm.locate_type((Type)path2, parts) : null);
    }

    // tg: code/game/machinery/machine_frame.dm:215
    public override async DmTask<object> install_parts_from_part_replacer(mob_living user = null, obj_item_storage_part_replacer replacer = null, float? no_sound = null)
    {
        no_sound ??= FALSE;
        if (!Dm.truthy(length(replacer.contents)))
            return FALSE;
        float amt = 0;
        foreach (object path in Dm.each(req_components))
            amt += Dm.num(req_components[path]);
        if (amt == 0)
            return FALSE;

        bool play_sound = false;
        DmList part_list = replacer.get_sorted_parts(); //parts sorted in order of tier
        foreach (object path in Dm.each(req_components))
        {
            object target_path;
            if (ispath(path, typeof(datum_stock_part)))
                target_path = Dm.initial((Type)path, (datum_stock_part p) => p.physical_object_base_type);
            else
                target_path = path;

            obj_item part;
            while (Dm.num(req_components[path]) > 0 && (part = look_for(part_list, target_path, ispath(path, typeof(obj_item_stack_ore_bluespace_crystal)) ? typeof(obj_item_stack_sheet_bluespace_crystal) : null) as obj_item) != null)
            {
                Dm.remove(ref part_list, part);
                if (istype(part, typeof(obj_item_stack)))
                {
                    obj_item_stack S = (obj_item_stack)part;
                    float used_amt = min(round(Dm.num(S.get_amount())), Dm.num(req_components[path]));
                    object stack_name = S.singular_name;
                    if (used_amt == 0 || !Dm.truthy(await S.use(used_amt)))
                        continue;
                    req_components[path] = Dm.num(req_components[path]) - used_amt;
                    // No balloon alert here so they can look back and see what they added
                    to_chat(user, span_notice(I18n.T("You add [] [] to [].", used_amt, stack_name, this)));
                    play_sound = true;
                }
                else if (Dm.truthy(replacer.atom_storage.attempt_remove(part, this)))
                {
                    object stock_part_datum = GLOB.stock_part_datums_per_object[part.GetType()];
                    if (stock_part_datum != null)
                    {
                        Dm.append(ref components, stock_part_datum);
                        qdel(part);
                    }
                    else
                    {
                        Dm.append(ref components, part);
                        await part.forceMove(this);
                    }
                    req_components[path] = Dm.num(req_components[path]) - 1;
                    // No balloon alert here so they can look back and see what they added
                    to_chat(user, span_notice(I18n.T("You add [] to [].", part, this)));
                    play_sound = true;
                }
            }
        }

        if (play_sound && !Dm.truthy(no_sound))
            replacer.play_rped_effect();

        return TRUE;
    }

    // tg: code/game/machinery/machine_frame.dm:265
    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        object dot = base.can_be_unfasten_wrench(user, silent);
        if (!Dm.eq(dot, SUCCESSFUL_UNFASTEN))
            return dot;

        if (Dm.truthy(circuit_machine?.needs_anchored))
        {
            balloon_alert(user, I18n.T("frame must be anchored!"));
            return FAILED_UNFASTEN;
        }

        return dot;
    }

    // tg: code/game/machinery/machine_frame.dm:276
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act(user, tool);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (!Dm.eq(state, FRAME_STATE_BOARD_INSTALLED))
            return dot;
        if (Dm.truthy(await finalize_construction(user, tool)))
            return ITEM_INTERACT_SUCCESS;

        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/machinery/machine_frame.dm:287
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;
        if (!Dm.eq(state, FRAME_STATE_WIRED))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("removing cables..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)) || !Dm.eq(state, FRAME_STATE_WIRED))
            return ITEM_INTERACT_BLOCKING;

        state = FRAME_STATE_EMPTY;
        update_appearance(UPDATE_ICON_STATE);
        Dm.@new<obj_item_stack_cable_coil>(drop_location(), 5);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/machine_frame.dm:302
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;
        if (!Dm.eq(state, FRAME_STATE_BOARD_INSTALLED))
            return ITEM_INTERACT_BLOCKING;

        tool.play_tool_sound(this);
        DmList leftover_components = components.Copy();
        Dm.remove(ref leftover_components, circuit_value);
        await dump_contents();
        balloon_alert(user, I18n.T("circuit board[] removed", Dm.truthy(Dm.length(leftover_components)) ? " and components" : ""));
        // Circuit exited handles updating state
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Attempts to add the passed part to the frame
     *
     * Requires no sanity check that the passed part is a stock part
     *
     * Arguments
     * * user - the player
     * * tool - the part to add
     */
    // tg: code/game/machinery/machine_frame.dm:324
    [DmPrivate]
    public virtual async DmTask<object> add_part(mob_living user = null, obj_item tool = null)
    {
        foreach (object stock_part_base in Dm.each(req_components))
        {
            if (Dm.eq(req_components[stock_part_base], 0))
                continue;

            Type stock_part_path;

            if (ispath(stock_part_base, typeof(obj_item)))
                stock_part_path = (Type)stock_part_base;
            else if (ispath(stock_part_base, typeof(datum_stock_part)))
                stock_part_path = Dm.initial((Type)stock_part_base, (datum_stock_part p) => p.physical_object_type);
            else
            {
                stack_trace(Dm.text("Bad stock part in req_components: []", stock_part_base), "code/game/machinery/machine_frame.dm", 337);
                continue;
            }

            //if we require an bluespace crystall and we have an full sheet of them we can allow that
            if (ispath(stock_part_path, typeof(obj_item_stack_ore_bluespace_crystal)) && tool is obj_item_stack_sheet_bluespace_crystal)
            {
                //allow it
            }
            else if (!istype(tool, stock_part_path))
                continue;

            if (isstack(tool))
            {
                obj_item_stack S = (obj_item_stack)tool;
                float used_amt = min(round(Dm.num(S.get_amount())), Dm.num(req_components[stock_part_path]));
                if (used_amt > 0 && Dm.truthy(await S.use(used_amt)))
                {
                    req_components[stock_part_path] = Dm.num(req_components[stock_part_path]) - used_amt;
                    // No balloon alert here so they can look back and see what they added
                    to_chat(user, span_notice(I18n.T("You add [] to [].", tool, this)));
                }
                return null;
            }

            // We might end up qdel'ing the part if it's a stock part datum.
            // In practice, this doesn't have side effects to the name,
            // but academically we should not be using an object after it's deleted.
            string part_name = Dm.text("\\the []", tool);

            if (ispath(stock_part_base, typeof(datum_stock_part)))
            {
                // We can't just reuse stock_part_path here or its singleton,
                // or else putting in a tier 2 part will deconstruct to a tier 1 part.
                object stock_part_datum = GLOB.stock_part_datums_per_object[tool.GetType()];
                if (stock_part_datum == null)
                {
                    stack_trace(Dm.text("[] does not have an associated stock part datum!", tool.GetType()), "code/game/machinery/machine_frame.dm", 365);
                    continue;
                }

                Dm.append(ref components, stock_part_datum);

                // We regenerate the stock parts on deconstruct.
                // This technically means we lose unique qualities of the stock part, but
                // it's worth it for how dramatically this simplifies the code.
                // The only place I can see it affecting anything is like...RPG qualities. :P
                qdel(tool);
            }
            else if (Dm.truthy(await user.transferItemToLoc(tool, this)))
                Dm.append(ref components, tool);
            else
                break;

            // No balloon alert here so they can look back and see what they added
            to_chat(user, span_notice(I18n.T("You add [] to [].", part_name, this)));
            req_components[stock_part_base] = Dm.num(req_components[stock_part_base]) - 1;
            return TRUE;
        }

        balloon_alert(user, I18n.T("can't add that!"));
        return FALSE;
    }

    // tg: code/game/machinery/machine_frame.dm:390
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;

        object sw__1 = state;
        if (Dm.eq(sw__1, FRAME_STATE_EMPTY))
        {
            if (tool is obj_item_stack_cable_coil)
            {
                if (!Dm.truthy(tool.tool_start_check(user, amount: 5)))
                    return ITEM_INTERACT_BLOCKING;

                balloon_alert(user, I18n.T("adding cables..."));
                if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50, amount: 5)) || !Dm.eq(state, FRAME_STATE_EMPTY))
                    return ITEM_INTERACT_BLOCKING;

                state = FRAME_STATE_WIRED;
                update_appearance(UPDATE_ICON_STATE);
                return ITEM_INTERACT_SUCCESS;
            }
        }
        else if (Dm.eq(sw__1, FRAME_STATE_WIRED))
        {
            if (circuit_value == null && tool is obj_item_storage_part_replacer)
                return Dm.truthy(await install_circuit_from_part_replacer(user, (obj_item_storage_part_replacer)tool)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        }
        else if (Dm.eq(sw__1, FRAME_STATE_BOARD_INSTALLED))
        {
            if (tool is obj_item_storage_part_replacer)
                return Dm.truthy(await install_parts_from_part_replacer(user, (obj_item_storage_part_replacer)tool)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        }

        return dot;
    }

    // Override of base_item_interaction so we only try to add parts to the frame AFTER running item_interaction and all the tool_acts
    // tg: code/game/machinery/machine_frame.dm:420
    public override async DmTask<object> base_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object dot = await base.base_item_interaction(user, tool, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (Dm.truthy(user.combat_mode))
            return NONE;

        return Dm.truthy(await add_part(user, tool)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
    }

    /**
     * Attempt to finalize the construction of the frame into a machine
     * as according to our circuit and parts
     *
     * If successful, results in qdel'ing the frame and newing of a machine
     *
     * Arguments
     * * user - the player
     * * tool - the tool used to finalize the construction
     */
    // tg: code/game/machinery/machine_frame.dm:439
    public override async DmTask<object> finalize_construction(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(Dm.locate_type(circuit_value.build_path, loc)))
        {
            balloon_alert(user, I18n.T("identical machine present!"));
            return FALSE;
        }
        foreach (object component in Dm.each(req_components))
        {
            if (Dm.num(req_components[component]) > 0)
            {
                user.balloon_alert(user, I18n.T("missing components!"));
                return FALSE;
            }
        }

        if (!Dm.truthy(circuit_value.completion_requirements(this, user)))
            return FALSE;

        tool.play_tool_sound(this);
        // Prevent us from dropping stuff thanks to /Exited
        obj_item_circuitboard_machine leaving_circuit = circuit_machine;
        Dm.remove(ref components, leaving_circuit);
        leaving_circuit.replacement_parts = components;
        // Build the machine with the replacement parts
        circuit_value = null;
        obj_machinery new_machine = Dm.@new(leaving_circuit.build_path, loc, leaving_circuit) as obj_machinery;
        if (new_machine is obj_machinery)
        {
            // Set anchor state
            await new_machine.set_anchored(anchored);
            //Inform machine that its finished & cleanup
            await new_machine.on_construction(user);
            components = null;
        }
        qdel(this);
        return TRUE;
    }
}
