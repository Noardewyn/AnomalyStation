// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/hivebot/hivebot_behavior.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Says a random binary string to a hive partner. Movement to the partner is handled in the tree.
// tg: code/modules/mob/living/basic/space_fauna/hivebot/hivebot_behavior.dm:8
public partial class datum_bt_node_ai_behavior_relay_message : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/space_fauna/hivebot/hivebot_behavior.dm:14
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[target_key] as mob_living;
        mob_living living_pawn = controller.pawn as mob_living;

        if (QDELETED(target))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);
        string message_relayed = "";
        for (int i = 1; i <= length_of_message; i++)
            message_relayed += prob(50) ? "1" : "0";
        Waiting.Detach(living_pawn.say(message_relayed, forced: "AI Controller")); // INVOKE_ASYNC
        return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_SUCCEEDED);
    }
}

/// Repairs a damaged machine once in range.
// tg: code/modules/mob/living/basic/space_fauna/hivebot/hivebot_behavior.dm:28
public partial class datum_bt_node_ai_behavior_hunt_target_repair_machines : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/modules/mob/living/basic/space_fauna/hivebot/hivebot_behavior.dm:31
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        (hunter as mob_living_basic_hivebot_mechanic).repair_machine(hunted as obj_machinery);
        return null;
    }
}
