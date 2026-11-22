// Translated from tgstation (snapshot 1d99bd7f): code/datums/mind/skills.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file: the mind's skill table, its experience and levels, the modifiers it reads and the levels it
// prints.

// tg: code/datums/mind/skills.dm:1
public partial class datum_mind : datum
{
    // tg: code/datums/mind/skills.dm:1
    public virtual void init_known_skills()
    {
        foreach (object type in Dm.each(GLOB.skill_types))
            known_skills[type] = Dm.list(SKILL_LEVEL_NONE, 0);
    }

    ///Return the amount of EXP needed to go to the next level. Returns 0 if max level
    // tg: code/datums/mind/skills.dm:6
    public virtual object exp_needed_to_level_up(object skill = null)
    {
        object lvl = update_skill_level(skill);
        if (Dm.ge(lvl, length(SKILL_EXP_LIST()))) //If we're already past the last exp threshold
            return 0;
        return Dm.sub(SKILL_EXP_LIST()[Dm.add(lvl, 1)], ((DmList)known_skills[skill])[SKILL_EXP]);
    }

    ///Adjust experience of a specific skill
    // tg: code/datums/mind/skills.dm:13
    public virtual void adjust_experience(object skill = null, object amt = null, float? silent = null, float? force_old_level = null)
    {
        silent ??= FALSE;
        force_old_level ??= 0;
        datum_skill S = GetSkillRef(skill) as datum_skill;
        object old_level = Dm.truthy(force_old_level) ? (object)force_old_level : ((DmList)known_skills[skill])[SKILL_LVL]; //Get current level of the S skill
        experience_multiplier = Dm.initial(this, (datum_mind p) => p.experience_multiplier);
        foreach (object key in Dm.each(experience_multiplier_reasons))
            experience_multiplier += Dm.num(experience_multiplier_reasons[key]);
        ((DmList)known_skills[skill])[SKILL_EXP] = max(0, Dm.add(((DmList)known_skills[skill])[SKILL_EXP], Dm.mul(amt, experience_multiplier))); //Update exp. Prevent going below 0
        ((DmList)known_skills[skill])[SKILL_LVL] = update_skill_level(skill); //Check what the current skill level is based on that skill's exp
        if (Dm.gt(((DmList)known_skills[skill])[SKILL_LVL], old_level))
            S.level_gained(this, ((DmList)known_skills[skill])[SKILL_LVL], old_level, silent);
        else if (Dm.lt(((DmList)known_skills[skill])[SKILL_LVL], old_level))
            S.level_lost(this, ((DmList)known_skills[skill])[SKILL_LVL], old_level, silent);
    }

    ///Set experience of a specific skill to a number
    // tg: code/datums/mind/skills.dm:27
    public virtual void set_experience(object skill = null, object amt = null, float? silent = null)
    {
        silent ??= FALSE;
        object old_level = ((DmList)known_skills[skill])[SKILL_EXP];
        ((DmList)known_skills[skill])[SKILL_EXP] = amt;
        adjust_experience(skill, 0, silent, Dm.num_or_null(old_level)); //Make a call to adjust_experience to handle updating level
    }

    ///Set level of a specific skill
    // tg: code/datums/mind/skills.dm:33
    public virtual void set_level(object skill = null, object newlevel = null, float? silent = null)
    {
        silent ??= FALSE;
        object oldlevel = get_skill_level(skill);
        object difference = Dm.sub(SKILL_EXP_LIST()[newlevel], SKILL_EXP_LIST()[oldlevel]);
        adjust_experience(skill, difference, silent);
    }

    ///Check what the current skill level is based on that skill's exp
    // tg: code/datums/mind/skills.dm:39
    public virtual object update_skill_level(object skill = null)
    {
        float i = 0;
        foreach (object exp in Dm.each(SKILL_EXP_LIST()))
        {
            i++;
            if (Dm.ge(((DmList)known_skills[skill])[SKILL_EXP], SKILL_EXP_LIST()[i]))
                continue;
            return i - 1; //Return level based on the last exp requirement that we were greater than
        }
        return i; //If we had greater EXP than even the last exp threshold, we return the last level
    }

    ///Gets the skill's singleton and returns the result of its get_skill_modifier
    // tg: code/datums/mind/skills.dm:49
    public virtual object get_skill_modifier(object skill = null, object modifier = null)
    {
        datum_skill S = GetSkillRef(skill) as datum_skill;
        return S.get_skill_modifier(modifier, ((DmList)known_skills[skill])[SKILL_LVL]);
    }

    ///Gets the player's current level number from the relevant skill
    // tg: code/datums/mind/skills.dm:54
    public virtual object get_skill_level(object skill = null)
    {
        return ((DmList)known_skills[skill])[SKILL_LVL];
    }

    ///Gets the player's current exp from the relevant skill
    // tg: code/datums/mind/skills.dm:58
    public virtual object get_skill_exp(object skill = null)
    {
        return ((DmList)known_skills[skill])[SKILL_EXP];
    }

    // tg: code/datums/mind/skills.dm:61
    public virtual object get_skill_level_name(object skill = null)
    {
        object level = get_skill_level(skill);
        return SSskills.level_names[level];
    }

    // tg: code/datums/mind/skills.dm:65
    public virtual void print_levels(object user = null)
    {
        DmList shown_skills = Dm.list();
        foreach (object i in Dm.each(known_skills))
        {
            if (Dm.gt(((DmList)known_skills[i])[SKILL_LVL], SKILL_LEVEL_NONE)) //Do we actually have a level in this?
                Dm.append(ref shown_skills, i);
        }
        if (!Dm.truthy(length(shown_skills)))
        {
            to_chat(user, span_notice(I18n.T("You don't seem to have any particularly outstanding skills.")));
            return;
        }
        string msg = I18n.T("[]\n<span class='notice'>", span_info(I18n.T("<EM>Your skills</EM>")));
        foreach (object i in Dm.each(shown_skills))
        {
            Type the_skill = i as Type;
            msg += I18n.T("[] - []\n", Dm.initial(the_skill, (datum_skill p) => p.name), get_skill_level_name(the_skill));
        }
        msg += "</span>";
        to_chat(user, boxed_message(msg));
    }
}
