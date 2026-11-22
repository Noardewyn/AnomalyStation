// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/damaged_eyes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts carbon mobs whose eyes are damaged at or beyond the controller's BB_EYE_DAMAGE_THRESHOLD.
// tg: code/datums/ai/basic_mobs/targeting_strategies/damaged_eyes.dm:3
public partial class datum_targeting_strategy_damaged_eyes : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/damaged_eyes.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!iscarbon(target))
            return FALSE;
        object threshold = controller?.blackboard[BB_EYE_DAMAGE_THRESHOLD];
        if (!Dm.truthy(threshold))
            return FALSE;
        mob_living_carbon carbon_target = (mob_living_carbon)target;
        obj_item_organ_eyes eyes = carbon_target.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes == null || Dm.num(eyes.damage) < Dm.num(threshold))
            return FALSE;
        return TRUE;
    }
}
