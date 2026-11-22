// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_ai_behaviors/call_reinforcements.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Emotes a reinforcement call and alerts nearby faction members, adding the current target to their retaliate lists.
/// Returns FAILURE when there is no valid target or the target is a friend.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/call_reinforcements.dm:3
public partial class datum_bt_node_ai_behavior_call_reinforcements : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/call_reinforcements.dm:9
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (controller.blackboard[target_key] is not mob target)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        mob pawn_mob = controller.pawn as mob;
        DmList friends = controller.blackboard[BB_FRIENDS_LIST] as DmList;
        if (Dm.truthy(friends) && Dm.@in(target, friends))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        object say_text = controller.blackboard[BB_REINFORCEMENTS_SAY];
        if (say_text != null)
            Waiting.NoSleep(pawn_mob.say(say_text, forced: "AI Controller"));
        else
        {
            object emote_text = controller.blackboard[BB_REINFORCEMENTS_EMOTE];
            if (emote_text != null)
                pawn_mob.manual_emote(emote_text);
        }

        foreach (mob other_mob in Dm.each<mob>(oview(reinforcements_range, pawn_mob)))
        {
            if (!Dm.truthy(pawn_mob.faction_check_atom(other_mob)) || other_mob.ai_controller == null)
                continue;
            datum_ai_controller other_controller = (datum_ai_controller)other_mob.ai_controller;
            other_controller.set_blackboard_key_assoc_lazylist(BB_BASIC_MOB_RETALIATE_LIST, target, world.time);
            other_controller.set_blackboard_key(BB_BASIC_MOB_REINFORCEMENT_TARGET, pawn_mob);
        }

        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Mining/swarm variant: boosts priority rather than forcing retaliation, shorter range and faster cooldown.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/call_reinforcements.dm:38
public partial class datum_bt_node_ai_behavior_call_reinforcements_mining : datum_bt_node_ai_behavior_call_reinforcements
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/call_reinforcements.dm:41
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (controller.blackboard[target_key] is not mob target)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        mob pawn_mob = controller.pawn as mob;
        foreach (mob other_mob in Dm.each<mob>(oview(reinforcements_range, pawn_mob)))
        {
            if (!Dm.truthy(pawn_mob.faction_check_atom(other_mob)) || other_mob.ai_controller == null)
                continue;
            datum_ai_controller other_controller = (datum_ai_controller)other_mob.ai_controller;
            DmList existing_requests = other_controller.blackboard[BB_MINING_MOB_REINFORCEMENTS_REQUESTS] as DmList;
            if (!Dm.truthy(existing_requests) || !Dm.truthy(existing_requests[target]))
                other_controller.set_blackboard_key_assoc_lazylist(BB_MINING_MOB_REINFORCEMENTS_REQUESTS, target, Dm.list());
            other_controller.add_blackboard_key_assoc(BB_MINING_MOB_REINFORCEMENTS_REQUESTS, target, world.time);
        }
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}
