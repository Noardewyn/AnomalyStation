// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/_fish.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish item, its size and weight with the weight class, force, slowdown, fillets and reagents they scale,
// its hunger, feeding and growth, its health in and out of water and its death, breeding with its partner and evolutions, the
// edible fish with its cooking, petting, the flopping animation, electrogenesis, the export price and the examine. The var block
// is generated (D111).

// tg: code/modules/fishing/fish/_fish.dm:19
[DmPath("/obj/item/fish")]
public partial class obj_item_fish : obj_item
{
    // tg: code/modules/fishing/fish/_fish.dm:1  #define FISH_SAD 0 (file-local)
    private const float FISH_SAD = 0;
    // tg: code/modules/fishing/fish/_fish.dm:2  #define FISH_VERY_HAPPY 4 (file-local)
    private const float FISH_VERY_HAPPY = 4;
    // tg: code/modules/fishing/fish/_fish.dm:6  #define FISH_SUBMERGING_THRESHOLD 100 SECONDS (file-local)
    private const float FISH_SUBMERGING_THRESHOLD = 100 * SECONDS;
    // tg: code/modules/fishing/fish/_fish.dm:7  #define STARVING_FISH_SUBMERGING_THRESHOLD 20 SECONDS (file-local)
    private const float STARVING_FISH_SUBMERGING_THRESHOLD = 20 * SECONDS;

    // tg: code/modules/fishing/fish/_fish.dm:4  #define GET_FISH_ELECTROGENESIS(fish) (fish.electrogenesis_power * fish.size * 0.1) (file-local)
    private static float GET_FISH_ELECTROGENESIS(obj_item_fish fish) => fish.electrogenesis_power * Dm.num(fish.size) * 0.1f;

    // tg: code/modules/fishing/fish/_fish.dm:200
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object apply_qualities = Dm.arg(__extra, 0) ?? TRUE;
        object dot = base.Initialize(mapload, __extra);
        base_icon_state = icon_state;
        //It's important that we register the signals before the component is attached.
        RegisterSignal(this, COMSIG_AQUARIUM_CONTENT_DO_ANIMATION, nameof(update_aquarium_animation));
        RegisterSignal(this, COMSIG_AQUARIUM_CONTENT_RANDOMIZE_POSITION, nameof(randomize_aquarium_position));
        RegisterSignal(this, COMSIG_AQUARIUM_CONTENT_GENERATE_APPEARANCE, nameof(update_aquarium_appearance));
        Waiting.NoSleep(AddComponent(typeof(datum_component_aquarium_content), Dm.list(COMSIG_ATOM_WAS_ATTACKED)));

