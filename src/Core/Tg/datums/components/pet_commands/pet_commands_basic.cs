// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/pet_commands/pet_commands_basic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).
// None of these are really complex enough to merit their own file

/**
 * # Pet Command: Idle
 * Tells a pet to resume its idle behaviour, usually staying put where you leave it
 */
// tg: code/datums/components/pet_commands/pet_commands_basic.dm:8
public partial class datum_pet_command_idle : datum_pet_command
{
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:14
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_stay));
        return null;
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:17
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return Dm.text("signals [] to stay idle!", living_pet);
    }
}

/**
 * # Pet Command: Stop
 * Tells a pet to exit command mode and resume its normal behaviour
 */
// tg: code/datums/components/pet_commands/pet_commands_basic.dm:25
public partial class datum_pet_command_free : datum_pet_command
{
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:31
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, null);
        controller.clear_blackboard_key(BB_ACTIVE_PET_COMMAND);
        return null;
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:35
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return Dm.text("signals [] to go free!", living_pet);
    }
}

/**
 * # Pet Command: Follow
 * Tells a pet to follow you until you tell it to do something else
 */
// tg: code/datums/components/pet_commands/pet_commands_basic.dm:43
public partial class datum_pet_command_follow : datum_pet_command
{
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:53
    public override async DmTask set_command_active(mob_living parent = null, mob_living commander = null, float? radial_command = null)
    {
        await base.set_command_active(parent, commander, radial_command);
        await set_command_target(parent, commander);
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:57
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return Dm.text("signals [] to follow!", living_pet);
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:60
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, follow_subtree);
        return null;
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:63
    public override async DmTask add_new_friend(mob_living tamer = null)
    {
        await base.add_new_friend(tamer);
        mob_living parent = weak_parent.resolve() as mob_living;
        if (parent == null)
            return;
        if (Dm.truthy(activate_on_befriend) && !Dm.truthy((parent.ai_controller as datum_ai_controller).blackboard_key_exists(BB_ACTIVE_PET_COMMAND)))
            await try_activate_command(tamer);
    }
}

/**
 * # Pet Command: Play Dead
 * Pretend to be dead for a random period of time
 */
// tg: code/datums/components/pet_commands/pet_commands_basic.dm:80
public partial class datum_pet_command_play_dead : datum_pet_command
{
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:85
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_play_dead));
        return null;
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:88
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return Dm.text("signals [] to play dead!", living_pet);
    }
}

/**
 * # Pet Command: Good Boy
 * React if complimented
 */
// tg: code/datums/components/pet_commands/pet_commands_basic.dm:96
public partial class datum_pet_command_good_boy : datum_pet_command
{
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:100
    [DmParams("parent")]
    public override async DmTask New(params object[] __extra)
    {
        mob_living parent = Dm.arg<mob_living>(__extra, 0);
        await base.New(__extra);
        Dm.append(ref speech_commands, Dm.text("good []", parent.name));
        switch (parent.gender)
        {
            case MALE:
                Dm.append(ref speech_commands, "good boy");
                return;
            case FEMALE:
                Dm.append(ref speech_commands, "good girl");
                return;
        }
        // If we get past this point someone has finally added a non-binary dog
        return;
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:112
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, null);
        controller.clear_blackboard_key(BB_ACTIVE_PET_COMMAND);
        mob_living parent = weak_parent.resolve() as mob_living;
        if (parent == null)
            return null;
        Dm.@new<obj_effect_temp_visual_heart>(parent.loc);
        await parent.emote("spin");
        return null;
    }
}

/**
 * # Pet Command: Use ability
 * Use an an ability that does not require any targets
 */
// tg: code/datums/components/pet_commands/pet_commands_basic.dm:127
public partial class datum_pet_command_untargeted_ability : datum_pet_command
{
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:129
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        datum_action_cooldown ability = controller.blackboard[ability_key] as datum_action_cooldown;
        if (ability == null || !Dm.truthy(Waiting.NoSleep(ability.IsAvailable())))
            return null;
        controller.set_blackboard_key(BB_PET_ACTIVE_ABILITY, ability);
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_untargeted_ability));
        controller.clear_blackboard_key(BB_ACTIVE_PET_COMMAND);
        return null;
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:137
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return Dm.text("signals [] to use an ability!", living_pet);
    }
}

/**
 * # Pet Command: Attack
 * Tells a pet to chase and bite the next thing you point at
 */
// tg: code/datums/components/pet_commands/pet_commands_basic.dm:145
public partial class datum_pet_command_attack : datum_pet_command
{
    // Refuse to target things we can't target, chiefly other friends
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:158
    public override async DmTask<object> set_command_target(mob_living parent = null, atom target = null)
    {
        if (target == null)
            return FALSE;
        mob_living living_parent = parent;
        if (!Dm.truthy(living_parent.ai_controller))
            return FALSE;
        datum_targeting_strategy targeter = GET_TARGETING_STRATEGY((living_parent.ai_controller as datum_ai_controller).blackboard[targeting_strategy_key]);
        if (targeter == null)
            return FALSE;
        if (!Dm.truthy(await targeter.is_valid_target(living_parent, target)))
        {
            refuse_target(parent, target);
            return FALSE;
        }
        return await base.set_command_target(parent, target);
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:172
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return target == null ? null : Dm.text("signals [] to attack []!", living_pet, target);
    }

    /// Display feedback about not targeting something
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:176
    public virtual void refuse_target(mob_living parent = null, atom target = null)
    {
        mob_living living_parent = parent;
        living_parent.balloon_alert_to_viewers(Dm.text("[]", refuse_reaction));
        living_parent.visible_message(span_notice(Dm.text("[] refuses to attack [].", living_parent, target)));
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:181
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, attack_subtree);
        return null;
    }
}

