// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/cat/cat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the cult (the cultist_pet element is a row) (gaps package C2).

// tg: code/modules/mob/living/basic/pets/cat/cat.dm:2
public partial class mob_living_basic_pet_cat : mob_living_basic_pet
{
    ///list of items we can carry
    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:43
    protected static DmList huntable_items => TgState.Current.mob_living_basic_pet_cat__huntable_items ??= Dm.list(typeof(obj_item_fish), typeof(obj_item_food_deadmouse), typeof(obj_item_food_fishmeat));

    ///list of pet commands we follow
    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:49
    private static DmList pet_commands => TgState.Current.mob_living_basic_pet_cat__pet_commands ??= Dm.list(typeof(datum_pet_command_idle), typeof(datum_pet_command_free), typeof(datum_pet_command_follow_start_active), typeof(datum_pet_command_perform_trick_sequence));

    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:64
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));
        Waiting.NoSleep(AddElement(typeof(datum_element_cultist_pet), null, cult_icon_state));
        Waiting.NoSleep(AddElement(typeof(datum_element_wears_collar), null, TRUE, collar_icon_state));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "purr", typeof(datum_mood_event_pet_animal)));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_CLAW));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        add_cell_sample();
        add_verb(this, typeof(mob_living).GetMethod(nameof(toggle_resting)));
        add_traits(Dm.list(TRAIT_CAT_EMOTES_ALLOWED, TRAIT_CATLIKE_INSTINCT, TRAIT_VENTCRAWLER_ALWAYS, TRAIT_WOUND_LICKER, TRAIT_COLORBLIND), INNATE_TRAIT);
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_HUNTABLE_PREY, typecacheof(huntable_items));
        if (Dm.truthy(can_breed))
            add_breeding_component();
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:80
    public virtual void add_cell_sample()
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_CAT, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:83
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;

        if (target is obj_machinery_oven_range && Dm.truthy(can_interact_with_stove))
        {
            await target.attack_hand(this);
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
        }

        if (!Dm.truthy(can_hold_item))
            return BASIC_MOB_CONTINUE_ATTACK_CHAIN;

        if (!Dm.truthy(is_type_in_list(target, huntable_items)) || held_food != null)
            return BASIC_MOB_CONTINUE_ATTACK_CHAIN;
        atom_movable movable_target = target as atom_movable;
        await movable_target.forceMove(this);
        return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:101
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone != held_food)
            return dot;
        held_food = null;
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:108
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (Dm.truthy(is_type_in_list(arrived, huntable_items)))
        {
            held_food = arrived as obj_item;
            update_appearance(UPDATE_OVERLAYS);
        }
        return await base.Entered(arrived, old_loc, old_locs);
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:114
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (stat == DEAD || Dm.truthy(resting) || held_food == null)
            return dot;
        if (held_food is obj_item_fish)
            held_item_overlay = mutable_appearance(icon, "cat_fish_overlay") as mutable_appearance;
        if (held_food is obj_item_food_deadmouse)
            held_item_overlay = mutable_appearance(icon, "cat_mouse_overlay") as mutable_appearance;
        Dm.append(ref dot, held_item_overlay);
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:124
    public override void update_resting()
    {
        base.update_resting();
        if (stat == DEAD)
            return;
        update_appearance(UPDATE_ICON_STATE);
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:130
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(resting))
        {
            icon_state = Dm.text("[]_rest", icon_living);
            return dot;
        }
        icon_state = Dm.text("[]", icon_living);
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:137
    public virtual void add_breeding_component()
    {
        DmList baby_types = Dm.list(Dm.kv(typeof(mob_living_basic_pet_cat_kitten), 1));
        Waiting.NoSleep(AddComponent(typeof(datum_component_breed), typecacheof(Dm.list(typeof(mob_living_basic_pet_cat))), null, baby_types, post_birth_callback));
    }
}

// tg: code/modules/mob/living/basic/pets/cat/cat.dm:161
public partial class mob_living_basic_pet_cat_breadcat : mob_living_basic_pet_cat
{
    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:179
    public override void add_cell_sample()
    {
    }
}

// tg: code/modules/mob/living/basic/pets/cat/cat.dm:183
public partial class mob_living_basic_pet_cat_original : mob_living_basic_pet_cat
{
    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:192
    public override void add_cell_sample()
    {
    }
}

// tg: code/modules/mob/living/basic/pets/cat/cat.dm:196
public partial class mob_living_basic_pet_cat_kitten : mob_living_basic_pet_cat
{
    // tg: code/modules/mob/living/basic/pets/cat/cat.dm:209
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), null, null, null, null, null, null, huntable_items));
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_pet_cat__huntable_items;
    internal DmList mob_living_basic_pet_cat__pet_commands;
}
