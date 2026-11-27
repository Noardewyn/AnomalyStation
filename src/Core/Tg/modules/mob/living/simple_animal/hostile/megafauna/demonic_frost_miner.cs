// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

public partial class datum_controller_global_vars
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:2  GLOBAL_LIST_EMPTY(frost_miner_prisms)
    public DmList frost_miner_prisms;

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:2
    public virtual void InitGlobalfrost_miner_prisms()
    {
        frost_miner_prisms = Dm.list();
        Dm.append(ref gvars_datum_init_order, "frost_miner_prisms");
    }
}

/*

Difficulty: Extremely Hard

*/

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:10
public partial class mob_living_simple_animal_hostile_megafauna_demonic_frost_miner : mob_living_simple_animal_hostile_megafauna
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:1  #define FROST_MINER_SHOULD_ENRAGE (health <= maxHealth*0.25 && !enraged) (file-local)
    private bool FROST_MINER_SHOULD_ENRAGE => health <= maxHealth * 0.25f && !Dm.truthy(enraged);

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:64
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        frost_orbs = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire_shrapnel>(this);
        hard_frost_orbs = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire_shrapnel_strong>(this);
        snowball_machine_gun = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire>(this);
        hard_snowball_machine_gun = Dm.@new<datum_action_cooldown_mob_cooldown_direct_and_aoe>(this);
        ice_shotgun = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_shotgun_blast_pattern>(this);
        hard_ice_shotgun = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_shotgun_blast_pattern_circular>(this);
        Waiting.NoSleep(frost_orbs.Grant(this));
        Waiting.NoSleep(hard_frost_orbs.Grant(this));
        Waiting.NoSleep(snowball_machine_gun.Grant(this));
        Waiting.NoSleep(hard_snowball_machine_gun.Grant(this));
        Waiting.NoSleep(ice_shotgun.Grant(this));
        Waiting.NoSleep(hard_ice_shotgun.Grant(this));
        foreach (obj_structure_frost_miner_prism prism_to_set in Dm.each<obj_structure_frost_miner_prism>(GLOB.frost_miner_prisms))
            Waiting.NoSleep(prism_to_set.set_prism_light(LIGHT_COLOR_BLUE, 5));
        RegisterSignal(this, COMSIG_MOB_ABILITY_STARTED, nameof(start_attack));
        Waiting.NoSleep(AddElement(typeof(datum_element_knockback), 7, FALSE, TRUE));
        Waiting.NoSleep(AddElement(typeof(datum_element_lifesteal), 50));
        ADD_TRAIT(this, TRAIT_NO_FLOATING_ANIM, INNATE_TRAIT);
        Waiting.NoSleep(AddComponent(typeof(datum_component_boss_music), Dm.resource("sound/music/boss/bdm_boss.ogg"), COMSIG_HOSTILE_FOUND_TARGET)); // change to COMSIG_AI_BLACKBOARD_KEY_SET(BB_CURRENT_TARGET) in basic conversion
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:86
    public override object Destroy(float? force = null)
    {
        frost_orbs = null;
        hard_frost_orbs = null;
        snowball_machine_gun = null;
        hard_snowball_machine_gun = null;
        ice_shotgun = null;
        hard_ice_shotgun = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:95
    public override async DmTask OpenFire(atom A = null)
    {
        if (client != null)
            return;
        if (target is mob_living living_target && living_target.stat == DEAD) //don't go out of our way to fire our disintegrating attacks at corpses
            return;

        bool easy_attack = prob(80 - Dm.num(enraged) * 40);
        chosen_attack = rand(1, 3);
        switch (Dm.num(chosen_attack))
        {
            case 1:
                if (easy_attack)
                    await frost_orbs.Trigger(null, null, target);
                else
                    await hard_frost_orbs.Trigger(null, null, target);
                break;
            case 2:
                if (easy_attack)
                    await snowball_machine_gun.Trigger(null, null, target);
                else
                    await hard_snowball_machine_gun.Trigger(null, null, target);
                break;
            case 3:
                if (easy_attack)
                    await ice_shotgun.Trigger(null, null, target);
                else
                    await hard_ice_shotgun.Trigger(null, null, target);
                break;
        }
    }

    /// Pre-ability usage stuff
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:122
    [SignalHandler]
    public virtual object start_attack(mob_living owner = null, datum_action_cooldown activated = null)
    {
        if (Dm.truthy(enraging))
            return COMPONENT_BLOCK_ABILITY_START;
        if (FROST_MINER_SHOULD_ENRAGE)
        {
            Waiting.Detach(check_enraged()); // INVOKE_ASYNC
            return COMPONENT_BLOCK_ABILITY_START;
        }
        float projectile_speed_multiplier = 1 + Dm.num(enraged);
        frost_orbs.projectile_speed_multiplier = projectile_speed_multiplier;
        snowball_machine_gun.projectile_speed_multiplier = projectile_speed_multiplier;
        ice_shotgun.projectile_speed_multiplier = projectile_speed_multiplier;
        return null;
    }

    /// Checks if the demonic frost miner is ready to be enraged
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:135
    public virtual async DmTask check_enraged()
    {
        if (!FROST_MINER_SHOULD_ENRAGE)
            return;
        update_cooldowns(Dm.list(Dm.kv(COOLDOWN_UPDATE_SET_MELEE, 8 * SECONDS), Dm.kv(COOLDOWN_UPDATE_SET_RANGED, 8 * SECONDS)));
        frost_orbs.StartCooldown(8 * SECONDS);
        await adjust_brute_loss(-maxHealth);
        enraged = TRUE;
        enraging = TRUE;
        animate(this, Dm.named("pixel_z", 96), Dm.named("time", 100), Dm.named("easing", ELASTIC_EASING), Dm.named("flags", ANIMATION_RELATIVE));
        spin(100, 10);
        await sleep(60); // SLEEP_CHECK_DEATH(60, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        playsound(this, Dm.resource("sound/effects/explosion/explosion3.ogg"), 100, TRUE);
        icon_state = "demonic_miner_phase2";
        // tg names flags twice here (ANIMATION_END_NOW, then ANIMATION_RELATIVE); the later one is the one passed
        animate(this, Dm.named("pixel_z", -96), Dm.named("time", 8), Dm.named("flags", ANIMATION_RELATIVE));
        spin(8, 2);
        foreach (obj_structure_frost_miner_prism prism_to_set in Dm.each<obj_structure_frost_miner_prism>(GLOB.frost_miner_prisms))
            await prism_to_set.set_prism_light(LIGHT_COLOR_PURPLE, 5);
        await sleep(8); // SLEEP_CHECK_DEATH(8, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        foreach (mob_living L in Dm.each<mob_living>(viewers(null, this)))
            shake_camera(L, 3, 2);
        playsound(this, Dm.resource("sound/effects/meteorimpact.ogg"), 100, TRUE);
        ADD_TRAIT(this, TRAIT_MOVE_FLYING, FROSTMINER_ENRAGE_TRAIT);
        enraging = FALSE;
        await adjust_brute_loss(-maxHealth);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:160
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        Waiting.NoSleep(adjust_brute_loss(-30 * Dm.num(severity)));
        visible_message(span_danger(I18n.T("[] absorbs the explosion!", this)), span_userdanger(I18n.T("You absorb the explosion!")));
        return TRUE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:165
    public override object Goto(object target = null, object delay = null, object minimum_distance = null)
    {
        if (Dm.truthy(enraging))
            return null;
        return base.Goto(target, delay, minimum_distance);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:170
    public override async DmTask<object> MoveToTarget(DmList possible_targets = null)
    {
        if (Dm.truthy(enraging))
            return null;
        return await base.MoveToTarget(possible_targets);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:175
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        if (Dm.truthy(enraging))
            return null;
        return await base.Move(newloc, direction, glide_size_override, update_dir);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:180
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (health > 0)
            return null;
        turf T = get_turf(this);
        float loot = rand(1, 3);
        foreach (obj_structure_frost_miner_prism prism_to_set in Dm.each<obj_structure_frost_miner_prism>(GLOB.frost_miner_prisms))
            await prism_to_set.set_prism_light(COLOR_GRAY, 1);
        switch (loot)
        {
            case 1:
                Dm.@new<obj_item_resurrection_crystal>(T);
                break;
            case 2:
                Dm.@new<obj_item_clothing_shoes_winterboots_ice_boots_ice_trail>(T);
                break;
            case 3:
                Dm.@new<obj_item_pickaxe_drill_jackhammer_demonic>(T);
                break;
        }
        return await base.death(gibbed, __extra);
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:197
public partial class obj_projectile_colossus_frost_orb : obj_projectile_colossus
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:206
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (isturf(target) || isobj(target))
            await EX_ACT(target, EXPLODE_HEAVY);
        return dot;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:221
public partial class obj_projectile_colossus_ice_blast : obj_projectile_colossus
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:230
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (isturf(target) || isobj(target))
            await EX_ACT(target, EXPLODE_HEAVY);
        return dot;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:235
public partial class datum_status_effect_ice_block_talisman : datum_status_effect
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:243
    [DmParams("new_owner", "set_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object set_duration = Dm.arg(__extra, 0);
        if (isnum(set_duration))
            duration = Dm.num(set_duration);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:253
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_MOVABLE_PRE_MOVE, nameof(owner_moved));
        if (!IS_UNCONSCIOUS_OR_CRIT(owner))
            to_chat(owner, span_userdanger(I18n.T("You become frozen in a cube!")));
        cube = Dm.icon(Dm.resource("icons/effects/freeze.dmi"), "ice_cube");
        DmList icon_dimensions = get_icon_dimensions(owner.icon);
        cube.Scale(icon_dimensions["width"], icon_dimensions["height"]);
        owner.add_overlay(cube);
        return await base.on_apply(__extra);
    }

    /// Blocks movement from the status effect owner
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:264
    [SignalHandler]
    public virtual object owner_moved()
    {
        return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:268
    public override async DmTask be_replaced()
    {
        owner.cut_overlay(cube);
        UnregisterSignal(owner, COMSIG_MOVABLE_PRE_MOVE);
        await base.be_replaced();
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:273
    public override async DmTask<object> on_remove()
    {
        if (!IS_UNCONSCIOUS_OR_CRIT(owner))
            to_chat(owner, span_notice(I18n.T("The cube melts!")));
        owner.cut_overlay(cube);
        UnregisterSignal(owner, COMSIG_MOVABLE_PRE_MOVE);
        return null;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:279
public partial class obj_structure_frost_miner_prism : obj_structure
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:288
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.combine(ref GLOB.frost_miner_prisms, this);
        Waiting.NoSleep(set_prism_light(LIGHT_COLOR_BLUE, 5));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:293
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.frost_miner_prisms, this);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:297
    public virtual async DmTask set_prism_light(object new_color = null, object new_range = null)
    {
        color = new_color;
        set_light_color(new_color);
        await set_light(new_range);
    }
}
