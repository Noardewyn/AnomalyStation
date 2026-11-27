// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/wirecarp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// WireCarp (gaps S4b, D327): the NTNet monitor of the network administrator: the relays and their switches, the intrusion detection,
// the network's log and the PDAs' mass messaging, with the NtosNetMonitor window (lines 1-69), and its circuit component
// (:71-131) that works them from a circuit plugged into the computer. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/wirecarp.dm:1
public partial class datum_computer_file_program_ntnetmonitor : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/wirecarp.dm:14
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        switch (action)
        {
            case "resetIDS":
                SSmodular_computers.intrusion_detection_alarm = FALSE;
                return TRUE;
            case "toggleIDS":
                SSmodular_computers.intrusion_detection_enabled = !Dm.truthy(SSmodular_computers.intrusion_detection_enabled) ? TRUE : FALSE;
                return TRUE;
            case "toggle_relay":
            {
                // locate(params["ref"]) in SSmachines.get_machines_by_type(/obj/machinery/ntnet_relay)
                obj_machinery_ntnet_relay target_relay = Dm.locate_ref(@params["ref"]) as obj_machinery_ntnet_relay;
                if (target_relay != null && !Dm.@in(target_relay, SSmachines.get_machines_by_type(typeof(obj_machinery_ntnet_relay))))
                    target_relay = null;
                if (target_relay == null)
                    return dot;
                await target_relay.set_relay_enabled(!Dm.truthy(target_relay.relay_enabled) ? TRUE : FALSE);
                return TRUE;
            }
            case "purgelogs":
                SSmodular_computers.purge_logs();
                return TRUE;
            case "toggle_mass_pda":
            {
                if (!Dm.@in(@params["ref"], GLOB.pda_messengers))
                    return dot;
                datum_computer_file_program_messenger target_messenger = GLOB.pda_messengers[@params["ref"]] as datum_computer_file_program_messenger;
                target_messenger.spam_mode = !Dm.truthy(target_messenger.spam_mode) ? TRUE : FALSE;
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/wirecarp.dm:39
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["ntnetrelays"] = Dm.list();
        foreach (obj_machinery_ntnet_relay relays in Dm.each<obj_machinery_ntnet_relay>(SSmachines.get_machines_by_type(typeof(obj_machinery_ntnet_relay))))
        {
            DmList relay_data = Dm.list();
            relay_data["is_operational"] = Dm.truthy(relays.is_operational) ? TRUE : FALSE;
            relay_data["name"] = relays.name;
            relay_data["ref"] = REF(relays);

            Dm.append_at(data, "ntnetrelays", Dm.list(relay_data));
        }

        data["idsstatus"] = SSmodular_computers.intrusion_detection_enabled;
        data["idsalarm"] = SSmodular_computers.intrusion_detection_alarm;

        data["ntnetlogs"] = Dm.list();
        foreach (object i in Dm.each(SSmodular_computers.modpc_logs))
            Dm.append_at(data, "ntnetlogs", Dm.list(Dm.list(Dm.kv("entry", i))));

        data["tablets"] = Dm.list();
        foreach (datum_computer_file_program_messenger app in Dm.each<datum_computer_file_program_messenger>(GLOB.pda_messengers_by_name))
        {
            obj_item_modular_computer pda = app.computer;

            DmList tablet_data = Dm.list();
            tablet_data["enabled_spam"] = app.spam_mode;
            tablet_data["name"] = pda.saved_identification;
            tablet_data["ref"] = REF(app);

            Dm.append_at(data, "tablets", Dm.list(tablet_data));
        }

        return data;
    }
}

// tg: code/modules/modular_computers/file_system/programs/wirecarp.dm:71
public partial class obj_item_circuit_component_mod_program_ntnetmonitor : obj_item_circuit_component_mod_program
{
    // tg: code/modules/modular_computers/file_system/programs/wirecarp.dm:91
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        target = add_input_port("Target Messenger/Relay", PORT_TYPE_ATOM) as datum_port_input;
        toggle_ids = add_input_port("Toggle IDS Status", PORT_TYPE_SIGNAL, trigger: nameof(toggle_ids__proc)) as datum_port_input;
        toggle_relay = add_input_port("Toggle NTnet Relay", PORT_TYPE_SIGNAL, trigger: nameof(toggle_relay__proc)) as datum_port_input;
        purge_logs = add_input_port("Purge Logs", PORT_TYPE_SIGNAL, trigger: nameof(purge_logs__proc)) as datum_port_input;
        toggle_mass_pda = add_input_port("Toggle Mass Messenger", PORT_TYPE_SIGNAL, trigger: nameof(toggle_pda_stuff)) as datum_port_input;
        toggle_mime_mode = add_input_port("Toggle Mime Mode", PORT_TYPE_SIGNAL, trigger: nameof(toggle_pda_stuff)) as datum_port_input;
        get_pdas = add_input_port("Get PDAs", PORT_TYPE_SIGNAL, trigger: nameof(get_pdas__proc)) as datum_port_input;
        all_messengers = add_output_port("List of PDAs", PORT_TYPE_LIST(PORT_TYPE_ATOM)) as datum_port_output;
    }

    // tg: code/modules/modular_computers/file_system/programs/wirecarp.dm:102
    public virtual void get_pdas__proc(datum_port port = null)
    {
        DmList computers_with_messenger = Dm.list();
        foreach (object messenger_ref in Dm.each(GLOB.pda_messengers))
        {
            datum_computer_file_program_messenger messenger = GLOB.pda_messengers[messenger_ref] as datum_computer_file_program_messenger;
            Dm.combine(ref computers_with_messenger, WEAKREF(messenger.computer));
        }
        all_messengers.set_output(computers_with_messenger);
    }

    // tg: code/modules/modular_computers/file_system/programs/wirecarp.dm:109
    public virtual void toggle_ids__proc(datum_port port = null)
    {
        SSmodular_computers.intrusion_detection_enabled = Dm.truthy(SSmodular_computers.intrusion_detection_enabled) ? FALSE : TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/wirecarp.dm:112
    public virtual async DmTask toggle_relay__proc(datum_port port = null)
    {
        obj_machinery_ntnet_relay target_relay = target.value as obj_machinery_ntnet_relay;
        if (target_relay == null)
            return;
        await target_relay.set_relay_enabled(Dm.truthy(target_relay.relay_enabled) ? FALSE : TRUE);
    }

    // tg: code/modules/modular_computers/file_system/programs/wirecarp.dm:118
    public virtual void purge_logs__proc(datum_port port = null)
    {
        SSmodular_computers.purge_logs();
    }

    // tg: code/modules/modular_computers/file_system/programs/wirecarp.dm:121
    public virtual void toggle_pda_stuff(datum_port port = null)
    {
        obj_item_modular_computer computer = target.value as obj_item_modular_computer;
        if (computer == null)
            return;
        datum_computer_file_program_messenger target_messenger = Dm.locate_type(typeof(datum_computer_file_program_messenger), computer.stored_files) as datum_computer_file_program_messenger;
        if (target_messenger == null)
            return;
        if (COMPONENT_TRIGGERED_BY(toggle_mass_pda, port))
            target_messenger.spam_mode = Dm.truthy(target_messenger.spam_mode) ? FALSE : TRUE;
        if (COMPONENT_TRIGGERED_BY(toggle_mime_mode, port))
            target_messenger.mime_mode = Dm.truthy(target_messenger.mime_mode) ? FALSE : TRUE;
    }
}
