// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/decorated_donut.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts decorated donuts that are visible to the pawn.
// tg: code/datums/ai/basic_mobs/targeting_strategies/decorated_donut.dm:3
public partial class datum_targeting_strategy_decorated_donut : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/decorated_donut.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not obj_item_food_donut candidate || !Dm.truthy(candidate.is_decorated))
            return FALSE;
        return can_see(living_mob, candidate, vision_range);
    }
}
