// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/spin_web.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Trigger a web-spinning action at the current web target turf.
/// Expects move_to_target to have positioned the spider first.
/// Clears BB_SPIDER_WEB_TARGET on finish.
// tg: code/datums/ai/generic_behaviors/spin_web.dm:4
public partial class datum_bt_node_ai_behavior_spin_web : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/spin_web.dm:11
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        if (!Dm.truthy(controller.blackboard_key_exists(action_key)) || !Dm.truthy(controller.blackboard_key_exists(target_key)))
            return FALSE;
        return await base.setup(controller);
    }

    // tg: code/datums/ai/generic_behaviors/spin_web.dm:16
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        datum_action_cooldown web_action = controller.blackboard[action_key] as datum_action_cooldown;
        if (web_action == null)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        return start_async();
    }

    // tg: code/datums/ai/generic_behaviors/spin_web.dm:26
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        datum_action_cooldown web_action = controller.blackboard[action_key] as datum_action_cooldown;
        object result = await web_action.Trigger();
        if (!Dm.truthy(async_still_valid()))
            return null;
        finish_async(Dm.truthy(result) ? AI_BEHAVIOR_SUCCEEDED : AI_BEHAVIOR_FAILED);
        return null;
    }

    // tg: code/datums/ai/generic_behaviors/spin_web.dm:33
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}
