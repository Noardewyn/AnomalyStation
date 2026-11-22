// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/apc/apc_tool_act.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 3 (the APC): the item and tool acts, the cell in, the ID swipe lock, the board, the terminal and the frame, the
// crowbar, screwdriver, wirecutter and welder steps, the emag and the EMP; the M7 shocks slice: fork_outlet_act and the shock
// of a cable node when the terminal is wired. Left out: the wire hacking window (wires.interact, D162: no wire hacking in M7,
// a not-translated leaf). E13: rcd_vals and rcd_act (the RCD's simple circuits upgrade).

// tg: code/modules/power/apc/apc_tool_act.dm:3
public partial class obj_machinery_power_apc : obj_machinery_power
{
    //attack with an item - open/close cover, insert cell, or (un)lock interface
    // tg: code/modules/power/apc/apc_tool_act.dm:3
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = null;
        dot = NONE;
        if (HAS_TRAIT(tool, TRAIT_APC_SHOCKING))
        {
            dot = await fork_outlet_act(user, tool);
            if (Dm.truthy(dot))
                return dot;
        }

        if (Dm.truthy(tool.GetID()))
        {
            togglelock(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_stock_parts_power_store)
            dot = await cell_act(user, tool as obj_item_stock_parts_power_store);
        else if (tool is obj_item_stack_cable_coil)
            dot = await cable_act(user, tool as obj_item_stack_cable_coil, LAZYACCESS(modifiers, RIGHT_CLICK));
        else if (tool is obj_item_electronics_apc)
            dot = await electronics_act(user, tool as obj_item_electronics_apc);
        else if (tool is obj_item_electroadaptive_pseudocircuit)
            dot = await pseudocircuit_act(user, tool as obj_item_electroadaptive_pseudocircuit);
        else if (tool is obj_item_wallframe_apc)
            dot = await wallframe_act(user, tool as obj_item_wallframe_apc);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.truthy(panel_open) && !Dm.truthy(opened) && Dm.truthy(is_wire_tool(tool)))
        {
            // wires.interact(user) (:28): the wire hacking window, which D162 leaves out of M7.
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        return dot;
    }

