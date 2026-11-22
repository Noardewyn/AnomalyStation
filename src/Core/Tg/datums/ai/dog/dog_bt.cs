// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/dog/dog_bt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/**
 * BT version of basic_melee_attack/dog.
 * When adjacent and BB_DOG_HARASS_HARM = FALSE: paws harmlessly (animation, no damage).
 * When adjacent and BB_DOG_HARASS_HARM = TRUE: bites normally.
 * Returns FAILURE if targeting strategy rejects the target.
 */
// tg: code/datums/ai/dog/dog_bt.dm:10
public partial class datum_bt_node_ai_behavior_basic_melee_attack_dog : datum_bt_node_ai_behavior_basic_melee_attack
{
    // tg: code/datums/ai/dog/dog_bt.dm:10
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        if (!(isturf(living_pawn.loc) || HAS_TRAIT(living_pawn, TRAIT_AI_BAGATTACK)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        atom target = controller.blackboard[target_key] as atom;

        if (!ispath(targeting_strategy))
            targeting_strategy = controller.blackboard[targeting_strategy];

        datum_targeting_strategy strategy = GET_TARGETING_STRATEGY(targeting_strategy);
        if (QDELETED(target) || strategy == null || !Dm.truthy(Waiting.NoSleep(strategy.is_valid_target(living_pawn, target))))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        if (!Dm.truthy(controller.blackboard[BB_DOG_HARASS_HARM]))
        {
            paw_harmlessly(living_pawn, target, seconds_per_tick);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }

        return base.perform(seconds_per_tick, controller);
    }

    /// Swat at someone we don't like but won't hurt
    // tg: code/datums/ai/dog/dog_bt.dm:31
    public virtual void paw_harmlessly(mob_living living_pawn = null, atom target = null, object seconds_per_tick = null)
    {
        if (!SPT_PROB(20, seconds_per_tick))
            return;
        Waiting.Detach(living_pawn.do_attack_animation(target, ATTACK_EFFECT_DISARM));
        playsound(target, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 50, TRUE, -1);
        target.visible_message(span_danger(Dm.text("[] paws ineffectually at []!", living_pawn, target)), span_danger(Dm.text("[] paws ineffectually at you!", living_pawn)));
    }
}

/**
 * Searches for a target with TRAIT_HATED_BY_DOGS within 2 tiles. Sets BB_DOG_HARASS_TARGET and
 * BB_DOG_HARASS_HARM = FALSE if found. Returns FAILURE if no valid target is found.
 * Combine with a cooldown decorator or embed the SPT_PROB gate in the parent tree.
 */
// tg: code/datums/ai/dog/dog_bt.dm:45
public partial class datum_bt_node_ai_behavior_find_hated_dog_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/dog/dog_bt.dm:48
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!SPT_PROB(10, seconds_per_tick))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        mob_living dog = controller.pawn as mob_living;

        if (!ispath(targeting_strategy))
            targeting_strategy = controller.blackboard[targeting_strategy];

        datum_targeting_strategy strategy = GET_TARGETING_STRATEGY(targeting_strategy);
        foreach (mob_living iter_living in Dm.each<mob_living>(oview(2, dog)))
        {
            if (IS_UNCONSCIOUS_OR_CRIT(iter_living) || !HAS_TRAIT(iter_living, TRAIT_HATED_BY_DOGS))
                continue;
            if (dog.buckled != null)
            {
                dog.audible_message(span_notice(Dm.text("[] growls at [], yet [] [] much too comfy to move.", dog, iter_living, dog.p_they(), dog.p_are())), hearing_distance: COMBAT_MESSAGE_RANGE);
                continue;
            }
            if (strategy == null || !Dm.truthy(Waiting.NoSleep(strategy.is_valid_target(dog, iter_living))))
                continue;
            dog.audible_message(span_warning(Dm.text("[] growls at [], seemingly annoyed by [] presence.", dog, iter_living, iter_living.p_their())), hearing_distance: COMBAT_MESSAGE_RANGE);
            controller.set_blackboard_key(target_key, iter_living);
            controller.set_blackboard_key(BB_DOG_HARASS_HARM, FALSE);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }

        controller.clear_blackboard_key(target_key);
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }
}

/**
 * Dog-specific idle: random walks at a dog-appropriate rate, and occasionally spins/tail-chases.
 * Reads BB_DOG_IS_SLOW to determine movement chance. Always returns BT_RUNNING.
 */
// tg: code/datums/ai/dog/dog_bt.dm:82
public partial class datum_bt_node_ai_behavior_idle_dog : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/dog/dog_bt.dm:82
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;

        obj_item carry_item = controller.blackboard[BB_SIMPLE_CARRY_ITEM] as obj_item;
        if (carry_item != null && SPT_PROB(5, seconds_per_tick))
            living_pawn.visible_message(span_notice(Dm.text("[] gently teethes on \\the [] in [] mouth.", living_pawn, carry_item, living_pawn.p_their())), vision_distance: COMBAT_MESSAGE_RANGE);

        float move_chance = Dm.truthy(controller.blackboard[BB_DOG_IS_SLOW]) ? 2.5f : 5;
        if (isturf(living_pawn.loc) && living_pawn.pulledby == null)
        {
            if (SPT_PROB(move_chance, seconds_per_tick) && Dm.truthy(Dm.bitand(living_pawn.mobility_flags, MOBILITY_MOVE)))
            {
                object move_dir = pick(GLOB.alldirs);
                Waiting.NoSleep(living_pawn.Move(get_step(living_pawn, Dm.num(move_dir)), Dm.num(move_dir)));
            }
            else if (SPT_PROB(2, seconds_per_tick))
            {
                living_pawn.manual_emote(pick("dances around.", Dm.text("chases [] tail!", living_pawn.p_their())));
                Waiting.NoSleep(living_pawn.AddComponent(typeof(datum_component_spinny)));
            }
        }

        return AI_BEHAVIOR_DELAY;
    }
}

///Dog speech updates the BB keys based on the dogs swag
// tg: code/datums/ai/dog/dog_bt.dm:103
public partial class datum_bt_node_ai_behavior_random_speech_blackboard_dog : datum_bt_node_ai_behavior_random_speech_blackboard
{
    // tg: code/datums/ai/dog/dog_bt.dm:103
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (controller.pawn is not mob_living_basic_pet_dog dog_pawn)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        dog_pawn.update_dog_speak_blackboard(controller);
        return base.perform(seconds_per_tick, controller);
    }
}
