// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/slime/ai/behaviours.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///Slimes feed on what they hunt
// tg: code/modules/mob/living/basic/slime/ai/behaviours.dm:3
public partial class datum_bt_node_ai_behavior_hunt_target_interact_with_target_slime : datum_bt_node_ai_behavior_hunt_target_interact_with_target
{
    // tg: code/modules/mob/living/basic/slime/ai/behaviours.dm:5
    public override async DmTask<object> target_caught(mob_living hunter_ = null, atom hunted_ = null)
    {
        mob_living_basic_slime hunter = hunter_ as mob_living_basic_slime;
        mob_living hunted = hunted_ as mob_living;
        if (!Dm.truthy(hunter.can_feed_on(hunted))) // Target is no longer edible
        {
            await hunter.UnarmedAttack(hunted, TRUE);
            return null;
        }

        if (!Dm.eq(hunted.body_position, STANDING_UP) || prob(20)) //Not standing, or we rolled well? Feed.
        {
            await hunter.start_feeding(hunted);
            return null;
        }

        if (hunted.client != null && Dm.num(hunted.health) >= 20) //If target has a client and is healthy, punch them a bit before feasting
        {
            await hunter.UnarmedAttack(hunted, TRUE);
            return null;
        }

        await hunter.start_feeding(hunted);
        return null;
    }

    // tg: code/modules/mob/living/basic/slime/ai/behaviours.dm:18
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        mob_living_basic_slime slime_pawn = controller.pawn as mob_living_basic_slime;
        atom target = controller.blackboard[target_key] as atom;
        if (!Dm.truthy(slime_pawn.can_feed_on(target as mob_living)))
            controller.clear_blackboard_key(target_key);
    }
}
