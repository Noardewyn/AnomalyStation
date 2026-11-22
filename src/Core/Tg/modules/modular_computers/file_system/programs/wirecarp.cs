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
// the network's log and the PDAs' mass messaging, with the NtosNetMonitor window (lines 1-69). Its circuit component
// (`/obj/item/circuit_component/mod_program/ntnetmonitor`, :71-131) waits with wiremod (not-translated section 164). The var block is
// generated (D111).

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
