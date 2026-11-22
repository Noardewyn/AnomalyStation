// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/apc_control.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the engineering package E6, D287): the power flow control console, its ApcControl window (the log-in by the chief
// engineer's access, the station's APCs with their charge, draw, breaker and channel toggles, the remote access to an APC's own
// window, the activity log, an emag's open console and its restore). The AI's attack_ai came with the AI (silicons SI4).
// The var blocks are generated (D111).

// tg: code/game/machinery/computer/apc_control.dm:1
public partial class obj_machinery_computer_apc_control : obj_machinery_computer
{
    // tg: code/game/machinery/computer/apc_control.dm:23
    [DmParams("mapload", "C")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        is_on_station = Dm.num(is_station_level(z));
        return dot;
    }

    // tg: code/game/machinery/computer/apc_control.dm:27
    public override async DmTask on_set_machine_stat(object old_value = null)
    {
        await base.on_set_machine_stat(old_value);
        if (Dm.truthy(machine_stat) && active_apc != null)
            await disconnect_apc();
    }

    // tg: code/game/machinery/computer/apc_control.dm:38
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        if (user != null)
        {
            user.log_message(Dm.text("emagged [].", this), LOG_ATTACK, color: "red");
            balloon_alert(user, I18n.T("access controller shorted"));
        }
        playsound(this, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        return TRUE;
    }

    ///Creates a log entry in the console with a timestamp, current login ID data and the text provided in log_text
    // tg: code/game/machinery/computer/apc_control.dm:49
    public virtual void log_activity(object log_text = null)
    {
        if (!Dm.truthy(should_log))
            return;
        LAZYADD(ref logs, Dm.text("([]): [] []", round_timestamp(), auth_id, log_text));
    }

    ///Resets the console's emagged state and re-enables logging of activity
    // tg: code/game/machinery/computer/apc_control.dm:55
    public virtual void restore_comp(mob user = null)
    {
        obj_flags = Dm.bitand(obj_flags, Dm.bitnot(EMAGGED));
        should_log = TRUE;
        user.log_message(Dm.text("restored the logs of [].", this), LOG_GAME);
        log_activity(I18n.T("-=- Logging restored to full functionality at this point -=-"));
        restoring = FALSE;
    }

    ///Initiates remote access to the APC
    // tg: code/game/machinery/computer/apc_control.dm:63
    public virtual async DmTask connect_apc(obj_machinery_power_apc apc = null, mob user = null)
    {
        if (apc == null)
            return;
        if (apc.remote_control_user != null)
        {
            to_chat(user, span_warning(I18n.T("\\The [] is being controlled by someone else!", apc)));
            return;
        }
        if (active_apc != null)
            await disconnect_apc();
        playsound(this, Dm.resource("sound/machines/terminal/terminal_prompt_confirm.ogg"), 50, FALSE);
        await apc.connect_remote_access(user);
        user.log_message(Dm.text("remotely accessed [] from [].", apc, this), LOG_GAME);
        log_activity(I18n.T("[] remotely accessed APC in []", auth_id, get_area_name(apc.area, TRUE)));
        active_apc = apc;
        RegisterSignal(active_apc, COMSIG_QDELETING, nameof(on_apc_destroyed));
    }

    ///Disconnects the computer from the accessed APC upon its destruction
    // tg: code/game/machinery/computer/apc_control.dm:79
    [SignalHandler]
    public virtual void on_apc_destroyed(datum source = null)
    {
        Waiting.NoSleep(disconnect_apc(TRUE)); //to prevent the APC from trying to speak while being qdel'd
    }

    /**
     * Disconnect from the APC we're currently in remote access with
     * arguments:
     * mute - whether the APC should announce the disconnection locally, passed into apc's disconnect_remote_access()
     */
    // tg: code/game/machinery/computer/apc_control.dm:88
    public virtual async DmTask disconnect_apc(float? mute = null)
    {
        mute ??= FALSE;
        UnregisterSignal(active_apc, COMSIG_QDELETING);
        if (active_apc.remote_control_user != null)
            await active_apc.disconnect_remote_access(mute);
        active_apc = null;
    }

