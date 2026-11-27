// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/launchpad_control.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the launchpad control console. Up to four launchpads are uploaded from a multitool's buffer; its LaunchpadConsole
// window lists them, renames one, moves its target offset within the pad's range, unlinks it (after an alert) and launches or
// pulls through it. Its USB port takes its circuit component, which picks a pad of the console by its number and then works as
// the launchpad's own. The var blocks are generated (D111).

// tg: code/game/machinery/computer/launchpad_control.dm:1
public partial class obj_machinery_computer_launchpad : obj_machinery_computer
{
    // tg: code/game/machinery/computer/launchpad_control.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        launchpads = Dm.list();
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_usb_port), typecacheof(Dm.list(typeof(obj_item_circuit_component_bluespace_launchpad_console)), only_root_path: TRUE)));
        return dot;
    }

    // tg: code/game/machinery/computer/launchpad_control.dm:56
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        await DmTask.CompletedTask;
        to_chat(user, span_warning(I18n.T("You are too primitive to use this computer!")));
        return null;
    }

    // tg: code/game/machinery/computer/launchpad_control.dm:60
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        obj_item_multitool multitool = tool as obj_item_multitool;
        object dot = NONE;
        if (!istype(multitool.buffer, typeof(obj_machinery_launchpad)))
            return dot;

        if (LAZYLEN(launchpads) < maximum_pads)
        {
            Dm.combine(ref launchpads, multitool.buffer);
            multitool.set_buffer(null);
            to_chat(user, span_notice(I18n.T("You upload the data from the [] buffer.", multitool)));
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/machinery/computer/launchpad_control.dm:71
    public virtual object pad_exists(object number = null)
    {
        obj_machinery_launchpad pad = launchpads[number] as obj_machinery_launchpad;
        if (QDELETED(pad))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/computer/launchpad_control.dm:77
    public virtual object get_pad(object number = null)
    {
        obj_machinery_launchpad pad = launchpads[number] as obj_machinery_launchpad;
        return pad;
    }

    // tg: code/game/machinery/computer/launchpad_control.dm:81
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "LaunchpadConsole", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/computer/launchpad_control.dm:88
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        DmList pad_list = Dm.list();
        for (float i = 1, i__end = LAZYLEN(launchpads); i <= i__end; i++)
        {
            if (Dm.truthy(pad_exists(i)))
            {
                obj_machinery_launchpad pad = get_pad(i) as obj_machinery_launchpad;
                DmList this_pad = Dm.list();
                this_pad["name"] = pad.display_name;
                this_pad["id"] = i;
                if (Dm.truthy(Dm.bitand(pad.machine_stat, NOPOWER)))
                    this_pad["inactive"] = TRUE;
                Dm.append(ref pad_list, Dm.list(this_pad));
            }
            else
                Dm.remove(ref launchpads, get_pad(i));
        }
        data["launchpads"] = pad_list;
        data["selected_id"] = selected_id;
        if (Dm.truthy(selected_id))
        {
            obj_machinery_launchpad current_pad = launchpads[selected_id] as obj_machinery_launchpad;
            data["x"] = current_pad.x_offset;
            data["y"] = current_pad.y_offset;
            data["pad_name"] = current_pad.display_name;
            data["range"] = current_pad.range;
            data["selected_pad"] = current_pad;
            if (QDELETED(current_pad) || Dm.truthy(Dm.bitand(current_pad.machine_stat, NOPOWER)))
            {
                data["pad_active"] = FALSE;
                return data;
            }
            data["pad_active"] = TRUE;
        }

        return data;
    }

    // tg: code/game/machinery/computer/launchpad_control.dm:118
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        obj_machinery_launchpad current_pad = launchpads[selected_id] as obj_machinery_launchpad;
        object sw__1 = action;
        if (Dm.eq(sw__1, "select_pad"))
        {
            selected_id = text2num(@params["id"]);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "set_pos"))
        {
            object new_x = text2num(@params["x"]);
            object new_y = text2num(@params["y"]);
            current_pad.set_offset(new_x, new_y);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "move_pos"))
        {
            object plus_x = text2num(@params["x"]);
            object plus_y = text2num(@params["y"]);
            if (Dm.truthy(plus_x) || Dm.truthy(plus_y))
            {
                current_pad.set_offset(
                    x: current_pad.x_offset + Dm.num(plus_x),
                    y: current_pad.y_offset + Dm.num(plus_y)
                );
            }
            else
            {
                current_pad.set_offset(
                    x: 0,
                    y: 0
                );
            }
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "rename"))
        {
            dot = TRUE;
            object new_name = @params["name"];
            if (!Dm.truthy(new_name))
                return dot;
            current_pad.display_name = Dm.str(new_name);
        }
        else if (Dm.eq(sw__1, "remove"))
        {
            if (usr != null && Dm.eq(await tgui_alert(usr, "Are you sure?", "Unlink Launchpad", Dm.list("I'm Sure", "Abort")), "I'm Sure"))
            {
                Dm.remove(ref launchpads, current_pad);
                selected_id = null;
            }
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "launch"))
        {
            object checks = current_pad.teleport_checks();
            if (checks == null)
                await current_pad.doteleport(usr, TRUE);
            else
                to_chat(usr, span_warning(Dm.str(checks)));
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "pull"))
        {
            object checks = current_pad.teleport_checks();
            if (checks == null)
                await current_pad.doteleport(usr, FALSE);
            else
                to_chat(usr, span_warning(Dm.str(checks)));

            dot = TRUE;
        }
        dot = TRUE;
        return dot;
    }
}

// tg: code/game/machinery/computer/launchpad_control.dm:18
public partial class obj_item_circuit_component_bluespace_launchpad_console : obj_item_circuit_component_bluespace_launchpad
{
    // tg: code/game/machinery/computer/launchpad_control.dm:26
    public override async DmTask populate_ports()
    {
        launchpad_id = add_input_port("Launchpad ID", PORT_TYPE_NUMBER, trigger: null, @default: 1) as datum_port_input;
        await base.populate_ports();
    }

    // tg: code/game/machinery/computer/launchpad_control.dm:30
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_computer_launchpad)))
            attached_console = shell as obj_machinery_computer_launchpad;
    }

    // tg: code/game/machinery/computer/launchpad_control.dm:35
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        attached_console = null;
        return base.unregister_usb_parent(shell);
    }

    // tg: code/game/machinery/computer/launchpad_control.dm:39
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (attached_console == null || length(attached_console.launchpads) == 0)
        {
            why_fail.set_output("No launchpads connected!");
            on_fail.set_output(COMPONENT_SIGNAL);
            return null;
        }

        if (!Dm.truthy(launchpad_id.value))
            return null;

        attached_launchpad = KEYBYINDEX(attached_console.launchpads, Dm.num(launchpad_id.value)) as obj_machinery_launchpad;

        if (attached_launchpad == null)
        {
            why_fail.set_output("Invalid launchpad selected!");
            on_fail.set_output(COMPONENT_SIGNAL);
            return null;
        }
        base.input_received(port, return_values);
        return null;
    }
}
