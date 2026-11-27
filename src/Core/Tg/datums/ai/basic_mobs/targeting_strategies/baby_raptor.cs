// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/baby_raptor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


/// Accepts visible, living baby raptors. ONLY USE ON BABY RAPTORS WE ASSUME IT IS ONE
// tg: code/datums/ai/basic_mobs/targeting_strategies/baby_raptor.dm:2
public partial class datum_targeting_strategy_healthy_raptor_baby : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/baby_raptor.dm:4
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        mob_living_basic_raptor candidate = target as mob_living_basic_raptor;
        if (candidate.stat == DEAD || !Dm.eq(candidate.growth_stage, RAPTOR_BABY))
            return FALSE;
        return can_see(living_mob, candidate, vision_range);
    }
}
