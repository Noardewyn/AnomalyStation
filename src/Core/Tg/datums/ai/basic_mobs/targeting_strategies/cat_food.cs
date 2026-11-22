// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/cat_food.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts visible food that has no kitten (other than the pawn) nearby.
/// Pair with a food typecache source (e.g. oview_typed/from_bb_key/basic_foods).
// tg: code/datums/ai/basic_mobs/targeting_strategies/cat_food.dm:3
public partial class datum_targeting_strategy_cat_food : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/cat_food.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        foreach (mob_living_basic_pet_cat_kitten nearby_kitten in Dm.each<mob_living_basic_pet_cat_kitten>(oview(2, target)))
        {
            if (nearby_kitten != living_mob)
                return FALSE;
            break;
        }
        return can_see(living_mob, target, vision_range);
    }
}
