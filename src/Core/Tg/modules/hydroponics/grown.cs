// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the fresh produce base. Its seed is hand-declared (rule V3 types it Type; tg stores the seed item in it at init).

// tg: code/modules/hydroponics/grown.dm:14
public partial class obj_item_food_grown : obj_item_food
{
    /// type path, gets converted to item on New(). It's safe to assume it's always a seed item.
    // tg: code/modules/hydroponics/grown.dm:22
    public object seed;

    // tg: code/modules/hydroponics/grown.dm:14 (the hand-declared seed makes the class's var block hand-written: its overrides move here)
    public obj_item_food_grown()
    {
        name = "fresh produce";
        icon = Dm.resource("icons/obj/service/hydroponics/harvest.dmi");
        icon_state = "berrypile";
        worn_icon = Dm.resource("icons/mob/clothing/head/hydroponics.dmi");
        abstract_type = typeof(obj_item_food_grown);
        max_volume = 100;
        w_class = 2;
        resistance_flags = 4;
    }

    private const float BITE_SIZE_POTENCY_MULTIPLIER = 0.05f;
    private const float BITE_SIZE_VOLUME_MULTIPLIER = 0.01f;

    // tg: code/modules/hydroponics/grown.dm:47
    [DmParams("loc", "new_seed")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
    }

    // tg: code/modules/hydroponics/grown.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_seeds new_seed = Dm.arg(__extra, 0) as obj_item_seeds;
        if (!Dm.truthy(tastes))
            tastes = Dm.list(Dm.kv(Dm.text("[]", name), 1)); //This happens first else the component already inits

        if (new_seed != null)
        {
            seed = new_seed.Copy();
        }
        else if (ispath(seed))
        {
            // This is for adminspawn or map-placed growns. They get the default stats of their seed type.
            seed = Dm.@new((Type)seed);
            ((obj_item_seeds)seed).adjust_potency(50 - Dm.num(((obj_item_seeds)seed).potency));
        }
        else if (seed == null)
        {
            stack_trace(I18n.T("Grown object created without a seed. WTF"), "code/modules/hydroponics/grown.dm", 62);
            return INITIALIZE_HINT_QDEL;
        }

        obj_item_seeds our_seed = (obj_item_seeds)seed;

        if (Dm.truthy(offset_at_init))
        {
            pixel_x = base_pixel_x + rand(-5, 5);
            pixel_y = base_pixel_y + rand(-5, 5);
        }

        // Go through all traits in their genes and call on_new_plant from them.
        foreach (datum_plant_gene_trait trait in Dm.each<datum_plant_gene_trait>(our_seed.genes))
            Waiting.NoSleep(trait.on_new_plant(this, loc));

        // Set our default bitesize: bite size = 1 + (potency * 0.05) * (max_volume * 0.01) * modifier
        // A 100 potency, non-densified plant = 1 + (5 * 1 * modifier) = 6u bite size
        // For reference, your average 100 potency tomato has 14u of reagents - So, with no modifier it is eaten in 3 bites
        bite_consumption = 1 + Dm.round(Dm.max(Dm.num(our_seed.potency) * BITE_SIZE_POTENCY_MULTIPLIER, 1) * (Dm.num(max_volume) * BITE_SIZE_VOLUME_MULTIPLIER) * bite_consumption_mod);

        object dot = base.Initialize(mapload, __extra); //Only call it here because we want all the genes and shit to be applied before we add edibility. God this code is a mess.

