// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/robot_control.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the AI's remote control of the station's bots (RemoteRobotControl). The var block is generated
// (D111).

// tg: code/modules/mob/living/silicon/ai/robot_control.dm:1
public partial class datum_robot_control : datum
{
    // tg: code/modules/mob/living/silicon/ai/robot_control.dm:4
    [DmParams("new_owner")]
    public override async DmTask New(params object[] __extra)
    {
        mob_living_silicon_ai new_owner = Dm.arg<mob_living_silicon_ai>(__extra, 0);
        await base.New(__extra);
        if (new_owner == null)
            qdel(this);
        owner = new_owner;
    }

    // tg: code/modules/mob/living/silicon/ai/robot_control.dm:9
    public virtual bool is_interactable(mob user = null)
    {
        if (user != owner || Dm.truthy(owner.incapacitated))
            return false;
        if (Dm.truthy(owner.control_disabled))
        {
            to_chat(user, span_warning(I18n.T("Wireless control is disabled.")));
            return false;
        }
        return true;
    }

    // tg: code/modules/mob/living/silicon/ai/robot_control.dm:17
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (is_interactable(user))
            return await base.ui_status(user, state);
        return UI_CLOSE;
    }

    // tg: code/modules/mob/living/silicon/ai/robot_control.dm:22
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/modules/mob/living/silicon/ai/robot_control.dm:25
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "RemoteRobotControl");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/robot_control.dm:31
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        if (owner == null || user != owner)
            return null;
        DmList data = Dm.list();
        turf ai_current_turf = get_turf(owner);

        data["robots"] = Dm.list();
        foreach (mob_living_basic_bot our_bot in Dm.each_cast<mob_living_basic_bot>(GLOB.bots_list))
        {
            if (!isbot(our_bot) || !Dm.truthy(is_valid_z_level(ai_current_turf, get_turf(our_bot))))
                continue;

            mob_living_basic_bot basic_bot = our_bot;
            if (!Dm.truthy(Dm.bitand(basic_bot.bot_mode_flags, BOT_MODE_REMOTE_ENABLED)))
                continue;
            DmList basic_bot_data = Dm.list(
                Dm.kv("name", basic_bot.name),
                Dm.kv("model", basic_bot.bot_type),
                Dm.kv("mode", basic_bot.mode),
                Dm.kv("hacked", Dm.truthy(Dm.bitand(basic_bot.bot_access_flags, BOT_COVER_HACKED)) ? TRUE : FALSE),
                Dm.kv("location", get_area_name(basic_bot, TRUE)),
                Dm.kv("ref", REF(basic_bot))
            );
            DmList robots = data["robots"] as DmList;
            Dm.append(ref robots, Dm.list(basic_bot_data));
        }

        return data;
    }

    // tg: code/modules/mob/living/silicon/ai/robot_control.dm:56
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot) || !isliving(ui.user))
            return dot;
        mob_living our_user = ui.user as mob_living;
        if (!is_interactable(our_user))
            return dot;
        if (Dm.truthy(owner.control_disabled))
            return dot;
        mob_living bot = locate_ref_in(@params["ref"], GLOB.bots_list) as mob_living;
        if (bot == null)
            return dot;

        switch (Dm.str(action))
        {
            case "callbot": //Command a bot to move to a selected location.
            {
                mob_living_basic_bot basic_bot = bot as mob_living_basic_bot;
                if (!Dm.truthy(Dm.bitand(basic_bot.bot_mode_flags, BOT_MODE_REMOTE_ENABLED)))
                    return dot;

                owner.bot_ref = WEAKREF(bot) as datum_weakref;
                owner.setting_waypoint = TRUE;
                to_chat(our_user, span_notice(I18n.T("Set your waypoint by clicking on a valid location free of obstructions.")));
                break;
            }
            case "interface": //Remotely connect to a bot!
            {
                owner.bot_ref = WEAKREF(bot) as datum_weakref;
                mob_living_basic_bot basic_bot = bot as mob_living_basic_bot;
                if (!Dm.truthy(Dm.bitand(basic_bot.bot_mode_flags, BOT_MODE_REMOTE_ENABLED)))
                    return dot;

                await bot.attack_ai(our_user);
                break;
            }
        }

        return TRUE;
    }
}
