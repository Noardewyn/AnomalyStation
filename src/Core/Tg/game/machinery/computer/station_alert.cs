// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/station_alert.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the engineering package E6, D287): the station alert console, the station's fire, atmosphere and power alarms in
// its StationAlertConsole window and the red alert:2 screen while any is raised. The var block is generated (D111).

// tg: code/game/machinery/computer/station_alert.dm:1
public partial class obj_machinery_computer_station_alert : obj_machinery_computer
{
    private static ref DmList Initialize__alert_areas => ref TgState.Current.obj_machinery_computer_station_alert__Initialize__alert_areas;

    // tg: code/game/machinery/computer/station_alert.dm:12
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(is_station_level(z)))
        {
            if (Initialize__alert_areas == null)
                Initialize__alert_areas = Dm.add(GLOB.the_station_areas, Dm.typesof(typeof(area_mine))) as DmList;
            alert_control = Dm.@new<datum_station_alert>(this, Dm.list(ALARM_ATMOS, ALARM_FIRE, ALARM_POWER), SSmapping.levels_by_trait(ZTRAIT_STATION), Initialize__alert_areas);
        }
        else
        {
            name = "local alert console";
            alert_control = Dm.@new<datum_station_alert>(this, Dm.list(ALARM_ATMOS, ALARM_FIRE, ALARM_POWER), Dm.list(z), Dm.named("title", name));
        }
        RegisterSignals(alert_control.listener, Dm.list(COMSIG_ALARM_LISTENER_TRIGGERED, COMSIG_ALARM_LISTENER_CLEARED), nameof(update_alarm_display));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/machinery/computer/station_alert.dm:24
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref alert_control);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/computer/station_alert.dm:28
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        await alert_control.ui_interact(user);
        return dot;
    }

    // tg: code/game/machinery/computer/station_alert.dm:32  (tg's override does not call the parent's)
    public override async DmTask on_set_machine_stat(object old_value = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            alert_control.listener.prevent_alarm_changes();
        else
            alert_control.listener.allow_alarm_changes();
    }

    // tg: code/game/machinery/computer/station_alert.dm:38
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return dot;
        if (Dm.truthy(Dm.length(alert_control.listener.alarms)))
            Dm.append(ref dot, "alert:2");
        return dot;
    }

    /**
     * Signal handler for calling an icon update in case an alarm is added or cleared
     *
     * Arguments:
     * * source The datum source of the signal
     */
    // tg: code/game/machinery/computer/station_alert.dm:51
    [SignalHandler]
    public virtual void update_alarm_display(datum source = null)
    {
        Waiting.NoSleep(update_icon());
    }
}

public sealed partial class TgState
{
    /// tg <c>var/static/list/alert_areas</c> in <c>/obj/machinery/computer/station_alert/Initialize</c> (rules 10.6).
    // tg: code/game/machinery/computer/station_alert.dm:14
    internal DmList obj_machinery_computer_station_alert__Initialize__alert_areas;
}
