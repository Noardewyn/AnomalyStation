// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/server_control.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD2, D333: the R&D consoles and servers): the R&D server control console and its ServerControl window:
// the research log, every server of its techweb with its status and the switch that halts it, every R&D console reading the web
// with the lock its board holds; research director access, or an emag. The var block is generated (D111).

// tg: code/modules/research/server_control.dm:1
public partial class obj_machinery_computer_rdservercontrol : obj_machinery_computer
{
    // tg: code/modules/research/server_control.dm:12
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_default_techweb_link))) && stored_research == null) /* CONFIG_GET(flag/no_default_techweb_link) */
            CONNECT_TO_RND_SERVER_ROUNDSTART(ref stored_research, this);
    }

    // tg: code/modules/research/server_control.dm:17
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        var multitool = tool as obj_item_multitool;
        if (multitool != null && !QDELETED(multitool.buffer as datum) && multitool.buffer is datum_techweb techweb)
        {
            stored_research = techweb;
            balloon_alert(user, "techweb connected");
        }
        return TRUE;
    }

    // tg: code/modules/research/server_control.dm:23
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        Dm.combine(ref obj_flags, EMAGGED);
        playsound(this, SFX_SPARKS, 75, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        balloon_alert(user, "console emagged");
        return TRUE;
    }

    // tg: code/modules/research/server_control.dm:31
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "ServerControl", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/research/server_control.dm:38
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        data["server_connected"] = stored_research != null ? TRUE : FALSE;

        if (stored_research != null)
        {
            Dm.append_at(data, "logs", stored_research.research_logs);

            foreach (obj_machinery_rnd_server server in Dm.each_cast<obj_machinery_rnd_server>(stored_research.techweb_servers))
            {
                Dm.append_at(data, "servers", Dm.list(Dm.list(
                    Dm.kv("server_name", server),
                    Dm.kv("server_details", server.get_status_text()),
                    Dm.kv("server_disabled", server.research_disabled),
                    Dm.kv("server_ref", REF(server)))));
            }

            foreach (obj_machinery_computer_rdconsole console in Dm.each_cast<obj_machinery_computer_rdconsole>(stored_research.consoles_accessing))
            {
                // the board instance is circuit_value (GAP(types), _machinery.cs)
                obj_item_circuitboard_computer_rdconsole console_board = console.circuit_value as obj_item_circuitboard_computer_rdconsole;
                Dm.append_at(data, "consoles", Dm.list(Dm.list(
                    Dm.kv("console_name", console),
                    Dm.kv("console_location", get_area(console)),
                    Dm.kv("console_locked", console_board.locked),
                    Dm.kv("console_ref", REF(console)))));
            }
        }

        return data;
    }

    // tg: code/modules/research/server_control.dm:65
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return TRUE;
        if (!Dm.truthy(await allowed__proc(usr)) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(usr, "access denied!");
            playsound(this, "sound/machines/click.ogg", 20, TRUE);
            return TRUE;
        }

        if (Dm.eq(action, "lockdown_server"))
        {
            obj_machinery_rnd_server server_selected = Dm.locate_ref(@params["selected_server"]) is obj_machinery_rnd_server found && Dm.@in(found, stored_research.techweb_servers) ? found : null; // locate(params["selected_server"]) in stored_research.techweb_servers
            if (server_selected == null)
                return FALSE;
            server_selected.toggle_disable(usr);
            return TRUE;
        }
        else if (Dm.eq(action, "lock_console"))
        {
            obj_machinery_computer_rdconsole console_selected = Dm.locate_ref(@params["selected_console"]) is obj_machinery_computer_rdconsole found__1 && Dm.@in(found__1, stored_research.consoles_accessing) ? found__1 : null; // locate(params["selected_console"]) in stored_research.consoles_accessing
            // tg reads the board before its null check (a runtime on a stale console that ends the act); null-safe here
            obj_item_circuitboard_computer_rdconsole console_board = console_selected?.circuit_value as obj_item_circuitboard_computer_rdconsole;
            if (console_selected == null)
                return FALSE;
            console_board.locked = Dm.truthy(console_board.locked) ? FALSE : TRUE;
            return TRUE;
        }
        return null;
    }
}
