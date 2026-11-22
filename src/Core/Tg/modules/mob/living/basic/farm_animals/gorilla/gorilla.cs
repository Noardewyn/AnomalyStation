// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the Kong achievement's award (gaps package C6).

// tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:15
public partial class mob_living_basic_gorilla : mob_living_basic
{
    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:44  var/static/list/gorilla_food
    private DmList gorilla_food => TgState.Current.mob_living_basic_gorilla__gorilla_food ??= Dm.list(
        typeof(obj_item_food_bread_banana),
        typeof(obj_item_food_breadslice_banana),
        typeof(obj_item_food_cnds_banana_honk),
        typeof(obj_item_food_grown_banana),
        typeof(obj_item_food_popsicle_topsicle_banana),
        typeof(obj_item_food_salad_fruit),
        typeof(obj_item_food_salad_jungle),
        typeof(obj_item_food_sundae));

    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:57
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_SIMIAN, TRAIT_ADVANCEDTOOLUSER, TRAIT_CAN_STRIP, TRAIT_CHUNKYFINGERS), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_wall_tearer), Dm.named("allow_reinforced", FALSE)));
        Waiting.NoSleep(AddElement(typeof(datum_element_dextrous), Dm.named("can_throw", TRUE)));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_BAREFOOT));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), Dm.named("heal_amt", 10f), Dm.named("food_types", gorilla_food)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_amputating_limbs), Dm.named("surgery_time", 0f), Dm.named("surgery_verb", "punches")));
        Waiting.NoSleep(AddComponent(typeof(datum_component_personal_crafting)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_basic_inhands), Dm.named("y_offset", -1f)));
        Waiting.NoSleep(AddElement(typeof(datum_element_strippable), GLOB.strippable_gorilla_items));

        RegisterSignal(this, COMSIG_MOB_EMOTED("ooga"), nameof(check_kong_state));

        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_FOODS, typecacheof(gorilla_food));
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:77
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FITNESS))
            return dot;
        Dm.append(ref dot, span_notice("This animal appears to be in peak physical condition and yet it has probably never worked out a day in its life. The untapped potential is almost frightening."));
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:84
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(is_holding_items()))
            Dm.append(ref dot, "standing_overlay");
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:89
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (stat == DEAD)
            return dot;
        icon_state = Dm.truthy(is_holding_items()) ? "standing" : "crawling";
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:95
    public override async DmTask update_held_items()
    {
        await base.update_held_items();
        update_appearance(UPDATE_ICON);
        if (Dm.truthy(is_holding_items()))
            await add_movespeed_modifier(typeof(datum_movespeed_modifier_gorilla_standing));
        else
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_gorilla_standing));
    }

    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:103
    public override async DmTask<object> melee_attack(atom target_ = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.melee_attack(target_, modifiers, ignore_cooldown);
        if (!Dm.truthy(dot) || target_ is not mob_living target)
            return dot;
        await ooga_ooga();
        if (prob(paralyze_chance))
        {
            await target.Paralyze(2 * SECONDS);
            visible_message(span_danger(Dm.text("[] knocks [] down!", this, target)));
        }
        else
            await target.throw_at(get_edge_target_turf(target, dir) as atom, rand(1, 2), 7, this);
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:114
    public override async DmTask gib(float? drop_bitflags = null)
    {
        drop_bitflags ??= DROP_BRAIN;
        if (!Dm.truthy(Dm.bitand(drop_bitflags, DROP_BRAIN)))
        {
            await base.gib(drop_bitflags);
            return;
        }
        mob_living_brain gorilla_brain = Dm.@new<mob_living_brain>(drop_location());
        gorilla_brain.name = real_name as string;
        gorilla_brain.real_name = real_name;
        if (mind != null)
            await mind.transfer_to(gorilla_brain);
        await base.gib(drop_bitflags);
    }

    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:123
    public override object can_use_guns(obj_item G = null)
    {
        to_chat(this, span_warning("Your meaty finger is much too large for the trigger guard!"));
        return FALSE;
    }

    /// Assert your dominance with audio cues
    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:128
    public virtual async DmTask ooga_ooga()
    {
        if (client == null)
            return; // Sorry NPCs
        oogas -= 1;
        if (oogas > 0)
            return;
        oogas = rand(2, 6);
        await emote("ooga");
    }

    /// Check how well we are roleplaying as King Kong
    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:138
    [SignalHandler]
    public virtual void check_kong_state()
    {
        if (!HAS_TRAIT(this, TRAIT_MOB_ELEVATED))
            return;
        if (pulling is not mob_living pulled || !Dm.eq(pulled.gender, FEMALE) || Dm.num(grab_state) < GRAB_AGGRESSIVE)
            return;
        // client?.give_award(/datum/award/achievement/misc/kong, src): the achievements wait (a row)
    }
}

// tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:172
public partial class mob_living_basic_gorilla_cargorilla : mob_living_basic_gorilla
{
    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:182
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_PACIFISM, INNATE_TRAIT);
        Waiting.NoSleep(AddComponent(typeof(datum_component_crate_carrier)));
        return dot;
    }
}

/// big fucking gorilla version of pun pun. be afraid.
// tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:188
public partial class mob_living_basic_gorilla_bar : mob_living_basic_gorilla
{
    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:198
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_PAWN_POSSESSED_BY_AI_CONTROLLER, nameof(on_possessed_by_ai_controller));
        Waiting.NoSleep(gorrilify_punpun_ai());

        if (GLOB.the_one_and_only_punpun == null)
            GLOB.the_one_and_only_punpun = this;
        return dot;
    }

    /// Signal handler for when an ai controller possesses us, reapplies our gorilla-specific ai tweaks if it's a monkey controller
    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:206
    [SignalHandler]
    public virtual void on_possessed_by_ai_controller(datum source = null, datum_ai_controller source_controller = null)
    {
        if (source_controller is not datum_ai_controller_monkey)
            return;
        Waiting.NoSleep(gorrilify_punpun_ai());
    }

    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:212
    public virtual async DmTask gorrilify_punpun_ai()
    {
        if (ai_controller is not datum_ai_controller controller)
            return;
        controller.override_blackboard_key(BB_SONG_LINES, GORILLA_SONG);
        controller.override_blackboard_key(BB_EMOTE_KEY, "ooga");
        controller.override_blackboard_key(BB_EMOTE_CHANCE, 50f);
        await controller.set_behavior_tree_override(SUBPLAN_ID_MONKEY_COMBAT, typeof(datum_bt_node_subtree_bar_gorilla_combat));
    }

    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:218
    public override object Destroy(float? force = null)
    {
        if (GLOB.the_one_and_only_punpun == this)
            GLOB.the_one_and_only_punpun = null;
        return base.Destroy(force);
    }
}

// tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:225
public partial class mob_living_basic_gorilla_genetics : mob_living_basic_gorilla
{
    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:249
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        qdel(GetComponent(typeof(datum_component_amputating_limbs)));
        return dot;
    }
}

public partial class datum_controller_global_vars
{

    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:6  GLOBAL_LIST_INIT(strippable_gorilla_items, create_strippable_list(...))
    public DmList strippable_gorilla_items;

    // tg: code/modules/mob/living/basic/farm_animals/gorilla/gorilla.dm:6
    public virtual void InitGlobalstrippable_gorilla_items()
    {
        strippable_gorilla_items = create_strippable_list(Dm.list(typeof(datum_strippable_item_hand_left), typeof(datum_strippable_item_hand_right))) as DmList;
        Dm.append(ref gvars_datum_init_order, "strippable_gorilla_items");
    }
}

public partial class TgState
{
    internal DmList mob_living_basic_gorilla__gorilla_food;
}
