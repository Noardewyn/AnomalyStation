// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/antagonist/dos.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4c, D327): the DoS Traffic Generator, the syndicate's app that floods an NTNet relay (the relay's own overload
// is its file's), with its NtosNetDos window. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/antagonist/dos.dm:1
public partial class datum_computer_file_program_ntnet_dos : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/antagonist/dos.dm:17
    public override object process_tick(object seconds_per_tick = null)
    {
        dos_speed = 0;
        switch (ntnet_status)
        {
            case NTNET_LOW_SIGNAL:
                dos_speed = NTNETSPEED_LOWSIGNAL * 10;
                break;
            case NTNET_GOOD_SIGNAL:
                dos_speed = NTNETSPEED_HIGHSIGNAL * 10;
                break;
            case NTNET_ETHERNET_SIGNAL:
                dos_speed = NTNETSPEED_ETHERNET * 10;
                break;
        }
        if (target != null && Dm.truthy(executed))
        {
            target.dos_overload += dos_speed;
            if (!Dm.truthy(target.is_operational))
            {
                target.dos_sources.Remove(this);
                target = null;
                error = "Connection to destination relay lost.";
            }
        }
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/antagonist/dos.dm:33
    public override object kill_program(mob user = null)
    {
        if (target != null)
            target.dos_sources.Remove(this);
        target = null;
        executed = FALSE;
        return base.kill_program(user);
    }

    // tg: code/modules/modular_computers/file_system/programs/antagonist/dos.dm:40
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        switch (action)
        {
            case "PRG_target_relay":
                foreach (obj_machinery_ntnet_relay relays in Dm.each<obj_machinery_ntnet_relay>(SSmachines.get_machines_by_type(typeof(obj_machinery_ntnet_relay))))
                {
                    if (Dm.eq(relays.uid, @params["targid"]))
                    {
                        target = relays;
                        break;
                    }
                }
                return TRUE;
            case "PRG_reset":
                if (target != null)
                {
                    target.dos_sources.Remove(this);
                    target = null;
                }
                executed = FALSE;
                error = "";
                return TRUE;
            case "PRG_execute":
                if (target != null)
                {
                    executed = TRUE;
                    target.dos_sources.Add(this);
                    if (Dm.truthy(SSmodular_computers.intrusion_detection_enabled))
                    {
                        SSmodular_computers.add_log(Dm.text("IDS WARNING - Excess traffic flood targeting relay [] detected from device: []", target.uid, computer.name));
                        SSmodular_computers.intrusion_detection_alarm = TRUE;
                    }
                }
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/antagonist/dos.dm:65
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["error"] = error;
        if (target != null && Dm.truthy(executed))
        {
            data["target"] = TRUE;
            data["speed"] = dos_speed;

            data["overload"] = target.dos_overload;
            data["capacity"] = target.dos_capacity;
        }
        else
        {
            data["target"] = FALSE;
            data["relays"] = Dm.list();
            foreach (obj_machinery_ntnet_relay relays in Dm.each<obj_machinery_ntnet_relay>(SSmachines.get_machines_by_type(typeof(obj_machinery_ntnet_relay))))
                Dm.append_at(data, "relays", Dm.list(Dm.list(Dm.kv("id", relays.uid))));
            data["focus"] = target != null ? target.uid : null;
        }

        return data;
    }
}