/**
 * # Breed command. breed with a partner!
 */
// tg: code/datums/components/pet_commands/pet_commands_basic.dm:188
public partial class datum_pet_command_breed : datum_pet_command
{
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:194
    public override async DmTask<object> set_command_target(mob_living parent = null, atom target = null)
    {
        if (target == null || !isliving(target))
            return FALSE;
        if (!HAS_TRAIT(parent, TRAIT_MOB_BREEDER) || !HAS_TRAIT(target, TRAIT_MOB_BREEDER))
            return FALSE;
        if (parent.ai_controller == null)
            return FALSE;
        datum_ai_controller controller = parent.ai_controller as datum_ai_controller;
        if (!Dm.truthy(controller.blackboard[BB_BREED_READY]) || controller.blackboard[BB_BABIES_PARTNER_TYPES] == null)
            return FALSE;
        mob_living living_target = target as mob_living;
        if (!Dm.truthy((living_target.ai_controller as datum_ai_controller)?.blackboard[BB_BREED_READY]))
            return FALSE;
        return await base.set_command_target(parent, target);
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:208
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_breed));
        return null;
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:211
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return target == null ? null : Dm.text("signals [] to breed with []!", living_pet, target);
    }
}

/**
 * # Pet Command: Targetted Ability
 * Tells a pet to use some kind of ability on the next thing you point at
 */
// tg: code/datums/components/pet_commands/pet_commands_basic.dm:219
public partial class datum_pet_command_use_ability : datum_pet_command
{
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:230
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        if (!Dm.truthy(pet_ability_key))
            return null;
        datum_action_cooldown using_action = controller.blackboard[pet_ability_key] as datum_action_cooldown;
        if (QDELETED(using_action))
            return null;
        // Store the action datum in a fixed key so the targeted_ability subtree can find it
        controller.override_blackboard_key(BB_TARGETED_ACTION, using_action);
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_targeted_ability));
        return null;
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:240
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return target == null ? null : Dm.text("signals [] to use an ability on []!", living_pet, target);
    }
}

// tg: code/datums/components/pet_commands/pet_commands_basic.dm:244
public partial class datum_pet_command_protect_owner : datum_pet_command
{
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:254
    public override async DmTask add_new_friend(mob_living tamer = null)
    {
        RegisterSignal(tamer, COMSIG_ATOM_WAS_ATTACKED, nameof(set_attacking_target));
        if (!HAS_TRAIT(tamer, TRAIT_RELAYING_ATTACKER))
            await tamer.AddElement(typeof(datum_element_relay_attackers));
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:259
    public override async DmTask remove_friend(mob_living unfriended = null)
    {
        UnregisterSignal(unfriended, COMSIG_ATOM_WAS_ATTACKED);
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:262
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, protect_owner_subtree);
        return null;
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:265
    public override async DmTask set_command_active(mob_living parent = null, mob_living victim = null, float? radial_command = null)
    {
        await base.set_command_active(parent, victim, radial_command);
        await set_command_target(parent, victim);
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:269
    public override object valid_callout_target(mob_living speaker = null, object callout = null, atom target = null)
    {
        return target == speaker || get_dist(speaker, target) <= 1 ? TRUE : FALSE;
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:272
    [SignalHandler]
    public virtual void set_attacking_target(atom source = null, mob_living attacker = null)
    {
        mob_living_basic owner = weak_parent.resolve() as mob_living_basic;
        if (owner == null)
            return;
        datum_ai_controller controller = owner.ai_controller as datum_ai_controller;
        if (source == attacker)
        {
            DmList interventions = controller?.blackboard[BB_OWNER_SELF_HARM_RESPONSES] as DmList ?? Dm.list();
            if (Dm.truthy(length(interventions)) && self_harm_message_cooldown <= world.time && prob(30))
            {
                self_harm_message_cooldown = world.time + 5 * SECONDS;
                object chosen_statement = pick(interventions);
                Waiting.Detach(owner.say(chosen_statement));
            }
            return;
        }
        mob_living current_target = controller?.blackboard[BB_CURRENT_PET_TARGET] as mob_living;
        if (attacker == current_target) //we are already dealing with this target
            return;
        if (isliving(attacker) && Dm.truthy(can_see(owner, attacker, protect_range)))
            Waiting.Detach(set_command_active(owner, attacker));
    }
}

/**
 * # Fish command: command the mob to fish at the next fishing spot you point at.
 */
// tg: code/datums/components/pet_commands/pet_commands_basic.dm:295
public partial class datum_pet_command_fish : datum_pet_command
{
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:301
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        if (!Dm.truthy(controller.blackboard_key_exists(BB_CURRENT_PET_TARGET)))
        {
            await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_stay));
            return null;
        }
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_fish));
        return null;
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:307
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return Dm.text("signals [] to go fish!", living_pet);
    }
}

// tg: code/datums/components/pet_commands/pet_commands_basic.dm:311
public partial class datum_pet_command_move : datum_pet_command
{
    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:317
    public override async DmTask<object> set_command_target(mob_living parent = null, atom target = null)
    {
        if (target == null || !Dm.truthy(can_see(parent, target, 9)))
            return FALSE;
        return await base.set_command_target(parent, target);
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:322
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_move_to));
        return null;
    }

    // tg: code/datums/components/pet_commands/pet_commands_basic.dm:325
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return Dm.text("signals [] to move!", living_pet);
    }
}
