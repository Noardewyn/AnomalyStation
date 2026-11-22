// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/computers/logbrowser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D2: the telecomms server monitoring console): it scans a network for the servers within 25 tiles, shows
// a server's traffic and stored packets in its ServerMonitor window and deletes a packet with telecomms access; a packet in a language
// the reader does not know is scrambled (gaps L2). The var block is generated (D111).

// tg: code/game/machinery/telecomms/computers/logbrowser.dm:1  #define MAIN_VIEW 0, SERVER_VIEW 1, MAX_NETWORK_ID_LENGTH 15 (file-local)
// tg: code/game/machinery/telecomms/computers/logbrowser.dm:5
public partial class obj_machinery_computer_telecomms_server : obj_machinery_computer_telecomms
{
    private const float MAIN_VIEW = 0;
    private const float SERVER_VIEW = 1;
    private const float MAX_NETWORK_ID_LENGTH = 15;

    // tg: code/game/machinery/telecomms/computers/logbrowser.dm:27
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list(
            Dm.kv("screen", screen),
            Dm.kv("network", network),
            Dm.kv("error", error_message));

        if (screen == MAIN_VIEW)
        {
            // If there are servers in the buffer, send them
            DmList found_servers = Dm.list();
            foreach (obj_machinery_telecomms_server server in Dm.each<obj_machinery_telecomms_server>(servers))
                Dm.append(ref found_servers, Dm.list(Dm.list(Dm.kv("ref", REF(server)), Dm.kv("name", server.name), Dm.kv("id", server.id))));
            data["servers"] = found_servers;
        }
        else if (screen == SERVER_VIEW)
        {
            // Send selected server data
            DmList server_out = Dm.list();
            server_out["name"] = SelectedServer.name;
            server_out["traffic"] = SelectedServer.total_traffic;
            // Get the messages on this server
            DmList packets = Dm.list();
            foreach (datum_comm_log_entry packet in Dm.each<datum_comm_log_entry>(SelectedServer.log_entries))
            {
                DmList packet_out = Dm.list();

                packet_out["name"] = packet.name;

                object language = packet.parameters["language"];
                datum_language language_instance = GLOB.language_datum_instances[language] as datum_language;
                packet_out["language"] = language_instance.name;
                string message_out = "";
                object message_in = packet.parameters["message"];
                if (Dm.truthy(universal_translate) || Dm.truthy(user.has_language(language)))
                {
                    // Known language or translator, show plain text
                    message_out = Dm.text("\"[]\"", message_in);
                }
                else if (!Dm.truthy(user.has_language(language)))
                {
                    // Language unknown: scramble (gaps L2)
                    message_out = Dm.text("\"[]\"", language_instance.scramble_paragraph(message_in, user.get_partially_understood_languages() as DmList));
                }
                else
                    message_out = I18n.T("(Unintelligible)");
                packet_out["message"] = message_out;

                object mobtype = packet.parameters["mobtype"];
                string race = I18n.T("Unidentifiable");
                if (ispath(mobtype, typeof(mob_living_carbon_human)) || ispath(mobtype, typeof(mob_living_brain)))
                    race = I18n.T("Humanoid");

                // NT knows a lot about slimes, but not aliens. Can identify slimes
                else if (ispath(mobtype, typeof(mob_living_basic_slime)))
                    race = I18n.T("Slime");

                // sometimes M gets deleted prematurely for AIs... just check the job
                else if (ispath(mobtype, typeof(mob_living_silicon)) || Dm.eq(packet.parameters["job"], JOB_AI))
                    race = I18n.T("Artificial Life");

                else if (isobj(mobtype))
                    race = I18n.T("Machinery");

                else if (ispath(mobtype, typeof(mob_living_simple_animal)))
                    race = I18n.T("Domestic Animal");

                packet_out["race"] = race;
                packet_out["source"] = packet.parameters["name"];
                packet_out["job"] = packet.parameters["job"];
                packet_out["type"] = packet.input_type;
                packet_out["ref"] = REF(packet);

                Dm.append(ref packets, Dm.list(packet_out));
            }
            server_out["packets"] = packets;
            data["server"] = server_out;
        }
        return data;
    }

    // tg: code/game/machinery/telecomms/computers/logbrowser.dm:100
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        error_message = "";

        dot = TRUE;
        if (Dm.eq(action, "scan_network"))
        {
            // Scan for a network
            object new_network = @params["network_id"];

            if (Dm.length(new_network) > MAX_NETWORK_ID_LENGTH)
            {
                error_message = I18n.T("OPERATION FAILED: NETWORK ID TOO LONG.");
                return dot;
            }

            if (servers.len > 0)
            {
                error_message = I18n.T("OPERATION FAILED: BUFFER ALREADY POPULATED. PLEASE CLEAR THE BUFFER.");
                return dot;
            }

            network = Dm.str(new_network);

            foreach (obj_machinery_telecomms_server server in Dm.each<obj_machinery_telecomms_server>(urange(25, this)))
            {
                if (Dm.eq(server.network, network))
                    servers.Add(server);
            }
            if (servers.len == 0)
            {
                error_message = I18n.T("OPERATION FAILED: UNABLE TO LOCATE ANY SERVERS IN NETWORK [].", network);
                return dot;
            }
            return dot;
        }
        if (Dm.eq(action, "clear_buffer"))
        {
            servers = Dm.list();
            network = "";
            return dot;
        }
        if (Dm.eq(action, "view_server"))
        {
            SelectedServer = Dm.locate_ref(@params["server"]) as obj_machinery_telecomms_server;
            if (SelectedServer == null)
            {
                error_message = I18n.T("OPERATION FAILED: UNABLE TO LOCATE SERVER.");
                return dot;
            }
            screen = SERVER_VIEW;
            return dot;
        }
        if (Dm.eq(action, "return_home"))
        {
            SelectedServer = null;
            screen = MAIN_VIEW;
            return dot;
        }
        if (Dm.eq(action, "delete_packet"))
        {
            if (SelectedServer == null)
                return dot;
            // Delete a packet from server logs
            datum_comm_log_entry packet = Dm.locate_ref(@params["ref"]) as datum_comm_log_entry;
            if (!Dm.@in(packet, SelectedServer.log_entries))
            {
                error_message = I18n.T("OPERATION FAILED: PACKET NOT FOUND.");
                return dot;
            }
            if (!Dm.truthy(await allowed__proc(usr)) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            {
                error_message = I18n.T("OPERATION FAILED: ACCESS DENIED.");
                return dot;
            }
            SelectedServer.log_entries.Remove(packet);
            error_message = I18n.T("SUCCESSFULLY DELETED [].", packet.name);
            qdel(packet);
            return dot;
        }
        return FALSE;
    }

    // tg: code/game/machinery/telecomms/computers/logbrowser.dm:162
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ServerMonitor", name);
            await ui.open();
        }
        return dot;
    }
}
