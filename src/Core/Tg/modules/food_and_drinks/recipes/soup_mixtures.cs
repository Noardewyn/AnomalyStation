// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/recipes/soup_mixtures.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the soup reactions' procs (cooking soup in the soup pot on the stove and the range), the
// prefilled soup bowls and the watery soup's and the beet soup's glass styles (the glass style base is gaps H3's). The soup reagents
// and the other glass styles are data. The var blocks are generated (D111).

/**
 * ## Soup base chemical reaction.
 *
 * Somewhat important note! Keep in mind one serving of soup is roughly 20-25 units. Adjust your results according.
 *
 * Try to aim to have each reaction worth 3 servings of soup (60u - 90u).
 * By default soup reactions require 50 units of water,
 * and they will also inherent the reagents of the ingredients used,
 * so you might end up with more nutrient than you expect.
 */
// tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:25
public partial class datum_chemical_reaction_food_soup : datum_chemical_reaction_food
{
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:57
    public override async DmTask<object> pre_reaction_other_checks(datum_reagents holder = null)
    {
        obj_item_reagent_containers_cup_soup_pot pot = holder.my_atom as obj_item_reagent_containers_cup_soup_pot;
        if (pot == null)
            return FALSE;
        if (!Dm.truthy(Dm.length(required_ingredients)))
            return TRUE;

        //copy of all ingredients to check out
        DmList reqs_copy = required_ingredients.Copy();
        //number of ingredients who's requested amounts has been satisfied
        float completed_ingredients = 0;
        foreach (obj_item ingredient in Dm.each_cast<obj_item>(pot.added_ingredients))
        {
            Type ingredient_type = ingredient.type;
            do
            {
                object ingredient_count = reqs_copy[ingredient_type];

                //means we still have left over ingredients
                if (Dm.truthy(ingredient_count))
                {
                    //decode ingredient type i.e. stack or not and fulfill request
                    if (ispath(ingredient_type, typeof(obj_item_stack)))
                    {
                        obj_item_stack stack_ingredient = (obj_item_stack)ingredient;
                        ingredient_count = Dm.num(ingredient_count) - Dm.num(stack_ingredient.amount);
                    }
                    else
                        ingredient_count = Dm.num(ingredient_count) - 1;

                    //assign final values
                    if (Dm.num(ingredient_count) <= 0)
                    {
                        completed_ingredients += 1;
                        ingredient_count = 0;
                    }
                    reqs_copy[ingredient_type] = ingredient_count;

                    //work complete
                    break;
                }
                //means we have to look for subtypes
                else if (ingredient_count == null)
                    ingredient_type = Dm.parent_type(ingredient_type);
                //means we have no more remaining ingredients so bail, can happen if multiple ingredients of the same type/subtype are in the pot
                else
                    break;
            }
            while (ingredient_type != typeof(obj_item));
        }

        return Dm.num(completed_ingredients == reqs_copy.len);
    }

    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:104
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (!Dm.truthy(Dm.length(required_ingredients)))
            return null;

        // If a food item is supposed to be made, remove relevant ingredients from the pot, then make the item
        if (resulting_food_path != null)
        {
            DmList tracked_ingredients__1 = null;
            LAZYINITLIST(ref tracked_ingredients__1);
            float ingredient_max_multiplier = INFINITY;
            obj_item_reagent_containers_cup_soup_pot pot__1 = holder.my_atom as obj_item_reagent_containers_cup_soup_pot;

            // Tracked ingredients are indexed by type and point to a list containing the actual items
            foreach (obj_item ingredient in Dm.each_cast<obj_item>(pot__1.added_ingredients))
            {
                if (Dm.truthy(is_type_in_list(ingredient, required_ingredients)))
                {
                    DmList by_type = tracked_ingredients__1[ingredient.type] as DmList;
                    LAZYADD(ref by_type, ingredient);
                    tracked_ingredients__1[ingredient.type] = by_type;
                }
            }
            // Find the max number of ingredients that may be used for making the food item
            foreach (object ingredient_type in Dm.each(tracked_ingredients__1))
                ingredient_max_multiplier = min(ingredient_max_multiplier, LAZYLEN(tracked_ingredients__1[ingredient_type] as DmList));
            // Create the food items, removing the relavent ingredients at the same time
            for (float i = 1, i__end = min(Dm.num(created_volume), ingredient_max_multiplier); i <= i__end; i++)
            {
                foreach (object ingredient_type in Dm.each(tracked_ingredients__1))
                {
                    object ingredient = (tracked_ingredients__1[ingredient_type] as DmList)[i];
                    LAZYREMOVE(ref pot__1.added_ingredients, ingredient);
                    qdel(ingredient);
                }
                obj_item created = Dm.@new(resulting_food_path, get_turf(pot__1)) as obj_item;
                created.pixel_y += 8;
            }
            // Re-add required reagents that were not used in this step
            if (Dm.num(created_volume) > ingredient_max_multiplier)
            {
                foreach (object reagent_path in Dm.each(required_reagents))
                    await holder.add_reagent(reagent_path, Dm.num(required_reagents[reagent_path]) * (Dm.num(created_volume) - ingredient_max_multiplier));
            }
        }

