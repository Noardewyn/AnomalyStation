// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/camera/camera_construction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the camera's construction, repair, upgrades and notes held up to it, whole.

// tg: code/game/machinery/camera/camera_construction.dm:1
public partial class obj_machinery_camera : obj_machinery
{
    // tg: code/game/machinery/camera/camera_construction.dm:1
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (camera_construction_state == CAMERA_STATE_WRENCHED || camera_construction_state == CAMERA_STATE_WELDED)
        {
            if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
                return ITEM_INTERACT_BLOCKING;
            user.balloon_alert_to_viewers(Dm.text("[]welding...", camera_construction_state == CAMERA_STATE_WELDED ? "un" : null));
            audible_message(span_hear(I18n.T("You hear welding.")));
            if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)))
            {
                user.balloon_alert_to_viewers(Dm.text("stopped []welding!", camera_construction_state == CAMERA_STATE_WELDED ? "un" : null));
                return null;
            }
            camera_construction_state = camera_construction_state == CAMERA_STATE_WELDED ? CAMERA_STATE_WRENCHED : CAMERA_STATE_WELDED;
            await set_anchored(camera_construction_state == CAMERA_STATE_WELDED ? TRUE : FALSE);
            user.balloon_alert_to_viewers(camera_construction_state == CAMERA_STATE_WELDED ? "welded" : "unwelded");
            return ITEM_INTERACT_SUCCESS;
        }
        if (camera_construction_state == CAMERA_STATE_FINISHED)
        {
            if (!Dm.truthy(panel_open))
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(tool.tool_start_check(user, amount: 2)))
                return ITEM_INTERACT_BLOCKING;
            audible_message(span_hear(I18n.T("You hear welding.")));
            if (!Dm.truthy(await tool.use_tool(this, user, 100, volume: 50)))
                return ITEM_INTERACT_BLOCKING;
            user.visible_message(span_warning(I18n.T("[] unwelds [], leaving it as just a frame bolted to the wall.", user, this)),
                span_warning(I18n.T("You unweld [], leaving it as just a frame bolted to the wall", this)));
            await deconstruct(TRUE);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.welder_act(user, tool);
    }

    // tg: code/game/machinery/camera/camera_construction.dm:28
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (camera_construction_state == CAMERA_STATE_WIRED)
        {
            tool.play_tool_sound(this);
            object input = await tgui_input_text(user, "Which networks would you like to connect this camera to? Separate networks with a comma. No Spaces!\nFor example: SS13,Security,Secret", "Set Network", "SS13", max_length: MAX_NAME_LEN);
            if (input == null)
                return ITEM_INTERACT_BLOCKING;
            DmList tempnetwork = splittext(input, ",") as DmList;
            if (!Dm.truthy(length(tempnetwork)))
            {
                to_chat(user, span_warning(I18n.T("No network found, please hang up and try your call again!")));
                return ITEM_INTERACT_BLOCKING;
            }
            foreach (object i in Dm.each(tempnetwork.Copy()))
            {
                tempnetwork.Remove(i);
                tempnetwork.Add(LOWER_TEXT(i));
            }
            camera_construction_state = CAMERA_STATE_FINISHED;
            toggle_cam(user, displaymessage: FALSE);
            network = tempnetwork;
            return ITEM_INTERACT_SUCCESS;
        }
        if (camera_construction_state == CAMERA_STATE_FINISHED)
        {
            toggle_panel_open();
            to_chat(user, span_notice(I18n.T("You screw the camera's panel [].", Dm.truthy(panel_open) ? "open" : "closed")));
            tool.play_tool_sound(this);
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.screwdriver_act(user, tool);
    }

    // tg: code/game/machinery/camera/camera_construction.dm:55
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (camera_construction_state == CAMERA_STATE_WIRED)
        {
            Dm.@new<obj_item_stack_cable_coil>(drop_location(), 2);
            tool.play_tool_sound(this);
            to_chat(user, span_notice(I18n.T("You cut the wires from the circuits.")));
            camera_construction_state = CAMERA_STATE_WELDED;
            return ITEM_INTERACT_SUCCESS;
        }
        if (camera_construction_state == CAMERA_STATE_FINISHED)
        {
            if (!Dm.truthy(panel_open))
                return ITEM_INTERACT_BLOCKING;
            toggle_cam(user, 1);
            atom_integrity = max_integrity; //this is a pretty simplistic way to heal the camera, but there's no reason for this to be complex.
            await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
            tool.play_tool_sound(this);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.wirecutter_act(user, tool);
    }

    // tg: code/game/machinery/camera/camera_construction.dm:73
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (camera_construction_state != CAMERA_STATE_WRENCHED)
            return NONE;
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You detach [] from its place.", this)));
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/camera/camera_construction.dm:81
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (camera_construction_state != CAMERA_STATE_FINISHED || !Dm.truthy(panel_open))
            return NONE;
        DmList droppable_parts = Dm.list();
        if (xray_module != null)
            Dm.append(ref droppable_parts, xray_module);
        if (emp_module != null)
            Dm.append(ref droppable_parts, emp_module);
        if (proximity_monitor != null)
            Dm.append(ref droppable_parts, proximity_monitor);
        if (!Dm.truthy(length(droppable_parts)))
            return ITEM_INTERACT_BLOCKING;
        object choice = await tgui_input_list(user, "Select a part to remove", "Part Removal", sort_names(droppable_parts) as DmList);
        if (choice == null)
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You remove [] from [].", choice, this)));
        if (choice == xray_module)
        {
            drop_upgrade(xray_module);
            removeXRay();
        }
        if (choice == emp_module)
        {
            drop_upgrade(emp_module);
            removeEmpProof();
        }
        if (choice == proximity_monitor)
        {
            drop_upgrade(proximity_monitor);
            removeMotion();
        }
        tool.play_tool_sound(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/camera/camera_construction.dm:113
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (camera_construction_state != CAMERA_STATE_FINISHED || !Dm.truthy(panel_open))
            return NONE;
        float initial_view_range = Dm.initial(GetType(), (obj_machinery_camera c) => c.view_range);
        setViewRange(view_range == initial_view_range ? short_range : initial_view_range);
        to_chat(user, span_notice(I18n.T("You [] the camera's focus.", view_range == initial_view_range ? "restore" : "mess up")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/camera/camera_construction.dm:120
    public virtual async DmTask<object> gas_analyzer_act(mob_living user = null, obj_item tool = null)
    {
        if (camera_construction_state == CAMERA_STATE_FINISHED && !Dm.truthy(panel_open))
            return NONE;
        if (Dm.truthy(isXRay(TRUE)))
        {
            to_chat(user, span_warning(I18n.T("[] already has that upgrade!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool, newloc: this)))
            return ITEM_INTERACT_BLOCKING;
        upgradeXRay(FALSE, TRUE);
        to_chat(user, span_notice(I18n.T("You attach [] into []'s inner circuits.", tool, this)));
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/camera/camera_construction.dm:133
    public virtual async DmTask<object> plasma_act(mob_living user = null, obj_item tool = null)
    {
        if (camera_construction_state == CAMERA_STATE_FINISHED && !Dm.truthy(panel_open))
            return NONE;
        if (Dm.truthy(isEmpProof(TRUE)))
        {
            to_chat(user, span_warning(I18n.T("[] already has that upgrade!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await tool.use_tool(this, user, 0, amount: 1)))
            return ITEM_INTERACT_BLOCKING;
        upgradeEmpProof(FALSE, TRUE);
        to_chat(user, span_notice(I18n.T("You attach [] into []'s inner circuits.", tool, this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/camera/camera_construction.dm:145
    public virtual async DmTask<object> prox_act(mob_living user = null, obj_item tool = null)
    {
        if (camera_construction_state == CAMERA_STATE_FINISHED && !Dm.truthy(panel_open))
            return NONE;
        if (Dm.truthy(isMotion()))
        {
            to_chat(user, span_warning(I18n.T("[] already has that upgrade!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool, newloc: this)))
            return ITEM_INTERACT_BLOCKING;
        upgradeMotion();
        to_chat(user, span_notice(I18n.T("You attach [] into []'s inner circuits.", tool, this)));
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/camera/camera_construction.dm:158
    public virtual async DmTask<object> cable_act(mob_living user = null, obj_item tool = null)
    {
        if (camera_construction_state != CAMERA_STATE_WELDED)
            return NONE;
        obj_item_stack_cable_coil coil = tool as obj_item_stack_cable_coil;
        if (coil == null || !Dm.truthy(await coil.use(2)))
        {
            to_chat(user, span_warning(I18n.T("You need two lengths of cable to wire []!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You add wires to [].", this)));
        camera_construction_state = CAMERA_STATE_WIRED;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/camera/camera_construction.dm:168
    public virtual async DmTask<object> computer_act(mob_living user = null, obj_item tool = null)
    {
        if (camera_construction_state != CAMERA_STATE_FINISHED)
            return NONE;
        obj_item_modular_computer computer = tool as obj_item_modular_computer;
        object note_name = sanitize(computer.name);
        datum_computer_file_program_notepad notepad_app = Dm.locate<datum_computer_file_program_notepad>(computer.stored_files);
        if (notepad_app == null)
            return ITEM_INTERACT_BLOCKING;
        object note_text = sanitize(notepad_app.written_note);
        if (!Dm.truthy(note_text))
            return ITEM_INTERACT_BLOCKING;
        display_note(user, note_name, note_text, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/camera/camera_construction.dm:182
    public virtual async DmTask<object> paper_act(mob_living user = null, obj_item tool = null)
    {
        if (camera_construction_state != CAMERA_STATE_FINISHED)
            return NONE;
        obj_item_paper paper = tool as obj_item_paper;
        last_shown_paper = paper.copy(paper.type, null) as obj_item_paper;
        object note_name = sanitize(last_shown_paper.name);
        last_shown_paper.camera_holder = WEAKREF(this) as datum_weakref;
        display_note(user, note_name, null, FALSE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/camera/camera_construction.dm:192
    public virtual void display_note(mob_living user = null, object title = null, object text = null, object is_computer = null)
    {
        to_chat(user, span_notice(I18n.T("You hold up \\the [] up to the camera...", title)));
        user.log_talk(title, LOG_GAME, "Pressed to camera", TRUE);
        user.changeNext_move(CLICK_CD_MELEE);

        // Iterate over all living mobs and check if anyone is elibile to view the paper.
        // This is backwards, but cameras don't store a list of people that are looking through them,
        // and we'll have to iterate this list anyway so we can use it to pull out AIs too.
        foreach (mob potential_viewer in Dm.each<mob>(GLOB.player_list))
        {
            // All AIs view through cameras, so we need to check them regardless.
            if (isAI(potential_viewer))
            {
                mob_living_silicon_ai ai = potential_viewer as mob_living_silicon_ai;
                if (Dm.truthy(ai.control_disabled) || Dm.eq(ai.stat, DEAD))
                    continue;

                ai.log_talk(title, LOG_VICTIM, Dm.text("Pressed to camera from []", key_name(user)), FALSE);
                if (Dm.truthy(is_computer))
                    ai.last_tablet_note_seen = Dm.text("<HTML><HEAD><TITLE>[]</TITLE></HEAD><BODY><TT>[]</TT></BODY></HTML>", title, text);
                else
                    log_paper(Dm.text("[] held [] up to [], requesting [] read it.", key_name(user), last_shown_paper, this, key_name(ai)));

                string href_string = Dm.truthy(is_computer) ? "show_tablet_note=1" : Dm.text("show_paper_note=[]", REF(last_shown_paper));
                if (Dm.eq(user.name, "Unknown"))
                    to_chat(ai, I18n.T("[] holds <a href='byond://?_src_=usr;[];'>\\a []</a> up to one of your cameras...", span_name(Dm.text("[]", user)), href_string, title));
                else
                    to_chat(ai, I18n.T("[] holds <a href='byond://?_src_=usr;[];'>\\a []</a> up to one of your cameras...", span_name(Dm.text("<a href='byond://?src=[];track=[]'>[]</a>", REF(ai), html_encode(user.name), user)), href_string, title));
            }
            // If it's not an AI, eye if the client's eye is set to the camera. I wonder if this even works anymore with tgui camera apps and stuff?
            else if (potential_viewer.client?.eye == this)
            {
                potential_viewer.log_talk(title, LOG_VICTIM, Dm.text("Pressed to camera from []", key_name(user)), FALSE);
                if (!Dm.truthy(is_computer))
                {
                    log_paper(Dm.text("[] held [] up to [], and [] may read it.", key_name(user), last_shown_paper, this, key_name(potential_viewer)));
                    to_chat(potential_viewer, I18n.T("[] holds <a href='byond://?_src_=usr;show_paper_note=[];'>\\a []</a> up to your camera...", span_name(Dm.text("[]", user)), REF(last_shown_paper), title));
                }
                else
                    ClientSeam.Browse(potential_viewer, Dm.text("<HTML><HEAD><TITLE>[]</TITLE></HEAD><BODY><TT>[]</TT></BODY></HTML>", title, text), Dm.text("window=[]", title));
            }
        }
    }

    // tg: code/game/machinery/camera/camera_construction.dm:228
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        if (tool is obj_item_stack_sheet_mineral_plasma)
            return await plasma_act(user, tool);
        if (tool is obj_item_analyzer)
            return await gas_analyzer_act(user, tool);
        if (tool is obj_item_assembly_prox_sensor) // isprox(tool)
            return await prox_act(user, tool);
        if (tool is obj_item_stack_cable_coil)
            return await cable_act(user, tool);
        if (tool is obj_item_modular_computer)
            return await computer_act(user, tool);
        if (tool is obj_item_paper)
            return await paper_act(user, tool);
        return NONE;
    }
}
