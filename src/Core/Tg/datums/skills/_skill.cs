// Translated from tgstation (snapshot 1d99bd7f): code/datums/skills/_skill.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file: the skill singletons' modifiers, the level up and level down messages and the legendary
// level's status symbol (the skill cape, sent by a supply pod). The var blocks are generated (D111).

public partial class datum_controller_global_vars
{
    // tg: code/datums/skills/_skill.dm:1  GLOBAL_LIST_INIT(skill_types, subtypesof(/datum/skill))
    public DmList skill_types;

    // tg: code/datums/skills/_skill.dm:1  (GLOBAL_MANAGED(skill_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalskill_types()
    {
        skill_types = subtypesof(typeof(datum_skill));
        Dm.append(ref gvars_datum_init_order, "skill_types");
    }
}

// tg: code/datums/skills/_skill.dm:3
public partial class datum_skill : datum
{
    // tg: code/datums/skills/_skill.dm:16
    public virtual object get_skill_modifier(object modifier = null, object level = null)
    {
        return ((DmList)modifiers[modifier])[level]; //Levels range from 1 (None) to 7 (Legendary)
    }

    /**
     * new: sets up some lists.
     *
     *Can't happen in the datum's definition because these lists are not constant expressions
     */
    // tg: code/datums/skills/_skill.dm:23
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        levelUpMessages = Dm.list(span_nicegreen(I18n.T("What the hell is []? Tell an admin if you see this message.", name)), //This first index shouldn't ever really be used
            span_nicegreen(I18n.T("I'm starting to figure out what [] really is!", name)),
            span_nicegreen(I18n.T("I'm getting a little better at []!", name)),
            span_nicegreen(I18n.T("I'm getting much better at []!", name)),
            span_nicegreen(I18n.T("I feel like I've become quite proficient at []!", name)),
            span_nicegreen(I18n.T("After lots of practice, I've begun to truly understand the intricacies and surprising depth behind []. I now consider myself a master [].", name, title)),
            span_nicegreen(I18n.T("Through incredible determination and effort, I've reached the peak of my [] abiltities. I'm finally able to consider myself a legendary []!", name, title)));
        levelDownMessages = Dm.list(span_nicegreen(I18n.T("I have somehow completely lost all understanding of []. Please tell an admin if you see this.", name)),
            span_nicegreen(I18n.T("I'm starting to forget what [] really even is. I need more practice...", name)),
            span_nicegreen(I18n.T("I'm getting a little worse at []. I'll need to keep practicing to get better at it...", name)),
            span_nicegreen(I18n.T("I'm getting a little worse at []...", name)),
            span_nicegreen(I18n.T("I'm losing my [] expertise ....", name)),
            span_nicegreen(I18n.T("I feel like I'm losing my mastery of [].", name)),
            span_nicegreen(I18n.T("I feel as though my legendary [] skills have deteriorated. I'll need more intense training to recover my lost skills.", name)));
    }

    /**
     * level_gained: Gives skill levelup messages to the user
     *
     * Only fires if the xp gain isn't silent, so only really useful for messages.
     * Arguments:
     * * mind - The mind that you'll want to send messages
     * * new_level - The newly gained level. Can check the actual level to give different messages at different levels, see defines in skills.dm
     * * old_level - Similar to the above, but the level you had before levelling up.
     * * silent - Silences the announcement if TRUE
     */
    // tg: code/datums/skills/_skill.dm:50
    public virtual void level_gained(datum_mind mind = null, object new_level = null, object old_level = null, object silent = null)
    {
        if (Dm.truthy(silent))
            return;
        to_chat(mind.current, levelUpMessages[new_level]); //new_level will be a value from 1 to 6, so we get appropriate message from the 6-element levelUpMessages list
    }

    /**
     * level_lost: See level_gained, same idea but fires on skill level-down
     */
    // tg: code/datums/skills/_skill.dm:57
    public virtual void level_lost(datum_mind mind = null, object new_level = null, object old_level = null, object silent = null)
    {
        if (Dm.truthy(silent))
            return;
        to_chat(mind.current, levelDownMessages[old_level]); //old_level will be a value from 1 to 6, so we get appropriate message from the 6-element levelUpMessages list
    }

    /**
     * try_skill_reward: Checks to see if a user is eligable for a tangible reward for reaching a certain skill level
     *
     * Currently gives the user a special cloak when they reach a legendary level at any given skill
     * Arguments:
     * * mind - The mind that you'll want to send messages and rewards to
     * * new_level - The current level of the user. Used to check if it meets the requirements for a reward
     */
    // tg: code/datums/skills/_skill.dm:70
    public virtual async DmTask try_skill_reward(datum_mind mind = null, object new_level = null)
    {
        if (!Dm.eq(new_level, SKILL_LEVEL_LEGENDARY))
            return;
        if (!ispath(skill_item_path))
        {
            to_chat(mind.current, span_nicegreen(I18n.T("My legendary [] skill is quite impressive, though it seems the Professional [] Association doesn't have any status symbols to commemorate my abilities with. I should let Centcom know of this travesty, maybe they can do something about it.", name, title)));
            return;
        }
        if (Dm.truthy(LAZYFIND(mind.skills_rewarded, type)))
        {
            to_chat(mind.current, span_nicegreen(I18n.T("It seems the Professional [] Association won't send me another status symbol.", title)));
            return;
        }
        await podspawn(Dm.list(
            Dm.kv("target", get_turf(mind.current)),
            Dm.kv("style", typeof(datum_pod_style_advanced)),
            Dm.kv("spawn", skill_item_path),
            Dm.kv("delays", Dm.list(Dm.kv(POD_TRANSIT, 150), Dm.kv(POD_FALLING, 4), Dm.kv(POD_OPENING, 30), Dm.kv(POD_LEAVING, 30)))));
        to_chat(mind.current, span_nicegreen(I18n.T("My legendary skill has attracted the attention of the Professional [] Association. It seems they are sending me a status symbol to commemorate my abilities.", title)));
        LAZYADD(ref mind.skills_rewarded, type);
    }
}
