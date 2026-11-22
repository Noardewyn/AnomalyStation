// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/huntable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts any visible target that isn't a dead mob. Mirrors the legacy hunt finder's validity check.
// tg: code/datums/ai/basic_mobs/targeting_strategies/huntable.dm:3
public partial class datum_targeting_strategy_huntable : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/huntable.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is mob_living living_target && living_target.stat == DEAD)
            return FALSE;
        return can_see(living_mob, target, vision_range);
    }
}
