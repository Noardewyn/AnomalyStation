// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/statusdisplay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The status display app (gaps S4a, D327): the station's status displays' message and picture, set from a PDA or a console
// through the NtosStatus window (lines 1-91), and its circuit component that writes them from a circuit plugged into the computer.
// The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/statusdisplay.dm:1
public partial class datum_computer_file_program_status : datum_computer_file_program
{
    /**
     * Post status display radio packet.
     * Arguments:
     * * command - the status display command
     * * data1 - the data1 value, as defined by status displays
     * * data2 - the data2 value, as defined by status displays
     */
    // tg: code/modules/modular_computers/file_system/programs/statusdisplay.dm:26
    public virtual void post_status(object command = null, object data1 = null, object data2 = null)
    {
        datum_radio_frequency frequency = SSradio.return_frequency(FREQ_STATUS_DISPLAYS) as datum_radio_frequency;
        if (frequency == null)
            return;

        datum_signal status_signal = Dm.@new<datum_signal>(Dm.list(Dm.kv("command", command)));
        if (Dm.eq(command, "message"))
        {
            status_signal.data["top_text"] = data1;
            status_signal.data["bottom_text"] = data2;
        }
        else if (Dm.eq(command, "alert"))
        {
            status_signal.data["picture_state"] = data1;
        }

        frequency.post_signal(computer, status_signal); // tg passes the program (src); the port's signal source is an obj, its computer
    }

    /**
     * Post a message to status displays
     * Arguments:
     * * upper - Top text
     * * lower - Bottom text
     */
    // tg: code/modules/modular_computers/file_system/programs/statusdisplay.dm:47
    public virtual void post_message(object upper = null, object lower = null, object log_usr = null)
    {
        log_usr ??= key_name(usr);
        post_status("message", upper, lower);
        log_game(Dm.text("[] has changed the station status display message to \"[] []\" []", log_usr, upper, lower, loc_name(usr)));
    }

    /**
     * Post a picture to status displays
     * Arguments:
     * * picture - The picture name
     */
    // tg: code/modules/modular_computers/file_system/programs/statusdisplay.dm:56
    public virtual void post_picture(object picture = null, object log_usr = null)
    {
        log_usr ??= key_name(usr);
        if (!Dm.@in(picture, GLOB.status_display_approved_pictures))
            return;
        if (Dm.@in(picture, GLOB.status_display_state_pictures))
            post_status(picture);
        else
        {
            if (Dm.eq(picture, "currentalert")) // You cannot set Code Blue display during Code Red and similiar
                post_status("alert", SSsecurity_level?.current_security_level?.status_display_icon_state ?? "greenalert");
            else
                post_status("alert", picture);
        }

        log_game(Dm.text("[] has changed the station status display message to \"[]\" []", log_usr, picture, loc_name(usr)));
    }

    // tg: code/modules/modular_computers/file_system/programs/statusdisplay.dm:69
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        switch (action)
        {
            case "setStatusMessage":
                upper_text = reject_bad_text(Dm.truthy(@params["upperText"]) ? @params["upperText"] : "", MAX_STATUS_LINE_LENGTH) as string;
                lower_text = reject_bad_text(Dm.truthy(@params["lowerText"]) ? @params["lowerText"] : "", MAX_STATUS_LINE_LENGTH) as string;

                post_message(upper_text, lower_text);
                break;
            case "setStatusPicture":
                post_picture(@params["picture"]);
                break;
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/statusdisplay.dm:80
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["maxStatusLineLength"] = MAX_STATUS_LINE_LENGTH;
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/statusdisplay.dm:85
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["upperText"] = upper_text;
        data["lowerText"] = lower_text;

        return data;
    }
}

// tg: code/modules/modular_computers/file_system/programs/statusdisplay.dm:94
public partial class obj_item_circuit_component_mod_program_status : obj_item_circuit_component_mod_program
{
    // tg: code/modules/modular_computers/file_system/programs/statusdisplay.dm:105
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        upper_text = add_input_port("Upper text", PORT_TYPE_STRING) as datum_port_input;
        bottom_text = add_input_port("Bottom text", PORT_TYPE_STRING) as datum_port_input;
    }

    // tg: code/modules/modular_computers/file_system/programs/statusdisplay.dm:110
    public override void populate_options()
    {
        status_display_pics = add_option_port("Set Status Display Picture", GLOB.status_display_approved_pictures, trigger: nameof(set_picture)) as datum_port_input;
    }

    // tg: code/modules/modular_computers/file_system/programs/statusdisplay.dm:113
    public virtual void set_picture(datum_port port = null)
    {
        datum_computer_file_program_status status = associated_program as datum_computer_file_program_status;
        object creator = parent.get_creator();
        Waiting.DetachSync(() => status.post_picture(status_display_pics.value, creator));
    }

    // tg: code/modules/modular_computers/file_system/programs/statusdisplay.dm:117
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        datum_computer_file_program_status status = associated_program as datum_computer_file_program_status;
        object upper = upper_text.value, lower = bottom_text.value, creator = parent.get_creator();
        Waiting.DetachSync(() => status.post_message(upper, lower, creator));
        return null;
    }
}
