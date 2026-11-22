// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/spider/spider.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The base spider (gaps package C2: Sergeant Araneus) but its growing young (spiderlings, the antagonists' stages stay inert); the
// maintenance spider (gaps E1a: the vent clogs). Rows: the elements of a player spider and the web ability.

/**
 * Base type of various spider life stages
 */
// tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:5
public partial class mob_living_basic_spider : mob_living_basic
{
    /// Commands you can give this spider once it is tamed
    // tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:55
    private static DmList tamed_commands => TgState.Current.mob_living_basic_spider__tamed_commands ??= Dm.list(typeof(datum_pet_command_idle), typeof(datum_pet_command_free), typeof(datum_pet_command_follow), typeof(datum_pet_command_attack));

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:94
    private static DmList food_types => TgState.Current.mob_living_basic_spider__food_types ??= Dm.list(typeof(obj_item_food_meat_slab_human_mutant_lizard), typeof(obj_item_food_meat_slab_human_mutant_fly), typeof(obj_item_food_meat_slab_human_mutant_moth), typeof(obj_item_food_meat_slab_mouse), typeof(obj_item_food_meat_slab_mothroach), typeof(obj_item_food_meat_slab_blood_worm), typeof(obj_item_food_deadmouse));

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:75
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_WEB_SURFER, TRAIT_FENCE_CLIMBER), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_CLAW));
        Waiting.NoSleep(AddElement(typeof(datum_element_nerfed_pulling), GLOB.typecache_general_bad_things_to_easily_move));
        Waiting.NoSleep(AddElement(typeof(datum_element_prevent_attacking_of_types), GLOB.typecache_general_bad_hostile_attack_targets, "this tastes awful!"));
        Waiting.NoSleep(AddElement(typeof(datum_element_cliff_walking)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_health_scaling_effects), null, null, null, null, null, null, null, 1.5f));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_allergenic_attack), BUGS, 20f, 5f));

        if (Dm.truthy(poison_per_bite))
            Waiting.NoSleep(AddElement(typeof(datum_element_venomous), poison_type, poison_per_bite, null, bite_injection_flags));

        datum_action_cooldown_mob_cooldown_lay_web webbing = Dm.@new(web_type, this) as datum_action_cooldown_mob_cooldown_lay_web;
        webbing.webbing_time = Dm.num(webbing.webbing_time) * web_speed;
        Waiting.NoSleep(webbing.Grant(this));
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_SPIDER_WEB_ACTION, webbing);

        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), food_types, 20f, 10f));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:104
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;
        GLOB.spidermobs[this] = TRUE;
        await add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_player_spider_modifier), multiplicative_slowdown: player_speed_modifier);

        await AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_spider));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:113
    public override async DmTask<object> Logout()
    {
        object dot = await base.Logout();
        remove_movespeed_modifier(typeof(datum_movespeed_modifier_player_spider_modifier));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:117
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.spidermobs, this);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:121
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        Dm.@new<obj_effect_temp_visual_heart>(loc);
        await AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_spider));
        await AddComponent(typeof(datum_component_obeys_commands), tamed_commands);
    }

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:127
    public override object mob_negates_gravity()
    {
        if (Dm.locate<obj_structure_spider_stickyweb>(loc) != null)
            return TRUE;
        return base.mob_negates_gravity();
    }

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:132
    public override async DmTask<object> expose_reagents(DmList reagents = null, datum_reagents source = null, float? methods = null, float? show_message = null, params object[] __extra)
    {
        object dot = await base.expose_reagents(reagents, source, methods, show_message, __extra);
        float volume_modifier = Dm.num(Dm.arg(__extra, 0) ?? 1f);
        foreach (datum_reagent_toxin_pestkiller current_reagent in Dm.each<datum_reagent_toxin_pestkiller>(reagents))
            await apply_damage(50 * volume_modifier, STAMINA, BODY_ZONE_CHEST);
        return dot;
    }
}

public partial class datum_controller_global_vars : datum_controller
{
    /// A typecache of objects that player controlled, easily accessible, hostile mobs should not be able to attack
    // tg: code/_globalvars/lists/typecache.dm:46
    public DmList typecache_general_bad_hostile_attack_targets;

    public virtual void InitGlobaltypecache_general_bad_hostile_attack_targets()
    {
        typecache_general_bad_hostile_attack_targets = typecacheof(Dm.list(typeof(obj_machinery_airalarm), typeof(obj_machinery_atmospherics), typeof(obj_machinery_power_apc))) as DmList;
        Dm.append(ref gvars_datum_init_order, "typecache_general_bad_hostile_attack_targets");
    }

    /// A typecache of objects that player controlled, easily accessible, hostile mobs should not be able to move around easily
    // tg: code/_globalvars/lists/typecache.dm:53
    public DmList typecache_general_bad_things_to_easily_move;

    public virtual void InitGlobaltypecache_general_bad_things_to_easily_move()
    {
        typecache_general_bad_things_to_easily_move = typecacheof(Dm.list(typeof(obj_machinery_portable_atmospherics_canister), typeof(obj_structure_reagent_dispensers))) as DmList;
        Dm.append(ref gvars_datum_init_order, "typecache_general_bad_things_to_easily_move");
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_spider__tamed_commands;
    internal DmList mob_living_basic_spider__food_types;
}

// tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:197  (gaps E1a: the vent clogs' maintenance spider)
public partial class mob_living_basic_spider_maintenance : mob_living_basic_spider
{
    ///list of pet commands we follow
    // tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:222  var/static/list/pet_commands (a type-level static, rules 10.6)
    private static DmList pet_commands => TgState.Current.mob_living_basic_spider_maintenance__pet_commands ??= Dm.list(
        typeof(datum_pet_command_idle),
        typeof(datum_pet_command_free),
        typeof(datum_pet_command_follow_start_active),
        typeof(datum_pet_command_perform_trick_sequence));

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider.dm:229
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_web_walker), typeof(datum_movespeed_modifier_average_web)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));
        Waiting.NoSleep(AddElement(typeof(datum_element_tiny_mob_hunter)));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_spider_maintenance__pet_commands;
}
