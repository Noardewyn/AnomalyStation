// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/dog/_dog.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the cult (the cultist_pet element is a row) (gaps package C2).

// tg: code/modules/mob/living/basic/pets/dog/_dog.dm:12
public partial class datum_pet_command_attack_dog : datum_pet_command_attack
{
    // tg: code/modules/mob/living/basic/pets/dog/_dog.dm:14
    public override async DmTask set_command_active(mob_living parent = null, mob_living commander = null, float? radial_command = null)
    {
        await base.set_command_active(parent, commander, radial_command);
        (parent.ai_controller as datum_ai_controller).set_blackboard_key(BB_DOG_HARASS_HARM, TRUE);
    }
}

//Dogs.

// tg: code/modules/mob/living/basic/pets/dog/_dog.dm:21
public partial class mob_living_basic_pet_dog : mob_living_basic_pet
{
    /// Instructions you can give to dogs
    // tg: code/modules/mob/living/basic/pets/dog/_dog.dm:41
    private static DmList pet_commands => TgState.Current.mob_living_basic_pet_dog__pet_commands ??= Dm.list(
        typeof(datum_pet_command_idle),
        typeof(datum_pet_command_free),
        typeof(datum_pet_command_move),
        typeof(datum_pet_command_good_boy_dog),
        typeof(datum_pet_command_follow_dog),
        typeof(datum_pet_command_perform_trick_sequence),
        typeof(datum_pet_command_attack_dog),
        typeof(datum_pet_command_fetch),
        typeof(datum_pet_command_play_dead));

    // tg: code/modules/mob/living/basic/pets/dog/_dog.dm:70
    private static DmList food_types => TgState.Current.mob_living_basic_pet_dog__food_types ??= Dm.list(typeof(obj_item_food_meat_slab_human_mutant_skeleton), typeof(obj_item_stack_sheet_bone));

    // tg: code/modules/mob/living/basic/pets/dog/_dog.dm:67
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_cultist_pet), null, cult_icon_state));
        Waiting.NoSleep(AddElement(typeof(datum_element_wears_collar), null, null, collar_icon_state));
        ADD_TRAIT(this, TRAIT_WOUND_LICKER, INNATE_TRAIT);
        ADD_TRAIT(this, TRAIT_COLORBLIND, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "woof"));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_CLAW));
        Waiting.NoSleep(AddElement(typeof(datum_element_unfriend_attacker), "%SOURCE% fixes %TARGET% with a look of betrayal."));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), food_types, 30f, 15f, FALSE));
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));
        area dog_area = get_area(this) as area;
        if (dog_area == null)
            return dot;
        // RULE-GAP: tg's `for(var/obj/structure/bed/dogbed/dog_bed in dog_area)`: the port's area.contents lists only its turfs (Builtin/area.builtin.cs)
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(dog_area.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each_cast<turf>(zlevel_turfs))
            {
                foreach (obj_structure_bed_dogbed dog_bed in Dm.each<obj_structure_bed_dogbed>(area_turf))
                {
                    if (Dm.truthy(dog_bed.update_owner(this))) //No muscling in on my turf you fucking parrot
                        return dot;
                }
            }
        }
        return dot;
    }

    ///Updates dog speech and emotes
    // tg: code/modules/mob/living/basic/pets/dog/_dog.dm:89
    public virtual void update_dog_speech(DmList speech_data = null)
    {
        speech_data[BB_EMOTE_SAY] = string_list(Dm.list("YAP", "Woof!", "Bark!", "AUUUUUU"));
        speech_data[BB_EMOTE_HEAR] = string_list(Dm.list("barks!", "woofs!", "yaps.", "pants."));
        speech_data[BB_EMOTE_SEE] = string_list(Dm.list(Dm.text("shakes [] head.", p_their()), Dm.text("chases [] tail.", p_their()), "shivers."));
    }

    /// Populates BB_BASIC_MOB_SPEAK_LINES with the dog's current speech data for BT random speech.
    /// Subtypes override this to apply fashion accessories or variant speech.
    // tg: code/modules/mob/living/basic/pets/dog/_dog.dm:97
    public virtual void update_dog_speak_blackboard(datum_ai_controller controller = null)
    {
        DmList speech_data = Dm.list();
        speech_data[BB_EMOTE_SAY] = Dm.list("YAP", "Woof!", "Bark!", "AUUUUUU");
        speech_data[BB_EMOTE_HEAR] = Dm.list("barks!", "woofs!", "yaps.", "pants.");
        speech_data[BB_EMOTE_SEE] = Dm.list(Dm.text("shakes [] head.", p_their()), Dm.text("chases [] tail.", p_their()), "shivers.");
        speech_data[BB_SPEAK_CHANCE] = 1;
        controller.override_blackboard_key(BB_BASIC_MOB_SPEAK_LINES, speech_data);
    }

    ///Proc to run on a successful taming attempt
    // tg: code/modules/mob/living/basic/pets/dog/_dog.dm:106
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        visible_message(span_notice(Dm.text("[] licks at [] in a friendly manner!", this, tamer)));
    }
}

/// A dog bone fully heals a dog, and befriends it if it's not your friend.
// tg: code/modules/mob/living/basic/pets/dog/_dog.dm:112
public partial class obj_item_dog_bone : obj_item
{
    // tg: code/modules/mob/living/basic/pets/dog/_dog.dm:123
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (target is not mob_living_basic_pet_dog dog_target || Dm.truthy(user.combat_mode))
            return await base.pre_attack(target, user, modifiers, attack_modifiers);
        if (IS_UNCONSCIOUS_OR_CRIT(dog_target))
            return await base.pre_attack(target, user, modifiers, attack_modifiers);
        await dog_target.emote("spin");
        await dog_target.fully_heal();
        if (Dm.truthy(await dog_target.befriend(user)))
            await dog_target.tamed(user);
        Dm.@new<obj_effect_temp_visual_heart>(target.loc);
        qdel(this);
        return TRUE;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_pet_dog__pet_commands;
    internal DmList mob_living_basic_pet_dog__food_types;
}
