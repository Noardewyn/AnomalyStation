// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/unique_plant_genes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file. The admin, attack, bomber and investigate logs are comments (M10+).

// --- Below here are special, unique plant traits that only belong to certain plants. ---
// They are un-removable and cannot be mutated randomly, and should never be graftable.

/// Holymelon's anti-magic trait. Charges based on potency.
// tg: code/modules/hydroponics/unique_plant_genes.dm:5
public partial class datum_plant_gene_trait_anti_magic : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:13
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        shield_uses = Dm.round(Dm.num(our_seed.potency) / 20);
        //deliver us from evil o melon god
        await our_plant.AddComponent(typeof(datum_component_anti_magic),
            Dm.named("antimagic_flags", Dm.bitor(MAGIC_RESISTANCE, MAGIC_RESISTANCE_HOLY)),
            Dm.named("inventory_flags", ITEM_SLOT_HANDS),
            Dm.named("charges", shield_uses),
            Dm.named("block_magic", Dm.@new<datum_callback>(this, nameof(drain_antimagic))),
            Dm.named("expiration", Dm.@new<datum_callback>(this, nameof(expire))));
        return dot;
    }

    /// When the plant our gene is hosted in is drained of an anti-magic charge.
    // tg: code/modules/hydroponics/unique_plant_genes.dm:29
    public virtual void drain_antimagic(mob user = null, obj_item our_plant = null)
    {
        to_chat(user, span_warning(I18n.T("[] hums slightly, and seems to decay a bit.", our_plant)));
    }

    /// When the plant our gene is hosted in is drained of all of its anti-magic charges.
    // tg: code/modules/hydroponics/unique_plant_genes.dm:33
    public virtual void expire(mob user = null, obj_item our_plant = null)
    {
        to_chat(user, span_warning(I18n.T("[] rapidly turns into ash!", our_plant)));
        Dm.@new<obj_effect_decal_cleanable_ash>(our_plant.drop_location());
        qdel(our_plant);
    }
}

