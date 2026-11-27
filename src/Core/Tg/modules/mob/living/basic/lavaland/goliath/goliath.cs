// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/goliath/goliath.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Slow moving mob which attempts to immobilise its target
// tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:2
public partial class mob_living_basic_mining_goliath : mob_living_basic_mining
{
    /// Things we want to eat off the floor (or a plate, we're not picky)
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:48 (var/static/list/goliath_foods)
    public static DmList goliath_foods => TgState.Current.mob_living_basic_mining_goliath__goliath_foods ??= Dm.list(typeof(obj_item_food_grown_ash_flora), typeof(obj_item_food_bait_worm));

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_TENTACLE_IMMUNE, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_HEAVY));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), Dm.named("heal_amt", 10), Dm.named("food_types", goliath_foods)));
        Waiting.NoSleep(AddElement(
            typeof(datum_element_change_force_on_death),
            Dm.named("move_force", MOVE_FORCE_DEFAULT),
            Dm.named("move_resist", MOVE_RESIST_DEFAULT),
            Dm.named("pull_force", PULL_FORCE_DEFAULT)));

        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_target_timer)));
        Waiting.NoSleep(AddComponentFrom(INNATE_TRAIT, typeof(datum_component_shovel_hands)));
        if (Dm.truthy(tameable))
            Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), Dm.named("tame_chance", 10), Dm.named("bonus_tame_chance", 5)));

        datum_ai_controller controller = ai_controller as datum_ai_controller;
        tentacles = Dm.@new<datum_action_cooldown_mob_cooldown_goliath_tentacles>(this);
        Waiting.NoSleep(tentacles.Grant(this));
        melee_tentacles = Dm.@new<datum_action_cooldown_mob_cooldown_tentacle_burst>(this);
        Waiting.NoSleep(melee_tentacles.Grant(this));
        Waiting.NoSleep(AddComponent(typeof(datum_component_revenge_ability), melee_tentacles, Dm.named("targeting", GET_TARGETING_STRATEGY(controller.blackboard[BB_TARGETING_STRATEGY])), Dm.named("max_range", 1), Dm.named("target_self", TRUE)));
        tentacle_line = Dm.@new<datum_action_cooldown_mob_cooldown_tentacle_grasp>(this);
        Waiting.NoSleep(tentacle_line.Grant(this));
        Waiting.NoSleep(AddComponent(typeof(datum_component_revenge_ability), tentacle_line, Dm.named("targeting", GET_TARGETING_STRATEGY(controller.blackboard[BB_TARGETING_STRATEGY])), Dm.named("min_range", 2), Dm.named("max_range", 9)));

        tentacles_ready();
        RegisterSignal(this, COMSIG_MOB_ABILITY_FINISHED, nameof(used_ability));
        controller.set_blackboard_key(BB_BASIC_FOODS, typecacheof(goliath_foods));
        controller.set_blackboard_key(BB_GOLIATH_TENTACLES, tentacles);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:82
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref tentacles);
        QDEL_NULL(ref melee_tentacles);
        QDEL_NULL(ref tentacle_line);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:88
    public override object get_hud_x_offset()
    {
        return -4;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:91
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(saddled))
            Dm.append(ref dot, span_info(I18n.T("Someone appears to have attached a saddle to this one.")));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:117
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_goliath_saddle))
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (!Dm.truthy(tameable))
        {
            balloon_alert(user, "doesn't fit!");
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(saddled))
        {
            balloon_alert(user, "already saddled!");
            return ITEM_INTERACT_BLOCKING;
        }

        if (!HAS_TRAIT(this, TRAIT_TAMED))
        {
            balloon_alert(user, "too rowdy!");
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, "affixing saddle...");
        if (!Dm.truthy(await do_after(user, delay: 5.5f * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, "ready to ride");
        qdel(tool);
        await make_rideable();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:142
    public virtual async DmTask make_rideable()
    {
        saddled = TRUE;
        add_overlay("goliath_saddled");
        await AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_goliath));
    }

    /// When we use an ability, activate some kind of visual tell
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:148
    [SignalHandler]
    public virtual void used_ability(mob_living source = null, datum_action_cooldown ability = null)
    {
        if (stat == DEAD || Dm.truthy(Waiting.NoSleep(ability.IsAvailable())))
            return; // We died or the action failed for some reason like being out of range
        if (ability is datum_action_cooldown_mob_cooldown_goliath_tentacles)
        {
            if (Dm.num(ability.cooldown_time) <= 2 * SECONDS)
                return;
            icon_state = icon_living as string;
            _addtimer(Dm.@new<datum_callback>(this, nameof(tentacles_ready)), Dm.num(ability.cooldown_time) - 2 * SECONDS, TIMER_DELETE_ME, file: "code/modules/mob/living/basic/lavaland/goliath/goliath.dm", line: 156);
            return;
        }
        if (!(ability_animation_cooldown <= world.time)) // COOLDOWN_FINISHED(src, ability_animation_cooldown), inline (rules 11.1 (a))
            return;
        ability_animation_cooldown = world.time + 2 * SECONDS; // COOLDOWN_START(src, ability_animation_cooldown, 2 SECONDS)
        playsound(this, Dm.resource("sound/effects/magic/demon_attack1.ogg"), vol: 50, vary: TRUE);
        Shake(1, 0, 1.5f * SECONDS);
    }

    /// Called slightly before tentacles ability comes off cooldown, as a warning
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:165
    public virtual void tentacles_ready()
    {
        if (stat == DEAD)
            return;
        icon_state = tentacle_warning_state;
    }

    // Copy entire faction rather than just placing user into faction, to avoid tentacle peril on station
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:171
    public override async DmTask<object> befriend(mob_living new_friend = null)
    {
        object dot = await base.befriend(new_friend);
        if (dot == null)
            return dot;
        // SET_FACTION_AND_ALLIES_FROM(src, new_friend) (__DEFINES/mobfactions.dm:119), inline
        set_faction(new_friend.get_faction());
        set_allies(LAZYLISTDUPLICATE(new_friend.allies));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:177
    public override async DmTask<object> RangedAttack(atom atom_target = null, object modifiers = null)
    {
        if (tentacles != null)
            await tentacles.Trigger(null, null, atom_target);
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:180
    public override async DmTask<object> ranged_secondary_attack(atom atom_target = null, object modifiers = null)
    {
        if (tentacle_line != null)
            await tentacle_line.Trigger(null, null, atom_target);
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:183
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (stat != DEAD)
            Dm.Deferred("code/modules/mob/living/basic/lavaland/goliath/goliath.dm:186", "update_overlays: emissive_appearance(icon, \"[icon_living]_e\", src), the goliath's glow in the dark (DEVIATION DV12)");
        return dot;
    }
}

// Goliaths can summon tentacles more frequently as they take damage, scary.
// tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:97 (tg declares this override on a subtype named apply_damage, so the plain goliath never runs it; kept as tg has it)
public partial class mob_living_basic_mining_goliath_apply_damage : mob_living_basic_mining_goliath
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:97
    public override async DmTask<object> apply_damage(float? damage = null, string damagetype = null, object def_zone = null, float? blocked = null, float? forced = null, float? spread_damage = null, float? wound_bonus = null, float? exposed_wound_bonus = null, float? sharpness = null, object attack_direction = null, object attacking_item = null, float? wound_clothing = null)
    {
        damage ??= 0;
        damagetype ??= BRUTE;
        blocked ??= 0;
        forced ??= FALSE;
        spread_damage ??= FALSE;
        wound_bonus ??= 0;
        exposed_wound_bonus ??= 0;
        sharpness ??= NONE;
        wound_clothing ??= TRUE;
        object dot = await base.apply_damage(damage, damagetype, def_zone, blocked, forced, spread_damage, wound_bonus, exposed_wound_bonus, sharpness, attack_direction, attacking_item, wound_clothing);
        if (Dm.num(dot) <= 0)
            return dot;
        if (Dm.num(tentacles.cooldown_time) > 1 * SECONDS)
            tentacles.cooldown_time = Dm.num(tentacles.cooldown_time) - 1 * SECONDS;
        return dot;
    }
}

