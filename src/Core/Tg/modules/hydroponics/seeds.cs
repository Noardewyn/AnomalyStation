// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/seeds.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the seed's stats a harvested or spawned plant reads. Gaps P2 (D298): the rest of the file (harvesting, mutating,
// the stats' setters, the crossbreeding, the renaming, the graft, the tray's plant picture). The screentip element stays out (D264).

// tg: code/modules/hydroponics/seeds.dm:6
public partial class obj_item_seeds : obj_item
{
    // tg: code/modules/hydroponics/seeds.dm:69
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object nogenes = Dm.arg(__extra, 0) ?? FALSE;
        object dot = base.Initialize(mapload, __extra);
        pixel_x = base_pixel_x + rand(-8, 8);
        pixel_y = base_pixel_y + rand(-8, 8);

        icon_grow ??= Dm.text("[]-grow", species);
        icon_dead ??= Dm.text("[]-dead", species);

        if (icon_harvest == null && get_gene(typeof(datum_plant_gene_trait_plant_type_fungal_metabolism)) == null && yield != -1)
            icon_harvest = Dm.text("[]-harvest", species);

        if (!Dm.truthy(nogenes))
        {
            foreach (object plant_gene in Dm.each(genes))
            {
                if (ispath(plant_gene))
                {
                    Dm.remove(ref genes, plant_gene);
                    Dm.append(ref genes, Dm.@new((Type)plant_gene));
                }
            }

            // Go through all traits in their genes and call on_new_seed from them.
            foreach (datum_plant_gene_trait traits in Dm.each<datum_plant_gene_trait>(genes))
                traits.on_new_seed(this);

            foreach (object reag_id in Dm.each(reagents_add))
                Dm.append(ref genes, Dm.@new<datum_plant_gene_reagent>(reag_id, reagents_add[reag_id]));
            reagents_from_genes(); //quality coding
        }

