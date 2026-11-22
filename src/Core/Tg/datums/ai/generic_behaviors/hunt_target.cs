// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/hunt_target.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Performs an action on a blackboard-keyed hunt target once in range.
/// Movement to the target is handled externally via a move_to_target leaf.
// tg: code/datums/ai/generic_behaviors/hunt_target.dm:3
public partial class datum_bt_node_ai_behavior_hunt_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:15
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        atom hunted = controller.blackboard[target_key] as atom;
        return Dm.num(!QDELETED(hunted));
    }

    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:19
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom hunted = controller.blackboard[target_key] as atom;
        if (QDELETED(hunted))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        Waiting.NoSleep(target_caught(controller.pawn as mob_living, hunted));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:26
    public virtual async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        if (isliving(hunted))
        {
            mob_living living_target = (mob_living)hunted;
            hunter.manual_emote(I18n.T("chomps []!", living_target));
            living_target.investigate_log(Dm.text("has been killed by [].", key_name(hunter)), INVESTIGATE_DEATHS);
            await living_target.death();
        }
        else if (IS_EDIBLE(hunted))
            await hunted.attack_animal(hunter);
        else
        {
            hunter.manual_emote(I18n.T("chomps []!", hunted));
            qdel(hunted);
        }
        return null;
    }

    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:38
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        hunt_async_target = null;
        if (Dm.truthy(succeeded) && Dm.truthy(cooldown_key))
            controller.set_blackboard_key(cooldown_key, world.time + hunt_cooldown);
        else if (Dm.truthy(target_key))
            controller.clear_blackboard_key(target_key);
        if (Dm.truthy(always_reset_target) && Dm.truthy(target_key))
            controller.clear_blackboard_key(target_key);
    }
}

/// Uses ai_interact() on the target instead of default kill/eat/qdel.
/// behavior_combat_mode, always_reset_target, and hunt_cooldown are all configurable
// tg: code/datums/ai/generic_behaviors/hunt_target.dm:50
public partial class datum_bt_node_ai_behavior_hunt_target_interact_with_target : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:54
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        atom hunted = controller.blackboard[target_key] as atom;
        if (QDELETED(hunted))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        hunt_async_target = hunted;
        return start_async();
    }

    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:66
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        await target_caught(controller.pawn as mob_living, hunt_async_target);
        if (!Dm.truthy(async_still_valid()))
            return null;
        finish_async(AI_BEHAVIOR_SUCCEEDED);
        return null;
    }

    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:72
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        return await ((datum_ai_controller)hunter.ai_controller).ai_interact(hunted, behavior_combat_mode);
    }
}

/// Celebrates around the target with a spin animation.
// tg: code/datums/ai/generic_behaviors/hunt_target.dm:114
public partial class datum_bt_node_ai_behavior_hunt_target_snail_people : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:117
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        hunter.manual_emote(I18n.T("Celebrates around []!", hunted));
        hunter.SpinAnimation(speed: 1, loops: 3);
        return null;
    }
}

/// Starts pulling the target item toward the hunter.
// tg: code/datums/ai/generic_behaviors/hunt_target.dm:122
public partial class datum_bt_node_ai_behavior_hunt_target_pull_target : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:125
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        obj_item hunted = controller.blackboard[target_key] as obj_item;
        if (QDELETED(hunted))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        hunt_async_target = hunted;
        return start_async();
    }

    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:137
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        await target_caught(controller.pawn as mob_living, hunt_async_target);
        if (!Dm.truthy(async_still_valid()))
            return null;
        finish_async(AI_BEHAVIOR_SUCCEEDED);
        return null;
    }

    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:143
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        return await hunter.start_pulling(hunted as atom_movable);
    }
}

/// Emotes enjoyment of the target's scent.
// tg: code/datums/ai/generic_behaviors/hunt_target.dm:147
public partial class datum_bt_node_ai_behavior_hunt_target_sniff_flora : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:150
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        hunter.manual_emote(I18n.T("Enjoys the sweet scent eminating from []!", Dm.initial(hunted, (atom a) => a.name)));
        return null;
    }
}

/// Playfully headbutts the target's legs.
// tg: code/datums/ai/generic_behaviors/hunt_target.dm:154
public partial class datum_bt_node_ai_behavior_hunt_target_headbutt_leg : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:157
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        hunter.manual_emote(I18n.T("playfully headbutts []'s legs!", hunted));
        return null;
    }
}

/// Attempts to buckle (latch onto) the target mob.
// tg: code/datums/ai/generic_behaviors/hunt_target.dm:161
public partial class datum_bt_node_ai_behavior_hunt_target_latch_onto : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:163
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        if (!Dm.truthy(await base.setup(controller)))
            return FALSE;
        mob_living living_pawn = controller.pawn as mob_living;
        return Dm.num(!Dm.truthy(living_pawn.buckled));
    }

    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:169
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        if (Dm.truthy(hunter.buckled))
            return FALSE;
        if (!Dm.truthy(await ((atom_movable)hunted).buckle_mob(hunter, force: TRUE)))
            return FALSE;
        hunted.visible_message(span_notice(I18n.T("[] has been latched onto by []!", hunted, hunter)));
        return TRUE;
    }
}

// tg: code/datums/ai/generic_behaviors/hunt_target.dm:178
public partial class datum_bt_node_ai_behavior_hunt_target_play_with_owner : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/datums/ai/generic_behaviors/hunt_target.dm:181
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        DmList interactions_list = ((datum_ai_controller)hunter.ai_controller).blackboard[BB_INTERACTIONS_WITH_OWNER] as DmList;
        object interaction_message = Dm.truthy(length(interactions_list)) ? pick(interactions_list) : "Plays with";
        hunter.manual_emote(Dm.text("[] []!", interaction_message, hunted));
        return null;
    }
}
