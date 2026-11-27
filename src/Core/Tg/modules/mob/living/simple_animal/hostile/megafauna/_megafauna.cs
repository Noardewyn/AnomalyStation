// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:1
public partial class mob_living_simple_animal_hostile_megafauna : mob_living_simple_animal_hostile
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:63
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_basic_mob_attack_telegraph),
            Dm.named("display_telegraph_overlay", FALSE),
            Dm.named("telegraph_duration", attack_delay)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_seethrough_mob)));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        if (achievement_type != null || score_achievement_type != null)
        {
            DmList achievements = Dm.list(typeof(datum_award_achievement_boss_boss_killer), typeof(datum_award_score_boss_score));
            if (achievement_type != null)
                Dm.append(ref achievements, achievement_type);
            if (score_achievement_type != null)
                Dm.append(ref achievements, score_achievement_type);
            Waiting.NoSleep(AddElement(typeof(datum_element_kill_achievement), string_list(achievements), crusher_achievement_type, typeof(datum_memory_megafauna_slayer)));
        }
        if (Dm.truthy(gps_name) && Dm.truthy(true_spawn))
            Waiting.NoSleep(AddComponent(typeof(datum_component_gps), gps_name));
        if (Dm.truthy(crusher_loot))
        {
            if (islist(crusher_loot))
                crusher_loot = string_list(crusher_loot as DmList);
            Waiting.NoSleep(AddElement(
                typeof(datum_element_crusher_loot),
                Dm.named("trophy_type", crusher_loot),
                Dm.named("guaranteed_drop", 0.6f),
                Dm.named("replace_all", replace_crusher_drop),
                Dm.named("drop_immediately", del_on_death)));
        }
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        add_traits(Dm.list(TRAIT_NO_TELEPORT), MEGAFAUNA_TRAIT);
        grant_actions_by_list(attack_action_types);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:95
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        //Safety check
        if (loc == null)
            return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:101
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (health > 0 && !Dm.truthy(gibbed)) // prevents instakills
            return null;
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:106
    public override async DmTask gib(float? drop_bitflags = null)
    {
        if (health > 0)
            return;
        await base.gib(drop_bitflags);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:111
    public override async DmTask<object> singularity_act()
    {
        set_health(0);
        return await base.singularity_act();
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:115
    public override async DmTask dust(object just_ash = null, object drop_items = null, object give_moodlet = null, object force = null)
    {
        if (!Dm.truthy(force) && health > 0)
            return;
        loot.Cut();
        await base.dust(just_ash, drop_items, give_moodlet, force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:121
    public override async DmTask<object> AttackingTarget(atom attacked_target = null)
    {
        if (recovery_time >= world.time)
            return null;
        object dot = await base.AttackingTarget(attacked_target);
        if (target != null && !Dm.truthy(CanAttack(target)))
        {
            LoseTarget();
            return dot;
        }
        if (!isliving(target))
            return dot;
        mob_living living_target = target as mob_living;
        if (living_target.stat == DEAD || (living_target.health <= living_target.dead_threshold && HAS_TRAIT(living_target, TRAIT_NODEATH)))
        {
            await devour(living_target);
            return dot;
        }
        if (client == null && Dm.truthy(ranged) && ranged_cooldown <= world.time)
            await OpenFire();
        return dot;
    }

    /// Devours a target and restores health to the megafauna
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:138
    public virtual async DmTask<object> devour(mob_living victim = null)
    {
        if (victim == null || Dm.truthy(victim.has_status_effect(typeof(datum_status_effect_gutted))))
        {
            LoseTarget();
            return FALSE;
        }
        celebrate_kill(victim);
        if (!Dm.truthy(is_station_level(z)) || client != null) //NPC monsters won't heal while on station
            await heal_overall_damage(Dm.num(victim.maxHealth) * 0.5f);
        victim.investigate_log(Dm.text("has been devoured by [].", this), INVESTIGATE_DEATHS);
        if (iscarbon(victim))
        {
            qdel(victim.get_organ_slot(ORGAN_SLOT_LUNGS));
            qdel(victim.get_organ_slot(ORGAN_SLOT_HEART));
            qdel(victim.get_organ_slot(ORGAN_SLOT_LIVER));
        }
        await victim.adjust_brute_loss(500);
        await victim.death(); //make sure they die
        await victim.apply_status_effect(typeof(datum_status_effect_gutted));
        LoseTarget();
        return TRUE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:156
    public virtual void celebrate_kill(mob_living L = null)
    {
        visible_message(
            span_danger(I18n.T("[] disembowels []!", this, L)),
            span_userdanger(I18n.T("You feast on []'s organs, restoring your health!", L)));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:161
    public override object CanAttack(object the_target = null)
    {
        object dot = base.CanAttack(the_target);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!isliving(the_target))
            return TRUE;
        mob_living living_target = the_target as mob_living;
        return Dm.num(!Dm.truthy(living_target.has_status_effect(typeof(datum_status_effect_gutted))));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:170
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.eq(severity, EXPLODE_DEVASTATE))
            Waiting.NoSleep(adjust_brute_loss(250));
        else if (Dm.eq(severity, EXPLODE_HEAVY))
            Waiting.NoSleep(adjust_brute_loss(100));
        else if (Dm.eq(severity, EXPLODE_LIGHT))
            Waiting.NoSleep(adjust_brute_loss(50));

        return TRUE;
    }

    /// Sets/adds the next time the megafauna can use a melee or ranged attack, in deciseconds. It is a list to allow using named args. Use the ignore_staggered var if youre setting the cooldown to ranged_cooldown_time.
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:184
    public virtual void update_cooldowns(DmList cooldown_updates = null, float? ignore_staggered = null)
    {
        ignore_staggered ??= FALSE;
        if (!Dm.truthy(ignore_staggered) && Dm.truthy(has_status_effect(typeof(datum_status_effect_rebuked))))
        {
            foreach (object update in Dm.each(cooldown_updates))
                cooldown_updates[update] = Dm.num(cooldown_updates[update]) * 2;
        }
        if (Dm.truthy(cooldown_updates[COOLDOWN_UPDATE_SET_MELEE]))
            recovery_time = world.time + Dm.num(cooldown_updates[COOLDOWN_UPDATE_SET_MELEE]);
        if (Dm.truthy(cooldown_updates[COOLDOWN_UPDATE_ADD_MELEE]))
            recovery_time += Dm.num(cooldown_updates[COOLDOWN_UPDATE_ADD_MELEE]);
        if (Dm.truthy(cooldown_updates[COOLDOWN_UPDATE_SET_RANGED]))
            ranged_cooldown = world.time + Dm.num(cooldown_updates[COOLDOWN_UPDATE_SET_RANGED]);
        if (Dm.truthy(cooldown_updates[COOLDOWN_UPDATE_ADD_RANGED]))
            ranged_cooldown += Dm.num(cooldown_updates[COOLDOWN_UPDATE_ADD_RANGED]);
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:198
public partial class datum_action_innate_megafauna_attack : datum_action_innate
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:204
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        if (!ismegafauna(grant_to))
            return FALSE;
        return await base.Grant(grant_to, __extra);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:209
    public override async DmTask<object> Activate()
    {
        mob_living_simple_animal_hostile_megafauna fauna = owner as mob_living_simple_animal_hostile_megafauna;
        fauna.chosen_attack = Dm.num(chosen_attack_num);
        to_chat(fauna, chosen_message);
        return null;
    }
}
