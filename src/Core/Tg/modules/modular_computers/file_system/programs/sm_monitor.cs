// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/sm_monitor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the engineering package E2, D287): NT CIMS, the supermatter monitor of the engineering consoles, with its
// NtosSupermatter window (the crystals' sm_ui_data), the focused crystal's delamination alert and the console's smmon screen.
// The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:1
public partial class datum_computer_file_program_supermatter_monitor : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:20
    public override async DmTask<object> on_start(mob_living user = null)
    {
        object dot = await base.on_start(user);
        refresh();
        return dot;
    }

    /// Apparently destroy calls this [/datum/computer_file/Destroy]. Here just to clean our references.
    // tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:25
    public override object kill_program(mob user = null)
    {
        foreach (object supermatter in Dm.each(supermatters))
            clear_supermatter(supermatter as obj_machinery_power_supermatter_crystal);
        return base.kill_program(user);
    }

    /// Refreshes list of active supermatter crystals
    // tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:31
    public virtual void refresh()
    {
        foreach (object supermatter in Dm.each(supermatters))
            clear_supermatter(supermatter as obj_machinery_power_supermatter_crystal);
        turf user_turf = get_turf(computer.ui_host() as atom);
        if (user_turf == null)
            return;
        foreach (obj_machinery_power_supermatter_crystal sm in Dm.each_cast<obj_machinery_power_supermatter_crystal>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_supermatter_crystal))))
        {
            //Exclude Syndicate owned, Delaminating, not within coverage, not on a tile.
            if (!Dm.truthy(sm.include_in_cims) || !isturf(sm.loc) || !(Dm.truthy(is_station_level(sm.z)) || Dm.truthy(is_mining_level(sm.z)) || Dm.eq(sm.z, user_turf.z)))
                continue;
            Dm.append(ref supermatters, sm);
            RegisterSignal(sm, COMSIG_QDELETING, nameof(clear_supermatter));
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:44
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["gas_metadata"] = sm_gas_data();
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:49
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList sm_data = Dm.list();
        data["sm_data"] = sm_data;
        foreach (obj_machinery_power_supermatter_crystal sm in Dm.each_cast<obj_machinery_power_supermatter_crystal>(supermatters))
            Dm.append(ref sm_data, Dm.list(sm.sm_ui_data()));
        data["focus_uid"] = focused_supermatter?.uid;
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:57
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.eq(action, "PRG_refresh"))
        {
            refresh();
            return TRUE;
        }
        else if (Dm.eq(action, "PRG_focus"))
        {
            foreach (obj_machinery_power_supermatter_crystal sm in Dm.each<obj_machinery_power_supermatter_crystal>(supermatters))
            {
                if (Dm.eq(sm.uid, @params["focus_uid"]))
                {
                    if (focused_supermatter == sm)
                        unfocus_supermatter();
                    else
                        focus_supermatter(sm);
                    return TRUE;
                }
            }
        }
        return dot;
    }

    /// Sends an SM delam alert to the computer if our focused supermatter is delaminating.
    /// [var/obj/machinery/power/supermatter_crystal/focused_supermatter].
    // tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:74
    [SignalHandler]
    public virtual void send_alert()
    {
        if (!Dm.truthy(computer.get_ntnet_status()))
            return;
        computer.alert_call(this, "Crystal delamination in progress!");
        alert_pending = TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:81
    [SignalHandler]
    public virtual void clear_supermatter(obj_machinery_power_supermatter_crystal sm = null)
    {
        supermatters.Remove(sm);
        if (focused_supermatter == sm)
            unfocus_supermatter();
        UnregisterSignal(sm, COMSIG_QDELETING);
    }

    // tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:88
    public virtual void focus_supermatter(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (sm == focused_supermatter)
            return;
        if (focused_supermatter != null)
            unfocus_supermatter();
        RegisterSignal(sm, COMSIG_SUPERMATTER_DELAM_ALARM, nameof(send_alert));
        focused_supermatter = sm;
    }

    // tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:96
    public virtual void unfocus_supermatter()
    {
        if (focused_supermatter == null)
            return;
        UnregisterSignal(focused_supermatter, COMSIG_SUPERMATTER_DELAM_ALARM);
        focused_supermatter = null;
    }

    // tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:102
    public virtual object get_status()
    {
        object dot = SUPERMATTER_INACTIVE;
        foreach (obj_machinery_power_supermatter_crystal S in Dm.each<obj_machinery_power_supermatter_crystal>(supermatters))
            dot = Dm.max(dot, S.get_status());
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/sm_monitor.dm:107
    public override object process_tick(object seconds_per_tick = null)
    {
        base.process_tick(seconds_per_tick);
        float new_status = Dm.num(get_status());
        if (last_status != new_status)
        {
            last_status = new_status;
            ui_header = Dm.text("smmon_[].gif", last_status);
            program_open_overlay = Dm.text("smmon_[]", last_status);
            if (computer is obj_item_modular_computer)
                computer.update_appearance();
        }
        return null;
    }
}
