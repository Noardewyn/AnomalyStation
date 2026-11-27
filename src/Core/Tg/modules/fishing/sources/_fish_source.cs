// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/sources/_fish_source.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the autowiki's table (generate_wiki_contents, a live-partial row: tg's wiki generator is not ported): where
// the fish come from. Every fishing spot has a fish source; a source rolls the catch from its weighted table (modified by the
// bait, the hook, the rod and the fish traits), keeps its limited catches with their regeneration, computes the minigame's
// difficulty, hands out the reward, levels out the rarer fish with good bait, lists the catchable fish to a fisher's look, gives
// dead fish to an explosion and takes released fish back. The var blocks are generated (D111).

// tg: code/modules/fishing/sources/_fish_source.dm:1, :11
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/fishing/sources/_fish_source.dm:1  GLOBAL_LIST_INIT_TYPED(preset_fish_sources, /datum/fish_source, init_subtypes_w_path_keys(/datum/fish_source, list()))
    public DmList preset_fish_sources;

    // tg: code/modules/fishing/sources/_fish_source.dm:1  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual async DmTask InitGlobalpreset_fish_sources()
    {
        preset_fish_sources = await init_subtypes_w_path_keys(typeof(datum_fish_source), Dm.list()) as DmList;
        Dm.append(ref gvars_datum_init_order, "preset_fish_sources");
    }

    /**
     * When adding new fishable rewards to a table/counts, you can specify an icon to show in place of the
     * generic fish icon in the minigame UI should the user have the TRAIT_REVEAL_FISH trait, by adding it to
     * this list.
     */
    // tg: code/modules/fishing/sources/_fish_source.dm:11  GLOBAL_LIST_INIT(specific_fish_icons, generate_specific_fish_icons())
    public DmList specific_fish_icons;

    // tg: code/modules/fishing/sources/_fish_source.dm:11  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalspecific_fish_icons()
    {
        specific_fish_icons = generate_specific_fish_icons() as DmList;
        Dm.append(ref gvars_datum_init_order, "specific_fish_icons");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/fishing/sources/_fish_source.dm:13
    public static object generate_specific_fish_icons()
    {
        DmList return_list = zebra_typecacheof(Dm.list(
            Dm.kv(typeof(datum_data_vending_product), FISH_ICON_COIN),
            Dm.kv(typeof(mob_living_basic_axolotl), FISH_ICON_CRITTER),
            Dm.kv(typeof(obj_effect_spawner_random_frog), FISH_ICON_CRITTER),
            Dm.kv(typeof(mob_living_basic_carp), FISH_ICON_DEF),
            Dm.kv(typeof(mob_living_basic_mining), FISH_ICON_HOSTILE),
            Dm.kv(typeof(mob_living_basic_skeleton), FISH_ICON_BONE),
            Dm.kv(typeof(mob_living_basic_stickman), FISH_ICON_HOSTILE),
            Dm.kv(typeof(obj_effect_decal_remains), FISH_ICON_BONE),
            Dm.kv(typeof(obj_effect_mob_spawn_corpse), FISH_ICON_BONE),
            Dm.kv(typeof(obj_effect_spawner_message_in_a_bottle), FISH_ICON_BOTTLE),
            Dm.kv(typeof(obj_item_coin), FISH_ICON_COIN),
            Dm.kv(typeof(obj_item_fish), FISH_ICON_DEF),
            Dm.kv(typeof(obj_item_fish_armorfish), FISH_ICON_CRAB),
            Dm.kv(typeof(obj_item_fish_boned), FISH_ICON_BONE),
            Dm.kv(typeof(obj_item_fish_chainsawfish), FISH_ICON_WEAPON),
            Dm.kv(typeof(obj_item_fish_chasm_crab), FISH_ICON_CRAB),
            Dm.kv(typeof(obj_item_fish_gunner_jellyfish), FISH_ICON_JELLYFISH),
            Dm.kv(typeof(obj_item_fish_holo_crab), FISH_ICON_CRAB),
            Dm.kv(typeof(obj_item_fish_holo_puffer), FISH_ICON_CHUNKY),
            Dm.kv(typeof(obj_item_fish_jumpercable), FISH_ICON_ELECTRIC),
            Dm.kv(typeof(obj_item_fish_lavaloop), FISH_ICON_WEAPON),
            Dm.kv(typeof(obj_item_fish_mastodon), FISH_ICON_BONE),
            Dm.kv(typeof(obj_item_fish_pike_armored), FISH_ICON_WEAPON),
            Dm.kv(typeof(obj_item_fish_pufferfish), FISH_ICON_CHUNKY),
            Dm.kv(typeof(obj_item_fish_sand_crab), FISH_ICON_CRAB),
            Dm.kv(typeof(obj_item_fish_skin_crab), FISH_ICON_CRAB),
            Dm.kv(typeof(obj_item_fish_slimefish), FISH_ICON_SLIME),
            Dm.kv(typeof(obj_item_fish_sludgefish), FISH_ICON_SLIME),
            Dm.kv(typeof(obj_item_fish_starfish), FISH_ICON_STAR),
            Dm.kv(typeof(obj_item_fish_stingray), FISH_ICON_WEAPON),
            Dm.kv(typeof(obj_item_fish_swordfish), FISH_ICON_WEAPON),
            Dm.kv(typeof(obj_item_fish_zipzap), FISH_ICON_ELECTRIC),
            Dm.kv(typeof(obj_item_fishing_rod), FISH_ICON_COIN),
            Dm.kv(typeof(obj_item_instrument_trumpet_spectral), FISH_ICON_BONE),
            Dm.kv(typeof(obj_item_instrument_saxophone_spectral), FISH_ICON_BONE),
            Dm.kv(typeof(obj_item_instrument_trombone_spectral), FISH_ICON_BONE),
            Dm.kv(typeof(obj_item_knife_carp), FISH_ICON_WEAPON),
            Dm.kv(typeof(obj_item_seeds_grass), FISH_ICON_SEED),
            Dm.kv(typeof(obj_item_seeds_random), FISH_ICON_SEED),
            Dm.kv(typeof(obj_item_storage_wallet), FISH_ICON_COIN),
            Dm.kv(typeof(obj_item_stack_sheet_bone), FISH_ICON_BONE),
            Dm.kv(typeof(obj_item_stack_sheet_mineral), FISH_ICON_GEM),
            Dm.kv(typeof(obj_item_stack_ore), FISH_ICON_GEM),
            Dm.kv(typeof(obj_item_survivalcapsule_fishing), FISH_ICON_COIN),
            Dm.kv(typeof(obj_structure_closet_crate), FISH_ICON_COIN),
            Dm.kv(typeof(obj_structure_mystery_box), FISH_ICON_COIN))) as DmList;

        return_list[FISHING_RANDOM_SEED] = FISH_ICON_SEED;
        return_list[FISHING_RANDOM_ORGAN] = FISH_ICON_ORGAN;
        return_list[FISHING_VENDING_CHUCK] = FISH_ICON_COIN;
        return return_list;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/fishing/sources/_fish_source.dm:103  var/static/list/profound_fisher_blacklist
    internal DmList datum_fish_source__profound_fisher_blacklist;
    // tg: code/modules/fishing/sources/_fish_source.dm:109  var/static/weight_result_multiplier
    internal DmList datum_fish_source__weight_result_multiplier;
    // tg: code/modules/fishing/sources/_fish_source.dm:115  var/static/weight_leveling_exponents
    internal DmList datum_fish_source__weight_leveling_exponents;
}

/**
 * Where the fish actually come from - every fishing spot has one assigned but multiple fishing holes
 * can share single source, ie single shared one for ocean/lavaland river
 */
// tg: code/modules/fishing/sources/_fish_source.dm:71
public partial class datum_fish_source : datum
{
    /// Mindless mobs that can fish will never pull up items on this list
    // tg: code/modules/fishing/sources/_fish_source.dm:103
    public static DmList profound_fisher_blacklist => TgState.Current.datum_fish_source__profound_fisher_blacklist ??= typecacheof(Dm.list(
        typeof(mob_living_basic_mining_lobstrosity),
        typeof(obj_structure_closet_crate_necropolis_tendril))) as DmList;

    ///List of multipliers used to make fishes more common compared to everything else depending on bait quality, indexed from best to worst.
    // tg: code/modules/fishing/sources/_fish_source.dm:109
    public static DmList weight_result_multiplier => TgState.Current.datum_fish_source__weight_result_multiplier ??= Dm.list(
        Dm.kv(TRAIT_GREAT_QUALITY_BAIT, 9),
        Dm.kv(TRAIT_GOOD_QUALITY_BAIT, 3.5f),
        Dm.kv(TRAIT_BASIC_QUALITY_BAIT, 2));

    ///List of exponents used to level out the table weight differences between fish depending on bait quality.
    // tg: code/modules/fishing/sources/_fish_source.dm:115
    public static DmList weight_leveling_exponents => TgState.Current.datum_fish_source__weight_leveling_exponents ??= Dm.list(
        Dm.kv(TRAIT_GREAT_QUALITY_BAIT, 0.7f),
        Dm.kv(TRAIT_GOOD_QUALITY_BAIT, 0.55f),
        Dm.kv(TRAIT_BASIC_QUALITY_BAIT, 0.4f));

    // tg: code/modules/fishing/sources/_fish_source.dm:126
    public override async DmTask New(params object[] __extra)
    {
        // PORT(harness): a world without the fishing subsystem (the toy worlds) reads it as not yet initialized.
        if ((SSfishing == null || !Dm.truthy(SSfishing.initialized)) && associated_safe_turfs != null) //This is only needed during world init
        {
            associated_safe_turfs = typecacheof(associated_safe_turfs) as DmList;
            if (safe_turfs_blacklist != null)
                Dm.remove(ref associated_safe_turfs, typecacheof(safe_turfs_blacklist));
        }
        if (!Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(focus_only/fish_sources_tables) is FALSE in production (rules 1.7)
        {
            await DmTask.CompletedTask;
            return;
        }
    }

    // tg: code/modules/fishing/sources/_fish_source.dm:141
    public override object Destroy(float? force = null)
    {
        if (explosive_fishing_score != 0)
            STOP_PROCESSING(SSprocessing, this);
        return base.Destroy(force);
    }

    ///Called when src is set as the fish source of a fishing spot component
    // tg: code/modules/fishing/sources/_fish_source.dm:147
    public virtual void on_fishing_spot_init(datum_component_fishing_spot spot = null)
    {
        return;
    }

    ///Called whenever a fishing spot with this fish source attached is deleted
    // tg: code/modules/fishing/sources/_fish_source.dm:151
    public virtual void on_fishing_spot_del(datum_component_fishing_spot spot = null)
    {
    }

    /// Can we fish in this spot at all. Returns DENIAL_REASON or null if we're good to go
    // tg: code/modules/fishing/sources/_fish_source.dm:154
    public virtual object reason_we_cant_fish(obj_item_fishing_rod rod = null, mob fisherman = null, atom parent = null)
    {
        return rod.reason_we_cant_fish(this);
    }

    /// Called below above proc, in case the fishing source has anything to do that isn't denial
    // tg: code/modules/fishing/sources/_fish_source.dm:158
    public virtual void on_start_fishing(obj_item_fishing_rod rod = null, mob fisherman = null, atom parent = null)
    {
        return;
    }

    /**
     * Calculates the difficulty of the minigame:
     *
     * This includes the source's fishing difficulty, that of the fish, the rod,
     * favorite and disliked baits, fish traits and the fisherman skill.
     *
     * This is the version to call while we are in the fishing minigame phase.
     */
    // tg: code/modules/fishing/sources/_fish_source.dm:169
    [ShouldNotOverride]
    public virtual object calculate_difficulty_minigame(datum_fishing_challenge challenge = null, object reward_path = null, obj_item_fishing_rod rod = null, mob fisherman = null)
    {
        float dot = 0; // begin with a fresh calculation each time
        dot += Dm.num(calculate_difficulty(reward_path, rod, fisherman));

        // Difficulty modifier added by the fisher's skill level
        if (!Dm.truthy(Dm.bitand(challenge.special_effects, FISHING_MINIGAME_RULE_NO_EXP)))
            dot += Dm.num(fisherman.mind?.get_skill_modifier(typeof(datum_skill_fishing), SKILL_VALUE_MODIFIER));

        if (Dm.truthy(Dm.bitand(challenge.special_effects, FISHING_MINIGAME_RULE_KILL)))
            challenge.RegisterSignal(this, COMSIG_FISH_SOURCE_REWARD_DISPENSED, nameof(datum_fishing_challenge.hurt_fish));
        return dot;
    }

    /**
     * Calculates the difficulty of the minigame:
     *
     * This includes the source's fishing difficulty, that of the fish, the rod,
     * favorite and disliked baits, fish traits and the fisherman skill.
     *
     * For non-fish, it's just the source's fishing difficulty minus the fisherman skill.
     */
    // tg: code/modules/fishing/sources/_fish_source.dm:190
    public virtual object calculate_difficulty(object result = null, obj_item_fishing_rod rod = null, mob fisherman = null, params object[] __extra)
    {
        float dot = fishing_difficulty;

        // Difficulty modifier added by having the Settler quirk
        if (HAS_TRAIT(fisherman, TRAIT_EXPERT_FISHER))
            dot += EXPERT_FISHER_DIFFICULTY_MOD;

        // Difficulty modifier added by the rod
        dot += rod.difficulty_modifier;

        bool is_fish_instance = isfish(result);
        if (!ispath(result, typeof(obj_item_fish)) && !is_fish_instance)
        {
            // In the future non-fish rewards can have variable difficulty calculated here
            return dot;
        }

        //Just to clarify when we should use the path instead of the fish, which can be both a path and an instance.
        Type result_path = is_fish_instance ? ((datum)result).type : (Type)result;

        // Baseline fish difficulty
        dot += Dm.num(Dm.initial(result_path, (obj_item_fish p) => p.fishing_difficulty_modifier));

        DmList fish_properties = SSfishing.fish_properties[result_path] as DmList;
        if (rod.bait_value != null)
        {
            obj_item bait = rod.bait_value;
            //Fav bait makes it easier
            DmList fav_bait = fish_properties[FISH_PROPERTIES_FAV_BAIT] as DmList;
            foreach (object bait_identifer in Dm.each(fav_bait))
            {
                if (Dm.truthy(is_matching_bait(bait, bait_identifer)))
                    dot += FAV_BAIT_DIFFICULTY_MOD;
            }
            //Disliked bait makes it harder
            DmList disliked_bait = fish_properties[FISH_PROPERTIES_BAD_BAIT] as DmList;
            foreach (object bait_identifer in Dm.each(disliked_bait))
            {
                if (Dm.truthy(is_matching_bait(bait, bait_identifer)))
                    dot += DISLIKED_BAIT_DIFFICULTY_MOD;
            }
        }

        // Matching/not matching fish traits and equipment
        DmList fish_traits;
        if (is_fish_instance)
            fish_traits = ((obj_item_fish)result).fish_traits;
        else
            fish_traits = fish_properties[FISH_PROPERTIES_TRAITS] as DmList;

        float additive_mod = 0;
        float multiplicative_mod = 1;
        foreach (object fish_trait in Dm.each(fish_traits))
        {
            datum_fish_trait trait = GLOB.fish_traits[fish_trait] as datum_fish_trait;
            DmList mod = trait.difficulty_mod(rod, fisherman) as DmList;
            additive_mod += Dm.num(mod[ADDITIVE_FISHING_MOD]);
            multiplicative_mod *= Dm.num(mod[MULTIPLICATIVE_FISHING_MOD]);
        }

        dot += additive_mod;
        dot *= multiplicative_mod;
        return dot;
    }

    ///Comsig proc from the fishing minigame for 'roll_reward'
    // tg: code/modules/fishing/sources/_fish_source.dm:246
    [SignalHandler, ShouldNotOverride]
    public virtual void roll_reward_minigame(datum source = null, obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null, DmList rewards = null)
    {
        Dm.append(ref rewards, Waiting.NoSleep(roll_reward(rod, fisherman, location)));
    }

    /// Returns a typepath, instance or another special value which we use for dispensing a reward later.
    // tg: code/modules/fishing/sources/_fish_source.dm:253
    public virtual async DmTask<object> roll_reward(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null)
    {
        object picked = pick_weight(await get_modified_fish_table(rod, fisherman, location) as DmList);
        return Dm.truthy(picked) ? picked : FISHING_DUD;
    }

    /// Version of roll_reward() that blacklists objects that shouldn't be caught by ai-controlled mobs.
    // tg: code/modules/fishing/sources/_fish_source.dm:257
    public virtual async DmTask<object> roll_mindless_reward(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null)
    {
        DmList final_table = await get_modified_fish_table(rod, fisherman, location) as DmList;
        Dm.remove(ref final_table, profound_fisher_blacklist);
        object picked = pick_weight(final_table);
        return Dm.truthy(picked) ? picked : FISHING_DUD;
    }

    /**
     * Used to register signals or add traits and the such right after conditions have been cleared
     * and before the minigame starts.
     */
    // tg: code/modules/fishing/sources/_fish_source.dm:266
    public virtual void pre_challenge_started(obj_item_fishing_rod rod = null, mob user = null, datum_fishing_challenge challenge = null)
    {
        return;
    }

    ///Proc called when the challenge is interrupted within the fish source code.
    // tg: code/modules/fishing/sources/_fish_source.dm:270
    public virtual void interrupt_challenge(object reason = null)
    {
        SEND_SIGNAL(COMSIG_FISHING_SOURCE_INTERRUPT_CHALLENGE, Dm.list(this, reason));
    }

    /**
     * Proc called when the COMSIG_MOB_COMPLETE_FISHING signal is sent.
     * Check if we've succeeded. If so, write into memory and dispense the reward.
     */
    // tg: code/modules/fishing/sources/_fish_source.dm:277
    [SignalHandler, ShouldCallParent]
    public virtual object on_challenge_completed(mob user = null, datum_fishing_challenge challenge = null, object success = null)
    {
        UnregisterSignal(user, COMSIG_MOB_COMPLETE_FISHING);
        if (!Dm.truthy(success))
            return null;
        atom_movable reward = Waiting.NoSleep(dispense_reward(challenge.reward_path, user, challenge.location, challenge.used_rod)) as atom_movable;
        challenge.used_rod.SEND_SIGNAL(COMSIG_FISHING_ROD_CAUGHT_FISH, Dm.list(challenge.used_rod, reward, user));
        Waiting.NoSleep(challenge.used_rod.on_reward_caught(reward, user));
        if (reward == null)
            return null;
        user.add_mob_memory(typeof(datum_memory_caught_fish), Dm.named("protagonist", user), Dm.named("deuteragonist", reward.name));
        REMOVE_TRAIT(reward, TRAIT_FISH_JUST_SPAWNED, TRAIT_GENERIC);
        return null;
    }

    /// Gives out the reward if possible
    // tg: code/modules/fishing/sources/_fish_source.dm:292
    public virtual async DmTask<object> dispense_reward(object reward_path = null, mob fisherman = null, atom fishing_spot = null, obj_item_fishing_rod rod = null)
    {
        atom_movable reward = await simple_dispense_reward(reward_path, get_turf(fisherman), fishing_spot) as atom_movable;
        if (reward == null) //balloon alert instead
        {
            fisherman.balloon_alert(fisherman, pick(duds));
            return null;
        }
        if (isitem(reward)) //Try to put it in hand
            Waiting.Detach(fisherman.put_in_hands((obj_item)reward)); // INVOKE_ASYNC(fisherman, TYPE_PROC_REF(/mob, put_in_hands), reward)
        else if (istype(reward, typeof(obj_effect_spawner))) // Do not attempt to forceMove() a spawner. It will break things, and the spawned item should already be at the mob's turf by now.
        {
            fisherman.balloon_alert(fisherman, I18n.T("caught something!"));
            return null;
        }
        fisherman.balloon_alert(fisherman, I18n.T("caught []!", reward));

        DmList fishing_data = null;
        if (isfish(reward))
        {
            ADD_TRAIT(reward, TRAIT_NO_FISHING_ACHIEVEMENT, TRAIT_GENERIC);
            obj_item_fish caught_fish = (obj_item_fish)reward;
            fishing_data = Dm.list(caught_fish.size, caught_fish.weight, caught_fish.custom_materials);
        }
        log_fish(Dm.text("[] has caught a [] at [] using [].", fisherman, reward, fishing_spot, rod), fishing_data);
        return reward;
    }

    ///Simplified version of dispense_reward that doesn't need a fisherman.
    // tg: code/modules/fishing/sources/_fish_source.dm:315
    public virtual async DmTask<object> simple_dispense_reward(object reward_path = null, atom spawn_location = null, atom fishing_spot = null)
    {
        if (reward_path == null)
            return null;
        area area = get_area(fishing_spot) as area;
        if (!Dm.truthy(Dm.bitand(area.area_flags, UNLIMITED_FISHING)) && fish_counts[reward_path] != null) // This is limited count result
        {
            //Somehow, we're trying to spawn an expended reward.
            if (Dm.num(fish_counts[reward_path]) <= 0)
                return null;
            fish_counts[reward_path] = Dm.num(fish_counts[reward_path]) - 1;
            object regen_time = fish_count_regen?[reward_path];
            if (Dm.truthy(regen_time))
            {
                LAZYADDASSOC(ref currently_on_regen, reward_path, 1);
                if (Dm.num(currently_on_regen[reward_path]) == 1)
                    _addtimer(Dm.@new<datum_callback>(this, nameof(regen_count), reward_path), Dm.num(regen_time), file: "code/modules/fishing/sources/_fish_source.dm", line: 328);
            }
        }

        atom_movable reward = await spawn_reward(reward_path, spawn_location, fishing_spot) as atom_movable;
        SEND_SIGNAL(COMSIG_FISH_SOURCE_REWARD_DISPENSED, Dm.list(this, reward));
        return reward;
    }

    // tg: code/modules/fishing/sources/_fish_source.dm:334
    public virtual void regen_count(object reward_path = null)
    {
        if (!Dm.truthy(LAZYACCESS(currently_on_regen, reward_path)))
            return;
        fish_counts[reward_path] = Dm.num(fish_counts[reward_path]) + 1;
        currently_on_regen[reward_path] = Dm.num(currently_on_regen[reward_path]) - 1;
        if (Dm.num(currently_on_regen[reward_path]) <= 0)
        {
            LAZYREMOVE(ref currently_on_regen, reward_path);
            return;
        }
        object regen_time = fish_count_regen[reward_path];
        _addtimer(Dm.@new<datum_callback>(this, nameof(regen_count), reward_path), Dm.num(regen_time), file: "code/modules/fishing/sources/_fish_source.dm", line: 342);
    }

    /// Spawns a reward from a atom path right where the fisherman is. Part of the dispense_reward() logic.
    // tg: code/modules/fishing/sources/_fish_source.dm:344
    public virtual async DmTask<object> spawn_reward(object reward_path = null, atom spawn_location = null, atom fishing_spot = null, params object[] __extra)
    {
        if (Dm.eq(reward_path, FISHING_DUD))
            return null;
        if (ismovable(reward_path))
        {
            atom_movable reward = (atom_movable)reward_path;
            await reward.forceMove(spawn_location);
            return reward;
        }
        if (ispath(reward_path, typeof(datum_chasm_detritus)))
            return await (GLOB.chasm_detritus_types[reward_path] as datum_chasm_detritus).dispense_detritus(spawn_location, fishing_spot);
        if (!ispath(reward_path, typeof(atom_movable)))
            throw Dm.CRASH(Dm.text("Unsupported /datum path [] passed to fish_source/proc/spawn_reward()", reward_path), "code/modules/fishing/sources/_fish_source.dm", 354);
        atom_movable spawned = await Dm.new_async((Type)reward_path, spawn_location) as atom_movable;
        if (isfish(spawned))
        {
            obj_item_fish caught_fish = (obj_item_fish)spawned;
            ADD_TRAIT(caught_fish, TRAIT_FISH_JUST_SPAWNED, TRAIT_GENERIC);
            await caught_fish.randomize_size_and_weight();
        }
        return spawned;
    }

    /// Returns the fish table, with with the unavailable items from fish_counts removed.
    // tg: code/modules/fishing/sources/_fish_source.dm:363
    public virtual object get_fish_table(atom location = null, float? from_explosion = null)
    {
        from_explosion ??= FALSE;
        DmList table = fish_table.Copy();
        //message bottles cannot spawn from explosions. They're meant to be one-time messages (rarely) and photos from past rounds
        //and it would suck if the pool of bottle messages were constantly being emptied by explosive fishing.
        if (Dm.truthy(from_explosion))
            Dm.remove(table, typeof(obj_effect_spawner_message_in_a_bottle));
        foreach (object result in Dm.each(table))
        {
            if (fish_counts[result] != null && Dm.num(fish_counts[result]) <= 0)
                Dm.remove(table, result);
        }
        return table;
    }

    /// Builds a fish weights table modified by bait/rod/user properties
    // tg: code/modules/fishing/sources/_fish_source.dm:375
    public virtual async DmTask<object> get_modified_fish_table(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null)
    {
        obj_item bait = rod.bait_value;
        ///An exponent used to level out the table weight differences between fish depending on bait quality.
        float leveling_exponent = 0;
        ///Multiplier used to make fishes more common compared to everything else.
        float result_multiplier = 1;

        DmList final_table = get_fish_table(location) as DmList;

        if (bait != null)
        {
            foreach (object trait in Dm.each(weight_result_multiplier))
            {
                if (HAS_TRAIT(bait, trait))
                {
                    result_multiplier = Dm.num(weight_result_multiplier[trait]);
                    leveling_exponent = Dm.num(weight_leveling_exponents[trait]);
                    break;
                }
            }
        }

        if (HAS_TRAIT(rod, TRAIT_ROD_REMOVE_FISHING_DUD))
            Dm.remove(final_table, FISHING_DUD);

        if (fisherman.client == null)
            Dm.remove(final_table, typeof(obj_effect_spawner_message_in_a_bottle)); // avoids npc's to get messages in a bottle. Fish for them!

        foreach (object result in Dm.each(final_table))
        {
            final_table[result] = Dm.num(final_table[result]) * Dm.num(rod.hook_value.get_hook_bonus_multiplicative(result));
            final_table[result] = Dm.num(final_table[result]) + Dm.num(rod.hook_value.get_hook_bonus_additive(result)); //Decide on order here so it can be multiplicative

            if (ispath(result, typeof(mob_living)) && bait != null && (HAS_TRAIT(bait, TRAIT_GOOD_QUALITY_BAIT) || HAS_TRAIT(bait, TRAIT_GREAT_QUALITY_BAIT)))
                final_table[result] = round(Dm.num(final_table[result]) * result_multiplier, 1);

            else if (ispath(result, typeof(obj_item_fish)) || isfish(result))
            {
                if (bait != null)
                {
                    final_table[result] = round(Dm.num(final_table[result]) * result_multiplier, 1);
                    float mult = Dm.num(bait.check_bait(result));
                    final_table[result] = round(Dm.num(final_table[result]) * mult, 1);
                    if (mult > 1 && HAS_TRAIT(bait, TRAIT_BAIT_ALLOW_FISHING_DUD))
                        Dm.remove(final_table, FISHING_DUD);
                }
                else
                    final_table[result] = round(Dm.num(final_table[result]) * FISH_WEIGHT_MULT_WITHOUT_BAIT, 1); //Fishing without bait is not going to be easy

                // Apply fish trait modifiers
                final_table[result] = await get_fish_trait_catch_mods(final_table[result], result, rod, fisherman, location);
            }

            if (Dm.num(final_table[result]) <= 0)
                Dm.remove(final_table, result);
        }

        if (leveling_exponent != 0)
            level_out_fish(final_table, leveling_exponent);

        return final_table;
    }

    ///A proc that levels out the weights of various fish, leading to rarer fishes being more common.
    // tg: code/modules/fishing/sources/_fish_source.dm:427
    public virtual void level_out_fish(DmList table = null, object exponent = null)
    {
        float highest_fish_weight = 0;
        DmList collected_fish_weights = Dm.list();
        foreach (object fishable in Dm.each(table))
        {
            if (ispath(fishable, typeof(obj_item_fish)) || isfish(fishable))
            {
                float fish_weight = Dm.num(table[fishable]);
                collected_fish_weights[fishable] = fish_weight;
                if (fish_weight > highest_fish_weight)
                    highest_fish_weight = fish_weight;
            }
        }

        foreach (object fish in Dm.each(collected_fish_weights))
        {
            float difference = highest_fish_weight - Dm.num(collected_fish_weights[fish]);
            if (difference == 0)
                continue;
            table[fish] = Dm.num(table[fish]) + round((float)Math.Pow(difference, Dm.num(exponent)), 1);
        }
    }

    // The fish is a fish or a fish's typepath (the table's keys are either): the slot's parameter is decided as object
    // (translation-rules.md Appendix A).
    // tg: code/modules/fishing/sources/_fish_source.dm:443
    public virtual async DmTask<object> get_fish_trait_catch_mods(object weight = null, object fish = null, obj_item_fishing_rod rod = null, mob user = null, atom location = null)
    {
        object fish_value = fish;
        bool is_fish_instance = isfish(fish_value);
        if (!ispath(fish_value, typeof(obj_item_fish)) && !is_fish_instance)
            return weight;
        float multiplier = 1;
        float total = Dm.num(weight);
        DmList fish_traits;
        if (is_fish_instance)
            fish_traits = ((obj_item_fish)fish_value).fish_traits;
        else
            fish_traits = (SSfishing.fish_properties[fish_value] as DmList)[FISH_PROPERTIES_TRAITS] as DmList;
        foreach (object fish_trait in Dm.each(fish_traits))
        {
            datum_fish_trait trait = GLOB.fish_traits[fish_trait] as datum_fish_trait;
            DmList mod = await trait.catch_weight_mod(rod, user, location, is_fish_instance ? ((datum)fish_value).type : (Type)fish_value) as DmList;
            total += Dm.num(mod[ADDITIVE_FISHING_MOD]);
            multiplier *= Dm.num(mod[MULTIPLICATIVE_FISHING_MOD]);
        }

        return round(total * multiplier, 1);
    }

    ///returns true if this fishing spot has fish that are shown in the catalog.
    // tg: code/modules/fishing/sources/_fish_source.dm:463
    public virtual object has_known_fishes(atom location = null)
    {
        bool show_anyway = Dm.truthy(Dm.bitand(fish_source_flags, FISH_SOURCE_FLAG_IGNORE_HIDDEN_ON_CATALOG));
        foreach (object reward in Dm.each(get_fish_table(location)))
        {
            if (!ispath(reward, typeof(obj_item_fish)) && !isfish(reward))
                continue;
            // tg returns TRUE only without show_anyway (:470), so the aquariums' spots that show every fish never claim one; kept.
            if (!show_anyway && Dm.truthy(Dm.bitand(Dm.initial(reward, (obj_item_fish p) => p.fish_flags), FISH_FLAG_SHOW_IN_CATALOG)))
                return TRUE;
        }
        return FALSE;
    }

    ///Add a string with the names of catchable fishes to the examine text.
    // tg: code/modules/fishing/sources/_fish_source.dm:475
    public virtual async DmTask get_catchable_fish_names(mob user = null, atom location = null, DmList examine_text = null)
    {
        DmList known_fishes = Dm.list();
        bool show_anyway = Dm.truthy(Dm.bitand(fish_source_flags, FISH_SOURCE_FLAG_IGNORE_HIDDEN_ON_CATALOG));

        obj_item_fishing_rod rod = user.get_active_held_item() as obj_item_fishing_rod;
        DmList final_table = null;
        if (rod == null || rod.hook_value == null)
            rod = null;
        else
            final_table = await get_modified_fish_table(rod, user, location) as DmList;

        float total_weight = 0;
        DmList rodless_weights = Dm.list();
        float total_rod_weight = 0;
        DmList rod_weights = Dm.list();
        DmList table = get_fish_table(location) as DmList;
        foreach (object reward in Dm.each(table))
        {
            float weight = Dm.num(table[reward]);
            float final_weight = 0;
            if (rod != null)
            {
                total_weight += weight;
                final_weight = Dm.num(final_table[reward]);
                total_rod_weight += final_weight;
            }
            if (!ispath(reward, typeof(obj_item_fish)) && !isfish(reward))
                continue;
            if (!show_anyway && !Dm.truthy(Dm.bitand(Dm.initial(reward, (obj_item_fish p) => p.fish_flags), FISH_FLAG_SHOW_IN_CATALOG)))
                continue;
            if (rod != null)
            {
                rodless_weights[reward] = weight;
                rod_weights[reward] = final_weight;
            }
            else
                Dm.append(ref known_fishes, Dm.initial(reward, (atom p) => p.name));
        }

        if (rod != null)
        {
            foreach (object reward in Dm.each(rodless_weights))
            {
                float percent_weight = Dm.num(rodless_weights[reward]) / total_weight;
                float percent_rod_weight = Dm.num(rod_weights[reward]) / total_rod_weight;
                string init_name = Dm.initial(reward, (atom p) => p.name);
                float ratio = percent_rod_weight != 0 ? percent_weight / percent_rod_weight : float.PositiveInfinity;
                if (ratio < 0.9f)
                {
                    init_name = span_bold(init_name);
                    if (ratio < 0.3f)
                        init_name = Dm.text("<u>[]</u>", init_name);
                }
                else if (ratio > 1.1f)
                    init_name = span_small(init_name);
                Dm.append(ref known_fishes, init_name);
            }
        }

        if (!Dm.truthy(length(known_fishes)))
            return;

        string info = I18n.T("You can catch the following fish here");

        if (rod != null)
            info = span_tooltip(I18n.T("In bold are fish you're more likely to catch with the current setup. The opposite is true for the smaller font"), info);
        Dm.append(examine_text, span_info(I18n.T("[]: [].", info, english_list(known_fishes))));
    }

    ///How much the explosive_fishing_score impacts explosive fishing. The higher the value, the stronger the malus for repeated calls
    // tg: code/modules/fishing/sources/_fish_source.dm:531  #define EXPLOSIVE_FISHING_MALUS_EXPONENT 0.55
    private const float EXPLOSIVE_FISHING_MALUS_EXPONENT = 0.55f;
    ///How much the explosive_fishing_score is reduced each second.
    // tg: code/modules/fishing/sources/_fish_source.dm:533  #define EXPLOSIVE_FISHING_RECOVERY_RATE 0.18
    private const float EXPLOSIVE_FISHING_RECOVERY_RATE = 0.18f;

    // tg: code/modules/fishing/sources/_fish_source.dm:535
    [SignalHandler]
    public virtual void spawn_reward_from_explosion(atom location = null, object severity = null)
    {
        if (Dm.truthy(Dm.bitand(fish_source_flags, FISH_SOURCE_FLAG_EXPLOSIVE_NONE)))
            return;
        float multiplier = 1;
        if (Dm.truthy(Dm.bitand(fish_source_flags, FISH_SOURCE_FLAG_EXPLOSIVE_MALUS)))
        {
            if (explosive_fishing_score <= 0)
            {
                explosive_fishing_score = 1;
                START_PROCESSING(SSprocessing, this);
            }
            else
            {
                explosive_fishing_score++;
                multiplier = (float)Math.Pow(explosive_fishing_score, -EXPLOSIVE_FISHING_MALUS_EXPONENT);
            }
        }
        float severity_num = Dm.num(severity);
        for (int i = 1; i <= severity_num + 2; i++)
        {
            if (!prob((100 + 100 * severity_num) / i * multiplier))
                continue;
            object reward_loot = pick_weight(get_fish_table(location, from_explosion: TRUE) as DmList);
            atom spawn_location = isturf(location) ? location : location.drop_location() as atom;
            atom_movable reward = Waiting.NoSleep(simple_dispense_reward(reward_loot, spawn_location, location)) as atom_movable;
            if (reward == null)
                continue;
            if (isfish(reward))
            {
                obj_item_fish fish = (obj_item_fish)reward;
                Waiting.NoSleep(fish.set_status(FISH_DEAD, silent: TRUE));
            }
            if (isitem(reward))
            {
                reward.pixel_x = rand(-9, 9);
                reward.pixel_y = rand(-9, 9);
            }
            if (severity_num >= EXPLODE_DEVASTATE)
                reward.ex_act(EXPLODE_LIGHT);
        }
    }

    // tg: code/modules/fishing/sources/_fish_source.dm:563
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        explosive_fishing_score -= EXPLOSIVE_FISHING_RECOVERY_RATE * Dm.num(seconds_per_tick);
        if (explosive_fishing_score <= 0)
        {
            STOP_PROCESSING(SSprocessing, this);
            explosive_fishing_score = 0;
        }
        return null;
    }

    ///Called when releasing a fish in a fishing spot with the TRAIT_CATCH_AND_RELEASE trait.
    // tg: code/modules/fishing/sources/_fish_source.dm:576
    public virtual async DmTask readd_fish(atom location = null, obj_item_fish fish = null, mob_living releaser = null)
    {
        if (releaser != null)
        {
            bool is_morbid = HAS_MIND_TRAIT(releaser, TRAIT_MORBID);
            bool is_naive = HAS_MIND_TRAIT(releaser, TRAIT_NAIVE);
            if (Dm.eq(fish.status, FISH_DEAD)) //ded fish won't repopulate the sea.
            {
                if (is_naive || is_morbid)
                    await releaser.add_mood_event("fish_released", typeof(datum_mood_event_fish_released), is_morbid && !is_naive ? TRUE : FALSE, fish);
            }
            if ((Dm.@in(fish.type, fish_table) != is_morbid) || is_naive)
                await releaser.add_mood_event("fish_released", typeof(datum_mood_event_fish_released), is_morbid && !is_naive ? TRUE : FALSE, fish);
        }
        //don't do anything if the fish is dead, not native to this fish source or has no limited amount.
        if (Dm.eq(fish.status, FISH_DEAD) || fish_table[fish.type] == null || fish_counts[fish.type] == null)
            return;
        //ditto if no restrictions apply
        area area = get_area(location) as area;
        if (Dm.truthy(Dm.bitand(area.area_flags, UNLIMITED_FISHING)))
            return;
        //If this fish population isn't recovering from recent losses, we just increase it.
        if (!Dm.truthy(LAZYACCESS(currently_on_regen, fish.type)))
            fish_counts[fish.type] = Dm.num(fish_counts[fish.type]) + 1;
        else
            regen_count(fish.type);
    }
}
