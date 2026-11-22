// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_ai_behaviors/use_mob_ability.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file but the shapeshift variant (its spell is inert, with its host) (gaps package C1).

/// Triggers a mob ability stored in a blackboard key. Returns INSTANT SUCCESS if triggered, INSTANT FAILURE if unavailable or trigger fails.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/use_mob_ability.dm:2
public partial class datum_bt_node_ai_behavior_use_mob_ability : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/use_mob_ability.dm:5
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        datum_action using_action = Waiting.NoSleep(get_valid_ability(controller)) as datum_action;
        if (QDELETED(using_action))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        return start_async();
    }

    /// Returns the action to trigger, or null if it isn't available. Override to prep the action before it fires.
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/use_mob_ability.dm:16
    public virtual async DmTask<object> get_valid_ability(datum_ai_controller controller = null)
    {
        datum_action using_action = controller.blackboard[ability_key] as datum_action;
        if (QDELETED(using_action) || !Dm.truthy(await using_action.IsAvailable()))
            return null;
        return using_action;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/use_mob_ability.dm:22
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        datum_action using_action = controller.blackboard[ability_key] as datum_action;
        object result = await using_action.Trigger();
        if (!Dm.truthy(async_still_valid()))
            return null;
        finish_async(Dm.truthy(result) ? AI_BEHAVIOR_SUCCEEDED : AI_BEHAVIOR_FAILED);
        return null;
    }
}
