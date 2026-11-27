// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/injured_raptor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// tg: code/datums/ai/basic_mobs/targeting_strategies/injured_raptor.dm:1
public partial class datum_targeting_strategy_injured_mob_not_self_injured_raptor : datum_targeting_strategy_injured_mob_not_self
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/injured_raptor.dm:3
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        if (!(target is mob_living_basic_raptor))
            return FALSE;
        return await base.is_valid_target(living_mob, target, vision_range, controller);
    }
}
