// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/botany.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file (the seed extractor's and the plant analyzer's seed data).

public static partial class GlobalProcs
{
    /// Returns a list of seed data generated from the given seeds.
    /// Filling the "name", "icon" and "icon_state" fields of the data is the caller's responsibility.
    // tg: code/__HELPERS/botany.dm:3
    public static async DmTask<object> generate_seed_data_from(obj_item_seeds seeds = null)
    {
        DmList seed_data = Dm.list();

        seed_data["lifespan"] = seeds.lifespan;
        seed_data["endurance"] = seeds.endurance;
        seed_data["maturation"] = seeds.maturation;
        seed_data["production"] = seeds.production;
        seed_data["yield"] = seeds.yield;
        seed_data["potency"] = seeds.potency;
        seed_data["instability"] = seeds.instability;

        DmList traits = Dm.list();
        foreach (datum_plant_gene_trait trait in Dm.each<datum_plant_gene_trait>(seeds.genes))
            Dm.append(ref traits, trait.type);
        seed_data["traits"] = traits;

        DmList reagents = Dm.list();
        foreach (datum_plant_gene_reagent reagent in Dm.each<datum_plant_gene_reagent>(seeds.genes))
            Dm.append(ref reagents, Dm.list(Dm.list(Dm.kv("name", reagent.name), Dm.kv("rate", reagent.rate))));
        seed_data["reagents"] = reagents;

        datum_plant_gene_trait_maxchem volume_mod_trait = Dm.locate_type(typeof(datum_plant_gene_trait_maxchem), seeds.genes) as datum_plant_gene_trait_maxchem;
        seed_data["volume_mod"] = volume_mod_trait != null ? volume_mod_trait.rate : 1;

        datum_plant_gene_trait_modified_volume volume_unit_trait = Dm.locate_type(typeof(datum_plant_gene_trait_modified_volume), seeds.genes) as datum_plant_gene_trait_modified_volume;
        seed_data["volume_units"] = volume_unit_trait != null ? volume_unit_trait.new_capacity : PLANT_REAGENT_VOLUME;

        DmList mutatelist = Dm.list();
        foreach (Type mutant in Dm.each_cast<Type>(seeds.mutatelist))
            Dm.append(ref mutatelist, Dm.initial(mutant, (obj_item_seeds s) => s.plantname));
        seed_data["mutatelist"] = mutatelist;

        if (ispath(seeds.product, typeof(obj_item)))
        {
            obj_item product = Dm.@new(seeds.product) as obj_item;

            DmList grind_results_names = Dm.list();
            foreach (Type reagent in Dm.each_cast<Type>(await product.grind_results()))
                Dm.append(ref grind_results_names, Dm.initial(reagent, (datum_reagent r) => r.name));
            seed_data["grind_results"] = grind_results_names;

            if (product is obj_item_food_grown grown_product)
            {
                Type distill_reagent = grown_product.distill_reagent;
                if (ispath(distill_reagent, typeof(datum_reagent)))
                    seed_data["distill_reagent"] = Dm.initial(distill_reagent, (datum_reagent r) => r.name);

                object juice_reagent = grown_product.juice_typepath();
                if (ispath(juice_reagent, typeof(datum_reagent)))
                    seed_data["juice_name"] = Dm.initial((Type)juice_reagent, (datum_reagent r) => r.name);
            }

            qdel(product);
        }

        return seed_data;
    }
}
