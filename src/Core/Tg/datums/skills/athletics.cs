// Translated from tgstation (snapshot 1d99bd7f): code/datums/skills/athletics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The var block is generated (D111).

// tg: code/datums/skills/athletics.dm:1
public partial class datum_skill_athletics : datum_skill
{
    // tg: code/datums/skills/athletics.dm:29
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        levelUpMessages[SKILL_LEVEL_NOVICE] = span_nicegreen(I18n.T("I am just getting started on my [] journey! I think I should be able to identify other people who are working to improve their body by sight.", name));
    }

    // tg: code/datums/skills/athletics.dm:33
    public override void level_gained(datum_mind mind = null, object new_level = null, object old_level = null, object silent = null)
    {
        base.level_gained(mind, new_level, old_level, silent);
        if (Dm.ge(new_level, SKILL_LEVEL_NOVICE) && Dm.lt(old_level, SKILL_LEVEL_NOVICE))
            ADD_TRAIT(mind, TRAIT_EXAMINE_FITNESS, SKILL_TRAIT);
    }

    // tg: code/datums/skills/athletics.dm:38
    public override void level_lost(datum_mind mind = null, object new_level = null, object old_level = null, object silent = null)
    {
        base.level_lost(mind, new_level, old_level, silent);
        if (Dm.ge(old_level, SKILL_LEVEL_NOVICE) && Dm.lt(new_level, SKILL_LEVEL_NOVICE))
            REMOVE_TRAIT(mind, TRAIT_EXAMINE_FITNESS, SKILL_TRAIT);
    }
}
