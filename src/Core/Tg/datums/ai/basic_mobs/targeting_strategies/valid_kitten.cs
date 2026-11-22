// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/valid_kitten.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts living kittens that don't already have huntable food sitting next to them.
/// Reads the prey typecache from BB_HUNTABLE_PREY on the searching controller.
// tg: code/datums/ai/basic_mobs/targeting_strategies/valid_kitten.dm:3
public partial class datum_targeting_strategy_valid_kitten : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/valid_kitten.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not mob_living kitten || kitten.stat == DEAD)
            return FALSE;
        DmList prey = controller?.blackboard[BB_HUNTABLE_PREY] as DmList;
        if (Dm.truthy(prey) && Dm.truthy(length(typecache_filter_list(oview(2, kitten), prey))))
            return FALSE;
        return TRUE;
    }
}
