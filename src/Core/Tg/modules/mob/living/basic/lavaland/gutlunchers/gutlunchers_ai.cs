// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The controllers and the mine walls subtree are var blocks (generated); their trees are tg's compiled .bt.json (BehaviorTrees/).

// tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:30
public partial class datum_pet_command_mine_walls : datum_pet_command
{
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:36
    public override async DmTask<object> try_activate_command(mob_living commander = null, float? radial_command = null)
    {
        mob_living parent = weak_parent.resolve() as mob_living;
        if (parent == null)
            return null;
        //no walls for us to mine
        object target_in_vicinity = Dm.locate<turf_closed_mineral>(oview(9, parent));
        if (target_in_vicinity == null)
            return null;
        return await base.try_activate_command(commander, radial_command);
    }

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:46
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_mine_walls));
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:49
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return I18n.T("signals [] to start mining!", living_pet);
    }
}

//pet commands
// tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:53
public partial class datum_pet_command_breed_gutlunch : datum_pet_command_breed
{
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:1  #define MAXIMUM_GUTLUNCH_POP 20 (file-local, #undef at :111)
    private const float MAXIMUM_GUTLUNCH_POP = 20;

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:55
    public override async DmTask<object> set_command_target(mob_living parent = null, atom target = null)
    {
        if (GLOB.gutlunch_count >= MAXIMUM_GUTLUNCH_POP)
        {
            parent.balloon_alert_to_viewers("can't reproduce anymore!");
            return FALSE;
        }
        return await base.set_command_target(parent, target);
    }
}

/// Interacts with the food trough to eat ore, then clears the hungry flag.
// tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:62
public partial class datum_bt_node_ai_behavior_hunt_target_interact_with_target_food_trough : datum_bt_node_ai_behavior_hunt_target_interact_with_target
{
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:66
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (Dm.truthy(succeeded))
            controller.clear_blackboard_key(BB_CHECK_HUNGRY);
    }
}

///Find nearby ashwalkers (which we share the FACTION_ASHWALKERS faction with), we love lizards.
// tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:72
public partial class datum_bt_node_ai_behavior_befriend_ashwalkers : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:75
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        foreach (mob_living potential_friend in Dm.each<mob_living>(oview(9, living_pawn)))
        {
            /// The client check is necessary so we don't befriend any npc mining mob that isn't hostile to ashwalkers (until attacked)
            if (living_pawn.client == null || !Dm.truthy(living_pawn.faction_check_atom(potential_friend)) || Dm.truthy(living_pawn.has_ally(REF(potential_friend))))
                continue;
            Waiting.NoSleep(living_pawn.befriend(potential_friend));
            to_chat(potential_friend, span_nicegreen(I18n.T("[] looks at you with endearing eyes!", living_pawn)));
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }
}

/// Searches for and moves to a parent mob (of types in BB_FIND_MOM_TYPES), sets BB_FOUND_MOM.
// tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:88
public partial class datum_bt_node_ai_behavior_find_parent : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_ai.dm:92
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob living_pawn = controller.pawn as mob;
        DmList mom_types = controller.blackboard[mom_types_key] as DmList;
        if (Dm.length(mom_types) == 0)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        foreach (mob mother in Dm.each<mob>(oview(7, living_pawn)))
        {
            if (!Dm.truthy(is_type_in_list(mother, mom_types)))
                continue;
            controller.set_blackboard_key(found_mom_key, mother);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }
}
