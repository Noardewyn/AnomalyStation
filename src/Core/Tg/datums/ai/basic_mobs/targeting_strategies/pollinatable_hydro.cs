// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/pollinatable_hydro.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file (the bees find trays to pollinate).

/// Accepts visible hydroponics trays a bee can currently pollinate.
// tg: code/datums/ai/basic_mobs/targeting_strategies/pollinatable_hydro.dm:2
public partial class datum_targeting_strategy_pollinatable_hydro : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/pollinatable_hydro.dm:4
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        obj_machinery_hydroponics candidate = target as obj_machinery_hydroponics;
        if (candidate == null || !Dm.truthy(candidate.can_bee_pollinate()))
            return FALSE;
        return can_see(living_mob, candidate, vision_range);
    }
}
