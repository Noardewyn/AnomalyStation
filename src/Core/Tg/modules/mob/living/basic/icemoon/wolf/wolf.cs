// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/icemoon/wolf/wolf.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// tg: code/modules/mob/living/basic/icemoon/wolf/wolf.dm:1
public partial class mob_living_basic_mining_wolf : mob_living_basic_mining
{
    //commands to give when tamed
    // tg: code/modules/mob/living/basic/icemoon/wolf/wolf.dm:42 (var/static/list/pet_commands)
    public static DmList pet_commands => TgState.Current.mob_living_basic_mining_wolf__pet_commands ??= Dm.list(
        typeof(datum_pet_command_idle),
        typeof(datum_pet_command_move),
        typeof(datum_pet_command_free),
        typeof(datum_pet_command_good_boy_wolf),
        typeof(datum_pet_command_follow_wolf),
        typeof(datum_pet_command_attack),
        typeof(datum_pet_command_fetch),
        typeof(datum_pet_command_play_dead),
        typeof(datum_pet_command_protect_owner));

    // tg: code/modules/mob/living/basic/icemoon/wolf/wolf.dm:55
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        ADD_TRAIT(this, TRAIT_WOUND_LICKER, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_CLAW));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_flee_while_injured)));

        if (Dm.truthy(can_tame))
            make_tameable();
        return dot;
    }

    // tg: code/modules/mob/living/basic/icemoon/wolf/wolf.dm:65
    public virtual void make_tameable()
    {
        DmList food_types = TgState.Current.mob_living_basic_mining_wolf__make_tameable__food_types ??= Dm.list(typeof(obj_item_food_meat_slab));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), Dm.named("food_types", food_types), Dm.named("tame_chance", 15), Dm.named("bonus_tame_chance", 5)));
    }

    // tg: code/modules/mob/living/basic/icemoon/wolf/wolf.dm:69
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        Dm.@new<obj_effect_temp_visual_heart>(loc);
        // ride wolf, life good
        await AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_wolf));
        await AddComponent(typeof(datum_component_obeys_commands), pet_commands);
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        // this is purely a convenience thing once tamed so you can drag them away from shit
        controller.ai_traits = Dm.num(Dm.bitor(controller.ai_traits, STOP_MOVING_WHEN_PULLED));
        // makes tamed wolves run away far less
        controller.set_blackboard_key(BB_BASIC_MOB_FLEE_DISTANCE, 7);
    }

    //port the faction fix from goliath basicmob to make the wildlife hostile when tamed (and also help defuckulate reinforcements ai)
    //this should also produce interesting behavior where tamed wolves defend other tamed wolves.
    // tg: code/modules/mob/living/basic/icemoon/wolf/wolf.dm:82
    public override async DmTask<object> befriend(mob_living new_friend = null)
    {
        object dot = await base.befriend(new_friend);
        if (dot == null)
            return dot;
        // SET_FACTION_AND_ALLIES_FROM(src, new_friend) (__DEFINES/mobfactions.dm:119), inline
        set_faction(new_friend.get_faction());
        set_allies(LAZYLISTDUPLICATE(new_friend.allies));
        visible_message(span_notice(I18n.T("[] lowers [] snout at []'s offering and begins to wag [] tail.", this, p_their(), new_friend, p_their())));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/icemoon/wolf/wolf.dm:42 (var/static/list/pet_commands)
    internal DmList mob_living_basic_mining_wolf__pet_commands;
    // tg: code/modules/mob/living/basic/icemoon/wolf/wolf.dm:65 (var/static/list/food_types)
    internal DmList mob_living_basic_mining_wolf__make_tameable__food_types;
}
