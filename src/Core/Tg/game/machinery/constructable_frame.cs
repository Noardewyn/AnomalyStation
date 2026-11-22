// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/constructable_frame.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 7 (machine frames, D148): the whole file. The construction frame: its board (circuit_value: GAP(types), tg's
// `circuit` var is generated as a Type), disassembly with a screwdriver or a welder, anchoring with a wrench, installing a
// board by hand. The rapid part exchange device's paths (install_circuit_from_part_replacer, ranged_item_interaction with the
// bluespace RPED) came with the RPED (gaps OD3). The examine
// is translated (X1). The var blocks are generated.

// tg: code/game/machinery/constructable_frame.dm:1
public partial class obj_structure_frame : obj_structure
{
    /// Reference to the circuit inside the frame (GAP(types): tg's VAR_FINAL circuit, generated as a Type)
    public obj_item_circuitboard circuit_value;

    // tg: code/game/machinery/constructable_frame.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_ICON_STATE);
        return dot;
    }

    // tg: code/game/machinery/constructable_frame.dm:21
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (circuit_value != null)
            Dm.append(ref dot, I18n.T("It has \\a [] installed.", circuit_value));
        return dot;
    }

    // tg: code/game/machinery/constructable_frame.dm:26
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        if (isprojectile(mover))
            return TRUE;
        return await base.CanAllowThrough(mover, border_dir);
    }

    // tg: code/game/machinery/constructable_frame.dm:31
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        atom drop_loc = drop_location() as atom;
        Dm.@new<obj_item_stack_sheet_iron>(drop_loc, 5);
        if (circuit_value != null)
            await circuit_value.forceMove(drop_loc);
    }

    /// Called when circuit has been set to a new board
    // tg: code/game/machinery/constructable_frame.dm:37
    public virtual void circuit_added(obj_item_circuitboard added = null)
    {
        return;
    }

    /// Called when circuit has been removed from the frame
    // tg: code/game/machinery/constructable_frame.dm:41
    public virtual void circuit_removed(obj_item_circuitboard removed = null)
    {
        return;
    }

    // tg: code/game/machinery/constructable_frame.dm:44
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone != circuit_value)
            return dot;
        circuit_value = null;

        if (QDELING(this))
            return dot;

        circuit_removed(gone as obj_item_circuitboard);
        return dot;
    }

    // tg: code/game/machinery/constructable_frame.dm:55
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref circuit_value);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/constructable_frame.dm:59
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, state);
        return dot;
    }

    /// Checks if the frame can be disassembled, and if so, begins the process
    // tg: code/game/machinery/constructable_frame.dm:64
    public virtual async DmTask<object> try_dissassemble(mob_living user = null, obj_item tool = null, object disassemble_time = null)
    {
        disassemble_time ??= 8 * SECONDS;
        if (!Dm.eq(state, FRAME_STATE_EMPTY))
            return NONE;
        if (Dm.truthy(anchored) && Dm.eq(state, FRAME_STATE_EMPTY)) //when using a screwdriver on an incomplete frame(missing components) no point checking for this
        {
            balloon_alert(user, I18n.T("must be unanchored first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: Dm.eq(tool.tool_behaviour, TOOL_WELDER) ? 1 : 0)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("disassembling..."));
        user.visible_message(
            span_warning(I18n.T("[] begins disassembling [].", user, this)),
            span_notice(I18n.T("You start to disassemble []...", this)),
            span_hear(I18n.T("You hear banging and clanking.")));
        if (!Dm.truthy(await tool.use_tool(this, user, disassemble_time, amount: Dm.eq(tool.tool_behaviour, TOOL_WELDER) ? 1 : 0, volume: 50)) || !Dm.eq(state, FRAME_STATE_EMPTY))
            return ITEM_INTERACT_BLOCKING;

        turf decon_turf = get_turf(this) as turf;
        await deconstruct(TRUE);
        foreach (obj_item_stack leftover in Dm.each<obj_item_stack>(decon_turf))
            // tg: code/game/machinery/constructable_frame.dm:85
            leftover.add_fingerprint(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/constructable_frame.dm:88
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return await try_dissassemble(user, tool, disassemble_time: 8 * SECONDS);
    }

    // tg: code/game/machinery/constructable_frame.dm:91
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        return await try_dissassemble(user, tool, disassemble_time: 2 * SECONDS);
    }

    /**
     * Attempt to finalize the construction of the frame into a machine
     *
     * If successful, results in qdel'ing the frame and newing of a machine
     *
     * Arguments
     * * user - the player
     * * tool - the tool used to finalize the construction
     */
    // tg: code/game/machinery/constructable_frame.dm:103
    public virtual async DmTask<object> finalize_construction(mob_living user = null, obj_item tool = null)
    {
        stack_trace(Dm.text("[] finalize_construction unimplemented.", GetType()), "code/game/machinery/constructable_frame.dm", 104);
        return FALSE;
    }

    // tg: code/game/machinery/constructable_frame.dm:107
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = NONE;
        object sw__1 = await default_unfasten_wrench(user, tool, 4 * SECONDS);
        if (Dm.eq(sw__1, SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;
        else if (Dm.eq(sw__1, FAILED_UNFASTEN))
            return ITEM_INTERACT_BLOCKING;
        return dot;
    }

    // tg: code/game/machinery/constructable_frame.dm:116
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = NONE;
        if (tool is obj_item_circuitboard) // Install board will fail if passed an invalid circuitboard and give feedback
            return Dm.truthy(await install_board(user, (obj_item_circuitboard)tool, by_hand: TRUE)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        return dot;
    }

    // tg: code/game/machinery/constructable_frame.dm:121
    public override async DmTask<object> ranged_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object dot = NONE;

        if (tool is not obj_item_storage_part_replacer_bluespace)
            return dot;

        dot = await item_interaction(user, tool, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            user.Beam(this, icon_state: "rped_upgrade", time: 0.5f * SECONDS);
        return dot;
    }

    /**
     * Installs the passed circuit board into the frame
     *
     * Assumes there is no circuit already installed
     *
     * Arguments
     * * board - the machine circuitboard to install
     * * user - the player
     * * by_hand - is the player installing the board by hand or from the RPED.
     * Used to decide how to transfer the board into the frame
     */
    // tg: code/game/machinery/constructable_frame.dm:142
    public virtual async DmTask<object> install_board(mob_living user = null, obj_item_circuitboard board = null, float? by_hand = null)
    {
        by_hand ??= FALSE;
        if (!istype(board, board_type) || board.build_path == null)
        {
            balloon_alert(user, I18n.T("invalid board!"));
            return FALSE;
        }
        if (Dm.truthy(by_hand) && !Dm.truthy(await user.transferItemToLoc(board, this)))
            return FALSE;
        else if (!Dm.truthy(await board.forceMove(this)))
            return FALSE;

        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/machinery/constructable_frame.dm:151
        balloon_alert(user, I18n.T("circuit installed"));
        circuit_value = board;
        if (Dm.truthy(by_hand))
            // tg: code/game/machinery/constructable_frame.dm:155
            circuit_value.add_fingerprint(user);
        circuit_added(board);
        return TRUE;
    }

    /**
     * Attempt to install a circuit from the contents of an RPED
     *
     * Arguments
     * * user - the player
     * * replacer - the RPED being used
     * * no_sound - if true, no sound will be played
     */
    // tg: code/game/machinery/constructable_frame.dm:167
    public virtual async DmTask<object> install_circuit_from_part_replacer(mob_living user = null, obj_item_storage_part_replacer replacer = null, float? no_sound = null)
    {
        no_sound ??= FALSE;
        if (!Dm.truthy(length(replacer.contents)))
            return FALSE;

        DmList circuit_boards = Dm.list();
        foreach (object o in Dm.each(replacer.contents))
        {
            obj_item_circuitboard board = o as obj_item_circuitboard;
            if (istype(board, board_type))
                circuit_boards[board.name] = board;
        }

        if (!Dm.truthy(length(circuit_boards)))
            return FALSE;

        //if there is only one board directly install it else pick from list
        obj_item_circuitboard target_board = null;
        if (Dm.num(length(circuit_boards)) == 1)
        {
            foreach (object board_name in Dm.each(circuit_boards))
                target_board = circuit_boards[board_name] as obj_item_circuitboard;
        }
        else
        {
            object option = await tgui_input_list(user, I18n.T("Select Circuitboard To Install"), I18n.T(" Available Boards"), circuit_boards);
            target_board = option == null ? null : circuit_boards[option] as obj_item_circuitboard;
            // Everything still where it should be after the UI closed?
            if (QDELETED(target_board) || QDELETED(this) || QDELETED(user) || !Dm.@in(target_board, replacer.contents) || !Dm.truthy(user.is_holding(replacer)))
                return FALSE;
            // User still within range?
            bool close_enough = istype(replacer, typeof(obj_item_storage_part_replacer_bluespace)) || Dm.truthy(await user.Adjacent(this));
            if (!close_enough)
                return FALSE;
        }

        if (Dm.truthy(await install_board(user, target_board, by_hand: FALSE)))
        {
            // After installing, attempts to follow up by inserting parts
            await install_parts_from_part_replacer(user, replacer, no_sound: TRUE);
            if (!Dm.truthy(no_sound))
                replacer.play_rped_effect();
            return TRUE;
        }

        return FALSE;
    }

    /**
     * Attempt to install necessary parts from the contents of an RPED
     *
     * Arguments
     * * user - the player
     * * replacer - the RPED being used
     * * no_sound - if true, no sound will be played
     */
    // tg: code/game/machinery/constructable_frame.dm:213
    public virtual async DmTask<object> install_parts_from_part_replacer(mob_living user = null, obj_item_storage_part_replacer replacer = null, float? no_sound = null)
    {
        return FALSE;
    }
}
