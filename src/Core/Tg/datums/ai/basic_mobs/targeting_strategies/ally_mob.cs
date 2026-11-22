// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/ally_mob.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts visible mobs that are allies of the pawn (and not the pawn itself).
// tg: code/datums/ai/basic_mobs/targeting_strategies/ally_mob.dm:3
public partial class datum_targeting_strategy_ally_mob : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/ally_mob.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target == living_mob || !Dm.truthy(living_mob.has_ally(target)))
            return FALSE;
        return can_see(living_mob, target, vision_range);
    }
}
