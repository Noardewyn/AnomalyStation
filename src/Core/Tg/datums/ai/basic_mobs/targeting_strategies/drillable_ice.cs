// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/drillable_ice.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts ice turfs that can have a hole made in them and are visible to the pawn.
/// Pair with a range_turfs source.
// tg: code/datums/ai/basic_mobs/targeting_strategies/drillable_ice.dm:3
public partial class datum_targeting_strategy_drillable_ice : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/drillable_ice.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not turf_open_misc_ice candidate || !Dm.truthy(candidate.can_make_hole))
            return FALSE;
        return can_see(living_mob, candidate, vision_range);
    }
}
