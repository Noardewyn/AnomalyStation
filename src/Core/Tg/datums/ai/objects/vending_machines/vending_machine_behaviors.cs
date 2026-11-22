// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/objects/vending_machines/vending_machine_behaviors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the rogue vending machine's behaviour tree leaves. The var blocks are generated (D111).

/// Returns TRUE if the vending machine pawn is currently tilted.
// tg: code/datums/ai/objects/vending_machines/vending_machine_behaviors.dm:2
public partial class datum_bt_node_decorator_vending_is_tilted : datum_bt_node_decorator
{
    // tg: code/datums/ai/objects/vending_machines/vending_machine_behaviors.dm:2
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        var vendor_pawn = (obj_machinery_vending)controller.pawn;
        return vendor_pawn.tilted;
    }
}

/// Searches nearby tiles for a valid living target and sets the given BB key. Sets tilt cooldown and fails if none found.
// tg: code/datums/ai/objects/vending_machines/vending_machine_behaviors.dm:7
public partial class datum_bt_node_ai_behavior_find_vendor_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/objects/vending_machines/vending_machine_behaviors.dm:13
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        foreach (mob_living living_target in Dm.each<mob_living>(oview(vision_range, controller.pawn)))
        {
            if (Dm.truthy(living_target.stat) || Dm.truthy(living_target.incorporeal_move))
                continue;
            controller.set_blackboard_key(target_key, living_target);
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_SUCCEEDED);
        }
        controller.clear_blackboard_key(target_key);
        controller.set_blackboard_key(BB_VENDING_TILT_COOLDOWN, world.time + search_cooldown);
        return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);
    }
}

/// Telegraphs and tilts onto the target. Returns success once the machine is tilted.
// tg: code/datums/ai/objects/vending_machines/vending_machine_behaviors.dm:24
public partial class datum_bt_node_ai_behavior_vendor_crush : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/objects/vending_machines/vending_machine_behaviors.dm:31
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        var vendor_pawn = (obj_machinery_vending)controller.pawn;
        if (Dm.truthy(vendor_pawn.tilted))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_SUCCEEDED);
        if (Dm.truthy(controller.blackboard[BB_VENDING_BUSY_TILTING]))
            return AI_BEHAVIOR_DELAY;
        var target_turf = get_turf(controller.blackboard[target_key]) as turf;
        if (target_turf == null || !Dm.truthy(Waiting.NoSleep(target_turf.IsReachableBy(vendor_pawn))))
            return AI_BEHAVIOR_FAILED;
        controller.movement.stop_moving_towards(controller);
        controller.set_blackboard_key(BB_VENDING_BUSY_TILTING, TRUE);
        Dm.@new<obj_effect_temp_visual_telegraphing_vending_machine_tilt>(target_turf);
        _addtimer(Dm.@new<datum_callback>(this, nameof(tiltonmob), controller, target_turf), time_to_tilt, file: "code/datums/ai/objects/vending_machines/vending_machine_behaviors.dm", line: 43);
        return AI_BEHAVIOR_DELAY;
    }

    // tg: code/datums/ai/objects/vending_machines/vending_machine_behaviors.dm:46
    public virtual async DmTask tiltonmob(datum_ai_controller controller = null, turf target_turf = null)
    {
        if (QDELETED(controller) || QDELETED(controller.pawn))
            return;
        var vendor_pawn = (obj_machinery_vending)controller.pawn;
        if (Dm.truthy(Dm.bitand(await vendor_pawn.tilt(target_turf, 0), SUCCESSFULLY_CRUSHED_MOB)))
        {
            await vendor_pawn.say(pick(I18n.T("Supersize this!"), I18n.T("Eat my shiny metal ass!"), I18n.T("Want to consume some of my products?"), I18n.T("SMASH!"), I18n.T("Don't you love these smashing prices!")));
            controller.set_blackboard_key(BB_VENDING_LAST_HIT_SUCCESSFUL, TRUE);
        }
        else
        {
            if (Dm.truthy(vendor_pawn.icon_deny))
                flick(vendor_pawn.icon_deny, vendor_pawn);
            await vendor_pawn.say(pick(I18n.T("Get back here!"), I18n.T("Don't you want my well priced love?")));
            controller.set_blackboard_key(BB_VENDING_LAST_HIT_SUCCESSFUL, FALSE);
        }
        controller.set_blackboard_key(BB_VENDING_UNTILT_COOLDOWN, world.time + untilt_cooldown);
        controller.set_blackboard_key(BB_VENDING_BUSY_TILTING, FALSE);
    }

    // tg: code/datums/ai/objects/vending_machines/vending_machine_behaviors.dm:61
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.set_blackboard_key(BB_VENDING_BUSY_TILTING, FALSE);
        if (!Dm.truthy(succeeded))
            controller.clear_blackboard_key(target_key);
    }
}

/// Untilts the machine. Sets a tilt cooldown if the previous hit was successful.
// tg: code/datums/ai/objects/vending_machines/vending_machine_behaviors.dm:68
public partial class datum_bt_node_ai_behavior_vendor_rise_up : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/objects/vending_machines/vending_machine_behaviors.dm:72
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        var vendor_pawn = (obj_machinery_vending)controller.pawn;
        vendor_pawn.visible_message(span_warning(I18n.T("[] untilts itself!", vendor_pawn)));
        if (Dm.truthy(controller.blackboard[BB_VENDING_LAST_HIT_SUCCESSFUL]))
            controller.set_blackboard_key(BB_VENDING_TILT_COOLDOWN, world.time + success_tilt_cooldown);
        Waiting.NoSleep(vendor_pawn.untilt());
        return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_SUCCEEDED);
    }
}