        RegisterSignal(this, COMSIG_MOVABLE_GET_AQUARIUM_BEAUTY, nameof(get_aquarium_beauty));
        RegisterSignal(this, COMSIG_ATOM_ON_LAZARUS_INJECTOR, nameof(use_lazarus));
        if (Dm.truthy(Dm.bitand(fish_flags, FISH_DO_FLOP_ANIM)))
        {
            RegisterSignal(this, COMSIG_ATOM_TEMPORARY_ANIMATION_START, nameof(on_temp_animation));
            check_flopping();
        }
        if (status != FISH_DEAD)
        {
            ADD_TRAIT(this, TRAIT_UNCOMPOSTABLE, REF(this)); //Compost fish only when it's dead.
            START_PROCESSING(SSobj, this);
        }

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_FISH_STASIS), nameof(enter_stasis));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_FISH_STASIS), nameof(exit_stasis));

        //Adding this because not all fish have the gore foodtype that makes them automatically eligible for dna infusion.
        add_traits(Dm.list(TRAIT_DUCT_TAPE_UNREPAIRABLE, TRAIT_VALID_DNA_INFUSION), INNATE_TRAIT);

        //new fish should be modestly hungry and cannot reproduce right away.
        breeding_wait = world.time + (breeding_timeout * NEW_FISH_BREEDING_TIMEOUT_MULT);
        last_feeding = world.time - (feeding_frequency * NEW_FISH_LAST_FEEDING_MULT);

        if (Dm.truthy(apply_qualities))
        {
            Waiting.NoSleep(apply_traits()); //Make sure traits are applied before size and weight.
            Waiting.NoSleep(update_size_and_weight());
        }

        register_context();
        register_item_context();

        // tg: PERFORM_ALL_TESTS(focus_only/fish_population) is FALSE in production (rules 1.7)
        if (!Dm.truthy(apply_qualities) || !Dm.truthy(FALSE) || type == abstract_type || stable_population > 1)
            return dot;
        if (Dm.truthy(length(compatible_types)))
        {
            foreach (Type fishie in Dm.each_cast<Type>(compatible_types))
            {
                if (Dm.initial(fishie, (obj_item_fish p) => p.stable_population) > 1)
                {
                    if (!HAS_TRAIT(this, TRAIT_FISH_RECESSIVE))
                        stack_trace(Dm.text("[] has a stable population of [] but is compatible with fishes with a higher stable population. However, it doesn't have the [] trait. Either increase its stable population or add the trait to it.", type, stable_population, typeof(datum_fish_trait_recessive)), "code/modules/fishing/fish/_fish.dm", 241);
                    return dot;
                }
            }
        }
        if (!HAS_TRAIT(this, TRAIT_FISH_NO_MATING) && !HAS_TRAIT(this, TRAIT_FISH_CROSSBREEDER))
        {
            DmList pick_one = Dm.list(
                typeof(datum_fish_trait_crossbreeder),
                typeof(datum_fish_trait_no_mating));
            stack_trace(Dm.text("[] has a stable_population of [] but has neither of these traits: []. Either increase its stable population or add one of these traits to it.", type, stable_population, english_list(pick_one)), "code/modules/fishing/fish/_fish.dm", 249);
        }
        return dot;
    }

    // tg: code/modules/fishing/fish/_fish.dm:252
    [ShouldNotOverride]
    public override async DmTask<object> grind_results()
    {
        DmList grind_results = fish_grind_results() as DmList;
        foreach (object reagent_type in Dm.each(grind_results))
            grind_results[reagent_type] = Dm.num(grind_results[reagent_type]) * max(FLOOR(Dm.num(weight) / FISH_GRIND_RESULTS_WEIGHT_DIVISOR, 0.1f), 0.1f);

        await DmTask.CompletedTask;
        return grind_results;
    }

    // tg: code/modules/fishing/fish/_fish.dm:261
    public virtual object fish_grind_results()
    {
        return Dm.list();
    }

    // tg: code/modules/fishing/fish/_fish.dm:266
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (force == 0)
        {
            user.visible_message(span_suicide(I18n.T("[] slaps []self with [], but nothing happens!", user, user.p_them(), this)));
            return SHAME;
        }
        user.visible_message(span_suicide(I18n.T("[] starts rapidly slapping []self with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        user.set_combat_mode(TRUE);
        ADD_TRAIT(user, TRAIT_COMBAT_MODE_LOCK, REF(this));
        await slapperoni(user, iteration: 1);
        REMOVE_TRAIT(user, TRAIT_COMBAT_MODE_LOCK, REF(this));
        if (user.stat == DEAD)
            return MANUAL_SUICIDE;
        user.visible_message(span_suicide(I18n.T("[] slaps []self with [], but fails to go through with it!", user, user.p_them(), this)));
        return SHAME;
    }

    // tg: code/modules/fishing/fish/_fish.dm:280
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (attack_type == OVERWHELMING_ATTACK)
            return FALSE;
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }

    // tg: code/modules/fishing/fish/_fish.dm:285
    public virtual async DmTask slapperoni(mob_living user = null, object iteration = null)
    {
        await stoplag(0.1f * SECONDS);
        user.visible_message(span_bolddanger(suicide_slap_text));
        await user.attackby(this, user);
        if (user.stat > SOFT_CRIT || (Dm.num(iteration) > 100))
        {
            REMOVE_TRAIT(user, TRAIT_COMBAT_MODE_LOCK, REF(this));
            await user.gib(Dm.bitor(Dm.bitor(DROP_ORGANS, DROP_BODYPARTS), DROP_ITEMS));
            return;
        }
        // Kept as tg has it: iteration++ passes the value before the increment, so the count never grows.
        object iteration__post = iteration;
        iteration = Dm.num(iteration) + 1;
        await slapperoni(user, iteration__post);
    }

    // tg: code/modules/fishing/fish/_fish.dm:295
    [DmParams("source", "context", "held_item", "user")]
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (HAS_TRAIT(source, TRAIT_CATCH_AND_RELEASE))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Release";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return NONE;
    }

    // tg: code/modules/fishing/fish/_fish.dm:301
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (this == held_item)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Pet";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (held_item is obj_item_reagent_containers_cup_fish_feed)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Feed";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (held_item is obj_item_fish_analyzer)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Scan";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (held_item is obj_item_clothing_neck_stethoscope)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Check Pulse";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return NONE;
    }

    // The first of tg's two item_interaction definitions on the fish (rules 2.5, __d0). Kept as tg has it: the second
    // definition (:536) replaces it and never calls ..(), so checking a fish's pulse with a stethoscope never runs.
    // tg: code/modules/fishing/fish/_fish.dm:316
    public async DmTask<object> item_interaction__d0(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_clothing_neck_stethoscope)
            return NONE;
        user.balloon_alert_to_viewers(I18n.T("checking pulse"));
        if (!Dm.truthy(await do_after(user, 2.5f * SECONDS, this)))
            return ITEM_INTERACT_FAILURE;
        // Sir... I'm afraid your fish is dying.
        user.visible_message(span_notice(I18n.T("[] checks the pulse of [] with [].", user, this, tool)), span_notice(I18n.T("You check the pulse of [] with [].", this, tool)));
        object warns = await get_health_warnings(user, always_deep: TRUE);
        if (!Dm.truthy(warns))
        {
            to_chat(user, span_notice(I18n.T("[] appears to be perfectly healthy!", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        to_chat(user, warns);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/fish/_fish.dm:331
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!HAS_TRAIT(interacting_with, TRAIT_CATCH_AND_RELEASE))
            return NONE;
        if (HAS_TRAIT(this, TRAIT_NODROP))
        {
            balloon_alert(user, I18n.T("[] stuck to your hand!", p_theyre()));
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, I18n.T("releasing fish..."));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, interacting_with)))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, I18n.T("fish released"));
        string goodbye_text = I18n.T("Bye bye [].", name);
        if (status == FISH_DEAD && !HAS_MIND_TRAIT(user, TRAIT_NAIVE))
            goodbye_text = I18n.T("May [] rest in peace...", p_they());
        user.visible_message(span_notice(I18n.T("[] releases [] into []", user, this, interacting_with)),
            span_notice(I18n.T("You release [] into []. []", this, interacting_with, goodbye_text)),
            span_notice(I18n.T("You hear a splash.")));
        released(interacting_with, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/fish/_fish.dm:350
    public virtual void released(atom location = null, mob_living user = null)
    {
        playsound(location, Dm.resource("sound/effects/splash.ogg"), 50);
        if (Dm.truthy(location._listen_lookup?[COMSIG_FISH_RELEASED_INTO]))
            location._SendSignal(COMSIG_FISH_RELEASED_INTO, Dm.list(location, this, user));
        qdel(this);
    }

    ///Main proc that makes the fish edible.
    // tg: code/modules/fishing/fish/_fish.dm:356
    [DmParams()]
    public virtual async DmTask make_edible(params object[] __extra)
    {
        object foodtypes = get_food_types();
        if (Dm.truthy(Dm.bitand(foodtypes, RAW)))
            // tg: PERFORM_ALL_TESTS(edible_fish) is FALSE in production (rules 1.7)
            await AddComponent(typeof(datum_component_infective), GLOB.floor_diseases.Copy(), Dm.named("weak", TRUE), Dm.named("weak_infection_chance", Dm.truthy(FALSE) ? 100 : 15));
        else
            await AddComponent(typeof(datum_component_germ_sensitive));
        float bites_to_finish = Dm.num(weight) / FISH_WEIGHT_BITE_DIVISOR;
        create_reagents(INFINITY); //We'll set this to the total volume of the reagents right after generate_fish_reagents() is over
        await generate_fish_reagents(bites_to_finish);
        ((datum_reagents)reagents).maximum_volume = round(((datum_reagents)reagents).total_volume * 1.25f); //make some meager space for condiments.
        await AddComponentFrom(
            SOURCE_EDIBLE_INNATE,
            typeof(datum_component_edible),
            Dm.named("food_flags", Dm.bitor(FOOD_NO_EXAMINE, FOOD_NO_BITECOUNT)),
            Dm.named("foodtypes", foodtypes),
            Dm.named("volume", ((datum_reagents)reagents).total_volume),
            Dm.named("eat_time", 1.5f * SECONDS),
            Dm.named("bite_consumption", ((datum_reagents)reagents).total_volume / bites_to_finish),
            Dm.named("after_eat", Dm.@new<datum_callback>(this, nameof(after_eat))),
            Dm.named("check_liked", Dm.@new<datum_callback>(this, nameof(check_liked))),
            Dm.named("reagent_purity", 1));
        RegisterSignals(this, Dm.list(COMSIG_ITEM_FRIED, COMSIG_ITEM_BARBEQUE_GRILLED), nameof(on_fish_cooked));
    }

    ///A proc that returns the food types the edible component has when initialized.
    // tg: code/modules/fishing/fish/_fish.dm:381
    public virtual object get_food_types()
    {
        return Dm.bitor(Dm.bitor(Dm.bitor(SEAFOOD, MEAT), RAW), GORE);
    }

    ///Kill the fish, remove the raw and gore food types, and the infectiveness too if not under-cooked.
    // tg: code/modules/fishing/fish/_fish.dm:385
    [SignalHandler, ShouldNotOverride]
    public virtual void on_fish_cooked(datum source = null, object cooking_time = null)
    {
        Waiting.NoSleep(damage_fish(max_integrity));

        //Remove the blood from the reagents holder and reward the player with some extra nutriment added to the fish.
        datum_reagent_consumable_nutriment_protein protein = ((datum_reagents)reagents).has_reagent(typeof(datum_reagent_consumable_nutriment_protein), check_subtypes: TRUE) as datum_reagent_consumable_nutriment_protein;
        datum_reagent_blood blood = ((datum_reagents)reagents).has_reagent(typeof(datum_reagent_blood)) as datum_reagent_blood;
        float old_blood_volume = blood != null ? Dm.num(blood.volume) : 0; //we can't use the ?. operator since the above proc doesn't return null but 0
        Waiting.NoSleep(((datum_reagents)reagents).del_reagent(typeof(datum_reagent_blood)));

        ///Make space for the additional nutriment
        if (blood != null || protein != null)
        {
            float volume_mult = 1;
            float protein_volume = protein != null ? Dm.num(protein.volume) : 0;
            if (Dm.truthy(bites_amount))
            {
                float initial_bites_left = Dm.num(weight) / FISH_WEIGHT_BITE_DIVISOR;
                float bites_left = initial_bites_left - bites_amount;
                volume_mult = initial_bites_left / bites_left;
            }
            Waiting.NoSleep(adjust_reagents_capacity((protein_volume - old_blood_volume) * volume_mult));
            ///Add the extra nutriment
            if (protein != null)
                Waiting.NoSleep(((datum_reagents)reagents).multiply(2, typeof(datum_reagent_consumable_nutriment_protein)));
        }

        //Remove the raw and gore foodtypes from the edible component
        Waiting.NoSleep(AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("foodtypes", Dm.bitand(get_food_types(), Dm.bitnot(Dm.bitor(RAW, GORE))))));
        if (Dm.num(cooking_time) >= FISH_SAFE_COOKING_DURATION)
            Waiting.NoSleep(well_cooked());

        ///override the signals so they don't mess with blood and proteins again.
        RegisterSignals(this, Dm.list(COMSIG_ITEM_FRIED, COMSIG_ITEM_BARBEQUE_GRILLED), nameof(on_fish_cooked_again), TRUE);
    }

    ///Just kill the fish, again, and perhaps remove the infective comp.
    // tg: code/modules/fishing/fish/_fish.dm:418
    [SignalHandler]
    public virtual void on_fish_cooked_again(datum source = null, object cooking_time = null)
    {
        if (!HAS_TRAIT(this, TRAIT_FISH_SURVIVE_COOKING))
            Waiting.NoSleep(damage_fish(max_integrity));
        if (Dm.num(cooking_time) >= FISH_SAFE_COOKING_DURATION)
            Waiting.NoSleep(well_cooked());
    }

    ///The fish is well cooked. Change how the fish tastes, remove the infective comp and add the relative trait.
    // tg: code/modules/fishing/fish/_fish.dm:426
    public virtual async DmTask well_cooked()
    {
        qdel(GetComponent(typeof(datum_component_infective)));
        await AddComponent(typeof(datum_component_germ_sensitive));
        ADD_TRAIT(this, TRAIT_FISH_WELL_COOKED, INNATE_TRAIT);
        datum_reagent_consumable_nutriment_protein protein = ((datum_reagents)reagents).has_reagent(typeof(datum_reagent_consumable_nutriment_protein), check_subtypes: TRUE) as datum_reagent_consumable_nutriment_protein;
        if (protein != null)
            protein.data = get_fish_taste_cooked();
    }

    ///Checks if the fish is liked or not when eaten by a human.
    // tg: code/modules/fishing/fish/_fish.dm:435
    public virtual async DmTask<object> check_liked(mob_living eater = null)
    {
        if (HAS_TRAIT(eater, TRAIT_PACIFISM) && (status == FISH_ALIVE || HAS_MIND_TRAIT(eater, TRAIT_NAIVE)))
        {
            await eater.add_mood_event("eating_fish", typeof(datum_mood_event_pacifist_eating_fish_item));
            return FOOD_TOXIC;
        }
        if (HAS_TRAIT(eater, TRAIT_AGEUSIA))
            return null;
        if (HAS_TRAIT(eater, TRAIT_FISH_EATER) && !HAS_TRAIT(eater, TRAIT_VEGETARIAN))
            return FOOD_LIKED;
        return null;
    }

    /**
     * Fish is not a reagent holder yet it's edible, so it doen't behave like most other snacks
     * which means it has its own way of handling being bitten, which is defined here.
     */
    // tg: code/modules/fishing/fish/_fish.dm:448
    [ShouldCallParent]
    public virtual async DmTask after_eat(mob_living eater = null, mob_living feeder = null)
    {
        if (!Dm.truthy(((datum_reagents)reagents).total_volume))
            return;
        bites_amount++;
        float bites_to_finish = Dm.num(weight) / FISH_WEIGHT_BITE_DIVISOR;
        await damage_fish((max_integrity / bites_to_finish) * 3);
        await flinch_on_eat(eater, feeder);
    }

    // tg: code/modules/fishing/fish/_fish.dm:457
    public virtual async DmTask flinch_on_eat(mob_living eater = null, mob_living feeder = null)
    {
        if (status == FISH_ALIVE && prob(50) && Dm.truthy(feeder.is_holding(this)) && Dm.truthy(await feeder.dropItemToGround(this)))
        {
            to_chat(feeder, span_warning(I18n.T("[] slips out of your hands in pain!", this)));
            turf target_turf = get_ranged_target_turf(get_turf(this) as atom, pick(GLOB.alldirs), 2) as turf;
            await throw_at(target_turf);
        }
    }

    ///A proc that returns a static reagent holder with a set reagents that you'd get when eating this fish.
    // tg: code/modules/fishing/fish/_fish.dm:464
    [ShouldNotOverride]
    public virtual async DmTask generate_fish_reagents(float? multiplier = null)
    {
        multiplier ??= 1;
        DmList reagents_to_add = get_base_edible_reagents_to_add() as DmList;
        if (Dm.truthy(_listen_lookup?[COMSIG_GENERATE_REAGENTS_TO_ADD]))
            _SendSignal(COMSIG_GENERATE_REAGENTS_TO_ADD, Dm.list(this, reagents_to_add));
        if (multiplier != 1)
        {
            foreach (object reagent in Dm.each(reagents_to_add))
                reagents_to_add[reagent] = Dm.num(reagents_to_add[reagent]) * multiplier.Value;
        }
        await ((datum_reagents)reagents).add_reagent_list(reagents_to_add, added_purity: 1);
        datum_reagent_consumable_nutriment_protein protein = ((datum_reagents)reagents).has_reagent(typeof(datum_reagent_consumable_nutriment_protein), check_subtypes: TRUE) as datum_reagent_consumable_nutriment_protein;
        if (protein != null)
            protein.data = HAS_TRAIT(this, TRAIT_FISH_WELL_COOKED) ? get_fish_taste_cooked() : get_fish_taste();
    }

    // tg: code/modules/fishing/fish/_fish.dm:476
    public virtual object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw fish", 2.5f), Dm.kv("scales", 1));
    }

    // tg: code/modules/fishing/fish/_fish.dm:479
    public virtual object get_fish_taste_cooked()
    {
        return Dm.list(Dm.kv("cooked fish", 2));
    }

    ///The proc that adds in the main reagents this fish has when eaten (without accounting for traits)
    // tg: code/modules/fishing/fish/_fish.dm:483
    public virtual object get_base_edible_reagents_to_add()
    {
        DmList return_list = Dm.list(
            Dm.kv(typeof(datum_reagent_consumable_nutriment_protein), 2),
            Dm.kv(typeof(datum_reagent_blood), 1));
        //It has been at the very least under-cooked.
        if (HAS_TRAIT(this, TRAIT_FOOD_FRIED) || HAS_TRAIT(this, TRAIT_FOOD_BBQ_GRILLED))
        {
            return_list[typeof(datum_reagent_consumable_nutriment_protein)] = Dm.num(return_list[typeof(datum_reagent_consumable_nutriment_protein)]) * 2;
            Dm.remove(ref return_list, typeof(datum_reagent_blood));
        }
        if (required_fluid_type == AQUARIUM_FLUID_SALTWATER)
            return_list[typeof(datum_reagent_consumable_salt)] = 0.4f;
        return return_list;
    }

    ///adjusts the maximum volume of the fish reagents holder and update the amount of food to bite
    // tg: code/modules/fishing/fish/_fish.dm:497
    public virtual async DmTask adjust_reagents_capacity(object amount_to_add = null)
    {
        if (reagents == null)
            return;
        ((datum_reagents)reagents).maximum_volume += Dm.num(amount_to_add);
        float bites_to_finish = Dm.num(weight) / FISH_WEIGHT_BITE_DIVISOR;
        ///updates how many units of reagent one bite takes if edible.
        if (IS_EDIBLE(this))
            await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("bite_consumption", ((datum_reagents)reagents).maximum_volume / bites_to_finish));
    }

    ///Grinding a fish replaces some the protein it has with blood and gibs. You ain't getting a clean smoothie out of it.
    // tg: code/modules/fishing/fish/_fish.dm:507
    public override async DmTask<object> on_grind()
    {
        object dot = await base.on_grind();
        if (reagents == null)
            return dot;
        await ((datum_reagents)reagents).convert_reagent(typeof(datum_reagent_consumable_nutriment_protein), typeof(datum_reagent_consumable_liquidgibs), multiplier: 0.4f, include_source_subtypes: TRUE);
        await ((datum_reagents)reagents).convert_reagent(typeof(datum_reagent_consumable_nutriment_protein), typeof(datum_reagent_blood), multiplier: 0.2f, include_source_subtypes: TRUE);
        return dot;
    }

    ///When processed, the reagents inside this fish will be passed to the created atoms.
    // tg: code/modules/fishing/fish/_fish.dm:515
    public override async DmTask UsedforProcessing(mob_living user = null, obj_item used_item = null, DmList chosen_option = null, DmList created_atoms = null)
    {
        float created_len = length(created_atoms);
        foreach (atom_movable created in Dm.each<atom_movable>(created_atoms))
        {
            if (created.reagents == null)
                continue;
            foreach (datum_reagent reagent in Dm.each<datum_reagent>(((datum_reagents)reagents).reagent_list))
            {
                float transfer_vol = Dm.num(reagent.volume) / created_len;
                datum_reagent result_reagent = ((datum_reagents)created.reagents).has_reagent(reagent.type) as datum_reagent;
                if (result_reagent == null)
                {
                    await ((datum_reagents)created.reagents).add_reagent(reagent.type, transfer_vol, await ((datum_reagents)reagents).copy_data(reagent) as DmList, ((datum_reagents)reagents).chem_temp, reagent.purity, reagent.ph, no_react: TRUE);
                    continue;
                }
                await ((datum_reagents)created.reagents).multiply(transfer_vol / Dm.num(result_reagent.volume), reagent.type);
            }
        }
        await base.UsedforProcessing(user, used_item, chosen_option, created_atoms);
    }

    // tg: code/modules/fishing/fish/_fish.dm:529
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if ((status == FISH_DEAD || HAS_TRAIT(this, TRAIT_FISH_STASIS)) && Dm.truthy(icon_state_dead))
            icon_state = Dm.str(icon_state_dead);
        else
            icon_state = Dm.str(base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // The second item_interaction definition, the one that runs (see item_interaction__d0).
    // tg: code/modules/fishing/fish/_fish.dm:536
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_reagent_containers_cup_fish_feed)
            return NONE;

        if (!Dm.truthy(((datum_reagents)tool.reagents).total_volume))
        {
            balloon_alert(user, I18n.T("[] is empty!", tool.name));
            return ITEM_INTERACT_BLOCKING;
        }

        if (status == FISH_DEAD)
        {
            balloon_alert(user, I18n.T("[] []", name, HAS_MIND_TRAIT(user, TRAIT_NAIVE) ? I18n.T("isn't hungry") : I18n.T("is dead!")));
            return ITEM_INTERACT_BLOCKING;
        }

        await feed(tool.reagents as datum_reagents);
        balloon_alert(user, I18n.T("fed []", name));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/fish/_fish.dm:552
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(catcher_name) && Dm.truthy(catch_date))
            Dm.append(ref dot, span_boldnicegreen(I18n.T("Caught by [] on [].", catcher_name, catch_date)));

        if (HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISH) || HAS_TRAIT(loc, TRAIT_EXAMINE_FISH))
        {
            Dm.append(ref dot, span_notice(I18n.T("[] [] cm long.", p_Theyre(), size)));
            Dm.append(ref dot, span_notice(I18n.T("[] weigh[] [] [].", p_They(), p_s(), weight, span_tooltip(I18n.T("the standard unit of measurement for space age fish"), I18n.T("kiloclam")))));

            if (HAS_TRAIT(this, TRAIT_FISH_GENEGUNNED))
                Dm.append(ref dot, span_warning(I18n.T("[] been edited by a fish genegun. []'ll die if edited again.", p_Theyve(), p_They())));
        }

        Dm.append(ref dot, await get_health_warnings(user, always_deep: FALSE));

        if (HAS_TRAIT(this, TRAIT_FISHING_BAIT))
            Dm.append(ref dot, span_smallnoticeital(I18n.T("[] can be used as a fishing bait.", p_They())));

        if (Dm.truthy(bites_amount))
            Dm.append(ref dot, span_warning(I18n.T("[] been bitten by someone.", p_Theyve())));
        return dot;
    }

    // tg: code/modules/fishing/fish/_fish.dm:572
    public virtual async DmTask<object> get_health_warnings(mob user = null, float? always_deep = null)
    {
        always_deep ??= FALSE;
        object dot = null;
        if (!HAS_MIND_TRAIT(user, TRAIT_EXAMINE_DEEPER_FISH) && !Dm.truthy(always_deep))
            return dot;
        if (status == FISH_DEAD)
            return span_deadsay(I18n.T("[] [].", p_Theyre(), HAS_MIND_TRAIT(user, TRAIT_NAIVE) ? I18n.T("taking the big snooze") : I18n.T("dead")));

        DmList warnings = Dm.list();
        if (Dm.truthy(get_starvation_mult()))
            Dm.append(ref warnings, I18n.T("starving"));
        if (!HAS_TRAIT(this, TRAIT_FISH_STASIS) && !Dm.truthy(await proper_environment()))
            Dm.append(ref warnings, I18n.T("drowning"));

        float health_ratio = Dm.num(get_health_percentage());
        if (health_ratio >= 0 && health_ratio <= 0.25f)
            Dm.append(ref warnings, I18n.T("dying"));
        else if (health_ratio >= 0.25f && health_ratio <= 0.5f)
            Dm.append(ref warnings, I18n.T("very unhealthy"));
        else if (health_ratio >= 0.5f && health_ratio <= 0.75f)
            Dm.append(ref warnings, I18n.T("unhealthy"));
        else if (health_ratio >= 0.75f && health_ratio <= 0.9f)
            Dm.append(ref warnings, I18n.T("mostly healthy"));

        if (Dm.truthy(length(warnings)))
            Dm.append(ref dot, span_warning(I18n.T("[] [].", p_Theyre(), english_list(warnings))));

        return dot;
    }

    /**
     * This proc takes a base size, base weight and deviation arguments to generate new size and weight through a gaussian distribution (bell curve)
     * Mainly used to determinate the size and weight of caught fish.
     */
    // tg: code/modules/fishing/fish/_fish.dm:604
    public virtual async DmTask randomize_size_and_weight(object base_size = null, object base_weight = null, object deviation = null, float? update = null)
    {
        base_size ??= average_size;
        base_weight ??= average_weight;
        deviation ??= weight_size_deviation;
        update ??= TRUE;
        float size_deviation = 0.2f * Dm.num(base_size);
        temp_size = round(clamp(gaussian(Dm.num(base_size), size_deviation), average_size * 1 / MAX_FISH_DEVIATION_COEFF, average_size * MAX_FISH_DEVIATION_COEFF));

        float weight_deviation = 0.2f * Dm.num(base_weight);
        temp_weight = round(clamp(gaussian(Dm.num(base_weight), weight_deviation), average_weight * 1 / MAX_FISH_DEVIATION_COEFF, average_weight * MAX_FISH_DEVIATION_COEFF));

        set_max_size_and_weight(temp_size, temp_weight);
        if (Dm.truthy(update))
            await update_size_and_weight(temp_size, temp_weight);
    }

    ///Set the maximum size and weight a fish can reach from base size and weight args if they have't been set already.
    // tg: code/modules/fishing/fish/_fish.dm:616
    public virtual void set_max_size_and_weight(object base_size = null, object base_weight = null)
    {
        if (!Dm.truthy(maximum_size))
            maximum_size = min(Dm.num(base_size) * 2, average_size * MAX_FISH_DEVIATION_COEFF);
        if (!Dm.truthy(maximum_weight))
            maximum_weight = min(Dm.num(base_weight) * 2, average_weight * MAX_FISH_DEVIATION_COEFF);
    }

    ///Updates weight and size, along with weight class, number of fillets you can get and grind results.
    // tg: code/modules/fishing/fish/_fish.dm:623
    public virtual async DmTask update_size_and_weight(object new_size = null, object new_weight = null, float? update_materials = null)
    {
        new_size ??= average_size;
        new_weight ??= average_weight;
        update_materials ??= TRUE;
        Dm.combine(ref fish_flags, FISH_FLAG_UPDATING_SIZE_AND_WEIGHT);
        if (Dm.truthy(_listen_lookup?[COMSIG_FISH_UPDATE_SIZE_AND_WEIGHT]))
            _SendSignal(COMSIG_FISH_UPDATE_SIZE_AND_WEIGHT, Dm.list(this, new_size, new_weight));

        bool is_mount = loc is obj_structure_fish_mount; //used to prevent fish from getting butchered inside mounts

        if (Dm.truthy(size))
        {
            if (!is_mount)
                remove_fillet_type();
            if (Dm.num(size) > FISH_SIZE_TWO_HANDS_REQUIRED)
                qdel(GetComponent(typeof(datum_component_two_handed)));
        }
        else
            set_max_size_and_weight(new_size, new_weight);

        size = new_size;

        object init_icon_state = Dm.initial(this, (obj_item p) => p.inhand_icon_state);
        float size__n = Dm.num(size);
        if (size__n >= 0 && size__n <= FISH_SIZE_TINY_MAX)
        {
            update_weight_class(WEIGHT_CLASS_TINY);
            if (!Dm.truthy(init_icon_state))
                inhand_icon_state = "fish_small";
        }
        else if (size__n >= FISH_SIZE_TINY_MAX && size__n <= FISH_SIZE_SMALL_MAX)
        {
            if (!Dm.truthy(init_icon_state))
                inhand_icon_state = "fish_small";
            update_weight_class(WEIGHT_CLASS_SMALL);
        }
        else if (size__n >= FISH_SIZE_SMALL_MAX && size__n <= FISH_SIZE_NORMAL_MAX)
        {
            if (!Dm.truthy(init_icon_state))
                inhand_icon_state = "fish_normal";
            update_weight_class(WEIGHT_CLASS_NORMAL);
        }
        else if (size__n >= FISH_SIZE_NORMAL_MAX && size__n <= FISH_SIZE_BULKY_MAX)
        {
            if (!Dm.truthy(init_icon_state))
                inhand_icon_state = "fish_bulky";
            update_weight_class(WEIGHT_CLASS_BULKY);
        }
        else if (size__n >= FISH_SIZE_BULKY_MAX && size__n <= FISH_SIZE_HUGE_MAX)
        {
            if (!Dm.truthy(init_icon_state))
                inhand_icon_state = "fish_huge";
            update_weight_class(WEIGHT_CLASS_HUGE);
        }
        else if (size__n >= FISH_SIZE_HUGE_MAX && size__n <= INFINITY)
        {
            if (!Dm.truthy(init_icon_state))
                inhand_icon_state = "fish_huge";
            update_weight_class(WEIGHT_CLASS_GIGANTIC);
        }

        if (Dm.num(size) > FISH_SIZE_TWO_HANDS_REQUIRED || (HAS_TRAIT(this, TRAIT_FISH_SHOULD_TWOHANDED) && w_class >= WEIGHT_CLASS_BULKY))
        {
            inhand_icon_state = Dm.text("[]_wielded", inhand_icon_state);
            await AddComponent(typeof(datum_component_two_handed), Dm.named("require_twohands", TRUE));
        }

        if (!is_mount)
            await add_fillet_type();

        bool make_edible = !Dm.truthy(weight);
        if (Dm.truthy(weight))
        {
            if (reagents != null) //This fish has reagents. Adjust the maximum volume of the reagent holder and do some math to adjut the reagents too.
            {
                float new_weight_ratio = Dm.num(new_weight) / Dm.num(weight);
                float volume_diff = ((datum_reagents)reagents).maximum_volume * new_weight_ratio - ((datum_reagents)reagents).maximum_volume;
                // Kept as tg has it: the ratio is compared with the weight, not with 1.
                if (new_weight_ratio > Dm.num(weight))
                {
                    await adjust_reagents_capacity(volume_diff);
                    ///As always, we want to maintain proportions here, so we need to get the ratio of bites left and initial bites left.
                    float weight_diff = Dm.num(new_weight) - Dm.num(weight);
                    float multiplier = weight_diff / FISH_WEIGHT_BITE_DIVISOR;
                    float initial_bites_left = Dm.num(weight) / FISH_WEIGHT_BITE_DIVISOR;
                    float bites_left = initial_bites_left - bites_amount;
                    float amount_to_gen = bites_left / initial_bites_left * multiplier;
                    await generate_fish_reagents(amount_to_gen);
                }
                else
                {
                    await ((datum_reagents)reagents).multiply(new_weight_ratio);
                    await adjust_reagents_capacity(volume_diff);
                }
            }
        }

        weight = new_weight;

        if (make_edible)
            await this.make_edible();

        if (Dm.num(weight) >= FISH_WEIGHT_SLOWDOWN && !HAS_TRAIT(this, TRAIT_SPEED_POTIONED))
        {
            slowdown = GET_FISH_SLOWDOWN(weight);
            drag_slowdown = round(slowdown * 0.5f, 1);
        }
        else
        {
            slowdown = 0;
            drag_slowdown = 0;
        }
        if (ismob(loc))
        {
            mob mob = loc as mob;
            await mob.update_equipment_speed_mods();
        }

        float mats_len = length(custom_materials);
        if (Dm.truthy(update_materials) && Dm.truthy(mats_len))
        {
            DmList new_mats_list = custom_materials.Copy();
            float multiplier = 1 / mats_len;
            float unmodified_weight = Dm.num(weight);
            foreach (object mat_type in Dm.each(custom_materials))
            {
                datum_material material = await SSmaterials.get_material(mat_type) as datum_material;
                unmodified_weight /= GET_MATERIAL_MODIFIER(1 + (Dm.num(material.get_property(MATERIAL_DENSITY)) - 4) * 0.1f, multiplier);
            }
            multiplier = unmodified_weight / Dm.num(weight);
            foreach (object mat_type in Dm.each(new_mats_list))
                new_mats_list[mat_type] = Dm.num(new_mats_list[mat_type]) * multiplier;
            await set_custom_materials(new_mats_list); // apply_material_effects() will call update_fish_force for us
        }
        await update_fish_force();

        Dm.mask(ref fish_flags, Dm.bitnot(FISH_FLAG_UPDATING_SIZE_AND_WEIGHT));
    }

    // tg: code/modules/fishing/fish/_fish.dm:722
    public virtual void remove_fillet_type()
    {
        if (fillet_type == null)
            return;
        float amount = max(round(num_fillets * Dm.num(size) / FISH_FILLET_NUMBER_SIZE_DIVISOR, 1), 1);
        // tg: PERFORM_ALL_TESTS(fish_size_weight) is FALSE in production (rules 1.7)
        float time = Dm.truthy(FALSE) ? 0 : 0.5f * SECONDS * amount;
        RemoveElement(typeof(datum_element_processable), TOOL_KNIFE, fillet_type, amount, time, Dm.named("screentip_verb", "Cut"));
    }

    // tg: code/modules/fishing/fish/_fish.dm:729
    public virtual async DmTask<object> add_fillet_type()
    {
        if (fillet_type == null)
            return null;
        float amount = max(round(num_fillets * Dm.num(size) / FISH_FILLET_NUMBER_SIZE_DIVISOR, 1), 1);
        // tg: PERFORM_ALL_TESTS(fish_size_weight) is FALSE in production (rules 1.7)
        float time = Dm.truthy(FALSE) ? 0 : 0.5f * SECONDS * amount;
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, fillet_type, amount, time, Dm.named("screentip_verb", "Cut"));
        return amount; //checked by a unit test
    }

    ///Reset weapon-related variables of this items and recalculates those values based on the fish weight and size.
    // tg: code/modules/fishing/fish/_fish.dm:738
    public virtual async DmTask update_fish_force()
    {
        if (force >= 15 && Dm.eq(hitsound, SFX_ALT_FISH_SLAP))
            hitsound = SFX_DEFAULT_FISH_SLAP;
        force = Dm.initial(this, (obj p) => p.force);
        throwforce = Dm.initial(this, (atom_movable p) => p.throwforce);
        throw_range = Dm.initial(this, (atom_movable p) => p.throw_range);
        demolition_mod = Dm.initial(this, (obj p) => p.demolition_mod);
        attack_verb_continuous = Dm.initial(this, (obj_item p) => p.attack_verb_continuous);
        attack_verb_simple = Dm.initial(this, (obj_item p) => p.attack_verb_simple);
        hitsound = Dm.initial(this, (obj_item p) => p.hitsound);
        damtype = Dm.initial(this, (obj p) => p.damtype);
        attack_speed = Dm.initial(this, (obj_item p) => p.attack_speed);
        block_chance = Dm.initial(this, (obj_item p) => p.block_chance);
        armour_penetration = Dm.initial(this, (obj_item p) => p.armour_penetration);
        wound_bonus = Dm.initial(this, (obj p) => p.wound_bonus);
        exposed_wound_bonus = Dm.initial(this, (obj p) => p.exposed_wound_bonus);
        toolspeed = Dm.initial(this, (obj_item p) => p.toolspeed);

        float weight_rank = GET_FISH_WEIGHT_RANK(weight);

        throw_range -= weight_rank;
        get_force_rank();

        float bonus_malus = weight_rank - w_class;
        if (Dm.truthy(bonus_malus))
            calculate_fish_force_bonus(bonus_malus);

        throwforce = force;

        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_EFFECTS)) && Dm.truthy(length(custom_materials))) //struck by metal gen or something.
        {
            if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_AFFECT_STATISTICS)))
            {
                float index = 1;
                foreach (object current_material in Dm.each(custom_materials))
                {
                    datum_material material = await SSmaterials.get_material(current_material) as datum_material;
                    // Kept as tg has it: custom_materials[material] is keyed by the material's type, so the datum reads null.
                    change_material_strength(material, custom_materials[material], get_material_multiplier(material, custom_materials, index));
                    index += 1;
                }
            }

            datum_material master = await get_master_material() as datum_material;
            if (Dm.truthy(master?.item_sound_override))
            {
                hitsound = master.item_sound_override;
                usesound = master.item_sound_override;
                mob_throw_hit_sound = master.item_sound_override;
                equip_sound = master.item_sound_override;
                pickup_sound = master.item_sound_override;
                drop_sound = master.item_sound_override;
            }
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_FISH_FORCE_UPDATED]))
            _SendSignal(COMSIG_FISH_FORCE_UPDATED, Dm.list(this, weight_rank, bonus_malus));
        if (force >= 15 && Dm.eq(hitsound, SFX_DEFAULT_FISH_SLAP)) // don't override special attack sounds
            hitsound = SFX_ALT_FISH_SLAP; // do more damage - do heavier slap sound
    }

    ///A proc that makes the fish slightly stronger or weaker if there's a noticeable discrepancy between size and weight.
    // tg: code/modules/fishing/fish/_fish.dm:789
    public virtual void calculate_fish_force_bonus(object bonus_malus = null)
    {
        demolition_mod += Dm.num(bonus_malus) * 0.1f;
        attack_speed += Dm.num(bonus_malus) * 0.1f;
        force = round(force * (1 + Dm.num(bonus_malus) * 0.1f), 0.1f);
    }

    // tg: code/modules/fishing/fish/_fish.dm:794
    public virtual void get_force_rank()
    {
        switch (w_class)
        {
            case WEIGHT_CLASS_TINY:
                force -= 3;
                attack_speed -= 0.1f * SECONDS;
                break;
            case WEIGHT_CLASS_NORMAL:
                force += 2;
                break;
            case WEIGHT_CLASS_BULKY:
                force += 5;
                attack_speed += 0.1f * SECONDS;
                break;
            case WEIGHT_CLASS_HUGE:
                force += 9;
                attack_speed += 0.2f * SECONDS;
                demolition_mod += 0.2f;
                break;
            case WEIGHT_CLASS_GIGANTIC:
                force += 13;
                attack_speed += 0.4f * SECONDS;
                demolition_mod += 0.4f;
                break;
        }
    }

    // tg: code/modules/fishing/fish/_fish.dm:813
    public override async DmTask apply_single_mat_effect(datum_material material = null, object amount = null, object multiplier = null)
    {
        await base.apply_single_mat_effect(material, amount, multiplier);
        //The materials are being increased/decreased along with the weight.
        if (!Dm.truthy(Dm.bitand(fish_flags, FISH_FLAG_UPDATING_SIZE_AND_WEIGHT)))
            material_weight_mult *= GET_MATERIAL_MODIFIER(1 + (Dm.num(material.get_property(MATERIAL_DENSITY)) - 4) * 0.1f, multiplier);
    }

    // tg: code/modules/fishing/fish/_fish.dm:819
    public override async DmTask apply_material_effects(DmList materials = null)
    {
        await base.apply_material_effects(materials);
        //Either effects aren't applied of he materials are simply being increased/decreased along with the weight. Avoids recursion.
        if (!Dm.truthy(Dm.bitand(material_flags, MATERIAL_EFFECTS)) || Dm.truthy(Dm.bitand(fish_flags, FISH_FLAG_UPDATING_SIZE_AND_WEIGHT)))
            return;
        maximum_weight = Dm.num(maximum_weight) * material_weight_mult;
        object weight__base = Dm.truthy(temp_weight) ? temp_weight : weight;
        await update_size_and_weight(size, Dm.num(weight__base) * material_weight_mult, update_materials: FALSE);
    }

    // tg: code/modules/fishing/fish/_fish.dm:827
    public override async DmTask remove_material_effects(float? replace_mats = null)
    {
        replace_mats ??= TRUE;
        await base.remove_material_effects(replace_mats);
        if (Dm.truthy(replace_mats) || !Dm.truthy(Dm.bitand(material_flags, MATERIAL_EFFECTS)))
            return;
        maximum_weight = Dm.num(maximum_weight) / material_weight_mult;
        await update_size_and_weight(size, Dm.num(weight) / material_weight_mult);
        material_weight_mult = 1;
    }

    /**
     * This proc has fish_traits list populated with fish_traits paths from three different lists:
     * traits from x_traits and y_traits are compared, and inserted if conditions are met;
     * traits from fixed_traits are inserted unconditionally.
     * traits from removed_traits will be removed from the for loop.
     *
     * This proc should only be called if the fish was spawned with the apply_qualities arg set to FALSE
     * and hasn't had inherited traits already.
     */
    // tg: code/modules/fishing/fish/_fish.dm:844
    public virtual async DmTask inherit_traits(DmList x_traits = null, DmList y_traits = null, DmList fixed_traits = null, DmList removed_traits = null)
    {
        fish_traits = fixed_traits?.Copy() is DmList or__1 && Dm.truthy(or__1) ? or__1 : Dm.list();

        DmList same_traits = Dm.bitand(x_traits, y_traits) as DmList;
        DmList all_traits = Dm.sub(y_traits != null ? Dm.bitor(x_traits, y_traits) : x_traits, removed_traits) as DmList;

        /// a list of incompatible traits that'll be filled as it goes on. Don't let any such trait pass onto the fish.
        DmList incompatible_traits = Dm.list();

        ///some traits can spontaneously manifest for some fishes. These have higher priorities than other traits
        DmList potential_spontaneous_traits = GLOB.spontaneous_fish_traits[type] as DmList;
        foreach (object trait_type in Dm.each(potential_spontaneous_traits))
        {
            if (!prob(Dm.num(potential_spontaneous_traits[trait_type])))
                continue;
            datum_fish_trait trait = GLOB.fish_traits[trait_type] as datum_fish_trait;
            if (Dm.truthy(length(Dm.bitand(fish_traits, trait.incompatible_traits))))
                continue;
            Dm.combine(ref fish_traits, trait_type);
            Dm.combine(ref incompatible_traits, trait.incompatible_traits);
        }

        foreach (object trait_type in Dm.each(fish_traits))
        {
            datum_fish_trait trait = GLOB.fish_traits[trait_type] as datum_fish_trait;
            Dm.combine(ref incompatible_traits, trait.incompatible_traits);
        }
        /**
         * shuffle the traits, so, in the case of incompatible traits, we don't have to choose which to discard.
         * Instead we let the random numbers do it for us in a first come, first served basis.
         */
        foreach (object trait_type in Dm.each(shuffle(all_traits)))
        {
            if (Dm.@in(trait_type, fish_traits))
                continue; //likely a fixed trait
            if (Dm.@in(trait_type, incompatible_traits))
                continue;
            datum_fish_trait trait = GLOB.fish_traits[trait_type] as datum_fish_trait;
            if (trait == null)
            {
                stack_trace(Dm.text("Couldn't find trait [] in the global fish traits list", Dm.truthy(trait_type) ? trait_type : "null"), "code/modules/fishing/fish/_fish.dm", 879);
                continue;
            }
            if (trait.fish_whitelist != null && !Dm.@in(type, trait.fish_whitelist))
                continue;
            if (Dm.truthy(length(Dm.bitand(fish_traits, trait.incompatible_traits))))
                continue;
            // If there's no partner, we've been reated through parthenogenesis or growth, therefore, traits are copied
            // Otherwise, we check if both have the trait or perform a probability check.
            if (y_traits == null || Dm.@in(trait_type, same_traits) || prob(trait.inheritability))
            {
                Dm.combine(ref fish_traits, trait_type);
                Dm.combine(ref incompatible_traits, trait.incompatible_traits);
            }
        }

        await apply_traits();
    }

    // tg: code/modules/fishing/fish/_fish.dm:893
    public virtual async DmTask apply_traits()
    {
        foreach (object fish_trait_type in Dm.each(fish_traits))
        {
            datum_fish_trait trait = GLOB.fish_traits[fish_trait_type] as datum_fish_trait;
            await trait.apply_to_fish(this);
        }
    }

    // tg: code/modules/fishing/fish/_fish.dm:898
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        check_flopping();
        return dot;
    }

    /// Stop processing once the stasis trait is added
    // tg: code/modules/fishing/fish/_fish.dm:903
    [SignalHandler]
    public virtual void enter_stasis(datum source = null)
    {
        stop_flopping();
        update_appearance();
        STOP_PROCESSING(SSobj, this);
    }

    /// Start processing again when the stasis trait is removed
    // tg: code/modules/fishing/fish/_fish.dm:910
    [SignalHandler]
    public virtual void exit_stasis(datum source = null)
    {
        if (status == FISH_DEAD)
            return;
        START_PROCESSING(SSobj, this);
        check_flopping();
    }

    ///Returns the value for hunger ranging from 0 to the cap (by default 1)
    // tg: code/modules/fishing/fish/_fish.dm:918
    public virtual object get_hunger(float? cap = null)
    {
        cap ??= FISH_STARVING_THRESHOLD;
        float dot = clamp((world.time - Dm.num(last_feeding)) / feeding_frequency, 0, cap.Value);
        if (HAS_TRAIT(this, TRAIT_FISH_NO_HUNGER))
            return min(dot, FISH_STARVING_THRESHOLD * 0.2f);
        return dot;
    }

    // tg: code/modules/fishing/fish/_fish.dm:923
    public virtual object get_starvation_mult()
    {
        float hunger = Dm.num(get_hunger(cap: FISH_STARVING_THRESHOLD * 2));
        return hunger >= FISH_STARVING_THRESHOLD ? hunger : 0;
    }

    ///Feed the fishes with the contents of the fish feed
    // tg: code/modules/fishing/fish/_fish.dm:928
    public virtual async DmTask feed(datum_reagents fed_reagents = null)
    {
        if (status != FISH_ALIVE)
            return;

        ///If one of the reagent with fish effects is also our food reagent this is set to TRUE
        float already_fed = FALSE;
        object was_hungry = get_hunger();
        foreach (datum_reagent reagent in Dm.each<datum_reagent>(fed_reagents.reagent_list))
        {
            if (!Dm.truthy(fed_reagents.has_reagent(reagent.type, 0.1f)) || !Dm.truthy(await reagent.used_on_fish(this)))
                continue;
            await fed_reagents.remove_reagent(reagent.type, 0.1f);
            if (reagent.type == food)
                already_fed = TRUE;
        }

        if (Dm.truthy(was_hungry) && !Dm.truthy(get_hunger())) //one of the other reagents already sated its hunger.
            return;

        if (Dm.truthy(already_fed))
        {
            await sate_hunger();
            return;
        }

        if (Dm.truthy(await fed_reagents.remove_reagent(food, 0.1f)))
        {
            await sate_hunger();
            return;
        }

        datum_reagent wrong_reagent = pick(fed_reagents.reagent_list) as datum_reagent;
        if (wrong_reagent == null)
            return;
        await fed_reagents.remove_reagent(wrong_reagent.type, 0.1f);
    }

    ///Proc that should be called when the fish is fed. By default, it grows the fish depending on various variables.
    // tg: code/modules/fishing/fish/_fish.dm:959
    public virtual async DmTask sate_hunger()
    {
        if (HAS_TRAIT(loc, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH))
        {
            last_feeding = world.time;
            return;
        }
        float hunger = Dm.num(get_hunger());
        last_feeding = world.time;
        if (hunger < 0.05f) //don't bother growing for very small amounts.
            return;

        float new_size = Dm.num(size);
        float new_weight = Dm.num(weight);
        float hunger_mult;
        if (hunger <= FISH_GROWTH_PEAK)
            hunger_mult = hunger / FISH_GROWTH_PEAK;
        else
        {
            hunger_mult = 1 - (hunger - FISH_GROWTH_PEAK) * 4;
            if (hunger_mult <= 0)
                return;
        }
        float base_mult = FISH_GROWTH_MULT;
        if (HAS_TRAIT(this, TRAIT_FISH_QUICK_GROWTH))
            base_mult *= 2.5f;
        if (Dm.num(size) < Dm.num(maximum_size))
        {
            new_size += CEILING((Dm.num(maximum_size) - Dm.num(size)) * base_mult / (w_class * FISH_SIZE_WEIGHT_GROWTH_MALUS) * hunger_mult, 1);
            new_size = min(new_size, Dm.num(maximum_size));
        }
        if (Dm.num(weight) < Dm.num(maximum_weight))
        {
            new_weight += CEILING((Dm.num(maximum_weight) - Dm.num(weight)) * base_mult / (GET_FISH_WEIGHT_RANK(weight) * FISH_SIZE_WEIGHT_GROWTH_MALUS) * hunger_mult, 1);
            new_weight = min(new_weight, Dm.num(maximum_weight));
        }
        if (new_size != Dm.num(size) || new_weight != Dm.num(weight))
            await update_size_and_weight(new_size, new_weight);
    }

    // tg: code/modules/fishing/fish/_fish.dm:989
    public virtual void check_flopping()
    {
        if (QDELETED(this)) //we don't care anymore
            return;

        if (!Dm.truthy(Dm.bitand(fish_flags, FISH_DO_FLOP_ANIM)))
            return;

        // Do additional stuff
        // Start flopping if outside of fish container
        bool should_be_flopping = status == FISH_ALIVE && (loc != null && !HAS_TRAIT(loc, TRAIT_STOP_FISH_FLOPPING));

        if (should_be_flopping)
            start_flopping();
        else
            stop_flopping();
    }

    // tg: code/modules/fishing/fish/_fish.dm:1005
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (HAS_TRAIT(this, TRAIT_FISH_STASIS) || status != FISH_ALIVE)
            return null;
        Waiting.NoSleep(do_fish_process(seconds_per_tick));
        if (status != FISH_ALIVE || !Dm.truthy(is_type_in_typecache(loc, SSfishing?.fish_safe_turfs_by_type?[type])))
        {
            time_passed_on_safe_turf = 0 * SECONDS;
            return null;
        }
        time_passed_on_safe_turf += Dm.num(seconds_per_tick) * SECONDS;
        if (time_passed_on_safe_turf >= (Dm.truthy(get_starvation_mult()) ? STARVING_FISH_SUBMERGING_THRESHOLD : FISH_SUBMERGING_THRESHOLD))
        {
            visible_message(span_notice(I18n.T("[] disperses into \\the []", this, loc)), span_notice(I18n.T("You hear a splash.")));
            released(loc);
        }
        return null;
    }

    // tg: code/modules/fishing/fish/_fish.dm:1017
    public virtual async DmTask do_fish_process(object seconds_per_tick = null)
    {
        //safe mode, don't do much except a few things that don't involve growing or reproducing.
        if (loc != null && HAS_TRAIT_FROM(loc, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT))
        {
            last_feeding = Dm.num(last_feeding) + Dm.num(seconds_per_tick) * SECONDS;
            breeding_wait = Dm.num(breeding_wait) + Dm.num(seconds_per_tick) * SECONDS;
        }
        else
        {
            await process_health(seconds_per_tick);
            if (Dm.truthy(ready_to_reproduce()))
                await try_to_reproduce();

            if (HAS_TRAIT(this, TRAIT_FISH_ELECTROGENESIS) && electrogenesis_cooldown <= world.time) // COOLDOWN_FINISHED, inline (rules 11.1 (a))
                await try_electrogenesis();
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_FISH_LIFE]))
            _SendSignal(COMSIG_FISH_LIFE, Dm.list(this, seconds_per_tick));
    }

    // tg: code/modules/fishing/fish/_fish.dm:1032
    public virtual async DmTask set_status(object new_status = null, float? silent = null)
    {
        silent ??= FALSE;
        if (Dm.eq(status, new_status))
            return;
        if (Dm.eq(new_status, FISH_ALIVE))
        {
            status = FISH_ALIVE;
            await repair_damage(max_integrity);
            await regenerate_bites(bites_amount);
            last_feeding = world.time; //reset hunger
            check_flopping();
            START_PROCESSING(SSobj, this);
            ADD_TRAIT(this, TRAIT_UNCOMPOSTABLE, INNATE_TRAIT);
        }
        else if (Dm.eq(new_status, FISH_DEAD))
        {
            status = FISH_DEAD;
            STOP_PROCESSING(SSobj, this);
            REMOVE_TRAIT(this, TRAIT_UNCOMPOSTABLE, INNATE_TRAIT);
            stop_flopping();
            if (!Dm.truthy(silent))
            {
                object message = span_warning(replacetext(death_text, "%SRC", Dm.text("[]", this)));
                if (loc != null && HAS_TRAIT(loc, TRAIT_IS_AQUARIUM))
                    loc.visible_message(message);
                else
                    visible_message(message);
            }
        }
        update_appearance();
        await update_fish_force();
        if (Dm.truthy(_listen_lookup?[COMSIG_FISH_STATUS_CHANGED]))
            _SendSignal(COMSIG_FISH_STATUS_CHANGED, Dm.list(this));
    }

    // tg: code/modules/fishing/fish/_fish.dm:1059
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        if (Dm.eq(var_name, nameof(status)))
        {
            if (!Dm.eq(var_value, FISH_DEAD) && !Dm.eq(var_value, FISH_ALIVE))
                var_value = Dm.truthy(var_value) ? FISH_ALIVE : FISH_DEAD;
            await set_status(var_value);
        }
        else if (Dm.eq(var_name, nameof(size)))
        {
            if (!isnum(var_value) || Dm.eq(var_value, 0))
                return FALSE;
            await update_size_and_weight(var_value, weight);
        }
        else if (Dm.eq(var_name, nameof(weight)))
        {
            if (!isnum(var_value) || Dm.eq(var_value, 0))
                return FALSE;
            await update_size_and_weight(size, var_value);
        }
        else if (Dm.eq(var_name, nameof(fish_flags)))
        {
            float old_fish_flags = fish_flags;
            fish_flags = Dm.num(var_value);
            if (Dm.truthy(Dm.bitand(Dm.bitxor(old_fish_flags, fish_flags), FISH_DO_FLOP_ANIM))) //the flopping flag wasn't added nor removed
                return TRUE;
            if (Dm.truthy(Dm.bitand(fish_flags, FISH_DO_FLOP_ANIM)))
                RegisterSignal(this, COMSIG_ATOM_TEMPORARY_ANIMATION_START, nameof(on_temp_animation));
            else
                UnregisterSignal(this, COMSIG_ATOM_TEMPORARY_ANIMATION_START);
            check_flopping();
        }
        else if (Dm.eq(var_name, nameof(fillet_type)))
        {
            if (!ispath(var_value))
                return FALSE;
            remove_fillet_type();
            fillet_type = var_value as Type;
            await add_fillet_type();
        }
        else if (Dm.eq(var_name, nameof(num_fillets)))
        {
            if (!isnum(var_value))
                return FALSE;
            remove_fillet_type();
            num_fillets = Dm.num(var_value);
            await add_fillet_type();
        }
        else
            return await base.vv_edit_var(var_name, var_value);

        return TRUE;
    }

    // tg: code/modules/fishing/fish/_fish.dm:1100
    public override async DmTask<object> expose_reagents(DmList reagents = null, datum_reagents source = null, float? methods = null, float? show_message = null, params object[] __extra)
    {
        methods ??= TOUCH;
        float? volume_modifier = Dm.arg<float?>(__extra, 0);
        volume_modifier ??= 1;
        show_message ??= TRUE;
        object dot = await base.expose_reagents(reagents, source, methods, show_message, __extra);
        if (Dm.truthy(Dm.bitand(dot, COMPONENT_NO_EXPOSE_REAGENTS)) || status != FISH_DEAD)
            return dot;
        datum_reagent_medicine_strange_reagent revival = Dm.locate<datum_reagent_medicine_strange_reagent>(reagents);
        if (revival == null)
            return dot;
        if (Dm.num(reagents[revival]) >= 2 * w_class && Dm.truthy(revival.pre_rez_check(this)))
            await set_status(FISH_ALIVE);
        else
        {
            balloon_alert_to_viewers(I18n.T("twitches for a moment!"));
            animate(this, Dm.named("pixel_x", 1), Dm.named("time", 0.1f * SECONDS), Dm.named("loop", 2), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
            animate(Dm.named("pixel_x", -1), Dm.named("flags", ANIMATION_RELATIVE));
        }
        return dot;
    }

    // tg: code/modules/fishing/fish/_fish.dm:1114
    [SignalHandler]
    public virtual object use_lazarus(datum source = null, obj_item_lazarus_injector injector = null, mob user = null)
    {
        if (injector.revive_type != SENTIENCE_ORGANIC)
        {
            balloon_alert(user, I18n.T("invalid creature!"));
            return null;
        }
        if (status != FISH_DEAD)
        {
            balloon_alert(user, I18n.T("[] not dead!", p_theyre()));
            return null;
        }
        Waiting.NoSleep(set_status(FISH_ALIVE));
        injector.expend(this, user);
        return LAZARUS_INJECTOR_USED;
    }

    // tg: code/modules/fishing/fish/_fish.dm:1126
    [SignalHandler]
    public virtual void update_aquarium_appearance(datum source = null, obj_effect_aquarium visual = null)
    {
        visual.icon = Dm.truthy(dedicated_in_aquarium_icon) ? dedicated_in_aquarium_icon : icon;
        visual.icon_state = Dm.truthy(dedicated_in_aquarium_icon_state) ? Dm.str(dedicated_in_aquarium_icon_state) : Dm.text("[]_small", Dm.initial(this, (atom p) => p.icon_state));
    }

    // tg: code/modules/fishing/fish/_fish.dm:1131
    [SignalHandler]
    public virtual void randomize_aquarium_position(datum source = null, atom_movable current_aquarium = null, obj_effect_aquarium visual = null)
    {
        float avg_width = round(Dm.num(sprite_width) * 0.5f);
        float avg_height = round(Dm.num(sprite_height) * 0.5f);
        float pw_min = Dm.num(visual.aquarium_zone_min_pw) + avg_width - 16;
        float pw_max = Dm.num(visual.aquarium_zone_max_pw) - avg_width - 16;
        float pz_min = Dm.num(visual.aquarium_zone_min_pz) + avg_height - 16;
        float pz_max = Dm.num(visual.aquarium_zone_max_pz) - avg_height - 16;

        visual.pixel_w = visual.base_pixel_w = rand(pw_min, pw_max);
        visual.pixel_z = visual.base_pixel_z = rand(pz_min, pz_max);
    }

    // tg: code/modules/fishing/fish/_fish.dm:1143
    [SignalHandler]
    public virtual void update_aquarium_animation(datum source = null, object current_animation = null, obj_effect visual = null, object fluid_type = null)
    {
        object animation = get_aquarium_animation(fluid_type);
        if (Dm.eq(animation, current_animation))
            return;
        if (Dm.eq(animation, AQUARIUM_ANIMATION_FISH_SWIM))
            swim_animation(visual as obj_effect_aquarium);
        else if (Dm.eq(animation, AQUARIUM_ANIMATION_FISH_DEAD))
            dead_animation(visual as obj_effect_aquarium);
    }

    // tg: code/modules/fishing/fish/_fish.dm:1154
    public virtual object get_aquarium_animation(object fluid_type = null)
    {
        if (Dm.eq(fluid_type, AQUARIUM_FLUID_AIR) || status == FISH_DEAD)
            return AQUARIUM_ANIMATION_FISH_DEAD;
        else
            return AQUARIUM_ANIMATION_FISH_SWIM;
    }

    /// Create looping random path animation, pixel offsets parameters include offsets already
    // tg: code/modules/fishing/fish/_fish.dm:1161
    public virtual void swim_animation(obj_effect_aquarium visual = null)
    {
        float avg_width = round(Dm.num(sprite_width) / 2);
        float avg_height = round(Dm.num(sprite_height) / 2);

        float pw_min = Dm.num(visual.aquarium_zone_min_pw) + avg_width - 16;
        float pw_max = Dm.num(visual.aquarium_zone_max_pw) - avg_width - 16;
        float pz_min = Dm.num(visual.aquarium_zone_min_pz) + avg_height - 16;
        float pz_max = Dm.num(visual.aquarium_zone_max_pz) - avg_width - 16;

        float origin_w = visual.base_pixel_w;
        float origin_z = visual.base_pixel_z;
        float prev_w = origin_w;
        float prev_z = origin_z;
        animate(visual, Dm.named("pixel_w", origin_w), Dm.named("time", 0), Dm.named("loop", -1)); //Just to start the animation
        float move_number = rand(3, 5); //maybe unhardcode this
        for (float i = 1; i <= move_number; i++)
        {
            //If it's last movement, move back to start otherwise move to some random point
            float target_w = i == move_number ? origin_w : rand(pw_min, pw_max); //could do with enforcing minimal delta for prettier zigzags
            float target_z = i == move_number ? origin_z : rand(pz_min, pz_max);
            float dist_w = prev_w - target_w;
            float dist_z = prev_z - target_z;
            prev_w = target_w;
            prev_z = target_z;
            float dist = abs(dist_w) + abs(dist_z);
            float eyeballed_time = dist * 2; //2ds per px
            //Face the direction we're going
            matrix dir_mx = Dm.matrix(visual.transform);
            if (dist_w <= 0) //assuming default sprite is facing left here
                dir_mx.Scale(-1, 1);
            animate(Dm.named("transform", dir_mx), Dm.named("time", 0), Dm.named("loop", -1));
            animate(Dm.named("pixel_w", target_w), Dm.named("pixel_z", target_z), Dm.named("time", eyeballed_time), Dm.named("loop", -1));
        }
    }

    // tg: code/modules/fishing/fish/_fish.dm:1193
    public virtual void dead_animation(obj_effect_aquarium visual = null)
    {
        //Set base_pixel_y to lowest possible value
        float avg_height = round(Dm.num(sprite_height) / 2);
        float pz_min = Dm.num(visual.aquarium_zone_min_pz) + avg_height - 16;
        visual.base_pixel_z = pz_min;
        animate(visual, Dm.named("pixel_z", pz_min), Dm.named("time", 1)); //flop to bottom and end current animation.
    }

    ///Malus to the beauty value if the fish content is dead
    // tg: code/modules/fishing/fish/_fish.dm:1201  #define DEAD_FISH_BEAUTY -500 (file-local)
    private const float DEAD_FISH_BEAUTY = -500;
    ///Prevents more impressive fishes from providing a positive beauty even when dead.
    // tg: code/modules/fishing/fish/_fish.dm:1203  #define MAX_DEAD_FISH_BEAUTY -200 (file-local)
    private const float MAX_DEAD_FISH_BEAUTY = -200;
    ///Some fish are already so ugly, they can't get much worse when dead
    // tg: code/modules/fishing/fish/_fish.dm:1205  #define MIN_DEAD_FISH_BEAUTY -600 (file-local)
    private const float MIN_DEAD_FISH_BEAUTY = -600;

    // tg: code/modules/fishing/fish/_fish.dm:1207
    [SignalHandler]
    public virtual void get_aquarium_beauty(datum source = null, DmList beauty_holder = null)
    {
        float actual_beauty = beauty;
        if (status == FISH_DEAD)
            actual_beauty = clamp(beauty + DEAD_FISH_BEAUTY, MIN_DEAD_FISH_BEAUTY, MAX_DEAD_FISH_BEAUTY);

        Dm.append(beauty_holder, actual_beauty);
    }

    /// Checks if our current environment lets us live.
    // tg: code/modules/fishing/fish/_fish.dm:1220
    public virtual async DmTask<object> proper_environment(object temp_range_min = null, object temp_range_max = null)
    {
        temp_range_min ??= required_temperature_min;
        temp_range_max ??= required_temperature_max;
        if (loc == null)
            return TRUE;

        if (HAS_TRAIT(loc, TRAIT_IS_AQUARIUM))
        {
            if (!Dm.truthy(Dm.bitand(fish_flags, FISH_FLAG_SAFE_TEMPERATURE)) || !Dm.truthy(Dm.bitand(fish_flags, FISH_FLAG_SAFE_FLUID)))
                return FALSE;
            return TRUE;
        }

        if (Dm.truthy(is_type_in_typecache(loc, SSfishing?.fish_safe_turfs_by_type?[type])))
            return TRUE;
        if (required_fluid_type != AQUARIUM_FLUID_AIR && !HAS_TRAIT(this, TRAIT_FISH_AMPHIBIOUS))
            return FALSE;
        datum_gas_mixture mixture = await loc.return_air() as datum_gas_mixture;
        if (mixture == null)
            return FALSE;
        if (safe_air_limits != null && !Dm.truthy(mixture.check_gases(safe_air_limits)))
            return FALSE;
        // Kept as tg has it: the temperature is checked against the fish's own range, not the arguments.
        if (!ISINRANGE(mixture.temperature, required_temperature_min, required_temperature_max))
            return FALSE;
        float pressure = Dm.num(mixture.return_pressure());
        if (!ISINRANGE(pressure, min_pressure, max_pressure))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/fishing/fish/_fish.dm:1245
    public virtual async DmTask process_health(object seconds_per_tick = null)
    {
        float health_change = 0;
        if (!Dm.truthy(await proper_environment()))
            health_change -= 2.5f; //Dying here
        float starvation_mult = Dm.num(get_starvation_mult());
        if (Dm.truthy(starvation_mult))
            health_change -= 0.25f * starvation_mult; //Starving
        else
            health_change += 0.5f; //Slowly healing
        if (HAS_TRAIT(this, TRAIT_FISH_ON_TESLIUM))
            health_change -= 0.65f;

        if (!Dm.truthy(health_change))
            return;

        health_change *= Dm.num(seconds_per_tick);
        if (health_change < 0)
            await damage_fish(-health_change);
        else
            await repair_damage(health_change);
    }

    ///Used to damage this fish while it's still alive. Prevents the fish from taking damage beyond the integrity_failure threshold
    // tg: code/modules/fishing/fish/_fish.dm:1267
    public virtual async DmTask damage_fish(object amount = null)
    {
        if (status == FISH_DEAD || Dm.num(amount) <= 0)
            return;
        float current_integrity = Dm.num(get_integrity());
        await take_damage(min(Dm.num(amount), current_integrity - max_integrity * integrity_failure), sound_effect: FALSE, armour_penetration: 100);
    }

    /// fish dies when its integrity reaches 50%
    // tg: code/modules/fishing/fish/_fish.dm:1274
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        await set_status(FISH_DEAD);
        return dot;
    }

    // tg: code/modules/fishing/fish/_fish.dm:1278
    public override async DmTask<object> repair_damage(object amount = null)
    {
        object dot = await base.repair_damage(amount);
        if (!Dm.truthy(dot) || !Dm.truthy(bites_amount))
            return dot;
        float current_integrity = Dm.num(get_integrity());
        float old_integrity = current_integrity - Dm.num(amount);
        float old_max_integrity_diff = max_integrity - old_integrity;
        float percent = (max_integrity - current_integrity) / old_max_integrity_diff;
        float bites_to_recover = bites_amount * percent;
        await regenerate_bites(bites_to_recover);
        return dot;
    }

    // tg: code/modules/fishing/fish/_fish.dm:1289
    public virtual async DmTask regenerate_bites(object amount = null)
    {
        amount = min(Dm.num(amount), bites_amount);
        if (Dm.num(amount) <= 0)
            return;
        bites_amount -= Dm.num(amount);
        await generate_fish_reagents(Dm.num(amount));
    }

    /// returns a value between 0 and 1 representing how much integrity the fish has before dying (atom_break)
    // tg: code/modules/fishing/fish/_fish.dm:1297
    public virtual object get_health_percentage()
    {
        float max_health = max_integrity * (1 - integrity_failure);
        float death_thres = max_integrity - max_health;
        return CLAMP01((Dm.num(get_integrity()) - death_thres) / max_health);
    }

    /// Returns tracked_fish_by_type but flattened and without the items in the blacklist, also shuffled if shuffle is TRUE.
    // tg: code/modules/fishing/fish/_fish.dm:1303
    public virtual object get_aquarium_fishes(float? shuffle = null, object blacklist = null)
    {
        shuffle ??= FALSE;
        object dot = Dm.list();
        foreach (obj_item_fish fish in Dm.each<obj_item_fish>(loc))
            Dm.append(ref dot, fish);
        Dm.remove(ref dot, blacklist);
        if (Dm.truthy(shuffle))
            dot = GlobalProcs.shuffle(dot as DmList);
        return dot;
    }

    // tg: code/modules/fishing/fish/_fish.dm:1312
    public virtual object ready_to_reproduce(float? being_targeted = null)
    {
        being_targeted ??= FALSE;
        if (loc == null || !HAS_TRAIT(loc, TRAIT_IS_AQUARIUM))
            return FALSE;
        if (Dm.truthy(being_targeted) && HAS_TRAIT(this, TRAIT_FISH_NO_MATING))
            return FALSE;
        if (!Dm.truthy(being_targeted) && length(get_aquarium_fishes()) >= AQUARIUM_MAX_BREEDING_POPULATION)
            return FALSE;
        return Dm.num(!HAS_TRAIT(loc, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH) && Dm.num(get_health_percentage()) >= 0.8f && world.time >= Dm.num(breeding_wait));
    }

    // tg: code/modules/fishing/fish/_fish.dm:1321
    public virtual async DmTask<object> try_to_reproduce()
    {
        if (loc == null || !HAS_TRAIT(loc, TRAIT_IS_AQUARIUM))
            return FALSE;

        obj_item_fish second_fish = null;

        ///Fishes with this trait cannot mate, but could still reproduce asexually, so don't early return.
        if (!HAS_TRAIT(this, TRAIT_FISH_NO_MATING))
        {
            DmList available_fishes = Dm.list();
            if (Dm.truthy(loc._listen_lookup?[COMSIG_AQUARIUM_GET_REPRODUCTION_CANDIDATES]))
                loc._SendSignal(COMSIG_AQUARIUM_GET_REPRODUCTION_CANDIDATES, Dm.list(loc, this, available_fishes));
            Dm.remove(ref available_fishes, this); //self-reproduction goes last
            if (Dm.truthy(length(available_fishes)))
            {
                available_fishes = shuffle(available_fishes) as DmList;
                foreach (obj_item_fish other_fish in Dm.each_cast<obj_item_fish>(available_fishes))
                {
                    if (Dm.truthy(other_fish.ready_to_reproduce(TRUE)))
                    {
                        second_fish = other_fish;
                        break;
                    }
                }
            }
        }

        //check if the fish can self-reproduce if there's no other option
        if (second_fish == null && !HAS_TRAIT(this, TRAIT_FISH_SELF_REPRODUCE))
            return FALSE;

        // tg: PERFORM_ALL_TESTS(fish_breeding) is FALSE in production (rules 1.7)
        if (Dm.truthy(FALSE) && second_fish != null && !Dm.truthy(length(evolution_types)))
            return await create_offspring(second_fish.type, second_fish);

        object chosen_type = null;
        datum_fish_evolution chosen_evolution = null;
        DmList possible_evolutions = Dm.list();
        foreach (object evolution_type in Dm.each(evolution_types))
        {
            datum_fish_evolution evolution = GLOB.fish_evolutions[evolution_type] as datum_fish_evolution;
            if (Dm.truthy(await evolution.check_conditions(this, second_fish, loc as atom_movable)))
                Dm.append(ref possible_evolutions, evolution);
        }
        if (Dm.truthy(second_fish?.evolution_types))
        {
            object secondary_evolutions = Dm.sub(second_fish.evolution_types, evolution_types);
            foreach (object evolution_type in Dm.each(secondary_evolutions))
            {
                datum_fish_evolution evolution = GLOB.fish_evolutions[evolution_type] as datum_fish_evolution;
                if (Dm.truthy(await evolution.check_conditions(second_fish, this, loc as atom_movable)))
                    Dm.append(ref possible_evolutions, evolution);
            }
        }

        DmList types = spawn_types is DmList or__1 && Dm.truthy(or__1) ? or__1 : Dm.list(type);
        if (Dm.truthy(length(possible_evolutions)))
        {
            chosen_evolution = pick(possible_evolutions) as datum_fish_evolution;
            chosen_type = chosen_evolution.new_fish_type;
        }
        else if (second_fish != null)
        {
            DmList second_fish_types = second_fish.spawn_types is DmList or__2 && Dm.truthy(or__2) ? or__2 : Dm.list(second_fish.type);
            bool recessive = HAS_TRAIT(this, TRAIT_FISH_RECESSIVE);
            bool recessive_partner = HAS_TRAIT(second_fish, TRAIT_FISH_RECESSIVE);
            if (Dm.truthy(Dm.bitand(fish_flags, FISH_FLAG_OVERPOPULATED)))
            {
                if (recessive_partner && !recessive)
                    return FALSE;
                chosen_type = pick(second_fish_types);
            }
            else
            {
                if (recessive && !recessive_partner)
                    chosen_type = pick(second_fish_types);
                else if (recessive_partner && !recessive)
                    chosen_type = pick(types);
                else
                {
                    DmList picks = Dm.add(second_fish_types, types) as DmList;
                    chosen_type = pick(picks);
                }
            }
        }
        else
            chosen_type = pick(types);

        return await create_offspring(chosen_type, second_fish, chosen_evolution);
    }

    ///The timeout multiplier for offspring fish, the ones generated when two compatible fish are coupled
    // tg: code/modules/fishing/fish/_fish.dm:1386  #define OFFSPRING_FISH_BREEDING_TIMEOUT_MULT 2 (file-local)
    private const float OFFSPRING_FISH_BREEDING_TIMEOUT_MULT = 2;

    /// A product of fish breeding is spawned, and it's inherited traits are handled here.
    // tg: code/modules/fishing/fish/_fish.dm:1389
    public virtual async DmTask<object> create_offspring(object chosen_type = null, obj_item_fish partner = null, datum_fish_evolution evolution = null)
    {
        obj_item_fish new_fish = Dm.@new((Type)chosen_type, loc, FALSE) as obj_item_fish;
        //Try to pass down compatible traits based on inheritability
        await new_fish.inherit_traits(fish_traits, partner?.fish_traits, evolution?.new_traits, evolution?.removed_traits);

        ///If set, the offspring will inherit materials from the parent.
        obj_item_fish chosen_material_giver = null;
        //We combine two methods for determining the size and weight of the offspring for less extreme results.
        if (partner != null)
        {
            float ratio_size = new_fish.average_size * (((Dm.num(size) / average_size) + (Dm.num(partner.size) / partner.average_size)) / 2);
            float mean_size = (Dm.num(size) + Dm.num(partner.size)) / 2;
            // Kept as tg has it: the weight ratio is scaled by the offspring's average size, not its average weight.
            float ratio_weight = new_fish.average_size * (((Dm.num(weight) / average_weight) + (Dm.num(partner.weight) / partner.average_weight)) / 2);
            float mean_weight = (Dm.num(weight) + Dm.num(partner.weight)) / 2;
            await new_fish.randomize_size_and_weight((mean_size + ratio_size) * 0.5f, (mean_weight + ratio_weight) * 0.5f, 0.3f, update: FALSE);
            partner.breeding_wait = world.time + partner.breeding_timeout;

            if (Dm.truthy(length(partner.custom_materials)))
            {
                if (Dm.truthy(length(custom_materials)))
                    chosen_material_giver = pick(this, partner) as obj_item_fish;
                else if (prob(50))
                    chosen_material_giver = partner;
            }
            else if (Dm.truthy(length(custom_materials)) && prob(50))
                chosen_material_giver = this;
        }
        else
        {
            new_fish.temp_size = size;
            new_fish.temp_weight = weight;
            if (Dm.truthy(length(custom_materials)))
                chosen_material_giver = this;
        }

        if (chosen_material_giver != null)
        {
            //We need the original weight of the fish to set the correct amount (it scales with weight) of mats for the offspring
            float mats_multiplier = Dm.num(new_fish.temp_weight) / (Dm.num(chosen_material_giver.weight) / material_weight_mult);
            DmList new_mats = chosen_material_giver.custom_materials.Copy();
            foreach (object material in Dm.each(new_mats))
                new_mats[material] = Dm.num(new_mats[material]) * mats_multiplier;
            await new_fish.set_custom_materials(new_mats); // apply_material_effects() will call update_size_and_weight()
        }
        else
            await new_fish.update_size_and_weight(new_fish.temp_size, new_fish.temp_weight);

        DmList fishing_data = Dm.list(new_fish.size, new_fish.weight, new_fish.custom_materials);
        log_fish(Dm.text("[] has been bred at [] from [].", new_fish, new_fish.drop_location(), partner), fishing_data);

        breeding_wait = world.time + breeding_timeout;

        new_fish.breeding_wait = world.time + new_fish.breeding_timeout * OFFSPRING_FISH_BREEDING_TIMEOUT_MULT;

        return new_fish;
    }

    // tg: code/modules/fishing/fish/_fish.dm:1440-1446 the flopping animation's file-local defines
    private const float PAUSE_BETWEEN_PHASES = 15;
    private const float PAUSE_BETWEEN_FLOPS = 2;
    private const float FLOP_COUNT = 2;
    private const float FLOP_DEGREE = 20;
    private const float FLOP_SINGLE_MOVE_TIME = 1.5f;
    private const float JUMP_X_DISTANCE = 5;
    private const float JUMP_Y_DISTANCE = 6;

    /// This flopping animation played while the fish is alive.
    // tg: code/modules/fishing/fish/_fish.dm:1449
    public virtual void flop_animation()
    {
        float pause_between = PAUSE_BETWEEN_PHASES + rand(1, 5); //randomized a bit so fish are not in sync
        animate(this, Dm.named("time", pause_between), Dm.named("loop", -1));
        //move nose down and up
        for (float _ = 1; _ <= FLOP_COUNT; _++)
        {
            matrix up_matrix = Dm.matrix();
            up_matrix.Turn(FLOP_DEGREE);
            matrix down_matrix = Dm.matrix();
            down_matrix.Turn(-FLOP_DEGREE);
            animate(Dm.named("transform", down_matrix), Dm.named("time", FLOP_SINGLE_MOVE_TIME), Dm.named("loop", -1));
            animate(Dm.named("transform", up_matrix), Dm.named("time", FLOP_SINGLE_MOVE_TIME), Dm.named("loop", -1));
            animate(Dm.named("transform", Dm.matrix()), Dm.named("time", FLOP_SINGLE_MOVE_TIME), Dm.named("loop", -1), Dm.named("easing", Dm.bitor(BOUNCE_EASING, EASE_IN)));
            animate(Dm.named("time", PAUSE_BETWEEN_FLOPS), Dm.named("loop", -1));
        }
        //bounce up and down
        animate(Dm.named("time", pause_between), Dm.named("loop", -1), Dm.named("flags", ANIMATION_PARALLEL));
        bool jumping_right = false;
        float up_time = 3 * FLOP_SINGLE_MOVE_TIME / 2;
        for (float _ = 1; _ <= FLOP_COUNT; _++)
        {
            jumping_right = !jumping_right;
            float x_step = jumping_right ? JUMP_X_DISTANCE / 2 : -JUMP_X_DISTANCE / 2;
            animate(Dm.named("time", up_time), Dm.named("pixel_y", JUMP_Y_DISTANCE), Dm.named("pixel_x", x_step), Dm.named("loop", -1), Dm.named("flags", ANIMATION_RELATIVE), Dm.named("easing", Dm.bitor(BOUNCE_EASING, EASE_IN)));
            animate(Dm.named("time", up_time), Dm.named("pixel_y", -JUMP_Y_DISTANCE), Dm.named("pixel_x", x_step), Dm.named("loop", -1), Dm.named("flags", ANIMATION_RELATIVE), Dm.named("easing", Dm.bitor(BOUNCE_EASING, EASE_OUT)));
            animate(Dm.named("time", PAUSE_BETWEEN_FLOPS), Dm.named("loop", -1));
        }
    }

    /// Starts flopping animation
    // tg: code/modules/fishing/fish/_fish.dm:1482
    public virtual void start_flopping()
    {
        if (HAS_TRAIT(this, TRAIT_FISH_FLOPPING)) //Requires update_transform/animate_wrappers to be less restrictive.
            return;
        ADD_TRAIT(this, TRAIT_FISH_FLOPPING, TRAIT_GENERIC);
        flop_animation();
    }

    /// Stops flopping animation
    // tg: code/modules/fishing/fish/_fish.dm:1489
    public virtual void stop_flopping()
    {
        if (HAS_TRAIT(this, TRAIT_FISH_FLOPPING))
        {
            REMOVE_TRAIT(this, TRAIT_FISH_FLOPPING, TRAIT_GENERIC);
            animate(this, Dm.named("transform", Dm.matrix())); //stop animation
        }
    }

    /// Refreshes flopping animation after temporary animation finishes
    // tg: code/modules/fishing/fish/_fish.dm:1495
    public virtual void on_temp_animation(datum source = null, object animation_duration = null)
    {
        if (Dm.num(animation_duration) > 0)
            _addtimer(Dm.@new<datum_callback>(this, nameof(refresh_flopping)), Dm.num(animation_duration), file: "code/modules/fishing/fish/_fish.dm", line: 1497);
    }

    // tg: code/modules/fishing/fish/_fish.dm:1499
    public virtual void refresh_flopping()
    {
        if (HAS_TRAIT(this, TRAIT_FISH_FLOPPING))
            flop_animation();
    }

    // tg: code/modules/fishing/fish/_fish.dm:1503
    public virtual async DmTask try_electrogenesis()
    {
        if (status == FISH_DEAD || Dm.truthy(get_starvation_mult()))
            return;
        // COOLDOWN_START, inline (rules 11.1 (a)); ELECTROGENESIS_VARIANCE is (rand(-10 SECONDS, 10 SECONDS)), expanded inline
        electrogenesis_cooldown = world.time + (ELECTROGENESIS_DURATION + rand(-10 * SECONDS, 10 * SECONDS));
        float fish_zap_range = 1;
        float fish_zap_power = 1 * KILO * JOULES; // ~5 damage, just a little friendly "yeeeouch!"
        float fish_zap_flags = ZAP_MOB_DAMAGE;
        if (HAS_TRAIT(loc, TRAIT_BIOELECTRIC_GENERATOR))
        {
            fish_zap_range = 5;
            fish_zap_power = GET_FISH_ELECTROGENESIS(this);
            if (HAS_TRAIT(this, TRAIT_FISH_ON_TESLIUM))
                fish_zap_power *= 0.5f;
            Dm.combine(ref fish_zap_flags, Dm.bitor(ZAP_GENERATES_POWER, ZAP_MOB_STUN));
        }
        await tesla_zap(source: get_turf(this) as atom, zap_range: fish_zap_range, power: fish_zap_power, cutoff: 1 * MEGA * JOULES, zap_flags: fish_zap_flags);
    }

    // tg: code/modules/fishing/fish/_fish.dm:1519-1529 the export price's file-local defines
    ///The multiplier of the factor of size and weight of the fish, used to determinate the raw price before exponentation
    private const float FISH_PRICE_MULTIPLIER = 0.01f;
    ///This makes each additional unit of fish weight and size yields diminishing marginal returns.
    private const float FISH_PRICE_CURVE_EXPONENT = 0.85f;
    /**
     * past this threshold, the price of fish will plateu even faster.
     * This stops particularly huge fish from being an overly efficient way to make money
     * that bypasses price elasticity by selling fewer units.
     */
    private const float FISH_PRICE_SOFT_CAP_THRESHOLD = 6000;
    ///The second exponent used for soft-capping the fish price.
    private const float FISH_PRICE_SOFT_CAP_EXPONENT = 0.86f;

    ///Returns the price of this fish, for the fish export.
    // tg: code/modules/fishing/fish/_fish.dm:1532
    [DmParams("price")]
    public virtual object get_export_price(object price = null, params object[] __extra)
    {
        float size_weight_exponentation = pow(Dm.num(size) * Dm.num(weight) * FISH_PRICE_MULTIPLIER, FISH_PRICE_CURVE_EXPONENT);
        float raw_price = Dm.num(price) + size_weight_exponentation;
        if (raw_price >= FISH_PRICE_SOFT_CAP_THRESHOLD + 1)
        {
            float soft_cap = pow(raw_price - FISH_PRICE_SOFT_CAP_THRESHOLD, FISH_PRICE_SOFT_CAP_EXPONENT);
            raw_price = FISH_PRICE_SOFT_CAP_THRESHOLD + soft_cap;
        }
        if (HAS_TRAIT(this, TRAIT_FISH_LOW_PRICE)) //Avoid printing money by simply ordering fish and sending it back.
            raw_price *= 0.05f;
        return raw_price;
    }

    // tg: code/modules/fishing/fish/_fish.dm:1547
    public virtual async DmTask<object> get_happiness_value()
    {
        float happiness_value = 0;
        if (Dm.truthy(Dm.bitand(fish_flags, FISH_FLAG_PETTED)))
            happiness_value++;
        if (Dm.num(get_hunger()) < 0.5f)
            happiness_value++;
        if (loc != null && HAS_TRAIT(loc, TRAIT_IS_AQUARIUM))
        {
            if (Dm.truthy(Dm.bitand(fish_flags, FISH_FLAG_SAFE_FLUID)))
                happiness_value++;
            if (Dm.truthy(Dm.bitand(fish_flags, FISH_FLAG_SAFE_TEMPERATURE)))
                happiness_value++;
        }
        else if (Dm.truthy(await proper_environment()))
            happiness_value += 2;
        if (Dm.truthy(bites_amount)) // ouch
            happiness_value -= 2;
        if (Dm.num(get_health_percentage()) < 0.6f)
            happiness_value -= 1;
        return clamp(happiness_value, FISH_SAD, FISH_VERY_HAPPY);
    }

    // tg: code/modules/fishing/fish/_fish.dm:1566
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        await try_pet_fish(user as mob_living);
        return dot;
    }

    // tg: code/modules/fishing/fish/_fish.dm:1570
    public virtual async DmTask<object> try_pet_fish(mob_living user = null)
    {
        bool in_aquarium = loc != null && HAS_TRAIT(loc, TRAIT_IS_AQUARIUM);
        if (status == FISH_DEAD)
        {
            to_chat(user, span_warning(I18n.T("You try to pet [], but [] motionless!", this, p_theyre())));
            return FALSE;
        }
        if (!Dm.truthy(await proper_environment()))
        {
            to_chat(user, span_warning(I18n.T("You try to pet [], but [] not feeling well!", this, p_theyre())));
            return FALSE;
        }

        return await pet_fish(user, Dm.num(in_aquarium));
    }

    // tg: code/modules/fishing/fish/_fish.dm:1581
    public virtual async DmTask<object> pet_fish(mob_living user = null, object in_aquarium = null)
    {
        if (Dm.truthy(Dm.bitand(fish_flags, FISH_FLAG_PETTED)))
        {
            if (Dm.truthy(in_aquarium))
                to_chat(user, span_warning(I18n.T("[] runs away from your finger as you dip it into the water!", this)));
            else
                to_chat(user, span_warning(I18n.T("You try to pet [] but [] squirm[] away!", this, p_they(), p_s())));
            return FALSE;
        }
        if (HAS_TRAIT(this, TRAIT_FISH_ELECTROGENESIS) && GET_FISH_ELECTROGENESIS(this) > 15 * MEGA * JOULES)
            await user.electrocute_act(5, this); //was it all worth it?
        Dm.combine(ref fish_flags, FISH_FLAG_PETTED);
        Dm.@new<obj_effect_temp_visual_heart>(get_turf(this));
        if (Dm.@in(typeof(datum_fish_trait_predator), fish_traits) && prob(50))
        {
            if (Dm.truthy(in_aquarium))
            {
                user.visible_message(
                    span_warning(I18n.T("[] dances around before biting []!", this, user)),
                    span_warning(I18n.T("[] dances around before biting you!", this)),
                    vision_distance: DEFAULT_MESSAGE_RANGE - 3);
            }
            else
            {
                user.visible_message(
                    span_warning(I18n.T("[] bites []'s hand!", this, user)),
                    span_warning(I18n.T("You pet [] as you hold [], only for [] to happily bite back!", this, p_they(), p_them())),
                    vision_distance: DEFAULT_MESSAGE_RANGE - 3);
            }
            object body_zone = pick(BODY_ZONE_R_ARM, BODY_ZONE_L_ARM);
            await user.apply_damage((force * 0.2f) + w_class * 2, BRUTE, body_zone, Dm.num(user.run_armor_check(body_zone, MELEE)));
            playsound(this, Dm.resource("sound/items/weapons/bite.ogg"), 45, TRUE, -1);
        }
        else
        {
            if (Dm.truthy(in_aquarium))
                to_chat(user, span_notice(I18n.T("[] dances around!", this)));
            else
                to_chat(user, span_notice(I18n.T("You pet [] as you hold [].", this, p_they())));
            await user.add_mood_event("petted_fish", typeof(datum_mood_event_fish_petting), this, Dm.num(HAS_MIND_TRAIT(user, TRAIT_MORBID)));
            playsound(this, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 30, TRUE, -1);
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(undo_petted)), 30 * SECONDS, file: "code/modules/fishing/fish/_fish.dm", line: 1615);
        return TRUE;
    }

    // tg: code/modules/fishing/fish/_fish.dm:1618
    public virtual void undo_petted()
    {
        Dm.mask(ref fish_flags, Dm.bitnot(FISH_FLAG_PETTED));
    }

    ///Proc called in trophy_fishes.dm, when a fish is mounted on persistent trophy mounts
    // tg: code/modules/fishing/fish/_fish.dm:1622
    public virtual void persistence_save(DmList data = null)
    {
        return;
    }

    ///Proc called in trophy_fishes.dm, when a persistent fishing trophy mount is spawned and the fish instantiated
    // tg: code/modules/fishing/fish/_fish.dm:1626
    public virtual async DmTask persistence_load(DmList data = null)
    {
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/fishing/fish/_fish.dm:9 (interleaved types, rules 1.9; gaps P4c: the fish bounties read it)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/fishing/fish/_fish.dm:9  GLOBAL_LIST_INIT(fish_compatible_fluid_types, list(...))
    public DmList fish_compatible_fluid_types;

    // tg: code/modules/fishing/fish/_fish.dm:9  (GLOBAL_MANAGED(fish_compatible_fluid_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfish_compatible_fluid_types()
    {
        fish_compatible_fluid_types = Dm.list(
            Dm.kv(AQUARIUM_FLUID_ANY_WATER, Dm.list(AQUARIUM_FLUID_SALTWATER, AQUARIUM_FLUID_FRESHWATER, AQUARIUM_FLUID_SULPHWATEVER)),
            Dm.kv(AQUARIUM_FLUID_ANADROMOUS, Dm.list(AQUARIUM_FLUID_SALTWATER, AQUARIUM_FLUID_FRESHWATER)),
            Dm.kv(AQUARIUM_FLUID_SALTWATER, Dm.list(AQUARIUM_FLUID_SALTWATER)),
            Dm.kv(AQUARIUM_FLUID_FRESHWATER, Dm.list(AQUARIUM_FLUID_FRESHWATER)),
            Dm.kv(AQUARIUM_FLUID_SULPHWATEVER, Dm.list(AQUARIUM_FLUID_SULPHWATEVER)),
            Dm.kv(AQUARIUM_FLUID_AIR, Dm.list(AQUARIUM_FLUID_AIR)));
        Dm.append(ref gvars_datum_init_order, "fish_compatible_fluid_types");
    }
}