/// Version of the goliath that already starts saddled and doesn't require a lasso to be ridden.
// tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:189
public partial class mob_living_basic_mining_goliath_deathmatch : mob_living_basic_mining_goliath
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:193
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(make_rideable());
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:197
    public override async DmTask make_rideable()
    {
        add_overlay("goliath_saddled");
        await AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_goliath_deathmatch));
    }
}

/// Rare Goliath variant which occasionally replaces the normal mining mob, releases shitloads of tentacles
// tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:213
public partial class mob_living_basic_mining_goliath_ancient_immortal : mob_living_basic_mining_goliath_ancient
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:224
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.Life(seconds_per_tick, __extra);
        if (!Dm.truthy(dot) || !isturf(loc))
            return dot;
        if (!Dm.truthy(LAZYLEN(tentacle_target_turfs)) || retarget_turfs_cooldown <= world.time) // COOLDOWN_FINISHED(src, retarget_turfs_cooldown)
            cache_nearby_turfs();
        foreach (turf target_turf in Dm.each<turf>(tentacle_target_turfs))
        {
            if (Dm.truthy(Waiting.NoSleep(target_turf.is_blocked_turf(exclude_mobs: TRUE))))
            {
                Dm.remove(ref tentacle_target_turfs, target_turf);
                continue;
            }
            if (prob(10))
                Dm.@new<obj_effect_goliath_tentacle>(target_turf, this);
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:237
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (loc == old_loc || stat == DEAD || !isturf(loc))
            return dot;
        cache_nearby_turfs();
        return dot;
    }

    /// Store nearby turfs in our list so we can pop them out later
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath.dm:244
    public virtual void cache_nearby_turfs()
    {
        retarget_turfs_cooldown = world.time + 5 * SECONDS; // COOLDOWN_START(src, retarget_turfs_cooldown, 5 SECONDS)
        LAZYCLEARLIST(tentacle_target_turfs);
        foreach (turf_open floor in Dm.each<turf_open>(orange(4, loc)))
            LAZYADD(ref tentacle_target_turfs, floor);
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_mining_goliath__goliath_foods;
}
