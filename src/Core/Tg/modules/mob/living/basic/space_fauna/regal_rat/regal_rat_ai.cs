// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/// Only use the ability when we don't have it active
// tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_ai.dm:11
public partial class datum_bt_node_ai_behavior_use_mob_ability_domain : datum_bt_node_ai_behavior_use_mob_ability
{
    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_ai.dm:13
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (controller.blackboard[ability_key] is not datum_action_cooldown_mob_cooldown_domain domain || Dm.truthy(domain.is_active))
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
        return base.perform(seconds_per_tick, controller);
    }
}