public static partial class GlobalProcs
{
    /// Returns random fish, using random_case_rarity probabilities.
    // tg: code/modules/fishing/fish/_fish.dm:1630
    public static object random_fish_type(object required_fluid = null)
    {
        DmList probability_table = TgState.Current.random_fish_type__probability_table;
        string argkey = Dm.text("fish_[]", required_fluid); //If this expands more extract bespoke element arg generation to some common helper.
        if (probability_table == null || !Dm.truthy(probability_table[argkey]))
        {
            if (probability_table == null)
                probability_table = TgState.Current.random_fish_type__probability_table = Dm.list();
            DmList chance_table = Dm.list();
            foreach (Type _fish_type in Dm.each_cast<Type>(subtypesof(typeof(obj_item_fish))))
            {
                Type fish = _fish_type;
                float rarity = Dm.initial(fish, (obj_item_fish p) => p.random_case_rarity);
                if (!Dm.truthy(rarity))
                    continue;
                if (Dm.truthy(required_fluid))
                {
                    string init_fish_fluid_type = Dm.initial(fish, (obj_item_fish p) => p.required_fluid_type);
                    if (!Dm.@in(required_fluid, GLOB.fish_compatible_fluid_types[init_fish_fluid_type]))
                        continue;
                }
                chance_table[fish] = Dm.initial(fish, (obj_item_fish p) => p.random_case_rarity);
            }
            probability_table[argkey] = chance_table;
        }
        return pick_weight(probability_table[argkey] as DmList);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/fishing/fish/_fish.dm:1631  random_fish_type's var/static/probability_table (rules 10.6)
    internal DmList random_fish_type__probability_table;
}
