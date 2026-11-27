// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Cowardly mob with a charging attack
// tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:2
public partial class mob_living_basic_mining_lobstrosity : mob_living_basic_mining
{
    /// Things we will eat if we see them (arms, chiefly)
    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:40 (var/static/list/target_foods)
    public static DmList target_foods => TgState.Current.mob_living_basic_mining_lobstrosity__target_foods ??= Dm.list(typeof(obj_item_bodypart_arm), typeof(obj_item_fish_lavaloop));

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_NODROWN, TRAIT_SWIMMER), INNATE_TRAIT);
        // AddComponent(/datum/component/profound_fisher): the lobstrosity's fishing in lava (fishing)
        Dm.Deferred("code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:45", "Initialize: AddComponent(/datum/component/profound_fisher), the lobstrosity's fishing (fishing is not ported)");
        Waiting.NoSleep(AddElement(typeof(datum_element_mob_grabber)));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_CLAW));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), Dm.named("food_types", target_foods)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", strings("crustacean_replacement.json", "crustacean"))));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_amputating_limbs),
            Dm.named("surgery_time", snip_speed),
            Dm.named("surgery_verb", "begins snipping"),
            Dm.named("target_zones", GLOB.arm_zones)));
        charge = Dm.@new(charge_type, this) as datum_action_cooldown_mob_cooldown_charge_basic_charge_lobster;
        Waiting.NoSleep(charge.Grant(this));
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        controller.set_blackboard_key(BB_TARGETED_ACTION, charge);
        DmList fishable_turfs = TgState.Current.mob_living_basic_mining_lobstrosity__Initialize__fishable_turfs ??= typecacheof(Dm.list(typeof(turf_open_lava))) as DmList;
        controller.set_blackboard_key(BB_FISHABLE_LIST, fishable_turfs);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:63
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref charge);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:67
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (stat != DEAD)
            Dm.Deferred("code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:70", "update_overlays: emissive_appearance(icon, \"[icon_living]_e\", src), the lobstrosity's glow in the dark (DEVIATION DV12)");
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:72
    public override async DmTask<object> ranged_secondary_attack(atom atom_target = null, object modifiers = null)
    {
        await charge.Trigger(null, null, atom_target);
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:75
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        Dm.@new<obj_effect_temp_visual_heart>(loc);
        /// Pet commands for this mob, however you'll have to tame juvenile lobstrosities to a trained adult one.
        DmList pet_commands = Dm.list(
            typeof(datum_pet_command_idle),
            typeof(datum_pet_command_free),
            typeof(datum_pet_command_move),
            typeof(datum_pet_command_attack),
            charge_command,
            typeof(datum_pet_command_follow_start_active),
            typeof(datum_pet_command_fish));
        await AddComponent(typeof(datum_component_happiness));
        await AddComponent(typeof(datum_component_obeys_commands), pet_commands);
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        controller.ai_traits = Dm.bitor(controller.ai_traits, STOP_MOVING_WHEN_PULLED);
        response_help_continuous = "pets";
        response_help_simple = "pet";
        response_disarm_continuous = "gently pushes aside";
        response_disarm_simple = "gently push aside";
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:96
    public override async DmTask<object> befriend(mob_living new_friend = null)
    {
        object dot = await base.befriend(new_friend);
        if (dot == null)
            return dot;
        // APPLY_FACTION_AND_ALLIES_FROM(src, new_friend) (__DEFINES/mobfactions.dm:135), inline
        add_faction(new_friend.get_faction());
        add_ally(LAZYLISTDUPLICATE(new_friend.allies));
        remove_faction(FACTION_MINING);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:103
    public override async DmTask mind_initialize()
    {
        await base.mind_initialize();
        if (Dm.num(mind.get_skill_level(typeof(datum_skill_fishing))) < base_fishing_level)
            mind.set_level(typeof(datum_skill_fishing), base_fishing_level, TRUE);
    }
}

/// Charge a long way, knock down for longer, and perform an instant melee attack
// tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:117
public partial class datum_action_cooldown_mob_cooldown_charge_basic_charge_lobster : datum_action_cooldown_mob_cooldown_charge_basic_charge
{
    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:122
    public override async DmTask hit_target(atom_movable source = null, object target = null, object damage_dealt = null)
    {
        await base.hit_target(source, target, damage_dealt);
        if (!isbasicmob(source) || !isliving(target))
            return;
        mob_living_basic basic_source = source as mob_living_basic;
        mob_living living_target = target as mob_living;
        await basic_source.melee_attack(living_target, ignore_cooldown: TRUE);
        (basic_source.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_MOB_STOP_FLEEING, TRUE);
        await basic_source.start_pulling(living_target);
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:132
    public override async DmTask<object> do_charge(atom_movable charger = null, atom target_atom = null, object delay = null, object past = null)
    {
        object dot = await base.do_charge(charger, target_atom, delay, past);
        if (!isliving(charger))
            return dot;
        await apply_post_charge(charger as mob_living);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:138 (tg defines it twice on the lobster's charge; the later one at :260 wins)
    public async DmTask apply_post_charge__d0(mob_living charger = null)
    {
        await charger.apply_status_effect(typeof(datum_status_effect_tired_post_charge));
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:260 (meant for the shrimp's charge, it overrides the lobster's own: kept as tg has it)
    public virtual async DmTask apply_post_charge(mob_living charger = null)
    {
        await charger.apply_status_effect(typeof(datum_status_effect_tired_post_charge_lesser));
    }
}

///A weaker, yet somewhat faster lobstrosity. Sources include aquarium chasm chrabs, chasms, plasma rivers and perhaps xenobio.
// tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:142
public partial class mob_living_basic_mining_lobstrosity_juvenile : mob_living_basic_mining_lobstrosity
{
    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:209  #define MAX_JUVENILES_ALLOWED_ON_TURF 3 (file-local, #undef at :222)
    private const int MAX_JUVENILES_ALLOWED_ON_TURF = 3;

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:182
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        float growth_step = 1000 / (7 * MINUTES); //It should take 7-ish minutes if you keep the happiness above 40% and at most 12
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_growth_and_differentiation),
            Dm.named("growth_path", grow_type),
            Dm.named("growth_probability", 58),
            Dm.named("lower_growth_value", growth_step),
            Dm.named("upper_growth_value", growth_step),
            Dm.named("scale_with_happiness", TRUE),
            Dm.named("optional_checks", Dm.@new<datum_callback>(this, nameof(ready_to_grow))),
            Dm.named("optional_grow_behavior", Dm.@new<datum_callback>(this, nameof(grow_up)))));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), Dm.named("tame_chance", 35), Dm.named("bonus_tame_chance", 20)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_swarming), 16, 11));
        ADD_TRAIT(this, TRAIT_MOB_HIDE_HAPPINESS, INNATE_TRAIT); //Do not let strangers know it gets happy when poked if stray.
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:199
    public override void add_ranged_armour(DmList vulnerable_projectiles = null)
    {
        Waiting.NoSleep(AddElement(
            typeof(datum_element_ranged_armour),
            Dm.named("minimum_projectile_force", 30),
            Dm.named("below_projectile_multiplier", 0.6f),
            Dm.named("vulnerable_projectile_types", vulnerable_projectiles),
            Dm.named("minimum_thrown_force", 13),
            Dm.named("throw_blocked_message", throw_blocked_message)));
    }

    ///Juvenile lobstrosities can swarm and pass through each other, but only 3 at most can stand the same turf.
    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:212
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        if (!(mover is mob_living_basic_mining_lobstrosity_juvenile))
            return await base.CanAllowThrough(mover, border_dir);
        float juveniles_count = 0;
        foreach (mob_living_basic_mining_lobstrosity_juvenile lob in Dm.each<mob_living_basic_mining_lobstrosity_juvenile>(loc))
        {
            juveniles_count++;
            if (juveniles_count > MAX_JUVENILES_ALLOWED_ON_TURF)
                return await base.CanAllowThrough(mover, border_dir);
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:224
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        // They are more pettable I guess
        await AddElement(typeof(datum_element_pet_bonus), "chitter");
        REMOVE_TRAIT(this, TRAIT_MOB_HIDE_HAPPINESS, INNATE_TRAIT);
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:230
    public virtual object ready_to_grow()
    {
        return Dm.num(isturf(loc));
    }

    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:233
    public virtual async DmTask grow_up()
    {
        object name_to_use = name == Dm.initial(this, (mob_living_basic_mining_lobstrosity_juvenile p) => p.name) ? Dm.initial(grow_type, (mob_living_basic_mining_lobstrosity p) => p.name) : name;
        mob_living_basic_mining_lobstrosity grown = await change_mob_type(grow_type, get_turf(this), name_to_use) as mob_living_basic_mining_lobstrosity;
        if (HAS_TRAIT(this, TRAIT_TAMED))
            await grown.tamed();
        foreach (object friend in Dm.each((ai_controller as datum_ai_controller)?.blackboard?[BB_FRIENDS_LIST]))
            await grown.befriend(friend as mob_living);
        await grown.set_brute_loss(get_brute_loss());
        await grown.set_fire_loss(get_fire_loss());
        qdel(this); //We called change_mob_type without 'delete_old_mob = TRUE' since we had to pass down friends and damage
    }
}

///Command the lobster to charge at someone.
// tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:264
public partial class datum_pet_command_use_ability_lob_charge : datum_pet_command_use_ability
{
    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:274
    public override async DmTask<object> set_command_target(mob_living parent = null, atom target = null)
    {
        if (target == null)
            return FALSE;
        datum_targeting_strategy targeter = GET_TARGETING_STRATEGY((parent.ai_controller as datum_ai_controller).blackboard[targeting_strategy_key]);
        if (!Dm.truthy(targeter == null ? null : await targeter.is_valid_target(parent, target)))
        {
            parent.balloon_alert_to_viewers("shakes head!");
            return FALSE;
        }
        return await base.set_command_target(parent, target);
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_mining_lobstrosity__target_foods;
    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity.dm:59 (var/static/list/fishable_turfs)
    internal DmList mob_living_basic_mining_lobstrosity__Initialize__fishable_turfs;
}