        // AddElement(/datum/element/contextual_screentip_item_typechecks, ...): the context tips (screentips, not now)
        return dot;
    }

    // tg: code/modules/hydroponics/seeds.dm:106
    public override object Destroy(float? force = null)
    {
        // No AS ANYTHING here, because the list/genes could have typepaths in it.
        foreach (datum_plant_gene gene in Dm.each<datum_plant_gene>(genes))
        {
            gene.on_removed(this);
            qdel(gene);
        }

        genes.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/hydroponics/seeds.dm:115
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Use a pen on it to rename it or change its description.")));
        if (Dm.truthy(reagents_add) && Dm.truthy(user.can_see_reagents()))
        {
            Dm.append(ref dot, span_notice(I18n.T("- Plant Reagents -")));
            foreach (datum_plant_gene_reagent reagent_gene in Dm.each<datum_plant_gene_reagent>(genes))
                Dm.append(ref dot, span_notice(Dm.text("- [] -", reagent_gene.get_name())));
        }
        return dot;
    }

    /// Copy all the variables from one seed to a new instance of the same seed and return it.
    // tg: code/modules/hydroponics/seeds.dm:124
    public virtual object Copy()
    {
        obj_item_seeds copy_seed = Dm.@new(type, null, TRUE) as obj_item_seeds;
        // Copy all the stats
        copy_seed.lifespan = lifespan;
        copy_seed.endurance = endurance;
        copy_seed.maturation = maturation;
        copy_seed.production = production;
        copy_seed.yield = yield;
        copy_seed.potency = potency;
        copy_seed.instability = instability;
        copy_seed.weed_rate = weed_rate;
        copy_seed.weed_chance = weed_chance;
        copy_seed.name = name;
        copy_seed.plantname = plantname;
        copy_seed.desc = desc;
        copy_seed.productdesc = productdesc;
        copy_seed.genes = Dm.list();
        foreach (datum_plant_gene gene in Dm.each<datum_plant_gene>(genes))
        {
            datum_plant_gene copied_gene = gene.Copy() as datum_plant_gene;
            Dm.append(ref copy_seed.genes, copied_gene);
            copied_gene.on_new_seed(copy_seed);
        }

        copy_seed.reagents_add = reagents_add.Copy(); // Faster than grabbing the list from genes.
        return copy_seed;
    }

    // tg: code/modules/hydroponics/seeds.dm:149
    public virtual object get_gene(object typepath = null)
    {
        return Dm.locate_type((Type)typepath, genes);
    }

    // tg: code/modules/hydroponics/seeds.dm:152
    public virtual void reagents_from_genes()
    {
        reagents_add = Dm.list();
        foreach (datum_plant_gene_reagent R in Dm.each<datum_plant_gene_reagent>(genes))
            reagents_add[R.reagent_id] = R.rate;
    }

    // tg: code/modules/hydroponics/seeds.dm:274
    public virtual async DmTask prepare_result(obj_item result = null)
    {
        if (result.reagents == null)
            throw Dm.CRASH(Dm.text("[] has no reagents.", result), "code/modules/hydroponics/seeds.dm", 276);

        if (result is not obj_item_grown && result.GetComponent(typeof(datum_component_edible)) == null) // IS_EDIBLE
            return;

        float sum_reagent_percentage = 0;
        foreach (object reagent_type in Dm.each(reagents_add))
            sum_reagent_percentage += Dm.num(reagents_add[reagent_type]);

        object reagent_purity = get_reagent_purity();
        foreach (object reagent_type in Dm.each(reagents_add))
        {
            float reagent_volume_percentage = Dm.num(reagents_add[reagent_type]);
            // Going over 100% volume percentage penalizes you
            // However the plant will always have at least 1u of each of the reagents in its reagent production traits
            await ((datum_reagents)result.reagents).add_reagent(
                reagent_type: reagent_type,
                amount: Dm.max(1, Dm.round((Dm.num(((datum_reagents)result.reagents).maximum_volume)) * (Dm.num(potency) / 100) * reagent_volume_percentage * Dm.min(1, 1 / sum_reagent_percentage))),
                added_purity: reagent_purity,
                creation_callback: Dm.@new<datum_callback>(this, nameof(send_reagent_signal), result));
        }

        if (Dm.truthy(result?._listen_lookup?[COMSIG_PLANT_ON_HARVEST]))
            result._SendSignal(COMSIG_PLANT_ON_HARVEST, Dm.list(result, this));
    }

    /// Returns reagent purity based on seed stats
    // tg: code/modules/hydroponics/seeds.dm:299
    public virtual object get_reagent_purity()
    {
        float purity_from_lifespan = Dm.num(lifespan) / 400; //up to +25% for lifespan
        float purity_from_endurance = Dm.num(endurance) / 400; //up to +25% for endurance
        float purity_from_instability = rand(-Dm.num(instability), Dm.num(instability)) / 400; //up to +-25% at random for instability
        float result_purity = Dm.clamp(0.5f + purity_from_lifespan + purity_from_endurance + purity_from_instability, 0, 1); //50% base + stats
        return result_purity;
    }

    /// Sends a signal to reagents in produced produce
    // tg: code/modules/hydroponics/seeds.dm:307
    public virtual void send_reagent_signal(obj_item plant = null, datum_reagent the_reagent = null)
    {
        if (Dm.truthy(the_reagent?._listen_lookup?[COMSIG_REAGENT_GROWN_IN_PLANT]))
            the_reagent._SendSignal(COMSIG_REAGENT_GROWN_IN_PLANT, Dm.list(the_reagent, this, plant));
    }

    // tg: code/modules/hydroponics/seeds.dm:157
    public virtual void mutate(float? lifemut = null, float? endmut = null, float? productmut = null, float? yieldmut = null, float? potmut = null, float? wrmut = null, float? wcmut = null, float? traitmut = null, float? stabmut = null)
    {
        float lm = lifemut ?? 2, em = endmut ?? 5, pm = productmut ?? 1, ym = yieldmut ?? 2, ptm = potmut ?? 25, wr = wrmut ?? 2, wc = wcmut ?? 5, tm = traitmut ?? 0, sm = stabmut ?? 3;
        adjust_lifespan(rand(-lm, lm));
        adjust_endurance(rand(-em, em));
        adjust_production(rand(-pm, pm));
        adjust_yield(rand(-ym, ym));
        adjust_potency(rand(-ptm, ptm));
        adjust_instability(rand(-sm, sm));
        adjust_weed_rate(rand(-wr, wr));
        adjust_weed_chance(rand(-wc, wc));
        if (prob(tm))
        {
            if (prob(50))
                add_random_traits(1, 1);
            else
                add_random_reagents(1, 1);
        }
    }

    // tg: code/modules/hydroponics/seeds.dm:174
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null) //Works with the Somatoray to modify plant variables.
    {
        if (hitting_projectile is not obj_projectile_energy_flora_yield)
            return await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        float rating = 1;
        if (loc is obj_machinery_hydroponics H)
            rating = Dm.num(H.rating);
        if (yield == 0)//Oh god don't divide by zero you'll doom us all.
            adjust_yield(1 * rating);
        else if (prob(1 / (yield * yield) * 100))//This formula gives you diminishing returns based on yield. 100% with 1 yield, decreasing to 25%, 11%, 6, 4, 2...
            adjust_yield(1 * rating);
        return BULLET_ACT_HIT;
    }

    // Harvest procs
    // tg: code/modules/hydroponics/seeds.dm:188
    public virtual object getYield()
    {
        float return_yield = yield;
        obj_machinery_hydroponics parent = loc as obj_machinery_hydroponics;
        // Handle hydroponics malus for soil lovers like rootcrops.
        // (a seed out of a tray reads parent.tray_flags of a non-tray: a runtime in tg; read null-safely)
        if (!Dm.truthy(Dm.bitand(parent?.tray_flags ?? 0, SOIL)) && get_gene(typeof(datum_plant_gene_trait_soil_lover)) != null)
            return_yield *= SOIL_LOVER_HYDRO_YIELD_MALUS;
        // Determine if the plant should get a yield bonus from bee pollination.
        foreach (datum_plant_gene_trait trait in Dm.each<datum_plant_gene_trait>(genes))
        {
            if (Dm.truthy(Dm.bitand(trait.trait_flags, TRAIT_NO_POLLINATION)))
                return return_yield;
        }

        if (parent != null)
        {
            if (parent.yieldmod == 0)
                return_yield = Dm.min(return_yield, 1);//1 if above zero, 0 otherwise
            else
                return_yield *= parent.yieldmod;
        }

        return Dm.round(return_yield);
    }

    // tg: code/modules/hydroponics/seeds.dm:208
    public virtual async DmTask<object> harvest(mob user = null)
    {
        ///Reference to the tray/soil the seeds are planted in.
        obj_machinery_hydroponics parent = loc as obj_machinery_hydroponics; //for ease of access
        ///Count used for creating the correct amount of results to the harvest.
        float t_amount = 0;
        ///List of plants all harvested from the same batch.
        DmList result = Dm.list();
        ///Tile of the harvester to deposit the growables.
        atom output_loc = (Dm.truthy(await parent.Adjacent(user)) ? user.drop_location() : parent.drop_location()) as atom; //needed for TK
        ///Name of the grown products.
        object product_name = null;
        ///The Number of products produced by the plant, typically the yield. Modified by certain traits.
        float product_count = Dm.num(getYield());

        while (t_amount < product_count)
        {
            obj_item_food_grown t_prod;
            if (instability >= 30 && Dm.truthy(Dm.bitand(seed_flags, MUTATE_EARLY)) && Dm.truthy(LAZYLEN(mutatelist)) && prob(instability / 3))
            {
                Type mutated_seed_type = pick(mutatelist) as Type;
                Type t_prod_type = Dm.initial(mutated_seed_type, (obj_item_seeds s) => s.product);
                if (t_prod_type == null)
                    continue;
                obj_item_seeds mutated_seed = Dm.@new(mutated_seed_type) as obj_item_seeds;
                foreach (datum_plant_gene_trait trait in Dm.each<datum_plant_gene_trait>(parent.myseed.genes))
                {
                    if (Dm.truthy(Dm.bitand(trait.mutability_flags, PLANT_GENE_MUTATABLE)) && Dm.truthy(trait.can_add(mutated_seed)))
                        Dm.append(ref mutated_seed.genes, trait.Copy());
                }
                t_prod = Dm.@new(t_prod_type, output_loc, Dm.named("new_seed", mutated_seed)) as obj_item_food_grown;
                t_prod.transform = Dm.initial(t_prod, "transform");
                t_prod.transform = Dm.mul(t_prod.transform, Dm.num(Dm.pow(Dm.sin((90 * Dm.num(((obj_item_seeds)t_prod.seed).potency)) / 100), 2)) + 0.5f); // TRANSFORM_USING_VARIABLE(t_prod.seed.potency, 100) + 0.5
                ADD_TRAIT(t_prod, TRAIT_PLANT_WILDMUTATE, INNATE_TRAIT);
                t_amount++;
                if (t_prod.seed != null)
                    ((obj_item_seeds)t_prod.seed).set_instability(Dm.round(instability * 0.5f));
                continue;
            }
            else
            {
                // Create a descendent seed so we can modify our offspring before creating a grown from it.
                obj_item_seeds descendent_seed = Copy() as obj_item_seeds;
                // Cuck potency if a soil lover such as a root crop is grown in water.
                if (!Dm.truthy(Dm.bitand(parent.tray_flags, SOIL)) && get_gene(typeof(datum_plant_gene_trait_soil_lover)) != null)
                    descendent_seed.set_potency(Dm.round(Dm.num(potency) * RANDOM_DECIMAL(SOIL_LOVER_HYDRO_POTENCY_MIN, SOIL_LOVER_HYDRO_POTENCY_MAX)));

                t_prod = Dm.@new(product, output_loc, Dm.named("new_seed", descendent_seed)) as obj_item_food_grown;
            }
            if (parent.myseed.plantname != Dm.initial(parent.myseed, (obj_item_seeds s) => s.plantname))
                t_prod.name = LOWER_TEXT(parent.myseed.plantname);
            if (Dm.truthy(productdesc))
                t_prod.desc = productdesc as string;
            obj_item_seeds prod_seed = t_prod.seed as obj_item_seeds;
            prod_seed.name = parent.myseed.name;
            prod_seed.desc = parent.myseed.desc;
            prod_seed.plantname = parent.myseed.plantname;
            result.Add(t_prod); // User gets a consumable
            if (t_prod == null)
                return null;
            t_amount++;
            product_name = parent.myseed.plantname;
        }
        if (product_count >= 1)
            SSblackbox.record_feedback("tally", "food_harvested", product_count, product_name);
        await parent.update_tray(user, product_count);

        return result;
    }

    /// Setters procs ///

    /**
     * Adjusts seed yield up or down according to adjustamt. (Max 10)
     */
    // tg: code/modules/hydroponics/seeds.dm:316
    public virtual void adjust_yield(object adjustamt = null)
    {
        if (yield == -1) // Unharvestable shouldn't suddenly turn harvestable
            return;

        float max_yield = MAX_PLANT_YIELD;
        float min_yield = 0;
        foreach (datum_plant_gene_trait trait in Dm.each<datum_plant_gene_trait>(genes))
        {
            if (Dm.truthy(Dm.bitand(trait.trait_flags, TRAIT_HALVES_YIELD)))
            {
                max_yield = Dm.round(max_yield / 2);
                break;
            }
        }
        if (get_gene(typeof(datum_plant_gene_trait_plant_type_fungal_metabolism)) != null)
            min_yield = FUNGAL_METAB_YIELD_MIN;

        yield = Dm.clamp(yield + Dm.num(adjustamt), min_yield, max_yield);
    }

    /**
     * Adjusts seed lifespan up or down according to adjustamt. (Max 100)
     */
    // tg: code/modules/hydroponics/seeds.dm:334
    public virtual void adjust_lifespan(object adjustamt = null)
    {
        lifespan = Dm.clamp(Dm.num(lifespan) + Dm.num(adjustamt), 10, MAX_PLANT_LIFESPAN);
    }

    /**
     * Adjusts seed endurance up or down according to adjustamt. (Max 100)
     */
    // tg: code/modules/hydroponics/seeds.dm:340
    public virtual void adjust_endurance(object adjustamt = null)
    {
        endurance = Dm.clamp(endurance + Dm.num(adjustamt), MIN_PLANT_ENDURANCE, MAX_PLANT_ENDURANCE);
    }

    /**
     * Adjusts seed production seed up or down according to adjustamt. (Max 10)
     */
    // tg: code/modules/hydroponics/seeds.dm:346
    public virtual void adjust_production(object adjustamt = null)
    {
        if (yield == -1)
            return;
        production = Dm.clamp(Dm.num(production) + Dm.num(adjustamt), 1, MAX_PLANT_PRODUCTION);
    }

    /**
     * Adjusts seed potency up or down according to adjustamt. (Max 100)
     */
    // tg: code/modules/hydroponics/seeds.dm:354
    public virtual void adjust_potency(object adjustamt = null)
    {
        if (potency == -1)
            return;
        potency = Dm.clamp(Dm.num(potency) + Dm.num(adjustamt), 0, MAX_PLANT_POTENCY);
    }

    /**
     * Adjusts seed instability up or down according to adjustamt. (Max 100)
     */
    // tg: code/modules/hydroponics/seeds.dm:362
    public virtual void adjust_instability(object adjustamt = null)
    {
        if (instability == -1)
            return;
        instability = Dm.clamp(instability + Dm.num(adjustamt), 0, MAX_PLANT_INSTABILITY);
    }

    /**
     * Adjusts seed weed grwoth speed up or down according to adjustamt. (Max 10)
     */
    // tg: code/modules/hydroponics/seeds.dm:370
    public virtual void adjust_weed_rate(object adjustamt = null)
    {
        weed_rate = Dm.clamp(weed_rate + Dm.num(adjustamt), 0, MAX_PLANT_WEEDRATE);
    }

    /**
     * Adjusts seed weed chance up or down according to adjustamt. (Max 67%)
     */
    // tg: code/modules/hydroponics/seeds.dm:376
    public virtual void adjust_weed_chance(object adjustamt = null)
    {
        weed_chance = Dm.clamp(weed_chance + Dm.num(adjustamt), 0, MAX_PLANT_WEEDCHANCE);
    }

    //Directly setting stats

    /**
     * Sets the plant's yield stat to the value of adjustamt. (Max 10, or 5 with some traits)
     */
    // tg: code/modules/hydroponics/seeds.dm:384
    public virtual void set_yield(object adjustamt = null)
    {
        if (yield == -1) // Unharvestable shouldn't suddenly turn harvestable
            return;

        float max_yield = MAX_PLANT_YIELD;
        float min_yield = 0;
        foreach (datum_plant_gene_trait trait in Dm.each<datum_plant_gene_trait>(genes))
        {
            if (Dm.truthy(Dm.bitand(trait.trait_flags, TRAIT_HALVES_YIELD)))
            {
                max_yield = Dm.round(max_yield / 2);
                break;
            }
        }
        if (get_gene(typeof(datum_plant_gene_trait_plant_type_fungal_metabolism)) != null)
            min_yield = FUNGAL_METAB_YIELD_MIN;

        yield = Dm.clamp(Dm.num(adjustamt), min_yield, max_yield);
    }

    /**
     * Sets the plant's lifespan stat to the value of adjustamt. (Max 100)
     */
    // tg: code/modules/hydroponics/seeds.dm:402
    public virtual void set_lifespan(object adjustamt = null)
    {
        lifespan = Dm.clamp(Dm.num(adjustamt), 10, MAX_PLANT_LIFESPAN);
    }

    /**
     * Sets the plant's endurance stat to the value of adjustamt. (Max 100)
     */
    // tg: code/modules/hydroponics/seeds.dm:408
    public virtual void set_endurance(object adjustamt = null)
    {
        endurance = Dm.clamp(Dm.num(adjustamt), MIN_PLANT_ENDURANCE, MAX_PLANT_ENDURANCE);
    }

    /**
     * Sets the plant's production stat to the value of adjustamt. (Max 10)
     */
    // tg: code/modules/hydroponics/seeds.dm:414
    public virtual void set_production(object adjustamt = null)
    {
        if (yield == -1)
            return;
        production = Dm.clamp(Dm.num(adjustamt), 1, MAX_PLANT_PRODUCTION);
    }

    /**
     * Sets the plant's potency stat to the value of adjustamt. (Max 100)
     */
    // tg: code/modules/hydroponics/seeds.dm:422
    public virtual void set_potency(object adjustamt = null)
    {
        if (potency == -1)
            return;
        potency = Dm.clamp(Dm.num(adjustamt), 0, MAX_PLANT_POTENCY);
    }

    /**
     * Sets the plant's instability stat to the value of adjustamt. (Max 100)
     */
    // tg: code/modules/hydroponics/seeds.dm:430
    public virtual void set_instability(object adjustamt = null)
    {
        if (instability == -1)
            return;
        instability = Dm.clamp(Dm.num(adjustamt), 0, MAX_PLANT_INSTABILITY);
    }

    /**
     * Sets the plant's weed production rate to the value of adjustamt. (Max 10)
     */
    // tg: code/modules/hydroponics/seeds.dm:438
    public virtual void set_weed_rate(object adjustamt = null)
    {
        weed_rate = Dm.clamp(Dm.num(adjustamt), 0, MAX_PLANT_WEEDRATE);
    }

    /**
     * Sets the plant's weed growth percentage to the value of adjustamt. (Max 67%)
     */
    // tg: code/modules/hydroponics/seeds.dm:444
    public virtual void set_weed_chance(object adjustamt = null)
    {
        weed_chance = Dm.clamp(Dm.num(adjustamt), 0, MAX_PLANT_WEEDCHANCE);
    }

    /**
     * Override for seeds with unique text for their analyzer. (No newlines at the start or end of unique text!)
     * Returns null if no unique data
     * Return an assoc list (label = text) to add a new line to the analyzer
     * Return an assoc list (label = list(text = tooltip, text = tooltip)) to add a new collapsible section to the analyzer
     */
    // tg: code/modules/hydroponics/seeds.dm:453
    public virtual object get_unique_analyzer_data()
    {
        return null;
    }

    /**
     * Override for seeds with special chem reactions.
     */
    // tg: code/modules/hydroponics/seeds.dm:459
    public virtual void on_chem_reaction(datum_reagents reagents = null)
    {
        return;
    }

    // tg: code/modules/hydroponics/seeds.dm:462
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        plantname = input as string;
        return Dm.text("[]", LOWER_TEXT(input));
    }

    // tg: code/modules/hydroponics/seeds.dm:466
    public override async DmTask<object> descformat(object input = null, mob_living user = null)
    {
        object dot = input;
        if (product != null && !Dm.truthy(productdesc))
            productdesc = Dm.initial(product, (obj_item p) => p.desc);
        object newproductdesc = await tgui_input_text(user, "Write a new product description", "Product Description", productdesc, max_length: MAX_DESC_LEN);
        if (newproductdesc == null)
            return dot;
        if (!Dm.truthy(await user.can_perform_action(this)))
            return dot;
        productdesc = newproductdesc;
        return dot;
    }

    // tg: code/modules/hydroponics/seeds.dm:477
    public override async DmTask rename_reset()
    {
        plantname = Dm.initial(this, (obj_item_seeds s) => s.plantname);
        productdesc = Dm.initial(this, (obj_item_seeds s) => s.productdesc);
    }

    // tg: code/modules/hydroponics/seeds.dm:481
    public virtual void randomize_stats()
    {
        set_lifespan(rand(25, 60));
        set_endurance(rand(15, 35));
        set_production(rand(2, 10));
        set_yield(rand(1, 10));
        set_potency(rand(10, 35));
        set_weed_rate(rand(1, 10));
        set_weed_chance(rand(5, 100));
        maturation = rand(6, 12);
    }

    // tg: code/modules/hydroponics/seeds.dm:491
    public virtual void add_random_reagents(float? lower = null, float? upper = null)
    {
        float amount_random_reagents = rand(lower ?? 0, upper ?? 2);
        for (float i = 1; i <= amount_random_reagents; i++)
        {
            float random_amount = rand(4, 15) * 0.01f; // this must be multiplied by 0.01, otherwise, it will not properly associate
            datum_plant_gene_reagent R = Dm.@new<datum_plant_gene_reagent>(get_random_reagent_id(), random_amount);
            if (Dm.truthy(R.can_add(this)))
            {
                if (!Dm.truthy(R.try_upgrade_gene(this)))
                    Dm.append(ref genes, R);
            }
            else
            {
                qdel(R);
            }
        }
        reagents_from_genes();
    }

    // tg: code/modules/hydroponics/seeds.dm:503
    public virtual void add_random_traits(float? lower = null, float? upper = null)
    {
        float amount_random_traits = rand(lower ?? 0, upper ?? 2);
        for (float i = 1; i <= amount_random_traits; i++)
        {
            object random_trait = pick(subtypesof(typeof(datum_plant_gene_trait)));
            datum_plant_gene_trait picked_random_trait = Dm.@new((Type)random_trait) as datum_plant_gene_trait;
            if (Dm.truthy(Dm.bitand(picked_random_trait.mutability_flags, PLANT_GENE_MUTATABLE)) && Dm.truthy(picked_random_trait.can_add(this)))
                Dm.append(ref genes, picked_random_trait);
            else
                qdel(picked_random_trait);
        }
    }

    // tg: code/modules/hydroponics/seeds.dm:513
    public virtual void add_random_plant_type(float? normal_plant_chance = null)
    {
        if (prob(normal_plant_chance ?? 75))
        {
            object random_plant_type = pick(subtypesof(typeof(datum_plant_gene_trait_plant_type)));
            datum_plant_gene_trait_plant_type P = Dm.@new((Type)random_plant_type) as datum_plant_gene_trait_plant_type;
            if (Dm.truthy(P.can_add(this)))
                Dm.append(ref genes, P);
            else
                qdel(P);
        }
    }

    // tg: code/modules/hydroponics/seeds.dm:522
    public virtual void remove_random_reagents(float? lower = null, float? upper = null)
    {
        float amount_random_reagents = rand(lower ?? 0, upper ?? 2);
        for (float i = 1; i <= amount_random_reagents; i++)
        {
            object chemical = pick(reagents_add);
            qdel(chemical as datum); // tg qdels a reagent typepath here (reagents_add holds types): qdel of a non-datum does nothing
        }
    }

    /**
     * Creates a graft from this plant.
     *
     * Creates a new graft from this plant.
     * Passes this plant to the new graft so it can make a copy.
     * Returns the created graft.
     */
    // tg: code/modules/hydroponics/seeds.dm:535
    public virtual object create_graft()
    {
        obj_item_graft snip = Dm.@new<obj_item_graft>(loc, this);
        return snip;
    }

    /**
     * Applies a graft to this plant.
     *
     * Adds the graft trait to this plant if possible.
     * Increases plant stats by 2/3 of the grafts stats to a maximum of 100 (10 for yield).
     * Returns TRUE if the graft could apply its trait successfully, FALSE if it fails to apply the trait.
     * NOTE even if the graft fails to apply the trait it still adjusts the plant's stats and reagents.
     *
     * Arguments:
     * - [snip][/obj/item/graft]: The graft being used applied to this plant.
     */
    // tg: code/modules/hydroponics/seeds.dm:550
    public virtual object apply_graft(obj_item_graft snip = null)
    {
        obj_item_seeds dna = snip.plant_dna as obj_item_seeds;
        if (dna == null)
            return null; // a graft made without a mother plant holds a seed path: a runtime in tg
        Type new_trait_type = dna.graft_gene ?? typeof(datum_plant_gene_trait_repeated_harvest);
        datum_plant_gene new_trait = Dm.@new(new_trait_type) as datum_plant_gene;
        if (Dm.truthy(new_trait?.can_add(this)))
            Dm.append(ref genes, new_trait);
        else
            new_trait = null;

        // Adjust stats based on graft stats
        set_lifespan(Dm.round(Dm.max(Dm.num(lifespan), (Dm.num(lifespan) + (2f / 3) * (Dm.num(dna.lifespan) - Dm.num(lifespan))))));
        set_endurance(Dm.round(Dm.max(endurance, (endurance + (2f / 3) * (dna.endurance - endurance)))));
        set_production(Dm.round(Dm.max(Dm.num(production), (Dm.num(production) + (2f / 3) * (Dm.num(dna.production) - Dm.num(production))))));
        set_weed_rate(Dm.round(Dm.max(weed_rate, (weed_rate + (2f / 3) * (dna.weed_rate - weed_rate)))));
        set_weed_chance(Dm.round(Dm.max(weed_chance, (weed_chance + (2f / 3) * (dna.weed_chance - weed_chance)))));
        set_yield(Dm.round(Dm.max(yield, (yield + (2f / 3) * (dna.yield - yield)))));

        // Add in any reagents, too.
        reagents_from_genes();
        // Return the new trait so the tray can know we were successful and display its name.
        return new_trait;
    }

    // tg: code/modules/hydroponics/seeds.dm:589
    public virtual void perform_reagent_pollination(obj_item_seeds donor = null)
    {
        DmList valid_reagents = Dm.list();
        foreach (datum_plant_gene_reagent donor_reagent in Dm.each<datum_plant_gene_reagent>(donor.genes))
        {
            bool repeated = false;
            foreach (datum_plant_gene_reagent receptor_reagent in Dm.each<datum_plant_gene_reagent>(genes))
            {
                if (Dm.eq(donor_reagent.reagent_id, receptor_reagent.reagent_id))
                {
                    if (receptor_reagent.rate < donor_reagent.rate)
                    {
                        receptor_reagent.rate = donor_reagent.rate;
                        // sucessful pollination/upgrade, we stop here.
                        reagents_from_genes();
                        return;
                    }
                    else
                    {
                        repeated = true;
                        break;
                    }
                }
            }

            if (!repeated)
                Dm.append(ref valid_reagents, donor_reagent);
        }

        if (Dm.truthy(length(valid_reagents)))
        {
            // pick a valid reagent that our receptor seed don't have and add the gene to it
            datum_plant_gene_reagent selected_reagent = pick(valid_reagents) as datum_plant_gene_reagent;
            Dm.append(ref genes, selected_reagent.Copy());
            reagents_from_genes();
        }
    }

    /// Returns a mutable appearance to be used as an overlay for the plant in hydro trays.
    // tg: code/modules/hydroponics/seeds.dm:614
    public virtual object get_tray_overlay(object age = null, object status = null, object tray_offset = null)
    {
        mutable_appearance plant_overlay = mutable_appearance(growing_icon, layer: OBJ_LAYER + 0.01f) as mutable_appearance;
        switch (status as string)
        {
            case HYDROTRAY_PLANT_DEAD:
                plant_overlay.icon_state = icon_dead as string;
                break;
            case HYDROTRAY_PLANT_HARVESTABLE:
                plant_overlay.icon_state = Dm.truthy(icon_harvest) ? icon_harvest as string : Dm.text("[][]", icon_grow, growthstages);
                break;
            default:
                float t_growthstate = Dm.clamp(Dm.round((Dm.num(age) / maturation) * growthstages), 1, growthstages);
                plant_overlay.icon_state = Dm.text("[][]", icon_grow, t_growthstate);
                break;
        }
        plant_overlay.pixel_z = plant_icon_offset + Dm.num(tray_offset);
        return plant_overlay;
    }

    /// Called when the seed is set in a tray
    // tg: code/modules/hydroponics/seeds.dm:628
    public virtual void on_planted(obj_machinery_hydroponics parent = null)
    {
        return;
    }

    /// Called when the seed is removed from a tray - possibly from being harvested, possibly from being uprooted
    // tg: code/modules/hydroponics/seeds.dm:632
    public virtual void on_unplanted(obj_machinery_hydroponics parent = null)
    {
        return;
    }
}

// tg: code/modules/hydroponics/seeds.dm:580
public partial class obj_item : obj
{
    // tg: code/modules/hydroponics/seeds.dm:580
    public virtual object get_plant_seed()
    {
        return null;
    }
}

// tg: code/modules/hydroponics/seeds.dm:583
public partial class obj_item_food_grown : obj_item_food
{
    // tg: code/modules/hydroponics/seeds.dm:583
    public override object get_plant_seed()
    {
        return seed;
    }
}

// tg: code/modules/hydroponics/seeds.dm:586
public partial class obj_item_grown : obj_item
{
    // tg: code/modules/hydroponics/seeds.dm:586
    public override object get_plant_seed()
    {
        return seed;
    }
}
