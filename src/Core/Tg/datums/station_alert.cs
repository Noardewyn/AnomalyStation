// Translated from tgstation (snapshot 1d99bd7f): code/datums/station_alert.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the engineering package E6, D287): the alert list behind the station alert consoles and the Canary program,
// with its StationAlertConsole window; the AI's jump to an alert's camera (select_camera) came with the AI (silicons SI4). The var block
// is generated (D111).

// tg: code/datums/station_alert.dm:1
public partial class datum_station_alert : datum
{
    // tg: code/datums/station_alert.dm:13
    public override object ui_host(mob user = null)
    {
        return holder;
    }

    // tg: code/datums/station_alert.dm:16
    [DmParams("holder", "alarm_types", "listener_z_level", "listener_areas", "title", "camera_view")]
    public override async DmTask New(params object[] __extra)
    {
        this.holder = Dm.arg(__extra, 0);
        this.alarm_types = Dm.arg(__extra, 1) as DmList;
        object listener_z_level = Dm.arg(__extra, 2);
        object listener_areas = Dm.arg(__extra, 3);
        this.title = Dm.arg(__extra, 4) ?? "Station Alerts";
        this.camera_view = Dm.arg(__extra, 5) ?? FALSE;
        listener = Dm.@new<datum_alarm_listener>(alarm_types, listener_z_level, listener_areas);
        await base.New(__extra);
    }

    // tg: code/datums/station_alert.dm:23
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref listener);
        return base.Destroy(force);
    }

    // tg: code/datums/station_alert.dm:27
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "StationAlertConsole", title);
            await ui.open();
        }
        return null;
    }

    // tg: code/datums/station_alert.dm:33
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["cameraView"] = camera_view;
        DmList data_alarms = Dm.list();
        data["alarms"] = data_alarms;
        DmList nominal_types = alarm_types.Copy();
        DmList alarms = listener.alarms;
        foreach (object alarm_type in Dm.each(alarms))
        {
            DmList category_alerts = Dm.list();
            DmList alarm_category = Dm.list(
                Dm.kv("name", alarm_type),
                Dm.kv("alerts", category_alerts));
            DmList alerts = alarms[alarm_type] as DmList;
            foreach (object alert in Dm.each(alerts))
            {
                DmList alert_details = alerts[alert] as DmList;
                Dm.append(ref category_alerts, Dm.list(Dm.list(
                    Dm.kv("name", get_area_name(alert_details[1] as atom, TRUE)),
                    Dm.kv("cameras", Dm.truthy(camera_view) ? Dm.length(alert_details[2]) : null),
                    Dm.kv("sources", Dm.truthy(camera_view) ? Dm.length(alert_details[3]) : null),
                    Dm.kv("ref", Dm.truthy(camera_view) ? REF(alert) : null))));
            }
            Dm.append(ref data_alarms, Dm.list(alarm_category));
            Dm.remove(ref nominal_types, alarm_type);
        }
        if (Dm.truthy(Dm.length(nominal_types)))
        {
            foreach (object nominal_type in Dm.each(nominal_types))
            {
                DmList nominal_category = Dm.list(
                    Dm.kv("name", nominal_type),
                    Dm.kv("alerts", Dm.list()));
                Dm.append(ref data_alarms, Dm.list(nominal_category));
            }
        }
        return data;
    }

    // tg: code/datums/station_alert.dm:64
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "select_camera"))
        {
            mob_living_silicon_ai ai = usr as mob_living_silicon_ai;
            if (ai == null)
                return dot;

            // silicons SI4
            DmList alarms = listener.alarms;
            DmList alerts = Dm.list();
            foreach (object alarm_type in Dm.each(alarms))
                Dm.append(ref alerts, alarms[alarm_type]);

            object our_alert = locate_ref_in(@params["alert"], alerts);
            if (!Dm.truthy(length(our_alert)))
                return null;
            DmList chosen_alert = alerts[our_alert] as DmList;
            DmList cameras = chosen_alert[2] as DmList;
            if (!Dm.truthy(length(cameras)))
                return null;
            DmList named_cameras = Dm.list();
            foreach (obj_machinery_camera camera in Dm.each<obj_machinery_camera>(cameras))
                named_cameras[camera.c_tag] = camera;

            object chosen_camera;
            if (length(named_cameras) == 1)
                chosen_camera = named_cameras[1];
            else
            {
                chosen_camera = await tgui_input_list(ai, "Choose a camera to jump to", "Camera Selection", named_cameras);
                if (chosen_camera == null)
                    return null;
            }
            obj_machinery_camera selected_camera = named_cameras[chosen_camera] as obj_machinery_camera;
            if (!Dm.truthy(selected_camera.can_use()))
            {
                to_chat(ai, span_warning(I18n.T("Camera is unavailable!")));
                return null;
            }
            ai.switchCamera(selected_camera);
            return null;
        }
        return dot;
    }
}
