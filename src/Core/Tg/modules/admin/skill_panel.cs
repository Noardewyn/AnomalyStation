// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/skill_panel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The admins' skill panel (the console's skill-panel, D275): tg's SkillPanel window (the client's
// Interfaces/SkillPanel.cs) lists a mind's skills, levels and experience; its buttons ask the admin for a number with tgui's number
// prompt (DV537: BYOND's input() as num|null, as DV217). The var block is generated (D111).

// tg: code/modules/admin/skill_panel.dm:1
public partial class datum_skill_panel : datum
{
    // tg: code/modules/admin/skill_panel.dm:5
    [DmParams("user", "mind")]
    public override async DmTask New(params object[] __extra) //H can either be a client or a mob due to byondcode(tm)
    {
        object user = Dm.arg(__extra, 0);
        datum_mind mind = Dm.arg<datum_mind>(__extra, 1);
        targetmind = mind;
        if (user is client)
        {
            client userClient = (client)user;
            holder = userClient; //if its a client, assign it to holder
        }
        else
        {
            mob userMob = user as mob;
            holder = userMob.client; //if its a mob, assign the mob's client to holder
        }
        await DmTask.CompletedTask;
    }

    // tg: code/modules/admin/skill_panel.dm:14
    public override object ui_state(mob user = null) => ADMIN_STATE(R_ADMIN);

    // tg: code/modules/admin/skill_panel.dm:17
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "SkillPanel");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/admin/skill_panel.dm:23
    public override async DmTask<object> ui_data(mob user = null) //Sends info about the skills to UI
    {
        DmList dot = null;
        dot = Dm.list();
        if (Dm.truthy(user?.mind))
        {
            foreach (object type in Dm.each(GLOB.skill_types))
            {
                datum_skill S = GetSkillRef(type) as datum_skill;
                object lvl_num = targetmind.get_skill_level(type);
                object lvl_name = uppertext(targetmind.get_skill_level_name(type));
                object exp = targetmind.get_skill_exp(type);
                object xp_prog_to_level = targetmind.exp_needed_to_level_up(type);
                object xp_req_to_level = 0;
                if (Dm.truthy(xp_prog_to_level)) //is it even possible to level up?
                    xp_req_to_level = Dm.sub(SKILL_EXP_LIST()[Dm.add(lvl_num, 1)], SKILL_EXP_LIST()[lvl_num]);
                object exp_percent = Dm.div(exp, SKILL_EXP_LIST()[SKILL_LEVEL_LEGENDARY]);
                Dm.append_at(dot, "skills", Dm.list(Dm.list(Dm.kv("playername", targetmind.current), Dm.kv("path", type), Dm.kv("name", S.name), Dm.kv("desc", S.desc), Dm.kv("lvlnum", lvl_num), Dm.kv("lvl", lvl_name), Dm.kv("exp", exp), Dm.kv("exp_prog", Dm.sub(xp_req_to_level, xp_prog_to_level)), Dm.kv("exp_req", xp_req_to_level), Dm.kv("exp_percent", exp_percent), Dm.kv("max_exp", SKILL_EXP_LIST()[length(SKILL_EXP_LIST())]))));
            }
        }
        return dot;
    }

    // tg: code/modules/admin/skill_panel.dm:38
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "adj_exp"))
        {
            Type skill = text2path(Dm.str(@params["skill"]));
            // DEVIATION(DV537): input("...") as num|null is tgui's number prompt, between -10000 and 10000 (any number in BYOND's)
            object number = await tgui_input_number(usr, I18n.T("Please insert the amount of experience you'd like to add/subtract:"), I18n.T("Adjust Exp"), max_value: 10000, min_value: -10000, round_value: FALSE);
            if (Dm.truthy(number))
                targetmind.adjust_experience(skill, number);
        }
        else if (Dm.eq(sw__1, "set_exp"))
        {
            Type skill = text2path(Dm.str(@params["skill"]));
            // DEVIATION(DV537): as above, between 0 and 10000
            object number = await tgui_input_number(usr, I18n.T("Please insert the number you want to set the player's exp to:"), I18n.T("Set Exp"), max_value: 10000, min_value: 0, round_value: FALSE);
            if (Dm.truthy(number))
                targetmind.set_experience(skill, number);
        }
        else if (Dm.eq(sw__1, "set_lvl"))
        {
            Type skill = text2path(Dm.str(@params["skill"]));
            float max_skill = length(SKILL_EXP_LIST());
            // DEVIATION(DV537): as above, a whole number between 1 and max_skill
            object number = await tgui_input_number(usr, I18n.T("Please insert a whole number between 1 (NONE) and [] (LEGENDARY) corresponding to the level you'd like to set the player to.", max_skill), I18n.T("Set Level"), max_value: max_skill, min_value: 1);
            if (Dm.gt(number, 0) && Dm.le(number, max_skill))
                targetmind.set_level(skill, number);
        }
        return dot;
    }
}
