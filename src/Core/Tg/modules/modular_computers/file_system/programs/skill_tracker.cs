// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/skill_tracker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. ExperTrak, the PDA's skill tracker, with its NtosSkillTracker window: the user's skills, levels and
// progress, and the legendary level's reward button (the skill cape's supply pod). The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/skill_tracker.dm:1
public partial class datum_computer_file_program_skill_tracker : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/skill_tracker.dm:12
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList skills = Dm.list();
        data["skills"] = skills;

        datum_mind targetmind = user.mind;
        if (Dm.truthy(targetmind))
        {
            foreach (object type in Dm.each(GLOB.skill_types))
            {
                datum_skill skill = GetSkillRef(type) as datum_skill;
                object lvl_num = targetmind.get_skill_level(type);
                object lvl_name = uppertext(targetmind.get_skill_level_name(type));
                object exp = targetmind.get_skill_exp(type);
                object xp_prog_to_level = targetmind.exp_needed_to_level_up(type);
                object xp_req_to_level = 0;
                if (Dm.truthy(xp_prog_to_level) && Dm.lt(lvl_num, length(SKILL_EXP_LIST()))) // is it even possible to level up?
                    xp_req_to_level = Dm.sub(SKILL_EXP_LIST()[Dm.add(lvl_num, 1)], SKILL_EXP_LIST()[lvl_num]);

                DmList skilldata = Dm.list(
                    Dm.kv("name", skill.name),
                    Dm.kv("desc", skill.desc),
                    Dm.kv("title", skill.title),
                    Dm.kv("lvl_name", lvl_name));
                if (Dm.truthy(exp) && Dm.truthy(xp_req_to_level))
                {
                    skilldata["progress_percent"] = Dm.div(Dm.sub(xp_req_to_level, xp_prog_to_level), xp_req_to_level);
                    skilldata["overall_percent"] = Dm.div(exp, SKILL_EXP_LIST()[length(SKILL_EXP_LIST())]);
                }
                if (Dm.ge(lvl_num, length(SKILL_EXP_LIST())) && !Dm.@in(type, targetmind.skills_rewarded))
                    skilldata["reward"] = TRUE;
                skills.Add(Dm.list(skilldata)); // skills[++skills.len] = skilldata
            }
        }

        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/skill_tracker.dm:45
    public virtual object find_skilltype(object name = null)
    {
        foreach (object type in Dm.each(GLOB.skill_types))
        {
            datum_skill skill = GetSkillRef(type) as datum_skill;
            if (Dm.eq(skill.name, name))
                return type;
        }

        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/skill_tracker.dm:53
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        object sw__1 = action;
        if (Dm.eq(sw__1, "PRG_reward"))
        {
            object skill_type = find_skilltype(@params["skill"]);
            if (Dm.truthy(skill_type))
            {
                datum_skill skill = GetSkillRef(skill_type) as datum_skill;
                datum_mind mind = ui.user.mind;
                object new_level = mind.get_skill_level(skill_type);
                await skill.try_skill_reward(mind, new_level);
                return TRUE;
            }
        }
        return dot;
    }
}