        // This only happens if we're being instant reacted so let's just skip to what we really want
        if (reaction == null)
        {
            // testing("Soup reaction of type [type] instant reacted, cleaning up."): empty without TESTING (rules 1.7)
            await clean_up(holder);
            return null;
        }

        if (total_ingredient_max == null)
        {
            total_ingredient_max = 0;
            // We only need to calculate this once, effectively static per-type
            foreach (object ingredient_type in Dm.each(required_ingredients))
                total_ingredient_max = Dm.add(total_ingredient_max, required_ingredients[ingredient_type]);
        }

        obj_item_reagent_containers_cup_soup_pot pot = holder.my_atom as obj_item_reagent_containers_cup_soup_pot;
        DmList tracked_ingredients = Dm.list();
        foreach (obj_item ingredient in Dm.each_cast<obj_item>(pot.added_ingredients))
        {
            // Track all ingredients in data. Assoc list of weakref to ingredient to initial total volume.
            tracked_ingredients[WEAKREF(ingredient)] = (((datum_reagents)ingredient.reagents)?.total_volume is var or__1 && Dm.truthy(or__1) ? or__1 : 1);
            // Equalize temps. Otherwise when we add ingredient temps in, it'll lower reaction temp
            if (ingredient.reagents != null)
                ((datum_reagents)ingredient.reagents).chem_temp = holder.chem_temp;
        }

        // Store a list of weakrefs to ingredients as
        reaction.data["ingredients"] = tracked_ingredients;

