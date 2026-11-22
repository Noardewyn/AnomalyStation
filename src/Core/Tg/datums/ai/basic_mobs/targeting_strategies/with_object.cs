// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/with_object.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Find mobs who are holding the bb configurable object type
/// (an extension of basic targeting behaviour, that allows you to only target the mob if they have a specific item in their hand).
// tg: code/datums/ai/basic_mobs/targeting_strategies/with_object.dm:8
public partial class datum_targeting_strategy_basic_holding_object : datum_targeting_strategy_basic
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/with_object.dm:10
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        datum_ai_controller our_controller = living_mob.ai_controller as datum_ai_controller;
        object object_type_path = our_controller.blackboard[target_item_key];

        if (object_type_path == null)
            return FALSE; // no op
        if (!ismob(target))
            return FALSE; // no hands no problems

        // Look at me, type casting like a grown up
        mob targetmob = (mob)target;
        // Check if our parent behaviour agrees we can attack this target (we ignore faction by default)
        object is_valid_target = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (Dm.truthy(is_valid_target) && Dm.truthy(targetmob.is_holding_item_of_type(object_type_path)))
            return TRUE; // they have the item
        // No valid target
        return FALSE;
    }
}
