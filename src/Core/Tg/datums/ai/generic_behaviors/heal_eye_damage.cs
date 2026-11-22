// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/heal_eye_damage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Heals the eye damage of the keyed target. Movement to the target is handled externally.
// tg: code/datums/ai/generic_behaviors/heal_eye_damage.dm:2
public partial class datum_bt_node_ai_behavior_heal_eye_damage : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/heal_eye_damage.dm:6
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        return QDELETED(controller.blackboard[target_key] as datum) ? FALSE : TRUE;
    }

    // tg: code/datums/ai/generic_behaviors/heal_eye_damage.dm:10
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_carbon target = controller.blackboard[target_key] as mob_living_carbon;
        if (QDELETED(target))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);

        mob_living_basic_eyeball eye = controller.pawn as mob_living_basic_eyeball;
        obj_item_organ_eyes eyes = target.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        Waiting.NoSleep(eye.heal_eye_damage(target, eyes));
        return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_SUCCEEDED);
    }

    // tg: code/datums/ai/generic_behaviors/heal_eye_damage.dm:20
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}