        // testing("Soup reaction started of type [type]! [length(pot.added_ingredients)] inside."): empty without TESTING (rules 1.7)
        return null;
    }

    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:161
    public override async DmTask<object> reaction_step(datum_reagents holder = null, datum_equilibrium reaction = null, object delta_t = null, object delta_ph = null, object step_reaction_vol = null)
    {
        if (!Dm.truthy(Dm.length(required_ingredients)))
            return null;
        // testing(...): empty without TESTING (rules 1.7)
        obj_item_reagent_containers_cup_soup_pot pot = holder.my_atom as obj_item_reagent_containers_cup_soup_pot;
        DmList cached_ingredients = reaction.data["ingredients"] as DmList;
        float num_current_ingredients = Dm.length(pot.added_ingredients);
        float num_cached_ingredients = Dm.length(cached_ingredients);

        // Clamp multiplier to ingredient number
        reaction.multiplier = min(reaction.multiplier, Dm.num(Dm.div(num_current_ingredients, total_ingredient_max)));

        // An ingredient was removed during the mixing process.
        // Stop reacting immediately, we can't verify the reaction is correct still.
        // If it is still correct it will restart shortly.
        if (num_current_ingredients < num_cached_ingredients)
            return END_REACTION; // testing("Soup reaction ended due to losing ingredients.")

        // An ingredient was added mid mix.
        // Throw it in the ingredients list
        else if (num_current_ingredients > num_cached_ingredients)
        {
            foreach (obj_item new_ingredient in Dm.each_cast<obj_item>(pot.added_ingredients))
            {
                object new_ref = WEAKREF(new_ingredient);
                if (Dm.truthy(cached_ingredients[new_ref]))
                    continue;
                if (new_ingredient.reagents != null)
                    ((datum_reagents)new_ingredient.reagents).chem_temp = holder.chem_temp;
                cached_ingredients[new_ref] = (((datum_reagents)new_ingredient.reagents)?.total_volume is var or__1 && Dm.truthy(or__1) ? or__1 : 1);
            }
        }

        turf below_pot = get_turf(pot);
        foreach (datum_weakref ingredient_ref in Dm.each_cast<datum_weakref>(cached_ingredients))
        {
            obj_item ingredient = ingredient_ref.resolve() as obj_item;

            // An ingredient has gone missing, stop the reaction
            if (QDELETED(ingredient) || ingredient.loc != holder.my_atom)
                return END_REACTION; // testing("Soup reaction ended due to having an invalid ingredient present.")

            // Transfer 20% of the initial reagent volume of the ingredient to the soup.
            if (!Dm.truthy(await transfer_ingredient_reagents(ingredient, holder, max(Dm.num(cached_ingredients[ingredient_ref]) * 0.2f, 2))))
                continue; //all reagents were transfered

            // Uh oh we reached the top of the pot, the soup's gonna boil over.
            if (holder.total_volume >= holder.maximum_volume * 0.95f)
            {
                below_pot.visible_message(span_warning(I18n.T("[] starts to boil over!", pot)));
                // Create a spread of dirty foam
                await do_foam(1, pot, below_pot, carry: holder, foam_type: typeof(datum_effect_system_fluid_spread_foam_dirty), stop_reactions: TRUE, reagent_scale: 0.1f);
                // Loses a bit from the foam
                foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(holder.reagent_list))
                    reagent.volume = Dm.num(reagent.volume) * 0.5f;
                await holder.update_total();
            }
        }
        return null;
    }

    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:213
    public override async DmTask<object> reaction_finish(datum_reagents holder = null, datum_equilibrium reaction = null, object react_vol = null)
    {
        object dot = await base.reaction_finish(holder, reaction, react_vol);
        obj_item_reagent_containers_cup_soup_pot pot = holder.my_atom as obj_item_reagent_containers_cup_soup_pot;
        if (pot == null)
            throw Dm.CRASH(Dm.text("[]) made it to the end of the [] reaction chain.", holder.my_atom != null ? "Non-pot atom" : "Null pot", type), "code/modules/food_and_drinks/recipes/soup_mixtures.dm", 217);

        // testing(...): empty without TESTING (rules 1.7)
        await clean_up(holder, reaction, react_vol);
        return dot;
    }

    /**
     * Cleans up the ingredients and adds whatever leftover reagents to the mixture
     *
     * * holder: The soup pot
     * * reaction: The reaction being cleaned up, note this CAN be null if being instant reacted
     * * react_vol: How much soup was produced
     */
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:229
    public virtual async DmTask clean_up(datum_reagents holder = null, datum_equilibrium reaction = null, object react_vol = null)
    {
        obj_item_reagent_containers_cup_soup_pot pot = holder.my_atom as obj_item_reagent_containers_cup_soup_pot;

        if (reaction != null)
            reaction.data["ingredients"] = null;

        // If soup is made, remove ingredients as their reagents were added to the soup
        if (Dm.truthy(react_vol))
        {
            foreach (obj_item ingredient in Dm.each_cast<obj_item>(pot.added_ingredients))
            {
                // Let's not mess with  indestructible items.
                // Chef doesn't need more ways to delete things with cooking.
                if (Dm.truthy(Dm.bitand(ingredient.resistance_flags, INDESTRUCTIBLE)))
                    continue;

                // Everything else will just get fried
                if (ingredient.reagents == null && !Dm.truthy(is_type_in_list(ingredient, required_ingredients)))
                {
                    await ingredient.AddElement(typeof(datum_element_fried_item), 30 * SECONDS);
                    continue;
                }

                // Things that had reagents or ingredients in the soup will get deleted
                LAZYREMOVE(ref pot.added_ingredients, ingredient);
                // Send everything left behind
                await transfer_ingredient_reagents(ingredient, holder);
                // Delete, it's done
                qdel(ingredient);
            }
        }

        // Anything left in the ingredient list will get dumped out
        await pot.dump_ingredients(get_turf(pot), y_offset: 8);
        // Blackbox log the chemical reaction used, to account for soup reaction that don't produce typical results
        // BLACKBOX_LOG_FOOD_MADE(type): SSblackbox.record_feedback("tally", "food_made", 1, type) (__DEFINES/food.dm)
        SSblackbox.record_feedback("tally", "food_made", 1, type);
    }

    /**
     * Transfers reagents from the passed reagent to the soup pot, as a "result"
     * Also handles deleting a portion of nutriment reagents present, pseudo-converting
     * it into soup reagent. Returns TRUE if any reagents were transfered FALSE if there is
     * nothing to transfer
     *
     * Arguments
     * * obj/item/ingredient - The ingredient to transfer reagents from
     * * datum/reagentsholder - The reagent holder of the soup pot the reaction is taking place in
     * * amount - The amount of reagents to transfer, if null will transfer all reagents
     */
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:270
    public virtual async DmTask<object> transfer_ingredient_reagents(obj_item ingredient = null, datum_reagents holder = null, object amount = null)
    {
        if (ingredient_reagent_multiplier <= 0)
            return FALSE;
        datum_reagents ingredient_pool = (datum_reagents)ingredient.reagents;
        // Some ingredients are purely flavor (no pun intended) and will have reagents
        if (ingredient_pool == null || ingredient_pool.total_volume <= 0)
            return FALSE;
        if (amount == null)
            amount = ingredient_pool.total_volume; // testing(...): empty without TESTING (rules 1.7)

        // Some of the nutriment goes into "creating the soup reagent" itself, gets deleted.
        // Mainly done so that nutriment doesn't overpower the main course
        float remove_amount = Dm.num(amount) * percentage_of_nutriment_converted;
        await ingredient_pool.remove_reagent(typeof(datum_reagent_consumable_nutriment), remove_amount);
        await ingredient_pool.remove_reagent(typeof(datum_reagent_consumable_nutriment_vitamin), remove_amount);
        // The other half of the nutriment, and the rest of the reagents, will get put directly into the pot
        await ingredient_pool.trans_to(holder, Dm.num(amount), ingredient_reagent_multiplier, no_react: TRUE);
        return TRUE;
    }

    /// Adds text to the requirements list of the recipe
    /// Return a list of strings, each string will be a new line in the requirements list
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:292
    public virtual object describe_recipe_details()
    {
        return null;
    }

    /// Adds text to the results list of the recipe
    /// Return a list of strings, each string will be a new line in the results list
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:297
    public virtual object describe_result()
    {
        return null;
    }
}

