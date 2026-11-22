// Translated from tgstation (snapshot 1d99bd7f): code/datums/station_traits/admin_panel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R2 (D327): the whole file: the admins' Modify Station Traits window (StationTraitsPanel): the round's traits, reverted one
// by one until the round starts, and the traits of the next round (written to FUTURE_STATION_TRAITS_FILE, which the next round's
// SSstation reads; the host carries the file over tg's reboot, as BYOND's data folder outlives a reboot). The verb's body is
// station_traits_panel, which the admin console's station-traits runs (D275). The var block is generated (D111).

public static partial class GlobalProcs
{
    // ADMIN_VERB(station_traits_panel, R_FUN, "Modify Station Traits", "Modify the station traits for the next round.", ADMIN_CATEGORY_EVENTS):
    // the verb's body, which the admin console's station-traits runs (D275)
    // tg: code/datums/station_traits/admin_panel.dm:1
    public static async DmTask station_traits_panel(client user = null)
    {
        if (!Dm.truthy(check_rights_for(user, R_FUN)))
            return;
        // var/static/datum/station_traits_panel/station_traits_panel = new (a proc-level static that makes a datum: rules 10.6, its
        // InitStatics line is the lead's; the ??= stands for the worlds that do not run InitStatics)
        station_traits_panel__station_traits_panel ??= Dm.@new<datum_station_traits_panel>();
        await station_traits_panel__station_traits_panel.ui_interact(user.mob);
    }

    // tg: code/datums/station_traits/admin_panel.dm:2  the verb's var/static/datum/station_traits_panel/station_traits_panel
    private static ref datum_station_traits_panel station_traits_panel__station_traits_panel => ref TgState.Current.GlobalProcs__station_traits_panel__station_traits_panel;
}

public sealed partial class TgState
{
    // tg: code/datums/station_traits/admin_panel.dm:2
    internal datum_station_traits_panel GlobalProcs__station_traits_panel__station_traits_panel;

    // tg: code/datums/station_traits/admin_panel.dm:6
    internal DmList datum_station_traits_panel__future_traits;
}

// tg: code/datums/station_traits/admin_panel.dm:5
public partial class datum_station_traits_panel : datum
{
    // tg: code/datums/station_traits/admin_panel.dm:6  var/static/list/future_traits
    public static ref DmList future_traits => ref TgState.Current.datum_station_traits_panel__future_traits;

    // tg: code/datums/station_traits/admin_panel.dm:8
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["too_late_to_revert"] = too_late_to_revert();

        DmList current_station_traits = Dm.list();
        foreach (datum_station_trait station_trait in Dm.each_cast<datum_station_trait>(SSstation.station_traits))
        {
            Dm.append(ref current_station_traits, Dm.list(Dm.list(
                Dm.kv("name", station_trait.name),
                Dm.kv("can_revert", station_trait.can_revert),
                Dm.kv("ref", REF(station_trait)))));
        }

        data["current_traits"] = current_station_traits;
        data["future_station_traits"] = future_traits;

        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/datums/station_traits/admin_panel.dm:26
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList valid_station_traits = Dm.list();

        foreach (Type station_trait_path in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_station_trait))))
        {
            Dm.append(ref valid_station_traits, Dm.list(Dm.list(
                Dm.kv("name", Dm.initial(station_trait_path, (datum_station_trait p) => p.name)),
                Dm.kv("path", station_trait_path))));
        }

        data["valid_station_traits"] = valid_station_traits;

        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/datums/station_traits/admin_panel.dm:41
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "revert":
            {
                object @ref = @params["ref"];
                if (!Dm.truthy(@ref))
                    return TRUE;

                var station_trait = locate_ref(@ref) as datum_station_trait;

                if (!istype(station_trait, typeof(datum_station_trait)))
                    return TRUE;

                if (Dm.truthy(too_late_to_revert()))
                {
                    to_chat(usr, span_warning(I18n.T("It's too late to revert station traits, the round has already started!")));
                    return TRUE;
                }

                if (!Dm.truthy(station_trait.can_revert))
                {
                    stack_trace(Dm.text("[] can't be reverted, but was requested anyway.", station_trait.type), "code/datums/station_traits/admin_panel.dm", 62);
                    return TRUE;
                }

                object message = Dm.text("[] reverted the station trait [] ([])", key_name(usr), station_trait.name, station_trait.type);
                log_admin(message);
                message_admins(message);

                station_trait.revert();

                return TRUE;
            }
            case "setup_future_traits":
            {
                if (Dm.truthy(too_late_for_future_traits()))
                {
                    to_chat(usr, span_warning(I18n.T("It's too late to add future station traits, the round is already over!")));
                    return TRUE;
                }

                DmList new_future_traits = Dm.list();
                DmList station_trait_names = Dm.list();

                foreach (object station_trait_text in Dm.each(@params["station_traits"]))
                {
                    Type station_trait_path = station_trait_text is string text ? text2path(text) : station_trait_text as Type;
                    if (!ispath(station_trait_path, typeof(datum_station_trait)) || station_trait_path == typeof(datum_station_trait))
                    {
                        log_admin(Dm.text("[] tried to set an invalid future station trait: []", key_name(usr), station_trait_text));
                        to_chat(usr, span_warning(I18n.T("Invalid future station trait: []", station_trait_text)));
                        return TRUE;
                    }

                    Dm.append(ref station_trait_names, Dm.initial(station_trait_path, (datum_station_trait p) => p.name));

                    Dm.append(ref new_future_traits, Dm.list(Dm.list(
                        Dm.kv("name", Dm.initial(station_trait_path, (datum_station_trait p) => p.name)),
                        Dm.kv("path", station_trait_path))));
                }

                object joined = station_trait_names.Join(", ");
                object message = Dm.text("[] has prepared the following station traits for next round: []", key_name(usr), Dm.truthy(joined) ? joined : "None");
                log_admin(message);
                message_admins(message);

                future_traits = new_future_traits;
                rustg_file_write(json_encode(@params["station_traits"]), FUTURE_STATION_TRAITS_FILE);

                return TRUE;
            }
            case "clear_future_traits":
            {
                if (future_traits == null)
                {
                    to_chat(usr, span_warning(I18n.T("There are no future station traits.")));
                    return TRUE;
                }

                object message = Dm.text("[] has cleared the station traits for next round.", key_name(usr));
                log_admin(message);
                message_admins(message);

                fdel(FUTURE_STATION_TRAITS_FILE);
                future_traits = null;

                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/datums/station_traits/admin_panel.dm:116
    public virtual object too_late_for_future_traits()
    {
        return Dm.num(SSticker.current_state) >= GAME_STATE_FINISHED ? TRUE : FALSE;
    }

    // tg: code/datums/station_traits/admin_panel.dm:119
    public virtual object too_late_to_revert()
    {
        return Dm.num(SSticker.current_state) >= GAME_STATE_PLAYING ? TRUE : FALSE;
    }

    // tg: code/datums/station_traits/admin_panel.dm:122
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        await DmTask.CompletedTask;
        return Dm.truthy(check_rights_for(user.client, R_FUN)) ? UI_INTERACTIVE : UI_CLOSE;
    }

    // tg: code/datums/station_traits/admin_panel.dm:125
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "StationTraitsPanel");
            await ui.open();
        }
        return null;
    }
}
