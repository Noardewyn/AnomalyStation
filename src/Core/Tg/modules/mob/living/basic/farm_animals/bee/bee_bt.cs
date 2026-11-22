// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the bees' behaviour tree leaves (the controllers and subtrees are var-only and load their .bt.json files).

/**
 * Searches for a valid beebox home. Skipped if the bee is already inside its home.
 * Sets BB_CURRENT_HOME when found.
 */
// tg: code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm:7
public partial class datum_bt_node_ai_behavior_find_hive : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm:9
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic_bee bee_pawn = controller.pawn as mob_living_basic_bee;
        obj_structure_beebox current_home = controller.blackboard[BB_CURRENT_HOME] as obj_structure_beebox;

        if (!QDELETED(current_home))
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED); // already have a home; let the inhabit sequence handle it

        if (bee_pawn.loc is obj_structure_beebox)
        {
            controller.set_blackboard_key(BB_CURRENT_HOME, bee_pawn.loc);
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
        }

        foreach (obj_structure_beebox potential_home in Dm.each<obj_structure_beebox>(oview(10, bee_pawn)))
        {
            if (!Dm.truthy(potential_home.habitable(bee_pawn)))
                continue;
            controller.set_blackboard_key(BB_TARGET_HOME, potential_home);
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_SUCCEEDED);
        }

        return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);
    }
}

/**
 * Moves to BB_CURRENT_HOME and inhabits it (ai_interact). Clears BB_CURRENT_HOME on failure.
 * Must be in range (adjacent) to work. Returns FAILURE if home is gone or full.
 */
// tg: code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm:33
public partial class datum_bt_node_ai_behavior_inhabit_hive : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm:36
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic_bee bee_pawn = controller.pawn as mob_living_basic_bee;
        obj_structure_beebox home = controller.blackboard[BB_CURRENT_HOME] as obj_structure_beebox;
        if (QDELETED(home))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);
        if (!Dm.truthy(home.habitable(bee_pawn)))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);
        if (!Dm.truthy(Waiting.NoSleep(bee_pawn.Adjacent(home))))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);
        Waiting.Detach(controller.ai_interact(home, FALSE)); // INVOKE_ASYNC
        return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_SUCCEEDED);
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm:48
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (!Dm.truthy(succeeded))
            controller.clear_blackboard_key(BB_CURRENT_HOME);
    }
}

///Chance to leave or enter hive
// tg: code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm:54
public partial class datum_bt_node_ai_behavior_enter_exit_hive : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm:59
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        obj_structure_beebox home = controller.blackboard[BB_CURRENT_HOME] as obj_structure_beebox;
        if (QDELETED(home))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);

        mob_living bee_pawn = controller.pawn as mob_living;
        float prob_to_use = (bee_pawn.loc == home) ? exit_chance : flyback_chance;

        if (!SPT_PROB(prob_to_use, seconds_per_tick))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);

        controller.set_blackboard_key(BB_WANTS_TO_TRANSITION_HIVE, TRUE);
        return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_SUCCEEDED);
    }
}

/// Pollinates the hydro tray at BB_TARGET_HYDRO. Must be adjacent.
// tg: code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm:80
public partial class datum_bt_node_ai_behavior_pollinate_hydro : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm:83
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        obj_machinery_hydroponics tray = controller.blackboard[BB_TARGET_HYDRO] as obj_machinery_hydroponics;
        mob_living_basic_bee bee_pawn = controller.pawn as mob_living_basic_bee;
        if (QDELETED(tray) || !Dm.truthy(Waiting.NoSleep(bee_pawn.Adjacent(tray))))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);
        bee_pawn.pollinate(tray);
        return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_SUCCEEDED);
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm:91
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(BB_TARGET_HYDRO);
    }
}

/// Picks a random turf near BB_SWARM_TARGET and stores it in BB_SWIRL_TURF for move_to_target.
/// Succeeds when a turf is found; fails otherwise so the parent sequence fails and retries.
// tg: code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm:98
public partial class datum_bt_node_ai_behavior_swirl_around_target : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/bee_bt.dm:101
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[BB_SWARM_TARGET] as atom;
        mob_living bee_pawn = controller.pawn as mob_living;
        if (QDELETED(target))
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);

        if (!SPT_PROB(swirl_chance, seconds_per_tick))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);

        DmList possible_turfs = Dm.list();
        foreach (turf possible_turf in Dm.each<turf>(oview(2, target)))
        {
            if (!Dm.truthy(Waiting.NoSleep(possible_turf.is_blocked_turf(source_atom: bee_pawn))))
                Dm.append(ref possible_turfs, possible_turf);
        }

        if (!Dm.truthy(length(possible_turfs)))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);

        controller.set_blackboard_key(BB_SWIRL_TURF, pick(possible_turfs));
        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
    }
}
