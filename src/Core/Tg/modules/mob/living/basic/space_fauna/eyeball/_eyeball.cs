// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/eyeball/_eyeball.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/eyeball/_eyeball.dm:1
public partial class mob_living_basic_eyeball : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/eyeball/_eyeball.dm:53
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        grant_actions_by_list(Dm.list(Dm.kv(typeof(datum_action_cooldown_spell_pointed_death_glare), BB_GLARE_ABILITY)));

        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        DmList food_types = string_list(Dm.list(typeof(obj_item_food_grown_carrotlike_carrot))) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), Dm.named("food_types", food_types), Dm.named("tame_chance", 100f)));
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        on_hit_overlay = mutable_appearance(icon, Dm.text("[]_crying", icon_state)) as mutable_appearance;
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/eyeball/_eyeball.dm:66
    public override async DmTask<object> UnarmedAttack(atom A = null, object proximity_flag = null, DmList modifiers = null)
    {
        object dot = await base.UnarmedAttack(A, proximity_flag, modifiers);
        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(proximity_flag))
            return dot;

        if (A is obj_item_food_grown_carrotlike_carrot)
        {
            await adjust_brute_loss(-5);
            to_chat(this, span_warning(Dm.text("You eat []! It restores some health!", A)));
            qdel(A);
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/eyeball/_eyeball.dm:80
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attackby(attacking_item, user, modifiers, attack_modifiers);
        if (!Dm.truthy(attacking_item.force) && !Dm.truthy((user as mob_living)?.combat_mode))
            return dot;
        if (Dm.truthy(crying))
            return dot;
        change_crying_state();
        _addtimer(Dm.@new<datum_callback>(this, nameof(change_crying_state)), 10 * SECONDS, file: "code/modules/mob/living/basic/space_fauna/eyeball/_eyeball.dm", line: 87); //cry for 10 seconds then remove
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/eyeball/_eyeball.dm:89
    public virtual void change_crying_state()
    {
        crying = Dm.truthy(crying) ? FALSE : TRUE;
        if (Dm.truthy(crying))
        {
            add_overlay(on_hit_overlay);
            return;
        }
        cut_overlay(on_hit_overlay);
    }

    // tg: code/modules/mob/living/basic/space_fauna/eyeball/_eyeball.dm:97
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;
        if (target is not mob_living_carbon_human human_target)
            return BASIC_MOB_CONTINUE_ATTACK_CHAIN;
        obj_item_organ_eyes eyes = human_target.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes == null || Dm.num(eyes.damage) < 10)
            return BASIC_MOB_CONTINUE_ATTACK_CHAIN;
        await heal_eye_damage(human_target, eyes);
        return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
    }

    // tg: code/modules/mob/living/basic/space_fauna/eyeball/_eyeball.dm:110
    public virtual async DmTask heal_eye_damage(mob_living target = null, obj_item_organ_eyes eyes = null)
    {
        if (eye_healing > world.time) // !COOLDOWN_FINISHED
            return;
        to_chat(target, span_warning(Dm.text("[] seems to be healing your []!", this, eyes.zone)));
        await eyes.apply_organ_damage(-1 * healing_factor);
        Dm.@new<obj_effect_temp_visual_heal>(get_turf(target), COLOR_HEALING_CYAN);
        await befriend(target);
        eye_healing = world.time + 15 * SECONDS;
    }

    // tg: code/modules/mob/living/basic/space_fauna/eyeball/_eyeball.dm:119
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        spin(2 * SECONDS, 1f);
        //become passive to the humens
        add_faction(tamer.get_faction()); // APPLY_FACTION_AND_ALLIES_FROM (__DEFINES/mobfactions.dm:135), inline
        add_ally(LAZYLISTDUPLICATE(tamer.allies));
    }
}
