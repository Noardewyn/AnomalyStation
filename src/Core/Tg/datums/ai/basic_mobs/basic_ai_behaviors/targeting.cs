// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file but the proximity field (tg's own failed_to_find_anyone keeps it commented out, "TEMP DISABLED") and the EVLOG lines (gaps package C1).

// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:2-6
public partial class datum_controller_global_vars : datum_controller
{
    /// List of objects that AIs will treat as targets
    public DmList hostile_machines_by_z;
    /// Static typecache list of things we are interested in
    /// Consider this a union of the for loop and the hearers call from below
    /// Must be kept up to date with the contents of hostile_machines
    public DmList target_interested_atoms;

    public virtual void InitGlobalhostile_machines_by_z()
    {
        hostile_machines_by_z = Dm.alist();
        Dm.append(ref gvars_datum_init_order, "hostile_machines_by_z");
    }

    public virtual void InitGlobaltarget_interested_atoms()
    {
        target_interested_atoms = typecacheof(Dm.list(typeof(mob), typeof(obj_machinery_porta_turret), typeof(obj_vehicle_sealed_mecha))) as DmList;
        Dm.append(ref gvars_datum_init_order, "target_interested_atoms");
    }
}

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/ai/ai_blackboard.dm:296
    public static string BB_FIND_TARGETS_FIELD(object type) => Dm.text("bb_find_targets_field_[]", type);
}

///Used to find combat targets; Allow finding things hidden in things such as lockers too.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:10
public partial class datum_bt_node_ai_behavior_acquire_target_update_combat_targets : datum_bt_node_ai_behavior_acquire_target
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:22
    public override object get_cooldown(datum_ai_controller controller = null)
    {
        if (Dm.truthy(controller.blackboard[BB_FIND_TARGETS_FIELD(type)]))
            return 60 * SECONDS;
        return base.get_cooldown(controller);
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:27
    public override object can_search(datum_ai_controller controller = null)
    {
        return Dm.num(!Dm.truthy(controller.blackboard[BB_FIND_TARGETS_FIELD(type)]));
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:30
    public override async DmTask<object> on_no_candidates(datum_ai_controller controller = null, atom current_target = null, datum_targeting_strategy strategy = null, object range = null)
    {
        if (current_target != null && Dm.truthy(await strategy.can_keep_target(controller.pawn as mob_living, current_target, target_loss_distance, controller)))
            return Dm.list(current_target);
        if (current_target == null)
            failed_to_find_anyone(controller, target_key, targeting_strategy, hiding_location_key);
        return Dm.list();
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:37
    public override void on_no_valid_candidates(datum_ai_controller controller = null, atom current_target = null)
    {
        if (current_target == null)
            failed_to_find_anyone(controller, target_key, targeting_strategy, hiding_location_key);
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:41
    public override void on_target_found(datum_ai_controller controller = null, atom target = null, datum_targeting_strategy strategy = null)
    {
        atom potential_hiding_location = strategy.find_hidden_mobs(controller.pawn as mob_living, target) as atom;
        if (potential_hiding_location != null)
            controller.set_blackboard_key(hiding_location_key, potential_hiding_location);
        else if (Dm.truthy(hiding_location_key))
            controller.clear_blackboard_key(hiding_location_key);
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:48
    public virtual void failed_to_find_anyone(datum_ai_controller controller = null, object target_key = null, object targeting_strategy = null, object hiding_location_key = null)
    {
        // TEMP DISABLED nuke this if performance improves (tg's own comment: the proximity field that waits for someone interesting is commented out at the snapshot)
        controller.clear_blackboard_key(target_key);
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:73
    public virtual async DmTask new_turf_found(turf found = null, datum_ai_controller controller = null, datum_targeting_strategy strategy = null)
    {
        bool valid_found = false;
        mob pawn = controller.pawn as mob;
        foreach (object maybe_target in Dm.each(found))
        {
            if (Dm.eq(maybe_target, pawn))
                continue;
            if (!Dm.truthy(is_type_in_typecache(maybe_target, GLOB.target_interested_atoms)))
                continue;
            if (!Dm.truthy(await strategy.is_valid_target(pawn as mob_living, maybe_target as atom)))
                continue;
            valid_found = true;
            break;
        }
        if (!valid_found)
            return;
        // If we found any one thing we "could" attack, then run the full search again so we can select from the best possible canidate
        qdel(controller.blackboard[BB_FIND_TARGETS_FIELD(type)]); // autoclears so it's fine
        // Fire instantly, you should find something I hope
        modify_cooldown(world.time);
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:93
    public virtual async DmTask<object> atom_allowed(atom_movable checking = null, datum_targeting_strategy strategy = null, mob pawn = null)
    {
        if (checking == pawn)
            return FALSE;
        if (!ismob(checking) && !Dm.truthy(is_type_in_typecache(checking, GLOB.target_interested_atoms)))
            return FALSE;
        if (!Dm.truthy(await strategy.is_valid_target(pawn as mob_living, checking)))
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:102
    public virtual async DmTask new_atoms_found(DmList found = null, datum_ai_controller controller = null, object target_key = null, datum_targeting_strategy strategy = null, object hiding_location_key = null)
    {
        mob pawn = controller.pawn as mob;
        DmList accepted_targets = Dm.list();
        foreach (object maybe_target in Dm.each(found))
        {
            if (Dm.eq(maybe_target, pawn))
                continue;
            // Need to better handle viewers here
            if (!ismob(maybe_target) && !Dm.truthy(is_type_in_typecache(maybe_target, GLOB.target_interested_atoms)))
                continue;
            if (!Dm.truthy(await strategy.is_valid_target(pawn as mob_living, maybe_target as atom)))
                continue;
            Dm.append(ref accepted_targets, maybe_target);
        }

        // Alright, we found something acceptable, let's use it yeah?
        datum_target_priority_strategy priority_strategy = get_target_priority_strategy(controller) as datum_target_priority_strategy;
        atom current_target = controller.blackboard[target_key] as atom;
        atom target = pick_final_target(controller, accepted_targets, priority_strategy, current_target) as atom;
        if (target != current_target)
            controller.set_blackboard_key(target_key, target);
        on_target_found(controller, target, strategy);

        await finish_action(controller, succeeded: TRUE);
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:127
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (Dm.truthy(succeeded))
        {
            qdel(controller.blackboard[BB_FIND_TARGETS_FIELD(type)]); // autoclears so it's fine
            modify_cooldown(get_cooldown(controller));
        }
    }

    /// Preserve the combat query's original first-valid behavior when no optional selection strategy is configured.
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeting.dm:135
    public override object pick_final_target(datum_ai_controller controller = null, DmList filtered_targets = null, datum_target_priority_strategy priority_strategy = null, atom current_target = null)
    {
        if (priority_strategy == null)
            return filtered_targets[1];
        return base.pick_final_target(controller, filtered_targets, priority_strategy, current_target);
    }
}
