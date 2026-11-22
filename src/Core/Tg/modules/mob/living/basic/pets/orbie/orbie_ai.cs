// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/orbie/orbie_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): Orbie's AI (orbie.bt.json, the behavior tree the controller reads): playdates with another Orbie, a PDA
// message read aloud at level 2, and its pet commands (follow when summoned, the lights at level 2, a photo at level 3, the trick sequence
// programmed in the app, which came with gaps C2 for every pet). The var blocks are generated (D111).

// tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:4
public partial class datum_ai_controller_basic_controller_orbie : datum_ai_controller_basic_controller
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:1-2  the file's defines
    private const float MESSAGE_EXPIRY_TIME = 30 * SECONDS;

    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:14
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        object dot = await base.TryPossessPawn(new_pawn);
        if (Dm.truthy(Dm.bitand(dot, AI_CONTROLLER_INCOMPATIBLE)))
            return dot;
        RegisterSignal(new_pawn, COMSIG_AI_BLACKBOARD_KEY_SET(BB_LAST_RECEIVED_MESSAGE), nameof(on_set_message));
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:20
    [SignalHandler]
    public virtual void on_set_message(datum source = null)
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_blackboard_key), BB_LAST_RECEIVED_MESSAGE), MESSAGE_EXPIRY_TIME, file: "code/modules/mob/living/basic/pets/orbie/orbie_ai.dm", line: 23);
    }
}

/// Finds a nearby free orbie to play with and mutually registers as playmates.
// tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:26
public partial class datum_bt_node_ai_behavior_acquire_target_update_interaction_target_find_playmate : datum_bt_node_ai_behavior_acquire_target_update_interaction_target
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:31
    public override void on_target_found(datum_ai_controller controller = null, atom target = null, datum_targeting_strategy strategy = null)
    {
        mob_living_basic_orbie playmate = target as mob_living_basic_orbie;
        (playmate.ai_controller as datum_ai_controller).set_blackboard_key(BB_NEARBY_PLAYMATE, controller.pawn);
    }
}

/// Accepts an orbie that is free to play (no current playmate and not on playdate cooldown).
// tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:36
public partial class datum_targeting_strategy_playmate : datum_targeting_strategy
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:38
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        if (target is not mob_living_basic_orbie orbie_target)
            return FALSE;
        if (orbie_target == living_mob || orbie_target.stat == DEAD || orbie_target.ai_controller == null)
            return FALSE;
        datum_ai_controller target_controller = orbie_target.ai_controller as datum_ai_controller;
        if (Dm.truthy(target_controller.blackboard[BB_NEARBY_PLAYMATE]))
            return FALSE;
        if (Dm.num(target_controller.blackboard[BB_NEXT_PLAYDATE]) > world.time)
            return FALSE;
        return TRUE;
    }
}

///plays with a nearby orbie
// tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:51
public partial class datum_bt_node_ai_behavior_interact_with_playmate : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:1  #define PET_PLAYTIME_COOLDOWN (2 MINUTES)
    private const float PET_PLAYTIME_COOLDOWN = 2 * MINUTES;

    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:54
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic living_pawn = controller.pawn as mob_living_basic;
        atom target = controller.blackboard[target_key] as atom;

        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        living_pawn.manual_emote(I18n.T("plays with []!", target));
        living_pawn.spin(spintime: 4, speed: 1);
        Waiting.Detach(living_pawn.ClickOn(target));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:66
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
        controller.set_blackboard_key(BB_NEXT_PLAYDATE, world.time + PET_PLAYTIME_COOLDOWN);
    }
}

///relays a pda message if orbie is level 2+
// tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:72
public partial class datum_bt_node_ai_behavior_relay_pda_message : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:75
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (Dm.num(controller.blackboard[BB_VIRTUAL_PET_LEVEL]) < 2)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        mob_living_basic living_pawn = controller.pawn as mob_living_basic;
        object text_to_say = controller.blackboard[target_key];
        if (text_to_say == null)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        Waiting.Detach(living_pawn.say(text_to_say, forced: "AI controller"));
        living_pawn.spin(spintime: 4, speed: 1);
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:88
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}

// tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:92
public partial class datum_pet_command_follow_orbie : datum_pet_command_follow
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:94
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        mob_living parent = Dm.arg(__extra, 0) as mob_living;
        RegisterSignal(parent, COMSIG_VIRTUAL_PET_SUMMONED, nameof(on_summon));
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:98
    [SignalHandler]
    public virtual void on_summon(datum source = null, mob_living friend = null)
    {
        Waiting.Detach(set_command_active(source as mob_living, friend));
    }
}

///command to make our pet turn its lights on, we need to be level 2 to activate this ability
// tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:103
public partial class datum_pet_command_untargeted_ability_pet_lights : datum_pet_command_untargeted_ability
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:111
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        if (Dm.num(controller.blackboard[BB_VIRTUAL_PET_LEVEL]) < 2)
        {
            controller.clear_blackboard_key(BB_ACTIVE_PET_COMMAND);
            return TRUE;
        }
        return await base.execute_action(controller);
    }
}

// tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:120
public partial class datum_pet_command_use_ability_take_photo : datum_pet_command_use_ability
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:130
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return target == null ? null : I18n.T("signals [] to take a photo of []!", living_pet, target);
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:134
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        if (Dm.num(controller.blackboard[BB_VIRTUAL_PET_LEVEL]) < 3)
        {
            controller.clear_blackboard_key(BB_ACTIVE_PET_COMMAND);
            return TRUE;
        }
        return await base.execute_action(controller);
    }
}

// tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:140
public partial class datum_pet_command_perform_trick_sequence : datum_pet_command
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:144
    public override object find_command_in_text(object spoken_text = null, float? check_verbosity = null)
    {
        mob_living living_pawn = weak_parent.resolve() as mob_living;
        if (living_pawn?.ai_controller == null)
            return FALSE;
        object text_command = (living_pawn.ai_controller as datum_ai_controller).blackboard[BB_TRICK_NAME];
        if (text_command == null)
            return FALSE;
        return findtext(spoken_text, text_command);
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:156
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        DmList trick_sequence = controller.blackboard[BB_TRICK_SEQUENCE] as DmList;
        for (int index = 1; index <= length(trick_sequence); index++)
            _addtimer(Dm.@new<datum_callback>(living_pawn, nameof(living_pawn.emote), trick_sequence[index], index * 0.5f * SECONDS), 0, file: "code/modules/mob/living/basic/pets/orbie/orbie_ai.dm", line: 160); // tg's parenthesis puts the delay in the callback: a quirk of tg
        controller.clear_blackboard_key(BB_ACTIVE_PET_COMMAND);
        return TRUE;
    }
}

// tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:117  (a proc on a type no one defines, so DM makes it: never used)
public partial class datum_pet_command_use_ability_pet_lights : datum_pet_command_use_ability
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:117
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return I18n.T("signals [] to toggle its lights!", living_pet);
    }
}

// tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:153  (a proc on a type no one defines, so DM makes it: never used)
public partial class datum_pet_command_perform_trick_sequence_light : datum_pet_command_perform_trick_sequence
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_ai.dm:153
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return I18n.T("signals [] to dance!", living_pet);
    }
}
