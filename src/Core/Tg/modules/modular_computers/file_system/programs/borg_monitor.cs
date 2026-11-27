// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/borg_monitor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SiliConnect and the syndicate's Roboverlord (gaps S4b, D327): the cyborgs' remote monitor, their system log downloaded by tapping a
// cyborg, and messages to a cyborg, with the NtosCyborgRemoteMonitor window (lines 1-188), and their circuit
// components (:190-216) that message a cyborg from a circuit plugged into the computer. The var blocks are
// generated (D111).

// tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:1
public partial class datum_computer_file_program_borg_monitor : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:18
    public override object Destroy(float? force = null)
    {
        loglist = null;
        DL_source = null;
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:23
    public override object kill_program(mob user = null)
    {
        loglist = null; //Not everything is saved if you close an app
        DL_source = null;
        DL_progress = 0;
        return base.kill_program(user);
    }

    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:29
    public override async DmTask<object> tap(atom tapped_atom = null, mob_living user = null, DmList modifiers = null)
    {
        mob_living_silicon_robot borgo = tapped_atom as mob_living_silicon_robot;
        if (borgo == null || borgo.modularInterface == null)
            return FALSE;
        DL_source = borgo;
        DL_progress = 0;

        string username = "unknown user";
        obj_item_card_id stored_card = computer.GetID() as obj_item_card_id;
        if (stored_card != null && Dm.truthy(stored_card.registered_name))
            username = Dm.text("user []", stored_card.registered_name);
        to_chat(borgo, span_userdanger(I18n.T("Request received from [] for the system log file. Upload in progress.", username))); //Damning evidence may be contained, so warn the borg
        await borgo.logevent(Dm.text("File request by []: /var/logs/syslog", username));
        borgo.balloon_alert(user, "downloading logs");
        return TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:45
    public override object process_tick(object seconds_per_tick = null)
    {
        if (DL_source == null)
        {
            DL_progress = -1;
            return null;
        }

        turf here = get_turf(computer) as turf;
        turf there = get_turf(DL_source) as turf;
        if (!Dm.truthy(Waiting.NoSleep(here.Adjacent(there)))) //If someone walked away, cancel the download
        {
            to_chat(DL_source, span_danger(I18n.T("Log upload failed: general connection error."))); //Let the borg know the upload stopped
            DL_source = null;
            DL_progress = -1;
            return null;
        }

        if (DL_progress == 100)
        {
            if (DL_source == null || DL_source.modularInterface == null) //sanity check, in case the borg or their modular tablet poofs somehow
            {
                loglist = Dm.list(Dm.text("System log of unit []", DL_source.name));
                loglist.Add("Error -- Download corrupted.");
            }
            else
            {
                loglist = DL_source.modularInterface.borglog.Copy();
                loglist.Insert(1, Dm.text("System log of unit []", DL_source.name));
            }
            DL_progress = -1;
            DL_source = null;
            return null;
        }

        DL_progress += 25;
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:71
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["card"] = FALSE;
        if (Dm.truthy(checkID()))
            data["card"] = TRUE;

        data["cyborgs"] = Dm.list();
        foreach (mob_living_silicon_robot R in Dm.each<mob_living_silicon_robot>(GLOB.silicon_mobs))
        {
            if (!evaluate_borg(R))
                continue;

            DmList upgrade = null;
            foreach (obj_item_borg_upgrade I in Dm.each<obj_item_borg_upgrade>(R.upgrades))
                Dm.append(ref upgrade, Dm.text("\\[[]\\] ", I.name));

            bool shell = Dm.truthy(R.shell) && !Dm.truthy(R.ckey);

            obj_item_stock_parts_power_store cell = R.cell as obj_item_stock_parts_power_store;
            DmList cyborg_data = Dm.list(
                Dm.kv("name", R.name),
                Dm.kv("integ", Dm.round((Dm.num(R.health) + 100) / 2)), //mob heath is -100 to 100, we want to scale that to 0 - 100
                Dm.kv("locked_down", R.lockcharge),
                Dm.kv("status", R.stat),
                Dm.kv("shell_discon", shell ? TRUE : FALSE),
                Dm.kv("charge", cell != null ? Dm.round(Dm.num(cell.percent())) : null),
                Dm.kv("module", R.model != null ? Dm.text("[] Model", R.model.name) : "No Model Detected"),
                Dm.kv("upgrades", upgrade),
                Dm.kv("ref", REF(R)));
            Dm.append_at(data, "cyborgs", Dm.list(cyborg_data));
            data["DL_progress"] = DL_progress;
        }

        data["borglog"] = loglist;

        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:109
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        switch (action)
        {
            case "messagebot":
            {
                // locate(params["ref"]) in GLOB.silicon_mobs
                mob_living_silicon_robot robot = Dm.locate_ref(@params["ref"]) as mob_living_silicon_robot;
                if (robot != null && !Dm.@in(robot, GLOB.silicon_mobs))
                    robot = null;
                await message_robot(robot, usr);
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:117
    public virtual async DmTask<object> message_robot(mob_living_silicon_robot robot = null, mob user = null)
    {
        if (robot == null)
            return TRUE;
        object ID = checkID();
        if (!Dm.truthy(ID))
            return FALSE;
        if (robot.stat == DEAD) //Dead borgs will listen to you no longer
        {
            to_chat(user, span_warning(I18n.T("Error -- Could not open a connection to unit:[]", robot)));
            return FALSE;
        }
        object message = await tgui_input_text(user, "Message to be sent to remote cyborg", "Send Message", max_length: MAX_MESSAGE_LEN);
        if (!Dm.truthy(message))
            return FALSE;
        return await send_message(message, robot, user);
    }

    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:131
    public virtual async DmTask<object> send_message(object message = null, mob_living_silicon_robot robot = null, mob user = null)
    {
        object ID = checkID();
        if (!Dm.truthy(ID))
            return FALSE;
        if (robot.stat == DEAD) //Dead borgs will listen to you no longer
        {
            if (user != null)
            {
                to_chat(user, span_warning(I18n.T("Error -- Could not open a connection to unit:[]", robot)));
                return FALSE;
            }
        }
        to_chat(robot, Dm.text("<br><br>[]<br>", span_notice(I18n.T("Message from [] -- \"[]\"", ID, message))));
        if (user != null)
            to_chat(user, I18n.T("Message sent to []: []", robot, message));
        await robot.logevent(Dm.text("Message from [] -- \"[]\"", ID, message));
        Dm.output_to(robot, sound(Dm.resource("sound/machines/beep/twobeep_high.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        if (robot.connected_ai != null)
        {
            to_chat(robot.connected_ai, Dm.text("<br><br>[]<br>", span_notice(I18n.T("Message from [] to [] -- \"[]\"", ID, robot, message))));
            Dm.output_to(robot.connected_ai, sound(Dm.resource("sound/machines/beep/twobeep_high.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        }
        user?.log_talk(message, LOG_PDA, tag: Dm.text("Cyborg Monitor Program: ID name \"[]\" to []", ID, robot));
        return TRUE;
    }

    ///This proc is used to determin if a borg should be shown in the list (based on the borg's scrambledcodes var). Syndicate version overrides this to show only syndicate borgs.
    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:151
    public virtual bool evaluate_borg(mob_living_silicon_robot R = null)
    {
        if (!Dm.truthy(is_valid_z_level(get_turf(computer) as turf, get_turf(R) as turf)))
            return false;
        if (Dm.truthy(R.scrambledcodes))
            return false;
        return true;
    }

    ///Gets the ID's name, if one is inserted into the device. This is a separate proc solely to be overridden by the syndicate version of the app.
    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:159
    public virtual object checkID()
    {
        obj_item_card_id ID = computer.GetID() as obj_item_card_id;
        if (ID == null)
        {
            if (Dm.truthy(Dm.bitand(computer.obj_flags, EMAGGED)))
                return "STDERR:UNDF";
            return FALSE;
        }
        object dot = Dm.text("[]", ID.registered_name);
        if (Dm.truthy(ID.assignment))
            dot = Dm.text("[], []", dot, ID.assignment);
        return dot;
    }
}

// tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:169
public partial class datum_computer_file_program_borg_monitor_syndicate : datum_computer_file_program_borg_monitor
{
    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:180
    public override bool evaluate_borg(mob_living_silicon_robot R = null)
    {
        if (!Dm.truthy(is_valid_z_level(get_turf(computer) as turf, get_turf(R) as turf)))
            return false;
        if (!Dm.truthy(R.scrambledcodes))
            return false;
        return true;
    }

    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:187
    public override object checkID()
    {
        return "[CLASSIFIED]"; //no ID is needed for the syndicate version's message function, and the borg will see "[CLASSIFIED]" as the message sender.
    }
}

// tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:190
public partial class obj_item_circuit_component_mod_program_borg_monitor : obj_item_circuit_component_mod_program
{
    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:199
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        target_robot = add_input_port("Receiver", PORT_TYPE_ATOM) as datum_port_input;
        set_message = add_input_port("Set Message", PORT_TYPE_STRING, trigger: nameof(sanitize_borg_message)) as datum_port_input;
    }

    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:204
    public virtual void sanitize_borg_message(datum_port port = null)
    {
        set_message.set_value(trim(html_encode(set_message.value), MAX_MESSAGE_LEN));
    }

    // tg: code/modules/modular_computers/file_system/programs/borg_monitor.dm:207
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (!Dm.truthy(length(set_message.value)) || !iscyborg(target_robot.value))
            return null;
        mob_living_silicon_robot robot = target_robot.value as mob_living_silicon_robot;
        datum_computer_file_program_borg_monitor monitor = associated_program as datum_computer_file_program_borg_monitor;
        if (Dm.truthy(Waiting.NoSleep(monitor.send_message(set_message.value, robot))))
            monitor.computer.log_talk(Dm.text("Cyborg Monitor message (ID name \"[]\") sent to [] by []: []", monitor.checkID(), key_name(robot), parent.get_creator(), set_message.value));
        return null;
    }
}