    /**
    * Checks for whether the APC provided is eligible for access and being listed in the APC list.
    * The APC has to:
    * - be on a station z-level if the computer is station-side or be on the same z-level as the computer if otherwise (away subtype, charlie station)
    * - be not hacked by a malf AI
    * - have AI control enabled
    * - be not emagged
    * - be working
    * - not be an AI monitored area (AI sat areas and AI upload)
    */
    // tg: code/game/machinery/computer/apc_control.dm:105
    public virtual bool check_apc(obj_machinery_power_apc checked_apc = null)
    {
        return (Dm.truthy(is_on_station) ? Dm.truthy(is_station_level(checked_apc.z)) : checked_apc.z == z) && !Dm.truthy(checked_apc.malfhack) && !Dm.truthy(checked_apc.aidisabled)
            && !Dm.truthy(Dm.bitand(checked_apc.obj_flags, EMAGGED)) && !Dm.truthy(checked_apc.machine_stat) && !(checked_apc.area is area_station_ai);
    }

    // tg: code/game/machinery/computer/apc_control.dm:108
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ApcControl");
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/apc_control.dm:115
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["auth_id"] = auth_id;
        data["authenticated"] = authenticated;
        data["emagged"] = Dm.bitand(obj_flags, EMAGGED);
        data["logging"] = should_log;
        data["restoring"] = restoring;
        DmList data_logs = Dm.list();
        data["logs"] = data_logs;
        DmList data_apcs = Dm.list();
        data["apcs"] = data_apcs;

        foreach (object entry in Dm.each(logs))
            Dm.append(ref data_logs, Dm.list(Dm.list(Dm.kv("entry", entry))));

