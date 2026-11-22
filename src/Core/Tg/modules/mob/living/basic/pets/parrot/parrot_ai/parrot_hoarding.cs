// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_hoarding.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/// Accepts open turfs which aren't space, aren't blocked, and are within hoarding range. Used to pick a parrot's nest.
// tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_hoarding.dm:4
public partial class datum_targeting_strategy_parrot_hoard_location : datum_targeting_strategy
{
    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_hoarding.dm:4
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not turf_open candidate || is_space_or_openspace(candidate))
            return FALSE;
        if (Dm.truthy(await candidate.is_blocked_turf(source_atom: living_mob)))
            return FALSE;
        return TRUE;
    }
}

/// Accepts small items lying on a turf away from the nest, or non-ally humans holding a small valuable. Used to pick something to steal.
// tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_hoarding.dm:18
public partial class datum_targeting_strategy_parrot_hoard_item : datum_targeting_strategy
{
    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_hoarding.dm:18
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (controller == null)
            return FALSE;

        object ignore_items = controller.blackboard[BB_IGNORE_ITEMS];
        if (Dm.truthy(is_type_in_typecache(target, ignore_items)))
            return FALSE;

        object nest_turf = controller.blackboard[BB_HOARD_LOCATION];
        if (target.loc == nest_turf)
            return FALSE;

        if (target is obj_item loose_item)
            return Dm.num(loose_item.w_class) <= WEIGHT_CLASS_SMALL ? TRUE : FALSE;

        if (!ishuman(target))
            return FALSE;
        if (Dm.truthy(living_mob.has_ally(target as atom_movable))) // dont steal from friends
            return FALSE;
        return holding_valuable(controller, target as mob_living);
    }

    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_hoarding.dm:43
    public virtual object holding_valuable(datum_ai_controller controller = null, mob_living human_target = null)
    {
        object ignore_items = controller.blackboard[BB_IGNORE_ITEMS];
        foreach (obj_item potential_item in Dm.each<obj_item>(human_target.held_items))
        {
            if (Dm.truthy(is_type_in_typecache(potential_item, ignore_items)))
                continue;
            if (Dm.num(potential_item.w_class) <= WEIGHT_CLASS_SMALL)
                return TRUE;
        }
        return FALSE;
    }
}

/// Finds something for the parrot to steal. Temporarily ignores faction when eyeing a person's belongings.
// tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_hoarding.dm:54
public partial class datum_bt_node_ai_behavior_acquire_target_parrot_hoard_item : datum_bt_node_ai_behavior_acquire_target
{
    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_hoarding.dm:58
    public override void on_target_found(datum_ai_controller controller = null, atom target = null, datum_targeting_strategy strategy = null)
    {
        if (ishuman(target))
            controller.set_blackboard_key(BB_ALWAYS_IGNORE_FACTION, TRUE);
    }
}

/// Single-hit grab variant which resets the faction-ignore flag once we are done stealing.
// tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_hoarding.dm:65
public partial class datum_bt_node_ai_behavior_basic_melee_attack_interact_once_parrot : datum_bt_node_ai_behavior_basic_melee_attack_interact_once
{
    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_hoarding.dm:65
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.set_blackboard_key(BB_ALWAYS_IGNORE_FACTION, FALSE);
    }
}
