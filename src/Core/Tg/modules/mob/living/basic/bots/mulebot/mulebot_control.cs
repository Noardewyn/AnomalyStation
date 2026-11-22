// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/mulebot/mulebot_control.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:1
public partial class mob_living_basic_bot_mulebot : mob_living_basic_bot
{
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_control.dm:1
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Mule", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_control.dm:7
    public override async DmTask<object> ui_data(mob user = null)
    {
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        DmList data = Dm.list();
        data["powerStatus"] = Dm.bitand(bot_mode_flags, BOT_MODE_ON);
        data["locked"] = Dm.bitand(bot_access_flags, BOT_COVER_LOCKED);
        data["siliconUser"] = Dm.num(HAS_SILICON_ACCESS(user));
        data["mode"] = Dm.truthy(mode) ? mode : "Ready";
        data["modeStatus"] = "";
        switch (mode)
        {
            case BOT_IDLE: case BOT_DELIVER: case BOT_GO_HOME:
                data["modeStatus"] = "good";
                break;
            case BOT_BLOCKED: case BOT_NAV: case BOT_WAIT_FOR_NAV:
                data["modeStatus"] = "average";
                break;
            case BOT_NO_ROUTE:
                data["modeStatus"] = "bad";
                break;
        }
        data["load"] = Dm.num(load != null);
        data["loadName"] = get_load_name();
        data["destination"] = controller.blackboard[BB_MULEBOT_DESTINATION_BEACON];
        data["homeDestination"] = controller.blackboard[BB_MULEBOT_HOME_BEACON];
        data["destinationsList"] = GLOB.deliverybeacontags;
        data["cellPercent"] = cell?.percent();
        data["autoReturn"] = Dm.bitand(mulebot_delivery_flags, MULEBOT_RETURN_MODE);
        data["autoPickup"] = Dm.bitand(mulebot_delivery_flags, MULEBOT_AUTO_PICKUP_MODE);
        data["reportDelivery"] = Dm.bitand(mulebot_delivery_flags, MULEBOT_REPORT_DELIVERY_MODE);
        data["botId"] = id;
        data["allowPossession"] = Dm.bitand(bot_mode_flags, BOT_MODE_CAN_BE_SAPIENT);
        data["possessionEnabled"] = can_be_possessed;
        data["paiInserted"] = Dm.num(paicard != null);
        return data;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_control.dm:36
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = ui.user;
        if (Dm.truthy(dot) || (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) && !HAS_SILICON_ACCESS(user)))
            return dot;

        await bot_control(action, user, @params);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_control.dm:44
    [DmParams("command", "user", "params", "pda")]
    public override async DmTask<object> bot_control(object command = null, mob user = null, DmList user_access = null, params object[] __extra)
    {
        DmList @params = user_access ?? Dm.list();
        object pda = Dm.arg(__extra, 0) ?? FALSE;
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        if (Dm.truthy(pda) && Dm.truthy((wires as datum_wires).is_cut(WIRE_RX))) // MULE wireless is controlled by wires.
            return null;

        switch (command as string)
        {
            case "stop":
                if (!Dm.eq(mode, BOT_IDLE))
                    await bot_reset();
                break;
            case "go":
                if (Dm.eq(mode, BOT_IDLE))
                    start();
                break;
            case "home":
                if (Dm.eq(mode, BOT_IDLE) || Dm.eq(mode, BOT_DELIVER))
                    start_home();
                break;
            case "destination":
            {
                object new_dest;
                if (Dm.truthy(pda))
                    new_dest = await tgui_input_list(user, "Enter Destination", "Mulebot Settings", GLOB.deliverybeacontags, controller.blackboard[BB_MULEBOT_DESTINATION_BEACON]);
                else
                    new_dest = @params["value"];
                if (Dm.truthy(new_dest))
                    set_destination(new_dest);
                break;
            }
            case "setid":
            {
                object new_id = await tgui_input_text(user, "Enter ID", "ID Assignment", id, max_length: MAX_NAME_LEN);
                if (Dm.truthy(new_id))
                {
                    set_id(new_id);
                    name = Dm.text("\\improper MULEbot []", new_id);
                }
                break;
            }
            case "sethome":
            {
                object new_home = await tgui_input_list(user, "Enter Home", "Mulebot Settings", GLOB.deliverybeacontags, controller.blackboard[BB_MULEBOT_HOME_BEACON]);
                if (Dm.truthy(new_home))
                    controller.set_blackboard_key(BB_MULEBOT_HOME_BEACON, new_home);
                break;
            }
            case "unload":
                if (load != null && !Dm.eq(mode, BOT_HUNT))
                    await unload();
                break;
            case "autoret":
                mulebot_delivery_flags = Dm.bitxor(mulebot_delivery_flags, MULEBOT_RETURN_MODE);
                break;
            case "autopick":
                mulebot_delivery_flags = Dm.bitxor(mulebot_delivery_flags, MULEBOT_AUTO_PICKUP_MODE);
                break;
            case "report":
                mulebot_delivery_flags = Dm.bitxor(mulebot_delivery_flags, MULEBOT_REPORT_DELIVERY_MODE);
                break;
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_control.dm:83
    public virtual void start()
    {
        if (!Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)))
            return;
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        if (Dm.eq(controller.blackboard[BB_MULEBOT_DESTINATION_BEACON], controller.blackboard[BB_MULEBOT_HOME_BEACON]))
            update_bot_mode(new_mode: BOT_GO_HOME);
        else
            update_bot_mode(new_mode: BOT_DELIVER);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_control.dm:91
    public virtual void start_home()
    {
        set_destination((ai_controller as datum_ai_controller).blackboard[BB_MULEBOT_HOME_BEACON]);
        update_bot_mode(new_mode: BOT_GO_HOME);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_control.dm:95
    public virtual void set_destination(object new_destination = null)
    {
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_MULEBOT_DESTINATION_BEACON, new_destination);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_control.dm:98
    public virtual void set_home(turf home_loc = null)
    {
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        if (Dm.truthy(home_destination))
        {
            controller.set_blackboard_key(BB_MULEBOT_HOME_BEACON, home_destination);
            home_destination = null;
        }
        if (home_loc == null)
            throw Dm.CRASH(Dm.text("MULEbot [] was requested to set a home location to null", id), "code/modules/mob/living/basic/bots/mulebot/mulebot_control.dm", 103);

        obj_machinery_navbeacon home_beacon = Dm.locate<obj_machinery_navbeacon>(home_loc);
        if (home_beacon == null)
        {
            controller.set_blackboard_key(BB_MULEBOT_HOME_BEACON, "");
            return;
        }
        controller.set_blackboard_key(BB_MULEBOT_HOME_BEACON, home_beacon.location);
    }

    ///Sets the new ID of the mulebot
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_control.dm:114
    public virtual void set_id(object new_id = null)
    {
        id = new_id;
    }
}
