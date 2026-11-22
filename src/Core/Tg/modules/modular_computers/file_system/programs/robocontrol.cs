// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/robocontrol.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4b, D327): BotKeeper, the remote of the station's bots and MULEs and the drones' ping, with its NtosRoboControl
// window. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/robocontrol.dm:2
public partial class datum_computer_file_program_robocontrol : datum_computer_file_program
{
    ///List of all ping types you can annoy drones with.
    // tg: code/modules/modular_computers/file_system/programs/robocontrol.dm:16  var/static/list/drone_ping_types
    private static DmList drone_ping_types => TgState.Current.datum_computer_file_program_robocontrol__drone_ping_types ??= Dm.list(
        "Low",
        "Medium",
        "High",
        "Critical");

    // tg: code/modules/modular_computers/file_system/programs/robocontrol.dm:23
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        turf current_turf = get_turf(computer.ui_host() as atom) as turf;
        DmList botlist = Dm.list();
        DmList mulelist = Dm.list();

        if (computer != null)
            data["id_owner"] = (object)computer.stored_id ?? "";

        botcount = 0;

        foreach (mob_living_basic_bot basic_bot in Dm.each<mob_living_basic_bot>(GLOB.bots_list))
        {
            if (!Dm.truthy(is_valid_z_level(current_turf, get_turf(basic_bot) as turf)) || !Dm.truthy(Dm.bitand(basic_bot.bot_mode_flags, BOT_MODE_REMOTE_ENABLED))) //Only non-emagged bots on the same Z-level are detected!
                continue;
            if (!Dm.truthy(await basic_bot.allowed__proc(user)) && !Dm.truthy(basic_bot.check_access(computer.stored_id))) // Only check Bots we can access
                continue;
            DmList newbot = Dm.list(
                Dm.kv("name", basic_bot.name),
                Dm.kv("mode", basic_bot.get_mode_ui()),
                Dm.kv("model", basic_bot.bot_type),
                Dm.kv("locat", get_area(basic_bot)),
                Dm.kv("bot_ref", REF(basic_bot)),
                Dm.kv("mule_check", FALSE));
            if (Dm.eq(basic_bot.bot_type, MULE_BOT))
            {
                mob_living_basic_bot_mulebot basic_mulebot = basic_bot as mob_living_basic_bot_mulebot;
                mulelist.Add(Dm.list(Dm.list(
                    Dm.kv("name", basic_mulebot.name),
                    Dm.kv("id", basic_mulebot.id),
                    Dm.kv("dest", (basic_mulebot.ai_controller as datum_ai_controller).blackboard[BB_MULEBOT_DESTINATION_BEACON]),
                    Dm.kv("power", basic_mulebot.cell != null ? basic_mulebot.cell.percent() : 0),
                    Dm.kv("home", (basic_mulebot.ai_controller as datum_ai_controller).blackboard[BB_MULEBOT_HOME_BEACON]),
                    Dm.kv("autoReturn", Dm.bitand(basic_mulebot.mulebot_delivery_flags, MULEBOT_RETURN_MODE)),
                    Dm.kv("autoPickup", Dm.bitand(basic_mulebot.mulebot_delivery_flags, MULEBOT_AUTO_PICKUP_MODE)),
                    Dm.kv("reportDelivery", Dm.bitand(basic_mulebot.mulebot_delivery_flags, MULEBOT_REPORT_DELIVERY_MODE)),
                    Dm.kv("mule_ref", REF(basic_mulebot)),
                    Dm.kv("load", basic_mulebot.get_load_name()))));
                newbot["mule_check"] = TRUE;
            }
            botlist.Add(Dm.list(newbot));
        }

        foreach (mob_living_basic_drone all_drones in Dm.each<mob_living_basic_drone>(GLOB.drones_list))
        {
            if (Dm.truthy(all_drones.hacked))
                continue;
            if (!Dm.truthy(is_valid_z_level(current_turf, get_turf(all_drones) as turf)))
                continue;
            DmList drone_data = Dm.list(
                Dm.kv("name", all_drones.name),
                Dm.kv("status", all_drones.stat),
                Dm.kv("drone_ref", REF(all_drones)));
            Dm.append_at(data, "drones", Dm.list(drone_data));
        }

