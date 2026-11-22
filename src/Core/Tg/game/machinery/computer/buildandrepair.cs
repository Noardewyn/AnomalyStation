// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/buildandrepair.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 (the computer frames, D171): the whole file. The computer frame's five states (empty, board installed, board secured, wired,
// glassed), each with its icon_state ("0" to "4" of stock_parts.dmi, the frame's update_icon_state): the wrench anchors it, a
// computer board goes in by hand, the screwdriver secures it, five cable and two glass panels follow, the screwdriver completes the
// computer from the board's build_path; the wirecutter, crowbar and screwdriver take it apart in reverse, and deconstruction leaves
// the glass and cable. The rapid part exchange device's paths (install_parts_from_part_replacer) came with the RPED (gaps
// OD3). The GAP: tg's `circuit` var is a Type here, the board instance is circuit_value (as the machine frame's).
// The var blocks are generated.

// tg: code/game/machinery/computer/buildandrepair.dm:1
public partial class obj_structure_frame_computer : obj_structure_frame
{
    // tg: code/game/machinery/computer/buildandrepair.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        register_context();
        return dot;
    }

    // tg: code/game/machinery/computer/buildandrepair.dm:14
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        atom drop_loc = drop_location() as atom;
        if (Dm.eq(state, FRAME_COMPUTER_STATE_GLASSED))
        {
            if (Dm.truthy(disassembled))
                Dm.@new<obj_item_stack_sheet_glass>(drop_loc, 2);
            else
            {
                Dm.@new<obj_item_shard>(drop_loc);
                Dm.@new<obj_item_shard>(drop_loc);
            }
        }
        if (Dm.num(state) >= FRAME_COMPUTER_STATE_WIRED)
            Dm.@new<obj_item_stack_cable_coil>(drop_loc, 5);
        await base.atom_deconstruct(disassembled);
    }

    // tg: code/game/machinery/computer/buildandrepair.dm:26
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if (held_item == null)
            return dot;

        object sw__1 = state;
        if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_EMPTY))
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
            {
                context[SCREENTIP_CONTEXT_LMB] = Dm.text("[]chor", Dm.truthy(anchored) ? "Unan" : "An");
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (Dm.truthy(anchored) && held_item is obj_item_circuitboard_computer)
            {
                context[SCREENTIP_CONTEXT_LMB] = "Install board";
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
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_BOARD_INSTALLED))
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Pry out board";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Secure board";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_BOARD_SECURED))
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Unsecure board";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (held_item is obj_item_stack_cable_coil)
            {
                context[SCREENTIP_CONTEXT_LMB] = "Install cable";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_WIRED))
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_WIRECUTTER))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Cut out cable";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (held_item is obj_item_stack_sheet_glass)
            {
                context[SCREENTIP_CONTEXT_LMB] = "Install panel";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_GLASSED))
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Pry out glass";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Complete frame";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }
        return dot;
    }

    // tg: code/game/machinery/computer/buildandrepair.dm:74
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        object sw__1 = state;
        if (Dm.eq(sw__1, FRAME_STATE_EMPTY))
        {
            Dm.append(ref dot, span_notice(I18n.T("It can be [] []", EXAMINE_HINT(I18n.T("anchored")), Dm.truthy(anchored) ? I18n.T("loose.") : I18n.T("into place."))));
            if (Dm.truthy(anchored))
                Dm.append(ref dot, span_warning(I18n.T("It's missing a circuit board!")));
            else
                Dm.append(ref dot, span_notice(I18n.T("It can be [] or [] apart.", EXAMINE_HINT(I18n.T("welded")), EXAMINE_HINT(I18n.T("screwed")))));
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_BOARD_INSTALLED))
        {
            Dm.append(ref dot, span_notice(I18n.T("The circuit board can be [] out.", EXAMINE_HINT(I18n.T("pried")))));
            Dm.append(ref dot, span_info(I18n.T("A circuit board is installed and should be [] in place.", EXAMINE_HINT(I18n.T("screwed")))));
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_BOARD_SECURED))
        {
            Dm.append(ref dot, span_notice(I18n.T("The circuit board can be [] loose.", EXAMINE_HINT(I18n.T("screwed")))));
            Dm.append(ref dot, span_info(I18n.T("It should be [] with 5 cables.", EXAMINE_HINT(I18n.T("wired")))));
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_WIRED))
        {
            Dm.append(ref dot, span_notice(I18n.T("Its wires can be [].", EXAMINE_HINT(I18n.T("cut")))));
            Dm.append(ref dot, span_info(I18n.T("It should be [] with 2 glass panels.", EXAMINE_HINT(I18n.T("fitted")))));
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_GLASSED))
        {
            Dm.append(ref dot, span_notice(I18n.T("The screen can be [] out.", EXAMINE_HINT(I18n.T("pried")))));
            Dm.append(ref dot, span_info(I18n.T("The monitor should be [] on to complete it.", EXAMINE_HINT(I18n.T("screwed")))));
        }
        return dot;
    }

    // tg: code/game/machinery/computer/buildandrepair.dm:97
    public override void circuit_added(obj_item_circuitboard added = null)
    {
        state = FRAME_COMPUTER_STATE_BOARD_INSTALLED;
        update_appearance(UPDATE_ICON_STATE);
    }

    // tg: code/game/machinery/computer/buildandrepair.dm:101
    public override void circuit_removed(obj_item_circuitboard removed = null)
    {
        state = FRAME_COMPUTER_STATE_EMPTY;
        update_appearance(UPDATE_ICON_STATE);
    }

    // tg: code/game/machinery/computer/buildandrepair.dm:105
    public override async DmTask<object> install_board(mob_living user = null, obj_item_circuitboard board = null, float? by_hand = null)
    {
        if (!Dm.eq(state, FRAME_COMPUTER_STATE_EMPTY))
        {
            balloon_alert(user, I18n.T("circuit already installed!"));
            return FALSE;
        }
        object dot = await base.install_board(user, board, by_hand);
        if (Dm.truthy(dot) && !Dm.truthy(by_hand)) // Installing via RPED auto-secures it
        {
            state = FRAME_COMPUTER_STATE_BOARD_SECURED;
            update_appearance(UPDATE_ICON_STATE);
        }
        return dot;
    }

    // tg: code/game/machinery/computer/buildandrepair.dm:115
    public override async DmTask<object> install_parts_from_part_replacer(mob_living user = null, obj_item_storage_part_replacer replacer = null, float? no_sound = null)
    {
        no_sound ??= FALSE;
        switch ((int)Dm.num(state))
        {
            case FRAME_COMPUTER_STATE_BOARD_SECURED:
            {
                obj_item_stack_cable_coil cable = Dm.locate_type(typeof(obj_item_stack_cable_coil), replacer.contents) as obj_item_stack_cable_coil;
                if (cable == null)
                    return FALSE;

                if (Dm.truthy(await add_cabling(user, cable, time: 0)))
                {
                    if (!Dm.truthy(no_sound))
                    {
                        replacer.play_rped_effect();
                        no_sound = TRUE;
                    }
                    return await install_parts_from_part_replacer(user, replacer, no_sound: no_sound);  // Recursive call to handle the next part
                }

                return FALSE;
            }

            case FRAME_COMPUTER_STATE_WIRED:
            {
                obj_item_stack_sheet_glass glass_sheets = Dm.locate_type(typeof(obj_item_stack_sheet_glass), replacer.contents) as obj_item_stack_sheet_glass;
                if (glass_sheets == null)
                    return FALSE;

                if (Dm.truthy(await add_glass(user, glass_sheets, time: 0)))
                {
                    if (!Dm.truthy(no_sound))
                        replacer.play_rped_effect();
                    return TRUE;
                }

                return FALSE;
            }
        }
        return null;
    }

    // tg: code/game/machinery/computer/buildandrepair.dm:142
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;

        object sw__1 = state;
        if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_EMPTY))
        {
            if (tool is obj_item_storage_part_replacer)
                return Dm.truthy(await install_circuit_from_part_replacer(user, (obj_item_storage_part_replacer)tool)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_BOARD_SECURED))
        {
            if (tool is obj_item_stack_cable_coil)
                return Dm.truthy(await add_cabling(user, (obj_item_stack_cable_coil)tool)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;

            if (tool is obj_item_storage_part_replacer)
                return Dm.truthy(await install_parts_from_part_replacer(user, (obj_item_storage_part_replacer)tool)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_WIRED))
        {
            if (tool is obj_item_stack_sheet_glass)
                return Dm.truthy(await add_glass(user, (obj_item_stack_sheet_glass)tool)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;

            if (tool is obj_item_storage_part_replacer)
                return Dm.truthy(await install_parts_from_part_replacer(user, (obj_item_storage_part_replacer)tool)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        }
        return dot;
    }

    // tg: code/game/machinery/computer/buildandrepair.dm:166
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act(user, tool);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;

        object sw__1 = state;
        if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_BOARD_INSTALLED))
        {
            tool.play_tool_sound(this);
            balloon_alert(user, I18n.T("circuit secured"));
            state = FRAME_COMPUTER_STATE_BOARD_SECURED;
            update_appearance(UPDATE_ICON_STATE);
            return ITEM_INTERACT_SUCCESS;
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_BOARD_SECURED))
        {
            tool.play_tool_sound(this);
            balloon_alert(user, I18n.T("circuit unsecured"));
            state = FRAME_COMPUTER_STATE_BOARD_INSTALLED;
            update_appearance(UPDATE_ICON_STATE);
            return ITEM_INTERACT_SUCCESS;
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_WIRED))
        {
            if (!Dm.truthy(user.combat_mode))
            {
                balloon_alert(user, I18n.T("no glass!"));
                return ITEM_INTERACT_BLOCKING;
            }
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_GLASSED))
        {
            if (Dm.truthy(await finalize_construction(user, tool)))
                return ITEM_INTERACT_SUCCESS;
            return ITEM_INTERACT_BLOCKING;
        }
        return dot;
    }

    // tg: code/game/machinery/computer/buildandrepair.dm:196
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        object sw__1 = state;
        if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_BOARD_INSTALLED))
        {
            tool.play_tool_sound(this);
            balloon_alert(user, I18n.T("circuit removed"));
            circuit_value.add_fingerprint(user);
            await circuit_value.forceMove(drop_location() as atom);
            return ITEM_INTERACT_SUCCESS;
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_BOARD_SECURED))
        {
            balloon_alert(user, I18n.T("unsecure the circuit!"));
            return ITEM_INTERACT_BLOCKING;
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_WIRED))
        {
            balloon_alert(user, I18n.T("remove the wiring!"));
            return ITEM_INTERACT_BLOCKING;
        }
        else if (Dm.eq(sw__1, FRAME_COMPUTER_STATE_GLASSED))
        {
            tool.play_tool_sound(this);
            balloon_alert(user, I18n.T("glass removed"));
            state = FRAME_COMPUTER_STATE_WIRED;
            update_appearance(UPDATE_ICON_STATE);
            obj_item_stack_sheet_glass dropped_glass = Dm.@new<obj_item_stack_sheet_glass>(drop_location(), 2);
            if (!QDELETED(dropped_glass))
                dropped_glass.add_fingerprint(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/game/machinery/computer/buildandrepair.dm:226
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (!Dm.eq(state, FRAME_COMPUTER_STATE_WIRED))
            return ITEM_INTERACT_BLOCKING;

        tool.play_tool_sound(this);
        balloon_alert(user, I18n.T("cables removed"));
        state = FRAME_COMPUTER_STATE_BOARD_SECURED;
        update_appearance(UPDATE_ICON_STATE);

        obj_item_stack_cable_coil dropped_cables = Dm.@new<obj_item_stack_cable_coil>(drop_location(), 5);
        if (!QDELETED(dropped_cables))
            dropped_cables.add_fingerprint(user);
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Adds cable to the computer to wire it
     * Arguments
     *
     * * mob/living/user - the player who is adding the cable
     * * obj/item/stack/cable_coil/cable - the cable we are trying to add
     * * time - time taken to complete the operation
     */
    // tg: code/game/machinery/computer/buildandrepair.dm:251
    [DmPrivate]
    public virtual async DmTask<object> add_cabling(mob_living user = null, obj_item_stack_cable_coil cable = null, float? time = null)
    {
        time ??= 2 * SECONDS;

        if (!Dm.eq(state, FRAME_COMPUTER_STATE_BOARD_SECURED))
            return FALSE;
        if (!Dm.truthy(cable.tool_start_check(user, amount: 5)))
            return FALSE;
        if (time > 0)
            balloon_alert(user, I18n.T("adding cables..."));
        if (!Dm.truthy(await cable.use_tool(this, user, time, volume: 50, amount: 5)) || !Dm.eq(state, FRAME_COMPUTER_STATE_BOARD_SECURED))
            return FALSE;

        state = FRAME_COMPUTER_STATE_WIRED;
        update_appearance(UPDATE_ICON_STATE);
        return TRUE;
    }

    /**
     * Adds glass sheets to the computer to complete the screen
     * Arguments
     *
     * * mob/living/user - the player who is adding the glass
     * * obj/item/stack/sheet/glass/glass - the glass we are trying to add
     * * time - time taken to complete the operation
     */
    // tg: code/game/machinery/computer/buildandrepair.dm:275
    [DmPrivate]
    public virtual async DmTask<object> add_glass(mob_living user = null, obj_item_stack_sheet_glass glass = null, float? time = null)
    {
        time ??= 2 * SECONDS;

        if (!Dm.eq(state, FRAME_COMPUTER_STATE_WIRED))
            return FALSE;
        if (!Dm.truthy(glass.tool_start_check(user, amount: 2)))
            return FALSE;
        if (time > 0)
        {
            playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            balloon_alert(user, I18n.T("adding glass..."));
        }
        if (!Dm.truthy(await glass.use_tool(this, user, time, amount: 2)) || !Dm.eq(state, FRAME_COMPUTER_STATE_WIRED))
            return FALSE;

        state = FRAME_COMPUTER_STATE_GLASSED;
        update_appearance(UPDATE_ICON_STATE);
        return TRUE;
    }

    // tg: code/game/machinery/computer/buildandrepair.dm:292
    public override async DmTask<object> finalize_construction(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("frame must be anchored!"));
            return FALSE;
        }

        tool.play_tool_sound(this);
        obj_machinery new_machine = Dm.@new(circuit_value.build_path, loc) as obj_machinery;
        new_machine.balloon_alert(user, I18n.T("monitor connected"));
        await new_machine.setDir(dir);
        transfer_fingerprints_to(new_machine);

        if (new_machine is obj_machinery_computer)
        {
            obj_machinery_computer new_computer = (obj_machinery_computer)new_machine;

            new_machine.clear_components();

            // Set anchor state and move the frame's parts over to the new machine.
            // Then refresh parts and call on_construction().
            await new_computer.set_anchored(anchored);
            new_computer.component_parts = Dm.list(circuit_value);
            new_computer.circuit = circuit;
            new_computer.circuit_value = circuit_value;

            await circuit_value.forceMove(new_computer);

            foreach (atom_movable movable_part in Dm.each<atom_movable>(contents.Copy()))
            {
                await movable_part.forceMove(new_computer);
                Dm.append(ref new_computer.component_parts, movable_part);
            }

            await new_computer.RefreshParts();
            await new_computer.on_construction(user);
        }

        qdel(this);
        return TRUE;
    }
}

/// Helpers for rcd
// tg: code/game/machinery/computer/buildandrepair.dm:327
public partial class obj_structure_frame_computer_rcd : obj_structure_frame_computer
{
    // tg: code/game/machinery/computer/buildandrepair.dm:332
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // yeah...
        name = "computer frame";
        icon = Dm.resource("icons/obj/devices/stock_parts.dmi");
        return base.Initialize(mapload, __extra);
    }
}
