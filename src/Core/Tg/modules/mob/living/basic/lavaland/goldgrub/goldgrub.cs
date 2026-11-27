// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


//An ore-devouring but easily scared creature
// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:2
public partial class mob_living_basic_mining_goldgrub : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(mapload))
            generate_loot();
        else
            can_lay_eggs = FALSE;

        // Rarer ores heal us a bit depending on the rarity
        DmList food_types = TgState.Current.mob_living_basic_mining_goldgrub__Initialize__food_types ??= Dm.list(
            Dm.kv(typeof(obj_item_stack_ore), 0),
            Dm.kv(typeof(obj_item_stack_ore_gold), 5),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 5),
            Dm.kv(typeof(obj_item_stack_ore_diamond), 15),
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal), 20),
            Dm.kv(typeof(obj_item_stack_ore_bananium), 30));

        DmList innate_actions = TgState.Current.mob_living_basic_mining_goldgrub__Initialize__innate_actions ??= Dm.list(
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_spit_ore), BB_SPIT_ABILITY),
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_burrow), BB_BURROW_ABILITY));

        grant_actions_by_list(innate_actions);

        Waiting.NoSleep(AddElement(typeof(datum_element_ore_collecting)));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), Dm.named("food_types", food_types), Dm.named("add_to_contents", TRUE)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_proficient_miner), 0.05f, TRUE)); // Speedy boi
        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_listen_to_weather)));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_appearance_on_aggro),
            Dm.named("overlay_icon", Dm.resource("icons/mob/simple/lavaland/lavaland_monsters_wide.dmi")),
            Dm.named("overlay_state", "goldgrub_alert")));

        if (Dm.truthy(can_tame))
            make_tameable(food_types);
        if (Dm.truthy(can_lay_eggs))
            make_egg_layer();

        ADD_TRAIT(this, TRAIT_BOULDER_BREAKER, INNATE_TRAIT);
        ADD_TRAIT(this, TRAIT_INSTANTLY_PROCESSES_BOULDERS, INNATE_TRAIT);
        RegisterSignal(this, COMSIG_ATOM_PRE_BULLET_ACT, nameof(block_bullets));
        if (Dm.truthy(has_emissive))
            update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:90
    public override object get_hud_x_offset()
    {
        return -4;
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:93
    [SignalHandler]
    public virtual object block_bullets(datum source = null, obj_projectile hitting_projectile = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(this))
            return NONE;

        /// Reflects PKA/PKC shots and plasma cutter beams, unless they have high armor penetration
        if (prob(Dm.num(hitting_projectile.armour_penetration)) || (!Dm.eq(hitting_projectile.armor_flag, BOMB) && !Dm.eq(hitting_projectile.armor_flag, ENERGY)))
            return NONE;

        visible_message(span_danger(I18n.T("[] is repelled by []'s girth!", hitting_projectile, source)));
        return COMPONENT_BULLET_BLOCKED;
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:106
    public virtual void barf_contents(object gibbed = null)
    {
        playsound(this, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
        foreach (obj_item_stack_ore ore in Dm.each<obj_item_stack_ore>(contents))
            Waiting.NoSleep(ore.forceMove(loc));

        if (!Dm.truthy(gibbed))
            visible_message(span_danger(I18n.T("[] spits out its consumed ores!", this)));
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:114
    public virtual void generate_loot()
    {
        float loot_amount = rand(1, 3);
        DmList weight_lootdrops = Dm.list(
            Dm.kv(typeof(obj_item_stack_ore_silver), 4),
            Dm.kv(typeof(obj_item_stack_ore_gold), 3),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 3),
            Dm.kv(typeof(obj_item_stack_ore_diamond), 1));
        for (float i = 1; i <= loot_amount; i++)
        {
            Type picked_loot = pick_weight(weight_lootdrops) as Type;
            Dm.@new(picked_loot, this);
        }
    }

    // The first definition of death (tg defines it twice on this type; the second's ..() calls this one, rules 2.5 and 7.5).
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:126
    public async DmTask<object> death__d0(object gibbed = null, params object[] __extra)
    {
        barf_contents(gibbed);
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:130
    public virtual void make_tameable(DmList food_types = null)
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), Dm.named("food_types", food_types), Dm.named("tame_chance", 25), Dm.named("bonus_tame_chance", 5)));
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:133
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        Dm.@new<obj_effect_temp_visual_heart>(loc);
        await AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_goldgrub));
        await AddComponent(typeof(datum_component_obeys_commands), pet_commands);
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:139
    public virtual void make_egg_layer()
    {
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_egg_layer),
            typeof(obj_item_food_egg_green_grub_egg),
            Dm.list(typeof(obj_item_stack_ore_bluespace_crystal)),
            Dm.named("lay_messages", Dm.list("lays an egg.", "squats down and croons.", "begins making a huge racket.", "begins clucking raucously.")), // EGG_LAYING_MESSAGES (__DEFINES/botany.dm:101)
            Dm.named("eggs_left", 0),
            Dm.named("eggs_added_from_eating", 1),
            Dm.named("max_eggs_held", 1)));
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:150
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(has_emissive))
            Dm.Deferred("code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:151", "update_overlays: emissive_appearance(icon, \"[icon_state]_e\", src), the goldgrub's glow in the dark (DEVIATION DV12)");
        return dot;
    }

    // The second definition of death (rules 2.5: its ..() is the first definition, death__d0).
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:155
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await death__d0(gibbed, __extra);
        if (!QDELETED(this) && Dm.truthy(has_emissive))
            update_appearance(UPDATE_OVERLAYS);
        return dot;
    }
}

// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:158
public partial class mob_living_basic_mining_goldgrub_baby : mob_living_basic_mining_goldgrub
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:177
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_growth_and_differentiation),
            Dm.named("growth_time", 5 * MINUTES),
            Dm.named("growth_path", typeof(mob_living_basic_mining_goldgrub)),
            Dm.named("growth_probability", 100),
            Dm.named("lower_growth_value", 0.5f),
            Dm.named("upper_growth_value", 1),
            Dm.named("signals_to_kill_on", Dm.list(COMSIG_MOB_CLIENT_LOGIN)),
            Dm.named("optional_checks", Dm.@new<datum_callback>(this, nameof(ready_to_grow)))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:190
    public virtual object ready_to_grow()
    {
        return (!IS_UNCONSCIOUS_OR_CRIT(this) && !is_jaunting(this)) ? TRUE : FALSE;
    }
}

// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:191
public partial class obj_item_food_egg_green_grub_egg : obj_item_food_egg_green
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:198
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_fertile_egg),
            Dm.named("embryo_type", typeof(mob_living_basic_mining_goldgrub_baby)),
            Dm.named("minimum_growth_rate", 1),
            Dm.named("maximum_growth_rate", 2),
            Dm.named("total_growth_required", 100),
            Dm.named("current_growth", 0),
            Dm.named("location_allowlist", typecacheof(Dm.list(typeof(turf))))));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:53 (var/static/list/food_types)
    internal DmList mob_living_basic_mining_goldgrub__Initialize__food_types;
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub.dm:62 (var/static/list/innate_actions)
    internal DmList mob_living_basic_mining_goldgrub__Initialize__innate_actions;
}