        data["bots"] = botlist;
        data["mules"] = mulelist;
        data["botcount"] = botlist.len;
        data["droneaccess"] = GLOB.drone_machine_blacklist_enabled;
        data["dronepingtypes"] = drone_ping_types;

        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/robocontrol.dm:87
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob current_user = ui.user;
        obj_item_card_id id_card = computer?.stored_id;

        DmList standard_actions = TgState.Current.datum_computer_file_program_robocontrol__standard_actions ??= Dm.list(
            "patroloff",
            "patrolon",
            "ejectpai");
        DmList MULE_actions = TgState.Current.datum_computer_file_program_robocontrol__MULE_actions ??= Dm.list(
            "stop",
            "go",
            "home",
            "destination",
            "setid",
            "sethome",
            "unload",
            "autoret",
            "autopick",
            "report",
            "ejectpai");
        // locate(params["robot"]) in GLOB.bots_list
        mob_living_basic_bot basic_bot = Dm.locate_ref(@params["robot"]) as mob_living_basic_bot;
        if (basic_bot != null && !Dm.@in(basic_bot, GLOB.bots_list))
            basic_bot = null;
        if (Dm.@in(action, standard_actions))
            await basic_bot.bot_control(action, current_user, id_card?.GetAccess() as DmList);
        if (Dm.@in(action, MULE_actions))
            await basic_bot.bot_control(action, current_user, id_card?.GetAccess() as DmList, TRUE);

        switch (action)
        {
            case "summon":
                await basic_bot.bot_control(action, current_user, id_card != null ? id_card.access : id_card?.GetAccess() as DmList);
                break;
            case "ejectcard":
                if (computer == null || computer.stored_id == null)
                    return dot;
                if (id_card != null)
                {
                    GLOB.manifest.modify(id_card.registered_name, id_card.assignment, id_card.get_trim_assignment());
                    await computer.remove_id(usr);
                }
                else
                    playsound(get_turf(computer.ui_host() as atom), Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 25, FALSE);
                break;
            case "changedroneaccess":
                if (computer == null || computer.stored_id == null || id_card == null)
                {
                    to_chat(current_user, span_notice(I18n.T("No ID found, authorization failed.")));
                    return dot;
                }
                if (Dm.truthy(isdrone(current_user)))
                {
                    to_chat(current_user, span_notice(I18n.T("You can't free yourself.")));
                    return dot;
                }
                if (!Dm.@in(ACCESS_CE, id_card.access))
                {
                    to_chat(current_user, span_notice(I18n.T("Required access not found on ID.")));
                    return dot;
                }
                GLOB.drone_machine_blacklist_enabled = !Dm.truthy(GLOB.drone_machine_blacklist_enabled) ? TRUE : FALSE;
                break;
            case "ping_drones":
            {
                if (!Dm.truthy(@params["ping_type"]) || !Dm.@in(@params["ping_type"], drone_ping_types))
                    return dot;
                area current_area = get_area(current_user) as area;
                if (current_area == null || QDELETED(current_user))
                    return dot;
                object msg = span_boldnotice(I18n.T("NON-DRONE PING: []: [] priority alert in []!", current_user.name, @params["ping_type"], current_area.name));
                _alert_drones(msg, TRUE, current_user);
                to_chat(current_user, msg);
                playsound(computer, Dm.resource("sound/machines/terminal/terminal_success.ogg"), 15, TRUE); // tg plays it at src, the program (a datum): its computer
                break;
            }
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/modular_computers/file_system/programs/robocontrol.dm:16, :92, :97  the program's static lists (rules 10.6)
    internal DmList datum_computer_file_program_robocontrol__drone_ping_types;
    internal DmList datum_computer_file_program_robocontrol__standard_actions;
    internal DmList datum_computer_file_program_robocontrol__MULE_actions;
}
