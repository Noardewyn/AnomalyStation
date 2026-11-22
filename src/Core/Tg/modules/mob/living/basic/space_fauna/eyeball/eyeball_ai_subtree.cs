// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/eyeball/eyeball_ai_subtree.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/eyeball/eyeball_ai_subtree.dm:10
public partial class datum_targeting_strategy_basic_eyeball : datum_targeting_strategy_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/eyeball/eyeball_ai_subtree.dm:10
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not mob_living_carbon_human human_target)
            return TRUE;
        if (Dm.truthy(human_target.is_blind()))
            return FALSE;
        object eye_damage_threshold = (living_mob.ai_controller as datum_ai_controller).blackboard[BB_EYE_DAMAGE_THRESHOLD];
        if (!Dm.truthy(eye_damage_threshold))
            return TRUE;
        obj_item_organ_eyes eyes = human_target.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes == null) // a runtime in tg (no eyes)
            return TRUE;
        if (Dm.num(eyes.damage) > Dm.num(eye_damage_threshold)) //we dont attack people with bad vision
            return FALSE;

        return can_see(target, living_mob, 9) ; //if the target cant see us dont attack him
    }
}
