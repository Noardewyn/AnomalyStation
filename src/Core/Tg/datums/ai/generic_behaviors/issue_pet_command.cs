// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/issue_pet_command.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/**
 * Issues a spoken pet command and points at the target.
 * Requires a blackboard key holding a list of /datum/pet_command instances.
 * Use a cooldown decorator in the tree  time_between_perform has no effect on one-shot behaviors.
 */
// tg: code/datums/ai/generic_behaviors/issue_pet_command.dm:8
public partial class datum_bt_node_ai_behavior_issue_pet_command : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/issue_pet_command.dm:18
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        object dot = await base.setup(controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (Dm.truthy(commandable_mob_type))
        {
            if (Dm.locate_type((Type)commandable_mob_type, oview(command_distance, controller.pawn)) == null)
                return FALSE;
        }
        atom target = controller.blackboard[target_key] as atom;
        return !QDELETED(target) ? TRUE : FALSE;
    }

    // tg: code/datums/ai/generic_behaviors/issue_pet_command.dm:28
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic living_pawn = controller.pawn as mob_living_basic;
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        DmList commands = controller.blackboard[command_list_key] as DmList;
        if (!Dm.truthy(length(commands)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        datum_pet_command cmd = Dm.locate_type((Type)command_type, commands) as datum_pet_command;
        if (cmd == null)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        Waiting.Detach(living_pawn.say(pick(cmd.speech_commands), forced: "controller"));
        Waiting.Detach(living_pawn._pointed(target));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
