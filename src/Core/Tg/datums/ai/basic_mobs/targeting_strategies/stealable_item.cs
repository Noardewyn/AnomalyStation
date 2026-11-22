// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/stealable_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts visible items that aren't anchored or already being pulled.
// tg: code/datums/ai/basic_mobs/targeting_strategies/stealable_item.dm:3
public partial class datum_targeting_strategy_pickup_item_stealable_item : datum_targeting_strategy_pickup_item
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/stealable_item.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not obj_item candidate || Dm.truthy(candidate.anchored) || Dm.truthy(candidate.pulledby))
            return FALSE;
        return can_see(living_mob, candidate, vision_range);
    }
}
