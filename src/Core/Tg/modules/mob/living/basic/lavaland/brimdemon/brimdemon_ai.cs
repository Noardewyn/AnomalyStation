// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/brimdemon/brimdemon_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Brimdemon's beam only fires when the target is lined up on a cardinal direction.
// tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon_ai.dm:15
public partial class datum_bt_node_ai_behavior_targeted_mob_ability_brimbeam : datum_bt_node_ai_behavior_targeted_mob_ability
{
    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon_ai.dm:15
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(target) || !Dm.@in(get_dir(controller.pawn, target), GLOB.cardinals))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        return base.perform(seconds_per_tick, controller);
    }
}
