// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/**
 *LEGION
 *
 *Legion spawns from the necropolis gate in the far north of lavaland. It is the guardian of the Necropolis and emerges from within whenever an intruder tries to enter through its gate.
 *Whenever Legion emerges, everything in lavaland will receive a notice via color, audio, and text. This is because Legion is powerful enough to slaughter the entirety of lavaland with little effort. LOL
 *
 *It has three attacks.
 *Spawn Skull. Most of the time it will use this attack. Spawns a single legion skull.
 *Spawn Sentinel. The legion will spawn up to three sentinels, depending on its size.
 *CHARGE! The legion starts spinning and tries to melee the player. It will try to flick itself towards the player, dealing some damage if it hits.
 *
 *When Legion dies, it will split into three smaller skulls up to three times.
 *If you kill all of the smaller ones it drops a staff of storms, which allows its wielder to call and disperse ash storms at will and functions as a powerful melee weapon.
 *
 *Difficulty: Medium
 */

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:24
public partial class mob_living_simple_animal_hostile_megafauna_legion : mob_living_simple_animal_hostile_megafauna
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:20  #define LEGION_LARGE 3 (file-local)
    private const float LEGION_LARGE = 3;
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:21  #define LEGION_MEDIUM 2 (file-local)
    private const float LEGION_MEDIUM = 2;
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:22  #define LEGION_SMALL 1 (file-local)
    private const float LEGION_SMALL = 1;

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:67
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_FLOATING_ANIM, INNATE_TRAIT);
        create_legion_skull = Dm.@new<datum_action_cooldown_mob_cooldown_create_legion_skull>(this);
        chase_target = Dm.@new<datum_action_cooldown_mob_cooldown_chase_target>(this);
        chase_target.size = size;
        create_legion_turrets = Dm.@new<datum_action_cooldown_mob_cooldown_create_legion_turrets>(this);
        create_legion_turrets.maximum_turrets = Dm.num(size) * 2;
        Waiting.NoSleep(create_legion_skull.Grant(this));
        Waiting.NoSleep(chase_target.Grant(this));
        Waiting.NoSleep(create_legion_turrets.Grant(this));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:79
    public override object Destroy(float? force = null)
    {
        create_legion_skull = null;
        chase_target = null;
        create_legion_turrets = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:115
    public override async DmTask OpenFire(atom A = null)
    {
        if (client != null)
            return;

        float roll = rand(4); //Larger skulls use more attacks.
        if (roll >= 0 && roll <= 2)
            await create_legion_skull.Trigger(null, null, target);
        else if (roll == 3)
            await chase_target.Trigger(null, null, target);
        else if (roll == 4)
            await create_legion_turrets.Trigger(null, null, target);
    }

    ///Deals some extra damage on throw impact.
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:128
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (hit_atom is mob_living hit_living)
        {
            playsound(this, attack_sound, 100, TRUE);
            Waiting.NoSleep(hit_living.apply_damage(22 * size / 2, wound_bonus: CANT_WOUND)); //It gets pretty hard to dodge the skulls when there are a lot of them. Scales down with size
            Waiting.NoSleep(hit_living.safe_throw_at(get_step(this, get_dir(this, hit_living)), 2)); //Some knockback. Prevent the legion from melee directly after the throw.
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:135
    public override object GiveTarget(object new_target = null)
    {
        object dot = base.GiveTarget(new_target);
        if (target != null)
            wander = TRUE;
        return dot;
    }

    ///This makes sure that the legion door opens on taking damage, so you can't cheese this boss.
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:141
    public override async DmTask on_damage_loss_changed(object amount = null, object updating_health = null, object forced = null, object damage_type = null)
    {
        if (GLOB.necropolis_gate != null && Dm.truthy(true_spawn) && Dm.num(amount) > 0)
            await GLOB.necropolis_gate.toggle_the_gate(null, TRUE); //very clever.
        await base.on_damage_loss_changed(amount, updating_health, forced, damage_type);
    }

    ///In addition to parent functionality, this will also turn the target into a small legion if they are unconscious.
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:148
    public override async DmTask<object> AttackingTarget(atom attacked_target = null)
    {
        object dot = await base.AttackingTarget(attacked_target);
        if (!Dm.truthy(dot) || !ishuman(target))
            return dot;
        mob_living living_target = (mob_living)target;
        if (IS_UNCONSCIOUS_AND_ALIVE(living_target))
        {
            mob_living_basic_mining_legion_brood legion = Dm.@new<mob_living_basic_mining_legion_brood>(loc);
            await legion.infest(living_target as mob_living_carbon_human);
        }
        return dot;
    }

    ///Special snowflake death() here. Can only die if size is 1 or lower and HP is 0 or below.
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:158
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        //Make sure we didn't get cheesed
        if (health > 0)
            return null;
        if (Dm.truthy(Split()))
            return null;
        //We check what loot we should drop.
        bool last_legion = true;
        foreach (mob_living_simple_animal_hostile_megafauna_legion other in Dm.each<mob_living_simple_animal_hostile_megafauna_legion>(GLOB.mob_living_list))
        {
            if (other != this)
            {
                last_legion = false;
                break;
            }
        }
        if (last_legion)
        {
            loot = Dm.list(typeof(obj_item_storm_staff));
            DmList achievements = Dm.list(typeof(datum_award_achievement_boss_boss_killer), typeof(datum_award_score_boss_score), typeof(datum_award_score_legion_score), typeof(datum_award_achievement_boss_legion_kill));
            Waiting.NoSleep(AddElement(typeof(datum_element_kill_achievement), string_list(achievements), crusher_achievement_type, typeof(datum_memory_megafauna_slayer)));
        }
        else if (prob(20)) //20% chance for sick lootz.
        {
            loot = Dm.list(typeof(obj_structure_closet_crate_necropolis_tendril));
            if (!Dm.truthy(true_spawn))
                loot = null;
        }
        return await base.death(gibbed, __extra);
    }

    ///Splits legion into smaller skulls.
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:182
    public virtual object Split()
    {
        size--;
        if (size == LEGION_SMALL)
        {
            for (int i = 0; i <= 2; i++)
                Dm.@new<mob_living_simple_animal_hostile_megafauna_legion_small>(loc);
        }
        else if (size == LEGION_MEDIUM)
        {
            Dm.@new<mob_living_simple_animal_hostile_megafauna_legion_medium_left>(loc);
            Dm.@new<mob_living_simple_animal_hostile_megafauna_legion_medium_right>(loc);
            Dm.@new<mob_living_simple_animal_hostile_megafauna_legion_medium_eye>(loc);
        }
        return null; // tg returns nothing, so death never stops here and every split legion also dies
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:100
public partial class mob_living_simple_animal_hostile_megafauna_legion_small : mob_living_simple_animal_hostile_megafauna_legion
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:108
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:112
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // if (stat != DEAD) . += emissive_appearance(icon, "[icon_state]_e", src, effect_type = EMISSIVE_NO_BLOOM): the eyes' glow on
        // the emissive plane (DEVIATION DV12)
        Dm.Deferred("code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:114", "update_overlays: emissive_appearance of the small legion's eyes (DEVIATION DV12)");
        return dot;
    }
}
