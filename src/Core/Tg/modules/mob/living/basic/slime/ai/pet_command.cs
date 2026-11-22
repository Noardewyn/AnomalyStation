// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/slime/ai/pet_command.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///Slimes feed on what they're told to attack
// tg: code/modules/mob/living/basic/slime/ai/pet_command.dm:2
public partial class datum_pet_command_attack_slime : datum_pet_command_attack
{
    // tg: code/modules/mob/living/basic/slime/ai/pet_command.dm:8
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        mob_living_basic_slime slime_pawn = controller.pawn as mob_living_basic_slime;
        if (isslime(slime_pawn) && Dm.truthy(slime_pawn.can_feed_on(controller.blackboard[BB_CURRENT_PET_TARGET] as mob_living, check_friendship: TRUE)))
        {
            await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_attack_slime));
            return null;
        }
        return await base.execute_action(controller);
    }
}
