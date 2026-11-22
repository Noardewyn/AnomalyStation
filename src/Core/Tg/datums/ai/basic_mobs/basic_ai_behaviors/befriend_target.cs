// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_ai_behaviors/befriend_target.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/befriend_target.dm:1
public partial class datum_bt_node_ai_behavior_befriend_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/befriend_target.dm:7
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        mob_living living_target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(living_target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (!Dm.truthy(long_range_friendship) && get_dist(living_pawn, living_target) > 1)
            return AI_BEHAVIOR_INSTANT;
        Waiting.NoSleep(living_pawn.befriend(living_target));
        object befriend_text = controller.blackboard[befriend_message];
        if (Dm.truthy(befriend_text))
            to_chat(living_target, span_nicegreen(Dm.text("[] []", living_pawn, befriend_text)));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/befriend_target.dm:19
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (Dm.truthy(forget_target))
            controller.clear_blackboard_key(target_key);
    }
}
