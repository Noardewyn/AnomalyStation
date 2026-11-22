// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/hauntium/haunted_bt_nodes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file, the haunted item's behaviour tree leaves (the tree is tg's haunted.bt.json). tg declares the held
// decorator's type as /datum/bt_node/decaorator/item_being_held (a typo) and its procs on /datum/bt_node/decorator/item_being_held,
// which the procs' path makes; the tree names the latter. The var blocks are generated (D111).

/// Gates on whether the haunted item pawn is currently inside a mob's inventory.
// tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:4
public partial class datum_bt_node_decorator_item_being_held : datum_bt_node_decorator
{
    // tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:4
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return ismob(((atom)controller.pawn).loc) ? TRUE : FALSE;
    }

    // tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:7
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(COMSIG_ITEM_ENTERED_HANDS, COMSIG_ITEM_DROPPED), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:11
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_ITEM_ENTERED_HANDS, COMSIG_ITEM_DROPPED));
    }
}

/**
 * Attempts to slip out of the holder's hands with a per-tick probability.
 * Returns RUNNING while waiting for the chance to fire; SUCCEEDED after escaping.
 */
// tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:18
public partial class datum_bt_node_ai_behavior_item_escape_grasp : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:20
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!SPT_PROB(HAUNTED_ITEM_ESCAPE_GRASP_CHANCE, seconds_per_tick))
            return AI_BEHAVIOR_INSTANT;
        var item_pawn = (obj_item)controller.pawn;
        var item_holder = item_pawn.loc as mob;
        if (!ismob(item_holder))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);
        item_pawn.visible_message(span_warning(I18n.T("[] slips out of the hands of []!", item_pawn, item_holder)));
        Waiting.NoSleep(item_holder.dropItemToGround(item_pawn, TRUE));
        return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_SUCCEEDED);
    }
}

///Find someone to haunt
// tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:32
public partial class datum_bt_node_ai_behavior_pick_haunt_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:36
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!prob(HAUNTED_ITEM_ATTACK_HAUNT_CHANCE))
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
        var item_pawn = (obj_item)controller.pawn;
        var to_haunt = controller.blackboard[haunt_list_key] as DmList;
        foreach (mob_living candidate in Dm.each_cast<mob_living>(to_haunt?.Copy()))
        {
            if (QDELETED(candidate) || Dm.num(to_haunt[candidate]) <= 0)
            {
                controller.remove_thing_from_blackboard_key(haunt_list_key, candidate);
                continue;
            }
            if (Dm.num(get_dist(candidate, item_pawn)) <= CURSED_VIEW_RANGE)
            {
                controller.set_blackboard_key(target_key, candidate);
                return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
            }
        }
        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
    }
}

/// Haunted variant: decrements haunt list aggro when throws are exhausted.
/// BT args: target_key, throw_count_key, haunt_list_key
// tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:52
public partial class datum_bt_node_ai_behavior_throw_attack_haunted : datum_bt_node_ai_behavior_throw_attack
{
    // tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:58
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        active_haunt_list_key = haunt_list_key;
        return base.perform(seconds_per_tick, controller);
    }

    // tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:62
    public override object on_throws_exhausted(datum_ai_controller controller = null, atom throw_target = null, object target_key = null, object throw_count_key = null)
    {
        controller.add_blackboard_key_assoc(active_haunt_list_key, throw_target, -1);
        return base.on_throws_exhausted(controller, throw_target, target_key, throw_count_key);
    }
}

///Teleport every now and then
// tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:68
public partial class datum_bt_node_ai_behavior_idle_ghost_item : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/hauntium/haunted_bt_nodes.dm:73
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        base.perform(seconds_per_tick, controller);
        var item_pawn = (obj_item)controller.pawn;
        if (ismob(item_pawn.loc)) //Being held. dont teleport
            return AI_BEHAVIOR_INSTANT;
        if (SPT_PROB(teleport_chance, seconds_per_tick))
        {
            playsound(item_pawn.loc, "sound/items/haunted/ghostitemattack.ogg", 100, TRUE);
            Waiting.NoSleep(do_teleport(item_pawn, get_turf(item_pawn) as atom, 4, channel: TELEPORT_CHANNEL_MAGIC));
        }
        return AI_BEHAVIOR_INSTANT;
    }
}
