// Translated from tgstation (snapshot 1d99bd7f): code/datums/skills/fishing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file: the skill and the fishing examine traits its levels give. The minigame that trains it and
// the readers of the traits are the fishing package's (not-translated.md 221). The var block is generated (D111).

/**
 * skill associated with the fishing feature. It modifies the fishing minigame difficulty
 * and is gained each time one is completed.
 */
// tg: code/datums/skills/fishing.dm:5
public partial class datum_skill_fishing : datum_skill
{
    // tg: code/datums/skills/fishing.dm:12
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        levelUpMessages[SKILL_LEVEL_NOVICE] = span_nicegreen(I18n.T("I'm starting to figure out what [] really is! I can guess a fish size and weight at a glance.", name));
        levelUpMessages[SKILL_LEVEL_APPRENTICE] = span_nicegreen(I18n.T("I'm getting a little better at []! I can tell if a fish is hungry, dying and otherwise.", name));
        levelUpMessages[SKILL_LEVEL_JOURNEYMAN] = span_nicegreen(I18n.T("I feel like I've become quite proficient at []! I can tell what fishes I can catch at any given fishing spot.", name));
        levelUpMessages[SKILL_LEVEL_MASTER] = span_nicegreen(I18n.T("I've begun to truly understand the surprising depth behind []. As a master [], I can guess what I'm going to catch now!", name, title));
    }

    // tg: code/datums/skills/fishing.dm:19
    public override void level_gained(datum_mind mind = null, object new_level = null, object old_level = null, object silent = null)
    {
        base.level_gained(mind, new_level, old_level, silent);
        if (Dm.ge(new_level, SKILL_LEVEL_NOVICE) && Dm.lt(old_level, SKILL_LEVEL_NOVICE))
            ADD_TRAIT(mind, TRAIT_EXAMINE_FISH, SKILL_TRAIT);
        if (Dm.ge(new_level, SKILL_LEVEL_APPRENTICE) && Dm.lt(old_level, SKILL_LEVEL_APPRENTICE))
            ADD_TRAIT(mind, TRAIT_EXAMINE_DEEPER_FISH, SKILL_TRAIT);
        if (Dm.ge(new_level, SKILL_LEVEL_JOURNEYMAN) && Dm.lt(old_level, SKILL_LEVEL_JOURNEYMAN))
            ADD_TRAIT(mind, TRAIT_EXAMINE_FISHING_SPOT, SKILL_TRAIT);
        if (Dm.ge(new_level, SKILL_LEVEL_MASTER) && Dm.lt(old_level, SKILL_LEVEL_MASTER))
            ADD_TRAIT(mind, TRAIT_REVEAL_FISH, SKILL_TRAIT);
    }

    // tg: code/datums/skills/fishing.dm:30
    public override void level_lost(datum_mind mind = null, object new_level = null, object old_level = null, object silent = null)
    {
        base.level_lost(mind, new_level, old_level, silent);
        if (Dm.ge(old_level, SKILL_LEVEL_MASTER) && Dm.lt(new_level, SKILL_LEVEL_MASTER))
            REMOVE_TRAIT(mind, TRAIT_REVEAL_FISH, SKILL_TRAIT);
        if (Dm.ge(old_level, SKILL_LEVEL_JOURNEYMAN) && Dm.lt(new_level, SKILL_LEVEL_JOURNEYMAN))
            REMOVE_TRAIT(mind, TRAIT_EXAMINE_FISHING_SPOT, SKILL_TRAIT);
        if (Dm.ge(old_level, SKILL_LEVEL_APPRENTICE) && Dm.lt(new_level, SKILL_LEVEL_APPRENTICE))
            REMOVE_TRAIT(mind, TRAIT_EXAMINE_DEEPER_FISH, SKILL_TRAIT);
        if (Dm.ge(old_level, SKILL_LEVEL_NOVICE) && Dm.lt(new_level, SKILL_LEVEL_NOVICE))
            REMOVE_TRAIT(mind, TRAIT_EXAMINE_FISH, SKILL_TRAIT);
    }
}
