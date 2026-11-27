// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/fish_traits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: every trait's effect on the fish and on the mobs grown from it, and on fishing (the difficulty and the catch
// weight the rod's line, bait and material change). The var blocks are generated.

// tg: code/modules/fishing/fish/fish_traits.dm:2 and :8 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars : datum_controller
{
    ///A global list of singleton fish traits by their paths
    // tg: code/modules/fishing/fish/fish_traits.dm:2  GLOBAL_LIST_INIT(fish_traits, init_subtypes_w_path_keys(/datum/fish_trait, list()))
    public DmList fish_traits;

    /**
     * A nested list of fish types and traits that they can spontaneously manifest with associated probabilities
     * e.g. list(/obj/item/fish = list(/datum/fish_trait = 100), etc...)
     */
    // tg: code/modules/fishing/fish/fish_traits.dm:8  GLOBAL_LIST_INIT(spontaneous_fish_traits, populate_spontaneous_fish_traits())
    public DmList spontaneous_fish_traits;

    // tg: code/modules/fishing/fish/fish_traits.dm:2  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual async DmTask InitGlobalfish_traits()
    {
        fish_traits = await init_subtypes_w_path_keys(typeof(datum_fish_trait), Dm.list()) as DmList;
        Dm.append(ref gvars_datum_init_order, "fish_traits");
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:8  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalspontaneous_fish_traits()
    {
        spontaneous_fish_traits = populate_spontaneous_fish_traits() as DmList;
        Dm.append(ref gvars_datum_init_order, "spontaneous_fish_traits");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/fishing/fish/fish_traits.dm:10
    public static object populate_spontaneous_fish_traits()
    {
        DmList list = Dm.list();
        foreach (object trait_path in Dm.each(GLOB.fish_traits))
        {
            datum_fish_trait trait = GLOB.fish_traits[trait_path] as datum_fish_trait;
            if (trait.spontaneous_manifest_types == null)
                continue;
            DmList trait_typecache = Dm.sub(zebra_typecacheof(trait.spontaneous_manifest_types), typeof(obj_item_fish)) as DmList;
            foreach (object fish_type in Dm.each(trait_typecache))
            {
                object trait_prob = trait_typecache[fish_type];
                if (!Dm.truthy(trait_prob))
                    continue;
                // LAZYSET(list[fish_type], trait_path, trait_typecache[fish_type]), inline (rules 11.1)
                if (list[fish_type] == null)
                    list[fish_type] = Dm.list();
                ((DmList)list[fish_type])[trait_path] = trait_typecache[fish_type];
            }
        }
        return list;
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:24
public partial class datum_fish_trait : datum
{
    /// Difficulty modifier from this mod, needs to return a list with two values
    // tg: code/modules/fishing/fish/fish_traits.dm:48
    [ShouldCallParent]
    public virtual object difficulty_mod(obj_item_fishing_rod rod = null, mob fisherman = null)
    {
        return Dm.list(Dm.kv(ADDITIVE_FISHING_MOD, 0), Dm.kv(MULTIPLICATIVE_FISHING_MOD, 1));
    }

    /// Catch weight table modifier from this mod, needs to return a list with two values
    // tg: code/modules/fishing/fish/fish_traits.dm:53
    [ShouldCallParent]
    public virtual async DmTask<object> catch_weight_mod(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null, Type fish_type = null)
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(ADDITIVE_FISHING_MOD, 0), Dm.kv(MULTIPLICATIVE_FISHING_MOD, 1));
    }

    /// Returns special minigame rules and effects applied by this trait
    // tg: code/modules/fishing/fish/fish_traits.dm:58
    public virtual void minigame_mod(obj_item_fishing_rod rod = null, mob fisherman = null, datum_fishing_challenge minigame = null)
    {
        return;
    }

    /// Applies some special qualities to the fish that has been spawned
    // tg: code/modules/fishing/fish/fish_traits.dm:62
    [ShouldCallParent]
    public virtual async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        initial ??= TRUE;
        if (!Dm.truthy(initial))
        {
            if (Dm.@in(type, fish.fish_traits))
                return FALSE;
            if (fish_whitelist != null && !Dm.@in(fish.type, fish_whitelist))
                return FALSE;
            if (Dm.truthy(length(Dm.bitand(fish.fish_traits, incompatible_traits))))
                return FALSE;
        }
        Dm.combine(ref fish.fish_traits, type);
        if (Dm.truthy(traits_to_add))
            fish.add_traits(traits_to_add, FISH_TRAIT_DATUM);
        if (Dm.truthy(length(reagents_to_add)))
            RegisterSignal(fish, COMSIG_GENERATE_REAGENTS_TO_ADD, nameof(add_reagents));
        await DmTask.CompletedTask;
        return TRUE;
    }

    /// Applies some special qualities to basic mobs generated by fish (i.e. chasm chrab --> young lobstrosity --> lobstrosity).
    // tg: code/modules/fishing/fish/fish_traits.dm:79
    [ShouldCallParent]
    public virtual async DmTask apply_to_mob(mob_living_basic mob = null)
    {
        RegisterSignal(mob, COMSIG_MOB_CHANGED_TYPE, nameof(on_transformed));
        await DmTask.CompletedTask;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:83
    [ShouldCallParent]
    public virtual async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        Dm.remove(ref fish.fish_traits, type);
        if (Dm.truthy(traits_to_add))
            fish.remove_traits(traits_to_add, FISH_TRAIT_DATUM);
        if (Dm.truthy(length(reagents_to_add)))
            UnregisterSignal(fish, COMSIG_GENERATE_REAGENTS_TO_ADD); // Kept as tg has it: tg passes PROC_REF(add_reagents) as a third argument, which UnregisterSignal ignores
        await DmTask.CompletedTask;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:91
    [SignalHandler]
    public virtual void on_transformed(mob source = null, mob desired_mob = null)
    {
        Waiting.NoSleep(apply_to_mob(desired_mob as mob_living_basic));
    }

    /// Proc used by both the predator and necrophage traits.
    // tg: code/modules/fishing/fish/fish_traits.dm:96
    public virtual async DmTask eat_fish(obj_item_fish predator = null, obj_item_fish prey = null)
    {
        string message = prey.status == FISH_DEAD ? I18n.T("[] eats []'s carcass.", predator, prey) : I18n.T("[] hunts down and eats [].", predator, prey);
        predator.loc.visible_message(span_warning(message));
        if (Dm.truthy(prey._listen_lookup?[COMSIG_FISH_EATEN_BY_OTHER_FISH]))
            prey._SendSignal(COMSIG_FISH_EATEN_BY_OTHER_FISH, Dm.list(prey, predator));
        qdel(prey);
        await predator.sate_hunger();
    }

    /**
     * Signal sent when we need to generate an abstract holder containing
     * reagents to be transfered, usually as a result of the fish being eaten by someone
     */
    // tg: code/modules/fishing/fish/fish_traits.dm:108
    [SignalHandler]
    public virtual void add_reagents(obj_item_fish fish = null, DmList reagents = null)
    {
        foreach (object reagent in Dm.each(reagents_to_add))
            reagents[reagent] = Dm.num(reagents[reagent]) + Dm.num(reagents_to_add[reagent]);
    }

    /// Proc that adds or changes the venomous when the fish size and/or weight are updated
    // tg: code/modules/fishing/fish/fish_traits.dm:114
    public virtual async DmTask add_venom(obj_item_fish source = null, object venom_path = null, object new_weight = null, float? mult = null)
    {
        mult ??= 0.25f;
        if (Dm.truthy(source.size))
        {
            float old_amount = max(round((Dm.num(source.weight) / FISH_GRIND_RESULTS_WEIGHT_DIVISOR) * mult.Value, 0.1f), mult.Value);
            source.RemoveElement(typeof(datum_element_venomous), venom_path, old_amount);
        }

        float new_amount = max(round((Dm.num(new_weight) / FISH_GRIND_RESULTS_WEIGHT_DIVISOR) * mult.Value, 0.1f), mult.Value);
        await source.AddElement(typeof(datum_element_venomous), venom_path, new_amount);
    }

    /// Proc that changes the venomous element based on if the fish is alive or dead (basically dead fish are weaker).
    // tg: code/modules/fishing/fish/fish_traits.dm:123
    public virtual async DmTask change_venom(obj_item_fish source = null, object venom_path = null, object live_mult = null, object dead_mult = null, float? just_remove = null)
    {
        just_remove ??= FALSE;
        float live_amount = max(round((Dm.num(source.weight) / FISH_GRIND_RESULTS_WEIGHT_DIVISOR) * Dm.num(live_mult), 0.1f), Dm.num(live_mult));
        float dead_amount = max(round((Dm.num(source.weight) / FISH_GRIND_RESULTS_WEIGHT_DIVISOR) * Dm.num(dead_mult), 0.1f), Dm.num(dead_mult));
        bool is_dead = source.status == FISH_DEAD;
        source.RemoveElement(typeof(datum_element_venomous), venom_path, is_dead ? live_amount : dead_amount, Dm.named("thrown_effect", TRUE));
        if (Dm.truthy(just_remove))
            return;
        await source.AddElement(typeof(datum_element_venomous), venom_path, is_dead ? dead_amount : live_amount, Dm.named("thrown_effect", TRUE));
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:132
public partial class datum_fish_trait_wary : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:137
    public override object difficulty_mod(obj_item_fishing_rod rod = null, mob fisherman = null)
    {
        object dot = base.difficulty_mod(rod, fisherman);
        if (Dm.num(rod.alpha) <= Dm.num(Dm.initial(typeof(datum_material_glass), (datum_material p) => p.alpha)))
            return dot;
        // Wary fish require transparent line or they're harder
        if (rod.line_value == null || !Dm.truthy(Dm.bitand(rod.line_value.fishing_line_traits, FISHING_LINE_CLOAKED)))
            ((DmList)dot)[ADDITIVE_FISHING_MOD] = Dm.num(((DmList)dot)[ADDITIVE_FISHING_MOD]) + FISH_TRAIT_MINOR_DIFFICULTY_BOOST;
        return dot;
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:145
public partial class datum_fish_trait_shiny_lover : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:150
    public override object difficulty_mod(obj_item_fishing_rod rod = null, mob fisherman = null)
    {
        object dot = base.difficulty_mod(rod, fisherman);
        // These fish are easier to catch with shiny hook
        if (HAS_TRAIT(rod, TRAIT_ROD_ATTRACT_SHINY_LOVERS) || Dm.num(rod.bait_value?.get_item_credit_value()) >= PAYCHECK_CREW)
            ((DmList)dot)[ADDITIVE_FISHING_MOD] = Dm.num(((DmList)dot)[ADDITIVE_FISHING_MOD]) - FISH_TRAIT_MINOR_DIFFICULTY_BOOST;
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:156
    public override async DmTask<object> catch_weight_mod(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null, Type fish_type = null)
    {
        object dot = await base.catch_weight_mod(rod, fisherman, location, fish_type);
        // These fish are harder to find without a shiny hook
        if (!HAS_TRAIT(rod, TRAIT_ROD_ATTRACT_SHINY_LOVERS))
            ((DmList)dot)[MULTIPLICATIVE_FISHING_MOD] = 0.5f;
        return dot;
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:162
public partial class datum_fish_trait_picky_eater : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:167
    public override async DmTask<object> catch_weight_mod(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null, Type fish_type = null)
    {
        DmList dot = await base.catch_weight_mod(rod, fisherman, location, fish_type) as DmList;
        DmList fav_baits = (SSfishing.fish_properties[fish_type] as DmList)?[FISH_PROPERTIES_FAV_BAIT] as DmList;
        foreach (object identifier in Dm.each(fav_baits))
        {
            if (identifier is not DmList identifier_list)
                continue;
            if (!Dm.eq(identifier_list[FISH_BAIT_TYPE], FISH_BAIT_FOODTYPE))
                continue;
            if (Dm.truthy(is_matching_bait(rod, identifier_list))) //Bait or no bait, it's a yummy rod.
                return dot;
        }
        if (rod.bait_value == null)
        {
            dot[MULTIPLICATIVE_FISHING_MOD] = 0;
            return dot;
        }
        if (HAS_TRAIT(rod.bait_value, TRAIT_OMNI_BAIT))
            return dot;

        foreach (object identifier in Dm.each(fav_baits))
        {
            if (Dm.truthy(is_matching_bait(rod.bait_value, identifier))) //we like this bait anyway
                return dot;
        }

        DmList bad_baits = (SSfishing.fish_properties[fish_type] as DmList)?[FISH_PROPERTIES_BAD_BAIT] as DmList;
        foreach (object identifier in Dm.each(bad_baits))
        {
            if (Dm.truthy(is_matching_bait(rod.bait_value, identifier))) //we hate this bait.
            {
                dot[MULTIPLICATIVE_FISHING_MOD] = 0;
                return dot;
            }
        }

        if (!HAS_TRAIT(rod.bait_value, TRAIT_GOOD_QUALITY_BAIT) && !HAS_TRAIT(rod.bait_value, TRAIT_GREAT_QUALITY_BAIT))
            dot[MULTIPLICATIVE_FISHING_MOD] = 0;
        return dot;
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:194
public partial class datum_fish_trait_nocturnal : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:199
    public override async DmTask<object> catch_weight_mod(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null, Type fish_type = null)
    {
        object dot = await base.catch_weight_mod(rod, fisherman, location, fish_type);
        if (HAS_TRAIT(rod, TRAIT_ROD_IGNORE_ENVIRONMENT))
            return dot;
        turf turf = get_turf(location) as turf;
        if (Dm.truthy(turf?.check_lumcount_above(SHADOW_SPECIES_LIGHT_THRESHOLD)))
            ((DmList)dot)[MULTIPLICATIVE_FISHING_MOD] = 0;
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:207
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_FISH_LIFE, nameof(check_light));
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:211
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, COMSIG_FISH_LIFE);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:215
    [SignalHandler]
    public virtual void check_light(obj_item_fish source = null, object seconds_per_tick = null)
    {
        if (source.loc == null || (!HAS_TRAIT(source.loc, TRAIT_IS_AQUARIUM) && !isturf(source.loc)))
            return;
        turf turf = get_turf(source) as turf;
        if (Dm.truthy(turf.check_lumcount_above(SHADOW_SPECIES_LIGHT_THRESHOLD)))
            Waiting.NoSleep(source.damage_fish(0.5f * Dm.num(seconds_per_tick)));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:223
    public override async DmTask apply_to_mob(mob_living_basic mob = null)
    {
        await base.apply_to_mob(mob);
        // Make sure the mob can also see in the dark
        ADD_TRAIT(mob, TRAIT_TRUE_NIGHT_VISION, FISH_TRAIT_DATUM);
        RegisterSignal(mob, COMSIG_LIVING_HANDLE_BREATHING, nameof(on_non_stasis_life));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:229
    [SignalHandler]
    public virtual void on_non_stasis_life(mob_living_basic mob = null, object seconds_per_tick = null)
    {
        seconds_per_tick ??= SSMOBS_DT();
        turf our_turf = mob.loc as turf;
        if (!isturf(our_turf))
            return;

        if (Dm.truthy(our_turf.check_lumcount_below(SHADOW_SPECIES_LIGHT_THRESHOLD))) //heal in the dark
            Waiting.NoSleep(mob.apply_status_effect(typeof(datum_status_effect_shadow_regeneration)));
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:238
public partial class datum_fish_trait_heavy : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:242
    public override async DmTask apply_to_mob(mob_living_basic mob = null)
    {
        await base.apply_to_mob(mob);
        await mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_heavy_fish));
        mob.maxHealth *= 1.5f;
        mob.health *= 1.5f;
        mob.melee_damage_lower *= 1.3f;
        mob.melee_damage_upper *= 1.3f;
        mob.obj_damage *= 1.3f;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:251
    public override void minigame_mod(obj_item_fishing_rod rod = null, mob fisherman = null, datum_fishing_challenge minigame = null)
    {
        minigame.mover.fish_idle_velocity -= 10;
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:254
public partial class datum_fish_trait_carnivore : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:259
    public override async DmTask<object> catch_weight_mod(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null, Type fish_type = null)
    {
        DmList dot = await base.catch_weight_mod(rod, fisherman, location, fish_type) as DmList;
        if (IS_EDIBLE(rod)) //who cares about the bait, that fishing rod is yummy!
            return dot;
        if (rod.bait_value == null)
        {
            dot[MULTIPLICATIVE_FISHING_MOD] = 0;
            return dot;
        }
        if (HAS_TRAIT(rod.bait_value, TRAIT_OMNI_BAIT))
            return dot;
        DmList bait_identifier = Dm.list(
            Dm.kv(FISH_BAIT_TYPE, FISH_BAIT_FOODTYPE),
            Dm.kv(FISH_BAIT_VALUE, Dm.bitor(Dm.bitor(MEAT, SEAFOOD), BUGS)));
        if (!Dm.truthy(is_matching_bait(rod.bait_value, bait_identifier)))
            dot[MULTIPLICATIVE_FISHING_MOD] = 0;
        return dot;
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:275
public partial class datum_fish_trait_vegan : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:280
    public override async DmTask<object> catch_weight_mod(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null, Type fish_type = null)
    {
        DmList dot = await base.catch_weight_mod(rod, fisherman, location, fish_type) as DmList;
        if (await rod.get_master_material() is datum_material_bamboo) //bamboo is technically grass.
            return dot;
        if (rod.bait_value == null)
        {
            dot[MULTIPLICATIVE_FISHING_MOD] = 0;
            return dot;
        }
        if (HAS_TRAIT(rod.bait_value, TRAIT_OMNI_BAIT))
            return dot;
        if (istype(rod.bait_value, typeof(obj_item_food_grown)))
            return dot;
        DmList bait_liked_identifier = Dm.list(
            Dm.kv(FISH_BAIT_TYPE, FISH_BAIT_FOODTYPE),
            Dm.kv(FISH_BAIT_VALUE, Dm.bitor(VEGETABLES, FRUIT)));
        DmList bait_hated_identifier = Dm.list(
            Dm.kv(FISH_BAIT_TYPE, FISH_BAIT_FOODTYPE),
            Dm.kv(FISH_BAIT_VALUE, Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(MEAT, SEAFOOD), GORE), BUGS), DAIRY)));
        if (!Dm.truthy(is_matching_bait(rod.bait_value, bait_liked_identifier)) || Dm.truthy(is_matching_bait(rod.bait_value, bait_hated_identifier)))
            dot[MULTIPLICATIVE_FISHING_MOD] = 0;
        return dot;
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:302
public partial class datum_fish_trait_emulsijack : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:308
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_FISH_LIFE, nameof(emulsify));
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:312
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, COMSIG_FISH_LIFE);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:316
    [SignalHandler]
    public virtual void emulsify(obj_item_fish source = null, object seconds_per_tick = null)
    {
        if (source.loc == null || !HAS_TRAIT(source.loc, TRAIT_IS_AQUARIUM))
            return;
        float emulsified = FALSE;
        foreach (obj_item_fish victim in Dm.each<obj_item_fish>(source.loc))
        {
            // Kept as tg has it: the continue skips only the inner loop, so no resistance trait spares a victim.
            foreach (object trait in Dm.each(resistance_traits))
            {
                if (HAS_TRAIT(victim, trait))
                    continue;
            }
            Waiting.NoSleep(victim.damage_fish(3 * Dm.num(seconds_per_tick))); //the victim may heal a bit but this will quickly kill
            emulsified = TRUE;
        }
        if (Dm.truthy(emulsified))
        {
            Waiting.NoSleep(source.repair_damage(3 * Dm.num(seconds_per_tick)));
            Waiting.NoSleep(source.sate_hunger());
        }
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:331
    public override async DmTask apply_to_mob(mob_living_basic mob = null)
    {
        await base.apply_to_mob(mob);
        RegisterSignal(mob, COMSIG_LIVING_HANDLE_BREATHING, nameof(on_non_stasis_life));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:335
    [SignalHandler]
    public virtual void on_non_stasis_life(mob_living_basic mob = null, object seconds_per_tick = null)
    {
        seconds_per_tick ??= SSMOBS_DT();
        turf_open our_turf = get_turf(mob) as turf_open;
        if (Dm.num(((datum_gas_mixture)Waiting.NoSleep(our_turf.return_air())).return_pressure()) > ONE_ATMOSPHERE * 1.5f) //put a cap otherwise closed spaces may overpressurize
            return;

        datum_gas_mixture stench = Dm.@new<datum_gas_mixture>();

        stench.set_gas(typeof(datum_gas_miasma), MIASMA_CORPSE_MOLES * 2 * Dm.num(seconds_per_tick));
        stench.temperature = mob.bodytemperature;
        Waiting.NoSleep(our_turf.assume_air(stench));
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:347
public partial class datum_fish_trait_emulsijack_psychic : datum_fish_trait_emulsijack
{
    // tg: code/modules/fishing/fish/fish_traits.dm:353
    public override void on_non_stasis_life(mob_living_basic mob = null, object seconds_per_tick = null)
    {
        return;
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:356
public partial class datum_fish_trait_necrophage : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:361
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_FISH_LIFE, nameof(eat_dead_fishes));
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:365
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, COMSIG_FISH_LIFE);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:369
    [SignalHandler]
    public virtual void eat_dead_fishes(obj_item_fish source = null, object seconds_per_tick = null)
    {
        if (Dm.num(source.get_hunger()) < 0.75f || source.loc == null || !HAS_TRAIT(source.loc, TRAIT_IS_AQUARIUM))
            return;
        foreach (obj_item_fish victim in Dm.each<obj_item_fish>(source.loc))
        {
            if (victim.status != FISH_DEAD || victim == source || HAS_TRAIT(victim, TRAIT_YUCKY_FISH))
                continue;
            Waiting.NoSleep(eat_fish(source, victim));
            return;
        }
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:410
public partial class datum_fish_trait_revival : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:416
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_FISH_STATUS_CHANGED, nameof(check_status));
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:420
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        // Kept as tg has it: tg passes FISH_TRAIT_DATUM as a third argument, which UnregisterSignal ignores.
        UnregisterSignal(fish, COMSIG_FISH_STATUS_CHANGED);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:424
    [SignalHandler]
    public virtual void check_status(obj_item_fish source = null)
    {
        if (source.status == FISH_DEAD)
            _addtimer(Dm.@new<datum_callback>(this, nameof(revive), WEAKREF(source)), rand(1 * MINUTES, 2 * MINUTES), file: "code/modules/fishing/fish/fish_traits.dm", line: 427);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:429
    public virtual async DmTask revive(datum_weakref fish_ref = null)
    {
        obj_item_fish source = fish_ref.resolve() as obj_item_fish;
        if (QDELETED(source) || source.status != FISH_DEAD || !Dm.@in(type, source.fish_traits))
            return;
        await source.set_status(FISH_ALIVE);
        object message = span_nicegreen(I18n.T("[] twitches. It's alive!", source));
        if (source.loc != null && HAS_TRAIT(source.loc, TRAIT_IS_AQUARIUM))
            source.loc.visible_message(message);
        else
            source.visible_message(message);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:440
    public override async DmTask apply_to_mob(mob_living_basic mob = null)
    {
        await base.apply_to_mob(mob);
        await mob.AddComponent(typeof(datum_component_regenerator), Dm.named("regeneration_delay", 6 * SECONDS), Dm.named("brute_per_second", 2 * SECONDS), Dm.named("outline_colour", COLOR_BLUE));
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:444  (its catch_weight_mod reads the rod's bait: live-partial)
public partial class datum_fish_trait_predator : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:450
    public override async DmTask<object> catch_weight_mod(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null, Type fish_type = null)
    {
        DmList dot = await base.catch_weight_mod(rod, fisherman, location, fish_type) as DmList;
        if (isfish(rod.bait_value))
            dot[MULTIPLICATIVE_FISHING_MOD] = Dm.num(dot[MULTIPLICATIVE_FISHING_MOD]) * 2;
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:455
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_FISH_LIFE, nameof(eat_fishes));
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:459
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, COMSIG_FISH_LIFE);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:463
    [SignalHandler]
    public virtual void eat_fishes(obj_item_fish source = null, object seconds_per_tick = null)
    {
        if (Dm.num(source.get_hunger()) < 0.75f || source.loc == null || !HAS_TRAIT(source.loc, TRAIT_IS_AQUARIUM))
            return;
        foreach (obj_item_fish victim in Dm.each_cast<obj_item_fish>(source.get_aquarium_fishes(TRUE, source)))
        {
            if (Dm.num(victim.size) >= Dm.num(source.size) * 0.7f) // It's a big fish eat small fish world
                continue;
            if (victim.status != FISH_ALIVE || HAS_TRAIT(victim, TRAIT_YUCKY_FISH) || SPT_PROB(80, seconds_per_tick))
                continue;
            Waiting.NoSleep(eat_fish(source, victim));
            return;
        }
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:482
public partial class datum_fish_trait_toxic : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:490
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        initial ??= TRUE;
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_FISH_UPDATE_SIZE_AND_WEIGHT, nameof(make_venomous));
        RegisterSignal(fish, COMSIG_FISH_STATUS_CHANGED, nameof(on_status_change));
        RegisterSignal(fish, COMSIG_FISH_EATEN_BY_OTHER_FISH, nameof(on_eaten));
        if (!Dm.truthy(initial))
            make_venomous(fish, fish.size, fish.weight);
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:498
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, Dm.list(COMSIG_FISH_UPDATE_SIZE_AND_WEIGHT, COMSIG_FISH_STATUS_CHANGED, COMSIG_FISH_EATEN_BY_OTHER_FISH));
        await change_venom(fish, reagents_to_add[1], 0.25f * venom_mult, 0.1f * venom_mult, just_remove: TRUE);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:503
    [SignalHandler]
    public virtual void make_venomous(obj_item_fish source = null, object new_size = null, object new_weight = null)
    {
        if (!HAS_TRAIT(source, TRAIT_FISH_STINGER))
            return;
        Waiting.NoSleep(add_venom(source, reagents_to_add[1], new_weight, mult: (source.status == FISH_DEAD ? 0.1f : 0.25f) * venom_mult));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:509
    [SignalHandler]
    public virtual void on_status_change(obj_item_fish source = null)
    {
        if (!HAS_TRAIT(source, TRAIT_FISH_STINGER))
            return;
        Waiting.NoSleep(change_venom(source, reagents_to_add[1], 0.25f * venom_mult, 0.1f * venom_mult));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:515
    public virtual void on_eaten(obj_item_fish source = null, obj_item_fish predator = null)
    {
        if (HAS_TRAIT(predator, TRAIT_FISH_TOXIN_IMMUNE))
            return;
        RegisterSignal(predator, COMSIG_FISH_LIFE, nameof(damage_predator), TRUE);
        RegisterSignal(predator, COMSIG_FISH_STATUS_CHANGED, nameof(stop_damaging), TRUE);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:521
    [SignalHandler]
    public virtual void damage_predator(obj_item_fish source = null, object seconds_per_tick = null)
    {
        Waiting.NoSleep(source.damage_fish(3 * Dm.num(seconds_per_tick)));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:525
    [SignalHandler]
    public virtual void stop_damaging(obj_item_fish source = null)
    {
        if (source.status == FISH_DEAD)
            UnregisterSignal(source, Dm.list(COMSIG_FISH_LIFE, COMSIG_FISH_STATUS_CHANGED));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:530
    public override async DmTask apply_to_mob(mob_living_basic mob = null)
    {
        await base.apply_to_mob(mob);
        await mob.AddElement(typeof(datum_element_venomous), reagents_to_add[1], 0.5f * mob.mob_size * venom_mult);
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:556
public partial class datum_fish_trait_territorial : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:561
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_FISH_LIFE, nameof(try_attack_fish));
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:565
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, COMSIG_FISH_LIFE);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:569
    [SignalHandler]
    public virtual void try_attack_fish(obj_item_fish source = null, object seconds_per_tick = null)
    {
        if (source.loc == null || !HAS_TRAIT(source.loc, TRAIT_IS_AQUARIUM) || !SPT_PROB(1, seconds_per_tick))
            return;
        DmList fishes = source.get_aquarium_fishes(TRUE, source) as DmList;
        if (length(fishes) < 5)
            return;
        foreach (obj_item_fish victim in Dm.each_cast<obj_item_fish>(source.get_aquarium_fishes(TRUE, source)))
        {
            if (victim.status != FISH_ALIVE)
                continue;
            source.loc.visible_message(span_warning(I18n.T("[] violently [] []", source, pick("whips", "bites", "attacks", "slams"), victim)));
            float damage = round(rand(4, 20) * (Dm.num(source.size) / Dm.num(victim.size))); //smaller fishes take extra damage.
            Waiting.NoSleep(victim.damage_fish(damage));
            return;
        }
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:584
public partial class datum_fish_trait_lubed : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:592
    public override async DmTask<object> catch_weight_mod(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null, Type fish_type = null)
    {
        object dot = await base.catch_weight_mod(rod, fisherman, location, fish_type);
        if (await rod.get_master_material() is datum_material_bananium) //x5 chance of catching lubefish & co with a bananium rod.
            ((DmList)dot)[MULTIPLICATIVE_FISHING_MOD] = Dm.num(((DmList)dot)[MULTIPLICATIVE_FISHING_MOD]) * 5;
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:597
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        object dot = await base.apply_to_fish(fish, initial);
        await fish.AddComponent(typeof(datum_component_slippery), 8 * SECONDS, Dm.bitor(SLIDE, GALOSHES_DONT_HELP));
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:601
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        qdel(fish.GetComponent(typeof(datum_component_slippery)));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:605
    public override async DmTask apply_to_mob(mob_living_basic mob = null)
    {
        await base.apply_to_mob(mob);
        await mob.AddElement(typeof(datum_element_lube_walking));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:609
    public override void minigame_mod(obj_item_fishing_rod rod = null, mob fisherman = null, datum_fishing_challenge minigame = null)
    {
        minigame.reeling_velocity *= 1.4f;
        minigame.gravity_velocity *= 1.4f;
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:613
public partial class datum_fish_trait_amphibious : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:620
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        object dot = await base.apply_to_fish(fish, initial);
        if (fish.required_fluid_type == AQUARIUM_FLUID_AIR)
            fish.required_fluid_type = AQUARIUM_FLUID_FRESHWATER;
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:625
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        fish.required_fluid_type = Dm.initial(fish, (obj_item_fish p) => p.required_fluid_type);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:629
    public override async DmTask apply_to_mob(mob_living_basic mob = null)
    {
        await base.apply_to_mob(mob);
        ADD_TRAIT(mob, TRAIT_NODROWN, FISH_TRAIT_DATUM);
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:639
public partial class datum_fish_trait_antigrav : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:645
    public override void minigame_mod(obj_item_fishing_rod rod = null, mob fisherman = null, datum_fishing_challenge minigame = null)
    {
        Dm.combine(ref minigame.special_effects, FISHING_MINIGAME_RULE_ANTIGRAV);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:648
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        object dot = await base.apply_to_fish(fish, initial);
        await fish.AddElement(typeof(datum_element_forced_gravity), NEGATIVE_GRAVITY);
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:652
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        fish.RemoveElement(typeof(datum_element_forced_gravity), NEGATIVE_GRAVITY);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:656
    public override async DmTask apply_to_mob(mob_living_basic mob = null)
    {
        await base.apply_to_mob(mob);
        mob.add_traits(Dm.list(TRAIT_FREE_HYPERSPACE_MOVEMENT, TRAIT_SPACEWALK), FISH_TRAIT_DATUM);
        await mob.AddElement(typeof(datum_element_simple_flying));
    }
}

///Anxiety means the fish will die if in a location with more than 3 fish (including itself)
///This is just barely enough to crossbreed out of anxiety, but it severely limits the potential of
// tg: code/modules/fishing/fish/fish_traits.dm:663  (its difficulty_mod reads the rod's line: live-partial)
public partial class datum_fish_trait_anxiety : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:668
    public override object difficulty_mod(obj_item_fishing_rod rod = null, mob fisherman = null)
    {
        object dot = base.difficulty_mod(rod, fisherman);
        // Anxious fish are easier with a cloaked line.
        if (rod.line_value != null && Dm.truthy(Dm.bitand(rod.line_value.fishing_line_traits, FISHING_LINE_CLOAKED)))
            ((DmList)dot)[ADDITIVE_FISHING_MOD] = Dm.num(((DmList)dot)[ADDITIVE_FISHING_MOD]) - FISH_TRAIT_MINOR_DIFFICULTY_BOOST;
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:674
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        object dot = await base.apply_to_fish(fish, initial);
        // Kept as tg has it: it unregisters instead of registering, so an anxious fish never freaks out.
        UnregisterSignal(fish, COMSIG_FISH_LIFE);
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:678
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, COMSIG_FISH_LIFE);
    }

    ///signal sent when the anxiety fish is fed, killing it if sharing contents with too many fish.
    // tg: code/modules/fishing/fish/fish_traits.dm:683
    [SignalHandler]
    public virtual void on_fish_life(obj_item_fish fish = null, object seconds_per_tick = null)
    {
        float fish_tolerance = 3;
        if (fish.loc == null || fish.status == FISH_DEAD)
            return;
        foreach (obj_item_fish other_fish in Dm.each<obj_item_fish>(fish.loc.contents))
        {
            if (fish_tolerance <= 0)
            {
                fish.loc.visible_message(span_warning(I18n.T("[] seems to freak out for a moment, then it stops moving...", fish)));
                Waiting.NoSleep(fish.set_status(FISH_DEAD));
                return;
            }
            fish_tolerance -= 1;
        }
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:695
public partial class datum_fish_trait_electrogenesis : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:701
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        initial ??= TRUE;
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_FISH_FORCE_UPDATED, nameof(on_force_updated));
        // Kept as tg has it: the second "signal" is a trait name, which no one sends.
        RegisterSignals(fish, Dm.list(COMSIG_ITEM_FRIED, TRAIT_FOOD_BBQ_GRILLED), nameof(on_fish_cooked));
        if (!Dm.truthy(initial))
            await fish.update_fish_force();
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:708
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, Dm.list(COMSIG_FISH_FORCE_UPDATED, COMSIG_ITEM_FRIED, TRAIT_FOOD_BBQ_GRILLED));
        await fish.update_fish_force();
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:713
    [SignalHandler]
    public virtual void on_fish_cooked(obj_item_fish fish = null, object cooked_time = null)
    {
        if (Dm.num(cooked_time) >= FISH_SAFE_COOKING_DURATION)
            Waiting.NoSleep(((datum_reagents)fish.reagents).del_reagent(typeof(datum_reagent_consumable_liquidelectricity)));
        else
            Waiting.NoSleep(((datum_reagents)fish.reagents).multiply(0.66f, typeof(datum_reagent_consumable_liquidelectricity)));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:720
    public override void add_reagents(obj_item_fish fish = null, DmList reagents = null)
    {
        base.add_reagents(fish, reagents);
        if (HAS_TRAIT(fish, TRAIT_FISH_WELL_COOKED)) // Cooking it well removes all liquid electricity
            Dm.remove(reagents, typeof(datum_reagent_consumable_liquidelectricity));
        else
        {
            Dm.remove(reagents, typeof(datum_reagent_blood));
            //Otherwise, undercooking it will remove 2/3 of it.
            if (!HAS_TRAIT(fish, TRAIT_FOOD_FRIED) && !HAS_TRAIT(fish, TRAIT_FOOD_BBQ_GRILLED))
                reagents[typeof(datum_reagent_consumable_liquidelectricity)] = Dm.num(reagents[typeof(datum_reagent_consumable_liquidelectricity)]) - 1;
        }
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:730
    [SignalHandler]
    public virtual void on_force_updated(obj_item_fish fish = null, object weight_rank = null, object bonus_or_malus = null)
    {
        if (fish.status == FISH_ALIVE)
        {
            fish.force += 10 - fish.w_class;
            fish.damtype = BURN;
            fish.attack_verb_continuous = Dm.list("shocks", "zaps");
            fish.attack_verb_simple = Dm.list("shock", "zap");
            fish.hitsound = Dm.resource("sound/effects/sparks/sparks4.ogg");
        }
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:739
    public override async DmTask apply_to_mob(mob_living_basic mob = null)
    {
        await base.apply_to_mob(mob);
        ADD_TRAIT(mob, TRAIT_SHOCKIMMUNE, FISH_TRAIT_DATUM);
        mob.grant_actions_by_list(Dm.list(typeof(datum_action_cooldown_mob_cooldown_charge_apc)));
        await mob.AddElement(typeof(datum_element_venomous), typeof(datum_reagent_teslium), 3 * mob.mob_size);
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:745
public partial class datum_fish_trait_stunted : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:751
    public override async DmTask apply_to_mob(mob_living_basic mob = null)
    {
        await base.apply_to_mob(mob);
        qdel(mob.GetComponent(typeof(datum_component_growth_and_differentiation)));
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:755
public partial class datum_fish_trait_stinger : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:766
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        initial ??= TRUE;
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_FISH_FORCE_UPDATED, nameof(on_force_updated));
        if (!Dm.truthy(initial))
            await fish.update_fish_force();
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:772
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, COMSIG_FISH_FORCE_UPDATED);
        await fish.update_fish_force();
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:777
    [SignalHandler]
    public virtual void on_force_updated(obj_item_fish fish = null, object weight_rank = null, object bonus_or_malus = null)
    {
        fish.force += 1 + fish.w_class + Dm.num(bonus_or_malus);
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:781
public partial class datum_fish_trait_toxic_barbs : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:787
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        initial ??= TRUE;
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_FISH_UPDATE_SIZE_AND_WEIGHT, nameof(make_venomous));
        RegisterSignal(fish, COMSIG_FISH_STATUS_CHANGED, nameof(on_status_change));
        if (!Dm.truthy(initial))
            make_venomous(fish, fish.size, fish.weight);
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:794
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, Dm.list(COMSIG_FISH_UPDATE_SIZE_AND_WEIGHT, COMSIG_FISH_STATUS_CHANGED));
        await change_venom(fish, typeof(datum_reagent_toxin_venom), 0.7f, 0.3f, just_remove: TRUE);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:799
    [SignalHandler]
    public virtual void make_venomous(obj_item_fish source = null, object new_size = null, object new_weight = null)
    {
        if (!HAS_TRAIT(source, TRAIT_FISH_STINGER))
        {
            ///Remove the trait from the fish so it doesn't show on the analyzer as it doesn't do anything on stingerless ones.
            Waiting.NoSleep(remove_from_fish(source));
            return;
        }
        Waiting.NoSleep(add_venom(source, typeof(datum_reagent_toxin_venom), new_weight, mult: source.status == FISH_DEAD ? 0.3f : 0.7f));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:807
    [SignalHandler]
    public virtual void on_status_change(obj_item_fish source = null)
    {
        if (!HAS_TRAIT(source, TRAIT_FISH_STINGER))
            return;
        Waiting.NoSleep(change_venom(source, typeof(datum_reagent_toxin_venom), 0.7f, 0.3f));
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:813
public partial class datum_fish_trait_hallucinogenic : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:819
    public override void add_reagents(obj_item_fish fish = null, DmList reagents = null)
    {
        // Kept as tg has it: it checks the trait datum (src), not the fish, so the cooked fish keeps its neurotoxin.
        if (!HAS_TRAIT(this, TRAIT_FOOD_FRIED) && !HAS_TRAIT(this, TRAIT_FOOD_BBQ_GRILLED))
            base.add_reagents(fish, reagents);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:823
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        initial ??= TRUE;
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_FISH_UPDATE_SIZE_AND_WEIGHT, nameof(make_venomous));
        RegisterSignal(fish, COMSIG_FISH_STATUS_CHANGED, nameof(on_status_change));
        if (!Dm.truthy(initial))
            make_venomous(fish, fish.size, fish.weight);
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:830
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, Dm.list(COMSIG_FISH_UPDATE_SIZE_AND_WEIGHT, COMSIG_FISH_STATUS_CHANGED));
        await change_venom(fish, typeof(datum_reagent_toxin_mindbreaker_fish), 0.7f, 0.3f, just_remove: TRUE);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:835
    [SignalHandler]
    public virtual void make_venomous(obj_item_fish source = null, object new_size = null, object new_weight = null)
    {
        if (!HAS_TRAIT(source, TRAIT_FISH_STINGER))
        {
            ///Remove the trait from the fish so it doesn't show on the analyzer as it doesn't do anything on stingerless ones.
            Dm.remove(ref source.fish_traits, type);
            UnregisterSignal(source, Dm.list(COMSIG_FISH_UPDATE_SIZE_AND_WEIGHT, COMSIG_FISH_STATUS_CHANGED));
            return;
        }
        Waiting.NoSleep(add_venom(source, typeof(datum_reagent_toxin_mindbreaker_fish), new_weight, mult: source.status == FISH_DEAD ? 0.3f : 0.7f));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:844
    [SignalHandler]
    public virtual void on_status_change(obj_item_fish source = null)
    {
        if (!HAS_TRAIT(source, TRAIT_FISH_STINGER))
            return;
        Waiting.NoSleep(change_venom(source, typeof(datum_reagent_toxin_mindbreaker_fish), 0.7f, 0.3f));
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:850
public partial class datum_fish_trait_ink : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:857
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_ATOM_PROCESSED, nameof(on_process));
        RegisterSignal(fish, COMSIG_ITEM_ATTACK_ZONE, nameof(attacked_someone));
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:862
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, Dm.list(COMSIG_ATOM_PROCESSED, COMSIG_ITEM_ATTACK_ZONE));
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:866
    [SignalHandler]
    public virtual void attacked_someone(obj_item_fish source = null, mob_living target = null, mob_living user = null, object zone = null)
    {
        if (HAS_TRAIT(source, TRAIT_FISH_INK_ON_COOLDOWN) || source.status == FISH_DEAD)
            return;
        if (!iscarbon(target) || Dm.truthy(target.get_bodypart(BODY_ZONE_HEAD)))
        {
            Waiting.NoSleep(target.adjust_temp_blindness_up_to(4 * SECONDS, 8 * SECONDS));
            Waiting.NoSleep(target.adjust_confusion_up_to(1.5f * SECONDS, 4 * SECONDS));
            Waiting.NoSleep(target.AddComponent(typeof(datum_component_face_decal_splat),
                Dm.named("color", COLOR_NEARLY_ALL_BLACK),
                Dm.named("memory_type", typeof(datum_memory_witnessed_inking)),
                Dm.named("mood_event_type", typeof(datum_mood_event_inked))));
        }
        target.visible_message(span_warning(I18n.T("[] is inked by []!", target, source)), span_userdanger(I18n.T("You've been inked by []!", source)));
        playsound(target, SFX_DESECRATION, 50, TRUE);
        ADD_TRAIT(source, TRAIT_FISH_INK_ON_COOLDOWN, FISH_TRAIT_DATUM);
        _addtimer(TRAIT_CALLBACK_REMOVE(source, TRAIT_FISH_INK_ON_COOLDOWN, FISH_TRAIT_DATUM), 9 * SECONDS, file: "code/modules/fishing/fish/fish_traits.dm", line: 881);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:883
    [SignalHandler]
    public virtual void on_process(obj_item_fish source = null, mob_living user = null, obj_item process_item = null, DmList results = null)
    {
        Dm.@new<obj_item_food_ink_sac>(source.drop_location());
    }
}

// tg: code/modules/fishing/fish/fish_traits.dm:887
public partial class datum_fish_trait_camouflage : datum_fish_trait
{
    // tg: code/modules/fishing/fish/fish_traits.dm:894
    public override void minigame_mod(obj_item_fishing_rod rod = null, mob fisherman = null, datum_fishing_challenge minigame = null)
    {
        Dm.combine(ref minigame.special_effects, FISHING_MINIGAME_RULE_CAMO);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:897
    public override async DmTask<object> apply_to_fish(obj_item_fish fish = null, float? initial = null)
    {
        object dot = await base.apply_to_fish(fish, initial);
        RegisterSignal(fish, COMSIG_FISH_LIFE, nameof(fade_out));
        RegisterSignals(fish, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_FISH_STATUS_CHANGED), nameof(reset_alpha));
        return dot;
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:902
    public override async DmTask remove_from_fish(obj_item_fish fish = null)
    {
        await base.remove_from_fish(fish);
        UnregisterSignal(fish, Dm.list(COMSIG_FISH_LIFE, COMSIG_MOVABLE_MOVED, COMSIG_FISH_STATUS_CHANGED));
        reset_alpha(fish);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:907
    [SignalHandler]
    public virtual void fade_out(obj_item_fish source = null, object seconds_per_tick = null)
    {
        if (source.status == FISH_DEAD || Dm.num(source.last_move) + 5 * SECONDS >= world.time)
            return;
        source.alpha = max(source.alpha - 10 * Dm.num(seconds_per_tick), 10);
    }

    // tg: code/modules/fishing/fish/fish_traits.dm:913
    [SignalHandler]
    public virtual void reset_alpha(obj_item_fish source = null)
    {
        if (QDELETED(source))
            return;
        float init_alpha = Dm.initial(source, (atom p) => p.alpha);
        if (init_alpha != source.alpha)
            animate(source, Dm.named("alpha", init_alpha), Dm.named("time", 1.2f * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_OUT)));
    }
}
