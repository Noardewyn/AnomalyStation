// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/alarm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the engineering package E6, D287): Canary, the station alert program of the engineering consoles, with its
// NtosStationAlertConsole window, the console's alert-green and alert-red screens and the header's alarm pictures. The var block
// is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/alarm.dm:1
public partial class datum_computer_file_program_alarm_monitor : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/alarm.dm:17
    [SignalHandler]
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        base.on_install(source, computer_installing, user);
        //We want to send an alarm if we're in one of the mining home areas
        //Or if we're on station. Otherwise, die.
        DmList allowed_areas = Dm.add(GLOB.the_station_areas, Dm.typesof(typeof(area_mine))) as DmList;
        alert_control = Dm.@new<datum_station_alert>(computer, Dm.list(ALARM_ATMOS, ALARM_FIRE, ALARM_POWER), Dm.named("listener_areas", allowed_areas));
        RegisterSignals(alert_control.listener, Dm.list(COMSIG_ALARM_LISTENER_TRIGGERED, COMSIG_ALARM_LISTENER_CLEARED), nameof(update_alarm_display));
    }

    // tg: code/modules/modular_computers/file_system/programs/alarm.dm:25
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref alert_control);
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/alarm.dm:29
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        Dm.append(ref data, await alert_control.ui_data(user));
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/alarm.dm:34
    [SignalHandler]
    public virtual void update_alarm_display()
    {
        // has_alert is true if there are any active alarms in our listener.
        has_alert = Dm.length(alert_control.listener.alarms) > 0 ? TRUE : FALSE;

        if (!Dm.truthy(has_alert))
        {
            program_open_overlay = "alert-green";
            ui_header = "alarm_green.gif";
        }
        else
        {
            // If we don't know the status, assume the worst.
            // Technically we should never have anything other than a truthy or falsy value
            // but this will allow for unknown values to fall through to be an actual alert.
            program_open_overlay = "alert-red";
            ui_header = "alarm_red.gif";
        }
        update_computer_icon(); // Always update the icon after we check our conditional because we might've changed it
    }
}
