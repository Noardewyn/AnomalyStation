// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file: the bees' targeting and their pet commands (swirl, the beehive, scatter). check_beehive_conditions'
// first parameter is `object` (Appendix A): tg's root names it hive but every caller and override passes the pawn first.

// tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:4
public partial class datum_targeting_strategy_basic_bee : datum_targeting_strategy_basic
{
    /// if we have a hive, this will be our aggro distance
    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:2
    private const float AGGRO_DISTANCE_FROM_HIVE = 2;

    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:6
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        mob_living owner = living_mob;
        if (!isliving(target))
            return FALSE;
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;

        mob_living mob_target = target as mob_living;

        if (Dm.truthy(Dm.bitand(mob_target.mob_biotypes, MOB_PLANT)))
            return FALSE;

        datum_ai_controller_basic_controller bee_ai = owner.ai_controller as datum_ai_controller_basic_controller;
        if (bee_ai == null)
            return FALSE;

        atom bee_hive = bee_ai.blackboard[BB_CURRENT_HOME] as atom;
        if (bee_hive != null && get_dist(target, bee_hive) > AGGRO_DISTANCE_FROM_HIVE && Dm.truthy(can_see(owner, bee_hive, 9)))
            return FALSE;

        return !Dm.truthy(mob_target.bee_friendly()) ? TRUE : FALSE;
    }
}

///swirl around the owner in menacing fashion
// tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:30
public partial class datum_pet_command_attack_swirl : datum_pet_command_attack
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:41
    public override async DmTask<object> try_activate_command(mob_living commander = null, float? radial_command = null)
    {
        mob_living living_pawn = weak_parent.resolve() as mob_living;
        if (living_pawn == null)
            return null;
        datum_ai_controller_basic_controller controller = living_pawn.ai_controller as datum_ai_controller_basic_controller;
        if (controller == null)
            return null;
        controller.clear_blackboard_key(BB_CURRENT_PET_TARGET);
        controller.set_blackboard_key(key_to_swarm, commander);
        return await base.try_activate_command(commander, radial_command);
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:52
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        if (Dm.truthy(controller.blackboard_key_exists(BB_CURRENT_PET_TARGET)))
            return await base.execute_action(controller);
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_swirl));
        return null;
    }
}

// tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:58
public partial class datum_pet_command_beehive : datum_pet_command
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:62
    public override async DmTask<object> try_activate_command(mob_living commander = null, float? radial_command = null)
    {
        mob_living living_pawn = weak_parent.resolve() as mob_living;
        if (living_pawn == null)
            return null;
        datum_ai_controller_basic_controller controller = living_pawn.ai_controller as datum_ai_controller_basic_controller;
        if (controller == null)
            return null;
        obj hive = controller.blackboard[BB_CURRENT_HOME] as obj;
        if (hive == null)
            return null;
        if (!Dm.truthy(check_beehive_conditions(living_pawn, hive)))
            return null;
        return await base.try_activate_command(commander, radial_command);
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:76  (the first parameter is object: Appendix A)
    public virtual object check_beehive_conditions(object hive = null, params object[] __extra)
    {
        return null;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:79
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_beehive));
        return null;
    }
}

// tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:82
public partial class datum_pet_command_beehive_enter : datum_pet_command_beehive
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:87
    [DmParams("living_pawn", "hive")]
    public override object check_beehive_conditions(object hive = null, params object[] __extra)
    {
        mob_living living_pawn = hive as mob_living;
        obj_structure the_hive = Dm.arg(__extra, 0) as obj_structure;
        if (Dm.@in(living_pawn, the_hive)) //already in hive
            return FALSE;
        return can_see(living_pawn, the_hive, 9);
    }
}

// tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:92
public partial class datum_pet_command_beehive_exit : datum_pet_command_beehive
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:97
    [DmParams("living_pawn", "hive")]
    public override object check_beehive_conditions(object hive = null, params object[] __extra)
    {
        mob_living living_pawn = hive as mob_living;
        obj_structure the_hive = Dm.arg(__extra, 0) as obj_structure;
        return Dm.@in(living_pawn, the_hive) ? TRUE : FALSE;
    }
}

// tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:100
public partial class datum_pet_command_scatter : datum_pet_command
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:105
    public override async DmTask set_command_active(mob_living parent = null, mob_living commander = null, float? radial_command = null)
    {
        await base.set_command_active(parent, commander, radial_command);
        await set_command_target(parent, commander);
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:109
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_scatter));
        return null;
    }
}