/// So this one's kind of a "failed" result, but also a "custom" result
/// Getting to this temperature and having no other soup reaction made means you're either messing something up
/// or you simply aren't following a recipe. So it'll just combine
/// This subtype is only for easy mapping / spawning in specific types of soup.
/// Do not use it anywhere else.
// tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:314
public partial class obj_item_reagent_containers_cup_bowl_soup : obj_item_reagent_containers_cup_bowl
{
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:318
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (initial_reagent != null)
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(initial_reagent, initial_portion));
        return dot;
    }
}

/// This style runs dual purpose -
/// Primarily it's just a bowl style for water,
/// but secondarily it lets chefs know if their soup had too much water in it
// tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:326
public partial class datum_glass_style_has_foodtype_soup_watery_soup : datum_glass_style_has_foodtype_soup
{
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:332
    public override void set_name(obj_item thing = null)
    {
        if (Dm.length(((datum_reagents)thing.reagents).reagent_list) <= 2)
        {
            base.set_name(thing);
            return;
        }

        thing.name = I18n.T("Watery bowl of something");
    }

    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:338
    public override void set_desc(obj_item thing = null)
    {
        if (Dm.length(((datum_reagents)thing.reagents).reagent_list) <= 2)
        {
            base.set_desc(thing);
            return;
        }

        thing.desc = I18n.T("Looks like whatever's in there is very watered down.");
    }
}

// tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:347
public partial class datum_chemical_reaction_food_soup_custom : datum_chemical_reaction_food_soup
{
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:361
    public override async DmTask<object> pre_reaction_other_checks(datum_reagents holder = null)
    {
        obj_item_reagent_containers_cup_soup_pot pot = holder.my_atom as obj_item_reagent_containers_cup_soup_pot;
        if (pot == null)
            return FALSE; // Not a pot
        if (Dm.truthy(holder.is_reacting))
            return FALSE; // Another soup is being made
        if (Dm.length(pot.added_ingredients) <= num_ingredients_needed)
            return FALSE; // Not a lot here to go off of
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:371
    public override object describe_recipe_details()
    {
        return Dm.list(I18n.T("Created from burning soup with at least [] ingredients present", num_ingredients_needed));
    }

    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:374
    public override object describe_result()
    {
        return Dm.list(I18n.T("Whatever's in the pot"));
    }
}

// tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:534
public partial class datum_chemical_reaction_food_soup_hotchili_ghost : datum_chemical_reaction_food_soup_hotchili
{
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:536
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        required_ingredients[typeof(obj_item_food_grown_ghost_chili)] = required_ingredients[typeof(obj_item_food_grown_chili)];
        Dm.remove(ref required_ingredients, typeof(obj_item_food_grown_chili));
        results.Insert(1, typeof(datum_reagent_consumable_nutriment_soup_hotchili_ghost));
    }
}

// tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:643
public partial class datum_chemical_reaction_food_soup_chili_sin_carne_ghost : datum_chemical_reaction_food_soup_chili_sin_carne
{
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:645
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        required_ingredients[typeof(obj_item_food_grown_ghost_chili)] = required_ingredients[typeof(obj_item_food_grown_chili)];
        Dm.remove(ref required_ingredients, typeof(obj_item_food_grown_chili));
        results.Insert(1, typeof(datum_reagent_consumable_nutriment_soup_chili_sin_carne_ghost));
        results[typeof(datum_reagent_consumable_nutriment_soup_chili_sin_carne_ghost)] = results[typeof(datum_reagent_consumable_nutriment_soup_chili_sin_carne)];
    }
}

// tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:795
public partial class datum_chemical_reaction_food_soup_slimesoup_alt : datum_chemical_reaction_food_soup_slimesoup
{
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:806
    public override async DmTask<object> pre_reaction_other_checks(datum_reagents holder = null)
    {
        return TRUE;
    }
}

// tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:853
public partial class datum_chemical_reaction_food_soup_mysterysoup : datum_chemical_reaction_food_soup
{
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:881
    public override async DmTask<object> reaction_finish(datum_reagents holder = null, datum_equilibrium reaction = null, object react_vol = null)
    {
        object dot = await base.reaction_finish(holder, reaction, react_vol);
        await holder.add_reagent(pick(extra_reagent_types), num_bonus);
        return dot;
    }

    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:885
    public override object describe_result()
    {
        DmList extra_sublist = Dm.list();
        foreach (Type extra_type in Dm.each_cast<Type>(extra_reagent_types))
            Dm.append(ref extra_sublist, Dm.text("[]", Dm.initial(extra_type, (datum_reagent r) => r.name)));

        return Dm.list(I18n.T("Will also contain [] units of one randomly: []", num_bonus, Dm.jointext(extra_sublist, ", ")));
    }
}

// tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:908
public partial class datum_chemical_reaction_food_soup_monkey : datum_chemical_reaction_food_soup
{
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:926
    public override object describe_result()
    {
        return Dm.list(I18n.T("May contain a monkey."));
    }
}

// tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:964
public partial class datum_glass_style_has_foodtype_soup_white_beet : datum_glass_style_has_foodtype_soup
{
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:969
    public override void set_name(obj_item thing = null)
    {
        string how_do_you_spell_it = Dm.str(Dm.pick(I18n.T("borsch"), I18n.T("bortsch"), I18n.T("borstch"), I18n.T("borsh"), I18n.T("borshch"), I18n.T("borscht")));
        thing.name = how_do_you_spell_it;

        // DM: locate(T) in a /datum/reagents (not a list, not an atom) iterates nothing, so soup is null here as in tg
        datum_reagent soup = Dm.locate_type(required_drink_type, thing.reagents) as datum_reagent;
        if (soup == null)
            // Shouldn't happen but things are weird
            return;

        // Update tastes with the new name
        DmList data__1 = soup.data as DmList;
        LAZYSET(ref data__1, how_do_you_spell_it, 1);
        soup.data = data__1;
        // Not perfect. Doesn't clear when the bowl type is unset. But it'll do well enough
    }
}

// tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:985
public partial class datum_chemical_reaction_food_soup_beetsoup : datum_chemical_reaction_food_soup
{
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:997
    public override object describe_result()
    {
        return Dm.list(I18n.T("Changes name randomly to a common misspelling of \"Borscht\"."));
    }
}

// tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:1869
public partial class datum_chemical_reaction_food_soup_cornmeal_porridge : datum_chemical_reaction_food_soup
{
    // tg: code/modules/food_and_drinks/recipes/soup_mixtures.dm:1878
    public override async DmTask<object> pre_reaction_other_checks(datum_reagents holder = null)
    {
        return TRUE;
    }
}