        foreach (obj_machinery_power_apc apc in Dm.each_cast<obj_machinery_power_apc>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))))
        {
            if (check_apc(apc))
            {
                float has_cell = apc.cell != null ? TRUE : FALSE;
                Dm.append(ref data_apcs, Dm.list(Dm.list(
                    Dm.kv("name", apc.area.name),
                    Dm.kv("operating", apc.operating),
                    Dm.kv("charge", Dm.truthy(has_cell) ? apc.cell.percent() : "NOCELL"),
                    Dm.kv("load", display_power(apc.lastused_total)),
                    Dm.kv("charging", apc.charging),
                    Dm.kv("chargeMode", apc.chargemode),
                    Dm.kv("eqp", apc.equipment),
                    Dm.kv("lgt", apc.lighting),
                    Dm.kv("env", apc.environ),
                    Dm.kv("responds", Dm.truthy(apc.aidisabled) ? apc.aidisabled : apc.panel_open),
                    Dm.kv("ref", REF(apc)))));
            }
        }
        return data;
    }

    // tg: code/game/machinery/computer/apc_control.dm:147
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob_living user = ui.user as mob_living;
        switch (action)
        {
            case "log-in":
            {
                if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                {
                    authenticated = TRUE;
                    auth_id = "Unknown (Unknown):";
                    log_activity(I18n.T("[] logged in to the terminal", auth_id));
                    return dot;
                }
                if (user == null)
                    return dot;
                obj_item_card_id user_id_card = user.get_idcard(TRUE) as obj_item_card_id;
                if (user_id_card != null)
                {
                    if (Dm.truthy(check_access(user_id_card)))
                    {
                        authenticated = TRUE;
                        auth_id = Dm.text("[] ([]):", user_id_card.registered_name, user_id_card.assignment);
                        log_activity(I18n.T("[] logged in to the terminal", auth_id));
                        playsound(this, Dm.resource("sound/machines/terminal/terminal_on.ogg"), 50, FALSE);
                    }
                    else
                    {
                        auth_id = Dm.text("[] ([]):", user_id_card.registered_name, user_id_card.assignment);
                        log_activity(I18n.T("[] attempted to log into the terminal", auth_id));
                        playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 50, FALSE);
                        await say(I18n.T("ID rejected, access denied!"));
                    }
                    return dot;
                }
                auth_id = "Unknown (Unknown):";
                log_activity(I18n.T("[] attempted to log into the terminal", auth_id));
                break;
            }
            case "log-out":
                log_activity(I18n.T("[] logged out of the terminal", auth_id));
                playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 50, FALSE);
                authenticated = FALSE;
                auth_id = "[NULL]";
                break;
            case "toggle-logs":
                should_log = Dm.truthy(should_log) ? FALSE : TRUE;
                user.log_message(Dm.text("set the logs of [] [].", this, Dm.truthy(should_log) ? "On" : "Off"), LOG_GAME);
                break;
            case "restore-console":
                restoring = TRUE;
                _addtimer(Dm.@new<datum_callback>(this, nameof(restore_comp), user), Dm.rand(3, 5) * 9 * SECONDS, file: "code/game/machinery/computer/apc_control.dm", line: 187);
                break;
            case "access-apc":
            {
                object @ref = @params?["ref"];
                playsound(this, SFX_TERMINAL_TYPE, 50, FALSE);
                // locate(ref) in SSmachines.get_machines_by_type_and_subtypes(/obj/machinery/power/apc)
                obj_machinery_power_apc remote_target = Dm.locate_ref(@ref) as obj_machinery_power_apc;
                if (remote_target != null && !Dm.@in(remote_target, SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))))
                    remote_target = null;
                if (remote_target == null || !check_apc(remote_target))
                    return dot;
                await connect_apc(remote_target, user);
                return TRUE;
            }
            case "check-logs":
                log_activity(I18n.T("Checked Logs"));
                break;
            case "check-apcs":
                log_activity(I18n.T("Checked APCs"));
                break;
            case "toggle-minor":
            {
                object @ref = @params?["ref"];
                object type = @params?["type"];
                object value = @params?["value"];
                // locate(ref) in SSmachines.get_machines_by_type_and_subtypes(/obj/machinery/power/apc)
                obj_machinery_power_apc target = Dm.locate_ref(@ref) as obj_machinery_power_apc;
                if (target != null && !Dm.@in(target, SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))))
                    target = null;
                if (target == null || !check_apc(target))
                    return dot;

                value = target.setsubsystem(text2num(value));
                // switch(type) // Sanity check
                if (Dm.eq(type, "equipment"))
                    target.equipment = Dm.num_or_null(value);
                else if (Dm.eq(type, "lighting"))
                    target.lighting = Dm.num(value);
                else if (Dm.eq(type, "environ"))
                    target.environ = Dm.num(value);
                else if (type == null)
                    return dot;
                else
                {
                    message_admins(Dm.text("Warning: possible href exploit by [] - attempted to set [] on [] to []", key_name(user), html_encode(type), target, html_encode(value)));
                    user.log_message(Dm.text("possibly trying to href exploit - attempted to set [] on [] to []", html_encode(type), target, html_encode(value)), LOG_ADMIN);
                    return dot;
                }

                target.update_appearance();
                target.update();
                string setTo = "";
                // switch(target.vars[type]): the channel just set (rules 10.7, vars is not translated)
                object set_value = Dm.eq(type, "equipment") ? target.equipment : Dm.eq(type, "lighting") ? target.lighting : target.environ;
                if (Dm.eq(set_value, 0))
                    setTo = I18n.T("Off");
                else if (Dm.eq(set_value, 1))
                    setTo = I18n.T("Auto Off");
                else if (Dm.eq(set_value, 2))
                    setTo = I18n.T("On");
                else if (Dm.eq(set_value, 3))
                    setTo = I18n.T("Auto On");
                log_activity(I18n.T("Set APC [] [] to []", target.area.name, type, setTo));
                user.log_message("set APC [target.area.name] [type] to [setTo]]", LOG_GAME);
                break;
            }
            case "breaker":
            {
                object @ref = @params?["ref"];
                // locate(ref) in SSmachines.get_machines_by_type_and_subtypes(/obj/machinery/power/apc)
                obj_machinery_power_apc breaker_target = Dm.locate_ref(@ref) as obj_machinery_power_apc;
                if (breaker_target != null && !Dm.@in(breaker_target, SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))))
                    breaker_target = null;
                if (breaker_target == null || !check_apc(breaker_target))
                    return dot;
                await breaker_target.toggle_breaker(user);
                string setTo = Dm.truthy(breaker_target.operating) ? I18n.T("On") : I18n.T("Off");
                log_activity(I18n.T("Turned APC []'s breaker []", breaker_target.area.name, setTo));
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/game/machinery/computer/apc_control.dm:247
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        if (active_apc != null)
            Waiting.NoSleep(disconnect_apc());
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/machinery/computer/apc_control.dm:32
public partial class obj_machinery_computer_apc_control : obj_machinery_computer
{
    // tg: code/game/machinery/computer/apc_control.dm:32
    public override async DmTask<object> attack_ai(mob user = null)
    {
        // GAP(lead): isAdminGhostAI (modules/mob/mob_helpers.dm:357) is not declared: no admin ghosts yet (as _machinery.cs).
        bool isAdminGhostAI__gap = false;
        if (!isAdminGhostAI__gap)
        {
            to_chat(user, span_warning(I18n.T("[] does not support AI control.", this))); //You already have APC access, cheater!
            return null;
        }
        return await base.attack_ai(user);
    }
}