/// Traits that turn a plant into a weapon, giving them force and effects on attack.
// tg: code/modules/hydroponics/unique_plant_genes.dm:39
public partial class datum_plant_gene_trait_attack : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:50
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(force_multiplier))
        {
            obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
            our_plant.force = Dm.round((5 + Dm.num(our_seed.potency) * Dm.num(force_multiplier)), 1);
        }
        RegisterSignal(our_plant, COMSIG_ITEM_ATTACK, nameof(on_plant_attack));
        RegisterSignal(our_plant, COMSIG_ITEM_AFTERATTACK, nameof(after_plant_attack));
        return dot;
    }

    /// Signal proc for [COMSIG_ITEM_ATTACK] that allows for effects on attack
    // tg: code/modules/hydroponics/unique_plant_genes.dm:62
    [SignalHandler]
    public virtual void on_plant_attack(obj_item source = null, mob_living target = null, mob_living user = null)
    {
        Waiting.Detach(attack_effect(source, target, user)); // INVOKE_ASYNC
    }

    /*
     * Effects done when we hit people with our plant, ON attack.
     * Override on a per-plant basis.
     *
     * our_plant - our plant, that we're attacking with
     * user - the person who is attacking with the plant
     * target - the person who is attacked by the plant
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:75
    public virtual DmTask attack_effect(obj_item our_plant = null, mob_living target = null, mob_living user = null)
    {
        return DmTask.CompletedTask;
    }

    /// Signal proc for [COMSIG_ITEM_AFTERATTACK] that allows for effects after an attack is done
    // tg: code/modules/hydroponics/unique_plant_genes.dm:79
    [SignalHandler]
    public virtual void after_plant_attack(obj_item source = null, atom target = null, mob user = null, DmList modifiers = null)
    {
        Waiting.DetachSync(() => after_attack_effect(source, target, user as mob_living)); // INVOKE_ASYNC
    }

    /*
     * Effects done when we hit people with our plant, AFTER the attack is done.
     * Extend on a per-plant basis.
     *
     * our_plant - our plant, that we're attacking with
     * user - the person who is attacking with the plant
     * target - the atom which is attacked by the plant
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:91
    public virtual void after_attack_effect(obj_item our_plant = null, atom target = null, mob_living user = null, params object[] __extra)
    {
        if (!Dm.truthy(degrades_after_hit))
            return;

        // We probably hit something or someone. Reduce our force
        if (Dm.num(our_plant.force) > 0)
        {
            our_plant.force = Dm.num(our_plant.force) - rand(1, (Dm.num(our_plant.force) / 3) + 1);
            return;
        }

        // When our force degrades to zero or below, we're all done
        to_chat(user, span_warning(I18n.T("All the [] have fallen off [] from violent whacking!", degradation_noun, our_plant)));
        qdel(our_plant);
    }
}

/// Novaflower's attack effects (sets people on fire) + degradation on attack
// tg: code/modules/hydroponics/unique_plant_genes.dm:107
public partial class datum_plant_gene_trait_attack_novaflower_attack : datum_plant_gene_trait_attack
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:115
    public override async DmTask attack_effect(obj_item our_plant = null, mob_living target = null, mob_living user = null)
    {
        if (target == null)
            return;

        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        to_chat(target, span_danger(I18n.T("You are lit on fire from the intense heat of []!", our_plant)));
        await target.adjust_fire_stacks(Dm.round(Dm.num(our_seed.potency) / 20));
        if (Dm.truthy(target.ignite_mob()))
        {
            message_admins(Dm.text("[] set [] on fire with [] at []", ADMIN_LOOKUPFLW(user), ADMIN_LOOKUPFLW(target), our_plant, AREACOORD(user)));
            user.log_message(Dm.text("set [] on fire with []", key_name(target), our_plant), LOG_ATTACK);
            target.log_message(Dm.text("was set on fire by [] with [].", key_name(user), our_plant), LOG_VICTIM, log_globally: FALSE);
        }
        our_plant.investigate_log(Dm.text("was used by [] to burn [] at []", key_name(user), key_name(target), AREACOORD(user)), INVESTIGATE_BOTANY);
    }
}

/// Sunflower's attack effect (shows cute text)
// tg: code/modules/hydroponics/unique_plant_genes.dm:129
public partial class datum_plant_gene_trait_attack_sunflower_attack : datum_plant_gene_trait_attack
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:133
    public override void after_attack_effect(obj_item our_plant = null, atom target = null, mob_living user = null, params object[] __extra)
    {
        if (target is mob target_mob)
        {
            user.visible_message(Dm.text("<font color='green'>[] smacks [] with [] []! <font color='orange'><b>FLOWER POWER!</b></font></font>", user, target_mob, user.p_their(), our_plant.name), ignored_mobs: Dm.list(target_mob, user));
            if (target_mob != user)
                to_chat(target_mob, Dm.text("<font color='green'>[] smacks you with []!<font color='orange'><b>FLOWER POWER!</b></font></font>", user, our_plant));
            to_chat(user, Dm.text("<font color='green'>Your []'s <font color='orange'><b>FLOWER POWER</b></font> strikes []!</font>", our_plant.name, target_mob));
        }

        base.after_attack_effect(our_plant, target, user, __extra);
    }
}

/// Traits for plants with backfire effects. These are negative effects that occur when a plant is handled without gloves/unsafely.
// tg: code/modules/hydroponics/unique_plant_genes.dm:155
public partial class datum_plant_gene_trait_backfire : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:166
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(genes_to_check))
            genes_to_check = string_list(genes_to_check) as DmList;
        if (Dm.truthy(traits_to_check))
            traits_to_check = string_list(traits_to_check) as DmList;
        await our_plant.AddElement(typeof(datum_element_plant_backfire), cancel_action_on_backfire, traits_to_check, genes_to_check);
        RegisterSignal(our_plant, COMSIG_PLANT_ON_BACKFIRE, nameof(on_backfire));
        return dot;
    }

    /// Signal proc for [COMSIG_PLANT_ON_BACKFIRE] that causes the backfire effect.
    // tg: code/modules/hydroponics/unique_plant_genes.dm:178
    [SignalHandler]
    public virtual void on_backfire(obj_item source = null, mob_living_carbon user = null)
    {
        Waiting.Detach(backfire_effect(source, user)); // INVOKE_ASYNC
    }

    /**
     * The actual backfire effect on the user.
     * Override with plant-specific effects.
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:187
    public virtual DmTask backfire_effect(obj_item our_plant = null, mob_living_carbon user = null)
    {
        return DmTask.CompletedTask;
    }
}

/// Rose's prick on backfire
// tg: code/modules/hydroponics/unique_plant_genes.dm:191
public partial class datum_plant_gene_trait_backfire_rose_thorns : datum_plant_gene_trait_backfire
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:197
    public override async DmTask backfire_effect(obj_item our_plant = null, mob_living_carbon user = null)
    {
        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        if (our_seed.get_gene(typeof(datum_plant_gene_trait_sticky)) == null && prob(66))
        {
            to_chat(user, span_danger(I18n.T("[]'s thorns nearly prick your hand. Best be careful.", our_plant)));
            return;
        }

        to_chat(user, span_danger(I18n.T("[]'s thorns prick your hand. Ouch.", our_plant)));
        our_plant.investigate_log(Dm.text("rose-pricked [] at []", key_name(user), AREACOORD(user)), INVESTIGATE_BOTANY);
        await user.apply_damage(2, BRUTE, user.get_active_hand());
    }
}

/// Novaflower's hand burn on backfire
// tg: code/modules/hydroponics/unique_plant_genes.dm:208
public partial class datum_plant_gene_trait_backfire_novaflower_heat : datum_plant_gene_trait_backfire
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:214
    public override async DmTask backfire_effect(obj_item our_plant = null, mob_living_carbon user = null)
    {
        to_chat(user, span_danger(I18n.T("[] singes your bare hand!", our_plant)));
        our_plant.investigate_log(Dm.text("self-burned [] for [] at []", key_name(user), our_plant.force, AREACOORD(user)), INVESTIGATE_BOTANY);
        await user.apply_damage(Dm.num(our_plant.force), our_plant.damtype as string, user.get_active_hand(), wound_bonus: CANT_WOUND);
    }
}

/// Normal Nettle hannd burn on backfire
// tg: code/modules/hydroponics/unique_plant_genes.dm:220
public partial class datum_plant_gene_trait_backfire_nettle_burn : datum_plant_gene_trait_backfire
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:225
    public override async DmTask backfire_effect(obj_item our_plant = null, mob_living_carbon user = null)
    {
        to_chat(user, span_danger(I18n.T("[] burns your bare hand!", our_plant)));
        our_plant.investigate_log(Dm.text("self-burned [] for [] at []", key_name(user), our_plant.force, AREACOORD(user)), INVESTIGATE_BOTANY);
        await user.apply_damage(Dm.num(our_plant.force), our_plant.damtype as string, user.get_active_hand(), wound_bonus: CANT_WOUND);
    }
}

/// Deathnettle hand burn + stun on backfire
// tg: code/modules/hydroponics/unique_plant_genes.dm:231
public partial class datum_plant_gene_trait_backfire_nettle_burn_death : datum_plant_gene_trait_backfire_nettle_burn
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:235
    public override async DmTask backfire_effect(obj_item our_plant = null, mob_living_carbon user = null)
    {
        // . = ..(): the parent's backfire_effect returns nothing, so . is null and tg always returns here (its stun never runs)
        await base.backfire_effect(our_plant, user);
        object dot = null;
        if (!Dm.truthy(dot) || prob(50))
            return;

        await user.Paralyze(10 * SECONDS);
        to_chat(user, span_userdanger(I18n.T("You are stunned by the powerful acids of []!", our_plant)));
    }
}

/// Ghost-Chili heating up on backfire
// tg: code/modules/hydroponics/unique_plant_genes.dm:243
public partial class datum_plant_gene_trait_backfire_chili_heat : datum_plant_gene_trait_backfire
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:254
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        our_chili = WEAKREF(our_plant) as datum_weakref;
        RegisterSignals(our_plant, Dm.list(COMSIG_QDELETING, COMSIG_ITEM_DROPPED), nameof(stop_backfire_effect));
        return dot;
    }

    /*
     * Begin processing the trait on backfire.
     *
     * our_plant - our source plant, which is backfiring
     * user - the mob holding our plant
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:268
    public override DmTask backfire_effect(obj_item our_plant = null, mob_living_carbon user = null)
    {
        held_mob = WEAKREF(user) as datum_weakref;
        START_PROCESSING(SSobj, this);
        return DmTask.CompletedTask;
    }

    /*
     * Stop processing the trait when we're dropped or deleted.
     *
     * our_plant - our source plant
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:277
    [SignalHandler]
    public virtual void stop_backfire_effect(datum source = null)
    {
        held_mob = null;
        STOP_PROCESSING(SSobj, this);
    }

    /*
     * The processing of our trait. Heats up the mob ([held_mob]) currently holding the source plant ([our_chili]).
     * Stops processing if we're no longer being held by [held mob].
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:287
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        mob_living_carbon our_mob = held_mob?.resolve() as mob_living_carbon;
        obj_item our_plant = our_chili?.resolve() as obj_item;

        // If our weakrefs don't resolve, or if our mob is not holding our plant, stop processing.
        if (our_mob == null || our_plant == null || !Dm.truthy(our_mob.is_holding(our_plant)))
        {
            stop_backfire_effect();
            return null;
        }

        our_mob.adjust_bodytemperature(7.5f * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick));
        if (SPT_PROB(5, seconds_per_tick))
            to_chat(our_mob, span_warning(I18n.T("Your hand holding [] burns!", our_plant)));
        return null;
    }
}

/// Bluespace Tomato squashing on the user on backfire
// tg: code/modules/hydroponics/unique_plant_genes.dm:301
public partial class datum_plant_gene_trait_backfire_bluespace : datum_plant_gene_trait_backfire
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:307
    public override DmTask backfire_effect(obj_item our_plant = null, mob_living_carbon user = null)
    {
        if (prob(50))
            return DmTask.CompletedTask;

        to_chat(user, span_danger(I18n.T("[] slips out of your hand!", our_plant)));

        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        datum_plant_gene_trait_squash squash_gene = our_seed.get_gene(typeof(datum_plant_gene_trait_squash)) as datum_plant_gene_trait_squash;
        squash_gene.squash_plant(our_plant, user);
        return DmTask.CompletedTask;
    }
}

/// Traits for plants that can be activated to turn into a mob.
// tg: code/modules/hydroponics/unique_plant_genes.dm:318
public partial class datum_plant_gene_trait_mob_transformation : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:336
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(dangerous))
        {
            await our_plant.AddElement(typeof(datum_element_plant_backfire), TRUE);
            RegisterSignal(our_plant, COMSIG_PLANT_ON_BACKFIRE, nameof(early_awakening));
        }
        RegisterSignal(our_plant, COMSIG_ITEM_ATTACK_SELF, nameof(manual_awakening));
        RegisterSignal(our_plant, COMSIG_ITEM_PRE_ATTACK, nameof(pre_consumption_check));
        return dot;
    }

    /*
     * Before we can eat our plant, check to see if it's waking up. Don't eat it if it is.
     *
     * our_plant - signal source, the plant we're eating
     * target - the mob eating the plant
     * user - the mob feeding someone the plant (generally, target == user)
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:354
    [SignalHandler]
    public virtual object pre_consumption_check(obj_item our_plant = null, atom target = null, mob user = null)
    {
        if (!Dm.truthy(awakening))
            return null;

        if (!ismob(target))
            return null;

        if (target != user)
            to_chat(user, span_warning(I18n.T("[] is twitching and shaking, preventing you from feeding it to [].", our_plant, target)));
        to_chat(target, span_warning(I18n.T("[] is twitching and shaking, preventing you from eating it.", our_plant)));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /*
     * Called when a user manually activates the plant.
     * Checks if the critera is met to spawn it, and spawns it in 3 seconds if it is.
     *
     * our_plant - our plant that we're waking up
     * user - the mob activating the plant
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:375
    [SignalHandler]
    public virtual void manual_awakening(obj_item our_plant = null, mob user = null)
    {
        if (Dm.truthy(awakening) || isspaceturf(user.loc))
            return;

        if (Dm.truthy(dangerous) && HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            to_chat(user, span_notice(I18n.T("You decide not to awaken []. It may be very dangerous!", our_plant)));
            return;
        }

        to_chat(user, span_notice(I18n.T("You begin to awaken []...", our_plant)));
        begin_awaken(our_plant, 3 * SECONDS);
        our_plant.investigate_log(Dm.text("was awakened by [] at [].", key_name(user), AREACOORD(user)), INVESTIGATE_BOTANY);
    }

    /*
     * Called when a user accidentally activates the plant via backfire effect.
     *
     * our_plant - our plant, which is waking up
     * user - the mob handling the plant
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:395
    [SignalHandler]
    public virtual void early_awakening(obj_item our_plant = null, mob_living_carbon user = null)
    {
        if (!Dm.truthy(awakening) && !isspaceturf(user.loc) && prob(25))
        {
            our_plant.visible_message(span_danger(I18n.T("[] begins to growl and shake!", our_plant)));
            begin_awaken(our_plant, 1 * SECONDS);
            our_plant.investigate_log(Dm.text("was awakened (via plant backfire) by [] at [].", key_name(user), AREACOORD(user)), INVESTIGATE_BOTANY);
        }
    }

    /*
     * Actually begin the process of awakening the plant.
     *
     * awaken_time - the time, in seconds, it will take for the plant to spawn.
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:408
    public virtual void begin_awaken(obj_item our_plant = null, object awaken_time = null)
    {
        awakening = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(awaken), our_plant), Dm.num(awaken_time), file: "code/modules/hydroponics/unique_plant_genes.dm", line: 410);
    }

    /*
     * Actually awaken the plant, spawning the mob designated by the [killer_plant] typepath.
     *
     * our_plant - the plant that's waking up
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:417
    public virtual async DmTask awaken(obj_item our_plant = null)
    {
        if (QDELETED(our_plant))
            return;
        if (!ispath(killer_plant))
            return;

        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        mob_living spawned_mob = Dm.@new(killer_plant, our_plant.drop_location()) as mob_living;
        spawned_mob.maxHealth = Dm.num(spawned_mob.maxHealth) + Dm.round(our_seed.endurance * mob_health_multiplier);
        spawned_mob.health = spawned_mob.maxHealth;
        if (spawned_mob is mob_living_simple_animal_hostile) // ishostile
        {
            mob_living_simple_animal_hostile spawned_simplemob = spawned_mob as mob_living_simple_animal_hostile;
            spawned_simplemob.melee_damage_lower = Dm.num(spawned_simplemob.melee_damage_lower) + Dm.round(Dm.num(our_seed.potency) * mob_melee_multiplier);
            spawned_simplemob.melee_damage_upper = Dm.num(spawned_simplemob.melee_damage_upper) + Dm.round(Dm.num(our_seed.potency) * mob_melee_multiplier);
            spawned_simplemob.move_to_delay = Dm.num(spawned_simplemob.move_to_delay) - Dm.round(Dm.num(our_seed.production) * mob_speed_multiplier);
        }

        if (isbasicmob(spawned_mob))
        {
            mob_living_basic spawned_basicmob = spawned_mob as mob_living_basic;
            spawned_basicmob.melee_damage_lower = Dm.num(spawned_basicmob.melee_damage_lower) + Dm.round(Dm.num(our_seed.potency) * mob_melee_multiplier);
            spawned_basicmob.melee_damage_upper = Dm.num(spawned_basicmob.melee_damage_upper) + Dm.round(Dm.num(our_seed.potency) * mob_melee_multiplier);
            // basic mob speeds aren't exactly equivalent to simple animal's "move to delay" but this seems balanced enough.
            float calculated_speed = Dm.num(Dm.initial(spawned_basicmob, "speed")) - Dm.round((Dm.num(our_seed.production) * mob_speed_multiplier), 0.01f);
            await spawned_basicmob.set_varspeed(calculated_speed);
        }

        await our_plant.forceMove(our_plant.drop_location() as atom);
        spawned_mob.visible_message(span_notice(I18n.T("[] growls as it suddenly awakens!", our_plant)));
        qdel(our_plant);
    }
}

/// Traiit for plants eaten in 1 bite.
// tg: code/modules/hydroponics/unique_plant_genes.dm:461
public partial class datum_plant_gene_trait_one_bite : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:467
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        if (our_plant is obj_item_food_grown grown_plant)
            grown_plant.bite_consumption_mod = 100;
        return dot;
    }
}

/// Traits for plants with a different base max_volume.
// tg: code/modules/hydroponics/unique_plant_genes.dm:477
public partial class datum_plant_gene_trait_modified_volume : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:484
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        if (our_plant is obj_item_food_grown grown_plant)
            grown_plant.max_volume = new_capacity;
        return dot;
    }
}

/// Plants that explode when used (based on their reagent contents)
// tg: code/modules/hydroponics/unique_plant_genes.dm:506
public partial class datum_plant_gene_trait_bomb_plant : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:513
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        our_plant.max_integrity = 40; // Max_integrity is lowered so they explode better, or something like that.
        RegisterSignal(our_plant, COMSIG_ITEM_ATTACK_SELF, nameof(trigger_detonation));
        RegisterSignal(our_plant, COMSIG_ATOM_EX_ACT, nameof(explosion_reaction));
        RegisterSignal(our_plant, COMSIG_OBJ_DECONSTRUCT, nameof(deconstruct_reaction));
        return dot;
    }

    /*
     * Trigger our plant's detonation.
     *
     * our_plant - the plant that's exploding
     * user - the mob detonating the plant
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:529
    [SignalHandler]
    public virtual void trigger_detonation(obj_item our_plant = null, mob_living user = null)
    {
        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        obj_item_food_grown grown_plant = our_plant as obj_item_food_grown;
        // If we have an alt icon, use that to show our plant is exploding.
        if (our_plant != null && Dm.truthy(grown_plant?.alt_icon))
            our_plant.icon_state = grown_plant.alt_icon as string;
        else
            our_plant.color = COLOR_RED;

        playsound(our_plant, Dm.resource("sound/effects/fuse.ogg"), our_seed.potency, FALSE);
        user.visible_message(
            span_warning(I18n.T("[] plucks the stem from []!", user, our_plant)),
            span_userdanger(I18n.T("You pluck the stem from [], which begins to hiss loudly!", our_plant)));
        log_bomber(user, "primed a", our_plant, "for detonation");
        Waiting.NoSleep(detonate(our_plant));
    }

    /*
     * Reacting to when the plant is deconstructed.
     * When is a plant ever deconstructed? Apparently, when it burns.
     *
     * our_plant - the plant that's 'deconstructed'
     * disassembled - if it was disassembled when it was deconstructed.
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:555
    [SignalHandler]
    public virtual void deconstruct_reaction(obj_item our_plant = null, object disassembled = null)
    {
        if (!Dm.truthy(disassembled))
            Waiting.NoSleep(detonate(our_plant));
        if (!QDELETED(our_plant))
            qdel(our_plant);
    }

    /*
     * React to explosions that hit the plant.
     * Ensures that the plant id deleted by its own explosion.
     * Also prevents mass chain reaction with piles plants.
     *
     * our_plant - the plant that's exploded on
     * severity - severity of the explosion
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:571
    [SignalHandler]
    public virtual void explosion_reaction(obj_item our_plant = null, object severity = null)
    {
        qdel(our_plant);
    }

    /*
     * RActually blow up the plant.
     *
     * our_plant - the plant that's exploding for real
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:581
    public virtual async DmTask detonate(obj_item our_plant = null)
    {
        datum_reagents holder = our_plant.reagents as datum_reagents;
        holder.chem_temp = 1000; //Sets off the gunpowder
        await holder.handle_reactions();
    }
}

/// A subtype of bomb plants that have their boom sized based on potency instead of reagent contents.
// tg: code/modules/hydroponics/unique_plant_genes.dm:586
public partial class datum_plant_gene_trait_bomb_plant_potency_based : datum_plant_gene_trait_bomb_plant
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:589
    public override void trigger_detonation(obj_item our_plant = null, mob_living user = null)
    {
        user.visible_message(
            span_warning(I18n.T("[] primes []!", user, our_plant)),
            span_userdanger(I18n.T("You prime []!", our_plant)));
        log_bomber(user, "primed a", our_plant, "for detonation");

        obj_item_food_grown grown_plant = our_plant as obj_item_food_grown;
        if (our_plant != null && Dm.truthy(grown_plant?.alt_icon))
            our_plant.icon_state = grown_plant.alt_icon as string;
        else
            our_plant.color = COLOR_RED;

        playsound(our_plant.drop_location() as atom, Dm.resource("sound/items/weapons/armbomb.ogg"), 75, TRUE, -3);
        _addtimer(Dm.@new<datum_callback>(this, nameof(detonate), our_plant), rand(1 * SECONDS, 6 * SECONDS), file: "code/modules/hydroponics/unique_plant_genes.dm", line: 603);
    }

    // tg: code/modules/hydroponics/unique_plant_genes.dm:605
    public override async DmTask detonate(obj_item our_plant = null)
    {
        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        float flame_reach = Dm.clamp(Dm.round(Dm.num(our_seed.potency) / 20), 1, 5); //Like IEDs - their flame range can get up to 5, but their real boom is small

        await our_plant.forceMove(our_plant.drop_location() as atom);
        await explosion(our_plant, devastation_range: -1, heavy_impact_range: -1, light_impact_range: 2, flame_range: flame_reach);
        qdel(our_plant);
    }
}

/// Corpseflower's miasma production.
/// Can be generalized in the future to spawn any gas, but I don't think that's necessarily a good idea.
// tg: code/modules/hydroponics/unique_plant_genes.dm:615
public partial class datum_plant_gene_trait_gas_production : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:624
    public override void on_new_seed(obj_item_seeds new_seed = null)
    {
        RegisterSignal(new_seed, COMSIG_SEED_ON_PLANTED, nameof(set_home_tray));
        RegisterSignal(new_seed, COMSIG_SEED_ON_GROW, nameof(try_release_gas));
        RegisterSignal(new_seed, COMSIG_QDELETING, nameof(stop_gas));
        stinky_seed = WEAKREF(new_seed) as datum_weakref;
    }

    // tg: code/modules/hydroponics/unique_plant_genes.dm:630
    public override void on_removed(obj_item_seeds old_seed = null)
    {
        UnregisterSignal(old_seed, Dm.list(COMSIG_QDELETING, COMSIG_SEED_ON_PLANTED, COMSIG_SEED_ON_GROW));
        stop_gas();
    }

    /*
     * Whenever we're planted, set a new home tray.
     *
     * our_seed - the seed growing
     * grown_tray - the tray we were planted in
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:640
    [SignalHandler]
    public virtual void set_home_tray(obj_item_seeds our_seed = null, obj_machinery_hydroponics grown_tray = null)
    {
        home_tray = WEAKREF(grown_tray) as datum_weakref;
    }

    /*
     * Whenever the plant starts to grow in a tray, check if we can release gas.
     *
     * our_seed - the seed growing
     * grown_tray - the tray, we're currently growing within
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:651
    [SignalHandler]
    public virtual void try_release_gas(obj_item_seeds our_seed = null, obj_machinery_hydroponics grown_tray = null)
    {
        if (Dm.num(grown_tray.age) < our_seed.maturation) // Start a little before it blooms
            return;

        START_PROCESSING(SSobj, this);
    }

    /*
     * Stop the seed from releasing gas.
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:662
    [SignalHandler]
    public virtual void stop_gas(datum source = null)
    {
        STOP_PROCESSING(SSobj, this);
    }

    /*
     * If the conditions are acceptable and the potency is high enough, release miasma into the air.
     */
    // tg: code/modules/hydroponics/unique_plant_genes.dm:670
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        obj_item_seeds seed = stinky_seed?.resolve() as obj_item_seeds;
        obj_machinery_hydroponics tray = home_tray?.resolve() as obj_machinery_hydroponics;

        // If our weakrefs don't resolve, or if our seed is /somehow/ not in the tray it was planted in, stop processing.
        if (seed == null || tray == null || seed.loc != tray)
        {
            stop_gas();
            return null;
        }

        turf_open tray_turf = get_turf(tray) as turf_open;
        datum_gas_mixture tray_air = Waiting.NoSleep(tray_turf.return_air()) as datum_gas_mixture;
        if (Dm.abs(ONE_ATMOSPHERE - Dm.num(tray_air.return_pressure())) > (Dm.num(seed.potency) / 10 + 10)) // clouds can begin showing at around 50-60 potency in standard atmos
            return null;

        datum_gas_mixture stank = Dm.@new<datum_gas_mixture>();

        stank.set_gas(typeof(datum_gas_miasma), (seed.yield + 6) * 3.5f * MIASMA_CORPSE_MOLES * Dm.num(seconds_per_tick)); // this process is only being called about 2/7 as much as corpses so this is 12-32 times a corpses
        stank.temperature = T20C; // without this the room would eventually freeze and miasma mining would be easier
        Waiting.NoSleep(tray_turf.assume_air(stank));
        return null;
    }
}

