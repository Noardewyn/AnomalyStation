// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/living_not_dead.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts living mobs that are not dead and visible to the pawn. Skips faction checks,
/// so it's suitable for finding allies (e.g. a hivebot looking for another hivebot).
// tg: code/datums/ai/basic_mobs/targeting_strategies/living_not_dead.dm:3
public partial class datum_targeting_strategy_living_not_dead : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/living_not_dead.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not mob_living candidate || candidate.stat == DEAD)
            return FALSE;
        return can_see(living_mob, candidate, vision_range);
    }
}