    /// Called when we interact with the APC with an item with which we can get shocked when we stuff it into an APC
    // tg: code/modules/power/apc/apc_tool_act.dm:34  (the M7 shocks slice)
    public virtual async DmTask<object> fork_outlet_act(mob_living user = null, obj_item tool = null)
    {
        object metal = 0;
        object shock_source = null;
        Dm.append(ref metal, LAZYACCESS(tool.custom_materials, await SSmaterials.get_material(typeof(datum_material_iron))));//This prevents wooden rolling pins from shocking the user

        if (Dm.truthy(cell) || Dm.truthy(terminal)) //The mob gets shocked by whichever powersource has the most electricity
        {
            if (Dm.truthy(cell) && Dm.truthy(terminal))
                shock_source = (cell.charge > ((datum_powernet)terminal.powernet).avail ? (object)cell : terminal.powernet);
            else
                shock_source = (terminal?.powernet is var or__1 && Dm.truthy(or__1) ? or__1 : cell);
        }

        if (Dm.truthy(shock_source) && Dm.truthy(metal) && (Dm.truthy(panel_open) || Dm.truthy(opened))) //Now you're cooking with electricity
        {
            if (!Dm.truthy(await electrocute_mob(user as mob_living_carbon, shock_source, this, siemens_coeff: 1, dist_check: TRUE)))//People with insulated gloves just attack the APC normally. They're just short of magical anyway
                return NONE;
            do_sparks(5, TRUE, this);
            user.visible_message(span_notice(I18n.T("[] shoves [] into the internal components of [], erupting into a cascade of sparks!", user.name, tool, this)));
            if (Dm.eq(shock_source, cell))//If the shock is coming from the cell just fully discharge it, because it's funny
                await cell.use(cell.charge);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    /// Called when we interact with the APC with a cell, attempts to insert it
    // tg: code/modules/power/apc/apc_tool_act.dm:55
    public virtual async DmTask<object> cell_act(mob_living user = null, obj_item_stock_parts_power_store new_cell = null)
    {
        if (!Dm.truthy(opened))
            return NONE;

        if (cell != null)
        {
            balloon_alert(user, I18n.T("cell already installed!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)))
        {
            balloon_alert(user, I18n.T("no connector for a cell!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(new_cell, this)))
            return ITEM_INTERACT_BLOCKING;
        cell = new_cell;
        user.visible_message(span_notice(I18n.T("[] inserts the power cell to []!", user.name, this.name)));
        balloon_alert(user, I18n.T("cell inserted"));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    /// Checks if we can place a terminal on the APC
    // tg: code/modules/power/apc/apc_tool_act.dm:74
    public virtual object can_place_terminal(mob_living user = null, obj_item_stack_cable_coil installing_cable = null, float? silent = null)
    {
        silent ??= TRUE;
        if (!Dm.truthy(opened))
            return FALSE;
        turf host_turf = get_turf(this);
        if (host_turf.underfloor_accessibility < UNDERFLOOR_INTERACTABLE)
        {
            if (!Dm.truthy(silent) && Dm.truthy(user))
                balloon_alert(user, I18n.T("remove the floor plating!"));
            return FALSE;
        }
        if (terminal != null)
        {
            if (!Dm.truthy(silent) && Dm.truthy(user))
                balloon_alert(user, I18n.T("already wired!"));
            return FALSE;
        }
        if (!Dm.truthy(has_electronics))
        {
            if (!Dm.truthy(silent) && Dm.truthy(user))
                balloon_alert(user, I18n.T("no board to wire!"));
            return FALSE;
        }
        if (Dm.truthy(panel_open))
        {
            if (!Dm.truthy(silent) && Dm.truthy(user))
                balloon_alert(user, I18n.T("wires prevent placing a terminal!"));
            return FALSE;
        }
        if (Dm.lt(installing_cable.get_amount(), 10))
        {
            if (!Dm.truthy(silent) && Dm.truthy(user))
                balloon_alert(user, I18n.T("need ten lengths of cable!"));
            return FALSE;
        }
        return TRUE;
    }

    /// Called when we interact with the APC with a cable, attempts to wire the APC and create a terminal
    // tg: code/modules/power/apc/apc_tool_act.dm:101
    public virtual async DmTask<object> cable_act(mob_living user = null, obj_item_stack_cable_coil installing_cable = null, object is_right_clicking = null)
    {
        if (!Dm.truthy(opened))
            return NONE;
        if (!Dm.truthy(can_place_terminal(user, installing_cable, silent: FALSE)))
            return ITEM_INTERACT_BLOCKING;

        object terminal_cable_layer = cable_layer; // Default to machine's cable layer
        if (Dm.truthy(is_right_clicking))
        {
            object choice = await tgui_input_list(user, I18n.T("Select Power Input Cable Layer"), I18n.T("Select Cable Layer"), GLOB.cable_name_to_layer);
            if (choice == null
                || !Dm.truthy(user.is_holding(installing_cable))
                || !Dm.truthy(await user.Adjacent(this))
                || Dm.truthy(user.incapacitated)
                || !Dm.truthy(can_place_terminal(user, installing_cable, silent: TRUE))
            )
                return ITEM_INTERACT_BLOCKING;
            terminal_cable_layer = GLOB.cable_name_to_layer[choice];
        }

        user.visible_message(span_notice(I18n.T("[] starts addding cables to the APC frame.", user.name)));
        balloon_alert(user, I18n.T("adding cables..."));
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);

        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(can_place_terminal(user, installing_cable, silent: TRUE)))
            return ITEM_INTERACT_BLOCKING;
        turf our_turf = get_turf(this);
        obj_structure_cable cable_node = our_turf.get_cable_node(Dm.num_or_null(terminal_cable_layer)) as obj_structure_cable;
        if (prob(50) && Dm.truthy(await electrocute_mob(usr as mob_living_carbon, cable_node, cable_node, 1, TRUE)))
        {
            do_sparks(5, TRUE, this);
            return ITEM_INTERACT_BLOCKING;
        }
        await installing_cable.use(10);
        user.visible_message(span_notice(I18n.T("[] adds cables to the APC frame.", user.name)));
        balloon_alert(user, I18n.T("cables added"));
        make_terminal(terminal_cable_layer);
        terminal.connect_to_network();
        return ITEM_INTERACT_SUCCESS;
    }

    /// Called when we interact with the APC with APC electronics, attempts to install the board
    // tg: code/modules/power/apc/apc_tool_act.dm:140
    public virtual async DmTask<object> electronics_act(mob_living user = null, obj_item_electronics_apc installing_board = null)
    {
        if (!Dm.truthy(opened))
            return NONE;

        if (Dm.truthy(has_electronics))
        {
            balloon_alert(user, I18n.T("there is already a board!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            balloon_alert(user, I18n.T("the frame is damaged!"));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] inserts the power control board into [].", user.name, this)));
        balloon_alert(user, I18n.T("inserting the board..."));
        playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);

        if (!Dm.truthy(await do_after(user, 1 * SECONDS, target: this)) || Dm.truthy(has_electronics))
            return ITEM_INTERACT_BLOCKING;

        has_electronics = APC_ELECTRONICS_INSTALLED;
        locked = FALSE;
        balloon_alert(user, I18n.T("board installed"));
        qdel(installing_board);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Called when we interact with the APC with and APC frame, used for replacing a damaged cover/frame
    // tg: code/modules/power/apc/apc_tool_act.dm:201
    public virtual async DmTask<object> wallframe_act(mob_living user = null, obj_item_wallframe_apc wallframe = null)
    {
        if (!Dm.truthy(opened))
            return NONE;

        if (!(Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || opened == APC_COVER_REMOVED || Dm.lt(atom_integrity, max_integrity))) // There is nothing to repair
        {
            balloon_alert(user, I18n.T("no reason for repairs!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) && opened == APC_COVER_REMOVED && Dm.truthy(has_electronics) && terminal != null) // Cover is the only thing broken, we do not need to remove elctronicks to replace cover
        {
            user.visible_message(span_notice(I18n.T("[] replaces missing APC's cover.", user.name)));
            balloon_alert(user, I18n.T("replacing APC's cover..."));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this))) // replacing cover is quicker than replacing whole frame
                return ITEM_INTERACT_BLOCKING;
            balloon_alert(user, I18n.T("cover replaced"));
            qdel(wallframe);
            update_integrity(30); //needs to be welded to fully repair but can work without
            await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(Dm.bitor(BROKEN, MAINT))));
            opened = APC_COVER_OPENED;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(has_electronics))
        {
            balloon_alert(user, I18n.T("remove the board inside!"));
            return ITEM_INTERACT_BLOCKING;
        }
        user.visible_message(span_notice(I18n.T("[] replaces the damaged APC frame with a new one.", user.name)));
        balloon_alert(user, I18n.T("replacing damaged frame..."));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, I18n.T("replaced frame"));
        qdel(wallframe);
        await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
        atom_integrity = max_integrity;
        if (opened == APC_COVER_REMOVED)
            opened = APC_COVER_OPENED;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/apc/apc_tool_act.dm:236
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item crowbar = null)
    {
        object dot = null;
        dot = TRUE;

        //Prying off broken cover
        if ((opened == APC_COVER_CLOSED || opened == APC_COVER_OPENED) && Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            crowbar.play_tool_sound(this);
            balloon_alert(user, I18n.T("prying..."));
            if (!Dm.truthy(await crowbar.use_tool(this, user, 5 * SECONDS)))
                return dot;
            opened = APC_COVER_REMOVED;
            balloon_alert(user, I18n.T("cover removed"));
            update_appearance();
            return dot;
        }

        //Opening and closing cover
        if ((!Dm.truthy(opened) && opened != APC_COVER_REMOVED) && !Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            if (Dm.truthy(coverlocked) && !Dm.truthy(Dm.bitand(machine_stat, MAINT))) // locked...
            {
                balloon_alert(user, I18n.T("cover is locked!"));
                return dot;
            }
            else if (Dm.truthy(panel_open))
            {
                balloon_alert(user, I18n.T("wires prevents opening it!"));
                return dot;
            }
            else
            {
                opened = APC_COVER_OPENED;
                update_appearance();
                return dot;
            }
        }

        if ((Dm.truthy(opened) && has_electronics == APC_ELECTRONICS_SECURED) && !Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            opened = APC_COVER_CLOSED;
            coverlocked = TRUE; //closing cover relocks it
            balloon_alert(user, I18n.T("locking the cover"));
            update_appearance();
            return dot;
        }

        //Taking out the electronics
        if (!Dm.truthy(opened) || has_electronics != APC_ELECTRONICS_INSTALLED)
            return dot;
        if (terminal != null)
        {
            balloon_alert(user, I18n.T("disconnect wires first!"));
            return dot;
        }
        crowbar.play_tool_sound(this);
        if (!Dm.truthy(await crowbar.use_tool(this, user, 50)))
            return dot;
        if (has_electronics != APC_ELECTRONICS_INSTALLED)
            return dot;
        has_electronics = APC_ELECTRONICS_MISSING;
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            user.visible_message(span_notice(I18n.T("[] breaks the power control board inside []!", user.name, name)),
                span_hear(I18n.T("You hear a crack.")));
            balloon_alert(user, I18n.T("charred board breaks"));
            return dot;
        }
        else if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            Dm.mask(ref obj_flags, Dm.bitnot(EMAGGED));
            user.visible_message(span_notice(I18n.T("[] discards an emagged power control board from []!", user.name, name)));
            balloon_alert(user, I18n.T("emagged board discarded"));
            return dot;
        }
        else if (Dm.truthy(malfhack))
        {
            user.visible_message(span_notice(I18n.T("[] discards a strangely programmed power control board from []!", user.name, name)));
            balloon_alert(user, I18n.T("reprogrammed board discarded"));
            malfai = null;
            malfhack = 0;
            return dot;
        }
        user.visible_message(span_notice(I18n.T("[] removes the power control board from []!", user.name, name)));
        balloon_alert(user, I18n.T("removed the board"));
        Dm.@new<obj_item_electronics_apc>(loc);
        return dot;
    }

    // tg: code/modules/power/apc/apc_tool_act.dm:303
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item W = null)
    {
        if (Dm.truthy(await base.screwdriver_act(user, W)))
            return TRUE;
        object dot = null;
        dot = TRUE;

        if (!Dm.truthy(opened))
        {
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            {
                balloon_alert(user, I18n.T("interface is broken!"));
                return dot;
            }
            toggle_panel_open();
            balloon_alert(user, I18n.T("wires []", Dm.truthy(panel_open) ? I18n.T("exposed") : I18n.T("unexposed")));
            W.play_tool_sound(this);
            update_appearance();
            return dot;
        }

        if (cell != null)
        {
            user.visible_message(span_notice(I18n.T("[] removes \\the [] from []!", user, cell, this)));
            balloon_alert(user, I18n.T("cell removed"));
            turf user_turf = get_turf(user);
            await cell.forceMove(user_turf);
            cell = null;
            charging = APC_NOT_CHARGING;
            update_appearance();
            return dot;
        }

        float sw__1 = has_electronics;
        if (sw__1 == APC_ELECTRONICS_INSTALLED)
        {
            has_electronics = APC_ELECTRONICS_SECURED;
            await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(MAINT)));
            W.play_tool_sound(this);
            balloon_alert(user, I18n.T("board fastened"));
        }
        else if (sw__1 == APC_ELECTRONICS_SECURED)
        {
            has_electronics = APC_ELECTRONICS_INSTALLED;
            await set_machine_stat(Dm.bitor(machine_stat, MAINT));
            W.play_tool_sound(this);
            balloon_alert(user, I18n.T("board unfastened"));
        }
        else
        {
            balloon_alert(user, I18n.T("no board to fasten!"));
            return dot;
        }
        update_appearance();
        return dot;
    }

    // tg: code/modules/power/apc/apc_tool_act.dm:344
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item W = null)
    {
        object dot = null;
        dot = await base.wirecutter_act(user, W);
        if (terminal != null && Dm.truthy(opened))
        {
            await terminal.dismantle(user, W);
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/power/apc/apc_tool_act.dm:350
    public override async DmTask<object> welder_act(mob_living user = null, obj_item welder = null)
    {
        object dot = null;
        dot = await base.welder_act(user, welder);

        //repairing the cover
        if (Dm.lt(atom_integrity, max_integrity) && Dm.truthy(has_electronics))
        {
            if (opened == APC_COVER_REMOVED)
            {
                balloon_alert(user, I18n.T("no cover to repair!"));
                return dot;
            }
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            {
                balloon_alert(user, I18n.T("too damaged to repair!"));
                return dot;
            }
            if (!Dm.truthy(welder.tool_start_check(user, amount: 1)))
                return dot;
            balloon_alert(user, I18n.T("repairing..."));
            if (Dm.truthy(await welder.use_tool(this, user, 4 * SECONDS, volume: 50)))
            {
                // min(atom_integrity += 50, max_integrity): the compound assignment first, then its value (rules 4.3)
                Dm.append(ref atom_integrity, 50);
                update_integrity(min(atom_integrity, max_integrity));
                balloon_alert(user, I18n.T("repaired"));
            }
            return ITEM_INTERACT_SUCCESS;
        }

        //disassembling the frame
        if (!Dm.truthy(opened) || Dm.truthy(has_electronics) || terminal != null)
            return dot;
        if (!Dm.truthy(welder.tool_start_check(user, amount: 1)))
            return dot;
        user.visible_message(span_notice(I18n.T("[] welds [].", user.name, this)),
            span_hear(I18n.T("You hear welding.")));
        balloon_alert(user, I18n.T("welding the APC frame"));
        if (!Dm.truthy(await welder.use_tool(this, user, 50, volume: 50)))
            return dot;
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || opened == APC_COVER_REMOVED)
        {
            Dm.@new<obj_item_stack_sheet_iron>(loc);
            user.visible_message(span_notice(I18n.T("[] cuts [] apart with [].", user.name, this, welder)));
            user.balloon_alert(user, I18n.T("disassembled the broken frame"));
        }
        else
        {
            Dm.@new<obj_item_wallframe_apc>(loc);
            user.visible_message(span_notice(I18n.T("[] cuts [] from the wall with [].", user.name, this, welder)));
            user.balloon_alert(user, I18n.T("cut the frame from the wall"));
        }
        qdel(this);
        return TRUE;
    }

    // tg: code/modules/power/apc/apc_tool_act.dm:436
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || Dm.truthy(malfhack))
            return FALSE;

        if (Dm.truthy(opened))
        {
            balloon_alert(user, I18n.T("close the cover first!"));
            return FALSE;
        }
        else if (Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("close the panel first!"));
            return FALSE;
        }
        else if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, MAINT))))
        {
            balloon_alert(user, I18n.T("nothing happens!"));
            return FALSE;
        }
        else
        {
            flick("apc-spark", this);
            playsound(this, SFX_SPARKS, 75, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            Dm.combine(ref obj_flags, EMAGGED);
            locked = FALSE;
            balloon_alert(user, I18n.T("interface damaged"));
            update_appearance();
            flicker_hacked_icon();
            return TRUE;
        }
    }

    // damage and destruction acts
    // tg: code/modules/power/apc/apc_tool_act.dm:460
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.emp_act(severity, __extra);
        if (!Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
        {
            if (cell != null)
                await cell.emp_act(severity);
            if (occupier != null)
                await occupier.emp_act(severity);
        }
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        lighting = APC_CHANNEL_OFF;
        equipment = APC_CHANNEL_OFF;
        environ = APC_CHANNEL_OFF;
        update_appearance();
        update();
        _addtimer(Dm.@new<datum_callback>(this, nameof(reset), APC_RESET_EMP), 60 * SECONDS, file: "code/modules/power/apc/apc_tool_act.dm", line: 474);
        return dot;
    }

    // tg: code/modules/power/apc/apc_tool_act.dm:476
    public virtual void togglelock(mob_living user = null)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            balloon_alert(user, I18n.T("interface is broken!"));
        else if (Dm.truthy(opened))
            balloon_alert(user, I18n.T("close the cover first!"));
        else if (Dm.truthy(panel_open))
            balloon_alert(user, I18n.T("close the panel first!"));
        else if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, MAINT))))
            balloon_alert(user, I18n.T("nothing happens!"));
        else
        {
            if (Dm.truthy(Waiting.NoSleep(allowed__proc(usr))) && !Dm.truthy(wires.is_cut(WIRE_IDSCAN)) && ((!Dm.truthy(malfhack) && remote_control_user == null) || (Dm.truthy(malfhack) && (malfai == user || Dm.@in(user, malfai.connected_robots)))))
            {
                locked = Dm.not(locked);
                balloon_alert(user, Dm.truthy(locked) ? I18n.T("locked") : I18n.T("unlocked"));
                update_appearance();
            }
            else
                balloon_alert(user, I18n.T("access denied!"));
        }
    }

    // tg: code/modules/power/apc/apc_tool_act.dm:166
    public virtual async DmTask<object> pseudocircuit_act(mob_living user = null, obj_item_electroadaptive_pseudocircuit pseudocircuit = null)
    {
        if (!Dm.truthy(has_electronics))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            {
                balloon_alert(user, I18n.T("frame is too damaged!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await pseudocircuit.adapt_circuit(user as mob_living_silicon_robot, circuit_cost: 0.05f * STANDARD_CELL_CHARGE)))
                return ITEM_INTERACT_BLOCKING;
            user.visible_message(
                span_notice(I18n.T("[] fabricates a circuit and places it into [].", user, this)),
                span_notice(I18n.T("You adapt a power control board and click it into place in []'s guts.", this)));
            has_electronics = APC_ELECTRONICS_INSTALLED;
            locked = FALSE;
            return ITEM_INTERACT_SUCCESS;
        }

        if (cell == null)
        {
            if (Dm.truthy(Dm.bitand(machine_stat, MAINT)))
            {
                balloon_alert(user, I18n.T("no board for a cell!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await pseudocircuit.adapt_circuit(user as mob_living_silicon_robot, circuit_cost: 0.5f * STANDARD_CELL_CHARGE)))
                return ITEM_INTERACT_BLOCKING;
            obj_item_stock_parts_power_store_battery_crap_empty bad_cell = Dm.@new<obj_item_stock_parts_power_store_battery_crap_empty>(this);
            await bad_cell.forceMove(this);
            cell = bad_cell;
            user.visible_message(
                span_notice(I18n.T("[] fabricates a weak power cell and places it into [].", user, this)),
                span_warning(I18n.T("Your [] whirrs with strain as you create a weak power cell and place it into []!", pseudocircuit.name, this)));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        balloon_alert(user, I18n.T("has both board and cell!"));
        return ITEM_INTERACT_BLOCKING;
    }
}

// tg: code/modules/power/apc/apc_tool_act.dm:390 (E13: the RCD's simple circuits upgrade)
public partial class obj_machinery_power_apc : obj_machinery_power
{
    // tg: code/modules/power/apc/apc_tool_act.dm:390
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (!Dm.truthy(Dm.bitand(the_rcd.construction_upgrades, RCD_UPGRADE_SIMPLE_CIRCUITS)))
            return FALSE;

        if (!Dm.truthy(has_electronics))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            {
                balloon_alert(user, I18n.T("frame is too damaged!"));
                return FALSE;
            }
            return Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 1));
        }

        if (!Dm.truthy(cell))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, MAINT)))
            {
                balloon_alert(user, I18n.T("no board for a cell!"));
                return FALSE;
            }
            return Dm.list(Dm.kv("delay", 5 * SECONDS), Dm.kv("cost", 10));
        }

        balloon_alert(user, I18n.T("has both board and cell!"));
        return FALSE;
    }

    // tg: code/modules/power/apc/apc_tool_act.dm:409
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (!Dm.truthy(Dm.bitand(the_rcd.construction_upgrades, RCD_UPGRADE_SIMPLE_CIRCUITS)) || !Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_WALLFRAME))
            return FALSE;

        if (!Dm.truthy(has_electronics))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            {
                balloon_alert(user, I18n.T("frame is too damaged!"));
                return null;
            }
            balloon_alert(user, I18n.T("control board placed"));
            has_electronics = TRUE;
            locked = TRUE;
            return TRUE;
        }

        if (!Dm.truthy(cell))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, MAINT)))
            {
                balloon_alert(user, I18n.T("no board for a cell!"));
                return FALSE;
            }
            obj_item_stock_parts_power_store_battery_crap_empty C = Dm.@new<obj_item_stock_parts_power_store_battery_crap_empty>(this);
            await C.forceMove(this);
            cell = C;
            balloon_alert(user, I18n.T("power cell installed"));
            update_appearance();
            return TRUE;
        }

        balloon_alert(user, I18n.T("has both board and cell!"));
        return FALSE;
    }
}
