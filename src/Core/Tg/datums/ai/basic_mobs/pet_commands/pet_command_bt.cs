// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/pet_commands/pet_command_bt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1): the pet command leaves C2 builds on.

// Pet-command-specific BT behaviors and override subtrees.
// Generic leaf behaviors (wait, play_dead, pick_up_item_virtual, pass_item_virtual, ai_interact) live in basic_ai_behaviors/.

/// Validates a protect_owner target; clears command + target if invalid.
// tg: code/datums/ai/basic_mobs/pet_commands/pet_command_bt.dm:5
public partial class datum_bt_node_ai_behavior_protect_owner_check : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/pet_commands/pet_command_bt.dm:7
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living victim = controller.blackboard[BB_CURRENT_PET_TARGET] as mob_living;
        if (QDELETED(victim))
        {
            controller.clear_blackboard_key(BB_ACTIVE_PET_COMMAND);
            controller.clear_blackboard_key(BB_CURRENT_PET_TARGET);
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        }
        datum_targeting_strategy targeter = GET_TARGETING_STRATEGY(controller.blackboard[BB_PET_TARGETING_STRATEGY]);
        if (targeter == null || !Dm.truthy(Waiting.NoSleep(targeter.is_valid_target(controller.pawn as mob_living, victim))))
        {
            controller.clear_blackboard_key(BB_ACTIVE_PET_COMMAND);
            controller.clear_blackboard_key(BB_CURRENT_PET_TARGET);
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        }
        object minimum_stat = controller.blackboard[BB_TARGET_MINIMUM_STAT];
        float checked_stat = victim.stat;
        if (Dm.truthy(controller.blackboard[BB_TREAT_UNCONSCIOUS_AS_HARDCRIT]) && IS_UNCONSCIOUS(victim))
            checked_stat = max(checked_stat, HARD_CRIT);

        if ((isnum(minimum_stat) && checked_stat > Dm.num(minimum_stat)) || victim == controller.pawn)
        {
            controller.clear_blackboard_key(BB_ACTIVE_PET_COMMAND);
            controller.clear_blackboard_key(BB_CURRENT_PET_TARGET);
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        }
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Validates a fetch item at target_key; adds to ignore list and clears keys on failure.
// tg: code/datums/ai/basic_mobs/pet_commands/pet_command_bt.dm:32
public partial class datum_bt_node_ai_behavior_fetch_seek : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/pet_commands/pet_command_bt.dm:35
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        obj_item target = controller.blackboard[target_key] as obj_item;
        return Dm.num(!QDELETED(target));
    }

    // tg: code/datums/ai/basic_mobs/pet_commands/pet_command_bt.dm:39
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        obj_item fetch_thing = controller.blackboard[target_key] as obj_item;
        if (QDELETED(fetch_thing))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (Dm.truthy(fetch_thing.anchored))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/basic_mobs/pet_commands/pet_command_bt.dm:46
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (Dm.truthy(succeeded))
            return;
        obj_item target = controller.blackboard[target_key] as obj_item;
        if (target != null)
            controller.set_blackboard_key_assoc_lazylist(BB_FETCH_IGNORE_LIST, target, TRUE);
        controller.clear_blackboard_key(target_key);
        controller.clear_blackboard_key(BB_FETCH_DELIVER_TO);
    }
}

/// Clears the fetch ignore list at most once per AI_FETCH_IGNORE_DURATION. Always succeeds.
// tg: code/datums/ai/basic_mobs/pet_commands/pet_command_bt.dm:56
public partial class datum_bt_node_ai_behavior_forget_failed_fetches : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/pet_commands/pet_command_bt.dm:59
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (clear_cooldown <= world.time && Dm.truthy(LAZYLEN(controller.blackboard[BB_FETCH_IGNORE_LIST]))) // COOLDOWN_FINISHED, inline (rules 11.1)
        {
            clear_cooldown = world.time + AI_FETCH_IGNORE_DURATION; // COOLDOWN_START, inline
            controller.clear_blackboard_key(BB_FETCH_IGNORE_LIST);
        }
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Clears BB_ACTIVE_PET_COMMAND and removes the SUBPLAN_ID_PET_COMMAND override.
// tg: code/datums/ai/basic_mobs/pet_commands/pet_command_bt.dm:66
public partial class datum_bt_node_ai_behavior_clear_pet_command : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/pet_commands/pet_command_bt.dm:68
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        controller.clear_blackboard_key(BB_ACTIVE_PET_COMMAND);
        Waiting.NoSleep(controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, null));
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}
