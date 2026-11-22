// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/scanning_plants.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the botanical scans of harvested plants (destructive), with a drawn plant gene
// or a wild mutation. The var block is generated (D111).

// tg: code/modules/experisci/experiment/types/scanning_plants.dm:1
public partial class datum_experiment_scanning_random_plants : datum_experiment_scanning_random
{
    // tg: code/modules/experisci/experiment/types/scanning_plants.dm:13
    [DmParams("techweb")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (Dm.truthy(possible_plant_genes.len))
        {
            foreach (object req_atom in Dm.each(required_atoms))
            {
                object chosen_gene = pick(possible_plant_genes);
                required_genes[req_atom] = chosen_gene;
            }
        }
    }

    // tg: code/modules/experisci/experiment/types/scanning_plants.dm:20
    public override async DmTask<object> serialize_progress_stage(Type target = null, DmList seen_instances = null)
    {
        return EXPERIMENT_PROG_INT(I18n.T("Scan samples of a harvested plant."),
            Dm.truthy(Dm.bitand(traits, EXPERIMENT_TRAIT_DESTRUCTIVE)) ? scanned[target] : seen_instances.len, required_atoms[target]);
    }
}

// tg: code/modules/experisci/experiment/types/scanning_plants.dm:24
public partial class datum_experiment_scanning_random_plants_traits : datum_experiment_scanning_random_plants
{
    // tg: code/modules/experisci/experiment/types/scanning_plants.dm:24
    public override async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        if (!(target is obj_item_food_grown))
            return FALSE;
        obj_item_food_grown crop = target as obj_item_food_grown;
        if (Dm.truthy(possible_plant_genes.len))
        {
            if (((obj_item_seeds)crop.seed).get_gene(required_genes[typepath]) == null)
                await experiment_handler.announce_message(I18n.T("Scanned plant is missing required genetics. Please scan a plant with the proper genetics."));
            return (await base.final_contributing_index_checks(experiment_handler, target, typepath) is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.num(((obj_item_seeds)crop.seed).get_gene(required_genes[typepath]) != null));
        }
        return await base.final_contributing_index_checks(experiment_handler, target, typepath);
    }

    // tg: code/modules/experisci/experiment/types/scanning_plants.dm:34
    public override async DmTask<object> serialize_progress_stage(Type target = null, DmList seen_instances = null)
    {
        if (Dm.truthy(possible_plant_genes.len))
        {
            Type gene = required_genes[target] as Type;
            return EXPERIMENT_PROG_INT(I18n.T("Scan samples of harvested plants with the trait: [].", Dm.initial(gene, (datum_plant_gene p) => p.name)),
                Dm.truthy(Dm.bitand(traits, EXPERIMENT_TRAIT_DESTRUCTIVE)) ? scanned[target] : seen_instances.len, required_atoms[target]);
        }
        return null;
    }
}

// tg: code/modules/experisci/experiment/types/scanning_plants.dm:40
public partial class datum_experiment_scanning_random_plants_wild : datum_experiment_scanning_random_plants
{
    // tg: code/modules/experisci/experiment/types/scanning_plants.dm:40
    public override async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        return (await base.final_contributing_index_checks(experiment_handler, target, typepath) is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.num(HAS_TRAIT(target, TRAIT_PLANT_WILDMUTATE)));
    }
}