        Waiting.NoSleep(((datum_reagents)reagents).clear_reagents());
        Waiting.NoSleep(our_seed.prepare_result(this));
        transform = Dm.mul(transform, Dm.num(Dm.pow(Dm.sin((90 * Dm.num(our_seed.potency)) / 100), 2)) + 0.5f); //Makes the resulting produce's sprite larger or smaller based on potency!
        ADD_TRAIT(this, TRAIT_VALID_DNA_INFUSION, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/hydroponics/grown.dm:85
    public override object Destroy(float? force = null)
    {
        if (isatom(seed))
        {
            qdel(seed);
            seed = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/hydroponics/grown.dm:90
    public override async DmTask make_dryable()
    {
        await AddElement(typeof(datum_element_dryable), type); // gaps H2: the drying rack
    }

    // tg: code/modules/hydroponics/grown.dm:93
    public override async DmTask make_leave_trash()
    {
        if (trash_type != null)
            await AddElement(typeof(datum_element_food_trash), trash_type, FOOD_TRASH_OPENABLE, nameof(generate_trash));
    }

    /// Generates a piece of trash based on our plant item. Used by [/datum/element/food_trash].
    /// location - Optional. If passed, generates the item at the passed location instead of at src's drop location.
    // tg: code/modules/hydroponics/grown.dm:100
    public virtual async DmTask<object> generate_trash(atom location = null)
    {
        // If this is some type of grown thing, we pass a seed arg into its Inititalize()
        if (ispath(trash_type, typeof(obj_item_grown)) || ispath(trash_type, typeof(obj_item_food_grown)))
            return Dm.@new(trash_type, location ?? drop_location(), seed);

        return Dm.@new(trash_type, location ?? drop_location());
    }

    // tg: code/modules/hydroponics/grown.dm:107
    public override object blend_requirements(atom_movable grinder = null, mob_living user = null)
    {
        if (!Dm.truthy(dry_grind) || HAS_TRAIT(this, TRAIT_DRIED))
            return TRUE;
        if (user != null)
            to_chat(user, span_warning(I18n.T("[] needs to be dry before it can be ground up!", this)));
        return FALSE;
    }

    /// Turns the nutriments and vitamins into the distill reagent or fruit wine
    // tg: code/modules/hydroponics/grown.dm:115
    public virtual async DmTask ferment()
    {
        float reagent_purity = Dm.num(((obj_item_seeds)seed).get_reagent_purity());
        float purity_above_base = Dm.clamp((reagent_purity - 0.5f) * 2, 0, 1);
        float quality_min = DRINK_NICE;
        float quality_max = DRINK_FANTASTIC;
        float quality = Dm.round(LERP(quality_min, quality_max, purity_above_base));
        foreach (datum_reagent reagent in Dm.each<datum_reagent>(((datum_reagents)reagents).reagent_list))
        {
            if (reagent.type != typeof(datum_reagent_consumable_nutriment) && reagent.type != typeof(datum_reagent_consumable_nutriment_vitamin))
                continue;
            if (distill_reagent != null)
            {
                // initial(booze.boozepwr) of a distill reagent that is no ethanol (the barrelmelon's antihol, the vanilla, ...) reads a var
                // the type does not have: a runtime in tg, which ends the proc here
                if (!ispath(distill_reagent, typeof(datum_reagent_consumable_ethanol)))
                    return;
                DmList data = Dm.list();
                data["quality"] = quality;
                data["boozepwr"] = Dm.round(Dm.num(Dm.initial(distill_reagent, "boozepwr")) * reagent_purity * 2); // default boozepwr at 50% purity
                await ((datum_reagents)reagents).add_reagent(distill_reagent, reagent.volume, data, added_purity: reagent_purity);
            }
            else
            {
                DmList data = Dm.list();
                data["names"] = Dm.list(Dm.kv(Dm.text("[]", Dm.initial(this, "name")), 1));
                data["color"] = Dm.truthy(filling_color) ? filling_color : reagent.color; // filling_color is not guaranteed to be set for every plant. try to use it if we have it, otherwise use the reagent's color var
                data["boozepwr"] = Dm.round(Dm.num(wine_power) * reagent_purity * 2); // default boozepwr at 50% purity
                data["quality"] = quality;
                if (Dm.truthy(wine_flavor))
                    data["tastes"] = Dm.list(Dm.kv(wine_flavor, 1));
                else
                    data["tastes"] = Dm.list(Dm.kv(tastes[1], 1));
                await ((datum_reagents)reagents).add_reagent(typeof(datum_reagent_consumable_ethanol_fruit_wine), reagent.volume, data, added_purity: reagent_purity);
            }
            await ((datum_reagents)reagents).del_reagent(reagent.type);
        }
    }

    // tg: code/modules/hydroponics/grown.dm:143
    public override async DmTask<object> grind_atom(datum_reagents target_holder = null, mob user = null)
    {
        DmList grind_reagents = await grind_results() as DmList;
        foreach (object result in Dm.each(grind_reagents))
            grind_reagents[result] = Dm.round(Dm.num(((obj_item_seeds)seed).potency));

        float grind_results_num = Dm.num(length(grind_reagents));
        if (grind_results_num != 0)
        {
            object average_purity = ((datum_reagents)reagents).get_average_purity();
            float total_nutriment_amount = Dm.num(((datum_reagents)reagents).get_reagent_amount(typeof(datum_reagent_consumable_nutriment), type_check: REAGENT_SUB_TYPE));
            float single_reagent_amount = grind_results_num > 1 ? Dm.round(total_nutriment_amount / grind_results_num, CHEMICAL_QUANTISATION_LEVEL) : total_nutriment_amount;
            await ((datum_reagents)reagents).remove_reagent(typeof(datum_reagent_consumable_nutriment), total_nutriment_amount, include_subtypes: TRUE);
            foreach (object reagent in Dm.each(grind_reagents))
                await ((datum_reagents)reagents).add_reagent(reagent, single_reagent_amount, added_purity: average_purity);
        }

        return reagents == null ? null : await ((datum_reagents)reagents).trans_to(target_holder, ((datum_reagents)reagents).total_volume, transferred_by: user);
    }

    // tg: code/modules/hydroponics/grown.dm:159
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        //if we attack with paper and the grown is a mushroom, create a spore print.
        obj_item_seeds our_seed = seed as obj_item_seeds;
        if (tool is obj_item_paper && our_seed?.get_gene(typeof(datum_plant_gene_trait_plant_type_fungal_metabolism)) != null)
        {
            qdel(tool);
            our_seed.name = Dm.text("[] spore print", LOWER_TEXT(our_seed.plantname));
            our_seed.desc = Dm.text("A dusting of [] spores have been deposited in a beautiful pattern on the surface of the paper. ", LOWER_TEXT(our_seed.plantname));
            our_seed.icon_state = Dm.text("spore_print[]", pick(1, 2, 3));
            await our_seed.forceMove(drop_location() as atom);
            playsound(user, Dm.resource("sound/items/paper_flip.ogg"), 20);
            seed = null;
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.item_interaction(user, tool, modifiers, __extra);
    }
}
