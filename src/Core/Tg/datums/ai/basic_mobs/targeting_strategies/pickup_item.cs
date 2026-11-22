// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/pickup_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Ensures the item is actually on a turf, else we can't go there to pick it up!
// tg: code/datums/ai/basic_mobs/targeting_strategies/pickup_item.dm:3
public partial class datum_targeting_strategy_pickup_item : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/pickup_item.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!isturf(target.loc) && !Dm.truthy(living_mob.is_holding(target as obj_item)))
            return FALSE;
        return TRUE;
    }
}
