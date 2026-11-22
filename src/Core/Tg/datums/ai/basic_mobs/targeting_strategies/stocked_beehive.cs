// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/stocked_beehive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file (a space bear raids an apiary).

/// Accepts beehives that contain at least one honeycomb and are visible to the pawn.
// tg: code/datums/ai/basic_mobs/targeting_strategies/stocked_beehive.dm:2
public partial class datum_targeting_strategy_stocked_beehive : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/stocked_beehive.dm:4
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        obj_structure_beebox candidate = target as obj_structure_beebox;
        if (candidate == null || !Dm.truthy(Dm.length(candidate.honeycombs)))
            return FALSE;
        return can_see(living_mob, candidate, vision_range);
    }
}
