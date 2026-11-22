// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/legged_conscious_human.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts conscious humans who have at least one leg and are visible to the pawn.
// tg: code/datums/ai/basic_mobs/targeting_strategies/legged_conscious_human.dm:3
public partial class datum_targeting_strategy_legged_conscious_human : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/legged_conscious_human.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not mob_living_carbon_human candidate || IS_UNCONSCIOUS_OR_CRIT(candidate))
            return FALSE;
        if (candidate.get_bodypart(BODY_ZONE_R_LEG) == null && candidate.get_bodypart(BODY_ZONE_L_LEG) == null)
            return FALSE;
        return can_see(living_mob, candidate, vision_range);
    }
}