/// Poppy's unique trait that allows slicing for sap
// tg: code/modules/hydroponics/unique_plant_genes.dm:697
public partial class datum_plant_gene_trait_opium_production : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:705
    public override void on_plant_in_tray(obj_machinery_hydroponics tray = null, obj_item_seeds seed = null)
    {
        RegisterSignal(tray, COMSIG_ATOM_ITEM_INTERACTION, nameof(try_extract));
        extracted = FALSE; // just in case...
    }

    // tg: code/modules/hydroponics/unique_plant_genes.dm:709
    public override void on_unplanted_from_tray(obj_machinery_hydroponics tray = null, obj_item_seeds seed = null)
    {
        UnregisterSignal(tray, COMSIG_ATOM_ITEM_INTERACTION);
    }

    /// Redirect tray item interaction so we can have custom extracting behavior
    // tg: code/modules/hydroponics/unique_plant_genes.dm:713
    [SignalHandler]
    public virtual object try_extract(obj_machinery_hydroponics source = null, mob_living user = null, obj_item tool = null, params object[] __extra)
    {
        if (!Dm.truthy(tool.sharpness) || Dm.eq(tool.tool_behaviour, TOOL_SHOVEL))
            return NONE;

        if (Dm.num(source.age) < 10)
        {
            to_chat(user, span_warning(I18n.T("The [] are too young to extract sap from!", LOWER_TEXT(source.myseed.plantname))));
            return ITEM_INTERACT_FAILURE;
        }
        if (Dm.num(source.age) > 19)
        {
            to_chat(user, span_warning(I18n.T("The [] are too old to extract sap from!", LOWER_TEXT(source.myseed.plantname))));
            return ITEM_INTERACT_FAILURE;
        }
        if (Dm.truthy(extracted))
        {
            to_chat(user, span_warning(I18n.T("The [] have already been harvested for sap!", LOWER_TEXT(source.myseed.plantname))));
            return ITEM_INTERACT_FAILURE;
        }

        extracted = TRUE;
        Dm.@new<obj_item_food_drug_opium_raw>(source.drop_location(), source.myseed.potency);
        // playsound(src, 'sound/effects/bubbles/bubbles.ogg', 30, TRUE): src is the gene datum, which has no turf, so playsound returns at once
        playsound(tool, Dm.resource("sound/items/weapons/bladeslice.ogg"), 30, TRUE);
        user.visible_message(
            span_notice(I18n.T("[] carefully slices open a [] pod, extracting a sap.", user, source.myseed.species)),
            span_notice(I18n.T("You carefully slice the []'s pod, collecting the fragrant, alluring sap.", source.myseed.species)),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        return ITEM_INTERACT_SUCCESS;
    }
}

/// Preset plant reagent genes that are unremovable from a plant.
// tg: code/modules/hydroponics/unique_plant_genes.dm:749
public partial class datum_plant_gene_reagent_preset : datum_plant_gene_reagent
{
    // tg: code/modules/hydroponics/unique_plant_genes.dm:752
    [DmParams("new_reagent_id", "new_reagent_rate")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_reagent(reagent_id);
    }
}
