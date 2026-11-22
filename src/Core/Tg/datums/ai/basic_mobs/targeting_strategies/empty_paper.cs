// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/empty_paper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6; the paper's is_empty came with the paperwork, gaps S3).

/// Accepts pieces of paper that have nothing written on them yet.
// tg: code/datums/ai/basic_mobs/targeting_strategies/empty_paper.dm:2
public partial class datum_targeting_strategy_empty_paper : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/empty_paper.dm:4
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        obj_item_paper candidate = target as obj_item_paper;
        if (candidate == null || !Dm.truthy(candidate.is_empty()))
            return FALSE;
        return TRUE;
    }
}
