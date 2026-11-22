// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/conscious_human.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Targets non-incapable human carbons with a mind. Used for interaction targets (traders, etc.).
// tg: code/datums/ai/basic_mobs/targeting_strategies/conscious_human.dm:3
public partial class datum_targeting_strategy_capable_human : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/conscious_human.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!ishuman(target))
            return FALSE;
        mob_living_carbon_human human_target = (mob_living_carbon_human)target;
        if (Dm.truthy(human_target.incapacitated) || human_target.mind == null)
            return FALSE;
        return TRUE;
    }
}
